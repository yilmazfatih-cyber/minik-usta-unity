#!/usr/bin/env node
/**
 * Atlas extension for the v3 block traits (paket E3, docs/ART_UNITY.md section 9). The shipped atlas (pages 0-2) is the
 * Blender render of package B1 and cannot be rebuilt from this repository (the 3D PNG sources are not checked in), so the
 * new frames - the SVGs under `art/svg/trait/` (sources in `art/src/trait/`) - are rasterised, packed into EXTRA pages
 * that follow the existing ones, and merged into `Resources/art/atlas.json`:
 *
 *   node tools/art/extend.mjs --web <web checkout with node_modules>   (needs Chromium: /opt/pw-browsers/chromium)
 *
 * The run is idempotent: frames and pages written by an earlier run (`extend.frames`, `extend.firstPage` in atlas.json)
 * are dropped first, so editing a trait SVG and running it again replaces the old frames. It never touches pages
 * 0..firstPage-1. The frame names, the atlas.json format and the page naming (`art/atlas_<N>`, `atlas_<N>.bytes`) stay the
 * v4 contract (ART_UNITY section 3); `AtlasData` / `Atlas` read the extra pages like any other.
 *
 * Options: --web <dir>  --pad 2  --max 1024  --png <dir> (plain review copies)  --dir art/svg/trait
 */
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { atlasJson, pack, rasterise, readFrames } from './rasterize.mjs';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const args = process.argv.slice(2);
const opt = (n, d) => {
  const i = args.indexOf('--' + n);
  return i >= 0 && i + 1 < args.length ? args[i + 1] : d;
};
const MAX = Number(opt('max', '1024'));
const PAD = Number(opt('pad', '2'));
const DIR = resolve(opt('dir', join(ROOT, 'art', 'svg', 'trait')));
const OUT = resolve(opt('out', join(ROOT, 'Assets', 'MinikUsta', 'Resources', 'art')));
const PNG_DIR = opt('png', '');

const jsonPath = join(OUT, 'atlas.json');
const atlas = JSON.parse(readFileSync(jsonPath, 'utf8'));
const prior = atlas.extend ?? null;
let firstPage = atlas.atlases.length;
if (prior) {
  firstPage = prior.firstPage;
  for (const n of prior.frames) delete atlas.frames[n];
  atlas.atlases = atlas.atlases.slice(0, firstPage);
  for (let i = firstPage; i < firstPage + prior.pages; i++) rmSync(join(OUT, `atlas_${i}.bytes`), { force: true });
}

const frames = readFrames(DIR);
const pages = pack(frames, MAX, PAD);
const pngs = await rasterise(frames, pages);
const fragment = atlasJson(frames, pages);
pngs.forEach((buf, i) => {
  writeFileSync(join(OUT, `atlas_${firstPage + i}.bytes`), buf);
  if (PNG_DIR) {
    mkdirSync(PNG_DIR, { recursive: true });
    writeFileSync(join(PNG_DIR, `atlas-${firstPage + i}.png`), buf);
  }
});
for (const [name, f] of Object.entries(fragment.frames)) atlas.frames[name] = { ...f, atlas: f.atlas + firstPage };
fragment.atlases.forEach((p, i) => atlas.atlases.push({ file: `art/atlas_${firstPage + i}`, w: p.w, h: p.h }));
atlas.extend = {
  doc: 'docs/ART_UNITY.md section 9',
  generator: 'tools/art/extend.mjs',
  firstPage,
  pages: pages.length,
  frames: Object.keys(fragment.frames).sort(),
};
writeFileSync(jsonPath, JSON.stringify(atlas, null, 1) + '\n');
console.log(`atlas extend: ${frames.length} frames -> page ${firstPage}${pages.length > 1 ? '-' + (firstPage + pages.length - 1) : ''} (${pages.map((p) => p.w + 'x' + p.h).join(', ')}), ${pngs.reduce((s, b) => s + b.length, 0)} bytes`);
if (!existsSync(join(OUT, `atlas_${firstPage}.bytes`))) throw new Error('page not written');
