import { appName } from '../../src/lib/app-identity.ts';
import { expect,test } from './fixtures.ts';
import { clickTile,openPanel,ready,selectTrain } from './browser-helpers.ts';

test('offline demo gate is local, inert, bounded and traps focus without a service', async ({ page }) => {
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

  expect(requests.length).toBeGreaterThan(0); expect(requests.every(url => url.includes('/api/bootstrap?'))).toBe(true);
  // All service traffic here is bootstrap discovery; demo rendering never requests live saves/entities.
});

test('slow join and slow assets never reveal gameplay before a first live draw', async ({ page }) => {
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

test('asset failure keeps sign-in available on a plain backdrop', async ({ page }) => {
  await page.route('**/assets/*.png', route => route.abort());
  await page.route('**/api/bootstrap?*', route => route.fulfill({ status: 401, json: { anonymousAccess: true } }));
  await page.goto('/'); await expect(page.getByRole('alert')).toHaveText('Map images could not load. Reload to retry.');
  await expect(page.getByLabel('Enter access key to join game', { exact: true })).toBeEditable();
  await expect(page.getByRole('button', { name: 'Connect', exact: true })).toBeEnabled();
  await expect(page.getByRole('button', { name: 'Reload', exact: true })).toBeEnabled();
  await expect(page.locator('.session-dialog button.primary')).toHaveCount(1);
  await expect(page.getByRole('button', { name: 'Connect', exact: true })).toHaveClass('primary session-connect');
  await expect(page.locator('.command-bar')).toHaveCount(0);

});

test('disconnect clears targeting, blocks shortcuts and restores same-world camera without replay', async ({ page }) => {
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

  offline = false; await ready(page);
  await expect(page.locator('canvas')).toHaveAttribute('data-camera', camera!); await expect(page.locator('canvas')).toHaveAttribute('data-zoom', zoom!);
  await expect(page.getByRole('button', { name: 'Inspect / drive', exact: true })).toHaveAttribute('aria-pressed', 'true'); expect(commandCount).toBe(0);
});

test('failed leave latches until revocation, then requires explicit empty-key rejoin', async ({ page }) => {
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

  await page.getByRole('button', { name: 'Connect', exact: true }).click(); await ready(page); expect(attempts).toBe(2);
});

test('rejected sign-in stays inline and version mismatch offers reload', async ({ page }) => {
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

});

test('lost authentication clears live identity and requires sign-in again', async ({ page }) => {
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

});
