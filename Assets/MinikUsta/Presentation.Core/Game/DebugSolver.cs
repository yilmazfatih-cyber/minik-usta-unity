#nullable enable
// Development aid (principle 8 "solver çözümünü oynat"): the next move of the level's canonical solution
// (`Resources/solutions/level_NNN.solver.json`, the web solver's log copied by tools/sync-from-web.sh) while the attempt
// still follows it; off the solution path, a greedy CORRECT drag — a holdable block, a correct spot of its colour
// (`Deadlock.CorrectSpots`, K-16 + K-34) and a reachable release node whose core fall lands there with an ok verdict.
// The greedy fallback does not search (it can walk into a dead end the solver avoids). Never used by the game logic;
// the Unity layer binds it to a dev-build key.
using MinikUsta.Core;
using MinikUsta.Core.Json;

namespace MinikUsta.Presentation.Game
{
    public static class DebugSolver
    {
        /// <summary>The canonical log of a `level_NNN.solver.json` text (start action first); null when unreadable.</summary>
        public static SessionAction[]? SolutionLog(string? json)
        {
            if (json == null) return null;
            try
            {
                var log = JsonReader.Parse(json)["log"];
                return log == null ? null : SessionActionJson.LogFromJson(log);
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        /// <summary>The solution's next action when the attempt's log is a prefix of `solution`; else null.</summary>
        public static SessionAction? NextSolutionMove(GameSession session, SessionAction[]? solution)
        {
            if (solution == null || session.Outcome != BoosterOutcomes.Playing) return null;
            var log = session.Log;
            if (log.Count >= solution.Length) return null;
            for (int i = 1; i < log.Count; i++)
                if (!SessionActionJson.SameAs(log[i], solution[i])) return null;
            return solution[log.Count];
        }

        /// <summary>The solution's next move, else the greedy correct drag.</summary>
        public static SessionAction? Next(GameSession session, SessionAction[]? solution) =>
            NextSolutionMove(session, solution) ?? NextCorrectMove(session);

        public static DragMove? NextCorrectMove(GameSession session)
        {
            if (session.Outcome != BoosterOutcomes.Playing) return null;
            var s = session.State;
            var hooks = session.Hooks;
            var geo = s.Lvl.Geo;
            foreach (int id in Summary.HoldableIds(s, hooks))
            {
                var drag = Movement.BeginDrag(s, id, hooks.Drag);
                if (drag == null) continue;
                var shape = drag.Shape;
                foreach (var spot in Deadlock.CorrectSpots(s, shape, State.PieceColor(s, id), id))
                {
                    foreach (int iy in new[] { spot.Iy, geo.Rows - shape.H })
                    {
                        if (iy < 0) continue;
                        var node = new DragNode(spot.Ix, iy, Movement.Free);
                        if (!drag.IsReachable(node)) continue;
                        var fall = Gravity.ComputeFall(s, id, node, new FallOptions(null, hooks.Fall));
                        if (fall.Landing.Ix == spot.Ix && fall.Landing.Iy == spot.Iy && fall.Verdict.Ok) return new DragMove(id, node);
                    }
                }
            }
            return null;
        }
    }
}
