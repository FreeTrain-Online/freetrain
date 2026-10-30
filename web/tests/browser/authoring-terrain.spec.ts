import {test,expect,type Page} from '@playwright/test';
import {ready,openPanel,closePanel,canonicalState} from './ux-helpers.ts';
import {mkdirSync} from 'node:fs';
import {join} from 'node:path';
const state=canonicalState;
const save=async(page:Page)=>(await page.request.get('/api/saves/download')).body();
async function replace(page:Page,bytes:Buffer){const s=await state(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:bytes})).ok()).toBe(true);}
async function empty(page:Page,locale='en'){
 const s=await state(page),r=await page.request.post('/api/worlds/empty/preview',{data:{name:'Map authoring',dimensions:{h:30,v:42,z:6},waterLevel:1,epoch:s.worldEpoch,revision:s.revision}});expect(r.ok()).toBe(true);const c=await r.json();
 expect((await page.request.post('/api/worlds/empty/commit',{data:{id:c.id,requestHash:c.requestHash,candidateHash:c.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).ok()).toBe(true);
 await expect(page.locator('canvas')).toHaveAttribute('data-ruleset','terrain-state-1');await ready(page,locale);
}
async function tool(page:Page,locale='en'){await page.getByRole('button',{name:locale==='ja'?'地形操作':'Modify terrain',exact:true}).click();}
async function toggle(page:Page,enable=true,locale='en'){
 await page.getByRole('button',{name:locale==='ja'?(enable?'マップ作成を開始':'マップ作成を終了'):(enable?'Enter map authoring':'Exit map authoring'),exact:true}).click();
 await page.getByRole('button',{name:locale==='ja'?'モード変更を確定':'Confirm mode change',exact:true}).click();
 await expect.poll(async()=>(await state(page)).terrain.constructionMode).toBe(enable);
}
async function author(page:Page,locale='en') {await page.getByLabel(locale==='ja'?'地形の選択方法':'Terrain selection',{exact:true}).selectOption('authoring');}
async function target(page:Page,action='plantForest',locale='en',start=[20,20,1],end=[22,22]){
 await page.getByLabel(locale==='ja'?'地形の操作':'Terrain action',{exact:true}).selectOption(action);
 for(const [label,n] of [[locale==='ja'?'隅のセル X':'Corner cell X',start[0]],[locale==='ja'?'隅のセル Y':'Corner cell Y',start[1]],[locale==='ja'?'隅のセル Z':'Corner cell Z',start[2]],[locale==='ja'?'終点 X':'End X',end[0]],[locale==='ja'?'終点 Y':'End Y',end[1]]] as const)await page.getByLabel(label,{exact:true}).fill(String(n));
 await page.getByRole('button',{name:locale==='ja'?'景観をプレビュー':'Preview scenery',exact:true}).click();
}
const confirm=(page:Page,locale='en')=>page.getByRole('button',{name:locale==='ja'?'地形の変更を確定':'Confirm terrain change',exact:true});
test('Owner mode confirmation, exact forest preview, save/reload, creek route and exit preserve scenery and canvas',async({page})=>{
 await page.goto('/');await ready(page);const original=await save(page);
 try{
  await empty(page);await tool(page);const before=await save(page);const canvas=page.locator('canvas');await canvas.evaluate(el=>el.setAttribute('data-instance','authoring'));
  await page.getByRole('button',{name:'Enter map authoring',exact:true}).click();expect(await save(page)).toEqual(before);
  await page.getByRole('button',{name:'Cancel preview',exact:true}).click();expect(await save(page)).toEqual(before);
  await toggle(page);await author(page);await target(page);await expect(confirm(page)).toBeEnabled();const preview=page.waitForResponse(r=>r.url().includes('/api/terrain/corner/preview'));
  await target(page);const predicted=(await(await preview).json()).decorations;const untouched=await save(page);await expect(page.getByTestId('authoring-result')).toContainText('Trees:');expect(await save(page)).toEqual(untouched);
  await confirm(page).click();await expect.poll(async()=>(await state(page)).terrain.occupants.length).toBe(predicted.length);
  const current=await state(page);for(const d of predicted){const entity=current.entities.find((e:any)=>e.anchor.x===d.position.x&&e.anchor.y===d.position.y);expect(current.terrain.occupants.find((o:any)=>o.entityId===entity.id).forest).toEqual(d.forest);expect(entity.owned).toBe(false);}
  const forest=await save(page);await page.reload();await ready(page);expect(await save(page)).toEqual(forest);await tool(page);await expect(page.getByTestId('authoring-mode')).toContainText('is on');await author(page);
  await target(page,'placeCreek','en',[23,20,1],[23,22]);await expect(confirm(page)).toBeEnabled();const evidence=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'authoring-terrain');mkdirSync(evidence,{recursive:true});await page.screenshot({path:join(evidence,'authoring-creek-en.png'),fullPage:true});await confirm(page).click();await expect.poll(async()=>(await state(page)).terrain.occupants.filter((o:any)=>o.kind==='creek').length).toBe(3);
  const authored=await state(page);await toggle(page,false);const ended=await state(page);expect(ended.entities).toEqual(authored.entities);expect(ended.terrain.occupants).toEqual(authored.terrain.occupants);expect(ended.terrain.waterLevel).toBe(1);expect(ended.paused).toBe(authored.paused);
  // The normal tools remain available after leaving mode.
  await page.getByLabel('Terrain selection',{exact:true}).selectOption('rectangle');await expect(page.getByRole('button',{name:'Preview rectangle',exact:true})).toBeEnabled();
 }finally{await replace(page,original);}
});
test('mode changes invalidate peer previews and delayed authoring responses; panels and Escape clear targeting',async({page,browser})=>{
 await page.goto('/');await ready(page);const original=await save(page),peer=await browser.newPage();
 try{
  await empty(page);await tool(page);await toggle(page);await author(page);await target(page);await expect(confirm(page)).toBeEnabled();
  await page.keyboard.press('Escape');await expect(confirm(page)).toBeDisabled();await target(page);await openPanel(page,'help');await closePanel(page);await expect(confirm(page)).toBeDisabled();
  await author(page);await target(page);await peer.goto('/');await ready(peer);await tool(peer);await toggle(peer,false);await expect(confirm(page)).toBeDisabled();
  await toggle(peer,true);await author(page);
  let release!:()=>void;const waiting=new Promise<void>(r=>release=r);await page.route('**/api/terrain/corner/preview?**',async route=>{const response=await route.fetch();await waiting;await route.fulfill({response});});
  const received=page.waitForResponse(r=>r.url().includes('/api/terrain/corner/preview'));await target(page);await toggle(peer,false);release();void received.catch(()=>{});await expect(confirm(page)).toBeDisabled();
  expect((await state(page)).terrain.occupants).toEqual([]);
 }finally{await peer.close();await replace(page,original);}
});
test('Japanese narrow authoring uses keyboard confirmation and all fields stay reachable',async({browser})=>{
 const context=await browser.newContext({viewport:{width:390,height:844},locale:'ja-JP',deviceScaleFactor:2}),page=await context.newPage();await page.goto('/');await ready(page,'ja');const original=await save(page);
 try{
  await empty(page,'ja');await tool(page,'ja');await toggle(page,true,'ja');await author(page,'ja');await target(page,'plantForest','ja');await expect(confirm(page,'ja')).toBeEnabled();
  await page.getByLabel('樹種',{exact:true}).selectOption('ginkgo');await page.getByLabel('森林の地面表示',{exact:true}).selectOption('grass');
  await page.getByRole('button',{name:'景観をプレビュー',exact:true}).click();await expect(confirm(page,'ja')).toBeEnabled();expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  const path=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'authoring-terrain');mkdirSync(path,{recursive:true});await page.screenshot({path:join(path,'authoring-ja-narrow.png'),fullPage:true});
  await confirm(page,'ja').focus();await page.keyboard.press('Enter');await expect.poll(async()=>(await state(page)).terrain.occupants.length).toBeGreaterThan(0);expect((await state(page)).paused).toBe(true);
 }finally{await replace(page,original);await context.close();}
});
test('Operator authors scenery in the Owner mode; Observer only previews and mode exit disables Operator drafts',async({page,browser})=>{
 await page.goto('/');await ready(page);const original=await save(page);const operator=await browser.newPage(),observer=await browser.newPage();
 try{
  await empty(page);await tool(page);await toggle(page);
  expect((await operator.request.post('/api/access',{data:{secret:'operator-test'}})).ok()).toBe(true);await operator.goto('/');await ready(operator);await tool(operator);
  await expect(operator.getByRole('button',{name:'Exit map authoring',exact:true})).toHaveCount(0);await author(operator);await target(operator);await expect(confirm(operator)).toBeEnabled();await confirm(operator).click();
  await expect.poll(async()=>(await state(page)).terrain.occupants.length).toBeGreaterThan(0);
  expect((await observer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);await observer.goto('/');await ready(observer);await expect(observer.getByRole('button',{name:'Modify terrain',exact:true})).toBeDisabled();
  const s=await state(page);const q={expectedWorldEpoch:s.worldEpoch,expectedGeometryRevision:s.terrain.geometryRevision,position:{x:23,y:20,z:1},action:'placeCreek',offsetX:null,offsetY:null,stroke:null,rectangle:{end:{x:23,y:22,z:1},soft:false}};
  expect((await observer.request.post('/api/terrain/corner/preview?protocolVersion=5&contentVersion=terrain-content-1',{data:q})).ok()).toBe(true);
  await target(operator,'placeCreek','en',[23,20,1],[23,22]);await expect(confirm(operator)).toBeEnabled();await toggle(page,false);await expect(confirm(operator)).toBeDisabled();
  await expect(operator.getByRole('button',{name:'Preview scenery',exact:true})).toBeDisabled();
 }finally{await operator.close();await observer.close();await replace(page,original);}
});
