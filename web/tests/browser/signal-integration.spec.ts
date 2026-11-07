import {test,expect,type Page} from './fixtures.ts';
import {canonicalState as state,openPanel,closePanel,ready} from './browser-helpers.ts';
import {sourcePixelCounts} from './pixel-helpers.ts';
import {readFileSync,mkdirSync} from 'node:fs';
const en=JSON.parse(readFileSync('src/locales/en/app.json','utf8'));
const ja=JSON.parse(readFileSync('src/locales/ja/app.json','utf8'));
const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
const evidence=process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!;

async function pickSignal(page:Page,read:any){
 const voxel=read.mixed.voxels.find((v:any)=>v.position.x===22&&v.position.y===20),rail=voxel.traffic.rail;
 const frame=catalog.renderRecipes.find((r:any)=>r.id===rail.definition.recipeId).frames.find((f:any)=>f.status==='ready'&&f.selector.direction===rail.direction);
 const mask=catalog.masks[frame.masks['spring/day']],row=mask.findIndex((r:string)=>r.includes('1')),column=mask[row].indexOf('1');
 const p=voxel.position,center=Math.trunc((read.map.height-1)/2);
 const q={x:16*(p.x-center+p.y)-frame.anchor[0]+column+.5,y:8*(-p.x+center+p.y)-16*p.z-frame.anchor[1]+row+.5};
 await closePanel(page);const canvas=page.locator('canvas.world-canvas');
 await canvas.dispatchEvent('wheel',await canvas.evaluate((el,q)=>{const [x,y]=el.dataset.camera!.split(',').map(Number),z=Number(el.dataset.zoom);return{deltaX:(q.x-x)*z,deltaY:(q.y-y)*z,deltaMode:0};},q));
 await expect(canvas).toHaveAttribute('data-presentation','ready');
 expect((await sourcePixelCounts(page,catalog,[voxel],'signal',read.map.height))[0]).toBeGreaterThan(3);
 const click=await canvas.evaluate((el,q)=>{const r=el.getBoundingClientRect(),[x,y]=el.dataset.camera!.split(',').map(Number),z=Number(el.dataset.zoom);return{x:r.x+r.width/2+(q.x-x)*z,y:r.y+r.height/2+(q.y-y)*z};},q);
 await page.mouse.click(click.x,click.y);return page.locator('.primary-panel .fleet-controller-manager');
}
for(const [locale,suffix] of [['en','three'],['ja','shunting']] as const)test(`${locale} real shared signal waits, scoped properties, peer snapshots and checkpoint continuation`,async({page,browser,baseURL,worldServer})=>{
 test.setTimeout(120000);const text=locale==='ja'?ja:en,errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 const initial=await state(page),input=readFileSync(`../tmp/p5-24-signal-fleet-${suffix}.json`);
 expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+initial.worldEpoch,{data:input})).ok()).toBe(true);
 await page.goto('/');await ready(page);
 if(locale==='ja'){
  await openPanel(page,'settings');await page.getByLabel('Language',{exact:true}).selectOption('ja');await closePanel(page);
  await page.setViewportSize({width:390,height:844});await page.evaluate(()=>document.documentElement.style.fontSize='200%');
 }
 async function manager(){await openPanel(page,'fleet',locale);await page.getByRole('button',{name:text.controllerManager,exact:true}).click();return page.locator('.fleet-controller-manager');}
 const baseline=await state(page),first=baseline.mixed.fleet.controllers.find((c:any)=>c.name==='Signal ten'),second=baseline.mixed.fleet.controllers.find((c:any)=>c.name==='Signal twenty');
 let controls=await manager();await controls.getByRole('combobox',{name:text.controllerManager,exact:true}).selectOption(second.id);
 await controls.getByRole('button',{name:text.controllerConfigure,exact:true}).click();let dialog=page.getByRole('dialog',{name:text.controllerConfigure,exact:true});
 await dialog.getByLabel(text.controllerMinutes,{exact:true}).fill('30');await dialog.getByRole('button',{name:text.controllerApply,exact:true}).click();await expect(dialog).toHaveCount(0);
 controls=await pickSignal(page,await state(page));await expect(controls).toContainText(text.signalSharedController);
 await controls.getByRole('combobox',{name:text.controllerManager,exact:true}).selectOption(second.id);
 const before=await(await page.request.get('/api/saves/download')).body();
 await controls.getByRole('button',{name:text.controllerConfigure,exact:true}).click();dialog=page.getByRole('dialog',{name:text.controllerConfigure,exact:true});
 for(const axis of ['X','Y','Z'])await expect(dialog.getByLabel(axis,{exact:true})).toBeDisabled();
 await expect(dialog.getByLabel(text.position,{exact:true})).toBeDisabled();
 await expect(dialog.getByLabel(text.controllerMinutes,{exact:true})).toHaveValue('30');
 await dialog.getByRole('button',{name:text.cancel,exact:true}).click();expect(await(await page.request.get('/api/saves/download')).body()).toEqual(before);
 await controls.getByRole('button',{name:text.controllerConfigure,exact:true}).click();dialog=page.getByRole('dialog',{name:text.controllerConfigure,exact:true});
 await dialog.getByLabel(text.controllerMinutes,{exact:true}).fill('20');
 mkdirSync(evidence+'/signals',{recursive:true});await page.screenshot({path:evidence+`/signals/${locale}-settings.png`});
 await dialog.getByRole('button',{name:text.controllerApply,exact:true}).click();await expect(dialog).toHaveCount(0);
 let current=await state(page);expect(current.mixed.fleet.controllers.find((c:any)=>c.id===first.id).harbors[0].minutes).toBe(10);
 expect(current.mixed.fleet.trains).toEqual(baseline.mixed.fleet.trains);expect(current.mixed.queue).toEqual(baseline.mixed.queue);expect(current.mixed.voxels).toEqual(baseline.mixed.voxels);
 async function stepTo(minute:number){await closePanel(page);while(Number((await state(page)).tick)<minute){const tick=(await state(page)).tick;await expect(page.locator('button.step')).toBeEnabled();const canvas=page.locator('canvas.world-canvas');await canvas.focus();await canvas.press('n');await expect.poll(async()=>Number((await state(page)).tick)).toBe(Number(tick)+1);}}
 async function inspectWait(index:number){
  await openPanel(page,'fleet',locale);await page.getByRole('button',{name:new RegExp('^'+text.ownedStock)}).click();
  await page.locator('.fleet-owned .catalog-item-main').nth(index).click();await expect(page.getByTestId('fleet-state')).toContainText(text.fleetWaiting);await expect(page.getByTestId('fleet-wait-harbor')).toHaveText(text.fleetSignalWaiting);await page.getByTestId('fleet-wait-harbor').scrollIntoViewIfNeeded();await expect(page.getByTestId('fleet-wait-harbor')).toBeInViewport();
 }
 const context=await browser.newContext({baseURL}),peer=await context.newPage();
 try{
  expect((await peer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);await peer.goto('/');await ready(peer);
  await stepTo(102);current=await state(page);const t=current.mixed.fleet.trains[0];expect(t.state).toBe('stoppingatstation');expect(t.stopCallCount).toBe(1);expect(t.moveCount).toBe(0);expect(t.passengers).toBe(0);
  await expect.poll(async()=>JSON.stringify((await state(peer)).mixed)).toBe(JSON.stringify(current.mixed));
  await inspectWait(0);await page.screenshot({path:evidence+`/signals/${locale}-waiting.png`});await closePanel(page);
  const bytes=await(await page.request.get('/api/saves/download')).body();
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+current.worldEpoch,{data:bytes})).ok()).toBe(true);await page.goto('/');await ready(page,locale);if(locale==='ja')await page.evaluate(()=>document.documentElement.style.fontSize='200%');
  expect((await state(page)).mixed).toEqual(current.mixed);
  await worldServer.restart!();expect((await page.request.post('/api/access',{data:{secret:''}})).ok()).toBe(true);await page.reload();await ready(page,locale);if(locale==='ja')await page.evaluate(()=>document.documentElement.style.fontSize='200%');
  expect((await state(page)).mixed).toEqual(current.mixed);
  await stepTo(114);current=await state(page);const waiting=current.mixed.fleet.trains[1];expect(waiting.state).toBe('stoppingatstation');expect(waiting.stopCallCount).toBe(1);expect(waiting.moveCount).toBe(2);
  expect(current.mixed.queue.pending.find((a:any)=>a.handlerId==='train/'+waiting.id).dueMinute).toBe('134');
  await inspectWait(1);await page.screenshot({path:evidence+`/signals/${locale}-second-wait.png`});
  controls=await manager();await controls.getByRole('combobox',{name:text.controllerManager,exact:true}).selectOption(second.id);
  await controls.getByRole('button',{name:text.controllerConfigure,exact:true}).click();dialog=page.getByRole('dialog',{name:text.controllerConfigure,exact:true});
  await dialog.getByRole('button',{name:text.controllerRemoveLocation,exact:true}).click();await dialog.getByRole('button',{name:text.controllerApply,exact:true}).click();await expect(dialog).toHaveCount(0);
  expect((await state(page)).mixed.queue).toEqual(current.mixed.queue);expect((await state(page)).mixed.fleet.trains).toEqual(current.mixed.fleet.trains);
  await stepTo(136);current=await state(page);expect(current.mixed.fleet.trains[1].stopCallCount).toBe(0);expect(current.mixed.fleet.trains[1].moveCount).toBe(4);
  const simple=current.mixed.fleet.trains[2];expect(simple.state).toBe('moving');expect(simple.stopCallCount).toBe(0);expect(simple.moveCount).toBe(5);expect(simple.cars[0].state.location.x).toBe(23);
  expect(current.mixed.voxels.find((v:any)=>v.position.x===22&&v.position.y===20).traffic.rail.controllerId).toBeNull();
  expect(current.mixed.fleet.trains.every((t:any)=>t.passengers===0&&t.boardingOrigin===null)).toBe(true);expect(errors).toEqual([]);
 }finally{await context.close();}
});
