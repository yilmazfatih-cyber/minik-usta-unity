#nullable enable
// Port of src/core/obstacles/S1_slidingSite.ts (web repo, source of truth).
namespace MinikUsta.Core.Obstacles
{
    /// <summary>
    /// S1 Kayan Şantiye — sliding site (docs/OBSTACLES.md S1; docs/GDD.md K-22, K-25, K-26; first level 5).
    ///
    /// Rule: `build.mode = "segments"` with 2–5 segments; only the active segment is on the site. When it is complete
    /// (K-15) in K-35 step 8 the site shifts to the next segment, which comes empty, and the batch with
    /// `forSegment` = completed segments joins the end of the truck queue; step 9 delivers the queue FIFO. After the last
    /// segment there is no shift: step 11 checks the win.
    ///
    /// The behaviour is the `segments` site strategy (Site.cs) and the delivery (Delivery.cs), which the pipeline runs for
    /// every segments level (a one-segment level is the trivial case), so this plugin has no hook. It applies when the
    /// level's data signature has S1 (segments mode and ≥ 2 segments, K-45/9).
    /// </summary>
    public static class S1_SlidingSite
    {
        public static readonly ObstacleRule Rule = new ObstacleRule(
            "S1",
            RuleZones.Site,
            301,
            new[] { ObstacleRules.ObstacleInfoKey("S1") },
            lvl => ObstacleRules.UsesMechanic(lvl, "S1"));
    }
}
