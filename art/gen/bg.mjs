// v4 backgrounds (ART_REFERENCE §7: depth layers 1–8, "sakin orta"; §2.1 skies). 1080 × 1920 design.
import { mul, mix, rr, lin, rad, uid, doc, r1, contactShadow } from './lib.mjs';

const W = 1080;
const H = 1920;

function cloud(x, y, s, a = 1, tint = '#C9D9F5') {
  const g = uid('cl');
  const blobs = [[0, 0, 70], [70, -30, 86], [150, 0, 66], [-60, 20, 50], [210, 24, 46]];
  let o = `<g transform="translate(${x} ${y}) scale(${s})" opacity="${a}">`;
  for (const [bx, by, r] of blobs) o += `<circle cx="${bx}" cy="${by + 10}" r="${r}" fill="${tint}"/>`;
  o += `<rect x="-110" y="20" width="370" height="60" rx="30" fill="${tint}"/>`;
  for (const [bx, by, r] of blobs) o += `<circle cx="${bx}" cy="${by}" r="${r}" fill="url(#${g})"/>`;
  o += `<rect x="-110" y="12" width="370" height="56" rx="28" fill="url(#${g})"/>`;
  o += `<ellipse cx="40" cy="-40" rx="60" ry="22" fill="#FFFFFF" fill-opacity="0.7"/></g>`;
  return { defs: lin(g, [[0, '#FFFFFF'], [0.65, '#F4F8FF'], [1, '#DCE7FA']], 0, 0, 0, 1), body: o };
}
function house(x, y, w, h, roof, body = '#FFF4D6', opts = {}) {
  const rf = uid('hr');
  const bd = uid('hb');
  const defs = lin(rf, [[0, mix(roof, '#FFFFFF', 0.25)], [0.6, roof], [1, mul(roof, 0.75)]]) + lin(bd, [[0, body], [1, mul(body, 0.88)]], 0, 0, 1, 0);
  const ink = mul(roof, 0.45);
  const rh = h * 0.62;
  let o = `<g>`;
  o += `<rect x="${x}" y="${y}" width="${w}" height="${h}" fill="url(#${bd})" stroke="${mul(body, 0.55)}" stroke-width="4"/>`;
  o += `<rect x="${x + w - w * 0.22}" y="${y}" width="${w * 0.22}" height="${h}" fill="#7E8CC8" fill-opacity="0.25"/>`;
  // windows
  const ww = Math.max(10, w * 0.2);
  for (const [fx, fy] of opts.windows ?? [[0.22, 0.3], [0.58, 0.3]]) o += `<rect x="${r1(x + w * fx)}" y="${r1(y + h * fy)}" width="${r1(ww)}" height="${r1(ww * 1.15)}" rx="3" fill="#5FA8E8" stroke="${mul(body, 0.5)}" stroke-width="3"/><rect x="${r1(x + w * fx + 2)}" y="${r1(y + h * fy + 2)}" width="${r1(ww * 0.4)}" height="${r1(ww * 0.5)}" fill="#FFFFFF" fill-opacity="0.6"/>`;
  if (opts.door) o += `<path d="${rr(x + w * 0.4, y + h * 0.58, w * 0.22, h * 0.42, w * 0.1)}" fill="${mul(roof, 0.7)}" stroke="${ink}" stroke-width="3"/>`;
  // roof
  o += `<path d="M${x - 12} ${y + 4}L${x + w / 2} ${y - rh}L${x + w + 12} ${y + 4}Z" fill="url(#${rf})" stroke="${ink}" stroke-width="5" stroke-linejoin="round"/>`;
  o += `<path d="M${x + 2} ${y - 4}L${x + w / 2} ${y - rh + 12}" stroke="#FFFFFF" stroke-opacity="0.45" stroke-width="5" stroke-linecap="round"/>`;
  if (opts.chimney) o += `<rect x="${x + w * 0.7}" y="${y - rh * 0.75}" width="${w * 0.14}" height="${rh * 0.4}" fill="${mul(roof, 0.8)}" stroke="${ink}" stroke-width="3"/>`;
  o += `</g>`;
  return { defs, body: o };
}
function tree(x, y, s, c = '#4FA457') {
  const g = uid('tr');
  const defs = rad(g, [[0, mix(c, '#FFFFFF', 0.3)], [0.5, c], [1, mul(c, 0.7)]], 0.38, 0.3, 0.75);
  let o = `<g transform="translate(${x} ${y}) scale(${s})"><rect x="-7" y="-10" width="14" height="44" rx="5" fill="#8A5A2B"/>`;
  o += `<circle cx="-22" cy="-30" r="30" fill="url(#${g})"/><circle cx="22" cy="-32" r="28" fill="url(#${g})"/><circle cx="0" cy="-58" r="34" fill="url(#${g})"/><ellipse cx="-10" cy="-74" rx="14" ry="7" fill="#FFFFFF" fill-opacity="0.35"/></g>`;
  return { defs, body: o };
}

function home() {
  const parts = [];
  const sky = uid('sk');
  const sun = uid('sn');
  const haze = uid('hz');
  const far = uid('fa');
  const hill = uid('hl');
  const hill2 = uid('h2');
  const mead = uid('md');
  const path = uid('pa');
  const vig = uid('vg');
  let defs =
    lin(sky, [[0, '#1B49A4'], [0.3, '#2B5FB6'], [0.48, '#7AA3D8'], [0.6, '#CFE3F5'], [1, '#E6F1FA']]) +
    rad(sun, [[0, '#FFF6D0', 0.85], [0.4, '#FFE7A3', 0.35], [1, '#FFE7A3', 0]]) +
    lin(haze, [[0, '#FFFFFF', 0], [1, '#FFFFFF', 0.35]]) +
    lin(far, [[0, '#A9C2EC'], [1, '#93AEDF']]) +
    lin(hill, [[0, '#8ED36F'], [0.5, '#62B455'], [1, '#3F8E45']]) +
    lin(hill2, [[0, '#7CC46A'], [1, '#4C9A4F']]) +
    lin(mead, [[0, '#9ADB78'], [0.4, '#79C562'], [1, '#4E9E4A']]) +
    lin(path, [[0, '#F3DDA8'], [1, '#E2BD80']]) +
    rad(vig, [[0, '#000000', 0], [0.7, '#000000', 0], [1, '#0A0830', 0.35]], 0.5, 0.45, 0.75);
  let b = `<rect width="${W}" height="${H}" fill="url(#${sky})"/>`;
  b += `<circle cx="820" cy="400" r="380" fill="url(#${sun})"/><circle cx="820" cy="400" r="70" fill="#FFF8DA" fill-opacity="0.9"/>`;
  for (const c of [cloud(150, 330, 1.0, 0.95), cloud(700, 640, 0.8, 0.9), cloud(420, 820, 0.55, 0.75), cloud(980, 250, 0.6, 0.85)]) {
    defs += c.defs;
    b += c.body;
  }
  // far hills (air perspective)
  b += `<path d="M0 1010Q120 900 260 960Q400 1010 520 930Q660 840 800 930Q940 1000 1080 920V1300H0Z" fill="url(#${far})"/>`;
  b += `<rect y="860" width="${W}" height="200" fill="url(#${haze})"/>`;
  // Renkli Tepe: the colourful hill town (right) + left wooded hill
  b += `<path d="M-40 1120Q100 1000 260 1060Q340 1090 420 1130V1400H-40Z" fill="url(#${hill2})"/>`;
  b += `<path d="M300 1180Q420 1020 600 960Q760 910 900 980Q1020 1040 1120 1120V1400H300Z" fill="url(#${hill})"/>`;
  b += `<path d="M330 1172Q440 1030 600 975Q760 924 900 992" fill="none" stroke="#B8F09A" stroke-opacity="0.6" stroke-width="8" stroke-linecap="round"/>`;
  const houses = [
    [560, 1000, 70, 60, '#E8473B', { chimney: true }],
    [650, 960, 64, 70, '#2984DE', { windows: [[0.22, 0.25], [0.58, 0.25]], door: true }],
    [740, 975, 72, 62, '#FFC93C', {}],
    [830, 1000, 66, 64, '#8E44D9', { chimney: true }],
    [470, 1060, 64, 58, '#FF9A1F', { door: true }],
    [910, 1050, 64, 58, '#2AB3A0', {}],
    [610, 1065, 70, 60, '#3FC35A', { door: true }],
    [710, 1060, 64, 56, '#E8473B', {}],
    [800, 1080, 70, 58, '#FF9A1F', { door: true }],
  ];
  for (const [x, y, w, h, roof, opts] of houses) {
    const hh = house(x, y, w, h, roof, '#FFF4D6', opts);
    defs += hh.defs;
    b += hh.body;
  }
  // hilltop tower crane (construction theme landmark)
  b += `<g transform="translate(700 760)"><path d="M0 0V200M24 0V200" stroke="#C46A00" stroke-width="7"/><path d="M0 20L24 40M24 20L0 40M0 60L24 80M24 60L0 80M0 100L24 120M24 100L0 120M0 140L24 160M24 140L0 160" stroke="#E08A00" stroke-width="4"/><path d="M-120 0H190" stroke="#C46A00" stroke-width="10" stroke-linecap="round"/><path d="M-120 0H190" stroke="#FFB13D" stroke-width="5" stroke-linecap="round"/><path d="M12 0L12 -40L-120 0M12 -40L190 0" fill="none" stroke="#8A4A00" stroke-width="3"/><path d="M150 0V70" stroke="#3F454D" stroke-width="3"/><rect x="138" y="70" width="24" height="18" rx="3" fill="#E8473B" stroke="#7A1C16" stroke-width="3"/><rect x="-118" y="4" width="40" height="22" rx="3" fill="#6E7681"/></g>`;
  // trees on the left hill and among the houses
  for (const t of [tree(60, 1090, 1.0, '#4FA457'), tree(160, 1060, 0.85, '#3F9A5C'), tree(250, 1100, 0.9, '#5DB04F'), tree(990, 1070, 0.8, '#3F9A5C'), tree(430, 1120, 0.7, '#5DB04F')]) {
    defs += t.defs;
    b += t.body;
  }
  // meadow + path (calm middle: nothing behind the character group / play button)
  b += `<path d="M0 1240Q300 1180 560 1210Q820 1240 1080 1200V${H}H0Z" fill="url(#${mead})"/>`;
  b += `<path d="M0 1240Q300 1180 560 1210Q820 1240 1080 1200" fill="none" stroke="#C6F2A6" stroke-opacity="0.7" stroke-width="10"/>`;
  b += `<path d="M640 1215Q610 1300 700 1380Q820 1480 760 1640Q730 1760 820 ${H}H1000Q900 1760 940 1640Q990 1480 860 1370Q760 1290 700 1214Z" fill="url(#${path})"/>`;
  b += `<path d="M700 1240Q690 1300 760 1360" fill="none" stroke="#FFFFFF" stroke-opacity="0.35" stroke-width="6" stroke-linecap="round"/>`;
  // grass tufts + flowers (seeded)
  let s = 11;
  const rnd = () => ((s = (s * 16807) % 2147483647) / 2147483647);
  for (let i = 0; i < 40; i++) {
    const x = rnd() * W;
    const y = 1290 + rnd() * 560;
    if (x > 600 && x < 1000 && y < 1700) continue;
    const c = ['#FFFFFF', '#FFE96A', '#FF8FB0', '#B8A8FF'][i % 4];
    b += `<g transform="translate(${r1(x)} ${r1(y)})"><path d="M-6 6Q-4 -4 0 -8Q4 -4 6 6" fill="#3F8E45" fill-opacity="0.6"/><circle r="5" fill="${c}"/><circle r="2" fill="#FFC21A"/></g>`;
  }
  // foreground bushes (corners, 10 % darker)
  const bush = uid('bu');
  defs += rad(bush, [[0, '#5DB04F'], [0.6, '#3F8E45'], [1, '#2B6B36']], 0.4, 0.3, 0.8);
  b += `<g fill="url(#${bush})"><circle cx="-20" cy="1560" r="130"/><circle cx="90" cy="1640" r="110"/><circle cx="1100" cy="1520" r="140"/><circle cx="990" cy="1640" r="100"/></g>`;
  b += `<g fill="#FFFFFF" fill-opacity="0.18"><ellipse cx="-10" cy="1470" rx="60" ry="20"/><ellipse cx="1060" cy="1420" rx="60" ry="20"/></g>`;
  b += `<rect width="${W}" height="${H}" fill="url(#${vig})"/>`;
  return doc(W, H, defs, b, { scale: 0.75 });
}

function level() {
  const glow = uid('lg');
  const glow2 = uid('l2');
  const band = uid('lb');
  const vig = uid('lv');
  const defs =
    rad(glow, [[0, '#2B2661'], [1, '#2B2661', 0]], 0.5, 0.26, 0.6) +
    rad(glow2, [[0, '#3A2E85', 0.55], [1, '#3A2E85', 0]], 0.5, 0.62, 0.55) +
    lin(band, [[0, '#0B0920', 0], [1, '#0B0920', 0.45]]) +
    rad(vig, [[0, '#000000', 0], [0.65, '#000000', 0], [1, '#000000', 0.4]], 0.5, 0.45, 0.8);
  let b = `<rect width="${W}" height="${H}" fill="#1A173C"/><rect width="${W}" height="${H}" fill="url(#${glow})"/><rect width="${W}" height="${H}" fill="url(#${glow2})"/>`;
  // blueprint grid α 0,02
  let grid = '';
  for (let x = 0; x <= W; x += 120) grid += `M${x} 0V${H}`;
  for (let y = 0; y <= H; y += 120) grid += `M0 ${y}H${W}`;
  b += `<path d="${grid}" stroke="#FFFFFF" stroke-opacity="0.025" stroke-width="2"/>`;
  // edge silhouettes α 0,06: tower crane (left), scaffold block (right)
  b += `<g fill="none" stroke="#FFFFFF" stroke-opacity="0.07" stroke-width="6"><path d="M70 1920V520M110 1920V520M70 560L110 600M110 560L70 600M70 640L110 680M110 640L70 680M70 720L110 760M110 720L70 760M30 520H330M90 520L90 470L30 520M90 470L330 520M280 520V600"/><path d="M960 1920V980M1040 1920V980M960 1060H1080M960 1160H1080M960 1260H1080M960 1360H1080M960 1060L1040 1160M960 1160L1040 1260"/></g>`;
  b += `<g fill="#FFFFFF" fill-opacity="0.04"><circle cx="190" cy="300" r="4"/><circle cx="860" cy="210" r="3"/><circle cx="950" cy="420" r="4"/><circle cx="320" cy="160" r="3"/><circle cx="640" cy="120" r="3"/></g>`;
  b += `<rect y="1480" width="${W}" height="440" fill="url(#${band})"/>`;
  b += `<rect width="${W}" height="${H}" fill="url(#${vig})"/>`;
  return doc(W, H, defs, b, { scale: 0.375 });
}

export function frames() {
  return [
    { cat: 'bg', name: 'bg_home', svg: home },
    { cat: 'bg', name: 'bg_level', svg: level },
  ];
}
