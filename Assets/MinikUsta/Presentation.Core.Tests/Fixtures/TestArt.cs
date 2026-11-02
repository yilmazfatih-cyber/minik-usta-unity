#nullable enable
// B2: the shipped 3D libraries (Resources/art3d/king.json, fx.json) for the presentation tests, read once.
using MinikUsta.Presentation.Anim;

namespace MinikUsta.Presentation.Tests.Fixtures
{
    public static class TestArt
    {
        private static ArtSet? set;

        public static ArtSet Set => set ??= ArtSet.Load(PresentationPaths.ResourceText);

        public static ClipLibrary King => Set.King!;
        public static ClipLibrary Fx => Set.Fx!;

        /// <summary>A frame name the Unity build can draw: the v4 atlas, a 3D library frame or alias.</summary>
        public static bool Has(string name) => ArtContractTests.Atlas.Has(name) || Set.Has(name);
    }
}
