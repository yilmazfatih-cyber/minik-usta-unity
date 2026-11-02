#nullable enable
// One block on the board (Faz 3 Q1; paket V1 v4 art; web PieceView.ts; ART_UNITY §4.1): a root GameObject at the piece-box
// centre with the parts of `BlockSkin.Block` — per cell the volume tile `blk_<c>_<mm>` (one outline, one side wall, stud and
// embossed symbol for the whole piece), the corner fills that close the inner notches (no inner edges in O / L / T / S), the
// gloss of a holdable block (K-09) or the mortar dots of a placed one (K-14) — and the lifted drop blob (`fx_drop_blob`, shown
// while held, JUICE #1; B2: it drops away, widens and softens as the block rises — `LiftShadow.Pose`). Pooled by
// `UnityLevelView`; `Configure` re-skins it for another piece (no allocation once the pool holds enough parts).
using System.Collections.Generic;
using MinikUsta.Core;
using MinikUsta.Presentation;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Feel;
using MinikUsta.Presentation.Game;
using UnityEngine;

namespace MinikUsta.Unity
{
    public sealed class UnityPieceVisual : IPieceVisual
    {
        private sealed class Part
        {
            public SpriteRenderer Sr = null!;
            public FrameSprite Fs;
            public int Cell;
            /// <summary>Design px, relative to the piece box centre, y down.</summary>
            public PxRect Rect;
            public Rgba Tint;
            public int Order;
        }

        private readonly DesignSpace space;
        private readonly GameObject root;
        private readonly Transform tr;
        private readonly List<Part> parts = new List<Part>();
        private readonly SpriteRenderer blob;
        private readonly FeelSettings feel;
        private Vector3 blobPos;
        private Vector3 blobScale = Vector3.one;
        private float liftHeight;
        private int used;
        private ShapeDef? shape;
        private double cellPx = 120;
        private float[] cellSx = new float[0];
        private float[] cellSy = new float[0];
        private bool cellsDirty;
        private float grow = 1f;
        private int colorIndex;
        private bool dim;
        private bool placed;
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

        public UnityPieceVisual(DesignSpace space, Transform parent, FeelSettings feel)
        {
            this.space = space;
            this.feel = feel;
            root = new GameObject("Piece");
            tr = root.transform;
            tr.SetParent(parent, false);
            var b = new GameObject("Lifted");
            b.transform.SetParent(tr, false);
            blob = b.AddComponent<SpriteRenderer>();
            blob.enabled = false;
        }

        public int PieceId { get; private set; } = -1;

        /// <summary>Re-skins the visual for `pieceId` (shape cells, colour, cell size) and resets its pose.</summary>
        public void Configure(int pieceId, ShapeDef shape, int color, double cellPx)
        {
            PieceId = pieceId;
            root.name = "Piece " + pieceId;
            this.shape = shape;
            this.cellPx = cellPx;
            colorIndex = color;
            dim = false;
            placed = false;
            int n = shape.Cells.Length;
            if (cellSx.Length < n)
            {
                cellSx = new float[n];
                cellSy = new float[n];
            }
            for (int i = 0; i < n; i++)
            {
                cellSx[i] = 1f;
                cellSy[i] = 1f;
            }
            cellsDirty = false;
            grow = 1f;
            position = Vec2.Zero;
            scale = Vec2.One;
            rotation = 0;
            alpha = 1;
            flash = 0;
            tint = 0;
            hop = 0;
            raised = false;
            lifted = false;
            liftHeight = 0f;
            visible = true;
            root.SetActive(true);
            Rebuild();
            ApplyTransform();
        }

        private BlockLook Look => placed ? BlockLook.Placed : dim ? BlockLook.Dim : BlockLook.Holdable;

        /// <summary>The parts of the current shape / colour / look (sprites, rectangles, base tints); then pose, colours, order.</summary>
        private void Rebuild()
        {
            if (shape == null) return;
            var list = BlockSkin.Block(shape, colorIndex, cellPx, Look, AtlasSprites.Data);
            while (parts.Count < list.Count)
            {
                var go = new GameObject("Part");
                go.transform.SetParent(tr, false);
                parts.Add(new Part { Sr = go.AddComponent<SpriteRenderer>() });
            }
            used = list.Count;
            for (int i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                bool on = i < used;
                p.Sr.gameObject.SetActive(on);
                if (!on) continue;
                var s = list[i];
                p.Fs = AtlasSprites.Resolve(s.Frame);
                if (!p.Fs.Ok) p.Sr.gameObject.SetActive(false);
                p.Cell = s.Cell;
                p.Rect = s.Rect;
                p.Tint = s.Tint;
                p.Order = s.Order;
            }
            // lifted blob (JUICE #1; ART_UNITY §4.1): under the block, (w + 0,8) cells wide
            var bf = AtlasSprites.Resolve(Frames.DropBlob);
            double bw = (shape.W + 0.8) * cellPx, bh = 0.5 * cellPx;
            var br = new PxRect(-bw / 2, shape.H * cellPx / 2 - bh / 2 + Palette.LiftedShadowY, bw, bh);
            if (SpriteDraw.Apply(blob, bf, br.W, br.H, false, new Color(1f, 1f, 1f, 0.65f)))
            {
                SpriteDraw.PivotPoint(bf, br, out double px, out double py);
                blobPos = new Vector3(DesignSpace.Units(px), -DesignSpace.Units(py), 0f);
                blobScale = blob.transform.localScale;
                ApplyBlob();
            }
            blob.enabled = lifted && bf.Ok;
            for (int i = 0; i < used; i++) PlacePart(parts[i]);
            ApplyColors();
            ApplyOrder();
        }

        /// <summary>A part at its cell's squash (about the bottom centre of the block box, Q2 landing wave).</summary>
        private void PlacePart(Part p)
        {
            if (shape == null || !p.Fs.Ok) return;
            float sx = p.Cell < cellSx.Length ? cellSx[p.Cell] : 1f, sy = p.Cell < cellSy.Length ? cellSy[p.Cell] : 1f;
            double bottom = shape.H * cellPx / 2; // design px, y down
            var r = p.Rect;
            var q = new PxRect(r.X * sx, bottom + (r.Y - bottom) * sy, r.W * sx, r.H * sy);
            SpriteDraw.Apply(p.Sr, p.Fs, q.W, q.H, false, Color.white);
            SpriteDraw.PivotPoint(p.Fs, q, out double px, out double py);
            p.Sr.transform.localPosition = new Vector3(DesignSpace.Units(px), -DesignSpace.Units(py), 0f);
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

        public float Grow
        {
            get => grow;
            set
            {
                grow = value;
                ApplyTransform();
            }
        }

        public int CellCount => shape?.Cells.Length ?? 0;

        public void SetCellScale(int cell, float sx, float sy)
        {
            if (cell < 0 || cell >= CellCount) return;
            cellSx[cell] = sx;
            cellSy[cell] = sy;
            cellsDirty = true;
            for (int i = 0; i < used; i++)
                if (parts[i].Cell == cell) PlacePart(parts[i]);
            ApplyColors();
        }

        public void ResetCells()
        {
            if (!cellsDirty) return;
            cellsDirty = false;
            for (int i = 0; i < CellCount; i++)
            {
                cellSx[i] = 1f;
                cellSy[i] = 1f;
            }
            for (int i = 0; i < used; i++) PlacePart(parts[i]);
            ApplyColors();
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
                blob.enabled = value && blob.sprite != null;
            }
        }

        public float LiftHeight
        {
            get => liftHeight;
            set
            {
                liftHeight = value;
                ApplyBlob();
                ApplyColors();
            }
        }

        /// <summary>B2 / JUICE #1: the drop shadow for the lift height (down, wider, softer as the block rises).</summary>
        private void ApplyBlob()
        {
            LiftShadow.Pose(liftHeight, feel, out float dy, out float sc, out _);
            blob.transform.localPosition = new Vector3(blobPos.x, blobPos.y - DesignSpace.Units(dy), 0f);
            blob.transform.localScale = new Vector3(blobScale.x * sc, blobScale.y * sc, 1f);
        }

        public bool Placed
        {
            get => placed;
            set
            {
                if (placed == value) return;
                placed = value;
                Rebuild();
            }
        }

        public void SetLook(int color, bool dimmed)
        {
            if (color == colorIndex && dimmed == dim) return;
            colorIndex = color;
            dim = dimmed;
            Rebuild();
        }

        // --- drawing -----------------------------------------------------------------------------------------------------

        private void ApplyTransform()
        {
            tr.localPosition = space.World(position.X, position.Y - hop);
            tr.localScale = new Vector3(scale.X * grow, scale.Y * grow, 1f);
            tr.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private static readonly Color Red = UnityConvert.ToColor(Rgba.Hex(Palette.GhostInvalid));

        private void ApplyColors()
        {
            for (int i = 0; i < used; i++)
            {
                var p = parts[i];
                if (!p.Fs.Ok) continue;
                var c = SpriteDraw.Mul(UnityConvert.ToColor(p.Tint), p.Fs.Tint);
                if (flash > 0) c = UnityConvert.Lerp(c, Color.white, flash);
                if (tint > 0) c = UnityConvert.Lerp(c, Red, tint);
                c.a *= alpha;
                p.Sr.color = c;
            }
            var bc = blob.color;
            LiftShadow.Pose(liftHeight, feel, out _, out _, out float ba);
            blob.color = new Color(bc.r, bc.g, bc.b, ba * alpha);
        }

        private void ApplyOrder()
        {
            int b = raised ? Order.Raised : Order.Pieces;
            blob.sortingOrder = b - 1;
            for (int i = 0; i < parts.Count; i++) parts[i].Sr.sortingOrder = b + parts[i].Order;
        }
    }

    /// <summary>A small block icon flying to the blocks-left chip (D-091): the 1×1 volume tile of its colour.</summary>
    public sealed class UnityFlyer : IFlyer
    {
        private readonly DesignSpace space;
        private readonly GameObject go;
        private readonly SpriteRenderer body;
        private FrameSprite fs;
        private Vec2 position;
        private Vec2 scale = Vec2.One;
        private float rotation;
        private float alpha = 1f;
        private float size = 72f;

        public UnityFlyer(DesignSpace space, Transform parent)
        {
            this.space = space;
            go = new GameObject("Flyer");
            go.transform.SetParent(parent, false);
            var b = new GameObject("Body");
            b.transform.SetParent(go.transform, false);
            body = b.AddComponent<SpriteRenderer>();
            body.sortingOrder = Order.Flyer;
            go.SetActive(false);
        }

        public void Configure(int colorIndex, double sizePx)
        {
            fs = AtlasSprites.Resolve(Frames.Block(colorIndex, 0));
            size = (float)sizePx;
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
            t.localScale = new Vector3(scale.X, scale.Y, 1f);
            t.localRotation = Quaternion.Euler(0f, 0f, rotation);
            var r = new PxRect(-size / 2.0, -size / 2.0, size, size);
            if (SpriteDraw.Apply(body, fs, r.W, r.H, false, new Color(1f, 1f, 1f, alpha)))
            {
                SpriteDraw.PivotPoint(fs, r, out double px, out double py);
                body.transform.localPosition = new Vector3(DesignSpace.Units(px), -DesignSpace.Units(py), 0f);
            }
        }
    }
}
