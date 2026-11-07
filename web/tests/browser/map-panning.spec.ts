import { at,clickTile,ready } from './browser-helpers.ts';
import { expect,test } from './fixtures.ts';

test('owner and observer inspect drags survive running world updates', async ({ page, browser }) => {
  await page.goto('/'); await ready(page);
  await page.getByRole('button', { name: 'Run', exact: false }).click();
  const observer = await browser.newContext();
  try {
    const watcher = await observer.newPage();
    await watcher.request.post('/api/access', { data: { secret: 'observer-test' } });
    await watcher.goto('/'); await ready(watcher);
    await expect(watcher.getByTestId('participant-role')).toHaveText('Observer');
    for (const [role, target] of [['owner', page], ['observer', watcher]] as const) {
      const canvas = target.locator('canvas'), box = (await canvas.boundingBox())!;
      for (const button of ['left', 'middle'] as const) {
        await test.step(`${role}: ${button} drag`, async () => {
          const initial = await canvas.getAttribute('data-camera');
          await target.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
          await target.mouse.down({ button });
          await target.mouse.move(box.x + box.width / 2 + 30, box.y + box.height / 2 + 10);
          await expect(canvas).not.toHaveAttribute('data-camera', initial!);
          // Keep the button held across at least one authoritative update.
          const tick = await target.getByTestId('tick').textContent();
          await expect(target.getByTestId('tick')).not.toHaveText(tick!);
          const before = await canvas.getAttribute('data-camera');
          await target.mouse.move(box.x + box.width / 2 + 90, box.y + box.height / 2 + 30);
          await target.mouse.up({ button });
          await expect.poll(() => canvas.getAttribute('data-camera')).not.toBe(before);
          await expect(target.getByRole('button', { name: 'Inspect', exact: true })).toHaveAttribute('aria-pressed', 'true');
        });
      }
    }
  } finally {
    await page.getByRole('button', { name: 'Pause', exact: false }).click();
    await expect(page.getByTestId('clock-state')).toHaveText('Paused');
    await observer.close();
  }
});

test('trackpad pans, wheel and keyboard zoom, and tool shortcuts select without toggling', async ({ page }) => {
  await page.goto('/'); await ready(page);
  const canvas = page.locator('canvas');
  await canvas.focus();
  const initialCamera = await canvas.getAttribute('data-camera');
  const initialZoom = await canvas.getAttribute('data-zoom');
  await canvas.dispatchEvent('wheel', { deltaX: 24, deltaY: 12, deltaMode: 0 });
  await expect(canvas).not.toHaveAttribute('data-camera', initialCamera!);
  await expect(canvas).toHaveAttribute('data-zoom', initialZoom!);
  await canvas.dispatchEvent('wheel', { deltaY: -3, deltaMode: 1 });
  await expect.poll(async () => Number(await canvas.getAttribute('data-zoom'))).toBeGreaterThan(Number(initialZoom));
  await canvas.dispatchEvent('wheel', { deltaY: 3, deltaMode: 1 });
  await expect.poll(async () => Number(await canvas.getAttribute('data-zoom'))).toBeCloseTo(Number(initialZoom));
  await page.keyboard.press('+');
  await expect.poll(async () => Number(await canvas.getAttribute('data-zoom'))).toBeCloseTo(Number(initialZoom) + .25);
  await page.keyboard.press('=');
  await expect.poll(async () => Number(await canvas.getAttribute('data-zoom'))).toBeCloseTo(Number(initialZoom) + .5);
  await canvas.dispatchEvent('keydown', { key: '+', repeat: true });
  await page.keyboard.press('-');
  await expect.poll(async () => Number(await canvas.getAttribute('data-zoom'))).toBeCloseTo(Number(initialZoom) + .25);
  await page.keyboard.press('-');
  await expect.poll(async () => Number(await canvas.getAttribute('data-zoom'))).toBeCloseTo(Number(initialZoom));
  for (let i = 0; i < 20; i++) await page.keyboard.press('+');
  await expect.poll(async () => Number(await canvas.getAttribute('data-zoom'))).toBe(4);
  for (let i = 0; i < 20; i++) await page.keyboard.press('-');
  await expect.poll(async () => Number(await canvas.getAttribute('data-zoom'))).toBe(.5);
  for (const [key, label] of [['t', 'Build track'], ['t', 'Build track'], ['r', 'Remove'], ['i', 'Inspect']]) {
    await page.keyboard.press(key);
    await expect(page.getByRole('button', { name: label, exact: true })).toHaveAttribute('aria-pressed', 'true');
  }
  await canvas.dispatchEvent('keydown', { key: 't', ctrlKey: true });
  await expect(page.getByRole('button', { name: 'Inspect', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await page.getByRole('button', { name: 'Game ▾', exact: true }).click();
  await page.getByRole('button', { name: 'Help', exact: true }).click();
  const help = page.locator('.primary-panel');
  await expect(help).toContainText('two-finger');
  await expect(help).toContainText('I — Inspect');
  await expect(help).not.toContainText('Diagnostics');
  await expect(help).not.toContainText('Network');
});

test('middle and Space pan never pick or target, including Space released before pointer-up', async ({ page }) => {
  await page.goto('/'); await ready(page);
  if (await page.getByTestId('clock-state').textContent() === 'Running') await page.getByRole('button', { name: 'Pause', exact: false }).click();
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
  await page.keyboard.press('Escape'); await expect(page.getByRole('button', { name: 'Inspect', exact: true })).toHaveAttribute('aria-pressed', 'true');
});
