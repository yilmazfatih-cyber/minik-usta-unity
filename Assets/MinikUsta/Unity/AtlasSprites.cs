#nullable enable
// The v4 art atlas in Unity (paket V1; docs/ART_UNITY.md §3.2): Resources/art/atlas.json (TextAsset, parsed by the engine-
// independent `AtlasData`) + one PNG page per `atlases[i]` (TextAsset "art/atlas_N", bytes) → Texture2D via
// ImageConversion.LoadImage (no mipmaps: the 2 px page padding is for single-level sampling; bilinear, clamp; the CPU copy is
// dropped) → one Sprite per frame on first use: Sprite.Create(page, rect, pivot, ppu × scale, 0, FullRect, border (L, B, R, T)).
// Loaded once (TECH §10.6), cached for the app's life. A missing frame — or a missing atlas — falls back to the procedural
// placeholder tile of `FallbackTiles` (ProceduralSprites), so the game always draws something readable.
using System.Collections.Generic;
using MinikUsta.Presentation.Art;
using UnityEngine;

namespace MinikUsta.Unity
{
    /// <summary>A sprite to draw a frame with: the atlas frame (tint white) or its procedural fallback (tint from `FallbackTiles`).</summary>
    public readonly struct FrameSprite
    {
        public readonly Sprite? Sprite;
        /// <summary>The atlas frame (null for a fallback tile).</summary>
        public readonly AtlasFrame? Frame;
        public readonly Rgba Tint;
        public readonly bool Sliced;

        public FrameSprite(Sprite? sprite, AtlasFrame? frame, Rgba tint, bool sliced)
        {
            Sprite = sprite;
            Frame = frame;
            Tint = tint;
            Sliced = sliced;
        }

        public bool Ok => Sprite != null;

        /// <summary>Design px size of the sprite at scale 1 (a fallback tile is 100 design px = 1 world unit).</summary>
        public double DesignW => Frame != null ? Frame.DesignW : DesignSpace.PixelsPerUnit;
        public double DesignH => Frame != null ? Frame.DesignH : DesignSpace.PixelsPerUnit;
        public float PivotX => Frame != null ? Frame.PivotX : 0.5f;
        public float PivotY => Frame != null ? Frame.PivotY : 0.5f;
    }

    public static class AtlasSprites
    {
        public const string JsonResource = "art/atlas";

        private static AtlasData? data;
        private static Texture2D?[]? pages;
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        private static readonly HashSet<string> warned = new HashSet<string>();

        /// <summary>The parsed atlas.json (empty when it did not load).</summary>
        public static AtlasData Data
        {
            get
            {
                Load();
                return data!;
            }
        }

        /// <summary>atlas.json and every page loaded.</summary>
        public static bool Loaded { get; private set; }

        /// <summary>Reads atlas.json and decodes the pages once (idempotent).</summary>
        public static void Load()
        {
            if (data != null) return;
            ProceduralSprites.Build();
            var json = Resources.Load<TextAsset>(JsonResource);
            if (json == null)
            {
                Debug.LogWarning("Minik Usta: " + JsonResource + " not found; procedural placeholder art");
                data = AtlasData.Empty;
                pages = new Texture2D?[0];
                return;
            }
            try
            {
                data = AtlasData.Parse(json.text);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Minik Usta: atlas.json: " + e.Message + "; procedural placeholder art");
                data = AtlasData.Empty;
                pages = new Texture2D?[0];
                return;
            }
            pages = new Texture2D?[data.Pages.Count];
            bool all = true;
            for (int i = 0; i < data.Pages.Count; i++)
            {
                var page = data.Pages[i];
                var asset = Resources.Load<TextAsset>(page.Resource);
                if (asset == null)
                {
                    Debug.LogWarning("Minik Usta: atlas page " + page.Resource + " not found");
                    all = false;
                    continue;
                }
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.name = "MinikUsta." + page.Resource;
                if (!ImageConversion.LoadImage(tex, asset.bytes, true))
                {
                    Debug.LogWarning("Minik Usta: atlas page " + page.Resource + " is not a PNG");
                    all = false;
                    continue;
                }
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                if (tex.width != page.W || tex.height != page.H)
                    Debug.LogWarning("Minik Usta: atlas page " + page.Resource + " is " + tex.width + "×" + tex.height + ", atlas.json says " + page.W + "×" + page.H);
                pages[i] = tex;
            }
            Loaded = all && data.Count > 0;
        }

        /// <summary>The atlas sprite of `name` (created on first use), or null when the frame / its page is missing.</summary>
        public static Sprite? Get(string name)
        {
            Load();
            if (cache.TryGetValue(name, out var s)) return s;
            var f = data!.Find(name);
            if (f == null || pages == null || f.Page >= pages.Length) return null;
            var tex = pages[f.Page];
            if (tex == null) return null;
            f.UnityRect(data.Pages[f.Page].H, out float x, out float y, out float w, out float h);
            f.UnityBorder(out float l, out float b, out float r, out float t);
            var sprite = Sprite.Create(tex, new Rect(x, y, w, h), new Vector2(f.PivotX, f.PivotY), f.PixelsPerUnit(data.Ppu), 0, SpriteMeshType.FullRect, new Vector4(l, b, r, t));
            sprite.name = name;
            cache[name] = sprite;
            return sprite;
        }

        /// <summary>The sprite to draw `name` with: the atlas frame, else its procedural fallback (`FallbackTiles`), else none.</summary>
        public static FrameSprite Resolve(string name)
        {
            var s = Get(name);
            if (s != null) return new FrameSprite(s, data!.Find(name), Rgba.White, data.Find(name)!.Sliced);
            var fb = FallbackTiles.Of(name);
            if (Loaded && !FallbackTiles.IsProceduralOnly(name) && warned.Add(name)) Debug.LogWarning("Minik Usta: atlas frame " + name + " missing; placeholder tile");
            if (fb == null) return new FrameSprite(null, null, Rgba.White, false);
            return new FrameSprite(ProceduralSprites.Get(fb.Value.Tile), null, fb.Value.Tint, fb.Value.Sliced);
        }
    }
}
