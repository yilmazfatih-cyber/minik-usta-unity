/**
 * Lossless PNG re-encoder for the art tools (no dependency): decodes an 8-bit RGBA / RGB non-interlaced PNG (Chromium's
 * canvas output), re-filters every row with the minimum-sum-of-absolute-differences heuristic and deflates at level 9.
 * Typically 30–40 % smaller than canvas.toDataURL output; pixels are identical.
 */
import { deflateSync, inflateSync, crc32 } from 'node:zlib';

const SIG = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);

function chunks(buf) {
  const out = [];
  let o = 8;
  while (o < buf.length) {
    const len = buf.readUInt32BE(o);
    const type = buf.toString('latin1', o + 4, o + 8);
    out.push({ type, data: buf.subarray(o + 8, o + 8 + len) });
    o += 12 + len;
  }
  return out;
}
function chunk(type, data) {
  const head = Buffer.alloc(8);
  head.writeUInt32BE(data.length, 0);
  head.write(type, 4, 'latin1');
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(Buffer.concat([head.subarray(4), data])) >>> 0, 0);
  return Buffer.concat([head, data, crc]);
}
const paeth = (a, b, c) => {
  const p = a + b - c;
  const pa = Math.abs(p - a);
  const pb = Math.abs(p - b);
  const pc = Math.abs(p - c);
  return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
};

export function optimizePng(buf) {
  if (!buf.subarray(0, 8).equals(SIG)) return buf;
  const cs = chunks(buf);
  const ih = cs.find((c) => c.type === 'IHDR').data;
  const w = ih.readUInt32BE(0);
  const h = ih.readUInt32BE(4);
  const depth = ih[8];
  const ctype = ih[9];
  const interlace = ih[12];
  if (depth !== 8 || interlace !== 0 || (ctype !== 6 && ctype !== 2)) return buf;
  const bpp = ctype === 6 ? 4 : 3;
  const stride = w * bpp;
  const raw = inflateSync(Buffer.concat(cs.filter((c) => c.type === 'IDAT').map((c) => c.data)));
  // unfilter
  const px = Buffer.alloc(stride * h);
  for (let y = 0; y < h; y++) {
    const f = raw[y * (stride + 1)];
    const src = y * (stride + 1) + 1;
    const dst = y * stride;
    for (let x = 0; x < stride; x++) {
      const a = x >= bpp ? px[dst + x - bpp] : 0;
      const b = y > 0 ? px[dst - stride + x] : 0;
      const c = x >= bpp && y > 0 ? px[dst - stride + x - bpp] : 0;
      const v = raw[src + x];
      px[dst + x] = (f === 0 ? v : f === 1 ? v + a : f === 2 ? v + b : f === 3 ? v + ((a + b) >> 1) : v + paeth(a, b, c)) & 255;
    }
  }
  // re-filter (adaptive)
  const out = Buffer.alloc((stride + 1) * h);
  const cand = [0, 1, 2, 3, 4].map(() => Buffer.alloc(stride));
  for (let y = 0; y < h; y++) {
    const row = y * stride;
    let best = 0;
    let bestSum = Infinity;
    for (let f = 0; f < 5; f++) {
      const cb = cand[f];
      let sum = 0;
      for (let x = 0; x < stride; x++) {
        const v = px[row + x];
        const a = x >= bpp ? px[row + x - bpp] : 0;
        const b = y > 0 ? px[row - stride + x] : 0;
        const c = x >= bpp && y > 0 ? px[row - stride + x - bpp] : 0;
        const r = (f === 0 ? v : f === 1 ? v - a : f === 2 ? v - b : f === 3 ? v - ((a + b) >> 1) : v - paeth(a, b, c)) & 255;
        cb[x] = r;
        sum += r < 128 ? r : 256 - r;
        if (sum >= bestSum) break;
      }
      if (sum < bestSum) {
        bestSum = sum;
        best = f;
      }
    }
    // recompute the winner fully (the loop above may have stopped early for it only when it lost)
    const cb = cand[best];
    for (let x = 0; x < stride; x++) {
      const v = px[row + x];
      const a = x >= bpp ? px[row + x - bpp] : 0;
      const b = y > 0 ? px[row - stride + x] : 0;
      const c = x >= bpp && y > 0 ? px[row - stride + x - bpp] : 0;
      cb[x] = (best === 0 ? v : best === 1 ? v - a : best === 2 ? v - b : best === 3 ? v - ((a + b) >> 1) : v - paeth(a, b, c)) & 255;
    }
    out[y * (stride + 1)] = best;
    cb.copy(out, y * (stride + 1) + 1);
  }
  const idat = deflateSync(out, { level: 9, memLevel: 9 });
  const res = Buffer.concat([SIG, chunk('IHDR', ih), chunk('IDAT', idat), chunk('IEND', Buffer.alloc(0))]);
  return res.length < buf.length ? res : buf;
}
