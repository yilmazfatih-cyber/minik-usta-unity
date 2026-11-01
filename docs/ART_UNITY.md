# ART_UNITY — Unity sanat paketi v4 (paket V0)

Sahip: design-lead · Tarih: 2026-11-01 · Karar: D-098 (Royal Match / Block Blast çıtası, ücretsiz, üretken yapay zekâsız
kendi SVG sanatımız) · Stil kitabı: web reposu `docs/ART_REFERENCE.md` v1.1 (§2 renk, §3 bileşen, §5 blok, §6 karakter,
§7 arka plan, §8 efekt, §10 rubrik, §11 kopyalanabilir / kopyalanamaz) · Kaynak tarifler: web `docs/ART_DIRECTION.md`
(§2.1 blok paleti D-083 değişmedi, §3A blok, §4 şantiye, §5 duvar / geçit / vinç alanı, §14 UI kiti),
`src/theme/tokens.json` (`blockV2`, `kit.v3.*`), `public/art/**` (web v3 SVG'leri; buraya kopyalanıp yükseltildi).

> Bu belge Unity tarafının **çizim kontratıdır**: hangi kare var, atlasta nasıl durur, hangi ekranda nereye konur. Kod
> (code-lead) yalnız `Assets/MinikUsta/Resources/art/atlas.json` + `atlas-N.png.bytes` okur; SVG ve araçlar derlemeye
> girmez.

---

## 1. Özet

| Ne | Değer |
| -- | ----- |
| Kare sayısı | **488** (blok 274, plan hayaleti 18, düşüş gölgesi 80, tahta 16, UI 43, ikon 33, efekt 11, karakter 8, arka plan 2, kasaba 1, logo 2) |
| Atlas | **4 sayfa**, ≤ 2048 × 2048 (2048², 2044², 2048², 2044 × 1968), doluluk %91,8, PNG toplam **3,9 MB** (kayıpsız yeniden sıkıştırılmış) |
| Ölçek | varsayılan **1,5** raster px / tasarım px (120 px karo → 180 px); kare başına `scale` (bkz. §3.3) |
| Kaynak | `art/gen/*.mjs` (üretici, design-lead) + `art/src/**` (elle düzenlenen / web v3'ten kopyalanıp yükseltilen SVG) → `art/svg/**` (derlenmiş SVG kaynakları, incelemeye açık) |
| Araçlar | `tools/art/rasterize.mjs` (SVG → atlas), `tools/art/preview.mjs` (kompozit mock), `tools/art/png.mjs` (kayıpsız PNG sıkıştırıcı) |
| Önizleme | `artifacts/preview/v4-level.png`, `v4-home.png`, `v4-win.png`, `v4-sheet.png` (git dışı klasör; bu dört dosya `git add -f` ile depoda) |
| Bağımlılık | yeni npm/NuGet bağımlılığı **yok**: Playwright + Chromium web reposunun `node_modules`'undan (`--web ../minik-usta`) ve `/opt/pw-browsers/chromium`'dan; yazı tipi Baloo 2 (OFL 1.1) web reposundan |

## 2. Boru hattı ve komutlar

```
node art/gen/build.mjs            # art/gen + art/src → art/svg/<kategori>/<kare>.svg (488 dosya; ad denetimi, filter/mask/image yasağı)
node tools/art/rasterize.mjs      # art/svg → Assets/MinikUsta/Resources/art/atlas-N.png.bytes + atlas.json  (~15 s)
node tools/art/preview.mjs        # atlas'tan kompozit mock → artifacts/preview/v4-{level,home,win,sheet}.png
```

- `rasterize.mjs` seçenekleri: `--web <yol>` (varsayılan `../minik-usta`), `--pad 2` (kenar boşluğu, kenar pikseli
  uzatılarak doldurulur), `--max 2048`, `--out <klasör>`, `--png <klasör>` (inceleme için düz `.png` kopyaları).
- SVG içindeki `<text>` (yalnız `logo_wordmark`) DOM'da çizilir (sayfaya yüklenen Baloo 2); diğerleri `Image` →
  `canvas.drawImage` ile hedef boyutta vektörden raster'lanır. Paketleyici skyline (sol alt sezgisi), deterministik.
- Kare meta verisi SVG kökündedir: `width/height` = **tasarım px**; `data-scale`, `data-pivot "x y"` (Unity, 0..1,
  sol alttan), `data-border "L T R B"` (tasarım px), `data-anchor "x y"`, `data-stud`, `data-sym`.
- Final sanat yerine geçecek bir kareyi değiştirmek: aynı adla `art/src/<kategori>/<ad>.svg` yaz (ya da üreticide
  düzenle) → üç komutu koş. Ad, boyut ve pivot korunursa kod değişmez. **Logo** (`logo_wordmark`) bu yolla tek dosyayla
  değişir (mağaza adı D-033 / D-068 ile seçilince).

## 3. `atlas.json` kontratı

```json
{
 "version": 4, "ppu": 100, "scale": 1.5, "pad": 2, "origin": "top-left", "pivotOrigin": "bottom-left",
 "atlases": [ { "file": "art/atlas-0.png", "w": 2048, "h": 2048 }, … ],
 "frames": {
  "blk_y_00": { "atlas": 0, "x": 2, "y": 2, "w": 180, "h": 180, "pivotX": 0.5, "pivotY": 0.5,
                "borderL": 0, "borderT": 0, "borderR": 0, "borderB": 0, "scale": 1.5,
                "anchorX": 0.5, "anchorY": 0.5833, "studD": 54, "symD": 44.3 }, …
 }
}
```

### 3.1 Alanlar ve birimler

| Alan | Birim / kural |
| ---- | ------------- |
| `atlas` | sayfa sırası (0…N−1); dosya `Resources/art/atlas-<atlas>.png.bytes` |
| `x, y, w, h` | atlas pikseli, **sol üst köken** (PNG satır sırası). Unity `Rect`: `new Rect(x, H − y − h, w, h)` (`H` = sayfa yüksekliği, `atlases[i].h`) |
| `pivotX, pivotY` | 0..1, **sol alttan** (Unity `Sprite.Create` pivotu ile aynı) |
| `borderL/T/R/B` | 9-dilim kenarları, **atlas pikseli**. Unity `Vector4` sırası **(L, B, R, T)**: `new Vector4(borderL, borderB, borderR, borderT)` |
| `scale` | raster px / tasarım px. **Sprite PPU = `ppu × scale`** (ör. 1,5 → 150): böylece 180 px'lik karo dünyada 1,2 birim = 120 tasarım px olur; uGUI `Image` (Canvas referans PPU 100) 9-dilim kenarlarını da tasarım px'te çizer |
| `anchorX, anchorY` | yalnız blok karoları: çıkıntı (stud) merkezi, 0..1 sol alttan; sembol / parlama / renk körü kapağı buraya konur |
| `studD`, `symD` | yalnız blok karoları: çıkıntı çapı ve sembol kutusu, **tasarım px** (120 px hücrede; hücre `c` ise × c / 120) |

`JsonUtility` sözlük okuyamaz; `frames` bir nesne-haritasıdır → `MinikUsta.Core` `JsonReader` ile okunur.

### 3.2 Unity'de yükleme (öneri — code-lead uygular, API'ler stub + gerçek imza ile doğrulanmalı)

1. `Resources.Load<TextAsset>("art/atlas")` → `.text` → JSON.
2. Her sayfa: `Resources.Load<TextAsset>("art/atlas-0.png")` (Unity yalnız son uzantıyı `.bytes` düşer; ad
   `atlas-0.png` kalır — P3-13'teki "noktalı Resources adı" şüphesi burada da geçerli: CI ilk koşuda doğrulanmalı;
   gerekirse araç `--out` ile adları `atlas_0.bytes` biçimine çevirmek tek satırlık değişiklik).
3. `var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false); tex.LoadImage(bytes);` (`ImageConversion.LoadImage`
   genişletme yöntemi, ImageConversionModule), `filterMode = Bilinear`, `wrapMode = Clamp`.
4. Kare: `Sprite.Create(tex, rect, new Vector2(pivotX, pivotY), ppu * scale, 0, SpriteMeshType.FullRect,
   new Vector4(borderL, borderB, borderR, borderT))`.
5. Bellek: 4 sayfa × 16 MB (RGBA32, sıkıştırmasız) ≈ **64 MB GPU**. Sayfa 3 (`atlas-3`) yalnız ana sayfa / kazanma
   arka planlarını taşıyacak biçimde ayrılırsa oyun sahnesinde 3 sayfa yeter — bkz. §8 madde 1.

### 3.3 Ölçekler (tek ölçek ilkesi, bilinçli istisnalar)

| Grup | `scale` | Neden |
| ---- | ------- | ----- |
| Bloklar, plan hayaletleri, tahta karoları, UI kiti, ikonlar, kıvılcım / yıldız / konfeti | **1,5** | tek ölçek; 390 pt'de DPR 3 → hücre ≈ 130 ekran px, 180 px karo ≈ 0,72× küçültme (mip gerekmez) |
| Karakterler (tam boy) | 1,25 | ekranda en çok ≈ 1,1× tasarım boyunda; atlas alanı |
| Büst karakterler, logo, düşüş gölgesi çizgileri, vinç bandı, halka, toz | 1,0 | düz renk / yumuşak kenar; 1,5 bilgi katmaz |
| Arka planlar | `bg_home` 0,75 · `bg_level` 0,375 · `town_ch1_treehouse` 0,75 | 1080 × 1920 × 1,5 sayfaya sığmaz; gradyan ağırlıklı, büyütmede bozulmaz |
| Yumuşak efektler (`fx_glow`, `fx_rays`, `fx_drop_blob`, `fx_sheen`), `floor_site_deep` | 0,5 | bulanık / düz |

## 4. Kompozisyon kuralları (kod bu kurallarla birleştirir)

### 4.1 Blok (ART §3A + §3A.5 + ART_REFERENCE §5.2)

- **Hücre karosu** `blk_<c>_<mm>`: `c` ∈ w y g r o c b p, `mm` = komşu maskesi 00–15 (bit 1 sol, 2 sağ, 4 **aşağı**
  (y − 1), 8 **yukarı** (y + 1); Presentation.Core `Atlas.Body` / `BoardPose.NeighbourMask` ile aynı). Karo hücreyi
  tam kaplar (`c × c`), parçanın tek konturu, tek yan duvarı (20 px dudak), yastık, çıkıntı ve kabartmalı sembol içinde.
  Karolar **çapraz komşusuz** çizilir: iki dik komşu varken iç köşe içbükeydir (keskin köşe, r 0).
- **Köşe dolgusu** `blk_<c>_<mm>_cf_<tl|tr|bl|br>` (128 adet): hücrenin o köşesindeki iki dik komşu **ve** çapraz
  hücre aynı parçadaysa (ör. O4'ün ortası) karonun üstüne konur; pivot = hücrenin o köşesi, boy 32 × 32 tasarım px.
  Böylece L, T, S ve O biçimleri tam parça çizimiyle piksel piksel aynıdır (doğrulama: üretici `pieceMarkup` ile
  karşılaştırıldı). `tl` = sol + yukarı + sol-yukarı.
- **Tutulabilir parlama** `blk_gloss` (K-09; ART §3A.5 6e/6f): `anchor`'a, ölçek `studD / 64 × c / 120`. Tutulamaz
  parçada çizilmez ve parça `#EBEBEB` tint alır (`blockV2.notHoldableTint`).
- **Yerleşmiş (kilitli)**: aynı karo + `blk_locked` (4 harç noktası) + tint `#EFEFEF`; parlama yok (K-14).
- **Kaldırılmış**: ölçek 1,08 + altında `fx_drop_blob` (α 0,6–0,7, bloğun 1,8 hücre genişliğinde).
- **Renk körü modu**: `stud_<c>` (çıkıntı kapağı; pişmiş sembolü örter) `anchor`'a `studD / 64` ölçekle, üstüne
  `sym_<c>` × 1,2 × `symD / 50`.
- **Sembol tek başına** `sym_<c>`: hedef çipi, öğretici, renk körü açıklaması.

### 4.2 Plan, düşüş gölgesi, şantiye

- **Plan hücresi (hayalet)** `ghost_<c>` (ART §4: altlık #BCCADD + renk %80, kesik 4 px kontur, plan mürekkebinde
  sembol); **inşa cephesi** (K-34) `ghost_<c>_front` + üstüne `ghost_front` (6 px beyaz düz kontur + parlama);
  açılmamış `?` hücresi `ghost_hidden`.
- **Düşüş gölgesi (K-18, UX §5.4)**: hücre başına `shadow_fill_<mm>` (blok rengiyle tint, α 0,25–0,30) + çizgi:
  doğru → `shadow_line_<mm>` tint `#5CF59A`; hatalı → `shadow_dash_<mm>` tint `#FF4A3D` (2 Hz nabız kodda); nötr / Zor →
  `shadow_dash_<mm>` beyaz α 0,6. Gölge karoları **çapraz komşulu** çizilir; içbükey köşede (çapraz yok)
  `shadow_<line|dash>_<mm>_cc_<köşe>` eklenir (32 px, pivot köşe). ✓ / ! / ↓ rozeti kodda (beyaz Ø44 daire +
  `icon_check` ya da metin).
- **Zemin**: saha `floor_yard_a` / `floor_yard_b` (dama, delikli pano, Ø12 delik), şantiye `floor_site` (ozalit +
  ince ızgara), plan dışı `floor_site_deep`.

### 4.3 Tahta parçaları (ScreenLayout ölçüleriyle; `k = c / 120`)

| Kare | Yerleşim |
| ---- | -------- |
| `board_tray` (9-dilim) | dış dikdörtgen = vinç alanı tepesi … tahta altı, `Board.Crane ∪ Board.Board` büyütülmüş **48 px** (sol / üst / sağ) ve **64 px** (alt). İç delik saydamdır |
| `crane_band` | `Board.Crane` boyunca yatay döşenir (yükseklik 2 c); alt kesik çizgi vinç alanı sınırıdır (K-05). Saha üstü hava ve geçit açıklığı: aynı ton (beyaz α 0,08 düz) |
| `crane_hook` | pivot üst orta; vinç alanının sol ucunda süs (ölçek 0,9 k) ve vinç güçlendirici animasyonunda |
| `wall_seg` | `WallRect(height)` içinde dikey döşenir (60 k × 120 k / satır, beton tuğla şerit) |
| `wall_cap` | pivot alt orta = duvar tepesinin ortası, `y + 6 k` (64 × 36 tasarım px, iki yandan 2 px taşar) |
| `gap_seg` + `gap_edge` | W1 açıklığı: `GapRect` içinde `gap_seg` dikey döşenir; açıklığın üst ve alt kenarına birer `gap_edge` (pivot orta, alttaki ters) |
| `rail_seg` | W1 rayı: açıklığın üst ve alt sınırında, şantiye boyunca yatay döşenir (16 px) |
| `scaffold_pipe`, `beam_seg`, `scaffold_clamp` | şantiyenin iki yanı (dikey döşeme, 16 × 1,2 genişlik), plan tepesinde tavan kirişi (ART §4), uçlarda kelepçe |
| `truck` | durum şeridindeki kamyon çipi (ölçek 0,33) ve teslimat animasyonu (pivot tekerlek altı); `fx_dust` tekerlekte |

### 4.4 HUD ve pencereler (ART_REFERENCE §9.4–9.9)

| Bölge (ScreenLayout) | Kareler |
| -------------------- | ------- |
| `LevelCapsule` (24, 24, 220 × 60) | `capsule_hud` 9-dilim + "Bölüm N" lavanta 38–40 px |
| `Goals` (satır 1) | `capsule_hud` çipleri: `icon_blocks` (82 px, kapsülden taşar) + sayı (krem) + "blok"; Usta Serisi: `bead_on` / `bead_off` (44 px) |
| `Panorama` (48, 128, 300 × 136) | `hud_well` 9-dilim; içinde plan küçük resmi (`ghost_<c>` / `blk_<c>_00` 22 px), `icon_star` / `icon_star_empty` |
| `Moves` (390, 128, 300 × 136) | `hud_moves_plate` 9-dilim; "HAMLE" krem 30 px üst bantta, sayı `bigNumber` 80–96 px plakada (y + 94) |
| `Pause` (896, 128, 136²) | `btn_tool` (basılı `btn_tool_pressed`) + `icon_pause` 80 px |
| `Boosters` (4 × 160, x 304 + 192 i, alttan 120) | `btn_booster` / `_pressed` / `_off` + ikon 112 px (`icon_hammer`, `icon_crane`, `icon_brush`, `icon_undo`; `icon_gold_trowel` ödül ve dükkân) + `badge_count` (68 × 72, sağ altta, yarısı dışarıda) ya da adet 0 iken `btn_plus` |
| `Character` (16, alttan 24, 280 × 296) | `chr_tuna_idle` (200 × 320) + `chr_kepce_idle` (190 × 131) önde |
| `Status` | kapsül + `truck` + "×1" tray'in alt kenarına biner |
| Öğretici (UX §13.1) | `bubble_speech` 9-dilim (≤ 880 × ≥ 138), Usta Dede portresi `chr_dede_bust` Ø 132 daire kırpımı, `ui_glove` |
| Pencere (§3.7) | `panel_window` 9-dilim, başlık `ribbon_title` 9-dilim (panelin üst kenarına biner), içerik `panel_plate` ya da `panel_card`, kapat `btn_close` (köşeye biner), birincil `btn_primary` L, ikincil `btn_secondary`, tehlike `btn_danger`, teklif `btn_offer`, pasif `btn_disabled`; satır içi `btn_primary_m` |
| Kazanma / ödül (§8, §9.6) | karartma `px_white` #05030D α 0,85, `fx_rays` (tint #FFE7A3 α 0,35, 0,02–0,05 tur/s), `fx_glow`, 3 × `icon_star` (orta 260, yan 200), `chr_tuna_cheer`, ödül kapsülleri (`capsule_hud` + `icon_coin` / `icon_gold_trowel`), `fx_confetti` / `fx_confetti_round` / `fx_spark` / `fx_star` (blok renkleriyle tint) |
| Ana sayfa (§9.2) | `bg_home`, `town_ch1_treehouse`, `capsule_home` + `icon_heart` (sayı kalbin içinde) / `icon_coin` + `btn_plus`, `btn_tool` + `icon_settings`, alan şeridi `ribbon_title` + `progress_track` / `progress_fill` + `icon_star`, `btn_primary` 600 × 200 (alttan 336), `nav_bar` + `nav_tab_on` + `icon_nav_*` (kilitli sekmede α 0,55 + `icon_lock`) |
| Ayarlar | `toggle_on` / `toggle_off`, `icon_sound_on` / `_off`, `icon_music`, `icon_vibration`, `icon_motion`, `flag_tr` / `flag_en` |
| Yükleme | `logo_wordmark`, `logo_emblem`, `deco_brick_*` (sembolsüz uçuşan tuğlalar), `bg_home` |

## 5. Kare aileleri

| Aile | Adet | Tasarım boyu | Ölçek | Pivot | 9-dilim (tasarım px L T R B) | Nerede |
| ---- | ---- | ------------ | ----- | ----- | ---------------------------- | ------ |
| `blk_<c>_<mm>` | 128 | 120² | 1,5 | 0,5 0,5 | — | saha, şantiye, sürükleme, panorama küçük resmi |
| `blk_<c>_<mm>_cf_<k>` | 128 | 32² | 1,5 | hücre köşesi | — | O / büyük parça iç köşeleri |
| `sym_<c>` · `stud_<c>` | 8 · 8 | 64² · 72² | 1,5 | 0,5 0,53 · 0,5 0,5 | — | renk körü modu, çip, öğretici |
| `blk_gloss` · `blk_locked` | 1 · 1 | 72² · 120² | 1,5 | merkez | — | tutulabilir parlama · yerleşmiş |
| `ghost_<c>` · `ghost_<c>_front` | 8 · 8 | 120² | 1,5 | merkez | — | plan hücresi · inşa cephesi |
| `ghost_front` · `ghost_hidden` | 1 · 1 | 120² | 1,5 | merkez | — | cephe çizgisi · `?` hücresi |
| `shadow_{fill,line,dash}_<mm>` | 48 | 120² | 1,0 | merkez | — | K-18 gölgesi (beyaz, tint) |
| `shadow_{line,dash}_<mm>_cc_<k>` | 32 | 32² | 1,0 | köşe | — | gölge iç köşesi |
| `floor_yard_a/b`, `floor_site`, `floor_site_deep` | 4 | 120² | 1,5 (deep 0,5) | merkez | — | zemin |
| `wall_seg` · `wall_cap` | 2 | 60 × 120 · 64 × 36 | 1,5 | merkez · alt orta | — | duvar |
| `gap_seg` · `gap_edge` · `rail_seg` | 3 | 60 × 120 · 60 × 22 · 120 × 16 | 1,5 | merkez | — | geçit W1 |
| `crane_band` · `crane_hook` | 2 | 120 × 240 · 96 × 160 | 1,0 · 1,5 | merkez · üst orta | — | vinç alanı |
| `board_tray` | 1 | 240² | 1,5 | merkez | 72 72 72 88 | tahta tepsisi |
| `scaffold_pipe` · `beam_seg` · `scaffold_clamp` | 3 | 16 × 120 · 120 × 16 · 28 × 24 | 1,5 | merkez | — | iskele, tavan kirişi |
| `truck` | 1 | 360 × 200 | 1,5 | 0,5 0,05 | — | kamyon |
| `btn_primary` (+ `_pressed`) | 2 | 300 × 212 (L) | 1,5 | merkez | 72 64 72 76 | birincil CTA |
| `btn_primary_m`, `btn_secondary`, `btn_danger`, `btn_offer` (+ `_pressed`), `btn_disabled` | 9 | 240 × 136 (M) | 1,5 | merkez | 60 44 60 52 | ikincil / tehlike / teklif / pasif |
| `btn_tool` (+ `_pressed`) | 2 | 136² | 1,5 | merkez | 52 52 52 60 | HUD aracı (duraklat, ayar) |
| `btn_booster` (+ `_pressed`, `_off`) · `btn_slot` | 3 · 1 | 160² · 184² | 1,5 | merkez | 56 56 56 64 · 60 60 60 68 | güçlendirici · bölüm başı seçim karesi |
| `btn_close` (+ `_pressed`) · `btn_plus` | 2 · 1 | 132² · 64² | 1,5 | merkez | — | kapat · satın al |
| `badge_count` · `badge_dot` | 2 | 72 × 76 · 56 × 60 | 1,5 | merkez | — | sayı rozeti |
| `capsule_hud` · `capsule_home` | 2 | 180 × 60 · 264 × 88 | 1,5 | merkez | 34 30 34 30 · 48 44 48 44 | sayaç kapsülleri |
| `hud_moves_plate` · `hud_well` | 2 | 300 × 136 · 200 × 136 | 1,5 | merkez | 40 56 40 40 · 36 36 36 36 | hamle, panorama |
| `panel_window` · `panel_plate` · `panel_card` | 3 | 320 × 420 · 200² · 200² | 1,5 | merkez | 96 96 96 160 · 44 44 44 52 · 44 44 44 48 | pencere |
| `ribbon_title` | 1 | 520 × 150 | 1,5 | merkez | 150 40 150 40 | başlık kurdelesi |
| `progress_track` · `progress_fill` · `_s` ikizleri | 4 | 200 × 84 · 120 × 64 · 120 × 48 · 80 × 36 | 1,5 | merkez | kapsül uçları | ilerleme |
| `bubble_speech` · `bubble_tail` | 2 | 240 × 180 · 64 × 56 | 1,5 | merkez · 0,35 1 | 56 56 56 64 | konuşma / öğretici balonu |
| `bead_on/off` · `toggle_on/off` | 4 | 56² · 176 × 88 | 1,5 | merkez | — | seri boncuğu · ayar anahtarı |
| `nav_bar` · `nav_tab_on` | 2 | 216 × 216 · 300² | 1,5 | merkez · alt orta | 8 40 8 8 · — | alt gezinme |
| `px_white` | 1 | 8² | 1,0 | merkez | — | karartma, bantlar (tint) |
| `icon_*`, `flag_*`, `ui_glove`, `deco_brick_*` | 33 | 128² (eldiven 140 × 160, tuğla 200²) | 1,5 | merkez | — | ikonlar |
| `fx_*` | 11 | 16–1024 | 0,5–1,5 | merkez | — | parçacık, ışın, parlama |
| `chr_tuna_{idle,cheer,think}`, `chr_dede_{idle,talk}`, `chr_kepce_idle` | 6 | 300 × 480 (cheer 300 × 400, Kepçe 320 × 220) | 1,25 | 0,5 0,02 (ayak) | — | karakterler |
| `chr_tuna_bust`, `chr_dede_bust` | 2 | 256 × 320 | 1,0 | merkez | — | portre (öğretici, avatar) |
| `bg_home` · `bg_level` | 2 | 1080 × 1920 | 0,75 · 0,375 | merkez | — | arka plan |
| `town_ch1_treehouse` | 1 | 760 × 820 | 0,75 | alt orta | — | ana sayfa yapısı |
| `logo_wordmark` · `logo_emblem` | 2 | 680 × 400 · 360² | 1,0 | merkez | — | yükleme, mağaza görselleri |

Tam ad listesi: Ek A.

## 6. Stil kitabı uyumu ve özgünlük (§11)

- **Blok**: ART §3A v2 + §3A.5 çıkıntı + ART_REFERENCE §5.2 v3 farkları birebir (kontur 4 px × 0,50, dudak 20 px
  × 0,74, yastığın üst 0,20 h'si × 1,28 bevel bandı, parlama α 0,30 rx 0,18 w, çıkıntı hilalleri, sembol kabartması).
  Palet D-083 değişmedi (W #764012 … P #5D3AAE); renk körü güvencesi palette ve sembolde, v4 yalnız ışık/hacim ekler.
  Referansın hücre başına 2 × 2 çıkıntısı **alınmadı** (hücre başına tek çıkıntı + sembol).
- **UI kiti**: ART_REFERENCE §3.1 (düğme çizim sırası, L / M ölçüleri, basılı hâl: plaka 8 px iner, dudak × 0,35),
  §3.2 kırmızı ×, §3.3 yeşil +, §3.4 kapsül (koyu çukur), §3.5 kare rozet, §3.7 pencere katmanları, §3.8 ilerleme,
  §3.9 gezinme, §3.14 balon; renkler §2.2–2.4 hex'leri.
- **Tahta**: §5.3 tepsi (çerçeve 48, yüz #5247B8 → #3F35A0, dış kenar #221B66, iç çizgi #2E2778, köşe 40 / 20) +
  ART §4–5 (ozalit, plan, duvar başlığı ikaz şeridi, vinç alanı kesik çizgisi). Duvar v4'te **beton tuğla şerit**
  (ART §5 beton tonları, koşu bağı; R bloğundan renk ve çıkıntısızlıkla ayrılır).
- **Karakterler**: web v3 SVG'leri (§6.2 kuralları: 4 duraklı hacim gradyanı, kenar ışığı, ortam ve temas gölgesi, iki
  parlamalı göz, kontursuz) kopyalanıp v4 kareleri olarak alındı; **yeni pozlar** `chr_tuna_think` (el çenede, gözler
  yukarı, tek kaş kalkık, "?" düşünce balonu) ve `chr_dede_talk` (açık avuçla anlatan kol, açık ağız, kalkık kaşlar)
  bu kopyaların elle düzenlenmesiyle çizildi. Tuna'nın yaşı hiçbir yerde yok (D-044).
- **Arka plan**: `bg_home` gündüz göğü (§2.1 yükleme göğü tonları #1B49A4 → #2B5FB6 → #7AA3D8 → ufuk), §7.1
  katmanları 1–8 (gök, hava perspektifli uzak tepeler, Renkli Tepe'nin renkli çatılı evleri ve kule vinci, ağaçlar,
  çayır + patika, köşelerde koyu çalı kümeleri, vinyet, sis bandı); "sakin orta" korunur. `bg_level` §7.2 sade çivit +
  ozalit ızgarası α 0,025 + kenar siluetleri α 0,07 + alt bant.
- **Logo**: `logo_wordmark` ART_REFERENCE §9.1'in **dili** (renkli 3B harf, mor kontur, aşağı-sağ ekstrüzyon); harf
  biçimi Baloo 2, renk sırası bizim; referans logonun tuğla-harf motifi ve "!" yok.
- **Kopyalanmayan** (§11): referans ekran görüntülerinden hiçbir piksel, iz ya da kırpma; maskot, karakter, logo, ad
  yok. Referanslar yalnız ölçü ve anatomi kaynağıdır (stil kitabı üzerinden).

## 7. Öz puan (ART_REFERENCE §10 rubriği; kabul her boyutta ≥ 4)

Mock'lar `tools/art/preview.mjs` çıktısıdır: yalnız atlas karelerinden çizilir (kontratı da sınar), metinler
`Resources/i18n/tr.json`'dan, Baloo 2 ile. Puanlar design-lead'in öz değerlendirmesidir; nihai kabul code-lead'in Unity
ekran görüntüleriyle (§10.1 yöntemi) yapılır.

### 7.1 Tur 1 (ilk kompozit)

| Mock | Hacim / ışık | Renk / kontrast | Tipografi | Düzen / boşluk | Karakter / sahne | İkon / rozet | Bulgu |
| ---- | ------------ | --------------- | --------- | -------------- | ---------------- | ------------ | ----- |
| `v4-level` | 4 | 4 | 4 | **3** | 4 | 4 | HUD ile tepsi arasında 168 px ölü boşluk; hamle rakamı lacivert banda değiyor |
| `v4-home` | 4 | 4 | 4 | 4 | **3** | 4 | köşe çalıları düz daireler (ön plan katmanı kaba); ağaç ev vinç + evlerle üst üste, kalabalık |
| `v4-win` | 4 | 4 | 4 | **3** | 4 | 4 | Tuna'nın elleri "Bütün bloklar yerinde!" metninin üstünde; ışın yelpazesi kartın dışına taşıyor |
| `v4-sheet` | — | — | — | **2** | — | — | tuval 3000 px'te kesiliyor, son 90 kare görünmüyor |
| `logo_wordmark` | 4 | 4 | **3** | — | — | — | Baloo 2'nin üst üste binen kontur parçaları vurgu çizgisinde iç çizgi olarak görünüyor |

### 7.2 Düzeltmeler

1. `v4-level`: boşluğa ART_REFERENCE §9.9 öğretici balonu (x 150, y 282, Usta Dede portresi Ø 132, `tut.m.free`
   "Şimdi alttakini taşı!") + sürüklenen bloğun altında eldiven; hamle rakamı 6 px aşağı, 80 px.
2. `v4-home`: ön plan çalıları 7 yumrulu küme + yaprak parlamaları + çiçek; ağaç ev çayıra indirildi (alt kenar
   y 1236), karakter grubu §9.2 dikdörtgenine (x 30–615) sıkıştırıldı.
3. `v4-win`: metin y 950'ye, Tuna kartın içine (252 × 336), kart içi ışın yelpazesi karta kırpıldı.
4. `v4-sheet`: tuval boyu kare sayısından hesaplanıyor (488 kare, 2048 × 3584).
5. `logo_wordmark`: vurgu çizgisi kaldırıldı; yerine üst %42'ye kırpılmış beyaz α 0,22 parlama katmanı.
6. Ek (puan dışı): atlas paketleyici raf → skyline (5 → 4 sayfa, doluluk %87,9 → %91,8); PNG'ler kayıpsız yeniden
   sıkıştırıldı (5,7 → 3,9 MB); `wall_cap` en-boy hatası (60 → 64 tasarım px) ve `gap_edge` pivotu düzeltildi.

### 7.3 Tur 2 (son)

| Mock | Hacim / ışık | Renk / kontrast | Tipografi | Düzen / boşluk | Karakter / sahne | İkon / rozet | Kabul |
| ---- | ------------ | --------------- | --------- | -------------- | ---------------- | ------------ | ----- |
| `v4-level` | 4 | 4 | 4 | 4 | 4 | 4 | evet |
| `v4-home` | 4 | 4 | 4 | 4 | 4 | 4 | evet |
| `v4-win` | 4 | 4 | 4 | 4 | 4 | 4 | evet |
| `v4-sheet` | 4 | 4 | — | 4 | — | 4 | evet (kontrol sayfası) |

Gerekçe (neden 4, neden 5 değil):

- **Hacim / ışık 4**: her dokunulur öğede kontur + bevel bandı + dudak + iç plaka, ışık sol üstten; bloklarda çıkıntı
  hilalleri ve yastık parlaması. 5 değil: plan hücreleri bilerek düz (ART §4), HUD kapsülleri düz çukur; basılı hâller
  var ama mock'ta görünmüyor.
- **Renk / kontrast 4**: çivit zemin → doygun öğe → krem / beyaz yazı üç kademesi; yeşil = tek birincil, kırmızı
  rozet / ×, altın ödül. 5 değil: blok paleti (D-083, renk körü güvencesi) referansın C\* 70–95 doygunluğunun altında
  (G #44D088, C #C8CFDA, Y #FFE23E açık tonlar).
- **Tipografi 4**: Baloo 2 800, krem sayaç + koyu kontur, lavanta kapsül metni, `bigNumber` hamle. 5 değil: pencere
  başlığı mock'ta `titleTwoRing` yerine düğme stiliyle (kurdele üstünde) — yazı çizimi kodun işi (`kit.v3.text`).
- **Düzen / boşluk 4**: ScreenLayout dikdörtgenleri birebir (HUD 24 / 128, tepsi 48, güçlendirici 304 + 192 i, alttan
  120); ana sayfa §9.2 (kapsüller x 160 / 500, 300 × 88; düğme 600 × 200 alttan 336; gezinme 200 + 300² sekme).
- **Karakter / sahne 4**: §6 öğeleri (kenar ışığı, AO, temas gölgesi, iki parlamalı göz) + §7 katmanları 1–8 ana
  sayfada. 5 değil: karakterler web v3 düzeyinde vektör; referansın render (3B) malzeme okunurluğuna yan yana
  konduğunda ulaşmıyor (§8 madde 4).
- **İkon / rozet 4**: §3.5 kare rozet 72, §3.3 yeşil + Ø 56, §3.13 kilit, ikonlar 3 ton + spekülar + gölge. 5 değil:
  yeni ayar ikonları (ses, titreşim, hareket) tek parça siluet; "küçük illüstrasyon" düzeyinde değil.

## 8. Bilinen sorunlar ve sonraki adımlar

1. **Bellek**: 4 × 2048² RGBA32 ≈ 64 MB (çalışma anında `LoadImage` sıkıştırmasız). Öneri: oyun sahnesi atlas 0–2'yi,
   ana sayfa ayrı sayfaları yüklesin — bunun için paketleyiciye kategori → sayfa ataması eklenmeli (şimdilik
   paketleyici kareleri boy sırasıyla dağıtıyor, sayfalar karışık); uzun vadede PNG'leri `Texture2D` varlığı olarak içe aktarıp ASTC sıkıştırma (Editor gerekir).
2. **Mip ve kenar boşluğu** (P3-12): 2 px boşluk tek seviyeli örnekleme içindir; `mipChain: true` açılırsa
   `--pad 8` ile yeniden raster'la (tek bayrak).
3. **Resources adı**: `atlas-0.png.bytes` → yükleme adı `atlas-0.png` (noktalı ad; P3-13 ile aynı şüphe). CI'da
   doğrulanmazsa araç çıktı adı `atlas_0.bytes` yapılır.
4. **Karakter kalitesi**: rubrik 4'ün alt sınırı; RM düzeyi için sonraki sanat paketinde (V-x) poz başına yeni çizim
   (tam boy Kepçe oturuş, Gribeton v4, Dede tam boy yüz detayları) önerilir.
5. **Yazı**: mock'lardaki metin stilleri ART_REFERENCE §4'ün kodla çizimidir; Unity'de TMP yok (D-096) → Baloo 2
   `Font` varlığı + `Outline` / gölge bileşenleri ya da metin dokusu pişirme kararı code-lead'de (P3-16 Türkçe glifler).
6. Kopyalar: `art/src/icon/*`, `chr/*`, `town/*`, `logo/logo_emblem.svg` web v3'ün birebir kopyasıdır (+ `data-scale`);
   web dosyası değişirse elle eşitlenir (web görseli D-094 ile dondurulmuş).

---

## Ek A — tam kare listesi (488)

**block** (274): `blk_b_00`, `blk_b_01`, `blk_b_02`, `blk_b_03`, `blk_b_04`, `blk_b_05`, `blk_b_05_cf_bl`, `blk_b_06`, `blk_b_06_cf_br`, `blk_b_07`, `blk_b_07_cf_bl`, `blk_b_07_cf_br`, `blk_b_08`, `blk_b_09`, `blk_b_09_cf_tl`, `blk_b_10`, `blk_b_10_cf_tr`, `blk_b_11`, `blk_b_11_cf_tl`, `blk_b_11_cf_tr`, `blk_b_12`, `blk_b_13`, `blk_b_13_cf_bl`, `blk_b_13_cf_tl`, `blk_b_14`, `blk_b_14_cf_br`, `blk_b_14_cf_tr`, `blk_b_15`, `blk_b_15_cf_bl`, `blk_b_15_cf_br`, `blk_b_15_cf_tl`, `blk_b_15_cf_tr`, `blk_c_00`, `blk_c_01`, `blk_c_02`, `blk_c_03`, `blk_c_04`, `blk_c_05`, `blk_c_05_cf_bl`, `blk_c_06`, `blk_c_06_cf_br`, `blk_c_07`, `blk_c_07_cf_bl`, `blk_c_07_cf_br`, `blk_c_08`, `blk_c_09`, `blk_c_09_cf_tl`, `blk_c_10`, `blk_c_10_cf_tr`, `blk_c_11`, `blk_c_11_cf_tl`, `blk_c_11_cf_tr`, `blk_c_12`, `blk_c_13`, `blk_c_13_cf_bl`, `blk_c_13_cf_tl`, `blk_c_14`, `blk_c_14_cf_br`, `blk_c_14_cf_tr`, `blk_c_15`, `blk_c_15_cf_bl`, `blk_c_15_cf_br`, `blk_c_15_cf_tl`, `blk_c_15_cf_tr`, `blk_g_00`, `blk_g_01`, `blk_g_02`, `blk_g_03`, `blk_g_04`, `blk_g_05`, `blk_g_05_cf_bl`, `blk_g_06`, `blk_g_06_cf_br`, `blk_g_07`, `blk_g_07_cf_bl`, `blk_g_07_cf_br`, `blk_g_08`, `blk_g_09`, `blk_g_09_cf_tl`, `blk_g_10`, `blk_g_10_cf_tr`, `blk_g_11`, `blk_g_11_cf_tl`, `blk_g_11_cf_tr`, `blk_g_12`, `blk_g_13`, `blk_g_13_cf_bl`, `blk_g_13_cf_tl`, `blk_g_14`, `blk_g_14_cf_br`, `blk_g_14_cf_tr`, `blk_g_15`, `blk_g_15_cf_bl`, `blk_g_15_cf_br`, `blk_g_15_cf_tl`, `blk_g_15_cf_tr`, `blk_gloss`, `blk_locked`, `blk_o_00`, `blk_o_01`, `blk_o_02`, `blk_o_03`, `blk_o_04`, `blk_o_05`, `blk_o_05_cf_bl`, `blk_o_06`, `blk_o_06_cf_br`, `blk_o_07`, `blk_o_07_cf_bl`, `blk_o_07_cf_br`, `blk_o_08`, `blk_o_09`, `blk_o_09_cf_tl`, `blk_o_10`, `blk_o_10_cf_tr`, `blk_o_11`, `blk_o_11_cf_tl`, `blk_o_11_cf_tr`, `blk_o_12`, `blk_o_13`, `blk_o_13_cf_bl`, `blk_o_13_cf_tl`, `blk_o_14`, `blk_o_14_cf_br`, `blk_o_14_cf_tr`, `blk_o_15`, `blk_o_15_cf_bl`, `blk_o_15_cf_br`, `blk_o_15_cf_tl`, `blk_o_15_cf_tr`, `blk_p_00`, `blk_p_01`, `blk_p_02`, `blk_p_03`, `blk_p_04`, `blk_p_05`, `blk_p_05_cf_bl`, `blk_p_06`, `blk_p_06_cf_br`, `blk_p_07`, `blk_p_07_cf_bl`, `blk_p_07_cf_br`, `blk_p_08`, `blk_p_09`, `blk_p_09_cf_tl`, `blk_p_10`, `blk_p_10_cf_tr`, `blk_p_11`, `blk_p_11_cf_tl`, `blk_p_11_cf_tr`, `blk_p_12`, `blk_p_13`, `blk_p_13_cf_bl`, `blk_p_13_cf_tl`, `blk_p_14`, `blk_p_14_cf_br`, `blk_p_14_cf_tr`, `blk_p_15`, `blk_p_15_cf_bl`, `blk_p_15_cf_br`, `blk_p_15_cf_tl`, `blk_p_15_cf_tr`, `blk_r_00`, `blk_r_01`, `blk_r_02`, `blk_r_03`, `blk_r_04`, `blk_r_05`, `blk_r_05_cf_bl`, `blk_r_06`, `blk_r_06_cf_br`, `blk_r_07`, `blk_r_07_cf_bl`, `blk_r_07_cf_br`, `blk_r_08`, `blk_r_09`, `blk_r_09_cf_tl`, `blk_r_10`, `blk_r_10_cf_tr`, `blk_r_11`, `blk_r_11_cf_tl`, `blk_r_11_cf_tr`, `blk_r_12`, `blk_r_13`, `blk_r_13_cf_bl`, `blk_r_13_cf_tl`, `blk_r_14`, `blk_r_14_cf_br`, `blk_r_14_cf_tr`, `blk_r_15`, `blk_r_15_cf_bl`, `blk_r_15_cf_br`, `blk_r_15_cf_tl`, `blk_r_15_cf_tr`, `blk_w_00`, `blk_w_01`, `blk_w_02`, `blk_w_03`, `blk_w_04`, `blk_w_05`, `blk_w_05_cf_bl`, `blk_w_06`, `blk_w_06_cf_br`, `blk_w_07`, `blk_w_07_cf_bl`, `blk_w_07_cf_br`, `blk_w_08`, `blk_w_09`, `blk_w_09_cf_tl`, `blk_w_10`, `blk_w_10_cf_tr`, `blk_w_11`, `blk_w_11_cf_tl`, `blk_w_11_cf_tr`, `blk_w_12`, `blk_w_13`, `blk_w_13_cf_bl`, `blk_w_13_cf_tl`, `blk_w_14`, `blk_w_14_cf_br`, `blk_w_14_cf_tr`, `blk_w_15`, `blk_w_15_cf_bl`, `blk_w_15_cf_br`, `blk_w_15_cf_tl`, `blk_w_15_cf_tr`, `blk_y_00`, `blk_y_01`, `blk_y_02`, `blk_y_03`, `blk_y_04`, `blk_y_05`, `blk_y_05_cf_bl`, `blk_y_06`, `blk_y_06_cf_br`, `blk_y_07`, `blk_y_07_cf_bl`, `blk_y_07_cf_br`, `blk_y_08`, `blk_y_09`, `blk_y_09_cf_tl`, `blk_y_10`, `blk_y_10_cf_tr`, `blk_y_11`, `blk_y_11_cf_tl`, `blk_y_11_cf_tr`, `blk_y_12`, `blk_y_13`, `blk_y_13_cf_bl`, `blk_y_13_cf_tl`, `blk_y_14`, `blk_y_14_cf_br`, `blk_y_14_cf_tr`, `blk_y_15`, `blk_y_15_cf_bl`, `blk_y_15_cf_br`, `blk_y_15_cf_tl`, `blk_y_15_cf_tr`, `stud_b`, `stud_c`, `stud_g`, `stud_o`, `stud_p`, `stud_r`, `stud_w`, `stud_y`, `sym_b`, `sym_c`, `sym_g`, `sym_o`, `sym_p`, `sym_r`, `sym_w`, `sym_y`

**ghost** (18): `ghost_b`, `ghost_b_front`, `ghost_c`, `ghost_c_front`, `ghost_front`, `ghost_g`, `ghost_g_front`, `ghost_hidden`, `ghost_o`, `ghost_o_front`, `ghost_p`, `ghost_p_front`, `ghost_r`, `ghost_r_front`, `ghost_w`, `ghost_w_front`, `ghost_y`, `ghost_y_front`

**shadow** (80): `shadow_dash_00`, `shadow_dash_01`, `shadow_dash_02`, `shadow_dash_03`, `shadow_dash_04`, `shadow_dash_05`, `shadow_dash_05_cc_bl`, `shadow_dash_06`, `shadow_dash_06_cc_br`, `shadow_dash_07`, `shadow_dash_07_cc_bl`, `shadow_dash_07_cc_br`, `shadow_dash_08`, `shadow_dash_09`, `shadow_dash_09_cc_tl`, `shadow_dash_10`, `shadow_dash_10_cc_tr`, `shadow_dash_11`, `shadow_dash_11_cc_tl`, `shadow_dash_11_cc_tr`, `shadow_dash_12`, `shadow_dash_13`, `shadow_dash_13_cc_bl`, `shadow_dash_13_cc_tl`, `shadow_dash_14`, `shadow_dash_14_cc_br`, `shadow_dash_14_cc_tr`, `shadow_dash_15`, `shadow_dash_15_cc_bl`, `shadow_dash_15_cc_br`, `shadow_dash_15_cc_tl`, `shadow_dash_15_cc_tr`, `shadow_fill_00`, `shadow_fill_01`, `shadow_fill_02`, `shadow_fill_03`, `shadow_fill_04`, `shadow_fill_05`, `shadow_fill_06`, `shadow_fill_07`, `shadow_fill_08`, `shadow_fill_09`, `shadow_fill_10`, `shadow_fill_11`, `shadow_fill_12`, `shadow_fill_13`, `shadow_fill_14`, `shadow_fill_15`, `shadow_line_00`, `shadow_line_01`, `shadow_line_02`, `shadow_line_03`, `shadow_line_04`, `shadow_line_05`, `shadow_line_05_cc_bl`, `shadow_line_06`, `shadow_line_06_cc_br`, `shadow_line_07`, `shadow_line_07_cc_bl`, `shadow_line_07_cc_br`, `shadow_line_08`, `shadow_line_09`, `shadow_line_09_cc_tl`, `shadow_line_10`, `shadow_line_10_cc_tr`, `shadow_line_11`, `shadow_line_11_cc_tl`, `shadow_line_11_cc_tr`, `shadow_line_12`, `shadow_line_13`, `shadow_line_13_cc_bl`, `shadow_line_13_cc_tl`, `shadow_line_14`, `shadow_line_14_cc_br`, `shadow_line_14_cc_tr`, `shadow_line_15`, `shadow_line_15_cc_bl`, `shadow_line_15_cc_br`, `shadow_line_15_cc_tl`, `shadow_line_15_cc_tr`

**board** (16): `beam_seg`, `board_tray`, `crane_band`, `crane_hook`, `floor_site`, `floor_site_deep`, `floor_yard_a`, `floor_yard_b`, `gap_edge`, `gap_seg`, `rail_seg`, `scaffold_clamp`, `scaffold_pipe`, `truck`, `wall_cap`, `wall_seg`

**ui** (43): `badge_count`, `badge_dot`, `bead_off`, `bead_on`, `btn_booster`, `btn_booster_off`, `btn_booster_pressed`, `btn_close`, `btn_close_pressed`, `btn_danger`, `btn_danger_pressed`, `btn_disabled`, `btn_offer`, `btn_offer_pressed`, `btn_plus`, `btn_primary`, `btn_primary_m`, `btn_primary_m_pressed`, `btn_primary_pressed`, `btn_secondary`, `btn_secondary_pressed`, `btn_slot`, `btn_tool`, `btn_tool_pressed`, `bubble_speech`, `bubble_tail`, `capsule_home`, `capsule_hud`, `hud_moves_plate`, `hud_well`, `nav_bar`, `nav_tab_on`, `panel_card`, `panel_plate`, `panel_window`, `progress_fill`, `progress_fill_s`, `progress_track`, `progress_track_s`, `px_white`, `ribbon_title`, `toggle_off`, `toggle_on`

**icon** (33): `deco_brick_1`, `deco_brick_i2`, `deco_brick_l3`, `deco_brick_o`, `flag_en`, `flag_tr`, `icon_blocks`, `icon_brush`, `icon_check`, `icon_chest`, `icon_coin`, `icon_crane`, `icon_gold_trowel`, `icon_hammer`, `icon_heart`, `icon_lock`, `icon_motion`, `icon_music`, `icon_nav_album`, `icon_nav_home`, `icon_nav_league`, `icon_nav_shop`, `icon_nav_team`, `icon_pause`, `icon_play`, `icon_settings`, `icon_sound_off`, `icon_sound_on`, `icon_star`, `icon_star_empty`, `icon_undo`, `icon_vibration`, `ui_glove`

**fx** (11): `fx_chip`, `fx_confetti`, `fx_confetti_round`, `fx_drop_blob`, `fx_dust`, `fx_glow`, `fx_rays`, `fx_ring`, `fx_sheen`, `fx_spark`, `fx_star`

**chr** (8): `chr_dede_bust`, `chr_dede_idle`, `chr_dede_talk`, `chr_kepce_idle`, `chr_tuna_bust`, `chr_tuna_cheer`, `chr_tuna_idle`, `chr_tuna_think`

**bg** (2): `bg_home`, `bg_level`

**town** (1): `town_ch1_treehouse`

**logo** (2): `logo_emblem`, `logo_wordmark`

