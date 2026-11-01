// v4 particles and effects (ART_REFERENCE §8; JUICE): white masks tinted at runtime unless stated.
import { rr, lin, rad, uid, doc, r1, starPath, softStar, circlePath } from './lib.mjs';

function spark() {
  const g = uid('fs');
  const d = 'M32 2Q35 26 62 32Q35 38 32 62Q29 38 2 32Q29 26 32 2Z';
  return doc(64, 64, rad(g, [[0, '#FFFFFF', 0.9], [1, '#FFFFFF', 0]]), `<circle cx="32" cy="32" r="22" fill="url(#${g})" fill-opacity="0.6"/><path d="${d}" fill="#FFFFFF"/>`, { scale: 1.5 });
}
function star() {
  return doc(64, 64, '', `<path d="${softStar(32, 34, 30, 13, 5, -90, 0.15)}" fill="#FFFFFF"/>`, { scale: 1.5 });
}
function dust() {
  const g = uid('fd');
  let b = '';
  for (const [x, y, r] of [[48, 54, 30], [30, 48, 20], [66, 46, 22], [46, 34, 22], [62, 62, 18]]) b += `<circle cx="${x}" cy="${y}" r="${r}" fill="url(#${g})"/>`;
  return doc(96, 96, rad(g, [[0, '#FFFFFF', 0.8], [0.6, '#FFFFFF', 0.45], [1, '#FFFFFF', 0]]), b, { scale: 1 });
}
function confetti() {
  return doc(24, 12, '', `<rect width="24" height="12" rx="2" fill="#FFFFFF"/><rect x="1" y="1" width="22" height="3" rx="1.5" fill="#FFFFFF"/>`, { scale: 1.5 });
}
function confettiRound() {
  return doc(16, 16, '', `<circle cx="8" cy="8" r="7.5" fill="#FFFFFF"/>`, { scale: 1.5 });
}
function ring() {
  return doc(128, 128, '', `<circle cx="64" cy="64" r="56" fill="none" stroke="#FFFFFF" stroke-width="10"/><circle cx="64" cy="64" r="56" fill="none" stroke="#FFFFFF" stroke-opacity="0.35" stroke-width="16"/>`, { scale: 1 });
}
function glow() {
  const g = uid('fg');
  return doc(256, 256, rad(g, [[0, '#FFFFFF', 1], [0.35, '#FFFFFF', 0.55], [0.7, '#FFFFFF', 0.15], [1, '#FFFFFF', 0]]), `<circle cx="128" cy="128" r="128" fill="url(#${g})"/>`, { scale: 0.5 });
}
/** Win / reward ray fan (ART_REFERENCE §8): 18 rays, alternating weight; rotate at runtime (0,02–0,05 turn/s). */
function rays() {
  const g = uid('fr');
  let b = '';
  const n = 18;
  for (let i = 0; i < n; i++) {
    const a0 = (i * 360) / n;
    const w = i % 2 === 0 ? 10 : 6;
    const p = (a, r) => [512 + r * Math.cos((a * Math.PI) / 180), 512 + r * Math.sin((a * Math.PI) / 180)];
    const [x1, y1] = p(a0 - w / 2, 512);
    const [x2, y2] = p(a0 + w / 2, 512);
    b += `<path d="M512 512L${r1(x1)} ${r1(y1)}L${r1(x2)} ${r1(y2)}Z" fill="url(#${g})" fill-opacity="${i % 2 === 0 ? 1 : 0.6}"/>`;
  }
  return doc(1024, 1024, rad(g, [[0, '#FFFFFF', 0.9], [0.5, '#FFFFFF', 0.35], [1, '#FFFFFF', 0]], 0.5, 0.5, 0.5, ' gradientUnits="userSpaceOnUse" cx="512" cy="512" r="512"').replace(' cx="0.5" cy="0.5" r="0.5"', ''), b, { scale: 0.5 });
}
function dropBlob() {
  const g = uid('fb');
  return doc(240, 80, rad(g, [[0, '#000000', 0.45], [0.6, '#000000', 0.2], [1, '#000000', 0]]), `<ellipse cx="120" cy="40" rx="120" ry="40" fill="url(#${g})"/>`, { scale: 0.5 });
}
function chip() {
  return doc(32, 24, '', `<path d="M3 6L22 2L29 12L24 21L6 22Z" fill="#FFFFFF"/><path d="M6 6L21 3L26 10" fill="none" stroke="#FFFFFF" stroke-opacity="0.6" stroke-width="2"/>`, { scale: 1.5 });
}
function sheen() {
  const g = uid('fh');
  return doc(80, 240, lin(g, [[0, '#FFFFFF', 0], [0.5, '#FFFFFF', 0.7], [1, '#FFFFFF', 0]], 0, 0, 1, 0), `<rect width="80" height="240" fill="url(#${g})"/>`, { scale: 0.5 });
}

export function frames() {
  return [
    { cat: 'fx', name: 'fx_spark', svg: spark },
    { cat: 'fx', name: 'fx_star', svg: star },
    { cat: 'fx', name: 'fx_dust', svg: dust },
    { cat: 'fx', name: 'fx_confetti', svg: confetti },
    { cat: 'fx', name: 'fx_confetti_round', svg: confettiRound },
    { cat: 'fx', name: 'fx_ring', svg: ring },
    { cat: 'fx', name: 'fx_glow', svg: glow },
    { cat: 'fx', name: 'fx_rays', svg: rays },
    { cat: 'fx', name: 'fx_drop_blob', svg: dropBlob },
    { cat: 'fx', name: 'fx_chip', svg: chip },
    { cat: 'fx', name: 'fx_sheen', svg: sheen },
  ];
}
