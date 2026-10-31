#nullable enable
// Port of tests/review/placement.review.test.ts (independent rule review of gravity.ts and placement.ts, rounds 1–4) —
// the tests that run on the gravity / placement / movement APIs alone. The tests that go through `applyMove` or
// `GameSession` (K-17 / K-34 / K-22 pipeline examples, K-18 shadow-equals-result properties, S4 debris through the
// pipeline, K-14 vs K-39 Undo) are in PlacementReviewTests.P3.cs (same partial class).
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
    public partial class PlacementReviewTests
    {
        /// <summary>Y8-like placement hook (the real rule is Phase 3): mortar blocks ask to stick on a wrong placement.</summary>
        private static readonly PlacementRules Mortar = new PlacementRules((s, id, cells, v) =>
            HasFlag(s, id, FlagBit.Mortar) ? PlacementOverrides.Stick : PlacementOverrides.Default);

        private static readonly BoardGeo Geo = Geometry.DefaultGeo;

        // --- K-16 + K-34: the single correctness test ------------------------------------------------------------------------

        [Test(Description = "K-34 a block cannot leave a buried hole under its own overhang (C3_180 over (6,0),(7,0) leaves (6,1) empty)")]
        public void K34_a_block_cannot_leave_a_buried_hole_under_its_own_overhang()
        {
            // GDD K-34: "bir bloğun altında doldurulması gereken boş hücre (gömülü delik) bırakılamaz"
            var s = Init(new LevelSpec { Plan = WW(3), Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("B1_0", "W", 1, 0), Pc("C3_180", "W", 2, 0)) });
            ToSite(s, 0, 6, 0);
            ToSite(s, 1, 7, 0);
            // C3_180 = (1,0) (0,1) (1,1): top(6) = top(7) = 1 → yL = max(1 − 1, 1 − 0) = 1 → cells (7,1) (6,2) (7,2)
            var fall = Gravity.ComputeFall(s, 2, N(6, 8));
            Assert.AreEqual(A(6, 1), fall.Landing);
            Assert.AreEqual(7, fall.Distance);
            Assert.AreEqual(Wrong(Reasons(VerdictReasons.Support), Site(6, 1)), fall.Verdict);
            CollectionAssert.AreEqual(new[] { Site(6, 1), Site(7, 1) }, Placement.BuildFront(s));
        }

        [Test(Description = "K-34 a Golden Trowel cell is \"doğru dolu\": it supports a falling block that is then correct")]
        public void K34_a_Golden_Trowel_cell_is_dogru_dolu_it_supports_a_falling_block_that_is_then_correct()
        {
            // GDD §0: "Doğru dolu hücre: Doğru yerleşmiş (kilitli) bir bloğun ya da Altın Mala'nın doldurduğu plan hücresi"
            var s = Init(new LevelSpec { Plan = WW(2), Pieces = Ps(Pc("B1_0", "W", 0, 0)) });
            SetSiteOcc(s, 0, 0, 0, SiteTrowel); // trowel at (6,0)
            Grid.RefreshSiteMasks(s, 0);
            Assert.AreEqual(0b1, FilledMask(s, 0, 0));
            CollectionAssert.AreEqual(new[] { 0b1, 0b0 }, Gravity.SiteColumnMasks(s));
            var fall = Gravity.ComputeFall(s, 0, N(6, 8));
            Assert.AreEqual(A(6, 1), fall.Landing);
            Assert.AreEqual(OkV, fall.Verdict);
            CollectionAssert.AreEqual(new[] { Site(6, 1), Site(7, 0) }, Placement.BuildFront(s));
            Assert.IsFalse(Placement.IsSegmentComplete(s, 0));
        }

        [Test(Description = "K-34 an empty `.` in the bottom row counts as filled: no front there, a fall into it is `window`, a rail above it is correct")]
        public void K34_an_empty_dot_in_the_bottom_row_counts_as_filled()
        {
            // plan (top → bottom) WW / .W → (6,0) is `.`, (7,0) W, (6,1) W, (7,1) W; OBSTACLES S2: "`.` üstündeki hücre … ray"
            var s = Init(new LevelSpec { Wall = Wall(3, Gap(1, 1)), Plan = new[] { "WW", ".W" }, Pieces = Ps(Pc("B1_0", "W", 5, 1)) });
            CollectionAssert.AreEqual(new[] { Site(6, 1), Site(7, 0) }, Placement.BuildFront(s));
            var into = Gravity.ComputeFall(s, 0, N(6, 8));
            Assert.AreEqual(A(6, 0), into.Landing);
            Assert.AreEqual(Wrong(Reasons(VerdictReasons.Window)), into.Verdict);
            var rail = Gravity.ComputeFall(s, 0, R(0, 6, 1));
            Assert.AreEqual(FallResult.ModeRail, rail.Mode);
            Assert.AreEqual(A(6, 1), rail.Landing);
            Assert.AreEqual(OkV, rail.Verdict);
        }

        [Test(Description = "K-34 an unfilled `?` cell below is missing support; `?` cells can be front cells; K-15 needs them filled")]
        public void K34_an_unfilled_hidden_cell_below_is_missing_support_hidden_cells_can_be_front_cells()
        {
            // K-32 repeat p = 1: (c, 1) takes the colour of (c, 0) → (6,1) = W, (7,1) = Y
            var s = Init(new LevelSpec
            {
                Plan = new[] { "WW", "??", "WY" },
                Hidden = Repeat1,
                Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("B1_0", "Y", 1, 0), Pc("B1_0", "W", 2, 0)),
            });
            ToSite(s, 0, 6, 0);
            ToSite(s, 1, 7, 0);
            Assert.AreEqual(Wrong(Reasons(VerdictReasons.Support), Site(6, 1)), Placement.IsCorrectPlacement(s, 2, CellsOf("B1_0", 6, 2)));
            CollectionAssert.AreEqual(new[] { Site(6, 1), Site(7, 1) }, Placement.BuildFront(s));
            Assert.IsFalse(Placement.IsSegmentComplete(s, 0));
        }

        [Test(Description = "K-34 a correct placement under a wrong-object overhang is allowed: only the cells below count (K-19 example note)")]
        public void K34_a_correct_placement_under_a_wrong_object_overhang_is_allowed_only_the_cells_below_count()
        {
            // GDD K-19: "Çıkıntının altına doğru yerleşim yalnızca çıkıntı yanlış bir nesneyse (… Y8) mümkündür."
            var s = Init(new LevelSpec
            {
                Wall = Wall(4, Gap(2, 1)),
                Plan = WW(4),
                Pieces = Ps(Pc("D2_0", "W", 0, 0), Pc("B1_0", "R", 1, 0, new[] { "mortar" }), Pc("B1_0", "W", 5, 2)),
            });
            ToSite(s, 0, 7, 0); // (7,0)–(7,1) correctly filled
            ToSite(s, 1, 7, 3, locked: false, stuck: true); // stuck mortar overhang at (7,3), (7,2) empty under it
            var under = Gravity.ComputeFall(s, 2, R(0, 7, 2));
            Assert.AreEqual(A(7, 2), under.Landing);
            Assert.AreEqual(OkV, under.Verdict);
            // over the top of the mortar: above the plan (h = 4) and K-34 broken by the empty (7,2) and the mortar cell (7,3)
            var over = Gravity.ComputeFall(s, 2, N(7, 8));
            Assert.AreEqual(A(7, 4), over.Landing);
            Assert.AreEqual(Wrong(Reasons(VerdictReasons.Outside, VerdictReasons.Support), Site(7, 2), Site(7, 3)), over.Verdict);
        }

        [Test(Description = "K-34 hook 1: a wrong object ABOVE an empty plan cell does not remove the front below it")]
        public void K34_hook_1_a_wrong_object_ABOVE_an_empty_plan_cell_does_not_remove_the_front_below_it()
        {
            // hook 1: "boş, `.` olmayan ve altındaki bütün plan hücreleri doğru dolu ya da boş `.` olan en alt plan hücresi"
            var s = Init(new LevelSpec { Plan = WW(3), Pieces = Ps(Pc("B1_0", "W", 0, 0)), Debris = new[] { new D("B1_0", "R", 6, 2) } });
            Assert.AreEqual(0b100, WrongOccMask(s, 0, 0));
            CollectionAssert.AreEqual(new[] { Site(6, 0), Site(7, 0) }, Placement.BuildFront(s));
            Assert.IsTrue(Placement.IsCorrectPlacement(s, 0, CellsOf("B1_0", 6, 0)).Ok);
        }

        // --- K-18 shadow ------------------------------------------------------------------------------------------------------

        [Test(Description = "K-18 a correct landing shows \"correct\" on easy/normal and only the position on hard/superhard")]
        public void K18_a_correct_landing_shows_correct_on_easy_normal_and_only_the_position_on_hard_superhard()
        {
            var s = Init(new LevelSpec { Plan = new[] { "WW" }, Pieces = Ps(Pc("B1_0", "W", 0, 0)) });
            var fall = Gravity.ComputeFall(s, 0, N(6, 8));
            Assert.IsTrue(fall.Verdict.Ok);
            Assert.AreEqual(CorrectShadow, Gravity.ShadowInfo(fall, "easy"));
            Assert.AreEqual(CorrectShadow, Gravity.ShadowInfo(fall, "normal"));
            Assert.AreEqual(NeutralShadow, Gravity.ShadowInfo(fall, "hard"));
            Assert.AreEqual(NeutralShadow, Gravity.ShadowInfo(fall, "superhard"));
        }

        [Test(Description = "K-18 a CORRECT landing on an unrevealed `?` cell is neutral even on easy (no correct cue leaks), free fall and rail")]
        public void K18_a_CORRECT_landing_on_an_unrevealed_hidden_cell_is_neutral_even_on_easy_free_fall_and_rail()
        {
            // GDD K-18: "Gölge açılmamış bir `?` hücresine değiyorsa bütün zorluklarda doğruluk nötrdür (… doğru/hatalı sesi de çalmaz)"
            var s = Init(new LevelSpec
            {
                Difficulty = "easy",
                Wall = Wall(3, Gap(1, 1)),
                Plan = new[] { "??", "WY" },
                Hidden = Repeat1,
                Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("B1_0", "Y", 1, 0), Pc("B1_0", "W", 5, 1)),
            });
            ToSite(s, 0, 6, 0);
            ToSite(s, 1, 7, 0);
            var free = Gravity.ComputeFall(s, 2, N(6, 8));
            Assert.AreEqual(A(6, 1), free.Landing);
            Assert.IsTrue(free.Verdict.Ok);
            Assert.IsTrue(free.TouchesHidden);
            Assert.AreEqual(NeutralShadow, Gravity.ShadowInfo(free, "easy"));
            var rail = Gravity.ComputeFall(s, 2, R(0, 6, 1));
            Assert.IsTrue(rail.TouchesHidden);
            Assert.AreEqual(NeutralShadow, Gravity.ShadowInfo(rail, "easy"));
            // the real placement is still correct and opens the cell (K-32)
            var o = Placement.SettlePlacement(s, 2, free.Landing, free.Verdict);
            Assert.AreEqual(PlacementOutcome.KindCorrect, o.Kind);
            CollectionAssert.AreEqual(new[] { new RevealedCell(6, 1, "W") }, o.Revealed);
        }

        [Test(Description = "K-18 a neutral shadow drops window/colour but keeps support with its cells (K-34 hook 2 order on easy; Faz 2R: no `debris`)")]
        public void K18_a_neutral_shadow_drops_window_colour_but_keeps_support_with_its_cells()
        {
            // plan (top → bottom) WW / W. / WW: (7,1) is `.`; (6,0) filled; R debris O4 outside the plan at (6,4)
            var s = Init(new LevelSpec
            {
                Difficulty = "hard",
                Plan = new[] { "WW", "W.", "WW" },
                Pieces = Ps(Pc("B1_0", "W", 0, 0)),
                Debris = new[] { new D("O4_0", "R", 6, 4) },
            });
            ToSite(s, 0, 6, 0);
            var fall = Gravity.ComputeFall(s, 1, N(6, 8));
            Assert.AreEqual(A(6, 1), fall.Landing);
            Assert.AreEqual(Wrong(Reasons(VerdictReasons.Window, VerdictReasons.Color, VerdictReasons.Support), Site(7, 0)), fall.Verdict);
            Assert.AreEqual(Shadow(ShadowInfo.ToneNeutral, Reasons(VerdictReasons.Support), Missing(Site(7, 0)), false), Gravity.ShadowInfo(fall, s.Lvl.Difficulty));
            Assert.AreEqual(Shadow(ShadowInfo.ToneWrong, Reasons(VerdictReasons.Window, VerdictReasons.Color, VerdictReasons.Support), Missing(Site(7, 0)), false),
                Gravity.ShadowInfo(fall, "easy"));
        }

        // --- K-19 normal gravity / K-11 landing --------------------------------------------------------------------------------

        [Test(Description = "K-19 without steering the landing, distance, path and verdict do not depend on the gravity setting")]
        public void K19_without_steering_the_landing_distance_path_and_verdict_do_not_depend_on_the_gravity_setting()
        {
            // GDD K-19: "İniş satırı, cam kırılması ve doğrulama hızdan bağımsızdır." Only G-L adds steering.
            var results = new[] { "low", "normal", "high" }.Select(build =>
            {
                var s = Init(new LevelSpec { Gravity = new GravitySpec { Build = build }, Plan = WW(4), Pieces = Ps(Pc("D2_0", "W", 0, 0), Pc("O4_0", "W", 2, 0)) });
                ToSite(s, 0, 6, 0);
                return Gravity.ComputeFall(s, 1, N(6, 8));
            }).ToArray();
            Assert.AreEqual(A(6, 2), results[0].Landing);
            Assert.AreEqual(6, results[0].Distance);
            CollectionAssert.AreEqual(new[] { A(6, 8), A(6, 2) }, results[0].Path);
            Assert.AreEqual(Wrong(Reasons(VerdictReasons.Support), Site(7, 0), Site(7, 1)), results[0].Verdict);
            Assert.IsNull(results[0].Steered);
            Assert.AreEqual(results[0], results[1]);
            Assert.AreEqual(results[0], results[2]);
        }

        [Test(Description = "K-17 a release over a full column lands in the crane rows (d = 0): outside, and a mortar block does not stick there (E-08)")]
        public void K17_a_release_over_a_full_column_lands_in_the_crane_rows_outside_and_a_mortar_block_does_not_stick_there()
        {
            // GDD K-17 / OBSTACLES Y8: "Bir hücresi plan dışındaysa normal geri seker"; K-11: "Blok havada asılı kalamaz"
            var s = Init(new LevelSpec { Plan = WW(8), Pieces = Ps(Pc("I4_0", "W", 0, 0), Pc("I4_0", "W", 1, 0), Pc("B1_0", "W", 2, 7, new[] { "mortar" })) });
            ToSite(s, 0, 6, 0);
            ToSite(s, 1, 6, 4);
            var start = Placement.PiecePlace(s, 2);
            var fall = Gravity.ComputeFall(s, 2, N(6, 8));
            Assert.AreEqual(A(6, 8), fall.Landing);
            Assert.AreEqual(0, fall.Distance);
            Assert.AreEqual(Wrong(Reasons(VerdictReasons.Outside)), fall.Verdict);
            var o = Placement.SettlePlacement(s, 2, fall.Landing, fall.Verdict, new SettleOptions(start, Mortar));
            Assert.AreEqual(PlacementOutcome.KindBounced, o.Kind);
            Assert.AreEqual(new ReturnTarget(1, YardP(2, 7), null), o.Target);
            Assert.IsFalse(HasFlag(s, 2, FlagBit.Stuck));
            Assert.AreEqual(3, YardOcc(s, 2, 7));
            ExpectConsistent(s);
        }

        // --- K-17 bounce-back target ----------------------------------------------------------------------------------------

        /// <summary>GDD K-17 step 2 order, written from the text: xs = 0 … 6 − w by |x − start x|, ties: larger x (nearer the wall).</summary>
        private static int[] GddColumnOrder(int startX, int w)
        {
            var xs = new List<int>();
            for (int c = 0; c <= 6 - w; c++) xs.Add(c);
            xs.Sort((a, b) =>
            {
                int d = Math.Abs(a - startX) - Math.Abs(b - startX);
                return d != 0 ? d : b - a;
            });
            return xs.ToArray();
        }

        /// <summary>Step-by-step drop from y = 10 − h over an occupancy set; null when a landed cell would be above y = 7.</summary>
        private static int? NaiveDrop(HashSet<string> occupied, string shape, int x)
        {
            var def = Shapes.ShapeById(shape);
            bool Free(int y) => def.Cells.All(c => !occupied.Contains((x + c.X) + "," + (y + c.Y)));
            int yy = 10 - def.H;
            if (!Free(yy)) return null;
            while (yy > 0 && Free(yy - 1)) yy--;
            return yy + def.H <= 8 ? yy : (int?)null;
        }

        [Test(Description = "K-17 step 2 equals a step-by-step drop from y = 10 − h on random yards (crates support, holes stay empty)")]
        public void K17_step_2_equals_a_step_by_step_drop_from_y_10_minus_h_on_random_yards()
        {
            var shapes = new[] { "B1_0", "D2_0", "D2_90", "I3_0", "O4_0", "C3_0", "C3_90", "C3_180", "C3_270", "L4_0", "J4_0", "T4_0" };
            var rng = new Mulberry32(17_2026);
            int step2 = 0;
            int step3 = 0;
            for (int round = 0; round < 80; round++)
            {
                var occupied = new HashSet<string>();
                var pieces = new List<P>();
                var obstacles = new List<Json.JsonValue>();
                for (int x = 0; x < 6; x++)
                {
                    int top = rng.NextInt(9);
                    for (int y = 0; y < top; y++)
                    {
                        if (rng.NextInt(4) == 0) continue; // a hole under an overhang
                        occupied.Add(x + "," + y);
                        if (obstacles.Count < 3 && rng.NextInt(7) == 0) obstacles.Add(Crate(x, y, 1));
                        else pieces.Add(Pc("B1_0", "W", x, y));
                    }
                }
                if (pieces.Count == 0) continue;
                var s = Init(new LevelSpec
                {
                    Plan = new[] { "WW" },
                    Pieces = pieces.ToArray(),
                    Obstacles = obstacles.ToArray(),
                    Batches = new[] { new BatchSpec { ForSegment = 0, Pieces = shapes.Select(sh => Pc(sh, "G", 0, 8)).ToArray() } },
                });
                for (int x = 0; x < 6; x++)
                    for (int y = 0; y < 8; y++) Assert.AreEqual(occupied.Contains(x + "," + y), YardOcc(s, x, y) != 0);
                for (int i = 0; i < shapes.Length; i++)
                {
                    string sh = shapes[i];
                    int id = pieces.Count + i;
                    int w = Shapes.ShapeById(sh).W;
                    int h = Shapes.ShapeById(sh).H;
                    foreach (int startX in new[] { 6, 7 })
                    {
                        if (startX + w > 8) continue;
                        ReturnTarget? expected = null;
                        foreach (int c in GddColumnOrder(startX, w))
                        {
                            int? y = NaiveDrop(occupied, sh, c);
                            if (y == null) continue;
                            expected = new ReturnTarget(2, YardP(c, y.Value), A(c, 10 - h));
                            break;
                        }
                        var got = Placement.ReturnTarget(s, id, new ReturnOptions(skipStart: true, start: SiteP(startX, 0)));
                        if (got.Step == 3)
                        {
                            step3++;
                            Assert.IsNull(expected);
                            Assert.AreEqual(At.ZoneSite == got.To.Zone ? "" : "queue", got.To.Zone);
                        }
                        else
                        {
                            step2++;
                            Assert.AreEqual(expected, got);
                        }
                        for (int c = 0; c <= 6 - w; c++) Assert.AreEqual(NaiveDrop(occupied, sh, c) ?? -1, Placement.DropIntoYard(s, Shapes.ShapeById(sh), c));
                    }
                }
            }
            Assert.Greater(step2, 300);
            Assert.Greater(step3, 0);
        }

        [Test(Description = "K-17 step 2: a full near column is skipped, a crate supports, the hole under it stays empty; 2-high drop from y = 8")]
        public void K17_step_2_a_full_near_column_is_skipped_a_crate_supports_the_hole_under_it_stays_empty()
        {
            // column 5: crate (5,0) + blocks (5,1)…(5,6) → a D2_0 would end at (5,7)–(5,8) → not ≤ 7 → skipped
            // column 4: only a crate at (4,2) → D2_0 falls from (4,8) onto it → (4,3); (4,0)–(4,1) stay empty
            var pieces = new List<P>();
            for (int y = 1; y <= 6; y++) pieces.Add(Pc("B1_0", "W", 5, y));
            var s = Init(new LevelSpec
            {
                Plan = new[] { "WW" },
                Pieces = pieces.ToArray(),
                Obstacles = new[] { Crate(5, 0, 1), Crate(4, 2, 1) },
                Batches = new[] { new BatchSpec { ForSegment = 0, Pieces = Ps(Pc("D2_0", "G", 0, 8)) } },
            });
            int id = pieces.Count;
            var target = Placement.ReturnTarget(s, id, new ReturnOptions(skipStart: true, start: SiteP(6, 0)));
            Assert.AreEqual(new ReturnTarget(2, YardP(4, 3), A(4, 8)), target);
        }

        [Test(Description = "K-17 Y8: a stuck mortar block re-dropped on a correct cell locks; stuck clears and its old cell is freed")]
        public void K17_Y8_a_stuck_mortar_block_re_dropped_on_a_correct_cell_locks_stuck_clears_and_its_old_cell_is_freed()
        {
            // TECH §5.2 / GDD Y8 "Doğru yerleşirse normal kilitlenir"; plan (top → bottom) WW / RW → (6,0) = R
            var s = Init(new LevelSpec { Plan = new[] { "WW", "RW" }, Pieces = Ps(Pc("B1_0", "R", 0, 0, new[] { "mortar" })) });
            ToSite(s, 0, 7, 1, locked: false, stuck: true); // stuck on the W cell (7,1)
            Assert.AreEqual(0b10, WrongOccMask(s, 0, 1));
            var start = Placement.PiecePlace(s, 0);
            var fall = Gravity.ComputeFall(s, 0, N(6, 8));
            Assert.AreEqual(A(6, 0), fall.Landing);
            Assert.IsTrue(fall.Verdict.Ok);
            var o = Placement.SettlePlacement(s, 0, fall.Landing, fall.Verdict, new SettleOptions(start, Mortar));
            Assert.AreEqual(PlacementOutcome.KindCorrect, o.Kind);
            Assert.IsFalse(HasFlag(s, 0, FlagBit.Stuck));
            Assert.IsTrue(HasFlag(s, 0, FlagBit.Locked));
            CollectionAssert.AreEqual(new[] { 0, 0 }, new[] { WrongOccMask(s, 0, 0), WrongOccMask(s, 0, 1) });
            CollectionAssert.AreEqual(new[] { 0b1, 0 }, new[] { FilledMask(s, 0, 0), FilledMask(s, 0, 1) });
            Assert.AreEqual(0, SiteOcc(s, 0, 1, 1));
            CollectionAssert.AreEqual(new[] { Site(6, 1), Site(7, 0) }, Placement.BuildFront(s));
            ExpectConsistent(s);
        }

        [Test(Description = "E-24 a fresh mortar block falling into a `.` cell sticks (window): no front above it, segment incomplete (E-43)")]
        public void E24_a_fresh_mortar_block_falling_into_a_dot_cell_sticks_no_front_above_it_segment_incomplete()
        {
            var s = Init(new LevelSpec
            {
                Plan = new[] { "WW", "W.", "WW" },
                Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("B1_0", "W", 1, 0, new[] { "mortar" }), Pc("B1_0", "W", 2, 0)),
            });
            ToSite(s, 0, 7, 0);
            var fall = Gravity.ComputeFall(s, 1, N(7, 8));
            Assert.AreEqual(A(7, 1), fall.Landing);
            Assert.AreEqual(Wrong(Reasons(VerdictReasons.Window)), fall.Verdict);
            var o = Placement.SettlePlacement(s, 1, fall.Landing, fall.Verdict, new SettleOptions(rules: Mortar));
            Assert.AreEqual(PlacementOutcome.KindStuck, o.Kind);
            Assert.AreEqual(SiteP(7, 1), o.At);
            Assert.AreEqual(VerdictReasons.Window, o.Reason);
            CollectionAssert.IsEmpty(o.MissingSupport!);
            Assert.AreEqual(0b10, WrongOccMask(s, 0, 1));
            CollectionAssert.AreEqual(new[] { Site(6, 0) }, Placement.BuildFront(s));
            Assert.IsFalse(Placement.IsSegmentComplete(s, 0));
            // E-43: the next W block in column 7 lands on the mortar at (7,2) and is wrong for support at (7,1)
            var next = Gravity.ComputeFall(s, 2, N(7, 8));
            Assert.AreEqual(A(7, 2), next.Landing);
            Assert.AreEqual(Wrong(Reasons(VerdictReasons.Support), Site(7, 1)), next.Verdict);
            ExpectConsistent(s);
        }

        [Test(Description = "K-34 hook 4: a mortar block of the right colour sticks for support only; it is no \"doğru dolu\" for the front")]
        public void K34_hook_4_a_mortar_block_of_the_right_colour_sticks_for_support_only()
        {
            var s = Init(new LevelSpec { Plan = WW(2), Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("D2_90", "W", 1, 0, new[] { "mortar" })) });
            ToSite(s, 0, 6, 0);
            var fall = Gravity.ComputeFall(s, 1, N(6, 8));
            Assert.AreEqual(A(6, 1), fall.Landing);
            Assert.AreEqual(Wrong(Reasons(VerdictReasons.Support), Site(7, 0)), fall.Verdict);
            var o = Placement.SettlePlacement(s, 1, fall.Landing, fall.Verdict, new SettleOptions(rules: Mortar));
            Assert.AreEqual(PlacementOutcome.KindStuck, o.Kind);
            Assert.AreEqual(SiteP(6, 1), o.At);
            Assert.AreEqual(VerdictReasons.Support, o.Reason);
            CollectionAssert.AreEqual(Missing(Site(7, 0)), o.MissingSupport);
            CollectionAssert.AreEqual(new[] { 0b10, 0b10 }, new[] { WrongOccMask(s, 0, 0), WrongOccMask(s, 0, 1) });
            CollectionAssert.AreEqual(new[] { 0b1, 0 }, new[] { FilledMask(s, 0, 0), FilledMask(s, 0, 1) });
            // column 6: the lowest unfilled cell (6,1) holds the mortar → no front; column 7: (7,0) is empty → front
            CollectionAssert.AreEqual(new[] { Site(7, 0) }, Placement.BuildFront(s));
            Assert.IsFalse(Placement.IsSegmentComplete(s, 0));
            ExpectConsistent(s);
        }

        [Test(Description = "K-17 step 2: a cement bag supports the drop, a hidden screw neither supports nor blocks it (K-08)")]
        public void K17_step_2_a_cement_bag_supports_the_drop_a_hidden_screw_neither_supports_nor_blocks_it()
        {
            var s = Init(new LevelSpec { Plan = new[] { "WW" }, Pieces = Ps(Pc("B1_0", "W", 0, 0)), Obstacles = new[] { Bag(2, 0), Screw(3, 0) } });
            Assert.AreEqual(1, Placement.DropIntoYard(s, Shapes.ShapeById("B1_0"), 2));
            Assert.AreEqual(0, Placement.DropIntoYard(s, Shapes.ShapeById("B1_0"), 3));
            Assert.AreEqual(1, Placement.DropIntoYard(s, Shapes.ShapeById("O4_0"), 2));
            Assert.AreEqual(0, Placement.DropIntoYard(s, Shapes.ShapeById("O4_0"), 3));
        }

        // --- independent oracle written from the GDD text (K-16, K-34 incl. hook 1 and hook 2) --------------------------------

        private const string Locked = "locked";
        private const string Stuck = "stuck";
        private const string Trowel = "trowel";
        private const string Debris = "debris";

        private sealed class OracleBoard
        {
            /// <summary>`Plan[sx][sy]`: colour, ".", or null (outside the plan area).</summary>
            public readonly string?[][] Plan;
            /// <summary>`Occ[sx][sy]` for plan rows 0–7.</summary>
            public readonly string?[][] Occ;

            public OracleBoard(string?[][] plan, string?[][] occ)
            {
                Plan = plan;
                Occ = occ;
            }
        }

        /// <summary>GDD K-16 (1)–(3) + K-34 + hook 2, written from the rule text (the moving block is not debris and not on the site).</summary>
        private static Verdict OracleVerdict(OracleBoard b, BoardCell[] cells, string color)
        {
            bool outside = false;
            bool window = false;
            bool wrongColor = false;
            var low = new[] { 99, 99 };
            foreach (var c in cells)
            {
                int sx = c.X - 6;
                if (sx < 0 || sx > 1 || c.Y < 0)
                {
                    outside = true;
                    continue;
                }
                low[sx] = Math.Min(low[sx], c.Y);
                string? ch = c.Y < 8 ? b.Plan[sx][c.Y] : null;
                if (ch == null) outside = true;
                else if (ch == ".") window = true;
                else if (ch != color) wrongColor = true;
            }
            var missing = new List<At>();
            for (int sx = 0; sx < 2; sx++)
            {
                int r = low[sx];
                if (r == 99) continue;
                for (int sy = 0; sy < Math.Min(r, 8); sy++)
                {
                    string? ch = b.Plan[sx][sy];
                    string? o = b.Occ[sx][sy];
                    if (ch == null) continue;
                    if (ch == ".")
                    {
                        // "`.` hücreleri bu kuralda yalnızca boşken dolu sayılır"
                        if (o == Stuck || o == Debris) missing.Add(Site(6 + sx, sy));
                    }
                    else if (o != Locked && o != Trowel)
                    {
                        // "plan satırları 0 … r−1 içindeki `.` olmayan her hücre doğru dolu olmalıdır"
                        missing.Add(Site(6 + sx, sy));
                    }
                }
            }
            var reasons = new List<string>();
            if (outside) reasons.Add(VerdictReasons.Outside);
            if (window) reasons.Add(VerdictReasons.Window);
            if (wrongColor) reasons.Add(VerdictReasons.Color);
            if (missing.Count > 0) reasons.Add(VerdictReasons.Support);
            return new Verdict(reasons.Count == 0, reasons.ToArray(), reasons.Count == 0 ? new At[0] : missing.ToArray());
        }

        /// <summary>GDD K-34 hook 1, written from the rule text.</summary>
        private static At[] OracleFront(OracleBoard b)
        {
            var o = new List<At>();
            for (int sx = 0; sx < 2; sx++)
            {
                int r = -1;
                for (int sy = 0; sy < 8; sy++)
                {
                    string? ch = b.Plan[sx][sy];
                    string? oc = b.Occ[sx][sy];
                    if (ch != null && ch != "." && oc != Locked && oc != Trowel)
                    {
                        r = sy;
                        break;
                    }
                }
                if (r < 0 || b.Occ[sx][r] != null) continue; // complete column, or the cell is not empty
                bool below = true;
                for (int sy = 0; sy < r; sy++)
                    if (b.Plan[sx][sy] == "." && b.Occ[sx][sy] != null) below = false;
                if (below) o.Add(Site(6 + sx, r));
            }
            return o.ToArray();
        }

        [Test(Description = "K-34 verdict, hook-2 reasons/missingSupport, hook-1 front and the K-11 fall match the GDD text on random boards")]
        public void K34_verdict_hook_2_reasons_missingSupport_hook_1_front_and_the_K11_fall_match_the_GDD_text_on_random_boards()
        {
            var moving = Shapes.All.Where(d => d.Id == d.Canonical && d.W <= 2 && d.H <= 4 && !d.Heavy).Select(d => d.Id).ToArray();
            var colors = new[] { "W", "Y", "R" };
            var rng = new Mulberry32(34_1617);
            int verdicts = 0;
            int falls = 0;
            var seenReasons = new HashSet<string>();
            for (int round = 0; round < 250; round++)
            {
                int h = 1 + rng.NextInt(8);
                var plan = new[] { new string?[8], new string?[8] };
                var occ = new[] { new string?[8], new string?[8] };
                for (int sx = 0; sx < 2; sx++)
                    for (int sy = 0; sy < 8; sy++)
                    {
                        plan[sx][sy] = sy >= h ? null : rng.NextInt(5) == 0 ? "." : colors[rng.NextInt(2)];
                        occ[sx][sy] = null;
                    }
                if (!plan.Any(col => col.Any(ch => ch != null && ch != "."))) plan[0][0] = "W";
                var rows = new List<string>();
                for (int sy = h - 1; sy >= 0; sy--) rows.Add(plan[0][sy] + plan[1][sy]);
                // occupants: locked / trowel on colour cells, stuck on plan-area cells, debris anywhere in the 2 × 8 area
                var blocks = new List<(int x, int y, string kind, string color)>();
                var debris = new List<D>();
                var trowels = new List<(int sx, int sy)>();
                for (int sx = 0; sx < 2; sx++)
                    for (int sy = 0; sy < 8; sy++)
                    {
                        string? ch = plan[sx][sy];
                        int roll = rng.NextInt(10);
                        if (ch != null && ch != "." && roll < 3)
                        {
                            occ[sx][sy] = Locked;
                            blocks.Add((6 + sx, sy, Locked, ch));
                        }
                        else if (ch != null && ch != "." && roll == 3)
                        {
                            occ[sx][sy] = Trowel;
                            trowels.Add((sx, sy));
                        }
                        else if (ch != null && roll == 4)
                        {
                            occ[sx][sy] = Stuck;
                            blocks.Add((6 + sx, sy, Stuck, "R"));
                        }
                        else if (roll == 5 && debris.Count < 3)
                        {
                            occ[sx][sy] = Debris;
                            debris.Add(new D("B1_0", "R", 6 + sx, sy));
                        }
                    }
                string shape = moving[rng.NextInt(moving.Length)];
                string color = colors[rng.NextInt(2)];
                var pieces = new List<P> { Pc(shape, color, 2, 4) };
                for (int i = 0; i < blocks.Count; i++) pieces.Add(Pc("B1_0", blocks[i].color, i % 6, i / 6));
                var s = Init(new LevelSpec { Plan = rows.ToArray(), Pieces = pieces.ToArray(), Debris = debris.ToArray() });
                for (int i = 0; i < blocks.Count; i++) ToSite(s, 1 + i, blocks[i].x, blocks[i].y, locked: blocks[i].kind == Locked, stuck: blocks[i].kind == Stuck);
                foreach (var tr in trowels) SetSiteOcc(s, 0, tr.sx, tr.sy, SiteTrowel);
                Grid.RefreshSiteMasks(s, 0);
                var board = new OracleBoard(plan, occ);
                CollectionAssert.AreEqual(OracleFront(board), Placement.BuildFront(s));

                var def = Shapes.ShapeById(shape);
                bool FreeAt(int ix, int iy) => Movement.BlockCells(def, ix, iy).All(c => c.Y >= 8 || occ[c.X - 6][c.Y] == null);
                for (int k = 0; k < 12; k++)
                {
                    int ix = 6 + rng.NextInt(3 - def.W);
                    int iy = rng.NextInt(11 - def.H);
                    if (!FreeAt(ix, iy)) continue;
                    var v = Placement.IsCorrectPlacement(s, 0, Movement.BlockCells(def, ix, iy));
                    var want = OracleVerdict(board, Movement.BlockCells(def, ix, iy), color);
                    Assert.AreEqual(want, v, shape + " at (" + ix + "," + iy + ")");
                    foreach (var r in v.Reasons) seenReasons.Add(r);
                    if (v.Ok) seenReasons.Add("ok");
                    verdicts++;
                }
                // K-11 / K-18: a FREE release at the top of either column lands where a step-by-step fall stops
                for (int ix = 6; ix <= 8 - def.W; ix++)
                {
                    int top = 10 - def.H;
                    if (!FreeAt(ix, top)) continue;
                    int y = top;
                    while (y > 0 && FreeAt(ix, y - 1)) y--;
                    var fall = Gravity.ComputeFall(s, 0, N(ix, top));
                    Assert.AreEqual(A(ix, y), fall.Landing, shape + " column " + ix);
                    Assert.AreEqual(top - y, fall.Distance, shape + " column " + ix);
                    Assert.AreEqual(OracleVerdict(board, Movement.BlockCells(def, ix, y), color), fall.Verdict);
                    falls++;
                }
            }
            Assert.Greater(verdicts, 1000);
            Assert.Greater(falls, 200);
            CollectionAssert.AreEqual(new[] { "color", "ok", "outside", "support", "window" }, seenReasons.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        }

        // --- round 2: K-17 bounce-back search edges ----------------------------------------------------------------------------

        [Test(Description = "K-17 step 2 tie-break for a yard start: equal distance goes to the larger x (nearer the wall) first")]
        public void K17_step_2_tie_break_for_a_yard_start_equal_distance_goes_to_the_larger_x_first()
        {
            // GDD K-17 (2): "xs = 0 … 6 − w başlangıç x'ine uzaklığa göre sıralanır, eşitlikte duvara yakın (büyük x) önce"
            CollectionAssert.AreEqual(new[] { 2, 3, 1, 4, 0, 5 }, Placement.NearestColumnsFirst(Geo, 2, 1));
            CollectionAssert.AreEqual(new[] { 3, 4, 2, 1, 0 }, Placement.NearestColumnsFirst(Geo, 3, 2));
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5 }, Placement.NearestColumnsFirst(Geo, 0, 1));
            // site starts (TECH §5.2 / GDD K-17 exception): "w = 1: 5, 4, 3 …; w = 2: 4, 3 …"
            CollectionAssert.AreEqual(new[] { 5, 4, 3, 2, 1, 0 }, Placement.NearestColumnsFirst(Geo, 6, 1));
            CollectionAssert.AreEqual(new[] { 5, 4, 3, 2, 1, 0 }, Placement.NearestColumnsFirst(Geo, 7, 1));
            CollectionAssert.AreEqual(new[] { 4, 3, 2, 1, 0 }, Placement.NearestColumnsFirst(Geo, 6, 2));

            P[] Full(int x) => Ps(Pc("I4_0", "W", x, 0), Pc("I4_0", "W", x, 4));
            // B1 (piece 0) whose start (2,7) is taken by another block → step 1 fails, step 2 searches 2, 3, 1, 4, 0, 5
            ReturnTarget B1(params int[] fullCols)
            {
                var s = Init(new LevelSpec { Plan = new[] { "WW" }, Pieces = Ps(Pc("B1_0", "G", 0, 7)).Plus(fullCols.SelectMany(Full).ToArray()) });
                return Placement.ReturnTarget(s, 0, new ReturnOptions(start: YardP(2, 7)));
            }
            ReturnTarget Yard(int x, int y, int h) => new ReturnTarget(2, YardP(x, y), A(x, 10 - h));
            Assert.AreEqual(Yard(3, 0, 1), B1(2));
            Assert.AreEqual(Yard(1, 0, 1), B1(2, 3));
            Assert.AreEqual(Yard(4, 0, 1), B1(2, 3, 1));
            // 2, 3, 1, 4 full: 0 (distance 2) comes before 5 (distance 3); the held B1's own cell (0,7) does not count
            Assert.AreEqual(Yard(0, 0, 1), B1(2, 3, 1, 4));
            Assert.AreEqual(Yard(5, 0, 1), B1(2, 3, 1, 4, 0));
            // O4 (piece 0, held, at (0,6)) with start x = 3: 3 (cols 3–4), 4 (cols 4–5), 2 (cols 2–3), 1, 0
            ReturnTarget O4(params int[] fullCols)
            {
                var s = Init(new LevelSpec { Plan = new[] { "WW" }, Pieces = Ps(Pc("O4_0", "G", 0, 6)).Plus(fullCols.SelectMany(Full).ToArray()) });
                return Placement.ReturnTarget(s, 0, new ReturnOptions(start: YardP(3, 6)));
            }
            Assert.AreEqual(Yard(4, 0, 2), O4(3));
            // 3 and 5 full: x 3, 4 and 2 fail; x 1 covers columns 1–2 (the held block's own cells do not count)
            Assert.AreEqual(Yard(1, 0, 2), O4(3, 5));
        }

        [Test(Description = "K-17 exception: a broken stuck glass mortar D2_90 skips step 1, tries x 4 then 3 (w = 2), unsticks and reopens the front")]
        public void K17_exception_a_broken_stuck_glass_mortar_D2_90_skips_step_1_tries_x_4_then_3_unsticks_and_reopens_the_front()
        {
            // GDD K-17 "İstisnalar"; TECH §5.2: "ilk aday duvara en yakın sütundur (w = 1: 5, 4, 3 …; w = 2: 4, 3 …)"
            var s = Init(new LevelSpec
            {
                Plan = WW(3),
                Pieces = Ps(Pc("D2_0", "W", 0, 0), Pc("D2_0", "W", 1, 0), Pc("D2_90", "R", 2, 0, new[] { "glass", "mortar" }), Pc("I4_0", "W", 5, 0), Pc("I4_0", "W", 5, 4)),
            });
            ToSite(s, 0, 6, 0);
            ToSite(s, 1, 7, 0);
            ToSite(s, 2, 6, 2, locked: false, stuck: true);
            CollectionAssert.IsEmpty(Placement.BuildFront(s));
            var target = Placement.ReturnBrokenPiece(s, 2, Placement.PiecePlace(s, 2));
            // x 4 covers the full column 5 → a cell would end above y = 7; x 3 (columns 3–4) is empty
            Assert.AreEqual(new ReturnTarget(2, YardP(3, 0), A(3, 9)), target);
            Assert.AreEqual(YardP(3, 0), Placement.PiecePlace(s, 2));
            Assert.IsFalse(HasFlag(s, 2, FlagBit.Stuck));
            CollectionAssert.AreEqual(new[] { 0, 0 }, new[] { WrongOccMask(s, 0, 0), WrongOccMask(s, 0, 1) });
            CollectionAssert.AreEqual(new[] { 0, 0 }, new[] { SiteOcc(s, 0, 0, 2), SiteOcc(s, 0, 1, 2) });
            CollectionAssert.AreEqual(new[] { Site(6, 2), Site(7, 2) }, Placement.BuildFront(s));
            ExpectConsistent(s);
        }

        [Test(Description = "K-17 / Y8: a stuck mortar re-dropped wrong inside the plan sticks at the new cell; front and K-34 follow it")]
        public void K17_Y8_a_stuck_mortar_re_dropped_wrong_inside_the_plan_sticks_at_the_new_cell_front_and_K34_follow_it()
        {
            // GDD K-17: "Plan içinde başka bir hatalı konuma inerse orada yeniden yapışır (Y8)"
            var s = Init(new LevelSpec { Plan = WW(2), Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("B1_0", "R", 1, 0, new[] { "mortar" }), Pc("B1_0", "W", 2, 0)) });
            ToSite(s, 0, 6, 0);
            ToSite(s, 1, 6, 1, locked: false, stuck: true);
            CollectionAssert.AreEqual(new[] { Site(7, 0) }, Placement.BuildFront(s));
            var start = Placement.PiecePlace(s, 1);
            var fall = Gravity.ComputeFall(s, 1, N(7, 8));
            Assert.AreEqual(A(7, 0), fall.Landing);
            Assert.AreEqual(Wrong(Reasons(VerdictReasons.Color)), fall.Verdict);
            var o = Placement.SettlePlacement(s, 1, fall.Landing, fall.Verdict, new SettleOptions(start, Mortar));
            Assert.AreEqual(PlacementOutcome.KindStuck, o.Kind);
            Assert.AreEqual(SiteP(7, 0), o.At);
            Assert.AreEqual(VerdictReasons.Color, o.Reason);
            CollectionAssert.IsEmpty(o.MissingSupport!);
            Assert.IsTrue(HasFlag(s, 1, FlagBit.Stuck));
            Assert.AreEqual(0, SiteOcc(s, 0, 0, 1));
            CollectionAssert.AreEqual(new[] { 0, 0b1 }, new[] { WrongOccMask(s, 0, 0), WrongOccMask(s, 0, 1) });
            // column 6: (6,1) is empty again → front; column 7: the mortar sits on its lowest cell → no front
            CollectionAssert.AreEqual(new[] { Site(6, 1) }, Placement.BuildFront(s));
            Assert.IsTrue(Gravity.ComputeFall(s, 2, N(6, 8)).Verdict.Ok);
            Assert.AreEqual(Wrong(Reasons(VerdictReasons.Support), Site(7, 0)), Placement.IsCorrectPlacement(s, 2, CellsOf("B1_0", 7, 1)));
            ExpectConsistent(s);
        }

        // --- round 2: Y8 / K-34 edges --------------------------------------------------------------------------------------------

        [Test(Description = "E-08 a mortar block with one cell above the plan top (still inside the 2 × 8 site) bounces instead of sticking")]
        public void E08_a_mortar_block_with_one_cell_above_the_plan_top_bounces_instead_of_sticking()
        {
            // OBSTACLES Y8: "bütün hücreleri plan alanında (renkli, `?` ya da `.` hücre) ise … yapışır. Bir hücresi plan dışındaysa normal geri seker"
            var s = Init(new LevelSpec
            {
                Plan = WW(2),
                Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("D2_0", "R", 1, 0, new[] { "mortar" }), Pc("B1_0", "R", 2, 0, new[] { "mortar" })),
            });
            ToSite(s, 0, 6, 0);
            var tall = Gravity.ComputeFall(s, 1, N(6, 8));
            Assert.AreEqual(A(6, 1), tall.Landing); // cells (6,1) W and (6,2) above h = 2
            Assert.AreEqual(Wrong(Reasons(VerdictReasons.Outside, VerdictReasons.Color)), tall.Verdict);
            Assert.IsFalse(Placement.AllCellsInPlanArea(s, tall.Cells));
            var o = Placement.SettlePlacement(s, 1, tall.Landing, tall.Verdict, new SettleOptions(rules: Mortar));
            Assert.AreEqual(PlacementOutcome.KindBounced, o.Kind);
            Assert.AreEqual(new ReturnTarget(1, YardP(1, 0), null), o.Target);
            Assert.IsFalse(HasFlag(s, 1, FlagBit.Stuck));
            CollectionAssert.AreEqual(new[] { 0, 0 }, new[] { WrongOccMask(s, 0, 0), WrongOccMask(s, 0, 1) });
            // the same release with a 1-high mortar stays inside the plan → it sticks at (6,1)
            var shortFall = Gravity.ComputeFall(s, 2, N(6, 8));
            Assert.AreEqual(A(6, 1), shortFall.Landing);
            Assert.AreEqual(PlacementOutcome.KindStuck, Placement.SettlePlacement(s, 2, shortFall.Landing, shortFall.Verdict, new SettleOptions(rules: Mortar)).Kind);
            Assert.AreEqual(0b10, WrongOccMask(s, 0, 0));
            ExpectConsistent(s);
        }

        [Test(Description = "N32 / K-34 a mortar stuck on a `?` cell in the wrong colour opens nothing; that cell is missing support and the shadow above stays neutral")]
        public void N32_K34_a_mortar_stuck_on_a_hidden_cell_in_the_wrong_colour_opens_nothing()
        {
            // OBSTACLES N32: "Yanlış renkle `?` hücresine yapışan harç hücreyi açmaz"; K-32 repeat p = 1 → every row = W Y
            var s = Init(new LevelSpec
            {
                Plan = new[] { "??", "??", "WY" },
                Hidden = Repeat1,
                Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("B1_0", "Y", 1, 0), Pc("B1_0", "R", 2, 0, new[] { "mortar" }), Pc("B1_0", "W", 3, 0)),
            });
            ToSite(s, 0, 6, 0);
            ToSite(s, 1, 7, 0);
            var m = Gravity.ComputeFall(s, 2, N(6, 8));
            Assert.AreEqual(A(6, 1), m.Landing);
            Assert.IsTrue(m.TouchesHidden);
            Assert.AreEqual(Wrong(Reasons(VerdictReasons.Color)), m.Verdict);
            Assert.AreEqual(NeutralShadow, Gravity.ShadowInfo(m, "easy"));
            Assert.AreEqual(PlacementOutcome.KindStuck, Placement.SettlePlacement(s, 2, m.Landing, m.Verdict, new SettleOptions(rules: Mortar)).Kind);
            Assert.AreEqual(0, RevealedMask(s, 0));
            var w = Gravity.ComputeFall(s, 3, N(6, 8));
            Assert.AreEqual(A(6, 2), w.Landing);
            Assert.IsTrue(w.TouchesHidden);
            Assert.AreEqual(Wrong(Reasons(VerdictReasons.Support), Site(6, 1)), w.Verdict);
            Assert.AreEqual(Shadow(ShadowInfo.ToneNeutral, Reasons(VerdictReasons.Support), Missing(Site(6, 1)), false), Gravity.ShadowInfo(w, "easy"));
            CollectionAssert.AreEqual(new[] { Site(7, 1) }, Placement.BuildFront(s));
            Assert.IsFalse(Placement.IsSegmentComplete(s, 0));
            ExpectConsistent(s);
        }

        [Test(Description = "K-34 a stuck mortar lifted from a `.` cell does not block itself: its own rail placement above is correct, another block is not (E-43)")]
        public void K34_a_stuck_mortar_lifted_from_a_dot_cell_does_not_block_itself()
        {
            // K-34: "`.` hücreleri bu kuralda yalnızca boşken dolu sayılır" — once lifted, (7,1) is empty
            var s = Init(new LevelSpec
            {
                Wall = Wall(4, Gap(2, 1)),
                Plan = new[] { "WW", "W.", "WW" },
                Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("B1_0", "W", 1, 0, new[] { "mortar" }), Pc("B1_0", "W", 2, 0)),
            });
            ToSite(s, 0, 7, 0);
            ToSite(s, 1, 7, 1, locked: false, stuck: true);
            CollectionAssert.AreEqual(new[] { Site(6, 0) }, Placement.BuildFront(s));
            var own = Gravity.ComputeFall(s, 1, R(0, 7, 2));
            Assert.AreEqual(OkV, own.Verdict);
            var other = Gravity.ComputeFall(s, 2, R(0, 7, 2));
            Assert.AreEqual(Wrong(Reasons(VerdictReasons.Support), Site(7, 1)), other.Verdict);
            // FREE in column 7: the mortar falls through its own old cell back into the `.`; another block lands on top of it
            var ownFree = Gravity.ComputeFall(s, 1, N(7, 8));
            Assert.AreEqual(A(7, 1), ownFree.Landing);
            Assert.AreEqual(Wrong(Reasons(VerdictReasons.Window)), ownFree.Verdict);
            var otherFree = Gravity.ComputeFall(s, 2, N(7, 8));
            Assert.AreEqual(A(7, 2), otherFree.Landing);
            Assert.AreEqual(Wrong(Reasons(VerdictReasons.Support), Site(7, 1)), otherFree.Verdict);
            // the mortar's own rail placement locks it at (7,2); (7,1) is an empty `.` again and column 7 is complete
            Assert.AreEqual(PlacementOutcome.KindCorrect, Placement.SettlePlacement(s, 1, own.Landing, own.Verdict, new SettleOptions(rules: Mortar)).Kind);
            Assert.IsFalse(HasFlag(s, 1, FlagBit.Stuck));
            Assert.AreEqual(0, SiteOcc(s, 0, 1, 1));
            CollectionAssert.AreEqual(new[] { 0, 0b101 }, new[] { WrongOccMask(s, 0, 1), FilledMask(s, 0, 1) });
            CollectionAssert.AreEqual(new[] { Site(6, 0) }, Placement.BuildFront(s));
            ExpectConsistent(s);
        }

        [Test(Description = "K-18 / K-34 hook 2 on an unrevealed `?` landing (easy): the colour is hidden but `support` and its cells still show (E-20)")]
        public void K18_K34_hook_2_on_an_unrevealed_hidden_landing_the_colour_is_hidden_but_support_and_its_cells_still_show()
        {
            // K-34 kanca 2: "`support` nedeni gizli bilgi taşımadığı için gölge nötr olduğunda (Zor, `?`) da gösterilebilir"
            var s = Init(new LevelSpec
            {
                Difficulty = "easy",
                Plan = new[] { "??", "WW" },
                Hidden = Repeat1,
                Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("D2_90", "Y", 1, 0), Pc("D2_90", "W", 3, 0)),
            });
            ToSite(s, 0, 6, 0);
            var y = Gravity.ComputeFall(s, 1, N(6, 8));
            Assert.AreEqual(A(6, 1), y.Landing);
            Assert.IsTrue(y.TouchesHidden);
            Assert.AreEqual(Wrong(Reasons(VerdictReasons.Color, VerdictReasons.Support), Site(7, 0)), y.Verdict);
            var neutralSupport = Shadow(ShadowInfo.ToneNeutral, Reasons(VerdictReasons.Support), Missing(Site(7, 0)), false);
            Assert.AreEqual(neutralSupport, Gravity.ShadowInfo(y, "easy"));
            Assert.AreEqual(neutralSupport, Gravity.ShadowInfo(y, "normal"));
            var w = Gravity.ComputeFall(s, 2, N(6, 8));
            Assert.AreEqual(Wrong(Reasons(VerdictReasons.Support), Site(7, 0)), w.Verdict);
            Assert.AreEqual(neutralSupport, Gravity.ShadowInfo(w, "easy"));
            // E-20: the real placement of the Y block is wrong (primary reason colour) and opens nothing
            var o = Placement.SettlePlacement(s, 1, y.Landing, y.Verdict);
            Assert.AreEqual(PlacementOutcome.KindBounced, o.Kind);
            Assert.AreEqual(VerdictReasons.Color, o.Reason);
            Assert.AreEqual(0, RevealedMask(s, 0));
            ExpectConsistent(s);
        }

        [Test(Description = "K-19 normal gravity: computeFall ignores a steer option; landing, path and verdict equal the unsteered fall")]
        public void K19_normal_gravity_computeFall_ignores_a_steer_option()
        {
            // K-19 tablo: `normal` → Yönlendirme "Yok"
            var s = Init(new LevelSpec { Plan = WW(2), Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("B1_0", "W", 1, 0)) });
            ToSite(s, 0, 6, 0);
            var plain = Gravity.ComputeFall(s, 1, N(7, 8));
            var asked = Gravity.ComputeFall(s, 1, N(7, 8), new FallOptions(new Steer(-1, 4)));
            Assert.AreEqual(plain, asked);
            Assert.IsNull(asked.Steered);
            Assert.AreEqual(A(7, 0), asked.Landing);
            CollectionAssert.AreEqual(new[] { A(7, 8), A(7, 0) }, asked.Path);
        }

        // --- round 3: landing edges (K-11, K-17, K-34, Y8) ------------------------------------------------------------------------

        [Test(Description = "K-11 K-34 a 2-wide block resting in the crane rows on a full column is outside AND breaks support in the empty column (r = 8)")]
        public void K11_K34_a_2_wide_block_resting_in_the_crane_rows_on_a_full_column_is_outside_AND_breaks_support_in_the_empty_column()
        {
            // K-11: "Blok havada asılı kalamaz" → (6,8), d = 0; K-34: column 7, r = 8 → plan rows 0 … 7 must be filled
            var s = Init(new LevelSpec { Moves = 10, Plan = WW(8), Pieces = Ps(Pc("I4_0", "W", 0, 0), Pc("I4_0", "W", 1, 0), Pc("D2_90", "W", 2, 0)) });
            ToSite(s, 0, 6, 0);
            ToSite(s, 1, 6, 4);
            var fall = Gravity.ComputeFall(s, 2, N(6, 8));
            Assert.AreEqual(A(6, 8), fall.Landing);
            Assert.AreEqual(0, fall.Distance);
            CollectionAssert.AreEqual(new[] { A(6, 8) }, fall.Path);
            var column7 = Enumerable.Range(0, 8).Select(y => Site(7, y)).ToArray();
            Assert.AreEqual(Wrong(Reasons(VerdictReasons.Outside, VerdictReasons.Support), column7), fall.Verdict);
            var o = Placement.SettlePlacement(s, 2, fall.Landing, fall.Verdict);
            Assert.AreEqual(PlacementOutcome.KindBounced, o.Kind);
            Assert.AreEqual(A(6, 8), o.Landing);
            Assert.AreEqual(YardP(2, 0), o.Target!.To);
            Assert.AreEqual(VerdictReasons.Outside, o.Reason);
            CollectionAssert.AreEqual(column7, o.MissingSupport);
            Assert.AreEqual(YardP(2, 0), Placement.PiecePlace(s, 2));
            CollectionAssert.AreEqual(new[] { Site(7, 0) }, Placement.BuildFront(s));
            ExpectConsistent(s);
        }

        [Test(Description = "K-17 Y8 a stuck mortar re-dropped so that it lands on exactly its own cells is still wrong and re-sticks there; masks and front are unchanged")]
        public void K17_Y8_a_stuck_mortar_re_dropped_so_that_it_lands_on_exactly_its_own_cells_is_still_wrong_and_re_sticks_there()
        {
            // K-17: "Plan içinde başka bir hatalı konuma inerse orada yeniden yapışır (Y8)" — the same cells are inside the plan too
            var s = Init(new LevelSpec { Plan = WW(2), Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("B1_0", "R", 1, 0, new[] { "mortar" })) });
            ToSite(s, 0, 6, 0);
            ToSite(s, 1, 6, 1, locked: false, stuck: true);
            int[] Masks() => new[] { FilledMask(s, 0, 0), FilledMask(s, 0, 1), WrongOccMask(s, 0, 0), WrongOccMask(s, 0, 1) };
            var before = Masks();
            CollectionAssert.AreEqual(new[] { 0b1, 0, 0b10, 0 }, before);
            CollectionAssert.AreEqual(new[] { Site(7, 0) }, Placement.BuildFront(s));
            var start = Placement.PiecePlace(s, 1);
            var fall = Gravity.ComputeFall(s, 1, N(6, 8));
            Assert.AreEqual(A(6, 1), fall.Landing);
            Assert.AreEqual(Wrong(Reasons(VerdictReasons.Color)), fall.Verdict);
            var o = Placement.SettlePlacement(s, 1, fall.Landing, fall.Verdict, new SettleOptions(start, Mortar));
            Assert.AreEqual(PlacementOutcome.KindStuck, o.Kind);
            Assert.AreEqual(SiteP(6, 1), o.At);
            Assert.AreEqual(VerdictReasons.Color, o.Reason);
            CollectionAssert.IsEmpty(o.MissingSupport!);
            Assert.IsTrue(HasFlag(s, 1, FlagBit.Stuck));
            Assert.IsFalse(HasFlag(s, 1, FlagBit.Locked));
            Assert.AreEqual(2, SiteOcc(s, 0, 0, 1));
            CollectionAssert.AreEqual(before, Masks());
            CollectionAssert.AreEqual(new[] { Site(7, 0) }, Placement.BuildFront(s));
            ExpectConsistent(s);
        }

        [Test(Description = "K-17 Y8 a stuck W mortar whose missing base was filled by rail falls back onto its own cells and now locks (K-14): wrongOcc → filled")]
        public void K17_Y8_a_stuck_W_mortar_whose_missing_base_was_filled_by_rail_falls_back_onto_its_own_cells_and_now_locks()
        {
            // OBSTACLES Y8: "Doğru yerleşirse normal kilitlenir"; K-34: the block's own old cells are not a wrong object below it
            var s = Init(new LevelSpec { Plan = WW(3), Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("D2_0", "W", 1, 0, new[] { "mortar" })) });
            ToSite(s, 1, 6, 1, locked: false, stuck: true); // stuck for support: (6,0) was empty
            Assert.AreEqual(Wrong(Reasons(VerdictReasons.Support), Site(6, 0)), Placement.IsCorrectPlacement(s, 1, CellsOf("D2_0", 6, 1)));
            ToSite(s, 0, 6, 0); // (6,0) filled later through a row-0 gap (K-34 example 2 path)
            CollectionAssert.AreEqual(new[] { 0b1, 0b110 }, new[] { FilledMask(s, 0, 0), WrongOccMask(s, 0, 0) });
            CollectionAssert.AreEqual(new[] { Site(7, 0) }, Placement.BuildFront(s));
            var start = Placement.PiecePlace(s, 1);
            var fall = Gravity.ComputeFall(s, 1, N(6, 8));
            Assert.AreEqual(A(6, 1), fall.Landing);
            Assert.AreEqual(OkV, fall.Verdict);
            var o = Placement.SettlePlacement(s, 1, fall.Landing, fall.Verdict, new SettleOptions(start, Mortar));
            Assert.AreEqual(PlacementOutcome.KindCorrect, o.Kind);
            Assert.AreEqual(SiteP(6, 1), o.At);
            Assert.IsTrue(HasFlag(s, 1, FlagBit.Locked));
            Assert.IsFalse(HasFlag(s, 1, FlagBit.Stuck));
            CollectionAssert.AreEqual(new[] { 0b111, 0 }, new[] { FilledMask(s, 0, 0), WrongOccMask(s, 0, 0) });
            CollectionAssert.AreEqual(new[] { Site(7, 0) }, Placement.BuildFront(s));
            ExpectConsistent(s);
        }

        // --- round 3: K-18 shadow properties --------------------------------------------------------------------------------

        /// <summary>Random plan + reachable board (round 2 recipe): per column a correctly built prefix, wrong objects above it.</summary>
        private sealed class RandomBoard
        {
            public string[] Rows = new string[0];
            public string[][] Plan = new string[0][];
            public readonly List<(int x, int y, string kind, string color)> Blocks = new List<(int x, int y, string kind, string color)>();
            public readonly List<D> Debris = new List<D>();
            public readonly List<(int sx, int sy)> Trowels = new List<(int sx, int sy)>();
        }

        /// <summary>TS `randomBoard(rng, keepFree, maxBuilt)`: `keepFree(x, y)` cells get nothing; `maxBuilt(sx, h)` caps the built prefix.</summary>
        private static RandomBoard MakeRandomBoard(Mulberry32 rng, Func<int, int, bool>? keepFree = null, Func<int, int, int>? maxBuilt = null)
        {
            var colors = new[] { "W", "Y" };
            var b = new RandomBoard();
            int h = 1 + rng.NextInt(8);
            var plan = new[] { new string[h], new string[h] };
            for (int sx = 0; sx < 2; sx++)
                for (int sy = 0; sy < h; sy++)
                    plan[sx][sy] = rng.NextInt(5) == 0 ? "." : colors[rng.NextInt(2)];
            if (!plan.Any(col => col.Any(ch => ch != "."))) plan[0][0] = "W";
            var rows = new List<string>();
            for (int sy = h - 1; sy >= 0; sy--) rows.Add(plan[0][sy] + plan[1][sy]);
            for (int sx = 0; sx < 2; sx++)
            {
                int built = Math.Min(rng.NextInt(h + 1), maxBuilt != null ? maxBuilt(sx, h) : h);
                for (int sy = 0; sy < 8; sy++)
                {
                    string? ch = sy < h ? plan[sx][sy] : null;
                    if (keepFree != null && keepFree(6 + sx, sy)) continue;
                    if (sy < built)
                    {
                        if (ch == ".") continue;
                        if (rng.NextInt(4) == 0) b.Trowels.Add((sx, sy));
                        else b.Blocks.Add((6 + sx, sy, Locked, ch!));
                        continue;
                    }
                    int roll = rng.NextInt(12);
                    if (ch != null && roll == 0) b.Blocks.Add((6 + sx, sy, Stuck, "R"));
                    else if (roll == 1 && b.Debris.Count < 2) b.Debris.Add(new D("B1_0", "R", 6 + sx, sy));
                }
            }
            b.Rows = rows.ToArray();
            b.Plan = plan;
            return b;
        }

        /// <summary>Builds the board: piece 0 = `mover`, then one B1 per site block (yard slots x 0–3, rows 0–3, vacated by `ToSite`).</summary>
        private static GameState BuildBoard(RandomBoard b, P mover, WallSpec? wall = null)
        {
            var pieces = new List<P> { mover };
            for (int i = 0; i < b.Blocks.Count; i++) pieces.Add(Pc("B1_0", b.Blocks[i].color, i % 4, i / 4));
            var s = Init(new LevelSpec { Moves = 20, Wall = wall, Plan = b.Rows, Pieces = pieces.ToArray(), Debris = b.Debris.ToArray() });
            for (int i = 0; i < b.Blocks.Count; i++) ToSite(s, 1 + i, b.Blocks[i].x, b.Blocks[i].y, locked: b.Blocks[i].kind == Locked, stuck: b.Blocks[i].kind == Stuck);
            foreach (var t in b.Trowels) SetSiteOcc(s, 0, t.sx, t.sy, SiteTrowel);
            Grid.RefreshSiteMasks(s, 0);
            return s;
        }

        [Test(Description = "K-18 the shadow is a pure, repeatable preview: computeFall, shadowInfo, isCorrectPlacement, buildFront and returnTarget never change the state")]
        public void K18_the_shadow_is_a_pure_repeatable_preview()
        {
            // K-18: the shadow shows where the block WOULD stop; K-08: the board is frozen while dragging (TECH §5.1 "Pure")
            var shapes = new[] { "B1_0", "D2_0", "D2_90", "O4_0", "C3_0", "C3_180", "I3_0", "L4_0", "T4_0" };
            var rng = new Mulberry32(18_3003);
            int calls = 0;
            for (int round = 0; round < 120; round++)
            {
                var b = MakeRandomBoard(rng);
                string shape = shapes[rng.NextInt(shapes.Length)];
                var s = BuildBoard(b, Pc(shape, "W", 4, 4));
                ExpectConsistent(s);
                var before = Snapshot(s);
                // movers: the yard block and every stuck / debris block on the site
                var movers = new List<int> { 0 };
                for (int id = 1; id < s.Lvl.Layout.Counts.Pieces; id++)
                    if (HasFlag(s, id, FlagBit.Stuck) || HasFlag(s, id, FlagBit.Debris)) movers.Add(id);
                foreach (int id in movers)
                {
                    var def = Shapes.ShapeById(id == 0 ? shape : "B1_0"); // site blocks and debris of the board are B1
                    for (int ix = 6; ix <= 8 - def.W; ix++)
                    {
                        for (int iy = 0; iy <= 10 - def.H; iy++)
                        {
                            foreach (var node in new[] { N(ix, iy), R(0, ix, iy) })
                            {
                                var a = Gravity.ComputeFall(s, id, node);
                                var again = Gravity.ComputeFall(s, id, node);
                                Assert.AreEqual(a, again);
                                Gravity.ShadowInfo(a, "easy");
                                Gravity.ShadowInfo(a, "hard");
                                Placement.IsCorrectPlacement(s, id, a.Cells);
                                calls++;
                            }
                        }
                    }
                    Placement.ReturnTarget(s, id);
                    Placement.ReturnTarget(s, id, new ReturnOptions(skipStart: true));
                }
                Placement.BuildFront(s);
                Placement.IsSegmentComplete(s, 0);
                CollectionAssert.AreEqual(before, s.Buf);
            }
            Assert.Greater(calls, 5000);
        }

        // --- round 4: K-34 hook 2 reasonCells ----------------------------------------------------------------------------------

        private static string[] CellKeys(BoardCell[] cells) => cells.Select(c => c.X + "," + c.Y).ToArray();

        [Test(Description = "K-16 (1) / K-34 hook 2: color → only the cells whose plan colour differs; window → only `.` cells; outside → only cells off the plan; support → none (its cells are missingSupport); Faz 2R: a debris block is judged like any block")]
        public void K16_1_K34_hook_2_reasonCells_per_reason()
        {
            // plan (bottom → top): y0 WY, y1 W., y2 off the plan
            var s = Init(new LevelSpec { Plan = new[] { "W.", "WY" }, Pieces = Ps(Pc("B1_0", "Y", 0, 0), Pc("C3_0", "W", 2, 0)), Debris = new[] { new D("B1_0", "W", 7, 1) } });
            var cells = CellsOf("C3_0", 6, 0); // (6,0) W ✓, (7,0) Y ✗ colour, (6,1) W ✓
            CollectionAssert.AreEqual(Reasons(VerdictReasons.Color), Placement.IsCorrectPlacement(s, 1, cells).Reasons);
            CollectionAssert.AreEqual(new[] { "7,0" }, CellKeys(Placement.ReasonCells(s, 1, cells, VerdictReasons.Color)));
            var high = CellsOf("C3_0", 6, 1); // (6,1) W ✓, (7,1) `.`, (6,2) off the plan
            CollectionAssert.AreEqual(Reasons(VerdictReasons.Outside, VerdictReasons.Window), Placement.IsCorrectPlacement(s, 1, high).Reasons.Take(2).ToArray());
            CollectionAssert.AreEqual(new[] { "6,2" }, CellKeys(Placement.ReasonCells(s, 1, high, VerdictReasons.Outside)));
            CollectionAssert.AreEqual(new[] { "7,1" }, CellKeys(Placement.ReasonCells(s, 1, high, VerdictReasons.Window)));
            CollectionAssert.IsEmpty(Placement.ReasonCells(s, 1, high, VerdictReasons.Color));
            CollectionAssert.IsEmpty(Placement.ReasonCells(s, 1, high, VerdictReasons.Support));
            // OBSTACLES S4 Faz 2R: the `debris` reason is gone; a W debris block over a W cell is correct (K-16, K-47)
            var on = CellsOf("B1_0", 6, 0);
            var v = Placement.IsCorrectPlacement(s, 2, on);
            Assert.IsTrue(v.Ok);
            CollectionAssert.IsEmpty(v.Reasons);
        }

        [Test(Description = "K-32 / K-16 (1) the colour reason reads the RESOLVED `?` colour (the shadow is neutral there, K-18, but the cell is named)")]
        public void K32_K16_1_the_colour_reason_reads_the_RESOLVED_hidden_colour()
        {
            var s = Init(new LevelSpec { Plan = new[] { "??", "WY" }, Hidden = Repeat1, Pieces = Ps(Pc("D2_90", "Y", 0, 0), Pc("D2_90", "Y", 2, 0)) });
            ToSite(s, 0, 6, 0, locked: true);
            // a D2_90 Y on row 1 (`??` = W Y by repeat): (6,1) resolves to W → wrong colour, (7,1) resolves to Y → fine
            var cells = CellsOf("D2_90", 6, 1);
            Assert.AreEqual(VerdictReasons.Color, Placement.IsCorrectPlacement(s, 1, cells).Reasons[0]);
            CollectionAssert.AreEqual(new[] { "6,1" }, CellKeys(Placement.ReasonCells(s, 1, cells, VerdictReasons.Color)));
        }

        [Test(Description = "K-34 hook 2 property (seeded random boards): reasonCells(primary) is non-empty exactly for the cell reasons, is a subset of the landing in board order, and each named cell breaks that reason on its own")]
        public void K34_hook_2_property_reasonCells_primary_is_non_empty_exactly_for_the_cell_reasons()
        {
            var r = new Mulberry32(20261006);
            double Rng() => r.Next();
            var colors = new[] { "W", "Y", "G" };
            T Pick<T>(T[] xs) => xs[(int)Math.Floor(Rng() * xs.Length)];
            int named = 0;
            for (int trial = 0; trial < 300; trial++)
            {
                int h = 1 + (int)Math.Floor(Rng() * 4);
                var plan = new string[h];
                for (int i = 0; i < h; i++)
                {
                    var row = new string[2];
                    for (int c = 0; c < 2; c++) row[c] = Rng() < 0.15 ? "." : Pick(colors);
                    plan[i] = row[0] + row[1];
                }
                if (plan.All(row => row == "..")) continue;
                string shape = Pick(new[] { "B1_0", "D2_0", "D2_90", "C3_0", "C3_90", "C3_180", "C3_270", "O4_0" });
                string color = Pick(colors);
                var s = Init(new LevelSpec { Plan = plan, Pieces = Ps(Pc(shape, color, 0, 0)) });
                var sh = Shapes.ShapeById(shape);
                for (int ix = 6; ix + sh.W <= 8; ix++)
                {
                    for (int iy = 0; iy + sh.H <= 8; iy++)
                    {
                        var cells = Movement.BlockCells(sh, ix, iy);
                        var v = Placement.IsCorrectPlacement(s, 0, cells);
                        string? primary = v.Reasons.Length > 0 ? v.Reasons[0] : null;
                        foreach (var reason in new[] { VerdictReasons.Outside, VerdictReasons.Window, VerdictReasons.Color, VerdictReasons.Support })
                        {
                            var got = Placement.ReasonCells(s, 0, cells, reason);
                            string? PlanAt(int x, int y) => y < h ? plan[h - 1 - y][x - 6].ToString() : null;
                            var expected = cells.Where(c =>
                            {
                                string? ch = PlanAt(c.X, c.Y);
                                if (reason == VerdictReasons.Outside) return ch == null;
                                if (reason == VerdictReasons.Window) return ch == ".";
                                if (reason == VerdictReasons.Color) return ch != null && ch != "." && ch != color;
                                return false;
                            }).ToArray();
                            CollectionAssert.AreEqual(CellKeys(expected), CellKeys(got));
                            if (reason == primary && reason != VerdictReasons.Support)
                            {
                                Assert.Greater(got.Length, 0);
                                named += 1;
                            }
                        }
                    }
                }
            }
            Assert.Greater(named, 200);
        }
    }
}
