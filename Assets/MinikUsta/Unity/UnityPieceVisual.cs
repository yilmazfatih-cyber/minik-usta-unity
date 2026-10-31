#nullable enable
// One block on the board (Faz 3 Q1, placeholder art; web PieceView.ts): a root GameObject at the piece-box centre with,
// per cell, a body (rounded on the open sides, `Atlas.Body(mask)`, tinted with the block colour), a gloss highlight, the
// colour symbol (ink tint, ART §2.2) and the lifted drop shadow (shadow.lifted, shown while held). Pooled by
// `UnityLevelView`; `Configure` re-skins it for another piece (no allocation once the pool holds enough cells).
using System.Collections.Generic;
using MinikUsta.Core;
using MinikUsta.Presentation;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Game;
using UnityEngine;

namespace MinikUsta.Unity
{
    public sealed class UnityPieceVisual : IPieceVisual
    {
        private sealed class CellParts
        {
            public SpriteRenderer BodySr = null!;
            public SpriteRenderer GlossSr = null!;
            public SpriteRenderer SymbolSr = null!;
            public SpriteRenderer ShadowSr = null!;
        }

        /// <summary>blockV2.symbolSizeRatio: the symbol box is 0,42 c.</summary>
        private const float SymbolRatio = 0.42f;

        private readonly DesignSpace space;
        private readonly GameObject root;
        private readonly Transform tr;
        private readonly List<CellParts> parts = new List<CellParts>();
        private int used;
        private int colorIndex;
        private bool dim;
        private Vec2 position;
        private Vec2 scale = Vec2.One;
        private float rotation;
        private float alpha = 1f;
        private float flash;
        private float tint;
        private float hop;
        private bool raised;
        private bool lifted;
        private bool visible = true;

        public UnityPieceVisual(DesignSpace space, Transform parent)
        {
            this.space = space;
            root = new GameObject("Piece");
            tr = root.transform;
            tr.SetParent(parent, false);
        }

        public int PieceId { get; private set; } = -1;

        /// <summary>Re-skins the visual for `pieceId` (shape cells, colour, cell size) and resets its pose.</summary>
        public void Configure(int pieceId, ShapeDef shape, int color, double cellPx)
        {
            PieceId = pieceId;
            root.name = "Piece " + pieceId;
            colorIndex = color;
            dim = false;
            used = shape.Cells.Length;
            while (parts.Count < used) parts.Add(NewCell());
            float unit = DesignSpace.Units(cellPx);
            for (int i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                bool on = i < used;
                p.BodySr.gameObject.SetActive(on);
                p.GlossSr.gameObject.SetActive(on);
                p.SymbolSr.gameObject.SetActive(on);
                p.ShadowSr.gameObject.SetActive(on);
                if (!on) continue;
                var c = shape.Cells[i];
                var o = BoardPose.CellOffset(shape, c, cellPx);
                var local = new Vector3(DesignSpace.Units(o.X), -DesignSpace.Units(o.Y), 0f);
                p.BodySr.sprite = ProceduralSprites.Get(Atlas.Body(BoardPose.NeighbourMask(shape, c)));
                Place(p.BodySr.transform, local, unit);
                Place(p.GlossSr.transform, local, unit);
                Place(p.SymbolSr.transform, local, unit * SymbolRatio);
                p.SymbolSr.sprite = ProceduralSprites.Get(Atlas.Symbol(color));
                p.ShadowSr.sprite = p.BodySr.sprite;
                Place(p.ShadowSr.transform, local + new Vector3(0f, -DesignSpace.Units(Palette.LiftedShadowY), 0f), unit);
            }
            position = Vec2.Zero;
            scale = Vec2.One;
            rotation = 0;
            alpha = 1;
            flash = 0;
            tint = 0;
            hop = 0;
            raised = false;
            lifted = false;
            visible = true;
            root.SetActive(true);
            ApplyTransform();
            ApplyColors();
            ApplyOrder();
        }

        private static void Place(Transform t, Vector3 local, float size)
        {
            t.localPosition = local;
            t.localScale = new Vector3(size, size, 1f);
        }

        private CellParts NewCell()
        {
            var p = new CellParts();
            p.ShadowSr = NewRenderer("Shadow");
            p.BodySr = NewRenderer("Body");
            p.GlossSr = NewRenderer("Gloss");
            p.GlossSr.sprite = ProceduralSprites.Get(Tile.Gloss);
            p.SymbolSr = NewRenderer("Symbol");
            return p;
        }

        private SpriteRenderer NewRenderer(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(tr, false);
            return go.AddComponent<SpriteRenderer>();
        }

        /// <summary>Back to the pool: hidden, parked.</summary>
        public void Park()
        {
            PieceId = -1;
            root.SetActive(false);
        }

        // --- IPieceVisual ------------------------------------------------------------------------------------------------

        public Vec2 Position
        {
            get => position;
            set
            {
                position = value;
                ApplyTransform();
            }
        }

        public Vec2 Scale
        {
            get => scale;
            set
            {
                scale = value;
                ApplyTransform();
            }
        }

        public float Rotation
        {
            get => rotation;
            set
            {
                rotation = value;
                ApplyTransform();
            }
        }

        public float Hop
        {
            get => hop;
            set
            {
                hop = value;
                ApplyTransform();
            }
        }

        public float Alpha
        {
            get => alpha;
            set
            {
                alpha = value;
                ApplyColors();
            }
        }

        public float Flash
        {
            get => flash;
            set
            {
                flash = value;
                ApplyColors();
            }
        }

        public float Tint
        {
            get => tint;
            set
            {
                tint = value;
                ApplyColors();
            }
        }

        public bool Visible
        {
            get => visible;
            set
            {
                visible = value;
                root.SetActive(value);
            }
        }

        public bool Raised
        {
            get => raised;
            set
            {
                if (raised == value) return;
                raised = value;
                ApplyOrder();
            }
        }

        public bool Lifted
        {
            get => lifted;
            set
            {
                if (lifted == value) return;
                lifted = value;
                for (int i = 0; i < used; i++) parts[i].ShadowSr.enabled = value;
            }
        }

        public void SetLook(int color, bool dimmed)
        {
            if (color != colorIndex)
            {
                colorIndex = color;
                var sym = ProceduralSprites.Get(Atlas.Symbol(color));
                for (int i = 0; i < used; i++) parts[i].SymbolSr.sprite = sym;
            }
            dim = dimmed;
            ApplyColors();
        }

        // --- drawing -----------------------------------------------------------------------------------------------------

        private void ApplyTransform()
        {
            tr.localPosition = space.World(position.X, position.Y - hop);
            tr.localScale = new Vector3(scale.X, scale.Y, 1f);
            tr.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private static readonly Color Red = UnityConvert.ToColor(Rgba.Hex(Palette.GhostInvalid));

        private void ApplyColors()
        {
            var baseColor = Palette.Block(colorIndex);
            if (dim) baseColor = baseColor.Scale(0.92f); // blockV2.notHoldableTint #EBEBEB
            var body = UnityConvert.ToColor(baseColor);
            if (flash > 0) body = UnityConvert.Lerp(body, Color.white, flash);
            if (tint > 0) body = UnityConvert.Lerp(body, Red, tint);
            body.a = alpha;
            var ink = UnityConvert.ToColor(Palette.SymbolInk(colorIndex), alpha);
            var gloss = new Color(1f, 1f, 1f, alpha);
            var shadow = new Color(0f, 0f, 0f, Palette.LiftedShadowAlpha * alpha);
            for (int i = 0; i < used; i++)
            {
                var p = parts[i];
                p.BodySr.color = body;
                p.GlossSr.color = gloss;
                p.SymbolSr.color = ink;
                p.ShadowSr.color = shadow;
                p.ShadowSr.enabled = lifted;
            }
        }

        private void ApplyOrder()
        {
            int b = raised ? Order.Raised : Order.Pieces;
            for (int i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                p.ShadowSr.sortingOrder = b - 1;
                p.BodySr.sortingOrder = b;
                p.GlossSr.sortingOrder = b + 1;
                p.SymbolSr.sortingOrder = b + 2;
            }
        }
    }

    /// <summary>A small block icon flying to the blocks-left chip (D-091).</summary>
    public sealed class UnityFlyer : IFlyer
    {
        private readonly DesignSpace space;
        private readonly GameObject go;
        private readonly SpriteRenderer body;
        private readonly SpriteRenderer symbol;
        private Vec2 position;
        private Vec2 scale = Vec2.One;
        private float rotation;
        private float alpha = 1f;
        private float size = 72f;
        private int color;

        public UnityFlyer(DesignSpace space, Transform parent)
        {
            this.space = space;
            go = new GameObject("Flyer");
            go.transform.SetParent(parent, false);
            body = go.AddComponent<SpriteRenderer>();
            body.sprite = ProceduralSprites.Get(Atlas.Body(0));
            body.sortingOrder = Order.Flyer;
            var s = new GameObject("Symbol");
            s.transform.SetParent(go.transform, false);
            s.transform.localScale = new Vector3(0.42f, 0.42f, 1f);
            symbol = s.AddComponent<SpriteRenderer>();
            symbol.sortingOrder = Order.Flyer + 1;
            go.SetActive(false);
        }

        public void Configure(int colorIndex, double sizePx)
        {
            color = colorIndex;
            size = (float)sizePx;
            symbol.sprite = ProceduralSprites.Get(Atlas.Symbol(colorIndex));
            alpha = 1;
            scale = Vec2.One;
            Apply();
        }

        public bool Visible
        {
            get => go.activeSelf;
            set => go.SetActive(value);
        }

        public Vec2 Position
        {
            get => position;
            set
            {
                position = value;
                Apply();
            }
        }

        public Vec2 Scale
        {
            get => scale;
            set
            {
                scale = value;
                Apply();
            }
        }

        public float Rotation
        {
            get => rotation;
            set
            {
                rotation = value;
                Apply();
            }
        }

        public float Alpha
        {
            get => alpha;
            set
            {
                alpha = value;
                Apply();
            }
        }

        private void Apply()
        {
            var t = go.transform;
            t.localPosition = space.World(position);
            float u = DesignSpace.Units(size);
            t.localScale = new Vector3(u * scale.X, u * scale.Y, 1f);
            t.localRotation = Quaternion.Euler(0f, 0f, rotation);
            body.color = UnityConvert.ToColor(Palette.Block(color), alpha);
            symbol.color = UnityConvert.ToColor(Palette.SymbolInk(color), alpha);
        }
    }
}
