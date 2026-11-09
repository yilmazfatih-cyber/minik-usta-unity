#nullable enable
// Port of src/core/goals.ts (web repo, source of truth).
using System;
using System.Collections.Generic;
using MinikUsta.Core.Level;
using static MinikUsta.Core.State;

namespace MinikUsta.Core
{
    /// <summary>Countable targets of `clear` / `collect` goals (GDD K-41) and the `build` goal. TS `GoalKind`.</summary>
    public static class GoalKinds
    {
        public const string Build = "build";
        public const string Crate = "crate";
        public const string Chain = "chain";
        public const string Debris = "debris";
        public const string Screw = "screw";
    }

    /// <summary>Goal panel row (UX goal panel: `value/target`, ✓ when done).</summary>
    public sealed class GoalView : IEquatable<GoalView>
    {
        public readonly int Index;
        public readonly string Kind;
        public readonly int Value;
        public readonly int Target;
        public readonly bool Done;

        public GoalView(int index, string kind, int value, int target, bool done)
        {
            Index = index;
            Kind = kind;
            Value = value;
            Target = target;
            Done = done;
        }

        public bool Equals(GoalView? o) => o != null && Index == o.Index && Kind == o.Kind && Value == o.Value && Target == o.Target && Done == o.Done;
        public override bool Equals(object? obj) => obj is GoalView g && Equals(g);
        public override int GetHashCode() => Index * 31 + Value;
        public override string ToString() => Index + ":" + Kind + " " + Value + "/" + Target + (Done ? " done" : "");
    }

    /// <summary>
    /// Goal counters and the full-cover accounting (docs/GDD.md K-41, K-28, K-47, K-48; TECH_DESIGN §2.4 `goals` section,
    /// §6.2 steps 7–8, §7.2 S4, §2R.2).
    ///
    /// One counter per `lvl.Goals[i]` (the state's goal section, same order):
    /// - `build`: completed segments, target S (UX goal panel "2/4"). It changes when a segment completes (K-35 step 8), so
    ///   its `goalProgress` event carries step 8; the other goals are reported in step 7.
    /// - `clear` / `collect`: counted where the effect happens — crate destroyed, chain removed (Y3 rule), screw collected
    ///   (obstacle rules) and debris leaving its start spot (core, `CountDebrisLeftSite`, K-41 Faz 2R: each debris at most
    ///   once, the first time it lands in the yard / queue or is placed correctly elsewhere; then it is a plain block).
    /// A counter never passes its target: the goal stays "done" and the surplus is not counted (K-41).
    ///
    /// K-47 / K-48: material = every piece but Ağır Yük; remaining supply / demand per colour, `BlocksLeft` (UX §5.9) and
    /// the win test `LevelGoalsMet` (segments + extra goals + no material in the yard, queue or undelivered batches).
    /// </summary>
    public static class Goals
    {
        /// <summary>What a goal counts.</summary>
        public static string GoalKind(GoalData goal)
        {
            if (goal.Type == "build") return GoalKinds.Build;
            if (goal.Type == "clear") return goal.Target ?? "";
            return goal.Item ?? "";
        }

        /// <summary>Target of goal `i`: S for `build`, `count` otherwise.</summary>
        public static int GoalTarget(CompiledLevel lvl, int i)
        {
            if (i < 0 || i >= lvl.Goals.Length) return 0;
            var goal = lvl.Goals[i];
            return goal.Type == "build" ? lvl.Segments.Length : goal.Count ?? 0;
        }

        /// <summary>Goal `i` reached its target.</summary>
        public static bool GoalDone(GameState s, int i) => GoalValue(s, i) >= GoalTarget(s.Lvl, i);

        /// <summary>
        /// Adds `n` to every goal counting `kind` (capped at its target, K-41). Returns the indices whose value changed.
        /// Called by obstacle rules (crate, chain, screw) and by `CountDebrisLeftSite`.
        /// </summary>
        public static int[] AddGoalCount(GameState s, string kind, int n = 1)
        {
            if (n < 0) throw new ArgumentOutOfRangeException(nameof(n), "addGoalCount: bad amount " + n);
            var changed = new List<int>();
            for (int i = 0; i < s.Lvl.Goals.Length; i++)
            {
                if (GoalKind(s.Lvl.Goals[i]) != kind) continue;
                int before = GoalValue(s, i);
                int after = Math.Min(GoalTarget(s.Lvl, i), before + n);
                if (after != before)
                {
                    SetGoalValue(s, i, after);
                    changed.Add(i);
                }
            }
            return changed.ToArray();
        }

        /// <summary>Writes the completed segment count into every `build` goal (K-35 step 8). Returns the indices that changed.</summary>
        public static int[] SetBuildProgress(GameState s, int completed)
        {
            var changed = new List<int>();
            for (int i = 0; i < s.Lvl.Goals.Length; i++)
            {
                if (s.Lvl.Goals[i].Type != "build") continue;
                int value = Math.Min(GoalTarget(s.Lvl, i), completed);
                if (GoalValue(s, i) != value)
                {
                    SetGoalValue(s, i, value);
                    changed.Add(i);
                }
            }
            return changed.ToArray();
        }

        /// <summary>
        /// K-41 `clear: debris`, OBSTACLES S4 (Faz 2R): a debris block counts once, the first time it leaves its start spot on
        /// the site — a yard placement (drag, FREE or rail, K-12 exception; crane), a CORRECT placement elsewhere on the site
        /// (drag, crane, Golden Trowel) or the hammer (K-36: down to the yard or the truck queue). At that moment its `debris`
        /// flag goes and it is an ordinary material block. A wrong placement bounces it back to its start (K-17 step 1): no
        /// count, the flag stays. `startZone` = the piece's zone at action start. Returns true when it counted now.
        /// </summary>
        public static bool CountDebrisLeftSite(GameState s, int pieceId, int startZone)
        {
            if (startZone != Zone.Site) return false;
            int flags = PieceFlagBits(s, pieceId);
            if ((flags & FlagBit.Debris) == 0) return false;
            if (PieceZone(s, pieceId) == Zone.Site && (flags & FlagBit.Locked) == 0) return false;
            SetFlag(s, pieceId, FlagBit.Debris, false);
            AddGoalCount(s, GoalKinds.Debris, 1);
            return true;
        }

        /// <summary>Every goal other than `build` reached its target (K-28, E-27).</summary>
        public static bool ExtraGoalsMet(GameState s)
        {
            for (int i = 0; i < s.Lvl.Goals.Length; i++)
                if (s.Lvl.Goals[i].Type != "build" && !GoalDone(s, i)) return false;
            return true;
        }

        /// <summary>Every segment is complete: `deliveryCursor` counts completed segments (K-22, K-23, K-28).</summary>
        public static bool AllSegmentsComplete(GameState s) => Hdr(s, H.DeliveryCursor) >= s.Lvl.Segments.Length;

        // --- K-47 / K-48: full cover ---------------------------------------------------------------------------------------

        /// <summary>K-44 / K-47: a material block (every piece but Ağır Yük; debris included).</summary>
        public static bool IsMaterial(GameState s, int id) => id >= 0 && id < s.Lvl.Pieces.Length && s.Lvl.Pieces[id].Cls == PieceClasses.Material;

        /// <summary>
        /// K-47 "kalan arz": a material block that is not locked — in the yard, the truck queue, an undelivered batch, or
        /// unlocked on the site (debris, stuck mortar).
        /// </summary>
        public static bool IsRemainingSupply(GameState s, int id)
        {
            if (!IsMaterial(s, id)) return false;
            int zone = PieceZone(s, id);
            if (zone == Zone.Yard || zone == Zone.Queue || zone == Zone.Pending) return true;
            return zone == Zone.Site && (PieceFlagBits(s, id) & FlagBit.Locked) == 0;
        }

        /// <summary>K-47: remaining supply cells per colour index (length 8).</summary>
        public static int[] RemainingSupplyByColor(GameState s)
        {
            var o = new int[ColorCodes.All.Length];
            int P = s.Lvl.Layout.Counts.Pieces;
            for (int id = 0; id < P; id++)
            {
                if (!IsRemainingSupply(s, id)) continue;
                int n = Shapes.ShapeByIndex(PieceShape(s, id)).CellCount;
                // v3 Y9 (K-57 item 2): a two-tone block supplies each cell in its own colour
                for (int i = 0; i < n; i++)
                {
                    int c = PieceCellColor(s, id, i);
                    if (c >= 0 && c < o.Length) o[c] += 1;
                }
            }
            return o;
        }

        /// <summary>K-47: remaining demand cells per colour index (plan cells of every segment not correctly filled; `?` resolved).</summary>
        public static int[] RemainingDemandByColor(GameState s)
        {
            var o = new int[ColorCodes.All.Length];
            int ws = s.Lvl.Geo.Ws;
            int hs = s.Lvl.Geo.Hs;
            for (int seg = 0; seg < s.Lvl.Segments.Length; seg++)
            {
                var plan = s.Lvl.Segments[seg];
                for (int sx = 0; sx < ws; sx++)
                {
                    int filled = FilledMask(s, seg, sx);
                    for (int sy = 0; sy < hs; sy++)
                    {
                        int c = plan.PlanColors[sy * ws + sx];
                        if (c >= 0 && ((filled >> sy) & 1) == 0) o[c] += 1;
                    }
                }
            }
            return o;
        }

        /// <summary>
        /// UX §5.9 item 1, K-47, K-48 (PL-2R-06): blocks left = N − correctly placed (locked) material blocks. A held block is
        /// not counted apart; a Söküm or an Undo raises it by itself; 0 ⇔ every segment is complete (K-47 item 5).
        /// </summary>
        public static int BlocksLeft(GameState s)
        {
            int placed = 0;
            int P = s.Lvl.Layout.Counts.Pieces;
            for (int id = 0; id < P; id++)
            {
                if (!IsMaterial(s, id) || PieceZone(s, id) != Zone.Site) continue;
                if ((PieceFlagBits(s, id) & FlagBit.Locked) != 0) placed += 1;
            }
            return s.Lvl.MaterialCount - placed;
        }

        /// <summary>K-48 (3): a material block is still in the yard, the truck queue or an undelivered batch.</summary>
        public static bool MaterialLeft(GameState s)
        {
            int P = s.Lvl.Layout.Counts.Pieces;
            for (int id = 0; id < P; id++)
            {
                if (!IsMaterial(s, id)) continue;
                int zone = PieceZone(s, id);
                if (zone == Zone.Yard || zone == Zone.Queue || zone == Zone.Pending) return true;
            }
            return false;
        }

        /// <summary>
        /// K-48 (K-28): every segment complete, every extra goal met and no material block left in the yard, the truck queue or
        /// an undelivered batch (Ağır Yük, crates and bags may stay). A held block is not core state (TECH §2R.2).
        /// </summary>
        public static bool LevelGoalsMet(GameState s) => AllSegmentsComplete(s) && ExtraGoalsMet(s) && !MaterialLeft(s);

        /// <summary>Goal values in `lvl.Goals` order (snapshot at move start, compared in step 7).</summary>
        public static int[] GoalSnapshot(GameState s)
        {
            var o = new int[s.Lvl.Goals.Length];
            for (int i = 0; i < o.Length; i++) o[i] = GoalValue(s, i);
            return o;
        }

        /// <summary>Indices of the non-`build` goals whose value differs from `before` (K-35 step 7 `goalProgress`).</summary>
        public static int[] ChangedCountGoals(GameState s, int[] before)
        {
            var o = new List<int>();
            for (int i = 0; i < s.Lvl.Goals.Length; i++)
            {
                int was = i < before.Length ? before[i] : 0;
                if (s.Lvl.Goals[i].Type != "build" && GoalValue(s, i) != was) o.Add(i);
            }
            return o.ToArray();
        }

        public static GoalView[] GoalViews(GameState s)
        {
            var o = new GoalView[s.Lvl.Goals.Length];
            for (int i = 0; i < o.Length; i++)
                o[i] = new GoalView(i, GoalKind(s.Lvl.Goals[i]), GoalValue(s, i), GoalTarget(s.Lvl, i), GoalDone(s, i));
            return o;
        }
    }
}
