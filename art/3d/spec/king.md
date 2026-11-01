# Usta Kral — karakter tasarım sayfası (D-101)

Sahip: design-lead · Tarih: 2026-11-01 · Paket: D1 · Bağlı: `docs/ART_3D.md` (§7 animasyon, §8 özet), web
`docs/ART_REFERENCE.md` §6 (karakter çizim dili), §11 (kopya sınırı) · Çizimler: `art/3d/spec/king/` (SVG + PNG,
`gen_sheet.py` ile üretilir; 3B oran blockout'u `king_blockout.py`, Blender 4.0.2 ile sınandı).

> **Karar özeti.** Usta Kral, Usta Dede'nin yerini alan ana semboldür: rehber (öğretici balonu portresi ve işaret
> pozu), pencerelerde ve ana sayfada tepki veren karakter. **Özgün** bir tasarımdır: sarı plastik inşaat bareti, baretin
> önünde mavi emaye plaka üstünde küçük altın taç arması, kısa köşeli kestane çene sakalı (bıyık yok), turuncu iş
> tulumu, gök mavisi kraliyet kuşağı, deri alet kemeri ve asa gibi taşıdığı **Altın Mala**. Royal Match'in kralından
> (King Robert) 17 maddede bilinçli olarak ayrılır (§10); yasak birleşimler §11'dedir.

| Dosya | Ne |
| ----- | -- |
| `king/king_front.png` (+ `.svg`) | ön görünüş, 2,5 baş ızgarası |
| `king/king_expressions.png` | 9 ifade (8 klip + şaşkın) |
| `king/king_poses.png` | 8 anahtar poz (her klibin ana pozu) |
| `king/king_palette.png` | 13 malzeme × 4 ton |
| `king/king_scale.png` | okunurluk: 512, oyun köşesi 296, portre 132 ve 64 |
| `king/king_blockout_turnaround.png` | Blender blockout: ön, 3/4 (−32°), yan, arka (rig B, Standard görünüm) |
| `king/king_blockout.py` | blockout betiği (bpy; oranların ve ışık rig'inin kaynağı) |
| `king/gen_sheet.py`, `king/render_png.mjs` | SVG üreticisi ve PNG'ye çevirici (Playwright + kurulu Chromium; yeni bağımlılık yok) |

---

## 1. Kimlik

| Alan | Değer |
| ---- | ----- |
| Ad | **Usta Kral** (TR). EN önerisi **"Builder King"** (açık soru: orkestratör / entrepreneur, D-068 ad ailesiyle birlikte) |
| Rol | Renkli Tepe'nin usta kralı: tacını baretine taktırmış, sarayı bırakıp şantiyeye inmiş kral. Oyunda öğretmen ve tezahüratçı: ipucunu o verir, doğru hamlede sevinir, takılınca düşünür, hamleler bitince **cesaret verir** |
| Kişilik | sıcak, coşkulu, biraz sakar, asla alaycı değil; abartılı yüz ifadeleri, büyük jestler. Kimseyi suçlamaz (CLAUDE: başarısızlık utandırmaz) |
| Yaş | hiçbir metinde geçmez (D-044 ruhu; Tuna için de aynı). Görünüş: olgun yetişkin (sakal, gülme çizgileri), yaşlı değil |
| Ses (JUICE #81) | `sfx_voice_king`: triangle 240 Hz "bla", ±2 yarım ton (Dede'nin 260 Hz tınısının yerini alır) |
| Diğer karakterlerle | Tuna onun çırağı (ikincil, ana sayfa ve hikâye); Kepçe ayağının dibinde (oyun köşesinde küçük); Bay Gribeton karşı görüş |

## 2. Oranlar (1 BU = 1 baş; Blender ölçüsü)

Toplam boy **2,50 BU** (bot tabanı → baret tepesi). "Baş" = baret tepesi → sakal altı = **1,00 BU** (boyun %40'ı).
SVG ölçüsü (512 tuval): zemin y 500, tepe y 18 → 192,8 px / BU.

| Parça | z aralığı (BU) | Genişlik (BU) | Not |
| ----- | -------------- | ------------- | --- |
| Bot + taban | 0,00 – 0,22 | 0,40 × 2, derinlik 0,50 | tıknaz iş botu, krem taban 0,06 |
| Bacak (tulum) | 0,18 – 0,58 | Ø 0,26 | kısa; paça kıvrımı 0,09 |
| Gövde ("tuğla") | 0,48 – 1,48 | omuz 1,06, göbek 1,12, derinlik 0,74 | yuvarlatılmış kutu, köşe yarıçapı 0,26; göbek 0,06 öne |
| Kemer | 0,62 – 0,77 | 1,12 | toka 0,17 × 0,15 |
| Kol (üst / ön) | omuz 1,30 → dirsek 0,98 → bilek 0,74 | Ø 0,23 / Ø 0,18 | dirsekte kıvrık kol ağzı Ø 0,27 |
| El | — | 0,24 × 0,26 | eldiven gibi tombul, başparmak ayrı; parmak çizgisi yalnız ima |
| Baş (yüz kutusu) | 1,49 – 2,27 | 0,90 × 0,80 derinlik | **squircle**: yuvarlatılmış kare, köşe 0,32 |
| Sakal | 1,40 – 1,98 (yanaklar) | 0,92 | çenede 0,22 kalın, alt kenar düz-yuvarlak ("tuğla") |
| Göz | merkez 1,95, x ±0,175 | 0,19 × 0,23 | göz yüksekliği yüz kutusunun **%29**'u (D-087'nin ≤ %30 kuralı) |
| Baret kubbesi | 2,10 – 2,50 | 1,04 × 1,00 | yarım küre, sırt kaburgası 0,12 |
| Siperlik / kenar | 2,06 – 2,13 | Ø 1,20 (ön siperlik 0,34 öne) | — |
| Taç arması | merkez 2,30, öne −24° eğik | 0,28 × 0,20 | §6 |

Siluet kuralları: (1) baret + düz kenarlı sakal + kutu gövde **üç dikdörtgen kütle** okunur; (2) kollar gövdeden her
pozda en az 0,08 BU boşlukla ayrılır (negatif alan); (3) her pozda tek, net bir hareket çizgisi (line of action).

## 3. Biçim dili

Bizim blok dilimiz: **köşeli ama yumuşatılmış**. Baş squircle, gövde iki katlı yuvarlak kutu (tuğla), eller tombul
eldiven, botlar kutu-burun. Keskin köşe yok (en küçük pah 0,02 BU); organik yuvarlaklık yalnız yanak, burun ve göz
çevresinde. Malzeme ayrımı yüzeyle okunur: baret **parlak plastik**, tulum **mat pamuk** (sheen), kuşak **saten**,
kemer **deri**, mala ve arma **metal**, ten **ince yarı saydam** (subsurface 0,12).

## 4. Renk paleti (hex; açık / taban / gölge / derin) ve malzeme

| Malzeme | Açık | Taban | Gölge | Derin | Principled (roughness · coat · metallic · sheen · sss) |
| ------- | ---- | ----- | ----- | ----- | ------------------------------------------------------ |
| Baret (plastik) | #FFF09A | **#FFD43B** | #E8A80C | #B07A00 | 0,28 · 0,6 (coat r 0,06) · 0 · 0 · 0 |
| Taç arması, toka, rozet, mala ağzı (metal) | #FFF2B8 | **#FFD45A** | #E0A21A | #8A5A00 | 0,22 · 0 · 1 · 0 · 0 |
| Arma plakası (emaye) | #7FB8F0 | **#2984DE** | #1C5A97 | #11375D | 0,18 · 0,8 · 0 · 0 · 0 |
| İş tulumu (pamuk) | #F7B866 | **#EC8D20** | #C46A12 | #8E4A0A | 0,72 · 0 · 0 · 0,4 · 0 |
| Kol / paça kıvrımı | #FBD49A | **#F6AC55** | #D98A34 | #A86420 | 0,72 · 0 · 0 · 0,4 · 0 |
| İç tişört (V yaka) | #FFFBEF | **#FFF3D6** | #E6CFA2 | #BFA676 | 0,7 · 0 · 0 · 0,3 · 0 |
| Kraliyet kuşağı (saten) | #9CC6F2 | **#2984DE** | #1C5A97 | #11375D | 0,35 · 0 · 0 · 0,8 · 0; kenarlarda 2 ince altın şerit |
| Alet kemeri, kese (deri) | #C08A52 | **#8A4E1C** | #64360F | #3E200A | 0,5 · 0,2 · 0 · 0 · 0 |
| İş botu | #9A6A40 | **#5A3418** | #3E220E | #24130A | 0,45 · 0,3 · 0 · 0 · 0; bağcık #FFD43B |
| Bot tabanı | #FFF8E6 | **#F2E3C6** | #D4BE94 | #A8905E | 0,6 · 0 · 0 · 0 · 0 |
| Ten | #F8CFA6 | **#E9A877** | #C27F52 | #93583A | 0,45 · 0 · 0 · 0 · 0,12 (radius 1,0 / 0,45 / 0,25); yanak #E8735A α 0,25–0,5 |
| Saç, sakal, kaş (kestane) | #A8653A | **#7A4222** | #5A2E14 | #3A1A08 | 0,6 · 0 · 0 · 0,5 · 0; tutam başına açık çizgi |
| Mala sapı (ahşap) | #D9A066 | **#B8763E** | #8A5426 | #5A3412 | 0,55 · 0 · 0 · 0 · 0 |
| Göz | akı #FFFFFF → #DCE6F2; iris #E0A45A / #C88A3E / #7A4512 (amber-kahve, radyal); bebek #1E0F06; iki parlama (büyük saat 10–11, küçük saat 4–5) | | | | akı 0,12 + coat 0,5; iris coat 0,8 |
| Ağız | iç #6A1F2A, dil #FF7A7A, diş #FFFDF6 | | | | 0,5 |

Renk düzeni bilinçlidir: **turuncu (firma rengi: kamyon, Tuna'nın yeleği, vinç kancası) + gök mavisi (B bloğu) +
güvenlik sarısı + kestane**. Mor ve altın ağırlıklı kraliyet düzeni (RM) yok; kırmızı + beyaz kürk (klasik kral
pelerini) yok. Turuncu tulum Tuna'nın turuncu yeleğiyle "aynı firma" bağı kurar; ikisi yan yana durduğunda ayrım
siluetten (baret rengi: kral sarı / Tuna nane; gövde kütlesi) ve mavi kuşaktan gelir.

## 5. Yüz ve ifade aralığı

Yapı: iri oval gözler (iris gözün %66'sı), **kalın, yuvarlak uçlu "tuğla" kaşlar** (asıl oyunculuk aracı; baret
siperliğinin **önünde** çizilir, asla altında kaybolmaz), küçük yuvarlak-kare düğme burun (ten renginde, yalnız
parlama), geniş ağız (sakalın üstünde, tam görünür: **bıyık yok** — ağız bütün ifadeyi taşır), kestane çene sakalı
yanak çizgisinden çeneye bant, çenede düz-yuvarlak alt kenar.

| İfade (klip) | Gözler | Kaşlar (dy BU, açı) | Ağız | Yanak |
| ------------ | ------ | ------------------- | ---- | ----- |
| `neutral` (idle) | açık, merkez | 0, dış uç +4° | kapalı gülümseme yayı | α 0,25 |
| `cheer` | mutlu yay (^ ^) | −0,05, +10° | açık gülüş, üst diş sırası, dil | α 0,4 |
| `big_cheer` (kazanma) | mutlu yay, daha kavisli | −0,08, +16° | çok geniş açık "D", diş + dil | α 0,5 |
| `think` (takılma) | açık, sağ-yukarı bakış (+0,03, −0,035) | sol +0,02 −8°, sağ −0,06 +14° (tek kaş kalkık) | yana kaymış, büzük | α 0,2 |
| `talk` (öğretici) | açık | −0,02, +6° | ağız şekilleri A / O / E / M (klipte döngü) | α 0,25 |
| `point` (işaret) | açık, hedefe (+0,035) | +0,01, −8° (kararlı) | kapalı dişli sırıtış (bir köşe yukarı) | α 0,3 |
| `encourage` (hamle bitti) | yumuşak (alt kapak kalkık, sıcak) | −0,03, **iç uçlar yukarı** −12° (empati) | küçük açık sıcak gülümseme | α 0,35 |
| `wave` (ana sayfa) | açık, hafif yukarı | −0,04, +8° | açık gülüş | α 0,35 |
| `surprised` (büst) | iri (iris küçük) | −0,08 | küçük "o" | α 0,2 |

Yasak ifadeler: ağlama, kızgın kaş + bağırma, alaycı tek kaş + sırıtış, hayal kırıklığıyla baş sallama (oyuncuyu
suçlayan her okuma). Hata anında kral **düşünür** (`think`), hamleler bitince **cesaret verir** (`encourage`).

## 6. Baret ve taç arması (karar)

İki seçenek değerlendirildi:

| Seçenek | Artı | Eksi | Sonuç |
| ------- | ---- | ---- | ----- |
| (a) Baretin **önünde** küçük kabartma taç arması | siluet saf baret: "taç takan kral" siluetinden (RM) en uzak; arma bir firma logosu gibi okunur; animasyonda baret tek parça kalır | 64 px'te taç küçük kalır | **SEÇİLDİ** |
| (b) Baretin **üstünde** küçük taç | "kral" siluetten anında okunur | taç-başta siluet RM kralının imza siluetine yaklaşır; ikincil hareket (sallanan taç) RM tepkisini çağrıştırır | reddedildi |

Arma tarifi: baret kubbesinin önünde, kubbe normaline oturan (öne −24° eğik) **0,28 × 0,20 BU yuvarlatılmış
dikdörtgen plaka**, gök mavisi emaye (#2984DE, coat 0,8), 0,02 BU altın çerçeve; üstünde **3 uçlu altın taç**: alçak
taban bandı (0,18 × 0,05), uçlar küt üçgen ve her ucun tepesinde **yuvarlak çıkıntı (stud)** — bizim tuğla
çıkıntımızın dili. **Taçta taş yok** (RM'nin ön-orta fasetli kırmızı/pembe taşı alınmaz). Okunurluk: 132 px portrede
arma 26 px, 64 px'te 13 px (mavi plaka + altın kontrastı ile, `king_scale.png`).

Baret: güvenlik sarısı parlak plastik, sırtta 0,12 BU kaburga, kenar Ø 1,20, önde kısa siperlik. İkincil hareket:
sıçramada baret 1–2 kare gecikmeyle iner (`cheer`), büyük sevinçte başından havalanıp geri oturur (`big_cheer`
kare 10–22). Baret kayışı yok.

## 7. Kıyafet

- **İş tulumu** (tek parça, uzun kollu, dirseğe kadar kıvrık): turuncu pamuk, ön ortada fermuar (kesik çizgi), göğüste
  sağda (izleyicinin sağı) cep + sarı kalem, V yakada krem iç tişört, iki yaka kanadı açık turuncu. **Askılı (bib)
  tulum değil**, kareli gömlek yok (Bob the Builder ayrımı, §10.2).
- **Kraliyet kuşağı**: sağ omuzdan (izleyicinin solu) sol kalçaya, 0,17 BU genişlik, gök mavisi saten, kenarlarda ince
  altın şerit, arkada aynı açıyla iner. Üstünde **3 altın rozet**: bizim blok sembollerimiz **yaprak (G), parıltı (B),
  elmas (P)** kabartmalı. Kuşak kemerin **üstünden** geçer.
- **Alet kemeri**: kahverengi deri, altın kare-yuvarlak toka (içinde deri delik), sol kalçada (izleyicinin solu) kese
  + kırmızı saplı tornavida ucu, sağ kalçada mala askısı (mala elde değilken).
- **Pelerin, kürk yaka, cüppe, omuzluk yok.**
- **Botlar**: kahverengi deri iş botu, krem kalın taban, sarı bağcık (çarpı).

## 8. Aksesuar — Altın Mala (asa)

Kralın imza nesnesi oyunun 4. güçlendiricisidir (`icon_gold_trowel`, K-33): aynı model. Ağız: baklava (elmas) biçimli
altın mala ağzı (0,32 × 0,42 BU, kalınlık 0,06), ortasında ince parlak sırt; altın bilezik (ferrule); ahşap sap
(Ø 0,07 × 0,26). Duruşta sağ elde **ağız yukarı** asa gibi tutulur; `point` klibinde işaret ucu malanın ucudur
(öğretici eldiveniyle çakışmaz: eldiven hedef hücrede, kral köşeden malayla gösterir); `big_cheer`'de havaya kalkar.

## 9. Anahtar pozlar (`king_poses.png`)

| Klip | Anahtar poz | Hareket çizgisi | İlke (12 ilkeden) |
| ---- | ----------- | --------------- | ----------------- |
| `idle` | ayakta, sol el yanda açık, sağ elde mala ağzı yukarı | dik, hafif S | ikincil hareket: baret 4 kare gecikmeli, mala ±3° |
| `cheer` | iki yumruk havada, ayaklar yerden 0,13 BU | dikey C | hazırlık (çömelme), esneme-ezilme, aşma |
| `big_cheer` | büyük sıçrama, mala ve el yukarı, baret havada | eğik, gövde −4° | abartı, takip (baret geç oturur), zamanlama |
| `think` | sağ elin işaret parmağı sakalda, baş 3° yana, gözler yukarı | baş eğik | yavaş giriş-çıkış, ikincil (parmak tıklaması) |
| `talk` | sol el açık avuç öne, sağ elde mala | hafif öne | sahneleme (avuç izleyiciye), ritim (beat) |
| `point` | sağ kol yatay, mala ucu hedefe | yatay, beden zıt yöne | hazırlık (geri çekiş), aşma (6°), sabitleme |
| `encourage` | sol avuç öne açık, sağ başparmak yukarı | hafif geri sonra öne | sahneleme, katı çizim (iki el ayrık) |
| `wave` | sağ kol yukarı, ön kol sallanır | eğik, beden karşı salınım | örtüşen hareket (el 2 kare geride) |

## 10. Fark tablosu — Royal Match'in kralı (King Robert) ile

Kaynak: proje sahibinin referans ekranları `owner-2026-10-14/ref-08, ref-09, ref-11` (oyun içi, mutfak sahnesi),
`ref-12` (koleksiyon), `ref-13`, `ref-15` (kral + uşak + köpek). Gözlenen RM kralı: büyük altın taç (yan yatık, ön
ortada fasetli pembe-kırmızı taş), gür beyaz sakal + kıvrık beyaz bıyık + gür beyaz kaş, büyük yumru pembe burun, küçük
koyu gözler, mor tunik (kollarda açık mor şeritler, kabarık), geniş altın yaka arkada altın-sarı pelerine dönüşür, koyu
kemer + büyük altın kare toka, mor pantolon, altın tokalı mavi ayakkabı, ~4 baş boy, varil gövde, sık kullanılan poz
kollar kavuşturulmuş.

| # | Boyut | RM kralı (King Robert) | Usta Kral (bizim) |
| - | ----- | ---------------------- | ----------------- |
| 1 | Ad | King Robert | Usta Kral (EN önerisi "Builder King") |
| 2 | Başlık | büyük altın taç, başın üstünde, yan yatık | sarı plastik **inşaat bareti**; taç yalnız 0,28 × 0,20 BU arma |
| 3 | Taç biçimi ve yeri | uzun sivri uçlar, ön ortada fasetli kırmızı/pembe **taş** | mavi emaye plakada 3 küt uç, uçlarda **yuvarlak tuğla çıkıntısı**, **taş yok**, baretin önünde |
| 4 | Sakal | gür, uzun, kabarık **beyaz** tam sakal | kısa, **köşeli kestane çene sakalı** (yanak → çene bandı) |
| 5 | Bıyık | kıvrık uçlu beyaz bıyık | **bıyık yok** (ağız tamamen görünür) |
| 6 | Kaş | gür beyaz kaş | kalın, yuvarlak uçlu kestane "tuğla" kaş, siperliğin önünde |
| 7 | Burun | büyük, yumru, pembe | küçük yuvarlak-kare düğme burun, ten renginde |
| 8 | Göz | küçük, koyu, yuvarlak | iri oval, amber-kahve iris, iki parlama |
| 9 | Ten | açık pembe-bej | sıcak buğday / bronz (#E9A877) |
| 10 | Oran ve siluet | ~4 baş, iri göğüslü varil gövde, uzun bacak | **2,5 baş**, iri baş (%40), yuvarlatılmış **tuğla** gövde, kısa bacak |
| 11 | Kıyafet | mor tunik, kabarık şeritli kollar | turuncu **iş tulumu**, dirseğe kıvrık kol, V yaka, fermuar, cep + kalem |
| 12 | Pelerin / kürk | altın yaka + altın-sarı pelerin | **pelerin yok, kürk yok, yaka yok** |
| 13 | Renk düzeni | mor + altın (+ beyaz) | turuncu + gök mavisi + güvenlik sarısı (+ kestane) |
| 14 | Kraliyet işareti | taç + pelerin | **gök mavisi kuşak** + 3 blok sembolü rozeti (yaprak, parıltı, elmas) |
| 15 | Kemer | koyu kemer, büyük altın kare toka | deri **alet kemeri**, kese, tornavida, mala askısı |
| 16 | Ayakkabı | altın tokalı mavi ayakkabı | kahverengi **iş botu**, krem taban, sarı bağcık |
| 17 | El nesnesi | yok | **Altın Mala** (oyundaki güçlendirici) asa gibi |
| 18 | Oyun içi rol | sıkıntıdaki kral, "kralı kurtar" sahneleri | usta-öğretmen, **asla kurtarılacak konumda değil**; tezahüratçı |
| 19 | Yan karakterler | uşak (kırmızı frak, gri saç, kıvrık bıyık), turuncu uzun kulaklı köpek + kırmızı fular | uşak yok; Kepçe sosis köpek (turuncu kask), Tuna çırak |
| 20 | Sikke amblemi | RM sikkesinde taç | bizim sikkede **mala** (taç sikkede asla kullanılmaz) |

### 10.1 Yasak listesi (tek tek ve birlikte)

1. Başın üstünde **taç olarak giyilen** her taç (arma boyunun 1,2 katını aşan her taç biçimi).
2. Taçta ön-orta **fasetli kırmızı / pembe taş**.
3. **Beyaz / gri** gür sakal; kıvrık uçlu bıyık (her renkte); gür beyaz kaş.
4. Mor tunik + altın yaka + altın / sarı pelerin birleşimi; **kırmızı pelerin + beyaz kürk (ermin) yaka** birleşimi.
5. Büyük yumru pembe burun.
6. Kalın koyu kemer + büyük altın kare toka birlikte; altın tokalı mavi ayakkabı.
7. "Kral + uşak + köpek" üçlü kompozisyonu; kollar kavuşturulmuş kral pozu pencere illüstrasyonunda.
8. Uşak karakteri (kırmızı frak, gri saç, kıvrık bıyık); turuncu, uzun kulaklı köpek + kırmızı fular.
9. Mutfak / sıkıntıdaki kral sahneleri, "kralı kurtar" metinleri ve görev kalıpları.
10. Sikkede taç amblemi.
11. Bob the Builder birleşimi: sarı baret + **mavi askılı (bib) tulum** + **kareli gömlek** + temiz traş yüz.
12. Mario birleşimi: kırmızı kep + mavi askılı tulum + gür koyu bıyık; Wario birleşimi: sarı kep + mor tulum +
    zikzak bıyık.

### 10.2 Diğer tanınmış figürlerle çapraz denetim

| Figür | Paylaşılan | Ayıran |
| ----- | ---------- | ------ |
| Bob the Builder | sarı baret, alet kemeri | turuncu tek parça tulum (askılı değil), kareli gömlek yok, **sakal**, taç arması, kuşak, 2,5 baş squircle kafa |
| Mario / Wario | tulum + yüz kılı | kep yok (baret), **bıyık yok**, renk düzeni turuncu + mavi kuşak |
| Handy Manny | alet kemeri | sakal, baret, taç arması, kuşak |

## 11. 3B üretim notları (B1k için)

- Model: blockout (`king_blockout.py`) oranları ±%3; final topoloji yüz ifadeleri için **şekil anahtarı (shape key)**:
  `brow_L/R_up`, `brow_L/R_in`, `eye_L/R_close`, `eye_happy`, `mouth_smile`, `mouth_open`, `mouth_O`, `mouth_E`,
  `mouth_M`, `mouth_side`, `cheek_puff`; göz bebekleri ve iris ayrı nesne (bakış yönü = döndürme).
- İskelet: Blender armature (kök, kalça, omurga 2, boyun, baş, baret (baş çocuğu, ikincil hareket için ayrı kemik),
  omuz / üst kol / ön kol / el × 2, mala (sağ el çocuğu, ayrılabilir), bacak 2 × 2, bot); sakal ve kuşak sert parça.
- Kurgu: bütün klipler **K0** (idle'ın 0. çizimi) pozuyla başlar ve biter (`docs/ART_3D.md` §7.1) — klipler arası
  geçiş kesmesiz.
- Işık: rig B (`frames.json → rigs.B_char`), Standard görünüm; gölge yakalayıcı temas gölgesi kareye pişirilir.
- Portre (büst): baret tepesi + sakal altı **Ø 132 dairenin içine** sığar (kırpım dairesi büstün %80'i); `king_scale.png`
  bu kuralın SVG sınamasıdır (portre 132'de çene kırpılmış görünür → büst kadrajı 0,9× ölçekle çizilir).
- Kabul: (1) ön siluet blockout siluetiyle XOR alanı ≤ %6; (2) 64 px portrede taç arması ve sakal seçilir; (3) 296 px
  oyun köşesinde ifade 1 sn'de okunur (9 ifadenin ikili karışma testi: 5 kişiden ≥ 4'ü doğru); (4) §10.1 yasak
  listesinin hiçbir maddesi yok; (5) mağaza öncesi ters görsel arama (ASSET §15).
