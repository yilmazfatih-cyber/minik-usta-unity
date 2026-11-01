// v4 UI kit (ART_REFERENCE §3: button §3.1, close §3.2, plus §3.3, capsule §3.4, badge §3.5, ribbon §3.6 / ART §14.3,
// panel §3.7, progress §3.8, nav §3.9, lock §3.13, bubble §3.14; HUD §9.4). 9-slice borders in design px.
import { mul, mix, rr, rr2, lin, rad, uid, doc, r1, circlePath, arcPath, softShadow } from './lib.mjs';

const BTN = {
  green: { bevel: '#3EE01A', bevelLine: '#9BF34E', faceTop: '#5CE83A', faceMid: '#2BCA0A', faceBottom: '#19B205', lip: '#02630F', stroke: '#0A5A12', rim: '#1CCC16' },
  cream: { bevel: '#FFFFFF', bevelLine: '#FFFFFF', faceTop: '#FFF6D2', faceMid: '#FAEDC4', faceBottom: '#F4E3B6', lip: '#D9B878', stroke: '#6B4A26', rim: '#EAD7A4' },
  red: { bevel: '#FE6E73', bevelLine: '#FFB0B3', faceTop: '#FF6B6B', faceMid: '#FE3232', faceBottom: '#E01F22', lip: '#7C0000', stroke: '#A30101', rim: '#D41E22' },
  orange: { bevel: '#FFD36B', bevelLine: '#FFE9A8', faceTop: '#FFB32A', faceMid: '#FF9A10', faceBottom: '#F08A00', lip: '#B35A00', stroke: '#7A3A00', rim: '#E07D00' },
  blue: { bevel: '#25BCFC', bevelLine: '#8CE4FF', faceTop: '#2A9AE6', faceMid: '#1C89D2', faceBottom: '#1670C0', lip: '#0E489B', stroke: '#5343B8', rim: '#1468B4' },
  purple: { bevel: '#7A66F8', bevelLine: '#A294FF', faceTop: '#6A58F8', faceMid: '#5B48F7', faceBottom: '#4F3DE0', lip: '#2A1D8B', stroke: '#16133C', rim: '#4A39D0' },
  grey: { bevel: '#D6D1C8', bevelLine: '#E4E0D8', faceTop: '#B9B2A5', faceMid: '#AFA79A', faceBottom: '#A39B8E', lip: '#8C8579', stroke: '#5C564D', rim: '#9C9488' },
};

/**
 * Volumetric button (ART_REFERENCE §3.1 draw order): shadow, outline, outer face ring, lip, inner plate (3-stop
 * gradient), bevel band + light line, optional gloss. `pressed`: plate 8 px down, lip ×0,35.
 */
function button(w, h, r, k, c, pressed = false, opts = {}) {
  const stroke = k.stroke;
  const lip = k.lip;
  const plate = uid('bp');
  const bev = uid('bb');
  const ring = uid('br');
  const defs =
    lin(plate, [[0, c.faceTop], [0.55, c.faceMid], [1, c.faceBottom]]) +
    lin(bev, [[0, c.bevel, 0.9], [1, c.bevel, 0]]) +
    lin(ring, [[0, mul(c.faceMid, 0.92)], [1, mul(c.faceBottom, 0.8)]]);
  const lipH = pressed ? Math.round(k.lipH * 0.35) : k.lipH;
  const down = pressed ? k.lipH - lipH : 0;
  let b = '';
  if (!pressed) b += `<path d="${rr(0, 10, w, h - 10, r)}" fill="#000000" fill-opacity="0.28"/>`;
  b += `<path d="${rr(0, down, w, h - down - (pressed ? 0 : 6), r)}" fill="${stroke}"/>`;
  const o = k.outline;
  const ih = h - down - (pressed ? 0 : 6) - 2 * o;
  b += `<path d="${rr(o, down + o, w - 2 * o, ih, r - o)}" fill="${c.lip}"/>`;
  b += `<path d="${rr(o, down + o, w - 2 * o, ih - lipH, r - o)}" fill="url(#${ring})"/>`;
  b += `<path d="${rr(o + 2, down + o + ih - lipH - 3, w - 2 * o - 4, 3, 1.5)}" fill="${mix(c.lip, '#FFFFFF', 0.12)}" fill-opacity="0.6"/>`;
  const px = o + k.inset;
  const py = down + o + k.insetTop;
  const pw = w - 2 * px;
  const ph = ih - lipH - k.insetTop - k.insetBottom;
  const pr = Math.max(6, r - o - k.inset * 0.6);
  b += `<path d="${rr(px, py, pw, ph, pr)}" fill="url(#${plate})"/>`;
  b += `<path d="${rr2(px, py, pw, Math.min(k.bevelH, ph / 2), pr, 0)}" fill="url(#${bev})"/>`;
  b += `<rect x="${r1(px + pw * 0.04)}" y="${r1(py + 3)}" width="${r1(pw * 0.92)}" height="${k.lineH}" rx="${k.lineH / 2}" fill="${c.bevelLine}" fill-opacity="0.9"/>`;
  if (opts.gloss) b += `<ellipse cx="${r1(px + pw - Math.min(70, pw * 0.2))}" cy="${r1(py + 22)}" rx="${r1(Math.min(34, pw * 0.1))}" ry="9" fill="#FFFFFF" fill-opacity="0.5"/>`;
  return { defs, body: b, plate: { x: px, y: py, w: pw, h: ph } };
}
const SIZE = {
  L: { outline: 6, inset: 24, insetTop: 18, insetBottom: 10, bevelH: 38, lineH: 5, lipH: 40 },
  M: { outline: 5, inset: 16, insetTop: 12, insetBottom: 8, bevelH: 24, lineH: 4, lipH: 28 },
  S: { outline: 4, inset: 12, insetTop: 10, insetBottom: 6, bevelH: 16, lineH: 3, lipH: 20 },
  Q: { outline: 8, inset: 14, insetTop: 12, insetBottom: 8, bevelH: 18, lineH: 4, lipH: 20 },
};
function btnFrame(variant, size, pressed) {
  const c = BTN[variant];
  const dims = { L: [300, 212, 48], M: [240, 136, 36], S: [180, 100, 28] }[size];
  const k = { ...SIZE[size], stroke: c.stroke, lip: c.lip };
  const { defs, body } = button(dims[0], dims[1], dims[2], k, c, pressed, { gloss: variant === 'green' && size !== 'L' ? false : variant === 'cream' });
  const bL = dims[2] + 24;
  const bT = size === 'L' ? 64 : size === 'M' ? 44 : 34;
  const bB = size === 'L' ? 76 : size === 'M' ? 52 : 40;
  return doc(dims[0], dims[1], defs, body, { scale: 1.5, border: [bL, bT, bL, bB] });
}
function squareBtn(variant, s, r, size, pressed) {
  const c = BTN[variant];
  const k = { ...SIZE[size], stroke: c.stroke, lip: c.lip };
  const { defs, body } = button(s, s, r, k, c, pressed);
  return doc(s, s, defs, body, { scale: 1.5, border: [r + 16, r + 16, r + 16, r + 24] });
}

function closeBtn(pressed) {
  const f = uid('cf');
  const defs = rad(f, [[0, '#FF6B6B'], [0.6, '#FE3232'], [1, '#E01F22']], 0.4, 0.35, 0.7);
  const cx = 66;
  const cy = pressed ? 66 : 62;
  let b = pressed ? '' : `<circle cx="66" cy="70" r="60" fill="#000000" fill-opacity="0.3"/>`;
  b += `<circle cx="${cx}" cy="${cy}" r="60" fill="#A30101"/><circle cx="${cx}" cy="${cy + 4}" r="50" fill="#7C0000"/><circle cx="${cx}" cy="${cy - 2}" r="49" fill="url(#${f})"/>`;
  b += `<path d="${arcPath(cx, cy - 2, 42, 200, 340)}" fill="none" stroke="#FE9A9D" stroke-opacity="0.9" stroke-width="6" stroke-linecap="round"/>`;
  const bar = (rot) => `<g transform="rotate(${rot} ${cx} ${cy - 2})"><rect x="${cx - 13}" y="${cy - 35 + 3}" width="26" height="66" rx="13" fill="#8E1233"/><rect x="${cx - 13}" y="${cy - 35}" width="26" height="66" rx="13" fill="#F6EBDE" stroke="#8E1233" stroke-width="6"/></g>`;
  b += bar(45) + bar(-45);
  b += `<g transform="rotate(45 ${cx} ${cy - 2})"><rect x="${cx - 7}" y="${cy - 30}" width="14" height="56" rx="7" fill="#F6EBDE"/></g>`;
  return doc(132, 132, defs, b, { scale: 1.5 });
}
function plusBtn() {
  const f = uid('pf');
  let b = `<circle cx="32" cy="34" r="28" fill="#000000" fill-opacity="0.25"/><circle cx="32" cy="32" r="28" fill="#0C8312"/><circle cx="32" cy="35" r="23" fill="#0E7A0E"/><circle cx="32" cy="31" r="23" fill="url(#${f})"/>`;
  b += `<path d="${arcPath(32, 31, 19, 210, 330)}" fill="none" stroke="#FFFFFF" stroke-opacity="0.5" stroke-width="4" stroke-linecap="round"/>`;
  b += `<path d="M18 33H46M32 19V47" stroke="#0C8312" stroke-width="10" stroke-linecap="round"/><path d="M18 31H46M32 17V45" stroke="#FFFFFF" stroke-width="8" stroke-linecap="round"/>`;
  return doc(64, 64, rad(f, [[0, '#8DF06C'], [0.5, '#5BE03A'], [1, '#22B018']], 0.4, 0.35, 0.7), b, { scale: 1.5 });
}
function badgeSquare() {
  const f = uid('bf');
  const s = 72;
  let b = `<path d="${rr(0, 4, s, s, 20)}" fill="#000000" fill-opacity="0.3"/><path d="${rr(0, 0, s, s, 20)}" fill="#9C0B00"/><path d="${rr(5, 5, s - 10, s - 10, 16)}" fill="#A80E07"/><path d="${rr(5, 5, s - 10, s - 16, 16)}" fill="url(#${f})"/>`;
  b += `<path d="${rr2(7, 7, s - 14, 18, 14, 4)}" fill="#FFFFFF" fill-opacity="0.22"/>`;
  return doc(s, s + 4, lin(f, [[0, '#F04A3C'], [0.45, '#DA201A'], [1, '#C8140F']]), b, { scale: 1.5, pivot: [0.5, 0.5 + 2 / 76] });
}
function badgeRound() {
  const f = uid('bd');
  let b = `<circle cx="28" cy="31" r="28" fill="#000000" fill-opacity="0.3"/><circle cx="28" cy="28" r="28" fill="#9C0B00"/><circle cx="28" cy="31" r="23" fill="#A80E07"/><circle cx="28" cy="28" r="22" fill="url(#${f})"/><path d="${arcPath(28, 28, 17, 205, 335)}" fill="none" stroke="#FFFFFF" stroke-opacity="0.35" stroke-width="5" stroke-linecap="round"/>`;
  return doc(56, 60, lin(f, [[0, '#F04A3C'], [1, '#C8140F']]), b, { scale: 1.5 });
}
/** Counter capsule (§3.4): dark well, 9-slice; h 60 (HUD) / 88 (home). */
function capsule(h, a, fill) {
  const w = h * 3;
  let b = `<path d="${rr(0, 0, w, h, h / 2)}" fill="${fill}" fill-opacity="${a}"/>`;
  b += `<path d="${rr(1.5, 1.5, w - 3, h - 3, h / 2 - 1.5)}" fill="none" stroke="#0E0C2C" stroke-opacity="0.7" stroke-width="3"/>`;
  b += `<path d="${rr(4, h * 0.62, w - 8, h * 0.34, h * 0.17)}" fill="#000000" fill-opacity="0.18"/>`;
  b += `<rect x="${h / 2}" y="3" width="${w - h}" height="2" fill="#FFFFFF" fill-opacity="0.12"/>`;
  b += `<path d="M${h / 2} ${h - 1.5}H${w - h / 2}" stroke="#FFFFFF" stroke-opacity="0.10" stroke-width="2"/>`;
  return doc(w, h, '', b, { scale: 1.5, border: [h / 2 + 4, h / 2, h / 2 + 4, h / 2] });
}
/** HUD moves plate (§9.4): navy label band with gold lines over a cream plate. */
function movesPlate() {
  const W = 300;
  const H = 136;
  const navy = uid('mn');
  const cream = uid('mc');
  const defs = lin(navy, [[0, '#33456E'], [1, '#26355A']]) + lin(cream, [[0, '#FFFBE0'], [0.5, '#F9F3C3'], [1, '#EADFA4']]);
  let b = `<path d="${rr(0, 8, W, H - 8, 30)}" fill="#000000" fill-opacity="0.3"/>`;
  b += `<path d="${rr(0, 0, W, H - 4, 30)}" fill="#0F1A33"/>`;
  b += `<path d="${rr(6, 46, W - 12, H - 56, 24)}" fill="#C9B97A"/><path d="${rr(6, 46, W - 12, H - 62, 24)}" fill="url(#${cream})"/>`;
  b += `<path d="${rr(14, 52, W - 28, 10, 5)}" fill="#FFFFFF" fill-opacity="0.55"/>`;
  b += `<path d="${rr2(6, 6, W - 12, 46, 24, 4)}" fill="url(#${navy})"/>`;
  b += `<rect x="6" y="44" width="${W - 12}" height="4" fill="#F7CD02"/><rect x="6" y="49" width="${W - 12}" height="3" fill="#E07500"/>`;
  b += `<rect x="24" y="11" width="${W - 48}" height="3" rx="1.5" fill="#FFFFFF" fill-opacity="0.18"/>`;
  return doc(W, H, defs, b, { scale: 1.5, border: [40, 56, 40, 40] });
}
/** Dark inset well (panorama well, goal panel). */
function well() {
  const W = 200;
  const H = 136;
  let b = `<path d="${rr(0, 0, W, H, 28)}" fill="#120F33" fill-opacity="0.85"/><path d="${rr(2, 2, W - 4, H - 4, 26)}" fill="none" stroke="#000000" stroke-opacity="0.45" stroke-width="4"/><path d="${rr(6, 10, W - 12, 12, 6)}" fill="#000000" fill-opacity="0.25"/><path d="M28 ${H - 2}H${W - 28}" stroke="#7A6CF0" stroke-opacity="0.35" stroke-width="3" stroke-linecap="round"/>`;
  return doc(W, H, '', b, { scale: 1.5, border: [36, 36, 36, 36] });
}

/** Window panel (§3.7), 9-slice. */
function panelWindow() {
  const W = 320;
  const H = 420;
  const body = uid('pw');
  const defs = lin(body, [[0, '#4A3AC7'], [1, '#523ED5']]);
  let b = `<path d="${rr(0, 16, W, H - 16, 64)}" fill="#000000" fill-opacity="0.4"/>`;
  b += `<path d="${rr(0, 0, W, H - 8, 64)}" fill="#221380"/>`;
  b += `<path d="${rr(7, 7, W - 14, H - 22, 58)}" fill="#3A27B0"/>`;
  b += `<path d="${rr(12, 9, W - 24, 6, 3)}" fill="#7B6BF2" fill-opacity="0.7"/>`;
  b += `<path d="${rr(23, 23, W - 46, H - 54, 44)}" fill="#27198A"/>`;
  b += `<path d="${rr(31, 31, W - 62, H - 70, 38)}" fill="#3C2AAD"/>`;
  b += `<path d="${rr(53, 53, W - 106, H - 106 - 50, 28)}" fill="#261883"/>`;
  b += `<path d="${rr(53, 53, W - 106, H - 106 - 50 - 22, 28)}" fill="#3726A2"/>`;
  b += `<path d="${rr(53, 53, W - 106, H - 106 - 50 - 40, 28)}" fill="url(#${body})"/>`;
  b += `<path d="${rr(60, 57, W - 120, 8, 4)}" fill="#FFFFFF" fill-opacity="0.12"/>`;
  return doc(W, H, defs, b, { scale: 1.5, border: [96, 96, 96, 160] });
}
function panelPlate() {
  const W = 200;
  const H = 200;
  let b = `<path d="${rr(0, 0, W, H, 36)}" fill="#352496"/><path d="${rr(6, 6, W - 12, H - 12, 30)}" fill="#6B5DE0"/><path d="${rr(6, 6, W - 12, H - 22, 30)}" fill="#8C7DFE"/><path d="${rr2(6, 6, W - 12, 8, 30, 0)}" fill="#7261F1"/><path d="${rr(14, 16, W - 28, 6, 3)}" fill="#FFFFFF" fill-opacity="0.18"/>`;
  return doc(W, H, '', b, { scale: 1.5, border: [44, 44, 44, 52] });
}
function panelCard() {
  const W = 200;
  const H = 200;
  const g = uid('pk');
  let b = `<path d="${rr(0, 0, W, H, 36)}" fill="#D9B878"/><path d="${rr(4, 4, W - 8, H - 10, 32)}" fill="#FAF0CE"/><path d="${rr(10, 10, W - 20, H - 22, 28)}" fill="url(#${g})"/><path d="${rr2(10, 10, W - 20, 12, 28, 0)}" fill="#E6D9B0" fill-opacity="0.8"/>`;
  return doc(W, H, lin(g, [[0, '#F7EDC6'], [1, '#F2E6BA']]), b, { scale: 1.5, border: [44, 44, 44, 48] });
}
/** Gold ribbon title (ART §14.3 + ART_REFERENCE §2.4 gold): body + folded tails; 9-slice over the tails. */
function ribbon() {
  const W = 520;
  const H = 150;
  const face = uid('rf');
  const tail = uid('rt');
  const defs = lin(face, [[0, '#FFE96A'], [0.5, '#FFD119'], [1, '#FFAD00']]) + lin(tail, [[0, '#F0A000'], [1, '#C46A00']]);
  const bx = 64;
  const by = 14;
  const bw = W - 128;
  const bh = 104;
  let b = '';
  // tails (behind, 22 px lower, V notch)
  const tailL = `M${bx + 20} ${by + 22}H${8}L${34} ${by + 22 + bh / 2}L${8} ${by + 22 + bh}H${bx + 20}Z`;
  const tailR = `M${W - bx - 20} ${by + 22}H${W - 8}L${W - 34} ${by + 22 + bh / 2}L${W - 8} ${by + 22 + bh}H${W - bx - 20}Z`;
  b += `<path d="${tailL}" transform="translate(0 6)" fill="#000000" fill-opacity="0.25"/><path d="${tailR}" transform="translate(0 6)" fill="#000000" fill-opacity="0.25"/>`;
  b += `<path d="${tailL}" fill="url(#${tail})" stroke="#8A3A06" stroke-width="6" stroke-linejoin="round"/><path d="${tailR}" fill="url(#${tail})" stroke="#8A3A06" stroke-width="6" stroke-linejoin="round"/>`;
  b += `<path d="M${bx} ${by + bh}L${bx + 22} ${by + bh + 22}V${by + bh}Z" fill="#7A2E06"/><path d="M${W - bx} ${by + bh}L${W - bx - 22} ${by + bh + 22}V${by + bh}Z" fill="#7A2E06"/>`;
  b += `<path d="${rr(bx, by + 6, bw, bh, 18)}" fill="#000000" fill-opacity="0.25"/>`;
  b += `<path d="${rr(bx, by, bw, bh, 18)}" fill="#B8430A"/><path d="${rr(bx + 6, by + 6, bw - 12, bh - 12, 13)}" fill="#D96A00"/><path d="${rr(bx + 6, by + 6, bw - 12, bh - 20, 13)}" fill="url(#${face})"/>`;
  b += `<path d="${rr2(bx + 10, by + 9, bw - 20, (bh - 20) * 0.35, 10, 4)}" fill="#FFFFFF" fill-opacity="0.35"/>`;
  b += `<path d="M${bx + 16} ${by + bh - 18}H${bx + bw - 16}" stroke="#FFF4B0" stroke-opacity="0.5" stroke-width="2" stroke-dasharray="6 6"/>`;
  return doc(W, H, defs, b, { scale: 1.5, border: [150, 40, 150, 40] });
}
/** Progress bar (§3.8 meta): orange frame, navy track; fill separate. */
function progressTrack() {
  const W = 200;
  const H = 84;
  let b = `<path d="${rr(0, 4, W, H - 4, 42)}" fill="#000000" fill-opacity="0.25"/><path d="${rr(0, 0, W, H - 4, 40)}" fill="#F79900"/><path d="${rr(3, 2, W - 6, 10, 5)}" fill="#FFD36B" fill-opacity="0.8"/><path d="${rr(8, 8, W - 16, H - 20, 32)}" fill="#06436D"/><path d="${rr(8, 8, W - 16, 10, 5)}" fill="#000000" fill-opacity="0.3"/>`;
  return doc(W, H, '', b, { scale: 1.5, border: [44, 40, 44, 44] });
}
function progressFill() {
  const W = 120;
  const H = 64;
  const g = uid('pf');
  let b = `<path d="${rr(0, 0, W, H, 32)}" fill="#1E6B00"/><path d="${rr(3, 2, W - 6, H - 6, 29)}" fill="url(#${g})"/><path d="${rr(10, 6, W - 20, 14, 7)}" fill="#FFFFFF" fill-opacity="0.3"/>`;
  return doc(W, H, lin(g, [[0, '#A4E03A'], [0.4, '#7FC21A'], [1, '#4E9300']]), b, { scale: 1.5, border: [34, 30, 34, 30] });
}
function progressTrackS() {
  const W = 120;
  const H = 48;
  let b = `<path d="${rr(0, 0, W, H, 24)}" fill="#3B2A1A"/><path d="${rr(6, 6, W - 12, H - 12, 18)}" fill="#FFFFFF" fill-opacity="0.12"/><path d="${rr(6, 6, W - 12, 8, 4)}" fill="#000000" fill-opacity="0.3"/>`;
  return doc(W, H, '', b, { scale: 1.5, border: [26, 24, 26, 24] });
}
function progressFillS() {
  const W = 80;
  const H = 36;
  const g = uid('pg');
  let b = `<path d="${rr(0, 0, W, H, 18)}" fill="url(#${g})"/><path d="${rr(6, 4, W - 12, 9, 4.5)}" fill="#FFFFFF" fill-opacity="0.45"/><path d="${rr(2, H - 8, W - 4, 6, 3)}" fill="#B87900" fill-opacity="0.45"/>`;
  return doc(W, H, lin(g, [[0, '#FFE96A'], [1, '#FFAD00']]), b, { scale: 1.5, border: [20, 18, 20, 18] });
}
/** Speech / tutorial bubble (§3.14), 9-slice + separate tail. */
function bubble() {
  const W = 240;
  const H = 180;
  let b = `<path d="${rr(0, 10, W, H - 10, 40)}" fill="#000000" fill-opacity="0.25"/><path d="${rr(0, 0, W, H - 4, 40)}" fill="#0B3A6E"/><path d="${rr(6, 6, W - 12, H - 20, 34)}" fill="#2E9BF0"/><path d="${rr(14, 14, W - 28, H - 36, 28)}" fill="#FFF6D2"/><path d="${rr2(14, 14, W - 28, 14, 28, 0)}" fill="#FFFFFF" fill-opacity="0.6"/>`;
  return doc(W, H, '', b, { scale: 1.5, border: [56, 56, 56, 64] });
}
function bubbleTail() {
  let b = `<path d="M6 0H58L20 52Z" fill="#0B3A6E"/><path d="M14 0H48L22 36Z" fill="#2E9BF0"/><path d="M20 0H40L24 22Z" fill="#FFF6D2"/>`;
  return doc(64, 56, '', b, { scale: 1.5, pivot: [0.35, 1] });
}
/** Streak bead (Usta Serisi). */
function bead(on) {
  const g = uid('bd');
  if (!on) {
    return doc(56, 56, '', `<circle cx="28" cy="29" r="24" fill="#0B0920" fill-opacity="0.75"/><circle cx="28" cy="28" r="24" fill="none" stroke="#5B4CC8" stroke-opacity="0.8" stroke-width="3"/><circle cx="28" cy="31" r="15" fill="#000000" fill-opacity="0.35"/>`, { scale: 1.5 });
  }
  const defs = rad(g, [[0, '#FFF3A0'], [0.45, '#FFC21A'], [0.85, '#F08A00'], [1, '#B35A00']], 0.38, 0.32, 0.75);
  let b = `<circle cx="28" cy="31" r="25" fill="#000000" fill-opacity="0.3"/><circle cx="28" cy="28" r="25" fill="#7A3A00"/><circle cx="28" cy="28" r="21" fill="url(#${g})"/>`;
  b += `<path d="M28 15L31.5 24.5L41 28L31.5 31.5L28 41L24.5 31.5L15 28L24.5 24.5Z" fill="#FFFFFF" fill-opacity="0.9"/><ellipse cx="21" cy="19" rx="6" ry="3.5" transform="rotate(-35 21 19)" fill="#FFFFFF" fill-opacity="0.7"/>`;
  return doc(56, 56, defs, b, { scale: 1.5 });
}
function toggle(on) {
  const W = 176;
  const H = 88;
  const tr = on ? '#4CC23F' : '#9C96B5';
  const knobX = on ? W - 44 : 44;
  const k = uid('tk');
  let b = `<path d="${rr(0, 4, W, H - 4, 42)}" fill="#000000" fill-opacity="0.25"/><path d="${rr(0, 0, W, H - 4, 42)}" fill="${mul(tr, 0.6)}"/><path d="${rr(5, 5, W - 10, H - 14, 37)}" fill="${tr}"/><path d="${rr(5, 5, W - 10, 14, 7)}" fill="#000000" fill-opacity="0.18"/>`;
  b += `<circle cx="${knobX}" cy="${H / 2 + 2}" r="36" fill="#000000" fill-opacity="0.25"/><circle cx="${knobX}" cy="${H / 2 - 2}" r="36" fill="url(#${k})"/><circle cx="${knobX}" cy="${H / 2 - 2}" r="35" fill="none" stroke="#C9C3DA" stroke-width="2"/>`;
  return doc(W, H, rad(k, [[0, '#FFFFFF'], [0.7, '#F2EFFA'], [1, '#D6D0E8']], 0.4, 0.3, 0.7), b, { scale: 1.5 });
}
/** Bottom navigation bar (§3.9), 9-slice; raised selected tab. */
function navBar() {
  const W = 216;
  const H = 200;
  const g = uid('nb');
  let b = `<rect y="-16" width="${W}" height="16" fill="url(#${g}s)"/><rect width="${W}" height="${H}" fill="url(#${g})"/><rect width="${W}" height="8" fill="#31229C"/><rect y="8" width="${W}" height="11" fill="#4634CE"/>`;
  const defs = lin(g, [[0, '#533FE9'], [1, '#4E3CDB']]) + lin(g + 's', [[0, '#000000', 0], [1, '#000000', 0.25]]);
  return doc(W, H + 16, defs, b, { scale: 1.5, border: [8, 40, 8, 8], viewBox: `0 -16 ${W} ${H + 16}` });
}
function navTab() {
  const W = 300;
  const H = 300;
  const g = uid('nt');
  let b = `<path d="${rr2(0, 0, W, H, 44, 0)}" fill="#2C1E94"/><path d="${rr2(12, 12, W - 24, H - 12, 34, 0)}" fill="url(#${g})"/><path d="${rr2(17, 17, W - 34, 5, 3, 3)}" fill="#9C92FF"/>`;
  return doc(W, H, lin(g, [[0, '#6A5CFF'], [1, '#5747E8']]), b, { scale: 1.5, pivot: [0.5, 0] });
}

export function frames() {
  const o = [];
  for (const [v, size, name] of [
    ['green', 'L', 'btn_primary'],
    ['cream', 'M', 'btn_secondary'],
    ['red', 'M', 'btn_danger'],
    ['orange', 'M', 'btn_offer'],
    ['grey', 'M', 'btn_disabled'],
    ['green', 'M', 'btn_primary_m'],
  ]) {
    o.push({ cat: 'ui', name, svg: () => btnFrame(v, size, false) });
    if (v !== 'grey') o.push({ cat: 'ui', name: name + '_pressed', svg: () => btnFrame(v, size, true) });
  }
  o.push({ cat: 'ui', name: 'btn_tool', svg: () => squareBtn('purple', 136, 36, 'M', false) });
  o.push({ cat: 'ui', name: 'btn_tool_pressed', svg: () => squareBtn('purple', 136, 36, 'M', true) });
  o.push({ cat: 'ui', name: 'btn_booster', svg: () => squareBtn('green', 160, 40, 'Q', false) });
  o.push({ cat: 'ui', name: 'btn_booster_pressed', svg: () => squareBtn('green', 160, 40, 'Q', true) });
  o.push({ cat: 'ui', name: 'btn_booster_off', svg: () => squareBtn('grey', 160, 40, 'Q', false) });
  o.push({ cat: 'ui', name: 'btn_slot', svg: () => squareBtn('blue', 184, 44, 'Q', false) });
  o.push({ cat: 'ui', name: 'btn_close', svg: () => closeBtn(false) });
  o.push({ cat: 'ui', name: 'btn_close_pressed', svg: () => closeBtn(true) });
  o.push({ cat: 'ui', name: 'btn_plus', svg: plusBtn });
  o.push({ cat: 'ui', name: 'badge_count', svg: badgeSquare });
  o.push({ cat: 'ui', name: 'badge_dot', svg: badgeRound });
  o.push({ cat: 'ui', name: 'capsule_hud', svg: () => capsule(60, 0.85, '#14113A') });
  o.push({ cat: 'ui', name: 'capsule_home', svg: () => capsule(88, 0.8, '#23164D') });
  o.push({ cat: 'ui', name: 'hud_moves_plate', svg: movesPlate });
  o.push({ cat: 'ui', name: 'hud_well', svg: well });
  o.push({ cat: 'ui', name: 'panel_window', svg: panelWindow });
  o.push({ cat: 'ui', name: 'panel_plate', svg: panelPlate });
  o.push({ cat: 'ui', name: 'panel_card', svg: panelCard });
  o.push({ cat: 'ui', name: 'ribbon_title', svg: ribbon });
  o.push({ cat: 'ui', name: 'progress_track', svg: progressTrack });
  o.push({ cat: 'ui', name: 'progress_fill', svg: progressFill });
  o.push({ cat: 'ui', name: 'progress_track_s', svg: progressTrackS });
  o.push({ cat: 'ui', name: 'progress_fill_s', svg: progressFillS });
  o.push({ cat: 'ui', name: 'bubble_speech', svg: bubble });
  o.push({ cat: 'ui', name: 'bubble_tail', svg: bubbleTail });
  o.push({ cat: 'ui', name: 'bead_on', svg: () => bead(true) });
  o.push({ cat: 'ui', name: 'bead_off', svg: () => bead(false) });
  o.push({ cat: 'ui', name: 'toggle_on', svg: () => toggle(true) });
  o.push({ cat: 'ui', name: 'toggle_off', svg: () => toggle(false) });
  o.push({ cat: 'ui', name: 'nav_bar', svg: navBar });
  o.push({ cat: 'ui', name: 'nav_tab_on', svg: navTab });
  o.push({ cat: 'ui', name: 'px_white', svg: () => doc(8, 8, '', '<rect width="8" height="8" fill="#FFFFFF"/>', { scale: 1 }) });
  return o;
}
