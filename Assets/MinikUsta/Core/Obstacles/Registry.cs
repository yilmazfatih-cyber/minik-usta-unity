#nullable enable
// Port of src/core/obstacles/registry.ts (web repo, source of truth).
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using MinikUsta.Core.Level;
using static MinikUsta.Core.State;

namespace MinikUsta.Core.Obstacles
{
    /// <summary>The rules of one level, in `Order`, with the ownership tables (TECH §7.1).</summary>
    public sealed class RuleSet
    {
        public readonly ObstacleRule[] Rules;
        public readonly IReadOnlyDictionary<string, ObstacleRule> RuleByObstacleType;
        public readonly IReadOnlyDictionary<string, ObstacleRule> RuleByPieceFlag;
        public readonly IReadOnlyDictionary<string, ObstacleRule> RuleByGapType;

        public RuleSet(ObstacleRule[] rules, IReadOnlyDictionary<string, ObstacleRule> byObstacleType, IReadOnlyDictionary<string, ObstacleRule> byPieceFlag,
            IReadOnlyDictionary<string, ObstacleRule> byGapType)
        {
            Rules = rules;
            RuleByObstacleType = byObstacleType;
            RuleByPieceFlag = byPieceFlag;
            RuleByGapType = byGapType;
        }
    }

    public sealed class RuleOptions
    {
        /// <summary>Debug panel only (development, TECH §7.1 `disabledRules`): rules switched off.</summary>
        public readonly ISet<string>? Disabled;
        /// <summary>A rule table instead of `AllRules` (tests); it is validated first.</summary>
        public readonly ObstacleRule[]? Rules;

        public RuleOptions(ISet<string>? disabled = null, ObstacleRule[]? rules = null)
        {
            Disabled = disabled;
            Rules = rules;
        }
    }

    /// <summary>
    /// Obstacle registry (docs/TECH_DESIGN.md §7.1, §7.3).
    ///
    /// `AllRules` lists every rule plugin, one file each. `LevelHooks(lvl)` keeps the rules the level uses (`AppliesTo`),
    /// sorts them by `Order` and composes their hooks into the pipeline's `MoveHooks`, cached per CompiledLevel. `applyMove`
    /// uses it unless the caller passes its own hooks; the scene passes `LevelHooks(lvl).Drag` to `BeginDrag` and
    /// `LevelHooks(lvl).Fall` to `ComputeFall` (shadow), so preview and move run the same rules.
    ///
    /// Composition (active rules in `Order`; core never branches on a rule id):
    /// - `CanPick`: every rule must allow · `CanPassGap`, `OnPassGap`: the owner of the gap's type (`RuleByGapType`); a gap
    ///   whose owner has no `CanPassGap` reads its `open` field (the movement default).
    /// - `ModifyFall`: chained · `OnLanded`: first non-`none` effect · `OnPlacement`: first non-`default` override ·
    ///   `OnYardRelease`: every rule · `MoveCost`: the largest defined value (none → base 1 in the pipeline).
    /// - `OnNeighborMoved`: an obstacle goes to the owner of its type, a block to the owners of its flags (strongest effect)
    ///   · `OnCellUncovered`: the owner of the hidden item's type.
    /// - `Timers`: `OnMoveEnd` by rule id plus the site strategy's S5 / S6 ticks; the pipeline calls them in `lvl.Step10`
    ///   order (GDD K-35 step 10), never in `Order`. The K-40 Open Shutter expiry joins in Phase 3.
    /// A hook that no active rule provides stays null: a level with only W1 and S1 (core models) gets empty hooks.
    /// `ValidateRules` rejects a malformed table when this class loads (e.g. `OnMoveEnd` without `MoveEndOrder`, §7.3).
    /// </summary>
    public static class Registry
    {
        private static readonly Dictionary<string, int> TimerOrder = BuildTimerOrder();
        private static readonly JsRegex InfoKeyFormat = new JsRegex("^obs\\.[a-z0-9]+\\.desc$");

        /// <summary>
        /// Every rule plugin (TECH §7.1). Faz 2R: W1, Y3, S1 (S2 Plan Boşluğu left the MVP, R2-01: its plugin is gone, the `.`
        /// cell rules stay in the core for a later return); a new obstacle adds one line here.
        /// </summary>
        public static readonly ObstacleRule[] AllRules =
        {
            W1_StaticGap.Rule, Y3_Chain.Rule, Y9_TwoTone.Rule, Y10_Pallet.Rule, Y11_Wrap.Rule, Y12_Bonus.Rule, Y13_Slippery.Rule, S1_SlidingSite.Rule,
        };

        static Registry()
        {
            AssertValidRules(AllRules);
        }

        private static Dictionary<string, int> BuildTimerOrder()
        {
            var o = new Dictionary<string, int>();
            foreach (var t in Compiler.Step10Timers) o[t.Id] = t.Order;
            return o;
        }

        private static bool IsTimerId(string id) => TimerOrder.ContainsKey(id);

        /// <summary>
        /// Step-10 ticks of the site strategy (TECH §7.1 `SiteStrategy.onCarouselTick` / `onElevatorTick`, §7.3): S5 does not
        /// tick in a move whose step 8 turned the platform (K-23, `MoveScratch.RotatedAtStep8`); S6 moves the frame (K-24).
        /// These ids are not available to a rule's `OnMoveEnd`.
        /// </summary>
        private static Action<RuleContext>? SiteTick(string timerId, SiteStrategy site)
        {
            switch (timerId)
            {
                case "S5":
                    return ctx =>
                    {
                        if (ctx.Scratch.RotatedAtStep8) return;
                        int? front = site.CarouselTick(ctx.S);
                        if (front.HasValue) ctx.Emit(new CarouselRotatedEvent(front.Value));
                    };
                case "S6":
                    return ctx => ctx.Emit(new ElevatorMovedEvent(site.ElevatorTick(ctx.S)));
                default:
                    return null;
            }
        }

        private static bool HasSiteTick(string id) => id == "S5" || id == "S6";

        // --- validation --------------------------------------------------------------------------------------------------------

        /// <summary>Problems of a rule table (empty = valid). TECH §7.1 / §7.3 contracts; see the class comment.</summary>
        public static string[] ValidateRules(IReadOnlyList<ObstacleRule> rules)
        {
            var errors = new List<string>();
            var ids = new HashSet<string>();
            var orders = new Dictionary<int, string>();
            var owners = new Dictionary<string, string>();
            foreach (var r in rules)
            {
                string where = "rule " + r.Id;
                if (!RuleIds.IsRuleId(r.Id))
                {
                    errors.Add(where + ": unknown rule id");
                    continue;
                }
                if (!ids.Add(r.Id)) errors.Add(where + ": duplicate id");
                string zone = RuleZones.Of(r.Id);
                if (r.Zone != zone) errors.Add(where + ": zone " + r.Zone + " ≠ " + zone);
                int bse = RuleZones.OrderBase(zone);
                if (r.Order <= bse || r.Order >= bse + 100)
                    errors.Add(where + ": order " + r.Order + " is outside " + (bse + 1) + "…" + (bse + 99) + " (TECH §7.3)");
                if (orders.TryGetValue(r.Order, out var twin)) errors.Add(where + ": order " + r.Order + " is also used by " + twin);
                else orders[r.Order] = r.Id;

                string infoKey = ObstacleRules.ObstacleInfoKey(r.Id);
                string prefix = infoKey.Substring(0, infoKey.Length - ".desc".Length);
                if (r.InfoKeys.Length == 0) errors.Add(where + ": no info card key (OBSTACLES R-08)");
                foreach (var k in r.InfoKeys)
                    if (!InfoKeyFormat.IsMatch(k) || !k.StartsWith(prefix, StringComparison.Ordinal))
                        errors.Add(where + ": info card key " + k + " is not " + prefix + "….desc");

                if (r.OnMoveEnd != null)
                {
                    bool hasTimer = TimerOrder.TryGetValue(r.Id, out int timerOrder);
                    if (!r.MoveEndOrder.HasValue) errors.Add(where + ": onMoveEnd needs moveEndOrder (TECH §7.3)");
                    else if (!hasTimer || (IsTimerId(r.Id) && HasSiteTick(r.Id)))
                        errors.Add(where + ": " + r.Id + " has no rule timer in K-35 step 10 (STEP10_TIMERS)");
                    else if (r.MoveEndOrder.Value != timerOrder)
                        errors.Add(where + ": moveEndOrder " + r.MoveEndOrder.Value + " ≠ K-35 step 10 position " + timerOrder);
                }
                else if (r.MoveEndOrder.HasValue)
                {
                    errors.Add(where + ": moveEndOrder without onMoveEnd");
                }

                var own = r.Owns;
                var claims = new[]
                {
                    new KeyValuePair<string, string?>("obstacle", own?.Obstacle),
                    new KeyValuePair<string, string?>("pieceFlag", own?.PieceFlag),
                    new KeyValuePair<string, string?>("gapType", own?.GapType),
                };
                foreach (var claim in claims)
                {
                    if (claim.Value == null) continue;
                    string key = claim.Key + ":" + claim.Value;
                    if (owners.TryGetValue(key, out var prev)) errors.Add(where + ": " + claim.Key + " " + claim.Value + " is already owned by " + prev);
                    else owners[key] = r.Id;
                }
                if ((r.CanPassGap != null || r.OnPassGap != null) && own?.GapType == null)
                    errors.Add(where + ": canPassGap / onPassGap need owns.gapType");
                if (r.OnNeighborMoved != null && own?.Obstacle == null && own?.PieceFlag == null)
                    errors.Add(where + ": onNeighborMoved needs owns.obstacle or owns.pieceFlag");
                if (r.OnCellUncovered != null && own?.Obstacle == null)
                    errors.Add(where + ": onCellUncovered needs owns.obstacle");
                if ((r.CanHammer != null) != (r.OnHammer != null))
                    errors.Add(where + ": canHammer and onHammer come together (K-36)");
                if (r.CanHammer != null && own?.Obstacle == null && own?.PieceFlag == null)
                    errors.Add(where + ": canHammer / onHammer need owns.obstacle or owns.pieceFlag");
            }
            return errors.ToArray();
        }

        /// <summary>Throws when `rules` is not a valid rule table.</summary>
        public static void AssertValidRules(IReadOnlyList<ObstacleRule> rules)
        {
            var errors = ValidateRules(rules);
            if (errors.Length > 0) throw new InvalidOperationException("obstacle registry:\n  " + string.Join("\n  ", errors));
        }

        // --- active rule set ---------------------------------------------------------------------------------------------------

        /// <summary>Active rules of `lvl` (`AppliesTo`, not disabled), sorted by `Order`, and their ownership tables. TS `ruleSet`.</summary>
        public static RuleSet GetRuleSet(CompiledLevel lvl, RuleOptions? opts = null)
        {
            opts ??= new RuleOptions();
            if (opts.Rules != null) AssertValidRules(opts.Rules);
            var table = opts.Rules ?? AllRules;
            var rules = new List<ObstacleRule>();
            foreach (var r in table)
            {
                if (opts.Disabled != null && opts.Disabled.Contains(r.Id)) continue;
                if (!r.AppliesTo(lvl)) continue;
                // insertion by order (stable; orders are unique after validation)
                int i = rules.Count;
                while (i > 0 && rules[i - 1].Order > r.Order) i--;
                rules.Insert(i, r);
            }
            var byObstacleType = new Dictionary<string, ObstacleRule>();
            var byPieceFlag = new Dictionary<string, ObstacleRule>();
            var byGapType = new Dictionary<string, ObstacleRule>();
            foreach (var r in rules)
            {
                if (r.Owns?.Obstacle != null) byObstacleType[r.Owns.Obstacle] = r;
                if (r.Owns?.PieceFlag != null) byPieceFlag[r.Owns.PieceFlag] = r;
                if (r.Owns?.GapType != null) byGapType[r.Owns.GapType] = r;
            }
            return new RuleSet(rules.ToArray(), byObstacleType, byPieceFlag, byGapType);
        }

        /// <summary>Ids of the active rules of `lvl`, in `Order`.</summary>
        public static string[] ActiveRuleIds(CompiledLevel lvl, RuleOptions? opts = null)
        {
            var rules = GetRuleSet(lvl, opts).Rules;
            var o = new string[rules.Length];
            for (int i = 0; i < rules.Length; i++) o[i] = rules[i].Id;
            return o;
        }

        /// <summary>
        /// Info card keys of the level's obstacles, in rule order (OBSTACLES R-08; the scene shows a card on the first
        /// encounter and on tap). Only keys of mechanics the level really uses (S7 covers S7-R and S7-M).
        /// </summary>
        public static string[] InfoKeysFor(CompiledLevel lvl, RuleOptions? opts = null)
        {
            var used = new HashSet<string>();
            foreach (var m in lvl.Mechanics) used.Add(ObstacleRules.ObstacleInfoKey(m));
            var o = new List<string>();
            foreach (var r in GetRuleSet(lvl, opts).Rules)
                foreach (var k in r.InfoKeys) if (used.Contains(k)) o.Add(k);
            return o.ToArray();
        }

        // --- hook composition --------------------------------------------------------------------------------------------------

        private static readonly ConditionalWeakTable<CompiledLevel, MoveHooks> Cache = new ConditionalWeakTable<CompiledLevel, MoveHooks>();

        /// <summary>
        /// The pipeline hooks of `lvl` (TECH §7.1): composed once per CompiledLevel and cached; with `opts` (debug panel,
        /// tests) they are composed fresh.
        /// </summary>
        public static MoveHooks LevelHooks(CompiledLevel lvl, RuleOptions? opts = null)
        {
            if (opts != null && (opts.Disabled != null || opts.Rules != null)) return ComposeHooks(lvl, GetRuleSet(lvl, opts));
            if (Cache.TryGetValue(lvl, out var hooks)) return hooks;
            hooks = ComposeHooks(lvl, GetRuleSet(lvl));
            Cache.Add(lvl, hooks);
            return hooks;
        }

        /// <summary>`MoveHooks` of a rule set; every field is set only when an active rule provides that hook.</summary>
        public static MoveHooks ComposeHooks(CompiledLevel lvl, RuleSet set)
        {
            return new MoveHooks(
                ComposeDrag(set),
                ComposeFall(set.Rules),
                ComposePlacement(set.Rules),
                ComposePassGap(set),
                ComposeYardRelease(set.Rules),
                ComposeMoveCost(set.Rules),
                ComposeNeighborMoved(set),
                ComposeCellUncovered(set),
                ComposeAfterNeighbors(set.Rules),
                ComposeHammer(set),
                ComposeTruckHelp(set.Rules),
                ComposeTimers(lvl, set.Rules),
                null,
                ComposePlacementBonus(set.Rules),
                ComposeCorrectPlacement(set.Rules));
        }

        private static Func<RuleContext, int, int>? ComposePlacementBonus(ObstacleRule[] rules)
        {
            var fns = new List<Func<RuleContext, int, int>>();
            foreach (var r in rules) if (r.PlacementBonus != null) fns.Add(r.PlacementBonus);
            if (fns.Count == 0) return null;
            return (ctx, id) =>
            {
                int n = 0;
                foreach (var f in fns) n += f(ctx, id);
                return n;
            };
        }

        private static Action<RuleContext, int>? ComposeCorrectPlacement(ObstacleRule[] rules)
        {
            var fns = new List<Action<RuleContext, int>>();
            foreach (var r in rules) if (r.OnCorrectPlacement != null) fns.Add(r.OnCorrectPlacement);
            if (fns.Count == 0) return null;
            return (ctx, id) =>
            {
                foreach (var f in fns) f(ctx, id);
            };
        }

        private static ObstacleRule? GapOwner(RuleSet set, GameState s, int gap)
        {
            if (gap < 0 || gap >= s.Lvl.Gaps.Length) return null;
            return set.RuleByGapType.TryGetValue(s.Lvl.Gaps[gap].Type, out var r) ? r : null;
        }

        private static DragRules? ComposeDrag(RuleSet set)
        {
            var pickers = new List<Func<GameState, int, bool>>();
            bool gated = false;
            foreach (var r in set.Rules)
            {
                if (r.CanPick != null) pickers.Add(r.CanPick);
                if (r.CanPassGap != null) gated = true;
            }
            if (pickers.Count == 0 && !gated) return null;
            Func<GameState, int, bool> canPick = (s, id) =>
            {
                foreach (var f in pickers) if (!f(s, id)) return false;
                return true;
            };
            Func<GameState, int, int, bool> canPassGap = (s, gap, id) =>
            {
                var decide = GapOwner(set, s, gap)?.CanPassGap;
                return decide != null ? decide(s, gap, id) : GapField(s, gap, GF.Open) != 0;
            };
            return new DragRules(pickers.Count > 0 ? canPick : null, gated ? canPassGap : null);
        }

        private static FallRules? ComposeFall(ObstacleRule[] rules)
        {
            var mods = new List<Func<GameState, int, FallPlan, FallPlan>>();
            var lands = new List<Func<GameState, int, FallResult, LandingEffect>>();
            foreach (var r in rules)
            {
                if (r.ModifyFall != null) mods.Add(r.ModifyFall);
                if (r.OnLanded != null) lands.Add(r.OnLanded);
            }
            if (mods.Count == 0 && lands.Count == 0) return null;
            Func<GameState, int, FallPlan, FallPlan> modifyFall = (s, id, plan) =>
            {
                var p = plan;
                foreach (var f in mods) p = f(s, id, p);
                return p;
            };
            Func<GameState, int, FallResult, LandingEffect> onLanded = (s, id, fall) =>
            {
                foreach (var f in lands)
                {
                    var e = f(s, id, fall);
                    if (e.Kind != LandingEffect.KindNone) return e;
                }
                return LandingEffect.None;
            };
            return new FallRules(mods.Count > 0 ? modifyFall : null, lands.Count > 0 ? onLanded : null);
        }

        private static PlacementRules? ComposePlacement(ObstacleRule[] rules)
        {
            var fns = new List<Func<GameState, int, BoardCell[], Verdict, string>>();
            foreach (var r in rules) if (r.OnPlacement != null) fns.Add(r.OnPlacement);
            if (fns.Count == 0) return null;
            return new PlacementRules((s, id, cells, verdict) =>
            {
                foreach (var f in fns)
                {
                    string o = f(s, id, cells, verdict);
                    if (o != PlacementOverrides.Default) return o;
                }
                return PlacementOverrides.Default;
            });
        }

        private static Action<RuleContext, int, int>? ComposePassGap(RuleSet set)
        {
            bool any = false;
            foreach (var r in set.Rules) if (r.OnPassGap != null) any = true;
            if (!any) return null;
            return (ctx, gap, id) => GapOwner(set, ctx.S, gap)?.OnPassGap?.Invoke(ctx, gap, id);
        }

        private static Action<RuleContext, int>? ComposeYardRelease(ObstacleRule[] rules)
        {
            var fns = new List<Action<RuleContext, int>>();
            foreach (var r in rules) if (r.OnYardRelease != null) fns.Add(r.OnYardRelease);
            if (fns.Count == 0) return null;
            return (ctx, id) =>
            {
                foreach (var f in fns) f(ctx, id);
            };
        }

        private static Func<RuleContext, int, int?>? ComposeMoveCost(ObstacleRule[] rules)
        {
            var fns = new List<Func<RuleContext, int, int?>>();
            foreach (var r in rules) if (r.MoveCost != null) fns.Add(r.MoveCost);
            if (fns.Count == 0) return null;
            return (ctx, id) =>
            {
                int? best = null;
                foreach (var f in fns)
                {
                    int? c = f(ctx, id);
                    if (c.HasValue && (!best.HasValue || c.Value > best.Value)) best = c;
                }
                return best;
            };
        }

        private sealed class FlagHook
        {
            public readonly int Bit;
            public readonly Func<RuleContext, EntityRef, int, string> F;

            public FlagHook(int bit, Func<RuleContext, EntityRef, int, string> f)
            {
                Bit = bit;
                F = f;
            }
        }

        private static Func<RuleContext, EntityRef, int, string>? ComposeNeighborMoved(RuleSet set)
        {
            bool any = false;
            foreach (var r in set.Rules) if (r.OnNeighborMoved != null) any = true;
            if (!any) return null;
            var byFlag = new List<FlagHook>();
            foreach (var r in set.Rules)
            {
                string? flag = r.Owns?.PieceFlag;
                if (flag != null && r.OnNeighborMoved != null) byFlag.Add(new FlagHook(FlagBit.Of(flag), r.OnNeighborMoved));
            }
            return (ctx, entity, moved) =>
            {
                if (entity.IsObstacle)
                {
                    Func<RuleContext, EntityRef, int, string>? f = null;
                    if (entity.Index >= 0 && entity.Index < ctx.Lvl.Obstacles.Length
                        && set.RuleByObstacleType.TryGetValue(ctx.Lvl.Obstacles[entity.Index].Type, out var owner))
                        f = owner.OnNeighborMoved;
                    return f != null ? f(ctx, entity, moved) : NeighborEffects.None;
                }
                int flags = PieceFlagBits(ctx.S, entity.Id);
                string result = NeighborEffects.None;
                foreach (var h in byFlag)
                {
                    if ((flags & h.Bit) == 0) continue;
                    string e = h.F(ctx, entity, moved);
                    if (NeighborEffects.Rank(e) > NeighborEffects.Rank(result)) result = e;
                }
                return result;
            };
        }

        private static Action<RuleContext, int>? ComposeCellUncovered(RuleSet set)
        {
            bool any = false;
            foreach (var r in set.Rules) if (r.OnCellUncovered != null) any = true;
            if (!any) return null;
            return (ctx, obstacle) =>
            {
                if (obstacle < 0 || obstacle >= ctx.Lvl.Obstacles.Length) return;
                if (set.RuleByObstacleType.TryGetValue(ctx.Lvl.Obstacles[obstacle].Type, out var owner)) owner.OnCellUncovered?.Invoke(ctx, obstacle);
            };
        }

        private static Action<RuleContext>? ComposeAfterNeighbors(ObstacleRule[] rules)
        {
            var fns = new List<Action<RuleContext>>();
            foreach (var r in rules) if (r.AfterNeighbors != null) fns.Add(r.AfterNeighbors);
            if (fns.Count == 0) return null;
            return ctx =>
            {
                foreach (var f in fns) f(ctx);
            };
        }

        private static Func<RuleContext, bool>? ComposeTruckHelp(ObstacleRule[] rules)
        {
            var fns = new List<Func<RuleContext, bool>>();
            foreach (var r in rules) if (r.OnTruckHelp != null) fns.Add(r.OnTruckHelp);
            if (fns.Count == 0) return null;
            return ctx =>
            {
                bool changed = false;
                foreach (var f in fns) if (f(ctx)) changed = true;
                return changed;
            };
        }

        /// <summary>K-36: an obstacle goes to the owner of its type, a block to the first owner of its flags that accepts it.</summary>
        private static HammerRules? ComposeHammer(RuleSet set)
        {
            bool any = false;
            foreach (var r in set.Rules) if (r.CanHammer != null) any = true;
            if (!any) return null;
            ObstacleRule? Owner(GameState s, EntityRef entity)
            {
                if (entity.IsObstacle)
                {
                    if (entity.Index < 0 || entity.Index >= s.Lvl.Obstacles.Length) return null;
                    if (!set.RuleByObstacleType.TryGetValue(s.Lvl.Obstacles[entity.Index].Type, out var r)) return null;
                    return r.CanHammer != null && r.CanHammer(s, entity) ? r : null;
                }
                int flags = PieceFlagBits(s, entity.Id);
                foreach (var r in set.Rules)
                {
                    string? flag = r.Owns?.PieceFlag;
                    if (flag != null && (flags & FlagBit.Of(flag)) != 0 && r.CanHammer != null && r.CanHammer(s, entity)) return r;
                }
                return null;
            }
            return new HammerRules(
                (s, entity) => Owner(s, entity) != null,
                (ctx, entity) =>
                {
                    var r = Owner(ctx.S, entity);
                    if (r?.OnHammer == null) throw new ArgumentOutOfRangeException(nameof(entity), "hammer: no rule accepts this target");
                    return r.OnHammer(ctx, entity);
                },
                (s, entity) =>
                {
                    var r = Owner(s, entity);
                    return r != null ? (r.HammerKind ?? HammerTargetKinds.Chain) : null;
                });
        }

        private static IReadOnlyDictionary<string, Action<RuleContext>>? ComposeTimers(CompiledLevel lvl, ObstacleRule[] rules)
        {
            var o = new Dictionary<string, Action<RuleContext>>();
            foreach (var r in rules) if (r.OnMoveEnd != null && IsTimerId(r.Id)) o[r.Id] = r.OnMoveEnd;
            var site = Site.SiteStrategy(lvl);
            foreach (var t in lvl.Step10)
            {
                var tick = SiteTick(t.Id, site);
                if (tick != null) o[t.Id] = tick;
            }
            return o.Count > 0 ? o : null;
        }
    }
}
