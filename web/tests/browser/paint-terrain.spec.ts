import {test,expect,type Page} from '@playwright/test';
import {ready,openPanel,closePanel} from './ux-helpers.ts';
const boot='/api/bootstrap?protocolVersion=5&contentVersion=reference-spatial-content-2&contentVersion=terrain-content-1';
const state=async(page:Page)=>(await(await page.request.get(boot)).json()).snapshot;
const save=async(page:Page)=>(await page.request.get('/api/saves/download')).body();
async function empty(page:Page,locale="en"){
 const s=await state(page),r=await page.request.post('/api/worlds/empty/preview',{data:{name:'Paint terrain',dimensions:{h:30,v:42,z:6},waterLevel:1,epoch:s.worldEpoch,revision:s.revision}});expect(r.ok()).toBe(true);const c=await r.json();
 expect((await page.request.post('/api/worlds/empty/commit',{data:{id:c.id,requestHash:c.requestHash,candidateHash:c.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).ok()).toBe(true);await ready(page,locale);
}
async function tool(page:Page,ja=false){await page.getByRole('button',{name:ja?'地形操作':'Modify terrain',exact:true}).click();await page.getByLabel(ja?'地形の選択方法':'Terrain selection',{exact:true}).selectOption('paint');}
async function pixel(page:Page,x=20,y=20,z=1){return page.locator('canvas').evaluate((canvas,p)=>{
 const [cx,cy]=canvas.dataset.camera!.split(',').map(Number),zoom=Number(canvas.dataset.zoom),r=canvas.getBoundingClientRect();
 return{x:r.x+r.width/2+(16*(p.x+p.y-20)+16-cx)*zoom,y:r.y+r.height/2+(8*(-p.x+p.y+20)-16*p.z+2-cy)*zoom};
},{x,y,z});}
async function revision(page:Page){return Number((await state(page)).terrain.geometryRevision);}
async function monitorFrames(page:Page){
 await expect(page.locator('canvas')).toHaveAttribute('data-assets','ready');
 await page.locator('canvas').evaluate(canvas=>{
  const sample=document.createElement('canvas');sample.width=32;sample.height=32;const ctx=sample.getContext('2d')!;
  canvas.dataset.paintMonitor='active';canvas.dataset.paintFrames='0';canvas.dataset.blankFrames='0';
  const frame=()=>{
   if(canvas.dataset.paintMonitor!=='active')return;
   ctx.drawImage(canvas,0,0,32,32);const pixels=ctx.getImageData(0,0,32,32).data;
   let terrain=0;for(let i=0;i<pixels.length;i+=4)if(pixels[i]!==216||pixels[i+1]!==229||pixels[i+2]!==217)terrain++;
   canvas.dataset.paintFrames=String(Number(canvas.dataset.paintFrames)+1);
   if(!terrain)canvas.dataset.blankFrames=String(Number(canvas.dataset.blankFrames)+1);
   requestAnimationFrame(frame);
  };requestAnimationFrame(frame);
 });
}
async function restore(page:Page,bytes:Buffer){await page.mouse.up();const s=await state(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:bytes})).ok()).toBe(true);}
test('held painting and Shift reversal keep terrain visible across every revision, dragging and Lower work, and release stops',async({page})=>{
 await page.goto('/');await ready(page);const original=await save(page);
 try{
  await empty(page);await tool(page);await page.locator('canvas').evaluate(c=>c.dataset.instance='paint-kept');const p=await pixel(page);await monitorFrames(page);
  await page.mouse.move(p.x,p.y);await page.mouse.down();await expect.poll(()=>revision(page)).toBeGreaterThanOrEqual(2);
  const other=await pixel(page,24,20);await page.mouse.move(other.x,other.y);const before=await revision(page);await expect.poll(()=>revision(page)).toBeGreaterThan(before);
  await page.mouse.up();await expect(page.getByTestId('paint-status')).toHaveAttribute('data-active','false');
  await expect(page.locator('.command-feedback')).toContainText('Confirmed');const stopped=await revision(page);await page.waitForTimeout(350);expect(await revision(page)).toBe(stopped);
  const raised=await state(page);expect(raised.terrain.occupants.some((o:any)=>o.ne>0)).toBe(true);expect(raised.paused).toBe(true);await page.screenshot({path:test.info().outputPath('paint-en.png'),fullPage:true});
  const sum=(s:any)=>s.terrain.cells.reduce((n:number,c:any)=>n+16*c.ground,0)+s.terrain.occupants.reduce((n:number,o:any)=>n+o.ne+o.se+o.sw+o.nw,0);
  await page.keyboard.down('Shift');await page.mouse.move(other.x,other.y);await page.mouse.down();
  await expect.poll(async()=>sum(await state(page))).toBeLessThan(sum(raised));
  await page.mouse.up();await page.keyboard.up('Shift');const reversed=await state(page);
  await page.getByLabel('Terrain action',{exact:true}).selectOption('lower');await page.mouse.move(other.x,other.y);await page.mouse.down();
  await expect.poll(async()=>sum(await state(page))).toBeLessThan(sum(reversed));
  await expect.poll(()=>revision(page)).toBeGreaterThan(stopped);await page.mouse.up();
  const frames=await page.locator('canvas').evaluate(c=>{c.dataset.paintMonitor='stopped';return {count:Number(c.dataset.paintFrames),blank:Number(c.dataset.blankFrames)};});
  expect(frames.count).toBeGreaterThan(3);expect(frames.blank).toBe(0);
  await expect(page.locator('canvas')).toHaveAttribute('data-instance','paint-kept');const bytes=await save(page);await page.reload();await ready(page);expect(await save(page)).toEqual(bytes);
 }finally{await restore(page,original);}
});
test('release cancels a delayed preview, Escape and focus loss stop held painting, and middle drag only pans',async({page})=>{
 await page.goto('/');await ready(page);const original=await save(page);
 let release=()=>{};
 try{
  await empty(page);await tool(page);const p=await pixel(page);let received=false;const gate=new Promise<void>(r=>release=r);
  await page.route('**/api/terrain/corner/preview?**',async route=>{received=true;const response=await route.fetch();await gate;await route.fulfill({response}).catch(()=>{});});
  await page.mouse.move(p.x,p.y);await page.mouse.down();await expect.poll(()=>received).toBe(true);await page.mouse.up();release();await page.unrouteAll({behavior:'wait'});await page.waitForTimeout(250);expect(await revision(page)).toBe(0);
  await page.mouse.down();await expect.poll(()=>revision(page)).toBeGreaterThan(0);await page.keyboard.press('Escape');await expect(page.getByTestId('paint-status')).toHaveAttribute('data-active','false');await page.mouse.up();
  const atEscape=await revision(page);await page.waitForTimeout(350);expect(await revision(page)).toBe(atEscape);
  await page.mouse.move(p.x+90,p.y);await page.mouse.down();await page.evaluate(()=>window.dispatchEvent(new Event('blur')));await page.mouse.up();await expect(page.getByTestId('paint-status')).toHaveAttribute('data-active','false');
  const before=await revision(page);await page.mouse.down({button:'middle'});await page.mouse.move(p.x+120,p.y+20);await page.mouse.up({button:'middle'});await page.waitForTimeout(250);expect(await revision(page)).toBe(before);
  await openPanel(page,'help');await closePanel(page);await expect(page.getByLabel('Terrain selection',{exact:true})).toHaveValue('corner');
 }finally{release();await restore(page,original);}
});
test('Japanese narrow paint has accessible action/coordinate alternatives and shared time continues',async({browser})=>{
 const context=await browser.newContext({locale:'ja-JP',viewport:{width:390,height:844},deviceScaleFactor:2}),page=await context.newPage();await page.goto('/');await ready(page,'ja');const original=await save(page);
 try{
  await empty(page,'ja');await page.getByRole('button',{name:'運転',exact:false}).first().click();await tool(page,true);await expect(page.getByTestId('paint-status')).toContainText('ドラッグ');
  for(const [label,value] of [['隅のセル X',20],['隅のセル Y',20],['隅のセル Z',1]] as const)await page.getByLabel(label,{exact:true}).fill(String(value));
  await page.getByRole('button',{name:'隅をプレビュー',exact:true}).click();await expect(page.getByRole('button',{name:'地形の変更を確定',exact:true})).toBeEnabled();await page.getByRole('button',{name:'地形の変更を確定',exact:true}).focus();await page.keyboard.press('Enter');await expect.poll(()=>revision(page)).toBe(1);
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);expect((await state(page)).paused).toBe(false);await page.screenshot({path:test.info().outputPath('paint-ja.png'),fullPage:true});
 }finally{await restore(page,original);await context.close();}
});
