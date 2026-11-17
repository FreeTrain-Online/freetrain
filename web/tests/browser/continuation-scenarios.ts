import { readFileSync, mkdtempSync, writeFileSync, truncateSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { test, expect, type Page, type Browser } from './fixtures.ts';
import { browserFixture } from './generated-fixtures.ts';
import { openPanel, closePanel, ready } from './browser-helpers.ts';
import { assertCanonicalState, assertSaveBytes } from './assertions.ts';
import type { SessionFixture } from './session.ts';

export const continuationScenarios = ['rejected uploads', 'slot and download', 'reconnect', 'restart continuation', 'complete journey'] as const;
type Scenario = typeof continuationScenarios[number];
type Inputs = { page: Page; browser: Browser; baseURL: string | undefined; worldServer: { restart?: () => Promise<void> }; sessions: SessionFixture };

/** Independently prepare every persistence boundary from the backend-produced
 * oracle. Keep real file input, download, peer application and process restart. */
export function continuationScenario(domain: 'station' | 'fleet', mode: string, locale: 'en' | 'ja', scenario: Scenario) {
  return async ({ page, browser, baseURL, worldServer, sessions }: Inputs) => {
    test.setTimeout(120000);
    const family = domain === 'station' ? 'stationContinuation' : 'fleetContinuation';
    const path = browserFixture(family) + (domain === 'station' ? '.' + mode : '');
    const original = readFileSync(path), expected = readFileSync(path + '.expected');
    const text = JSON.parse(readFileSync(`src/locales/${locale}/app.json`, 'utf8')) as Record<string, string>;
    const errors: string[] = [];
    page.on('pageerror', error => errors.push(String(error)));
    const saved = async () => (await page.request.get('/api/saves/download')).body();
    const button = (name: string) => page.getByRole('button', { name, exact: true });
    const owner = await sessions.observe(page, 'owner');
    const exact = (actual: Uint8Array, target: Uint8Array, phase: string) => assertSaveBytes(actual, target, { phase, peer: 'owner' });
    const secret = process.env.FREETRAIN_ONLINE_TEST_CONTAINER_IMAGE ? 'fleet-container-test' : '';
    await test.step('Prepare the independent paused checkpoint', async () => {
      if (secret) expect((await page.request.post('/api/access', { data: { secret } })).ok()).toBe(true);
      await page.goto('/'); await ready(page);
      await sessions.replaceSave(owner, original, 'initial continuation save');
      await owner.reconnect(() => page.reload(), owner.latest()!, 'initial reload'); await ready(page);
      if (locale === 'ja') {
        await openPanel(page, 'settings'); await page.getByLabel('Language', { exact: true }).selectOption('ja'); await closePanel(page); await ready(page, locale);
      }
    });
    const context = await browser.newContext({ baseURL, locale: 'ja-JP' }), peer = await context.newPage();
    peer.on('pageerror', error => errors.push(String(error)));
    const observer = await sessions.observe(peer, 'peer');
    try {
      await test.step('Join the JA peer and verify browser application', async () => {
        if (secret) expect((await peer.request.post('/api/access', { data: { secret: 'observer-test' } })).ok()).toBe(true);
        await peer.goto('/'); await ready(peer, 'ja'); await sessions.checkpoint(owner, 'peer joined');
      });
      const initial = await owner.ready(), epoch = initial.worldEpoch;
      if (['rejected uploads', 'slot and download', 'complete journey'].includes(scenario)) {
        await test.step('Write the named slot and verify downloaded bytes', async () => {
          await openPanel(page, 'saves', locale); await page.getByLabel(text.saveSlot, { exact: true }).selectOption('slot2');
          await button(text.saveGame).click(); await expect(page.getByTestId('save-feedback')).toHaveText(text.saved);
          const download = page.waitForEvent('download'); await button(text.downloadSave).click();
          await exact(readFileSync((await (await download).path())!), original, 'slot download');
        });
        if (scenario !== 'slot and download') await test.step('Reject malformed, incompatible, graph and oversized uploads', async () => {
          const badVersion = JSON.parse(original.toString()); badVersion.saveVersion = 8;
          const badPack = JSON.parse(original.toString()); badPack.requiredPacks[0].catalogHash = '0'.repeat(64);
          const badGraph = JSON.parse(original.toString());
          if (domain === 'station') badGraph.mixed.stationGraph.platforms[0].hostId = '999999';
          else badGraph.mixed.fleet.trains[0].controllerId = '999999';
          for (const [buffer, message] of [[Buffer.from('{broken'), text.invalidSave], [Buffer.from(JSON.stringify(badVersion)), text.incompatibleSave],
            [Buffer.from(JSON.stringify(badPack)), text.incompatibleSave], [Buffer.from(JSON.stringify(badGraph)), text.invalidSave]] as const) {
            const upload = page.getByLabel(text.uploadSave, { exact: true }); await expect(upload).toBeEnabled();
            await upload.setInputFiles({ name: `rejected-${domain}.json`, mimeType: 'application/json', buffer });
            await expect(page.getByTestId('save-feedback')).toContainText(message);
            await exact(await saved(), original, 'rejected upload'); expect((await owner.ready()).worldEpoch).toBe(epoch);
            await sessions.converge(initial, 'rejection preserves both browsers');
            await expect(page.locator('.decision-dialog')).toHaveCount(0);
          }
          const directory = mkdtempSync(join(tmpdir(), `${domain}-oversize-`)), large = join(directory, 'large.json');
          try {
            writeFileSync(large, 'x'); truncateSync(large, 256 * 1024 * 1024 + 1);
            const upload = page.getByLabel(text.uploadSave, { exact: true }); await expect(upload).toBeEnabled(); await upload.setInputFiles(large);
            await expect(page.getByTestId('save-feedback')).toHaveText(text.saveTooLarge); await exact(await saved(), original, 'oversized upload');
          } finally { rmSync(directory, { recursive: true, force: true }); }
        });
        await test.step('Load the named slot and converge on its replacement epoch', async () => {
          await button(text.loadGame).click(); await button(text.confirmLoad).click();
          await owner.publication({ worldEpoch: String(BigInt(epoch) + 1n), revision: '0', tick: '0' });
          await sessions.checkpoint(owner, 'named slot replacement'); await ready(page, locale);
          await exact(await saved(), original, 'loaded slot');
        });
      }
      if (['reconnect', 'complete journey'].includes(scenario)) await test.step('Reconnect the owner while the peer retains its world', async () => {
        const target = await sessions.checkpoint(owner, 'before reconnect');
        const generation = observer.observation.generation;
        await openPanel(page, 'session', locale);
        await owner.reconnect(() => button(text.reconnect).click(), target, 'explicit reconnect');
        await sessions.converge(target, 'both peers after reconnect');
        expect(observer.observation.generation).toBe(generation);
        await exact(await saved(), original, 'reconnected checkpoint');
      });
      if (['restart continuation', 'complete journey'].includes(scenario)) {
        await test.step('Restart the process at the exact checkpoint', async () => {
          await sessions.restartCheckpoint(worldServer, [{ session: owner, secret }, { session: observer, secret: secret ? 'observer-test' : '' }], 'checkpoint restart');
          await ready(page, locale); await ready(peer, 'ja'); await exact(await saved(), original, 'restarted checkpoint');
        });
        await test.step('Match every continuation minute and independent successor save', async () => {
          const end = Number(JSON.parse(expected.toString()).clock.absoluteMinute);
          for (let minute = Number(initial.tick) + 1; minute <= end; minute++) {
            await test.step(`Advance minute ${minute}`, () => sessions.minute(owner, () => page.getByRole('button', { name: text.step, exact: false }).first().click(), `continuation minute ${minute}`));
          }
          await exact(await saved(), expected, 'independent uninterrupted successor save');
          const target = await sessions.checkpoint(owner, 'final continuation');
          await assertCanonicalState(target.mixed, JSON.parse(expected.toString()).mixed, { phase: 'independent fixture successor', peer: 'owner', ...target });
        });
      }
      expect(errors).toEqual([]);
    } finally { await context.close(); }
  };
}
