# ART_3D — 3B görsel dil ve üretim sözleşmesi (paket D1)

Sahip: design-lead · Tarih: 2026-11-01 · Kararlar: **D-099** (sabit saha), **D-100** (3B görsel dil: Blender 4.0.2 ile
prosedürel model, Cycles ile ön-görüntülenmiş sprite ve kare dizisi, Unity'de sprite + 2.5B tween), **D-101** (maskot
Usta Kral) · Stil kitabı: web reposu `docs/ART_REFERENCE.md` v1.1 (§2 renk, §3 bileşen anatomisi, §5 blok, §6 karakter,
§7 arka plan, §8 efekt, §10 rubrik, §11 kopya sınırı) · Önceki sanat kontratı: `docs/ART_UNITY.md` (v4 SVG atlas;
kare adları ve `atlas.json` biçimi **aynen** kalır) · Olay → animasyon: web `docs/JUICE.md` · Karakter sayfası:
`art/3d/spec/king.md` · Kare ve klip sözleşmesi: **`art/3d/spec/frames.json`** (üretici `art/3d/spec/gen_frames.py`).

> **Bu belge ne yapar.** 3B sürümün *nasıl görüneceğini* (malzeme, ışık, kamera, renk), *neyin üretileceğini* (kare ve
> klip listesi, boyut, pivot, 9-dilim, olay kareleri) ve *nasıl kabul edileceğini* (ölçülebilir ölçütler) sabitler.
> Sonraki paketler (öneri: **B1** blok + tahta + UI + ikon, **B1k** karakterler, **B2** FX + klipler + arka plan;
> paket kapsamını orkestratör belirler) bu sözleşmeye uyar. Kod `atlas.json` + atlas sayfalarını okumaya devam eder:
> v4'teki 488 kare adı, tasarım boyu, pivotu ve 9-dilim kenarı **değişmez**, yalnız pikselleri 3B render olur; yeni
> kareler yeni adlarla gelir.
>
> **Sınandı (2026-11-01, bu pakette).** Blender 4.0.2 (Ubuntu `apt`) başsız: Cycles CPU çalışıyor (OIDN yok →
> `use_denoising = False`), EEVEE `libEGL.so.1` olmadığı için **Blender'ı düşürüyor** (kullanılmaz). Rig A (blok) ve
> rig B (karakter) değerleri bu belgedeki sayılarla render edildi ve ölçüldü: `art/3d/spec/ref/tile_rig_test.png`,
> `ref/tile_rig_measure.txt`, `king/king_blockout_turnaround.png`.

---

## 1. 3B görsel dilin özü (10 cümle)

1. Her nesne **yumuşak hacimli bir oyuncaktır**: keskin köşe yok, en küçük pah hücrenin %2'si; kütleler iri, ayrıntı
   az ve kalın (Royal Match havası: şişkin, okşanası formlar).
2. Malzeme **parlak şeker-plastiktir**: Principled BSDF, roughness 0,30, ince cam gibi kaplama (coat 0,40, coat
   roughness 0,06), açık renklerde hafif iç ışıma (subsurface 0,10–0,12) — "jöle şekeri" sıcaklığı, metal değil.
3. Işık üç katmanlıdır: **sıcak anahtar güneş** (#FFE6C4 / #FFF1DC) ekranın sol üstünden, **soğuk mavi dolgu**
   (#9CC2FF / #E4EAFF) izleyici tarafından gölgeleri açar, **kenar ışığı** (#F2E8FF) siluetin arkasını ayırır.
4. Renk **markadır**: blok tabanları D-083 hex'leriyle birebir (üst yüz ΔE00 ≤ 8, ölçülen en kötü 5,9); görünüm
   dönüşümü **Standard** (AgX ve Filmic turuncu ve sarıyı ölçülebilir biçimde soldurur, §6.3).
5. Okunurluk kutsaldır: her blok rengi kendi sembolünü çıkıntının tepesinde kabartma olarak taşır; 3B ışık sembol
   kontrastını 3:1'in altına indiremez; plan ve düşüş gölgesi **düz** kalır (oyun bilgisi, 2B).
6. Kamera **tek açıdan** bakar: tahta ve bloklar 35° eğik ortografik (üst yüz + ön dudak), karakterler 10° yukarıdan,
   ikonlar 28° yukarıdan 3/4; böylece bütün ekran tek bir oyuncak sahnesi gibi tutarlıdır.
7. Gölge sahnenin parçasıdır: karakter ve yapılarda temas gölgesi kareye pişirilir (gölge yakalayıcı), bloklarda
   kaldırma gölgesi ayrı sprite (`fx_drop_blob`) olarak tween'lenir.
8. Hareket **sevimli abartıdır**: her eylem hazırlıkla başlar, hedefi aşar, esneyip ezilerek oturur; baret, kuşak ve
   mala gibi ikincil parçalar 2–4 kare gecikmeyle takip eder.
9. Ödül anı **sahnelenir**: karartma → ortada tek büyük 3B nesne + ışın + parlama → sayı → "Dokun ve al"; parıltı,
   konfeti ve yıldız patlaması yalnız bu anlarda, boşta asla.
10. Her şey **özgündür**: Usta Kral, ikonlar, sikke ve logo bizim nesnelerimizdir; referanslardan yalnız ölçü,
    anatomi ve efekt dili alınır (ART_REFERENCE §11, bu belgenin §11'i).

---

## 2. Blok malzemesi ve geometrisi (8 renk: W Y G R O C B P)

### 2.1 Malzeme (Principled BSDF, Blender 4.0 adları; ölçülmüş değerler)

`Base Color = srgb_to_linear(taban hex)`. Ortak: Coat IOR 1,5, Subsurface Radius (1,0 / 0,45 / 0,25), Subsurface Scale
0,05, Sheen 0, Metallic 0. Renk başına değerler `tile_rig_test.py` ile render edilip `tile_rig_measure.py` ile
ölçüldü (rig A, 180 px / hücre, 128 örnek; üst yüz yamasının medyanı, çıkıntı hariç).

| Kod | Taban (tokens) | Sembol mürekkebi | Roughness | Specular IOR Level | Coat | Subsurface | Ölçülen üst yüz | ΔE00 | Dudak / taban |
| --- | -------------- | ---------------- | --------- | ------------------ | ---- | ---------- | --------------- | ---- | ------------- |
| W | #764012 | beyaz %92 | 0,45 | **0,20** | **0,00** | 0,04 | #794729 | 4,71 | 0,79 |
| Y | #FFE23E | taban × 0,40 | 0,30 | 0,50 | 0,40 | 0,12 | #FFDE49 | 1,75 | 0,72 |
| G | #44D088 | taban × 0,40 | 0,30 | 0,50 | 0,40 | 0,12 | #50CB84 | 1,53 | 0,75 |
| R | #CF404E | beyaz %92 | 0,32 | 0,50 | 0,40 | 0,08 | #D84F59 | 4,05 | 0,77 |
| O | #EC8D20 | taban × 0,40 | 0,30 | 0,50 | 0,40 | 0,10 | #FF9946 | 5,87 | 0,76 |
| C | #C8CFDA | taban × 0,40 | 0,42 | 0,50 | 0,25 | 0,06 | #DCDEE7 | 4,34 | 0,72 |
| B | #2984DE | beyaz %92 | 0,30 | 0,50 | 0,40 | 0,08 | #3F87E1 | 2,15 | 0,79 |
| P | #5D3AAE | beyaz %92 | 0,32 | 0,50 | 0,40 | 0,06 | #6649AB | 4,32 | 0,80 |

W notu: koyu sıcak tonda beyaz yansıma kahveyi griye çekiyordu (ilk ölçüm ΔE00 9,2); W'de kaplama 0 ve Specular IOR
Level 0,2 ile 4,7'ye indi. Kenar pahının üst çizgisinde ince açık parlama ("şeker kenarı") kasıtlıdır.

### 2.2 Geometri (rig A; 1 BU = 1 hücre genişliği = 120 tasarım px)

| Öğe | Değer | Neden |
| --- | ----- | ----- |
| Kamera | ortografik, `rotation_euler = (35°, 0, 0)`: düz aşağıdan **35° eğik**, yalnız eğim (sapma yok) | üst yüz + ön dudak görünür; yatay ve dikey döşemede dikiş yok |
| Ön dudak (lip) | **24 tasarım px = 0,20 hücre** (v4: 20) | referans 26/117 = 0,22; daha tıknaz oyuncak |
| Slab kalınlığı T | 0,20 / sin 35° = **0,3487 BU** | dudağın ekrandaki boyu 24 px |
| Satır adımı | 1 / cos 35° = **1,2208 BU** (model derinliği) | ekranda her satır tam 1 hücre |
| Hücre kutusu y aralığı | alt komşusu **varsa** `[(j − 0,2) / cos35, (j + 0,8) / cos35]`, **yoksa** `[j / cos35, (j + 0,8) / cos35]` | dudak yalnız parçanın alt kenarında, hücre ızgarasının içinde |
| Dış köşe yarıçapı | 0,20 hücre (24 px) | v4 `cornerRadiusRatio` 0,2 |
| Kenar pahı | Bevel 0,10 hücre, 5 segment, profil 0,7, `limit_method = ANGLE` 30° | yalnız dış kenarlar yuvarlanır; hücre dikişleri düz kalır |
| İç (içbükey) köşe | pahın doğal yuvarlağı ≈ 0,06 hücre | v4 `innerCornerRadiusRatio` 0,07 |
| Yastık kubbesi | üst yüz ortası +0,02 BU (displace ya da subdivision ile) | ışık gradyanı, "şişkin" his |
| Çıkıntı (stud) | hücre başına **tek**; Ø 0,45 hücre (54 px), yükseklik 0,12, üst pah 0,03, merkez = hücrenin görünen üst yüz aralığının ortası | v4 `studD` 54; referansın 2 × 2 çıkıntısı alınmaz (ART_REFERENCE §11) |
| Sembol | çıkıntı tepesinde kabartma, yükseklik 0,015 hücre, kutu 0,37 hücre (44 px); mürekkep §2.1 | v4 `symD` 44,3; ART_DIRECTION §3.1 SVG yolları `io_curve_svg` ile içe alınıp katılaştırılır |
| Normaller | `use_auto_smooth = True`, 30° (Blender 4.0 API; 4.1'de kalktı) | boolean yok, harden normals yok (§2.3) |

### 2.3 Hücre birleşimi — çok hücreli blok tek parça (otomatik karo)

Kod v4'teki gibi blok başına **hücre karosu** `blk_<c>_<mm>` (16 maske × 8 renk) + iç köşe dolgusu
`blk_<c>_<mm>_cf_<tl|tr|bl|br>` (128) birleştirir; 3B'de bu karolar bir **bağlam render'ından kırpılır**:

1. Maske `mm` (bit 1 sol, 2 sağ, 4 aşağı, 8 yukarı) için merkez hücre + maskedeki dik komşular aynı parça olarak
   kurulur (**çapraz komşu yok**: v4 kuralı "karolar çapraz komşusuz çizilir").
2. Birleşim: hücre kutuları `join` → köşe kaynağı (`remove_doubles`, 1e-4) → çakışan yüz çiftleri silinir →
   `dissolve_limit` 1° → Bevel (ANGLE 30°) → auto smooth. **EXACT boolean ve `harden_normals` kullanılmaz**: ilk testte
   L parçada siyah yüzler ve gölgeleme izi bıraktı (`tile_rig_test.py` geçmişi). Bu yöntemle L parça tek parça,
   dikişsiz ve iç köşesi yumuşak çıktı (`ref/tile_rig_test.png`).
3. 3 × 3 hücrelik alan 2× süper örneklemeyle (360 px / hücre) render edilir, **merkez hücre** kırpılıp LANCZOS ile
   180 × 180'e indirilir. Komşular bağlamda bulunduğu için pah, ortam gölgesi ve yansıma dikişte süreklidir.
4. İç köşe dolgusu `cf`: merkez + iki dik komşu + **çapraz** komşu (2 × 2) render edilir, merkez hücrenin o köşesinden
   32 × 32 tasarım px kırpılır (pivot köşe; v4 ile aynı).
5. Çıkıntı merkezi (`anchorX/Y`, `studD`, `symD`) render'dan hesaplanıp `atlas.json`'a yazılır (v4 alanları).
   Alt komşusu olmayan hücrede çıkıntı aşağı kayar (v4: anchorY 0,6083 / 0,4958; 3B'de dudak 24 px olduğu için
   ≈ 0,60 / 0,50).
6. **Dikiş testi** (kabul): rastgele 12 parça (I2, I3, L3, O4, T4, S4, Z4, L4, J4, I4, P5, U5) karolardan birleştirilir,
   aynı parçaların tam render'ıyla karşılaştırılır: dikiş çizgileri boyunca piksel farkı ≤ 6/255 (ortalama), ≤ 24/255
   (en çok).

### 2.4 Varyantlar

| Durum | Görünüm | Kare |
| ----- | ------- | ---- |
| Tutulabilir (K-09) | karo + çıkıntı üstünde ayrı **parlama** sprite'ı (yalnız speküler, ADD) | `blk_gloss` (3B: çıkıntı tepesi parlaması) |
| Tutulamaz | karo `#EBEBEB` tint, parlama yok | — |
| Yerleşmiş / kilitli (K-14) | karo `#EFEFEF` tint + 4 harç noktası (temas gölgeli) + yerleşim parıltısı (`fx_lock_shine`) | `blk_locked` |
| Kaldırılmış | `blk_lift` tween (ölçek 1,08, 6 px yukarı) + `fx_drop_blob` | — |
| Plan hayaleti | **2B kalır** (ozalit üstüne basılı, ART §4): kesik kontur, düz dolgu | `ghost_<c>`, `ghost_<c>_front`, `ghost_front`, `ghost_hidden` |
| Düşüş gölgesi (K-18) | **2B kalır** (beyaz, tint) | `shadow_*` |
| Renk körü modu | 3B çıkıntı kapağı (sembolsüz) + 2B sembol × 1,2 | `stud_<c>` (3B) + `sym_<c>` (2B) |

### 2.5 Blok doğrulama (B1 kabulü)

| Ölçüt | Eşik | Bu pakette ölçülen (rig A testi) |
| ----- | ---- | -------------------------------- |
| Üst yüz rengi | ΔE00 ≤ 8 (8 renk) | en kötü **5,87** (O) |
| Ön dudak | taban × 0,70–0,85 | **0,72–0,80** |
| Sembol kontrastı (çıkıntı tepesinde) | ≥ 3:1 (WCAG 1.4.11) | B1 ölçer (karolarda sembol var) |
| Renk körlüğü | üst yüz ölçülen renkleriyle ART §2.2 yöntemi (Machado + Brettel, CIEDE2000): her koşulda en kötü çift ≥ 12 | B1 ölçer |
| Dikiş | §2.3 madde 6 | B1 ölçer |
| 375 pt'de 1 sn | 8 renk + sembol tek bakışta ayrılır (ART ilke 2) | B1 ekran görüntüsüyle |

---

## 3. Tahta (rig A; aynı eğim, aynı ışık)

| Parça | Kare (boy, tasarım px) | 3B tarif |
| ----- | ---------------------- | -------- |
| Saha zemini | `floor_yard_a` / `floor_yard_b` (120²) | **ahşap palet tahtası**: hücre başına 3 açık meşe tahta (#E2B97A / dama ikizi #D4A867), aralarında 4 px **kum** derzi (#E9C891, ince tane), tahta uçlarında Ø 6 çivi başı; mat (roughness 0,7); dudak yok (zemin) |
| Duvar (W, 60 px) | `wall_seg` (60 × 120, dikey döşeme), `wall_cap` (64 × 36) | **beton tuğla**, koşu bağı, hücre başına 3 sıra (40 px), tuğla #C9C2B4 / #B3AB9C, derz #8C8579 4 px, çıkıntısız ve mat (R bloğundan renk, çıkıntısızlık ve yatay derzle ayrılır; kırmızı pişmiş tuğla **kullanılmaz**); başlık: sarı #FFD43B ve antrasit #3A3346 45° ikaz şeridi, plastik |
| Geçit (W1) | `gap_seg` (60 × 120), `gap_edge` (60 × 22), `rail_seg` (120 × 16) | açıklık koyu çivit #2A2670 + iki galvaniz ray (#C9D1DA, metal 0,8, roughness 0,35), kenar başlıkları ikaz şeridi |
| Şantiye iskelesi | `scaffold_pipe` (16 × 120), `scaffold_clamp` (28 × 24), `beam_seg` (120 × 16) | galvaniz boru Ø 0,12 hücre, turuncu kelepçe (#EC8D20 plastik), tavan kirişi sarı-antrasit ikaz; şantiye zemini `floor_site` ozalit **2B kalır** (plan kâğıdı) |
| Vinç alanı (K-05) | `crane_band` (2B kalır, 120 × 240), `crane_hook` (96 × 160, rig C) | bant beyaz α 0,08 + alt kesik çizgi (2B); kanca turuncu makara + krom kanca + sarı ikaz bandı (3B) |
| Tepsi | `board_tray` 9-dilim (240², kenar 72 72 72 88) | rig D: kalın mor plastik çerçeve, yüz #5247B8 → #3F35A0, dış kenar #221B66, iç oluk #2E2778, köşe 40 / 20, coat 0,3 (ART_REFERENCE §5.3) |
| Kamyon | `truck` (360 × 200, rig C) | turuncu kabin + sarı kasa; **D-099 ile kamyon dolumu (#19) kalkar**; yalnız kuyruk çipi (K-26, product-lead kararına bağlı) için kalır |

Katman sırası (ART §4) değişmez: zemin → plan → ızgara → ray → bloklar → iskele önü → düşüş gölgesi → sürüklenen blok.

---

## 4. HUD ve arayüz (rig D: 20° eğik ortografik)

Ortak anatomi ART_REFERENCE §3.1'in 3B karşılığıdır: **dış kontur** = gövdenin koyu tonlu ince dış halkası (0,04 BU,
taban × 0,45), **dış yüz** (taban × 0,7), **iç plaka** (yüzden 0,06 BU yüksek, üst kenarı pahlı 0,04 BU), **alt dudak**
(gövdenin önü; kamera eğimi nedeniyle görünür, L boyda 40 tasarım px), coat 0,5 / coat roughness 0,05 (parlak),
plakanın üst %18'inde açık bevel bandı ışıktan doğal gelir. 9-dilim karelerde orta bölge **x boyunca düzgün** olmalıdır
(yalnız dikey gradyan; yatay değişen yansıma yok): bu yüzden rig D'de anahtar ışık yalnız eğim ekseninde döner.

| Öğe | Kareler (v4 adları) | 3B tarif |
| --- | ------------------- | -------- |
| Düğme L / M (normal) | `btn_primary`, `btn_primary_m`, `btn_secondary`, `btn_danger`, `btn_offer`, `btn_disabled` | yeşil #5CE83A → #2BCA0A → #19B205, dudak #02630F, kontur #0A5A12 (§2.3 dörtlüler); greenSoft / orange / cream / grey varyantları aynı model, malzeme değişir |
| Düğme (basılı) | `*_pressed` | iç plaka 8 px iner (0,053 BU), dudak 40 → 14 px, ölçek 0,97 (JUICE #97) |
| Araç düğmesi | `btn_tool`, `btn_tool_pressed` | mor dörtlü (#7A66F8 / #604DF7 / #2A1D8B / #16133C), ikon ayrı sprite |
| Güçlendirici yuvası | `btn_booster` (+ `_pressed`, `_off`), `btn_slot` | yeşil küçük kare (bevel 18, dudak 20); `_off` gri dörtlü + α 0,85 |
| Kapat × | `btn_close` (+ `_pressed`) | kırmızı yuvarlak kubbe (#FE3232, dudak #7C0000), krem × kabartma 0,03 BU |
| Satın al + | `btn_plus` | yeşil kubbe, beyaz + kabartma |
| Pencere | `panel_window` 9-dilim (320 × 420; 96 96 96 160), `panel_plate`, `panel_card` | ART_REFERENCE §3.7 katmanları 3B kademe olarak: dış kontur → açık kenar → oluk (0,02 BU çukur) → iç bevel → gövde (#4A3AC7 → #523ED5) → alt çift dudak; iç plaka 0,03 BU yüksek |
| Kurdele başlık ("kurdeleli pencere") | `ribbon_title` 9-dilim (520 × 150; 150 40 150 40) | saten şerit (roughness 0,35, sheen 0,6), orta bant yatay düzgün, uçlarda katlanmış kuyruk (gölgeli kıvrım), altın kenar şeritleri |
| Çipler / sayaçlar | `capsule_hud`, `capsule_home`, `hud_moves_plate`, `hud_well` | **çukur** kapsül: içe 0,03 BU basık, koyu #14113A α 0,85 dolgu, iç kenar gölgesi; hamle plakası krem #F9F3C3 kubbe + lacivert bant |
| Rozetler | `badge_count`, `badge_dot`, `bead_on/off` | kırmızı yuvarlatılmış küp (#F04A3C → #C8140F), coat 0,6; boncuk altın / gri yarım küre |
| İlerleme | `progress_track`, `progress_fill` (+ `_s`) | iz çukur lacivert #06436D, dolgu yeşil (#7FC21A → #4E9300) jel tüp (subsurface 0,15) |
| Anahtar, balon, gezinme | `toggle_on/off`, `bubble_speech`, `bubble_tail`, `nav_bar`, `nav_tab_on` | aynı dil; balon krem #FFF6D2 + iki halkalı kontur (#2E9BF0 / #0B3A6E) yastık |
| Yıldız | `icon_star`, `icon_star_empty`, `icon_star_xl` | 5 köşeli **şişkin** yıldız (köşeler 0,12 yarıçap), altın #FFE96A → #FFD119 → #FFAD00, coat 0,6; boş: koyu çivit cam α 0,6 |
| Sikke | `icon_coin`, `icon_coin_xl`, `fx_coin_*` | kalın altın disk, kenarda tırtıl, yüzde kabartma **mala** (taç değil, §11), metal 1, roughness 0,22 |
| Kalp | `icon_heart`, `icon_heart_xl` | şişkin kalp, kırmızı #FF3B3B → #B80E12 jöle (subsurface 0,15, coat 0,7), sayı kodla içine yazılır |
| 4 güçlendirici | `icon_crane`, `icon_hammer`, `icon_brush`, `icon_gold_trowel` (+ `_xl`) | **vinç**: turuncu oyuncak vinç kolu + sarı kanca; **çekiç**: kırmızı başlı ahşap saplı tokmak (kauçuk baş, roughness 0,5); **boya**: geniş fırça, ahşap sap, kılda gök mavisi + nane boya damlası; **altın mala**: Usta Kral'ın malası (king.md §8) |
| Kazanma ışınları | `fx_rays` (**2B kalır**: 1024², ADD, tint #FFE7A3 α 0,35, 0,02–0,05 tur/s) + 3B ödül nesnesi (`icon_*_xl`) + `fx_glow` (2B) + `fx_star_burst` (3B dizi) | ART_REFERENCE §8 "Dokun ve al" dizisi |

İkon rig'i C: 28° yukarıdan, nesne −20° sapma (3/4), nesne karenin %86'sını doldurur, temas gölgesi yok (ikonlar
düğme üstünde durur), kenar ışığı gücü × 1,3 (koyu zeminde siluet).

---

## 5. Arka planlar (rig F)

### 5.1 Ana sayfa — Renkli Tepe (katmanlı paralaks)

Her katman ayrı render edilir (gök hariç `film_transparent`), Unity'de kaydırma ve cihaz eğimiyle (ya da sürüklemeyle)
paralaks çarpanı kadar kayar; `bg_home` aynı katmanların **düzleştirilmiş birleşimi** olarak kalır (v4 kodu bozulmaz).

| Kare | Tasarım boyu | Ölçek | Paralaks | İçerik ve renk |
| ---- | ------------ | ----- | -------- | -------------- |
| `bg_home_sky` | 1080 × 1920 | 0,375 | 0,00 | gök gradyanı #1B49A4 → #2B5FB6 → #7AA3D8 → ufuk #F0D7D6, sağ üstte yumuşak güneş halesi #FFF3C4 α 0,35 |
| `bg_home_clouds` | 1080 × 640 | 0,5 | 0,06 | 3 katmanlı pamuk bulut (#FFFFFF / #F4E3E8 / #E6D2DD), 6 px/s sürüklenir |
| `bg_home_far` | 1080 × 720 | 0,5 | 0,15 | uzak tepeler, sise %40 karışmış (mist #C7D8F2), kontur yok |
| `bg_home_town` | 1080 × 900 | 0,75 | 0,35 | Renkli Tepe evleri (çatılar blok paletinden: R, O, B, G, Y), kule vinç, yuvarlak ağaçlar; kasaba yapısı (`town_ch*`) bu katmanın önüne ayrı konur |
| `bg_home_ground` | 1080 × 760 | 0,75 | 0,60 | çayır (#7CC75A → #4E9A3A) + kum patika (#E9C891); karakterlerin bastığı zemin |
| `bg_home_fore_l`, `bg_home_fore_r` | 420 × 760 | 0,75 | 1,00 | sol: yuvarlak çalı kümesi + çiçek; sağ: tuğla paleti + kum yığını; ekran kenarına %40 taşar |

"Sakin orta" (ART §7) korunur: Bölüm düğmesinin (600 × 200, alttan 336) ve karakter grubunun arkasında katman 4–6
nesnesi yok. Vinyet: kenarlarda × 0,85 (kodda `px_white` tint ya da birleşimde pişmiş).

### 5.2 Bölüm ekranı — sakin

`bg_level` (1080 × 1920, ölçek 0,375): ART_REFERENCE §7.2 çivit zemin #1A173C + (540, 0,26 H) radyal parlama #2B2661 +
ozalit ızgarası α 0,02 + kenar vinyeti × 0,85. 3B katkı yalnız kenarlarda: sol altta ve sağ üstte **odak dışı** vinç
kolu ve iskele siluetleri (α 0,07, alan derinliği bulanıklığı Cycles DOF f/1,4 ile), tahta tepsisinin arkasında hiçbir
nesne yok. Oyun bilgisi (bloklar) zeminden en az 3 kademe parlaklıkla ayrılır.

---

## 6. Işık, kamera, render ve renk yönetimi

### 6.1 Rig'ler (Blender değerleri; ayrıntı `frames.json → rigs`)

| Rig | Kamera (ortografik) | Anahtar | Dolgu | Kenar | Dünya | Kullanım |
| --- | ------------------- | ------- | ----- | ----- | ----- | -------- |
| **A_tile** | `rot (35°, 0, 0)`; 1 BU = 1 hücre | SUN #FFF1DC **2,9**, `rot (−48°, 0, 32°)`, açı 6° (ekranın sol üstünden) | SUN #E4EAFF **1,1**, `rot (60°, 0, −15°)`, açı 30° (izleyici tarafından: ön dudağı açar) | AREA #FFFFFF 80 W, 2 m, sahne merkezi + (−2, +5,5, +2,5) | #B4B8CC × 0,45 | blok, tahta, zemin, duvar |
| **B_char** | `rot (80°, 0, 0)` (10° aşağı), ortho scale 3,0, hedef z 1,3 | SUN #FFE6C4 **2,6**, `rot (50°, 0, −35°)`, açı 8° | AREA #9CC2FF 180 W, 3 m, (3,0, −3,2, 1,6) → (0, 0, 1,3) | AREA #F2E8FF 360 W, 1,6 m, (2,0, 3,0, 3,2) → baş | #5A6A96 × 0,25; gölge yakalayıcı zemin | karakterler |
| **C_prop** | `rot (62°, 0, 0)` (28° aşağı), nesne sapması −20° | B_char ile aynı | B_char | B_char × 1,3 | #5A6A96 × 0,30 | ikon, sikke, yıldız, kalp, kamyon, kasaba yapıları |
| **D_ui** | `rot (20°, 0, 0)` | SUN #FFFFFF 2,2, `rot (−25°, 0, 30°)`, açı 10° | AREA #C8D8FF 100 W, 6 m, (0, −4, 4) | — | #FFFFFF × 0,35 | düğme, pencere, kurdele, çip, tepsi |
| **E_fx** | `rot (90°, 0, 0)` (önden), efekt başına kadraj | B_char + parlamalarda emission | — | — | siyah × 0 | parçacık ve efekt dizileri |
| **F_bg** | `rot (78°, 0, 0)` (12° aşağı) | SUN #FFE9C8 3,0, `rot (55°, 0, −30°)`, açı 4° | gök (dünya gradyanı) | — | gök gradyanı × 1,0; mist 8 → 48 m #C7D8F2 | arka plan katmanları |

Işık yönü anlamı: Blender güneşi yerel −Z boyunca ışır; A_tile'da `(−48°, 0, 32°)` ışığı ekranın sol üstünden
sağ alta doğru gönderir (üst pah açık, ön dudak koyu), dolgu `(60°, 0, −15°)` izleyici tarafından gelir. Ölçülen
sonuç §2.1 tablosundadır.

### 6.2 Render boyutları (saklanan boy = tasarım boyu × `scale`; render = saklanan × 2, sonra LANCZOS)

| Aile | Tasarım | `scale` | Saklanan | Render |
| ---- | ------- | ------- | -------- | ------ |
| Blok karosu | 120 × 120 | 1,5 | **180** | 360 / hücre (3 × 3 bağlam = 1080²) |
| Karakter (usta görüntü) | 512 × 512 tuval | — | **512** | 1024 |
| Karakter durağanları | 300 × 480 | 1,25 | 375 × 600 | 750 × 1200 |
| Karakter klip kareleri | köşe 320², sahne 400 × 480 | 1,0 | 320², 400 × 480 | 2× |
| İkon | 128 × 128 | 1,5 | 192 | **256** usta → 384 render |
| Büyük ödül nesnesi | 256 × 256 | 1,0 | 256 | 512 |
| Arka plan | **1080 × 1920** | 0,375–0,75 | 405 × 720 … 810 × 1440 | 1080 × 1920 (64 örnek) |

### 6.3 Renk yönetimi ve dosya

- Görünüm dönüşümü **Standard**, look None, exposure 0, gamma 1, ekran aygıtı sRGB. Ölçüm (rig B, aynı sahne): AgX
  (look "AgX - Medium High Contrast") turuncu tulumu şeftaliye, sarı bareti soluk hardala çekti; "AgX - Punchy" daha
  koyu ve daha az doygun; Standard referansın şeker doygunluğuna en yakın ve blok hex'lerini koruyor (§2.1). Blender
  4.0.2'de geçerli adlar: `Standard`, `AgX`, `Filmic`, `Filmic Log`, `False Color`, `Raw`; AgX look'ları `AgX - …`
  önekiyle (ör. `AgX - Punchy`). Highlight kırpılan tek tek sahnelerde (arka plan güneşi) exposure −0,3'e kadar inilir.
- Malzeme rengi: `Base Color = srgb_to_linear(hex)`.
- Cycles CPU; `use_denoising = False` (Ubuntu derlemesinde OpenImageDenoise yok: açık bırakılırsa render
  "Build without OpenImageDenoiser" hatasıyla durur); örnek sayısı A 128, B 96, C 128, D 96, E 64, F 64;
  `sample_clamp_indirect = 3`, `blur_glossy = 1` (ateş böceği gürültüsü yok); gürültü 2× süper örnekleme + LANCZOS
  küçültmeyle bastırılır.
- EEVEE **kullanılmaz**: başsız ortamda `libEGL.so.1` yok, Blender çıkış kodu 2 ile düşüyor (2026-11-01 denemesi).
- Çıktı: PNG RGBA 8 bit, **düz (ilişkisiz) alfa**, sRGB; `film_transparent = True`; karakter ve yapılarda gölge
  yakalayıcı (`is_shadow_catcher`) temas gölgesini alfaya yazar.
- **Alfa taşırma**: küçültmeden sonra tam saydam piksellere en yakın opak pikselin RGB'si 4 px kopyalanır (çift doğrusal
  süzmede koyu hale yok). Atlas paketleyicinin 2 px kenar uzatması (v4 `--pad 2`) aynen kalır.

### 6.4 Klasör ve komut düzeni (öneri; B paketleri kurar)

```
art/3d/spec/            bu paket: frames.json, gen_frames.py, king.md, king/, tile_rig_test.py, ref/
art/3d/scenes/<aile>.py B paketleri: bpy sahne betikleri (blocks.py, board.py, ui.py, icons.py, chars.py, fx.py, bg.py)
art/3d/out/<kare>.png   render çıktısı, saklanan boyda, frames.json adıyla (klip kareleri chr_king_idle_00 …)
```

`blender -b --factory-startup -noaudio --python art/3d/scenes/blocks.py -- --out art/3d/out` → atlas paketleyici
(`tools/art/rasterize.mjs`'in 3B girişi ya da yeni `pack3d`): `art/3d/out/<ad>.png` varsa o kare SVG raster'ının
yerine geçer, yoksa v4 SVG'si kalır (`frames.json → status`). `atlas.json` biçimi (ART_UNITY §3) değişmez.

---

## 7. Animasyon

### 7.1 İlkeler (Royal Match dili, 12 ilkenin uygulaması)

1. **Tek dinlenme pozu K0**: Usta Kral'ın bütün klipleri idle'ın 0. çizimiyle başlar ve biter → klipler arası geçiş
   kesmesizdir, karışım (crossfade) gerekmez. Döngülü kliplerin `in` bölümü vardır; `out` = `in`'in tersi ya da verilen
   çizimler.
2. **Zaman çizgisi 24 fps**; benzersiz çizimler **ikili / dörtlü** tutulur (`timeline` dizisi), hızlı eylemde tekli.
   Üstüne Unity'de **sürekli 2.5B tween katmanı** biner (ör. idle nefesi: `scaleY = 1 + 0,015 sin(2πt / 2 s)`, ayak
   pivotlu): 6 fps çizimler akıcı hareketle yumuşar, bellek az kalır.
3. **Hazırlık** (anticipation): her büyük eylemden önce 2–6 kare zıt yön (çömelme, geri çekiş).
4. **Ezilme-esneme**: sıçramada sy 1,08, inişte 0,90; hacim korunur (sx = 2 − sy değil, sx ≈ 1/√sy).
5. **Aşma ve oturma** (overshoot): hedefi %3–6 ya da 6° geçer, 3–5 karede yerleşir.
6. **İkincil hareket ve takip**: baret 2–4 kare gecikmeli, mala ±3°, kuşak sert; büyük sevinçte baret havalanır.
7. **Yavaş giriş-çıkış**: anahtar pozlar arası ara çizimler pozlara yakın yığılır.
8. **Kavisler**: el, mala ve baş yay çizer; doğrusal ara çizim yok.
9. **Sahneleme**: her poz tek okunur siluet; eller gövdeden ayrık (king.md §2 siluet kuralı).
10. **Abartı**: ifade ve jest bir kademe büyük (kaş ±0,08 BU, ağız genişliği yüzün %75'ine kadar).
11. **Zamanlama JUICE'a bağlı**: blok ve FX süreleri `docs/JUICE.md` ms'sidir; klip kareleri eğrinin biçimidir, kod
    süreyi JUICE'tan alır.
12. **Azaltılmış hareket** (JUICE kural 8): karakter klipleri yerine anahtar poz durağanı (`chr_king_still_<poz>`) 150 ms
    solmayla; tween katmanı kapalı; döngüler durur.

### 7.2 Usta Kral klipleri (`frames.json → clips`)

Köşe seti tuvali 320 × 320 (pivot 0,5 / 0,0625, ölçek 1,0); sahne seti 400 × 480 (pivot 0,5 / 0,05). Kare adı
`chr_king_<klip>_<NN>` (NN = benzersiz çizim no).

| Klip | Kare | fps | Döngü | Benzersiz | Olay kareleri | Nerede (JUICE) |
| ---- | ---- | --- | ----- | --------- | ------------- | -------------- |
| `king_idle` | 48 | 24 | 0–47 | 14 | 30 `blink` | oyun köşesi, pencereler (sürekli nefes tween'i) |
| `king_cheer` | 24 | 24 | yok | 16 | 4 `lift_off`, 10 `apex`, 14 `land` | doğru yerleşim serisi, hedef tamam (#12, #15, #54; Tuna #82'nin yerine) |
| `king_think` | 36 | 24 | 8–35 (`out` = 0–7 ters) | 12 | 12 `tap`, 24 `tap`, 28 `blink` | takılma ipucu, D1 öncesi (#21a/#21c) |
| `king_talk` | 24 | 24 | 0–23 | 10 | 0 `beat`, 12 `beat` | öğretici balonu daktilo süresince (#81, #103) |
| `king_point` | 18 | 24 | 12–17 (`out` = u11 ×2, u0 ×2) | 12 | 6 `point_hit`, 12 `hold_start`, 14 `glint` | öğretici işaret (#103, #104 nabzı `point_hit`'te başlar); sol hedefe ayna |
| `king_encourage` | 24 | 24 | yok | 14 | 9 `gesture`, 15 `pump` | hamleler bitti (#57), +5 teklifi (#52) — utandırmayan, sıcak |
| `king_home_idle` | 48 | 24 | 0–47 | 14 | 30 `blink` | ana sayfa (sahne boyu) |
| `king_wave` | 24 | 24 | 6–23 (`out` = 0–5 ters) | 12 | 9 `wave_peak`, 18 `wave_peak` | ana sayfa karşılama (#99) |
| `king_big_cheer` | 48 | 24 | 24–47 | 25 | 6 `jump`, 15 `land`, 22 `hat_land`, 28 `hop`, 40 `hop` | kazanma (#55 aşama 4–5, #95) |
| `kepce_wag` (öncelik 2) | 16 | 24 | 0–15 | 8 | — | Kepçe kralın yanında |

Olaylar kodda ses ve haptiğe bağlanır: `land` → `sfx_land` −6 dB + hafif haptik; `hat_land` → `sfx_clamp`; `beat` →
`sfx_voice_king`; `point_hit` → JUICE #104 vurgu nabzı başlar; `pump` → hafif haptik. Durağanlar:
`chr_king_still_<idle|cheer|big_cheer|think|talk|point|encourage|wave>` (300 × 480), büstler
`chr_king_bust_<neutral|happy|think|talk|encourage|surprised>` (256 × 320; öğretici portresi Ø 132 daire kırpımı ifade
türüne göre değişir).

### 7.3 Blok klipleri (yalnız tween verisi; `frames.json → tweenClips`, 60 fps örnekleme)

| Klip | Kare | İçerik | JUICE |
| ---- | ---- | ------ | ----- |
| `blk_lift` | 6 | ölçek 1,0 → 1,09 → 1,08; y 0 → −6 px; gölge α 0 → 0,3, ofset 0 → 18 px | #1 (80 ms `Back.easeOut`), #91 |
| `blk_land_squash` | 8 | sx 1,0 → 1,12 → 0,98 → 1,0; sy 1,0 → 0,86 → 1,03 → 1,0; kare 1'de `dust_puff` | #11 (120 ms) |
| `blk_bad_shake` | 12 | x 0, ±2 px × 3; kırmızı tint #FF4A3D α 0,5 → 0 | #13 (sallanma bölümü) |
| `fx_lock_shine` (render dizisi) | 12 @ 60 fps | çıkıntı tepesinde beyaz ADD parıltı, hücreler arası 40 ms gecikme alttan üste | #12, #92 (200 ms) |

### 7.4 FX dizileri (render; 24 fps; ayrıntı `frames.json → clips`)

| Klip | Kare | Olay | Tuval (tasarım) | Not |
| ---- | ---- | ---- | --------------- | --- |
| `fx_crane_grab` | 8 | 5 `grab` (ters oynatmada 2 `release`) | 160 × 200, pivot üst orta | pençe açık → kapalı; halat ve taşıma tween (#62, 900 ms) |
| `fx_hammer_smash` | 11 | 6 `impact` | 320², pivot sap ucu | −60° → +20° savrulma, vuruş, geri tepme (#61, 450 ms) |
| `fx_impact_star` | 8 | 0 `burst` | 256² | vuruş patlaması (beyaz çekirdek + altın) |
| `fx_paint_splash` | 12 | 3 `splat` | 256² | beyaz render, yeni renkle tint (#63, 500 ms) |
| `fx_trowel_sparkle` | 12 (döngü) | 0, 6 `glint` | 192² | altın mala ±15° dönüş + parıltı (#16, #17) |
| `fx_coin_spin` | 12 (döngü) | 0 `face` | 128², ölçek 1,5 | sikke dikey eksende 360° (#56, #74) |
| `fx_star_burst` | 14 | 0 `burst` | 512², ölçek 0,5 | halka + 16 küçük yıldız (#96, 600 ms) |
| `fx_confetti3d_rect` / `_round` | 8 (döngü) | — | 24 × 12 / 16², ölçek 1,5 | 3B takla dönüşü, beyaz render, palet tint (#55, #18) |
| `fx_dust_puff` | 10 | 0 `puff` | 192 × 128, pivot alt orta | krem / gri tint (#11, #19, #62) |
| `fx_segment_done` | 14 | 0 `flash`, 6 `fly` | 512², ölçek 0,5 | dilim parlaması + halka + kıvılcım; panoramaya uçuş tween (#18, #93) |

### 7.5 Bellek bütçesi (WebGL)

v4 atlası `Resources/*.bytes` + `LoadImage` ile **sıkıştırmasız RGBA32** yüklenir (ART_UNITY §3.2, §8). Karakter kareleri
için hedef: **oyun ekranında ≤ 32 MB, ana sayfada ≤ 24 MB** (kırpılmış RGBA32 eşdeğeri). Tahmin: köşe seti 78 çizim ×
≈ 254 KB = 19,8 MB; sahne seti 51 × ≈ 422 KB = 21,5 MB; kazanma penceresi oyun ekranına `king_big_cheer` (25 × 422 KB =
10,5 MB) ekler → 30,3 MB. **Öneri (code-lead)**: karakter ve FX sayfaları Unity içe aktarma hattında sıkıştırılmış
`Texture2D` varlığı olarak (ASTC / ETC2 / DXT, CI derlemesinde) gelsin → ≈ 8 kat az GPU belleği; olmazsa sayfa başına
ekran bazlı yükleme (köşe seti / sahne seti ayrı sayfa).

---

## 8. Usta Kral (özet; tam sayfa `art/3d/spec/king.md`)

Sarı plastik inşaat bareti + önünde gök mavisi emaye plaka üstünde **3 uçlu, uçları yuvarlak çıkıntılı, taşsız** altın
taç arması; kısa köşeli **kestane çene sakalı, bıyık yok**; iri amber gözler, kalın "tuğla" kaşlar (siperliğin önünde);
düğme burun; **2,5 baş** squircle kafa + tuğla gövde; turuncu **iş tulumu** (dirseğe kıvrık kol, V yaka, cep + kalem);
gök mavisi **kraliyet kuşağı** üstünde yaprak / parıltı / elmas rozetleri; deri **alet kemeri**; kahverengi **iş
botu**; asa gibi taşınan **Altın Mala**. Palet: turuncu #EC8D20, gök mavisi #2984DE, güvenlik sarısı #FFD43B, kestane
#7A4222, ten #E9A877.

**Fark tablosu özeti** (20 madde, king.md §10): ad; başlık (dev taç ↔ baret); taç biçimi ve yeri (ön-orta taşlı sivri
taç ↔ taşsız küçük arma); sakal (gür beyaz ↔ kısa kestane); bıyık (kıvrık beyaz ↔ yok); kaş; burun (yumru pembe ↔
düğme); göz (küçük koyu ↔ iri amber); ten; oran (~4 baş varil ↔ 2,5 baş tuğla); kıyafet (mor tunik ↔ turuncu tulum);
pelerin / kürk (altın pelerin ↔ yok); renk düzeni (mor + altın ↔ turuncu + mavi + sarı); kraliyet işareti (taç +
pelerin ↔ kuşak + blok sembolü rozetleri); kemer; ayakkabı; el nesnesi (yok ↔ Altın Mala); rol (kurtarılacak kral ↔
öğretmen); yan karakterler (uşak + turuncu köpek ↔ yok / Kepçe); sikke amblemi (taç ↔ mala). Yasak listesi king.md
§10.1 (12 madde: dev taç, ön-orta taş, beyaz sakal / kıvrık bıyık, mor-altın pelerin ve kırmızı-ermin birleşimleri,
yumru pembe burun, kral-uşak-köpek üçlüsü, "kralı kurtar" sahneleri, sikkede taç, Bob the Builder / Mario / Wario
birleşimleri).

Blockout (`king/king_blockout.py`, rig B, Standard) oranları ve malzemeleri doğrular; final model B1k'nın işidir
(şekil anahtarları, iskelet ve kabul: king.md §11).

---

## 9. `frames.json` sözleşmesi

Üretici: `python3 art/3d/spec/gen_frames.py` (standart kütüphane; girdi `Assets/MinikUsta/Resources/art/atlas.json`).

| Alan | Anlam |
| ---- | ----- |
| `frames.<ad>.w / h` | tasarım px (1080 genişlikli tuval) |
| `scale` | raster px / tasarım px (saklanan = w × scale); Unity sprite PPU = 100 × scale (ART_UNITY §3.1) |
| `pivot` | [x, y], 0..1, **sol alttan** |
| `border` | 9-dilim, tasarım px, sıra **L T R B** (atlas.json'a × scale yazılır) |
| `rig` | §6.1 rig kimliği; `pipeline` `blender` ya da `svg` |
| `status` | `replace3d` (v4 adı, aynı boy / pivot / kenar, yeni 3B piksel) · `keep2d` (v4 SVG'si kalır) · `new` (yeni ad) |
| `aliasOf`, `deprecated` | `chr_dede_idle` → `chr_king_still_idle`, `chr_dede_talk` → `chr_king_still_talk`, `chr_dede_bust` → `chr_king_bust_neutral`: eski adlar Usta Kral olarak render edilir, kod `chr_king_*`'a geçince silinir |
| `anchor` (blok) | render'dan hesaplanır, atlas.json `anchorX/Y`, `studD`, `symD` |
| `clips.<ad>` | `frames`, `fps`, `unique`, `frameName` (`…_{NN}`), `canvas`, `scale`, `pivot`, `loop` ({start, end} ya da false), `out`, `timeline` (kare → çizim), `events` (kare no → ad), `tween`, `use`, `juice`, `priority` |
| `tweenClips` | yalnız veri (render yok) |
| `colorManagement`, `rigs`, `blockMaterial`, `budget` | §6, §2, §7.5 değerleri |

Sayılar (bu sürüm): **783 kare** = 488 v4 (371 `replace3d`, 117 `keep2d`) + 295 yeni (Usta Kral durağanı 8, büstü
6, klip çizimi 129; Kepçe klip çizimi 8; FX dizi karesi 129; `icon_*_xl` 8; paralaks katmanı 7),
**22 klip**, **3 tween klibi**. `keep2d` aileleri: `ghost_*`,
`shadow_*`, `sym_*`, `floor_site*`, `crane_band`, `px_white`, `fx_glow`, `fx_rays`, `fx_sheen`, `fx_drop_blob`,
`logo_wordmark` (değiştirilebilir kelime işareti, D-068), `flag_*`.

Uyumluluk kuralı: bir `replace3d` karesinin `w / h / pivot / border` değeri **değiştirilemez** (kod yerleşimi bunlara
bağlı, `FrameContractTests`); değişmesi gerekiyorsa yeni ad açılır ve bu belgeye satır eklenir.

---

## 10. Kabul ölçütleri (ART_REFERENCE §10 rubriğine ek)

Her ekran §10 rubriğiyle (6 boyut, her boyutta ≥ 4) puanlanır; 3B sürüm için ek ölçütler:

| Aile | Ölçüt |
| ---- | ----- |
| Blok | §2.5 tablosu (palet ΔE00 ≤ 8, dudak 0,70–0,85, sembol ≥ 3:1, CVD ≥ 12, dikiş, 375 pt'de 1 sn) |
| UI | 9-dilim karelerde orta sütunun yatay ışık değişimi ≤ 3/255; basılı / normal çift aynı dış kontur (piksel farkı yalnız plaka ve dudakta) |
| İkon | 64 px'te siluet tanınır (5 kişilik ikili test ≥ 4 doğru); ikon karenin %86'sı ± 4 |
| Karakter | king.md §11 kabulü; klip başı ve sonu K0 ile piksel farkı ≤ 2/255 (kesmesiz geçiş) |
| FX | parçacık bütçesi JUICE kural 4 (ekranda ≤ 120, patlama ≤ 40) |
| Bellek | §7.5 hedefleri |
| Rubrik "hacim / ışık" 5 için | sol üst anahtar + mavi dolgu + kenar ışığı her 3B karede; ışık yönü ekranlar arası tutarlı |
| Rubrik "karakter / sahne" 5 için | 3B render malzeme okunurluğu (plastik / pamuk / saten / deri / metal) + temas gölgesi + paralaks katmanları |

---

## 11. Kopyalanabilir / kopyalanamaz (3B ekleri)

ART_REFERENCE §11 aynen geçerlidir. 3B'ye özgü ekler:

- **Alınır:** ışık dili (sıcak anahtar + mavi dolgu + kenar ışığı), malzeme ruhu (şeker plastik, parlak kaplama),
  oyuncak oranları, ödül sahnelemesi, animasyon ilkeleri (hazırlık, aşma, ikincil hareket).
- **Alınmaz:** referans karakterlerinin modeli, yüzü, siluet ve kıyafet birleşimi (king.md §10.1); RM kralının taç
  biçimi (sivri uçlu, ön-orta taşlı); RM sikkesinin taç amblemi; Block Out! maskotları ve 3B tuğla-harf logosu; referans
  ikonları (roket, dondurucu tabanca, ufo, şato, kupa çizimleri); referansın hücre başına 2 × 2 çıkıntısı; referans
  görüntülerinden doku, renk örneği dışında piksel, kırpma, iz sürme ya da "image to 3D" dönüşümü.
- **Üretken yapay zekâ yok, satın alınmış varlık yok** (D-100): bütün modeller bpy betiğiyle prosedürel ya da elle
  modellenir; dokular prosedüreldir (Blender node'ları) ya da kendi SVG'lerimizdir.
- **Doğrulama:** mağaza öncesi her karakter ve ikon için ters görsel arama (ASSET §15); king.md §10.1 yasak listesi
  B1k kabulünün maddesidir.

---

## 12. D-099 (sabit saha) görsel sonuçları

1. Bölümün bütün blokları ilk andan sahada: saha **dolu** (6 × 8'e kadar 48 hücre) açılır; bu yüzden tutulabilir /
   tutulamaz ayrımı (parlama + tint, §2.4) ilk bakışta okunmalıdır — B1 kabulüne "dolu saha" ekran görüntüsü eklenir
   (Bölüm 1–10'dan en dolu saha).
2. Dilim geçişinde **kamyon dolumu yok**: JUICE #19 (kamyon teslimatı) ve `truck` teslimat animasyonu kullanılmaz;
   `truck` karesi yalnız K-26 kuyruk çipi kalırsa durur (product-lead). Dilim tamamlama (#18, `fx_segment_done`)
   kalır ve tek ödül vurgusu olur.
3. Birden çok dilimi planlama: panorama kuyusu (HUD `Panorama`) ve şantiye planı görünür kalır; 3B'de plan hayaleti
   2B kalır (§2.4), böylece "sıradaki dilimin hangi bloğu istediği" dolu saha üstünde de okunur.
4. Kazı ve park (K-12 ray): ray ve kelepçe 3B (§3) — parkta kelepçe 0 → 1 ölçek (JUICE #23) render'lı kelepçe
   sprite'ıyla.

---

## 13. Açık sorular ve önerilen kararlar

1. **EN adı**: "Builder King" önerisi (orkestratör / entrepreneur; D-068 ad ailesiyle).
2. **Hikâye**: STORY.md'de Usta Dede rolü (firma kurucusu, öğretmen) Usta Kral'a geçer; ipucu cümleleri ve
   `sfx_voice_dede` → `sfx_voice_king` (web STORY / i18n güncellemesi ayrı paket; Unity i18n'de Dede adı geçen anahtarlar
   listelenmeli).
3. **Oyun köşesi**: köşede Tuna yerine **Usta Kral** (+ küçük Kepçe) önerilir (`LevelHud` `hud.tuna` → `hud.king`,
   klipler §7.2); Tuna ana sayfada ve hikâyede kalır.
4. **Sıkıştırılmış doku** içe aktarımı (§7.5) code-lead kararı; olmazsa karakter klipleri sayfa bazında yüklenir.
5. **Blok dudağı 20 → 24 px**: `anchorY` değerleri değişir (atlas'tan okunur; kod değişmez), ama `BoardLayout` ya da
   testler 20 px'e sabit bağlıysa code-lead denetler.
