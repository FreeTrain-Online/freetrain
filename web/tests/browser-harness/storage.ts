import { randomBytes } from 'node:crypto';
import { existsSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { expect, test } from '../browser/fixtures.ts';

// Two passing cases must reuse a worker: deliberate failures restart workers and
// would hide the raw-trace accumulation this regression protects against.
test.describe.configure({ mode: 'serial' });
let scratch = '', previousFiles: string[] = [];
function files(directory: string): string[] {
  return existsSync(directory)
    ? readdirSync(directory, { withFileTypes: true })
      .flatMap((entry) =>
        entry.isDirectory()
          ? files(join(directory, entry.name))
          : [join(directory, entry.name)]
      )
    : [];
}
test('a passing case generates raw trace resources', async ({ page }, info) => {
  scratch = join(
    info.project.outputDir,
    `.playwright-artifacts-${info.workerIndex}`,
    'traces',
  );
  const body = randomBytes(2 * 1024 * 1024);
  await page.route('http://storage.test/**', (route) =>
    route.fulfill(
      route.request().url().endsWith('/data')
        ? { body, contentType: 'application/octet-stream' }
        : { body: '<h1>Trace storage</h1>', contentType: 'text/html' },
    ));
  await page.goto('http://storage.test/');
  await page.evaluate(async () => {
    await (await fetch('/data')).arrayBuffer();
  });
  await page.context().close();
  previousFiles = files(scratch);
  expect(previousFiles.reduce((sum, path) => sum + statSync(path).size, 0))
    .toBeGreaterThan(body.length);
  // A sentinel verifies the entire previous scratch directory is reclaimed.
  writeFileSync(join(scratch, 'completed-case'), 'complete');
});
test('the next case reclaims previous scratch and can still record a trace', async ({ page }) => {
  expect(previousFiles.length).toBeGreaterThan(0);
  for (const path of previousFiles) expect(existsSync(path), path).toBe(false);
  expect(existsSync(join(scratch, 'completed-case'))).toBe(false);
  await page.setContent('<h1>Storage regression</h1>');
  await expect(page.getByRole('heading')).toHaveText('Storage regression');
  await test.info().attach('inline-evidence', {
    body: Buffer.from('retained bytes'),
    contentType: 'text/plain',
  });
});
