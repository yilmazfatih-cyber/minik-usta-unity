# Bölümler v3c — paket raporu (LEVELS_V3C_REPORT)

Sahip: code-lead (belge) · Tarih: 2026-11-12 · Kapsam: Bölüm 6–10 üç sütunlu şantiye, palet (Y10), sargı/makas/kaygan/bonus
yayını ve çekirdek (C#) eşitliği (paket Z kapsamında, P1–P4 ve F turlarının toplamı).
Kararlar: web `docs/DECISIONS.md` **D-103** (geniş şantiye), **D-104** (palet yalnız şantiyeye bırakılır), **D-105** (bonus yalnız ödül anı)
— hepsi KABUL (proje sahibi, 2026-11-09). Bu paketle D-099…D-101 de web `docs/DECISIONS.md`'ye KABUL olarak yazıldı (Unity README'nin
referansı). Kural metinleri: web `docs/GDD.md` §16 (K-49, K-56, K-58, K-60, K-62, K-63). Bölüm tasarımı: web `docs/LEVELS.md` §8.14.

Bu rapor yalnız bu oturumda koşturulan komutların çıktısına ve commit'lere dayanır; "tahmin" olarak işaretlenmeyen her sayı P3/P4/F
raporlarından alınmış ve dosya içeriğiyle karşılaştırılmıştır (bkz. §6).

## 1. Durum özeti

- Yayın bölümleri: web `levels/level_001…010.json` (web `wip/phase-2r`, F turu `b185084`); Bölüm 1–5 değişmedi, Bölüm 6–10 üç
  sütunlu şantiyede (Ws = 3). Blok sayıları **6: 17 · 7: 18 · 8: 18 · 9: 17 · 10: 19 = 89** (v3b'de bu beş bölüm 71 idi).
- Hedef "18–22 blok" (D-103) tutturulmadı: 6 ve 9 bir blok eksik, 17–19 aralığı. Nedeni geometrik (aşağıda §6); karar
  proje sahibinde (ÖNERİ P-P3-1, LEVELS §8.14.6).
- Çekirdek: Geometry.MaxCols 9; P5 (artı) şekli 52–55 indislerinde; `heavy` = yalnız I5/Q9 (web ve C# aynı); palet katmanları
  yalnız şantiyeye bırakılır (`palletSiteOnly`, 0 maliyet); bonus `movesChanged{reason:'bonus'}` olayı; `RULES_VERSION` 5.
- Yerleşim: 9 sütunlu tahta için hücre 108 px, duvar 54, tahta 1026, kenar 27 px; hücre 100 px'in altına inmez.
- Görsel: palet yığını `x n` rozeti, her örtülü katman için ahşap levha; bonus "+2" patlaması (≤ 900 ms, girdi engellenmez);
  Bölüm 6–10 başında ci-shots görüntüleri (v3_wide_6, v3_pallet_7, v3_wrap_8, v3_slippery_9, v3_bonus_10; bkz. §7).

## 2. Paketler ve commit'ler

Model dağılımı (D-095, D-098): P1/P2/P3/R Opus; P4/F Sonnet; Z Haiku; orkestratör Fable. Paket Z bu raporu ve web kararlarını yazar.

| Paket | Model | Web commit | Unity commit | Sonuç |
| --- | --- | --- | --- | --- |
| P1 (çekirdek düzeltme ve tasarım kontratı) | Opus | `1f197dd` | — | `heavy` hatası (U-v3c-1) kapandı; GDD K-49/K-56/K-58/K-60/K-62/K-63 yazıldı |
| P2 (çekirdek: 9 sütun, P5, palet, bonus, plan_trap) | Opus | `5d2a9d7` | `f7a8043` | RULES_VERSION 5; C# birebir (D-091) |
| P3 (bölüm tasarımı ve jeneratör: 6–10) | Opus | `37d5617` | `6d91021`, `4b8ed97` (ci-shots, palet görseli) | Bölüm 6–10 yayında; P-P3-1 ÖNERİ |
| P4 (Unity eşitliği, yerleşim, görsel, bilgi kartı) | Sonnet | — | `f7a8043`, `6d91021`, `4b8ed97` | C# 595 + 1280 test (§4) |
| F (kapanış: lookahead vekil kapısı, bonus kredisi, Ws = 3 zorunluluğu, pallet_stuck, CI) | Sonnet | `b185084` | `82ef885`, `e09624a`, `6c9d50f`, `2385036` | web ve Unity veri/vektör yeniden eşitlendi; ci-shots notu |
| R (şüpheci inceleme; 10 Engel/Önemli) | Opus | `885d1be` (review log) | `4c1f2a8` | Bulgular REVIEW_LOG'da |
| Z (karar kaydı, bu rapor) | Haiku | `42a44fa` (DECISIONS) | bu commit | D-099…D-105 KABUL |

## 3. Bölüm tablosu (yayın verisi)

Kaynak: web `levels/level_006…010.json` ve P3 kontratı (bu veriyle `levels:validate` yeşil). Plan hücresi Σ / dolu F / boş E;
`min` çözücünün en iyi bulduğu hamle (alt sınır parantezde; kanıtlı optimum değil); P bütçe payı; yüzde değerler yalnız
ölçülen vekillerdir.

| Bölüm | Ad | Zorluk ve tanıtım | Yard × şantiye × dilim | Duvar | Blok (domino) | Plan Σ/F/E | Özel bloklar | Palet | min (alt) | Hamle | P | YAO | Kaldırma oranı | Vekil kazanma (P3 raporu) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 6 | Geniş Temel | Normal, S9 | 6×8 · 3×5 · 3 | 7 | 17 (7) | 45/45/3 | iki renkli ×2 | — | 19 (18) | 23 | 4 | 1,00 | 1,00 | 0,25 / 0,24 |
| 7 | Palet Yığını | Normal, Y10 | 6×8 · 3×4 · 4 | 7 | 18 (8) | 48/44/4 | — | D2_90 W ×3 | 20 (19) | 24 | 4 | 1,00 | 1,00 | 0,16 / 0,38 |
| 8 | Paketli Kat | Normal, Y11 | 6×8 · 3×4 · 4 | 7 | 18 (9) | 48/44/4 | sargı ×2, makas ×1, iki renkli ×2 | D2_90 G ×3 | 20 (19) | 24 | 4 | 1,00 | 1,00 | 0,35 / 0,68 |
| 9 | Kaygan Çatı | Normal, Y13 | 6×8 · 3×5 · 3 | 7 | 17 (7) | 45/41/7 | kaygan ×2, iki renkli ×2, T4_270 | D2_90 W ×3 | 19 (18) | 23 | 4 | 1,00 | 1,00 | 0,26 / 0,44 |
| 10 | Ağaç Ev Tamam! | Zor, teaches yok | 6×8 · 3×4 · 4 | 6 | 19 (9) | 48/44/4 | sargı ×2, makas, kaygan, bonus +2, iki renkli C3 ×3, I3_90 | D2_90 G ×3 | 22 (20) | 23 | 1 | 1,00 | 0,79 | 0,04 / 0,30 |

Notlar: 1) Bölüm 6–9 P = 4 ile Normal tanıtım; 10 P = 1 ile Zor. 2) Vekil sütunu P3 raporundaki değerleri aynen taşır; "/" öncesi
bütçe içinde, sonrası bütçe + N/2 (P3'te bu iki değer 6. bölümde ters görünüyor, §6). 3) Bölüm 6–10 hedef bantları (win band):
6 ve 7 80–90 %, 8 80–90 %, 9 80–84 % (kasıtlı alt yarı), 10 50–60 %. 4) Bölüm 10 Zor olarak tek bölüm; bonus bu bölümde ilk kez görünür.

Her bölümde palet kullanılır (7–10), palet bütün katmanlarında `flag` yok; her palet yere basar (`pallet_not_grounded` geçer).
Bölüm 7 tanıtımı Normal zorlukta, Bölüm 1–5 2 sütunlu kalır (D-103).

## 4. Kapılar ve test sayıları (bu oturumda koşturuldu)

| Kapı | Komut | Sonuç |
| --- | --- | --- |
| Unity derleme | `cd dotnet && dotnet build -warnaserror` | 0 hata, 0 uyarı (19,7 s) |
| Unity testleri | `cd dotnet && dotnet test` | Presentation.Core.Tests: 595 geçti, 0 kaldı · Core.Tests: 1272 geçti, 8 atlandı, 0 kaldı (toplam 1280) |
| Stub kapısı | `bash tools/stub-gate-selftest.sh` | 4 durum beklendiği gibi ("all 4 cases as expected") |
| Web kapısı | `npm run check` (web worktree, 42a44fa) | Çıkış 0; 2208 geçti, 83 atlandı (§5) |
| Web bölüm kapısı | `npm run levels:check` | Yapılmadı: P3 raporuna göre uzun sürüyor (~10 dk, Bölüm 10 ölü durum taraması ~600 s); bu pakette kapsam dışı bırakıldı, §6'da açık |

Önceki kanıtlar (değiştirilmedi): P4 raporu Unity 595 + 1280 testini ve 10 geometri üzerinde yerleşim testlerini kaydeder;
F turu CI'da `levels:check` çalıştırmayı ekledi (web).

## 5. Web kapısı (Z turu, 42a44fa)

`npm run check` (web worktree, 42a44fa): çıkış kodu 0; testler 123 dosya, 2208 geçti, 83 atlandı (2291); rule-coverage (phase ≤ v3): 88 kimlik, 1685 test. `npm run levels:check` bu pakette koşmadı (§4); 83 atlanan testin nedenleri bu raporda incelenmedi.

## 6. Doğrulanmayanlar

- Blok sayıları (17/18/18/17/19) D-103'ün 18–22 amacının altında. Geometrik neden: tek D2_90 paletiyle 6×8 saha en çok 48 plan
  hücresi verir ve %50 domino sınırıyla 19 blok sınırlanır (P-P3-1).
- "Vekil kazanma" değerleri P3 raporundan alındı; F turu lookahead vekil kapısı ekledi ve bu sayılar yeniden ölçülmedi. Bölüm 6
  satırında "bütçe içi 0,25, bütçe + N/2 0,24" değerleri ters sırada görünüyor (ikincisi ilkinden düşük olamaz); yeniden ölçülmeden
  doğru sayı bilinmiyor.
- Ölü durum kanıtı yok: D-090 kapısı Bölüm 6–10 için 10 000 yürüyüşle tahmin, yakalanmayan çıkmaz %0,00. Tam tarama (plan_trap)
  yalnız plan düzeyinde kanıtlıdır; bu, tam çıkmaz kanıtı değildir.
- `min` değerleri çözücünün en iyi bulduğu değerdir; alt sınır (parantez) ile 1–2 hamle fark var.
- Bonus ilk görünüş: P3 raporu bonusu Bölüm 10'a koyar; F turu commit mesajı "Y12 first level 7" diyor. Veride bonus yalnız
  Bölüm 10'da var (`level_010.json`, `bonus` ve `bonusMoves`); `mechanics.ts` içinde Y12 `firstLevel: 7`. Bu iki bilgi çelişiyor:
  L-22 sayımı için Y12 muaf tutulduğundan ilk-seviye tablosu ile bölüm verisi ayrışıyor. Net karar yok.
- Unity CI: F turundan sonra GameCI'nin son `main` koşusu bu oturumda doğrulanmadı; oynama sayfasının güncel sürümü ve ci-shots
  görüntüleri bu raporda yeniden incelenmedi.
- Bölüm 6–10 başlangıç ekranlarının bu pakette yeniden görsel incelemesi yapılmadı (ci-shots son koşusu bu oturumda indirilmedi).

## 7. Bilinen sorunlar

1. Blok sayısı 17–19 (hedef 18–22). Bölüm 6 ve 9 için 18'e çıkmak tek bir ek D2 ya da C3 gerektirir; geometri buna izin vermiyor.
2. Tutorial vurgusu hücre deseni `cell:[0-7],[0-9]` (web) — 9 sütunlu tahtada sütun 8 reddedilir (P4 notu; web genişletmesi
   bekleniyor). Bölüm 6–10 bu vurguyu kullanıyorsa yüklenmeyebilir; mevcut bölümlerde kullanılmıyor.
3. `levels:check` uzun sürer (~10 dk; Bölüm 10 ölü durum taraması).
4. Bölüm 10 Zor için alt bant (50–60 %) vekil ölçümünde 0,04 / 0,30 — kasıtlı zorluk mı, dip mi, proje sahibi kararı.
5. CI: web ve Unity koşuları yeşil olduğunda bile WebGL veri boyutu (25 MB) ve 3B çözme süresi (281–320 ms) bütçenin üstünde
   (P4 notu; yalnız yavaş yazılım-GL koşucusunda).
6. Palet bilgi kartı metni ("Katman yalnız şantiyeye bırakılır.") yalnız Unity üstyazısında (unity_tr / unity_en); web i18n'de
   karşılığı design-lead'de.

## 8. Açık sorular (proje sahibi ve ürün sorumlusu)

1. P-P3-1: Bölüm 6–10 blok sayısı 17–19 kalsın mı, yoksa min ≤ 24 tavanı (LEVELS §8.13.7 seçenek B) mı açılsın?
2. Bonus ilk görünümü: Bölüm 7 (F'nin Y12 `firstLevel: 7`) mı, yoksa Bölüm 10 (veri) mı? L-22 muafiyeti kalıcı mı?
3. D-090 ölü durum kanıtı: 17–22 bloklu bölümler için tahmin sınıfı kabul mü, yoksa makro çizge kanıtı mı beklenecek?
4. K-63 plan_trap: Ws = 3 bölümlerde hata (Kolay/Normal) mı, yalnız tek dilim içindeki tuzaklar mı?
5. Hikaye 1'de palet şekli yalnız D2_90 (K-58 madde 10, ÖNERİ). Hikaye 2'de C3/O4 paletleri için tutulabilirlik kuralı?

## 9. Oynama adresi

https://yilmazfatih-cyber.github.io/minik-usta-unity/ — `main` push'undan sonra GameCI ~8 dk. Bu rapor yazılırken Pages'in son
sürümü doğrulanmadı; "levels v3c" yazısı ve Bölüm 6–10 başlangıç ekranları için son ci-shots koşusuna bakılmalıdır.
