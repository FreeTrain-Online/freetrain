import { expect,test,type Page } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { createHash } from 'node:crypto';
import { ready,openPanel,closePanel } from './ux-helpers.ts';
const bootPath='/api/bootstrap?protocolVersion=3&contentVersion=reference-spatial-content-2';
const bootstrap=async(page:Page)=>(await(await page.request.get(bootPath)).json()).snapshot;
async function at(page:Page,p:{x:number;y:number;z:number}) {
  return page.locator('canvas').evaluate((el,p)=>{
    const canvas=el as HTMLCanvasElement,r=canvas.getBoundingClientRect(),[cx,cy]=canvas.dataset.camera!.split(',').map(Number),zoom=Number(canvas.dataset.zoom);
    return{x:r.x+r.width/2+(16*(p.x+p.y-31)+16-cx)*zoom,y:r.y+r.height/2+(8*(p.y-p.x+31)-16*p.z+8-cy)*zoom};
  },p);
}
async function target(page:Page,p:{x:number;y:number;z:number}){const q=await at(page,p);await page.mouse.click(q.x,q.y);}
async function checker(page:Page){await page.getByRole('button',{name:'Check a location',exact:true}).click();await page.getByLabel('Track level',{exact:true}).selectOption('0');}

test('prepared bundled catalog and decoded source sheets ship separately from the selected reference world',async({page})=>{
  await page.goto('/');await ready(page);const before=await bootstrap(page),canvas=page.locator('canvas');
  await canvas.evaluate(el=>el.setAttribute('data-instance','prepared-preserved'));
  const response=await page.request.get('/content/bundled-v2/catalog.json');expect(response.ok()).toBeTruthy();
  const bytes=await response.body(),catalog=JSON.parse(bytes.toString());
  const hash=(await(await page.request.get('/content/bundled-v2/catalog.sha256')).text()).trim();
  expect(createHash('sha256').update(bytes).digest('hex')).toBe(hash);
  expect(catalog.source.revision).toBe('883cf7d81cf49db25ff38cd3a25d0f140afe31a0');
  expect(catalog.counts.contributions).toBe(448);expect(catalog.status).toBe('prepared; not selected by a world profile');
  const rail=catalog.assets.find((a:any)=>a.path==='trunk/core/res/RailRoads.bmp');
  for(const mode of ['day','night']){
    const image=await page.request.get(rail[mode].url);expect(image.ok()).toBeTruthy();
    expect(createHash('sha256').update(await image.body()).digest('hex')).toBe(rail[mode].sha256);
    expect(await page.evaluate(async url=>{const image=new Image();image.src=url;await image.decode();return [image.naturalWidth,image.naturalHeight];},rail[mode].url)).toEqual([rail.width,rail.height]);
  }
  expect(before.requiredPacks.every((p:any)=>p.id!=='exav-stable-bundled')).toBeTruthy();
  expect(await bootstrap(page)).toEqual(before);await expect(canvas).toHaveAttribute('data-instance','prepared-preserved');
});

test('default live H/V map renders, picks source rails and keeps unsupported operations unavailable',async({page})=>{
  const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
  await page.goto('/');await ready(page);
  const canvas=page.locator('canvas');await expect(canvas).toHaveAttribute('data-coordinate-system','hv');await expect(canvas).toHaveAttribute('data-ruleset','reference-spatial-4');
  await expect(page.getByTestId('reference-world')).toHaveText('Spatial reference world');
  await expect(page.getByRole('button',{name:'Run',exact:false})).toBeEnabled();await expect(page.getByRole('button',{name:'Remove track',exact:true})).toBeDisabled();
  const before=await bootstrap(page);expect(before.tracks).toHaveLength(32);expect(before.trains).toHaveLength(0);
  expect(before.entities).toHaveLength(32);
  for(const track of before.tracks){const entity=before.entities.find((e:any)=>e.id===track.entityId);expect(entity.anchor).toEqual(track.position);expect(entity.definition.recipeId).toBe(track.shape);}
  await canvas.evaluate(el=>el.setAttribute('data-instance','canonical-preserved'));
  await target(page,{x:31,y:24,z:0});await expect(page.getByTestId('selected-object')).toHaveText(`Track · ${before.tracks.find((t:any)=>t.position.x===31&&t.position.y===24).entityId} · 31, 24, 0`);await expect(page.locator('.primary-panel')).toContainText('Normal rail pattern 6');
  await openPanel(page,'session');await expect(page.locator('.primary-panel')).toContainText('Economy, trains and construction are not available yet.');
  await expect(canvas).toHaveAttribute('data-instance','canonical-preserved');expect(await bootstrap(page)).toEqual(before);expect(errors).toEqual([]);
});

test('source H/V targeting checks eight-direction snapping without changing the world',async({page})=>{
  await page.goto('/');await ready(page);const before=await bootstrap(page);await checker(page);
  await target(page,{x:27,y:24,z:0});await expect(page.getByTestId('geometry-status')).toContainText('within the map');
  await page.getByRole('button',{name:'Use as alignment anchor',exact:true}).click();await page.getByLabel('Align target',{exact:true}).selectOption('8');
  await target(page,{x:29,y:29,z:0});await expect(page.getByTestId('preview-position')).toHaveText('29, 26, 0');await expect(page.getByTestId('preview-heading')).toContainText('South-east');
  await expect(page.getByRole('button',{name:'Confirm track change',exact:true})).toBeDisabled();expect(await bootstrap(page)).toEqual(before);
  const directory=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'canonical');mkdirSync(directory,{recursive:true});await page.screenshot({path:join(directory,'reference-en.png'),fullPage:true});
});

test('real Save 5 download, upload confirmation and socket epoch replacement preserve the canvas',async({page})=>{
  await page.goto('/');await ready(page);const before=await bootstrap(page),canvas=page.locator('canvas');await canvas.evaluate(el=>el.setAttribute('data-instance','save-preserved'));
  const save=await(await page.request.get('/api/saves/download')).body();expect(JSON.parse(save.toString()).saveVersion).toBe(5);
  await openPanel(page,'saves');await expect(page.getByRole('button',{name:'Save slot',exact:true})).toBeEnabled();
  await page.getByLabel('Upload save',{exact:true}).setInputFiles({name:'canonical.json',mimeType:'application/json',buffer:save});
  await expect(page.getByRole('dialog')).toBeVisible();await page.getByRole('button',{name:'Confirm load',exact:true}).click();
  await expect(canvas).toHaveAttribute('data-world-epoch',String(BigInt(before.worldEpoch)+1n));await expect(canvas).toHaveAttribute('data-instance','save-preserved');
  expect(await(await page.request.get('/api/saves/download')).body()).toEqual(save);
});

test('prototype and corrupt saves report errors without replacement confirmation or mutation',async({page})=>{
  await page.goto('/');await ready(page);const before=await bootstrap(page);await openPanel(page,'saves');
  for(const [body,message] of [['{"saveVersion":1}','not supported'],['{"saveVersion":2}','not supported'],['{"saveVersion":3}','not supported'],['{"saveVersion":4}','not supported'],['{broken','Invalid save data']] as const){
    await expect(page.getByLabel('Upload save',{exact:true})).toBeEnabled();
    await page.getByLabel('Upload save',{exact:true}).setInputFiles({name:'unsupported.json',mimeType:'application/json',buffer:Buffer.from(body)});
    await expect(page.getByTestId('save-feedback')).toContainText(message);await expect(page.getByRole('dialog',{name:'Replace the current game with this save?',exact:true})).toHaveCount(0);
    expect(await bootstrap(page)).toEqual(before);
  }
});

test('random state validation rejects incompatible saves before confirmation and keeps the current epoch',async({page})=>{
  await page.goto('/');await ready(page);await openPanel(page,'saves');
  const before=await bootstrap(page),bytes=await(await page.request.get('/api/saves/download')).body();
  for(const [mutation,message] of [['algorithm','not supported'],['duplicate','Invalid save data'],['zero','Invalid save data']] as const){
    const save=JSON.parse(bytes.toString());
    if(mutation==='algorithm')save.random.algorithm='future';
    if(mutation==='duplicate')save.random.streams[1].id=save.random.streams[0].id;
    if(mutation==='zero')save.random.streams[0].state.values.fill(0);
    await expect(page.getByLabel('Upload save',{exact:true})).toBeEnabled();
    await page.getByLabel('Upload save',{exact:true}).setInputFiles({name:'random.json',mimeType:'application/json',buffer:Buffer.from(JSON.stringify(save))});
    await expect(page.getByTestId('save-feedback')).toContainText(message);
    await expect(page.getByRole('dialog',{name:'Replace the current game with this save?',exact:true})).toHaveCount(0);
    expect(await bootstrap(page)).toEqual(before);
    expect(await(await page.request.get('/api/saves/download')).body()).toEqual(bytes);
  }
});

test('new epoch clears a pending H/V preview and discards its delayed response',async({page})=>{
  let release=()=>{},waiting=false;const held=new Promise<void>(r=>{release=r;});
  await page.route('**/api/spatial/preview?*',async route=>{try{const response=await route.fetch();waiting=true;await held;await route.fulfill({response});}catch{}});
  try{
    await page.goto('/');await ready(page);await checker(page);await target(page,{x:27,y:24,z:0});await expect.poll(()=>waiting).toBe(true);
    const before=await bootstrap(page),save=await(await page.request.get('/api/saves/download')).body();
    expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+before.worldEpoch,{data:save})).ok()).toBe(true);
    await expect(page.locator('canvas')).toHaveAttribute('data-world-epoch',String(BigInt(before.worldEpoch)+1n));release();await expect(page.getByTestId('preview-position')).toHaveCount(0);
  }finally{release();}
});

test('unknown canonical content pack blocks reveal before the live canvas',async({page})=>{
  await page.route('**/api/bootstrap?*',async route=>{const response=await route.fetch(),body=await response.json();body.requiredPacks[0].catalogHash='0'.repeat(64);body.snapshot.requiredPacks[0].catalogHash='0'.repeat(64);await route.fulfill({response,json:body});});
  await page.goto('/');await expect(page.getByRole('heading',{name:'Update required',exact:true})).toBeVisible();await expect(page.locator('canvas')).toHaveAttribute('data-scene','demo');
});

test('canonical Japanese presentation fits narrow DPR 2 and retains canvas through panels',async({browser})=>{
  const context=await browser.newContext({viewport:{width:640,height:1000},deviceScaleFactor:2,locale:'ja-JP'});
  try{
    const page=await context.newPage();await page.goto('/');await ready(page,'ja');await expect(page.getByTestId('reference-world')).toHaveText('空間参照ワールド');
    await openPanel(page,'session','ja');await expect(page.locator('.primary-panel')).toContainText('8方向の環状線');await closePanel(page);
    await page.getByRole('button',{name:'ゲーム ▾',exact:true}).click();
    await expect(page.getByTestId('menu-game-time')).toContainText('年4月1日(土)');
    await page.keyboard.press('Escape');
    await page.getByRole('button',{name:'位置を確認',exact:true}).click();await page.getByLabel('線路の高さ',{exact:true}).selectOption('0');
    await target(page,{x:27,y:24,z:0});await expect(page.getByTestId('geometry-status')).toContainText('対象はマップの範囲内です。');
    await page.getByRole('button',{name:'方向合わせの基準にする',exact:true}).click();await page.getByLabel('対象の方向合わせ',{exact:true}).selectOption('8');
    await target(page,{x:29,y:29,z:0});await expect(page.getByTestId('preview-heading')).toContainText('南東');
    expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
    const directory=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'canonical');mkdirSync(directory,{recursive:true});await page.screenshot({path:join(directory,'reference-ja-2x.png'),fullPage:true});
  }finally{await context.close();}
});

test('canonical Observer can inspect and query geometry while save endpoints enforce roles',async({browser})=>{
  const context=await browser.newContext();
  try{
    const page=await context.newPage();expect((await page.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);
    await page.goto('/');await ready(page);await expect(page.getByTestId('participant-role')).toHaveText('Observer');
    const before=await bootstrap(page);
    const response=await page.request.post('/api/spatial/preview?protocolVersion=3&contentVersion=reference-spatial-content-2',{data:{expectedWorldEpoch:before.worldEpoch,position:{x:27,y:24,z:0},anchor:null,snap:0}});
    expect(response.ok()).toBe(true);expect((await response.json()).storage).toEqual({h:10,v:28,z:0});
    expect((await page.request.post('/api/saves/slot1/save?expectedWorldEpoch='+before.worldEpoch)).status()).toBe(403);
    expect((await page.request.get('/api/saves/download')).status()).toBe(403);
    expect(await bootstrap(page)).toEqual(before);
  }finally{await context.close();}
});

test('save availability must be known before upload, and Cancel never sends a replacement',async({page})=>{
  let release=()=>{},waiting=false,uploads=0;const held=new Promise<void>(r=>{release=r;});
  await page.route('**/api/saves/status',async route=>{const response=await route.fetch();waiting=true;await held;await route.fulfill({response});});
  page.on('request',request=>{if(new URL(request.url()).pathname==='/api/saves/upload')uploads++;});
  try{
    await page.goto('/');await ready(page);const before=await bootstrap(page),save=await(await page.request.get('/api/saves/download')).body();
    await openPanel(page,'saves');await expect.poll(()=>waiting).toBe(true);
    await expect(page.getByLabel('Upload save',{exact:true})).toBeDisabled();
    release();await expect(page.getByLabel('Upload save',{exact:true})).toBeEnabled();
    await page.getByLabel('Upload save',{exact:true}).setInputFiles({name:'canonical.json',mimeType:'application/json',buffer:save});
    const modal=page.getByRole('dialog',{name:'Replace the current game with this save?',exact:true});await expect(modal).toBeVisible();
    expect(uploads).toBe(0);await modal.getByRole('button',{name:'Cancel',exact:true}).click();await expect(modal).toHaveCount(0);
    expect(uploads).toBe(0);expect(await bootstrap(page)).toEqual(before);
  }finally{release();}
});

test('canonical clock step, shared run/pause, panel navigation and Save 5 continuation',async({page,browser})=>{
  const observerContext=await browser.newContext();
  try{
    await page.goto('/');await ready(page);
    const original=await(await page.request.get('/api/saves/download')).json();
    const before=await bootstrap(page);
    const save={...original,clock:{absoluteMinute:'130079',nextOrder:'0',pending:[]},paused:true};
    expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+before.worldEpoch,{data:save})).ok()).toBe(true);
    await expect(page.getByTestId('game-time')).toHaveText('Year 1 · 4/1 (Sat) 07:50');
    await page.getByRole('button',{name:'Step',exact:false}).first().click();
    await expect(page.getByTestId('game-time')).toHaveText('Year 1 · 4/1 (Sat) 08:00');
    expect((await bootstrap(page)).tick).toBe('130080');
    const observer=await observerContext.newPage();
    expect((await observer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);
    await observer.goto('/');await ready(observer);
    await expect(observer.getByRole('button',{name:'Run',exact:false})).toBeDisabled();
    await page.getByRole('button',{name:'Run',exact:false}).click();
    await expect(observer.getByTestId('clock-state')).toHaveText('Running');
    const running=await bootstrap(page);await openPanel(page,'settings');
    await expect.poll(async()=>BigInt((await bootstrap(page)).tick)>BigInt(running.tick)).toBe(true);
    await page.getByRole('button',{name:'Pause',exact:false}).click();
    await expect(observer.getByTestId('clock-state')).toHaveText('Paused');
    const captured=await(await page.request.get('/api/saves/download')).json();
    expect(captured.paused).toBe(true);expect(captured.clock.absoluteMinute).toBe((await bootstrap(page)).tick);
    const paused=await bootstrap(page);
    expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+paused.worldEpoch,{data:captured})).ok()).toBe(true);
    await expect(page.locator('canvas')).toHaveAttribute('data-world-epoch',String(BigInt(paused.worldEpoch)+1n));
    expect((await bootstrap(page)).tick).toBe(captured.clock.absoluteMinute);
  }finally{
    const pause=page.getByRole('button',{name:'Pause',exact:false});if(await pause.count())await pause.click();
    await observerContext.close();
  }
});

test('dangling entity ownership blocks reveal before the live canvas',async({page})=>{
  await page.route('**/api/bootstrap?*',async route=>{
    const response=await route.fetch(),body=await response.json();body.snapshot.tracks[0].entityId='999';
    await route.fulfill({response,json:body});
  });
  await page.goto('/');await expect(page.getByRole('heading',{name:'Update required',exact:true})).toBeVisible();
  await expect(page.locator('canvas')).toHaveAttribute('data-scene','demo');
});

test('invalid entity graphs report localized save errors before confirmation and preserve current state',async({page})=>{
  await page.goto('/');await ready(page);await openPanel(page,'saves');
  const before=await bootstrap(page),bytes=await(await page.request.get('/api/saves/download')).body();
  for(const mutation of ['owner','duplicate','allocator'] as const){
    const save=JSON.parse(bytes.toString());
    if(mutation==='owner')save.rails[0].entityId='999';
    if(mutation==='duplicate')save.spatial.entities[1].id=save.spatial.entities[0].id;
    if(mutation==='allocator')save.spatial.nextEntityId='1';
    await expect(page.getByLabel('Upload save',{exact:true})).toBeEnabled();
    await page.getByLabel('Upload save',{exact:true}).setInputFiles({name:'entities.json',mimeType:'application/json',buffer:Buffer.from(JSON.stringify(save))});
    await expect(page.getByTestId('save-feedback')).toContainText('Invalid save data');
    await expect(page.getByRole('dialog',{name:'Replace the current game with this save?',exact:true})).toHaveCount(0);
    expect(await bootstrap(page)).toEqual(before);expect(await(await page.request.get('/api/saves/download')).body()).toEqual(bytes);
  }
});
