import { execFileSync } from 'node:child_process';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { prepareRenderingHarnesses } from './rendering-harnesses.ts';
import type { FullConfig } from '@playwright/test';
import {
  configuredFixtures,
  fixtureFamilies,
  selectedFixtures,
  type SelectedTest,
  validateFixture,
} from './generated-fixtures.ts';

/** Listing applies the same project, grep, shard, and file filters without setup. */
function selectedTests(config: FullConfig): SelectedTest[] | undefined {
  const command = config.argv.indexOf('test', 1);
  if (command < 0) return;
  const env = { ...process.env };
  for (
    const key of [
      'PLAYWRIGHT_JSON_OUTPUT_FILE',
      'PLAYWRIGHT_JSON_OUTPUT_DIR',
      'PLAYWRIGHT_JSON_OUTPUT_NAME',
    ]
  ) delete env[key];
  const report = JSON.parse(
    execFileSync('deno', [
      'run',
      '-A',
      'npm:@playwright/test@1.64.1',
      'test',
      ...config.argv.slice(command + 1),
      '--list',
      '--reporter=json',
    ], {
      env,
      encoding: 'utf8',
      maxBuffer: 16 * 1024 * 1024,
    }),
  );
  const tests: SelectedTest[] = [];
  type Suite = { specs?: SelectedTest[]; suites?: Suite[] };
  function visit(suite: Suite) {
    tests.push(...suite.specs ?? []);
    for (const child of suite.suites ?? []) visit(child);
  }
  visit(report);
  return tests;
}

export default function setup(config: FullConfig) {
  const tests = selectedTests(config);
  const selected = tests ? selectedFixtures(tests) : configuredFixtures(config.projects);
  const root = resolve(__dirname, '../../..');
  const configuration = process.env.FREETRAIN_ONLINE_TEST_SERVER_CONFIGURATION ?? 'Release';
  if (!['Debug', 'Release'].includes(configuration)) {
    throw new Error('Invalid test server configuration');
  }
  const output = mkdtempSync(join(tmpdir(), 'freetrain-browser-inputs-'));
  const cleanup = () => rmSync(output, { recursive: true, force: true });
  try {
    process.env.FREETRAIN_BROWSER_HARNESSES = output;
    prepareRenderingHarnesses(output, tests?.map(test => test.file));
    if (!selected.length) {
      // Focused selections without generated inputs still need a current server.
      execFileSync('dotnet', [
        'build',
        resolve(root, 'src/FreeTrain.Online.Server'),
        '-c',
        configuration,
        '-p:RestoreLockedMode=true',
      ], { cwd: root, stdio: 'inherit' });
      return cleanup;
    }
    const env = { ...process.env };
    // Unselected producer outputs must not write to inherited developer paths.
    for (const family of Object.values(fixtureFamilies)) delete env[family.env];
    for (const name of selected) {
      const family = fixtureFamilies[name];
      env[family.env] = process.env[family.env] = join(output, name + '.json');
    }
    console.log(`Generating browser fixtures: ${selected.join(', ')}`);
    execFileSync('dotnet', [
      'test',
      resolve(root, 'tests/FreeTrain.Online.Tests'),
      '-c',
      configuration,
      '-p:RestoreLockedMode=true',
      '--filter',
      [...new Set(selected.flatMap((name) => fixtureFamilies[name].producers))]
        .map((name) => `FullyQualifiedName~${name}`).join('|'),
    ], { cwd: root, stdio: 'inherit', env });
    for (const name of selected) validateFixture(name, env[fixtureFamilies[name].env]!);
    return cleanup;
  } catch (error) {
    cleanup();
    throw error;
  }
}
