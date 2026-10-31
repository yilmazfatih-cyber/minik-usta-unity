/**
 * Skeptic review (REVIEW_PHASE2 R-13): D3a (K-30) result and expansion counter (`D3A_STATS.expansions`) per budget.
 * The fuzz only sees the result of the default budget on small sites (levels 1–10: at most 16 expansions), but the
 * counter decides `unknown` near the budget, so it must match node for node on large searches too. Boards: seeded
 * random 4 × 8 sites with two segments, exactly tiled by the supply (batch 0 one B1 in the yard, the rest pending in
 * batches for segment 0 and 1), plus recoloured variants (dead searches that fill the memo); budgets 1, 37, 500, 5 000
 * and the default. The levels pass the schema only (compile without logic checks, like `Compiler.Compile`).
 *
 * Usage: node tools/review/gen-d3a-vectors.ts [/path/to/minik-usta] > Assets/MinikUsta/Core.Tests/Golden/review_d3a_vectors.json
 */
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const WEB = resolve(process.argv[2] ?? join(import.meta.dirname, '..', '..', '..', 'minik-usta'));
const web = (p: string) => import(pathToFileURL(join(WEB, p)).href);
const { LevelSchema } = await web('src/core/level/schema.ts');
const { compile } = await web('src/core/level/compile.ts');
const { createInitialState, cloneState, setPieceField, PF } = await web('src/core/state.ts');
const { tileRemaining, D3A_STATS, D3A_MAX_EXPANSIONS } = await web('src/core/deadlock.ts');
const { shapeById } = await web('src/core/shapes.ts');
const { mulberry32 } = await web('src/core/rng.ts');

const BUDGETS = [1, 37, 500, 5000, D3A_MAX_EXPANSIONS];
const SHAPES = ['B1_0', 'D2_0', 'D2_90', 'I3_0', 'I3_90', 'O4_0', 'L4_0', 'L4_90', 'J4_0', 'T4_0', 'T4_180', 'S4_0', 'Z4_0', 'I4_0', 'C3_0'];
const COLORS = ['W', 'Y', 'G', 'R'];
const WS = 4;
const HS = 8;
const rng = mulberry32(31102026);

/** Bottom-up tiling of a WS × HS plan: returns pieces {shape, color, x, y} and the plan rows (top → bottom). */
function tile(ncol: number): { pieces: { shape: string; color: string; x: number; y: number }[]; rows: string[] } {
  const height = new Array(WS).fill(0);
  const grid: string[][] = Array.from({ length: HS }, () => new Array(WS).fill('W'));
  const pieces: { shape: string; color: string; x: number; y: number }[] = [];
  for (let guard = 0; guard < 400 && height.some((h) => h < HS); guard++) {
    const id = SHAPES[rng.nextInt(SHAPES.length)];
    const sh = shapeById(id);
    const fits: number[] = [];
    for (let x0 = 0; x0 + sh.w <= WS; x0++) {
      const y0 = height[x0] - sh.colBottom[0];
      let ok = y0 >= 0;
      for (let j = 0; j < sh.w && ok; j++) {
        if (y0 + sh.colBottom[j] !== height[x0 + j] || y0 + sh.colTop[j] >= HS) ok = false;
      }
      if (ok) fits.push(x0);
    }
    if (fits.length === 0) continue;
    const x0 = fits[rng.nextInt(fits.length)];
    const y0 = height[x0] - sh.colBottom[0];
    const color = COLORS[rng.nextInt(ncol)];
    for (const c of sh.cells) grid[y0 + c.y][x0 + c.x] = color;
    for (let j = 0; j < sh.w; j++) height[x0 + j] = y0 + sh.colTop[j] + 1;
    pieces.push({ shape: id, color, x: x0, y: y0 });
  }
  // fill what is left with B1 (always fits on top of a column)
  for (let x = 0; x < WS; x++) {
    while (height[x] < HS) {
      const color = COLORS[rng.nextInt(ncol)];
      grid[height[x]][x] = color;
      pieces.push({ shape: 'B1_0', color, x, y: height[x] });
      height[x]++;
    }
  }
  return { pieces, rows: grid.map((r) => r.join('')).reverse() };
}

const boards: { level: string; rows: string[] }[] = [];
for (let n = 0; n < 24; n++) {
  const ncol = 2 + rng.nextInt(3);
  const a = tile(ncol);
  const b = tile(ncol);
  // shuffle each segment's pieces (class order and counts do not depend on it, the piece ids do)
  for (const t of [a, b]) for (let i = t.pieces.length - 1; i > 0; i--) { const j = rng.nextInt(i + 1); [t.pieces[i], t.pieces[j]] = [t.pieces[j], t.pieces[i]]; }
  const first = a.pieces.findIndex((p) => p.shape === 'B1_0');
  const yardPiece = first >= 0 ? a.pieces.splice(first, 1)[0] : null;
  const level = {
    id: 41, chapter: 5, name: { tr: 'R', en: 'R' }, difficulty: 'hard', moves: 99,
    goals: [{ type: 'build' }], gravity: { build: 'normal', yard: false }, wall: { height: 4, gaps: [] },
    site: { cols: WS, rows: HS },
    build: { mode: 'segments', segments: [{ name: { tr: 'a', en: 'a' }, rows: a.rows }, { name: { tr: 'b', en: 'b' }, rows: b.rows }] },
    yard: {
      cols: 4, rows: 8,
      batches: [
        { forSegment: 0, pieces: yardPiece ? [{ shape: 'B1_0', color: yardPiece.color, x: 0, y: 0 }] : [{ shape: 'B1_0', color: 'W', x: 0, y: 0 }] },
        { forSegment: 0, pieces: a.pieces.map((p) => ({ shape: p.shape, color: p.color, x: 0, y: 8 })) },
        { forSegment: 1, pieces: b.pieces.map((p) => ({ shape: p.shape, color: p.color, x: 0, y: 8 })) },
      ],
    },
    obstacles: [],
  };
  if (!yardPiece) level.yard.batches[0].pieces[0].color = 'W';
  boards.push({ level: JSON.stringify(level), rows: [] });
}

const out: { level: string; rows: string[] }[] = [];
for (const bd of boards) {
  const parsed = LevelSchema.safeParse(JSON.parse(bd.level));
  if (!parsed.success) throw new Error('schema: ' + JSON.stringify(parsed.error.issues.slice(0, 2)));
  const lvl = compile(parsed.data);
  const s0 = createInitialState(lvl);
  const P = lvl.layout.counts.pieces;
  const variants: { name: string; s: unknown }[] = [{ name: 'init', s: s0 }];
  for (let v = 0; v < 4; v++) {
    const s = cloneState(s0);
    const p = rng.nextInt(P);
    const c = rng.nextInt(4);
    setPieceField(s, p, PF.color, c);
    variants.push({ name: `p${p}c${c}`, s });
  }
  const rows: string[] = [];
  for (const { name, s } of variants) rows.push(`${name}|` + BUDGETS.map((b) => `${tileRemaining(s, b)}:${D3A_STATS.expansions}`).join(' '));
  out.push({ level: bd.level, rows });
}
process.stdout.write(JSON.stringify({ generator: 'tools/review/gen-d3a-vectors.ts', budgets: BUDGETS, boards: out }) + '\n');
