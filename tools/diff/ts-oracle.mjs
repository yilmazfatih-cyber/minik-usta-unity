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
 * (segment shifts, wins) are reached; the Faz 2R archive levels (`archive`, schemaVersion 1) also reach truck deliveries; a third of the seeds is "burn"-biased (yard shifts) so the
 * out-of-moves window, offers, K-29 losses and Söküm (K-30) show up within the move cap.
 * Sources: the shipped levels 1–10 (D-099, schemaVersion 2, fixed yard), the Faz 2R archive levels 1–10 (Core.Tests/Archive2R,
 * schemaVersion 1), tests/fixtures/cover.ts full-cover boards, random levels in the 9 K-49 geometries (static gap,
 * yard gravity on/off), crowded "truck" boards whose batches queue (K-25, K-26, E-03), and scripted scenarios (P4 item
 * 3: Söküm / K-30 with Undo, offers and exits; D1 — unchain, Söküm `access` (RULES_VERSION 3), after an offer; +5 offers;
 * Undo; truck queue; K-55 fixed yard: tests/level/fixtures/v2/p1_two_floors.json), some followed by random valid play. Every source file carries the level data
 * (`level`) so C# compiles the very same level (`levelHash` is compared).
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
/** Faz 2R archive (D-099 / K-55 item 9): levels 1–10 as `schemaVersion` 1 with their truck batches and golden replays. */
const ARCHIVE = resolve(opt('--archive', join(ROOT, 'Assets', 'MinikUsta', 'Core.Tests', 'Archive2R')));
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
  const cap = script && !script.randomAfter ? script.actions.length : MOVES;
  for (let k = 0; k < cap; k++) {
    if (game.outcome === 'won' || game.outcome === 'lost' || game.outcome === 'exited') break;
    const a = script && k < script.actions.length ? script.actions[k] : k < prefix ? prefixMoves[k] : randomAction(game, rng, burn, k);
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

// `level` = the shipped levels (D-099, schemaVersion 2: one batch, fixed yard); `archive` = the Faz 2R levels (schemaVersion 1)
for (const kind of ['level', 'archive']) {
  for (let n = 1; n <= 10; n++) {
    const json = readJson(kind === 'level' ? join(WEB, 'levels', `level_${pad(n)}.json`) : join(ARCHIVE, `level_${pad(n)}.json`));
    const loaded = loadLevel(json);
    if (!loaded.ok) throw new Error(`${kind} ${n} does not load`);
    const lvl = loaded.level;
    const hand = readJson(kind === 'level' ? join(WEB, 'tests', 'golden', `level_${pad(n)}.hand.json`) : join(ARCHIVE, `level_${pad(n)}.hand.json`));
    const canonical = hand.log.filter((a) => a.kind === 'drag');
    const seqs = [];
    for (let seed = 1; seed <= SEEDS; seed++) {
      const burn = seed % 3 === 0;
      seqs.push(playSequence(`${kind} ${pad(n)} seed ${pad(seed, 2)}${burn ? ' burn' : ''}`, lvl, seed, burn ? [] : canonical, burn));
    }
    writeSource(kind === 'level' ? `level_${pad(n)}.json` : `archive_${pad(n)}.json`, { kind, n }, lvl.data, seqs);
  }
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

// --- v3 block traits (RULES_VERSION 4, GDD §16): random fixed-yard levels with two-tone (Y9), pallet (Y10), wrap + scissors (Y11),
// bonus (Y12) and slippery (Y13) blocks, one family per variant, random valid play (pallet layers, holds, crane / trowel / brush /
// hammer on the traits, the K-30 D1 widening). schemaVersion 3, so C# parses the stored level data with the same v3 fields.

const V3_VARIANTS = ['twotone', 'pallet', 'wrap', 'bonus', 'slippery', 'mix'];
const V3_GEOS = [
  { wy: 4, hy: 4, ws: 2, hs: 4 },
  { wy: 5, hy: 5, ws: 2, hs: 5 },
  { wy: 6, hy: 5, ws: 2, hs: 6 },
  { wy: 5, hy: 6, ws: 3, hs: 6 },
];

function v3LevelData(g, rng, id, variant) {
  const used = new Set();
  const pieces = [];
  const traits = {};
  const place = (shapeId, color, flags = []) => {
    const shape = shapeById(shapeId);
    for (let t = 0; t < 60; t++) {
      const x = rng.nextInt(g.wy - shape.w + 1);
      const y = rng.nextInt(g.hy - shape.h + 1);
      const cells = shape.cells.map((c) => `${x + c.x},${y + c.y}`);
      if (cells.some((c) => used.has(c))) continue;
      cells.forEach((c) => used.add(c));
      pieces.push([shapeId, color, x, y, flags]);
      return pieces.length - 1;
    }
    return -1;
  };
  const colors = ['W', 'Y'];
  const pick = (a) => a[rng.nextInt(a.length)];
  const all = variant === 'mix';
  if (variant === 'pallet' || all) {
    const shapeId = pick(['B1_0', 'D2_0', 'D2_90']);
    const layers = 2 + rng.nextInt(2);
    const color = pick(colors);
    const first = place(shapeId, color);
    if (first >= 0) {
      traits[first] = { stack: { id: 0, z: 0 } };
      const [sid, col, x, y] = pieces[first];
      for (let z = 1; z < layers; z++) {
        pieces.push([sid, col, x, y, []]);
        traits[pieces.length - 1] = { stack: { id: 0, z } };
      }
    }
  }
  if (variant === 'twotone' || all) {
    for (let k = 0; k < 1 + rng.nextInt(3); k++) {
      const shapeId = pick(['D2_0', 'D2_90', 'C3_0', 'C3_180']);
      const n = shapeById(shapeId).cells.length;
      const first = pick(colors);
      const other = first === 'W' ? 'Y' : 'W';
      let cs = first;
      for (let i = 1; i < n; i++) cs += rng.next() < 0.5 ? first : other;
      if (new Set(cs).size < 2) cs = first + other + cs.slice(2);
      const idx = place(shapeId, first);
      if (idx >= 0) traits[idx] = { ...(traits[idx] ?? {}), colors: cs };
    }
  }
  if (variant === 'wrap' || all) {
    const s = place(pick(['B1_0', 'D2_0']), pick(colors), ['scissors']);
    for (let k = 0; k < 1 + rng.nextInt(3); k++) place(pick(['B1_0', 'D2_90', 'O4_0']), pick(colors), s >= 0 ? ['wrapped'] : []);
  }
  if (variant === 'bonus' || all) {
    for (let k = 0; k < 1 + rng.nextInt(2); k++) {
      const idx = place(pick(['B1_0', 'D2_0']), pick(colors), ['bonus']);
      if (idx >= 0 && rng.next() < 0.5) traits[idx] = { ...(traits[idx] ?? {}), bonusMoves: 1 + rng.nextInt(3) };
    }
  }
  if (variant === 'slippery' || all) {
    for (let k = 0; k < 1 + rng.nextInt(3); k++) place(pick(['B1_0', 'D2_0', 'D2_90', 'C3_0']), pick(colors), ['slippery']);
  }
  const fill = 0.35 + rng.next() * 0.2;
  for (let t = 0; t < 200 && used.size < fill * g.wy * g.hy; t++) place(pick(['B1_0', 'D2_0', 'D2_90', 'O4_0']), pick(colors));
  // K-02: keep at least two free yard cells
  while (g.wy * g.hy - used.size < 2 && pieces.length > 0) {
    const p = pieces.pop();
    const shape = shapeById(p[0]);
    shape.cells.forEach((c) => used.delete(`${p[2] + c.x},${p[3] + c.y}`));
  }
  const hasScissors = pieces.some((p) => p[4].includes('scissors'));
  const spec = {
    id,
    schemaVersion: 3,
    moves: 40,
    yard: { cols: g.wy, rows: g.hy },
    site: { cols: g.ws, rows: g.hs },
    plan: planRows(g, rng),
    pieces: hasScissors ? pieces : pieces.map(([a, b, c, d, f]) => [a, b, c, d, f.filter((x) => x !== 'wrapped')]),
    traits,
    wall: { height: 2 + rng.nextInt(2) },
    gravity: { yard: false },
  };
  return level(spec);
}

const v3Rng = mulberry32(0x7633_7633);
let v3Levels = 0;
for (let vi = 0; vi < V3_VARIANTS.length; vi++) {
  for (let gi = 0; gi < V3_GEOS.length; gi++) {
    let data = null;
    for (let t = 0; t < 30 && data === null; t++) {
      let d = null;
      try {
        d = v3LevelData(V3_GEOS[gi], v3Rng, 5, V3_VARIANTS[vi]);
      } catch {
        d = null;
      }
      if (d && roundTrips(d)) {
        const res = loadLevel(JSON.parse(JSON.stringify(d)));
        if (res.ok || !res.issues?.some((i) => i.code === 'schema_version_low' || i.code.startsWith('stack') || i.code.startsWith('scissors') || i.code.startsWith('colors'))) data = d;
      }
    }
    if (data === null) continue;
    const lvl = compile(data);
    const geo = V3_GEOS[gi];
    const n = vi * V3_GEOS.length + gi + 1;
    const seqs = [];
    for (let seed = 1; seed <= 3; seed++) seqs.push(playSequence(`v3 ${V3_VARIANTS[vi]} ${geo.wy}x${geo.hy}_${geo.ws}x${geo.hs} seed ${pad(seed, 2)}`, lvl, seed + gi, [], seed === 3));
    writeSource(`v3_${pad(n)}.json`, { kind: 'v3', n, label: V3_VARIANTS[vi] }, data, seqs);
    v3Levels++;
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
/** tests/core/deadlock.test.ts JAM: yard 5 × 4, four blocks under an I5 Ağır Yük, crates on top of it; plan 2 × 4 W. */
const JAM = {
  yard: { cols: 5, rows: 4 },
  site: { cols: 2, rows: 4 },
  wall: { height: 2 },
  plan: ['WW', 'WW', 'WW', 'WW'],
  pieces: [['D2_90', 'W', 0, 0], ['B1_0', 'W', 2, 0], ['B1_0', 'W', 3, 0], ['B1_0', 'W', 4, 0], ['I5_0', 'W', 0, 1], ['D2_90', 'W', 0, 3], ['B1_0', 'W', 2, 3]],
  obstacles: [0, 1, 2, 3, 4].map((x) => ({ type: 'crate', x, y: 2, hp: 1 })),
};
/** tests/core/deadlock.test.ts "D1 removes chains first": two chained B1 W (Y3) and a D2_0 W. */
const CHAINED = {
  wall: { height: 2 },
  site: { cols: 2, rows: 2 },
  plan: ['WW', 'WW'],
  pieces: [['B1_0', 'W', 0, 0, ['chained']], ['B1_0', 'W', 1, 0, ['chained']], ['D2_0', 'W', 4, 0]],
};
/**
 * K-30 D1 boards found by a seeded search in the Faz 2R time (random crate-jammed yards, random valid drags; the TS
 * reshuffle then changed H.rng). RULES_VERSION 3 has no reshuffle: the log still leads to a D1 move, and the D1 Söküm
 * (`access`) or the lifted chain follows; random valid play after it.
 */
const D1_RNG = [
  { spec: {"id":12,"yard":{"cols":5,"rows":5},"site":{"cols":2,"rows":4},"wall":{"height":2},"plan":["WW","WY","YW","YY"],"pieces":[["O4_0","W",3,0],["I3_90","Y",0,1],["B1_0","Y",1,0],["B1_0","W",0,0],["B1_0","W",2,0],["B1_0","W",4,4],["B1_0","Y",2,3],["B1_0","W",3,3],["B1_0","Y",3,4],["B1_0","W",2,4],["B1_0","W",1,3],["B1_0","Y",0,3],["B1_0","Y",4,3]],"obstacles":[{"type":"crate","x":0,"y":2,"hp":1},{"type":"crate","x":1,"y":2,"hp":1},{"type":"crate","x":2,"y":2,"hp":1},{"type":"crate","x":3,"y":2,"hp":1},{"type":"crate","x":4,"y":2,"hp":1}],"moves":30}, log: [{"kind":"drag","pieceId":12,"to":{"ix":5,"iy":1,"mode":0}},{"kind":"drag","pieceId":11,"to":{"ix":6,"iy":6,"mode":0}},{"kind":"drag","pieceId":9,"to":{"ix":5,"iy":5,"mode":0}},{"kind":"drag","pieceId":7,"to":{"ix":5,"iy":2,"mode":0}},{"kind":"drag","pieceId":5,"to":{"ix":6,"iy":3,"mode":0}},{"kind":"drag","pieceId":7,"to":{"ix":5,"iy":6,"mode":0}},{"kind":"drag","pieceId":8,"to":{"ix":5,"iy":5,"mode":0}},{"kind":"drag","pieceId":9,"to":{"ix":6,"iy":2,"mode":0}},{"kind":"drag","pieceId":7,"to":{"ix":6,"iy":5,"mode":0}},{"kind":"drag","pieceId":7,"to":{"ix":5,"iy":5,"mode":0}},{"kind":"drag","pieceId":6,"to":{"ix":4,"iy":3,"mode":0}},{"kind":"drag","pieceId":6,"to":{"ix":5,"iy":6,"mode":0}},{"kind":"drag","pieceId":10,"to":{"ix":6,"iy":4,"mode":0}},{"kind":"drag","pieceId":9,"to":{"ix":6,"iy":4,"mode":0}},{"kind":"drag","pieceId":10,"to":{"ix":5,"iy":4,"mode":0}},{"kind":"drag","pieceId":6,"to":{"ix":6,"iy":5,"mode":0}},{"kind":"drag","pieceId":9,"to":{"ix":6,"iy":6,"mode":0}}] },
  { spec: {"id":12,"yard":{"cols":5,"rows":3},"site":{"cols":2,"rows":4},"wall":{"height":2},"plan":["YW","YY","WW","WW"],"pieces":[["C3_0","W",2,0],["D2_90","Y",3,1],["C3_0","Y",0,0],["B1_0","Y",4,0],["D2_0","W",1,1]],"obstacles":[{"type":"crate","x":0,"y":2,"hp":1},{"type":"crate","x":2,"y":2,"hp":1},{"type":"crate","x":3,"y":2,"hp":1},{"type":"crate","x":4,"y":2,"hp":1}],"moves":30}, log: [{"kind":"drag","pieceId":4,"to":{"ix":6,"iy":3,"mode":0}},{"kind":"drag","pieceId":3,"to":{"ix":5,"iy":0,"mode":0}},{"kind":"drag","pieceId":3,"to":{"ix":5,"iy":5,"mode":0}},{"kind":"drag","pieceId":3,"to":{"ix":5,"iy":2,"mode":0}},{"kind":"drag","pieceId":3,"to":{"ix":6,"iy":3,"mode":0}}] },
  { spec: {"id":12,"yard":{"cols":4,"rows":3},"site":{"cols":2,"rows":2},"wall":{"height":2},"plan":["YW","YY"],"pieces":[["D2_0","W",2,0],["D2_0","Y",1,1],["D2_0","W",3,0],["C3_0","W",0,0]],"obstacles":[{"type":"crate","x":0,"y":2,"hp":1},{"type":"crate","x":2,"y":2,"hp":1},{"type":"crate","x":3,"y":2,"hp":1}],"moves":30}, log: [{"kind":"drag","pieceId":1,"to":{"ix":4,"iy":3,"mode":0}}] },
  { spec: {"id":12,"yard":{"cols":4,"rows":5},"site":{"cols":2,"rows":4},"wall":{"height":2},"plan":["YY","WY","YW","WW"],"pieces":[["O4_0","W",0,0],["D2_90","Y",2,1],["B1_0","Y",3,0],["B1_0","W",2,0],["B1_0","Y",2,4],["B1_0","W",3,4],["B1_0","W",0,3],["B1_0","Y",1,4],["B1_0","W",1,3],["B1_0","Y",2,3],["B1_0","W",3,3],["B1_0","Y",0,4]],"obstacles":[{"type":"crate","x":0,"y":2,"hp":1},{"type":"crate","x":1,"y":2,"hp":1},{"type":"crate","x":2,"y":2,"hp":1},{"type":"crate","x":3,"y":2,"hp":1}],"moves":30}, log: [{"kind":"drag","pieceId":11,"to":{"ix":4,"iy":1,"mode":0}},{"kind":"drag","pieceId":4,"to":{"ix":4,"iy":1,"mode":0}},{"kind":"drag","pieceId":10,"to":{"ix":5,"iy":0,"mode":0}},{"kind":"drag","pieceId":9,"to":{"ix":5,"iy":1,"mode":0}},{"kind":"drag","pieceId":5,"to":{"ix":5,"iy":1,"mode":0}},{"kind":"drag","pieceId":7,"to":{"ix":3,"iy":3,"mode":0}},{"kind":"drag","pieceId":4,"to":{"ix":4,"iy":0,"mode":0}},{"kind":"drag","pieceId":8,"to":{"ix":4,"iy":0,"mode":0}},{"kind":"drag","pieceId":4,"to":{"ix":5,"iy":6,"mode":0}},{"kind":"drag","pieceId":11,"to":{"ix":5,"iy":5,"mode":0}},{"kind":"drag","pieceId":9,"to":{"ix":5,"iy":6,"mode":0}},{"kind":"drag","pieceId":9,"to":{"ix":4,"iy":1,"mode":0}},{"kind":"drag","pieceId":6,"to":{"ix":4,"iy":2,"mode":0}},{"kind":"drag","pieceId":7,"to":{"ix":5,"iy":6,"mode":0}},{"kind":"drag","pieceId":7,"to":{"ix":4,"iy":5,"mode":0}}] },
  { spec: {"id":12,"yard":{"cols":6,"rows":4},"site":{"cols":2,"rows":3},"wall":{"height":2},"plan":["WW","YY","WY"],"pieces":[["C3_0","Y",2,1],["I3_90","W",2,0],["B1_0","Y",5,2],["C3_0","Y",0,1],["B1_0","Y",5,0],["C3_0","W",4,1],["B1_0","Y",1,2],["D2_90","W",0,0],["B1_0","W",3,2]],"obstacles":[{"type":"crate","x":0,"y":3,"hp":1},{"type":"crate","x":1,"y":3,"hp":1},{"type":"crate","x":2,"y":3,"hp":1},{"type":"crate","x":3,"y":3,"hp":1},{"type":"crate","x":4,"y":3,"hp":1},{"type":"crate","x":5,"y":3,"hp":1}],"moves":30}, log: [{"kind":"drag","pieceId":2,"to":{"ix":7,"iy":1,"mode":0}}] },
  { spec: {"id":12,"yard":{"cols":5,"rows":4},"site":{"cols":2,"rows":2},"wall":{"height":2},"plan":["YY","YY"],"pieces":[["C3_0","Y",3,1],["C3_0","Y",2,0],["C3_0","Y",1,2],["D2_90","W",0,0],["B1_0","W",4,0],["B1_0","Y",4,2],["D2_90","Y",0,1],["B1_0","W",0,2]],"obstacles":[{"type":"crate","x":0,"y":3,"hp":1},{"type":"crate","x":2,"y":3,"hp":1},{"type":"crate","x":3,"y":3,"hp":1},{"type":"crate","x":4,"y":3,"hp":1}],"moves":30}, log: [{"kind":"drag","pieceId":5,"to":{"ix":6,"iy":1,"mode":0}}] },
];
/** K-55 GDD example P1 (tests/level/fixtures/v2/p1_two_floors.json, schemaVersion 2): yard 6×4, two 2×5 floors, 8 blocks. */
const P1_JSON = readJson(join(WEB, 'tests', 'level', 'fixtures', 'v2', 'p1_two_floors.json'));
/** The P1 solution of tests/core/fixedYard.test.ts: 9 moves, the segment change on move 6. */
const P1_SOLUTION = [D(5, 6, 5), D(6, 7, 5), D(3, 4, 3), D(1, 6, 5), D(2, 7, 5), D(3, 6, 5), D(4, 6, 5), D(7, 6, 5), D(0, 6, 5)];
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
  // K-30 / K-55 item 8 D1: a jammed yard is torn down at step 12 (Söküm `access`, never re-laid), then random valid play (hammer on crates …)
  ...[1, 2, 3, 4].map((k) => ({ name: `D1 jam teardown then random ${k}`, spec: JAM, actions: [D(5, 5, 4), D(6, 5, 4)], randomAfter: true, seed: k })),
  // K-30 D1 not at zero moves; K-29 after +5 only D1 runs once (E-42): its Söküm takes the last drag back, then random valid play
  ...[1, 2].map((k) => ({ name: `D1 after the offer then random ${k}`, spec: { ...JAM, moves: 2 }, actions: [D(5, 5, 4), D(6, 5, 4), COINS], randomAfter: true, seed: k })),
  // K-30 D1 removes chains first (Y3 truck help), then random valid play
  ...[1, 2].map((k) => ({ name: `D1 unchain then random ${k}`, spec: CHAINED, actions: [D(2, 6, 8)], randomAfter: true, seed: k })),
  // K-30 D1 on seeded crate-jammed boards (the Faz 2R reshuffle search), then random valid play
  ...D1_RNG.map((x, k) => ({ name: `D1 seeded jam ${k + 1}`, spec: x.spec, actions: x.log, randomAfter: true, seed: k + 1 })),
  // K-55 fixed yard (E-62): the P1 solution — the segment change on move 6 leaves the yard bit for bit, no delivery — and a win
  { name: 'K-55 P1 two floors win', json: P1_JSON, actions: P1_SOLUTION },
  // K-55 + K-39: Undo of the move that completes floor 1, then the same move again, then random valid play
  { name: 'K-55 P1 undo across the segment change', json: P1_JSON, actions: [...P1_SOLUTION.slice(0, 6), UNDO, P1_SOLUTION[5], P1_SOLUTION[6]], randomAfter: true, seed: 1 },
  // K-55 item 4 / E-63: random valid play on the two-floor yard (blocks are not bound to a floor: swaps, D3a Söküm)
  ...[1, 2, 3, 4, 5, 6].map((k) => ({ name: `K-55 P1 random play ${k}`, json: P1_JSON, actions: [], randomAfter: true, seed: k + 10 })),
  // K-55 + K-29: few moves, the window opens and +5 offers follow, then random valid play
  ...[1, 2].map((k) => ({ name: `K-55 P1 offers ${k}`, json: { ...P1_JSON, moves: 3 }, actions: [P1_SOLUTION[0], P1_SOLUTION[1], P1_SOLUTION[2], COINS], randomAfter: true, seed: k + 20 })),
];

const scenarioFiles = [];
SCENARIOS.forEach((sc, i) => {
  const data = sc.json ? LevelSchema.parse(JSON.parse(JSON.stringify(sc.json))) : level(sc.spec);
  if (!roundTrips(data)) throw new Error(`scenario ${sc.name} does not round-trip through JSON`);
  const lvl = compile(data);
  const seq = playSequence(`scenario ${pad(i + 1, 2)} ${sc.name}`, lvl, sc.seed ?? 0, [], false, sc);
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
console.log(`ts-oracle: web ${webCommit}, ${totals.sequences} sequences, ${totals.steps} actions (${geoLevels} random geometry levels, ${truckLevels} truck levels, ${v3Levels} v3 trait levels, ${scenarioFiles.length} scenarios) → ${OUT}`);
console.log('actions ', JSON.stringify(manifest.actions));
console.log('statuses', JSON.stringify(manifest.statuses));
console.log('outcomes', JSON.stringify(manifest.outcomes));
console.log('events  ', JSON.stringify(manifest.events));
