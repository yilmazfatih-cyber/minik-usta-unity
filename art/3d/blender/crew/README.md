# Tuna ve Kepçe — 3B model, animasyon ve sprite hattı (sanat turu 1)

Sahip: design-lead · Tarih: 2026-11-02 · Kararlar: D-100 (3B görsel dil, ücretsiz ve açık kaynak üretim), D-101 (Usta
Kral ana sembol; Tuna ve Kepçe ikincil karakter), D-044 (Tuna'nın yaşı hiçbir metinde geçmez), D-087 (Tuna oranları) ·
Tasarım kaynağı: web reposu `docs/ART_DIRECTION.md` §11.1 (Tuna) ve §11.3 (Kepçe) · Işık, malzeme ve kamera:
`art/3d/blender/king/build_king.py` (rig B, aynı kit) · İnceleme: sanat turu 1 bulgusu "aynı karede iki ayrı üretim
dili" (3B kral yanında v4 düz SVG Tuna ve Kepçe).

Üretken yapay zekâ yok, satın alınmış ya da indirilmiş varlık yok, referans görüntülerinden piksel yok: iki karakter de
`bpy` + `bmesh` ilkelleriyle (süperelipsoid, kesim düzlemi, eğri tüp, izdüşümlü çıkartma) kurulur.

## 1. Komutlar (repo kökünden)

| Adım | Komut | Süre (4 çekirdek) |
| ---- | ----- | ----------------- |
| 1. Render (51 çizim + 1 büst, 2× ana boy 224 px/BU) | `blender -b --factory-startup -noaudio --python art/3d/blender/crew/animate_crew.py -- --out art/3d/out/crew/raw --samples 64` | ≈ 8 dk |
| 2. Son işlem (temas gölgesi, 224 → 112 px/BU, alfa taşırma, kırpma) | `python3 art/3d/blender/king/post_king.py --raw art/3d/out/crew/raw --out art/3d/out/crew/frames --div 2` | ≈ 5 sn |
| 3. Paket (`Resources/art3d/crew.json` + `crew_N.bytes`) | `node tools/art/pack3d.mjs --name crew --frames art/3d/out/crew/frames --budget-mb 9` | ≈ 5 sn |
| 4. Atlas durağanları (`chr_tuna_*`, `chr_kepce_idle` v4 adları, 3B piksel) | `python3 art/3d/blender/env/post_env.py --only chr_tuna_cheer,chr_tuna_idle,chr_tuna_think,chr_tuna_bust,chr_kepce_idle` sonra `node tools/art/rasterize.mjs --web ../minik-usta` | ≈ 30 sn |
| Dönüş görünüşü | `blender -b --factory-startup -noaudio --python art/3d/blender/crew/build_crew.py -- --out art/3d/out/crew/turn` | ≈ 20 sn |

## 2. Model

- **Ölçek**: 1 BU = Usta Kral'ın başı; üç karakter aynı dünya ölçeğinde (ana sayfada yan yana çizilir). Tuna ayak →
  kafatası tepesi 2,045 BU = **3,6 baş** (D-087), baret dahil ≈ 2,2 BU (kralın %83'ü). Kepçe burun → kuyruk ≈ 1,9 BU,
  baret dahil boy ≈ 1,1 BU.
- **Tuna**: nane yeşili baret #7FE0C4 (siperlik #5CC9AB), önde kabarık beyaz yıldız; turuncu yelek #FF8A1F + iki sarı
  reflektör bant #FFF06A (önü açık); krem / teal çizgili uzun kollu tişört (#F7F3EA / #2A9D8F, nesne Z'sinde yumuşak
  bant); lacivert pantolon #2E3A59, sol dizin altında kum rengi yama #E9C891 + çapraz dikiş; **kocaman sarı iş
  eldivenleri** #FFC93C (imza siluet: avuç, dört kalın parmak, başparmak, genişleyen manşet); kırmızı spor ayakkabı
  #E8473B, beyaz taban ve burun; sağ kulağın arkasında sarı kalem + pembe silgi; dağınık kestane saç #4A2C1D (kask
  altından çene hizasına taşan sivri tutamlar, uçları dışa); iri amber gözler (yüksekliği görünen yüzün %30'u, iki
  parlama), düz hafif açılı kaşlar, kısa burun, küçük yan gülüş, çiller, hafif yanak.
- **Kepçe**: sosis köpek #B5652B, ağız çevresi ve göğüs #D99A5E, kulaklar #8A4A1F (biri baretin altından sarkar);
  **büyük gelen turuncu baret** #FF9A1F geriye ve yakın yana kaymış; nane yeşili tasma #7FE0C4 + kemik biçimli altın
  künye; uzun gövde, çok kısa bacaklar, açık renk patiler; ince uçlu kuyruk; iri koyu gözler (kahve iris halkası, iki
  parlama) izleyiciye bakar; siyah parlak burun; gülümseme ve pembe dil.
- **Işık / malzeme**: Usta Kral'ın rig B'si (sıcak anahtar güneş, mavi dolgu, iki kenar ışığı, yalnız parlak ışınlar için
  stüdyo ortamı), Standard görünüm dönüşümü, aynı şeker-plastik Principled değerleri. Temas gölgesi `post_king.py`
  ile: elips genişliği ayak / pati açıklığının **1,1 katı** (Tuna 0,62 BU, Kepçe 1,30 BU), tepe α **0,35**.

## 3. Klipler (`Resources/art3d/crew.json`, 24 fps, king.json biçimi)

| Klip | Kare | Döngü | Çizim | Set | Olaylar | Hareket |
| ---- | ---- | ----- | ----- | --- | ------- | ------- |
| `tuna_home_idle` | 48 | 0–47 | 24 (ikili) | stage | 3 / 15 / 27 / 39 `wave_peak`, 30 `blink` | ekranın sol eldiveni havada 2 Hz el sallar (eldiven 2 kare gecikmeli), öbür eldiven belde, ağırlık aktarma, diz yaylanması, baş karşı-salınımı, baret takibi, göz kırpma |
| `kepce_home_idle` | 48 | 0–47 | 24 (ikili) | stage | 30 `blink` | 2 Hz kuyruk sallama, soluma (çene + nefes), yavaş baş eğme, kulaklar 3 kare gecikmeli, baret takibi |
| `kepce_corner_idle` | 48 | 0–47 | `kepce_home_idle` çizimleri | corner | 30 `blink` | oyun ekranı köşesi (ART_3D §13.3) |

Bütün döngüler faz-sürekli: 47. kareden 0. kareye dikiş yok. Set ölçekleri king.json ile aynı kural: `stage` = 480
tasarım px / 3,6 BU (ana sayfa, kral 0,705 ile aynı boy oranı), `corner` = 320 tasarım px / 3,0 BU. Çizimler kök
pivotla hizalanır (ayaklar yerde, kırpma kayması yok); kod kareleri kutuya sığdırmaz.

## 4. Atlas durağanları (v4 adları; bugünkü kod hiçbir değişiklik olmadan 3B'yi çizer)

| Atlas karesi (boy, pivot değişmez) | Çizim | Gösterim yoğunluğu (tasarım px / BU) | Saklanan ölçek |
| ---------------------------------- | ----- | ------------------------------------ | -------------- |
| `chr_tuna_cheer` 300 × 400, 0,5 0,02 | `chr_tuna_still_cheer` (iki eldiven havada, mutlu gözler, açık gülüş) | 133,3 (= kralın sahne boyu) | 0,84 |
| `chr_tuna_idle` 300 × 480 | `chr_tuna_still_idle` | 160 | 0,7 |
| `chr_tuna_think` 300 × 480 | `chr_tuna_still_think` (eldiven çenede, gözler yukarıda) | 160 | 0,7 |
| `chr_tuna_bust` 256 × 320, 0,5 0,5 | `chr_tuna_bust_neutral` | 256 | 0,75 |
| `chr_kepce_idle` 320 × 220, 0,5 0,02 | `chr_kepce_idle_00` | 150 | 0,75 |

## 5. Bilinen sınırlar

- Unity'de oynatılmadı. `crew.json`'u okuyup klip oynatmak code-lead işidir (`ArtSet`'e `art3d/crew` kütüphanesi,
  `home.tuna` → `tuna_home_idle`, `home.kepce` → `kepce_home_idle`, `hud.kepce` → `kepce_corner_idle`).
- Tuna'nın öteki pozları (blok taşıma, kalemle not, Kepçe'yi kucaklama) bu turda yok.
