// logo_wordmark: "Minik Usta" (code name, D-033 / D-068 — the store name comes later: this frame is replaceable).
// ART_REFERENCE §9.1 3D-letter language (own letterforms = Baloo 2 800, own colour order; no tuile-letter motif).
import { mul, mix, lin, uid, doc, r1 } from './lib.mjs';

const FONT = `font-family="'Baloo 2', 'Arial Rounded MT Bold', sans-serif" font-weight="800"`;
const COLS = ['#E8473B', '#FF9A1F', '#FFC93C', '#3FC35A', '#2984DE', '#8E44D9', '#E8473B', '#FF9A1F', '#2AB3A0'];

function word(text, x, y, size, colourOffset, defsOut) {
  const letters = [...text];
  let ext = '';
  let out = '';
  let face = '';
  let hi = '';
  const spans = (fillFn) => letters.map((ch, i) => `<tspan fill="${fillFn(i)}">${ch}</tspan>`).join('');
  for (let k = 14; k >= 1; k--) ext += `<text x="${x}" y="${y + k}" ${FONT} font-size="${size}" text-anchor="middle" letter-spacing="-2" transform="translate(${r1(k * 0.35)} 0)">${spans((i) => mul(COLS[(i + colourOffset) % COLS.length], 0.55))}</text>`;
  out = `<text x="${x}" y="${y}" ${FONT} font-size="${size}" text-anchor="middle" letter-spacing="-2" fill="#4F1E9C" stroke="#4F1E9C" stroke-width="16" stroke-linejoin="round">${text}</text>`;
  const shadow = `<text x="${x}" y="${y + 26}" ${FONT} font-size="${size}" text-anchor="middle" letter-spacing="-2" fill="#1A0B40" fill-opacity="0.35" stroke="#1A0B40" stroke-opacity="0.35" stroke-width="16" stroke-linejoin="round">${text}</text>`;
  const ids = letters.map((_, i) => {
    const id = uid('lw');
    const c = COLS[(i + colourOffset) % COLS.length];
    defsOut.push(lin(id, [[0, mix(c, '#FFFFFF', 0.45)], [0.45, c], [1, mul(c, 0.82)]], 0, y - size * 0.72, 0, y, 'userSpaceOnUse'));
    return id;
  });
  face = `<text x="${x}" y="${y}" ${FONT} font-size="${size}" text-anchor="middle" letter-spacing="-2">${spans((i) => `url(#${ids[i]})`)}</text>`;
  hi = `<text x="${x - 2}" y="${y - 4}" ${FONT} font-size="${size}" text-anchor="middle" letter-spacing="-2" fill="none" stroke="#FFFFFF" stroke-opacity="0.35" stroke-width="3">${text}</text>`;
  return shadow + `<g stroke="#4F1E9C" stroke-width="16" stroke-linejoin="round">${ext.replace(/<text /g, '<text paint-order="stroke" ')}</g>` + out + face + hi;
}

function wordmark() {
  const defs = [];
  const w = 680;
  const h = 400;
  let b = `<g transform="rotate(-4 340 200)">`;
  b += word('Minik', 340, 175, 170, 0, defs);
  b += word('Usta', 360, 345, 170, 5, defs);
  // stud dots on the i tittles (toy-brick wink; own motif)
  b += `</g>`;
  return doc(w, h, defs.join(''), b, { scale: 1 });
}

export function frames() {
  return [{ cat: 'logo', name: 'logo_wordmark', svg: wordmark }];
}
