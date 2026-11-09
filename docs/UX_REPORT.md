# UX turu raporu (U1–U5) — Minik Usta Unity

Yazan: code-lead (paket U5, model Haiku 5.5) · Tarih: 2026-11-09 · Kaynak commit: `0523dd4` (`origin/main` HEAD, U4 sonrası)
Şartname: [`docs/UX_UNITY.md`](UX_UNITY.md) · Denetim: [`docs/review_inbox/ux-audit.md`](review_inbox/ux-audit.md) ·
Gerçek CI incelemesi: [`docs/review_inbox/ux-review.md`](review_inbox/ux-review.md) · U4 kapanışı:
[`docs/review_inbox/ux-review-closures-u4.md`](review_inbox/ux-review-closures-u4.md)

Yöntem: `docs/UX_UNITY.md` stüdyo `ux-design` / `ux-review` / `design-system` becerilerinin yöntemiyle yazıldı. Web reposu
(`minik-usta`, `wip/phase-2r`) yalnız okundu. Referans görüntüleri (`artifacts/reference/**`) bu makinede yok; kıyas
yalnız `ART_REFERENCE.md` ölçümleriyle yapıldı.

## 0. Özet

- **Durum:** P0-1 … P0-11 kodda uygulandı; ux-review'in 7 Önemli bulgusu (R-01 … R-07) U4 ile kapatıldı. Öneriler (R-08 … R-12)
  ve P1/P2 birikimi açık.
- **Yeniden inceleme yapılmadı:** AC-14 (rubrik ≥ 4/5) U4 sonrası design-lead tarafından yeniden puanlanmadı. Bu yüzden
  "UX turu kabul" denemez; kapanış koşulu aşağıda §6.
- **Yeni bulgu (U5, görüntüden):** EXPAND ve K3 birlikte, Bölüm 1'de alt bantta boşluk bırakıyor (§6, AU-1).
- **Test:** `dotnet test` ve `dotnet build -warnaserror` yeşil (§5). Görsel kanıt yalnız CI görüntüleriyle; cihazda
  dokunuş ve Unity Editor'da görme yapılmadı.

## 1. Ne değişti (commit zinciri)

| Paket | Commit | İçerik |
| ----- | ------ | ------ |
| U1 | `bc54230` | UX denetimi (`ux-audit.md`, 24 bulgu) ve `UX_UNITY.md` (K1–K6 kararları, ekran şartnameleri, bileşen kütüphanesi, öğretici, AC-1…AC-14) |
| U2 | `fb25754` | P0-1 … P0-11 uygulaması: EXPAND varsayılanı, geri tuşu yığını ve çıkış onayı, LockedHint, HUD metinleri ≥ 34 px, hedef kartı 340×176, öğretici balonu tahtanın üstünde, güçlendirici yuvası kuralı (K3), gezinme sekmesi adları, dil hapları, metin düzeltmeleri, 4 yeni CI görüntüsü |
| U2 fix | `92d1cc7` | Kazanma penceresi 45 s tutuluyor; CI "win" görüntüsü ana ekran olmuyor |
| U3 | `e3357d1` | `ux-review.md`: gerçek CI görüntülerine karşı inceleme, karar **NEEDS REVISION** (Engel 0, Önemli 7, Öneri 5) |
| U4 | `0523dd4` | R-01 … R-07 kapatıldı (aşağıda) |

### 1.1 P0 maddeleri (U2) — durum

| Madde | Konu | Durum | Kanıt |
| ----- | ---- | ----- | ----- |
| P0-1 | EXPAND varsayılan, ölü bant yok | Uygulandı, görüntüde doğrulandı | `390x844/home`, `tutorial`; `360x800/tutorial`. Test `U2_P0_1_EXPAND_is_the_default_and_phones_have_no_dead_bands` |
| P0-2 | Geri tuşu yığını, çıkış onayı (K5) | Uygulandı, test ile doğrulandı (tuş basımı CI'da yok) | `U2_P0_2_*` (5 test) |
| P0-3 | LockedHint balonu (1200 ms, 3 sallanma) | Uygulandı, test ile doğrulandı | `U2_P0_3_*` (4 test) |
| P0-4 | HUD metinleri ≥ 34 px | Uygulandı, görüntüde doğrulandı | `U2_P0_4_AC5_*` (10 bölüm × TR/EN × 3 boyut) |
| P0-5 | Hedef kartı 340×176, plan hücresi 36 px | Uygulandı, görüntüde doğrulandı | `U2_P0_5_*` |
| P0-6 | Öğretici balonu tahtanın üstünde | Uygulandı, görüntüde doğrulandı | `U2_P0_6_*`; `390x844/tutorial` |
| P0-7 | Güçlendirici yuvası kuralı (K3) | Uygulandı; U4 ile bölüm başı penceresinde de uygulandı | `U2_P0_7_*`; `U4_R05_*` |
| P0-8 | Kilitli sekme adları görünür | Uygulandı; kontrast U4 ile düzeltildi | `U2_P0_8_*`; `U4_R02_*` |
| P0-9 | Dil hapı genişliği | Uygulandı, görüntüde doğrulandı | `U2_P0_9_*`; `360x800/settings` |
| P0-10 | `tut.ctx.*` ve yeni anahtarlar | Uygulandı, test ile doğrulandı | `U2_P0_10_AC13_*` |
| P0-11 | 4 yeni CI görüntüsü | Uygulandı | `pause`, `exit_confirm`, `level_start_window`, `lose_offer` (manifest 0523dd4) |

### 1.2 Önemli bulgular (ux-review R-01 … R-07) — U4 kapanışı

| Id | Bulgu | Ne yapıldı | Test |
| -- | ----- | ---------- | ---- |
| R-01 | Oyun ekranında boş bantlar | `ScreenLayout.SceneBand` (H > 1920 ve boş bant ≥ 96 px iken; bant en fazla 420 px) — yer tutucu silüet hücreleri; yapı ilerledikçe renkli bloğa döner. **Not:** §6 AU-1'deki alt boşluk bu değişiklikle çözülmedi. | `U4_R01_*` (4) |
| R-02 | Kilitli sekme adı kontrastı 3,27:1 | Ad tam opaklık, beyaz dolgu, `#2A1D8B` kontur; yalnız ikon %55 | `U4_R02_*` |
| R-03 | "Teklif 1/3" kontrastı 2,10:1 | Sayaç yalnız 2. tekliften; krem `#FFF6D2` + kontur | `U4_R03_*` (2), `K29_*` güncellendi |
| R-04 | Açık mor zeminde beyaz metin 2,5–2,7:1 | `UiKit.PlateTint` (`#9180FF`), beyaz ≥ 5,0:1 | `U4_R04_*` (2) |
| R-05 | Bölüm başı penceresinde 30 px kilit etiketi | K3 uygulandı; etiket 34 px, `slot.Bottom + 8` | `U4_R05_*` (2) |
| R-06 | Kazanma HUD'unda mala rozeti ekran kenarına değiyor | Sağ dolgu 24 → 40 px; rozet kapsül içinde; EN "Streak" | `U4_R06_*` (2) |
| R-07 | "Çık" kırmızı, duraklat 136 px | "Çık" ikincil (krem); duraklat 144 px | `U4_R07_*` |

## 2. Önce / sonra görüntüleri

CI görüntüleri `ci-shots` dalındadır (her yeşil `main` koşusunda `ci-shots/<kısa-sha>/` ve `latest/`).

| Ekran | Önce (`8af961b`, UX öncesi) | Sonra (`0523dd4`, U4 sonrası) |
| ----- | --------------------------- | ----------------------------- |
| Öğretici 390×844 | `ci-shots/8af961b/390x844/tutorial.png` | `ci-shots/0523dd4/390x844/tutorial.png` |
| Ana sayfa 390×844 | `ci-shots/8af961b/390x844/home.png` | `ci-shots/0523dd4/390x844/home.png` |
| Bölüm başı 360×800 | `ci-shots/8af961b/360x800/level_start.png` | `ci-shots/0523dd4/360x800/level_start.png` |
| Kazanma 390×844 | `ci-shots/8af961b/390x844/win.png` | `ci-shots/0523dd4/390x844/win.png` |

Yeni görüntüler (U2 ile eklendi, yalnız `0523dd4` ve sonrasında): `pause`, `exit_confirm`, `level_start_window`, `lose_offer`
(390×844 ve 360×800). Görüntü yolu: `ci-shots/0523dd4/<boyut>/<ad>.png`.

Ölçü notu: 390×844 görüntüsü 780 px, 1 görüntü px ≈ 1,385 tasarım px (EXPAND tuvali 1080 × 2338).

## 3. Yerleşim ve kalıp kararları (K1–K6, `UX_UNITY.md` §2)

| No | Karar | Seçim |
| -- | ----- | ----- |
| K1 | Ölçek kipi | EXPAND varsayılan (FIT yalnız tablet / 1920 yükseklik) |
| K2 | Öğretici balonu | Tahtanın üstünde dock; kuyruk eldivenin x'inde |
| K3 | Kilitli güçlendirici yuvası | Yuva, kilidi açılmadan 2 bölüm önce görünür; bölüm 1–5'te yok |
| K4 | Ana ekran kilitli sekmeleri | Ad görünür, soluk; dokunuşta LockedHint |
| K5 | Geri tuşu ana ekranda | Çıkış onayı penceresi; geri hiçbir kaynağı harcamaz |
| K6 | Ölü dokunuş | 0; her düğme eylem ya da ipucu verir (100 ms içinde tepki) |

## 4. Ölçülen kabul ölçütleri (AC-1 … AC-14)

"Test" = bu turda `dotnet test` ile çalıştırılıp geçen NUnit testi; "görüntü" = `0523dd4` CI görüntüsü; "ölçülmedi" açıkça yazılır.

| AC | Ölçüt | Gerçek değer / kanıt | Durum |
| -- | ----- | -------------------- | ----- |
| AC-1 | ≤ 3 dokunuş (ilk açılış → Bölüm 1) | 1 dokunuş (U3 incelemesi, `GameApp.Taps`) | Geçti |
| AC-2 | ≤ 10 s etkileşim (cihaz) | Cihazda ölçülmedi; WebGL 21,5 MB | **Açık (P1)** |
| AC-3 | Dokunma ≥ 128 px; sık hedef ≥ 144 px | En küçük hedef 128 px (390'da 46,2 pt; 360'ta 42,7 pt, `UX_UNITY` bilinçli kabul). Duraklat 144 px (U4). Test `U2_AC3_*` geçti. Win, lose_offer, level_start_window ağacı taranmıyor (R-09) | Kısmen |
| AC-4 | Metin kontrastı ≥ 4,5:1 | Düzeltilen değerler (test): sekme adı beyaz/#6858F8 ≈ 4,8 (ikon hariç); beyaz / #9180FF tonu ≥ 5,0; krem teklif sayacı ≥ 4,5 (`U4_R02/R03/R04` geçti). **Kazanma bonus satırı 4,50 (sınırda)**; U4 sonrası görüntüden yeniden örneklenmedi | Kısmen |
| AC-5 | Hiçbir metin < 34 px | `U2_P0_4_AC5_*` geçti (HUD); `U4_R05_*` (34 px etiket). Pencere `line2` metni 32 px (bilinen, P1) | Kısmen |
| AC-6 | Birincil eylem y ≥ 1400 (FIT) / ≥ H − 520 (EXPAND), yükseklik ≥ 144 | Bu turda ölçülmedi (önceki turda P0-5 ve pencere testleri) | Ölçülmedi |
| AC-7 | Geri her ekranda tanımlı, kaynak harcamaz | `U2_P0_2_AC7_every_screen_but_boot_has_a_back_move_of_the_flow_table` ve 4 geri testi geçti | Geçti |
| AC-8 | Ölü dokunuş 0 | `U2_P0_3_AC8_every_home_button_answers_a_tap` geçti; yalnız ana ekran | Kısmen |
| AC-9 | Öğretici ≤ 2 adım, karartma α = 0 | K-53 testleri bu turda ayrı filtrelenmedi; tam paket geçti | Kısmen |
| AC-10 | Pencere ≤ 220 ms; dokunuş tepkisi ≤ 80 ms | Ölçülmedi (perf ve JUICE süre testi yok) | Ölçülmedi |
| AC-11 | Hareketi azalt: ölçek tepesi ≤ 1,03 | `U2_P0_3_reduced_motion_keeps_the_lock_still` geçti; ölçek tepesi testi bu turda ayrı yok | Kısmen |
| AC-12 | 360×800'de taşma / kırpma yok | `360x800/settings`, `tutorial` görüntüleri (U3); `level_start` (360) U5'te açılmadı; rozet kenarı U4 ile düzeltildi | Kısmen |
| AC-13 | TR/EN anahtarı tam; "evlat/kiddo/dede" yok | `U2_P0_10_AC13_*` geçti. Web kaynağı `tr.json`/`en.json` hâlâ "evlat/kiddo" içeriyor (Unity override ile kapandı) | Geçti (Unity); kaynak açık |
| AC-14 | Rubrik her boyut ≥ 4/5 | U3 puanı: oyun/öğretici 3 (R-01), bölüm başı 3 (R-05), +5 teklifi 3 (R-03), kazanma 4 (R-06). U4 sonrası yeniden puanlanmadı | **Açık** |

## 5. Doğrulama komutları (bu tur, `0523dd4` üzerinde)

| Komut | Sonuç |
| ----- | ----- |
| `cd dotnet && dotnet build -warnaserror` | 0 hata, 0 uyarı |
| `dotnet test` (tüm çözüm) | MinikUsta.Presentation.Core.Tests: 572 geçti, 0 kaldı. MinikUsta.Core.Tests: 1226 geçti, 7 atlandı (önceden atlanmış testler; bu turda değişmedi) |
| `dotnet test --filter FullyQualifiedName~U2_` | 44 geçti |
| `dotnet test --filter FullyQualifiedName~U4_` | 14 geçti |
| `dotnet test --filter FullyQualifiedName~C1_shots` | 5 geçti |
| `bash tools/stub-gate-selftest.sh` | 4 durum beklendiği gibi (PASS 2, 3, 4 + korumasız Handheld reddi) |

Konsol hatası ve GameCI "Verify" adımı bu raporda yeniden kontrol edilmedi; son yeşil koşu manifest'te
(`run 42536017348`, commit `0523dd4`).

## 6. Açık sorunlar ve kapanış koşulu

**Açık sorunlar (yeni, U5 görüntü incelemesi):**

- **AU-1 · Bölüm 1 öğretici, 390×844 · Düzen (Önemli).** `ci-shots/0523dd4/390x844/tutorial.png` görüntüsünde tahtanın altında
  yaklaşık %25 (görüntü y ≈ 1270–1688) boş; yalnız kral köşede. K3 gereği Bölüm 1'de yuva yok; alt satır boş kalıyor. Üstte
  R-01 bandı (silüet) dolu. Öneri: alt boşluğa Bölüm 1 için tek birincil iş ipucu şeridi ya da durum şeridi (`Strip`) ve
  tahtanın EXPAND'de dikey ortalanması (`UX_UNITY` §4.2). Sahip: design-lead (yerleşim) → code-lead (`LayoutSettings`).
- **AU-2 · `docs/review_inbox/ux-review-closures-u4.md`.** Başlık "CI check" bölümünü söylüyor, ama dosyada böyle bir bölüm yok.
  U4 kapanışının görsel kanıtı kayıtlı değil. Öneri: bu raporun §2 tablosu kanıt olarak kullanılsın ve closures dosyasına
  bağlantı eklensin (code-lead).
- **AU-3 · Test kapsamı.** R-09 açık: `U2_AC3` win, lose_offer, level_start_window, lose_final ağaçlarını taramıyor.

**Kapanış koşulu (UX turu "kabul" için):**

1. design-lead, `0523dd4` görüntülerini yeniden puanlar (rubrik ≥ 4/5 her boyut; AC-14). AU-1 bu puanlamadan önce kapanmalı.
2. AC-2 (cihazda ilk açılış ≤ 10 s) ve AC-10 (perf) en az bir cihaz ölçümüyle raporlanır; yoksa "ölçülmedi" olarak kalır.
3. R-09 testleri genişletilir (AC-3 kapsamı).

## 7. Kalan P1 ve P2

**P1 (ux-review R-08 … R-12 ve U2 listesi):**

- R-08: "Oyna" ve "Devam" birincil düğmeleri ~554 px; şartname ≥ 560 × 160.
- R-10: `drag_mid` karesi, sürükleme hissi (kare hedefin üstünde alındı; yeni kare gerekli).
- R-11: `exit_confirm` ve `lose_offer` pencerelerinde iki kral (büst + tam boy) aynı anda; pencere sadeleşecek.
- R-12: "Ana sayfa" (pencere) / "Ana Sayfa" (sekme); tek biçim.
- UX-07 ilerleme alt satırı; UX-12 kral portresi/pozu; UX-13 seri ilk nabzı; UX-14 manifestte parmak koordinatları;
  UX-16 `win.bonus` metni; UX-17 alt bilgi kontrastı; UX-19 Müzik satırı; UX-20 duraklat ikonları; UX-21 bölüm başı planı
  ortala; UX-22 reklam yoksa satır gizli (entrepreneur onayı); UX-23 "Teklif n/3" yalnız n ≥ 2 (S3, ürün onayı);
  pencere `line2` 32 → 34 px; `CloseButton` 136 → 144 px (UX_UNITY ile tutarsız).
- S2 (product-lead): Bölüm 1–12'de geri al yok. Öneri: bölüm başına 1 ücretsiz geri al.

**P2:** "Büyük yazı" ayarı; logo yükleme ekranına taşınması; sol el modu; yüksek kontrast; TalkBack etiketleri.

## 8. Model dağılımı (D-095 / D-098)

| Paket | Model |
| ----- | ----- |
| U1 (UX denetimi, UX_UNITY) | Opus |
| U2 (P0 uygulaması) | Opus |
| U3 (CI görüntü incelemesi) | Opus |
| U4 (R-01 … R-07 kapanışı) | Sonnet |
| U5 (bu rapor, README, doğrulama) | Haiku 5.5 |
| Orkestratör | Fable |

## 9. Oynama adresi

- Oyun (WebGL): https://yilmazfatih-cyber.github.io/minik-usta-unity/ (her `main` itişinde `Unity WebGL → Pages`, ~8 dk).
- CI görüntüleri: `ci-shots` dalı, `latest/` ve `0523dd4/`.
