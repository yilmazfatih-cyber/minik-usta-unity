# Minik Usta — Unity taşıması

Minik Usta'nın (dikey ekranlı blok yerleştirme bulmacası) Unity 6 sürümü. Kaynak gerçek **web reposudur**
(`yilmazfatih-cyber/minik-usta`, yerelde `../minik-usta`): `src/core/**` (11 400 satır saf TypeScript), `docs/GDD.md`
(K-01…K-54), `docs/TECH_DESIGN.md` §2–§6 ve §2R, `levels/*.json`, `tests/golden/*.json`. Bu repo o davranışı C#'a
**satır satır** taşır; yeni mekanik icat edilmez, belirsizlikte TS kodu esastır.

## Bağlayıcı kararlar (web reposu `docs/DECISIONS.md`)

| Karar | Özet |
| ----- | ---- |
| D-091 | Mevcut mekanik birebir taşınır; **match-3 yok**. Brifteki match-3 maddeleri karşılıklarına çevrilir (sürükleme hissi, kötü geçiş geri sekmesi, şantiye düşüşü, kilit parlaması, Altın Mala, kamyon teslimatı, takılma nabzı, Bonus İnşaat). |
| D-092 | Kahraman **Tuna** kalır (D-044, D-087); "genç kral" uygulanmaz. |
| D-093 | Çekirdek **saf C#** (UnityEngine bağımlılığı yok); bulutta .NET SDK + NUnit ile derlenir ve test edilir, aynı test dosyaları Unity Test Framework (EditMode) altında çalışır. Görünüm katmanı proje sahibinin Unity kurulumunda derlenir. |
| D-094 | Unity projesi bu ayrı repoda yaşar; web (Phaser) sürümü `main` = de6f16c olarak yayında kalır, görsel v3 web'de dondurulur. |

Faz 1 raporu: web reposunda `docs/UNITY_PORT_FAZ1.md`. TS → C# dosya eşlemesi: [`docs/PORT_MAP.md`](docs/PORT_MAP.md).

## Proje yapısı

```
/
├─ Assets/
│  ├─ MinikUsta/
│  │  ├─ Core/                     saf C# çekirdek (asmdef: noEngineReferences = true, autoReferenced)
│  │  │  ├─ Types.cs Geometry.cs Coords.cs Shapes.cs Rng.cs Hash.cs Fnv1a.cs State.cs Grid.cs Ascii.cs
│  │  │  ├─ Json/                  bağımlılıksız JSON okuyucu/yazıcı (JsonValue, JsonReader, JsonWriter + canonicalJson)
│  │  │  └─ Level/                 LevelData (şema modeli), LevelJson (zod şemasının elle yazılmış karşılığı), Compile, Plan, Mechanics
│  │  └─ Core.Tests/               NUnit 3 testleri (Editor platformu; Unity EditMode ile aynı dosyalar)
│  │     ├─ Fixtures/              Builders (tests/fixtures/builders.ts portu), J (JSON literal), TestPaths
│  │     └─ Golden/                web reposundan kopya: level_NNN.hand.json, level_NNN.solver.json, p0_vectors.json
│  └─ StreamingAssets/MinikUsta/levels/   level_001…010.json (web reposundan birebir kopya)
├─ dotnet/                         Unity dışı derleme ve test (D-093)
│  ├─ MinikUsta.sln
│  ├─ MinikUsta.Core/              Assets/MinikUsta/Core/**/*.cs → netstandard2.1, LangVersion 9, Nullable, TreatWarningsAsErrors
│  └─ MinikUsta.Core.Tests/        Assets/MinikUsta/Core.Tests/**/*.cs → net8.0, NUnit 3.14 + NUnit3TestAdapter + Microsoft.NET.Test.Sdk
├─ tools/
│  ├─ sync-from-web.sh             levels/*.json ve tests/golden/*.json dosyalarını web reposundan kopyalar
│  └─ gen-p0-vectors.ts            TS çekirdeğinden bit-eşitlik vektörleri üretir (node 22, ek paket yok)
└─ docs/PORT_MAP.md                TS dosyası → C# dosyası eşleme tablosu (her paket günceller)
```

`ProjectSettings/` ve `Packages/manifest.json` henüz yok: Unity projesini proje sahibi Unity 6 ile bu klasörde açar
(Unity eksik klasörleri üretir); `.gitignore` Unity'nin ürettiği `Library/`, `Temp/`, `Logs/` vb. klasörleri dışlar.

## Komutlar

| Komut | Ne yapar |
| ----- | -------- |
| `cd dotnet && dotnet build -warnaserror` | Çekirdek + testleri derler; uyarı = hata |
| `cd dotnet && dotnet test` | Tüm NUnit testlerini koşturur (bölüm ve altın JSON'ları çıktıya kopyalanır, testler repo kökünü `Assets/MinikUsta/Core/MinikUsta.Core.asmdef` ile bulur) |
| `tools/sync-from-web.sh [/yol/minik-usta]` | Bölüm ve altın dosyalarını web reposundan yeniler (varsayılan `../minik-usta`) |
| `node tools/gen-p0-vectors.ts [/yol/minik-usta]` | `Core.Tests/Golden/p0_vectors.json` dosyasını TS çekirdeğinden yeniden üretir |

Unity içinde: Window → General → Test Runner → EditMode; `MinikUsta.Core.Tests` asmdef'i `UNITY_INCLUDE_TESTS` tanımı ve
`nunit.framework.dll` referansıyla aynı dosyaları koşturur.

## Taşıma kuralları

- **Saf C#:** `Assets/MinikUsta/Core` içinde `UnityEngine`, `System.Text.Json`, `Newtonsoft` yok. JSON için Core içindeki
  küçük okuyucu kullanılır.
- **Unity 6 uyumu:** LangVersion 9 (file-scoped namespace, global using, record struct, required üye yok);
  `#nullable enable`. Core csproj hedefi **netstandard2.1**: Unity'nin sunduğu API yüzeyi; net8.0'a özel bir API
  yanlışlıkla kullanılamaz (test projesi net8.0'dır).
- **Belirlenimcilik:** 32-bit tamsayı aritmetiği TS ile bit bit aynı (`Math.imul` → `unchecked(a * b)`, `>>>` →
  `(int)((uint)x >> n)`, `|0` → `unchecked((int)…)`); mulberry32, splitmix32, fmix32, Zobrist tabloları ve FNV-1a 64
  (`ulong`) birebir; karma dizeleri TS ile aynı hex biçiminde. `p0_vectors.json` bunu test eder.
- **Kural atıfları:** Her K-xx kuralı yorumlarda ve test adlarında (`K49_…`) korunur.
- **Veri:** Aynı `level_NNN.json` dosyaları değiştirilmeden okunur; geçersiz bölüm oyuna yüklenmez (şema + mantık
  denetimleri, K-45).
