import { mkdirSync, mkdtempSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { defineConfig } from '@playwright/test';

const evidenceParent = resolve(__dirname, '../tmp');
mkdirSync(evidenceParent, { recursive: true });
const evidence = process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE ?? mkdtempSync(join(evidenceParent, 'browser-results-'));
mkdirSync(evidence, { recursive: true });
process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE = evidence;

const canonical = [
  'fleet-controller-manager.spec.ts', 'manual-driving-soak.spec.ts', 'canonical-manual-driving.spec.ts', 'game-toolbar.spec.ts', 'train-runtime.spec.ts', 'construction-regression.spec.ts', 'cross-tool.spec.ts', 'building-catalog.spec.ts',
  'object-removal.spec.ts', 'railway-construction.spec.ts', 'bridge-construction.spec.ts',
  'girder-construction.spec.ts', 'garage-construction.spec.ts', 'signal-construction.spec.ts',
  'pole-construction.spec.ts', 'speed-limit.spec.ts', 'stationary-construction.spec.ts',
  'tunnel-construction.spec.ts', 'slope-construction.spec.ts', 'railway-rendering.spec.ts',
  'reference-world.spec.ts', 'terrain.spec.ts', 'corner-terrain.spec.ts',
  'rectangle-terrain.spec.ts', 'terrace-terrain.spec.ts', 'authoring-terrain.spec.ts',
  'paint-terrain.spec.ts', 'fleet-deployment.spec.ts', 'fleet-trading.spec.ts', 'fleet-persistence.spec.ts', 'train-rendering.spec.ts',
];

export default defineConfig({
  testDir: './tests/browser',
  use: { viewport: { width: 1280, height: 900 } },
  // Each worker owns its backend, preview server and save directory (fixtures.ts).
  // Tests within a file remain sequential; each case starts a fresh backend.
  workers: 2,
  grepInvert: /@extended|@performance/,
  projects: [
    { name: 'canonical', testMatch: canonical },
    { name: 'startup', testMatch: 'startup-world.spec.ts' },
    { name: 'prototype-regression', testIgnore: [...canonical, 'startup-world.spec.ts',
      'mixed-continuation.spec.ts', 'optional-content.spec.ts', 'capacity.spec.ts', 'railway-capacity.spec.ts'] },
  ],
});
