# Bölümler v3c2 şüpheci inceleme (REVIEW_LEVELS_V3C2)

İnceleyen: code-lead (bağımsız şüpheci; depo kodu değiştirilmedi) · Tarih: 2026-11-12 ·
Yöntem: stüdyo `qa-lead` rolü + `balance-check` (Progression) ve `design-review` ölçütleri, `level-designer` /
`systems-designer` / `ux-designer` bakışı. Orkestrasyonlu koşu: soru sorulmadı, seçenekler ve seçimim her bulguda yazılı.
Ölçüm betikleri yalnız scratch'te koştu (repoya girmedi); yöntemleri ilgili bölümde tarif edildi.

İncelenen sürümler:

| Repo | Ref | İçerik |
| --- | --- | --- |
| web `minik-usta` | `origin/wip/phase-2r` = `70d7dc8` (A2 `e75023b`, `12715f2`; A1 `98a01c0`; B1 `b92120a`; C2 `2a08cd2`) | D-106 çekirdeği, çözücü vekili, Bölüm 4–10 verisi, GDD/OBSTACLES/LEVELS/STORY |
| Unity `minik-usta-unity` | `origin/main` = `4a4cc07` (C1 `afe949e`, `55bc84a`; belge `7e17c51`, `4a4cc07`) | C# eşi, D-108 görünümü, bonus patlaması düzeltmesi, ci-shots |
| Unity `ci-shots` | `0c8f3c0` (çekimler `55bc84a`) | 18 ekran + 2 şerit / boyut, 390×844 ve 360×800, DPR 2 |

Önem: **Engel** = bağlayıcı karar ya da kapı karşılanmıyor, iş geri döner · **Önemli** = bu turda kapanmalı ·
**Öneri** = birikim listesi. Sonuç: **Engel yok; 5 Önemli, 10 Öneri.** Kapılar yeşil; D-106 kural ↔ TS ↔ C# ↔ test
zinciri sağlam; D-107 sayısal hedeflerinin hepsi tutuyor. Açık kalanlar oyun deneyimi tarafında: Bölüm 7'nin öğretim
sırası ve bonus anı yalnız kanonik çözümde tutuyor, Kolay 3–4 aynı vekilde Zor finalden zor, 7–9'da şekil çeşitliliği
geriledi, iki EN öğretici metni 6 kelimeyi aşıyor.

## 1. Kendi koşturduğum komutlar (kanıt)

| Komut (kendi worktree'lerimde) | Sonuç |
| --- | --- |
| web `node tools/solve.ts --check --no-cache --jobs 4` | **yeşil**, 13 dk 6 sn: validate 10 dosya 0 hata / 0 uyarı; solve 10/10 (0 önbellekten) **0 hata / 0 uyarı**; bot aşaması "not present — skipped". Önbellekteki `artifacts/solver/*.json` ile fark yalnız `cacheKey` (`srcf6518d28` → `src0afd9b08`) ve süre alanları → determinizm OK |
| web `npm run check` | yeşil, 8 dk 46 sn: typecheck + lint + format + vitest **125 dosya, 2317 geçti, 0 atlandı** + `test:rules` (88 kimlik, 1702 test) |
| web `npm run build` | yeşil; ilk yükleme **614,3 / 620 KB** (5,7 KB pay) |
| web CI (`gh run list`) | `70d7dc8`, `2a08cd2`, `b92120a`, `98a01c0`: `check` ✓ + `levels-nocache` ✓ (C2'de 15,5 dk gerçek koşu, 0 uyarı). A2'nin iki belge commit'i (`e75023b`, `12715f2`) **kırmızı** (CL-V3C2-12) |
| Unity `cd dotnet && dotnet build -warnaserror` | yeşil, 0 uyarı / 0 hata |
| Unity `node tools/diff/ts-oracle.mjs <web 70d7dc8>` | 851 dizi, 24 007 eylem, kapsam kapıları geçti (§3) |
| Unity `dotnet test` (kâhin çıktısıyla) | Core **2151 geçti / 5 atlandı** (JS uyumluluk notları), Presentation **609 geçti**; DiffFuzz 0 sapma. Kâhin çıktısı yokken Core 1298 / 8 atlanan (3 kâhin testi atlanır) |
| Unity `bash tools/stub-gate-selftest.sh` | yeşil (4/4) |
| Unity CI `55bc84a` (run 43296036331) | Build WebGL ✓, CI screenshots ✓ (`ok`, hata 0, konsolda istisna 0), Deploy Pages ✓. Pages dağıtım kaydı `55bc84a` (son kod commit'i; `4a4cc07` yalnız belge) |
| Kendi vekil ölçümüm (§4, §5) | 1–10, bağımsız tohumlar, 1 200–2 000 oyun, Wilson %95 |
| Kendi senaryom: Bölüm 7 öğretici ve bonus (§6) | 2 000'er oyun |

NOT ASSESSED (tahmin yapılmadı, "geçti" sayılmaz): canlı Pages sayfası (sandbox proxy 403; yalnız dağıtım kaydı
doğrulandı), cihazda FPS, orta bot (`levels:bot` yazılmamış), proje sahibinin referans ekranlarıyla yan yana karşılaştırma
(`artifacts/reference/` bu ortamda yok), web sunumunda D-108 görünümü (web palet çizmiyor, CL-V3C2-08).

## 2. D-106: kural ↔ TS ↔ C# ↔ test

Kural metni GDD K-58 madde 4 ve 10 (KABUL, D-106), V3-24; OBSTACLES Y10. Kod: web `grid.collisionMasks` yalnız `exclude + 1`
atlar (`ownCellCode` silindi), Vinç `yardSpotFree` `self + 1`, `deadlock.quickHoldable` her palet katmanını BFS'e bırakır,
`palletStuck` ve L-41 `pallet_stuck` silindi, `RULES_VERSION` 6. C# `Grid.CollisionMasks`, `Boosters.YardSpotFree`,
`Deadlock.QuickHoldable`, `Session.RulesVersion = 6` satır satır aynı.

| Kenar durumu | Kural | TS | C# | Test / kanıt |
| --- | --- | --- | --- | --- |
| Katman kendi yığınından çıkar (C3, L4, T4, J4, O4) | madde 4, 10 | ✓ | ✓ | `palletD106.test.ts` (5 şekil), `PalletD106Tests` (19; eski çekirdekte 10'u kırmızı) |
| Çıkarken başka bloğa değer | madde 4 | çarpışır | çarpışır | "capped by a B1 it cannot move (immovable)"; P1 vektörlerinde 5 geniş tahtanın her tutuşunun R kümesi düğüm düğüm TS ile aynı |
| Palet başka blok için dolu | madde 2 | ✓ | ✓ | "the pallet stays solid for every other block" |
| Yan kayma (yanı boşsa kendi hücresinin üstünden) | madde 4 | ✓ | ✓ | "D2_90 example … slides sideways"; A1'de Bölüm 10 v3c `min` 22 → 21 bu yüzden (bkz. açık soru 1) |
| Duvar / çerçeve | madde 4 | ✓ | ✓ | "a layer under a wall higher than the board has only cancels" |
| Vinç → saha hedefi | madde 9 e | `palletSiteOnly` | aynı | fuzz'da 110 ret (104 rastgele + 6 betik); Vinç ile şantiyeye 16 uygulanmış |
| Altın Mala | madde 9 e | değişmedi | değişmedi | fuzz'da palet katmanına 28 uygulanmış |
| Geri Al | madde 6 | tampon geri yüklenir | aynı | fuzz'da D-106 çıkışından hemen sonra Geri Al **16** kez, bit eşit |
| Söküm | madde 6 | tampon geri yüklenir | aynı | fuzz'da D-106 çıkışından sonra Söküm **0** kez (CL-V3C2-09) |
| D1 (son katman dahil BFS) | K-30 | ✓ | ✓ | "the last pallet layer behind an uncrossable wall is no move" |
| `pallet_stuck` emekli | madde 10 | ✓ | ✓ | "pallet_stuck is retired" (6 şekil) |

Sonuç: D-106 zinciri tam; TS ↔ C# eşitliği hem R kümesi düzeyinde (P1 vektörleri) hem eylem düzeyinde (DiffFuzz) kanıtlı.
GDD K-58 madde 4 metni koddaki yan kaymayı açıkça yazıyor.

## 3. Fuzz kapsamı (kendi ürettiğim kâhin çıktısı, web `70d7dc8`)

C1'in sayıları birebir tekrarlandı (kâhin deterministik): 851 dizi, 24 007 eylem; Vinç `palletSiteOnly` reddi 110;
sürükleme `palletSiteOnly` iptali 164 (sayaçta 161 + betik); D-106 kendi ayak izinden çıkış C3 171 / L4 50 / O4 44 /
J4 42 / T4 20; P5 205 / S4 178 / Z4 89 uygulanmış hamle; örtülü katmana dokunma 751; `movesChanged{bonus}` 30.
Kapı eşikleri (Vinç reddi ≥ 20, D-106 toplam ≥ 20 ve aile ≥ 3, P5/S4/Z4 ≥ 5) geçti. Benim taramam: paletli kaynaklarda
D-106 çıkışı içeren 78 dizi; çıkış → Geri Al 16; çıkış → Söküm **0** (CL-V3C2-09). Unity CI `dotnet test` ve kâhini
koşturmuyor; C# eşitliği yalnız ajan koşularına dayanıyor (CL-V3C2-15).

## 4. Bölümler — D-107 hedefleri (balance-check: Progression)

Kaynak: `levels/level_004…010.json`, golden'lar, önbelleksiz çözücü (kendi koşum).

| # | Etiket | N (hedef) | D2 payı | Tekil aile / yönelim | Palet | Bonus | `min` (alt sınır) | hamle = min + P + a | YAO | `plan_trap` |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 4 | Kolay | 10 | %60 | 2 | – | – | 12 (11) | 16 = 12 + 4 | 10/10 | 0 (Kolay'da error kapısı) |
| 5 | Normal · Y9 | 12 | %67 | 4 | – | – | 14 (13) | 18 = 14 + 4 | 12/12 | 0 |
| 6 | Normal · S9 | **17** (≥ 17) ✓ | %47 | 4 / 7 | – | – | 19 (18) | 23 = 19 + 4 | 17/17 | 0 |
| 7 | Normal · Y10 | **20** (≥ 18; 19–20) ✓ | %50 | 3 / 7 | `O4_0` Y ×4 | `D2_0` R +2 | 21 (21) | 25 = 21 + 4 | 20/20 | 0 |
| 8 | Normal · Y11 | **19** ✓ | %47 | 3 / **5** | `C3_0` W ×3 | – | 20 (20) | 24 = 20 + 4 | 19/19 | 0 |
| 9 | Normal · Y13 | **19** ✓ | %47 | 3 / **5** | `C3_270` G ×3 | – | 21 (20) | 25 = 21 + 4 | 19/19 | 0 |
| 10 | Zor | **20** (≥ 20; 20–22) ✓ | %35 | 4 / 6 | `C3_0` W ×4 | – (etkin pay 3) | 21 (21) | 24 = 21 + 1 + 2 | 20/20 | 0 |

Domino ≤ %50 ✓ (Bölüm 7 sınırda), YAO ≥ %60 ✓ (hepsi 1,00), `min` ≤ 22 ✓, bonus yalnız Bölüm 7 ✓, K-55 tek parti ✓,
D-090: 10 000 yürüyüşte yakalanmayan çıkmaz 0 (**tahmin**). Bölüm 1–3 golden'ları değişmedi.

**Şekil çeşitliliği:** v3c'de 7–9'un paletsiz bloklarında 6 dörtlü blok vardı (L4 ×2, L4 ×3, T4 ×1); v3c2'de **0** —
7–9'un paletsiz blokları yalnız D2, C3, I3. Blok sayısı dörtlüleri ikili/üçlü bloklarla değiştirerek arttı (CL-V3C2-04).

**Zorluk bantları (resmî ileri bakış vekili, ε 0,1, bütçe = `moves`), kendi ölçümüm:** resmî kapı tohum = bölüm kimliği ile
400 oyun koşuyor. Ben 3 bağımsız tohumla (101, 202, 303) 1 200 oyun koşturdum:

| # | Bant | Resmî (400, tohum = id) | Bağımsız 1 200 oyun [Wilson %95] | Tohum başına | moves − 1 / + 1 | ε 0 (400 oyun) |
| --- | --- | --- | --- | --- | --- | --- |
| 6 | 0,80–0,90 | 0,83 [0,79–0,86] | **0,810 [0,787–0,831]** | 0,81 / **0,78** / 0,85 | 0,70 / 0,88 | 1,00 |
| 7 | 0,80–0,90 | 0,86 [0,83–0,89] | 0,851 [0,830–0,870] | 0,86 / 0,84 / 0,85 | 0,77 / 0,90 | 0,96 |
| 8 | 0,80–0,90 | 0,86 [0,82–0,89] | 0,858 [0,837–0,876] | 0,87 / 0,85 / 0,85 | 0,75 / 0,92 | 1,00 |
| 9 | 0,80–0,90 | 0,88 [0,84–0,91] | 0,844 [0,823–0,864] | 0,84 / 0,85 / 0,85 | 0,74 / 0,92 | – |
| 10 | 0,50–0,60 | 0,54 [0,49–0,59] | **0,560 [0,532–0,588]** | 0,55 / 0,56 / 0,56 | 0,38 / 0,69 | 0,93 |

Bantların hepsi tutuyor; final %50–60 içinde. Bölüm 6 bandın alt kenarında: Wilson alt ucu 0,787, bir tohumda 0,78
(CL-V3C2-06). Finalin zorluğu içgörüden değil hata toleransından geliyor: hatasız (ε 0) ileri bakan oyuncu %93 bitiriyor,
%10 rastgele hamleyle %56. Bu D-107'nin ε 0,1 tanımıyla uyumlu (dar bütçe = zor); bulgu değil, kayıt.

## 5. Zorluk eğrisi 1–10 (aynı vekil)

| # | Etiket | ε 0,1 (2 000 oyun) [Wilson] | ε 0 (1 200) | ε 0,2 (1 200) | ε 0,1, `moves` + 4 / + 8 |
| --- | --- | --- | --- | --- | --- |
| 1 | Kolay | 0,998 | – | – | – |
| 2 | Kolay | 0,961 | – | – | – |
| 3 | Kolay | **0,509 [0,487–0,531]** | 0,59 | 0,35 | 0,66 / 0,77 |
| 4 | Kolay | **0,430 [0,408–0,452]** | 0,54 | 0,29 | 0,55 / 0,59 |
| 5 | Normal | 0,558 [0,536–0,580] | 0,73 | 0,35 | 0,85 / 0,94 |
| 6–9 | Normal | 0,81–0,86 (§4) | 0,96–1,00 | – | – |
| 10 | Zor | 0,56 (§4) | 0,93 | – | – |

Aynı resmî ölçüyle Kolay 3 ve 4, Normal 6–9'dan 30–40 puan zor ve Zor finalden zor (4: 0,43 < 10: 0,56). Hatasız
oyuncuda fark daha da açık (4: 0,54, 10: 0,93). Bölüm 4'te bütçe 8 hamle büyüse de oran 0,59'da kalıyor: darboğaz bütçe
değil, vekilin göremediği iki derin kazı (B1 §8.15.6 madde 3 bunu söylüyor). Ya Bölüm 3–5 gerçekten zor (eğri 3–5'te
tepe yapıp 6'da düşüyor), ya vekil derin kazılarda kör (o zaman 6–10 bantlarının anlamı da sorgulanır). İki okumada da
"abartısız zorluk" ve Kolay etiketi kanıtlanamıyor (CL-V3C2-03).

## 6. Bölüm 7 senaryosu: öğretici ve bonus (palet tanıtımı)

Yöntem: çözücünün hamle kümesiyle "görünen doğru yerleşimi al, yoksa rastgele kaydır" oyuncusu (resmî yerleştiren vekil),
ε 0 / 0,2 / 1; her oyunda 1. kat bittiği an (`visibleSegment` 0 → 1 = `segmentDone`) `holdableIgnoringMoves(piece 16)` ve
`hasReachableCorrect(piece 16)` sorulur. Doğrulama: kanonik golden yeniden oynatıldığında 9. hamlede ikisi de `true`.

| Ölçüm | ε 0 | ε 0,2 | ε 1 (rastgele) |
| --- | --- | --- | --- |
| 1. kat biten oyun | 1 948 / 2 000 | 1 924 / 2 000 | 188 / 2 000 |
| O an palet üst katmanı (piece 16) tutulabilir ve doğru yere gidebilir | **75 (%3,9)** | **89 (%4,6)** | 10 |

En sık durum: 1. kat R olarak 2, 3, 4 ve 10 ya da 12 (bonus), W olarak 6 ve 7 ile biter; paletin üstündeki `D2_0` R (0,2) ve `D2_0` W (1,2) ile yanındaki
`D2_0` R (2,0) sahada kalır. 1. katın R/W talebini paletin üstündeki bloklar dışındaki R/W bloklar da karşılıyor; "paletin
üstünde yalnız 1. katın blokları" kısıtı kanonik çözüm için doğru, oyuncu için değil. Öğretici adım 1 (`startOn
segmentDone`, el `tap` piece 16, `tut.m.pallet`) oyunların ≈ %96'sında **tutulamayan** bir katmanı gösterir; adım 1
`overWall` (parça süzgeçsiz) ile başka bir blokla biter, adım 2 ("Katmanı kaldır, şantiyeye bırak.") yine örtülü paleti
gösterir (CL-V3C2-01).

Bonus (`D2_0` R (5,2), piece 12), ε 0,1, 2 000 oyun: yerleşen 1 929; **1. katta 1 569 (%81)**, 2. katta 228, 3. katta 132;
ilk palet katmanından **önce 1 783 (%92)**. Kanonik çözümde bonus 20. hamlede (son kat). D-107'nin "palet tanıtımının
ödülü" niyeti oyuncuların büyük çoğunluğunda gerçekleşmiyor: "+2" paletten önce, 1. katta patlıyor (CL-V3C2-02).

## 7. Öğretici metinleri ve anahtarlar (K-53, STORY §0-4 ve §6A)

| Bölüm | Anahtar | TR (kelime) | EN (kelime) | Bağ |
| --- | --- | --- | --- | --- |
| 6 | `tut.m.widesite` | Şantiye genişledi: yatay blok sığar. (5) | Wider site: flat blocks fit. (5) | vurgu `T4_270` (3 geniş, düz) ✓ |
| 7/1 | `tut.m.pallet` | Paletten hep en üstteki alınır. (5) | Take from the top of the pallet. (**7**) | §6 |
| 7/2 | `tut.m.palletsite` | Katmanı kaldır, şantiyeye bırak. (4) | Lift a layer, set it on the site. (**8**) | §6 |
| 8 | `tut.m.wrap` | Önce makaslı bloğu yerleştir. (4) | Place the scissors block first. (5) | ✓ |
| 9 | `tut.m.slippery` | Kaygan blok boşlukta durmaz, kayar. (5) | Slippery blocks slide into gaps. (5) | ✓ |
| 10 | `tut.m.cranebooster` | Vinç gömülü bloğu da taşır. (5) | The Crane moves buried blocks too. (6) | ✓ |
| kart | `card.y10.site` | Katman sahaya inmez, yalnız şantiyeye gider. (6) | Layers never rest on the field, only on the site. (10; kart sınırı 12) | Unity kartında görünür ✓ |

TR/EN anahtar eşitliği tam (web testi yeşil). STORY §0-4 "öğretici balonu TR ve EN ≤ 6 kelime" iki EN satırında
çiğneniyor; STORY §6A notu "EN 5–6" diyor (yanlış). Web'de EN sayım testi yok, TR testi eski sınırla (≤ 8) koşuyor. Unity
üstyazısı geçici 6 kelimelik EN metin kullanıyor (CL-V3C2-05). Öğretici ≤ 2 adım ✓.

## 8. Görsel kanıt (ci-shots `55bc84a`, 390×844 ve 360×800, DPR 2)

Okuduğum çekimler (Read, iki boyut): `v3_pallet_7`, `v3_bonus_burst_7`, `v3_final_10`, `v3_info_7`; ayrıca 4× büyütülmüş
kesitler. Konsol günlüklerinde istisna / hata 0.

- **`v3_bonus_burst_7` var** (iki boyutta): HAMLE plakasında altın "+2", sayaç 7 (25 − 20 + 2), "1 blok", şantiyede yeni
  kırmızı bonus bloğu. "+2" yazısının turuncu konturu köşeli görünüyor (yumuşak kontur değil).
- **"×n" beyaz daire** (D-108): 360'ta dış kontur 46–48 fiziksel px = **69–72 design px** (≥ 56 ✓); "4" rakamı ≈ 21 fiziksel
  px = 10,5 CSS px. 390'da 50–51 fiziksel px. Altın yıldız "+2" ile biçim (daire / 5 kollu yıldız), renk (beyaz / altın) ve
  işaret (×n / +n) olarak ayrışıyor ✓ (Bölüm 7 aynı tahtada ikisi var).
- **Kızak:** gri-mavi metal, perçinli, ≈ 12 fiziksel px (≈ 18 design px) dudak; W kahverengisiyle karışmıyor (Bölüm 10
  W paleti altında açıkça ayrı) ✓. Katman bantları 360'ta ≈ 2 CSS px adımla seçilmesi zor; yığın derinliği rozetten okunuyor.
- **Diğer rozetler (360):** makas ve kaygan dairesi 37–38 fiziksel px = **55–57 design px** — 56 eşiğinde, payı yok
  (CL-V3C2-14). "×4" rozeti Bölüm 10'da soldaki sargılı bloğun ağının üstüne taşıyor, iki bölümde de kendi hücresindeki
  simgenin bir kısmını örtüyor (aynı bloğun diğer hücrelerinde simge görünür; renk + simge kuralı bozulmuyor).
- **Bilgi kartı `v3_info_7`:** "Paletten hep en üstteki alınır. Katman sahaya inmez, yalnız şantiyeye gider." ✓

Rubrik öz puanı (ART_REFERENCE §10; Bölüm 7 ve 10 tahtası, palet ve bonus öğeleri; iki boyut): hacim/ışık 4 · renk/kontrast
4 · tipografi 4 ("+2" konturu köşeli) · düzen/boşluk 4 (rozet taşması) · ikon/rozet 4 · karakter/sahne — (değişmedi).
Kabul ≥ 4 ✓. Referans ekranlarla yan yana karşılaştırma yapılamadı (NOT ASSESSED).

Eksik: Bölüm 7 giriş nabzı (ART §3A.6, STORY §6A: 2 × 400 ms, ölçek 1,06) çizilmedi; web sunumunda D-108 yok (CL-V3C2-08).

## 9. CI, Pages, sahiplik

- Web CI son dört commit'te yeşil; `levels-nocache` işi gerçek koşuyor (C2: 15,5 dk, 0 hata, 0 uyarı). A2'nin iki belge
  commit'i `i18n.test.ts` ("every STORY 6 / 6A / 7 row … is in tr and en") yüzünden kırmızı; A1 gelene kadar ≈ 45 dk
  `wip/phase-2r` kırmızı kaldı (CL-V3C2-12).
- Unity CI `55bc84a` yeşil, Pages dağıtımı `55bc84a` = son kod commit'i (güncel). Unity CI `dotnet test` ve DiffFuzz
  koşturmuyor (CL-V3C2-15). P4'ten beri gerçek oyuncuda bonus anında donma (C1 kök nedeni) bu sürümle Pages'ten kalktı;
  konsolda istisna 0.
- Unity verisi web ile eşit: `Resources` ve `StreamingAssets` bölümleri 1–10, golden'lar, `i18n/tr.json`, `en.json`
  birebir aynı.
- Sahiplik: B1 (product-lead) code-lead'in `tests/tools/packageF.test.ts` dosyasına iki yeni `[]` beklentisi (test mantığı)
  ve `tests/level/shipped.test.ts`'e yeni test adları/beklentileri ekledi; REVIEW_LOG'da açıkça yazıldı, C2 packageF testini
  kendi biçimiyle yeniden yazdı (CL-V3C2-11). C2'nin `docs/LEVELS.md` işaret satırı değişikliği B1'in yazılı iznine dayanıyor
  (ihlal değil). A2 yalnız kendi dosyalarına yazdı. DECISIONS.md'de D-106…D-108 henüz yok (Z paketinin işi; bulgu değil).

## 10. Önceki bulguların durumu (REVIEW_LEVELS_V3C, CL-V3C-01…17)

| Kimlik | Önem | Durum | Kanıt (bu inceleme) |
| --- | --- | --- | --- |
| CL-V3C-01 final zorluğu / bantlar | Önemli | **Kapandı** | Bantlar resmî vekille ve bağımsız tohumlarla tutuyor (§4); final 0,56. Kalıntı: Bölüm 6 alt kenarda (CL-V3C2-06) |
| CL-V3C-02 vekil bonusu | Önemli | Kapandı (F) | `lastRawCost`; Bölüm 7 vekili bonusla ölçülüyor |
| CL-V3C-03 Bölüm 4–5 tuzak | Önemli | **Kapandı** (tuzak) | `plan_trap` 0, Kolay'da error kapısı, `NARROW_PLAN_TRAP_OPEN` boş (iki port). Yeni sorun: zorluk eğrisi (CL-V3C2-03) |
| CL-V3C-04 blok sayısı | Önemli | **Kapandı** | 17 / 20 / 19 / 19 / 20 = 95, D-107 alt sınırları ✓. Yeni sorun: çeşitlilik (CL-V3C2-04) |
| CL-V3C-05 doğrulayıcı açıkları | Önemli | Kapandı (F) | `siteColsRange(6…10)` = [3, 3]; `pallet_stuck` D-106 ile emekli; Y12 muaf |
| CL-V3C-06 palet tanıtımı + öğretici anahtarları | Önemli | **Açık** | Anahtarlar ve iki adımlı akış var; ama adım 1 oyunların ≈ %96'sında örtülü paleti gösteriyor (CL-V3C2-01), iki EN metni > 6 kelime (CL-V3C2-05) |
| CL-V3C-07 önbellek kapısı | Önemli | Kapandı | CI `levels-nocache` gerçek koşuyor; kendi önbelleksiz koşum yeşil. Kalıntı: commit'teki önbellek anahtarı eski (CL-V3C2-07) |
| CL-V3C-08 bonus patlaması görüntüsü | Önemli | **Kapandı** | `v3_bonus_burst_7` iki boyutta; kök neden (HUD hedefi) düzeltildi, konsol istisnası 0 |
| CL-V3C-09 LEVELS §2 / atlanan testler | Önemli | **Kapandı** | web 2317 geçti / **0 atlandı**; `data.review` işareti etkin. Kalıntı: belge tutarlılığı (CL-V3C2-10) |
| CL-V3C-10 palet görünümü | Önemli | **Kapandı** (Unity) | ×n beyaz daire, gri-mavi metal kızak, ölçüler §8. Kalıntı: giriş nabzı, web sunumu (CL-V3C2-08) |
| CL-V3C-11 fuzz boşlukları | Öneri | **Kapandı** | §3 sayıları kendi koşumda birebir. Kalıntı: Söküm 0 (CL-V3C2-09) |
| CL-V3C-12 rozet boyu | Öneri | **Kapandı** (sınırda) | ×n 69–72 design px; makas/kaygan 55–57 (CL-V3C2-14) |
| CL-V3C-13 örneklem | Öneri | **Kapandı** | 400 oyun + Wilson raporda; kapı hâlâ tek tohumun nokta tahmininde (CL-V3C2-06) |
| CL-V3C-14 GDD örnekleri | Öneri | **Kapandı** | K-58 örneği (sayaç 16 → 15, (7,0)) ve K-62 örneği (20. hamle, 6 − 1 + 2 = 7) golden ile ve `v3_bonus_burst_7` ile aynı |
| CL-V3C-15 vurgu deseni [0-8] | Öneri | **Kapandı** | web şema + C# `LevelSchemaPatterns`, `done.at` / `hand.path` x 0–8 |
| CL-V3C-16 sahiplik | Öneri | **Tekrarladı** | B1 yine code-lead test dosyasında mantık değiştirdi (CL-V3C2-11) |
| CL-V3C-17 WebGL boyutu / 3B çözme | Öneri | **Açık** | `55bc84a` ci-shots uyarısı: 3B sayfa çözme 278 / 261 ms > 250 ms (CL-V3C2-13) |

## 11. Bulgular

### Engel

Yok. Bağlayıcı kararlar (D-103…D-108, K-55, tam örtü, YAO ≥ %60, D-090 kapıları, K-53 ≤ 2 adım, `min` ≤ 22, bütçe =
min + pay, renk + simge) karşılanıyor; kapılar yeşil.

### Önemli

**CL-V3C2-01 · Bölüm 7 öğretici adım 1 oyunların ≈ %96'sında tutulamayan katmanı gösteriyor** — owner: product-lead ·
yer: `levels/level_007.json` `tutorial` (adım 1 `startOn segmentDone`, `piece:16`, `done overWall`), LEVELS §8.15.6 madde 1.
Kanıt: §6. 1. katın R/W talebini paletin üstündeki `D2_0` R (0,2) ve `D2_0` W (1,2) dışında 10, 12, 17 (R) ve 6, 7 (W)
de karşılıyor; yerleştiren vekil 1. katı bitirdiğinde palet üst katmanı yalnız %3,9 (ε 0) / %4,6 (ε 0,2) oyunda
tutulabiliyor. §8.15.6'daki "vurgu 1 hamle bile tutulamayan bir bloğu göstermez" yalnız kanonik çözüm için doğru.
Düzeltme seçenekleri: (A) code-lead öğretici sözlüğüne parça süzgeçli bir başlatma koşulu ekler (`startOn: { event:
'pieceFree', piece }`: sahne her hamle sonunda `holdableIgnoringMoves` sorar; ≈ 20 satır TS + C# eşi, test "K-53 …"),
product-lead adım 1'i ona bağlar; veri ve golden değişmez. (B) product-lead 1. katın R/W talebini paletin üstündeki
bloklara bağlayacak şekilde 10/17/6'yı yeniden boyar (golden ve vekil yeniden ölçülür). **Seçimim A** (ucuz, veriden
bağımsız, Hikaye 2 tanıtımlarında da işe yarar; B1'in `hold { piece }` önerisiyle aynı aile). Kabul: §6 senaryosunda
adım 1 başladığında piece 16 tutulabilir oranı ≥ %95.

**CL-V3C2-02 · Bölüm 7 bonusu oyuncuların %81'inde 1. katta, %92'sinde paletten önce patlıyor** — owner: product-lead ·
yer: `levels/level_007.json` piece 12 (`D2_0` R (5,2), `bonus`), D-107 ("bonus palet tanıtımının ödülü"), LEVELS §8.15.1
madde 3. Kanıt: §6 (2 000 oyun, ε 0,1): bonus 1 929 oyunda yerleşiyor, 1 569'unda 1. katta, 1 783'ünde ilk palet
katmanından önce. Bonus bloğu erişilebilir bir R dikey domino; 1. kat üç R dikey yuva istiyor. Kanonik çözüm onu son
kata (20. hamle) bıraktığı için tasarım bunu görmedi. Düzeltme: product-lead bonusu 1. katta yuvası olmayan bir bloğa
taşır (1. kat R dikey domino istediği için bonus R `D2_0` kalamaz; ör. yalnız 2–3. katta yuvası olan bir renk/şekil ya da
2. katın kazısının altındaki bir blok) ve aynı senaryoyla "ilk palet katmanından sonra" oranını ≥ %80'e çıkarır; ya da orkestratör D-107 gerekçesini "Bölüm 7'de herhangi bir anda" diye
yumuşatır. Seçimim veri düzeltmesi (sahibin isteği "ödül anı"; paletten önce gelen "+2" tanıtımı dağıtıyor).

**CL-V3C2-03 · Zorluk eğrisi: Kolay 3–4 aynı vekilde Normal 6–9'dan ve Zor finalden zor** — owner: product-lead ·
yer: `levels/level_003…005.json` (`targets.policyWin` yok), LEVELS §1 eğri tablosu, §8.15.6 madde 3. Kanıt: §5. ε 0,1:
3 0,51, 4 **0,43**, 5 0,56 · 6–9 0,81–0,86 · 10 0,56. Hatasız (ε 0): 3 0,59, 4 0,54 · 10 0,93. Bölüm 4'te bütçe + 8 ile
bile 0,59. Sahibin "abartısız zorluk" amacı ve "Kolay" etiketi resmî ölçüyle kanıtlanamıyor; eğri 3–5'te tepe yapıp 6'da
düşüyor. Düzeltme: (1) code-lead vekile "2 adım kazı" ileri bakışı (gereken bloğun üstündeki bloğu kaldıran kaydırmayı
tercih) ekleyip 3–5 ve 6–10'u yeniden ölçer (≈ 40 satır; vekilin derin kazıda kör olup olmadığını ayırır); (2) sonuca göre
product-lead 3–5'e `targets.policyWin` bandı koyar (öneri Kolay ≥ 0,85, Normal 0,80–0,90) ve Bölüm 4'ü tek derin kazıya
indirir (B1 önerisi; Bölüm 5 için `moves` + 4 → 0,85 yeter). "1–5 değişmez" notu nedeniyle karar orkestratör üzerinden
sahibe gider; bu turda en az karar kapanmalı.

**CL-V3C2-04 · 7–9'da şekil çeşitliliği geriledi** — owner: product-lead · yer: `levels/level_007…009.json`, LEVELS §8.15.1
madde 2. Kanıt: §4. 7–9'un paletsiz blokları yalnız D2, C3, I3 (v3c'de aynı bölümlerde 6 dörtlü blok: L4 ×5, T4 ×1);
8 ve 9'da 5 yönelim. Blok sayısı dörtlüler ikili/üçlülerle değiştirilerek arttı; sahibin amacı "daha çok blok, **daha
çeşitli şekil**". D-107 7–9 için ≥ 18 istiyor; 8 ve 9 19'da. Düzeltme: 8 ve 9'da birer L4/T4/J4 ile N 18–19 (D2 payı ≤ %50
kalır) ya da sahip bu takası açıkça kabul eder. Kabul: 7–9'un her birinde ≥ 1 paletsiz dörtlü blok, ≥ 4 aile.

**CL-V3C2-05 · İki EN öğretici metni 6 kelimeyi aşıyor; web'de EN sayım testi yok** — owner: design-lead · yer: STORY §6A
(`tut.m.pallet` EN 7, `tut.m.palletsite` EN 8; §6A notu "EN 5–6" diyor), web `src/i18n/en.json`, Unity
`unity_en.json` / `unity_tr.json` geçici üstyazısı, web `tests/review/services-theme.review.test.ts` (TR ≤ 8, EN yok).
Kanıt: §7; STORY §0-4 "TR ve EN ≤ 6 kelime". Düzeltme: design-lead STORY §6A EN satırlarını ≤ 6 kelimeye çeker (Unity'deki
"Always take the pallet's top layer." / "Lift a layer onto the site." uygun); code-lead `en.json`'u eşitler, testi "K-53
STORY 0-4 tutorial texts TR and EN ≤ 6 words" diye sevkiyattaki her `tutorial[].textKey` için iki dile genişletir, Unity
üstyazısındaki iki anahtarı siler.

### Öneri

**CL-V3C2-06 · Bölüm 6 bant kenarında; kapı tek tohumun nokta tahmini** — owner: code-lead · yer: `tools/solver/solveLevel.ts`
(`seed: level.id`, 400 oyun), `level_006.json`. Kanıt: §4: bağımsız 1 200 oyunda 0,810 [0,787–0,831], bir tohumda 0,78.
Çekirdek ya da vekilde RNG tüketimini değiştiren her değişiklik uyarıyı açabilir. Düzeltme: bant kapısını 1 200 oyuna
çıkarmak (Bölüm 9 en uzun: ≈ 80 sn) ya da "Wilson aralığı bantla kesişmiyorsa uyarı" kuralı; product-lead isterse Bölüm 6
`moves` 24 (0,88).

**CL-V3C2-07 · Önbellek anahtarı eski** — owner: code-lead · yer: web `artifacts/solver/*.json` (`srcf6518d28`; güncel
`src0afd9b08`). Önbellekli `levels:check` her bölümü baştan çözüyor (≈ 13 dk). Kendi koşum: fark yalnız anahtar ve süre.
Düzeltme: bir sonraki web commit'inde dosyaları tazelemek.

**CL-V3C2-08 · D-108 eksikleri: Bölüm 7 giriş nabzı ve web sunumu** — owner: code-lead · yer: Unity sunum (nabız yok),
web `src/theme/draw/blockV2.ts` (palet çizimi yok). Düzeltme: Unity'de 2 × 400 ms, ölçek 1,06 nabız + sunum testi; web
sunumu sevkiyat hedefiyse D-108 portu, değilse TECH_DESIGN'da "web sunumu v3c paletini çizmez" notu.

**CL-V3C2-09 · Fuzz: D-106 çıkışından sonra Söküm hiç yok** — owner: code-lead · yer: Unity `tools/diff/ts-oracle.mjs`.
Geri Al 16, Vinç 16, Mala 28 kez kapsanıyor; Söküm 0. Düzeltme: betikli senaryo (palet katmanı yerleşimi → D3a → Söküm)
+ kapsam kapısı ≥ 3.

**CL-V3C2-10 · LEVELS belge tutarlılığı** — owner: product-lead · yer: LEVELS §8.15.5 ("aha" tablosu yalnız başlık, satır
yok); §2 kartlarında "Saha" satırı F = Σ (54) yazıyor, GDD K-58 madde 5 "F ayak izidir" diyor (C2 testi karta uydurdu);
§8.3 ve §8.9 hâlâ "katman çentikleri, rakam yok" diyor. Düzeltme: "aha" satırları; F tanımı tek (K-58 madde 5) ve kartta
"F = 42 (ayak izi), Σ = 54"; §8.3/§8.9'a "D-108 ile geçersiz" notu (code-lead testi buna göre çevirir).

**CL-V3C2-11 · Sahiplik: product-lead code-lead test mantığını değiştirdi (CL-V3C-16 tekrarı)** — owner: product-lead ·
yer: web `b92120a` `tests/tools/packageF.test.ts` (iki yeni `[]` beklentisi), `tests/level/shipped.test.ts` (test adları,
beklentiler). Açıkça yazıldı ve C2 yeniden yazdı; ama kural "veri/golden tazeleme serbest, mantık REVIEW_LOG'a". Düzeltme:
yeni beklentiler REVIEW_LOG'a yazılır, code-lead ekler.

**CL-V3C2-12 · Belge önce itildi, web CI 45 dk kırmızı** — owner: design-lead · yer: web `e75023b`, `12715f2`
(`i18n.test.ts` STORY §6A satırlarını i18n'de arıyor). Düzeltme: yeni anahtar satırları i18n ile aynı turda (ya da
`wip/<etiket>` dalında) itmek; isterse code-lead testine "i18n bekliyor" işareti.

**CL-V3C2-13 · 3B sayfa çözme bütçe üstünde (CL-V3C-17 sürüyor)** — owner: code-lead · yer: Unity ci-shots `55bc84a`
uyarısı (278 / 261 ms > 250 ms, yazılım GL). Düzeltme: sonraki sanat eklemesinden önce sayfa birleştirme / sıkıştırma ölçümü.

**CL-V3C2-14 · Rozetlerde pay yok; ×n rozeti komşuya taşıyor; "+2" konturu köşeli** — owner: code-lead · yer: Unity
`BlockSkin` (`BadgeMinPx` 66), "+n" patlama yazısı. Kanıt: §8 (makas/kaygan 55–57 design px; Bölüm 10'da ×4 sol komşunun
ağına biniyor). Düzeltme: görünen çapı ≥ 60'a çıkaracak +4 design px; ×n rozetini kendi ayak izinin içinde tutmak; "+n"
konturunu yumuşatmak (SDF kontur yumuşaklığı).

**CL-V3C2-15 · Unity CI C# eşitliğini denetlemiyor** — owner: code-lead · yer: Unity `.github/workflows/unity-webgl.yml`
(yalnız GameCI derleme, ci-shots, Pages). `dotnet build -warnaserror`, `dotnet test` ve DiffFuzz yalnız ajan koşularında.
Düzeltme: web reposunu sabit bir commit'te çekip `ts-oracle.mjs` + `dotnet test` koşturan ≈ 3 dk'lık bir iş (`Assets/**`
ve `tools/diff/**` değişince).

## 12. Sonuç ve açık sorular

Kapılar (web `check`, önbelleksiz `levels:check`, `build`; Unity build/test, DiffFuzz, stub kapısı, CI, Pages) **yeşil**.
D-106 kural ↔ TS ↔ C# ↔ test zinciri kenar durumlarıyla kanıtlı; D-107 sayıları (blok, domino, `plan_trap` 0, bonus
Bölüm 7, bantlar) tutuyor; D-108 Unity'de ölçülerle doğrulandı; `v3_bonus_burst_7` var. Bu turda kapanması gerekenler:
Bölüm 7'nin öğretim sırası (01) ve bonus anı (02), zorluk eğrisi kararı (03), 7–9 şekil çeşitliliği (04), EN metinleri (05).

Açık sorular (orkestratöre):

1. A1'in sorusu: D-106 yan kaymayı da serbest bırakıyor (Bölüm 10 v3c `min` 22 → 21; "D2_90 paletli sonuçlar değişmemeli"
   beklentisi bu yüzden tutmadı). GDD K-58 madde 4 yan kaymayı KABUL metnine yazdı; Z paketi D-106 kaydına aynısını yazmalı.
2. CL-V3C2-03: "Bölüm 1–5 değişmez" notu Bölüm 3–5'in zorluk düzeltmesini (en az Bölüm 4 yerleşimi ve 3–5 bütçeleri)
   kapsıyor mu?
3. CL-V3C2-04: 8 ve 9'da 19 blok mu, 18 blok + dörtlü şekil mi?

## 13. Paket Fc kapanışları (code-lead, 2026-11-12)

Web `wip/phase-2r` `0ba4d9c` + Fc değişiklikleri (`SOLVER_VERSION` 8) ile eşitlendi: `tools/sync-from-web.sh` (Bölüm 7–9 verisi,
golden'lar, `en.json` Resources'a), `tools/gen-p0…p3-vectors.ts` ve `tools/review/gen-follow-vectors.ts` yeniden üretildi
(web `wt` aynı commit). Unity bu turdan önce Bölüm 7–9'un eski (v3c2) verisini taşıyordu; DiffFuzz ve vektör testleri 42 yerde
kırmızıydı, eşitlemeden sonra yeşil (aşağıda).

| Kimlik | Önem | Durum | Kanıt |
| --- | --- | --- | --- |
| CL-V3C2-05 EN öğretici metinleri > 6 kelime | Önemli | **Kapandı** | web `en.json` ve STORY §6A: "Always take the pallet's top layer." (6) / "Lift a layer onto the site." (6); Unity `unity_en.json` / `unity_tr.json` üstyazıları (`tut.m.pallet`, `tut.m.palletsite`) silindi, `K53_every_shipped_tutorial_text_…_at_most_six_words` web kopyasıyla yeşil; web testi `services-theme.review.test.ts` "K-53 STORY 0-4 CL-V3C2-05" TR + EN ≤ 6 (sevkiyattaki her `textKey`) |
| Fl (i) resmî vekile 2 adım kazı bakışı | İstek | **Yapıldı (web)** | `walks.ts` `lookDepth` 2 (varsayılan), `SOLVER_VERSION` 8; 3 0,93 · 4 0,89 · 5 0,97; 6–10 0,85 / 0,88 / 0,89 / 0,86 / 0,51 (bantlar içinde); test "K-51 item 3 CL-V3C2-03 two dig steps" |
| Fl (ii) `startOn: { event: 'pieceFree', piece }` | İstek | **Yapıldı (TS + C#)** | C#: `TutEvents.PieceFree`, `LevelJson.TutCond(startOn)`, `Logic.ConditionImpossible`, `TutorialEvents.PieceFreeNow`, `TutorialController.Refresh`; testler `K53_pieceFree_*` (3), web "K-53 pieceFree …" (4); veri bağlama product-lead'de |
| Unity eşitlemesi (Fl bağımlılığı) | — | **Yapıldı** | `dotnet build -warnaserror` 0 uyarı; `dotnet test` Core.Tests 1 298 geçti / 8 atlandı, Presentation.Core.Tests 612 geçti; `tools/stub-gate-selftest.sh` 4/4; `v3_bonus_burst_7` senaryosu veriden okuyor (19. hamle, `piece:16`) ve `C1_CL_V3C_08_K60_item_7_…` yeşil |
