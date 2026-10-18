import { expect, test } from '@playwright/test';
import { mkdir } from 'node:fs/promises';
import { at, bootstrap, clickSprite, clickTile, closePanel, openPanel, ready, selectStation, selectTrain } from './ux-helpers.ts';
const evidence = '../tmp/ux2-2026-10-18';
test.beforeEach(async ({ page }) => {
  await mkdir(evidence, { recursive: true });
  await page.goto('/'); await ready(page);
  if (await page.getByTestId('clock-state').textContent() === 'Running') await page.getByRole('button', { name: 'Pause', exact: false }).click();
});

test('UX2 dirty close traps focus, consumes Escape once, and supports Keep, Discard and Apply', async ({ page }) => {
  await selectStation(page);
  const input = page.getByLabel('Station name', { exact: true });
  const confirmed = await page.getByTestId('confirmed-name').textContent();
  await input.fill('UX2 編集中');
  // Escape in a native field retains the editor; Escape from the panel opens the decision.
  await page.keyboard.press('Escape'); await expect(page.getByRole('dialog')).toHaveCount(0);
  await page.getByRole('button', { name: 'Close panel', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Unsaved station name', exact: true });
  await expect(dialog).toBeVisible(); await page.locator('canvas').evaluate(el => (el as HTMLElement).focus());
  expect(await dialog.evaluate(el => el.contains(document.activeElement))).toBe(true);
  await page.keyboard.press('Shift+Tab'); expect(await dialog.evaluate(el => el.contains(document.activeElement))).toBe(true);
  await page.screenshot({ path: `${evidence}/dirty-station.png` });
  await page.keyboard.press('Escape'); await expect(dialog).toHaveCount(0);
  await expect(input).toHaveValue('UX2 編集中'); await expect(page.locator('.primary-panel')).toBeVisible();
  await page.getByRole('button', { name: 'Close panel', exact: true }).click();
  await dialog.getByRole('button', { name: 'Keep editing', exact: true }).click(); await expect(input).toHaveValue('UX2 編集中');
  await page.getByRole('button', { name: 'Game ▾', exact: true }).click();
  await page.locator('#game-menu').getByRole('button', { name: 'Settings', exact: true }).click();
  await expect(dialog).toBeVisible();
  await dialog.getByRole('button', { name: 'Keep editing', exact: true }).click();
  await expect(page.locator('.primary-panel h2')).toBeFocused(); await expect(input).toHaveValue('UX2 編集中');

  await page.getByRole('button', { name: 'Close panel', exact: true }).click();
  await dialog.getByRole('button', { name: 'Discard draft', exact: true }).click();
  await expect(page.locator('.primary-panel')).toHaveCount(0); await expect(page.locator('canvas')).toBeFocused();
  await selectStation(page); await expect(input).toHaveValue(confirmed!);
  await input.fill('UX2 適用駅');
  await page.getByRole('button', { name: 'Build track', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Inspect / drive', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await dialog.getByRole('button', { name: 'Apply name', exact: true }).click();
  await expect(dialog).toHaveCount(0); await expect(page.getByRole('button', { name: 'Build track', exact: true })).toHaveAttribute('aria-pressed', 'true');
  expect((await bootstrap(page)).stations[0].name).toBe('UX2 適用駅'); await expect(page.locator('canvas')).toBeFocused();
});

test('UX2 dirty retarget keeps the original entity and stale revisions require Reset', async ({ page, browser }) => {
  await selectStation(page); const station = (await bootstrap(page)).stations[0];
  await page.getByLabel('Station name', { exact: true }).fill('Original draft');
  await clickSprite(page, { x: 16, y: 16, z: 0 }, 'pier');
  const dialog = page.getByRole('dialog'); await expect(dialog).toBeVisible();
  await expect(page.getByTestId('selected-object')).toContainText(station.id);
  await dialog.getByRole('button', { name: 'Keep editing', exact: true }).click();
  const context = await browser.newContext(); const other = await context.newPage();
  try {
    await other.goto('/'); await ready(other); await selectStation(other);
    await other.getByLabel('Station name', { exact: true }).fill('Remote revision');
    await other.getByRole('button', { name: 'Apply name', exact: true }).click();
    await expect(page.getByTestId('confirmed-name')).toHaveText('Remote revision');
    await expect(page.getByLabel('Station name', { exact: true })).toHaveValue('Original draft');
    await expect(page.getByRole('button', { name: 'Apply name', exact: true })).toBeDisabled();
    await page.getByRole('button', { name: 'Close panel', exact: true }).click();
    await expect(dialog.getByRole('button', { name: 'Apply name', exact: true })).toBeDisabled();
    await dialog.getByRole('button', { name: 'Keep editing', exact: true }).click();
    await page.getByRole('button', { name: 'Reset draft', exact: true }).click();
    await expect(page.getByLabel('Station name', { exact: true })).toHaveValue('Remote revision');
    await closePanel(page); await clickSprite(page, { x: 16, y: 16, z: 0 }, 'pier');
    await expect(page.getByTestId('selected-object')).toContainText('building-1');
  } finally { await context.close(); }
});

test('UX2 replacement modal blocks the game, cancels without changes and preserves safe slot choice', async ({ page }) => {
  await openPanel(page, 'saves');
  await page.getByLabel('Slot', { exact: true }).selectOption('slot2');
  await page.getByRole('button', { name: 'Save slot', exact: true }).click();
  await expect(page.getByTestId('save-feedback')).toHaveText('Save completed');
  await openPanel(page, 'settings'); await openPanel(page, 'saves');
  await expect(page.getByLabel('Slot', { exact: true })).toHaveValue('slot2');
  const before = await bootstrap(page), camera = await page.locator('canvas').getAttribute('data-camera');
  await page.getByRole('button', { name: 'Load slot', exact: true }).click();
  const dialog = page.getByRole('dialog'); await expect(dialog).toContainText('every participant');
  await page.screenshot({ path: `${evidence}/shared-replacement.png` });
  await page.mouse.click(600, 450); await page.mouse.wheel(0, -250); await page.keyboard.press('ArrowUp');
  await expect(dialog).toBeVisible(); await expect(page.locator('canvas')).toHaveAttribute('data-camera', camera!);
  await page.keyboard.press('Escape'); await expect(dialog).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Load slot', exact: true })).toBeFocused();
  expect(await bootstrap(page)).toEqual(before);
});

test('UX2 panel/menu gestures isolate the world, return focus and invalidate geometry', async ({ page }) => {
  await page.getByRole('button', { name: 'Build track', exact: true }).click(); await clickTile(page, { x: 37, y: 16, z: 1 });
  await expect(page.getByTestId('preview-position')).toHaveText('37, 16, 1');
  await openPanel(page, 'catalog');
  await expect(page.getByRole('button', { name: 'Build track', exact: true })).toHaveAttribute('aria-pressed', 'true');
  const canvas = page.locator('canvas'), zoom = await canvas.getAttribute('data-zoom');
  const catalog = page.locator('.catalog'); const box = (await catalog.boundingBox())!;
  await page.mouse.move(box.x + 20, box.y + 40); await page.mouse.wheel(0, -300);
  await expect(canvas).toHaveAttribute('data-zoom', zoom!);
  await catalog.getByRole('button').last().click();
  await closePanel(page); await expect(page.getByTestId('preview-position')).toHaveText('Select a tile on the map');
  await expect(canvas).toBeFocused();
  await page.getByRole('button', { name: 'Game ▾', exact: true }).click();
  await page.mouse.click(600, 400); await expect(page.locator('#game-menu')).toHaveCount(0);
  await expect(page.getByTestId('preview-position')).toHaveText('Select a tile on the map');
  await expect(page.getByRole('button', { name: 'Game ▾', exact: true })).toBeFocused();
  await openPanel(page, 'catalog'); await expect(page.getByTestId('building-name')).toHaveText('Condominium 1');
  await closePanel(page); await page.getByRole('button', { name: 'Inspect / drive', exact: true }).click();
  await clickSprite(page, { x: 16, y: 16, z: 0 }, 'pier');
  await openPanel(page, 'catalog'); await expect(page.getByTestId('building-name')).toHaveText('Condominium 1');
});

test('UX2 middle and Space pan never pick or target, including Space released before pointer-up', async ({ page }) => {
  const canvas = page.locator('canvas');
  await page.getByRole('button', { name: 'Build track', exact: true }).click();
  const p = await at(page, { x: 22, y: 16, z: 0 }); const camera = await canvas.getAttribute('data-camera');
  await page.mouse.move(p.x, p.y); await page.mouse.down({ button: 'middle' }); await page.mouse.move(p.x + 60, p.y + 20); await page.mouse.up({ button: 'middle' });
  await expect(canvas).not.toHaveAttribute('data-camera', camera!); await expect(page.getByTestId('preview-position')).toHaveText('Select a tile on the map');
  await canvas.focus(); await page.keyboard.down('Space'); await page.mouse.down(); await page.keyboard.up('Space'); await page.mouse.up();
  await expect(page.getByTestId('preview-position')).toHaveText('Select a tile on the map');
  await page.keyboard.down('Space'); await page.mouse.down(); await page.mouse.move(p.x + 100, p.y + 50); await page.keyboard.up('Space'); await page.mouse.up();
  await expect(page.getByTestId('preview-position')).toHaveText('Select a tile on the map');
  await clickTile(page, { x: 37, y: 16, z: 1 });
  await page.getByLabel('Track shape', { exact: true }).selectOption('eastWest');
  await expect(page.getByRole('button', { name: 'Confirm track change', exact: true })).toBeDisabled();
  await clickTile(page, { x: 37, y: 16, z: 1 }); await canvas.focus(); await page.keyboard.press('Escape');
  await expect(page.getByTestId('preview-position')).toHaveText('Select a tile on the map');
  await expect(page.getByRole('button', { name: 'Build track', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await page.keyboard.press('Escape'); await expect(page.getByRole('button', { name: 'Inspect / drive', exact: true })).toHaveAttribute('aria-pressed', 'true');
});

test('UX2 schemes persist across refresh and gates without changing camera, canvas, draft or world', async ({ page }) => {
  const errors: string[] = []; page.on('pageerror', error => errors.push(error.message));
  await page.locator('canvas').evaluate(el => el.setAttribute('data-instance', 'same'));
  const oldZoom = await page.locator('canvas').getAttribute('data-zoom');
  await page.getByRole('button', { name: 'Zoom in', exact: true }).click();
  await expect(page.locator('canvas')).not.toHaveAttribute('data-zoom', oldZoom!);
  const canvas = page.locator('canvas'), camera = await canvas.getAttribute('data-camera'), zoom = await canvas.getAttribute('data-zoom');
  const before = await bootstrap(page);
  await page.getByRole('button', { name: 'Build track', exact: true }).click();
  await openPanel(page, 'settings');
  for (const scheme of ['classic-blue', 'slate', 'plum', 'evergreen']) {
    await page.getByLabel('UI color scheme', { exact: true }).selectOption(scheme);
    await expect(page.locator('html')).toHaveAttribute('data-ui-scheme', scheme);
    await expect(canvas).toHaveAttribute('data-instance', 'same'); await expect(canvas).toHaveAttribute('data-camera', camera!); await expect(canvas).toHaveAttribute('data-zoom', zoom!);
    await expect(page.getByRole('button', { name: 'Build track', exact: true })).toHaveAttribute('aria-pressed', 'true');
  }
  await page.getByLabel('UI color scheme', { exact: true }).selectOption('plum');
  expect(await page.evaluate(() => JSON.parse(localStorage.getItem('freetrain.ui.v1')!))).toEqual({ version: 1, scheme: 'plum' });
  expect(await bootstrap(page)).toEqual(before);
  await page.reload(); await ready(page); await expect(page.locator('html')).toHaveAttribute('data-ui-scheme', 'plum');
  await selectStation(page); await page.getByLabel('Station name', { exact: true }).fill('Preserve my draft');
  await page.getByRole('button', { name: 'Reset draft', exact: true }).click();
  await openPanel(page, 'session'); await page.getByRole('button', { name: 'Leave game', exact: true }).click();
  await expect(page.getByLabel('Operator access key', { exact: true })).toBeVisible(); await expect(page.getByLabel('UI color scheme', { exact: true })).toHaveValue('plum');
  await page.getByLabel('UI color scheme', { exact: true }).selectOption('classic-blue');
  await page.getByLabel('Language', { exact: true }).selectOption('ja'); await page.setViewportSize({ width: 360, height: 800 });
  await expect(page.getByLabel('操作キー', { exact: true })).toBeVisible();
  await page.screenshot({ path: `${evidence}/scheme-gate-ja.png` });
  await page.reload(); await expect(page.getByLabel('UI配色', { exact: true })).toHaveValue('classic-blue'); expect(errors).toEqual([]);
});

test('UX2 invalid or obsolete stored scheme falls back before gameplay', async ({ page }) => {
  for (const stored of ['not JSON', '{"version":1,"scheme":"unknown"}', '{"version":2,"scheme":"plum"}']) {
    await page.evaluate(value => localStorage.setItem('freetrain.ui.v1', value), stored); await page.reload(); await ready(page);
    await expect(page.locator('html')).toHaveAttribute('data-ui-scheme', 'evergreen');
  }
});

test('UX2 disconnect cancels dirty decisions without replaying a name', async ({ page }) => {
  let drop!: () => void, offline = false, renames = 0;
  await page.route('**/api/bootstrap?*', route => offline ? route.fulfill({ status: 503 }) : route.continue());
  await page.routeWebSocket('**/api/session?*', route => {
    const server = route.connectToServer(); drop = () => { offline = true; server.close(); route.close(); };
    route.onMessage(message => { if (JSON.parse(String(message)).payload?.kind === 'renameStation') renames++; server.send(message); });
  });
  await page.reload(); await ready(page); await selectStation(page);
  const before = (await bootstrap(page)).stations[0].name;
  await page.getByLabel('Station name', { exact: true }).fill('Interrupted draft');
  await page.getByRole('button', { name: 'Close panel', exact: true }).click(); await expect(page.getByRole('dialog')).toContainText('Interrupted draft');
  drop(); await expect(page.getByRole('heading', { name: 'Connection lost', exact: true })).toBeVisible();
  await expect(page.locator('.decision-dialog')).toHaveCount(0);
  offline = false; await ready(page); await selectStation(page);
  await expect(page.getByLabel('Station name', { exact: true })).toHaveValue(before); expect(renames).toBe(0);
});


test('UX2 an open menu suspends driving even if keyboard focus returns to the canvas', async ({ page }) => {
  await selectTrain(page);
  await page.getByRole('combobox', { name: 'Controller', exact: true }).selectOption('manual');
  await expect(page.getByRole('status')).toHaveText('Confirmed by server');
  const canvas = page.locator('canvas'); await canvas.focus(); await page.keyboard.down('ArrowRight');
  await expect(page.getByTestId('manual-input')).toContainText('ArrowRight');
  await page.getByRole('button', { name: 'Game ▾', exact: true }).click();
  await expect(page.getByTestId('manual-input')).toHaveText('Keys released'); await page.keyboard.up('ArrowRight');
  await canvas.focus(); await page.keyboard.press('ArrowLeft');
  await expect(page.getByTestId('manual-input')).toHaveText('Keys released');
  await expect(page.locator('#game-menu')).toBeVisible();
  await page.keyboard.press('Escape'); await expect(page.locator('#game-menu')).toHaveCount(0);
  await page.getByRole('combobox', { name: 'Controller', exact: true }).selectOption('automatic');
  await page.getByRole('button', { name: 'Release control', exact: true }).click();
});
