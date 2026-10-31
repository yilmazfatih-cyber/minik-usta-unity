# TS → C# eşleme tablosu (PORT_MAP)

Kaynak gerçek: web reposu `src/core/**` (`../minik-usta`, commit `4ce980f`). Her paket bu tabloyu günceller.
Durum: **P0** = iskelet paketinde taşındı · **P1** = hareket paketinde taşındı · **P2** = kural / çıkmaz / güçlendirici
paketinde taşındı · **sonraki** = henüz taşınmadı (önerilen paket parantezde). Her satırdaki C# üye adları `grep` ile doğrulanmıştır.

## Çekirdek (`Assets/MinikUsta/Core`)

| TS dosyası | Satır | C# dosyası | Durum | Not |
| --- | ---: | --- | --- | --- |
| `types.ts` | 326 | `Types.cs` | P0 (+P1) | `ColorCodes`, `ShapeKinds`, `Rotations`, `Zone` (int sabitler, `Zone.IndexOf(name)`), `Anchor`, `DragNode`, `DragMode.Free`, `PieceFlags`, `At` (readonly struct: zone + x + y + seg?), `Verdict` (P1: `IEquatable`, TS `toEqual` eşitliği), `Steer`, `HammerTarget`, `SessionAction` (abstract) → `Move` (`MoveKind` enum + alt sınıflar), `UndoAction`, `StartAction`; `GameEvent` (abstract, `EventKind` enum + alt sınıflar); `VerdictReasons.All` (K-34 sırası); `CancelReasons`; `TeardownCauses`, `TeardownPlace`, `TeardownMove`, `HammerTargetKinds` |
| `geometry.ts` | 171 | `Geometry.cs` | P0 | `Geometry.MaxCols/MaxRows/CraneRows/MaxBoardRows/MaxSiteCols/SegmentSlots/MaxSegments`, `GeoParams` (struct), `BoardGeo` (sealed, readonly alanlar, `SameAs`), `MakeGeo`, `GeoFromLevel(LevelData)`, `HasDefaultBoard`, `GeoLabel`, `DefaultSizes`, `DefaultGeo` |
| `coords.ts` | 166 | `Coords.cs` | P0 | `CellIndex/CellX/CellY`, `DragNodeCode/DragNodeFromCode`, `InGrid/OnBoard/IsYardCell/IsYardAir/IsSiteCell/IsSiteAir/IsCraneCell/SideOf/StraddlesBoundary`, `RowRangeMask/OpenFreeMask/OpenRailMask/ClosedBoundaryMask/BoundaryAllows`, `Neighbors4` |
| `shapes.ts` | 240 | `Shapes.cs` | P0 | `Cell`, `ShapeDef` (Index, Id, Kind, Rotation, Cells, W, H, CellCount, Rows, ColRows, ColBottom, ColTop, Heavy, Canonical, CanonicalIndex), `Shapes.All` (52), `ShapeById/ShapeByIndex/IsShapeId/RotateShapeId/RotateClockwise`, `AlwaysHeavy`, `ForbiddenShapes`, `KindFirstChapter`, `HeavyMinLevelId` |
| `rng.ts` | 121 | `Rng.cs` | P0 | `IRng` (NextU32/Next/NextInt), `Mulberry32(seed)`, `BufferRng(int[] buf, int index)`, `Splitmix32(seed)`, `Rng.Mulberry32Next/Mulberry32Output/Fmix32/Hash32` (`Hash32Version = "fmix32-chain-v1"`); `Step/ToUnit/ToInt` internal |
| `hash.ts` | 163 | `Hash.cs` | P0 | `ZobristTables` (Pos0/Pos1/Field0/Field1 uint[], Cycle), `Hash.ZobristSeed/PosSite/PosQueue`, `PiecePosition`, `HashedFieldOffsets`, `BuildZobrist`, `HashState(s, uint[]?)`, `HashHex`, `Lcm` |
| `moves.ts` → kanca tipleri | (945) | `Hooks.cs` | **P2** | `MoveScratch` (WasStuck, RotatedAtStep8, Affected), `RuleContext` (Lvl, S, Rng, Gravity, Scratch, `Emit(GameEvent)`, `WithEmit`, `RuleContext.Of(s, emit, scratch?)` = TS `makeContext` / test `ctxOf`), `EntityRef` (`Obstacle(index)` / `Piece(id)`), `NeighborEffects` (none/affected/freed + `Rank`), `HammerRules` (CanHit, Hit), `MoveHooks` (Drag, Fall, Placement, OnPassGap, OnYardRelease, MoveCost, OnNeighborMoved, OnCellUncovered, AfterNeighbors, Hammer, OnTruckHelp, Timers, Deadlock; `None` = `NO_HOOKS`, `DefinedKeys()` = `Object.keys`, `IsEmpty`) |
| `moves.ts` → `fnv1a64`, `canonicalJson` | (945) | `Fnv1a.cs`, `Json/JsonWriter.cs` (`Canonical`) | P0 (kısmi) | Yalnız karma yardımcıları taşındı |
| `moves.ts` (geri kalan) | 945 | — | sonraki (K-35 hattı paketi, P3) | `applyMove`, `EventSink`/`ArraySink`/`NULL_SINK`, `Emitter`, `MoveResult`, `ApplyOptions`, `measureYao`, `isLevelWon`, `eventLogHash`; P1/P2 test dosyalarının ertelenen `applyMove` testleri bununla gelir (aşağıda) |
| `state.ts` | 453 | `State.cs` | P0 | `H` (18 başlık alanı), `PF` (9 alanlı kayıt), `GF`, `OF`, `FlagBit`, `StateCounts`, `StateLayout`, `GameState(Lvl, Buf)`, `PieceView`, `State.ComputeLayout/Hdr/SetHdr/PieceField/…/YardOcc/SiteOcc/FilledMask/WrongOccMask/GapField/ObstacleField/HiddenCollected/GoalValue/RevealedMask/QueueIds/EnqueuePiece/RemoveQueueAt/PlanAreaMask/CreateInitialState/CloneState/EncodeState/DecodeState` |
| `grid.ts` | 293 | `Grid.cs` | P0 | `BoardCell`, `Grid.VisibleSegment/PieceBoardCells/OccupyPiece/VacatePiece/RefreshSiteMasks/CollisionMasks/SiteColumnTops/StateInvariantErrors/PlanColorAt` |
| `ascii.ts` | 411 | `Ascii.cs` | P0 | `AsciiSize`, `AsciiBoard`, `Ascii.AirChar`, `WallChar`, `ToAscii(s, ids)`, `ParseAscii`, `FromAscii(lvl, text)` |
| — (zod yerine) | — | `Json/JsonValue.cs`, `Json/JsonReader.cs`, `Json/JsonWriter.cs` | P0 (+P2) | Bağımlılıksız JSON: sayı/dize/dizi/nesne/bool/null; anahtar sırası korunur; `JsonWriter.Canonical` = TS `canonicalJson`; sayı biçimi ECMAScript `Number.prototype.toString`. P2: `JsonValue.Remove(key)` (TS `delete`), `Clone()` (derin kopya) |
| `level/schema.ts` | 400 | `Level/LevelData.cs`, `Level/LevelJson.cs` | P0 (+P2) | `LevelJson.Parse(json, issues)` / `ParseOrThrow`; zod varsayılanları ve hata kodları birebir. P2: ayrımlı birleşimlerin (`type`, `kind`, `event`) "eşleşen ayırıcı yok" sorunu zod 4 gibi ayırıcı anahtarın yolunda (`tutorial[1].done.event`); `LevelSchemaPatterns`, `MechanicIds`, `TutEvents` |
| `level/logic.ts` | 1585 | `Level/Logic.cs` | **P2** | `Issue` (Code, Rule, Check, Severity, Path, Message), `Severities`, `LogicContext` (FileId, PreviousMechanics, BoosterUnlock, I18nKeys (`I18nKeySets`), Only, TileBudget; `WithOnly`), `ValidationResult`, `Logic.CheckIds` (28), `RuntimeChecks` (9), `RuleOf(check)`, `ColorFirstLevel`, `ChapterColorLimit`, `HardLevels/SuperhardLevels`, `HighlightBoosterKey`, `YardColsRange/YardRowsRange/SiteRowsRange`, `SiteColsRange(chapter)`, `YardEmptyMin/YardEmptyMax`, `TileBudget`, `TutorialMaxSteps`, `CellsAt`, `LevelSizes` + `LevelSizesOf`, `FitsFrame`, `CanReachSite`, `IssuesFromSchema(SchemaIssue[], json)`, `ValidateLevelJson(json, ctx)`, `CheckLevel(level, ctx)` (L-01…L-31 aynı sırayla; `Push` sırası = TS), `LevelColorSet`, `TileLevel(level, plans, geo?, budget)` (L-11 D3a, sayısal/metin memo anahtarı), `MaterialShortfall` (deprecated K-27, kararlı sıralama). Mesajlar TS şablonlarıyla karakter karakter aynı (p2 vektörleri karşılaştırır) |
| `level/compile.ts` → `loadLevel` | (462) | `Level/Load.cs` | **P2** | `LoadResult` (Ok, Level, Warnings, Stage schema|logic, Issues, `LevelOrThrow`), `Compiler.LoadLevel(JsonValue|string, LogicContext?)` (şema + `RuntimeChecks` + `Compile`); `Compiler` artık `partial` |
| `level/compile.ts` | 462 | `Level/Compile.cs` | P0 | `Compiler.Compile(LevelData) → CompiledLevel` (Geo, Layout, Zobrist, Segments, Pieces, Gaps, Batches, Obstacles, Supply/Demand, Mechanics, Step10, Cycle, TutorialPieceIds), `GravityProfiles`, `Step10Timers`, `CycleLength`, `PieceClassOf` |
| `level/plan.ts` | 153 | `Level/Plan.cs` | P0 | `Plan.BuildPlans(level, geo) → PlansResult`, `Plan.LocalIndex(sx, sy, ws)`, karakter sabitleri `Plan.None/Dot/Hidden` |
| `level/mechanics.ts` | 97 | `Level/Mechanics.cs` | P0 | `Mechanics.Table`, `DeriveMechanics`, `NewMechanics`, `IsCargo`, `BoardHeight` |
| `movement.ts` | 767 | `Movement.cs` | P1 | `Movement.Free/RailMode(gap)/NodesPerMode/HysteresisD2/D2Epsilon/IsCargoShape/IsSiteClosed/IsCancelled/TryBeginDrag(s, id, DragRules?) → DragAttempt/BeginDrag/CanPickPiece/BlockCells`; `DragRules(canPick, canPassGap, siteClosed)`; `DropKinds`, `DropClass`; `PickFailures`; `DragSession {…}`; `FollowResult {…}` |
| `gravity.ts` | 605 | `Gravity.cs` | P1 | `Gravity.ComputeFall(s, id, node, FallOptions?) → FallResult`, `SiteColumnMasks`, `SiteLandingRow`, `YardBalloonLanding`, `ShadowInfo`, `SettleYard(s, SettleHooks?) → YardMotion[]`; `FallRules(modifyFall, onLanded)`, `FallPlan`, `LandingEffect`, `SettleHooks`, `FallenBlock`; `Bits.Clz32/HighestBit/LowestBit` |
| `placement.ts` | 648 | `Placement.cs` | P1 | `PiecePlace`, `Placement.PiecePlace/SitePlace/YardPlace/PlaceAt`, `IsCorrectPlacement`, `ReasonCells`, `AllCellsInPlanArea`, `BuildFront`, `EligibleTrowelCells`, `IsSegmentComplete`, `NearestColumnsFirst`, `YardDropRow`, `DropIntoYard`, `ReturnTarget`, `MovePiece`, `LockPiece`, `StickPiece`, `SettlePlacement → PlacementOutcome`, `ReturnBrokenPiece`; `PlacementRules(onPlacement)` + `PlacementOverrides.Default/Stick` |
| `delivery.ts` | 107 | `Delivery.cs` | P1 | `Delivery.BatchesFor`, `EnqueueBatchesFor`, `DeliveryColumns`, `DeliveryLanding`, `DeliverQueue → DeliveryResult` (K-25, K-26, E-34) |
| `site.ts` | 82 | `Site.cs` | P1 | `SiteStrategy` (abstract), `Site.SiteStrategy(lvl)`; S1 `segments` tam; `carousel` ve asansör "Phase 3" fırlatır (`NotImplementedException`, TS `Error`) |
| `panorama.ts` | 55 | `Panorama.cs` | P1 | `Panorama.PanoramaView(s) → PanoramaSegment[]` (K-06) |
| `goals.ts` | 229 | `Goals.cs` | **P2** | `GoalKinds`, `GoalView`, `Goals.GoalKind/GoalTarget/GoalDone/AddGoalCount/SetBuildProgress/CountDebrisLeftSite/ExtraGoalsMet/AllSegmentsComplete/IsMaterial/IsRemainingSupply/RemainingSupplyByColor/RemainingDemandByColor/BlocksLeft/MaterialLeft/LevelGoalsMet/GoalSnapshot/ChangedCountGoals/GoalViews` (K-41, K-28, K-47, K-48) |
| `combo.ts` | 149 | `Combo.cs` | **P2** | `ComboStep`, `Combo.ComboForTrowel/ComboOf/TrowelsOf/ComboOnCorrect/ComboReset/GrantTrowels` (K-33); deprecated Faz 2 hücre malası `TrowelTarget`, `TrowelRejectReasons`, `TrowelFill`, `TrowelRejection`, `ApplyTrowel` |
| `boosters.ts` | 486 | `Boosters.cs` | **P2** | `BoosterOutcomes`, `UndoBlocks`, `UndoView`, `BoosterRejects`, `HammerEffect`, `CraneSpot`, `CraneEffect`, `TrowelEffect`, `BoosterSlots`, `BoosterTargets`; `Boosters.UndoBlock` (K-39), `HammerTargetKind/HammerTargets/ApplyHammer` (K-36), `CraneSelectable/CraneSiteSpots/CraneRejection/ApplyCraneEffect/HasCraneTarget` (K-37), `PaintSelectable/PaintPairOk/PaintPartners/HasPaintTarget/PaintRejection/SwapColors/ColorCodeOf` (K-38), `TrowelSelectable/TrowelSpots(precheck)/TrowelPieces/GoldTrowelRejection/ApplyTrowelEffect` (K-33), `BoosterTargets` (K-54) |
| `deadlock.ts` | 866 | `Deadlock.cs` | **P2** | `TileResults`, `IDeadTable`, `DeadTableJson`, `Step12Options`, `D3aStats.Expansions`; `Deadlock.AnalyticsCause/D3aMaxExpansions (20 000)/ReshuffleAttempts (8)/HasPaintGates/IsFullCover/QuickHoldable/HoldableIgnoringMoves/NoMoves/HasReachableCorrect/AnyOneMoveCorrect/ColumnHeights/CorrectSpots/NeededNow/ColorImbalance/TileRemaining (4096 yuvalı memo, exact `long` anahtar / FNV karışımı bit bit TS)/DeadTableFromJson/DetectDeadlock(s, table, tiling)/PrecheckOk/ReshuffleYard(s, IRng, neededFirst, drag, fall) → PieceShift[]/RunStep12(ctx, hooks, Step12Options)/Teardown(s, preAction, cause) → TeardownEvent` (K-30, K-29, K-35 adım 12) |
| `summary.ts` | 245 | `Summary.cs` | **P2** | `QueueEntry`, `PendingCounts`, `TurnSummary`, `ReachCache`, `SlotStates`; `Summary.HoldableIds/QueueEntries/PendingCounts/CarryIds/TurnSummary(s, hooks, undoable)/UnlockedNeeded(prev, s, cache, hooks)/SlotState(unlocked, hasTarget, count)/PurchaseOffered` (TECH §2R.15, K-09, K-26, K-34 kanca 5, K-54) |
| `obstacles/types.ts` | 169 | `Obstacles/Types.cs` | **P2** | `RuleIds.All` (26), `RuleZones` (Wall/Yard/Site/Gravity, `OrderBase`, `Of(id)`), `RuleOwnership(obstacle, pieceFlag, gapType)`, `ObstacleRule` (Id, Zone, Order, InfoKeys, AppliesTo, Owns + 16 isteğe bağlı kanca; yapıcı adlı parametrelerle = TS `defineRule`; `WithId`), `ObstacleRules.ObstacleInfoKey/UsesMechanic` |
| `obstacles/registry.ts` | 445 | `Obstacles/Registry.cs` | **P2** | `RuleSet`, `RuleOptions(disabled, rules)`, `Registry.AllRules` (W1, Y3, S1; statik yapıcı `AssertValidRules`), `ValidateRules` (TS hata metinleriyle), `AssertValidRules`, `GetRuleSet` (= `ruleSet`), `ActiveRuleIds`, `InfoKeysFor`, `LevelHooks(lvl, opts?)` (`ConditionalWeakTable` önbelleği), `ComposeHooks`; S5/S6 kene fabrikaları (`SiteTick`) |
| `obstacles/W1_staticGap.ts` | 24 | `Obstacles/W1_StaticGap.cs` | **P2** | `W1_StaticGap.Rule` (order 101, owns gapType static, kancasız) |
| `obstacles/Y3_chain.ts` | 85 | `Obstacles/Y3_Chain.cs` | **P2** | `Y3_Chain.Rule` (order 203, owns pieceFlag chained; canPick, onNeighborMoved, afterNeighbors (E-53), canHammer/onHammer, onTruckHelp) |
| `obstacles/S1_slidingSite.ts` | 22 | `Obstacles/S1_SlidingSite.cs` | **P2** | `S1_SlidingSite.Rule` (order 301, kancasız) |
| `session.ts` | 439 | — | sonraki (oturum paketi, P5) | `GameSession.start/replay/commit/apply/undo`, `levelHash`, `RULES_VERSION = 2`; altın `hand.json` eşdeğerlik testi burada kapanır |

## Testler (`Assets/MinikUsta/Core.Tests`)

Sayılar: C# `[Test]` (TestCaseSource açılmış) / TS `it()`. "Ertelenen" testlerin hepsi `applyMove` olayları ya da `GameSession`
üzerinden koşar ve K-35 / oturum paketleriyle taşınır; test başlıkları (`// Port of …`) ertelenenleri sayar. P2'de TS
`run(s, dragTo(…))` kurulumları `Boards.Drop` ile (K-35 adım 0–4, 6–9'un olaysız çekirdek çağrıları; adım 5/10/11/12 yok),
güçlendirici hamleleri doğrudan `Boosters.*Rejection` + `Apply*Effect` ile, kanonik çözümler `Boards.Play` ile oynatılır.

| TS testi | C# testi | Durum | Not |
| --- | --- | --- | --- |
| `tests/fixtures/builders.ts` | `Fixtures/Builders.cs`, `Fixtures/J.cs` | P0 | `Level/Compiled/InitialState(LevelSpec)`, `ExpectAscii` |
| — | `Fixtures/TestPaths.cs` | P0 (+P1, +P2) | repo kökü, `levels/`, `Golden/`, `Vectors` (p0), `P1Vectors`, `P2Vectors`; P2: `FixturesDir`, `FixturePath(group)`, `LoadFixture(group, name)`, `FixtureNames`, `LoadBoosterUnlock()` (`config/economy.json`) |
| `tests/fixtures/cover.ts` | `Fixtures/Cover.cs` | **P2** | `Cover.CoverLevel(seed)`: TS ile aynı RNG çağrı sırası (p2 vektörleri 40 tohumun `levelHash` değerini karşılaştırır) |
| `tests/obstacles/obstacles.fixtures.ts` | `Fixtures/ObstacleFixtures.cs` | **P2** | `CtxOf(s, rotatedAtStep8) → CapturedContext {Ctx, Events, Types()}`, `RankOf`, `FakeRule(id, …adlı kanca parametreleri)`, `Noop`; `i18nText` web reposunun `src/i18n`'ini okur, karşılığı yok |
| — (her TS test dosyasının kendi kopyaları) | `Fixtures/Boards.cs` | P1 (+P2) | P1 yardımcıları + P2: `LevelFile` artık `Compiler.LoadLevel` (TS `levelFile`), `Drop(s, id, ix, iy, gap?, hooks?) → DropResult {Drop, Outcome, Completed, Delivery, Correct}`, `HandMoves(level)` (altın `log`), `Play(s, level, from, to)` / `Play(s, HandMove)` |
| `tests/core/movement.test.ts` + `geometry.test.ts` hücre modeli | `Fixtures/CellModel.cs` | P1 | |
| `types.test.ts` | `TypesTests.cs` | P0 | |
| `rng.test.ts` | `RngTests.cs` | P0 | |
| `hash.test.ts` | `HashTests.cs` | P0 | |
| `state.test.ts` | `StateTests.cs` | P0 | |
| `grid.test.ts` | `GridTests.cs` | P0 | |
| `coords.test.ts` | `CoordsTests.cs` | P0 | |
| `ascii.test.ts` | `AsciiTests.cs` | P0 | |
| `shapes.test.ts` | `ShapesTests.cs` | P0 | |
| `level/compile.test.ts` | `CompileTests.cs` | P0 + **P2** (12/11) | P2: "K-45 loadLevel runs schema + runtime checks and refuses invalid levels" eklendi (tam) |
| `tests/level/validator.test.ts` | `LogicTests.cs` | **P2** (103/103) | 62 geçersiz fikstür (her biri yalnız kendi kodunu verir) + 3 kapsama testi, 8 geçerli fikstür + 4, 19 mantık ayrıntısı (K-02 bandı, K-49 aralıkları, K-47 örtü, K-30 D3a, K-44, K-45/7 moloz, çalışma zamanı alt kümesi), 7 L-17 öğretici testi; fikstürler `Fixtures/Levels/{valid,invalid}` |
| `tests/level/schema.test.ts` | `SchemaTests.cs` | **P2** (20/21) | Şema yarıları tam. Belge karşılaştırmaları web reposunda kalır: GDD 14.1 `done` sözlüğü (16 olayın şema yarısı burada), UX 13.1 vurgu sözlüğü (örnek dizgeler burada), LEVELS 11–38 `done` satırları (atlandı) |
| `tests/level/mechanics.test.ts` | `MechanicsTests.cs` | **P2** (8/8) | İlk testin OBSTACLES.md satır karşılaştırması web reposunda; "İlk bölüm" sütunu sabit tabloyla doğrulanır |
| `tests/level/drafts.test.ts` | `DraftsTests.cs` | **P2** (11/12) | 10 bölüm mekanik geçmişi + economy.json ile 0 hata; yeni mekanikler 4→W1, 5→S1, 6→W2, 8→Y5, 9→W3; 8 GDD karşı örneği. LEVELS §2 özet satırı testi veri tutarlılığı testiyle (E bandı, N, K-47 tam örtü) değiştirildi; textKey ↔ LEVELS testi atlandı |
| `tests/level/validate-tool.test.ts` | — | taşınmaz | `tools/validate-levels.ts` CLI ve `tools/lib/levelsAllow.ts` web araçlarıdır (Unity'de karşılığı yok) |
| `tests/core/goals.test.ts` | `GoalsTests.cs` | **P2** (5/5) | Bölüm 5 testi `Boards.Play` ile |
| `tests/core/combo.test.ts` | `ComboTests.cs` | **P2** (7/8) | Durum yarıları tam; `comboChanged`/`trowelEarned` olay sıraları, `boosterApplied`/`pieceLifted`, `legacyTrowel` reddi ve E-09 mini hattı (segment tamamlayan mala) K-35 ile |
| `tests/core/boosters.test.ts` | `BoostersTests.cs` | **P2** (13/14) | Etkiler doğrudan (`ApplyHammer`, `ApplyCraneEffect`, `SwapColors`, `ApplyTrowelEffect`); olay listeleri, `measureYao`, `won` bayrağı K-35 ile; K-39 Undo (`GameSession`) oturum paketiyle |
| `tests/core/deadlock.test.ts` | `DeadlockTests.cs` | **P2** (14/17) | D3a 6/6 (400 tohumlu kahin dahil), Söküm 5 + D1 3 doğrudan `RunStep12`/`Teardown`/`DetectDeadlock`; ertelenen: E-37 undo-after-teardown, K-43 çıkış cezası (oturum), E-59 kepenk fazı (zamanlayıcı, K-35); E-58 "0 hamlede D1 yok" + K-29 `afterOffer` doğrudan `RunStep12` ile |
| `tests/core/summary.test.ts` | `SummaryTests.cs` | **P2** (8/8) | `GameSession.apply` yerine `Drop` + `TurnSummary`; `undoable` girdisi oturumdan gelir |
| `tests/obstacles/registry.test.ts` | `Obstacles/RegistryTests.cs` | **P2** (21/21) | Tablo doğrulama metinleri regex ile; birleştirilmiş kancalar doğrudan çağrılır (komşu etkisi dağıtımı, `onCellUncovered`, adım 10 zamanlayıcı sırası `lvl.Step10` üzerinden, S6/S5 keneleri) |
| `tests/obstacles/W1_staticGap.test.ts` | `Obstacles/W1StaticGapTests.cs` | **P2** (8/9) | K-46 YAO sayaçları (`measureYao`, hat adım 3) K-35 ile; i18n kart metni web reposunda |
| `tests/obstacles/S1_slidingSite.test.ts` | `Obstacles/S1SlidingSiteTests.cs` | **P2** (7/7) | Bölüm 5 kanonik çözümü `Play` ile; olay listeleri / `won` / YAO K-35 ve oturum ile |
| `tests/obstacles/Y3_chain.test.ts` | `Obstacles/Y3ChainTests.cs` | **P2** (4/4) | Adım 5 komşu etkisi ve adım sonu taraması doğrudan `hooks.OnNeighborMoved` / `AfterNeighbors` ile |
| `tests/obstacles/S2_planVoid.test.ts` | `Obstacles/S2PlanVoidTests.cs` | **P2** (10/10) | `.` hücre kuralları çekirdekte; oturum/olay yarıları (`won`, YAO, `wrongCount`, `legacyTrowel`) K-35 ile |
| `geometry.test.ts` (K-49, 9 geometri) | `GeometryTests.cs` + `GeometryTests.P1.cs` | P0 + P1 (22/25 + kapı) | Ertelenen: rastgele oyun değişmezleri + replay (oturum), E-56 `applyMove` yarısı, K-44 hat testi |
| `geometry.gate.test.ts` | `GeometryTests.cs` (kapı bölümü) | P0 + P1 | |
| `movement.test.ts` | `MovementTests.cs` | P1 (56/56) | Ertelenen (1): "K-12 dragEnteredRail … Bölüm 4 gapPass" (`run`) |
| `gravity.test.ts` | `GravityTests.cs` | P1 (34/33) | |
| `placement.test.ts` | `PlacementTests.cs` | P1 (36/35) | |
| `site.test.ts` | `SiteTests.cs` | P1 (3/4) | |
| `panorama.test.ts` | `PanoramaTests.cs` | P1 (5/4) | |
| `delivery.test.ts` | `DeliveryTests.cs` | P1 (6/8) | |
| `tests/review/movement.review.test.ts` | `MovementReviewTests.cs` | P1 (53/70) | Ertelenen 18 (`applyMove`) |
| `tests/review/placement.review.test.ts` | `PlacementReviewTests.cs` | P1 (34/52) | Ertelenen 18 (`applyMove` / `GameSession`) |
| `tests/review/data.review.test.ts` | — | sonraki | `materialShortfall` çağrısı (1 test) dahil; Faz 2 inceleme testleri |
| — | `P1VectorTests.cs` | P1 (12) | TS ↔ C# karma eşitliği (484 parça) |
| — | `P2VectorTests.cs` | **P2** (12) | TS ↔ C# eşitliği: (1) 80 fikstür × 2 bağlam + 10 bölüm × 2 bağlam = 160 doğrulayıcı koşusu — mantık aşamasında kod/kural/denetim/önem/yol/**mesaj**, şema aşamasında kod/…/yol (zod'un kendi mesajları hariç), `tileLevel`, renk kümesi; (2) 10 bölümün kanonik çözüm boyunca TS `applyMove` ile üretilen 90 durumu (`encodeState` → `DecodeState`): D3a sonucu + açılım sayısı, D2, tespit, pre-check, D1 yüklemleri, holdable/needed/goals/queue/pending/carry/trowelPieces/targets/hammerTargets/front/rules, parça başına quick/hold/reach/correctSpots/craneSiteSpots/paintPartners/trowelSpots(±precheck)/hammerTargetKind/seçilebilirlikler, klon üzerinde `RunStep12` (olaylar + sonraki karma), doğrudan `Teardown`, dış RNG ile `ReshuffleYard`; (3) `Cover.CoverLevel(1…40)` `levelHash` + başlangıç tamponu + aynı sorgular. Üretici `tools/gen-p2-vectors.ts`. Negatif kontrol yapıldı: mesaj sözcüğü, reshuffle eşitlik kırıcısı ve vinç dönüş sırası bozulduğunda üç ayrı test üç ayrı doğru farkla düştü |
| — | `JsonTests.cs` | P0 | |
| `tests/golden/hand.test.ts` | — | sonraki (oturum paketi) | `level_NNN.hand.json` adımlarını oynat → `eventLogHash` + son ASCII birebir |

Toplam: 618 test (P1 sonunda 366; P2 +252), `dotnet build -warnaserror` 0 uyarı.

## Veri

| Web | Unity | Araç |
| --- | --- | --- |
| `levels/level_001…010.json` | `Assets/StreamingAssets/MinikUsta/levels/` | `tools/sync-from-web.sh` |
| `config/economy.json` | `Assets/StreamingAssets/MinikUsta/config/economy.json` | `tools/sync-from-web.sh` (L-17 `booster:`/`pre:` açılış seviyeleri) |
| `tests/golden/*.json` | `Assets/MinikUsta/Core.Tests/Golden/` | `tools/sync-from-web.sh` |
| `tests/level/fixtures/{valid,invalid}/*.json` | `Assets/MinikUsta/Core.Tests/Fixtures/Levels/{valid,invalid}/` | `tools/sync-from-web.sh` |
| TS çekirdeği (P0 vektörleri) | `Assets/MinikUsta/Core.Tests/Golden/p0_vectors.json` | `node tools/gen-p0-vectors.ts` |
| TS çekirdeği (P1 vektörleri) | `Assets/MinikUsta/Core.Tests/Golden/p1_vectors.json` | `node tools/gen-p1-vectors.ts` (`--verbose`: `p1_vectors.verbose.json`, git dışı) |
| TS çekirdeği (P2 vektörleri) | `Assets/MinikUsta/Core.Tests/Golden/p2_vectors.json` (204 KB) | `node tools/gen-p2-vectors.ts` (`--verbose`: `p2_vectors.verbose.json`, git dışı) |

Web reposu değişince: `bash tools/sync-from-web.sh`, `node tools/gen-p0-vectors.ts`, `node tools/gen-p1-vectors.ts`,
`node tools/gen-p2-vectors.ts`, `cd dotnet && dotnet test`; sonra bu dosyanın başındaki web commit'ini güncelle.
