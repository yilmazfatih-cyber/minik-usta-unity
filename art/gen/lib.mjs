// Shared helpers of the v4 art generator (design-lead; ART_REFERENCE v1.1 §2–§8). Pure string builders: every function
// returns SVG markup in DESIGN px (1080-wide canvas); tools/art/rasterize.mjs multiplies by the frame scale.
// No `filter`, `mask`, `image` (ART_REFERENCE §6.2, R2-08): blur and glow are stacked translucent copies.

export const r1 = (v) => Math.round(v * 10) / 10;

// ---- colour ----------------------------------------------------------------------------------------------------------
export function rgb(hex) {
  const h = hex.replace('#', '');
  return [parseInt(h.slice(0, 2), 16), parseInt(h.slice(2, 4), 16), parseInt(h.slice(4, 6), 16)];
}
export function hex([r, g, b]) {
  const c = (v) => Math.max(0, Math.min(255, Math.round(v))).toString(16).padStart(2, '0').toUpperCase();
  return '#' + c(r) + c(g) + c(b);
}
/** base × f (sRGB multiply; ART §2.5 tone formulas). f > 1 brightens (clamped). */
export const mul = (h, f) => hex(rgb(h).map((v) => v * f));
/** a·(1 − t) + b·t */
export const mix = (a, b, t) => {
  const A = rgb(a);
  const B = rgb(b);
  return hex(A.map((v, i) => v * (1 - t) + B[i] * t));
};
/** CIELAB L* of a hex (D65, sRGB). */
export function lstar(h) {
  const lin = rgb(h).map((v) => {
    const c = v / 255;
    return c <= 0.04045 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
  });
  const y = 0.2126 * lin[0] + 0.7152 * lin[1] + 0.0722 * lin[2];
  return y > 216 / 24389 ? 116 * Math.cbrt(y) - 16 : (24389 / 27) * y;
}

// ---- ids --------------------------------------------------------------------------------------------------------------
let idCounter = 0;
export const uid = (p = 'g') => `${p}${(idCounter++).toString(36)}`;
export const resetIds = () => {
  idCounter = 0;
};

// ---- gradients --------------------------------------------------------------------------------------------------------
/** stops: [[offset, colour, opacity?], …] */
export function lin(id, stops, x1 = 0, y1 = 0, x2 = 0, y2 = 1, units = '') {
  const u = units ? ` gradientUnits="${units}"` : '';
  return `<linearGradient id="${id}" x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}"${u}>${stopsOf(stops)}</linearGradient>`;
}
export function rad(id, stops, cx = 0.5, cy = 0.5, r = 0.5, extra = '') {
  return `<radialGradient id="${id}" cx="${cx}" cy="${cy}" r="${r}"${extra}>${stopsOf(stops)}</radialGradient>`;
}
function stopsOf(stops) {
  return stops
    .map(([o, c, a]) => `<stop offset="${o}" stop-color="${c}"${a === undefined || a === 1 ? '' : ` stop-opacity="${a}"`}/>`)
    .join('');
}
/** ART_REFERENCE §6.2 rule 1: 4-stop volume gradient of a signature colour. */
export function vol(id, c, cx = 0.38, cy = 0.32, r = 0.78) {
  return rad(id, [[0, mix(c, '#FFFFFF', 0.35)], [0.45, c], [0.85, mul(c, 0.78)], [1, mul(c, 0.62)]], cx, cy, r);
}

// ---- shapes -----------------------------------------------------------------------------------------------------------
export function rr(x, y, w, h, r) {
  r = Math.max(0, Math.min(r, w / 2, h / 2));
  return `M${r1(x + r)} ${r1(y)}H${r1(x + w - r)}A${r1(r)} ${r1(r)} 0 0 1 ${r1(x + w)} ${r1(y + r)}V${r1(y + h - r)}A${r1(r)} ${r1(r)} 0 0 1 ${r1(x + w - r)} ${r1(y + h)}H${r1(x + r)}A${r1(r)} ${r1(r)} 0 0 1 ${r1(x)} ${r1(y + h - r)}V${r1(y + r)}A${r1(r)} ${r1(r)} 0 0 1 ${r1(x + r)} ${r1(y)}Z`;
}
/** Rounded rect with separate top / bottom radii. */
export function rr2(x, y, w, h, rt, rb) {
  rt = Math.max(0, Math.min(rt, w / 2, h / 2));
  rb = Math.max(0, Math.min(rb, w / 2, h / 2));
  return `M${r1(x + rt)} ${r1(y)}H${r1(x + w - rt)}A${r1(rt)} ${r1(rt)} 0 0 1 ${r1(x + w)} ${r1(y + rt)}V${r1(y + h - rb)}A${r1(rb)} ${r1(rb)} 0 0 1 ${r1(x + w - rb)} ${r1(y + h)}H${r1(x + rb)}A${r1(rb)} ${r1(rb)} 0 0 1 ${r1(x)} ${r1(y + h - rb)}V${r1(y + rt)}A${r1(rt)} ${r1(rt)} 0 0 1 ${r1(x + rt)} ${r1(y)}Z`;
}
export function circlePath(cx, cy, r) {
  return `M${r1(cx - r)} ${r1(cy)}A${r1(r)} ${r1(r)} 0 1 0 ${r1(cx + r)} ${r1(cy)}A${r1(r)} ${r1(r)} 0 1 0 ${r1(cx - r)} ${r1(cy)}Z`;
}
/** Arc stroke path from angle a0 to a1 (degrees, 0 = east, clockwise on screen). */
export function arcPath(cx, cy, r, a0, a1) {
  const p = (a) => [cx + r * Math.cos((a * Math.PI) / 180), cy + r * Math.sin((a * Math.PI) / 180)];
  const [x0, y0] = p(a0);
  const [x1, y1] = p(a1);
  const large = Math.abs(a1 - a0) > 180 ? 1 : 0;
  return `M${r1(x0)} ${r1(y0)}A${r1(r)} ${r1(r)} 0 ${large} 1 ${r1(x1)} ${r1(y1)}`;
}
/** n-point star path. */
export function starPath(cx, cy, ro, ri, n = 5, rot = -90) {
  let d = '';
  for (let i = 0; i < n * 2; i++) {
    const a = ((rot + (i * 180) / n) * Math.PI) / 180;
    const r = i % 2 === 0 ? ro : ri;
    d += (i === 0 ? 'M' : 'L') + r1(cx + r * Math.cos(a)) + ' ' + r1(cy + r * Math.sin(a));
  }
  return d + 'Z';
}
/** Rounded star (soft toy star): quadratic joins at the tips and valleys. */
export function softStar(cx, cy, ro, ri, n = 5, rot = -90, round = 0.18) {
  const pts = [];
  for (let i = 0; i < n * 2; i++) {
    const a = ((rot + (i * 180) / n) * Math.PI) / 180;
    const r = i % 2 === 0 ? ro : ri;
    pts.push([cx + r * Math.cos(a), cy + r * Math.sin(a)]);
  }
  let d = '';
  const L = pts.length;
  for (let i = 0; i < L; i++) {
    const p = pts[i];
    const a = pts[(i - 1 + L) % L];
    const b = pts[(i + 1) % L];
    const t = round;
    const s = [p[0] + (a[0] - p[0]) * t, p[1] + (a[1] - p[1]) * t];
    const e = [p[0] + (b[0] - p[0]) * t, p[1] + (b[1] - p[1]) * t];
    d += (i === 0 ? 'M' : 'L') + r1(s[0]) + ' ' + r1(s[1]) + 'Q' + r1(p[0]) + ' ' + r1(p[1]) + ' ' + r1(e[0]) + ' ' + r1(e[1]);
  }
  return d + 'Z';
}

// ---- soft shadow / glow without filters (stacked copies) --------------------------------------------------------------
/** A blurred-looking drop shadow of path d: `steps` growing copies, total alpha ≈ a. */
export function softShadow(d, dy, a, steps = 4, spread = 6, color = '#000000') {
  let o = '';
  for (let i = steps; i >= 1; i--) {
    const w = (spread * i) / steps;
    o += `<path d="${d}" transform="translate(0 ${r1(dy)})" fill="${color}" fill-opacity="${r1((a / steps) * 100) / 100}" stroke="${color}" stroke-opacity="${r1((a / steps) * 100) / 100}" stroke-width="${r1(w * 2)}" stroke-linejoin="round"/>`;
  }
  return o;
}
/** Soft ellipse (contact shadow): 3 nested ellipses (ART_REFERENCE §6.2 rule 4). */
export function contactShadow(cx, cy, rx, ry, a = 0.35, color = '#000000') {
  return `<g fill="${color}"><ellipse cx="${r1(cx)}" cy="${r1(cy)}" rx="${r1(rx * 1.5)}" ry="${r1(ry * 1.5)}" fill-opacity="${r1(a * 0.3 * 100) / 100}"/><ellipse cx="${r1(cx)}" cy="${r1(cy)}" rx="${r1(rx * 1.25)}" ry="${r1(ry * 1.25)}" fill-opacity="${r1(a * 0.55 * 100) / 100}"/><ellipse cx="${r1(cx)}" cy="${r1(cy)}" rx="${r1(rx)}" ry="${r1(ry)}" fill-opacity="${r1(a * 100) / 100}"/></g>`;
}

// ---- document ---------------------------------------------------------------------------------------------------------
/**
 * One frame. meta: { scale, pivot: [x, y] (Unity, 0..1 from bottom-left), border: [L, T, R, B] (design px),
 * anchor: [x, y] (Unity 0..1), stud: d (design px), use: 'free text' }.
 */
export function doc(w, h, defs, body, meta = {}) {
  const attrs = [];
  if (meta.scale !== undefined) attrs.push(`data-scale="${meta.scale}"`);
  if (meta.pivot) attrs.push(`data-pivot="${meta.pivot.join(' ')}"`);
  if (meta.border) attrs.push(`data-border="${meta.border.join(' ')}"`);
  if (meta.anchor) attrs.push(`data-anchor="${meta.anchor.map((v) => Math.round(v * 10000) / 10000).join(' ')}"`);
  if (meta.stud !== undefined) attrs.push(`data-stud="${r1(meta.stud)}"`);
  if (meta.sym !== undefined) attrs.push(`data-sym="${r1(meta.sym)}"`);
  const vb = meta.viewBox ?? `0 0 ${w} ${h}`;
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="${vb}" ${attrs.join(' ')}>\n<defs>${defs}</defs>\n${body}\n</svg>\n`;
}
