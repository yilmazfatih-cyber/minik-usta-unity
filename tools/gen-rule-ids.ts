/**
 * Writes Assets/MinikUsta/Core.Tests/Golden/rule_ids.json: every rule id the web repo's coverage gate requires
 * (tools/rule-coverage.ts `ruleIds`, read from docs/GDD.md, docs/TECH_DESIGN.md §12.4 and docs/OBSTACLES.md) with its
 * kind (K / E / obstacle / N) and phase (2R = 2.5). RuleCoverageTests.cs applies the same gate to the C# test names
 * (`--phase 2R`: 76 ids at web b31c4e8). The documents stay in the web repo; this file is their extract.
 *
 * Usage: node tools/gen-rule-ids.ts [/path/to/minik-usta]   (node 22 type stripping; no extra packages)
 */
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { execSync } from 'node:child_process';

const HERE = dirname(fileURLToPath(import.meta.url));
const ROOT = resolve(HERE, '..');
const WEB = resolve(process.argv.slice(2).find((a) => !a.startsWith('--')) ?? join(ROOT, '..', 'minik-usta'));
const rc = await import(pathToFileURL(join(WEB, 'tools', 'rule-coverage.ts')).href);
const doc = (name: string): string => readFileSync(join(WEB, 'docs', name), 'utf8');

const ids: { id: string; kind: string; phase: number }[] = rc.ruleIds({
  gdd: doc('GDD.md'),
  tech: doc('TECH_DESIGN.md'),
  obstacles: doc('OBSTACLES.md'),
});
const KIND_ORDER = ['K', 'obstacle', 'E', 'N'];
ids.sort((a, b) => KIND_ORDER.indexOf(a.kind) - KIND_ORDER.indexOf(b.kind) || a.id.localeCompare(b.id, 'en', { numeric: true }));

let webCommit = '?';
try {
  webCommit = execSync('git rev-parse --short HEAD', { cwd: WEB }).toString().trim();
} catch {
  /* not a git checkout */
}
const phase2R: number = rc.PHASE_2R;
const required = ids.filter((r) => r.phase <= phase2R);
const count = (kind: string): number => required.filter((r) => r.kind === kind).length;
const out = {
  generatedFrom: { webRepo: 'yilmazfatih-cyber/minik-usta', webCommit, generator: 'tools/gen-rule-ids.ts', source: 'tools/rule-coverage.ts ruleIds' },
  phase2R,
  required2R: { total: required.length, K: count('K'), obstacle: count('obstacle'), E: count('E'), N: count('N') },
  ids,
};
const target = join(ROOT, 'Assets', 'MinikUsta', 'Core.Tests', 'Golden', 'rule_ids.json');
// one id per line keeps the file diffable
const body = JSON.stringify({ ...out, ids: [] }, null, 1).replace(/"ids": \[\]/, `"ids": [\n${ids.map((r) => JSON.stringify(r)).join(',\n')}\n]`);
writeFileSync(target, body + '\n');
console.log(`wrote ${target} (web ${webCommit}, ${ids.length} ids, ${required.length} up to phase 2R: ${JSON.stringify(out.required2R)})`);
