import { defineConfig } from '@playwright/test';
import base from './playwright.config.ts';
import { browserSpecs } from './tests/browser/spec-ownership.ts';

export default defineConfig({
  ...base,
  grepInvert: undefined,
  projects: [{ name: 'canonical', testMatch: browserSpecs.continuation }],
});
