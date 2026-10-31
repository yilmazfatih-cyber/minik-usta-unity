#nullable enable
// Port of tests/core/deadlock.test.ts (web repo): D3a / D2 / D3b / Söküm / D1 through K-35 step 12 (`Boards.Run` =
// TS `run`, `GameSession` for the session halves), plus direct calls where the TS tests call the helpers directly. Complete
// since the K-35 / session packages (E-58 at zero moves, E-37 undo after teardown, K-43 exit penalty, E-59 shutter phase
// restore, K-29 D1 after +5, the teardown event position and `MoveResult.TeardownCause`).
using System;
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core.Level;
using MinikUsta.Core.Obstacles;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;
using static MinikUsta.Core.Tests.Fixtures.Boards;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class DeadlockTests
    {
        /// <summary>
        /// GDD K-30 "döşeme çıkmazı" example (Ws = 2): plan bottom → top YW, YW, WW, WW; blocks D2_0 Y, D2_0 W, O4 W.
        /// Putting D2_0 W on column 0 (x 6) rows 2–3 is correct by K-16, but the O4 W then fits nowhere.
        /// </summary>
        private static LevelSpec TILING() => new LevelSpec
        {
            Wall = Wall(2),
            Site = new SizeSpec(2, 4),
            Plan = new[] { "WW", "WW", "YW", "YW" },
            Pieces = Ps(Pc("D2_0", "Y", 0, 0), Pc("D2_0", "W", 1, 0), Pc("O4_0", "W", 2, 0)),
        };

        /// <summary>The pieces after the dead-end placement, as a teardown would list them.</summary>
        private static int[] PlaceOf(GameState s, int id) => new[] { PieceZone(s, id), PieceX(s, id), PieceY(s, id) };

        /// <summary>TS `RULES_VERSION` of core/session.ts.</summary>
        private const int RulesVersion = GameSession.RulesVersion;

        // --- K-30 D3a tiling (tileRemaining) ----------------------------------------------------------------------------------------

        [Test(Description = "K-30 D3a detects the GDD tiling dead end; the start state is tileable")]
        public void K30_D3a_detects_the_GDD_tiling_dead_end()
        {
            var s = Init(TILING());
            Assert.IsTrue(Deadlock.IsFullCover(s.Lvl));
            Assert.AreEqual(TileResults.Ok, Deadlock.TileRemaining(s));
            Run(s, DragTo(0, 6, 8), noTruckHelp: true);
            Assert.AreEqual(TileResults.Ok, Deadlock.TileRemaining(s));
            Run(s, DragTo(1, 6, 8), noTruckHelp: true);
            Assert.IsTrue(HasFlag(s, 1, FlagBit.Locked)); // K-16 correct …
            Assert.AreEqual(TileResults.Dead, Deadlock.TileRemaining(s)); // … but the O4 W fits nowhere
            Assert.LessOrEqual(D3aStats.Expansions, 200);
        }

        [Test(Description = "K-30 D3a a block may wait for its neighbour column: C3 over a B1 is tileable (TECH §2R.4 note: the lowest-column shortcut would call it dead)")]
        public void K30_D3a_a_block_may_wait_for_its_neighbour_column()
        {
            // plan bottom → top: W Y / W W; C3_90 = (0,0)(0,1)(1,1) must wait for the B1 Y under its overhang
            var s = Init(new LevelSpec
            {
                Wall = Wall(2),
                Site = new SizeSpec(2, 2),
                Plan = new[] { "WW", "WY" },
                Pieces = Ps(Pc("C3_90", "W", 0, 0), Pc("B1_0", "Y", 3, 0)),
            });
            Assert.AreEqual(TileResults.Ok, Deadlock.TileRemaining(s));
            Drop(s, 1, 7, 8);
            var last = Drop(s, 0, 6, 8);
            Assert.IsTrue(last.Correct);
            Assert.IsTrue(Goals.LevelGoalsMet(s));
        }

        [Test(Description = "K-30 D3a respects batch availability: an undelivered batch k block only fills segments ≥ k")]
        public void K30_D3a_respects_batch_availability()
        {
            LevelSpec Base() => new LevelSpec { Wall = Wall(2), Site = new SizeSpec(2, 1), Plans = new[] { new[] { "WW" }, new[] { "YY" } } };
            var late = Base();
            late.Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("B1_0", "Y", 1, 0));
            late.Batches = new[] { new BatchSpec { ForSegment = 1, Pieces = Ps(Pc("B1_0", "W", 0, 8), Pc("B1_0", "Y", 1, 8)) } };
            var ls = Init(late);
            Assert.IsTrue(Deadlock.IsFullCover(ls.Lvl));
            Assert.AreEqual(-1, Deadlock.ColorImbalance(ls)); // D2 is balanced …
            Assert.AreEqual(TileResults.Dead, Deadlock.TileRemaining(ls)); // … but the second W only comes with segment 1
            var carried = Base();
            carried.Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("B1_0", "W", 1, 0), Pc("B1_0", "Y", 2, 0));
            carried.Batches = new[] { new BatchSpec { ForSegment = 1, Pieces = Ps(Pc("B1_0", "Y", 0, 8)) } };
            Assert.AreEqual(TileResults.Ok, Deadlock.TileRemaining(Init(carried))); // a Y carried early is fine (LEVELS §2.0 "taşınan malzeme")
        }

        [Test(Description = "K-30 D3a paint joker: a block whose box height fits a W6 gate may take its colour; K-30 W6 level skips D2")]
        public void K30_D3a_paint_joker()
        {
            var gate = J.O("type", "paint", "y", 0, "size", 1, "color", "R");
            var flat = Init(new LevelSpec
            {
                Wall = Wall(2, gate), Site = new SizeSpec(2, 2), Plan = new[] { "RR", "RR" }, Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("D2_90", "W", 2, 0)),
            });
            Assert.IsTrue(Deadlock.IsFullCover(flat.Lvl)); // W6: cell totals
            Assert.GreaterOrEqual(Deadlock.ColorImbalance(flat), 0); // D2 would fire …
            Assert.IsNull(Deadlock.DetectDeadlock(flat, null)); // … but W6 skips it; the joker tiles R with W
            Assert.AreEqual(TileResults.Ok, Deadlock.TileRemaining(flat));
            var tall = Init(new LevelSpec { Wall = Wall(2, gate), Site = new SizeSpec(2, 2), Plan = new[] { "RR", "RR" }, Pieces = Ps(Pc("O4_0", "W", 0, 0)) });
            Assert.AreEqual(TileResults.Dead, Deadlock.TileRemaining(tall)); // 2 rows high > gate size 1: no joker
            Assert.AreEqual(TeardownCauses.Tiling, Deadlock.DetectDeadlock(tall, null));
        }

        [Test(Description = "K-30 D3a budget exhaustion is unknown, not dead; the budget is the GDD number 20 000")]
        public void K30_D3a_budget_exhaustion_is_unknown_not_dead()
        {
            var pieces = new List<P>();
            for (int i = 0; i < 10; i++) pieces.Add(Pc("B1_0", "W", i % 6, i / 6));
            pieces.Add(Pc("D2_0", "Y", 0, 3));
            var s = Init(new LevelSpec
            {
                Wall = Wall(2), Site = new SizeSpec(2, 6), Plan = new[] { "WY", "WW", "WW", "WW", "WW", "WW" },
                // ten B1 W and a D2_0 Y: the cell totals match, but the single Y cell takes no 2-cell block (found by search)
                Pieces = pieces.ToArray(),
            });
            Assert.AreEqual(20_000, Deadlock.D3aMaxExpansions);
            Assert.AreEqual(TileResults.Dead, Deadlock.TileRemaining(s));
            Assert.Greater(D3aStats.Expansions, 1);
            Assert.AreEqual(TileResults.Unknown, Deadlock.TileRemaining(s, 1));
            Assert.AreEqual(1, D3aStats.Expansions);
        }

        [Test(Description = "K-30 D3a equals an independent oracle (every exact cover, then a bottom-up build order = an acyclic \"under\" graph) on 400 seeded plans")]
        public void K30_D3a_equals_an_independent_oracle_on_400_seeded_plans()
        {
            var rng = new Mulberry32(2026);
            int dead = 0;
            for (int n = 0; n < 400; n++)
            {
                var t = RandomTiling(rng);
                var s = Init(t.Spec);
                string want = Oracle(t.Plan, t.Blocks) ? TileResults.Ok : TileResults.Dead;
                Assert.AreEqual(want, Deadlock.TileRemaining(s), "case " + n + ": " + string.Join("/", t.Plan.Select(r => r[0] + r[1])) + " " + string.Join(",", t.Blocks.Select(b => b.Shape + b.Color)));
                if (want == TileResults.Dead) dead++;
            }
            Assert.Greater(dead, 20);
            Assert.Less(dead, 380);
        }

        // --- K-30 Söküm (teardown) ---------------------------------------------------------------------------------------------------

        [Test(Description = "K-30 teardown restores the pre-action state except moves left and moves spent; c = 0; it is the last event (GDD example)")]
        public void K30_teardown_restores_the_pre_action_state_except_moves_left_and_moves_spent()
        {
            var s = Init(TILING());
            Run(s, DragTo(0, 6, 8));
            CollectionAssert.AreEqual(new[] { 1, 1 }, new[] { Hdr(s, H.Combo), Hdr(s, H.MovesSpent) });
            var pre = Snapshot(s);
            var r = Run(s, DragTo(1, 6, 8));
            Assert.AreEqual(MoveStatuses.Applied, r.Res.Status);
            Assert.IsNull(r.Res.Reason);
            Assert.IsFalse(r.Res.Won);
            Assert.IsFalse(r.Res.OutOfMoves);
            Assert.AreEqual(TeardownCauses.Tiling, r.Res.TeardownCause);
            var last = (TeardownEvent)r.Ev[r.Ev.Count - 1];
            Assert.AreEqual(r.Ev.Count - 1, last.Seq);
            Assert.AreEqual(12, last.Step);
            Assert.AreEqual(1, last.ToTurn);
            Assert.AreEqual(TeardownCauses.Tiling, last.Cause);
            Assert.AreEqual(1, last.Pieces.Length);
            Assert.AreEqual(1, last.Pieces[0].PieceId);
            Assert.AreEqual(Site(6, 2, 0), last.Pieces[0].From.At);
            Assert.AreEqual(YardAt(1, 0), last.Pieces[0].To.At);
            CollectionAssert.Contains(Types(r.Ev), "placementCorrect");
            CollectionAssert.AreEqual(new[] { Zone.Yard, 1, 0 }, PlaceOf(s, 1));
            // every field but the counter, movesSpent and the streak is the pre-action value
            for (int i = 0; i < s.Buf.Length; i++)
                if (i != H.MovesLeft && i != H.MovesSpent && i != H.Combo) Assert.AreEqual(pre[i], s.Buf[i], "word " + i);
            CollectionAssert.AreEqual(new[] { 1, 18, 2, 0 }, new[] { Hdr(s, H.Turn), Hdr(s, H.MovesLeft), Hdr(s, H.MovesSpent), Hdr(s, H.Combo) });
            Assert.AreEqual("tiling", Deadlock.AnalyticsCause(r.Res.TeardownCause ?? TeardownCauses.Color));
            Assert.AreEqual("color_balance", Deadlock.AnalyticsCause(TeardownCauses.Color));
            ExpectConsistent(s);
        }

        [Test(Description = "E-26 the last 1×1 W cell with only a 2-cell W block left is a tiling dead end → Söküm takes the last placement back (no refund)")]
        public void E26_the_last_1x1_W_cell_with_only_a_2_cell_W_block_left_is_a_tiling_dead_end()
        {
            // plan bottom → top: W W / W Y; blocks D2_0 W, B1 W, B1 Y. A B1 W on (6,0) leaves (6,1) + (7,0) for the D2_0 W
            var s = Init(E26());
            var r = Run(s, DragTo(1, 6, 8));
            var td = Find<TeardownEvent>(r.Ev);
            Assert.AreEqual(TeardownCauses.Tiling, td.Cause);
            Assert.AreEqual(0, td.ToTurn);
            CollectionAssert.AreEqual(new[] { Zone.Yard, 2, 0 }, PlaceOf(s, 1));
            CollectionAssert.AreEqual(new[] { 19, 1 }, new[] { Hdr(s, H.MovesLeft), Hdr(s, H.MovesSpent) });
            Assert.AreEqual(3, Goals.BlocksLeft(s));
        }

        [Test(Description = "K-30 step 12 runs at zero moves before the offer window; E13 out-of-moves offer only after deadlock check (E-58)")]
        public void K30_step_12_runs_at_zero_moves_before_the_offer_window_E58()
        {
            var lvl = Builders.Compiled(TILING());
            var game = GameSession.Start(lvl);
            game.Commit(DragTo(0, 6, 8));
            SetHdr(game.State, H.MovesLeft, 1);
            var pack = game.Apply(DragTo(1, 6, 8));
            CollectionAssert.AreEqual(new object[] { MoveStatuses.Applied, true, TeardownCauses.Tiling }, new object[] { pack.Status, pack.OutOfMoves, pack.TeardownCause! });
            CollectionAssert.AreEqual(new[] { "outOfMoves", "teardown" }, Types(pack.Events).Skip(pack.Events.Length - 2).ToArray());
            Assert.IsNotNull(pack.Teardown);
            Assert.AreEqual(TeardownCauses.Tiling, pack.Teardown!.Cause);
            Assert.AreEqual(12, pack.Teardown.Step);
            Assert.AreEqual(BoosterOutcomes.OutOfMoves, game.Outcome);
            CollectionAssert.AreEqual(new[] { 0, 2, 1 }, new[] { game.MovesLeft, game.MovesSpent, game.MovesMade });
            // the window shows the post-Söküm board: the torn-down block counts again
            Assert.AreEqual(2, pack.Summary.BlocksLeft);
            Assert.AreEqual(new OfferInfo(1, true), game.NextOffer());
        }

        [Test(Description = "E-37 undo after teardown returns the move: the counter comes back, movesSpent drops by 1 (GDD K-30 \"Geri Al ile\")")]
        public void E37_undo_after_teardown_returns_the_move()
        {
            var game = GameSession.Start(Builders.Compiled(TILING()));
            game.Commit(DragTo(0, 6, 8));
            var before = Snapshot(game.State);
            var r = game.Commit(DragTo(1, 6, 8));
            Assert.AreEqual(TeardownCauses.Tiling, r.TeardownCause);
            CollectionAssert.AreEqual(new[] { 18, 2 }, new[] { game.MovesLeft, game.MovesSpent });
            Assert.IsTrue(game.Undo());
            CollectionAssert.AreEqual(before, game.State.Buf);
            CollectionAssert.AreEqual(new[] { 19, 1, 1 }, new[] { game.MovesLeft, game.MovesSpent, game.MovesMade });
            // E-37: the same placement goes to the same Söküm again
            Assert.AreEqual(TeardownCauses.Tiling, game.Commit(DragTo(1, 6, 8)).TeardownCause);
        }

        [Test(Description = "K-43 exit penalty uses movesSpent: a move taken back by a Söküm still makes the exit a loss (GDD K-43 example)")]
        public void K43_exit_penalty_uses_movesSpent()
        {
            var undone = GameSession.Start(Builders.Compiled(TILING()));
            undone.Commit(DragTo(0, 6, 8));
            undone.Undo(); // K-39: movesSpent back to 0
            var free = undone.Exit();
            CollectionAssert.AreEqual(new object[] { ExitKinds.Free, 0, 0 }, new object[] { free.Kind, free.MovesMade, free.MovesSpent });
            var torn = GameSession.Start(Builders.Compiled(E26()));
            Assert.AreEqual(TeardownCauses.Tiling, torn.Commit(DragTo(1, 6, 8)).TeardownCause); // m back to 0 …
            CollectionAssert.AreEqual(new[] { 0, 1 }, new[] { torn.MovesMade, torn.MovesSpent }); // … movesSpent stays 1
            var loss = torn.Exit();
            CollectionAssert.AreEqual(new object[] { ExitKinds.Loss, 0, 1 }, new object[] { loss.Kind, loss.MovesMade, loss.MovesSpent });
        }

        [Test(Description = "E-48 teardown undoes the segment shift and the delivery: the segment comes back, the batch is pending again (D3b table cause)")]
        public void E48_teardown_undoes_the_segment_shift_and_the_delivery()
        {
            var spec = new LevelSpec
            {
                Wall = Wall(2), Site = new SizeSpec(2, 1), Plans = new[] { new[] { "WW" }, new[] { "YY" } },
                Pieces = Ps(Pc("D2_90", "W", 0, 0)), Batches = new[] { new BatchSpec { ForSegment = 1, Pieces = Ps(Pc("D2_90", "Y", 2, 8)) } },
            };
            var lvl = Builders.Compiled(spec);
            var probe = CreateInitialState(lvl);
            Moves.ApplyMove(probe, DragTo(0, 6, 8), null, new ApplyOptions(noTruckHelp: true));
            CollectionAssert.AreEqual(new[] { 1, Zone.Yard }, new[] { Hdr(probe, H.ActiveSeg), PieceZone(probe, 1) });
            var table = TableOf(probe);
            var s = CreateInitialState(lvl);
            var pre = Snapshot(s);
            var sink = new ArraySink();
            var res = Moves.ApplyMove(s, DragTo(0, 6, 8), sink, new ApplyOptions(deadTable: table));
            Assert.AreEqual(TeardownCauses.Access, res.TeardownCause);
            var types = Types(sink.Events);
            foreach (string t in new[] { "segmentCompleted", "siteShifted", "deliveryArrived", "teardown" }) CollectionAssert.Contains(types, t);
            var ev = (TeardownEvent)sink.Events[sink.Events.Count - 1];
            Assert.AreEqual(TeardownCauses.Access, ev.Cause);
            Assert.AreEqual(2, ev.Pieces.Length);
            Assert.AreEqual(0, ev.Pieces[0].PieceId);
            Assert.AreEqual(Site(6, 0, 0), ev.Pieces[0].From.At);
            Assert.AreEqual(YardAt(0, 0), ev.Pieces[0].To.At);
            Assert.AreEqual(1, ev.Pieces[1].PieceId);
            Assert.AreEqual(At.ZoneYard, ev.Pieces[1].From.At!.Value.Zone);
            Assert.AreEqual(TeardownPlace.Pending, ev.Pieces[1].To.Off);
            CollectionAssert.AreEqual(new[] { 0, 0, Zone.Pending }, new[] { Hdr(s, H.ActiveSeg), Hdr(s, H.DeliveryCursor), PieceZone(s, 1) });
            for (int i = 0; i < s.Buf.Length; i++)
                if (i != H.MovesLeft && i != H.MovesSpent) Assert.AreEqual(pre[i], s.Buf[i], "word " + i);
        }

        [Test(Description = "E-59 teardown restores the shutter phase: m 3 → 4 opens the shutter, the Söküm takes m back to 3 and closes it")]
        public void E59_teardown_restores_the_shutter_phase()
        {
            var spec = TILING();
            spec.Wall = Wall(4, J.O("type", "shutter", "y", 0, "size", 1, "period", 2, "phase", 0));
            var lvl = Builders.Compiled(spec);
            var hooks = new MoveHooks(timers: new Dictionary<string, Action<RuleContext>> { ["W4"] = ShutterTick, ["K-40"] = _ => { } });
            var s = CreateInitialState(lvl);
            Run(s, DragTo(0, 6, 8), hooks); // m 1
            Run(s, DragTo(2, 2, 2), hooks); // m 2 (yard)
            Run(s, DragTo(2, 2, 0), hooks); // m 3 (yard)
            CollectionAssert.AreEqual(new[] { 3, 0 }, new[] { Hdr(s, H.Turn), GapField(s, 0, GF.Open) }); // ⌊3/2⌋ odd → closed
            var pre = Snapshot(s);
            var r = Run(s, DragTo(1, 6, 8), hooks);
            var changed = Find<GapChangedEvent>(r.Ev);
            Assert.AreEqual(10, changed.Step); // m 4 → open …
            Assert.IsTrue(changed.Open);
            Assert.AreEqual(TeardownCauses.Tiling, r.Res.TeardownCause);
            CollectionAssert.AreEqual(new[] { 3, 0 }, new[] { Hdr(s, H.Turn), GapField(s, 0, GF.Open) }); // … and closed again after the Söküm
            for (int i = 0; i < s.Buf.Length; i++)
                if (i != H.MovesLeft && i != H.MovesSpent && i != H.Combo) Assert.AreEqual(pre[i], s.Buf[i], "word " + i);
            CollectionAssert.AreEqual(new[] { 16, 4 }, new[] { Hdr(s, H.MovesLeft), Hdr(s, H.MovesSpent) });
        }

        [Test(Description = "K-30 teardown after a booster keeps the inventory spent: the hammer stays applied (meta spends it), the board comes back (D3b table)")]
        public void K30_teardown_after_a_booster_keeps_the_inventory_spent_the_board_comes_back()
        {
            var lvl = Builders.Compiled(new LevelSpec
            {
                Wall = Wall(2), Site = new SizeSpec(2, 2), Plan = new[] { "RR", "RR" }, Pieces = Ps(Pc("O4_0", "R", 0, 0), Pc("Q9_0", "W", 0, 2)),
            });
            var probe = CreateInitialState(lvl);
            Moves.ApplyMove(probe, new HammerMove(HammerTarget.Piece(1)), null, new ApplyOptions(noTruckHelp: true));
            Assert.AreEqual(Zone.Gone, PieceZone(probe, 1));
            var s = CreateInitialState(lvl);
            var pre = Snapshot(s);
            var r = Run(s, new HammerMove(HammerTarget.Piece(1)), deadTable: TableOf(probe));
            Assert.AreEqual(TeardownCauses.Access, r.Res.TeardownCause);
            CollectionAssert.AreEqual(new[] { "boosterApplied", "cargoSmashed", "teardown" }, Types(r.Ev));
            CollectionAssert.AreEqual(pre, s.Buf); // a booster spends no move: the whole buffer is back
            Assert.AreEqual(Zone.Yard, PieceZone(s, 1));
        }

        [Test(Description = "K-30 table is ignored on levelHash mismatch (and on another rules version); a matching table answers by Zobrist hash")]
        public void K30_table_is_ignored_on_levelHash_mismatch_a_matching_table_answers_by_Zobrist_hash()
        {
            var lvl = Builders.Compiled(TILING());
            var s = CreateInitialState(lvl);
            var h = Hash.HashState(s);
            string entry = h[1].ToString("x8") + h[0].ToString("x8");
            string levelHash = LevelJson.LevelHash(lvl.Data);
            var json = new DeadTableJson(levelHash, RulesVersion, true, new[] { entry });
            var table = Deadlock.DeadTableFromJson(json, levelHash, RulesVersion);
            Assert.IsNotNull(table);
            Assert.IsTrue(table!.Complete);
            Assert.IsTrue(table.Has(h[0], h[1]));
            Assert.IsFalse(table.Has(h[0] ^ 1, h[1]));
            Assert.AreEqual(TeardownCauses.Access, Deadlock.DetectDeadlock(s, table));
            Assert.IsNull(Deadlock.DeadTableFromJson(json, "0000000000000000", RulesVersion));
            Assert.IsNull(Deadlock.DeadTableFromJson(json, levelHash, RulesVersion + 1));
        }

        // --- K-30 D1 no moves ------------------------------------------------------------------------------------------------------

        /// <summary>Yard 5 × 4: four blocks under an I5 Ağır Yük, crates on top of it; two free blocks on the top row. Plan 2 × 4 W.</summary>
        private static LevelSpec JAM() => new LevelSpec
        {
            Yard = new SizeSpec(5, 4),
            Site = new SizeSpec(2, 4),
            Wall = Wall(2),
            Plan = WW(4),
            Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("B1_0", "W", 2, 0), Pc("B1_0", "W", 3, 0), Pc("B1_0", "W", 4, 0), Pc("I5_0", "W", 0, 1), Pc("D2_90", "W", 0, 3), Pc("B1_0", "W", 2, 3)),
            Obstacles = Enumerable.Range(0, 5).Select(x => Crate(x, 2, 1)).ToArray(),
        };

        /// <summary>K-35 step 12 as the pipeline calls it after a drag (the Söküm target is the buffer before the drag).</summary>
        private static (string? cause, CapturedContext ctx) Step12(GameState s, int[] pre, bool tiling)
        {
            var hooks = Registry.LevelHooks(s.Lvl);
            var ctx = ObstacleFixtures.CtxOf(s);
            string? cause = Deadlock.RunStep12(ctx.Ctx, hooks, new Step12Options(pre, tiling, Hdr(s, H.MovesLeft) <= 0, null));
            return (cause, ctx);
        }

        [Test(Description = "K-30 D1 reshuffles a jammed yard (crates stay, shapes, colours and count kept) so one drag move places a block correctly")]
        public void K30_D1_reshuffles_a_jammed_yard()
        {
            var s = Init(JAM());
            Assert.IsTrue(Deadlock.IsFullCover(s.Lvl));
            Run(s, DragTo(5, 5, 4));
            Assert.IsFalse(Deadlock.NoMoves(s));
            var before = Enumerable.Range(0, 5).Select(id => PieceShape(s, id) * 100 + PieceColor(s, id)).ToArray();
            var r = Run(s, DragTo(6, 5, 4));
            var detected = Find<DeadlockDetectedEvent>(r.Ev);
            Assert.AreEqual(DeadlockReasons.NoMoves, detected.Reason);
            Assert.AreEqual(12, detected.Step);
            var help = Find<TruckHelpEvent>(r.Ev);
            Assert.AreEqual(TruckHelpKinds.Reshuffle, help.HelpKind);
            Assert.Greater((help.Moves ?? new PieceShift[0]).Length, 0);
            Assert.IsFalse(Deadlock.NoMoves(s));
            Assert.IsTrue(Deadlock.AnyOneMoveCorrect(s));
            CollectionAssert.AreEqual(before, Enumerable.Range(0, 5).Select(id => PieceShape(s, id) * 100 + PieceColor(s, id)).ToArray());
            Assert.IsTrue(Enumerable.Range(0, 5).All(id => PieceZone(s, id) == Zone.Yard));
            CollectionAssert.IsEmpty(Grid.StateInvariantErrors(s));
        }

        [Test(Description = "K-30 D1 removes chains first: two chained blocks touching each other are freed by the truck help (Y3), no reshuffle")]
        public void K30_D1_removes_chains_first()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(2), Site = new SizeSpec(2, 2), Plan = new[] { "WW", "WW" },
                Pieces = Ps(Pc("B1_0", "W", 0, 0, new[] { "chained" }), Pc("B1_0", "W", 1, 0, new[] { "chained" }), Pc("D2_0", "W", 4, 0)),
            });
            var r = Run(s, DragTo(2, 6, 8));
            var types = Types(r.Ev);
            CollectionAssert.AreEqual(new[] { "deadlockDetected", "chainReleased", "chainReleased", "truckHelp" }, types.Skip(types.Length - 4).ToArray());
            Assert.AreEqual(TruckHelpKinds.Unchain, Find<TruckHelpEvent>(r.Ev).HelpKind);
            CollectionAssert.AreEqual(new[] { false, false }, new[] { HasFlag(s, 0, FlagBit.Chained), HasFlag(s, 1, FlagBit.Chained) });
        }

        [Test(Description = "K-30 D1 not run at zero moves; K-29 after +5 only D1 runs once (E-42)")]
        public void K30_D1_is_not_run_at_zero_moves()
        {
            var spec = JAM();
            spec.Moves = 2;
            var lvl = Builders.Compiled(spec);
            var game = GameSession.Start(lvl);
            game.Commit(DragTo(5, 5, 4));
            var last = game.Apply(DragTo(6, 5, 4)); // counter 0 → no D1 help
            Assert.IsTrue(last.OutOfMoves);
            CollectionAssert.DoesNotContain(Types(last.Events), "truckHelp");
            Assert.IsTrue(Deadlock.NoMoves(game.State, game.Hooks.Drag));
            CollectionAssert.IsEmpty(last.Summary.Holdable);
            var offer = game.Apply(new AddMovesMove(5, AddMovesSource.OfferCoins));
            CollectionAssert.AreEqual(new[] { "movesChanged:1", "deadlockDetected:12", "truckHelp:12" }, offer.Events.Select(e => e.TypeName + ":" + e.Step).ToArray());
            Assert.Greater(offer.Summary.Holdable.Length, 0);
            CollectionAssert.AreEqual(new[] { 5, 2, 2 }, new[] { game.MovesLeft, game.MovesMade, game.MovesSpent });
        }

        // --- helpers ----------------------------------------------------------------------------------------------------------------

        private static LevelSpec E26() => new LevelSpec
        {
            Wall = Wall(2), Site = new SizeSpec(2, 2), Plan = new[] { "WY", "WW" },
            Pieces = Ps(Pc("D2_0", "W", 0, 0), Pc("B1_0", "W", 2, 0), Pc("B1_0", "Y", 3, 0)),
        };

        /// <summary>W4-like step-10 hook for the test: open ⇔ ⌊(m + phase) / period⌋ even (OBSTACLES W4); emits gapChanged.</summary>
        private static void ShutterTick(RuleContext ctx)
        {
            for (int i = 0; i < ctx.Lvl.Gaps.Length; i++)
            {
                var g = ctx.Lvl.Gaps[i];
                if (g.Type != "shutter") continue;
                int open = (Hdr(ctx.S, H.Turn) + g.Phase) / g.Period % 2 == 0 ? 1 : 0;
                if (GapField(ctx.S, i, GF.Open) != open)
                {
                    SetGapField(ctx.S, i, GF.Open, open);
                    ctx.Emit(new GapChangedEvent(i, open == 1, g.Y));
                }
            }
        }

        private sealed class OneEntryTable : IDeadTable
        {
            private readonly uint lo;
            private readonly uint hi;

            public OneEntryTable(uint lo, uint hi)
            {
                this.lo = lo;
                this.hi = hi;
            }

            public bool Complete => true;
            public bool Has(uint a, uint b) => a == lo && b == hi;
        }

        /// <summary>A one-entry D3b table holding the state `s`.</summary>
        private static IDeadTable TableOf(GameState s)
        {
            var h = Hash.HashState(CloneState(s));
            return new OneEntryTable(h[0], h[1]);
        }

        // --- the D3a oracle ----------------------------------------------------------------------------------------------------------

        private static readonly string[] OracleShapes = { "B1_0", "D2_0", "D2_90", "O4_0", "C3_0", "C3_90", "C3_180", "C3_270" };
        private static readonly string[] OracleColors = { "W", "Y" };

        private sealed class OracleBlock
        {
            public readonly string Shape;
            public readonly string Color;

            public OracleBlock(string shape, string color)
            {
                Shape = shape;
                Color = color;
            }
        }

        private sealed class Tiling
        {
            public LevelSpec Spec = new LevelSpec();
            public string[][] Plan = new string[0][];
            public List<OracleBlock> Blocks = new List<OracleBlock>();
        }

        private sealed class Fit
        {
            public readonly string Shape;
            public readonly int Ax;
            public readonly int Ay;

            public Fit(string shape, int ax, int ay)
            {
                Shape = shape;
                Ax = ax;
                Ay = ay;
            }
        }

        /// <summary>
        /// A random 2-wide plan (2–4 rows) painted from a random exact cover; then, half of the time, one block's colour or
        /// shape is perturbed (a dead case is likely, but the oracle decides).
        /// </summary>
        private static Tiling RandomTiling(IRng rng)
        {
            int hs = 2 + rng.NextInt(3);
            var plan = new string[hs][];
            var covered = new bool[hs][];
            for (int y = 0; y < hs; y++)
            {
                plan[y] = new[] { "W", "W" };
                covered[y] = new bool[2];
            }
            var blocks = new List<OracleBlock>();
            for (int guard = 0; guard < 40; guard++)
            {
                int cx = -1;
                int cy = -1;
                for (int y = 0; y < hs && cx < 0; y++)
                    for (int x = 0; x < 2 && cx < 0; x++)
                        if (!covered[y][x])
                        {
                            cx = x;
                            cy = y;
                        }
                if (cx < 0) break;
                var fits = new List<Fit>();
                foreach (var id in OracleShapes)
                {
                    var sh = Shapes.ShapeById(id);
                    foreach (var c in sh.Cells)
                    {
                        int ax = cx - c.X;
                        int ay = cy - c.Y;
                        bool ok = true;
                        foreach (var d in sh.Cells)
                        {
                            int x = ax + d.X;
                            int y = ay + d.Y;
                            if (!(x >= 0 && x < 2 && y >= 0 && y < hs && !covered[y][x])) ok = false;
                        }
                        if (ok) fits.Add(new Fit(id, ax, ay));
                    }
                }
                int pickIndex = rng.NextInt(fits.Count);
                var pick = pickIndex < fits.Count ? fits[pickIndex] : new Fit("B1_0", cx, cy);
                string color = OracleColors[rng.NextInt(2)];
                foreach (var d in Shapes.ShapeById(pick.Shape).Cells)
                {
                    plan[pick.Ay + d.Y][pick.Ax + d.X] = color;
                    covered[pick.Ay + d.Y][pick.Ax + d.X] = true;
                }
                blocks.Add(new OracleBlock(pick.Shape, color));
            }
            if (rng.Next() < 0.5 && blocks.Count > 0)
            {
                int i = rng.NextInt(blocks.Count);
                var b = blocks[i];
                var twin = OracleShapes.Where(id => Shapes.ShapeById(id).CellCount == Shapes.ShapeById(b.Shape).CellCount).ToArray();
                if (rng.Next() < 0.5) blocks[i] = new OracleBlock(b.Shape, b.Color == "W" ? "Y" : "W");
                else
                {
                    int ti = rng.NextInt(twin.Length);
                    blocks[i] = new OracleBlock(ti < twin.Length ? twin[ti] : b.Shape, b.Color);
                }
            }
            // the blocks wait in a 6 × 8 yard, one per 2 × 2 slot
            var pieces = new P[blocks.Count];
            for (int i = 0; i < blocks.Count; i++) pieces[i] = Pc(blocks[i].Shape, blocks[i].Color, (i % 3) * 2, (i / 3) * 2);
            var rows = new string[hs];
            for (int y = 0; y < hs; y++) rows[hs - 1 - y] = plan[y][0] + plan[y][1];
            return new Tiling
            {
                Spec = new LevelSpec { Wall = Wall(2), Site = new SizeSpec(2, hs), Plan = rows, Pieces = pieces },
                Plan = plan,
                Blocks = blocks,
            };
        }

        private sealed class PlacedOracle
        {
            public readonly int Block;
            public readonly int Ax;
            public readonly int Ay;

            public PlacedOracle(int block, int ax, int ay)
            {
                Block = block;
                Ax = ax;
                Ay = ay;
            }
        }

        /// <summary>Every exact cover of the plan by the blocks (colours matching), then K-34: the "sits on" graph has no cycle.</summary>
        private static bool Oracle(string[][] plan, List<OracleBlock> blocks)
        {
            int hs = plan.Length;
            int[][] owner = new int[hs][];
            for (int y = 0; y < hs; y++) owner[y] = new[] { -1, -1 };
            bool[] used = new bool[blocks.Count];
            List<PlacedOracle> placed = new List<PlacedOracle>();
            bool Buildable()
            {
                // edge a → b: a covers the cell right under b's bottom cell of some column
                int n = placed.Count;
                List<int>[] deps = new List<int>[n];
                for (int i = 0; i < n; i++) deps[i] = new List<int>();
                for (int i = 0; i < n; i++)
                {
                    var p = placed[i];
                    var sh = Shapes.ShapeById(blocks[p.Block].Shape);
                    for (int c = 0; c < sh.W; c++)
                    {
                        int y = p.Ay + sh.ColBottom[c] - 1;
                        if (y < 0) continue;
                        int o = owner[y][p.Ax + c];
                        int j = placed.FindIndex(q => q.Block == o);
                        if (j >= 0) deps[i].Add(j);
                    }
                }
                int[] state = new int[n];
                bool Cyclic(int i)
                {
                    if (state[i] == 1) return true;
                    if (state[i] == 2) return false;
                    state[i] = 1;
                    foreach (int j in deps[i]) if (Cyclic(j)) return true;
                    state[i] = 2;
                    return false;
                }
                for (int i = 0; i < n; i++) if (Cyclic(i)) return false;
                return true;
            }
            bool Search()
            {
                int cx = -1;
                int cy = -1;
                for (int y = 0; y < hs && cx < 0; y++)
                    for (int x = 0; x < 2 && cx < 0; x++)
                        if (owner[y][x] < 0)
                        {
                            cx = x;
                            cy = y;
                        }
                if (cx < 0) return used.All(u => u) && Buildable();
                for (int b = 0; b < blocks.Count; b++)
                {
                    if (used[b]) continue;
                    var blk = blocks[b];
                    var sh = Shapes.ShapeById(blk.Shape);
                    foreach (var c in sh.Cells)
                    {
                        int ax = cx - c.X;
                        int ay = cy - c.Y;
                        bool ok = true;
                        foreach (var d in sh.Cells)
                        {
                            int x = ax + d.X;
                            int y = ay + d.Y;
                            if (!(x >= 0 && x < 2 && y >= 0 && y < hs && owner[y][x] < 0 && plan[y][x] == blk.Color)) ok = false;
                        }
                        if (!ok) continue;
                        foreach (var d in sh.Cells) owner[ay + d.Y][ax + d.X] = b;
                        used[b] = true;
                        placed.Add(new PlacedOracle(b, ax, ay));
                        bool found = Search();
                        placed.RemoveAt(placed.Count - 1);
                        used[b] = false;
                        foreach (var d in sh.Cells) owner[ay + d.Y][ax + d.X] = -1;
                        if (found) return true;
                    }
                }
                return false;
            }
            return Search();
        }
    }
}
