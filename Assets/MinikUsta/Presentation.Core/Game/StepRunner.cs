#nullable enable
// Plays one `AnimationStep` on the views (Faz 3 Q1): the JUICE.md row of the step as built-in tweens (D-096) on the block
// visuals and HUD chips — web EventPlayer.ts `runCue` + juice/handlers.ts, placeholder edition (no particles, sounds or
// haptics yet). The state is already final when a step runs (TECH §1.4); intermediate poses come from the step's event
// (K-35 order), the final pose from the state at `End`. Board motions carry `LevelPlayer.BoardTag` so a grab can finish
// them (R-12); `instant` steps (fast-forward, flush) complete at once, after the running board motions completed.
//
// Faz 3 Q2 adds the game-feel layer on top of the same rows (the D-091 list): grow-and-lock pulse and sprite sparks on a
// correct placement, landing dust and the cell-by-cell landing wave, streak beads and the trowel pop, the delivery truck and
// the site slide, confetti, camera shakes, Söküm dust, Bonus İnşaat coins and the win star. The helpers of that layer are in
// StepRunner.Fx.cs.
using System;
using System.Collections.Generic;
using MinikUsta.Core;
using MinikUsta.Presentation.Animation;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Feel;
using MinikUsta.Presentation.Tweening;
using static MinikUsta.Core.State;

namespace MinikUsta.Presentation.Game
{
    internal sealed partial class StepRunner
    {
        private readonly LevelPlayer p;

        public StepRunner(LevelPlayer player)
        {
            p = player;
        }

        private FeelSettings F => p.Feel;

        public void Run(AnimationStep step, float time, bool instant)
        {
            var session = p.Session;
            var L = p.Layout;
            if (session == null || L == null) return;
            if (instant) p.Clock.Complete(LevelPlayer.BoardTag);
            p.StepsRun++;
            var s = session.State;
            var g = L.Grid;
            float c = (float)g.CellPx;
            switch (step.Kind)
            {
                case StepKind.Cancel:
                    {
                        var v = Piece(step.PieceId);
                        if (v == null || !BoardPose.RestAnchor(s, step.PieceId, out int ax, out int ay)) return;
                        var home = BoardPose.Center(g, ax, ay, BoardPose.ShapeOf(s, step.PieceId));
                        v.Lifted = false;
                        if (step.Flag) Play(Tweens.Move(v, home, F.YardDropMs, F.YardDropEase), time, instant, true);
                        else Play(Tweens.Move(v, home, step.Ms, step.Ease, p.Reduced ? 0 : F.CancelArcCells * c), time, instant, true);
                        Play(Tweens.Scale(v, 1f, step.Ms, EaseType.OutQuad), time, instant, true);
                        Play(Tweens.Fade(v, 1f, Math.Min(step.Ms, F.GhostSwitchMs)), time, instant, true);
                        return;
                    }
                case StepKind.YardDrop:
                    {
                        if (!(step.Event is PieceMovedEvent e)) return;
                        var v = Piece(e.PieceId);
                        if (v == null) return;
                        var shape = BoardPose.ShapeOf(s, e.PieceId);
                        var to = BoardPose.Center(g, e.To, shape);
                        v.Lifted = false;
                        Play(Tweens.Move(v, to, step.Ms, step.Ease), time, instant, true);
                        Play(Tweens.Scale(v, 1f, step.Ms, ReleaseEase(step.Ease)), time, instant, true);
                        if (!p.Reduced) Play(Squash(v, 1f, F.YardDropSquashY, step.Ms, step.Ease).SetDelay(step.Ms), time, instant, true);
                        DustAfter(step.Ms, Bottom(to, shape, c), F.ParticleDustYard, time, instant);
                        return;
                    }
                case StepKind.RailClamp:
                    {
                        if (!(step.Event is PieceMovedEvent e)) return;
                        var v = Piece(e.PieceId);
                        if (v == null) return;
                        v.Lifted = false;
                        Play(Tweens.Move(v, BoardPose.Center(g, e.To, BoardPose.ShapeOf(s, e.PieceId)), step.Ms, step.Ease), time, instant, true);
                        Play(Tweens.Scale(v, 1f, step.Ms, step.Ease), time, instant, true);
                        return;
                    }
                case StepKind.SiteFall:
                    {
                        if (!(step.Event is PieceFellEvent e)) return;
                        var v = Piece(e.PieceId);
                        if (v == null) return;
                        v.Lifted = false;
                        var to = BoardPose.Center(g, e.To, BoardPose.ShapeOf(s, e.PieceId));
                        Play(Tweens.Move(v, to, step.Ms).SetCurve(FallMotion.ReleaseFallCurve(e.Rows, session.Lvl.Gravity.Build, F)), time, instant, true);
                        Play(Tweens.Scale(v, 1f, Math.Min(step.Ms, F.YardDropMs), ReleaseEase(EaseType.OutQuad)), time, instant, true);
                        return;
                    }
                case StepKind.Landing:
                    {
                        var v = Piece(step.PieceId);
                        if (v == null) return;
                        var shape = BoardPose.ShapeOf(s, step.PieceId);
                        float sx = p.Reduced ? F.ReducedScaleMax : F.LandSquashX;
                        float sy = p.Reduced ? 2 - sx : F.LandSquashY;
                        Play(LandSquash(v, shape, sx, sy, step.Ms, step.Ease), time, instant, true);
                        p.Particles.Dust(Bottom(v.Position, shape, c), F.ParticleLand);
                        return;
                    }
                case StepKind.YardFall:
                    {
                        if (!(step.Event is PieceFellEvent e)) return;
                        var v = Piece(e.PieceId);
                        if (v == null) return;
                        var shape = BoardPose.ShapeOf(s, e.PieceId);
                        var to = BoardPose.Center(g, e.To, shape);
                        Play(Tweens.Move(v, to, step.Ms).SetCurve(FallMotion.YardFallLeg(e.Rows, F).Progress), time, instant, true);
                        if (!p.Reduced)
                        {
                            float sx = 1 + (F.LandSquashX - 1) / 2;
                            Play(LandSquash(v, shape, sx, 2 - sx, F.LandMs, F.LandEase).SetDelay(step.Ms), time, instant, true);
                        }
                        DustAfter(step.Ms, Bottom(to, shape, c), F.ParticleDustCascade, time, instant);
                        return;
                    }
                case StepKind.BalloonGlide:
                    {
                        if (!(step.Event is BalloonRoseEvent e)) return;
                        var v = Piece(e.PieceId);
                        if (v == null) return;
                        v.Lifted = false;
                        Play(Tweens.Move(v, BoardPose.Center(g, e.To, BoardPose.ShapeOf(s, e.PieceId)), step.Ms, step.Ease), time, instant, true);
                        Play(Tweens.Scale(v, 1f, Math.Min(step.Ms, F.YardDropMs), EaseType.OutQuad), time, instant, true);
                        return;
                    }
                case StepKind.WindDrift:
                    {
                        if (!(step.Event is WindDriftEvent e)) return;
                        var v = Piece(e.PieceId);
                        if (v == null) return;
                        var to = new Vec2(v.Position.X + e.Dx * c, v.Position.Y);
                        Play(Tweens.Move(v, to, step.Ms, step.Ease), time, instant, true);
                        return;
                    }
                case StepKind.PlaceCorrect:
                    {
                        var v = Piece(step.PieceId);
                        if (v == null) return;
                        Play(FlashPulse(v, F.PlaceFlashPeak, step.Ms), time, instant, true);
                        // Q2 (4): grow 1,0 → 1,2 and shrink into the lock, with a burst of sprite sparks in the block's colour
                        Play(GrowPulse(v, p.Reduced ? F.ReducedScaleMax : F.PlacePulsePeak, F.PlacePulseMs, F.PlacePulseSplit, F.PlacePulseEase), time, instant, true);
                        p.Particles.Sparks(v.Position, Palette.Block(PieceColor(s, step.PieceId)), F.ParticlePlaceOk);
                        return;
                    }
                case StepKind.BlocksChipFly:
                    {
                        int left = step.N ?? p.BlocksLeft;
                        var v = Piece(step.PieceId);
                        var chip = HudLayout.Center(HudLayout.BlocksChip(L));
                        if (v == null || instant || p.Reduced || step.Ms <= 0)
                        {
                            p.ShowBlocks(left);
                            Play(Tweens.Pop(p.View.BlocksChip, 1f, F.BlocksChipPeak, 1f, F.BlocksChipPopMs), time, instant, false);
                            return;
                        }
                        var flyer = p.AcquireFlyer(PieceColor(s, step.PieceId), c * 0.6);
                        flyer.Visible = true;
                        flyer.Position = v.Position;
                        flyer.Scale = Vec2.One;
                        flyer.Alpha = 1;
                        var seq = new Sequence()
                            .Append(Tweens.Move(flyer, chip, step.Ms, step.Ease, F.BlocksChipFlyArcCells * c))
                            .AppendCallback(() =>
                            {
                                p.ReleaseFlyer(flyer);
                                p.ShowBlocks(left);
                            })
                            .Append(Tweens.Pop(p.View.BlocksChip, 1f, F.BlocksChipPeak, 1f, F.BlocksChipPopMs));
                        Play(seq, time, false, false);
                        return;
                    }
                case StepKind.PlaceWrong:
                    {
                        if (!(step.Event is PieceBouncedEvent e)) return;
                        var v = Piece(e.PieceId);
                        if (v == null) return;
                        v.Raised = true;
                        Play(Pulse01(x => v.Tint = x, F.WrongTintAlpha, F.WrongTintMs), time, instant, true);
                        p.Buzz(HapticKind.DoubleLight);
                        if (step.Flag || !e.To.HasValue) return; // into the truck queue: #88
                        var to = e.To.Value;
                        var shape = BoardPose.ShapeOf(s, e.PieceId);
                        float shake = p.Reduced ? 0 : F.WrongShakeMs;
                        int rows = e.ViaDrop ? step.N ?? 0 : 0;
                        float fall = e.ViaDrop ? FallMotion.YardFallMs(rows, F) : 0;
                        float flight = Math.Max(0, step.Ms - shake - fall);
                        var seq = new Sequence();
                        if (shake > 0) seq.Append(Tweens.Shake(v, new Vec2(F.WrongShakePx, 0), F.WrongShakeCycles, shake, F.WrongShakeEase));
                        seq.Append(Tweens.Move(v, BoardPose.Center(g, to.X, to.Y + rows, shape), flight, F.BounceEase, p.Reduced ? 0 : F.BounceArcCells * c));
                        if (fall > 0) seq.Append(Tweens.Move(v, BoardPose.Center(g, to.X, to.Y, shape), fall).SetCurve(FallMotion.YardFallLeg(rows, F).Progress));
                        var rest = Bottom(BoardPose.Center(g, to.X, to.Y, shape), shape, c);
                        seq.AppendCallback(() => p.Particles.Dust(rest, F.ParticleDustBounce)); // JUICE #13: 3 small grey dust puffs where it comes to rest
                        Play(seq, time, instant, true);
                        return;
                    }
                case StepKind.BounceToQueue:
                    {
                        var v = Piece(step.PieceId);
                        if (v == null) return;
                        v.Raised = true;
                        var seq = new Sequence()
                            .Append(Tweens.Move(v, HudLayout.QueueChip(L), step.Ms, step.Ease, p.Reduced ? 0 : F.BounceArcCells * c))
                            .Join(Tweens.Scale(v, F.QueueShrink, step.Ms, step.Ease))
                            .AppendCallback(() => v.Visible = false);
                        Play(seq, time, instant, true);
                        return;
                    }
                case StepKind.PieceReturn:
                    {
                        if (!(step.Event is PieceReturnedEvent e)) return;
                        var v = Piece(e.PieceId);
                        if (v == null) return;
                        v.Raised = true;
                        if (!e.To.HasValue)
                        {
                            var q = new Sequence()
                                .Append(Tweens.Move(v, HudLayout.QueueChip(L), step.Ms, step.Ease))
                                .Join(Tweens.Scale(v, F.QueueShrink, step.Ms, step.Ease))
                                .AppendCallback(() => v.Visible = false);
                            Play(q, time, instant, true);
                            return;
                        }
                        var to = BoardPose.Center(g, e.To.Value, BoardPose.ShapeOf(s, e.PieceId));
                        Play(Tweens.Move(v, to, step.Ms, step.Ease, p.Reduced ? 0 : F.BounceArcCells * c), time, instant, true);
                        Play(Tweens.Scale(v, 1f, step.Ms, EaseType.OutQuad), time, instant, true);
                        return;
                    }
                case StepKind.TrowelFlight:
                case StepKind.CraneLift:
                    {
                        if (!(step.Event is PieceLiftedEvent e)) return;
                        var v = Piece(e.PieceId);
                        if (v == null) return;
                        v.Raised = true;
                        var to = BoardPose.Center(g, e.To, BoardPose.ShapeOf(s, e.PieceId));
                        var seq = new Sequence()
                            .Append(Tweens.Move(v, to, step.Ms, step.Ease, p.Reduced ? 0 : F.TrowelArcCells * c))
                            .Append(FlashPulse(v, F.PlaceFlashPeak, F.PlaceSheenMs));
                        Play(seq, time, instant, true);
                        return;
                    }
                case StepKind.CargoSmash:
                    {
                        if (!(step.Event is CargoSmashedEvent e)) return;
                        var v = Piece(e.PieceId);
                        if (v == null) return;
                        var seq = new Sequence()
                            .Append(Tweens.Scale(v, F.SmashScale, step.Ms, EaseType.Linear))
                            .Join(Tweens.Fade(v, 0f, step.Ms))
                            .Join(FlashPulse(v, F.PlaceFlashPeak, Math.Min(step.Ms, F.BlockedShakeMs)))
                            .AppendCallback(() => v.Visible = false);
                        Play(seq, time, instant, true);
                        return;
                    }
                case StepKind.Brush:
                    {
                        if (!(step.Event is ColorsSwappedEvent e)) return;
                        var a = Piece(e.A);
                        var b = Piece(e.B);
                        var seq = new Sequence().AppendInterval(step.Ms / 2).AppendCallback(() =>
                        {
                            a?.SetLook(PieceColor(s, e.A), false);
                            b?.SetLook(PieceColor(s, e.B), false);
                        }).AppendInterval(step.Ms / 2);
                        Play(seq, time, instant, true);
                        return;
                    }
                case StepKind.PiecePaint:
                    {
                        if (!(step.Event is PiecePaintedEvent e)) return;
                        Piece(e.PieceId)?.SetLook(PieceColor(s, e.PieceId), false);
                        return;
                    }
                case StepKind.GlassBreak:
                    {
                        var v = Piece(step.PieceId);
                        if (v != null) Play(FlashPulse(v, F.PlaceFlashPeak, step.Ms), time, instant, true);
                        p.CameraShake(F.GlassShakePx); // JUICE #42: the screen shakes 2 px
                        p.Buzz(HapticKind.Medium);
                        return;
                    }
                case StepKind.StreakPip:
                    StreakPip(step, L, time, instant);
                    return;
                case StepKind.TrowelEarned:
                    TrowelEarned(step, L, time, instant);
                    return;
                case StepKind.StreakReset:
                    StreakReset(step, time, instant);
                    return;
                case StepKind.MovesTick:
                case StepKind.MovesAdded:
                    {
                        int n = step.N ?? session.MovesLeft;
                        if (step.Kind == StepKind.MovesAdded)
                        {
                            var seq = new Sequence().AppendInterval(step.Ms).AppendCallback(() => p.ShowMoves(n))
                                .Append(Tweens.Pop(p.View.MovesChip, 1f, F.MovesAddBump, 1f, F.MovesTickMs));
                            Play(seq, time, instant, false);
                            return;
                        }
                        p.ShowMoves(n);
                        if (step.Ms > 0) Play(Tweens.Pop(p.View.MovesChip, 1f, 1.12f, 1f, step.Ms), time, instant, false);
                        return;
                    }
                case StepKind.LastMoves:
                    {
                        p.ShowMoves(step.N ?? session.MovesLeft);
                        if (!p.Reduced && step.Ms > 0) Play(Tweens.Pulse(p.View.MovesChip, F.LastMovesStrong, step.Ms / 2), time, instant, false);
                        return;
                    }
                case StepKind.SegmentDone:
                    {
                        // JUICE #18 (web EventPlayer.segmentDone): the structure flashes ×2 while the scaffold fades
                        // (duration.scaffoldFade); with a next segment (`Flag`, `ToSeg`) the finished one flies to its
                        // panorama slot and the next plan comes in; the last segment stays (the win plays on it)
                        if (!(step.Event is SegmentCompletedEvent e)) return;
                        float fade = Math.Min(F.ScaffoldFadeMs, step.Ms);
                        var seq = new Sequence();
                        var slot = HudLayout.Center(HudLayout.Segments(L));
                        float slideStart = p.Reduced ? 0 : fade;
                        float slideMs = Math.Max(0, step.Ms - slideStart);
                        foreach (int id in p.Pieces.Ids)
                        {
                            if (PieceZone(s, id) != Zone.Site || PieceSeg(s, id) != e.Seg) continue;
                            var v = p.Pieces.Get(id)!;
                            if (!p.Reduced) seq.Insert(0, Flashes(v, F.SegmentFlashPeak, fade, F.SegmentFlashes));
                            if (!step.Flag) continue;
                            v.Raised = true;
                            seq.Insert(slideStart, Tweens.Move(v, slot, slideMs, step.Ease));
                            seq.Insert(slideStart, Tweens.Scale(v, 0.25f, slideMs, step.Ease));
                            seq.Insert(slideStart + slideMs * 0.7f, Tweens.Fade(v, 0f, slideMs * 0.3f));
                        }
                        if (step.Flag)
                        {
                            seq.InsertCallback(slideStart, () => p.RebuildBoard());
                            // JUICE #18 "sıradaki dilim ekranın sağ kenarından girer": the new site slides in over the board's right edge
                            if (!p.Reduced) seq.Insert(slideStart, SiteSlideIn(L, slideMs, step.Ease));
                        }
                        seq.AppendInterval(Math.Max(0, step.Ms - seq.Duration));
                        Play(seq, time, instant, true);
                        // Q2 (6, 12): a burst of confetti in the segment's colours, a light camera shake, a strong buzz
                        var site = L.Board.Site;
                        p.Particles.Confetti(new Vec2((float)site.CenterX, (float)site.Y - 40f), (float)site.W + 160f, F.ParticleSegment, SegmentPalette(s, e.Seg),
                            F.ConfettiSegmentLifeMs, (float)site.H * 0.9f);
                        p.CameraShake(F.SegmentShakePx);
                        p.Buzz(HapticKind.Heavy);
                        return;
                    }
                case StepKind.TruckDelivery:
                case StepKind.QueueChip:
                    {
                        if (step.Drops == null) return;
                        bool fromChip = step.Kind == StepKind.QueueChip;
                        var seq = new Sequence();
                        foreach (var d in step.Drops)
                        {
                            var v = p.Pieces.Ensure(s, d.PieceId);
                            var shape = BoardPose.ShapeOf(s, d.PieceId);
                            var from = fromChip ? HudLayout.QueueChip(L) : BoardPose.Center(g, d.From, shape);
                            var to = BoardPose.Center(g, d.To, shape);
                            v.Visible = false;
                            Func<float, float> curve = fromChip ? (Func<float, float>)(u => (float)Easing.Evaluate(F.BounceToQueueEase, u)) : FallMotion.YardFallLeg(d.Rows, F).Progress;
                            seq.Insert(d.Delay, DropFromTo(v, from, to, d.Ms, curve, fromChip ? F.QueueShrink : 1f));
                            if (!fromChip && !p.Reduced) seq.Insert(d.Delay + d.Ms, LandSquash(v, shape, 1 + (F.LandSquashX - 1) / 2, 2 - (1 + (F.LandSquashX - 1) / 2), F.LandMs, F.LandEase));
                            var landed = Bottom(to, shape, c);
                            int dust = fromChip ? F.ParticleDustCascade : F.ParticleDustBlock; // JUICE #19: 4 dust per block
                            seq.InsertCallback(d.Delay + d.Ms, () => p.Particles.Dust(landed, dust));
                        }
                        if (fromChip) seq.Insert(0, Tweens.Pop(p.View.BlocksChip, 1f, F.QueueChipPeak, 1f, F.QueueChipMs));
                        else TruckRide(seq, step, L, s);
                        Play(seq, time, instant, true);
                        return;
                    }
                case StepKind.Win:
                    {
                        var seq = new Sequence();
                        foreach (int id in p.Pieces.Ids)
                        {
                            if (PieceZone(s, id) != Zone.Site) continue;
                            seq.Insert(0, FlashPulse(p.Pieces.Get(id)!, F.PlaceFlashPeak, F.WinGlowMs));
                        }
                        WinFx(seq, L);
                        seq.AppendInterval(Math.Max(0, step.Ms - seq.Duration));
                        Play(seq, time, instant, true);
                        return;
                    }
                case StepKind.BonusBuild:
                    BonusBuild(step, L, time, instant);
                    return;
                case StepKind.Teardown:
                    {
                        if (!(step.Event is TeardownEvent e)) return;
                        var seq = new Sequence();
                        for (int i = 0; i < e.Pieces.Length; i++)
                        {
                            int id = e.Pieces[i].PieceId;
                            var v = p.Pieces.Get(id);
                            if (v == null) continue;
                            float at = F.TeardownSettleMs + i * F.TeardownStaggerMs;
                            var shape = BoardPose.ShapeOf(s, id);
                            if (BoardPose.RestAnchor(s, id, out int ax, out int ay))
                            {
                                var home = BoardPose.Center(g, ax, ay, shape);
                                seq.Insert(at, Tweens.Move(v, home, F.TeardownMs, step.Ease, p.Reduced ? 0 : F.TeardownArcCells * c));
                                // JUICE #107 "inişte toz" (4 per block) — and a puff where it leaves the site
                                var landed = Bottom(home, shape, c);
                                seq.InsertCallback(at, () => p.Particles.Dust(Bottom(v.Position, shape, c), F.ParticleDustBlock));
                                seq.InsertCallback(at + F.TeardownMs, () => p.Particles.Dust(landed, F.ParticleDustBlock));
                            }
                            else seq.Insert(at, Tweens.Fade(v, 0f, F.TeardownMs));
                        }
                        seq.AppendInterval(Math.Max(0, step.Ms - seq.Duration));
                        Play(seq, time, instant, true);
                        return;
                    }
                case StepKind.TruckHelp:
                    {
                        if (!(step.Event is TruckHelpEvent e) || e.Moves == null) return;
                        foreach (var m in e.Moves)
                        {
                            var v = Piece(m.PieceId);
                            if (v == null) continue;
                            Play(Tweens.Move(v, BoardPose.Center(g, m.To, BoardPose.ShapeOf(s, m.PieceId)), step.Ms, step.Ease), time, instant, true);
                        }
                        return;
                    }
                case StepKind.CellsReveal:
                case StepKind.CrateHit:
                case StepKind.CrateBreak:
                case StepKind.BagTear:
                case StepKind.ChainBreak:
                case StepKind.GapUnlock:
                case StepKind.GapChange:
                case StepKind.WetTick:
                case StepKind.WetDry:
                case StepKind.ScrewCollect:
                case StepKind.KeyCollect:
                    p.RebuildBoard();
                    return;
                case StepKind.CarouselRotate:
                case StepKind.ElevatorMove:
                case StepKind.Resync:
                    p.SyncAll();
                    return;
                case StepKind.End:
                    p.PlanEnded();
                    return;
                default:
                    // Instant, StreakReset, StreakPip, TrowelEarned, BuildFront, SupportFlash, MortarStuck, Steer, GoalTick,
                    // GoalDone, YardClear, OutOfMoves, BoosterRejected: no placeholder visual yet; `End` re-reads the state
                    return;
            }
        }

        private IPieceVisual? Piece(int id) => id >= 0 ? p.Pieces.Get(id) : null;

        /// <summary>Starts `t` at the step's scheduled `time` (catching up the frame), or completes it at once.</summary>
        private void Play(Tweenable t, float time, bool instant, bool board)
        {
            t.Tag = board ? LevelPlayer.BoardTag : LevelPlayer.HudTag;
            if (instant)
            {
                t.Complete();
                return;
            }
            p.Clock.Play(t, Math.Max(0f, p.Clock.Now - time));
        }

        /// <summary>JUICE #11 squash: (sx, sy) at the start, back to rest scale 1 (the overshoot of Back.easeOut is the bounce).</summary>
        internal static Tween Squash(ITweenTarget v, float sx, float sy, float ms, EaseType ease)
        {
            var squashed = new Vec2(sx, sy);
            return new Tween(ms, (u, k) => v.Scale = u >= 1 ? Vec2.One : Vec2.Lerp(squashed, Vec2.One, k), () => v.Scale = squashed, ease);
        }

        /// <summary>0 → peak → 0 (sin π·u) of a float (flash, tint).</summary>
        internal static Tween Pulse01(Action<float> set, float peak, float ms) =>
            new Tween(ms, (u, k) => set(u >= 1 ? 0f : peak * (float)Math.Sin(Math.PI * u)), null, EaseType.Linear);

        internal static Tween FlashPulse(IPieceVisual v, float peak, float ms) => Pulse01(x => v.Flash = x, peak, ms);

        /// <summary>`count` white flashes in `ms` (JUICE #18 "yapı 2 kez parlar").</summary>
        internal static Tween Flashes(IPieceVisual v, float peak, float ms, int count) =>
            new Tween(ms, (u, k) =>
            {
                if (u >= 1)
                {
                    v.Flash = 0;
                    return;
                }
                double f = u * Math.Max(1, count);
                v.Flash = peak * (float)Math.Sin(Math.PI * (f - Math.Floor(f)));
            }, null, EaseType.Linear);

        /// <summary>A block that appears at `from` (truck bed, queue chip) and moves to `to` along `curve`, scale `fromScale` → 1.</summary>
        internal static Tween DropFromTo(IPieceVisual v, Vec2 from, Vec2 to, float ms, Func<float, float> curve, float fromScale)
        {
            var t = new Tween(ms, (u, k) =>
            {
                v.Position = u >= 1 ? to : Vec2.Lerp(from, to, k);
                float sc = u >= 1 ? 1f : fromScale + (1f - fromScale) * u;
                v.Scale = new Vec2(sc, sc);
            }, () =>
            {
                v.Visible = true;
                v.Alpha = 1;
                v.Position = from;
                v.Scale = new Vec2(fromScale, fromScale);
            });
            t.SetCurve(curve);
            return t;
        }
    }
}
