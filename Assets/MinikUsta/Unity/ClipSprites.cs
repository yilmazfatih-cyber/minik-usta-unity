#nullable enable
// The 3D clip libraries of paket B1/B1k in Unity (paket B2; D-100): Resources/art3d/king.json + king_N.bytes and fx.json + fx_N.bytes,
// parsed by the engine-independent `ClipLibrary` (`ArtSet`). The pages are decoded LAZILY, one page on the first frame asked from it
// (ImageConversion.LoadImage, the `AtlasSprites` path: RGBA32, no mipmaps, bilinear, clamp, CPU copy dropped), and the TextAsset that
// held the PNG bytes is unloaded at once. So the home page pays only for the king's pages and fx_1, the level screen for the king and
// fx_0; nothing is decoded before it is shown. Every decode is logged with its time (`[minik] art3d decode …`), the WebGL first-load
// cost the B2 brief asks to measure. One Sprite per frame on first use: Sprite.Create(page, rect, pivot, 100 × scale, 0, FullRect)
// — the pivot of an fx frame may lie outside 0..1 (the clip canvas pivot relative to the trimmed rect), which Sprite.Create accepts.
using System.Collections.Generic;
using MinikUsta.Presentation.Anim;
using MinikUsta.Presentation.Art;
using UnityEngine;

namespace MinikUsta.Unity
{
    public static class ClipSprites
    {
        private sealed class Lib
        {
            public ClipLibrary Data = null!;
            public Texture2D?[] Pages = new Texture2D?[0];
            public bool[] Tried = new bool[0];
        }

        private static readonly List<Lib> libs = new List<Lib>();
        private static readonly Dictionary<string, Sprite?> cache = new Dictionary<string, Sprite?>();

        /// <summary>The libraries in use (`ArtSet.Empty` until `Use`).</summary>
        public static ArtSet Art { get; private set; } = ArtSet.Empty;

        /// <summary>Milliseconds spent decoding pages so far (WebGL first-load cost).</summary>
        public static float DecodeMs { get; private set; }

        /// <summary>Reads king.json and fx.json (Resources) once; a missing / broken file only logs a warning.</summary>
        public static ArtSet Load()
        {
            if (libs.Count > 0 || Art != ArtSet.Empty) return Art;
            var art = ArtSet.Load(name =>
            {
                var a = Resources.Load<TextAsset>(name);
                return a != null ? a.text : null;
            }, message => Debug.LogWarning(message));
            Use(art);
            return art;
        }

        /// <summary>Uses `art` from now on (pages decode on first use).</summary>
        public static void Use(ArtSet art)
        {
            Art = art;
            libs.Clear();
            cache.Clear();
            foreach (var l in art.Libraries)
                libs.Add(new Lib { Data = l, Pages = new Texture2D?[l.Atlas.Pages.Count], Tried = new bool[l.Atlas.Pages.Count] });
        }

        /// <summary>The frame (or alias) `name` of a 3D library, null when none has it.</summary>
        public static AtlasFrame? Frame(string name) => Art.Frame(name);

        /// <summary>The sprite of frame `name` (its page decoded on first use), or null.</summary>
        public static Sprite? Get(string name)
        {
            if (cache.TryGetValue(name, out var s)) return s;
            s = null;
            foreach (var l in libs)
            {
                var f = l.Data.Find(name);
                if (f == null) continue;
                var tex = Page(l, f.Page);
                if (tex == null) break;
                f.UnityRect(l.Data.Atlas.Pages[f.Page].H, out float x, out float y, out float w, out float h);
                s = Sprite.Create(tex, new Rect(x, y, w, h), new Vector2(f.PivotX, f.PivotY), f.PixelsPerUnit(l.Data.Atlas.Ppu), 0, SpriteMeshType.FullRect);
                s.name = name;
                break;
            }
            cache[name] = s;
            return s;
        }

        /// <summary>Decodes the pages of a library now (the boot can warm the king before the home screen shows).</summary>
        public static void Warm(string libraryId)
        {
            foreach (var l in libs)
                if (l.Data.Id == libraryId)
                    for (int i = 0; i < l.Pages.Length; i++) Page(l, i);
        }

        private static Texture2D? Page(Lib l, int index)
        {
            if (index < 0 || index >= l.Pages.Length) return null;
            if (l.Tried[index]) return l.Pages[index];
            l.Tried[index] = true;
            var info = l.Data.Atlas.Pages[index];
            float t0 = Time.realtimeSinceStartup;
            var asset = Resources.Load<TextAsset>(info.Resource);
            if (asset == null)
            {
                Debug.LogWarning("Minik Usta: art3d page " + info.Resource + " not found");
                return null;
            }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.name = "MinikUsta." + info.Resource;
            bool ok = ImageConversion.LoadImage(tex, asset.bytes, true);
            Resources.UnloadAsset(asset); // the PNG bytes are not needed once decoded
            if (!ok)
            {
                Debug.LogWarning("Minik Usta: art3d page " + info.Resource + " is not a PNG");
                return null;
            }
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            float ms = (Time.realtimeSinceStartup - t0) * 1000f;
            DecodeMs += ms;
            Debug.Log("[minik] art3d decode " + info.Resource + " " + tex.width + "x" + tex.height + " " + System.Math.Round(ms) + " ms (total " + System.Math.Round(DecodeMs) + " ms)");
            l.Pages[index] = tex;
            return tex;
        }
    }
}
