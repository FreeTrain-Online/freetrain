import { rmSync } from 'node:fs';
import { join } from 'node:path';
import type { Browser, TestInfo } from '@playwright/test';

/** Playwright 1.64.1 discards passing trace archives, but keeps their raw
 * recordings/resources until worker exit. Reclaim those before the next case
 * creates any contexts. Previous fixture teardown and trace export have finished
 * at this point; retained trace.zip files live outside this scratch directory.
 * Keep the real-browser storage harness when updating the pinned Playwright.
 */
export function clearCompletedTraceScratch(browser: Browser, info: TestInfo) {
  const trace = info.project.use.trace;
  if (
    browser.contexts().length ||
    (typeof trace === 'object' && 'live' in trace && trace.live)
  ) return;
  rmSync(
    join(
      info.project.outputDir,
      `.playwright-artifacts-${info.workerIndex}`,
      'traces',
    ),
    { recursive: true, force: true },
  );
}
