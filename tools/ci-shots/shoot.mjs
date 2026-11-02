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
//                  [--query scale=expand] [--only level_start,tutorial,win]   (extra URL parameters; save only these shots)
//                  [--no-motion] [--seq-timeout 420]   (P4B-08: skip / limit the motion windows)
// Motion windows (P4B-08): the game runs two stretches in slow motion (shots.json `sequences`, `motionScale`, ShotScenario) and reports
// `seq:<name>:begin|t=<game ms>|end`. Between begin and end this script photographs the page as fast as it can (JPEG, CSS pixels), tags
// every picture with the last t, and analyses them in the browser: the pictures that differ from the previous one (frame statistics in the
// manifest: samples, unique pictures, changed pairs, changes per game second), the box where anything moved, and a strip PNG
// (<viewport>/seq_<name>_strip.png, 24 pictures cropped to that box). The numbers say how fluid the animation is; the strip is for the
// design-lead's rubric. Decode times of the 3D pages (`[minik] art3d decode ...`) go into the manifest too (P4B-05): a budget warning
// when their sum exceeds shots.json `art3dDecodeBudgetMs`, and every decode after the tutorial shot (during play) is listed as late.
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
const extraQuery = arg('query', '');
const only = arg('only', '');
const noMotion = process.argv.includes('--no-motion');
const seqTimeoutMs = Number(arg('seq-timeout', '420')) * 1000;
const want = only ? new Set(only.split(',').map((x) => x.trim()).filter(Boolean)) : null;
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

// --- motion analysis (P4B-08) ----------------------------------------------------------------------------------------------------
// Runs in a scratch page of the same browser context: decodes the pictures (JPEG, CSS pixels) to canvases, counts the consecutive pairs
// that differ (a pair "changed" when at least 24 pixels differ by more than 48 in the channel sum), takes the union of the changed
// pixels as the box where anything moved, and lays 24 evenly spaced pictures, cropped to that box, out as a strip (PNG data URL).
async function analyseFrames(context, frames, focus) {
  const ap = await context.newPage();
  try {
    await ap.setContent('<!doctype html><body style="margin:0"></body>');
    const items = frames.map((f) => ({ t: f.t, b64: f.buf.toString('base64') }));
    return await ap.evaluate(async ({ list, focus }) => {
      const imgs = [];
      for (const it of list) {
        const im = new Image();
        im.src = 'data:image/jpeg;base64,' + it.b64;
        await im.decode();
        imgs.push(im);
      }
      const w = imgs[0].naturalWidth;
      const h = imgs[0].naturalHeight;
      const cv = document.createElement('canvas');
      cv.width = w;
      cv.height = h;
      const g = cv.getContext('2d', { willReadFrequently: true });
      const px = imgs.map((im) => {
        g.drawImage(im, 0, 0);
        return g.getImageData(0, 0, w, h).data;
      });
      // `focus` (picture fractions, shots.json): only changes inside it count (the UI and the parallax move too), and the strip is cropped to it
      const fx0 = focus ? Math.round(focus.x * w) : 0;
      const fy0 = focus ? Math.round(focus.y * h) : 0;
      const fx1 = focus ? Math.min(w, Math.round((focus.x + focus.w) * w)) : w;
      const fy1 = focus ? Math.min(h, Math.round((focus.y + focus.h) * h)) : h;
      let bx0 = w;
      let by0 = h;
      let bx1 = -1;
      let by1 = -1;
      let changedPairs = 0;
      const gaps = [];
      for (let i = 1; i < px.length; i++) {
        const a = px[i - 1];
        const b = px[i];
        let cnt = 0;
        let x0 = w;
        let y0 = h;
        let x1 = -1;
        let y1 = -1;
        for (let y = fy0; y < fy1; y++) {
          for (let x = fx0; x < fx1; x++) {
            const o = (y * w + x) * 4;
            const d = Math.abs(a[o] - b[o]) + Math.abs(a[o + 1] - b[o + 1]) + Math.abs(a[o + 2] - b[o + 2]);
            if (d > 48) {
              cnt++;
              if (x < x0) x0 = x;
              if (x > x1) x1 = x;
              if (y < y0) y0 = y;
              if (y > y1) y1 = y;
            }
          }
        }
        if (cnt >= 24) {
          changedPairs++;
          bx0 = Math.min(bx0, x0);
          by0 = Math.min(by0, y0);
          bx1 = Math.max(bx1, x1);
          by1 = Math.max(by1, y1);
        }
        gaps.push(list[i].t - list[i - 1].t);
      }
      const pad = 24;
      let box;
      if (focus) box = { x: fx0, y: fy0, w: fx1 - fx0, h: fy1 - fy0 };
      else if (bx1 < 0) box = { x: Math.round(w / 4), y: Math.round(h / 4), w: Math.round(w / 2), h: Math.round(h / 2) };
      else {
        const x = Math.max(0, bx0 - pad);
        const y = Math.max(0, by0 - pad);
        box = { x, y, w: Math.min(w - x, Math.max(160, bx1 - x + pad)), h: Math.min(h - y, Math.max(160, by1 - y + pad)) };
      }
      const n = Math.min(24, imgs.length);
      const pick = [];
      for (let k = 0; k < n; k++) pick.push(Math.round((k * (imgs.length - 1)) / Math.max(1, n - 1)));
      const scale = 200 / box.h;
      const cw = Math.max(1, Math.round(box.w * scale));
      const cols = 8;
      const rows = Math.ceil(n / cols);
      const sheet = document.createElement('canvas');
      sheet.width = cols * (cw + 4) + 4;
      sheet.height = rows * (200 + 18) + 4;
      const sg = sheet.getContext('2d');
      sg.fillStyle = '#26323f';
      sg.fillRect(0, 0, sheet.width, sheet.height);
      sg.font = '11px sans-serif';
      pick.forEach((idx, k) => {
        const cx = 4 + (k % cols) * (cw + 4);
        const cy = 4 + Math.floor(k / cols) * 218;
        sg.drawImage(imgs[idx], box.x, box.y, box.w, box.h, cx, cy + 16, cw, 200);
        sg.fillStyle = '#ffffff';
        sg.fillText('t=' + list[idx].t + ' ms', cx + 2, cy + 12);
      });
      const span = list[list.length - 1].t - list[0].t;
      const sorted = [...gaps].sort((p, q) => p - q);
      const median = sorted.length ? sorted[Math.floor(sorted.length / 2)] : 0;
      return {
        sheet: sheet.toDataURL('image/png'),
        pictureSize: { w, h },
        box,
        movedBox: bx1 < 0 ? null : { x: bx0, y: by0, w: bx1 - bx0 + 1, h: by1 - by0 + 1 },
        focus: focus || null,
        firstT: list[0].t,
        lastT: list[list.length - 1].t,
        gameSpanMs: span,
        medianGapMs: median,
        changedPairs,
        changedRatio: gaps.length ? Math.round((changedPairs / gaps.length) * 100) / 100 : 0,
        changesPerGameSecond: span > 0 ? Math.round((changedPairs / (span / 1000)) * 10) / 10 : null,
        // above ~16.7 ms between pictures the cadence is below 60 per game second: the count is a lower bound
        undersampled: median > 16.7,
      };
    }, { list: items, focus: focus || null });
  } finally {
    await ap.close().catch(() => {});
  }
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
  const decodes = []; // P4B-05: `[minik] art3d decode <resource> <w>x<h> <ms> ms`
  const seqT = {}; // P4B-08: the last `seq:<name>:t=` per sequence
  const seqEnded = new Set();
  let currentShot = 'boot';
  page.on('console', (msg) => {
    const text = msg.text();
    const d = /\[minik\] art3d decode (\S+) (\d+)x(\d+) (\d+) ms/.exec(text);
    if (d) decodes.push({ resource: d[1], width: Number(d[2]), height: Number(d[3]), ms: Number(d[4]), afterShot: currentShot });
    const i = text.indexOf(cfg.tag);
    const ev = i >= 0 ? text.slice(i + cfg.tag.length).trim().split('\n')[0].trim() : null;
    const tm = ev ? /^seq:([a-z_]+):t=(\d+)$/.exec(ev) : null;
    // the per-frame t lines are kept out of the console tail (hundreds of them) and out of the ordered event list
    if (!tm) {
      consoleLog.push('[' + msg.type() + '] ' + text);
      if (consoleLog.length > 3000) consoleLog.splice(0, 1000);
    }
    if (!ev) return;
    if (tm) {
      seqT[tm[1]] = Number(tm[2]);
      return;
    }
    if (ev.startsWith('shot:')) currentShot = ev.slice(5);
    const end = /^seq:([a-z_]+):end$/.exec(ev);
    if (end) seqEnded.add(end[1]);
    events.push(ev);
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
  /** P4B-08: photographs the page between `seq:<name>:begin` and `:end`, analyses the pictures in the browser, writes the strip. */
  async function captureSequence(pg, context, viewport, name, dir, focus) {
    const begin = await next((e) => e === 'seq:' + name + ':begin' || e.startsWith('error:'), shotTimeoutMs);
    if (begin === null || begin.startsWith('error:')) {
      const why = begin === null ? 'seq:' + name + ':begin did not arrive within ' + shotTimeoutMs / 1000 + ' s' : begin;
      warn(viewport.name + ': ' + why);
      return { ok: false, error: why };
    }
    const frames = [];
    const deadline = Date.now() + seqTimeoutMs;
    while (!seqEnded.has(name) && Date.now() < deadline) {
      const t = seqT[name] === undefined ? 0 : seqT[name];
      const buf = await pg.screenshot({ type: 'jpeg', quality: 60, scale: 'css' });
      frames.push({ t, buf });
    }
    const finished = seqEnded.has(name);
    if (!finished) warn(viewport.name + ': seq:' + name + ':end did not arrive within ' + seqTimeoutMs / 1000 + ' s (' + frames.length + ' pictures)');
    if (frames.length < 3) return { ok: false, error: 'only ' + frames.length + ' picture(s)' };
    const crypto = await import('node:crypto');
    const unique = new Set(frames.map((f) => crypto.createHash('sha1').update(f.buf).digest('hex'))).size;
    const analysis = await analyseFrames(context, frames, focus);
    const stripName = 'seq_' + name + '_strip.png';
    fs.writeFileSync(path.join(dir, stripName), Buffer.from(analysis.sheet.split(',')[1], 'base64'));
    delete analysis.sheet;
    return { ok: finished, finished, pictures: frames.length, uniquePictures: unique, strip: viewport.name + '/' + stripName, ...analysis };
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

    const motionQuery = noMotion || !cfg.motionScale ? '' : '&motion=' + cfg.motionScale + (cfg.motionMs ? '&motionMs=' + cfg.motionMs : '');
    await page.goto(baseUrl + '?shots=1' + motionQuery + (extraQuery ? '&' + extraQuery : ''), { waitUntil: 'domcontentloaded', timeout: 60000 });
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
      for (const sq of (cfg.sequences || []).filter((s) => s.before === name)) {
        if (noMotion) continue;
        try {
          result.motion = result.motion || {};
          result.motion[sq.name] = await captureSequence(page, ctx, vp, sq.name, dirVp, sq.focus);
        } catch (e) {
          result.motionErrors = result.motionErrors || [];
          result.motionErrors.push(sq.name + ': ' + String(e && e.message ? e.message : e));
          warn(vp.name + ': motion window ' + sq.name + ' failed: ' + String(e && e.message ? e.message : e));
        }
      }
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
      if (want && !want.has(name)) continue; // signalled, but not wanted in this run
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
    // P4B-05: the decode budget (a desktop-class CPU in CI; a phone is a few times slower)
    const total = decodes.reduce((s, d) => s + d.ms, 0);
    const late = decodes.filter((d) => ['tutorial', 'drag_mid', 'landed', 'win'].includes(d.afterShot));
    result.art3d = { decodes, totalMs: total, budgetMs: cfg.art3dDecodeBudgetMs, lateDecodes: late.map((d) => d.resource + ' (' + d.ms + ' ms after ' + d.afterShot + ')') };
    if (cfg.art3dDecodeBudgetMs && total > cfg.art3dDecodeBudgetMs) warn(vp.name + ': 3D page decodes took ' + total + ' ms in total (budget ' + cfg.art3dDecodeBudgetMs + ' ms)');
    if (late.length > 0) warn(vp.name + ': 3D page decode during play: ' + result.art3d.lateDecodes.join(', '));
    result.ok = result.errors.length === 0 && result.screens.length === (want ? want.size : cfg.shots.length) && !result.screens.some((s) => s.blank);
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
  query: extraQuery || null,
  shots: want ? cfg.shots.filter((n) => want.has(n)) : cfg.shots,
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
    console.log(vp.name + ': ' + manifest.viewports[vp.name].screens.length + '/' + (want ? want.size : cfg.shots.length) + ' shots, errors: ' + manifest.viewports[vp.name].errors.length);
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
