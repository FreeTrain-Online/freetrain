import { defineConfig } from '@playwright/test';
import base from './playwright.config.ts';

// Exhaustive functional matrices include routine witnesses. Performance has a
// separate sequential owner (playwright.performance.config.ts).
export default defineConfig({
  ...base,
  grepInvert: /@performance/,
  metadata: { extended: true },
});
