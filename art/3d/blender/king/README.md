# Usta Kral — 3B model, animasyon ve sprite hattı (paket B1k)

Sahip: design-lead · Tarih: 2026-11-01 · Kararlar: D-100 (3B görsel dil, ücretsiz ve açık kaynak üretim), D-101
(Usta Kral maskotu) · Tasarım kaynağı: `art/3d/spec/king.md` (oranlar, palet, yüz, baret, kıyafet, fark tablosu),
`art/3d/spec/frames.json` (rig B, renk yönetimi, klip zaman çizgileri), `docs/ART_3D.md` §6–§8.

Usta Kral tamamen **prosedürel** üretilir: Blender 4.0.2 `bpy` + `bmesh` + `mathutils` ile ilkel şekiller
(süperelipsoid, kesim düzlemi, EXACT boolean, bevel, solidify), Cycles CPU ile ön-görüntülenmiş sprite. Üretken
yapay zekâ yok, satın alınmış ya da indirilmiş varlık yok, referans görüntülerinden piksel yok.

## 1. Komutlar (repo kökünden)

| Adım | Komut | Süre (4 çekirdek CPU) |
| ---- | ----- | --------------------- |
| 1. Render (71 çizim + 6 büst, 2× boy: 200 px/BU, büst 300 px/BU) | `blender -b --factory-startup -noaudio --python art/3d/blender/king/animate_king.py -- --out art/3d/out/king/raw --samples 64 --bust-px 300` | 16,6 dk (ölçüldü) |
| 2. Dönüş görünüşü (K0: ön, 3/4, yan, arka) | `blender -b --factory-startup -noaudio --python art/3d/blender/king/build_king.py -- --out art/3d/out/king/turn --px 200 --samples 64` | ≈ 1 dk |
| 3. Son işlem (gölge, 200 → 94 px/BU küçültme, alfa taşırma, kırpma) | `python3 art/3d/blender/king/post_king.py` | ≈ 10 sn |
| 4. Paketleme (atlas sayfaları + `king.json`, 16 MB bütçe denetimi) | `node tools/art/pack3d.mjs --png artifacts/preview` | ≈ 10 sn |
| 5. Önizleme | `python3 art/3d/blender/king/preview_king.py` | ≈ 10 sn |

Hızlı poz denetimi: 1. adımı `--px 70 --samples 8` ile çalıştırın (≈ 1 dk). Tek çizim: `--only chr_king_cheer_05`;
tek klip: `--clips king_point`. Ara çıktılar (`art/3d/out/`) git dışıdır; repoya yalnız paketlenmiş
`Assets/MinikUsta/Resources/art3d/king_N.bytes` + `king.json` girer.

Ortam notları (bu makinede ölçüldü): apt `blender 4.0.2`'de **EEVEE başsız çalışmaz** (`libEGL.so.1` yok, render
boş döner) ve **OpenImageDenoise yok** → Cycles CPU, `use_denoising = False`; gürültüyü 2× süper örnekleme +
LANCZOS küçültme ve uyarlamalı örnekleme (eşik 0,02) giderir. `use_auto_smooth` 4.0 API'sidir (4.1'de kalktı);
betikler 4.0.2'ye sabittir. Python tarafı yalnız standart kütüphane + Pillow; paketleyici yalnız `node:zlib`.

## 2. Dosyalar

| Dosya | Ne |
| ----- | -- |
| `build_king.py` | model, malzemeler, eklem hiyerarşisi, kol IK'sı, el şekilleri, yüz sistemi, ışık / kamera / render ayarları; doğrudan çalıştırılınca K0 dönüş görünüşü |
| `animate_king.py` | ifadeler, anahtar pozlar, 8 klip (+ `king_home_idle` takma adı), yumuşatma, ikincil hareket, çizim planı, render ve `manifest.json` |
| `post_king.py` | temas gölgesi, 2× → 1× LANCZOS (ön çarpımlı alfa), alfa taşırma, kırpma, `frames.json` |
| `preview_king.py` | `king-sheet.png`, `king-turn.png`, `king-strip-<klip>.png` |
| `../../../../tools/art/pack3d.mjs` | v4 skyline paketleyicisiyle (`rasterize.mjs` `pack`) atlas sayfaları + `king.json`; bellek bütçesi aşılırsa hata |
| `preview/` | gözden geçirme için küçük kopyalar (sheet, turn, strip) |

## 3. Model

- **Ölçü**: 1 BU = 1 baş; toplam boy 2,50 BU; karakter −Y'ye bakar. Oranlar `king.md` §2 ile aynı (blockout ±%3).
- **Biçim dili**: bütün kütleler süperelipsoid (|x|ⁿ+|y|ⁿ+|z|ⁿ=1, n 2,2–4,2; küreselleştirilmiş küpten, kutup yok):
  squircle baş (n 3), tuğla gövde (n 3, göbek ve bel şişkinliği deforme ile), kutu burunlu bot (n 3), tombul eller.
- **Bantlar** (kuşak, altın şeritler, kemer): gövde yüzeyinden `bisect_plane` ile kesilen şerit, normal boyunca
  itilip `SOLIDIFY` — kenarlar düz ve temiz, gövde eğrisine tam oturur. Kuşak kemerin üstünden geçer.
- **Sakal + saç**: büyütülmüş baş süperelipsoidinden EXACT boolean ile yüz penceresi (U biçimli, şakaklarda
  incelen favori), çene köşe pahları ve ense kesilir; altında öne çıkan tuğla çene sakalı. Baretin altında saç
  vardır (büyük sevinçte baret havalanınca kel görünmez). Saç malzemesinde uzun dalga bantlarından tutam kabartması
  ve açık kestane çizgiler.
- **Yüz**: göz akı + amber iris + bebek tek malzemede, **bakış boşluğunda** (gaze empty) radyal maske → boşluğu
  döndürmek irisi yüzeyde kaydırır; iki sabit parlama; deri göz kapağı kabukları (üst / alt, döndürme ile açık,
  yarım, kapalı, yumuşak); kapalı ve mutlu (^) göz için kirpik yayları; siperliğin önünde kalın tuğla kaşlar
  (dy, açı; sağ/sol ayrı); 14 ağız biçimi yüz ve burun-ağız çıkıntısı (muzzle) yüzeyine izdüşürülmüş çıkartma
  (boşluk + üst diş + dil): `smile`, `grin`, `big`, `wave`, `A`, `A2`, `O`, `E`, `M`, `half`, `side`, `smirk`,
  `soft`, `o_small`; yanak kızarıklığı (değer 0–1) ve yanak şişmesi.
- **Baret**: güvenlik sarısı parlak plastik (coat 0,6), sırt kaburgası, önde kısa siperlikli kenar, 10° geriye yatık
  (kaşlar siperliğin altında kalmaz); önde altın çerçeveli mavi emaye plaka ve **3 uçlu, uçları yuvarlak çıkıntılı,
  taşsız** altın taç arması.
- **Eklemler** (parça bazlı hiyerarşi, boş nesneler): `view > root > leg_l, leg_r, body > head > hat > hat_tilt,
  arm_l / arm_r > elbow > wrist > (avuç, 4 parmak, başparmak) ; wrist_r > trowel`. Kollar **iki kemikli IK** (bilek
  hedefi + dirsek kutbu, gövde uzayında); el yönü isteğe bağlı **avuç normali + parmak yönü** ile; el şekilleri
  `relax`, `open`, `fist`, `grip`, `index`, `thumb`. Altın Mala dünya yönünde tutulur (`trowel_tilt / lean / spin`),
  konumu el kavrayışına bağlı. Adlandırma **ekran** tarafına göredir: `arm_r` = ekranın sağı (malayı tutar).
- **Işık**: `frames.json → rigs.B_char` (anahtar güneş #FFE6C4 2,6, mavi dolgu alanı, mor-beyaz kenar) + ikinci
  sıcak kenar ışığı; dünya ışığı #5A6A96 × 0,25; **yalnız parlak ışınlar** için sıcak-üst / serin-ufuk stüdyo
  ortamı (metal ve coat okunur, dağınık ışık değişmez). Görünüm dönüşümü **Standard** (ART_3D 6.3).
- **Gölge**: uzun gölge sprite'a pişirilmez (zemin değişkendir); `post_king.py` kök noktasına yumuşak, sıcak
  koyu (#3B2614) temas elipsi ekler — havadayken küçülür ve açılır.

## 4. Klipler (24 fps; kare sayıları, döngüler ve olay kareleri `frames.json` ile aynı)

Çizimler "ikili" (≈ 12 fps) ya da tutuşlu; benzersiz sayıları bellek bütçesi (§6) için `frames.json`'daki
tahminden azdır. **K0** (`chr_king_idle_00`) bütün kliplerin ilk ve son karesidir (kesmesiz geçiş).

| Klip | Kare | Döngü | `out` | Çizim (K0 dahil) | Olaylar | Royal Match dili (bizim çizimimizle) |
| ---- | ---- | ----- | ----- | ---------------- | ------- | ------------------------------------- |
| `king_idle` | 48 | 0–47 | — | 9 | 30 `blink` | ağırlık aktarma salınımı, baş karşı-salınımı, mala ±3°, baret 4 kare geç; göz kırpma 30–35 |
| `king_cheer` | 24 | yok | — | 10 | 4 `lift_off`, 10 `apex`, 14 `land` | çömelme (sy 0,91) → esneme 1,08 → tepe → iniş ezilmesi 0,90 → aşma; baret hazırlıkta bastırır, tepede havalanır, 2 kare geç oturur |
| `king_think` | 36 | 8–35 | 0–7 ters | 9 | 12 / 24 `tap`, 28 `blink` | işaret parmağı sakalda, tek kaş kalkık, gözler sağ-yukarı, parmak tıklaması, bakış kayması |
| `king_talk` | 24 | 0–23 | — | 9 | 0 / 12 `beat` | A-O-E-M ağız döngüsü, avuç yukarı açılan el, vuruşta baş sallama |
| `king_point` | 18 | 12–17 | R ×2, K0 ×2 | 8 | 6 `point_hit`, 12 `hold_start`, 14 `glint` | geri çekiş + gövde zıt yöne, malayla vuruş 6° aşma, oturma, tutuşta nefes; sol hedefe `scaleX = −1` |
| `king_encourage` | 24 | yok | — | 9 | 9 `gesture`, 15 `pump` | empatik kaşlar ve yumuşak gözler, geri yaslanma, avuç öne, başparmak yukarı pompası (aşmalı) |
| `king_wave` (sahne) | 24 | 6–23 | 0–5 ters | 8 | 9 / 18 `wave_peak` | ön kol ±25° sallanır, el 2 kare geriden gelir (örtüşen hareket), gövde karşı-salınımı |
| `king_big_cheer` (sahne) | 48 | 24–47 | — | 16 | 6 `jump`, 15 `land`, 22 `hat_land`, 28 / 40 `hop` | derin çömelme → büyük sıçrama, baret kafadan havalanır, iniş ezilmesi, baret geç oturur; zafer döngüsü ±6° salınım + iki küçük sekme |
| `king_home_idle` (sahne) | 48 | 0–47 | — | `king_idle` çizimleri | 30 `blink` | aynı çizimler sahne ölçeğinde |

Büstler (`chr_king_bust_<neutral|happy|think|talk|encourage|surprised>`) baş + omuz, 256 × 320 tasarım kutusu,
pivot = kutu merkezi. Durağanlar `chr_king_still_<poz>` ve eski `chr_dede_*` adları `king.json → aliases` ile
birer çizime bağlanır (yeni piksel yok).

## 5. `king.json` (Resources/art3d) biçimi

```json
{
 "version": 1, "ppu": 100, "pad": 2, "origin": "top-left", "pivotOrigin": "bottom-left", "pxPerBU": 94,
 "renderCanvas": {"w": 395, "h": 390, "rootX": 197, "rootY": 33},
 "memoryBytesRGBA32": 16333120,
 "atlases": [{"file": "art3d/king_0", "w": 1964, "h": 2012}, {"file": "art3d/king_1", "w": 588, "h": 224}],
 "frames": { "chr_king_idle_00": {"atlas": 0, "x": 169, "y": 864, "w": 164, "h": 275, "pivotX": 0.502439, "pivotY": 0.104728, "borderL": 0, "borderT": 0, "borderR": 0, "borderB": 0, "scale": 0.88125, "ox": 115, "oy": 4} },
 "clips": { "king_cheer": { "frames": ["chr_king_idle_00", "chr_king_idle_00", "chr_king_cheer_01", "..."], "fps": 24,
            "loop": false, "out": null, "events": {"4": "lift_off", "10": "apex", "14": "land"}, "set": "corner", "scale": 0.88125,
            "canvas": [320, 320], "pivot": [0.5, 0.0625] } },
 "aliases": { "chr_king_still_idle": { "of": "chr_king_idle_00", "scale": 0.705 },
              "chr_dede_idle": { "of": "chr_king_idle_00", "scale": 0.705, "deprecated": true } }
}
```

- `frames` alanları `atlas.json` (ART_UNITY §3) ile aynıdır; ek olarak `ox, oy` = kırpılmış dikdörtgenin tam
  render tuvalinin sol altından ofseti (saklama pikseli). **Pivot = kök nokta** (ayakların yerdeki izdüşümü;
  büstte tuval merkezi): bütün kareler pivotla hizalanır, kırpma kayması olmaz.
- **Ölçek kuralı**: sprite PPU = `ppu × scale`. Klip oynatırken **klibin** `scale`'i kullanılır (köşe seti 0,88125:
  320 tasarım px = 3,0 BU; sahne seti 0,705: 480 tasarım px = 3,6 BU; büst 0,75: 320 tasarım px = 1,6 BU). Aynı çizim iki sette ortaktır
  (`king_home_idle`). Kod kareleri bir kutuya **sığdırmamalı** (kırpılmış dikdörtgenler kareden kareye farklıdır);
  kök pivotu sabit noktaya koyup ölçekle çizmelidir.
- `clips.<ad>.frames[i]` = 24 fps zaman çizgisinin i. karesinde gösterilecek kare adı; `out` = döngüden çıkış
  dizisi (K0'a döner); `events` anahtarları kare indeksidir (`frames.json` ile aynı ses / haptik bağları).
- Yükleme: `Resources.Load<TextAsset>("art3d/king")`, sayfalar `Resources.Load<TextAsset>("art3d/king_0")` →
  `ImageConversion.LoadImage` (atlas ile aynı yol; düz Resources adı, ikinci nokta yok — CI.md, P3-13).

## 6. Bellek bütçesi

Hedef: bütün kral sayfaları **≤ 16 MB RGBA32** (WebGL, sıkıştırmasız `LoadImage`). Ölçümler:

| Deneme | Kırpılmış alan | Paket | Sonuç |
| ------ | -------------- | ----- | ----- |
| 106,7 px/BU, 78 çizim | 4,67 Mpx (17,8 MB) | — | aştı |
| 100 px/BU, 71 çizim | 3,90 Mpx; kenar paylı 4,05 Mpx (bütçenin %96,5'i) | skyline 19,6 MB | aştı (hiçbir paketleyici sığdıramaz) |
| 96 px/BU | 3,61 Mpx | MaxRects 2 sayfa, 15,67 MB | sığar, pay az |
| **94 px/BU** (karar) + büst 150 px/BU | 3,59 Mpx | **MaxRects 1964 × 2012 + 588 × 224, doluluk %91,4, 15,58 MB** | sığar |

Karar: çizimler **94 px/BU** (2× ana render'dan LANCZOS; köşe seti ölçeği 0,88125 → 390 pt telefonda ≈ 1,2×
büyütme), büstler 150 px/BU (Ø 132 öğretici portresi keskin kalsın). 78 → 71 çizim: en az hareket katan 7 ara çizim
çıkarıldı (cheer 18, talk 20, point 10, big_cheer 5 / 19 / 27 / 39); kare sayıları ve olay kareleri değişmedi.
`pack3d.mjs` hem v4 skyline'ı hem MaxRects'i (5 sıralama × 4 sayfa genişliği) dener, küçüğünü yazar; bütçe aşılırsa
çıkış kodu 1. PNG boyutu: `king_0.bytes` 5,3 MB + `king_1.bytes` 0,2 MB.

## 7. Öz denetim — fark tablosu (king.md §10) ve yasak listesi (§10.1)

Render'lara bakılarak işaretlendi (`preview/king-sheet.png`, `preview/king-turn.png`, büstler); karşılaştırma
proje sahibinin referans ekranlarıyla (`owner-2026-10-14/ref-08, ref-11, ref-13, ref-15`) yalnız göz kontrolüyle
yapıldı, hiçbir referans pikseli kullanılmadı.

| # | Boyut | Render'da görülen | Durum |
| - | ----- | ----------------- | ----- |
| 1 | Ad | Usta Kral (dosya ve kare adları `chr_king_*`) | tamam |
| 2 | Başlık | sarı plastik inşaat bareti (kubbe + sırt kaburgası + kenar); başın üstünde taç yok | tamam |
| 3 | Taç biçimi ve yeri | baretin önünde mavi emaye plakada 3 küt uçlu altın taç, uçlarda yuvarlak çıkıntı, **taş yok** | tamam |
| 4 | Sakal | kısa, köşeli kestane çene sakalı + favoriler (beyaz değil, uzun değil) | tamam |
| 5 | Bıyık | yok; ağız tamamen görünür | tamam |
| 6 | Kaş | kalın kestane tuğla kaşlar, siperliğin altında görünür (baret 10° geriye yatık) | tamam |
| 7 | Burun | küçük, ten renginde düğme burun (pembe yumru değil) | tamam |
| 8 | Göz | iri oval, amber iris, iki parlama | tamam |
| 9 | Ten | sıcak buğday (#E9A877) | tamam |
| 10 | Oran | 2,5 baş, tuğla gövde, kısa bacak | tamam |
| 11 | Kıyafet | turuncu tek parça iş tulumu, dirseğe kıvrık kol, V yaka, fermuar, cep + sarı kalem | tamam |
| 12 | Pelerin / kürk | yok (yalnız küçük tulum yakası) | tamam |
| 13 | Renk düzeni | turuncu + gök mavisi + güvenlik sarısı + kestane; mor yok | tamam |
| 14 | Kraliyet işareti | gök mavisi kuşak + 3 altın rozet | tamam |
| 15 | Kemer | orta kahve deri alet kemeri, **küçük** yuvarlak-kare toka, kese + tornavida | tamam (yasak 6'daki "kalın koyu kemer + büyük kare toka" birleşimi yok) |
| 16 | Ayakkabı | kahverengi iş botu, krem taban, sarı bağcık | tamam |
| 17 | El nesnesi | Altın Mala (asa gibi; işaret klibinde gösterge) | tamam |
| 18 | Rol | öğretmen / tezahüratçı pozları; kurtarılma sahnesi yok | tamam |
| 19 | Yan karakterler | render'da uşak ya da köpek yok | tamam |
| 20 | Sikke amblemi | bu pakette sikke yok (ikon paketi) | kapsam dışı |

Yasak listesi (§10.1): 1 başta taç **yok** · 2 fasetli taş **yok** · 3 beyaz sakal / kıvrık bıyık / beyaz kaş
**yok** · 4 mor tunik + altın yaka + pelerin **yok** · 5 pembe yumru burun **yok** · 6 kalın koyu kemer + büyük altın
kare toka **yok**, altın tokalı mavi ayakkabı **yok** · 7 kollar kavuşturulmuş poz **yok** (8 klipte de) · 8 uşak /
köpek **yok** · 9 mutfak / kurtarma **yok** · 10 kapsam dışı · 11 Bob the Builder birleşimi (askılı mavi tulum,
kareli gömlek, traşlı yüz) **yok** · 12 Mario / Wario birleşimi (kep, bıyık) **yok**. Benzeyen bir öğe bulunmadı;
değişiklik gerekmedi. Ortak olan yalnız serbest arketip: sevimli, abartılı ifadeli 3B kral.

## 8. Bilinen sınırlar

- `frames.json` (v5, D1 sözleşmesi) kral klipleri için çizim sayısı tahminini (129) ve 1,0 ölçeğini taşır;
  çalışma zamanının sözleşmesi **`king.json`**'dur (kare sayıları, fps, döngüler ve olay kareleri aynıdır; çizim
  sayısı ve ölçek bellek bütçesi için değişti). `gen_frames.py` sonraki D paketinde `king.json`'a hizalanmalı.
- `kepce_wag` (öncelik 2) bu pakette yok.
- Unity'de oynatılmadı: kodun `king.json`'u okuyup klip oynatması code-lead işidir (`hud.tuna` → `hud.king`,
  `home.dede` → `king_home_idle` / `king_wave`).
