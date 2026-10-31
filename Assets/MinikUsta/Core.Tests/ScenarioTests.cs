#nullable enable
// P4 scenario tests: Söküm / deadlock (K-30), +5 offers (K-29), Undo (K-39) and the truck queue (K-25, K-26, K-48)
// combined in one attempt. The same scripts are played by the TS oracle (tools/diff/ts-oracle.mjs "scenario NN …"),
// so DiffFuzzTests compares every step of them with the TypeScript core (Zobrist hash, buffer, events, ASCII, session
// fields); this file states their GDD meaning. Every expected number below is the TS value of that oracle run.
// Fixtures: TILING / E26 (tests/core/deadlock.test.ts, GDD K-30 "döşeme çıkmazı" and E-26), E03 / WIN_WITH_TRUCK /
// STREAK (tests/review/pipeline.review.test.ts).
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core.Json;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;
using static MinikUsta.Core.Tests.Fixtures.Boards;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class ScenarioTests
    {
        private static DragMove D(int id, int ix, int iy) => new DragMove(id, N(ix, iy));

        /// <summary>GDD K-30 example (Ws 2): plan bottom → top YW, YW, WW, WW; D2_0 Y (0), D2_0 W (1), O4 W (2). D2_0 W on column 6 rows 2–3 is correct by K-16 but leaves the O4 W no place.</summary>
        private static LevelSpec TILING(int moves = 20) => new LevelSpec
        {
            Moves = moves,
            Wall = new WallSpec { Height = 2 },
            Site = new SizeSpec(2, 4),
            Plan = new[] { "WW", "WW", "YW", "YW" },
            Pieces = Ps(Pc("D2_0", "Y", 0, 0), Pc("D2_0", "W", 1, 0), Pc("O4_0", "W", 2, 0)),
        };

        /// <summary>E-26: plan bottom → top W W / W Y; D2_0 W (0), B1 W (1), B1 Y (2). A B1 W on (6,0) leaves (6,1) + (7,0) for the D2_0 W.</summary>
        private static LevelSpec E26() => new LevelSpec
        {
            Wall = new WallSpec { Height = 2 },
            Site = new SizeSpec(2, 2),
            Plan = new[] { "WY", "WW" },
            Pieces = Ps(Pc("D2_0", "W", 0, 0), Pc("B1_0", "W", 2, 0), Pc("B1_0", "Y", 3, 0)),
        };

        private static P[] FullColumn(int x) => Ps(Pc("I4_0", "Y", x, 0), Pc("I4_0", "Y", x, 4));
        private static P[] Column7(int x) => Ps(Pc("I4_0", "Y", x, 0), Pc("I3_0", "Y", x, 4));
        private static P[] Cat(params P[][] parts) => parts.SelectMany(p => p).ToArray();

        /// <summary>E-03: the truck O4 R (id 14) waits until a yard move opens a 2 × 2; mover B1 W (5,7) = 0, B1 Y (1,6) = 13.</summary>
        private static LevelSpec E03() => new LevelSpec
        {
            Plans = new[] { new[] { "W." }, new[] { "WW" } },
            Pieces = Cat(Ps(Pc("B1_0", "W", 5, 7)), Column7(5), FullColumn(2), FullColumn(3), FullColumn(4),
                Ps(Pc("I4_0", "Y", 0, 0), Pc("D2_0", "Y", 0, 4), Pc("I4_0", "Y", 1, 0), Pc("D2_0", "Y", 1, 4), Pc("B1_0", "Y", 1, 6))),
            Batches = new[] { new BatchSpec { ForSegment = 1, Pieces = Ps(Pc("O4_0", "R", 0, 8)) } },
        };

        /// <summary>K-48 (3): batch 1 = B1 W x 0 (13, lands on (0,7)) and O4 R x 0 (14, never fits).</summary>
        private static LevelSpec WIN_WITH_TRUCK() => new LevelSpec
        {
            Plans = new[] { new[] { "W." }, new[] { "W." } },
            Pieces = Cat(Ps(Pc("B1_0", "W", 0, 7)), Column7(0), FullColumn(1), FullColumn(2), FullColumn(3), FullColumn(4), FullColumn(5)),
            Batches = new[] { new BatchSpec { ForSegment = 1, Pieces = Ps(Pc("B1_0", "W", 0, 8), Pc("O4_0", "R", 0, 8)) } },
        };

        private static LevelSpec STREAK(int moves = 20) => new LevelSpec
        {
            Moves = moves,
            Wall = new WallSpec { Height = 2 },
            Plan = new[] { "WW", "WW", "WW", "WW", "WW" },
            Pieces = Ps(Pc("D2_0", "W", 0, 0), Pc("D2_0", "W", 1, 0), Pc("D2_0", "W", 2, 0), Pc("D2_0", "W", 3, 0), Pc("B1_0", "Y", 4, 0), Pc("B1_0", "W", 5, 0)),
        };

        /// <summary>Session counters `movesLeft, movesMade, movesSpent` (K-43 item 2: the exit penalty reads movesSpent).</summary>
        private static int[] Counters(GameSession g) => new[] { g.MovesLeft, g.MovesMade, g.MovesSpent };

        private static string TeardownJson(ActionResult r) => JsonWriter.Canonical(EventJson.ToJson(r.Teardown!));

        // --- K-30 Söküm ----------------------------------------------------------------------------------------------------

        [Test(Description = "K-30 E-37 Söküm (tiling dead end) restores the pre-move board, keeps the spent move; Undo returns that move; the same placement tears down again; the right one wins (movesSpent 4, m 3)")]
        public void K30_E37_teardown_then_Undo_returns_the_move_the_same_placement_tears_down_again_the_right_one_wins()
        {
            var game = GameSession.Start(Builders.Compiled(TILING()));
            Assert.AreEqual(MoveStatuses.Applied, game.Commit(D(0, 6, 8)).Status);
            var pre = Snapshot(game.State);
            var torn = game.Apply(D(1, 6, 8));
            Assert.AreEqual(TeardownCauses.Tiling, torn.TeardownCause);
            Assert.AreEqual("teardown", torn.Events.Last().TypeName, "the Söküm is the last event of the move");
            Assert.AreEqual("{\"cause\":\"tiling\",\"pieces\":[{\"from\":{\"seg\":0,\"x\":6,\"y\":2,\"zone\":\"site\"},\"pieceId\":1,\"to\":{\"x\":1,\"y\":0,\"zone\":\"yard\"}}],\"seq\":5,\"step\":12,\"t\":\"teardown\",\"toTurn\":1}", TeardownJson(torn));
            // the board, m and the streak come back; the counter and movesSpent keep the move (no refund)
            CollectionAssert.AreEqual(new[] { 18, 1, 2 }, Counters(game));
            Assert.AreEqual(0, Hdr(game.State, H.Combo));
            for (int i = 0; i < pre.Length; i++)
                if (i != H.MovesLeft && i != H.MovesSpent && i != H.Combo) Assert.AreEqual(pre[i], game.State.Buf[i], "word " + i);
            // E-37: Undo after a Söküm returns the move (counter + 1, movesSpent − 1)
            Assert.IsTrue(game.Undo());
            CollectionAssert.AreEqual(pre, game.State.Buf);
            CollectionAssert.AreEqual(new[] { 19, 1, 1 }, Counters(game));
            Assert.AreEqual(UndoBlocks.NoDragMove, game.UndoBlock());
            Assert.AreEqual(TeardownCauses.Tiling, game.Apply(D(1, 6, 8)).TeardownCause, "the same placement goes to the same Söküm");
            Assert.IsNull(game.Apply(D(1, 7, 8)).TeardownCause);
            var win = game.Apply(D(2, 6, 8));
            Assert.IsTrue(win.Won);
            CollectionAssert.AreEqual(new[] { 16, 3, 4 }, Counters(game));
            Assert.AreEqual(BoosterOutcomes.Won, game.Outcome);
            var replayed = GameSession.Replay(game.State.Lvl, game.Log);
            CollectionAssert.AreEqual(game.State.Buf, replayed.State.Buf);
        }

        [Test(Description = "K-30 K-29 E-58 a Söküm on the last move: step 12 runs before the window, the window shows the torn-down board; Undo is closed (lossWindow); +5 then finishes the level")]
        public void K30_K29_E58_a_teardown_on_the_last_move_opens_the_window_after_step_12_then_plus_5_finishes()
        {
            var game = GameSession.Start(Builders.Compiled(TILING(2)));
            game.Commit(D(0, 6, 8));
            var last = game.Apply(D(1, 6, 8));
            CollectionAssert.AreEqual(new object?[] { MoveStatuses.Applied, true, TeardownCauses.Tiling }, new object?[] { last.Status, last.OutOfMoves, last.TeardownCause });
            CollectionAssert.AreEqual(new[] { "outOfMoves@11", "teardown@12" }, last.Events.Skip(last.Events.Length - 2).Select(e => e.TypeName + "@" + e.Step).ToArray());
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, Counters(game));
            Assert.AreEqual(BoosterOutcomes.OutOfMoves, game.Outcome);
            Assert.AreEqual(2, last.Summary.BlocksLeft, "the torn-down block counts again");
            Assert.AreEqual(UndoBlocks.LossWindow, game.UndoBlock());
            Assert.AreEqual(UndoBlocks.LossWindow, game.Apply(new UndoAction()).Reason);
            var offer = game.NextOffer();
            Assert.IsTrue(offer.HasValue && offer.Value.N == 1 && offer.Value.AdAllowed);
            Assert.AreEqual(MoveStatuses.Applied, game.AcceptOffer(AddMovesSource.OfferCoins).Status);
            CollectionAssert.AreEqual(new[] { 5, 1, 2 }, Counters(game));
            Assert.AreEqual(UndoBlocks.NoDragMove, game.UndoBlock(), "the offer is not undoable and clears the snapshot");
            game.Commit(D(1, 7, 8));
            Assert.IsTrue(game.Commit(D(2, 6, 8)).Won);
            CollectionAssert.AreEqual(new[] { 3, 3, 4 }, Counters(game));
            Assert.AreEqual(1, game.OffersUsed);
        }

        [Test(Description = "K-30 K-43 E-26 a Söküm takes the only move back (m 0) but movesSpent stays 1: with a Termos start the exit is a loss and nothing is refunded")]
        public void K30_K43_E26_a_teardown_takes_m_back_to_0_but_movesSpent_stays_1_the_exit_is_a_loss()
        {
            var game = GameSession.Start(Builders.Compiled(E26()), new[] { PreBoosters.Thermos });
            Assert.AreEqual(23, game.MovesLeft, "20 + Termos 3 (K-40)");
            var torn = game.Apply(D(1, 6, 8));
            Assert.AreEqual(TeardownCauses.Tiling, torn.TeardownCause);
            Assert.AreEqual(0, torn.Teardown!.ToTurn);
            CollectionAssert.AreEqual(new[] { 22, 0, 1 }, Counters(game));
            var x = game.Exit();
            CollectionAssert.AreEqual(new object[] { "loss", 0, 1, 0, true }, new object[] { x.Kind, x.MovesMade, x.MovesSpent, x.RefundPreBoosters.Length, x.StreakBonusConsumed });
            CollectionAssert.AreEqual(new[] { "lost", UndoBlocks.LevelOver }, new[] { game.Outcome, game.UndoBlock() });
        }

        [Test(Description = "K-30 K-40 K-33 a Söküm keeps the start bonuses (Termos, streak tier 2, two trowels); a Golden Trowel after it places the torn-down block and is never undone")]
        public void K30_K40_K33_a_teardown_keeps_the_start_bonuses_a_trowel_after_it_places_the_block_and_is_never_undone()
        {
            var opts = new SessionOptions(streakBonus: t => new StreakBonus(t, t >= 2 ? 1 : 0));
            var game = GameSession.Start(Builders.Compiled(TILING()), new[] { PreBoosters.TrowelStart, PreBoosters.Thermos }, 2, opts);
            CollectionAssert.AreEqual(new[] { 25, 2 }, new[] { game.MovesLeft, Hdr(game.State, H.Trowels) });
            game.Commit(D(0, 6, 8));
            Assert.AreEqual(TeardownCauses.Tiling, game.Apply(D(1, 6, 8)).TeardownCause);
            CollectionAssert.AreEqual(new[] { 23, 1, 2 }, Counters(game));
            Assert.AreEqual(2, Hdr(game.State, H.Trowels), "the Söküm restores the pre-move trowels (none were spent)");
            var trowel = game.Apply(new GoldTrowelMove(1, 7, 0));
            Assert.AreEqual(MoveStatuses.Applied, trowel.Status);
            CollectionAssert.AreEqual(new[] { "boosterApplied@1", "pieceLifted@1" }, trowel.Events.Select(e => e.TypeName + "@" + e.Step).ToArray());
            CollectionAssert.AreEqual(new[] { 23, 1, 2 }, Counters(game), "a booster spends no move (GDD §10)");
            Assert.AreEqual(UndoBlocks.NoDragMove, game.Apply(new UndoAction()).Reason);
            Assert.IsTrue(game.Commit(D(2, 6, 8)).Won);
            CollectionAssert.AreEqual(new[] { 22, 2, 3 }, Counters(game));
        }

        // --- K-26 truck queue with Undo -----------------------------------------------------------------------------------

        [Test(Description = "E-03 K-26 K-39 a yard move that opens room delivers the queued O4 (Kamyonda 1 → 0); Undo puts it back into the queue; redoing delivers it again")]
        public void E03_K26_K39_a_yard_move_delivers_the_queued_block_Undo_puts_it_back_redo_delivers_it_again()
        {
            var game = GameSession.Start(Builders.Compiled(E03()));
            var shift = game.Apply(D(0, 6, 8));
            Assert.AreEqual(1, Find<DeliveryQueuedEvent>(shift.Events).Queued);
            CollectionAssert.AreEqual(new[] { 14 }, QueueIds(game.State));
            CollectionAssert.AreEqual(new[] { 14 }, shift.Summary.Queue.Select(q => q.PieceId).ToArray());
            var before = Snapshot(game.State);
            var open = game.Apply(D(13, 5, 7));
            CollectionAssert.AreEqual(new[] { 14 }, Find<DeliveryArrivedEvent>(open.Events).Pieces);
            Assert.AreEqual(0, Find<DeliveryQueuedEvent>(open.Events).Queued);
            CollectionAssert.IsEmpty(open.Summary.Queue);
            Assert.IsTrue(game.Undo());
            CollectionAssert.AreEqual(before, game.State.Buf);
            CollectionAssert.AreEqual(new[] { 14 }, QueueIds(game.State));
            Assert.AreEqual(Zone.Queue, PieceZone(game.State, 14));
            var again = game.Apply(D(13, 5, 7));
            Assert.AreEqual(Moves.EventLogHash(open.Events), Moves.EventLogHash(again.Events));
            CollectionAssert.AreEqual(new[] { 18, 2, 2 }, Counters(game));
        }

        [Test(Description = "K-48 K-28 K-39 the last segment completes while an O4 waits in the truck: no win; Undo and redo give the same non-winning move")]
        public void K48_K28_K39_the_truck_queue_holds_back_the_win_Undo_and_redo_give_the_same_non_winning_move()
        {
            var game = GameSession.Start(Builders.Compiled(WIN_WITH_TRUCK()));
            game.Commit(D(0, 6, 8));
            CollectionAssert.AreEqual(new[] { 14 }, QueueIds(game.State));
            var last = game.Apply(D(13, 6, 8));
            Assert.AreEqual(1, Find<SegmentCompletedEvent>(last.Events).Seg);
            Assert.IsFalse(last.Won);
            Assert.AreEqual(BoosterOutcomes.Playing, game.Outcome);
            Assert.AreEqual(1, last.Summary.Queue.Length, "Kamyonda: 1");
            Assert.IsTrue(game.Undo());
            var again = game.Apply(D(13, 6, 8));
            Assert.IsFalse(again.Won);
            Assert.AreEqual(Moves.EventLogHash(last.Events), Moves.EventLogHash(again.Events));
            CollectionAssert.AreEqual(new[] { 14 }, QueueIds(game.State));
        }

        // --- K-29 offers with Undo ----------------------------------------------------------------------------------------

        [Test(Description = "K-29 K-39 Undo is closed in both out-of-moves windows, open again after the accepted offer's next drag; offer 2 has no ad; declining loses (7 moves, 1 offer)")]
        public void K29_K39_Undo_closed_in_the_windows_open_after_the_offer_s_next_drag_offer_2_without_ad_decline_loses()
        {
            var game = GameSession.Start(Builders.Compiled(STREAK(2)));
            game.Commit(D(0, 6, 8));
            Assert.IsTrue(game.Apply(D(5, 5, 1)).OutOfMoves);
            Assert.AreEqual(UndoBlocks.LossWindow, game.Apply(new UndoAction()).Reason);
            Assert.AreEqual(MoveStatuses.Applied, game.AcceptOffer(AddMovesSource.OfferCoins).Status);
            CollectionAssert.AreEqual(new[] { 5, 2, 2 }, Counters(game));
            game.Commit(D(1, 7, 8));
            Assert.IsTrue(game.Undo());
            CollectionAssert.AreEqual(new[] { 5, 2, 2 }, Counters(game));
            game.Commit(D(1, 7, 8));
            foreach (int y in new[] { 0, 1, 0, 1 }) game.Commit(D(5, 5, y));
            CollectionAssert.AreEqual(new[] { 0, 7, 7 }, Counters(game));
            var offer = game.NextOffer();
            Assert.IsTrue(offer.HasValue && offer.Value.N == 2 && !offer.Value.AdAllowed);
            Assert.AreEqual(UndoBlocks.LossWindow, game.UndoBlock());
            game.DeclineOffer();
            CollectionAssert.AreEqual(new[] { "lost", UndoBlocks.LevelOver }, new[] { game.Outcome, game.UndoBlock() });
            Assert.AreEqual(1, game.OffersUsed);
        }

        // --- K-39 Undo with the streak and the trowel -----------------------------------------------------------------------

        [Test(Description = "K-39 K-33 K-43 Undo takes the earned trowel back, depth 1 refuses a 2nd Undo, the redo earns it again; a trowel use is not undone; a wrong drop is; the exit after 4 moves is a loss")]
        public void K39_K33_K43_Undo_takes_the_trowel_back_depth_1_redo_earns_it_again_trowel_use_stays_exit_is_a_loss()
        {
            var game = GameSession.Start(Builders.Compiled(STREAK()));
            game.Commit(D(0, 6, 8));
            game.Commit(D(1, 7, 8));
            game.Commit(D(2, 6, 8));
            var fourth = game.Apply(D(3, 7, 8));
            Assert.AreEqual(1, Find<TrowelEarnedEvent>(fourth.Events).Trowels);
            Assert.IsTrue(game.Undo());
            CollectionAssert.AreEqual(new[] { 3, 0 }, new[] { Hdr(game.State, H.Combo), Hdr(game.State, H.Trowels) });
            Assert.AreEqual(UndoBlocks.NoDragMove, game.Apply(new UndoAction()).Reason);
            Assert.AreEqual(1, Find<TrowelEarnedEvent>(game.Apply(D(3, 7, 8)).Events).Trowels);
            Assert.AreEqual(MoveStatuses.Applied, game.Apply(new GoldTrowelMove(5, 6, 4)).Status);
            Assert.AreEqual(0, Hdr(game.State, H.Trowels));
            var wrong = game.Apply(D(4, 7, 8));
            Assert.IsTrue(Has<PieceBouncedEvent>(wrong.Events));
            CollectionAssert.AreEqual(new[] { 15, 5, 5 }, Counters(game));
            Assert.IsTrue(game.Undo());
            CollectionAssert.AreEqual(new[] { 16, 4, 4 }, Counters(game));
            Assert.AreEqual(Zone.Site, PieceZone(game.State, 5), "the trowel's block stays placed");
            var x = game.Exit();
            CollectionAssert.AreEqual(new object[] { "loss", 4, 4, true }, new object[] { x.Kind, x.MovesMade, x.MovesSpent, x.StreakBonusConsumed });
        }
    }
}
