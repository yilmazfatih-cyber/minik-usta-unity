#nullable enable
// What the fall shadow shows — port of web src/scenes/level/shadowLook.ts (GDD K-18, K-34 hook 2; UX_FLOWS §5.3–§5.4;
// D-014). Pure: the core decides the landing (`Gravity.ComputeFall`) and what may be shown (`Gravity.ShadowInfo`); this
// maps it to the look:
//
// | case                         | outline  | badge   | pulse | missing-support cells                     |
// | ---------------------------- | -------- | ------- | ----- | ----------------------------------------- |
// | easy/normal, correct         | valid    | ok      | no    | —                                         |
// | easy/normal, wrong           | invalid  | warn / support (primary reason support) | yes | primary support: `MissingSupport` |
// | hard/superhard or `?` cell   | neutral  | —       | no    | easy/normal + support: `MissingSupport`   |
// | glass will break (S3)        | as above | glass (physics information, every difficulty)              |
//
// Only the PRIMARY reason is shown. On the rail (K-12) there is no fall: the outline sits on the block (`Body` false). A
// cancelling release (K-07 rows 3–5) shows no shadow; the dragged block turns 60 % opaque instead (`CancelPreview`).
using System.Collections.Generic;
using System.Text;
using MinikUsta.Core;
using MinikUsta.Presentation.Art;

namespace MinikUsta.Presentation.Game
{
    public enum ShadowBadge
    {
        None,
        Ok,
        Warn,
        Support,
        Glass,
    }

    public sealed class ShadowLook
    {
        private static readonly At[] NoAt = new At[0];
        private static readonly BoardCell[] NoCells = new BoardCell[0];

        public readonly ShadowOutline Outline;
        /// <summary>Ghost body (block colour at `alpha.ghostFill`) at the landing; false on the rail.</summary>
        public readonly bool Body;
        public readonly ShadowBadge Badge;
        /// <summary>UX §5.4: the wrong outline pulses at 2 Hz.</summary>
        public readonly bool Pulse;
        public readonly At[] SupportCells;
        /// <summary>Landing cells of the primary reason (debris / outside / window / colour; easy/normal wrong only).</summary>
        public readonly BoardCell[] MismatchCells;
        /// <summary>Equality key: a change replays the JUICE #7 switch.</summary>
        public readonly string Key;

        private ShadowLook(ShadowOutline outline, bool body, ShadowBadge badge, bool pulse, At[] support, BoardCell[] mismatch)
        {
            Outline = outline;
            Body = body;
            Badge = badge;
            Pulse = pulse;
            SupportCells = support;
            MismatchCells = mismatch;
            var k = new StringBuilder();
            k.Append(outline).Append('|').Append(badge).Append('|').Append(body ? 'b' : 'r').Append('|');
            foreach (var c in support) k.Append(c.X).Append(',').Append(c.Y).Append(';');
            k.Append('|');
            foreach (var c in mismatch) k.Append(c.X).Append(',').Append(c.Y).Append(';');
            Key = k.ToString();
        }

        /// <summary>
        /// UX §5.4 look of a release that falls (`siteFree`) or stays on the rail (`siteRail`). `s` + `pieceId` let the core
        /// name the cells of the primary reason; without them there are no mismatch cells.
        /// </summary>
        public static ShadowLook Of(FallResult fall, string difficulty, GameState? s = null, int pieceId = -1)
        {
            var info = Gravity.ShadowInfo(fall, difficulty);
            bool easy = difficulty == "easy" || difficulty == "normal";
            string? primary = info.Reasons.Length > 0 ? info.Reasons[0] : null;
            ShadowOutline outline;
            var badge = ShadowBadge.None;
            bool pulse = false;
            At[] support = NoAt;
            BoardCell[] mismatch = NoCells;
            if (info.Tone == ShadowInfo.ToneCorrect)
            {
                outline = ShadowOutline.Valid;
                badge = ShadowBadge.Ok;
            }
            else if (info.Tone == ShadowInfo.ToneWrong)
            {
                outline = ShadowOutline.Invalid;
                badge = primary == VerdictReasons.Support ? ShadowBadge.Support : ShadowBadge.Warn;
                pulse = true;
                if (primary == VerdictReasons.Support) support = info.MissingSupport;
                else if (primary != null && s != null && pieceId >= 0) mismatch = Placement.ReasonCells(s, pieceId, fall.Cells, primary);
            }
            else
            {
                outline = ShadowOutline.Neutral;
                if (easy && primary == VerdictReasons.Support) support = info.MissingSupport;
            }
            if (info.Breaks) badge = ShadowBadge.Glass;
            return new ShadowLook(outline, fall.Mode == FallResult.ModeFree, badge, pulse, support, mismatch);
        }

        /// <summary>
        /// UX §5.3 cancel preview: K-07 rows 3 (crane area over the yard), 4 (straddling the wall), 5 (site closed, E-27) and, from v3c,
        /// a lifted pallet layer anywhere off the site (K-58 item 9 c, D-104: it is never parked; the block shows the cancel look and
        /// goes back onto its pallet, no move spent).
        /// </summary>
        public static bool CancelPreview(DropClass drop) =>
            drop.Kind == DropKinds.Cancel
            && (drop.Reason == CancelReasons.CraneOverYard || drop.Reason == CancelReasons.Straddle || drop.Reason == CancelReasons.SiteClosed
                || drop.Reason == CancelReasons.PalletSiteOnly);

        /// <summary>A release onto the site that the shadow shows (K-07 rows 6 and 7).</summary>
        public static bool ShowsShadow(DropClass drop) => drop.Kind == DropKinds.SiteFree || drop.Kind == DropKinds.SiteRail;

        /// <summary>Top-right cell of a shape (highest row, then rightmost): where the badge sits (UX §5.4 "sağ üstte").</summary>
        public static Cell BadgeCell(ShapeDef shape)
        {
            var best = shape.Cells[0];
            foreach (var c in shape.Cells)
                if (c.Y > best.Y || (c.Y == best.Y && c.X > best.X)) best = c;
            return best;
        }
    }

    /// <summary>Everything the view needs to draw the K-18 shadow of the dragged block.</summary>
    public sealed class ShadowModel
    {
        public readonly int PieceId;
        public readonly ShapeDef Shape;
        public readonly int ColorIndex;
        /// <summary>Landing anchor (global board cells; on the rail the node itself).</summary>
        public readonly int LandX;
        public readonly int LandY;
        public readonly ShadowLook Look;
        /// <summary>Landing box centre (design px).</summary>
        public readonly Vec2 Center;
        public readonly double CellPx;

        public ShadowModel(int pieceId, ShapeDef shape, int colorIndex, int landX, int landY, ShadowLook look, Vec2 center, double cellPx)
        {
            PieceId = pieceId;
            Shape = shape;
            ColorIndex = colorIndex;
            LandX = landX;
            LandY = landY;
            Look = look;
            Center = center;
            CellPx = cellPx;
        }

        public Rgba OutlineColor => Palette.GhostOutline(Look.Outline);

        public Rgba BodyColor => Palette.Block(ColorIndex).WithAlpha(Palette.GhostFill);

        /// <summary>The badge tile (null = no badge).</summary>
        public Tile? BadgeTile
        {
            get
            {
                switch (Look.Badge)
                {
                    case ShadowBadge.Ok: return Tile.Check;
                    case ShadowBadge.Warn:
                    case ShadowBadge.Support:
                    case ShadowBadge.Glass:
                        return Tile.Warn;
                    default: return null;
                }
            }
        }

        public Rgba BadgeColor => Look.Badge == ShadowBadge.Ok ? Rgba.Hex(Palette.GhostValid) : Look.Badge == ShadowBadge.Support ? Rgba.Hex(Palette.GhostSupport) : Rgba.Hex(Palette.GhostInvalid);

        /// <summary>Badge centre (design px): inside the top-right cell's corner (web `badgeCentre`).</summary>
        public Vec2 BadgeCenter(double diameter)
        {
            var cell = ShadowLook.BadgeCell(Shape);
            double left = Center.X - Shape.W * CellPx / 2, top = Center.Y - Shape.H * CellPx / 2;
            double right = left + (cell.X + 1) * CellPx;
            double cellTop = top + (Shape.H - 1 - cell.Y) * CellPx;
            return new Vec2((float)(right - diameter / 2), (float)(cellTop + diameter / 2));
        }

        public IEnumerable<Vec2> CellCenters()
        {
            foreach (var c in Shape.Cells)
            {
                var o = BoardPose.CellOffset(Shape, c, CellPx);
                yield return new Vec2(Center.X + o.X, Center.Y + o.Y);
            }
        }
    }
}
