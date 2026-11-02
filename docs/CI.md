# Bulut derlemesi (GitHub Actions + GameCI) — Unity kurmadan oynamak

Unity Editor kurmadan oyunu denemenin yolu: GitHub, projeyi kendi sunucusunda derler ve WebGL sürümünü GitHub Pages'e koyar.
İş akışı: `.github/workflows/unity-webgl.yml` (derleme + yayın; adı **Unity WebGL → Pages**). Lisans için ayrı bir iş akışı
ya da dosya yoktur (§1: yalnız iki secret; `unity-activation.yml` P3-03 ile kaldırıldı). Unity sürümü `ProjectSettings/ProjectVersion.txt` ile sabit:
**6000.0.85f1**; iş akışı sürümü oradan okur, imaj `unityci/editor:ubuntu-6000.0.85f1-webgl-3` Docker Hub'da var (2026-11-01'de denetlendi).

Yayın adresi: https://yilmazfatih-cyber.github.io/minik-usta-unity/ (ilk dağıtım koşu 41352478361'de başarıyla bildirildi; sayfanın içeriği bu ortamdan açılamadı, bkz. Durum)

**Durum:** Bu ortamda Unity derlenmez; tek kanıt Actions koşusudur. v6 geçişinde üç koşu yapıldı (2026-11-01); sonuncusu ilk yeşil koşudur:

| Koşu | Commit | Sonuç |
| --- | --- | --- |
| [41350030654](https://github.com/yilmazfatih-cyber/minik-usta-unity/actions/runs/41350030654) | `be4536a` | Lisans tamam: CLI v0.1.72 indi (SHA256 doğrulandı), `unityci/editor:ubuntu-6000.0.85f1-webgl-3` çekildi, **Personal koltuk yalnız `UNITY_EMAIL` + `UNITY_PASSWORD` ile alındı** (`Licensing method: personal`, `Seat ID: …-UnityPersonal`, editörde `Product: Unity Personal`) ve iş bitince **iade edildi** (`Successfully returned the entitlement license`). Derleme proje tarafında kırıldı: `Project has invalid dependencies: com.unity.modules.vibration` (§2b). |
| [41350760117](https://github.com/yilmazfatih-cyber/minik-usta-unity/actions/runs/41350760117) | `a2679b0` | Manifest düzeltmesi işledi. **Unity derlemesi başarılı**: hiç `error CS` yok (yalnız `CiBuild.cs(67,30): warning CS8632`, 2 kez), `Build Finished, Result: Success.`, `WebGL build succeeded: /github/workspace/build/WebGL/MinikUsta (35 MB)` (Unity derlemesi 5 dk 35 sn, iş toplamı 9 dk 13 sn); `Build/MinikUsta.{data,wasm,framework.js,loader.js}` ve `TemplateData/` günlükte görüldü; koltuk yine iade edildi. İş yine de **kırmızı** bitti: `[ERROR] Error: There was an error building the project. Please read the logs for details. at validateBuild`, ardından `Build failed with exit code 1`. Neden: CLI v0.1.72, Unity çıkış kodu 0 olsa bile konteyner çıktısında `Build succeeded!` (ya da `# Build results #` bloğunda `Errors: 0` + `Size:`) arar; `CiBuild` ikisini de yazmıyordu. v4 yalnız çıkış koduna bakardı, yani v6 geçişinde `buildMethod` sözleşmesi değişti (önceki "değişmedi" notu yanlıştı). Pages adımları atlandı. |
| [41352478361](https://github.com/yilmazfatih-cyber/minik-usta-unity/actions/runs/41352478361) | `f119d06` | **İlk yeşil koşu.** `Check CiBuild reports success to the GameCI CLI` geçti; Unity derlemesi başarılı (`WebGL build succeeded: /github/workspace/build/WebGL/MinikUsta (35 MB)`, `Errors: 0`, `Warnings: 0`, `Size: 37569149 bytes`); günlükte `Build succeeded!` var ve `validateBuild` hatası yok; koltuk iade edildi. `Prepare Pages artifact` çıktısında `index.html`, `Build/`, `StreamingAssets/`, `TemplateData/` listelendi. **Deploy to GitHub Pages** işi `Reported success!` ile bitti, `Evaluated environment url: https://yilmazfatih-cyber.github.io/minik-usta-unity/`. İş toplamı 9 dk 31 sn (Unity derlemesi 5 dk 43 sn), deploy 20 sn. Artifact'ler: `github-pages` (12,9 MB) ve `unity-generated-files` (400 dosya, 131555 bayt; **2026-11-15'te silinir**). |

**E1 kapandı (41352478361):** `CiBuild.ReportSuccess` başarı yolunda yerleşik raporlayıcının metnini yazar (`# Build results #` bloğu ve `Build succeeded!`);
hata yolu bunu yazmaz, istisna atar (çıkış kodu 1). İş akışındaki koruma adımı bunu Unity'den önce denetler (§2b). Düzeltme push'tan önce bu ortamda şöyle
sınandı: `CiBuild.cs` gerçek dosya olarak Unity API taslaklarıyla derlenip çalıştırıldı; başarı çıktısı CLI'nin iki denetiminden de geçti, hata
çıktısı ikisinden de geçmedi (denetimin Python portuyla; CLI kodu çalıştırılmadı); koruma betiği gerçek ve bozulmuş dosyalarla denendi. Kesin kanıt yukarıdaki yeşil koşudur.

**Not (savunma katmanı):** aynı gün `acc3f82` commit'i ek bir koruma getirdi: Build adımı `continue-on-error: true`, başarı diskteki oyuncuya bakan
"Verify WebGL output" adımıyla belirlenir (§2b "Unity derlemesi başarılı ama adım kırmızı"). E1 düzeltmesi 41352478361'de Build adımını kendi başına
yeşil bitirdi, yani `continue-on-error` artık yalnız ikinci savunma hattıdır; Verify adımı bağımsız olarak değerlidir. `continue-on-error` kalsın mı
sorusu orkestratöre bırakıldı (kalırsa CLI'nin gelecekteki yanlış kırmızıları yayını engellemez; kalkarsa Build adımı gerçek durumu yansıtır).

**Pages kaynağı (O2):** `deploy-pages` adımı başarılı oldu; kaynak "GitHub Actions" değilse bu adım kırmızı biterdi, yani ayar doğru (dolaylı kanıt: Settings ekranı ve
`GET /repos/…/pages` bu ortamdan okunamıyor, vekil sunucu 403 veriyor).

**Açık işler:**
- **Yayının içeriği doğrulanmadı:** vekil sunucu `*.github.io` adresini engelliyor, bu yüzden sayfa ve `Build/*` dosyaları HTTP ile denenemedi. Proje sahibi adresi tarayıcıda bir kez açıp
  oyunun yüklendiğini teyit etmeli (WebGL ilk açılışta indirme büyüktür, §3). Sonuç `docs/CI.md` Durum bölümüne yazılır.
- **`unity-generated-files` işlenmedi:** artifact hazır, §4 uygulanabilir (P3-05). Bu görevin kapsamı dışında bırakıldı; sıradaki iş.
- `CiBuild.cs(92,30): warning CS8632` (`string?` için `#nullable` bağlamı yok) sürüyor; zararsız (§3), düzeltme gerekirse ayrı küçük iş.

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

**Koltuk (seat):** Personal hesapta eşzamanlı koltuk çok azdır; bu yüzden **Unity çalıştıran iş** (`build`) `unity-license` eşzamanlılık grubundadır
(aynı anda tek Unity işi, çalışan iş iptal edilmez, sırada en yeni koşu bekler). Paket C1'den beri grup **iş düzeyindedir**, iş akışı düzeyinde
değil: ekran görüntüsü işi ve Pages dağıtımı koltuk kullanmaz, bir sonraki derlemeyi bekletmemelidir. Unity çalıştıran her yeni iş
(`unity-android.yml` `build` işi) aynı grup adını kullanır.
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
| Verify WebGL output | Dosya listesi (bayt ve yol) yazdırılır; `index.html`, `Build/*.wasm*` ve `Build/*.data*` var ve boş değil. Build adımı kırmızı olsa bile bu adım geçerse iş yeşil kalır ve `::warning::` yazar (§2b "Unity derlemesi başarılı ama adım kırmızı") | `::error::Unity build produced no WebGL player (see the Build step log)`: gerçek derleme hatası; nedeni Build adımının günlüğünde ara. Pages adımları ve deploy atlanır |
| Collect / Upload files Unity generated | Koşu sayfasının **Artifacts** bölümünde `unity-generated-files` (§4); derleme kırmızı olsa da yüklenir | Adım atlanırsa sahne yok (`has=false`) ya da Build adımı hiç başlamadı (secret denetimi kırmızı) |
| Prepare Pages artifact | `ls` çıktısında `index.html`, `Build/` (loader, framework, data, wasm dosyaları), `StreamingAssets/` ve `TemplateData/` (41352478361'de görüldü). Sıkıştırma kapalı olduğundan `.br` / `.gz` dosyası olmaz | Klasör yoksa çıktı yolu farklı (§2b) |
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
| Günlükte `Build Finished, Result: Success.` ve `WebGL build succeeded: …` var, sonra `[ERROR] Error: There was an error building the project. Please read the logs for details. at validateBuild` ve `Build failed with exit code 1`; Pages adımları atlanır | `buildMethod` `Build succeeded!` ya da `# Build results #` bloğu yazmıyor. CLI v0.1.72 bunu Unity çıkış kodundan ayrı denetler (`unity-build-validation.ts`); v4'te bu denetim yoktu. build.sh'ın `Build succeeded` satırı (ünlemsiz) sayılmaz. İlk görüldüğü koşu: 41350760117. Ayrıntı ve koruma: aşağıdaki "Unity derlemesi başarılı ama adım kırmızı" | `CiBuild.ReportSuccess` çağrılıyor mu bak (E1); iş akışındaki koruma adımı bunu Unity'den önce denetler. Günlükte `Build succeeded!` (ünlemli) ara |
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
| Pages 404 | Pages kaynağı "GitHub Actions" değil ya da deploy işi atlandı | §1 adım 3. Deploy işi yalnız `has_scene == 'true'` ise, iş yeşilse ve "Verify WebGL output" geçtiyse (`player_verified == 'true'`) çalışır |
| İlk açılış çok yavaş | WebGL sıkıştırması kapalı; indirme büyük | Beklenen; §3 |

### Unity derlemesi başarılı ama adım kırmızı (validateBuild)

**Belirti** (koşu 41350760117, `a2679b0`): Unity `Build Finished, Result: Success.`, `ExitCode: 0` ve `Exiting batchmode successfully now!`
yazar; `CiBuild` `WebGL build succeeded: /github/workspace/build/WebGL/MinikUsta (35 MB)` yazar; konteyner `Build succeeded` (ünlemsiz) der ve
`build/WebGL/MinikUsta` klasörünü listeler. Hemen ardından koltuk iadesi gelir ve hata gibi görünür:

```
Returning personal license seat
An error occured while trying to return the ULF license. Ulf license file not found (/root/.local/share/unity3d/Unity/Unity_lic.ulf) (1404)
No ULF license file to return - this seat is entitlement-based; returning it through the editor instead.
[Licensing::Module] Successfully returned the entitlement license
[Licensing::Module] Error: Serial number unavailable for ULF return; skipping operation
```

Sonunda adım kırmızı biter, Pages adımları atlanır, hiçbir şey yayınlanmaz:

```
[ERROR] Error: There was an error building the project. Please read the logs for details. at validateBuild (/$bunfs/root/game-ci:14595:22)
##[error]Build failed with exit code 1
```

**Neden** (GameCI CLI v0.1.72 kaynağından doğrulandı):

- Koltuk iadesi satırları zararsız gürültüdür; kırmızıya neden olan onlar değildir. `entrypoint.sh` → `runsteps.sh` konteynerden `build.sh`'ın
  çıkış koduyla (`BUILD_EXIT_CODE`, burada 0) çıkar; iade bu kodu değiştirmez. `System.run` yalnız sıfır olmayan çıkış kodunda fırlatır.
- Kırmızı adım CLI'nin kendi denetimidir: `docker.ts` Unity derlemesinden sonra `UnityBuildValidation.validateBuild(dockerRun.output)` çağırır.
  Çıktıda `Build succeeded!` (ünlemli) yoksa `# Build results #` bloğunu ve içinde `Errors: 0` ile `Size:` satırını arar; ikisi de yoksa
  yukarıdaki hatayı fırlatır. Özel `buildMethod` bunları yazmıyordu; build.sh'ın `Build succeeded` satırı ünlemsiz olduğundan sayılmaz.
  Kalıcı düzeltme `CiBuild.ReportSuccess` (§2'deki `buildMethod` sözleşmesi); `f119d06` ile `main`'e girdi.
- v0.1.72 en yeni CLI etiketidir (2026-11-01'de `main`'de bu denetimi değiştiren bir sürüm yok), yani sürümü yükseltmek çözüm değildir.
  Kaynaklar: [unity-build-validation.ts](https://github.com/game-ci/cli/blob/v0.1.72/src/model/unity/build-validation/unity-build-validation.ts),
  [docker.ts](https://github.com/game-ci/cli/blob/v0.1.72/src/model/docker.ts),
  [runsteps.sh](https://github.com/game-ci/cli/blob/v0.1.72/dist/platforms/ubuntu/steps/runsteps.sh),
  [build.sh](https://github.com/game-ci/cli/blob/v0.1.72/dist/platforms/ubuntu/steps/build.sh).

**Koruma** (`unity-webgl.yml`): CLI'nin sonradan çıkardığı yanlış kırmızı, çalışan bir oyunun yayınını engellemesin diye başarı, diskteki
oyuncuya bakılarak belirlenir.

1. "Check CiBuild reports success to the GameCI CLI" adımı `Build succeeded!` çağrısının silinmediğini Unity açılmadan denetler.
2. Build adımı `continue-on-error: true`; kendi sonucu `steps.build.outcome`'da kalır.
3. "Verify WebGL output" adımı dosya listesini bayt boyutlarıyla yazdırır ve `index.html`, `Build/*.wasm*`, `Build/*.data*` (sıkıştırma kapalı;
   `.br` / `.gz` de kabul edilir) var ve boş değilse geçer. Build adımı kırmızıyken geçerse `::warning::` yazar, iş yeşil kalır. Eksikse
   `::error::Unity build produced no WebGL player (see the Build step log)` ile kırmızı biter.
4. Pages hazırlığı, Pages yüklemesi ve deploy işi yalnız Verify geçtiyse çalışır (`steps.verify.outputs.ok`, iş çıktısı `player_verified`).
   Unity'nin ürettiği dosyaların yüklenmesi (`unity-generated-files`) her durumda çalışır.

Gerçek derleme hatasında (`error CS…`, paket çözümleme) oyuncu oluşmaz, Verify kırmızı biter; hatanın nedeni yine **Build** adımının günlüğündedir.

Android: `.github/workflows/unity-android.yml` aynı korumayı taşır (§8): Build `continue-on-error: true`, `.apk`/`.aab` doğrulayan Verify, yükleme Verify'a bağlı,
aynı `unity-license` grubu. (P4B-13: eski «henüz yok» satırı kaldırıldı.)

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
  eski Input Manager (`activeInputHandler: 0`), WebGL sıkıştırma **Gzip + açma yedeği** (`webGLCompressionFormat: 1`, `webGLDecompressionFallback: 1`; P4B-04,
  önceden `2` = kapalı; Unity'de `WebGLCompressionFormat` = Brotli 0, Gzip 1, Disabled 2; UnityCsReference `webgl.cs` kaynağıyla doğrulandı), gamma renk uzayı.
  `CiBuild.BuildWebGL` aynı iki ayarı derleme anında da verir.
- **`.meta` dosyaları:** Repoda şu an yalnız `Level.unity.meta` izlenir. Unity diğer `.meta` dosyalarını her koşuda yeni GUID'le
  üretir (Library önbelleği isabetsizleşir). §4 ile depoya işlenir (karar: işlenir; GUID kararlılığı). İlk yeşil koşu beklenmez: 41350760117 artifact'i hazır.
- **Beklenen uyarı (görüldü):** `CiBuild.cs`, `#nullable` bağlamı olmadan `string?` kullanır; Unity derleyicisi bunu uyarı (CS8632,
  `CiBuild.cs(67,30)`) olarak verdi (41350760117, 2 kez). GameCI uyarıları hata saymaz; derleme durmadı.
- **WebGL sıkıştırması (P4B-04, 2026-11-02):** `CiBuild.BuildWebGL` **Gzip + `decompressionFallback = true`** üretir. GitHub Pages `Content-Encoding` başlığını
  bu dosyalar için vermez; açma yedeği açıkken Unity yükleyicisi dosyayı kendi JavaScript açıcısıyla açar (dosyalar `*.data.unityweb`, `*.wasm.unityweb`, `*.framework.js.unityweb`
  adını alır; `Verify` adımının `*.wasm*` / `*.data*` kalıpları hepsini kabul eder). Sıkıştırma kapalıyken (`Disabled`) koşu 41386489687'de `.data` 20,3 MB + `.wasm` 27,8 MB
  = **48,1 MB** indiriliyordu (3B sanat `.data`'yı 10,2 → 20,3 MB yaptı). Gzip `.wasm`'ı 3–4× küçültür; PNG'ler (`king_0` 5,3 MB, `atlas_0/1` 2,8 + 2,9 MB) zaten sıkışık olduğundan
  `.data` az küçülür. **İlk ölçüm** yeni koşunun `Verify` adımının günlüğünde ve iş özetinde ("WebGL download size"); sayı §9'a yazılır. Bütçe: `.data` + `.wasm` ≤ 25 MB
  (aşılırsa `::warning title=WebGL download size`). Bedeli: açma işlemi tarayıcıda JavaScript ile yapılır (masaüstünde ~1 sn, telefonda birkaç sn tahmini; **ölçülmedi**).
  Denenmedi (ayrı koşu ister): `Managed Stripping Level: High` (IL2CPP'de yansıma kullanan uGUI türlerini silebilir) ve WebGL "Code Optimization: Disk Size"
  (`UnityEditor.WebGL.UserBuildSettings.codeOptimization`; bu API UnityCsReference 6000.0'da yok, kapalı modülde, docs.unity3d.com bu ortamdan açılamadı: doğrulanmadan kullanılmadı).
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
  Unity WebGL derlemesi ikinci koşuda başarılı oldu (35 MB); CLI doğrulamasından geçen derleme ve Pages dağıtımı üçüncü koşuda görüldü (41352478361). Henüz görülmeyen: yayındaki oyunun tarayıcıda açılması.

## 4. Unity'nin ürettiği dosyaları depoya işle (P3-05)

Bulutta Unity açılamadığı için bazı dosyaları Unity ilk koşuda kendisi üretir: eksik `ProjectSettings/*.asset` dosyaları
(Graphics, Quality, Tag …), `ProjectSettings.asset`'in tam biçimi, `Packages/packages-lock.json` ve her varlığın `.meta` dosyası
(betik GUID'leri). Depoda yoklarsa her koşuda yeni GUID'le yeniden üretilirler: Library önbelleği isabetsizleşir ve ileride bir
varlık (prefab, ScriptableObject) bir betiğe GUID ile bağlanırsa her klonda kopar. İş akışı bu dosyaları her koşuda
**`unity-generated-files`** adlı artifact olarak yükler (derleme kırmızı olsa da; saklama 14 gün).

**Artifact hazır.** İlk yeşil koşu 41352478361'in `unity-generated-files` artifact'i (400 dosya, 131555 bayt; **2026-11-15'te silinir**) en yeni commit'ten geldiği için en az yetim `.meta` içerir.
Her koşu yeni bir artifact üretir (kırmızı olsa da); hangisi alınırsa alınsın aynı adımlar geçerlidir.

**Çok ajanlı depo kuralı:** bu depoda aynı anda başka ajanlar çalışır; çalışma ağacında başkalarının yarım işi ve hazırlanmış (staged)
değişiklikleri bulunabilir. Bu yüzden **`git add -A` kullanılmaz**, yalnız Unity'nin ürettiği yollar eklenir ve işlem ayrı bir çalışma ağacında yapılır.
Kirli ağaçta `git pull --rebase` zaten reddedilir; temiz ağaç hem bunu hem yanlışlıkla başkasının dosyasını commit'lemeyi önler.

1. Artifact'i indir (içinde `ProjectSettings/`, `Packages/` ve `Assets/` olmalı):
   ```
   gh run download 41352478361 -n unity-generated-files -D ../unity-generated
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

## 5. Paket V1 ile gelen dosyalar (code-lead, 2026-11-01)

- **Atlas sayfaları düz adlıdır:** `Resources/art/atlas_0.bytes … atlas_3.bytes`, `atlas.json` `file` alanı `art/atlas_N` (= `Resources.Load`
  yolu). Eski `atlas-N.png.bytes` adındaki ikinci nokta P3-13 şüphesi yüzünden kaldırıldı; run 41350760117 bu adlarla derlendi (`atlas_0.bytes`
  1,3 MB, `atlas_2.bytes` 1,1 MB yapı raporunda). Oyunun açılışta okuduğu: `art/atlas` (TextAsset) + sayfalar → `ImageConversion.LoadImage`.
- **Yazı tipi:** `Resources/fonts/Baloo2-ExtraBold.ttf` (OFL 1.1, `OFL.txt` yanında; `tools/font/build-font.sh` ile yeniden üretilir).
  Unity bu TTF'yi dinamik `Font` olarak içe aktarır; `UiRenderer` `Resources.Load<Font>("fonts/Baloo2-ExtraBold")` ile okur, bulamazsa
  `LegacyRuntime.ttf`'ye düşer ve Console'a uyarı yazar.
- **Kayıt:** `PlayerPrefs` (`minikusta.save`, `.bak`, `.corrupt`); WebGL'de IndexedDB. Her yazımdan sonra `PlayerPrefs.Save()`.

## 6. Referans derleme denetimi (`tools/unity-refcheck.sh`, paket V1 madde 8)

Amaç: Unity katmanını stub'lara ek olarak Unity'nin **gerçek** referans derlemelerine karşı da derlemek (NuGet `UnityEngine.Modules`, `artifacts/`
altına, git dışı). **Sonuç: çalışmıyor, kapıya eklenmedi.** Neden:

1. nuget.org'da `UnityEngine.Modules` paketinin tek sürümü **2021.3.33**'tür (`api.nuget.org/v3-flatcontainer/unityengine.modules/index.json`,
   2026-11-01); 6000.0.x yok. 2021.3 başka bir API yüzeyidir (ör. `ImageConversion.LoadImage(Texture2D, ReadOnlySpan<byte>, bool)` 6000.0'da
   var, 2021.3'te yok): ona karşı yeşil derleme 6000.0 için kanıt değildir, kırmızı derleme de yanlış alarm olabilir.
2. Paket uGUI'yi (`UnityEngine.UI`) içermez; needle-mirror/com.unity.ugui kaynağından ayrıca derlemek gerekir ve uGUI kaynağı motorun iç
   (internal) API'lerine dayanır.
3. Asıl kanıt artık var: run 41350760117'den beri her `main` itişinde GameCI **Unity 6000.0.85f1** ile gerçek derleme yapıyor (o koşuda
   derleme ve WebGL yapısı başarılı oldu; yalnız CLI doğrulaması kırmızıydı, §2b). Stub kapısı bulutta hızlı ön denetim olarak kalır.

`tools/unity-refcheck.sh` her çalıştığında nuget.org'u yeniden sorar: 6000.0.x yoksa `SKIPPED` yazıp 0 ile çıkar; bir gün çıkarsa 2 ile çıkar ve
derleme adımının buraya bağlanmasını ister. Yeni API imzaları yerel UnityCsReference 6000.0.85f1 kaynağıyla (etiket `6000.0.85f1`) ve uGUI
kaynağıyla doğrulandı; liste `docs/PORT_MAP.md` "Paket V1" bölümündedir.

## 7. Ekran görüntüleri (`screenshots` işi, paket C1)

Her `main` itişinde `unity-webgl.yml`, Pages'e giden **aynı oyuncuyu** başsız Chromium'da oynatır ve ekranlarını `ci-shots` dalına yazar. İnceleme
yapanlar Unity kurmadan oyunun gerçek görüntüsünü buradan okur.

**Akış.** `build` işi Verify'dan sonra oyuncuyu `webgl-player` artifact'i (3 gün) olarak yükler. `screenshots` işi (`needs: build`, `continue-on-error: true`,
Unity koltuğu kullanmaz, Pages dağıtımını beklemez) onu indirir, `tools/ci-shots` içindeki **Playwright 1.64.1**'i (`package-lock.json` ile sabit; npm'de
2026-11-01'de doğrulandı) ve Chromium'u kurar, `tools/ci-shots/shoot.mjs` ile çeker, `tools/ci-shots/publish.sh` ile dala yazar. Kurulum ya da çekim
başarısızsa iş yine yeşil biter ve `::warning title=CI screenshots::…` ek açıklaması verir; yayını hiçbir koşulda engellemez.

**Otomatik sürücü (`?shots=1`).** Sayfa adresine `?shots=1` eklenince (isteğe bağlı `&shotsHold=2500`, oyun ms'si) `GameRunner`,
`Presentation.Core/Shots/ShotScenario` çalıştırır (`Application.absoluteURL`; WebGL dışında adres boştur, sürücü hiç açılmaz). Sürücü **bellekte kayıt**
kullanır (`MemorySaveStore`), yani bu parametreli bir ziyaret gerçek kaydı hiç değiştiremez. Senaryo, parmağın bitirdiği çağrılarla oyunu oynar ve
her anda `Debug.Log("[minikShot] shot:<ad>")` yazar (tarayıcı konsolu; jslib yok, derleme riski yok). Playwright bu satırı `page.on('console')` ile
bekler ve satırdan 600 ms sonra `page.screenshot` alır; sürücü her işaretten sonra `HoldMs` (2500 oyun ms'si) hiçbir şeye dokunmaz, böylece yazılımsal GL'nin
saniyede birkaç karelik hızında bile istenen kare ekrandadır.

| Sıra | Ad | An |
| --- | --- | --- |
| 1 | `home` | ana ekran, açılıştan 2,5 sn sonra |
| 2 | `settings` | ayarlar paneli açık |
| 3 | `level_start` | Bölüm 1, başlangıçtan 0,5 sn sonra (öğretici henüz yok) |
| 4 | `tutorial` | başlangıçtan 3 sn sonra: parlama, balon, eldiven yolda |
| 5 | `drag_mid` | gerçek işaretçi sürüklemesi: altın çözümün şantiyeye giden ilk hamlesindeki blok (öncesindeki saha hamleleri oynanır) yolun %80'inde, duvarın üstünde, K-18 gölgesiyle; bırakılmaz, iptal edilir |
| 6 | `landed` | o hamle işlendi, blok yerine oturmuş ve sahne durulmuş |
| 7 | `win` | kazanma penceresi, ödüller yerleşmiş |

Sonra `done` yazılır. Hata olursa `error:<neden>` yazılır (zaman aşımı 240 sn oyun süresi) ve Playwright ilgili görünümde durur. Sıra ve adlar
`tools/ci-shots/shots.json` ile `ShotScenario.ShotNames` arasında ortaktır; `C1_ci_shots_json_lists_the_same_shots_and_tag_as_the_scenario` testi ikisini eşitler.

**FIT / EXPAND kanıtı (`expand/`).** Sayfa adresine `?scale=expand` (ya da `?scale=fit`) eklenince o ziyaret için ölçek kipi seçilir (`ScreenFit.ModeFromUrl`; varsayılan
`LayoutSettings.Mode` = FIT, Faz 3 brifi). `screenshots` işi aynı oyuncuyu bir kez de `?scale=expand` ile açar ve `level_start`, `tutorial`, `win` karelerini
`latest/expand/` (ve `<kısa-sha>/expand/`) altına yazar (kendi `manifest.json`'u var, `query: "scale=expand"`). Neden: FIT, 9:16'dan uzun her telefonda (390×844'te %18, 360×800'de %20)
üst ve altta ölü koyu bant bırakır; web D-015 EXPAND'i kabul etmişti. Karar orkestratörün; geçiş tek satır (`LayoutSettings.Mode`).

**Görünümler ve hâl.** 390×844 ve 360×800 CSS px, `deviceScaleFactor` 2 (PNG 780×1688 ve 720×1600), Android kullanıcı aracısı + `isMobile` (varsayılan Unity
şablonu bu durumda tüm pencereyi dolduran `unity-mobile` düzenine geçer), `tr-TR`, `--use-angle=swiftshader --enable-unsafe-swiftshader`. Her PNG, Playwright'ın
yazdığı 8 bit PNG'yi çözen küçük bir denetimden geçer: tek renkli (canvas yakalanmamış) görüntü `blank: true` ve uyarı olur.

**`ci-shots` dalının biçimi.** Dal **tek bir yetim commit**'tir ve her koşuda `--force` ile itilir (depo büyümez; son 8 koşunun klasörü korunur):

```
latest/manifest.json              kaynak commit, zaman, görünüm başına ekran listesi ve hatalar
latest/<görünüm>/<ad>.png         390x844/home.png … 360x800/win.png
latest/console-<görünüm>.log      tarayıcı konsolunun son 600 satırı
<kısa-sha>/…                      aynı dosyalar, koşu başına bir klasör (kısa sha = commit'in ilk 7 hanesi)
index.json                        [{shortSha, commit, generatedAt, ok, run}, …] yeniden eskiye
README.md                         dalın açıklaması
```

`manifest.json` alanları: `schema` (1), `commit`, `shortSha`, `generatedAt` (ISO), `run` (Actions koşusu adresi), `renderer`, `dpr`, `shots` (adlar),
`viewports.<ad>` = `{width, height, dpr, canvas{cssWidth, cssHeight, pixelWidth, pixelHeight}, gl, ready, screens[{name, file, bytes, colours, blank}], errors[], ok}`,
`ok` (hepsi tamam), `warnings[]`. Okumak: `bash tools/ci-shots/fetch.sh [hedef] [kısa-sha|latest]` (dalı çeker, klasörü `artifacts/ci-shots` altına açar, özet yazar) ya da tek dosya için
`git fetch origin ci-shots && git show origin/ci-shots:latest/390x844/win.png > win.png`.
Aynı dosyalar `ci-screenshots` artifact'i (14 gün) olarak da yüklenir.

**Yerel deneme.** `cd tools/ci-shots && npm ci && PLAYWRIGHT_CHROMIUM_EXECUTABLE=/yol/chrome node shoot.mjs --dir <WebGL klasörü> --out <çıktı> --sha <sha>`;
`publish.sh` yerel bir `file://` uzağa karşı denendi (`REPO_URL=file:///…/bare.git SHOTS_DIR=… SHA=… bash publish.sh`).

**Koşular.**

| Koşu | Commit | Sonuç |
| --- | --- | --- |
| [41361422803](https://github.com/yilmazfatih-cyber/minik-usta-unity/actions/runs/41361422803) | `58e1700` | İlk deneme: Unity derlemesi, dağıtım ve ekran görüntüsü işi yeşil (derleme ≈ 8 dk, görüntü işi ≈ 2 dk); 2 görünüm × 6 ekran, hepsi renkli (160–430 renk), `ok: true`. Görüntüler **gerçek bir hatayı** gösterdi: `CanvasScaler` `Shrink` kipi (`max(w/1080, h/1920)`) 9:16'dan dar her ekranda (390×844 dahil) uGUI tuvalini iki yandan kırpıyordu (HUD kenarları, ana sayfanın ayar düğmesi, 4. güçlendirici, ayarlar penceresinin kapat düğmesi). Önceki inceleme (`REVIEW_PHASE3.md` Q5 g) "Shrink ile kamera aynı ölçeği verir" demişti; yanlıştı. |
| [41362863833](https://github.com/yilmazfatih-cyber/minik-usta-unity/actions/runs/41362863833) | `338d2cb` | Düzeltme (`ConstantPixelSize` + `DesignViewport.Scale`) işledi: tuval tam görünüyor. Öğretici balonu, eldiven, iz noktaları ve vurgu ilk kez ekranda (`tutorial.png`). FIT, kırpma gidince 9:16'dan uzun ekranda üst ve altta ölü bant gösterdi (aşağıda). Konsol: `Audio context resumed after 0.652 seconds.`, uyarı yok. |
| [41364199994](https://github.com/yilmazfatih-cyber/minik-usta-unity/actions/runs/41364199994) | `626257a` | Yuvarlak vurgu çerçevesi (`tut_highlight`), `expand/` kareleri: EXPAND'de ekran tam doluyor (390×844: tasarım 1080 × 2337). `drag_mid`'de blok kırmızı "yanlış yer" gölgesi gösterdi (yolun %80'inde, altın düğümde değil); sonraki commit parmağı altın düğüme tam götürür ve yeşil ✓ gölgeyi testle (`ShadowBadge.Ok`) doğrular. |

Süre: iş akışı toplamı ≈ 10 dk (Unity 6 dk 20 sn); ekran görüntüsü işi Pages dağıtımına paralel ≈ 2 dk 30 sn ve sonraki Unity derlemesini bekletmez.

## 8. Android (`unity-android.yml`, paket C1)

İş akışı **Unity Android → APK**: `workflow_dispatch` (Actions → Run workflow; `export_type` = `androidPackage` APK ya da `androidAppBundle` AAB) ve `v*` etiketleri.
Her itişte **çalışmaz**: IL2CPP Android derlemesi WebGL'den çok uzundur ve Unity koltuğunu tutar. Aynı hesap (`UNITY_EMAIL` / `UNITY_PASSWORD`), aynı sabit CLI
(`game-ci/unity-builder@v6.0.0`, `cliVersion: v0.1.72`), aynı `unity-license` koltuk grubu (`build` işinde).

- **Derleme:** `targetPlatform: Android`, `buildMethod: MinikUsta.Editor.CiBuild.BuildAndroid`, imaj `unityci/editor:ubuntu-6000.0.85f1-android-3` (sürüm
  `ProjectVersion.txt`'ten). `CiBuild.BuildAndroid`: dikey yön, IL2CPP + ARM64 + ARMv7 (Mono ARM64 üretemez), uygulama kimliği `com.tunaco.minikusta` (şirket adı "Tuna & Co."
  geçerli bir paket adı vermez), sürüm kodu `-androidVersionCode` (= `github.run_number`), sürüm adı `-buildVersion` (etiketten `v` çıkmış hâli, değilse `0.0.<koşu>`),
  çıktı yolu `-customBuildPath` (`build/Android/MinikUsta.apk`; GameCI uzantıyı `androidExportType`'tan koyar). Özel `buildMethod` kullanıldığı için GameCI'nin kendi Android
  ayarları uygulanmaz; imza ve sürüm bağımsız değişkenlerini `CiBuild` okur. Başarıda `ReportSuccess` "Build succeeded!" yazar (§2b); iki iş akışındaki koruma adımı
  `BuildAndroid`'in de çağırdığını denetler.
- **İmza:** dört gizli değer varsa sürüm imzası, yoksa Unity'nin hata ayıklama anahtar deposu (yan yükleme için yeterli; mağaza reddeder).
  `ANDROID_KEYSTORE_BASE64` (`base64 -w0 release.keystore`), `ANDROID_KEYSTORE_PASS`, `ANDROID_KEYALIAS_NAME`, `ANDROID_KEYALIAS_PASS` (Settings → Secrets → Actions).
  Anahtar deposunun adı `minikusta.keystore`dır; CLI base64'ü proje köküne yazar (`setup-android.ts`), `CiBuild` onu `PlayerSettings.Android.keystoreName` yapar. Değerler günlüğe yazılmaz.
- **Doğrulama (WebGL'deki Verify deseni):** `Build` adımı `continue-on-error`; başarıyı diskteki paket belirler: boş olmayan `.apk`/`.aab`, geçerli zip, `AndroidManifest.xml`,
  `classes.dex`, `lib/arm64-v8a/libunity.so`. Paket yoksa ya da eksikse iş kırmızı biter; GameCI'nin yanlış kırmızısı sağlam paketi gizlemez.
- **Çıktı:** `minikusta-android-<koşu>` artifact'i (30 gün). Sürüm imzası yoksa APK hata ayıklama imzalıdır.
- **Disk:** Android imajı yaklaşık 9 GB'dır; iş, koşucudaki kullanılmayan büyük dizinleri (`/usr/share/dotnet`, `/usr/local/lib/android`, `/opt/ghc`, …) siler. Library önbelleği bilerek yoktur
  (IL2CPP Android Library'si birkaç GB'dır ve WebGL önbelleğini 10 GB sınırından dışarı iterdi).
- **İlk gerçek koşu (P4B-06, 2026-11-02, `workflow_dispatch`, koşu 41394679602, `264f07d`):** **başarılı**. Koltuk grubu yüzünden WebGL koşusunun (41394615409) bitmesini bekledi (≈ 5 dk kuyruk);
  iş 12 dk 30 sn: imaj çekme 1 dk 50 sn, disk temizliği 2 dk 20 sn, Unity derlemesi **7 dk 26 sn** (IL2CPP ARM64 + ARMv7), "Warnings: 0 / Errors: 0", "Build succeeded!". Çıktı `MinikUsta.apk` **46 262 569 B (44,1 MB)**,
  artifact `minikusta-android-1` 45 485 807 B (30 gün); Verify (zip, `AndroidManifest.xml`, `classes.dex`, `lib/arm64-v8a/libunity.so`) geçti. İmza: Unity hata ayıklama anahtar deposu (gizli değerler yok). Günlük: `Android: id
  com.tunaco.minikusta, version 0.0.1 (1), IL2CPP ARM64+ARMv7, portrait`; Android SDK `build-tools/36.0.0`, `cmdline-tools/16.0`; GameCI'nin "auto detected target API version" adımı `platforms;android-` (boş sürüm) için uyarı verdi ve
  Unity kendi paketli SDK'sını kullandı. **Hedef API düzeyi bu koşuda okunamadı** (artifact indirmesi vekilde 403). İlk düzeltme denemesi (`androidTargetSdkVersion: AndroidApiLevel35`, koşu 41397122458, 14 dk 17 sn, derleme yeşil) **işe yaramadı**: GameCI CLI girdiyi sayıya çevirir, Unity'ye
  `-androidTargetSdkVersion NaN` gitti ve günlük `minSdk AndroidApiLevel23, targetSdk AndroidApiLevelAuto` yazdı (yeni günlük satırı sayesinde görüldü). Şimdi iş akışı `androidTargetSdkVersion: 35` verir;
  `CiBuild.ConfigureAndroid` bağımsız değişkeni önce tamsayı (enum değerleri API düzeyidir), sonra üye adı olarak okuyup `PlayerSettings.Android.targetSdkVersion` yapar (`AndroidSdkVersions.AndroidApiLevel35`
  UnityCsReference 6000.0'da var) ve günlüğe `minSdk` / `targetSdk` yazar; Google Play 2026'da en az 35 ister. **APK bir telefonda açılmadı** (bu ortamda cihaz yok): sonraki adım.
- **P4B-13 notu:** `unity-license` grubunda tek bekleyen iş tutulur; Android işi bekliyorsa araya yeni bir WebGL itişi girerse **bekleyen Android iptal olur**. Android'i itiş yokken başlatın (ya da bir WebGL işi *çalışırken* tek başına bekletin).
- **Eski not (ilk koşudan önce yazıldı):** bu ortamda Unity ve Android imajı yoktu. Kullanılan `PlayerSettings` üyeleri UnityCsReference 6000.0 ile doğrulandı
  (`SetApplicationIdentifier(NamedBuildTarget, string)`, `SetScriptingBackend(NamedBuildTarget, ScriptingImplementation)`, `Android.targetArchitectures`, `Android.bundleVersionCode`,
  `Android.useCustomKeystore`, `keystoreName`, `keystorePass`, `keyaliasName`, `keyaliasPass`, `defaultInterfaceOrientation`).

## 9. İndirme boyutu ve 3B sayfa çözme bütçesi (paket F1, P4B-04 / P4B-05 / P4B-08)

- **İndirme bütçesi:** `Build/*.data*` + `Build/*.wasm*` ≤ **25 MB** (sıkıştırılmış, diskteki dosyalar tarayıcının indirdiği dosyalardır). `Verify WebGL output` adımı
  boyutu günlüğe ve iş özetine yazar ("WebGL download size"), aşılırsa `::warning title=WebGL download size`. FTUE çıtası: ilk açılıştan Bölüm 1'e ≤ 10 sn
  (20 Mbit/s bağlantıda 25 MB ≈ 10 sn, ek JS açma süresi ayrı). Ölçümler aşağıda **Ölçümler** tablosundadır.
- **3B sayfa çözme bütçesi:** `[minik] art3d decode …` satırlarının toplamı ≤ **250 ms** (CI'nin masaüstü sınıfı CPU'su; telefon birkaç kat yavaştır) ve **hiçbir çözme
  oyun sırasında olmamalı** (`tutorial` çekiminden sonra). `tools/ci-shots/shoot.mjs` her görünümün `manifest.json` kaydına `art3d { decodes, totalMs, budgetMs, lateDecodes }`
  yazar, bütçe aşılırsa ya da oyun sırasında çözme olursa `::warning` verir (yayını engellemez). `ArtWarmup`: kral sayfaları `Awake`'te, fx sayfaları ana ekrandan çıkınca ya da
  2,5 sn sakin ana ekrandan sonra.
- **Hareket kanıtı (P4B-08):** `?shots=1&motion=0.0125&motionMs=1800` (`shoot.mjs` `shots.json`'dan her zaman ekler; `--no-motion` ile kapanır) iki pencereyi yavaş çekimde oynatır: `king_wave` (ana ekran, açılıştan
  sonraki ilk 1,8 oyun sn'si, `home` çekiminden önce) ve `king_big_cheer` (kazanma penceresi açılınca, `win` çekiminden önce). İlk koşu (`264f07d`) `motion=0.1` ile yapıldı: CI'nin yazılımsal GL'sinde bir
  resim ≈ 1,2 sn sürüyor ve oyun kare başına 10 ms ilerliyordu, yani resimler arası **126 ms oyun süresi** (`undersampled`); 60 kare/sn çözünürlüğü (≤ 16,7 ms) için ölçek 0,0125'e indi
  (resim başına ≈ 15 ms; pencere başına ≈ 2,5 dk gerçek süre; sahne zaman aşımı pencereleri saymaz). `shots.json` `sequences[].focus` (resim kesirleri) değişim sayımını ve şeridin kırpmasını kralın bulunduğu bölgeye
  sınırlar (kenar paralaksı ve arayüz de hareket eder). Pencerede oyun gerçek zamanın `motionScale` katıyla ilerler
  (`ShotScenario.TimeScale`; `GameRunner` uygulamanın kare süresini bununla çarpar) ve her kare `seq:<ad>:t=<oyun ms>` yazar. Playwright begin–end arasında sayfayı olabildiğince sık
  fotoğraflar (JPEG, CSS pikseli), her resmi son `t` ile etiketler ve tarayıcıda çözümler: bir önceki resimden farklı olan çiftler (≥ 24 piksel, kanal toplamı farkı > 48), benzersiz resim
  sayısı, oyun saniyesi başına değişim sayısı, hareket kutusu ve 24 resimlik şerit PNG (`<görünüm>/seq_<ad>_strip.png`, `ci-shots` dalında). `undersampled: true` ise resimler arası
  ortalama oyun süresi 16,7 ms'den uzundur: değişim sayısı alt sınırdır. **Bu sayılar hareketin akıcılığını ölçer, "Royal Match gibi" olduğunu kanıtlamaz**: şeridi design-lead rubrikle puanlar.
  Tasarım gereği 2,5B katman (nefes, ezilme, pop) klip çizimlerinin üstünde sürekli hareket eder; klip çizim sayısı `king.json`'dadır (`king_idle` 48 kare / 9 çizim …, P4B-08).
- **Ayrı bir CI denemesi ister, yapılmadı:** `Managed Stripping Level: High`, WebGL "Code Optimization: Disk Size" (API doğrulanamadı, §3 sıkıştırma maddesi), `king_0` render
  ölçeğinin %70'e çekilmesi (design-lead; sayfa ≈ yarıya iner), ASTC/ETC2 içe aktarılmış dokular (P3-11).

### P4B-08 sonucu: kralın hareketi (koşu `0b85e7f`, CI Chromium, ölçek 0,0125, 1 790 oyun ms'si)

| Görünüm | Pencere | Resim | Benzersiz (JPEG) | Değişen çift | Oyun sn'si başına değişim | Medyan aralık |
| --- | --- | --- | --- | --- | --- | --- |
| 390×844 | `king_wave` (ana ekran) | 119 | 92 | 25 (%21) | **14,0** | 15 ms (60/sn çözünürlük) |
| 390×844 | `king_big_cheer` (kazanma) | 77 | 34 | 24 (%32) | **13,4** | 24 ms (az örneklenmiş) |
| 360×800 | `king_wave` | 136 | 97 | 25 (%19) | **13,9** | 13 ms |
| 360×800 | `king_big_cheer` | 87 | 37 | 25 (%29) | **13,9** | 21 ms |

Okuma: kral bölgesinde (odak dikdörtgeni) saniyede ≈ **14 görünür değişiklik** var, 60 örnek/sn çözünürlükte ardışık çiftlerin yalnız %19–32'si farklı. Yani ekrandaki hareket **≈ 14 Hz basamaklıdır**,
sürekli 60 fps iskelet hareketi değildir (klip çizim sayısıyla uyumlu: `king_wave` 24 karede 8 çizim). 2,5B katmanın (nefes, ezilme) küçük kaymaları eşik altında (≥ 24 piksel, kanal toplamı farkı > 48) kalıyor olabilir:
sayı bir **alt sınırdır** ve çıplak gözle görünen akıcılığı yerine geçmez. Şeritler (`ci-shots/latest/<görünüm>/seq_king_wave_strip.png`, `seq_king_big_cheer_strip.png`) ayrı bir karardır: design-lead rubrikle puanlar;
puanı < 4/5 ise yol: Blender'dan baş / gövde / kol / baret ayrı render + mevcut tween motoru (kesme rig'i) ya da klip çizim sayısını artırmak (+≈ 15 MB, önerilmez). **Bu pakette kesme rig'i yapılmadı.**

### Ölçümler

| Koşu | `.data` | `.wasm` | Toplam | Not |
| --- | --- | --- | --- | --- |
| 41386489687 (`e4be42e`, sıkıştırma kapalı) | 20 323 050 B | 27 790 131 B | 48,1 MB | yük: P4B-04 kanıtı |
| 41394615409 (`264f07d`, **Gzip + açma yedeği**) | 15 721 315 B (`.data.unityweb`) | 6 816 590 B (`.wasm.unityweb`) | **22 537 905 B = 21,5 MB** (−%53) | `framework.js.unityweb` 81 678 B, `loader.js` 47 339 B; "Complete build size" 21,7 MB (46,4 idi); Pages dağıtımı ve 7/7 ekran görüntüsü iki görünümde de yeşil: yükleyicinin JS açıcısı çalışıyor |

3B sayfa çözme (CI Chromium, yazılımsal GL, `264f07d`): 390×844 `king_0` 133 ms + `king_1` 4 + `fx_1` 19 + `fx_0` 79 = **235 ms**; 360×800 132 + 4 + 18 + 40 = **194 ms** (bütçe 250); **hepsi `home` çekiminden önce**,
oyun sırasında çözme **yok** (`lateDecodes: []`). Önceki koşuda `fx_0` (47 ms) `drag_mid` ile `landed` arasındaydı (P4B-05 kanıtı).
