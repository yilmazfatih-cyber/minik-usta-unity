#nullable enable
// A plain-field `IUiSurface` (the Unity UiRenderer stand-in): it keeps the tree of every layer, counts refreshes and lets a test
// tap a button by id (only an enabled, visible button fires — the uGUI Button's behaviour).
// C1: like UiRenderer, every `Show` of a layer DESTROYS the objects of its previous tree, even when the same root is shown again: a
// tween target taken from `Target(node)` before that Show throws when it is used afterwards (Unity: the engine's
// NullReferenceException on a destroyed RectTransform). A HUD that forgets to rebind a target fails here instead of freezing a level.
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
        private readonly Dictionary<UiNode, SurfaceTarget> targets = new Dictionary<UiNode, SurfaceTarget>();
        private readonly Dictionary<UiLayer, int> generation = new Dictionary<UiLayer, int>();
        public int Shows;
        public int Refreshes;

        public void Show(UiLayer layer, UiNode? root, Action<UiNode>? onTap)
        {
            // the previous tree's objects are gone (UiRenderer.Show destroys every child of the layer)
            generation[layer] = Gen(layer) + 1;
            var dead = new List<UiNode>();
            foreach (var kv in targets) if (kv.Value.Layer == layer) dead.Add(kv.Key);
            foreach (var n in dead)
            {
                targets[n].Destroyed = true;
                targets.Remove(n);
            }
            Roots[layer] = root;
            Taps[layer] = onTap;
            if (!LayerVisible.ContainsKey(layer)) LayerVisible[layer] = true;
            Shows++;
        }

        private int Gen(UiLayer layer) => generation.TryGetValue(layer, out int g) ? g : 0;

        /// <summary>How many times `layer` was shown (built or rebuilt).</summary>
        public int ShowsOf(UiLayer layer) => Gen(layer);

        public void Refresh(UiNode node) => Refreshes++;

        public ITweenTarget Target(UiNode node)
        {
            var layer = LayerOf(node);
            // UiRenderer: a node that is not on screen gets a throwaway plain target
            if (layer == null) return new PlainTarget();
            if (!targets.TryGetValue(node, out var t))
            {
                t = new SurfaceTarget(node.Id, layer.Value);
                targets[node] = t;
            }
            return t;
        }

        /// <summary>The layer whose current tree holds `node` (null when it is in none).</summary>
        private UiLayer? LayerOf(UiNode node)
        {
            var top = node;
            while (top.Parent != null) top = top.Parent;
            foreach (var kv in Roots) if (kv.Value == top) return kv.Key;
            return null;
        }

        /// <summary>A RectTransform stand-in: usable until the next Show of its layer, then every access throws.</summary>
        public sealed class SurfaceTarget : ITweenTarget
        {
            private readonly string id;
            private Vec2 position;
            private Vec2 scale = Vec2.One;
            private float rotation;
            private float alpha = 1f;

            public SurfaceTarget(string id, UiLayer layer)
            {
                this.id = id;
                Layer = layer;
            }

            public UiLayer Layer { get; }
            public bool Destroyed { get; set; }

            private void Alive()
            {
                if (Destroyed) throw new InvalidOperationException("the tween target of UI node '" + id + "' was destroyed by a later Show of layer " + Layer + " (Unity: NullReferenceException on a destroyed RectTransform)");
            }

            public Vec2 Position
            {
                get { Alive(); return position; }
                set { Alive(); position = value; }
            }

            public Vec2 Scale
            {
                get { Alive(); return scale; }
                set { Alive(); scale = value; }
            }

            public float Rotation
            {
                get { Alive(); return rotation; }
                set { Alive(); rotation = value; }
            }

            public float Alpha
            {
                get { Alive(); return alpha; }
                set { Alive(); alpha = value; }
            }
        }

        public void SetLayerVisible(UiLayer layer, bool visible) => LayerVisible[layer] = visible;

        public UiNode? Root(UiLayer layer) => Roots.TryGetValue(layer, out var r) ? r : null;

        public bool Visible(UiLayer layer) => Root(layer) != null && (!LayerVisible.TryGetValue(layer, out bool v) || v);

        /// <summary>The top-most visible layer holding `id` (Overlay → Fx → Window → Hud → Home).</summary>
        public (UiLayer, UiNode)? Locate(string id)
        {
            for (int l = (int)UiLayer.Hint; l >= 0; l--)
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
