// Writes every generated SVG under art/svg/<category>/<frame>.svg (design-lead; v4 art pack, D-098).
// Usage: node art/gen/build.mjs            (no dependencies; Node ≥ 22)
// Hand-tuned parts (characters, logo, backgrounds, icons) are template code in the sibling modules; the SVG files are
// the reviewable sources the rasteriser (tools/art/rasterize.mjs) reads.
import { mkdirSync, rmSync, writeFileSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { resetIds } from './lib.mjs';
import * as blocks from './blocks.mjs';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const OUT = join(ROOT, 'art', 'svg');

const frames = [];
const add = (cat, name, svg) => frames.push({ cat, name, svg });

export async function collect() {
  const { COLORS, MASK_IDS, pad2 } = blocks;
  for (const code of Object.keys(COLORS)) {
    const c = code.toLowerCase();
    for (const m of MASK_IDS) {
      resetIds();
      add('block', `blk_${c}_${pad2(m)}`, blocks.blockTile(code, m));
    }
    for (const k of ['tl', 'tr', 'bl', 'br']) {
      for (const m of blocks.concaveMasks(k)) {
        resetIds();
        add('block', `blk_${c}_${pad2(m)}_cc_${k}`, blocks.concaveTile(code, k, m));
      }
    }
    resetIds();
    add('block', `sym_${c}`, blocks.symbolTile(code));
    add('block', `stud_${c}`, blocks.studCap(code));
    add('ghost', `ghost_${c}`, blocks.ghostTile(code));
    add('ghost', `ghost_${c}_front`, blocks.ghostTile(code, true));
  }
  add('block', 'blk_gloss', blocks.studGloss());
  add('block', 'blk_locked', blocks.lockedOverlay());
  add('ghost', 'ghost_front', blocks.ghostFrontLine());
  add('ghost', 'ghost_hidden', blocks.ghostHidden());
  for (const m of MASK_IDS) {
    for (const kind of ['fill', 'line', 'dash']) {
      resetIds();
      add('shadow', `shadow_${kind}_${pad2(m)}`, blocks.shadowTile(kind, m));
    }
  }
  const extra = ['board', 'ui', 'icons', 'fx', 'bg', 'chr', 'logo'];
  for (const mod of extra) {
    const p = join(dirname(fileURLToPath(import.meta.url)), mod + '.mjs');
    if (!existsSync(p)) continue;
    const m = await import(p);
    for (const f of m.frames()) {
      resetIds();
      add(f.cat ?? mod, f.name, typeof f.svg === 'function' ? f.svg() : f.svg);
    }
  }
  return frames;
}

if (import.meta.url === `file://${process.argv[1]}`) {
  await collect();
  rmSync(OUT, { recursive: true, force: true });
  const names = new Set();
  let bytes = 0;
  for (const f of frames) {
    if (names.has(f.name)) throw new Error('duplicate frame ' + f.name);
    if (!/^[a-z0-9_]+$/.test(f.name)) throw new Error('frame name must be lower case + underscore: ' + f.name);
    if (/<(filter|mask|image)\b/.test(f.svg)) throw new Error('forbidden element in ' + f.name);
    names.add(f.name);
    const p = join(OUT, f.cat, f.name + '.svg');
    mkdirSync(dirname(p), { recursive: true });
    writeFileSync(p, f.svg);
    bytes += f.svg.length;
  }
  console.log(`art/svg: ${frames.length} frames, ${(bytes / 1024).toFixed(0)} KB`);
}
