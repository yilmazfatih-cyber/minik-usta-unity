# Faz 3 şüpheci inceleme — Unity katmanı GameCI'de derlenip Pages'te açılınca ne kırılır? (REVIEW_PHASE3)

İncelenen: `main` `b2033f4` (Q0 `e97e844`, Q1 `b4efe18`, Q3 `7448015`, Q2 `b2033f4`). İnceleyen: code-lead (bağımsız tur).
Kod değiştirilmedi; yalnız bu belge eklendi. Önem: **Engel** = ilk CI koşusunu ya da Pages açılışını kırar, iş geri döner ·
**Önemli** = aynı sprintte kapanmalı · **Öneri** = birikim listesi.

**Kapanış (Q5):** P3-01 … P3-05 kapandı; kanıt ve kalanlar belgenin sonundaki «Kapanış — Q5» bölümünde. P3-06 … P3-18 (Öneri) dokunulmadı.

## Yöntem

Bulutta Unity Editor yok (D-093). Stub derlemesi (`dotnet/UnityStubs`) yalnız imzaları denetler; platforma göre var olan ya da
olmayan türleri göremez. Bu yüzden imza denetimi bu turda **gerçek Unity derlemelerine karşı** tekrarlandı. Her şey
scratchpad'de yapıldı; repoya bağımlılık eklenmedi:

1. **Gerçek UnityEngine modülleri:** nuget.org `UnityEngine.Modules 2021.3.33` (Unity 2021.3 oyuncu derlemesinin modül DLL'leri;
   paket içindeki `BepInEx.AssemblyPublicizer` izine göre bir Windows oyuncu derlemesinden alınmış). nuget.org'da Unity 6 sürümü yok;
   2021.3, kullanılan API'lerin hepsini içeren en yakın gerçek referans.
2. **Gerçek uGUI:** `needle-mirror/com.unity.ugui` HEAD (1.0.0) kaynağı bu modüllere karşı derlendi (`VertexHelper.cs` içinde
   2021.1–2021.3 arası değişen 3 `CanvasRenderer` satırı yorum yapıldı; kullanılan API'lere dokunmuyor). `Runtime/UnityEngine.UI.asmdef`
   adı `UnityEngine.UI` (asmdef referansı doğru).
3. `Assets/MinikUsta/Unity/**/*.cs` bu iki referansa karşı derlendi: netstandard2.1, C# 9, proje düzeyinde `Nullable disable`
   (Unity'nin varsayılanı; dosyalar `#nullable enable` taşıyor), uyarılar hata sayılmadan.
4. **Test asmdef'leri** (Unity Editor bunları da derler; biri kırılırsa batchmode `executeMethod`'a hiç gelmez): `Core.Tests` ve
   `Presentation.Core.Tests` kaynakları NUnit **3.5.0** (Unity'nin `com.unity.ext.nunit` 2.0.5 paketi "NUnit 3.5 tabanlı") ve C# 9 ile derlendi.
5. **UnityCsReference 6000.0** (2026-10-15 başlığı) seyrek klonla okundu: Handheld, Input, Resources, PlayerSettings(WebGL),
   BuildPipeline, BuildSummary, EditorUserBuildSettings, MonoLibraryHelpers (Editor derleme referansları).
6. **Unity 6 dosya biçimleri** Unity-Technologies'in açık repolarıyla karşılaştırıldı: `2d-techdemos` (6000.3.3f1), `megacity-metro`
   (6000.1.0f1), `InputSystem` (6000.0.69f1), `game-programming-patterns-demo` (6000.0.9f1).
7. Docker Hub: `unityci/editor:ubuntu-6000.0.85f1-webgl-3` etiketi var (2026-10-20).

Erişilemeyen: `docs.unity3d.com` ve `issuetracker.unity3d.com` bu oturumun ağ izinlerinde yok; bu iki kaynaktaki bilgiler arama
sonucu özetlerinden alındı (aşağıda açıkça belirtildi). Doğrudan okunmaları için bu iki alan adına oturum ağ ayarlarında izin verilmeli.

Kapı (inceleme anında, değişiklik yok): `cd dotnet && dotnet build -warnaserror` → 0 uyarı, 0 hata; `dotnet test` →
Presentation 283/283, Core 1455 geçti + 5 atlandı (bilinen R-03/04/09/10/11), 0 kırmızı.

## Özet

| Kimlik | Önem | Yer | Konu |
| --- | --- | --- | --- |
| P3-01 | **Engel** | `Assets/MinikUsta/Unity/GameRunner.cs:42` | `Handheld` sınıfı mobil dışı hedeflerde yok; WebGL oyuncu derlemesi `CS0103` ile düşer |
| P3-02 | Önemli | `dotnet/UnityStubs/UnityEngine.cs:276-280`, `dotnet/MinikUsta.Unity` | Stub denetimi platform kullanılabilirliğini ve `#if` dallarını görmüyor (P3-01'i kaçırdı) |
| P3-03 | Önemli | `docs/CI.md` §1 adım 2, `.github/workflows/unity-activation.yml` | `.alf` → license.unity3d.com/manual yolu Personal lisansta artık çalışmıyor |
| P3-04 | Önemli | `Packages/manifest.json:3-4` | Kullanılmayan URP 17.0.3 ve Cinemachine 3.1.1 her CI koşusunda derleniyor |
| P3-05 | Önemli | `ProjectSettings/` (yalnız 3 dosya), `.meta` yok | `InputManager.asset` yok: eksen yoksa `StandaloneInputModule` her karede istisna atar, pencere düğmeleri ölür; GUID'ler her koşuda yeniden üretiliyor |
| P3-06 | Öneri | `ProjectSettings/ProjectSettings.asset:6` | `serializedVersion: 28` Unity 6000.1+ değeri; 6000.0 `26` yazıyor |
| P3-07 | Öneri | `Assets/MinikUsta/Unity/PointerInput.cs:15, 54-67` | Mobil tarayıcının dokunuştan sonra ürettiği uyumluluk fare olayları ikinci basış olarak işlenebilir |
| P3-08 | Öneri | `PointerInput.cs`, `GameRunner.cs` | Kaybolan dokunuş bitişi (sekme/uygulama değişimi) sürüklemeyi açık bırakır; yeni dokunuşlar "ikinci parmak" sayılır |
| P3-09 | Öneri | `PointerInput.cs` ↔ `UnityHud.cs:137-139` | uGUI ve tahta aynı dokunuşu alıyor; bugün zararsız, tahta üstüne gelecek ilk uGUI düğmesinde sızar |
| P3-10 | Öneri | `Assets/MinikUsta/Scenes/Level.unity:192-193` | Built-in RP kamerasında HDR ve MSAA açık; 2D için mobil WebGL'de boşa doluluk maliyeti |
| P3-11 | Öneri | `Assets/MinikUsta/Unity/ProceduralSprites.cs:19-29` | Atlas ilk karede eşzamanlı üretiliyor; 2 × 4 MB geçici dizi; WebGL süresi ölçülmedi |
| P3-12 | Öneri | `ProceduralSprites.cs:24` (`mipChain: false`) | 120 px karo 390 px genişlikte ≈ 43 px çiziliyor (2,8× küçültme, mip yok): ince kontur titrer |
| P3-13 | Öneri | `GameRunner.cs:86`, `Editor/CiBuild.cs:35` | Debug kısayolu noktalı Resources adı kullanıyor; Pages derlemesinde debug hiç yok |
| P3-14 | Öneri | `Assets/MinikUsta/Editor/CiBuild.cs:67` | Editor asmdef hiçbir yerde derleme denetiminden geçmiyor; `string?` `#nullable` dışında |
| P3-15 | Öneri | `ProjectSettings.asset` `webGLTemplate`, varsayılan şablon | Masaüstünde 540×960 sabit tuval sayfayı kaydırır; mobilde DPR 3 tam çözünürlük |
| P3-16 | Öneri | `UnityHud.cs:125` (`LegacyRuntime.ttf`) | Türkçe glifler WebGL'de görülmedi; işletim sistemi font yedeği WebGL'de yok |
| P3-17 | Öneri | `Assets/StreamingAssets/MinikUsta/**` | Yalnız testlerin okuduğu kopyalar oyun derlemesine giriyor |
| P3-18 | Öneri | `.github/workflows/unity-webgl.yml` (Build adımı) | GameCI `versioning` varsayılanı (Semantic) git geçmişi ister; özel `buildMethod` sürümü kullanmıyor |

**Engel: 1 (P3-01).** Bu düzeltilmeden ilk lisanslı CI koşusu derleme adımında kırmızı olur. P3-01 düzeltmesi scratchpad'de
denendi: aynı gerçek referanslara karşı Unity katmanı **0 hata, 0 uyarı** derlendi; yani gerçek referans derlemesinin bulduğu
tek derleme hatası budur.

## Bulgular

### P3-01 — Engel — `Handheld.Vibrate` korumasız; WebGL hedefinde tür yok

- **Yer:** `Assets/MinikUsta/Unity/GameRunner.cs:42`
  `var haptics = new FixedPulseHaptics(Application.platform == RuntimePlatform.Android, Handheld.Vibrate);`
- **Sorun:** `Application.platform` denetimi çalışma zamanındadır; derleyici `Handheld` türünü hedef platformun UnityEngine
  derlemesinde bulmak zorundadır. Unity, `Handheld`'ı yalnız mobil hedeflerde (Android, iOS) derlemeye koyar. WebGL oyuncu
  derlemesinde `error CS0103: The name 'Handheld' does not exist in the current context` beklenir ve `BuildPipeline.BuildPlayer`
  başarısız olur (`CiBuild` "WebGL build failed" fırlatır).
- **Kanıt:**
  1. Gerçek referans derlemesi (Yöntem 1-3): `GameRunner.cs(42,98): error CS0103: The name 'Handheld' does not exist in the current context`.
     Meta veri dökümü: 2021.3 oyuncu `UnityEngine.CoreModule.dll` içinde `Handheld` türü yok (yalnız `DeviceType.Handheld` enum üyesi var).
  2. Unity Issue Tracker 1332218 "Standalone: Handheld does not exist" (2018.4–2021.2'de üretildi; çözüm 2022.1'de belgeye "bu sınıf
     yalnız mobil platformlarda vardır" notu eklemek oldu) — https://issuetracker.unity3d.com/issues/standalone-handheld-does-not-exist
     (bu oturumdan açılamadı; içerik arama özetinden). Aynı belirti Windows Store'da: https://issuetracker.unity3d.com/issues/handheld-class-is-not-available-in-windows-store-apps-platform
  3. UnityCsReference 6000.0 `Runtime/Export/Handheld/Handheld.bindings.cs:92-93` (`public static extern void Vibrate()`) kaynakta `#if`
     taşımıyor: dışlama yerel derleme sisteminde yapılıyor. Q2'nin "kaynaktan doğrulandı" notu imzayı doğruladı, kullanılabilirliği değil.
  4. Düzeltilmiş kopya (aşağıdaki koruma) aynı referanslara karşı: `Build succeeded`, 0 uyarı.
- **Düzeltme:** `GameRunner.cs:42`:
  ```csharp
  #if UNITY_ANDROID || UNITY_IOS
              var haptics = new FixedPulseHaptics(Application.platform == RuntimePlatform.Android, Handheld.Vibrate);
  #else
              var haptics = new FixedPulseHaptics(false, () => { });
  #endif
  ```
  Ardından P3-02'deki stub değişikliği; ikisi aynı commit'te.

### P3-02 — Önemli — Stub denetimi platform kullanılabilirliğini ve `#if` dallarını görmüyor

- **Yer:** `dotnet/UnityStubs/UnityEngine.cs:276-280` (`Handheld` koşulsuz), `dotnet/MinikUsta.Unity/MinikUsta.Unity.csproj` (tek tanım kümesi).
- **Sorun:** Stub, "imza doğru mu" sorusunu yanıtlıyor; "bu tür WebGL'de var mı" sorusunu yanıtlamıyor. P3-01 bu yüzden yeşil
  geçti. P3-01 düzeltmesi bir `#if UNITY_ANDROID || UNITY_IOS` dalı getirir; bugünkü dotnet projesi o dalı hiç derlemez.
- **Kanıt:** P3-01 kanıt 1 ve 4; `dotnet build -warnaserror` aynı kodla 0 hata veriyor. Tür bildirimleri UnityCsReference 6000.0 ile
  karşılaştırıldı (32 tür): taban sınıflar ve `sealed` uyuyor; iki küçük fark var, bugünkü kullanımı etkilemiyor: Unity'de
  `Input` `static`, `Application` `sealed` (stub'ta ikisi de düz `class`).
- **Düzeltme:**
  1. Stub'ta `Handheld`'ı `#if UNITY_ANDROID || UNITY_IOS` içine al (yorum: "Unity yalnız mobil hedeflerde derler; Issue 1332218").
     Varsayılan dotnet derlemesi (tanım yok = WebGL'in durumu) korumasız her kullanımı yakalar.
  2. `dotnet/MinikUsta.Unity.Android/MinikUsta.Unity.Android.csproj`: aynı kaynaklar, `<DefineConstants>$(DefineConstants);UNITY_ANDROID</DefineConstants>`,
     `UnityStubs`'a da aynı tanım (ayrı bir `UnityStubs.Android` projesi ya da `Compile Include` ile aynı dosya). Çözüme ekle; kapı ikisini de derler.
  3. (Orkestratör onayı gerekir: yeni NuGet bağımlılığı.) İsteğe bağlı `tools/unity-refcheck.sh`: `UnityEngine.Modules` + uGUI kaynağı ile
     bu turdaki gerçek referans derlemesini tekrarlar; repoya değil `artifacts/`'a indirir, kapıya bağlanmaz. Bu tur, stub'ın tek başına
     yeterli olmadığını gösterdi.

### P3-03 — Önemli — Lisans: `.alf` el ile etkinleştirme yolu Personal'da kapalı

- **Yer:** `docs/CI.md:16-18` ("Kurulumsuz" yol), `.github/workflows/unity-activation.yml` (`game-ci/request-manual-activation-file@v2`).
- **Sorun:** Unity, Personal lisansların el ile (`.alf` → license.unity3d.com/manual) etkinleştirilmesini kaldırdı. Proje sahibi
  belgedeki ilk yolu izlerse `.ulf` alamaz; CI ilk adımda takılır.
- **Kanıt:** Unity destek makalesi "Manual activation for Personal licenses is no longer supported" — https://support.unity.com/hc/en-us/articles/5541428270356 ;
  GameCI CLI v0.1.61 sürüm notu (Personal için `.alf/.ulf` gidiş-dönüşü yok; hesap ya da seri ile etkinleştirme) — https://github.com/game-ci/cli/releases/tag/v0.1.61 ;
  eski "Personal seçeneğini sayfayı inceleyerek açma" geçici çözümü — https://dev.to/ankursheel/workaround-for-unity-personal-license-manual-activation-not-supported-49j2
  (içerikler arama özetinden; sayfalar bu oturumdan açılmadı).
- **Düzeltme:** `CI.md` §1: Unity Hub yolu tek yol olsun (Hub ile oturum aç → Personal etkinleşir → `Unity_lic.ulf` içeriği `UNITY_LICENSE`'a;
  `UNITY_EMAIL`, `UNITY_PASSWORD` ayrıca). "Kurulumsuz" maddesini kaldır ya da "yalnız Pro/Plus" diye işaretle. `unity-activation.yml`'i
  sil ya da adını ve açıklamasını "Pro/Plus için" yap. Belge değişikliği code-lead'in (CI.md code-lead dosyası).

### P3-04 — Önemli — Kullanılmayan URP ve Cinemachine paketleri

- **Yer:** `Packages/manifest.json:3-4` (`com.unity.cinemachine 3.1.1`, `com.unity.render-pipelines.universal 17.0.3`).
- **Sorun:** Hiçbir dosya kullanmıyor (`grep -r "Cinemachine\|Rendering.Universal\|UnityEngine.Rendering" Assets` → 0); D-096 de
  kullanmıyor diyor. Batchmode her koşuda bu paketleri ve bağımlılıklarını (SRP Core, Shader Graph, Splines …) içe aktarıp derler:
  CI süresi uzar ve paket içindeki tek bir derleme hatası bizim derlememizi düşürür. URP, sürümü Editor'e bağlı bir çekirdek paket;
  17.0.3, 6000.0.85f1'in kendi sürümüyle aynı olmayabilir (çözümleme ya da yükseltme uyarısı).
- **Kanıt:** manifest satırları; Assets'te kullanım yok; grafik ayarı yok (`GraphicsSettings.asset` yok → Built-in RP etkin).
- **Düzeltme:** İki satırı sil. İsteğe bağlı: `com.unity.modules.particlesystem` (D-096: ParticleSystem yok) ve `physics2d` da çıkabilir;
  `audio` kalmalı (sahnedeki `AudioListener`), `ui`, `jsonserialize` (test-framework), `vibration` kaldırıldı: Unity 6000.0'da böyle bir yerleşik modül yok.

### P3-05 — Önemli — Eksik ProjectSettings dosyaları ve izlenmeyen `.meta`'lar

- **Yer:** `ProjectSettings/` yalnız `ProjectSettings.asset`, `EditorBuildSettings.asset`, `ProjectVersion.txt` içeriyor; Assets altında
  yalnız `Level.unity.meta` izleniyor.
- **Sorun:** `InputManager.asset`, `GraphicsSettings.asset`, `TagManager.asset`, `QualitySettings.asset` her koşuda Unity'nin
  varsayılanından üretilir. Riskli olan `InputManager`: `StandaloneInputModule.ShouldActivateModule()` her karede
  `GetButtonDown("Submit")`, `GetButtonDown("Cancel")`, `GetAxisRaw("Horizontal"/"Vertical")` çağırır. Bu eksenler yoksa
  `ArgumentException: Input Button Submit is not setup` atılır; istisna `EventSystem.Update` içinde `Process()`'ten önce olduğu
  için hiçbir uGUI düğmesi çalışmaz: Mola açılmaz, kazanma penceresinin "Devam"ı basılmaz, oyun ilk kazanmada kilitlenir.
  Unity'nin dosya yokken varsayılan eksenleri (Horizontal, Vertical, Submit, Cancel …) yazması beklenir; bu ortamda doğrulanamadı.
  Ayrıca `.meta`'lar her koşuda yeni GUID ile üretilir: Library önbelleği isabetsizleşir, ileride bir prefab ya da ScriptableObject
  (ör. FeelSettings sarmalayıcısı) betiğe GUID ile bağlanırsa her klonda kopar.
- **Kanıt:** dosya listesi; ugui `StandaloneInputModule.ShouldActivateModule` / `EventSystem.Update` akışı (com.unity.ugui kaynağı).
  Karşılaştırma için 6000.0.69f1 projesinin (`InputSystem`) `InputManager.asset`'inde 18 eksen var (Horizontal, Submit, Cancel dahil);
  Unity'nin dosya yokken aynısını ürettiği bu ortamda görülemedi. `GraphicsSettings.asset` yokken varsayılanın
  `m_SpritesDefaultMaterial: {fileID: 10754 …}` içerdiği aynı projede görülüyor (koddan kurulan SpriteRenderer'ların malzemesi).
- **Düzeltme:** İş akışına Build'den sonra bir adım: `ProjectSettings/` ve `Assets/**/*.meta` dosyalarını `actions/upload-artifact`
  ile yükle. İlk yeşil koşudan sonra bunları indirip commit et (GUID'ler sabitlenir, ayarlar Unity'nin tam biçimine geçer). İlk koşu
  günlüğünde `is not setup` aranmalı; çıkarsa Unity varsayılan `InputManager.asset`'i hemen commit edilir.

### P3-06 — Öneri — `PlayerSettings serializedVersion: 28`

- **Yer:** `ProjectSettings/ProjectSettings.asset:6`.
- **Sorun:** 6000.0 ile kaydedilmiş iki açık projede değer `26` (ikisinde de yalnız Unity 6'da olan `webGLShowDiagnostics` alanı var,
  yani gerçekten 6000.0 kaydı): `Unity-Technologies/InputSystem` (6000.0.69f1), `game-programming-patterns-demo` (6000.0.9f1).
  `28`, `megacity-metro` (6000.1.0f1) ve `2d-techdemos` (6000.3.3f1) dosyalarında. Dosya Editor'den yeni sürüm söylüyor: yükseltme
  dalları atlanır, alanlar adla okunur; büyük olasılıkla zararsız, doğrulanmadı. Bütün alan adları 6000.0 dosyalarında var (karşılaştırıldı).
- **Düzeltme:** `26` yaz ya da P3-05'teki ilk koşu çıktısını commit et.

### P3-07 — Öneri — Dokunuştan sonra gelen uyumluluk fare olayları

- **Yer:** `Assets/MinikUsta/Unity/PointerInput.cs:15` (`MouseId = 1000`), `:54-67` (fare dalı `touchCount == 0` iken çalışır).
- **Sorun:** Mobil tarayıcı, `touchstart/touchend` üzerinde `preventDefault` çağrılmamışsa dokunmadan ~0-300 ms sonra
  `mousedown/mouseup` üretir. Unity WebGL'in bunu bastırıp bastırmadığı doğrulanamadı. Bastırmıyorsa her dokunma bir de
  `PointerDown(1000)`/`PointerUp(1000)` üretir: çift dokunma zıplaması, K-09 bloğunda çift titreme ve titreşim isteği. Ayrıca Chrome
  dokunma kimliklerini artan sayı verir; Unity bunu `fingerId`'ye geçiriyorsa 1000. dokunuş fare kimliğiyle çakışır.
- **Kanıt:** kod akışı; `Input.simulateMouseWithTouches = false` yalnız Unity'nin kendi benzetimini kapatır, tarayıcınınkini değil.
- **Düzeltme:** Bir dokunuş görüldükten sonra fare dalını 500 ms kapat (`lastTouchTime`, `Time.unscaledTime`); `MouseId = -1`
  (`DragMapper` kimliği yalnız eşitlikle karşılaştırıyor, negatif sorun değil). Pages'te telefonda tek dokunmayla kontrol.

### P3-08 — Öneri — Kaybolan dokunuş bitişi sürüklemeyi açık bırakır

- **Yer:** `PointerInput.Poll`, `GameRunner` (`OnApplicationFocus`/`OnApplicationPause` yok); `LevelPlayer.PointerDown` (`if (Mapper.Active) return;`).
- **Sorun:** Sekme ya da uygulama değişiminde `Ended/Canceled` gelmezse `DragMapper` etkin kalır; sonraki her dokunuş "ikinci parmak"
  sayılıp yok sayılır, blok havada kalır. Tek kurtuluş Mola (`PausePressed` → `Mapper.Abort()`); oyuncu bunu bilmez.
- **Düzeltme:** `GameRunner.OnApplicationFocus(false)` ve `OnApplicationPause(true)` → etkin işaretçiye `PointerUp(id, canceled: true)`;
  `Poll` içinde `Mapper.Active && touchCount == 0 && !mouseDown` ise aynı iptal. Stub'a iki sihirli metot gerekmez (imza yok); FakeView ile test.

### P3-09 — Öneri — uGUI ve tahta aynı dokunuşu alıyor

- **Yer:** `PointerInput.Poll` `EventSystem`'e bakmıyor; `UnityHud.cs:137-139` aynı dokunuşu ayrıca işliyor.
- **Sorun:** Bugün zararsız: pencere açıkken `LevelPlayer.PointerDown` `Panel != None` ile çıkıyor, HUD düğmeleri tahta dikdörtgeni
  dışında. Güçlendirici düğmeleri ya da tahta üstüne binen ilk uGUI öğesi (öğretici balonu, Bonus İnşaat çipi) basışı tahtaya da sızdırır.
- **Düzeltme:** `EventSystem.current.IsPointerOverGameObject(fingerId)` (fare için parametresiz) doğruysa `PointerDown` gönderme;
  stub'a gerçek imzasıyla `bool IsPointerOverGameObject()` ve `bool IsPointerOverGameObject(int pointerId)` eklenir.

### P3-10 — Öneri — Kamerada HDR ve MSAA açık

- **Yer:** `Assets/MinikUsta/Scenes/Level.unity:192-193` (`m_HDR: 1`, `m_AllowMSAA: 1`).
- **Sorun:** Built-in RP'de HDR kamera kayan noktalı ara hedefe çizer ve sonra kopyalar; MSAA kalite ayarına göre çoklu örnek açar.
  Saf sprite sahnesinde kazanç yok; mobil WebGL'de bant genişliği gider (ilke 6: orta telefonda 60 FPS).
- **Düzeltme:** İki alanı `0` yap (sahne YAML'ı elle; başka alan değişmez).

### P3-11 — Öneri — Atlas ilk karede, eşzamanlı ve iki kopya

- **Yer:** `Assets/MinikUsta/Unity/ProceduralSprites.cs:19-29`.
- **Sorun:** `Atlas.Build()` .NET 8'de ≈ 85 ms; IL2CPP→wasm orta telefonda bunun birkaç katı olabilir (ölçülmedi) ve `GameRunner.Awake`
  içinde, ilk karede çalışıyor. Ayrıca 4 MB `byte[]` + 4 MB `Color32[]` geçici bellek; WebGL yığını büyüdükten sonra küçülmez.
- **Düzeltme:** `tex.SetPixelData(atlas.Pixels, 0)` + `Apply(false, true)` ile `Color32` kopyasını kaldır (stub'a gerçek imza:
  `public void SetPixelData<T>(T[] data, int mipLevel, int sourceDataStartIndex = 0)`). Süreyi Pages'te `Time.realtimeSinceStartup`
  farkıyla konsola yaz; > 200 ms ise atlası karelere böl.

### P3-12 — Öneri — Mip yok, 2,8× küçültme

- **Yer:** `ProceduralSprites.cs:24` (`mipChain: false`), `FilterMode.Bilinear`.
- **Sorun:** Karo içeriği 120 px; 390×844'te FIT ölçeği 0,361 → hücre ≈ 43 ekran px. Mip olmadan çift doğrusal örnekleme 2,8× küçültmede
  6 px konturu ve 12 px dudağı hareket sırasında titreştirir (önizleme bunu göstermez: önizleme kendi örneklemesini yapar).
- **Düzeltme:** `mipChain: true` ve `Pad` ≥ 8 (iki mip düzeyi komşu karoyu çekmez) ya da atlası hedef ölçekte (≈ 60 px içerik) üret.

### P3-13 — Öneri — Debug kısayolu ve Pages'te debug yokluğu

- **Yer:** `GameRunner.cs:86` (`Resources.Load("solutions/level_NNN.solver")`), `Editor/CiBuild.cs:35` (`BuildOptions.None`).
- **Sorun:** (a) Varlık adı `level_001.solver`; Unity'nin `Resources.Load` yolunda son noktadan sonrasını uzantı sayıp saymadığı
  doğrulanamadı. Sayıyorsa `null` döner ve S tuşu sessizce açgözlü hamleye düşer. (b) Pages derlemesi geliştirme derlemesi değil;
  `Debug.isDebugBuild` false, ilke 8'in debug araçlarına bulutta hiç erişilemiyor.
- **Düzeltme:** Dosyaları `level_NNN_solver.json` diye adlandır (`tools/sync-from-web.sh` hedef adı). İş akışına `workflow_dispatch`
  girdisi `development: true` → `CiBuild` `-development` argümanında `BuildOptions.Development`.

### P3-14 — Öneri — `CiBuild.cs` hiç derleme denetiminden geçmiyor

- **Yer:** `Assets/MinikUsta/Editor/CiBuild.cs:67` (`string?`, dosyada `#nullable enable` yok); `dotnet/` çözümünde Editor projesi yok.
- **Sorun:** Unity `CS8632` uyarısı verir (hata değil). Asıl risk: bu dosyadaki bir yazım hatası ancak lisanslı CI koşusunda görülür.
  Bu turda API'ler elle doğrulandı (UnityCsReference 6000.0): `BuildPipeline.BuildPlayer(BuildPlayerOptions)`
  (`Editor/Mono/BuildPipeline.bindings.cs:363`), `BuildPlayerOptions.scenes/locationPathName/target/options` (`:218-226`),
  `PlayerSettings.WebGL.compressionFormat/dataCaching/decompressionFallback` (`PlayerSettingsWebGL.bindings.cs:145/82/188`),
  `WebGLCompressionFormat { Brotli, Gzip, Disabled }` (`:19-24`), `BuildSummary.totalSize/totalErrors/result`
  (`Modules/BuildReportingEditor/Managed/BuildSummary.cs:26/32/36`), `EditorUserBuildSettings.buildAppBundle` (`:905`),
  `PlayerSettings.runInBackground` (`PlayerSettings.bindings.cs:589`). `ulong / (1024 * 1024)` sabit int dönüşümüyle geçerli.
- **Düzeltme:** İlk satıra `#nullable enable`. İsteğe bağlı: `UnityEditor` stub'ı + `dotnet/MinikUsta.Editor` projesi.

### P3-15 — Öneri — WebGL şablonu ve piksel oranı

- **Yer:** `ProjectSettings.asset` (`webGLTemplate: APPLICATION:Default`, `defaultScreenWidthWeb 540`, `defaultScreenHeightWeb 960`).
- **Sorun:** Masaüstünde tuval 540×960 CSS px sabit; tarayıcı iç yüksekliği ≈ 1000 px'ten küçükse sayfa kayar, güçlendirici çubuğu
  katlamanın altında kalır. Mobilde tuval `devicePixelRatio` ile çizilir (DPR 3 → 1170×2532); 2D için gereğinden fazla doluluk.
- **Düzeltme:** `Assets/WebGLTemplates/MinikUsta/index.html`: tuval `min(100vh · 9/16, 100vw)` genişlik, `config.devicePixelRatio =
  Math.min(window.devicePixelRatio, 2)`; `webGLTemplate: PROJECT:MinikUsta`. Yatay tutuşta "telefonu dikey tut" katmanı (Faz 5).

### P3-16 — Öneri — LegacyRuntime'da Türkçe glifler (açık soru, sürüyor)

- **Yer:** `UnityHud.cs:125`.
- **Sorun:** WebGL'de işletim sistemi font yedeği yok; glif fontta yoksa kutu çizilir. `tr.json`'un ASCII dışı karakterleri:
  `· Ç Ö × Ü â ç ö ü ğ İ ı Ş ş` (Latin-1 + Latin Genişletilmiş-A). LegacyRuntime'ın bunları içerip içermediği görülmedi.
- **Düzeltme:** İlk Pages açılışında başlıkta "İğşı Çöü" ekran görüntüsü. Kutu varsa OFL bir TTF'yi `Resources/fonts`'a koyup
  `Resources.Load<Font>` (orkestratör + design-lead onayı; D-096 TMP'yi yasaklar, dinamik `Font` serbest).

### P3-17 — Öneri — StreamingAssets kopyaları derlemeye giriyor

- **Yer:** `Assets/StreamingAssets/MinikUsta/{levels,config}` (72 KB).
- **Sorun:** Oyun bunları okumuyor (D-096: Resources); yalnız `Core.Tests` okuyor. Unity bunları `build/StreamingAssets/`'a kopyalar;
  veri iki yerde durduğu için sapma riski taşır (bugün `cmp` ile 11/11 aynı).
- **Düzeltme:** Test kopyalarını Assets dışına taşı (`dotnet/testdata/levels`) ya da `TestPaths.LevelsDir`'i `Resources/levels`'a çevir
  (`PresentationPaths` zaten öyle yapıyor).

### P3-18 — Öneri — GameCI sürümleme

- **Yer:** `.github/workflows/unity-webgl.yml` Build adımı (`versioning` girdisi yok → `Semantic`; `actions/checkout@v4` sığ klon).
- **Sorun:** GameCI anlamsal sürüm için git geçmişine ihtiyaç duyar (sığ klonu derinleştirmeye çalışır). `CiBuild` `-buildVersion`'ı
  okumuyor; adım boşa iş yapıyor ve git durumuna bağımlılık ekliyor.
- **Düzeltme:** `with: versioning: None`.

## Kontrol listesi sonuçları

| # | Madde | Sonuç |
| --- | --- | --- |
| 1 | Stub imzaları ↔ gerçek Unity | Gerçek referans derlemesinde **tek hata: P3-01** (imza değil, platform kullanılabilirliği). Diğer bütün üyeler (Transform, RectTransform, SpriteRenderer.drawMode/size, Sprite.Create 7 parametreli, Texture2D, Resources.GetBuiltinResource, Input.*, Touch, Camera, Canvas, CanvasScaler.ScaleMode/ScreenMatchMode, Graphic, Image.Type, Text, Selectable, Button.onClick, IPointer*Handler, EventSystem, StandaloneInputModule, RuntimeInitializeOnLoadMethodAttribute, SceneManager) 0 hata, 0 uyarı. Ad çakışması: UnityCsReference 6000.0 `UnityEngine` ad alanı ile Core/Presentation türlerinin kesişimi `Grid`, `ScaleMode` (+ iç içe `Random.State`); Unity katmanında hiçbiri nitelemesiz kullanılmıyor. 6000.0'da eklenen API'ler 2021.3 referansıyla denetlenemez; kullanılmıyorlar. |
| 2 | Sahne, .meta, EditorBuildSettings, ProjectSettings | `Level.unity` Unity 6 biçimiyle aynı (RenderSettings `serializedVersion: 10`, LightmapSettings `13` + `m_BakeOnSceneLoad`, Camera alan kümesi, `SceneRoots` `!u!1660057539 &9223372036854775807`; 2d-techdemos 6000.3 sahnesiyle karşılaştırıldı). `Level.unity.meta` `DefaultImporter` (sahne için doğru). GUID'ler 32 onaltılık; sahne GUID'i = EditorBuildSettings GUID'i. EditorBuildSettings `serializedVersion: 2` doğru; `m_UseUCBPForAssetBundles` 6000.3'te var, 6000.1'de yok — 6000.0 bilinmeyen alanı yok sayar. ProjectSettings alan adlarının hepsi 6000.0 dosyalarında var; sürüm P3-06. `companyName: Tuna & Co.` YAML'da geçerli (`&` başta değil). |
| 3 | WebGL tuzakları | Resources.Load yolları dosyalarla eşleşiyor (`levels/level_NNN`, `i18n/tr`, `config/economy`); StreamingAssets çalışma zamanında okunmuyor. `Application.targetFrameRate` WebGL'de verilmiyor (doğru: tarayıcı rAF). Kamera FIT: `OrthographicSize = ScreenH/Scale/2/ppu` ile CanvasScaler `Shrink` aynı ölçeği veriyor (390×844: ölçek 0,361, tuval 1080×2337, tasarım dikdörtgeni ortada) — HUD ve sprite'lar hizalı. Dokunma: P3-07, P3-08. Handheld: P3-01. Sıkıştırma: `Disabled = 2` doğru ve `CiBuild` derleme anında da kapatıyor. Built-in sprite malzemesi `GraphicsSettings` varsayılanında (`m_SpritesDefaultMaterial`); `LegacyRuntime.ttf` her oyuncu derlemesindeki yerleşik kaynaklarda. |
| 4 | `[RuntimeInitializeOnLoadMethod]` ve Awake sırası | `AfterSceneLoad` sahne nesnelerinin Awake'inden sonra çalışır: `Camera.main` (etiket `MainCamera`, yerleşik etiket) bulunur. `AddComponent<GameRunner>()` Awake'i eşzamanlı çalıştırır; Awake içinde sırayla atlas → görünümler → HUD (EventSystem burada) → oyuncu → `SetScreen` → `Load`. `UnityHud` kurucusunda `SetStreak` çağrısından önce kullandığı bütün alanlar atanmış. `PressableButton.Init` AddComponent'ten hemen sonra; `Update` `feel?.` ile korumalı. Grafikler Image ile aynı nesneye konmuyor (Label alt nesne açıyor). Sorun yok. |
| 5 | asmdef grafiği | Core (`noEngineReferences`) ← Presentation.Core (`noEngineReferences`) ← Unity (+ `UnityEngine.UI`). Editor yalnız Editor. Testler yalnız Editor, `UNITY_INCLUDE_TESTS`, `overrideReferences` + `nunit.framework.dll`. Test kaynakları NUnit 3.5.0 + C# 9 + netstandard2.1 ile **0 hata** (Unity'nin NUnit'i 3.5 tabanlı). Editor derlemeleri `NET_Unity_4_8` profiliyle derlenir ve `System.IO.Compression.dll` varsayılan referans (`MonoLibraryHelpers.GetNet46SystemReferences`): `Preview.cs`'deki `DeflateStream` derlenir. Asmdef dışı `.cs` yok; özel klasör adı çakışması yok. `.meta` üretimi: P3-05. |
| 6 | Çekirdek bağlantıları | K-08/R-12: `PointerDown` tahtadaysa `Clock.Complete(BoardTag)` + `Queue.FastForward` + `Complete`, sonra `SpeedUp`; kilitte ve oyun bitince durum `null` geçiyor (seçim yok). K-18: her düğüm değişiminde `Gravity.ComputeFall(s, id, node, FallOptions(null, hooks.Fall))` hamle öncesi durumla. Kalan blok çipi: `Summary.BlocksLeft` (çekirdek `TurnSummary`). Kazanma: `session.Outcome == Won` (çekirdek K-48); sunum kendi koşulunu hesaplamıyor. Pencere açıkken basış `Panel != None` ile reddediliyor; Mola yalnız `Playing` iken. Sorun yok. |
| 7 | ProceduralSprites bellek | Tek 1024² RGBA32 doku (4 MB GPU), `Apply(false, true)` ile CPU kopyası bırakılıyor, 49 sprite, uygulama ömrü boyunca statik; sızıntı yok, `Destroy` gerekmiyor (tek sahne). Havuzlar en yüksek kullanıma kadar büyüyor (parçacık ≤ 120). Geçici bellek ve süre: P3-11. |
| 8 | CI iş akışı | `buildMethod: MinikUsta.Editor.CiBuild.BuildWebGL` → sahneler `EditorBuildSettings.scenes`'ten; GameCI `-customBuildPath` = `build/WebGL/MinikUsta`, Pages adımı aynı klasörü yüklüyor; `upload-pages-artifact@v3` + `deploy-pages@v4` + `pages: write`, `id-token: write` doğru. Docker imajı var. Lisans: P3-03. Paketler: P3-04. Sürümleme: P3-18. Bu belge yalnız `docs/` değiştirdiği için iş akışını tetiklemez (`paths` süzgeci). |

## Doğrulanamayanlar (dürüst liste)

- Unity 6000.0.85f1 ile gerçek derleme ve WebGL oyuncusu; bu tur 2021.3 oyuncu referanslarıyla yaklaşık bir denetimdir.
- `Handheld`'ın 6000.0 WebGL derlemesinde yokluğu doğrudan görülmedi; kanıt 2021.3 oyuncu DLL'i + Issue Tracker özeti. Düzeltme her iki
  durumda da zararsız.
- `InputManager.asset` yokken Unity'nin varsayılan eksenleri yazması (P3-05), Unity WebGL'in dokunma olaylarında `preventDefault`
  çağırması (P3-07), `Resources.Load`'un noktalı adları (P3-13), LegacyRuntime'ın Türkçe glifleri (P3-16).
- Performans (60 FPS, 4× CPU, dokunuş-kare), atlas süresi WebGL'de, D3a/özet maliyeti IL2CPP'de.
- REVIEW_PHASE2 R-05 (IL2CPP'de `double` biçimlendirme/ayrıştırma): oyun sonucunu etkilemiyor, `eventLogHash` için Unity koşusu bekliyor.

## Tekrar üretme (scratchpad; repoya eklenmez)

```bash
S=<scratch>; curl -L -o $S/umod.nupkg https://api.nuget.org/v3-flatcontainer/unityengine.modules/2021.3.33/unityengine.modules.2021.3.33.nupkg
unzip -q $S/umod.nupkg -d $S/umod
git clone --depth 1 https://github.com/needle-mirror/com.unity.ugui.git $S/ugui   # Runtime/UI/Core/Utility/VertexHelper.cs 3 satır yorum
# 1) ugui Runtime/**/*.cs → netstandard2.1, C# 9, Reference $S/umod/lib/netstandard2.0/*.dll → UnityEngine.UI.dll
# 2) Assets/MinikUsta/Unity/**/*.cs + ProjectReference Core, Presentation.Core + yukarıdaki referanslar, Nullable disable
#    → beklenen: GameRunner.cs(42,98): error CS0103 'Handheld'; P3-01 korumasıyla 0 hata
# 3) Assets/MinikUsta/{Core.Tests,Presentation.Core.Tests}/**/*.cs + NUnit 3.5.0 lib/net45 → 0 hata
```

## Kapanış — Q5

Paket Q5 (code-lead, aynı depo `main`). Kapı: `cd dotnet && dotnet build -warnaserror` → 8 proje, **0 uyarı, 0 hata**; `dotnet test` →
Presentation **283/283**, Core **1455 geçti + 5 atlandı** (bilinen R-03/04/09/10/11), 0 kırmızı; `tools/stub-gate-selftest.sh` → 4/4.
Bu bulutta Unity yok: aşağıdaki "doğrulandı" ifadeleri stub derlemesi, `dotnet` ve dosya karşılaştırması için geçerlidir; Unity'nin
kendisinde hiçbiri denenmedi.

| Kimlik | Durum | Ne değişti | Nasıl doğrulandı | Kalan |
| --- | --- | --- | --- | --- |
| P3-01 | **Kapandı** | `GameRunner.cs`: `Handheld.Vibrate` çağrısı `#if UNITY_ANDROID \|\| UNITY_IOS` içinde; `#else` dalı `new FixedPulseHaptics(false, () => { })`. Aynı commit'te P3-02 | Eski (korumasız) dosya geri konup derlendi: `GameRunner.cs(42,98): error CS0103: The name 'Handheld' does not exist` — incelemenin gerçek referans derlemesindeki hatayla aynı satır ve sütun. Düzeltilmiş dosya her iki projede 0 hata. Android dalındaki yazım hatası (`Handheld.Vibrat`) yalnız Android projesinde `CS0117` verdi, varsayılan proje geçti: iki dal da gerçekten derleniyor | WebGL oyuncusunda `Handheld` yokluğu Unity'de görülmedi (kanıt incelemenin 2021.3 DLL'i ve Issue Tracker özeti); düzeltme iki durumda da zararsız |
| P3-02 | **Kapandı** (madde 1-2); madde 3 onay bekliyor | (1) `UnityStubs/UnityEngine.cs`: `Handheld` `#if UNITY_ANDROID \|\| UNITY_IOS` içinde. (2) Yeni `UnityStubs.Android` (aynı stub kaynakları, `UNITY_ANDROID`) ve `MinikUsta.Unity.Android` (aynı Unity katmanı kaynakları, `UNITY_ANDROID`) projeleri çözüme eklendi; ortak ayar ve kaynak yolu `dotnet/UnityLayer.props`. (3) Ek: `tools/stub-gate-selftest.sh` kapının kendisini sınar (korumasız `Handheld` varsayılan stub'ta `CS0234` ile derlenmez, Android stub'ında derlenir; korumalı biçim ikisinde derlenir) | `dotnet build -warnaserror` iki projeyi de derler; öz-sınama 4/4; stub'taki `#if` silinirse durum 1 kırmızı olur | Madde 3 (`tools/unity-refcheck.sh`, `UnityEngine.Modules` NuGet paketini `artifacts/`'a indirir) yeni bir NuGet indirmesi olduğundan **orkestratör onayı gerekir**; yapılmadı. `Input` / `Application` farkı: UnityCsReference 6000.0'da `Modules/InputLegacy/Input.bindings.cs:376` `public class Input` ve `Runtime/Export/Application/Application.bindings.cs:34,402` `public partial class Application` (static / sealed değil); stub zaten böyle, değişiklik gerekmedi. Stub'ta `#if`'siz duran ama başka bir platformda olmayan başka bir tür olup olmadığını yalnız GameCI ya da gerçek referans derlemesi gösterir |
| P3-03 | **Kapandı** | `docs/CI.md` §1: Unity Hub tek yol (Windows / macOS / Linux `Unity_lic.ulf` yolları; `UNITY_LICENSE` + `UNITY_EMAIL` + `UNITY_PASSWORD`); "Kurulumsuz" maddesi kaldırıldı. `.github/workflows/unity-activation.yml` silindi (`git rm`) | Bu oturumda web araması: Unity forumunda "Unity no longer supports manual activation of Personal licenses" başlığı ve GameCI belgelerindeki aynı uyarı (arama özeti; `game.ci` ve mirror alan adları bu oturumdan açılamadı, Unity destek makalesi de açılmadı) | Linux yolu (`~/.local/share/unity3d/Unity/Unity_lic.ulf`) ve "Hub'da lisans var ama dosya yok" çaresi (Editor'ü bir kez açmak) doğrulanmadı; CI.md bunu açıkça yazıyor. Plus/Pro yolu kapsam dışı bırakıldı |
| P3-04 | **Kapandı** | `Packages/manifest.json`: `com.unity.cinemachine` ve `com.unity.render-pipelines.universal` silindi (JSON geçerli; `Assets`'te kullanım yoktu: grep 0). İsteğe bağlı `particlesystem` ve `physics2d` modülleri **bırakıldı** | Manifest ayrıştırıldı; Assets taraması. Bırakma gerekçesi: WebGL'de motor kodu kırpması açık, kullanılmayan yerleşik modül zaten derlemeye girmez; kazanç yok, doğrulanamayan değişiklik riski var | Unity paket çözümlemesi (kalan paketler: `ugui 2.0.0`, `test-framework 1.4.5`) Unity'de denenmedi |
| P3-05 | **Kapandı** (kısmen ilk koşuya bağlı) | (a) `ProjectSettings/InputManager.asset` depoya eklendi: Unity 6000.0.69f1 (`Unity-Technologies/InputSystem`) dosyasından birebir kopya, 18 eksen (Horizontal, Vertical, Submit ×2, Cancel dahil), LF, BOM yok. (b) `.github/workflows/unity-webgl.yml`: Build'den sonra `unity-generated-files` artifact'ı (`ProjectSettings/`, `Packages/packages-lock.json`, `Assets/**/*.meta`; derleme kırmızı olsa da; 14 gün). (c) `docs/CI.md` §4: ilk yeşil koşudan sonra dosyaları depoya işleme adımları, hata tablosuna `is not setup` satırı | Kopya, 6000.0.9f1 projesinin (`game-programming-patterns-demo`) `InputManager.asset` dosyasının ilk 295 satırıyla `diff` ile **birebir aynı** (o projenin ek özel eksenleri sonraki satırlarda). Workflow YAML'ı ayrıştırıldı (adım sırası doğru) | **Dosyaların depoya işlenmesi ilk yeşil koşudan sonraya kaldı** (bugün hiç koşu yok; `.meta` ve Unity'nin tam biçimli ayarları ancak o koşuda doğar). Unity'nin dosya yokken kendisinin aynı varsayılanı yazıp yazmadığı ve elle konan dosyayı sorunsuz okuyup okumadığı görülmedi. Artifact'ın içinde klasör yapısının korunması (en küçük ortak üst klasör kuralı) ilk koşuda kontrol edilir |

### Q5 sonrası açık ve dokunulmayanlar

- **Orkestratör onayı bekleyen:** P3-02 madde 3 (`tools/unity-refcheck.sh`, yeni NuGet indirmesi).
- **Öneri (P3-06 … P3-18):** dokunulmadı, birikim listesinde. Not: P3-06 (`serializedVersion: 28` → `26`) ilk koşu çıktısı işlenince (CI.md §4)
  zaten Unity'nin kendi değeriyle yer değiştirir; P3-18 (`versioning: None`) tek satırdır ve ilk koşunun kırılma riskini küçültür, ilk
  Unity hatası geldiğinde ilk aday olarak alınmalı.
- **Kapı sözleşmesi:** Platforma bağlı yeni bir Unity API'si eklenirken önce kodda ve stub'ta aynı `#if` ile korunur (CI.md §3);
  `tools/stub-gate-selftest.sh` stub'a dokunan her değişiklikte çalıştırılır.
