#nullable enable
// The Faz 3 Q2 game-feel layer (the D-091 list: lift / release, bad pass, site fall, correct placement, Golden Trowel, segment +
// truck, Söküm, blocks chip, win, stuck pulse, buttons, camera, haptics) end to end through the real level player on a
// `FakeView`: finger → core → plan → steps → views. Rule ids in the test names: K-09 … K-48 and the JUICE rows they play.
using System;
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core;
using MinikUsta.Presentation.Animation;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Feel;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Input;
using MinikUsta.Presentation.Tests.Fixtures;
using MinikUsta.Presentation.Tweening;
using MinikUsta.Presentation.Ui;
using NUnit.Framework;

namespace MinikUsta.Presentation.Tests
{
    public sealed class RecordingHaptics : IHapticSink
    {
        public readonly List<HapticKind> Kinds = new List<HapticKind>();

        public void Vibrate(HapticKind kind) => Kinds.Add(kind);
    }

    public sealed class GameFeelTests
    {
        private static readonly Rgba Gold = Rgba.Hex(Palette.Gold);

        // --- helpers ------------------------------------------------------------------------------------------------------

        /// <summary>Frames until the plan, the tweens, the particles and the coins are done, `each` after every frame.</summary>
        private static float Run(Rig rig, Action? each = null, float maxMs = 30000)
        {
            float t = 0;
            var p = rig.Player;
            while ((p.Queue.Busy || p.Clock.Count > 0 || p.Particles.Alive > 0 || p.CoinsInFlight > 0) && t < maxMs)
            {
                p.Tick(Rig.FrameMs);
                t += Rig.FrameMs;
                each?.Invoke();
            }
            Assert.Less(t, maxMs, "the plan finished");
            return t;
        }

        /// <summary>Particles of `kind` emitted so far (B2: confetti counts both its rectangle and its round 3D pieces).</summary>
        private static int Count(Rig rig, SparkKind kind) =>
            rig.View.SparkLog.Count(s => s.Kind == kind || (kind == SparkKind.Confetti && s.Kind == SparkKind.ConfettiRound));

        /// <summary>Plays the golden solution of `level` by finger, up to and including drag `upTo`; each drag settles first.</summary>
        private static void PlayTo(Rig rig, int level, int upTo, bool archive = false)
        {
            var drags = Play.SolverGolden(level, archive).Drags;
            for (int i = 0; i <= upTo; i++)
            {
                rig.DragTo(drags[i]);
                if (i < upTo) rig.Settle();
            }
        }

        /// <summary>Holds `pieceId` over node `to` with a finger (the PreviewTests technique) and leaves the finger down.</summary>
        private static void HoldOver(Rig rig, int pieceId, DragNode to)
        {
            rig.PressPoint(pieceId, out double x, out double y);
            rig.Player.PointerDown(3, x, y);
            var drag = rig.Player.Mapper.Session!;
            var g = rig.Player.Layout!.Grid;
            var grab = DragMath.GrabAt(g, x, y, drag.Start.Ix, drag.Start.Iy, drag.Shape.Cells);
            rig.Player.PointerMove(3, x, y - 10);
            rig.Tick(150);
            var path = drag.PathTo(to) ?? drag.PathTo(new DragNode(to.Ix, to.Iy + 1, to.Mode))!;
            foreach (var node in path)
            {
                DragMath.FingerFor(g, grab, node.Ix, node.Iy, rig.Player.Feel.FingerOffsetCells, out double fx, out double fy);
                rig.Player.PointerMove(3, fx, fy);
                rig.Tick(Rig.FrameMs);
            }
            rig.Tick(200);
        }

        /// <summary>Faz 2R level 1 (the archive, D-099): releases a block of the wrong colour over the first golden spot (a K-17 wrong placement).</summary>
        private static int ReleaseWrong(Rig rig)
        {
            var moves = Play.CraneMoves(1, archive: true);
            var m = moves[0];
            var s = rig.Session.State;
            var wrong = moves.First(x => x.PieceId != m.PieceId && State.PieceColor(s, x.PieceId) != State.PieceColor(s, m.PieceId));
            HoldOver(rig, wrong.PieceId, m.To);
            int before = rig.Player.Packages;
            rig.Player.PointerUp(3);
            Assert.AreEqual(before + 1, rig.Player.Packages, "the release committed");
            Assert.IsTrue(rig.Player.LastResult!.Events.OfType<PlacementWrongEvent>().Any(), "K-17: the block was placed wrong");
            return wrong.PieceId;
        }

        private static ParticleField Field(FakeView view, FeelSettings? feel = null, int seed = 7)
        {
            var f = new ParticleField(view, feel ?? new FeelSettings());
            f.Reset(seed);
            return f;
        }

        // --- particles ------------------------------------------------------------------------------------------------------

        [Test]
        public void K12_sparks_fly_up_in_a_cone_shrink_fade_and_go_back_to_the_pool()
        {
            var view = new FakeView();
            var feel = new FeelSettings();
            var field = Field(view, feel);
            var origin = new Vec2(540, 800);
            Assert.AreEqual(14, field.Sparks(origin, Palette.Block(0), feel.ParticlePlaceOk), "particles.placeOk");
            field.Advance(1f);
            Assert.AreEqual(14, field.Shown);
            Assert.AreEqual(14, view.SparksOut.Count);
            field.Advance(feel.SparkLifeMs * 0.5f);
            foreach (var s in view.SparksOut)
            {
                Assert.Less(s.Position.Y, origin.Y - feel.SparkRiseMinPx * 0.4f, "JUICE #12: the sparks rise");
                Assert.Less(Math.Abs(s.Position.X - origin.X), (feel.SparkRiseMaxPx + 50) * Math.Sin(feel.SparkConeDeg * Math.PI / 180) + 40, "a cone, not a fan");
                Assert.Less(s.Scale.X, 1f);
            }
            Assert.IsTrue(view.SparksOut.Any(s => s.Color.Equals(Palette.Block(0))), "block colour");
            Assert.IsTrue(view.SparksOut.Any(s => s.Color.Equals(Rgba.White)), "and white");
            field.Advance(feel.SparkLifeMs * 2);
            Assert.AreEqual(0, field.Alive);
            Assert.AreEqual(0, view.SparksOut.Count, "every sprite is back in the pool");
            Assert.AreEqual(14, view.SparksAcquired);
        }

        [Test]
        public void K12_a_burst_is_capped_at_40_and_the_screen_budget_at_120_oldest_burst_first()
        {
            var view = new FakeView();
            var feel = new FeelSettings();
            var field = Field(view, feel);
            Assert.AreEqual(40, field.Sparks(new Vec2(100, 100), Rgba.White, 100), "particles.maxPerBurst");
            var palette = new[] { Rgba.White, Gold };
            int first = field.Alive;
            for (int i = 0; i < 6; i++)
            {
                field.Confetti(new Vec2(540, 0), 1080, 40, palette, 1500, 800);
                field.Advance(10f);
                Assert.LessOrEqual(field.Alive, feel.ParticleMaxOnScreen, "JUICE §0 rule 4: ≤ 120 alive");
            }
            Assert.LessOrEqual(field.Peak, feel.ParticleMaxOnScreen);
            Assert.Greater(field.Culled, 0, "the oldest bursts ended early");
            Assert.AreEqual(40, first);
            // the first burst (sparks) is the one that went first: no spark is left, the last confetti burst is complete
            Assert.IsFalse(view.SparksOut.Any(s => s.Kind == SparkKind.Spark));
            Assert.LessOrEqual(view.SparksOut.Count, feel.ParticleMaxOnScreen);
        }

        [Test]
        public void K12_reduced_motion_keeps_a_fifth_of_the_particles_and_no_confetti_or_ring()
        {
            var view = new FakeView();
            var feel = new FeelSettings();
            var field = Field(view, feel);
            field.Reduced = true;
            Assert.AreEqual(3, field.Sparks(Vec2.Zero, Rgba.White, 14), "JUICE #12 reduced: ≤ 20 %");
            Assert.AreEqual(1, field.Dust(Vec2.Zero, 4), "at least one");
            Assert.AreEqual(0, field.Confetti(Vec2.Zero, 1000, 40, new[] { Gold }, 1500, 600), "JUICE §0 rule 8: no confetti");
            Assert.AreEqual(3, field.StarBurst(Vec2.Zero, feel.ParticleStarBurst), "JUICE #96 reduced: 3 stars");
            field.Advance(1f);
            Assert.IsFalse(view.SparksOut.Any(s => s.Kind == SparkKind.Ring), "no ring");
            var full = Field(new FakeView(), feel);
            full.StarBurst(Vec2.Zero, feel.ParticleStarBurst);
            Assert.AreEqual(feel.ParticleStarBurst + 1, full.Alive, "16 stars and the ring");
        }

        [Test]
        public void Particles_are_deterministic_for_one_seed_and_differ_for_another()
        {
            string Trace(int seed)
            {
                var view = new FakeView();
                var field = Field(view, null, seed);
                field.Sparks(new Vec2(300, 900), Palette.Block(2), 14);
                field.Dust(new Vec2(300, 900), 10);
                field.Confetti(new Vec2(540, 0), 1000, 24, new[] { Rgba.White, Gold }, 900, 500);
                var o = new System.Text.StringBuilder();
                for (int i = 0; i < 20; i++)
                {
                    field.Advance(33f);
                    foreach (var s in view.SparksOut) o.Append(s.Position).Append(s.Alpha).Append(';');
                }
                return o.ToString();
            }
            Assert.AreEqual(Trace(5), Trace(5));
            Assert.AreNotEqual(Trace(5), Trace(6));
        }

        [Test]
        public void K12_dust_spreads_to_both_sides_along_the_ground_and_fades()
        {
            var view = new FakeView();
            var feel = new FeelSettings();
            var field = Field(view, feel);
            var at = new Vec2(540, 1000);
            field.Dust(at, feel.ParticleLand);
            field.Advance(feel.DustLifeMs * 0.5f);
            Assert.IsTrue(view.SparksOut.Any(s => s.Position.X < at.X - 5));
            Assert.IsTrue(view.SparksOut.Any(s => s.Position.X > at.X + 5));
            foreach (var s in view.SparksOut)
            {
                Assert.LessOrEqual(Math.Abs(s.Position.X - at.X), feel.DustSpreadPx + 1, "JUICE #11: ±60 px");
                Assert.LessOrEqual(s.Position.Y, at.Y, "it rises a little");
                Assert.Less(s.Alpha, feel.DustAlpha, "and fades");
                Assert.Greater(s.Scale.X, 0.5f, "while it grows");
            }
            Assert.IsTrue(view.SparksOut.All(s => s.Kind == SparkKind.Dust));
        }

        // --- the landing wave (JUICE #11, Q2 item 3) ----------------------------------------------------------------------

        [Test]
        public void K11_cell_wave_ranks_start_at_the_bottom_left_cell_and_the_delays_stay_inside_the_beat()
        {
            var f = new FeelSettings();
            foreach (var shape in Shapes.All)
            {
                var ranks = CellWave.Ranks(shape);
                Assert.AreEqual(shape.Cells.Length, ranks.Length);
                Assert.AreEqual(0, ranks.Min(), shape.Id + ": a cell starts the wave");
                for (int i = 0; i < ranks.Length; i++)
                {
                    float d = CellWave.DelayMs(ranks[i], f, f.LandMs);
                    Assert.GreaterOrEqual(d, 0f);
                    Assert.LessOrEqual(d, f.CellStaggerCapMs);
                    Assert.LessOrEqual(d, f.LandMs * 0.6f, shape.Id + ": every cell keeps at least 40 % of the squash");
                }
            }
            Assert.AreEqual(25f, CellWave.DelayMs(1, f, 120), "20–30 ms between neighbours");
            Assert.AreEqual(50f, CellWave.DelayMs(2, f, 120));
            Assert.AreEqual(60f, CellWave.DelayMs(5, f, 120), "capped");
        }

        private static IPieceVisual FindBlock(Rig rig, bool multiCell)
        {
            foreach (var v in rig.View.Live.Values.OrderBy(x => x.PieceId))
                if ((v.CellCount > 1) == multiCell && CellWave.Ranks(v.Shape).Max() >= (multiCell ? 1 : 0)) return v;
            throw new AssertionException("no " + (multiCell ? "multi-cell" : "single-cell") + " block on the board");
        }

        private static float[] LandingFirstSquash(Rig rig, FakePiece v, float stepMs, Action? before = null)
        {
            before?.Invoke();
            var f = rig.Player.Feel;
            var plan = AnimationPlan.FromSteps(new[]
            {
                AnimationStep.Create(StepKind.Landing, 0, stepMs, 11, f.LandEase, fastForward: true, pieceId: v.PieceId, n: 1),
                AnimationStep.Create(StepKind.End, stepMs, 0),
            });
            rig.Player.Queue.Schedule(plan, rig.Player.Clock.Now);
            var first = Enumerable.Repeat(-1f, v.CellCount).ToArray();
            float t = 0;
            for (int i = 0; i <= 140; i++)
            {
                rig.Player.Tick(1f);
                t += 1f;
                for (int c = 0; c < v.CellCount; c++)
                    if (first[c] < 0 && v.CellScales.Length > c && Math.Abs(v.CellScales[c].Y - 1f) > 0.02f) first[c] = t;
            }
            return first;
        }

        [Test]
        public void K11_a_multi_cell_block_lands_cell_by_cell_25_ms_apart_and_is_done_with_the_beat()
        {
            var rig = new Rig();
            rig.Load(1);
            var v = (FakePiece)FindBlock(rig, true);
            var ranks = CellWave.Ranks(v.Shape);
            var first = LandingFirstSquash(rig, v, rig.Player.Feel.LandMs);
            for (int c = 0; c < ranks.Length; c++)
            {
                float want = CellWave.DelayMs(ranks[c], rig.Player.Feel, rig.Player.Feel.LandMs);
                Assert.AreEqual(want + 1f, first[c], 2.5f, "cell " + c + " (rank " + ranks[c] + ") starts " + want + " ms into the landing");
            }
            Assert.IsTrue(ranks.Distinct().Count() > 1);
            Assert.IsTrue(v.CellScales.All(s => s.X == 1f && s.Y == 1f), "every cell is back at rest");
            Assert.AreEqual(1f, v.Scale.X, "the block as a whole is not squashed");
            Assert.AreEqual(1f, v.Scale.Y);
            Assert.AreEqual(rig.Player.Feel.ParticleLand, Count(rig, SparkKind.Dust), "JUICE #11: 10 dust puffs at the foot");
        }

        [Test]
        public void K11_with_a_zero_stagger_or_in_reduced_motion_the_block_squashes_as_one_x_1_12_y_0_86()
        {
            var feel = new FeelSettings { CellStaggerMs = 0 };
            var rig = new Rig(1080, 1920, null, null, feel);
            rig.Load(1);
            var one = (FakePiece)FindBlock(rig, true);
            float minY = 1f;
            var plan = AnimationPlan.FromSteps(new[]
            {
                AnimationStep.Create(StepKind.Landing, 0, 120, 11, feel.LandEase, fastForward: true, pieceId: one.PieceId),
                AnimationStep.Create(StepKind.End, 120, 0),
            });
            rig.Player.Queue.Schedule(plan, rig.Player.Clock.Now);
            for (int i = 0; i < 140; i++)
            {
                rig.Player.Tick(1f);
                minY = Math.Min(minY, one.Scale.Y);
            }
            Assert.AreEqual(feel.LandSquashY, minY, 0.02f, "x 1,12 / y 0,86 (JUICE #11)");
            Assert.AreEqual(0, one.CellScaleCalls, "no per-cell squash");

            var red = new Rig();
            red.Player.Reduced = true;
            red.Load(1);
            var multi = (FakePiece)FindBlock(red, true);
            LandingFirstSquash(red, multi, 120);
            Assert.AreEqual(0, multi.CellScaleCalls, "reduced motion: the block squashes as one (≤ 3 %)");
        }

        // --- correct placement (JUICE #12, Q2 item 4) --------------------------------------------------------------------

        [Test]
        public void K12_a_correct_placement_grows_to_1_2_locks_back_and_bursts_into_14_sparks()
        {
            var rig = new Rig();
            rig.Load(1);
            var m = Play.CraneMoves(1)[0];
            var v = rig.View.Live[m.PieceId];
            rig.DragTo(m);
            float peak = 1f, peakAt = -1, t = 0;
            Run(rig, () =>
            {
                t += Rig.FrameMs;
                if (v.Grow > peak)
                {
                    peak = v.Grow;
                    peakAt = t;
                }
            });
            var f = rig.Player.Feel;
            Assert.AreEqual(f.PlacePulsePeak, peak, 0.02f, "1,0 → 1,2");
            Assert.AreEqual(1f, v.Grow, "and it locks back at 1,0");
            Assert.Greater(peakAt, 0);
            var block = Palette.Block(State.PieceColor(rig.Session.State, m.PieceId));
            Assert.AreEqual((f.ParticlePlaceOk + 1) / 2, rig.View.SparkLog.Count(s => s.Kind == SparkKind.Spark && s.Color.Equals(block)),
                "particles.placeOk sprite sparks: every other one in the block colour, the rest white");
            Assert.AreEqual(f.ParticlePlaceOk + f.ParticlePip, Count(rig, SparkKind.Spark), "14 placement sparks and the 4 of the first streak bead");
        }

        [Test]
        public void K10_a_released_block_lets_go_of_its_1_08_lift_with_a_back_ease_out_dip_under_1()
        {
            var rig = new Rig();
            rig.Load(1);
            var m = Play.CraneMoves(1)[0];
            var v = rig.View.Live[m.PieceId];
            rig.DragTo(m);
            Assert.AreEqual(rig.Player.Feel.LiftScale, v.Scale.X, 0.02f, "still lifted at the release");
            float min = 2f;
            Run(rig, () => min = Math.Min(min, v.Scale.X));
            Assert.Less(min, 0.998f, "Q2 item 1: Back.easeOut overshoots the rest scale (OutQuad would not)");
            Assert.Greater(min, 0.97f);
            Assert.AreEqual(1f, v.Scale.X, 1e-4);
        }

        [Test]
        public void K12_a_released_block_settles_with_back_ease_out_scale_and_reduced_motion_pulses_3_percent()
        {
            var f = new FeelSettings();
            Assert.AreEqual(EaseType.OutBack, f.ReleaseScaleEase, "Q2 item 1: release / landing OutBack");
            var red = new Rig();
            red.Player.Reduced = true;
            red.Load(1);
            var m = Play.CraneMoves(1)[0];
            var v = red.View.Live[m.PieceId];
            red.DragTo(m);
            float peak = 1f;
            Run(red, () => peak = Math.Max(peak, v.Grow));
            Assert.AreEqual(f.ReducedScaleMax, peak, 0.005f, "JUICE §0 rule 8: scale peaks ≤ 1,03");
        }

        // --- bad pass (JUICE #13, Q2 item 2) ------------------------------------------------------------------------------

        [Test]
        public void K17_a_wrong_placement_goes_to_its_spot_shakes_8_px_bounces_back_and_puffs_grey_dust()
        {
            var rig = new Rig(390, 844);
            rig.LoadArchive(1);
            int id = ReleaseWrong(rig);
            var v = rig.View.Live[id];
            var f = rig.Player.Feel;
            Assert.AreEqual(8f, f.WrongShakePx, "Q2 item 2: 8 px");
            // run the fall (if any) and watch the shake: the block's x swings ± 8 px around where it stopped
            float tint = 0, minX = float.MaxValue, maxX = float.MinValue, shakeStart = -1;
            int dust0 = Count(rig, SparkKind.Dust);
            float t = 0;
            Run(rig, () =>
            {
                t += Rig.FrameMs;
                tint = Math.Max(tint, v.Tint);
                if (v.Tint > 0.05f && shakeStart < 0) shakeStart = t;
                if (shakeStart >= 0 && t - shakeStart < f.WrongShakeMs - 20f) // the bounce starts at 120 ms
                {
                    minX = Math.Min(minX, v.Position.X);
                    maxX = Math.Max(maxX, v.Position.X);
                }
            });
            Assert.AreEqual(f.WrongTintAlpha, tint, 0.06f, "red tint 50 %");
            Assert.GreaterOrEqual(maxX - minX, f.WrongShakePx, "the shake swings about ±8 px (frames sample a 25 Hz wave)");
            Assert.LessOrEqual(maxX - minX, 2 * f.WrongShakePx + 0.5f);
            rig.AssertBoardMatchesState("after the bounce");
            Assert.GreaterOrEqual(Count(rig, SparkKind.Dust) - dust0, f.ParticleDustBounce, "JUICE #13: 3 small grey dust puffs");
        }

        [Test]
        public void K17_the_wrong_placement_buzzes_double_light_and_empties_the_streak_in_200_ms()
        {
            var sink = new RecordingHaptics();
            var rig = new Rig(1080, 1920, null, sink);
            rig.LoadArchive(1);
            PlayTo(rig, 1, 0, archive: true);
            rig.Settle();
            Assert.AreEqual(1, rig.View.StreakBeads, "one bead after one correct placement");
            ReleaseWrong(rig);
            float minScale = 1f;
            Run(rig, () => minScale = Math.Min(minScale, rig.View.Beads[0].Scale.X));
            Assert.AreEqual(0, rig.View.StreakBeads, "K-33: a wrong placement sets the streak to 0");
            Assert.LessOrEqual(minScale, rig.Player.Feel.PipFrom + 0.1f, "JUICE #107: the bead goes out (0,6)");
            Assert.Contains(HapticKind.DoubleLight, sink.Kinds, "JUICE #13: double light");
        }

        // --- streak and Golden Trowel (JUICE #15, #16, Q2 item 5) ----------------------------------------------------------

        [Test]
        public void K33_the_beads_fill_one_by_one_and_the_fourth_pops_the_trowel_with_a_gold_rain()
        {
            var sink = new RecordingHaptics();
            var rig = new Rig(1080, 1920, null, sink);
            rig.Load(1);
            var f = rig.Player.Feel;
            var drags = Play.SolverGolden(1).Drags;
            for (int i = 0; i < 3; i++)
            {
                var bead = rig.View.Beads[i];
                rig.DragTo(drags[i]);
                float peak = 0;
                Run(rig, () => peak = Math.Max(peak, bead.Scale.X));
                Assert.AreEqual(i + 1, rig.View.StreakBeads, "bead " + (i + 1) + " is filled");
                Assert.AreEqual(f.PipPeak, peak, 0.05f, "JUICE #15: the bead pops 0,6 → 1,2");
                Assert.AreEqual(1f, bead.Scale.X, 1e-4);
                Assert.AreEqual(0, rig.View.StreakTrowels);
            }
            float iconPeak = 0, spin = 0;
            int sparks0 = Count(rig, SparkKind.Spark);
            rig.DragTo(drags[3]);
            Run(rig, () =>
            {
                iconPeak = Math.Max(iconPeak, rig.View.TrowelIcon.Scale.X);
                spin = Math.Max(spin, rig.View.TrowelIcon.Rotation);
            });
            Assert.AreEqual(0, rig.View.StreakBeads, "the four beads empty into the trowel");
            Assert.AreEqual(1, rig.View.StreakTrowels);
            Assert.AreEqual("×1", rig.View.TrowelText, "common.times");
            Assert.AreEqual(Rig.Tr().Get("hud.streak"), rig.View.StreakLabel);
            Assert.GreaterOrEqual(iconPeak, f.ComboIconPeak - 0.06f, "JUICE #16: 1,0 → 1,4");
            Assert.AreEqual(f.ComboIconRest, rig.View.TrowelIcon.Scale.X, 1e-4, "→ 1,1");
            Assert.Greater(spin, 120f, "the trowel turns");
            Assert.AreEqual(0f, rig.View.TrowelIcon.Rotation, 1e-4, "and ends upright");
            Assert.AreEqual(f.ParticlePlaceOk + f.ParticlePip + f.ParticleCombo, Count(rig, SparkKind.Spark) - sparks0,
                "the 4th placement: 14 sparks, the bead's 4 and particles.combo (30) gold sparkles");
            Assert.IsTrue(rig.View.SparkLog.Count(s => s.Kind == SparkKind.Spark && s.Color.Equals(Gold)) >= f.ParticleCombo * 2 / 3, "gold");
            Assert.Contains(HapticKind.Medium, sink.Kinds, "JUICE #16: medium");
        }

        // --- segment slide, truck, confetti, shake (JUICE #18, #19, Q2 item 6, 12) ----------------------------------------------

        /// <summary>
        /// Plays the golden solution of level 5 up to the first move that completes a segment and returns the rig right after
        /// it was committed (the package with the segment slide, and with `archive` — Faz 2R schemaVersion 1 — the truck). The
        /// shipped level 5 (D-099, fixed yard) slides on move 7; the Faz 2R level 5 on move 5 and delivers batch 1.
        /// </summary>
        private static Rig PlayToSegmentSlide(RecordingHaptics? sink = null, bool reduced = false, bool archive = false)
        {
            var rig = new Rig(1080, 1920, null, sink);
            rig.Player.Reduced = reduced;
            if (archive) rig.LoadArchive(5);
            else rig.Load(5);
            var drags = Play.SolverGolden(5, archive).Drags;
            var probe = GameSession.Start(Play.LevelFile(5, archive));
            int slide = -1;
            for (int i = 0; i < drags.Length && slide < 0; i++)
                if (probe.Apply(drags[i]).Events.Any(e => e is SiteShiftedEvent)) slide = i;
            Assert.GreaterOrEqual(slide, 1, "the golden solution of level 5 completes a segment before its last move");
            for (int i = 0; i < slide; i++) rig.DragTo(drags[i]);
            rig.DragTo(drags[slide], settleFirst: true); // the package with the segment slide (and the truck of a Faz 2R level)
            return rig;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void K22_the_next_segment_slides_in_from_the_right_edge_with_confetti_a_camera_shake_and_a_strong_buzz(bool archive)
        {
            var sink = new RecordingHaptics();
            var rig = PlayToSegmentSlide(sink, archive: archive);
            var f = rig.Player.Feel;
            float slideMax = 0, camMax = 0;
            Run(rig, () =>
            {
                slideMax = Math.Max(slideMax, rig.View.SiteLayer.Position.X);
                camMax = Math.Max(camMax, Math.Max(Math.Abs(rig.View.CameraRig.Position.X), Math.Abs(rig.View.CameraRig.Position.Y)));
            });
            var L = rig.Player.Layout!;
            Assert.Greater(slideMax, L.W - L.Grid.BuildX - 1, "the new site starts off the right edge of the screen");
            Assert.AreEqual(0f, rig.View.SiteLayer.Position.X, "and ends in place");
            Assert.AreEqual(1, rig.Player.Shakes, "one shake (JUICE #18; the truck does not shake)");
            Assert.Greater(camMax, 0.5f);
            Assert.LessOrEqual(camMax, f.SegmentShakePx + 0.01f, "3 px");
            Assert.AreEqual(Vec2.Zero, rig.View.CameraRig.Position, "the camera is back");
            Assert.AreEqual(f.ParticleSegment, Count(rig, SparkKind.Confetti), "particles.segment confetti");
            Assert.Contains(HapticKind.Heavy, sink.Kinds, "JUICE #18: strong");
            rig.AssertBoardMatchesState("after the slide");
        }

        [Test]
        public void K25_K55_D099_an_archive_batch_drops_into_the_yard_in_dust_without_a_truck()
        {
            // D-099: the truck view is gone; a `schemaVersion` 1 level (Faz 2R archive) still delivers its batch: the blocks fall
            // in from above the yard columns, each lands in dust, the camera only shakes for the segment
            var rig = PlayToSegmentSlide(archive: true); // Faz 2R level 5 (schemaVersion 1): batch 1 arrives by truck
            var f = rig.Player.Feel;
            int dust0 = Count(rig, SparkKind.Dust);
            Run(rig);
            int drops = rig.Player.LastResult!.Events.OfType<PieceFellEvent>().Count(e => e.Cause == FallCauses.Delivery);
            Assert.Greater(drops, 0);
            Assert.GreaterOrEqual(Count(rig, SparkKind.Dust) - dust0, drops * f.ParticleDustBlock, "4 dust per block");
            Assert.AreEqual(1, rig.Player.Shakes, "the segment shake only");
            CollectionAssert.DoesNotContain(rig.Player.Fx.Log, "truck");
            rig.AssertBoardMatchesState("after the delivery");
        }

        [Test]
        public void K25_reduced_motion_delivery_has_no_ride_no_shake_no_zoom()
        {
            var rig = PlayToSegmentSlide(null, true, archive: true);
            Run(rig);
            Assert.AreEqual(0, rig.Player.Shakes, "JUICE §0 rule 8: no screen shake");
            Assert.AreEqual(0, rig.Player.Zooms, "no camera push-in");
            Assert.AreEqual(0, Count(rig, SparkKind.Confetti), "no confetti");
            Assert.AreEqual(0f, rig.View.SiteLayer.Position.X, "no slide");
            rig.AssertBoardMatchesState("reduced");
        }

        [Test]
        public void K55_E62_a_fixed_yard_segment_change_slides_the_site_with_no_truck_no_delivery_and_no_truck_chip()
        {
            var rig = PlayToSegmentSlide(); // the shipped level 5 (schemaVersion 2): segment 1 done on move 7, the yard stays as it is
            Run(rig);
            var events = rig.Player.LastResult!.Events;
            Assert.IsTrue(events.Any(e => e is SiteShiftedEvent), "the site still slides in (K-22)");
            Assert.IsFalse(events.Any(e => e is DeliveryArrivedEvent || e is DeliveryQueuedEvent), "step 9 delivers nothing");
            Assert.IsFalse(events.Any(e => e is PieceFellEvent f && f.Cause == FallCauses.Delivery), "no delivery fall");
            Assert.IsFalse(rig.View.HudState!.Truck, "the Kamyonda: N chip stays hidden at N = 0 (E-62)");
            Assert.AreEqual(0, rig.View.HudState!.Queue);
            rig.AssertBoardMatchesState("after the fixed-yard slide");
        }

        [Test]
        public void K55_a_Faz_2R_level_still_shows_its_truck_chip_while_a_block_waits_in_the_queue()
        {
            var rig = PlayToSegmentSlide(archive: true); // Faz 2R level 5 (schemaVersion 1): batch 1 arrives by truck
            Run(rig);
            Assert.AreEqual(rig.View.HudState!.Queue > 0, rig.View.HudState!.Truck, "the chip follows the queue (N > 0)");
        }

        // --- Söküm dust (JUICE #107) -------------------------------------------------------------------------------------

        [Test]
        public void K30_torn_down_blocks_puff_dust_where_they_leave_and_where_they_land()
        {
            var rig = new Rig();
            rig.Load(1);
            var ids = rig.View.Live.Keys.OrderBy(i => i).Take(2).ToArray();
            var moves = ids.Select(i => new TeardownMove(i, TeardownPlace.OnBoard(At.Site(6, 0, 0)), TeardownPlace.OnBoard(At.Yard(0, 0)))).ToArray();
            var ev = Play.Ev(new TeardownEvent(1, moves, TeardownCauses.Tiling), 12, 1);
            var f = rig.Player.Feel;
            float ms = f.TeardownSettleMs + f.TeardownMs + (ids.Length - 1) * f.TeardownStaggerMs;
            var plan = AnimationPlan.FromSteps(new[]
            {
                AnimationStep.Create(StepKind.Teardown, 0, ms, 107, EaseType.InOutQuad, lockInput: true, ev: ev, n: ids.Length),
                AnimationStep.Create(StepKind.End, ms, 0),
            });
            rig.Player.Queue.Schedule(plan, rig.Player.Clock.Now);
            Run(rig);
            Assert.AreEqual(ids.Length * 2 * f.ParticleDustBlock, Count(rig, SparkKind.Dust), "4 dust at take-off and 4 at touch-down per block");
        }

        // --- blocks-left chip flight (D-091, Q2 item 8) ------------------------------------------------------------------

        [Test]
        public void K48_a_correct_placement_flies_a_block_icon_on_an_arc_to_the_blocks_chip_and_the_chip_pops()
        {
            var rig = new Rig();
            rig.Load(1);
            var m = Play.CraneMoves(1)[0];
            rig.DragTo(m);
            var chip = rig.Player.Layout!;
            var target = HudLayout.Center(HudLayout.BlocksChip(chip));
            float chipPeak = 1f, arcAbove = 0;
            Vec2? start = null;
            Run(rig, () =>
            {
                chipPeak = Math.Max(chipPeak, rig.View.BlocksChip.Scale.X);
                var fl = rig.View.LastFlyer;
                if (fl != null && fl.Visible)
                {
                    start ??= fl.Position;
                    // above the straight line start → chip: the arc
                    arcAbove = Math.Max(arcAbove, StraightLineY(start.Value, target, fl.Position.X) - fl.Position.Y);
                }
            });
            Assert.IsNotNull(start, "an icon flew");
            Assert.Greater(arcAbove, rig.Player.Feel.BlocksChipFlyArcCells * (float)chip.Grid.CellPx * 0.5f, "a curved flight (1,5 cells at the middle)");
            Assert.AreEqual(rig.Player.Feel.BlocksChipPeak, chipPeak, 0.03f, "the counter pops 1,0 → 1,15");
            Assert.AreEqual(rig.Player.Summary!.BlocksLeft, rig.View.Blocks);
        }

        private static float StraightLineY(Vec2 a, Vec2 b, float x) => Math.Abs(b.X - a.X) < 1e-3f ? a.Y : a.Y + (b.Y - a.Y) * (x - a.X) / (b.X - a.X);

        // --- win (JUICE #55, #56, #96, Q2 item 9, 12) -------------------------------------------------------------------

        [Test]
        public void K28_the_win_turns_the_left_over_moves_into_gold_coins_with_confetti_a_shake_and_a_star()
        {
            var sink = new RecordingHaptics();
            var rig = new Rig(1080, 1920, null, sink);
            rig.Load(1);
            var f = rig.Player.Feel;
            var drags = Play.SolverGolden(1).Drags;
            for (int i = 0; i < drags.Length - 1; i++) rig.DragTo(drags[i]);
            rig.Settle();
            int movesLeftBefore = rig.Session.MovesLeft;
            var goldSeen = new List<int>();
            int coinsWhenOpened = -1;
            float starPeak = 0, chipPeak = 1f, maxShake = 0, zoomPeak = 1f;
            int zoomsBeforeWin = rig.Player.Zooms;
            rig.DragTo(drags[drags.Length - 1]);
            Run(rig, () =>
            {
                zoomPeak = Math.Max(zoomPeak, rig.View.CameraRig.Scale.X);
                if (rig.View.Gold >= 0 && (goldSeen.Count == 0 || goldSeen[goldSeen.Count - 1] != rig.View.Gold)) goldSeen.Add(rig.View.Gold);
                if (rig.View.Panel != null && coinsWhenOpened < 0) coinsWhenOpened = rig.Player.CoinsInFlight;
                starPeak = Math.Max(starPeak, rig.View.StarTarget.Scale.X);
                chipPeak = Math.Max(chipPeak, rig.View.GoldChip.Scale.X);
                maxShake = Math.Max(maxShake, Math.Abs(rig.View.CameraRig.Position.X));
            });
            Assert.AreEqual(BoosterOutcomes.Won, rig.Session.Outcome);
            int lastMoves = rig.Session.MovesLeft;
            Assert.Greater(lastMoves, 0);
            int counted = Math.Min(lastMoves, rig.Player.Economy.BonusMaxMovesCounted);
            Assert.AreEqual(counted, Count(rig, SparkKind.Coin), "JUICE #56: one coin per left-over move (at most 10)");
            Assert.AreEqual(counted * 3, rig.View.Gold, "economy.json bonusCoinsPerMoveLeft 3 each");
            Assert.AreEqual(0, goldSeen[0], "the chip appears at 0");
            for (int i = 1; i < goldSeen.Count; i++) Assert.AreEqual(3, goldSeen[i] - goldSeen[i - 1], "it counts up coin by coin");
            Assert.AreEqual(0, coinsWhenOpened, "the window opens after the last coin landed");
            Assert.AreEqual(f.GoldChipPeak, chipPeak, 0.03f, "the chip pops with each coin");
            Assert.AreEqual(0, rig.View.Moves, "the plate counted down to 0");
            // D-099 level 1 has two floors: the first segment's slide (confetti + 3 px shake), then the last segment's and the win's
            int segments = rig.Session.Lvl.Segments.Length;
            Assert.GreaterOrEqual(Count(rig, SparkKind.Confetti), f.ParticleSegment * segments + f.ParticleWin, "JUICE #55 confetti");
            Assert.LessOrEqual(Count(rig, SparkKind.Confetti), f.ParticleSegment * segments + f.ParticleWin * f.ParticleWinWaves);
            Assert.AreEqual(segments + 1, rig.Player.Shakes, "every segment's 3 px (the last one included) and the win's");
            Assert.IsTrue(sink.Kinds.Contains(HapticKind.Win) && sink.Kinds.Contains(HapticKind.Success), "victory pattern and the star's success buzz");
            Assert.IsTrue(rig.View.Panel!.Star, "META §1: a first win gives its star");
            Assert.AreEqual(f.WinStarPeak, starPeak, 0.03f, "JUICE #96: the star 0 → 1,2");
            Assert.AreEqual(1f, rig.View.StarTarget.Scale.X, 1e-4);
            // a level player without the king's window (no HUD): the sprite stars (with the HUD the fx_star_burst clip plays: KingTests)
            Assert.AreEqual(f.ParticleStarBurst, Count(rig, SparkKind.Star), "16 small stars");
            Assert.AreEqual(1, Count(rig, SparkKind.Ring), "and the white ring");
            Assert.AreEqual(2, rig.Player.Zooms - zoomsBeforeWin, "B2: the last segment's push-in and the win's (JUICE #18, #55)");
            Assert.AreEqual(f.WinZoomPeak, zoomPeak, 0.005f, "the win push-in peak");
            Assert.AreEqual(Vec2.One, rig.View.CameraRig.Scale, "and the camera is back");
            Assert.AreEqual(0f, rig.View.SiteLayer.Position.X, "the last segment does not slide");
            Assert.LessOrEqual(movesLeftBefore - lastMoves, 1);
            Assert.AreEqual(0, rig.View.SparksOut.Count, "every particle sprite went back to the pool");
            Assert.AreEqual(0, rig.View.FlyersOut.Count);
        }

        [Test]
        public void K28_a_repeat_win_of_the_same_level_gives_no_star_and_the_gold_chip_starts_hidden_again()
        {
            var rig = new Rig();
            rig.Load(2);
            foreach (var d in Play.SolverGolden(2).Drags) rig.DragTo(d);
            rig.Settle();
            Run(rig);
            Assert.IsTrue(rig.View.Panel!.Star);
            Assert.GreaterOrEqual(rig.View.Gold, 0);
            rig.Player.Restart();
            Assert.AreEqual(-1, rig.View.Gold, "a new attempt hides the gold chip");
            Assert.AreEqual(0, rig.Player.Particles.Alive);
            Assert.AreEqual(0, rig.Player.CoinsInFlight);
            foreach (var d in Play.SolverGolden(2).Drags) rig.DragTo(d);
            rig.Settle();
            Run(rig);
            Assert.AreEqual(PanelKind.Win, rig.Player.Panel);
            Assert.IsFalse(rig.View.Panel!.Star, "META §1: a repeat win gives no star");
        }

        [Test]
        public void K28_reduced_motion_win_counts_the_gold_at_once_without_coins_confetti_or_a_shake()
        {
            var rig = new Rig();
            rig.Player.Reduced = true;
            rig.Load(1);
            foreach (var d in Play.SolverGolden(1).Drags) rig.DragTo(d);
            rig.Settle();
            Run(rig);
            int left = rig.Session.MovesLeft;
            Assert.AreEqual(Math.Min(left, 10) * 3, rig.View.Gold, "JUICE #56 reduced: the total is counted");
            Assert.AreEqual(0, Count(rig, SparkKind.Coin));
            Assert.AreEqual(0, Count(rig, SparkKind.Confetti));
            Assert.AreEqual(0, rig.Player.Shakes);
            Assert.AreEqual(0, Count(rig, SparkKind.Ring), "no star ring");
            Assert.AreEqual(3, Count(rig, SparkKind.Star), "JUICE #96 reduced: 3 stars");
            Assert.AreEqual(PanelKind.Win, rig.Player.Panel);
        }

        // --- stuck pulse (K-34 hook 5, Q2 item 10) ---------------------------------------------------------------------

        [Test]
        public void K34_hook5_the_stuck_pulse_makes_the_needed_blocks_glow_softly_and_the_glow_goes_out()
        {
            var rig = new Rig();
            rig.Load(1);
            var needed = rig.Session.Summary().NeededNow;
            var v = rig.View.Live[needed[0]];
            rig.Tick(StuckPulse.EasyIdleMs - 200);
            Assert.AreEqual(0f, v.Flash, 1e-6, "no glow before the idle time");
            float peak = 0;
            for (int i = 0; i < 400; i++)
            {
                rig.Player.Tick(Rig.FrameMs);
                peak = Math.Max(peak, v.Flash);
            }
            Assert.AreEqual(rig.Player.Feel.HintGlowPeak, peak, 0.05f, "a soft glow, nothing like the placement flash");
            Assert.Less(peak, rig.Player.Feel.PlaceFlashPeak);
            rig.Settle();
            Assert.AreEqual(0f, v.Flash, 1e-6);
        }

        // --- camera, haptics, buttons (Q2 items 11, 12) -----------------------------------------------------------------

        [Test]
        public void K18_a_camera_shake_lasts_120_ms_decays_and_always_ends_at_zero_even_when_shakes_overlap()
        {
            var rig = new Rig();
            rig.Load(1);
            var f = rig.Player.Feel;
            rig.Player.CameraShake(3f);
            float max = 0, lastNonZero = 0, t = 0;
            for (int i = 0; i < 20; i++)
            {
                rig.Player.Tick(Rig.FrameMs / 2);
                t += Rig.FrameMs / 2;
                var o = rig.View.CameraRig.Position;
                max = Math.Max(max, Math.Max(Math.Abs(o.X), Math.Abs(o.Y)));
                if (!o.Equals(Vec2.Zero)) lastNonZero = t;
                if (i == 3) rig.Player.CameraShake(3f); // overlaps the first
            }
            Assert.Greater(max, 1f);
            Assert.LessOrEqual(max, 3f + 1e-3f);
            Assert.LessOrEqual(lastNonZero, f.CameraShakeMs * 1.6f + 20, "120 ms (the second shake ran from 33 ms)");
            Assert.AreEqual(Vec2.Zero, rig.View.CameraRig.Position);
            Assert.AreEqual(2, rig.Player.Shakes);
            var red = new Rig();
            red.Player.Reduced = true;
            red.Load(1);
            red.Player.CameraShake(3f);
            Assert.AreEqual(0, red.Player.Shakes, "JUICE §0 rule 8: no screen shake");
            rig.Player.CameraShake(0f);
            Assert.AreEqual(2, rig.Player.Shakes, "a zero amplitude is no shake");
        }

        [Test]
        public void Haptics_router_drops_buzzes_closer_than_the_gap_and_when_the_switch_is_off()
        {
            var sink = new RecordingHaptics();
            var router = new HapticRouter(sink);
            Assert.IsTrue(router.Pulse(HapticKind.Heavy, 1000, 120));
            Assert.IsFalse(router.Pulse(HapticKind.Heavy, 1100, 120), "one buzz per beat");
            Assert.IsTrue(router.Pulse(HapticKind.Win, 1120, 120));
            router.Enabled = false;
            Assert.IsFalse(router.Pulse(HapticKind.Win, 5000, 120), "JUICE §0 rule 7: the switch");
            router.Enabled = true;
            Assert.IsTrue(router.Pulse(HapticKind.Medium, 5000, 120));
            Assert.AreEqual(3, sink.Kinds.Count);
            Assert.AreEqual(2, router.Dropped);
            Assert.IsFalse(new HapticRouter(null).Pulse(HapticKind.Win, 0, 0), "no sink, no buzz");
        }

        [Test]
        public void Haptics_fixed_pulse_device_buzzes_medium_and_stronger_and_only_when_supported()
        {
            int buzzes = 0;
            var android = new FixedPulseHaptics(true, () => buzzes++);
            foreach (HapticKind k in Enum.GetValues(typeof(HapticKind))) android.Vibrate(k);
            Assert.AreEqual(4, buzzes, "Medium, Heavy, Success and Win; Light and DoubleLight are too weak for a 400 ms buzz");
            buzzes = 0;
            var other = new FixedPulseHaptics(false, () => buzzes++);
            foreach (HapticKind k in Enum.GetValues(typeof(HapticKind))) other.Vibrate(k);
            Assert.AreEqual(0, buzzes, "Handheld.Vibrate is asked on Android only");
        }

        [Test]
        public void Haptics_the_win_pattern_and_the_segment_buzz_reach_the_device_but_light_placement_taps_do_not()
        {
            int buzzes = 0;
            var sink = new FixedPulseHaptics(true, () => buzzes++);
            var rig = new Rig(1080, 1920, null, sink);
            rig.Load(1);
            rig.DragTo(Play.CraneMoves(1)[0]);
            Run(rig);
            Assert.AreEqual(0, buzzes, "a correct placement taps light: nothing on a one-pulse device");
            foreach (var d in Play.SolverGolden(1).Drags.Skip(1)) rig.DragTo(d);
            Run(rig);
            Assert.GreaterOrEqual(buzzes, 2, "the trowel (medium), the segment (strong) and the win");
        }

        [Test]
        public void UX69_a_button_sinks_to_0_94_pops_on_release_and_settles_with_back_ease_out()
        {
            var f = new FeelSettings();
            var target = new FakeTarget();
            var press = new PressFeel(target, f);
            press.Press();
            Assert.IsTrue(press.Held);
            float t = 0;
            while (press.Busy)
            {
                press.Tick(5f);
                t += 5f;
            }
            Assert.AreEqual(f.ButtonPressScale, target.Scale.X, 1e-4, "0,94");
            Assert.LessOrEqual(t, f.ButtonPressMs + 5f, "in 60 ms");
            press.Release();
            Assert.IsFalse(press.Held);
            Assert.AreEqual(f.ButtonReleasePeak, target.Scale.X, 1e-4, "released: 1,04 first");
            float below = 2f;
            t = 0;
            while (press.Busy)
            {
                press.Tick(5f);
                t += 5f;
                below = Math.Min(below, target.Scale.X);
            }
            Assert.AreEqual(1f, target.Scale.X, 1e-6, "settles at 1,0");
            Assert.LessOrEqual(t, f.ButtonReleaseMs + 5f, "in 120 ms");
            Assert.Less(below, 1f, "Back.easeOut dips under 1 before it settles");
            Assert.Greater(below, 0.95f);
        }

        [Test]
        public void UX69_a_pointer_that_slides_off_cancels_the_press_without_the_pop()
        {
            var f = new FeelSettings();
            var target = new FakeTarget();
            var press = new PressFeel(target, f);
            press.Press();
            press.Tick(100f);
            float max = 0;
            press.Cancel();
            for (int i = 0; i < 40; i++)
            {
                press.Tick(5f);
                max = Math.Max(max, target.Scale.X);
            }
            Assert.IsFalse(press.Held);
            Assert.AreEqual(1f, target.Scale.X, 1e-6);
            Assert.Less(max, f.ButtonReleasePeak - 0.02f, "no 1,04 pop on a cancel");
            press.Release();
            Assert.AreEqual(1f, target.Scale.X, 1e-6, "a release after the cancel does nothing");
            press.Press();
            press.Reset();
            Assert.AreEqual(1f, target.Scale.X);
            Assert.IsFalse(press.Held);
        }

        // --- housekeeping -----------------------------------------------------------------------------------------------

        [Test]
        public void K35_after_every_plan_the_blocks_are_at_rest_scale_and_cells_without_a_leftover_grow()
        {
            var rig = new Rig(390, 844);
            rig.Load(5);
            foreach (var m in Play.CraneMoves(5))
            {
                rig.DragTo(m);
                rig.Settle();
                foreach (var v in rig.View.Live.Values)
                {
                    Assert.AreEqual(1f, v.Grow, 1e-5, "piece " + v.PieceId + " grow");
                    Assert.IsTrue(v.CellScales.All(c => c.X == 1f && c.Y == 1f), "piece " + v.PieceId + " cells");
                }
            }
            rig.AssertBoardMatchesState("end");
            Assert.AreEqual(Vec2.Zero, rig.View.CameraRig.Position);
            Assert.AreEqual(0f, rig.View.SiteLayer.Position.X);
            Assert.AreEqual(Vec2.One, rig.View.CameraRig.Scale, "B2: the zoom is back at rest");
        }

        [Test]
        public void Determinism_the_same_moves_give_the_same_particles_and_the_same_frames()
        {
            string Trace()
            {
                var rig = new Rig(390, 844);
                rig.Load(5);
                var o = new System.Text.StringBuilder();
                foreach (var m in Play.CraneMoves(5).Take(6))
                {
                    rig.DragTo(m, settleFirst: false);
                    for (int i = 0; i < 40; i++)
                    {
                        rig.Tick(Rig.FrameMs);
                        foreach (var s in rig.View.SparksOut) o.Append(s.Kind).Append(s.Position).Append(s.Alpha).Append(';');
                        o.Append(rig.View.CameraRig.Position).Append(rig.View.CameraRig.Scale).Append(rig.View.SiteLayer.Position);
                        foreach (var fx in rig.View.FxOut) o.Append(fx.Frame).Append(fx.Position).Append(';');
                    }
                }
                return o.ToString();
            }
            Assert.AreEqual(Trace(), Trace());
        }

        [Test]
        public void Economy_reads_the_bonus_values_of_economy_json_and_falls_back_to_the_same_numbers()
        {
            var e = EconomyInfo.FromText(PresentationPaths.ResourceText("config/economy"));
            Assert.AreEqual(3, e.BonusCoinsPerMoveLeft);
            Assert.AreEqual(10, e.BonusMaxMovesCounted);
            var d = EconomyInfo.FromText(null);
            Assert.AreEqual(3, d.BonusCoinsPerMoveLeft);
            Assert.AreEqual(10, d.BonusMaxMovesCounted);
            var custom = EconomyInfo.FromText("{\"levelRewards\":{\"bonusCoinsPerMoveLeft\":5,\"bonusMaxMovesCounted\":4}}");
            Assert.AreEqual((5, 4), (custom.BonusCoinsPerMoveLeft, custom.BonusMaxMovesCounted));
        }

        [Test]
        public void UX58_the_streak_capsule_the_gold_chip_and_the_window_star_fit_their_places_on_every_screen([ValueSource(typeof(Play), nameof(Play.Levels))] int level)
        {
            foreach (var (w, h) in new[] { (390, 844), (360, 800) })
            {
                var rig = new Rig(w, h);
                rig.Load(level);
                var L = rig.Player.Layout!;
                // V1 (v4 mock, ART_UNITY §4.4): the streak capsule is in HUD row 1, right of the blocks-left chip
                var s = HudLayout.Streak(L);
                Assert.GreaterOrEqual(s.X, HudLayout.BlocksChip(L).Right, "B" + level + ": right of the blocks chip");
                Assert.LessOrEqual(s.Right, L.Goals.Right + 0.5, "B" + level + ": inside the goals row");
                Assert.GreaterOrEqual(s.Y, L.Goals.Y);
                Assert.LessOrEqual(s.Bottom, L.TopGroupBottomY);
                var parts = new List<PxRect> { HudLayout.StreakLabel(L), HudLayout.TrowelIcon(L), HudLayout.TrowelCount(L) };
                for (int i = 0; i < 4; i++) parts.Add(HudLayout.Bead(L, i));
                for (int i = 0; i < parts.Count; i++)
                {
                    Assert.GreaterOrEqual(parts[i].X, s.X, "part " + i);
                    Assert.LessOrEqual(parts[i].Right, s.Right + 0.5, "part " + i);
                    for (int j = i + 1; j < parts.Count; j++) Assert.IsFalse(parts[i].Overlaps(parts[j]), "streak parts " + i + " and " + j + " overlap");
                }
                var chip = HudLayout.GoldChip(L);
                Assert.IsFalse(chip.Overlaps(L.Moves) || chip.Overlaps(L.Pause), "the gold chip sits between the moves plate and the pause tool");
                Assert.GreaterOrEqual(chip.W, 150);
                var truck = HudLayout.TruckChip(L);
                Assert.GreaterOrEqual(truck.X, 0);
                Assert.LessOrEqual(truck.Right, L.W);
                Assert.IsFalse(truck.Overlaps(L.BoosterBar), "the truck chip is above the booster bar");
                // the win window's star sits in the top part of the window (V1 WindowViews.WinLayout; JUICE #96)
                var win = WindowViews.WinLayout(L.W, L.H, rig.Player.LayoutSettings);
                Assert.GreaterOrEqual(win.Star.Y, win.Panel.Y, "the star is inside the window");
                Assert.Less(win.Star.Bottom, win.Card.Y, "the star is above the structure card");
                Assert.GreaterOrEqual(win.Panel.Y, 0);
                Assert.LessOrEqual(win.Panel.Bottom, L.H);
            }
        }

        [Test]
        public void Atlas_the_q2_tiles_are_drawn_a_ring_is_hollow_a_puff_is_soft_a_coin_has_a_rim()
        {
            var a = Atlas.Build();
            int mid = Atlas.Content / 2;
            foreach (var t in new[] { Tile.Spark, Tile.Soft, Tile.Star, Tile.Coin, Tile.Trowel, Tile.Ring })
            {
                int ink = 0;
                for (int y = 0; y < Atlas.Content; y++)
                    for (int x = 0; x < Atlas.Content; x++)
                        if (a.At(t, x, y).A > 128) ink++;
                Assert.Greater(ink, 400, t + " has content");
                Assert.Less(ink, Atlas.Content * Atlas.Content, t + " is not a filled square");
            }
            Assert.AreEqual(0, a.At(Tile.Ring, mid, mid).A, "the ring is hollow");
            Assert.Greater(a.At(Tile.Ring, mid, 8).A, 200, "its band");
            Assert.Greater(a.At(Tile.Soft, mid, mid).A, 240, "the puff is dense in the middle");
            Assert.AreEqual(0, a.At(Tile.Soft, 2, 2).A, "and gone at the corner");
            Assert.Less(a.At(Tile.Coin, mid, 8).R, a.At(Tile.Coin, mid, mid).R, "the rim is darker than the face");
            Assert.AreEqual(0, a.At(Tile.Spark, 3, 3).A, "a sparkle's corners are empty");
            Assert.Greater(a.At(Tile.Spark, mid, mid).A, 200);
        }

        [Test]
        public void Views_every_ilevelview_state_resets_on_a_new_attempt()
        {
            var rig = new Rig();
            rig.Load(5);
            var drags = Play.SolverGolden(5).Drags;
            for (int i = 0; i < 3; i++) rig.DragTo(drags[i]);
            rig.Player.CameraShake(3f);
            rig.Player.Tick(30);
            rig.Player.Restart();
            Assert.AreEqual(Vec2.Zero, rig.View.CameraRig.Position);
            Assert.AreEqual(0, rig.Player.Particles.Alive);
            Assert.AreEqual(0, rig.View.SparksOut.Count, "the pool got every sprite back");
            Assert.AreEqual(0, rig.Player.ShownBeads);
            Assert.AreEqual(0, rig.View.StreakBeads);
            Assert.AreEqual(0, rig.Player.Fx.Alive, "B2: every FX clip ended");
            Assert.AreEqual(0, rig.View.FxOut.Count, "and its sprite is back in the pool");
            Assert.AreEqual(-1, rig.View.Gold);
        }
    }
}
