import {readFileSync,writeFileSync} from 'node:fs';
import {test,expect,type Page} from './fixtures.ts';
import {canonicalState as state,ready,closePanel,openPanel,exposeMapPoint} from './browser-helpers.ts';
import {fit} from './layout-helpers.ts';
const en=JSON.parse(readFileSync('src/locales/en/app.json','utf8'));
const ja=JSON.parse(readFileSync('src/locales/ja/app.json','utf8'));
const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
const bytes=async(p:Page)=>(await p.request.get('/api/saves/download')).body();
test.use({hasTouch:true,actionTimeout:15000});

async function pick(page:Page,object:any,text:typeof en,touch:boolean,hasController=true){
 await closePanel(page);await page.getByRole('button',{name:text.referenceInspect,exact:true}).click();
 const canvas=page.locator('canvas.world-canvas');await expect(canvas).toHaveAttribute('data-presentation','ready');
 const recipe=object.kind?`system:thin-${object.direction}`:object.definition.recipeId;
 const frame=catalog.renderRecipes.find((r:any)=>r.id===recipe).frames.find((f:any)=>f.status==='ready');
 const mask=catalog.masks[frame.masks['spring/day']];let sample:any;
 for(let y=1;y<mask.length-1&&!sample;y++)for(let x=1;x<mask[y].length-1&&!sample;x++)
  if(mask.slice(y-1,y+2).every((r:string)=>r.slice(x-1,x+2)==='111'))sample={x:x+.5,y:y+.5};
 expect(sample).toBeTruthy();const center=Math.floor(((await state(page)).map.height-1)/2);
 const location=object.kind?{...object.location,x:object.location.x+object.length-1}:object.location;
 const q={x:16*(location.x+frame.cell[0]-center+location.y+frame.cell[1])-frame.anchor[0]+sample.x,
  y:8*(location.y+frame.cell[1]-location.x-frame.cell[0]+center)-16*(object.location.z+frame.cell[2])-frame.anchor[1]+sample.y};
 // Bring the exact painted opaque sample into view, then clear the narrow sheet.
 await canvas.dispatchEvent('wheel',await canvas.evaluate((el,q)=>{const [x,y]=el.dataset.camera!.split(',').map(Number),z=Number(el.dataset.zoom);return {deltaX:(q.x-x)*z,deltaY:(q.y-y)*z,deltaMode:0};},q));
 await page.evaluate(()=>new Promise<void>(r=>requestAnimationFrame(()=>requestAnimationFrame(()=>r()))));
 const point=await canvas.evaluate((el,q)=>{const r=el.getBoundingClientRect(),[x,y]=el.dataset.camera!.split(',').map(Number),z=Number(el.dataset.zoom);return {x:r.x+r.width/2+(q.x-x)*z,y:r.y+r.height/2+(q.y-y)*z};},q);
 const p=await exposeMapPoint(page,point);
 await page.evaluate(()=>new Promise<void>(r=>requestAnimationFrame(()=>requestAnimationFrame(()=>r()))));
 await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/pick-${object.id}.png`});
 writeFileSync(`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/pick-${object.id}.json`,JSON.stringify({object,q,p,camera:await canvas.getAttribute('data-camera')}));
 if(touch)await page.touchscreen.tap(p.x,p.y);else await page.mouse.click(p.x,p.y);
 if(hasController)await expect(page.locator('.primary-panel .fleet-controller-manager')).toBeVisible();
}

for(const locale of ['en','ja'] as const)test(`P6-22 ${locale} picked hosts share TAT settings, retain rule files and run real group reassignment with exact peers/restart`,async({page,browser,baseURL,worldServer})=>{
 test.setTimeout(240000);const text=locale==='ja'?ja:en,touch=locale==='ja';
 if(touch)await page.setViewportSize({width:430,height:860});
 await page.addInitScript(l=>localStorage.setItem('freetrain-online.locale',l),locale);
 const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 const context=await browser.newContext({baseURL}),peer=await context.newPage();
 const messages:any[][]=[[],[]];
 for(const [i,p] of [page,peer].entries())await p.routeWebSocket('**/api/session?*',route=>{
  const server=route.connectToServer();server.onMessage(m=>{messages[i].push(JSON.parse(String(m)));route.send(m);});route.onMessage(m=>server.send(m));
 });
 const latest=(i:number)=>{const m=messages[i].filter(m=>['join','snapshot','delta'].includes(m.type)).at(-1);return m?.snapshot??m;};
 async function consistent(){const s=await state(page);for(const i of [0,1])await expect.poll(()=>latest(i)?.mixed).toEqual(s.mixed);return s;}
 async function upload(data:Buffer){expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+(await state(page)).worldEpoch,{data})).ok()).toBe(true);await ready(page,locale);await ready(peer);await consistent();}
 async function step(){const tick=BigInt((await state(page)).tick)+1n;const canvas=page.locator('canvas.world-canvas');await canvas.focus();await canvas.press('n');await expect.poll(async()=>(await state(page)).tick).toBe(String(tick));return consistent();}
 async function until(predicate:(s:any)=>boolean,limit=160){let s=await state(page);for(let n=0;!predicate(s)&&n<limit;n++)s=await step();expect(predicate(s)).toBe(true);return s;}
 async function manager(){await openPanel(page,'fleet',locale);await page.getByRole('button',{name:text.controllerManager,exact:true}).click();return page.locator('.fleet-controller-manager');}
 let id:string;
 const controller=async()=>(await state(page)).mixed.fleet.controllers.find((c:any)=>c.id===id);
 async function configure(object:any){await pick(page,object,text,touch);const controls=page.locator('.primary-panel .fleet-controller-manager');await controls.getByLabel(text.controllerManager,{exact:true}).selectOption(id);await controls.getByRole('button',{name:text.controllerConfigure,exact:true}).click();return page.getByRole('dialog',{name:text.controllerConfigure,exact:true});}
 async function apply(dialog:any){await dialog.getByRole('button',{name:text.controllerApply,exact:true}).click();await expect(dialog).toHaveCount(0);await consistent();}
 try{
  await page.goto('/');await ready(page,locale);
  expect((await peer.request.post('/api/access',{data:{secret:'operator-test'}})).ok()).toBe(true);await peer.goto('/');await ready(peer);
  await upload(readFileSync('tests/fixtures/station-controller-v10.json'));
  let s=await consistent();const platforms=s.mixed.stationGraph.platforms,host=s.mixed.stationGraph.buildings.find((b:any)=>b.id===platforms[0].hostId),other=s.mixed.stationGraph.buildings.find((b:any)=>b.id===platforms[2].hostId);
  expect(platforms[1].hostId).toBe(host.id);expect(other.id).not.toBe(host.id);
  const controls=await manager();await controls.getByRole('button',{name:text.controllerCreate,exact:true}).click();
  let dialog=page.getByRole('dialog',{name:text.controllerCreate,exact:true});await dialog.getByLabel(text.controllerName,{exact:true}).fill('Station shared / 駅共有');await apply(dialog);
  id=(await state(page)).mixed.fleet.controllers.find((c:any)=>c.name==='Station shared / 駅共有').id;
  expect((await controller()).harbors).toEqual([]);
  // Map-picked platforms use the host location, never their rail coordinate.
  dialog=await configure(platforms[0]);
  for(const [axis,value] of [['X',host.location.x],['Y',host.location.y],['Z',host.location.z]] as const){await expect(dialog.getByLabel(axis,{exact:true})).toHaveValue(String(value));await expect(dialog.getByLabel(axis,{exact:true})).toBeDisabled();}
  await expect(dialog.getByLabel(text.controllerStop,{exact:true})).toHaveCount(0);
  await dialog.getByRole('button',{name:text.controllerLocation,exact:true}).click();await dialog.getByLabel(text.controllerMinutes,{exact:true}).fill('40');await apply(dialog);
  dialog=await configure(platforms[1]);await expect(dialog.getByLabel(text.controllerMinutes,{exact:true})).toHaveValue('40');await dialog.getByRole('button',{name:text.cancel,exact:true}).click();
  dialog=await configure(host);await expect(dialog.getByLabel(text.controllerMinutes,{exact:true})).toHaveValue('40');await dialog.getByRole('button',{name:text.cancel,exact:true}).click();
  dialog=await configure(platforms[2]);await dialog.getByRole('button',{name:text.controllerLocation,exact:true}).click();await dialog.getByLabel(text.controllerStop,{exact:true}).selectOption('pass');await apply(dialog);
  expect((await controller()).harbors.map((h:any)=>[h.location,h.kind,h.minutes])).toEqual([[host.location,'fixed',40],[other.location,'pass',0]]);
  // Same controller can retain absent-location settings. Removing and adding the
  // picked entry does not change the independent station's entry.
  dialog=await configure(platforms[1]);await dialog.getByRole('button',{name:text.controllerRemoveLocation,exact:true}).click();await apply(dialog);
  expect((await controller()).harbors).toHaveLength(1);
  dialog=await configure(platforms[0]);await dialog.getByRole('button',{name:text.controllerLocation,exact:true}).click();await dialog.getByLabel(text.controllerMinutes,{exact:true}).fill('40');await apply(dialog);
  // Read an actual legacy companion through this station's Advanced editor.
  dialog=await configure(platforms[0]);await dialog.getByLabel(text.controllerStop,{exact:true}).selectOption('advanced');await apply(dialog);
  dialog=await configure(platforms[1]);await dialog.getByRole('button',{name:text.controllerRules,exact:true}).click();
  const rules=page.getByRole('dialog',{name:text.controllerRules,exact:true}),before=await bytes(page);
  await rules.getByLabel(text.ruleFileRead,{exact:true}).setInputFiles({name:'station.ftdd',mimeType:'application/octet-stream',buffer:readFileSync('tests/fixtures/timetable/single.ftdd')});
  await expect(rules.getByRole('list',{name:text.ruleFilePreview,exact:true}).getByRole('listitem')).toHaveCount(9);expect(await bytes(page)).toEqual(before);
  await rules.getByRole('button',{name:text.ruleFileAppend,exact:true}).click();await expect(rules.getByRole('button',{name:text.ruleFileAppend,exact:true})).toHaveCount(0);
  expect((await controller()).harbors.find((h:any)=>h.location.x===host.location.x).rules).toEqual(JSON.parse(readFileSync('tests/fixtures/timetable/expected.json','utf8')));
  const downloadEvent=page.waitForEvent('download');await rules.getByRole('button',{name:text.ruleFileSave,exact:true}).click();const download=await downloadEvent;
  expect(download.suggestedFilename()).toBe('diagram.ftdd.json');expect(JSON.parse(readFileSync((await download.path())!,'utf8')).rules).toHaveLength(9);
  await rules.getByRole('button',{name:text.close,exact:true}).click();
  const advancedSave=await bytes(page);await upload(advancedSave);expect(await bytes(page)).toEqual(advancedSave);
  expect((await controller()).harbors.find((h:any)=>h.location.x===host.location.x).rules).toHaveLength(9);
  dialog=await configure(platforms[0]);await dialog.getByLabel(text.controllerStop,{exact:true}).selectOption('fixed');await dialog.getByLabel(text.controllerMinutes,{exact:true}).fill('40');await apply(dialog);
  // Assign the real train's group. Its delegation resolves again on departure.
  let management=await manager();const f=(await state(page)).mixed.fleet,simple=f.controllers.find((c:any)=>c.kind==='simple');
  await management.locator('.fleet-group-manager').getByLabel(text.sharedController,{exact:true}).selectOption(id);await consistent();await closePanel(page);
  s=await until(s=>s.mixed.fleet.trains[0].stopCallCount===1);
  const arrival=BigInt(s.tick),train=s.mixed.fleet.trains[0];
  expect(BigInt(s.mixed.queue.pending.find((a:any)=>a.handlerId==='train/'+train.id).dueMinute)-arrival).toBe(40n);
  management=await manager();await management.locator('.fleet-group-manager').getByLabel(text.sharedController,{exact:true}).selectOption(simple.id);await consistent();await closePanel(page);
  const waiting=await bytes(page);s=await until(s=>s.mixed.fleet.trains[0].stopCallCount===0);const departed=await bytes(page),departureTick=s.tick;
  expect(s.mixed.fleet.trains[0].passengers).toBeGreaterThan(0);
  await upload(waiting);expect(await bytes(page)).toEqual(waiting);await worldServer.restart!();
  for(const [i,p] of [page,peer].entries())expect((await p.request.post('/api/access',{data:{secret:i?'operator-test':''}})).ok()).toBe(true);
  await page.reload();await peer.reload();await ready(page,locale);await ready(peer);await consistent();expect(await bytes(page)).toEqual(waiting);
  await until(s=>s.tick===departureTick);expect(await bytes(page)).toEqual(departed);
  // Reassign to TAT while moving: the other real station's Pass entry is used.
  management=await manager();await management.locator('.fleet-group-manager').getByLabel(text.sharedController,{exact:true}).selectOption(id);await consistent();await closePanel(page);
  const loaded=(await state(page)).mixed.fleet.trains[0].passengers;
  s=await until(s=>s.mixed.fleet.trains[0].cars[0].state.location.x>39);
  expect(s.mixed.fleet.trains[0].passengers).toBe(loaded);expect(s.mixed.stationGraph.buildings.find((b:any)=>b.id===other.id).trains.today).toBe(0);
  dialog=await configure(host);await expect(dialog.getByLabel(text.controllerMinutes,{exact:true})).toHaveValue('40');await fit(page);
  await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/station-controller-${locale}.png`});
  // A peer removes the real host while a local settings draft is open. The
  // picked editor disappears; the controller retains its committed location.
  await dialog.getByLabel(text.controllerMinutes,{exact:true}).fill('90');
  await pick(peer,host,en,false);await peer.getByRole('button',{name:en.stationRemove,exact:true}).click();
  await peer.getByRole('button',{name:en.confirmRemoval,exact:true}).click();
  await expect.poll(async()=>(await state(page)).mixed.stationGraph.buildings.some((b:any)=>b.id===host.id)).toBe(false);
  await expect(dialog).toHaveCount(0);expect((await controller()).harbors.find((h:any)=>h.location.x===host.location.x).minutes).toBe(40);await consistent();
  await closePanel(page);await page.getByRole('button',{name:text.stationTask,exact:true}).click();
  if(!await page.locator('#building-catalog').count())await page.getByRole('button',{name:text.chooseFromCatalog,exact:true}).click();
  await page.locator(`[data-catalog-variant="${host.definition.recipeId}"]`).click();await page.getByRole('button',{name:text.closeCatalog,exact:true}).click();
  for(const [label,value] of [[text.stationX,host.location.x],[text.stationY,host.location.y],[text.stationLevel,host.location.z]] as const)await page.getByLabel(label,{exact:true}).fill(String(value));
  await page.locator('.station-task form').getByRole('button',{name:text.stationCheck,exact:true}).click();await page.getByRole('button',{name:text.stationConfirm,exact:true}).click();
  await expect.poll(async()=>(await state(page)).mixed.stationGraph.buildings.length).toBe(2);
  const rebuilt=(await state(page)).mixed.stationGraph.buildings.find((b:any)=>b.location.x===host.location.x);expect(rebuilt.id).not.toBe(host.id);
  for(const [i,platform] of platforms.slice(0,2).entries()){
   await pick(page,platform,text,touch,false);await expect(page.getByTestId('platform-disconnected')).toBeVisible();await expect(page.locator('.primary-panel .fleet-controller-manager')).toHaveCount(0);
   const select=page.getByLabel(text.platformHost,{exact:true});await expect(select).toBeEnabled();await select.selectOption(i===0?rebuilt.id:platforms[0].id);
   await page.getByRole('button',{name:text.platformApply,exact:true}).click();await expect(page.locator('.primary-panel .fleet-controller-manager')).toBeVisible();await consistent();
  }
  dialog=await configure(platforms[1]);await expect(dialog.getByLabel(text.controllerMinutes,{exact:true})).toHaveValue('40');await dialog.getByRole('button',{name:text.cancel,exact:true}).click();
  writeFileSync(`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/station-controller-${locale}.json`,JSON.stringify({host,other,controller:await controller(),state:await consistent()},null,2));
  expect(errors).toEqual([]);
 }finally{await context.close();}
});
