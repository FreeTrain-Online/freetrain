import { pickStationObject as pick } from './station-picking.ts';
import { assertExact } from './assertions.ts';
import {readFileSync,writeFileSync} from 'node:fs';
import {test,expect,type Page} from './fixtures.ts';
import { canonicalState as state, ready, closePanel, openPanel } from './browser-helpers.ts';
import {fit} from './layout-helpers.ts';
import {assertSaveBytes,evidencePath} from './assertions.ts';

const en=JSON.parse(readFileSync('src/locales/en/app.json','utf8'));
const ja=JSON.parse(readFileSync('src/locales/ja/app.json','utf8'));
const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
const bytes=async(p:Page)=>(await p.request.get('/api/saves/download')).body();
test.use({hasTouch:true,actionTimeout:15000});

for(const locale of ['en','ja'] as const)test(`P6-22 ${locale} picked hosts share TAT settings, retain rule files and run real group reassignment with exact peers/restart`,async({page,browser,baseURL,worldServer,sessions})=>{
 test.setTimeout(240000);const text=locale==='ja'?ja:en,touch=locale==='ja';
 if(touch)await page.setViewportSize({width:430,height:860});
 await page.addInitScript(l=>localStorage.setItem('freetrain-online.locale',l),locale);
 const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 const context=await browser.newContext({baseURL}),peer=await context.newPage();
 const owner=await sessions.observe(page,'owner'),observer=await sessions.observe(peer,'operator');
 const exact=async(actual:Uint8Array,expected:Uint8Array,phase:string)=>assertSaveBytes(actual,expected,{phase,peer:'owner'});
 async function consistent(){return test.step("Converge on one paused peer checkpoint",async()=>{return sessions.checkpoint(owner,'controller peer checkpoint');});}
 async function upload(data:Buffer){return test.step("Replace the paused save",async()=>{await sessions.replaceSave(owner,data,'controller save replacement');await ready(page,locale);await ready(peer);});}
 async function step(){return test.step("Advance one simulation minute",async()=>{return sessions.minute(owner,async()=>{const canvas=page.locator('canvas.world-canvas');await canvas.focus();await canvas.press('n');},'controller single minute');});}
 async function until(predicate:(s:any)=>boolean,limit=160){return test.step("Continue to the source callback boundary",async()=>{let s=await state(page);for(let n=0;!predicate(s)&&n<limit;n++)s=await step();expect(predicate(s)).toBe(true);return s;});}
 async function manager(){await openPanel(page,'fleet',locale);await page.getByRole('button',{name:text.controllerManager,exact:true}).click();return page.locator('.fleet-controller-manager');}
 let id:string;
 const controller=async()=>(await state(page)).mixed.fleet.controllers.find((c:any)=>c.id===id);
 async function configure(object:any){return test.step("Open the controller editor",async()=>{await pick(page,object,text,touch,true);const controls=page.locator('.primary-panel .fleet-controller-manager');await controls.getByLabel(text.controllerManager,{exact:true}).selectOption(id);await controls.getByRole('button',{name:text.controllerConfigure,exact:true}).click();return page.getByRole('dialog',{name:text.controllerConfigure,exact:true});});}
 async function apply(dialog:any){return test.step("Apply the controller change",async()=>{await owner.acceptedAction(()=>dialog.getByRole('button',{name:text.controllerApply,exact:true}).click());await expect(dialog).toHaveCount(0);await consistent();});}
 try{
  await page.goto('/');await ready(page,locale);
  expect((await peer.request.post('/api/access',{data:{secret:'operator-test'}})).ok()).toBe(true);await peer.goto('/');await ready(peer);
  await upload(readFileSync('tests/fixtures/station-controller-v11.json'));
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
  await expect(rules.getByRole('list',{name:text.ruleFilePreview,exact:true}).getByRole('listitem')).toHaveCount(9);await exact(await bytes(page),before,'rule preview unchanged');
  await owner.acceptedAction(()=>rules.getByRole('button',{name:text.ruleFileAppend,exact:true}).click());await expect(rules.getByRole('button',{name:text.ruleFileAppend,exact:true})).toHaveCount(0);
  await assertExact((await controller()).harbors.find((h:any)=>h.location.x===host.location.x).rules,JSON.parse(readFileSync('tests/fixtures/timetable/expected.json','utf8')),"(await controller()).harbors.find((h:any)=>h.location.x===host.location.x).rules");
  const downloadEvent=page.waitForEvent('download');await rules.getByRole('button',{name:text.ruleFileSave,exact:true}).click();const download=await downloadEvent;
  expect(download.suggestedFilename()).toBe('diagram.ftdd.json');expect(JSON.parse(readFileSync((await download.path())!,'utf8')).rules).toHaveLength(9);
  await rules.getByRole('button',{name:text.close,exact:true}).click();
  const advancedSave=await bytes(page);await upload(advancedSave);await exact(await bytes(page),advancedSave,'advanced rules replacement');
  expect((await controller()).harbors.find((h:any)=>h.location.x===host.location.x).rules).toHaveLength(9);
  dialog=await configure(platforms[0]);await dialog.getByLabel(text.controllerStop,{exact:true}).selectOption('fixed');await dialog.getByLabel(text.controllerMinutes,{exact:true}).fill('40');await apply(dialog);
  // Assign the real train's group. Its delegation resolves again on departure.
  let management=await manager();const f=(await state(page)).mixed.fleet,simple=f.controllers.find((c:any)=>c.kind==='simple');
  await owner.acceptedAction(()=>management.locator('.fleet-group-manager').getByLabel(text.sharedController,{exact:true}).selectOption(id));await consistent();await closePanel(page);
  s=await until(s=>s.mixed.fleet.trains[0].stopCallCount===1);
  const arrival=BigInt(s.tick),train=s.mixed.fleet.trains[0];
  expect(BigInt(s.mixed.queue.pending.find((a:any)=>a.handlerId==='train/'+train.id).dueMinute)-arrival).toBe(40n);
  management=await manager();await owner.acceptedAction(()=>management.locator('.fleet-group-manager').getByLabel(text.sharedController,{exact:true}).selectOption(simple.id));await consistent();await closePanel(page);
  const waiting=await bytes(page);s=await until(s=>s.mixed.fleet.trains[0].stopCallCount===0);const departed=await bytes(page),departureTick=s.tick;
  expect(s.mixed.fleet.trains[0].passengers).toBeGreaterThan(0);
  await upload(waiting);await exact(await bytes(page),waiting,'waiting checkpoint');await sessions.restartCheckpoint(worldServer,[{session:owner},{session:observer,secret:'operator-test'}],'controller checkpoint restart');
  await ready(page,locale);await ready(peer);await exact(await bytes(page),waiting,'restarted waiting checkpoint');
  await until(s=>s.tick===departureTick);await exact(await bytes(page),departed,'independent departure continuation');
  // Reassign to TAT while moving: the other real station's Pass entry is used.
  management=await manager();await owner.acceptedAction(()=>management.locator('.fleet-group-manager').getByLabel(text.sharedController,{exact:true}).selectOption(id));await consistent();await closePanel(page);
  const loaded=(await state(page)).mixed.fleet.trains[0].passengers;
  s=await until(s=>s.mixed.fleet.trains[0].cars[0].state.location.x>39);
  expect(s.mixed.fleet.trains[0].passengers).toBe(loaded);expect(s.mixed.stationGraph.buildings.find((b:any)=>b.id===other.id).trains.today).toBe(0);
  dialog=await configure(host);await expect(dialog.getByLabel(text.controllerMinutes,{exact:true})).toHaveValue('40');await fit(page);
  const screenshot=evidencePath(test.info(),`station-controller-${locale}`,'png');await page.screenshot({path:screenshot});await test.info().attach('station-controller',{path:screenshot,contentType:'image/png'});
  // A peer removes the real host while a local settings draft is open. The
  // picked editor disappears; the controller retains its committed location.
  await dialog.getByLabel(text.controllerMinutes,{exact:true}).fill('90');
  if(!host)throw new Error('Missing controller host');await pick(peer,host,en,false,true);await peer.getByRole('button',{name:en.stationRemove,exact:true}).click();
  await observer.acceptedAction(()=>peer.getByRole('button',{name:en.confirmRemoval,exact:true}).click());
  await expect.poll(async()=>(await state(page)).mixed.stationGraph.buildings.some((b:any)=>b.id===host.id)).toBe(false);
  await expect(dialog).toHaveCount(0);expect((await controller()).harbors.find((h:any)=>h.location.x===host.location.x).minutes).toBe(40);await consistent();
  await closePanel(page);await page.getByRole('button',{name:text.stationTask,exact:true}).click();
  if(!await page.locator('#building-catalog').count())await page.getByRole('button',{name:text.chooseFromCatalog,exact:true}).click();
  await page.locator(`[data-catalog-variant="${host.definition.recipeId}"]`).click();await page.getByRole('button',{name:text.closeCatalog,exact:true}).click();
  for(const [label,value] of [[text.stationX,host.location.x],[text.stationY,host.location.y],[text.stationLevel,host.location.z]] as const)await page.getByLabel(label,{exact:true}).fill(String(value));
  await page.locator('.station-task form').getByRole('button',{name:text.stationCheck,exact:true}).click();await owner.acceptedAction(()=>page.getByRole('button',{name:text.stationConfirm,exact:true}).click());
  await expect.poll(async()=>(await state(page)).mixed.stationGraph.buildings.length).toBe(2);
  const rebuilt=(await state(page)).mixed.stationGraph.buildings.find((b:any)=>b.location.x===host.location.x);expect(rebuilt.id).not.toBe(host.id);
  for(const [i,platform] of platforms.slice(0,2).entries()){
   await pick(page,platform,text,touch,false);await expect(page.getByTestId('platform-disconnected')).toBeVisible();await expect(page.locator('.primary-panel .fleet-controller-manager')).toHaveCount(0);
   const select=page.getByLabel(text.platformHost,{exact:true});await expect(select).toBeEnabled();await select.selectOption(i===0?rebuilt.id:platforms[0].id);
   await owner.acceptedAction(()=>page.getByRole('button',{name:text.platformApply,exact:true}).click());await expect(page.locator('.primary-panel .fleet-controller-manager')).toBeVisible();await consistent();
  }
  dialog=await configure(platforms[1]);await expect(dialog.getByLabel(text.controllerMinutes,{exact:true})).toHaveValue('40');await dialog.getByRole('button',{name:text.cancel,exact:true}).click();
  writeFileSync(evidencePath(test.info(),`station-controller-${locale}`,'json'),JSON.stringify({host,other,controller:await controller(),state:await consistent()},null,2));
  expect(errors).toEqual([]);
 }finally{await context.close();}
});
