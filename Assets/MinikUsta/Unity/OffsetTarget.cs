#nullable enable
// A Transform's offset as a tween target (Faz 3 Q2): the camera rig of the shakes and the site layer of the segment slide.
// `Position` is the offset in design px (y down); the Transform sits at `home` + offset in world units (y up). Scale, rotation
// and alpha are stored only (nothing tweens them).
using MinikUsta.Presentation;
using MinikUsta.Presentation.Tweening;
using UnityEngine;

namespace MinikUsta.Unity
{
    public sealed class OffsetTarget : ITweenTarget
    {
        private readonly Transform target;
        private readonly Vector3 home;
        private Vec2 position;

        public OffsetTarget(Transform target)
        {
            this.target = target;
            home = target.localPosition;
        }

        public Vec2 Position
        {
            get => position;
            set
            {
                position = value;
                target.localPosition = new Vector3(home.x + value.X / DesignSpace.PixelsPerUnit, home.y - value.Y / DesignSpace.PixelsPerUnit, home.z);
            }
        }

        public Vec2 Scale { get; set; } = Vec2.One;

        public float Rotation { get; set; }

        public float Alpha { get; set; } = 1f;
    }
}
