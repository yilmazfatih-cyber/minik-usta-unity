/**
 * Generates Assets/MinikUsta/Core.Tests/Golden/p1_vectors.json from the TypeScript core (the source of truth, D-091):
 * the P1 modules (movement, gravity, placement, delivery) evaluated on the 10 shipped levels and on 45 seeded random
 * boards of the 9 geometries of TECH §2R.1. Per board: settleYard motions (yard gravity boards), state hash after the
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

let webCommit = '?';
try {
  webCommit = execSync('git rev-parse --short HEAD', { cwd: WEB }).toString().trim();
} catch {
  /* not a git checkout */
}
const out = {
  generatedFrom: { webRepo: 'yilmazfatih-cyber/minik-usta', webCommit, generator: 'tools/gen-p1-vectors.ts' },
  random: { seed: RANDOM_SEED, perGeometry: RANDOM_PER_GEO, levelId: 2 },
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
  full.boards = all.map((b) => ({ name: b.name, pieces: b.pieces.map((p: PieceVector) => ({ id: p.id, pick: p.pick, lines: p.lines, follow: p.follow })) }));
  const vtarget = join(ROOT, 'Assets', 'MinikUsta', 'Core.Tests', 'Golden', 'p1_vectors.verbose.json');
  writeFileSync(vtarget, JSON.stringify(full, null, 1) + '\n');
  console.log(`wrote ${vtarget}`);
}
