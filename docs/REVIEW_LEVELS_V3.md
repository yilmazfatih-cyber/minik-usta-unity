# Bölümler v3 şüpheci inceleme (REVIEW_LEVELS_V3)

İnceleyen: code-lead (bağımsız şüpheci, kod değiştirilmedi) · Tarih: 2026-11-09 ·
Yöntem: Claude-Code-Game-Studios `qa-lead` / `qa-tester` rolleri + `balance-check` ve `playtest-report` becerileri
(orkestrasyonlu koşu istisnası: soru sorulmadı, seçenekler ve seçim her bulguda yazılı).

İncelenen sürümler:

| Repo | Ref | İçerik |
| ---- | --- | ------ |
| web `minik-usta` | `origin/wip/e2` = `1fe9af3` (= `2035fc6` E1 çekirdek + E2 bölümleri) | 10 bölüm, çözücü, doğrulayıcı |
| Unity `minik-usta-unity` | `origin/main` = `d52cae1` (E3) | C# portu, sanat, sunum |

Unity'de yayınlanan `Resources/levels/level_0NN.json` dosyaları web `wip/e2` dosyalarıyla JSON düzeyinde birebir aynı
(1, 5, 10 karşılaştırıldı: eşit). Bu yüzden bölüm incelemesi web `wip/e2` üzerinde yapıldı.

Önem: **Engel** = sahibinin isteği ya da bir kapı karşılanmıyor, iş geri döner · **Önemli** = bu turda kapanmalı ·
**Öneri** = birikim listesi.

## 1. Kendi koşturduğum komutlar (kanıt)

| Komut (kendi worktree'mde) | Sonuç |
| --- | --- |
| web `npm ci` (kilit dosyasından, yeni bağımlılık yok) | 139 paket |
| web `npm run levels:check` | **KIRMIZI** (çıkış 1): 10/10 çözüldü, **6 hata** (`shape_decisions_low` L-42, Bölüm 5–10), 4 uyarı (`puzzle_greedy_easy/wins` L-39: 6, 8, 10). Bot aşaması: `tools/playtest-bot.ts not present — skipped` |
| web `node tools/solve.ts --no-cache --jobs 4` | Önbelleksiz yeniden hesap 61 s; durum sayıları, min, YAO önbellekle **birebir aynı** (determinizm OK) |
| web `npx vitest run` | **KIRMIZI**: 116 başarısız / 2134 geçti / 18 atlandı (10 dosya: `data.review` 73, `solver/shipped` 25, `shipped.heavy` 9, `i18n` 4, `levelScene2r` 2, `level/shipped` 2, `fastpath` 1, `validate-tool` 1, `schema` 1, `golden/hand` 1) |
| web `typecheck`, `lint`, `format:check` | yeşil |
| web `npm run test:rules` | yeşil, ama "K 46" sayıyor: K-56…K-66 ÖNERİ başlığı olduğu için **sayılmıyor** (bkz. CL-V3-11) |
| Unity `dotnet build -warnaserror` | yeşil (0 uyarı) |
| Unity `dotnet test` | yeşil: Core 1250 geçti / 8 atlandı, Presentation 586 geçti. **Atlananlar diff-fuzz** (`no oracle output`) |
| Unity `node tools/diff/ts-oracle.mjs <web e2>` + `dotnet test --filter DiffFuzz` | 729/729 yeşil (fuzz yalnız yerelde, CI'da koşmuyor) |
| Unity `bash tools/stub-gate-selftest.sh` | yeşil (4/4) |
| CI (`gh run list`) | `d52cae1`: Build WebGL ✓, Deploy Pages ✓, CI screenshots ✓ (27 dk). `a4e1fb1`: iptal (40 dk sınırı) |

NOT ASSESSED (veri yok, tahmin yapılmadı): CI ekran görüntüsü yapıtları (indirme 403), canlı Pages sayfası (proxy 403),
`traitDelta` (bot "özelliği yok say" kipi yazılmamış), cihazda FPS. Bunlar "geçti" sayılmaz.

## 2. Kural ↔ kod ↔ test

Test adında kimlik geçen test sayısı (TS: `it/test/describe` adı; C#: `[Test(Description=…)]`):

| Kural | TS | C# | Not |
| --- | --- | --- | --- |
| K-56 şekiller | 6 | 6 | OK |
| K-57 iki renkli | 7 | 11 | OK |
| K-58 palet | 7 | 9 | OK (yayında palet yok) |
| K-59 sargı + makas | 6 | 11 | OK |
| K-60 bonus | 6 | 11 | OK |
| K-61 kaygan | 2 | 3 | OK |
| K-62 hamle bütçesi v3 | 2 | **0** | C# testi yok |
| K-63 döşeme aileleri | 2 | **0** | `tile_family_mix` C#'ta `Logic.cs`'te var, adlı testi yok |
| K-64 ölçütler | 7 | **0** | Yalnız TS çözücü aracı: C# için "N/A" yazılmalı |
| K-65 Hikaye 1 okunurluk | 4 | **0** | `colors_low_contrast`, `stack_mixed` C#'ta var, adlı testi yok |
| K-66 güçlendiriciler | 3 | 5 | OK |

TS-C# fuzz (yerelde ürettiğim `tools/diff/out`, 24 v3 tahta × 3 tohum = 72 dizi, 2790 adım). Olay sayımı:

| Aile | Diziler | Doğru yerleşim | Özellik olayı | Kazanış |
| --- | --- | --- | --- | --- |
| twotone | 12 | 9 | renk sekmesi 130 (iki renkli hücre kontrolü çalışıyor) | 0 |
| pallet | 12 | 19 | **`covered` reddi 0** (örtülü katman hiç denenmiyor) | 0 |
| wrap | 12 | 13 | `unwrapped` makas 10, çekiç 3 | 0 |
| bonus | 12 | 16 | `movesChanged{bonus}` 10 | 0 |
| slippery | 12 | 18 | `pieceFell` 262 (kaygana ayrıştırılamıyor) | 0 |
| mix | 12 | 21 | makas 7, çekiç 13, bonus 12 | 0 |

Sonuç: sargı/makas/bonus gerçekten kapsanıyor; palet "örtülü katmanı alma" yolu, v3 kazanış yolu ve kaygan düşüşün ayrımı
fuzz'da **kapsanmıyor**.

## 3. Bölümler (balance-check: Progression)

Ölçülen (önbelleksiz çözücü, `wip/e2`):

| # | Etiket | Saha | Şantiye | N | min | hamle | P | açgözlü kazanır | aha | bot acemi/orta/usta | yeni |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | Kolay | 5×4 | 2×4×2 | 8 | 8 | 12 | 4 | 1.00 | 0 | .89/.99/1.00 | S1 |
| 2 | Kolay | 6×5 | 2×6×2 | 8 | 8 | 12 | 4 | 1.00 | 0 | .76/.97/1.00 | L4/J4 |
| 3 | Kolay | 6×4 | 2×5×2 | 8 | 10 | 14 | 4 | 1.00 | 0 | .75/.97/1.00 | C3 |
| 4 | Kolay | 6×4 | 2×5×2 | 8 | 10 | 14 | 4 | 1.00 | 0 | .70/.96/.99 | R, ayna C3 |
| 5 | Normal | 5×4 | 2×4×2 | 8 | 10 | 14 | 4 | 1.00 | 0 | .62/.93/.99 | Y9 |
| 6 | Normal | 6×4 | 2×5×2 | 8 | 10 | 12 | 2 | 1.00 | 0 | .45/.83/.96 | T4 |
| 7 | Normal | 6×4 | 2×5×2 | 9 | 10 | 14 | 4 | 1.00 | 0 | .68/.95/.99 | Y11 |
| 8 | Normal | 6×4 | 2×5×2 | 8 | 12 | 14 | 2 | 1.00 | 0 | .32/.73/.91 | I3, Çekiç |
| 9 | Normal | 6×4 | 2×5×2 | 8 | 10 | 14 (+2 bonus) | 4 | 1.00 | 0 | .72/.96/1.00 | Y12 |
| 10 | Zor | 6×4 | 2×5×2 | 8 | 12 | 13 | 1 | 1.00 | 0 | .15/.49/.74 | final, Vinç |

Sahibinin sınırları: minMoves ≤ 20 ✓ (8–12) · "aha" ≤ 2 adım: aha **hiç yok** (ahaMin 0 her bölümde) · abartı zorluk:
Bölüm 10'da orta bot %49, usta bot %74, ama açgözlü strateji %100 kazanıyor → zorluk içgörüden değil hamle payından
(P = 1) geliyor; bu "sinir bozucu" profilidir.

Blok eğrisi: 8, 8, 8, 8, 8, 8, 9, 8, 8, 8 = **81** (eski A2: 71; orkestratör hedefi 124, Bölüm 10'da 16–22). Saha hiçbir
bölümde 6×5'i aşmıyor; 7/10 bölüm aynı geometri (6×4 saha, 2×5 şantiye, duvar 4). Şekil dağılımı: D2 81 bloğun 55'i (%68).

Tanıtım sırası: 1 S1 · 2 L4/J4 · 3 C3 · 4 R + ayna C3 · 5 Y9 · 6 T4 · 7 Y11 · 8 I3 + Çekiç açılışı · 9 Y12 · 10 Vinç.
Bölüm başına bir fikir kuralı 4 ve 8'de esniyor (renk + ayna; şekil + güçlendirici). Çekiç Bölüm 8'de açılıyor ama Bölüm 8'de
hedef yok (`noTarget`). Öğretici: 5'te `tut.m.match` ("Plandaki renge uyanı seç.") iki renkli bloğun hücre başına kuralını
anlatmıyor; 7 `tut.m.wrap` ve 9 `tut.m.bonus` uygun. `obs.y9…y13` bilgi kartları i18n'de yok.

JSON `targets` alanları ölçülen değere çekilmiş (ör. Bölüm 10 `greedyWin [0.9, 1]`, `ahaMin [0, 0]`; LEVELS şartnamesi
`greedyWin [0.05, 0.25]`, `ahaMin ≥ 2`). Çözücü bunu `targets_diverge_from_spec` notuyla bildiriyor ama kapı geçiyor:
hedef ölçüme uydurulduğu için kapı anlamını yitiriyor.

## 4. Eğlence testi (playtest-report, 3 strateji)

| Strateji | Kaynak | Sonuç |
| --- | --- | --- |
| Açgözlü (her hamlede doğru yerleşim varsa yap) | çözücü `greedyWin` | 10/10 bölümü bütçe içinde, fazla hamlesiz (`greedyExcess 0`) bitiriyor |
| En üsttekini al | çözücü acemi bot (yaklaşık) | 1–7, 9'da %62–89; 8'de %32, 10'da %15 |
| Plancı | usta bot | %74–100 |

Okuma: açgözlü oyuncu hiçbir bölümde "durup düşünmek" zorunda kalmıyor; plansız oyuncu yalnız 8 ve 10'da cezalanıyor ve
ceza "bir hamle eksik kaldım" biçiminde geliyor. Aynı saha boyutu + %68 domino + iki renkli blokların hepsi dikey domino
(D2_90) olunca 3–10 arası görsel ve zihinsel olarak birbirine benziyor (tekrar hissi). Olumlu: YAO/liftRatio 0.88–1.00,
tuzak 0, çıkmaz oranı 0, D1 0: hiçbir bölüm haksız değil.

## 5. Görsel okunurluk

`art/src/trait/*.svg` 12 kareyi 360 px ölçeğinde (rozet 70 tasarım px ≈ 23 CSS px) ve 2× boyutta tarayıcıda çizip baktım:

- Makas, sargı ağı, fiyonk, kar tanesi 23 px'te ayırt ediliyor.
- `ico_layers_2/3/4` 23 px'te birbirinden ayırt edilemiyor (yalnız çizgi sayısı farklı).
- `ico_bonus_1/2/3` rakamı 23 px'te okunuyor ama sınırda.
- `ovl_ice` açık zeminde neredeyse görünmüyor.
- Renk kontrastı (tokens): W-Y 6.45:1, W-G 4.23:1, R-Y 3.6:1, **G-Y 1.52:1**. Bölüm 10'daki iki iki-renkli blok tam da G-Y
  (biri sargılı, biri makaslı). `SaveModel.Colorblind` kaydediliyor ama `BlockSkin` kullanmıyor.
- Oyun içi ekran görüntüsü: NOT ASSESSED (CI yapıtı indirilemedi).

## 6. CI, Pages, telif

CI son koşu yeşil (WebGL + Pages dağıtımı + ekran görüntüsü). Canlı sayfa sandbox'tan doğrulanamadı. Yeni SVG'ler elle
yazılmış (lisans/üçüncü taraf iz yok, gömülü bitmap yok). Tema: sargı/makas/bonus nötr; Usta Kral (D-101) ile çelişen öğe yok.

## 7. Bulgular

### Engel

**CL-V3-01 · Sahibinin "daha çok blok" isteği karşılanmıyor** — yer: `levels/level_001…010.json`.
Kanıt: N = 8 (Bölüm 7'de 9), toplam 81; saha en çok 6×5. Sebep E2-B2: D-090 tam tarama kanıtı yaklaşık 8–9 parçada tıkanıyor.
Seçenekler: (A) kanıtı makro grafa taşımak (U-v3-2 B; pahalı, haftalar). (B) Beam araması `best == lowerBound` bulduğunda
çözülebilirliği kanıtlanmış saymak. Ölü masa kapısını tam tarama yerine D1 + D3a çalışma anı kurtarma ve 10 000 rastgele
yürüyüşte ölü durum oranı ≤ %1 ile vermek (ucuz, mevcut araçlar). (C) Bölümü 3 dilime bölüp dilim başına 6–7 parça vermek
(K-55 sabit saha ile çelişir, product-lead kararı). **Seçimim B**: Bölüm 1 için N 8, Bölüm 10 için N 16 ile 6–10 yeniden
üretilsin. Bu yolun tohum 1/3/5'te 0,1–19 s ölçümü E1 raporunda var.

**CL-V3-02 · Kırmızı kapılı bölümler yayında** — yer: Unity `main` `00e68bf` (sync kaynağı web `wip/e2`).
Kanıt: web `levels:check` 6 hata, `vitest` 116 başarısız. Unity bunları `main`'e aldı ve Pages'e dağıttı. Web
`wip/phase-2r` hâlâ eski bölümlerde, yani D-091 "tek doğruluk kaynağı" bozuk.
Düzeltme: (1) product-lead `shape_decisions_low` için B3 kararını versin (Hikaye 1'de uyarı); (2) içerik testleri yeni
bölümlere uyarlansın; (3) `wip/e2` `wip/phase-2r`'ye alınsın; (4) Unity sync zinciri yeniden koşsun. O zamana kadar Unity
README/Pages'te "v3 önizleme" etiketi taşısın.

### Önemli

**CL-V3-03 · "Aha" yok, Bölüm 10 zorluğu hamle payından geliyor** — yer: Bölüm 6–10 `targets`, `moves`.
Kanıt: açgözlü strateji 10/10 kazanıyor, `ahaMin 0`. Bölüm 10'da P = 1, orta bot %49. Hedefler ölçüme uydurulmuş.
Düzeltme: şartname hedefleri JSON'a geri yazılsın (Bölüm 10 `greedyWin ≤ 0.25`, `ahaMin ≥ 1`). İçgörü yoksa Bölüm 10
`moves` 14 olsun (P 2). Bölüm 7–10'da en az birinde `ahaMin ≥ 1` kapı olsun.

**CL-V3-04 · Tekrar hissi** — yer: Bölüm 3–10.
Kanıt: 7/10 aynı geometri, D2 %68, iki renkli blokların hepsi D2_90, `shapeDecisions 0`.
Düzeltme: geometri her bölümde değişsin (duvar 3/4/5, saha 5×5/6×5/6×6). Bölüm 3'ten sonra D2 en çok %50 olsun. Bölüm
10'a kadar en az bir iki renkli C3 ya da L4 gelsin.

**CL-V3-05 · Yayında yalnız 3 yeni özellik var (istenen ≥ 4)** — yer: Bölüm 5, 7, 9.
Kanıt: Y10 palet B1 yüzünden yok, Y13 Hikaye 2'de. Unity palet ve kaygan bloğu yalnız dev Bölüm 99'da çiziyor.
Seçenekler: (A) K-58 madde 9 "katman yalnız şantiyeye" kuralıyla paleti getirmek (yeni ölü durum ailesi kapanır, ama
doğrulama gerekir). (B) Y13 kaygan bloğu Bölüm 8'e almak: yard yerçekimi çekirdekte zaten var, yeni çıkmaz ailesi yok,
sanatı çizili. **Seçimim B** (en ucuz).

**CL-V3-06 · Bölüm 9 bonusunun oyuna etkisi yok** — yer: `level_009.json`.
Kanıt: `moves` 14 = min 10 + P 4 (Normal bölümde Kolay satırı) + 2 bonus. Bonusa hiç ihtiyaç yok.
Düzeltme: Normal satırına inilsin (`moves` 12). Bonus bütçeyi gerçekten genişletsin ve HUD "(+2)" göstersin. k = 0 kararı
sürecekse bunun bir "his anı" olduğu sahibine açıkça söylensin.

**CL-V3-07 · Çekiç Bölüm 8'de hedefsiz açılıyor** — yer: Bölüm 8, booster açılış tablosu.
Kanıt: E3, hedef `noTarget`. HUD'da 3 deneme hakkı kilitli duruyor.
Düzeltme: açılış Bölüm 10'a alınsın (P-E2-1); orada sargı var. Ya da Bölüm 8'e bir sargı + makas eklensin (bir fikir
kuralını bozar). Seçimim: Bölüm 10.

**CL-V3-08 · G-Y iki renkli bloğun kontrastı 1.52:1** — yer: Bölüm 10, K-65 izinli çiftler, `BlockSkin`.
Düzeltme: Bölüm 10'da G-Y yerine R-Y ya da W-Y kullanılsın, G-Y K-65 listesinden çıkarılsın. Colorblind ayarı açıkken
renge simge eklensin (`SaveModel.Colorblind` zaten var).

**CL-V3-09 · C#'ta K-62/K-63/K-65 adlı testi yok, fuzz CI'da koşmuyor** — yer: `Assets/MinikUsta/Core.Tests`,
`.github/workflows`.
Düzeltme: üç kural için C# testi yazılsın. K-64 `rule-ids`'de "TS araç, C# N/A" diye işaretlensin. 6–8 dosyalık küçük bir
v3 oracle anlık görüntüsü test fikstürü olarak depoya konsun, böylece `DiffFuzz` CI'da atlanmasın.

**CL-V3-10 · Fuzz kapsam boşlukları** — yer: `tools/diff/ts-oracle.mjs`.
Kanıt: palet `covered` reddi 0, v3 kazanış 0/72, iki renkli doğru yerleşim 9/426 adım.
Düzeltme: eylem seçici örtülü katmanı da denesin. Kanonik çözümün sapmalı oynatıldığı diziler eklensin. Her özellik ailesi
için ≥ 20 olay ve ≥ 1 kazanış eşiği test olarak eklensin.

**CL-V3-11 · Kural kapsama kapısı v3 kurallarını görmüyor** — yer: web `tools/rule-coverage.ts`.
Kanıt: `test:rules` K 46 sayıyor, K-56…K-66 ÖNERİ başlığı olduğu için dışarıda.
Düzeltme: `--phase v3` ÖNERİ K-xx'leri de sayan bir mod eklensin, ya da product-lead başlıkları bağlayıcı yapsın.

**CL-V3-12 · Öğretici ve bilgi kartları eksik** — yer: Bölüm 5 `tut.m.match`, i18n `obs.y9…y13`.
Düzeltme: Bölüm 5 için yeni anahtar yazılsın (ör. TR "İki renk de plana otursun." / EN "Both colours must match the plan.";
design-lead ton kontrolü yapsın). `obs.y9…y13` kartları TR ve EN olarak eklensin.

### Öneri

**CL-V3-13 · Küçük boyutta ayırt edilemeyen ikonlar** — `ico_layers_2/3/4` ve `ovl_ice` 23 px'te. Palet ve kaygan blok
yayına girmeden önce katman sayısı çentikle (blok kenarında 2–4 çentik) gösterilsin, buz örtüsüne koyu kenar eklensin.

**CL-V3-14 · `shapeDecisions` ölçütü kalibre edilmeli** — 300 adayda en iyi değer 1, yayında hep 0. Elle kurulmuş, değeri
≥ 2 olan bir pozitif fikstür testi yazılsın. Tanım hatalıysa önce ölçüt düzeltilsin, kapı ondan sonra konsun.

**CL-V3-15 · İçeriğe bağlı TS testleri** — `golden/hand.test.ts` `movesLeft = budget − min` varsayıyor (C# bonuslu formüle
geçti). `fastpath.test.ts` Bölüm 2'de önbellek isabeti bekliyor. İkisi de fikstüre bağlansın.

**CL-V3-16 · Bütçe payları dar** — atlas 66.98 / 67.11 MB, ilk yük paketi 612.5 / 620 KB. Yeni özellik kareleri mevcut UI
sayfasına paketlensin ya da 3 katman ikonu tek ikona + çentik kaplamasına indirilsin.

**CL-V3-17 · CI süresi** — ekran görüntüsü işi bir kez 40 dk sınırında iptal oldu. v3 çekimleri ayrı, elle tetiklenen bir
iş akışına taşınsın.

## 8. Ucuz alternatifler (fizibilite)

| Pahalı fikir | Ucuz alternatif |
| --- | --- |
| Makro graf kanıtı (U-v3-2 B) | Beam `best == lowerBound` + rastgele yürüyüşle ölü durum örneklemesi (CL-V3-01 B) |
| Palet için D3b çıkmaz tablosu | Önce kaygan blok (Y13) Hikaye 1'e alınsın, palet "yalnız şantiyeye" kuralıyla Hikaye 2'de gelsin |
| `traitDelta` bot kipi | Aynı bölümü özelliği kaldırılmış kopyasıyla çözüp `min` ve açgözlü kazanma farkına bakmak (araç 1 gün) |
| Her yeni özellik için yeni atlas sayfası | Mevcut UI rozet sayfasında boş kare + çentik kaplaması |
