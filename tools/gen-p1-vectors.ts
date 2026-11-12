/**
 * Generates Assets/MinikUsta/Core.Tests/Golden/p1_vectors.json from the TypeScript core (the source of truth, D-091):
 * the P1 modules (movement, gravity, placement, delivery) evaluated on the 10 shipped levels (D-099, schemaVersion 2), on
 * 50 seeded random boards of the 10 geometries of TECH §2R.1 and on the 10 Faz 2R archive levels (schemaVersion 1, truck
 * batches; Assets/MinikUsta/Core.Tests/Archive2R, `archive_level_NNN`). Per board: settleYard motions (yard gravity boards), state hash after the
 * settle, build front, site column masks. Per piece: tryBeginDrag outcome, R with BFS distances, K-07 class of every
 * node, computeFall + shadowInfo of every site node, a K-08 sticky-follow sweep over the board (0.5-cell grid), the
 * yard drop rows (K-17 step 2 / K-25) and returnTarget. Everything is serialised to one text line per item and hashed
 * with FNV-1a 64 per piece; the first board (level 1) also keeps the full lines as the format anchor. The C# test
 * (P1VectorTests.cs) rebuilds the same lines and must match hash for hash.
 *
 * Usage: node tools/gen-p1-vectors.ts [/path/to/minik-usta] [--verbose]   (node 22 type stripping; no extra packages)
 * `--verbose` also writes every line of every piece to p1_vectors.verbose.json next to the vectors (git-ignored by
 * size: regenerate it when a hash mismatch needs diagnosing).
 *
 * The random boards reuse the helpers of tests/core/geometry.test.ts (G, planRows, yardPieces, randomLevel) copied
 * verbatim: the RNG call order is part of the contract with GeometryTests.RandomLevel.
 *
 * CL-V3C-11 (C1): after the archive levels, `wide` boards on a 6 × 8 | 3 × 5 frame (level id 8, schemaVersion 3) carry their
 * level data (`level`), so C# compiles the very same board: the 3-wide shapes P5 / S4 / Z4 of a 3-column site (D-103 K-56
 * annex; yard gravity and a static gap on two of them) and pallets of the D-106 shapes (C3, L4, T4, J4, O4: the held layer
 * rises through its own footprint, RULES_VERSION 6).
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
/** Faz 2R archive (D-099 / K-55 item 9): levels 1–10 as `schemaVersion` 1 with their truck batches. */
const ARCHIVE = resolve(args.find((a) => a.startsWith('--archive='))?.slice('--archive='.length) ?? join(ROOT, 'Assets', 'MinikUsta', 'Core.Tests', 'Archive2R'));
const archiveLevel = (n: number): any => JSON.parse(readFileSync(join(ARCHIVE, `level_${String(n).padStart(3, '0')}.json`), 'utf8'));

const { LevelSchema } = await web('src/core/level/schema.ts');
const { compile } = await web('src/core/level/compile.ts');
const { createInitialState, H, hdr, pieceZone } = await web('src/core/state.ts');
const { hashHex } = await web('src/core/hash.ts');
const { tryBeginDrag } = await web('src/core/movement.ts');
const { computeFall, shadowInfo, settleYard, siteColumnMasks } = await web('src/core/gravity.ts');
const { buildFront, dropIntoYard, returnTarget, yardDropRow } = await web('src/core/placement.ts');
const { visibleSegment } = await web('src/core/grid.ts');
const { fnv1a64 } = await web('src/core/moves.ts');
const { shapeById } = await web('src/core/shapes.ts');
const { mulberry32 } = await web('src/core/rng.ts');
const { Zone } = await web('src/core/types.ts');
const { level } = await web('tests/fixtures/builders.ts');

// --- geometry set G and the random level of tests/core/geometry.test.ts (verbatim copies) -----------------------------

interface GeoCase {
  readonly wy: number;
  readonly hy: number;
  readonly ws: number;
  readonly hs: number;
}

const G: readonly GeoCase[] = [
  { wy: 4, hy: 4, ws: 2, hs: 5 },
  { wy: 4, hy: 4, ws: 2, hs: 6 },
  { wy: 4, hy: 5, ws: 2, hs: 5 },
  { wy: 4, hy: 5, ws: 2, hs: 7 },
  { wy: 6, hy: 5, ws: 2, hs: 6 },
  { wy: 6, hy: 5, ws: 2, hs: 7 },
  { wy: 6, hy: 8, ws: 2, hs: 8 },
  { wy: 5, hy: 7, ws: 3, hs: 7 },
  { wy: 4, hy: 6, ws: 4, hs: 6 },
  { wy: 6, hy: 8, ws: 3, hs: 5 }, // D-103 wide site (levels 6-10 v3c): 9 columns
];

const label = (g: GeoCase): string => `${g.wy}x${g.hy}|${g.ws}x${g.hs}`;
const sizes = (g: GeoCase) => ({ yard: { cols: g.wy, rows: g.hy }, site: { cols: g.ws, rows: g.hs } });

function cellsOf(shape: any, n: { ix: number; iy: number }): [number, number][] {
  return shape.cells.map((c: any) => [n.ix + c.x, n.iy + c.y]);
}

function planRows(g: GeoCase, rng: any): string[] {
  const colors = ['W', 'Y'];
  return Array.from({ length: g.hs }, () =>
    Array.from({ length: g.ws }, () => colors[rng.nextInt(2)] ?? 'W').join(''),
  );
}

function yardPieces(g: GeoCase, rng: any, fill: number): any[] {
  const used = new Set<string>();
  const out: any[] = [];
  const shapes = ['B1_0', 'D2_0', 'D2_90', 'O4_0', 'C3_0', 'C3_180'];
  for (let tries = 0; tries < 400 && used.size < fill * g.wy * g.hy; tries++) {
    const shape = shapeById(shapes[rng.nextInt(shapes.length)] ?? 'B1_0');
    if (shape.w > g.wy || shape.h > g.hy) continue;
    const x = rng.nextInt(g.wy - shape.w + 1);
    const y = rng.nextInt(g.hy - shape.h + 1);
    const cells = cellsOf(shape, { ix: x, iy: y }).map(([cx, cy]) => `${cx},${cy}`);
    if (cells.some((c) => used.has(c))) continue;
    cells.forEach((c) => used.add(c));
    out.push([shape.id, rng.next() < 0.5 ? 'W' : 'Y', x, y]);
  }
  return out;
}

function randomLevel(g: GeoCase, rng: any, id: number): any {
  const h = Math.max(g.hy, g.hs);
  const height = rng.nextInt(h + 1);
  const gaps: any[] =
    height >= 3 && rng.next() < 0.5
      ? [{ type: 'static', y: rng.nextInt(height - 2), size: 1 + rng.nextInt(2) }]
      : [];
  const gap = gaps[0];
  if (gap && gap.y + gap.size > height - 1) gaps.length = 0;
  return compile(
    level({
      id,
      moves: 60,
      ...sizes(g),
      plan: planRows(g, rng),
      pieces: yardPieces(g, rng, 0.55 + rng.next() * 0.3),
      wall: { height, gaps },
      gravity: { yard: rng.next() < 0.5 },
    }),
  );
}

// --- serialisation (one line per item; the C# test writes the same lines) ----------------------------------------------

const key = (n: any): string => `${n.mode}:${n.ix},${n.iy}`;
const anchor = (a: any): string => `${a.ix},${a.iy}`;
const at = (a: any): string => `${a.zone}:${a.x},${a.y},${a.seg ?? '-'}`;
const cell = (c: any): string => `${c.x},${c.y}`;
const verdict = (v: any): string => `${v.ok ? 1 : 0}[${v.reasons.join(',')}]{${v.missingSupport.map(at).join(';')}}`;
const place = (p: any): string => `${p.zone},${p.x},${p.y},${p.seg}`;
const flags = (f: any): string =>
  `${f.changed ? 1 : 0}${f.crossedWall ? 'W' : ''}${f.enteredRail ? 'R' : ''}${f.blockedByWallHeight ? 'H' : ''}${f.blockedCargo ? 'C' : ''}:${f.path.length}`;

interface PieceVector {
  readonly id: number;
  readonly pick: string;
  readonly cargo?: boolean;
  readonly canCrossWall?: boolean;
  readonly canEnterRail?: boolean;
  readonly reachableCount?: number;
  readonly starts?: string[];
  readonly nodes?: number;
  readonly hash?: string;
  readonly followHash?: string;
  lines?: string[];
  follow?: string[];
}

function pieceVector(s: any, lvl: any, id: number, keepLines: boolean): PieceVector {
  const a = tryBeginDrag(s, id);
  if (!a.ok) return { id, pick: a.reason };
  const d = a.session;
  const geo = lvl.geo;
  const lines: string[] = [];
  for (const n of d.reachableNodes()) {
    const cls = d.classify(n);
    let line = `${key(n)}:${d.distanceFromStart(n)}:${cls.kind}:${cls.reason ?? '-'}:${cls.row}:${cls.gap ?? '-'}`;
    if (cls.kind === 'siteFree' || cls.kind === 'siteRail') {
      const f = computeFall(s, id, n);
      const effect = f.effect.kind === 'break' ? `break${f.effect.penalty}` : f.effect.kind;
      line +=
        `|${f.mode}|${anchor(f.landing)}|${f.dir}|${f.distance}|${f.drift}|${f.path.map(anchor).join('>')}` +
        `|${f.cells.map(cell).join('>')}|${verdict(f.verdict)}|${f.touchesHidden ? 1 : 0}|${effect}` +
        `|${shadowInfo(f, lvl.difficulty).tone}`;
    }
    lines.push(line);
  }
  const drops: number[] = [];
  for (let ix = 0; ix + d.shape.w <= geo.wy; ix++) drops.push(dropIntoYard(s, d.shape, ix));
  lines.push(`yard:${yardDropRow(geo, d.shape)}:${drops.join(',')}`);
  const zone = pieceZone(s, id);
  if (zone === Zone.yard || zone === Zone.site) {
    const start = { zone: 'site', x: geo.siteX, y: 0, seg: visibleSegment(s) };
    const rt = returnTarget(s, id, { skipStart: true, start });
    lines.push(`rt:${rt.step}:${place(rt.to)}:${rt.dropFrom ? anchor(rt.dropFrom) : '-'}`);
    const rt0 = returnTarget(s, id);
    lines.push(`rt0:${rt0.step}:${place(rt0.to)}:${rt0.dropFrom ? anchor(rt0.dropFrom) : '-'}`);
  }
  // K-08 sticky follow sweep on a fresh session: anchor points on a 0.5-cell grid, row-major, bottom row first
  const f2 = tryBeginDrag(s, id).session;
  const follow: string[] = [];
  for (let py2 = 0; py2 < 2 * geo.rows; py2++) {
    for (let px2 = 0; px2 < 2 * geo.cols; px2++) {
      const r = f2.follow(px2 / 2, py2 / 2);
      follow.push(`${px2 / 2},${py2 / 2}>${key(r.node)}:${flags(r)}`);
    }
  }
  follow.push(`end:${key(f2.current)}`);
  const v: PieceVector = {
    id,
    pick: 'ok',
    cargo: d.cargo,
    canCrossWall: d.canCrossWall,
    canEnterRail: d.canEnterRail,
    reachableCount: d.reachableCount,
    starts: d.startNodes.map(key),
    nodes: lines.length,
    hash: fnv1a64(lines.join('\n')),
    followHash: fnv1a64(follow.join('\n')),
  };
  if (keepLines) {
    v.lines = lines;
    v.follow = follow;
  }
  return v;
}

function boardVector(name: string, lvl: any, keepLines: boolean): any {
  const s = createInitialState(lvl);
  const motions = lvl.gravity.yard ? settleYard(s).map((m: any) => `${m.kind}:${m.id}:${anchor(m.from)}>${anchor(m.to)}:${m.dy}`) : [];
  const pieces: PieceVector[] = [];
  for (let id = 0; id < lvl.layout.counts.pieces; id++) pieces.push(pieceVector(s, lvl, id, keepLines));
  return {
    name,
    geo: `${lvl.geo.wy}x${lvl.geo.hy}|${lvl.geo.ws}x${lvl.geo.hs}`,
    yardGravity: lvl.gravity.yard,
    wallHeight: lvl.wallHeight,
    gaps: lvl.gaps.map((g: any) => `${g.type}:${g.y}:${g.size}`),
    motions,
    hashAfterSettle: hashHex(s),
    movesLeft: hdr(s, H.movesLeft),
    front: buildFront(s).map(at),
    masks: siteColumnMasks(s),
    pieces,
  };
}

const boards: any[] = [];
for (let n = 1; n <= 10; n++) {
  const raw = JSON.parse(readFileSync(join(WEB, 'levels', `level_${String(n).padStart(3, '0')}.json`), 'utf8'));
  boards.push(boardVector(`level_${String(n).padStart(3, '0')}`, compile(LevelSchema.parse(raw)), n === 1));
}
const RANDOM_SEED = 20261031;
const RANDOM_PER_GEO = 5;
const rng = mulberry32(RANDOM_SEED);
for (const c of G) {
  for (let k = 0; k < RANDOM_PER_GEO; k++) boards.push(boardVector(`random ${label(c)} #${k}`, randomLevel(c, rng, 2), false));
}
for (let n = 1; n <= 10; n++) boards.push(boardVector(`archive_level_${String(n).padStart(3, '0')}`, compile(LevelSchema.parse(archiveLevel(n))), false));

// --- CL-V3C-11: wide-shape and D-106 pallet boards (level data stored with the vector) ----------------------------------
const WIDE_PLAN = ['WWW', 'YYW', 'WYW', 'YYY', 'WWW'];
const stackTraits = (stacks: readonly (readonly [number, number, number])[]): Record<number, unknown> =>
  Object.fromEntries(stacks.map(([idx, id, z]) => [idx, { stack: { id, z } }]));
const WIDE_BOARDS: readonly { readonly name: string; readonly spec: any }[] = [
  {
    name: 'wide P5_0 S4_0 Z4_90',
    spec: { wall: { height: 2 }, pieces: [['P5_0', 'W', 0, 0], ['S4_0', 'Y', 3, 0], ['Z4_90', 'W', 3, 3], ['B1_0', 'Y', 5, 0], ['D2_0', 'W', 0, 4]] },
  },
  {
    name: 'wide S4_90 Z4_0 P5_0 yard gravity',
    spec: { wall: { height: 4 }, gravity: { yard: true }, pieces: [['S4_90', 'W', 0, 0], ['Z4_0', 'Y', 3, 0], ['P5_0', 'W', 2, 4], ['O4_0', 'Y', 0, 2], ['B1_0', 'Y', 5, 6]] },
  },
  {
    name: 'wide Z4_90 S4_0 P5_0 static gap',
    spec: { wall: { height: 3, gaps: [{ type: 'static', y: 0, size: 1 }] }, pieces: [['Z4_90', 'Y', 0, 0], ['S4_0', 'W', 4, 0], ['P5_0', 'Y', 0, 3], ['D2_90', 'W', 2, 0]] },
  },
  {
    name: 'wide pallets C3_0 x3 L4_90 x2',
    spec: {
      wall: { height: 2 },
      pieces: [['C3_0', 'W', 0, 0], ['C3_0', 'W', 0, 0], ['C3_0', 'W', 0, 0], ['L4_90', 'Y', 3, 0], ['L4_90', 'Y', 3, 0], ['B1_0', 'W', 2, 4]],
      traits: stackTraits([[0, 0, 0], [1, 0, 1], [2, 0, 2], [3, 1, 0], [4, 1, 1]]),
    },
  },
  {
    name: 'wide pallets T4_0 x4 J4_270 x2 O4_0 x2',
    spec: {
      wall: { height: 3 },
      pieces: [
        ['T4_0', 'W', 0, 0], ['T4_0', 'W', 0, 0], ['T4_0', 'W', 0, 0], ['T4_0', 'W', 0, 0],
        ['J4_270', 'Y', 3, 0], ['J4_270', 'Y', 3, 0], ['O4_0', 'W', 2, 5], ['O4_0', 'W', 2, 5],
      ],
      traits: stackTraits([[0, 0, 0], [1, 0, 1], [2, 0, 2], [3, 0, 3], [4, 1, 0], [5, 1, 1], [6, 2, 0], [7, 2, 1]]),
    },
  },
];
const WIDE_LEVEL_ID = 8;
for (const { name, spec } of WIDE_BOARDS) {
  // no two blocks share a cell except the layers of one pallet (same stack id, same anchor)
  const owner = new Map<string, string>();
  spec.pieces.forEach((p: any[], i: number) => {
    const tag = spec.traits?.[i]?.stack ? `stack${spec.traits[i].stack.id}` : `piece${i}`;
    for (const [cx, cy] of cellsOf(shapeById(p[0]), { ix: p[2], iy: p[3] })) {
      const k = `${cx},${cy}`;
      if (owner.has(k) && owner.get(k) !== tag) throw new Error(`${name}: blocks overlap at ${k}`);
      owner.set(k, tag);
    }
  });
  const data = level({ id: WIDE_LEVEL_ID, schemaVersion: 3, moves: 40, yard: { cols: 6, rows: 8 }, site: { cols: 3, rows: 5 }, plan: WIDE_PLAN, ...spec });
  const parsed = LevelSchema.parse(JSON.parse(JSON.stringify(data)));
  boards.push({ ...boardVector(name, compile(parsed), false), level: JSON.parse(JSON.stringify(data)) });
}

let webCommit = '?';
try {
  webCommit = execSync('git rev-parse --short HEAD', { cwd: WEB }).toString().trim();
} catch {
  /* not a git checkout */
}
const out = {
  generatedFrom: { webRepo: 'yilmazfatih-cyber/minik-usta', webCommit, generator: 'tools/gen-p1-vectors.ts' },
  random: { seed: RANDOM_SEED, perGeometry: RANDOM_PER_GEO, levelId: 2 },
  wide: { count: WIDE_BOARDS.length, levelId: WIDE_LEVEL_ID },
  boards,
};
const target = join(ROOT, 'Assets', 'MinikUsta', 'Core.Tests', 'Golden', 'p1_vectors.json');
writeFileSync(target, JSON.stringify(out, null, 2) + '\n');
console.log(`wrote ${target} (web ${webCommit}, ${boards.length} boards, ${boards.reduce((n, b) => n + b.pieces.length, 0)} pieces)`);
if (verbose) {
  const full = {
    boards: boards.map((b) => ({ name: b.name, pieces: b.pieces.map((p: PieceVector) => ({ id: p.id, pick: p.pick, lines: p.lines, follow: p.follow })) })),
  };
  // regenerate with the lines kept for every piece
  const all: any[] = [];
  let i = 0;
  for (let n = 1; n <= 10; n++) {
    const raw = JSON.parse(readFileSync(join(WEB, 'levels', `level_${String(n).padStart(3, '0')}.json`), 'utf8'));
    all.push(boardVector(boards[i++].name, compile(LevelSchema.parse(raw)), true));
  }
  const rng2 = mulberry32(RANDOM_SEED);
  for (const c of G) for (let k = 0; k < RANDOM_PER_GEO; k++) all.push(boardVector(boards[i++].name, randomLevel(c, rng2, 2), true));
  for (let n = 1; n <= 10; n++) all.push(boardVector(boards[i++].name, compile(LevelSchema.parse(archiveLevel(n))), true));
  full.boards = all.map((b) => ({ name: b.name, pieces: b.pieces.map((p: PieceVector) => ({ id: p.id, pick: p.pick, lines: p.lines, follow: p.follow })) }));
  const vtarget = join(ROOT, 'Assets', 'MinikUsta', 'Core.Tests', 'Golden', 'p1_vectors.verbose.json');
  writeFileSync(vtarget, JSON.stringify(full, null, 1) + '\n');
  console.log(`wrote ${vtarget}`);
}
