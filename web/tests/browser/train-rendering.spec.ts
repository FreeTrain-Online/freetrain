import {execFileSync} from 'node:child_process';
import {readFileSync,writeFileSync} from 'node:fs';
import {test,expect} from './fixtures.ts';
import {ready} from './browser-helpers.ts';
test('all bundled moving-car poses paint exact independent crop/anchor pixels and alpha picks in every season and light',async({page})=>{
 test.setTimeout(120_000);
 const harness=execFileSync('deno',['run','-A','scripts/bundle-rendering-tests.ts'],{encoding:'utf8',maxBuffer:16*1024*1024});
 await page.route('**/__tests/train-rendering.js',route=>route.fulfill({contentType:'text/javascript',body:harness}));
 await page.goto('/');await ready(page);
 const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
 const report=[];
 for(const season of ['spring','summer','autumn','winter'])for(const night of [false,true]){
  const result=await page.evaluate(async({catalog,season,night})=>{
   const {poseGallery}=await import('/__tests/train-rendering.js');return poseGallery(catalog,{season,night});
  },{catalog,season,night});
  expect(result.recipes).toBe(8);expect(result.tiles).toBe(896);expect(result.pixels).toBe(896*48*48);expect(result.picks).toBe(result.pixels);
  writeFileSync(`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/train-poses-${season}-${night?'night':'day'}.png`,Buffer.from(result.png.split(',')[1],'base64'));
  const infrastructure=await page.evaluate(async({catalog,season,night})=>{
   const {infrastructureGallery}=await import('/__tests/train-rendering.js');return infrastructureGallery(catalog,{season,night});
  },{catalog,season,night});
  expect(infrastructure.tiles).toBeGreaterThan(100);expect(infrastructure.poles).toBeGreaterThan(0);
  writeFileSync(`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/train-infrastructure-${season}-${night?'night':'day'}.png`,Buffer.from(infrastructure.png.split(',')[1],'base64'));
  const tunnel=await page.evaluate(async({catalog,season,night})=>{
   const {tunnelMovementPixels}=await import('/__tests/train-rendering.js');return tunnelMovementPixels(catalog,{season,night});
  },{catalog,season,night});
  expect(tunnel.picks).toBe(240*180);
  writeFileSync(`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/train-tunnel-${season}-${night?'night':'day'}.png`,Buffer.from(tunnel.png.split(',')[1],'base64'));
  report.push({season,night,recipes:result.recipes,poses:result.tiles,pixels:result.pixels,picks:result.picks,infrastructure:infrastructure.tiles,poles:infrastructure.poles,tunnelPicks:tunnel.picks});
 }
 const local=await page.evaluate(async catalog=>{const {localMovementPixels}=await import('/__tests/train-rendering.js');return localMovementPixels(catalog);},catalog);
 expect(local.picks).toBe(24000);expect(local.columns).toHaveLength(4);
 writeFileSync(`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/train-local-redraw.png`,Buffer.from(local.png.split(',')[1],'base64'));
 writeFileSync(`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/train-rendering-report.json`,JSON.stringify({variants:report,local:{picks:local.picks,columns:local.columns}},null,2));
});
