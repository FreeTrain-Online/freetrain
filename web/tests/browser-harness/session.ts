import { test, expect } from '../browser/fixtures.ts';
import { ready } from '../browser/browser-helpers.ts';
import type { TestObservationWindow } from '../../src/lib/test-observation.ts';

test('correct intercepted traffic cannot satisfy convergence when the browser did not apply it', async ({ page, sessions }) => {
  const session = await sessions.observe(page, 'blocked-browser');
  // Suppress only browser delivery of deltas. The real server, interception,
  // observer decoder, and initial join remain intact.
  await page.addInitScript(() => {
    const Native = globalThis.WebSocket;
    globalThis.WebSocket = new Proxy(Native, { construct(target, args, newTarget) {
      const socket = Reflect.construct(target, args, newTarget) as WebSocket;
      socket.addEventListener('message', event => {
        if (JSON.parse(event.data).type === 'delta') event.stopImmediatePropagation();
      }, { capture: true });
      return socket;
    } });
  });
  await page.goto('/'); await ready(page);
  const before = await session.ready(); await session.converge(before, 'initial join');
  const receipt = await session.command({ type: 'command', requestId: 'held-step', expectedWorldEpoch: before.worldEpoch, payload: { kind: 'step' } });
  expect(receipt.type).toBe('accepted');
  if (receipt.type !== 'accepted') throw new Error('Step rejected');
  const target = await session.publication(receipt);
  expect(BigInt(target.tick)).toBe(BigInt(before.tick) + 1n);
  expect(await page.evaluate(() => (window as Window & TestObservationWindow).__freetrainAppliedSession!().state!.tick)).toBe(before.tick);
  // A short, intentional absence window proves convergence keeps waiting for
  // browser application even though the complete correct traffic is available.
  await expect(session.converge(target, 'held browser delta', 300)).rejects.toThrow('Browser-applied state lag');
});

test('a peer at the target with incorrect state fails immediately with exact bounded evidence', async ({ page, sessions }) => {
  const session = await sessions.observe(page, 'corrupt-browser');
  await page.goto('/'); await ready(page); const target = await session.ready();
  await page.evaluate(() => {
    const target = window as Window & TestObservationWindow;
    const read = target.__freetrainAppliedSession!;
    Object.defineProperty(target, '__freetrainAppliedSession', { configurable: true, value: () => {
      const applied = JSON.parse(JSON.stringify(read())) as ReturnType<typeof read>;
      if (applied.state) (applied.state as { cash: string }).cash = '999';
      return applied;
    } });
  });
  const start = Date.now();
  await expect(session.converge(target, 'corrupt applied cash')).rejects.toThrow(/Canonical state mismatch.*browser applied/);
  expect(Date.now() - start).toBeLessThan(1500);
});
