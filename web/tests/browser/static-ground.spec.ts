import { restartWorldServer } from './session.ts';
import { assertExact } from './assertions.ts';
import {readFileSync} from 'node:fs';
import {test,expect,type Page} from './fixtures.ts';
import {browserFixture} from './generated-fixtures.ts';
import {canonicalState as state,ready,closePanel,exposeMapPoint} from './browser-helpers.ts';
const texts=['en','ja'].map(l=>JSON.parse(readFileSync(`src/locales/${l}/app.json`,'utf8')));
const bytes=async(p:Page)=>(await p.request.get('/api/saves/download')).body();
async function point(p:Page,x:number,y:number){return p.locator('canvas.world-canvas').evaluate((el,w)=>{const r=el.getBoundingClientRect(),[x,y]=el.dataset.camera!.split(',').map(Number),z=Number(el.dataset.zoom);return {x:r.x+r.width/2+(w.x-x)*z,y:r.y+r.height/2+(w.y-y)*z};},{x:16*(x-15+y)+16,y:8*(y-(x-15))-32+8});}
async function quote(p:Page,t:any){
 for(const [label,value] of [[t.landAnchorX,14],[t.landAnchorY,12],[t.landEndX,15],[t.landEndY,13]] as const)await p.getByLabel(label,{exact:true}).fill(String(value));
 await p.locator('.land-task form').getByRole('button',{name:t.groundCheck,exact:true}).click();
}
test.use({hasTouch:true});
for(const ja of [false,true])test(`P7-16 static farmland ${ja?'JA phone touch':'EN keyboard'} authored catalog real providers peer removal and exact restart`,async({page,browser,baseURL,worldServer})=>{
 test.setTimeout(120000);const t=texts[ja?1:0],locale=ja?'ja':'en';if(ja)await page.setViewportSize({width:430,height:860});
 await page.addInitScript(ja=>localStorage.setItem('freetrain-online.locale',ja?'ja':'en'),ja);
 const context=await browser.newContext({baseURL}),peer=await context.newPage();const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 try{
  await page.goto('/');await ready(page,locale);const initial=await state(page);
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+initial.worldEpoch,{data:readFileSync(browserFixture('staticGround'))})).ok()).toBe(true);await ready(page,locale);
  await peer.goto('/');await ready(peer);await page.getByRole('button',{name:t.landTask,exact:true}).click();
  await page.getByLabel(t.landAction,{exact:true}).selectOption('cover');const catalog=page.getByLabel(t.groundType,{exact:true});expect(await catalog.locator('option').count()).toBe(45);
  // The final-row portable crop must be usable on the map and in the catalog.
  await catalog.selectOption('{168C94A9-1D5B-4160-BBD8-21C830DCB752}-3,4');await expect(page.locator('.land-task .catalog-preview img')).toBeVisible();
  const original=await bytes(page);await quote(page,t);const confirm=page.getByRole('button',{name:t.groundConfirm,exact:true});await expect(confirm).toBeEnabled();
  await expect(page.getByTestId('land-counts')).toContainText(`${t.landSelected}: 4`);await expect(page.getByTestId('land-amount')).toContainText(ja?'1,200,000':'1,200,000');await assertExact(await bytes(page),original,"await bytes(page)");
  await page.getByRole('button',{name:t.cancelPreview,exact:true}).click();await expect(confirm).toBeDisabled();await assertExact(await bytes(page),original,"await bytes(page)");
  // Ground/property alpha picks participate in the same two-corner workflow.
  for(const [x,y] of [[14,12],[15,13]]){const at=await exposeMapPoint(page,await point(page,x,y));if(ja)await page.touchscreen.tap(at.x,at.y);else await page.mouse.click(at.x,at.y);}
  await expect(confirm).toBeEnabled();await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/ground-${locale}-review.png`});
  await page.locator('canvas').focus();await page.keyboard.press('Enter');
  await expect.poll(async()=>(await state(peer)).mixed.city.ground.length).toBe(4);let after=await state(page);
  expect(after.mixed.city.properties).toHaveLength(0);expect(after.mixed.city.producers).toHaveLength(4);expect(after.mixed.stationGraph.listeners).toHaveLength(4);
  expect(after.mixed.stationGraph.listenerMemberships[0].providers).toHaveLength(4);expect(after.mixed.accounts.transactions.at(-1).expenditure).toBe('1200000');
  const saved=await bytes(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+after.worldEpoch,{data:saved})).ok()).toBe(true);await ready(page,locale);await assertExact(await bytes(page),saved,"await bytes(page)");
  await restartWorldServer(worldServer);for(const p of [page,peer])expect((await p.request.post('/api/access',{data:{secret:''}})).ok()).toBe(true);await page.reload();await peer.reload();await ready(page,locale);await ready(peer);await assertExact(await bytes(page),saved,"await bytes(page)");
  await page.getByRole('button',{name:t.referenceInspect,exact:true}).click();const at=await exposeMapPoint(page,await point(page,15,13));if(ja)await page.touchscreen.tap(at.x,at.y);else await page.mouse.click(at.x,at.y);
  await expect(page.getByTestId('land-current-value')).toBeVisible();await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/ground-${locale}-placed.png`});await closePanel(page);
  // A peer's actual edit updates the first browser and detaches both listener directions.
  const en=texts[0];await peer.getByRole('button',{name:en.landTask,exact:true}).click();await peer.getByLabel(en.landAction,{exact:true}).selectOption('removeCover');await quote(peer,en);
  await expect(peer.getByRole('button',{name:en.groundConfirm,exact:true})).toBeEnabled();await expect(peer.getByTestId('land-amount')).toContainText('¥0');await peer.getByRole('button',{name:en.groundConfirm,exact:true}).click();
  await expect.poll(async()=>(await state(page)).mixed.city.ground.length).toBe(0);after=await state(page);expect(after.mixed.city.producers).toHaveLength(0);expect(after.mixed.stationGraph.listeners).toHaveLength(0);expect(after.mixed.stationGraph.listenerMemberships[0].providers).toHaveLength(0);
  expect(after.mixed.accounts.transactions.at(-1).expenditure).toBe('1200000');const removed=await bytes(page);
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+after.worldEpoch,{data:removed})).ok()).toBe(true);await ready(page,locale);await assertExact(await bytes(page),removed,"await bytes(page)");
  await page.getByRole('button',{name:t.landTask,exact:true}).click();await page.getByLabel(t.landAction,{exact:true}).selectOption('cover');await page.getByLabel(t.groundType,{exact:true}).selectOption('{A70C51A0-B495-4F78-AA50-E4A2D2F7A7DA}');await expect(page.locator('.land-task .catalog-preview img')).toBeVisible();
  const beforeMix=await bytes(page),randomBefore=JSON.parse(beforeMix.toString('utf8')).random;await quote(page,t);await expect(page.getByRole('button',{name:t.groundConfirm,exact:true})).toBeEnabled();await assertExact(await bytes(page),beforeMix,"await bytes(page)");
  await page.getByRole('button',{name:t.groundConfirm,exact:true}).click();await expect.poll(async()=>(await state(peer)).mixed.city.ground.length).toBe(4);
  const mixed=await state(page);expect(JSON.parse((await bytes(page)).toString('utf8')).random.streams.find((r:any)=>r.id==='land-selection').state.index).toBe((randomBefore.streams.find((r:any)=>r.id==='land-selection').state.index+4)%55);
  expect(mixed.mixed.city.ground.every((g:any)=>['{168C94A9-1D5B-4160-BBD8-21C830DCB752}-0,0','{168C94A9-1D5B-4160-BBD8-21C830DCB752}-1,0'].includes(g.definition.recipeId))).toBe(true);
  const mixedSave=await bytes(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+mixed.worldEpoch,{data:mixedSave})).ok()).toBe(true);await ready(page,locale);await assertExact(await bytes(page),mixedSave,"await bytes(page)");expect(errors).toEqual([]);
 }finally{await context.close();}
});
