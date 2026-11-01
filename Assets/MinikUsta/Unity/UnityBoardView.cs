#nullable enable
// The static board (Faz 3 Q1; paket V1 v4 art): every `BoardItem` of a `BoardScene` as a pooled SpriteRenderer drawn from the
// atlas frame into its rectangle (`SpriteDraw`; tray / 9-slice frames sliced). With no atlas at all the Q1 placeholder items
// are drawn instead (procedural tiles, `Tile` / `Color`). Rebuilt by `LevelPlayer` after every package and on board events;
// the pool only grows to the largest board seen. The K-18 fall shadow is `UnityShadowView` below.
using System.Collections.Generic;
using MinikUsta.Presentation;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Tweening;
using UnityEngine;

namespace MinikUsta.Unity
{
    public sealed class UnityBoardView
    {
        private readonly DesignSpace space;
        private readonly Transform parent;
        private readonly Transform siteRoot;
        private readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();

        public UnityBoardView(DesignSpace space, Transform parent)
        {
            this.space = space;
            this.parent = parent;
            // the site layer (blueprint, plan, platform) hangs under its own root so the segment slide (JUICE #18) can move it
            siteRoot = new GameObject("Site").transform;
            siteRoot.SetParent(parent, false);
            SiteLayer = new OffsetTarget(siteRoot);
        }

        /// <summary>The site layer's offset (design px): the next segment slides in from the right edge.</summary>
        public ITweenTarget SiteLayer { get; }

        private static bool IsSiteLayer(BoardItemKind kind) =>
            kind == BoardItemKind.SitePanel || kind == BoardItemKind.PlanCell || kind == BoardItemKind.HatchCell || kind == BoardItemKind.HiddenCell
            || kind == BoardItemKind.Platform || kind == BoardItemKind.SiteFloor || kind == BoardItemKind.BuildFront;

        public void Build(BoardScene scene)
        {
            bool art = AtlasSprites.Loaded;
            var items = scene.Items;
            int used = 0;
            foreach (var item in items)
            {
                // art mode draws the framed items; placeholder mode the Q1 items (no frame needed)
                if (art ? item.Frame == null : item.ArtOnly) continue;
                if (used == pool.Count)
                {
                    var go = new GameObject("Board");
                    go.transform.SetParent(parent, false);
                    pool.Add(go.AddComponent<SpriteRenderer>());
                }
                var sr = pool[used++];
                var want = IsSiteLayer(item.Kind) ? siteRoot : parent;
                if (sr.transform.parent != want) sr.transform.SetParent(want, false);
                sr.gameObject.SetActive(true);
                sr.sortingOrder = item.Order;
                sr.flipY = item.FlipY;
                var t = sr.transform;
                t.localRotation = Quaternion.identity;
                FrameSprite fs;
                Color tint;
                bool sliced;
                if (art)
                {
                    fs = AtlasSprites.Resolve(item.Frame!);
                    tint = UnityConvert.ToColor(item.Tint);
                    sliced = item.Sliced || (fs.Frame != null && fs.Frame.Sliced && item.Kind == BoardItemKind.Tray);
                }
                else
                {
                    fs = new FrameSprite(ProceduralSprites.Get(item.Tile), null, Rgba.White, item.Tile == Tile.Panel);
                    tint = UnityConvert.ToColor(item.Color);
                    sliced = item.Sliced;
                }
                if (!SpriteDraw.Apply(sr, fs, item.Rect.W, item.Rect.H, sliced, tint)) continue;
                SpriteDraw.PivotPoint(fs, item.Rect, out double px, out double py);
                t.localPosition = space.World(px, py);
            }
            for (int i = used; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// The K-18 fall shadow (UX §5.4; ART_UNITY §4.2): per cell `shadow_fill_<mm>` tinted with the block colour + the outline
    /// (`shadow_line` correct / `shadow_dash` wrong, neutral) with its concave corners (`BlockSkin.Shadow`), plus the ✓ / ! badge on
    /// the top-right cell (`icon_check` / `badge_dot`).
    /// </summary>
    public sealed class UnityShadowView
    {
        /// <summary>a11y.ghostBadgePx-like badge diameter (design px).</summary>
        private const double BadgePx = 56;

        private readonly DesignSpace space;
        private readonly Transform parent;
        private readonly List<SpriteRenderer> parts = new List<SpriteRenderer>();
        private readonly SpriteRenderer badge;

        public UnityShadowView(DesignSpace space, Transform parent)
        {
            this.space = space;
            this.parent = parent;
            badge = New("Badge");
            badge.gameObject.SetActive(false);
        }

        private SpriteRenderer New(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<SpriteRenderer>();
        }

        public void Show(ShadowModel? m)
        {
            var list = m == null ? null : BlockSkin.Shadow(m.Shape, m.ColorIndex, m.CellPx, m.Look);
            int n = list?.Count ?? 0;
            while (parts.Count < n) parts.Add(New("Shadow"));
            for (int i = 0; i < parts.Count; i++) parts[i].gameObject.SetActive(i < n);
            if (m == null || list == null)
            {
                badge.gameObject.SetActive(false);
                return;
            }
            // on the rail (K-12) the outline sits on the dragged block itself, above it
            int baseOrder = m.Look.Body ? Order.Ghost : Order.Raised + 3;
            for (int i = 0; i < n; i++)
            {
                var p = list[i];
                var sr = parts[i];
                var fs = AtlasSprites.Resolve(p.Frame);
                sr.sortingOrder = baseOrder + p.Order;
                var r = new PxRect(m.Center.X + p.Rect.X, m.Center.Y + p.Rect.Y, p.Rect.W, p.Rect.H);
                if (!SpriteDraw.Apply(sr, fs, r.W, r.H, false, UnityConvert.ToColor(p.Tint))) continue;
                SpriteDraw.PivotPoint(fs, r, out double px, out double py);
                sr.transform.localPosition = space.World(px, py);
            }
            var tile = m.BadgeTile;
            badge.gameObject.SetActive(tile.HasValue);
            if (tile.HasValue)
            {
                var c = m.BadgeCenter(BadgePx);
                var fs = AtlasSprites.Resolve(m.Look.Badge == ShadowBadge.Ok ? "icon_check" : "badge_dot");
                badge.sortingOrder = Order.GhostBadge;
                var r = new PxRect(c.X - BadgePx / 2, c.Y - BadgePx / 2, BadgePx, BadgePx);
                if (SpriteDraw.Apply(badge, fs, r.W, r.H, false, Color.white))
                {
                    SpriteDraw.PivotPoint(fs, r, out double px, out double py);
                    badge.transform.localPosition = space.World(px, py);
                }
            }
        }
    }
}
