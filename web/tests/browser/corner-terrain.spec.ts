import {test,expect,type Page} from '@playwright/test';
import {ready,openPanel,closePanel,canonicalState} from './ux-helpers.ts';
import {mkdirSync} from 'node:fs';
import {join} from 'node:path';
const compat='protocolVersion=6&contentVersion=terrain-content-1';
const state=canonicalState;
async function replace(page:Page,bytes:Buffer){const s=await state(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:bytes})).ok()).toBe(true);}
async function empty(page:Page,locale='en'){
 const s=await state(page);const response=await page.request.post('/api/worlds/empty/preview',{data:{name:'Corners',dimensions:{h:30,v:42,z:6},waterLevel:1,epoch:s.worldEpoch,revision:s.revision}});expect(response.ok()).toBe(true);
 const c=await response.json();expect((await page.request.post('/api/worlds/empty/commit',{data:{id:c.id,requestHash:c.requestHash,candidateHash:c.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).ok()).toBe(true);
 await expect(page.locator('canvas')).toHaveAttribute('data-ruleset','terrain-state-1');await ready(page,locale);
}
async function target(page:Page,locale='en'){
 await page.getByLabel(locale==='ja'?'隅のセル X':'Corner cell X',{exact:true}).fill('20');
 await page.getByLabel(locale==='ja'?'隅のセル Y':'Corner cell Y',{exact:true}).fill('20');
 await page.getByLabel(locale==='ja'?'隅のセル Z':'Corner cell Z',{exact:true}).fill('1');
 await page.getByRole('button',{name:locale==='ja'?'隅をプレビュー':'Preview corner',exact:true}).click();
 await expect(page.getByTestId('corner-status')).toContainText(locale==='ja'?'変更できます':'change is legal');
}
async function pixel(page:Page,x:number,y:number,z:number,dx:number,dy:number){return page.locator('canvas').evaluate((canvas,args)=>{
 const [cx,cy]=(canvas as HTMLCanvasElement).dataset.camera!.split(',').map(Number),zoom=Number((canvas as HTMLCanvasElement).dataset.zoom),r=canvas.getBoundingClientRect();
 return {x:r.x+r.width/2+(16*(args.x+args.y-20)+args.dx-cx)*zoom,y:r.y+r.height/2+(8*(-args.x+args.y+20)-16*args.z+args.dy-cy)*zoom};
},{x,y,z,dx,dy});}

test('corner EN coordinate preview is read-only, confirms exactly once, preserves canvas and saves, then lowers after reopening',async({page})=>{
 await page.goto('/');await ready(page);const original=await(await page.request.get('/api/saves/download')).body();
 const errors:string[]=[];page.on('pageerror',error=>errors.push(String(error)));
 try{
  await empty(page);const canvas=page.locator('canvas');await canvas.evaluate(el=>el.setAttribute('data-instance','corner-canvas'));
  const before=await(await page.request.get('/api/saves/download')).body();
  await page.getByRole('button',{name:'Modify terrain',exact:true}).click();await target(page);
  expect(await(await page.request.get('/api/saves/download')).body()).toEqual(before);
  await expect(page.locator('.corner-changes li')).toHaveCount(4);
  await expect(page.locator('.tool-card')).toContainText('Cost: 0');
  await page.getByRole('button',{name:'Confirm terrain change',exact:true}).click();
  await expect.poll(async()=>JSON.parse((await(await page.request.get('/api/saves/download')).body()).toString()).packedTerrain.occupantCount).toBe(4);
  await expect(page.getByRole('button',{name:'Confirm terrain change',exact:true})).toBeDisabled();await expect(canvas).toHaveAttribute('data-instance','corner-canvas');
  const after=await(await page.request.get('/api/saves/download')).body();await page.reload();await ready(page);expect(await(await page.request.get('/api/saves/download')).body()).toEqual(after);
  await page.getByRole('button',{name:'Modify terrain',exact:true}).click();await page.getByLabel('Terrain action',{exact:true}).selectOption('lower');await target(page);
  await page.getByRole('button',{name:'Confirm terrain change',exact:true}).click();await expect.poll(async()=>JSON.parse((await(await page.request.get('/api/saves/download')).body()).toString()).packedTerrain.occupantCount).toBe(0);
  expect(errors).toEqual([]);
 }finally{await replace(page,original);}
});

test('pointer Ctrl reverses action; Shift stroke stops on release and blur, remains read-only until one confirmation; panels and Escape clear it',async({page})=>{
 await page.goto('/');await ready(page);const original=await(await page.request.get('/api/saves/download')).body();
 try{
  await empty(page);await page.getByRole('button',{name:'Modify terrain',exact:true}).click();
  // Pick the top half of a ground diamond. Its source-selected NE corner is (20,20).
  const p=await pixel(page,20,20,1,16,3);await page.keyboard.down('Control');await page.mouse.click(p.x,p.y);await page.keyboard.up('Control');
  await expect(page.getByTestId('corner-position')).toHaveText('20, 20, 1 · Lower corner');
  await page.getByRole('button',{name:'Cancel preview',exact:true}).click();
  const before=await(await page.request.get('/api/saves/download')).body();
  await page.keyboard.down('Shift');await page.mouse.move(p.x,p.y);await page.mouse.down();
  const next=await pixel(page,21,20,1,16,3);await page.mouse.move(next.x,next.y);await page.mouse.up();await page.keyboard.up('Shift');
  await expect(page.locator('.tool-card')).toContainText('Stroke sites: 2/64');
  await expect(page.getByTestId('corner-status')).toContainText('change is legal');
  await page.mouse.move(next.x+10,next.y);await expect(page.locator('.tool-card')).toContainText('Stroke sites: 2/64');
  expect(await(await page.request.get('/api/saves/download')).body()).toEqual(before);
  await page.evaluate(()=>window.dispatchEvent(new Event('blur')));await page.mouse.move(p.x,p.y);
  await expect(page.locator('.tool-card')).toContainText('Stroke sites: 2/64');
  await page.getByRole('button',{name:'Confirm terrain change',exact:true}).click();await expect.poll(async()=>JSON.parse((await(await page.request.get('/api/saves/download')).body()).toString()).packedTerrain.geometryRevision).toBe('1');
  await target(page);await page.keyboard.press('Escape');await expect(page.getByRole('button',{name:'Confirm terrain change',exact:true})).toBeDisabled();
  await target(page);await openPanel(page,'help');await closePanel(page);await expect(page.getByRole('button',{name:'Confirm terrain change',exact:true})).toBeDisabled();
 }finally{await replace(page,original);}
});

test('late previews cannot overwrite newer targets; remote edits invalidate review and observer preview has no mutation authority',async({page,browser})=>{
 await page.goto('/');await ready(page);const original=await(await page.request.get('/api/saves/download')).body();
 let release=()=>{};const held=new Promise<void>(r=>{release=r;});
 const peer=await browser.newPage();
 try{
  await empty(page);await page.getByRole('button',{name:'Modify terrain',exact:true}).click();
  let delayed=false;await page.route('**/api/terrain/corner/preview?*',async route=>{const response=await route.fetch();if(!delayed){delayed=true;await held;}await route.fulfill({response});});
  await page.getByLabel('Corner cell X',{exact:true}).fill('20');await page.getByLabel('Corner cell Y',{exact:true}).fill('20');
  await page.getByRole('button',{name:'Preview corner',exact:true}).click();await expect.poll(()=>delayed).toBe(true);
  await page.getByLabel('Corner cell X',{exact:true}).fill('21');await page.getByRole('button',{name:'Preview corner',exact:true}).click();await expect(page.getByTestId('corner-position')).toContainText('21, 20, 1');release();await expect(page.getByTestId('corner-position')).toContainText('21, 20, 1');
  await peer.goto('/');await ready(peer);await peer.getByRole('button',{name:'Modify terrain',exact:true}).click();await target(peer);await peer.getByRole('button',{name:'Confirm terrain change',exact:true}).click();
  await expect(page.getByTestId('corner-status')).toContainText('Terrain changed');await expect(page.getByRole('button',{name:'Confirm terrain change',exact:true})).toBeDisabled();
  const before=await state(page);const q={expectedWorldEpoch:before.worldEpoch,expectedGeometryRevision:before.terrain.geometryRevision,position:{x:20,y:20,z:1},action:'raise',offsetX:null,offsetY:null,stroke:null};
  expect((await page.request.post('/api/terrain/corner/preview?'+compat,{data:{...q,expectedGeometryRevision:'0'}})).status()).toBe(409);
  const viewer=await browser.newPage();try{
   expect((await viewer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);await viewer.goto('/');await ready(viewer);
   await expect(viewer.getByRole('button',{name:'Modify terrain',exact:true})).toBeDisabled();
   expect((await viewer.request.post('/api/terrain/corner/preview?'+compat,{data:q})).ok()).toBe(true);
  }finally{await viewer.close();}
 }finally{release();await peer.close();await replace(page,original);}
});

test('Japanese narrow terrain controls work by keyboard, scroll internally, and preserve canvas and shared pause state',async({browser})=>{
 const context=await browser.newContext({viewport:{width:390,height:844},locale:'ja-JP',deviceScaleFactor:2}),page=await context.newPage();
 await page.goto('/');await ready(page,'ja');const original=await(await page.request.get('/api/saves/download')).body();
 try{
  await empty(page,'ja');await openPanel(page,'help','ja');await closePanel(page);
  await page.getByRole('button',{name:'地形操作',exact:true}).focus();await page.keyboard.press('Enter');await target(page,'ja');
  await page.getByRole('button',{name:'座標をストロークに追加',exact:true}).click();
  await expect(page.locator('.tool-card')).toContainText('ストロークの地点: 2/64');
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  const path=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'corner-terrain');mkdirSync(path,{recursive:true});await page.screenshot({path:join(path,'corner-ja-narrow.png'),fullPage:true});
  await page.getByRole('button',{name:'地形の変更を確定',exact:true}).focus();await page.keyboard.press('Enter');
  await expect.poll(async()=>JSON.parse((await(await page.request.get('/api/saves/download')).body()).toString()).packedTerrain.occupantCount).toBe(4);
  expect((await state(page)).paused).toBe(true);
 }finally{await replace(page,original);await context.close();}
});
