# Bulut derlemesi (GitHub Actions + GameCI) — Unity kurmadan oynamak

Unity Editor kurmadan oyunu denemenin yolu: GitHub, projeyi kendi sunucusunda derler ve WebGL sürümünü GitHub Pages'e koyar.
İş akışı: `.github/workflows/unity-webgl.yml` (derleme + yayın; adı **Unity WebGL → Pages**). Lisans için ayrı bir iş akışı
ya da dosya yoktur (§1: yalnız iki secret; `unity-activation.yml` P3-03 ile kaldırıldı). Unity sürümü `ProjectSettings/ProjectVersion.txt` ile sabit:
**6000.0.85f1**; iş akışı sürümü oradan okur, imaj `unityci/editor:ubuntu-6000.0.85f1-webgl-3` Docker Hub'da var (2026-11-01'de denetlendi).

Yayın adresi (ilk başarılı derlemeden sonra açılır): https://yilmazfatih-cyber.github.io/minik-usta-unity/

**Durum (Faz 3 Q3):** Bu ortamda Unity hiç derlenmedi. Gerçek derlemenin ilk kanıtı, §1 tamamlandıktan sonraki ilk Actions
koşusudur; Q1 kodu (`b4efe18` ve sonrası) bu koşuda ilk kez derlenecek.

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
[lisans seçimi ve hata açıklamaları](https://github.com/game-ci/cli/blob/v0.1.72/dist/platforms/ubuntu/steps/licensing_method.sh).

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
- Süre: ilk derleme 20–40 dk (imaj + Library önbelleği), sonrakiler 10–20 dk. Bu süreler tahmindir, ölçülmedi.
- Çıktı: https://yilmazfatih-cyber.github.io/minik-usta-unity/ (web sürümünden ayrı adres).

## 2a. İlk koşuda beklenen sonuç

Actions → **Unity WebGL → Pages** → **Run workflow**. İş: **Build WebGL (GameCI)**. Beklenen adımlar:

| Adım | Beklenen | Başarısızlıkta |
| --- | --- | --- |
| Check for a scene | `has=true`; `::notice::` mesajı yok | Sahne dosyası yok ya da yol farklı |
| Check Unity account secrets | Sessizce geçer (değerler yazdırılmaz) | `::error::UNITY_EMAIL / UNITY_PASSWORD repository secrets are not set`: §1 adım 2. Sonraki Unity adımları atlanır |
| Cache Library | İlk koşuda önbellek boş; normal | — |
| Build | CLI indirilir (`Installing game-ci CLI v0.1.72`), imaj çekilir, günlükte `Requesting activation (personal license via Unity account)`, Unity açılır, `CiBuild.BuildWebGL` çalışır. Başarıda `WebGL build succeeded: build/WebGL/MinikUsta (N MB)`; sonunda koltuk iade edilir | Lisans: §2b tablosu. Derleme: `error CS…`. Betik: `WebGL build failed: …` |
| Collect / Upload files Unity generated | Koşu sayfasının **Artifacts** bölümünde `unity-generated-files` (§4); derleme kırmızı olsa da yüklenir | Adım atlanırsa sahne yok (`has=false`) ya da Build adımı hiç başlamadı (secret denetimi kırmızı) |
| Prepare Pages artifact | `ls` çıktısında `index.html`, `Build/` (loader, framework, data, wasm dosyaları) ve `TemplateData/`. Sıkıştırma kapalı olduğundan `.br` / `.gz` dosyası olmaz | Klasör yoksa çıktı yolu farklı (§2b) |
| Upload Pages artifact, Deploy to GitHub Pages | Deploy işinin `page_url` çıktısındaki adres açılır | 404: Pages kaynağı "GitHub Actions" değil (§1 adım 4) |

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
| `UNITY_EMAIL / UNITY_PASSWORD repository secrets are not set` | İki secret'tan biri yok (ya da Environment/Organization secret'ı olarak girilmiş) | §1 adım 2; **Repository secrets** altında |
| `Unity rejected the account credentials, or wants a second factor` | Yanlış e-posta/şifre, sosyal giriş hesabı, 2FA açık ya da yeni IP için doğrulama e-postası bekliyor | §1 adım 1; e-postayı onayla, **Re-run jobs** |
| `Unity reports no available Personal seats for this account` | Önceki iş koltuğu iade etmeden bitti (iptal, zaman aşımı) ya da hesap başka işte kullanımda | §1 "Koltuk": id.unity.com'da takılı etkinleştirmeyi serbest bırak, yeniden başlat |
| `Unity did not grant a Personal seat for this editor version` | Unity koltuk vermedi (CLI kaynağı geçici olabildiğini söylüyor) | Koşuyu yeniden başlat; sürerse günlükle code-lead'e |
| `error CS0234` / `CS0246` / `CS0103` / `CS1061` (`Assets/MinikUsta/Unity/…`) | Unity API imzası ya da asmdef referansı | Hata metnindeki ad `UnityEngine.UI` ise `MinikUsta.Unity.asmdef` referansına bak (§3). Ad `Handheld` gibi yalnız mobil hedefte var olan bir tür ise `#if UNITY_ANDROID || UNITY_IOS` ile koru; stub'da da aynı koşul olmalı (§3). Başka bir API ise `dotnet/UnityStubs/UnityEngine.cs` imzası gerçek Unity imzasıyla düzeltilir, sonra Unity katmanı. Kod değişikliği code-lead ile |
| `error CS…` (`Assets/MinikUsta/Core/…` ya da `Presentation.Core/…`) | Unity derleyicisi dotnet'ten farklı davranıyor (dil sürümü ya da tanımlar) | Önce `cd dotnet && dotnet build -warnaserror && dotnet test` ile yeniden üret; sürerse ilk hata satırıyla code-lead'e |
| Oyun açılıyor ama hiçbir uGUI düğmesi çalışmıyor (Mola, Devam); konsolda her karede `Input Button Submit is not setup` | `ProjectSettings/InputManager.asset` yok ya da eksenleri eksik (P3-05) | Depodaki `InputManager.asset` Unity 6000.0'ın varsayılanıdır; yoksa §4'teki artifact'ten al. Günlükte `is not setup` ara |
| `WebGL build failed: <sonuç> (N errors)` | Build pipeline hatası (`CiBuild.cs`) | Günlükteki ilk `error` satırına bak; sahne yolu ve GUID §2 ile karşılaştırılır |
| `Prepare Pages artifact` içinde `build/WebGL/MinikUsta` yok | GameCI çıktı yolu farklı | "Build" adımındaki `WebGL build succeeded: …` satırındaki yolu al; `CiBuild.cs` varsayılanı ile iş akışındaki yol aynı olmalı |
| Yayın açılıyor ama ekran boş ya da siyah | Sahne açılmadı (aktif sahne adı `Level` değil) ya da bölüm yüklenemedi | Tarayıcı konsolunda `Minik Usta: …` varsa bölüm dosyası; yoksa sahne adı ve §2 |
| Pages 404 | Pages kaynağı "GitHub Actions" değil ya da deploy işi atlandı | §1 adım 4. Deploy işi yalnız `has_scene == 'true'` ise çalışır |
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
- **uGUI asmdef adı (açık risk):** `Assets/MinikUsta/Unity/MinikUsta.Unity.asmdef` `UnityEngine.UI` adıyla referans veriyor. Unity'nin
  uGUI paketinin asmdef adı bu ortamda doğrulanamadı (Unity dokümantasyonuna erişim yok). Hata çıkarsa aday adlar `Unity.ugui` ve
  `Unity.UI`. Kesin cevap, Unity Editor ilk açılışta indirdiği `Library/PackageCache/com.unity.ugui@*/` altındaki `.asmdef`
  dosyasının adıdır.
- **`ProjectSettings.asset`:** `companyName` "Tuna & Co.", `productName` "Minik Usta", portre (`defaultScreenOrientation: 0`),
  eski Input Manager (`activeInputHandler: 0`), WebGL sıkıştırma **kapalı** (`webGLCompressionFormat: 2`; Unity'de
  `WebGLCompressionFormat` = Brotli 0, Gzip 1, Disabled 2; UnityCsReference `webgl.cs` kaynağıyla doğrulandı), gamma renk uzayı.
  `CiBuild.BuildWebGL` sıkıştırmayı zaten derleme anında kapatır.
- **`.meta` dosyaları:** Repoda şu an yalnız `Level.unity.meta` izlenir. Unity diğer `.meta` dosyalarını her koşuda yeni GUID'le
  üretir (Library önbelleği isabetsizleşir). İlk yeşil koşudan sonra §4 ile depoya işlenir (karar: işlenir; GUID kararlılığı).
- **Beklenen uyarı:** `CiBuild.cs`, `#nullable` bağlamı olmadan `string?` kullanır; Unity derleyicisi bunu uyarı (CS8632) olarak
  verebilir. GameCI uyarıları hata saymaz; derleme durmaz.
- WebGL derlemesi sıkıştırmasız üretilir (`CiBuild.BuildWebGL`); GitHub Pages Brotli/Gzip başlıklarını vermediği için zorunlu.
  İndirme boyutu bu yüzden büyüktür (tahmini ≈ 30–60 MB); telefon tarayıcısında ilk açılış yavaş olabilir.
- Telefonda gerçek his için Android APK daha doğrudur: `CiBuild.BuildAndroid` hazır; ayrı bir iş akışı istendiğinde eklenir
  (imza anahtarı gerekmez, debug APK "bilinmeyen kaynak" izniyle kurulur).
- Lisans dosyası olmadığı için süresi dolan bir şey yoktur; Unity şifresi değişirse `UNITY_PASSWORD` secret'ı güncellenir.
- **unity-builder v6 notları (v4'ten geçiş):** Eylem artık GameCI CLI'yi indirip çalıştırır; `cliVersion` **v0.1.72**'ye sabit
  (varsayılan `latest` idi; yükseltme bilerek yapılır). `unityVersion` girdisi verilmez: sürüm `ProjectVersion.txt`'ten okunur
  (v6.0.0 `action.yml` açıklaması "auto dışı değer yok sayılır" der, ama kaynak kodu `--engineVersion` olarak geçirir; belge bayat).
  `UNITY_LICENSE` geçirilmez. Çıktı yolu değişmedi: `buildsPath` varsayılanı `build` + `WebGL` + `buildName` = `build/WebGL/MinikUsta`
  (`-customBuildPath` CLI'nin `build.sh` betiğinden aynı biçimde gelir). `versioning` varsayılan (`Semantic`) kaldı: CLI sığ klonu
  kendisi derinleştirir (`git fetch --unshallow`), etiket yoksa sürüm commit sayısından üretilir; `allowDirtyBuild: true` bunun için gerekli.
- **Paketler (P3-04):** `Packages/manifest.json` yalnız kullanılanı içerir: `com.unity.ugui`, `com.unity.test-framework` ve yerleşik
  modüller. URP ve Cinemachine çıkarıldı (D-096'ya göre kullanılmıyorlar; her koşuda içe aktarılıp derleniyorlardı, paket içi bir hata
  derlememizi düşürebilirdi). Yerleşik `particlesystem` ve `physics2d` modülleri kullanılmıyor ama bırakıldı: motor kodu kırpması
  (WebGL'de açık) kullanılmayan modülü zaten derlemeden çıkarır, kaldırmanın kazancı yok.
- Unity 6 ile GameCI akışı (unity-builder v6 + CLI v0.1.72 + Personal hesap etkinleştirmesi, 6000.0.85f1) bu ortamdan doğrulanamadı;
  CLI'nin kendi ölçümü 6000.0.36f1 ve 6000.6.0f1 üzerindedir. İlk çalışmada hata çıkarsa günlükleri okuyup düzeltiriz.

## 4. İlk yeşil koşudan sonra: Unity'nin ürettiği dosyaları depoya işle (P3-05)

Bulutta Unity açılamadığı için bazı dosyaları Unity ilk koşuda kendisi üretir: eksik `ProjectSettings/*.asset` dosyaları
(Graphics, Quality, Tag …), `ProjectSettings.asset`'in tam biçimi, `Packages/packages-lock.json` ve her varlığın `.meta` dosyası
(betik GUID'leri). Depoda yoklarsa her koşuda yeni GUID'le yeniden üretilirler: Library önbelleği isabetsizleşir ve ileride bir
varlık (prefab, ScriptableObject) bir betiğe GUID ile bağlanırsa her klonda kopar. İş akışı bu dosyaları her koşuda
**`unity-generated-files`** adlı artifact olarak yükler (derleme kırmızı olsa da; saklama 14 gün).

1. Actions → ilk **yeşil** koşu → **Artifacts** → `unity-generated-files` dosyasını indir ve aç (ya da
   `gh run download <koşu-no> -n unity-generated-files -D <klasör>`). İçinde `ProjectSettings/`, `Packages/` ve `Assets/` klasörleri olmalı.
2. Bu klasörlerin içeriğini deponun köküne kopyala (üzerine yazılsın). `git status` yalnız yeni `.meta` dosyalarını, `ProjectSettings/`
   altındaki yeni dosyaları ve `ProjectSettings.asset` / `packages-lock.json` değişikliğini göstermelidir; başka bir değişiklik
   beklenmedik demektir, commit öncesi bakılır.
3. `git add -A && git commit -m "chore: commit Unity generated settings and meta files" && git push origin main`. Sonraki koşu Library
   önbelleğini bir kez kaçırır (anahtar `Assets/**` özetini içerir), sonra GUID'ler sabittir.
4. Günlükte `is not setup` ara. `ProjectSettings/InputManager.asset` depoda hazırdır: Unity 6000.0.69f1 ve 6000.0.9f1 projelerinin
   (Unity-Technologies/InputSystem, game-programming-patterns-demo) ortak varsayılanıdır, 18 eksen (Horizontal, Vertical, Submit,
   Cancel …). Uygulama bu eksenleri `StandaloneInputModule` yüzünden ister: eksik olsa her karede istisna atılır, hiçbir uGUI düğmesi
   çalışmaz ve oyun ilk kazanmada kilitlenirdi. Aramada `is not setup` çıkarsa dosya yüklenmemiş demektir; artifact'teki `InputManager.asset`
   ile karşılaştırılır.

Bu adım code-lead'in işidir; ilk yeşil koşudan sonra yapılır (bu belgenin yazıldığı sırada hiç koşu olmadı, adımlar denenmedi).
