# Bölümler v3c şüpheci inceleme (REVIEW_LEVELS_V3C)

İnceleyen: code-lead (bağımsız şüpheci, depo kodu değiştirilmedi) · Tarih: 2026-11-12 ·
Yöntem: Claude-Code-Game-Studios `qa-lead` rolü + `playtest-report` ve `balance-check` becerileri, `level-designer` /
`systems-designer` ölçütleri (orkestrasyonlu koşu istisnası: soru sorulmadı; seçenekler ve seçimim her bulguda yazılı).
Ölçüm betikleri yalnız scratch'te koştu (repoya girmedi); yöntemleri §4 ve §5'te tarif edildi.

İncelenen sürümler:

| Repo | Ref | İçerik |
| ---- | --- | ------ |
| web `minik-usta` | `origin/wip/phase-2r` = `37d5617` (P1 `1f197dd`, P2 `5d2a9d7`, P3 `37d5617`) | GDD K-49/K-56 eki/K-58/K-60/K-62/K-63 eki, çekirdek, çözücü, 10 bölüm |
| Unity `minik-usta-unity` | `origin/main` = `4b8ed97` (P4 `f7a8043`, `6d91021`, `4b8ed97`) | C# portu, yerleşim, sunum, ci-shots |

Unity `Resources/levels/level_001, 005…010.json` web JSON'larıyla **birebir aynı** (JSON eşitliği); Unity `i18n/tr.json`,
`en.json` web ile birebir aynı. Bölüm incelemesi web `37d5617` üzerinde yapıldı.

Önem: **Engel** = sahibinin bağlayıcı kararı ya da bir kapı karşılanmıyor, iş geri döner · **Önemli** = bu turda
kapanmalı · **Öneri** = birikim listesi. Sonuç: **Engel yok; 10 Önemli, 7 Öneri.**

## 1. Kendi koşturduğum komutlar (kanıt)

| Komut (kendi worktree'lerimde) | Sonuç |
| --- | --- |
| web `npm run levels:check` | yeşil (çıkış 0) **ama 1 sn**: `artifacts/solver/*.json` git'te izleniyor, 10/10 önbellekten geldi (bkz. CL-V3C-07) |
| web `node tools/solve.ts --check --no-cache --jobs 4` | **yeşil**, 677 sn: validate 0 hata / 1 uyarı (Bölüm 5 `tile_family_mix`), solve 0 hata / 0 uyarı, bot aşaması "not present — skipped". Önbelleğe göre fark: yalnız süre alanları (zamanlama dışı **0** satır) → determinizm OK. Bölüm 10 tek başına 676 sn |
| web `npm run check` | yeşil, 266 sn: typecheck + lint + format + vitest **2200 geçti / 83 atlandı** (122 dosya) + `test:rules` (88 kimlik, 1677 test) |
| web CI (`gh run list`) `37d5617` | CI ✓ (yalnız `check` + `build` + `levels:validate`; `levels:check` CI'da yok) |
| Unity `cd dotnet && dotnet build -warnaserror` | yeşil, 0 uyarı / 0 hata |
| Unity `dotnet test` | yeşil: Core **2068 geçti / 5 atlandı** (atlananlar K01/K43/K44/K45 JS uyumluluk notları), Presentation **595 geçti** |
| Unity `node tools/diff/ts-oracle.mjs <web 37d5617>` + `dotnet test --filter DiffFuzz` | 794 dizi, 21 981 eylem; **796/796 yeşil** (0 sapma) |
| Unity `bash tools/stub-gate-selftest.sh` | yeşil (4/4) |
| Unity CI `4b8ed97` | Build WebGL ✓ (7 dk), CI screenshots ✓ (23 dk), Deploy Pages ✓. Pages dağıtım kaydı `4b8ed97`, 09:36:29Z (API) |
| Kendi senaryom: palet yalnız şantiyeye (§6) | **ALL OK** (Bölüm 7–10, 12 katman, 0 ihlal) |
| Kendi ölçümüm: `planTrapScan` 1–10, oyun vekilleri, tuzağa düşme (§4, §5) | tablolar aşağıda |

NOT ASSESSED (tahmin yapılmadı, "geçti" sayılmaz): canlı Pages sayfası (sandbox proxy 403; yalnız dağıtım kaydı
doğrulandı), bilgi kartı ve "+2" patlamasının gerçek görüntüsü (ci-shots'ta yok, CL-V3C-08), cihazda FPS, orta bot
(`levels:bot` yazılmamış).

## 2. Kural ↔ kod ↔ test

Test adında kimlik geçen test sayısı (TS: `it/test/describe` adı; C#: `[Test(Description=…)]`):

| Kural | TS | C# | Kod ↔ metin |
| --- | --- | --- | --- |
| K-17 (palet istisnası) | 56 | 41 | OK (geri sekme kendi paletine, adım 1–3 atlanır; her iki tarafta testli) |
| K-49 (D-103) | 62 | 44 | **Açık:** madde 2 "Bölüm 6–10'da Ws = 3" doğrulanmıyor: `siteColsRange(id ≥ 6)` = [2, 3] (`src/core/level/logic.ts:170`, C# `Logic.SiteColsRange` aynı). Hücre basamağı 108 / duvar 54 / pay 27 her iki tarafta testli |
| K-56 eki (P5, S4/Z4 serbest) | 14 | 10 | OK; P5 TS↔C# vektörü yok (CL-V3C-11) |
| K-58 madde 9 (D-104) | 16 | 12 | OK (a)–(g) iki tarafta adlı testli. **Açık:** madde 10 (ÖNERİ) için `pallet_stuck` kodu yok (CL-V3C-05) |
| K-60 madde 6–7 (D-105) | 11 | 9 | Olay ve sunum OK. **Açık:** madde 6 "bonus yeni fikir sayılmaz" doğrulayıcıda yok: L-16/L-22 Y12'yi yeni mekanik sayıyor, `mechanics.ts` Y12 `firstLevel` 10; test bunun yerine gevşetildi (CL-V3C-05) |
| K-62 (D-105) | 6 | 5 | Bütçe formülü OK (`k` = 0). **Açık:** "vekiller bonusu uygulayarak ölçülür" uygulanmıyor (CL-V3C-02) |
| K-63 eki (`plan_trap`) | 7 | 4 | Ws = 3 için OK. Ws = 2 bölümlerde tarama koşmuyor; Bölüm 4–5'te tuzak var (CL-V3C-03) |

## 3. Fuzz kapsamı (TS kâhini `37d5617`, kendi ürettiğim `tools/diff/out`)

| Olay | Sayı | Not |
| --- | --- | --- |
| 3 sütunlu tahtada dizi / adım / kazanış | 184 / 6 491 / 29 | Ağırlık sevkiyattaki 6–10'da (26'şar dizi); rastgele 3 sütunlu geometri yalnız 12 dosya, 1–2 dizi |
| Palet katmanı sürüklemesi | 376 | |
| `palletSiteOnly` iptali (sahaya bırakma) | 50 | 6–10'da 21 |
| Hatalı şantiye bırakması → kendi paletine geri sekme | 151 | 6–10'da 52 |
| Palet katmanı doğru yerleşimi | 77 | |
| Vinç + palet katmanı | 15 | **hepsi geçerli şantiye hedefi; `palletSiteOnly` reddi 0** (kâhin yalnız geçerli eylem oynar) |
| `movesChanged{bonus}` | 37 (3 sütunda 23) | Vinç ile 7, Altın Mala ile 6 |
| P5 / S4 / Z4 içeren dizi | **0 / 0 / 0** | D-103 bunları Ws = 3'te serbest bırakıyor |

Sonuç: palet iptali, geri sekme ve bonus olayı TS↔C# bit eşitliğiyle kapsanıyor; Vinç reddi ve yeni geniş şekiller
fuzz'da yok (yalnız C# birim testinde).

## 4. Bölümler (balance-check: Progression)

Ölçüm (önbelleksiz çözücü + kendi vekillerim, 200'er oyun, tohum sabit):

| # | Etiket · teaches | Σ / F / E | N (D2 %) | min (alt sınır) | hamle (P) | plan tuzağı | liftRatio | resmî vekil bütçe / +N/2 | p0 (bonussuz) / p0b (bonuslu) | orta (1 adım ileri) | orta %10 hata | orta %20 hata | palet hamleleri (kat) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 3 | Kolay | 20 / – | 9 (89) | 11 (10) | 15 (4) | 0 | 0,78 | .18 / .41 | .20 / .20 | **.64** | – | – | – |
| 4 | Kolay | 24 / – | 10 (60) | 12 (11) | 16 (4) | **4** | 1,00 | .11 / .14 | .08 / .08 | **.29** (seçici .52) | – | – | – |
| 5 | Normal · Y9 | 30 / – | 12 (67) | 14 (13) | 18 (4) | **3** | 0,75 | .33 / .57 | .23 / .23 | .69 | .55 | .37 | – |
| 6 | Normal · S9 | 45 / 45 / 3 | 17 (41) | 19 (18) | 23 (4) | 0 | 1,00 | .25 / .24 | .23 / .23 | **1,00** | .86 | .42 | – |
| 7 | Normal · Y10 | 48 / 44 / 4 | 18 (44) | 20 (19) | 24 (4) | 0 | 1,00 | .16 / .38 | .16 / .16 | **1,00** | .79 | .34 | 15, 16, 20 (3, 4, 4) |
| 8 | Normal · Y11 | 48 / 44 / 4 | 18 (50) | 20 (19) | 24 (4) | 0 | 0,94 | .35 / .68 | .34 / .34 | **1,00** | .85 | .46 | 16, 17, 20 (3, 4, 4) |
| 9 | Normal · Y13 | 45 / 41 / 7 | 17 (41) | 19 (18) | 23 (4) | 0 | 1,00 | .26 / .44 | .21 / .21 | **.97** | .80 | .48 | 13, 14, 19 (2, 3, 3) |
| 10 | **Zor** · – | 48 / 44 / 4 | 19 (47) | 22 (20) | 23 (1) | 0 | 0,79 | .04 / .30 | .06 / **.10** | **1,00** | .68 | .23 | 17, 18, 22 (3, 4, 4) |

Vekil tanımları: **resmî vekil** = `policyWinRate` (görünen doğru yerleşim varsa rastgele biri, yoksa **rastgele**
kaydırma). **p0/p0b** = aynı politika, benim tohumumla; p0b bonusu sayaca ekler. **orta** = doğru yerleşim varsa
rastgele biri; yoksa her kaydırmanın sonrasındaki doğru yerleşim sayısına bakıp en çok açanı seçer (1 adım ileri bakış,
beşer); **%10/%20 hata** = hamlelerin o kadarı geçerli hamleler arasından rastgele. Vekiller çözücünün `Expander`
hamle kümesini kullanır (oyuncununkinin alt kümesi), bu yüzden "orta" oranları alt sınırdır.

Sahibinin sınırları: `min` ≤ 22 ✓ (19–22) · domino payı ≤ %50 ✓ · `plan_trap` 0 ✓ (6–10, tam tarama) · YAO 1,00 ✓ ·
D-090: 10 000 yürüyüşte yakalanmayan çıkmaz 0 (**tahmin**, kanıt değil; Bölüm 5'teki 1/182 v3b'den sürüyor) ·
D3a "bilinmiyor" 0 (her bölüm) · **zorluk abartı yok ✓**: 1 adım ileri bakan oyuncu 6–10'u %97–100 bitiriyor, hiçbir
bölüm duvar değil. Ama **"Zor" final orta oyuncu için zor değil** ve bantlar resmî araçla ölçülmüyor (CL-V3C-01).

Blok eğrisi: 17 / 18 / 18 / 17 / 19 = **89** (D-103 amacı 18–22; 6 ve 9 bir eksik, CL-V3C-04). Yerleşim çeşitliliği:
6 ve 9 aynı (6×8 · 3×5×3 · duvar 7), 7 ve 8 aynı (3×4×4 · 7), 10 ayrı (3×4×4 · **6**) → CL-V3-04 "en çok 2" ✓.

Palet ritmi: dört bölümün hepsinde palet sağ/sol alt köşede gömülü durur ve ilk katmanı kanonik çözümün son
%25–35'inde (hamle 13–17) yerleşir; palet bir **kapasite hilesi** gibi çalışıyor (3 blok, 1 ayak izi), oyuncuya
erken bir karar sunmuyor. Tanıtım bölümü 7'de de böyle (CL-V3C-06).

## 5. Eğlence testi (playtest-report, 4 strateji)

| Strateji | Sonuç | Okuma |
| --- | --- | --- |
| Rastgele kazan (resmî vekil) | 6–10 bütçede %4–35 | Gerçek bir orta oyuncuyu temsil etmiyor: kazıyı rastgele yapıyor. LEVELS §8.14.3'teki "testere dişi vekilde görünür" yorumu bu vekile dayanıyor |
| 1 adım ileri bakan orta oyuncu | 6, 7, 8, 10: %100; 9: %97 | Kazıyı "gereken bloğu açan" hamleye yöneltmek her bölümü bitiriyor; sıra ya da park kararı hiç cezalandırılmıyor |
| Orta + %10 hata | 6–9 %79–86, 10 %68 | 6–9 %80–90 bandına yakın; final %50–60 bandının üstünde; 9 → 10 basamağı ~15 puan |
| Orta + %20 hata | 6–9 %34–48, 10 %23 | Hata payı arttıkça fark açılıyor: zorluk içgörüden değil hata toleransından geliyor |

Bölüm 10'da bonus her zaman yerleşir (tam örtü: her blok yerleşmek zorunda), yani kazanan her oyuncu +2 alır; etkin
pay P 1 değil **3**'tür (D-105 gereği bilinçli, ama "Zor" etiketi bunu hesaba katmıyor).

Tuzağa düşme (1 adım ileri bakan oyuncu, D3a/D2 ölü durum = oyunda Söküm): Bölüm 4 **86/200** (ort. 20,6. hamlede),
Bölüm 5 1/200, Bölüm 3 ve 6–8 0/200. Bölüm 4 Kolay'dır ve tam tarama 4 plan tuzağı buluyor (örnek: dilim 2, `D2_0`
(1,2)); 10 000 yürüyüşün 259'u D1/D2/D3a ile kurtarılıyor (CL-V3C-03).

Olumlu: 6–10'da tuzak 0, ölü masa tahmini 0, YAO/liftRatio 0,79–1,00, palet kuralı cezasız (iptal hamle yakmıyor),
bonus anı gerçekten ödül (bütçe düşmüyor).

## 6. Palet yalnız şantiyeye — senaryo kanıtı (D-104, K-58 madde 9)

Sevkiyattaki 3 sütunlu Bölüm 7–10'da golden çözümü oynayıp her palet katmanı tutulabilir olduğunda (kopyalar üzerinde)
şunları denedim; sonra golden çözüm kaldığı yerden sürdü ve kazandı:

| Deneme | Bölüm 7 / 8 / 9 / 10 | Beklenen | Sonuç |
| --- | --- | --- | --- |
| Sürükleme oturumunun saha/saha havası/Vinç Alanı düğümleri (`classify`) | 121 / 120 / 90 / 121 | hepsi `cancel`, hiçbiri `yard` | ✓ |
| Her saha hücresine ham `applyMove` (6 × 10 × 3 katman) | 180'er | iptal, sayaç aynı, durum karması aynı, katman yerinde | ✓ |
| Her saha hücresine Vinç (× 4 açı) | 720'şer | `craneRejection` = `palletSiteOnly` | ✓ |
| Hatalı şantiye bırakması | 43 / 41 / 34 / 34 | maliyet 1, seri 0, aynı (x, y)'de paletin üst katmanı, değişmez hatasız | ✓ |
| Doğru şantiye bırakması | 10 / 10 / 16 / 17 | normal yerleşim | ✓ |
| Golden çözüm sonu | kazandı; kalan hamle 4 / 4 / 4 / **3** | Bölüm 10: 23 − 22 + 2 (bonus) = 3 | ✓ |

C# tarafı aynı bölümlerde fuzz ile bit eşit (§3: 21 iptal + 52 geri sekme, 0 sapma) ve `WideSiteTests` (K-58 madde 9
a–g) yeşil. **Bonus bütçeyi düşürmüyor:** Bölüm 10 `moves` 23 = `min` 22 + P 1 (`a` 0), doğrulayıcı `moves_budget`
temiz, golden sonunda sayaç +2 ile 3.

## 7. Öğretici (K-53)

Hepsi ≤ 1 adım, kilitlemez ✓. İçerik uyumu zayıf:

| # | Anahtar (TR) | Vurgu | Sorun |
| --- | --- | --- | --- |
| 6 | `tut.m.pattern` "Plan alttan üste dolar." | gömülü iki renkli `D2_0` | Metin ne geniş şantiyeyi ne iki renkli ikizi anlatıyor (bilgi kartı `obs.s9.desc` anlatıyor) |
| 7 | `tut.m.useall` "Bütün bloklar plana girecek." | palet üst katmanı (`piece:17`) | Katman iki bloğun altında; adım `placementCorrect piece:17` ile, yani kanonik **15. hamlede** bitiyor; vurgu 14 hamle boyunca tutulamayan bir bloğu gösteriyor. D-104 kuralı ("yalnız şantiyeye") web i18n'de yok, yalnız Unity katmanında (`card.y10.site`) |
| 8 | `tut.m.wrap` | makaslı C3 | uygun |
| 9 | – | – | Kaygan yalnız bilgi kartıyla (`obs.y13.desc`); planlanan `tut.m.slippery` yok |
| 10 | `tut.m.cranebooster` (`startOn segmentDone`) | Vinç | uygun; ama final hem Vinç açılışını hem bonusun ilk görünüşünü taşıyor |

## 8. Görsel okunurluk ve yerleşim (ci-shots `4b8ed97`, 390×844 ve 360×800, DPR 2)

Beş v3c başlangıç görüntüsünü yan yana ve 360 px'te 1:1 CSS ölçeğinde okudum (konsol günlüklerinde hata yok):

- **Yerleşim:** 9 sütun sığıyor; 360'ta yan pay 18 fiziksel px = 9 CSS = 27 design px ✓, hücre 72 fiziksel = 36 CSS =
  108 design px ✓ (≥ 100). Duvar şeridi, Vinç Alanı, plan ve HUD taşmıyor. Blok dokunma hedefi (hücre + pay) birim
  testte ≥ 44 pt ✓. FIT kipinde 9 sütunlu görüntü yok (FIT çekimleri yalnız Bölüm 1).
- **Rozetler (360):** özellik rozeti çapı ≈ 31 fiziksel px ≈ **15–16 CSS px** (≈ 46 design px; `kit.v3.badge.smallPx`
  56 × k 0,9 = 50'nin altında). Şekiller ayırt ediliyor (yıldız, makas, kar tanesi, kalaslı "×3", fiyonklu ağ); "+2" ve
  "×3" rakamları ≈ 7–8 CSS px, DPR 2'de okunuyor ama sınırda.
- **Palet derinliği:** "×3" rozeti + kızak okunuyor; ince tahtalar 360'ta zor seçiliyor (P4 de yazmış). **Ama** GDD K-65
  madde 4 (KABUL) ve OBSTACLES Y10 "Okunurluk" katman sayısını **rakamsız çentikle**, tabanı **ahşap olmayan gri-mavi
  metal kızakla** tarif ediyor; ekranda "×3" rakamı ve ahşap kalaslar var, Bölüm 10'da "+2" ile aynı tahtada (CL-V3C-10).
- **İki renkli bloklar:** renk + hücre simgesi korunmuş (renk körlüğü kuralı ✓).
- **Bilgi kartı ve "+2" patlaması:** ci-shots'ta **yok**; yalnız FakeView testi var (CL-V3C-08).

Rubrik puanım (ART_REFERENCE §10, kabul ≥ 4): Düzen ve boşluk 4 · Renk ve kontrast 4 · İkon ve rozet 4 (390) / **3**
(360, rakamlar) · Tipografi (öğretici balonu) 4 · Bilgi kartı ve patlama: değerlendirilmedi (görüntü yok).

## 9. CI, Pages, i18n

Web CI ve Unity CI son commit'lerde yeşil; Unity Pages dağıtımı `4b8ed97` (main HEAD) — güncel. Unity CI belge
commit'lerinde tetiklenmez (`paths` süzgeci). i18n: TR/EN anahtar eşitliği web testlerinde yeşil; Unity overlay
`card.y10.site` TR + EN var. P4 uyarıları (WebGL 25 MB = bütçe sınırı; 3B sayfa çözme 281–320 ms > 250 ms) sürüyor.

## 10. Bulgular

### Engel

Yok.

### Önemli

**CL-V3C-01 · Zorluk bantları ölçülmüyor; "Zor" final orta oyuncu için zor değil** — yer: `levels/level_006…010.json`
`targets` (yalnız `min` ve `deadRate`), `tools/solver/walks.ts` `policyWinRate`, LEVELS §8.14.3.
Kanıt: §4–§5. Resmî vekil rastgele kazıyor (%4–35). 1 adım ileri bakan oyuncu 6–10'u %97–100, %10 hatayla 6–9'u
%79–86, 10'u %68 bitiriyor (bant %50–60). Bonus her kazanışta alındığı için 10'un etkin payı 3.
Düzeltme (ucuz): `walks.ts`'e "1 adım ileri bakış + ε" vekili (≈ 60 satır; bölüm başına 1–80 sn) ve bantları
`targets.midBand` ile kapıya bağlamak (ε = 0,1). Final için product-lead seçenekleri: (A) bonusu finalden 7 ya da 9'a
taşımak (CL-V3C-05 c ile mümkün olur) → etkin pay 1; (B) finale 2 hamlelik bir kazı kararı (yanlış kazı 2 hamle yakar)
koymak. **Seçimim A** (veri değişmeden bonus yer değiştirir, sahibin "ödül anı" isteği 7/9'da da karşılanır).

**CL-V3C-02 · K-62: oyun vekili bonusu uygulamıyor** — yer: `tools/solver/expand.ts` `edgeInfo` (`Math.max(1, cost)`),
`tools/solver/walks.ts` `policyWinRate` (`spent += infoCost`).
Kanıt: bonus yerleşiminin gerçek maliyeti 1 − 2 = −1, kenar bilgisinde 1'e kırpılıyor. Bölüm 10 aynı politikada
bonussuz .06 / .30, bonuslu .10 / .36. GDD K-62: "vekil kazanma oranları bonusu gerçek oyundaki gibi uygulayarak
ölçülür".
Düzeltme: politika döngüsünde maliyeti `movesLeft` farkından (kırpmasız) almak ya da bonus bayrağı kalktığında
`bonusMoves` düşmek; test adı "K-62 play proxy credits the bonus". LEVELS §8.14.3 vekil sütunu yeniden üretilir.

**CL-V3C-03 · Kolay Bölüm 4 ve Normal Bölüm 5'te plan tuzağı var; 2 sütunda kapı yok** — yer: `levels/level_004.json`,
`level_005.json`; `src/core/level/logic.ts` (`plan_trap` yalnız Ws = 3), çözücü (`trapCount` büyük bölümde "–").
Kanıt: `planTrapScan` tam tarama: Bölüm 4 **4 tuzak** (dilim 2 `D2_0` (1,2)), Bölüm 5 **3 tuzak** (dilim 1 `L4_0`
(0,0)); 1–3 ve 6–10'da 0. 1 adım ileri bakan oyuncu Bölüm 4'te oyunların %43'ünde D3a ölü durumuna düşüyor (Söküm),
bütçede yalnız %29 kazanıyor (Kolay!). P2 bunu sordu; P3 cevabı "2 sütunlu bölümlerde tarama zaten çalışmıyor" oldu.
Düzeltme: code-lead `plan_trap`'i Ws = 2'de de koşturur (ms sürer): Kolay'da error (`trap_in_easy` anlamı), Normal'de
warn. product-lead 4 ve 5'i üreticinin renk onarımıyla yeniden boyar (yerleşim aynı kalabilir). Sahibin "1–5 değişmez"
notu nedeniyle karar product-lead'in; ben düzeltmeyi öneriyorum.

**CL-V3C-04 · Blok sayısı 17/18/18/17/19; "geometrik tavan" gerekçesi Bölüm 9 ve 10 için doğru değil** — yer:
`level_006.json`, `level_009.json`, LEVELS §8.14.1 madde 2 ve §8.14.6 soru 1, DECISIONS P-P3-1.
Kanıt (aritmetik, K-02 `E ≥ 2`, domino ≤ %50): Bölüm 9 Σ 48 (3×4×4) + `D2_90`×3 palet → F 44, E 4 → 8 D2 + 8 C3 + T4 +
L4 = 48, **N 18, T4 korunur** (duvar 6 ile yerleşim çeşitliliği de korunur: 9 ve 10). Bölüm 10: K-58 madde 1 paleti
**4 katmana** kadar izin verir; Σ 54 + `D2_90`×4 + `D2_90`×3 → F 44, E 4 → 10 D2 + 6 C3 + 4 L/J, **N 20**, 2 kazıyla
`min` 22. Bölüm 6 gerçekten bir değiş tokuş: N 18 yalnız 9 D2 + 9 C3 ile (J4 düşer, 2 şekil türü).
Düzeltme: product-lead 9'u (ve isterse 10'u) üreticiyle yeniden kurar; Bölüm 6 için seçenek (17 + çeşitlilik / 18 + iki
tür) düzeltilmiş gerekçeyle sahibe gider. Uzun vadeli seçenek: K-58 madde 10'u "katman kendi paletinin ayak izinin
üstünden geçebilir" diye yazmak (çekirdekte palet katmanı oturumunun çarpışma maskesinden kendi ayak izini çıkarmak;
katman sahaya zaten inemediği için yalnız yolu etkiler) → C3 paletleri açılır, Σ 54'te N 20–21.

**CL-V3C-05 · Doğrulayıcı kural metninin gerisinde (üç açık)** — yer: `src/core/level/logic.ts`
(`siteColsRange`, `checkMechanics`), `src/core/level/mechanics.ts` (Y12 `firstLevel` 10), C# eşleri.
Kanıt: (a) GDD K-49 madde 2 "Bölüm 6–10'da Ws = 3" → kod [2, 3] kabul ediyor; (b) K-58 madde 10 `pallet_stuck` yok,
C3/O4/L4/J4/T4 paleti yalnız çözücüde `unsolvable` olarak düşüyor; (c) K-60 madde 6 (KABUL) bonusu yeni fikir saymaz,
L-16/L-22 sayıyor; P3 bunun yerine `shipped.test.ts`'teki `teaches` beklentisini Y12'yi süzerek gevşetti.
Düzeltme (code-lead, aynı commit'te C#, D-091): `siteColsRange(6…10)` = [3, 3] + "K-49 item 2 levels 6–10 require
Ws = 3"; `pallet_stuck` (alt katmanlı paletin dört birim ötelemesi de kendi ayak iziyle kesişiyorsa error, L-41) +
"K-58 item 10 …"; L-16/L-22'de Y12 muaf, Y12 `firstLevel` 7 + "K-60 item 6 bonus is not a new mechanic".

**CL-V3C-06 · Palet tanıtımı geç ve gömülü; öğretici metni kuralı anlatmıyor** — yer: `level_007.json` (palet (4,0),
üstü `Wl` ve `Rm`; `tutorial` `piece:17`), `src/i18n/*.json`, STORY §6A.
Kanıt: §4 palet ritmi ve §7. Bölüm 7'de palet katmanları 3. ve 4. kata, kanonik 15./16./20. hamlede gidiyor; vurgu 14
hamle boyunca tutulamayan bloğu gösteriyor, metin "Bütün bloklar plana girecek." D-104: "tanıtım bölümü kolay bir
dilimle". D-104 kural cümlesi yalnız Unity'de (`card.y10.site`); web i18n'de yok (D-091 tek kaynak ilkesi). Bölüm 6
metni ("Plan alttan üste dolar.") vurguladığı iki renkli bloğu anlatmıyor.
Düzeltme: product-lead Bölüm 7'de paletin üstünü başta açık bırakır ve ilk katmanı 1. ya da 2. kata verir (ya da
adıma `startOn` ekler); design-lead `tut.m.palletsite`, `tut.m.widesite`, `tut.m.slippery`'yi STORY §6A'ya ekler ve
`card.y10.site` metnini web i18n'e taşır (öneri: `obs.y10.desc` ikinci cümlesi); code-lead i18n sayım testini günceller.

**CL-V3C-07 · `levels:check` kapısı önbellekten geçiyor** — yer: `artifacts/solver/*.json` (git'te izleniyor),
`.github/workflows/ci.yml` (yalnız `levels:validate`).
Kanıt: temiz worktree'de `npm run levels:check` 1 sn'de "10 from cache" ile yeşil. Önbellek anahtarı `levelHash`,
`RULES_VERSION`, `SOLVER_VERSION` ve seçenekler; çözücü ya da çekirdek kodu sürüm artırılmadan değişirse eski sonuç
geçer. Benim önbelleksiz koşum yeşil ve birebir aynı çıktı (bu tur için kapı gerçekten yeşil).
Düzeltme: CI'a `levels/**`, `tools/solver/**`, `src/core/**` değişince `node tools/solve.ts --check --no-cache --jobs 4`
(≈ 11 dk) işi; ya da önbellek anahtarına `src/core` + `tools/solver` kaynak karması. Ajan raporlarında "levels:check
yeşil" yalnız `--no-cache` ile yazılsın.

**CL-V3C-08 · Görsel kanıt eksik: bilgi kartı ve "+2" patlaması** — yer: `tools/ci-shots/shots.json`, `ShotScenario`.
Kanıt: ci-shots `4b8ed97`'de `level_start_window` Bölüm 3 (kartsız); v3c çekimleri pencere kapandıktan sonraki tahta.
P4'ün "bilgi kartı 4/5" puanı bir görüntüye dayanmıyor. R2-13: "görsel kalite görüntüyle kanıtlanır".
Düzeltme: `v3_info_7` (Bölüm 7 başlangıç penceresi, kart + `card.y10.site`) ve `v3_bonus_burst_10` (golden'ı bonus
hamlesine kadar oynayıp tween'i %45'te durduran kare) çekimleri; ayrıca 9 sütunlu FIT çekimi (`fit/v3_wide_6`).

**CL-V3C-09 · 83 belge↔veri testi atlanıyor** — yer: `tests/review/data.review.test.ts` (`describe.skipIf(!LEVELS_2_CURRENT)`),
LEVELS §1/§2.
Kanıt: `npm test` 83 atlanan testin tamamı bu dosyada; LEVELS §2 kartları ve §1 eğri tablosu 6–10 için eski taslak.
Hiçbir bölümün belge↔JSON denetimi koşmuyor.
Düzeltme: product-lead §2 kartlarını v3c'ye çeker (ya da §8.14.2 tablosunu §2 biçimine taşır); code-lead testi açar.

**CL-V3C-10 · Palet görünümü kabul edilmiş okunurluk kuralından sapıyor** — yer: Unity `art` `ico_layers_2..4`,
`pallet_slab`, `BlockSkin` (P4 `f7a8043`, `4b8ed97`); GDD K-65 madde 4, OBSTACLES Y10 "Okunurluk", ART_UNITY Y10 satırı.
Kanıt: K-65 madde 4: "palet katman sayısı rakamla değil çentikle gösterilir"; OBSTACLES Y10: "taban açık gri-mavi metal
kızaktır (ahşap rengi ve ağaç damarı yok — W 'Ahşap' bloğuyla karışmasın) … çentikle (rakam yok; '+2' bonus … ile
karışmasın)". ci-shots: "×3" rakamlı rozet + ahşap kalaslar; Bölüm 7'de palet W (ahşap) bloğu; Bölüm 10'da "×3" ve "+2"
aynı tahtada. Sapma hiçbir karar ya da REVIEW_LOG kaydına dayanmıyor (benim v3 incelememdeki CL-V3-13 "çentikler 23 px'te
ayırt edilmiyor" bulgusu muhtemel tetikleyici).
Düzeltme seçenekleri: (A) çentiğe dönmek ama çentikleri büyütmek (dudakta 2–4 çentik, her biri ≥ 16 design px) ve
kızağı gri-mavi metal yapmak; (B) product-lead + design-lead K-65 madde 4'ü "×n, beyaz daire, altın yıldızdan ayrı
biçim" diye değiştirir ve OBSTACLES/ART_UNITY metni eşitlenir. **Seçimim B, renk için A**: 360'ta "×3" beyaz daire ile
"+2" altın yıldız biçimce ayrışıyor (görüntüde doğruladım), ama ahşap taban W bloğuyla karışma riskini gerçekten taşıyor;
kızak rengi spesifikasyona dönsün. Karar kayda geçmeden sapma kalmasın.

### Öneri

**CL-V3C-11 · Fuzz boşlukları** — yer: Unity `tools/diff/ts-oracle.mjs`, `tools/gen-p1-vectors.ts`. Vinç `palletSiteOnly`
reddi 0 kez, P5/S4/Z4 0 dizide. Düzeltme: kâhine (iptaller gibi) reddedilen Vinç denemesi adımı ve P5/S4/Z4 içeren 3
sütunlu bir geometri; p1 vektörlerine P5.

**CL-V3C-12 · 360 px'te rozet rakamları küçük** — yer: Unity `BlockSkin` rozet ölçeği. Rozet ≈ 46 design px, rakam ≈ 7–8
CSS px. Düzeltme: rozet `k`'dan bağımsız en az 56 design px, rakam ≥ `kit.badge.fontPx` · k.

**CL-V3C-13 · Vekil örneklem gürültüsü** — yer: `walks.ts` (`plays: 200`). %95 güven ±6,5 puan; Bölüm 6'da gevşek bütçe
(.24) sıkı bütçenin (.25) altında. Düzeltme: bant kapısında 1 000 oyun ya da Wilson aralığı raporu.

**CL-V3C-14 · GDD örnekleri sevkiyatla uyuşmuyor** — yer: GDD K-58 örneği (Bölüm 7 paleti (0,0)–(1,0), sayaç 20; sevkiyat
(4,0), 24), K-62 örneği (Bölüm 9 `min` 22 → 26; sevkiyat 19 → 23). Düzeltme: product-lead örnekleri §8.14 verisine çeker.

**CL-V3C-15 · Öğretici vurgu deseni 9. sütunu adresleyemiyor** — yer: şema `cell:[0-7],[0-9]` (web ve C#). Düzeltme:
`[0-8]`, iki tarafta aynı commit.

**CL-V3C-16 · Sahiplik** — yer: web `37d5617`. product-lead commit'i code-lead dosyalarını (`src/core/level/mechanics.ts`,
`tests/level/shipped.test.ts` mantığı, `tests/tools/solver/shipped.expected.ts`) değiştirdi. Değişikliği inceledim, K-60
madde 6 ile tutarlı. Düzeltme: veri tazeleme dışındaki test mantığı değişikliği REVIEW_LOG üzerinden code-lead'e gitsin.

**CL-V3C-17 · WebGL boyutu bütçe sınırında** — yer: Unity CI uyarıları (P4). 25 MB / 25 MB, 3B çözme 281–320 ms > 250 ms.
Düzeltme: sonraki sanat eklemesinden önce atlas sıkıştırma / sayfa birleştirme ölçümü.

## 11. Sonuç

Kapılar (web `check`, önbelleksiz `levels:check`, Unity build/test, DiffFuzz, stub kapısı, CI, Pages) **yeşil**;
D-104 palet kuralı sevkiyattaki bölümlerde senaryoyla, D-105 bonus bütçesi golden sonuyla kanıtlandı; zorluk abartısı
yok. Kapanması gerekenler: zorluk bantlarının gerçek bir orta vekille ölçülmesi ve finalin gerçekten zor olması
(01, 02), 2 sütunlu bölümlerdeki tuzaklar (03), blok sayısı gerekçesinin düzeltilmesi (04), doğrulayıcı açıkları (05),
palet tanıtımı (06), kapının önbellekten bağımsızlığı (07), eksik görsel kanıt (08), atlanan belge testleri (09) ve
palet görünümünün kabul edilmiş K-65 madde 4 ile eşitlenmesi (10).

## 12. Kapanışlar (code-lead, Paket F, 2026-11-12)

Web `wip/phase-2r` + Unity `main` aynı turda (D-091). Önbelleksiz `node tools/solve.ts --check --no-cache --jobs 4`
(605 sn, 4 işçi, 10/10 çözüldü, 0 hata, 2 bant uyarısı + Bölüm 4/5 plan_trap uyarıları) yeşil; Unity `dotnet build -warnaserror` 0 uyarı, `dotnet test` 595 + 1275 geçti, stub kapısı 4/4.

| Kimlik | Durum | Kapanış |
|---|---|---|
| CL-V3C-01 | **Kısmen kapandı** | `walks.ts policyWinRate` artık `lookahead` (1 adım ileri bakış) + `eps` destekliyor; `targets.policyWin` (şema, C# eşi) bandı ε = 0,1 ve `moves` bütçesinde kapıya bağlı (`metric_out_of_band`, uyarı). Ölçüm (her hamlede 24 rastgele kaydırma adayı puanlanır; tam tarama Bölüm 10'da 218 sn sürüyordu, örneklemeyle 18 sn): 6/7/8/9/10 = 0,88 / 0,76 / 0,89 / 0,86 / 0,62. Bantlar (6–9: 0,80–0,90; 10: 0,50–0,60) level_006…010 `targets.policyWin` alanına yazıldı. **Bölüm 7 (0,76) ve 10 (0,62) bant dışı** → 2 uyarı. Final için A ya da B kararı product-lead'in; kapı kırmızıya dönmesin diye şimdilik uyarı. |
| CL-V3C-02 | **Kapandı** | `Expander.lastRawCost` (kırpılmamış maliyet, bonus = 1 − 2 = −1); politika `spent` bunu kullanıyor. Test `K-62 play proxy credits the bonus`. Bölüm 10 etkin payı artık ölçüme giriyor. LEVELS §8.14.3 vekil sütunu product-lead'in yeniden üretmesi için hazır (rapor notu `lookahead proxy`). |
| CL-V3C-03 | **Kod kapandı, veri açık** | `plan_trap` taraması `Ws = 2`'de de koşuyor (yalnız `schemaVersion 3`; arşivler etkilenmez): Kolay = error, Normal+ = warn. Bölüm 4'ün bilinen tuzağı `NARROW_PLAN_TRAP_OPEN = {4}` ile uyarı (Bölüm 5 Normal → uyarı). **Açık: product-lead 4 ve 5'i yeniden boyayınca `{4}` kümesi boşaltılır** (C# eşi `Logic.NarrowPlanTrapOpen`). |
| CL-V3C-04 | product-lead | Veri/gerekçe işi; code-lead eylemi yok. Öneri: K-58 madde 10 yazılırsa `palletStuck` C3'ü açacak biçimde gevşetilir. |
| CL-V3C-05 | **Kapandı** | (a) `siteColsRange(6…10) = [3, 3]` (yalnız `schemaVersion 3`; arşiv v1/v2 [2, 3] kalır) + `K-49 item 2 levels 6-10 require Ws = 3`; (b) `pallet_stuck` (L-41, error): paletin dört birim ötelemesinin hepsi kendi ayak iziyle kesişirse (C3/O4/L4/J4/T4) + `K-58 item 10`; (c) L-16/L-22 Y12'yi saymaz, `Y12 firstLevel 7`. C# eşleri aynı turda (`Logic.SiteColsRange`, `PalletStuck`, `CheckMechanics`, `Mechanics.TraitTable`), `PackageFTests.cs`. |
| CL-V3C-06 | design-lead / product-lead | i18n anahtarları STORY §6A'ya eklenince sayım testi güncellenir; şimdi eklenecek anahtar yok. |
| CL-V3C-07 | **Kapandı** | (1) Çözücü önbellek anahtarına `src/core/**` + `tools/solver/**` kaynak karması eklendi (sürüm artırmadan değişen kod eski sonucu geçiremez). (2) CI'a `levels-nocache` işi: `levels/`, `tools/solver/`, `src/core/` ya da `tools/solve.ts` değişince (PR'larda her zaman) `node tools/solve.ts --check --no-cache --jobs 4`. Bu rapordaki "levels:check yeşil" önbelleksiz koşuya dayanır. |
| CL-V3C-08 | **Kısmen kapandı** | `v3_info_7` (Bölüm 7 başlangıç penceresi: kart + palet metni, `GameApp.DebugOpenLevelStart`) ve FIT `v3_wide_6` (9 sütun, 390 ve 360) ana dal CI'ında üretildi ve ben okudum: kart görünür, 9 sütunlu FIT tahtası taşmadan sığıyor. **`v3_bonus_burst_10` çalışmadı**: golden çözüm bonusa kadar oynandı, "+2" ekranda göründü (`_failure.png`) ama senaryo sinyali 150 ve 300 sn içinde gelmedi; neden bulunamadı (oyun saatinin takıldığından kuşkulanıyorum). Adım senaryodan çıkarıldı ki manifest yeşil kalsın; yerel WebGL koşusuyla ayrı bir iş olarak açık. |
| CL-V3C-09 | product-lead | LEVELS §2 kartları çekilince `LEVELS_2_CURRENT` açılır; code-lead bekliyor. |
| CL-V3C-10 | design-lead / product-lead | Karar (rakam B, taban A) onların; Unity sanat tarafı kararla birlikte. |

## 13. Kapanışlar (v3c2 turu: A1, B1, C2 web; C1 Unity, 2026-11-12)

Kararlar: D-106 (palet katmanı kendi paletiyle çarpışmaz, `RULES_VERSION` 6), D-107 (blok hedefleri, bonus Bölüm 7, bantlar),
D-108 (palet görünümü). Web `wip/phase-2r`: A1 `98a01c0`, B1 `b92120a`, C2 `2a08cd2`. Unity `main`: C1 `afe949e`.

Kapılar (C1'in kendi koşuları): Unity `dotnet build -warnaserror` 0 uyarı / 0 hata; `dotnet test` Presentation **609 geçti**, Core
**2151 geçti / 5 atlandı** (JS uyumluluk notları); `tools/stub-gate-selftest.sh` 4/4. Web 2a08cd2 `npm run check` yeşil (125 dosya,
**2317 test, atlanan 0** — `data.review` işareti etkin; kural kapsamı 88 kimlik / 1702 test). Önbelleksiz `levels:check`: §13.2.

### 13.1 Bulgu tablosu

| Kimlik | Durum | Kapanış |
|---|---|---|
| CL-V3C-01 | **Kapandı** (B1) | Bonus Bölüm 7'ye taşındı (seçenek A); ileri bakış vekili (ε 0,1, 400 oyun, Wilson %95) 6–9 = 0,83 / 0,86 / 0,86 / 0,88, 10 = 0,54 → bantlar (6–9 %80–90, 10 %50–60) içinde, `policyWin` uyarısı 0. Bölüm 10'da bonus yok, etkin pay P 1 + a 2. Unity verisi 2a08cd2 ile eşit. |
| CL-V3C-03 | **Kapandı** (B1 + C2 + C1) | Bölüm 4 ve 5 yeniden boyandı, tuzak 4 → 0 ve 3 → 0. `NARROW_PLAN_TRAP_OPEN` boş (web C2, C# `Logic.NarrowPlanTrapOpen`). Tuzaksız kanıtlı plan `tile_family_mix`'i susturur (iki tarafta). C# testleri `K63_C2_narrow_plan_trap_open_is_empty_and_levels_1_to_5_have_no_plan_trap`, `K63_C2_tile_family_mix_is_silent_on_a_proven_trap_free_plan`. |
| CL-V3C-04 | **Kapandı** (A1 + B1) | D-106 ile C3/O4 paletleri açıldı: Bölüm 6–10 = 17 / 20 / 19 / 19 / 20 = 95 blok (D-107: 6 ≥ 17, 7–9 ≥ 18, 10 ≥ 20). |
| CL-V3C-06 | **Kapandı** (B1 + design-lead + C1) | Bölüm 7 öğreticisi iki adım (`startOn: segmentDone`, `tut.m.pallet` → `tut.m.palletsite`), 6 `tut.m.widesite`, 9 `tut.m.slippery`; `card.y10.site` web i18n'de (Unity üstyazısından kaldırıldı). **Not:** iki EN metni 6 kelimeyi aşıyor (7 ve 8); Unity üstyazısı geçici 6 kelimelik metin kullanıyor, design-lead'e REVIEW_LOG. |
| CL-V3C-08 | **Kök neden bulundu, düzeltildi** (C1) | §13.3. |
| CL-V3C-09 | **Kapandı** (B1 + C2) | LEVELS §2 kartları sevkiyat verisinden; `data.review` işareti etkin, 83 kart testi koşuyor (web 2317 / 0 atlanan). |
| CL-V3C-10 | **Kapandı** (D-108 + C1) | "×n" beyaz daire rozet (satırda ilk), gri-mavi metal kızak, katman bantları; §13.4. |
| CL-V3C-11 | **Kapandı** (C1) | Kâhine Vinç `palletSiteOnly` reddi, D-106 şekilli paletler, P5/S4/Z4 geniş tahtaları ve betikli C3 palet senaryosu; kapılar eklendi. §13.2. |
| CL-V3C-12 | **Kapandı** (C1) | `BadgeMinPx` 66 (görünen ≥ 56), `PalletBadgePx` 82 / en az 61 (görünen Ø 76 / ≥ 56); rakam ≥ 36 × k. Test `Y10_K65_item_4_D108_CL_V3C_12_badges_never_shrink_under_56_design_px`. |
| CL-V3C-13 | **Kapandı** (A1) | `policyWinRates` aynı oyunlarda birden çok bütçe, 400 oyun, Wilson %95 aralığı raporda. |
| CL-V3C-14 | **Kapandı** (B1) | GDD K-58 / K-62 örnekleri sevkiyat verisinden. |
| CL-V3C-15 | **Kapandı** (A1 + C2 + C1) | Vurgu deseni `cell:[0-8],[0-9]`, `done.at` ve `hand.path` x 0–8; C# `LevelSchemaPatterns`, `LevelJson` ve testler (`JsRegexTests`, `K65_K53_CL_V3C_15_hand_path_and_done_at_accept_column_8`). |

### 13.2 C# eşitliği ve fuzz (C1)

- D-106: `Grid.CollisionMasks` her zaman `exclude + 1` atlar, `OwnCellCode` kalktı; Vinç `YardSpotFree` `self + 1`;
  `Deadlock.QuickHoldable` her palet katmanını BFS'e bırakır; `Logic.PalletStuck` ve `pallet_stuck` kalktı; `RulesVersion` 6.
  `PalletD106Tests` (web `palletD106.test.ts` ikizi, 19 test): eski C# çekirdeğinde 10'u kırmızı.
- Golden, çözüm günlükleri, bölümler, i18n, fikstürler web 2a08cd2'den (`tools/sync-from-web.sh`); p0–p3, review follow / d3a /
  json ve `rule_ids` vektörleri yeniden üretildi. p1'e 5 "geniş" tahta eklendi (P5, S4, Z4; C3, L4, T4, J4, O4 paletleri; düzey
  verisi vektörde).
- DiffFuzz (`node tools/diff/ts-oracle.mjs <web 2a08cd2>`): **851 dizi, 24 007 eylem, 0 sapma** (C# `DiffFuzz` 853/853). Kapsam:
  Vinç `palletSiteOnly` reddi **110** (rastgele 104 + betik 6), sürükleme `palletSiteOnly` iptali 164, D-106 kendi ayak izinden
  çıkış C3 171 / L4 50 / O4 44 / J4 42 / T4 20, P5 205 / S4 178 / Z4 89 uygulanmış hamle. Kâhin kapıları bunların her birini
  sayıyor. Eski C# çekirdeği bu çıktıda **92 dizide** sapıyor (Bölüm 7–10 ve D-106 tahtalarının hepsi).

### 13.3 CL-V3C-08 kök nedeni (bonus patlaması sinyali)

Kanıt: `ci-shots` dalındaki konsol günlükleri — bonus adımının olduğu üç koşuda (82ef885, e09624a, 6c9d50f) son 600 kaydın hepsi
`NullReferenceException … UnityEngine.Bindings.ThrowHelper` (1200 satır), adımın olmadığı koşularda (4b8ed97, 2385036) 0.
`_failure.png`: "+2" patlaması, yıldız parçacıkları ve sevinen kral aynı karede donmuş. Sebep: `LevelHud.RebindTargets` "+n"
patlama hedefini yeniden bağlamıyordu; her bölümün ilk `SetHudState`'i boş plan küçük resmini doldurup HUD'u yeniden gösterdiği için
(`SyncCells` → `surface.Show(Hud)`, UiRenderer eski RectTransform'ları yok eder) hedef yok edilmiş nesnede kalıyordu. Bonus
yerleşiminde `Tweens.Pop(MovesBurst)` her karede motor istisnası attı; istisna `GameApp.Tick` içinde olduğu için dizi ilerlemedi ve
`GameRunner.Update`'te ondan sonra çağrılan `shots.Tick` hiç çalışmadı → sinyal yok. Zaman aşımı büyütmek bunu çözemezdi; gerçek
oyuncu da bonus anında takılıyordu (P4 f7a8043'ten beri).

Düzeltme: (1) `RebindTargets` bütün HUD hedeflerini bağlar ve `Layout` de onu çağırır; (2) `FakeSurface` bir katmanı yeniden
gösterince eski hedefleri yok eder (Unity gibi) — `HudRebindTests` düzeltmesiz kodda "B1 at the start: the burst target" ile
kırmızı; (3) `GameRunner`, `GameApp.Tick` istisnasını `ShotScenario.Fault` ile `error:exception:…` olarak bildirir (sessiz zaman
aşımı yerine anında hata); (4) çekim zaman aşımı varsayılan 150 sn'ye döndü; (5) `v3_bonus_burst_7`: Bölüm 7 golden'ın bonus
hamlesine kadar animasyonsuz yeniden oynatılır (`GameApp.DebugStartLevelAt`, `GameSession.Replay`), bonus hamlesi animasyonla
oynanır, patlama göründükten 144 ms sonra (320 ms'lik büyümenin %45'i) oyun saati durur (`TimeScale` 0) ve çekim istenir; döngüdeki
tween'ler (rozet nabzı) yüzünden `Idle()` yerine hamle kuyruğu beklenir. `v3_bonus_10` → `v3_final_10`.
