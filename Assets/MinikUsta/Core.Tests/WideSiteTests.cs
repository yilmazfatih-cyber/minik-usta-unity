#nullable enable
// Levels v3c (D-103 wide site, D-104 pallet layers on the site only, D-105 bonus reward; GDD K-49, K-56 annex, K-58 item 9, K-60 items
// 6-7, K-62, K-63 annex): the C# twin of tests/core/wideSite.test.ts. The diff fuzz (DiffFuzzTests: the 6 × 8 | 3 × 5 and 6 × 6 | 3 × 5
// boards, `palletSiteOnly` cancels, the bonus event with `pieceId`) proves bit equality with the TypeScript core; these tests state the
// rules in words. Every test name carries its rule id.
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core.Level;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;
using static MinikUsta.Core.Tests.Fixtures.Boards;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class WideSiteTests
    {
        private static readonly SizeSpec WideYard = new SizeSpec(6, 4);
        private static readonly SizeSpec WideSite = new SizeSpec(3, 5);

        private static LevelData Parse(LevelSpec spec)
        {
            spec.SchemaVersion = 3;
            var res = Logic.ValidateLevelJson(Builders.LevelJson(spec), new LogicContext(only: new string[0]));
            Assert.IsNotNull(res.Level, string.Join("\n", res.Issues.Select(i => i.ToString())));
            return res.Level!;
        }

        private static GameState V3(LevelSpec spec) => CreateInitialState(Compiler.Compile(Parse(spec)));

        private static string[] Codes(LevelSpec spec)
        {
            spec.SchemaVersion = 3;
            return Logic.ValidateLevelJson(Builders.LevelJson(spec), new LogicContext()).Issues.Select(i => i.Code).ToArray();
        }

        private static Dictionary<int, MinikUsta.Core.Json.JsonValue> Tr(params (int, MinikUsta.Core.Json.JsonValue)[] items) => items.ToDictionary(i => i.Item1, i => i.Item2);
        private static MinikUsta.Core.Json.JsonValue StackOf(int id, int z) => J.O("stack", J.O("id", id, "z", z));

        // --- K-49 D-103 wide site ----------------------------------------------------------------------------------------------

        [Test(Description = "K-44 K-49 3-wide material crosses the wall: L4_270 W flies over a height-5 wall onto a 3-column site")]
        public void K44_K49_3_wide_material_crosses_the_wall_L4_270_flies_over_a_height_5_wall_onto_a_3_column_site()
        {
            var s = V3(new LevelSpec { Id = 6, Yard = WideYard, Site = WideSite, Wall = Wall(5), Plan = new[] { "GGG", "GGG", "GGG", "YYW", "WWW" }, Pieces = Ps(Pc("L4_270", "W", 0, 0)) });
            CollectionAssert.AreEqual(new[] { 9, 6, 5 }, new[] { s.Lvl.Geo.Cols, s.Lvl.Geo.SiteX, s.Lvl.Geo.H });
            var d = Movement.BeginDrag(s, 0);
            Assert.IsNotNull(d);
            Assert.IsTrue(d!.ReachableNodes().Any(n => n.Ix >= 6));
            var r = Run(s, DragTo(0, 6, 5));
            var ok = Find<PlacementCorrectEvent>(r.Ev);
            Assert.IsTrue(ok.OverWall);
            CollectionAssert.AreEqual(new[] { "6,0", "7,0", "8,0", "8,1" }, ok.Cells.Select(c => c.X + "," + c.Y).ToArray());
            CollectionAssert.IsEmpty(Grid.StateInvariantErrors(s));
        }

        [Test(Description = "K-49 K-56 the GDD T4_90 example: correct on heights (2, 1, 2); on (1, 1, 1) it lands on the middle column and fails support")]
        public void K49_K56_the_GDD_T4_90_example_correct_on_heights_2_1_2_otherwise_support()
        {
            LevelSpec Spec() => new LevelSpec
            {
                Id = 6, Yard = WideYard, Site = WideSite, Wall = Wall(4), Plan = new[] { "GGG", "GGG", "YYY", "WYW", "WWW" },
                Pieces = Ps(Pc("I3_90", "W", 0, 0), Pc("B1_0", "W", 3, 0), Pc("B1_0", "W", 4, 0), Pc("T4_90", "Y", 3, 2)),
            };
            var s = V3(Spec());
            Run(s, DragTo(0, 6, 6));
            Run(s, DragTo(1, 6, 6));
            Run(s, DragTo(2, 8, 6));
            var r = Run(s, DragTo(3, 6, 5));
            CollectionAssert.AreEqual(new[] { "7,1", "6,2", "7,2", "8,2" }, Find<PlacementCorrectEvent>(r.Ev).Cells.Select(c => c.X + "," + c.Y).ToArray());
            var t = V3(Spec());
            Run(t, DragTo(0, 6, 6));
            var wrong = Run(t, DragTo(3, 6, 5));
            CollectionAssert.AreEqual(new[] { VerdictReasons.Support }, Find<PlacementWrongEvent>(wrong.Ev).Reasons);
            var to = Find<PieceBouncedEvent>(wrong.Ev).To!.Value;
            Assert.AreEqual((At.ZoneYard, 3, 2), (to.Zone, to.X, to.Y), "K-17 step 1: its start");
        }

        [Test(Description = "K-17 K-56 bounce-back columns of a 3-wide block: xs = 0 … Wy − 3, nearest the wall first for a site start")]
        public void K17_K56_bounce_back_columns_of_a_3_wide_block()
        {
            var g = Geometry.MakeGeo(new GeoParams(6, 8, 3, 5, 0));
            CollectionAssert.AreEqual(new[] { 3, 2, 1, 0 }, Placement.NearestColumnsFirst(g, 8, 3));
            CollectionAssert.AreEqual(new[] { 1, 2, 0, 3 }, Placement.NearestColumnsFirst(g, 1, 3));
        }

        [Test(Description = "K-49 K-56 P5 (plus) on a 9-column board: ASCII round trip, hash stable, D3a tiles the 3-wide plan, canonical P5_0")]
        public void K49_K56_P5_plus_on_a_9_column_board_ascii_round_trip_hash_stable_D3a_tiles()
        {
            var s = V3(new LevelSpec
            {
                Id = 6, Yard = WideYard, Site = new SizeSpec(3, 4), Wall = Wall(1), Plan = new[] { "YYY", "WRW", "RRR", "WRW" },
                Pieces = Ps(Pc("P5_0", "R", 0, 0), Pc("B1_0", "W", 3, 0), Pc("B1_0", "W", 4, 0), Pc("B1_0", "W", 5, 0), Pc("B1_0", "W", 3, 1), Pc("I3_90", "Y", 3, 2)),
            });
            string text = Ascii.ToAscii(s, ids: true);
            Assert.AreEqual(text, Ascii.ToAscii(Ascii.FromAscii(s.Lvl, text), ids: true));
            Assert.AreEqual(Hash.HashHex(Ascii.FromAscii(s.Lvl, text)), Hash.HashHex(s));
            Assert.AreEqual(TileResults.Ok, Deadlock.TileRemaining(s));
            Assert.AreEqual("P5_0", Shapes.ShapeById("P5_270").Canonical);
            Assert.AreEqual(52, Shapes.ShapeById("P5_0").Index);
        }

        [Test(Description = "K-49 item 2 and K-56 D-103 annex: levels 1–5 stay 2 columns; on Ws = 3 S4, Z4 and P5 are free of the kind lock, T4 / I3 keep theirs")]
        public void K49_item2_K56_annex_levels_1_to_5_stay_narrow_and_S4_Z4_P5_are_free_on_a_3_column_site()
        {
            string[] One(int id, int ws, string shape) => Codes(new LevelSpec
            {
                Id = id, Yard = WideYard, Site = new SizeSpec(ws, 4),
                Plan = ws == 3 ? new[] { "WWW", "WWW", "WWW", "WWW" } : new[] { "WW", "WW", "WW", "WW" },
                Pieces = Ps(Pc(shape, "W", 0, 0)),
            });
            CollectionAssert.Contains(One(3, 3, "B1_0"), "size_out_of_range");
            CollectionAssert.DoesNotContain(One(6, 3, "P5_0"), "shape_locked");
            CollectionAssert.DoesNotContain(One(6, 3, "S4_90"), "shape_locked");
            CollectionAssert.DoesNotContain(One(6, 3, "Z4_0"), "shape_locked");
            CollectionAssert.Contains(One(6, 2, "S4_0"), "shape_locked");
            CollectionAssert.Contains(One(6, 2, "P5_0"), "piece_too_wide");
            CollectionAssert.Contains(One(7, 3, "I3_90"), "shape_locked");
            CollectionAssert.DoesNotContain(One(6, 3, "L4_90"), "piece_too_wide");
        }

        [Test(Description = "K-49 RULES_VERSION 5 (D-103…D-105 change the replay of saved logs)")]
        public void K49_RULES_VERSION_5()
        {
            Assert.AreEqual(5, GameSession.RulesVersion);
        }

        // --- K-58 item 9 D-104 pallet layers on the site only -------------------------------------------------------------------

        private static LevelSpec PalletSpec() => new LevelSpec
        {
            Id = 7, Yard = new SizeSpec(4, 4), Site = new SizeSpec(2, 4), Wall = Wall(2), Plan = new[] { "GG", "WW", "WW", "WW" },
            Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("D2_90", "W", 0, 0), Pc("D2_90", "W", 0, 0), Pc("D2_90", "G", 2, 0)),
            Traits = Tr((0, StackOf(0, 0)), (1, StackOf(0, 1)), (2, StackOf(0, 2))),
        };

        [Test(Description = "K-58 item 9 (c) a release in the yard, the yard air or the crane area over the yard cancels with palletSiteOnly, cost 0; an ordinary block still parks")]
        public void K58_item9_c_a_pallet_layer_released_off_the_site_cancels_with_palletSiteOnly()
        {
            var s = V3(PalletSpec());
            Assert.IsTrue(Movement.IsPalletLayer(s, 2));
            Assert.IsFalse(Movement.IsPalletLayer(s, 3));
            var d = Movement.BeginDrag(s, 2)!;
            Assert.IsTrue(d.PalletLayer);
            foreach (var n in d.ReachableNodes())
                if (n.Ix + 1 <= s.Lvl.Geo.BoundaryX) Assert.AreEqual(DropKinds.Cancel, d.Classify(n).Kind, n.Ix + "," + n.Iy);
            int left = Hdr(s, H.MovesLeft);
            foreach (var (x, y) in new[] { (2, 2), (0, 5) })
            {
                var r = Run(s, DragTo(2, x, y));
                Assert.AreEqual(MoveStatuses.Cancelled, r.Res.Status);
                CollectionAssert.AreEqual(new[] { "moveCancelled" }, Types(r.Ev));
                Assert.AreEqual(CancelReasons.PalletSiteOnly, Find<MoveCancelledEvent>(r.Ev).Reason);
            }
            Assert.AreEqual(left, Hdr(s, H.MovesLeft));
            CollectionAssert.AreEqual(new[] { true, true, false }, new[] { Grid.IsCovered(s, 0), Grid.IsCovered(s, 1), Grid.IsCovered(s, 2) });
            Assert.AreEqual(MoveStatuses.Applied, Run(s, DragTo(3, 2, 2)).Res.Status);
        }

        [Test(Description = "K-58 item 9 (b) K-17 a wrong site drop costs 1, resets the streak and bounces the layer back onto its own pallet as the top layer")]
        public void K58_item9_b_K17_a_wrong_site_drop_bounces_the_layer_back_onto_its_own_pallet()
        {
            var spec = PalletSpec();
            spec.Plan = new[] { "WW", "WW", "GG", "WW" };
            var s = V3(spec);
            Run(s, DragTo(2, 4, 4)); // correct: bottom row W
            Assert.AreEqual(1, Hdr(s, H.Combo));
            int left = Hdr(s, H.MovesLeft);
            var r = Run(s, DragTo(1, 4, 4)); // lands on row 1 (G): wrong colour
            CollectionAssert.AreEqual(new[] { VerdictReasons.Color }, Find<PlacementWrongEvent>(r.Ev).Reasons);
            var b = Find<PieceBouncedEvent>(r.Ev);
            Assert.AreEqual((At.ZoneYard, 0, 0), (b.To!.Value.Zone, b.To.Value.X, b.To.Value.Y));
            Assert.IsFalse(b.ViaDrop);
            Assert.AreEqual(left - 1, Hdr(s, H.MovesLeft));
            Assert.AreEqual(0, Hdr(s, H.Combo));
            Assert.AreEqual((Zone.Yard, 0, 0), (PieceZone(s, 1), PieceX(s, 1), PieceY(s, 1)));
            Assert.IsTrue(Grid.IsCovered(s, 0));
            Assert.IsFalse(Grid.IsCovered(s, 1));
            Assert.AreEqual(2, YardOcc(s, 0, 0)); // layer 1 owns the pallet cells again
            CollectionAssert.IsEmpty(Grid.StateInvariantErrors(s));
        }

        [Test(Description = "K-58 item 9 (a) (h) correct drops empty the pallet; the last layer frees its footprint; Undo puts it back")]
        public void K58_item9_a_h_correct_drops_empty_the_pallet_and_undo_puts_it_back()
        {
            var session = GameSession.Start(Compiler.Compile(Parse(PalletSpec())));
            session.Commit(DragTo(2, 4, 4));
            session.Commit(DragTo(1, 4, 4));
            Assert.AreEqual(1, YardOcc(session.State, 0, 0));
            session.Commit(DragTo(0, 4, 4));
            CollectionAssert.AreEqual(new[] { 0, 0 }, new[] { YardOcc(session.State, 0, 0), YardOcc(session.State, 1, 0) });
            Assert.IsTrue(session.Undo());
            Assert.AreEqual(1, YardOcc(session.State, 0, 0));
            Assert.IsTrue(Movement.IsPalletLayer(session.State, 0));
        }

        [Test(Description = "K-58 item 9 (e) the crane has no yard target for a pallet layer; a correct site target is allowed")]
        public void K58_item9_e_the_crane_has_no_yard_target_for_a_pallet_layer()
        {
            var s = V3(PalletSpec());
            var hooks = MinikUsta.Core.Obstacles.Registry.LevelHooks(s.Lvl);
            Assert.AreEqual(BoosterRejects.PalletSiteOnly, Boosters.CraneRejection(s, new CraneMove(2, "yard", 2, 2, 0), hooks));
            Assert.IsNull(Boosters.CraneRejection(s, new CraneMove(2, "site", 4, 0, 90), hooks));
            Assert.IsNull(Boosters.CraneRejection(s, new CraneMove(3, "yard", 2, 2, 90), hooks));
        }

        [Test(Description = "K-58 item 9 (f) a pallet layer cannot be slippery, mortar or glass (flag_combo_forbidden)")]
        public void K58_item9_f_a_pallet_layer_cannot_be_slippery_mortar_or_glass()
        {
            foreach (var flag in new[] { "slippery", "mortar", "glass" })
            {
                var spec = PalletSpec();
                spec.Pieces = Ps(Pc("D2_90", "W", 0, 0, new[] { flag }), Pc("D2_90", "W", 0, 0), Pc("D2_90", "W", 0, 0), Pc("D2_90", "G", 2, 0));
                CollectionAssert.Contains(Codes(spec), "flag_combo_forbidden", flag);
            }
        }

        [Test(Description = "K-58 item 9 (g) pallet_not_grounded: a story-1 pallet one row up is an error; chapter 2 may do it")]
        public void K58_item9_g_pallet_not_grounded_in_story_1()
        {
            var lifted = PalletSpec();
            lifted.Pieces = Ps(Pc("D2_90", "W", 0, 1), Pc("D2_90", "W", 0, 1), Pc("D2_90", "W", 0, 1), Pc("D2_90", "G", 0, 0));
            CollectionAssert.Contains(Codes(lifted), "pallet_not_grounded");
            CollectionAssert.DoesNotContain(Codes(PalletSpec()), "pallet_not_grounded");
            CollectionAssert.DoesNotContain(Codes(PalletSpec()), "pallet_capacity");
        }

        // --- K-60 K-62 D-105 bonus reward -----------------------------------------------------------------------------------------

        private static LevelSpec BonusSpec() => new LevelSpec
        {
            Id = 7, Yard = new SizeSpec(4, 4), Site = new SizeSpec(2, 4), Moves = 3, Plan = new[] { "GG", "GG", "WW", "WW" },
            Pieces = Ps(Pc("D2_90", "W", 0, 0, new[] { "bonus" }), Pc("D2_90", "W", 2, 0)),
            Traits = Tr((0, J.O("bonusMoves", 2))),
        };

        [Test(Description = "K-60 item 7 K-62 a correct bonus placement emits movesChanged{reason: bonus, delta, pieceId} after the cost")]
        public void K60_item7_K62_a_correct_bonus_placement_emits_the_bonus_event_with_the_piece_id()
        {
            var s = V3(BonusSpec());
            var r = Run(s, DragTo(0, 4, 4));
            var changes = r.Ev.OfType<MovesChangedEvent>().ToArray();
            CollectionAssert.AreEqual(new[] { "move:-1:2", "bonus:2:4" }, changes.Select(e => e.Reason + ":" + e.Delta + ":" + e.MovesLeft).ToArray());
            Assert.AreEqual(0, changes[1].PieceId);
            Assert.AreEqual(-1, changes[0].PieceId, "only the bonus event names a block");
            Assert.AreEqual(4, Hdr(s, H.MovesLeft));
            StringAssert.Contains("\"pieceId\":0", MinikUsta.Core.Json.JsonWriter.Canonical(EventJson.ToJson(changes[1])));
        }

        [Test(Description = "K-60 item 7 K-62 the crane placement of a bonus block pays the bonus too; a wrong drop pays nothing")]
        public void K60_item7_K62_the_crane_placement_pays_the_bonus_too_a_wrong_drop_pays_nothing()
        {
            var s = V3(BonusSpec());
            var crane = Run(s, new CraneMove(0, "site", 4, 0, 90));
            var bonus = crane.Ev.OfType<MovesChangedEvent>().First(e => e.Reason == MovesChangedReasons.Bonus);
            CollectionAssert.AreEqual(new[] { 2, 0, 5 }, new[] { bonus.Delta, bonus.PieceId, bonus.MovesLeft });
            var spec = BonusSpec();
            spec.Plan = new[] { "WW", "WW", "GG", "GG" };
            var t = V3(spec);
            var wrong = Run(t, DragTo(0, 4, 4));
            CollectionAssert.AreEqual(new[] { MovesChangedReasons.Move }, wrong.Ev.OfType<MovesChangedEvent>().Select(e => e.Reason).ToArray());
        }

        [Test(Description = "K-62 bonus_late is retired: a bonus block placed last raises no budget code")]
        public void K62_bonus_late_is_retired()
        {
            CollectionAssert.DoesNotContain(Codes(BonusSpec()), "bonus_late");
        }

        // --- K-63 D-103 annex plan trap scan ---------------------------------------------------------------------------------------

        private static LevelSpec TrapSpec() => new LevelSpec
        {
            Id = 6, Yard = new SizeSpec(6, 4), Site = new SizeSpec(3, 4), Wall = Wall(2), Plan = new[] { "YYY", "YYY", "WWW", "WWW" },
            Pieces = Ps(Pc("L4_270", "W", 0, 0), Pc("D2_90", "W", 3, 0), Pc("I3_90", "Y", 0, 2), Pc("I3_90", "Y", 3, 1)),
        };

        private static LevelSpec FixedSpec()
        {
            var t = TrapSpec();
            t.Plan = new[] { "YYY", "YYY", "YWW", "WWW" };
            t.Traits = Tr((1, J.O("colors", "YW")));
            t.Pieces = Ps(Pc("L4_270", "W", 0, 0), Pc("D2_90", "Y", 3, 0), Pc("I3_90", "Y", 0, 2), Pc("I3_90", "Y", 3, 1));
            return t;
        }

        private static Logic.PlanTrapResult Scan(LevelSpec spec)
        {
            var level = Parse(spec);
            return Logic.PlanTrapScan(level, Plan.BuildPlans(level).Plans);
        }

        [Test(Description = "K-63 GDD example: D2_90 W first leaves cells only J4_270 / L4_90 could tile → 2 traps; the two-tone fix has 0")]
        public void K63_annex_the_GDD_example_has_2_traps_the_two_tone_fix_has_0()
        {
            var t = Scan(TrapSpec());
            CollectionAssert.AreEqual(new object[] { "done", true, 2 }, new object[] { t.Status, t.Tileable, t.Traps });
            Assert.IsTrue(t.HasExample);
            CollectionAssert.AreEqual(new object[] { 0, "D2_90", 0 }, new object[] { t.ExampleSeg, t.ExampleShape, t.ExampleY });
            var f = Scan(FixedSpec());
            CollectionAssert.AreEqual(new object[] { "done", true, 0 }, new object[] { f.Status, f.Tileable, f.Traps });
        }

        [Test(Description = "K-63 plan_trap is an error in levels 1–10 and on Easy / Normal, a warning on Hard later; tile_family_mix is not raised on Ws = 3")]
        public void K63_annex_plan_trap_severity_and_no_tile_family_mix_on_a_wide_site()
        {
            string[] Sev(LevelSpec spec)
            {
                spec.SchemaVersion = 3;
                return Logic.ValidateLevelJson(Builders.LevelJson(spec), new LogicContext()).Issues.Where(i => i.Code == "plan_trap").Select(i => i.Severity).ToArray();
            }
            CollectionAssert.AreEqual(new[] { Severities.Error }, Sev(TrapSpec()));
            var hard = TrapSpec();
            hard.Id = 12;
            hard.Difficulty = "hard";
            CollectionAssert.AreEqual(new[] { Severities.Warn }, Sev(hard));
            var normal = TrapSpec();
            normal.Id = 12;
            normal.Difficulty = "normal";
            CollectionAssert.AreEqual(new[] { Severities.Error }, Sev(normal));
            CollectionAssert.IsEmpty(Sev(FixedSpec()));
            CollectionAssert.DoesNotContain(Codes(TrapSpec()), "tile_family_mix");
        }

        [Test(Description = "K-63 K-49 the shipped wide levels 6–10 are plan-trap free (a full scan, status done) and every one has a 3-column site")]
        public void K63_K49_shipped_levels_6_to_10_have_no_plan_trap_on_a_3_column_site()
        {
            foreach (int n in new[] { 6, 7, 8, 9, 10 })
            {
                var data = Parse2(n);
                Assert.AreEqual(3, Geometry.GeoFromLevel(data).Ws, "B" + n);
                var res = Logic.PlanTrapScan(data, Plan.BuildPlans(data).Plans);
                CollectionAssert.AreEqual(new object[] { "done", true, 0 }, new object[] { res.Status, res.Tileable, res.Traps }, "B" + n);
            }
        }

        private static LevelData Parse2(int n) => MinikUsta.Core.Level.LevelJson.ParseOrThrow(TestPaths.LoadCurrentLevelJson(n));
    }
}
