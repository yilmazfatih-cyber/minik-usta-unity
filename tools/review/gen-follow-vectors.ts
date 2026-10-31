/**
 * Skeptic review (REVIEW_PHASE2 R-08): K-08 sticky follow under a continuous pointer. The P4 fuzz releases drags on
 * nodes of R (`moveTo`), so `DragSession.follow(px, py)` — nearest node, the D2 hysteresis, the tie order (1)–(4),
 * `blockedByWallHeight`, `blockedCargo` — was only checked by the hand-written TS tests. This generator plays seeded
 * pointer walks (quarter-cell grid points to hit exact ties, free points, far jumps) over every pickable block of the
 * states along the golden logs of levels 1–10 and records, per step, what the C# `DragSession` must return.
 *
 * Usage: node tools/review/gen-follow-vectors.ts [/path/to/minik-usta] > Assets/MinikUsta/Core.Tests/Golden/review_follow_vectors.json
 */
import { readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const WEB = resolve(process.argv[2] ?? join(import.meta.dirname, '..', '..', '..', 'minik-usta'));
const web = (p: string) => import(pathToFileURL(join(WEB, p)).href);
const { loadLevel } = await web('src/core/level/compile.ts');
const { GameSession } = await web('src/core/session.ts');
const { tryBeginDrag, isCancelled } = await web('src/core/movement.ts');
const { mulberry32 } = await web('src/core/rng.ts');
const { hashHex } = await web('src/core/hash.ts');

const STEPS = 32;
const node = (n: { ix: number; iy: number; mode: number }): string => `${n.ix},${n.iy},${n.mode}`;
const drop = (d: { kind: string; reason?: string; row: number; gap?: number }): string =>
  `${d.kind}:${d.reason ?? ''}:${d.row}:${d.gap ?? -1}`;
const drags: unknown[] = [];
for (let level = 1; level <= 10; level++) {
  const id = String(level).padStart(3, '0');
  const json = JSON.parse(readFileSync(join(WEB, 'levels', `level_${id}.json`), 'utf8'));
  const hand = JSON.parse(readFileSync(join(WEB, 'tests', 'golden', `level_${id}.hand.json`), 'utf8'));
  const lr = loadLevel(json);
  if (!lr.ok) throw new Error(`level ${id} does not load`);
  const log = hand.log as { kind: string; preBoosters?: string[]; streakTier?: number }[];
  const rng = mulberry32(level * 7919 + 17);
  for (let k = 0; k < log.length; k += 2) {
    const start = log[0] as { preBoosters: string[]; streakTier: number };
    const game = GameSession.start(lr.level, start.preBoosters, start.streakTier);
    for (let i = 1; i <= k; i++) game.apply(log[i]);
    const s = game.state;
    if (game.outcome !== 'playing') continue;
    const P = lr.level.layout.counts.pieces;
    for (let pieceId = 0; pieceId < P; pieceId++) {
      const a = tryBeginDrag(s, pieceId, game.hooks.drag);
      if (!a.ok) continue;
      const ses = a.session;
      const geo = lr.level.geo;
      let px = ses.current.ix + rng.next() * 0.5;
      let py = ses.current.iy + rng.next() * 0.5;
      const pointer: number[] = [];
      const lines: string[] = [];
      for (let t = 0; t < STEPS; t++) {
        const r = rng.next();
        if (r < 0.1) {
          px = rng.next() * (geo.cols + 2) - 1;
          py = rng.next() * (geo.rows + 2) - 1;
        } else {
          px += (rng.next() - 0.5) * 2.2;
          py += (rng.next() - 0.5) * 2.2;
        }
        if (r > 0.45) {
          px = Math.round(px * 4) / 4;
          py = Math.round(py * 4) / 4;
        }
        px = Math.min(geo.cols + 1, Math.max(-1.5, px));
        py = Math.min(geo.rows + 1, Math.max(-1.5, py));
        pointer.push(px, py);
        const nearest = ses.nearest(px, py);
        const f = ses.follow(px, py);
        const flags = [f.changed, f.crossedWall, f.enteredRail, f.blockedByWallHeight, f.blockedCargo].map((b) => (b ? 1 : 0)).join('');
        lines.push(`${node(nearest)}>${node(f.node)}|${flags}|${f.path.map(node).join(' ')}|${drop(ses.classify())}`);
      }
      drags.push({ level, prefix: k, pieceId, z: hashHex(s), pointer, lines, cancelled: isCancelled(ses.classify()) });
    }
  }
}
process.stdout.write(JSON.stringify({ generator: 'tools/review/gen-follow-vectors.ts', steps: STEPS, drags }) + '\n');
