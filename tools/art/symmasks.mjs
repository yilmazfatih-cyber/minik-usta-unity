#!/usr/bin/env node
/**
 * Block symbol masks for the 3D renders (package B1, art/3d/blender/env/blocks.py). Takes the INK layer of each
 * art/svg/block/sym_<c>.svg (the second group: the v4 drop-shadow copy is dropped, the 3D emboss replaces it), paints ink
 * white and the base-colour cut lines (G leaf vein, P diamond facets) black, and rasterises it at 512 x 512 on black in
 * the preinstalled Chromium (Playwright from the web repo, no new dependency) -> art/3d/out/env/sym/sym_<c>.png.
 * The 100 x 100 symbol design box maps to the whole image (symD = 44 design px on the stud).
 *
 * Usage: node tools/art/symmasks.mjs [--web ../minik-usta] [--out art/3d/out/env/sym]
 */
import { createRequire } from 'node:module';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const args = process.argv.slice(2);
const opt = (n, d) => {
  const i = args.indexOf('--' + n);
  return i >= 0 && i + 1 < args.length ? args[i + 1] : d;
};
const WEB = resolve(opt('web', join(ROOT, '..', 'minik-usta')));
const OUT = resolve(opt('out', join(ROOT, 'art', '3d', 'out', 'env', 'sym')));
const BASE = { w: '#764012', y: '#FFE23E', g: '#44D088', r: '#CF404E', o: '#EC8D20', c: '#C8CFDA', b: '#2984DE', p: '#5D3AAE' };

export function inkSvg(code, svg) {
  const groups = [...svg.matchAll(/<g transform="translate\(7 5\) scale\(0\.5\)">([\s\S]*)<\/g>\s*<\/svg>/g)];
  if (!groups.length) throw new Error('sym_' + code + ': ink group not found');
  let body = groups[0][1];
  const base = BASE[code].toUpperCase();
  body = body.replace(/(fill|stroke)="(#[0-9A-Fa-f]{6})"/g, (m, attr, col) => `${attr}="${col.toUpperCase() === base ? '#000000' : '#FFFFFF'}"`);
  body = body.replace(/\sopacity="[^"]*"/g, '');
  return `<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 100 100"><rect x="-10" y="-10" width="120" height="120" fill="#000"/>${body}</svg>`;
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const require = createRequire(join(WEB, 'package.json'));
  let pw;
  try {
    pw = require('playwright');
  } catch {
    pw = require('@playwright/test');
  }
  const browser = await pw.chromium.launch({ executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE ?? '/opt/pw-browsers/chromium' });
  const page = await browser.newPage({ viewport: { width: 600, height: 600 } });
  mkdirSync(OUT, { recursive: true });
  for (const code of Object.keys(BASE)) {
    const svg = inkSvg(code, readFileSync(join(ROOT, 'art', 'svg', 'block', `sym_${code}.svg`), 'utf8'));
    const b64 = await page.evaluate(async (s) => {
      const img = new Image();
      img.src = 'data:image/svg+xml;base64,' + btoa(s);
      await img.decode();
      const c = document.createElement('canvas');
      c.width = 512;
      c.height = 512;
      c.getContext('2d').drawImage(img, 0, 0, 512, 512);
      return c.toDataURL('image/png').split(',')[1];
    }, svg);
    writeFileSync(join(OUT, `sym_${code}.png`), Buffer.from(b64, 'base64'));
    console.log('symmask', code);
  }
  await browser.close();
}
