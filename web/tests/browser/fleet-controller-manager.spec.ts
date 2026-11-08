import {test,expect,type Page,type WebSocketRoute} from './fixtures.ts';
import {canonicalState as state,openPanel,closePanel,ready} from './browser-helpers.ts';
import {readFileSync,mkdirSync} from 'node:fs';
const en=JSON.parse(readFileSync('src/locales/en/app.json','utf8'));
const ja=JSON.parse(readFileSync('src/locales/ja/app.json','utf8'));
const profile=JSON.parse(readFileSync('src/generated/fleet-profile.json','utf8'));
const evidence=process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!;
async function setup(page:Page,locale='en'){
 const initial=await state(page);
 const input=readFileSync(process.env.FREETRAIN_CONTROLLER_FLEET_OUTPUT??'../tmp/p5-18-manager-fleet.json');
 expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+initial.worldEpoch,{data:input})).ok()).toBe(true);
 await page.goto('/');await ready(page);
 if(locale==='ja'){await openPanel(page,'settings');await page.getByLabel('Language',{exact:true}).selectOption('ja');await closePanel(page);}
}
async function manager(page:Page,text= en,locale='en'){
 await openPanel(page,'fleet',locale);await page.getByRole('button',{name:text.controllerManager,exact:true}).click();
 return page.locator('.fleet-controller-manager');
}
async function owned(page:Page,text=en){
 await page.getByRole('button',{name:new RegExp(`^${text.ownedStock}`)}).click();return page.locator('.fleet-details');
}
for(const [locale,coarse] of [['en',false],['ja',false],['en',true],['ja',true]] as const)test.describe(`${locale} ${coarse?'coarse':'desktop'}`,()=>{
 test.use({hasTouch:coarse});
 test(`${locale} shared controller management, copy isolation, assignments and reopen`,async({page,browser,baseURL,worldServer})=>{
 test.setTimeout(90000);const text=locale==='ja'?ja:en,errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 await setup(page,locale);const initial=await state(page);
 if(coarse){await page.setViewportSize({width:390,height:844});await page.evaluate(()=>document.documentElement.style.fontSize='200%');}
 let controls=await manager(page,text,locale);
 await controls.getByRole('button',{name:text.controllerCreate,exact:true}).click();
 let dialog=page.getByRole('dialog',{name:text.controllerCreate,exact:true});
 await dialog.getByLabel(text.controllerType,{exact:true}).selectOption(profile.providers.find((p:any)=>p.kind==='timetable').binding.contributionId);
 await dialog.getByLabel(text.controllerName,{exact:true}).fill('共有・Shared');await dialog.getByRole('button',{name:text.controllerApply,exact:true}).click();
 await expect(dialog).toHaveCount(0);
 let f=(await state(page)).mixed.fleet,source=f.controllers.find((c:any)=>c.name==='共有・Shared');expect(source).toBeDefined();
 await controls.getByRole('combobox',{name:text.controllerManager,exact:true}).selectOption(source.id);
 await controls.getByRole('button',{name:text.controllerConfigure,exact:true}).click();dialog=page.getByRole('dialog',{name:text.controllerConfigure,exact:true});
 for(const [axis,value] of [['X','22'],['Y','20'],['Z','2']])await dialog.getByLabel(axis,{exact:true}).fill(value);
 await dialog.getByRole('button',{name:text.controllerLocation,exact:true}).click();
 mkdirSync(evidence+'/controller-manager',{recursive:true});await page.screenshot({path:evidence+`/controller-manager/${locale}-${coarse?'coarse':'desktop'}-configure.png`});
 await dialog.getByLabel(text.controllerMinutes,{exact:true}).fill('17');await dialog.getByRole('button',{name:text.controllerApply,exact:true}).click();await expect(dialog).toHaveCount(0);
 await controls.getByRole('combobox',{name:text.sharedController,exact:true}).selectOption(source.id);
 await expect(page.locator('.command-feedback')).toHaveText(text.accepted);
 const details=await owned(page,text);await page.locator('.fleet-owned .catalog-item-main').first().click();
 const delegation=f.controllers.find((c:any)=>c.kind==='delegation');
 await details.getByRole('combobox',{name:text.sharedController,exact:true}).selectOption(delegation.id);
 await expect(details.getByTestId('effective-controller')).toContainText('共有・Shared');
 await page.locator('.fleet-owned .catalog-item-main').nth(1).click();
 await details.getByRole('combobox',{name:text.sharedController,exact:true}).selectOption(source.id);
 await expect(details.getByTestId('effective-controller')).toContainText('共有・Shared');
 controls=await manager(page,text,locale);await controls.getByRole('combobox',{name:text.controllerManager,exact:true}).selectOption(source.id);
 await expect(controls.getByTestId('controller-reference-count')).toContainText('2');
 await controls.getByRole('button',{name:text.controllerCopy,exact:true}).click();dialog=page.getByRole('dialog',{name:text.controllerCopy,exact:true});
 await expect(dialog).toContainText('共有・Shared 1');await dialog.getByRole('button',{name:text.controllerApply,exact:true}).click();await expect(dialog).toHaveCount(0);
 f=(await state(page)).mixed.fleet;const copy=f.controllers.find((c:any)=>c.name==='共有・Shared 1');expect(copy.harbors).toEqual(f.controllers.find((c:any)=>c.id===source.id).harbors);
 await controls.getByRole('combobox',{name:text.controllerManager,exact:true}).selectOption(copy.id);
 await controls.getByRole('button',{name:text.controllerConfigure,exact:true}).click();dialog=page.getByRole('dialog',{name:text.controllerConfigure,exact:true});
 await dialog.getByLabel(text.controllerMinutes,{exact:true}).fill('31');await dialog.getByRole('button',{name:text.controllerApply,exact:true}).click();await expect(dialog).toHaveCount(0);
 f=(await state(page)).mixed.fleet;expect(f.controllers.find((c:any)=>c.id===source.id).harbors[0].minutes).toBe(17);expect(f.controllers.find((c:any)=>c.id===copy.id).harbors[0].minutes).toBe(31);
 await controls.getByRole('combobox',{name:text.controllerManager,exact:true}).selectOption(source.id);
 await controls.getByRole('button',{name:text.controllerDelete,exact:true}).click();dialog=page.getByRole('dialog',{name:text.controllerDelete,exact:true});
 await expect(dialog).toContainText(text.controllerReferenced);await expect(dialog.getByRole('button',{name:text.controllerDelete,exact:true})).toBeDisabled();
 await dialog.getByRole('button',{name:text.cancel,exact:true}).click();
 await controls.getByRole('button',{name:text.controllerRename,exact:true}).click();dialog=page.getByRole('dialog',{name:text.controllerRename,exact:true});
 await dialog.getByLabel(text.controllerName,{exact:true}).fill('変更・Renamed');await dialog.getByRole('button',{name:text.controllerApply,exact:true}).click();await expect(dialog).toHaveCount(0);
 const before=await(await page.request.get('/api/saves/download')).body();
 await controls.getByRole('button',{name:text.controllerConfigure,exact:true}).click();dialog=page.getByRole('dialog',{name:text.controllerConfigure,exact:true});
 await dialog.getByLabel(text.controllerMinutes,{exact:true}).fill('59');await dialog.getByRole('button',{name:text.cancel,exact:true}).click();
 expect(await(await page.request.get('/api/saves/download')).body()).toEqual(before);
 await controls.getByRole('combobox',{name:text.controllerManager,exact:true}).selectOption(copy.id);
 await controls.getByRole('button',{name:text.controllerDelete,exact:true}).click();dialog=page.getByRole('dialog',{name:text.controllerDelete,exact:true});
 await dialog.getByRole('button',{name:text.controllerDelete,exact:true}).click();await expect(dialog).toHaveCount(0);
 const downloaded=await(await page.request.get('/api/saves/download')).body(),epoch=(await state(page)).worldEpoch;
 expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+epoch,{data:downloaded})).ok()).toBe(true);
 await page.goto('/');await ready(page,locale);const current=await state(page);
 expect(current.mixed.fleet.controllers.find((c:any)=>c.id===source.id).name).toBe('変更・Renamed');
 expect(current.mixed.fleet.trains[0].controllerId).toBe(delegation.id);expect(current.mixed.fleet.trains[1].controllerId).toBe(source.id);
 expect(current.tick).toBe(initial.tick);expect(current.cash).toBe(initial.cash);expect(current.mixed.queue).toEqual(initial.mixed.queue);
 const context=await browser.newContext({baseURL}),peer=await context.newPage();
 try{
  expect((await peer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);await peer.goto('/');await ready(peer);
  const observer=await manager(peer);await expect(observer.getByRole('button',{name:en.controllerCreate,exact:true})).toBeDisabled();
  await observer.getByRole('combobox',{name:en.controllerManager,exact:true}).selectOption(source.id);
  await observer.getByRole('button',{name:en.controllerConfigure,exact:true}).click();
  await expect(peer.getByRole('dialog').getByRole('button',{name:en.controllerApply,exact:true})).toBeDisabled();
 }finally{await context.close();}
 controls=await manager(page,text,locale);await controls.getByRole('combobox',{name:text.controllerManager,exact:true}).selectOption(source.id);
 mkdirSync(evidence+'/controller-manager',{recursive:true});await page.screenshot({path:evidence+`/controller-manager/${locale}-${coarse?'coarse':'desktop'}.png`});
 expect(errors).toEqual([]);
 await closePanel(page);await worldServer.restart!();expect((await page.request.post('/api/access',{data:{secret:''}})).ok()).toBe(true);await page.reload();await ready(page,locale);
 expect((await state(page)).mixed.fleet.controllers.find((c:any)=>c.id===source.id).harbors[0].minutes).toBe(17);
});
});
test('controller drafts detect peer revision changes and identical command receipts do not create twice',async({page,browser,baseURL})=>{
 await setup(page);let transport:WebSocketRoute|undefined;const commands:any[]=[],replies:any[]=[];
 await page.routeWebSocket('**/api/session?*',route=>{transport=route.connectToServer();route.onMessage(m=>{const d=JSON.parse(String(m));if(d.payload?.kind==='manageFleetController')commands.push(d);transport!.send(m);});transport.onMessage(m=>{const d=JSON.parse(String(m));if(d.type==='accepted'||d.type==='rejected')replies.push(d);route.send(m);});});
 await page.reload();await ready(page);const controls=await manager(page);
 await controls.getByRole('button',{name:en.controllerCreate,exact:true}).click();let dialog=page.getByRole('dialog',{name:en.controllerCreate,exact:true});
 await dialog.getByLabel(en.controllerName,{exact:true}).fill('Shared timetable');await dialog.getByRole('button',{name:en.controllerApply,exact:true}).click();await expect(dialog).toHaveCount(0);
 const request=commands.at(-1),before=await(await page.request.get('/api/saves/download')).body();transport!.send(JSON.stringify(request));
 await expect.poll(()=>replies.filter(r=>r.requestId===request.requestId).length).toBe(2);
 expect(await(await page.request.get('/api/saves/download')).body()).toEqual(before);
 const f=(await state(page)).mixed.fleet,c=f.controllers.find((c:any)=>c.name==='Shared timetable');
 await controls.getByRole('combobox',{name:en.controllerManager,exact:true}).selectOption(c.id);
 await controls.getByRole('button',{name:en.controllerRename,exact:true}).click();dialog=page.getByRole('dialog',{name:en.controllerRename,exact:true});await dialog.getByLabel(en.controllerName,{exact:true}).fill('Local draft');
 const context=await browser.newContext({baseURL}),peer=await context.newPage();
 try{
  expect((await peer.request.post('/api/access',{data:{secret:'operator-test'}})).ok()).toBe(true);await peer.goto('/');await ready(peer);const remote=await manager(peer);
  await remote.getByRole('combobox',{name:en.controllerManager,exact:true}).selectOption(c.id);await remote.getByRole('button',{name:en.controllerRename,exact:true}).click();
  await peer.getByRole('dialog').getByLabel(en.controllerName,{exact:true}).fill('Peer name');await peer.getByRole('dialog').getByRole('button',{name:en.controllerApply,exact:true}).click();
  await expect(dialog).toContainText(en.controllerConflict);await expect(dialog.getByRole('button',{name:en.controllerApply,exact:true})).toBeDisabled();
  await dialog.getByRole('button',{name:en.cancel,exact:true}).click();
 }finally{await context.close();}
});
test('signal inspector edits a selected shared controller at its location without assigning the signal',async({page})=>{
 await setup(page);const controls=await manager(page);
 await controls.getByRole('button',{name:en.controllerCreate,exact:true}).click();let dialog=page.getByRole('dialog',{name:en.controllerCreate,exact:true});
 await dialog.getByLabel(en.controllerType,{exact:true}).selectOption(profile.providers.find((p:any)=>p.kind==='timetable').binding.contributionId);
 await dialog.getByLabel(en.controllerName,{exact:true}).fill('Signal settings');await dialog.getByRole('button',{name:en.controllerApply,exact:true}).click();await expect(dialog).toHaveCount(0);
 const read=await state(page),c=read.mixed.fleet.controllers.find((c:any)=>c.name==='Signal settings');
 const voxel=read.mixed.voxels.find((v:any)=>v.traffic?.rail?.definition.contributionId==='{5349DE2A-DF3E-49B4-87A0-973105B7E6A4}');
 const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
 const frame=catalog.renderRecipes.find((r:any)=>r.id===voxel.traffic.rail.definition.recipeId).frames.find((f:any)=>f.status==='ready'&&f.selector.direction===voxel.traffic.rail.direction);
 const mask=catalog.masks[frame.masks['spring/day']],row=mask.findIndex((r:string)=>r.includes('1')),column=mask[row].indexOf('1');
 const p=voxel.position,center=Math.trunc((read.map.height-1)/2),q={x:16*(p.x-center+p.y)-frame.anchor[0]+column+.5,y:8*(-p.x+center+p.y)-16*p.z-frame.anchor[1]+row+.5};
 await closePanel(page);const canvas=page.locator('canvas.world-canvas');
 await canvas.dispatchEvent('wheel',await canvas.evaluate((el,q)=>{const [x,y]=el.dataset.camera!.split(',').map(Number),z=Number(el.dataset.zoom);return{deltaX:(q.x-x)*z,deltaY:(q.y-y)*z,deltaMode:0};},q));
 await expect(canvas).toHaveAttribute('data-presentation','ready');
 const click=await canvas.evaluate((el,q)=>{const r=el.getBoundingClientRect(),[x,y]=el.dataset.camera!.split(',').map(Number),z=Number(el.dataset.zoom);return{x:r.x+r.width/2+(q.x-x)*z,y:r.y+r.height/2+(q.y-y)*z};},q);await page.mouse.click(click.x,click.y);
 const inspector=page.locator('.primary-panel');await expect(inspector).toContainText(en.signalSharedController);
 await inspector.getByRole('combobox',{name:en.controllerManager,exact:true}).selectOption(c.id);
 const before=await(await page.request.get('/api/saves/download')).body();
 await inspector.getByRole('button',{name:en.controllerConfigure,exact:true}).click();dialog=page.getByRole('dialog',{name:en.controllerConfigure,exact:true});
 await expect(dialog.getByLabel('X',{exact:true})).toHaveValue(String(p.x));await expect(dialog.getByLabel('Y',{exact:true})).toHaveValue(String(p.y));
 expect(await(await page.request.get('/api/saves/download')).body()).toEqual(before);
 await dialog.getByRole('button',{name:en.controllerLocation,exact:true}).click();await dialog.getByLabel(en.controllerStop,{exact:true}).selectOption('pass');
 await dialog.getByRole('button',{name:en.controllerApply,exact:true}).click();await expect(dialog).toHaveCount(0);
 const after=await state(page);expect(after.mixed.fleet.controllers.find((r:any)=>r.id===c.id).harbors).toEqual([{location:p,kind:'pass',minutes:0,reverse:false}]);
 expect(after.mixed.fleet.trains).toEqual(read.mixed.fleet.trains);expect(after.mixed.voxels).toEqual(read.mixed.voxels);expect(voxel.traffic.rail.controllerId).toBeNull();
 await inspector.getByRole('button',{name:en.controllerConfigure,exact:true}).click();dialog=page.getByRole('dialog',{name:en.controllerConfigure,exact:true});
 await dialog.getByRole('button',{name:en.controllerRemoveLocation,exact:true}).click();await dialog.getByRole('button',{name:en.controllerApply,exact:true}).click();await expect(dialog).toHaveCount(0);
 expect((await state(page)).mixed.fleet.controllers.find((r:any)=>r.id===c.id).harbors).toEqual([]);
});
test('named controllers can be created and configured before buying a train',async({page})=>{
 await page.goto('/');await ready(page);const initial=await state(page);expect(initial.mixed?.fleet).toBeUndefined();
 const controls=await manager(page);await controls.getByRole('button',{name:en.controllerCreate,exact:true}).click();
 let dialog=page.getByRole('dialog',{name:en.controllerCreate,exact:true});await dialog.getByLabel(en.controllerName,{exact:true}).fill('Before trains');
 await dialog.getByRole('button',{name:en.controllerApply,exact:true}).click();await expect(dialog).toHaveCount(0);
 const after=await state(page);expect(after.mixed.fleet.trains).toEqual([]);expect(after.mixed.accounts.transactions).toEqual([]);expect(after.tick).toBe(initial.tick);
 const controller=after.mixed.fleet.controllers.find((c:any)=>c.name==='Before trains');expect(controller.kind).toBe('timetable');
 await controls.getByRole('combobox',{name:en.controllerManager,exact:true}).selectOption(controller.id);
 const before=await(await page.request.get('/api/saves/download')).body();
 await controls.getByRole('button',{name:en.controllerConfigure,exact:true}).click();dialog=page.getByRole('dialog',{name:en.controllerConfigure,exact:true});
 await expect(dialog.getByRole('button',{name:en.controllerLocation,exact:true})).toBeEnabled();
 await dialog.getByRole('button',{name:en.cancel,exact:true}).click();expect(await(await page.request.get('/api/saves/download')).body()).toEqual(before);
});
for(const locale of ['en','ja'] as const)test(`${locale} basic TAT junction and daily drafts synchronize, cancel and reopen`,async({page,browser,baseURL,worldServer})=>{
 test.setTimeout(90000);const text=locale==='ja'?ja:en;
 const initial=await state(page),input=readFileSync('../tmp/p5-19-tat-fleet.json');
 const upload=await page.request.post('/api/saves/upload?expectedWorldEpoch='+initial.worldEpoch,{data:input});expect(upload.ok(),await upload.text()).toBe(true);
 await page.goto('/');await ready(page);
 if(locale==='ja'){await openPanel(page,'settings');await page.getByLabel('Language',{exact:true}).selectOption('ja');await closePanel(page);await page.setViewportSize({width:390,height:844});await page.evaluate(()=>document.documentElement.style.fontSize='150%');}
 const source=(await state(page)).mixed.fleet.controllers.find((c:any)=>c.name==='Shared TAT');
 const controls=await manager(page,text,locale);await controls.getByRole('combobox',{name:text.controllerManager,exact:true}).selectOption(source.id);
 const context=await browser.newContext({baseURL}),peer=await context.newPage();
 try{
  await peer.goto('/');await ready(peer);
  const peers=await manager(peer);await peers.getByRole('combobox',{name:en.controllerManager,exact:true}).selectOption(source.id);
  await peers.getByRole('button',{name:en.controllerConfigure,exact:true}).click();
  async function editJunction(){
   await controls.getByRole('button',{name:text.controllerConfigure,exact:true}).click();
   const dialog=page.getByRole('dialog',{name:text.controllerConfigure,exact:true});
   await dialog.getByLabel(text.controllerSettingType,{exact:true}).selectOption('junction');
   await dialog.getByLabel(text.position,{exact:true}).selectOption('0');return dialog;
  }
  let dialog=await editJunction();await dialog.getByLabel(text.controllerDefaultRoute,{exact:true}).selectOption('straight');
  const before=await(await page.request.get('/api/saves/download')).body();await dialog.getByRole('button',{name:text.cancel,exact:true}).click();
  expect(await(await page.request.get('/api/saves/download')).body()).toEqual(before);
  dialog=await editJunction();await expect(dialog.getByLabel(text.controllerDefaultRoute,{exact:true})).toHaveValue('curve');
  await dialog.getByLabel(text.controllerDefaultRoute,{exact:true}).selectOption('straight');
  mkdirSync(evidence+'/tat',{recursive:true});await page.screenshot({path:evidence+`/tat/${locale}-junction.png`});
  await dialog.getByRole('button',{name:text.controllerApply,exact:true}).click();await expect(dialog).toHaveCount(0);
  await expect(peer.getByRole('dialog',{name:en.controllerConfigure,exact:true})).toContainText(en.controllerConflict);await peer.getByRole('dialog',{name:en.controllerConfigure,exact:true}).getByRole('button',{name:en.cancel,exact:true}).click();
  await expect.poll(async()=>((await state(peer)).mixed.fleet.controllers.find((c:any)=>c.id===source.id).junctions[0].defaultRoute)).toBe('straight');
  await controls.getByRole('button',{name:text.controllerConfigure,exact:true}).click();dialog=page.getByRole('dialog',{name:text.controllerConfigure,exact:true});
  await dialog.getByLabel(text.controllerStop,{exact:true}).selectOption('daily');await dialog.getByLabel(text.controllerDepartureHour,{exact:true}).fill('8');await dialog.getByLabel(text.controllerDepartureMinute,{exact:true}).fill('0');await dialog.getByLabel(text.controllerReverse,{exact:true}).check();
  await page.screenshot({path:evidence+`/tat/${locale}-daily.png`});await dialog.getByRole('button',{name:text.controllerApply,exact:true}).click();await expect(dialog).toHaveCount(0);
  const bytes=await(await page.request.get('/api/saves/download')).body(),epoch=(await state(page)).worldEpoch;
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+epoch,{data:bytes})).ok()).toBe(true);await page.goto('/');await ready(page,locale);
  const c=(await state(page)).mixed.fleet.controllers.find((c:any)=>c.id===source.id);
  expect(c.junctions).toEqual([{location:{x:22,y:20,z:2},defaultRoute:'straight'}]);expect(c.harbors).toEqual([{location:{x:100,y:100,z:2},kind:'daily',minutes:480,reverse:true}]);
  const reopenedControls=await manager(page,text,locale),copy=(await state(page)).mixed.fleet.controllers.find((row:any)=>row.name==='Shared TAT 1');
  await reopenedControls.getByRole('combobox',{name:text.controllerManager,exact:true}).selectOption(copy.id);
  await reopenedControls.getByRole('button',{name:new RegExp('^'+text.controllerCleanJunctions)}).click();
  dialog=page.getByRole('dialog',{name:text.controllerCleanJunctions,exact:true});await expect(dialog).toContainText('100, 100, 2');
  const beforeCleanup=await(await page.request.get('/api/saves/download')).body();await dialog.getByRole('button',{name:text.cancel,exact:true}).click();
  expect(await(await page.request.get('/api/saves/download')).body()).toEqual(beforeCleanup);
  await reopenedControls.getByRole('button',{name:new RegExp('^'+text.controllerCleanJunctions)}).click();dialog=page.getByRole('dialog',{name:text.controllerCleanJunctions,exact:true});
  await dialog.getByRole('button',{name:text.controllerApply,exact:true}).click();await expect(dialog).toHaveCount(0);
  const cleaned=(await state(page)).mixed.fleet.controllers.find((row:any)=>row.id===copy.id);expect(cleaned.junctions).toHaveLength(1);expect(cleaned.harbors).toHaveLength(1);
  await closePanel(page);await worldServer.restart!();expect((await page.request.post('/api/access',{data:{secret:''}})).ok()).toBe(true);await page.reload();await ready(page,locale);
  expect((await state(page)).mixed.fleet.controllers.find((row:any)=>row.id===source.id).harbors[0].minutes).toBe(480);
 }finally{await context.close();}
});
for(const locale of ['en','ja'] as const)test(`${locale} advanced ordered rules edit masks, copies, order, peer conflicts and confirmed clear`,async({page,browser,baseURL,worldServer})=>{
 test.setTimeout(120000);const text=locale==='ja'?ja:en,errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 const initial=await state(page),input=readFileSync('../tmp/p5-20-tat-fleet.json');
 expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+initial.worldEpoch,{data:input})).ok()).toBe(true);
 await page.goto('/');await ready(page);
 if(locale==='ja'){await openPanel(page,'settings');await page.getByLabel('Language',{exact:true}).selectOption('ja');await closePanel(page);await page.setViewportSize({width:390,height:844});await page.evaluate(()=>document.documentElement.style.fontSize='150%');}
 const baseline=await state(page),source=baseline.mixed.fleet.controllers.find((c:any)=>c.name==='Shared TAT');
 const controls=await manager(page,text,locale);await controls.getByRole('combobox',{name:text.controllerManager,exact:true}).selectOption(source.id);
 await controls.getByRole('button',{name:text.controllerConfigure,exact:true}).click();let dialog=page.getByRole('dialog',{name:text.controllerConfigure,exact:true});
 await dialog.getByLabel(text.controllerStop,{exact:true}).selectOption('advanced');await dialog.getByRole('button',{name:text.controllerApply,exact:true}).click();await expect(dialog).toHaveCount(0);
 async function openRules(kind='harbor'){
  await controls.getByRole('button',{name:text.controllerConfigure,exact:true}).click();const config=page.getByRole('dialog',{name:text.controllerConfigure,exact:true});
  if(kind==='junction'){await config.getByLabel(text.controllerSettingType,{exact:true}).selectOption('junction');await config.getByLabel(text.position,{exact:true}).selectOption('0');}
  await config.getByRole('button',{name:text.controllerRules,exact:true}).click();return page.getByRole('dialog',{name:text.controllerRules,exact:true});
 }
 async function apply(rules=dialog){await rules.getByRole('button',{name:text.controllerApply,exact:true}).click();await expect(rules.getByRole('button',{name:text.controllerApply,exact:true})).toHaveCount(0);}
 const data=async()=>((await state(page)).mixed.fleet.controllers.find((c:any)=>c.id===source.id));
 dialog=await openRules();const before=await(await page.request.get('/api/saves/download')).body();
 await dialog.getByRole('button',{name:text.ruleAdd,exact:true}).click();
 await expect(dialog.getByLabel(text.ruleAction,{exact:true})).toHaveValue('pass');
 for(const [label,value] of [[text.ruleMonth,'4'],[text.ruleDay,'23'],[text.ruleWeekday,'5'],[text.ruleHour,'8'],[text.ruleMinute,'0']])await dialog.getByLabel(label,{exact:true}).selectOption(value);
 await dialog.getByLabel(text.ruleAction,{exact:true}).selectOption('pass');
 await dialog.getByRole('button',{name:text.cancel,exact:true}).click();expect(await(await page.request.get('/api/saves/download')).body()).toEqual(before);
 await dialog.getByRole('button',{name:text.ruleAdd,exact:true}).click();
 for(const [label,value] of [[text.ruleMonth,'4'],[text.ruleDay,'23'],[text.ruleWeekday,'5'],[text.ruleHour,'8'],[text.ruleMinute,'0']])await dialog.getByLabel(label,{exact:true}).selectOption(value);
 await dialog.getByLabel(text.ruleAction,{exact:true}).selectOption('pass');await apply();
 await expect.poll(async()=>(await data()).harbors[0].rules.length).toBe(1);
 await dialog.getByRole('button',{name:text.ruleAdd,exact:true}).click();
 await expect(dialog.getByLabel(text.ruleMonth,{exact:true})).toHaveValue('4');await expect(dialog.getByLabel(text.ruleDay,{exact:true})).toHaveValue('23');
 for(const [label,value] of [[text.ruleMonth,'4'],[text.ruleDay,'23'],[text.ruleWeekday,'5'],[text.ruleHour,'8'],[text.ruleMinute,'0']])await dialog.getByLabel(label,{exact:true}).selectOption(value);
 await expect(dialog).toContainText(text.ruleDuplicate);await expect(dialog.getByRole('button',{name:text.controllerApply,exact:true})).toBeDisabled();
 await dialog.getByRole('button',{name:text.cancel,exact:true}).click();
 await dialog.getByLabel(text.controllerRules,{exact:true}).selectOption('0');await dialog.getByRole('button',{name:text.ruleEdit,exact:true}).click();
 await expect(dialog.getByLabel(text.ruleDay,{exact:true})).toHaveValue('23');await dialog.getByLabel(text.ruleAction,{exact:true}).selectOption('go');await apply();
 await dialog.getByRole('button',{name:text.ruleCopy,exact:true}).click();await apply();
 await dialog.getByLabel(text.controllerRules,{exact:true}).selectOption('1');await dialog.getByRole('button',{name:text.ruleEdit,exact:true}).click();
 await dialog.getByLabel(text.ruleAction,{exact:true}).selectOption('stop');await apply();
 await dialog.getByRole('button',{name:text.ruleUp,exact:true}).click();await apply();
 expect((await data()).harbors[0].rules.map((r:any)=>r.action)).toEqual(['stop','go']);
 mkdirSync(evidence+'/advanced-tat',{recursive:true});await page.screenshot({path:evidence+`/advanced-tat/${locale}-ordered.png`});
 await dialog.getByRole('button',{name:text.close,exact:true}).click();
 dialog=await openRules('junction');await dialog.getByRole('button',{name:text.ruleAdd,exact:true}).click();
 await expect(dialog.getByLabel(text.ruleMinute,{exact:true})).toHaveCount(0);
 await dialog.getByLabel(text.ruleMonth,{exact:true}).selectOption('12');await dialog.getByLabel(text.ruleDay,{exact:true}).selectOption('31');
 await dialog.getByLabel(text.ruleAction,{exact:true}).selectOption('curve');await apply();
 await dialog.getByLabel(text.controllerRules,{exact:true}).selectOption('0');await dialog.getByRole('button',{name:text.ruleEdit,exact:true}).click();
 await expect(dialog.getByLabel(text.ruleDay,{exact:true})).toHaveValue('31');
 await page.screenshot({path:evidence+`/advanced-tat/${locale}-junction.png`});await dialog.getByRole('button',{name:text.cancel,exact:true}).click();
 await dialog.getByRole('button',{name:text.close,exact:true}).click();
 const context=await browser.newContext({baseURL}),peer=await context.newPage();
 try{
  await peer.goto('/');await ready(peer);const remote=await manager(peer);await remote.getByRole('combobox',{name:en.controllerManager,exact:true}).selectOption(source.id);
  dialog=await openRules();await dialog.getByRole('button',{name:text.ruleAdd,exact:true}).click();
  await remote.getByRole('button',{name:en.controllerRename,exact:true}).click();let peerDialog=peer.getByRole('dialog');await peerDialog.getByLabel(en.controllerName,{exact:true}).fill('Peer ordered TAT');
  await peerDialog.getByRole('button',{name:en.controllerApply,exact:true}).click();await expect(dialog).toContainText(text.controllerConflict);await expect(dialog.getByRole('button',{name:text.controllerApply,exact:true})).toBeDisabled();
  await dialog.getByRole('button',{name:text.cancel,exact:true}).click();await dialog.getByRole('button',{name:text.close,exact:true}).click();
  expect((await peer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);await peer.goto('/');await ready(peer);
  const observer=await manager(peer);await observer.getByRole('combobox',{name:en.controllerManager,exact:true}).selectOption(source.id);
  await observer.getByRole('button',{name:en.controllerConfigure,exact:true}).click();await peer.getByRole('dialog').getByRole('button',{name:en.controllerRules,exact:true}).click();
  const inspected=peer.getByRole('dialog',{name:en.controllerRules,exact:true});await expect(inspected.getByLabel(en.controllerRules,{exact:true}).locator('option')).toHaveCount(2);
  await expect(inspected.getByRole('button',{name:en.ruleAdd,exact:true})).toBeDisabled();await expect(inspected.getByRole('button',{name:en.ruleClear,exact:true})).toBeDisabled();
 }finally{await context.close();}
 dialog=await openRules();await dialog.getByRole('button',{name:text.ruleClear,exact:true}).click();await expect(dialog).toContainText(text.ruleClearHelp);
 const prior=await(await page.request.get('/api/saves/download')).body();await dialog.getByRole('button',{name:text.cancel,exact:true}).click();expect(await(await page.request.get('/api/saves/download')).body()).toEqual(prior);
 await dialog.getByRole('button',{name:text.close,exact:true}).click();
 await controls.getByRole('button',{name:text.controllerCopy,exact:true}).click();let copyDialog=page.getByRole('dialog',{name:text.controllerCopy,exact:true});await copyDialog.getByRole('button',{name:text.controllerApply,exact:true}).click();await expect(copyDialog).toHaveCount(0);
 const copy=(await state(page)).mixed.fleet.controllers.find((c:any)=>c.name==='Peer ordered TAT 1');expect(copy.harbors).toEqual((await data()).harbors);expect(copy.junctions).toEqual((await data()).junctions);
 dialog=await openRules();await dialog.getByRole('button',{name:text.ruleClear,exact:true}).click();await apply();expect((await data()).harbors[0].rules).toBeUndefined();
 await dialog.getByRole('button',{name:text.close,exact:true}).click();
 const after=await state(page);expect(after.tick).toBe(baseline.tick);expect(after.cash).toBe(baseline.cash);expect(after.mixed.queue).toEqual(baseline.mixed.queue);expect(after.mixed.fleet.trains).toEqual(baseline.mixed.fleet.trains);
 const bytes=await(await page.request.get('/api/saves/download')).body();dialog=await openRules();
 expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+after.worldEpoch,{data:bytes})).ok()).toBe(true);
 await expect(dialog).toHaveCount(0);await page.goto('/');await ready(page,locale);
 const reopened=(await state(page)).mixed.fleet.controllers.find((c:any)=>c.id===copy.id);expect(reopened.harbors[0].rules.map((r:any)=>r.action)).toEqual(['stop','go']);expect(reopened.junctions[0].rules[0].minutes).toBe(-1);
 await closePanel(page);await worldServer.restart!();expect((await page.request.post('/api/access',{data:{secret:''}})).ok()).toBe(true);await page.reload();await ready(page,locale);
 expect((await state(page)).mixed.fleet.controllers.find((c:any)=>c.id===copy.id).harbors).toEqual(reopened.harbors);expect(errors).toEqual([]);
});

for(const locale of ['en','ja'] as const)test(`${locale} TURNING actions edit, depart with exact car identities and survive checkpoint restart`,async({page,worldServer})=>{
 test.setTimeout(90000);const text=locale==='ja'?ja:en,errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 const initial=await state(page),input=readFileSync('../tmp/p5-21-turning-fleet.json');
 expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+initial.worldEpoch,{data:input})).ok()).toBe(true);
 await page.goto('/');await ready(page);
 if(locale==='ja'){await openPanel(page,'settings');await page.getByLabel('Language',{exact:true}).selectOption('ja');await closePanel(page);await page.setViewportSize({width:390,height:844});await page.evaluate(()=>document.documentElement.style.fontSize='150%');}
 const baseline=await state(page),source=baseline.mixed.fleet.controllers.find((c:any)=>c.name==='Shared TAT'),train=baseline.mixed.fleet.trains[0];
 const controls=await manager(page,text,locale);await controls.getByRole('combobox',{name:text.controllerManager,exact:true}).selectOption(source.id);
 async function openRules(){
  await controls.getByRole('button',{name:text.controllerConfigure,exact:true}).click();
  await page.getByRole('dialog',{name:text.controllerConfigure,exact:true}).getByRole('button',{name:text.controllerRules,exact:true}).click();
  return page.getByRole('dialog',{name:text.controllerRules,exact:true});
 }
 let dialog=await openRules();await dialog.getByLabel(text.controllerRules,{exact:true}).selectOption('0');
 await dialog.getByRole('button',{name:text.ruleEdit,exact:true}).click();
 const action=dialog.getByLabel(text.ruleAction,{exact:true});await expect(action).toHaveValue('reverse2');
 await expect(action.locator('option[value="reverse2"]')).toHaveText(text.ruleTurningReverse);
 await expect(action.locator('option[value="rforwarding2"]')).toHaveText(text.ruleTurningForwarding);
 await action.selectOption('rforwarding2');
 await action.scrollIntoViewIfNeeded();
 mkdirSync(evidence+'/turning',{recursive:true});await page.screenshot({path:evidence+`/turning/${locale}-editor.png`});
 await dialog.getByRole('button',{name:text.cancel,exact:true}).click();
 expect((await state(page)).mixed.fleet.controllers.find((c:any)=>c.id===source.id).harbors[0].rules[0].action).toBe('reverse2');
 await dialog.getByRole('button',{name:text.ruleEdit,exact:true}).click();await action.selectOption(locale==='en'?'reverse2':'rforwarding2');
 await dialog.getByRole('button',{name:text.controllerApply,exact:true}).click();await expect(dialog.getByRole('button',{name:text.controllerApply,exact:true})).toHaveCount(0);
 await dialog.getByRole('button',{name:text.close,exact:true}).click();await closePanel(page);
 const bytes=await(await page.request.get('/api/saves/download')).body();
 expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+(await state(page)).worldEpoch,{data:bytes})).ok()).toBe(true);
 await page.goto('/');await ready(page,locale);
 const canvas=page.locator('canvas.world-canvas');
 for(let n=0;n<10;n++){
  await expect(page.locator('button.step')).toBeEnabled();
  const before=await state(page);await canvas.focus();await canvas.press('n');
  await expect.poll(async()=>Number((await state(page)).tick)).toBe(Number(before.tick)+1);
 }
 const current=await state(page),after=current.mixed.fleet.trains.find((t:any)=>t.id===train.id);
 expect(after.cars.map((c:any)=>c.id)).toEqual(train.cars.map((c:any)=>c.id));
 expect(after.cars.map((c:any)=>c.state.location.x)).toEqual([17,18,19]);
 expect(after.cars.map((c:any)=>c.state.heading)).toEqual([6,6,6]);
 expect(after.isReversed).toBe(false);expect(after.forwarding).toBe(locale==='ja');expect(after.moveCount).toBe(1);expect(after.stopCallCount).toBe(0);
 expect(current.mixed.queue.pending.filter((a:any)=>a.handlerId==='train/'+train.id)).toHaveLength(1);
 expect(current.mixed.queue.pending.find((a:any)=>a.handlerId==='train/'+train.id).dueMinute).toBe('214');
 await page.screenshot({path:evidence+`/turning/${locale}-departed.png`});
 await worldServer.restart!();expect((await page.request.post('/api/access',{data:{secret:''}})).ok()).toBe(true);await page.reload();await ready(page,locale);
 expect((await state(page)).mixed.fleet.trains.find((t:any)=>t.id===train.id)).toEqual(after);expect(errors).toEqual([]);
});

test('canonical train panels expose automatic controllers without driving controls or key input',async({page})=>{
 await setup(page);const commands:string[]=[];
 await page.routeWebSocket('**/api/session?*',route=>{const server=route.connectToServer();route.onMessage(message=>{
  const data=JSON.parse(String(message));if(data.type==='command')commands.push(data.payload.kind);server.send(message);
 });});
 await page.reload();await ready(page);const controls=await manager(page);
 await controls.getByRole('button',{name:en.controllerCreate,exact:true}).click();
 const dialog=page.getByRole('dialog',{name:en.controllerCreate,exact:true});
 expect(await dialog.getByLabel(en.controllerType,{exact:true}).locator('option').allTextContents()).toEqual([en.controllerTimetable]);
 await dialog.getByRole('button',{name:en.cancel,exact:true}).click();
 const details=await owned(page);await page.locator('.fleet-owned .catalog-item-main').first().click();
 await expect(details.getByRole('combobox',{name:en.sharedController,exact:true})).toBeVisible();
 await expect(page.locator('.fleet-manual')).toHaveCount(0);
 for(const name of ['Take control','Release control'])await expect(page.getByRole('button',{name,exact:true})).toHaveCount(0);
 await closePanel(page);const canvas=page.locator('canvas.world-canvas');await canvas.focus();
 for(const key of ['ArrowUp','ArrowDown','ArrowLeft','ArrowRight','Shift'])await canvas.press(key);
 await page.waitForTimeout(1200);
 expect(commands.filter(kind=>['acquireControl','releaseControl','setController','controllerInput'].includes(kind))).toEqual([]);
});
