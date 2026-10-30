# TS → C# eşleme tablosu (PORT_MAP)

Kaynak gerçek: web reposu `src/core/**` (`../minik-usta`, commit `4ce980f`). Her paket bu tabloyu günceller.
Durum: **P0** = bu pakette taşındı ve test edildi · **sonraki** = henüz taşınmadı (önerilen paket parantezde).

## Çekirdek (`Assets/MinikUsta/Core`)

| TS dosyası | Satır | C# dosyası | Durum | Not |
| --- | ---: | --- | --- | --- |
| `types.ts` | 326 | `Types.cs` | P0 | `ColorCodes`, `ShapeKinds`, `Rotations`, `Zone` (int sabitler), `Anchor`, `DragNode`, `DragMode.Free`, `PieceFlags`, `At` (readonly struct: zone + x + y), `SessionAction` (abstract) → `Move` (`MoveKind` enum + alt sınıflar), `UndoAction`, `StartAction`; `GameEvent` (abstract, `EventKind` enum + alt sınıflar); `VerdictReasons.All` (K-34 sırası) |
| `geometry.ts` | 171 | `Geometry.cs` | P0 | `Geometry.MaxCols/MaxRows/CraneRows/MaxBoardRows/MaxSiteCols/SegmentSlots/MaxSegments`, `GeoParams` (struct), `BoardGeo` (sealed, readonly alanlar, `SameAs`), `MakeGeo`, `GeoFromLevel(LevelData)`, `HasDefaultBoard`, `GeoLabel`, `DefaultSizes`, `DefaultGeo` |
| `coords.ts` | 166 | `Coords.cs` | P0 | `CellIndex/CellX/CellY`, `DragNodeCode/DragNodeFromCode`, `InGrid/OnBoard/IsYardCell/IsYardAir/IsSiteCell/IsSiteAir/IsCraneCell/SideOf/StraddlesBoundary`, `RowRangeMask/OpenFreeMask/OpenRailMask/ClosedBoundaryMask/BoundaryAllows`, `Neighbors4` |
| `shapes.ts` | 240 | `Shapes.cs` | P0 | `Cell`, `ShapeDef` (Index, Id, Kind, Rotation, Cells, W, H, CellCount, Rows, ColRows, ColBottom, ColTop, Heavy, Canonical, CanonicalIndex), `Shapes.All` (52), `ShapeById/ShapeByIndex/IsShapeId/RotateShapeId/RotateClockwise`, `AlwaysHeavy`, `ForbiddenShapes`, `KindFirstChapter`, `HeavyMinLevelId` |
| `rng.ts` | 121 | `Rng.cs` | P0 | `IRng` (NextU32/Next/NextInt), `Mulberry32(seed)`, `BufferRng(int[] buf, int index)`, `Splitmix32(seed)`, `Rng.Mulberry32Next/Mulberry32Output/Fmix32/Hash32` (`Hash32Version = "fmix32-chain-v1"`); `Step/ToUnit/ToInt` internal |
| `hash.ts` | 163 | `Hash.cs` | P0 | `ZobristTables` (Pos0/Pos1/Field0/Field1 uint[], Cycle), `Hash.ZobristSeed/PosSite/PosQueue`, `PiecePosition`, `HashedFieldOffsets`, `BuildZobrist`, `HashState(s, uint[]?)`, `HashHex`, `Lcm` |
| `moves.ts` → `fnv1a64`, `canonicalJson` | (945) | `Fnv1a.cs`, `Json/JsonWriter.cs` (`Canonical`) | P0 (kısmi) | Yalnız karma yardımcıları taşındı; `applyMove`, `ArraySink`, `eventLogHash` hattı **sonraki** (K-35 hattı paketi) |
| `state.ts` | 453 | `State.cs` | P0 | `H` (18 başlık alanı), `PF` (9 alanlı kayıt), `GF`, `OF`, `FlagBit`, `StateCounts`, `StateLayout`, `GameState(Lvl, Buf)`, `PieceView`, `State.ComputeLayout/Hdr/SetHdr/PieceField/…/YardOcc/SiteOcc/FilledMask/WrongOccMask/GapField/ObstacleField/HiddenCollected/GoalValue/RevealedMask/QueueIds/EnqueuePiece/RemoveQueueAt/PlanAreaMask/CreateInitialState/CloneState/EncodeState/DecodeState` |
| `grid.ts` | 293 | `Grid.cs` | P0 | `BoardCell`, `Grid.VisibleSegment/PieceBoardCells/OccupyPiece/VacatePiece/RefreshSiteMasks/CollisionMasks/SiteColumnTops/StateInvariantErrors/PlanColorAt` |
| `ascii.ts` | 411 | `Ascii.cs` | P0 | `AsciiSize`, `AsciiBoard`, `Ascii.AirChar`, `WallChar`, `ToAscii(s, ids)`, `ParseAscii`, `FromAscii(lvl, text)` |
| — (zod yerine) | — | `Json/JsonValue.cs`, `Json/JsonReader.cs`, `Json/JsonWriter.cs` | P0 | Bağımlılıksız JSON: sayı/dize/dizi/nesne/bool/null; anahtar sırası korunur; `JsonWriter.Canonical` = TS `canonicalJson`; sayı biçimi ECMAScript `Number.prototype.toString` |
| `level/schema.ts` | 400 | `Level/LevelData.cs`, `Level/LevelJson.cs` | P0 (ön alım) | `LevelJson.Parse(json, issues)` / `ParseOrThrow`; zod varsayılanları ve hata kodları birebir |
| `level/compile.ts` | 462 | `Level/Compile.cs` | P0 (ön alım) | `Compiler.Compile(LevelData) → CompiledLevel` (Geo, Layout, Zobrist, Segments, Pieces, Gaps, Batches, Obstacles, Supply/Demand, Mechanics, Step10, Cycle, TutorialPieceIds), `GravityProfiles`, `Step10Timers`, `CycleLength`, `PieceClassOf` |
| `level/plan.ts` | 153 | `Level/Plan.cs` | P0 (ön alım) | `Plan.BuildPlans(level, geo) → PlansResult`, `Plan.LocalIndex(sx, sy, ws)`, karakter sabitleri `Plan.None/Dot/Hidden` |
| `level/mechanics.ts` | 97 | `Level/Mechanics.cs` | P0 (ön alım) | `Mechanics.Table`, `DeriveMechanics`, `NewMechanics`, `IsCargo`, `BoardHeight` |
| `level/logic.ts` | 1585 | — | sonraki (bölüm doğrulama paketi) | K-45 35 hata kodu, `loadLevel`; `CompileTests` içindeki "loadLevel refuses invalid levels" testi bununla gelir |
| `movement.ts` | 767 | — | sonraki (hareket paketi) | BFS + yapışkan takip, `tryBeginDrag`, `DragSession`, `DropClass`, `isCargoShape` |
| `placement.ts` | 648 | — | sonraki (hareket paketi) | `movePiece`, `dropIntoYard`, `yardDropRow`, `returnTarget` |
| `gravity.ts` | 605 | — | sonraki (hareket paketi) | şantiye/saha yerçekimi, G profilleri |
| `delivery.ts` | 107 | — | sonraki (hareket paketi) | `deliverQueue`, `deliveryLanding` |
| `site.ts` | 82 | — | sonraki (hareket paketi) | dilim kayması S1 |
| `obstacles/*.ts` (registry, types, W1, Y3, S1) | 745 | — | sonraki (engel paketi) | eklenti arayüzü; çekirdeğe engele özel if yığını eklenmez |
| `moves.ts` (geri kalan) | 945 | — | sonraki (K-35 hattı paketi) | `applyMove`, `MoveResult`, olay akışı, `eventLogHash` |
| `goals.ts`, `combo.ts`, `summary.ts`, `panorama.ts` | 678 | — | sonraki (K-35 hattı paketi) | |
| `deadlock.ts` | 866 | — | sonraki (çıkmaz paketi) | D1/D2/D3a, Söküm |
| `boosters.ts` | 486 | — | sonraki (güçlendirici paketi) | |
| `session.ts` | 439 | — | sonraki (oturum paketi) | `GameSession.start/replay/commit`, geri al, `RULES_VERSION = 2`; altın `hand.json` eşdeğerlik testi burada kapanır |

## Testler (`Assets/MinikUsta/Core.Tests`)

| TS testi | C# testi | Durum | Not |
| --- | --- | --- | --- |
| `tests/fixtures/builders.ts` | `Fixtures/Builders.cs`, `Fixtures/J.cs` | P0 | `Level/Compiled/InitialState(LevelSpec)`, `ExpectAscii` |
| — | `Fixtures/TestPaths.cs` | P0 | repo kökü, `levels/`, `Golden/`, `p0_vectors.json` erişimi (dotnet ve Unity'de aynı) |
| `types.test.ts` | `TypesTests.cs` | P0 | + 43 olay türü ve oturum eylemi adları |
| `rng.test.ts` | `RngTests.cs` | P0 | + TS bit-eşitlik vektörleri (`p0_vectors.rng`) |
| `hash.test.ts` | `HashTests.cs` | P0 | + 10 bölümün başlangıç karması ve Zobrist tablo başları TS ile bit bit |
| `state.test.ts` | `StateTests.cs` | P0 | |
| `grid.test.ts` | `GridTests.cs` | P0 | + 1 000 adımlı seed'li yürüyüşün karmaları TS ile bit bit |
| `coords.test.ts` | `CoordsTests.cs` | P0 | |
| `ascii.test.ts` | `AsciiTests.cs` | P0 | |
| `shapes.test.ts` | `ShapesTests.cs` | P0 | |
| `level/compile.test.ts` | `CompileTests.cs` | P0 (10/11) | `loadLevel` testi `logic.ts` ile gelir |
| `geometry.test.ts` (K-49, 9 geometri) | `GeometryTests.cs` | P0 (14/23) | geometry, bölgeler/sınır, compile/layout, Zobrist konumları, ASCII gidiş-dönüş, helpers. Hareket/oturum isteyen 9 test (rastgele oyun değişmezleri, kenar modeli ≡ hücre modeli, E-56, E-57, K-44 Ağır Yük) hareket ve oturum paketleriyle |
| `geometry.gate.test.ts` | `GeometryTests.cs` (kapı bölümü) | P0 | Sayı kapısı (5–10 sabitleri gerekçeli); ESLint yarısının karşılığı: tek kalan pre-2R sabiti `State.YardOccRows` `[Obsolete]` ve referanssız. Sonraki paketler `GeometryCore` listesine `Movement.cs, Gravity.cs, Placement.cs, Delivery.cs` ekler |
| — | `JsonTests.cs` | P0 | okuyucu/yazıcı, canonical, FNV-1a vektörleri, 10 bölümün parse→write→parse kararlılığı |
| `tests/golden/hand.test.ts` | — | sonraki (oturum paketi) | `level_NNN.hand.json` adımlarını oynat → `eventLogHash` + son ASCII birebir |

## Veri

| Web | Unity | Araç |
| --- | --- | --- |
| `levels/level_001…010.json` | `Assets/StreamingAssets/MinikUsta/levels/` | `tools/sync-from-web.sh` |
| `tests/golden/*.json` | `Assets/MinikUsta/Core.Tests/Golden/` | `tools/sync-from-web.sh` |
| TS çekirdeği (hesaplanan vektörler) | `Assets/MinikUsta/Core.Tests/Golden/p0_vectors.json` | `node tools/gen-p0-vectors.ts` |
