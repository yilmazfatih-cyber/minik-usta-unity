#nullable enable
// The landing wave of a multi-cell block (Faz 3 Q2 item 3; JUICE #11): the cells squash one after another, 20–30 ms apart,
// starting at the bottom-left cell and running up and to the right (the impact travels through the block), instead of the
// whole block squashing at once. Pure: the rank of a cell is its distance (x + y) from the bottom-left-most cell of the shape (that cell is rank 0).
using System;
using MinikUsta.Core;
using MinikUsta.Presentation.Feel;

namespace MinikUsta.Presentation.Game
{
    public static class CellWave
    {
        /// <summary>Wave rank of every cell of `shape` (index = `ShapeDef.Cells` index; the bottom-left cell is 0).</summary>
        public static int[] Ranks(ShapeDef shape)
        {
            var cells = shape.Cells;
            int minX = int.MaxValue, minY = int.MaxValue;
            foreach (var c in cells)
            {
                minX = Math.Min(minX, c.X);
                minY = Math.Min(minY, c.Y);
            }
            var o = new int[cells.Length];
            int first = int.MaxValue;
            for (int i = 0; i < o.Length; i++)
            {
                o[i] = (cells[i].X - minX) + (cells[i].Y - minY);
                first = Math.Min(first, o[i]);
            }
            // a shape with no cell in its bottom-left corner (a rotated corner piece) starts its wave at the nearest cell
            for (int i = 0; i < o.Length; i++) o[i] -= first;
            return o;
        }

        /// <summary>Start delay (ms) of a cell of `rank`: `rank × CellStaggerMs`, never more than `CellStaggerCapMs` and never more than the squash itself.</summary>
        public static float DelayMs(int rank, FeelSettings f, float squashMs) =>
            Math.Max(0f, Math.Min(Math.Min(rank * f.CellStaggerMs, f.CellStaggerCapMs), squashMs * 0.6f));
    }
}
