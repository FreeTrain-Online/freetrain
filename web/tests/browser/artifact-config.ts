import { mkdirSync, mkdtempSync } from 'node:fs';
import { join, resolve } from 'node:path';
import type { PlaywrightTestConfig } from '@playwright/test';
import { prepareSessionRuntime } from './session-runtime.ts';

/** Shared evidence policy; callers retain project selection, budgets and servers. */
export function browserArtifacts(): Pick<PlaywrightTestConfig, 'outputDir' | 'reporter'> {
  const parent = resolve('../tmp'); mkdirSync(parent, { recursive: true });
  const root = resolve(process.env.FREETRAIN_BROWSER_ARTIFACT_ROOT ?? mkdtempSync(join(parent, 'browser-run-')));
  process.env.FREETRAIN_BROWSER_ARTIFACT_ROOT = root;
  const evidence = resolve(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE ?? join(root, 'evidence'));
  mkdirSync(evidence, { recursive: true }); process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE = evidence;
  prepareSessionRuntime(root);
  return {
    outputDir: join(root, 'test-results'),
    reporter: [[resolve('tests/browser/bounded-reporter.ts')], [resolve('tests/browser/required-tests-reporter.ts')], ['json', { outputFile: join(root, 'report.json') }]],
  };
}
export const failureArtifacts = { trace: 'retain-on-failure', screenshot: 'only-on-failure' } as const;
