#nullable enable
// Port of src/core/level/mechanics.ts (web repo, source of truth).
using System;
using System.Collections.Generic;

namespace MinikUsta.Core.Level
{
    public sealed class MechanicSignature
    {
        public readonly string Id;
        /// <summary>OBSTACLES "İlk bölüm" column (design data; the derivation never uses it).</summary>
        public readonly int FirstLevel;
        public readonly Func<LevelData, bool> Detect;

        public MechanicSignature(string id, int firstLevel, Func<LevelData, bool> detect)
        {
            Id = id;
            FirstLevel = firstLevel;
            Detect = detect;
        }
    }

    /// <summary>
    /// Mechanic data signatures (GDD K-45/10, R-21; TECH_DESIGN §8.3, §2R.3 CL-2R-12): the code twin of the OBSTACLES
    /// "Veri imzası" table (Faz 2R: W1 first level 4, W2 `height = H`, Y5 = I5/Q9 Ağır Yük, S2 out of the MVP, S9 new).
    /// A level's mechanic set is derived ONLY from its data; `teaches` adds nothing (it is checked by L-16).
    /// </summary>
    public static class Mechanics
    {
        private static IEnumerable<PieceData> AllPieces(LevelData level)
        {
            foreach (var b in level.Yard.Batches)
                foreach (var p in b.Pieces) yield return p;
        }

        private static bool AnyFlag(LevelData level, string flag)
        {
            foreach (var p in AllPieces(level)) if (p.HasFlag(flag)) return true;
            return false;
        }

        private static bool AnyGap(LevelData level, string type)
        {
            foreach (var g in level.Wall.Gaps) if (g.Type == type) return true;
            return false;
        }

        private static bool AnyObstacle(LevelData level, string type)
        {
            foreach (var o in level.Obstacles) if (o.Type == type) return true;
            return false;
        }

        /// <summary>
        /// Ağır Yük (Y5, GDD K-44 Faz 2R): I5 and Q9 in every orientation; not material, no colour. Same predicate as
        /// movement `IsCargoShape` (a test keeps them equal); read from Shapes so the level modules stay out of the
        /// movement → grid → compile dependency cycle.
        /// </summary>
        public static bool IsCargo(ShapeDef shape) => Shapes.AlwaysHeavy.Contains(shape.Kind);

        /// <summary>
        /// Board height H = max(Hy, Hs + eMax) (K-49) from the data, without building a `BoardGeo`: it never throws on a level
        /// whose sizes break the frame (L-28 reports those), and it clamps like `MakeGeo`.
        /// </summary>
        public static int BoardHeight(LevelData level)
        {
            int hy = level.Yard.Rows ?? Geometry.DefaultSizes.Hy;
            int hs = level.Site?.Rows ?? Geometry.DefaultSizes.Hs;
            int eMax = level.Build.Elevator != null ? Math.Max(0, level.Build.Elevator.Range[1]) : 0;
            return Math.Min(Geometry.MaxBoardRows, Math.Max(hy, hs + eMax));
        }

        private static bool AnyCargo(LevelData l)
        {
            foreach (var p in AllPieces(l)) if (IsCargo(Shapes.ShapeById(p.Shape))) return true;
            return false;
        }

        private static bool AnyHidden(LevelData l, string kind)
        {
            foreach (var s in l.Build.Segments) if (s.Hidden != null && s.Hidden.Kind == kind) return true;
            return false;
        }

        /// <summary>One row per OBSTACLES "Veri imzası" line (Faz 2R), same order.</summary>
        public static readonly MechanicSignature[] Table =
        {
            new MechanicSignature("W1", 4, l => AnyGap(l, "static")),
            new MechanicSignature("W2", 6, l => l.Wall.Height == BoardHeight(l)),
            // N2: W3 is a size, not a type.
            new MechanicSignature("W3", 9, l => { foreach (var g in l.Wall.Gaps) if (g.Size == 1) return true; return false; }),
            new MechanicSignature("W4", 13, l => AnyGap(l, "shutter")),
            new MechanicSignature("W5", 16, l => AnyGap(l, "slider")),
            new MechanicSignature("W6", 22, l => AnyGap(l, "paint")),
            new MechanicSignature("W7", 26, l => AnyGap(l, "locked")),
            new MechanicSignature("W8", 32, l => l.Wall.Fan != null),
            new MechanicSignature("Y1", 11, l => AnyObstacle(l, "crate")),
            new MechanicSignature("Y2", 18, l => AnyObstacle(l, "cement_bag")),
            new MechanicSignature("Y3", 24, l => AnyFlag(l, PieceFlags.Chained)),
            new MechanicSignature("Y4", 28, l => AnyFlag(l, PieceFlags.Wet)),
            // Faz 2R: Ağır Yük = I5/Q9 in any batch (K-44); the w ≥ 3 "heavy" orientations are material blocks now.
            new MechanicSignature("Y5", 8, AnyCargo),
            new MechanicSignature("Y6", 14, l => l.Gravity.Yard),
            new MechanicSignature("Y7", 19, l => AnyObstacle(l, "screw")),
            new MechanicSignature("Y8", 35, l => AnyFlag(l, PieceFlags.Mortar)),
            new MechanicSignature("S1", 1, l => l.Build.Mode == "segments" && l.Build.Segments.Length >= 2),
            new MechanicSignature("S3", 21, l => AnyFlag(l, PieceFlags.Glass)),
            new MechanicSignature("S4", 17, l => (l.Build.Debris?.Length ?? 0) > 0),
            new MechanicSignature("S5", 31, l => l.Build.Mode == "carousel"),
            new MechanicSignature("S6", 37, l => l.Build.Elevator != null),
            new MechanicSignature("S7-R", 27, l => AnyHidden(l, "repeat")),
            new MechanicSignature("S7-M", 29, l => AnyHidden(l, "mirrorOf")),
            new MechanicSignature("S8", 38, l => AnyFlag(l, PieceFlags.Balloon)),
            new MechanicSignature("G-H", 15, l => l.Gravity.Build == "high"),
            new MechanicSignature("G-L", 23, l => l.Gravity.Build == "low"),
            // S9 Geniş Şantiye (K-49, R2-02; D-103: first level 6, v3c): a site of 3 columns.
            new MechanicSignature("S9", 6, l => (l.Site?.Cols ?? Geometry.DefaultSizes.Ws) >= 3),
        };

        /// <summary>
        /// v3 block traits (OBSTACLES "v3 imza önerileri", GDD §16): same shape as `Table`, kept apart until the rows move into the
        /// OBSTACLES "Veri imzası" table. First levels = LEVELS §8.3. TS `TRAIT_MECHANICS`.
        /// </summary>
        public static readonly MechanicSignature[] TraitTable =
        {
            new MechanicSignature("Y9", 5, l => { foreach (var p in AllPieces(l)) if (p.Colors != null) return true; return false; }),
            new MechanicSignature("Y10", 7, l => { foreach (var p in AllPieces(l)) if (p.Stack != null) return true; return false; }),
            new MechanicSignature("Y11", 8, l => AnyFlag(l, PieceFlags.Wrapped) || AnyFlag(l, PieceFlags.Scissors)),
            new MechanicSignature("Y12", 7, l => AnyFlag(l, PieceFlags.Bonus)),
            new MechanicSignature("Y13", 9, l => AnyFlag(l, PieceFlags.Slippery)),
        };

        static Mechanics()
        {
            if (Table.Length != MechanicIds.All.Length) throw new InvalidOperationException("mechanics table out of sync with MECHANIC_IDS");
            if (string.Join(",", System.Linq.Enumerable.Select(TraitTable, m => m.Id)) != string.Join(",", MechanicIds.Traits))
                throw new InvalidOperationException("trait table out of sync with TRAIT_MECHANIC_IDS");
        }

        /// <summary>The level's mechanic set: signature table order, then the v3 traits (Y9 … Y13).</summary>
        public static string[] DeriveMechanics(LevelData level)
        {
            var o = new List<string>();
            foreach (var m in Table) if (m.Detect(level)) o.Add(m.Id);
            foreach (var m in TraitTable) if (m.Detect(level)) o.Add(m.Id);
            return o.ToArray();
        }

        /// <summary>Mechanics of `level` that are not in `previous` (the union of earlier levels' sets), in table order.</summary>
        public static string[] NewMechanics(LevelData level, ISet<string> previous)
        {
            var o = new List<string>();
            foreach (var id in DeriveMechanics(level)) if (!previous.Contains(id)) o.Add(id);
            return o.ToArray();
        }
    }
}
