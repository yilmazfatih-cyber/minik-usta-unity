#nullable enable
// Port of tests/core/boosters.test.ts (web repo). Booster effects are applied through the Boosters helpers (the TS tests run
// them through `applyMove`); drags through `Boards.Drop`. Deferred to the K-35 / session packages: the booster event lists,
// `measureYao`, the `won` flag of step 11 and the K-39 Undo tests on `GameSession`.
using System.Linq;
using MinikUsta.Core.Obstacles;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;
using static MinikUsta.Core.Tests.Fixtures.Boards;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class BoostersTests
    {
        /// <summary>LEVELS Bölüm 8 (Faz 2R draft): yard 6 × 5, site 2 × 6; `f` Q9 Ağır Yük over the first needed `a` (O4 R).</summary>
        private static LevelSpec B8() => new LevelSpec
        {
            Id = 8,
            Difficulty = "normal",
            Yard = new SizeSpec(6, 5),
            Site = new SizeSpec(2, 6),
            Wall = Wall(5),
            Plan = new[] { "WW", "YY", "YW", "YW", "RR", "RR" },
            Pieces = Ps(Pc("O4_0", "R", 0, 0), Pc("D2_0", "W", 2, 0), Pc("D2_0", "Y", 3, 0), Pc("D2_90", "Y", 4, 2), Pc("D2_90", "W", 4, 3), Pc("Q9_0", "W", 0, 2)),
        };

        /// <summary>GDD K-30 tiling example board (site 2 × 4, plan bottom → top YW YW WW WW).</summary>
        private static LevelSpec TILING() => new LevelSpec
        {
            Wall = Wall(2),
            Site = new SizeSpec(2, 4),
            Plan = new[] { "WW", "WW", "YW", "YW" },
            Pieces = Ps(Pc("D2_0", "Y", 0, 0), Pc("D2_0", "W", 1, 0), Pc("O4_0", "W", 2, 0)),
        };

        private static CraneMove Crane(int id, string zone, int x, int y, int rotation) => new CraneMove(id, zone, x, y, rotation);

        [Test(Description = "K-36 GDD example (Bölüm 8): the Q9 Ağır Yük is smashed (gone), the O4 R is uncovered; no move, m or streak; K-54 the slot then has no target")]
        public void K36_GDD_example_Bolum_8_the_Q9_Agir_Yuk_is_smashed()
        {
            var s = Init(B8());
            var hooks = Registry.LevelHooks(s.Lvl);
            var targets = Boosters.HammerTargets(s, hooks);
            Assert.AreEqual(1, targets.Length);
            Assert.AreEqual(5, targets[0].PieceId);
            Assert.AreEqual(HammerTargetKinds.Cargo, Boosters.HammerTargetKind(s, HammerTarget.Piece(5), hooks));
            var ctx = ObstacleFixtures.CtxOf(s);
            var fx = Boosters.ApplyHammer(ctx.Ctx, HammerTarget.Piece(5), hooks);
            Assert.AreEqual(HammerTargetKinds.Cargo, fx.Kind);
            Assert.AreEqual(YardAt(0, 2), fx.At);
            Assert.AreEqual(Zone.Gone, PieceZone(s, 5));
            CollectionAssert.AreEqual(new[] { 0, 20, 0, 0 }, new[] { Hdr(s, H.Turn), Hdr(s, H.MovesLeft), Hdr(s, H.MovesSpent), Hdr(s, H.Combo) });
            CollectionAssert.IsEmpty(Boosters.HammerTargets(s, hooks));
            Assert.IsFalse(Boosters.BoosterTargets(s, hooks, false).Hammer);
            Assert.AreEqual(SlotStates.NoTarget, Summary.SlotState(true, false, 2));
            ExpectConsistent(s);
        }

        [Test(Description = "E-51 the hammer on a material yard block (the O4 R, the D2_0 W) is no target and changes nothing (K-47)")]
        public void E51_the_hammer_on_a_material_yard_block_is_no_target()
        {
            var s = Init(B8());
            var hooks = Registry.LevelHooks(s.Lvl);
            var before = Snapshot(s);
            foreach (int id in new[] { 0, 1 })
            {
                Assert.IsNull(Boosters.HammerTargetKind(s, HammerTarget.Piece(id), hooks));
                Assert.Throws<System.ArgumentOutOfRangeException>(() => Boosters.ApplyHammer(ObstacleFixtures.CtxOf(s).Ctx, HammerTarget.Piece(id), hooks));
            }
            CollectionAssert.AreEqual(before, s.Buf);
        }

        [Test(Description = "K-36 a site debris goes down to the yard by K-17 step 2 (first column nearest the wall) and counts clear: debris; a stuck mortar block is freed the same way")]
        public void K36_a_site_debris_goes_down_to_the_yard_by_K17_step_2_and_counts_clear_debris()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(2),
                Site = new SizeSpec(2, 2),
                Plan = new[] { "WW", "RR" },
                Pieces = Ps(Pc("D2_90", "R", 0, 0)),
                Debris = new[] { new D("D2_90", "W", 6, 0) },
                Goals = new[] { J.O("type", "build"), J.O("type", "clear", "target", "debris", "count", 1) },
            });
            var hooks = Registry.LevelHooks(s.Lvl);
            Assert.AreEqual(HammerTargetKinds.SiteDebris, Boosters.HammerTargetKind(s, HammerTarget.Piece(1), hooks));
            var fx = Boosters.ApplyHammer(ObstacleFixtures.CtxOf(s).Ctx, HammerTarget.Piece(1), hooks);
            Assert.AreEqual(HammerTargetKinds.SiteDebris, fx.Kind);
            Assert.AreEqual(Site(6, 0, 0), fx.From);
            Assert.AreEqual(2, fx.Target!.Step);
            Assert.AreEqual(YardP(4, 0), fx.Target.To);
            Assert.IsTrue(Goals.CountDebrisLeftSite(s, 1, Zone.Site)); // mini pipeline step 7
            Assert.AreEqual(1, GoalValue(s, 1));
            CollectionAssert.AreEqual(new object[] { Zone.Yard, 4, false }, new object[] { PieceZone(s, 1), PieceX(s, 1), HasFlag(s, 1, FlagBit.Debris) });
            ExpectConsistent(s);

            // Y8-like sticking rule (the real plugin is Faz 3): a mortar block sticks on a wrong placement
            var mortar = new PlacementRules((st, id, cells, verdict) => HasFlag(st, id, FlagBit.Mortar) ? PlacementOverrides.Stick : PlacementOverrides.Default);
            var m = Init(new LevelSpec
            {
                Wall = Wall(2),
                Site = new SizeSpec(2, 1),
                Plan = new[] { "WW" },
                Pieces = Ps(Pc("B1_0", "Y", 0, 0, new[] { "mortar" }), Pc("D2_90", "W", 2, 0)),
            });
            Drop(m, 0, 6, 8, null, new MoveHooks(placement: mortar));
            Assert.IsTrue(HasFlag(m, 0, FlagBit.Stuck));
            Assert.AreEqual(HammerTargetKinds.StuckMortar, Boosters.HammerTargetKind(m, HammerTarget.Piece(0), MoveHooks.None));
            Boosters.ApplyHammer(ObstacleFixtures.CtxOf(m).Ctx, HammerTarget.Piece(0), MoveHooks.None);
            CollectionAssert.AreEqual(new object[] { Zone.Yard, false }, new object[] { PieceZone(m, 0), HasFlag(m, 0, FlagBit.Stuck) });
            ExpectConsistent(m);
        }

        // --- K-37 Crane ------------------------------------------------------------------------------------------------------

        [Test(Description = "K-37 (b) a buried block flies to its correct site spot and locks: no streak, no move (Bölüm 10 shape); K-48 only the Ağır Yük stays")]
        public void K37_b_a_buried_block_flies_to_its_correct_site_spot_and_locks()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(2),
                Site = new SizeSpec(2, 4),
                Plan = new[] { "GG", "GG", "RR", "RR" },
                Pieces = Ps(Pc("O4_0", "R", 0, 0), Pc("O4_0", "G", 2, 0), Pc("Q9_0", "W", 2, 2)),
            });
            Drop(s, 0, 6, 8);
            int c = Hdr(s, H.Combo);
            CollectionAssert.AreEqual(new[] { new CraneSpot(0, A(6, 2)) }, Boosters.CraneSiteSpots(s, 1));
            var move = Crane(1, At.ZoneSite, 6, 2, 0);
            Assert.IsNull(Boosters.CraneRejection(s, move, MoveHooks.None));
            var fx = Boosters.ApplyCraneEffect(s, move);
            Assert.AreEqual(YardAt(2, 0), fx.From);
            Assert.AreEqual(Site(6, 2, 0), fx.To);
            Assert.IsTrue(fx.ToSite);
            Assert.IsTrue(HasFlag(s, 1, FlagBit.Locked));
            Assert.IsNotNull(Site.SiteStrategy(s.Lvl).CompleteIfDone(s)); // mini pipeline step 8
            Assert.IsTrue(Goals.LevelGoalsMet(s)); // K-48: only the Ağır Yük stays
            CollectionAssert.AreEqual(new[] { c, 1 }, new[] { Hdr(s, H.Combo), Hdr(s, H.Turn) });
        }

        [Test(Description = "K-37 a site target may turn the block clockwise (C3_0 → C3_270), width ≤ Ws; a yard target never turns it; the Ağır Yük never goes to the site")]
        public void K37_a_site_target_may_turn_the_block_clockwise()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(2),
                Site = new SizeSpec(2, 2),
                Plan = new[] { "WW", "WW" },
                Pieces = Ps(Pc("C3_0", "W", 0, 0), Pc("B1_0", "W", 3, 0), Pc("I5_0", "W", 0, 4)),
            });
            CollectionAssert.AreEqual(new[] { new CraneSpot(0, A(6, 0)), new CraneSpot(270, A(6, 0)) }, Boosters.CraneSiteSpots(s, 0));
            Assert.AreEqual(BoosterRejects.BadRotation, Boosters.CraneRejection(s, Crane(0, At.ZoneYard, 3, 2, 90), MoveHooks.None));
            Assert.AreEqual(BoosterRejects.NoTarget, Boosters.CraneRejection(s, Crane(2, At.ZoneSite, 6, 0, 0), MoveHooks.None));
            var cargoDown = Crane(2, At.ZoneYard, 0, 3, 0);
            Assert.IsNull(Boosters.CraneRejection(s, cargoDown, MoveHooks.None));
            Boosters.ApplyCraneEffect(s, cargoDown);
            var turn = Crane(0, At.ZoneSite, 6, 0, 270);
            Assert.IsNull(Boosters.CraneRejection(s, turn, MoveHooks.None));
            var fx = Boosters.ApplyCraneEffect(s, turn);
            Assert.AreEqual("C3_270", Shapes.ShapeByIndex(PieceShape(s, 0)).Id);
            Assert.AreEqual("C3_270", fx.Shape.Id);
            Assert.IsTrue(Drop(s, 1, 6, 8).Correct);
            Assert.IsTrue(Goals.LevelGoalsMet(s));
            ExpectConsistent(s);
        }

        [Test(Description = "K-37 a block wider than Ws after the turn is refused (I3_90 on a 2-wide site)")]
        public void K37_a_block_wider_than_Ws_after_the_turn_is_refused()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(2),
                Site = new SizeSpec(2, 3),
                Plan = new[] { "WW", "WW", "YW" },
                Pieces = Ps(Pc("I3_0", "W", 0, 0), Pc("D2_0", "W", 1, 0), Pc("B1_0", "Y", 2, 0)),
            });
            Drop(s, 2, 6, 8);
            CollectionAssert.AreEqual(new[] { new CraneSpot(0, A(7, 0)) }, Boosters.CraneSiteSpots(s, 0)); // I3_90 (w 3) never listed
            Assert.AreEqual(BoosterRejects.BadRotation, Boosters.CraneRejection(s, Crane(0, At.ZoneSite, 6, 1, 90), MoveHooks.None));
        }

        [Test(Description = "E-60 crane target rejected by the D3a precheck: of the two correct spots of the D2_0 W, the dead one is refused and nothing is spent; a yard target cannot turn it")]
        public void E60_crane_target_rejected_by_the_D3a_precheck()
        {
            var s = Init(TILING());
            Drop(s, 0, 6, 8);
            CollectionAssert.AreEqual(new[] { new CraneSpot(0, A(6, 2)), new CraneSpot(0, A(7, 0)) }, Boosters.CraneSiteSpots(s, 1));
            var before = Snapshot(s);
            Assert.AreEqual(BoosterRejects.Precheck, Boosters.CraneRejection(s, Crane(1, At.ZoneSite, 6, 2, 0), MoveHooks.None));
            CollectionAssert.AreEqual(before, s.Buf);
            Assert.AreEqual(BoosterRejects.BadRotation, Boosters.CraneRejection(s, Crane(1, At.ZoneYard, 3, 3, 90), MoveHooks.None));
            var ok = Crane(1, At.ZoneSite, 7, 0, 0);
            Assert.IsNull(Boosters.CraneRejection(s, ok, MoveHooks.None));
            Boosters.ApplyCraneEffect(s, ok);
            Assert.IsTrue(HasFlag(s, 1, FlagBit.Locked));
        }

        // --- K-38 Paint brush ------------------------------------------------------------------------------------------------

        [Test(Description = "E-52 a 2-cell W block with O4 Y: cell counts differ → nothing, not spent; O4 W with O4 Y → colours swap, supply per colour unchanged (K-47)")]
        public void E52_paint_brush_cell_counts_differ_nothing_equal_swap()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(2),
                Site = new SizeSpec(2, 5),
                Plan = new[] { "WW", "YY", "YY", "WW", "WW" },
                Pieces = Ps(Pc("O4_0", "Y", 0, 0), Pc("O4_0", "W", 2, 0), Pc("D2_90", "W", 4, 0)),
            });
            var supply = Goals.RemainingSupplyByColor(s);
            var before = Snapshot(s);
            Assert.AreEqual(BoosterRejects.NoTarget, Boosters.PaintRejection(s, 2, 0));
            CollectionAssert.AreEqual(before, s.Buf);
            CollectionAssert.AreEqual(new[] { 1 }, Boosters.PaintPartners(s, 0));
            Assert.IsNull(Boosters.PaintRejection(s, 1, 0));
            Boosters.SwapColors(s, 1, 0);
            CollectionAssert.AreEqual(new[] { "Y", "W" }, new[] { Boosters.ColorCodeOf(PieceColor(s, 1)), Boosters.ColorCodeOf(PieceColor(s, 0)) });
            CollectionAssert.AreEqual(new[] { 0, 1 }, new[] { PieceColor(s, 0), PieceColor(s, 1) }); // W, Y
            CollectionAssert.AreEqual(new[] { 0, 0, before[s.Lvl.Layout.Pieces] }, new[] { PieceX(s, 0), PieceY(s, 0), PieceShape(s, 0) });
            CollectionAssert.AreEqual(supply, Goals.RemainingSupplyByColor(s));
            CollectionAssert.AreEqual(new[] { 0, 20 }, new[] { Hdr(s, H.Turn), Hdr(s, H.MovesLeft) });
        }

        [Test(Description = "K-38 pre-check: a swap whose result cannot be tiled is refused (D2_0 Y ↔ D2_90 W leaves a vertical Y need for a horizontal block)")]
        public void K38_precheck_a_swap_whose_result_cannot_be_tiled_is_refused()
        {
            // plan bottom → top: Y W / Y W / W W; blocks D2_0 Y, D2_0 W, D2_90 W
            var s = Init(new LevelSpec
            {
                Wall = Wall(2),
                Site = new SizeSpec(2, 3),
                Plan = new[] { "WW", "YW", "YW" },
                Pieces = Ps(Pc("D2_0", "Y", 0, 0), Pc("D2_0", "W", 1, 0), Pc("D2_90", "W", 2, 0)),
            });
            Assert.AreEqual(BoosterRejects.Precheck, Boosters.PaintRejection(s, 0, 2));
            Assert.IsNull(Boosters.PaintRejection(s, 0, 1));
            Assert.IsTrue(Boosters.HasPaintTarget(s));
        }

        // --- K-54 slot targets (K-36…K-39) and K-39 Undo --------------------------------------------------------------------------

        [Test(Description = "K-54 slot state order locked noTarget empty ready (first that holds)")]
        public void K54_slot_state_order()
        {
            Assert.AreEqual(SlotStates.Locked, Summary.SlotState(false, false, 0));
            Assert.AreEqual(SlotStates.Locked, Summary.SlotState(false, true, 3));
            Assert.AreEqual(SlotStates.NoTarget, Summary.SlotState(true, false, 3));
            Assert.AreEqual(SlotStates.Empty, Summary.SlotState(true, true, 0));
            Assert.AreEqual(SlotStates.Ready, Summary.SlotState(true, true, 1));
        }

        [Test(Description = "K-54 no purchase offer for booster without target: only an empty (unlocked, targeted, 0) slot shows \"+\" and the window")]
        public void K54_no_purchase_offer_for_booster_without_target()
        {
            Assert.IsFalse(Summary.PurchaseOffered(SlotStates.NoTarget));
            Assert.IsFalse(Summary.PurchaseOffered(SlotStates.Locked));
            Assert.IsFalse(Summary.PurchaseOffered(SlotStates.Ready));
            Assert.IsTrue(Summary.PurchaseOffered(SlotStates.Empty));
        }

        [Test(Description = "K-54 targets on Bölüm 8 at the start: hammer (Ağır Yük), crane, trowel (the O4 R has P), paint brush (D2_0 W ↔ D2_0 Y); Undo needs a drag (the GameSession half comes with the session package)")]
        public void K54_targets_on_Bolum_8_at_the_start()
        {
            var s = Init(B8());
            var t = Boosters.BoosterTargets(s, Registry.LevelHooks(s.Lvl), false);
            Assert.AreEqual(new BoosterTargets(true, true, false, true, true), t);
            CollectionAssert.AreEqual(new[] { A(6, 0) }, Boosters.TrowelSpots(s, 0));
            Assert.AreEqual(UndoBlocks.NoDragMove, Boosters.UndoBlock(new UndoView(BoosterOutcomes.Playing, false)));
            Assert.IsNull(Boosters.UndoBlock(new UndoView(BoosterOutcomes.Playing, true)));
            Assert.AreEqual(UndoBlocks.LossWindow, Boosters.UndoBlock(new UndoView(BoosterOutcomes.OutOfMoves, true)));
            Assert.AreEqual(UndoBlocks.LevelOver, Boosters.UndoBlock(new UndoView(BoosterOutcomes.Won, true)));
        }

        [Test(Description = "K-36 hook ownership: a chained block is a hammer target through the Y3 rule (the chain goes, the block stays)")]
        public void K36_hook_ownership_a_chained_block_is_a_hammer_target_through_the_Y3_rule()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(2),
                Site = new SizeSpec(2, 1),
                Plan = new[] { "WW" },
                Pieces = Ps(Pc("B1_0", "W", 0, 0, new[] { "chained" }), Pc("B1_0", "W", 1, 0)),
                Goals = new[] { J.O("type", "build"), J.O("type", "clear", "target", "chain", "count", 1) },
            });
            var hooks = Registry.LevelHooks(s.Lvl);
            Assert.AreEqual(HammerTargetKinds.Chain, Boosters.HammerTargetKind(s, HammerTarget.Piece(0), hooks));
            Assert.IsNull(Boosters.HammerTargetKind(s, HammerTarget.Piece(1), hooks));
            var ctx = ObstacleFixtures.CtxOf(s);
            var fx = Boosters.ApplyHammer(ctx.Ctx, HammerTarget.Piece(0), hooks);
            Assert.AreEqual(HammerTargetKinds.Chain, fx.Kind);
            Assert.AreEqual(EntityRef.Piece(0), fx.Entity);
            CollectionAssert.AreEqual(new[] { "chainReleased" }, ctx.Types());
            CollectionAssert.AreEqual(new object[] { false, Zone.Yard, 1 }, new object[] { HasFlag(s, 0, FlagBit.Chained), PieceZone(s, 0), GoalValue(s, 1) });
        }
    }
}
