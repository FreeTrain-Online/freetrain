import { equal, match, ok } from 'node:assert/strict';
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';

type Attachment = { name: string; path?: string };
type Result = {
  title: string;
  status: string;
  retry: number;
  errorPath: string;
  attachments: Attachment[];
};
const web = resolve(import.meta.dirname!, '..');
const root = mkdtempSync(resolve(web, '../tmp/browser-diagnostics-'));
const attachmentPaths = new Set<string>();
const records: object[] = [];
function text(path: string) {
  return readFileSync(path, 'utf8');
}
function attachment(result: Result, name: string) {
  const path = result.attachments.find((a) => a.name === name)?.path;
  ok(path, `Missing ${name}: ${result.title}`);
  return path;
}
async function run(label: string, mode?: string) {
  const directory = join(root, label);
  mkdirSync(directory);
  const env: Record<string, string> = {
    FREETRAIN_BROWSER_ARTIFACT_ROOT: directory,
  };
  if (mode) env.FREETRAIN_DIAGNOSTICS_MANAGED = mode;
  const run = await new Deno.Command(Deno.execPath(), {
    cwd: web,
    env,
    args: [
      'run',
      '-A',
      'npm:@playwright/test@1.64.1',
      'test',
      '--config',
      'tests/browser-harness/playwright.config.ts',
    ],
    stdout: 'piped',
    stderr: 'piped',
  }).output();
  const consoleText = new TextDecoder().decode(run.stdout) +
    new TextDecoder().decode(run.stderr);
  writeFileSync(join(directory, 'console.log'), consoleText);
  equal(
    run.code,
    1,
    `Expected deliberate failures; see ${directory}/console.log`,
  );
  const report = JSON.parse(text(join(directory, 'report.json')));
  const summary = JSON.parse(text(join(directory, 'summary.json'))) as {
    results: Result[];
  };
  equal(summary.results.length, mode ? 1 : 6);
  equal(report.stats.unexpected, mode ? 1 : 6);
  for (const block of consoleText.split(/(?=^FAILED:)/m).slice(1)) {
    const lines = block.split('\nBrowser run ')[0].trimEnd().split('\n');
    ok(
      lines.length <= 50,
      `${label}: ${lines.length} console lines in failure`,
    );
  }
  for (const result of summary.results) {
    equal(result.status, 'failed');
    equal(result.retry, 0);
    const error = text(result.errorPath);
    for (const a of result.attachments) {
      if (a.path) {
        ok(!attachmentPaths.has(a.path), `Overwritten attachment: ${a.path}`);
        attachmentPaths.add(a.path);
        ok(readFileSync(a.path).length >= 0);
      }
    }
    attachment(result, 'process-association');
    if (mode === 'setup') {
      match(error, /Process evidence:/);
      match(error, /ENOENT/);
      match(
        text(attachment(result, 'process-lifecycle.ndjson')),
        /startup-failed/,
      );
      continue;
    }
    const events = text(attachment(result, 'browser-timeline')).trim().split(
      '\n',
    ).map((l) => JSON.parse(l));
    const peers = new Set(
      events.filter((e) => e.kind === 'page-open').map((e) => e.peer),
    );
    equal(peers.size, 2);
    equal(result.attachments.filter((a) => a.name === 'screenshot').length, 2);
    const trace = attachment(result, 'trace');
    const contents = await new Deno.Command('unzip', {
      args: ['-p', trace, '*.trace'],
      stdout: 'piped',
      stderr: 'piped',
    }).output();
    equal(contents.code, 0);
    const traceText = new TextDecoder().decode(contents.stdout);
    ok(
      new Set([...traceText.matchAll(/"pageId":"([^"]+)"/g)].map((m) => m[1]))
        .size >= 2,
      'Trace must contain both pages',
    );
    if (mode) {
      for (const peer of peers) {
        ok(
          events.some((e) =>
            e.peer === peer && e.kind === 'protocol' &&
            e.worldEpoch !== undefined
          ),
          `Missing native/routed protocol metadata: ${peer}`,
        );
      }
      const lifecycle = text(attachment(result, 'process-lifecycle.ndjson'));
      if (mode === 'restart') {
        match(lifecycle, /startup-failed/);
        match(error, /Process evidence:/);
      }
      if (mode === 'restart-success') {
        match(error, /deliberate failure after successful restart/);
      }
      if (mode === 'teardown') match(error, /deliberate teardown failure/);
      const association = JSON.parse(
        text(attachment(result, 'process-association')),
      );
      const workerLifecycle = text(
        join(association.directory, 'lifecycle.ndjson'),
      );
      match(workerLifecycle, /backend/);
      match(workerLifecycle, /preview/);
    } else {
      for (const peer of peers) {
        for (
          const kind of [
            'console-error',
            'page-error',
            'request-failed',
            'socket-close',
            'protocol',
          ]
        ) {
          ok(
            events.some((e) => e.peer === peer && e.kind === kind),
            `${result.title}: missing ${kind} for ${peer}`,
          );
        }
      }
      ok(
        events.some((e) =>
          e.type === 'accepted' && e.requestId === 'diagnostic-command'
        ),
      );
      ok(
        text(attachment(result, 'protocol-payloads')).includes(
          'diagnostic-command',
        ),
      );
      const frames = text(attachment(result, 'protocol-payloads')).trim().split(
        '\n',
      ).map((line) => JSON.parse(line));
      ok(
        frames.some((frame) =>
          frame.encoding === 'base64' &&
          frame.payload === Buffer.from([0, 255, 128, 1]).toString('base64')
        ),
        'Binary payload bytes must be retained exactly',
      );
      if (result.title.endsWith(' › heat')) {
        match(error, /\$\.mixed\.city\.heat\.q\[11990\]/);
      }
      if (result.title.endsWith(' › length')) {
        match(error, /expected length 12000; actual length 11999/);
      }
      if (result.title.endsWith(' › missing')) match(error, /actual <missing>/);
      if (result.title.endsWith(' › bytes')) {
        match(error, /First differing offset=5/);
        match(error, /sha256=/);
      }
      if (result.title.endsWith(' › json')) {
        match(error, /\$\.a: expected 1; actual 2/);
      }
      if (result.title.endsWith(' › ordinary')) {
        ok(JSON.parse(error)[0].stack.split('\n').length > 1000);
        match(consoleText, /more lines in full error artifact/);
      }
    }
  }
  records.push({
    label,
    directory,
    expectedFailures: summary.results.length,
    exitCode: run.code,
  });
  console.log(
    `Verified ${label}: ${summary.results.length} deliberate failures, bounded output and retained evidence.`,
  );
}
console.log(`Diagnostics acceptance artifacts: ${root}`);
// Passing cases exercise reuse of the same worker (failed cases replace it).
{
  const directory = join(root, 'storage');
  mkdirSync(directory);
  const run = await new Deno.Command(Deno.execPath(), {
    cwd: web,
    env: { FREETRAIN_BROWSER_ARTIFACT_ROOT: directory, FREETRAIN_DIAGNOSTICS_STORAGE: '1' },
    args: ['run', '-A', 'npm:@playwright/test@1.64.1', 'test', '--config', 'tests/browser-harness/playwright.config.ts'],
    stdout: 'piped', stderr: 'piped',
  }).output();
  writeFileSync(join(directory, 'console.log'), new TextDecoder().decode(run.stdout) + new TextDecoder().decode(run.stderr));
  equal(run.code, 0, `Storage regression failed; see ${directory}/console.log`);
  const summary = JSON.parse(text(join(directory, 'summary.json')));
  equal(summary.results.length, 2);
  equal(summary.results[0].workerIndex, summary.results[1].workerIndex);
  const inline = attachment(summary.results[1], 'inline-evidence');
  equal(text(inline), 'retained bytes');
  records.push({ label: 'storage', directory, expectedFailures: 0, exitCode: run.code });
  console.log('Verified storage: passing cases reclaim raw traces and retain inline evidence as files.');
}
await run('concurrent-1');
await run('concurrent-2');
for (const mode of ['setup', 'restart', 'restart-success', 'teardown']) {
  await run(`managed-${mode}`, mode);
}
writeFileSync(
  join(root, 'acceptance.json'),
  JSON.stringify(
    {
      status: 'passed',
      runs: records,
      distinctAttachments: attachmentPaths.size,
    },
    null,
    2,
  ),
);
console.log(`Diagnostics acceptance passed: ${root}/acceptance.json`);
