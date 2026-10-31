/**
 * Differential fuzz oracle (P4): the TypeScript core of the web repo (the source of truth, D-091) plays seeded random
 * VALID action sequences through `GameSession` and records, after every action, what the C# port must reproduce bit
 * for bit. Assets/MinikUsta/Core.Tests/DiffFuzzTests.cs reads the output, replays the same actions through the C#
 * `GameSession` and compares every step; the first divergence fails with the step, the action, both ASCII boards and
 * the first differing event.
 *
 * Actions (all valid for the current state, so every step is a real move or session action):
 * - drag: a pickable block (`tryBeginDrag` with the level's drag hooks) released on a node of its `DragSession`
 *   reachable set R that is not a start node and not a K-07 cancel (`classify`), site or yard by a seed bias;
 * - boosters: hammer (`hammerTargets`), crane (`craneSiteSpots` or a yard anchor with `craneRejection` = null), paint
 *   brush (`paintPartners`, `paintRejection` = null), Golden Trowel (`trowelPieces` / `trowelSpots`,
 *   `goldTrowelRejection` = null);
 * - session: Undo when `undoBlock()` is null (K-39), +5 offers in the out-of-moves window (coins, or the ad when
 *   allowed; K-29), decline (ends the attempt), exit (rare, K-43);
 * - start: pre-level boosters (K-40) and the streak tier from the seed.
 * Real levels 1–10 first play a seed-dependent prefix of the canonical solution (tests/golden hand.json) so late phases
 * (truck deliveries, segment shifts, wins) are reached; a third of the seeds is "burn"-biased (yard shifts) so the
 * out-of-moves window, offers, K-29 losses and Söküm (K-30) show up within the move cap.
 * Sources: levels 1–10, tests/fixtures/cover.ts full-cover boards, random levels in the 9 K-49 geometries (static gap,
 * yard gravity on/off). Every source file carries the level data (`level`) so C# compiles the very same level
 * (`levelHash` is compared).
 *
 * Per step (`steps[i]`): `a` action, `st` status (applied / cancelled / rejected, or exit / decline), `rs` reason,
 * `w` won, `oom` out of moves, `td` teardown cause, `z` Zobrist hash (hashHex), `b` FNV-1a of the whole buffer, `n`
 * event count, `ev` eventLogHash, `e` canonical JSON of every event, `ascii` toAscii(state, { ids: true }), session
 * fields `oc` outcome, `ml` moves left, `mm` moves made, `ms` moves spent, `ou` offers used, `ub` undo block, `x`
 * exit result. Step 0 is the start action. After the sequence: `log` (canonical JSON hash of the session log) and
 * `replay` (GameSession.replay: buffer hash, event hash, outcome).
 *
 * Usage: node tools/diff/ts-oracle.mjs [/path/to/minik-usta] [--seeds N] [--moves M] [--out DIR]
 *   --seeds N  seeds per real level (default 24; cover boards get N/8, random geometries N/8 levels each)
 *   --moves M  action cap per sequence, prefix included (default 40)
 * Output (git-ignored): tools/diff/out/<source>.json + tools/diff/out/manifest.json. Node 22 type stripping imports the
 * web repo's .ts files directly; no extra packages.
 */
import { mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { execSync } from 'node:child_process';

const HERE = dirname(fileURLToPath(import.meta.url));
const ROOT = resolve(HERE, '..', '..');
const argv = process.argv.slice(2);
const opt = (name, def) => {
  const i = argv.indexOf(name);
  return i < 0 ? def : argv[i + 1];
};
const positional = argv.filter((a, i) => !a.startsWith('--') && !(i > 0 && argv[i - 1].startsWith('--')));
const WEB = resolve(positional[0] ?? join(ROOT, '..', 'minik-usta'));
const SEEDS = Number(opt('--seeds', '24'));
const MOVES = Number(opt('--moves', '40'));
const OUT = resolve(opt('--out', join(HERE, 'out')));
if (!(SEEDS >= 1) || !(MOVES >= 1)) throw new Error('--seeds and --moves need positive integers');

const web = (p) => import(pathToFileURL(join(WEB, p)).href);
const { compile, loadLevel } = await web('src/core/level/compile.ts');
const { LevelSchema } = await web('src/core/level/schema.ts');
const { Zone } = await web('src/core/types.ts');
const { pieceZone, pieceShape } = await web('src/core/state.ts');
const { hashHex } = await web('src/core/hash.ts');
const { tryBeginDrag, isCancelled } = await web('src/core/movement.ts');
const { ArraySink, eventLogHash, fnv1a64, canonicalJson } = await web('src/core/moves.ts');
const { GameSession, levelHash } = await web('src/core/session.ts');
const { shapeByIndex, shapeById } = await web('src/core/shapes.ts');
const { mulberry32 } = await web('src/core/rng.ts');
const { toAscii } = await web('src/core/ascii.ts');
const boosters = await web('src/core/boosters.ts');
const { level } = await web('tests/fixtures/builders.ts');
const { coverLevel } = await web('tests/fixtures/cover.ts');

const pad = (n, w = 3) => String(n).padStart(w, '0');
const readJson = (p) => JSON.parse(readFileSync(p, 'utf8'));
const bufHash = (s) => fnv1a64(Array.from(s.buf).join(','));
const key = (n) => `${n.mode}:${n.ix},${n.iy}`;

/** Pre-level boosters (K-40) by seed; the streak tier is seed % 3. Same table as gen-p3-vectors.ts. */
const PRE = [[], ['trowelStart', 'thermos'], ['openShutter'], ['thermos', 'trowelStart', 'openShutter']];
/** Streak bonus (core cannot read economy.json): tier t → t moves, a trowel from tier 2 (C#: same function). */
const streakBonus = (tier) => ({ moves: tier, trowels: tier >= 2 ? 1 : 0 });

// --- random levels in the K-49 geometries (tests/core/geometry.test.ts `randomLevel`, copied: test code, not core) ----

const GEOS = [
  { wy: 4, hy: 4, ws: 2, hs: 5 },
  { wy: 4, hy: 4, ws: 2, hs: 6 },
  { wy: 4, hy: 5, ws: 2, hs: 5 },
  { wy: 4, hy: 5, ws: 2, hs: 7 },
  { wy: 6, hy: 5, ws: 2, hs: 6 },
  { wy: 6, hy: 5, ws: 2, hs: 7 },
  { wy: 6, hy: 8, ws: 2, hs: 8 },
  { wy: 5, hy: 7, ws: 3, hs: 7 },
  { wy: 4, hy: 6, ws: 4, hs: 6 },
];

function planRows(g, rng) {
  const colors = ['W', 'Y'];
  return Array.from({ length: g.hs }, () => Array.from({ length: g.ws }, () => colors[rng.nextInt(2)] ?? 'W').join(''));
}

function yardPieces(g, rng, fill) {
  const used = new Set();
  const out = [];
  const shapes = ['B1_0', 'D2_0', 'D2_90', 'O4_0', 'C3_0', 'C3_180'];
  for (let tries = 0; tries < 400 && used.size < fill * g.wy * g.hy; tries++) {
    const shape = shapeById(shapes[rng.nextInt(shapes.length)] ?? 'B1_0');
    if (shape.w > g.wy || shape.h > g.hy) continue;
    const x = rng.nextInt(g.wy - shape.w + 1);
    const y = rng.nextInt(g.hy - shape.h + 1);
    const cells = shape.cells.map((c) => `${x + c.x},${y + c.y}`);
    if (cells.some((c) => used.has(c))) continue;
    cells.forEach((c) => used.add(c));
    out.push([shape.id, rng.next() < 0.5 ? 'W' : 'Y', x, y]);
  }
  return out;
}

function randomLevelData(g, rng, id) {
  const h = Math.max(g.hy, g.hs);
  const height = rng.nextInt(h + 1);
  const gaps = height >= 3 && rng.next() < 0.5 ? [{ type: 'static', y: rng.nextInt(height - 2), size: 1 + rng.nextInt(2) }] : [];
  const gap = gaps[0];
  if (gap && gap.y + gap.size > height - 1) gaps.length = 0;
  return level({
    id,
    moves: 30,
    yard: { cols: g.wy, rows: g.hy },
    site: { cols: g.ws, rows: g.hs },
    plan: planRows(g, rng),
    pieces: yardPieces(g, rng, 0.55 + rng.next() * 0.3),
    wall: { height, gaps },
    gravity: { yard: rng.next() < 0.5 },
  });
}

/**
 * A crowded level with truck batches (K-25, K-26, E-03): 2–3 segments of 1–3 rows, the B1 blocks of segment 0 plus
 * filler blocks in a 75–95 % full yard, one batch of 3–6 blocks per later segment (random drop columns or none), so
 * deliveries queue and later yard moves deliver from the queue (step 9).
 */
function truckLevelData(g, rng, id) {
  const segs = 2 + rng.nextInt(2);
  const colors = ['W', 'Y'];
  const plan = Array.from({ length: segs }, () =>
    Array.from({ length: 1 + rng.nextInt(Math.min(3, g.hs)) }, () => Array.from({ length: g.ws }, () => colors[rng.nextInt(2)]).join('')),
  );
  const used = new Set();
  const pieces = [];
  const place = (shapeId, color, tries) => {
    const shape = shapeById(shapeId);
    for (let t = 0; t < tries; t++) {
      const x = rng.nextInt(g.wy - shape.w + 1);
      const y = rng.nextInt(g.hy - shape.h + 1);
      const cells = shape.cells.map((c) => `${x + c.x},${y + c.y}`);
      if (cells.some((c) => used.has(c))) continue;
      cells.forEach((c) => used.add(c));
      pieces.push([shapeId, color, x, y]);
      return true;
    }
    return false;
  };
  for (const row of plan[0]) for (const ch of row) if (!place('B1_0', ch, 200)) return null;
  const fill = 0.75 + rng.next() * 0.2;
  const fillers = ['B1_0', 'D2_0', 'D2_90', 'O4_0'];
  for (let t = 0; t < 300 && used.size < fill * g.wy * g.hy; t++) place(fillers[rng.nextInt(fillers.length)], colors[rng.nextInt(2)], 1);
  const batches = [];
  for (let k = 1; k < segs; k++) {
    const n = 3 + rng.nextInt(4);
    const bp = [];
    for (let j = 0; j < n; j++) {
      const sh = shapeById(fillers[rng.nextInt(fillers.length)]);
      bp.push([sh.id, colors[rng.nextInt(2)], rng.nextInt(g.wy - sh.w + 1), 0]);
    }
    const drop = rng.next() < 0.5 ? { dropColumns: [rng.nextInt(g.wy), rng.nextInt(g.wy)] } : {};
    batches.push({ forSegment: k, pieces: bp, ...drop });
  }
  return level({
    id,
    moves: 25,
    yard: { cols: g.wy, rows: g.hy },
    site: { cols: g.ws, rows: g.hs },
    plan,
    pieces,
    batches,
    gravity: { yard: rng.next() < 0.5 },
  });
}

/** The level data round-trips through JSON and the schema unchanged (C# parses the stored JSON). */
function roundTrips(data) {
  const parsed = LevelSchema.safeParse(JSON.parse(JSON.stringify(data)));
  return parsed.success && levelHash(parsed.data) === levelHash(data);
}

// --- valid actions ---------------------------------------------------------------------------------------------------

const pick = (rng, xs) => (xs.length === 0 ? undefined : xs[rng.nextInt(xs.length)]);

function dragAction(game, rng, burn) {
  const s = game.state;
  const geo = s.lvl.geo;
  const P = s.lvl.layout.counts.pieces;
  const first = rng.nextInt(Math.max(1, P));
  for (let k = 0; k < P; k++) {
    const id = (first + k) % P;
    const z = pieceZone(s, id);
    if (z !== Zone.yard && z !== Zone.site) continue;
    const a = tryBeginDrag(s, id, game.hooks.drag);
    if (!a.ok) continue;
    const d = a.session;
    const starts = new Set(d.startNodes.map(key));
    const nodes = d.reachableNodes().filter((n) => !starts.has(key(n)) && !isCancelled(d.classify(n)));
    if (nodes.length === 0) continue;
    const site = nodes.filter((n) => n.ix >= geo.siteX);
    const yard = nodes.filter((n) => n.ix < geo.siteX);
    const r = rng.next();
    const pool = burn ? (yard.length > 0 && r < 0.85 ? yard : nodes) : site.length > 0 && r < 0.6 ? site : nodes;
    const to = pick(rng, pool);
    if (to) return { kind: 'drag', pieceId: id, to: { ix: to.ix, iy: to.iy, mode: to.mode } };
  }
  return null;
}

function hammerAction(game, rng) {
  const t = pick(rng, boosters.hammerTargets(game.state, game.hooks));
  return t ? { kind: 'hammer', target: t } : null;
}

function craneAction(game, rng) {
  const s = game.state;
  const P = s.lvl.layout.counts.pieces;
  const ids = [];
  for (let id = 0; id < P; id++) if (boosters.craneSelectable(s, id, game.hooks)) ids.push(id);
  const id = pick(rng, ids);
  if (id === undefined) return null;
  const valid = (m) => boosters.craneRejection(s, m, game.hooks) === null;
  if (rng.next() < 0.65) {
    const spots = boosters.craneSiteSpots(s, id).map((sp) => ({ kind: 'crane', pieceId: id, to: { zone: 'site', x: sp.at.ix, y: sp.at.iy }, rotation: sp.rotation }));
    const m = pick(rng, spots.filter(valid));
    if (m) return m;
  }
  const shape = shapeByIndex(pieceShape(s, id));
  for (let t = 0; t < 12; t++) {
    const m = {
      kind: 'crane',
      pieceId: id,
      to: { zone: 'yard', x: rng.nextInt(Math.max(1, s.lvl.geo.wy - shape.w + 1)), y: rng.nextInt(Math.max(1, s.lvl.geo.hy - shape.h + 1)) },
      rotation: shape.rotation,
    };
    if (valid(m)) return m;
  }
  return null;
}

function paintAction(game, rng) {
  const s = game.state;
  const P = s.lvl.layout.counts.pieces;
  const pairs = [];
  for (let a = 0; a < P; a++) {
    if (!boosters.paintSelectable(s, a)) continue;
    for (const b of boosters.paintPartners(s, a)) if (boosters.paintRejection(s, a, b) === null) pairs.push([a, b]);
  }
  const p = pick(rng, pairs);
  return p ? { kind: 'paint', a: p[0], b: p[1] } : null;
}

function trowelAction(game, rng) {
  const s = game.state;
  const moves = [];
  for (const id of boosters.trowelPieces(s, game.hooks))
    for (const at of boosters.trowelSpots(s, id)) {
      const m = { kind: 'goldTrowel', pieceId: id, x: at.ix, y: at.iy };
      if (boosters.goldTrowelRejection(s, m, game.hooks) === null) moves.push(m);
    }
  return pick(rng, moves) ?? null;
}

function randomAction(game, rng, burn, step) {
  if (game.outcome === 'outOfMoves') {
    const offer = game.nextOffer();
    if (!offer || rng.next() < 0.12) return { kind: 'decline' };
    return { kind: 'addMoves', amount: 5, source: offer.adAllowed && rng.next() < 0.5 ? 'offerAd' : 'offerCoins' };
  }
  const r = rng.next();
  let a = null;
  if (r < 0.006 && step > 4) return { kind: 'exit' };
  if (r < 0.08) a = game.undoBlock() === null ? { kind: 'undo' } : null;
  else if (r < 0.14) a = hammerAction(game, rng);
  else if (r < 0.21) a = craneAction(game, rng);
  else if (r < 0.26) a = paintAction(game, rng);
  else if (r < 0.34) a = trowelAction(game, rng);
  return a ?? dragAction(game, rng, burn);
}

// --- one sequence ----------------------------------------------------------------------------------------------------

const totals = { sequences: 0, steps: 0, actions: {}, statuses: {}, events: {}, outcomes: {} };
const bump = (m, k) => (m[k] = (m[k] ?? 0) + 1);

function sessionFields(game) {
  return {
    oc: game.outcome,
    ml: game.movesLeft,
    mm: game.movesMade,
    ms: game.movesSpent,
    ou: game.offersUsed,
    ub: game.undoBlock() ?? null,
  };
}

function snapshot(game, events) {
  for (const e of events) bump(totals.events, e.t);
  const s = game.state;
  return {
    z: hashHex(s),
    b: bufHash(s),
    n: events.length,
    ev: eventLogHash(events),
    e: events.map((e) => canonicalJson(e)),
    ascii: toAscii(s, { ids: true }),
    ...sessionFields(game),
  };
}

function playSequence(name, lvl, seed, prefixMoves, burn, script = null) {
  let h = 2166136261;
  for (const ch of name) h = Math.imul(h ^ ch.charCodeAt(0), 16777619) >>> 0;
  const rng = mulberry32(h);
  const preBoosters = script ? (script.preBoosters ?? []) : PRE[seed % PRE.length];
  const streakTier = script ? (script.streakTier ?? 0) : seed % 3;
  const startSink = new ArraySink();
  const game = GameSession.start(lvl, { preBoosters, streakTier }, { streakBonus }, startSink);
  const steps = [{ i: 0, a: { kind: 'start', preBoosters, streakTier }, st: 'start', ...snapshot(game, startSink.events) }];
  const prefix = prefixMoves.length === 0 ? 0 : seed % (prefixMoves.length + 1);
  const cap = script ? script.actions.length : MOVES;
  for (let k = 0; k < cap; k++) {
    if (game.outcome === 'won' || game.outcome === 'lost' || game.outcome === 'exited') break;
    const a = script ? script.actions[k] : k < prefix ? prefixMoves[k] : randomAction(game, rng, burn, k);
    if (!a) break;
    bump(totals.actions, a.kind);
    const i = steps.length;
    if (a.kind === 'exit') {
      const x = game.exit();
      steps.push({ i, a, st: 'exit', x, ...snapshot(game, []) });
      bump(totals.statuses, 'exit:' + x.kind);
      break;
    }
    if (a.kind === 'decline') {
      game.declineOffer();
      steps.push({ i, a, st: 'decline', ...snapshot(game, []) });
      bump(totals.statuses, 'decline');
      break;
    }
    const r = game.apply(a);
    bump(totals.statuses, `${a.kind}:${r.status}${r.reason ? ':' + r.reason : ''}`);
    steps.push({
      i,
      a,
      st: r.status,
      rs: r.reason ?? null,
      w: r.won,
      oom: r.outOfMoves,
      td: r.teardownCause ?? null,
      ...snapshot(game, r.events),
    });
  }
  bump(totals.outcomes, game.outcome);
  const replaySink = new ArraySink();
  const replayed = GameSession.replay(lvl, game.log, { streakBonus }, replaySink);
  totals.sequences++;
  totals.steps += steps.length - 1;
  return {
    name,
    seed,
    burn,
    prefix,
    preBoosters,
    streakTier,
    steps,
    log: { length: game.log.length, hash: fnv1a64(canonicalJson(game.log)) },
    replay: { b: bufHash(replayed.state), ev: eventLogHash(replaySink.events), oc: replayed.outcome },
  };
}

// --- sources ---------------------------------------------------------------------------------------------------------

rmSync(OUT, { recursive: true, force: true });
mkdirSync(OUT, { recursive: true });
const files = [];

function writeSource(file, source, data, sequences) {
  const body = {
    source,
    levelHash: levelHash(data),
    level: data,
    sequences,
  };
  // one sequence per line keeps the file greppable
  const text = JSON.stringify({ ...body, sequences: [] }).replace(/"sequences":\[\]\}$/, `"sequences":[\n${sequences.map((q) => JSON.stringify(q)).join(',\n')}\n]}`);
  writeFileSync(join(OUT, file), text + '\n');
  files.push({ file, source, sequences: sequences.map((q) => q.name), steps: sequences.reduce((n, q) => n + q.steps.length - 1, 0) });
}

for (let n = 1; n <= 10; n++) {
  const json = readJson(join(WEB, 'levels', `level_${pad(n)}.json`));
  const loaded = loadLevel(json);
  if (!loaded.ok) throw new Error(`level ${n} does not load`);
  const lvl = loaded.level;
  const hand = readJson(join(WEB, 'tests', 'golden', `level_${pad(n)}.hand.json`));
  const canonical = hand.log.filter((a) => a.kind === 'drag');
  const seqs = [];
  for (let seed = 1; seed <= SEEDS; seed++) {
    const burn = seed % 3 === 0;
    seqs.push(playSequence(`level ${pad(n)} seed ${pad(seed, 2)}${burn ? ' burn' : ''}`, lvl, seed, burn ? [] : canonical, burn));
  }
  writeSource(`level_${pad(n)}.json`, { kind: 'level', n }, lvl.data, seqs);
}

const COVER = 20;
const coverSeeds = Math.max(1, Math.round(SEEDS / 8));
for (let n = 1; n <= COVER; n++) {
  const data = level(coverLevel(n));
  if (!roundTrips(data)) throw new Error(`cover ${n} does not round-trip through JSON`);
  const lvl = compile(data);
  const seqs = [];
  for (let seed = 1; seed <= coverSeeds; seed++) seqs.push(playSequence(`cover ${pad(n)} seed ${pad(seed, 2)}`, lvl, seed, [], seed % 2 === 0));
  writeSource(`cover_${pad(n)}.json`, { kind: 'cover', n }, data, seqs);
}

const geoRng = mulberry32(0x4b2d_2049);
const perGeo = Math.max(1, Math.round(SEEDS / 8));
let geoLevels = 0;
for (let g = 0; g < GEOS.length; g++) {
  for (let k = 0; k < perGeo; k++) {
    let data = null;
    for (let t = 0; t < 8 && data === null; t++) {
      const d = randomLevelData(GEOS[g], geoRng, 3);
      if (roundTrips(d)) data = d;
    }
    if (data === null) continue;
    const lvl = compile(data);
    const geo = GEOS[g];
    const label = `${geo.wy}x${geo.hy}_${geo.ws}x${geo.hs}`;
    const n = g * perGeo + k + 1;
    const seq = playSequence(`geo ${label} level ${pad(k + 1, 2)}`, lvl, k + 1, [], k % 2 === 1);
    writeSource(`geo_${pad(n)}.json`, { kind: 'geo', n, label }, data, [seq]);
    geoLevels++;
  }
}

const truckRng = mulberry32(0x7275_636b);
const perTruck = Math.max(1, Math.round(SEEDS / 8));
let truckLevels = 0;
for (let g = 0; g < GEOS.length; g++) {
  for (let k = 0; k < perTruck; k++) {
    let data = null;
    for (let t = 0; t < 16 && data === null; t++) {
      let d = null;
      try {
        d = truckLevelData(GEOS[g], truckRng, 12);
      } catch {
        d = null; // the spec broke the schema: draw again
      }
      if (d && roundTrips(d)) data = d;
    }
    if (data === null) continue;
    const lvl = compile(data);
    const geo = GEOS[g];
    const label = `${geo.wy}x${geo.hy}_${geo.ws}x${geo.hs}`;
    const n = g * perTruck + k + 1;
    const seqs = [];
    for (let seed = 1; seed <= 2; seed++) seqs.push(playSequence(`truck ${label} level ${pad(k + 1, 2)} seed ${pad(seed, 2)}`, lvl, seed + k, [], seed === 2));
    writeSource(`truck_${pad(n)}.json`, { kind: 'truck', n, label }, data, seqs);
    truckLevels++;
  }
}

// --- scripted scenarios (P4 item 3): Söküm / deadlock, +5 offers, Undo, truck queue -----------------------------------
// Fixed action lists on the fixtures of tests/core/deadlock.test.ts and tests/review/pipeline.review.test.ts (copied:
// test data, not core). The C# test compares them step by step like the random sequences; ScenarioTests.cs asserts
// their GDD meaning.

const D = (pieceId, ix, iy, mode = 0) => ({ kind: 'drag', pieceId, to: { ix, iy, mode } });
const UNDO = { kind: 'undo' };
const COINS = { kind: 'addMoves', amount: 5, source: 'offerCoins' };
const AD = { kind: 'addMoves', amount: 5, source: 'offerAd' };
const EXIT = { kind: 'exit' };
const DECLINE = { kind: 'decline' };
const fullColumn = (x) => [['I4_0', 'Y', x, 0], ['I4_0', 'Y', x, 4]];
const column7 = (x) => [['I4_0', 'Y', x, 0], ['I3_0', 'Y', x, 4]];
const TILING = {
  wall: { height: 2 },
  site: { cols: 2, rows: 4 },
  plan: ['WW', 'WW', 'YW', 'YW'],
  pieces: [['D2_0', 'Y', 0, 0], ['D2_0', 'W', 1, 0], ['O4_0', 'W', 2, 0]],
};
const E26 = { wall: { height: 2 }, site: { cols: 2, rows: 2 }, plan: ['WY', 'WW'], pieces: [['D2_0', 'W', 0, 0], ['B1_0', 'W', 2, 0], ['B1_0', 'Y', 3, 0]] };
const FIFO = {
  plan: [['W.'], ['W.'], ['W.']],
  pieces: [['B1_0', 'W', 0, 7], ['B1_0', 'W', 5, 7], ...column7(0), ...column7(5), ...fullColumn(1), ...fullColumn(2), ...fullColumn(3), ...fullColumn(4)],
  batches: [{ forSegment: 1, pieces: [['B1_0', 'R', 0, 8], ['B1_0', 'R', 0, 8]] }, { forSegment: 2, pieces: [['B1_0', 'G', 5, 8]] }],
};
const E03 = {
  plan: [['W.'], ['WW']],
  pieces: [['B1_0', 'W', 5, 7], ...column7(5), ...fullColumn(2), ...fullColumn(3), ...fullColumn(4), ['I4_0', 'Y', 0, 0], ['D2_0', 'Y', 0, 4], ['I4_0', 'Y', 1, 0], ['D2_0', 'Y', 1, 4], ['B1_0', 'Y', 1, 6]],
  batches: [{ forSegment: 1, pieces: [['O4_0', 'R', 0, 8]] }],
};
const WIN_WITH_TRUCK = {
  plan: [['W.'], ['W.']],
  pieces: [['B1_0', 'W', 0, 7], ...column7(0), ...fullColumn(1), ...fullColumn(2), ...fullColumn(3), ...fullColumn(4), ...fullColumn(5)],
  batches: [{ forSegment: 1, pieces: [['B1_0', 'W', 0, 8], ['O4_0', 'R', 0, 8]] }],
};
const STREAK = {
  moves: 20,
  wall: { height: 2 },
  plan: ['WW', 'WW', 'WW', 'WW', 'WW'],
  pieces: [['D2_0', 'W', 0, 0], ['D2_0', 'W', 1, 0], ['D2_0', 'W', 2, 0], ['D2_0', 'W', 3, 0], ['B1_0', 'Y', 4, 0], ['B1_0', 'W', 5, 0]],
};
const shuttle = (n, from = 1) => Array.from({ length: n }, (_, i) => D(5, 5, (from + i) % 2));

const SCENARIOS = [
  // K-30 Söküm (tiling dead end) → E-37 Undo → the same Söküm again → the right placement → win
  { name: 'teardown tiling undo redo win', spec: TILING, actions: [D(0, 6, 8), D(1, 6, 8), UNDO, D(1, 6, 8), D(1, 7, 8), D(2, 6, 8)] },
  // K-30 Söküm on the last move: step 12 before the window (E-58) → +5 (K-29) → finish
  { name: 'teardown on the last move then offer', spec: { ...TILING, moves: 2 }, actions: [D(0, 6, 8), D(1, 6, 8), UNDO, COINS, D(1, 7, 8), D(2, 6, 8)] },
  // K-30 Söküm with a Golden Trowel and Termos in play, then a K-43 exit after the torn-down move (movesSpent)
  { name: 'teardown then exit is a loss', spec: E26, preBoosters: ['thermos'], actions: [D(1, 6, 8), EXIT] },
  { name: 'teardown with start bonuses then trowel', spec: TILING, preBoosters: ['trowelStart', 'thermos'], streakTier: 2, actions: [D(0, 6, 8), D(1, 6, 8), { kind: 'goldTrowel', pieceId: 1, x: 7, y: 0 }, UNDO, D(2, 6, 8)] },
  // K-26 / E-04 FIFO queue across two segment shifts, E-21 Undo of a shift + delivery, redo
  { name: 'truck fifo undo redo', spec: FIFO, actions: [D(0, 6, 8), D(1, 6, 8), UNDO, D(1, 6, 8), D(14, 6, 8)] },
  // E-03 a yard move delivers the queued block; then the queue empties and the level goes on
  { name: 'truck queue delivered by a yard move', spec: E03, actions: [D(0, 6, 8), D(13, 5, 7), UNDO, D(13, 5, 7), D(14, 6, 8)] },
  // K-48 (3) the win waits for the truck queue
  { name: 'truck queue holds back the win', spec: WIN_WITH_TRUCK, actions: [D(0, 6, 8), D(13, 6, 8), UNDO, D(13, 6, 8)] },
  // K-29 three offers (ad, coins, coins), then the loss without a window
  { name: 'offers exhausted then loss', spec: { ...STREAK, moves: 1 }, actions: [...shuttle(1), AD, ...shuttle(5, 0), COINS, ...shuttle(5, 1), COINS, ...shuttle(5, 0), COINS, D(0, 6, 8)] },
  // K-29 / K-39 Undo closed in the window, Undo after an accepted offer, decline → lost
  { name: 'undo around the offer window then decline', spec: { ...STREAK, moves: 2 }, actions: [D(0, 6, 8), D(5, 5, 1), UNDO, COINS, D(1, 7, 8), UNDO, D(1, 7, 8), ...shuttle(4, 0), UNDO, DECLINE] },
  // K-39 Undo of the 4th correct placement (trowel taken back), depth 1, K-43 exit after Undo of the only move
  { name: 'undo streak trowel and depth', spec: STREAK, actions: [D(0, 6, 8), D(1, 7, 8), D(2, 6, 8), D(3, 7, 8), UNDO, UNDO, D(3, 7, 8), { kind: 'goldTrowel', pieceId: 5, x: 6, y: 4 }, D(4, 7, 8), UNDO, EXIT] },
];

const scenarioFiles = [];
SCENARIOS.forEach((sc, i) => {
  const data = level(sc.spec);
  if (!roundTrips(data)) throw new Error(`scenario ${sc.name} does not round-trip through JSON`);
  const lvl = compile(data);
  const seq = playSequence(`scenario ${pad(i + 1, 2)} ${sc.name}`, lvl, 0, [], false, sc);
  writeSource(`scenario_${pad(i + 1)}.json`, { kind: 'scenario', n: i + 1, label: sc.name }, data, [seq]);
  scenarioFiles.push(sc.name);
});

let webCommit = '?';
try {
  webCommit = execSync('git rev-parse --short HEAD', { cwd: WEB }).toString().trim();
} catch {
  /* not a git checkout */
}
const sorted = (m) => Object.fromEntries(Object.keys(m).sort().map((k) => [k, m[k]]));
const manifest = {
  generatedFrom: { webRepo: 'yilmazfatih-cyber/minik-usta', webCommit, generator: 'tools/diff/ts-oracle.mjs', seeds: SEEDS, moves: MOVES },
  sequences: totals.sequences,
  steps: totals.steps,
  actions: sorted(totals.actions),
  statuses: sorted(totals.statuses),
  outcomes: sorted(totals.outcomes),
  events: sorted(totals.events),
  files,
};
writeFileSync(join(OUT, 'manifest.json'), JSON.stringify(manifest, null, 1) + '\n');
console.log(`ts-oracle: web ${webCommit}, ${totals.sequences} sequences, ${totals.steps} actions (${geoLevels} random geometry levels, ${truckLevels} truck levels, ${scenarioFiles.length} scenarios) → ${OUT}`);
console.log('actions ', JSON.stringify(manifest.actions));
console.log('statuses', JSON.stringify(manifest.statuses));
console.log('outcomes', JSON.stringify(manifest.outcomes));
console.log('events  ', JSON.stringify(manifest.events));
