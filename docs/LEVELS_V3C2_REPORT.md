# Bölümler v3c2 ve v3c3 — paket raporu (LEVELS_V3C2_REPORT)

Sahip: code-lead (belge) · Tarih: 2026-11-13 · Kapsam: v3c2 (D-106 palet çarpışma kuralı, D-107 blok hedefleri, D-108 palet
görünümü) ve v3c3 (Bölüm 7–9 yeniden kurulumu, öğretici ve bonus anı, şekil çeşitliliği) yayını; C# çekirdeğinin ve Unity
verisinin web ile eşitliği. Yayın sürümü: web `f13df20` (Fc), Unity `main` `834e819` (Fc).

Kararlar (web `docs/DECISIONS.md`, web commit `e8902c2`, orkestratör 2026-11-12, KABUL):
- **D-106**: sürüklenen palet katmanı kendi paletiyle çarpışmaz (K-58 madde 10); RULES_VERSION 6; `pallet_stuck` emekli.
- **D-107**: Bölüm 6 ≥ 17, 7–9 ≥ 18, 10 ≥ 20 blok; bonus Bölüm 7'de; zorluk bantları lookahead vekiliyle.
- **D-108**: "×n" beyaz daire rozet; gri-mavi metal kızak (K-65 madde 4).
- **P-Fl-1** (Bölüm 1–5 verisi değişmez; resmî vekil 2 adım kazı bakışı) ve **P-Fl-2** (7–9'da 18–19 blok + dörtlü şekil) KABUL.
- **P-P3-1** KAPANDI (D-106 ve D-107 ile çözüldü).

Bu rapor yalnız bu oturumda koşturulan komutların çıktısına, CI kayıtlarına ve commit'lere dayanır; bulunmayan her değer
"ölçülmedi" ya da "doğrulanmadı" diye işaretlidir (bkz. §6).

## 1. Durum özeti

- Yayın bölümleri: web `levels/level_001…010.json` (f13df20). Unity `Assets/MinikUsta/Resources/levels/level_001…010.json`
  dosyaları web ile bayt bayt aynı (`cmp`, 10/10, bu oturumda kontrol edildi).
- Blok sayıları: **1: 8 · 2: 8 · 3: 9 · 4: 10 · 5: 12 · 6: 17 · 7: 19 · 8: 18 · 9: 18 · 10: 20**; 6–10 toplamı 92 (v3c2 95,
  v3c 89). D-107 alt sınırları (6 ≥ 17, 7–9 ≥ 18, 10 ≥ 20) sağlanır.
- Kurallar: `RULES_VERSION` 6 (D-106; 5 ile kaydedilmiş denemeler geçersiz), `SOLVER_VERSION` 7. Unity C# çekirdeği web ile
  eşitlendi (C1 `afe949e`, Fc `834e819`).
- Görsel: palet katmanı "×n" beyaz daire rozetinde, kızak gri-mavi metal; bonus "+2" patlaması Bölüm 7'de (öğretici ve bonus
  ci-shots'ta bu pakette yeniden incelenmedi, §6).
- Kapılar (§4): Unity build 0 hata / 0 uyarı; Unity testleri 612 + 1298 geçti, 0 kaldı; stub kapısı 4/4; web `npm run check`
  yeşil; web CI `levels-nocache` (önbelleksiz `levels:check`) f13df20 üzerinde yeşil.

## 2. Paketler ve commit'ler

Model dağılımı (D-095): A1, B1, C1, R, Fl Opus · A2, C2, Fc Sonnet · orkestratör Opus · bu paket (PKG: DECISIONS ve bu rapor)
Haiku 5.5.

| Paket | Model | Web commit'leri | Unity commit'leri | Sonuç |
| --- | --- | --- | --- | --- |
| A1 (D-106 çekirdeği, pallet_stuck emekli, cell:[0-8], policy vekili games + Wilson) | Opus | `98a01c0` | — | RULES_VERSION 6; C# eşi C1'de |
| A2 (STORY 6A anahtarları, ART 3A.6, tokens check.palletV3c) | Sonnet | `e75023b`, `12715f2` | `7e17c51` | ART_UNITY Y10 şartnamesi (D-108) |
| B1 (Bölüm 6–10 yeniden kurulumu, Bölüm 4–5 boyaması, LEVELS §8.15) | Opus | `b92120a` | — | Bölüm 6, 10 ve 4–5 verisi |
| C1 (Unity: D-106 portu, C2 portu, D-108 görünümü, bonus patlaması kökü) | Opus | `70d7dc8` (yalnız docs) | `afe949e`, `55bc84a`, `4a4cc07` | DiffFuzz 853/853 yeşil (C1 kaydı) |
| C2 (NARROW_PLAN_TRAP_OPEN boş, tile_family_mix susturma, şema x 0–8, data.review işareti) | Sonnet | `2a08cd2` | — | 176/176 veri/kart testi yeşil |
| R (şüpheci inceleme v3c2; 5 Önemli, 10 Öneri) | Opus | `37e43ac` (review log) | `ff34019` | Bulgular CL-V3C2-01…11 |
| Fl (Bölüm 7–9 yeniden kurulumu, P-Fl-1 ölçümü, kapanış koşusu) | Opus | `6a83302`, `0ba4d9c` | — | v3c3 veri; önbelleksiz kapı 0 hata |
| Fc (iki adımlı kazı vekili, startOn pieceFree, EN öğretici metinleri ≤ 6 kelime) | Sonnet | `f13df20` | `834e819` | Yayın komutu |
| PKG (bu paket: P-Fl kararları ve bu rapor) | Haiku 5.5 | `e8902c2` (DECISIONS) | bu rapor (aşağıdaki commit) | D-106…D-108, P-Fl-1/2 KABUL |

## 3. Bölüm tablosu (yayın verisi, web f13df20)

Kaynak: web `levels/level_001…010.json`, golden'lar (`tests/golden/level_NNN.solver.json`: `min`, `yao.overWall`), LEVELS §8.15
(6, 10), §8.16.2 (7–9). `min`: çözücünün en iyi bulduğu hamle (parantez: alt sınır, kanıtlı değilse). `pay` = P + a; hamle =
min + pay. YAO = `overWall` / blok sayısı; 1–10'un hepsinde 1,00 (golden'lardan). Vekil: ileri bakış vekili, ε 0,1, [Wilson %95]
— ölçülmemiş hücre "—" ile işaretli.

| # | Ad | Zorluk · etiket | Saha / şantiye / duvar | Σ / F / E | Blok (domino) | Şekiller | Palet | Özel bloklar | min (alt) | pay · hamle | YAO | Vekil (ε 0,1) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | Ağaç Basamakları | Kolay · S1 | 5×4 / 2×4 / 3 | — | 8 | — | — | — | 8 (8) | — · 12 | 1,00 | — |
| 2 | Köşe Destekleri | Kolay · — | 6×5 / 2×6 / 5 | — | 8 | — | — | — | 8 (8) | — · 12 | 1,00 | — |
| 3 | Köşe Taşları | Kolay · — | 5×5 / 2×5 / 4 | — | 9 | — | — | — | 11 (11) | — · 15 | 1,00 | 0,93 (2 adım) |
| 4 | Ön Duvar | Kolay · — | 6×5 / 2×4 / 4 | — | 10 | — | — | — | 12 (12) | — · 16 | 1,00 | 0,89 (2 adım) |
| 5 | Renkli Pencere | Normal · Y9 | 6×6 / 2×5 / 4 | — | 12 | — | — | iki renkli | 14 (14) | — · 18 | 1,00 | 0,97 (2 adım) |
| 6 | Geniş Temel | Normal · S9 | 6×8 / 3×5×3 / 7 | 45 / 45 / 3 | 17 (8) | C3 ×7, D2 ×8, L4, T4 | — | iki renkli ×2 | 19 (18) | 4 + 0 · 23 | 1,00 | 0,83 [0,79–0,86] (§8.15) |
| 7 | Palet Yığını | Normal · Y10 | 6×8 / 3×6×3 / 7 | 54 / 42 / 6 | 19 (9) | D2 ×9, C3 ×4, L4 ×2 | `O4_0` Y ×4 | bonus `D2_0` W | 20 (20) | 4 + 0 · 24 | 1,00 | 0,86 [0,83–0,89] |
| 8 | Paketli Kat | Normal · Y11 | 6×8 / 3×4×4 / 7 | 48 / 42 / 6 | 18 (7) | D2 ×7, C3 ×4, I3 ×3, L4 | `C3_0` W ×3 | makas `D2_90` Y; sargılı `C3_270` G, `I3_90` W; iki renkli `D2_0` GW ×2 | 20 (19) | 4 + 2 · 26 | 1,00 | 0,87 [0,83–0,90] |
| 9 | Kaygan Çatı | Normal · Y13 | 6×8 / 3×4×4 / 7 | 48 / 42 / 6 | 18 (7) | D2 ×7, C3 ×5, I3 ×2, L4 | `C3_270` G ×3 | kaygan `D2_0` W ×2; iki renkli `D2_0` WY, `D2_90` YW | 20 (19) | 4 + 1 · 25 | 1,00 | 0,83 [0,79–0,86] |
| 10 | Ağaç Ev Tamam! | Zor · — | 6×8 / 3×6×3 / 6 | 54 / 45 / 3 | 20 (7) | C3 ×7, D2 ×7, I3, L4 | `C3_0` W ×4 | makas `D2_0` G; kaygan `D2_0` G; sargılı `D2_0` G, `I3_90` G; iki renkli C3 ×3 | 21 (21) | 3 + 0 · 24 | 1,00 | 0,51 (kapı, yeni vekil); 0,54 [0,49–0,59] (§8.15) |

Notlar:
1. Bölüm 1–5 2 sütunlu şantiyede kalır (D-103); 1–5 şekil ayrıntısı ve kanonik çözüm LEVELS §2 kartlarında. Bölüm 4 ve 5
   v3c2'de boyandı (CL-V3C-03; yerleşim ve şekiller aynı, Bölüm 4'ün `eventLogHash` değeri `e4f5bf2770578c87` olarak değişti; Bölüm 5 `aa2b8f84c7abebf9` aynı kaldı).
2. Bölüm 6–10 "saha / şantiye / duvar" sütunu web LEVELS §8.15–§8.16 notasyonunu aynen taşır.
3. Bölüm 8–9: `min` kanıtsız (en iyi bulunan 20, alt sınır 19). Bölüm 7 ve 10'da `min` alt sınıra eşit (kanıtlı).
4. Ölü masa resmî tahmini (10 000 yürüyüş, D-090): 7 %0,19, 8 %0,02, 9 %0,11 (kapı ≤ %1); 6 ve 10 için 0 (§8.15). Bu tahmin
   sınıfıdır, tam kanıt değildir.
5. Palet ve bonus: palet yalnız şantiyeye bırakılır (D-104); palet katmanı kendi paletinin üstünde yanal kayabilir (D-106).
   Bölüm 7'de bonus D-107 gereği ilk palet katmanından sonra; Bölüm 10'da bonus yok.

**Öğretici anahtarları (`teaches` ve `tutorial`):** 6 `tut.m.widesite`; 7 `tut.m.pallet` ve `tut.m.palletsite` (iki adım,
adım 1 `startOn: pieceFree`, Fc); 8 `tut.m.wrap`; 9 `tut.m.slippery`; 10 `tut.m.cranebooster`. EN metinleri 6 kelimeyi aşmaz
(Fc `f13df20`, web `en.json`).

## 4. Kapılar ve test sayıları (bu oturumda koşturuldu)

| Kapı | Komut | Sonuç |
| --- | --- | --- |
| Unity derleme | `cd dotnet && dotnet build -warnaserror` (worktree, `origin/main` 834e819) | 0 uyarı, 0 hata |
| Unity testleri | `cd dotnet && dotnet test` | Presentation.Core.Tests: 612 geçti, 0 kaldı · MinikUsta.Core.Tests: 1298 geçti, 8 atlandı, 0 kaldı (toplam 1306 kayıt) |
| Stub kapısı | `bash tools/stub-gate-selftest.sh` | "all 4 cases as expected", çıkış 0 |
| Web kapısı | `npm run check` (web worktree, `e8902c2`; f13df20 + yalnız DECISIONS.md) | Çıkış 0; 123 dosya, 2208 geçti, 83 atlandı (2291); rule-coverage 88 kimlik, 1685 test |
| Web bölüm kapısı (önbelleksiz) | `node tools/solve.ts --check --no-cache --jobs 4` (CI `levels-nocache`, web f13df20) | CI işi **success**, 17 dk 27 sn (run 43333723644). Yerel koşu yapılmadı (§6). |

Not: web `levels:check` kanıtı yalnız `--no-cache` koşusuna dayanır (CLAUDE.md ve ekip kuralı). Fl kapanış koşusu (`0ba4d9c`,
1–10, 10 dk 49 sn, 0 hata, 0 uyarı) Fc'den önceydi; Fc `walks.ts` ve `tutorial.ts` dosyalarını değiştirdiği için kanıt olarak
Fc'nin kendi CI koşusu (`f13df20`) kullanılmıştır.

## 5. CI ve yayın kayıtları

| Repo | Commit | İş | Sonuç | Süre / not |
| --- | --- | --- | --- | --- |
| web | `f13df20` | CI (`check`) | success | 7 dk 35 sn |
| web | `f13df20` | CI (`levels-nocache`) | success | 17 dk 27 sn (sınır 40 dk) |
| web | `e8902c2` | CI (yalnız DECISIONS.md) | başlatıldı, bu rapor yazılırken çalışıyor (run 43337689244) | doğrulanmadı |
| Unity | `834e819` | Unity WebGL → Pages (GameCI) | success | 27 dk 8 sn (run 43333734965) |

Pages: `main` push'unda GameCI WebGL derlemesini yayınlar; bu koşu "success" olarak bitti. Yayınlanan sayfa tarayıcıda açılıp
bölüm 6–10 ekranları bu pakette incelenmedi (§6).

## 6. Doğrulanmayanlar

- **Yerel önbelleksiz `levels:check`** bu pakette koşmadı; kanıt CI `levels-nocache` işidir (§4–5).
- **Vekil sürümleri karışık.** 1–5 değerleri Fl'nin 2 adım kazı vekilinden (REVIEW_LOG, Fl kapanışı: 3 0,93, 4 0,89, 5 0,97);
  6 ve 10 değerleri B1 (§8.15) ve Fc öncesi kapı ölçümünden (10 için 0,51); 7–9 değerleri Fl kapanışından (§8.16.2). Bölüm 6 ve
  10 için 2 adımlı vekil tek değer olarak yeniden ölçülmedi; Bölüm 1–2 vekili ölçülmedi ("—").
- **Bölüm 8–9 `min`** kanıtsız: en iyi bulunan 20, alt sınır 19. Daha kısa çözüm bulunursa bütçe oyuncu lehine 1 bol olur.
- **Bölüm 3–5 `targets.policyWin` bantları** yazılmadı (P-Fl-1 adım 3, product-lead). Kapıda bu bölümler görünmez.
- **Ölü masa:** 6–10 için tahmin (10 000 yürüyüş); güçlü yoklamada 7–9'un üç ucu da çözülebilir, ama tam çıkmaz kanıtı yok.
- **Görsel:** v3c2/v3c3 ekranlarının ci-shots görüntüleri (`v3_pallet_7`, `v3_bonus_burst_7`, `v3_final_10`) bu pakette indirilip
  incelenmedi. Bonus patlamasının donma kökü C1'de kapatıldı, son CI görüntüsü doğrulanmadı.
- **D-108 giriş nabzı** (Bölüm 7, 2 × 400 ms, ölçek 1,06) Unity'de C1'de yapılmadı; bu pakette de yapılmadı (§7).
- **Fc'nin Unity EN öğretici geçici metinleri** kaldırma commit mesajına göre yapıldı; bu rapor EN dosyasını satır satır
  karşılaştırmadı.
- **Web'de EN kelime sayısı testi** yok (C1 önerisi); "≤ 6 kelime" kuralı yalnız elle doğrulandı.
- **Pages'in güncel içeriği** tarayıcıda açılmadı.

## 7. Bilinen sorunlar

1. Bölüm 8–9 `min` kanıtsız (§6); bölüm verisi D-107 alt sınırlarında (18 blok).
2. "×n" palet rozeti bazen sol komşu bloğun ya da kendi hücresindeki sembolün bir kısmının üstüne biniyor (C1 gözlemi, Bölüm 9
   ve 10). Aynı bloğun diğer hücrelerinde sembol görünür kalıyor. Rozet çakışması D-108 kabul ölçütünü tam karşılamıyor.
3. 360 px'te katman bantları 6 design px (2 CSS px adım) ince; katman derinliği rozetten okunuyor (C1).
4. D-108 giriş nabzı (Bölüm 7) yapılmadı (§6).
5. 3B sayfa çözme süresi: 278 / 261 ms (bütçe 250 ms; CL-V3C-17, yazılım GL). İlk koşuda yoktu. Ölçüm ci-shots'ta (C1).
6. Bölüm 4 Kolay: 1 adımlı vekilde ε 0'da 0,56 (ε 0,1'de 0,46) idi; 2 adımlı vekilde 0,89 (Fl). Eğrinin Kolay bandı için 3–5 `policyWin`
   bandı henüz yazılmadı (P-Fl-1 adım 3).
7. Web bundle boyutu: ilk yükleme 614,3 / 620 KB (A1 ölçümü; bütçeye 5,7 KB kaldı). Bu paketten sonra ölçülmedi.
8. `levels-nocache` CI işi ~17,5 dk; B1 yerel ölçümü ~33 dk idi (4 iş, Bölüm 10 ~16 dk). CI'da 40 dk sınırının %44'ü.
9. Tutorial vurgu deseni (`cell:[0-8],[0-9]`) C# ve web'de eşit (C1, A1); ilk sürümde sütun 8 reddediliyordu, düzeltildi.

## 8. Açık sorular (proje sahibi ve ürün sorumlusu; ayrı tutulur)

Proje sahibi:
1. **Bonus patlaması donması:** P4 (`f7a8043`) sürümünden beri Unity derlemesinde gerçek oyuncuyu etkiliyordu; kök neden C1'de
   kapatıldı. Bu hata proje sahibine ayrıca bildirilsin mi?
2. **Bölüm 1–5 değişmez** (P-Fl-1 kabul edildi): sahibin "Bölüm 1–5 değişmez" notu korunuyor; bu karar sahibe onayla bildirilmeli mi?
3. **"Kat 1 bitince açık palet"** (B1 soru 2): 6×8 sahada üstü başta açık palet kurulamadı; Bölüm 7 öğreticisi bu düzenle
   çalışır. Kabul mü?
4. **Hikaye 1'de 20+ blok** için yüksek saha (B1 soru 3) Hikaye 2'ye mi kalsın? D-107 Bölüm 7–9 hedefini 19–20 olarak yazar.
5. **CI `levels-nocache` kapısı** (~17,5 dk; B1 soru 4 "33 dk" idi): kabul mü?
6. **D-090 tahmin sınıfı** 17–20 bloklu bölümler için kabul mü, yoksa makro çizge kanıtı mı beklenecek?
7. **Bölüm 10 Zor bandı** (vekil 0,51, bant 0,50–0,60): kasıtlı dip mi?

Ürün sorumlusu (product-lead):
- Bölüm 3–5 `targets.policyWin` bantlarını yazmak (P-Fl-1 adım 3).
- Bölüm 8–9 `min` için daha kısa çözüm arayışı ya da kanıt kabul kararı.
- LEVELS §2 kartlarının v3c3 verisi ile eşitliği (Fl bunları üretti; bu pakette yeniden karşılaştırılmadı).

## 9. Oynama adresi

https://yilmazfatih-cyber.github.io/minik-usta-unity/ — `main` `834e819` push'u GameCI ile yayınlandı (run 43333734965, success).
Yayının içeriği bu pakette tarayıcıda doğrulanmadı.
