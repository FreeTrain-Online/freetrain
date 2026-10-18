import { expect, test, type Page } from '@playwright/test';
import { ready, openPanel, selectStation, selectTrain } from './ux-helpers.ts';

test('independent browsers share edits and leases; reconnect and hidden tabs preserve the shared world', async ({ browser }) => {
  const first = await browser.newContext({ locale: 'en-US' });
  const second = await browser.newContext({ locale: 'en-US' });
  const a = await first.newPage(), b = await second.newPage();
  const errors: string[] = [];
  for (const page of [a, b]) page.on('pageerror', error => errors.push(error.message));
  try {
    await Promise.all([a.goto('/'), b.goto('/')]); await ready(a); await ready(b);
    const bootstrap = async (page: Page) => (await (await page.request.get('/api/bootstrap?protocolVersion=2&contentVersion=prototype-content-1')).json());
    const alice = await bootstrap(a), bob = await bootstrap(b);
    expect(alice.participantId).not.toBe(bob.participantId);
    expect(alice.serverIncarnation).toBe(bob.serverIncarnation); expect(alice.worldEpoch).toBe(bob.worldEpoch);
    await expect(a.getByTestId('participant-role')).toHaveText('Owner');
    if (!alice.snapshot.paused) await a.getByRole('button', { name: 'Pause', exact: false }).click();
    await expect(b.getByTestId('clock-state')).toHaveText('Paused');
    await selectTrain(a); await selectTrain(b);
    await a.getByRole('button', { name: 'Take control', exact: true }).click();
    await expect(b.getByTestId('control-holder')).toContainText(alice.participantId);
    await b.getByRole('button', { name: 'Take control', exact: true }).click();
    await expect(b.getByRole('status')).toHaveText('Another participant controls this train.');
    await expect(b.getByRole('button', { name: 'Release control', exact: true })).toBeDisabled();
    await a.getByRole('button', { name: 'Release control', exact: true }).click();
    await expect(b.getByTestId('control-holder')).toContainText('Available');
    await b.getByRole('button', { name: 'Take control', exact: true }).click();
    await expect(a.getByTestId('control-holder')).toContainText(bob.participantId);
    await b.getByRole('button', { name: 'Release control', exact: true }).click();
    await selectStation(a); await selectStation(b);
    await b.getByLabel('Station name', { exact: true }).fill('Unsubmitted draft');
    await a.getByLabel('Station name', { exact: true }).fill('共有する駅');
    await a.getByRole('button', { name: 'Apply name', exact: true }).click();
    await expect(b.getByTestId('confirmed-name')).toHaveText('共有する駅');
    await expect(b.getByLabel('Station name', { exact: true })).toHaveValue('Unsubmitted draft');
    await b.reload(); await ready(b); await selectStation(b);
    expect((await bootstrap(b)).participantId).toBe(bob.participantId);
    await expect(b.getByTestId('confirmed-name')).toHaveText('共有する駅');
    await a.getByRole('button', { name: 'Run', exact: false }).click();
    await expect(b.getByTestId('clock-state')).toHaveText('Running');
    const tick = await a.getByTestId('tick').textContent();
    await b.evaluate(() => { Object.defineProperty(document, 'hidden', { configurable: true, value: true }); document.dispatchEvent(new Event('visibilitychange')); });
    await expect(a.getByTestId('tick')).not.toHaveText(tick!);
    await expect(a.getByTestId('clock-state')).toHaveText('Running');
    await a.getByRole('button', { name: 'Pause', exact: false }).click();
    await expect(a.getByTestId('clock-state')).toHaveText('Paused');
    expect(errors).toEqual([]);
  } finally { await first.close(); await second.close(); }
});

test('Leave game signs out, releases control and allows changing roles or rejoining locally', async ({ browser }) => {
  const first = await browser.newContext({ locale: 'en-US' });
  const second = await browser.newContext({ locale: 'en-US' });
  const a = await first.newPage(), b = await second.newPage();
  const errors: string[] = [];
  for (const page of [a, b]) page.on('pageerror', error => errors.push(error.message));
  const login = async (key: string, role: string) => {
    await a.getByLabel('Operator access key', { exact: true }).fill(key);
    await a.getByRole('button', { name: 'Connect', exact: true }).click();
    await ready(a); await expect(a.getByTestId('participant-role')).toHaveText(role);
  };
  try {
    await Promise.all([a.goto('/'), b.goto('/')]); await ready(a); await ready(b);
    const snapshot = (await (await a.request.get('/api/bootstrap?protocolVersion=2&contentVersion=prototype-content-1')).json()).snapshot;
    if (!snapshot.paused) await a.getByRole('button', { name: 'Pause', exact: false }).click();
    await selectTrain(a); await selectTrain(b);
    await a.getByRole('button', { name: 'Take control', exact: true }).click();
    await expect(a.getByRole('status')).toHaveText('Confirmed by server');
    await b.getByRole('button', { name: 'Run', exact: false }).click();
    await expect(a.getByTestId('clock-state')).toHaveText('Running');
    await openPanel(a, 'session');
    await a.getByRole('button', { name: 'Leave game', exact: true }).click();
    await expect(a.getByLabel('Operator access key', { exact: true })).toBeVisible();
    await expect(a.getByTestId('participant-role')).toHaveCount(0); await expect(a.locator('canvas')).toHaveAttribute('data-scene', 'demo');
    await expect(b.getByTestId('control-holder')).toContainText('Available');
    await expect(b.getByTestId('clock-state')).toHaveText('Running');
    const tick = await b.getByTestId('tick').textContent(); await expect(b.getByTestId('tick')).not.toHaveText(tick!);
    await a.reload(); await expect(a.getByLabel('Operator access key', { exact: true })).toBeVisible();
    await expect(a.getByTestId('connection')).toHaveText('Operator access required');
    await login('operator-test', 'Operator');
    await openPanel(a, 'saves');
    await expect(a.getByRole('button', { name: 'Save slot', exact: true })).toBeDisabled();
    await openPanel(a, 'session');
    await a.getByRole('button', { name: 'Leave game', exact: true }).click();
    await expect(a.getByLabel('Operator access key', { exact: true })).toBeVisible();
    await login('observer-test', 'Observer');
    await expect(a.getByRole('button', { name: 'Pause', exact: false })).toBeDisabled();
    await openPanel(a, 'settings');
    await a.getByLabel('Language').selectOption('ja');
    await openPanel(a, 'session', 'ja');
    await a.getByRole('button', { name: 'ゲームから退出', exact: true }).click();
    await expect(a.getByLabel('操作キー', { exact: true })).toBeVisible();
    await a.getByLabel('言語').selectOption('en');
    await login('', 'Owner');
    await b.getByRole('button', { name: 'Pause', exact: false }).click();
    await expect(b.getByTestId('clock-state')).toHaveText('Paused');
    expect(errors).toEqual([]);
  } finally { await first.close(); await second.close(); }
});

test('a failed Leave game request offers a retry and clears the displayed world', async ({ page }) => {
  await page.goto('/'); await ready(page);
  if (await page.getByTestId('clock-state').textContent() === 'Running')
    await page.getByRole('button', { name: 'Pause', exact: false }).click();
  await expect(page.getByTestId('clock-state')).toHaveText('Paused');
  let attempts = 0;
  await page.route('**/api/leave', async route => {
    if (++attempts === 1) await route.fulfill({ status: 503 });
    else await route.continue();
  });
  await openPanel(page, 'session');
  await page.getByRole('button', { name: 'Leave game', exact: true }).click();
  await expect(page.getByRole('status')).toHaveText('Leaving could not be confirmed. Retry Leave game to sign out.');
  await expect(page.locator('canvas')).toHaveAttribute('data-scene', 'demo');
  await page.getByRole('button', { name: 'Retry leave', exact: true }).click();
  await expect(page.getByLabel('Operator access key', { exact: true })).toBeVisible();
  expect(attempts).toBe(2);
});
