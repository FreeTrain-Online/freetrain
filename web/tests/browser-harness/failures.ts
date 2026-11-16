// Deliberate failures live outside required spec discovery and run only through
// check-browser-diagnostics.ts, which requires and verifies their failure status.
import { expect, test } from '../browser/fixtures.ts';
import {
  assertCanonicalState,
  assertJSONSave,
  assertSaveBytes,
} from '../browser/assertions.ts';

async function pages(
  page: import('@playwright/test').Page,
  browser: import('@playwright/test').Browser,
) {
  const peer = await browser.newPage(), context = peer.context();
  for (const p of [page, peer]) {
    await p.route(
      'http://diagnostics.test/**',
      (route) =>
        route.fulfill({
          contentType: 'text/html',
          body: '<h1>Diagnostic page</h1>',
        }),
    );
    await p.goto('http://diagnostics.test/');
    await p.routeWebSocket('ws://diagnostics.test/session', (route) => {
      route.send(
        JSON.stringify({
          type: 'snapshot',
          worldEpoch: '2',
          revision: '4',
          tick: '8',
        }),
      );
      route.send(Buffer.from([0, 255, 128, 1]));
      route.onMessage(() =>
        route.send(
          JSON.stringify({
            type: 'accepted',
            requestId: 'diagnostic-command',
            revision: '5',
            tick: '9',
          }),
        )
      );
      route.onClose((code, reason) => {
        void route.close({ code, reason });
      });
    });
    await p.evaluate(async () => {
      console.error('diagnostic console error');
      setTimeout(() => {
        throw new Error('diagnostic page error');
      }, 0);
      await new Promise<void>((resolve) => {
        const ws = new WebSocket('ws://diagnostics.test/session');
        ws.addEventListener(
          'open',
          () =>
            ws.send(
              JSON.stringify({ type: 'step', requestId: 'diagnostic-command' }),
            ),
        );
        ws.addEventListener('message', () => ws.close(), { once: true });
        ws.addEventListener('close', () => resolve(), { once: true });
      });
    });
    await p.route('http://failed.test/**', (route) => route.abort('failed'));
    await p.evaluate(() => fetch('http://failed.test/request').catch(() => {}));
  }
  return context;
}
const expected = {
  worldEpoch: '2',
  revision: '5',
  tick: '9',
  mixed: { city: { heat: { q: Array(12000).fill(2) } } },
};
for (const kind of ['heat', 'length', 'missing', 'bytes', 'json', 'ordinary']) {
  test(kind, async ({ page, browser }) => {
    const context = await pages(page, browser);
    const actual = structuredClone(expected),
      options = { phase: kind, peer: 'observer' };
    try {
      if (kind === 'heat') actual.mixed.city.heat.q[11990] = 3;
      if (kind === 'length') actual.mixed.city.heat.q.pop();
      if (kind === 'missing') {
        Reflect.deleteProperty(actual.mixed.city.heat, 'q');
      }
      if (['heat', 'length', 'missing'].includes(kind)) {
        await assertCanonicalState(actual, expected, options);
      }
      if (kind === 'bytes') {
        await assertSaveBytes(
          Buffer.from('{"a":2}'),
          Buffer.from('{"a":1}'),
          options,
        );
      }
      if (kind === 'json') await assertJSONSave('{"a":2}', '{"a":1}', options);
      if (kind === 'ordinary') {
        expect(Array.from({ length: 2000 }, (_, i) => `actual line ${i}`))
          .toEqual(
            Array.from({ length: 2000 }, (_, i) => `expected line ${i}`),
          );
      }
    } finally {
      // Also verify evidence/cleanup for a peer the spec leaves open.
      if (kind !== 'ordinary') await context.close();
    }
  });
}
