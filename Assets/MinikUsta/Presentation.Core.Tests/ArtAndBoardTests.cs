#nullable enable
// Placeholder art (atlas, palette), the static board scene, the K-18 shadow look and the i18n strings (Faz 3 Q1).
using System;
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core;
using MinikUsta.Core.Json;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Layout;
using MinikUsta.Presentation.Tests.Fixtures;
using MinikUsta.Presentation.Text;
using NUnit.Framework;

namespace MinikUsta.Presentation.Tests
{
    public sealed class AtlasTests
    {
        private static Atlas? atlas;
        private static Atlas A => atlas ??= Atlas.Build();

        [Test]
        public void Atlas_is_built_once_deterministically_and_every_tile_has_its_own_square()
        {
            var b = Atlas.Build();
            Assert.AreEqual(Atlas.Size * Atlas.Size * 4, b.Pixels.Length);
            CollectionAssert.AreEqual(A.Pixels, b.Pixels, "the same bytes every build");
            var seen = new HashSet<(int, int)>();
            foreach (var t in Atlas.All)
            {
                var r = Atlas.RectOf(t);
                Assert.IsTrue(seen.Add((r.X, r.Y)), t + " overlaps another tile");
                Assert.GreaterOrEqual(r.X - Atlas.Pad, 0);
                Assert.LessOrEqual(r.Y + r.H + Atlas.Pad, Atlas.Size);
            }
            Assert.LessOrEqual(Atlas.All.Count(), Atlas.Columns * Atlas.Columns);
        }

        [Test]
        public void TECH106_the_atlas_builds_in_well_under_a_second_on_dotnet()
        {
            Atlas.Build(); // JIT warm-up
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Atlas.Build();
            sw.Stop();
            TestContext.Out.WriteLine("Atlas.Build: " + sw.ElapsedMilliseconds + " ms (" + Atlas.Size + "² px, " + Atlas.All.Count() + " tiles)");
            // a budget, not a benchmark: WebGL / IL2CPP is several times slower than .NET 8 (see docs/PHASE3 notes)
            Assert.Less(sw.ElapsedMilliseconds, 1500);
        }

        [Test]
        public void Block_cells_are_rounded_on_open_sides_and_flush_where_the_piece_continues()
        {
            var lone = Atlas.Body(0);
            Assert.AreEqual(0, A.At(lone, 0, 0).A, "an open corner is transparent (rounded)");
            Assert.AreEqual(255, A.At(lone, 60, 60).A, "the face is opaque");
            Assert.Less(A.At(lone, 60, 1).A, 255, "the inset edge");
            // right neighbour: the right edge runs to the tile edge, opaque; corners on that side are square
            var right = Atlas.Body(2);
            Assert.AreEqual(255, A.At(right, Atlas.Content - 1, 60).A);
            Assert.AreEqual(0, A.At(right, 0, 0).A, "the left corners stay rounded");
            var all = Atlas.Body(15);
            Assert.AreEqual(255, A.At(all, 0, 0).A, "a cell surrounded on four sides fills the tile");
            // tokens blockV2 outline 0,42 × base: the open outline is darker than the face
            Assert.Less(A.At(lone, 60, 6).R, A.At(lone, 60, 60).R);
            // the extruded padding repeats the edge (no seam between connected cells)
            var r = Atlas.RectOf(right);
            Assert.AreEqual(A.At(r.X + r.W - 1, r.Y + 60), A.At(r.X + r.W + Atlas.Pad - 1, r.Y + 60));
        }

        [Test]
        public void R05_every_colour_has_a_distinct_symbol_mask()
        {
            var masks = new List<byte[]>();
            for (int c = 0; c < 8; c++)
            {
                var t = Atlas.Symbol(c);
                var m = new byte[Atlas.Content * Atlas.Content];
                int ink = 0;
                for (int y = 0; y < Atlas.Content; y++)
                    for (int x = 0; x < Atlas.Content; x++)
                    {
                        var px = A.At(t, x, y);
                        m[y * Atlas.Content + x] = px.A;
                        if (px.A > 128) ink++;
                        Assert.AreEqual(255, px.R, "symbols are white masks (tinted with the ink colour)");
                    }
                Assert.Greater(ink, 600, ColorCodes.All[c] + " symbol has ink");
                Assert.Less(ink, Atlas.Content * Atlas.Content * 6 / 10, ColorCodes.All[c] + " symbol is not a filled square");
                foreach (var o in masks) CollectionAssert.AreNotEqual(o, m, ColorCodes.All[c] + " symbol differs from the others");
                masks.Add(m);
            }
        }

        [Test]
        public void Gradient_tile_runs_from_game_bottom_to_game_top()
        {
            var bottom = A.At(Tile.Gradient, 60, 0);
            var top = A.At(Tile.Gradient, 60, Atlas.Content - 1);
            var b = Rgba.Hex(Palette.GameBottom);
            var t = Rgba.Hex(Palette.GameTop);
            Assert.AreEqual(b.R, bottom.R, 2);
            Assert.AreEqual(t.B, top.B, 2);
        }
    }

    public sealed class PaletteTests
    {
        private static string Tok(string path) => PresentationPaths.Token(path).AsString;

        [Test]
        public void Palette_colours_are_the_tokens_json_values()
        {
            for (int i = 0; i < 8; i++)
                Assert.AreEqual(Tok("color.block." + ColorCodes.All[i]).ToUpperInvariant(), Palette.BlockHex[i], "color.block." + ColorCodes.All[i]);
            var pairs = new (string, string)[]
            {
                ("color.ghost.valid", Palette.GhostValid), ("color.ghost.invalid", Palette.GhostInvalid), ("color.ghost.neutral", Palette.GhostNeutral),
                ("color.ghost.support", Palette.GhostSupport), ("color.board.yardFloor", Palette.YardFloor), ("color.board.yardFloorAlt", Palette.YardFloorAlt),
                ("color.board.yardFrame", Palette.YardFrame), ("color.board.blueprint", Palette.Blueprint), ("color.board.blueprintDeep", Palette.BlueprintDeep),
                ("color.board.planUnderlay", Palette.PlanUnderlay), ("color.board.wall", Palette.Wall), ("color.board.wallDark", Palette.WallDark),
                ("color.board.rail", Palette.Rail), ("color.board.shutterSlat", Palette.ShutterSlat), ("color.board.craneSky", Palette.CraneSky),
                ("color.ui.panel", Palette.Panel), ("color.ui.panelEdge", Palette.PanelEdge), ("color.ui.ink", Palette.Ink), ("color.ui.primary", Palette.Primary),
                ("color.ui.secondary", Palette.Secondary), ("color.ui.neutral", Palette.Neutral), ("color.ui.danger", Palette.Danger), ("color.ui.gold", Palette.Gold),
                ("color.ui.overlay", Palette.Overlay), ("color.scene.gameTop", Palette.GameTop), ("color.scene.gameBottom", Palette.GameBottom),
            };
            foreach (var (path, value) in pairs) Assert.AreEqual(Tok(path).ToUpperInvariant(), value, path);
            Assert.AreEqual(PresentationPaths.Token("alpha.planFill").AsDouble, Palette.PlanFill, 1e-6);
            Assert.AreEqual(PresentationPaths.Token("alpha.planEmptyHatch").AsDouble, Palette.PlanEmptyHatch, 1e-6);
            Assert.AreEqual(PresentationPaths.Token("alpha.craneSky").AsDouble, Palette.CraneSkyAlpha, 1e-6);
            Assert.AreEqual(PresentationPaths.Token("alpha.ghostFill").AsDouble, Palette.GhostFill, 1e-6);
            Assert.AreEqual(PresentationPaths.Token("alpha.overlay").AsDouble, Palette.OverlayAlpha, 1e-6);
            Assert.AreEqual(PresentationPaths.Token("alpha.symbolWhite").AsDouble, Palette.SymbolWhiteAlpha, 1e-6);
            Assert.AreEqual(PresentationPaths.Token("block.symbolDarkFactor").AsDouble, Palette.SymbolDarkFactor, 1e-6);
            Assert.AreEqual(PresentationPaths.Token("shadow.lifted.y").AsDouble, Palette.LiftedShadowY, 1e-6);
            Assert.AreEqual(PresentationPaths.Token("shadow.lifted.alpha").AsDouble, Palette.LiftedShadowAlpha, 1e-6);
        }

        [Test]
        public void UX02_window_and_moves_plate_numbers_are_the_tokens_and_options_sit_in_the_thumb_zone()
        {
            var s = new LayoutSettings();
            Assert.AreEqual(PresentationPaths.Token("layout.popup.panelBottomPx").AsDouble, s.PopupPanelBottomPx);
            Assert.AreEqual(PresentationPaths.Token("layout.popup.optionW").AsDouble, s.PopupOptionW);
            Assert.AreEqual(PresentationPaths.Token("layout.popup.optionH").AsDouble, s.PopupOptionH);
            Assert.AreEqual(PresentationPaths.Token("layout.popup.optionGap").AsDouble, s.PopupOptionGap);
            Assert.AreEqual(PresentationPaths.Token("layout.popup.optionsBottomInsetPx").AsDouble, s.PopupOptionsBottomInsetPx);
            Assert.AreEqual(PresentationPaths.Token("kit.v3.screen.hud.movesBandH").AsDouble, s.MovesBandH);
            Assert.AreEqual(PresentationPaths.Token("kit.v3.screen.hud.movesFontPx").AsDouble, s.MovesFontPx);
            Assert.AreEqual(PresentationPaths.Token("kit.v3.screen.hud.movesBand").AsString.ToUpperInvariant(), Palette.MovesBand);
            Assert.AreEqual(PresentationPaths.Token("kit.v3.screen.hud.movesCream").AsString.ToUpperInvariant(), Palette.MovesCream);
            var L = ScreenLayout.Create(s, 1920);
            for (int n = 1; n <= 3; n++)
            {
                var w = HudLayout.Window(L, s, n);
                Assert.AreEqual(1920 - s.PopupPanelBottomPx, w.Panel.Bottom, 1e-9, "the panel bottom edge is anchored (layout.popup)");
                Assert.GreaterOrEqual(w.Options[n - 1].Y, 1056, "UX §0.1: every option in the comfortable zone (y ≥ 1056)");
                Assert.GreaterOrEqual(w.Options[0].Y, 1400, "the primary (lowest) option y ≥ 1400");
                Assert.Less(w.Title.Bottom, w.Options[n - 1].Y + 1e-9, "title above the options");
                foreach (var o in w.Options)
                {
                    Assert.GreaterOrEqual(o.H, L.MinTargetPx, "UX §0.3: ≥ 128 px targets");
                    Assert.GreaterOrEqual(o.X, w.Panel.X);
                    Assert.LessOrEqual(o.Right, w.Panel.Right);
                }
            }
        }

        [Test]
        public void R05_symbol_ink_is_dark_on_light_bases_and_white_on_dark_ones()
        {
            // ART §2.2: L* ≥ 60 (Y G O C) → base × 0,40; W R B P → white 85 %
            Assert.AreEqual(new[] { false, true, true, false, true, true, false, false }, Palette.DarkInk);
            Assert.AreEqual(Rgba.Hex("#FFE23E").Scale(0.4f), Palette.SymbolInk(1));
            Assert.AreEqual(Rgba.White.WithAlpha(0.85f), Palette.SymbolInk(0));
        }

        [Test]
        public void Rgba_hex_round_trip_and_lerp()
        {
            Assert.AreEqual("#2984DE", Rgba.Hex("#2984de").ToHex());
            Assert.AreEqual(Rgba.White, Rgba.Lerp(Rgba.Black, Rgba.White, 2));
            Assert.AreEqual(new Rgba(128, 128, 128), Rgba.Lerp(Rgba.Black, Rgba.White, 0.5f));
            Assert.Throws<FormatException>(() => Rgba.Hex("2984DE"));
        }
    }

    public sealed class BoardSceneTests
    {
        [Test]
        public void K49_UX58_the_static_board_of_every_level_has_its_yard_wall_and_plan([ValueSource(typeof(Play), nameof(Play.Levels))] int level)
        {
            var lvl = Play.LevelFile(level);
            var session = GameSession.Start(lvl);
            var L = ScreenLayout.Create(new LayoutSettings(), 1920, lvl.Geo);
            var board = BoardScene.Build(session.State, L);
            var geo = lvl.Geo;
            Assert.AreEqual(geo.Wy * geo.Hy, board.Count(BoardItemKind.YardCell), "one floor cell per yard cell");
            Assert.AreEqual(lvl.WallHeight, board.Count(BoardItemKind.Wall) + board.Count(BoardItemKind.Gap) + board.Count(BoardItemKind.ClosedGap), "one wall tile per wall row");
            int planCells = 0, dots = 0;
            for (int sy = 0; sy < geo.Hs; sy++)
                for (int sx = 0; sx < geo.Ws; sx++)
                {
                    int pc = Grid.PlanColorAt(session.State, 0, sx, sy);
                    if (pc >= 0) planCells++;
                    else if (pc == MinikUsta.Core.Level.Compiler.PlanDot) dots++;
                }
            Assert.AreEqual(planCells, board.Count(BoardItemKind.PlanCell) + board.Count(BoardItemKind.HiddenCell), "K-47 plan cells of segment 1");
            Assert.AreEqual(dots, board.Count(BoardItemKind.HatchCell));
            foreach (var gap in lvl.Gaps) Assert.Greater(board.Count(BoardItemKind.Gap) + board.Count(BoardItemKind.ClosedGap), 0, "W1 gap drawn");
            // every plan cell carries its plan colour (R-05: the block on it must match)
            foreach (var item in board.Items.Where(i => i.Kind == BoardItemKind.PlanCell))
            {
                int pc = Grid.PlanColorAt(session.State, 0, item.X - geo.SiteX, item.Y);
                Assert.AreEqual(Palette.Block(pc).WithAlpha(Palette.PlanFill), item.Color);
                Assert.AreEqual(L.Grid.CellRect(item.X, item.Y), item.Rect);
            }
            Assert.IsTrue(board.Items.All(i => i.Order >= Game.Order.Background && i.Order < Game.Order.Pieces), "the static board is under the blocks");
        }

        [Test]
        public void Board_pose_neighbour_masks_and_cell_offsets()
        {
            var vertical = new[] { "D2_0", "D2_90" }.Select(Shapes.ShapeById).First(sh => sh.H == 2);
            var horizontal = new[] { "D2_0", "D2_90" }.Select(Shapes.ShapeById).First(sh => sh.W == 2);
            Assert.AreEqual(2, BoardPose.NeighbourMask(horizontal, horizontal.Cells.First(c => c.X == 0)), "left cell of a horizontal domino: right neighbour");
            Assert.AreEqual(1, BoardPose.NeighbourMask(horizontal, horizontal.Cells.First(c => c.X == 1)));
            Assert.AreEqual(8, BoardPose.NeighbourMask(vertical, vertical.Cells.First(c => c.Y == 0)), "bottom cell of a vertical domino: up neighbour");
            Assert.AreEqual(4, BoardPose.NeighbourMask(vertical, vertical.Cells.First(c => c.Y == 1)));
            var o = BoardPose.CellOffset(horizontal, horizontal.Cells.First(c => c.X == 0), 120);
            Assert.AreEqual(-60f, o.X, 1e-5);
            Assert.AreEqual(0f, o.Y, 1e-5);
            var top = BoardPose.CellOffset(vertical, vertical.Cells.First(c => c.Y == 1), 120);
            Assert.AreEqual(-60f, top.Y, 1e-5, "y down: the upper cell is above the centre");
        }
    }

    public sealed class ShadowLookTests
    {
        /// <summary>Level 1: piece 0 = D2_0 Y, piece 1 = D2_0 W; released over column 4 in the crane area (web shadowLook.test.ts).</summary>
        private static (FallResult ok, FallResult color, GameState s) Level1Falls()
        {
            var session = GameSession.Start(Play.LevelFile(1));
            var s = session.State;
            var node = new DragNode(4, 5, Movement.Free);
            return (Gravity.ComputeFall(s, 0, node), Gravity.ComputeFall(s, 1, node), s);
        }

        private static FallResult With(FallResult f, bool? touchesHidden = null, LandingEffect? effect = null) =>
            new FallResult(f.Mode, f.Landing, f.Dir, f.Distance, f.Path, f.Drift, f.Steered, f.Cells, f.Verdict, touchesHidden ?? f.TouchesHidden, effect ?? f.Effect);

        [Test]
        public void K18_easy_correct_landing_valid_outline_ok_badge_ghost_body()
        {
            var look = ShadowLook.Of(Level1Falls().ok, "easy");
            Assert.AreEqual(ShadowOutline.Valid, look.Outline);
            Assert.AreEqual(ShadowBadge.Ok, look.Badge);
            Assert.IsFalse(look.Pulse);
            Assert.IsTrue(look.Body);
            Assert.AreEqual(0, look.SupportCells.Length);
        }

        [Test]
        public void K18_easy_wrong_colour_invalid_outline_warn_badge_pulse_and_the_mismatch_cells()
        {
            var f = Level1Falls();
            Assert.AreEqual(VerdictReasons.Color, f.color.Verdict.Reasons[0]);
            var look = ShadowLook.Of(f.color, "normal", f.s, 1);
            Assert.AreEqual(ShadowOutline.Invalid, look.Outline);
            Assert.AreEqual(ShadowBadge.Warn, look.Badge);
            Assert.IsTrue(look.Pulse);
            Assert.Greater(look.MismatchCells.Length, 0, "UX §5.4: the 45° hatch cells of the primary reason");
        }

        [Test]
        public void K18_hard_and_superhard_neutral_outline_only()
        {
            foreach (var d in new[] { "hard", "superhard" })
            {
                var look = ShadowLook.Of(Level1Falls().color, d);
                Assert.AreEqual(ShadowOutline.Neutral, look.Outline, d);
                Assert.AreEqual(ShadowBadge.None, look.Badge, d);
                Assert.IsFalse(look.Pulse, d);
            }
        }

        [Test]
        public void E20_K18_an_unrevealed_cell_is_neutral_and_never_leaks_the_colour()
        {
            var f = Level1Falls();
            var hidden = ShadowLook.Of(With(f.color, touchesHidden: true), "easy");
            var hiddenOk = ShadowLook.Of(With(f.ok, touchesHidden: true), "easy");
            Assert.AreEqual(ShadowOutline.Neutral, hidden.Outline);
            Assert.AreEqual(ShadowBadge.None, hidden.Badge);
            Assert.AreEqual(hidden.Key, hiddenOk.Key, "right and wrong look the same over a ? cell");
        }

        [Test]
        public void K18_glass_crack_shows_at_every_difficulty()
        {
            var breaking = With(Level1Falls().ok, effect: LandingEffect.Break(1));
            Assert.AreEqual(ShadowBadge.Glass, ShadowLook.Of(breaking, "superhard").Badge);
            Assert.AreEqual(ShadowBadge.Glass, ShadowLook.Of(breaking, "easy").Badge);
        }

        [Test]
        public void JUICE7_the_key_changes_exactly_when_the_look_changes()
        {
            var f = Level1Falls();
            Assert.AreEqual(ShadowLook.Of(f.ok, "easy").Key, ShadowLook.Of(f.ok, "easy").Key);
            Assert.AreNotEqual(ShadowLook.Of(f.ok, "easy").Key, ShadowLook.Of(f.color, "easy").Key);
        }

        [Test]
        public void K07_cancel_preview_rows_3_4_5_and_shadow_rows_6_7()
        {
            Assert.IsTrue(ShadowLook.CancelPreview(DropClass.CraneOverYard));
            Assert.IsTrue(ShadowLook.CancelPreview(DropClass.Straddle));
            Assert.IsTrue(ShadowLook.CancelPreview(DropClass.SiteClosed));
            Assert.IsFalse(ShadowLook.CancelPreview(DropClass.SameSpot));
            Assert.IsFalse(ShadowLook.CancelPreview(DropClass.SiteFree));
            Assert.IsTrue(ShadowLook.ShowsShadow(DropClass.SiteFree));
            Assert.IsTrue(ShadowLook.ShowsShadow(DropClass.SiteRail(0)));
            Assert.IsFalse(ShadowLook.ShowsShadow(DropClass.Yard));
            Assert.IsFalse(ShadowLook.ShowsShadow(DropClass.SiteClosed));
        }

        [Test]
        public void UX54_the_badge_sits_inside_the_top_right_cell()
        {
            var shape = Shapes.ShapeById("L4_0");
            var cell = ShadowLook.BadgeCell(shape);
            Assert.IsTrue(shape.Cells.Contains(cell));
            Assert.AreEqual(shape.H - 1, cell.Y);
            var g = ScreenLayout.Create(new LayoutSettings(), 1920).Grid;
            var model = new ShadowModel(0, shape, 0, 6, 0, ShadowLook.Of(Level1Falls().ok, "easy"), BoardPose.Center(g, 6, 0, shape), g.CellPx);
            double d = 64;
            var p = model.BadgeCenter(d);
            var r = g.CellRect(6 + cell.X, cell.Y);
            Assert.AreEqual(r.X + r.W, p.X + d / 2, 1e-3);
            Assert.AreEqual(r.Y, p.Y - d / 2, 1e-3);
        }
    }

    public sealed class StringsTests
    {
        [Test]
        public void I18n_every_key_the_unity_hud_uses_exists_in_tr_and_en()
        {
            var tr = Rig.Tr();
            var en = Rig.En();
            Assert.Greater(tr.Count, 100, "tr.json was read from Resources/i18n");
            foreach (var k in Strings.UsedKeys)
            {
                Assert.IsTrue(tr.Has(k), "tr.json has " + k);
                Assert.IsTrue(en.Has(k), "en.json has " + k);
            }
            Assert.AreEqual("Bölüm 7", tr.Format(Strings.Keys.LevelLabel, 7));
            Assert.AreEqual("1/3", tr.Format(Strings.Keys.Count, ("n", 1), ("max", 3)));
            Assert.AreEqual("missing.key", tr.Get("missing.key"));
        }

        [Test]
        public void I18n_turkish_upper_case_without_a_culture()
        {
            Assert.AreEqual("KAZANDIN!", TrText.Upper("Kazandın!"));
            Assert.AreEqual("İNŞAAT", TrText.Upper("inşaat"));
            Assert.AreEqual("BÖLÜM İKİ", TrText.Upper("Bölüm iki"));
        }

        [Test]
        public void Resources_hold_a_byte_identical_copy_of_every_level_file()
        {
            for (int n = 1; n <= 10; n++)
            {
                string res = PresentationPaths.ResourceText(LevelCatalog.ResourceName(n))!;
                string stream = System.IO.File.ReadAllText(System.IO.Path.Combine(PresentationPaths.RepoRoot, "Assets", "StreamingAssets", "MinikUsta", "levels", "level_" + n.ToString("000") + ".json"));
                Assert.AreEqual(stream, res, "level " + n);
            }
        }
    }
}
