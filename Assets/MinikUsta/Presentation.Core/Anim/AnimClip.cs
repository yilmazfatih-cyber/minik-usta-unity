#nullable enable
// One pre-rendered 3D clip (paket B2; D-100; ART_3D §7, art/3d/blender/king/README.md, art/3d/blender/env/README.md): the frame
// name shown at every timeline index (24 fps for the character and most FX, 60 fps for fx_lock_shine), an optional loop range,
// an optional `out` sequence back to the rest pose K0, and named event frames (sound / haptic / gameplay hooks). The clip
// carries only data; `AnimPlayer` plays it, `ClipLibrary` reads it from king.json / fx.json.
using System;
using System.Collections.Generic;

namespace MinikUsta.Presentation.Anim
{
    public sealed class AnimClip
    {
        private static readonly IReadOnlyDictionary<int, string> NoEvents = new Dictionary<int, string>();

        public AnimClip(string name, string[] frames, float fps, int loopStart = -1, int loopEnd = -1, string[]? @out = null,
            IReadOnlyDictionary<int, string>? events = null, double canvasW = 0, double canvasH = 0, double pivotX = 0.5, double pivotY = 0,
            double scale = 1, string set = "", bool tinted = false, string? aliasOf = null)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("clip name", nameof(name));
            if (frames == null || frames.Length == 0) throw new ArgumentException("clip " + name + ": no frames", nameof(frames));
            if (!(fps > 0)) throw new ArgumentOutOfRangeException(nameof(fps), "clip " + name + ": fps " + fps);
            if (loopStart >= 0 || loopEnd >= 0)
            {
                if (loopStart < 0 || loopEnd < loopStart || loopEnd >= frames.Length)
                    throw new ArgumentOutOfRangeException(nameof(loopStart), "clip " + name + ": loop " + loopStart + ".." + loopEnd + " of " + frames.Length);
            }
            if (!(scale > 0)) throw new ArgumentOutOfRangeException(nameof(scale), "clip " + name + ": scale " + scale);
            Name = name;
            Frames = frames;
            Fps = fps;
            LoopStart = loopStart;
            LoopEnd = loopEnd;
            Out = @out ?? new string[0];
            Events = events ?? NoEvents;
            CanvasW = canvasW;
            CanvasH = canvasH;
            PivotX = pivotX;
            PivotY = pivotY;
            Scale = scale;
            Set = set;
            Tinted = tinted;
            AliasOf = aliasOf;
        }

        public string Name { get; }
        /// <summary>Frame name per timeline index (repeated names hold a drawing on twos / fours).</summary>
        public string[] Frames { get; }
        public float Fps { get; }
        /// <summary>Loop range (timeline indices, inclusive); −1 = the clip plays once.</summary>
        public int LoopStart { get; }
        public int LoopEnd { get; }
        /// <summary>Exit frames back to K0 when a loop is released (empty = none).</summary>
        public string[] Out { get; }
        /// <summary>Event name per timeline index (king: land, beat, point_hit …; fx: impact, grab, splat …).</summary>
        public IReadOnlyDictionary<int, string> Events { get; }
        /// <summary>The clip canvas (design px) and its pivot (0..1 from the bottom-left): the point the clip is placed by.</summary>
        public double CanvasW { get; }
        public double CanvasH { get; }
        public double PivotX { get; }
        public double PivotY { get; }
        /// <summary>Raster px per design px the clip is drawn at (king corner 0,88125, stage 0,705; fx per clip).</summary>
        public double Scale { get; }
        /// <summary>King: "corner" | "stage"; fx: "" .</summary>
        public string Set { get; }
        /// <summary>Rendered white: the caller tints it (paint splash, confetti).</summary>
        public bool Tinted { get; }
        /// <summary>king_home_idle → king_idle (same drawings, stage scale).</summary>
        public string? AliasOf { get; }

        public bool Loops => LoopStart >= 0;
        public int Length => Frames.Length;
        public int LoopSpan => Loops ? LoopEnd - LoopStart + 1 : 0;
        public float FrameMs => 1000f / Fps;
        /// <summary>One pass of the timeline (intro and the loop once), ms.</summary>
        public float DurationMs => Frames.Length * FrameMs;
        /// <summary>The clip returns to its first drawing at the end of its timeline (king_talk, king_cheer): a release waits for the pass end.</summary>
        public bool EndsOnFirst => Frames[Frames.Length - 1] == Frames[0];

        /// <summary>The first drawing (K0 for every king clip).</summary>
        public string First => Frames[0];

        /// <summary>Every distinct frame name of the clip (timeline and out), in first-use order.</summary>
        public IEnumerable<string> Drawings()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var f in Frames) if (seen.Add(f)) yield return f;
            foreach (var f in Out) if (seen.Add(f)) yield return f;
        }

        public override string ToString() => Name + " (" + Frames.Length + " @ " + Fps + " fps" + (Loops ? ", loop " + LoopStart + "–" + LoopEnd : "") + ")";
    }

    /// <summary>A named event frame reached by a player.</summary>
    public readonly struct AnimEvent
    {
        public readonly AnimClip Clip;
        public readonly string Name;
        public readonly int Index;

        public AnimEvent(AnimClip clip, string name, int index)
        {
            Clip = clip;
            Name = name;
            Index = index;
        }

        public override string ToString() => Clip.Name + "@" + Index + ":" + Name;
    }
}
