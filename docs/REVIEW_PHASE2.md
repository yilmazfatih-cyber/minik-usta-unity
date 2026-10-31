# Faz 2 şüpheci inceleme — C# çekirdeği TS'den nerede sapıyor? (REVIEW_PHASE2)

Kaynak gerçek: web reposu `../minik-usta` (`b31c4e8`, yalnız okundu). İncelenen: `Assets/MinikUsta/Core/**` (`7404317`).
Yöntem: `docs/PORT_MAP.md` eşlemesiyle TS ve C# dosyaları yan yana okundu. Her şüphe için önce node 22 ile TS çalışma
zamanında beklenen değer üretildi, sonra C# testi yazıldı. Çekirdek koduna dokunulmadı; yalnız testler, üreticiler ve bu
liste eklendi.

Testler `Assets/MinikUsta/Core.Tests/ReviewPhase2Tests.cs` dosyasında. C#'ta kırmızı olan testler
`[Ignore("REVIEW_PHASE2 R-xx …")]` ile işaretli: takım yeşil kalıyor ama bulgu çalıştırılabilir durumda duruyor. Düzelten
kişi `Ignore` satırını kaldırır ve test değişmeden geçmelidir. Doğrulama komutu (Ignore satırları geçici olarak silinip):
`sed -i '/^        \[Ignore("REVIEW_PHASE2/d' Assets/MinikUsta/Core.Tests/ReviewPhase2Tests.cs && cd dotnet && dotnet test --filter ReviewPhase2Tests`
→ 16 kırmızı, 10 yeşil (inceleme koşusu). Ardından `git checkout` ile Ignore satırlarını geri getirin.

Önem: **Engel** = davranış farkı, oyunda yanlış sonuç · **Önemli** = aynı sprintte kapanmalı · **Öneri** = birikim listesi.

## Özet

| Kimlik | Önem | Yer | Konu | Test |
| --- | --- | --- | --- | --- |
| R-01 | Önemli | `Level/LevelData.cs` (`PlanRow`, `ShapeId`, `PieceRef`, `TutTextKey`, `Highlight`), `Level/Logic.cs`, `Obstacles/Registry.cs` | .NET `$` sondaki `\n`'den önce de eşleşiyor (JS'te yalnız girdinin sonunda) | `K45_a_trailing_newline_fails_the_schema_like_zod` (4 durum) |
| R-02 | Önemli | aynı düzenli ifadeler + `Logic.cs` `int.Parse` (satır 1239–1247, 1415–1416, 1531) | .NET `\d` her Unicode rakamıyla eşleşiyor (JS'te `[0-9]`); doğrulayıcı `FormatException` fırlatıyor | `K45_non_ASCII_digits_fail_the_schema_without_throwing` (5 durum) |
| R-03 | Öneri | `Level/LevelJson.cs` `IntAny` / `Int` | ±(2^53 − 1) dışındaki tamsayılarda zod 4 `too_big`/`too_small` verip sınır denetimine devam ediyor; C# `invalid_type` veriyor | `K45_unsafe_integers_report_too_big_like_zod_4` |
| R-04 | Öneri | `Json/JsonWriter.cs` (`Canonical`, `Write`) | JS nesneleri dizi indeksi olan anahtarları ("0" … "4294967294") önce ve artan sırada dolaşıyor; C# tüm anahtarları UTF-16 sırasıyla diziyor | `K43_canonical_json_orders_array_index_keys_like_a_JS_object` |
| R-05 | Öneri (koruma yeşil) | `Json/JsonWriter.cs` `JsNumber`, `Json/JsonReader.cs` | `"R"` biçimi ve `double.TryParse` en kısa / doğru yuvarlamayı yalnız .NET Core 3.0+'ta garanti ediyor; Unity Mono/IL2CPP doğrulanmadı | `K35_K43_JsNumber_format_…`, `K35_K43_JsonReader_…`, `K35_K43_string_quoting_…` |
| R-06 | Önemli (test altyapısı; çekirdek yeşil) | `Core.Tests/*VectorTests.cs`, `DiffFuzzTests.cs`, `FullCoverTests.cs` | Test düzeneği kültüre bağlı: fi-FI / sv-SE / ar-SA yerel ayarında 1 420 testin 599'u kırmızı | çekirdek koruması: `K35_K43_K45_core_outputs_do_not_depend_on_CurrentCulture` |
| R-07 | Önemli | `Deadlock.cs` (`MemoLo/MemoHi/MemoGen`, `memoGeneration`, `memoUsed`, `D3aStats.Expansions`) | D3a bellek tablosu süreç genelinde ve `static`: paralel `TileRemaining` çağrıları birbirinin girdilerini okuyor ve yanlış `dead`/`ok` dönüyor | `K30_D3a_tile_remaining_is_thread_safe` |
| R-08 | — (koruma yeşil) | `Movement.cs` `DragSession.Follow/Nearest` | K-08 sürekli işaretçi takibi fuzz'da hiç denenmemişti (fuzz yalnız `moveTo`) | `K08_K05_K44_follow_under_a_continuous_pointer_equals_TS` |
| R-09 | Öneri | `Coords.cs` `CellY`, `DragNodeFromCode` | Negatif girdide TS `Math.floor`, C# sıfıra doğru kırpan tamsayı bölmesi kullanıyor | `K01_cell_and_node_decoding_floor_negative_inputs_like_TS` |
| R-10 | Öneri | `Shapes.cs` `AlwaysHeavy`, `ForbiddenShapes`; `Level/Logic.cs` `HardLevels`, `SuperhardLevels` | Kural tabloları dışarıya açık ve değiştirilebilir `HashSet` (TS'de dondurulmuş) | `K44_K02_rule_tables_are_read_only` |
| R-11 | Öneri | `Json/JsonReader.cs` `MaxDepth = 256` | 300 seviye iç içe dizi node'da okunuyor, C#'ta "nesting too deep" hatası veriyor | `K43_deep_nesting_parses_like_JSON_parse` |
| R-12 | Öneri | `Level/LevelJson.cs` `Pattern` | zod en mesajı `/^…$/` biçiminde, C# mesajı eğik çizgisiz `^…$` | `K45_regex_schema_message_equals_zod_en_locale` |
| R-13 | — (koruma yeşil) | `Deadlock.cs` `TileRemaining` | D3a açılım sayacı yalnız küçük sahalarda (en çok 16) karşılaştırılmıştı; büyük aramalarda ve bütçe sınırında da eşleşiyor | `K30_D3a_result_and_expansions_equal_TS_on_large_searches` |
| T-01 | Önemli (web, TS hatası) | web `src/core/deadlock.ts` `reshuffleYard` | Her denemenin başındaki `s.buf.set(start)` RNG başlığını da sıfırlıyor; 1.–7. denemeler aynı karıştırmayı üretiyor (P4'ten devralındı, burada doğrulandı) | mevcut P4 D1-RNG senaryoları |

Engel bulunmadı. Oyun hattında (hamle, olay, karma, kayıt/devam) 10 374 fuzz eylemi, 3 648 sürekli takip adımı ve 120 büyük
D3a araması TS ile bit bit aynı çıktı. Sapmaların hepsi geçersiz veri, eşzamanlılık, yerel ayar ya da bugün kullanılmayan
kenar durumlarda.

## Bulgular

### R-01 (Önemli): `$` sondaki satır sonunu kabul ediyor

- **TS:** `schema.ts` `PLAN_ROW`, `ShapeIdSchema`, `HIGHLIGHT_REGEX`, `PIECE_REF_REGEX`, `TUT_TEXT_KEY_REGEX`. JS'te `/^…$/`
  (m bayrağı yok) yalnız girdinin sonunda eşleşir.
- **C#:** `new Regex("^…$")` → .NET'te `$`, sondaki `\n`'den önce de eşleşir. `RegexOptions.ECMAScript` bunu
  değiştirmiyor (denendi).
- **Tekrar üretme** (level_001, ilk eşleşme düzenlendi; TS = `loadLevel`):
  - plan satırı `"WW\n"` → TS `schema L-01/schema_invalid@build.segments[0].rows[0]`, C# `logic L-05/plan_size`;
  - şekil `"D2_0\n"` → TS şema hatası, C# `LoadLevel` **`ArgumentOutOfRangeException: unknown shape id`** fırlatıyor;
  - vurgu `"piece:0\n"` ve textKey `"tut.m.lift\n"` → TS reddediyor, C# bölümü **yüklüyor** (`ok`).
- **Düzeltme:** Tüm çekirdek düzenli ifadelerinde `$` yerine `\z` yazın, `\d` yerine `[0-9]` yazın (ya da
  `RegexOptions.ECMAScript` ile birlikte `\z` kullanın). Hata mesajları TS desen metnini taşımalı (R-12): görünen metni ayrı
  bir sabitte tutun. Kapsam: `LevelData.cs` (Highlight, PieceRef, TutTextKey, PlanRow, CargoShape, ShapeId), `Logic.cs`
  (PlanRowChars, TruckRef, StartRef, BatchArg, Digits), `Registry.cs` (InfoKeyFormat), `Ascii.cs` (HeaderRe, SizeRe, RowRe).
  `Ascii.cs` için ayrıca .NET `\s` ≠ JS `\s` (`\x85` .NET'te var JS'te yok, `﻿` JS'te var .NET'te yok).

### R-02 (Önemli): `\d` Unicode rakamlarını kabul ediyor, doğrulayıcı çöküyor

- **TS:** `/\d/` = `[0-9]`; `Number(arg)`.
- **C#:** .NET `\d` = `\p{Nd}` (Arapça-Hint `٠`, Devanagari vb.). Ardından `int.Parse("٠")` → `FormatException`.
- **Tekrar üretme** (`Logic.ValidateLevelJson(json, { fileId: 1 })`): vurgu `piece:٠`, `done.piece` `piece:٠` ve
  `piece:k1_٠` → TS `none L-01/schema_invalid@…`, C# **`THROW FormatException`**. `debris:٠` ve textKey
  `tut.l١.lift` → TS reddediyor, C# kabul ediyor (`level`). Çalışma zamanı `LoadLevel` bu durumları kabul ediyor.
- **Düzeltme:** R-01 ile aynı (`[0-9]`). Ek olarak `int.Parse` çağrılarına `CultureInfo.InvariantCulture` verin ve bunları
  yalnız `[0-9]` ile doğrulanmış girdiye uygulayın.

### R-03 (Öneri): güvenli tamsayı aralığı dışı

- **TS (zod 4.6.5, `z.config(en())`):** `yard.cols = 1e20` iki sorun veriyor: `too_big` "Too big: expected int to be
  <=9007199254740991" ve `too_big` "Too big: expected number to be <=16". `seed = 2^53` → `too_big`, `seed = −2^53` →
  `too_small` "…>=-9007199254740991".
- **C#:** `IsInteger` |n| > 2^53 − 1 için false dönüyor → tek sorun: `invalid_type` "expected int, received number".
- Etki: `LoadLevel` iki tarafta da `schema` aşamasında reddediyor. Yalnız sorun kodu, mesajı ve sayısı farklı.
- **Düzeltme:** `IntAny`/`Int` içinde tamsayı olup güvenli aralık dışında kalan değerlere zod sırasıyla `too_big`/`too_small`
  sorunu ekleyin ve `minimum`/`maximum` denetimini sürdürün.

### R-04 (Öneri): dizi indeksi anahtarlarının sırası

- **TS:** `sortKeys` sıralı anahtarları yeni bir nesneye yazıyor. JS nesnesi dizi indeksi anahtarlarını (2^32 − 1'den küçük
  kanonik tamsayı dizgeleri) önce ve sayısal artan sırada dolaşıyor:
  `canonicalJson({"10":1,"2":2,"b":3,"a":4})` = `{"2":2,"10":1,"a":4,"b":3}`. `JSON.stringify(JSON.parse('{"b":1,"1":2}'))`
  = `{"1":2,"b":1}`.
- **C#:** `{"10":1,"2":2,"a":4,"b":3}` ve `{"b":1,"1":2}`.
- Etki: Bugün kanonik JSON'a giren yapıların (olaylar, `LevelData`, eylem günlüğü, `BoosterAppliedEvent.Detail`) hiçbirinde
  sayısal anahtar yok (`Record<…>` taraması yapıldı). Ham JSON alan `GameSession.LevelHash(JsonValue)` ya da ileride sayısal
  anahtarlı bir `detail` (örneğin parça id'sine göre harita) `levelHash`/`eventLogHash` sapması üretir.
- **Düzeltme:** `JsonWriter`'da anahtarları iki gruba ayırın: önce dizi indeksleri (sayısal artan), sonra diğerleri
  (`Canonical`'da sıralı, `Write`'ta ekleme sırasında).

### R-05 (Öneri, net8'de yeşil): sayı biçimi ve ayrıştırma Unity'de doğrulanmalı

- `JsNumber.Format`, `double.ToString("R")`'nin en kısa gidiş-dönüş biçimi olduğuna dayanıyor. Bu yalnız .NET Core 3.0+'ta
  garanti. .NET Framework / eski Mono'da `"R"` bazı değerlerde 17 hane ya da gidiş-dönüşü bozuk çıktı veriyordu.
  `JsonReader` ise `double.TryParse`'ın doğru yuvarladığına ve `1e400` → ∞ verdiğine dayanıyor (eski Mono'da taşma `false`
  dönüyordu, bu da "bad number" hatası demek).
- Kanonik JSON'a giren kesirli sayılar: `targets.deadRate` (`levelHash`), YAO. 401 çift (kenar durumlar, rastgele bit
  desenleri, oranlar), 300 ondalık metin ve 200 dize (vekil çiftler, tek vekiller, ` `, BOM, Türkçe harfler) TS
  çıktısıyla karşılaştırılıyor. Hepsi .NET 8'de yeşil.
- **Düzeltme / adım:** Bu üç testi Unity EditMode'da ve bir IL2CPP cihaz derlemesinde (Android ARM64) koşun. Kırmızıysa
  `JsNumber` için `"R"`'a dayanmayan bir en kısa hane algoritması (Ryu / Grisu) yazın.

### R-06 (Önemli, test altyapısı): test düzeneği yerel ayara bağlı

- **Tekrar üretme:** `cd dotnet && LC_ALL=fi_FI.UTF-8 LANG=fi_FI.UTF-8 dotnet test --no-build` → **599/1 420 kırmızı**
  (ar_SA.UTF-8'de de aynı). tr_TR'de yeşil. Neden: test yardımcılarındaki `string.Join(",", s.Buf)` ve `"cargo/" + -1`
  gibi ifadeler negatif sayıları kültürün eksi işaretiyle yazıyor (fi/sv/nb: U+2212 `−`, ar: U+061C + `-`). Örnekler:
  `P3VectorTests.BufHash`, `P2VectorTests`, `DiffFuzzTests` (tampon karması `b`), `FullCoverTests.K44_…`.
- **Çekirdek temiz:** `K35_K43_K45_core_outputs_do_not_depend_on_CurrentCulture`, 10 altın oynatmanın olay karmasını,
  Zobrist karmasını, ASCII'sini, olay ve günlük JSON'unu, 70 fikstürün doğrulayıcı mesajlarını ve `JsNumber`'ı fi-FI, ar-SA,
  tr-TR ve sv-SE altında değişmez kültürle karşılaştırıyor. Hepsi aynı çıktı. (Unity cihazda `CurrentCulture` cihaz dilini
  izler; bu test bir gerilemeyi yakalar.)
- **Düzeltme (doğrulandı):** Test derlemesine tek satır ekleyin: `[assembly: NUnit.Framework.SetCulture("")]`. Bu
  satırla fi_FI altında 1 446 testin 0'ı kırmızı. Ya da yardımcılarda `ToString(CultureInfo.InvariantCulture)` kullanın.
  Unity Test Framework'te (NUnit 3.5) derleme düzeyi `SetCulture` doğrulanmadı.

### R-07 (Önemli): D3a bellek tablosu iş parçacığı güvenli değil

- **TS:** `deadlock.ts` modül düzeyinde `memoLo/memoHi/memoGen`, `memoGeneration`, `D3A_STATS` var. JS tek iş parçacıklı
  olduğu için bu güvenli.
- **C#:** `Deadlock.cs:374–378` `static` diziler ve sayaçlar, `D3aStats.Expansions` `static`. `Movement.cs` kendi
  karalama dizilerini `[ThreadStatic]` yapmış, `Deadlock` yapmamış. Paralel çağrılar `memoGeneration`'ı paylaşıyor, bir
  aramanın "başarısız" işaretini öbürü okuyor ve dalı yanlışlıkla budayabiliyor.
- **Tekrar üretme:** 187 durum (Bölüm 1–10 + blok yeniden boyamaları) + 120 büyük D3a durumu, 8 iş parçacığı, `Parallel.For`.
  İnceleme konsol koşusunda 14 960 çağrıdan 58'i, testte 11–15'i sıralı sonuçtan farklı çıktı.
- Etki: Ana iş parçacığında oyun etkilenmiyor. C#'a taşınacak `levels:solve`, oyun botu (acemi/orta/usta × 500) ya da arka
  plan ipucu hesabı paralel koşarsa K-30 tespiti ve Söküm yanlış olur. Belirlenimcilik ilkesi bozulur.
- **Düzeltme:** Bellek tablosunu ve sayaçları `[ThreadStatic]` yapın (diziyi ilk kullanımda ayırın) ya da çağrı başına bir
  `D3aMemo` bağlamı geçirin. `D3aStats.Expansions` için de aynısı. Ardından `Ignore`'u kaldırın.

### R-09 (Öneri): negatif hücre/düğüm kodu

- **TS:** `cellY(-1) = -1`, `cellY(-9) = -2`, `dragNodeFromCode(-1) = {ix:-1, iy:-1, mode:-1}`, `(-81) = {-1, -1, -2}`.
- **C#:** `CellY(-1) = 0`, `CellY(-9) = -1`, `DragNodeFromCode(-1) = {ix:-1, iy:0, mode:0}`.
- Etki: Bugün negatif girdiyle çağıran yok (çağıranlar −1 kodunu önceden ayıklıyor). Herkese açık API, ileride ayıklama
  paneli ya da bot yanlış düğüm çözer.
- **Düzeltme:** Taban bölme kullanın (`FloorDiv(a, b)`) ya da negatif girdide `ArgumentOutOfRangeException` fırlatıp TS'ye de
  aynı koşulu ekletin.

### R-10 (Öneri): değiştirilebilir kural tabloları

- `Shapes.AlwaysHeavy`, `Shapes.ForbiddenShapes`, `Logic.HardLevels`, `Logic.SuperhardLevels` `public static readonly
  HashSet<…>`: başvuru salt okunur ama içerik değiştirilebilir. TS karşılıkları `Readonly…`/`Object.freeze`.
- **Düzeltme:** `IReadOnlyCollection<T>` (ya da `ReadOnlySet`/özel sarmalayıcı) olarak açın. `ConditionalWeakTable`
  önbellekleri ve `Registry` zaten güvenli.

### R-11 (Öneri): iç içe derinlik sınırı

- `JsonReader.MaxDepth = 256`, `JSON.parse` 300 seviyeyi okuyor. Bölüm ve kayıt verisi bu derinliğe ulaşmıyor. Sınır, kötü
  niyetli veriye karşı yığın taşmasını önlüyor.
- **Düzeltme:** Sınırı belgeleyin (TECH tasarımına "JSON en fazla 256 seviye" maddesi) ya da yineleme yerine açık bir
  yığın kullanıp V8'in fiili sınırına yaklaşın. Testi sınıra göre güncelleyin.

### R-12 (Öneri): düzenli ifade mesajı

- zod en: `Invalid string: must match pattern /^[WYGROCBP.?]{1,4}$/`; C#: `… pattern ^[WYGROCBP.?]{1,4}$`. Oyunun
  `src/` çalışma zamanında zod yereli yüklenmediği için TS mesajı yalnız "Invalid input". C# `tools/validate-levels.ts`'in en
  yerelini izliyor. Kod ve yol aynı.
- **Düzeltme:** `Pattern` mesajında `"/" + kaynak + "/"` yazın (R-01 düzeltmesinden sonra kaynak metin TS ile aynı kalmalı).

### T-01 (Önemli, web reposu): D1 karıştırma denemeleri aynı

- `src/core/deadlock.ts` `reshuffleYard`: `s.buf.set(start)` RNG başlığını (`H.rng`, `bufferRng(s.buf, H.rng)`) da geri
  yüklüyor. Bu yüzden 1.–7. denemeler aynı karıştırmayı üretiyor; `RESHUFFLE_ATTEMPTS = 8` fiilen 2 farklı deneme demek.
  C# birebir taşıyor. TS düzeltilirse `RULES_VERSION` artmalı, P2/P3 vektörleri ve kehanet yeniden üretilmeli.
- Önerilen kayıt: `[code-lead → product-lead] D1 reshuffle denemeleri` (REVIEW_LOG, web).

## Sapma çıkmayan şüpheler (kanıtla kapatıldı)

- **Tamsayı/işaret:** `mulberry32`, `splitmix32`, `fmix32`, `hash32`, D3a anahtarı (`exact` < 2^52, `k % 2^32 | 0`,
  `Math.floor(k / 2^32) | 0`), karışım sabitleri (`0x9e3779b9 ^ segPos` → `unchecked((int)0x9e3779b9u)`), `Lcm` (parçalar
  shutter 2·period ≤ 8, slider ≤ 14, carousel ≤ 30, asansör ≤ 6; sonuç int'e sığıyor), `seed | 0` (long → int kırpma = ToInt32; `−2^40 − 7` → `−7`, testte sabitlendi).
- **Sıralama kararlılığı:** 14 TS `sort` çağrısının C# karşılıkları eşitlikte id ile tam sıralı ya da kararlı ekleme
  sıralaması kullanıyor (`StableSort`, `DeliveryOrder`, `MaterialShortfall`, `Registry` kural sırası).
- **Yineleme sırası:** Çekirdekte `Dictionary`/`HashSet` üzerinden silme yok (ekleme sırası korunuyor). Dize sıralamaları
  `CompareOrdinal`.
- **Varsayılanlar:** shutter `phase` 0, slider `dir` 1, Ağır Yük rengi `W`, boş `flags`/`dropColumns`/`debris`/`tutorial`,
  `hand.path` yok/boş, `site`/`yard.cols` yok, Unicode ad, sıralaması değişmiş anahtarlar: 34 bölüm çeşidinde `levelHash`,
  `loadLevel` sonucu/sorun kodları, başlangıç tamponu, Zobrist, ASCII, `cycle`, adım 10 sırası ve başlangıç olayları TS ile
  aynı (inceleme sondası; vektör dosyası eklenmedi, P2/P3 bunları kısmen kapsıyor).
- **JSON okuyucu:** yinelenen anahtar (ilk konum, son değer), `\uXXXX` ve vekil çiftler, tek vekiller, `-0`, `1E2`, `1e400`
  → `null`, 2^53'ten büyük tamsayılar: testlerle sabitlendi.
- **K-08 sürekli takip (R-08):** 114 sürükleme × 32 adım (çeyrek hücre ızgarası: tam eşitlikler; serbest noktalar; uzak
  sıçramalar). En yakın düğüm, histerezis, eşitlik sırası (1)–(4), yol, `crossedWall`/`enteredRail`/`blockedByWallHeight`/
  `blockedCargo` ve K-07 sınıfı TS ile aynı.
- **D3a sayacı (R-13):** 24 rastgele 4 × 8 iki segmentli saha (120 durum; ok 28, dead 46, unknown 46), 5 bütçe. Sonuç ve
  açılım sayısı düğüm düğüm aynı, 20 000 bütçe sınırında da.

## Üreticiler

| Çıktı | Komut |
| --- | --- |
| `Core.Tests/Golden/review_json_vectors.json` (54 KB) | `node tools/review/gen-json-vectors.ts ../minik-usta > Assets/MinikUsta/Core.Tests/Golden/review_json_vectors.json` |
| `Core.Tests/Golden/review_follow_vectors.json` (240 KB) | `node tools/review/gen-follow-vectors.ts ../minik-usta > Assets/MinikUsta/Core.Tests/Golden/review_follow_vectors.json` |
| `Core.Tests/Golden/review_d3a_vectors.json` (57 KB) | `node tools/review/gen-d3a-vectors.ts ../minik-usta > Assets/MinikUsta/Core.Tests/Golden/review_d3a_vectors.json` |

Satır içi beklenen değerler (R-01–R-04, R-09, R-11, R-12) test yorumlarında TS sonda ifadesiyle birlikte duruyor.

## Doğrulanmayanlar

- Unity Editor EditMode ve IL2CPP cihaz koşusu yapılmadı (R-05 ve R-08 asıl orada anlam kazanıyor). IL2CPP'nin C++
  derleyicisi ARM64'te `dx * dx + dy * dy` ifadesini FMA'ya daraltabilir. K-08 `D2Epsilon = 1e-9` bunu büyük ölçüde
  soğuruyor ama tam eşitlik noktalarında fark yalnız cihazda görülür.
- `[assembly: SetCulture("")]` yalnız NUnit 3.14 / .NET 8'de denendi.
- Faz 3 engelleri (W4–W8, Y1/Y2/Y4/Y6, S2–S9) için kural kancası yok (`Registry` yalnız W1, Y3, S1). Bu yollar iki tarafta da
  yalnız birim testleriyle sınanıyor; fuzz ve bu inceleme onlara ulaşmıyor.
- `services/save.ts` (`SessionActionSchema` doğrulaması) taşınmadı. C# `SessionActionJson.FromJson` bozuk kayıtta TS'den farklı
  hata verebilir; incelenmedi.
