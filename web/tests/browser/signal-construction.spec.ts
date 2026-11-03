import { readFileSync } from 'node:fs';
import { expect,extended,test,type Page } from './fixtures.ts';
import { sourcePixelCounts } from './pixel-helpers.ts';
import { closePanel,openPanel,ready,canonicalState as state } from './browser-helpers.ts';

async function save(p:Page){return(await p.request.get('/api/saves/download')).body();}
async function replace(p:Page,bytes:Buffer){const s=await state(p);expect((await p.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:bytes})).ok()).toBe(true);await ready(p,(await p.locator("html").getAttribute("lang"))??"en");}
async function empty(p:Page){
 const s=await state(p),r=await p.request.post('/api/worlds/empty/preview',{data:{name:'Signal workflow',dimensions:{h:30,v:42,z:6},waterLevel:1,epoch:s.worldEpoch,revision:s.revision}});expect(r.ok()).toBe(true);
 const c=await r.json();expect((await p.request.post('/api/worlds/empty/commit',{data:{id:c.id,requestHash:c.requestHash,candidateHash:c.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).ok()).toBe(true);
 await expect(p.locator('canvas')).toHaveAttribute('data-ruleset','terrain-state-1');await ready(p);
}

const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
const pixels=(p:Page,voxels:any[])=>sourcePixelCounts(p,catalog,voxels,'signal');
const types=['{5349DE2A-DF3E-49B4-87A0-973105B7E6A4}','{6FA55842-E693-414f-A120-0A3D0C613B21}'];
for(const ja of [false,true])test(`signal types, directions, replacement, restoration, peers and exact reopen ${ja?'JA':'EN'}`,async({page,browser})=>{
 test.setTimeout(120_000);await page.goto('/');await ready(page);const original=await save(page);const peer=await browser.newPage();
 try{
  await peer.goto('/');await ready(peer);await empty(page);
  if(ja){await openPanel(page,'settings');await page.getByLabel('Language',{exact:true}).selectOption('ja');await closePanel(page);}
  if(ja&&!extended())await page.setViewportSize({width:430,height:850});
  await ready(page,ja?'ja':'en');await page.getByRole('button',{name:ja?'信号':'Signals',exact:true}).click();
  const text={type:ja?'信号の種類':'Signal type',dir:ja?'信号の向き':'Signal direction',action:ja?'線路の操作':'Track action',confirm:ja?'信号の変更を確定':'Confirm signal change'};
  for(const type of ja&&!extended()?types.slice(0,1):types)for(const d of ja&&!extended()?[2]:[0,2,4,6]){
   await page.getByLabel(text.action,{exact:true}).selectOption('lay');const name=ja?(type===types[0]?'三灯式':'入替信号'):(type===types[0]?'Three-aspect signal':'Shunting signal');await page.locator('#building-catalog').getByRole('group',{name:`${name} · ${text.dir}`,exact:true}).getByRole('button',{name:(ja?['北 (0)','東 (2)','南 (4)','西 (6)']:['North (0)','East (2)','South (4)','West (6)'])[d/2],exact:true}).click();
   for(const [name,value] of [[ja?'始点 X':'Start X',30],[ja?'始点 Y':'Start Y',27],[ja?'線路の高さ':'Track level',1]] as const)await page.getByLabel(name,{exact:true}).fill(String(value));
   const before=await save(page);await page.getByRole('button',{name:ja?'経路をプレビュー':'Preview route',exact:true}).click();await expect(page.getByRole('button',{name:text.confirm,exact:true})).toBeEnabled();expect(await save(page)).toEqual(before);
   const primary=page.getByRole('button',{name:text.confirm,exact:true});
   if(ja&&!extended()){await expect(primary).toBeInViewport();await primary.focus();await page.keyboard.press('Enter');}else await primary.click();
   await expect(page.getByTestId('rail-route')).toHaveCount(0);
   const built=await state(page),v=built.mixed.voxels[0];expect(v.traffic.rail.definition.contributionId).toBe(type);expect(v.traffic.rail.direction).toBe(d);expect(v.traffic.rail.pattern).toBe(d%4===0?10:4);expect(built.mixed.accounts.transactions).toEqual([]);
   await expect.poll(async()=>JSON.stringify((await state(peer)).mixed)).toBe(JSON.stringify(built.mixed));
   await expect(page.locator('canvas')).toHaveAttribute('data-presentation','ready');expect((await pixels(page,[v]))[0]).toBeGreaterThan(3);
   if(extended()||!ja&&type===types[0]&&d===0){
    const bytes=await save(page);await replace(page,bytes);expect(await save(page)).toEqual(bytes);await page.getByRole('button',{name:ja?'信号':'Signals',exact:true}).click();
   }
   // A signal cannot replace another signal; source requires prior removal.
   for(const [name,value] of [[ja?'始点 X':'Start X',30],[ja?'始点 Y':'Start Y',27],[ja?'線路の高さ':'Track level',1]] as const)await page.getByLabel(name,{exact:true}).fill(String(value));
   await page.getByRole('button',{name:ja?'経路をプレビュー':'Preview route',exact:true}).click();await expect(page.getByRole('button',{name:text.confirm,exact:true})).toBeDisabled();
   await page.getByLabel(text.action,{exact:true}).selectOption('remove');await page.getByRole('button',{name:ja?'経路をプレビュー':'Preview route',exact:true}).click();await expect(page.getByRole('button',{name:text.confirm,exact:true})).toBeEnabled();await page.getByRole('button',{name:text.confirm,exact:true}).click();await expect(page.getByTestId('rail-route')).toHaveCount(0);
   const removed=await state(page);expect(removed.mixed.voxels[0].traffic.rail.definition.contributionId).toBe(`system:pattern/${d%4===0?10:4}`);expect(removed.mixed.voxels[0].ownerId).toBe(v.ownerId);expect(removed.mixed.accounts.transactions).toEqual([]);
  }
 }finally{await peer.close();await replace(page,original);}
});
test('signal point pick, F/Escape, observer and narrow controls',async({page,browser})=>{
 await page.goto('/');await ready(page);const original=await save(page);const viewer=await browser.newPage();
 try{
  await empty(page);const canvas=page.locator('canvas');await canvas.focus();await page.keyboard.press('f');await expect(page.getByRole('button',{name:'Signals',exact:true})).toHaveAttribute('aria-pressed','true');
  const q=await canvas.evaluate(el=>{const c=el as HTMLCanvasElement,r=c.getBoundingClientRect(),[cx,cy]=c.dataset.camera!.split(',').map(Number),z=Number(c.dataset.zoom);return{x:r.x+r.width/2+(16*(30+27-20)+16-cx)*z,y:r.y+r.height/2+(8*(-30+27+20)-16+8-cy)*z};});
  await page.mouse.click(q.x,q.y);await expect(page.getByRole('button',{name:'Confirm signal change',exact:true})).toBeEnabled();const before=await save(page);
  await canvas.focus();await page.keyboard.press('Escape');await expect(page.getByTestId('rail-route')).toHaveCount(0);expect(await save(page)).toEqual(before);
  await page.setViewportSize({width:430,height:850});await page.getByRole('button',{name:'Preview route',exact:true}).click();await expect(page.getByRole('button',{name:'Confirm signal change',exact:true})).toBeEnabled();
  expect((await viewer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);await viewer.goto('/');await ready(viewer);await expect(viewer.getByRole('button',{name:'Signals',exact:true})).toBeDisabled();
 }finally{await viewer.close();await replace(page,original);}
});
