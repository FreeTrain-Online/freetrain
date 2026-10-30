import { expect, test } from '@playwright/test';
import { ready } from './ux-helpers.ts';

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
          await expect(target.getByRole('button', { name: 'Inspect / drive', exact: true })).toHaveAttribute('aria-pressed', 'true');
        });
      }
    }
  } finally {
    await page.getByRole('button', { name: 'Pause', exact: false }).click();
    await observer.close();
  }
});
