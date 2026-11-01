#nullable enable
// The v4 art atlas contract (paket V1; docs/ART_UNITY.md §3, design-lead's frame contract): Resources/art/atlas.json read
// with the core JSON reader (Unity's JsonUtility cannot read the `frames` map). Pure: the Unity layer only turns a frame into
// `Sprite.Create(page, UnityRect, Pivot, PixelsPerUnit, 0, FullRect, Border)`; tests and the compose tool use the same numbers.
//
// Units (ART_UNITY §3.1): x y w h and border* are atlas px with a TOP-LEFT origin (PNG rows); pivot* / anchor* are 0..1 from
// the bottom-left (Unity's sprite pivot); `scale` = raster px per design px, so a sprite's pixels-per-unit is ppu × scale and
// a 180 px tile spans 1,2 world units = 120 design px. studD / symD are design px of a 120 px cell.
using System;
using System.Collections.Generic;
using MinikUsta.Core.Json;

namespace MinikUsta.Presentation.Art
{
    /// <summary>One page of the atlas: a PNG stored as a TextAsset (`Resources/<Resource>.bytes`).</summary>
    public sealed class AtlasPage
    {
        /// <summary>The atlas.json `file` field: the Resources path of the page's TextAsset ("art/atlas_0" → Resources/art/atlas_0.bytes).</summary>
        public readonly string File;
        public readonly int W;
        public readonly int H;

        public AtlasPage(string file, int w, int h)
        {
            File = file;
            W = w;
            H = h;
        }

        /// <summary>
        /// `Resources.Load` path of the page's TextAsset = the `file` field. V1 renamed the pages to plain names (`atlas_0.bytes`,
        /// file "art/atlas_0"): a dotted Resources name ("atlas-0.png") is a known risk in Unity (P3-13).
        /// </summary>
        public string Resource => File;
    }

    /// <summary>A frame of the atlas (ART_UNITY §3.1).</summary>
    public sealed class AtlasFrame
    {
        public readonly string Name;
        public readonly int Page;
        /// <summary>Atlas px, top-left origin.</summary>
        public readonly int X;
        public readonly int Y;
        public readonly int W;
        public readonly int H;
        /// <summary>0..1 from the bottom-left (Unity sprite pivot).</summary>
        public readonly float PivotX;
        public readonly float PivotY;
        /// <summary>9-slice borders, atlas px.</summary>
        public readonly int BorderL;
        public readonly int BorderT;
        public readonly int BorderR;
        public readonly int BorderB;
        /// <summary>Raster px per design px.</summary>
        public readonly double Scale;
        /// <summary>Block tiles: the stud centre (0..1 from the bottom-left); -1 when the frame has none.</summary>
        public readonly double AnchorX;
        public readonly double AnchorY;
        /// <summary>Block tiles: stud diameter and symbol box, design px of a 120 px cell; 0 when absent.</summary>
        public readonly double StudD;
        public readonly double SymD;

        public AtlasFrame(string name, int page, int x, int y, int w, int h, float pivotX, float pivotY, int borderL, int borderT, int borderR, int borderB,
            double scale, double anchorX = -1, double anchorY = -1, double studD = 0, double symD = 0)
        {
            Name = name;
            Page = page;
            X = x;
            Y = y;
            W = w;
            H = h;
            PivotX = pivotX;
            PivotY = pivotY;
            BorderL = borderL;
            BorderT = borderT;
            BorderR = borderR;
            BorderB = borderB;
            Scale = scale;
            AnchorX = anchorX;
            AnchorY = anchorY;
            StudD = studD;
            SymD = symD;
        }

        /// <summary>Design px size (atlas px / scale).</summary>
        public double DesignW => W / Scale;
        public double DesignH => H / Scale;

        public bool Sliced => BorderL > 0 || BorderT > 0 || BorderR > 0 || BorderB > 0;

        public bool HasAnchor => AnchorX >= 0 && AnchorY >= 0;

        /// <summary>The Unity sprite rect on a page `pageH` px high (bottom-left origin): (x, H − y − h, w, h).</summary>
        public void UnityRect(int pageH, out float x, out float y, out float w, out float h)
        {
            x = X;
            y = pageH - Y - H;
            w = W;
            h = H;
        }

        /// <summary>Sprite pixels-per-unit for `ppu` design px per world unit: ppu × scale (ART_UNITY §3.1).</summary>
        public float PixelsPerUnit(double ppu) => (float)(ppu * Scale);

        /// <summary>Unity `Vector4` border order: (L, B, R, T), atlas px.</summary>
        public void UnityBorder(out float l, out float b, out float r, out float t)
        {
            l = BorderL;
            b = BorderB;
            r = BorderR;
            t = BorderT;
        }

        public override string ToString() => Name + " p" + Page + " (" + X + "," + Y + " " + W + "×" + H + ")";
    }

    /// <summary>The parsed atlas.json.</summary>
    public sealed class AtlasData
    {
        private readonly Dictionary<string, AtlasFrame> frames = new Dictionary<string, AtlasFrame>(StringComparer.Ordinal);
        private readonly List<AtlasPage> pages = new List<AtlasPage>();

        /// <summary>atlas.json `version` (4 = the V0 art pack).</summary>
        public int Version { get; private set; }
        /// <summary>Design px per world unit (100).</summary>
        public double Ppu { get; private set; } = 100;

        public IReadOnlyList<AtlasPage> Pages => pages;

        public int Count => frames.Count;

        public IEnumerable<string> Names => frames.Keys;

        public IEnumerable<AtlasFrame> Frames => frames.Values;

        public bool Has(string name) => frames.ContainsKey(name);

        public AtlasFrame? Find(string name) => frames.TryGetValue(name, out var f) ? f : null;

        public AtlasFrame this[string name] => frames.TryGetValue(name, out var f) ? f : throw new KeyNotFoundException("atlas: no frame " + name);

        /// <summary>An empty atlas (no pages): every frame is missing, the views fall back to the procedural tiles.</summary>
        public static AtlasData Empty => new AtlasData();

        /// <summary>Parses atlas.json; throws `FormatException` on a broken contract (missing field, frame outside its page).</summary>
        public static AtlasData Parse(string json)
        {
            var root = JsonReader.Parse(json);
            if (!root.IsObject) throw new FormatException("atlas.json: not an object");
            var o = new AtlasData();
            o.Version = Int(root, "version");
            var ppu = root["ppu"];
            if (ppu != null && ppu.IsNumber) o.Ppu = ppu.AsDouble;
            var atlases = root["atlases"];
            if (atlases == null || !atlases.IsArray) throw new FormatException("atlas.json: no atlases[]");
            foreach (var a in atlases.Items) o.pages.Add(new AtlasPage(Str(a, "file"), Int(a, "w"), Int(a, "h")));
            var fr = root["frames"];
            if (fr == null || !fr.IsObject) throw new FormatException("atlas.json: no frames{}");
            foreach (var kv in fr.Members)
            {
                var f = kv.Value;
                int page = Int(f, "atlas");
                if (page < 0 || page >= o.pages.Count) throw new FormatException("atlas.json: frame " + kv.Key + " on page " + page);
                var frame = new AtlasFrame(kv.Key, page, Int(f, "x"), Int(f, "y"), Int(f, "w"), Int(f, "h"), (float)Num(f, "pivotX", 0.5), (float)Num(f, "pivotY", 0.5),
                    Int(f, "borderL", 0), Int(f, "borderT", 0), Int(f, "borderR", 0), Int(f, "borderB", 0), Num(f, "scale", 1),
                    Num(f, "anchorX", -1), Num(f, "anchorY", -1), Num(f, "studD", 0), Num(f, "symD", 0));
                var p = o.pages[page];
                if (frame.X < 0 || frame.Y < 0 || frame.W <= 0 || frame.H <= 0 || frame.X + frame.W > p.W || frame.Y + frame.H > p.H)
                    throw new FormatException("atlas.json: frame " + frame + " outside its " + p.W + "×" + p.H + " page");
                if (!(frame.Scale > 0)) throw new FormatException("atlas.json: frame " + kv.Key + " scale " + frame.Scale);
                o.frames[kv.Key] = frame;
            }
            return o;
        }

        private static int Int(JsonValue o, string key, int? fallback = null)
        {
            var v = o[key];
            if (v == null || !v.IsNumber)
            {
                if (fallback.HasValue) return fallback.Value;
                throw new FormatException("atlas.json: missing " + key);
            }
            return (int)Math.Round(v.AsDouble);
        }

        private static double Num(JsonValue o, string key, double fallback)
        {
            var v = o[key];
            return v != null && v.IsNumber ? v.AsDouble : fallback;
        }

        private static string Str(JsonValue o, string key)
        {
            var v = o[key];
            if (v == null || !v.IsString) throw new FormatException("atlas.json: missing " + key);
            return v.AsString;
        }
    }
}
