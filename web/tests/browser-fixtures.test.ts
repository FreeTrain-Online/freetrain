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
import type { FullResult, TestCase, TestResult } from '@playwright/test/reporter';

Deno.test('browser fixture selection follows selected cases and excludes optional capacity inputs', () => {
  deepStrictEqual(
    selectedFixtures([{ file: '/tests/fleet-persistence.spec.ts', title: 'P5-14 Outside exit' }]),
    ['outside'],
  );
  deepStrictEqual(
    selectedFixtures([{ file: 'fleet-deployment.spec.ts', title: 'en playable purchase deploy' }]),
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
    selectedFixtures([{ file: 'station-continuation.spec.ts', title: 'service en' }, {
      file: 'station-continuation.spec.ts',
      title: 'midnight ja',
    }]),
    ['stationContinuation'],
  );
  deepStrictEqual(selectedFixtures([{ file: 'fleet-capacity.spec.ts', title: 'capacity' }]), []);
  deepStrictEqual(
    selectedFixtures([{
      file: 'fleet-controller-manager.spec.ts',
      title: 'named controllers can be created and configured before buying a train',
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
    throws(() => validateFixture('yard', path), /arrived.json; producer: Production_Simple/);
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
    throws(() => browserFixture('bells'), /Generate_real_selected_bell_departure_fixtures/);
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
    deepStrictEqual(await reporter.onEnd({ status: 'passed' } as FullResult), { status: 'failed' });
  } finally {
    console.error = error;
  }
});
