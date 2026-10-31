# Minik Usta — Unity taşıması

Minik Usta'nın (dikey ekranlı blok yerleştirme bulmacası) Unity 6 sürümü. Kaynak gerçek **web reposudur**
(`yilmazfatih-cyber/minik-usta`, yerelde `../minik-usta`): `src/core/**` (11 400 satır saf TypeScript), `docs/GDD.md`
(K-01…K-54), `docs/TECH_DESIGN.md` §2–§6 ve §2R, `levels/*.json`, `tests/golden/*.json`. Bu repo o davranışı C#'a
**satır satır** taşır; yeni mekanik icat edilmez, belirsizlikte TS kodu esastır.

## Bağlayıcı kararlar (web reposu `docs/DECISIONS.md`)

| Karar | Özet |
| ----- | ---- |
| D-091 | Mevcut mekanik birebir taşınır; **match-3 yok**. Brifteki match-3 maddeleri karşılıklarına çevrilir (sürükleme hissi, kötü geçiş geri sekmesi, şantiye düşüşü, kilit parlaması, Altın Mala, kamyon teslimatı, takılma nabzı, Bonus İnşaat). |
| D-092 | Kahraman **Tuna** kalır (D-044, D-087); "genç kral" uygulanmaz. |
| D-093 | Çekirdek **saf C#** (UnityEngine bağımlılığı yok); bulutta .NET SDK + NUnit ile derlenir ve test edilir, aynı test dosyaları Unity Test Framework (EditMode) altında çalışır. Görünüm katmanı proje sahibinin Unity kurulumunda derlenir. |
| D-094 | Unity projesi bu ayrı repoda yaşar; web (Phaser) sürümü `main` = de6f16c olarak yayında kalır, görsel v3 web'de dondurulur. |

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
│  │  │  ├─ Level/                 LevelData, LevelJson (zod şemasının elle yazılmış karşılığı), Logic (K-45 doğrulayıcı), Load (loadLevel), Compile, Plan, Mechanics
│  │  │  └─ Obstacles/             engel eklenti arayüzü (Types), kayıt + kanca birleştirme (Registry), W1_StaticGap, Y3_Chain, S1_SlidingSite
│  │  ├─ Presentation.Core/        motor-bağımsız sunum (Faz 3 Q0; asmdef noEngineReferences): Tween/ (Easing, Tween, Sequence, TweenClock),
│  │  │                            Feel/ (FeelSettings, FallMotion, StuckPulse), Layout/ (LayoutSettings, BoardLayout, ScreenFit, HitTest),
│  │  │                            Input/ (DragMath, DragMapper), Animation/ (AnimationStep, AnimationPlanner, AnimationQueue)
│  │  │                            Faz 3 Q1: Game/ (LevelPlayer, StepRunner, PieceViews, BoardScene, BoardPose, ShadowLook (K-18),
│  │  │                            HudLayout, LevelCatalog, DebugSolver, ILevelView/IPieceVisual), Art/ (Atlas: prosedürel doku,
│  │  │                            Symbols, Palette, Rgba), Text/ (Strings: tr.json/en.json, TrText.Upper)
│  │  │                            Faz 3 Q2 (his): Game/ (Particles (ParticleField), CellWave, Haptics, PressFeel, TruckModel, Economy,
│  │  │                            StepRunner.Fx), Art/ (kıvılcım, toz, yıldız, sikke, mala, halka karoları)
│  │  ├─ Unity/                    ince Unity katmanı (asmdef MinikUsta.Unity → Core, Presentation.Core, UnityEngine.UI): GameBootstrap
│  │  │                            ([RuntimeInitializeOnLoadMethod]), GameRunner (kare döngüsü), UnityLevelView (ILevelView), UnityPieceVisual,
│  │  │                            UnityBoardView/UnityShadowView, UnityHud (uGUI + LegacyRuntime), PointerInput (eski Input), ProceduralSprites,
│  │  │                            Q2: UnitySpark/UnityUiSpark (parçacık), UnityTruckView, OffsetTarget (kamera, şantiye katmanı), PressableButton
│  │  ├─ Resources/                levels/ (oyunun okuduğu bölümler), config/economy.json, i18n/{tr,en}.json, solutions/ (dev: çözümü oynat)
│  │  ├─ Scenes/Level.unity        yalnız ortografik Main Camera (elle yazılmış Unity 6 YAML; .meta GUID sabit)
│  │  ├─ Presentation.Core.Tests/  NUnit 3 testleri (Editor; Golden/tokens.json = web tokens kopyası)
│  │  └─ Core.Tests/               NUnit 3 testleri (Editor platformu; Unity EditMode ile aynı dosyalar)
│  │     ├─ Fixtures/              Builders (tests/fixtures/builders.ts), Boards (ortak tahta yardımcıları, Drop/Play), Cover (cover.ts), ObstacleFixtures, J (JSON literal), TestPaths, RuleCoverage (P4 kapsam kapısı)
│  │     │  └─ Levels/{valid,invalid}/  K-45 doğrulayıcı fikstürleri (web tests/level/fixtures kopyası)
│  │     ├─ Obstacles/             RegistryTests, W1StaticGapTests, S1SlidingSiteTests, Y3ChainTests, S2PlanVoidTests
│  │     └─ Golden/                web reposundan kopya: level_NNN.hand.json, level_NNN.solver.json; TS vektörleri p0…p3_vectors.json; rule_ids.json (kural kimlikleri)
│  └─ StreamingAssets/MinikUsta/
│     ├─ levels/                   level_001…010.json (web reposundan birebir kopya)
│     └─ config/economy.json       güçlendirici açılış seviyeleri (L-17 `booster:` / `pre:` vurguları)
├─ dotnet/                         Unity dışı derleme ve test (D-093)
│  ├─ MinikUsta.sln
│  ├─ MinikUsta.Core/              Assets/MinikUsta/Core/**/*.cs → netstandard2.1, LangVersion 9, Nullable, TreatWarningsAsErrors
│  ├─ MinikUsta.Core.Tests/        Assets/MinikUsta/Core.Tests/**/*.cs → net8.0, NUnit 3.14 + NUnit3TestAdapter + Microsoft.NET.Test.Sdk
│  ├─ MinikUsta.Presentation.Core/ Assets/MinikUsta/Presentation.Core/**/*.cs → netstandard2.1, LangVersion 9 (Core'a başvurur)
│  ├─ MinikUsta.Presentation.Core.Tests/  Assets/MinikUsta/Presentation.Core.Tests/**/*.cs → net8.0, NUnit 3.14
│  ├─ UnityStubs/                YALNIZ DERLEME DENETİMİ: Unity katmanının kullandığı UnityEngine / UI / EventSystems / Events /
│  │                             SceneManagement üyelerinin Unity 6000.0 imzalı boş gövdeli kopyaları (Unity'nin kendisi değil)
│  └─ MinikUsta.Unity/           Assets/MinikUsta/Unity/**/*.cs → UnityStubs'a karşı derlenir (yazım + imza denetimi; D-093)
├─ tools/
│  ├─ sync-from-web.sh             levels, golden, K-45 fikstürleri, economy.json ve tokens.json dosyalarını web reposundan kopyalar
│  ├─ gen-p0-vectors.ts            TS çekirdeğinden bit-eşitlik vektörleri üretir (node 22, ek paket yok)
│  ├─ gen-p1-vectors.ts            hareket / yerçekimi / yerleşim / teslimat vektörleri (10 bölüm + 9 geometri × 5 rastgele tahta)
│  ├─ gen-p2-vectors.ts            doğrulayıcı / hedef / güçlendirici / çıkmaz / özet / kayıt vektörleri (fikstürler, kanonik çözüm boyunca 90 durum, 40 cover tahtası)
│  ├─ gen-p3-vectors.ts            GameSession dizileri: her hamle türü, geri al, +5, ret, çıkış, replay (170 dizi, ~4 300 girdi)
│  ├─ gen-rule-ids.ts              web belgelerinden kural kimlikleri + fazları → Core.Tests/Golden/rule_ids.json (kapsam kapısı)
│  └─ diff/ts-oracle.mjs           diferansiyel fuzz kehaneti: TS GameSession ile ~400 tohumlu / betikli dizi → tools/diff/out/ (git dışı)
└─ docs/PORT_MAP.md                TS dosyası → C# dosyası eşleme tablosu (her paket günceller)
```

`ProjectSettings/ProjectVersion.txt` (6000.0.85f1), `Packages/manifest.json`, `ProjectSettings/ProjectSettings.asset`
(yalnız gerekli alanlar) ve `ProjectSettings/EditorBuildSettings.asset` (Level sahnesi) bulut derlemesi için eklendi
(`docs/CI.md`); kalan ayarları Unity ilk açılışta üretir. `.gitignore` Unity'nin ürettiği `Library/`, `Temp/`, `Logs/` vb. klasörleri dışlar.

## Komutlar

| Komut | Ne yapar |
| ----- | -------- |
| `cd dotnet && dotnet build -warnaserror` | Çekirdek + testleri derler; uyarı = hata |
| `cd dotnet && MINIK_PREVIEW_DIR=../artifacts/preview dotnet test --filter PreviewTests` | Unity yer tutucu ekranının yazılımsal önizlemesi (aynı atlas, renk, sıra, 9-dilim; metin yok) → 390×844 ve 360×800 PNG (`artifacts/`, git dışı). Unity ekran görüntüsü DEĞİL |
| `cd dotnet && dotnet test` | Tüm NUnit testlerini koşturur (testler repo kökünü `Assets/MinikUsta/Core/MinikUsta.Core.asmdef` ile bulur ve bölüm / altın / fikstür JSON'larını oradan okur) |
| `tools/sync-from-web.sh [/yol/minik-usta]` | Bölüm, altın, fikstür, economy.json ve tokens.json dosyalarını web reposundan yeniler (varsayılan `../minik-usta`); Faz 3 Q1'den beri bölümleri, economy.json'u, `src/i18n/{tr,en}.json`'u ve kanonik çözüm günlüklerini `Assets/MinikUsta/Resources/` altına da kopyalar (oyun `Resources.Load<TextAsset>` ile okur; StreamingAssets kopyası çekirdek testleri içindir) |
| `node tools/gen-p0-vectors.ts [/yol/minik-usta]` | `Core.Tests/Golden/p0_vectors.json` dosyasını TS çekirdeğinden yeniden üretir |
| `node tools/gen-p1-vectors.ts [/yol/minik-usta] [--verbose]` | `Core.Tests/Golden/p1_vectors.json` dosyasını (P1 modülleri: R, K-07 sınıfları, düşüşler, kararlar, K-08 takip taraması, K-17 dönüş hedefleri, settleYard) yeniden üretir; `--verbose` her satırı `p1_vectors.verbose.json` dosyasına yazar (git dışı, uyuşmazlık teşhisi için) |
| `node tools/gen-p3-vectors.ts [/yol/minik-usta] [--verbose]` | `Core.Tests/Golden/p3_vectors.json` dosyasını yeniden üretir: TS `GameSession.apply` ile 170 tohumlu deneme (sürükleme, çekiç, vinç, boya fırçası, Altın Mala, eski mala, geri al, +5 jeton/reklam, ret, çıkış) oynanır; girdiler saklanır, C# aynı girdileri oynatıp her paketin sonucunu, `eventLogHash`'ini, durum karmasını, oturum alanlarını ve `TurnSummary`'yi karşılaştırır; `--verbose` her satırı ve her paketin kanonik olay JSON'unu `p3_vectors.verbose.json` dosyasına yazar (git dışı). Uyuşmayan dizinin C# satırları `Golden/p3_csharp.<dizi>.txt` dosyasına yazılır (git dışı) |
| `node tools/gen-rule-ids.ts [/yol/minik-usta]` | `Core.Tests/Golden/rule_ids.json` dosyasını web reposunun kapsam aracıyla (`tools/rule-coverage.ts` `ruleIds`: GDD başlıkları, TECH §12.4 F sütunu, OBSTACLES imza tablosu ve matrisi) yeniden üretir; `RuleCoverageTests` Faz 2R'ye kadar her kimliğin (76) en az bir C# test adında geçtiğini denetler |
| `node tools/diff/ts-oracle.mjs [/yol/minik-usta] [--seeds N] [--moves M] [--out DIR]` | Diferansiyel fuzz kehaneti: web `src/core`'u içe aktarıp tohumlu **geçerli** eylem dizileri (DragSession R'sinden iptal etmeyen sürüklemeler, geçerli hedefli güçlendiriciler, Undo, +5, ret, çıkış) ve betikli senaryolar (Söküm, D1 karıştırma, kuyruk, teklif, Undo) oynar; her adımın Zobrist / tampon / olay karmasını, olay JSON'larını, ASCII'sini ve oturum alanlarını `tools/diff/out/*.json`'a yazar (git dışı). `DiffFuzzTests` aynı eylemleri C#'ta oynatıp her adımda karşılaştırır; ilk sapmada adım, eylem, ilk farklı olay ve iki tahta yan yana. Çıktı yoksa bu testler `Ignore` olur — `dotnet test`'ten önce koşturun |
| `node tools/gen-p2-vectors.ts [/yol/minik-usta] [--verbose]` | `Core.Tests/Golden/p2_vectors.json` dosyasını (K-45 doğrulayıcı sonuç listeleri, D1/D2/D3a/Söküm/reshuffle, K-33/36/37/38 hedef kümeleri, K-34 kanca 5, hedef sayaçları, özet, kayıt) yeniden üretir; durumlar TS `applyMove` ile kanonik çözüm boyunca üretilip `encodeState` ile saklanır, C# aynı tamponu çözer; `--verbose` her satırı `p2_vectors.verbose.json` dosyasına yazar (git dışı) |

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

**Durum (Q2):** Q0 (motor-bağımsız sunum çekirdeği), Q1 (oynanabilir tahta, yer tutucu görsel) ve Q2 (his katmanı: sprite
parçacıkları, iniş dalgası, mala ve seri boncukları, kamyon ve şantiye kayması, Bonus İnşaat sikkeleri, kazanma yıldızı, düğme
basışı, kamera sarsıntısı, Android titreşimi) tamam. Ses ve gerçek sanat sırada. Bu ortamda Unity derlenemedi; ilk gerçek derleme GitHub Actions'ta yapılır
([`docs/CI.md`](docs/CI.md)). Rapor taslağı: [`docs/PHASE3_REPORT.md`](docs/PHASE3_REPORT.md).

### Nasıl oynanır

- Adres: https://yilmazfatih-cyber.github.io/minik-usta-unity/ (ilk başarılı derlemeden sonra açılır).
- Açılışta doğrudan Bölüm 1 gelir; menü yok.
- Bir bloğa bas ve parmağınla sürükle. Blok kalkar ve parmağını izler; doğru yere bırakılan blok yerine oturur.
- Yanlış yere bırakılan blok kendi yerine geri döner. Kırmızı işaret ve puan kaybı yok.
- Bloklar duvarın üstünden ya da geçitten geçirilebilir.
- Hamleler bitince "Hamleler bitti!" penceresi çıkar; tek seçenek **Tekrar dene** (Q1'de +5 hamle teklifi yok).
- Bölüm tamamlanınca "Kazandın!" penceresi çıkar; **Devam** sonraki bölümü açar. 10. bölümden sonra 1. bölüme dönülür.
- Duraklatma simgesi (ekranın üst kısmı) "Mola" penceresini açar: **Devam** ya da **Bölümü baştan başlat**.
- Geliştirme derlemesinde (Editor ya da Development build) klavye kısayolları açılır: N sonraki bölüm, R baştan,
  A tahtanın ASCII hâlini Console'a yazar, S çözümün sonraki hamlesini oynatır. Yayındaki sürümde bunlar kapalıdır.

### Unity'de açma ve testler

1. Unity Hub'da **6000.0.85f1** sürümünü kur (sürüm `ProjectSettings/ProjectVersion.txt` ile sabit).
2. Hub → **Projects** → **Add** → bu repo klasörünü seç.
3. İlk açılış birkaç dakika sürer. Unity eksik `.meta` dosyalarını üretir; `git status` ile değişenleri gözden geçir.
4. `Assets/MinikUsta/Scenes/Level.unity` dosyasını aç ve **Play**'e bas.
5. Testler: **Window → General → Test Runner → EditMode → Run All**. Koşan assembly'ler: `MinikUsta.Core.Tests`
   ve `MinikUsta.Presentation.Core.Tests`. Beklenen sayılar dotnet koşusuna göre: Presentation 283 geçer. Core, `tools/diff/out`
   (git dışı, `ts-oracle.mjs` çıktısı) varsa 1455 geçer ve 5 atlar; yoksa 1049 geçer ve 7 atlar (DiffFuzz durumları üretilmez).
   Unity içinde bu koşu henüz yapılmadı.

Dotnet tarafı (Unity gerekmez):

```
cd dotnet && dotnet build -warnaserror && dotnet test
```

### Bilinen eksikler (Q2 sonrası)

- Ses yok. Titreşim yalnız Android'de (`Handheld.Vibrate`, tek sabit darbe). ↩ iptal rozeti, ip/eğilme ve sürükleme izi yok.
  Q2 ile gelenler: sprite parçacıkları, kamyon, seri boncukları ve mala, şantiye kayması, Bonus İnşaat sikkeleri, kazanma yıldızı,
  düğme basışı, kamera sarsıntısı (hepsi yer tutucu çizimle; Unity'de görülmedi).
- Öğretici yok. Güçlendiriciler devre dışı; Geri Al düğmesi yok; +5 hamle teklifi yok.
- Görseller yer tutucu: prosedürel renkli kutular ve 8 renk sembolü. Tuna çizimi yok.
- Ayarlar yok; "Animasyonları azalt" kapalı.
- Yalnız Türkçe metin bağlı (`en.json` henüz okunmuyor).
- LegacyRuntime fontunda Türkçe harfler (ş, ğ, ı, İ) Unity'de henüz görülmedi.
