import { defineConfig } from '@playwright/test';
import base from './playwright.config.ts';

export default defineConfig({ ...base, grepInvert: undefined,
  projects: [{ name: 'canonical', testMatch: 'mixed-continuation.spec.ts' }] });
