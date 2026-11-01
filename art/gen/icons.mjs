// New v4 icons in the v3 icon language (ART §9; ART_REFERENCE §3, §10 "ikon ve rozet": 3 tones + specular + shadow,
// dark-tone outline, no black). 128 × 128 design box.
import { mul, mix, rr, lin, rad, uid, doc, r1, starPath, softStar, arcPath, circlePath } from './lib.mjs';
import { neutralLIcon } from './blocks.mjs';

/** Shape with the v3 icon stack: drop copy (+8), dark outline (stroke), gradient face, inner top light. */
function volume(d, { top, mid, bottom, edge, outline = 10, lightA = 0.3 }) {
  const g = uid('ig');
  const c = uid('ic');
  const defs = lin(g, [[0, top], [0.5, mid], [1, bottom]]) + `<clipPath id="${c}"><path d="${d}"/></clipPath>`;
  let b = `<path d="${d}" fill="${edge}" stroke="${edge}" stroke-width="${outline}" stroke-linejoin="round" transform="translate(0 7)"/>`;
  b += `<path d="${d}" fill="${edge}" stroke="${edge}" stroke-width="${outline}" stroke-linejoin="round"/>`;
  b += `<path d="${d}" fill="url(#${g})"/>`;
  b += `<g clip-path="url(#${c})"><path d="${d}" fill="#FFFFFF" fill-opacity="${lightA}"/><path d="${d}" fill="url(#${g})" transform="translate(0 6)"/></g>`;
  return { defs, body: b };
}
const LAV = { top: '#F1ECFF', mid: '#D3C9FF', bottom: '#A99AF5', edge: '#2A1D8B' };

function pause() {
  const d = rr(34, 26, 22, 76, 11) + rr(72, 26, 22, 76, 11);
  const v = volume(d, LAV);
  return doc(128, 128, v.defs, v.body + `<rect x="38" y="32" width="6" height="30" rx="3" fill="#FFFFFF" fill-opacity="0.8"/><rect x="76" y="32" width="6" height="30" rx="3" fill="#FFFFFF" fill-opacity="0.8"/>`, { scale: 1.5 });
}
function starEmpty() {
  const d = softStar(64, 66, 54, 25, 5, -90, 0.16);
  const g = uid('se');
  let b = `<path d="${d}" fill="#0B0920" stroke="#0B0920" stroke-width="10" stroke-linejoin="round" transform="translate(0 4)" fill-opacity="0.6" stroke-opacity="0.6"/><path d="${d}" fill="url(#${g})" stroke="#16133C" stroke-width="8" stroke-linejoin="round"/>`;
  b += `<path d="${softStar(64, 70, 40, 18, 5, -90, 0.16)}" fill="#000000" fill-opacity="0.25"/>`;
  return doc(128, 128, lin(g, [[0, '#3A3192'], [1, '#2A2070']]), b, { scale: 1.5 });
}
function speaker(off) {
  const d = 'M22 50Q22 44 28 44H44L68 24Q76 18 76 28V100Q76 110 68 104L44 84H28Q22 84 22 78Z';
  const v = volume(d, { top: '#9FE6FF', mid: '#38B4F0', bottom: '#1670C0', edge: '#0B3A6E' });
  let b = v.body;
  if (!off) {
    b += `<path d="M88 46Q98 64 88 82" fill="none" stroke="#0B3A6E" stroke-width="15" stroke-linecap="round"/><path d="M100 34Q118 64 100 94" fill="none" stroke="#0B3A6E" stroke-width="15" stroke-linecap="round"/>`;
    b += `<path d="M88 46Q98 64 88 82" fill="none" stroke="#7FD6FF" stroke-width="8" stroke-linecap="round"/><path d="M100 34Q118 64 100 94" fill="none" stroke="#7FD6FF" stroke-width="8" stroke-linecap="round"/>`;
  } else {
    b += `<path d="M90 48L116 80M116 48L90 80" stroke="#7C0000" stroke-width="16" stroke-linecap="round"/><path d="M90 46L116 78M116 46L90 78" stroke="#FE3232" stroke-width="9" stroke-linecap="round"/>`;
  }
  return doc(128, 128, v.defs, b, { scale: 1.5 });
}
function music() {
  const d = 'M50 26L102 16V84Q102 98 86 100Q72 102 70 90Q70 80 84 76Q90 75 92 76V40L60 46V94Q60 108 44 110Q30 112 28 100Q28 90 42 86Q48 85 50 86Z';
  const v = volume(d, { top: '#FFB8E0', mid: '#E04FA8', bottom: '#A3207A', edge: '#5E0C45' });
  return doc(128, 128, v.defs, v.body, { scale: 1.5 });
}
function vibration() {
  const d = rr(40, 16, 48, 96, 12);
  const v = volume(d, { top: '#E3E8EE', mid: '#A3ABB5', bottom: '#6E7A87', edge: '#2B3036' });
  let b = v.body + `<path d="${rr(47, 26, 34, 66, 6)}" fill="#1F4F8F"/><path d="${rr(49, 28, 30, 18, 5)}" fill="#FFFFFF" fill-opacity="0.25"/><circle cx="64" cy="101" r="4.5" fill="#2B3036"/>`;
  for (const s of [-1, 1]) {
    const x = s < 0 ? 26 : 102;
    b += `<path d="M${x} 40L${x + s * -6} 52L${x} 64L${x + s * -6} 76L${x} 88" fill="none" stroke="#0B3A6E" stroke-width="11" stroke-linecap="round" stroke-linejoin="round"/><path d="M${x} 40L${x + s * -6} 52L${x} 64L${x + s * -6} 76L${x} 88" fill="none" stroke="#FFC21A" stroke-width="5" stroke-linecap="round" stroke-linejoin="round"/>`;
  }
  return doc(128, 128, v.defs, b, { scale: 1.5 });
}
function motion() {
  const d = circlePath(78, 64, 30);
  const v = volume(d, { top: '#BDF7A6', mid: '#5CE83A', bottom: '#19B205', edge: '#0A5A12' });
  let b = '';
  for (const [y, x0, w] of [[42, 14, 30], [64, 6, 34], [86, 14, 30]]) b += `<path d="M${x0} ${y}H${x0 + w}" stroke="#0A5A12" stroke-width="14" stroke-linecap="round"/><path d="M${x0} ${y}H${x0 + w}" stroke="#FFFFFF" stroke-width="7" stroke-linecap="round"/>`;
  b += v.body + `<ellipse cx="68" cy="50" rx="10" ry="6" transform="rotate(-30 68 50)" fill="#FFFFFF" fill-opacity="0.7"/>`;
  return doc(128, 128, v.defs, b, { scale: 1.5 });
}
function flagRing(inner, defs) {
  const c = uid('fc');
  let b = `<circle cx="64" cy="70" r="54" fill="#000000" fill-opacity="0.3"/><circle cx="64" cy="64" r="54" fill="#2A1D8B"/><circle cx="64" cy="64" r="48" fill="#FFFFFF"/>`;
  b += `<clipPath id="${c}"><circle cx="64" cy="64" r="44"/></clipPath><g clip-path="url(#${c})">${inner}<ellipse cx="50" cy="38" rx="30" ry="14" transform="rotate(-25 50 38)" fill="#FFFFFF" fill-opacity="0.3"/><path d="M20 96Q64 120 108 96V112H20Z" fill="#000000" fill-opacity="0.15"/></g>`;
  return doc(128, 128, defs, b, { scale: 1.5 });
}
function flagTr() {
  // crescent + star (white on red), proportions after the flag law ratios simplified
  const inner = `<rect x="0" y="0" width="128" height="128" fill="#E30A17"/><circle cx="54" cy="64" r="24" fill="#FFFFFF"/><circle cx="60" cy="64" r="19.2" fill="#E30A17"/><path d="${starPath(84, 64, 12, 4.8, 5, 180)}" fill="#FFFFFF"/>`;
  return flagRing(inner, '');
}
function flagEn() {
  const inner = `<rect width="128" height="128" fill="#012169"/><path d="M0 20L128 108M128 20L0 108" stroke="#FFFFFF" stroke-width="22"/><path d="M0 20L128 108M128 20L0 108" stroke="#C8102E" stroke-width="8"/><path d="M64 0V128M0 64H128" stroke="#FFFFFF" stroke-width="30"/><path d="M64 0V128M0 64H128" stroke="#C8102E" stroke-width="18"/>`;
  return flagRing(inner, '');
}
function play() {
  const d = 'M42 26Q42 16 52 22L104 56Q112 62 104 68L52 104Q42 110 42 100Z';
  const v = volume(d, { top: '#FFFFFF', mid: '#F2EEFF', bottom: '#C9BEFD', edge: '#2A1D8B' });
  return doc(128, 128, v.defs, v.body, { scale: 1.5 });
}

export function frames() {
  return [
    { cat: 'icon', name: 'icon_pause', svg: pause },
    { cat: 'icon', name: 'icon_star_empty', svg: starEmpty },
    { cat: 'icon', name: 'icon_sound_on', svg: () => speaker(false) },
    { cat: 'icon', name: 'icon_sound_off', svg: () => speaker(true) },
    { cat: 'icon', name: 'icon_music', svg: music },
    { cat: 'icon', name: 'icon_vibration', svg: vibration },
    { cat: 'icon', name: 'icon_motion', svg: motion },
    { cat: 'icon', name: 'icon_play', svg: play },
    { cat: 'icon', name: 'icon_blocks', svg: neutralLIcon },
    { cat: 'icon', name: 'flag_tr', svg: flagTr },
    { cat: 'icon', name: 'flag_en', svg: flagEn },
  ];
}
