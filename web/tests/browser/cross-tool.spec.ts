import {test,expect,type Page} from '@playwright/test';
import {mkdirSync} from 'node:fs';
import {join} from 'node:path';
import {ready,canonicalState as state,openPanel,closePanel} from './ux-helpers.ts';
async function save(p:Page){return(await p.request.get('/api/saves/download')).body();}
async function replace(p:Page,b:Buffer){const s=await state(p);expect((await p.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:b})).ok()).toBe(true);}
async function empty(p:Page){
 const s=await state(p),r=await p.request.post('/api/worlds/empty/preview',{data:{name:'Cross-tool matrix',dimensions:{h:30,v:42,z:6},waterLevel:1,epoch:s.worldEpoch,revision:s.revision}});expect(r.ok()).toBe(true);const c=await r.json();
 expect((await p.request.post('/api/worlds/empty/commit',{data:{id:c.id,requestHash:c.requestHash,candidateHash:c.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).ok()).toBe(true);await expect(p.locator('canvas')).toHaveAttribute('data-ruleset','terrain-state-1');
}
for(const ja of [false,true])test(`cross-tool terrain protection, peer invalidation, authoring and exact reopen ${ja?'JA narrow':'EN'}`,async({page,browser})=>{
 test.setTimeout(120_000);await page.goto('/');await ready(page);const original=await save(page),peer=await browser.newPage();const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 try{
  await empty(page);await page.getByRole('button',{name:'Track',exact:true}).click();
  for(const [name,n] of [['Start X',30],['Start Y',27],['Track level',1],['End X',33],['End Y',27]] as const)await page.getByLabel(name,{exact:true}).fill(String(n));
  await page.getByRole('button',{name:'Preview route',exact:true}).click();await page.getByRole('button',{name:'Confirm track change',exact:true}).click();await expect(page.getByTestId('rail-route')).toHaveCount(0);
  await peer.goto('/');await ready(peer);
  if(ja){await openPanel(page,'settings');await page.getByLabel('Language',{exact:true}).selectOption('ja');await closePanel(page);await ready(page,'ja');await page.setViewportSize({width:430,height:850});}
  const terrain=ja?'地形操作':'Modify terrain',check=ja?'隅をプレビュー':'Preview corner',confirm=ja?'地形の変更を確定':'Confirm terrain change';
  await page.getByRole('button',{name:terrain,exact:true}).click();
  const canvas=page.locator('canvas');await canvas.evaluate(el=>el.dataset.instance='cross-tool');const before=await save(page),built=await state(page),camera=await canvas.getAttribute('data-camera');
  async function corner(x:number,y:number){
   for(const [name,n] of [[ja?'隅のセル X':'Corner cell X',x],[ja?'隅のセル Y':'Corner cell Y',y],[ja?'隅のセル Z':'Corner cell Z',1]] as const)await page.getByLabel(name,{exact:true}).fill(String(n));
   await page.getByRole('button',{name:check,exact:true}).click();
  }
  await corner(30,27);await expect(page.getByRole('button',{name:confirm,exact:true})).toBeDisabled();expect(await save(page)).toEqual(before);
  await corner(20,20);await expect(page.getByRole('button',{name:confirm,exact:true})).toBeEnabled();expect(await save(page)).toEqual(before);
  // A pole changes mixed geometry while terrain stays unchanged. The captured
  // terrain preview must become stale on the other participant's publication.
  await peer.getByRole('button',{name:'Electric poles',exact:true}).click();
  for(const [name,n] of [['Start X',30],['Start Y',27],['Track level',1]] as const)await peer.getByLabel(name,{exact:true}).fill(String(n));
  await peer.getByRole('button',{name:'Preview route',exact:true}).click();await peer.getByRole('button',{name:'Confirm pole change',exact:true}).click();await expect(peer.getByTestId('rail-route')).toHaveCount(0);
  await expect(page.getByTestId('corner-status')).toContainText(ja?'地形が変更':'Terrain changed');await expect(page.getByRole('button',{name:confirm,exact:true})).toBeDisabled();
  expect((await state(page)).terrain.geometryRevision).toBe(built.terrain.geometryRevision);
  await corner(20,20);await page.getByRole('button',{name:confirm,exact:true}).click();await expect(page.getByRole('button',{name:confirm,exact:true})).toBeDisabled();
  await expect.poll(async()=>(await state(peer)).terrain.occupants.length).toBe(4);
  const after=await state(page);expect(after.mixed.voxels).toEqual((await state(peer)).mixed.voxels);expect(after.mixed.accounts).toEqual(built.mixed.accounts);expect(after.tick).toBe(built.tick);
  await page.getByRole('button',{name:ja?'マップ作成を開始':'Enter map authoring',exact:true}).click();await page.getByRole('button',{name:ja?'モード変更を確定':'Confirm mode change',exact:true}).click();
  await expect(page.getByTestId('authoring-mode')).toContainText(ja?'有効':'on');
  await expect(canvas).toHaveAttribute('data-instance','cross-tool');expect(await canvas.getAttribute('data-camera')).toBe(camera);
  const dir=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'cross-tool');mkdirSync(dir,{recursive:true});await page.screenshot({path:join(dir,ja?'ja-narrow.png':'en.png'),fullPage:true});
  const saved=await save(page);await replace(page,saved);await page.reload();await ready(page,ja?'ja':'en');expect(await save(page)).toEqual(saved);expect(errors).toEqual([]);
 }finally{await peer.close();await replace(page,original);}
});
