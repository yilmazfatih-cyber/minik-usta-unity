# B1 — 3B ortam, bloklar, arayüz ve efektler (Blender → atlas)

Sahip: code-lead (teknik sanatçı) · Tarih: 2026-11-02 · Kararlar: **D-099** (sabit saha), **D-100** (3B görsel dil:
Blender 4.0.2 bpy ile prosedürel model, Cycles CPU ön-render, Unity'de sprite + 2.5B tween), **D-101** (Usta Kral) ·
Sözleşmeler: `docs/ART_3D.md` (görsel dil, rig'ler, kabul), `art/3d/spec/frames.json` (kare ve klip listesi),
`docs/ART_UNITY.md` §3 (`atlas.json` biçimi — değişmedi) · Karakter hattı: `art/3d/blender/king/README.md` (B1k).

Bu paket v4 SVG atlasının **488 kare adını aynen koruyarak** pikselleri Blender render'ıyla değiştirir (bloklar, tahta,
arayüz kiti, ikonlar, arka planlar, parçacıklar) ve yeni kareleri / klipleri (`fx_*` dizileri, `icon_*_xl`, ana sayfa
paralaks katmanları) ayrı bir sayfa takımına (`Resources/art3d/fx.json` + `fx_N.bytes`) koyar. Unity kodu
değişmeden 3B sürümü çizer: kare adı, tasarım boyu, pivot ve 9-dilim kenarı aynıdır; yalnız saklanan çözünürlük
(`scale`) bellek bütçesi için kare başına ayarlanır (`DesignW = w / scale` değişmez).

Üretken yapay zekâ yok, satın alınmış / indirilmiş varlık yok, referans görüntülerinden piksel yok: her model `bpy` /
`bmesh` ilkelleriyle (süperelipsoid, ekstrüze silüet, bezier tüp, pah) kurulur; dokular Blender düğümleridir (gürültü,
dalga, şerit, renk rampası); blok sembolleri kendi v4 SVG'lerimizden maskedir.

## 1. Komutlar (repo kökünden)

| Adım | Komut | Süre (4 çekirdek, ölçüldü) |
| ---- | ----- | -------------------------- |
| Tümü | `WEB=../minik-usta bash art/3d/blender/env/render_all.sh` | ≈ 95 dk |
| Sembol maskeleri | `node tools/art/symmasks.mjs --web ../minik-usta` → `art/3d/out/env/sym/` | 2 sn |
| Bir aile | `blender -b --factory-startup -noaudio --python art/3d/blender/env/<aile>.py -- --out art/3d/out/env/raw [--only ad,önek*] [--quick]` | bloklar ≈ 50 dk, tahta 3, arayüz 12, ikon 8, arka plan 8, efekt 10 |
| Son işlem | `python3 art/3d/blender/env/post_env.py [--only …]` | 30 sn |
| Ana atlas | `node tools/art/rasterize.mjs --web ../minik-usta` (3B PNG varsa SVG'nin yerine geçer) | 15 sn |
| Yeni kareler | `node tools/art/packfx.mjs` → `Resources/art3d/fx.json` + `fx_N.bytes` | 5 sn |
| Önizleme | `cd dotnet && MINIK_PREVIEW_DIR=../artifacts/preview dotnet test --filter ScreenDumpTests` (sahneler `artifacts/preview/scenes/`) sonra `node tools/art/compose.mjs --web ../minik-usta --prefix v5-` | 40 sn |

`--quick` yarı çözünürlük ve 1/8 örnekle poz / yerleşim denetimidir. Ara çıktılar (`art/3d/out/`) git dışıdır;
repoya yalnız `Resources/art/atlas.json` + `atlas_N.bytes` ve `Resources/art3d/fx.json` + `fx_N.bytes` girer.

Ortam (bu makinede ölçüldü, B1k ile aynı): apt Blender 4.0.2, Cycles CPU, `use_denoising = False` (OIDN yok), EEVEE
başsız çalışmaz; görünüm dönüşümü **Standard**; uyarlamalı örnekleme eşiği 0,02; 2× (bloklarda 2×, arayüzde 3×) süper
örnekleme + ön-çarpımlı alfa üzerinde LANCZOS küçültme; 4 px alfa taşırma. Python tarafı standart kütüphane + Pillow;
paketleyiciler yalnız `node:zlib` + web reposundaki Playwright / Chromium (yeni bağımlılık yok).

## 2. Dosyalar

| Dosya | Ne |
| ----- | -- |
| `common.py` | sahne, Cycles ve renk yönetimi, rig A / C / D ışıkları, `Screen` (ekran-uzayı modelleme), malzeme ve geometri yardımcıları |
| `blocks.py` | 8 renk × 16 maske karo, 128 iç köşe dolgusu, 8 renk körü çıkıntı kapağı, `blk_gloss`, `blk_locked`, 4 yükleme tuğlası |
| `board.py` | ahşap palet saha zemini, beton tuğla duvar, ikaz başlıkları, geçit, ray, iskele borusu / kirişi / kelepçe, tepsi (9-dilim), vinç kancası, kamyon |
| `ui.py` | düğmeler (birincil L / M, ikincil, tehlike, teklif, pasif, araç, güçlendirici, yuva; normal + basılı), kapat ×, satın al +, rozetler, boncuklar, çukur kapsüller, HUD plakaları, pencere, kurdele, ilerleme, anahtar, konuşma balonu, gezinme |
| `icons.py` | 26 ikon + `ui_glove` + 8 `icon_*_xl` (aynı modeller büyük) |
| `bg.py` | ana sayfa "Renkli Tepe" 7 paralaks katmanı (tek perspektif sahne, katman başına render) + `bg_level` |
| `fx.py` | 12 efekt klibinin çizimleri + v4 parçacık kareleri (`fx_chip`, `fx_confetti`, `fx_confetti_round`, `fx_dust`, `fx_ring`, `fx_spark`, `fx_star`) |
| `post_env.py` | 2× → saklanan boy, gölge / temas elipsi / ışıltı (luma-alfa), alfa taşırma, katman kırpma, `bg_home` düzleştirme, `chr_dede_*` takma adlarının Usta Kral'dan üretimi |
| `scales.json` | kare başına saklanan ölçek + yeni karelerin tuval / pivot / klip bilgisi (bellek bütçesi buradan ayarlanır) |
| `render_all.sh` | bütün hat |
| `../../../../tools/art/symmasks.mjs`, `rasterize.mjs` (3B girişi), `packfx.mjs`, `compose.mjs` (`--prefix`) | araçlar |

## 3. Model ve render kuralları (ART_3D'nin uygulaması)

- **Bloklar (rig A, 35° eğik ortografik)**: hücre kutuları ekran uzayında kurulur (üst yüz `[j + 0,2, j + 1]`, ön dudak
  `[j, j + 0,2]`; altında komşu varsa üst yüz `j`'ye iner). Hücreler **voksel bölünür** (ana kutu + 0,2 satırlık uzantı):
  komşu iki hücrenin ortak duvarı her zaman çakışan bir yüz çiftidir → **kaynaştırmadan önce** silinir (Blender'ın
  `remove_doubles`'ı çiftin birini sessizce iç yüz olarak bırakıyordu, ilk denemede L parçada gölgeleme çizgileri yaptı),
  sonra kaynak + `dissolve_limit` + dikey köşelere ağırlıklı pah (r 0,11) + bütün dış kenarlara açı pahı (0,10, 5
  segment) + **Weighted Normal** (büyük düz yüz düz kalır; n-gen gölgeleme çizgisi yok). Boolean ve harden normals yok.
  Malzeme ART_3D §2.1 tablosu (W kaplamasız). Sembol: v4 sembol SVG'sinin mürekkep katmanı 512² maske → çıkıntı
  tepesinde renk karışımı + kabartma (bump). Karo = 3 × 3 bağlam render'ının merkez hücresi (360 px → 180 px);
  iç köşe dolgusu = çaprazı da olan 2 × 2 bağlamın köşesi (96 px → 48 px).
- **Işık**: rig A'nın anahtar / dolgu güneşleri aynen; kenar ışığı alan ışığı yerine **güneş** (konumdan bağımsız:
  bütün karolar aynı ışığı görür, dikiş yok). Rig D'de (arayüz, 20°) anahtar ışık yalnız eğim ekseninde ve kameranın
  **önünden**: ortografik kamera + güneş bir düz yüzün her noktasına aynı yarı-vektörü verir; anahtar ayna yönüne yakın
  olunca bütün plaka beyaza patlıyordu (ilk deneme). Düz plakalar x boyunca birörnek → 9-dilim orta sütun / satır düzgün.
- **Arayüz anatomisi** (ART_REFERENCE §3.1, v4 SVG ölçüleriyle): koyu **kontur levhası** (tam kare, alçak) → üstünde
  içeri çekilmiş **gövde** (ön duvarı renkli dudak: L 40, M 28, güçlendirici 20 tasarım px) → yükseltilmiş **plaka**
  (dikey renk rampası + üstte parlak bant) . Basılı hâl: dudak × 0,35, üst kenar aşağı (v4 ile aynı kayma), gölge yok.
  Damla gölgesi `post_env` ile (meta `shadow`).
- **İkonlar (rig C)**: 28° yukarıdan 3/4, kamera nesnenin izdüşüm kutusuna **otomatik sığdırılır** (%86 doluluk).
  Sikkede amblem **Altın Mala**; taç yok (ART_3D §11).
- **Arka plan (rig F)**: tek perspektif sahne (12° aşağı, 62° düşey görüş); her paralaks katmanı yalnız kendi nesneleri
  kameraya görünür, diğerleri gölge / sekme için sahnede kalır → katmanlar arka-öne birleşince aynı resim. `bg_home`
  = katmanların düzleştirilmişi (v4 kodu bunu çizer). `bg_level`: çivit + radyal parlama + ozalit ızgarası α ≈ 0,035 +
  kenarlarda alan derinliği bulanık vinç / iskele siluetleri; tepsinin arkası boş.
- **Efektler (rig E, önden)**: parlamalar yayıcı düzlem + radyal alfa (bloom yok); boyanan efektler (boya, konfeti, toz,
  çip, halka, kıvılcım, yıldız) **beyaz / açık gri** render (kod tint'ler, gölgeleme kalır). Halkalar karararak değil
  alfa ile söner.
- **Usta Kral takma adları**: `chr_dede_idle` / `chr_dede_talk` / `chr_dede_bust` v4 kare boyu ve pivotuyla, B1k
  sayfasındaki `chr_king_idle_00` / `chr_king_talk_06` / `chr_king_bust_neutral` çiziminden pivotla yerleştirilip
  yeniden örneklenir (ART_3D §9: eski adlar Usta Kral olarak çizilir) → ana sayfa ve öğretici bugünkü kodla 3B kralı
  gösterir. Tuna ve Kepçe karelerinin 3B'si bu paketin dışında (v4 SVG'si kaldı).

## 4. Sözleşme (kod bunları okur)

### 4.1 `Resources/art/atlas.json` + `atlas_N.bytes` (v4 biçimi, değişmedi)

- **488 kare adı aynen**; `w / h` = saklanan px, `scale` = saklanan px / tasarım px, tasarım boyu `w / scale` v4 ile
  aynı; `pivotX / pivotY`, 9-dilim kenarları (tasarım px × `scale`), blok `anchorX / anchorY / studD / symD` alanları
  aynı yerde. Ek üst alan `art3d: {doc, frames3d}` (okuyucu yok sayar).
- 3B pikselli kare: **365** = blok 266 (128 karo + 128 iç köşe + 8 `stud_*` + `blk_gloss` + `blk_locked`) + tahta 13 +
  arayüz 42 + ikon 31 (26 ikon + `ui_glove` + 4 yükleme tuğlası) + parçacık 7 + arka plan 2 + kasaba 1 + `chr_dede_*` 3
  (Usta Kral). v4 SVG'si kalan: plan hayaleti 18, düşüş gölgesi
  80, sembol 8, `floor_site*`, `crane_band`, `px_white`, `fx_glow / fx_rays / fx_sheen / fx_drop_blob`, `logo_*`,
  `flag_*`, `chr_tuna_*`, `chr_kepce_idle` (ART_3D `keep2d` + karakter paketi dışı).
- **Ölçek değişiklikleri** (`scales.json`): bloklar ve blok-boyu tahta karoları **1,25** (150 px / hücre; v4 1,5 →
  `ArtContractTests` 125 px / birim olarak sabitler), tepsi 1,5 (9-dilim kenarı sabit), arayüz / ikon / karakter-dışı
  çoğu kare **1,0**, `bg_home` 0,5, `town_ch1_treehouse` 0,6, `chr_dede_idle/talk` 0,7, `chr_dede_bust` 0,75. Tasarım
  boyu, pivot ve kenar değişmediği için `BoardLayout`, `BlockSkin`, `UiRenderer` aynen çalışır.
- Blok dudağı 24 tasarım px (v4 20): `anchorY` alt komşusuz 0,6688, alt komşulu 0,5688; `studD` 54, `symD` 44,4.

### 4.2 `Resources/art3d/fx.json` + `fx_N.bytes` (yeni)

```json
{ "version": 1, "ppu": 100, "pad": 2, "origin": "top-left", "pivotOrigin": "bottom-left", "memoryBytesRGBA32": …,
  "atlases": [ { "file": "art3d/fx_0", "w": 2048, "h": 700, "use": "level" }, { "file": "art3d/fx_1", "w": 1684, "h": 552, "use": "home" } ],
  "frames": { "fx_hammer_06": { "atlas": 0, "x": …, "y": …, "w": …, "h": …, "pivotX": …, "pivotY": …, "borderL": 0, …,
                                "scale": 0.5, "ox": …, "oy": …, "cw": 160, "ch": 160 }, … },
  "clips":  { "fx_hammer_smash": { "frames": ["fx_hammer_00", …], "fps": 24, "loop": false, "out": null,
                                   "events": { "6": "impact" }, "canvas": [320, 320], "pivot": [0.8, 0.1], "scale": 0.5,
                                   "blend": "normal", "use": "…" }, … },
  "layers": { "bg_home_town": { "frame": "bg_home_town", "parallax": 0.35, "z": 3, "x": 0, "y": 1064, "w": 1080, "h": 570 }, … } }
```

- Kareler **kırpılmıştır** (alfa kutusu + 1 px). `pivotX / pivotY` klibin tuval pivotudur, kırpılmış dikdörtgene göre
  (0..1 dışına çıkabilir; Unity `Sprite.Create` kabul eder). `Sprite.Create(tex, rect, new Vector2(pivotX, pivotY),
  ppu * scale)` ile çizilen her çizim aynı pivot noktasına oturur — kutuya sığdırma (box-fit) **yapılmaz** (B1k ile
  aynı kural). `ox, oy` = kırpılmış dikdörtgenin tam tuvalin sol altından uzaklığı (saklanan px), `cw × ch` tam tuval.
- `clips.<ad>.frames[i]` = 24 (kilit parıltısı 60) fps zaman çizgisinin i. karesinde gösterilen çizim (frames.json
  `timeline`), `events` kare no → olay adı; süre JUICE'tan gelir (kod çizimleri JUICE süresine yayar).
- `layers.<ad>`: ana sayfa paralaks katmanının 1080 × 1920 tasarım tuvalindeki yeri (sol alt köken, tasarım px),
  `parallax` çarpanı, `z` sırası (0 gök … 6 sağ ön plan). Katmanlar sırayla üst üste konunca `bg_home` çıkar.
- Sayfalar kullanım yerine göre ayrılır: `use: level` (efekt klipleri + `icon_*_xl`), `use: home` (paralaks
  katmanları); ekran yalnız kendi sayfasını yükleyebilir.

| Klip (fx.json) | Çizim | fps | Döngü | Olay | Saklanan ölçek | Not |
| -------------- | ----- | --- | ----- | ---- | -------------- | --- |
| `fx_crane_grab` (`fx_crane_claw_NN`) | 8 | 24 | yok | 5 `grab` | 0,6 | pençe açık → kapalı; bırakma = ters oynatma |
| `fx_hammer_smash` (`fx_hammer_NN`) | 11 | 24 | yok | 6 `impact` | 0,5 | sap ucu pivotlu savrulma, hazırlık + geri tepme |
| `fx_impact_star` | 8 | 24 | yok | 0 `burst` | 0,375 | beyaz çekirdek + altın hale |
| `fx_paint_splash` | 12 | 24 | yok | 3 `splat` | 0,375 | beyaz: yeni blok rengiyle tint |
| `fx_trowel_sparkle` | 12 | 24 | 0–11 | 0, 6 `glint` | 0,625 | altın mala ±15° + parıltı |
| `fx_coin_spin` (`fx_coin_NN`) | 12 | 24 | 0–11 | 0 `face` | 0,75 | iki yüzü malalı sikke 360° |
| `fx_star_burst` | 14 | 24 | yok | 0 `burst` | 0,3125 | halka + 16 yıldız, alfa ile söner |
| `fx_confetti3d_rect` / `_round` | 8 / 8 | 24 | 0–7 | — | 1,5 | beyaz, palet tint |
| `fx_dust_puff` | 10 | 24 | yok | 0 `puff` | 0,5 | krem / gri tint, pivot alt orta |
| `fx_segment_done` | 14 | 24 | yok | 0 `flash`, 6 `fly` | 0,3125 | dilim parlaması + halka + kıvılcım |
| `fx_lock_shine` | 12 | **60** | yok | — | 1,0 | çıkıntı tepesinde parıltı, hücreler arası 40 ms |

### 4.3 Usta Kral takma adları (atlas)

| Atlas karesi (v4 adı, boy ve pivot aynı) | Kaynak (`art3d/king.json`) | Ölçek |
| ---------------------------------------- | -------------------------- | ----- |
| `chr_dede_idle` 300 × 480, pivot 0,5 0,02 | `chr_king_idle_00` (K0) | 0,7 |
| `chr_dede_talk` 300 × 480, pivot 0,5 0,02 | `chr_king_talk_06` | 0,7 |
| `chr_dede_bust` 256 × 320, pivot 0,5 0,5 | `chr_king_bust_neutral` | 0,75 |

Kod `king.json`'a geçince (klip oynatma, ART_3D §7.2) bu üç kare silinebilir; o zamana kadar ana sayfa ve öğretici
portresi 3B Usta Kral'ı gösterir. Ayak altındaki temas gölgesi v4 pivotunun altında 5 px'te yumuşakça kesilir.

## 5. Ölçümler (bu pakette, `measure_blocks.py` + paketleyici çıktısı)

| Ölçüt | Eşik (ART_3D) | Ölçülen |
| ----- | ------------- | ------- |
| Blok üst yüzü ΔE00 (8 renk) | ≤ 8 | en kötü **6,51** (P); W 5,28, R 5,40, O 4,25, C 3,49, B 3,25, Y 2,01, G 1,98 |
| Dudak / taban | 0,70–0,85 | **0,85–0,93** (dolgu ışığı dudağı biraz açık bırakıyor; bilinen sapma, §7) |
| Sembol kontrastı (mürekkep çekirdeği ↔ blok yüzü) | ≥ 3:1 | en kötü **3,32:1** (B); R 3,86 (saf beyaz mürekkep + hafif ışıma) |
| Renk körlüğü (Machado 1,0, ölçülen üst yüzler) | her koşulda en kötü çift ≥ 12 | protan W–R **13,2**, deutan Y–O **12,7**, tritan R–O **13,0** |
| Dikiş (12 parça × 8 renk, karolardan birleşik) | ort. ≤ 6/255 | ort. **1,08/255** (karo içi komşu sütun 0,72/255); en çok 50/255 — çapraz komşusuz iç köşede geometrik kenar (v4 kuralı), dikiş değil |
| Ana atlas | sayfa ≤ 2048² | 3 sayfa 2048², 2048², 2040 × 804 → **38,26 MB** RGBA32 (v4: 4 sayfa 63,4 MB) |
| `fx.json` | — | 2 sayfa 1920 × 752 (level), 1684 × 584 (home) → **9,26 MB** |
| Toplam doku belleği (atlas + fx + Usta Kral 15,58) | ≤ 64 MB | **63,10 MB** (hepsi aynı anda yüklü; ekran bazlı yüklemede oyun ekranı ≈ 59 MB, ana sayfa ≈ 57 MB) |
| WebGL indirme artışı (PNG) | ≤ 15 MB | ana atlas 4,08 → **6,44 MB**, fx **1,98 MB** → bu paket **+4,34 MB** (B1k kral sayfaları dahil +10,1 MB) |

## 6. Öz puan (ART_REFERENCE §10 + hareket; kompozitler `artifacts/preview/v5-*.png`, 1080 × 1920 + 390 × 844 + 360 × 800)

Kompozitler Unity kodunun kendi sahne dökümünden (`ScreenDumpTests`) yalnız paketlenmiş atlas kareleriyle çizilir
(`compose.mjs`): yerleşim gerçek koddur, Unity'de görülmüş değildir.

| Ekran | Hacim / ışık | Renk / kontrast | Tipografi | Düzen / boşluk | Karakter / sahne | İkon / rozet | Hareket | Kabul |
| ----- | ------------ | --------------- | --------- | -------------- | ---------------- | ------------ | ------- | ----- |
| `v5-level` | 5 | 4 | 4 | 4 | 4 | 5 | 4 | evet |
| `v5-home` | 5 | 4 | 4 | 4 | 4 | 5 | 4 | evet |
| `v5-win` | 5 | 4 | 4 | 4 | 4 | 5 | 4 | evet |
| `v5-lose` | 5 | 4 | 4 | 4 | 4 | 5 | 4 | evet |
| `v5-settings` | 5 | 4 | 4 | 4 | — | 5 | — | evet |

- **Hacim / ışık 5**: bloklar, tahta, düğmeler, pencere, kurdele, ikonlar ve sahne aynı sol-üst sıcak anahtar + soğuk
  dolgu + kenar ışığıyla gerçek 3B; dudak, pah, temas gölgesi, basılı hâl (dudak × 0,35) render'dan gelir.
- **Renk 4**: D-083 paleti ΔE00 ≤ 6,5 ile korundu; yeşil = birincil, kırmızı ×, altın ödül. 5 değil: blok dudağı
  hedeften açık (0,85–0,93), saha zemini (ahşap palet) blok kahverengisiyle (W) aynı aileden.
- **Tipografi 4**: değişmedi (kodun Baloo 2 + kontur stili).
- **Düzen 4**: ScreenLayout aynen (kare adı / boyu / pivotu değişmedi); ana sayfada kasaba başlık kurdelesi ve ilerleme
  çubuğunun altına, karakterlerin arkasına indirildi (kamera 6° aşağı).
- **Karakter / sahne 4**: 3B Usta Kral (ana sayfa, öğretici portresi), 3B kasaba (paralaks katmanlı), 3B ağaç ev,
  `bg_level` alan derinliği. 5 değil: Tuna ve Kepçe hâlâ v4 vektör (3B'leri bu paketin dışında); oyun köşesinde ve
  kazanma penceresinde kodun Usta Kral kliplerine geçmesi gerekir (ART_3D §13.3).
- **İkon / rozet 5**: her ikon küçük 3B oyuncak illüstrasyon (3 ton + spekülar + yumuşak gölge), 26 + 8 xl.
- **Hareket 4**: 12 efekt klibi hazırlık, aşma, yavaş giriş-çıkış ve yay ilkeleriyle (`v5-fx.png`: çizimler pivotla
  tuvalde); 5 değil: Unity'de oynatılmadı (fx.json'u okuyan kod yok), blok tween'leri (`blk_lift` …) kodun işi.

Düzeltme turu (bu pakette, ≥ 4 olmayan bulgular): (1) arayüz rig'inde anahtar ayna yönündeydi → plakalar beyaza
patlıyordu; (2) dikey dudaklar stüdyo ufkunu yansıtıp griye dönüyordu → koyu ufuk + mat dudak; (3) kapsül / kuyu
zemini parlak → mat koyu; (4) × ve + işaretinin çakışan yüzleri birbirini gölgeliyordu → 1,5 px kademe; (5) tepsinin
alt yüzü deliği kapatıyordu → halka alt yüz; (6) ana sayfada çayır ile tepe arasında kırık çizgi → tek yükseklik
fonksiyonu + uzak katmanlar "holdout"; (7) kasaba UI'nin arkasında kalabalıktı → kamera 6°; (8) sikke koyu → şeker
altın; (9) çekiç ilk karelerde tuvalden taşıyordu → savrulma açısı kaydırıldı; (10) R sembolü 2,99:1 → saf beyaz.

## 7. Bilinen sorunlar ve sonraki adımlar

1. **Kod bağlantısı yok**: `fx.json` (klipler, `icon_*_xl`, paralaks katmanları) ve `king.json` için okuyucu / oynatıcı
   yazılmadı (bu paket yalnız sanat + araç). Mevcut kod v4 adlarıyla 3B pikselleri çizer; yeni klipler bekler.
2. **Blok dudağı** hedeften açık (0,85–0,93; ART_3D 0,70–0,85): `rig_A` dolgu gücü 1,1 → ≈ 0,8 ile yeniden render
   (≈ 50 dk) önerilir; okunurluk ve dikiş ölçütleri şimdiden geçiyor.
3. **Tuna, Kepçe, Bay Gribeton** 3B değil (v4 SVG); `logo_emblem` ve `logo_wordmark` v4 (kelime işareti değiştirilebilir
   kare kalır, D-068).
4. **Ölçek değişikliği**: bloklar 1,5 → 1,25 (bütçe için); `ArtContractTests` sabitleri güncellendi (125 px / birim,
   1–4 sayfa, ≤ 48 MB). ART_UNITY §3.3 tablosu (design-lead) bu değerlere göre güncellenmeli.
5. **Rig F** kamera eğimi 12° yerine 6° (ana sayfa düzeni için); paralaks katman boyları içerikten ölçülür
   (`fx.json → layers`), frames.json'daki önerilen boylardan farklıdır (yeni kareler, kod henüz okumuyor).
6. Render sırasında makinede başka bir ajan CPU kullanıyordu; tablodaki süreler bu yüzden üst sınırdır.

## 7. Sanat turu 1 (2026-11-02, design-lead) — değişen aileler

Ayrıntı ve kod sözleşmesi `docs/ART_3D.md` §14. Yalnız değişen kareler yeniden render edildi; geri kalan ham
render'lar B1 ile bit bit aynıdır (atlas yeniden üretimi B1 commit'iyle doğrulandı).

| Aile | Dosya | Değişiklik |
| ---- | ----- | ---------- |
| Tahta | `board.py` `floor_yard()` | ahşap palet → **gömük karanlık yuva** (#221C5A / #25205F, 5 px kenar #3A3480, 10 px iç duvar) |
| Blok | `blocks.py` `blk_locked` | krem harç noktaları → küçük çelik perçin (Ø 0,068 hücre) |
| İkon | `icons.py` | `icon_sound_on/off` tıknaz hoparlör, `icon_vibration` derin gövdeli telefon, `icon_motion` oyuncak salyangoz, `flag_tr/en` 3B rozet (bayrak yüzü saf Python ile rasterleştirilir: apt Blender'da numpy ve Pillow yok); bu altı kareye `shadow` meta (dy 5, blur 3, α 0,38) |
| Efekt | `fx.py` | `fx_star_burst` ve `fx_segment_done`: 14 / 12 px kalın degrade halka (`thick_ring`) + 3B şeker-altın yıldızlar (`gold_star_mat`, rig C ışığı); `fx_lock_shine` 3–6. karelerde tam, 1,2×; `fx_dust_puff` sıcak kum tonu, 9 tane |
| Kasaba | `town.py` | ağaç ev yeniden modellendi (gerçek ağaç + 10 parça); `town_ch1_s00`, `town_ch1_p01..p10` (öncekiler holdout), `town_ch1_treehouse` (bitmiş), `bld_<parça>_3d` × 10; `scales.json` → `newFrames` (`group: town` = home sayfası, `group: reward` = level sayfası); `packfx.mjs` → `fx.json` `stages.town_ch1` |
| Arka plan | `bg.py` | `bg_home_fore_r` tuğla paleti → çalı kümesi (kenarda yarım kesik nesne kalmadı); `bg_home_ground` → Oyna düğmesinin altında arnavut kaldırımı meydan; `bg_home` (düzleştirilmiş, yalnız yedek) ölçeği 0,5 → 0,25 |
| Karakter durağanları | `post_env.py` `king_aliases('crew', …)` | `chr_tuna_cheer / idle / think / bust`, `chr_kepce_idle` 3B (crew.json çizimlerinden pivotla); ölçekler 0,84 / 0,7 / 0,7 / 0,75 / 0,75 |

Komut (bu turun yeniden render'ı, ≈ 40 dk): `board.py --only floor_yard_a,floor_yard_b`, `blocks.py --only blk_locked`,
`icons.py --only icon_sound_on,icon_sound_off,icon_vibration,icon_motion,flag_tr,flag_en`, `fx.py --only
'fx_star_burst_*,fx_segment_done_*,fx_lock_shine_*,fx_dust_puff_*'`, `town.py`, `bg.py --only bg_home_ground,bg_home_fore_r`
→ `post_env.py` → `rasterize.mjs` → `packfx.mjs`.
