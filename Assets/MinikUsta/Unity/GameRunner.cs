#nullable enable
// The frame loop of the Unity build (Faz 3 Q1; paket V1): builds the views (`UnityLevelView` world sprites, `UiRenderer` uGUI),
// the save store (PlayerPrefs) and the engine-independent `GameApp` (save, languages, screen flow, home, settings, the level
// player and its windows), boots it, and every frame: screen size → viewport, pointer → app, app.Tick(dt). Development builds
// get keyboard shortcuts (principle 8, first slice of the debug panel): N next level, R restart, A log the ASCII board
// (TECH Appendix A), S play the next move of the level's golden solution.
using MinikUsta.Core;
using MinikUsta.Presentation.Feel;
using MinikUsta.Presentation.Flow;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.I18n;
using MinikUsta.Presentation.Layout;
using MinikUsta.Presentation.Meta;
using UnityEngine;

namespace MinikUsta.Unity
{
    public sealed class GameRunner : MonoBehaviour
    {
        /// <summary>Frame time cap (ms): a frame after a pause / tab switch does not jump animations by seconds.</summary>
        private const float MaxFrameMs = 100f;

        private GameApp? app;
        private PointerInput? input;
        private int screenW;
        private int screenH;

        public GameApp? App => app;

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
            var feel = new FeelSettings();
            var i18n = new Localizer(ReadText);
            i18n.Text.Missing = key => Debug.LogWarning("Minik Usta: missing i18n key " + key);
            var ui = new UiRenderer(feel);
            var view = new UnityLevelView(cam, ui, i18n.Text, () => app?.Player.PausePressed(), layout, feel);
            var catalog = new LevelCatalog(n => ReadText(LevelCatalog.ResourceName(n)));
            // Q2 item 12: Handheld.Vibrate buzzes one fixed pulse and only Android is asked (the brief); everywhere else the sink is silent.
            // P3-01: Unity compiles the `Handheld` class only for mobile targets (Android, iOS). The runtime platform check above cannot
            // stand in for a compile-time guard: in the WebGL player the type name does not exist and the build fails with CS0103.
#if UNITY_ANDROID || UNITY_IOS
            var haptics = new FixedPulseHaptics(Application.platform == RuntimePlatform.Android, Handheld.Vibrate);
#else
            var haptics = new FixedPulseHaptics(false, () => { });
#endif
            string? economyText = ReadText("config/economy");
            app = new GameApp(view, ui, catalog, i18n, EconomyConfig.FromText(economyText), new PlayerPrefsStore(), feel, layout, haptics,
                EconomyInfo.FromText(economyText), message => Debug.LogWarning(message));
            input = new PointerInput();
            screenW = Screen.width;
            screenH = Screen.height;
            app.SetScreen(screenW, screenH);
            app.Boot();
        }

        private static string? ReadText(string resource)
        {
            var asset = Resources.Load<TextAsset>(resource);
            return asset != null ? asset.text : null;
        }

        private void Update()
        {
            var a = app;
            if (a == null || input == null) return;
            if (Screen.width != screenW || Screen.height != screenH)
            {
                screenW = Screen.width;
                screenH = Screen.height;
                a.SetScreen(screenW, screenH);
            }
            input.Poll(a, a.Player.Viewport);
            if (Debug.isDebugBuild) DebugKeys(a);
            a.Tick(Mathf.Min(Time.unscaledDeltaTime * 1000f, MaxFrameMs));
        }

        private void DebugKeys(GameApp a)
        {
            var p = a.Player;
            if (Input.GetKeyDown(KeyCode.N)) a.DebugStartLevel(LevelCatalog.Next(p.LevelNumber));
            if (Input.GetKeyDown(KeyCode.R)) a.DebugStartLevel(p.LevelNumber);
            if (Input.GetKeyDown(KeyCode.A) && p.Session != null) Debug.Log(Ascii.ToAscii(p.Session.State, true));
            if (Input.GetKeyDown(KeyCode.S)) DebugSolveStep(a);
        }

        /// <summary>Plays the next move of the level's canonical solution from the current state (dev builds; principle 8 "solver çözümünü oynat").</summary>
        private static void DebugSolveStep(GameApp a)
        {
            var p = a.Player;
            var session = p.Session;
            if (a.Flow.Current != AppScreen.Level || session == null || session.Outcome != BoosterOutcomes.Playing || p.Queue.Locked(p.Clock.Now)) return;
            var hint = DebugSolver.Next(session, DebugSolver.SolutionLog(ReadText("solutions/level_" + p.LevelNumber.ToString("000", System.Globalization.CultureInfo.InvariantCulture) + ".solver")));
            if (hint != null) p.Commit(hint);
        }
    }
}
