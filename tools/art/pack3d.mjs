#!/usr/bin/env node
/**
 * 3D character sheet packer (design-lead, package B1k; docs: art/3d/blender/king/README.md). Reads the trimmed frames
 * written by art/3d/blender/king/post_king.py (`<frames>/frames.json` + `<frames>/<name>.png`, 8-bit RGBA PNG from
 * Pillow), packs them with the v4 skyline packer of tools/art/rasterize.mjs (same `--pad` gutter, filled by edge
 * extrusion) into pages of at most `--max` px and writes
 *   Assets/MinikUsta/Resources/art3d/<name>_<N>.bytes   (PNG; Unity TextAsset "art3d/<name>_<N>" -> ImageConversion.LoadImage;
 *                                                         flat Resources name, no second dot: CI.md / P3-13)
 *   Assets/MinikUsta/Resources/art3d/<name>.json        (frames + clips + aliases; format in the README)
 * and fails when the pages exceed the RGBA32 memory budget (`--budget-mb`, default 16).
 *
 * No dependency: node:zlib for PNG, the packer and PNG optimiser of the v4 tools.
 * Usage: node tools/art/pack3d.mjs [--frames art/3d/out/king/frames] [--name king] [--out <dir>] [--pad 2] [--max 2048]
 *                                  [--budget-mb 16] [--png <dir>]   (--png: plain <name>-N.png copies for review)
 */
import { existsSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { deflateSync, inflateSync, crc32 } from 'node:zlib';
import { optimizePng } from './png.mjs';
import { pack } from './rasterize.mjs';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const args = process.argv.slice(2);
const opt = (name, def) => {
  const i = args.indexOf('--' + name);
  return i >= 0 && i + 1 < args.length ? args[i + 1] : def;
};
const NAME = opt('name', 'king');
const FRAMES = resolve(opt('frames', join(ROOT, 'art', '3d', 'out', NAME, 'frames')));
const OUT = resolve(opt('out', join(ROOT, 'Assets', 'MinikUsta', 'Resources', 'art3d')));
const PAD = Number(opt('pad', '2'));
const MAX = Number(opt('max', '2048'));
const BUDGET_MB = Number(opt('budget-mb', '16'));
const PNG_DIR = opt('png', '');
const PPU = 100;

/** Clip sets: nominal design canvas (frames.json v5) and raster px per design px at the store size. */
function setInfo(set, pxPerBU) {
  if (set === 'stage') return { canvas: [400, 480], pivot: [0.5, 0.05], scale: round6(pxPerBU / (480 / 3.6)) };
  return { canvas: [320, 320], pivot: [0.5, 0.0625], scale: round6(pxPerBU / (320 / 3.0)) };
}
const round6 = (v) => Math.round(v * 1e6) / 1e6;

const SIG = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
const paeth = (a, b, c) => {
  const p = a + b - c;
  const pa = Math.abs(p - a);
  const pb = Math.abs(p - b);
  const pc = Math.abs(p - c);
  return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
};

/** Decodes an 8-bit non-interlaced RGBA / RGB / grey+alpha PNG into RGBA pixels. */
export function decodePng(buf) {
  if (!buf.subarray(0, 8).equals(SIG)) throw new Error('not a PNG');
  let o = 8;
  let ih = null;
  const idat = [];
  while (o < buf.length) {
    const len = buf.readUInt32BE(o);
    const type = buf.toString('latin1', o + 4, o + 8);
    const data = buf.subarray(o + 8, o + 8 + len);
    if (type === 'IHDR') ih = data;
    else if (type === 'IDAT') idat.push(data);
    o += 12 + len;
  }
  const w = ih.readUInt32BE(0);
  const h = ih.readUInt32BE(4);
  const depth = ih[8];
  const ctype = ih[9];
  if (depth !== 8 || ih[12] !== 0) throw new Error(`unsupported PNG (depth ${depth}, interlace ${ih[12]})`);
  const bpp = { 6: 4, 2: 3, 4: 2, 0: 1 }[ctype];
  if (!bpp) throw new Error(`unsupported PNG colour type ${ctype}`);
  const stride = w * bpp;
  const raw = inflateSync(Buffer.concat(idat));
  const px = Buffer.alloc(stride * h);
  for (let y = 0; y < h; y++) {
    const f = raw[y * (stride + 1)];
    const src = y * (stride + 1) + 1;
    const dst = y * stride;
    for (let x = 0; x < stride; x++) {
      const a = x >= bpp ? px[dst + x - bpp] : 0;
      const b = y > 0 ? px[dst - stride + x] : 0;
      const c = x >= bpp && y > 0 ? px[dst - stride + x - bpp] : 0;
      const v = raw[src + x];
      px[dst + x] = (f === 0 ? v : f === 1 ? v + a : f === 2 ? v + b : f === 3 ? v + ((a + b) >> 1) : v + paeth(a, b, c)) & 255;
    }
  }
  if (bpp === 4) return { w, h, data: px };
  const out = Buffer.alloc(w * h * 4);
  for (let i = 0; i < w * h; i++) {
    if (bpp === 3) {
      out[i * 4] = px[i * 3];
      out[i * 4 + 1] = px[i * 3 + 1];
      out[i * 4 + 2] = px[i * 3 + 2];
      out[i * 4 + 3] = 255;
    } else if (bpp === 2) {
      out[i * 4] = out[i * 4 + 1] = out[i * 4 + 2] = px[i * 2];
      out[i * 4 + 3] = px[i * 2 + 1];
    } else {
      out[i * 4] = out[i * 4 + 1] = out[i * 4 + 2] = px[i];
      out[i * 4 + 3] = 255;
    }
  }
  return { w, h, data: out };
}

function chunk(type, data) {
  const head = Buffer.alloc(8);
  head.writeUInt32BE(data.length, 0);
  head.write(type, 4, 'latin1');
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(Buffer.concat([head.subarray(4), data])) >>> 0, 0);
  return Buffer.concat([head, data, crc]);
}

/** RGBA pixels -> PNG (filter 0, then the v4 optimiser re-filters adaptively and deflates at level 9). */
export function encodePng(w, h, data) {
  const ih = Buffer.alloc(13);
  ih.writeUInt32BE(w, 0);
  ih.writeUInt32BE(h, 4);
  ih[8] = 8;
  ih[9] = 6;
  const raw = Buffer.alloc((w * 4 + 1) * h);
  for (let y = 0; y < h; y++) data.copy(raw, y * (w * 4 + 1) + 1, y * w * 4, (y + 1) * w * 4);
  const png = Buffer.concat([SIG, chunk('IHDR', ih), chunk('IDAT', deflateSync(raw, { level: 6 })), chunk('IEND', Buffer.alloc(0))]);
  return optimizePng(png);
}

/** Copies frame pixels into the page and extrudes its edge into the `pad` gutter (no bleeding between frames). */
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

/**
 * MaxRects packer (best short side fit; free-rectangle split + prune), several sort orders, pages up to max x max;
 * the last page shrinks to its used area. Returns the arrangement with the smallest total page area (character
 * sheets are many similar rectangles; the v4 skyline packer leaves ~25 % empty here). Deterministic.
 */
export function packMaxRects(frames, max, pad) {
  const orders = [
    (a, b) => b.h * b.w - a.h * a.w,
    (a, b) => b.h - a.h || b.w - a.w,
    (a, b) => b.w - a.w || b.h - a.h,
    (a, b) => Math.max(b.w, b.h) - Math.max(a.w, a.h) || b.h - a.h,
    (a, b) => b.w + b.h - (a.w + a.h),
  ];
  let best = null;
  for (const [oi, cmp] of orders.entries()) {
    for (const pageW of [max, max - 64, max - 128, max - 256]) {
      const items = [...frames].sort((a, b) => cmp(a, b) || (a.name < b.name ? -1 : 1));
      const placed = new Map();
      const pages = [];
      let rest = items;
      while (rest.length) {
        const free = [{ x: 0, y: 0, w: pageW, h: max }];
        const left = [];
        let usedW = 0;
        let usedH = 0;
        for (const f of rest) {
          const fw = f.w + 2 * pad;
          const fh = f.h + 2 * pad;
          let bi = -1;
          let bs = Infinity;
          let bl = Infinity;
          for (let i = 0; i < free.length; i++) {
            const r = free[i];
            if (fw <= r.w && fh <= r.h) {
              const ss = Math.min(r.w - fw, r.h - fh);
              const ls = Math.max(r.w - fw, r.h - fh);
              // prefer low y first (keeps the page short), then the short-side fit
              const score = r.y * 4 + ss;
              if (score < bs || (score === bs && ls < bl)) {
                bs = score;
                bl = ls;
                bi = i;
              }
            }
          }
          if (bi < 0) {
            left.push(f);
            continue;
          }
          const node = { x: free[bi].x, y: free[bi].y, w: fw, h: fh };
          placed.set(f.name, { atlas: pages.length, x: node.x + pad, y: node.y + pad });
          usedW = Math.max(usedW, node.x + fw);
          usedH = Math.max(usedH, node.y + fh);
          // split every free rect that intersects the node
          const next = [];
          for (const r of free) {
            if (node.x >= r.x + r.w || node.x + node.w <= r.x || node.y >= r.y + r.h || node.y + node.h <= r.y) {
              next.push(r);
              continue;
            }
            if (node.x > r.x) next.push({ x: r.x, y: r.y, w: node.x - r.x, h: r.h });
            if (node.x + node.w < r.x + r.w) next.push({ x: node.x + node.w, y: r.y, w: r.x + r.w - node.x - node.w, h: r.h });
            if (node.y > r.y) next.push({ x: r.x, y: r.y, w: r.w, h: node.y - r.y });
            if (node.y + node.h < r.y + r.h) next.push({ x: r.x, y: node.y + node.h, w: r.w, h: r.y + r.h - node.y - node.h });
          }
          free.length = 0;
          for (let i = 0; i < next.length; i++) {
            let contained = false;
            for (let j = 0; j < next.length && !contained; j++) {
              if (i === j) continue;
              const a = next[i];
              const b = next[j];
              if (a.x >= b.x && a.y >= b.y && a.x + a.w <= b.x + b.w && a.y + a.h <= b.y + b.h && (i > j || a.w !== b.w || a.h !== b.h || a.x !== b.x || a.y !== b.y)) contained = true;
            }
            if (!contained) free.push(next[i]);
          }
        }
        if (left.length === rest.length) throw new Error(`${rest[0].name}: does not fit a ${max} page`);
        pages.push({ index: pages.length, w: Math.ceil(usedW / 4) * 4, h: Math.ceil(usedH / 4) * 4 });
        rest = left;
      }
      const area = pages.reduce((s, p) => s + p.w * p.h, 0);
      if (!best || area < best.area) best = { area, pages, placed, order: oi, pageW };
    }
  }
  for (const f of frames) Object.assign(f, best.placed.get(f.name));
  return best.pages;
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const t0 = Date.now();
  const doc = JSON.parse(readFileSync(join(FRAMES, 'frames.json'), 'utf8'));
  const names = Object.keys(doc.frames).sort();
  const frames = names.map((name) => {
    const m = doc.frames[name];
    const img = decodePng(readFileSync(join(FRAMES, name + '.png')));
    if (img.w !== m.w || img.h !== m.h) throw new Error(`${name}: PNG ${img.w}x${img.h} != frames.json ${m.w}x${m.h}`);
    return { name, w: m.w, h: m.h, img, meta: m };
  });
  const sky = pack(frames.map((f) => ({ ...f })), MAX, PAD);
  const pages = packMaxRects(frames, MAX, PAD);
  const skyArea = sky.reduce((t, p) => t + p.w * p.h, 0);
  const mrArea = pages.reduce((t, p) => t + p.w * p.h, 0);
  if (skyArea < mrArea) {
    pages.length = 0;
    pack(frames, MAX, PAD).forEach((p) => pages.push(p));
  }
  const bytes = pages.reduce((s, p) => s + p.w * p.h * 4, 0);
  const budget = BUDGET_MB * 1048576;
  const buffers = pages.map((p) => Buffer.alloc(p.w * p.h * 4));
  for (const f of frames) blit(buffers[f.atlas], pages[f.atlas].w, f, f.img, PAD);
  // the pad gutter of the page edge may fall outside the rounded page: pack() reserves pad on every side, so it fits
  const pxPerBU = doc.pxPerBU;
  const stage = setInfo('stage', pxPerBU);
  const corner = setInfo('corner', pxPerBU);
  // bust box: 256 x 320 design = 1.28 x 1.6 BU (animate_king.BUST_*), i.e. 200 design px per BU; the crew sheet
  // (art/3d/blender/crew, art turn 1) writes its own bustDesignPxPerBU (256: a 1.0 x 1.25 BU box)
  const isBustName = (n) => /_bust(_|$)/.test(n);
  const bustPx = doc.frames[names.find(isBustName)]?.pxPerBU ?? 150;
  const bustScale = round6(bustPx / (doc.bustDesignPxPerBU ?? 200));
  const out = {};
  for (const f of [...frames].sort((a, b) => (a.name < b.name ? -1 : 1))) {
    const isBust = isBustName(f.name);
    out[f.name] = {
      atlas: f.atlas,
      x: f.x,
      y: f.y,
      w: f.w,
      h: f.h,
      pivotX: f.meta.pivotX,
      pivotY: f.meta.pivotY,
      borderL: 0,
      borderT: 0,
      borderR: 0,
      borderB: 0,
      scale: isBust ? bustScale : corner.scale,
      ox: f.meta.ox,
      oy: f.meta.oy,
    };
  }
  const clips = {};
  for (const [cn, c] of Object.entries(doc.clips).sort()) {
    const si = setInfo(c.set, pxPerBU);
    clips[cn] = {
      frames: c.frames,
      fps: c.fps,
      loop: c.loop,
      out: c.out ?? null,
      events: c.events,
      set: c.set,
      scale: si.scale,
      canvas: si.canvas,
      pivot: si.pivot,
      ...(c.aliasOf ? { aliasOf: c.aliasOf } : {}),
    };
  }
  // aliases: key-pose stills (stage scale) and the deprecated chr_dede_* names (ART_3D: rendered as Usta Kral)
  const aliases = {};
  if (doc.aliases) {
    // crew sheet: v4 atlas names -> drawing + the design px per BU it is shown at (scale = stored px per design px)
    for (const [a, v] of Object.entries(doc.aliases).sort()) {
      aliases[a] = { of: v.of, scale: round6((isBustName(v.of) ? bustPx : pxPerBU) / v.designPxPerBU) };
    }
  } else {
    for (const [pose, [clip, frame]] of Object.entries(doc.stills ?? {}).sort()) {
      aliases[`chr_king_still_${pose}`] = { of: doc.clips[clip].frames[frame], scale: stage.scale };
    }
    aliases.chr_dede_idle = { of: aliases.chr_king_still_idle.of, scale: stage.scale, deprecated: true };
    aliases.chr_dede_talk = { of: aliases.chr_king_still_talk.of, scale: stage.scale, deprecated: true };
    aliases.chr_dede_bust = { of: 'chr_king_bust_neutral', scale: bustScale, deprecated: true };
  }
  for (const [a, v] of Object.entries(aliases)) {
    if (!out[v.of]) throw new Error(`alias ${a} -> missing frame ${v.of}`);
  }
  for (const [cn, c] of Object.entries(clips)) {
    for (const n of [...c.frames, ...(c.out ?? [])]) if (!out[n]) throw new Error(`clip ${cn} -> missing frame ${n}`);
  }
  const json = {
    version: 1,
    doc: NAME === 'crew' ? 'art/3d/blender/crew/README.md' : 'art/3d/blender/king/README.md',
    generator: 'tools/art/pack3d.mjs',
    ppu: PPU,
    pad: PAD,
    origin: 'top-left',
    pivotOrigin: 'bottom-left',
    units:
      'x y w h = atlas px (top-left origin; Unity Rect(x, H - y - h, w, h)); pivot = root point (feet on the ground; bust: canvas centre), 0..1 from the bottom-left of the rect; ' +
      'scale = raster px per design px, sprite ppu = ppu * scale (use the CLIP scale when playing a clip, the alias scale for stills); ox oy = rect offset from the bottom-left of the full render canvas (store px)',
    pxPerBU,
    renderCanvas: { w: Math.round(doc.canvas.wBU * pxPerBU), h: Math.round(doc.canvas.hBU * pxPerBU), rootX: Math.round((doc.canvas.wBU * pxPerBU) / 2), rootY: Math.round(doc.canvas.rootYBU * pxPerBU) },
    memoryBytesRGBA32: bytes,
    atlases: pages.map((p) => ({ file: `art3d/${NAME}_${p.index}`, w: p.w, h: p.h })),
    frames: out,
    clips,
    aliases,
  };
  if (bytes > budget) {
    console.error(`pack3d: ${pages.map((p) => p.w + 'x' + p.h).join(', ')} = ${(bytes / 1048576).toFixed(2)} MB > budget ${BUDGET_MB} MB`);
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
  const text = JSON.stringify(json, null, 1);
  writeFileSync(join(OUT, `${NAME}.json`), text + '\n');
  const area = frames.reduce((s, f) => s + (f.w + 2 * PAD) * (f.h + 2 * PAD), 0);
  const pageArea = pages.reduce((s, p) => s + p.w * p.h, 0);
  console.log(
    `pack3d ${NAME}: ${frames.length} frames, ${Object.keys(clips).length} clips -> ${pages.length} page(s) ${pages.map((p) => p.w + 'x' + p.h).join(', ')}; ` +
      `fill ${((100 * area) / pageArea).toFixed(1)} %; RGBA32 ${(bytes / 1048576).toFixed(2)} MB (budget ${BUDGET_MB}); PNG ${(total / 1048576).toFixed(2)} MB; ` +
      `${((Date.now() - t0) / 1000).toFixed(1)} s`,
  );
  if (!existsSync(OUT)) process.exit(1);
}
