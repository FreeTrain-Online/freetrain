import { deepStrictEqual, equal, throws } from 'node:assert/strict';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import {
  browserFixture,
  configuredFixtures,
  fixtureFamilies,
  selectedFixtures,
  validateFixture,
} from './browser/generated-fixtures.ts';
import RequiredTestsReporter from './browser/required-tests-reporter.ts';
import {
  browserSpecs,
  validateSpecOwnership,
} from './browser/spec-ownership.ts';
import { writeTimings } from './browser/run-timings.ts';
import { readFileSync } from 'node:fs';
import type {
  FullResult,
  TestCase,
  TestResult,
} from '@playwright/test/reporter';

Deno.test('browser fixture selection follows selected cases and excludes optional capacity inputs', () => {
  deepStrictEqual(
    selectedFixtures([{
      file: '/tests/fleet-persistence.spec.ts',
      title: 'P5-14 Outside exit',
    }]),
    ['outside'],
  );
  deepStrictEqual(
    selectedFixtures([{
      file: 'fleet-deployment.spec.ts',
      title: 'en playable purchase deploy',
    }]),
    [],
  );
  deepStrictEqual(
    selectedFixtures([{
      file: 'fleet-deployment.spec.ts',
      title: 'P5-15 en equal-minute traffic',
    }]),
    ['operations'],
  );
  deepStrictEqual(
    selectedFixtures([{
      file: 'station-continuation.spec.ts',
      title: 'service en',
    }, {
      file: 'station-continuation.spec.ts',
      title: 'midnight ja',
    }]),
    ['stationContinuation'],
  );
  deepStrictEqual(
    selectedFixtures([{ file: 'fleet-capacity.spec.ts', title: 'capacity' }]),
    [],
  );
  deepStrictEqual(
    selectedFixtures([{
      file: 'fleet-controller-manager.spec.ts',
      title:
        'named controllers can be created and configured before buying a train',
    }]),
    [],
  );
  deepStrictEqual(
    selectedFixtures([{
      file: 'fleet-controller-manager.spec.ts',
      title:
        'ja advanced ordered rules edit masks, copies, order, peer conflicts and confirmed clear',
    }]),
    ['tat'],
  );
  // Editor runs have project filters but no CLI case selection.
  deepStrictEqual(
    configuredFixtures([{
      testDir: '/browser',
      testMatch: ['**/*.spec.ts'],
      testIgnore: ['**/*.spec.ts'],
    }]),
    [],
  );
  deepStrictEqual(
    configuredFixtures([{
      testDir: '/browser',
      testMatch: /mixed-continuation\.spec\.ts$/,
      testIgnore: [],
    }]),
    ['mixed'],
  );
});

Deno.test('fixture preflight rejects missing, empty and directory companion outputs with producer diagnostics', () => {
  const directory = mkdtempSync(join(tmpdir(), 'browser-fixture-check-')),
    path = join(directory, 'yard');
  try {
    writeFileSync(path, '{}');
    throws(
      () => validateFixture('yard', path),
      /arrived.json; producer: Production_Simple/,
    );
    writeFileSync(path + '.arrived.json', '{}');
    writeFileSync(path + '.departed.json', '');
    throws(() => validateFixture('yard', path), /departed.json; producer:/);
    writeFileSync(path + '.departed.json', '{}');
    validateFixture('yard', path);
    throws(() => validateFixture('city', directory), /Missing city fixture/);
  } finally {
    rmSync(directory, { recursive: true });
  }
});

Deno.test('required fixture lookup fails instead of silently skipping absent setup', () => {
  const key = fixtureFamilies.bells.env, previous = process.env[key];
  try {
    delete process.env[key];
    throws(
      () => browserFixture('bells'),
      /Generate_real_selected_bell_departure_fixtures/,
    );
  } finally {
    if (previous === undefined) delete process.env[key];
    else process.env[key] = previous;
  }
});

Deno.test('required browser reporter turns an otherwise green skipped selection into failure', async () => {
  const reporter = new RequiredTestsReporter();
  equal(await reporter.onEnd({ status: 'passed' } as FullResult), undefined);
  reporter.onTestEnd(
    { titlePath: () => ['canonical', 'required fixture'] } as TestCase,
    { status: 'skipped' } as TestResult,
  );
  const error = console.error;
  console.error = () => {};
  try {
    deepStrictEqual(await reporter.onEnd({ status: 'passed' } as FullResult), {
      status: 'failed',
    });
  } finally {
    console.error = error;
  }
});

Deno.test('browser ownership rejects new, removed and duplicate specs', () => {
  validateSpecOwnership(['one.spec.ts'], { canonical: ['one.spec.ts'] });
  throws(
    () => validateSpecOwnership(['new.spec.ts'], {}),
    /Unowned browser spec/,
  );
  throws(
    () => validateSpecOwnership([], { canonical: ['old.spec.ts'] }),
    /Missing browser spec/,
  );
  throws(() =>
    validateSpecOwnership(['one.spec.ts'], {
      canonical: ['one.spec.ts'],
      prototype: ['one.spec.ts'],
    }), /Duplicate browser owner/);
  equal(browserSpecs.continuation[0], 'mixed-continuation.spec.ts');
});

Deno.test('required browser reporter rejects retry-dependent success', async () => {
  const reporter = new RequiredTestsReporter();
  reporter.onTestEnd(
    { titlePath: () => ['canonical', 'flaky'] } as TestCase,
    { status: 'passed', retry: 1 } as TestResult,
  );
  const error = console.error;
  console.error = () => {};
  try {
    deepStrictEqual(await reporter.onEnd({ status: 'passed' } as FullResult), {
      status: 'failed',
    });
  } finally {
    console.error = error;
  }
});

Deno.test('browser timing artifacts preserve setup and separate worker measurements', () => {
  const directory = mkdtempSync(join(tmpdir(), 'browser-timings-'));
  const key = 'FREETRAIN_BROWSER_ARTIFACT_ROOT', previous = process.env[key];
  try {
    process.env[key] = directory;
    writeTimings('setup', { phases: { discoveryMs: 12 }, status: 'failed' });
    writeTimings('worker-0', { backendMs: 34 });
    deepStrictEqual(
      JSON.parse(readFileSync(join(directory, 'timings/setup.json'), 'utf8')),
      { phases: { discoveryMs: 12 }, status: 'failed' },
    );
    deepStrictEqual(
      JSON.parse(
        readFileSync(join(directory, 'timings/worker-0.json'), 'utf8'),
      ),
      { backendMs: 34 },
    );
  } finally {
    if (previous === undefined) delete process.env[key];
    else process.env[key] = previous;
    rmSync(directory, { recursive: true, force: true });
  }
});

Deno.test('mixed checkpoint refinement accepts independent fleet station and geometry worlds', async () => {
  const { requireMixedState } = await import('./browser/session.ts');
  const { parseMessageValue } = await import('../src/lib/protocol.ts');
  for (const [name, sections, absent] of [
    ['fleet', ['fleet', 'queue'], 'stationGraph'],
    ['stations', ['stationGraph', 'queue'], 'fleet'],
    ['mixed', [], 'fleet'],
  ] as const) {
    const input = JSON.parse(readFileSync(new URL(`../../prototype/protocol/fixtures/snapshot-${name}.json`, import.meta.url), 'utf8'));
    const state = parseMessageValue(input);
    if (state.type !== 'snapshot') throw new Error('Expected snapshot fixture');
    equal(state.mixed?.[absent], undefined);
    equal(requireMixedState(state, ...sections), state);
    deepStrictEqual(requireMixedState(state).mixed, input.mixed);
    throws(() => requireMixedState(state, absent), new RegExp(`Expected mixed.${absent}`));
  }
  throws(() => requireMixedState(undefined), /Expected a mixed checkpoint/);
});
