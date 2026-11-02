#nullable enable
// Paket B2: the 3D clip contract (Resources/art3d/king.json, fx.json — B1, B1k) and the engine-independent playback: `ClipLibrary`,
// `AnimPlayer` (deterministic: the same frame steps give the same drawings and events, however the time is cut into frames),
// `ClipPlacement` / `ClipActor` (placed by the pivot, never box-fitted), `FxPlayer` (budget, reduced motion, events) and
// `HomeParallax` (time drift, no edge ever shows).
using System;
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Presentation.Anim;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Tests.Fixtures;
using MinikUsta.Presentation.Ui;
using NUnit.Framework;

namespace MinikUsta.Presentation.Tests
{
    public class AnimTests
    {
        private static ClipLibrary King => TestArt.King;
        private static ClipLibrary Fx => TestArt.Fx;

        private static AnimClip Clip(string name) => TestArt.Set.Clip(name) ?? throw new AssertionException("no clip " + name);

        // --- the contract ---------------------------------------------------------------------------------------------------

        [Test]
        public void B2_king_json_has_the_nine_clips_of_ART_3D_7_2_with_their_frame_counts_loops_and_events()
        {
            Assert.AreEqual(1, King.Version);
            var want = new Dictionary<string, (int frames, int ls, int le, string events)>
            {
                ["king_idle"] = (48, 0, 47, "30:blink"),
                ["king_home_idle"] = (48, 0, 47, "30:blink"),
                ["king_cheer"] = (24, -1, -1, "4:lift_off 10:apex 14:land"),
                ["king_think"] = (36, 8, 35, "12:tap 24:tap 28:blink"),
                ["king_talk"] = (24, 0, 23, "0:beat 12:beat"),
                ["king_point"] = (18, 12, 17, "6:point_hit 12:hold_start 14:glint"),
                ["king_encourage"] = (24, -1, -1, "9:gesture 15:pump"),
                ["king_wave"] = (24, 6, 23, "9:wave_peak 18:wave_peak"),
                ["king_big_cheer"] = (48, 24, 47, "6:jump 15:land 22:hat_land 28:hop 40:hop"),
            };
            Assert.AreEqual(want.Count, King.Clips.Count, string.Join(",", King.Clips.Keys));
            foreach (var kv in want)
            {
                var c = King.Clip(kv.Key)!;
                Assert.IsNotNull(c, kv.Key);
                Assert.AreEqual(24f, c.Fps, kv.Key);
                Assert.AreEqual(kv.Value.frames, c.Length, kv.Key);
                Assert.AreEqual(kv.Value.ls, c.LoopStart, kv.Key);
                Assert.AreEqual(kv.Value.le, c.LoopEnd, kv.Key);
                Assert.AreEqual(kv.Value.events, string.Join(" ", c.Events.OrderBy(e => e.Key).Select(e => e.Key + ":" + e.Value)), kv.Key);
                Assert.AreEqual("chr_king_idle_00", c.First, kv.Key + " starts on K0 (ART_3D §7.1 rule 1)");
                // a clip leaves to K0: its timeline ends on K0, or its out frames do, or (idle, big cheer) the owner settles the cut
                bool k0 = c.Frames[c.Length - 1] == c.First || (c.Out.Length > 0 && c.Out[c.Out.Length - 1] == c.First);
                if (kv.Key != "king_idle" && kv.Key != "king_home_idle" && kv.Key != "king_big_cheer") Assert.IsTrue(k0, kv.Key + " ends on K0");
                foreach (var f in c.Drawings()) Assert.IsTrue(King.Has(f), kv.Key + ": " + f);
            }
            Assert.AreEqual("corner", King.Clip("king_idle")!.Set);
            Assert.AreEqual("stage", King.Clip("king_big_cheer")!.Set);
            Assert.AreEqual(0.705 / 0.88125, King.Clip("king_home_idle")!.Scale / King.Clip("king_idle")!.Scale, 1e-6, "stage = 1,25 × the corner size");
        }

        [Test]
        public void B2_king_aliases_resolve_stills_busts_and_the_deprecated_dede_names()
        {
            foreach (var pose in new[] { "idle", "cheer", "big_cheer", "think", "talk", "point", "encourage", "wave" })
                Assert.IsTrue(King.Has("chr_king_still_" + pose), pose);
            foreach (var e in new[] { "neutral", "happy", "think", "talk", "encourage", "surprised" })
                Assert.IsTrue(King.Has("chr_king_bust_" + e), e);
            Assert.IsTrue(King.Aliases["chr_dede_idle"].Deprecated);
            Assert.AreEqual("chr_king_cheer_05", King.StillOf(Clip("king_cheer")));
            Assert.AreEqual("chr_king_idle_00", King.StillOf(Clip("king_home_idle")), "the home idle uses the idle still");
            Assert.IsNull(Fx.StillOf(Clip("fx_dust_puff")), "fx have no stills");
        }

        [Test]
        public void B2_fx_json_has_the_twelve_clips_the_xl_icons_and_the_seven_parallax_layers()
        {
            var clips = new[]
            {
                "fx_crane_grab", "fx_hammer_smash", "fx_impact_star", "fx_paint_splash", "fx_trowel_sparkle", "fx_coin_spin", "fx_star_burst",
                "fx_confetti3d_rect", "fx_confetti3d_round", "fx_dust_puff", "fx_segment_done", "fx_lock_shine",
            };
            CollectionAssert.AreEquivalent(clips, Fx.Clips.Keys);
            Assert.AreEqual(60f, Fx.Clip("fx_lock_shine")!.Fps, "ART_3D §7.3: 12 cels at 60 fps = 200 ms (JUICE #12)");
            Assert.AreEqual(200f, Fx.Clip("fx_lock_shine")!.DurationMs, 1e-3);
            Assert.AreEqual("impact", Fx.Clip("fx_hammer_smash")!.Events[6]);
            Assert.IsTrue(Fx.Clip("fx_paint_splash")!.Tinted && Fx.Clip("fx_confetti3d_rect")!.Tinted, "white renders, tinted in code");
            foreach (var n in new[] { "coin", "star", "heart", "chest", "crane", "hammer", "brush", "gold_trowel" }) Assert.IsTrue(Fx.Has("icon_" + n + "_xl"), n);
            Assert.AreEqual(new[] { "bg_home_sky", "bg_home_clouds", "bg_home_far", "bg_home_town", "bg_home_ground", "bg_home_fore_l", "bg_home_fore_r" },
                Fx.Layers.Select(l => l.Name).ToArray(), "z order, back to front");
            Assert.AreEqual(0.0, Fx.Layers[0].Parallax);
            Assert.AreEqual(1.0, Fx.Layers[6].Parallax);
        }

        [Test]
        public void B2_memory_atlas_plus_king_plus_fx_stay_within_64_MB_RGBA32()
        {
            long atlas = ArtContractTests.Atlas.Pages.Sum(p => 4L * p.W * p.H);
            long total = atlas + King.MemoryBytes + Fx.MemoryBytes;
            Assert.LessOrEqual(total, 64L * 1024 * 1024, "ART_3D §7.5 / B1: " + total / 1e6 + " MB");
            foreach (var lib in new[] { King, Fx })
                foreach (var p in lib.Atlas.Pages) Assert.That(p.W <= 2048 && p.H <= 2048, p.Resource);
            Assert.IsFalse(King.Atlas.Pages.Any(p => p.Resource.Contains(".")), "P3-13: no dotted Resources names");
        }

        [Test]
        public void B2_a_broken_library_is_left_out_and_reported_never_thrown()
        {
            var log = new List<string>();
            var art = ArtSet.Load(name => name == ArtSet.KingResource ? "{\"version\":1}" : null, log.Add);
            Assert.IsNull(art.King);
            Assert.IsNull(art.Fx);
            Assert.AreEqual(2, log.Count, string.Join(" | ", log));
            Assert.IsNull(art.Clip("king_idle"));
        }

        // --- the player -------------------------------------------------------------------------------------------------------

        private static List<string> Trace(AnimClip clip, IEnumerable<float> steps, int passes = 0, AnimClip? then = null, Action<AnimPlayer, int>? at = null)
        {
            var p = new AnimPlayer();
            var o = new List<string>();
            p.OnEvent = e => o.Add("@" + e);
            p.Play(clip, passes, then);
            o.Add(p.Frame!);
            int i = 0;
            foreach (float dt in steps)
            {
                at?.Invoke(p, i++);
                p.Advance(dt);
                o.Add(p.Frame!);
            }
            return o;
        }

        [Test]
        public void B2_player_is_deterministic_and_fires_every_event_once_in_order_whatever_the_frame_steps()
        {
            var cheer = Clip("king_cheer");
            var fine = Enumerable.Repeat(1000f / 60f, 90).ToList(); // 1,5 s at 60 fps
            var a = Trace(cheer, fine);
            Assert.AreEqual(a, Trace(cheer, fine), "same steps, same trace");
            var coarse = Enumerable.Repeat(1000f / 60f * 3f, 30).ToList(); // the same 1,5 s at 20 fps
            var ea = a.Where(x => x.StartsWith("@")).ToList();
            var eb = Trace(cheer, coarse).Where(x => x.StartsWith("@")).ToList();
            CollectionAssert.AreEqual(new[] { "@king_cheer@4:lift_off", "@king_cheer@10:apex", "@king_cheer@14:land" }, ea);
            CollectionAssert.AreEqual(ea, eb, "a long frame fires every event it crossed, in order");
            Assert.AreEqual("chr_king_idle_00", a[a.Count - 1], "the cheer ends on K0 and holds it");
        }

        [Test]
        public void B2_player_maps_24_fps_frames_and_continues_with_then_without_losing_time()
        {
            var cheer = Clip("king_cheer");
            var idle = Clip("king_idle");
            var p = new AnimPlayer();
            p.Play(cheer, 0, idle);
            Assert.AreEqual(AnimPhase.Main, p.Phase);
            p.Advance(cheer.FrameMs * 4.5f);
            Assert.AreEqual(4, p.Index);
            Assert.AreEqual(cheer.Frames[4], p.Frame);
            p.Advance(cheer.FrameMs * 20f + 10f); // 24,5 frames + 10 ms: the cheer ended half a frame + 10 ms ago
            Assert.AreSame(idle, p.Clip, "then: the idle");
            Assert.AreEqual(cheer.FrameMs * 0.5f + 10f, p.Time, 1e-3, "the rest of the time carried over");
            Assert.AreEqual(2, p.Started);
        }

        [Test]
        public void B2_player_loops_its_range_passes_times_then_leaves_through_out()
        {
            var point = Clip("king_point"); // 18 frames, loop 12..17, out 4 frames
            var p = new AnimPlayer();
            p.Play(point, 2);
            var seen = new List<int>();
            for (int i = 0; i < 18 + 6 + 4 + 3; i++)
            {
                if (p.Phase == AnimPhase.Main) seen.Add(p.Index);
                p.Advance(point.FrameMs);
            }
            CollectionAssert.AreEqual(Enumerable.Range(0, 18).Concat(Enumerable.Range(12, 6)), seen, "intro + 2 passes of the loop");
            Assert.AreEqual(AnimPhase.Done, p.Phase);
            Assert.AreEqual("chr_king_idle_00", p.Frame, "the out frames end on K0");
        }

        [Test]
        public void B2_release_leaves_a_loop_through_out_at_the_pass_end_or_with_a_cut()
        {
            var think = Clip("king_think");
            var p = new AnimPlayer();
            p.Play(think);
            p.Advance(think.FrameMs * 20);
            p.Release();
            Assert.AreEqual(AnimPhase.Out, p.Phase, "think leaves through its out frames");
            p.Advance(think.FrameMs * think.Out.Length);
            Assert.AreEqual(AnimPhase.Done, p.Phase);
            Assert.AreEqual("chr_king_idle_00", p.Frame);

            var talk = Clip("king_talk");
            var q = new AnimPlayer();
            q.Play(talk);
            q.Advance(talk.FrameMs * 30); // pass 2, index 6
            q.Release();
            Assert.AreEqual(AnimPhase.Main, q.Phase, "talk finishes its pass (it ends on K0)");
            Assert.IsTrue(q.Ending);
            q.Advance(talk.FrameMs * 17 + 1);
            Assert.AreEqual(AnimPhase.Main, q.Phase);
            q.Advance(talk.FrameMs);
            Assert.AreEqual(AnimPhase.Done, q.Phase);
            Assert.AreEqual("chr_king_idle_00", q.Frame);

            var idle = Clip("king_idle");
            var r = new AnimPlayer();
            r.Play(idle);
            r.Advance(idle.FrameMs * 13);
            r.Release();
            Assert.AreEqual(AnimPhase.Done, r.Phase, "idle leaves at once");
            Assert.IsTrue(r.Cut, "a hard cut the owner softens");
        }

        [Test]
        public void B2_reduced_motion_shows_the_still_pose_runs_twice_as_fast_and_fires_no_event()
        {
            var cheer = Clip("king_cheer");
            var p = new AnimPlayer { Reduced = true, StillOf = c => King.StillOf(c) };
            int events = 0;
            p.OnEvent = _ => events++;
            p.Play(cheer);
            Assert.AreEqual("chr_king_cheer_05", p.Frame);
            p.Advance(cheer.DurationMs / 2f + 1f);
            Assert.AreEqual(AnimPhase.Done, p.Phase, "half the time");
            Assert.AreEqual(0, events);
        }

        // --- placement and the actor ------------------------------------------------------------------------------------------

        [Test]
        public void B2_a_trimmed_frame_is_placed_by_its_pivot_the_feet_stay_put_from_frame_to_frame()
        {
            var big = Clip("king_big_cheer");
            var feet = new Vec2(500, 1300);
            foreach (var name in big.Drawings())
            {
                var f = King.Find(name)!;
                var r = ClipPlacement.Rect(f, feet.X, feet.Y, ClipPlacement.ClipFactor(f, big));
                Assert.AreEqual(feet.X, r.X + f.PivotX * r.W, 1e-3, name);
                Assert.AreEqual(feet.Y, r.Bottom - f.PivotY * r.H, 1e-3, name);
                Assert.AreEqual(f.W / big.Scale, r.W, 1e-6, name + ": stage size = stored px / 0,705");
            }
        }

        [Test]
        public void B2_actor_writes_its_node_only_when_the_drawing_or_the_rect_changed_and_breathes_in_idle()
        {
            var surface = new FakeSurface();
            var node = new UiNode("k", new PxRect(0, 0, 10, 10));
            var actor = new ClipActor(King) { BaseScale = Clip("king_idle").Scale };
            actor.Player.Play(Clip("king_cheer"));
            actor.Bind(surface, node, new Vec2(200, 900), 1);
            int w0 = actor.Writes;
            Assert.AreEqual("chr_king_idle_00", node.Frame);
            actor.Advance(1f); // same drawing, same rect (the cheer has no breath)
            Assert.AreEqual(w0, actor.Writes, "nothing changed, nothing written");
            actor.Advance(Clip("king_cheer").FrameMs * 3);
            Assert.AreEqual(w0 + 1, actor.Writes);
            Assert.AreEqual("chr_king_cheer_01", node.Frame);
            Assert.IsFalse(node.PreserveAspect, "drawn by its pivot, never box-fitted");

            actor.Player.Play(Clip("king_idle"));
            double maxY = 0, minX = 2;
            for (int i = 0; i < 125; i++)
            {
                actor.Advance(16f); // one 2 s breath
                actor.Squash(out double bx, out double by);
                maxY = Math.Max(maxY, by);
                minX = Math.Min(minX, bx);
            }
            Assert.AreEqual(1.015, maxY, 1e-3, "ART_3D §7.1 rule 2: scaleY 1 + 0,015 sin");
            Assert.AreEqual(0.994, minX, 1e-3, "scaleX 1 − 0,006 sin");
            actor.Player.Reduced = true;
            actor.Squash(out double sx, out double sy);
            Assert.AreEqual(1.0, sy, 1e-9, "reduced motion: no breathing");
        }

        // --- FX ----------------------------------------------------------------------------------------------------------------

        private static (FxPlayer fx, List<FakeFx> sprites) NewFx()
        {
            var sprites = new List<FakeFx>();
            var fx = new FxPlayer(d =>
            {
                var s = new FakeFx(d);
                sprites.Add(s);
                return s;
            }, s => ((FakeFx)s).InPool = true) { Art = TestArt.Set };
            return (fx, sprites);
        }

        [Test]
        public void B2_fx_player_plays_a_clip_by_its_pivot_fires_its_events_and_returns_the_sprite()
        {
            var (fx, sprites) = NewFx();
            var events = new List<string>();
            int id = fx.Play("fx_hammer_smash", new Vec2(300, 400), FxDepth.Over, 1f, null, 0f, null, e => events.Add(e.Name));
            Assert.Greater(id, 0);
            Assert.AreEqual(1, sprites.Count);
            Assert.AreEqual("fx_hammer_00", sprites[0].Frame);
            Assert.AreEqual(new Vec2(300, 400), sprites[0].Position);
            var c = Clip("fx_hammer_smash");
            for (int i = 0; i < 40; i++) fx.Advance(1000f / 60f);
            CollectionAssert.AreEqual(new[] { "impact" }, events, "JUICE #61: the hit at frame 6");
            Assert.AreEqual(0, fx.Alive);
            Assert.IsTrue(sprites[0].InPool, "back in the pool");
            Assert.AreEqual(c.Length, sprites[0].Frames.Count, "every cel shown once (24 fps clip, 60 fps frames)");
        }

        [Test]
        public void B2_K36_JUICE61_the_hammer_impact_starts_the_impact_star_from_its_event_safely()
        {
            var (fx, sprites) = NewFx();
            for (int i = 0; i < FxPlayer.MaxAlive - 1; i++) fx.Play("fx_dust_puff", new Vec2(i, 0), FxDepth.Over); // a full budget
            fx.Play("fx_hammer_smash", new Vec2(300, 400), FxDepth.Over, onEvent: e =>
            {
                if (e.Name == "impact") fx.Play("fx_impact_star", new Vec2(260, 420), FxDepth.Over);
            });
            int stars = 0;
            for (int i = 0; i < 60; i++)
            {
                fx.Advance(1000f / 60f);
                Assert.LessOrEqual(fx.Alive, FxPlayer.MaxAlive, "the budget holds while an event adds an effect");
                stars = Math.Max(stars, sprites.Count(s => s.Visible && s.Frame != null && s.Frame.StartsWith("fx_impact_star")));
            }
            Assert.AreEqual(1, stars, "the impact star played");
            Assert.AreEqual(0, fx.Alive);
        }

        [Test]
        public void B2_fx_player_keeps_its_budget_and_reduced_motion_skips_the_heavy_clips()
        {
            var (fx, _) = NewFx();
            for (int i = 0; i < FxPlayer.MaxAlive + 10; i++) fx.Play("fx_dust_puff", new Vec2(i, 0), FxDepth.Over);
            Assert.AreEqual(FxPlayer.MaxAlive, fx.Alive, "the oldest end early");
            Assert.AreEqual(FxPlayer.MaxAlive, fx.Peak);
            fx.Reset();
            fx.Reduced = true;
            Assert.AreEqual(-1, fx.Play("fx_segment_done", Vec2.Zero, FxDepth.Under), "heavy: not in reduced motion");
            Assert.Greater(fx.Play("fx_dust_puff", Vec2.Zero, FxDepth.Over), 0, "light: played …");
            var dust = Clip("fx_dust_puff");
            fx.Advance(dust.DurationMs / 2f + 1f);
            Assert.AreEqual(0, fx.Alive, "… twice as fast");
            Assert.AreEqual(-1, fx.Play("fx_nothing", Vec2.Zero, FxDepth.Over), "an unknown clip is skipped");
        }

        [Test]
        public void B2_fx_player_follows_a_moving_block_and_a_life_ends_a_loop()
        {
            var (fx, sprites) = NewFx();
            var at = new Vec2(100, 100);
            fx.Play("fx_trowel_sparkle", new Vec2(0, -50), FxDepth.Over, 1f, null, 0f, () => at, lifeMs: 300f, fadeMs: 100f);
            at = new Vec2(400, 700);
            fx.Advance(16f);
            Assert.AreEqual(new Vec2(400, 650), sprites[0].Position, "follow + offset");
            fx.Advance(250f);
            Assert.Less(sprites[0].Alpha, 1f, "fades out at the end of its life");
            fx.Advance(40f);
            Assert.AreEqual(0, fx.Alive);
        }

        // --- home parallax -----------------------------------------------------------------------------------------------------

        [Test]
        public void B2_home_layers_drift_with_time_by_their_factor_and_never_show_an_edge()
        {
            var m = new HomeModel { Layers = Fx.Layers };
            var home = HomeView.Build(m, 1080, 1920, Rig.Tr());
            Assert.IsNull(home.Find("home.bg"), "the layers replace the flattened bg_home");
            var surface = new FakeSurface();
            var px = new HomeParallax();
            px.Bind(surface, home, Fx.Layers);
            Assert.AreEqual(6, px.Count, "the sky (factor 0) stays");
            double maxFore = 0;
            for (int i = 0; i < 1000; i++)
            {
                px.Advance(20f);
                foreach (var l in Fx.Layers)
                {
                    var n = home.Get(HomeView.LayerPrefix + l.Name);
                    if (l.W >= 1080)
                    {
                        Assert.LessOrEqual(n.Rect.X, 1e-6, l.Name + " covers the left edge");
                        Assert.GreaterOrEqual(n.Rect.Right, 1080 - 1e-6, l.Name + " covers the right edge");
                    }
                    if (l.Name == "bg_home_fore_l") maxFore = Math.Max(maxFore, Math.Abs(n.Rect.X - (0 - HomeView.ParallaxAmpPx)));
                }
            }
            Assert.AreEqual(HomeView.ParallaxAmpPx, maxFore, 0.5, "the foreground sways the full amplitude");
            int w = px.Writes;
            px.Reduced = true;
            px.Bind(surface, home, Fx.Layers);
            for (int i = 0; i < 100; i++) px.Advance(20f);
            Assert.AreEqual(6, px.Writes, "reduced motion: placed once, never moved");
            Assert.Greater(w, 6);
        }
    }
}
