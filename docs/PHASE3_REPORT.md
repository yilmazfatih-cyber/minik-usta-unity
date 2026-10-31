# Faz 3 raporu — oynanabilir yer tutucu tahta (taslak)

Durum: **taslak; Q4 ve Q5 sonrası orkestratör tamamlar.** Unity Editor'de hiç derlenmedi (bulutta Unity yok, D-093).
İlk gerçek derleme GameCI koşusudur ([`docs/CI.md`](CI.md)); lisans secret'ları girilene kadar kırmızıdır.
Repo: `yilmazfatih-cyber/minik-usta-unity`, dal `main`. Model dağılımı: orkestratör doldurur (D-095).

## 1. Ne yapıldı

| Paket | İçerik | Commit |
| --- | --- | --- |
| Q0 | Motor-bağımsız sunum çekirdeği: yerleşik tween (16 yumuşatma eğrisi, Phaser formülleri), `FeelSettings` (tokens + JUICE), `FallMotion`, `StuckPulse`, düzen ve hit-test, `DragMath` / `DragMapper` (K-09, K-08), K-35 `AnimationPlanner` (43 olay türü), `AnimationQueue` | `e97e844` |
| Q1 | Oynanabilir yer tutucu tahta: `LevelPlayer` (basış → K-09 → BFS → kaldırma → K-08 takip → K-18 gölgesi → bırakma → `GameSession.Apply` → plan → kuyruk → adımlar), `StepRunner` (tween ile JUICE satırları), R-12 hızlı ilerleme, kilit ve 3× hız, pencereler (kazanma, hamle bitti, mola), prosedürel atlas, tahta sahnesi, uGUI HUD, `Level.unity`, `EditorBuildSettings`, `ProjectSettings`, `Resources` kopyaları, CI | `b4efe18` |
| Q3 | Belgeler: README Faz 3 bölümü, `CI.md` (ilk koşu beklentisi, hata ayıklama), `PORT_MAP` (Unity dosya tablosu, web `src/scenes/level/**` 40 dosya kapsamı, veri yolları), bu rapor taslağı. Senkron doğrulaması (0 sapma) | bu paketin commit'i |
| Q2 | Ses, titreşim, havuzlu parçacık, kamyon çizimi, seri boncukları, ↩ rozeti, ip ve eğilme, öğretici | **henüz yok** |

## 2. Ne test edildi (bu ortamda)

- `cd dotnet && dotnet build -warnaserror`: Build succeeded, **0 uyarı, 0 hata** (6 proje; `UnityStubs` ve `MinikUsta.Unity` dahil).
- `cd dotnet && dotnet test`: `MinikUsta.Presentation.Core.Tests` **238 / 238 geçti**; `MinikUsta.Core.Tests` **1455 geçti, 5 atlandı**
  (R-03, R-04, R-09, R-10, R-11: Öneri düzeyi açık bulgular), **0 kırmızı**. Bu sayı çalışma ağacında, git dışı
  `tools/diff/out` (109 dosya, `ts-oracle.mjs` çıktısı) mevcutken alındı.
- Temiz klon doğrulaması (`git archive HEAD` → ayrı dizin, `b4efe18`): build **0 uyarı, 0 hata**; Presentation **238 / 238**;
  Core **1049 geçti, 7 atlandı** (fark, `tools/diff/out` yokluğunda üretilmeyen 404 DiffFuzz durumudur). Kırmızı yok.
- Q1 testleri (`LevelPlayerTests`): 10 bölümün altın çözümü parmakla oynanır; `eventLogHash` TS değeriyle birebir; End'de durum
  sapması 0,5 px altında; vinç alanından bırakmalar; R-12 hızlı basış; K-18 gölgesi; K-09 titreme; K-07 iptal; K-22 / K-25 kilit
  ve 3× hız; K-29 yalnız "Tekrar dene"; 10 → 1 döngüsü; mola; geçersiz bölüm; K-34 kanca 5; K-49 dört ekran boyutunda sığma.
- `tools/sync-from-web.sh /home/user/minik-usta` (web `fd61abd`): sapma yok, `git status` temiz.
  `Resources/levels` (10), `Resources/solutions` (10), `config/economy.json`, `i18n/tr.json`, `i18n/en.json` ve
  `Presentation.Core.Tests/Golden/tokens.json` web kaynaklarıyla `cmp` ile bayt eşit.
- `Resources.Load` yolları (`levels/level_NNN`, `i18n/tr`, `solutions/level_NNN.solver`) ↔ dosyalar: eşleşiyor (grep ile).
- Sahne ve ayarlar: `Level.unity.meta` GUID = `EditorBuildSettings.asset` GUID = `a7cea9e5c5164f70b12f92c99359b9a4`; kamera
  ortografik 9,6 (= 1920 / 2 / 100); arka plan `#1F1850`; `webGLCompressionFormat: 2` (Disabled).
- Unity API denetimi: Unity katmanının kullandığı tüm Unity türleri `dotnet/UnityStubs` imzalarıyla derleniyor (derleme
  denetimi asıl kanıt). Q1 `unityApisUsed` listesi, Unity katmanındaki üye erişimlerinin kaba bir taramasıyla karşılaştırıldı;
  listede olmayan bir Unity üyesi bulunmadı (yerel alanlar ve Presentation türleri hariç). Tarama tam kanıt değildir.
- Geliştirme kısayolları `Debug.isDebugBuild` ile korunuyor; CI sürümü `BuildOptions.None` ile derlenir (kısayollar kapalı).

## 3. Ne doğrulanamadı (dürüst liste)

- **Unity Editor'de derleme.** GameCI koşusu yapılmadı; `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD` tanımlı değil
  (`Missing Unity License File`).
- Unity EditMode ve PlayMode testleri; `Level.unity` açılışı; `GameBootstrap`'in aktif sahne adı eşleşmesi.
- WebGL derlemesi, Pages yayını ve adresin açılması.
- **uGUI asmdef adı.** `MinikUsta.Unity.asmdef` `UnityEngine.UI` adıyla referans veriyor. Unity dokümantasyonuna bu oturumdan
  erişilemedi (adres çözümlenmiyor); doğru ad `Unity.ugui` ya da `Unity.UI` olabilir. Bulutta doğrulanamadı.
- `ProjectSettings.asset` yalnız gerekli alanları içerir; Unity ilk açılışta kalanını yazar.
- Performans: orta seviye telefonda 60 FPS, 4× CPU yavaşlatmasında ≥ 50 FPS, dokunuş-kare gecikmesi. Hiçbiri ölçülmedi.
- LegacyRuntime fontunda Türkçe harfler (ş, ğ, ı, İ, ö, ü, ç) görülmedi.
- Görsel: `artifacts/preview/*.png` (390×844 ve 360×800) yalnız yazılımsal önizlemedir, metin çizmez; Unity ekran görüntüsü değildir.
- Görsel çıta (ART_REFERENCE §10, Q1 öz puanı): Hacim ve ışık 3, Renk ve kontrast 3, Tipografi 2, Düzen 4, Karakter ve sahne 1,
  İkon ve rozet 2. R2-13'ün "≥ 4/5 altı düzeltilmeden teslim edilmez" kuralı birkaç boyutta karşılanmıyor. Q1 yer tutucu olarak
  teslim edildi; karar açık soruda.
- Ses ve titreşim: yok (Q2).

## 4. Bilinen sorunlar ve riskler

- **Çalışma ağacı kırmızı (commit dışı, Q3'ün yazarı olmayan değişiklikler).** Q3 sırasında `Presentation.Core` altında commit'siz
  değişiklikler belirdi ve sürdü: değiştirilenler `Art/Atlas.cs`, `Art/Symbols.cs`, `Feel/FeelSettings.cs`, `Game/HudLayout.cs`,
  `Game/Views.cs` (`ILevelView`'a `TrowelIcon`, `GoldChip`, `StarTarget` eklendi; `Presentation.Core.Tests/Fixtures/FakeView.cs`
  bunları henüz karşılamıyor); yeniler `Game/CellWave.cs`, `Economy.cs`, `Haptics.cs`, `Particles.cs`, `PressFeel.cs`,
  `TruckModel.cs`. Çalışma ağacında dotnet build 30 hata (CS0535) veriyor. Q3 bu dosyalara dokunmadı ve commit'e almadı; commit
  yalnız dört belgeyi içerir (`git add -A` kullanılmadı). HEAD (`b4efe18`) temizdir (§2). Bu değişikliklerin sahibi orkestratör
  tarafından belirlenmeli; çalışma ağacının tek yazarlı olması doğrulanmalı.
- `MinikUsta.Unity.asmdef`: uGUI referans adı (§3). İlk GameCI hatası bu olabilir; düzeltme tek satır.
- `CiBuild.cs`: `#nullable` bağlamı olmadan `string?`; Unity'de CS8632 uyarısı beklenir, hata değil.
- Yalnız `Level.unity.meta` izleniyor; Unity'nin üreteceği diğer `.meta` dosyaları henüz commit'te değil.
- `Resources/config/economy.json` ve `Resources/i18n/en.json` kopyalı ama çalışma zamanında okunmuyor (güçlendirici açılışı Q2+;
  EN bağlantısı açık: CLAUDE.md "TR/EN eksiksiz" kapısı).
- Q1 pencere kapsamı eksik: +5 teklifi, çıkış onayı, Söküm, kayıp penceresi. Geri Al düğmesi yok.
- "Animasyonları azalt" ayarı yok (JUICE §0 kural 8): `LevelPlayer.Reduced` her zaman `false`; ayar ekranı yok.
- `PORT_MAP.md` kaynak commit'i düzeltildi (`b31c4e8` → `fd61abd`); veri değişmedi (`4ce980f`'den beri).
- `TestPaths`'in `Application.dataPath`'tan repo kökünü bulması Unity'de denenmedi (Core ve Presentation testleri Unity
  EditMode'da bu yola bağlı).
- Web'de bilinen T-01 (D1 yeniden dizme RNG başlığı) C# çekirdeğinde TS'yi birebir izler; web düzeltmesiyle birlikte kapanacak.

## 5. Açık sorular (orkestratör)

- Q1'den kalan: (a) ART_REFERENCE ≥ 4 çıtası hangi pakette geçerli olacak; (b) kazanma penceresi bitmiş yapıyı alçak mı
  gösterecek, yoksa yapı ayrıca (panorama ya da kart üstünde) mı gösterilecek; (c) LegacyRuntime Türkçe glifleri çizmezse
  Baloo 2 TTF `Resources`'a eklenebilir mi (lisans ve boyut onayı); (d) kullanılmayan URP ve Cinemachine paketleri
  `Packages/manifest.json`'dan çıkarılsın mı (CI süresi; D-096'ya göre kullanılmıyor).
- Q3'ten: (e) Unity'nin üreteceği `.meta` dosyaları repoya girsin mi; (f) Pages dışında Android APK iş akışı istenecek mi.

## 6. Sonraki adım

- Proje sahibi: §1 üç secret ve Pages kaynağı → **Unity WebGL → Pages** ilk koşusu. Günlükte `WebGL build succeeded`
  satırı ya da `docs/CI.md` §2b'deki hata tablosu.
- Düzeltme turu: ilk Unity hatası geldiğinde UnityStubs imzası ve asmdef adı düzeltilir, sonra Q2.
- Q4 ve Q5 kapsamı: orkestratör tarafından yazılacak.
