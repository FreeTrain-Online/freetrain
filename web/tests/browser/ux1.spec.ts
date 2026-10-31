import { expect, test } from '@playwright/test';
import { appName } from '../../src/lib/app-identity.ts';
import { at, bootstrap, clickTile, closePanel, openPanel, ready, selectStation, selectTrain, worldReplacementFixture } from './ux-helpers.ts';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
const evidence = process.env.FREETRAIN_ONLINE_UX1_EVIDENCE!;

test('UX1 fixed viewport, persistent canvas and overlay-independent camera at target sizes', async ({ page }) => {
  const errors: string[] = []; page.on('pageerror', error => errors.push(error.message));
  await page.setViewportSize({ width: 1280, height: 720 });
  await page.addInitScript(() => {
    const testWindow = window as unknown as { mapDecodes: number }; testWindow.mapDecodes = 0;
    const decode = HTMLImageElement.prototype.decode;
    HTMLImageElement.prototype.decode = function () { testWindow.mapDecodes++; return decode.call(this); };
  });
  await page.goto('/'); await ready(page);
  const canvas = page.locator('canvas'); await canvas.evaluate(el => { el.setAttribute('data-instance', 'original'); });
  const box = (await canvas.boundingBox())!;
  expect(box.width * box.height / (1280 * 720)).toBeCloseTo(.91493, 4);
  const before = await bootstrap(page);
  const decodes = await page.evaluate(() => (window as unknown as { mapDecodes: number }).mapDecodes);
  const observations: unknown[] = [{ viewport: { width: 1280, height: 720 }, canvas: box, canvasCoverage: box.width * box.height / (1280 * 720), decodes }];
  const center = await at(page, { x: 22, y: 16, z: 0 });
  await page.mouse.move(center.x, center.y); await page.mouse.down(); await page.mouse.move(center.x + 70, center.y + 25); await page.mouse.up();
  const camera = await canvas.getAttribute('data-camera'), zoom = await canvas.getAttribute('data-zoom');
  await mkdir(evidence, { recursive: true });
  await page.screenshot({ path: `${evidence}/desktop-rest.png` });
  for (const id of ['settings', 'view', 'catalog', 'saves', 'help', 'session']) {
    await openPanel(page, id);
    await expect(canvas).toHaveAttribute('data-camera', camera!); await expect(canvas).toHaveAttribute('data-zoom', zoom!);
    expect(await canvas.boundingBox()).toEqual(box); await expect(canvas).toHaveAttribute('data-instance', 'original');
    expect(await page.evaluate(() => (window as unknown as { mapDecodes: number }).mapDecodes)).toBe(decodes);
    await closePanel(page);
  }
  expect(await bootstrap(page)).toEqual(before);
  for (const [width, height] of [[1920, 1080], [1280, 720], [1024, 768], [800, 900], [640, 900], [360, 800], [800, 450], [640, 450], [360, 450]]) {
    await page.setViewportSize({ width, height });
    await openPanel(page, 'settings'); await page.getByLabel('Language', { exact: true }).selectOption('ja');
    await openPanel(page, 'help', 'ja');
    const bounds = await page.evaluate(() => ({ w: document.documentElement.scrollWidth, h: document.documentElement.scrollHeight, x: scrollX, y: scrollY }));
    expect(bounds).toEqual({ w: width, h: height, x: 0, y: 0 });
    const panel = (await page.locator('.primary-panel').boundingBox())!;
    expect(panel.x).toBeGreaterThanOrEqual(0); expect(panel.y).toBeGreaterThanOrEqual(0);
    expect(panel.x + panel.width).toBeLessThanOrEqual(width); expect(panel.y + panel.height).toBeLessThanOrEqual(height);
    observations.push({ viewport: { width, height }, document: bounds, panel });
    await page.screenshot({ path: `${evidence}/help-ja-${width}x${height}.png` });
    await openPanel(page, 'settings', 'ja'); await page.getByLabel('言語', { exact: true }).selectOption('en'); await closePanel(page);
  }
  expect(errors).toEqual([]);
  await writeFile(`${evidence}/layout-observations.json`, JSON.stringify(observations, null, 2) + '\n');
});

test('UX1 selected inspectors, clock and explicit rail confirm/cancel remain playable', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 720 });
  await page.goto('/'); await ready(page);
  if (await page.getByTestId('clock-state').textContent() === 'Running') await page.getByRole('button', { name: 'Ⅱ Pause', exact: true }).click();
  await selectTrain(page); await expect(page.getByRole('combobox', { name: 'Controller', exact: true })).toBeVisible();
  await page.screenshot({ path: `${evidence}/desktop-inspector.png` });
  await selectStation(page); const old = (await bootstrap(page)).stations[0].name;
  await page.getByLabel('Station name', { exact: true }).fill('UX1 検証駅'); await page.getByRole('button', { name: 'Apply name', exact: true }).click();
  await expect(page.getByTestId('confirmed-name')).toHaveText('UX1 検証駅');
  await page.getByLabel('Station name', { exact: true }).fill(old); await page.getByRole('button', { name: 'Apply name', exact: true }).click();
  await expect(page.getByTestId('confirmed-name')).toHaveText(old); await closePanel(page);
  await page.getByRole('button', { name: 'Run', exact: false }).click(); await expect(page.getByTestId('clock-state')).toHaveText('Running');
  await page.getByRole('button', { name: 'Build track', exact: true }).click(); await expect(page.getByTestId('clock-state')).toHaveText('Running');
  await page.getByRole('button', { name: 'Pause', exact: false }).click(); await expect(page.getByTestId('clock-state')).toHaveText('Paused');
  const before = await bootstrap(page);
  await clickTile(page, { x: 37, y: 16, z: 1 }); await expect(page.getByTestId('preview-position')).toHaveText('37, 16, 1');
  await page.screenshot({ path: `${evidence}/desktop-rail.png` });
  await page.getByRole('button', { name: 'Cancel preview', exact: true }).click();
  expect(await bootstrap(page)).toEqual(before);
  await clickTile(page, { x: 37, y: 16, z: 1 });
  await page.getByRole('button', { name: 'Confirm track change', exact: true }).dblclick(); await expect(page.getByRole('status')).toHaveText('Confirmed by server');
  await expect(page.getByRole('button', { name: 'Confirm track change', exact: true })).toBeDisabled();
  expect(BigInt(before.cash) - BigInt((await bootstrap(page)).cash)).toBe(before.tracks.find((track:any)=>track.position.x===36&&track.position.y===16&&track.position.z===1)?.shape==='buffer'?200n:100n);
  await page.getByRole('button', { name: 'Remove', exact: true }).click(); await clickTile(page, { x: 37, y: 16, z: 1 });
  await page.getByRole('button', { name: 'Confirm track change', exact: true }).click(); await expect(page.getByRole('status')).toHaveText('Confirmed by server');
});

test('UX1 offline demo gate is local, inert, bounded and traps focus without a service', async ({ page }) => {
  const requests: string[] = [];
  await page.route('**/api/**', route => { requests.push(route.request().url()); return route.fulfill({ status: 503 }); });
  await page.goto('/');
  await expect(page.getByRole('button', { name: 'Retry now', exact: true })).toBeVisible();
  await expect(page.getByRole('dialog', { name: appName, exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Cannot reach game', exact: true })).toHaveCount(0);
  await expect(page.getByTestId('connection')).toHaveCount(0);
  await expect(page.locator('canvas')).toHaveAttribute('data-scene', 'demo');
  await expect(page.locator('.command-bar')).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Connect', exact: true })).toBeDisabled();
  const dialog = page.getByRole('dialog');
  await page.keyboard.press('Escape'); await page.mouse.click(4, 4); await expect(dialog).toBeVisible();
  await page.locator('canvas').evaluate(el => (el as HTMLElement).focus());
  expect(await dialog.evaluate(el => el.contains(document.activeElement))).toBe(true);
  await page.keyboard.press('Shift+Tab'); expect(await dialog.evaluate(el => el.contains(document.activeElement))).toBe(true);
  await page.keyboard.press('Tab'); expect(await dialog.evaluate(el => el.contains(document.activeElement))).toBe(true);
  const demo = await page.locator('canvas').getAttribute('data-camera');
  await page.mouse.move(40, 40); await page.mouse.wheel(0, -300); await page.keyboard.press('ArrowUp');
  await expect(page.locator('canvas')).toHaveAttribute('data-camera', demo!);
  await page.screenshot({ path: `${evidence}/unavailable-demo.png` });
  expect(requests.length).toBeGreaterThan(0); expect(requests.every(url => url.includes('/api/bootstrap?'))).toBe(true);
  // All service traffic here is bootstrap discovery; demo rendering never requests live saves/entities.
});

test('UX1 slow join and slow assets never reveal gameplay before a first live draw', async ({ page }) => {
  let releaseAssets!: () => void, releaseJoin!: () => void;
  const assets = new Promise<void>(resolve => { releaseAssets = resolve; }), join = new Promise<void>(resolve => { releaseJoin = resolve; });
  await page.route('**/assets/*.png', async route => { await assets; await route.continue(); });
  await page.routeWebSocket('**/api/session?*', route => {
    const server = route.connectToServer(); server.onMessage(async message => { await join; route.send(message); });
  });
  await page.goto('/', { waitUntil: 'domcontentloaded' }); await expect(page.getByRole('heading', { name: 'Loading game…', exact: true })).toBeVisible();
  await expect(page.locator('.command-bar')).toHaveCount(0);
  releaseJoin(); await expect(page.getByTestId('connection')).toHaveText('Connected to game');
  await expect(page.locator('.command-bar')).toHaveCount(0); await expect(page.getByRole('dialog')).toBeVisible();
  releaseAssets(); await ready(page); await expect(page.getByRole('dialog')).toHaveCount(0);
});

test('UX1 asset failure keeps sign-in available on a plain backdrop', async ({ page }) => {
  await page.route('**/assets/*.png', route => route.abort());
  await page.route('**/api/bootstrap?*', route => route.fulfill({ status: 401, json: { anonymousAccess: true } }));
  await page.goto('/'); await expect(page.getByRole('alert')).toHaveText('Map images could not load. Reload to retry.');
  await expect(page.getByLabel('Enter access key to join game', { exact: true })).toBeEditable();
  await expect(page.getByRole('button', { name: 'Connect', exact: true })).toBeEnabled();
  await expect(page.getByRole('button', { name: 'Reload', exact: true })).toBeEnabled();
  await expect(page.locator('.session-dialog button.primary')).toHaveCount(1);
  await expect(page.getByRole('button', { name: 'Connect', exact: true })).toHaveClass('primary session-connect');
  await expect(page.locator('.command-bar')).toHaveCount(0);
  await page.screenshot({ path: `${evidence}/asset-fallback.png` });
});

test('UX1 disconnect clears targeting, blocks shortcuts and restores same-world camera without replay', async ({ page }) => {
  let drop!: () => void, offline = false, commandCount = 0;
  await page.route('**/api/bootstrap?*', route => offline ? route.fulfill({ status: 503 }) : route.continue());
  await page.routeWebSocket('**/api/session?*', route => {
    const server = route.connectToServer(); drop = () => { offline = true; server.close(); route.close(); };
    route.onMessage(message => { if (JSON.parse(String(message)).type === 'command') commandCount++; server.send(message); });
  });
  await page.goto('/'); await ready(page);
  const initialZoom = await page.locator('canvas').getAttribute('data-zoom');
  await page.getByRole('button', { name: 'Zoom in', exact: true }).click();
  await expect(page.locator('canvas')).not.toHaveAttribute('data-zoom', initialZoom!);
  const camera = await page.locator('canvas').getAttribute('data-camera'), zoom = await page.locator('canvas').getAttribute('data-zoom');
  await page.getByRole('button', { name: 'Build track', exact: true }).click(); await clickTile(page, { x: 37, y: 16, z: 1 });
  drop(); await expect(page.getByRole('dialog')).toBeVisible(); await expect(page.locator('canvas')).toHaveAttribute('data-scene', 'demo');
  await page.keyboard.press('Escape'); await page.keyboard.press('ArrowUp');
  await expect(page.locator('.tool-card')).toHaveCount(0); await expect(page.locator('.tool-strip')).toHaveCount(0);
  await page.screenshot({ path: `${evidence}/disconnected-demo.png` });
  offline = false; await ready(page);
  await expect(page.locator('canvas')).toHaveAttribute('data-camera', camera!); await expect(page.locator('canvas')).toHaveAttribute('data-zoom', zoom!);
  await expect(page.getByRole('button', { name: 'Inspect / drive', exact: true })).toHaveAttribute('aria-pressed', 'true'); expect(commandCount).toBe(0);
});

test('UX1 failed leave latches until revocation, then requires explicit empty-key rejoin', async ({ page }) => {
  await page.goto('/'); await ready(page);
  let attempts = 0, bootstraps = 0;
  await page.route('**/api/leave', route => ++attempts === 1 ? route.fulfill({ status: 503 }) : route.continue());
  page.on('request', request => { if (request.url().includes('/api/bootstrap')) bootstraps++; });
  await openPanel(page, 'session'); await page.getByRole('button', { name: 'Leave game', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Leave not confirmed', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Retry now', exact: true })).toHaveCount(0); await expect(page.getByRole('button', { name: 'Connect', exact: true })).toHaveCount(0);
  await expect(page.locator('canvas')).toHaveAttribute('data-scene', 'demo');
  const count = bootstraps; await page.waitForTimeout(600); expect(bootstraps).toBe(count);
  await page.getByRole('button', { name: 'Retry leave', exact: true }).click();
  await expect(page.getByLabel('Enter access key to join game', { exact: true })).toBeVisible(); await expect(page.locator('.command-bar')).toHaveCount(0);
  await page.screenshot({ path: `${evidence}/signed-out.png` });
  await page.getByRole('button', { name: 'Connect', exact: true }).click(); await ready(page); expect(attempts).toBe(2);
});

test('UX1 rejected sign-in stays inline and version mismatch offers reload', async ({ page }) => {
  let incompatible = false, attempts = 0;
  await page.route('**/api/bootstrap?*', route => route.fulfill({ status: incompatible ? 426 : 401, json: { anonymousAccess: false } }));
  await page.route('**/api/access', route => { attempts++; return route.fulfill({ status: 401 }); });
  await page.goto('/'); await expect(page.getByLabel('Enter access key to join game', { exact: true })).toBeVisible();
  await expect(page.getByRole('dialog', { name: appName, exact: true })).toBeVisible();
  await expect(page.getByRole('dialog').getByRole('heading')).toHaveCount(0);
  await expect(page.getByText('Operator access required', { exact: true })).toHaveCount(0);
  await expect(page.getByText('Ready', { exact: true })).toBeHidden();
  await page.getByLabel('Enter access key to join game', { exact: true }).fill('invalid-test-key');
  await page.getByRole('button', { name: 'Connect', exact: true }).click();
  await expect(page.getByRole('alert')).toHaveText('Access failed. Check the key and try again.');
  await expect(page.getByLabel('Enter access key to join game', { exact: true })).toHaveValue(''); expect(attempts).toBe(1);
  await expect(page.locator('canvas')).toHaveAttribute('data-scene', 'demo');
  incompatible = true; await page.reload();
  await expect(page.getByRole('heading', { name: 'Update required', exact: true })).toBeVisible();
  await expect(page.getByText('Client and server versions differ. Reload after updating.', { exact: true })).toHaveCount(1);
  await expect(page.getByRole('button', { name: 'Reload', exact: true })).toBeEnabled();
  await expect(page.getByRole('button', { name: 'Retry now', exact: true })).toHaveCount(0);
  await page.screenshot({ path: `${evidence}/incompatible.png` });
});

test('UX1 lost authentication clears live identity and requires sign-in again', async ({ page }) => {
  let drop!: () => void, revoked = false;
  await page.route('**/api/bootstrap?*', route => revoked ? route.fulfill({ status: 401, json: { anonymousAccess: false } }) : route.continue());
  await page.routeWebSocket('**/api/session?*', route => { const server = route.connectToServer(); drop = () => { revoked = true; server.close(); route.close(); }; });
  await page.goto('/'); await ready(page); await selectTrain(page);
  drop();
  await expect(page.getByRole('heading', { name: 'Sign in again', exact: true })).toBeVisible();
  await expect(page.getByTestId('participant-role')).toHaveCount(0); await expect(page.getByTestId('selected-object')).toHaveCount(0);
  await expect(page.locator('canvas')).toHaveAttribute('data-scene', 'demo');
  await expect(page.getByLabel('Enter access key to join game', { exact: true })).toBeEditable();
  await page.getByLabel('Language', { exact: true }).selectOption('ja');
  await page.setViewportSize({ width: 360, height: 800 });
  await page.screenshot({ path: `${evidence}/auth-required-ja.png` });
});

test('UX1 epoch snapshot fixture discards old geometry and restores initial framing', async ({ page }) => {
  const replaceWorld=await worldReplacementFixture(page);
  await page.goto('/'); await ready(page);
  await page.getByRole('button', { name: 'Reset camera', exact: true }).click();
  await page.locator('canvas').evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
  const homeZoom = await page.locator('canvas').getAttribute('data-zoom');
  const homeCamera = await page.locator('canvas').getAttribute('data-camera');
  await page.getByRole('button', { name: 'Zoom in', exact: true }).click();
  await page.getByRole('button', { name: 'Build track', exact: true }).click(); await clickTile(page, { x: 37, y: 16, z: 1 });
  await expect(page.getByTestId('preview-position')).toHaveText('37, 16, 1');
  const next=await replaceWorld();
  await expect(page.locator('canvas')).toHaveAttribute('data-world-epoch',next.worldEpoch);
  await expect(page.getByRole('button', { name: 'Inspect / drive', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('.tool-card')).toHaveCount(0); await expect(page.locator('canvas')).toHaveAttribute('data-zoom', homeZoom!);
  await expect(page.locator('canvas')).toHaveAttribute('data-camera', homeCamera!);
});
