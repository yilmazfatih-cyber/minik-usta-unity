#nullable enable
// Port of tests/core/session.test.ts (web repo): `GameSession` — K-39 Undo, K-29 +5 offers, K-43 exit / resume / replay,
// K-40 level start bonuses, `levelHash` and `RulesVersion`.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MinikUsta.Core.Json;
using MinikUsta.Core.Level;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;
using static MinikUsta.Core.Tests.Fixtures.Boards;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class SessionTests
    {
        /// <summary>Four W rows, four W dominoes, a Y single (wrong anywhere) and a W single that shuttles in the yard.</summary>
        private static LevelSpec SHUTTLE(int? moves = null) => new LevelSpec
        {
            Moves = moves ?? 12,
            Wall = Wall(2),
            Plan = WW(4),
            Pieces = Ps(Pc("D2_0", "W", 0, 0), Pc("D2_0", "W", 1, 0), Pc("D2_0", "W", 2, 0), Pc("D2_0", "W", 3, 0), Pc("B1_0", "Y", 4, 0), Pc("B1_0", "W", 5, 0)),
        };

        /// <summary>A full-cover board (K-47): plan 2 × 2 W, two D2_0 W blocks.</summary>
        private static LevelSpec FULL() => new LevelSpec
        {
            Wall = Wall(2),
            Plan = new[] { "WW", "WW" },
            Pieces = Ps(Pc("D2_0", "W", 0, 0), Pc("D2_0", "W", 1, 0)),
        };

        /// <summary>Full cover, four W rows and four W dominoes.</summary>
        private static LevelSpec FULL4() => new LevelSpec
        {
            Wall = Wall(2),
            Plan = WW(4),
            Pieces = Ps(Pc("D2_0", "W", 0, 0), Pc("D2_0", "W", 1, 0), Pc("D2_0", "W", 2, 0), Pc("D2_0", "W", 3, 0)),
        };

        private static Move Hand(int level, int i) => new DragMove(HandMoves(level)[i].PieceId, HandMoves(level)[i].To);

        private static IEnumerable<Move> HandAll(int level) => HandMoves(level).Select(m => (Move)new DragMove(m.PieceId, m.To));

        private static string EventsText(IEnumerable<GameEvent> ev) => JsonWriter.Canonical(EventJson.ToJson(ev));

        /// <summary>`n` yard moves of the W single between (5,0) and (5,1).</summary>
        private static void Shuttle(GameSession session, int n)
        {
            for (int i = 0; i < n; i++)
            {
                int y = PieceY(session.State, 5) == 0 ? 1 : 0;
                Assert.AreEqual(MoveStatuses.Applied, session.Commit(DragTo(5, 5, y)).Status);
            }
        }

        /// <summary>Ends the moves with one yard move (counter forced to 1 first).</summary>
        private static void RunOut(GameSession session)
        {
            SetHdr(session.State, H.MovesLeft, 1);
            Shuttle(session, 1);
        }

        private static readonly StartAction PlainStart = new StartAction(new string[0], 0);

        // --- K-39 Undo ----------------------------------------------------------------------------------------------------------

        [Test(Description = "K-39 GDD example: after a wrong placement (9 → 8, c 3 → 0) Undo gives back 9, c = 3 and the start cell")]
        public void K39_GDD_example_Undo_gives_back_the_counter_the_streak_and_the_start_cell()
        {
            var session = GameSession.Start(Builders.Compiled(SHUTTLE()));
            foreach (var m in new[] { DragTo(0, 6, 8), DragTo(1, 7, 8), DragTo(2, 6, 8) }) session.Commit(m);
            var s = session.State;
            CollectionAssert.AreEqual(new[] { 9, 3 }, new[] { session.MovesLeft, Hdr(s, H.Combo) });
            var wrong = session.Commit(DragTo(4, 7, 8));
            Assert.AreEqual(MoveStatuses.Applied, wrong.Status);
            CollectionAssert.AreEqual(new[] { 8, 0, 4 }, new[] { session.MovesLeft, Hdr(s, H.Combo), session.MovesMade });
            Assert.IsTrue(session.CanUndo());
            Assert.IsTrue(session.Undo());
            CollectionAssert.AreEqual(new[] { 9, 3, 3 }, new[] { session.MovesLeft, Hdr(s, H.Combo), session.MovesMade });
            CollectionAssert.AreEqual(new[] { Zone.Yard, 4, 0 }, new[] { PieceZone(s, 4), PieceX(s, 4), PieceY(s, 4) });
            Assert.AreSame(s, session.State); // restored in place
            ExpectConsistent(s);
        }

        [Test(Description = "K-39 depth 1: a second Undo needs a new drag move")]
        public void K39_depth_1_a_second_Undo_needs_a_new_drag_move()
        {
            var session = GameSession.Start(Builders.Compiled(SHUTTLE()));
            Shuttle(session, 2);
            Assert.IsTrue(session.Undo());
            Assert.IsFalse(session.Undo());
            Assert.AreEqual(UndoBlocks.NoDragMove, session.UndoBlock());
            Shuttle(session, 1);
            Assert.IsTrue(session.Undo());
            CollectionAssert.AreEqual(new[] { "start", "drag", "drag", "undo", "drag", "undo" }, session.Log.Select(a => a.Kind).ToArray());
        }

        [Test(Description = "K-39 Undo takes a correctly placed (locked) block back to the yard — the only K-14 exception")]
        public void K39_Undo_takes_a_locked_block_back_to_the_yard()
        {
            var session = GameSession.Start(LevelFile(5));
            session.Commit(Hand(5, 0)); // d shifts
            session.Commit(Hand(5, 1)); // a (O4 R) → (4,0), locked
            var s = session.State;
            Assert.AreNotEqual(0, PieceField(s, 0, PF.Flags) & FlagBit.Locked);
            session.Undo();
            CollectionAssert.AreEqual(new[] { Zone.Yard, 0, 0 }, new[] { PieceZone(s, 0), PieceX(s, 0), PieceY(s, 0) });
            Assert.AreEqual(0, PieceField(s, 0, PF.Flags) & FlagBit.Locked);
        }

        [Test(Description = "E-21 Undo of a move with a segment shift and a delivery restores everything")]
        public void E21_Undo_of_a_move_with_a_segment_shift_and_a_delivery_restores_everything()
        {
            var session = GameSession.Start(LevelFile(5));
            foreach (var m in HandAll(5).Take(4)) session.Commit(m);
            var before = Snapshot(session.State);
            var first = new ArraySink();
            session.Commit(Hand(5, 4), first); // Sol Oda complete: shift + truck (k1_0…k1_3)
            Assert.AreEqual(1, Hdr(session.State, H.ActiveSeg));
            CollectionAssert.AreEqual(Enumerable.Repeat(Zone.Yard, 4).ToArray(), new[] { 4, 5, 6, 7 }.Select(id => PieceZone(session.State, id)).ToArray());
            CollectionAssert.IsEmpty(QueueIds(session.State));
            Assert.IsTrue(session.Undo());
            CollectionAssert.AreEqual(before, session.State.Buf);
            CollectionAssert.AreEqual(Enumerable.Repeat(Zone.Pending, 4).ToArray(), new[] { 4, 5, 6, 7 }.Select(id => PieceZone(session.State, id)).ToArray());
            var again = new ArraySink();
            session.Commit(Hand(5, 4), again);
            Assert.AreEqual(EventsText(first.Events), EventsText(again.Events));
        }

        [Test(Description = "K-39 no Undo after a trowel use, after a +5 offer, in the out-of-moves window or after the win")]
        public void K39_no_Undo_after_a_trowel_use_a_plus_5_offer_in_the_window_or_after_the_win()
        {
            var trowelled = GameSession.Start(Builders.Compiled(FULL()), new[] { PreBoosters.TrowelStart });
            Assert.AreEqual(MoveStatuses.Applied, trowelled.Commit(DragTo(0, 6, 8)).Status);
            Assert.IsTrue(trowelled.CanUndo());
            Assert.AreEqual(MoveStatuses.Applied, trowelled.Commit(new GoldTrowelMove(1, 7, 0)).Status);
            Assert.AreEqual(UndoBlocks.LevelOver, trowelled.UndoBlock()); // the trowel completed the level (K-48)

            var t2 = GameSession.Start(Builders.Compiled(FULL4()), new[] { PreBoosters.TrowelStart });
            t2.Commit(DragTo(0, 6, 8));
            Assert.AreEqual(MoveStatuses.Applied, t2.Commit(new GoldTrowelMove(1, 7, 0)).Status);
            Assert.AreEqual(UndoBlocks.NoDragMove, t2.UndoBlock());

            var session = GameSession.Start(Builders.Compiled(SHUTTLE()));
            RunOut(session);
            Assert.AreEqual(BoosterOutcomes.OutOfMoves, session.Outcome);
            Assert.AreEqual(UndoBlocks.LossWindow, session.UndoBlock());
            Assert.IsFalse(session.Undo());
            session.AcceptOffer(AddMovesSource.OfferCoins);
            Assert.AreEqual(UndoBlocks.NoDragMove, session.UndoBlock());

            var won = GameSession.Start(Builders.Compiled(FULL()));
            won.Commit(DragTo(0, 6, 8));
            won.Commit(DragTo(1, 7, 8));
            CollectionAssert.AreEqual(new[] { BoosterOutcomes.Won, UndoBlocks.LevelOver }, new[] { won.Outcome, won.UndoBlock() });
            Assert.AreEqual("notPlaying", won.Commit(DragTo(0, 3, 6)).Reason);
        }

        // --- K-29 +5 offers -------------------------------------------------------------------------------------------------------

        [Test(Description = "K-29 an accepted offer sets 5 moves; m, timers and the streak stay; step 12 runs once (E-42; Faz 2R: also at 0 moves, before the window)")]
        public void K29_an_accepted_offer_sets_5_moves_and_runs_step_12_once()
        {
            var calls = new List<int>();
            var hooks = new MoveHooks(deadlock: ctx =>
            {
                calls.Add(Hdr(ctx.S, H.MovesLeft));
                ctx.Emit(new DeadlockDetectedEvent(DeadlockReasons.NoMoves));
            });
            var session = GameSession.Start(Builders.Compiled(SHUTTLE()), null, 0, new SessionOptions(hooks: hooks));
            session.Commit(DragTo(0, 6, 8)); // c = 1
            RunOut(session);
            CollectionAssert.AreEqual(new[] { 11, 0 }, calls); // after every move, the last one (counter 0) included
            int m = session.MovesMade;
            var sink = new ArraySink();
            var res = session.AcceptOffer(AddMovesSource.OfferAd, sink);
            Assert.AreEqual(MoveStatuses.Applied, res.Status);
            CollectionAssert.AreEqual(new[] { "movesChanged@1", "deadlockDetected@12" }, sink.Events.Select(e => e.TypeName + "@" + e.Step).ToArray());
            var changed = Find<MovesChangedEvent>(sink.Events);
            Assert.AreEqual(MovesChangedReasons.Offer, changed.Reason);
            CollectionAssert.AreEqual(new[] { GameSession.OfferMoves, 5 }, new[] { changed.Delta, changed.MovesLeft });
            CollectionAssert.AreEqual(new[] { 11, 0, 5 }, calls);
            CollectionAssert.AreEqual(new[] { 5, m, 1 }, new[] { session.MovesLeft, session.MovesMade, Hdr(session.State, H.Combo) });
            CollectionAssert.AreEqual(new object[] { BoosterOutcomes.Playing, 1, true }, new object[] { session.Outcome, session.OffersUsed, session.AdOfferUsed });
        }

        [Test(Description = "K-29 the ad option exists only on offer 1; after 3 offers the level is lost at once")]
        public void K29_the_ad_option_exists_only_on_offer_1_after_3_offers_the_level_is_lost()
        {
            var session = GameSession.Start(Builders.Compiled(SHUTTLE()));
            RunOut(session);
            Assert.AreEqual(new OfferInfo(1, true), session.NextOffer());
            Assert.AreEqual(MoveStatuses.Applied, session.AcceptOffer(AddMovesSource.OfferAd).Status);
            Shuttle(session, GameSession.OfferMoves);
            Assert.AreEqual(new OfferInfo(2, false), session.NextOffer());
            Assert.AreEqual("adNotAllowed", session.AcceptOffer(AddMovesSource.OfferAd).Reason);
            Assert.AreEqual(MoveStatuses.Applied, session.AcceptOffer(AddMovesSource.OfferCoins).Status);
            Shuttle(session, GameSession.OfferMoves);
            Assert.AreEqual(new OfferInfo(3, false), session.NextOffer());
            session.AcceptOffer(AddMovesSource.OfferCoins);
            Shuttle(session, GameSession.OfferMoves);
            Assert.AreEqual(3, GameSession.MaxOffers);
            Assert.AreEqual(BoosterOutcomes.Lost, session.Outcome);
            Assert.IsNull(session.NextOffer());
            Assert.AreEqual(3, session.OffersUsed);
            Assert.AreEqual(StateFlag.Lost, Hdr(session.State, H.Flags) & StateFlag.Lost);
        }

        [Test(Description = "K-29 declining the offer loses the level; no move is taken afterwards")]
        public void K29_declining_the_offer_loses_the_level()
        {
            var session = GameSession.Start(Builders.Compiled(SHUTTLE()));
            var ex = Assert.Throws<InvalidOperationException>(() => session.DeclineOffer());
            StringAssert.Contains("no out-of-moves window", ex!.Message);
            RunOut(session);
            session.DeclineOffer();
            Assert.AreEqual(BoosterOutcomes.Lost, session.Outcome);
            Assert.AreEqual("notPlaying", session.Commit(DragTo(5, 5, 0)).Reason);
            Assert.AreEqual("noOffer", session.AcceptOffer(AddMovesSource.OfferCoins).Reason);
        }

        // --- K-43 exit and resume ---------------------------------------------------------------------------------------------------

        [Test(Description = "K-43 E-41 exit at movesSpent = 0 is free: pre-level boosters come back, the streak bonus is not consumed")]
        public void K43_E41_exit_at_movesSpent_0_is_free()
        {
            var session = GameSession.Start(
                Builders.Compiled(SHUTTLE()),
                new[] { PreBoosters.Thermos, PreBoosters.TrowelStart },
                2,
                new SessionOptions(streakBonus: tier => new StreakBonus(2, 1)));
            CollectionAssert.AreEqual(new[] { 17, 2 }, new[] { session.MovesLeft, Hdr(session.State, H.Trowels) });
            var exit = session.Exit();
            Assert.AreEqual(ExitKinds.Free, exit.Kind);
            CollectionAssert.AreEqual(new[] { 0, 0 }, new[] { exit.MovesMade, exit.MovesSpent });
            CollectionAssert.AreEqual(new[] { PreBoosters.Thermos, PreBoosters.TrowelStart }, exit.RefundPreBoosters);
            Assert.IsFalse(exit.StreakBonusConsumed);
            Assert.AreEqual(BoosterOutcomes.Exited, session.Outcome);
            var ex = Assert.Throws<InvalidOperationException>(() => session.Exit());
            StringAssert.Contains("not running", ex!.Message);
        }

        [Test(Description = "K-43 exit after a move (movesSpent ≥ 1) is a loss")]
        public void K43_exit_after_a_move_is_a_loss()
        {
            var session = GameSession.Start(Builders.Compiled(SHUTTLE()), new[] { PreBoosters.Thermos });
            Shuttle(session, 1);
            var exit = session.Exit();
            Assert.AreEqual(ExitKinds.Loss, exit.Kind);
            CollectionAssert.AreEqual(new[] { 1, 1 }, new[] { exit.MovesMade, exit.MovesSpent });
            CollectionAssert.IsEmpty(exit.RefundPreBoosters);
            Assert.IsTrue(exit.StreakBonusConsumed);
            Assert.AreEqual(BoosterOutcomes.Lost, session.Outcome);
        }

        [Test(Description = "K-43 E-38 replay rebuilds the attempt bit for bit and plays on like the live session")]
        public void K43_E38_replay_rebuilds_the_attempt_bit_for_bit()
        {
            var lvl = LevelFile(5);
            var live = GameSession.Start(lvl);
            live.Commit(Hand(5, 0));
            live.Commit(Hand(5, 1));
            live.Commit(DragTo(1, 5, 5)); // b (W) onto the Y column: wrong (bounces)
            live.Undo();
            live.Commit(Hand(5, 2));
            live.Commit(Hand(5, 3));
            var resumed = GameSession.Replay(lvl, live.Log);
            Assert.IsTrue(resumed.Resumed);
            Assert.IsFalse(live.Resumed);
            CollectionAssert.AreEqual(live.State.Buf, resumed.State.Buf);
            Assert.IsTrue(SessionActionJson.SameLog(live.Log, resumed.Log));
            CollectionAssert.AreEqual(new object[] { live.CanUndo(), live.Outcome }, new object[] { resumed.CanUndo(), resumed.Outcome });
            foreach (var m in HandAll(5).Skip(4))
            {
                live.Commit(m);
                resumed.Commit(m);
            }
            CollectionAssert.AreEqual(live.State.Buf, resumed.State.Buf);
            Assert.AreEqual(BoosterOutcomes.Won, resumed.Outcome);
            Assert.AreEqual(6, resumed.MovesLeft);
            Assert.AreEqual(new YaoMeasure(8, 0, 1.0), resumed.Yao());
            Assert.AreEqual(BoosterOutcomes.Won, live.Outcome);
        }

        [Test(Description = "K-43 the out-of-moves window survives a restart with the same offer number")]
        public void K43_the_out_of_moves_window_survives_a_restart()
        {
            var lvl = Builders.Compiled(SHUTTLE());
            var live = GameSession.Start(lvl);
            Shuttle(live, 12);
            live.AcceptOffer(AddMovesSource.OfferAd);
            Shuttle(live, GameSession.OfferMoves);
            Assert.AreEqual(BoosterOutcomes.OutOfMoves, live.Outcome);
            var resumed = GameSession.Replay(lvl, live.Log);
            Assert.AreEqual(BoosterOutcomes.OutOfMoves, resumed.Outcome);
            Assert.AreEqual(new OfferInfo(2, false), resumed.NextOffer());
            Assert.IsTrue(resumed.AdOfferUsed);
        }

        [Test(Description = "K-43 a log that does not replay throws ReplayError with the action index")]
        public void K43_a_log_that_does_not_replay_throws_ReplayError()
        {
            var lvl = LevelFile(5);
            var start = PlainStart;
            Assert.Throws<ReplayError>(() => GameSession.Replay(lvl, new SessionAction[0]));
            var buried = new SessionAction[] { start, DragTo(0, 4, 5) }; // a (O4 R) lies under d (K-09)
            StringAssert.Contains("action 1", Assert.Throws<ReplayError>(() => GameSession.Replay(lvl, buried))!.Message);
            StringAssert.Contains("second start", Assert.Throws<ReplayError>(() => GameSession.Replay(lvl, new SessionAction[] { start, start }))!.Message);
            StringAssert.Contains("undo", Assert.Throws<ReplayError>(() => GameSession.Replay(lvl, new SessionAction[] { start, new UndoAction() }))!.Message);
            StringAssert.Contains("offer", Assert.Throws<ReplayError>(() =>
                GameSession.Replay(lvl, new SessionAction[] { start, new AddMovesMove(GameSession.OfferMoves, AddMovesSource.OfferAd) }))!.Message);
            try
            {
                GameSession.Replay(lvl, buried);
                Assert.Fail("no throw");
            }
            catch (ReplayError e)
            {
                Assert.AreEqual(1, e.Index);
            }
        }

        [Test(Description = "K-43 seeded random play: every state stays consistent and the replay equals the live session")]
        public void K43_seeded_random_play_replay_equals_the_live_session()
        {
            var lvl = LevelFile(5);
            for (int seed = 1; seed <= 12; seed++)
            {
                var rng = new Mulberry32(seed);
                var live = GameSession.Start(lvl);
                for (int step = 0; step < 60 && live.Outcome != BoosterOutcomes.Won && live.Outcome != BoosterOutcomes.Lost; step++)
                {
                    if (live.Outcome == BoosterOutcomes.OutOfMoves)
                    {
                        if (live.AcceptOffer(AddMovesSource.OfferCoins).Status != MoveStatuses.Applied) break;
                        continue;
                    }
                    if (live.CanUndo() && rng.Next() < 0.1)
                    {
                        live.Undo();
                        continue;
                    }
                    int P = lvl.Layout.Counts.Pieces;
                    var sessions = new List<DragSession>();
                    for (int id = 0; id < P; id++)
                    {
                        var a = Movement.TryBeginDrag(live.State, id);
                        if (a.Ok) sessions.Add(a.Session!);
                    }
                    if (sessions.Count == 0) break;
                    var drag = sessions[rng.NextInt(sessions.Count)];
                    var nodes = drag.ReachableNodes();
                    var to = nodes.Length > 0 ? nodes[rng.NextInt(nodes.Length)] : drag.Start;
                    live.Commit(new DragMove(drag.PieceId, to), new ArraySink());
                    ExpectConsistent(live.State);
                }
                var resumed = GameSession.Replay(lvl, live.Log);
                CollectionAssert.AreEqual(live.State.Buf, resumed.State.Buf, "seed " + seed);
                CollectionAssert.AreEqual(
                    new object[] { live.Outcome, live.OffersUsed, live.CanUndo() },
                    new object[] { resumed.Outcome, resumed.OffersUsed, resumed.CanUndo() },
                    "seed " + seed);
            }
        }

        // --- K-40 level start and versioning -----------------------------------------------------------------------------------------

        [Test(Description = "K-40 Termos +3 moves, Mala Başlangıcı +1 trowel, Açık Kepenk for the first 5 moves; m stays 0")]
        public void K40_Termos_Mala_Baslangici_Acik_Kepenk_m_stays_0()
        {
            var sink = new ArraySink();
            var pre = new[] { PreBoosters.Thermos, PreBoosters.TrowelStart, PreBoosters.OpenShutter };
            var session = GameSession.Start(
                Builders.Compiled(SHUTTLE()),
                pre,
                3,
                new SessionOptions(streakBonus: tier => new StreakBonus(tier, 2)),
                sink);
            var s = session.State;
            CollectionAssert.AreEqual(new[] { 18, 3, 5, 0 }, new[] { session.MovesLeft, Hdr(s, H.Trowels), Hdr(s, H.OpenShutterUntil), session.MovesMade });
            CollectionAssert.AreEqual(
                new[] { "booster/3", "booster/3" },
                sink.Events.Select(e => e is MovesChangedEvent mc ? mc.Reason + "/" + mc.Delta : e.TypeName).ToArray());
            var log = session.Log;
            Assert.AreEqual(1, log.Count);
            Assert.IsTrue(SessionActionJson.SameAs(new StartAction(pre, 3), log[0]));
            var ex = Assert.Throws<InvalidOperationException>(() => GameSession.Start(Builders.Compiled(SHUTTLE()), null, 1));
            StringAssert.Contains("streakBonus", ex!.Message);
        }

        [Test(Description = "K-43 levelHash pins the level data; RULES_VERSION is a positive integer")]
        public void K43_levelHash_pins_the_level_data_RulesVersion_is_a_positive_integer()
        {
            var a = Builders.Compiled(SHUTTLE());
            Assert.IsTrue(new Regex("^[0-9a-f]{16}$").IsMatch(GameSession.LevelHash(a.Data)));
            Assert.AreEqual(GameSession.LevelHash(a.Data), GameSession.LevelHash(Builders.Compiled(SHUTTLE()).Data));
            Assert.AreNotEqual(GameSession.LevelHash(a.Data), GameSession.LevelHash(Builders.Compiled(SHUTTLE(13)).Data));
            Assert.GreaterOrEqual(GameSession.RulesVersion, 1);
        }
    }
}
