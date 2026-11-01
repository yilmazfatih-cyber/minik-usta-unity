#!/usr/bin/env node
/**
 * Unity screen composites from Presentation.Core scenes (paket V1, code-lead). NOT a Unity screenshot: the scenes are dumped
 * by the NUnit test `ScreenDumpTests` (Presentation.Core layout: board items with atlas frames, block parts, the K-18 shadow,
 * the HUD / window / home / settings UI trees with their texts and styles) and this tool only draws them — from the shipped
 * atlas pages (Assets/MinikUsta/Resources/art/atlas.json + atlas_N.bytes) and the shipped Baloo 2 ExtraBold TTF
 * (Resources/fonts) — the way the Unity layer would: frame drawn into its rectangle (9-slice for sliced frames, tint × alpha),
 * text with fill + outline + drop shadow, shrunk to fit its rectangle (uGUI best fit). Phone sizes are FIT letterboxes of
 * the 1080-wide design canvas (CanvasScaler Shrink), the camera colour around it.
 *
 * Usage:
 *   cd dotnet && MINIK_PREVIEW_DIR=../artifacts/preview dotnet test --filter ScreenDumpTests
 *   node tools/art/compose.mjs [--web ../minik-usta] [--in artifacts/preview/scenes] [--out artifacts/preview]
 * Output: <out>/v4-unity-<scene>.png (1080 × 1920), -360x800.png, -390x844.png.
 * Needs Playwright from the web repo's node_modules and Chromium at /opt/pw-browsers/chromium (no new dependency).
 */
import { createRequire } from 'node:module';
import { mkdirSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { optimizePng } from './png.mjs';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const args = process.argv.slice(2);
const opt = (n, d) => {
  const i = args.indexOf('--' + n);
  return i >= 0 && i + 1 < args.length ? args[i + 1] : d;
};
const WEB = resolve(opt('web', join(ROOT, '..', 'minik-usta')));
const IN = resolve(opt('in', join(ROOT, 'artifacts', 'preview', 'scenes')));
const OUT = resolve(opt('out', join(ROOT, 'artifacts', 'preview')));
const RES = join(ROOT, 'Assets', 'MinikUsta', 'Resources');
const SIZES = [
  [360, 800],
  [390, 844],
];

const atlas = JSON.parse(readFileSync(join(RES, 'art', 'atlas.json'), 'utf8'));
const pages = atlas.atlases.map((a) => readFileSync(join(RES, a.file + '.bytes')).toString('base64'));
const font = readFileSync(join(RES, 'fonts', 'Baloo2-ExtraBold.ttf')).toString('base64');
const scenes = readdirSync(IN)
  .filter((f) => f.endsWith('.json'))
  .sort()
  .map((f) => JSON.parse(readFileSync(join(IN, f), 'utf8')));

const require = createRequire(join(WEB, 'package.json'));
let pw;
try {
  pw = require('playwright');
} catch {
  pw = require('@playwright/test');
}

const KIT = String.raw`
const A = window.__atlas; const IMG = window.__img;
function fr(n) { const f = A.frames[n]; if (!f) throw new Error('missing frame ' + n); return f; }
function rgba(hex) { const r = parseInt(hex.slice(1, 3), 16), g = parseInt(hex.slice(3, 5), 16), b = parseInt(hex.slice(5, 7), 16);
  const a = hex.length > 7 ? parseInt(hex.slice(7, 9), 16) / 255 : 1; return [r, g, b, a]; }
const TC = {};
/** the frame tinted (multiply, alpha kept): Unity's SpriteRenderer / Image colour */
function src(n, tint) {
  const f = fr(n);
  if (!tint || tint.slice(0, 7).toUpperCase() === '#FFFFFF') return [IMG[f.atlas], f.x, f.y];
  const key = n + tint.slice(0, 7); if (!TC[key]) {
    const c = document.createElement('canvas'); c.width = f.w; c.height = f.h; const x = c.getContext('2d');
    x.drawImage(IMG[f.atlas], f.x, f.y, f.w, f.h, 0, 0, f.w, f.h);
    x.globalCompositeOperation = 'multiply'; x.fillStyle = tint.slice(0, 7); x.fillRect(0, 0, f.w, f.h);
    x.globalCompositeOperation = 'destination-in'; x.drawImage(IMG[f.atlas], f.x, f.y, f.w, f.h, 0, 0, f.w, f.h);
    TC[key] = c; }
  return [TC[key], 0, 0];
}
/** draw frame n INTO rect (x, y, w, h); o: tint '#RRGGBBAA', alpha, sliced, flipY, rot (deg ccw), aspect (preserveAspect) */
function draw(g, n, x, y, w, h, o = {}) {
  const f = fr(n); const [im, sx, sy] = src(n, o.tint);
  const ta = o.tint ? rgba(o.tint)[3] : 1;
  g.save(); g.globalAlpha = (o.alpha ?? 1) * ta;
  g.translate(x + w / 2, y + h / 2); if (o.rot) g.rotate(-o.rot * Math.PI / 180); if (o.flipY) g.scale(1, -1);
  if (o.aspect) { const k = Math.min(w / (f.w / f.scale), h / (f.h / f.scale)); const ww = f.w / f.scale * k, hh = f.h / f.scale * k; w = ww; h = hh; }
  const x0 = -w / 2, y0 = -h / 2;
  const hasBorder = f.borderL + f.borderT + f.borderR + f.borderB > 0;
  if (o.sliced && hasBorder) {
    const s = f.scale; let L = f.borderL / s, T = f.borderT / s, R = f.borderR / s, B = f.borderB / s;
    const kx = Math.min(1, w / Math.max(1e-6, L + R)), ky = Math.min(1, h / Math.max(1e-6, T + B)); L *= kx; R *= kx; T *= ky; B *= ky; // uGUI shrinks borders that do not fit
    const SX = [sx, sx + f.borderL, sx + f.w - f.borderR, sx + f.w], SY = [sy, sy + f.borderT, sy + f.h - f.borderB, sy + f.h];
    const DX = [x0, x0 + L, x0 + w - R, x0 + w], DY = [y0, y0 + T, y0 + h - B, y0 + h];
    for (let i = 0; i < 3; i++) for (let j = 0; j < 3; j++) {
      const sw = SX[i + 1] - SX[i], sh = SY[j + 1] - SY[j], dw = DX[i + 1] - DX[i], dh = DY[j + 1] - DY[j];
      if (sw > 0 && sh > 0 && dw > 0 && dh > 0) g.drawImage(im, SX[i], SY[j], sw, sh, DX[i], DY[j], dw + 0.35, dh + 0.35);
    }
  } else g.drawImage(im, sx, sy, f.w, f.h, x0, y0, w, h);
  g.restore();
}
const FF = "'Baloo2XB'";
/** uGUI Text: best fit (shrink to the rect width, ≥ half size), Shadow (drop) + Outline (stroke) + fill, middle aligned */
function text(g, t) {
  if (!t.text) return;
  let size = t.size; g.save(); g.font = size + 'px ' + FF;
  while (size > t.size / 2 && g.measureText(t.text).width > t.w) { size -= 1; g.font = size + 'px ' + FF; }
  g.textBaseline = 'middle'; g.textAlign = t.align === 'left' ? 'left' : t.align === 'right' ? 'right' : 'center'; g.lineJoin = 'round';
  const x = t.align === 'left' ? t.x : t.align === 'right' ? t.x + t.w : t.x + t.w / 2; const y = t.y + t.h / 2 + size * 0.06;
  const k = size / t.size; const ta = t.ta ?? 1;
  const col = (hex) => { const [r, gg, b, a] = rgba(hex); return 'rgba(' + r + ',' + gg + ',' + b + ',' + a * ta + ')'; };
  if (t.shadowPx > 0.5) { g.fillStyle = col(t.shadow); if (t.outlinePx > 0.5) { g.strokeStyle = col(t.shadow); g.lineWidth = 2 * t.outlinePx * k; g.strokeText(t.text, x, y + t.shadowPx * k); } g.fillText(t.text, x, y + t.shadowPx * k); }
  if (t.outlinePx > 0.5) { g.strokeStyle = col(t.outline); g.lineWidth = 2 * t.outlinePx * k; g.strokeText(t.text, x, y); }
  g.fillStyle = col(t.fill); g.fillText(t.text, x, y);
  g.restore();
}
function node(g, n) {
  if (n.hidden) return;
  if (n.f) draw(g, n.f, n.x, n.y, n.w, n.h, { tint: n.t, alpha: n.a, sliced: n.s, rot: n.r, aspect: n.pa });
  if (n.text !== undefined) text(g, n);
  for (const c of n.children ?? []) node(g, c);
}
function scene(sc) {
  const W = sc.canvas.w, H = sc.canvas.h;
  const cv = document.createElement('canvas'); cv.width = W; cv.height = H; const g = cv.getContext('2d'); g.imageSmoothingQuality = 'high';
  g.fillStyle = sc.clear; g.fillRect(0, 0, W, H);
  for (const s of sc.world) draw(g, s.f, s.x, s.y, s.w, s.h, { tint: s.t, alpha: s.a, sliced: s.s, flipY: s.fy });
  for (const l of sc.ui) node(g, l);
  return cv;
}
function fit(cv, clear, w, h) {
  const out = document.createElement('canvas'); out.width = w; out.height = h; const g = out.getContext('2d'); g.imageSmoothingQuality = 'high';
  g.fillStyle = clear; g.fillRect(0, 0, w, h);
  const k = Math.min(w / cv.width, h / cv.height); const dw = cv.width * k, dh = cv.height * k;
  g.drawImage(cv, (w - dw) / 2, (h - dh) / 2, dw, dh); return out;
}
`;

async function main() {
  const browser = await pw.chromium.launch({ executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE ?? '/opt/pw-browsers/chromium' });
  const page = await browser.newPage({ viewport: { width: 1200, height: 1000 } });
  await page.setContent('<!doctype html><html><body style="margin:0"></body></html>');
  await page.evaluate(
    async ({ atlas, pages, font }) => {
      window.__atlas = atlas;
      window.__img = await Promise.all(
        pages.map(async (b64) => {
          const im = new Image();
          im.src = 'data:image/png;base64,' + b64;
          await im.decode();
          return im;
        }),
      );
      const bin = Uint8Array.from(atob(font), (c) => c.charCodeAt(0));
      const ff = new FontFace('Baloo2XB', bin);
      await ff.load();
      document.fonts.add(ff);
    },
    { atlas, pages, font },
  );
  await page.addScriptTag({ content: KIT });
  mkdirSync(OUT, { recursive: true });
  for (const sc of scenes) {
    const shots = await page.evaluate(
      ({ sc, sizes }) => {
        const cv = scene(sc);
        const o = [['', cv.toDataURL('image/png').split(',')[1]]];
        for (const [w, h] of sizes) o.push(['-' + w + 'x' + h, fit(cv, sc.clear, w, h).toDataURL('image/png').split(',')[1]]);
        return o;
      },
      { sc, sizes: SIZES },
    );
    for (const [suffix, b64] of shots) {
      const file = join(OUT, `v4-unity-${sc.name}${suffix}.png`);
      writeFileSync(file, optimizePng(Buffer.from(b64, 'base64')));
      console.log('compose:', file);
    }
  }
  await browser.close();
}
await main();
