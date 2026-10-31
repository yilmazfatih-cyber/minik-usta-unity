#nullable enable
// The static board (Faz 3 Q1): every `BoardItem` of a `BoardScene` as a pooled SpriteRenderer (simple tiles stretched by
// scale; `Tile.Panel` 9-sliced to its rectangle). Rebuilt by `LevelPlayer` after every package and on board events; the
// pool only grows to the largest board seen.
using System.Collections.Generic;
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
            || kind == BoardItemKind.Platform;

        public void Build(BoardScene scene)
        {
            var items = scene.Items;
            while (pool.Count < items.Count)
            {
                var go = new GameObject("Board");
                go.transform.SetParent(parent, false);
                pool.Add(go.AddComponent<SpriteRenderer>());
            }
            for (int i = 0; i < pool.Count; i++)
            {
                var sr = pool[i];
                if (i >= items.Count)
                {
                    sr.gameObject.SetActive(false);
                    continue;
                }
                var item = items[i];
                var want = IsSiteLayer(item.Kind) ? siteRoot : parent;
                if (sr.transform.parent != want) sr.transform.SetParent(want, false);
                sr.gameObject.SetActive(true);
                sr.sprite = ProceduralSprites.Get(item.Tile);
                sr.color = UnityConvert.ToColor(item.Color);
                sr.sortingOrder = item.Order;
                var t = sr.transform;
                t.localPosition = space.Center(item.Rect);
                t.localRotation = Quaternion.identity;
                float w = DesignSpace.Units(item.Rect.W), h = DesignSpace.Units(item.Rect.H);
                if (item.Sliced)
                {
                    sr.drawMode = SpriteDrawMode.Sliced;
                    sr.size = new Vector2(w, h);
                    t.localScale = Vector3.one;
                }
                else
                {
                    sr.drawMode = SpriteDrawMode.Simple;
                    t.localScale = new Vector3(w, h, 1f);
                }
            }
        }
    }

    /// <summary>The K-18 fall shadow (UX §5.4, placeholder): per cell the ghost body (block colour at alpha.ghostFill) and
    /// the outline (color.ghost.*), plus the ✓ / ! badge on the top-right cell.</summary>
    public sealed class UnityShadowView
    {
        /// <summary>a11y.ghostBadgePx-like badge diameter (design px).</summary>
        private const double BadgePx = 56;

        private readonly DesignSpace space;
        private readonly Transform parent;
        private readonly List<SpriteRenderer> bodies = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> outlines = new List<SpriteRenderer>();
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
            int n = m == null ? 0 : m.Shape.Cells.Length;
            while (bodies.Count < n)
            {
                bodies.Add(New("GhostBody"));
                outlines.Add(New("GhostOutline"));
            }
            for (int i = 0; i < bodies.Count; i++)
            {
                bool on = i < n;
                bodies[i].gameObject.SetActive(on && m!.Look.Body);
                outlines[i].gameObject.SetActive(on);
            }
            if (m == null)
            {
                badge.gameObject.SetActive(false);
                return;
            }
            float unit = DesignSpace.Units(m.CellPx);
            // on the rail (K-12) the outline sits on the dragged block itself, above it
            int outlineOrder = m.Look.Body ? Order.Ghost + 1 : Order.Raised + 3;
            int i2 = 0;
            foreach (var c in m.Shape.Cells)
            {
                var o = BoardPose.CellOffset(m.Shape, c, m.CellPx);
                var pos = space.World(m.Center.X + o.X, m.Center.Y + o.Y);
                var body = bodies[i2];
                body.sprite = ProceduralSprites.Get(MinikUsta.Presentation.Art.Atlas.Body(BoardPose.NeighbourMask(m.Shape, c)));
                body.color = UnityConvert.ToColor(m.BodyColor);
                body.sortingOrder = Order.Ghost;
                body.transform.localPosition = pos;
                body.transform.localScale = new Vector3(unit, unit, 1f);
                var line = outlines[i2];
                line.sprite = ProceduralSprites.Get(MinikUsta.Presentation.Art.Tile.Ghost);
                line.color = UnityConvert.ToColor(m.OutlineColor);
                line.sortingOrder = outlineOrder;
                line.transform.localPosition = pos;
                line.transform.localScale = new Vector3(unit, unit, 1f);
                i2++;
            }
            var tile = m.BadgeTile;
            badge.gameObject.SetActive(tile.HasValue);
            if (tile.HasValue)
            {
                badge.sprite = ProceduralSprites.Get(tile.Value);
                badge.color = UnityConvert.ToColor(m.BadgeColor);
                badge.sortingOrder = Order.GhostBadge;
                var p = m.BadgeCenter(BadgePx);
                badge.transform.localPosition = space.World(p);
                float u = DesignSpace.Units(BadgePx);
                badge.transform.localScale = new Vector3(u, u, 1f);
            }
        }
    }
}
