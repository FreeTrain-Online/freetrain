import { deepStrictEqual, equal, match, rejects } from 'node:assert/strict';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import type { Browser, TestInfo } from '@playwright/test';
import { clearCompletedTraceScratch } from './browser/trace-storage.ts';
import type {
  FullConfig,
  FullResult,
  TestCase,
  TestResult,
} from '@playwright/test/reporter';
import {
  assertCanonicalState,
  assertJSONSave,
  assertSaveBytes,
  compareValues,
  formatComparison,
} from './browser/assertions.ts';
import BoundedReporter, { boundedLines } from './browser/bounded-reporter.ts';
import {
  attachProcessEvidence,
  ProcessEvidence,
  processLogOffsets,
} from './browser/process-evidence.ts';

function evidence() {
  const directory = mkdtempSync(join(tmpdir(), 'browser-diagnostics-unit-'));
  const attachments: { name: string; path: string }[] = [];
  const info = {
    outputPath: (...parts: string[]) => join(directory, ...parts),
    testId: 'unit',
    titlePath: ['unit'],
    workerIndex: 0,
    attach: (name: string, options: { path: string }) => {
      attachments.push({ name, path: options.path });
      return Promise.resolve();
    },
  } as unknown as TestInfo;
  return {
    directory,
    attachments,
    info,
    cleanup: () => rmSync(directory, { recursive: true, force: true }),
  };
}
const context = { phase: 'checkpoint reopen', peer: 'JA observer' };
Deno.test('trace scratch cleanup isolates workers and preserves archives and active contexts', () => {
  const e = evidence();
  try {
    const worker = join(e.directory, '.playwright-artifacts-0');
    const scratch = join(worker, 'traces');
    const other = join(e.directory, '.playwright-artifacts-1', 'traces');
    mkdirSync(scratch, { recursive: true });
    mkdirSync(other, { recursive: true });
    writeFileSync(join(scratch, 'raw.trace'), 'raw');
    writeFileSync(join(other, 'raw.trace'), 'other worker');
    writeFileSync(join(e.directory, 'trace.zip'), 'retained trace');
    writeFileSync(join(worker, 'temporary.zip'), 'pending export');
    const info = { workerIndex: 0, project: { outputDir: e.directory, use: {} } } as TestInfo;
    clearCompletedTraceScratch({ contexts: () => [{}] } as unknown as Browser, info);
    equal(existsSync(scratch), true);
    const browser = { contexts: () => [] } as unknown as Browser;
    clearCompletedTraceScratch(browser, { ...info, project: { ...info.project, use: { trace: { live: true } } } } as unknown as TestInfo);
    equal(existsSync(scratch), true);
    clearCompletedTraceScratch(browser, info);
    equal(existsSync(scratch), false);
    equal(readFileSync(join(other, 'raw.trace'), 'utf8'), 'other worker');
    equal(readFileSync(join(e.directory, 'trace.zip'), 'utf8'), 'retained trace');
    equal(readFileSync(join(worker, 'temporary.zip'), 'utf8'), 'pending export');
  } finally { e.cleanup(); }
});
Deno.test('canonical heat corruption reports precise path, target identity and full values only on failure', async () => {
  const e = evidence();
  try {
    const expected = {
      worldEpoch: '7',
      revision: '42',
      tick: '99',
      mixed: { city: { heat: { q: Array(10000).fill(2) } } },
    };
    const actual = structuredClone(expected);
    actual.mixed.city.heat.q[9912] = 3;
    await assertCanonicalState(
      expected,
      structuredClone(expected),
      context,
      e.info,
    );
    equal(e.attachments.length, 0);
    await rejects(
      assertCanonicalState(actual, expected, context, e.info),
      (error) => {
        match(
          String(error),
          /\$\.mixed\.city\.heat\.q\[9912\]: expected 2; actual 3/,
        );
        match(String(error), /worldEpoch='7' revision='42' tick='99'/);
        match(String(error), /phase=checkpoint reopen peer=JA observer/);
        match(
          String(error),
          /expected length 10000; actual length 10000; 1 mismatched indices/,
        );
        equal(String(error).split('\n').length < 20, true);
        return true;
      },
    );
    equal(e.attachments.length, 2);
    equal(
      (readFileSync(e.attachments[1].path, 'utf8').match(/\b2\b/g) ?? [])
        .length,
      9999,
    );
  } finally {
    e.cleanup();
  }
});
Deno.test('comparison visits every array value and distinguishes length, order, missing and undefined', () => {
  const result = compareValues({ a: Array(20000).fill(1), extra: undefined }, {
    a: Array(20001).fill(2),
    missing: 0,
  });
  equal(result.differences, 20004);
  equal(result.arrays[0].mismatchedIndices, 20001);
  equal(result.samples.length, 5);
  equal(formatComparison(result).length < 2000, true);
  match(formatComparison(result), /19999 more beyond path sample/);
  equal(compareValues([1, 2], [2, 1]).differences, 2);
  equal(compareValues({}, { a: undefined }).differences, 1);
  equal(
    compareValues({ a: [0], b: [1] }, { a: [0], b: [2] }).arrays[0].path,
    '$.b',
  );
});
Deno.test('save comparisons preserve JSON and byte equality, prefix offsets, hashes and unique artifacts', async () => {
  const e = evidence(),
    bytes = (value: string) => new TextEncoder().encode(value);
  try {
    await assertJSONSave('{"a":1,"b":2}', '{"b":2,"a":1}', context, e.info);
    await assertSaveBytes(bytes('same'), bytes('same'), context, e.info);
    equal(e.attachments.length, 0);
    await rejects(
      assertSaveBytes(bytes('{"a":1} '), bytes('{"a":1}'), context, e.info),
      /First differing offset=7 expected=<end> actual=32/,
    );
    await rejects(
      assertSaveBytes(bytes('{"a":2}'), bytes('{"a":1}'), context, e.info),
      (error) => {
        match(String(error), /sha256=[a-f0-9]{64}/);
        match(String(error), /\$\.a: expected 1; actual 2/);
        return true;
      },
    );
    await rejects(
      assertSaveBytes(bytes('{bad'), bytes('{}'), context, e.info),
      /not valid JSON/,
    );
    await rejects(
      assertJSONSave('{"a":2}', '{"a":1}', context, e.info),
      /\$\.a: expected 1; actual 2/,
    );
    await rejects(
      assertJSONSave('{bad', '{}', context, e.info),
      /Invalid JSON save/,
    );
    equal(new Set(e.attachments.map((a) => a.path)).size, 10);
    for (const attachment of e.attachments) {
      equal(existsSync(attachment.path), true);
    }
    equal(readFileSync(e.attachments[1].path, 'utf8'), '{"a":1} ');
  } finally {
    e.cleanup();
  }
});
Deno.test('ordinary reporter errors stay bounded with complete original errors, source locations and attachments', () => {
  const e = evidence(), previous = process.env.FREETRAIN_BROWSER_ARTIFACT_ROOT;
  const log = console.log, stderr = console.error, output: string[] = [];
  process.env.FREETRAIN_BROWSER_ARTIFACT_ROOT = e.directory;
  console.log = console.error = (...values) => {
    output.push(values.join(' '));
  };
  try {
    const reporter = new BoundedReporter();
    reporter.onBegin({ rootDir: e.directory } as FullConfig);
    const error = {
      message: 'ordinary assert',
      stack: Array.from({ length: 1000 }, (_, i) => `diagnostic ${i}`).join(
        '\n',
      ),
      location: { file: 'spec.ts', line: 4, column: 1 },
    };
    const original = structuredClone(error);
    const result = {
      status: 'failed',
      retry: 0,
      workerIndex: 2,
      errors: [error],
      attachments: [{
        name: 'save',
        path: '/save.json',
        contentType: 'application/json',
      }, { name: 'inline.png', body: Buffer.alloc(1024 * 1024, 127), contentType: 'image/png' }],
    } as TestResult;
    reporter.onTestEnd(
      {
        titlePath: () => ['test'],
        id: 'test',
        expectedStatus: 'passed',
        location: error.location,
      } as TestCase,
      result,
    );
    equal(output[1].split('\n').length <= 50, true);
    deepStrictEqual(result.errors[0], original);
    deepStrictEqual(
      JSON.parse(readFileSync(result.attachments.find(a => a.name === 'complete-errors')!.path!, 'utf8')),
      [original],
    );
    match(output[1], /spec.ts:4:1/);
    match(output[1], /save: \/save.json/);
    reporter.onError(error);
    equal(output.at(-1)!.split('\n').length <= 50, true);
    reporter.onEnd({ status: 'failed' } as FullResult);
    equal(
      JSON.parse(readFileSync(join(e.directory, 'summary.json'), 'utf8'))
        .results[0].status,
      'failed',
    );
    const summary = readFileSync(join(e.directory, 'summary.json'), 'utf8');
    equal(summary.length < 10000, true);
    const inline = JSON.parse(summary).results[0].attachments.find((a: { name: string }) => a.name === 'inline.png');
    equal(inline.body, undefined);
    equal(inline.bytes, 1024 * 1024);
    deepStrictEqual(readFileSync(inline.path), result.attachments[1].body);
    equal(result.attachments[1].path, undefined);
    equal(boundedLines('x'.repeat(10000))[0].length < 550, true);
  } finally {
    console.log = log;
    console.error = stderr;
    if (previous === undefined) {
      delete process.env.FREETRAIN_BROWSER_ARTIFACT_ROOT;
    } else process.env.FREETRAIN_BROWSER_ARTIFACT_ROOT = previous;
    e.cleanup();
  }
});
Deno.test('reporter stdout summarizes final test outcomes and lists failures once', () => {
  const e = evidence(), previous = process.env.FREETRAIN_BROWSER_ARTIFACT_ROOT;
  const log = console.log, stderr = console.error, output: string[] = [];
  process.env.FREETRAIN_BROWSER_ARTIFACT_ROOT = e.directory;
  console.log = console.error = (...values) => { output.push(values.join(' ')); };
  try {
    const reporter = new BoundedReporter();
    reporter.onBegin({ rootDir: e.directory } as FullConfig);
    const statuses = ['passed', 'failed', 'timedOut', 'interrupted', 'skipped', 'passed'] as const;
    for (const [index, status] of statuses.entries()) {
      const test = {
        id: `case-${index}`, titlePath: () => ['canonical', 'terrain.spec.ts', `case ${index}`],
        location: { file: 'terrain.spec.ts', line: index + 1, column: 1 },
        expectedStatus: 'passed',
      } as TestCase;
      const result = { status, retry: 0, workerIndex: 0, errors: [], attachments: [] } as unknown as TestResult;
      if (index === 5) reporter.onTestEnd(test, { ...result, status: 'failed' });
      reporter.onTestEnd(test, { ...result, retry: index === 5 ? 1 : 0 });
    }
    reporter.onError({ message: 'runner failure' });
    reporter.onEnd({ status: 'failed' } as FullResult);
    const summary = output.at(-1)!;
    match(summary, /^Browser run failed: 2 passed, 3 failed, 1 skipped\./);
    match(summary, /Runner errors: 1/);
    const failureLines = summary.split('\n').filter(line => line.startsWith('  - '));
    deepStrictEqual(failureLines, [
      '  - canonical › terrain.spec.ts › case 1 (failed)',
      '  - canonical › terrain.spec.ts › case 2 (timedOut)',
      '  - canonical › terrain.spec.ts › case 3 (interrupted)',
    ]);
    const passing = new BoundedReporter();
    passing.onBegin({ rootDir: e.directory } as FullConfig);
    passing.onEnd({ status: 'passed' } as FullResult);
    match(output.at(-1)!, /^Browser run passed: 0 passed, 0 failed, 0 skipped\./);
    equal(output.at(-1)!.includes('Failed tests:'), false);
  } finally {
    console.log = log;
    console.error = stderr;
    if (previous === undefined) delete process.env.FREETRAIN_BROWSER_ARTIFACT_ROOT;
    else process.env.FREETRAIN_BROWSER_ARTIFACT_ROOT = previous;
    e.cleanup();
  }
});
Deno.test('process evidence separates starts and streams and associates test slices', async () => {
  const e = evidence();
  try {
    const directory = join(e.directory, 'worker'),
      logs = new ProcessEvidence(directory);
    const first = logs.start('backend', 'dotnet');
    first.event('ready', { pid: 12 });
    const offsets = processLogOffsets(directory),
      second = logs.start('backend', 'dotnet');
    second.event('startup-failed', { error: 'bad checkpoint' });
    equal(first.stdout !== second.stdout, true);
    equal(first.stderr !== first.stdout, true);
    await attachProcessEvidence(e.info, directory, offsets);
    const manifest = JSON.parse(
      readFileSync(e.attachments.at(-1)!.path, 'utf8'),
    );
    equal(
      manifest.files.find((file: { source: string }) =>
        file.source.endsWith('lifecycle.ndjson')
      ).start > 0,
      true,
    );
    match(
      readFileSync(
        e.attachments.find((a) => a.name === 'process-lifecycle.ndjson')!.path,
        'utf8',
      ),
      /startup-failed/,
    );
  } finally {
    e.cleanup();
  }
});

Deno.test('suite exact assertion dispatch preserves download bytes and full heat comparisons', async () => {
  const { assertExact } = await import('./browser/assertions.ts');
  const e = evidence();
  try {
    await rejects(assertExact(Buffer.from('{"x":1}'), Buffer.from('{ "x":1}'), 'download', 'owner', e.info), /Exact save bytes mismatch/);
    const expected = { mixed: { city: { heat: { q: [1, 2, 3] } } } };
    const actual = structuredClone(expected); actual.mixed.city.heat.q[1] = 4;
    await rejects(assertExact(actual, expected, 'minute', 'JA peer', e.info), /\$\.mixed\.city\.heat\.q\[1\]/);
    equal(e.attachments.length, 4);
  } finally { e.cleanup(); }
});

Deno.test('independently owned capacity processes retain stdout stderr exit and restart logs', async () => {
  const { spawn } = await import('node:child_process');
  const { captureProcess, processEvidenceDirectory } = await import('./browser/process-evidence.ts');
  const e = evidence(), previous = process.env.FREETRAIN_BROWSER_ARTIFACT_ROOT;
  process.env.FREETRAIN_BROWSER_ARTIFACT_ROOT = e.directory;
  try {
    for (let n = 0; n < 2; n++) {
      const child = captureProcess(spawn(Deno.execPath(), ['eval', `console.log('start ${n}'); console.error('stderr ${n}');`]), 'backend', 0);
      await new Promise<void>((resolve, reject) => { child.on('close', code => code === 0 ? resolve() : reject(new Error(`Child exit ${code}`))); child.on('error', reject); });
    }
    const directory = processEvidenceDirectory(0);
    for (let n = 0; n < 2; n++) {
      const prefix = String(n + 1).padStart(3, '0') + '-backend';
      match(readFileSync(join(directory, prefix + '.stdout.log'), 'utf8'), new RegExp(`start ${n}`));
      match(readFileSync(join(directory, prefix + '.stderr.log'), 'utf8'), new RegExp(`stderr ${n}`));
    }
    const events = readFileSync(join(directory, 'lifecycle.ndjson'), 'utf8').trim().split('\n').map(line => JSON.parse(line));
    equal(events.filter(event => event.kind === 'start').length, 2); equal(events.filter(event => event.kind === 'exit' && event.code === 0).length, 2);
  } finally { if (previous === undefined) delete process.env.FREETRAIN_BROWSER_ARTIFACT_ROOT; else process.env.FREETRAIN_BROWSER_ARTIFACT_ROOT = previous; e.cleanup(); }
});
