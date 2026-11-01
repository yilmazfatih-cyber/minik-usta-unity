// v4 board pieces (ART §2.4 board colours, §4 site, §5 wall / gaps / crane area; ART_REFERENCE §5.3 tray, §7.2 scene).
import { mul, mix, rr, rr2, lin, rad, uid, doc, r1, circlePath, contactShadow } from './lib.mjs';

const INK = '#3B2A1A';

function yardFloor(alt) {
  const base = alt ? '#E2BD80' : '#E9C891';
  const g = uid('yf');
  const h = uid('yh');
  const defs = lin(g, [[0, mix(base, '#FFFFFF', 0.1)], [1, mul(base, 0.97)]]) + rad(h, [[0, '#B98A4E'], [0.7, '#C9A065'], [1, '#D4AE6E']], 0.5, 0.35, 0.6);
  let b = `<rect width="120" height="120" fill="url(#${g})"/>`;
  // pegboard grid lines (3 px #D4AE6E split across the tile edge) + panel bevel
  b += `<path d="M0 1H120M0 119H120M1 0V120M119 0V120" stroke="#D4AE6E" stroke-width="3"/>`;
  b += `<path d="M3 4.5H116M4.5 3V116" stroke="#FFFFFF" stroke-opacity="0.22" stroke-width="3"/>`;
  b += `<path d="M3 115.5H117M115.5 3V117" stroke="#B98A4E" stroke-opacity="0.18" stroke-width="3"/>`;
  // ART §2.4 DL-2R-18 hole Ø 12 (+ lower inner shade arc)
  b += `<circle cx="60" cy="60.8" r="7.4" fill="#FFFFFF" fill-opacity="0.35"/><circle cx="60" cy="60" r="6.6" fill="url(#${h})"/><path d="M54.4 62A6 6 0 0 0 65.6 62" fill="none" stroke="#9C7038" stroke-opacity="0.5" stroke-width="2"/>`;
  // faint wood fibre
  b += `<path d="M14 30Q40 26 70 31T112 28M10 88Q36 84 66 90T110 86" fill="none" stroke="${mul(base, 0.9)}" stroke-opacity="0.35" stroke-width="2" stroke-linecap="round"/>`;
  return doc(120, 120, defs, b, { scale: 1.5 });
}

function siteFloor() {
  let b = `<rect width="120" height="120" fill="#1F4F8F"/>`;
  const g = uid('sf');
  b += `<rect width="120" height="120" fill="url(#${g})"/>`;
  b += `<path d="M0 0.5H120M0 119.5H120M0.5 0V120M119.5 0V120" stroke="#FFFFFF" stroke-opacity="0.14" stroke-width="1"/>`;
  // paper speckles (seeded, fixed)
  let s = 7;
  const rnd = () => ((s = (s * 16807) % 2147483647) / 2147483647);
  for (let i = 0; i < 26; i++) b += `<rect x="${r1(4 + rnd() * 110)}" y="${r1(4 + rnd() * 110)}" width="2" height="2" fill="#FFFFFF" fill-opacity="0.05"/>`;
  return doc(120, 120, rad(g, [[0, '#FFFFFF', 0.05], [1, '#FFFFFF', 0]], 0.3, 0.25, 0.9), b, { scale: 1.5 });
}
function siteDeep() {
  return doc(120, 120, '', `<rect width="120" height="120" fill="#173D70"/><path d="M0 0.5H120M0.5 0V120" stroke="#000000" stroke-opacity="0.12" stroke-width="1"/>`, { scale: 0.5 });
}

/** Wall: concrete-brick strip, 60 px wide, one row (120) tall; tiles vertically (running bond, 4 courses). */
function wallSeg() {
  const g = uid('wg');
  const defs = lin(g, [[0, '#D3D8DE'], [0.5, '#A9AFB8'], [1, '#7D848E']], 0, 0, 1, 0);
  let b = `<rect width="60" height="120" fill="#6E757D"/>`;
  const brick = (x, y, w, h) => {
    let o = `<path d="${rr(x + 1.5, y + 1.5, w - 3, h - 3, 4)}" fill="url(#${g})"/>`;
    o += `<path d="${rr(x + 3, y + 2.5, w - 6, 5, 2.5)}" fill="#FFFFFF" fill-opacity="0.35"/>`;
    o += `<path d="M${x + 4} ${y + h - 3}H${x + w - 4}" stroke="#596068" stroke-opacity="0.55" stroke-width="3" stroke-linecap="round"/>`;
    return o;
  };
  for (let i = 0; i < 4; i++) {
    const y = i * 30;
    if (i % 2 === 0) b += brick(0, y, 60, 30);
    else b += brick(-30, y, 60, 30) + brick(30, y, 60, 30);
  }
  // formwork pores + side edges + outline (6 px ink α .8 on both long sides)
  b += `<g fill="#596068" fill-opacity="0.45"><circle cx="17" cy="14" r="1.6"/><circle cx="44" cy="51" r="1.4"/><circle cx="14" cy="77" r="1.5"/><circle cx="47" cy="103" r="1.6"/></g>`;
  b += `<rect x="6" width="5" height="120" fill="#FFFFFF" fill-opacity="0.18"/><rect x="49" width="5" height="120" fill="#000000" fill-opacity="0.16"/>`;
  b += `<rect x="0" width="6" height="120" fill="${INK}" fill-opacity="0.85"/><rect x="54" width="6" height="120" fill="${INK}" fill-opacity="0.85"/>`;
  return doc(60, 120, defs, b, { scale: 1.5 });
}
function hazardStripes(x, y, w, h, clipId) {
  let o = `<clipPath id="${clipId}"><path d="${rr(x, y, w, h, Math.min(6, h / 2))}"/></clipPath><g clip-path="url(#${clipId})"><rect x="${x}" y="${y}" width="${w}" height="${h}" fill="#FFC21A"/>`;
  for (let i = -2; i < w / 14 + 2; i++) o += `<path d="M${x + i * 20} ${y + h}L${x + i * 20 + 10} ${y + h}L${x + i * 20 + 10 + h} ${y}L${x + i * 20 + h} ${y}Z" fill="#2B2B2B"/>`;
  o += `<rect x="${x}" y="${y}" width="${w}" height="${Math.max(2, h * 0.28)}" fill="#FFFFFF" fill-opacity="0.3"/><rect x="${x}" y="${y + h - Math.max(2, h * 0.22)}" width="${w}" height="${Math.max(2, h * 0.22)}" fill="#000000" fill-opacity="0.22"/></g>`;
  return o;
}
/** Wall cap (ART §5: 20 px hazard head + 6 px outline), sits on the wall top: pivot bottom centre. */
function wallCap() {
  const c = uid('wc');
  let b = `<path d="${rr(-2, 4, 64, 30, 9)}" fill="${INK}"/>`;
  b += hazardStripes(4, 9, 52, 20, c);
  b += `<path d="${rr(-2, 4, 64, 30, 9)}" fill="none" stroke="${INK}" stroke-width="6"/>`;
  return doc(60, 36, '', b, { scale: 1.5, pivot: [0.5, 0], viewBox: '-2 0 64 36' });
}
/** Gap opening of one row (W1 static, ART §5): sky-filled hole with dark jambs (the wall's thickness). */
function gapSeg() {
  const g = uid('gj');
  const defs = lin(g, [[0, '#000000', 0.35], [1, '#000000', 0]], 0, 0, 1, 0);
  let b = `<rect width="60" height="120" fill="#FFFFFF" fill-opacity="0.10"/>`;
  b += `<rect width="12" height="120" fill="url(#${g})"/><rect x="48" width="12" height="120" fill="url(#${g})" transform="matrix(-1 0 0 1 108 0)"/>`;
  b += `<rect x="0" width="4" height="120" fill="${INK}" fill-opacity="0.85"/><rect x="56" width="4" height="120" fill="${INK}" fill-opacity="0.85"/>`;
  return doc(60, 120, defs, b, { scale: 1.5 });
}
/** Hazard band of a gap edge (14 px band + 6 px outline); flip Y for the bottom edge. Pivot = the wall edge it hugs. */
function gapEdge() {
  const c = uid('ge');
  let b = `<rect x="0" y="0" width="60" height="22" fill="${INK}"/>`;
  b += hazardStripes(3, 3, 54, 14, c);
  return doc(60, 22, '', b, { scale: 1.5, pivot: [0.5, 1] });
}
/** W1 rail (ART §5 Faz 2 tur 2): 8 px steel + 2 px light line, 4 × 12 sleeper notch every 40 px; tiles horizontally. */
function railSeg() {
  let b = `<rect x="0" y="4" width="120" height="8" fill="#3F454D"/><rect x="0" y="4" width="120" height="2" fill="#C9CED5"/><rect x="0" y="10" width="120" height="2" fill="#23272C"/>`;
  for (const x of [18, 58, 98]) b += `<rect x="${x}" y="0" width="4" height="16" rx="1.5" fill="#2B3036"/><rect x="${x}" y="0" width="2" height="16" fill="#6E7681"/>`;
  return doc(120, 16, '', b, { scale: 1.5 });
}
/** Crane area band (K-05): 2 rows of sky, dashed lower bound; tiles horizontally. */
function craneBand() {
  const g = uid('cb');
  const defs = lin(g, [[0, '#FFFFFF', 0.04], [1, '#FFFFFF', 0.12]]);
  let b = `<rect width="120" height="240" fill="url(#${g})"/>`;
  b += `<path d="M4 238H22M34 238H52M64 238H82M94 238H112" stroke="#FFFFFF" stroke-opacity="0.45" stroke-width="4" stroke-linecap="round"/>`;
  b += `<path d="M0 120H120" stroke="#FFFFFF" stroke-opacity="0.05" stroke-width="2"/>`;
  return doc(120, 240, defs, b, { scale: 1 });
}
/** Crane hook ornament / crane booster hook: cable, pulley block, steel J hook. Pivot top centre (hangs). */
function craneHook() {
  const steel = uid('hs');
  const org = uid('ho');
  const defs =
    lin(steel, [[0, '#E3E8EE'], [0.45, '#A3ABB5'], [1, '#5C646E']], 0, 0, 1, 0) +
    lin(org, [[0, '#FFC46B'], [0.5, '#FF9A1F'], [1, '#C46A00']], 0, 0, 1, 0);
  let b = `<rect x="45" y="0" width="6" height="58" fill="#3F454D"/><rect x="45" y="0" width="2" height="58" fill="#8A96A3"/>`;
  b += `<path d="${rr(22, 52, 52, 46, 12)}" fill="#7A3A00"/><path d="${rr(25, 55, 46, 38, 10)}" fill="url(#${org})"/><path d="${rr(28, 57, 40, 9, 4.5)}" fill="#FFFFFF" fill-opacity="0.35"/>`;
  b += `<circle cx="48" cy="75" r="9" fill="#3F454D"/><circle cx="48" cy="75" r="5" fill="#C9CED5"/><circle cx="46.5" cy="73.5" r="1.8" fill="#FFFFFF"/>`;
  b += `<rect x="42" y="96" width="12" height="16" fill="url(#${steel})" stroke="#3F454D" stroke-width="3"/>`;
  b += `<path d="M48 110V128Q48 152 30 152Q12 152 12 136Q12 126 20 122" fill="none" stroke="#3F454D" stroke-width="17" stroke-linecap="round"/>`;
  b += `<path d="M48 110V128Q48 152 30 152Q12 152 12 136Q12 126 20 122" fill="none" stroke="url(#${steel})" stroke-width="11" stroke-linecap="round"/>`;
  b += `<path d="M44 116V128Q44 146 30 146" fill="none" stroke="#FFFFFF" stroke-opacity="0.55" stroke-width="3" stroke-linecap="round"/>`;
  return doc(96, 160, defs, b, { scale: 1.5, pivot: [0.5, 1] });
}
/** Board tray (ART_REFERENCE §5.3, 48 px frame): 9-slice, transparent hole. */
function boardTray() {
  const W = 240;
  const H = 240;
  const f = uid('tf');
  const defs = lin(f, [[0, '#5247B8'], [1, '#3F35A0']]);
  const hole = rr(48, 48, W - 96, H - 96 - 16, 20);
  let b = `<path d="${rr(0, 10, W, H - 10, 40)}" fill="#000000" fill-opacity="0.25"/>`;
  b += `<path fill-rule="evenodd" d="${rr(0, 0, W, H, 40)}${hole}" fill="#221B66"/>`;
  b += `<path fill-rule="evenodd" d="${rr(8, 8, W - 16, H - 16 - 24 + 8, 34)}${hole}" fill="url(#${f})"/>`;
  b += `<path d="${rr(12, 10, W - 24, 6, 3)}" fill="#7E73E0" fill-opacity="0.6"/>`;
  b += `<path d="${rr(46, 46, W - 92, H - 92 - 16, 22)}" fill="none" stroke="#2E2778" stroke-width="4"/>`;
  b += `<path d="${rr(50, 50, W - 100, H - 100 - 16, 18)}" fill="none" stroke="#000000" stroke-opacity="0.22" stroke-width="4"/>`;
  // bolts
  for (const [x, y] of [[24, 24], [W - 24, 24], [24, H - 40], [W - 24, H - 40]]) b += `<circle cx="${x}" cy="${y + 1.5}" r="6" fill="#221B66"/><circle cx="${x}" cy="${y}" r="5" fill="#8C80F0"/><circle cx="${x - 1.5}" cy="${y - 1.5}" r="1.8" fill="#FFFFFF" fill-opacity="0.8"/>`;
  return doc(W, H, defs, b, { scale: 1.5, border: [72, 72, 72, 88] });
}
function scaffoldPipe() {
  const g = uid('sp');
  return doc(16, 120, lin(g, [[0, '#C6CED6'], [0.5, '#A3ABB5'], [1, '#6E7A87']], 0, 0, 1, 0), `<rect x="0" width="16" height="120" fill="${INK}"/><rect x="3" width="10" height="120" fill="url(#${g})"/><rect x="5" width="2" height="120" fill="#FFFFFF" fill-opacity="0.55"/>`, { scale: 1.5 });
}
function beamSeg() {
  const g = uid('bs');
  return doc(120, 16, lin(g, [[0, '#C6CED6'], [0.5, '#A3ABB5'], [1, '#6E7A87']]), `<rect y="0" width="120" height="16" fill="${INK}"/><rect y="3" width="120" height="10" fill="url(#${g})"/><rect y="5" width="120" height="2" fill="#FFFFFF" fill-opacity="0.55"/>`, { scale: 1.5 });
}
function scaffoldClamp() {
  const g = uid('sc');
  return doc(28, 24, lin(g, [[0, '#FFBE5C'], [1, '#C46A00']]), `<path d="${rr(1.5, 1.5, 25, 21, 5)}" fill="url(#${g})" stroke="#8A4A00" stroke-width="3"/><rect x="5" y="4" width="18" height="4" rx="2" fill="#FFFFFF" fill-opacity="0.45"/><circle cx="14" cy="14" r="3" fill="#8A4A00"/>`, { scale: 1.5 });
}

/** Delivery truck, side view, cab right, empty cargo bed (blocks are drawn by the game on `truck_bed`). */
function truck() {
  const body = uid('tb');
  const bed = uid('td');
  const win = uid('tw');
  const tyre = uid('ty');
  const defs =
    lin(body, [[0, '#FFE07A'], [0.45, '#FFC21A'], [1, '#D99A00']]) +
    lin(bed, [[0, '#FF9A50'], [1, '#D9531E']]) +
    lin(win, [[0, '#BFE6FF'], [1, '#5FA8E8']], 0, 0, 1, 1) +
    rad(tyre, [[0, '#5A5F66'], [0.7, '#2B2F35'], [1, '#15171A']]);
  let b = contactShadow(180, 190, 160, 10, 0.3);
  // chassis
  b += `<path d="${rr(20, 140, 320, 26, 8)}" fill="#3F454D"/>`;
  // cargo bed (open box)
  b += `<path d="${rr(14, 72, 200, 76, 12)}" fill="#7A2E10"/><path d="${rr(19, 77, 190, 66, 9)}" fill="url(#${bed})"/>`;
  b += `<path d="M24 92H204M24 112H204M24 132H204" stroke="#B8441A" stroke-opacity="0.55" stroke-width="3"/><path d="${rr(22, 79, 184, 8, 4)}" fill="#FFFFFF" fill-opacity="0.35"/>`;
  b += `<rect x="30" y="66" width="10" height="14" rx="3" fill="#7A2E10"/><rect x="188" y="66" width="10" height="14" rx="3" fill="#7A2E10"/>`;
  // cab
  b += `<path d="M222 150V70Q222 52 240 52H292Q306 52 314 66L336 104Q342 114 342 128V150Z" fill="#8A5A00"/>`;
  b += `<path d="M227 146V72Q227 57 241 57H290Q302 57 309 69L330 106Q336 116 336 128V146Z" fill="url(#${body})"/>`;
  b += `<path d="M248 66H286Q295 66 300 75L314 102H248Z" fill="url(#${win})" stroke="#2B3A55" stroke-width="4" stroke-linejoin="round"/><path d="M256 72L268 72L254 96H250Z" fill="#FFFFFF" fill-opacity="0.6"/>`;
  b += `<path d="M232 66Q232 60 238 60H252" fill="none" stroke="#FFFFFF" stroke-opacity="0.6" stroke-width="5" stroke-linecap="round"/>`;
  b += `<rect x="252" y="118" width="22" height="5" rx="2.5" fill="#8A5A00"/><path d="M226 128H336" stroke="#FFFFFF" stroke-width="7"/><path d="M226 128H336" stroke="#FF8A1F" stroke-width="7" stroke-dasharray="14 8"/><path d="M226 124.5H336" stroke="#8A5A00" stroke-opacity="0.4" stroke-width="1.5"/>`;
  // headlight + bumper + beacon
  b += `<path d="${rr(326, 112, 14, 12, 4)}" fill="#FFF6B0" stroke="#8A5A00" stroke-width="2.5"/><path d="${rr(318, 146, 30, 12, 5)}" fill="#C9CED5" stroke="#5C646E" stroke-width="3"/>`;
  b += `<path d="M258 52V44Q258 36 266 36H272Q280 36 280 44V52Z" fill="#FF6A1A" stroke="#8A2E00" stroke-width="3"/><path d="M263 42Q264 39 268 39" stroke="#FFFFFF" stroke-width="3" stroke-linecap="round"/>`;
  // wheels
  for (const x of [70, 170, 296]) {
    b += `<circle cx="${x}" cy="164" r="26" fill="url(#${tyre})"/><circle cx="${x}" cy="164" r="13" fill="#C9CED5" stroke="#5C646E" stroke-width="3"/><circle cx="${x}" cy="164" r="4" fill="#5C646E"/><path d="M${x - 16} ${148}A22 22 0 0 1 ${x + 6} ${142}" fill="none" stroke="#FFFFFF" stroke-opacity="0.25" stroke-width="4" stroke-linecap="round"/>`;
  }
  return doc(360, 200, defs, b, { scale: 1.5, pivot: [0.5, 0.05] });
}

export function frames() {
  return [
    { cat: 'board', name: 'floor_yard_a', svg: () => yardFloor(false) },
    { cat: 'board', name: 'floor_yard_b', svg: () => yardFloor(true) },
    { cat: 'board', name: 'floor_site', svg: siteFloor },
    { cat: 'board', name: 'floor_site_deep', svg: siteDeep },
    { cat: 'board', name: 'wall_seg', svg: wallSeg },
    { cat: 'board', name: 'wall_cap', svg: wallCap },
    { cat: 'board', name: 'gap_seg', svg: gapSeg },
    { cat: 'board', name: 'gap_edge', svg: gapEdge },
    { cat: 'board', name: 'rail_seg', svg: railSeg },
    { cat: 'board', name: 'crane_band', svg: craneBand },
    { cat: 'board', name: 'crane_hook', svg: craneHook },
    { cat: 'board', name: 'board_tray', svg: boardTray },
    { cat: 'board', name: 'scaffold_pipe', svg: scaffoldPipe },
    { cat: 'board', name: 'scaffold_clamp', svg: scaffoldClamp },
    { cat: 'board', name: 'beam_seg', svg: beamSeg },
    { cat: 'board', name: 'truck', svg: truck },
  ];
}
