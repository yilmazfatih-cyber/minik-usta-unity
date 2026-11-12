#nullable enable
// Tutorial `done` / `startOn` event matching (paket C1; web src/scenes/level/tutorial/tutorialEvents.ts; GDD §14.1/3 closed
// vocabulary, K-53). Pure: reads one committed move's `GameEvent` list (K-35 steps) and, for the flag filters, the state after it.
//
// Three classes of events (GDD §14.1/3):
//  - drag signals `overWall`, `gapPass`, `holdOverBuild` come from the level screen DURING the drag, never from move events;
//  - move-end events are read from the move's events and count AT MOST ONCE per move (several matching core events of one move —
//    two crates, three falling blocks — are one count);
//  - `tap` and `boosterUsed`.
// The `piece` filter of `yardMove` / `placementCorrect` names a block by its highlight id (`piece:<i>`, `piece:k<p>_<i>`), resolved
// through `CompiledLevel.TutorialPieceIds`.
using System;
using System.Collections.Generic;
using MinikUsta.Core;
using MinikUsta.Core.Level;
using MinikUsta.Core.Obstacles;

namespace MinikUsta.Presentation.Tutorial
{
    public static class TutorialEvents
    {
        public const string OverWall = "overWall";
        public const string GapPass = "gapPass";
        public const string HoldOverBuild = "holdOverBuild";

        /// <summary>Drag-time signals (TECH §8.2: signals of the level screen, not core events).</summary>
        public static bool IsDragSignal(string ev) => ev == OverWall || ev == GapPass || ev == HoldOverBuild;

        private static bool HasFlag(GameState? s, int id, string flag) => s != null && State.HasFlag(s, id, FlagBit.Of(flag));

        private static bool AtMatches(int[]? at, int x, int y) => at == null || (at[0] == x && at[1] == y);

        /// <summary>Anchor (box origin) of a block from its cells (shapes are normalised: min x, min y).</summary>
        private static bool AnchorOf(At[] cells, out int x, out int y)
        {
            x = int.MaxValue;
            y = int.MaxValue;
            if (cells.Length == 0) return false;
            foreach (var c in cells)
            {
                x = Math.Min(x, c.X);
                y = Math.Min(y, c.Y);
            }
            return true;
        }

        /// <summary>
        /// `startOn: pieceFree` (K-53 item 7): the named block can be held now (K-30 `HoldableIgnoringMoves`, the counter ignored). A STATE
        /// condition: read at the level start and after each move, never from a move's events. False when the state is unknown or the
        /// highlight id names no block. Web `pieceFreeNow`.
        /// </summary>
        public static bool PieceFreeNow(TutCondition cond, GameState? s)
        {
            if (s == null || cond.Piece == null) return false;
            return s.Lvl.TutorialPieceIds.TryGetValue(cond.Piece, out int id) && Deadlock.HoldableIgnoringMoves(s, id, Registry.LevelHooks(s.Lvl).Drag);
        }

        /// <summary>GDD §14.1/3 `piece` filter: no filter, or `pieceId` is the block the highlight id names.</summary>
        private static bool PieceMatches(string? piece, int pieceId, GameState? s)
        {
            if (piece == null) return true;
            if (s == null) return false;
            return s.Lvl.TutorialPieceIds.TryGetValue(piece, out int id) && id == pieceId;
        }

        /// <summary>
        /// True when one committed move matches a move-end condition (filters included). `s` is the state after the move (piece flags for
        /// `flag`, the level's highlight ids for `piece`). Drag signals and `tap` never match here.
        /// </summary>
        public static bool MoveMatches(TutCondition cond, IReadOnlyList<GameEvent> events, GameState? s)
        {
            switch (cond.Event)
            {
                case "turnEnd":
                    foreach (var e in events) if (e is MovesChangedEvent m && m.Reason == "move") return true;
                    return false;
                case "placementCorrect":
                    {
                        PlacementCorrectEvent? hit = null;
                        foreach (var e in events) if (e is PlacementCorrectEvent p && p.Step == 3) { hit = p; break; }
                        if (hit == null) return false;
                        if (!PieceMatches(cond.Piece, hit.PieceId, s)) return false;
                        if (cond.At != null && (!AnchorOf(hit.Cells, out int ax, out int ay) || !AtMatches(cond.At, ax, ay))) return false;
                        if (cond.Hidden == true)
                        {
                            foreach (var e in events) if (e is CellsRevealedEvent c && c.Step == 3 && c.Cells.Length > 0) return true;
                            return false;
                        }
                        return true;
                    }
                case "yardMove":
                    {
                        PieceMovedEvent? hit = null;
                        foreach (var e in events) if (e is PieceMovedEvent p && p.Step == 1 && p.To.Zone == At.ZoneYard) { hit = p; break; }
                        if (hit == null) return false;
                        if (!PieceMatches(cond.Piece, hit.PieceId, s)) return false;
                        if (!AtMatches(cond.At, hit.To.X, hit.To.Y)) return false;
                        if (cond.Painted == true)
                        {
                            foreach (var e in events) if (e is PiecePaintedEvent pp && pp.Step == 1) return true;
                            return false;
                        }
                        return true;
                    }
                case "landed":
                    {
                        foreach (var e in events) if (e is GlassBrokeEvent) return false;
                        int pieceId = -1;
                        bool found = false;
                        foreach (var e in events)
                        {
                            if (e.Step != 2) continue;
                            if (e is PieceFellEvent f && f.Cause == FallCauses.Release) { pieceId = f.PieceId; found = true; break; }
                            if (e is BalloonRoseEvent b && b.To.Zone == At.ZoneSite) { pieceId = b.PieceId; found = true; break; }
                        }
                        if (!found) return false;
                        if (cond.Flag != null && !HasFlag(s, pieceId, cond.Flag)) return false;
                        if (cond.Wind == true)
                        {
                            foreach (var e in events) if (e is WindDriftEvent w && w.PieceId == pieceId) return true;
                            return false;
                        }
                        return true;
                    }
                case "steered":
                    foreach (var e in events) if (e is SteeredEvent st && st.Step == 2) return true;
                    return false;
                case "obstacleHit":
                    {
                        bool hammer = false;
                        foreach (var e in events) if (e is BoosterAppliedEvent b && b.Booster == "hammer") hammer = true;
                        foreach (var e in events)
                        {
                            int step = e.Step;
                            bool stepOk = step == 5 || step == 6 || (hammer && step == 1);
                            if (!stepOk) continue;
                            if (cond.Type == "crate") { if (e is CrateDamagedEvent || e is CrateBrokenEvent) return true; }
                            else if (cond.Type == "cement_bag") { if (e is BagTornEvent) return true; }
                            else if (e is ChainReleasedEvent) return true;
                        }
                        return false;
                    }
                case "itemCollected":
                    foreach (var e in events)
                    {
                        if (e.Step != 5 && e.Step != 6) continue;
                        if (cond.Type == "screw" ? e is ScrewCollectedEvent : e is KeyCollectedEvent) return true;
                    }
                    return false;
                case "yardFall":
                    foreach (var e in events) if (e is PieceFellEvent f && f.Cause == FallCauses.YardGravity && f.Step == 6) return true;
                    return false;
                case "segmentDone":
                    foreach (var e in events) if (e is SegmentCompletedEvent c && c.Step == 8) return true;
                    return false;
                case "deliveryDone":
                    foreach (var e in events)
                        if (e is PieceFellEvent f && f.Cause == FallCauses.Delivery && f.Step == 9 && (cond.Flag == null || HasFlag(s, f.PieceId, cond.Flag))) return true;
                    return false;
                case "carouselTurn":
                    foreach (var e in events) if (e is CarouselRotatedEvent c && (c.Step == 8 || c.Step == 10)) return true;
                    return false;
                case "boosterUsed":
                    foreach (var e in events) if (e is BoosterAppliedEvent) return true;
                    return false;
                default: // overWall, gapPass, holdOverBuild, tap
                    return false;
            }
        }
    }
}
