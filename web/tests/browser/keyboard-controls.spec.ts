import { expect,test } from './fixtures.ts';
import { clickSprite,clickTile,closePanel,openPanel,ready,selectStation } from './browser-helpers.ts';

test('panel/menu gestures isolate the world, return focus and invalidate geometry', async ({ page }) => {
  await page.goto('/'); await ready(page);
  if (await page.getByTestId('clock-state').textContent() === 'Running') await page.getByRole('button', { name: 'Pause', exact: false }).click();
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

test('keyboard toolbars, native editing, Escape layers and deliberate map focus', async ({ page }) => {
  await page.goto('/'); await ready(page);
  const game = page.getByRole('button',{name:'Game ▾',exact:true}); await game.focus();
  await page.keyboard.press('End'); await expect(page.getByRole('button',{name:/Session ·/})).toBeFocused();
  await page.keyboard.press('Home'); await expect(game).toBeFocused();
  await page.keyboard.press('Enter'); await page.keyboard.press('End');
  await expect(page.locator('#game-menu button').last()).toBeFocused();
  await page.keyboard.press('Escape'); await expect(game).toBeFocused();
  const inspect = page.getByRole('button',{name:'Inspect / drive',exact:true}); await inspect.focus();
  await page.keyboard.press('ArrowDown'); await expect(page.getByRole('button',{name:'Trains',exact:true})).toBeFocused();
  await page.keyboard.press('ArrowDown'); await expect(page.getByRole('button',{name:'Build track',exact:true})).toBeFocused();
  await page.keyboard.press('Enter'); await expect(page.locator('canvas')).toBeFocused();
  await expect(page.getByTestId('map-selection')).toContainText('Build track');
  await page.keyboard.press('Escape'); await expect(inspect).toHaveAttribute('aria-pressed','true');
  await selectStation(page); const input = page.getByLabel('Station name',{exact:true}); await input.fill('Editing');
  await input.press('Home'); await input.press('Shift+End');
  expect(await input.evaluate(el => (el as HTMLInputElement).selectionEnd)).toBe(7);
  await input.press('Escape'); await expect(page.locator('.decision-dialog')).toHaveCount(0);
  await page.getByRole('button',{name:'Close panel',exact:true}).focus(); await page.keyboard.press('Escape');
  await expect(page.locator('.decision-dialog')).toBeVisible(); await page.keyboard.press('Escape');
  await expect(input).toHaveValue('Editing'); await expect(page.locator('.primary-panel')).toBeVisible();
  await page.getByRole('button',{name:'Reset draft',exact:true}).click();
  await openPanel(page,'settings');
  const scheme = page.getByLabel('UI color scheme',{exact:true}); await scheme.focus();
  await page.keyboard.press('p'); await page.keyboard.press('Enter'); await expect(scheme).toHaveValue('plum');
});

test('success expires, actionable rejection remains and tool Escape cancels before exit', async ({ page }) => {
  await page.clock.install();
  await page.goto('/'); await ready(page);
  if(await page.getByTestId('clock-state').textContent()==='Running') await page.getByRole('button',{name:'Pause',exact:false}).click();
  await page.getByRole('button',{name:'Single step',exact:false}).click(); await expect(page.getByRole('status')).toHaveText('Confirmed by server');
  await page.clock.fastForward(4100);await expect(page.locator('.command-feedback')).toHaveText('');
  await page.getByRole('button',{name:'Build track',exact:true}).click();
  await clickTile(page,{x:23,y:16,z:1});
  await page.getByRole('button',{name:'Confirm track change',exact:true}).click(); await expect(page.getByRole('status')).toHaveText('Track cannot be placed here.');
  await page.clock.fastForward(4200); await expect(page.getByRole('status')).toHaveText('Track cannot be placed here.');
  await expect(page.locator('.tool-card')).toContainText('Track cannot be placed here.');
  await page.locator('canvas').focus(); await page.keyboard.press('Escape'); await expect(page.getByRole('button',{name:'Build track',exact:true})).toHaveAttribute('aria-pressed','true');
  await page.keyboard.press('Escape'); await expect(page.getByRole('button',{name:'Inspect / drive',exact:true})).toHaveAttribute('aria-pressed','true');
});
