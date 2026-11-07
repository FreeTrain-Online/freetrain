import {test,expect,type Page} from './fixtures.ts';
import {canonicalState as state,openPanel,closePanel,ready} from './browser-helpers.ts';
import {readFileSync,mkdirSync} from 'node:fs';
const en=JSON.parse(readFileSync('src/locales/en/app.json','utf8'));
const ja=JSON.parse(readFileSync('src/locales/ja/app.json','utf8'));
const evidence=process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!;
async function manager(page:Page,text=en,locale='en'){
 await openPanel(page,'fleet',locale);await page.getByRole('button',{name:text.controllerManager,exact:true}).click();
 return page.locator('.fleet-group-manager');
}
async function setup(page:Page,locale='en'){
 const initial=await state(page),input=readFileSync(process.env.FREETRAIN_CONTROLLER_FLEET_OUTPUT??'../tmp/p5-18-manager-fleet.json');
 expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+initial.worldEpoch,{data:input})).ok()).toBe(true);
 await page.goto('/');await ready(page);
 if(locale==='ja'){await openPanel(page,'settings');await page.getByLabel('Language',{exact:true}).selectOption('ja');await closePanel(page);}
}
for(const locale of ['en','ja'] as const)test.describe(locale,()=>{
 test.use({hasTouch:locale==='ja'});
 test(`${locale} MAN-A17 nested group reassignment, overrides and durable reopen`,async({page,worldServer})=>{
 test.setTimeout(90000);const text=locale==='ja'?ja:en,errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 await setup(page,locale);const initial=await state(page),f=initial.mixed.fleet;
 if(locale==='ja'){await page.setViewportSize({width:390,height:844});await page.evaluate(()=>document.documentElement.style.fontSize='200%');}
 const groups=await manager(page,text,locale),trainControls=groups.locator('.fleet-group-trains');
 const selectGroup=groups.getByRole('combobox',{name:text.controllerGroup,exact:true});
 const apply=async()=>{const dialog=page.locator('dialog.decision-dialog');await dialog.getByRole('button',{name:text.controllerApply,exact:true}).click();await expect(dialog).toHaveCount(0);};
 async function create(name:string){
  const button=groups.getByRole('button',{name:text.groupCreate,exact:true});if(locale==='ja')await button.tap();else await button.click();const dialog=page.getByRole('dialog',{name:text.groupCreate,exact:true});
  await dialog.getByLabel(text.groupName,{exact:true}).fill(name);await apply();return (await state(page)).mixed.fleet.groups.at(-1);
 }
 const a=await create('本線・Main');await selectGroup.selectOption(a.id);const b=await create('支線・Branch');
 await selectGroup.selectOption(f.rootGroupId);const c=await create('Other');
 const timetable=f.controllers.find((c:any)=>c.kind==='timetable'),simple=f.controllers.find((c:any)=>c.kind==='simple'),delegation=f.controllers.find((c:any)=>c.kind==='delegation');
 await selectGroup.selectOption(a.id);
 await groups.getByRole('combobox',{name:text.sharedController,exact:true}).first().selectOption(timetable.id);
 await expect(groups.getByTestId('effective-controller').first()).toContainText('#'+timetable.id);
 await trainControls.getByRole('combobox',{name:text.groupTrain,exact:true}).selectOption(f.trains[0].id);
 await trainControls.getByRole('button',{name:text.groupMove,exact:true}).click();let dialog=page.getByRole('dialog',{name:text.groupMove,exact:true});
 await dialog.getByLabel(text.groupParent,{exact:true}).selectOption(b.id);await apply();
 await expect(trainControls.getByRole('combobox',{name:text.sharedController,exact:true})).toHaveValue(timetable.id);
 await trainControls.getByRole('combobox',{name:text.sharedController,exact:true}).selectOption(delegation.id);
 await expect(trainControls.getByTestId('effective-controller')).toContainText('#'+timetable.id);
 await groups.getByRole('combobox',{name:text.sharedController,exact:true}).first().selectOption(simple.id);
 await expect(trainControls.getByTestId('effective-controller')).toContainText('#'+simple.id);
 // Move an entire subtree. A descendant and the current parent cannot be chosen.
 await selectGroup.selectOption(a.id);await groups.getByTestId('group-actions').getByRole('button',{name:text.groupMove,exact:true}).click();
 dialog=page.getByRole('dialog',{name:text.groupMove,exact:true});
 const destinations=dialog.getByLabel(text.groupParent,{exact:true});await expect(destinations.locator(`option[value="${b.id}"]`)).toHaveCount(0);
 await expect(destinations.locator(`option[value="${a.id}"]`)).toHaveCount(0);await expect(destinations.locator(`option[value="${f.rootGroupId}"]`)).toHaveCount(0);
 await destinations.selectOption(c.id);mkdirSync(evidence+'/groups',{recursive:true});await page.screenshot({path:evidence+`/groups/${locale}-move.png`});await apply();
 await groups.getByRole('combobox',{name:text.sharedController,exact:true}).first().selectOption(delegation.id);
 await selectGroup.selectOption(c.id);await groups.getByRole('combobox',{name:text.sharedController,exact:true}).first().selectOption(timetable.id);
 await expect(trainControls.getByTestId('effective-controller')).toContainText('#'+timetable.id);
 await trainControls.getByRole('combobox',{name:text.sharedController,exact:true}).selectOption(simple.id);
 await groups.getByRole('combobox',{name:text.sharedController,exact:true}).first().selectOption(simple.id);
 await expect(trainControls.getByTestId('effective-controller')).toContainText('#'+simple.id);
 await trainControls.getByRole('combobox',{name:text.sharedController,exact:true}).selectOption(delegation.id);
 await selectGroup.selectOption(b.id);await groups.getByRole('button',{name:text.groupRename,exact:true}).click();dialog=page.getByRole('dialog',{name:text.groupRename,exact:true});
 await dialog.getByLabel(text.groupName,{exact:true}).fill('夜行・Night');
 const before=await(await page.request.get('/api/saves/download')).body();await dialog.press('Escape');await expect(dialog).toHaveCount(0);
 expect(await(await page.request.get('/api/saves/download')).body()).toEqual(before);
 await groups.getByRole('button',{name:text.groupRename,exact:true}).focus();await page.keyboard.press('Enter');dialog=page.getByRole('dialog',{name:text.groupRename,exact:true});
 await dialog.getByLabel(text.groupName,{exact:true}).fill('夜行・Night');await apply();
 await expect(groups.getByRole('button',{name:text.groupRename,exact:true})).toBeFocused();
 const saved=await state(page);expect(saved.tick).toBe(initial.tick);expect(saved.cash).toBe(initial.cash);expect(saved.mixed.queue).toEqual(initial.mixed.queue);expect(saved.mixed.voxels).toEqual(initial.mixed.voxels);
 expect(saved.mixed.fleet.trains[0].groupId).toBe(b.id);expect(saved.mixed.fleet.groups.find((g:any)=>g.id===a.id).parentId).toBe(c.id);
 mkdirSync(evidence+'/groups',{recursive:true});await page.screenshot({path:evidence+`/groups/${locale}.png`});
 const bytes=await(await page.request.get('/api/saves/download')).body();expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+saved.worldEpoch,{data:bytes})).ok()).toBe(true);
 await page.goto('/');await ready(page,locale);expect((await state(page)).mixed.fleet).toEqual(saved.mixed.fleet);
 await worldServer.restart!();expect((await page.request.post('/api/access',{data:{secret:''}})).ok()).toBe(true);await page.reload();await ready(page,locale);
 expect((await state(page)).mixed.fleet).toEqual(saved.mixed.fleet);expect(errors).toEqual([]);
});
});

test('group drafts fence peer edits and world replacement; observer can browse',async({page,browser,baseURL})=>{
 await setup(page);const groups=await manager(page);
 await groups.getByRole('button',{name:en.groupCreate,exact:true}).click();let dialog=page.locator('dialog.decision-dialog');await dialog.getByLabel(en.groupName,{exact:true}).fill('Local draft');
 const context=await browser.newContext({baseURL}),peer=await context.newPage();
 try{
  expect((await peer.request.post('/api/access',{data:{secret:'operator-test'}})).ok()).toBe(true);await peer.goto('/');await ready(peer);
  const remote=await manager(peer);await remote.getByRole('button',{name:en.groupCreate,exact:true}).click();
  await peer.locator('dialog.decision-dialog').getByLabel(en.groupName,{exact:true}).fill('Peer group');await peer.locator('dialog.decision-dialog').getByRole('button',{name:en.controllerApply,exact:true}).click();
  await expect(dialog).toContainText(en.groupConflict);await expect(dialog.getByLabel(en.groupName,{exact:true})).toHaveValue('Local draft');await expect(dialog.getByRole('button',{name:en.controllerApply,exact:true})).toBeDisabled();
  await dialog.getByRole('button',{name:en.cancel,exact:true}).click();
  const root=(await state(page)).mixed.fleet.rootGroupId;await groups.getByRole('button',{name:en.groupRename,exact:true}).click();dialog=page.locator('dialog.decision-dialog');
  await remote.getByRole('button',{name:en.groupRename,exact:true}).click();await peer.locator('dialog.decision-dialog').getByLabel(en.groupName,{exact:true}).fill('Root renamed');await peer.locator('dialog.decision-dialog').getByRole('button',{name:en.controllerApply,exact:true}).click();
  await expect(dialog).toContainText(en.groupConflict);await dialog.getByRole('button',{name:en.cancel,exact:true}).click();
  const bytes=await(await page.request.get('/api/saves/download')).body();await groups.getByRole('button',{name:en.groupRename,exact:true}).click();dialog=page.locator('dialog.decision-dialog');
  const read=await state(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+read.worldEpoch,{data:bytes})).ok()).toBe(true);
  // World replacement may close the panel; an old draft must never commit.
  await expect.poll(async()=>{const current=await state(page);return current.worldEpoch!==read.worldEpoch;}).toBe(true);
  if(await dialog.count()){await expect(dialog.getByRole('button',{name:en.controllerApply,exact:true})).toBeDisabled();await dialog.getByRole('button',{name:en.cancel,exact:true}).click();}
  expect((await state(page)).mixed.fleet.groups.find((g:any)=>g.id===root).name).toBe('Root renamed');
 }finally{await context.close();}
 const observerContext=await browser.newContext({baseURL}),observer=await observerContext.newPage();
 try{
  expect((await observer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);await observer.goto('/');await ready(observer);const controls=await manager(observer);
  await expect(controls.getByRole('button',{name:en.groupCreate,exact:true})).toBeDisabled();await expect(controls.getByRole('button',{name:en.groupRename,exact:true})).toBeDisabled();
  await expect(controls.getByRole('combobox',{name:en.controllerGroup,exact:true})).toBeEnabled();
 }finally{await observerContext.close();}
});

test('nested groups can be created before the first train or named controller',async({page})=>{
 await page.goto('/');await ready(page);const initial=await state(page);expect(initial.mixed?.fleet).toBeUndefined();const groups=await manager(page);
 await groups.getByRole('button',{name:en.groupCreate,exact:true}).click();const dialog=page.locator('dialog.decision-dialog');
 await dialog.getByLabel(en.groupName,{exact:true}).fill('Before trains');await dialog.getByRole('button',{name:en.controllerApply,exact:true}).click();await expect(dialog).toHaveCount(0);
 const after=await state(page);expect(after.mixed.fleet.trains).toEqual([]);expect(after.mixed.fleet.groups).toHaveLength(2);expect(after.tick).toBe(initial.tick);expect(after.mixed.accounts.transactions).toEqual([]);
});
