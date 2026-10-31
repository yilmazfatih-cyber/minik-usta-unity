#nullable enable
// The game-feel layer of the step runner (Faz 3 Q2; the D-091 list): everything a step adds on top of its JUICE row's tween —
// the grow-and-lock pulse, the cell-by-cell landing wave, sprite sparks / dust / confetti, the streak beads and trowel pop,
// the delivery truck and the site slide, the win confetti, the Bonus İnşaat coins. Pure presentation: the state is already
// final (TECH §1.4); nothing here reads or changes a rule.
using System;
using System.Collections.Generic;
using MinikUsta.Core;
using MinikUsta.Presentation.Animation;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Feel;
using MinikUsta.Presentation.Layout;
using MinikUsta.Presentation.Tweening;
using static MinikUsta.Core.State;

namespace MinikUsta.Presentation.Game
{
    internal sealed partial class StepRunner
    {
        private static Rgba[]? winPalette;

        /// <summary>Confetti colours of the win (JUICE #55 "palet renkleri"): the eight block colours, gold and white.</summary>
        internal static Rgba[] WinPalette
        {
            get
            {
                if (winPalette != null) return winPalette;
                var o = new List<Rgba>();
                for (int i = 0; i < Palette.BlockHex.Length; i++) o.Add(Palette.Block(i));
                o.Add(Rgba.Hex(Palette.Gold));
                o.Add(Rgba.White);
                return winPalette = o.ToArray();
            }
        }

        // --- small helpers -----------------------------------------------------------------------------------------------------

        /// <summary>Q2 (1): a released block settles to scale 1 with `ReleaseScaleEase` (Back.easeOut); reduced motion keeps the row's own ease.</summary>
        private EaseType ReleaseEase(EaseType rowEase) => p.Reduced ? rowEase : F.ReleaseScaleEase;

        /// <summary>The bottom-centre of a block box (where its dust rises).</summary>
        private static Vec2 Bottom(Vec2 center, ShapeDef shape, float cellPx) => new Vec2(center.X, center.Y + shape.H * cellPx / 2f);

        /// <summary>Runs `action` `delayMs` after the step's start (board motion: a grab completes it, R-12).</summary>
        private void After(float delayMs, Action action, float time, bool instant)
        {
            var seq = new Sequence();
            if (delayMs > 0) seq.AppendInterval(delayMs);
            seq.AppendCallback(action);
            Play(seq, time, instant, true);
        }

        private void DustAfter(float delayMs, Vec2 at, int count, float time, bool instant) =>
            After(delayMs, () => p.Particles.Dust(at, count), time, instant);

        /// <summary>Q2 (4): `Grow` 1 → `peak` (at `split` of the time) → 1: the block swells and shrinks into its lock.</summary>
        internal static Tween GrowPulse(IPieceVisual v, float peak, float ms, float split, EaseType ease) =>
            new Tween(ms, (u, k) => v.Grow = u >= 1f ? 1f : Tweens.PopCurve(k, 1f, peak, 1f, split), null, ease);

        /// <summary>
        /// Q2 (3): the landing squash. A block of several cells squashes cell by cell, `CellStaggerMs` apart (bottom-left cell first,
        /// the wave running up and to the right), each cell finishing with the beat so the plan's timing is unchanged; a single
        /// cell, reduced motion or a zero stagger squashes the block as one (the Q1 look).
        /// </summary>
        internal Tweenable LandSquash(IPieceVisual v, ShapeDef shape, float sx, float sy, float ms, EaseType ease)
        {
            if (p.Reduced || F.CellStaggerMs <= 0 || v.CellCount <= 1 || shape.Cells.Length != v.CellCount) return Squash(v, sx, sy, ms, ease);
            var seq = new Sequence();
            var ranks = CellWave.Ranks(shape);
            for (int i = 0; i < ranks.Length; i++)
            {
                float delay = CellWave.DelayMs(ranks[i], F, ms);
                seq.Insert(delay, CellSquash(v, i, sx, sy, ms - delay, ease));
            }
            return seq;
        }

        private static Tween CellSquash(IPieceVisual v, int cell, float sx, float sy, float ms, EaseType ease) =>
            new Tween(ms, (u, k) =>
            {
                if (u >= 1f) v.SetCellScale(cell, 1f, 1f);
                else v.SetCellScale(cell, sx + (1f - sx) * k, sy + (1f - sy) * k);
            }, () => v.SetCellScale(cell, sx, sy), ease);

        // --- streak beads and the Golden Trowel (JUICE #15, #16, #107) ---------------------------------------------------------

        private void StreakPip(AnimationStep step, ScreenLayout L, float time, bool instant)
        {
            int n = step.N ?? 0;
            if (n <= 0) return;
            int index = Math.Min(n, Combo.ComboForTrowel) - 1;
            p.ShowStreak(index + 1, p.ShownTrowels);
            // JUICE #15: the bead 0,6 → 1,2 → 1,0 with four small gold sparks
            Play(Tweens.Pop(p.View.StreakBead(index), F.PipFrom, F.PipPeak, 1f, step.Ms), time, instant, false);
            p.Particles.Sparks(HudLayout.Center(HudLayout.Bead(L, index)), Rgba.Hex(Palette.Gold), F.ParticlePip, 0.35f);
        }

        private void TrowelEarned(AnimationStep step, ScreenLayout L, float time, bool instant)
        {
            int total = step.N ?? p.ShownTrowels + 1;
            p.ShowStreak(0, total); // the four beads empty into the trowel
            var icon = p.View.TrowelIcon;
            // JUICE #16: the trowel grows 1,0 → 1,4 → 1,1 and turns (Elastic.easeOut), a rain of gold sparkles, a medium buzz
            Play(Tweens.Pop(icon, 1f, F.ComboIconPeak, F.ComboIconRest, step.Ms, 0.35f), time, instant, false);
            if (!p.Reduced)
            {
                var spin = Tweens.Rotate(icon, F.ComboSpinDeg, step.Ms, F.ComboEase);
                spin.OnComplete(() => icon.Rotation = 0f);
                Play(spin, time, instant, false);
            }
            p.Particles.GoldRain(HudLayout.Center(HudLayout.TrowelIcon(L)), F.ParticleCombo);
            p.Buzz(HapticKind.Medium);
        }

        private void StreakReset(AnimationStep step, float time, bool instant)
        {
            int shown = p.ShownBeads;
            if (shown <= 0) return;
            if (p.Reduced || instant)
            {
                p.ShowStreak(0, p.ShownTrowels);
                return;
            }
            // JUICE #107: the beads go out in 200 ms
            float half = F.StreakResetMs / 2f;
            var seq = new Sequence();
            for (int i = 0; i < shown && i < Combo.ComboForTrowel; i++)
            {
                var bead = p.View.StreakBead(i);
                seq.Insert(0, Tweens.Scale(bead, F.PipFrom, half, EaseType.InQuad));
                seq.Insert(half, Tweens.Scale(bead, 1f, half, EaseType.OutQuad));
            }
            seq.InsertCallback(half, () => p.ShowStreak(0, p.ShownTrowels));
            Play(seq, time, instant, false);
        }

        // --- segment slide, truck (JUICE #18, #19) -----------------------------------------------------------------------------

        /// <summary>JUICE #18: the next segment's site enters from the right edge of the screen (the layer returns to rest).</summary>
        private Tween SiteSlideIn(ScreenLayout L, float ms, EaseType ease)
        {
            var layer = p.View.SiteLayer;
            float off = (float)(L.W - L.Grid.BuildX + 24);
            return new Tween(ms, (u, k) => layer.Position = u >= 1f ? Vec2.Zero : new Vec2(off * (1f - k), 0f), () => layer.Position = new Vec2(off, 0f), ease);
        }

        /// <summary>The colours of the blocks of segment `seg` on the site (the confetti of its completion); every block colour when it has none.</summary>
        private List<Rgba> SegmentPalette(GameState s, int seg)
        {
            var o = new List<Rgba>();
            var seen = new HashSet<int>();
            foreach (int id in p.Pieces.Ids)
            {
                if (PieceZone(s, id) != Zone.Site || PieceSeg(s, id) != seg) continue;
                int color = PieceColor(s, id);
                if (seen.Add(color)) o.Add(Palette.Block(color));
            }
            if (o.Count == 0) o.AddRange(WinPalette);
            return o;
        }

        /// <summary>
        /// JUICE #19: the truck drives in from the right (`TruckEnterMs`, Back.easeOut), tips its bed while the blocks drop from the
        /// columns, and drives off to the left (`TruckExitMs`, Quad.easeIn) so the whole ride is `TruckMs` (700). Reduced motion:
        /// it just appears and disappears ("kamyon görünür/kaybolur").
        /// </summary>
        private void TruckRide(Sequence seq, AnimationStep step, ScreenLayout L, GameState s)
        {
            var drops = step.Drops;
            if (drops == null || drops.Length == 0) return;
            var g = L.Grid;
            var truck = p.View.Truck;
            float c = (float)g.CellPx;
            float width = TruckModel.WidthCells * c;
            float sumX = 0, topY = float.MaxValue;
            foreach (var d in drops)
            {
                var from = BoardPose.Center(g, d.From, BoardPose.ShapeOf(s, d.PieceId));
                sumX += from.X;
                topY = Math.Min(topY, from.Y);
            }
            float x = Math.Max(width / 2f + 8f, Math.Min((float)L.W - width / 2f - 8f, sumX / drops.Length));
            float y = topY + 0.15f * c;
            var stand = new Vec2(x, y);
            seq.InsertCallback(0, () =>
            {
                truck.Visible = true;
                truck.Scale = Vec2.One;
                truck.Alpha = 1f;
                truck.Rotation = 0f;
                truck.Position = p.Reduced ? stand : new Vec2((float)L.W + width / 2f + 20f, y);
            });
            if (p.Reduced)
            {
                seq.InsertCallback(F.ReducedFadeMs, () => truck.Visible = false);
                return;
            }
            float enter = Math.Min(F.TruckEnterMs, step.Ms);
            float exitAt = Math.Max(enter, step.Ms - F.TruckExitMs);
            float tilt = Math.Min(F.TruckBedTiltMs, Math.Max(0f, exitAt - enter));
            seq.Insert(0, Tweens.Move(truck, stand, enter, F.TruckEnterEase));
            if (tilt > 0)
            {
                seq.Insert(enter, Tweens.Rotate(truck, F.TruckBedTiltDeg, tilt, EaseType.OutQuad));
                seq.Insert(Math.Max(enter + tilt, exitAt - tilt), Tweens.Rotate(truck, 0f, tilt, EaseType.InQuad));
            }
            seq.Insert(exitAt, Tweens.Move(truck, new Vec2(-width / 2f - 20f, y), Math.Max(0f, step.Ms - exitAt), F.TruckExitEase));
            seq.InsertCallback(step.Ms, () => truck.Visible = false);
            p.Buzz(HapticKind.Light);
        }

        // --- win (JUICE #55, #56) ------------------------------------------------------------------------------------------------

        /// <summary>
        /// JUICE #55: after the glow (400 ms) and the ribbon (600 ms) the confetti falls in `ParticleWinWaves` waves of
        /// `ParticleWin`; the camera shakes lightly and the victory pattern buzzes (Q2 item 12).
        /// </summary>
        private void WinFx(Sequence seq, ScreenLayout L)
        {
            float at = F.WinGlowMs + F.WinRibbonMs;
            seq.InsertCallback(at, () =>
            {
                p.CameraShake(F.WinShakePx);
                p.Buzz(HapticKind.Win);
            });
            for (int w = 0; w < F.ParticleWinWaves; w++)
                seq.InsertCallback(at + w * F.WinConfettiWaveGapMs,
                    () => p.Particles.Confetti(new Vec2((float)L.W / 2f, -30f), (float)L.W, F.ParticleWin, WinPalette, F.WinConfettiMs, (float)L.H * 0.75f));
        }

        /// <summary>
        /// JUICE #56 Bonus İnşaat: every left-over move (at most `bonusMaxMovesCounted`) turns into a gold coin: the moves plate counts
        /// down, a coin arcs to the gold chip, the chip pops and counts up (`bonusCoinsPerMoveLeft` each), four gold sparks at the
        /// chip. The moves beyond the counted ones just go out at the end. Reduced motion: the total is counted at once.
        /// </summary>
        private void BonusBuild(AnimationStep step, ScreenLayout L, float time, bool instant)
        {
            int n = step.N ?? 0;
            if (n <= 0) return;
            int counted = Math.Min(n, Math.Max(1, p.Economy.BonusMaxMovesCounted));
            int perCoin = p.Economy.BonusCoinsPerMoveLeft;
            p.ShowGold(0);
            var seq = new Sequence();
            if (p.Reduced)
            {
                seq.AppendInterval(step.Ms);
                seq.AppendCallback(() =>
                {
                    p.ShowMoves(0, danger: false);
                    p.ShowGold(counted * perCoin);
                });
                Play(seq, time, instant, false);
                return;
            }
            var from = HudLayout.Center(HudLayout.Moves(L));
            var chip = HudLayout.Center(HudLayout.GoldChip(L));
            float gap = step.Ms / counted;
            for (int i = 0; i < counted; i++)
            {
                int plate = n - (i + 1);
                seq.InsertCallback(i * gap, () =>
                {
                    p.ShowMoves(plate, danger: false);
                    p.FlyCoin(from, chip, perCoin);
                });
            }
            seq.InsertCallback(step.Ms, () => p.ShowMoves(0, danger: false));
            Play(seq, time, instant, false);
        }
    }
}
