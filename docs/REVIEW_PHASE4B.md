# Faz 4B şüpheci inceleme — D-099 sabit saha, 10 yeni bölüm, Usta Kral ve 3D katman (REVIEW_PHASE4B)

İncelenen: Unity `main` `e4be42e` (A3 `028533f`, B1k `0fda183`, B1 `c0a336a`, B2 `143d3ab` + `e4be42e`, C1 `58e1700` … `6ce48d7`);
web `wip/phase-2r` `c1facf4` (A1, K-55 çekirdeği) ve `wip/a2-levels` `e80a318` (A2, 10 bölüm). İnceleyen: code-lead (bağımsız tur,
2026-11-02). Kod değiştirilmedi; yalnız bu belge eklendi. Önem: **Engel** = iş geri döner · **Önemli** = aynı sprintte kapanır ·
**Öneri** = birikim listesi.

**Sonuç: Engel yok.** TS ↔ C# eşdeğerliği, K-55 ("dilim geçişinde sahaya blok gelmez") ve telif ayrımı kanıtla geçer. 8 Önemli
bulgu var. En ağırları: (1) web kaynak dalı kırmızı; (2) bölüm ölçütleri şartname bantlarının dışında ve JSON `targets` ölçülen
değerin çevresine yazılmış; (3) WebGL indirmesi 48,6 MB ve sıkıştırılmamış; (4) ilk kullanımda eşzamanlı PNG çözme takılması.

## Yöntem ve kapılar (bu turda kendim koşturdum)

Her şey kendi worktree'lerimde yapıldı (Unity `origin/main` `e4be42e`, web `origin/wip/a2-levels` `e80a318`); ana çalışma ağaçlarına
dokunulmadı.

| Komut | Sonuç |
| --- | --- |
| `node tools/diff/ts-oracle.mjs <web e80a318>` | 129 dosya, 655 dizi, 16 376 eylem (drag 13 028, undo 752, +5 591, vinç 876, boya 554, mala 349, çekiç 71) |
| `cd dotnet && dotnet build -warnaserror` | 0 uyarı, 0 hata |
| `dotnet test --no-build` | Presentation.Core 504/504; Core **1882 geçti, 5 atlandı, 0 kaldı** (DiffFuzz 655 dizi, 0 sapma) |
| `dotnet test --filter HandGolden\|FixedYard` | 89/89 (yayın 10 + arşiv 10 bölümün `eventLogHash`, `finalAscii`, `levelHash`'i TS ile eşit) |
| `bash tools/stub-gate-selftest.sh` | 4/4 durum beklendiği gibi |
| web `npm run levels:validate` | 10 dosya, 0 hata, 0 uyarı |
| web `npm run levels:solve` (önbellek silinerek) | 10/10 çözüldü, 0 hata, 0 uyarı, 48 sn; değerler A2 tablosuyla birebir (durum sayıları dahil) |
| web `npm run typecheck` | 0 hata |
| web `npx vitest run` | **160 kırmızı / 1985 geçti / 7 atlandı** (36 dosya) — P4B-01 |
| web `npm run test:rules` (2R) / `--phase 3` | 76 kimlik tamam / K-55 ve E-62…E-67 kapsamda (eksik 51 kimlik Faz 3 içerik kuralları) |
| `gh run list` | son WebGL koşusu `41386489687` (`e4be42e`) yeşil: build 4 dk 54 sn, Pages deploy, ekran görüntüleri; Android iş akışının **hiç koşusu yok** |
| `git archive origin/ci-shots latest` | `manifest.json` `e4be42e`, `ok: true`, iki görünümde 7/7 ekran, `errors: []` |

Erişilemeyen: `docs.unity3d.com` (DNS yok) ve `yilmazfatih-cyber.github.io` (vekil 403). API imzaları UnityCsReference `6000.0`
dalından (`raw.githubusercontent.com`) okundu; Pages aktarım boyutu ölçülemedi.

## Özet

| Kimlik | Önem | Yer | Konu |
| --- | --- | --- | --- |
| P4B-01 | Önemli | web `wip/a2-levels`, Unity `Resources/levels` | Unity main'in veri kaynağı olan web dalı kırmızı; ana web dalında hâlâ v1 bölümler |
| P4B-02 | Önemli | web `levels/*.json` `targets`, LEVELS §6.6/§6.10 | Ölçütler şartname bantlarının dışında; bantlar ölçülen değere göre yazılmış; Bölüm 1–9 açgözlü oyunla kazanılıyor |
| P4B-03 | Önemli | web `levels/level_010.json` | Bölüm 10: `greedyWin` 0, P = 1, orta bot yok; bölüm sonu duvarı riski ölçülmedi |
| P4B-04 | Önemli | `CiBuild.BuildWebGL`, Resources art | WebGL çıktısı 48,6 MB, sıkıştırma kapalı; 3D sanat `.data`'yı 10,2 → 20,3 MB yaptı |
| P4B-05 | Önemli | `Unity/ClipSprites.cs`, `GameRunner.cs` | Sayfalar ilk kullanımda eşzamanlı çözülüyor: ilk doğru yerleşimde fx_0, ana ekranda king_0; `Warm` hiç çağrılmıyor |
| P4B-06 | Önemli | `.github/workflows/unity-android.yml`, `CiBuild.BuildAndroid` | Android yolu hiç derlenmedi |
| P4B-07 | Önemli | web `src/i18n/{tr,en}.json` | `obs.s1.desc` D-099 ile çelişiyor; "Dede" kalıntıları; ölü `tut.ctx.reshuffle` |
| P4B-08 | Önemli | `Resources/art3d/king.json`, ci-shots | 3D animasyon akıcılığı kanıtsız: klip başına 7–16 çizim; CI'da yalnız durağan kare |
| P4B-09 | Öneri | `GameRunner.cs`, `ShotScenario` | `?shots=1` ve çözüm kayıtları üretim derlemesinde |
| P4B-10 | Öneri | `Feel/FeelSettings.cs` | `DragTiltMaxDeg` 8, token `drag.tiltMaxDeg` 4'ü atlıyor; token testi ölü alanı denetliyor |
| P4B-11 | Öneri | `Core.Tests/FixedYardTests.cs` | K-55 değişmezi yalnız P1 fikstüründe açıkça sınanıyor; içi boş bir iddia var |
| P4B-12 | Öneri | LEVELS §6.6 | 10/10 bölümde ikinci kat kazısız ("by segment X+0") |
| P4B-13 | Öneri | iş akışları, `docs/CI.md` | Bekleyen Android işi yeni bir WebGL itişiyle iptal olur; CI.md'de eski satır |
| P4B-14 | Öneri | ci-shots | Draw call ve batch sayısı ölçülmüyor |
| P4B-15 | Öneri | ana ekran ve mağaza görselleri | Telif: Engel yok; kral + insan + köpek dizilimi ve EN adı için önlem |
| P4B-16 | Öneri | tahta görseli (Bölüm 1) | Boş saha sütunu kalas dokusuyla dolu gibi okunuyor |
| P4B-17 | Öneri | Unity katmanı (Android) | `Screen.safeArea` ve Android geri tuşu işlenmiyor |

## Bulgular

### P4B-01 — Önemli — Unity main'in veri kaynağı kırmızı bir web dalı

**Kanıt.** Unity `Resources/levels/*.json`, `StreamingAssets/…/levels/*.json` ve `Core.Tests/Golden/level_*.{hand,solver}.json`
web `e80a318` (`wip/a2-levels`) ile bayt bayt aynı (`cmp`, 10 + 20 dosya); `PORT_MAP.md` ve vektörlerin `webCommit` alanı
`e80a318` diyor. Bu dalda `vitest` **160 test / 36 dosya kırmızı** (ilk sıralar: `tools/solver/levels` 22,
`review/data.review` 21, `review/presentation.review` 16, `scenes/tutorial` 13, `core/moves` 9). Web'in ana dalı
`wip/phase-2r` (`c1facf4`) hâlâ Faz 2R'nin v1 bölümlerini taşıyor. Yani iki repo şu an farklı bölüm verisini doğru kabul ediyor.
Kırmızıların hepsi fikstür bağımlı (A2'nin A/B/C listesi). K-55 çekirdek testlerinden kırmızı olan tek test
`fixedYard.test.ts` E-65: `levels/level_005.json`'u v1 sanıyor, dosya artık v2. C# ikizi `Archive2R`'den okuduğu için geçiyor.

**Düzeltme.** (1) code-lead (web): A2'nin test paketi A/B/C uygulanır, Faz 2R verisi `tests/fixtures/levels-2r/` arşivine alınır.
(2) `wip/a2-levels`, `npm run check` yeşilken `wip/phase-2r`'ye birleştirilir. (3) Unity `tools/sync-from-web.sh` birleşik daldan
yeniden koşar; `PORT_MAP.md` kaynak satırı birleşik commit'e çevrilir. Veri değişmezse Unity'de fark çıkmamalıdır, bu da kanıttır.

### P4B-02 — Önemli — Bulmaca ölçütleri şartname bantlarının dışında, `targets` ölçülen değere göre yazılmış

**Kanıt (çözücüyü kendim koşturdum, A2 tablosuyla aynı).** LEVELS §6.6 Tablo B ve §6.2 ile karşılaştırma:

| Ölçüt | Şartname | Gerçek |
| --- | --- | --- |
| `greedyWin` | Normal 0,30–0,70; 5: 0,40–0,70, 7: 0,30–0,60 | Bölüm 1–9'un **hepsinde 1,00** |
| `greedyWin` | Bölüm 10: 0,05–0,25 | 0,00 |
| `forced` | Kolay 0–2, Normal 2–4, Bölüm 10 5–8 | 4, 5, 6, 8, 8, 7, 9, 7, 11, 13 (**10/10 bant üstü**) |
| `choices@0` | Kolay ≥ 3, Normal ≥ 4, Zor ≥ 5 | Bölüm 2: **1**, Bölüm 6: 3, Bölüm 10: 2 |
| `min` | Bölüm 10: 22–26 | 13 |
| `ahaMin` | Bölüm 10: ≥ 2 | 1 |

JSON `targets` bu değerlerin çevresine yazılmış (ör. Bölüm 10 `min [13,14]`, `ahaMin [1,2]`; Bölüm 5 ve 7 `greedyWin [0.5,1]`).
Bu yüzden `metric_out_of_band` 0 uyarı veriyor; kapı kendini doğruluyor. `puzzle_greedy_easy` yalnız `ahaMin < 1` iken,
`puzzle_greedy_wins` yalnız Zor'da çalıştığı için `greedyWin` 1,00 olan Normal bölümlerin hiçbiri uyarı almıyor.
Sonuç: Bölüm 1–9'da hiç plan yapmayan, her an en yakın doğru yerleşimi seçen oyuncu bütçe içinde kazanıyor. Aha anları
(5: 1, 7: 2, 9: 2) yalnız 1–3 hamlelik payı tüketiyor. Proje sahibinin "bölüm zekâsını artır" isteği bu ölçütle kanıtlanmış
sayılmaz. §6.10'daki gerekçeler (durum uzayı sınırı, U-4 yok) teknik olarak doğru, ama karar belgesi yok.

**Düzeltme (product-lead, karar orkestratörün).** Ya (a) Tablo B DECISIONS'ta resmen revize edilir ve proje sahibine "1. bölüm
açgözlü oyunla kazanılır" açıkça sorulur, ya da (b) `targets` yeniden Tablo B'ye eşitlenir ve bölümler ona çekilir. Ucuz ilk
adım A2'nin kendi önerisi: Normal P 2 → 1. Hesap (`moves = min + 1`): Bölüm 7'de `greedyMin` 13 > 12 olur ve açgözlü oyun kaybeder.
Bölüm 5'te 12 = 12 olur ve açgözlü oyun yine kazanır. Normal tanıtım bölümleri (6, 8, 9) Kolay satırındaki P 4'ü kullandığı
için değişmez. Kod tarafı (code-lead): `puzzle_greedy_easy` koşulu `greedyWin > 0.7` iken `ahaMin`'den bağımsız uyarsın (Normal,
tanıtım dışı). `targets` ile Tablo B arasında fark varsa `levels:solve` bir `targets_diverge_from_spec` notu yazsın.

### P4B-03 — Önemli — Bölüm 10 ölçülmemiş bir duvar olabilir

**Kanıt.** `level_010`: `min` 13, `moves` 14 (P = 1, a = 0), `greedyMin` 15, `greedyWin` 0,00, `choices@0` 2, `forced` 13/13 karar
noktası (her noktada tek doğru). Hedef kazanma oranı (orta bot) %45; `levels:bot` yok (CLAUDE.md'de `levels:check` = validate +
solve + bot, `package.json`'da yalnız `solve --check`). Bölüm 10, hikâye bölümünün finali; kaybedilirse can gider ve +5 teklifi
900 altın.

**Düzeltme.** Yayından önce orta bot ile ölç (WP-N). Bot yoksa `a` = 1 (`moves` 15) K-52 bandı [14, 16] içinde kalır. Bu durumda
en şanslı açgözlü oyuncu (`greedyMin` 15) tam sınırda kazanır ve `greedyWin` 0'ın üstüne, hedef bant 0,05–0,25'e doğru çıkar.
Doğru değer bot ölçümüyle seçilir.

### P4B-04 — Önemli — WebGL indirmesi 48,6 MB, sıkıştırma kapalı

**Kanıt (koşu `41386489687`, "Verify WebGL output").** `Build/MinikUsta.data` 20 323 050 B, `MinikUsta.wasm` 27 790 131 B,
`framework.js` 415 851 B. Build Report "Complete build size 46.4 mb". `CiBuild.BuildWebGL` `compressionFormat = Disabled`,
`decompressionFallback = false`. Karşılaştırma: C1 son koşusu (`41365297299`) `.data` 10 210 120 B; B1 sanatından sonra (`41380744110`)
20 279 880 B. Büyüme, Resources'taki PNG'lerden geliyor: `king_0` 5,28 MB, `atlas_0/1` 2,76 + 2,89 MB, `fx_0` 1,18 MB.
PNG zaten sıkışık olduğu için gzip bunları küçültmez. FTUE çıtası ilk açılıştan Bölüm 1'e ≤ 10 sn; 48,6 MB, 20 Mbit/s bağlantıda
tek başına yaklaşık 19 sn eder (hesap, ölçüm değil). Pages'in `.wasm`'ı anında gzip'leyip gzip'lemediği bu oturumdan
ölçülemedi.

**Düzeltme (code-lead).** (1) `compressionFormat = Gzip` + `decompressionFallback = true`. Unity, `Content-Encoding` başlığı
göndermeyen sunucularda dosyayı JS ile açar; `PlayerSettings.WebGL.decompressionFallback` UnityCsReference 6000.0'da var.
`.wasm` tipik olarak 3–4× küçülür; ölçüp CI.md'ye yaz. (2) Ayrı bir CI denemesinde Managed Stripping High ve WebGL "Code
Optimization: Disk Size" (Unity 6 ayar adı Editor'de doğrulanmalı). (3) design-lead + code-lead: `king_0` (1964×2012) çizimleri
ekranda en çok ~200 dp; 2× yoğunlukla render ölçeğini %70'e indirmek sayfayı yaklaşık yarıya düşürür. (4) Bütçe kapısı:
`Verify WebGL output` adımı `.data + .wasm` toplamı 25 MB'ı (sıkıştırılmış) aşarsa `::warning::` yazsın.

### P4B-05 — Önemli — İlk kullanımda eşzamanlı PNG çözme takılması

**Kanıt (`ci-shots/latest/console-390x844.log`, CI masaüstü Chromium).** `art3d decode art3d/king_0 1964x2012 164 ms` ana ekran
açılışında; `art3d decode art3d/fx_0 1920x752 47 ms` **`drag_mid` ile `landed` arasında**, yani oyuncunun ilk doğru
yerleşiminin (kilit parlaması) karesinde. 360×800'de 159 ms ve 49 ms. 4× CPU yavaşlatmada tahminen ~0,65 sn ve ~0,2 sn takılma
olur; "dokunuş ile tepki arasında tek kare" ve 60 FPS çıtasını en görünür anda kırar. `ClipSprites.Warm(string)` var ama
hiçbir yerden çağrılmıyor (`grep Warm(` → yalnız tanım).

**Düzeltme.** `GameRunner.Awake` → `ClipSprites.Load()`'dan hemen sonra, yükleme ekranı sürerken `ClipSprites.Warm("king")`.
Bölüm başlangıç penceresi açılınca, oyuncu "Oyna"ya basmadan önce `Warm("fx")`. `npm run perf` benzeri kapıya
`[minik] art3d decode` toplamını (≤ 250 ms masaüstü) ekle. Uzun vadede ASTC/ETC2 sıkıştırmalı içe aktarılmış dokular çözmeyi
tamamen kaldırır ve 62,6 MB'lık GPU belleğini yaklaşık 8× düşürür. Bunun bedeli platforma göre doku hedefi (P3-11 ile birlikte
değerlendirilmeli).

### P4B-06 — Önemli — Android yolu hiç derlenmedi

**Kanıt.** `gh run list -w "Unity Android → APK"` boş. `CiBuild.cs` dotnet derlemesine girmiyor (P3-14 sürüyor). Bu turda
`BuildAndroid`'in kullandığı editör API'lerini UnityCsReference 6000.0 ile tek tek doğruladım. Hepsi var ve imzalar eşleşiyor:
`PlayerSettings.SetApplicationIdentifier(NamedBuildTarget, string)`, `SetScriptingBackend(NamedBuildTarget, ScriptingImplementation)`,
`Android.targetArchitectures` (`ARMv7 = 1<<0`, `ARM64 = 1<<1`), `Android.bundleVersionCode`, `useCustomKeystore`,
`keystoreName/Pass`, `keyaliasName/Pass`, `EditorUserBuildSettings.buildAppBundle`, `exportAsGoogleAndroidProject`,
`defaultInterfaceOrientation`, `bundleVersion`. Derleme yine de kanıtlanmış değil (imaj, SDK hedef API düzeyi, IL2CPP süresi, disk).

**Düzeltme.** İtiş yokken bir kez `workflow_dispatch` (androidPackage) koşturulsun (P4B-13). APK bir telefonda açılıp
`docs/CI.md` §8'e koşu numarası, süre ve APK boyutu yazılsın. Play hedef API düzeyi (2026 için en az 35) Unity'nin seçtiğiyle
karşılaştırılsın.

### P4B-07 — Önemli — i18n: D-099 ile çelişen ve D-101 ile eskiyen metinler

**Kanıt (web `src/i18n`, Unity `Resources/i18n` kopyası aynı):**
- `obs.s1.desc` TR "Bu kat bitince şantiye kayar, kamyon malzeme getirir." / EN "…the truck delivers." D-099'da kamyon malzeme
  getirmez ve S1 Bölüm 1'de öğretiliyor. Unity bugün `obs.*.desc` göstermiyor (`grep` boş), web görseli donduruldu (D-094); bu
  yüzden Engel değil, ama ilk engel kartı bu metni gösterir.
- `lose.offer.gift` TR "Usta Dede'den hediye" / EN "a gift from Grandpa". Unity `unity_tr.json` / `unity_en.json` ile Usta Kral'a
  örtülü (doğru), web kaynağı eski. `story.prologue.p2.dede` konuşmacı anahtarı.
- `tut.ctx.reshuffle` "Kamyon sahayı yeniden diziyor" — `RULES_VERSION` 3'te yeniden dizme yok (K-55 madde 8); ölü anahtar.
- Kontrol: TR 259 = EN 259 anahtar, eksik yok. Unity Presentation/Unity katmanında oyuncuya giden sabit metin yok; Türkçe
  karakterli tek literaller yorum satırlarında.

**Düzeltme.** design-lead: `obs.s1.desc` için OBSTACLES S1'deki D-099 metni ("Kat bitince şantiye kayar; bütün bloklar baştan
sahada."). `lose.offer.gift` ve prolog konuşmacısı web'de Usta Kral'a çevrilsin, sonra Unity örtüsü kaldırılsın.
`tut.ctx.reshuffle` silinsin ya da "Söküm" metnine bağlansın. code-lead: `i18n.test`'e "v2 bölümünde kullanılan anahtarlarda
'kamyon … getir' geçmez" denetimi.

### P4B-08 — Önemli — 3D animasyonun Royal Match akıcılığı kanıtsız

**Kanıt.** `king.json` klipleri 24 fps zaman çizelgesinde az sayıda benzersiz çizim kullanıyor: `king_idle` 48 kare / 9 çizim
(~4,5 çizim/sn), `king_cheer` 24/10, `king_wave` 24/8, `king_point` 18/7, `king_big_cheer` 48/16. Üstüne 2.5D katmanı nefes, oturma
ezilmesi ve pop ekliyor (`ClipActor`). Bu, sınırlı (çizgi film) animasyondur; sürekli 60 fps iskelet hareketi değildir.
ci-shots yalnız durağan kare veriyor; bu yüzden hareketin "Royal Match gibi" olduğu ne kanıtlanabilir ne çürütülebilir.
Proje sahibinin açık isteği "tüm animasyonları 3D Royal Match gibi yap".

**Düzeltme.** (1) Kanıt önce: `?shots=1` senaryosuna ana ekran `king_wave` ve kazanma `king_big_cheer` için 2 sn'lik 30 fps kare
dizisi eklensin. ci-shots'a şerit PNG + benzersiz kare sayısı yazılsın; design-lead rubrikle puanlasın. (2) Yetmezse ucuz
akıcılık yolu: kesme (cut-out) rig. Blender'dan baş (6 ifade), gövde, iki kol (3'er poz) ve baret ayrı render edilir, Unity'deki
mevcut tween motoru bunları 60 fps'te oynatır. Tahmini bellek `king_0`'ın dörtte biri. Çizim sayısını 2× artırmak +15 MB
RGBA32 getirir; önerilmez.

### P4B-09 — Öneri — `?shots=1` ve çözüm kayıtları üretimde

`ShotOptions.FromUrl(Application.absoluteURL)` hiçbir derleme simgesiyle korunmuyor. Bellek içi kayıt kullandığı için gerçek
kayda dokunmuyor. Hata ayıklama tuşları ise `Debug.isDebugBuild` ile korunuyor (doğru). `Resources/solutions/*.solver.json`
(çözümler) üretim paketinde. **Düzeltme:** sürücü `#if UNITY_WEBGL` içine alınsın (Android/iOS'ta derlenmesin), bu kabul edilmiş
risk CI.md'ye yazılsın. Çözüm dosyaları yalnız `?shots=1` ve geliştirme derlemesinde okunuyor; mağaza derlemesinde paketten
çıkarmak için `IPreprocessBuildWithReport` ile geçici taşıma yapılabilir.

### P4B-10 — Öneri — His sabiti token'ı atlıyor

`FeelSettings.DragTiltMaxDeg = 8` (aşma payıyla ±10°), token `drag.tiltMaxDeg = 4` (JUICE #3). `TiltMaxDeg = 4` "token testi için"
tutuluyor ama hiçbir kod okumuyor; `FeelSettingsTests` ölü bir alanı denetliyor. B2'nin 24 yeni his sabitinin hiçbiri
`tokens.json`'da yok. **Düzeltme:** design-lead değeri token'da 8'e çeker (ya da kod 4'e döner). Token testi kullanılan alanı
denetler. Yeni sabitler `tokens.json` `juice` altına taşınır ve testle eşitlenir.

### P4B-11 — Öneri — K-55 değişmez testi dar, bir iddia içi boş

`K55_no_block_enters_the_yard_200_seeded_random_sessions` yalnız P1 fikstüründe koşuyor. 10 yayın bölümünde değişmez açıkça
sınanmıyor, yalnız dolaylı kanıt var: kehanetin v2 dizilerinde 130 `siteShifted`, 0 `deliveryQueued`/`deliveryArrived`, TS ile
olay olay eşit. Aynı testte `Assert.GreaterOrEqual(teardowns, 0)` her zaman doğrudur. Ayrıca v2 yayın dizilerinde hiç `teardown`
yok (çözücü: bu bölümlerde ölü durum yok), yani Söküm yolu yalnız v1 senaryolarında (`+5` sonrası D1 Söküm 7 kez) sınanıyor.
**Düzeltme:** `DiffFuzzTests` v2 dizilerinde her adımda kilitsiz malzeme kümesinin başlangıcın alt kümesi olduğunu denetlesin.
Boş iddia kaldırılsın ya da `> 0` veren bir tohum seçilsin.

### P4B-12 — Öneri — İkinci kat her bölümde kazısız

`levels:solve` 10/10 bölümde `minShifts … (by segment X+0)` veriyor: bütün kazı ve park birinci katta, şantiye kaydıktan sonra
kalan bloklar doğrudan yerleşiyor. "Yandaki ekran geçse de" anında karar kalmıyor. **Düzeltme (product-lead):** Bölüm 11+
için en az bir bölümde ikinci katta da ≥ 1 kazı (ör. birinci katta yapılan park ikinci katın bloğunu örtsün). Ölçüt olarak
`shiftsBySegment[1] ≥ 1` `targets`'a eklensin.

### P4B-13 — Öneri — Koltuk grubu ve CI belgesi

`unity-webgl.yml` ve `unity-android.yml` aynı `unity-license` grubunda (iş düzeyi, `cancel-in-progress: false`). GitHub bir grupta
tek bekleyen iş tutar; yeni bir WebGL itişi bekleyen Android işini iptal eder. **Düzeltme:** CI.md §8'e "Android'i itiş yokken
başlat" notu. `docs/CI.md` satır 201 "unity-android.yml henüz yok" diyor; §8 ile çelişiyor, silinsin.

### P4B-14 — Öneri — Draw call ölçülmüyor

`Isolated` düğümleri (köşe kral, ana ekran paralaks katmanları, pencere kralı, yıldız patlaması) iç içe Canvas ile çiziliyor;
her biri ayrı batch. 7 doku sayfası (atlas 3, king 2, fx 2) arasında geçiş de batch'i böler. Hiçbir koşuda draw call/batch sayısı
yok. **Düzeltme:** `?shots=1` her ekranda `Unity.Profiling.ProfilerRecorder` ile "Draw Calls Count" ve "Batches Count" yazsın
(release oyuncuda kullanılabilirlik ve imza UnityCsReference ile doğrulanıp stub eklenmeli). Bütçe ≤ 60.

### P4B-15 — Öneri — Telif: Engel yok, iki önlem

Usta Kral turnaround'u (`art/3d/blender/king/preview/king-turn.png`), klip sayfası ve CI'daki Unity görüntüleri, referanstaki
Royal Match kralıyla (`owner-2026-10-14/ref-08, 12, 13, 15`) yan yana incelendi. Siluet farklı (2,5 baş tuğla ↔ ~4 baş varil).
Başlık farklı (amblemli baret ↔ taşlı taç). Palet farklı (turuncu + gök mavisi + sarı ↔ mor + altın). Sakal ve bıyık farklı
(kısa kestane, bıyıksız ↔ gür beyaz sakal, kıvrık bıyık). Pelerin ve tunik yok; el nesnesi Altın Mala; ad farklı. ART_3D fark
tablosunun 20 maddesi görüntüyle tutarlı. Artık risk: ana ekranda kral + insan (Tuna) + köpek (Kepçe) yan yana duruyor;
Royal Match'in kral-uşak-köpek tanıtım kartlarına kompozisyon olarak benziyor (köpek ırkı, rengi ve baretiyle farklı).
EN metinde ad yalnız "the King". **Düzeltme:** mağaza görselleri ve ikonda kral-insan-köpek dizilimi kullanılmasın, Kepçe Tuna'nın
yanında dursun. EN ad için ART_3D §13.1 kapatılsın (entrepreneur + design-lead).

### P4B-16 — Öneri — Boş saha hücresi dolu gibi okunuyor

CI `level_start`/`tutorial` (390×844) görüntüsünde Bölüm 1'in boş sütunu (x = 4, dört hücre) tam boy kalas dokusuyla çiziliyor.
Taş duvarın hemen solunda, ahşap bir blok yığını ya da ikinci bir duvar gibi okunuyor. D-099'da park yeri bulmacanın ana
kaynağı; boş hücre ilk bakışta boş görünmeli. **Düzeltme (design-lead):** boş hücre dokusu düz ve koyu olsun, ya da ızgara
çizgisi alsın; rubrikle 390×844 ve 360×800'de yeniden puanlansın.

### P4B-17 — Öneri — Android: güvenli alan ve geri tuşu

Unity katmanında `Screen.safeArea` ve `KeyCode.Escape` kullanımı yok. Çentikli ve hareket çubuklu telefonlarda üst HUD kesilebilir;
Android geri tuşu hiçbir şey yapmaz. **Düzeltme:** görünüm alanı `Screen.safeArea` ile içe çekilsin (stub + Android ikizi).
Geri tuşu açık pencereyi kapatsın, bölümde duraklatma açsın, ana ekranda çıkış onayı sorsun.

## Kontrol listesi sonuçları

1. **D-099/K-55 TS ↔ C#:** birebir. Port satır satır aynı: `RunStep12`, `D1Help`, `Session.lastPre`, `AddMoves`, `RulesVersion` 3,
   L-36 çalışma zamanı kapısında. Kehaneti kendim ürettim, DiffFuzz 655 dizi / 16 376 eylem / 0 sapma. Golden yayın 10/10 + arşiv
   10/10. Dilim geçişinde sahaya blok gelmiyor. Kod kanıtı: v2'de tek parti, L-36 `RuntimeChecks`'te, geçersiz bölüm yüklenmez.
   Test kanıtı: `K55_segment_change_keeps_the_yard_E62…` (saha bit bit aynı, teslimat yok), 200 tohumlu değişmez testi,
   kehanette v2'de 130 kayma ve 0 teslimat olayı. P4B-11 testi genişletme önerisidir.
2. **10 bölüm:** çözücü değerleri A2 ile birebir; ama şartname bantlarının dışında (P4B-02, P4B-03). Öğreticiler yeni sahayla
   tutarlı. Her vurgulanan parça kanonik çözümün o anki hamlesi: Bölüm 1 parça 0 (1. hamle), 3 ve 5'te "sonraki kat" bloğu
   gerçekten 2. katta yerleşiyor, 4'te parça 7 kat bittikten sonraki ray hamlesi, 7'de parça 6 kattan sonraki ilk hamle,
   9'da tetik parça 6, hedef parça 4 (ray). CI `tutorial` görüntüsünde eldiven parça 0'dan duvar üstüne gidiyor. **Bölüm 1
   kolay:** `min` 6, kaydırma 0, `moves` 10, `greedyWin` 1,00, açılışta 5 seçenek.
3. **Unity:** yeni stub üyeleri UnityCsReference 6000.0 ile aynı: `Time.realtimeSinceStartup`, `Application.absoluteURL`,
   `Resources.UnloadAsset(Object)`, `AudioClip.Create(string,int,int,int,bool)`, `AudioClip.SetData(float[],int)`,
   `AudioSource.playOnAwake/spatialBlend/PlayOneShot(…)`, `AudioListener : AudioBehaviour`. Gerçekte `AudioClip : AudioResource`;
   stub `Object`'ten türüyor, katman tabanı kullanmadığı için zararsız. Bellek: 7 sayfa = **62,58 MiB RGBA32** (iddia 62,6 doğru),
   mip yok, `LoadImage(…, markNonReadable: true)`, PNG baytları hemen boşaltılıyor. Çözme süresi ve indirme boyutu P4B-04/05;
   draw call ölçülmüyor (P4B-14). WebGL ses: `stream: false` ve `SetData` yolu; CI konsolunda ses hatası yok; Unity'nin WebGL
   ses tablosu bu oturumdan okunamadı. `?shots=1` üretimde açık (P4B-09).
4. **Android:** iş akışı ve `BuildAndroid` okunarak incelendi, API imzaları doğrulandı, ama hiç koşmadı (P4B-06, P4B-13, P4B-17).
5. **i18n:** sabit metin yok, TR/EN 259/259; çelişen ve eskiyen anahtarlar P4B-07'de.
6. **Telif:** Engel yok (P4B-15).
7. **CI:** son koşu yeşil; Build adımı gerçekten "Build succeeded!" yazdı (sahte yeşil değil). Pages deploy işi başarılı.
   ci-shots `e4be42e` için üretildi, iki görünümde 7/7 ekran boş değil, hata yok. Pages adresi bu oturumdan açılamadı.

## Doğrulanamayanlar (dürüst liste)

- Unity'de, telefonda hiçbir şey görülmedi; FPS, dokunuş gecikmesi ve gerçek cihaz belleği ölçülmedi.
- Hareket (animasyon akıcılığı) görülmedi; yalnız durağan CI kareleri ve klip verisi (P4B-08).
- Pages'in sunduğu aktarım boyutu ve sıkıştırma başlıkları (vekil 403).
- WebGL'de `AudioClip.Create`/`SetData` desteği belgeden doğrulanamadı (docs.unity3d.com erişilemez); CI'da hata yok, ses
  duyulduğu kanıtlanmadı.
- Android derlemesi ve APK (hiç koşmadı).
- Orta bot kazanma oranları (araç yok).
- Draw call / batch sayısı.

## Hedefli yorumlar (REVIEW_LOG biçimi)

- [code-lead → product-lead] bulmaca zekâsı: Bölüm 1–9'da `greedyWin` 1,00; `targets` ölçülen değerin çevresinde. Tablo B'yi
  DECISIONS'la revize et ya da bölümleri Tablo B'ye çek; Normal P 2 → 1 ilk deneme (P4B-02, P4B-03, P4B-12).
- [code-lead → code-lead(web)] A2 test paketi A/B/C + Faz 2R arşiv fikstürü, sonra `wip/a2-levels` → `wip/phase-2r` (P4B-01).
- [code-lead → code-lead(unity)] `Warm` çağrıları, Gzip + fallback denemesi, Android ilk koşu, safeArea/geri tuşu, DiffFuzz'a v2
  değişmezi, draw call kaydı (P4B-04/05/06/09/11/14/17).
- [code-lead → design-lead] `obs.s1.desc`, "Dede" anahtarları, `drag.tiltMaxDeg`, boş hücre dokusu, kral render ölçeği, hareket
  kanıtı (P4B-07/08/10/16, P4B-04 madde 3).
- [code-lead → entrepreneur] Usta Kral'ın EN adı ve mağaza görsellerinde kral-insan-köpek dizilimi (P4B-15).

## Tekrar üretme (scratchpad; repoya eklenmez)

```bash
# Unity worktree: origin/main e4be42e; web worktree: origin/wip/a2-levels e80a318 (+ node_modules sembolik bağı)
node tools/diff/ts-oracle.mjs <web-wt>            # tools/diff/out, 129 dosya
cd dotnet && dotnet build -warnaserror && dotnet test --no-build && cd .. && bash tools/stub-gate-selftest.sh
cd <web-wt> && npm run levels:validate && rm -rf artifacts/solver && npm run levels:solve && npx vitest run
gh run list -R yilmazfatih-cyber/minik-usta-unity -L 12
gh run view --job=125088360058 -R yilmazfatih-cyber/minik-usta-unity --log | grep -E "Complete build size|Build/MinikUsta"
git archive origin/ci-shots latest | tar -x    # manifest.json, console-*.log ("[minik] art3d decode")
curl https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/6000.0/<yol>   # imza denetimi
```
