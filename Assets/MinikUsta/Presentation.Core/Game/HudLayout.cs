#nullable enable
// HUD rectangles of the placeholder level screen (ART_REFERENCE §9.4 HUD v3 frame; UX_FLOWS §5.1, §5.9), derived from
// `ScreenLayout`: row 1 = "Bölüm N" capsule + goals panel (level name, blocks-left chip), row 2 = panorama well (segment
// counter), moves plate, pause tool. Shared by the step runner (flight targets) and the Unity HUD (placement).
using MinikUsta.Presentation.Layout;

namespace MinikUsta.Presentation.Game
{
    public static class HudLayout
    {
        /// <summary>Width of the blocks-left chip at the right end of the goals panel (UX §5.9 item 1).</summary>
        public const double BlocksChipW = 260;

        public static PxRect Capsule(ScreenLayout L) => L.LevelCapsule;

        public static PxRect BlocksChip(ScreenLayout L) => new PxRect(L.Goals.Right - BlocksChipW, L.Goals.Y, BlocksChipW, L.Goals.H);

        public static PxRect LevelName(ScreenLayout L) => new PxRect(L.Goals.X, L.Goals.Y, L.Goals.W - BlocksChipW - 16, L.Goals.H);

        /// <summary>The panorama well shows the segment counter in Faz 3 Q1 (the panorama art comes later).</summary>
        public static PxRect Segments(ScreenLayout L) => L.Panorama;

        public static PxRect Moves(ScreenLayout L) => L.Moves;

        public static PxRect Pause(ScreenLayout L) => L.Pause;

        // --- Q2: streak capsule (UX §5.1 status strip, "Usta Serisi ●●●○ [mala]"), gold chip, window star ----------------------------

        /// <summary>Diameter of a streak bead (JUICE #15).</summary>
        public const double BeadPx = 32;

        /// <summary>The streak capsule at the left end of the status strip (UX §5.8 item 10: strip h 80).</summary>
        public static PxRect Streak(ScreenLayout L) => new PxRect(L.Board.Status.X, L.Board.Status.Y + 8, 510, 64);

        public static PxRect StreakLabel(ScreenLayout L)
        {
            var s = Streak(L);
            return new PxRect(s.X + 16, s.Y, 176, s.H);
        }

        /// <summary>Bead `i` (0 … 3), left to right.</summary>
        public static PxRect Bead(ScreenLayout L, int i)
        {
            var s = Streak(L);
            return new PxRect(s.X + 204 + i * 46, s.CenterY - BeadPx / 2, BeadPx, BeadPx);
        }

        public static PxRect TrowelIcon(ScreenLayout L)
        {
            var s = Streak(L);
            return new PxRect(s.X + 392, s.Y + 8, 48, 48);
        }

        /// <summary>The "×n" of the trowels held.</summary>
        public static PxRect TrowelCount(ScreenLayout L)
        {
            var s = Streak(L);
            return new PxRect(s.X + 444, s.Y, 58, s.H);
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

        /// <summary>Where #88 / #20 blocks enter and leave the truck queue: the status strip under the board.</summary>
        public static Vec2 QueueChip(ScreenLayout L) => new Vec2((float)L.Board.Status.CenterX, (float)L.Board.Status.CenterY);

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
