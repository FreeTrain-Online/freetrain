import { expect, test } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { ready, bootstrap, clickTile, worldReplacementFixture } from './ux-helpers.ts';

test('aligned targets use server-checked geometry without spending or changing the shared world',async({page})=>{
  await page.goto('/');await ready(page);
  if(!(await bootstrap(page)).paused)await page.getByRole('button',{name:'Pause',exact:false}).click();
  const before=await bootstrap(page),canvas=page.locator('canvas');
  await canvas.evaluate(el=>el.setAttribute('data-instance','spatial-preserved'));
  await expect(canvas).toHaveAttribute('data-coordinate-system','prototypeXY');
  await expect(canvas).toHaveAttribute('data-headings',before.trains.flatMap((train:any)=>train.carHeadings).join(','));
  await page.getByRole('button',{name:'Build track',exact:true}).click();
  await clickTile(page,{x:22,y:16,z:1});
  await expect(page.getByTestId('geometry-status')).toContainText('Target is within the map.');
  await page.getByRole('button',{name:'Use as alignment anchor',exact:true}).click();
  await page.getByLabel('Align target',{exact:true}).selectOption('8');
  await clickTile(page,{x:24,y:21,z:1});
  await expect(page.getByTestId('preview-position')).toHaveText('24, 18, 1');
  await expect(page.getByTestId('preview-heading')).toHaveText('Direction: South-east · Distance in cells: 2');
  await expect(page.getByRole('button',{name:'Confirm track change',exact:true})).toBeEnabled();
  await page.getByRole('button',{name:'Zoom in',exact:true}).click();
  await expect(page.getByTestId('preview-position')).toHaveText('24, 18, 1');
  await page.getByLabel('Align target',{exact:true}).selectOption('4');
  await expect(page.getByTestId('preview-position')).toHaveText('22, 21, 1');
  await expect(page.getByTestId('preview-heading')).toHaveText('Direction: South · Distance in cells: 5');
  await expect(canvas).toHaveAttribute('data-instance','spatial-preserved');
  const evidence=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'spatial');mkdirSync(evidence,{recursive:true});
  await page.screenshot({path:join(evidence,'alignment-en.png'),fullPage:true});
  expect(await bootstrap(page)).toEqual(before);
  await page.getByRole('button',{name:'Cancel preview',exact:true}).click();
  await expect(page.getByTestId('snap-anchor')).toHaveCount(0);
  await expect(page.getByRole('button',{name:'Confirm track change',exact:true})).toBeDisabled();
});

test('late target checks cannot re-enable confirmation after cancel or replace a newer target',async({page})=>{
  let release:()=>void=()=>{},waiting=false,first=true;
  const held=new Promise<void>(resolve=>{release=resolve;});
  await page.route('**/api/spatial/preview?*',async route=>{
    if(!first){await route.continue();return;}first=false;
    try {const response=await route.fetch();waiting=true;await held;await route.fulfill({response});}catch{/* The old request is deliberately aborted. */}
  });
  try {
    await page.goto('/');await ready(page);await page.getByRole('button',{name:'Build track',exact:true}).click();
    await clickTile(page,{x:22,y:16,z:1});await expect.poll(()=>waiting).toBe(true);
    await expect(page.getByRole('button',{name:'Confirm track change',exact:true})).toBeDisabled();
    await page.getByRole('button',{name:'Cancel preview',exact:true}).click();
    await clickTile(page,{x:24,y:18,z:1});await expect(page.getByTestId('geometry-status')).toContainText('Target is within the map.');
    release();await expect(page.getByTestId('preview-position')).toHaveText('24, 18, 1');
    await expect(page.getByRole('button',{name:'Confirm track change',exact:true})).toBeEnabled();
  } finally {release();}
});

test('client/server geometry disagreement blocks confirmation and preserves the world',async({page})=>{
  await page.route('**/api/spatial/preview?*',async route=>{
    const response=await route.fetch(),body=await response.json();body.projected.a++;
    await route.fulfill({response,json:body});
  });
  await page.goto('/');await ready(page);const before=await bootstrap(page);
  await page.getByRole('button',{name:'Build track',exact:true}).click();await clickTile(page,{x:22,y:16,z:1});
  await expect(page.getByTestId('geometry-status')).toContainText('could not be checked');
  await expect(page.getByRole('button',{name:'Confirm track change',exact:true})).toBeDisabled();
  expect(await bootstrap(page)).toEqual(before);
});

test('an unrecognized required pack blocks reveal at the compatibility gate',async({page})=>{
  await page.route('**/api/bootstrap?*',async route=>{
    const response=await route.fetch(),body=await response.json();
    body.requiredPacks[0].catalogHash='0'.repeat(64);
    body.snapshot.requiredPacks[0].catalogHash='0'.repeat(64);
    await route.fulfill({response,json:body});
  });
  await page.goto('/');await expect(page.getByRole('heading',{name:'Update required',exact:true})).toBeVisible();
  await expect(page.locator('canvas')).toHaveAttribute('data-scene','demo');
  await expect(page.locator('.map-viewport')).toHaveAttribute('inert','');
});

test('epoch snapshot fixture clears an in-flight geometry request and its alignment state',async({page})=>{
  const replaceWorld=await worldReplacementFixture(page);
  let release:()=>void=()=>{},waiting=false;
  const held=new Promise<void>(resolve=>{release=resolve;});
  await page.route('**/api/spatial/preview?*',async route=>{
    try{const response=await route.fetch();waiting=true;await held;await route.fulfill({response});}catch{/* Replacement aborts the request. */}
  });
  try {
    await page.goto('/');await ready(page);
    const before=await bootstrap(page);
    await page.getByRole('button',{name:'Build track',exact:true}).click();await clickTile(page,{x:22,y:16,z:1});
    await expect.poll(()=>waiting).toBe(true);
    const next=await replaceWorld();
    await expect(page.getByRole('button',{name:'Build track',exact:true})).toHaveAttribute('aria-pressed','false');
    release();await expect(page.getByTestId('preview-position')).toHaveCount(0);
    await expect(page.locator('canvas')).toHaveAttribute('data-world-epoch',next.worldEpoch);
    expect(BigInt(next.worldEpoch)).toBe(BigInt(before.worldEpoch)+1n);
  } finally {release();}
});

test('Japanese alignment labels and source direction names fit a narrow high-DPI map',async({browser})=>{
  const context=await browser.newContext({viewport:{width:640,height:1000},deviceScaleFactor:2,locale:'ja-JP'});
  try {
    const page=await context.newPage();await page.goto('/');await ready(page,'ja');
    await page.getByRole('button',{name:'線路を敷設',exact:true}).click();
    await clickTile(page,{x:22,y:16,z:1});await expect(page.getByTestId('geometry-status')).toContainText('対象はマップの範囲内です。');
    await page.getByRole('button',{name:'方向合わせの基準にする',exact:true}).click();await page.getByLabel('対象の方向合わせ',{exact:true}).selectOption('8');
    await clickTile(page,{x:24,y:21,z:1});await expect(page.getByTestId('preview-heading')).toHaveText('方向: 南東 · 距離（マス）: 2');
    expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
    const evidence=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'spatial');mkdirSync(evidence,{recursive:true});
    await page.screenshot({path:join(evidence,'alignment-ja-2x.png'),fullPage:true});
  } finally {await context.close();}
});
