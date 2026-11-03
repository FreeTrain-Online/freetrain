import { defineConfig } from '@playwright/test';
import base from './playwright.config.ts';

// Comprehensive validation includes exhaustive content/lifecycle matrices and
// large-world/performance regressions. The ordinary smoke tests also run.
export default defineConfig({ ...base, grepInvert: undefined, metadata: { extended: true } });
