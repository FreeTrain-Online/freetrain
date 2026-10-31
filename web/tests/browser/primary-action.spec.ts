import { expect, test, type Locator } from '@playwright/test';
import { appName } from '../../src/lib/app-identity.ts';
import { openPanel, ready, selectStation } from './ux-helpers.ts';

async function soleHighlight(scope: Locator, label: string) {
  const primary = scope.locator('button.primary');
  await expect(primary).toHaveCount(1);
  await expect(primary).toHaveAccessibleName(label);
  const colors = await scope.evaluate(el => {
    const primary = el.querySelector('button.primary')!;
    const background = getComputedStyle(primary).backgroundColor;
    return [...el.querySelectorAll('button')].filter(button =>
      button !== primary && !button.disabled && getComputedStyle(button).backgroundColor === background
    ).map(button => button.textContent);
  });
  expect(colors).toEqual([]);
}

test('informational dialogs and panels have one highlighted default and Return closes them', async ({ page }) => {
  await page.goto('/'); await ready(page);
  for (const label of ['Credits', 'License']) {
    await page.getByRole('button', { name: 'Game ▾', exact: true }).click();
    await page.locator('#game-menu').getByRole('button', { name: label, exact: true }).click();
    const dialog = page.getByRole('dialog', { name: label, exact: true });
    await soleHighlight(dialog, 'Close');
    await expect(dialog).toBeFocused();
    await page.keyboard.press('Enter');
    await expect(dialog).toHaveCount(0);
  }
  for (const panel of ['help', 'catalog', 'view', 'settings', 'session']) {
    await openPanel(page, panel);
    const dialog = page.locator('.primary-panel');
    await soleHighlight(dialog, 'Close panel');
    await expect(dialog.locator('h2')).toBeFocused();
    await page.keyboard.press('Enter');
    await expect(dialog).toHaveCount(0);
  }
});

test('Return applies station edits and dirty decisions while composition and held keys cannot submit', async ({ page }) => {
  await page.goto('/'); await ready(page); await selectStation(page);
  const input = page.getByLabel('Station name', { exact: true });
  const confirmed = page.getByTestId('confirmed-name');
  const before = await confirmed.textContent();
  await soleHighlight(page.locator('.primary-panel'), 'Apply name');
  await input.fill('Return station');
  for (const init of [{ isComposing: true }, { keyCode: 229 }, { repeat: true }]) {
    await input.dispatchEvent('keydown', { key: 'Enter', ...init });
    await expect(confirmed).toHaveText(before!);
  }
  await input.dispatchEvent('compositionstart');
  await input.press('Enter');
  await expect(confirmed).toHaveText(before!);
  await input.dispatchEvent('compositionend');
  await input.press('Enter');
  await expect(confirmed).toHaveText('Return station');

  await input.fill('Return decision');
  await page.getByRole('button', { name: 'Close panel', exact: true }).click();
  const decision = page.getByRole('dialog', { name: 'Unsaved station name', exact: true });
  await soleHighlight(decision, 'Apply name');
  await page.keyboard.press('Enter');
  await expect(decision).toHaveCount(0);
  await expect(page.locator('.primary-panel')).toHaveCount(0);
  await selectStation(page);
  await expect(confirmed).toHaveText('Return decision');
});

test('disabled defaults do not fall through and focused secondary controls keep native activation', async ({ page }) => {
  await page.goto('/'); await ready(page); await selectStation(page);
  const input = page.getByLabel('Station name', { exact: true });
  const before = await page.getByTestId('confirmed-name').textContent();
  await input.fill('   ');
  await page.getByRole('button', { name: 'Close panel', exact: true }).click();
  const decision = page.locator('.decision-dialog');
  await expect(decision.locator('button.primary')).toBeDisabled();
  await page.keyboard.press('Enter');
  await expect(decision).toBeVisible();
  await expect(input).toHaveValue('   ');
  await decision.getByRole('button', { name: 'Keep editing', exact: true }).focus();
  await page.keyboard.press('Enter');
  await expect(decision).toHaveCount(0);
  await expect(input).toHaveValue('   ');
  await page.getByRole('button', { name: 'Reset draft', exact: true }).focus();
  await page.keyboard.press('Enter');
  await expect(input).toHaveValue(before!);
  await openPanel(page, 'settings');
  const scheme = page.getByLabel('UI color scheme', { exact: true });
  await scheme.selectOption('plum'); await scheme.focus();
  await page.keyboard.press('Enter');
  await expect(scheme).toHaveValue('plum');
  await expect(page.locator('.primary-panel')).toBeVisible();
});

test('sign-in Return validates the form and nested credits never trigger the underlying default', async ({ page }) => {
  let attempts = 0;
  await page.route('**/api/bootstrap?*', route => route.fulfill({ status: 401, json: { anonymousAccess: false } }));
  await page.route('**/api/access', route => { attempts++; return route.fulfill({ status: 401 }); });
  await page.goto('/');
  const gate = page.getByRole('dialog', { name: appName, exact: true });
  const input = page.getByLabel('Enter access key to join game', { exact: true });
  await expect(input).toBeVisible();
  await soleHighlight(gate, 'Connect');
  await page.keyboard.press('Enter');
  await expect(input).toBeFocused();
  expect(attempts).toBe(0);
  await gate.getByRole('button', { name: 'Credits', exact: true }).click();
  const credits = page.getByRole('dialog', { name: 'Credits', exact: true });
  await expect(credits).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(credits).toHaveCount(0);
  expect(attempts).toBe(0);
  await input.fill('invalid-test-key');
  await gate.focus();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('alert')).toHaveText('Access failed. Check the key and try again.');
  expect(attempts).toBe(1);
});

test('Return on a file input opens its picker without activating the dialog default', async ({ page }) => {
  await page.goto('/'); await ready(page); await openPanel(page, 'saves');
  const upload = page.getByLabel('Upload save', { exact: true });
  await expect(upload).toBeEnabled();
  const choosing = page.waitForEvent('filechooser');
  await upload.press('Enter');
  await (await choosing).setFiles([]);
  await expect(page.getByTestId('save-feedback')).toHaveText('');
  await expect(page.locator('.primary-panel')).toBeVisible();
});
