#nullable enable
// A timeline of tweens and callbacks (D-096; the DOTween Sequence verbs Append / Join / Insert / AppendInterval /
// AppendCallback). Deterministic: a time step that jumps over several item boundaries (a long frame, a fast-forward)
// is split at those boundaries, so starts, completions and callbacks always run in time order.
using System;
using System.Collections.Generic;

namespace MinikUsta.Presentation.Tweening
{
    public sealed class Sequence : Tweenable
    {
        private sealed class Item
        {
            public readonly float At;
            public readonly int Seq;
            public readonly Tweenable? Child;
            public readonly Action? Callback;
            public bool Fired;

            public Item(float at, int seq, Tweenable? child, Action? callback)
            {
                At = at;
                Seq = seq;
                Child = child;
                Callback = callback;
            }
        }

        private readonly List<Item> items = new List<Item>();
        private float duration;
        private float lastAt;
        private int seq;
        private float cursor;
        private float[]? boundaries;

        /// <summary>End of the last item (ms).</summary>
        public override float Duration => duration;

        /// <summary>Number of children and callbacks.</summary>
        public int Count => items.Count;

        /// <summary>Adds `t` at the end of the sequence (after everything added so far).</summary>
        public Sequence Append(Tweenable t)
        {
            float at = duration;
            Add(at, t, null);
            lastAt = at;
            return this;
        }

        /// <summary>Adds `t` at the start of the last appended / inserted item, interval or callback (they play together).</summary>
        public Sequence Join(Tweenable t)
        {
            Add(lastAt, t, null);
            return this;
        }

        /// <summary>Adds `t` at `atMs` from the sequence start.</summary>
        public Sequence Insert(float atMs, Tweenable t)
        {
            Add(atMs, t, null);
            lastAt = atMs;
            return this;
        }

        /// <summary>Waits `ms` at the end.</summary>
        public Sequence AppendInterval(float ms)
        {
            AssertNotStarted("AppendInterval");
            if (!(ms >= 0)) throw new ArgumentOutOfRangeException(nameof(ms), "AppendInterval: ms must be ≥ 0");
            lastAt = duration;
            duration += ms;
            boundaries = null;
            return this;
        }

        /// <summary>Calls `action` at the current end.</summary>
        public Sequence AppendCallback(Action action)
        {
            float at = duration;
            Add(at, null, action);
            lastAt = at;
            return this;
        }

        /// <summary>Calls `action` at `atMs`.</summary>
        public Sequence InsertCallback(float atMs, Action action)
        {
            Add(atMs, null, action);
            lastAt = atMs;
            return this;
        }

        private void Add(float at, Tweenable? child, Action? callback)
        {
            AssertNotStarted("Sequence");
            if (!(at >= 0)) throw new ArgumentOutOfRangeException(nameof(at), "Sequence: the position must be ≥ 0");
            if (child == null && callback == null) throw new ArgumentNullException(nameof(child));
            if (child != null && (child.IsStarted || child.Elapsed > 0)) throw new InvalidOperationException("Sequence: the child already started");
            var item = new Item(at, seq++, child, callback);
            // keep the items sorted by (At, Seq): an item runs after every earlier item and after items added before it at the same time
            int i = items.Count;
            while (i > 0 && items[i - 1].At > at) i--;
            items.Insert(i, item);
            float end = child != null ? at + child.Span : at;
            if (end > duration) duration = end;
            boundaries = null;
        }

        protected override void Begin()
        {
            cursor = 0f;
        }

        protected override void Apply(float active)
        {
            var b = boundaries ??= Boundaries();
            // every boundary strictly between the last step and this one first, in order (a long frame or a fast-forward)
            for (int i = 0; i < b.Length; i++)
            {
                float t = b[i];
                if (t <= cursor) continue;
                if (t >= active) break;
                Step(t);
                if (!IsActive) return;
            }
            Step(active);
            cursor = active;
        }

        private void Step(float t)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.At > t) break;
                if (item.Child != null) item.Child.Goto(t - item.At);
                else if (!item.Fired)
                {
                    item.Fired = true;
                    item.Callback!.Invoke();
                }
                if (!IsActive) return;
            }
        }

        private float[] Boundaries()
        {
            var set = new SortedSet<float>();
            foreach (var item in items)
            {
                if (item.Child != null) item.Child.CollectBoundaries(set, item.At);
                else set.Add(item.At);
            }
            var o = new float[set.Count];
            set.CopyTo(o);
            return o;
        }

        internal override void CollectBoundaries(SortedSet<float> set, float offset)
        {
            base.CollectBoundaries(set, offset);
            float o = offset + Delay;
            foreach (var item in items)
            {
                if (item.Child != null) item.Child.CollectBoundaries(set, o + item.At);
                else set.Add(o + item.At);
            }
        }
    }
}
