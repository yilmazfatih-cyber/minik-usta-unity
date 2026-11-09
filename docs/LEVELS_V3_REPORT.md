# Bölümler v3 — paket raporu (LEVELS_V3_REPORT)

Sahip: code-lead (belge) · Tarih: 2026-11-09 · Kapsam: proje sahibinin 2026-11-08 isteği ("10 bölümü baştan incele; daha zevkli
tasarım; uğraşılabilir ama abartısız; daha iyi görsel; daha çok blok; daha farklı şekil ve özellik").
Karar: web `docs/DECISIONS.md` **D-102 (KABUL)**. Kural metinleri: web `docs/GDD.md` §16 (K-56…K-66, KABUL).
Bu rapor yalnız bu oturumda koşturulan komutların ve web/Unity dosyalarının çıktısına dayanır. Tahmin edilen değer yoktur.

## 1. Durum özeti

- Yayın bölümleri: web `levels/level_001…010.json` (commit `701b8f3`, `wip/phase-2r`); Unity `Resources/levels` ile bayt bayt aynı (10/10).
- Blok sayısı: **8, 8, 9, 10, 12, 14, 13, 14, 14, 16 = 118** (bugün 70; hedef 124 değil, bkz. §5 CL-V3-01).
- Yeni şekiller: L4/J4 (Bölüm 2), C3 (Bölüm 4), T4 (6), I3 (8). O4 (kare) eski şekildir; Bölüm 3, 5, 7, 9'da kullanılır.
  Uzun şekiller yalnız dikey yönde ve 2 sütunluk sahada (K-56).
- Yeni blok özellikleri (4 + 1): iki renkli Y9 (Bölüm 5), sargı + makas Y11 (7), bonus Y12 (9), kaygan Y13 (8).
  Palet Y10 **yayınlanan hiçbir bölümde yok** (K-58 madde 9 açık).
- Çekiç açılışı Bölüm 10 (`config/economy.json` `unlockLevel` 10). Vinç ve Altın Mala mevcut kurallarda kalır.
- Oynama adresi: https://yilmazfatih-cyber.github.io/minik-usta-unity/ (her `main` itişinden sonra GameCI ~8 dk; başlıkta
  "levels v3b preview" yazması CL-V3-02 kapanışına göre beklenir; bu oturumda doğrulanmadı). Bu rapor yazılırken Pages yeni sürümünü henüz doğrulamadım (bkz. §6).

## 2. Paketler ve commit'ler

| Paket | Model (D-095/D-098) | Web commit | Unity commit | Sonuç |
| --- | --- | --- | --- | --- |
| G1 / G1b (öneri, tasarım, 10 bölüm taslağı) | Opus (orkestratör Fable) | `015d715`, `2c28e44` | — | ÖNERİ P-G1-1, LEVELS §8 |
| E1 (çekirdek v3: şekiller, Y9…Y13, RULES_VERSION 4, büyük arama, üretici) | Opus | `2035fc6` | — | Çekirdek TS |
| E2 (10 bölüm, çözücü doğrulaması) | Opus | `1fe9af3` (wip/e2) | — | Bölüm verisi (sonra F ile birleşti) |
| E3 (C# port, sunum, sanat, Bölüm 99) | Sonnet | — | `c8b7748` (snapshot), `00e68bf`, `a4e1fb1`, `7893c1d`, `d52cae1` | C# port ve Unity v3 görünümü |
| F (bölüm v3b: 8–16 blok, büyük arama kapısı, CL-V3-01…12) | Sonnet | `701b8f3` | `7e7a2b3` (sync) | Yayın bölümleri |
| Şüpheci inceleme (code-lead) | Opus | — | `431bf8e` (`docs/REVIEW_LEVELS_V3.md`) | Bulgular + kapanışlar |
| Z (orkestratörün model dağılımında adı geçen mekanik iş) | Haiku | — | — | İş içeriği bu depolarda izlenemiyor |
| Bu paket (PKG): D-102, GDD etiketi, rapor | Haiku (belge) | `961f56b` | bu raporun commit'i | Bu belge |

Web `961f56b` yalnız belgeleri ve `tools/rule-coverage.ts` (ÖNERİ yanında KABUL başlıklarını da sayar; K-56…K-66 sayısı 66'da kalır) değiştirir.

## 3. Bölüm tablosu (web `levels/` 701b8f3)

Kaynak: JSON dosyaları ve `npm run levels:check` (web, `--check`). Blok sayısı `yard.batches` içindeki parçalardan sayıldı.
"Min" çözücünün **en iyi bulduğu** değerdir; tam tarama ile kanıtlanan yalnız Bölüm 1, 2 ve 7'dir (bkz. §5). "YAO" =
`liftRatio` (v3 ölçütü, K-64); Bölüm 1–2 `schemaVersion` 2 dosyası ya da tam tarama ile ölçüldü.

| # | Ad | Zorluk | Saha (sütun×satır) | Kat | Blok | Şekiller | Özellik (öğretilen) | Min (alt sınır) | Hamle | YAO | Oyun vekili (placement-first, "≤ hamle") |
| - | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | Ağaç Basamakları | Kolay | 5×4 (2×4) | 2 | 8 | D2 ×8 | — (S1) | 8 (8, kanıtlı) | 12 | 1,00 (8/8) | — |
| 2 | Köşe Destekleri | Kolay | 6×5 (2×6) | 2 | 8 | D2 ×4, L4 ×2, J4 ×2 | — | 8 (8, kanıtlı) | 12 | 1,00 (8/8) | — |
| 3 | Köşe Taşları | Kolay | 5×5 (2×5) | 2 | 9 | D2 ×8, O4 ×1 | — | 11 (10) | 15 | 0,78 (7/9) | 0,18 ≤15 · 0,41 ≤20 |
| 4 | Ön Duvar | Kolay | 6×5 (2×4) | 3 | 10 | C3 ×4, D2 ×6 | — | 12 (11) | 16 | 1,00 (10/10) | 0,11 ≤16 · 0,14 ≤21 |
| 5 | Renkli Pencere | Normal | 6×6 (2×5) | 3 | 12 | D2 ×8, C3 ×2, O4, L4 | iki renk ×4 (Y9) | 14 (13) | 18 | 0,75 (9/12) | 0,33 ≤18 · 0,57 ≤24 |
| 6 | Uzun Gövde | Normal | 6×7 (2×6) | 3 | 14 | D2 ×7, C3 ×6, T4 ×1 | iki renk ×1 | 16 (15) | 18 | 0,79 (11/14) | 0,07 ≤18 · 0,12 ≤25 |
| 7 | Paketli Malzeme | Normal | 6×6 (2×4) | 4 | 13 | D2 ×9, C3 ×2, O4 ×2 | iki renk ×2, sargı ×2, makas ×1 (Y11) | 14 (14, optimal) | 18 | 0,62 (8/13) | 0,47 ≤18 · 0,38 ≤25 |
| 8 | İp Merdiven | Normal | 6×7 (2×6) | 3 | 14 | D2 ×7, C3 ×3, I3 ×3, L4 ×1 | iki renk ×3, kaygan ×2 (Y13) | 16 (15) | 20 | 0,79 (11/14) | 0,01 ≤20 · 0 ≤27 |
| 9 | Çatı Altı | Normal | 6×7 (2×6) | 3 | 14 | D2 ×7, C3 ×2, I3 ×4, O4 ×1 | iki renk ×6, bonus +2 ×1 (Y12) | 18 (15) | 22 | 0,64 (9/14) | 0 ≤22 · 0,03 ≤29 · bonus anı 0,89 |
| 10 | Ağaç Ev Tamam! | Zor | 6×8 (2×5) | 4 | 16 | C3 ×3, I3 ×5, D2 ×8 | iki renk ×4, sargı ×2, makas ×1, kaygan ×1 | 20 (17) | 21 | 0,75 (12/16) | 0 ≤21 · 0,01 ≤29 |

Notlar: Duvar yüksekliği 3–5; 3 katlı ve 4 katlı bölümler `build.segments` ile tanımlıdır. "Oyun vekili" çözücünün
içinde çalışan sabit bir oyuncu sezgiseldir; `ahaMin` değildir (CL-V3-03). Bot (`tools/playtest-bot.ts`) bu depoda yok;
bu yüzden orta/usta bot oranı **yoktur** (bkz. §6). Bölüm 1 ve 2'de vekil satırı çıkmadı.

## 4. Ölçümler (bu oturumda koşturuldu)

Web `wip/phase-2r` (961f56b):

- `npm run check`: 120 test dosyası geçti; 2180 test geçti, 83 atlandı (toplam 2263); `test:rules --phase v3`: 87 kimlik, 1658 test.
  Atlananlar eski oturumdan gelir; bu paket yeni atlama eklemedi.
- `npm run levels:check`: `validate` 10 dosya, 0 hata, **7 uyarı** (L-41 `tile_family_mix`, Bölüm 5–9; K-63); `solve` 10 bölüm çözüldü
  (önbellekten), 0 hata, 0 uyarı; `bot` adımı atlandı (`tools/playtest-bot.ts` yok). Komut çıkışı 0.
- Büyük arama (U-v3-2, ışın genişliği 8–512): Bölüm 3–10 için "en iyi bulundu, optimal değil". Yalnız Bölüm 7 alt sınıra
  eşitlendi (14 = 14).
- Çıkmaz kapısı (CL-V3-01 B, 10 000 rastgele yürüyüş, tahmin, kanıt değil): Bölüm 5'te 182 örnekten **1** D1/D2/D3a'nın
  yakalamadığı çıkmaz durumu çıktı (tahmini %0,01). Bölüm 3, 4, 6, 8, 9, 10 için yakalanmayan çıkmaz 0 (Bölüm 7'de örnek yok). Bölüm 1 ve 2 tam taramayla kanıtlı, çıkmaz 0.

Unity `main` (7e7a2b3 + bu paket) — `dotnet/`:

- `dotnet build -warnaserror`: **0 uyarı, 0 hata**.
- `dotnet test` (ilk koşu, `tools/diff/out` yokken): Presentation.Core.Tests 586/586 geçti; Core.Tests 1254 geçti, 8 atlandı
  (3 DiffFuzz/L-40-L-41 paritesi + 5 eski `[Ignore]`).
- `node tools/diff/ts-oracle.mjs <web>` (web 701b8f3): 767 dizi, 20 894 eylem; 24 v3 özellik tahtası dahil; coveredPicks 583;
  familyWins: iki renk 41, sargı 16, kaygan 13, bonus 5.
- `dotnet test` (oracle çıktısıyla, ikinci koşu): Presentation.Core.Tests 586/586 geçti; **Core.Tests 2023 geçti, 5 atlandı**
  (atlananların hepsi `ReviewPhase2Tests.cs` içindeki R-03, R-04, R-09, R-11 "Öneri" `[Ignore]`'larıdır, eskiden beri).
  DiffFuzz ve L-40/L-41 paritesi koştu ve geçti (0 sapma).
- `bash tools/stub-gate-selftest.sh`: 4/4 beklenen sonuç (korumasız `Handheld` WebGL'de derlenmez; diğer üç durum derlenir).

## 5. Bulgular ve kapanışlar (kısa)

Şüpheci inceleme (`docs/REVIEW_LEVELS_V3.md`, kapanış F):

- **CL-V3-01 (Engel) — kısmen kapandı.** Blok toplamı 118 (hedef 124). Bölüm 10: 16 blok (hedef 16–22'nin alt ucu).
  17–22 blok için D2 payı sınırı gevşemeli ya da 6×8'den büyük saha gerekir. Ürün kararı.
- **CL-V3-02 (Engel) — kapandı (Unity tarafı).** Web `levels:check` yeşil; Pages başlığına "levels v3b preview" eklendi.
  `shape_decisions_low` Hikaye 1'de uyarı olarak kaldı (B3, product-lead onayı bekliyor).
- **CL-V3-03 (Önemli) — kısmen.** `ahaMin` ölçülemiyor; yerine oyun vekili. Bölüm 10 hamle 21 = en iyi 20 + 1.
- **CL-V3-04 (Önemli) — kısmen.** D2 payı toplamda %61; bölüm 4, 5, 7'de %60–69 (eşik %50'yi tutmadı).
- **CL-V3-05 (Önemli) — kapandı.** Kaygan Y13 Bölüm 8 (P-G1-1'deki 13 değil).
- **CL-V3-06 (Önemli) — karar bekliyor.** Bonus Hikaye 1'de `k` = 0: bütçeyi değiştirmez, his anıdır; HUD "(+2)" yok.
- **CL-V3-07 (Önemli) — kapandı.** Çekiç açılışı 10.
- **CL-V3-08 (Önemli) — kısmen.** G–Y çifti izinli çiftlerden çıkarıldı; renk körü sembol çizimi (BlockSkin) yapılmadı.
- **CL-V3-09 (Önemli) — kapandı.** K-62, K-63, K-65 C# testleri eklendi; K-64 yalnız TS aracında.
- **CL-V3-10 (Önemli) — kısmen.** Oracle çıktısı ve aile istatistikleri var; palet ailesi için gönderilen bölüm yok.
- **CL-V3-11 (Önemli) — kapandı.** `test:rules --phase v3` (K-56…K-66) npm run check'e bağlı.
- **CL-V3-12 (Önemli) — kapandı.** `tut.m.twotone` ve `obs.y9…y13.desc`.

Web tarafında bu oturumda eklenen düzeltmeler (961f56b): GDD §16 K-56…K-66 etiketi KABUL; `tools/rule-coverage.ts` KABUL
başlıklarını da sayar. Başka bir kural metni değişmedi.

## 6. Doğrulanmadı (dürüst liste)

- **Unity Editor / GameCI WebGL derlemesi ve Pages yayını** bu oturumda çalıştırılmadı. Push sonrası ~8 dk içinde Verify
  adımı ve Pages yeni sürümü beklenmeli; sonucu bu rapor yazılırken görmedim.
- **Ekran görüntüleri ve görsel okunurluk** (design-lead): CI `v3_*` çekimleri var (E3), ama bu oturumda incelenmedi.
- **Eğlence puanı (1–5)** v3b bölümleri için yok. G1 playtest raporu (`docs/review_inbox/levels-v3-playtest.md`) eski 10
  bölüm setine aittir ve yeni bölümler için yeniden puanlanmadı.
- **Bot oranları** yok: `tools/playtest-bot.ts` bu depoda yok. Yerine yalnız oyun vekili.
- **Min'in optimalliği** Bölüm 3–10 için kanıtlanmadı (§3). Bölüm 9 ve 10'da alt sınır farkı 3 (18/15, 20/17).
- **D-090 tam taraması** v3 bölümleri için koşulmadı (6×6+ saha ve 10+ blok sayısı 2,5 M durum sınırını aşar); çıkmaz
  kapısı tahmindir.
- **Performans** (orta telefon 60 FPS, DevTools 4× ≥ 50 FPS, ilk yükleme bütçesi) bu oturumda ölçülmedi.
- **Ekran okuyucu ve i18n TR/EN** eksiksizliği bu oturumda yeniden denetlenmedi.
- **Oracle çıktısı git dışıdır** (`tools/diff/out/`). Temiz bir çalışma kopyasında 3 paritesi testi atlanır; CI'da oracle
  çalıştırılıp çalıştırılmadığı bilinmiyor.

## 7. Bilinen sorunlar

1. Blok toplamı 118, hedef 124; Bölüm 10 alt sınırda (16). 17–22 blok yok.
2. Palet (Y10) yayında yok. K-58 madde 9 (katman yalnız şantiyeye) ya da D3b tablosu gerekir (ÖNERİ P-E2-1, B1 bulgusu).
3. Bölüm 5'te bir çıkmaz durumu yakalanmadı (182 örnekte 1; tahmin). Kanıt yok.
4. L-41 `tile_family_mix` 7 uyarı (Bölüm 5–9): çözücü sonuçları ile kapatıldığı iddia ediliyor; bağımsız doğrulama yok.
5. `shapeDecisions` büyük arama bölümlerinde `–` (tam çizge yok); L-42 `shape_decisions_low` kapısı bu bölümlerde ölçülemedi.
6. Bonus (Y12) Hikaye 1'de bütçeyi değiştirmez; HUD "(+2)" yok (CL-V3-06).
7. Renk körü blok sembolleri yapılmadı (CL-V3-08).
8. Analytics `booster_used.target` için `wrap` değeri yok (ANALYTICS.md entrepreneur'ün dosyası; E3'te talep edildi).
9. Oracle çıktısı git dışı; temiz kopyada 3 DiffFuzz/L-40-L-41 paritesi testi atlanır.

## 8. Açık sorular (proje sahibi / product-lead)

1. Bölüm 10 (ve 6–9) blok sayısı: 16–22 hedefi 17–22 için D2 payı sınırı gevşetilerek mi, 6×8'den büyük saha ile mi
   karşılanacak? (CL-V3-01)
2. Palet (Y10) ne zaman girsin: K-58 madde 9 (katman yalnız şantiyeye) mi, D3b tablosu mu?
3. Bonus Hikaye 1'de bütçe ekleme (`k` = 1) mi, yoksa yalnız ödül anı (`k` = 0) mı? (CL-V3-06)
4. Çekiç 10. bölümde açılıyor (economy.json). P-E2-1 önerisi (A) kabul mü?
5. Y13 kaygan 8. bölümde (P-G1-1 öneri 13'ü yerine). Kabul mü?
6. Eğlence değerlendirmesi için bir insan oyuncu testi (DKT) gerekiyor mu, yoksa oyun vekili yeterli mi?
7. Unity `README.md` D-099, D-100, D-101 kimliklerini "Paket B2" için kullanıyor; web `DECISIONS.md` bu numaraları yazmıyor.
   Orkestratör bu numaraları tek listede eşitlemeli.

## 9. Sonraki adım

- product-lead: K-58 palet kararı, blok sayısı 16–22 sınırı, Çekiç ve Y13 açılışları (§8).
- code-lead: `### K-xx` başlığına terfi ve TECH §12.4 satırları; v3 bölümler için tam tarama (U-v3-2 B, makro çizge); bot
  (`tools/playtest-bot.ts`); `shapeDecisions` ölçümü; analytics `wrap` (entrepreneur ile).
- design-lead: v3 ekran görüntüleri (CI `v3_*`) ve kaplama okunurluğu.
- Orkestratör: Pages'te "levels v3b preview" yayınının gerçekten çıktığını ve Verify'ın yeşil olduğunu kontrol etmek.
