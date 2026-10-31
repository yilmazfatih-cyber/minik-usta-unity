# Bulut derlemesi (GitHub Actions + GameCI) — Unity kurmadan oynamak

Unity Editor kurmadan oyunu denemenin yolu: GitHub, projeyi kendi sunucusunda derler ve WebGL sürümünü GitHub Pages'e koyar.
İş akışları: `.github/workflows/unity-activation.yml` (tek seferlik lisans dosyası) ve `.github/workflows/unity-webgl.yml`
(derleme + yayın). Unity sürümü `ProjectSettings/ProjectVersion.txt` ile sabit: **6000.0.85f1** (GameCI imajı var).

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
- Sahne yoksa (Faz 3'ten önce) derleme atlanır ve not düşer; Faz 3 `Assets/MinikUsta/Scenes/` altına sahneyi ekleyince
  gerçek derleme başlar.
- Süre: ilk derleme 20–40 dk (imaj + Library önbelleği), sonrakiler 10–20 dk.
- Çıktı: https://yilmazfatih-cyber.github.io/minik-usta-unity/ (web sürümünden ayrı adres).

## 3. Bilinmesi gerekenler

- WebGL derlemesi sıkıştırmasız üretilir (`CiBuild.BuildWebGL`); GitHub Pages Brotli/Gzip başlıklarını vermediği için
  zorunlu. İndirme boyutu bu yüzden büyüktür (≈ 30–60 MB); telefon tarayıcısında ilk açılış yavaş olabilir.
- Telefonda gerçek his için Android APK daha doğrudur: `CiBuild.BuildAndroid` hazır; ayrı bir iş akışı istendiğinde
  eklenir (imza anahtarı gerekmez, debug APK "bilinmeyen kaynak" iznizle kurulur).
- Personal lisans süresi dolarsa (GameCI topluluğu bunu bildiriyor) 1. adım yinelenir.
- Unity 6 ile GameCI akışı bu ortamdan doğrulanamadı; ilk çalışmada hata çıkarsa günlükleri okuyup düzeltiriz.
