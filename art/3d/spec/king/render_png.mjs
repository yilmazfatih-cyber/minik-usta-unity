// SVG -> PNG for the Usta Kral design sheets (reference only). Uses Playwright + the preinstalled Chromium from the
// web repo's node_modules (no new dependency). Usage: node art/3d/spec/king/render_png.mjs [--web ../minik-usta]
import { readdirSync, readFileSync, existsSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const argWeb = process.argv.indexOf('--web');
const web = resolve(argWeb > 0 ? process.argv[argWeb + 1] : process.env.MINIK_USTA_WEB ?? '/home/user/minik-usta');
const require = createRequire(join(web, 'package.json'));
const pw = require('playwright');
const exe = process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE ?? '/opt/pw-browsers/chromium';
const fontPath = join(web, 'public', 'fonts', 'baloo2-latin-tr.woff2');
const font = existsSync(fontPath) ? readFileSync(fontPath).toString('base64') : '';

const browser = await pw.chromium.launch({ executablePath: exe, headless: true });
const page = await browser.newPage();
for (const name of readdirSync(here).filter((n) => n.endsWith('.svg')).sort()) {
  const svg = readFileSync(join(here, name), 'utf8');
  const w = Number(/width="(\d+)"/.exec(svg)[1]);
  const h = Number(/height="(\d+)"/.exec(svg)[1]);
  await page.setViewportSize({ width: w, height: h });
  const css = font ? `@font-face{font-family:'Baloo 2';src:url(data:font/woff2;base64,${font}) format('woff2');font-weight:400 800;}` : '';
  await page.setContent(`<html><head><style>${css}html,body{margin:0;background:transparent}</style></head><body>${svg}</body></html>`);
  await page.evaluate(() => document.fonts.ready);
  await page.screenshot({ path: join(here, name.replace(/\.svg$/, '.png')), clip: { x: 0, y: 0, width: w, height: h } });
  console.log('png', name);
}
await browser.close();
