#nullable enable
// Paket E3: how the v3 block traits (GDD §16: two-tone Y9, pallet Y10, wrap + scissors Y11, bonus Y12, slippery Y13) are drawn and
// animated (docs/ART_UNITY.md section 9). Pure Presentation.Core: block parts from `BlockSkin.Block` + `BlockTraits`, the pooled
// visuals through `PieceViews.Sync`, the unwrap / bonus steps through `LevelPlayer` on the shipped v3 levels 7 and 9.
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core;
using MinikUsta.Core.Level;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;

namespace MinikUsta.Presentation.Tests
{
    [TestFixture]
    public class TraitsV3Tests
    {
        private const string PalletLevel = "{\"schemaVersion\":3,\"id\":3,\"chapter\":1,\"name\":{\"tr\":\"T\",\"en\":\"T\"},\"difficulty\":\"easy\",\"moves\":20,"
            + "\"goals\":[{\"type\":\"build\"}],\"gravity\":{\"build\":\"normal\",\"yard\":false},\"wall\":{\"height\":0,\"gaps\":[]},\"site\":{\"cols\":2,\"rows\":4},"
            + "\"build\":{\"mode\":\"segments\",\"segments\":[{\"name\":{\"tr\":\"a\",\"en\":\"a\"},\"rows\":[\"GG\",\"GG\",\"WW\",\"WW\"]}]},"
            + "\"yard\":{\"cols\":4,\"rows\":4,\"batches\":[{\"forSegment\":0,\"pieces\":["
            + "{\"shape\":\"D2_90\",\"color\":\"W\",\"x\":0,\"y\":0,\"stack\":{\"id\":0,\"z\":0}},"
            + "{\"shape\":\"D2_90\",\"color\":\"W\",\"x\":0,\"y\":0,\"stack\":{\"id\":0,\"z\":1}},"
            + "{\"shape\":\"D2_90\",\"color\":\"G\",\"x\":2,\"y\":0},{\"shape\":\"D2_90\",\"color\":\"G\",\"x\":2,\"y\":1}]}]},\"obstacles\":[]}";

        private static GameState StateOf(int level) => CreateInitialState(Play.LevelFile(level));

        private static int FindPiece(GameState s, System.Func<CompiledPiece, bool> pred)
        {
            for (int i = 0; i < s.Lvl.Pieces.Length; i++) if (pred(s.Lvl.Pieces[i])) return i;
            Assert.Fail("no such piece");
            return -1;
        }

        private static List<SpritePart> Parts(GameState s, int id, BlockLook look = BlockLook.Holdable) =>
            BlockSkin.Block(Shapes.ShapeByIndex(PieceShape(s, id)), PieceColor(s, id), 120, look, null, BlockTraits.Of(s, id));

        [Test(Description = "Y9 (K-57): a two-tone block draws every cell with its own colour tile, so the joined tiles read as one two-coloured block (colour and symbol per cell)")]
        public void Y9_two_tone_cells_draw_their_own_colour()
        {
            var s = StateOf(5);
            int id = FindPiece(s, p => p.CellColors != null);
            var cc = s.Lvl.Pieces[id].CellColors!;
            Assert.Greater(cc.Distinct().Count(), 1, "a two-tone block has at least two colours");
            var bodies = Parts(s, id).Where(p => p.Role == PartRole.Body).OrderBy(p => p.Cell).ToList();
            Assert.AreEqual(cc.Length, bodies.Count);
            for (int i = 0; i < cc.Length; i++) StringAssert.StartsWith("blk_" + ColorCodes.All[cc[i]].ToLowerInvariant() + "_", bodies[i].Frame, "cell " + i);
            var plain = Parts(s, FindPiece(s, p => p.CellColors == null && p.Cls == PieceClasses.Material)).Where(p => p.Role == PartRole.Body).ToList();
            Assert.IsTrue(plain.All(p => p.Frame == plain[0].Frame.Substring(0, 6) + p.Frame.Substring(6)), "a one-colour block is unchanged");
        }

        [Test(Description = "Y11 (K-59): a wrapped block gets the net over every cell and one knot badge on its top-left cell; the scissors block gets the scissors badge")]
        public void Y11_wrap_overlay_per_cell_knot_and_scissors_badges()
        {
            var s = StateOf(8);
            int wrapped = FindPiece(s, p => (p.Flags & FlagBit.Wrapped) != 0);
            int scissors = FindPiece(s, p => (p.Flags & FlagBit.Scissors) != 0);
            var wp = Parts(s, wrapped, BlockLook.Dim);
            int cells = Shapes.ShapeByIndex(PieceShape(s, wrapped)).Cells.Length;
            Assert.AreEqual(cells, wp.Count(p => p.Role == PartRole.Overlay && p.Frame == Frames.OvlWrap), "one net per cell");
            CollectionAssert.AreEqual(new[] { Frames.WrapKnot }, wp.Where(p => p.Role == PartRole.Badge).Select(p => p.Frame).ToArray());
            Assert.AreEqual(0, wp.Count(p => p.Role == PartRole.Gloss), "not holdable: no gloss (K-09)");
            CollectionAssert.AreEqual(new[] { Frames.IcoScissors }, Parts(s, scissors).Where(p => p.Role == PartRole.Badge).Select(p => p.Frame).ToArray());
            Assert.AreEqual(0, Parts(s, scissors).Count(p => p.Role == PartRole.Overlay), "the scissors block is not wrapped");
        }

        [Test(Description = "Y12 (K-60): the bonus block carries a star badge with its +n")]
        public void Y12_bonus_star_badge_shows_the_moves()
        {
            var s = StateOf(10);
            int id = FindPiece(s, p => (p.Flags & FlagBit.Bonus) != 0);
            Assert.AreEqual(2, s.Lvl.Pieces[id].BonusMoves);
            CollectionAssert.AreEqual(new[] { "ico_bonus_2" }, Parts(s, id).Where(p => p.Role == PartRole.Badge).Select(p => p.Frame).ToArray());
            Assert.AreEqual(Frames.IcoBonus(1), "ico_bonus_1");
            Assert.AreEqual(Frames.IcoBonus(9), "ico_bonus_3", "clamped to 1–3 (K-60)");
        }

        [Test(Description = "Y13 (K-61): a slippery block gets the frost sheen on every cell and the snowflake badge")]
        public void Y13_slippery_frost_and_snowflake()
        {
            var t = new BlockTraits(null, false, false, 0, true, 0, 0, false);
            var shape = Shapes.ShapeById("C3_0");
            var parts = BlockSkin.Block(shape, 1, 120, BlockLook.Holdable, null, t);
            Assert.AreEqual(3, parts.Count(p => p.Frame == Frames.OvlIce));
            CollectionAssert.AreEqual(new[] { Frames.IcoSlip }, parts.Where(p => p.Role == PartRole.Badge).Select(p => p.Frame).ToArray());
            int bc = BlockSkin.BadgeCell(shape);
            Assert.AreEqual(1, shape.Cells[bc].Y, "C3_0: the badge cell is on the top row");
        }

        [Test(Description = "E3 badges of one block go left to right along the top edge in a fixed order and stay on the top-left cell")]
        public void E3_badges_stack_in_a_fixed_order()
        {
            var t = new BlockTraits(null, true, true, 2, true, 3, 0, true);
            CollectionAssert.AreEqual(new[] { Frames.WrapKnot, Frames.IcoScissors, "ico_bonus_2", Frames.IcoSlip, "ico_layers_3" }, BlockSkin.BadgeFrames(t));
            var parts = BlockSkin.Block(Shapes.ShapeById("D2_0"), 0, 120, BlockLook.Holdable, null, t).Where(p => p.Role == PartRole.Badge).ToList();
            for (int i = 1; i < parts.Count; i++) Assert.Greater(parts[i].Rect.X, parts[i - 1].Rect.X, "slot " + i + " is right of slot " + (i - 1));
            Assert.IsTrue(parts.All(p => p.Cell == BlockSkin.BadgeCell(Shapes.ShapeById("D2_0"))));
            Assert.IsTrue(parts.All(p => p.Order > 3), "badges above the overlays");
        }

        [Test(Description = "E3 BlockTraits compare by value, read from the state, and plain blocks have none")]
        public void E3_block_traits_value_equality()
        {
            var s = StateOf(8);
            int wrapped = FindPiece(s, p => (p.Flags & FlagBit.Wrapped) != 0);
            Assert.AreEqual(BlockTraits.Of(s, wrapped), BlockTraits.Of(s, wrapped));
            Assert.IsTrue(BlockTraits.Of(s, wrapped).Wrapped);
            int plain = FindPiece(s, p => p.Flags == 0 && p.CellColors == null);
            Assert.AreSame(BlockTraits.None, BlockTraits.Of(s, plain));
            Assert.AreNotEqual(BlockTraits.None, BlockTraits.Of(s, wrapped));
            Assert.AreEqual(new BlockTraits(new[] { 1, 2 }, false, false, 0, false, 0, 0, false), new BlockTraits(new[] { 1, 2 }, false, false, 0, false, 0, 0, false));
            Assert.AreNotEqual(new BlockTraits(new[] { 1, 2 }, false, false, 0, false, 0, 0, false), new BlockTraits(new[] { 2, 1 }, false, false, 0, false, 0, 0, false));
        }

        [Test(Description = "E3 every frame a trait draws exists in the atlas (the 13 extension frames, page 3) and fits the stored contract")]
        public void E3_trait_frames_exist_in_the_atlas()
        {
            var a = ArtContractTests.Atlas;
            var names = new List<string> { Frames.OvlWrap, Frames.WrapKnot, Frames.IcoScissors, Frames.OvlIce, Frames.IcoSlip, Frames.PalletCell, Frames.PalletSlab };
            for (int n = 1; n <= 3; n++) names.Add(Frames.IcoBonus(n));
            for (int n = 2; n <= 4; n++) names.Add(Frames.IcoLayers(n));
            foreach (string name in names)
            {
                var f = a[name];
                Assert.IsNotNull(f, name);
                Assert.GreaterOrEqual(f.Page, 3, name + " lives in an extension page");
            }
            Assert.AreEqual(13, names.Distinct().Count());
        }

        [Test(Description = "Y10 (K-58, D-104, P4): a pallet draws the layer count on the top layer, one board per covered layer under the top layer's bottom edge, and planks under the last board; every layer keeps the state pose")]
        public void Y10_pallet_boards_planks_and_layer_count()
        {
            var rig = new Rig();
            rig.Player.Start(Play.FromJson(PalletLevel), 3);
            rig.Settle();
            var live = rig.View.Live;
            var top = live[1];
            var low = live[0];
            Assert.AreEqual(2, top.Traits.PalletLayers, "the top layer shows the layer count");
            Assert.AreEqual(0, top.Traits.PalletDepth);
            Assert.AreEqual(1, low.Traits.PalletDepth, "layer 0 is covered by one layer");
            Assert.IsTrue(low.Traits.PalletBase, "the lowest layer carries the planks");
            Assert.IsFalse(top.Traits.PalletBase);
            Assert.AreEqual(top.Position.X, low.Position.X, 0.01);
            Assert.AreEqual(top.Position.Y, low.Position.Y, 0.01, "P4: a covered layer lies in the same pose, so no layer moves when the one above leaves");
            var shape = Shapes.ShapeById("D2_90");
            var covered = BlockSkin.Block(shape, 0, 120, BlockLook.Dim, null, low.Traits);
            Assert.IsTrue(covered.Any(p => p.Role == PartRole.Body), "a covered layer keeps its body, so lifting the top layer shows it in place");
            Assert.IsFalse(covered.Any(p => p.Role == PartRole.Gloss), "dim, not holdable: no gloss (K-09, K-58 item 3)");
            var boards = covered.Where(p => p.Frame == Frames.PalletSlab).ToList();
            Assert.AreEqual(2, boards.Count, "one board per bottom cell");
            var planks = covered.Where(p => p.Frame == Frames.PalletCell).ToList();
            Assert.AreEqual(2, planks.Count, "one plank frame per cell");
            Assert.IsTrue(planks.All(p => p.Order < boards[0].Order && p.Order < 0), "planks under the boards and under every body");
            Assert.IsTrue(planks.All(p => p.Rect.W > 120 && p.Rect.H > 120), "the planks reach past the cell on both sides and below");
            var cell = BlockSkin.CellRect(shape, shape.Cells[0], 120);
            Assert.AreEqual(cell.Bottom, boards.Min(p => p.Rect.Y), 1e-9, "the first board sits right under the layer above");
            // three layers: the layer under two others shows its board one step lower and the planks two steps lower
            var deep = new BlockTraits(null, false, false, 0, false, 0, 2, true);
            var deepParts = BlockSkin.Block(shape, 0, 120, BlockLook.Dim, null, deep);
            Assert.AreEqual(cell.Bottom + BlockSkin.PalletSlabStep, deepParts.First(p => p.Frame == Frames.PalletSlab).Rect.Y, 1e-9);
            Assert.AreEqual(cell.Y + 2 * BlockSkin.PalletSlabStep, deepParts.First(p => p.Frame == Frames.PalletCell).Rect.Y, 1e-9);
            // the top layer: its body, the "×n" badge, no board of its own
            var topParts = BlockSkin.Block(shape, 0, 120, BlockLook.Holdable, null, top.Traits);
            Assert.IsTrue(topParts.Any(p => p.Role == PartRole.Body));
            CollectionAssert.AreEqual(new[] { "ico_layers_2" }, topParts.Where(p => p.Role == PartRole.Badge).Select(p => p.Frame).ToArray());
            Assert.IsFalse(topParts.Any(p => p.Frame == Frames.PalletSlab));
        }

        [Test(Description = "Y11 (K-59, E3): the scissors placement plays the unwrap step — the wrapped blocks' net and knot are released and the sparks / confetti fire")]
        public void Y11_scissors_placement_plays_the_unwrap_release_on_the_wrapped_blocks()
        {
            var rig = new Rig();
            rig.Load(8);
            rig.Settle();
            var s = rig.Session.State;
            int wrapped = FindPiece(s, p => (p.Flags & FlagBit.Wrapped) != 0);
            int before = rig.View.Live.Count;
            Assert.IsTrue(rig.View.Live[wrapped].Traits.Wrapped);
            foreach (var m in Play.SolverGolden(8).Drags)
            {
                rig.Settle();
                Assert.AreEqual(MoveStatuses.Applied, rig.Player.Commit(m).Status);
            }
            rig.Settle();
            var all = rig.View.Live.Values.Concat(rig.View.Pool).ToList();
            Assert.IsTrue(all.Any(p => p.MaxTraitFx >= 1f), "a block's wrap was released (TraitFx 0 → 1)");
            Assert.IsTrue(rig.Events.Count >= 0 && before > 0);
            Assert.AreEqual(BoosterOutcomes.Won, rig.Session.Outcome);
        }

        [Test(Description = "Y12 (K-60, E3): the bonus placement plays the BonusMoves step — the star leaves the block and the moves counter rolls up")]
        public void Y12_bonus_placement_plays_the_star_and_rolls_the_counter()
        {
            var rig = new Rig();
            rig.Load(10);
            rig.Settle();
            var seen = new List<int>();
            foreach (var m in Play.SolverGolden(10).Drags)
            {
                rig.Settle();
                int left = rig.Session.MovesLeft;
                var r = rig.Player.Commit(m);
                Assert.AreEqual(MoveStatuses.Applied, r.Status);
                rig.Settle();
                if (r.Events.Any(e => e is MovesChangedEvent mc && mc.Reason == MovesChangedReasons.Bonus))
                {
                    seen.Add(rig.Session.MovesLeft - left);
                    Assert.AreEqual(rig.Session.MovesLeft, rig.View.Moves, "the HUD counter shows the bonus");
                }
            }
            CollectionAssert.AreEqual(new[] { 1 }, seen, "cost −1 then bonus +2: the move nets +1");
            var all = rig.View.Live.Values.Concat(rig.View.Pool).ToList();
            Assert.IsTrue(all.Any(p => p.MaxTraitFx >= 0.8f), "the star badge flew off the block");
        }

        [Test(Description = "E3 the dev showcase level 99 (Resources/levels_dev) loads with the runtime checks and its board shows every v3 trait")]
        public void E3_showcase_level_99_shows_every_trait()
        {
            var rig = new Rig();
            Assert.IsTrue(rig.Player.Load(LevelCatalog.Showcase), rig.Player.LoadError);
            rig.Settle();
            var traits = rig.View.Live.Values.Select(p => p.Traits).ToList();
            Assert.IsTrue(traits.Any(t => t.CellColors != null), "Y9 two-tone");
            Assert.IsTrue(traits.Any(t => t.PalletLayers == 3), "Y10 pallet, three layers on top");
            Assert.AreEqual(2, traits.Count(t => t.PalletDepth > 0), "Y10 two covered layers");
            Assert.IsTrue(traits.Any(t => t.Wrapped) && traits.Any(t => t.Scissors), "Y11 wrap + scissors");
            Assert.IsTrue(traits.Any(t => t.Bonus == 3), "Y12 bonus +3");
            Assert.AreEqual(2, traits.Count(t => t.Slippery), "Y13 slippery");
            StringAssert.StartsWith("levels_dev/", LevelCatalog.ResourceName(LevelCatalog.Showcase));
            StringAssert.StartsWith("levels/", LevelCatalog.ResourceName(10));
        }
    }
}
