#nullable enable
// The home screen's 3D background in motion (paket B2; ART_3D §5.1; fx.json `layers`): the 7 Renkli Tepe layers drift sideways
// with time — offset = `HomeView.ParallaxAmpPx` × the layer's parallax factor × sin(2πt / `PeriodMs`) — so the far hills barely
// move, the town a little, the foreground most: depth without a device tilt sensor (B2 brief: "cihaz eğimi YOK"). The layers are
// built `2 × amplitude × factor` wider by `HomeView`, so no edge ever shows. A node is refreshed only when its offset moved by
// `StepPx` (a quarter pixel). Reduced motion: still.
using System;
using System.Collections.Generic;
using MinikUsta.Presentation.Ui;

namespace MinikUsta.Presentation.Anim
{
    public sealed class HomeParallax
    {
        private readonly struct Layer
        {
            public readonly UiNode Node;
            public readonly PxRect Rest;
            public readonly double Factor;

            public Layer(UiNode node, PxRect rest, double factor)
            {
                Node = node;
                Rest = rest;
                Factor = factor;
            }
        }

        private readonly List<Layer> layers = new List<Layer>();
        private IUiSurface? surface;
        private float time;

        /// <summary>One sway period (ms).</summary>
        public float PeriodMs { get; set; } = 16000f;

        /// <summary>Amplitude at parallax 1 (design px); `HomeView.ParallaxAmpPx` sized the layers for it.</summary>
        public double AmpPx { get; set; } = HomeView.ParallaxAmpPx;

        /// <summary>A node moves only when its offset changed by at least this much (design px).</summary>
        public double StepPx { get; set; } = 0.25;

        private bool reduced;

        /// <summary>Reduced motion: the layers go back to rest and stay there.</summary>
        public bool Reduced
        {
            get => reduced;
            set
            {
                if (reduced == value) return;
                reduced = value;
                Apply(true);
            }
        }

        /// <summary>Layer nodes bound (tests).</summary>
        public int Count => layers.Count;

        /// <summary>Refreshes written since the last bind (tests).</summary>
        public int Writes { get; private set; }

        /// <summary>The sideways offset of a layer of parallax `factor` at time `t` (ms).</summary>
        public double Offset(double factor, float t) => Reduced ? 0 : AmpPx * factor * Math.Sin(2 * Math.PI * t / Math.Max(1f, PeriodMs));

        /// <summary>Binds the layer nodes of a home tree (`HomeView.LayerPrefix` + layer name; factors from fx.json).</summary>
        public void Bind(IUiSurface s, UiNode home, IReadOnlyList<ParallaxLayer>? source)
        {
            layers.Clear();
            surface = s;
            Writes = 0;
            if (source == null) return;
            foreach (var l in source)
            {
                if (l.Parallax <= 0) continue;
                var n = home.Find(HomeView.LayerPrefix + l.Name);
                if (n != null) layers.Add(new Layer(n, n.Rect, l.Parallax));
            }
            Apply(true);
        }

        public void Unbind()
        {
            layers.Clear();
            surface = null;
        }

        public void Advance(float dtMs)
        {
            if (surface == null || layers.Count == 0 || !(dtMs > 0)) return;
            time += dtMs;
            Apply(false);
        }

        private void Apply(bool force)
        {
            var s = surface;
            if (s == null) return;
            foreach (var l in layers)
            {
                double dx = Offset(l.Factor, time);
                var want = new PxRect(l.Rest.X + dx, l.Rest.Y, l.Rest.W, l.Rest.H);
                if (!force && Math.Abs(want.X - l.Node.Rect.X) < StepPx) continue;
                l.Node.Rect = want;
                Writes++;
                s.Refresh(l.Node);
            }
        }
    }
}
