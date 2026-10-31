# TS → C# eşleme tablosu (PORT_MAP)

Kaynak gerçek: web reposu `src/core/**` (`../minik-usta`, commit `4ce980f`). Her paket bu tabloyu günceller.
Durum: **P0** = iskelet paketinde taşındı · **P1** = hareket paketinde taşındı · **sonraki** = henüz taşınmadı (önerilen paket parantezde).
Her satırdaki C# üye adları `grep` ile doğrulanmıştır.

## Çekirdek (`Assets/MinikUsta/Core`)

| TS dosyası | Satır | C# dosyası | Durum | Not |
| --- | ---: | --- | --- | --- |
| `types.ts` | 326 | `Types.cs` | P0 (+P1) | `ColorCodes`, `ShapeKinds`, `Rotations`, `Zone` (int sabitler, `Zone.IndexOf(name)`), `Anchor`, `DragNode`, `DragMode.Free`, `PieceFlags`, `At` (readonly struct: zone + x + y + seg?), `Verdict` (P1: `IEquatable`, TS `toEqual` eşitliği), `Steer`, `SessionAction` (abstract) → `Move` (`MoveKind` enum + alt sınıflar), `UndoAction`, `StartAction`; `GameEvent` (abstract, `EventKind` enum + alt sınıflar); `VerdictReasons.All` (K-34 sırası); `CancelReasons` |
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
| `movement.ts` | 767 | `Movement.cs` | **P1** | `Movement.Free/RailMode(gap)/NodesPerMode/HysteresisD2/D2Epsilon/IsCargoShape/IsSiteClosed/IsCancelled/TryBeginDrag(s, id, DragRules?) → DragAttempt/BeginDrag/CanPickPiece/BlockCells`; `DragRules(canPick, canPassGap, siteClosed)` (eklenti kancaları, engele özel if yok); `DropKinds` (cancel/yard/siteFree/siteRail), `DropClass {Kind, Reason, Row, Gap; Invalid/SameSpot/Yard/CraneOverYard/Straddle/SiteClosed/SiteFree/SiteRail(gap)}`; `PickFailures` (notOnBoard/hiddenSegment/locked/noMoves/rule/immovable); `DragSession {PieceId, Shape, Start, Cargo, StartNodes, Current, ReachableCount, CanCrossWall, CanEnterRail, Movable; IsReachable, DistanceFromStart, DistanceFromCurrent, ReachableNodes, Neighbours, Nearest(px,py), Follow(px,py) → FollowResult, PathTo, MoveTo, Classify(node?), Cells}`; `FollowResult {Node, Changed, Path, CrossedWall, EnteredRail, BlockedByWallHeight, BlockedCargo}` |
| `gravity.ts` | 605 | `Gravity.cs` | **P1** | `Gravity.ComputeFall(s, id, node, FallOptions?) → FallResult {Mode (free/rail), Landing, Dir, Distance, Path, Drift, Steered, Cells, Verdict, TouchesHidden, Effect}`, `SiteColumnMasks(s, exclude)`, `SiteLandingRow`, `YardBalloonLanding`, `ShadowInfo(fall, difficulty) → ShadowInfo {Tone correct/wrong/neutral, Reasons, MissingSupport, Breaks}`, `SettleYard(s, SettleHooks?) → YardMotion[] {Kind piece/bag, Id, From, To, Dy}`; kancalar `FallRules(modifyFall, onLanded)`, `FallPlan`, `LandingEffect (None / Break(penalty))`, `SettleHooks(onFallen)`, `FallenBlock`; `Bits.Clz32/HighestBit/LowestBit` (TS `Math.clz32`) |
| `placement.ts` | 648 | `Placement.cs` | **P1** | `PiecePlace` (struct: Zone adı, X, Y, Seg; `Yard/Site/Queue/Gone`), `Placement.PiecePlace(s, id)/SitePlace/YardPlace/PlaceAt`, `IsCorrectPlacement(s, id, cells) → Verdict` (K-16 + K-34 kanca 2 sırası), `ReasonCells`, `AllCellsInPlanArea`, `BuildFront(s) → At[]` (K-33), `EligibleTrowelCells`, `IsSegmentComplete` (K-15), `NearestColumnsFirst`, `YardDropRow(geo, shape)`, `DropIntoYard(s, shape, ix, exclude)`, `ReturnTarget(s, id, ReturnOptions(skipStart, start)) → ReturnTarget {Step 1–3, To, DropFrom}` (K-17), `MovePiece`, `LockPiece → RevealedCell[]`, `StickPiece`, `SettlePlacement(s, id, landing, verdict, SettleOptions(start, rules)) → PlacementOutcome (Correct / Stuck / Bounced)`, `ReturnBrokenPiece`; `PlacementRules(onPlacement)` + `PlacementOverrides.Default/Stick` (Y8 kancası) |
| `delivery.ts` | 107 | `Delivery.cs` | **P1** | `Delivery.BatchesFor`, `EnqueueBatchesFor(s, completed) → int[]`, `DeliveryColumns(s, id)`, `DeliveryLanding(s, id) → Anchor?`, `DeliverQueue(s) → DeliveryResult {Delivered[] {PieceId, From, To, Rows}, Queued}` (K-25, K-26, E-34) |
| `site.ts` | 82 | `Site.cs` | **P1** | `SiteStrategy` (abstract: Mode, ActiveSegment, FrameOffset, CompleteIfDone → SegmentCompletion? {Seg, Completed, ShiftedTo, Enqueued}, CarouselTick, ElevatorTick), `Site.SiteStrategy(lvl)`; S1 `segments` tam; `carousel` ve asansör TS'deki gibi "Phase 3" fırlatır (`NotImplementedException`, TS `Error`) |
| `panorama.ts` | 55 | `Panorama.cs` | **P1** | `Panorama.PanoramaView(s) → PanoramaSegment[] {Index, Status done/active/future, Rows}` (K-06; durum okuması tamponu değiştirmez) |
| `obstacles/*.ts` (registry, types, W1, Y3, S1) | 745 | — | sonraki (engel paketi) | eklenti arayüzü; `DragRules`, `FallRules`, `PlacementRules`, `SettleHooks` kanca tipleri P1'de hazır |
| `level/logic.ts` | 1585 | — | sonraki (bölüm doğrulama paketi) | K-45 35 hata kodu, `loadLevel`; `CompileTests` içindeki "loadLevel refuses invalid levels" testi bununla gelir |
| `moves.ts` (geri kalan) | 945 | — | sonraki (K-35 hattı paketi) | `applyMove`, `MoveResult`, olay akışı, `eventLogHash`; P1 test dosyalarının ertelenen `applyMove` testleri bununla gelir (aşağıda) |
| `goals.ts`, `combo.ts`, `summary.ts` | 623 | — | sonraki (K-35 hattı paketi) | |
| `deadlock.ts` | 866 | — | sonraki (çıkmaz paketi) | D1/D2/D3a, Söküm |
| `boosters.ts` | 486 | — | sonraki (güçlendirici paketi) | |
| `session.ts` | 439 | — | sonraki (oturum paketi) | `GameSession.start/replay/commit`, geri al, `RULES_VERSION = 2`; altın `hand.json` eşdeğerlik testi burada kapanır |

## Testler (`Assets/MinikUsta/Core.Tests`)

Sayılar: C# `[Test]` / TS `it()`. "Ertelenen" testlerin hepsi `applyMove` ya da `GameSession` üzerinden koşar ve K-35 / oturum paketleriyle taşınır;
test başlıkları (`// Port of …`) ertelenenleri adlarıyla sayar.

| TS testi | C# testi | Durum | Not |
| --- | --- | --- | --- |
| `tests/fixtures/builders.ts` | `Fixtures/Builders.cs`, `Fixtures/J.cs` | P0 | `Level/Compiled/InitialState(LevelSpec)`, `ExpectAscii` |
| — | `Fixtures/TestPaths.cs` | P0 (+P1) | repo kökü, `levels/`, `Golden/`, `Vectors` (p0), `P1Vectors` (p1) |
| — (her TS test dosyasının kendi kopyaları) | `Fixtures/Boards.cs` | P1 | `N/R/A/C`, `Site/YardAt/YardP/SiteP/QueueP`, `Key`, `Pc/Ps/Plus`, `Gap/LockedGap/Wall/Crate/Bag/Screw/KeyItem/Elevator/WW/Blockers`, `AnchorMap`, `Init/LevelFile/LevelState`, `Drag/ToSite/Remove/Snapshot`, `ExpectConsistent`, `AssertVerdict/AssertDrop/AssertCancel/AssertPickFails/AssertFall/AssertSteer`, `FirstHandMove`, `Canonical` |
| `tests/core/movement.test.ts` + `geometry.test.ts` hücre modeli | `Fixtures/CellModel.cs` | P1 | TECH §4.2 hücre-hücre referans modeli (`Valid/Step/Neighbours/Bfs/Classify`), her geometri |
| `types.test.ts` | `TypesTests.cs` | P0 | + 43 olay türü ve oturum eylemi adları |
| `rng.test.ts` | `RngTests.cs` | P0 | + TS bit-eşitlik vektörleri (`p0_vectors.rng`) |
| `hash.test.ts` | `HashTests.cs` | P0 | + 10 bölümün başlangıç karması ve Zobrist tablo başları TS ile bit bit |
| `state.test.ts` | `StateTests.cs` | P0 | |
| `grid.test.ts` | `GridTests.cs` | P0 | + 1 000 adımlı seed'li yürüyüşün karmaları TS ile bit bit |
| `coords.test.ts` | `CoordsTests.cs` | P0 | |
| `ascii.test.ts` | `AsciiTests.cs` | P0 | |
| `shapes.test.ts` | `ShapesTests.cs` | P0 | |
| `level/compile.test.ts` | `CompileTests.cs` | P0 (10/11) | `loadLevel` testi `logic.ts` ile gelir |
| `geometry.test.ts` (K-49, 9 geometri) | `GeometryTests.cs` + `GeometryTests.P1.cs` (partial) | P0 + **P1** (22/25 + kapı) | P0: geometry, bölgeler/sınır, compile/layout, Zobrist konumları, ASCII gidiş-dönüş, helpers (14). P1: kenar modeli ≡ hücre modeli 9 geometri × 90 tahta, E-56 (classify yarısı), K-05 kısa tahta, E-57 kamyon düşüşü, K-17 adım 2/3, K-44 ×3 (8). Ertelenen: rastgele oyun değişmezleri + replay (oturum), E-56 `applyMove` yarısı, K-44 hat testi |
| `geometry.gate.test.ts` | `GeometryTests.cs` (kapı bölümü) | P0 + **P1** | Sayı kapısı artık TS `GEOMETRY_CORE` ile aynı 12 dosya (Movement/Gravity/Placement/Delivery eklendi); `Movement.cs` gerekçeleri: `D2Epsilon = 1e-9`, `new short[8]` komşu tamponu, `DropClass` K-07 satır numaraları 5–7. ESLint yarısının karşılığı: `State.YardOccRows` `[Obsolete]` ve referanssız |
| `movement.test.ts` | `MovementTests.cs` | **P1** (56/56; 55 TS + 600 tahta özellik testi) | K-04, K-05, K-07, K-08 (eşitlik sırası 1–4, hysteresis 0.2), K-09 (a–e), K-10, K-11, K-12, K-13, K-44, Bölüm 1–10 başlangıç konumları, kenar modeli ≡ hücre modeli (600 tahta). Ertelenen (1): "K-12 dragEnteredRail … Bölüm 4 gapPass" (`run`) |
| `gravity.test.ts` | `GravityTests.cs` | **P1** (34/33) | K-11, K-18, K-19, K-20, E-20; W8 / S8 kancaları test tarafı kural olarak |
| `placement.test.ts` | `PlacementTests.cs` | **P1** (36/35) | K-14…K-17, K-32, K-33, K-34, E-24, Y8 yaşam döngüsü |
| `site.test.ts` | `SiteTests.cs` | **P1** (3/4) | Phase 3 taslak testi birebir; K-22 iki testi ve K-15 moloz testi `applyMove` ister → ertelendi, yerine aynı strateji kodunu doğrudan çağıran C# eki |
| `panorama.test.ts` | `PanoramaTests.cs` | **P1** (5/4) | K-06; bölüm tamamlama `applyMove` yerine `SettlePlacement` + strateji ile; Bölüm 5 HAND replay testi ertelendi; 2 C# eki (S2 `.`, `?` gizli) |
| `delivery.test.ts` | `DeliveryTests.cs` | **P1** (6/8) | K-25, K-26, E-34 doğrudan çağrılar. Ertelenen (3): E-03 kuyruk tahtası, K-25 arrivedTurn, K-25 Bölüm 5 replay |
| `tests/review/movement.review.test.ts` | `MovementReviewTests.cs` | **P1** (53/70) | Bağımsız kural incelemesi tur 1–3: movement / coords / grid / gravity API'leriyle koşan her test. Ertelenen (18, hepsi `applyMove`): K-07 maliyetleri, K-10/K-11/K-12/K-13 hat örnekleri, E-30, vinç transiti, dilim kayması sonrası moloz, N14 çekme, Bölüm 1/4/9 el yolları, S2 `.` yerleşim |
| `tests/review/placement.review.test.ts` | `PlacementReviewTests.cs` | **P1** (34/52) | Tur 1–4: gravity / placement / movement API'leriyle koşan her test (`reasonCells` dahil). Ertelenen (18, `applyMove` / `GameSession`): K-17/K-34/K-22 hat örnekleri, K-18 gölge = sonuç özellikleri, S4 moloz hattı, K-14 vs K-39 Undo |
| — | `P1VectorTests.cs` | **P1** (12) | TS ↔ C# karma eşitliği: 10 bölüm + 9 geometri × 5 rastgele tahta (484 parça): pick sonucu, R + BFS uzaklıkları, K-07 sınıfı, her saha düğümünde `ComputeFall` (iniş, yön, mesafe, yol, hücreler, karar, gizli, etki) + `ShadowInfo` tonu, K-08 `Follow` taraması (0,5 hücre ızgara, kayan nokta hysteresis dahil), `YardDropRow/DropIntoYard`, `ReturnTarget` (2 varyant); tahta başına `SettleYard` hareketleri + sonrasındaki durum karması, `BuildFront`, `SiteColumnMasks`. Üretici `tools/gen-p1-vectors.ts`; `GeometryTests.RandomLevel` TS `randomLevel` RNG dizisini birebir çeker (bu test onu da doğrular) |
| — | `JsonTests.cs` | P0 | okuyucu/yazıcı, canonical, FNV-1a vektörleri, 10 bölümün parse→write→parse kararlılığı |
| `tests/golden/hand.test.ts` | — | sonraki (oturum paketi) | `level_NNN.hand.json` adımlarını oynat → `eventLogHash` + son ASCII birebir |

## Veri

| Web | Unity | Araç |
| --- | --- | --- |
| `levels/level_001…010.json` | `Assets/StreamingAssets/MinikUsta/levels/` | `tools/sync-from-web.sh` |
| `tests/golden/*.json` | `Assets/MinikUsta/Core.Tests/Golden/` | `tools/sync-from-web.sh` |
| TS çekirdeği (P0 vektörleri) | `Assets/MinikUsta/Core.Tests/Golden/p0_vectors.json` | `node tools/gen-p0-vectors.ts` |
| TS çekirdeği (P1 vektörleri) | `Assets/MinikUsta/Core.Tests/Golden/p1_vectors.json` | `node tools/gen-p1-vectors.ts` (`--verbose`: `p1_vectors.verbose.json`, git dışı) |

Web reposu değişince: `bash tools/sync-from-web.sh`, `node tools/gen-p0-vectors.ts`, `node tools/gen-p1-vectors.ts`, `cd dotnet && dotnet test`;
sonra bu dosyanın başındaki web commit'ini güncelle.
