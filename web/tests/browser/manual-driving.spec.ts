import { expect,test } from './fixtures.ts';
import { closePanel,openPanel,ready,selectTrain } from './browser-helpers.ts';

test('an open menu suspends driving even if keyboard focus returns to the canvas', async ({ page }) => {
  await page.goto('/'); await ready(page);
  if (await page.getByTestId('clock-state').textContent() === 'Running') await page.getByRole('button', { name: 'Pause', exact: false }).click();
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

test('manual keys reset on blur, visibility, tool and lease loss without automatic resume', async ({ page }) => {
  await page.goto('/'); await ready(page); await selectTrain(page);
  await page.getByRole('combobox',{name:'Controller',exact:true}).selectOption('manual'); await expect(page.getByRole('status')).toHaveText('Confirmed by server');
  const canvas=page.locator('canvas');
  for(const reason of ['blur','hidden','tool','panel']) {
    await canvas.focus(); await page.keyboard.down('ArrowRight'); await expect(page.getByTestId('manual-input')).toContainText('ArrowRight');
    if(reason==='blur') await page.evaluate(()=>window.dispatchEvent(new Event('blur')));
    if(reason==='hidden') await page.evaluate(()=>document.dispatchEvent(new Event('visibilitychange')));
    if(reason==='tool') await page.getByRole('button',{name:'Build track',exact:true}).click();
    if(reason==='panel') await openPanel(page,'settings');
    await expect(page.getByTestId('manual-input')).toHaveText('Keys released'); await page.keyboard.up('ArrowRight');
    if(reason==='tool') await page.getByRole('button',{name:'Inspect / drive',exact:true}).click();
    if(reason==='panel') await closePanel(page);
    await canvas.focus(); await page.keyboard.press('Control+ArrowRight'); await expect(page.getByTestId('manual-input')).toHaveText('Keys released');
  }
  await selectTrain(page); await page.getByRole('button',{name:'Release control',exact:true}).click();
  await canvas.focus(); await page.keyboard.press('ArrowRight'); await expect(page.getByTestId('manual-input')).toHaveText('Keys released');
  await page.getByRole('combobox',{name:'Controller',exact:true}).selectOption('automatic'); await expect(page.getByRole('status')).toHaveText('Confirmed by server');
  await page.getByRole('button',{name:'Release control',exact:true}).click();
});
