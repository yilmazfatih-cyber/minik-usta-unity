#nullable enable
// The 3D clip libraries of paket B1/B1k (D-100) as one engine-independent contract (paket B2):
//   Resources/art3d/king.json + king_N.bytes  — Usta Kral (D-101): 71 drawings + 6 busts, 9 clips, still / deprecated aliases
//   Resources/art3d/fx.json   + fx_N.bytes    — 12 FX clips (144 drawings), the 8 xl icons, the 7 home parallax layers and the staged tree
//                                               house (`stages.town_ch1`: base + 10 level parts, 10 win-window pieces)
//   Resources/art3d/crew.json + crew_N.bytes  — Tuna and Kepçe in 3D (art turn 1): tuna_home_idle, kepce_home_idle, kepce_corner_idle
// The frame fields are the atlas.json ones (x y w h in atlas px, top-left; pivot 0..1 from the bottom-left of the TRIMMED rect,
// may lie outside 0..1 for fx; scale = raster px per design px; borders 0), so `AtlasData.Parse` reads the pages and frames and
// this class adds what atlas.json has no field for: the trim offset (ox oy, cw ch), the clips, the aliases and the layers.
// Pure: the Unity layer (`ClipSprites`) only turns a frame into a Sprite; the tests read the shipped files.
using System;
using System.Collections.Generic;
using MinikUsta.Core.Json;
using MinikUsta.Presentation.Art;

namespace MinikUsta.Presentation.Anim
{
    /// <summary>`aliases.<name>` of king.json: a still pose (`chr_king_still_cheer` → `chr_king_cheer_05`) or a deprecated v4 name.</summary>
    public sealed class ClipAlias
    {
        public readonly string Name;
        public readonly string Of;
        /// <summary>The scale the alias is drawn at (stills: the stage set 0,645; busts 0,75).</summary>
        public readonly double Scale;
        public readonly bool Deprecated;

        public ClipAlias(string name, string of, double scale, bool deprecated)
        {
            Name = name;
            Of = of;
            Scale = scale;
            Deprecated = deprecated;
        }
    }

    /// <summary>`layers.<name>` of fx.json: one home parallax layer (design px on the 1080 × 1920 canvas, BOTTOM-left origin).</summary>
    public sealed class ParallaxLayer
    {
        public readonly string Name;
        public readonly string Frame;
        public readonly double X;
        public readonly double Y;
        public readonly double W;
        public readonly double H;
        /// <summary>0 = fixed (sky) … 1 = moves with the foreground.</summary>
        public readonly double Parallax;
        public readonly int Z;

        public ParallaxLayer(string name, string frame, double x, double y, double w, double h, double parallax, int z)
        {
            Name = name;
            Frame = frame;
            X = x;
            Y = y;
            W = w;
            H = h;
            Parallax = parallax;
            Z = z;
        }

        /// <summary>The layer rectangle on the 1080 × 1920 canvas with a TOP-left origin (UiNode coordinates).</summary>
        public PxRect TopLeftRect => new PxRect(X, 1920 - Y - H, W, H);
    }

    /// <summary>
    /// `stages.<name>` of fx.json (art turn 1): a staged build on the home screen. `Base` is the bare site, `Parts[i]` the piece that
    /// level i + 1 adds; every frame is trimmed and carries the stage pivot (a point of the shared canvas, relative to ITS trimmed rect,
    /// may lie outside 0..1), so drawing each of them with its pivot at one anchor stacks them exactly. `LevelPieces` are the win-window
    /// pictures (360 × 360 design px canvas, pivot at the centre).
    /// </summary>
    public sealed class StageSet
    {
        public readonly string Name;
        public readonly double CanvasW;
        public readonly double CanvasH;
        public readonly AtlasFrame Base;
        public readonly IReadOnlyList<AtlasFrame> Parts;
        /// <summary>Win-window piece per level number (1 … 10).</summary>
        public readonly IReadOnlyDictionary<int, AtlasFrame> LevelPieces;

        public StageSet(string name, double canvasW, double canvasH, AtlasFrame @base, IReadOnlyList<AtlasFrame> parts, IReadOnlyDictionary<int, AtlasFrame> levelPieces)
        {
            Name = name;
            CanvasW = canvasW;
            CanvasH = canvasH;
            Base = @base;
            Parts = parts;
            LevelPieces = levelPieces;
        }

        /// <summary>The pieces drawn once `won` levels are won: the base, then `Parts[0 … won − 1]` (clamped to 0 … Parts.Count).</summary>
        public List<AtlasFrame> Frames(int won)
        {
            var o = new List<AtlasFrame> { Base };
            int n = Math.Max(0, Math.Min(Parts.Count, won));
            for (int i = 0; i < n; i++) o.Add(Parts[i]);
            return o;
        }

        /// <summary>The win-window piece of level `level` (null when the stage has none).</summary>
        public AtlasFrame? PieceOf(int level) => LevelPieces.TryGetValue(level, out var f) ? f : null;
    }

    public sealed class ClipLibrary
    {
        private readonly Dictionary<string, AnimClip> clips = new Dictionary<string, AnimClip>(StringComparer.Ordinal);
        private readonly Dictionary<string, ClipAlias> aliases = new Dictionary<string, ClipAlias>(StringComparer.Ordinal);
        private readonly List<ParallaxLayer> layers = new List<ParallaxLayer>();
        private readonly Dictionary<string, StageSet> stages = new Dictionary<string, StageSet>(StringComparer.Ordinal);

        private ClipLibrary(string id, AtlasData atlas)
        {
            Id = id;
            Atlas = atlas;
        }

        /// <summary>"king" | "fx" | "crew".</summary>
        public string Id { get; }
        /// <summary>The pages and frames (atlas.json fields).</summary>
        public AtlasData Atlas { get; }
        public int Version { get; private set; }
        /// <summary>RGBA32 bytes of every page (the file's `memoryBytesRGBA32`, or computed).</summary>
        public long MemoryBytes { get; private set; }

        public IReadOnlyDictionary<string, AnimClip> Clips => clips;
        public IReadOnlyDictionary<string, ClipAlias> Aliases => aliases;
        /// <summary>Home parallax layers, back to front (z order).</summary>
        public IReadOnlyList<ParallaxLayer> Layers => layers;
        /// <summary>fx.json `stages` (the staged tree house of chapter 1).</summary>
        public IReadOnlyDictionary<string, StageSet> Stages => stages;

        public StageSet? Stage(string name) => stages.TryGetValue(name, out var st) ? st : null;

        public AnimClip? Clip(string name) => clips.TryGetValue(name, out var c) ? c : null;

        public bool HasClip(string name) => clips.ContainsKey(name);

        /// <summary>A frame of the library, an alias resolved to its frame (stills, deprecated names) — or null.</summary>
        public AtlasFrame? Find(string name)
        {
            var f = Atlas.Find(name);
            if (f != null) return f;
            return aliases.TryGetValue(name, out var a) ? Atlas.Find(a.Of) : null;
        }

        public bool Has(string name) => Find(name) != null;

        /// <summary>
        /// The still pose of a clip in reduced motion (ART_3D §7.1 rule 12): a king clip → `chr_king_still_<pose>`; a crew clip → its first
        /// drawing (`chr_tuna_home_idle_00`, `chr_kepce_idle_00`); null for fx.
        /// </summary>
        public string? StillOf(AnimClip clip)
        {
            if (Id == "crew") return clip.First;
            string pose = (clip.AliasOf ?? clip.Name).Replace("king_home_idle", "king_idle");
            if (!pose.StartsWith("king_", StringComparison.Ordinal)) return null;
            return aliases.TryGetValue("chr_king_still_" + pose.Substring(5), out var a) ? a.Of : null;
        }

        /// <summary>Parses king.json / fx.json; throws `FormatException` on a broken contract (missing field, a clip frame that does not exist).</summary>
        public static ClipLibrary Parse(string id, string json)
        {
            var atlas = AtlasData.Parse(json);
            var root = JsonReader.Parse(json);
            var o = new ClipLibrary(id, atlas);
            var v = root["version"];
            o.Version = v != null && v.IsNumber ? (int)v.AsDouble : 0;
            var mem = root["memoryBytesRGBA32"];
            long computed = 0;
            foreach (var p in atlas.Pages) computed += 4L * p.W * p.H;
            o.MemoryBytes = mem != null && mem.IsNumber ? (long)mem.AsDouble : computed;
            var al = root["aliases"];
            if (al != null && al.IsObject)
            {
                foreach (var kv in al.Members)
                {
                    string of = Str(kv.Value, "of", id + " alias " + kv.Key);
                    if (!atlas.Has(of)) throw new FormatException(id + ".json: alias " + kv.Key + " → missing frame " + of);
                    var dep = kv.Value["deprecated"];
                    o.aliases[kv.Key] = new ClipAlias(kv.Key, of, Num(kv.Value, "scale", 1), dep != null && dep.IsBool && dep.AsBool);
                }
            }
            var cl = root["clips"];
            if (cl != null && cl.IsObject)
            {
                foreach (var kv in cl.Members) o.clips[kv.Key] = ParseClip(o, kv.Key, kv.Value);
            }
            var ly = root["layers"];
            if (ly != null && ly.IsObject)
            {
                foreach (var kv in ly.Members)
                {
                    var l = kv.Value;
                    string frame = Str(l, "frame", id + " layer " + kv.Key);
                    if (!atlas.Has(frame)) throw new FormatException(id + ".json: layer " + kv.Key + " → missing frame " + frame);
                    o.layers.Add(new ParallaxLayer(kv.Key, frame, Num(l, "x", 0), Num(l, "y", 0), Num(l, "w", 0), Num(l, "h", 0), Num(l, "parallax", 0), (int)Num(l, "z", 0)));
                }
                o.layers.Sort((a, b) => a.Z.CompareTo(b.Z));
            }
            var st = root["stages"];
            if (st != null && st.IsObject)
            {
                foreach (var kv in st.Members) o.stages[kv.Key] = ParseStage(o, kv.Key, kv.Value);
            }
            return o;
        }

        private static StageSet ParseStage(ClipLibrary lib, string name, JsonValue s)
        {
            string where = lib.Id + " stage " + name;
            double cw = 0, ch = 0;
            var canvas = s["canvas"];
            if (canvas != null && canvas.IsArray && canvas.Count == 2)
            {
                cw = canvas[0].AsDouble;
                ch = canvas[1].AsDouble;
            }
            AtlasFrame Need(string frame)
            {
                return lib.Atlas.Find(frame) ?? throw new FormatException(where + ": missing frame " + frame);
            }
            var baseFrame = Need(Str(s, "base", where));
            var parts = new List<AtlasFrame>();
            var pj = s["parts"];
            if (pj == null || !pj.IsArray) throw new FormatException(where + ": no parts[]");
            for (int i = 0; i < pj.Count; i++) parts.Add(Need(pj[i].IsString ? pj[i].AsString : throw new FormatException(where + ": part " + i)));
            var pieces = new Dictionary<int, AtlasFrame>();
            var lp = s["levelPieces"];
            if (lp != null && lp.IsObject)
            {
                foreach (var kv in lp.Members)
                {
                    if (!int.TryParse(kv.Key, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int level))
                        throw new FormatException(where + ": level piece key " + kv.Key);
                    pieces[level] = Need(kv.Value.IsString ? kv.Value.AsString : throw new FormatException(where + ": level piece " + kv.Key));
                }
            }
            return new StageSet(name, cw, ch, baseFrame, parts, pieces);
        }

        private static AnimClip ParseClip(ClipLibrary lib, string name, JsonValue c)
        {
            string where = lib.Id + " clip " + name;
            var fr = c["frames"];
            if (fr == null || !fr.IsArray || fr.Count == 0) throw new FormatException(where + ": no frames[]");
            var frames = new string[fr.Count];
            for (int i = 0; i < frames.Length; i++)
            {
                var f = fr[i];
                if (!f.IsString) throw new FormatException(where + ": frame " + i + " is not a name");
                frames[i] = f.AsString;
                if (!lib.Atlas.Has(frames[i])) throw new FormatException(where + ": missing frame " + frames[i]);
            }
            int ls = -1, le = -1;
            var loop = c["loop"];
            if (loop != null && loop.IsObject)
            {
                ls = (int)Num(loop, "start", -1);
                le = (int)Num(loop, "end", -1);
            }
            string[]? outFrames = null;
            var of = c["out"];
            if (of != null && of.IsArray)
            {
                outFrames = new string[of.Count];
                for (int i = 0; i < outFrames.Length; i++)
                {
                    outFrames[i] = of[i].IsString ? of[i].AsString : throw new FormatException(where + ": out " + i);
                    if (!lib.Atlas.Has(outFrames[i])) throw new FormatException(where + ": missing out frame " + outFrames[i]);
                }
            }
            var events = new Dictionary<int, string>();
            var ev = c["events"];
            if (ev != null && ev.IsObject)
            {
                foreach (var kv in ev.Members)
                {
                    if (!int.TryParse(kv.Key, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int at) || at < 0 || at >= frames.Length)
                        throw new FormatException(where + ": event frame " + kv.Key);
                    if (kv.Value.IsString) events[at] = kv.Value.AsString;
                }
            }
            double cw = 0, ch = 0, px = 0.5, py = 0;
            var canvas = c["canvas"];
            if (canvas != null && canvas.IsArray && canvas.Count == 2)
            {
                cw = canvas[0].AsDouble;
                ch = canvas[1].AsDouble;
            }
            var pivot = c["pivot"];
            if (pivot != null && pivot.IsArray && pivot.Count == 2)
            {
                px = pivot[0].AsDouble;
                py = pivot[1].AsDouble;
            }
            var set = c["set"];
            var tint = c["tint"];
            var alias = c["aliasOf"];
            return new AnimClip(name, frames, (float)Num(c, "fps", 24), ls, le, outFrames, events, cw, ch, px, py, Num(c, "scale", 1),
                set != null && set.IsString ? set.AsString : "", tint != null && tint.IsString, alias != null && alias.IsString ? alias.AsString : null);
        }

        private static double Num(JsonValue o, string key, double fallback)
        {
            var v = o[key];
            return v != null && v.IsNumber ? v.AsDouble : fallback;
        }

        private static string Str(JsonValue o, string key, string where)
        {
            var v = o[key];
            if (v == null || !v.IsString) throw new FormatException(where + ": missing " + key);
            return v.AsString;
        }
    }

    /// <summary>
    /// Every 3D library the game draws from (the v4 atlas stays in `AtlasData`): Usta Kral, the FX (and the staged tree house) and the
    /// crew (Tuna and Kepçe, art turn 1). Any of them may be missing (a build without the files, a parse error): the views then fall
    /// back to the v4 stills, nothing breaks.
    /// </summary>
    public sealed class ArtSet
    {
        public const string KingResource = "art3d/king";
        public const string FxResource = "art3d/fx";
        public const string CrewResource = "art3d/crew";
        /// <summary>fx.json `stages` key of chapter 1's tree house.</summary>
        public const string TownStageName = "town_ch1";

        public ArtSet(ClipLibrary? king, ClipLibrary? fx, ClipLibrary? crew = null)
        {
            King = king;
            Fx = fx;
            Crew = crew;
        }

        public ClipLibrary? King { get; }
        public ClipLibrary? Fx { get; }
        public ClipLibrary? Crew { get; }

        public static ArtSet Empty { get; } = new ArtSet(null, null, null);

        /// <summary>The staged tree house of chapter 1 (null without fx.json or without its `stages`).</summary>
        public StageSet? Town => Fx?.Stage(TownStageName);

        /// <summary>Reads the three libraries with `read(resource)` (null text = missing); a broken file is reported through `log` and left out.</summary>
        public static ArtSet Load(Func<string, string?> read, Action<string>? log = null)
        {
            return new ArtSet(TryParse("king", read(KingResource), log), TryParse("fx", read(FxResource), log), TryParse("crew", read(CrewResource), log));
        }

        private static ClipLibrary? TryParse(string id, string? json, Action<string>? log)
        {
            if (json == null)
            {
                log?.Invoke("Minik Usta: art3d/" + id + ".json not found; v4 stills");
                return null;
            }
            try
            {
                return ClipLibrary.Parse(id, json);
            }
            catch (Exception e)
            {
                log?.Invoke("Minik Usta: art3d/" + id + ".json: " + e.Message + "; v4 stills");
                return null;
            }
        }

        public IEnumerable<ClipLibrary> Libraries
        {
            get
            {
                if (King != null) yield return King;
                if (Fx != null) yield return Fx;
                if (Crew != null) yield return Crew;
            }
        }

        public AnimClip? Clip(string name) => King?.Clip(name) ?? Fx?.Clip(name) ?? Crew?.Clip(name);

        /// <summary>The library holding frame (or alias) `name`.</summary>
        public ClipLibrary? LibraryOf(string name)
        {
            if (King != null && King.Has(name)) return King;
            if (Fx != null && Fx.Has(name)) return Fx;
            if (Crew != null && Crew.Has(name)) return Crew;
            return null;
        }

        public AtlasFrame? Frame(string name) => King?.Find(name) ?? Fx?.Find(name) ?? Crew?.Find(name);

        public bool Has(string name) => Frame(name) != null;

        /// <summary>The library a clip belongs to.</summary>
        public ClipLibrary? Owner(AnimClip clip)
        {
            if (King != null && King.Clip(clip.Name) == clip) return King;
            if (Fx != null && Fx.Clip(clip.Name) == clip) return Fx;
            if (Crew != null && Crew.Clip(clip.Name) == clip) return Crew;
            return null;
        }
    }
}
