import { validateBrowserSources } from './tests/browser/source-policy.ts';
import { mkdirSync, mkdtempSync, readdirSync, readFileSync } from 'node:fs';
import {
  browserSpecs,
  validateSpecOwnership,
} from './tests/browser/spec-ownership.ts';
import { join, resolve } from 'node:path';
import { defineConfig } from '@playwright/test';
import { prepareSessionRuntime } from './tests/browser/session-runtime.ts';

validateSpecOwnership(
  readdirSync(resolve(__dirname, 'tests/browser')).filter((file) =>
    file.endsWith('.spec.ts')
  ),
  browserSpecs,
);

validateBrowserSources(Object.fromEntries(readdirSync(resolve(__dirname, 'tests/browser')).filter(file => file.endsWith('.spec.ts')).map(file => [file, readFileSync(resolve(__dirname, 'tests/browser', file), 'utf8')])));

const artifactParent = resolve(__dirname, '../tmp');
mkdirSync(artifactParent, { recursive: true });
const artifacts = process.env.FREETRAIN_BROWSER_ARTIFACT_ROOT ??
  mkdtempSync(join(artifactParent, 'browser-run-'));
process.env.FREETRAIN_BROWSER_ARTIFACT_ROOT = resolve(artifacts);
prepareSessionRuntime(resolve(artifacts));
const evidence = process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE ??
  join(artifacts, 'evidence');
mkdirSync(evidence, { recursive: true });
process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE = resolve(evidence);

export default defineConfig({
  testDir: './tests/browser',
  globalSetup: './tests/browser/global-setup.ts',
  outputDir: join(artifacts, 'test-results'),
  reporter: [['./tests/browser/bounded-reporter.ts'], [
    './tests/browser/required-tests-reporter.ts',
  ], [
    'json',
    { outputFile: join(artifacts, 'report.json') },
  ]],
  forbidOnly: !!process.env.CI,
  use: {
    viewport: { width: 1280, height: 900 },
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  // Each worker owns its backend, preview server and save directory (fixtures.ts).
  // Tests within a file remain sequential; each case starts a fresh backend.
  workers: 2,
  grepInvert: /@extended|@performance/,
  projects: [
    { name: 'canonical', testMatch: browserSpecs.canonical },
    { name: 'startup', testMatch: browserSpecs.startup },
    {
      name: 'prototype-regression',
      testMatch: browserSpecs['prototype-regression'],
    },
  ],
});
