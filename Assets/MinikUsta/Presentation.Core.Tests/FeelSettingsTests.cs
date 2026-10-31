#nullable enable
// FeelSettings and LayoutSettings defaults = the web game's tokens (src/theme/tokens.json, copied by tools/sync-from-web.sh):
// one table maps every token-backed field to its token path, so a design-lead token change shows up here as a red test
// naming the field. Values without a token (JUICE rows, web viewConstants.ts) are pinned by the JUICE tests below.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MinikUsta.Presentation.Feel;
using MinikUsta.Presentation.Layout;
using MinikUsta.Presentation.Tests.Fixtures;
using MinikUsta.Presentation.Tweening;
using NUnit.Framework;

namespace MinikUsta.Presentation.Tests
{
    [TestFixture]
    public class FeelSettingsTests
    {
        /// <summary>FeelSettings field → tokens.json path.</summary>
        private static readonly (string Field, string Token)[] FeelTokens =
        {
            ("ReducedFadeMs", "duration.reducedFade"), ("ReducedScaleMax", "a11y.reducedScaleMax"), ("ReducedParticleFactor", "particles.reducedFactor"),
            ("DragStartThresholdPx", "drag.startThresholdPx"), ("DragHoldMs", "drag.holdMs"), ("LiftScale", "drag.liftScale"),
            ("LiftMs", "duration.pick"), ("LiftGlowMs", "duration.liftGlow"), ("FingerOffsetCells", "drag.fingerOffsetCells"),
            ("FingerOffsetMs", "duration.fingerOffset"), ("TiltMaxDeg", "drag.tiltMaxDeg"), ("TrailMinSpeedCells", "drag.trailMinSpeedCells"),
            ("TetherDelayMs", "drag.tetherDelayMs"), ("TetherMinCells", "drag.tetherMinCells"), ("LeanDeg", "drag.leanDeg"),
            ("BumpPx", "drag.bumpPx"), ("BumpMs", "duration.bump"), ("CraneEnterMs", "duration.craneEnter"), ("WallPassMs", "duration.wallPass"),
            ("GhostSwitchMs", "duration.ghostSwitch"), ("BlockedShakeMs", "duration.blockedShake"), ("SteerSwipeMinPx", "drag.steerSwipeMinPx"),
            ("CancelMs", "duration.cancel"), ("YardDropMs", "duration.setYard"), ("RailClampMs", "duration.clamp"), ("GapRailMs", "duration.gapRail"),
            ("FallNormalAccel", "physics.fallNormalAccel"), ("FallNormalMax", "physics.fallNormalMax"), ("FallLowSpeed", "physics.fallLowSpeed"),
            ("FallHighAccel", "physics.fallHighAccel"), ("FallHighMax", "physics.fallHighMax"), ("YardFallAccel", "physics.yardFallAccel"),
            ("YardFallMax", "physics.yardFallMax"), ("YardCascadeStaggerMs", "physics.yardCascadeStaggerMs"), ("BalloonRiseSpeed", "physics.balloonRiseSpeed"),
            ("TruckDropStaggerMs", "physics.truckDropStaggerMs"), ("LandMs", "duration.land"), ("BalloonSettleMs", "duration.balloonSettle"),
            ("PlaceOkMs", "duration.placeOk"), ("PlaceSheenMs", "duration.placeSheen"), ("FrontShiftMs", "duration.frontShift"),
            ("PlaceBadMs", "duration.placeBad"), ("BounceToQueueMs", "duration.bounceToQueue"), ("SupportFlashMs", "duration.supportFlash"),
            ("MortarStickMs", "duration.mortarStick"), ("StreakPipMs", "duration.streakPip"), ("ComboMs", "duration.combo"),
            ("PieceReturnMs", "duration.placeBad"), ("BlocksChipFlyMs", "duration.screwCollect"), ("BlocksChipPopMs", "duration.queue"),
            ("SegmentMs", "duration.segment"), ("ScaffoldFadeMs", "duration.scaffoldFade"), ("TruckMs", "duration.truck"),
            ("QueueChipMs", "duration.queue"), ("TruckPreviewSlideMs", "duration.truckPreviewSlide"), ("TruckHelpUnchainMs", "duration.truckHelpUnchain"),
            ("ReshuffleMs", "duration.reshuffle"), ("TeardownMs", "duration.teardown"), ("TeardownStaggerMs", "duration.teardownStagger"),
            ("TeardownSettleMs", "duration.teardownSettle"), ("YardClearMs", "duration.yardClear"), ("GoldTrowelMs", "duration.goldTrowel"),
            ("HammerMs", "duration.hammer"), ("CraneBoosterMs", "duration.craneBooster"), ("BrushMs", "duration.brush"), ("UndoMs", "duration.undo"),
            ("BoosterSelectMs", "duration.boosterSelect"), ("MovesTickMs", "duration.movesTick"), ("LastMovesPulseMs", "duration.lastMovesPulse"),
            ("OfferMs", "duration.offer"), ("MovesAddMs", "duration.movesAdd"), ("GoalDoneMs", "duration.goalDone"), ("WinMs", "duration.win"),
            ("WinGlowMs", "duration.winGlow"), ("WinRibbonMs", "duration.winRibbon"), ("WinConfettiMs", "duration.winConfetti"),
            ("BonusPerMoveMs", "duration.bonusPerMove"), ("BonusMaxMs", "duration.bonusMax"), ("OutOfMovesMs", "duration.outOfMoves"),
            ("ButtonPressMs", "duration.buttonPress"), ("ButtonReleaseMs", "duration.buttonRelease"), ("PopupOpenMs", "duration.popupOpen"),
            ("PopupCloseMs", "duration.popupClose"), ("GapSqueezeMs", "duration.gapSqueeze"), ("ShutterMs", "duration.shutter"),
            ("TickMs", "duration.tick"), ("SliderMs", "duration.slider"), ("PaintGateMs", "duration.paintGate"), ("UnlockMs", "duration.unlock"),
            ("CrateHitMs", "duration.crateHit"), ("CrateBreakMs", "duration.crateBreak"), ("BagTearMs", "duration.bagTear"),
            ("ChainBreakMs", "duration.chainBreak"), ("WetDryMs", "duration.wetDry"), ("KeyPickupMs", "duration.keyPickup"),
            ("ScrewCollectMs", "duration.screwCollect"), ("DebrisMs", "duration.debris"), ("GlassBreakMs", "duration.glassBreak"),
            ("SteerMs", "duration.steer"), ("WindDriftMs", "duration.steer"), ("CarouselMs", "duration.carousel"), ("ElevatorMs", "duration.elevator"),
            ("RevealMs", "duration.reveal"), ("TargetPulseMs", "duration.targetPulse"),
        };

        /// <summary>FeelSettings ease field → tokens.json `easing.*` name.</summary>
        private static readonly (string Field, string Token)[] FeelEases =
        {
            ("LiftEase", "easing.pop"), ("BumpEase", "easing.pop"), ("CancelEase", "easing.slide"), ("YardDropEase", "easing.move"),
            ("RailClampEase", "easing.pop"), ("LandEase", "easing.pop"), ("PlaceOkEase", "easing.burst"), ("ComboEase", "easing.elastic"),
            ("SegmentEase", "easing.slide"), ("QueueChipEase", "easing.pop"), ("MovesTickEase", "easing.move"), ("FrontShiftEase", "easing.move"),
            ("BlockedShakeEase", "easing.settle"), ("WrongShakeEase", "easing.settle"), ("SupportFlashEase", "easing.settle"),
            ("BounceEase", "easing.move"), ("TruckEnterEase", "easing.pop"), ("ButtonReleaseEase", "easing.pop"), ("ButtonPressEase", "easing.move"),
            ("PopupOpenEase", "easing.pop"), ("PopupCloseEase", "easing.moveIn"), ("WinTitleEase", "easing.elastic"),
        };

        /// <summary>LayoutSettings field → tokens.json path.</summary>
        private static readonly (string Field, string Token)[] LayoutTokens =
        {
            ("DesignWidth", "meta.designWidth"), ("DesignHeight", "meta.designHeight"), ("ExpandMaxHeight", "meta.scale.expandMaxHeight"),
            ("CellPx", "layout.grid.cellPx"), ("WallW", "layout.grid.wallW"), ("CellMaxPx", "layout.adaptive.cellMaxPx"),
            ("BoardBottomY", "layout.board.boardBottomY"), ("MinGapTopPx", "layout.board.minGapTopPx"), ("ExpandShare", "layout.board.expandShare"),
            ("StatusY", "layout.board.statusY"), ("StatusH", "layout.board.statusH"), ("SiteRibbonW", "layout.board.siteRibbonW"),
            ("SiteRibbonH", "layout.board.siteRibbonH"), ("SiteRibbonTiltDeg", "layout.board.siteRibbonTiltDeg"),
            ("SiteRibbonNotchPx", "layout.board.siteRibbonNotchPx"), ("YardFramePx", "layout.adaptive.yardFramePx"),
            ("MinSideMarginPx", "layout.adaptive.minSideMarginPx"), ("TopGroupBottomY", "layout.top.groupBottomY"),
            ("HudRow1Y", "kit.v3.screen.hud.row1Y"), ("HudRow1H", "kit.v3.screen.hud.row1H"), ("LevelCapsuleW", "kit.v3.screen.hud.levelCapsuleW"),
            ("HudRow2Y", "kit.v3.screen.hud.row2Y"), ("ToolPx", "kit.v3.screen.hud.toolPx"), ("ToolLeftX", "kit.v3.screen.hud.toolLeftX"),
            ("ToolRightX", "kit.v3.screen.hud.toolRightX"), ("MovesW", "kit.v3.screen.hud.movesW"), ("MovesH", "kit.v3.screen.hud.movesH"),
            ("BoosterPx", "kit.v3.screen.hud.boosterPx"), ("BoosterGapPx", "kit.v3.screen.hud.boosterGapPx"), ("BoosterX", "kit.v3.screen.hud.boosterX"),
            ("BoosterBottomPx", "kit.v3.screen.hud.boosterBottomPx"), ("CharacterX", "layout.bottom.characterX"),
            ("CharacterBottomPx", "layout.bottom.characterBottomPx"), ("CharacterW", "layout.bottom.characterW"), ("CharacterH", "layout.bottom.characterH"),
            ("GroupTopFromBottomPx", "layout.bottom.groupTopFromBottomPx"), ("MinTargetPx", "touch.minTargetPx"), ("HitSlopPx", "touch.hitSlopPx"),
        };

        private static double FieldValue(object o, string name)
        {
            var f = o.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance) ?? throw new MissingFieldException(o.GetType().Name, name);
            var v = f.GetValue(o)!;
            return v is float x ? x : v is double d ? d : Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture);
        }

        [Test]
        public void FeelSettings_defaults_equal_the_tokens_json_values()
        {
            var feel = new FeelSettings();
            var wrong = new List<string>();
            foreach (var (field, token) in FeelTokens)
            {
                double want = PresentationPaths.Token(token).AsDouble;
                double got = FieldValue(feel, field);
                if (Math.Abs(want - got) > 1e-6) wrong.Add(field + " = " + got + ", tokens " + token + " = " + want);
            }
            CollectionAssert.IsEmpty(wrong);
        }

        [Test]
        public void FeelSettings_eases_equal_the_tokens_json_easing_names()
        {
            var feel = new FeelSettings();
            var wrong = new List<string>();
            foreach (var (field, token) in FeelEases)
            {
                var want = Easing.FromPhaserName(PresentationPaths.Token(token).AsString);
                var got = (EaseType)typeof(FeelSettings).GetField(field)!.GetValue(feel)!;
                if (want != got) wrong.Add(field + " = " + got + ", tokens " + token + " = " + Easing.PhaserName(want));
            }
            CollectionAssert.IsEmpty(wrong);
        }

        [Test]
        public void LayoutSettings_defaults_equal_the_tokens_json_values()
        {
            var layout = new LayoutSettings();
            var wrong = new List<string>();
            foreach (var (field, token) in LayoutTokens)
            {
                double want = PresentationPaths.Token(token).AsDouble;
                double got = FieldValue(layout, field);
                if (Math.Abs(want - got) > 1e-9) wrong.Add(field + " = " + got + ", tokens " + token + " = " + want);
            }
            CollectionAssert.IsEmpty(wrong);
            Assert.AreEqual(PresentationPaths.Token("meta.scale.fitHeight").AsDouble, layout.DesignHeight);
            Assert.AreEqual(PresentationPaths.Token("meta.scale.expandMinHeight").AsDouble, layout.DesignHeight);
        }

        [Test]
        public void FeelSettings_JUICE_rows_without_tokens_keep_the_web_values()
        {
            var f = new FeelSettings();
            // JUICE #1 lift, #9 yard drop, #11 landing squash, #13 wrong placement, #88, #107 (web viewConstants.ts VIEW / JUICE_VIEW)
            Assert.AreEqual(6f, f.LiftHopPx);
            Assert.AreEqual(0.94f, f.YardDropSquashY);
            Assert.AreEqual((1.12f, 0.86f), (f.LandSquashX, f.LandSquashY));
            Assert.AreEqual((0.5f, 100f, 120f, 2f, 3), (f.WrongTintAlpha, f.WrongTintMs, f.WrongShakeMs, f.WrongShakePx, f.WrongShakeCycles));
            Assert.AreEqual(1.5f, f.BounceArcCells);
            Assert.AreEqual(0.5f, f.CancelArcCells);
            Assert.AreEqual(0.3f, f.QueueShrink);
            Assert.AreEqual(1.5f, f.TeardownArcCells);
            Assert.AreEqual((12f, 120f), (f.PathMsPerCell, f.PathMaxMs), "TECH §4.4");
            Assert.AreEqual(0.6f, f.CancelPreviewAlpha, "UX §5.3");
            Assert.AreEqual((2f, 3, 1f, 120f), (f.BlockedShakePx, f.BlockedShakeCycles, f.TapHopCells, f.TapHopMs), "UX §5.3");
            Assert.AreEqual((0.6f, 1.2f), (f.PipFrom, f.PipPeak), "JUICE #15");
            Assert.AreEqual((1.4f, 1.1f), (f.ComboIconPeak, f.ComboIconRest), "JUICE #16");
            Assert.AreEqual((0.94f, 1.04f), (f.ButtonPressScale, f.ButtonReleasePeak), "JUICE #69");
            Assert.AreEqual((0.85f, 1.03f, 0.9f), (f.PopupFrom, f.PopupPeak, f.PopupCloseTo), "JUICE #70 / #71");
            Assert.AreEqual((1.06f, 600f), (f.HintPulsePeak, f.HintPulseMs), "web LevelScene HINT_PULSE_*");
            Assert.AreEqual(3f, f.LockSpeedUp, "JUICE §0 rule 3");
            Assert.AreEqual(f.WinGlowMs + f.WinRibbonMs + f.WinConfettiMs, f.WinMs, "JUICE #55: duration.win = winGlow + winRibbon + winConfetti");
        }

        [Test]
        public void FeelSettings_defaults_validate_and_bad_values_are_reported()
        {
            CollectionAssert.IsEmpty(new FeelSettings().Validate());
            var bad = new FeelSettings { LiftMs = -1, FallNormalAccel = 0, CancelPreviewAlpha = 1.5f, WrongShakeCycles = 0, LiftScale = 0 };
            var problems = bad.Validate();
            foreach (var name in new[] { "LiftMs", "FallNormalAccel", "CancelPreviewAlpha", "WrongShakeCycles", "LiftScale" })
                Assert.IsTrue(problems.Any(p => p.StartsWith(name + " ", StringComparison.Ordinal)), name + " reported: " + string.Join("; ", problems));
        }

        [Test]
        public void FeelSettings_clone_is_independent_of_the_original()
        {
            var a = FeelSettings.CreateDefault();
            var b = a.Clone();
            b.LiftMs = 500;
            Assert.AreEqual(80f, a.LiftMs);
            var la = LayoutSettings.CreateDefault();
            var lb = la.Clone();
            lb.CellMaxPx = 120;
            Assert.AreEqual(144.0, la.CellMaxPx);
        }

        [Test]
        public void Every_FeelSettings_field_is_a_public_serializable_value()
        {
            // the Q1 ScriptableObject serializes the class as is: only float, int and enum fields (Unity-serializable)
            Assert.IsTrue(Attribute.IsDefined(typeof(FeelSettings), typeof(SerializableAttribute)));
            Assert.IsTrue(Attribute.IsDefined(typeof(LayoutSettings), typeof(SerializableAttribute)));
            foreach (var f in typeof(FeelSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Assert.IsTrue(f.FieldType == typeof(float) || f.FieldType == typeof(int) || f.FieldType == typeof(EaseType), f.Name + " is " + f.FieldType.Name);
            Assert.IsEmpty(typeof(FeelSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance));
        }
    }
}
