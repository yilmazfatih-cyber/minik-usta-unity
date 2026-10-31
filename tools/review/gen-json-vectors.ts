/**
 * Skeptic review (REVIEW_PHASE2 R-05): JSON number / string vectors from the TS runtime for the C# `JsNumber`,
 * `JsonReader`, `JsonWriter` and `Fnv1a` (canonicalJson, eventLogHash, levelHash). .NET 8 passes them; the point is the
 * Unity player (Mono / IL2CPP): `JsNumber.Format` relies on `double.ToString("R")` being the shortest round-trip form and
 * `JsonReader` on `double.TryParse` being correctly rounded, which only .NET Core 3.0+ guarantees.
 *
 * Usage: node tools/review/gen-json-vectors.ts [/path/to/minik-usta] > Assets/MinikUsta/Core.Tests/Golden/review_json_vectors.json
 */
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const WEB = resolve(process.argv[2] ?? join(import.meta.dirname, '..', '..', '..', 'minik-usta'));
const web = (p: string) => import(pathToFileURL(join(WEB, p)).href);
const { mulberry32 } = await web('src/core/rng.ts');
const { canonicalJson, fnv1a64 } = await web('src/core/moves.ts');

const rng = mulberry32(20261031);
const u32 = (): number => rng.nextU32();
const hex8 = (n: number): string => (n >>> 0).toString(16).padStart(8, '0');
const view = new DataView(new ArrayBuffer(8));

// 1) doubles: random bit patterns (finite), random "game sized" values, and the classic edge cases
const doubles: { bits: string; text: string }[] = [];
const pushDouble = (x: number): void => {
  view.setFloat64(0, x);
  doubles.push({ bits: hex8(view.getUint32(0)) + hex8(view.getUint32(4)), text: JSON.stringify(x) });
};
for (const x of [0, -0, 1, -1, 0.1, 0.2, 0.1 + 0.2, 1 / 3, 2 / 3, 1e21, 1e-7, 1e-6, 5e-324, 2.2250738585072014e-308,
  1.7976931348623157e308, 2 ** 53, 2 ** 53 + 2, 123456789012345680000, 9007199254740993, 0.000001234, 4.35, 0.6, 0.59999999999999998,
  100, 1e20, 1e22, 1.5e-7, 12345.678, 3 / 5, 7 / 12, 0.05, 1 - 1e-16]) pushDouble(x);
while (doubles.length < 400) {
  view.setUint32(0, u32());
  view.setUint32(4, u32());
  const x = view.getFloat64(0);
  if (Number.isFinite(x)) pushDouble(x);
  // ratios like YAO (overWall / total) and rates in [0, 1]
  const a = rng.nextInt(200);
  const b = 1 + rng.nextInt(200);
  pushDouble(a / b);
  pushDouble(rng.next());
}

// 2) decimal texts → JSON.parse → JSON.stringify
const texts: { text: string; out: string }[] = [];
for (const t of ['1E2', '-0', '0.1', '1e-7', '9007199254740993', '1.0', '12345678901234567890', '0.30000000000000004',
  '2.2250738585072011e-308', '4.9406564584124654e-324', '1.7976931348623157e308', '0.1e1', '-12.5e-3', '1e+21']) {
  texts.push({ text: t, out: JSON.stringify(JSON.parse(t)) });
}
while (texts.length < 300) {
  const digits = 1 + rng.nextInt(20);
  let m = String(1 + rng.nextInt(9));
  for (let i = 1; i < digits; i++) m += String(rng.nextInt(10));
  if (rng.nextInt(2) === 0) m = m.slice(0, Math.max(1, m.length - 3)) + '.' + m.slice(Math.max(1, m.length - 3)) + '1';
  let s = (rng.nextInt(2) === 0 ? '' : '-') + m;
  if (rng.nextInt(3) === 0) s += 'e' + (rng.nextInt(2) === 0 ? '-' : '') + String(rng.nextInt(300));
  texts.push({ text: s, out: JSON.stringify(JSON.parse(s)) });
}

// 3) strings: canonicalJson quoting + fnv1a64 (UTF-8 by hand, lone surrogates as 3 bytes)
const POOL = ['a', 'Z', '0', ' ', '"', '\\', '/', '\b', '\f', '\n', '\r', '\t', '\u0000', '\u001f', '\u007f', 'ç', 'ğ',
  'ı', 'İ', 'ö', 'ş', 'ü', ' ', ' ', '😀', '\ud800', '\udfff', '﻿', '￿', '٠'];
const strings: { s: string; json: string; fnv: string }[] = [];
for (let n = 0; n < 200; n++) {
  let s = '';
  const len = rng.nextInt(12);
  for (let i = 0; i < len; i++) s += POOL[rng.nextInt(POOL.length)];
  strings.push({ s: JSON.stringify(s), json: canonicalJson(s), fnv: fnv1a64(s) });
}
process.stdout.write(JSON.stringify({ generator: 'tools/review/gen-json-vectors.ts', doubles, texts, strings }) + '\n');
