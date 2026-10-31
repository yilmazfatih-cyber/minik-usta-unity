#nullable enable
// The delivery truck of JUICE #19 as placeholder parts (Faz 3 Q2 item 6): rounded boxes and wheels, the cab on the LEFT (the
// truck drives in from the right edge, nose first). Positions and sizes are in cells (y down) from the middle of the
// `WidthCells` × `HeightCells` box, so the Unity view and the software preview draw the very same truck at any cell size.
using MinikUsta.Presentation.Art;

namespace MinikUsta.Presentation.Game
{
    public readonly struct PropPart
    {
        public readonly Tile Tile;
        /// <summary>Centre of the part from the prop centre, in cells (y down).</summary>
        public readonly float X;
        public readonly float Y;
        public readonly float W;
        public readonly float H;
        public readonly Rgba Color;
        /// <summary>9-sliced (`Tile.Panel`).</summary>
        public readonly bool Sliced;

        public PropPart(Tile tile, float x, float y, float w, float h, Rgba color, bool sliced = false)
        {
            Tile = tile;
            X = x;
            Y = y;
            W = w;
            H = h;
            Color = color;
            Sliced = sliced;
        }
    }

    public static class TruckModel
    {
        public const float WidthCells = 3.5f;
        public const float HeightCells = 1.3f;

        /// <summary>Back to front (index = draw order within the truck).</summary>
        public static PropPart[] Parts()
        {
            var wood = Rgba.Hex(Palette.YardFrame);
            var rail = Rgba.Hex(Palette.Rail);
            return new[]
            {
                new PropPart(Tile.Plain, -0.05f, 0.42f, 3.35f, 0.14f, rail),
                new PropPart(Tile.Panel, 0.45f, -0.10f, 2.4f, 0.92f, wood, true),
                new PropPart(Tile.Plain, 0.45f, -0.50f, 2.4f, 0.10f, wood.Scale(0.72f)),
                new PropPart(Tile.Panel, -1.25f, 0.02f, 1.0f, 1.05f, Rgba.Hex(Palette.Secondary), true),
                new PropPart(Tile.Panel, -1.42f, -0.16f, 0.5f, 0.4f, Rgba.Hex(Palette.PlanUnderlay), true),
                new PropPart(Tile.Circle, -1.1f, 0.52f, 0.52f, 0.52f, rail.Scale(0.8f)),
                new PropPart(Tile.Circle, 1.05f, 0.52f, 0.52f, 0.52f, rail.Scale(0.8f)),
                new PropPart(Tile.Circle, -1.1f, 0.52f, 0.2f, 0.2f, Rgba.Hex(Palette.PlanUnderlay)),
                new PropPart(Tile.Circle, 1.05f, 0.52f, 0.2f, 0.2f, Rgba.Hex(Palette.PlanUnderlay)),
            };
        }
    }
}
