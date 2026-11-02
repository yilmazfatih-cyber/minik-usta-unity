# TS → C# eşleme tablosu (PORT_MAP)

Kaynak gerçek: web reposu `src/core/**` (`../minik-usta`; **F1 sonrası: birleşik `wip/phase-2r` `6269c0c`** (= `wip/a2-levels`; P4B-01: `e80a318` veri + test paketi + Faz 2R arşivi); A3 `e80a318` ile taşındı — `RULES_VERSION` 3, K-55; önceki satırlar `fd61abd` içindir; çekirdek, testler ve veri `4ce980f` ile aynı — `4ce980f`'den bu yana yalnız belge commit'leri var:
`8ae243a`, `b31c4e8`, `fd61abd`. Q3 denetimi: `git diff --stat 4ce980f fd61abd -- src tests levels config` boş). Her paket bu tabloyu günceller.
Durum: **P0** = iskelet paketinde taşındı · **P1** = hareket paketinde taşındı · **P2** = kural / çıkmaz / güçlendirici
paketinde taşındı · **P3** = hamle hattı + oturum paketinde taşındı · **P4** = doğrulama derinliği paketinde eklendi
(kural kapsamı kapısı, TS kehanetli diferansiyel fuzz, senaryo testleri) · **sonraki** = henüz taşınmadı. Her satırdaki C# üye
adları `grep` ile doğrulanmıştır. P3 ile `src/core/**` altındaki tüm dosyalar taşınmıştır.

## Çekirdek (`Assets/MinikUsta/Core`)

| TS dosyası | Satır | C# dosyası | Durum | Not |
| --- | ---: | --- | --- | --- |
| `types.ts` | 326 | `Types.cs` | P0 (+P1, +P3) | P3: `BoosterAppliedEvent.Detail` artık `JsonValue?` (TS `detail: unknown`, kanonik JSON'a girer). `ColorCodes`, `ShapeKinds`, `Rotations`, `Zone` (int sabitler, `Zone.IndexOf(name)`), `Anchor`, `DragNode`, `DragMode.Free`, `PieceFlags`, `At` (readonly struct: zone + x + y + seg?), `Verdict` (P1: `IEquatable`, TS `toEqual` eşitliği), `Steer`, `HammerTarget`, `SessionAction` (abstract) → `Move` (`MoveKind` enum + alt sınıflar), `UndoAction`, `StartAction`; `GameEvent` (abstract, `EventKind` enum + alt sınıflar); `VerdictReasons.All` (K-34 sırası); `CancelReasons`; `TeardownCauses`, `TeardownPlace`, `TeardownMove`, `HammerTargetKinds` |
| `geometry.ts` | 171 | `Geometry.cs` | P0 | `Geometry.MaxCols/MaxRows/CraneRows/MaxBoardRows/MaxSiteCols/SegmentSlots/MaxSegments`, `GeoParams` (struct), `BoardGeo` (sealed, readonly alanlar, `SameAs`), `MakeGeo`, `GeoFromLevel(LevelData)`, `HasDefaultBoard`, `GeoLabel`, `DefaultSizes`, `DefaultGeo` |
| `coords.ts` | 166 | `Coords.cs` | P0 | `CellIndex/CellX/CellY`, `DragNodeCode/DragNodeFromCode`, `InGrid/OnBoard/IsYardCell/IsYardAir/IsSiteCell/IsSiteAir/IsCraneCell/SideOf/StraddlesBoundary`, `RowRangeMask/OpenFreeMask/OpenRailMask/ClosedBoundaryMask/BoundaryAllows`, `Neighbors4` |
| `shapes.ts` | 240 | `Shapes.cs` | P0 | `Cell`, `ShapeDef` (Index, Id, Kind, Rotation, Cells, W, H, CellCount, Rows, ColRows, ColBottom, ColTop, Heavy, Canonical, CanonicalIndex), `Shapes.All` (52), `ShapeById/ShapeByIndex/IsShapeId/RotateShapeId/RotateClockwise`, `AlwaysHeavy`, `ForbiddenShapes`, `KindFirstChapter`, `HeavyMinLevelId` |
| `rng.ts` | 121 | `Rng.cs` | P0 | `IRng` (NextU32/Next/NextInt), `Mulberry32(seed)`, `BufferRng(int[] buf, int index)`, `Splitmix32(seed)`, `Rng.Mulberry32Next/Mulberry32Output/Fmix32/Hash32` (`Hash32Version = "fmix32-chain-v1"`); `Step/ToUnit/ToInt` internal |
| `hash.ts` | 163 | `Hash.cs` | P0 | `ZobristTables` (Pos0/Pos1/Field0/Field1 uint[], Cycle), `Hash.ZobristSeed/PosSite/PosQueue`, `PiecePosition`, `HashedFieldOffsets`, `BuildZobrist`, `HashState(s, uint[]?)`, `HashHex`, `Lcm` |
| `moves.ts` → kanca tipleri | (945) | `Hooks.cs` | **P2** | `MoveScratch` (WasStuck, RotatedAtStep8, Affected), `RuleContext` (Lvl, S, Rng, Gravity, Scratch, `Emit(GameEvent)`, `WithEmit`, `RuleContext.Of(s, emit, scratch?)` = TS `makeContext` / test `ctxOf`), `EntityRef` (`Obstacle(index)` / `Piece(id)`), `NeighborEffects` (none/affected/freed + `Rank`), `HammerRules` (CanHit, Hit), `MoveHooks` (Drag, Fall, Placement, OnPassGap, OnYardRelease, MoveCost, OnNeighborMoved, OnCellUncovered, AfterNeighbors, Hammer, OnTruckHelp, Timers, Deadlock; `None` = `NO_HOOKS`, `DefinedKeys()` = `Object.keys`, `IsEmpty`) |
| `moves.ts` → `fnv1a64`, `canonicalJson` | (945) | `Fnv1a.cs`, `Json/JsonWriter.cs` (`Canonical`) | P0 | `Moves.Fnv1a64` / `Moves.CanonicalJson` bunlara yönlenir |
| `moves.ts` → olay alıcıları | (945) | `Events.cs` | **P3** | `IEventSink` (Enabled, Push) = `EventSink`, `NullSink.Instance` = `NULL_SINK`, `ArraySink` (Events, Clear), `Emitter` (internal: Seq 0, 1, 2 …, Step = K-35 adımı), `EventJson.ToJson(GameEvent / IEnumerable)` = TS nesne biçimi (`{seq, step, t, …}`; isteğe bağlı alanlar yalnız varken: `pieceMoved.gap`, `movesChanged.cost`, `truckHelp.moves/delivered`, `At.seg`; `At | 'queue'` → `"queue"`), `AtJson`, `AtOrQueue`, `TeardownPlaceJson` |
| `moves.ts` (geri kalan) | 945 | `Moves.cs` | **P3** | `Moves.ApplyMove(s, move, sink?, ApplyOptions?) → MoveResult` (drag: K-35 adım 0–12; hammer / crane / paint / goldTrowel: GDD §10 mini hat; `trowel` → `legacyTrowel`; `addMoves`: +5 teklifinde adım 12 bir kez), `MoveStatuses` (applied / cancelled / rejected), `MoveResult` (Status, Reason, Won, OutOfMoves, TeardownCause, `SameAs`), `ApplyOptions(hooks, noTruckHelp, strict, preAction, deadTable)` + `WithPreAction`, `IsLevelWon` (K-28), `MeasureYao → YaoMeasure` (K-46), `CanonicalJson`, `Fnv1a64`, `EventLogHash` (TECH §6.3). `Array.prototype.sort` kararlılığı için ekleme sıralaması (`StableSort`); `viaValid` / `steerValid` birebir |
| `state.ts` | 453 | `State.cs` | P0 | `H` (18 başlık alanı), `PF` (9 alanlı kayıt), `GF`, `OF`, `FlagBit`, `StateCounts`, `StateLayout`, `GameState(Lvl, Buf)`, `PieceView`, `State.ComputeLayout/Hdr/SetHdr/PieceField/…/YardOcc/SiteOcc/FilledMask/WrongOccMask/GapField/ObstacleField/HiddenCollected/GoalValue/RevealedMask/QueueIds/EnqueuePiece/RemoveQueueAt/PlanAreaMask/CreateInitialState/CloneState/EncodeState/DecodeState` |
| `grid.ts` | 293 | `Grid.cs` | P0 | `BoardCell`, `Grid.VisibleSegment/PieceBoardCells/OccupyPiece/VacatePiece/RefreshSiteMasks/CollisionMasks/SiteColumnTops/StateInvariantErrors/PlanColorAt` |
| `ascii.ts` | 411 | `Ascii.cs` | P0 | `AsciiSize`, `AsciiBoard`, `Ascii.AirChar`, `WallChar`, `ToAscii(s, ids)`, `ParseAscii`, `FromAscii(lvl, text)` |
| — (zod yerine) | — | `Json/JsonValue.cs`, `Json/JsonReader.cs`, `Json/JsonWriter.cs` | P0 (+P2) | Bağımlılıksız JSON: sayı/dize/dizi/nesne/bool/null; anahtar sırası korunur; `JsonWriter.Canonical` = TS `canonicalJson`; sayı biçimi ECMAScript `Number.prototype.toString`. P2: `JsonValue.Remove(key)` (TS `delete`), `Clone()` (derin kopya) |
| — (JS `RegExp` yerine) | — | `JsRegex.cs` | **P6** | `JsRegex : Regex` — TS düzenli ifade kaynağını (`/^…$/` içindeki metin) aynen alır, .NET'e JS anlamıyla çevirir: `$` → `\z`, `\d` → `[0-9]`, `\s` → JS'in 25 boşluk kod birimi, `\w` → ASCII, `.` → satır sonu hariç; güvenli çevirisi olmayan kaçışlar `NotSupportedException`. `Source` / `Literal` (`/kaynak/`, zod en mesajı), `IsJsSpace`, `Trim` (= `String.prototype.trim`), `DigitsToInt` (yalnız `[0-9]+`, taşmada `int.MaxValue`). Çekirdekte **tüm** düzenli ifadeler bunu kullanır (REVIEW_PHASE2 R-01, R-02, R-12) |
| `level/schema.ts` | 400 | `Level/LevelData.cs`, `Level/LevelJson.cs` | P0 (+P2, +A3) | `LevelJson.Parse(json, issues)` / `ParseOrThrow`; zod varsayılanları ve hata kodları birebir. P2: ayrımlı birleşimlerin (`type`, `kind`, `event`) "eşleşen ayırıcı yok" sorunu zod 4 gibi ayırıcı anahtarın yolunda (`tutorial[1].done.event`); `LevelSchemaPatterns`, `MechanicIds`, `TutEvents` **A3 (K-55):** `schemaVersion` 1 \| 2 (`LevelData.SchemaVersion`, `LevelSchema.SchemaVersionOf` / `IsFixedYardLevel`), `targets` D-099 bantları (`Min`, `MaxCover`, `DepLater`, `CrossDeps`, `ParkSlack` ≤ 9,99, `Forced`, `GreedyExcess`, `GreedyWin`, `AhaMin`, `Branch3`) |
| `level/logic.ts` | 1585 | `Level/Logic.cs` | **P2** (+A3) | `Issue` (Code, Rule, Check, Severity, Path, Message), `Severities`, `LogicContext` (FileId, PreviousMechanics, BoosterUnlock, I18nKeys (`I18nKeySets`), Only, TileBudget; `WithOnly`), `ValidationResult`, `Logic.CheckIds` (28), `RuntimeChecks` (9), `RuleOf(check)`, `ColorFirstLevel`, `ChapterColorLimit`, `HardLevels/SuperhardLevels`, `HighlightBoosterKey`, `YardColsRange/YardRowsRange/SiteRowsRange`, `SiteColsRange(chapter)`, `YardEmptyMin/YardEmptyMax`, `TileBudget`, `TutorialMaxSteps`, `CellsAt`, `LevelSizes` + `LevelSizesOf`, `FitsFrame`, `CanReachSite`, `IssuesFromSchema(SchemaIssue[], json)`, `ValidateLevelJson(json, ctx)`, `CheckLevel(level, ctx)` (L-01…L-31 aynı sırayla; `Push` sırası = TS), `LevelColorSet`, `TileLevel(level, plans, geo?, budget)` (L-11 D3a, sayısal/metin memo anahtarı), `MaterialShortfall` (deprecated K-27, kararlı sıralama). Mesajlar TS şablonlarıyla karakter karakter aynı (p2 vektörleri karşılaştırır) **A3:** L-36 `fixed_yard_batches` (K-45/11; çalışma anı alt kümesinde, `Logic.RuntimeChecks`), L-01 tüm `targets` bantlarını (`RateBand` dahil) denetler |
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
| `deadlock.ts` | 866 | `Deadlock.cs` | **P2** (+A3) | `TileResults`, `IDeadTable`, `DeadTableJson`, `Step12Options`, `D3aStats.Expansions` (**P6:** iş parçacığı başına; D3a bellek tablosu `[ThreadStatic]`, R-07); `Deadlock.AnalyticsCause/D3aMaxExpansions (20 000)/HasPaintGates/IsFullCover/QuickHoldable/HoldableIgnoringMoves/NoMoves/HasReachableCorrect/ColumnHeights/CorrectSpots/NeededNow/ColorImbalance/TileRemaining (4096 yuvalı memo, exact `long` anahtar / FNV karışımı bit bit TS)/DeadTableFromJson/DetectDeadlock(s, table, tiling)/PrecheckOk/RunStep12(ctx, hooks, Step12Options) (**A3, K-55 madde 8:** D1 → zincir/ıslaklık, hâlâ D1 ise `teardown{access}`; `afterOffer` +5 sonrası Söküm hedefi = `PreAction`)/Teardown(s, preAction, cause) → TeardownEvent` (K-30, K-29, K-35 adım 12) |
| `summary.ts` | 245 | `Summary.cs` | **P2** | `QueueEntry`, `PendingCounts`, `TurnSummary`, `ReachCache`, `SlotStates`; `Summary.HoldableIds/QueueEntries/PendingCounts/CarryIds/TurnSummary(s, hooks, undoable)/UnlockedNeeded(prev, s, cache, hooks)/SlotState(unlocked, hasTarget, count)/PurchaseOffered` (TECH §2R.15, K-09, K-26, K-34 kanca 5, K-54) |
| `obstacles/types.ts` | 169 | `Obstacles/Types.cs` | **P2** | `RuleIds.All` (26), `RuleZones` (Wall/Yard/Site/Gravity, `OrderBase`, `Of(id)`), `RuleOwnership(obstacle, pieceFlag, gapType)`, `ObstacleRule` (Id, Zone, Order, InfoKeys, AppliesTo, Owns + 16 isteğe bağlı kanca; yapıcı adlı parametrelerle = TS `defineRule`; `WithId`), `ObstacleRules.ObstacleInfoKey/UsesMechanic` |
| `obstacles/registry.ts` | 445 | `Obstacles/Registry.cs` | **P2** | `RuleSet`, `RuleOptions(disabled, rules)`, `Registry.AllRules` (W1, Y3, S1; statik yapıcı `AssertValidRules`), `ValidateRules` (TS hata metinleriyle), `AssertValidRules`, `GetRuleSet` (= `ruleSet`), `ActiveRuleIds`, `InfoKeysFor`, `LevelHooks(lvl, opts?)` (`ConditionalWeakTable` önbelleği), `ComposeHooks`; S5/S6 kene fabrikaları (`SiteTick`) |
| `obstacles/W1_staticGap.ts` | 24 | `Obstacles/W1_StaticGap.cs` | **P2** | `W1_StaticGap.Rule` (order 101, owns gapType static, kancasız) |
| `obstacles/Y3_chain.ts` | 85 | `Obstacles/Y3_Chain.cs` | **P2** | `Y3_Chain.Rule` (order 203, owns pieceFlag chained; canPick, onNeighborMoved, afterNeighbors (E-53), canHammer/onHammer, onTruckHelp) |
| `obstacles/S1_slidingSite.ts` | 22 | `Obstacles/S1_SlidingSite.cs` | **P2** | `S1_SlidingSite.Rule` (order 301, kancasız) |
| `session.ts` | 439 | `Session.cs` | **P3** (+A3) | `GameSession.Start(lvl, preBoosters?, streakTier, SessionOptions?, sink?)`, `Replay(lvl, log, opts?, sink?, ReplayStep?)` (`ReplayError` + `Index`), `Commit`, `Apply(SessionAction, sink?) → ActionResult` (MoveResult + Events + Teardown + Summary; TS `tee` = `TeeSink`), `Undo` / `UndoBlock` / `CanUndo` (K-39), `NextOffer → OfferInfo?` / `AcceptOffer(source)` / `DeclineOffer` (K-29), `Exit → ExitResult` (K-43, `movesSpent`), `Summary()`, `Yao()`, `State`, `Log` (kopya), `MovesMade/MovesSpent/MovesLeft/Outcome/OffersUsed/AdOfferUsed/Hooks/Resumed/PreBoosters/StreakTier`; sabitler `RulesVersion = 3` (A3, D-099: yeniden dizme kalktı; `lastPre` = son eylemin başlangıç tamponu, +5 sonrası D1 Söküm hedefi), `OfferMoves 5`, `MaxOffers 3`, `ThermosMoves 3`, `TrowelStart 1`, `OpenShutterMoves 5`; `LevelHash(JsonValue / LevelData)` (K-43/4); `SessionOptions(hooks, streakBonus, strict)`, `StreakBonus` |
| `session.ts` → `copyAction` + `services/save.ts` `asSessionActions` (okuma yarısı) | — | `SessionActionJson.cs` | **P3** | `ToJson(SessionAction / log)`, `FromJson`, `LogFromJson`, `Clone` (= `JSON.parse(JSON.stringify(a))`), `SameAs` / `SameLog` (kanonik JSON eşitliği = TS `toEqual`) |

## Testler (`Assets/MinikUsta/Core.Tests`)

Sayılar: C# `[Test]` / TS `it()` (`describe.each` / `TestCaseSource` açılmadan). P3 ile P1/P2'de ertelenen tüm `applyMove` /
`GameSession` testleri taşındı: `Boards.Run` = TS `run` (katı, `ArraySink`), `Boards.Drop` / `Boards.Play` artık tam K-35 hattı
(`Moves.ApplyMove`) üzerinden koşar; güçlendirici testleri TS gibi hamle olarak (`Run(s, new HammerMove(…))`) oynatılır. Hat
gerektiren inceleme testleri ayrı `partial` dosyalardadır (`MovementReviewTests.P3.cs`, `PlacementReviewTests.P3.cs`).
Unity reposunda karşılığı olmayan belge karşılaştırmaları (GDD/UX/LEVELS/OBSTACLES metinleri, `src/i18n` kart metinleri) web
reposunda kalır.

| TS testi | C# testi | Durum | Not |
| --- | --- | --- | --- |
| `tests/fixtures/builders.ts` | `Fixtures/Builders.cs`, `Fixtures/J.cs` | P0 | `Level/Compiled/InitialState(LevelSpec)`, `ExpectAscii` |
| — | `Fixtures/TestPaths.cs` | P0 (+P1, +P2) | repo kökü, `levels/`, `Golden/`, `Vectors` (p0), `P1Vectors`, `P2Vectors`; P2: `FixturesDir`, `FixturePath(group)`, `LoadFixture(group, name)`, `FixtureNames`, `LoadBoosterUnlock()` (`config/economy.json`) |
| `tests/fixtures/cover.ts` | `Fixtures/Cover.cs` | **P2** | `Cover.CoverLevel(seed)`: TS ile aynı RNG çağrı sırası (p2 vektörleri 40 tohumun `levelHash` değerini karşılaştırır) |
| `tests/obstacles/obstacles.fixtures.ts` | `Fixtures/ObstacleFixtures.cs` | **P2** | `CtxOf(s, rotatedAtStep8) → CapturedContext {Ctx, Events, Types()}`, `RankOf`, `FakeRule(id, …adlı kanca parametreleri)`, `Noop`; `i18nText` web reposunun `src/i18n`'ini okur, karşılığı yok |
| — (her TS test dosyasının kendi kopyaları) + `tests/core/moves.fixtures.ts` | `Fixtures/Boards.cs` | P1 (+P2, +P3) | P1 yardımcıları + P2: `LevelFile` = `Compiler.LoadLevel` (TS `levelFile`), `HandMoves(level)` (altın `log`), `Play(s, level, from, to)` / `Play(s, HandMove)`. P3: `DragTo` (TS `dragTo`), `Run(s, move, hooks?, strict = true, noTruckHelp, deadTable) → Ran {Res, Ev}` (TS `run`), `Types`, `Find<T>`, `AssertResult` / `AssertApplied`; `Drop(…) → DropResult` artık `Moves.ApplyMove` ile (iptal edilen bırakma fırlatır; adım 3/8/9 görünümleri olaylardan kurulur) |
| `tests/core/movement.test.ts` + `geometry.test.ts` hücre modeli | `Fixtures/CellModel.cs` | P1 | |
| `types.test.ts` | `TypesTests.cs` | P0 | |
| `rng.test.ts` | `RngTests.cs` | P0 | |
| `hash.test.ts` | `HashTests.cs` | P0 | |
| `state.test.ts` | `StateTests.cs` | P0 | |
| `grid.test.ts` | `GridTests.cs` | P0 | |
| `coords.test.ts` | `CoordsTests.cs` | P0 | |
| `ascii.test.ts` | `AsciiTests.cs` | P0 | |
| `shapes.test.ts` | `ShapesTests.cs` | P0 | |
| `level/compile.test.ts` | `CompileTests.cs` | P0 + P2 (12/11) | P2: "K-45 loadLevel runs schema + runtime checks and refuses invalid levels" eklendi (tam) |
| `tests/level/validator.test.ts` | `LogicTests.cs` | **P2** (103/103) | 62 geçersiz fikstür (her biri yalnız kendi kodunu verir) + 3 kapsama testi, 8 geçerli fikstür + 4, 19 mantık ayrıntısı (K-02 bandı, K-49 aralıkları, K-47 örtü, K-30 D3a, K-44, K-45/7 moloz, çalışma zamanı alt kümesi), 7 L-17 öğretici testi; fikstürler `Fixtures/Levels/{valid,invalid}` |
| `tests/level/schema.test.ts` | `SchemaTests.cs` | **P2** (20/21) | Şema yarıları tam. Belge karşılaştırmaları web reposunda kalır: GDD 14.1 `done` sözlüğü (16 olayın şema yarısı burada), UX 13.1 vurgu sözlüğü (örnek dizgeler burada), LEVELS 11–38 `done` satırları (atlandı) |
| `tests/level/mechanics.test.ts` | `MechanicsTests.cs` | **P2** (8/8) | İlk testin OBSTACLES.md satır karşılaştırması web reposunda; "İlk bölüm" sütunu sabit tabloyla doğrulanır |
| `tests/level/drafts.test.ts` | `DraftsTests.cs` | **P2** (11/12) | 10 bölüm mekanik geçmişi + economy.json ile 0 hata; yeni mekanikler 4→W1, 5→S1, 6→W2, 8→Y5, 9→W3; 8 GDD karşı örneği. LEVELS §2 özet satırı testi veri tutarlılığı testiyle (E bandı, N, K-47 tam örtü) değiştirildi; textKey ↔ LEVELS testi atlandı |
| `tests/level/validate-tool.test.ts` | — | taşınmaz | `tools/validate-levels.ts` CLI ve `tools/lib/levelsAllow.ts` web araçlarıdır (Unity'de karşılığı yok) |
| `tests/core/moves.test.ts` | `MovesTests.cs` | **P3** (28/28) | K-35 kanonik çözümlerde (Bölüm 5 kaydırma + teslimat + galibiyet, Bölüm 4 YAO 4/5), adım/seq sırası, olay günlüğü saflığı (`eventLogHash`), NULL_SINK; K-07 tablo satırları 1/2/3, maliyet toplamı, geçersiz kayıt (katı fırlatır), K-19 steer, `via`; adım 2–3 (K-11 düşüş, K-17 geri sekme, cam kırılması, harç yapışması + `MoveScratch.wasStuck`); adım 5–6 komşu etkileri ve gizli öğe denetimi; adım 10 zamanlayıcı sırası + "Phase 3"; adım 12; K-28/K-29 adım 11; K-41 hedefler; kanonik JSON + FNV-1a referans vektörleri |
| `tests/core/session.test.ts` | `SessionTests.cs` | **P3** (16/16) | K-39 Undo (GDD örneği, derinlik 1, kilitli blok, E-21, engeller), K-29 +5 (E-42, reklam yalnız 1. teklifte, 3 teklif sonrası kayıp, ret), K-43 çıkış (E-41 ücretsiz / kayıp), E-38 replay, pencere yeniden başlatmada korunur, `ReplayError` indeksi, 12 tohumlu rastgele oyun replay = canlı; K-40 başlangıç bonusları; `levelHash` / `RulesVersion` |
| `tests/core/fullcover.test.ts` | `FullCoverTests.cs` | **P3** (9/9) | K-47 derleme toplamları, K-44 Ağır Yük, K-48 galibiyet (bozuk veri, E-49, blok sayacı), K-41 S4 moloz (E-44), K-47 korunum: TS ile aynı boyut (`K47_LEVELS` 20 × `K47_SEEDS` 500, ortam değişkeniyle küçültülebilir) |
| `tests/golden/hand.test.ts` | `HandGoldenTests.cs` | **P3** (4/4 × 10 bölüm) | Transkripsiyon (ref/şekil/renk/geometri/bütçe/`levelHash`), hand = solver altını, kanonik çözüm `GameSession` (katı) ile oynanır: her adımda giriş türü, iniş, segment, FIFO teslimat, kuyruk, değişmezler; **`eventLogHash`, `finalAscii` ve `levelHash` TS değerleriyle 10/10 birebir**; replay = canlı |
| `tests/core/goals.test.ts` | `GoalsTests.cs` | P2 (5/5) | Bölüm 5 testi `Boards.Play` (= tam hat) ile |
| `tests/core/combo.test.ts` | `ComboTests.cs` | P2 + **P3** (8/8) | P3: `comboChanged`/`trowelEarned` olay sıraları, `boosterApplied`/`pieceLifted`, `legacyTrowel` reddi, E-09 mini hattı |
| `tests/core/boosters.test.ts` | `BoostersTests.cs` | P2 + **P3** (14/14) | P3: TS gibi hamle olarak (`Run(s, HAMMER(id))`, `CraneMove`, `PaintMove`): olay listeleri ve adımları, ret sonuçları (`EvJson` ile tam olay JSON'u), `won`, `MeasureYao`, K-39 Undo `GameSession` ile |
| `tests/core/deadlock.test.ts` | `DeadlockTests.cs` | P2 + **P3** (18/18) | P3: E-58, E-37 Söküm sonrası geri al, K-43 çıkış cezası, E-59 kepenk fazı, K-29 +5 sonrası D1, `teardown` olayının paketteki yeri, `MoveResult.TeardownCause`; `RulesVersion` = `GameSession.RulesVersion` |
| `tests/core/summary.test.ts` | `SummaryTests.cs` | P2 + **P3** (8/8) | `pack.Summary`, `pack.Teardown`, `game.Summary().BoosterTargets.Undo` `GameSession` ile |
| `tests/obstacles/registry.test.ts` | `Obstacles/RegistryTests.cs` | P2 + **P3** (21/21) | P3: iptal edilen seçim (`invalid`), `movesChanged` maliyet olayı (−3, base 2 glass 1), `mortarStuck`, adım 5 tarama sırası, adım 5 `screwCollected`, adım 10 olay sırası, S6 "Phase 3" hamle içinde ve `MoveHooks.None` ile; doğrudan kanca çağrıları C# eki olarak kalır |
| `tests/obstacles/W1_staticGap.test.ts` | `Obstacles/W1StaticGapTests.cs` | P2 + **P3** (9/9) | P3: K-46 YAO (Bölüm 4: 4/5, Bölüm 9: 3/4); i18n kart metni web reposunda |
| `tests/obstacles/S1_slidingSite.test.ts` | `Obstacles/S1SlidingSiteTests.cs` | P2 + **P3** (7/7) | P3: adım 8/9 olayları ve adımları, segment 2'de yerleşim, `GameSession` ile galibiyet (adım 11, 6 hamle kaldı, YAO 8/0), kuyruk ("Kamyonda: 1") olayları |
| `tests/obstacles/Y3_chain.test.ts` | `Obstacles/Y3ChainTests.cs` | P2 + **P3** (4/4) | Sürükleme ve çekiç hat üzerinden (`Run`) |
| `tests/obstacles/S2_planVoid.test.ts` | `Obstacles/S2PlanVoidTests.cs` | P2 + **P3** (10/10) | P3: olay listeleri, `won`, `legacyTrowel` reddi, Pencere tahtası `GameSession` ile (YAO 3/4, `wrongCount` 0) |
| `geometry.test.ts` (K-49, 9 geometri) | `GeometryTests.cs` + `GeometryTests.P1.cs` | P0 + P1 + **P3** (27/24 + kapı) | P3: 9 geometri × 6 bölüm × 40 hamle rastgele oyun değişmezleri + replay (`GameSession`, olay karması), E-56 `applyMove` yarısı, K-44 hat testi |
| `geometry.gate.test.ts` | `GeometryTests.cs` (kapı bölümü) | P0 + P1 | |
| `movement.test.ts` | `MovementTests.cs` | P1 + **P3** (56/56) | P3: "K-12 dragEnteredRail … Bölüm 4 gapPass" (`Run` ile kanonik hamle 1–2) |
| `gravity.test.ts` | `GravityTests.cs` | P1 (34/33) | |
| `placement.test.ts` | `PlacementTests.cs` | P1 (36/35) | |
| `site.test.ts` | `SiteTests.cs` | P1 + **P3** (5/4) | P3: K-22 iki test (adım 8/9 olay sırası, son segmentte galibiyet) ve K-15 `.` hücresindeki moloz; `completeIfDone` doğrudan testi C# eki |
| `panorama.test.ts` | `PanoramaTests.cs` | P1 + **P3** (4/4) | P3: segmentler `Run` ile tamamlanır; Bölüm 5 hamle 1–5 sonrası panorama (iki segmentin tüm satırları) |
| `delivery.test.ts` | `DeliveryTests.cs` | P1 + **P3** (8/8) | P3: E-03 kuyruk tahtası, `arrivedTurn`, Bölüm 5 kamyon partisi |
| `tests/review/movement.review.test.ts` | `MovementReviewTests.cs` + `MovementReviewTests.P3.cs` | P1 + **P3** (70/70) | P3 (18): K-07 maliyet/iptal, E-30, K-10 / K-11 / K-12 / K-13 hat örnekleri, vinç alanı geçişi, sonraki segment molozu, N14, Bölüm 1/4/9 eldivenleri, S2 `.` |
| `tests/review/placement.review.test.ts` | `PlacementReviewTests.cs` + `PlacementReviewTests.P3.cs` | P1 + **P3** (52/52) | P3 (19): K-17 / K-34 / K-22 hat örnekleri, K-18 "gölge = hamle sonucu" (300 serbest düşüş + 400 ray tahtası, TS ile aynı RNG sırası), S4 moloz hattı, K-14 / K-39 Undo; `MakeRandomBoard` TS `keepFree` / `maxBuilt` parametrelerini, `BuildBoard` duvarı alır |
| `tests/review/data.review.test.ts` | `DataReviewTests.cs` | **P4** kısmi (10/94) | Çekirdek yarısı: "review K-31 colour set and unlocks" (6: K-31 GDD örnekleri, renk açılışları her kaynak için, bölüm başına renk sınırı, çözülmüş `?`) ve "review K-27 material and K-25 truck batches" (4: Bölüm 5 parti 1 örtüsü + C3 W tuzağı, Ağır Yük arz değildir, W6 boya kapısı + `materialShortfall`, L-12 kamyon blokları). Kalan 84 test LEVELS §2 / GDD metinleriyle (belgeler) karşılaştırır; belgeler web reposunda |
| `tests/review/pipeline.review.test.ts` | `PipelineReviewTests.cs` | **P4** kısmi (47/90) | Senaryo describe'ları tam: K-25 / K-26 kamyon ve FIFO kuyruğu (tur 1–3: E-03, E-04, E-34, GDD örnekleri, "Kamyonda: N", K-48 (3)), K-28 / K-29 / K-35 adım sırası ve +5 penceresi (3 teklif sonrası kayıp, ret, reklam yalnız 1. teklifte), K-39 Undo (seri, mala, ray, +5, adım-12 RNG yardımı E-37, derinlik 1, bölüm bitti), K-43 çıkış ve devam (ücretsiz / kayıplı çıkış, bozuk günlük, E-42 RNG yardımı replay'i, Bölüm 5 önek önek devam, `levelHash`, replay alıcısı = canlı akış). Taşınmayan 43: K-22 / S1 (5), K-47 parti içeriği (3; K-27/K-47 `DataReviewTests`'te), K-33 (2), K-46 (1), K-35 adım 5 + K-41 moloz (2), W1 / S2 (7), tur 2 "other release kinds" (5) ve "K-22 / K-33 / W1 / S2 across segments" (6), tur 3 E-09 dilim kayması (3), W1/S1/S2 eklentileri (1), W1/S2 hat yerleşimi (3), K-35 adım 5/7/8/12 (5) — aynı kurallar `MovesTests`, `SiteTests`, `W1StaticGapTests`, `S2PlanVoidTests`, `ComboTests` ve fuzz ile kapsanıyor |
| `tests/tools/solver/checks.test.ts` (K-51, K-52) + `level10.test.ts` | `SolveStageTests.cs` | **P4** (9) | Çözücü (tools/solver) web aracıdır, taşınmaz. Unity verisi üzerinde denetlenebilen yarı: `movesBand` (tools/solver/metrics.ts) birebir; K-52 GDD örnekleri, tanıtım bölümü satırı, Bölüm 3 bandı 10–12, Bölüm 1–10 bütçesi çözücü altınının `min`'iyle bantta; K-51 madde 1 (`firstNeedDepth ≥ 1` = başlangıçta doğru yerleşim yok) C# hattında kaba kuvvetle (her seçilebilir bloğun her iptal etmeyen bırakması), GDD K-51 örneği (Bölüm 3, açık bloklar → derinlik 0); K-51 madde 4 / K-46 YAO ≥ 0,60 kanonik çözümde; E-54 / L-34 Bölüm 10 kanonik çözümünde kuyruk hep boş; K-51 madde 5 Bölüm 10 kamyonu kanonik hamle 8'de, 7 hamle kalır |
| `tests/tools/ruleCoverage.test.ts` + `tools/rule-coverage.ts` | `RuleCoverageTests.cs` + `Fixtures/RuleCoverage.cs` | **P4** (3/5) | `names` / `missingIds` birebir (C# tanımlayıcı biçimi `K49_…`, `…_E42_…` de sayılır); test adları yansımayla ("Fixture.Method" + `[TestCase(TestName)]`, yalnız yöntem adları — `Description` sayılmaz); kimlikler `Golden/rule_ids.json` (`node tools/gen-rule-ids.ts`, web `ruleIds`). Kapı: Faz 2R'ye kadar **76 kimlik** (K 46, engel 5, E 22, N 3) → eksik 0. Belge ayrıştırma ve CLI çıkış kodu testleri web aracına ait |
| — | `ScenarioTests.cs` | **P4** (8) | Söküm (K-30) + Undo (E-37) + aynı Söküm yeniden + galibiyet; son hamlede Söküm → adım 12 pencereden önce (E-58), Undo kapalı, +5, bitiş; Söküm sonrası kayıplı çıkış (E-26, movesSpent); Söküm başlangıç bonuslarını korur + Altın Mala geri alınmaz; E-03 kuyruk teslimi + Undo/yeniden; K-48 (3) kuyruk galibiyeti tutar + Undo/yeniden; iki pencerede Undo kapalı, teklif sonrası açık, 2. teklifte reklam yok, ret; seri/mala/derinlik/çıkış. Aynı betikler kehanette "scenario NN" dizileri olarak adım adım TS ile karşılaştırılır; buradaki beklenen sayılar o TS koşusunun değerleridir |
| — | `DiffFuzzTests.cs` | **P4** (1 + kehanet dizisi başına 1) | `tools/diff/ts-oracle.mjs` (node 22, web `src/core`'u içe aktarır) tohumlu, geçerli eylem dizileri oynar: 10 bölüm × 24 tohum (kanonik çözüm öneki; üçte biri "burn"), 20 cover × 3, 9 geometri × 3 rastgele bölüm, 9 geometri × 3 kalabalık kamyon tahtası × 2, 24 betikli senaryo (Söküm, D1 karıştırma — RNG'li denemeler ≥ 1 dahil, zincir çözme, teklif sonrası D1, +5, Undo, kuyruk). Her adımda Zobrist karması, tüm tamponun FNV'si, olay sayısı + `eventLogHash` + her olayın kanonik JSON'u, ASCII (id'li) ve oturum alanları; dizi sonunda günlük karması ve `Replay`. İlk sapmada adım, eylem, alan, ilk farklı olay ve iki tahta yan yana. Çıktı `tools/diff/out/` (git dışı); yoksa durumlar `Ignore` olur |
| — | `ReviewPhase2Tests.cs` | **İnceleme + P6** (30: 25 yeşil + 5 `Ignore` — R-03, R-04, R-09, R-10, R-11; P6: R-01, R-02, R-07, R-12 yeşile döndü, +`K30_D3aStats_expansions_are_per_thread`, +T-01 sabitleme (3 durum)) | Şüpheci inceleme (`docs/REVIEW_PHASE2.md`). Yeşil korumalar: JSON sayı/dize vektörleri (`JsNumber`, `JsonReader`, kanonik tırnaklama, `fnv1a64`; Unity Mono/IL2CPP için), çekirdek kültürden bağımsız (fi-FI, ar-SA, tr-TR, sv-SE), K-08 sürekli işaretçi takibi (114 sürükleme, 3 648 adım), D3a sonuç + açılım sayacı 5 bütçede (büyük 4 × 8 sahalar), şema düzenli ifadeleri ASCII'de, kesir/−0/büyük tohum, yinelenen anahtar. `Ignore("REVIEW_PHASE2 R-xx …")` = TS'den sapan, kırmızı bulgu testleri (R-01, R-02, R-03, R-04, R-07, R-09, R-10, R-11, R-12); düzelten `Ignore`'u kaldırır |
| — | `JsRegexTests.cs` | **P6** (9) | `JsRegex` JS anlamı: `$` yalnız girdi sonu, `\d` = 10 kod birimi, `\s` = tam 25 kod birimi (node 65 536 kod birimi taraması), `.` = 4 satır sonu hariç, `\w` = 63, desteklenmeyen kaçışlar, `Literal`, JS `trim`, `DigitsToInt`; `ParseAscii` JS boşluk davranışı (node `parseAscii` ile) |
| — | `CultureGuardTests.cs` + `AssemblyInfo.cs` | **P6** (1) | R-06: `[assembly: NUnit.Framework.SetCulture("")]` tüm testleri değişmez kültürde koşturur; kanarya test, bir koşucu özniteliği yok sayarsa 599 anlaşılmaz kırmızı yerine nedeni adıyla söyler |
| — | `P1VectorTests.cs` | P1 (12) | TS ↔ C# karma eşitliği (484 parça) |
| — | `P2VectorTests.cs` | P2 (12) | TS ↔ C# eşitliği: (1) 80 fikstür × 2 bağlam + 10 bölüm × 2 bağlam = 160 doğrulayıcı koşusu — mantık aşamasında kod/kural/denetim/önem/yol/**mesaj**, şema aşamasında kod/…/yol (zod'un kendi mesajları hariç), `tileLevel`, renk kümesi; (2) 10 bölümün kanonik çözüm boyunca TS `applyMove` ile üretilen 90 durumu (`encodeState` → `DecodeState`): D3a sonucu + açılım sayısı, D2, tespit, pre-check, D1 yüklemleri, holdable/needed/goals/queue/pending/carry/trowelPieces/targets/hammerTargets/front/rules, parça başına quick/hold/reach/correctSpots/craneSiteSpots/paintPartners/trowelSpots(±precheck)/hammerTargetKind/seçilebilirlikler, klon üzerinde `RunStep12` (olaylar + sonraki karma), doğrudan `Teardown`, kabul edilen +5 sonrası `RunStep12` (`step12offer:`; A3: `ReshuffleYard` ve `any:` satırı kalktı); (3) `Cover.CoverLevel(1…40)` `levelHash` + başlangıç tamponu + aynı sorgular. Üretici `tools/gen-p2-vectors.ts`. Negatif kontrol yapıldı: mesaj sözcüğü, reshuffle eşitlik kırıcısı ve vinç dönüş sırası bozulduğunda üç ayrı test üç ayrı doğru farkla düştü |
| — | `JsonTests.cs` | P0 | |
| — | `P3VectorTests.cs` | **P3** (3 + 170 dizi) | TS ↔ C# eşitliği: `tools/gen-p3-vectors.ts` TS `GameSession.apply` ile 170 tohumlu deneme oynar (Bölüm 1–10 × 8 tohum, kanonik çözümün tohuma bağlı bir önekinden sonra; Bölüm 1–10 × 3 "burn" dizisi: hamle tüketip +5 pencerelerini açar; 20 cover tahtası × 3 tohum) — sürükleme (erişilebilir düğüm, ham düğüm → `invalid`, başlangıç → `sameSpot`), çekiç / vinç / boya fırçası / Altın Mala (geçerli ve rastgele hedefler → her ret nedeni), eski hücre malası, geri al (`lossWindow`, `noDragMove`), +5 jeton/reklam (`adNotAllowed`), pencerede sürükleme (`notPlaying`), ret, çıkış (ücretsiz/kayıp); K-40 başlangıç bonusları (Termos, Mala Başlangıcı, Açık Kepenk, seri kademesi 0–2). C# saklanan girdileri oynatır ve her paket için aynı satırı kurar: sonuç (durum, neden, won, outOfMoves, Söküm nedeni), olay sayısı + `eventLogHash`, `teardown.seq`, kanonik `TurnSummary`, Zobrist karması + tüm tamponun FNV'si, oturum alanları (outcome, teklifler, undo engeli, sıradaki teklif, sayaçlar), YAO, değişmez sayısı. Sonra günlüğün kanonik JSON karması, `Replay` (tampon, olaylar, outcome), son ASCII; tüm dizilerde olay türü ve sonuç sayıları TS sayılarıyla aynı. Kapsam: 23 olay türü (Söküm 27, levelWon 61, outOfMoves 101, …), 4 298 girdi. Negatif kontrol: `colorsSwapped.aColor` alan adı ve reklam teklifi koşulu bozulduğunda ilgili diziler ayrı ayrı düştü |

P4'te ad değişikliği (gövde aynı, TS başlığındaki kimlik ada eklendi; kapsam kapısı yalnız yöntem adını sayar):
`MechanicsTests.K45_10_W3_derived_from_gap_size_any_gap_type_N2`, `MovementReviewTests.K05_Y5_N6_open_height_…`,
`SessionTests.K29_E42_…`, `DeadlockTests.K30_K29_E42_…`, `ComboTests.K33_E50_…`.

Toplam: 1 460 test (P1 sonunda 366; P2 sonunda 618; P3 sonunda 937; P4 +483: 406 fuzz durumu dahil — kehanet çıktısıyla; inceleme +26; P6 +14: 1 455 geçer, 5 `Ignore`),
`dotnet build -warnaserror` 0 uyarı; `fi_FI`, `ar_SA`, `sv_SE`, `tr_TR` yerel ayarlarında da 0 kırmızı.

### Paket F1 — inceleme turu (P4B-01), web `wip/phase-2r` (`6269c0c`)

Web, `wip/a2-levels`'i kırmızı testleriyle değil, yeşil `npm run check` ile (2 227 test) `wip/phase-2r`'ye birleştirdi; Faz 2R verisi web'de de arşivdir (`tests/fixtures/levels-2r/` = `Core.Tests/Archive2R`, bayt bayt aynı).
`tools/sync-from-web.sh` birleşik daldan yeniden koştu: `levels/`, `tests/golden/`, `economy.json`, `tokens.json`, fikstürler, çözümler **bayt bayt değişmedi** (kanıt); yalnız `i18n/{tr,en}.json` değişti (P4B-07).
`gen-p0…p3`, `gen-rule-ids` yeniden koştu: yalnız `webCommit` satırı değişti. Kehanet: `node tools/diff/ts-oracle.mjs <web>` 655 dizi / 16 376 eylem, `dotnet test` Core 1 882 geçer, 5 atlanır, 0 kırmızı.

| TS | C# | Not |
| --- | --- | --- |
| `level/mechanics.ts` `MECHANICS` S1 `firstLevel` 5 → 1 | `Level/Mechanics.cs` S1 `1`, `MechanicsTests.cs` | OBSTACLES "Veri imzası" ile eşit (yalnız tablo; doğrulayıcı kullanmaz) |
| `src/i18n/*.json` (`obs.s1.desc`, `lose.offer.gift`, `story.prologue.p2.king`; `tut.ctx.reshuffle` silindi) | `Resources/i18n/{tr,en}.json` (eşitlendi), `unity_{tr,en}.json` (gift örtüsü kalktı) | web kaynağı Usta Kral'ı taşır |
| — (Unity'ye özgü) | `Presentation.Core/Anim/ArtWarmup.cs`, `Unity/GameRunner.cs` | P4B-05: kral sayfaları `Awake`'te, fx sayfaları ana ekrandan çıkınca ya da 2,5 sn sakin ana ekrandan sonra çözülür (`ClipSprites.Warm`) |
| — (Unity'ye özgü) | `Presentation.Core/Shots/ShotScenario.cs` (`Motion`, `TimeScale`, `ShotOptions.MotionScale`), `tools/ci-shots/shoot.mjs` | P4B-08: iki yavaş çekim penceresi (`seq:king_wave`, `seq:king_big_cheer`), kare istatistiği + şerit PNG; P4B-05: 3B sayfa çözme süreleri manifest'e |
| — | `Editor/CiBuild.cs`, `ProjectSettings.asset`, `unity-webgl.yml` | P4B-04: Gzip + `decompressionFallback`, indirme boyutu bütçesi (§9) |

### Paket A3 — K-55 sabit saha (D-099), web `wip/a2-levels` (`e80a318`)

| TS | C# | Not |
| --- | --- | --- |
| `deadlock.ts` (`d1Help`, `runStep12`, `reshuffleYard` kalktı) | `Deadlock.cs` | `D1Help` → `D1Result.None/Freed/Stuck`; `Stuck` ise `RunStep12` Söküm `access` üretir (`PreAction` yoksa `null`); `ReshuffleYard`, `ReshuffleAttempts`, `AnyOneMoveCorrect`, `PlaceLowest/PlaceNearWall/...` silindi. `TruckHelpKinds.Reshuffle` / `Reshape` / `DeliverMissing` ve `TruckHelpEvent.Moves` tip birliğinde durur (olay silinmedi) |
| `moves.ts` `addMovesMove` | `Moves.cs` `AddMovesMoveRun` | `ApplyOptions.PreAction` teklif sonrası da okunur: verilmezse Söküm olmaz; Söküm olursa `MoveResult.TeardownCause` döner |
| `session.ts` `#lastPre` | `Session.cs` `lastPre` | `Commit` ayarlar, `Undo` ve `AcceptOffer` sıfırlar; `AcceptOffer` onu `PreAction` olarak geçirir |
| `level/schema.ts`, `level/logic.ts` | `Level/LevelData.cs`, `LevelJson.cs`, `Logic.cs` | `schemaVersion` 1 \| 2, `targets` bantları, L-36 (yukarıdaki satırlar) |
| `tests/core/fixedYard.test.ts` + `tests/level/schema.test.ts` (K-55) + `tests/core/deadlock.test.ts` (E-64) + `tests/services/save.test.ts` (E-67) | `FixedYardTests.cs`, `DeadlockTests.cs`, `Presentation.Core.Tests/GameAppTests.cs` (`E67_…`), `LevelPlayerTests.cs` (`K30_K55_E64_…`), `GameFeelTests.cs` (`K55_E62_…`) | E-62 … E-67, 200 tohumlu "sahaya blok girmez" oturumu, E-63 takas ve D3a Söküm, E-65 L-36, E-66 kuyruk |
| `levels/*.json` v1 (web `c1facf4`) + `tests/golden/level_NNN.*` | `Core.Tests/Archive2R/` | Arşiv. `TestPaths.LoadLevelJson` / `Boards.LevelFile` / `LoadGolden("level_…")` arşivi, `…Current…` yayın bölümlerini okur; `HandGoldenTests` her ikisini koşar (10 + 10) |
| `gen-p1/p2/p3`, `diff/ts-oracle.mjs` | aynı araçlar | `--archive=` seçeneği; P2'de `any:` / `reshuffle:` satırları yok, `step12offer:` var; oracle'da D1 betikleri "jam teardown" olarak yeniden adlandı, K-55 P1 senaryoları eklendi |
| Sunum | `LevelPlayer.HudStateOf` | "Kamyonda: N" rozeti yalnız `N > 0` iken görünür (E-62); `AnimationPlanner` olay güdümlüdür: sabit sahada `deliveryArrived` yalnız K-17 / K-36 kuyruğundan gelir, kamyon yolculuğu çıkmaz; reshuffle dalı ölü (not düşüldü) |

## Sunum çekirdeği (`Assets/MinikUsta/Presentation.Core`, Faz 3 Q0)

Motor-bağımsız sunum katmanı (asmdef `noEngineReferences`, netstandard2.1, C# 9; D-093, D-096). Kaynak gerçek: web
`src/scenes/level/**` (Phaser sunum kodu = davranış referansı), `src/theme/layout.ts`, `docs/JUICE.md`, `tokens.json`.
Unity katmanı (Q1, `Assets/MinikUsta/Unity`) yalnız bunları çağırır; kural kararı vermez.

| Web dosyası | C# dosyası | Durum | Not |
| --- | --- | --- | --- |
| Phaser `Tweens` (DOTween yerine, D-096) + `easing.ts` | `Tween/Easing.cs` | **Q0** | `EaseType` (16: Linear, In/Out/InOut Quad, Cubic, InBack, OutBack (1.70158), OutBounce, OutElastic (Phaser varsayılanı genlik 0.1 → 1, periyot 0.1), In/Out/InOut Sine, In/Out Expo); `Easing.Evaluate` Phaser 4.2.1 formülleri, uçlar tam 0 / 1 (Expo'da Phaser 1 − 2⁻¹⁰ verir; bilinçli sapma); `FromPhaserName` / `TryFromPhaserName` / `PhaserName` (tokens `easing.*` adları) |
| — | `Tween/Tweenable.cs`, `Tween/Tween.cs`, `Tween/Sequence.cs`, `Tween/TweenClock.cs` | **Q0** | `Tweenable` (Delay, Duration, Span, OnStart, OnComplete, Complete, Kill, Tag, `Goto(ms)` yalnız ileri), `Tween` (`SetEase`, `SetCurve`), `ITweenTarget` (Position tasarım px y aşağı, Scale, Rotation derece saat yönü tersi, Alpha), `Tweens.Value/Move(arcPx)/Scale/Squash/Pulse/Pop/PopCurve/Rotate/Fade/Shake/Nudge/Call`, `Sequence` (Append, Join, Insert, AppendInterval, AppendCallback, InsertCallback; uzun adım bütün iç sınırlarda bölünür → geri çağrılar zaman sırasında), `TweenClock` (`Advance(dtMs)`, `TimeScale`, `Play(t, elapsedMs)`, `Complete(tag)`, `Kill(tag)`, `IsRunning(tag)`) |
| `juice/catalog.ts` + `viewConstants.ts` (`VIEW`, `JUICE_VIEW`) + tokens `duration/easing/drag/physics/particles/a11y` | `Feel/FeelSettings.cs` | **Q0** | Tek veri sınıfı (`[Serializable]`, yalnız float/int/EaseType alanları; Q1 ScriptableObject sarar), `Validate()`, `Clone()`; varsayılanlar `FeelSettingsTests` ile tokens.json kopyasına karşı denetlenir |
| `motion.ts` (`fallDurationMs`, `fallDistanceAt`, `fallLeg`, `glideMs`) + `plan.ts` (`releaseFallMs`, `yardFallMs`, `dropRows`) | `Feel/FallMotion.cs` | **Q0** | `FallLeg.Progress` = `Tween.SetCurve` eğrisi (ivmeli düşüş, mesafe oranı) |
| `stuckPulse.ts` | `Feel/StuckPulse.cs` | **Q0** | GDD K-34 kanca 5: Kolay 6000 ms, Normal 12000 ms, Zor/Çok Zor yok, 2 nabız (kural değeri, FeelSettings'te değil) |
| `theme/layout.ts` (`adaptiveCellPx`, `boardLayout`, `createLayout`, `designHeight`, `fitViewport`, `hitArea`) | `Layout/LayoutSettings.cs`, `Layout/BoardLayout.cs` (`BoardGeometry`, `BoardLayout`, `ScreenLayout`), `Layout/ScreenFit.cs` (`ScaleMode`, `DesignViewport`) | **Q0** | UX §5.8 tablosu birebir; `ScreenLayout.OnBoard` = DragController `onBoard`; `DesignViewport` Unity ekranı (sol alt köken) ↔ tasarım px ↔ dünya (kamera tuvalin ortasında, 100 px = 1 birim), `OrthographicSize` |
| `hitTest.ts` | `Layout/HitTest.cs` | **Q0** | `PieceAtPoint(s, grid, px, py, slop)` (−1 = yok) |
| `dragMath.ts` | `Input/DragMath.cs` | **Q0** | `Grab`, `Edges`, `ExceedsThreshold`, `GrabAt`, `TargetAnchor`, `FingerFor` (ters; bot/test/debug), `EdgesOf`, `DrawAnchor`, `PathDurationMs`, `PathPoint` |
| `DragController.ts` (Phaser'sız yarısı) | `Input/DragMapper.cs` | **Q0** | `Down → PressResult` (K-09 kapısı + tek BFS), `Move/Update → DragFrame` (yeniden kullanılan nesne; K-08 takip, çizim çapası, BFS yol izleme, sinyaller), `Up → ReleaseResult` (Tap / Aborted / Drag + çekirdek `DragMove`), `Abort` |
| `juice/plan.ts` (`planMove`) | `Animation/AnimationStep.cs`, `Animation/AnimationPlanner.cs` | **Q0** | `StepKind` (57), `AnimationStep` (Kind, Juice, At, Ms, Ease, Lock, FastForward, Group, Event, Also, PieceId, Cells, N, First, Flag, ToSeg, Drops), `AnimationGroup`, `AnimationPlan` (`Steps`, `Groups`, `Ms`, `CoveredEvents`, `FromSteps`), `PlanContext.Capture(session)`, `AnimationPlanner.Plan(ActionResult | events, ctx, feel)`. Web P0 sırası birebir; 43 olay türünün hepsi bir adıma düşer (yoksa `Instant`); iki bilinçli ek: D-091 `BlocksChipFly` ve #57'nin #107'den sonra oynaması (JUICE §0 kural 10) |
| `juice/playback.ts` | `Animation/AnimationQueue.cs` | **Q0** | `Schedule`, `Advance`, `Locked`, `LockEnd`, `FastForward` (R-12), `SpeedUp` / `Rate` (3×), `Flush`, `Clear`, `Pending`, `Busy`, `LastStart` |

Testler (`Assets/MinikUsta/Presentation.Core.Tests`, `dotnet/MinikUsta.Presentation.Core.Tests`): `EasingTests`, `TweenTests`,
`FeelSettingsTests` (tokens.json eşlemesi), `FallAndHintTests` (JUICE §0.1, K-34 kanca 5), `BoardLayoutTests` (web
`boardLayout.test.ts`: UX §5.8 tablosu, EXPAND, 9 geometri, hit-test gidiş-dönüş, `ScreenFit`), `DragMapperTests` (web
`dragMath.test.ts` + Bölüm 1–10 kanonik çözümleri parmak simülasyonuyla → aynı `DragMove` ve TS `eventLogHash`),
`AnimationPlannerTests` (web `juice.test.ts` zamanlama testleri + 43 olay türü + JUICE kural 14 + saflık),
`AnimationQueueTests` (web oynatma testleri + gerçek plan uçtan uca). Veri: `Golden/tokens.json` (`tools/sync-from-web.sh`).

### Faz 3 Q1 — oynanabilir yer tutucu tahta (`Presentation.Core/Game`, `Art`, `Text` + `Assets/MinikUsta/Unity`)

Motor-bağımsız oyun ekranı (Presentation.Core, NUnit ile test edilir) + ince Unity katmanı (yalnız GameObject, sprite, uGUI;
bulutta `dotnet/UnityStubs` imza şimine karşı derlenir, gerçek derleme GameCI'da).

| Web dosyası | C# dosyası | Durum | Not |
| --- | --- | --- | --- |
| `LevelScene.ts` (oturum, girdi akışı, pencere kararları) + `DragController` host | `Game/LevelPlayer.cs` | **Q1** | `Load/Start/Restart/Next`, `SetScreen`, `PointerDown/Move/Up` (tasarım px), `Tick(dtMs)`, `Commit`, `PausePressed`, `OnPanelAction`; R-12 basışta `Clock.Complete(BoardTag)` → `Queue.FastForward` → `Clock.Complete(BoardTag)`, kilitte `SpeedUp`; K-18 gölgesi her düğüm değişiminde `Gravity.ComputeFall`; `End` → `SyncAll` + K-28 kazanma / K-29 hamle bitti penceresi (+5 teklifi yok, yalnız "Tekrar dene"); K-34 kanca 5 takılma nabzı; `EndSnaps` (End'de görünümün durumdan ne kadar saptığı; testler 0,5 px altını ister) |
| `EventPlayer.ts` `runCue` + `juice/handlers.ts` (P0 satırları) | `Game/StepRunner.cs` | **Q1** | `StepKind` → yerleşik tween: #8 yay ile eve, #9 sahaya iniş + y 0,94, #23 ray, #10 ivmeli düşüş (`FallMotion` eğrisi), #11 ezilme (Back.easeOut), #12 beyaz flaş, D-091 kalan blok çipine uçuş + çip pop, #13 kırmızı ton + 2 px × 3 titreme + 1,5 hücre yay (+ K-17 üstten düşüş), #88 kuyruğa küçülerek, #18 dilim (son dilimde yalnız 2 flaş; sonraki varsa panoramaya uçuş + yeni plan), #19 kamyon düşüşleri (60 ms arayla), #20 çipten çıkış, #44 avlu çağlayanı, #50/#51/#53 sayaç, #55 saha parlaması, #56 Bonus İnşaat geri sayımı, #107 Söküm, #61/#62/#63 güçlendirici görünümleri; engel olayları tahtayı yeniden kurar; karşılığı olmayanlar görsel üretmez, `End` durumu yeniden okur |
| `PieceLayer.ts` + `pieceState.ts` (`statePose`) | `Game/PieceViews.cs`, `Game/BoardPose.cs` | **Q1** | Havuzlu `IPieceVisual`; dinlenme pozu (avlu; görünür dilimde saha, asansör ofseti dahil); hücre komşuluk maskesi |
| `shadowLook.ts` | `Game/ShadowLook.cs` | **Q1** | Birebir port (`Of`, `CancelPreview`, `ShowsShadow`, `BadgeCell`) + `ShadowModel` (görünüm verisi, rozet merkezi) |
| `BoardView.ts` (yer tutucu) | `Game/BoardScene.cs` | **Q1** | Statik tahta öğe listesi: zemin gradyanı, vinç bandı, avlu çerçevesi + zemin, ozalit paneli, plan hücreleri (renk / `?` / `.` tarama), asansör platformu, duvar şeridi (`Ascii.WallChar`: duvar / açık geçit / kapalı geçit) |
| `theme/textures.ts` (prosedürel doku) | `Art/Atlas.cs`, `Art/Symbols.cs`, `Art/Palette.cs`, `Art/Rgba.cs` | **Q1** | Tek 1024² RGBA atlas, açılışta bir kez (≈ 85 ms .NET 8): 16 komşuluk maskeli blok hücresi, parlama, 8 renk sembolü (ART §3.1 sadeleştirilmiş), zemin, plan, tarama, `?`, gölge konturu, 9-dilim panel, rozetler, duraklat ve güçlendirici ikonları; renkler tokens.json'dan (`PaletteTests`) |
| `src/i18n` + `upper()` | `Text/Strings.cs` | **Q1** | `Resources/i18n/tr.json` düzleştirilir; `{n}` doldurma; `TrText.Upper` kültürsüz i → İ |
| — | `Game/HudLayout.cs`, `Game/LevelCatalog.cs`, `Game/DebugSolver.cs`, `Game/Views.cs` | **Q1** | HUD v3 ve pencere dikdörtgenleri (layout.popup: alttan çapalı); bölüm 1–10 döngüsü + `Compiler.LoadLevel` (geçersiz bölüm oynanmaz, D-005); dev: kanonik çözümün sıradaki hamlesi, yoldan çıkınca açgözlü doğru yerleşim; motor sınırı arayüzleri |
| Phaser sahnesi / kamera / girdi | `Assets/MinikUsta/Unity/*` | **Q1** | Dosya bazında tablo: bu bölümün altında (Q3). |

Testler (Q1): `LevelPlayerTests` (10 bölümün altın çözümü parmakla oynanır → TS `eventLogHash`, kazanma penceresi, End'de
0,5 px altı sapma; vinç alanından bırakmalar düşer ve kazanır; R-12 hızlı basış; K-18 gölge, zor bölümde nötr; K-09
titreme; K-07 iptal; dokunma zıplaması; K-22/K-25 kilit ve 3× hız; K-29 yalnız "Tekrar dene"; 10 → 1 döngüsü; mola;
geçersiz bölüm; K-34 kanca 5; K-49 her bölüm 4 ekran boyutunda sığar; belirlenimcilik; havuz), `DebugSolverTests`,
`AtlasTests`, `PaletteTests`, `BoardSceneTests`, `ShadowLookTests` (web `shadowLook.test.ts` portu), `StringsTests`,
`PreviewTests` (`Fixtures/Preview.cs`: Unity görünümünün yazılımsal kopyası; `MINIK_PREVIEW_DIR` ile PNG).

### Faz 3 Q2 — his katmanı (`Presentation.Core/Game`, `Art`, `Feel` + Unity ek dosyaları)

Brifteki "game feel" maddeleri D-091 eşlemesiyle bu oyunun terimlerine çevrildi; her süre, sayı ve ease `FeelSettings` alanıdır
(`Q2` başlıklı bölüm). Hepsi motor-bağımsız ve NUnit ile sınanır; Unity yalnız sprite / uGUI çizer.

| Brif maddesi | JUICE | C# | Not |
| --- | --- | --- | --- |
| (1) kaldırma %8 büyüme + gölge + parmak ofseti; bırakma / iniş OutBack | #1, #9, #10, #11, #23, #91 | `LevelPlayer.DragFrame` (Q1), `FeelSettings.ReleaseScaleEase`, `StepRunner` | Kaldırma Q1'de vardı; bırakınca 1,08 → 1,0 ölçeği Back.easeOut ile (`ReleaseEase`; azaltılmış harekette satırın kendi ease'i) |
| (2) kötü geçiş: hedefe gidip geri sekme + 8 px titreme | #13, #88 | `StepKind.PlaceWrong`, `FeelSettings.WrongShakePx` | **JUICE 2 px, brif 8 px**: varsayılan 8 (tek alan; 2 yazılırsa web değeri). Geri sekme sonunda 3 gri toz |
| (3) şantiye düşüşü: ivmeli düşüş, inişte ezilme + sekme, hücreler arası 20–30 ms | #10, #11, #44 | `Game/CellWave.cs`, `StepRunner.LandSquash`, `IPieceVisual.SetCellScale` | Hücre sırası sol-alt hücreden (x + y); gecikme `CellStaggerMs` 25, tavan 60 ms ve ezilmenin %60'ı (adım süresi değişmez); 10 toz |
| (4) doğru yerleşim: 1,0 → 1,2 büyüme + küçülerek kilitlenme + sprite patlaması + parlama | #12, #92 | `StepRunner.GrowPulse` (`IPieceVisual.Grow` kanalı: #11 ezilmesiyle çakışmaz), `ParticleField.Sparks` | JUICE'ta ölçek nabzı yoktu (Q0 notu); brife göre eklendi. 14 kıvılcım (blok rengi + beyaz) 120–220 px yukarı |
| (5) Altın Mala: seri boncukları dolar, mala pop | #15, #16, #107 | `StepRunner.Fx` (`StreakPip`, `TrowelEarned`, `StreakReset`), `HudLayout.Streak/Bead/TrowelIcon`, `ILevelView.SetStreak` | Boncuk 0,6 → 1,2 → 1; 4. boncukta boncuklar boşalır, mala 1,0 → 1,4 → 1,1 + 360° (Elastic), 30 altın kıvılcım yağmuru |
| (6) dilim bitişi + kamyon: şantiye kayması, yeni partinin düşüşü | #18, #19, #20 | `StepRunner.SiteSlideIn`, `TruckRide`, `TruckModel`, `ILevelView.SiteLayer / Truck` | Yeni şantiye sağ kenardan kayarak girer; kamyon 300 ms girer, kasa 12° kalkar, bloklar 60 ms arayla düşer + 4 toz, 200 ms'de çıkar |
| (7) Söküm: kavisli dönüş uçuşu + toz | #107 | `StepKind.Teardown` | Uçuş Q1'de vardı (yay 1,5 hücre); blok başına kalkışta ve inişte 4 toz |
| (8) kalan blok çipine kavisli uçuş + sayaç pop | D-091 | `StepKind.BlocksChipFly` (Q1) | Q2'de yalnız sınandı (`K48_…`: yay yüksekliği, çip 1,15 pop) |
| (9) kazanma: kalan hamleler teker teker altına + yıldız | #55, #56, #96 | `StepRunner.BonusBuild`, `WinFx`, `LevelPlayer.FlyCoin / StarFx`, `Game/Economy.cs` | Hamle başına bir sikke (en çok 10, `economy.json` `bonusCoinsPerMoveLeft` 3), altın çipi sayar ve pop eder; konfeti 2 × 40; ilk kazanmada yıldız 0 → 1,2 → 1 + 16 küçük yıldız + halka. Pencere son sikke inince açılır |
| (10) hareketsizlikte takılma nabzı: bloklar hafif parlar | K-34 kanca 5 | `LevelPlayer.StuckPulseCheck`, `FeelSettings.HintGlowPeak` | Süre kural değeri (Kolay 6, Normal 12 s; brifteki "5 sn" değil); nabza %30 beyaz parıltı eklendi |
| (11) düğmeler: basınca 0,94, bırakınca OutBack | #69 | `Game/PressFeel.cs`, `Unity/PressableButton.cs` | Duraklat ve pencere seçenekleri; dışarı kayan işaretçi basışı iptal eder (tık yok, pop yok) |
| (12) kamera sarsıntısı 120 ms + Android titreşimi | #18, #42, #55 | `LevelPlayer.CameraShake`, `Unity/OffsetTarget.cs`, `Game/Haptics.cs` | Dilim bitişi 3 px, cam kırılması 2 px, kazanma 3 px (JUICE kural 5 yalnız ilk ikisini sayar; `WinShakePx` 0 = kapalı); HUD sallanmaz; `Handheld.Vibrate()` yalnız Android'de ve yalnız orta ve üstü güçte |

Testler: `GameFeelTests` (44), `FeelSettingsTests` (`particles.*`, `duration.starBurst` eşlemesi), `AtlasTests` (yeni karolar).
Yazılımsal ön izleme: `MINIK_PREVIEW_DIR=… dotnet test --filter Preview_game_feel` → `q2-*.png` (patlama, iniş dalgası, mala, dilim kayması,
kamyon, sikkeler, konfeti, yıldız; 390×844 ve 360×800).

### Faz 3 Q3 — Unity katmanı dosya tablosu, web `src/scenes/level/**` kapsamı, veri yolları

Q3 denetimi kod değiştirmedi. Doğrulananlar: web `src/core`, `tests`, `levels`, `config` `4ce980f`'den bu yana değişmedi;
`tools/sync-from-web.sh` 0 sapma verdi; `Resources/` kopyaları web kaynağıyla bayt bayt eşit (`cmp`).

#### Unity katmanı (`Assets/MinikUsta/Unity`)

Asmdef `MinikUsta.Unity` → Core, Presentation.Core, `UnityEngine.UI`. Bulutta yalnız `dotnet/MinikUsta.Unity` (UnityStubs'a
karşı) derlenir; gerçek Unity derlemesi GameCI'da (`docs/CI.md`).

| Unity dosyası | Görev | Durum |
| --- | --- | --- |
| `GameBootstrap.cs` | `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`; aktif sahne `Level` ise `GameRunner` ekler. Sahne yalnız Main Camera içerir | Q1 |
| `GameRunner.cs` | MonoBehaviour: `Update` kare döngüsü, ekran boyutu değişimi, `LevelPlayer` kurulumu; Resources okuma (`levels/level_NNN`, `i18n/tr`, `solutions/level_NNN.solver`); geliştirme derlemesinde N/R/A/S (`Debug.isDebugBuild`) | Q1 |
| `PointerInput.cs` | Eski Input Manager (`activeInputHandler` 0): dokunma (`touchCount`, `GetTouch`) ve fare (`GetMouseButton*`); ekran px → tasarım px, aynı karede `LevelPlayer`'a | Q1 |
| `UnityLevelView.cs` | `ILevelView` uygulaması; `UnityFlyer` havuzu (`Stack`). Oyun mantığı yok | Q1 |
| `UnityBoardView.cs` | Statik tahta öğeleri → havuzlu `SpriteRenderer` (`Tile.Panel` 9 dilim). İç sınıf `UnityShadowView` (K-18 gölgesi) | Q1 |
| `UnityPieceVisual.cs` | `IPieceVisual`: hücre başına gövde (`Atlas.Body`), parlama, renk sembolü, kaldırma gölgesi. İç sınıf `UnityFlyer` (`IFlyer`, havuzlu uçan parça) | Q1 |
| `UnityHud.cs` | uGUI: ScreenSpaceOverlay Canvas, CanvasScaler 1080×1920 `Shrink` (= FIT), LegacyRuntime fontu, pencereler (kazanma, hamle bitti, mola). `UiTarget : ITweenTarget` | Q1 |
| `UnityConvert.cs` | `DesignSpace` (tasarım px, y aşağı ↔ dünya birimi, 100 px = 1 birim) ve `UnityConvert` (`Rgba` ↔ `Color`) | Q1 |
| `UnitySpark.cs`, `UnityUiSpark.cs` | `ISpark`: havuzlu sprite parçacığı (atlas karosu: kıvılcım, toz, yıldız, sikke, konfeti, halka); kazanma yıldızı patlaması uGUI üzerinde (üst katman tuvali sprite'ların üstünde çizilir) | Q2 |
| `UnityTruckView.cs` | `IPropView`: `TruckModel.Parts` → SpriteRenderer'lar (sıra 90–98, bloklardan arkada) | Q2 |
| `OffsetTarget.cs` | Bir Transform'un tasarım px ofseti (kamera titreşimi, şantiye katmanı kayması) | Q2 |
| `PressableButton.cs` | uGUI düğmesine `IPointerDown/Up/ExitHandler` ile `PressFeel` (0,94 / 1,04 → 1,0 Back.easeOut, JUICE #69) | Q2 |
| `ProceduralSprites.cs` | `Art/Atlas` piksellerini açılışta bir kez tek `Texture2D`'ye kopyalar; her sprite onun dilimi (tek malzeme) | Q1 |
| `Scenes/Level.unity` (+ `.meta`) | Elle yazılmış Unity 6 YAML. Yalnız ortografik Main Camera: arka plan `#1F1850`, boyut 9,6 (= 1920 / 2 / 100). GUID `a7cea9e5c5164f70b12f92c99359b9a4` | Q1 |
| `Editor/CiBuild.cs` (asmdef `MinikUsta.Editor`, yalnız Editor) | GameCI `BuildWebGL` / `BuildAndroid`; WebGL sıkıştırması kapalı | Q1 (CI) |

#### Presentation.Core ek satırları (Q3)

| Web / Phaser dosyası | C# dosyası | Durum | Not |
| --- | --- | --- | --- |
| — (Phaser konum çiftleri) | `Presentation.Core/Geometry2D.cs` (`Vec2`, `PxRect`) | Q0 | Tween hedefi ve düzen için değer tipleri; tasarım px, y aşağı |
| `theme/tokens.ts` | `Art/Palette.cs`, `Feel/FeelSettings.cs` | Q0 / Q1 | Renk ve süreler `tokens.json` kopyasından; `FeelSettingsTests` denetler |
| `theme/draw/*.ts` (art, badge, block, blockV2, board, characters, color, context, fx, kit, kitIcons, kitV3, path, plan, scene, symbols, wall) | `Art/Atlas.cs`, `Art/Symbols.cs` | Q1 kısmen | Yer tutucu prosedürel kutular ve 8 renk sembolü. blockV2, kitV3, karakter ve ikon tarifleri Q2+ |
| `src/scenes/` (level dışı: BootScene, HomeScene, IntroScene, AssetLoaderScene, flow/*, appServices, atlas, backGuard, shaderWarmup, touchCancel) | — | kapsam dışı (Q1) | Ana menü, giriş ve yükleme ekranları Unity'de yok; Level sahnesi doğrudan açılır |

#### Web `src/scenes/level/**` dosya kapsamı (Q3 denetimi, 40 TS dosyası)

Durum anlamı: **Q0 / Q1** taşındı · **kısmen** bazı satırlar taşındı · **Q2** / **Q2+** henüz yok (Q2 kapsamı / sonraki) ·
**kapsam dışı** Unity'de karşılığı yok.

| Web dosyası | C# karşılığı | Durum | Not |
| --- | --- | --- | --- |
| `LevelScene.ts` | `Game/LevelPlayer.cs` | Q1 | Oturum, girdi akışı, pencere kararları; +5 teklifi ve çıkış penceresi yok |
| `EventPlayer.ts` | `Game/StepRunner.cs`, `Game/StepRunner.Fx.cs` | Q1 + **Q2** | `runCue` ve tween adımları; Q2: parçacık, kamera sarsıntısı, titreşim, kamyon, seri, Bonus İnşaat; ses Q2+ |
| `juice/handlers.ts` | `Game/StepRunner.cs`, `Game/StepRunner.Fx.cs` | Q1 + **Q2** | P0 satırları (tween) + Q2 satırları (#9, #11, #12, #13, #15, #16, #18, #19, #44, #55, #56, #96, #107, #42: parçacık, sarsıntı, titreşim); ses yok |
| `juice/stage.ts` | `Game/Views.cs` (`ILevelView`, `IPieceVisual`, `IFlyer`) | Q1 | Arayüz karşılığı |
| `juice/plan.ts` | `Animation/AnimationPlanner.cs` | Q0 | 43 olay türü, K-35 |
| `juice/playback.ts` | `Animation/AnimationQueue.cs` | Q0 | |
| `juice/catalog.ts`, `viewConstants.ts` | `Feel/FeelSettings.cs`, `Layout/LayoutSettings.cs` | Q0 | |
| `motion.ts` | `Feel/FallMotion.cs` | Q0 | İvmeli düşüş eğrisi |
| `stuckPulse.ts` | `Feel/StuckPulse.cs` | Q0 | K-34 kanca 5 |
| `easing.ts` | `Tween/Easing.cs` | Q0 | |
| `DragController.ts` (Phaser'sız yarı) | `Input/DragMapper.cs` | Q0 | Phaser yarısı → `Unity/PointerInput.cs` |
| `dragMath.ts` | `Input/DragMath.cs` | Q0 | |
| `hitTest.ts` | `Layout/HitTest.cs` | Q0 | |
| `juice/dragFeel.ts` | — | Q2 | Sürükleme hızı, yeni temas, titreşim eşiği (titreşim henüz yok) |
| `PieceLayer.ts` | `Game/PieceViews.cs` | Q1 | Havuzlu parça görünümleri |
| `pieceState.ts` | `Game/BoardPose.cs` | Q1 | Dinlenme pozu |
| `PieceView.ts` | `Game/PieceViews.cs`, `Unity/UnityPieceVisual.cs` | Q1 kısmen | Gövde, parlama, sembol, kaldırma gölgesi, #12 beyaz flaş, #13 kırmızı ton. setCrop süpürmesi Q2 |
| `Pool.ts` | — (genel havuz sınıfı yok) | Q1 kısmen | Havuzlar `PieceViews` ve `UnityLevelView` içinde (`Stack`) |
| `shadowLook.ts` | `Game/ShadowLook.cs` | Q1 | Birebir port, `ShadowLookTests` |
| `ShadowView.ts` | `Game/ShadowLook.cs` (model), `Unity/UnityBoardView.cs` (`UnityShadowView`) | Q1 kısmen | Düşüş gölgesi var; iptal önizlemesi ve ↩ rozeti Q2 |
| `BoardView.ts` | `Game/BoardScene.cs`, `Unity/UnityBoardView.cs` | Q1 | Yer tutucu tahta |
| `depth.ts` | `Game/BoardScene.cs` (`Order`) | Q1 kısmen | Sıra sabitleri; ART §4 sırasının tamamı Q2'de denetlenecek |
| `levels.ts` | `Game/LevelCatalog.cs` | Q1 | `import.meta.glob` yerine Resources; `Compiler.LoadLevel` doğrular |
| `theme/textures.ts` | `Art/Atlas.cs` | Q1 | Prosedürel doku |
| `LevelWindows.ts` | `Game/LevelPlayer.cs`, `Unity/UnityHud.cs` | Q1 kısmen | Kazanma, hamle bitti, mola. +5 teklifi, çıkış onayı, Söküm ve kayıp penceresi Q2+ |
| `Trail.ts` | — | Q2 | İz (JUICE #3, #6, #10) |
| `ParticleLayer.ts`, `juice/particles.ts` | `Game/Particles.cs` (`ParticleField`), `Unity/UnitySpark.cs`, `Unity/UnityUiSpark.cs` | **Q2** | Havuzlu sprite parçacıkları, kapalı biçimli hareket (tween yok), tohumlu dağılım, ekranda ≤ 120 / patlama ≤ 40 (en eski patlama önce biter), azaltılmış hareket ≤ %20; ParticleSystem kullanılmaz |
| `juice/fx.ts` | — | Q2 | Genel zaman animasyonu çalıştırıcısı. Q1 efektleri StepRunner'da Tween ile |
| `Truck.ts` | `Game/TruckModel.cs`, `Unity/UnityTruckView.cs` | **Q2** | Yer tutucu kamyon (yuvarlatılmış kutular + tekerlekler); sağdan girer, kasası kalkar, bloklar düşer, sola çıkar (700 ms) |
| `TrowelPicker.ts` | — | Q2+ | Güçlendirici hedef seçimi; Q1'de güçlendiriciler devre dışı |
| `tutorial/TutorialController.ts`, `TutorialPresence.ts`, `glove.ts`, `highlights.ts`, `tutorialEvents.ts`, `TutorialView.ts` | `Tutorial/*` (paket C1, aşağıda) | **C1** | Öğretici (K-53) taşındı; `contextTips.ts` (bağlamsal `tut.ctx.*` satırları, K-53/4) ve blok hayaleti (DS-15) taşınmadı |
| `sceneServices.ts` | `Game/Haptics.cs` (`HapticRouter`, `FixedPulseHaptics`), `Unity/GameRunner.cs` | **Q2** kısmen | Titreşim: `Handheld.Vibrate()` yalnız Android'de; ses Q2+ |
| `frameImage.ts` | — | kapsam dışı | Phaser kare yardımcısı; Unity'de Sprite pivotu |

## Faz 4 — görsel ve akış (paket V0, V1)

### Paket V0 — v4 sanat paketi (`art/**`, `Resources/art`, atlas kontratı)

| Kaynak | Üretilen / Unity karşılığı | Not |
| --- | --- | --- |
| `art/src/**/*.svg` (karakter 8, ikon 22, logo 1, kasaba 1) | `art/gen/*.mjs` girdisi | Elle çizilen kaynak SVG; stil kitabı `docs/ART_REFERENCE.md` (design-lead) |
| `art/gen/{lib,blocks,board,bg,fx,icons,logo,ui,build}.mjs` | `art/svg/**`: blok 274, gölge 80, ui 43, ikon 33, hayalet 18, tahta 16, fx 11, karakter 8, logo 2, zemin 2, kasaba 1 | Üretici; `node art/gen/build.mjs` |
| `tools/art/rasterize.mjs`, `tools/art/png.mjs` | `Resources/art/atlas_0…3.bytes` (V0'da `atlas-N.png.bytes`; V1'de düz adla yeniden adlandırıldı, P3-13) | Kayıpsız PNG sayfaları; 2 px kenar payı (mipsiz) |
| `tools/art/preview.mjs` | `artifacts/preview/v4-*.png` (git'te `add -f`) | Yazılımsal önizleme; `/artifacts/` git dışı kalır |
| `docs/ART_UNITY.md` | `Resources/art/atlas.json` (kontrat) → `Presentation.Core/Art/AtlasData.cs` (V1) | Kare adları, pivot, PPU = 100 × ölçek, kenar (L, B, R, T) |

### Paket V1 — v4 sanat, HUD v3, pencereler, ana ekran, ayarlar, akış, kayıt, i18n

| Web kaynağı / belge | C# (`Presentation.Core`, motor-bağımsız, NUnit) | Unity katmanı (`Assets/MinikUsta/Unity`) |
| --- | --- | --- |
| ART_UNITY §3 `atlas.json` kontratı | `Art/AtlasData.cs` (ayrıştırma, Unity rect, ppu × scale, kenar (L, B, R, T)) | `AtlasSprites.cs` (TextAsset → `ImageConversion.LoadImage` → `Sprite.Create`), `SpriteDraw.cs` |
| ART_UNITY §4.1–4.2 blok, gölge | `Art/Frames.cs`, `Art/BlockSkin.cs` (hücre karosu + köşe dolgusu + parlama / harç; K-18 dolgu + çizgi + içbükey köşe) | `UnityPieceVisual.cs`, `UnityShadowView` (`UnityBoardView.cs`) |
| ART_UNITY §4.3 tahta | `Game/BoardScene.cs` (tepsi, vinç bandı ve kancası, zeminler, plan hayaletleri + K-34 cephesi, duvar + başlık, geçit kenarı + ray, iskele) | `UnityBoardView.cs` |
| — (acil geri dönüş) | `Art/FallbackTiles.cs` (kare yoksa prosedürel karo) | `ProceduralSprites.cs` |
| `src/ui/GoalsPanel.ts`, `MovesCounter.ts`, `Panorama.ts`, `PauseButton.ts`, `StatusStrip.ts`, `boosterSlots.ts` | `Ui/LevelHud.cs` (HUD v3 ağacı, K-54 yuva durumları), `Game/HudLayout.cs`, `Game/PlanThumb.cs` | `UiRenderer.cs` (uGUI: Image / Text + Outline + Shadow / Button) |
| `src/ui/Popup.ts`, `WinScreen.ts`, `LevelStartWindow.ts`, `windowLines.ts`, `losePreview.ts` | `Ui/WindowViews.cs` (bölüm başı, kazanma (yapı pencere içinde), +5 teklifi, kayıp, çıkış onayı, mola) | `UiRenderer.cs` |
| `src/ui/offer.ts`, `price.ts`, `rewards.ts` | `Meta/Offer.cs`, `Meta/Price.cs`, `Meta/Rewards.cs`, `Meta/EconomyConfig.cs` | — |
| `src/scenes/HomeScene.ts` (ana sayfa v2), ayarlar | `Ui/HomeView.cs` (`HomeView`, `SettingsView`) | `UiRenderer.cs` |
| `src/scenes/flow/**` (UX §12) | `Flow/ScreenFlow.cs` (durum makinesi), `Flow/GameApp.cs` (kayıt + akış + pencereler, `ILevelHost`) | `GameRunner.cs` |
| `src/services/save.ts` | `Save/SaveModel.cs`, `Save/SaveCodec.cs` (sürüm, yedek, bozuk → varsayılan, K-43 kaydı) | `PlayerPrefsStore.cs` |
| `src/services/i18n.ts` | `I18n/Localizer.cs` (tr/en + Unity katmanı `unity_tr.json` / `unity_en.json`), `Text/Strings.cs` (canlı tablo, eksik anahtar bildirimi) | `GameRunner.cs` (`Debug.LogWarning`) |

Yeni testler (`Presentation.Core.Tests`): `ArtContractTests` (atlas, kareler, blok / gölge parçaları, tahta), `MetaTests` (K-28, K-29, fiyat satırı),
`SaveCodecTests` (K-43), `I18nTests` (kaynak taraması: koddaki her anahtar tr ve en'de; Baloo 2 cmap'i her karakteri çizer), `FlowTests`,
`GameAppTests` (ilk açılış ≤ 3 dokunuş, kazanma ödülü, K-43 devam / iptal, K-29 teklifleri, K-43/2 çıkış, ayarlar, K-54 yuvaları, dokunma
hedefleri ≥ 128 px), `ScreenDumpTests` (compose sahneleri). Fikstürler: `AppRig`, `FakeSurface`, `SceneDump`, `TrueTypeCmap`.

Unity API'leri (her biri stub'da gerçek imzayla; UnityCsReference 6000.0.85f1 ve com.unity.ugui kaynağıyla doğrulandı): `TextAsset.bytes`,
`ImageConversion.LoadImage(Texture2D, byte[], bool)`, `PlayerPrefs.GetString / SetString / HasKey / DeleteKey / Save`, `Text.resizeTextMinSize /
resizeTextMaxSize / supportRichText`, `UnityEngine.UI.Shadow` (`effectColor`, `effectDistance`), `UnityEngine.UI.Outline`, `Selectable.Transition`
+ `Selectable.transition`. Önceden stub'ta olup V1'de yeni kullanılanlar: `Texture.width / height`, `SpriteRenderer.flipY`, `Renderer.enabled`,
`CanvasScaler.referencePixelsPerUnit`, `Image.preserveAspect`, `Object.Destroy`, `Transform` numaralandırması.

### Paket V1 — ek dosyalar (V3 denetimi)

V1'in üç commit'inin dosya listesi (`96112ae`, `564d838`, `81a314a`) yukarıdaki tablolarla karşılaştırıldı; aşağıdakiler eksikti.

| Dosya | Durum | Karşılık / görev |
| --- | --- | --- |
| `Presentation.Core/Game/Economy.cs` | değişti | Level ekranının ekonomi değerleri (`docs/META.md` §3, `config/economy.json` `levelRewards`; Faz 3 Q2) |
| `Presentation.Core/Game/PieceViews.cs` | değişti | Tahta blok görünümleri (web `PieceLayer.ts`): havuzlu `IPieceVisual`, görünür tahtadaki her parça için bir tane |
| `Presentation.Core/Game/Views.cs` | değişti | Motor-bağımsız oyuncu ile motor arasındaki dikiş (`ILevelView`; Faz 3 Q1, D-093, D-096) |
| `Presentation.Core/Ui/UiNode.cs` | değişti | Motor-bağımsız UI ağacı: HUD, pencereler, ana sayfa, ayarlar |
| `Presentation.Core/Ui/UiSurface.cs` | değişti | UI ağacı ile motor arasındaki dikiş (Unity'de `UiRenderer` uygular) |
| `Presentation.Core/Ui/FontMetrics.cs` | yeni (`564d838`) | `tools/font/gen-metrics.py` üretir (Baloo 2 `hmtx` ilerlemeleri); `FontMetricsTests` TTF ile karşılaştırır |
| `Presentation.Core/Ui/TextFit.cs` | yeni (`564d838`) | uGUI "best fit" yerine tek satır sığdırma (Baloo 2 1,6 em satır kutusu sorunu) |
| `Unity/PointerInput.cs` | değişti | Eski Input Manager ile dokunuş ve fare girdisi (D-096; `activeInputHandler` 0) |
| `Unity/UnityLevelView.cs` | değişti | `ILevelView` Unity nesneleri: ortografik kamera (1080 × 1920 tasarım tuvalinin FIT'i) |
| `Unity/UnityTruckView.cs` | değişti | Kamyon: `truck` karesi (ART_UNITY §4.3); kasa ayrı parça değil (bilinen sorun, `README.md`) |
| `Unity/UnitySpark.cs` | değişti | Havuzlu sprite parçacığı, atlas karesi (D-096: ParticleSystem yok) |
| `Unity/UnityUiSpark.cs` | değişti | uGUI üzerinde sprite parçacığı: kazanma penceresinin yıldız patlaması |
| `Unity/UnityHud.cs` | **silindi** (Q1'de tanımlıydı) | Yerine `Unity/UiRenderer.cs` (uGUI) ve `Ui/LevelHud.cs` ağacı |
| `Resources/fonts/Baloo2-ExtraBold.ttf`, `OFL.txt` | yeni | Baloo 2 ExtraBold, Latin alt küme (82 KB), OFL 1.1; `tools/font/build-font.sh` üretir |
| `tools/art/compose.mjs` | yeni | Önizleme: ScreenDump JSON + atlas sayfaları + Baloo 2 TTF → önizleme PNG'leri (yazılımsal, Unity ekran görüntüsü değil) |
| `tools/font/gen-metrics.py` | yeni (`564d838`) | Baloo 2 TTF'den `FontMetrics.cs` üretir |
| `tools/unity-refcheck.sh` | yeni | Referans derleme denetimi; kapıya bağlanmadı (`docs/CI.md` §6) |
| `dotnet/UnityStubs/UnityEngine.cs` (ve Android ikizi) | değişti | Yeni üyeler; her biri UnityCsReference 6000.0.85f1 ve com.unity.ugui kaynağıyla doğrulandı (Unity API paragrafı yukarıda) |
| `artifacts/preview/v4-unity-*.png` (14 dosya) | yeni | Yazılımsal önizleme (`tools/art/compose.mjs`); git'te `add -f`, `/artifacts/` git dışı kalır |

## Veri yolları (Q3 denetimi)

| Çalışma zamanı yolu | Dosya | Durum |
| --- | --- | --- |
| `Resources.Load<TextAsset>("levels/level_NNN")` | `Resources/levels/level_001…010.json` | 10 / 10 eşleşiyor |
| `…("i18n/tr")` | `Resources/i18n/tr.json` | Eşleşiyor |
| `…("solutions/level_NNN.solver")` (geliştirme S tuşu) | `Resources/solutions/level_001…010.solver.json` | 10 / 10 eşleşiyor |
| `…("config/economy")` | `Resources/config/economy.json` | Okunuyor (Q2: Bonus İnşaat alanları) |
| `…("i18n/en")` | `Resources/i18n/en.json` | Okunuyor (V1: `I18n/Localizer`; `I18nTests` kaynak taraması) |

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
| web `docs/GDD.md`, `docs/TECH_DESIGN.md` §12.4, `docs/OBSTACLES.md` (`tools/rule-coverage.ts` `ruleIds`) | `Assets/MinikUsta/Core.Tests/Golden/rule_ids.json` (181 kimlik, Faz 2R'ye kadar 76) | `node tools/gen-rule-ids.ts` |
| TS çekirdeği (fuzz kehaneti) | `tools/diff/out/*.json` + `manifest.json` (~12 MB, git dışı) | `node tools/diff/ts-oracle.mjs [/yol/minik-usta] [--seeds N] [--moves M]` |
| TS çekirdeği (inceleme vektörleri) | `Assets/MinikUsta/Core.Tests/Golden/review_json_vectors.json`, `review_follow_vectors.json`, `review_d3a_vectors.json` | `node tools/review/gen-json-vectors.ts`, `gen-follow-vectors.ts`, `gen-d3a-vectors.ts` (stdout → dosya; `docs/REVIEW_PHASE2.md`) |
| `levels/*.json`, `config/economy.json`, `src/i18n/{tr,en}.json`, `tests/golden/level_*.solver.json` | `Assets/MinikUsta/Resources/{levels,config,i18n,solutions}/` (oyun `Resources.Load<TextAsset>` ile okur, D-096) | `tools/sync-from-web.sh` (Faz 3 Q1) |
| `src/theme/tokens.json` (design-lead) | `Assets/MinikUsta/Presentation.Core.Tests/Golden/tokens.json` | `tools/sync-from-web.sh` (Faz 3 Q0; `FeelSettingsTests`) |
| TS çekirdeği (P3 vektörleri) | `Assets/MinikUsta/Core.Tests/Golden/p3_vectors.json` (~360 KB, dizi başına bir satır) | `node tools/gen-p3-vectors.ts` (`--verbose`: `p3_vectors.verbose.json`, her satır + her paketin kanonik olay JSON'u, git dışı; uyuşmayan dizinin C# satırları `Golden/p3_csharp.<dizi>.txt`, git dışı) |

Web reposu değişince: `bash tools/sync-from-web.sh`, `node tools/gen-p0-vectors.ts`, `node tools/gen-p1-vectors.ts`,
`node tools/gen-p2-vectors.ts`, `node tools/gen-p3-vectors.ts`, `node tools/gen-rule-ids.ts`, `node tools/diff/ts-oracle.mjs`,
`node tools/review/gen-*-vectors.ts > Golden/review_*.json`, `cd dotnet && dotnet test`; sonra bu dosyanın başındaki web commit'ini güncelle.

## Faz 4 — paket C1 (ses, öğretici, CI ekran görüntüsü, Android)

### Ses (V2'nin devamı)

| Web kaynağı | C# (`Presentation.Core/Audio`) | Unity katmanı |
| --- | --- | --- |
| `src/services/audio.ts` (`renderSound`, ZzFX) | `Audio/ZzFx.cs` (bit-eşit sentezleyici), `Audio/SoundBank.cs` (tokens `audio.sfx` / `audio.seq` → tampon; kare başına örnek bütçesi) | `Resources/audio/sounds.json` (`tools/audio/gen-sounds.mjs`) |
| `src/services/audio/catalog.ts` (`semitoneRate`, `dbGain`, `comboRate`, `landGain`, `coinRate`) | `Audio/AudioPlayer.cs` (`SoundFeel`, politika: kilit, "Ses" anahtarı, aynı ses 60 ms, en çok 4 ses, perde adımı = yeniden örneklenmiş klip) | `UnityAudioOutput.cs` (tek `AudioSource`, `AudioClip.Create` + `SetData`, `PlayOneShot`) |
| JUICE ses sütunu | `Game/StepRunner.cs` `Snd(...)` çağrıları, `Audio/Sounds.cs` (ad listesi) | `PressableButton` (düğme sesi + WebGL kilit açma) |

Test: `AudioTests` (web vektörleriyle karşılaştırma `Golden/audio_vectors.json`, `tools/audio/gen-vectors.mjs`). WebGL'de ses ilk kullanıcı girdisine kadar kilitlidir (`AudioPlayer.Unlock`
← `GameApp.PointerDown` / `UiPressed`).

### Öğretici (K-53, `Presentation.Core/Tutorial`)

| Web | C# | Not |
| --- | --- | --- |
| `TutorialController.ts` | `TutorialController.cs` (`TutorialFactory`, `ITutorialHost`, `TutorialReplay`) | Veri güdümlü: yalnız `levels/*.json` `tutorial[]`; bölüme özel kod yok. K-43 devamında adım, kayıt günlüğünden yeniden kurulur (Unity kaydı öğretici konumu tutmaz) |
| `tutorialEvents.ts` | `TutorialEvents.cs` (`MoveMatches`, kapalı sözlük + süzgeçler) | `overWall`, `gapPass`, `holdOverBuild` sürükleme sinyalleridir (`LevelPlayer.DragFrame`, `TutorialPlayer.HoldWatch`) |
| `TutorialPresence.ts` | `TutorialPresence.cs` + `TutorialSettings` (tokens `tutorial.*`, testle eşit) | bekle / göster / gizle / bitti; 4 sn görünür, 4 sn boşta yeniden; 3 yeniden gösterimden sonra balon yok |
| `glove.ts` | `TutorialGlove.cs` (`Plays`, `FingerPoints`, `RoundedPath`, `PathAt`, `TrailDots`) | DL-2R-20 oynatma koşulu çekirdek BFS ile |
| `highlights.ts` | `TutorialLayout.cs` (`Rects`, `PlaceBubble`) | Vurgu kimliği sözlüğü UX §13.1; balon yuvası üst / alt |
| `TutorialView.ts` | `TutorialView.cs` (UiNode ağacı: parlama, iz, balon, halka, eldiven) + `TutorialPlayer.cs` (yapıştırıcı) | `UiLayer.Tutorial` (HUD üstü, pencere altı); tek `ui.Show` içerik değişince, kare başına `Refresh` yalnız eldiven / halka |

Kancalar: `LevelPlayer.Tutorial` (`TouchDown/Up`, `DragLifted`, `DragSignal`, `Tapped`, `MoveEnded` = plan bittiğinde, `PanelChanged`, `LayoutChanged`, `Stop`, `Tick`).
**Usta Kral bağlama noktası (D-101):** balon portresi `TutorialSettings.PortraitFrame` (bugün `chr_dede_bust`); kral portresi atlas'a girince yalnız bu dize değişir. Metinler i18n'dedir
(`tut.*`; bazı `tut.ctx.*` satırları "evlat" der, Usta Dede sesidir, içerik sahibi günceller).

### CI ekran görüntüsü

`Shots/ShotScenario.cs` (`?shots=1` sürücüsü, `ShotOptions`), `tools/ci-shots/{shoot.mjs,publish.sh,shots.json}`, `.github/workflows/unity-webgl.yml` `screenshots` işi (bkz. `docs/CI.md` §7).
Unity API: `Application.absoluteURL` (yeni stub, UnityCsReference 6000.0 `Application.bindings.cs`), `CanvasScaler.scaleFactor` (stub'ta vardı; `UiRenderer` artık `ConstantPixelSize` kullanır).

### Android

`.github/workflows/unity-android.yml`, `Editor/CiBuild.cs` `BuildAndroid` / `ConfigureAndroid` (bkz. `docs/CI.md` §8).
