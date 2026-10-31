#nullable enable
// Port of src/core/combo.ts (web repo, source of truth).
using System;
using static MinikUsta.Core.State;

namespace MinikUsta.Core
{
    /// <summary>Result of `ComboOnCorrect`.</summary>
    public sealed class ComboStep : IEquatable<ComboStep>
    {
        /// <summary>The counter value this placement reached (1…4): the pip the scene fills (JUICE #15).</summary>
        public readonly int Reached;
        /// <summary>Counter after the placement: `Reached`, or 0 when a trowel was earned.</summary>
        public readonly int Combo;
        /// <summary>A Golden Trowel was earned (JUICE #16).</summary>
        public readonly bool Earned;
        /// <summary>Trowels held after the placement.</summary>
        public readonly int Trowels;

        public ComboStep(int reached, int combo, bool earned, int trowels)
        {
            Reached = reached;
            Combo = combo;
            Earned = earned;
            Trowels = trowels;
        }

        public bool Equals(ComboStep? o) => o != null && Reached == o.Reached && Combo == o.Combo && Earned == o.Earned && Trowels == o.Trowels;
        public override bool Equals(object? obj) => obj is ComboStep c && Equals(c);
        public override int GetHashCode() => Reached * 31 + Trowels;
        public override string ToString() => "reached " + Reached + " combo " + Combo + (Earned ? " earned" : "") + " trowels " + Trowels;
    }

    /// <summary>Why a (deprecated Faz 2 cell) trowel target is refused (the trowel is not consumed, GDD §10).</summary>
    public static class TrowelRejectReasons
    {
        public const string LevelOver = "levelOver";
        public const string NoTrowel = "noTrowel";
        public const string NotVisibleSegment = "notVisibleSegment";
        public const string NotBuildFront = "notBuildFront";
    }

    /// <summary>Deprecated Faz 2 cell trowel target (segment, local column 0 | 1, plan row); use the `goldTrowel` move (K-33 Faz 2R).</summary>
    public readonly struct TrowelTarget
    {
        public readonly int Seg;
        public readonly int X;
        public readonly int Y;

        public TrowelTarget(int seg, int x, int y)
        {
            Seg = seg;
            X = x;
            Y = y;
        }
    }

    /// <summary>What a (deprecated) cell trowel use did.</summary>
    public sealed class TrowelFill
    {
        /// <summary>The filled cell (board coordinates, TECH §6.3 `At`).</summary>
        public readonly At Cell;
        public readonly string Color;
        /// <summary>The cell was an unrevealed `?` cell and opened (K-32).</summary>
        public readonly bool Revealed;
        /// <summary>Trowels left.</summary>
        public readonly int Trowels;

        public TrowelFill(At cell, string color, bool revealed, int trowels)
        {
            Cell = cell;
            Color = color;
            Revealed = revealed;
            Trowels = trowels;
        }
    }

    /// <summary>
    /// Usta Serisi (master streak) and the Golden Trowel (docs/GDD.md K-33; TECH_DESIGN §6.2 step 3, §6.4; JUICE #15–17,
    /// MVP-lite).
    ///
    /// - The streak counter `c` (state header `Combo`) starts at 0. Every correct placement made by a DRAG move adds 1; at
    ///   `c = 4` the player earns one Golden Trowel and `c = 0`. A wrong placement (bounce, mortar sticking) and a glass
    ///   break set `c = 0`. Yard moves, boosters and trowel use leave `c` alone; Undo restores the pre-move value (the whole
    ///   state snapshot, K-39).
    /// - Trowel use (Faz 2R, full cover K-47): the trowel no longer fills a plan cell without a block. The player picks a
    ///   yard block and it flies to a spot of its `P` set (move `goldTrowel`; Boosters `TrowelSpots`, `GoldTrowelRejection`,
    ///   `ApplyTrowelEffect`). The Faz 2 cell trowel below (`TrowelRejection`, `ApplyTrowel`, move `trowel`) is
    ///   deprecated: the pipeline rejects it (`legacyTrowel`).
    ///
    /// Pure state helpers: events and the K-35 mini pipeline belong to the move pipeline.
    /// </summary>
    public static class Combo
    {
        /// <summary>K-33: the streak length that earns one Golden Trowel.</summary>
        public const int ComboForTrowel = 4;

        /// <summary>Current streak counter `c` (K-33).</summary>
        public static int ComboOf(GameState s) => Hdr(s, H.Combo);

        /// <summary>Golden Trowels the player holds (K-33, K-40 Mala Başlangıcı, streak bonus).</summary>
        public static int TrowelsOf(GameState s) => Hdr(s, H.Trowels);

        /// <summary>K-33: a correct placement made by a drag move. `c += 1`; at `c = 4` one trowel is earned and `c = 0`.</summary>
        public static ComboStep ComboOnCorrect(GameState s)
        {
            int reached = Hdr(s, H.Combo) + 1;
            if (reached >= ComboForTrowel)
            {
                SetHdr(s, H.Combo, 0);
                SetHdr(s, H.Trowels, Hdr(s, H.Trowels) + 1);
                return new ComboStep(reached, 0, true, Hdr(s, H.Trowels));
            }
            SetHdr(s, H.Combo, reached);
            return new ComboStep(reached, reached, false, Hdr(s, H.Trowels));
        }

        /// <summary>K-33: wrong placement (bounce, mortar sticking) or glass break: `c = 0`. Returns true when the value changed.</summary>
        public static bool ComboReset(GameState s)
        {
            if (Hdr(s, H.Combo) == 0) return false;
            SetHdr(s, H.Combo, 0);
            return true;
        }

        /// <summary>Adds trowels (K-40 Mala Başlangıcı and the win-streak bonus at level start).</summary>
        public static void GrantTrowels(GameState s, int n)
        {
            if (n < 0) throw new ArgumentOutOfRangeException(nameof(n), "grantTrowels: bad amount " + n);
            SetHdr(s, H.Trowels, Hdr(s, H.Trowels) + n);
        }

        // --- Golden Trowel use (deprecated Faz 2 cell trowel) ----------------------------------------------------------------

        /// <summary>
        /// Deprecated Faz 2 cell trowel (see the class comment); the pipeline rejects the `trowel` move since Faz 2R.
        /// Precondition of a trowel use (TECH §6.4 `canUse*`): the level is not won, the player holds a trowel, the target is
        /// on the visible segment and is one of its build front cells (K-34). Returns null when the use is allowed.
        /// </summary>
        public static string? TrowelRejection(GameState s, TrowelTarget target)
        {
            if ((Hdr(s, H.Flags) & StateFlag.Won) != 0) return TrowelRejectReasons.LevelOver;
            if (Hdr(s, H.Trowels) <= 0) return TrowelRejectReasons.NoTrowel;
            if (target.Seg != Grid.VisibleSegment(s)) return TrowelRejectReasons.NotVisibleSegment;
            int x = s.Lvl.Geo.SiteX + target.X;
            int y = target.Y + Hdr(s, H.Elev);
            foreach (var c in Placement.BuildFront(s)) if (c.X == x && c.Y == y) return null;
            return TrowelRejectReasons.NotBuildFront;
        }

        /// <summary>
        /// Deprecated Faz 2 cell trowel (breaks the full cover K-47; never called by the pipeline since Faz 2R).
        /// K-33 trowel effect: the target cell is filled with its plan colour (`siteOcc` = SiteTrowel), joins `filled` and opens
        /// when hidden; one trowel is spent. Throws when `TrowelRejection` refuses the target (check it first).
        /// </summary>
        public static TrowelFill ApplyTrowel(GameState s, TrowelTarget target)
        {
            string? refused = TrowelRejection(s, target);
            if (refused != null) throw new ArgumentOutOfRangeException(nameof(target), "applyTrowel: target refused (" + refused + ")");
            int seg = target.Seg;
            int sx = target.X;
            int sy = target.Y;
            var plan = seg >= 0 && seg < s.Lvl.Segments.Length ? s.Lvl.Segments[seg] : null;
            int local = sy * s.Lvl.Geo.Ws + sx;
            SetSiteOcc(s, seg, sx, sy, SiteTrowel);
            SetFilledMask(s, seg, sx, FilledMask(s, seg, sx) | (1 << sy));
            bool hidden = plan != null && ((plan.HiddenMask >> local) & 1) == 1;
            bool revealed = hidden && ((RevealedMask(s, seg) >> local) & 1) == 0;
            if (revealed) SetRevealedMask(s, seg, RevealedMask(s, seg) | (1 << local));
            SetHdr(s, H.Trowels, Hdr(s, H.Trowels) - 1);
            int colorIndex = plan != null ? plan.PlanColors[local] : 0;
            return new TrowelFill(
                At.Site(s.Lvl.Geo.SiteX + sx, sy + Hdr(s, H.Elev), seg),
                colorIndex >= 0 && colorIndex < ColorCodes.All.Length ? ColorCodes.All[colorIndex] : "W",
                revealed,
                Hdr(s, H.Trowels));
        }
    }
}
