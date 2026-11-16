import { expect, test as managed } from '../browser/fixtures.ts';
import { readFileSync } from 'node:fs';
import { createServer } from 'node:http';
import { join } from 'node:path';
import { processEvidenceDirectory } from '../browser/process-evidence.ts';
const ready = async (page: import('@playwright/test').Page) => {
  await expect(page.getByTestId('connection')).toHaveText('Connected to game');
};
const fault = process.env.FREETRAIN_DIAGNOSTICS_MANAGED;
const test = managed.extend<{}, { injectFault: void }>({
  injectFault: [async ({}, use) => {
    const path = process.env.PATH;
    if (fault === 'setup') process.env.PATH = '';
    try {
      await use();
    } finally {
      process.env.PATH = path;
    }
  }, { scope: 'worker', auto: true }],
});
if (fault === 'teardown') {
  test.afterEach(() => {
    throw new Error('deliberate teardown failure');
  });
}
test(`managed ${fault} evidence`, async ({ page, browser, worldServer, baseURL }) => {
  await page.goto('/');
  await ready(page);
  const context = await browser.newContext({ baseURL }),
    peer = await context.newPage();
  try {
    // Exercise diagnostic capture of Playwright's default routed forwarding,
    // alongside the primary page's native socket events.
    await peer.routeWebSocket('**/api/session?*', (route) => {
      route.connectToServer();
    });
    await peer.goto('/');
    await ready(peer);
    if (fault === 'restart') {
      const events = readFileSync(
        join(
          processEvidenceDirectory(test.info().workerIndex),
          'lifecycle.ndjson',
        ),
        'utf8',
      ).trim().split('\n').map((line) => JSON.parse(line));
      const port = Number(
        new URL(
          events.find((event) =>
            event.kind === 'ready' && event.process.includes('backend')
          ).url,
        ).port,
      );
      const pid = worldServer.pid!()!;
      process.kill(pid, 'SIGTERM');
      await expect.poll(() => {
        try {
          process.kill(pid, 0);
          return true;
        } catch {
          return false;
        }
      }).toBe(false);
      const blocker = createServer((_, response) => {
        response.writeHead(503);
        response.end();
      });
      await new Promise<void>((resolve) =>
        blocker.listen(port, '127.0.0.1', resolve)
      );
      try {
        await worldServer.restart!();
      } finally {
        await new Promise<void>((resolve, reject) =>
          blocker.close((error) => error ? reject(error) : resolve())
        );
      }
    } else if (fault === 'restart-success') {
      await worldServer.restart!();
      throw new Error('deliberate failure after successful restart');
    }
  } finally {
    await context.close();
  }
});
