#nullable enable
// Levels v3 block traits and shapes (docs/GDD.md §16 ÖNERİ K-56…K-66, "K-30 değişikliği"; OBSTACLES "Bölümler v3 blok özellikleri"
// Y9–Y13; LEVELS §8.10 U-v3-0/1): the C# twin of tests/core/levelsV3.test.ts. The diff fuzz (DiffFuzzTests, `v3` boards + the shipped v3
// levels) proves bit equality with the TypeScript core; these tests state the rules in words. Every test name carries its rule id.
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core.Json;
using MinikUsta.Core.Level;
using MinikUsta.Core.Obstacles;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;
using static MinikUsta.Core.Tests.Fixtures.Boards;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class LevelsV3Tests
    {
        private static readonly SizeSpec Yard = new SizeSpec(4, 4);
        private static readonly SizeSpec SiteSize = new SizeSpec(2, 4);

        private static GameState V3(LevelSpec spec)
        {
            spec.SchemaVersion = 3;
            return Builders.InitialState(spec);
        }

        private static string[] Codes(LevelSpec spec)
        {
            spec.SchemaVersion = 3;
            return Logic.ValidateLevelJson(Builders.LevelJson(spec), new LogicContext()).Issues.Select(i => i.Code).ToArray();
        }

        private static Dictionary<int, JsonValue> Tr(params (int, JsonValue)[] items) => items.ToDictionary(i => i.Item1, i => i.Item2);
        private static JsonValue Colors(string c) => J.O("colors", c);
        private static JsonValue StackOf(int id, int z) => J.O("stack", J.O("id", id, "z", z));

        [Test(Description = "K-43 RULES_VERSION 4 (levels v3): a v3 client voids an attempt saved with an older rules version")]
        public void K43_rules_version_is_4_for_levels_v3() => Assert.AreEqual(4, GameSession.RulesVersion);

        // --- K-56 shapes --------------------------------------------------------------------------------------------------

        [Test(Description = "K-56 the 2-wide orientations have the contract cells and the ASCII drawing of a shape (top → bottom, two-tone colours)")]
        public void K56_shape_cells_and_ascii_drawing()
        {
            string Cells(string id) => string.Join(" ", Shapes.ShapeById(id).Cells.Select(c => c.X + "," + c.Y));
            Assert.AreEqual("0,0 0,1", Cells("D2_0"));
            Assert.AreEqual("0,0 1,0 0,1", Cells("C3_0"));
            Assert.AreEqual("0,0 1,0 0,1 0,2", Cells("L4_0"));
            Assert.AreEqual("1,0 1,1 0,2 1,2", Cells("L4_180"));
            Assert.AreEqual("0,0 0,1 1,1 0,2", Cells("T4_0"));
            Assert.AreEqual("0,0 0,1 0,2", Cells("I3_0"));
            Assert.AreEqual("XX / .X / .X", Ascii.ShapeAscii("L4_180"));
            Assert.AreEqual("X. / XX / X.", Ascii.ShapeAscii("T4_0"));
            Assert.AreEqual("WY", Ascii.ShapeAscii("D2_90", "WY"));
            Assert.AreEqual("W. / GG", Ascii.ShapeAscii("C3_0", "GGW"));
        }

        [Test(Description = "K-56 v3 unlocks by level: L4/J4 2, T4 6, I3 8, S4/Z4 11, I4 21; shape_locked by level id in a schemaVersion 3 file only")]
        public void K56_v3_unlocks_by_level_id()
        {
            foreach (var kv in new Dictionary<string, int> { { "L4", 2 }, { "J4", 2 }, { "T4", 6 }, { "I3", 8 }, { "S4", 11 }, { "Z4", 11 }, { "I4", 21 } })
                Assert.AreEqual(kv.Value, Shapes.KindFirstLevelV3[kv.Key], kv.Key);
            LevelSpec Spec(int id) => new LevelSpec
            {
                Id = id, Yard = Yard, Site = SiteSize, Wall = Wall(1), Plan = new[] { "WW", "WW", "WW", "WW" },
                Pieces = Ps(Pc("T4_0", "W", 0, 0), Pc("T4_180", "W", 2, 0)),
            };
            CollectionAssert.Contains(Codes(Spec(5)), "shape_locked");
            CollectionAssert.DoesNotContain(Codes(Spec(6)), "shape_locked");
        }

        [Test(Description = "K-56 piece_unreachable: a 3-tall shape over a wall that leaves only 2 rows (H + 2 − height < 3) and no gap")]
        public void K56_piece_unreachable()
        {
            LevelSpec Spec(int height) => new LevelSpec
            {
                Id = 8, Yard = Yard, Site = SiteSize, Wall = Wall(height), Plan = new[] { "WW", "WW", "WW", "WW" },
                Pieces = Ps(Pc("I3_0", "W", 0, 0), Pc("I3_0", "W", 1, 0), Pc("D2_0", "W", 2, 0)),
            };
            CollectionAssert.Contains(Codes(Spec(4)), "piece_unreachable");
            CollectionAssert.DoesNotContain(Codes(Spec(3)), "piece_unreachable");
        }

        // --- K-57 two-tone (Y9) -------------------------------------------------------------------------------------------

        private static LevelSpec TwoTone(params string[] plan) => new LevelSpec
        {
            Id = 5, Yard = Yard, Site = SiteSize, Plan = plan,
            Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("D2_90", "Y", 2, 0)),
            Traits = Tr((0, Colors("WY")), (1, Colors("YW"))),
        };

        [Test(Description = "K-57 every cell must land on its own colour; the wrong twin bounces with reason color; the right order wins")]
        public void K57_two_tone_is_checked_per_cell()
        {
            var s = V3(TwoTone("YW", "WY"));
            var wrong = Run(s, DragTo(1, 4, 3)); // "YW" onto the bottom row "WY"
            CollectionAssert.AreEqual(new[] { "color" }, Find<PlacementWrongEvent>(wrong.Ev).Reasons);
            Assert.AreEqual(Zone.Yard, PieceZone(s, 1));
            Assert.IsTrue(Has<PlacementCorrectEvent>(Run(s, DragTo(0, 4, 3)).Ev));
            var last = Run(s, DragTo(1, 4, 3));
            Assert.AreEqual(1, Find<PlacementCorrectEvent>(last.Ev).PieceId);
            Assert.IsTrue(last.Res.Won);
            CollectionAssert.IsEmpty(Grid.StateInvariantErrors(s));
        }

        [Test(Description = "K-57 K-47 supply counts cell colours; D3a (run time) and L-11 (validator) tile per cell")]
        public void K57_supply_and_tiling_count_cell_colours()
        {
            var s = V3(TwoTone("YW", "WY"));
            Assert.AreEqual(2, s.Lvl.Supply[0]);
            Assert.AreEqual(2, s.Lvl.Supply[1]);
            Assert.AreEqual(2, Goals.RemainingSupplyByColor(s)[0]);
            Assert.AreEqual(TileResults.Ok, Deadlock.TileRemaining(s));
            Assert.AreEqual(TileResults.Dead, Deadlock.TileRemaining(V3(TwoTone("WY", "WY")))); // balanced (W 2, Y 2) but "YW" fits no row
            var twins = TwoTone("YW", "WY", "WY", "WY");
            twins.Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("D2_90", "W", 2, 0), Pc("D2_90", "Y", 0, 1), Pc("D2_90", "Y", 2, 1));
            twins.Traits = Tr((0, Colors("WY")), (1, Colors("WY")), (2, Colors("YW")), (3, Colors("YW")));
            var codes = Codes(twins);
            CollectionAssert.DoesNotContain(codes, "cover_mismatch");
            CollectionAssert.Contains(codes, "untileable"); // two "YW" twins for a plan with one "YW" row
        }

        [Test(Description = "K-57 colors_invalid: wrong length, one colour, or `color` not the first cell colour (L-40 runs at load)")]
        public void K57_colors_invalid()
        {
            LevelSpec With(string a, string b)
            {
                var s = TwoTone("YW", "WY", "YW", "WY");
                s.Traits = Tr((0, Colors(a)), (1, Colors(b)));
                return s;
            }
            CollectionAssert.Contains(Codes(With("WYW", "YW")), "colors_invalid");
            CollectionAssert.Contains(Codes(With("WW", "YW")), "colors_invalid");
            CollectionAssert.Contains(Codes(With("YW", "YW")), "colors_invalid");
            Assert.IsFalse(Compiler.LoadLevel(Builders.LevelJson(With("YW", "YW").AsV3())).Ok, "a broken v3 level is not loaded");
        }

        [Test(Description = "K-57 K-66 the brush cannot pick a two-tone block; the crane does not turn it")]
        public void K57_K66_brush_and_crane()
        {
            var s = V3(TwoTone("YW", "WY"));
            var hooks = Registry.LevelHooks(s.Lvl);
            Assert.IsFalse(Boosters.PaintSelectable(s, 0));
            Assert.IsTrue(Boosters.CraneSelectable(s, 0, hooks));
            Assert.AreEqual(BoosterRejects.BadRotation, Boosters.CraneRejection(s, new CraneMove(0, At.ZoneSite, 4, 0, 0), hooks));
            Assert.IsNull(Boosters.CraneRejection(s, new CraneMove(0, At.ZoneSite, 4, 0, 90), hooks));
            Assert.AreEqual(BoosterRejects.NotCorrect, Boosters.CraneRejection(s, new CraneMove(1, At.ZoneSite, 4, 0, 90), hooks));
        }

        [Test(Description = "K-57 ASCII prints each two-tone cell in its own colour")]
        public void K57_ascii_prints_cell_colours()
        {
            var s = V3(TwoTone("YW", "WY"));
            StringAssert.Contains(" 0  w y y w", Ascii.ToAscii(s));
        }

        // --- K-58 pallet (Y10) --------------------------------------------------------------------------------------------

        private static LevelSpec Pallet() => new LevelSpec
        {
            Id = 3, Yard = Yard, Site = SiteSize, Plan = new[] { "GG", "GG", "WW", "WW" },
            Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("D2_90", "W", 0, 0), Pc("D2_90", "G", 2, 0), Pc("D2_90", "G", 2, 1)),
            Traits = Tr((0, StackOf(0, 0)), (1, StackOf(0, 1))),
        };

        [Test(Description = "K-58 only the top layer can be held (covered, no move); the pallet cells stay full; a lifted layer never returns")]
        public void K58_only_the_top_layer_can_be_held()
        {
            var s = V3(Pallet());
            CollectionAssert.IsEmpty(Grid.StateInvariantErrors(s));
            Assert.IsTrue(Grid.IsCovered(s, 0));
            Assert.AreEqual(2, YardOcc(s, 0, 0)); // the top layer (id 1) owns the cells
            var attempt = Movement.TryBeginDrag(s, 0);
            Assert.IsFalse(attempt.Ok);
            Assert.AreEqual(PickFailures.Covered, attempt.Reason);
            var session = Movement.BeginDrag(s, 1)!;
            Assert.IsTrue(session.IsReachable(N(0, 1)));
            Assert.IsFalse(session.IsReachable(N(1, 0)));
            var drop = session.Classify(N(0, 0));
            Assert.AreEqual(DropKinds.Cancel, drop.Kind);
            Assert.AreEqual(CancelReasons.SameSpot, drop.Reason);
            var placed = Run(s, DragTo(1, 4, 3));
            Assert.AreEqual(1, Find<PlacementCorrectEvent>(placed.Ev).PieceId);
            Assert.AreEqual(1, YardOcc(s, 0, 0)); // the layer below took the cells
            Assert.IsFalse(Grid.IsCovered(s, 0));
            Assert.IsTrue(Movement.TryBeginDrag(s, 0).Ok);
            CollectionAssert.IsEmpty(Grid.StateInvariantErrors(s));
        }

        [Test(Description = "K-58 item 6 Undo puts the layer back on its pallet (core buffer); the pallet layers never cancel in the hash")]
        public void K58_undo_restores_the_layer_and_layers_hash_apart()
        {
            var level = Compiler.Compile(Builders.Level(Pallet().AsV3()));
            var session = GameSession.Start(level);
            string before = Ascii.ToAscii(session.State, ids: true);
            session.Commit(DragTo(1, 4, 3));
            Assert.IsFalse(Grid.IsCovered(session.State, 0));
            Assert.IsTrue(session.Undo());
            Assert.IsTrue(Grid.IsCovered(session.State, 0));
            Assert.AreEqual(before, Ascii.ToAscii(session.State, ids: true));
            var lvl = session.State.Lvl;
            Assert.AreNotEqual(Hash.PieceClassExtra(lvl, 0, Zone.Yard, 0, 0), Hash.PieceClassExtra(lvl, 1, Zone.Yard, 0, 0));
            Assert.AreEqual(0, Hash.PieceClassExtra(lvl, 1, Zone.Yard, 2, 2)); // away from its pallet: an ordinary block
        }

        [Test(Description = "K-58 ASCII round trip of a pallet (ids mode `traits:` line)")]
        public void K58_ascii_round_trip_of_a_pallet()
        {
            var s = V3(Pallet());
            string text = Ascii.ToAscii(s, ids: true);
            StringAssert.Contains("traits: pallet:0,1@(0,0)", text);
            Assert.AreEqual(text, Ascii.ToAscii(Ascii.FromAscii(s.Lvl, text), ids: true));
            StringAssert.Contains("traits: pallet@(0,0)x2", Ascii.ToAscii(s));
        }

        [Test(Description = "K-58 K-66 the crane, the brush and the Golden Trowel never pick a covered layer; the hammer never targets one")]
        public void K58_K66_boosters_skip_covered_layers()
        {
            var s = V3(Pallet());
            var hooks = Registry.LevelHooks(s.Lvl);
            Assert.IsFalse(Boosters.CraneSelectable(s, 0, hooks));
            Assert.IsFalse(Boosters.PaintSelectable(s, 0));
            Assert.IsFalse(Boosters.TrowelSelectable(s, 0, hooks));
            Assert.IsNull(Boosters.HammerTargetKind(s, HammerTarget.Piece(0), hooks));
            Assert.IsTrue(Boosters.CraneSelectable(s, 1, hooks));
        }

        // --- K-59 wrap and scissors (Y11) ---------------------------------------------------------------------------------

        private static LevelSpec Wrap() => new LevelSpec
        {
            Id = 7, Yard = Yard, Site = SiteSize, Plan = new[] { "YY", "GG", "WW", "WW" },
            Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("D2_90", "W", 0, 1), Pc("D2_90", "G", 2, 0, new[] { "scissors" }), Pc("D2_90", "Y", 2, 2, new[] { "wrapped" })),
        };

        [Test(Description = "K-59 a wrapped block cannot be held (K-09 c); a wrong placement of the scissors keeps every wrap")]
        public void K59_a_wrapped_block_cannot_be_held()
        {
            var s = V3(Wrap());
            var attempt = Movement.TryBeginDrag(s, 3, Registry.LevelHooks(s.Lvl).Drag);
            Assert.IsFalse(attempt.Ok);
            Assert.AreEqual(PickFailures.Rule, attempt.Reason);
            var wrong = Run(s, DragTo(2, 4, 3)); // G onto the empty bottom row W
            CollectionAssert.AreEqual(new[] { "color" }, Find<PlacementWrongEvent>(wrong.Ev).Reasons);
            Assert.IsTrue(HasFlag(s, 3, FlagBit.Wrapped));
            CollectionAssert.DoesNotContain(Types(wrong.Ev), "unwrapped");
        }

        [Test(Description = "K-59 K-35 the correct placement of the scissors removes every wrap in step 5; Undo brings them back")]
        public void K59_scissors_remove_every_wrap_in_step_5()
        {
            var session = GameSession.Start(Compiler.Compile(Builders.Level(Wrap().AsV3())));
            Assert.AreEqual(MoveStatuses.Applied, session.Commit(DragTo(1, 4, 3)).Status);
            Assert.AreEqual(MoveStatuses.Applied, session.Commit(DragTo(0, 4, 3)).Status);
            var sink = new ArraySink();
            session.Commit(DragTo(2, 4, 3), sink);
            var un = Find<UnwrappedEvent>(sink.Events);
            Assert.AreEqual(UnwrapBys.Scissors, un.By);
            Assert.AreEqual(2, un.PieceId);
            CollectionAssert.AreEqual(new[] { 3 }, un.Pieces);
            Assert.AreEqual(5, un.Step);
            Assert.IsFalse(HasFlag(session.State, 3, FlagBit.Wrapped));
            Assert.IsFalse(HasFlag(session.State, 2, FlagBit.Scissors));
            Assert.IsTrue(Movement.TryBeginDrag(session.State, 3, session.Hooks.Drag).Ok);
            Assert.IsTrue(session.Undo());
            Assert.IsTrue(HasFlag(session.State, 3, FlagBit.Wrapped));
            Assert.IsTrue(HasFlag(session.State, 2, FlagBit.Scissors));
            session.Commit(DragTo(2, 4, 3));
            Assert.IsTrue(session.Commit(DragTo(3, 4, 3)).Won);
        }

        [Test(Description = "K-59 K-36 the hammer cuts one wrap (kind wrap), the block stays; the hammer never targets a scissors block")]
        public void K59_K36_the_hammer_cuts_one_wrap()
        {
            var s = V3(Wrap());
            var hooks = Registry.LevelHooks(s.Lvl);
            Assert.AreEqual(HammerTargetKinds.Wrap, Boosters.HammerTargetKind(s, HammerTarget.Piece(3), hooks));
            Assert.IsNull(Boosters.HammerTargetKind(s, HammerTarget.Piece(2), hooks));
            var hit = Run(s, new HammerMove(HammerTarget.Piece(3)));
            var un = Find<UnwrappedEvent>(hit.Ev);
            Assert.AreEqual(UnwrapBys.Hammer, un.By);
            CollectionAssert.AreEqual(new[] { 3 }, un.Pieces);
            Assert.AreEqual(Zone.Yard, PieceZone(s, 3));
            Assert.IsFalse(HasFlag(s, 3, FlagBit.Wrapped));
        }

        [Test(Description = "K-59 validator: scissors_missing, scissors_many, wrapped + scissors, scissors_blocked")]
        public void K59_validator_codes()
        {
            var p = Wrap().Pieces!;
            var missing = Wrap();
            missing.Pieces = Ps(p[0], p[1], Pc("D2_90", "G", 2, 0), p[3]);
            CollectionAssert.Contains(Codes(missing), "scissors_missing");
            var many = Wrap();
            many.Pieces = Ps(Pc("D2_90", "W", 0, 0, new[] { "scissors" }), p[1], p[2], p[3]);
            CollectionAssert.Contains(Codes(many), "scissors_many");
            var combo = Wrap();
            combo.Pieces = Ps(p[0], p[1], Pc("D2_90", "G", 2, 0, new[] { "scissors", "wrapped" }), Pc("D2_90", "Y", 2, 2));
            CollectionAssert.Contains(Codes(combo), "flag_combo_forbidden");
            var blocked = Wrap();
            blocked.Pieces = Ps(p[0], Pc("D2_90", "W", 0, 1, new[] { "wrapped" }), p[2], Pc("D2_90", "Y", 2, 2));
            CollectionAssert.Contains(Codes(blocked), "scissors_blocked");
        }

        // --- K-60 bonus (Y12) ---------------------------------------------------------------------------------------------

        private static LevelSpec Bonus(string[] plan, int moves) => new LevelSpec
        {
            Id = 9, Yard = Yard, Site = SiteSize, Moves = moves, Plan = plan,
            Pieces = Ps(Pc("D2_90", "W", 0, 0, new[] { "bonus" }), Pc("D2_90", "W", 2, 0)),
        };

        [Test(Description = "K-60 K-35 a correct placement adds bonusMoves in step 4 after the cost: a last move that hits 0 goes on")]
        public void K60_bonus_moves_are_added_in_step_4()
        {
            var s = V3(Bonus(new[] { "WW", "WW" }, 1));
            var r = Run(s, DragTo(0, 4, 3));
            var changes = OfType<MovesChangedEvent>(r.Ev).ToArray();
            CollectionAssert.AreEqual(
                new[] { "move:-1:0:4", "bonus:2:2:4" },
                changes.Select(e => e.Reason + ":" + e.Delta + ":" + e.MovesLeft + ":" + e.Step).ToArray());
            Assert.IsFalse(r.Res.OutOfMoves);
            Assert.AreEqual(2, Hdr(s, H.MovesLeft));
            Assert.IsFalse(HasFlag(s, 0, FlagBit.Bonus));
        }

        [Test(Description = "K-60 a wrong placement gives nothing: the counter reaches 0 → out of moves")]
        public void K60_a_wrong_placement_gives_no_bonus()
        {
            var spec = Bonus(new[] { "YY", "WW" }, 1);
            spec.Pieces = Ps(Pc("D2_90", "Y", 0, 0, new[] { "bonus" }), Pc("D2_90", "W", 2, 0));
            var s = V3(spec);
            var r = Run(s, DragTo(0, 4, 3));
            CollectionAssert.Contains(Types(r.Ev), "placementWrong");
            Assert.IsTrue(r.Res.OutOfMoves);
            Assert.IsTrue(HasFlag(s, 0, FlagBit.Bonus));
        }

        [Test(Description = "K-60 bonusMoves 1–3 (default 2); bonus_many above 2; bonus + wet is forbidden; bonusMoves without bonus is bonus_invalid")]
        public void K60_bonus_moves_and_validator_codes()
        {
            var three = Bonus(new[] { "WW", "WW" }, 5);
            three.Traits = Tr((0, J.O("bonusMoves", 3)));
            var s = V3(three);
            Run(s, DragTo(0, 4, 3));
            Assert.AreEqual(7, Hdr(s, H.MovesLeft));
            var many = Bonus(new[] { "WW", "WW", "WW", "WW" }, 9);
            many.Pieces = Ps(Pc("D2_90", "W", 0, 0, new[] { "bonus" }), Pc("D2_90", "W", 2, 0, new[] { "bonus" }), Pc("D2_90", "W", 0, 1, new[] { "bonus" }), Pc("D2_90", "W", 2, 1));
            CollectionAssert.Contains(Codes(many), "bonus_many");
            var wet = Bonus(new[] { "WW", "WW", "WW", "WW" }, 9);
            wet.Pieces = Ps(Pc("D2_90", "W", 0, 0, new[] { "bonus", "wet" }, 3));
            CollectionAssert.Contains(Codes(wet), "flag_combo_forbidden");
            var invalid = Bonus(new[] { "WW", "WW", "WW", "WW" }, 9);
            invalid.Traits = Tr((1, J.O("bonusMoves", 2)));
            CollectionAssert.Contains(Codes(invalid), "bonus_invalid");
        }

        [Test(Description = "K-60 K-33 a Golden Trowel placement of a bonus block also pays the bonus (mini pipeline step 4)")]
        public void K60_K33_a_golden_trowel_placement_pays_the_bonus()
        {
            var s = V3(Bonus(new[] { "WW", "WW" }, 3));
            s.Buf[H.Trowels] = 1;
            var r = Run(s, new GoldTrowelMove(0, 4, 0));
            var ch = Find<MovesChangedEvent>(r.Ev);
            Assert.AreEqual(MovesChangedReasons.Bonus, ch.Reason);
            Assert.AreEqual(2, ch.Delta);
            Assert.AreEqual(4, ch.Step);
            Assert.AreEqual(5, Hdr(s, H.MovesLeft));
        }

        // --- K-61 slippery (Y13) ------------------------------------------------------------------------------------------

        [Test(Description = "K-61 K-35 a slippery block parked over a gap falls in step 6 even without yard gravity; a plain block stays")]
        public void K61_a_slippery_block_falls_without_yard_gravity()
        {
            LevelSpec Spec(string[]? flags) => new LevelSpec
            {
                Id = 13, Yard = Yard, Site = SiteSize, Plan = new[] { "GG", "WW", "WW", "WW" },
                Pieces = Ps(Pc("D2_90", "W", 0, 0, flags), Pc("D2_90", "W", 2, 1)),
            };
            var s = V3(Spec(new[] { "slippery" }));
            Assert.IsFalse(s.Lvl.Gravity.Yard);
            var r = Run(s, DragTo(0, 2, 3)); // (2,2)–(3,2) empty, (2,1)–(3,1) full
            var fell = Find<PieceFellEvent>(r.Ev);
            Assert.AreEqual(0, fell.PieceId);
            Assert.AreEqual(FallCauses.YardGravity, fell.Cause);
            Assert.AreEqual(6, fell.Step);
            Assert.AreEqual(2, PieceY(s, 0));
            var plain = V3(Spec(null));
            Run(plain, DragTo(0, 2, 3));
            Assert.AreEqual(3, PieceY(plain, 0));
            CollectionAssert.IsEmpty(Grid.StateInvariantErrors(s));
        }

        // --- K-30 D1 widening, schema, mechanics --------------------------------------------------------------------------

        [Test(Description = "K-30 K-66 Y9 … Y13 are derived from the data, v3 fields in a v2 file are schema_version_low (L-40), events list 44 names")]
        public void K30_mechanics_derivation_and_schema_version_low()
        {
            var wrap = Builders.Level(Wrap().AsV3());
            CollectionAssert.AreEqual(new[] { "Y11" }, Mechanics.DeriveMechanics(wrap).Where(m => m.StartsWith("Y1") && m.Length == 3).ToArray());
            var v2 = Wrap();
            v2.SchemaVersion = 2;
            var codes = Logic.ValidateLevelJson(Builders.LevelJson(v2), new LogicContext(only: Logic.RuntimeChecks)).Issues.Select(i => i.Code).ToArray();
            CollectionAssert.Contains(codes, "schema_version_low");
            Assert.AreEqual(44, EventTypes.All.Length);
            Assert.AreEqual("unwrapped", EventTypes.NameOf(EventKind.Unwrapped));
        }

        [Test(Description = "K-35 the unwrapped event serializes with by, pieceId and the id list; bonus movesChanged carries reason bonus")]
        public void K35_unwrapped_and_bonus_event_json()
        {
            var o = EventJson.ToJson(new UnwrappedEvent(UnwrapBys.Scissors, 2, new[] { 3, 4 }));
            Assert.AreEqual("unwrapped", o["t"]!.AsString);
            Assert.AreEqual("scissors", o["by"]!.AsString);
            Assert.AreEqual(2, o["pieceId"]!.AsInt);
            Assert.AreEqual(2, o["pieces"]!.Items.Count);
            var b = EventJson.ToJson(new MovesChangedEvent(5, 2, MovesChangedReasons.Bonus));
            Assert.AreEqual("bonus", b["reason"]!.AsString);
        }
    }

    internal static class LevelSpecV3Extensions
    {
        public static LevelSpec AsV3(this LevelSpec spec)
        {
            spec.SchemaVersion = 3;
            return spec;
        }
    }
}
