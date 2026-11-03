import { readFileSync } from 'node:fs';
import { expect,extended,test,type Page } from './fixtures.ts';
import { sourcePixelCounts } from './pixel-helpers.ts';
import { closePanel,openPanel,ready,canonicalState as state } from './browser-helpers.ts';

async function save(p:Page){return(await p.request.get('/api/saves/download')).body();}
async function replace(p:Page,bytes:Buffer){const s=await state(p);expect((await p.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:bytes})).ok()).toBe(true);await ready(p,(await p.locator("html").getAttribute("lang"))??"en");}
async function empty(p:Page){
 const s=await state(p),r=await p.request.post('/api/worlds/empty/preview',{data:{name:'Pole workflow',dimensions:{h:30,v:42,z:6},waterLevel:1,epoch:s.worldEpoch,revision:s.revision}});expect(r.ok()).toBe(true);
 const c=await r.json();expect((await p.request.post('/api/worlds/empty/commit',{data:{id:c.id,requestHash:c.requestHash,candidateHash:c.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).ok()).toBe(true);
 await expect(p.locator('canvas')).toHaveAttribute('data-ruleset','terrain-state-1');await ready(p);
}

const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
const pixels=(p:Page,voxels:any[])=>sourcePixelCounts(p,catalog,voxels,'accessory');
const types=['{DDF7F835-8100-45fd-8AE7-EC498796D387}','{DD7D4548-5E0E-48e4-AB6E-64A443C240F7}','{86E56A60-19DF-4ba7-BFE8-ABFBDF5DFA0A}','{FD1635C1-05BF-41af-9185-4891B983E2A4}'];
async function target(p:Page,ja=false){
 for(const [name,value] of [[ja?'始点 X':'Start X',30],[ja?'始点 Y':'Start Y',27],[ja?'線路の高さ':'Track level',1]] as const)await p.getByLabel(name,{exact:true}).fill(String(value));
 await p.getByRole('button',{name:ja?'経路をプレビュー':'Preview route',exact:true}).click();
}
async function track(p:Page,dx:number,dy:number){
 await p.getByRole('button',{name:'Track',exact:true}).click();
 for(const [name,value] of [['Start X',30],['Start Y',27],['End X',30+dx],['End Y',27+dy],['Track level',1]] as const)await p.getByLabel(name,{exact:true}).fill(String(value));
 await p.getByRole('button',{name:'Preview route',exact:true}).click();await p.getByRole('button',{name:'Confirm track change',exact:true}).click();await expect(p.getByTestId('rail-route')).toHaveCount(0);
}
for(const ja of [false,true])test(`pole types, inferred axes, replacement, removal, peers and exact reopen ${ja?'JA':'EN'}`,async({page,browser})=>{
 test.setTimeout(120_000);await page.goto('/');await ready(page);const original=await save(page);const peer=await browser.newPage();
 try{
  await peer.goto('/');await ready(peer);
  for(const [dx,dy,axis] of (ja&&!extended()?[[3,0,0]]:[[3,0,0],[0,3,1],[-3,0,0],[0,-3,1]])){
   await empty(page);await track(page,dx,dy);
   const bare=await state(page),owner=bare.mixed.voxels.find((v:any)=>v.position.x===30&&v.position.y===27).ownerId;
   if(ja){await openPanel(page,'settings');await page.getByLabel('Language',{exact:true}).selectOption('ja');await closePanel(page);}
   if(ja&&!extended())await page.setViewportSize({width:430,height:850});
   await ready(page,ja?'ja':'en');await page.getByRole('button',{name:ja?'架線柱':'Electric poles',exact:true}).click();
   const confirm=page.getByRole('button',{name:ja?'架線柱の変更を確定':'Confirm pole change',exact:true});
   for(const type of ja&&!extended()?types.slice(0,1):types){
    await page.getByLabel(ja?'架線柱の種類':'Pole type',{exact:true}).selectOption(type);
    const before=await save(page);await target(page,ja);await expect(confirm).toBeEnabled();expect(await save(page)).toEqual(before);
    if(ja&&!extended()){await expect(confirm).toBeInViewport();await confirm.focus();await page.keyboard.press('Enter');}else await confirm.click();
    await expect(page.getByTestId('rail-route')).toHaveCount(0);
    await expect(page.locator('canvas')).toHaveAttribute('data-presentation','ready');
    const built=await state(page),v=built.mixed.voxels.find((v:any)=>v.ownerId===owner);
    expect(v.traffic.accessory.definition.contributionId).toBe(type);expect(v.traffic.accessory.direction).toBe(axis);expect(v.traffic.rail).toEqual(bare.mixed.voxels.find((v:any)=>v.ownerId===owner).traffic.rail);expect(built.mixed.accounts).toEqual(bare.mixed.accounts);
    await expect.poll(async()=>JSON.stringify((await state(peer)).mixed)).toBe(JSON.stringify(built.mixed));
    expect((await pixels(page,[v]))[0]).toBeGreaterThan(3);
    if(type===types[0]){
     const shown=await page.locator('canvas').evaluate(c=>(c as HTMLCanvasElement).toDataURL()),saved=await save(page);
     await openPanel(page,'view',ja?'ja':'en');await page.getByLabel(ja?'架線柱を表示':'Show electric poles',{exact:true}).uncheck();
     await expect.poll(()=>page.locator('canvas').evaluate(c=>(c as HTMLCanvasElement).toDataURL())).not.toBe(shown);await expect(page.locator('canvas')).toHaveAttribute('data-presentation','ready');expect(await save(page)).toEqual(saved);
     await page.getByLabel(ja?'架線柱を表示':'Show electric poles',{exact:true}).check();await closePanel(page);
     await expect.poll(()=>page.locator('canvas').evaluate(c=>(c as HTMLCanvasElement).toDataURL())).toBe(shown);
    }

   }
   if(extended()||!ja&&dx===3){
    const bytes=await save(page);await replace(page,bytes);expect(await save(page)).toEqual(bytes);await page.getByRole('button',{name:ja?'架線柱':'Electric poles',exact:true}).click();
   }
   await page.getByLabel(ja?'線路の操作':'Track action',{exact:true}).selectOption('remove');await target(page,ja);await expect(confirm).toBeEnabled();await confirm.click();await expect(page.getByTestId('rail-route')).toHaveCount(0);
   expect((await state(page)).mixed.voxels.find((v:any)=>v.ownerId===owner).traffic.accessory).toBeNull();
   await target(page,ja);await expect(confirm).toBeDisabled();
   if(ja){await openPanel(page,'settings','ja');await page.getByLabel('言語',{exact:true}).selectOption('en');await closePanel(page);}
  }
 }finally{await peer.close();await replace(page,original);}
});
test('pole point pick, E/Escape, obstruction, observer and narrow controls',async({page,browser})=>{
 await page.goto('/');await ready(page);const original=await save(page);const viewer=await browser.newPage();
 try{
  await empty(page);await track(page,3,0);const canvas=page.locator('canvas');await canvas.focus();await page.keyboard.press('e');await expect(page.getByRole('button',{name:'Electric poles',exact:true})).toHaveAttribute('aria-pressed','true');
  const q=await canvas.evaluate(el=>{const c=el as HTMLCanvasElement,r=c.getBoundingClientRect(),[cx,cy]=c.dataset.camera!.split(',').map(Number),z=Number(c.dataset.zoom);return{x:r.x+r.width/2+(16*(30+27-20)+16-cx)*z,y:r.y+r.height/2+(8*(-30+27+20)-16+8-cy)*z};});
  await page.mouse.click(q.x,q.y);await expect(page.getByRole('button',{name:'Confirm pole change',exact:true})).toBeEnabled();const before=await save(page);
  await canvas.focus();await page.keyboard.press('Escape');await expect(page.getByTestId('rail-route')).toHaveCount(0);expect(await save(page)).toEqual(before);
  await page.setViewportSize({width:430,height:850});await target(page);await expect(page.getByRole('button',{name:'Confirm pole change',exact:true})).toBeEnabled();
  await page.getByLabel('Start X',{exact:true}).fill('28');await page.getByRole('button',{name:'Preview route',exact:true}).click();await expect(page.getByRole('button',{name:'Confirm pole change',exact:true})).toBeDisabled();
  expect((await viewer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);await viewer.goto('/');await ready(viewer);await expect(viewer.getByRole('button',{name:'Electric poles',exact:true})).toBeDisabled();
 }finally{await viewer.close();await replace(page,original);}
});
