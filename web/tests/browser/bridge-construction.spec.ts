import { readFileSync } from 'node:fs';
import { expect,extended,test,type Browser,type Page } from './fixtures.ts';
import { sourcePixelCounts } from './pixel-helpers.ts';
import { closePanel,openPanel,ready,canonicalState as state } from './browser-helpers.ts';
const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
const bridge='{874B8BFD-1EF4-4828-9C5A-D0EA1049D28D}';
const compat='protocolVersion=10&contentVersion=terrain-content-1&contentVersion=mixed-railway-content-1';

async function save(p:Page){return(await p.request.get('/api/saves/download')).body();}
async function replace(p:Page,bytes:Buffer){const s=await state(p);expect((await p.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:bytes})).ok()).toBe(true);await ready(p,(await p.locator("html").getAttribute("lang"))??"en");}
async function empty(p:Page){
 const s=await state(p),r=await p.request.post('/api/worlds/empty/preview',{data:{name:'Bridge workflow',dimensions:{h:30,v:42,z:6},waterLevel:1,epoch:s.worldEpoch,revision:s.revision}});expect(r.ok()).toBe(true);
 const c=await r.json();expect((await p.request.post('/api/worlds/empty/commit',{data:{id:c.id,requestHash:c.requestHash,candidateHash:c.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).ok()).toBe(true);
 await expect(p.locator('canvas')).toHaveAttribute('data-ruleset','terrain-state-1');await ready(p);
}
async function target(p:Page,from=[30,27],to=[34,27],z=3,ja=false){
 for(const [name,value] of [[ja?'始点 X':'Start X',from[0]],[ja?'始点 Y':'Start Y',from[1]],[ja?'終点 X':'End X',to[0]],[ja?'終点 Y':'End Y',to[1]],[ja?'線路の高さ':'Track level',z]] as const)await p.getByLabel(name,{exact:true}).fill(String(value));
 await p.getByRole('button',{name:ja?'経路をプレビュー':'Preview route',exact:true}).click();
}
async function confirm(p:Page,ja=false,normal=false){await p.getByRole('button',{name:normal?'Confirm track change':ja?'鉄橋の変更を確定':'Confirm bridge change',exact:true}).click();await expect(p.getByTestId('rail-route')).toHaveCount(0);await expect(p.locator('.command-feedback')).toContainText(ja?'サーバーで確定':'Confirmed by server');}
async function cut(p:Page,z:number){await openPanel(p,'view');await p.getByLabel('Height cut',{exact:true}).fill(String(z));await closePanel(p);await expect(p.locator('canvas')).toHaveAttribute('data-terrain-cut',String(z));}
async function click(p:Page,x:number,y:number,z:number){
 const q=await p.locator('canvas').evaluate((el,a)=>{const c=el as HTMLCanvasElement,r=c.getBoundingClientRect(),[cx,cy]=c.dataset.camera!.split(',').map(Number),zoom=Number(c.dataset.zoom);return{x:r.x+r.width/2+(16*(a.x+a.y-20)+16-cx)*zoom,y:r.y+r.height/2+(8*(-a.x+a.y+20)-16*a.z+8-cy)*zoom};},{x,y,z});
 await p.mouse.click(q.x,q.y);
}
const pixels=(p:Page,voxels:any[])=>sourcePixelCounts(p,catalog,voxels,'rail');

test('all four bridge directions retain exact source art, alternating piers, actual costs and save reopen remove',async({page})=>{
 await page.goto('/');await ready(page);const original=await save(page);
 try{
  for(const [x,y,variants] of [[30,23,[3,4,4,4,5]],[34,27,[2,1,1,1,0]],[30,31,[5,4,4,4,3]],[26,27,[0,1,1,1,2]]] as const){
   await empty(page);await page.getByRole('button',{name:'Bridge',exact:true}).click();const before=await save(page);
   await target(page,[30,27],[x,y]);await expect(page.getByTestId('rail-status')).toContainText('route is legal');await expect(page.getByTestId('rail-cost')).toContainText('90,000,000');
   expect(await save(page)).toEqual(before);
   await confirm(page);const s=await state(page),rails=s.mixed.voxels.filter((v:any)=>v.traffic?.rail),piers=s.mixed.voxels.filter((v:any)=>v.kind==='pier');
   const dx=Math.sign(x-30),dy=Math.sign(y-27);expect(Array.from({length:5},(_,i)=>rails.find((v:any)=>v.position.x===30+i*dx&&v.position.y===27+i*dy).traffic.rail.variant)).toEqual(variants);
   expect(piers).toHaveLength(6);expect(rails.every((v:any)=>v.traffic.rail.definition.recipeId===bridge)).toBe(true);
   expect((await pixels(page,rails)).every(n=>n>10)).toBe(true);expect((await pixels(page,piers)).some(n=>n>10)).toBe(true);

   if(extended()||x===30&&y===23){
    const captured=await save(page);await replace(page,captured);expect(await save(page)).toEqual(captured);
    await cut(page,2);
    expect((await pixels(page,(await state(page)).mixed.voxels.filter((v:any)=>v.kind==='pier'))).some(n=>n>10)).toBe(true);
    await cut(page,5);await page.getByRole('button',{name:'Bridge',exact:true}).click();await page.getByLabel('Track action',{exact:true}).selectOption('remove');await target(page,[30,27],[x,y]);
    await expect(page.getByTestId('rail-cost')).toContainText('30,000,000');await expect(page.locator('.rail-losses li')).toHaveCount(6);await confirm(page);
    expect((await state(page)).mixed.voxels).toHaveLength(0);expect((await state(page)).mixed.accounts.transactions).toHaveLength(2);
   }
  }
 }finally{await replace(page,original);}
});

test('map short-span cancel and snapped long span synchronize with a peer and preserve the live canvas',async({page,browser})=>{
 await page.goto('/');await ready(page);const original=await save(page),ctx=await browser.newContext(),peer=await ctx.newPage();
 try{
  await empty(page);await peer.goto('/');await ready(peer);await page.getByRole('button',{name:'Bridge',exact:true}).click();
  const canvas=page.locator('canvas');const before=await save(page);
  await click(page,30,27,1);await click(page,31,27,1);await expect(page.getByTestId('rail-status')).toContainText('route is legal');await expect(page.getByTestId('rail-cost')).toContainText('12,000,000');
  await page.getByRole('button',{name:'Cancel preview',exact:true}).click();expect(await save(page)).toEqual(before);
  await target(page,[30,27],[31,27],1);await confirm(page);expect((await state(page)).mixed.voxels.filter((v:any)=>v.traffic?.rail).map((v:any)=>v.traffic.rail.variant)).toEqual([2,0]);
  const shortSave=await save(page);await replace(page,shortSave);expect(await save(page)).toEqual(shortSave);
  await page.getByRole('button',{name:'Bridge',exact:true}).click();await page.getByLabel('Track action',{exact:true}).selectOption('remove');await target(page,[30,27],[31,27],1);await expect(page.getByTestId('rail-cost')).toContainText('4,000,000');await confirm(page);
  await page.getByLabel('Track action',{exact:true}).selectOption('lay');await canvas.evaluate(el=>(el as any).__bridgeIdentity=true);
  await target(page,[30,27],[34,29],3);await expect(page.getByTestId('rail-route')).toContainText('30, 27, 3 → 34, 27, 3');await confirm(page);
  await expect(peer.locator('canvas')).toHaveAttribute('data-ruleset','mixed-railway-1');expect(await state(peer)).toEqual(await state(page));expect(await canvas.evaluate(el=>(el as any).__bridgeIdentity)).toBe(true);
  await target(page,[30,27],[34,29],3);await expect(page.getByTestId('rail-cost')).toContainText('90,000,000');await confirm(page);expect((await state(page)).mixed.accounts.transactions).toHaveLength(4);
 }finally{await replace(page,original);await ctx.close();}
});

test('existing straight segments survive bridge construction, wrong axis rejects and blocked lower supports are omitted',async({page})=>{
 await page.goto('/');await ready(page);const original=await save(page);
 try{
  await empty(page);await page.getByRole('button',{name:'Track',exact:true}).click();await target(page,[30,27],[32,27],1);await confirm(page,false,true);
  const existing=(await state(page)).mixed.voxels.filter((v:any)=>v.traffic?.rail);
  await page.getByRole('button',{name:'Bridge',exact:true}).click();await target(page,[29,27],[34,27],1);await expect(page.getByTestId('rail-cost')).toContainText('36,000,000');await confirm(page);
  let s=await state(page);expect(existing.every((v:any)=>s.mixed.voxels.some((n:any)=>JSON.stringify(v)===JSON.stringify(n)))).toBe(true);
  expect(s.mixed.voxels.filter((v:any)=>v.traffic?.rail?.definition.recipeId===bridge).map((v:any)=>[v.position.x,v.traffic.rail.variant]).sort((a:any,b:any)=>a[0]-b[0])).toEqual([[29,2],[33,2],[34,0]]);
  const before=await save(page);await target(page,[30,26],[30,28],1);await expect(page.getByTestId('rail-status')).not.toContainText('route is legal');expect(await save(page)).toEqual(before);
  await target(page,[29,27],[34,27],3);await expect(page.getByTestId('rail-cost')).toContainText('108,000,000');await expect(page.getByTestId('rail-cost')).toContainText('New piers: 0');await confirm(page);
  s=await state(page);expect(s.mixed.voxels.filter((v:any)=>v.kind==='pier')).toHaveLength(0);

 }finally{await replace(page,original);}
});

test('Japanese narrow DPR2 bridge task retains costs, keyboard shortcut, cancel and exact reopen',async({browser})=>{
 const ctx=await browser.newContext({viewport:{width:430,height:920},deviceScaleFactor:2}),page=await ctx.newPage();await page.goto('/');await ready(page);const original=await save(page);
 try{
  await empty(page);await openPanel(page,'settings');await page.getByLabel('Language',{exact:true}).selectOption('ja');await closePanel(page);
  await page.locator('canvas').focus();await page.keyboard.press('b');await expect(page.getByRole('heading',{name:'鉄橋',exact:true})).toBeVisible();
  await target(page,[30,27],[34,29],3,true);await expect(page.getByTestId('rail-cost')).toContainText('90,000,000');await expect(page.getByRole('button',{name:'鉄橋の変更を確定',exact:true})).toBeInViewport();
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  await expect(page.locator('.tool-card button.primary')).toHaveCount(1);
  await expect(page.getByRole('button',{name:'鉄橋の変更を確定',exact:true})).toHaveClass('primary');
  await page.getByLabel('始点 X',{exact:true}).focus();await page.keyboard.press('Enter');
  await expect(page.getByTestId('rail-route')).toHaveCount(0);await expect(page.locator('.command-feedback')).toContainText('サーバーで確定');
  const captured=await save(page);await replace(page,captured);expect(await save(page)).toEqual(captured);
 }finally{await replace(page,original);await ctx.close();}
});

async function wire(browser:Browser,q:any,secret?:string,duplicate=false){
 const p=await browser.newPage();let finish!:(m:any)=>void;const receipt=new Promise<any>(r=>finish=r);
 try{
  if(secret)expect((await p.request.post('/api/access',{data:{secret}})).ok()).toBe(true);
  await p.routeWebSocket('**/api/session?*',route=>{const server=route.connectToServer();let n=0;
   server.onMessage(message=>{const m=JSON.parse(String(message));route.send(message);
    if(m.type==='join'){const c=JSON.stringify({type:'command',requestId:'bridge-receipt',expectedWorldEpoch:q.expectedWorldEpoch,payload:{kind:'editRailway',query:q}});server.send(c);if(duplicate)server.send(c);}
    if(m.type==='rejected'||m.type==='accepted'&&++n===(duplicate?2:1))finish(m);
   });
  });await p.goto('/');await ready(p);
  if(secret)await expect(p.getByRole('button',{name:'Bridge',exact:true})).toBeDisabled();
  return await receipt;
 }finally{await p.close();}
}

test('late bridge previews cannot replace edited drafts, Observer commits reject and duplicate receipts book once',async({page,browser})=>{
 await page.goto('/');await ready(page);const original=await save(page);let release=()=>{};
 try{
  await empty(page);await page.getByRole('button',{name:'Bridge',exact:true}).click();
  let delayed=false;const held=new Promise<void>(resolve=>release=resolve);
  await page.route('**/api/railway/preview?*',async route=>{const response=await route.fetch();if(!delayed){delayed=true;await held;}await route.fulfill({response});});
  await target(page);await expect.poll(()=>delayed).toBe(true);await page.getByLabel('End X',{exact:true}).fill('35');release();await page.unrouteAll({behavior:'wait'});await page.evaluate(()=>new Promise<void>(r=>requestAnimationFrame(()=>requestAnimationFrame(()=>r()))));await expect(page.getByRole('button',{name:'Confirm bridge change',exact:true})).toBeDisabled();
  await target(page);const s=await state(page),q={expectedWorldEpoch:s.worldEpoch,expectedGeometryRevision:s.terrain.geometryRevision,expectedContentVersion:s.contentVersion,from:{x:30,y:27,z:3},to:{x:34,y:27,z:3},action:'lay',routing:'closest',infrastructure:'bridge'};
  const before=await save(page);expect((await wire(browser,q,'observer-test')).code).toBe('notAuthorized');expect(await save(page)).toEqual(before);
  expect((await wire(browser,q,undefined,true)).type).toBe('accepted');await expect(page.getByTestId('rail-status')).toContainText('changed');expect((await state(page)).mixed.accounts.transactions).toHaveLength(1);
  expect((await wire(browser,q)).code).toBe('staleRevision');
 }finally{release();await replace(page,original);}
});
