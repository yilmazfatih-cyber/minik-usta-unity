#nullable enable
// Screen layout numbers of the level screen (UX_FLOWS §5.1, §5.8; ART_REFERENCE §9.4 HUD v3). Defaults = tokens.json
// (`meta.*`, `layout.*`, `touch.*`, `kit.v3.screen.hud.*`) and the web theme/layout.ts constants; `FeelSettingsTests`
// checks the token-backed ones against the tokens.json copy. Design px of the 1080-wide design canvas.
using System;

namespace MinikUsta.Presentation.Layout
{
    /// <summary>
    /// How the design canvas maps onto the screen (web `ScaleMode`, TECH §10.1, R-06). FIT: the design height is 1920, the
    /// canvas is centred, the rest is letterbox / pillarbox (the camera background). EXPAND: the width stays 1080 and the
    /// design height follows the screen aspect from 1920 up to 2400 (web D-015 KABUL); the board group moves down by
    /// `(H − 1920) × ExpandShare`, the HUD stays at the top, the bottom group at the bottom.
    /// </summary>
    public enum ScaleMode
    {
        Fit,
        Expand,
    }

    [Serializable]
    public sealed class LayoutSettings
    {
        /// <summary>meta.designWidth.</summary>
        public double DesignWidth = 1080;
        /// <summary>meta.designHeight (= meta.scale.fitHeight).</summary>
        public double DesignHeight = 1920;
        /// <summary>meta.scale.expandMaxHeight.</summary>
        public double ExpandMaxHeight = 2400;
        /// <summary>Faz 3 orchestrator brief: FIT (the camera keeps the 1080 × 1920 canvas inside the screen).</summary>
        public ScaleMode Mode = ScaleMode.Fit;

        // board group (UX §5.1, §5.8; layout.grid / layout.board / layout.adaptive)
        /// <summary>layout.grid.cellPx — the base cell (k = c / 120).</summary>
        public double CellPx = 120;
        /// <summary>layout.grid.wallW — the wall strip at k = 1 (60·k).</summary>
        public double WallW = 60;
        /// <summary>layout.adaptive.cellMaxPx — UX §5.8 adaptive cell upper bound (D-085, DL-2R-14).</summary>
        public double CellMaxPx = 144;
        /// <summary>TECH §2R.1: the adaptive cell is a multiple of 12 px.</summary>
        public double CellStepPx = 12;
        /// <summary>TECH §2R.1: horizontal budget = design width − 2 × 60.</summary>
        public double SideReservePx = 60;
        /// <summary>layout.board.boardBottomY — the board's bottom edge at H 1920 (thumb zone).</summary>
        public double BoardBottomY = 1488;
        /// <summary>layout.board.minGapTopPx — HUD bottom → crane area top.</summary>
        public double MinGapTopPx = 24;
        /// <summary>layout.board.expandShare.</summary>
        public double ExpandShare = 0.5;
        /// <summary>layout.board.statusY / statusH — the status strip (streak beads, truck chip).</summary>
        public double StatusY = 1520;
        public double StatusH = 80;
        /// <summary>layout.board.siteRibbon* — "Yapı tamam!" ribbon (UX §5.1, §5.8 rule 5).</summary>
        public double SiteRibbonW = 306;
        public double SiteRibbonH = 96;
        public double SiteRibbonTiltDeg = -6;
        public double SiteRibbonNotchPx = 24;
        /// <summary>layout.adaptive.yardFramePx — the 20 px wooden yard frame (outside the cells).</summary>
        public double YardFramePx = 20;
        /// <summary>layout.adaptive.minSideMarginPx — board edge → screen edge.</summary>
        public double MinSideMarginPx = 30;

        // top group (HUD v3, ART_REFERENCE §9.4; kit.v3.screen.hud)
        /// <summary>layout.top.groupBottomY — bottom of the HUD group.</summary>
        public double TopGroupBottomY = 264;
        /// <summary>web layout.ts HUD_MARGIN_X / HUD_ROW_GAP / PANORAMA_WELL_W.</summary>
        public double HudMarginX = 24;
        public double HudRowGap = 24;
        public double PanoramaWellW = 300;
        /// <summary>kit.v3.screen.hud row 1 ("Bölüm N" capsule + goal chips).</summary>
        public double HudRow1Y = 24;
        public double HudRow1H = 60;
        public double LevelCapsuleW = 220;
        /// <summary>kit.v3.screen.hud row 2 (panorama well, moves plate, pause tool).</summary>
        public double HudRow2Y = 112;
        public double ToolPx = 136;
        public double ToolLeftX = 48;
        public double ToolRightX = 896;
        public double MovesW = 300;
        public double MovesH = 136;

        // bottom group (UX §5.10; kit.v3.screen.hud boosters, layout.bottom)
        /// <summary>UX §5.1: hammer, crane, brush, undo.</summary>
        public int BoosterSlots = 4;
        public double BoosterPx = 160;
        public double BoosterGapPx = 32;
        public double BoosterX = 304;
        public double BoosterBottomPx = 120;
        /// <summary>layout.bottom.character* — Tuna + Kepçe corner.</summary>
        public double CharacterX = 16;
        public double CharacterBottomPx = 24;
        public double CharacterW = 280;
        public double CharacterH = 296;
        /// <summary>layout.bottom.groupTopFromBottomPx — top of the bottom group = H − 320.</summary>
        public double GroupTopFromBottomPx = 320;

        // touch (UX §0.1, §0.3)
        /// <summary>touch.minTargetPx — every tap target ≥ 128 px.</summary>
        public double MinTargetPx = 128;
        /// <summary>touch.hitSlopPx — invisible pad around a block cell (× k).</summary>
        public double HitSlopPx = 30;

        public static LayoutSettings CreateDefault() => new LayoutSettings();

        public LayoutSettings Clone() => (LayoutSettings)MemberwiseClone();
    }
}
