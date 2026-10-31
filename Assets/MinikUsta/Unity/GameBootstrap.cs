#nullable enable
// Entry point of the Unity build (Faz 3 Q1): the Level scene holds only the orthographic Main Camera; everything else —
// view, HUD, input, the engine-independent LevelPlayer — is built in code after the scene loads, so no scene or prefab
// references a script GUID (the scene YAML is hand-written; Unity generates the script .meta files on import).
using UnityEngine;

namespace MinikUsta.Unity
{
    /// <summary>Boots the game after the Level scene loads: the scene holds only the camera, everything else is built in code.</summary>
    public static class GameBootstrap
    {
        public const string SceneName = "Level";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != SceneName) return;
            var go = new GameObject("MinikUsta");
            go.AddComponent<GameRunner>();
        }
    }
}
