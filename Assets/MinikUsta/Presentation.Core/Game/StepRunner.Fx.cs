#nullable enable
// The game-feel layer of the step runner (Faz 3 Q2; the D-091 list): everything a step adds on top of its JUICE row's tween —
// the grow-and-lock pulse, the cell-by-cell landing wave, sprite sparks / dust / confetti, the streak beads and trowel pop,
// the delivery truck and the site slide, the win confetti, the Bonus İnşaat coins. Pure presentation: the state is already
// final (TECH §1.4); nothing here reads or changes a rule.
using System;
using System.Collections.Generic;
using MinikUsta.Core;
using MinikUsta.Presentation.Anim;
using MinikUsta.Presentation.Animation;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Audio;
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

        /// <summary>
        /// B2 / JUICE #11: the pre-rendered dust cloud (`fx_dust_puff`, pivot = its bottom centre) at a block's ground contact, as wide
        /// as `LandPuffScale` × the block, `delayMs` after the step start; `k` scales it (a yard drop is smaller).
        /// </summary>
        private void Puff(Vec2 bottom, ShapeDef shape, float cellPx, float delayMs, float k, bool instant)
        {
            if (instant) return;
            var clip = p.Art.Fx?.Clip("fx_dust_puff");
            if (clip == null || clip.CanvasW <= 0) return;
            float scale = (float)(shape.W * cellPx * F.LandPuffScale / clip.CanvasW) * k;
            p.Fx.Play("fx_dust_puff", bottom, FxDepth.Over, scale, Rgba.Hex("#F3EAD8"), delayMs);
        }

        /// <summary>
        /// B2 / JUICE #12, #92 "kilit parlaması": `fx_lock_shine` (60 fps, 200 ms) on the stud of every cell of a correctly placed
        /// block, `LockShineStaggerMs` apart from the bottom row to the top (cells of a row left to right).
        /// </summary>
        private void LockShine(IPieceVisual v, ShapeDef shape, float cellPx, bool instant)
        {
            if (instant) return;
            var order = new int[shape.Cells.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) =>
            {
                var ca = shape.Cells[a];
                var cb = shape.Cells[b];
                return ca.Y != cb.Y ? ca.Y.CompareTo(cb.Y) : ca.X.CompareTo(cb.X);
            });
            var center = v.Position;
            for (int i = 0; i < order.Length; i++)
            {
                var o = BoardPose.CellOffset(shape, shape.Cells[order[i]], cellPx);
                // the stud sits a little above the cell centre (the 24 px front lip below it, ART_3D §2.2)
                var at = new Vec2(center.X + o.X, center.Y + o.Y - 0.1f * cellPx);
                p.Fx.Play("fx_lock_shine", at, FxDepth.Over, F.LockShineScale * cellPx / 120f, null, i * F.LockShineStaggerMs);
            }
        }

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
            Snd(Sounds.StreakPip, 0, ComboPitch(n)); // JUICE #15 at the streak pitch
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
            // B2: the 3D Golden Trowel turns and glints over the HUD icon; the corner king celebrates the streak
            if (!instant) p.Fx.Play("fx_trowel_sparkle", HudLayout.Center(HudLayout.TrowelIcon(L)), FxDepth.Ui, F.TrowelFxScale, lifeMs: step.Ms + 300f, fadeMs: 200f);
            p.King?.Cue(KingCue.BigCheer);
            p.Buzz(HapticKind.Medium);
            Snd(Sounds.Combo, 0); // JUICE #16
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

        // --- segment slide (JUICE #18; the #19 truck is gone: D-099) ----------------------------------------------------------------

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
                Snd(Sounds.Coin, 0); // JUICE #56 reduced: one coin sound for the whole count
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
                Snd(Sounds.Coin, i * gap, SoundFeel.CoinRate(i)); // JUICE #56: +1 semitone every 3 coins
            }
            seq.InsertCallback(step.Ms, () => p.ShowMoves(0, danger: false));
            Play(seq, time, instant, false);
        }
    }
}
