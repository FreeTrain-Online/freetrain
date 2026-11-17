import { assertExact } from './assertions.ts';
import { expect,test } from './fixtures.ts';
import { openPanel, ready } from './browser-helpers.ts';

test('fresh service renders generated hills and trees and reopens the saved terrain',async({page})=>{
  const errors:string[]=[];page.on('pageerror',error=>errors.push(String(error)));
  await page.goto('/');await ready(page);
  const canvas=page.locator('canvas');
  await expect(canvas).toHaveAttribute('data-ruleset','terrain-state-1');
  await expect(canvas).toHaveAttribute('data-assets','ready');
  await expect(page.getByTestId('reference-world')).toHaveText('Terrain world');
  const bytes=await(await page.request.get('/api/saves/download')).body(),save=JSON.parse(bytes.toString());
  expect(save.map.width).toBe(400);expect(save.map.height).toBe(600);expect(save.rails).toEqual([]);
  const request=JSON.parse(save.packedTerrain.provenance.requestJson);
  expect(request.source).toBe('generated');expect(request.Options.Relief.Kind).toBe(1);
  expect(new Set([...Buffer.from(save.packedTerrain.cells,'base64')].map(n=>n&63)).size).toBeGreaterThan(2);
  await page.reload();await ready(page);
  await assertExact(await(await page.request.get('/api/saves/download')).body(),bytes,"await(await page.request.get('/api/saves/download')).body()");
  expect(errors).toEqual([]);
});

test('fresh paused world can quote and purchase the initially selected train',async({page})=>{
  await page.goto('/');await ready(page);
  await openPanel(page,'fleet');
  const response=page.waitForResponse(r=>r.url().includes('/api/fleet/preview'));
  await page.getByRole('button',{name:'Get purchase quote',exact:true}).click();
  const quote=await response;
  expect(quote.ok(),await quote.text()).toBe(true);
  const dialog=page.getByRole('dialog',{name:'Purchase quote',exact:true});
  await expect(dialog).toBeVisible();
  await dialog.getByRole('button',{name:'Confirm purchase',exact:true}).click();
  await expect(dialog).toHaveCount(0);
  await page.getByRole('button',{name:'Owned stock',exact:false}).click();
  await expect(page.locator('.fleet-owned .catalog-item-main')).toHaveCount(1);
});
