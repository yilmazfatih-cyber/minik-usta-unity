#nullable enable
// C1 (CL-V3C-08 root cause): LevelHud rebuilds its tree in place when the plan picture changes (`SetHudState` → `surface.Show(Hud)`),
// and the Unity surface destroys the old RectTransforms on every Show. `RebindTargets` left the bonus "+2" burst target on the old
// tree, so the first bonus after a rebuild set the scale of a destroyed RectTransform every frame (an engine NullReferenceException
// thrown inside GameApp.Tick): the level froze with "+2" on the moves plate and the CI shot scenario, ticked after the app in
// GameRunner.Update, never ran again. FakeSurface now destroys the targets of a re-shown layer like Unity does.
using System.Linq;
using MinikUsta.Core;
using MinikUsta.Presentation.Flow;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Tests.Fixtures;
using MinikUsta.Presentation.Ui;
using NUnit.Framework;

namespace MinikUsta.Presentation.Tests
{
    [TestFixture]
    public class HudRebindTests
    {
        /// <summary>Every tween target the HUD hands out (read and written once: a destroyed one throws).</summary>
        private static void TouchAll(LevelHud hud, string where)
        {
            foreach (var (name, t) in new[] { ("moves", hud.MovesChip), ("blocks", hud.BlocksChip), ("burst", hud.MovesBurst), ("trowel", hud.TrowelIcon), ("gold", hud.GoldChip) }
                         .Concat(Enumerable.Range(0, 4).Select(i => ("bead" + i, hud.StreakBead(i)))))
            {
                Assert.DoesNotThrow(() => t.Scale = t.Scale, where + ": the " + name + " target is alive");
            }
        }

        [Test(Description = "C1 FakeSurface destroys the tween targets of a layer it shows again (UiRenderer.Show destroys the old tree)")]
        public void C1_fake_surface_destroys_the_targets_of_a_re_shown_layer()
        {
            var ui = new FakeSurface();
            var root = UiKit.Group("r", new PxRect(0, 0, 100, 100));
            var child = root.Add(UiKit.Group("r.c", new PxRect(0, 0, 10, 10)));
            ui.Show(UiLayer.Hud, root, null);
            var t = ui.Target(child);
            t.Scale = new Vec2(2f, 2f);
            ui.Show(UiLayer.Hud, root, null); // the same root again: Unity still rebuilds every object
            Assert.Throws<System.InvalidOperationException>(() => t.Scale = Vec2.One);
            Assert.DoesNotThrow(() => ui.Target(child).Scale = Vec2.One, "a target taken after the Show is alive");
            ui.Show(UiLayer.Window, null, null);
            Assert.DoesNotThrow(() => ui.Target(child).Scale = Vec2.One, "another layer's Show leaves the HUD alone");
        }

        [Test(Description = "C1 K-60 item 7 D-105 every HUD tween target stays alive across the HUD rebuilds (the first plan picture of a level rebuilds the tree right after Layout) and the bonus level plays its \"+2\" burst on the live target")]
        public void C1_K60_item_7_HUD_targets_survive_every_rebuild_and_the_bonus_burst_plays()
        {
            foreach (int level in Play.Levels)
            {
                var rig = new AppRig(null, 1080, 844 * 1080.0 / 390);
                rig.App.Boot();
                for (int n = 1; n < level; n++) rig.App.Save.Level(n).Won = true;
                rig.Tap(HomeView.PlayId);
                if (rig.Screen == AppScreen.LevelStart) rig.TapOption(PanelAction.Play);
                Assert.AreEqual(AppScreen.Level, rig.Screen, "B" + level);
                Assert.AreEqual(level, rig.Player.LevelNumber);
                rig.Settle();
                // Layout shows the HUD once, the first SetHudState fills the empty plan picture and shows it again (SyncCells)
                Assert.GreaterOrEqual(rig.Ui.ShowsOf(UiLayer.Hud), 2, "B" + level + ": the HUD was rebuilt after its layout");
                var hud = rig.View.Hud!;
                TouchAll(hud, "B" + level + " at the start");
                bool burst = false;
                int moves = 0;
                foreach (var m in Play.SolverGolden(level).Drags)
                {
                    rig.Settle();
                    var r = rig.Player.Commit(m);
                    Assert.AreEqual(MoveStatuses.Applied, r.Status, "B" + level);
                    if (r.Events.OfType<MovesChangedEvent>().Any(e => e.Reason == MovesChangedReasons.Bonus)) burst = true;
                    rig.Settle();
                    TouchAll(hud, "B" + level + " after move " + (++moves));
                }
                if (level == Play.BonusLevel)
                {
                    Assert.IsTrue(burst, "B" + level + " paid its bonus");
                    Assert.Contains("+2", rig.View.MovesBurstShown, "the burst showed and its pop tween ran on the HUD target");
                }
            }
        }
    }
}
