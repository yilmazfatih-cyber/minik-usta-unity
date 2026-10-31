/**
 * Generates Assets/MinikUsta/Core.Tests/Golden/p2_vectors.json from the TypeScript core (the source of truth, D-091) for the
 * P2 modules: level/logic (K-45 validator), goals, combo helpers, boosters, deadlock (D1/D2/D3a, Söküm, reshuffle), summary
 * and the obstacle registry.
 *
 * - `fixtures`: every tests/level/fixtures/{valid,invalid} file and levels 1–10 run through `validateLevelJson` under two
 *   contexts (default; "full" = empty mechanic history + fixed i18n keys + economy.json booster unlocks + fileId for levels);
 *   every issue (code, rule, check, severity, path, message), `tileLevel` and `levelColorSet` when the schema passes.
 * - `levels`: levels 1–10 played with the canonical solution (tests/golden hand.json) through `applyMove` (the full K-35
 *   pipeline, available here but not yet in C#); every state is stored as `encodeState` so the C# test decodes the same
 *   buffer and evaluates the P2 queries on it: D3a / D2 / detection, D1 predicates, correct spots, booster targets and
 *   effects per piece, goals, summary, registry; plus step 12 (`runStep12` with the previous state as Söküm target), a
 *   direct `teardown` and a `reshuffleYard` with an external seeded RNG, each on a clone (events compacted, hash after).
 * - `cover`: tests/fixtures/cover.ts `coverLevel(1…40)` level hashes and start-state queries (pins the C# Cover port).
 * Per state the query lines are hashed (FNV-1a 64); the first state of level 1 keeps the full lines as the format anchor.
 *
 * Usage: node tools/gen-p2-vectors.ts [/path/to/minik-usta] [--verbose]   (node 22 type stripping; no extra packages)
 * `--verbose` also writes every line of every state to p2_vectors.verbose.json (git-ignored).
 */
import { readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { execSync } from 'node:child_process';

const HERE = dirname(fileURLToPath(import.meta.url));
const ROOT = resolve(HERE, '..');
const args = process.argv.slice(2);
const verbose = args.includes('--verbose');
const WEB = resolve(args.find((a) => !a.startsWith('--')) ?? join(ROOT, '..', 'minik-usta'));
const web = (p: string): Promise<any> => import(pathToFileURL(join(WEB, p)).href);

const { LevelSchema } = await web('src/core/level/schema.ts');
const { compile, loadLevel } = await web('src/core/level/compile.ts');
const { validateLevelJson, tileLevel, levelColorSet, fitsFrame, levelSizes } = await web('src/core/level/logic.ts');
const { buildPlans } = await web('src/core/level/plan.ts');
const { deriveMechanics } = await web('src/core/level/mechanics.ts');
const { createInitialState, cloneState, encodeState, H, hdr, pieceZone, pieceShape, pieceColor } = await web('src/core/state.ts');
const { hashHex } = await web('src/core/hash.ts');
const { tryBeginDrag } = await web('src/core/movement.ts');
const { buildFront } = await web('src/core/placement.ts');
const { applyMove, fnv1a64 } = await web('src/core/moves.ts');
const { shapeByIndex } = await web('src/core/shapes.ts');
const { mulberry32, bufferRng } = await web('src/core/rng.ts');
const goals = await web('src/core/goals.ts');
const boosters = await web('src/core/boosters.ts');
const deadlock = await web('src/core/deadlock.ts');
const summary = await web('src/core/summary.ts');
const registry = await web('src/core/obstacles/registry.ts');
const { level } = await web('tests/fixtures/builders.ts');
const { coverLevel } = await web('tests/fixtures/cover.ts');
const { loadBoosterUnlock } = await web('tools/lib/levels.ts');

const I18N = { tr: new Set(['tut.m.dig', 'tut.m.free']), en: new Set(['tut.m.dig', 'tut.m.free']) };
const boosterUnlock = loadBoosterUnlock(WEB) ?? undefined;
const readJson = (p: string): unknown => JSON.parse(readFileSync(p, 'utf8'));
const levelPath = (n: number): string => join(WEB, 'levels', `level_${String(n).padStart(3, '0')}.json`);

// --- fixtures ------------------------------------------------------------------------------------------------------------

interface IssueVec {
  readonly code: string;
  readonly rule: string;
  readonly check: string;
  readonly severity: string;
  readonly path: string;
  readonly message: string;
}
const issueVec = (i: any): IssueVec => ({ code: i.code, rule: i.rule, check: i.check, severity: i.severity, path: i.path, message: i.message });

function fixtureVector(group: string, name: string, json: unknown, ctxName: string, ctx: any): any {
  const r = validateLevelJson(json, ctx);
  const out: any = { group, name, ctx: ctxName, schema: r.level === null, issues: r.issues.map(issueVec) };
  // a level outside the 8 × 10 frame (board_too_wide) has no geometry: L-28 stops the checks, tile / colours are undefined
  if (r.level && fitsFrame(levelSizes(r.level))) {
    const { plans } = buildPlans(r.level);
    out.tile = tileLevel(r.level, plans);
    out.colors = levelColorSet(r.level, plans);
  }
  return out;
}

const fixtures: any[] = [];
for (const group of ['valid', 'invalid']) {
  const dir = join(WEB, 'tests', 'level', 'fixtures', group);
  for (const f of readdirSync(dir).sort()) {
    if (!f.endsWith('.json')) continue;
    const name = f.replace(/\.json$/, '');
    const json = readJson(join(dir, f));
    fixtures.push(fixtureVector(group, name, json, 'default', {}));
    fixtures.push(fixtureVector(group, name, json, 'full', { previousMechanics: new Set(), i18nKeys: I18N, boosterUnlock }));
  }
}
// levels 1–10 with the mechanic history (drafts.test.ts `validateDraft`)
const history = new Set<string>();
for (let n = 1; n <= 10; n++) {
  const json = readJson(levelPath(n));
  fixtures.push(fixtureVector('levels', `level_${String(n).padStart(3, '0')}`, json, 'default', {}));
  fixtures.push(fixtureVector('levels', `level_${String(n).padStart(3, '0')}`, json, 'full', { fileId: n, previousMechanics: new Set(history), boosterUnlock, i18nKeys: I18N }));
  for (const m of deriveMechanics(LevelSchema.parse(json))) history.add(m);
}

// --- state queries ---------------------------------------------------------------------------------------------------------

const anchor = (a: any): string => `${a.ix},${a.iy}`;
const at = (a: any): string => `${a.zone}:${a.x},${a.y},${a.seg ?? '-'}`;
const list = (xs: readonly any[]): string => xs.join(',');
const bool = (b: boolean): string => (b ? '1' : '0');
const tplace = (p: any): string => (typeof p === 'string' ? p : at(p));

function eventLine(e: any): string {
  switch (e.t) {
    case 'deadlockDetected':
      return `deadlockDetected:${e.reason}`;
    case 'truckHelp':
      return `truckHelp:${e.kind}${e.moves ? ':' + e.moves.map((m: any) => `${m.pieceId}@${m.from.x},${m.from.y}>${m.to.x},${m.to.y}`).join(';') : ''}`;
    case 'chainReleased':
      return `chainReleased:${e.pieceId}`;
    case 'teardown':
      return `teardown:${e.toTurn}:${e.cause}:${e.pieces.map((p: any) => `${p.pieceId}:${tplace(p.from)}>${tplace(p.to)}`).join(';')}`;
    default:
      return `${e.t}`;
  }
}

function ctxOf(s: any, events: any[]): any {
  return {
    lvl: s.lvl,
    s,
    emit: (e: any) => events.push(e),
    rng: bufferRng(s.buf, H.rng),
    gravity: s.lvl.gravity,
    scratch: { wasStuck: false, rotatedAtStep8: false, affected: new Set() },
  };
}

/** Every P2 query on one state, one line per item. */
function stateLines(s: any, prevBuf: Int32Array | null, seed: number): string[] {
  const lvl = s.lvl;
  const hooks = registry.levelHooks(lvl);
  const lines: string[] = [];
  lines.push(`hash:${hashHex(s)}`);
  const tile = deadlock.tileRemaining(s);
  lines.push(`tile:${tile}:${deadlock.D3A_STATS.expansions}`);
  lines.push(`full:${bool(deadlock.isFullCover(lvl))}:imb:${deadlock.colorImbalance(s)}:detect:${deadlock.detectDeadlock(s, { table: null }) ?? '-'}:precheck:${bool(deadlock.precheckOk(s))}`);
  lines.push(`nomoves:${bool(deadlock.noMoves(s, hooks.drag))}:any:${bool(deadlock.anyOneMoveCorrect(s, hooks.drag, hooks.fall))}`);
  lines.push(`holdable:${list(summary.holdableIds(s, hooks))}`);
  lines.push(`needed:${list(deadlock.neededNow(s))}`);
  lines.push(`blocksLeft:${goals.blocksLeft(s)}:supply:${list(goals.remainingSupplyByColor(s))}:demand:${list(goals.remainingDemandByColor(s))}`);
  lines.push(`material:${bool(goals.materialLeft(s))}:segs:${bool(goals.allSegmentsComplete(s))}:extra:${bool(goals.extraGoalsMet(s))}:met:${bool(goals.levelGoalsMet(s))}`);
  lines.push(`goals:${goals.goalViews(s).map((g: any) => `${g.index}:${g.kind}:${g.value}/${g.target}:${bool(g.done)}`).join(';')}`);
  lines.push(`queue:${summary.queueEntries(s).map((q: any) => `${q.pieceId}:${q.shape}:${q.color}`).join(';')}`);
  const pend = summary.pendingCounts(s);
  lines.push(`pending:${pend.batches},${pend.blocks}:carry:${list(summary.carryIds(s))}`);
  lines.push(`trowelPieces:${list(boosters.trowelPieces(s, hooks))}`);
  const t = boosters.boosterTargets(s, hooks, false);
  lines.push(`targets:${bool(t.hammer)}${bool(t.crane)}${bool(t.undo)}${bool(t.paintBrush)}${bool(t.trowel)}`);
  lines.push(`hammerTargets:${boosters.hammerTargets(s, hooks).map((h: any) => ('pieceId' in h ? `p${h.pieceId}` : `o${h.obstacle}`)).join(',')}`);
  lines.push(`hasCrane:${bool(boosters.hasCraneTarget(s, hooks))}:hasPaint:${bool(boosters.hasPaintTarget(s))}`);
  lines.push(`front:${buildFront(s).map(at).join(';')}`);
  lines.push(`rules:${list(registry.activeRuleIds(lvl))}:info:${list(registry.infoKeysFor(lvl))}:hooks:${list(Object.keys(hooks).sort())}`);
  const P = lvl.layout.counts.pieces;
  for (let id = 0; id < P; id++) {
    const shape = shapeByIndex(pieceShape(s, id));
    const color = pieceColor(s, id);
    const spots = deadlock.correctSpots(s, shape, color).map(anchor).join(';');
    const crane = boosters.craneSiteSpots(s, id).map((c: any) => `${c.rotation}@${anchor(c.at)}`).join(';');
    lines.push(
      `p${id}:z${pieceZone(s, id)}:quick:${bool(deadlock.quickHoldable(s, id, hooks.drag))}:hold:${bool(deadlock.holdableIgnoringMoves(s, id, hooks.drag))}` +
        `:reach:${bool(deadlock.hasReachableCorrect(s, id, hooks.drag, hooks.fall))}:spots:${spots}:crane:${crane}` +
        `:paint:${list(boosters.paintPartners(s, id))}:trowel:${boosters.trowelSpots(s, id).map(anchor).join(';')}` +
        `:tp:${boosters.trowelSpots(s, id, { precheck: true }).map(anchor).join(';')}` +
        `:hk:${boosters.hammerTargetKind(s, { pieceId: id }, hooks) ?? '-'}:cs:${bool(boosters.craneSelectable(s, id, hooks))}` +
        `:ts:${bool(boosters.trowelSelectable(s, id, hooks))}:ps:${bool(boosters.paintSelectable(s, id))}`,
    );
  }
  // K-35 step 12 on a clone (the previous state is the Söküm target)
  {
    const copy = cloneState(s);
    const events: any[] = [];
    const cause = deadlock.runStep12(ctxOf(copy, events), hooks, {
      preAction: prevBuf ? Int32Array.from(prevBuf) : null,
      tiling: true,
      counterZero: hdr(copy, H.movesLeft) <= 0,
      table: null,
    });
    lines.push(`step12:${cause ?? '-'}:${events.map(eventLine).join('|')}:${hashHex(copy)}`);
  }
  // a direct Söküm back to the previous state
  if (prevBuf) {
    const copy = cloneState(s);
    const ev = deadlock.teardown(copy, Int32Array.from(prevBuf), 'tiling');
    lines.push(`teardown:${eventLine(ev)}:${hashHex(copy)}`);
  }
  // D1 reshuffle with an external seeded RNG (fresh randomness for attempts ≥ 1)
  {
    const copy = cloneState(s);
    const moves = deadlock.reshuffleYard(copy, mulberry32(seed), deadlock.neededNow(copy), hooks.drag, hooks.fall);
    lines.push(`reshuffle:${moves.map((m: any) => `${m.pieceId}@${m.from.x},${m.from.y}>${m.to.x},${m.to.y}`).join(';')}:${hashHex(copy)}`);
  }
  return lines;
}

function stateVector(name: string, s: any, prevBuf: Int32Array | null, seed: number, keepLines: boolean): any {
  const lines = stateLines(s, prevBuf, seed);
  const v: any = { name, buf: encodeState(s), count: lines.length, hash: fnv1a64(lines.join('\n')) };
  if (keepLines) v.lines = lines;
  return v;
}

// --- levels 1–10 along the canonical solution ------------------------------------------------------------------------------

const levels: any[] = [];
for (let n = 1; n <= 10; n++) {
  const json = readJson(levelPath(n));
  const loaded = loadLevel(json);
  if (!loaded.ok) throw new Error(`level ${n} does not load`);
  const lvl = loaded.level;
  const hand = JSON.parse(readFileSync(join(WEB, 'tests', 'golden', `level_${String(n).padStart(3, '0')}.hand.json`), 'utf8'));
  const moves = hand.log.filter((a: any) => a.kind !== 'start' && a.kind !== 'undo');
  const s = createInitialState(lvl);
  const states: any[] = [stateVector(`level ${n} start`, s, null, 1000 * n, n === 1 && !verbose ? true : verbose)];
  let prev: Int32Array = s.buf.slice();
  moves.forEach((m: any, i: number) => {
    const res = applyMove(s, m);
    if (res.status !== 'applied') throw new Error(`level ${n} move ${i} ${res.status} ${res.reason}`);
    states.push(stateVector(`level ${n} after move ${i + 1}`, s, prev, 1000 * n + i + 1, verbose));
    prev = s.buf.slice();
  });
  levels.push({ level: n, levelHash: hand.levelHash, states });
}

// --- cover levels ----------------------------------------------------------------------------------------------------------

const { levelHash } = await web('src/core/session.ts');
const cover: any[] = [];
for (let seed = 1; seed <= 40; seed++) {
  const lvl = compile(level(coverLevel(seed)));
  const s = createInitialState(lvl);
  cover.push({ seed, levelHash: levelHash(lvl.data), ...stateVector(`cover ${seed}`, s, null, 5000 + seed, verbose) });
}

let webCommit = '?';
try {
  webCommit = execSync('git rev-parse --short HEAD', { cwd: WEB }).toString().trim();
} catch {
  /* not a git checkout */
}
const out = {
  generatedFrom: { webRepo: 'yilmazfatih-cyber/minik-usta', webCommit, generator: 'tools/gen-p2-vectors.ts' },
  i18nKeys: [...I18N.tr],
  fixtures,
  levels,
  cover,
};
const target = join(ROOT, 'Assets', 'MinikUsta', 'Core.Tests', 'Golden', verbose ? 'p2_vectors.verbose.json' : 'p2_vectors.json');
writeFileSync(target, JSON.stringify(out, null, verbose ? 1 : 2) + '\n');
console.log(
  `wrote ${target} (web ${webCommit}, ${fixtures.length} fixture runs, ${levels.reduce((k, l) => k + l.states.length, 0)} level states, ${cover.length} cover levels)`,
);
