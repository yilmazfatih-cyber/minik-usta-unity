/**
 * Generates Assets/MinikUsta/Core.Tests/Golden/p0_vectors.json from the TypeScript core (the source of truth, D-091):
 * per-level initial Zobrist hash, ASCII boards, encoded buffer, layout, cover totals, Zobrist table heads, plus RNG /
 * FNV / canonical-JSON / grid-walk vectors. The C# tests replay the same computations and must match bit for bit.
 *
 * Usage: node tools/gen-p0-vectors.ts [/path/to/minik-usta]   (node 22 type stripping; no extra packages; the web repo's
 * node_modules provide zod and vitest for its own modules)
 */
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { execSync } from 'node:child_process';

const HERE = dirname(fileURLToPath(import.meta.url));
const ROOT = resolve(HERE, '..');
const WEB = resolve(process.argv[2] ?? join(ROOT, '..', 'minik-usta'));
const web = (p: string): Promise<any> => import(pathToFileURL(join(WEB, p)).href);

const { LevelSchema } = await web('src/core/level/schema.ts');
const { compile } = await web('src/core/level/compile.ts');
const { createInitialState, encodeState, PF, pieceField, setPieceField, yardOcc, computeLayout } = await web('src/core/state.ts');
const { hashHex, buildZobrist } = await web('src/core/hash.ts');
const { toAscii } = await web('src/core/ascii.ts');
const { canonicalJson, fnv1a64 } = await web('src/core/moves.ts');
const { mulberry32 } = await web('src/core/rng.ts');
const { shapeByIndex } = await web('src/core/shapes.ts');
const { occupyPiece, vacatePiece, stateInvariantErrors } = await web('src/core/grid.ts');
const { initialState } = await web('tests/fixtures/builders.ts');

const hex = (n: number): string => '0x' + (n >>> 0).toString(16).padStart(8, '0');
const heads = (a: Uint32Array, n: number): string[] => Array.from(a.slice(0, n)).map(hex);

const levels: unknown[] = [];
for (let n = 1; n <= 10; n++) {
  const raw = JSON.parse(readFileSync(join(WEB, 'levels', `level_${String(n).padStart(3, '0')}.json`), 'utf8'));
  const data = LevelSchema.parse(raw);
  const lvl = compile(data);
  const s = createInitialState(lvl);
  levels.push({
    level: n,
    levelHash: fnv1a64(canonicalJson(data)),
    rawEqualsParsed: canonicalJson(raw) === canonicalJson(data),
    initialHash: hashHex(s),
    ascii: toAscii(s),
    asciiIds: toAscii(s, { ids: true }),
    encoded: encodeState(s),
    layout: lvl.layout,
    cycle: lvl.cycle,
    seed: lvl.seed,
    mechanics: lvl.mechanics,
    step10: lvl.step10.map((t: { id: string }) => t.id),
    supply: lvl.supply,
    demand: lvl.demand,
    materialCount: lvl.materialCount,
    zobrist: {
      pos0: heads(lvl.zobrist.pos0, 4),
      pos1: heads(lvl.zobrist.pos1, 2),
      field0: heads(lvl.zobrist.field0, 2),
      field1: heads(lvl.zobrist.field1, 2),
      lengths: [lvl.zobrist.pos0.length, lvl.zobrist.field0.length],
    },
  });
}

const z = buildZobrist(computeLayout({ segments: 1, pieces: 1, gaps: 0, obstacles: 0, hidden: 0, goals: 1 }), 1);
const zobristP1 = {
  pos0: heads(z.pos0, 6),
  pos0At240: hex(z.pos0[240]),
  pos1First: hex(z.pos1[0]),
  field0: heads(z.field0, 3),
  field1First: hex(z.field1[0]),
  lengths: [z.pos0.length, z.field0.length],
};

// grid.test.ts "1 000 seeded random yard relocations"
const walkSpec = {
  pieces: [
    ['O4_0', 'W', 0, 0],
    ['D2_0', 'Y', 2, 0],
    ['D2_90', 'W', 3, 0],
    ['C3_90', 'Y', 0, 4],
    ['B1_0', 'W', 5, 7],
  ],
  obstacles: [{ type: 'crate', x: 4, y: 4, hp: 2 }],
};
const walk = (seed: number): string[] => {
  const s = initialState(walkSpec);
  const rng = mulberry32(seed);
  const hashes: string[] = [];
  for (let step = 0; step < 1000; step++) {
    const id = rng.nextInt(5);
    const shape = shapeByIndex(pieceField(s, id, PF.shape));
    const x = rng.nextInt(6 - shape.w + 1);
    const y = rng.nextInt(8 - shape.h + 1);
    vacatePiece(s, id);
    const free = shape.cells.every((c: { x: number; y: number }) => yardOcc(s, x + c.x, y + c.y) === 0);
    if (free) {
      setPieceField(s, id, PF.x, x);
      setPieceField(s, id, PF.y, y);
    }
    occupyPiece(s, id);
    if (stateInvariantErrors(s).length > 0) throw new Error('invariant broken in the TS walk');
    hashes.push(hashHex(s));
  }
  return hashes;
};
const w7 = walk(7);
const w8 = walk(8);
const gridWalk = {
  seed7: { first: w7[0], at500: w7[500], last: w7[999], fnvAll: fnv1a64(w7.join(',')) },
  seed8: { last: w8[999] },
};

const hashSpec = {
  id3: hashHex(
    initialState({
      id: 3,
      pieces: [
        ['B1_0', 'W', 0, 0],
        ['O4_0', 'Y', 2, 0],
      ],
    }),
  ),
  appendix: hashHex(
    initialState({
      id: 4,
      wall: { height: 6, gaps: [{ type: 'static', y: 3, size: 1 }] },
      plan: ['YY', 'WW', 'W.', 'WW', 'YY'],
      pieces: [
        ['D2_90', 'Y', 0, 7],
        ['D2_0', 'W', 2, 6],
        ['O4_0', 'G', 3, 4],
        ['B1_0', 'W', 5, 3],
        ['D2_90', 'Y', 0, 0],
        ['D2_90', 'W', 2, 0],
        ['D2_0', 'W', 4, 0],
      ],
      obstacles: [
        { type: 'crate', x: 5, y: 4, hp: 2 },
        { type: 'cement_bag', x: 0, y: 2 },
        { type: 'screw', x: 3, y: 4 },
        { type: 'key', x: 5, y: 3, id: 'a' },
      ],
    }),
  ),
};

const r9 = mulberry32(9);
const rm5 = mulberry32(-5);
const rng = {
  seed9: [0, 1, 2, 3, 4].map(() => [r9.next(), r9.nextInt(7)]),
  seedMinus5U32: [0, 1, 2].map(() => hex(rm5.nextU32())),
};

const canonicalInput = { b: 1, a: { d: [2, { z: 1, y: 2 }], c: undefined }, n: 0.35, m: -0, k: 1e21, j: 1.5e-7, s: 'x"y\\z\n\u0001é' };
const fnv = {
  empty: fnv1a64(''),
  a: fnv1a64('a'),
  foobar: fnv1a64('foobar'),
  dotlessI: fnv1a64('ı'),
  turkish: fnv1a64('Ağaç Basamakları'),
  emoji: fnv1a64('a😀b'),
  canonicalSample: canonicalJson(canonicalInput),
  canonicalSampleHash: fnv1a64(canonicalJson(canonicalInput)),
};

let webCommit = '?';
try {
  webCommit = execSync('git rev-parse --short HEAD', { cwd: WEB }).toString().trim();
} catch {
  /* not a git checkout */
}
const out = {
  generatedFrom: { webRepo: 'yilmazfatih-cyber/minik-usta', webCommit, generator: 'tools/gen-p0-vectors.ts' },
  levels,
  zobristP1,
  gridWalk,
  hashSpec,
  rng,
  fnv,
};
const target = join(ROOT, 'Assets', 'MinikUsta', 'Core.Tests', 'Golden', 'p0_vectors.json');
writeFileSync(target, JSON.stringify(out, null, 2) + '\n');
console.log(`wrote ${target} (web ${webCommit})`);
