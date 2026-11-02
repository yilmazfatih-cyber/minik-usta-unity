#!/usr/bin/env node
/**
 * B1 FX / new-frame packer (package B1; docs: art/3d/blender/env/README.md). Packs the NEW 3D frames written by
 * art/3d/blender/env/post_env.py (`art/3d/out/env/fx/frames.json` + `<name>.png`, trimmed to their alpha box) - FX clip
 * drawings, icon_*_xl, the home parallax layers - into pages of at most `--max` px (the MaxRects packer of pack3d.mjs, edge
 * extrusion into the `--pad` gutter) and writes
 *   Assets/MinikUsta/Resources/art3d/fx_<N>.bytes   (PNG; TextAsset "art3d/fx_<N>" -> ImageConversion.LoadImage; flat name, P3-13)
 *   Assets/MinikUsta/Resources/art3d/fx.json        (frames + clips + layers; format in the README)
 * Frames are grouped per page by use (`level`: fx clips + reward icons, `home`: parallax layers) so a screen can load only
 * its pages. Fails when the pages exceed the RGBA32 budget (`--budget-mb`, default 8).
 *
 * Usage: node tools/art/packfx.mjs [--frames art/3d/out/env/fx] [--out <dir>] [--pad 2] [--max 2048] [--budget-mb 8] [--png <dir>]
 */
import { existsSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { decodePng, encodePng, packMaxRects } from './pack3d.mjs';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const args = process.argv.slice(2);
const opt = (name, def) => {
  const i = args.indexOf('--' + name);
  return i >= 0 && i + 1 < args.length ? args[i + 1] : def;
};
const FRAMES = resolve(opt('frames', join(ROOT, 'art', '3d', 'out', 'env', 'fx')));
const OUT = resolve(opt('out', join(ROOT, 'Assets', 'MinikUsta', 'Resources', 'art3d')));
const PAD = Number(opt('pad', '2'));
const MAX = Number(opt('max', '2048'));
const BUDGET_MB = Number(opt('budget-mb', '8'));
const PNG_DIR = opt('png', '');
const PPU = 100;
const NAME = 'fx';

function blit(page, pw, f, img, pad) {
  for (let y = -pad; y < f.h + pad; y++) {
    const sy = Math.min(f.h - 1, Math.max(0, y));
    for (let x = -pad; x < f.w + pad; x++) {
      const sx = Math.min(f.w - 1, Math.max(0, x));
      const si = (sy * f.w + sx) * 4;
      const di = ((f.y + y) * pw + (f.x + x)) * 4;
      page[di] = img.data[si];
      page[di + 1] = img.data[si + 1];
      page[di + 2] = img.data[si + 2];
      page[di + 3] = img.data[si + 3];
    }
  }
}

const round6 = (v) => Math.round(v * 1e6) / 1e6;

if (import.meta.url === `file://${process.argv[1]}`) {
  const t0 = Date.now();
  const meta = JSON.parse(readFileSync(join(FRAMES, 'frames.json'), 'utf8'));
  const contract = JSON.parse(readFileSync(join(ROOT, 'art', '3d', 'spec', 'frames.json'), 'utf8'));
  const scales = JSON.parse(readFileSync(join(ROOT, 'art', '3d', 'blender', 'env', 'scales.json'), 'utf8')).newFrames;
  const names = Object.keys(meta).sort();
  const use = (n) => (meta[n].group === 'bg' || meta[n].group === 'town' ? 'home' : 'level');
  const all = names.map((name) => {
    const m = meta[name];
    const img = decodePng(readFileSync(join(FRAMES, name + '.png')));
    if (img.w !== m.w || img.h !== m.h) throw new Error(`${name}: PNG ${img.w}x${img.h} != frames.json ${m.w}x${m.h}`);
    return { name, w: m.w, h: m.h, img, meta: m, use: use(name) };
  });
  // pack per use group so a screen loads only its own pages
  const pages = [];
  const placedFrames = [];
  for (const group of ['level', 'home']) {
    const fr = all.filter((f) => f.use === group);
    if (!fr.length) continue;
    const pg = packMaxRects(fr, MAX, PAD);
    for (const f of fr) f.atlas += pages.length;
    for (const p of pg) pages.push({ ...p, index: pages.length, use: group });
    placedFrames.push(...fr);
  }
  const bytes = pages.reduce((s, p) => s + p.w * p.h * 4, 0);
  const buffers = pages.map((p) => Buffer.alloc(p.w * p.h * 4));
  for (const f of placedFrames) blit(buffers[f.atlas], pages[f.atlas].w, f, f.img, PAD);
  const frames = {};
  for (const f of [...placedFrames].sort((a, b) => (a.name < b.name ? -1 : 1))) {
    const m = f.meta;
    frames[f.name] = {
      atlas: f.atlas,
      x: f.x,
      y: f.y,
      w: f.w,
      h: f.h,
      pivotX: m.pivotX,
      pivotY: m.pivotY,
      borderL: 0,
      borderT: 0,
      borderR: 0,
      borderB: 0,
      scale: m.scale,
      ox: m.ox,
      oy: m.oy,
      cw: m.cw,
      ch: m.ch,
    };
  }
  // clips: the frames.json contract timelines (24 / 60 fps, loops, events) on the stored drawings
  const clips = {};
  for (const [cn, c] of Object.entries(contract.clips)) {
    if (!cn.startsWith('fx_')) continue;
    const prefix = c.frameName.replace('{NN}', '');
    const drawn = Object.keys(frames).filter((n) => n.startsWith(prefix) && /_\d\d$/.test(n));
    if (!drawn.length) continue;
    const sc = Object.entries(scales).find(([k]) => k === prefix + '*')?.[1];
    clips[cn] = {
      frames: c.timeline.map((i) => c.frameName.replace('{NN}', String(i).padStart(2, '0'))),
      fps: c.fps,
      loop: c.loop,
      out: null,
      events: c.events,
      canvas: c.canvas,
      pivot: c.pivot,
      scale: sc ? sc.scale : c.scale,
      ...(c.tint ? { tint: c.tint } : {}),
      blend: /impact_star|star_burst|segment_done|lock_shine|trowel_sparkle/.test(cn) ? 'additive-look (pre-lit emission, draw normal alpha)' : 'normal',
      juice: c.juice ?? undefined,
      use: c.use,
    };
    for (const n of clips[cn].frames) if (!frames[n]) throw new Error(`clip ${cn} -> missing frame ${n}`);
  }
  const layers = {};
  for (const [n, f] of Object.entries(meta)) if (f.layer) layers[n] = { frame: n, ...f.layer };
  // art turn 1: the chapter 1 tree house in stages (art/3d/blender/env/town.py). Every frame shares the 760 x 820 canvas
  // and its pivot (0.5, 0): draw `base` then parts[0 .. won - 1] at the SAME pivot point (no offsets needed); the
  // level -> piece map gives the win-window 3D piece (bld_*_3d) of each level of the chapter.
  const stages = {};
  if (frames.town_ch1_s00) {
    const parts = Object.keys(frames).filter((n) => /^town_ch1_p\d\d$/.test(n)).sort();
    const pieces = ['tree_steps', 'platform', 'front_wall', 'window_sill', 'two_rooms', 'tall_trunk', 'under_roof', 'garden_fence', 'rope_ladder', 'treehouse'];
    stages.town_ch1 = {
      canvas: [760, 820],
      pivot: [0.5, 0.0],
      base: 'town_ch1_s00',
      parts,
      finished: 'town_ch1_treehouse (atlas.json, v4 name)',
      levelPieces: Object.fromEntries(pieces.map((p, i) => [String(i + 1), `bld_${p}_3d`]).filter(([, f]) => frames[f])),
    };
  }
  const json = {
    version: 1,
    doc: 'art/3d/blender/env/README.md',
    generator: 'tools/art/packfx.mjs',
    ppu: PPU,
    pad: PAD,
    origin: 'top-left',
    pivotOrigin: 'bottom-left',
    units:
      'x y w h = atlas px (top-left origin; Unity Rect(x, H - y - h, w, h)); pivot = the clip / canvas pivot relative to the TRIMMED rect (may lie outside 0..1); ' +
      'scale = raster px per design px, sprite ppu = ppu * scale; ox oy = trimmed rect offset from the bottom-left of the full canvas cw x ch (stored px); ' +
      'layers: x y w h = where the layer sits on the 1080 x 1920 home canvas (design px, bottom-left origin), parallax factor, z order',
    memoryBytesRGBA32: bytes,
    atlases: pages.map((p) => ({ file: `art3d/${NAME}_${p.index}`, w: p.w, h: p.h, use: p.use })),
    frames,
    clips,
    layers,
    stages,
  };
  if (bytes > BUDGET_MB * 1048576) {
    console.error(`packfx: ${pages.map((p) => p.w + 'x' + p.h).join(', ')} = ${(bytes / 1048576).toFixed(2)} MB > budget ${BUDGET_MB} MB`);
    process.exit(1);
  }
  mkdirSync(OUT, { recursive: true });
  for (const f of readdirSync(OUT)) if (f.startsWith(NAME + '_') && f.endsWith('.bytes')) rmSync(join(OUT, f));
  let total = 0;
  buffers.forEach((buf, i) => {
    const png = encodePng(pages[i].w, pages[i].h, buf);
    writeFileSync(join(OUT, `${NAME}_${i}.bytes`), png);
    total += png.length;
    if (PNG_DIR) {
      mkdirSync(PNG_DIR, { recursive: true });
      writeFileSync(join(PNG_DIR, `${NAME}-${i}.png`), png);
    }
  });
  writeFileSync(join(OUT, `${NAME}.json`), JSON.stringify(json, null, 1) + '\n');
  console.log(
    `packfx: ${placedFrames.length} frames, ${Object.keys(clips).length} clips, ${Object.keys(layers).length} layers -> ${pages.length} page(s) ` +
      `${pages.map((p) => p.w + 'x' + p.h + ' ' + p.use).join(', ')}; RGBA32 ${(bytes / 1048576).toFixed(2)} MB (budget ${BUDGET_MB}); PNG ${(total / 1048576).toFixed(2)} MB; ` +
      `${((Date.now() - t0) / 1000).toFixed(1)} s`,
  );
  if (!existsSync(OUT)) process.exit(1);
}
