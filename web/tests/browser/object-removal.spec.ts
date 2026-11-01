import {test,expect,type Page} from '@playwright/test';
import {readFileSync,mkdirSync} from 'node:fs';
import {join} from 'node:path';
import {ready,canonicalState as state,openPanel,closePanel,previewRemovalLine,confirmRemoval} from './ux-helpers.ts';
const mixed=readFileSync('../prototype/protocol/saves/mixed-v8.json');
const terrain=readFileSync('../prototype/protocol/saves/terrain-v8.json');
const save=async(page:Page)=>(await page.request.get('/api/saves/download')).body();
async function replace(page:Page,data:Buffer){const s=await state(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data})).ok()).toBe(true);await ready(page);await expect(page.locator('canvas')).toHaveAttribute('data-assets','ready');}
async function click(page:Page,x:number,y:number){
 const p=await page.locator('canvas').evaluate((el,p)=>{
  const c=el as HTMLCanvasElement,r=c.getBoundingClientRect(),[cx,cy]=c.dataset.camera!.split(',').map(Number),zoom=Number(c.dataset.zoom);
  return {x:r.x+r.width/2+(p.x-cx)*zoom,y:r.y+r.height/2+(p.y-cy)*zoom};
 },{x,y});await page.mouse.click(p.x,p.y);
}
const confirm=(page:Page,ja=false)=>page.getByRole('button',{name:ja?'撤去を確定':'Confirm removal',exact:true});
const remove=(page:Page,ja=false)=>page.getByRole('button',{name:ja?'撤去':'Remove',exact:true});
async function revealForest(page:Page,ja=false){
 await openPanel(page,'view',ja?'ja':'en');await page.getByLabel(ja?'高さ表示':'Height cut',{exact:true}).fill('4');
 await expect(page.locator('canvas')).toHaveAttribute('data-terrain-cut','4');await closePanel(page);
}

async function empty(page:Page){
 const s=await state(page),response=await page.request.post('/api/worlds/empty/preview',{data:{name:'Removal lines',dimensions:{h:30,v:42,z:6},waterLevel:1,epoch:s.worldEpoch,revision:s.revision}});
 expect(response.ok()).toBe(true);const c=await response.json();
 expect((await page.request.post('/api/worlds/empty/commit',{data:{id:c.id,requestHash:c.requestHash,candidateHash:c.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).ok()).toBe(true);
 await expect(page.locator('canvas')).toHaveAttribute('data-ruleset','terrain-state-1');await ready(page);
}
async function build(page:Page,tool:'Track'|'Bridge',from:number[],to:number[],level=1){
 const button=page.getByRole('button',{name:tool,exact:true});if(await button.getAttribute('aria-pressed')!=='true')await button.click();
 if(tool==='Track')await expect(page.getByLabel('Track action',{exact:true})).toHaveCount(0);
 for(const [label,value] of [['Start X',from[0]],['Start Y',from[1]],['End X',to[0]],['End Y',to[1]],['Track level',level]] as const)await page.getByLabel(label,{exact:true}).fill(String(value));
 await page.getByRole('button',{name:'Preview route',exact:true}).click();
 await page.getByRole('button',{name:tool==='Track'?'Confirm track change':'Confirm bridge change',exact:true}).click();await expect(page.getByTestId('rail-route')).toHaveCount(0);
 await expect(page.locator('.command-feedback')).toContainText('Confirmed by server');
}

test('Track is build-only and one Remove line previews, cancels and removes tracks plus bridges across a gap in both peers',async({page,browser})=>{
 await page.goto('/');await ready(page);const original=await save(page),peer=await browser.newPage();
 try{
  await empty(page);await build(page,'Track',[30,27],[32,27]);await build(page,'Bridge',[34,27],[36,27]);await build(page,'Track',[30,29],[32,29]);
  await peer.goto('/');await ready(peer);const before=await save(page),read=await state(page);
  await previewRemovalLine(page,[30,27],[36,27]);await expect(page.locator('canvas')).toHaveAttribute('data-removal-path-cells','7');
  await expect(page.locator('.remove-task .rail-losses li')).toHaveCount(6);expect(await save(page)).toEqual(before);
  await page.getByRole('button',{name:'Cancel preview',exact:true}).click();await expect(confirm(page)).toBeDisabled();await expect(page.locator('canvas')).toHaveAttribute('data-removal-path-cells','0');expect(await save(page)).toEqual(before);
  await previewRemovalLine(page,[30,27],[36,27]);await confirmRemoval(page);
  await expect.poll(async()=>(await state(peer)).mixed.voxels.length).toBe(3);
  const after=await state(page);expect(after.mixed.voxels.every((v:any)=>v.position.y===29)).toBe(true);expect(after.tick).toBe(read.tick);expect(after.cash).toBe(read.cash);
  const bytes=await save(page);await page.reload();await ready(page);expect(await save(page)).toEqual(bytes);
 }finally{await peer.close();await replace(page,original);}
});

test('two map picks keep one level and a mode change discards an in-flight line preview',async({page})=>{
 await page.goto('/');await ready(page);const original=await save(page);let release=()=>{};
 try{
  await empty(page);await build(page,'Track',[30,27],[34,27]);await remove(page).click();await page.getByLabel('Removal selection',{exact:true}).selectOption('line');
  // Canonical centres on this 30 by 42 map, including the current camera/zoom.
  await click(page,16*(30+27-20)+16,8*(-30+27+20)-16+8);await expect(page.getByLabel('Start X',{exact:true})).toHaveValue('30');
  await expect(page.getByLabel('Removal level',{exact:true})).toHaveValue('1');
  await click(page,16*(34+27-20)+16,8*(-34+27+20)-16+8);await expect(confirm(page)).toBeEnabled();await expect(page.locator('canvas')).toHaveAttribute('data-removal-path-cells','5');
  const before=await save(page);await page.getByLabel('Removal selection',{exact:true}).selectOption('single');await expect(confirm(page)).toBeDisabled();await expect(page.locator('canvas')).toHaveAttribute('data-removal-path-cells','0');
  let intercepted!:()=>void;const received=new Promise<void>(r=>intercepted=r),waiting=new Promise<void>(r=>release=r);
  await page.route('**/api/removal/preview?**',async route=>{const response=await route.fetch();intercepted();await waiting;await route.fulfill({response});});
  await page.getByLabel('Removal selection',{exact:true}).selectOption('line');
  for(const [label,value] of [['Start X',30],['Start Y',27],['End X',34],['End Y',27],['Removal level',1]] as const)await page.getByLabel(label,{exact:true}).fill(String(value));
  await page.getByRole('button',{name:'Preview removal',exact:true}).click();await received;await page.getByLabel('Removal selection',{exact:true}).selectOption('single');release();
  await expect(confirm(page)).toBeDisabled();await expect(page.locator('canvas')).toHaveAttribute('data-removal-path-cells','0');expect(await save(page)).toEqual(before);
 }finally{release();await replace(page,original);}
});

test('Japanese narrow two-point removal accepts keyboard coordinates and confirms the complete line',async({browser})=>{
 const context=await browser.newContext({viewport:{width:390,height:844},locale:'ja-JP',deviceScaleFactor:2}),page=await context.newPage();
 await page.goto('/');await ready(page,'ja');const original=await save(page);
 try{
  const s=await state(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:mixed})).ok()).toBe(true);await ready(page,'ja');
  await previewRemovalLine(page,[1,0],[1,2],4,true);await expect(page.locator('canvas')).toHaveAttribute('data-removal-path-cells','3');
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  const dir=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'remove');mkdirSync(dir,{recursive:true});await page.screenshot({path:join(dir,'remove-line-ja-narrow.png'),fullPage:true});
  await page.locator('canvas').focus();await page.keyboard.press('Enter');await expect(page.locator('.command-feedback')).toContainText('サーバーで確定');
  await expect.poll(async()=>(await state(page)).mixed.voxels.length).toBe(1);
 }finally{const s=await state(page);await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:original});await context.close();}
});

test('Remove picks one rail with its pier, previews/cancels, then confirms once in both peers and survives reload',async({page,browser})=>{
 await page.goto('/');await ready(page);const original=await save(page),peer=await browser.newPage();
 let commands=0;await page.routeWebSocket('**/api/session?*',route=>{const server=route.connectToServer();route.onMessage(m=>{if(JSON.parse(String(m)).payload?.kind==='removeObject')commands++;server.send(m);});});
 try{
  await replace(page,mixed);await page.reload();await ready(page);await peer.goto('/');await ready(peer);
  await remove(page).click();await expect(page.getByRole('heading',{name:'Remove',exact:true})).toBeVisible();
  await expect(page.getByLabel('Track action',{exact:true})).toHaveCount(0);
  await click(page,16.5,-55.5);await expect(confirm(page)).toBeEnabled();await expect(page.getByTestId('remove-target')).toContainText('Railway');
  await expect(page.locator('.remove-task li')).toHaveCount(2);const before=await save(page),read=await state(page);
  await page.getByRole('button',{name:'Cancel preview',exact:true}).click();await expect(confirm(page)).toBeDisabled();expect(await save(page)).toEqual(before);
  await click(page,16.5,-55.5);await expect(confirm(page)).toBeEnabled();await page.keyboard.press('Enter');
  await expect.poll(async()=>(await state(peer)).mixed.voxels.length).toBe(2);await expect(confirm(page)).toBeDisabled();
  await page.keyboard.press('Enter');expect(commands).toBe(1);const after=await state(page);
  expect(after.mixed.voxels.some((v:any)=>['7','8'].includes(v.ownerId))).toBe(false);expect(after.cash).toBe(read.cash);expect(after.tick).toBe(read.tick);expect(after.terrain).toEqual(read.terrain);
  const removed=await save(page);await page.reload();await ready(page);expect(await save(page)).toEqual(removed);
 }finally{await peer.close();await replace(page,original);}
});

test('Remove clears a forest while preserving its terrain and rejects bare ground with terrain-tool guidance',async({page})=>{
 await page.goto('/');await ready(page);const original=await save(page);
 try{
  await replace(page,terrain);await revealForest(page);const before=await state(page);await remove(page).click();
  await click(page,0,-58);await expect(confirm(page)).toBeEnabled();await expect(page.getByTestId('remove-target')).toContainText('Forest');
  expect(await state(page)).toEqual(before);await confirm(page).click();await expect(page.locator('.command-feedback')).toContainText('Confirmed by server');await expect(confirm(page)).toBeDisabled();
  const after=await state(page);expect(after.terrain.occupants.every((o:any)=>o.forest===null)).toBe(true);
  expect(after.terrain.cells).toEqual(before.terrain.cells);expect(after.mixed).toBeUndefined();expect(after.tick).toBe(before.tick);
  await click(page,16,-33);await expect(confirm(page)).toBeDisabled();await expect(page.getByTestId('remove-status')).toContainText('Modify terrain');
 }finally{await replace(page,original);}
});

test('peer removal invalidates an object preview and late responses cannot restore its confirmation',async({page,browser})=>{
 await page.goto('/');await ready(page);const original=await save(page),peer=await browser.newPage();let release=()=>{};
 try{
  await replace(page,mixed);await peer.goto('/');await ready(peer);await remove(page).click();await remove(peer).click();
  await click(page,16.5,-55.5);await expect(confirm(page)).toBeEnabled();
  const waiting=new Promise<void>(r=>release=r);let intercepted!:()=>void;const received=new Promise<void>(r=>intercepted=r);
  await page.route('**/api/removal/preview?**',async route=>{const response=await route.fetch();intercepted();await waiting;await route.fulfill({response});});
  await click(page,16.5,-55.5);await received;
  await click(peer,16.5,-40.5);await expect(confirm(peer)).toBeEnabled();await expect(peer.getByTestId('remove-target')).toContainText('Pier');await confirm(peer).click();
  await expect(page.getByTestId('remove-status')).toContainText('world changed');release();await expect(confirm(page)).toBeDisabled();
  expect((await state(page)).mixed.voxels.some((v:any)=>v.ownerId==='7')).toBe(true);
  await page.unroute('**/api/removal/preview?**');await click(page,16.5,-55.5);await expect(confirm(page)).toBeEnabled();await expect(page.locator('.remove-task li')).toHaveCount(1);
 }finally{release();await peer.close();await replace(page,original);}
});

test('Escape and panels discard object drafts; observers cannot activate Remove',async({page,browser})=>{
 await page.goto('/');await ready(page);const original=await save(page),observer=await browser.newPage();
 try{
  await replace(page,mixed);await remove(page).click();await click(page,16.5,-55.5);await expect(confirm(page)).toBeEnabled();const before=await save(page);
  await page.keyboard.press('Escape');await expect(confirm(page)).toBeDisabled();await expect(remove(page)).toHaveAttribute('aria-pressed','true');
  await page.keyboard.press('Escape');await expect(remove(page)).toHaveAttribute('aria-pressed','false');
  await remove(page).click();await click(page,16.5,-55.5);await expect(confirm(page)).toBeEnabled();await openPanel(page,'help');await closePanel(page);await expect(confirm(page)).toBeDisabled();expect(await save(page)).toEqual(before);
  expect((await observer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);await observer.goto('/');await ready(observer);await expect(remove(observer)).toBeDisabled();
 }finally{await observer.close();await replace(page,original);}
});

test('Japanese narrow Remove keeps selection reachable and confirms a forest with Return',async({browser})=>{
 const context=await browser.newContext({viewport:{width:390,height:844},locale:'ja-JP',deviceScaleFactor:2}),page=await context.newPage();
 await page.goto('/');await ready(page,'ja');const original=await save(page);
 try{
  const s=await state(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:terrain})).ok()).toBe(true);await ready(page,'ja');await expect(page.locator('canvas')).toHaveAttribute('data-assets','ready');
  await revealForest(page,true);await remove(page,true).click();expect(await page.evaluate(()=>scrollX)).toBe(0);await click(page,0,-58);await expect(confirm(page,true)).toBeEnabled();await expect(page.getByTestId('remove-target')).toContainText('森林');
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  const dir=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'remove');mkdirSync(dir,{recursive:true});await page.screenshot({path:join(dir,'remove-ja-narrow.png'),fullPage:true});
  await page.keyboard.press('Enter');await expect(page.locator('.command-feedback')).toContainText('サーバーで確定');await expect(confirm(page,true)).toBeDisabled();expect((await state(page)).terrain.occupants.every((o:any)=>o.forest===null)).toBe(true);
 }finally{const s=await state(page);await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:original});await context.close();}
});
