# UX incelemesi — U2 uygulamasının gerçek CI görüntüleriyle doğrulanması

Yazan: design-lead (stüdyo becerisi `ux-review`, roller `ux-designer` + `accessibility-specialist`) · Tarih: 2026-11-08
Kaynak: `ci-shots/latest/manifest.json` → commit **`92d1cc7`** (= U2'nin son commit'i ve `origin/main` HEAD; bekleme
gerekmedi), koşu [42532565701](https://github.com/yilmazfatih-cyber/minik-usta-unity/actions/runs/42532565701),
headless Chromium + SwiftShader, DPR 2. Şartname: `docs/UX_UNITY.md`; önceki denetim: `docs/review_inbox/ux-audit.md`.

**Karar: NEEDS REVISION** — Engel yok; P0-1…P0-11'in tamamı görüntüde ya da testte karşılanmış. Ancak EXPAND'in
açtığı yeni boşluk, 3 kontrast açığı (AC-4), bölüm başı penceresinde 30 px kilit etiketi (AC-5) ve çıkış düğmesi
tutarsızlığı aynı turda kapanmalı (Önemli ×7). Rubrikte "düzen ve boşluk" oyun ekranında 3/5 → kabul çıtası (≥ 4) tutmuyor.

## 0. Yöntem ve sınırlar

- **İncelenen:** `latest/390x844/{home,settings,level_start,tutorial,drag_mid,landed,win,pause,exit_confirm,
  level_start_window,lose_offer}.png`, `latest/360x800/{home,settings,tutorial}.png`, `latest/fit/390x844/tutorial.png`.
- **Ölçü:** 390×844 görüntüsü 780 px → 1 görüntü px = 1,385 tasarım px; 360×800 → 1,5. EXPAND tuvali 390×844'te
  1080 × 2338, 360×800'de 1080 × 2400. 44 pt = 122 tasarım px @390, 132 @360.
- **Kontrast:** her metin kutusunda baskın zemin rengi ile en uç metin kümesi örneklendi (WCAG 2.x göreli parlaklık).
  Konturlu metinde dolgu–zemin değeri yazıldı; kontur ayrıca anıldı.
- **Dokunma hedefleri:** görüntüden göz kararı + `UiNode` ağacının tam dökümü (çalışma ağacında geçici bir NUnit sondası;
  commit edilmedi, silindi). Kod değiştirilmedi.
- **Sınırlar (dürüst):** referans görüntüleri (`artifacts/reference/**`) bu makinede yok; Royal Match kalıp kıyası
  `docs/ART_REFERENCE.md` ölçümlerinden yapıldı. CI setinde **EN görüntüsü yok** (EN yalnız testle doğrulandı).
  Back tuşu, LockedHint, metin geçersiz kılmaları (P0-2/3/10) dokunuş/klavye gerektirdiği için **görülmedi**, yalnız
  test adları ve kod okundu. Unity Editor'da, telefonda, gerçek dokunuşla hiçbir şey görülmedi.

## 1. P0 doğrulama tablosu

| Madde | Kanıt (görüntü / ölçüm) | Sonuç |
| ----- | ----------------------- | ----- |
| P0-1 EXPAND | `390x844/home,tutorial`, `360x800/tutorial`: tam ekran, karartılmış bant yok; `fit/390x844/tutorial` bantlı. | Geçti — ama yeni sorun **R-01** |
| P0-2 Back + QuitConfirm | Görüntü yok. `quit` ağacı: Kal = birincil (alt, y 1825), Çık = krem ikincil, × = Kal. | Test düzeyinde geçti |
| P0-3 LockedHint | Görüntü yok. Kilitli 4 sekme + can + altın + hedefleri `home:locked:*` eylemine bağlı (ölü dokunuş yok). | Test düzeyinde geçti |
| P0-4 HUD ≥ 34 px | `tutorial`: "Ağaç Basamakları" ~36, "Usta Serisi"/"blok"/"HAMLE" ~34 (görüntüde büyük harf yüksekliği 17–18 px ≈ 34–36 tasarım px). | Geçti |
| P0-5 GoalCard | Kart görüntüde x 18–262, y 83–208 → tasarım ~25–363 × 115–288 (340 × 176 ✓); karta HAMLE plakası arası 21 görüntü px = 29 px (≥ 24 ✓); plan hücreleri ~16 görüntü px ≈ 22–36 px ✓. | Geçti |
| P0-6 Balon | Balon gövdesi alt kenarı görüntü 632 → 875; eldiven merkezi 860–870 → 1191–1205; mesafe 316–330 px (≤ 420 ✓). Kuyruk vurgulu bloğun üstünde (x 170 → 235), eldivenin x'inde değil. Gövde tepsi çerçevesine 10 görüntü px (14 px) biniyor. | Geçti — karar **R-K1**, **R-K2** |
| P0-7 Güçlendirici yuvaları | `tutorial` (B1), `pause` (B2), `lose_offer` (B3): oyun ekranında yuva yok ✓. | Geçti — ama **R-05** (bölüm başı penceresi) |
| P0-8 Sekme adları | `home`, `360x800/settings`: Mağaza / Lig / Takım / Albüm görünür; kilit + soluk ikon. Kontrast 3,25–3,27 → **R-02**. | Kısmen |
| P0-9 Dil hapı | `360x800/settings`: "Türkçe" ve "English" tam; bayrakla çakışma yok. | Geçti |
| P0-10 Metinler | Görüntü yok (devam/destek ipucu). | Test düzeyinde geçti |
| P0-11 Yeni kareler | `pause`, `exit_confirm`, `level_start_window` (Bölüm 3), `lose_offer` iki boyda da var ✓. | Geçti |

## 2. Kabul ölçütleri (AC)

| AC | Ölçüm | Sonuç |
| -- | ----- | ----- |
| AC-1 ≤ 3 dokunuş | Ana sayfa → "Oyna · Bölüm 1" → tahta: **1 dokunuş** (B1–2 bölüm başı penceresini atlar). | ✓ |
| AC-2 ≤ 10 s | Cihazda ölçülmedi. | Doğrulanmadı |
| AC-3 ≥ 128 / sık ≥ 144 | Döküm: en küçük hedefler 128 (ayar dişlisi, can/altın kapsülü, dil hapları) ve 136 (duraklat, ×, pencere dişlisi). 128 @360 = **42,7 pt** (< 44; UX_UNITY §293'te bilerek kabul). Duraklat 136 < 144 (sık hedef) → **R-07**. Test `U2_AC3` yalnız home/settings/quit/level/pause/exit ağaçlarını tarıyor; win, lose_offer, level_start_window taranmıyor → **R-09**. | Kısmen |
| AC-4 kontrast ≥ 4,5 | Geçen: HUD lavanta #A098F8 / #181840 = 6,7; balon #184078 / #F8F8D8 = 9,5; "0/2" 17,5; dil hapı seçili 11,4 / seçili değil 7,4; çıkış gövde metni 4,77; win bonus #F8E868 / #6050E0 = **4,50 (sınırda)**. Kalan: sekme adları 3,27, "Teklif 1/3" #A098F8 / #6058E0 = **2,10**, ayar satırı beyaz / #A090F8 = 2,52 ve "Güçlendiricileri seç" beyaz / #9888F8 = 2,74 (bu ikisi koyu konturla okunuyor). | ✗ → **R-02, R-03, R-04** |
| AC-5 < 34 px yok | HUD ✓. Bölüm başı penceresindeki kilitli yuva etiketi "Bölüm 12" **30 px** (`WindowViews.cs:339`); pencere `line2` 32 px (biliniyor). | ✗ → **R-05** |
| AC-6 birincil y ≥ H − 520 | Ana sayfa Oyna üst kenarı 1801 (H − 520 = 1818, 17 px kısa); bölüm başı Oyna üst kenarı ~1752. Merkezle ölçülünce 1901 / ~1849 ✓. | Ölçüt belirsiz → **R-K3** |
| AC-7 back | Test (`U2_P0_2_*`). | Test ✓ |
| AC-8 ölü dokunuş 0 | Ana sayfadaki tüm hedefler bir eyleme ya da `locked:*` ipucuna bağlı. | ✓ (görülmedi) |
| AC-9 öğretici | Karartma yok ✓ (tahta tam parlak, `tutorial`); adım sayısı görüntüden doğrulanamaz. | ✓ / test |
| AC-12 360'ta kırpma yok | `360x800/settings`, `tutorial` ✓. Kazanma HUD'unda mala rozeti "×1" sağ kenarı tam 1080'e değiyor → **R-06**. | Kısmen |
| AC-13 TR/EN | Test. | Test ✓ |
| AC-14 rubrik ≥ 4 | §4: oyun ekranı düzen 3/5, pencereler tipografi 3/5. | ✗ |

## 3. Bulgular

### Önemli

**R-01 · Oyun ekranı (B1–5), 390×844 ve 360×800 · Düzen.** EXPAND bantları kaldırdı ama boşluğu tuvalin içine taşıdı:
HUD alt kenarı 288 ile tepsi üstü 861 arasında **~573 px**, tepsi altı 1752 ile ekran sonu 2338 arasında **~586 px**
düz koyu ızgara (ekranın %49'u). FIT'te üst boşluk ~365 px idi; artış (H − 1920) × 0,5. B1–5'te alt bantta güçlendirici
de olmadığı için kral köşesi tek başına kalıyor. Royal Match oyun ekranında (ART_REFERENCE §3, `gameGlow` 0,26 H)
tahta dikey olarak dolu bir sahnenin ortasında durur; bizde arka plan sahne değil, desen.
**Seçenekler:** (a) tahtayı üst gruba yasla (HUD + 48 px), alt bandı kral + güçlendirici + durum şeridine bırak; tahta
başparmaktan uzaklaşır (390'da tepsi altı 1752 → ~1350; zayıf). (b) Tahtayı ortada tut, üst boşluğa **yapı sahnesi**
koy: bu bölümün ürettiği yapı parçası (Ağaç Basamakları) iskelede yükselir, her doğru yerleştirmede bir kat; yükseklik
min(Boşluk − 48, 420) px, alt kenarı tepsi üstünün 24 px üstünde. (c) Yalnız arka planı zenginleştir (radyal parlama +
şantiye silüeti) — ucuz ama boşluk işlevsiz kalır.
**Seçim: (b) + (c).** Gerekçe: tahta başparmak bölgesinde kalır (AC-6 ruhu), boşluk ilerleme bilgisi taşır (hedef
kartının büyük hali), oyunun "her bölüm bir yapı parçası" vaadini görünür kılar; (c) sahne çizilene kadar yer tutucudur.
Yükseklik H ≤ 1920'de 0'a iner (FIT/tablet değişmez). **Sahip:** design-lead sahne tarifini UX_UNITY §Level'a ve
ART_UNITY'ye ekler; code-lead `ScreenLayout`'ta `SceneBand` dikdörtgenini açar.

**R-02 · Ana sayfa alt çubuğu (P0-8) · Kontrast.** Kilitli sekme adları #D8D0F0 / #6858F8 = **3,27:1** (< 4,5; 34 px
büyük metin sayılmaz). Kilitli denetimin istisnası var ama bu ad oyuncuya "ne geliyor" bilgisini veriyor.
**Düzeltme:** adı soldurma yerine dolgu #FFFFFF α 1 + 3 px #2A1D8B kontur, yalnız ikon %55 kalsın; ya da ad dolgusu
#F2EEFF (≥ 4,6:1 hedef). Kilit rozeti zaten durumu taşıyor.

**R-03 · `lose_offer` · Kontrast.** "Teklif 1/3" #A098F8 / #6058E0 = **2,10:1**. **Düzeltme:** UX-23 ile birlikte:
n ≥ 2 iken göster ve krem #FFF6D2 + #2A1D8B kontur kullan (lavanta yalnız koyu #16133C zeminde).

**R-04 · Pencere içi açık mor zemin (`settings` satırları, `level_start_window` "Güçlendiricileri seç") · Kontrast.**
Beyaz dolgu / #A090F8 = 2,52; / #9888F8 = 2,74. Koyu kontur okunurluğu kurtarıyor ama AC-4 dolgu–zemin diyor.
**Seçenekler:** (a) satır plakasını koyulaştır (#5B48F7 → beyaz 5,0:1); (b) kontur kalınlığını 0,10 em'e çıkarıp
AC-4'e "konturlu metinde kontur–zemin ≥ 3:1 ve dolgu–kontur ≥ 4,5:1" istisnası yaz. **Seçim: (a)** ayar satırları için
(çok okunan, küçük metin), (b) başlık şeritleri için (52 px+). Sahip: design-lead tokens + code-lead `panel_plate` tint.

**R-05 · `level_start_window` (Bölüm 3) · Tipografi + K3 tutarlılığı.** Üç kilitli ön-bölüm yuvası (Termos, Mala
Başlangıcı, Açık Kepenk) "Bölüm 12/16/20" etiketleri **30 px** (`WindowViews.cs:339`, AC-5 ✗) ve yuvanın alt bevel
çizgisinin üstüne biniyor (yakın planda harflerin alt yarısı koyu bantta). Ayrıca K3'ün "yuva açılmadan 2 bölüm önce
görünür" kuralı oyun içi yuvalara uygulandı ama ön-bölüm yuvalarına uygulanmadı: Bölüm 3'te 9–17 bölüm uzaktaki 3 kilit.
**Düzeltme:** aynı K3 kuralı (yuva açılış − 2'den itibaren); hiç yuva yoksa "Güçlendiricileri seç" başlığı ve sıra gizlenir,
pencere 240 px kısalır, plan resmi ortalanır (UX-21 ile birlikte). Etiket 34 px, yuvanın **altında** (y = r.Bottom + 8),
kilit ikonu yuvanın ortasında.

**R-06 · `win` (ve her HUD) · Kenar güvenliği.** Usta Serisi hapındaki mala rozeti "×1" sağ kenarı x 1080'e değiyor
(görüntü 757–780); yuvarlak köşeli ekranlarda (köşe yarıçapı ~40 pt ≈ 110 px) y ~50'deki bu rozet kesilir; safe-area
köşeleri kapsamaz. Ayrıca kazanma sırasında HAMLE plakası ile duraklat arasında beliren altın hapı ("12") plakaya 2 px
mesafede. **Düzeltme:** HUD 1. satırın sağ iç payı 24 → 40 px, rozet hapın içinde sağa yaslı (taşma 0); altın hapı
HAMLE plakasından ≥ 24 px (GoalCard kuralıyla aynı).

**R-07 · `pause` / `exit_confirm` · Tutarlılık.** Çıkış onayında "Çık" kırmızı (`btn_danger`), uygulamadan çıkış
onayında aynı karar krem ikincil. Aynı anlamlı eylem iki görsel dil. Kırmızı + yukarıda "Çık", aşağıda yeşil "Kal" yanlış
dokunuşu azaltıyor ama bizim ilke "hata affeder; kırmızı yalnız kapat ×". **Seçim:** her iki pencerede krem ikincil
(UX_UNITY P1 maddesini P0'a çek; tek satır değişiklik). Aynı turda duraklat 136 → 144 (sık hedef, AC-3).

### Öneri

**R-08 · `level_start_window`, `win` · Birincil düğme genişliği.** "Oyna" ve "Devam" ~554 px (400 görüntü px); şartname
≥ 560. 6 px; tek sabitle düzeltilir.

**R-09 · Test kapsamı (AC-3).** `U2_AC3` ağaç listesine `win`, `lose_offer`, `level_start_window`, `lose_final` eklensin;
360 genişlikte 44 pt (132 px) yalnız bilgi amaçlı uyarı olarak raporlansın (UX_UNITY §293 kabulünü değiştirmeden).

**R-10 · `drag_mid` karesi · Sürükleme hissi kanıtı.** Kare blok hedefin üstündeyken alınmış (yeşil tik ön izlemesi);
imza hareketi olan **duvar üstü vinç alanında** kaldırılmış blok (%8 büyüme, 1,2 hücre parmak ofseti, düşüş gölgesi)
CI setinde hiç görünmüyor. **Düzeltme:** `drag_crane` karesi ekle (blok vinç alanının ortasında, parmak noktası
manifest'e yazılı; UX-14).

**R-11 · `exit_confirm`, `lose_offer` · Çift kral.** Pencerede büst kral + köşede tam boy kral aynı anda; pencere
açıkken köşe kralı α 0,35'e insin ya da gizlensin (dikkat tek odakta). Gövde metni ("Henüz hamle yapmadın; can gitmez.")
sağa yaslı ve altında 180 px boşluk: metin dikeyde ortalansın, 2 satıra izin verilsin.

**R-12 · `pause` · Dil.** "Ana sayfa" (pencere) / "Ana Sayfa" (sekme) — tek biçim (`Ana Sayfa`), biliniyor.

## 4. Rubrik (ART_REFERENCE §10; 1–5, kabul ≥ 4)

| Ekran | Hacim/ışık | Renk-kontrast | Tipografi | Düzen-boşluk | Karakter/sahne | İkon-rozet |
| ----- | ---------- | ------------- | --------- | ------------ | -------------- | ---------- |
| Ana sayfa | 5 | 4 (R-02) | 4 | 5 | 5 | 4 |
| Oyun / öğretici | 4 | 4 | 4 | **3** (R-01) | 4 | 4 |
| Bölüm başı penceresi | 4 | 3 (R-04) | **3** (R-05) | 3 (R-05) | 4 | 4 |
| Duraklat / çıkış | 4 | 4 | 4 | 4 | 3 (R-11) | 4 |
| +5 teklifi | 4 | **3** (R-03) | 4 | 4 | 3 (R-11) | 4 |
| Kazanma | 5 | 4 | 4 | 4 | 5 | 4 (R-06) |
| Ayarlar | 4 | 3 (R-04) | 4 | 4 | — | 4 |

Kalıp kıyası (yalnız düzen): ana sayfa (üst kapsüller, ortada sahne + alanda büyük Oyna, 5'li alt çubukta seçili orta
sekme) ve pencereler (şerit başlık, × sağ üstte, tam genişlik seçenekler, birincil altta) referans sınıfında. Çizim ve
karakter benzerliği **yok**: Usta Kral, Tuna, Kepçe, ağaç ev ve kasaba kendi tasarımımız; rakip maskot, logo, ikon
izi bulunmadı.

## 5. Uygulayıcının sorduğu kararlar

- **R-K1 (balon vs tepsi çerçevesi):** mevcut değer kalır (gövde alt kenarı vinç alanı üstü − 16). Gövdenin tepsi
  çerçevesine ≤ 16 px binmesi kabul, kuyruğun çerçeveyi geçip vinç alanına girmesi istenen davranış (balon tahtaya
  "bağlı" okunur). Ölçüt **gövde alt kenarı → eldiven merkezi ≤ 420 px**; merkezden merkeze ölçülmez. UX_UNITY P0-6
  metni buna göre güncellenecek (design-lead).
- **R-K2 (kuyruğun x'i):** eldiven animasyonla hareket ettiği için kuyruk **vurgulu kaynak bloğun merkez x'ine** bakar
  (uygulamadaki gibi), eldivene değil. Şartname metni güncellenecek.
- **R-K3 (AC-6 referans noktası):** birincil düğmenin **merkez y'si ≥ H − 520** (EXPAND) / ≥ 1400 + h/2 (FIT). Bugünkü
  tüm birincil düğmeler bu tanımla geçiyor.
- **Yükseklik 1920'de kısalan hedef kartı:** kabul (FIT/tablet istisnası); telefon EXPAND'de her zaman 304/176.
- **Süren CI kare senaryosu (+3–4 dk):** kabul; win karesinin ana sayfaya düşmemesi daha değerli.
- **ILevelHudHost:** levels v3 programı görünümü değiştirirse bu arabirimi uygulaması şart (ipucu bağlantısı ölür);
  orkestratöre bağımlılık olarak bildirildi.

## 6. Yönlendirme

| Bulgu | Sahip | Tur |
| ----- | ----- | --- |
| R-01 sahne bandı tarifi | design-lead (UX_UNITY, ART_UNITY) → code-lead (`ScreenLayout.SceneBand`) | bu tur |
| R-02, R-03, R-04 renkler | design-lead tokens → code-lead | bu tur |
| R-05, R-06, R-07 | code-lead (`WindowViews`, `LevelHud`, `GameApp.PreLevelSlots`) | bu tur |
| R-08 … R-12 | code-lead / design-lead | P1 birikimi |
