#nullable enable
// Port of tests/obstacles/obstacles.fixtures.ts (web repo): shared helpers of the obstacle plugin tests. Not a test file.
// (`i18nText` reads src/i18n/*.json of the web repo and has no counterpart here: the info card texts live in the web repo.)
using System;
using System.Collections.Generic;
using MinikUsta.Core.Level;
using MinikUsta.Core.Obstacles;

namespace MinikUsta.Core.Tests.Fixtures
{
    /// <summary>A rule context outside a move (direct hook calls); `Events` collects what the hook emits.</summary>
    public sealed class CapturedContext
    {
        public readonly RuleContext Ctx;
        public readonly List<GameEvent> Events = new List<GameEvent>();

        public CapturedContext(GameState s, bool rotatedAtStep8 = false)
        {
            Ctx = RuleContext.Of(s, e => Events.Add(e), new MoveScratch(false, rotatedAtStep8));
        }

        /// <summary>Event type names in order (TS `types(events)`).</summary>
        public string[] Types()
        {
            var o = new string[Events.Count];
            for (int i = 0; i < o.Length; i++) o[i] = Events[i].TypeName;
            return o;
        }
    }

    public static class ObstacleFixtures
    {
        /// <summary>TS `ctxOf(s, rotatedAtStep8)`.</summary>
        public static CapturedContext CtxOf(GameState s, bool rotatedAtStep8 = false) => new CapturedContext(s, rotatedAtStep8);

        /// <summary>TECH §7.3 rank of a rule id: W n → 100 + n, Y n → 200 + n, S n → 300 + n, G-H 401, G-L 402.</summary>
        public static int RankOf(string id)
        {
            int n = id == "G-H" ? 1 : id == "G-L" ? 2 : int.Parse(id.Substring(1));
            return RuleZones.OrderBase(RuleZones.Of(id)) + n;
        }

        /// <summary>A test rule that applies to every level; the named arguments add hooks or override fields (TS `fakeRule(id, extra)`).</summary>
        public static ObstacleRule FakeRule(string id, string? zone = null, int? order = null, string[]? infoKeys = null, Func<CompiledLevel, bool>? appliesTo = null,
            RuleOwnership? owns = null, Func<GameState, int, bool>? canPick = null, Func<GameState, int, int, bool>? canPassGap = null,
            Func<GameState, int, FallPlan, FallPlan>? modifyFall = null, Func<GameState, int, FallResult, LandingEffect>? onLanded = null,
            Func<GameState, int, BoardCell[], Verdict, string>? onPlacement = null, Action<RuleContext, int, int>? onPassGap = null,
            Action<RuleContext, int>? onYardRelease = null, Func<RuleContext, int, int?>? moveCost = null,
            Func<RuleContext, EntityRef, int, string>? onNeighborMoved = null, Action<RuleContext, int>? onCellUncovered = null,
            Action<RuleContext>? onMoveEnd = null, int? moveEndOrder = null, Action<RuleContext>? afterNeighbors = null,
            Func<GameState, EntityRef, bool>? canHammer = null, Func<RuleContext, EntityRef, string>? onHammer = null,
            Func<RuleContext, bool>? onTruckHelp = null)
        {
            return new ObstacleRule(
                id,
                zone ?? RuleZones.Of(id),
                order ?? RankOf(id),
                infoKeys ?? new[] { ObstacleRules.ObstacleInfoKey(id) },
                appliesTo ?? (_ => true),
                owns, canPick, canPassGap, modifyFall, onLanded, onPlacement, onPassGap, onYardRelease, moveCost, onNeighborMoved, onCellUncovered,
                onMoveEnd, moveEndOrder, afterNeighbors, canHammer, onHammer, onTruckHelp);
        }

        public static readonly Action<RuleContext> Noop = _ => { };
    }
}
