#nullable enable
// What makes a block look different beyond its colour (paket E3, GDD §16 v3 block traits; docs/ART_UNITY.md section 9). Pure:
// read from the core state per piece (`Of`), compared by value so a visual re-skins only when something changed.
//
//   Y9  two-tone    `CellColors`: the colour of every cell (the body tile of each cell is its own colour; the joined tiles read as one
//                   two-coloured block, each cell keeps its embossed colour symbol — colour AND symbol, never colour alone)
//   Y10 pallet      `PalletLayers` (layers still on the pallet, top layer only), `PalletDepth` (layers above this one: 0 = the top
//                   layer; a covered layer is drawn lower and offset like a slab under the top), `PalletBase` (the lowest layer draws
//                   the planks)
//   Y11 wrap        `Wrapped` (film + rope net + knot badge), `Scissors` (scissors badge)
//   Y12 bonus       `Bonus` = moves the placement adds (star badge "+n")
//   Y13 slippery    `Slippery` (frost sheen + snowflake badge)
// Every trait has a badge or an overlay with its own SHAPE, so none of them depends on colour.
using System;
using MinikUsta.Core;
using static MinikUsta.Core.State;

namespace MinikUsta.Presentation.Art
{
    public sealed class BlockTraits : IEquatable<BlockTraits>
    {
        public static readonly BlockTraits None = new BlockTraits(null, false, false, 0, false, 0, 0, false);

        /// <summary>Colour index per shape cell (Y9), null for a one-colour block.</summary>
        public readonly int[]? CellColors;
        public readonly bool Wrapped;
        public readonly bool Scissors;
        /// <summary>Moves a correct placement adds (1–3); 0 = no bonus badge.</summary>
        public readonly int Bonus;
        public readonly bool Slippery;
        /// <summary>Layers still on this block's pallet (shown on the top layer); 0 = not on a pallet.</summary>
        public readonly int PalletLayers;
        /// <summary>Layers above this one (0 = top layer).</summary>
        public readonly int PalletDepth;
        /// <summary>The lowest layer on the pallet draws the planks.</summary>
        public readonly bool PalletBase;

        public BlockTraits(int[]? cellColors, bool wrapped, bool scissors, int bonus, bool slippery, int palletLayers, int palletDepth, bool palletBase)
        {
            CellColors = cellColors;
            Wrapped = wrapped;
            Scissors = scissors;
            Bonus = bonus;
            Slippery = slippery;
            PalletLayers = palletLayers;
            PalletDepth = palletDepth;
            PalletBase = palletBase;
        }

        public bool IsNone => CellColors == null && !Wrapped && !Scissors && Bonus == 0 && !Slippery && PalletLayers == 0 && !PalletBase && PalletDepth == 0;

        /// <summary>The traits of piece `id` in the current state.</summary>
        public static BlockTraits Of(GameState s, int id)
        {
            var lvl = s.Lvl;
            if (id < 0 || id >= lvl.Pieces.Length) return None;
            var cp = lvl.Pieces[id];
            int flags = PieceFlagBits(s, id);
            int layers = 0, depth = 0;
            bool lowest = false;
            if (cp.StackId >= 0 && PieceZone(s, id) == Zone.Yard && PieceX(s, id) == cp.X && PieceY(s, id) == cp.Y)
            {
                lowest = true;
                for (int q = 0; q < lvl.Pieces.Length; q++)
                {
                    var o = lvl.Pieces[q];
                    if (o.StackId != cp.StackId || PieceZone(s, q) != Zone.Yard || PieceX(s, q) != cp.X || PieceY(s, q) != cp.Y) continue;
                    layers++;
                    if (o.StackZ > cp.StackZ) depth++;
                    else if (o.StackZ < cp.StackZ) lowest = false;
                }
            }
            bool bonus = (flags & FlagBit.Bonus) != 0;
            var t = new BlockTraits(
                cp.CellColors,
                (flags & FlagBit.Wrapped) != 0,
                (flags & FlagBit.Scissors) != 0,
                bonus ? Math.Max(1, Math.Min(3, cp.BonusMoves)) : 0,
                (flags & FlagBit.Slippery) != 0,
                depth == 0 && layers >= 2 ? layers : 0,
                depth,
                lowest);
            return t.IsNone ? None : t;
        }

        public bool Equals(BlockTraits? o)
        {
            if (o == null) return false;
            if (ReferenceEquals(this, o)) return true;
            if (Wrapped != o.Wrapped || Scissors != o.Scissors || Bonus != o.Bonus || Slippery != o.Slippery || PalletLayers != o.PalletLayers
                || PalletDepth != o.PalletDepth || PalletBase != o.PalletBase) return false;
            if (CellColors == null || o.CellColors == null) return CellColors == o.CellColors;
            if (CellColors.Length != o.CellColors.Length) return false;
            for (int i = 0; i < CellColors.Length; i++) if (CellColors[i] != o.CellColors[i]) return false;
            return true;
        }

        public override bool Equals(object? obj) => Equals(obj as BlockTraits);

        public override int GetHashCode() => (Wrapped ? 1 : 0) | (Scissors ? 2 : 0) | (Slippery ? 4 : 0) | (PalletBase ? 8 : 0) | (Bonus << 4) | (PalletLayers << 8) | (PalletDepth << 12);

        public override string ToString() =>
            IsNone ? "none" : (CellColors != null ? "twoTone " : "") + (Wrapped ? "wrapped " : "") + (Scissors ? "scissors " : "") + (Bonus > 0 ? "bonus+" + Bonus + " " : "")
            + (Slippery ? "slippery " : "") + (PalletLayers > 0 || PalletDepth > 0 || PalletBase ? "pallet " + PalletLayers + "/" + PalletDepth + (PalletBase ? " base" : "") : "");
    }
}
