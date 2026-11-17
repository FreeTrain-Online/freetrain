import { equal } from 'node:assert/strict';
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';

const web = resolve(import.meta.dirname!, '..');
const root = mkdtempSync(resolve(web, '../tmp/browser-sessions-'));
type Result = { title: string; status: string; retry: number; workerIndex: number };
type Summary = { status: string; results: Result[] };
const runs: { workers: number; iteration: number; directory: string; status: string; results: number }[] = [];
console.log(`Session acceptance artifacts: ${root}`);
function record() { writeFileSync(join(root, 'acceptance.json'), JSON.stringify({ root, runs }, null, 2)); }
async function run(label: string, args: string[], expected: number, workers: number, iteration: number) {
  const directory = join(root, label); mkdirSync(directory);
  const child = await new Deno.Command(Deno.execPath(), {
    cwd: web,
    env: { FREETRAIN_BROWSER_ARTIFACT_ROOT: directory, FREETRAIN_ONLINE_BROWSER_EVIDENCE: join(directory, 'evidence') },
    args: ['run', '-A', 'npm:@playwright/test@1.64.1', 'test', ...args, `--workers=${workers}`, '--retries=0'],
    stdout: 'piped', stderr: 'piped',
  }).output();
  writeFileSync(join(directory, 'console.log'), new TextDecoder().decode(child.stdout) + new TextDecoder().decode(child.stderr));
  const summary = JSON.parse(readFileSync(join(directory, 'summary.json'), 'utf8')) as Summary;
  runs.push({ workers, iteration, directory, status: summary.status, results: summary.results.length }); record();
  equal(child.code, 0, `Browser run failed: ${directory}/console.log`);
  equal(summary.status, 'passed'); equal(summary.results.length, expected);
  for (const result of summary.results) { equal(result.status, 'passed', result.title); equal(result.retry, 0, result.title); }
  console.log(`${label}: ${expected} passed, zero skips/retries; ${directory}`);
}
await run('application-harness', ['--config', 'tests/browser-harness/session.config.ts'], 3, 1, 0);
await run('legacy-adapters', ['station-building-domain.spec.ts', 'passenger-feedback.spec.ts', 'passenger-service.spec.ts'], 5, 1, 0);
// Independent run roots, fresh fixture generation and managed workers on every
// invocation. The two worker-count series may run concurrently; cases within
// each series keep the configured worker limit and ordinary per-file ordering.
const series = await Promise.allSettled([1, 2].map(async workers => {
  for (let iteration = 1; iteration <= 10; iteration++) await run(`workers-${workers}-run-${iteration}`, ['station-continuation.spec.ts', 'station-controller.spec.ts', '--grep', 'complete journey|P6-22'], 10, workers, iteration);
}));
record();
for (const result of series) if (result.status === 'rejected') throw result.reason;
equal(runs.length, 22);
console.log(`Session acceptance passed: 200 station cases, 5 migrated regressions and 3 observation controls; ${join(root, 'acceptance.json')}`);
