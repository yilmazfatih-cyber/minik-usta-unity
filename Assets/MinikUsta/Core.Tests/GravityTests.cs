#nullable enable
// Port of tests/core/gravity.test.ts (K-11, K-18, K-19, K-20; W8 / S8 hooks as test-side rules).
using System;
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;
using static MinikUsta.Core.Tests.Fixtures.Boards;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class GravityTests
    {
        /// <summary>S3-like hook (the real one is the obstacle plugin): glass breaks when d &gt; the level's threshold.</summary>
        private static readonly FallRules GlassRules = new FallRules(onLanded: (s, id, fall) =>
            HasFlag(s, id, FlagBit.Glass) && fall.Distance > s.Lvl.Gravity.GlassThreshold ? LandingEffect.Break(1) : LandingEffect.None);

        /// <summary>S8-like hook: balloon blocks rise.</summary>
        private static readonly FallRules BalloonRules = new FallRules(modifyFall: (s, id, plan) => HasFlag(s, id, FlagBit.Balloon) ? plan.WithDir(1) : plan);

        /// <summary>W8-like hook, wind to the right: width-1 blocks with d ≥ 1 drift (E-17).</summary>
        private static readonly FallRules WindRight = new FallRules(modifyFall: (s, id, plan) =>
        {
            int w = Shapes.ShapeByIndex(PieceShape(s, id)).W;
            int d = Math.Abs(plan.Iy - Gravity.SiteLandingRow(s, id, plan.Ix, plan.Iy, plan.Dir));
            return w == 1 && d >= 1 ? plan.WithDrift(1) : plan;
        });

        private static FallOptions Opts(FallRules? rules = null, Steer? steer = null) => new FallOptions(steer, rules);

        // --- K-11 ------------------------------------------------------------------------------------------------------------

        [Test(Description = "K-11 GDD example: D2_90 dropped from the crane area lands at (6,0) with d = 8; lowered to (6,1), d = 1")]
        public void K11_GDD_example_D2_90_dropped_from_the_crane_area_lands_at_6_0_with_d_8_lowered_to_6_1_d_1()
        {
            var s = Init(new LevelSpec { Plan = WW(3), Pieces = Ps(Pc("D2_90", "W", 0, 0)) });
            var high = Gravity.ComputeFall(s, 0, N(6, 8));
            AssertFall(high, FallResult.ModeFree, A(6, 0), -1, 8, 0);
            CollectionAssert.AreEqual(new[] { A(6, 8), A(6, 0) }, high.Path);
            CollectionAssert.AreEqual(new[] { C(6, 0), C(7, 0) }, high.Cells);
            Assert.AreEqual(1, Gravity.ComputeFall(s, 0, N(6, 1)).Distance);
            Assert.AreEqual(0, Gravity.ComputeFall(s, 0, N(6, 0)).Distance);
        }

        [Test(Description = "K-11 the landing uses each covered column (C3_180 over an uneven silhouette)")]
        public void K11_the_landing_uses_each_covered_column()
        {
            var s = Init(new LevelSpec { Plan = WW(4), Pieces = Ps(Pc("D2_0", "W", 0, 0), Pc("C3_180", "W", 2, 0)) });
            ToSite(s, 0, 6, 0); // top(6) = 2, top(7) = 0
            // C3_180 = (1,0) (0,1) (1,1): column 6 bottom offset 1, column 7 bottom offset 0 → max(2 − 1, 0 − 0) = 1
            var fall = Gravity.ComputeFall(s, 1, N(6, 8));
            Assert.AreEqual(A(6, 1), fall.Landing);
            CollectionAssert.AreEqual(new[] { C(7, 1), C(6, 2), C(7, 2) }, fall.Cells);
        }

        [Test(Description = "K-11 the elevator platform is support: a fall stops on the frame floor (K-24)")]
        public void K11_the_elevator_platform_is_support_a_fall_stops_on_the_frame_floor()
        {
            var s = Init(new LevelSpec { Id = 37, Plan = WW(2), Elevator = Elevator(0, 2, 2, 1), Pieces = Ps(Pc("B1_0", "W", 0, 0)) });
            CollectionAssert.AreEqual(new[] { 0b11, 0b11 }, Gravity.SiteColumnMasks(s));
            var fall = Gravity.ComputeFall(s, 0, N(6, 8));
            Assert.AreEqual(A(6, 2), fall.Landing);
            Assert.IsTrue(fall.Verdict.Ok);
        }

        [Test(Description = "K-11 every reachable FREE site release lands where a step-by-step fall stops (seeded boards)")]
        public void K11_every_reachable_FREE_site_release_lands_where_a_step_by_step_fall_stops()
        {
            var shapes = new[] { "B1_0", "D2_0", "D2_90", "O4_0", "C3_0", "C3_90", "C3_180", "C3_270", "L4_0" };
            var rng = new Mulberry32(611);
            int checkedCount = 0;
            for (int round = 0; round < 100; round++)
            {
                string shape = shapes[rng.NextInt(shapes.Length)];
                var blockers = new List<P>();
                for (int i = 0; i < 6; i++) blockers.Add(Pc("B1_0", "W", i, 0));
                var s = Init(new LevelSpec { Plan = WW(8), Pieces = Ps(Pc(shape, "W", 0, 4)).Plus(blockers.ToArray()) });
                // random site cells (not necessarily supported: the fall physics must not care)
                var used = new HashSet<string>();
                for (int i = 1; i <= 6; i++)
                {
                    int x = 6 + rng.NextInt(2);
                    int y = rng.NextInt(8);
                    if (used.Contains(x + "," + y))
                    {
                        Remove(s, i);
                        continue;
                    }
                    used.Add(x + "," + y);
                    ToSite(s, i, x, y, locked: rng.NextInt(2) == 0, stuck: false);
                }
                var attempt = Movement.TryBeginDrag(s, 0);
                if (!attempt.Ok) continue;
                var before = Snapshot(s);
                var def = Shapes.ShapeById(shape);
                foreach (var node in attempt.Session!.ReachableNodes())
                {
                    if (node.Mode != Movement.Free || node.Ix < 6) continue;
                    var fall = Gravity.ComputeFall(s, 0, node);
                    int y = node.Iy;
                    bool Free(BoardCell c) => c.Y >= 8 || SiteOcc(s, 0, c.X - 6, c.Y) == 0;
                    while (y > 0 && Movement.BlockCells(def, node.Ix, y - 1).All(Free)) y--;
                    Assert.AreEqual(A(node.Ix, y), fall.Landing);
                    Assert.AreEqual(node.Iy - y, fall.Distance);
                    Assert.AreEqual(Placement.IsCorrectPlacement(s, 0, Movement.BlockCells(def, node.Ix, y)), fall.Verdict);
                    checkedCount++;
                }
                CollectionAssert.AreEqual(before, s.Buf); // computeFall is pure
            }
            Assert.Greater(checkedCount, 200);
        }

        // --- K-18 ------------------------------------------------------------------------------------------------------------

        [Test(Description = "K-18 on the rail the shadow is the block itself: no fall, no hooks, no glass break")]
        public void K18_on_the_rail_the_shadow_is_the_block_itself_no_fall_no_hooks_no_glass_break()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(4, Gap(2, 1)),
                Plan = WW(3),
                Pieces = Ps(Pc("D2_90", "W", 4, 2, new[] { "glass" })),
            });
            int calls = 0;
            var rules = new FallRules(modifyFall: (st, id, plan) =>
            {
                calls++;
                return plan;
            }, onLanded: GlassRules.OnLanded);
            var fall = Gravity.ComputeFall(s, 0, R(0, 6, 2), Opts(rules));
            AssertFall(fall, FallResult.ModeRail, A(6, 2), distance: 0);
            Assert.AreEqual(LandingEffect.None, fall.Effect);
            CollectionAssert.AreEqual(new[] { A(6, 2) }, fall.Path);
            CollectionAssert.AreEqual(Reasons(VerdictReasons.Support), fall.Verdict.Reasons);
            Assert.AreEqual(0, calls);
        }

        private static LevelSpec ColourSpec() => new LevelSpec
        {
            Difficulty = "easy",
            Plan = new[] { "WW", "WY", "WW", "WW", "WW" },
            Pieces = Ps(Pc("I3_0", "W", 0, 0), Pc("I3_0", "W", 1, 0), Pc("C3_0", "W", 2, 0)),
        };

        [Test(Description = "K-18 GDD example: C3_0 W landing with (7,3) on a Y cell is wrong (colour) on easy, position only on hard")]
        public void K18_GDD_example_C3_0_W_landing_with_7_3_on_a_Y_cell_is_wrong_on_easy_position_only_on_hard()
        {
            var s = Init(ColourSpec());
            ToSite(s, 0, 6, 0);
            ToSite(s, 1, 7, 0);
            var fall = Gravity.ComputeFall(s, 2, N(6, 8));
            Assert.AreEqual(A(6, 3), fall.Landing);
            CollectionAssert.AreEqual(Reasons(VerdictReasons.Color), fall.Verdict.Reasons);
            Assert.AreEqual(Shadow(ShadowInfo.ToneWrong, Reasons(VerdictReasons.Color), NoCells, false), Gravity.ShadowInfo(fall, "easy"));
            Assert.AreEqual(ShadowInfo.ToneWrong, Gravity.ShadowInfo(fall, "normal").Tone);
            Assert.AreEqual(NeutralShadow, Gravity.ShadowInfo(fall, "hard"));
            Assert.AreEqual(ShadowInfo.ToneNeutral, Gravity.ShadowInfo(fall, "superhard").Tone);
        }

        [Test(Description = "K-18 neutral shadow never leaks color; support may show (K-34 hook 2)")]
        public void K18_neutral_shadow_never_leaks_color_support_may_show()
        {
            var s = Init(new LevelSpec { Plan = new[] { "YY", "WW", "WW" }, Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("O4_0", "W", 2, 0)) });
            ToSite(s, 0, 6, 0);
            // O4 W lands at (6,1): (6,2)/(7,2) are Y (colour) and (7,0) is empty (support)
            var fall = Gravity.ComputeFall(s, 1, N(6, 8));
            CollectionAssert.AreEqual(Reasons(VerdictReasons.Color, VerdictReasons.Support), fall.Verdict.Reasons);
            foreach (var difficulty in new[] { "hard", "superhard" })
            {
                var shown = Gravity.ShadowInfo(fall, difficulty);
                Assert.AreEqual(ShadowInfo.ToneNeutral, shown.Tone);
                CollectionAssert.AreEqual(Reasons(VerdictReasons.Support), shown.Reasons);
                CollectionAssert.AreEqual(Missing(Site(7, 0)), shown.MissingSupport);
            }
            CollectionAssert.AreEqual(Reasons(VerdictReasons.Color, VerdictReasons.Support), Gravity.ShadowInfo(fall, "easy").Reasons);
        }

        [Test(Description = "E-20 a landing on an unrevealed `?` cell is neutral at every difficulty; once revealed it is not")]
        public void E20_a_landing_on_an_unrevealed_hidden_cell_is_neutral_at_every_difficulty_once_revealed_it_is_not()
        {
            var s = Init(new LevelSpec
            {
                Plan = new[] { "??", "WY" },
                Hidden = Repeat1,
                Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("B1_0", "Y", 1, 0), Pc("B1_0", "Y", 2, 0)),
            });
            ToSite(s, 0, 6, 0);
            ToSite(s, 1, 7, 0);
            var fall = Gravity.ComputeFall(s, 2, N(6, 8));
            Assert.AreEqual(A(6, 1), fall.Landing);
            Assert.IsTrue(fall.TouchesHidden);
            CollectionAssert.AreEqual(Reasons(VerdictReasons.Color), fall.Verdict.Reasons); // E-20: wrong colour on a hidden cell is a wrong placement
            foreach (var difficulty in new[] { "easy", "normal", "hard", "superhard" })
                Assert.AreEqual(NeutralShadow, Gravity.ShadowInfo(fall, difficulty));
            s.Buf[s.Lvl.Layout.Revealed] = 0b0100; // (6,1) revealed
            Assert.IsFalse(Gravity.ComputeFall(s, 2, N(6, 8)).TouchesHidden);
        }

        [Test(Description = "K-18 physics information (glass crack) shows at every difficulty, also in a neutral shadow")]
        public void K18_physics_information_shows_at_every_difficulty_also_in_a_neutral_shadow()
        {
            var s = Init(new LevelSpec { Difficulty = "hard", Plan = new[] { "WW" }, Pieces = Ps(Pc("B1_0", "W", 0, 0, new[] { "glass" })) });
            var fall = Gravity.ComputeFall(s, 0, N(6, 8), Opts(GlassRules));
            Assert.AreEqual(LandingEffect.Break(1), fall.Effect);
            Assert.AreEqual(Shadow(ShadowInfo.ToneNeutral, new string[0], NoCells, true), Gravity.ShadowInfo(fall, s.Lvl.Difficulty));
        }

        [Test(Description = "K-18 computeFall only accepts a release node fully on the site")]
        public void K18_computeFall_only_accepts_a_release_node_fully_on_the_site()
        {
            var s = Init(new LevelSpec { Plan = new[] { "WW" }, Pieces = Ps(Pc("D2_90", "W", 0, 0)) });
            Assert.Throws<ArgumentOutOfRangeException>(() => Gravity.ComputeFall(s, 0, N(5, 8)));
            Assert.Throws<ArgumentOutOfRangeException>(() => Gravity.ComputeFall(s, 0, N(2, 2)));
            Assert.Throws<ArgumentOutOfRangeException>(() => Gravity.ComputeFall(s, 0, N(7, 8)));
        }

        // --- K-19 ------------------------------------------------------------------------------------------------------------

        [Test(Description = "K-19 normal gravity has no hold limit and no steering")]
        public void K19_normal_gravity_has_no_hold_limit_and_no_steering()
        {
            var s = Init(new LevelSpec { Plan = new[] { "WW" }, Pieces = Ps(Pc("B1_0", "W", 0, 0)) });
            Assert.AreEqual("normal", s.Lvl.Gravity.Build);
            Assert.IsNull(s.Lvl.Gravity.HoldMs);
            Assert.IsNull(s.Lvl.Gravity.HoldMsReduced);
            Assert.IsFalse(s.Lvl.Gravity.Steerable);
            Assert.AreEqual(3, s.Lvl.Gravity.GlassThreshold);
            var seen = new List<FallPlan>();
            var rules = new FallRules(modifyFall: (st, id, plan) =>
            {
                seen.Add(plan);
                return plan;
            });
            var plain = Gravity.ComputeFall(s, 0, N(6, 8));
            var steered = Gravity.ComputeFall(s, 0, N(6, 8), Opts(rules, new Steer(1, 4)));
            Assert.IsNull(steered.Steered);
            Assert.AreEqual(plain.Landing, steered.Landing);
            CollectionAssert.AreEqual(plain.Path, steered.Path);
            Assert.IsNull(seen[0].Steer);
        }

        [Test(Description = "K-19 profiles: glass threshold low 4 / normal 3 / high 2; G-H hold 700 ms (1400 reduced)")]
        public void K19_profiles_glass_threshold_low_4_normal_3_high_2_GH_hold_700_ms()
        {
            var pieces = Ps(Pc("B1_0", "W", 0, 0));
            var low = Builders.Compiled(new LevelSpec { Pieces = pieces, Gravity = new GravitySpec { Build = "low" } }).Gravity;
            Assert.AreEqual(4, low.GlassThreshold);
            Assert.IsTrue(low.Steerable);
            Assert.IsNull(low.HoldMs);
            var high = Builders.Compiled(new LevelSpec { Pieces = pieces, Gravity = new GravitySpec { Build = "high" } }).Gravity;
            Assert.AreEqual(2, high.GlassThreshold);
            Assert.IsFalse(high.Steerable);
            Assert.AreEqual(700, high.HoldMs);
            Assert.AreEqual(1400, high.HoldMsReduced);
        }

        [Test(Description = "K-19 glass breaks only when d > threshold (OBSTACLES S3 example: normal, d = 5 breaks, d = 3 holds)")]
        public void K19_glass_breaks_only_when_d_above_threshold()
        {
            var s = Init(new LevelSpec
            {
                Plan = WW(5),
                Pieces = Ps(Pc("I3_0", "W", 0, 0), Pc("I3_0", "W", 1, 0), Pc("D2_90", "W", 2, 0, new[] { "glass" })),
            });
            ToSite(s, 0, 6, 0);
            ToSite(s, 1, 7, 0);
            var high = Gravity.ComputeFall(s, 2, N(6, 8), Opts(GlassRules));
            Assert.AreEqual(A(6, 3), high.Landing);
            Assert.AreEqual(5, high.Distance);
            Assert.AreEqual(LandingEffect.Break(1), high.Effect);
            var low = Gravity.ComputeFall(s, 2, N(6, 6), Opts(GlassRules));
            Assert.AreEqual(3, low.Distance);
            Assert.AreEqual(LandingEffect.None, low.Effect);
        }

        /// <summary>GDD K-19 example board (low): (6,0)–(6,1) and (7,0)–(7,1) filled, (7,2) empty `.`, (7,3) a rail block.</summary>
        private static GameState SteerBoard()
        {
            var s = Init(new LevelSpec
            {
                Gravity = new GravitySpec { Build = "low" },
                Plan = new[] { "WW", "W.", "WW", "WW" },
                Pieces = Ps(Pc("D2_0", "W", 0, 0), Pc("D2_0", "W", 1, 0), Pc("B1_0", "W", 2, 0), Pc("B1_0", "W", 3, 0)),
            });
            ToSite(s, 0, 6, 0);
            ToSite(s, 1, 7, 0);
            ToSite(s, 2, 7, 3);
            return s;
        }

        [Test(Description = "K-19 G-L GDD example: steer right at row 5 lands on the overhang; at row 2 it slides under it")]
        public void K19_GL_GDD_example_steer_right_at_row_5_lands_on_the_overhang_at_row_2_it_slides_under_it()
        {
            var s = SteerBoard();
            Assert.AreEqual(A(6, 2), Gravity.ComputeFall(s, 3, N(6, 8)).Landing);
            var over = Gravity.ComputeFall(s, 3, N(6, 8), Opts(steer: new Steer(1, 5)));
            AssertSteer(over.Steered, 1, 5);
            Assert.AreEqual(A(7, 4), over.Landing);
            CollectionAssert.AreEqual(new[] { A(6, 8), A(6, 5), A(7, 5), A(7, 4) }, over.Path);
            Assert.AreEqual(4, over.Distance); // K-19 rule 5: total fall from the release row
            var under = Gravity.ComputeFall(s, 3, N(6, 8), Opts(steer: new Steer(1, 2)));
            Assert.AreEqual(A(7, 2), under.Landing);
            CollectionAssert.AreEqual(Reasons(VerdictReasons.Window), under.Verdict.Reasons); // access is the rule's, correctness is K-16/K-34's
        }

        [Test(Description = "K-19 G-L invalid steer does not consume the right (edge, outside the window rows)")]
        public void K19_GL_invalid_steer_does_not_consume_the_right()
        {
            var s = SteerBoard();
            foreach (var steer in new[]
            {
                new Steer(-1, 5), // column 5 is not on the site
                new Steer(1, 3), // (7,3) is occupied
                new Steer(1, 9), // above the release row
                new Steer(1, 1), // below the unsteered landing row
            })
            {
                var fall = Gravity.ComputeFall(s, 3, N(6, 8), Opts(steer: steer));
                Assert.IsNull(fall.Steered);
                Assert.AreEqual(A(6, 2), fall.Landing);
            }
        }

        [Test(Description = "K-19 G-L steer of 1×2 piece blocked by occupied upper cell")]
        public void K19_GL_steer_of_1x2_piece_blocked_by_occupied_upper_cell()
        {
            var s = Init(new LevelSpec
            {
                Gravity = new GravitySpec { Build = "low" },
                Plan = WW(6),
                Pieces = Ps(Pc("D2_0", "W", 0, 0)),
                Debris = new[] { new D("B1_0", "R", 7, 4) },
            });
            // D2_0 falls in column 6; at row 3 the shifted cells are (7,3) empty and (7,4) debris → no shift
            var blocked = Gravity.ComputeFall(s, 0, N(6, 8), Opts(steer: new Steer(1, 3)));
            Assert.IsNull(blocked.Steered);
            Assert.AreEqual(A(6, 0), blocked.Landing);
            var free = Gravity.ComputeFall(s, 0, N(6, 8), Opts(steer: new Steer(1, 6)));
            AssertSteer(free.Steered, 1, 6);
            Assert.AreEqual(A(7, 5), free.Landing);
        }

        // --- wind (W8 via a rule hook) and balloon (S8 via a rule hook) -----------------------------------------------------------

        /// <summary>OBSTACLES W8 example: top(6) = 2, top(7) = 3.</summary>
        private static GameState WindBoard()
        {
            var s = Init(new LevelSpec { Plan = WW(5), Pieces = Ps(Pc("D2_0", "W", 0, 0), Pc("I3_0", "W", 1, 0), Pc("B1_0", "W", 2, 0)) });
            ToSite(s, 0, 6, 0);
            ToSite(s, 1, 7, 0);
            return s;
        }

        [Test(Description = "K-11 wind shifts the block at the release row before it falls (OBSTACLES W8 example)")]
        public void K11_wind_shifts_the_block_at_the_release_row_before_it_falls()
        {
            var fall = Gravity.ComputeFall(WindBoard(), 2, N(6, 7), Opts(WindRight));
            Assert.AreEqual(1, fall.Drift);
            Assert.AreEqual(A(7, 3), fall.Landing);
            CollectionAssert.AreEqual(new[] { A(6, 7), A(7, 7), A(7, 3) }, fall.Path);
        }

        [Test(Description = "E-17 no wind shift when the block already rests on the silhouette (d = 0)")]
        public void E17_no_wind_shift_when_the_block_already_rests_on_the_silhouette()
        {
            var fall = Gravity.ComputeFall(WindBoard(), 2, N(6, 2), Opts(WindRight));
            Assert.AreEqual(0, fall.Drift);
            Assert.AreEqual(A(6, 2), fall.Landing);
        }

        [Test(Description = "K-11 the core ignores a wind shift off the site or into a blocked cell")]
        public void K11_the_core_ignores_a_wind_shift_off_the_site_or_into_a_blocked_cell()
        {
            var always = new FallRules(modifyFall: (st, id, plan) => plan.WithDrift(-1));
            Assert.AreEqual(0, Gravity.ComputeFall(WindBoard(), 2, N(6, 7), Opts(always)).Drift);
            var right = new FallRules(modifyFall: (st, id, plan) => plan.WithDrift(1));
            Assert.AreEqual(0, Gravity.ComputeFall(WindBoard(), 2, N(6, 2), Opts(right)).Drift); // (7,2) is filled
        }

        /// <summary>OBSTACLES S8 example: plan h = 6, column 7 rows 0–3 filled, (7,4) `.`, (7,5) W empty.</summary>
        private static GameState BalloonBoard()
        {
            var s = Init(new LevelSpec
            {
                Plan = new[] { "WW", "W.", "WW", "WW", "WW", "WW" },
                Pieces = Ps(Pc("I4_0", "W", 0, 0), Pc("B1_0", "W", 1, 0, new[] { "balloon" }), Pc("B1_0", "W", 2, 0)),
            });
            ToSite(s, 0, 7, 0);
            return s;
        }

        [Test(Description = "K-34 balloon obeys support rule (OBSTACLES S8 example: the balloon hangs at (7,5), a plain block falls into `.`)")]
        public void K34_balloon_obeys_support_rule()
        {
            var s = BalloonBoard();
            var balloon = Gravity.ComputeFall(s, 1, N(7, 8), Opts(BalloonRules));
            AssertFall(balloon, dir: 1, landing: A(7, 5), distance: -3);
            Assert.IsTrue(balloon.Verdict.Ok);
            var plain = Gravity.ComputeFall(s, 2, N(7, 8), Opts(BalloonRules));
            Assert.AreEqual(A(7, 4), plain.Landing);
            CollectionAssert.AreEqual(Reasons(VerdictReasons.Window), plain.Verdict.Reasons);
        }

        [Test(Description = "E-15 a free balloon rises to the plan ceiling; E-35 released above the ceiling it comes down to it")]
        public void E15_a_free_balloon_rises_to_the_plan_ceiling_E35_released_above_the_ceiling_it_comes_down_to_it()
        {
            var s = BalloonBoard();
            var up = Gravity.ComputeFall(s, 1, N(6, 0), Opts(BalloonRules));
            Assert.AreEqual(A(6, 5), up.Landing);
            Assert.AreEqual(5, up.Distance);
            var down = Gravity.ComputeFall(s, 1, N(6, 9), Opts(BalloonRules));
            Assert.AreEqual(A(6, 5), down.Landing);
            Assert.AreEqual(-4, down.Distance);
            Assert.AreEqual(5, Gravity.SiteLandingRow(s, 1, 6, 9, 1));
        }

        [Test(Description = "E-15 a balloon over a silhouette that reaches the ceiling stays on top of it (outside)")]
        public void E15_a_balloon_over_a_silhouette_that_reaches_the_ceiling_stays_on_top_of_it()
        {
            var s = Init(new LevelSpec
            {
                Plan = WW(6),
                Pieces = Ps(Pc("I4_0", "W", 0, 0), Pc("D2_0", "W", 1, 0), Pc("B1_0", "W", 2, 0, new[] { "balloon" })),
            });
            ToSite(s, 0, 7, 0);
            ToSite(s, 1, 7, 4);
            var fall = Gravity.ComputeFall(s, 2, N(7, 8), Opts(BalloonRules));
            Assert.AreEqual(A(7, 6), fall.Landing);
            Assert.AreEqual(VerdictReasons.Outside, fall.Verdict.Reasons[0]);
        }

        [Test(Description = "E-15 the balloon ceiling follows the elevator offset (h + e − 1)")]
        public void E15_the_balloon_ceiling_follows_the_elevator_offset()
        {
            var s = Init(new LevelSpec { Id = 38, Plan = WW(2), Elevator = Elevator(0, 2, 2, 1), Pieces = Ps(Pc("B1_0", "W", 0, 0, new[] { "balloon" })) });
            Assert.AreEqual(A(6, 3), Gravity.ComputeFall(s, 0, N(6, 9), Opts(BalloonRules)).Landing);
        }

        [Test(Description = "K-19 G-L steering a balloon: rising under an overhang stops beneath it; above the ceiling it comes down")]
        public void K19_GL_steering_a_balloon_rising_under_an_overhang_stops_beneath_it_above_the_ceiling_it_comes_down()
        {
            var s = Init(new LevelSpec
            {
                Gravity = new GravitySpec { Build = "low" },
                Plan = WW(6),
                Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("B1_0", "W", 1, 0, new[] { "balloon" })),
            });
            ToSite(s, 0, 7, 4);
            var rising = Gravity.ComputeFall(s, 1, N(6, 0), Opts(BalloonRules, new Steer(1, 2)));
            AssertSteer(rising.Steered, 1, 2);
            Assert.AreEqual(A(7, 3), rising.Landing);
            var sinking = Gravity.ComputeFall(s, 1, N(6, 9), Opts(BalloonRules, new Steer(1, 7)));
            Assert.AreEqual(A(7, 5), sinking.Landing);
        }

        [Test(Description = "E-14 a balloon released in the yard rises under the first block above, else to y = 7")]
        public void E14_a_balloon_released_in_the_yard_rises_under_the_first_block_above_else_to_y_7()
        {
            var s = Init(new LevelSpec
            {
                Plan = new[] { "WW" },
                Pieces = Ps(Pc("B1_0", "W", 2, 1, new[] { "balloon" }), Pc("B1_0", "W", 2, 5), Pc("D2_0", "W", 4, 0, new[] { "balloon" })),
            });
            Assert.AreEqual(4, Gravity.YardBalloonLanding(s, 0, 2, 1));
            Assert.AreEqual(7, Gravity.YardBalloonLanding(s, 0, 3, 1));
            Assert.AreEqual(6, Gravity.YardBalloonLanding(s, 2, 4, 0));
        }

        // --- K-20 yard gravity -------------------------------------------------------------------------------------------------

        [Test(Description = "K-20 GDD example: a D2_90 resting on (3,1) holds; without it, the D2_90 and the B1 above fall 2 rows together")]
        public void K20_GDD_example_a_D2_90_resting_on_3_1_holds_without_it_the_D2_90_and_the_B1_above_fall_2_rows_together()
        {
            var baseP = Ps(Pc("D2_0", "W", 2, 0), Pc("D2_90", "Y", 2, 2), Pc("B1_0", "G", 2, 3));
            var held = Init(new LevelSpec { Gravity = new GravitySpec { Yard = true }, Pieces = baseP.Plus(Pc("D2_0", "R", 3, 0)) });
            Remove(held, 0);
            CollectionAssert.IsEmpty(Gravity.SettleYard(held));

            var s = Init(new LevelSpec { Gravity = new GravitySpec { Yard = true }, Pieces = baseP });
            Remove(s, 0);
            var fallen = new List<FallenBlock[]>();
            var motions = Gravity.SettleYard(s, new SettleHooks((st, f) =>
            {
                fallen.Add((FallenBlock[])f.Clone());
                return false;
            }));
            CollectionAssert.AreEqual(new[] { PieceMotion(1, 2, 2, 2, 0), PieceMotion(2, 2, 3, 2, 1) }, motions);
            Assert.AreEqual(1, fallen.Count);
            Assert.AreEqual(2, fallen[0].Length);
            Assert.AreEqual(1, fallen[0][0].PieceId);
            CollectionAssert.AreEqual(new[] { C(2, 2), C(3, 2) }, fallen[0][0].Cells);
            Assert.AreEqual(2, fallen[0][1].PieceId);
            CollectionAssert.AreEqual(new[] { C(2, 3) }, fallen[0][1].Cells);
            ExpectConsistent(s);
        }

        [Test(Description = "K-10 GDD example: yard gravity off leaves a dropped O4 hanging; on, it falls 2 rows to (4,4)")]
        public void K10_GDD_example_yard_gravity_off_leaves_a_dropped_O4_hanging_on_it_falls_2_rows_to_4_4()
        {
            var pieces = Ps(Pc("I4_0", "W", 4, 0), Pc("I4_0", "W", 5, 0), Pc("O4_0", "Y", 4, 6));
            var off = Init(new LevelSpec { Pieces = pieces });
            CollectionAssert.IsEmpty(Gravity.SettleYard(off));
            var on = Init(new LevelSpec { Gravity = new GravitySpec { Yard = true }, Pieces = pieces });
            CollectionAssert.AreEqual(new[] { PieceMotion(2, 4, 6, 4, 4) }, Gravity.SettleYard(on));
            ExpectConsistent(on);
        }

        [Test(Description = "K-20 crates never fall and support blocks; chained, wet and heavy blocks fall too (N27)")]
        public void K20_crates_never_fall_and_support_blocks_chained_wet_and_heavy_blocks_fall_too()
        {
            var s = Init(new LevelSpec
            {
                Id = 12,
                Gravity = new GravitySpec { Yard = true },
                Pieces = Ps(Pc("B1_0", "W", 1, 4), Pc("B1_0", "W", 3, 5, new[] { "chained" }), Pc("B1_0", "W", 4, 6, new[] { "wet" }, 3), Pc("I3_90", "W", 0, 7)),
                Obstacles = new[] { Crate(1, 3, 2) },
            });
            var motions = Gravity.SettleYard(s);
            CollectionAssert.AreEqual(new[] { (1, A(3, 0)), (2, A(4, 0)), (3, A(0, 5)) }, motions.Select(m => (m.Id, m.To)).ToArray());
            Assert.AreEqual(-1, YardOcc(s, 1, 3));
            Assert.AreEqual(4, PieceY(s, 0));
            ExpectConsistent(s);
        }

        [Test(Description = "E-33 falling block lands on a rising balloon without a gap")]
        public void E33_falling_block_lands_on_a_rising_balloon_without_a_gap()
        {
            var s = Init(new LevelSpec
            {
                Gravity = new GravitySpec { Yard = true },
                Pieces = Ps(Pc("B1_0", "W", 0, 4, new[] { "balloon" }), Pc("B1_0", "Y", 0, 6), Pc("B1_0", "R", 0, 7)),
            });
            CollectionAssert.AreEqual(new[] { PieceMotion(1, 0, 6, 0, 5), PieceMotion(2, 0, 7, 0, 6) }, Gravity.SettleYard(s));
            Assert.AreEqual(4, PieceY(s, 0));
        }

        [Test(Description = "K-20 balloons rise to the yard ceiling (y = 7); stacked balloons rise together")]
        public void K20_balloons_rise_to_the_yard_ceiling_stacked_balloons_rise_together()
        {
            var s = Init(new LevelSpec
            {
                Gravity = new GravitySpec { Yard = true },
                Pieces = Ps(Pc("B1_0", "W", 3, 2, new[] { "balloon" }), Pc("B1_0", "W", 4, 0, new[] { "balloon" }), Pc("B1_0", "W", 4, 1, new[] { "balloon" }), Pc("I4_0", "W", 5, 0)),
            });
            CollectionAssert.AreEqual(new[] { PieceMotion(1, 4, 0, 4, 6), PieceMotion(2, 4, 1, 4, 7), PieceMotion(0, 3, 2, 3, 7) }, Gravity.SettleYard(s));
            ExpectConsistent(s);
        }

        [Test(Description = "K-20 cement bags fall even with yard gravity off (S-12); a falling bag gives no neighbour effect (N26)")]
        public void K20_cement_bags_fall_even_with_yard_gravity_off_a_falling_bag_gives_no_neighbour_effect()
        {
            var s = Init(new LevelSpec { Id = 18, Pieces = Ps(Pc("B1_0", "W", 2, 5)), Obstacles = new[] { Bag(4, 5) } });
            int calls = 0;
            var motions = Gravity.SettleYard(s, new SettleHooks((st, f) =>
            {
                calls++;
                return false;
            }));
            CollectionAssert.AreEqual(new[] { Motion(YardMotion.KindBag, 0, A(4, 5), A(4, 0), -5) }, motions);
            CollectionAssert.AreEqual(new[] { 0, -1, 1 }, new[] { YardOcc(s, 4, 5), YardOcc(s, 4, 0), YardOcc(s, 2, 5) });
            Assert.AreEqual(0, calls);
        }

        [Test(Description = "E-12 gravity runs again after a fall effect removes an obstacle; one motion per block")]
        public void E12_gravity_runs_again_after_a_fall_effect_removes_an_obstacle_one_motion_per_block()
        {
            var s = Init(new LevelSpec
            {
                Id = 12,
                Gravity = new GravitySpec { Yard = true },
                Pieces = Ps(Pc("B1_0", "W", 0, 3), Pc("B1_0", "Y", 1, 4), Pc("B1_0", "G", 1, 7)),
                Obstacles = new[] { Crate(1, 3, 1) },
            });
            var calls = new List<int[]>();
            // neighbour effect of the falls: a crate next to a pre-fall cell loses its last hp and vanishes
            bool OnFallen(GameState st, FallenBlock[] fallen)
            {
                calls.Add(fallen.Select(f => f.PieceId).ToArray());
                bool changed = false;
                foreach (var f in fallen)
                    foreach (var c in f.Cells)
                        foreach (var n in Coords.Neighbors4(Geometry.DefaultGeo, c.X, c.Y))
                        {
                            if (n.Ix > 5 || YardOcc(st, n.Ix, n.Iy) != -1) continue;
                            SetYardOcc(st, n.Ix, n.Iy, 0);
                            SetObstacleField(st, 0, OF.Hp, 0);
                            changed = true;
                        }
                return changed;
            }
            var motions = Gravity.SettleYard(s, new SettleHooks(OnFallen));
            Assert.AreEqual(2, calls.Count);
            CollectionAssert.AreEqual(new[] { 0, 2 }, calls[0]);
            CollectionAssert.AreEqual(new[] { 1, 2 }, calls[1]);
            CollectionAssert.AreEqual(new[] { PieceMotion(0, 0, 3, 0, 0), PieceMotion(1, 1, 4, 1, 0), PieceMotion(2, 1, 7, 1, 1) }, motions);
            Assert.AreEqual(0, ObstacleField(s, 0, OF.Hp));
            ExpectConsistent(s);
        }

        [Test(Description = "K-14 locked site blocks never move with yard gravity; settling is deterministic")]
        public void K14_locked_site_blocks_never_move_with_yard_gravity_settling_is_deterministic()
        {
            var s = Init(new LevelSpec
            {
                Gravity = new GravitySpec { Yard = true },
                Plan = WW(3),
                Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("D2_90", "W", 2, 4), Pc("B1_0", "W", 1, 6, new[] { "balloon" })),
            });
            ToSite(s, 0, 7, 2); // a floating locked block (no site gravity, TECH §5.4)
            var copy = CloneState(s);
            var a = Gravity.SettleYard(s);
            var b = Gravity.SettleYard(copy);
            CollectionAssert.AreEqual(a, b);
            CollectionAssert.AreEqual(copy.Buf, s.Buf);
            Assert.AreEqual(0, Hdr(s, H.Turn));
            Assert.AreEqual(1, SiteOcc(s, 0, 1, 2));
            CollectionAssert.AreEqual(new[] { 1, 2 }, a.Select(m => m.Id).ToArray());
        }
    }
}
