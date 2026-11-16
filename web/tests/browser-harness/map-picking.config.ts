import { defineConfig } from '@playwright/test';
import base from './session.config.ts';
export default defineConfig({ ...base, testMatch: 'map-picking.ts' });
