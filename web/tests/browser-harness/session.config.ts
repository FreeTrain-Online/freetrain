import { defineConfig } from '@playwright/test';
import base from '../../playwright.config.ts';
import { resolve } from 'node:path';
export default defineConfig({
  ...base,
  testDir: __dirname,
  testMatch: 'session.ts',
  globalSetup: '../browser/global-setup.ts',
  reporter: [[resolve(__dirname, '../browser/bounded-reporter.ts')], [resolve(__dirname, '../browser/required-tests-reporter.ts')], ['json', { outputFile: resolve(process.env.FREETRAIN_BROWSER_ARTIFACT_ROOT!, 'report.json') }]],
  projects: [{ name: 'canonical' }],
  retries: 0,
  workers: 1,
});
