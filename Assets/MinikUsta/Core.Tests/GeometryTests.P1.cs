#nullable enable
// Port of tests/core/geometry.test.ts — the blocks that need the P1 modules (movement, gravity, placement, delivery):
// edge model ≡ cell model in every geometry of G, E-56 yard air (classify + `applyMove`), K-05 on a short board, E-57 truck
// drops over yard air, K-17 step 2 / step 3 on a short board, the K-44 Ağır Yük drag rule with its `blockedCargo` signal
// and its pipeline test, and (P3) the random-play invariants + replay through `GameSession`. Same class as GeometryTests.cs
// (partial): the geometry set G and the level helpers are shared.
using System;
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core.Json;
using MinikUsta.Core.Level;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;

namespace MinikUsta.Core.Tests
{
    public partial class GeometryTests
    {
        private static DragNode F(int ix, int iy) => new DragNode(ix, iy, Movement.Free);
        private static DragNode R(int gap, int ix, int iy) => new DragNode(ix, iy, Movement.RailMode(gap));
        private static string Key(DragNode n) => CellModel.Key(n);

        // --- edge model ≡ cell-by-cell model in every geometry of G -------------------------------------------------------

        [Test(Description = "K-04 K-05 K-07 K-08 K-11 K-12 K-13 K-44 K-49 R, BFS distances, edges and release classes equal the cell model (10 geometries × 90 boards)")]
        public void K04_K05_K07_K08_K11_K12_K13_K44_K49_R_BFS_distances_edges_and_release_classes_equal_the_cell_model_9_geometries_90_boards()
        {
            var rng = new Mulberry32(49_49);
            int compared = 0;
            foreach (var c in G)
            {
                for (int trial = 0; trial < 90; trial++)
                {
                    int h = Math.Max(c.Hy, c.Hs);
                    int height = rng.NextInt(h + 1);
                    var gaps = new List<JsonValue>();
                    if (height >= 2)
                    {
                        int y = rng.NextInt(height - 1);
                        int size = 1 + rng.NextInt(Math.Min(2, height - 1 - y));
                        gaps.Add(rng.Next() < 0.75 ? J.O("type", "static", "y", y, "size", size) : J.O("type", "locked", "y", y, "size", size, "keyId", "k"));
                    }
                    var lvl = Builders.Compiled(Sized(c, new LevelSpec
                    {
                        Id = 9,
                        Plan = PlanRows(c, rng),
                        Wall = new WallSpec { Height = height, Gaps = gaps.ToArray() },
                        Pieces = new[] { new P("B1_0", "W", 0, 0) },
                        Debris = new[] { new D("B1_0", "W", c.Wy, 0) },
                    }));
                    var g = lvl.Geo;
                    var s = CreateInitialState(lvl);
                    Array.Clear(s.Buf, lvl.Layout.YardOcc, lvl.Layout.Pieces - lvl.Layout.YardOcc);
                    int elev = g.H > c.Hs && rng.Next() < 0.4 ? 1 + rng.NextInt(g.H - c.Hs) : 0;
                    SetHdr(s, H.Elev, elev);
                    bool onSite = rng.Next() < 0.3;
                    int id = onSite ? 1 : 0;
                    SetPieceField(s, 1 - id, PF.Zone, Zone.Gone);
                    var pool = onSite
                        ? Boards.Canonical.Where(sh => sh.W <= c.Ws && !Movement.IsCargoShape(sh) && sh.H <= c.Hs).ToArray()
                        : Boards.Canonical.Where(sh => sh.W <= c.Wy && sh.H <= c.Hy).ToArray();
                    var shape = pool[rng.NextInt(pool.Length)];
                    SetPieceField(s, id, PF.Shape, shape.Index);
                    if (onSite)
                    {
                        SetPieceField(s, id, PF.Zone, Zone.Site);
                        SetPieceField(s, id, PF.X, g.SiteX + rng.NextInt(c.Ws - shape.W + 1));
                        SetPieceField(s, id, PF.Y, rng.NextInt(c.Hs - shape.H + 1));
                        SetPieceField(s, id, PF.Seg, 0);
                        if (rng.Next() < 0.3) SetFlag(s, id, FlagBit.Debris, false);
                    }
                    else
                    {
                        SetPieceField(s, id, PF.Zone, Zone.Yard);
                        SetPieceField(s, id, PF.X, rng.NextInt(c.Wy - shape.W + 1));
                        SetPieceField(s, id, PF.Y, rng.NextInt(c.Hy - shape.H + 1));
                    }
                    Grid.OccupyPiece(s, id);
                    double yardDensity = rng.Next() * 0.8;
                    double siteDensity = rng.Next() * 0.5;
                    for (int y = 0; y < c.Hy; y++)
                        for (int x = 0; x < c.Wy; x++)
                            if (YardOcc(s, x, y) == 0 && rng.Next() < yardDensity) SetYardOcc(s, x, y, -1);
                    for (int sy = 0; sy < c.Hs; sy++)
                        for (int sx = 0; sx < c.Ws; sx++)
                            if (SiteOcc(s, 0, sx, sy) == 0 && rng.Next() < siteDensity) SetSiteOcc(s, 0, sx, sy, SiteTrowel);

                    var m = new CellModel(g, shape, height,
                        lvl.Gaps.Select(gp => new ModelGap(gp.Y, gp.Y + gp.Size, gp.Type != "locked")).ToArray(),
                        (x, y) =>
                        {
                            if (x <= g.Wy - 1)
                            {
                                int v = YardOcc(s, x, y);
                                return v != 0 && v != id + 1;
                            }
                            if (y < elev) return true;
                            if (y >= g.H || y - elev >= c.Hs) return false; // crane area, site air
                            int sv = SiteOcc(s, 0, x - g.SiteX, y - elev);
                            return sv != 0 && sv != id + 1;
                        });
                    var anchor = F(PieceX(s, id), PieceY(s, id) + (onSite ? elev : 0));
                    var starts = new List<DragNode> { anchor };
                    if (onSite && HasFlag(s, id, FlagBit.Debris))
                        for (int gi = 0; gi < m.Gaps.Length; gi++)
                            if (m.Valid(R(gi, anchor.Ix, anchor.Iy))) starts.Add(R(gi, anchor.Ix, anchor.Iy));
                    foreach (var st in starts) m.Starts.Add(Key(st));
                    var want = m.Bfs(starts);
                    bool moves = want.Keys.Any(k => !k.EndsWith(":" + anchor.Ix + "," + anchor.Iy, StringComparison.Ordinal));
                    var attempt = Movement.TryBeginDrag(s, id);
                    string where = c.Label + " trial " + trial + " " + shape.Id;
                    if (!moves)
                    {
                        Assert.IsFalse(attempt.Ok, where);
                        Assert.AreEqual(PickFailures.Immovable, attempt.Reason, where);
                        continue;
                    }
                    if (!attempt.Ok) throw new InvalidOperationException(where + ": expected a session, got " + attempt.Reason);
                    var d = attempt.Session!;
                    var got = new Dictionary<string, int>();
                    foreach (var n in d.ReachableNodes()) got[Key(n)] = d.DistanceFromStart(n);
                    CollectionAssert.AreEqual(CellModel.Entries(want), CellModel.Entries(got), where + " R");
                    foreach (var n in d.ReachableNodes())
                    {
                        CollectionAssert.AreEqual(
                            m.Neighbours(n).Select(Key).OrderBy(k => k, StringComparer.Ordinal).ToArray(),
                            d.Neighbours(n).Select(Key).OrderBy(k => k, StringComparer.Ordinal).ToArray(),
                            where + " edges of " + Key(n));
                        Assert.AreEqual(m.Classify(starts, n), CellModel.DropName(d.Classify(n)), where + " class of " + Key(n));
                    }
                    compared++;
                }
            }
            Assert.Greater(compared, 400);
        }

        // --- E-56: yard air -----------------------------------------------------------------------------------------------

        /// <summary>Bölüm 1 size (Wy 4, Hy 4, Ws 2, Hs 5 → H 5): `a` = D2_0 Y at (0,2), the rest of column 0 full below it.</summary>
        private static GameState Bolum1() => Builders.InitialState(Sized(new GeoCase(4, 4, 2, 5), new LevelSpec
        {
            Id = 1,
            Moves = 11,
            Wall = new WallSpec { Height = 3 },
            Plan = new[] { "WW", "YY", "WW", "YW", "YW" },
            Pieces = new[] { new P("D2_0", "Y", 0, 2), new P("D2_0", "W", 0, 0), new P("D2_90", "W", 1, 0) },
        }));

        [Test(Description = "K-49 yard air drop cancels (E-56): `a` released at anchor (0,4) (yard air + crane area) or (0,3) (cell (0,4) y ≥ Hy) is K-07 row 3; no move spent, the block stays at (0,2)")]
        public void K49_yard_air_drop_cancels_E56_release_at_0_4_or_0_3_is_K07_row_3()
        {
            var s = Bolum1();
            var d = Boards.Drag(s, 0);
            Assert.IsTrue(d.IsReachable(F(0, 4))); // yard air is passed while dragging (K-05)
            Assert.IsTrue(d.IsReachable(F(0, 5))); // crane area y = H, H + 1
            Assert.IsFalse(d.IsReachable(F(0, 6))); // K-01: a cell at y = H + 2 does not exist
            foreach (var at in new[] { F(0, 4), F(0, 3) })
                Boards.AssertDrop(d.Classify(at), DropKinds.Cancel, CancelReasons.CraneOverYard, 3, null, Key(at));
            Boards.AssertDrop(d.Classify(F(2, 2)), DropKinds.Yard, null, 2);

            var before = Boards.Snapshot(s);
            var sink = new ArraySink();
            var res = Moves.ApplyMove(s, new DragMove(0, F(0, 4)), sink);
            Assert.AreEqual(MoveStatuses.Cancelled, res.Status);
            Assert.AreEqual(CancelReasons.CraneOverYard, res.Reason);
            CollectionAssert.AreEqual(before, s.Buf);
            Assert.AreEqual(11, Hdr(s, H.MovesLeft));
            CollectionAssert.AreEqual(new[] { 0, 2, Zone.Yard }, new[] { PieceX(s, 0), PieceY(s, 0), PieceZone(s, 0) });
            Assert.AreEqual(MoveStatuses.Cancelled, Moves.ApplyMove(s, new DragMove(0, F(0, 3))).Status);
        }

        [Test(Description = "K-05 K-49 over the wall on a short board: crane rows H, H + 1 are open, a 2-tall block crosses a wall of height H; release over the site falls (K-11)")]
        public void K05_K49_over_the_wall_on_a_short_board_crane_rows_H_H_plus_1_are_open_a_2_tall_block_crosses_a_wall_of_height_H()
        {
            var tall = Builders.InitialState(Sized(new GeoCase(4, 4, 2, 5), new LevelSpec
            {
                Id = 1,
                Wall = new WallSpec { Height = 5 },
                Plan = new[] { "WW", "YY", "WW", "YW", "YW" },
                Pieces = new[] { new P("D2_0", "Y", 0, 2) },
            }));
            var d = Boards.Drag(tall, 0);
            Assert.IsTrue(d.CanCrossWall);
            Assert.IsTrue(d.IsReachable(F(4, 5)));
            Boards.AssertDrop(d.Classify(F(4, 5)), DropKinds.SiteFree, null, 6);
            // a 3-tall block cannot clear a wall of height H: (H + 2) − height = 2 (K-05)
            var i3 = Boards.Drag(Builders.InitialState(Sized(new GeoCase(4, 4, 2, 5), new LevelSpec
            {
                Id = 11,
                Wall = new WallSpec { Height = 5 },
                Plan = new[] { "WW" },
                Pieces = new[] { new P("I3_0", "Y", 0, 0) },
            })), 0);
            Assert.IsFalse(i3.ReachableNodes().Any(n => n.Ix >= 4));
        }

        // --- E-57: drops over the yard --------------------------------------------------------------------------------------

        /// <summary>Bölüm 5 size class (Hy 4, H 5): column 0 full at y 0–2, a truck D2_0 for segment 1 at x = 0.</summary>
        private static GameState TruckBoard(int col1Full)
        {
            var pieces = new List<P> { new P("I3_0", "W", 0, 0), new P("D2_0", "W", 2, 0), new P("D2_0", "W", 3, 0) };
            if (col1Full > 0) pieces.Add(new P(col1Full == 1 ? "B1_0" : col1Full == 2 ? "D2_0" : "I3_0", "W", 1, 0));
            return Builders.InitialState(Sized(new GeoCase(4, 4, 2, 5), new LevelSpec
            {
                Id = 5,
                Plans = new[] { new[] { "WW", "WW", "WW", "WW", "WW" }, new[] { "YY", "YY", "YY", "YY", "YY" } },
                Pieces = pieces.ToArray(),
                Batches = new[] { new BatchSpec { ForSegment = 1, Pieces = new[] { new P("D2_0", "Y", 0, 4) } } },
            }));
        }

        private static int TruckId(GameState s) => s.Lvl.Batches.Length > 1 && s.Lvl.Batches[1].PieceIds.Length > 0 ? s.Lvl.Batches[1].PieceIds[0] : -1;

        [Test(Description = "K-49 yard air truck candidate rejected (E-57): D2_0 dropped in column 0 from y = 5 would rest at (0,3) with (0,4) in yard air → next K-25 column; none fits → stays queued (K-26)")]
        public void K49_yard_air_truck_candidate_rejected_E57_next_K25_column_none_fits_stays_queued_K26()
        {
            var s = TruckBoard(1);
            int id = TruckId(s);
            var shape = Shapes.ShapeById("D2_0");
            Assert.AreEqual(5, Placement.YardDropRow(s.Lvl.Geo, shape));
            Assert.AreEqual(-1, Placement.DropIntoYard(s, shape, 0)); // (0,3)–(0,4): (0,4) y ≥ Hy
            Assert.AreEqual(1, Placement.DropIntoYard(s, shape, 1));
            Placement.MovePiece(s, id, PiecePlace.Queue(0, 4));
            Assert.AreEqual(new Anchor(1, 1), Delivery.DeliveryLanding(s, id)!.Value);
            var res = Delivery.DeliverQueue(s);
            CollectionAssert.AreEqual(new[] { new Delivered(id, new Anchor(1, 5), new Anchor(1, 1), 4) }, res.Delivered);
            Boards.ExpectConsistent(s);

            // every column full above y 1: no candidate keeps both cells at y ≤ 3 → the block stays in the truck
            var full = TruckBoard(3);
            SetYardOcc(full, 2, 2, -1);
            SetYardOcc(full, 3, 2, -1);
            int fid = TruckId(full);
            Placement.MovePiece(full, fid, PiecePlace.Queue(0, 4));
            Assert.IsNull(Delivery.DeliveryLanding(full, fid));
            var none = Delivery.DeliverQueue(full);
            Assert.AreEqual(0, none.Delivered.Length);
            Assert.AreEqual(1, none.Queued);
        }

        [Test(Description = "K-17 K-49 bounce-back step 2 uses the same drop: start row (H + 2) − h, columns 0 … Wy − w nearest the start, a landing needs y ≤ Hy − 1")]
        public void K17_K49_bounce_back_step_2_uses_the_same_drop_start_row_H_plus_2_minus_h_columns_nearest_the_start_a_landing_needs_y_at_most_Hy_minus_1()
        {
            var s = TruckBoard(2);
            // columns 3 and 2 hold 3 rows: a D2_0 would rest at y 3–4, (·,4) is yard air → rejected
            SetYardOcc(s, 2, 2, -1);
            SetYardOcc(s, 3, 2, -1);
            const int id = 3; // the col-1 D2_0 at (1,0), bounced from a site start x = Wy: candidates 3, 2, 1, 0
            var t = Placement.ReturnTarget(s, id, new ReturnOptions(true, PiecePlace.Site(4, 0, 0)));
            Assert.AreEqual(2, t.Step);
            Assert.AreEqual(new Anchor(1, 5), t.DropFrom!.Value);
            CollectionAssert.AreEqual(new[] { 1, 0 }, new[] { t.To.X, t.To.Y }); // itself excluded: its own column 1 is free again
            // nothing fits: step 3, the end of the truck queue, first truck column clamped to Wy − w
            SetYardOcc(s, 1, 2, -1);
            SetYardOcc(s, 0, 3, -1);
            var q = Placement.ReturnTarget(s, id, new ReturnOptions(true, PiecePlace.Site(4, 0, 0)));
            Assert.AreEqual(new ReturnTarget(3, PiecePlace.Queue(3, 4), null), q);
        }

        // --- K-44 Ağır Yük ------------------------------------------------------------------------------------------------

        [Test(Description = "K-44 cargo never leaves the yard while dragged: every node of R keeps all cells at x ≤ Wy − 1, y ≤ Hy − 1, FREE only (Q9 and I5, every geometry, open wall, a gap)")]
        public void K44_cargo_never_leaves_the_yard_while_dragged_every_node_of_R_keeps_all_cells_in_the_yard_FREE_only()
        {
            foreach (var c in G)
            {
                foreach (var shapeId in new[] { "Q9_0", "I5_0", "I5_180" })
                {
                    var shape = Shapes.ShapeById(shapeId);
                    if (shape.W > c.Wy || shape.H > c.Hy) continue;
                    var s = Builders.InitialState(Sized(c, new LevelSpec
                    {
                        Id = 8,
                        Wall = new WallSpec { Height = 2, Gaps = new[] { J.O("type", "static", "y", 0, "size", 1) } },
                        Plan = new[] { new string('W', c.Ws) },
                        Pieces = new[] { new P(shapeId, "W", 0, 0) },
                    }));
                    var d = Boards.Drag(s, 0);
                    string where = c.Label + " " + shapeId;
                    Assert.IsTrue(d.Cargo, where);
                    Assert.IsFalse(d.CanCrossWall || d.CanEnterRail, where);
                    foreach (var n in d.ReachableNodes())
                    {
                        Assert.AreEqual(Movement.Free, n.Mode, where + " " + Key(n));
                        foreach (var (x, y) in CellsOf(shape, n.Ix, n.Iy))
                            Assert.IsTrue(Coords.IsYardCell(s.Lvl.Geo, x, y), where + " " + Key(n));
                        if (Key(n) != Key(d.Start)) Boards.AssertDrop(d.Classify(n), DropKinds.Yard, null, 2, null, where + " " + Key(n));
                    }
                    // all of the empty yard is reachable: (Wy − w + 1) × (Hy − h + 1) anchors
                    Assert.AreEqual((c.Wy - shape.W + 1) * (c.Hy - shape.H + 1), d.ReachableCount, where);
                }
            }
        }

        [Test(Description = "K-44 cargo is I5 and Q9 in every orientation, nothing else (the pre-2R \"w ≥ 3 is heavy\" branch is gone)")]
        public void K44_cargo_is_I5_and_Q9_in_every_orientation_nothing_else()
        {
            foreach (var sh in Shapes.All) Assert.AreEqual(sh.Kind == "I5" || sh.Kind == "Q9", Movement.IsCargoShape(sh), sh.Id);
            var wide = Boards.Drag(Builders.InitialState(Sized(new GeoCase(5, 7, 3, 7), new LevelSpec
            {
                Id = 11,
                Plan = new[] { "WWW" },
                Pieces = new[] { new P("L4_90", "W", 0, 0) },
            })), 0);
            Assert.IsFalse(wide.Cargo);
            Assert.IsTrue(wide.ReachableNodes().Any(n => n.Ix >= 5 && wide.Classify(n).Kind == DropKinds.SiteFree));
        }

        [Test(Description = "K-44 blockedCargo once per hold: the first follow with p.x ≥ Wy or p.y ≥ Hy signals, later ones do not; a new hold signals again; material blocks never")]
        public void K44_blockedCargo_once_per_hold_the_first_follow_outside_the_yard_signals_a_new_hold_signals_again_material_blocks_never()
        {
            var c = new GeoCase(4, 5, 2, 5);
            var s = Builders.InitialState(Sized(c, new LevelSpec { Id = 8, Plan = new[] { "WW" }, Pieces = new[] { new P("Q9_0", "W", 0, 0) } }));
            var d = Boards.Drag(s, 0);
            Assert.IsFalse(d.Follow(0.6, 0.4).BlockedCargo); // inside the yard
            Assert.IsFalse(d.Follow(0.9, 1.8).BlockedCargo);
            var out_ = d.Follow(4, 1);
            Assert.IsTrue(out_.BlockedCargo); // p.x ≥ Wy
            Assert.IsTrue(d.IsReachable(out_.Node) && out_.Node.Ix + 3 <= 4); // the block itself stays in the yard
            Assert.IsFalse(d.Follow(5.5, 1).BlockedCargo); // at most once per hold
            Assert.IsFalse(d.Follow(0.5, 0.5).BlockedCargo);
            Assert.IsFalse(d.Follow(0.5, 6).BlockedCargo);
            var up = Boards.Drag(s, 0);
            Assert.IsFalse(up.Follow(1, 4.9).BlockedCargo);
            Assert.IsTrue(up.Follow(1, 5).BlockedCargo); // p.y ≥ Hy
            // a material block that leaves the yard gets no cargo signal
            var b1 = Boards.Drag(Builders.InitialState(Sized(c, new LevelSpec { Id = 8, Plan = new[] { "WW" }, Pieces = new[] { new P("B1_0", "W", 0, 0) } })), 0);
            Assert.IsFalse(b1.Cargo);
            foreach (var (px, py) in new[] { (4.0, 1.0), (5.0, 6.0), (0.0, 6.0) })
                Assert.IsFalse(b1.Follow(px, py).BlockedCargo, px + "," + py);
        }
        // --- K-44 cargo through the pipeline ------------------------------------------------------------------------------

        [Test(Description = "K-44 cargo drag through the pipeline: a slide is a yard move (1 move), the drop never reaches the site")]
        public void K44_cargo_drag_through_the_pipeline_a_slide_is_a_yard_move()
        {
            var s = Builders.InitialState(Sized(new GeoCase(6, 5, 2, 6), new LevelSpec
            {
                Id = 8,
                Moves = 10,
                Plan = new[] { "WW" },
                Pieces = new[] { new P("Q9_0", "W", 0, 0) },
            }));
            var res = Moves.ApplyMove(s, new DragMove(0, F(3, 2)));
            Assert.AreEqual(MoveStatuses.Applied, res.Status);
            CollectionAssert.AreEqual(new[] { 3, 2, Zone.Yard, 9 }, new[] { PieceX(s, 0), PieceY(s, 0), PieceZone(s, 0), Hdr(s, H.MovesLeft) });
            // (3,3) puts the top row at y 5 = Hy: not a node of R → the record is cancelled as invalid, nothing changes
            var before = Boards.Snapshot(s);
            Assert.AreEqual(MoveStatuses.Cancelled, Moves.ApplyMove(s, new DragMove(0, F(3, 3))).Status);
            CollectionAssert.AreEqual(before, s.Buf);
        }

        // --- invariants under random play ---------------------------------------------------------------------------------

        private static int CellsInPlay(GameState s)
        {
            int n = 0;
            for (int id = 0; id < s.Lvl.Layout.Counts.Pieces; id++)
            {
                int z = PieceZone(s, id);
                if (z == Zone.Yard || z == Zone.Site || z == Zone.Queue || z == Zone.Pending) n += Shapes.ShapeByIndex(PieceShape(s, id)).CellCount;
            }
            return n;
        }

        [Test(Description = "K-49 no overlap, cells conserved, encode/decode round trip, hash of the copy, replay = live session (10 geometries × 6 levels × up to 40 moves)")]
        public void K49_no_overlap_cells_conserved_round_trip_hash_of_the_copy_replay_equals_live_session()
        {
            var rng = new Mulberry32(20261007);
            int moves = 0;
            int wrong = 0;
            foreach (var c in G)
            {
                for (int n = 0; n < 6; n++)
                {
                    var lvl = RandomLevel(c, rng, 2);
                    var sink = new ArraySink();
                    var live = GameSession.Start(lvl, null, 0, null, sink);
                    var applied = new List<GameEvent>(sink.Events); // events of the start action and of every applied move
                    int total = CellsInPlay(live.State);
                    for (int step = 0; step < 40 && live.Outcome == BoosterOutcomes.Playing; step++)
                    {
                        var s = live.State;
                        var pickable = new List<int>();
                        for (int id = 0; id < lvl.Layout.Counts.Pieces; id++) if (Movement.TryBeginDrag(s, id).Ok) pickable.Add(id);
                        if (pickable.Count == 0) break;
                        int pid = pickable[rng.NextInt(pickable.Count)];
                        var d = Boards.Drag(s, pid);
                        var targets = d.ReachableNodes().Where(t => !d.StartNodes.Any(st => Key(st) == Key(t))).ToArray();
                        int ti = rng.NextInt(targets.Length); // TS `targets[rng.nextInt(targets.length)] ?? d.start` (nextInt(0) throws in both)
                        var to = ti < targets.Length ? targets[ti] : d.Start;
                        sink.Clear();
                        var res = live.Commit(new DragMove(pid, to), sink);
                        if (res.Status == MoveStatuses.Applied)
                        {
                            moves++;
                            applied.AddRange(sink.Events); // cancelled releases are not logged (K-07): their events never replay
                        }
                        if (sink.Events.Any(e => e is PieceBouncedEvent)) wrong++;
                        string where = c.Label + " level " + n + " step " + step;
                        CollectionAssert.IsEmpty(Grid.StateInvariantErrors(s), where);
                        Assert.AreEqual(total, CellsInPlay(s), where);
                        var copy = DecodeState(lvl, EncodeState(s));
                        CollectionAssert.AreEqual(s.Buf, copy.Buf, where);
                        Assert.AreEqual(Hash.HashHex(s), Hash.HashHex(copy), where);
                        for (int id2 = 0; id2 < lvl.Layout.Counts.Pieces; id2++)
                        {
                            if (PieceZone(s, id2) != Zone.Yard) continue;
                            foreach (var cell in Movement.BlockCells(Shapes.ShapeByIndex(PieceShape(s, id2)), PieceX(s, id2), PieceY(s, id2)))
                                Assert.IsTrue(Coords.IsYardCell(lvl.Geo, cell.X, cell.Y), where + " piece " + id2);
                        }
                    }
                    var replaySink = new ArraySink();
                    var replayed = GameSession.Replay(lvl, live.Log, null, replaySink);
                    CollectionAssert.AreEqual(live.State.Buf, replayed.State.Buf, c.Label);
                    Assert.AreEqual(Moves.EventLogHash(applied), Moves.EventLogHash(replaySink.Events), c.Label);
                }
            }
            Assert.Greater(moves, 600);
            Assert.Greater(wrong, 20);
        }
    }
}
