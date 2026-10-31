/**
 * Generates Assets/MinikUsta/Core.Tests/Golden/p3_vectors.json from the TypeScript core (the source of truth, D-091) for the
 * P3 modules: moves.ts (`applyMove`, the K-35 pipeline, every move kind, `eventLogHash`) and session.ts (`GameSession`:
 * start bonuses K-40, `apply` packages TECH §2R.15, Undo K-39, +5 offers K-29, exit K-43, replay K-43, `levelHash`).
 *
 * Each sequence is one attempt played through `GameSession.apply` with a seeded generator that mixes valid and refused
 * inputs: drags (reachable nodes, raw nodes that may be invalid, the start node = `sameSpot`), hammer / crane / paint
 * brush / Golden Trowel (valid targets and random ones that are rejected), the legacy cell trowel, Undo (allowed or
 * blocked), +5 offers (coins / ad, accepted or refused), decline and exit. Real levels 1–10 first play a seed-dependent
 * prefix of the canonical solution (tests/golden hand.json) so late phases (deliveries, segment shifts, wins) are reached;
 * tests/fixtures/cover.ts levels play from the start.
 *
 * The inputs are stored; the C# test replays them and rebuilds, per input, the same text line: result (status, reason,
 * won, outOfMoves, teardown cause), event count + `eventLogHash` of the package, Zobrist hash and FNV-1a of the whole
 * buffer, session fields (outcome, offers, undo block, next offer, counters), canonical JSON of the `TurnSummary`, YAO and
 * the invariant count. The lines of a sequence are hashed (FNV-1a 64); the first sequence keeps its full lines as the
 * format anchor. After the sequence: canonical JSON hash of the log, `GameSession.replay` buffer hash + replayed event
 * hash, final ASCII. `coverage` counts the event types of all packages (the C# test recounts them).
 *
 * Usage: node tools/gen-p3-vectors.ts [/path/to/minik-usta] [--verbose]   (node 22 type stripping; no extra packages)
 * `--verbose` also writes every line and the canonical event JSON of every package to p3_vectors.verbose.json (git-ignored).
 */
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { execSync } from 'node:child_process';

const HERE = dirname(fileURLToPath(import.meta.url));
const ROOT = resolve(HERE, '..');
const args = process.argv.slice(2);
const verbose = args.includes('--verbose');
const WEB = resolve(args.find((a) => !a.startsWith('--')) ?? join(ROOT, '..', 'minik-usta'));
const web = (p: string): Promise<any> => import(pathToFileURL(join(WEB, p)).href);

const { compile, loadLevel } = await web('src/core/level/compile.ts');
const { H, hdr, pieceZone, pieceShape } = await web('src/core/state.ts');
const { hashHex } = await web('src/core/hash.ts');
const { tryBeginDrag } = await web('src/core/movement.ts');
const { ArraySink, eventLogHash, fnv1a64, canonicalJson } = await web('src/core/moves.ts');
const { GameSession, levelHash } = await web('src/core/session.ts');
const { shapeByIndex } = await web('src/core/shapes.ts');
const { mulberry32 } = await web('src/core/rng.ts');
const { toAscii } = await web('src/core/ascii.ts');
const { stateInvariantErrors } = await web('src/core/grid.ts');
const boosters = await web('src/core/boosters.ts');
const { level } = await web('tests/fixtures/builders.ts');
const { coverLevel } = await web('tests/fixtures/cover.ts');

const pad = (n: number): string => String(n).padStart(3, '0');
const readJson = (p: string): any => JSON.parse(readFileSync(p, 'utf8'));
const bool = (b: boolean): string => (b ? '1' : '0');
const bufHash = (s: any): string => fnv1a64(Array.from(s.buf as Int32Array).join(','));

/** Pre-level boosters and streak tier of a seed (K-40). */
const PRE: readonly (readonly string[])[] = [[], ['trowelStart', 'thermos'], ['openShutter'], ['thermos', 'trowelStart', 'openShutter']];
/** Streak bonus the test passes (core cannot read economy.json): tier t → t moves, a trowel from tier 2. */
const streakBonus = (tier: number): { moves: number; trowels: number } => ({ moves: tier, trowels: tier >= 2 ? 1 : 0 });

// --- one random input ------------------------------------------------------------------------------------------------------

type Input = Record<string, unknown>;

function pick<T>(rng: any, xs: readonly T[]): T | undefined {
  return xs[rng.nextInt(Math.max(1, xs.length))];
}

function randomDrag(game: any, rng: any, burn: boolean): Input | null {
  const s = game.state;
  const hooks = game.hooks;
  const P = s.lvl.layout.counts.pieces;
  const start = rng.nextInt(P);
  for (let k = 0; k < P; k++) {
    const id = (start + k) % P;
    const z = pieceZone(s, id);
    if (z !== 0 && z !== 1) continue;
    const a = tryBeginDrag(s, id, hooks.drag);
    if (!a.ok) {
      if (rng.next() < 0.05) return { kind: 'drag', pieceId: id, to: { ix: 0, iy: 0, mode: 0 } }; // unpickable → invalid
      continue;
    }
    const roll = rng.next();
    if (roll < 0.05) return { kind: 'drag', pieceId: id, to: { ix: a.session.start.ix, iy: a.session.start.iy, mode: a.session.start.mode } };
    if (roll < 0.12)
      return {
        kind: 'drag',
        pieceId: id,
        to: { ix: rng.nextInt(s.lvl.geo.cols), iy: rng.nextInt(s.lvl.geo.rows), mode: 0 },
      };
    const nodes = a.session.reachableNodes();
    const site = nodes.filter((n: any) => n.ix >= s.lvl.geo.siteX);
    // favour site releases so the plan fills; a burn sequence favours yard shifts (1 move each, K-07 row 2)
    const yard = nodes.filter((n: any) => n.ix < s.lvl.geo.siteX && n.iy + a.session.shape.h <= s.lvl.geo.hy);
    const pool = burn ? (yard.length > 0 && rng.next() < 0.85 ? yard : nodes) : site.length > 0 && rng.next() < 0.6 ? site : nodes;
    const to = pool[rng.nextInt(pool.length)];
    if (to) return { kind: 'drag', pieceId: id, to: { ix: to.ix, iy: to.iy, mode: to.mode } };
  }
  return null;
}

function randomInput(game: any, rng: any, burn: boolean): Input | null {
  const s = game.state;
  const hooks = game.hooks;
  const P = s.lvl.layout.counts.pieces;
  if (game.outcome === 'outOfMoves') {
    const r = rng.next();
    if (r < (burn ? 0.08 : 0.15)) return { kind: 'decline' };
    if (r < 0.22) return { kind: 'undo' }; // K-39 lossWindow
    if (r < 0.27) return randomDrag(game, rng, burn) ?? { kind: 'undo' }; // the window rejects every move (notPlaying)
    return { kind: 'addMoves', amount: 5, source: r < 0.55 ? 'offerAd' : 'offerCoins' };
  }
  const roll = burn ? 0.4 + 0.6 * rng.next() : rng.next();
  if (roll < 0.03) return { kind: 'exit' };
  if (roll < 0.09) return { kind: 'undo' };
  if (roll < 0.17) {
    const t = rng.next() < 0.8 ? pick(rng, boosters.hammerTargets(s, hooks)) : undefined;
    return { kind: 'hammer', target: t ?? { pieceId: rng.nextInt(P) } };
  }
  if (roll < 0.25) {
    const ids = Array.from({ length: P }, (_, i) => i).filter((id) => boosters.craneSelectable(s, id, hooks));
    const id: number | undefined = rng.next() < 0.85 ? pick(rng, ids) : undefined;
    if (id === undefined) {
      const rid = rng.nextInt(P);
      return { kind: 'crane', pieceId: rid, to: { zone: 'site', x: rng.nextInt(s.lvl.geo.ws), y: rng.nextInt(s.lvl.geo.hs) }, rotation: 0 };
    }
    const spot = pick(rng, boosters.craneSiteSpots(s, id));
    if (spot && rng.next() < 0.6) return { kind: 'crane', pieceId: id, to: { zone: 'site', x: spot.at.ix, y: spot.at.iy }, rotation: spot.rotation };
    const shape = shapeByIndex(pieceShape(s, id));
    return {
      kind: 'crane',
      pieceId: id,
      to: { zone: 'yard', x: rng.nextInt(s.lvl.geo.wy - shape.w + 1), y: rng.nextInt(s.lvl.geo.hy - shape.h + 1) },
      rotation: shape.rotation,
    };
  }
  if (roll < 0.31) {
    const a = rng.nextInt(P);
    const b = rng.next() < 0.8 ? pick(rng, boosters.paintPartners(s, a)) : undefined;
    return { kind: 'paint', a, b: b ?? rng.nextInt(P) };
  }
  if (roll < 0.38) {
    const id: number | undefined = rng.next() < 0.85 ? pick(rng, boosters.trowelPieces(s, hooks)) : undefined;
    if (id === undefined) return { kind: 'goldTrowel', pieceId: rng.nextInt(P), x: rng.nextInt(s.lvl.geo.ws), y: rng.nextInt(s.lvl.geo.hs) };
    const at = pick(rng, boosters.trowelSpots(s, id));
    return at ? { kind: 'goldTrowel', pieceId: id, x: at.ix, y: at.iy } : { kind: 'goldTrowel', pieceId: id, x: 0, y: 0 };
  }
  if (roll < 0.4) return { kind: 'trowel', seg: 0, x: 0, y: 0 };
  return randomDrag(game, rng, burn);
}

// --- lines -----------------------------------------------------------------------------------------------------------------

const coverage: Record<string, number> = {};
const results: Record<string, number> = {};
const count = (map: Record<string, number>, key: string): void => {
  map[key] = (map[key] ?? 0) + 1;
};

function sessionLine(game: any): string {
  const s = game.state;
  const offer = game.nextOffer();
  const yao = game.yao();
  return (
    `${hashHex(s)}|${bufHash(s)}|${game.outcome}|${game.offersUsed}|${bool(game.adOfferUsed)}|${game.undoBlock() ?? '-'}` +
    `|${offer ? `${offer.n}${offer.adAllowed ? 'a' : ''}` : '-'}|${game.movesLeft}|${game.movesMade}|${game.movesSpent}` +
    `|yao:${yao.overWall},${yao.rail},${yao.yao ?? '-'}|inv:${stateInvariantErrors(s).length}`
  );
}

function applyLine(i: number, input: Input, r: any): string {
  return (
    `${i}|${r.status}|${r.reason ?? '-'}|${bool(r.won)}|${bool(r.outOfMoves)}|${r.teardownCause ?? '-'}` +
    `|ev:${r.events.length}:${eventLogHash(r.events)}|td:${r.teardown ? r.teardown.seq : '-'}|sum:${canonicalJson(r.summary)}`
  );
}

interface Sequence {
  readonly name: string;
  readonly source: { kind: 'level' | 'cover'; n: number };
  readonly seed: number;
  readonly preBoosters: readonly string[];
  readonly streakTier: number;
  readonly levelHash: string;
  readonly prefix: number;
  /** Burn sequence: drags only, mostly yard shifts, up to 90 inputs (out-of-moves windows, offers, K-29 loss). */
  readonly burn: boolean;
  readonly startEvents: string;
  readonly inputs: Input[];
  readonly count: number;
  readonly hash: string;
  readonly logHash: string;
  readonly logLength: number;
  readonly replay: { readonly buf: string; readonly events: string; readonly outcome: string };
  readonly finalAscii: string;
  lines?: string[];
  events?: string[];
}

function playSequence(name: string, source: Sequence['source'], lvl: any, seed: number, prefixMoves: readonly any[], keep: boolean, burn = false): Sequence {
  const rng = mulberry32(seed * 7919 + source.n * 131 + (source.kind === 'cover' ? 50_000 : 0));
  const preBoosters = PRE[seed % PRE.length] ?? [];
  const streakTier = seed % 3;
  const startSink = new ArraySink();
  const game = GameSession.start(lvl, { preBoosters, streakTier }, { streakBonus }, startSink);
  const lines: string[] = [`start|ev:${startSink.events.length}:${eventLogHash(startSink.events)}|${sessionLine(game)}`];
  const evText: string[] = [canonicalJson(startSink.events)];
  for (const e of startSink.events) count(coverage, e.t);
  const inputs: Input[] = [];
  const prefix = prefixMoves.length === 0 ? 0 : seed % (prefixMoves.length + 1);
  for (let i = 0; i < prefix; i++) inputs.push(prefixMoves[i]);
  let done = false;
  for (let step = 0; !done && step < prefix + (burn ? 90 : 40); step++) {
    if (game.outcome === 'won' || game.outcome === 'lost' || game.outcome === 'exited') break;
    const input = step < prefix ? inputs[step] : randomInput(game, rng, burn);
    if (input === null || input === undefined) break;
    if (step >= prefix) inputs.push(input);
    const i = step; // = inputs.length - 1 after the push (the prefix is stored up front)
    if (input.kind === 'exit') {
      const x = game.exit();
      lines.push(`${i}|exit|${x.kind}|${x.movesMade}|${x.movesSpent}|${x.refundPreBoosters.join(',')}|${bool(x.streakBonusConsumed)}|${sessionLine(game)}`);
      evText.push('[]');
      count(results, 'exit:' + x.kind);
      done = true;
      continue;
    }
    if (input.kind === 'decline') {
      game.declineOffer();
      lines.push(`${i}|decline|${sessionLine(game)}`);
      evText.push('[]');
      count(results, 'decline');
      done = true;
      continue;
    }
    const r = game.apply(input);
    for (const e of r.events) count(coverage, e.t);
    count(results, `${input.kind}:${r.status}:${r.reason ?? '-'}`);
    lines.push(`${applyLine(i, input, r)}|${sessionLine(game)}`);
    evText.push(canonicalJson(r.events));
  }
  count(results, 'outcome:' + game.outcome + (game.offersUsed > 0 ? ':offers' + game.offersUsed : ''));
  const log = game.log;
  const replaySink = new ArraySink();
  const replayed = GameSession.replay(lvl, log, { streakBonus }, replaySink);
  const seq: Sequence = {
    name,
    source,
    seed,
    preBoosters,
    streakTier,
    levelHash: levelHash(lvl.data),
    prefix,
    burn,
    startEvents: eventLogHash(startSink.events),
    inputs,
    count: lines.length,
    hash: fnv1a64(lines.join('\n')),
    logHash: fnv1a64(canonicalJson(log)),
    logLength: log.length,
    replay: { buf: bufHash(replayed.state), events: eventLogHash(replaySink.events), outcome: replayed.outcome },
    finalAscii: toAscii(game.state),
  };
  if (keep) seq.lines = lines;
  if (verbose) seq.events = evText;
  return seq;
}

// --- sequences -------------------------------------------------------------------------------------------------------------

const LEVEL_SEEDS = 8;
const BURN_SEEDS = 3;
const COVER_LEVELS = 20;
const COVER_SEEDS = 3;
const sequences: Sequence[] = [];
for (let n = 1; n <= 10; n++) {
  const loaded = loadLevel(readJson(join(WEB, 'levels', `level_${pad(n)}.json`)));
  if (!loaded.ok) throw new Error(`level ${n} does not load`);
  const hand = readJson(join(WEB, 'tests', 'golden', `level_${pad(n)}.hand.json`));
  const moves = hand.log.filter((a: any) => a.kind === 'drag');
  for (let seed = 1; seed <= LEVEL_SEEDS; seed++)
    sequences.push(playSequence(`level ${n} seed ${seed}`, { kind: 'level', n }, loaded.level, seed, moves, sequences.length === 0 || verbose));
  for (let seed = LEVEL_SEEDS + 1; seed <= LEVEL_SEEDS + BURN_SEEDS; seed++)
    sequences.push(playSequence(`level ${n} burn ${seed}`, { kind: 'level', n }, loaded.level, seed, [], verbose, true));
}
for (let n = 1; n <= COVER_LEVELS; n++) {
  const lvl = compile(level(coverLevel(n)));
  for (let seed = 1; seed <= COVER_SEEDS; seed++)
    sequences.push(playSequence(`cover ${n} seed ${seed}`, { kind: 'cover', n }, lvl, seed, [], verbose));
}

let webCommit = '?';
try {
  webCommit = execSync('git rev-parse --short HEAD', { cwd: WEB }).toString().trim();
} catch {
  /* not a git checkout */
}
const sorted = (m: Record<string, number>): Record<string, number> =>
  Object.fromEntries(Object.keys(m).sort().map((k) => [k, m[k] as number]));
const out = {
  generatedFrom: { webRepo: 'yilmazfatih-cyber/minik-usta', webCommit, generator: 'tools/gen-p3-vectors.ts' },
  coverage: sorted(coverage),
  results: sorted(results),
  sequences,
};
const target = join(ROOT, 'Assets', 'MinikUsta', 'Core.Tests', 'Golden', verbose ? 'p3_vectors.verbose.json' : 'p3_vectors.json');
// one sequence per line keeps the file diffable and small
const body = JSON.stringify({ ...out, sequences: [] }, null, 1).replace(
  /"sequences": \[\]/,
  `"sequences": [\n${sequences.map((s) => JSON.stringify(s)).join(',\n')}\n]`,
);
writeFileSync(target, body + '\n');
const inputs = sequences.reduce((k, s) => k + s.inputs.length, 0);
console.log(`wrote ${target} (web ${webCommit}, ${sequences.length} sequences, ${inputs} inputs)`);
console.log('coverage', JSON.stringify(sorted(coverage)));
console.log('results', JSON.stringify(sorted(results)));
