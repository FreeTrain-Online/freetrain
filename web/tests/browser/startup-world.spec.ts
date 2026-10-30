import {expect,test} from '@playwright/test';
import {ready} from './ux-helpers.ts';

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
  expect(await(await page.request.get('/api/saves/download')).body()).toEqual(bytes);
  expect(errors).toEqual([]);
});
