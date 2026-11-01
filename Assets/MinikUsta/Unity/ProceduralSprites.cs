#nullable enable
// Placeholder sprites (Faz 3 Q1; TECH §10.6 "dokular açılışta bir kez üretilir"): the engine-independent `Atlas` is
// rasterised once at boot, copied into ONE Texture2D and cut into one Sprite per tile. Every board, block, shadow and HUD
// sprite shares this texture (one material: the sprite batcher draws the board in a few calls). Cached for the app's life.
using MinikUsta.Presentation.Art;
using UnityEngine;

namespace MinikUsta.Unity
{
    public static class ProceduralSprites
    {
        private static Sprite[]? sprites;
        private static Texture2D? texture;

        /// <summary>Builds the atlas texture and the sprites once (idempotent).</summary>
        public static void Build()
        {
            if (sprites != null) return;
            var atlas = Atlas.Build();
            int n = Atlas.Size * Atlas.Size;
            var colors = new Color32[n];
            var px = atlas.Pixels;
            for (int i = 0; i < n; i++) colors[i] = new Color32(px[4 * i], px[4 * i + 1], px[4 * i + 2], px[4 * i + 3]);
            var tex = new Texture2D(Atlas.Size, Atlas.Size, TextureFormat.RGBA32, false);
            tex.name = "MinikUsta.Atlas";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(colors);
            tex.Apply(false, true);
            texture = tex;
            int count = (int)Atlas.Last + 1;
            var o = new Sprite[count];
            foreach (var t in Atlas.All)
            {
                var r = Atlas.RectOf(t);
                var rect = new Rect(r.X, r.Y, r.W, r.H);
                var pivot = new Vector2(0.5f, 0.5f);
                float b = t == Tile.Panel || t == Tile.Highlight ? Atlas.PanelBorder : 0f;
                var sprite = Sprite.Create(tex, rect, pivot, Atlas.PixelsPerUnit, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
                sprite.name = "MinikUsta." + t;
                o[(int)t] = sprite;
            }
            sprites = o;
        }

        public static Sprite Get(Tile t)
        {
            Build();
            return sprites![(int)t];
        }

        public static Texture2D? Texture => texture;
    }
}
