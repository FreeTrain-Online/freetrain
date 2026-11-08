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

test('fractional train damage replay and alpha picks match full source-order composition, with hidden tunnel interiors',async({page},testInfo)=>{
 const harness=execFileSync('deno',['run','-A','scripts/bundle-rendering-tests.ts'],{encoding:'utf8',maxBuffer:16*1024*1024});
 await page.route('**/__tests/train-rendering.js',route=>route.fulfill({contentType:'text/javascript',body:harness}));
 await page.goto('/');await ready(page);
 const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
 for(const night of [false,true]) {
  const report=await page.evaluate(async({catalog,night})=>{const {smoothMovementPixels}=await import('/__tests/train-rendering.js');return smoothMovementPixels(catalog,{season:'spring',night});},{catalog,night});
  expect(report.frames).toBe(15);expect(report.picks).toBe(144000);expect(report.hidden).toBe(5);
  const png=Buffer.from(report.png.split(',')[1],'base64');
  writeFileSync(`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/smooth-composition-${night?'night':'day'}.png`,png);
  await testInfo.attach(`smooth-composition-${night?'night':'day'}.png`,{body:png,contentType:'image/png'});
 }
});

test('tunnel trains cross both portals smoothly with hidden interiors and matching alpha picks',async({page},testInfo)=>{
 test.setTimeout(120_000);
 const harness=execFileSync('deno',['run','-A','scripts/bundle-rendering-tests.ts'],{encoding:'utf8',maxBuffer:16*1024*1024});
 await page.route('**/__tests/train-rendering.js',route=>route.fulfill({contentType:'text/javascript',body:harness}));
 await page.goto('/');await ready(page);
 const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
 for(const night of [false,true]) {
  const report=await page.evaluate(async({catalog,night})=>{const {trainTunnelPixels}=await import('/__tests/train-rendering.js');return trainTunnelPixels(catalog,{season:'spring',night});},{catalog,night});
  expect(report.frames).toBe(504);expect(report.picks).toBe(970200);
  expect(report.visible).toBeGreaterThan(0);expect(report.hidden).toBeGreaterThan(0);
  const png=Buffer.from(report.png.split(',')[1],'base64');writeFileSync(`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/train-tunnel-smooth-${night?'night':'day'}.png`,png);
  await testInfo.attach(`train-tunnel-smooth-${night?'night':'day'}.png`,{body:png,contentType:'image/png'});
 }
});

test('tunnel damage replay has bounded paint work per frame @performance',async({page},testInfo)=>{
 const harness=execFileSync('deno',['run','-A','scripts/bundle-rendering-tests.ts'],{encoding:'utf8',maxBuffer:16*1024*1024});
 await page.route('**/__tests/train-rendering.js',route=>route.fulfill({contentType:'text/javascript',body:harness}));
 await page.goto('/');await ready(page);
 const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
 const report=await page.evaluate(async catalog=>{
  const {trainTunnelPixels}=await import('/__tests/train-rendering.js');
  const result=await trainTunnelPixels(catalog,{season:'spring',night:false},true);
  const sorted=result.frameTimes.sort((a:number,b:number)=>a-b);
  return {frames:result.frames,paintCalls:result.paintCalls,p95:sorted[Math.floor(sorted.length*.95)],max:sorted.at(-1)};
 },catalog);
 console.log('tunnel redraw',report);
 await testInfo.attach('tunnel-redraw.json',{body:JSON.stringify(report,null,2),contentType:'application/json'});
 expect(report.paintCalls).toBeLessThan(report.frames*8);
 expect(report.p95).toBeLessThan(16);
});

test('whole head and tail car sprites stay above neighboring track pixels in every direction',async({page},testInfo)=>{
 test.setTimeout(90_000);
 const harness=execFileSync('deno',['run','-A','scripts/bundle-rendering-tests.ts'],{encoding:'utf8',maxBuffer:16*1024*1024});
 await page.route('**/__tests/train-rendering.js',route=>route.fulfill({contentType:'text/javascript',body:harness}));
 await page.goto('/');await ready(page);
 const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
 for(const night of [false,true]) {
  const report=await page.evaluate(async({catalog,night})=>{const {trainTrackOverlayPixels}=await import('/__tests/train-rendering.js');return trainTrackOverlayPixels(catalog,{season:'spring',night});},{catalog,night});
  expect(report.frames).toBe(80);expect(report.carPixels).toBeGreaterThan(10_000);
  const png=Buffer.from(report.png.split(',')[1],'base64');writeFileSync(`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/train-track-overlay-${night?'night':'day'}.png`,png);
  await testInfo.attach(`train-track-overlay-${night?'night':'day'}.png`,{body:png,contentType:'image/png'});
 }
});

test('neighboring car sprites keep continuous depth through tight ninety-degree turns',async({page},testInfo)=>{
 test.setTimeout(120_000);
 const harness=execFileSync('deno',['run','-A','scripts/bundle-rendering-tests.ts'],{encoding:'utf8',maxBuffer:16*1024*1024});
 await page.route('**/__tests/train-rendering.js',route=>route.fulfill({contentType:'text/javascript',body:harness}));
 await page.goto('/');await ready(page);
 const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
 for(const night of [false,true]) {
  const report=await page.evaluate(async({catalog,night})=>{const {trainTurnBlendPixels}=await import('/__tests/train-rendering.js');return trainTurnBlendPixels(catalog,{season:'spring',night});},{catalog,night});
  expect(report.frames).toBe(2816);expect(report.overlapPicks).toBeGreaterThan(1000);
  const png=Buffer.from(report.png.split(',')[1],'base64');writeFileSync(`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/train-turn-blending-${night?'night':'day'}.png`,png);
  await testInfo.attach(`train-turn-blending-${night?'night':'day'}.png`,{body:png,contentType:'image/png'});
 }
});

test('slope trains cross elevations and flat joins with whole sprites and matching alpha picks',async({page},testInfo)=>{
 test.setTimeout(120_000);
 const harness=execFileSync('deno',['run','-A','scripts/bundle-rendering-tests.ts'],{encoding:'utf8',maxBuffer:16*1024*1024});
 await page.route('**/__tests/train-rendering.js',route=>route.fulfill({contentType:'text/javascript',body:harness}));
 await page.goto('/');await ready(page);
 const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
 for(const night of [false,true]) {
  const report=await page.evaluate(async({catalog,night})=>{const {trainSlopePixels}=await import('/__tests/train-rendering.js');return trainSlopePixels(catalog,{season:'spring',night});},{catalog,night});
  expect(report.frames).toBe(784);expect(report.carPixels).toBeGreaterThan(10000);
  const png=Buffer.from(report.png.split(',')[1],'base64');writeFileSync(`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/train-slopes-${night?'night':'day'}.png`,png);
  await testInfo.attach(`train-slopes-${night?'night':'day'}.png`,{body:png,contentType:'image/png'});
 }
});
