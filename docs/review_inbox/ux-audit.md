# UX denetimi — Unity yapısı, gerçek CI ekran görüntüleri (paket U1)

Yazan: design-lead (stüdyo rolü `ux-designer` + `accessibility-specialist`; yöntem `ux-review` ölçütleri) · Tarih: 2026-11-08
Kaynak koşu: `ci-shots/latest/manifest.json` → commit `8af961b` (= `origin/main` HEAD, güncel), koşu
[41409259471](https://github.com/yilmazfatih-cyber/minik-usta-unity/actions/runs/41409259471), headless Chromium + SwiftShader, DPR 2.
Şartname: `docs/UX_UNITY.md` (bu denetimin düzeltmeleri oradaki P0/P1/P2 listesine bağlanır).

## 0. Kapsam, yöntem, sınırlar

**İncelenen görüntüler** (ci-shots dalı, `latest/`): `390x844/{home,settings,level_start,tutorial,drag_mid,landed,win}.png`,
`360x800/{home,tutorial}.png`, `expand/390x844/tutorial.png`; yerel önizlemeler `artifacts/preview/v5-lose-390x844.png`,
`v4-unity-pause.png`, `v4-unity-levelstart.png` (eski yapı; yalnız pencere düzeni için kanıt).

**Ölçü:** 1080 tasarım px = ekran genişliği. 390×844 görüntüsü 780 px genişlikte → 1 görüntü px = 1,385 tasarım px;
FIT'te tuval görüntüde y 150–1537 arasındadır (tasarım y = (görüntü y − 150) × 1,385). 44 pt = 122 px @390, 132 px @360.

**Ölçütler (ux-review §3A uyarlaması):** ilk 5 saniye okunurluğu · bilgi hiyerarşisi · dokunma hedefi (≥ 128 px, sık
≥ 144 px; UX_FLOWS §0.1) · başparmak bölgesi (birincil eylem y ≥ 1400, alt %40) · geri/çıkış · hata affediciliği ·
durum görünürlüğü · öğretici K-53 · pencereler · ana ekran · erişilebilirlik (metin ≥ 4,5:1, renk + simge, hareketi
azalt, yazı boyu ≥ 34 px) · metin kalitesi (TR/EN, "dede" kalıntısı) · geçiş/bekleme.

**Önem:** **Engel** = bir oyuncu akışı kırık ya da erişilebilirlik kuralı açıkça çiğneniyor; **Önemli** = aynı tur
kapanmalı; **Öneri** = P1/P2 birikimi.

**Sınırlar (dürüst):** referans görüntüleri (`/home/user/minik-usta/artifacts/reference/**`) bu makinede yok (dizin
bulunamadı); kalıp kıyası `docs/ART_REFERENCE.md` ölçümlerinden yapıldı. Kaybetme, duraklat, çıkış onayı ve bölüm başı
pencereleri CI setinde yok (`level_start` karesi Bölüm 1'dir ve Bölüm 1–2 pencereyi atlar); bunlar için yalnız eski
önizlemeler ve kod okundu. Telefonda, Unity Editor'da ve gerçek dokunuşla hiçbir şey görülmedi.

## 1. Özet karar (ux-review biçimi)

| Ekran | Karar | Ana sorun |
|---|---|---|
| Ana ekran | NEEDS REVISION | Kilitli sekme/“+” dokunuşu ölü (geri bildirim yok); %18 ölü bant |
| Oyun ekranı (HUD) | NEEDS REVISION | FIT'te 151 pt ölü bant + tahta üstünde ~390 px boş şerit; 28–30 px metinler; Bölüm 1'de 4 kilitli güçlendirici |
| Öğretici (Bölüm 1) | NEEDS REVISION | Balon ile el arasında 330–400 px göz yolu; kilitli güçlendirici dokunuşu sessiz |
| Sürükleme / iniş | APPROVED (koşullu) | Gölge ✓ ve hedef vurgusu okunuyor; parmak ofseti görüntüde kanıtlanamıyor |
| Kazanma | APPROVED (öneriyle) | "kalan 4 hamle" ↔ HUD "0" çelişkisi |
| Ayarlar | NEEDS REVISION | "Türkçe" düğme etiketi kırpılıyor; × ve Tamam aynı işi yapıyor (kabul) |
| Duraklat / çıkış onayı | APPROVED (kod) | Çıkış onayı var, ücretsiz çıkış ayrımı doğru; Android geri tuşu yok |
| Kaybetme + +5 | APPROVED (öneriyle) | Pasif "Reklam yok" düğmesi gürültü |
| Erişilebilirlik | NEEDS REVISION | Yazı boyu ayarı yok; 6 HUD metni < 34 px; geri tuşu ve güvenli alan yok |

Toplam: **Engel 0 · Önemli 10 · Öneri 14.** Hiçbir bulgu oyunu oynanamaz kılmıyor; Önemli olanlar UX_UNITY §12 P0'dır.

## 2. Bulgular

### 2.1 Bütün ekranlar

**UX-01 · Önemli · tüm ekranlar · FIT'te üst/alt ölü bant.**
Kanıt: `390x844/*.png` görüntü y 0–150 ve 1537–1688 düz #1E1A4A bant (her biri 75 pt; ekranın %18'i), `360x800/*.png` %20.
`expand/390x844/tutorial.png` aynı içeriği tam ekranda gösteriyor ve güçlendiriciler alt kenara iniyor (y 1648 → 2065
tasarım, başparmağa daha yakın). Neden önemli: casual referans sınıfında (RM/Block Out) kenar bandı yok; bant ilk 5
saniyede "port edilmiş web oyunu" hissi veriyor ve alt grubun 75 pt yukarıda kalması başparmak menzilini kısaltıyor.
Düzeltme: `LayoutSettings.Mode = Expand` varsayılan (web D-015'te kabul edilmişti, CI.md §FIT/EXPAND "karar
orkestratörün"); ana sayfa arka planı EXPAND'de ilk/son satır germe kuralıyla dolar (UX_FLOWS §0.1). Karar seçenekleri
UX_UNITY §2.1 K1.

**UX-02 · Önemli · tüm ekranlar (Android) · Geri tuşu ve güvenli alan yok.** Kanıt: `Assets/MinikUsta/**` içinde
`KeyCode.Escape` / geri tuşu işleyicisi yok; REVIEW_PHASE4B P4B-17 açık. Düzeltme: UX_UNITY §6.4 geri yığını
(en üst pencere kapanır → bölümde Duraklat → ana ekranda hiçbir şey ya da çıkış onayı); `Screen.safeArea` içe çekme.

**UX-03 · Önemli · erişilebilirlik · 34 px altı metinler.** Kanıt (`LevelHud.cs` + `390x844/tutorial.png`):
panorama adı "Ağaç Basamakları" 28 px (görüntüde ~10 px yükseklik; 375 pt'de ~9,7 pt), Usta Serisi etiketi 30,
"HAMLE" bandı 30, Altın Mala adedi 28, segment sayacı 30. UX_FLOWS §14 alt sınırı 34 px (`font.size.caption`).
Düzeltme: hepsi ≥ 34 px; panorama adı 36 px + 1 satıra sığmazsa kısaltma yerine 2 satır (UX_UNITY §4.2).

**UX-04 · Önemli · tüm ekranlar · ölü dokunuşlar.** Kanıt: `HomeView.OnUiTap` yalnız Oyna/Ayarlar/Kral'ı işler; kilitli
4 sekme, altın "+" ve can kapsülü dokunuşa hiçbir tepki vermez (`home.png`). `LevelHud.BoosterTapped` hiçbir yerde
bağlanmamış → Bölüm 1'deki 4 kilitli güçlendiriciye dokunmak sessiz (`tutorial.png`). Tasarım ilkesi "her dokunuş
ödüllendirilir" ve UX_FLOWS §0.3 "Kilitli öğe / Kilitli sekme" kalıbı (kilit sallanır + 1,2 s `common.unlockAt` /
`common.comingSoon` balonu, JUICE #73) çiğneniyor. Düzeltme: UX_UNITY §5.9 `LockedHint` bileşeni, P0.

**UX-05 · Öneri · tüm ekranlar · yazı boyu ayarı yok.** Ayarlar'da Ses/Titreşim/Hareketi azalt/Dil var; yazı boyu yok.
Düzeltme: P2 "Büyük yazı" (×1,15; yalnız gövde/etiket metinleri; HUD sayaçları zaten ≥ 44 px).

### 2.2 Ana ekran (`390x844/home.png`, `360x800/home.png`)

İlk 5 saniye: logo, "Ağaç Ev" kurdelesi, 0/10 çubuğu, Usta Kral + Tuna + Kepçe, yeşil "Oyna · Bölüm 1". Okunuyor; birincil
eylem tek ve belirgin. **Oyna: tasarım (210, 1384) 660×200** → y ≥ 1400 kuralı sağlanıyor, 200 px ≥ 144 ✓.
İlk açılıştan Bölüm 1'e **1 dokunuş** (Boot → Home → Oyna; Bölüm 1–2 pencere atlar) ✓ (≤ 3).

**UX-06 · Önemli · ana ekran · ilk açılışta 4/5 sekme kilitli duvar.** Kanıt: alt çubukta Ana Sayfa dışındaki 4 sekmenin
ikonu asma kilitli ve adsız; Bölüm 1'den önce oyuncu 4 kilit görüyor. Kilit kendi başına kabul (UX_FLOWS §2.2
madde 11) ama dokunuş ölü (UX-04) ve sekme adları yok → oyuncu neyin kilitli olduğunu bilmiyor. Düzeltme: kilitli
sekmede ikon %55 + kilit + **soluk sekme adı** (34 px), dokununca `LockedHint` "Yakında". P0 (adlar + ipucu).

**UX-07 · Öneri · ana ekran · ilerleme hissi.** "0/10" yıldız çubuğu var ama neyin 10 olduğu söylenmiyor; yıldız simgesi
çubuğun sağ ucunda hedef gibi duruyor (iyi). Düzeltme (P1): çubuk altında 34 px "Ağaç Ev için 10 ★" satırı, ilk
kazanmadan sonra ★ uçuşu bu çubuğa iner (JUICE #74).

**UX-08 · Öneri · ana ekran · logo payı.** Logo 2 satır, tasarım y ~300–420 alanı; RM kalıbında ana ekran üstü
etkinlik/ilerleme taşır, logo yükleme ekranındadır. Düzeltme (P2): logo yükleme ekranına, ana ekranda o alan "Ağaç Ev"
yapı kartına (ilerleme + sonraki kat önizlemesi) geçer.

### 2.3 Oyun ekranı ve HUD (`tutorial.png`, `drag_mid.png`, `landed.png`, `360x800/tutorial.png`)

Hiyerarşi (göz yolu ölçümü): 1) HAMLE 10 plakası (en büyük sayı, krem üstünde lacivert, 9,4:1) ✓ 2) tahta ✓ 3) öğretici
balonu ✓ 4) "6 blok" çipi 5) hedef kartı (çok küçük) 6) Usta Serisi. Durum görünürlüğü iyi: hamle, kalan blok,
plan hücreleri (kesik kenarlı hedef + sembol), sıradaki satır beyaz çerçeveli (`tutorial.png` şantiye alt satırı).

**UX-09 · Önemli · oyun ekranı · tahta üstünde boş şerit.** Kanıt: `drag_mid.png` HUD altı (tasarım y 264) ile vinç
alanı üstü (tasarım y ≈ 651) arası ~390 px boş; balon varken bile ~240 px boş. Aynı anda hedef kartı ("Ağaç
Basamakları", plan küçük resmi ~40 px hücre) en önemli ikinci bilgi olmasına karşın en küçük öğe. Düzeltme: UX_UNITY §4.2
— hedef kartı 300×136 → 420×176, plan küçük resmi hücre 28 → 36 px; balon yuvası bu şeride iner (UX-11). EXPAND'de
şerit tahta kaymasıyla (expandShare 0,5) zaten bölünür.

**UX-10 · Önemli · oyun ekranı (Bölüm 1–7) · 4 kilitli güçlendirici başparmak bölgesini işgal ediyor.** Kanıt:
`tutorial.png` alt şerit tasarım y 1648–1808: Çekiç, Vinç, Fırça, Geri Al hepsi kilitli (ilk açılış Bölüm 8, Geri Al 13;
`config/economy.json`). FTUE'de oyuncunun ilk gördüğü 4 düğme kullanılamaz; "Geri Al" kilidi ayrıca "hatanı geri
alamazsın" mesajı veriyor. Düzeltme: UX_UNITY §2.1 K3 — bir yuva açılış bölümünden **2 bölüm önce** görünür (önce
gizli), kilitliyken altında "Bölüm 8" etiketi; dokunuşta `LockedHint`. Bölüm 1–5'te alt şeritte yalnız Kral köşesi.
Ürün sorusu (product-lead'e): Bölüm 1–12'de hata affediciliği için ücretsiz tek Geri Al — UX_UNITY §13 S2.

**UX-11 · Önemli · öğretici · balon el ile uzak.** Kanıt: `tutorial.png` balon tasarım y ≈ 300–420 (üst dok), eldiven
y ≈ 940–1020 → göz yolu ~560 px; `360x800/tutorial.png` aynı. K-53 "el + kısa balon" sağlanıyor (karartma yok, girdi
serbest ✓) ama iki öğe ayrı yerde okunuyor. Düzeltme: balon **tahta üstü dokuna** (vinç alanı üstü − 16 px) taşınır,
kuyruk eldivenin x'ine bakar (UX_UNITY §7.2). Üst dok yalnız hedef paneli vurgusunda (Bölüm 1 adım 2, 2 adım 1).

**UX-12 · Öneri · oyun ekranı · Kral iki kez görünüyor.** Balonun portresi Usta Kral, sol alt köşe de Usta Kral
(`tutorial.png`). Kabul edilebilir (konuşan = köşedeki), ama göz iki yere bölünüyor. Düzeltme (P1): balon açıkken
köşedeki Kral "konuşma" pozuna geçer (KingCue.Talk) ve portre küçülür (128 → 96) ya da kalkar.

**UX-13 · Öneri · oyun ekranı · Usta Serisi FTUE'de erken.** "Usta Serisi ○○○○ + mala" Bölüm 1'de görünür ama
anlatılmaz (`tut.ctx.streak` bağlamsal). İlk doğru yerleşimde boncuk doluyor (`landed.png`) — ödül anı olarak iyi.
Düzeltme (P1): ilk boncuk dolduğunda kapsül 1 nabız + bağlamsal satır kuyruğa (K-53 madde 4).

**UX-14 · Öneri · sürükleme · kanıt eksik.** `drag_mid.png` bloğu şantiyede ✓ gölgeyle gösteriyor ama parmak yok;
1,2 hücre parmak ofseti ve %8 büyüme görüntüden doğrulanamıyor. Düzeltme (code-lead, P1): `drag_mid` karesine parmak
noktası işareti (yalnız ekran görüntüsü kipinde) ya da manifest'e `finger`/`piece` koordinatları.

**UX-15 · Öneri · iniş · "doğru" anının görünürlüğü.** `landed.png` hamle 10 → 9, blok 6 → 5, boncuk 1 dolu, hedef
hücreler doluyor ✓. Hedef kartındaki plan küçük resmi de güncelleniyor ama 28 px hücrede fark edilmiyor (UX-09).

### 2.4 Kazanma (`390x844/win.png`)

Karartma + ışın yelpazesi + "Kazandın!" altın kurdele + büyük yıldız + Kral sevinç + yapı kartı + +42 altın / +1 ★ +
yeşil "Devam" (tasarım y ≈ 1418–1580, ≥ 1400 ✓). Katmanlı ödül anı tam (RM kalıbı: başlık → nesne → ödül satırı → tek CTA).

**UX-16 · Öneri · kazanma · "kalan 4 hamle" ↔ HUD 0.** Arkadaki HUD HAMLE 0 gösteriyor (Bonus İnşaat hamleleri harcadı),
satır "kalan 4 hamle" diyor. Düzeltme (P1): metin `win.bonus` "Bonus İnşaat · 4 hamle → +{coin}" (hamlenin nereye gittiği
söylenir) ya da Bonus İnşaat sırasında HUD sayacı geri sayarken altın çipi aynı anda artar (JUICE #56 zaten var).

**UX-17 · Öneri · kazanma · iki satır alt bilgi 34 px sınırında ve 4,27:1.** "Bonus İnşaat…" ve "Altın Mala ×1" sarı
#FFE96A dolgu, mor #6557E1 zemin = 4,27:1 (yalnız dolgu; koyu kontur var). Düzeltme: kontur ≥ 0,08 em (#2C1F8E) şart
ya da krem #FFF6E3 dolgu (≥ 6:1).

### 2.5 Ayarlar (`390x844/settings.png`)

4 satır (Ses, Titreşim, Hareketi azalt, Dil) + "Tamam" + kırmızı ×. Anahtarlar 128 px hedef ✓; "Hareketi azalt" var ✓.

**UX-18 · Önemli · ayarlar · "Türkçe" etiketi kırpılıyor.** Kanıt: dil satırında seçili mavi hap içinde "Türkçe"nin
"e"si kenara değiyor (görüntü x ≈ 505, hap sağ kenarı 515). EN'de "English" aynı genişlikte sığıyor. Düzeltme: dil
hapı 300 → 340 px ya da bayrak 72 → 60 px; etiket alanı ≥ metin genişliği + 2 × 16 px (UX_UNITY §5.4 `SegmentedPill`).

**UX-19 · Öneri · ayarlar · eksik satırlar.** Müzik (`settings.music` anahtarı var), Geri bildirim, Zaman baskısını azalt
(G-H bölümleri gelince) yok. Düzeltme (P1): Müzik; (P2) diğerleri bölüm içeriği gelince.

### 2.6 Duraklat, çıkış onayı, bölüm başı, kaybetme (kod + eski önizlemeler)

- **Duraklat** (`v4-unity-pause.png`): "Mola" + Ana sayfa / Bölümü baştan başlat / Devam (yeşil en altta, y ≥ 1400 ✓),
  dişli panel köşesinde ✓. **Çıkış onayı** (`GameApp.OpenExitConfirm`): "Bölümden çık?" + "Henüz hamle yapmadın; can
  gitmez." ya da "Çıkarsan 1 can gider." (+ seri satırı), Kal (birincil) / Çık. Hata affediciliği ✓ (onaysız kayıp yok).
- **UX-20 · Öneri · duraklat · "Ana sayfa" ve "Baştan başlat" aynı krem.** İkisi de onaya gider; doğru. Öneri (P1):
  "Baştan başlat" ikonu (↻) ve "Ana sayfa" ikonu (ev) eklenir — renk dışı ayırt edici.
- **Bölüm başı** (`v4-unity-levelstart.png`, Bölüm 3+): ad + plan + ×5 + "13 hamle" + kilitli güçlendirici yuvaları
  "Bölüm 12/16/20" + Oyna (y ≈ 1340–1530) ✓. **UX-21 · Öneri:** plan küçük resmi sol kenara yapışık ve hedef sayısı
  ×5 ikonu ile hamle hapı arasında 30 px; P1'de plan kartın ortasına, sayaçlar altına.
- **Kaybetme** (`v5-lose-390x844.png`): "Hamleler bitti!" + Tuna düşünceli + "Kalan: 4 blok" + Teklif 1/3 + "+5 hamle
  900 (≈ ₺81)" turuncu / "Reklam izle · +5 hamle — Şu an reklam yok" gri / "Hayır, teşekkürler" krem; × var. R-15 eşit
  boy ✓, geri sayım yok ✓, suçlayıcı dil yok ✓ (K-29).
  **UX-22 · Öneri:** reklam yokken gri pasif düğme göstermek yerine satır gizlenir (2 seçenek), Faz 4'te reklam gelince
  döner. **UX-23 · Öneri:** "Teklif 1/3" oyuncuya baskı gibi okunabilir; satır yalnız 2. ve 3. teklifte ("son teklif").

### 2.7 Metin kalitesi (`Resources/i18n/tr.json`, `en.json`, `unity_*.json`)

- TR/EN anahtar eşleşmesi tam (unity_* 7/7). Öğretici satırları ≤ 6 kelime ✓ ("Bloğu duvarın üstünden taşı!").
- **UX-24 · Önemli · metin · "dede" sesi kalıntısı.** `tut.ctx.resume` "Tahta bıraktığın gibi duruyor, **evlat**." /
  "…, **kiddo**." ve `tut.ctx.support` "Önce alttaki boşluğu doldur, **evlat**." / "…, **kiddo**." Usta Kral (D-101)
  sesinde "evlat/kiddo" Usta Dede kalıntısıdır ve oyuncuyu çocuklaştırır (yetişkin oyuncu, D-044). Düzeltme:
  TR "Tahta bıraktığın gibi duruyor." · "Önce alttaki boşluğu doldur." · EN "Just as you left it." · "Fill the gap
  below first." — `unity_tr.json` / `unity_en.json` üzerine yazma anahtarı olarak (web kaynağı salt okunur).
- `common.home` "Ana sayfa" ↔ sekme "Ana Sayfa" büyük harf tutarsızlığı (Öneri, P1: ikisi de "Ana sayfa").

## 3. Rubrik özeti (ART_REFERENCE §10 boyutları, UX gözüyle; kabul ≥ 4)

| Ekran | Düzen/boşluk | Tipografi | Durum görünürlüğü | Başparmak | Geri bildirim | Erişilebilirlik |
|---|---|---|---|---|---|---|
| Ana ekran | 3 (bant) | 4 | 3 | 5 | 2 (ölü dokunuş) | 3 |
| Oyun HUD | 3 (boş şerit) | 3 (28–30 px) | 4 | 4 | 3 | 3 |
| Öğretici | 3 | 4 | 4 | 4 | 3 | 4 |
| Kazanma | 4 | 4 | 4 | 5 | 5 | 4 |
| Ayarlar | 4 | 3 (kırpma) | 4 | 4 | 4 | 4 |
| Kaybetme | 4 | 4 | 4 | 5 | 4 | 4 |

## 4. Yönlendirme

- **code-lead (Unity):** UX-01…04, 06, 09…11, 18 → UX_UNITY §12 P0 (kodla yapılır; bu pakette kod değişmedi).
- **içerik/i18n sahibi:** UX-24 (unity_* üzerine yazma).
- **product-lead:** UX-10 ürün sorusu (erken Geri Al), UX-22/23 (teklif satırı görünürlüğü).
- **CI:** UX-14 parmak koordinatı; kaybetme/duraklat/çıkış onayı/bölüm başı (Bölüm 3) karelerinin `screens` setine
  eklenmesi (bu denetimin kör noktası).
