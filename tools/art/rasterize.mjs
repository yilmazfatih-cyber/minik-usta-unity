#!/usr/bin/env node
/**
 * v4 art pack rasteriser (design-lead, package V0; docs/ART_UNITY.md). Reads every `art/svg/**\/*.svg`, rasterises it in
 * the preinstalled Chromium (Playwright from the web repo's node_modules — no new dependency), packs the frames into
 * ≤ 2048 × 2048 atlas pages (skyline packer, bottom-left heuristic, `--pad` px gutter filled by edge extrusion) and writes
 *   Assets/MinikUsta/Resources/art/atlas-<N>.png.bytes   (PNG; Unity loads it as a TextAsset → Texture2D.LoadImage)
 *   Assets/MinikUsta/Resources/art/atlas.json            (frame contract, see docs/ART_UNITY.md §2)
 *
 * Frame metadata comes from the SVG root: width/height = DESIGN px (1080-wide canvas); data-scale (raster px per design
 * px, default 1.5); data-pivot "x y" (Unity sprite pivot, 0..1 from bottom-left, default 0.5 0.5); data-border "L T R B"
 * (9-slice, design px → stored in atlas px); data-anchor "x y" (Unity 0..1, block stud centre); data-stud / data-sym
 * (design px). SVGs with <text> are drawn through the DOM (document fonts: Baloo 2 from the web repo, OFL 1.1).
 *
 * Usage: node tools/art/rasterize.mjs [--web ../minik-usta] [--pad 2] [--max 2048] [--out <dir>] [--png <dir>]
 *   --png <dir>  also writes plain atlas-N.png copies (for review; artifacts/ is git-ignored)
 */
import { createRequire } from 'node:module';
import { existsSync, mkdirSync, readFileSync, readdirSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { basename, dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { optimizePng } from './png.mjs';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const args = process.argv.slice(2);
const opt = (name, def) => {
  const i = args.indexOf('--' + name);
  return i >= 0 && i + 1 < args.length ? args[i + 1] : def;
};
const WEB = resolve(opt('web', join(ROOT, '..', 'minik-usta')));
const PAD = Number(opt('pad', '2'));
const MAX = Number(opt('max', '2048'));
const OUT = resolve(opt('out', join(ROOT, 'Assets', 'MinikUsta', 'Resources', 'art')));
const PNG_DIR = opt('png', '');
const SVG_DIR = join(ROOT, 'art', 'svg');
const CHROMIUM = process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE ?? '/opt/pw-browsers/chromium';
const DEFAULT_SCALE = 1.5;
const PPU = 100;

function walk(d, out = []) {
  for (const n of readdirSync(d).sort()) {
    const p = join(d, n);
    if (statSync(p).isDirectory()) walk(p, out);
    else if (n.endsWith('.svg')) out.push(p);
  }
  return out;
}
const attr = (svg, name) => {
  const m = svg.match(new RegExp(`<svg\\b[^>]*\\s${name}="([^"]*)"`));
  return m ? m[1] : undefined;
};
const nums = (s, n, def) => (s === undefined ? def : s.trim().split(/\s+/).map(Number).slice(0, n));

export function readFrames(dir = SVG_DIR) {
  const frames = [];
  for (const path of walk(dir)) {
    const svg = readFileSync(path, 'utf8');
    const name = basename(path, '.svg');
    if (!/^[a-z0-9_]+$/.test(name)) throw new Error(`${path}: frame names are lower case + underscore`);
    if (/<(filter|mask|image)\b/.test(svg)) throw new Error(`${path}: filter / mask / image are not allowed (ART_REFERENCE §6.2)`);
    const w = Number(attr(svg, 'width'));
    const h = Number(attr(svg, 'height'));
    if (!(w > 0 && h > 0)) throw new Error(`${path}: width / height (design px) missing`);
    const scale = Number(attr(svg, 'data-scale') ?? DEFAULT_SCALE);
    const [px, py] = nums(attr(svg, 'data-pivot'), 2, [0.5, 0.5]);
    const border = nums(attr(svg, 'data-border'), 4, [0, 0, 0, 0]).map((v) => Math.round(v * scale));
    const anchor = nums(attr(svg, 'data-anchor'), 2, undefined);
    const stud = attr(svg, 'data-stud');
    const sym = attr(svg, 'data-sym');
    frames.push({
      name,
      path: relative(ROOT, path).replace(/\\/g, '/'),
      svg,
      text: /<text\b/.test(svg),
      dw: w,
      dh: h,
      scale,
      w: Math.max(1, Math.round(w * scale)),
      h: Math.max(1, Math.round(h * scale)),
      pivotX: px,
      pivotY: py,
      border,
      anchor,
      stud: stud === undefined ? undefined : Number(stud),
      sym: sym === undefined ? undefined : Number(sym),
    });
  }
  const seen = new Set();
  for (const f of frames) {
    if (seen.has(f.name)) throw new Error('duplicate frame name ' + f.name);
    seen.add(f.name);
  }
  return frames;
}

/**
 * Skyline packer (bottom-left heuristic): frames sorted by height then width (desc); each frame goes where its top
 * edge ends lowest on the first page that fits; a new page opens when none does. Deterministic.
 */
export function pack(frames, max = MAX, pad = PAD) {
  const order = [...frames].sort((a, b) => b.h - a.h || b.w - a.w || (a.name < b.name ? -1 : 1));
  const pages = [];
  const newPage = () => {
    const pg = { sky: [{ x: 0, y: 0, w: max }], usedW: 0, usedH: 0 };
    pages.push(pg);
    return pg;
  };
  const fitAt = (sky, i, w, h) => {
    const x = sky[i].x;
    if (x + w > max) return -1;
    let y = 0;
    let left = w;
    for (let j = i; j < sky.length && left > 0; j++) {
      y = Math.max(y, sky[j].y);
      if (y + h > max) return -1;
      left -= sky[j].w;
    }
    return left > 0 ? -1 : y;
  };
  const place = (pg, i, y, w, h) => {
    const x = pg.sky[i].x;
    const seg = { x, y: y + h, w };
    const out = [];
    for (const s of pg.sky) {
      const e = s.x + s.w;
      if (e <= x || s.x >= x + w) out.push(s);
      else {
        if (s.x < x) out.push({ x: s.x, y: s.y, w: x - s.x });
        if (e > x + w) out.push({ x: x + w, y: s.y, w: e - x - w });
      }
    }
    out.push(seg);
    out.sort((a, b) => a.x - b.x);
    const merged = [];
    for (const s of out) {
      const m = merged[merged.length - 1];
      if (m && m.y === s.y && m.x + m.w === s.x) m.w += s.w;
      else merged.push({ ...s });
    }
    pg.sky = merged;
    pg.usedW = Math.max(pg.usedW, x + w);
    pg.usedH = Math.max(pg.usedH, y + h);
    return x;
  };
  for (const f of order) {
    const fw = f.w + 2 * pad;
    const fh = f.h + 2 * pad;
    if (fw > max || fh > max) throw new Error(`${f.name}: ${f.w}×${f.h} does not fit a ${max} page`);
    let done = false;
    for (let pi = 0; pi <= pages.length && !done; pi++) {
      const pg = pi < pages.length ? pages[pi] : newPage();
      let best = null;
      for (let i = 0; i < pg.sky.length; i++) {
        const y = fitAt(pg.sky, i, fw, fh);
        if (y < 0) continue;
        if (!best || y + fh < best.top || (y + fh === best.top && pg.sky[i].x < best.x)) best = { i, y, top: y + fh, x: pg.sky[i].x };
      }
      if (!best) continue;
      const x = place(pg, best.i, best.y, fw, fh);
      f.atlas = pi;
      f.x = x + pad;
      f.y = best.y + pad;
      done = true;
    }
  }
  return pages.map((pg, i) => ({ index: i, w: Math.ceil(pg.usedW / 4) * 4, h: Math.ceil(pg.usedH / 4) * 4 }));
}

async function loadChromium() {
  const require = createRequire(join(WEB, 'package.json'));
  let pw;
  try {
    pw = require('playwright');
  } catch {
    pw = require('@playwright/test');
  }
  return pw.chromium.launch({ executablePath: CHROMIUM, headless: true });
}

const sized = (svg, w, h) =>
  svg.replace(/<svg\b([^>]*)>/, (m, a) => '<svg' + a.replace(/\swidth="[^"]*"/, ` width="${w}"`).replace(/\sheight="[^"]*"/, ` height="${h}"`) + '>');

export async function rasterise(frames, pages, { pngDir } = {}) {
  const fontPath = join(WEB, 'public', 'fonts', 'baloo2-latin-tr.woff2');
  const font = existsSync(fontPath) ? readFileSync(fontPath).toString('base64') : '';
  const browser = await loadChromium();
  const page = await browser.newPage({ viewport: { width: 2100, height: 2100 }, deviceScaleFactor: 1 });
  await page.setContent('<!doctype html><html><head><style>html,body{margin:0;background:transparent}</style></head><body><div id="dom" style="position:absolute;left:0;top:0"></div></body></html>');
  await page.evaluate(async (b64) => {
    if (!b64) return;
    const bin = Uint8Array.from(atob(b64), (c) => c.charCodeAt(0));
    for (const weight of ['400', '600', '700', '800']) {
      const ff = new FontFace('Baloo 2', bin, { weight });
      await ff.load();
      document.fonts.add(ff);
    }
  }, font);
  await page.evaluate((list) => {
    window.__pages = list.map((p) => {
      const c = document.createElement('canvas');
      c.width = p.w;
      c.height = p.h;
      return c;
    });
  }, pages);
  for (const f of frames) {
    const svg = sized(f.svg, f.w, f.h);
    let pngB64 = null;
    if (f.text) {
      await page.evaluate((s) => {
        document.getElementById('dom').innerHTML = s;
      }, svg);
      const el = await page.$('#dom svg');
      pngB64 = (await el.screenshot({ omitBackground: true })).toString('base64');
    }
    await page.evaluate(
      async ({ f, svg, pngB64, pad }) => {
        const img = new Image();
        img.src = pngB64 ? 'data:image/png;base64,' + pngB64 : 'data:image/svg+xml;base64,' + btoa(unescape(encodeURIComponent(svg)));
        await img.decode();
        const c = window.__pages[f.atlas];
        const g = c.getContext('2d');
        g.clearRect(f.x - pad, f.y - pad, f.w + 2 * pad, f.h + 2 * pad);
        g.drawImage(img, f.x, f.y, f.w, f.h);
        if (pad > 0) {
          // edge extrusion into the gutter (bilinear sampling never pulls in a neighbour frame)
          const d = g.getImageData(f.x - pad, f.y - pad, f.w + 2 * pad, f.h + 2 * pad);
          const W = f.w + 2 * pad;
          const H = f.h + 2 * pad;
          const px = d.data;
          const cp = (sx, sy, tx, ty) => {
            const a = (sy * W + sx) * 4;
            const b = (ty * W + tx) * 4;
            px[b] = px[a];
            px[b + 1] = px[a + 1];
            px[b + 2] = px[a + 2];
            px[b + 3] = px[a + 3];
          };
          for (let y = 0; y < H; y++) {
            const sy = Math.min(H - pad - 1, Math.max(pad, y));
            for (let x = 0; x < W; x++) {
              const sx = Math.min(W - pad - 1, Math.max(pad, x));
              if (sx !== x || sy !== y) cp(sx, sy, x, y);
            }
          }
          g.putImageData(d, f.x - pad, f.y - pad);
        }
      },
      { f: { atlas: f.atlas, x: f.x, y: f.y, w: f.w, h: f.h }, svg, pngB64: pngB64, pad: PAD },
    );
  }
  const out = [];
  for (const p of pages) {
    const b64 = await page.evaluate((i) => window.__pages[i].toDataURL('image/png').split(',')[1], p.index);
    out.push(optimizePng(Buffer.from(b64, 'base64')));
  }
  await browser.close();
  return out;
}

export function atlasJson(frames, pages) {
  const sorted = [...frames].sort((a, b) => (a.name < b.name ? -1 : a.name > b.name ? 1 : 0));
  const fr = {};
  for (const f of sorted) {
    const o = {
      atlas: f.atlas,
      x: f.x,
      y: f.y,
      w: f.w,
      h: f.h,
      pivotX: f.pivotX,
      pivotY: f.pivotY,
      borderL: f.border[0],
      borderT: f.border[1],
      borderR: f.border[2],
      borderB: f.border[3],
      scale: f.scale,
    };
    if (f.anchor) {
      o.anchorX = f.anchor[0];
      o.anchorY = f.anchor[1];
    }
    if (f.stud !== undefined) o.studD = f.stud;
    if (f.sym !== undefined) o.symD = f.sym;
    fr[f.name] = o;
  }
  return {
    version: 4,
    doc: 'docs/ART_UNITY.md',
    generator: 'tools/art/rasterize.mjs',
    ppu: PPU,
    scale: DEFAULT_SCALE,
    pad: PAD,
    origin: 'top-left',
    pivotOrigin: 'bottom-left',
    units: 'x y w h border* = atlas px; pivot* anchor* = 0..1 from bottom-left; studD symD = design px; sprite ppu = ppu * scale',
    atlases: pages.map((p) => ({ file: `art/atlas-${p.index}.png`, w: p.w, h: p.h })),
    frames: fr,
  };
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const t0 = Date.now();
  const frames = readFrames();
  const pages = pack(frames);
  const pngs = await rasterise(frames, pages);
  rmSync(OUT, { recursive: true, force: true });
  mkdirSync(OUT, { recursive: true });
  let total = 0;
  pngs.forEach((buf, i) => {
    writeFileSync(join(OUT, `atlas-${i}.png.bytes`), buf);
    total += buf.length;
    if (PNG_DIR) {
      mkdirSync(PNG_DIR, { recursive: true });
      writeFileSync(join(PNG_DIR, `atlas-${i}.png`), buf);
    }
  });
  const json = JSON.stringify(atlasJson(frames, pages), null, 1);
  writeFileSync(join(OUT, 'atlas.json'), json + '\n');
  const area = frames.reduce((s, f) => s + (f.w + 2 * PAD) * (f.h + 2 * PAD), 0);
  const pageArea = pages.reduce((s, p) => s + p.w * p.h, 0);
  console.log(
    `atlas: ${frames.length} frames → ${pages.length} page(s) ${pages.map((p) => p.w + '×' + p.h).join(', ')}; fill ${((100 * area) / pageArea).toFixed(1)} %; ` +
      `PNG ${(total / 1048576).toFixed(2)} MB; json ${(json.length / 1024).toFixed(0)} KB; ${((Date.now() - t0) / 1000).toFixed(1)} s`,
  );
}
