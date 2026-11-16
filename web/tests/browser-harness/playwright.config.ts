import { defineConfig } from '@playwright/test';
import { join, resolve } from 'node:path';
const root = resolve(process.env.FREETRAIN_BROWSER_ARTIFACT_ROOT!);
export default defineConfig({
  testDir: __dirname,
  testMatch: process.env.FREETRAIN_DIAGNOSTICS_MANAGED
    ? 'managed.ts'
    : 'failures.ts',
  outputDir: join(root, 'test-results'),
  fullyParallel: true,
  workers: 2,
  retries: 0,
  timeout: 20000,
  reporter: [[resolve(__dirname, '../browser/bounded-reporter.ts')], [
    resolve(__dirname, '../browser/required-tests-reporter.ts'),
  ], ['json', { outputFile: join(root, 'report.json') }]],
  // The same built-in capture as the default required browser configuration.
  use: {
    headless: true,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{
    name: process.env.FREETRAIN_DIAGNOSTICS_MANAGED
      ? 'canonical'
      : 'diagnostic-harness',
  }],
});
