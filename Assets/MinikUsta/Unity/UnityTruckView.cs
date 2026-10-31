#nullable enable
// The delivery truck (Faz 3 Q2; JUICE #19) as a handful of sprites — `TruckModel.Parts` (rounded boxes and wheels on the shared
// atlas). The root sits at `Position` (design px, the truck's centre), turns with `Rotation` (the bed tips) and is sized from
// `CellPx` (the parts are in cells). Drawn behind the blocks (order 90 … 99) so the delivered blocks drop out of its bed.
using MinikUsta.Presentation;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Game;
using UnityEngine;

namespace MinikUsta.Unity
{
    public sealed class UnityTruckView : IPropView
    {
        /// <summary>Sorting order of the truck's first part (blocks start at `Order.Pieces` = 100).</summary>
        private const int BaseOrder = 90;

        private readonly DesignSpace space;
        private readonly GameObject root;
        private readonly SpriteRenderer[] renderers;
        private readonly PropPart[] parts = TruckModel.Parts();
        private Vec2 position;
        private Vec2 scale = Vec2.One;
        private float rotation;
        private float alpha = 1f;
        private double cellPx = 120;

        public UnityTruckView(DesignSpace space, Transform parent)
        {
            this.space = space;
            root = new GameObject("Truck");
            root.transform.SetParent(parent, false);
            renderers = new SpriteRenderer[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                var go = new GameObject("Part" + i);
                go.transform.SetParent(root.transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = ProceduralSprites.Get(parts[i].Tile);
                sr.sortingOrder = BaseOrder + i;
                renderers[i] = sr;
            }
            Layout();
            root.SetActive(false);
        }

        public double CellPx
        {
            get => cellPx;
            set
            {
                if (value == cellPx) return;
                cellPx = value;
                Layout();
            }
        }

        public bool Visible
        {
            get => root.activeSelf;
            set => root.SetActive(value);
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
                Colors();
            }
        }

        /// <summary>Sizes and places the parts for the current cell size (cells → world units).</summary>
        private void Layout()
        {
            float unit = DesignSpace.Units(cellPx);
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                var sr = renderers[i];
                var t = sr.transform;
                t.localPosition = new Vector3(part.X * unit, -part.Y * unit, 0f);
                if (part.Sliced)
                {
                    sr.drawMode = SpriteDrawMode.Sliced;
                    sr.size = new Vector2(part.W * unit, part.H * unit);
                    t.localScale = Vector3.one;
                }
                else
                {
                    sr.drawMode = SpriteDrawMode.Simple;
                    t.localScale = new Vector3(part.W * unit, part.H * unit, 1f);
                }
            }
            Colors();
        }

        private void Colors()
        {
            for (int i = 0; i < parts.Length; i++) renderers[i].color = UnityConvert.ToColor(parts[i].Color, alpha);
        }

        private void Apply()
        {
            var t = root.transform;
            t.localPosition = space.World(position);
            t.localScale = new Vector3(scale.X, scale.Y, 1f);
            t.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }
    }
}
