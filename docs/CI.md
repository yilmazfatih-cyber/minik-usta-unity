# Bulut derlemesi (GitHub Actions + GameCI) — Unity kurmadan oynamak

Unity Editor kurmadan oyunu denemenin yolu: GitHub, projeyi kendi sunucusunda derler ve WebGL sürümünü GitHub Pages'e koyar.
İş akışı: `.github/workflows/unity-webgl.yml` (derleme + yayın; adı **Unity WebGL → Pages**). Lisans için ayrı bir iş akışı
ya da dosya yoktur (§1: yalnız iki secret; `unity-activation.yml` P3-03 ile kaldırıldı). Unity sürümü `ProjectSettings/ProjectVersion.txt` ile sabit:
**6000.0.85f1**; iş akışı sürümü oradan okur, imaj `unityci/editor:ubuntu-6000.0.85f1-webgl-3` Docker Hub'da var (2026-11-01'de denetlendi).

Yayın adresi (ilk başarılı derlemeden sonra açılır): https://yilmazfatih-cyber.github.io/minik-usta-unity/

**Durum:** Bu ortamda Unity derlenmez; tek kanıt Actions koşusudur. v6 geçişinde iki koşu yapıldı (2026-11-01):

| Koşu | Commit | Sonuç |
| --- | --- | --- |
| [41350030654](https://github.com/yilmazfatih-cyber/minik-usta-unity/actions/runs/41350030654) | `be4536a` | Lisans tamam: CLI v0.1.72 indi (SHA256 doğrulandı), `unityci/editor:ubuntu-6000.0.85f1-webgl-3` çekildi, **Personal koltuk yalnız `UNITY_EMAIL` + `UNITY_PASSWORD` ile alındı** (`Licensing method: personal`, `Seat ID: …-UnityPersonal`, editörde `Product: Unity Personal`) ve iş bitince **iade edildi** (`Successfully returned the entitlement license`). Derleme proje tarafında kırıldı: `Project has invalid dependencies: com.unity.modules.vibration` (§2b). |
| [41350760117](https://github.com/yilmazfatih-cyber/minik-usta-unity/actions/runs/41350760117) | `a2679b0` | Manifest düzeltmesi işledi. **Unity derlemesi başarılı**: hiç `error CS` yok (yalnız `CiBuild.cs(67,30): warning CS8632`, 2 kez), `Build Finished, Result: Success.`, `WebGL build succeeded: /github/workspace/build/WebGL/MinikUsta (35 MB)` (Unity derlemesi 5 dk 35 sn, iş toplamı 9 dk 13 sn); `Build/MinikUsta.{data,wasm,framework.js,loader.js}` ve `TemplateData/` günlükte görüldü; koltuk yine iade edildi. İş yine de **kırmızı** bitti: `[ERROR] Error: There was an error building the project. Please read the logs for details. at validateBuild`, ardından `Build failed with exit code 1`. Neden: CLI v0.1.72, Unity çıkış kodu 0 olsa bile konteyner çıktısında `Build succeeded!` (ya da `# Build results #` bloğunda `Errors: 0` + `Size:`) arar; `CiBuild` ikisini de yazmıyordu. v4 yalnız çıkış koduna bakardı, yani v6 geçişinde `buildMethod` sözleşmesi değişti (önceki "değişmedi" notu yanlıştı). Pages adımları atlandı. |

**Düzeltme (E1, bu commit):** `CiBuild.ReportSuccess` başarı yolunda yerleşik raporlayıcının metnini yazar (`# Build results #` bloğu ve `Build succeeded!`);
hata yolu bunu yazmaz, istisna atar (çıkış kodu 1). İş akışına koruma adımı eklendi (§2b). Bu ortamda doğrulananlar: `CiBuild.cs` gerçek dosya olarak
Unity API taslaklarıyla derlendi; başarı çıktısı CLI'nin iki denetiminden de geçti, hata çıktısı ikisinden de geçmedi (CLI denetiminin Python
portuyla; CLI kodu çalıştırılmadı); koruma betiği gerçek ve bozulmuş dosyalarla denendi. **Doğrulanmayan:** düzeltmeden sonraki gerçek koşu.
Kanıt o koşudur: Build adımı yeşil, günlükte `Build succeeded!`, Pages adımları çalışmış olmalı.

**Açık işler:**
- **Pages kaynağı (proje sahibi, bir kez):** Settings → Pages → Build and deployment → Source = **GitHub Actions** (§1 adım 3). `has_pages` `true`, ama kaynak türü
  bu ortamdan okunamadı (`GET /repos/…/pages` vekil sunucuda 403). Kaynak "Deploy from a branch" ise deploy işi `deploy-pages` adımında kırmızı biter.
- **İlk Pages dağıtımı:** henüz olmadı. Olunca deploy işinin `page_url` çıktısı buraya yazılacak: _(boş)_.
- **`unity-generated-files`:** 41350760117 koşusunun artifact'i hazır (400 dosya, 131560 bayt; **2026-11-15'te silinir**). §4 beklemeden uygulanabilir.

## 1. Tek seferlik: Unity hesabı ve gizli değerler

Personal lisans için **`.ulf` dosyası gerekmez** (ve çalışmaz): Unity Personal için elle etkinleştirmeyi kaldırdı, `.ulf` da onu üreten
makineye bağlıdır; her koşuda yeni açılan CI konteynerinde kullanılamaz. İş akışı **game-ci/unity-builder v6.0.0** kullanır; bu
eylem GameCI CLI'yi (sabit sürüm **v0.1.72**) indirip çalıştırır. CLI, yalnız `UNITY_EMAIL` + `UNITY_PASSWORD` verilince Personal
koltuğu doğrudan Unity hesabından alır ve konteyner kapanırken iade eder.

1. **Unity hesabı:** CI için ayrı bir hesap açılması önerilir (şifresi GitHub'a girer). Hesap **Unity ID + şifre** ile girilen türden
   olmalı; Google / Apple / Facebook ile açılmış (şifresiz) hesap çalışmaz. **İki adımlı doğrulama (2FA) kapalı** olmalı. Hesapta
   Personal lisans hakkı bulunmalı (Hub'da bir kez oturum açmak bunu tanımlar; yeni hesapta gerekip gerekmediği doğrulanmadı).
2. Repo → **Settings** → **Secrets and variables** → **Actions** → **Repository secrets** → **New repository secret**; iki değer:
   | Ad | Değer |
   | --- | --- |
   | `UNITY_EMAIL` | Unity hesabının e-postası |
   | `UNITY_PASSWORD` | Unity hesabının şifresi (başında/sonunda boşluk ya da satır sonu kalmasın) |

   Eski `UNITY_LICENSE` secret'ı varsa silinir; iş akışı onu bilerek geçirmez (otomatik modda dolu `UNITY_LICENSE` önce `.ulf` yolunu seçtirir).
   İki secret'tan biri boşsa iş, Unity açılmadan şu hatayla durur: `UNITY_EMAIL / UNITY_PASSWORD repository secrets are not set`.
3. Repo → **Settings** → **Pages** → **Source: GitHub Actions** (bir kez).

**İlk koşu:** GitHub'ın koşucuları her koşuda farklı IP'den bağlanır; Unity "yeni cihaz" doğrulama e-postası gönderebilir. E-postadaki
bağlantı onaylanır, sonra Actions → koşu → **Re-run jobs** ile yeniden başlatılır.

**Koltuk (seat):** Personal hesapta eşzamanlı koltuk çok azdır; bu yüzden iş akışı `unity-license` eşzamanlılık grubundadır (aynı anda
tek Unity işi, çalışan iş iptal edilmez, sırada en yeni koşu bekler). Unity çalıştıran her yeni iş akışı aynı grubu kullanır.
Koşu bitince koltuk otomatik iade edilir. Takılırsa (**çalışan bir Unity koşusunu elle iptal etme** ya da 120 dk zaman aşımı koltuğu
açık bırakabilir) sonraki koşular günlükte `no available Personal seats` ile düşer:
- https://id.unity.com adresinde oturum açıp takılı kalmış etkinleştirmeyi serbest bırak (CLI kaynağı menü yolunu vermiyor;
  Unity arayüzü değişebilir), ya da CLI'nin `game-ci return-license` komutuyla iade et; sonra koşuyu yeniden başlat.
- Aynı Unity hesabını başka bir CI işiyle eşzamanlı kullanma.

`.ulf` / `UNITY_SERIAL` yolu eski seçenektir: yalnız Enterprise/Industry (`.ulf`) ya da Plus/Pro (`UNITY_SERIAL`) koltuğu içindir ve bu iş akışına bağlı değildir.

Kaynaklar: unity-builder [v6.0.0 sürüm notu](https://github.com/game-ci/unity-builder/releases) ve
[action.yml](https://github.com/game-ci/unity-builder/blob/v6.0.0/action.yml) ·
GameCI CLI [PR #246](https://github.com/game-ci/cli/pull/246) (`personal` stratejisi, koltuk iadesi), sürüm notları
[v0.1.62, v0.1.63, v0.1.66](https://github.com/game-ci/cli/releases) ·
[lisans seçimi ve hata açıklamaları](https://github.com/game-ci/cli/blob/v0.1.72/dist/platforms/ubuntu/steps/licensing_method.sh) ·
derleme doğrulaması: [unity-build-validation.ts](https://github.com/game-ci/cli/blob/v0.1.72/src/model/unity/build-validation/unity-build-validation.ts),
[docker.ts](https://github.com/game-ci/cli/blob/v0.1.72/src/model/docker.ts) (`validateBuild` her Unity derlemesinde çağrılır),
[StdOutReporter.cs](https://github.com/game-ci/cli/blob/v0.1.72/dist/default-build-script/Assets/Editor/UnityBuilderAction/Reporting/StdOutReporter.cs) (CLI'nin kendi derleyicisinin yazdığı metin).

## 2. Derleme ve oynama

- `main` dalına `Assets/`, `Packages/` ya da `ProjectSettings/` altında bir değişiklik gelince **Unity WebGL → Pages**
  iş akışı kendiliğinden çalışır; elle de başlatılabilir (Actions → Run workflow).
- Sahne: `Assets/MinikUsta/Scenes/Level.unity` (Faz 3 Q1). Sahne listesi tek yerden gelir: `ProjectSettings/EditorBuildSettings.asset`
  içinde `Assets/MinikUsta/Scenes/Level.unity`, GUID `a7cea9e5c5164f70b12f92c99359b9a4` (`Level.unity.meta` ile aynı).
  `CiBuild.BuildWebGL` sahne listesini bu dosyadan okur. İş akışının "Check for a scene" adımı `Assets/MinikUsta/Scenes/*.unity`
  kalıbına bakar; sahne var olduğu için gerçek derleme her `main` itişinde koşar.
- Sahnede yalnız ortografik Main Camera vardır; oyun `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` ile koddan kurulur
  (`Assets/MinikUsta/Unity/GameBootstrap.cs`, yalnız aktif sahne adı `Level` ise). Sahne YAML'ı hiçbir betik GUID'ine başvurmaz.
- Çıktı yolu: `CiBuild.BuildWebGL`, GameCI'nin verdiği `-customBuildPath` argümanını kullanır; argüman yoksa
  `build/WebGL/MinikUsta`. Pages adımı `build/WebGL/MinikUsta` klasörünü yükler; iş akışı bu klasörü `ls -la` ile yazdırır.
- **`buildMethod` sözleşmesi (v6):** Unity'nin çıkış kodu tek başına yetmez. CLI'nin `validateBuild` denetimi konteyner çıktısında
  `Build succeeded!` arar; yoksa `# Build results #` bloğunda `Errors: 0` ve `Size:` satırı arar; ikisi de yoksa Unity başarılı olsa bile adımı
  kırmızı bitirir. `CiBuild.ReportSuccess` ikisini de yazar ve `BuildWebGL` ile `BuildAndroid` başarılı `BuildPlayer` sonucundan sonra çağırır.
  Yeni bir giriş noktası eklenirse (örn. iOS) aynı çağrı oraya da konur. Hata yolu bu metni asla yazmaz.
- Süre: ölçülen tek değer 41350760117 koşusudur: iş toplamı 9 dk 13 sn (Unity derlemesi 5 dk 35 sn; imaj çekimi ve boş Library dahil). Önbellek
  yalnız yeşil işte kaydedilir (kırmızı işte `Post Cache Library` atlanır), yani ilk yeşil koşu da önbelleksiz sayılır; sonrakilerin daha kısa olması beklenir, ölçülmedi.
- Çıktı: https://yilmazfatih-cyber.github.io/minik-usta-unity/ (web sürümünden ayrı adres).

## 2a. İlk koşuda beklenen sonuç

Actions → **Unity WebGL → Pages** → **Run workflow**. İş: **Build WebGL (GameCI)**. Beklenen adımlar:

| Adım | Beklenen | Başarısızlıkta |
| --- | --- | --- |
| Check for a scene | `has=true`; `::notice::` mesajı yok | Sahne dosyası yok ya da yol farklı |
| Check CiBuild reports success to the GameCI CLI | Sessizce geçer (saniyeler) | `::error file=Assets/MinikUsta/Editor/CiBuild.cs::…`: `CiBuild.cs` artık `Build succeeded!` yazmıyor ya da `ReportSuccess(report.summary);` çağrısı iki giriş noktasında da yok (§2b, ilk satır). Unity açılmadan durur |
| Check Unity account secrets | Sessizce geçer (değerler yazdırılmaz) | `::error::UNITY_EMAIL / UNITY_PASSWORD repository secrets are not set`: §1 adım 2. Sonraki Unity adımları atlanır |
| Cache Library | İlk koşuda önbellek boş; normal | — |
| Build | CLI indirilir (`Installing game-ci CLI v0.1.72`), imaj çekilir, günlükte `Requesting activation (personal license via Unity account)`, Unity açılır, `CiBuild.BuildWebGL` çalışır. Başarıda sırayla `Build Finished, Result: Success.`, `WebGL build succeeded: /github/workspace/build/WebGL/MinikUsta (N MB)` (yol konteyner içindedir, mutlak), `# Build results #` bloğu (`Errors: 0`, `Size:`), `Build succeeded!` ve build.sh'ın kendi `Build succeeded` satırı (ünlemsiz; CLI için sayılmaz); sonunda koltuk iade edilir | Lisans: §2b tablosu. Derleme: `error CS…`. Betik: `WebGL build failed: …`. Unity yeşil ama adım kırmızı (`at validateBuild`): §2b |
| Collect / Upload files Unity generated | Koşu sayfasının **Artifacts** bölümünde `unity-generated-files` (§4); derleme kırmızı olsa da yüklenir | Adım atlanırsa sahne yok (`has=false`) ya da Build adımı hiç başlamadı (secret denetimi kırmızı) |
| Prepare Pages artifact | `ls` çıktısında `index.html`, `Build/` (loader, framework, data, wasm dosyaları) ve `TemplateData/`. Sıkıştırma kapalı olduğundan `.br` / `.gz` dosyası olmaz | Klasör yoksa çıktı yolu farklı (§2b) |
| Upload Pages artifact, Deploy to GitHub Pages | Deploy işinin `page_url` çıktısındaki adres açılır | 404: Pages kaynağı "GitHub Actions" değil (§1 adım 3) |

Açılışta Pages adresi doğrudan Bölüm 1'i gösterir (menü yok). Bölüm dosyası geçersizse oyun oynanmaz ve tarayıcı konsoluna
`Minik Usta: …` hatası yazılır (`GameRunner`; geçersiz bölüm yüklenmez, D-005).

## 2b. Hata ayıklama

### Günlük nerede

GitHub → repo → **Actions** → en son **Unity WebGL → Pages** koşusu → iş **Build WebGL (GameCI)** → adım **Build** → günlük.
Derleme hataları `error CS####` ile başlar ve dosya yolunu içerir (`Assets/MinikUsta/…`); lisans hataları aynı adımın başında çıkar.
Günlükte arama için tarayıcının Ctrl+F'i kullanılır. Pages dosyaları koşu sayfasındaki **Artifacts** bölümünden indirilebilir.

### Tarayıcı konsolu

Yayındaki oyunun hataları tarayıcı konsoluna düşer: masaüstü Chrome'da F12 → Console. Telefonda konsol yok; hata ayıklama
masaüstü tarayıcıdan yapılır.

### Belirti → neden → yapılacak

| Belirti | Olası neden | Yapılacak |
| --- | --- | --- |
| Günlükte `Build Finished, Result: Success.` ve `WebGL build succeeded: …` var, sonra `[ERROR] Error: There was an error building the project. Please read the logs for details. at validateBuild` ve `Build failed with exit code 1`; Pages adımları atlanır | `buildMethod` `Build succeeded!` ya da `# Build results #` bloğu yazmıyor. CLI v0.1.72 bunu Unity çıkış kodundan ayrı denetler (`unity-build-validation.ts`); v4'te bu denetim yoktu. build.sh'ın `Build succeeded` satırı (ünlemsiz) sayılmaz. İlk görüldüğü koşu: 41350760117 | `CiBuild.ReportSuccess` çağrılıyor mu bak (E1); iş akışındaki koruma adımı bunu Unity'den önce denetler. Günlükte `Build succeeded!` (ünlemli) ara |
| `UNITY_EMAIL / UNITY_PASSWORD repository secrets are not set` | İki secret'tan biri yok (ya da Environment/Organization secret'ı olarak girilmiş) | §1 adım 2; **Repository secrets** altında |
| `Unity rejected the account credentials, or wants a second factor` | Yanlış e-posta/şifre, sosyal giriş hesabı, 2FA açık ya da yeni IP için doğrulama e-postası bekliyor | §1 adım 1; e-postayı onayla, **Re-run jobs** |
| `Unity reports no available Personal seats for this account` | Önceki iş koltuğu iade etmeden bitti (iptal, zaman aşımı) ya da hesap başka işte kullanımda | §1 "Koltuk": id.unity.com'da takılı etkinleştirmeyi serbest bırak, yeniden başlat |
| `Unity did not grant a Personal seat for this editor version` | Unity koltuk vermedi (CLI kaynağı geçici olabildiğini söylüyor) | Koşuyu yeniden başlat; sürerse günlükle code-lead'e |
| `error CS0234` / `CS0246` / `CS0103` / `CS1061` (`Assets/MinikUsta/Unity/…`) | Unity API imzası ya da asmdef referansı | Hata metnindeki ad `UnityEngine.UI` ise `MinikUsta.Unity.asmdef` referansına bak (§3). Ad `Handheld` gibi yalnız mobil hedefte var olan bir tür ise `#if UNITY_ANDROID || UNITY_IOS` ile koru; stub'da da aynı koşul olmalı (§3). Başka bir API ise `dotnet/UnityStubs/UnityEngine.cs` imzası gerçek Unity imzasıyla düzeltilir, sonra Unity katmanı. Kod değişikliği code-lead ile |
| `error CS…` (`Assets/MinikUsta/Core/…` ya da `Presentation.Core/…`) | Unity derleyicisi dotnet'ten farklı davranıyor (dil sürümü ya da tanımlar) | Önce `cd dotnet && dotnet build -warnaserror && dotnet test` ile yeniden üret; sürerse ilk hata satırıyla code-lead'e |
| Oyun açılıyor ama hiçbir uGUI düğmesi çalışmıyor (Mola, Devam); konsolda her karede `Input Button Submit is not setup` | `ProjectSettings/InputManager.asset` yok ya da eksenleri eksik (P3-05) | Depodaki `InputManager.asset` Unity 6000.0'ın varsayılanıdır; yoksa §4'teki artifact'ten al. Günlükte `is not setup` ara |
| `WebGL build failed: <sonuç> (N errors)` | Build pipeline hatası (`CiBuild.cs`) | Günlükteki ilk `error` satırına bak; sahne yolu ve GUID §2 ile karşılaştırılır |
| `Project has invalid dependencies … cannot be found` (Unity, `Build failed, with exit code 1` öncesinde) | `Packages/manifest.json` Unity 6000.0.85f1'de olmayan bir paketi istiyor (ilk koşuda `com.unity.modules.vibration`) | İlgili satırı `manifest.json`'dan kaldır; `Handheld` `CoreModule`'dadır, ayrı modül gerekmez |
| Günlüğün sonunda `[ERROR] Unable to find image 'unityci/editor:…' locally` | Yanıltıcı: docker'ın "imaj yerelde yok, çekiliyorum" satırı. Neden değildir | Gerçek neden `Build failed, with exit code 1` satırından önceki Unity satırlarındadır (`error CS`, `Project has invalid dependencies` …) |
| `Prepare Pages artifact` içinde `build/WebGL/MinikUsta` yok | GameCI çıktı yolu farklı | "Build" adımındaki `WebGL build succeeded: /github/workspace/…` satırındaki yolu al (başındaki `/github/workspace/` konteynerdeki çalışma alanıdır, iş akışında `build/WebGL/MinikUsta`ya karşılık gelir); `CiBuild.cs` varsayılanı ile iş akışındaki yol aynı olmalı |
| Yayın açılıyor ama ekran boş ya da siyah | Sahne açılmadı (aktif sahne adı `Level` değil) ya da bölüm yüklenemedi | Tarayıcı konsolunda `Minik Usta: …` varsa bölüm dosyası; yoksa sahne adı ve §2 |
| Pages 404 | Pages kaynağı "GitHub Actions" değil ya da deploy işi atlandı | §1 adım 3. Deploy işi yalnız `has_scene == 'true'` ise ve Build işi yeşilse çalışır |
| İlk açılış çok yavaş | WebGL sıkıştırması kapalı; indirme büyük | Beklenen; §3 |

### Yeniden deneme

Koşu başarısızsa düzeltme commit'i `main`'e girince iş akışı kendiliğinden yeniden çalışır. Elle yeniden başlatmak için
Actions → Run workflow.

## 3. Bilinmesi gerekenler

- **Bulutta Unity yok (D-093):** Unity katmanı (`Assets/MinikUsta/Unity`) burada yalnız `dotnet/UnityStubs` imza şimlerine karşı
  derlenir (yazım ve imza denetimi). Gerçek Unity derlemesinin tek kanıtı bu iş akışıdır. Elle yazılmış dosyalar: `Level.unity`
  (+ `.meta`), `ProjectSettings/EditorBuildSettings.asset`, `ProjectSettings/ProjectSettings.asset` (yalnız gerekli alanlar; kalanını
  Unity varsayılanla doldurur ve ilk açılışta yeniden yazar), `ProjectSettings/InputManager.asset` (§4).
- **Stub denetiminin sınırı ve platform kuralı (P3-01, P3-02):** Stub "imza doğru mu" sorusunu yanıtlar; "bu tür bu platformda var mı"
  sorusunu yalnız açıkça `#if` ile işaretlenen türler için yanıtlar. `Handheld` Unity oyuncusunda yalnız Android ve iOS'ta vardır;
  WebGL'de `CS0103` verir (`GameRunner.cs:42` ilk koşuda bu yüzden kırılacaktı). Bu yüzden:
  - `dotnet/MinikUsta.Unity` hiç `UNITY_*` simgesi tanımlamaz (WebGL'in durumu); `dotnet/MinikUsta.Unity.Android` aynı kaynakları
    `UNITY_ANDROID` ile, `dotnet/UnityStubs.Android` stub'ıyla derler. `dotnet build -warnaserror` ikisini de derler, yani her
    `#if UNITY_ANDROID || UNITY_IOS` dalının iki tarafı da denetlenir (iOS ayrı proje istemez: dal Android ile aynı tarafı alır).
  - Platforma bağlı her Unity türü ya da üyesi hem Unity katmanında hem `dotnet/UnityStubs/UnityEngine.cs` içinde aynı `#if` ile
    korunur. Yeni bir platforma bağlı API eklenirse (ör. `Handheld`, `iPhone`, `Application.ExternalCall`) önce bu kural uygulanır.
  - `tools/stub-gate-selftest.sh` (~10 sn) kapının çalıştığını kanıtlar: korumasız `Handheld` varsayılan stub'ta derlenmez, Android
    stub'ında derlenir. Stub'taki `#if` silinirse betik kırmızı olur. Stub'a dokunan her değişiklikte çalıştırılır.
  - Hâlâ görülemeyen: stub'ta `#if`'siz duran bir türün başka bir platformda olmaması. Bunu yalnız gerçek derleme (GameCI) ya da gerçek
    referansa karşı derleme gösterir; ikincisi (`UnityEngine.Modules` NuGet paketini `artifacts/`'a indiren `tools/unity-refcheck.sh`)
    yeni bir NuGet indirmesi olduğundan orkestratör onayı bekliyor.
- **uGUI asmdef adı (kapandı):** `Assets/MinikUsta/Unity/MinikUsta.Unity.asmdef` `UnityEngine.UI` adıyla referans veriyor. 41350760117
  koşusunda betikler derlendi (hiç `error CS` yok); `UnityEngine.UI` referansı doğrulandı. Aday adlar (`Unity.ugui`, `Unity.UI`) gerekmedi.
  Başka bir asmdef adı hatası çıkarsa kesin cevap `Library/PackageCache/com.unity.ugui@*/` altındaki `.asmdef` dosyasının adıdır.
- **`ProjectSettings.asset`:** `companyName` "Tuna & Co.", `productName` "Minik Usta", portre (`defaultScreenOrientation: 0`),
  eski Input Manager (`activeInputHandler: 0`), WebGL sıkıştırma **kapalı** (`webGLCompressionFormat: 2`; Unity'de
  `WebGLCompressionFormat` = Brotli 0, Gzip 1, Disabled 2; UnityCsReference `webgl.cs` kaynağıyla doğrulandı), gamma renk uzayı.
  `CiBuild.BuildWebGL` sıkıştırmayı zaten derleme anında kapatır.
- **`.meta` dosyaları:** Repoda şu an yalnız `Level.unity.meta` izlenir. Unity diğer `.meta` dosyalarını her koşuda yeni GUID'le
  üretir (Library önbelleği isabetsizleşir). §4 ile depoya işlenir (karar: işlenir; GUID kararlılığı). İlk yeşil koşu beklenmez: 41350760117 artifact'i hazır.
- **Beklenen uyarı (görüldü):** `CiBuild.cs`, `#nullable` bağlamı olmadan `string?` kullanır; Unity derleyicisi bunu uyarı (CS8632,
  `CiBuild.cs(67,30)`) olarak verdi (41350760117, 2 kez). GameCI uyarıları hata saymaz; derleme durmadı.
- WebGL derlemesi sıkıştırmasız üretilir (`CiBuild.BuildWebGL`); GitHub Pages Brotli/Gzip başlıklarını vermediği için zorunlu.
  İndirme boyutu bu yüzden büyüktür (tahmini ≈ 30–60 MB); telefon tarayıcısında ilk açılış yavaş olabilir.
- Telefonda gerçek his için Android APK daha doğrudur: `CiBuild.BuildAndroid` hazır; ayrı bir iş akışı istendiğinde eklenir
  (imza anahtarı gerekmez, debug APK "bilinmeyen kaynak" izniyle kurulur).
- Lisans dosyası olmadığı için süresi dolan bir şey yoktur; Unity şifresi değişirse `UNITY_PASSWORD` secret'ı güncellenir.
- **unity-builder v6 notları (v4'ten geçiş):** Eylem artık GameCI CLI'yi indirip çalıştırır; `cliVersion` **v0.1.72**'ye sabit
  (varsayılan `latest` idi; yükseltme bilerek yapılır). `unityVersion` girdisi verilmez: sürüm `ProjectVersion.txt`'ten okunur
  (v6.0.0 `action.yml` açıklaması "auto dışı değer yok sayılır" der, ama kaynak kodu `--engineVersion` olarak geçirir; belge bayat).
  `UNITY_LICENSE` geçirilmez. `unityVersion` çelişkisi kaynaktan çözüldü: v6.0.0 `src/build-args.ts` (L241-244) `auto` dışındaki değeri
  `--engineVersion` olarak geçirir, `action.yml` açıklaması bayat; biz zaten `auto` bırakırız. Çıktı yolu değişmedi: `buildsPath` varsayılanı `build` + `WebGL` + `buildName` = `build/WebGL/MinikUsta`
  (`-customBuildPath` CLI'nin `build.sh` betiğinden aynı biçimde gelir). **Değişen şey `buildMethod` sözleşmesidir**: v4 yalnız çıkış koduna
  bakardı, v6 CLI'si ek olarak çıktıda `Build succeeded!` arar (§2, E1). `versioning` varsayılan (`Semantic`) kaldı: CLI sığ klonu
  kendisi derinleştirir (`git fetch --unshallow`), etiket yoksa sürüm commit sayısından üretilir; `allowDirtyBuild: true` bunun için gerekli.
- **Paketler (P3-04):** `Packages/manifest.json` yalnız kullanılanı içerir: `com.unity.ugui`, `com.unity.test-framework` ve yerleşik
  modüller. URP ve Cinemachine çıkarıldı (D-096'ya göre kullanılmıyorlar; her koşuda içe aktarılıp derleniyorlardı, paket içi bir hata
  derlememizi düşürebilirdi). Yerleşik `particlesystem` ve `physics2d` modülleri kullanılmıyor ama bırakıldı: motor kodu kırpması
  (WebGL'de açık) kullanılmayan modülü zaten derlemeden çıkarır, kaldırmanın kazancı yok.
- Lisans akışı (unity-builder v6 + CLI v0.1.72 + Personal hesap, 6000.0.85f1) uçtan uca çalıştı (etkinleştirme + iade; bkz. Durum).
  Çıktı yolu (`-customBuildPath /github/workspace/build/WebGL/MinikUsta`) ve sığ klonda `Semantic` sürümleme (`0.0.40`, ikinci koşuda `0.0.42`) günlükte doğrulandı.
  Unity WebGL derlemesi ikinci koşuda başarılı oldu (35 MB). Henüz görülmeyen: CLI doğrulamasından geçen bir derleme (E1 düzeltmesi bekliyor) ve Pages dağıtımı.

## 4. Unity'nin ürettiği dosyaları depoya işle (P3-05)

Bulutta Unity açılamadığı için bazı dosyaları Unity ilk koşuda kendisi üretir: eksik `ProjectSettings/*.asset` dosyaları
(Graphics, Quality, Tag …), `ProjectSettings.asset`'in tam biçimi, `Packages/packages-lock.json` ve her varlığın `.meta` dosyası
(betik GUID'leri). Depoda yoklarsa her koşuda yeni GUID'le yeniden üretilirler: Library önbelleği isabetsizleşir ve ileride bir
varlık (prefab, ScriptableObject) bir betiğe GUID ile bağlanırsa her klonda kopar. İş akışı bu dosyaları her koşuda
**`unity-generated-files`** adlı artifact olarak yükler (derleme kırmızı olsa da; saklama 14 gün).

**İlk yeşil koşuyu beklemek gerekmez.** Unity derlemesi 41350760117 koşusunda başarılı oldu ve artifact'i hazır (400 dosya, 131560 bayt;
**2026-11-15'te silinir**). E1 düzeltmesinden sonraki her koşu yeni bir artifact üretir; hangisi alınırsa alınsın aynı adımlar geçerlidir.

**Çok ajanlı depo kuralı:** bu depoda aynı anda başka ajanlar çalışır; çalışma ağacında başkalarının yarım işi ve hazırlanmış (staged)
değişiklikleri bulunabilir. Bu yüzden **`git add -A` kullanılmaz**, yalnız Unity'nin ürettiği yollar eklenir ve işlem ayrı bir çalışma ağacında yapılır.
Kirli ağaçta `git pull --rebase` zaten reddedilir; temiz ağaç hem bunu hem yanlışlıkla başkasının dosyasını commit'lemeyi önler.

1. Artifact'i indir (içinde `ProjectSettings/`, `Packages/` ve `Assets/` olmalı):
   ```
   gh run download 41350760117 -n unity-generated-files -D ../unity-generated
   ```
2. `origin/main`'den temiz bir çalışma ağacı aç ve artifact'i üzerine kopyala (aynı ad varsa üzerine yazılsın):
   ```
   git fetch origin
   git worktree add --detach ../minik-usta-unity-meta origin/main
   cp -a ../unity-generated/. ../minik-usta-unity-meta/
   cd ../minik-usta-unity-meta
   ```
3. Yalnız Unity'nin yollarını ekle:
   ```
   git add ProjectSettings/ Packages/packages-lock.json
   git add -- ':(glob)Assets/**/*.meta'
   ```
4. Denetle. `git status --short` yalnız yeni `.meta` dosyalarını, `ProjectSettings/` altındaki yeni ya da değişen dosyaları ve
   `packages-lock.json` değişikliğini göstermelidir; başka bir yol beklenmedik demektir, commit öncesi bakılır. Artifact eski bir commit'ten
   geldiyse, sonradan silinmiş ya da taşınmış bir varlığın `.meta` dosyası (yetim) gelebilir; aşağıdaki komut onları listeler, çıkanlar dizinden çıkarılır:
   ```
   git diff --cached --name-only | grep '\.meta$' | while read -r m; do [ -e "${m%.meta}" ] || echo "yetim: $m"; done
   git rm --cached -- <yetim .meta yolu>
   ```
   (Artifact'te olmayan, sonradan eklenmiş varlıkların `.meta` dosyası yoktur; onlar sonraki artifact'le gelir.)
5. Commit ve gönder:
   ```
   git commit -m "chore: commit Unity generated settings and meta files"
   git push origin HEAD:main
   ```
   Uzak dal ilerlemişse `git fetch origin && git rebase origin/main` (bu ağaç temiz olduğundan çalışır), sonra yeniden `git push origin HEAD:main`.
   Bitince çalışma ağacını kaldır: `cd - && git worktree remove ../minik-usta-unity-meta`.

   Çalışma ağacı temizse (tek ajan) aynı sonuca ana ağaçta `git add` komutları, `git pull --rebase origin main` ve `git push origin main` ile de varılır.

   Sonraki koşu Library önbelleğini bir kez kaçırır (anahtar `Assets/**` özetini içerir), sonra GUID'ler sabittir.
6. Günlükte `is not setup` ara. `ProjectSettings/InputManager.asset` depoda hazırdır: Unity 6000.0.69f1 ve 6000.0.9f1 projelerinin
   (Unity-Technologies/InputSystem, game-programming-patterns-demo) ortak varsayılanıdır, 18 eksen (Horizontal, Vertical, Submit,
   Cancel …). Uygulama bu eksenleri `StandaloneInputModule` yüzünden ister: eksik olsa her karede istisna atılır, hiçbir uGUI düğmesi
   çalışmaz ve oyun ilk kazanmada kilitlenirdi. Aramada `is not setup` çıkarsa dosya yüklenmemiş demektir; artifact'teki `InputManager.asset`
   ile karşılaştırılır.

Bu adım code-lead'in işidir. Yol belirteçleri, yetim `.meta` denetimi ve `git worktree` akışı geçici bir depoda (kirli ağaç ve başkasının staged dosyasıyla)
denendi; gerçek artifact ile henüz uygulanmadı.
