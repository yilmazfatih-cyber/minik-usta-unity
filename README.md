# Minik Usta — Unity taşıması

Minik Usta'nın (dikey ekranlı blok yerleştirme bulmacası) Unity 6 sürümü. Kaynak gerçek **web reposudur**
(`yilmazfatih-cyber/minik-usta`, yerelde `../minik-usta`): `src/core/**` (11 400 satır saf TypeScript), `docs/GDD.md`
(K-01…K-55), `docs/TECH_DESIGN.md` §2–§6 ve §2R, `levels/*.json`, `tests/golden/*.json`. Bu repo o davranışı C#'a
**satır satır** taşır; yeni mekanik icat edilmez, belirsizlikte TS kodu esastır.

## Bağlayıcı kararlar (web reposu `docs/DECISIONS.md`)

| Karar | Özet |
| ----- | ---- |
| D-091 | Mevcut mekanik birebir taşınır; **match-3 yok**. Brifteki match-3 maddeleri karşılıklarına çevrilir (sürükleme hissi, kötü geçiş geri sekmesi, şantiye düşüşü, kilit parlaması, Altın Mala, kamyon teslimatı, takılma nabzı, Bonus İnşaat). |
| D-092 | Kahraman **Tuna** kalır (D-044, D-087); "genç kral" uygulanmaz. |
| D-093 | Çekirdek **saf C#** (UnityEngine bağımlılığı yok); bulutta .NET SDK + NUnit ile derlenir ve test edilir, aynı test dosyaları Unity Test Framework (EditMode) altında çalışır. Görünüm katmanı proje sahibinin Unity kurulumunda derlenir. |
| D-094 | Unity projesi bu ayrı repoda yaşar; web (Phaser) sürümü `main` = de6f16c olarak yayında kalır, görsel v3 web'de dondurulur. |
| D-097 | Faz 3 kabul (2026-11-01): Q0–Q5 tamam; şüpheci inceleme 1 Engel + 4 Önemli kapattı (`docs/REVIEW_PHASE3.md`). |
| D-098 | Program otonom: faz sonunda onay beklenmez; durma yalnız proje sahibine bağlı girdilerde. Kalite çıtası ART_REFERENCE §10 (her boyut ≥ 4/5). Üretim Opus / Sonnet / Haiku, orkestratör Fable (D-095). |
| D-099 | **Sabit saha ve bulmaca zekâsı (K-55, paket A3 ile C#'a taşındı):** `schemaVersion` 2 bölümünde bütün bloklar baştan sahadadır, dilim geçişinde sahaya yeni parti ya da kamyon dolumu gelmez; D1 yeniden dizme kalktı (D1 → zincir/ıslaklık, hâlâ D1 ise Söküm `access`); `RULES_VERSION` 3. `schemaVersion` 1 (Faz 2R arşivi) verisi K-25…K-27 ile oynanmaya devam eder. |

Faz 1 raporu: web reposunda `docs/UNITY_PORT_FAZ1.md`. TS → C# dosya eşlemesi: [`docs/PORT_MAP.md`](docs/PORT_MAP.md).

## Proje yapısı

```
/
├─ Assets/
│  ├─ MinikUsta/
│  │  ├─ Core/                     saf C# çekirdek (asmdef: noEngineReferences = true, autoReferenced)
│  │  │  ├─ Types.cs Geometry.cs Coords.cs Shapes.cs Rng.cs Hash.cs Fnv1a.cs State.cs Grid.cs Ascii.cs
│  │  │  ├─ Movement.cs Gravity.cs Placement.cs Delivery.cs Site.cs Panorama.cs        (P1: hareket hattı)
│  │  │  ├─ Hooks.cs Goals.cs Combo.cs Boosters.cs Deadlock.cs Summary.cs              (P2: kurallar, çıkmaz, güçlendiriciler)
│  │  │  ├─ Events.cs Moves.cs Session.cs SessionActionJson.cs                         (P3: K-35 hamle hattı, GameSession, olay günlüğü)
│  │  │  ├─ Json/                  bağımlılıksız JSON okuyucu/yazıcı (JsonValue, JsonReader, JsonWriter + canonicalJson)
│  │  │  ├─ Level/                 LevelData (`schemaVersion` 1 | 2, D-099 `targets` bantları, `LevelSchema.IsFixedYardLevel`), LevelJson (zod şemasının elle yazılmış karşılığı), Logic (K-45 doğrulayıcı, L-36 `fixed_yard_batches`), Load (loadLevel), Compile, Plan, Mechanics
│  │  │  └─ Obstacles/             engel eklenti arayüzü (Types), kayıt + kanca birleştirme (Registry), W1_StaticGap, Y3_Chain, S1_SlidingSite
│  │  ├─ Presentation.Core/        motor-bağımsız sunum (Faz 3 Q0; asmdef noEngineReferences): Tween/ (Easing, Tween, Sequence, TweenClock),
│  │  │                            Feel/ (FeelSettings, FallMotion, StuckPulse), Layout/ (LayoutSettings, BoardLayout, ScreenFit, HitTest),
│  │  │                            Input/ (DragMath, DragMapper), Animation/ (AnimationStep, AnimationPlanner, AnimationQueue)
│  │  │                            Faz 3 Q1: Game/ (LevelPlayer, StepRunner, PieceViews, BoardScene, BoardPose, ShadowLook (K-18),
│  │  │                            HudLayout, LevelCatalog, DebugSolver, ILevelView/IPieceVisual), Art/ (Atlas: prosedürel doku,
│  │  │                            Symbols, Palette, Rgba), Text/ (Strings: tr.json/en.json, TrText.Upper)
│  │  │                            Faz 3 Q2 (his): Game/ (Particles (ParticleField), CellWave, Haptics, PressFeel, TruckModel, Economy,
│  │  │                            StepRunner.Fx), Art/ (kıvılcım, toz, yıldız, sikke, mala, halka karoları)
│  │  │                            Paket V1: Art/ (AtlasData, Frames, BlockSkin, FallbackTiles), Ui/ (UiNode ağacı, TextStyles, IUiSurface,
│  │  │                            LevelHud, WindowViews, HomeView + SettingsView), Flow/ (ScreenFlow, GameApp), Save/ (SaveModel, SaveCodec),
│  │  │                            Meta/ (EconomyConfig, Offer, Rewards, Price), I18n/ (Localizer), Game/PlanThumb
│  │  │                            C1: Audio/ (ZzFx, SoundBank, AudioPlayer, Sounds), Tutorial/ (TutorialController, TutorialPresence, TutorialEvents,
│  │  │                            TutorialGlove, TutorialLayout, TutorialView, TutorialPlayer), Shots/ (ShotScenario: ?shots=1 CI sürücüsü)
│  │  │                            B2: Anim/ (AnimClip, ClipLibrary + ArtSet: king.json / fx.json, AnimPlayer, ClipActor + ClipPlacement,
│  │  │                            KingDirector: Usta Kral, FxPlayer: 3B FX klipleri, HomeParallax)
│  │  ├─ Unity/                    ince Unity katmanı (asmdef MinikUsta.Unity → Core, Presentation.Core, UnityEngine.UI): GameBootstrap
│  │  │                            ([RuntimeInitializeOnLoadMethod]), GameRunner (kare döngüsü), UnityLevelView (ILevelView), UnityPieceVisual,
│  │  │                            UnityBoardView/UnityShadowView, UnityHud (uGUI + LegacyRuntime), PointerInput (eski Input), ProceduralSprites,
│  │  │                            Q2: UnitySpark/UnityUiSpark (parçacık), OffsetTarget (şantiye katmanı), PressableButton
│  │  │                            V1: AtlasSprites (v4 atlas), SpriteDraw, UiRenderer (uGUI; UnityHud'un yerine), PlayerPrefsStore
│  │  │                            B2: ClipSprites (art3d sayfaları, tembel çözme), SpriteAnimator + UnityFxSprite / UnityUiFx, CameraTarget (sarsıntı + zoom)
│  │  ├─ Resources/                levels/ (oyunun okuduğu bölümler), config/economy.json, i18n/{tr,en}.json (+ V1 unity_{tr,en}.json),
│  │  │                            solutions/ (dev: çözümü oynat), art/ (V0 atlas: atlas.json + atlas_N.bytes), fonts/ (Baloo 2 ExtraBold + OFL),
│  │  │                            art3d/ (B1/B1k 3B kütüphaneleri: king.json + king_N.bytes, fx.json + fx_N.bytes)
│  │  ├─ Scenes/Level.unity        yalnız ortografik Main Camera (elle yazılmış Unity 6 YAML; .meta GUID sabit)
│  │  ├─ Presentation.Core.Tests/  NUnit 3 testleri (Editor; Golden/tokens.json = web tokens kopyası)
│  │  └─ Core.Tests/               NUnit 3 testleri (Editor platformu; Unity EditMode ile aynı dosyalar)
│  │     ├─ Fixtures/              Builders (tests/fixtures/builders.ts), Boards (ortak tahta yardımcıları, Drop/Play), Cover (cover.ts), ObstacleFixtures, J (JSON literal), TestPaths, RuleCoverage (P4 kapsam kapısı)
│  │     │  └─ Levels/{valid,invalid,v2}/  K-45 doğrulayıcı fikstürleri (web tests/level/fixtures kopyası; `v2/` = K-55 örneği P1)
│  │     ├─ Obstacles/             RegistryTests, W1StaticGapTests, S1SlidingSiteTests, Y3ChainTests, S2PlanVoidTests
│  │     ├─ Archive2R/             **Faz 2R arşivi (`schemaVersion` 1, kamyon partili):** level_001…010.json + hand/solver altın kayıtları (web `c1facf4`). K-25…K-27 ve TS↔C# eşdeğerlik testleri bu veriye dayanır (K-55 madde 9); `TestPaths.LoadLevelJson`, `Boards.LevelFile`, `TestPaths.LoadGolden("level_NNN.…")` bunu okur
│  │     └─ Golden/                web reposundan kopya: level_NNN.hand.json, level_NNN.solver.json (yayın bölümleri, `schemaVersion` 2; `TestPaths.LoadCurrentGolden`); TS vektörleri p0…p3_vectors.json; rule_ids.json (kural kimlikleri)
│  └─ StreamingAssets/MinikUsta/
│     ├─ levels/                   level_001…010.json (yayın bölümleri, D-099 `schemaVersion` 2; web reposundan birebir kopya)
│     └─ config/economy.json       güçlendirici açılış seviyeleri (L-17 `booster:` / `pre:` vurguları)
├─ dotnet/                         Unity dışı derleme ve test (D-093)
│  ├─ MinikUsta.sln
│  ├─ MinikUsta.Core/              Assets/MinikUsta/Core/**/*.cs → netstandard2.1, LangVersion 9, Nullable, TreatWarningsAsErrors
│  ├─ MinikUsta.Core.Tests/        Assets/MinikUsta/Core.Tests/**/*.cs → net8.0, NUnit 3.14 + NUnit3TestAdapter + Microsoft.NET.Test.Sdk
│  ├─ MinikUsta.Presentation.Core/ Assets/MinikUsta/Presentation.Core/**/*.cs → netstandard2.1, LangVersion 9 (Core'a başvurur)
│  ├─ MinikUsta.Presentation.Core.Tests/  Assets/MinikUsta/Presentation.Core.Tests/**/*.cs → net8.0, NUnit 3.14
│  ├─ UnityStubs/                YALNIZ DERLEME DENETİMİ: Unity katmanının kullandığı UnityEngine / UI / EventSystems / Events /
│  │                             SceneManagement üyelerinin Unity 6000.0 imzalı boş gövdeli kopyaları (Unity'nin kendisi değil);
│  │                             `Handheld` gibi yalnız mobilde var olan türler `#if UNITY_ANDROID || UNITY_IOS` içinde (P3-02)
│  ├─ UnityStubs.Android/        aynı stub kaynakları, UNITY_ANDROID tanımlı (Handheld var)
│  ├─ UnityLayer.props           Unity katmanı projelerinin ortak ayarı ve kaynak yolu
│  ├─ MinikUsta.Unity/           Assets/MinikUsta/Unity/**/*.cs → UnityStubs'a karşı derlenir, UNITY_* tanımsız = WebGL durumu
│  │                             (yazım + imza + platform koruması denetimi; D-093)
│  └─ MinikUsta.Unity.Android/   aynı kaynaklar, UNITY_ANDROID tanımlı: `#if` dallarının Android tarafı
├─ tools/
│  ├─ stub-gate-selftest.sh        stub kapısının korumasız `Handheld`'ı WebGL durumunda reddettiğini kanıtlar (4 durum, ~10 sn)
│  ├─ sync-from-web.sh             levels, golden, K-45 fikstürleri, economy.json ve tokens.json dosyalarını web reposundan kopyalar
│  ├─ gen-p0-vectors.ts            TS çekirdeğinden bit-eşitlik vektörleri üretir (node 22, ek paket yok)
│  ├─ gen-p1-vectors.ts            hareket / yerçekimi / yerleşim / teslimat vektörleri (10 bölüm + 9 geometri × 5 rastgele tahta)
│  ├─ gen-p2-vectors.ts            doğrulayıcı / hedef / güçlendirici / çıkmaz / özet / kayıt vektörleri (fikstürler, kanonik çözüm boyunca 90 durum, 40 cover tahtası)
│  ├─ art/extend.mjs               v3 özellik karelerini (art/svg/trait) atlas sayfası 3'e ekler (ART_UNITY §9)
│  ├─ gen-p3-vectors.ts            GameSession dizileri: her hamle türü, geri al, +5, ret, çıkış, replay (170 dizi, ~4 300 girdi)
│  ├─ gen-rule-ids.ts              web belgelerinden kural kimlikleri + fazları → Core.Tests/Golden/rule_ids.json (kapsam kapısı)
│  ├─ diff/ts-oracle.mjs           diferansiyel fuzz kehaneti: TS GameSession ile ~400 tohumlu / betikli dizi → tools/diff/out/ (git dışı)
│  ├─ audio/                       gen-sounds.mjs (web tokens → Resources/audio/sounds.json), gen-vectors.mjs (web ZzFX ile bit-eşitlik vektörleri)
│  └─ ci-shots/                    CI ekran görüntüsü: shoot.mjs (Playwright 1.64.1, SwiftShader), publish.sh (ci-shots dalı), shots.json (docs/CI.md §7)
└─ docs/PORT_MAP.md                TS dosyası → C# dosyası eşleme tablosu (her paket günceller)
```

`ProjectSettings/ProjectVersion.txt` (6000.0.85f1), `Packages/manifest.json`, `ProjectSettings/ProjectSettings.asset`
(yalnız gerekli alanlar), `ProjectSettings/EditorBuildSettings.asset` (Level sahnesi) ve `ProjectSettings/InputManager.asset`
(Unity 6000.0 varsayılanı; uGUI `StandaloneInputModule` eksenleri ister) bulut derlemesi için eklendi (`docs/CI.md`); kalan
ayarları ve `.meta` dosyalarını Unity ilk koşuda üretir, iş akışı bunları artifact olarak yükler (`docs/CI.md` §4). `.gitignore` Unity'nin ürettiği `Library/`, `Temp/`, `Logs/` vb. klasörleri dışlar.

## Komutlar

| Komut | Ne yapar |
| ----- | -------- |
| `cd dotnet && dotnet build -warnaserror` | Çekirdek + testleri derler; uyarı = hata |
| `cd dotnet && MINIK_PREVIEW_DIR=../artifacts/preview dotnet test --filter PreviewTests` | Unity yer tutucu ekranının yazılımsal önizlemesi (aynı atlas, renk, sıra, 9-dilim; metin yok) → 390×844 ve 360×800 PNG (`artifacts/`, git dışı). Unity ekran görüntüsü DEĞİL |
| `cd dotnet && MINIK_PREVIEW_DIR=../artifacts/preview dotnet test --filter ScreenDumpTests && cd .. && node tools/art/compose.mjs` | V1 ekran kompozitleri: Presentation.Core sahneleri (tahta, bloklar, K-18 gölgesi, HUD, pencereler, ana sayfa, ayarlar) atlas sayfaları ve Baloo 2 ile çizilir → `artifacts/preview/v4-unity-<ekran>.png` (1080×1920), `-360x800`, `-390x844` (FIT). Unity ekran görüntüsü DEĞİL |
| `tools/unity-refcheck.sh` | nuget.org'da `UnityEngine.Modules` 6000.0.x var mı bakar; yoksa SKIPPED (bkz. `docs/CI.md` §6) |
| `tools/font/build-font.sh` | `Resources/fonts/Baloo2-ExtraBold.ttf`'yi google/fonts değişken Baloo 2'den (wght 800 örneği + Latin alt kümesi) yeniden üretir; fontTools gerekir |
| `cd dotnet && dotnet test` | Tüm NUnit testlerini koşturur (testler repo kökünü `Assets/MinikUsta/Core/MinikUsta.Core.asmdef` ile bulur ve bölüm / altın / fikstür JSON'larını oradan okur) |
| `tools/sync-from-web.sh [/yol/minik-usta]` | Bölüm, altın, fikstür (`valid`, `invalid`, `v2`), economy.json ve tokens.json dosyalarını web reposundan yeniler (varsayılan `../minik-usta`); `Archive2R/` elle yönetilir, bu betik ona dokunmaz; Faz 3 Q1'den beri bölümleri, economy.json'u, `src/i18n/{tr,en}.json`'u ve kanonik çözüm günlüklerini `Assets/MinikUsta/Resources/` altına da kopyalar (oyun `Resources.Load<TextAsset>` ile okur; StreamingAssets kopyası çekirdek testleri içindir) |
| `node tools/gen-p0-vectors.ts [/yol/minik-usta]` | `Core.Tests/Golden/p0_vectors.json` dosyasını TS çekirdeğinden yeniden üretir |
| `node tools/gen-p1-vectors.ts [/yol/minik-usta] [--verbose]` | `Core.Tests/Golden/p1_vectors.json` dosyasını (P1 modülleri: R, K-07 sınıfları, düşüşler, kararlar, K-08 takip taraması, K-17 dönüş hedefleri, settleYard) yeniden üretir; `--verbose` her satırı `p1_vectors.verbose.json` dosyasına yazar (git dışı, uyuşmazlık teşhisi için) |
| `node tools/gen-p3-vectors.ts [/yol/minik-usta] [--verbose]` | `Core.Tests/Golden/p3_vectors.json` dosyasını yeniden üretir: TS `GameSession.apply` ile 170 tohumlu deneme (sürükleme, çekiç, vinç, boya fırçası, Altın Mala, eski mala, geri al, +5 jeton/reklam, ret, çıkış) oynanır; girdiler saklanır, C# aynı girdileri oynatıp her paketin sonucunu, `eventLogHash`'ini, durum karmasını, oturum alanlarını ve `TurnSummary`'yi karşılaştırır; `--verbose` her satırı ve her paketin kanonik olay JSON'unu `p3_vectors.verbose.json` dosyasına yazar (git dışı). Uyuşmayan dizinin C# satırları `Golden/p3_csharp.<dizi>.txt` dosyasına yazılır (git dışı) |
| `node tools/gen-rule-ids.ts [/yol/minik-usta]` | `Core.Tests/Golden/rule_ids.json` dosyasını web reposunun kapsam aracıyla (`tools/rule-coverage.ts` `ruleIds`: GDD başlıkları, TECH §12.4 F sütunu, OBSTACLES imza tablosu ve matrisi) yeniden üretir; `RuleCoverageTests` Faz 2R'ye kadar her kimliğin (76) en az bir C# test adında geçtiğini denetler |
| `node tools/diff/ts-oracle.mjs [/yol/minik-usta] [--seeds N] [--moves M] [--out DIR]` | Diferansiyel fuzz kehaneti: web `src/core`'u içe aktarıp tohumlu **geçerli** eylem dizileri (DragSession R'sinden iptal etmeyen sürüklemeler, geçerli hedefli güçlendiriciler, Undo, +5, ret, çıkış) ve betikli senaryolar (Söküm, D1 karıştırma, kuyruk, teklif, Undo) oynar; her adımın Zobrist / tampon / olay karmasını, olay JSON'larını, ASCII'sini ve oturum alanlarını `tools/diff/out/*.json`'a yazar (git dışı). `DiffFuzzTests` aynı eylemleri C#'ta oynatıp her adımda karşılaştırır; ilk sapmada adım, eylem, ilk farklı olay ve iki tahta yan yana. Çıktı yoksa bu testler `Ignore` olur — `dotnet test`'ten önce koşturun |
| `node tools/gen-p2-vectors.ts [/yol/minik-usta] [--verbose]` | `Core.Tests/Golden/p2_vectors.json` dosyasını (K-45 doğrulayıcı sonuç listeleri, D1/D2/D3a/Söküm/reshuffle, K-33/36/37/38 hedef kümeleri, K-34 kanca 5, hedef sayaçları, özet, kayıt) yeniden üretir; durumlar TS `applyMove` ile kanonik çözüm boyunca üretilip `encodeState` ile saklanır, C# aynı tamponu çözer; `--verbose` her satırı `p2_vectors.verbose.json` dosyasına yazar (git dışı) |

**Faz 2R arşivi ve vektörler (paket A3):** `gen-p1/p2/p3-vectors.ts` ve `diff/ts-oracle.mjs` yayın bölümlerinin (web `levels/`) yanında
`Assets/MinikUsta/Core.Tests/Archive2R` arşivini de oynatır (`--archive=<klasör>` ile değişir): P1 `archive_level_NNN` tahtaları, P2 `archive` grubu
(doğrulayıcı) ve `levels[].archive` durumları, P3 `archive N seed S` dizileri, oracle `archive_NNN.json` kaynakları. `tools/review/gen-follow-vectors.ts` yalnız yayın
bölümlerini kullanır (K-08 izleme sürümden bağımsızdır). Çıkmaz D1 artık yeniden dizilmediği için P2 satırlarında `any:` ve `reshuffle:` yok; yerine
`step12offer:` (kabul edilen +5 sonrası D1 Söküm'ü) var.

Unity içinde: Window → General → Test Runner → EditMode; `MinikUsta.Core.Tests` asmdef'i `UNITY_INCLUDE_TESTS` tanımı ve
`nunit.framework.dll` referansıyla aynı dosyaları koşturur.

## Taşıma kuralları

- **Saf C#:** `Assets/MinikUsta/Core` içinde `UnityEngine`, `System.Text.Json`, `Newtonsoft` yok. JSON için Core içindeki
  küçük okuyucu kullanılır.
- **Unity 6 uyumu:** LangVersion 9 (file-scoped namespace, global using, record struct, required üye yok; `init` erişimcisi de
  yok — netstandard2.1'de `IsExternalInit` bulunmaz); `#nullable enable`. Core csproj hedefi **netstandard2.1**: Unity'nin
  sunduğu API yüzeyi; net8.0'a özel bir API yanlışlıkla kullanılamaz (test projesi net8.0'dır).
- **Belirlenimcilik:** 32-bit tamsayı aritmetiği TS ile bit bit aynı (`Math.imul` → `unchecked(a * b)`, `>>>` →
  `(int)((uint)x >> n)`, `|0` → `unchecked((int)…)`); mulberry32, splitmix32, fmix32, Zobrist tabloları ve FNV-1a 64
  (`ulong`) birebir; karma dizeleri TS ile aynı hex biçiminde. `p0_vectors.json` bunu test eder; `p1_vectors.json` hareket,
  yerçekimi (kayan nokta hysteresis dahil), yerleşim ve teslimat sonuçlarının 484 parça üzerinde TS ile karma karma eşitliğini
  test eder; `p2_vectors.json` doğrulayıcı mesajlarını, D3a açılım sayılarını (4096 yuvalı memo tablosu dahil), D1 reshuffle
  hamlelerini ve Söküm olaylarını 130 durum üzerinde TS ile karşılaştırır; `p3_vectors.json` K-35 hattını ve `GameSession`'ı
  her hamle türü için paket paket (`eventLogHash` dahil) TS ile karşılaştırır; altın testler (`HandGoldenTests`) 10 bölümün
  kanonik çözümünde `eventLogHash`, son ASCII tahta ve `levelHash` değerlerinin TS ile birebir aynı olduğunu doğrular;
  `DiffFuzzTests` (P4) kehanetin ~400 dizisinde (~10 000 eylem) **her adımı** TS ile karşılaştırır.
- **JS anlamı (P6):** Çekirdekteki her düzenli ifade `Core/JsRegex.cs` ile TS kaynak metninden kurulur (`$` yalnız girdi sonunda
  eşleşir, `\d` = `[0-9]`, `\s` = JS'in 25 boşluk kod birimi); düz `new Regex` kullanılmaz. Sayı → metin dönüşümleri değişmez
  kültürle yapılır; çekirdek çıktıları `CurrentCulture`'a bağlı değildir (`K35_K43_K45_core_outputs_do_not_depend_on_CurrentCulture`),
  testler derleme düzeyi `SetCulture("")` altında koşar (`Core.Tests/AssemblyInfo.cs`, kanarya: `CultureGuardTests`).
- **İş parçacığı güvenliği (P6):** Çekirdekte paylaşılan değiştirilebilir `static` durum yoktur (kural tabloları dışarıya açık `HashSet`: REVIEW_PHASE2 R-10, açık); D3a bellek tablosu, `D3aStats` ve
  `Movement` karalama dizileri `[ThreadStatic]` (solver / bot paralel koşabilir). Oyun durumu (`GameState`) iş parçacığı güvenli değildir;
  her iş parçacığı kendi durumunu çalıştırır.
- **Kural kapsamı:** `RuleCoverageTests` web reposunun `npm run test:rules -- --phase 2R` kapısını C# test adlarına uygular
  (76 kimlik; ad biçimi `K49_…`, `…_E42_…`, `W1StaticGapTests`).
- **Engeller eklentidir:** `Core/Obstacles/Types.cs` arayüzü (`ObstacleRule`), `Registry.cs` kuralları `Order` sırasına göre
  `MoveHooks`'a birleştirir; çekirdekte engele özel `if` yoktur (yeni engel = yeni dosya + `Registry.AllRules`'a bir satır).
- **Kural atıfları:** Her K-xx kuralı yorumlarda ve test adlarında (`K49_…`) korunur.
- **Veri:** Aynı `level_NNN.json` dosyaları değiştirilmeden okunur; geçersiz bölüm oyuna yüklenmez (`Compiler.LoadLevel`:
  şema + K-45 çalışma zamanı alt kümesi `Logic.RuntimeChecks`, D-005).

## Faz 3 — oynanabilir yer tutucu tahta

**Durum (Faz 3 KABUL, D-097):** Q0 (motor-bağımsız sunum çekirdeği), Q1 (oynanabilir tahta, yer tutucu görsel), Q2 (his katmanı: sprite parçacıkları, iniş dalgası, mala ve seri boncukları, kamyon ve şantiye kayması, Bonus İnşaat sikkeleri, kazanma yıldızı, düğme basışı, kamera sarsıntısı, Android titreşimi), Q4 (şüpheci inceleme) ve Q5 (kapanış) tamam. Gerçek sanat ve görsel katman Faz 4'te (bkz. Program); ses V2'de. Unity Editor bu ortamda yok; derleme GitHub Actions'ta GameCI ile yapılır ([`docs/CI.md`](docs/CI.md)). Rapor: [`docs/PHASE3_REPORT.md`](docs/PHASE3_REPORT.md).

### Nasıl oynanır (paket V1)

- Adres: https://yilmazfatih-cyber.github.io/minik-usta-unity/ (ilk başarılı derlemeden sonra açılır).
- Açılışta **ana sayfa** gelir: can ve altın kapsülleri, Ağaç Ev ilerlemesi, Usta Kral (B2), Tuna, Kepçe ve tek yeşil düğme **"Oyna · Bölüm N"**.
  Bölüm 1–2 tek dokunuşla başlar; Bölüm 3'ten itibaren önce bölüm başı penceresi (yapı resmi, blok ve hamle sayısı) açılır, **Oyna**'ya basılır.
- Bir bloğa bas ve sürükle; doğru yere bırakılan blok yerine oturur, yanlış yere bırakılan geri seker. Kırmızı çarpı ve puan kaybı yok.
- Hamleler bitince **+5 hamle** teklifi gelir: hayattaki ilk teklif Usta Kral'ın hediyesidir, sonra 900 / 1.350 / 1.800 altın (≈ ₺ karşılığı
  altında). Altın yetmezse düğme gri ve pasiftir; reklam seçeneği gri "Şu an reklam yok". "Hayır, teşekkürler" → bir can gider → **Tekrar dene** ya da **Ana sayfa**.
- Kazanınca: yıldız, Bonus İnşaat altını, kalan Altın Mala; bitmiş yapı pencerenin içindeki kartta görünür. **Devam** ana sayfaya döner.
- Duraklat: **Devam**, **Bölümü baştan başlat**, **Ana sayfa** ve köşede ayar dişlisi. Hamle yapmadan çıkmak cezasızdır; hamleden sonra 1 can gider (onay penceresi sorar).
- Ayarlar (ana sayfa dişlisi ya da mola penceresi): ses, titreşim, **Hareketi azalt**, dil **Türkçe / English**. Ayarlar ve ilerleme cihazda saklanır.
- Uygulama bir bölümün ortasında kapanırsa açılışta aynı yerden devam eder (K-43).
- Geliştirme derlemesinde (Editor ya da Development build) klavye kısayolları: N sonraki bölüm, R baştan, A tahtanın ASCII hâli, S çözümün sonraki hamlesi.

### Unity'de açma ve testler

1. Unity Hub'da **6000.0.85f1** sürümünü kur (sürüm `ProjectSettings/ProjectVersion.txt` ile sabit).
2. Hub → **Projects** → **Add** → bu repo klasörünü seç.
3. İlk açılış birkaç dakika sürer. Unity eksik `.meta` dosyalarını üretir; `git status` ile değişenleri gözden geçir.
4. `Assets/MinikUsta/Scenes/Level.unity` dosyasını aç ve **Play**'e bas.
5. Testler: **Window → General → Test Runner → EditMode → Run All**. Koşan assembly'ler: `MinikUsta.Core.Tests`
   ve `MinikUsta.Presentation.Core.Tests`. Beklenen sayılar dotnet koşusuna göre (paket A3, K-55; B2; F1): Presentation 514 geçer. Core, `tools/diff/out`
   (git dışı, `ts-oracle.mjs` çıktısı) varsa 1882 geçer ve 5 atlar (655 DiffFuzz dizisi, 0 sapma); yoksa 1226 geçer ve 7 atlar (DiffFuzz durumları üretilmez).
   Unity içinde bu koşu henüz yapılmadı.

Dotnet tarafı (Unity gerekmez):

```
cd dotnet && dotnet build -warnaserror && dotnet test
```

### Paket B2 — 3B / 2.5B entegrasyon (D-099, D-100, D-101)

- **Usta Kral** (D-101; özgün, baretli kral): ana sayfanın ana sembolü (girişte iki kez el sallar, sonra sahne boşta; dokununca
  sevinir), bölüm ekranının köşesinde Tuna'nın yerinde (doğru yerleşim → kısa sevinç; Altın Mala, dilim tamamlama ve kazanma →
  büyük sevinç; 5 sn dokunuşsuz ya da takılma nabzı → düşünür; öğretici balonu → konuşur, eldiven → işaret eder; hamle bitti →
  cesaretlendirir) ve pencerelerde (bölüm başı → Oyna'yı işaret eder; kazanma → büyük sevinç döngüsü; +5 teklifi / can gitti →
  cesaretlendirir, sonra sakin; çıkış onayı → düşünür). Öğretici balonunun portresi kralın konuşan büstüdür. Aynı anda en çok iki
  karakter klibi koşar (pencere açıkken köşe donar). Klipler pivotla (ayaklar) yerleştirilir, kutuya sığdırılmaz; boşta nefes
  tween'i; "Hareketi azalt"ta durağan poz, yarı süre, olay sesi yok.
- **3B FX klipleri** (`fx.json`): inişte toz bulutu, doğru yerleşimde her çıkıntıda kilit parlaması (alttan üste 40 ms),
  dilim tamamlamada şantiyenin arkasında parlama + kamera hafif yakınlaşma, kazanmada yakınlaşma + 3B konfeti (döner kareler) +
  dönen sikkeler + pencerede yıldızın ARKASINDA yıldız patlaması, güçlendiricilerde çekiç + darbe yıldızı, vinç pençesi, boya
  sıçraması, Altın Mala parıltısı. Bütçe: 24 canlı FX; azaltılmış harekette ağır FX yok, hafifler iki kat hızlı.
- **2.5B his:** sürüklenen blok hızına göre yaylı eğilir (±8°, bırakınca 120 ms'de doğrulur), kalkınca gölgesi aşağı kayar,
  genişler ve yumuşar; düğmeler basılıyken kitin `_pressed` çizimini gösterir; ana sayfa arka planı 7 paralaks katmanı olarak
  zamanla hafifçe kayar (cihaz eğimi yok).
- **D-099:** kamyon görünümü kalktı; Faz 2R arşiv bölümünün partisi kamyonsuz düşer, kuyruk çipi yalnız kuyrukta blok varken görünür.
- `FeelSettings` yeni alanları: `DragTilt*`, `LiftShadow*`, `LandPuffScale`, `LockShine*`, `SegmentFlareScale`, `SegmentZoom*`,
  `WinZoom*`, `CelFps`, `*FxScale`, `KingThinkAfterMs`. Önizleme: `ScreenDumpTests` → `tools/art/compose.mjs` (art3d kareleri de
  çizilir) → `artifacts/preview/b2-*.png` (Unity ekran görüntüsü DEĞİL).

### Bilinen eksikler (V1 sonrası)

- Ses yok (V2). Güçlendiriciler oynanmaz: yuvalar K-54 durumunu gösterir (kilitli / hedefsiz / "+" / adet) ama dokunuş bir şey yapmaz.
- Öğretici yok. Can yenilenmesi yok (Faz 5): can 0 iken bölüm yine başlar, can ayrılmaz. Mağaza, köprü, lig, kasaba görevleri yok.
- Unity'de görülmedi: uGUI metin yerleşimi (Baloo 2'nin dikey metrikleri), 9-dilim kenarlar, PressableButton hissi, kayıt / devam.

### Bilinen eksikler (Q2 sonrası, tarihsel)

- Ses yok. Titreşim yalnız Android'de (`Handheld.Vibrate`, tek sabit darbe). ↩ iptal rozeti, ip/eğilme ve sürükleme izi yok.
  Q2 ile gelenler: sprite parçacıkları, kamyon, seri boncukları ve mala, şantiye kayması, Bonus İnşaat sikkeleri, kazanma yıldızı,
  düğme basışı, kamera sarsıntısı (hepsi yer tutucu çizimle; Unity'de görülmedi).
- Öğretici yok. Güçlendiriciler devre dışı; Geri Al düğmesi yok; +5 hamle teklifi yok.
- Görseller yer tutucu: prosedürel renkli kutular ve 8 renk sembolü. Tuna çizimi yok.
- Ayarlar yok; "Animasyonları azalt" kapalı.
- Yalnız Türkçe metin bağlı (`en.json` henüz okunmuyor).
- LegacyRuntime fontunda Türkçe harfler (ş, ğ, ı, İ) Unity'de henüz görülmedi.

## Program (D-098)

Proje sahibinin çıtası: Royal Match / Block Blast seviyesinde, mağazaya çıkabilecek ürün. Kararlar web reposunda: `docs/DECISIONS.md`
D-097 (Faz 3 kabul) ve D-098 (program). Faz sonunda onay beklenmez; faz raporu yazılır ve sonraki faz başlar. Durma yalnız proje
sahibine bağlı girdilerde (lisans, mağaza hesapları, imza anahtarları, ücretli hizmetler) ve geri alınamaz dış eylemlerde.

| Faz | Kapsam | Durum |
| --- | --- | --- |
| 3 | Oynanabilir yer tutucu tahta, his katmanı, şüpheci inceleme ([`docs/PHASE3_REPORT.md`](docs/PHASE3_REPORT.md)) | KABUL (D-097) |
| 3-v3c | Bölümler v3c (D-103 geniş şantiye 6–10, D-104 palet yalnız şantiyeye, D-105 bonus ödül anı; KABUL 2026-11-09): 10 bölüm, 89 blok (6–10: 17/18/18/17/19; hedef 18–22 tutturulmadı, P-P3-1), P5 şekli, palet Y10, sargı/makas/kaygan/bonus; C# web 37d5617 ile eşit, RULES_VERSION 5; 595 + 1280 test yeşil. Açık sorular ve doğrulanmayanlar: [`docs/LEVELS_V3C_REPORT.md`](docs/LEVELS_V3C_REPORT.md) | Yayında (v3c); blok sayısı kararı proje sahibinde |
| 4 | Görsel ve his yükseltmesi: SVG sanat paketi (V0), görsel katman, HUD v3, pencereler, ana sayfa, ayarlar, kayıt, i18n (V1) | V0 ve V1 tamam; UX turu U1–U4 (P0 ve ux-review R-01…R-07) kodda; U5 raporu [`docs/UX_REPORT.md`](docs/UX_REPORT.md). Açık: AU-1 alt boşluk, AC-14 yeniden puanlama; sırada V2 (ses, öğretici, güçlendirici oynanışı) |
| 5 | İçerik ve meta: 50 bölüm, 5 hikâye bölümü, kasaba, ekonomi, 2 etkinlik, sahte mağaza | Başlamadı |
| 6 | Yayın hazırlığı: performans, Android ve WebGL derlemeleri, mağaza metinleri, gizlilik | Başlamadı |

**Kalite çıtası (ART_REFERENCE §10):** her ekranın her boyutu ≥ 4/5, iki bağımsız puanlayıcıyla. Görseller ücretsiz ve üretken yapay
zekâsız; kendi SVG sanatımız. Referans ekran görüntüleri yalnız stil rehberidir: yerleşim, bileşen anatomisi ve renk havası kopyalanabilir;
varlık, karakter, logo ve ad kopyalanmaz.

**Model dağılımı (D-095, D-098):** orkestratör (Fable) yalnız yönetir; zor port, mimari ve şüpheci inceleme Opus; uygulama ve düzeltme
Sonnet; belge, kopya ve mekanik iş Haiku.

**Yayın kanalı ve gizli değerler**

- Oyun: https://yilmazfatih-cyber.github.io/minik-usta-unity/ (her `main` itişinde `Unity WebGL → Pages` koşusu).
- Lisans: repo secret'ları `UNITY_EMAIL` ve `UNITY_PASSWORD` (Personal koltuk, GameCI). `UNITY_LICENSE` kullanılmaz; ayrıntı [`docs/CI.md`](docs/CI.md) §1.
- Son yeşil koşular: `41353265515` (`acc3f82`), `41353361615` (`81a314a`).
