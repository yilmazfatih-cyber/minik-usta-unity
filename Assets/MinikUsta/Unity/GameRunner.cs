#nullable enable
// The frame loop of the Unity build (Faz 3 Q1; paket V1): builds the views (`UnityLevelView` world sprites, `UiRenderer` uGUI),
// the save store (PlayerPrefs) and the engine-independent `GameApp` (save, languages, screen flow, home, settings, the level
// player and its windows), boots it, and every frame: screen size → viewport, pointer → app, app.Tick(dt). Development builds
// get keyboard shortcuts (principle 8, first slice of the debug panel): N next level, R restart, A log the ASCII board
// (TECH Appendix A), S play the next move of the level's golden solution.
using MinikUsta.Core;
using MinikUsta.Presentation.Anim;
using MinikUsta.Presentation.Audio;
using MinikUsta.Presentation.Feel;
using MinikUsta.Presentation.Flow;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.I18n;
using MinikUsta.Presentation.Layout;
using MinikUsta.Presentation.Meta;
using MinikUsta.Presentation.Save;
using MinikUsta.Presentation.Shots;
using UnityEngine;

namespace MinikUsta.Unity
{
    public sealed class GameRunner : MonoBehaviour
    {
        /// <summary>Frame time cap (ms): a frame after a pause / tab switch does not jump animations by seconds.</summary>
        private const float MaxFrameMs = 100f;

        private GameApp? app;
        private UiRenderer? ui;
        private PointerInput? input;
        private ShotScenario? shots;
        private ArtWarmup? warmup;
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
            // B2: the 3D clip libraries (Usta Kral, FX, home layers); pages decode on first use (ClipSprites)
            var art = ClipSprites.Load();
            // P4B-05: the king pages decode now, before the first frame (a 160 ms hitch on the first home frame otherwise); the fx
            // pages decode when the player leaves the home screen (ArtWarmup.Tick), never at the first correct placement
            warmup = new ArtWarmup(ClipSprites.Warm);
            warmup.WarmKing();
            var layout = new LayoutSettings();
            var urlMode = ScreenFit.ModeFromUrl(Application.absoluteURL);
            if (urlMode.HasValue) layout.Mode = urlMode.Value; // C1: ?scale=fit | ?scale=expand
            var feel = new FeelSettings();
            var i18n = new Localizer(ReadText);
            i18n.Text.Missing = key => Debug.LogWarning("Minik Usta: missing i18n key " + key);
            var ui = new UiRenderer(feel);
            this.ui = ui;
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
            // V2 sound: ZzFX recipes (Resources/audio/sounds.json) rendered in the background of the first seconds, one AudioSource.
            // WebGL keeps audio locked until the first user input (the browser's rule); other platforms start unlocked.
            var bank = new SoundBank(SoundSet.FromText(ReadText("audio/sounds")));
            bank.RequestAll();
            var audio = new AudioPlayer(bank, new UnityAudioOutput(gameObject, cam), Application.platform == RuntimePlatform.WebGLPlayer);
            // C1: `?shots=1` in the page URL (WebGL) starts the CI screenshot autoplay (docs/CI.md "Ekran görüntüleri"). It plays on an
            // in-memory save, so a visit with that parameter can never touch a real save.
            var shotOptions = ShotOptions.FromUrl(Application.absoluteURL);
            ISaveStore store = shotOptions != null ? new MemorySaveStore() : (ISaveStore)new PlayerPrefsStore();
            app = new GameApp(view, ui, catalog, i18n, EconomyConfig.FromText(economyText), store, feel, layout, haptics,
                EconomyInfo.FromText(economyText), message => Debug.LogWarning(message), audio, art);
            ui.Pressed = () => app?.UiPressed();
            input = new PointerInput();
            screenW = Screen.width;
            screenH = Screen.height;
            app.SetScreen(screenW, screenH);
            ui.SetCanvas(app.Player.Viewport.W, app.Player.Viewport.H);
            app.Boot();
            if (shotOptions != null)
                shots = new ShotScenario(app, n => DebugSolver.SolutionLog(ReadText("solutions/level_" + n.ToString("000", System.Globalization.CultureInfo.InvariantCulture) + ".solver")),
                    line => Debug.Log(line), shotOptions);
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
                ui?.SetCanvas(a.Player.Viewport.W, a.Player.Viewport.H);
            }
            input.Poll(a, a.Player.Viewport);
            if (Debug.isDebugBuild) DebugKeys(a);
            float dtMs = Mathf.Min(Time.unscaledDeltaTime * 1000f, MaxFrameMs);
            warmup?.Tick(dtMs, a.Flow.Current);
            a.Tick(shots != null ? dtMs * shots.TimeScale : dtMs);
            shots?.Tick(dtMs);
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
