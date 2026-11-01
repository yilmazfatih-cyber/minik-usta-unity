#!/usr/bin/env node
/**
 * v4 art pack composite mocks (design-lead, package V0). Draws ONLY from the shipped atlas (Assets/MinikUsta/Resources/
 * art/atlas-N.png.bytes + atlas.json) so the mocks also prove the frame contract (pivot, 9-slice border, scale, stud
 * anchor): artifacts/preview/v4-level.png (Bölüm 3 data, mid-move), v4-home.png, v4-win.png, v4-sheet.png.
 * Layout numbers = Presentation.Core ScreenLayout (FIT 1080 × 1920) and ART_REFERENCE §9. Texts are i18n strings
 * (Resources/i18n/tr.json) rendered in Baloo 2 (web repo font, OFL) or the system font when missing.
 *
 * Usage: node tools/art/preview.mjs [--web ../minik-usta] [--out artifacts/preview] [--only level,home,win,sheet]
 */
import { createRequire } from 'node:module';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const args = process.argv.slice(2);
const opt = (n, d) => {
  const i = args.indexOf('--' + n);
  return i >= 0 && i + 1 < args.length ? args[i + 1] : d;
};
const WEB = resolve(opt('web', join(ROOT, '..', 'minik-usta')));
const OUT = resolve(opt('out', join(ROOT, 'artifacts', 'preview')));
const ONLY = opt('only', 'level,home,win,sheet').split(',');
const ART = join(ROOT, 'Assets', 'MinikUsta', 'Resources', 'art');
const RES = join(ROOT, 'Assets', 'MinikUsta', 'Resources');

const atlas = JSON.parse(readFileSync(join(ART, 'atlas.json'), 'utf8'));
const pages = atlas.atlases.map((a, i) => readFileSync(join(ART, `atlas-${i}.png.bytes`)).toString('base64'));
const tr = JSON.parse(readFileSync(join(RES, 'i18n', 'tr.json'), 'utf8'));
const level3 = JSON.parse(readFileSync(join(RES, 'levels', 'level_003.json'), 'utf8'));
const fontPath = join(WEB, 'public', 'fonts', 'baloo2-latin-tr.woff2');
const font = existsSync(fontPath) ? readFileSync(fontPath).toString('base64') : '';
const t = (k, vars = {}) => {
  let s = k.split('.').reduce((o, p) => (o ? o[p] : undefined), tr) ?? k;
  for (const [a, b] of Object.entries(vars)) s = s.replace('{' + a + '}', b);
  return s;
};

const require = createRequire(join(WEB, 'package.json'));
let pw;
try {
  pw = require('playwright');
} catch {
  pw = require('@playwright/test');
}

// ---- page-side drawing kit (serialised into the browser) ----------------------------------------------------------
const KIT = String.raw`
const A = window.__atlas; const IMG = window.__img;
const BLOCK_HEX = { W: '#764012', Y: '#FFE23E', G: '#44D088', R: '#CF404E', O: '#EC8D20', C: '#C8CFDA', B: '#2984DE', P: '#5D3AAE' };
function fr(n) { const f = A.frames[n]; if (!f) throw new Error('missing frame ' + n); return f; }
function dsz(n) { const f = fr(n); return [f.w / f.scale, f.h / f.scale]; }
/** draw frame n into design rect (x, y, w, h); defaults to the frame's design size. opts: alpha, tint, flipX, flipY, rot (deg, around centre) */
function spr(g, n, x, y, w, h, o = {}) {
  const f = fr(n); const [dw, dh] = [f.w / f.scale, f.h / f.scale];
  w = w ?? dw; h = h ?? dh;
  g.save(); g.globalAlpha = o.alpha ?? 1;
  g.translate(x + w / 2, y + h / 2); if (o.rot) g.rotate(o.rot * Math.PI / 180); g.scale(o.flipX ? -1 : 1, o.flipY ? -1 : 1);
  if (o.tint) { const c = tinted(n, o.tint); g.drawImage(c, -w / 2, -h / 2, w, h); }
  else g.drawImage(IMG[f.atlas], f.x, f.y, f.w, f.h, -w / 2, -h / 2, w, h);
  g.restore();
}
/** pivot placement: (px, py) design point gets the frame pivot; scale s of design size */
function spv(g, n, px, py, s = 1, o = {}) {
  const f = fr(n); const w = f.w / f.scale * s; const h = f.h / f.scale * s;
  spr(g, n, px - f.pivotX * w, py - (1 - f.pivotY) * h, w, h, o);
}
const TC = {};
function tinted(n, col) {
  const key = n + col; if (TC[key]) return TC[key];
  const f = fr(n); const c = document.createElement('canvas'); c.width = f.w; c.height = f.h; const x = c.getContext('2d');
  x.drawImage(IMG[f.atlas], f.x, f.y, f.w, f.h, 0, 0, f.w, f.h);
  x.globalCompositeOperation = 'multiply'; x.fillStyle = col; x.fillRect(0, 0, f.w, f.h);
  x.globalCompositeOperation = 'destination-in'; x.drawImage(IMG[f.atlas], f.x, f.y, f.w, f.h, 0, 0, f.w, f.h);
  TC[key] = c; return c;
}
/** 9-slice into design rect (borders: atlas px → design px) */
function nine(g, n, x, y, w, h, o = {}) {
  const f = fr(n); const s = f.scale; const im = IMG[f.atlas];
  const L = f.borderL, T = f.borderT, R = f.borderR, B = f.borderB;
  const dl = L / s, dt = T / s, dr = R / s, db = B / s;
  const sx = [f.x, f.x + L, f.x + f.w - R, f.x + f.w]; const sy = [f.y, f.y + T, f.y + f.h - B, f.y + f.h];
  const dx = [x, x + dl, x + w - dr, x + w]; const dy = [y, y + dt, y + h - db, y + h];
  g.save(); g.globalAlpha = o.alpha ?? 1;
  for (let i = 0; i < 3; i++) for (let j = 0; j < 3; j++) {
    const sw = sx[i + 1] - sx[i], sh = sy[j + 1] - sy[j], ww = dx[i + 1] - dx[i], hh = dy[j + 1] - dy[j];
    if (sw > 0 && sh > 0 && ww > 0 && hh > 0) g.drawImage(im, sx[i], sy[j], sw, sh, dx[i], dy[j], ww, hh);
  }
  g.restore();
}
function tileX(g, n, x, y, w, h) { const [dw, dh] = dsz(n); const k = h / dh; const tw = dw * k; g.save(); g.beginPath(); g.rect(x, y, w, h); g.clip(); for (let xx = x; xx < x + w; xx += tw) spr(g, n, xx, y, tw + 0.5, h); g.restore(); }
function tileY(g, n, x, y, w, h) { const [dw, dh] = dsz(n); const k = w / dw; const th = dh * k; g.save(); g.beginPath(); g.rect(x, y, w, h); g.clip(); for (let yy = y; yy < y + h; yy += th) spr(g, n, x, yy, w, th + 0.5); g.restore(); }
// ---- text (ART_REFERENCE §4) ----
const FF = "'Baloo 2', 'Arial Rounded MT Bold', 'Arial', sans-serif";
function txt(g, s, x, y, size, style = 'plain', align = 'center', o = {}) {
  g.save(); g.font = '800 ' + size + 'px ' + FF; g.textAlign = align; g.textBaseline = 'middle'; g.lineJoin = 'round';
  const em = size;
  const st = (c, w) => { g.strokeStyle = c; g.lineWidth = w; g.strokeText(s, x, y); };
  const fl = (c, dx = 0, dy = 0) => { g.fillStyle = c; g.fillText(s, x + dx, y + dy); };
  if (style === 'twoRing') {
    g.save(); g.translate(0, 0.06 * em); st('#211865', 2 * 0.145 * em); fl('#211865'); g.restore();
    st('#3325A2', 2 * 0.145 * em); st('#5A47EF', 2 * 0.10 * em); fl('#FFFFFF');
  } else if (style === 'gold') {
    g.save(); g.translate(0, 0.06 * em); g.globalAlpha = 0.6; st('#5E2206', 2 * 0.06 * em + 4); fl('#5E2206'); g.restore();
    st('#B8430A', 2 * 0.06 * em + 2);
    const gr = g.createLinearGradient(0, y - em * 0.4, 0, y + em * 0.35); gr.addColorStop(0, '#FFF3A8'); gr.addColorStop(0.45, '#FFD119'); gr.addColorStop(1, '#FFAD00'); fl(gr);
  } else if (style === 'button') {
    const c = o.stroke ?? '#0E5A14'; const w = Math.max(4, 0.06 * em);
    g.save(); g.translate(0, 0.045 * em); st(c, 2 * w); fl(c); g.restore(); st(c, 2 * w); fl(o.fill ?? '#FFFFFF');
  } else if (style === 'cream') { st('#0B0918', 2 * 0.08 * em); fl('#FFF6E3'); }
  else if (style === 'lav') { fl('#0E0C2C', 0, 3); fl('#A09AFC'); }
  else if (style === 'big') { g.save(); g.translate(0, 0.05 * em); st('#00325A', 2 * 0.08 * em); fl('#00325A'); g.restore(); st('#00325A', 2 * 0.08 * em); fl('#FBF1BD'); }
  else if (style === 'card') { fl(o.fill ?? '#1A467B'); }
  else if (style === 'label') { st(o.stroke ?? '#352496', 2 * 0.06 * em); fl(o.fill ?? '#FFFFFF'); }
  else { g.save(); g.translate(0, 0.05 * em); st('#111111', 2 * 0.07 * em); fl('#111111'); g.restore(); st('#111111', 2 * 0.07 * em); fl(o.fill ?? '#FFFFFF'); }
  g.restore();
}
// ---- pieces ----
function maskOf(set, x, y) { let m = 0; if (set.has((x - 1) + ',' + y)) m |= 1; if (set.has((x + 1) + ',' + y)) m |= 2; if (set.has(x + ',' + (y - 1))) m |= 4; if (set.has(x + ',' + (y + 1))) m |= 8; return m; }
/** cells in board coords (y up); cellRect(x, y) → [px, py, c] (design, top-left); opts: locked, holdable, scale */
function piece(g, color, cells, cellRect, o = {}) {
  const c = color.toLowerCase(); const set = new Set(cells.map(([x, y]) => x + ',' + y));
  const sc = o.scale ?? 1;
  let cx0 = 0, cy0 = 0;
  if (sc !== 1) { const rs = cells.map(([x, y]) => cellRect(x, y)); const xs = rs.map(r => r[0]); const ys = rs.map(r => r[1]); const cc = rs[0][2]; cx0 = (Math.min(...xs) + Math.max(...xs) + cc) / 2; cy0 = (Math.min(...ys) + Math.max(...ys) + cc) / 2; }
  g.save(); if (sc !== 1) { g.translate(cx0, cy0); g.scale(sc, sc); g.translate(-cx0, -cy0); }
  for (const [x, y] of cells) {
    const [px, py, cc] = cellRect(x, y); const m = maskOf(set, x, y); const mm = String(m).padStart(2, '0');
    spr(g, 'blk_' + c + '_' + mm, px, py, cc, cc, o.locked ? { tint: '#EFEFEF' } : {});
    for (const [k, dx, dy] of [['tl', -1, 1], ['tr', 1, 1], ['bl', -1, -1], ['br', 1, -1]]) {
      if (set.has((x + dx) + ',' + y) && set.has(x + ',' + (y + dy)) && set.has((x + dx) + ',' + (y + dy))) {
        const n = 'blk_' + c + '_' + mm + '_cf_' + k; const f = fr(n); const s = cc / 120; const w = f.w / f.scale * s, h = f.h / f.scale * s;
        spr(g, n, dx < 0 ? px : px + cc - w, dy > 0 ? py : py + cc - h, w, h, o.locked ? { tint: '#EFEFEF' } : {});
      }
    }
    const f = fr('blk_' + c + '_' + mm); const ax = px + f.anchorX * cc; const ay = py + (1 - f.anchorY) * cc;
    if (o.holdable) { const gs = f.studD / 64 * cc / 120; spv(g, 'blk_gloss', ax, ay, gs); }
    if (o.locked) spr(g, 'blk_locked', px, py, cc, cc);
  }
  g.restore();
}
function shadowCells(g, color, cells, cellRect, kind, lineCol) {
  const set = new Set(cells.map(([x, y]) => x + ',' + y));
  for (const [x, y] of cells) { const [px, py, cc] = cellRect(x, y); const mm = String(maskOf(set, x, y)).padStart(2, '0');
    spr(g, 'shadow_fill_' + mm, px, py, cc, cc, { tint: BLOCK_HEX[color], alpha: 0.3 });
    spr(g, 'shadow_' + kind + '_' + mm, px, py, cc, cc, { tint: lineCol }); }
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
      if (font) {
        const bin = Uint8Array.from(atob(font), (c) => c.charCodeAt(0));
        for (const w of ['600', '700', '800']) {
          const ff = new FontFace('Baloo 2', bin, { weight: w });
          await ff.load();
          document.fonts.add(ff);
        }
      }
    },
    { atlas, pages, font },
  );
  await page.addScriptTag({ content: KIT });
  mkdirSync(OUT, { recursive: true });
  const shots = {};
  const ctx = { tr: tr, level3, strings: {} };
  ctx.strings = {
    level: t('home.play', { n: 3 }),
    levelNext: t('home.play', { n: 4 }),
    moves: t('hud.moves'),
    blocks: t('hud.blocks'),
    streak: t('hud.streak'),
    full: t('hud.livesFull'),
    winTitle: t('win.title'),
    winClear: t('win.clear'),
    winBonus: t('win.bonus', { n: 3 }),
    trowel: t('win.trowel', { n: 1 }),
    cont: t('common.continue'),
    home: t('common.home'),
    area: t('town.ch1.title'),
    count: t('common.count', { n: 3, max: 7 }),
    soon: t('common.comingSoon'),
  };
  for (const which of ONLY) {
    const b64 = await page.evaluate(
      ({ which, ctx }) => {
        const S = ctx.strings;
        const W = 1080;
        const H = 1920;
        const cv = document.createElement('canvas');
        cv.width = which === 'sheet' ? 2048 : W;
        cv.height = which === 'sheet' ? 3000 : H;
        const g = cv.getContext('2d');
        g.imageSmoothingQuality = 'high';
        // ---------------------------------------------------------------- level (Bölüm 3, mid-move) ----------------
        function drawLevel(state = {}) {
          spr(g, 'bg_level', 0, 0, W, H);
          const L = ctx.level3;
          const wy = L.yard.cols, hy = L.yard.rows, ws = L.site.cols, hs = L.site.rows;
          const Hh = Math.max(hy, hs);
          const c = 144, wallW = 72, boardW = (wy + ws) * c + wallW, yardX = Math.round((W - boardW) / 2);
          const bottom = 1488, top = bottom - Hh * c, crane = top - 2 * c;
          const wallX = yardX + wy * c, buildX = wallX + wallW;
          const col = (x) => (x < wy ? yardX + x * c : buildX + (x - wy) * c);
          const cellRect = (x, y) => [col(x), bottom - (y + 1) * c, c];
          // tray (ART_REFERENCE §5.3) around crane area + board
          nine(g, 'board_tray', yardX - 48, crane - 48, boardW + 96, bottom - crane + 48 + 64);
          // crane band + yard air
          tileX(g, 'crane_band', yardX, crane, boardW, 2 * c);
          g.fillStyle = 'rgba(255,255,255,0.08)'; g.fillRect(yardX, top, wy * c, (Hh - hy) * c);
          g.fillRect(wallX, top, wallW, (Hh - L.wall.height) * c);
          // yard floor + site floor
          for (let x = 0; x < wy; x++) for (let y = 0; y < hy; y++) { const [px, py] = cellRect(x, y); spr(g, (x + y) % 2 ? 'floor_yard_b' : 'floor_yard_a', px, py, c, c); }
          for (let x = 0; x < ws; x++) for (let y = 0; y < hs; y++) { const [px, py] = cellRect(wy + x, y); spr(g, 'floor_site', px, py, c, c); }
          // plan ghosts (rows[0] = top) with build front
          const rows = L.build.segments[0].rows; const plan = {};
          rows.forEach((r, i) => [...r].forEach((ch, x) => (plan[x + ',' + (hs - 1 - i)] = ch)));
          const placed = state.placed ?? [];
          const filled = new Set(); for (const p of placed) for (const [x, y] of p.cells) filled.add(x + ',' + y);
          for (let x = 0; x < ws; x++) {
            let front = -1; for (let y = 0; y < hs; y++) if (!filled.has(x + ',' + y)) { front = y; break; }
            for (let y = 0; y < hs; y++) { if (filled.has(x + ',' + y)) continue; const [px, py] = cellRect(wy + x, y); const ch = plan[x + ',' + y].toLowerCase();
              spr(g, y === front ? 'ghost_' + ch + '_front' : 'ghost_' + ch, px, py, c, c); if (y === front) spr(g, 'ghost_front', px, py, c, c); }
          }
          // scaffold + ceiling beam over the plan
          tileY(g, 'scaffold_pipe', buildX - 10, top - 20, 16 * 1.2, Hh * c + 20); tileY(g, 'scaffold_pipe', buildX + ws * c - 9, top - 20, 16 * 1.2, Hh * c + 20);
          tileX(g, 'beam_seg', buildX - 12, top - 12, ws * c + 24, 19);
          for (const xx of [buildX - 17, buildX + ws * c - 17]) spr(g, 'scaffold_clamp', xx, top - 16, 34, 29);
          // wall + cap
          const wallTop = bottom - L.wall.height * c;
          tileY(g, 'wall_seg', wallX, wallTop, wallW, L.wall.height * c);
          spv(g, 'wall_cap', wallX + wallW / 2, wallTop + 6, 1.2);
          // placed (locked) pieces in the site
          for (const p of placed) piece(g, p.color, p.cells.map(([x, y]) => [wy + x, y]), cellRect, { locked: true });
          // K-18 shadow
          if (state.shadow) {
            const sh = state.shadow; shadowCells(g, sh.color, sh.cells.map(([x, y]) => [wy + x, y]), cellRect, 'line', '#5CF59A');
            const [bx, by] = cellRect(wy + sh.cells[sh.cells.length - 1][0], sh.cells[sh.cells.length - 1][1]);
            g.save(); g.fillStyle = '#FFFFFF'; g.strokeStyle = '#0A5A12'; g.lineWidth = 4; g.beginPath(); g.arc(bx + c - 18, by + 18, 24, 0, 7); g.fill(); g.stroke(); g.restore();
            spr(g, 'icon_check', bx + c - 40, by - 4, 44, 44);
          }
          // yard pieces
          for (const p of state.yard ?? []) piece(g, p.color, p.cells, cellRect, { holdable: p.holdable });
          // lifted piece with drop path and soft shadow
          if (state.lifted) {
            const lp = state.lifted; const rs = lp.cells.map(([x, y]) => cellRect(x, y));
            const cx = rs[0][0] + c / 2; const ly = Math.max(...rs.map((r) => r[1])) + c;
            const [sx, sy] = cellRect(wy + state.shadow.cells[1][0], state.shadow.cells[1][1]);
            g.save(); g.strokeStyle = 'rgba(255,255,255,0.4)'; g.lineWidth = 6; g.setLineDash([8, 12]); g.beginPath(); g.moveTo(cx, ly + 10); g.lineTo(cx, sy - 6); g.stroke(); g.restore();
            spr(g, 'fx_drop_blob', cx - 110, ly - 4, 220, 60, { alpha: 0.7 });
            piece(g, lp.color, lp.cells, (x, y) => { const r = cellRect(x, y); return [r[0], r[1] - 18, r[2]]; }, { holdable: true, scale: 1.08 });
          }
          spv(g, 'crane_hook', yardX + 70, crane - 30, 0.9);
          // HUD (ScreenLayout v3): row 1
          nine(g, 'capsule_hud', 24, 24, 220, 60); txt(g, S.level, 134, 55, 38, 'lav');
          nine(g, 'capsule_hud', 300, 24, 250, 60); spr(g, 'icon_blocks', 276, 6, 92, 92); txt(g, String(state.blocksLeft ?? 4), 410, 53, 44, 'cream'); txt(g, S.blocks, 488, 56, 32, 'lav');
          nine(g, 'capsule_hud', 600, 24, 456, 60); txt(g, S.streak, 708, 55, 32, 'lav');
          for (let i = 0; i < 5; i++) spr(g, i < 2 ? 'bead_on' : 'bead_off', 806 + i * 48, 32, 44, 44);
          // row 2: panorama well, moves plate, pause
          nine(g, 'hud_well', 48, 128, 300, 136);
          for (let i = 0; i < rows.length; i++) for (let x = 0; x < ws; x++) { const ch = rows[i][x].toLowerCase(); const filledHere = filled.has(x + ',' + (hs - 1 - i)); spr(g, filledHere ? 'blk_' + ch + '_00' : 'ghost_' + ch, 170 + x * 24, 140 + i * 22, 22, 22); }
          txt(g, S.area, 108, 196, 26, 'lav');
          spr(g, 'icon_star', 228, 198, 44, 44); spr(g, 'icon_star_empty', 262, 198, 44, 44); spr(g, 'icon_star_empty', 296, 198, 44, 44);
          nine(g, 'hud_moves_plate', 390, 128, 300, 136); txt(g, S.moves.toLocaleUpperCase('tr-TR'), 540, 157, 30, 'card', 'center', { fill: '#F9F3C3' }); txt(g, String(state.moves ?? 8), 540, 216, 84, 'big');
          spr(g, 'btn_tool', 896, 128, 136, 136); spr(g, 'icon_pause', 924, 150, 80, 80);
          // status strip riding the tray's bottom edge
          nine(g, 'capsule_hud', 760, 1512, 240, 64); spr(g, 'truck', 770, 1508, 120, 67); txt(g, '×1', 940, 1546, 40, 'cream');
          // Tuna + Kepçe corner, boosters
          spr(g, 'chr_kepce_idle', 120, 1748, 190, 131);
          spr(g, 'chr_tuna_idle', 6, 1584, 200, 320);
          const icons = ['icon_hammer', 'icon_crane', 'icon_brush', 'icon_undo'];
          const counts = [2, 1, 0, 3];
          for (let i = 0; i < 4; i++) {
            const x = 304 + i * 192; const y = 1640;
            spr(g, counts[i] ? 'btn_booster' : 'btn_booster_off', x, y, 160, 160); spr(g, icons[i], x + 24, y + 14, 112, 112);
            if (counts[i]) { spr(g, 'badge_count', x + 112, y + 110, 68, 72); txt(g, String(counts[i]), x + 146, y + 144, 42, 'button', 'center', { stroke: '#7A0A05' }); }
            else spv(g, 'btn_plus', x + 146, y + 146, 1);
          }
        }
        const L3 = ctx.level3;
        const midState = {
          moves: 8,
          blocksLeft: 3,
          placed: [{ color: 'Y', cells: [[0, 0], [0, 1]] }],
          shadow: { color: 'W', cells: [[1, 0], [1, 1]] },
          yard: [
            { color: 'G', cells: [[2, 1], [3, 1], [2, 2], [3, 2]], holdable: true },
            { color: 'Y', cells: [[2, 0], [3, 0]], holdable: false },
          ],
          lifted: { color: 'W', cells: [[5, 3], [5, 4]] },
        };
        if (which === 'level') drawLevel(midState);
        // ---------------------------------------------------------------- home ----------------------------------------
        if (which === 'home') {
          spr(g, 'bg_home', 0, 0, W, H);
          // area structure (chapter 1) + progress
          spv(g, 'town_ch1_treehouse', 790, 1140, 0.6);
          nine(g, 'ribbon_title', 230, 236, 620, 120); txt(g, S.area, 540, 288, 54, 'button', 'center', { stroke: '#8A3A06' });
          nine(g, 'progress_track', 300, 356, 480, 84); nine(g, 'progress_fill', 308, 364, 196, 64); txt(g, S.count, 540, 396, 40, 'big');
          spr(g, 'icon_star', 740, 334, 120, 120);
          // characters group (calm middle behind the button)
          spr(g, 'chr_dede_idle', 360, 820, 300, 480);
          spr(g, 'chr_kepce_idle', 40, 1160, 300, 206);
          spr(g, 'chr_tuna_cheer', 120, 870, 330, 440);
          // top bar
          nine(g, 'capsule_home', 160, 104, 300, 88); spr(g, 'icon_heart', 126, 92, 112, 112); txt(g, '5', 182, 146, 40, 'button', 'center', { stroke: '#B80E12' }); txt(g, S.full, 340, 150, 44, 'cream');
          nine(g, 'capsule_home', 500, 104, 300, 88); spr(g, 'icon_coin', 466, 92, 112, 112); txt(g, '1.250', 660, 150, 44, 'cream'); spv(g, 'btn_plus', 790, 148, 1);
          spr(g, 'btn_tool', 920, 96, 112, 112); spr(g, 'icon_settings', 936, 110, 80, 80);
          // level button (L, alttan 336)
          nine(g, 'btn_primary', 240, 1384, 600, 200); txt(g, S.levelNext, 540, 1470, 88, 'button');
          // nav
          nine(g, 'nav_bar', 0, 1704, W, 216);
          const tabs = ['icon_nav_shop', 'icon_nav_league', 'icon_nav_home', 'icon_nav_team', 'icon_nav_album'];
          for (let i = 0; i < 5; i++) {
            const cx = 108 + i * 216;
            if (i === 2) { spr(g, 'nav_tab_on', cx - 150, 1620, 300, 300); spr(g, tabs[i], cx - 67, 1650, 134, 134); txt(g, S.home, cx, 1830, 40, 'label', 'center', { stroke: '#2C1E94' }); }
            else { spr(g, tabs[i], cx - 57, 1752, 115, 115, { alpha: i > 2 ? 0.55 : 1 }); if (i > 2) spr(g, 'icon_lock', cx + 14, 1812, 52, 52); }
            if (i > 0 && i < 5 && i !== 2 && i !== 3) { g.fillStyle = '#3624AA'; g.fillRect(cx - 109, 1780, 3, 80); }
          }
        }
        // ---------------------------------------------------------------- win -----------------------------------------
        if (which === 'win') {
          drawLevel({ moves: 3, blocksLeft: 0, placed: [{ color: 'Y', cells: [[0, 0], [0, 1]] }, { color: 'W', cells: [[1, 0], [1, 1]] }, { color: 'G', cells: [[0, 2], [1, 2], [0, 3], [1, 3]] }, { color: 'Y', cells: [[0, 4], [1, 4]] }], yard: [] });
          g.fillStyle = 'rgba(5,3,13,0.85)'; g.fillRect(0, 0, W, H);
          spr(g, 'fx_rays', 540 - 620, 700 - 620, 1240, 1240, { tint: '#FFE7A3', alpha: 0.35, rot: 8 });
          spr(g, 'fx_glow', 540 - 360, 700 - 360, 720, 720, { tint: '#FFE7A3', alpha: 0.5 });
          // panel + ribbon title + stars
          nine(g, 'panel_window', 40, 640, 1000, 984);
          nine(g, 'ribbon_title', 170, 548, 740, 190); txt(g, S.winTitle, 540, 622, 92, 'button', 'center', { stroke: '#8A3A06' });
          spr(g, 'icon_star', 186, 700, 200, 200, { rot: -12 }); spr(g, 'icon_star', 694, 700, 200, 200, { rot: 12 }); spr(g, 'icon_star', 410, 660, 260, 260);
          for (const [x, y, s, r] of [[150, 690, 48, 0], [930, 700, 56, 20], [300, 640, 36, 0], [800, 640, 40, 0]]) spr(g, 'fx_spark', x, y, s, s, { tint: '#FFF3C4', rot: r });
          txt(g, S.winClear, 540, 960, 56, 'label', 'center', { stroke: '#2A1D8B' });
          nine(g, 'panel_card', 100, 1010, 880, 280);
          spr(g, 'fx_rays', 200 - 260, 1150 - 260, 520, 520, { tint: '#F1E5BD', alpha: 0.6 });
          spr(g, 'chr_tuna_cheer', 92, 900, 300, 400);
          nine(g, 'capsule_hud', 470, 1050, 300, 96); spr(g, 'icon_coin', 446, 1036, 124, 124); txt(g, '+61', 680, 1100, 56, 'cream');
          nine(g, 'capsule_hud', 470, 1166, 300, 96); spr(g, 'icon_gold_trowel', 446, 1150, 124, 124); txt(g, '×1', 680, 1216, 56, 'cream');
          txt(g, S.winBonus, 600, 1316, 36, 'label', 'center', { stroke: '#2A1D8B', fill: '#FFE96A' });
          nine(g, 'btn_primary', 260, 1360, 560, 212); txt(g, S.cont, 540, 1452, 96, 'button');
          const cols = ['#CF404E', '#FFE23E', '#44D088', '#2984DE', '#EC8D20', '#5D3AAE'];
          let s = 3; const rnd = () => ((s = (s * 16807) % 2147483647) / 2147483647);
          for (let i = 0; i < 46; i++) { const x = rnd() * W, y = 300 + rnd() * 520; if (y > 560 && x > 150 && x < 930) continue; spr(g, i % 3 ? 'fx_confetti' : 'fx_confetti_round', x, y, i % 3 ? 30 : 18, i % 3 ? 15 : 18, { tint: cols[i % 6], rot: rnd() * 360 }); }
        }
        // ---------------------------------------------------------------- sheet ---------------------------------------
        if (which === 'sheet') {
          const names = Object.keys(window.__atlas.frames).sort();
          g.fillStyle = '#2B2661'; g.fillRect(0, 0, cv.width, cv.height);
          const cell = 112, lab = 16, cols = Math.floor(cv.width / cell);
          names.forEach((n, i) => {
            const x = (i % cols) * cell, y = Math.floor(i / cols) * (cell + lab);
            g.fillStyle = (Math.floor(i / cols) + i) % 2 ? '#352E73' : '#3D357F'; g.fillRect(x, y, cell, cell + lab);
            const f = fr(n); const dw = f.w / f.scale, dh = f.h / f.scale; const k = Math.min((cell - 8) / dw, (cell - 8) / dh, 1.2);
            spr(g, n, x + (cell - dw * k) / 2, y + (cell - dh * k) / 2, dw * k, dh * k);
            g.font = '600 10px sans-serif'; g.fillStyle = '#E8E4FF'; g.textAlign = 'center'; g.fillText(n.length > 20 ? n.slice(0, 19) + '…' : n, x + cell / 2, y + cell + 11);
          });
          const used = Math.ceil(names.length / cols) * (cell + lab);
          const c2 = document.createElement('canvas'); c2.width = cv.width; c2.height = used; c2.getContext('2d').drawImage(cv, 0, 0);
          return c2.toDataURL('image/png').split(',')[1];
        }
        return cv.toDataURL('image/png').split(',')[1];
      },
      { which, ctx },
    );
    const file = join(OUT, `v4-${which}.png`);
    writeFileSync(file, Buffer.from(b64, 'base64'));
    shots[which] = file;
    console.log('preview:', file);
  }
  await browser.close();
}
await main();
