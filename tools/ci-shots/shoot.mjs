#!/usr/bin/env node
// CI screenshots of the Unity WebGL build (paket C1; docs/CI.md "Ekran goruntuleri").
//
// Serves the built player (build/WebGL/MinikUsta) from a tiny local HTTP server, opens it in headless Chromium with software GL
// (SwiftShader) on mobile-sized viewports, appends `?shots=1` (the game then plays ShotScenario: home, settings, level_start,
// tutorial, drag_mid, landed, win) and saves a PNG whenever the page logs `[minikShot] shot:<name>` to the console. A manifest.json describes
// what was captured. The script NEVER fails the build: every problem becomes a `::warning::` annotation and an entry in the
// manifest, and the exit code is 0 (the workflow job is also continue-on-error). Exit code 2 only for wrong arguments.
//
//   node shoot.mjs --dir build/WebGL/MinikUsta --out artifacts/shots --sha <git sha> [--dpr 2] [--port 0]
//                  [--ready-timeout 240] [--shot-timeout 150] [--url-path /] [--settle 600]
// Environment: PLAYWRIGHT_CHROMIUM_EXECUTABLE (use a preinstalled Chromium), GITHUB_* (written into the manifest).
import fs from 'node:fs';
import http from 'node:http';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import zlib from 'node:zlib';
import { chromium } from 'playwright';

const here = path.dirname(fileURLToPath(import.meta.url));
const cfg = JSON.parse(fs.readFileSync(path.join(here, 'shots.json'), 'utf8'));

function arg(name, fallback) {
  const i = process.argv.indexOf('--' + name);
  return i >= 0 && i + 1 < process.argv.length ? process.argv[i + 1] : fallback;
}

const dir = arg('dir', null);
const out = arg('out', 'artifacts/shots');
const sha = arg('sha', process.env.GITHUB_SHA || 'unknown');
const dpr = Number(arg('dpr', '2'));
const port = Number(arg('port', '0'));
const readyTimeoutMs = Number(arg('ready-timeout', '240')) * 1000;
const shotTimeoutMs = Number(arg('shot-timeout', '150')) * 1000;
const settleMs = Number(arg('settle', '600'));
const urlPath = arg('url-path', '/');
if (!dir || !fs.existsSync(path.join(dir, 'index.html'))) {
  console.error('shoot.mjs: --dir must hold the WebGL player (index.html not found in ' + dir + ')');
  process.exit(2);
}

const warnings = [];
function warn(text) {
  warnings.push(text);
  console.log('::warning title=CI screenshots::' + text.replace(/\r?\n/g, ' '));
}
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// --- static server -----------------------------------------------------------------------------------------------------------
const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json',
  '.wasm': 'application/wasm',
  '.data': 'application/octet-stream',
  '.symbols': 'application/octet-stream',
  '.png': 'image/png',
  '.ico': 'image/x-icon',
  '.svg': 'image/svg+xml',
  '.mem': 'application/octet-stream',
};

function serve(root, listenPort) {
  const base = path.resolve(root);
  const server = http.createServer((req, res) => {
    try {
      let p = decodeURIComponent((req.url || '/').split('?')[0]);
      if (p.endsWith('/')) p += 'index.html';
      const file = path.resolve(path.join(base, p));
      if (!file.startsWith(base + path.sep) && file !== base) {
        res.writeHead(403).end('forbidden');
        return;
      }
      let ext = path.extname(file).toLowerCase();
      let enc = null;
      let real = file;
      if (!fs.existsSync(real)) {
        res.writeHead(404).end('not found');
        return;
      }
      // a compressed Unity build (.br / .gz) is served with its Content-Encoding, the inner extension decides the type
      if (ext === '.br' || ext === '.gz') {
        enc = ext === '.br' ? 'br' : 'gzip';
        ext = path.extname(file.slice(0, -ext.length)).toLowerCase();
      }
      const headers = { 'Content-Type': MIME[ext] || 'application/octet-stream', 'Cache-Control': 'no-store' };
      if (enc) headers['Content-Encoding'] = enc;
      const stat = fs.statSync(real);
      headers['Content-Length'] = stat.size;
      res.writeHead(200, headers);
      fs.createReadStream(real).pipe(res);
    } catch (e) {
      res.writeHead(500).end(String(e));
    }
  });
  return new Promise((resolve) => server.listen(listenPort, '127.0.0.1', () => resolve(server)));
}

// --- is the picture flat? ------------------------------------------------------------------------------------------------------
// A canvas that was not captured comes out as one flat colour (black / white). Decodes the 8-bit PNG Playwright writes (filter types
// 0-4, no interlace) and counts the distinct colours (4 bits per channel) on a sparse grid. A game frame has dozens; a flat one has 1-3.
function distinctColours(file) {
  const buf = fs.readFileSync(file);
  let pos = 8;
  let width = 0;
  let height = 0;
  let depth = 0;
  let colourType = 0;
  const idat = [];
  while (pos + 8 <= buf.length) {
    const len = buf.readUInt32BE(pos);
    const type = buf.toString('latin1', pos + 4, pos + 8);
    const data = buf.subarray(pos + 8, pos + 8 + len);
    if (type === 'IHDR') {
      width = data.readUInt32BE(0);
      height = data.readUInt32BE(4);
      depth = data[8];
      colourType = data[9];
    } else if (type === 'IDAT') idat.push(data);
    else if (type === 'IEND') break;
    pos += 12 + len;
  }
  const bpp = colourType === 6 ? 4 : colourType === 2 ? 3 : 0;
  if (depth !== 8 || bpp === 0) return -1; // not a format this check understands: do not judge
  const raw = zlib.inflateSync(Buffer.concat(idat));
  const stride = width * bpp;
  const rows = [];
  let prev = Buffer.alloc(stride);
  let o = 0;
  for (let y = 0; y < height; y++) {
    const ft = raw[o++];
    const row = Buffer.from(raw.subarray(o, o + stride));
    o += stride;
    for (let i = 0; i < stride; i++) {
      const a = i >= bpp ? row[i - bpp] : 0;
      const b = prev[i];
      const c = i >= bpp ? prev[i - bpp] : 0;
      let add = 0;
      if (ft === 1) add = a;
      else if (ft === 2) add = b;
      else if (ft === 3) add = (a + b) >> 1;
      else if (ft === 4) {
        const p = a + b - c;
        const pa = Math.abs(p - a);
        const pb = Math.abs(p - b);
        const pc = Math.abs(p - c);
        add = pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
      }
      row[i] = (row[i] + add) & 255;
    }
    rows.push(row);
    prev = row;
  }
  const seen = new Set();
  const stepX = Math.max(1, Math.floor(width / 40));
  const stepY = Math.max(1, Math.floor(height / 80));
  for (let y = 0; y < height; y += stepY)
    for (let x = 0; x < width; x += stepX) {
      const r = rows[y];
      const k = x * bpp;
      seen.add(((r[k] >> 4) << 8) | ((r[k + 1] >> 4) << 4) | (r[k + 2] >> 4));
    }
  return seen.size;
}

// --- one viewport ------------------------------------------------------------------------------------------------------------
const ANDROID_UA =
  'Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Mobile Safari/537.36';

async function captureViewport(browser, vp, baseUrl) {
  const result = { width: vp.width, height: vp.height, dpr, screens: [], errors: [], ok: false, canvas: null };
  const dirVp = path.join(out, vp.name);
  fs.mkdirSync(dirVp, { recursive: true });
  const ctx = await browser.newContext({
    viewport: { width: vp.width, height: vp.height },
    deviceScaleFactor: dpr,
    isMobile: true, // the default Unity template switches to its full-window "unity-mobile" layout for a mobile user agent
    hasTouch: true,
    userAgent: ANDROID_UA,
    locale: 'tr-TR',
  });
  const page = await ctx.newPage();
  const events = [];
  const consoleLog = [];
  page.on('console', (msg) => {
    const text = msg.text();
    consoleLog.push('[' + msg.type() + '] ' + text);
    if (consoleLog.length > 3000) consoleLog.splice(0, 1000);
    const i = text.indexOf(cfg.tag);
    if (i >= 0) events.push(text.slice(i + cfg.tag.length).trim().split('\n')[0].trim());
  });
  page.on('pageerror', (e) => consoleLog.push('[pageerror] ' + e.message));
  page.on('crash', () => consoleLog.push('[crash] the page crashed'));
  let cursor = 0;
  async function next(match, timeoutMs) {
    const end = Date.now() + timeoutMs;
    for (;;) {
      while (cursor < events.length) {
        const e = events[cursor++];
        if (match(e)) return e;
      }
      if (Date.now() > end) return null;
      await sleep(100);
    }
  }
  const finish = async () => {
    fs.writeFileSync(path.join(out, 'console-' + vp.name + '.log'), consoleLog.slice(-600).join('\n') + '\n');
    await ctx.close();
  };

  try {
    const gl = await page.evaluate(() => {
      const c = document.createElement('canvas');
      const g = c.getContext('webgl2');
      if (!g) return null;
      const ext = g.getExtension('WEBGL_debug_renderer_info');
      return ext ? g.getParameter(ext.UNMASKED_RENDERER_WEBGL) : 'webgl2';
    });
    result.gl = gl;
    if (!gl) warn(vp.name + ': this Chromium has no WebGL2 (software GL flags did not take effect)');

    await page.goto(baseUrl + '?shots=1', { waitUntil: 'domcontentloaded', timeout: 60000 });
    const ready = await next((e) => e.startsWith('ready') || e.startsWith('error:'), readyTimeoutMs);
    if (ready === null || ready.startsWith('error:')) {
      result.errors.push(ready === null ? 'the game did not report "ready" within ' + readyTimeoutMs / 1000 + ' s' : ready);
      warn(vp.name + ': ' + result.errors[result.errors.length - 1]);
      await page.screenshot({ path: path.join(dirVp, '_failure.png') }).catch(() => {});
      return result;
    }
    result.canvas = await page.evaluate(() => {
      const c = document.querySelector('canvas');
      return c ? { cssWidth: c.clientWidth, cssHeight: c.clientHeight, pixelWidth: c.width, pixelHeight: c.height } : null;
    });
    result.ready = ready;

    for (const name of cfg.shots) {
      const ev = await next((e) => e === 'shot:' + name || e.startsWith('error:'), shotTimeoutMs);
      if (ev === null) {
        result.errors.push('shot "' + name + '" did not arrive within ' + shotTimeoutMs / 1000 + ' s');
        warn(vp.name + ': ' + result.errors[result.errors.length - 1]);
        await page.screenshot({ path: path.join(dirVp, '_failure.png') }).catch(() => {});
        break;
      }
      if (ev.startsWith('error:')) {
        result.errors.push('the scenario reported ' + ev + ' before shot "' + name + '"');
        warn(vp.name + ': ' + result.errors[result.errors.length - 1]);
        await page.screenshot({ path: path.join(dirVp, '_failure.png') }).catch(() => {});
        break;
      }
      await sleep(settleMs); // the frame after the signal reaches the compositor (software GL is slow)
      const file = path.join(dirVp, name + '.png');
      await page.screenshot({ path: file, type: 'png' });
      const bytes = fs.statSync(file).size;
      let colours = -1;
      try {
        colours = distinctColours(file);
      } catch (e) {
        consoleLog.push('[shoot] colour check failed: ' + e.message);
      }
      const blank = colours >= 0 && colours < 4;
      if (blank) warn(vp.name + '/' + name + '.png is flat (' + colours + ' colours): the canvas was probably not captured');
      result.screens.push({ name, file: vp.name + '/' + name + '.png', bytes, colours, blank });
    }
    result.ok = result.errors.length === 0 && result.screens.length === cfg.shots.length && !result.screens.some((s) => s.blank);
    return result;
  } catch (e) {
    result.errors.push(String(e && e.message ? e.message : e));
    warn(vp.name + ': ' + result.errors[result.errors.length - 1]);
    return result;
  } finally {
    await finish();
  }
}

// --- main --------------------------------------------------------------------------------------------------------------------
function playwrightVersion() {
  try {
    return JSON.parse(fs.readFileSync(path.join(here, 'node_modules', 'playwright', 'package.json'), 'utf8')).version;
  } catch {
    return '?';
  }
}

fs.mkdirSync(out, { recursive: true });
const server = await serve(dir, port);
const baseUrl = 'http://127.0.0.1:' + server.address().port + urlPath;
console.log('serving ' + dir + ' at ' + baseUrl);

const manifest = {
  schema: 1,
  commit: sha,
  shortSha: sha.slice(0, 7),
  generatedAt: new Date().toISOString(),
  run:
    process.env.GITHUB_RUN_ID && process.env.GITHUB_REPOSITORY
      ? (process.env.GITHUB_SERVER_URL || 'https://github.com') + '/' + process.env.GITHUB_REPOSITORY + '/actions/runs/' + process.env.GITHUB_RUN_ID
      : null,
  renderer: 'headless Chromium, software GL (SwiftShader), Playwright ' + playwrightVersion(),
  dpr,
  shots: cfg.shots,
  viewports: {},
  ok: false,
  warnings,
};

let browser = null;
try {
  browser = await chromium.launch({
    headless: true,
    executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE || undefined,
    args: [
      '--use-angle=swiftshader',
      '--enable-unsafe-swiftshader',
      '--ignore-gpu-blocklist',
      '--enable-webgl',
      '--mute-audio',
      '--autoplay-policy=no-user-gesture-required',
      '--disable-background-timer-throttling',
      '--disable-renderer-backgrounding',
      '--disable-backgrounding-occluded-windows',
    ],
  });
  manifest.chromium = browser.version();
  for (const vp of cfg.viewports) {
    console.log('capturing ' + vp.name + ' ...');
    manifest.viewports[vp.name] = await captureViewport(browser, vp, baseUrl);
    console.log(vp.name + ': ' + manifest.viewports[vp.name].screens.length + '/' + cfg.shots.length + ' shots, errors: ' + manifest.viewports[vp.name].errors.length);
  }
} catch (e) {
  warn('browser: ' + (e && e.message ? e.message : e));
} finally {
  if (browser) await browser.close().catch(() => {});
  server.close();
}

manifest.ok = Object.keys(manifest.viewports).length === cfg.viewports.length && Object.values(manifest.viewports).every((v) => v.ok);
fs.writeFileSync(path.join(out, 'manifest.json'), JSON.stringify(manifest, null, 2) + '\n');
console.log('manifest: ' + path.join(out, 'manifest.json') + ' ok=' + manifest.ok);
process.exit(0);
