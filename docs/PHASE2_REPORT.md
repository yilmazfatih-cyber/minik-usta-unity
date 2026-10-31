# Faz 2 raporu — saf C# çekirdek (2026-10-31)

Durum: **tamamlandı; Faz 3 onayı bekliyor.** Repo: `yilmazfatih-cyber/minik-usta-unity`, dal `main`, son commit `859030e`.
Model dağılımı (D-095): P0–P2 oturum modeliyle (sınır kesintileri öncesi), P3–P5 Opus, P6 Sonnet; orkestratör Fable.

## 1. Ne yapıldı

| Paket | İçerik | Commit |
| --- | --- | --- |
| P0 | İskelet (asmdef'ler, `dotnet/` çözümü, NUnit), temel modüller: Types, Geometry, Coords, Shapes, Rng (mulberry32/splitmix32), Hash (Zobrist), Fnv1a, State (int[] tamponu), Grid, Ascii, bağımlılıksız JSON okuyucu/yazıcı, Level (şema, veri, compile, plan, mechanics) | `d9edc7f` |
| P1 | Movement (BFS kenar modeli, RAIL, K-08 yapışkan takip, K-09), Gravity, Placement (K-16/K-17/K-34), Site, Panorama, Delivery | `…` (P2 ile birlikte) |
| P2 | Level logic (K-45 kodlarının tamamı), Goals, Combo, Boosters (K-36…K-40, K-54), Deadlock (D1/D2/D3a + Söküm), Summary, engel kaydı (W1, Y3, S1) | `c4030da` |
| P3 | Moves (`ApplyMove`, K-35 12 adım, 43 olay türü, `EventLogHash`/`CanonicalJson`/FNV-1a birebir), Session (undo, replay, exit/movesSpent, +5 teklifi, Söküm paketi, `RulesVersion = 2`, `LevelHash`) | `ff5ac4c` |
| P4 | Kural kapsamı kapısı (76/76 kimlik), TS-kâhinli diferansiyel fuzz aracı (`tools/diff/ts-oracle.mjs`), senaryo testleri (Söküm, D1, +5, undo, kamyon kuyruğu) | `7404317` |
| P5 | Şüpheci inceleme: 12 bulgu (5 Önemli, 7 Öneri), her biri testle sabitlendi — `docs/REVIEW_PHASE2.md` | `afd7797` |
| P6 | Önemli bulguların kapanışı: JS anlamlı regex (`JsRegex`), kültürden bağımsız sayı ayrıştırma, iş parçacığı güvenli D3a belleği, 4 yerel ayarda test; T-01 (TS hatası) davranış olarak sabitlendi | `859030e` |

Çekirdek: `Assets/MinikUsta/Core` — 27 C# dosyası, UnityEngine bağımlılığı yok (asmdef `noEngineReferences`), C# 9, netstandard2.1.
Testler: `Assets/MinikUsta/Core.Tests` — NUnit 3 alt kümesi (Unity Test Framework uyumlu); `dotnet/` çözümüyle bulutta koşuyor.

## 2. Ne test edildi (hepsi yeşil)

- `dotnet build -warnaserror`: 0 uyarı, 0 hata.
- `dotnet test`: **1455 geçti, 0 kırmızı, 5 atlanan** (5 atlanan = Öneri düzeyindeki açık bulgular R-03/04/09/10/11; aşağıda).
- **Altın eşdeğerlik (brifin "aynı seed + aynı hamleler = aynı board" şartı):** 10 bölümün kanonik çözümleri C# ile oynandı; olay karması, son tahta ASCII'si ve bölüm karması TS değerleriyle bire bir (40/40).
- **Diferansiyel fuzz:** TS çekirdeği kâhin olarak 405 rastgele hamle dizisi (bölüm, tam örtü, 9 geometri, kamyon, senaryo) × 10 374 eylem (drag 8 291, crane 581, paint 335, goldTrowel 229, hammer 53, undo…); her adımda Zobrist karması, olay dizisi ve ASCII karşılaştırıldı: **0 sapma.**
- **Kural kapsamı:** Faz 2R'nin 76 kural kimliğinin (K 46, engel 5, E 22, N 3) her birinin en az bir C# testi var (eksik 0).
- Oturum vektörleri: 170 kayıtlı oturum dizisi (4 298 girdi, 23 olay türü) TS ile aynı.
- Yerel ayar: fi_FI, ar_SA, sv_SE, tr_TR altında da 0 kırmızı (P6 öncesi fi_FI'de 599 kırmızı vardı).
- Paralel D3a (iş parçacığı güvenliği) testi.

## 3. Ne doğrulanamadı (dürüst liste)

- **Unity Editor içinde derleme ve EditMode koşusu** yapılmadı (bulutta Unity yok, D-093): asmdef çözümlenmesi, `UNITY_INCLUDE_TESTS`, precompiled `nunit.framework.dll` referansı, `TestPaths.RepoRoot`'un `Application.dataPath`'tan yukarı yürümesi yalnız dotnet yolunda denendi.
- **IL2CPP / Unity Mono:** `JsRegex` (Regex alt sınıfı), `SetCulture("")`, `double` "R" biçimi ve `TryParse` davranışı yalnız .NET 8'de yeşil.
- Faz 3 engelleri (W4–W8, Y1/Y2/Y4/Y6, S2–S9) için kural kancası yok; fuzz oraya ulaşmıyor (web'de de henüz yok).
- T-01'in oyun içi etkisi (D1 yeniden dizmenin başarısızlık olasılığı) ölçülmedi.

## 4. Bulunan hatalar ve açık bulgular

- **T-01 (web TS'de hata, Önemli):** `deadlock.ts` `reshuffleYard` her denemede RNG başlığını da geri yüklüyor; 8 deneme fiilen 2 farklı dizilim. 1–10. bölümlerde D1 tetiklenmediği için oyuncu görmüyor. C# TS'yi birebir izliyor (D-091); web ile birlikte düzeltilecek (REVIEW_LOG'a yazıldı).
- Açık Öneri bulguları ([Ignore]'lu testler): R-03 ±2^53 dışı tamsayılar, R-04 dizi-indeks anahtar sırası (canonicalJson), R-09 negatif hücre kodunda Math.floor farkı, R-10/R-11 kenar durumları. Hiçbiri geçerli bölüm verisinde tetiklenmiyor; Faz 5 cilasına bırakıldı.

## 5. Faz 3 planı (onay bekliyor)

Hedef: yer tutucu görsellerle **oynanabilir tahta** + AnimationQueue + game feel listesi; CI ile WebGL derlemesi GitHub Pages'e.
- **Mimari (Opus):** `Assets/MinikUsta/Presentation` — `GameBootstrap` (bölüm yükleme: StreamingAssets JSON → Core), `BoardView`/`PieceView` (nesne havuzu), `DragController` (K-08 yapışkan takip, parmak ofseti 1,2 hücre, gölge), `AnimationQueue` (Core olaylarını DOTween sekanslarıyla sırayla oynatır; mantık animasyonu beklemez), `FeelSettings` ScriptableObject (bütün süreler/easing'ler ayarlanabilir).
- **Game feel (Sonnet; D-091 eşlemesi):** kaldırma %8 büyüme + gölge; bırakma/iniş OutBack + squash & bounce; kötü geçiş geri sekmesi + titreme; doğru yerleşim kilit parlaması + parçacık; dilim bitişi + kamyon teslimatı dizisi; Söküm uçuşu; kalan blok çipine uçuş + sayaç "pop"; kazanmada kalan hamleler altına dönüşür (Bonus İnşaat); 5 sn hareketsizlikte takılma nabzı; düğme basınca küçülme / bırakınca OutBack.
- **Sahne ve veri (Haiku):** `Assets/MinikUsta/Scenes/Level.unity`, yer tutucu sprite'lar (yuvarlatılmış renkli kutular, 8 renk + sembol), `ProjectSettings` tamamlanması, Pages için WebGL şablonu.
- **Doğrulama:** EditMode testleri (AnimationQueue sıralaması, DragController → Core hamle eşlemesi), PlayMode duman testi; CI'da WebGL derlemesi → https://yilmazfatih-cyber.github.io/minik-usta-unity/ ; sen tarayıcıda/telefonda denersin, Unity Editor'de de açarsan `Window → General → Test Runner` ile EditMode testlerini koşturursun.
- **Senden:** GitHub secrets (`UNITY_EMAIL`, `UNITY_PASSWORD`, `UNITY_LICENSE`) + Pages kaynağı "GitHub Actions" (`docs/CI.md`). DOTween'i CI'da kuramam (Asset Store); Faz 3'te DOTween yerine küçük bir yerleşik tween (Core-dışı, ~200 satır) kullanıp DOTween'e geçişi senin yerel kurulumuna bırakacağım — brifte DOTween yazıyor; kabul edersen bu ayrımı D-096 olarak yazarım.
