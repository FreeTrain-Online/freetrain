import { defineConfig } from '@playwright/test';
import base from './playwright.config.ts';

export default defineConfig({ ...base, grepInvert: undefined, grep: /@performance/, workers: 1 });
