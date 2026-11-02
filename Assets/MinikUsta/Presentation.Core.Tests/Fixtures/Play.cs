#nullable enable
// Shared helpers of the presentation tests: real levels and golden solutions through the real core (no stand-ins), a
// recording tween target, and the plan of every committed action.
using System;
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core;
using MinikUsta.Core.Json;
using MinikUsta.Core.Level;
using MinikUsta.Presentation.Animation;
using MinikUsta.Presentation.Feel;
using MinikUsta.Presentation.Tweening;

namespace MinikUsta.Presentation.Tests.Fixtures
{
    /// <summary>A tween target that just stores its values (the Unity layer's Transform + renderer stand-in).</summary>
    public sealed class FakeTarget : ITweenTarget
    {
        public Vec2 Position { get; set; }
        public Vec2 Scale { get; set; } = Vec2.One;
        public float Rotation { get; set; }
        public float Alpha { get; set; } = 1f;
    }

    /// <summary>The golden solution of a level (`level_NNN.solver.json`): its canonical log and event log hash.</summary>
    public sealed class Golden
    {
        public readonly int Level;
        public readonly SessionAction[] Log;
        public readonly string EventLogHash;

        public Golden(int level, SessionAction[] log, string eventLogHash)
        {
            Level = level;
            Log = log;
            EventLogHash = eventLogHash;
        }

        /// <summary>The drag moves of the solution (log[0] is the plain start action).</summary>
        public DragMove[] Drags => Log.OfType<DragMove>().ToArray();
    }

    /// <summary>One committed action and its plan.</summary>
    public sealed class Planned
    {
        public readonly SessionAction Action;
        public readonly ActionResult Result;
        public readonly PlanContext Context;
        public readonly AnimationPlan Plan;

        public Planned(SessionAction action, ActionResult result, PlanContext context, AnimationPlan plan)
        {
            Action = action;
            Result = result;
            Context = context;
            Plan = plan;
        }
    }

    public static class Play
    {
        /// <summary>Faz 2R vertical slice: levels 1–10 (EN-2R-17).</summary>
        public static readonly int[] Levels = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        /// <summary>A shipped level (D-099: `schemaVersion` 2, fixed yard) loaded like the game does; `archive` = the Faz 2R level (`schemaVersion` 1, truck batches).</summary>
        public static CompiledLevel LevelFile(int n, bool archive = false) =>
            Compiler.LoadLevel(archive ? PresentationPaths.LoadArchiveLevelJson(n) : PresentationPaths.LoadLevelJson(n)).LevelOrThrow();

        /// <summary>A level from JSON text without the K-45 logic checks (test boards, like the web `compiledLevel` builder).</summary>
        public static CompiledLevel FromJson(string json) => Compiler.Compile(LevelJson.ParseOrThrow(JsonReader.Parse(json)));

        public static Golden SolverGolden(int n, bool archive = false)
        {
            string file = "level_" + n.ToString("000") + ".solver.json";
            var j = archive ? PresentationPaths.LoadArchiveGolden(file) : PresentationPaths.LoadCoreGolden(file);
            return new Golden(n, SessionActionJson.LogFromJson(j["log"]!), j["eventLogHash"]!.AsString);
        }

        /// <summary>
        /// The canonical solution with every over-the-wall release moved up into the crane area (web `craneMoves`): the
        /// blocks fall (#10, #11) instead of being released on their landing node. The same landings and verdicts.
        /// </summary>
        public static DragMove[] CraneMoves(int n, bool archive = false)
        {
            var lvl = LevelFile(n, archive);
            var session = GameSession.Start(lvl, null, 0, new SessionOptions(strict: true));
            var o = new List<DragMove>();
            foreach (var m in SolverGolden(n, archive).Drags)
            {
                var drag = Movement.BeginDrag(session.State, m.PieceId, session.Hooks.Drag)!;
                var high = new DragNode(m.To.Ix, lvl.Geo.H, Movement.Free);
                bool over = m.To.Mode == Movement.Free && m.To.Ix >= lvl.Geo.SiteX && drag.IsReachable(high);
                var move = over ? new DragMove(m.PieceId, high) : m;
                o.Add(move);
                session.Commit(m);
            }
            return o.ToArray();
        }

        /// <summary>Commits `moves` on a fresh session of `lvl`; returns each action's plan (web `playMoves`).</summary>
        public static List<Planned> PlayMoves(CompiledLevel lvl, IEnumerable<SessionAction> moves, bool reduced = false, FeelSettings? feel = null)
        {
            feel ??= new FeelSettings();
            var session = GameSession.Start(lvl, null, 0, new SessionOptions(strict: true));
            var o = new List<Planned>();
            foreach (var move in moves)
            {
                var ctx = PlanContext.Capture(session, reduced);
                var res = session.Apply(move);
                if (res.Status != MoveStatuses.Applied) throw new InvalidOperationException("move " + o.Count + " was " + res.Status + " (" + res.Reason + ")");
                o.Add(new Planned(move, res, ctx, AnimationPlanner.Plan(res, ctx, feel)));
            }
            return o;
        }

        public static List<Planned> PlayCrane(int level, bool reduced = false, bool archive = false) =>
            PlayMoves(LevelFile(level, archive), CraneMoves(level, archive), reduced);

        public static string[] Kinds(AnimationPlan plan) => plan.Steps.Select(s => s.Kind.ToString()).ToArray();

        public static string[] KindsWithoutInstant(AnimationPlan plan) =>
            plan.Steps.Where(s => s.Kind != StepKind.Instant).Select(s => s.Kind.ToString()).ToArray();

        public static float StartOf(AnimationPlan plan, StepKind kind) => plan.Find(kind)?.At ?? float.NaN;

        /// <summary>An event with its K-35 step (and sequence number) set, as the core's emitter does.</summary>
        public static T Ev<T>(T e, int step, int seq = 0) where T : GameEvent
        {
            e.Step = step;
            e.Seq = seq;
            return e;
        }
    }
}
