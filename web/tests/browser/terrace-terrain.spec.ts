import {test,expect,type Page} from '@playwright/test';
import {ready,openPanel,closePanel,canonicalState} from './ux-helpers.ts';
import {mkdirSync} from 'node:fs';
import {join} from 'node:path';
const compat='protocolVersion=5&contentVersion=terrain-content-1';
const state=canonicalState;
const save=async(page:Page)=>(await page.request.get('/api/saves/download')).body();
async function replace(page:Page,bytes:Buffer){const s=await state(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:bytes})).ok()).toBe(true);}
async function empty(page:Page,locale='en'){
 const s=await state(page);const response=await page.request.post('/api/worlds/empty/preview',{data:{name:'Terraces',dimensions:{h:30,v:42,z:6},waterLevel:1,epoch:s.worldEpoch,revision:s.revision}});expect(response.ok()).toBe(true);
 const c=await response.json();expect((await page.request.post('/api/worlds/empty/commit',{data:{id:c.id,requestHash:c.requestHash,candidateHash:c.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).ok()).toBe(true);
 await expect(page.locator('canvas')).toHaveAttribute('data-ruleset','terrain-state-1');await ready(page,locale);
}
async function tool(page:Page,locale='en'){
 await page.getByRole('button',{name:locale==='ja'?'地形操作':'Modify terrain',exact:true}).click();
 await page.getByLabel(locale==='ja'?'地形の選択方法':'Terrain selection',{exact:true}).selectOption('terrace');
}
async function target(page:Page,locale='en',action='raise',start=[20,20,1],end=[21,21]){
 await page.getByLabel(locale==='ja'?'地形の操作':'Terrain action',{exact:true}).selectOption(action);
 for(const [label,value] of [[locale==='ja'?'隅のセル X':'Corner cell X',start[0]],[locale==='ja'?'隅のセル Y':'Corner cell Y',start[1]],[locale==='ja'?'隅のセル Z':'Corner cell Z',start[2]],[locale==='ja'?'終点 X':'End X',end[0]],[locale==='ja'?'終点 Y':'End Y',end[1]]] as const)
  await page.getByLabel(label,{exact:true}).fill(String(value));
 await page.getByRole('button',{name:locale==='ja'?'矩形をプレビュー':'Preview rectangle',exact:true}).click();
}
const confirm=(page:Page,locale='en')=>page.getByRole('button',{name:locale==='ja'?'地形の変更を確定':'Confirm terrain change',exact:true});
async function pixel(page:Page,x:number,y:number){return page.locator('canvas').evaluate((canvas,args)=>{
 const [cx,cy]=(canvas as HTMLCanvasElement).dataset.camera!.split(',').map(Number),zoom=Number((canvas as HTMLCanvasElement).dataset.zoom),r=canvas.getBoundingClientRect();
 return {x:r.x+r.width/2+(16*(args.x+args.y-20)+16-cx)*zoom,y:r.y+r.height/2+(8*(-args.x+args.y+20)-16+3-cy)*zoom};
},{x,y});}

async function slope(page:Page,locale='en'){
 // Author the fixture through the same public rectangle UI.
 await page.getByRole('button',{name:locale==='ja'?'地形操作':'Modify terrain',exact:true}).click();
 await page.getByLabel(locale==='ja'?'地形の選択方法':'Terrain selection',{exact:true}).selectOption('rectangle');
 await target(page,locale);await confirm(page,locale).click();
 await expect.poll(async()=>JSON.parse((await save(page)).toString()).packedTerrain.occupantCount).toBe(4);
 await page.getByLabel(locale==='ja'?'地形の選択方法':'Terrain selection',{exact:true}).selectOption('terrace');
}
async function terraceTarget(page:Page,action='placeTerrace',locale='en'){
 await page.getByLabel(locale==='ja'?'地形の操作':'Terrain action',{exact:true}).selectOption(action);
 for(const [label,value] of [[locale==='ja'?'隅のセル X':'Corner cell X',20],[locale==='ja'?'隅のセル Y':'Corner cell Y',20],[locale==='ja'?'隅のセル Z':'Corner cell Z',1]] as const)await page.getByLabel(label,{exact:true}).fill(String(value));
 await page.getByRole('button',{name:locale==='ja'?'地形をプレビュー':'Preview terrain change',exact:true}).click();
}
test('terrace preview is read-only; fresh identity, saved corners, exact reopen, cut and restoration',async({page})=>{
 await page.goto('/');await ready(page);const original=await save(page);
 try{
  await empty(page);await slope(page);const before=await save(page);const canvas=page.locator('canvas');await canvas.evaluate(el=>el.setAttribute('data-instance','terrace'));
  await terraceTarget(page);await expect(page.getByTestId('corner-status')).toContainText('change is legal');
  await expect(page.locator('.corner-changes')).toContainText('1 [0,4,0,0] → 2 [0,4,0,0]');expect(await save(page)).toEqual(before);
  await confirm(page).click();await expect.poll(async()=>Buffer.from(JSON.parse((await save(page)).toString()).packedTerrain.cells,'base64')[610]).toBe(130);
  const terrace=await save(page);await page.reload();await ready(page);expect(await save(page)).toEqual(terrace);await tool(page);
  await terraceTarget(page,'removeTerrace');await expect(page.getByTestId('corner-status')).toContainText('change is legal');await confirm(page).click();
  await expect.poll(async()=>Buffer.from(JSON.parse((await save(page)).toString()).packedTerrain.cells,'base64')[610]).toBe(65);
  await terraceTarget(page,'placeCut');await confirm(page).click();await expect.poll(async()=>JSON.parse((await save(page)).toString()).packedTerrain.occupantCount).toBe(3);
  await terraceTarget(page,'removeCut');await expect(page.locator('.corner-changes')).toContainText('[0,4,0,0]');await confirm(page).click();
  await expect.poll(async()=>JSON.parse((await save(page)).toString()).packedTerrain.occupantCount).toBe(4);expect((await state(page)).cash).toBe('0');
 }finally{await replace(page,original);}
});
test('terrace map ray selects backed-up voxel; Escape, panel and peer changes fence review',async({page,browser})=>{
 await page.goto('/');await ready(page);const original=await save(page);const peer=await browser.newPage();
 try{
  await empty(page);await slope(page);const canvas=page.locator('canvas');await canvas.evaluate(el=>el.setAttribute('data-instance','terrace'));
  const p=await pixel(page,20,20);await page.mouse.click(p.x,p.y);await expect(page.getByTestId('corner-status')).toContainText('change is legal');
  await confirm(page).click();await expect.poll(async()=>Buffer.from(JSON.parse((await save(page)).toString()).packedTerrain.cells,'base64')[610]).toBe(130);
  await page.getByLabel('Terrain action',{exact:true}).selectOption('removeTerrace');await page.mouse.click(p.x,p.y);
  await expect(page.getByTestId('corner-position')).toContainText('20, 20, 1');await expect(confirm(page)).toBeEnabled();
  await page.keyboard.press('Escape');await expect(confirm(page)).toBeDisabled();
  await terraceTarget(page,'removeTerrace');await openPanel(page,'help');await closePanel(page);await expect(confirm(page)).toBeDisabled();
  await page.getByLabel('Terrain selection',{exact:true}).selectOption('terrace');await terraceTarget(page,'removeTerrace');await peer.goto('/');await ready(peer);await tool(peer);await terraceTarget(peer,'removeTerrace');await confirm(peer).click();
  await expect(page.getByTestId('corner-status')).toContainText('Terrain changed');await expect(confirm(page)).toBeDisabled();await expect(canvas).toHaveAttribute('data-instance','terrace');
 }finally{await peer.close();await replace(page,original);}
});
test('Japanese narrow terrace controls support keyboard confirmation and diagnostics without pausing shared time',async({browser})=>{
 const context=await browser.newContext({viewport:{width:390,height:844},locale:'ja-JP',deviceScaleFactor:2}),page=await context.newPage();
 await page.goto('/');await ready(page,'ja');const original=await save(page);
 try{
  await empty(page,'ja');await slope(page,'ja');await terraceTarget(page,'placeTerrace','ja');await expect(confirm(page,'ja')).toBeEnabled();
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  const path=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'terrace-terrain');mkdirSync(path,{recursive:true});await page.screenshot({path:join(path,'terrace-ja-narrow.png'),fullPage:true});
  await confirm(page,'ja').focus();await page.keyboard.press('Enter');await expect.poll(async()=>Buffer.from(JSON.parse((await save(page)).toString()).packedTerrain.cells,'base64')[610]).toBe(130);
  await terraceTarget(page,'placeCut','ja');await expect(page.getByTestId('corner-status')).toContainText('地表');await expect(confirm(page,'ja')).toBeDisabled();expect((await state(page)).paused).toBe(true);
 }finally{await replace(page,original);await context.close();}
});
async function batchTarget(page:Page,locale='en',start=[20,20,1],end=[21,21]){
 for(const [label,n] of [[locale==='ja'?'隅のセル X':'Corner cell X',start[0]],[locale==='ja'?'隅のセル Y':'Corner cell Y',start[1]],[locale==='ja'?'隅のセル Z':'Corner cell Z',start[2]],[locale==='ja'?'終点 X':'End X',end[0]],[locale==='ja'?'終点 Y':'End Y',end[1]]] as const)await page.getByLabel(label,{exact:true}).fill(String(n));
 await page.getByRole('button',{name:locale==='ja'?'地形をプレビュー':'Preview terrain change',exact:true}).click();
}
test('terrace rectangle normalizes reversed coordinates, previews all cells read-only and saves one atomic result',async({page})=>{
 await page.goto('/');await ready(page);const original=await save(page);
 try{
  await empty(page);await tool(page);await page.getByRole('checkbox',{name:'Place terraces in a rectangle',exact:true}).check();const before=await save(page),s=await state(page);expect(s.terrain.occupants).toEqual([]);
  await batchTarget(page,'en',[21,21,1],[20,20]);await expect(page.getByTestId('terrace-batch-count')).toHaveText('Terraces to place: 4');expect(await save(page)).toEqual(before);
  await confirm(page).click();await expect.poll(async()=>(await state(page)).terrain.occupants.filter((o:any)=>o.kind==='terrace').length).toBe(4);
  expect((await state(page)).terrain.geometryRevision).toBe(String(BigInt(s.terrain.geometryRevision)+1n));expect((await state(page)).paused).toBe(s.paused);
  const batch=await save(page);await page.reload();await ready(page);expect(await save(page)).toEqual(batch);await tool(page);await page.getByRole('checkbox',{name:'Place terraces in a rectangle',exact:true}).check();
  await batchTarget(page);await expect(confirm(page)).toBeDisabled();await expect(page.getByTestId('terrace-batch-count')).toHaveText('Terraces to place: 0');
 }finally{await replace(page,original);}
});
test('two map corners select terrace batch; Escape and peer edits invalidate review on the preserved canvas',async({page,browser})=>{
 await page.goto('/');await ready(page);const original=await save(page),peer=await browser.newPage();
 try{
  await empty(page);await slope(page);await page.getByRole('checkbox',{name:'Place terraces in a rectangle',exact:true}).check();const before=await save(page);const canvas=page.locator('canvas');await canvas.evaluate(el=>el.setAttribute('data-instance','terrace-batch'));
  const a=await pixel(page,20,20),b=await pixel(page,21,21);await page.mouse.click(a.x,a.y);await expect(confirm(page)).toBeDisabled();await page.mouse.click(b.x,b.y);
  await expect(page.getByTestId('terrace-batch-count')).toHaveText('Terraces to place: 4');expect(await save(page)).toEqual(before);
  const directory=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'terrace-batch');mkdirSync(directory,{recursive:true});await page.screenshot({path:join(directory,'terrace-batch-en.png'),fullPage:true});
  await page.keyboard.press('Escape');await expect(confirm(page)).toBeDisabled();await batchTarget(page);
  await peer.goto('/');await ready(peer);await tool(peer);await terraceTarget(peer);await confirm(peer).click();await expect(page.getByTestId('rectangle-status')).toContainText('Terrain changed');await expect(confirm(page)).toBeDisabled();await expect(canvas).toHaveAttribute('data-instance','terrace-batch');
 }finally{await peer.close();await replace(page,original);}
});
test('Japanese narrow terrace batch supports end coordinates and one keyboard confirmation',async({browser})=>{
 const context=await browser.newContext({viewport:{width:390,height:844},locale:'ja-JP',deviceScaleFactor:2}),page=await context.newPage();await page.goto('/');await ready(page,'ja');const original=await save(page);
 try{
  await empty(page,'ja');await tool(page,'ja');await page.getByRole('checkbox',{name:'矩形で雛壇を設置',exact:true}).check();await batchTarget(page,'ja');await expect(page.getByTestId('terrace-batch-count')).toHaveText('設置する雛壇の数: 4');
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);await confirm(page,'ja').focus();await page.keyboard.press('Enter');await expect.poll(async()=>(await state(page)).terrain.occupants.filter((o:any)=>o.kind==='terrace').length).toBe(4);
 }finally{await replace(page,original);await context.close();}
});

test('bare ground map targeting places a terrace directly and removes it back to flat ground',async({page})=>{
 await page.goto('/');await ready(page);const original=await save(page);
 try{
  await empty(page);await tool(page);expect((await state(page)).terrain.occupants).toEqual([]);const before=await save(page);
  const p=await pixel(page,20,20);await page.mouse.click(p.x,p.y);await expect(page.getByTestId('corner-position')).toContainText('20, 20, 1');await expect(confirm(page)).toBeEnabled();expect(await save(page)).toEqual(before);
  await confirm(page).click();await expect.poll(async()=>(await state(page)).terrain.occupants.filter((o:any)=>o.kind==='terrace').length).toBe(1);
  const terrace=await save(page);await page.reload();await ready(page);expect(await save(page)).toEqual(terrace);await tool(page);await terraceTarget(page,'removeTerrace');await confirm(page).click();
  await expect.poll(async()=>Buffer.from(JSON.parse((await save(page)).toString()).packedTerrain.cells,'base64')[610]).toBe(65);
  const current=await state(page);expect(current.terrain.occupants[0].kind).toBe('mountain');expect([current.terrain.occupants[0].ne,current.terrain.occupants[0].se,current.terrain.occupants[0].sw,current.terrain.occupants[0].nw]).toEqual([0,0,0,0]);
 }finally{await replace(page,original);}
});
