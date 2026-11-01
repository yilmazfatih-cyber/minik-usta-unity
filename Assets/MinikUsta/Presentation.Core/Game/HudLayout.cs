#nullable enable
// HUD rectangles of the level screen (ART_REFERENCE §9.4 HUD v3 frame; UX_FLOWS §5.1, §5.9; ART_UNITY §4.4), derived from
// `ScreenLayout`: row 1 = "Bölüm N" capsule + blocks-left chip + streak capsule (beads, Golden Trowel), row 2 = panorama well
// (level name, plan picture, segment counter), moves plate, pause tool; the truck chip on the status strip. Shared by the step
// runner (flight targets), the HUD tree (`LevelHud`) and the window star (`WindowViews`).
using MinikUsta.Presentation.Layout;

namespace MinikUsta.Presentation.Game
{
    public static class HudLayout
    {
        /// <summary>Width of the blocks-left chip (V1: row 1 left of the streak capsule, the v4 mock).</summary>
        public const double BlocksChipW = 250;
        /// <summary>Gap between the "Bölüm N" capsule and the blocks chip (the chip icon overflows left into it).</summary>
        public const double BlocksChipInset = 32;

        public static PxRect Capsule(ScreenLayout L) => L.LevelCapsule;

        /// <summary>V1: the blocks-left chip at the left of the goals row (icon_blocks overflows its left end).</summary>
        public static PxRect BlocksChip(ScreenLayout L) => new PxRect(L.Goals.X + BlocksChipInset, L.Goals.Y, BlocksChipW, L.Goals.H);

        /// <summary>V1: the level name sits in the panorama well (left part; the plan picture is on the right).</summary>
        public static PxRect LevelName(ScreenLayout L) => new PxRect(L.Panorama.X + 12, L.Panorama.Y + 12, 150, 56);

        /// <summary>The panorama well: the level name, the plan picture of the visible segment and the segment counter.</summary>
        public static PxRect Segments(ScreenLayout L) => L.Panorama;

        /// <summary>V1: the plan picture area inside the panorama well.</summary>
        public static PxRect PanoramaPlan(ScreenLayout L) => new PxRect(L.Panorama.X + 170, L.Panorama.Y + 12, L.Panorama.W - 182, L.Panorama.H - 24);

        public static PxRect Moves(ScreenLayout L) => L.Moves;

        public static PxRect Pause(ScreenLayout L) => L.Pause;

        // --- streak capsule (UX §5.1 "Usta Serisi ●●●○ [mala]"; V1: row 1 right of the blocks chip, the v4 mock) -------------

        /// <summary>Diameter of a streak bead (JUICE #15).</summary>
        public const double BeadPx = 40;

        /// <summary>The streak capsule: the rest of the goals row after the blocks chip.</summary>
        public static PxRect Streak(ScreenLayout L)
        {
            var b = BlocksChip(L);
            double x = b.Right + 50;
            return new PxRect(x, L.Goals.Y, L.Goals.Right - x, L.Goals.H);
        }

        public static PxRect StreakLabel(ScreenLayout L)
        {
            var s = Streak(L);
            return new PxRect(s.X + 8, s.Y, 178, s.H);
        }

        /// <summary>Bead `i` (0 … 3), left to right.</summary>
        public static PxRect Bead(ScreenLayout L, int i)
        {
            var s = Streak(L);
            return new PxRect(s.X + 188 + i * 44, s.CenterY - BeadPx / 2, BeadPx, BeadPx);
        }

        public static PxRect TrowelIcon(ScreenLayout L)
        {
            var s = Streak(L);
            return new PxRect(s.X + 366, s.Y + 5, 50, 50);
        }

        /// <summary>The "×n" of the trowels held.</summary>
        public static PxRect TrowelCount(ScreenLayout L)
        {
            var s = Streak(L);
            return new PxRect(s.X + 416, s.Y, s.W - 418, s.H);
        }

        /// <summary>V1: the truck chip riding the tray's bottom edge at the right of the status strip (ART_UNITY §4.4 `Status`).</summary>
        public static PxRect TruckChip(ScreenLayout L)
        {
            var st = L.Board.Status;
            return new PxRect(st.Right - 250, st.Y - 8, 240, 64);
        }

        /// <summary>The gold chip between the moves plate and the pause tool (JUICE #56: the coins fly there).</summary>
        public static PxRect GoldChip(ScreenLayout L) => new PxRect(L.Moves.Right + 16, L.Moves.Y + 40, L.Pause.X - L.Moves.Right - 32, 60);

        /// <summary>The level star over the top edge of a window with `options` buttons (JUICE #96).</summary>
        public static PxRect WindowStar(ScreenLayout L, LayoutSettings s, int options)
        {
            var w = Window(L, s, options).Panel;
            const double size = 150;
            return new PxRect(w.CenterX - size / 2, w.Y - 20 - size / 2, size, size);
        }

        /// <summary>Where #88 / #20 blocks enter and leave the truck queue: the truck chip on the status strip.</summary>
        public static Vec2 QueueChip(ScreenLayout L) => Center(TruckChip(L));

        public static Vec2 Center(PxRect r) => new Vec2((float)r.CenterX, (float)r.CenterY);

        /// <summary>The label band on top of the moves plate (kit.v3 movesBandH).</summary>
        public static PxRect MovesBand(ScreenLayout L, LayoutSettings s) => new PxRect(L.Moves.X, L.Moves.Y, L.Moves.W, s.MovesBandH);

        /// <summary>The number area of the moves plate (below the band).</summary>
        public static PxRect MovesNumber(ScreenLayout L, LayoutSettings s) => new PxRect(L.Moves.X, L.Moves.Y + s.MovesBandH, L.Moves.W, L.Moves.H - s.MovesBandH);

        /// <summary>
        /// A level window with `options` buttons (UX §0.2, layout.popup): the panel's bottom edge at H − panelBottomPx, the
        /// options from the panel bottom (inset optionsBottomInsetPx) upwards, option 0 lowest (the primary choice sits in
        /// the thumb zone), the title and body bands above them.
        /// </summary>
        public static WindowRects Window(ScreenLayout L, LayoutSettings s, int options)
        {
            int n = System.Math.Max(0, options);
            double stack = n * s.PopupOptionH + System.Math.Max(0, n - 1) * s.PopupOptionGap;
            double h = s.PopupTitleH + s.PopupBodyH + stack + s.PopupOptionsBottomInsetPx;
            double w = s.PopupOptionW + 2 * s.PopupSideInsetPx;
            double bottom = L.H - s.PopupPanelBottomPx;
            var panel = new PxRect((L.W - w) / 2, bottom - h, w, h);
            var opts = new PxRect[n];
            for (int i = 0; i < n; i++)
            {
                double y = bottom - s.PopupOptionsBottomInsetPx - s.PopupOptionH - i * (s.PopupOptionH + s.PopupOptionGap);
                opts[i] = new PxRect((L.W - s.PopupOptionW) / 2, y, s.PopupOptionW, s.PopupOptionH);
            }
            return new WindowRects(panel, new PxRect(panel.X, panel.Y, w, s.PopupTitleH), new PxRect(panel.X, panel.Y + s.PopupTitleH, w, s.PopupBodyH), opts);
        }
    }

    public sealed class WindowRects
    {
        public readonly PxRect Panel;
        public readonly PxRect Title;
        public readonly PxRect Body;
        /// <summary>Option buttons, 0 = lowest.</summary>
        public readonly PxRect[] Options;

        public WindowRects(PxRect panel, PxRect title, PxRect body, PxRect[] options)
        {
            Panel = panel;
            Title = title;
            Body = body;
            Options = options;
        }
    }
}
