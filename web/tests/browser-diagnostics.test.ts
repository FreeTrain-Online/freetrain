import { deepStrictEqual, equal, match, rejects } from 'node:assert/strict';
import { existsSync, mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import type { TestInfo } from '@playwright/test';
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
      }],
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
      JSON.parse(readFileSync(result.attachments[1].path!, 'utf8')),
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
