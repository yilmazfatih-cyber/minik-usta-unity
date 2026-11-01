#nullable enable
// A plain-field `IUiSurface` (the Unity UiRenderer stand-in): it keeps the tree of every layer, counts refreshes and lets a test
// tap a button by id (only an enabled, visible button fires — the uGUI Button's behaviour).
using System;
using System.Collections.Generic;
using MinikUsta.Presentation.Tweening;
using MinikUsta.Presentation.Ui;

namespace MinikUsta.Presentation.Tests.Fixtures
{
    public sealed class FakeSurface : IUiSurface
    {
        public readonly Dictionary<UiLayer, UiNode?> Roots = new Dictionary<UiLayer, UiNode?>();
        public readonly Dictionary<UiLayer, Action<UiNode>?> Taps = new Dictionary<UiLayer, Action<UiNode>?>();
        public readonly Dictionary<UiLayer, bool> LayerVisible = new Dictionary<UiLayer, bool>();
        private readonly Dictionary<UiNode, PlainTarget> targets = new Dictionary<UiNode, PlainTarget>();
        public int Shows;
        public int Refreshes;

        public void Show(UiLayer layer, UiNode? root, Action<UiNode>? onTap)
        {
            Roots[layer] = root;
            Taps[layer] = onTap;
            if (!LayerVisible.ContainsKey(layer)) LayerVisible[layer] = true;
            Shows++;
        }

        public void Refresh(UiNode node) => Refreshes++;

        public ITweenTarget Target(UiNode node)
        {
            if (!targets.TryGetValue(node, out var t))
            {
                t = new PlainTarget();
                targets[node] = t;
            }
            return t;
        }

        public void SetLayerVisible(UiLayer layer, bool visible) => LayerVisible[layer] = visible;

        public UiNode? Root(UiLayer layer) => Roots.TryGetValue(layer, out var r) ? r : null;

        public bool Visible(UiLayer layer) => Root(layer) != null && (!LayerVisible.TryGetValue(layer, out bool v) || v);

        /// <summary>The top-most visible layer holding `id` (Overlay → Fx → Window → Hud → Home).</summary>
        public (UiLayer, UiNode)? Locate(string id)
        {
            for (int l = (int)UiLayer.Overlay; l >= 0; l--)
            {
                var layer = (UiLayer)l;
                if (!Visible(layer)) continue;
                var n = Root(layer)!.Find(id);
                if (n != null) return (layer, n);
            }
            return null;
        }

        /// <summary>Taps the button `id`; false when it is missing, hidden or disabled (nothing happens, like uGUI).</summary>
        public bool Tap(string id)
        {
            var hit = Locate(id);
            if (hit == null) return false;
            var (layer, n) = hit.Value;
            if (!n.Shown || !n.Enabled || n.Action == null) return false;
            Taps[layer]?.Invoke(n);
            return true;
        }
    }
}
