# Bulut derlemesi (GitHub Actions + GameCI) — Unity kurmadan oynamak

Unity Editor kurmadan oyunu denemenin yolu: GitHub, projeyi kendi sunucusunda derler ve WebGL sürümünü GitHub Pages'e koyar.
İş akışları: `.github/workflows/unity-activation.yml` (tek seferlik lisans dosyası) ve `.github/workflows/unity-webgl.yml`
(derleme + yayın; adı **Unity WebGL → Pages**). Unity sürümü `ProjectSettings/ProjectVersion.txt` ile sabit: **6000.0.85f1** (GameCI imajı var).

Yayın adresi (ilk başarılı derlemeden sonra açılır): https://yilmazfatih-cyber.github.io/minik-usta-unity/

**Durum (Faz 3 Q3):** Bu ortamda Unity hiç derlenmedi. Gerçek derlemenin ilk kanıtı, §1 tamamlandıktan sonraki ilk Actions
koşusudur; Q1 kodu (`b4efe18` ve sonrası) bu koşuda ilk kez derlenecek.

## 1. Tek seferlik: lisans ve gizli değerler

1. Ücretsiz Unity hesabı aç (unity.com) — e-posta + şifre lazım.
2. Lisans dosyası (`.ulf`) al. İki yol:
   - **Kurulumsuz:** GitHub'da repo → **Actions** → **Unity activation file** → **Run workflow**. Bitince çalışmanın altındaki
     **Artifacts** kısmından `Unity_v6000.0.85f1.alf` dosyasını indir. Ardından https://license.unity3d.com/manual adresine
     gir, Unity hesabınla oturum aç, `.alf` dosyasını yükle, "Personal" seç, `.ulf` dosyasını indir.
   - **Unity Hub ile:** Hub'ı kur, oturum aç; Personal lisans kendiliğinden etkinleşir. Dosya:
     Windows `C:\ProgramData\Unity\Unity_lic.ulf`, macOS `/Library/Application Support/Unity/Unity_lic.ulf`.
3. Repo → **Settings** → **Secrets and variables** → **Actions** → **New repository secret**; üç değer:
   | Ad | Değer |
   | --- | --- |
   | `UNITY_LICENSE` | `.ulf` dosyasının **tüm içeriği** (metin olarak aç, hepsini kopyala) |
   | `UNITY_EMAIL` | Unity hesabının e-postası |
   | `UNITY_PASSWORD` | Unity hesabının şifresi |
4. Repo → **Settings** → **Pages** → **Source: GitHub Actions** (bir kez).

GameCI bu değerleri yalnız derleme sırasında lisansı etkinleştirmek için kullanır; depoya yazılmaz.

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
| Cache Library | İlk koşuda önbellek boş; normal | — |
| Build | Unity açılır, lisans etkinleşir, `CiBuild.BuildWebGL` çalışır. Başarıda günlükte `WebGL build succeeded: build/WebGL/MinikUsta (N MB)` | Lisans: `Missing Unity License File`. Derleme: `error CS…`. Betik: `WebGL build failed: …` |
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
| `Missing Unity License File` | Üç secret eksik ya da yanlış | §1 adım 3; `UNITY_LICENSE` dosyanın tüm içeriği |
| `error CS0234` / `CS0246` / `CS0103` / `CS1061` (`Assets/MinikUsta/Unity/…`) | Unity API imzası ya da asmdef referansı | Hata metnindeki ad `UnityEngine.UI` ise `MinikUsta.Unity.asmdef` referansına bak (§3). Başka bir API ise `dotnet/UnityStubs/UnityEngine.cs` imzası gerçek Unity imzasıyla düzeltilir, sonra Unity katmanı. Kod değişikliği code-lead ile |
| `error CS…` (`Assets/MinikUsta/Core/…` ya da `Presentation.Core/…`) | Unity derleyicisi dotnet'ten farklı davranıyor (dil sürümü ya da tanımlar) | Önce `cd dotnet && dotnet build -warnaserror && dotnet test` ile yeniden üret; sürerse ilk hata satırıyla code-lead'e |
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
  derlenir (`dotnet/MinikUsta.Unity`; yazım ve imza denetimi). Gerçek Unity derlemesinin tek kanıtı bu iş akışıdır. Elle yazılmış
  dosyalar: `Level.unity` (+ `.meta`), `ProjectSettings/EditorBuildSettings.asset`, `ProjectSettings/ProjectSettings.asset`
  (yalnız gerekli alanlar; kalanını Unity varsayılanla doldurur ve ilk açılışta yeniden yazar).
- **uGUI asmdef adı (açık risk):** `Assets/MinikUsta/Unity/MinikUsta.Unity.asmdef` `UnityEngine.UI` adıyla referans veriyor. Unity'nin
  uGUI paketinin asmdef adı bu ortamda doğrulanamadı (Unity dokümantasyonuna erişim yok). Hata çıkarsa aday adlar `Unity.ugui` ve
  `Unity.UI`. Kesin cevap, Unity Editor ilk açılışta indirdiği `Library/PackageCache/com.unity.ugui@*/` altındaki `.asmdef`
  dosyasının adıdır.
- **`ProjectSettings.asset`:** `companyName` "Tuna & Co.", `productName` "Minik Usta", portre (`defaultScreenOrientation: 0`),
  eski Input Manager (`activeInputHandler: 0`), WebGL sıkıştırma **kapalı** (`webGLCompressionFormat: 2`; Unity'de
  `WebGLCompressionFormat` = Brotli 0, Gzip 1, Disabled 2; UnityCsReference `webgl.cs` kaynağıyla doğrulandı), gamma renk uzayı.
  `CiBuild.BuildWebGL` sıkıştırmayı zaten derleme anında kapatır.
- **`.meta` dosyaları:** Repoda şu an yalnız `Level.unity.meta` izlenir. Unity ilk açılışta diğer `.meta` dosyalarını üretir ve
  bunlar `git status`'ta yeni dosya olarak görünür. Commit öncesi gözden geçirilir; izlenmeleri gerekip gerekmediği code-lead
  kararıdır (GUID kararlılığı).
- **Beklenen uyarı:** `CiBuild.cs`, `#nullable` bağlamı olmadan `string?` kullanır; Unity derleyicisi bunu uyarı (CS8632) olarak
  verebilir. GameCI uyarıları hata saymaz; derleme durmaz.
- WebGL derlemesi sıkıştırmasız üretilir (`CiBuild.BuildWebGL`); GitHub Pages Brotli/Gzip başlıklarını vermediği için zorunlu.
  İndirme boyutu bu yüzden büyüktür (tahmini ≈ 30–60 MB); telefon tarayıcısında ilk açılış yavaş olabilir.
- Telefonda gerçek his için Android APK daha doğrudur: `CiBuild.BuildAndroid` hazır; ayrı bir iş akışı istendiğinde eklenir
  (imza anahtarı gerekmez, debug APK "bilinmeyen kaynak" izniyle kurulur).
- Personal lisans süresi dolarsa (GameCI topluluğu bunu bildiriyor) §1 adım 2 yinelenir.
- Unity 6 ile GameCI akışı bu ortamdan doğrulanamadı; ilk çalışmada hata çıkarsa günlükleri okuyup düzeltiriz.
