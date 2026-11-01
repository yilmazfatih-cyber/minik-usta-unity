#nullable enable
// The seam between the UI trees and the engine (paket V1): the Unity layer implements it with uGUI (`UiRenderer`), the tests
// with plain fields (`FakeSurface`). Nothing here decides anything: screens build trees, the surface shows them and reports taps.
using System;
using MinikUsta.Presentation.Tweening;

namespace MinikUsta.Presentation.Ui
{
    public interface IUiSurface
    {
        /// <summary>Shows `root` on `layer` (replacing what was there; null clears the layer). Taps on enabled buttons call `onTap`.</summary>
        void Show(UiLayer layer, UiNode? root, Action<UiNode>? onTap);

        /// <summary>Re-applies the properties of a shown node (rect, frame, tint, alpha, text, visibility, enabled); not its children list.</summary>
        void Refresh(UiNode node);

        /// <summary>A tween target on a shown node: `Scale` and `Rotation` about its centre.</summary>
        ITweenTarget Target(UiNode node);

        /// <summary>Shows / hides a whole layer without rebuilding it (the HUD while the home screen is up).</summary>
        void SetLayerVisible(UiLayer layer, bool visible);
    }

    /// <summary>A tween target that forwards to another one (the open window changes, the reference handed to the player stays).</summary>
    public sealed class ProxyTarget : ITweenTarget
    {
        private readonly PlainTarget fallback = new PlainTarget();

        public ITweenTarget? Inner { get; set; }

        private ITweenTarget T => Inner ?? fallback;

        public Vec2 Position
        {
            get => T.Position;
            set => T.Position = value;
        }

        public Vec2 Scale
        {
            get => T.Scale;
            set => T.Scale = value;
        }

        public float Rotation
        {
            get => T.Rotation;
            set => T.Rotation = value;
        }

        public float Alpha
        {
            get => T.Alpha;
            set => T.Alpha = value;
        }
    }

    /// <summary>A target that only stores its values.</summary>
    public sealed class PlainTarget : ITweenTarget
    {
        public Vec2 Position { get; set; }
        public Vec2 Scale { get; set; } = Vec2.One;
        public float Rotation { get; set; }
        public float Alpha { get; set; } = 1f;
    }
}
