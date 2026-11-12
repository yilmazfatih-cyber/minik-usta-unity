#nullable enable
// Port of src/core/boosters.ts (web repo, source of truth).
using System;
using System.Collections.Generic;
using static MinikUsta.Core.State;

namespace MinikUsta.Core
{
    /// <summary>Session outcome as seen by booster preconditions (core/session.ts `SessionOutcome`). TS `BoosterOutcome`.</summary>
    public static class BoosterOutcomes
    {
        public const string Playing = "playing";
        public const string OutOfMoves = "outOfMoves";
        public const string Won = "won";
        public const string Lost = "lost";
        public const string Exited = "exited";
    }

    /// <summary>Why Undo is not available (TS `UndoBlock`).</summary>
    public static class UndoBlocks
    {
        public const string LevelOver = "levelOver";
        public const string LossWindow = "lossWindow";
        public const string NoDragMove = "noDragMove";
    }

    public readonly struct UndoView
    {
        public readonly string Outcome;
        /// <summary>A pre-move snapshot of the last committed action exists: it was a drag and was not undone yet.</summary>
        public readonly bool LastDragUndoable;

        public UndoView(string outcome, bool lastDragUndoable)
        {
            Outcome = outcome;
            LastDragUndoable = lastDragUndoable;
        }
    }

    /// <summary>Why a booster target is refused (nothing happens, the booster is not spent; GDD §10). TS `BoosterReject`.</summary>
    public static class BoosterRejects
    {
        public const string LevelOver = "levelOver";
        public const string NoTarget = "noTarget";
        public const string SameSpot = "sameSpot";
        public const string BadRotation = "badRotation";
        public const string NotEmpty = "notEmpty";
        public const string NotCorrect = "notCorrect";
        public const string Precheck = "precheck";
        public const string NoTrowel = "noTrowel";
        public const string LegacyTrowel = "legacyTrowel";
        /// <summary>v3c K-58 item 9 (e), D-104: the crane has no yard target for a pallet layer (site only).</summary>
        public const string PalletSiteOnly = "palletSiteOnly";
    }

    /// <summary>What a hammer hit did (step 1 of the mini pipeline). `Kind` is a `HammerTargetKinds` value; the other fields depend on it.</summary>
    public sealed class HammerEffect
    {
        public readonly string Kind;
        /// <summary>cargo, siteDebris, stuckMortar.</summary>
        public readonly int PieceId;
        /// <summary>cargo: where it stood.</summary>
        public readonly At? At;
        /// <summary>siteDebris / stuckMortar: where it stood.</summary>
        public readonly At? From;
        /// <summary>siteDebris / stuckMortar: the K-17 target it went to.</summary>
        public readonly ReturnTarget? Target;
        /// <summary>crate / cementBag / chain: the entity a rule handled.</summary>
        public readonly EntityRef? Entity;

        private HammerEffect(string kind, int pieceId, At? at, At? from, ReturnTarget? target, EntityRef? entity)
        {
            Kind = kind;
            PieceId = pieceId;
            At = at;
            From = from;
            Target = target;
            Entity = entity;
        }

        public static HammerEffect Cargo(int pieceId, At at) => new HammerEffect(HammerTargetKinds.Cargo, pieceId, at, null, null, null);
        public static HammerEffect Returned(string kind, int pieceId, At from, ReturnTarget target) => new HammerEffect(kind, pieceId, null, from, target, null);
        public static HammerEffect Ruled(string kind, EntityRef entity) => new HammerEffect(kind, -1, null, null, null, entity);
    }

    /// <summary>K-37 (b): one site spot of a block in one of its orientations.</summary>
    public readonly struct CraneSpot : IEquatable<CraneSpot>
    {
        public readonly int Rotation;
        public readonly Anchor At;

        public CraneSpot(int rotation, Anchor at)
        {
            Rotation = rotation;
            At = at;
        }

        public bool Equals(CraneSpot o) => Rotation == o.Rotation && At.Equals(o.At);
        public override bool Equals(object? obj) => obj is CraneSpot c && Equals(c);
        public override int GetHashCode() => Rotation * 31 + At.GetHashCode();
        public override string ToString() => Rotation + "@" + At;
    }

    /// <summary>What a crane move did.</summary>
    public sealed class CraneEffect
    {
        public readonly At From;
        public readonly At To;
        public readonly ShapeDef Shape;
        public readonly bool ToSite;
        public readonly RevealedCell[] Revealed;

        public CraneEffect(At from, At to, ShapeDef shape, bool toSite, RevealedCell[] revealed)
        {
            From = from;
            To = to;
            Shape = shape;
            ToSite = toSite;
            Revealed = revealed;
        }
    }

    /// <summary>What a Golden Trowel use did.</summary>
    public sealed class TrowelEffect
    {
        public readonly At From;
        public readonly At To;
        public readonly RevealedCell[] Revealed;
        public readonly int Trowels;

        public TrowelEffect(At from, At to, RevealedCell[] revealed, int trowels)
        {
            From = from;
            To = to;
            Revealed = revealed;
            Trowels = trowels;
        }
    }

    /// <summary>In-level booster slots of K-54 (+ the trowel strip text, UX §5.2). TS `BoosterSlot`.</summary>
    public static class BoosterSlots
    {
        public const string Hammer = "hammer";
        public const string Crane = "crane";
        public const string Undo = "undo";
        public const string PaintBrush = "paintBrush";
        public const string Trowel = "trowel";

        public static readonly string[] All = { Hammer, Crane, Undo, PaintBrush, Trowel };
    }

    /// <summary>K-54 item 2 target predicates per slot. TS `Record&lt;BoosterSlot, boolean&gt;`.</summary>
    public sealed class BoosterTargets : IEquatable<BoosterTargets>
    {
        public readonly bool Hammer;
        public readonly bool Crane;
        public readonly bool Undo;
        public readonly bool PaintBrush;
        public readonly bool Trowel;

        public BoosterTargets(bool hammer, bool crane, bool undo, bool paintBrush, bool trowel)
        {
            Hammer = hammer;
            Crane = crane;
            Undo = undo;
            PaintBrush = paintBrush;
            Trowel = trowel;
        }

        public bool this[string slot]
        {
            get
            {
                switch (slot)
                {
                    case BoosterSlots.Hammer: return Hammer;
                    case BoosterSlots.Crane: return Crane;
                    case BoosterSlots.Undo: return Undo;
                    case BoosterSlots.PaintBrush: return PaintBrush;
                    case BoosterSlots.Trowel: return Trowel;
                    default: throw new ArgumentOutOfRangeException(nameof(slot), "unknown booster slot " + slot);
                }
            }
        }

        public bool Equals(BoosterTargets? o) => o != null && Hammer == o.Hammer && Crane == o.Crane && Undo == o.Undo && PaintBrush == o.PaintBrush && Trowel == o.Trowel;
        public override bool Equals(object? obj) => obj is BoosterTargets t && Equals(t);
        public override int GetHashCode() => (Hammer ? 1 : 0) | (Crane ? 2 : 0) | (Undo ? 4 : 0) | (PaintBrush ? 8 : 0) | (Trowel ? 16 : 0);
        public override string ToString() => "hammer " + Hammer + " crane " + Crane + " undo " + Undo + " paintBrush " + PaintBrush + " trowel " + Trowel;
    }

    /// <summary>
    /// In-level boosters: preconditions, effects and target predicates (docs/GDD.md §10, K-33, K-36…K-39, K-54; TECH_DESIGN
    /// §2R.2, §2R.15 item 7). Pure state helpers: the events and the K-35 mini pipeline belong to the move pipeline.
    ///
    /// Faz 2R (full cover, K-47): no booster creates or destroys a material block.
    /// - Hammer (K-36): smashes an Ağır Yük (gone), lifts the stuck mortar or moves a site debris down to the yard (K-17 step
    ///   2, else the queue end); crates, bags and chains go to their obstacle rules (`MoveHooks.Hammer`). Never a material
    ///   block in the yard, a locked or a queued block.
    /// - Crane (K-37): a yard block (material or cargo; chained / wet ones are refused by the rules' `canPick`), a site
    ///   debris or a stuck mortar block, to (a) an empty yard spot in its own orientation or (b) a correct site spot
    ///   (material only; it may turn clockwise there, width ≤ Ws). It locks without streak or YAO.
    /// - Paint brush (K-38): swaps the colours of two yard material blocks with equal cell counts and different colours.
    /// - Golden Trowel (K-33): a yard material block (not locked / chained / wet) flies to a spot of its `P` set
    ///   (`Deadlock.CorrectSpots`) and locks; no streak, no YAO.
    /// - Pre-check (CL-2R-05): crane, paint brush and trowel targets whose result state is a D3a dead end are invalid
    ///   (nothing happens, nothing is spent). It never changes the slot state (K-54).
    /// - Undo (K-39): a session action (core/session.ts); `UndoBlock` is its precondition.
    /// </summary>
    public static class Boosters
    {
        // --- K-39 Undo -----------------------------------------------------------------------------------------------------

        /// <summary>`canUseUndo` (TECH §6.4): null when Undo may be used now, else the reason (the UI greys the slot).</summary>
        public static string? UndoBlock(UndoView view)
        {
            if (view.Outcome == BoosterOutcomes.OutOfMoves) return UndoBlocks.LossWindow;
            if (view.Outcome != BoosterOutcomes.Playing) return UndoBlocks.LevelOver;
            if (!view.LastDragUndoable) return UndoBlocks.NoDragMove;
            return null;
        }

        // --- shared --------------------------------------------------------------------------------------------------------

        private static bool Won(GameState s) => (Hdr(s, H.Flags) & StateFlag.Won) != 0;

        private static bool ValidId(GameState s, int id) => id >= 0 && id < s.Lvl.Layout.Counts.Pieces;

        private static bool IsMaterialPiece(GameState s, int id) => Goals.IsMaterial(s, id);

        private static bool Locked(GameState s, int id) => (PieceFlagBits(s, id) & FlagBit.Locked) != 0;

        /// <summary>The rules allow picking the block (K-09 (c): Y3 chain, Y4 wet).</summary>
        private static bool RulesAllow(GameState s, int id, MoveHooks hooks) => hooks.Drag?.CanPick == null || hooks.Drag.CanPick(s, id);

        /// <summary>An unlocked site block of the visible segment that is a wrong object: debris (S4) or stuck mortar (Y8).</summary>
        private static bool IsSiteWrongObject(GameState s, int id)
        {
            if (PieceZone(s, id) != Zone.Site || Locked(s, id) || PieceSeg(s, id) != Grid.VisibleSegment(s)) return false;
            return (PieceFlagBits(s, id) & (FlagBit.Debris | FlagBit.Stuck)) != 0;
        }

        /// <summary>The orientation `kind_rotation` of a piece's kind (canonical shape).</summary>
        private static ShapeDef Turned(ShapeDef shape, int rotation) => Shapes.ShapeByIndex(Shapes.ShapeById(shape.Kind + "_" + rotation).CanonicalIndex);

        // --- K-36 Hammer ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// K-36 target kind of `target` (`HammerTargetKinds`), or null when it is not a hammer target now: an Ağır Yük in the
        /// yard, a site debris or a stuck mortar block of the visible segment, or an entity a rule accepts
        /// (`hooks.Hammer.CanHit`: crate, bag, chain).
        /// </summary>
        public static string? HammerTargetKind(GameState s, HammerTarget target, MoveHooks hooks)
        {
            if (target.Obstacle.HasValue)
            {
                int index = target.Obstacle.Value;
                if (index < 0 || index >= s.Lvl.Obstacles.Length) return null;
                var o = s.Lvl.Obstacles[index];
                var entity = EntityRef.Obstacle(index);
                if (hooks.Hammer == null || !hooks.Hammer.CanHit(s, entity)) return null;
                return o.Type == "crate" ? HammerTargetKinds.Crate : o.Type == "cement_bag" ? HammerTargetKinds.CementBag : null;
            }
            int id = target.PieceId ?? -1;
            if (!ValidId(s, id)) return null;
            int zone = PieceZone(s, id);
            if (zone == Zone.Yard && Movement.IsCargoShape(Shapes.ShapeByIndex(PieceShape(s, id)))) return HammerTargetKinds.Cargo;
            if (IsSiteWrongObject(s, id))
                return (PieceFlagBits(s, id) & FlagBit.Debris) != 0 ? HammerTargetKinds.SiteDebris : HammerTargetKinds.StuckMortar;
            if (zone == Zone.Yard && hooks.Hammer != null && hooks.Hammer.CanHit(s, EntityRef.Piece(id)))
                return hooks.Hammer.Kind?.Invoke(s, EntityRef.Piece(id)) ?? HammerTargetKinds.Chain;
            return null;
        }

        /// <summary>K-36 / K-54 hammer target set (piece targets in id order, then obstacles in index order).</summary>
        public static HammerTarget[] HammerTargets(GameState s, MoveHooks hooks)
        {
            var o = new List<HammerTarget>();
            if (Won(s)) return o.ToArray();
            int P = s.Lvl.Layout.Counts.Pieces;
            for (int id = 0; id < P; id++)
                if (HammerTargetKind(s, HammerTarget.Piece(id), hooks) != null) o.Add(HammerTarget.Piece(id));
            foreach (var ob in s.Lvl.Obstacles)
            {
                if ((ob.Type == "crate" || ob.Type == "cement_bag") && ObstacleField(s, ob.Index, OF.Hp) > 0)
                {
                    if (HammerTargetKind(s, HammerTarget.ObstacleAt(ob.Index), hooks) != null) o.Add(HammerTarget.ObstacleAt(ob.Index));
                }
            }
            return o.ToArray();
        }

        /// <summary>Applies a hammer hit to a valid target (check `HammerTargetKind` first; throws otherwise).</summary>
        public static HammerEffect ApplyHammer(RuleContext ctx, HammerTarget target, MoveHooks hooks)
        {
            var s = ctx.S;
            string? kind = HammerTargetKind(s, target, hooks);
            if (kind == null) throw new ArgumentOutOfRangeException(nameof(target), "applyHammer: not a hammer target");
            if (target.Obstacle.HasValue)
            {
                var entity = EntityRef.Obstacle(target.Obstacle.Value);
                hooks.Hammer?.Hit(ctx, entity);
                return HammerEffect.Ruled(kind == HammerTargetKinds.Crate ? HammerTargetKinds.Crate : HammerTargetKinds.CementBag, entity);
            }
            int id = target.PieceId ?? -1;
            if (kind == HammerTargetKinds.Cargo)
            {
                var at = At.Yard(PieceX(s, id), PieceY(s, id));
                Placement.MovePiece(s, id, PiecePlace.Gone(at.X, at.Y));
                return HammerEffect.Cargo(id, at);
            }
            if (kind == HammerTargetKinds.SiteDebris || kind == HammerTargetKinds.StuckMortar)
            {
                var from = Placement.PlaceAt(s, Placement.PiecePlace(s, id)) ?? At.Site(PieceX(s, id), PieceY(s, id), Grid.VisibleSegment(s));
                var ret = Placement.ReturnTarget(s, id, new ReturnOptions(skipStart: true));
                Placement.MovePiece(s, id, ret.To);
                return HammerEffect.Returned(kind, id, from, ret);
            }
            var piece = EntityRef.Piece(id);
            hooks.Hammer?.Hit(ctx, piece);
            return HammerEffect.Ruled(kind == HammerTargetKinds.Wrap ? HammerTargetKinds.Wrap : HammerTargetKinds.Chain, piece);
        }

        // --- K-37 Crane ----------------------------------------------------------------------------------------------------

        /// <summary>K-37: the crane may lift this block (yard block allowed by the rules, or a site debris / stuck mortar block).</summary>
        public static bool CraneSelectable(GameState s, int id, MoveHooks hooks)
        {
            if (!ValidId(s, id) || Won(s)) return false;
            int zone = PieceZone(s, id);
            if (zone == Zone.Yard) return !Locked(s, id) && !(s.Lvl.HasStacks && Grid.IsCovered(s, id)) && RulesAllow(s, id, hooks);
            return IsSiteWrongObject(s, id);
        }

        /// <summary>K-37 (a): every cell of `shape` at `(x, y)` inside the yard and empty (the piece itself excluded).</summary>
        private static bool YardSpotFree(GameState s, ShapeDef shape, int x, int y, int self)
        {
            int wy = s.Lvl.Geo.Wy;
            int hy = s.Lvl.Geo.Hy;
            if (x < 0 || y < 0 || x + shape.W > wy || y + shape.H > hy) return false;
            int own = s.Lvl.HasStacks ? Grid.OwnCellCode(s, self) : self + 1;
            foreach (var c in shape.Cells)
            {
                int v = YardOcc(s, x + c.X, y + c.Y);
                if (v != 0 && v != own) return false;
            }
            return true;
        }

        /// <summary>K-37 (b): the site spots of a material block in every orientation of width ≤ Ws (rotation clockwise from 0).</summary>
        public static CraneSpot[] CraneSiteSpots(GameState s, int id)
        {
            var o = new List<CraneSpot>();
            if (!ValidId(s, id) || !IsMaterialPiece(s, id)) return o.ToArray();
            var shape = Shapes.ShapeByIndex(PieceShape(s, id));
            if (Movement.IsCargoShape(shape)) return o.ToArray();
            var cc = s.Lvl.Pieces[id].CellColors;
            var seen = new HashSet<int>();
            foreach (int rotation in Rotations.All)
            {
                var t = Turned(shape, rotation);
                if (!seen.Add(t.Index)) continue;
                // v3 Y9: a two-tone block keeps its orientation (no rotated colour table)
                if (cc != null && t.Index != shape.Index) continue;
                foreach (var at in Deadlock.CorrectSpots(s, t, PieceColor(s, id), id, cc)) o.Add(new CraneSpot(rotation, at));
            }
            return o.ToArray();
        }

        /// <summary>Precondition of a crane move (target validity only, no pre-check): null when allowed.</summary>
        private static string? CraneTargetBlock(GameState s, CraneMove move, MoveHooks hooks)
        {
            if (Won(s)) return BoosterRejects.LevelOver;
            int id = move.PieceId;
            if (!CraneSelectable(s, id, hooks)) return BoosterRejects.NoTarget;
            var shape = Shapes.ShapeByIndex(PieceShape(s, id));
            if (!(move.Rotation == 0 || move.Rotation == 90 || move.Rotation == 180 || move.Rotation == 270)) return BoosterRejects.BadRotation;
            var t = Turned(shape, move.Rotation);
            int x = move.ToX;
            int y = move.ToY;
            if (move.ToZone == At.ZoneYard)
            {
                if (s.Lvl.HasStacks && Movement.IsPalletLayer(s, id)) return BoosterRejects.PalletSiteOnly;
                if (t.Index != shape.Index) return BoosterRejects.BadRotation;
                if (PieceZone(s, id) == Zone.Yard && PieceX(s, id) == x && PieceY(s, id) == y) return BoosterRejects.SameSpot;
                return YardSpotFree(s, shape, x, y, id) ? null : BoosterRejects.NotEmpty;
            }
            if (!IsMaterialPiece(s, id) || Movement.IsCargoShape(shape)) return BoosterRejects.NoTarget;
            if (t.W > s.Lvl.Geo.Ws) return BoosterRejects.BadRotation;
            var ccTarget = s.Lvl.Pieces[id].CellColors;
            if (ccTarget != null && t.Index != shape.Index) return BoosterRejects.BadRotation;
            foreach (var a in Deadlock.CorrectSpots(s, t, PieceColor(s, id), id, ccTarget)) if (a.Ix == x && a.Iy == y) return null;
            return BoosterRejects.NotCorrect;
        }

        /// <summary>K-37 precondition (TECH §6.4 `canUse*`) including the D3a pre-check (E-60): null when the move may be applied.</summary>
        public static string? CraneRejection(GameState s, CraneMove move, MoveHooks hooks)
        {
            string? block = CraneTargetBlock(s, move, hooks);
            if (block != null) return block;
            var copy = CloneState(s);
            ApplyCraneEffect(copy, move);
            return Deadlock.PrecheckOk(copy) ? null : BoosterRejects.Precheck;
        }

        /// <summary>Applies a crane move whose target `CraneTargetBlock` accepts (no check here).</summary>
        public static CraneEffect ApplyCraneEffect(GameState s, CraneMove move)
        {
            int id = move.PieceId;
            var from = Placement.PlaceAt(s, Placement.PiecePlace(s, id)) ?? At.Yard(PieceX(s, id), PieceY(s, id));
            var t = Turned(Shapes.ShapeByIndex(PieceShape(s, id)), move.Rotation);
            var anchor = new Anchor(move.ToX, move.ToY);
            if (move.ToZone == At.ZoneYard)
            {
                Placement.MovePiece(s, id, Placement.YardPlace(anchor));
                return new CraneEffect(from, At.Yard(anchor.Ix, anchor.Iy), t, false, new RevealedCell[0]);
            }
            // the old cells leave with the old shape, the new cells come with the turned one
            Placement.MovePiece(s, id, PiecePlace.Gone(0, 0));
            SetPieceField(s, id, PF.Shape, t.Index);
            var place = Placement.SitePlace(s, anchor);
            Placement.MovePiece(s, id, place);
            var revealed = Placement.LockPiece(s, id);
            return new CraneEffect(from, At.Site(anchor.Ix, anchor.Iy, place.Seg), t, true, revealed);
        }

        /// <summary>K-54: the crane has a target now ((a) or (b) for some selectable block; the pre-check is not part of it).</summary>
        public static bool HasCraneTarget(GameState s, MoveHooks hooks)
        {
            if (Won(s)) return false;
            int P = s.Lvl.Layout.Counts.Pieces;
            int wy = s.Lvl.Geo.Wy;
            int hy = s.Lvl.Geo.Hy;
            for (int id = 0; id < P; id++)
            {
                if (!CraneSelectable(s, id, hooks)) continue;
                if (CraneSiteSpots(s, id).Length > 0) return true;
                if (s.Lvl.HasStacks && Movement.IsPalletLayer(s, id)) continue; // K-58 item 9 (e): no yard target
                var shape = Shapes.ShapeByIndex(PieceShape(s, id));
                bool inYard = PieceZone(s, id) == Zone.Yard;
                for (int y = 0; y + shape.H <= hy; y++)
                {
                    for (int x = 0; x + shape.W <= wy; x++)
                    {
                        if (inYard && x == PieceX(s, id) && y == PieceY(s, id)) continue;
                        if (YardSpotFree(s, shape, x, y, id)) return true;
                    }
                }
            }
            return false;
        }

        // --- K-38 Paint brush ----------------------------------------------------------------------------------------------

        /// <summary>K-38: block A / B of a swap: a yard material block that is not locked (buried, chained or wet allowed).</summary>
        public static bool PaintSelectable(GameState s, int id) =>
            ValidId(s, id) && PieceZone(s, id) == Zone.Yard && IsMaterialPiece(s, id) && !Locked(s, id) && !IsTwoTone(s, id) &&
            !(s.Lvl.HasStacks && Grid.IsCovered(s, id));

        /// <summary>K-38: `b` may be swapped with `a` (equal cell counts, different colours).</summary>
        public static bool PaintPairOk(GameState s, int a, int b)
        {
            if (a == b || !PaintSelectable(s, a) || !PaintSelectable(s, b)) return false;
            if (PieceColor(s, a) == PieceColor(s, b)) return false;
            return Shapes.ShapeByIndex(PieceShape(s, a)).CellCount == Shapes.ShapeByIndex(PieceShape(s, b)).CellCount;
        }

        /// <summary>K-38 B candidates for A (UI highlight), id order.</summary>
        public static int[] PaintPartners(GameState s, int a)
        {
            var o = new List<int>();
            int P = s.Lvl.Layout.Counts.Pieces;
            for (int b = 0; b < P; b++) if (PaintPairOk(s, a, b)) o.Add(b);
            return o.ToArray();
        }

        /// <summary>K-54: some pair can be swapped now.</summary>
        public static bool HasPaintTarget(GameState s)
        {
            if (Won(s)) return false;
            int P = s.Lvl.Layout.Counts.Pieces;
            for (int a = 0; a < P; a++) for (int b = a + 1; b < P; b++) if (PaintPairOk(s, a, b)) return true;
            return false;
        }

        /// <summary>K-38 precondition with the D3a pre-check: null when the swap may be applied.</summary>
        public static string? PaintRejection(GameState s, int a, int b)
        {
            if (Won(s)) return BoosterRejects.LevelOver;
            if (!PaintPairOk(s, a, b)) return BoosterRejects.NoTarget;
            var copy = CloneState(s);
            SwapColors(copy, a, b);
            return Deadlock.PrecheckOk(copy) ? null : BoosterRejects.Precheck;
        }

        /// <summary>Swaps the colours of `a` and `b` (shape, place and flags stay).</summary>
        public static void SwapColors(GameState s, int a, int b)
        {
            int ca = PieceColor(s, a);
            SetPieceField(s, a, PF.Color, PieceColor(s, b));
            SetPieceField(s, b, PF.Color, ca);
        }

        /// <summary>Colour code of a colour index (W for an unknown index).</summary>
        public static string ColorCodeOf(int index) => index >= 0 && index < ColorCodes.All.Length ? ColorCodes.All[index] : "W";

        // --- K-33 Golden Trowel --------------------------------------------------------------------------------------------

        /// <summary>K-33: the trowel may take this block (yard material, not locked, chained or wet).</summary>
        public static bool TrowelSelectable(GameState s, int id, MoveHooks hooks) =>
            ValidId(s, id) && PieceZone(s, id) == Zone.Yard && IsMaterialPiece(s, id) && !Locked(s, id) &&
            !(s.Lvl.HasStacks && Grid.IsCovered(s, id)) && RulesAllow(s, id, hooks);

        /// <summary>
        /// K-33 `P`: the block's correct spots on the active segment (access, wall and gaps ignored; K-16 + K-34 on empty cells).
        /// With `precheck` the spots whose result is a D3a dead end are left out (they are not highlighted).
        /// </summary>
        public static Anchor[] TrowelSpots(GameState s, int id, bool precheck = false)
        {
            if (!ValidId(s, id) || PieceZone(s, id) != Zone.Yard || !IsMaterialPiece(s, id)) return new Anchor[0];
            var spots = Deadlock.CorrectSpots(s, Shapes.ShapeByIndex(PieceShape(s, id)), PieceColor(s, id), -1, s.Lvl.Pieces[id].CellColors);
            if (!precheck) return spots;
            var o = new List<Anchor>();
            foreach (var at in spots)
            {
                var copy = CloneState(s);
                ApplyTrowelEffect(copy, id, at);
                if (Deadlock.PrecheckOk(copy)) o.Add(at);
            }
            return o.ToArray();
        }

        /// <summary>K-54 / UX §5.2: yard blocks the trowel may take now (non-empty `P`, pre-check excluded), id order.</summary>
        public static int[] TrowelPieces(GameState s, MoveHooks hooks)
        {
            var o = new List<int>();
            if (Won(s)) return o.ToArray();
            int P = s.Lvl.Layout.Counts.Pieces;
            for (int id = 0; id < P; id++)
                if (TrowelSelectable(s, id, hooks) && TrowelSpots(s, id).Length > 0) o.Add(id);
            return o.ToArray();
        }

        /// <summary>K-33 precondition with the pre-check (E-50: an empty `P` refuses and spends nothing).</summary>
        public static string? GoldTrowelRejection(GameState s, GoldTrowelMove move, MoveHooks hooks)
        {
            if (Won(s)) return BoosterRejects.LevelOver;
            if (Hdr(s, H.Trowels) <= 0) return BoosterRejects.NoTrowel;
            if (!TrowelSelectable(s, move.PieceId, hooks)) return BoosterRejects.NoTarget;
            var spots = TrowelSpots(s, move.PieceId);
            if (spots.Length == 0) return BoosterRejects.NoTarget;
            bool inP = false;
            foreach (var a in spots) if (a.Ix == move.X && a.Iy == move.Y) inP = true;
            if (!inP) return BoosterRejects.NotCorrect;
            var copy = CloneState(s);
            ApplyTrowelEffect(copy, move.PieceId, new Anchor(move.X, move.Y));
            return Deadlock.PrecheckOk(copy) ? null : BoosterRejects.Precheck;
        }

        /// <summary>K-33 effect: the block flies to `at` on the visible segment and locks; one trowel is spent. No check here.</summary>
        public static TrowelEffect ApplyTrowelEffect(GameState s, int id, Anchor at)
        {
            var from = At.Yard(PieceX(s, id), PieceY(s, id));
            var place = Placement.SitePlace(s, at);
            Placement.MovePiece(s, id, place);
            var revealed = Placement.LockPiece(s, id);
            SetHdr(s, H.Trowels, Math.Max(0, Hdr(s, H.Trowels) - 1));
            return new TrowelEffect(from, At.Site(at.Ix, at.Iy, place.Seg), revealed, Hdr(s, H.Trowels));
        }

        // --- K-54 ----------------------------------------------------------------------------------------------------------

        /// <summary>K-54 item 2 target predicates (the D3a pre-check is not part of them). `undoable` comes from the session.</summary>
        public static BoosterTargets BoosterTargets(GameState s, MoveHooks hooks, bool undoable)
        {
            return new BoosterTargets(
                HammerTargets(s, hooks).Length > 0,
                HasCraneTarget(s, hooks),
                undoable,
                HasPaintTarget(s),
                TrowelPieces(s, hooks).Length > 0);
        }
    }
}
