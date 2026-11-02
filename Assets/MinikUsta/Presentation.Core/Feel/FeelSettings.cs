#nullable enable
// Every duration, ease and scale of the game feel in ONE data class (Faz 3 Q0; D-091 game-feel list, D-096). Defaults are
// the web game's values: tokens.json (`duration.*`, `easing.*`, `drag.*`, `physics.*`, `particles.*`, `a11y.*`) and the
// JUICE.md rows (web src/scenes/level/viewConstants.ts `VIEW` / `JUICE_VIEW` for the numbers without a token).
// `FeelSettingsTests` checks every token-backed default against a copy of tokens.json. The Unity layer wraps this class
// in a ScriptableObject (Q1) so every value can be tuned in the Inspector; game RULES never live here (K-xx values such
// as the K-34 hook 5 idle times are in `StuckPulse`).
//
// Units: *Ms milliseconds, *Px design px (1080 wide), *Cells board cells, *Deg degrees, Accel cells/s², Max/Speed
// cells/s; scales and alphas are factors.
using System;
using System.Collections.Generic;
using System.Reflection;
using MinikUsta.Presentation.Tweening;

namespace MinikUsta.Presentation.Feel
{
    [Serializable]
    public sealed class FeelSettings
    {
        // --- general (JUICE §0 rules 3, 8) -----------------------------------------------------------------------------------

        /// <summary>duration.reducedFade — "Animasyonları azalt": fades replace slides (JUICE §0 rule 8).</summary>
        public float ReducedFadeMs = 150;
        /// <summary>a11y.reducedScaleMax — scale peaks in the reduced variants.</summary>
        public float ReducedScaleMax = 1.03f;
        /// <summary>particles.reducedFactor.</summary>
        public float ReducedParticleFactor = 0.2f;
        /// <summary>JUICE §0 rule 3: a touch during a locked sequence plays it 3× faster.</summary>
        public float LockSpeedUp = 3f;

        // --- drag (UX §5.3; JUICE #1–#7, #91) ----------------------------------------------------------------------------

        /// <summary>drag.startThresholdPx — a press becomes a drag after 8 px …</summary>
        public float DragStartThresholdPx = 8;
        /// <summary>drag.holdMs — … or 100 ms held.</summary>
        public float DragHoldMs = 100;
        /// <summary>drag.liftScale — JUICE #1 lift 1,00 → 1,08 (the brief's "%8 büyüme").</summary>
        public float LiftScale = 1.08f;
        /// <summary>duration.pick — JUICE #1.</summary>
        public float LiftMs = 80;
        /// <summary>easing.pop — JUICE #1.</summary>
        public EaseType LiftEase = EaseType.OutBack;
        /// <summary>JUICE #1 "6 px yukarı zıplama".</summary>
        public float LiftHopPx = 6;
        /// <summary>duration.liftGlow — JUICE #91 lift glow ring (fades out in the same time on release).</summary>
        public float LiftGlowMs = 120;
        public EaseType LiftGlowEase = EaseType.OutQuad;
        /// <summary>drag.fingerOffsetCells — the grabbed cell's centre ends 1,2 cells above the finger (UX §5.3).</summary>
        public float FingerOffsetCells = 1.2f;
        /// <summary>duration.fingerOffset — the offset glides in over 90 ms (no jump at pick time, TECH §4.4).</summary>
        public float FingerOffsetMs = 90;
        /// <summary>drag.tiltMaxDeg — JUICE #3 tilt with the horizontal speed.</summary>
        public float TiltMaxDeg = 4;
        /// <summary>UX §5.3 "120 ms'de sönümlenir".</summary>
        public float TiltDecayMs = 120;
        /// <summary>drag.trailMinSpeedCells — JUICE #3 trail above 12 cells/s.</summary>
        public float TrailMinSpeedCells = 12;
        /// <summary>drag.tetherDelayMs — JUICE #4 dotted tether after 150 ms …</summary>
        public float TetherDelayMs = 150;
        /// <summary>drag.tetherMinCells — … of more than 0,5 cell separation.</summary>
        public float TetherMinCells = 0.5f;
        /// <summary>drag.leanDeg — JUICE #4 the block leans 3° toward the finger.</summary>
        public float LeanDeg = 3;
        /// <summary>drag.bumpPx — JUICE #4 sticky-follow bump stretch.</summary>
        public float BumpPx = 6;
        /// <summary>duration.bump.</summary>
        public float BumpMs = 120;
        public EaseType BumpEase = EaseType.OutBack;
        /// <summary>JUICE #4 bump haptic at most every 400 ms.</summary>
        public float BumpHapticMs = 400;
        /// <summary>TECH §4.4: a non-adjacent node change is followed along the BFS path, 12 ms per cell …</summary>
        public float PathMsPerCell = 12;
        /// <summary>… at most 120 ms.</summary>
        public float PathMaxMs = 120;
        /// <summary>duration.craneEnter — JUICE #5 shadow stretch in the crane area.</summary>
        public float CraneEnterMs = 150;
        /// <summary>duration.wallPass — JUICE #6 wall-pass trail.</summary>
        public float WallPassMs = 200;
        /// <summary>duration.ghostSwitch — JUICE #7 shadow state change.</summary>
        public float GhostSwitchMs = 80;
        /// <summary>UX §5.3 cancel preview: the dragged block is 60 % opaque (+ "↩").</summary>
        public float CancelPreviewAlpha = 0.6f;
        /// <summary>duration.blockedShake — JUICE #2 pick blocked (K-09).</summary>
        public float BlockedShakeMs = 180;
        /// <summary>UX §5.3 "2 px sağ-sol titreme (3 döngü)".</summary>
        public float BlockedShakePx = 2;
        public int BlockedShakeCycles = 3;
        public EaseType BlockedShakeEase = EaseType.InOutSine;
        /// <summary>JUICE #2 the blocking neighbours flash white for 300 ms.</summary>
        public float BlockedFlashMs = 300;
        /// <summary>UX §5.3 tap without drag: a 1-cell hop in 120 ms.</summary>
        public float TapHopCells = 1;
        public float TapHopMs = 120;
        /// <summary>drag.steerSwipeMinPx — UX §5.6 G-L steering swipe.</summary>
        public float SteerSwipeMinPx = 48;

        // --- release (JUICE #8, #9, #22, #23) --------------------------------------------------------------------------------

        /// <summary>duration.cancel — JUICE #8 the block returns on an arc (0 moves, K-07).</summary>
        public float CancelMs = 220;
        /// <summary>easing.slide (Cubic.easeInOut).</summary>
        public EaseType CancelEase = EaseType.InOutCubic;
        /// <summary>JUICE #8 "kavisli" (web VIEW.cancelArcCells 0,5).</summary>
        public float CancelArcCells = 0.5f;
        /// <summary>duration.setYard — JUICE #9 yard drop (K-10): settles into the cell, 1,08 → 1,00.</summary>
        public float YardDropMs = 90;
        /// <summary>easing.move (Quad.easeOut).</summary>
        public EaseType YardDropEase = EaseType.OutQuad;
        /// <summary>JUICE #9 small squash y 0,94.</summary>
        public float YardDropSquashY = 0.94f;
        /// <summary>duration.clamp — JUICE #23 rail park (K-12).</summary>
        public float RailClampMs = 120;
        public EaseType RailClampEase = EaseType.OutBack;
        /// <summary>duration.gapRail — JUICE #22 gap pass light flow (W1).</summary>
        public float GapRailMs = 150;

        // --- falls (JUICE §0.1, #10, #11, #43, #44; tokens.physics — accelerated, never "ms per row") -------------------------

        /// <summary>physics.fallNormalAccel — normal gravity (1 cell ≈ 150 ms, 6 cells ≈ 395 ms).</summary>
        public float FallNormalAccel = 90;
        /// <summary>physics.fallNormalMax.</summary>
        public float FallNormalMax = 22;
        /// <summary>physics.fallLowSpeed — G-L constant glide.</summary>
        public float FallLowSpeed = 4.5f;
        /// <summary>physics.fallHighAccel — G-H.</summary>
        public float FallHighAccel = 300;
        /// <summary>physics.fallHighMax.</summary>
        public float FallHighMax = 40;
        /// <summary>physics.yardFallAccel — yard gravity, truck and K-17 drops.</summary>
        public float YardFallAccel = 80;
        /// <summary>physics.yardFallMax.</summary>
        public float YardFallMax = 20;
        /// <summary>physics.yardCascadeStaggerMs — JUICE #44 column stagger.</summary>
        public float YardCascadeStaggerMs = 30;
        /// <summary>physics.balloonRiseSpeed — JUICE #43.</summary>
        public float BalloonRiseSpeed = 5;
        /// <summary>physics.truckDropStaggerMs — JUICE #19.</summary>
        public float TruckDropStaggerMs = 60;
        /// <summary>duration.land — JUICE #11 landing squash.</summary>
        public float LandMs = 120;
        /// <summary>easing.pop (Back.easeOut: the overshoot is the bounce).</summary>
        public EaseType LandEase = EaseType.OutBack;
        /// <summary>JUICE #11 squash x 1,12.</summary>
        public float LandSquashX = 1.12f;
        /// <summary>JUICE #11 squash y 0,86.</summary>
        public float LandSquashY = 0.86f;
        /// <summary>duration.balloonSettle — JUICE #43.</summary>
        public float BalloonSettleMs = 200;

        // --- placement (JUICE #12–#16, #83, #84, #88, #92) -------------------------------------------------------------------

        /// <summary>duration.placeOk — JUICE #12 correct placement: the plan cells fill, white flash ("kilit parlaması").</summary>
        public float PlaceOkMs = 200;
        /// <summary>easing.burst (Expo.easeOut).</summary>
        public EaseType PlaceOkEase = EaseType.OutExpo;
        /// <summary>JUICE #12 / #92 white ADD flash peak alpha (0 → 0,6 → 0).</summary>
        public float PlaceFlashPeak = 0.6f;
        /// <summary>duration.placeSheen — JUICE #92 flash + 6 sparkles after the fill.</summary>
        public float PlaceSheenMs = 260;
        /// <summary>duration.frontShift — JUICE #83 build front.</summary>
        public float FrontShiftMs = 160;
        public EaseType FrontShiftEase = EaseType.OutQuad;
        /// <summary>duration.placeBad — JUICE #13 wrong placement (K-17): tint, shake, arc bounce.</summary>
        public float PlaceBadMs = 350;
        /// <summary>JUICE #13 red tint (color.ghost.invalid) at 50 % for 100 ms.</summary>
        public float WrongTintAlpha = 0.5f;
        public float WrongTintMs = 100;
        /// <summary>JUICE #13 the shake before the bounce (web JUICE_VIEW.wrongShakeMs).</summary>
        public float WrongShakeMs = 120;
        /// <summary>
        /// JUICE #13 says "2 px yatay sallanma ×3" (block shake, never the screen: JUICE §0 rule 5); the Unity brief (Q2 item 2, "8 px
        /// titreme") asks for 8 px — 2 px on the 1080-wide canvas is under a phone pixel. Set 2 to get the web value back.
        /// </summary>
        public float WrongShakePx = 8;
        public int WrongShakeCycles = 3;
        /// <summary>JUICE #13 sallanma Sine.easeInOut.</summary>
        public EaseType WrongShakeEase = EaseType.InOutSine;
        /// <summary>JUICE #13 sekme Quad.easeOut.</summary>
        public EaseType BounceEase = EaseType.OutQuad;
        /// <summary>JUICE #13 arc height 1,5 cells.</summary>
        public float BounceArcCells = 1.5f;
        /// <summary>duration.bounceToQueue — JUICE #88 into the truck chip (K-17 step 3).</summary>
        public float BounceToQueueMs = 400;
        public EaseType BounceToQueueEase = EaseType.InOutQuad;
        /// <summary>JUICE #88 the block shrinks to 30 % on its way into the chip.</summary>
        public float QueueShrink = 0.3f;
        /// <summary>duration.supportFlash — JUICE #84 missing support hatch (3 blinks).</summary>
        public float SupportFlashMs = 600;
        public EaseType SupportFlashEase = EaseType.InOutSine;
        public int SupportBlinks = 3;
        /// <summary>duration.mortarStick — JUICE #14 (Y8).</summary>
        public float MortarStickMs = 300;
        public EaseType MortarStickEase = EaseType.OutBack;
        /// <summary>duration.streakPip — JUICE #15 bead 0,6 → 1,2 → 1,0.</summary>
        public float StreakPipMs = 160;
        public float PipFrom = 0.6f;
        public float PipPeak = 1.2f;
        /// <summary>duration.combo — JUICE #16 Golden Trowel earned (K-33; the brief's "booster oluşumu").</summary>
        public float ComboMs = 500;
        /// <summary>easing.elastic.</summary>
        public EaseType ComboEase = EaseType.OutElastic;
        public float ComboIconPeak = 1.4f;
        public float ComboIconRest = 1.1f;
        /// <summary>A glass / hammer return (`pieceReturned`) flies like the wrong-placement bounce (web plan.ts: duration.placeBad).</summary>
        public float PieceReturnMs = 350;

        // --- blocks-left chip (UX §5.9 item 1; D-091 "hedef taşlar HUD'a uçar" → flight to the chip) -------------------------

        /// <summary>
        /// D-091 counterpart of the match-3 "goal items fly to the HUD": after a correct placement a small block icon flies on
        /// an arc to the blocks-left chip, then the chip pops. No JUICE row yet: 500 ms Quad.easeInOut = the JUICE #40
        /// "flies to the counter" pattern (duration.screwCollect).
        /// </summary>
        public float BlocksChipFlyMs = 500;
        public EaseType BlocksChipFlyEase = EaseType.InOutQuad;
        /// <summary>Arc of that flight (cells of the board; the JUICE #13 / #17 arc).</summary>
        public float BlocksChipFlyArcCells = 1.5f;
        /// <summary>UX §5.9: the chip 1,0 → 1,15 → 1,0 (JUICE #20 format, duration.queue).</summary>
        public float BlocksChipPeak = 1.15f;
        public float BlocksChipPopMs = 200;

        // --- board sequences (JUICE #18–#21, #94, #107) ----------------------------------------------------------------------

        /// <summary>duration.segment — JUICE #18 segment slide (K-22), input locked.</summary>
        public float SegmentMs = 600;
        /// <summary>JUICE #18 kayma Cubic.easeInOut.</summary>
        public EaseType SegmentEase = EaseType.InOutCubic;
        /// <summary>duration.scaffoldFade.</summary>
        public float ScaffoldFadeMs = 200;
        /// <summary>JUICE #18 screen shake 3 px (one of the only two screen shakes, JUICE §0 rule 5).</summary>
        public float SegmentShakePx = 3;
        public float SegmentFlashPeak = 0.4f;
        public int SegmentFlashes = 2;
        /// <summary>duration.truck — JUICE #19 truck delivery (K-25), input locked.</summary>
        public float TruckMs = 700;
        public float TruckEnterMs = 300;
        public float TruckExitMs = 200;
        public EaseType TruckEnterEase = EaseType.OutBack;
        public EaseType TruckExitEase = EaseType.InQuad;
        /// <summary>duration.queue — JUICE #20 queue chip bump (K-26).</summary>
        public float QueueChipMs = 200;
        public EaseType QueueChipEase = EaseType.OutBack;
        public float QueueChipPeak = 1.15f;
        /// <summary>duration.truckPreviewSlide.</summary>
        public float TruckPreviewSlideMs = 160;
        /// <summary>duration.truckHelpUnchain — JUICE #21a.</summary>
        public float TruckHelpUnchainMs = 600;
        /// <summary>duration.reshuffle — JUICE #21c. Dead since RULES_VERSION 3 (D-099: the D1 reshuffle was removed; the token stays).</summary>
        public float ReshuffleMs = 900;
        /// <summary>duration.teardown — JUICE #107 Söküm (K-30), input locked.</summary>
        public float TeardownMs = 600;
        /// <summary>duration.teardownStagger — "son yerleşenden başlayarak 80 ms arayla".</summary>
        public float TeardownStaggerMs = 80;
        /// <summary>duration.teardownSettle — the block settles 120 ms before #107 (JUICE §0 rule 14).</summary>
        public float TeardownSettleMs = 120;
        /// <summary>JUICE #107 arc 1,5 cells.</summary>
        public float TeardownArcCells = 1.5f;
        /// <summary>duration.yardClear — JUICE #94 "Bütün bloklar yerinde" (K-48).</summary>
        public float YardClearMs = 450;
        /// <summary>JUICE #94 the `win.clear` ribbon stays 600 ms.</summary>
        public float YardClearRibbonMs = 600;

        // --- boosters (JUICE #17, #60–#64) -----------------------------------------------------------------------------------

        /// <summary>duration.goldTrowel — JUICE #17 Golden Trowel flight (K-33).</summary>
        public float GoldTrowelMs = 450;
        public EaseType GoldTrowelEase = EaseType.InOutQuad;
        public float TrowelArcCells = 1.5f;
        /// <summary>duration.hammer — JUICE #61 (K-36).</summary>
        public float HammerMs = 450;
        /// <summary>JUICE #61 the Ağır Yük shrinks to 0,6 while it fades.</summary>
        public float SmashScale = 0.6f;
        /// <summary>duration.craneBooster — JUICE #62 (K-37).</summary>
        public float CraneBoosterMs = 900;
        /// <summary>duration.brush — JUICE #63 (K-38).</summary>
        public float BrushMs = 500;
        /// <summary>duration.undo — JUICE #64 (K-39).</summary>
        public float UndoMs = 400;
        /// <summary>duration.boosterSelect — JUICE #60.</summary>
        public float BoosterSelectMs = 180;

        // --- HUD, offer and level end (JUICE #50–#57) ------------------------------------------------------------------------

        /// <summary>duration.movesTick — JUICE #50 counter roll.</summary>
        public float MovesTickMs = 180;
        public EaseType MovesTickEase = EaseType.OutQuad;
        /// <summary>JUICE #50 the digit slides 48 px.</summary>
        public float MovesRollPx = 48;
        /// <summary>duration.lastMovesPulse — JUICE #51 loop period.</summary>
        public float LastMovesPulseMs = 1000;
        public float LastMovesPulse = 1.06f;
        public float LastMovesStrong = 1.12f;
        /// <summary>duration.offer — JUICE #52 +5 window.</summary>
        public float OfferMs = 300;
        /// <summary>duration.movesAdd — JUICE #53 five "+1" chips.</summary>
        public float MovesAddMs = 500;
        public EaseType MovesAddEase = EaseType.InOutQuad;
        public int MovesAddChips = 5;
        public float MovesAddStaggerMs = 60;
        public float MovesAddBump = 1.2f;
        /// <summary>duration.goalDone — JUICE #54.</summary>
        public float GoalDoneMs = 300;
        /// <summary>duration.win — JUICE #55 = winGlow 400 + winRibbon 600 + winConfetti 1500.</summary>
        public float WinMs = 2500;
        public float WinGlowMs = 400;
        public float WinRibbonMs = 600;
        public float WinConfettiMs = 1500;
        public float WinTitlePeak = 1.1f;
        public float WinTitleMs = 500;
        public EaseType WinTitleEase = EaseType.OutElastic;
        /// <summary>duration.bonusPerMove — JUICE #56 Bonus İnşaat: one left-over move → gold every 120 ms …</summary>
        public float BonusPerMoveMs = 120;
        /// <summary>duration.bonusMax — … at most 1,2 s.</summary>
        public float BonusMaxMs = 1200;
        public EaseType BonusEase = EaseType.OutQuad;
        /// <summary>JUICE #56 reduced: the total is counted in 300 ms.</summary>
        public float BonusReducedMs = 300;
        /// <summary>JUICE #56 one coin flies for three bonus steps.</summary>
        public float BonusCoinFlyMs = 360;
        /// <summary>duration.outOfMoves — JUICE #57.</summary>
        public float OutOfMovesMs = 400;
        /// <summary>JUICE #57 the board darkens 20 %.</summary>
        public float LoseDim = 0.2f;

        // --- buttons and windows (JUICE #69–#71, #97) ------------------------------------------------------------------------

        /// <summary>duration.buttonPress — JUICE #69 press 0,94.</summary>
        public float ButtonPressMs = 60;
        public float ButtonPressScale = 0.94f;
        public EaseType ButtonPressEase = EaseType.OutQuad;
        /// <summary>duration.buttonRelease — JUICE #69 release 1,04 → 1,0 (Back.easeOut).</summary>
        public float ButtonReleaseMs = 120;
        public float ButtonReleasePeak = 1.04f;
        public EaseType ButtonReleaseEase = EaseType.OutBack;
        /// <summary>duration.popupOpen — JUICE #70.</summary>
        public float PopupOpenMs = 220;
        public float PopupFrom = 0.85f;
        public float PopupPeak = 1.03f;
        public EaseType PopupOpenEase = EaseType.OutBack;
        /// <summary>duration.popupClose — JUICE #71.</summary>
        public float PopupCloseMs = 160;
        public float PopupCloseTo = 0.9f;
        public EaseType PopupCloseEase = EaseType.InQuad;
        public float PopupDimMs = 180;

        // --- obstacles (JUICE §2–§3; Phase 3 rules — the planner already times their events) ---------------------------------

        /// <summary>duration.gapSqueeze — JUICE #24 (W3).</summary>
        public float GapSqueezeMs = 160;
        /// <summary>duration.shutter — JUICE #25 (W4).</summary>
        public float ShutterMs = 350;
        /// <summary>duration.tick — JUICE #26 / #37 counter tick.</summary>
        public float TickMs = 150;
        /// <summary>duration.slider — JUICE #27 (W5).</summary>
        public float SliderMs = 300;
        /// <summary>duration.paintGate — W6 paint gate.</summary>
        public float PaintGateMs = 300;
        /// <summary>duration.unlock — W7 lock opens.</summary>
        public float UnlockMs = 700;
        /// <summary>duration.crateHit — JUICE #32 (Y1).</summary>
        public float CrateHitMs = 250;
        /// <summary>duration.crateBreak — JUICE #33.</summary>
        public float CrateBreakMs = 400;
        /// <summary>duration.bagTear — JUICE #34 (Y2).</summary>
        public float BagTearMs = 400;
        /// <summary>duration.chainBreak — JUICE #36 (Y3).</summary>
        public float ChainBreakMs = 450;
        /// <summary>duration.wetDry — JUICE #38 (Y4).</summary>
        public float WetDryMs = 350;
        /// <summary>duration.keyPickup — JUICE #39.</summary>
        public float KeyPickupMs = 400;
        /// <summary>duration.screwCollect — JUICE #40 (Y7).</summary>
        public float ScrewCollectMs = 500;
        /// <summary>duration.debris — JUICE #41 (S4).</summary>
        public float DebrisMs = 300;
        /// <summary>duration.glassBreak — JUICE #42 (S3).</summary>
        public float GlassBreakMs = 500;
        /// <summary>duration.steer — JUICE #46 (G-L).</summary>
        public float SteerMs = 160;
        /// <summary>W8 fan drift (one column, before the fall): no token yet — the JUICE #46 one-column slide.</summary>
        public float WindDriftMs = 160;
        /// <summary>duration.carousel — JUICE #47 (S5).</summary>
        public float CarouselMs = 500;
        /// <summary>duration.elevator — JUICE #48 (S6).</summary>
        public float ElevatorMs = 300;
        /// <summary>duration.reveal — JUICE #49 (K-32).</summary>
        public float RevealMs = 350;

        // --- hints (K-34 hook 5 presentation; UX §5.2) ---------------------------------------------------------------------

        /// <summary>web LevelScene HINT_PULSE_MS — one stuck / unlock pulse.</summary>
        public float HintPulseMs = 600;
        /// <summary>web LevelScene HINT_PULSE_PEAK — the pulse scale (1,0 → 1,06 → 1,0).</summary>
        public float HintPulsePeak = 1.06f;
        /// <summary>duration.targetPulse — booster target pulse period (UX §5.2).</summary>
        public float TargetPulseMs = 1200;

        // --- Q2 game feel (D-091 list: the brief's "game feel" items in this game's terms; every value tunable) -------------------

        /// <summary>Q2 (1): a released block settles back to scale 1 with Back.easeOut ("bırakma/iniş OutBack"); JUICE #9 / #10 move keeps its own ease.</summary>
        public EaseType ReleaseScaleEase = EaseType.OutBack;
        /// <summary>Q2 (3): a multi-cell block lands cell by cell, this many ms apart (the 20–30 ms stagger; 0 = the block squashes as one).</summary>
        public float CellStaggerMs = 25;
        /// <summary>Q2 (3): the stagger of the last cell never exceeds this (big blocks stay within one landing beat of `LandMs` 120).</summary>
        public float CellStaggerCapMs = 60;
        /// <summary>Q2 (4): a correct placement grows 1,0 → 1,2 and shrinks back into its lock ("küçülerek kilitlenme").</summary>
        public float PlacePulsePeak = 1.2f;
        public float PlacePulseMs = 180;
        /// <summary>Q2 (4): the peak sits at this share of the pulse.</summary>
        public float PlacePulseSplit = 0.45f;
        public EaseType PlacePulseEase = EaseType.OutQuad;

        // particles: counts are tokens `particles.*` (JUICE rule 4: ≤ 120 alive, ≤ 40 per burst; reduced motion ≤ 20 %)

        /// <summary>particles.maxOnScreen.</summary>
        public int ParticleMaxOnScreen = 120;
        /// <summary>particles.maxPerBurst.</summary>
        public int ParticleMaxPerBurst = 40;
        /// <summary>particles.land — JUICE #11 landing dust.</summary>
        public int ParticleLand = 10;
        /// <summary>particles.placeOk — JUICE #12 sparks.</summary>
        public int ParticlePlaceOk = 14;
        /// <summary>particles.combo — JUICE #16 gold rain.</summary>
        public int ParticleCombo = 30;
        /// <summary>particles.segment — JUICE #18 confetti.</summary>
        public int ParticleSegment = 24;
        /// <summary>particles.win — JUICE #55 confetti per wave.</summary>
        public int ParticleWin = 40;
        /// <summary>particles.winWaves.</summary>
        public int ParticleWinWaves = 2;
        /// <summary>particles.starBurst — JUICE #96.</summary>
        public int ParticleStarBurst = 16;
        /// <summary>particles.sparkle.</summary>
        public int ParticleSparkle = 6;
        /// <summary>JUICE #9 yard drop dust, #44 yard cascade dust (3), #13 bounce dust (3), #15 gold sparks, #56 coin sparks, #19 / #107 dust per block.</summary>
        public int ParticleDustYard = 4;
        public int ParticleDustCascade = 3;
        public int ParticleDustBounce = 3;
        public int ParticlePip = 4;
        public int ParticleBonus = 4;
        public int ParticleDustBlock = 4;
        /// <summary>JUICE #12 "yukarı 120–220 px".</summary>
        public float SparkRiseMinPx = 120;
        public float SparkRiseMaxPx = 220;
        /// <summary>Half-angle (degrees) of the cone around "up" the sparks fly in.</summary>
        public float SparkConeDeg = 42;
        public float SparkLifeMs = 520;
        public float SparkSizePx = 40;
        /// <summary>JUICE #11 "yatay yayılım ±60 px".</summary>
        public float DustSpreadPx = 60;
        public float DustRisePx = 26;
        public float DustLifeMs = 380;
        public float DustSizePx = 64;
        /// <summary>Dust opacity at its start.</summary>
        public float DustAlpha = 0.55f;
        /// <summary>JUICE #16 the gold rain falls this far (px) from above the trowel.</summary>
        public float GoldRainHeightPx = 260;
        public float GoldRainLifeMs = 800;
        public float ConfettiSizePx = 26;
        /// <summary>JUICE #18 / #55 how far (px) a confetti piece falls.</summary>
        public float ConfettiFallPx = 620;
        public float ConfettiSegmentLifeMs = 900;
        /// <summary>duration.starBurst — JUICE #96 (the win star).</summary>
        public float StarBurstMs = 600;
        public float StarBurstMinPx = 160;
        public float StarBurstMaxPx = 320;
        public float StarSizePx = 46;
        /// <summary>JUICE #96: the central star 0 → 1,2 → 1,0 and the ring 0,6 → 1,6 (α 0,6 → 0).</summary>
        public float WinStarPeak = 1.2f;
        public float StarRingFrom = 0.6f;
        public float StarRingTo = 1.6f;
        public float StarRingAlpha = 0.6f;
        /// <summary>The confetti waves of the win start this far apart (JUICE #55: 2 waves in the 1,5 s).</summary>
        public float WinConfettiWaveGapMs = 500;

        // camera shake and haptics (JUICE §0 rules 5, 7; Q2 item 12)

        /// <summary>Q2 (12): one camera shake lasts 120 ms.</summary>
        public float CameraShakeMs = 120;
        public int CameraShakeCycles = 3;
        /// <summary>JUICE #42 glass break shake 2 px (JUICE #18's 3 px is `SegmentShakePx`).</summary>
        public float GlassShakePx = 2;
        /// <summary>Q2 (12) asks for a light shake at the win too (JUICE rule 5 lists only #18 and #42); 0 turns it off.</summary>
        public float WinShakePx = 3;
        /// <summary>Haptics closer than this are dropped (one buzz per beat).</summary>
        public float HapticMinGapMs = 120;

        // streak and trowel (JUICE #15, #16, #107), truck (#19), site slide (#18), bonus coins (#56)

        /// <summary>JUICE #16 "dönerek parlar": the trowel icon turns this far while it pops.</summary>
        public float ComboSpinDeg = 360;
        /// <summary>JUICE #107 "Usta Serisi boncukları 200 ms'de söner".</summary>
        public float StreakResetMs = 200;
        /// <summary>JUICE #19 "kasası kalkar": the truck tips back this far while it unloads.</summary>
        public float TruckBedTiltDeg = 12;
        public float TruckBedTiltMs = 120;
        /// <summary>JUICE #56 the coin peaks at this scale on its way to the gold chip.</summary>
        public float BonusCoinPeak = 1.25f;
        /// <summary>The gold chip pops by this much with each coin.</summary>
        public float GoldChipPeak = 1.18f;
        public float GoldChipPopMs = 140;

        // stuck pulse glow (K-34 hook 5, Q2 item 10: "tutulabilir bloklar hafif parlar")

        /// <summary>The white glow (0 … 1) the hinted blocks get during the stuck pulse.</summary>
        public float HintGlowPeak = 0.3f;

        /// <summary>The defaults (a fresh instance).</summary>
        public static FeelSettings CreateDefault() => new FeelSettings();

        /// <summary>A copy (the ScriptableObject keeps its own, runtime changes do not leak back).</summary>
        public FeelSettings Clone() => (FeelSettings)MemberwiseClone();

        /// <summary>
        /// Problems of a tuned instance (the ScriptableObject `OnValidate` shows them): every *Ms ≥ 0, scales and
        /// physics > 0, alphas 0 … 1, cycle / blink counts ≥ 1. Empty = valid.
        /// </summary>
        public List<string> Validate()
        {
            var o = new List<string>();
            foreach (var f in typeof(FeelSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object? v = f.GetValue(this);
                if (v is float x)
                {
                    if (float.IsNaN(x) || float.IsInfinity(x)) o.Add(f.Name + " is not a number");
                    else if (f.Name.EndsWith("Ms", StringComparison.Ordinal) && x < 0) o.Add(f.Name + " = " + Num.Text(x) + " < 0 ms");
                    else if ((f.Name.EndsWith("Accel", StringComparison.Ordinal) || f.Name.EndsWith("Max", StringComparison.Ordinal) || f.Name.EndsWith("Speed", StringComparison.Ordinal)) && x <= 0)
                        o.Add(f.Name + " = " + Num.Text(x) + " must be > 0");
                    else if ((f.Name.EndsWith("Alpha", StringComparison.Ordinal) || f.Name.EndsWith("Peak", StringComparison.Ordinal) && f.Name.Contains("Flash")) && (x < 0 || x > 1))
                        o.Add(f.Name + " = " + Num.Text(x) + " is outside 0 … 1");
                    else if ((f.Name.EndsWith("Scale", StringComparison.Ordinal) || f.Name.EndsWith("Shrink", StringComparison.Ordinal)) && x <= 0)
                        o.Add(f.Name + " = " + Num.Text(x) + " must be > 0");
                }
                else if (v is int n && n < 1) o.Add(f.Name + " = " + n + " must be ≥ 1");
            }
            return o;
        }
    }
}
