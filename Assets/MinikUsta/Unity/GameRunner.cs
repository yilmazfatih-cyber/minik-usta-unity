#nullable enable
// The frame loop of the Unity build (Faz 3 Q1): builds the view and the engine-independent `LevelPlayer`, loads level 1
// from Resources (D-096: levels as TextAsset, WebGL-safe) and every frame: screen size → viewport, pointer → player,
// player.Tick(dt). Development builds get keyboard shortcuts (principle 8, first slice of the debug panel):
// N next level, R restart, A log the ASCII board (TECH Appendix A), S play the next move of the level's golden solution.
using MinikUsta.Core;
using MinikUsta.Presentation.Feel;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Layout;
using MinikUsta.Presentation.Text;
using UnityEngine;

namespace MinikUsta.Unity
{
    public sealed class GameRunner : MonoBehaviour
    {
        /// <summary>Frame time cap (ms): a frame after a pause / tab switch does not jump animations by seconds.</summary>
        private const float MaxFrameMs = 100f;

        private LevelPlayer? player;
        private PointerInput? input;
        private int screenW;
        private int screenH;

        public LevelPlayer? Player => player;

        private void Awake()
        {
            if (Application.platform != RuntimePlatform.WebGLPlayer) Application.targetFrameRate = 60;
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                cam = go.AddComponent<Camera>();
            }
            var layout = new LayoutSettings();
            var view = new UnityLevelView(cam, () => player?.PausePressed(), layout);
            var strings = Strings.FromText(ReadText("i18n/tr"));
            var catalog = new LevelCatalog(n => ReadText(LevelCatalog.ResourceName(n)));
            player = new LevelPlayer(view, catalog, strings, new FeelSettings(), layout);
            input = new PointerInput();
            screenW = Screen.width;
            screenH = Screen.height;
            player.SetScreen(screenW, screenH);
            if (!player.Load(LevelCatalog.First)) Debug.LogError("Minik Usta: " + player.LoadError);
        }

        private static string? ReadText(string resource)
        {
            var asset = Resources.Load<TextAsset>(resource);
            return asset != null ? asset.text : null;
        }

        private void Update()
        {
            var p = player;
            if (p == null || input == null) return;
            if (Screen.width != screenW || Screen.height != screenH)
            {
                screenW = Screen.width;
                screenH = Screen.height;
                p.SetScreen(screenW, screenH);
            }
            input.Poll(p, p.Viewport);
            if (Debug.isDebugBuild) DebugKeys(p);
            p.Tick(Mathf.Min(Time.unscaledDeltaTime * 1000f, MaxFrameMs));
        }

        private void DebugKeys(LevelPlayer p)
        {
            if (Input.GetKeyDown(KeyCode.N)) p.Next();
            if (Input.GetKeyDown(KeyCode.R)) p.Restart();
            if (Input.GetKeyDown(KeyCode.A) && p.Session != null) Debug.Log(Ascii.ToAscii(p.Session.State, true));
            if (Input.GetKeyDown(KeyCode.S)) DebugSolveStep(p);
        }

        /// <summary>Plays the next move of the level's canonical solution from the current state (dev builds; principle 8 "solver çözümünü oynat").</summary>
        private static void DebugSolveStep(LevelPlayer p)
        {
            var session = p.Session;
            if (session == null || session.Outcome != BoosterOutcomes.Playing || p.Queue.Locked(p.Clock.Now)) return;
            var hint = DebugSolver.Next(session, DebugSolver.SolutionLog(ReadText("solutions/level_" + p.LevelNumber.ToString("000", System.Globalization.CultureInfo.InvariantCulture) + ".solver")));
            if (hint != null) p.Commit(hint);
        }
    }
}
