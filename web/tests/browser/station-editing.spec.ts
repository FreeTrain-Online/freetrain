import { expect,test } from './fixtures.ts';
import { bootstrap,clickSprite,closePanel,ready,selectStation } from './browser-helpers.ts';

test('dirty close traps focus, consumes Escape once, and supports Keep, Discard and Apply', async ({ page }) => {
  await page.goto('/'); await ready(page);
  if (await page.getByTestId('clock-state').textContent() === 'Running') await page.getByRole('button', { name: 'Pause', exact: false }).click();
  await selectStation(page);
  const input = page.getByLabel('Station name', { exact: true });
  const confirmed = await page.getByTestId('confirmed-name').textContent();
  await input.fill('UX2 編集中');
  // Escape in a native field retains the editor; Escape from the panel opens the decision.
  await page.keyboard.press('Escape'); await expect(page.locator('.decision-dialog')).toHaveCount(0);
  await page.getByRole('button', { name: 'Close panel', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Unsaved station name', exact: true });
  await expect(dialog).toBeVisible(); await page.locator('canvas').evaluate(el => (el as HTMLElement).focus());
  expect(await dialog.evaluate(el => el.contains(document.activeElement))).toBe(true);
  await page.keyboard.press('Shift+Tab'); expect(await dialog.evaluate(el => el.contains(document.activeElement))).toBe(true);

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
  await expect(page.getByRole('button', { name: 'Inspect', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await dialog.getByRole('button', { name: 'Apply name', exact: true }).click();
  await expect(dialog).toHaveCount(0); await expect(page.getByRole('button', { name: 'Build track', exact: true })).toHaveAttribute('aria-pressed', 'true');
  expect((await bootstrap(page)).stations[0].name).toBe('UX2 適用駅'); await expect(page.locator('canvas')).toBeFocused();
});

test('dirty retarget keeps the original entity and stale revisions require Reset', async ({ page, browser }) => {
  await page.goto('/'); await ready(page);
  if (await page.getByTestId('clock-state').textContent() === 'Running') await page.getByRole('button', { name: 'Pause', exact: false }).click();
  await selectStation(page); const station = (await bootstrap(page)).stations[0];
  await page.getByLabel('Station name', { exact: true }).fill('Original draft');
  await clickSprite(page, { x: 16, y: 16, z: 0 }, 'pier');
  const dialog = page.locator('.decision-dialog'); await expect(dialog).toBeVisible();
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

test('disconnect cancels dirty decisions without replaying a name', async ({ page }) => {
  await page.goto('/'); await ready(page);
  if (await page.getByTestId('clock-state').textContent() === 'Running') await page.getByRole('button', { name: 'Pause', exact: false }).click();
  let drop!: () => void, offline = false, renames = 0;
  await page.route('**/api/bootstrap?*', route => offline ? route.fulfill({ status: 503 }) : route.continue());
  await page.routeWebSocket('**/api/session?*', route => {
    const server = route.connectToServer(); drop = () => { offline = true; server.close(); route.close(); };
    route.onMessage(message => { if (JSON.parse(String(message)).payload?.kind === 'renameStation') renames++; server.send(message); });
  });
  await page.reload(); await ready(page); await selectStation(page);
  const before = (await bootstrap(page)).stations[0].name;
  await page.getByLabel('Station name', { exact: true }).fill('Interrupted draft');
  await page.getByRole('button', { name: 'Close panel', exact: true }).click(); await expect(page.locator('.decision-dialog')).toContainText('Interrupted draft');
  drop(); await expect(page.getByRole('heading', { name: 'Connection lost', exact: true })).toBeVisible();
  await expect(page.locator('.decision-dialog')).toHaveCount(0);
  offline = false; await ready(page); await selectStation(page);
  await expect(page.getByLabel('Station name', { exact: true })).toHaveValue(before); expect(renames).toBe(0);
});
