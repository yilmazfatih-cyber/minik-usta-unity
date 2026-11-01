// v4 blocks, plan ghosts, K-18 drop shadows (ART §2.1 palette — D-083 unchanged; ART §3A v2 "şeker blok" + §3A.5 stud;
// ART_REFERENCE §5.2 v3 deltas: outline 4 px × 0,50, lip 20 × 0,74, top bevel band 0,20 h × 1,28, gloss α 0,30,
// stud crescents). A piece is drawn as ONE polyomino (single outline, single side wall, pillows with grooves); the atlas
// keeps per-cell tiles: `blk_<c>_<mask>` = the centre cell of its 3 × 3 neighbourhood (mask bits 1 left, 2 right,
// 4 down, 8 up — Presentation.Core `Atlas.Body` / `BoardPose.NeighbourMask`) drawn with NO diagonal neighbours (every
// inner corner concave); where the diagonal cell IS part of the piece the opaque corner-fill patch
// `blk_<c>_<mask>_cf_<tl|tr|bl|br>` is laid on top (exact: concave corners are sharp, so all their geometry lies in the
// corner cell's 32 px box). Drop-shadow lines are the other way round (diagonals present, `_cc_` patch adds the corner).
import { mul, mix, lstar, rr, lin, uid, doc, r1, circlePath, arcPath } from './lib.mjs';

export const C = 120;
export const B = {
  inset: 4, outline: 4, outlineF: 0.5, lip: 20, darkF: 0.74, grooveF: 0.88, R: 24, r: 0,
  pShared: 7, pOpen: 14, pR: 19, lightMix: 0.32, gradStop: 0.3, bottomF: 0.92, bevelH: 0.2, bevelL: 1.28,
  shadeH: 0.18, shadeA: 0.28, gloss: [0.34, 0.13, 0.18, 0.06], glossRot: -8, glossA: 0.3,
  studRatio: 0.54, studSide: 6, studMargin: 6, studHiPx: 6, studHiL: 1.25, studHiA: 0.7, studShPx: 8, studShF: 0.8,
  symRatio: 0.42, symMaxRatio: 0.82,
};
export const COLORS = { W: '#764012', Y: '#FFE23E', G: '#44D088', R: '#CF404E', O: '#EC8D20', C: '#C8CFDA', B: '#2984DE', P: '#5D3AAE' };
export const PLAN_INK = { W: '#FFFFFF', Y: '#14233D', G: '#14233D', R: '#FFFFFF', O: '#14233D', C: '#14233D', B: '#14233D', P: '#FFFFFF' };
export const PLAN_UNDERLAY = '#BCCADD';

export function tones(base) {
  return {
    base,
    light: mix(base, '#FFFFFF', B.lightMix),
    bottom: mul(base, B.bottomF),
    groove: mul(base, B.grooveF),
    dark: mul(base, B.darkF),
    outline: mul(base, B.outlineF),
    bevel: mul(base, B.bevelL),
    studHi: mul(base, B.studHiL),
    studSh: mul(base, B.studShF),
    darkInk: lstar(base) >= 60,
    ink: lstar(base) >= 60 ? mul(base, 0.4) : '#FFFFFF',
  };
}

// ---- polyomino outline ------------------------------------------------------------------------------------------------
const key = (x, y) => x + ',' + y;
/** Boundary loops of a cell set (y down), clockwise (interior on the right); corner vertices with in/out directions. */
export function loops(cells) {
  const set = new Set(cells.map(([x, y]) => key(x, y)));
  const has = (x, y) => set.has(key(x, y));
  const edges = [];
  for (const [x, y] of cells) {
    if (!has(x, y - 1)) edges.push({ a: [x, y], b: [x + 1, y] });
    if (!has(x + 1, y)) edges.push({ a: [x + 1, y], b: [x + 1, y + 1] });
    if (!has(x, y + 1)) edges.push({ a: [x + 1, y + 1], b: [x, y + 1] });
    if (!has(x - 1, y)) edges.push({ a: [x, y + 1], b: [x, y] });
  }
  const byStart = new Map();
  for (const e of edges) {
    const k = key(...e.a);
    if (!byStart.has(k)) byStart.set(k, []);
    byStart.get(k).push(e);
  }
  const dir = (e) => [e.b[0] - e.a[0], e.b[1] - e.a[1]];
  const used = new Set();
  const out = [];
  for (const e0 of edges) {
    if (used.has(e0)) continue;
    const seq = [];
    let e = e0;
    while (e && !used.has(e)) {
      used.add(e);
      seq.push(e);
      const d = dir(e);
      const cands = (byStart.get(key(...e.b)) ?? []).filter((x) => !used.has(x));
      // prefer the right turn (keeps diagonal-touching loops apart)
      cands.sort((p, q) => {
        const cp = d[0] * dir(p)[1] - d[1] * dir(p)[0];
        const cq = d[0] * dir(q)[1] - d[1] * dir(q)[0];
        return cq - cp;
      });
      e = cands[0];
    }
    // corners only (merge collinear)
    const verts = [];
    for (let i = 0; i < seq.length; i++) {
      const pe = seq[(i - 1 + seq.length) % seq.length];
      const ce = seq[i];
      const d1 = dir(pe);
      const d2 = dir(ce);
      if (d1[0] !== d2[0] || d1[1] !== d2[1]) verts.push({ p: ce.a, d1, d2 });
    }
    out.push(verts);
  }
  return out;
}
const rightNormal = ([dx, dy]) => [-dy, dx];

/**
 * Rounded, inward-offset path of the loops. off(n) = offset of an edge with inward normal n (px). Convex corners
 * radius R, concave radius r (both already offset-adjusted by the caller).
 */
export function offsetPath(lps, offFn, R, r, cell = C, ox = 0, oy = 0) {
  let d = '';
  for (const verts of lps) {
    const pts = verts.map((v) => {
      const n1 = rightNormal(v.d1);
      const n2 = rightNormal(v.d2);
      const o1 = offFn(n1);
      const o2 = offFn(n2);
      return {
        x: ox + v.p[0] * cell + n1[0] * o1 + n2[0] * o2,
        y: oy + v.p[1] * cell + n1[1] * o1 + n2[1] * o2,
        d1: v.d1,
        d2: v.d2,
        convex: v.d1[0] * v.d2[1] - v.d1[1] * v.d2[0] > 0,
      };
    });
    const n = pts.length;
    const rad = pts.map((p, i) => {
      const a = pts[(i - 1 + n) % n];
      const b = pts[(i + 1) % n];
      const lin = Math.hypot(p.x - a.x, p.y - a.y);
      const lout = Math.hypot(b.x - p.x, b.y - p.y);
      return Math.max(0, Math.min(p.convex ? R : r, lin / 2, lout / 2));
    });
    const start = (i) => [pts[i].x + pts[i].d2[0] * rad[i], pts[i].y + pts[i].d2[1] * rad[i]];
    const s0 = start(0);
    d += `M${r1(s0[0])} ${r1(s0[1])}`;
    for (let k = 1; k <= n; k++) {
      const i = k % n;
      const p = pts[i];
      const ra = rad[i];
      d += `L${r1(p.x - p.d1[0] * ra)} ${r1(p.y - p.d1[1] * ra)}`;
      if (ra > 0) d += `A${r1(ra)} ${r1(ra)} 0 0 ${p.convex ? 1 : 0} ${r1(p.x + p.d2[0] * ra)} ${r1(p.y + p.d2[1] * ra)}`;
    }
    d += 'Z';
  }
  return d;
}

// ---- symbols (ART §3.1, 100 × 100 unit box) ---------------------------------------------------------------------------
export function symbol(code, ink, carve, opacity = 1, noCarve = false) {
  const o = opacity === 1 ? '' : ` opacity="${opacity}"`;
  const st = (w) => `fill="none" stroke="${ink}" stroke-width="${w}" stroke-linecap="round" stroke-linejoin="round"`;
  switch (code) {
    case 'W':
      return `<g${o}><path d="M10 24C30 14 50 34 90 22" ${st(9)}/><path d="M10 50C22 42 34 58 48 50" ${st(9)}/><ellipse cx="70" cy="51" rx="11" ry="7" ${st(7)}/><path d="M10 76C30 66 50 86 90 74" ${st(9)}/></g>`;
    case 'Y':
      return `<g fill="${ink}"${o}><circle cx="50" cy="28" r="13"/><circle cx="27" cy="70" r="13"/><circle cx="73" cy="70" r="13"/></g>`;
    case 'G':
      return `<g${o}><path d="M18 82C18 40 46 14 86 14C86 54 60 82 18 82Z" fill="${ink}"/>${noCarve ? '' : `<path d="M24 76L68 32" stroke="${carve}" stroke-width="7" stroke-linecap="round"/>`}</g>`;
    case 'R':
      return `<g${o}><rect x="10" y="16" width="80" height="68" rx="10" ${st(8)}/><path d="M10 50H90M50 16V50M30 50V84M70 50V84" ${st(8)}/></g>`;
    case 'O':
      return `<g${o}><path d="M8 44Q22 18 36 44Q50 18 64 44Q78 18 92 44" ${st(9)}/><path d="M8 80Q22 54 36 80Q50 54 64 80Q78 54 92 80" ${st(9)}/></g>`;
    case 'C':
      return `<g${o}><path d="M18 50L50 18M18 82L82 18M50 82L82 50M18 50L50 82M18 18L82 82M50 18L82 50" ${st(8)}/></g>`;
    case 'B':
      return `<g fill="${ink}"${o}><path d="M46 8C50 36 58 44 86 48C58 52 50 60 46 88C42 60 34 52 6 48C34 44 42 36 46 8Z"/><path d="M82 8C83 16 85 18 93 19C85 20 83 22 82 30C81 22 79 20 71 19C79 18 81 16 82 8Z"/></g>`;
    case 'P':
      return `<g${o}><path d="M50 8L90 42L50 92L10 42Z" fill="${ink}" stroke="${ink}" stroke-width="2" stroke-linejoin="round"/>${noCarve ? '' : `<path d="M10 42H90M30 42L50 8L70 42L50 92" stroke="${carve}" stroke-width="6" fill="none" stroke-linejoin="round"/>`}</g>`;
  }
  return '';
}
/** Symbol with the ART §3A.2 layer-7 emboss, centred on (cx, cy), box s. */
export function embossedSymbol(code, t, cx, cy, s) {
  const k = s / 100;
  const at = (dy) => `transform="translate(${r1(cx - s / 2)} ${r1(cy - s / 2 + dy)}) scale(${Math.round(k * 10000) / 10000})"`;
  const emb = t.darkInk
    ? `<g ${at(3)}>${symbol(code, '#FFFFFF', '#FFFFFF', 0.45, true)}</g>`
    : `<g ${at(4)}>${symbol(code, t.outline, t.outline, 0.55, true)}</g>`;
  const main = `<g ${at(0)}>${symbol(code, t.ink, t.base, t.darkInk ? 1 : 0.92)}</g>`;
  return emb + main;
}

// ---- one cell inside a polyomino --------------------------------------------------------------------------------------
/** Pillow rect of cell (x, y) of a cell set (px, origin of the cell set). */
export function pillowRect(has, x, y, cell = C) {
  const L = has(x - 1, y) ? B.pShared : B.pOpen;
  const Rr = has(x + 1, y) ? B.pShared : B.pOpen;
  const U = has(x, y - 1) ? B.pShared : B.pOpen;
  const D = has(x, y + 1) ? B.pShared : B.pOpen + B.lip;
  return { x: x * cell + L, y: y * cell + U, w: cell - L - Rr, h: cell - U - D };
}
export function studOf(P) {
  const d = Math.min(B.studRatio * C, P.w - 2 * B.studMargin, P.h - 2 * B.studMargin - B.studSide);
  return { d, cx: P.x + P.w / 2, cy: P.y + (P.h - B.studSide) / 2 };
}

/** Polyomino body layers (outline, side wall, face) of cell set `cells` — paths in px. */
export function bodyPaths(cells) {
  const lp = loops(cells);
  const S = offsetPath(lp, () => B.inset, B.R, B.r);
  // concave corners stay sharp (r 0): the per-cell tiles + the corner patch then reproduce the piece exactly
  const Bp = offsetPath(lp, () => B.inset + B.outline, B.R - B.outline, B.r);
  const F = offsetPath(lp, (n) => B.inset + B.outline + (n[1] === -1 ? B.lip : 0), B.R - B.outline, B.r);
  return { S, B: Bp, F };
}

function pillowMarkup(t, P, ids) {
  const g = ids.grad;
  const clip = uid('pc');
  const pr = Math.min(B.pR, P.w / 2, P.h / 2);
  const pd = rr(P.x, P.y, P.w, P.h, pr);
  let o = `<clipPath id="${clip}"><path d="${pd}"/></clipPath>`;
  o += `<path d="${pd}" fill="url(#${g})"/>`;
  o += `<g clip-path="url(#${clip})">`;
  o += `<rect x="${r1(P.x)}" y="${r1(P.y)}" width="${r1(P.w)}" height="${r1(P.h * B.bevelH)}" fill="url(#${ids.bevel})"/>`;
  o += `<rect x="${r1(P.x)}" y="${r1(P.y + P.h * (1 - B.shadeH))}" width="${r1(P.w)}" height="${r1(P.h * B.shadeH + 1)}" fill="url(#${ids.shade})"/>`;
  o += `<ellipse cx="${r1(P.x + P.w * B.gloss[0])}" cy="${r1(P.y + P.h * B.gloss[1])}" rx="${r1(P.w * B.gloss[2])}" ry="${r1(Math.max(3, P.h * B.gloss[3]))}" transform="rotate(${B.glossRot} ${r1(P.x + P.w * B.gloss[0])} ${r1(P.y + P.h * B.gloss[1])})" fill="#FFFFFF" fill-opacity="${B.glossA}"/>`;
  o += `</g>`;
  return o;
}
export function studMarkup(t, st) {
  const r = st.d / 2;
  let o = '';
  o += `<circle cx="${r1(st.cx)}" cy="${r1(st.cy + B.studSide)}" r="${r1(r)}" fill="${t.dark}"/>`;
  o += `<circle cx="${r1(st.cx)}" cy="${r1(st.cy)}" r="${r1(r)}" fill="${t.base}"/>`;
  // v3 crescents: upper-left highlight, lower shade (inside the top face)
  o += `<path d="${arcPath(st.cx, st.cy, r - B.studShPx / 2 - 0.5, 25, 155)}" fill="none" stroke="${t.studSh}" stroke-width="${B.studShPx}" stroke-linecap="round"/>`;
  o += `<path d="${arcPath(st.cx, st.cy, r - B.studHiPx / 2 - 0.5, 195, 285)}" fill="none" stroke="${t.studHi}" stroke-opacity="${B.studHiA}" stroke-width="${B.studHiPx}" stroke-linecap="round"/>`;
  o += `<circle cx="${r1(st.cx)}" cy="${r1(st.cy)}" r="${r1(r - 1)}" fill="none" stroke="${t.outline}" stroke-opacity="0.45" stroke-width="2"/>`;
  return o;
}
function toneDefs(t) {
  const ids = { grad: uid('pg'), bevel: uid('pb'), shade: uid('ps') };
  const defs =
    lin(ids.grad, [[0, t.light], [B.gradStop, t.base], [1, t.bottom]]) +
    lin(ids.bevel, [[0, t.bevel, 1], [1, t.bevel, 0]]) +
    lin(ids.shade, [[0, t.dark, 0], [1, t.dark, B.shadeA]]);
  return { ids, defs };
}

const DIRS = { 1: [-1, 0], 2: [1, 0], 4: [0, 1], 8: [0, -1] };
/** 3 × 3 neighbourhood of a mask (centre (1,1)); with `diag`, diagonals where both orthogonals are present. */
export function neighbourhood(mask, diag = true) {
  const cells = [[1, 1]];
  for (const bit of [1, 2, 4, 8]) if (mask & bit) cells.push([1 + DIRS[bit][0], 1 + DIRS[bit][1]]);
  if (!diag) return cells;
  const L = mask & 1, Rt = mask & 2, D = mask & 4, U = mask & 8;
  if (U && L) cells.push([0, 0]);
  if (U && Rt) cells.push([2, 0]);
  if (D && L) cells.push([0, 2]);
  if (D && Rt) cells.push([2, 2]);
  return cells;
}

export function blockTile(code, mask) {
  const t = tones(COLORS[code]);
  const cells = neighbourhood(mask, false);
  const set = new Set(cells.map(([x, y]) => key(x, y)));
  const has = (x, y) => set.has(key(x, y));
  const { S, B: Bp, F } = bodyPaths(cells);
  const { ids, defs } = toneDefs(t);
  const P = pillowRect(has, 1, 1);
  const st = studOf(P);
  const sym = Math.min(B.symRatio * C, B.symMaxRatio * st.d);
  let body = `<path d="${S}" fill="${t.outline}"/><path d="${Bp}" fill="${t.dark}"/><path d="${F}" fill="${t.groove}"/>`;
  body += pillowMarkup(t, P, ids);
  body += studMarkup(t, st);
  body += embossedSymbol(code, t, st.cx, st.cy, sym);
  return doc(C, C, defs, body, {
    viewBox: `${C} ${C} ${C} ${C}`,
    scale: 1.5,
    pivot: [0.5, 0.5],
    anchor: [(st.cx - C) / C, 1 - (st.cy - C) / C],
    stud: st.d,
    sym,
  });
}

/**
 * Corner-fill overlay of the centre cell with neighbour mask `mask` at corner tl / tr / bl / br (both orthogonal
 * neighbours of that corner present AND the diagonal cell part of the piece): the exact render of that cell with the
 * diagonal, cropped to a 32 px corner box — it covers the concave corner of the base tile. Pivot = the cell corner.
 */
export const CC_BOX = 32;
export function cornerFillTile(code, corner, mask) {
  const t = tones(COLORS[code]);
  const dx = corner.includes('l') ? -1 : 1;
  const dy = corner.includes('t') ? -1 : 1;
  const cells = neighbourhood(mask, false).concat([[1 + dx, 1 + dy]]);
  const set = new Set(cells.map(([x, y]) => key(x, y)));
  const has = (x, y) => set.has(key(x, y));
  const { S, B: Bp, F } = bodyPaths(cells);
  const { ids, defs } = toneDefs(t);
  const x0 = dx < 0 ? C : 2 * C - CC_BOX;
  const y0 = dy < 0 ? C : 2 * C - CC_BOX;
  const P = pillowRect(has, 1, 1);
  let body = `<path d="${S}" fill="${t.outline}"/><path d="${Bp}" fill="${t.dark}"/><path d="${F}" fill="${t.groove}"/>`;
  body += pillowMarkup(t, P, ids);
  return doc(CC_BOX, CC_BOX, defs, body, {
    viewBox: `${x0} ${y0} ${CC_BOX} ${CC_BOX}`,
    scale: 1.5,
    pivot: [dx < 0 ? 0 : 1, dy < 0 ? 1 : 0],
  });
}
/** Drop-shadow line patch (concave corner, diagonal missing) — white, additive. */
export function shadowConcaveTile(kind, corner, mask) {
  const dx = corner.includes('l') ? -1 : 1;
  const dy = corner.includes('t') ? -1 : 1;
  const cells = neighbourhood(mask, true).filter(([x, y]) => !(x === 1 + dx && y === 1 + dy));
  const lp = loops(cells);
  const x0 = dx < 0 ? C : 2 * C - CC_BOX;
  const y0 = dy < 0 ? C : 2 * C - CC_BOX;
  let body;
  if (kind === 'line') {
    const p = offsetPath(lp, () => 7, B.R - 3, 0);
    body = `<path d="${p}" fill="none" stroke="#FFFFFF" stroke-opacity="0.12" stroke-width="14"/><path d="${p}" fill="none" stroke="#FFFFFF" stroke-opacity="0.2" stroke-width="10"/><path d="${p}" fill="none" stroke="#FFFFFF" stroke-width="6"/>`;
  } else {
    const p = offsetPath(lp, () => 8, B.R - 4, 0);
    body = `<path d="${p}" fill="none" stroke="#FFFFFF" stroke-width="7" stroke-linecap="square"/>`;
  }
  const clip = uid('sc');
  const defs = `<clipPath id="${clip}"><rect x="${x0}" y="${y0}" width="${CC_BOX / 2}" height="${CC_BOX / 2}" transform="translate(${dx < 0 ? 0 : CC_BOX / 2} ${dy < 0 ? 0 : CC_BOX / 2})"/></clipPath>`;
  return doc(CC_BOX, CC_BOX, defs, `<g clip-path="url(#${clip})">${body}</g>`, {
    viewBox: `${x0} ${y0} ${CC_BOX} ${CC_BOX}`,
    scale: 1,
    pivot: [dx < 0 ? 0 : 1, dy < 0 ? 1 : 0],
  });
}
/** Masks that can have a concave corner at `corner`. */
export function concaveMasks(corner) {
  const need = (corner.includes('l') ? 1 : 2) | (corner.includes('t') ? 8 : 4);
  return Array.from({ length: 16 }, (_, m) => m).filter((m) => (m & need) === need);
}

/** Holdable stud gloss (ART §3A.5 6e/6f), white, for a nominal stud of 64 px; scale by studD / 64. */
export function studGloss() {
  const d = 64;
  const c = 36;
  const body = `<path d="${arcPath(c, c, 0.42 * d, 205, 285)}" fill="none" stroke="#FFFFFF" stroke-opacity="0.5" stroke-width="5" stroke-linecap="round"/><circle cx="${r1(c + Math.cos((225 * Math.PI) / 180) * 0.3 * d)}" cy="${r1(c + Math.sin((225 * Math.PI) / 180) * 0.3 * d)}" r="4" fill="#FFFFFF" fill-opacity="0.8"/>`;
  return doc(72, 72, '', body, { scale: 1.5, pivot: [0.5, 0.5] });
}
/** Colour-blind mode: a clean stud cap (covers the baked symbol) for a nominal 64 px stud. */
export function studCap(code) {
  const t = tones(COLORS[code]);
  const st = { cx: 36, cy: 36, d: 64 };
  const body = studMarkup(t, st).split('\n').join('').replace(/^<circle[^>]*\/>/, '');
  return doc(72, 72, '', body, { scale: 1.5, pivot: [0.5, 0.5] });
}
/** Stand-alone embossed symbol, nominal box 50 in a 64 frame (colour-blind ×1.2, legends, goal chips). */
export function symbolTile(code) {
  const t = tones(COLORS[code]);
  return doc(64, 64, '', embossedSymbol(code, t, 32, 30, 50), { scale: 1.5, pivot: [0.5, 0.5 + 2 / 64] });
}
/** Placed (locked) overlay: 4 mortar dots at the cell corners (ART §4 "Yerleşmiş blok"). */
export function lockedOverlay() {
  let body = '';
  for (const [x, y] of [[13, 13], [107, 13], [13, 104], [107, 104]]) {
    body += `<ellipse cx="${x}" cy="${y + 1.5}" rx="5.5" ry="4.5" fill="#3B2A1A" fill-opacity="0.25"/><ellipse cx="${x}" cy="${y}" rx="5" ry="4" fill="#FFFFFF" fill-opacity="0.6"/><ellipse cx="${x - 1.5}" cy="${y - 1.2}" rx="2" ry="1.3" fill="#FFFFFF" fill-opacity="0.8"/>`;
  }
  return doc(C, C, '', body, { scale: 1.5, pivot: [0.5, 0.5] });
}

// ---- plan ghost (ART §4) ----------------------------------------------------------------------------------------------
export const planFill = (code) => mix(PLAN_UNDERLAY, COLORS[code], 0.8);
export function ghostTile(code, front = false) {
  let fill = planFill(code);
  if (front) fill = mix(fill, '#FFFFFF', 0.15);
  const stroke = mul(fill, 0.65);
  const d = rr(8, 8, 104, 104, 17);
  let body = `<path d="${d}" fill="${fill}"/>`;
  // soft paper sheen (flat look kept: ≤ 6 % white top band)
  body += `<path d="${rr(10, 10, 100, 34, 15)}" fill="#FFFFFF" fill-opacity="0.06"/>`;
  body += `<path d="${rr(10, 10, 100, 100, 15)}" fill="none" stroke="${stroke}" stroke-width="4" stroke-dasharray="14 10" stroke-linecap="round"/>`;
  body += `<g transform="translate(35 35) scale(0.5)">${symbol(code, PLAN_INK[code], fill, 1)}</g>`;
  return doc(C, C, '', body, { scale: 1.5, pivot: [0.5, 0.5] });
}
export function ghostFrontLine() {
  const d = rr(10, 10, 100, 100, 15);
  let body = '';
  for (const [w, a] of [[16, 0.08], [12, 0.12], [9, 0.15]]) body += `<path d="${d}" fill="none" stroke="#FFFFFF" stroke-opacity="${a}" stroke-width="${w}"/>`;
  body += `<path d="${d}" fill="none" stroke="#FFFFFF" stroke-width="6"/>`;
  return doc(C, C, '', body, { scale: 1.5, pivot: [0.5, 0.5] });
}
export function ghostHidden() {
  const d = rr(10, 10, 100, 100, 15);
  let body = `<path d="${rr(8, 8, 104, 104, 17)}" fill="#FFFFFF" fill-opacity="0.10"/><path d="${d}" fill="none" stroke="#FFFFFF" stroke-opacity="0.6" stroke-width="4" stroke-dasharray="14 10" stroke-linecap="round"/>`;
  body += `<g transform="rotate(-4 60 60)"><path d="${rr(28, 30, 64, 64, 10)}" fill="#C99A52" fill-opacity="0.5" transform="translate(0 3)"/><path d="${rr(28, 28, 64, 64, 10)}" fill="#FFF4D6" stroke="#E2BD80" stroke-width="2"/><circle cx="60" cy="36" r="3.5" fill="#E2BD80"/>`;
  body += `<path d="M48 54Q48 42 60 42Q72 42 72 53Q72 60 64 64Q60 66 60 72" fill="none" stroke="#3B2A1A" stroke-width="8" stroke-linecap="round" stroke-linejoin="round"/><circle cx="60" cy="83" r="5" fill="#3B2A1A"/></g>`;
  return doc(C, C, '', body, { scale: 1.5, pivot: [0.5, 0.5] });
}

// ---- K-18 drop shadow (UX §5.4): per-mask silhouettes, white (tinted at runtime) -------------------------------------
export function shadowTile(kind, mask) {
  const cells = neighbourhood(mask);
  const lp = loops(cells);
  let body = '';
  if (kind === 'fill') {
    body = `<path d="${offsetPath(lp, () => B.inset, B.R, B.r)}" fill="#FFFFFF"/>`;
  } else if (kind === 'line') {
    const p = offsetPath(lp, () => 7, B.R - 3, 0);
    body = `<path d="${p}" fill="none" stroke="#FFFFFF" stroke-opacity="0.12" stroke-width="14"/><path d="${p}" fill="none" stroke="#FFFFFF" stroke-opacity="0.2" stroke-width="10"/><path d="${p}" fill="none" stroke="#FFFFFF" stroke-width="6"/>`;
  } else {
    const p = offsetPath(lp, () => 8, B.R - 4, 0);
    body = `<path d="${p}" fill="none" stroke="#FFFFFF" stroke-width="7" stroke-dasharray="16 10" stroke-linecap="round"/>`;
  }
  return doc(C, C, '', body, { viewBox: `${C} ${C} ${C} ${C}`, scale: 1, pivot: [0.5, 0.5] });
}

export const MASK_IDS = Array.from({ length: 16 }, (_, i) => i);
export const pad2 = (n) => String(n).padStart(2, '0');

/** A whole piece (reference render, decorations): cells [[x, y], …] (y down), cell px `cell` (shapes scale with c/120). */
export function pieceMarkup(code, cells, cell = C, ox = 0, oy = 0) {
  const k = cell / C;
  const t = tones(COLORS[code]);
  const set = new Set(cells.map(([x, y]) => key(x, y)));
  const has = (x, y) => set.has(key(x, y));
  const { S, B: Bp, F } = bodyPaths(cells);
  const { ids, defs } = toneDefs(t);
  let o = `<g transform="translate(${r1(ox)} ${r1(oy)}) scale(${k})"><defs>${defs}</defs><path d="${S}" fill="${t.outline}"/><path d="${Bp}" fill="${t.dark}"/><path d="${F}" fill="${t.groove}"/>`;
  for (const [x, y] of cells) {
    const P = pillowRect(has, x, y);
    const st = studOf(P);
    o += pillowMarkup(t, P, ids) + studMarkup(t, st);
    o += embossedSymbol(code, t, st.cx, st.cy, Math.min(B.symRatio * C, B.symMaxRatio * st.d));
    o += `<path d="${arcPath(st.cx, st.cy, 0.42 * st.d, 205, 285)}" fill="none" stroke="#FFFFFF" stroke-opacity="0.5" stroke-width="5" stroke-linecap="round"/>`;
  }
  return o + '</g>';
}
