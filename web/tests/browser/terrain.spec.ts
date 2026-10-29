import {test,expect,type Page} from '@playwright/test';
import {readFileSync,mkdirSync,writeFileSync} from 'node:fs';
import {join} from 'node:path';
import {ready,openPanel,closePanel} from './ux-helpers.ts';
const boot='/api/bootstrap?protocolVersion=4&contentVersion=reference-spatial-content-2&contentVersion=terrain-content-1';
const state=async(page:Page)=>(await(await page.request.get(boot)).json()).snapshot;
const save=readFileSync('../prototype/protocol/saves/terrain-v6.json');
async function replace(page:Page,bytes:Buffer){const s=await state(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:bytes})).ok()).toBe(true);}
async function point(page:Page,x:number,y:number){return page.locator('canvas').evaluate((canvas,args)=>{
 const r=canvas.getBoundingClientRect(),[cx,cy]=(canvas as HTMLCanvasElement).dataset.camera!.split(',').map(Number),z=Number((canvas as HTMLCanvasElement).dataset.zoom);
 return {x:r.x+r.width/2+(args.x-cx)*z,y:r.y+r.height/2+(args.y-cy)*z};
},{x,y});}

test('real terrain save replacement renders sea/cut/forest, picks independently, retains canvas and image cache across view/panel changes, then reopens',async({page})=>{
 let sheets=0;page.on('request',r=>{if(r.url().includes('/content/bundled-v2/')&&r.url().endsWith('.png'))sheets++;});
 await page.goto('/');await ready(page);const original=await(await page.request.get('/api/saves/download')).body();
 const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 const canvas=page.locator('canvas');await canvas.evaluate(el=>el.setAttribute('data-instance','terrain-preserved'));
 try {
  await replace(page,save);await expect(canvas).toHaveAttribute('data-ruleset','terrain-state-1');await expect(canvas).toHaveAttribute('data-assets','ready');await ready(page);
  await expect(canvas).toHaveAttribute('data-instance','terrain-preserved');await expect(page.getByTestId('reference-world')).toHaveText('Terrain world');
  await expect(page.getByRole('button',{name:'Remove track',exact:true})).toBeDisabled();
  // H0,V0 at water z3: source centre 16,-40; no sprite or land at this height.
  const q=await point(page,16,-40);await page.mouse.click(q.x,q.y);await expect(page.getByTestId('selected-object')).toHaveText('Terrain · cell:0,0 · 1, 0, 3');
  await openPanel(page,'view');await page.getByLabel('Height cut',{exact:true}).fill('4');await expect(canvas).toHaveAttribute('data-terrain-cut','4');await expect(canvas).toHaveAttribute('data-assets','ready');
  await expect.poll(()=>sheets).toBeGreaterThan(0);
  const terrainHash=await canvas.evaluate(el=>el.toDataURL());
  await page.getByLabel('Night',{exact:true}).check();await expect(canvas).toHaveAttribute('data-terrain-variant','spring/night');await expect.poll(async()=>canvas.evaluate(el=>el.toDataURL())).not.toBe(terrainHash);
  await page.getByLabel('Winter ground',{exact:true}).check();await expect(canvas).toHaveAttribute('data-terrain-variant','winter/night');
  await page.getByLabel('Night',{exact:true}).uncheck();await page.getByLabel('Winter ground',{exact:true}).uncheck();
  await expect.poll(async()=>canvas.evaluate(el=>el.toDataURL())).toBe(terrainHash);
  const decoded=sheets;
  for(let i=0;i<3;i++){await openPanel(page,'help');await openPanel(page,'view');}
  expect(sheets).toBe(decoded);await expect(canvas).toHaveAttribute('data-instance','terrain-preserved');
  await closePanel(page);
  // H0,V0's realized cedar at X0,Y0 is visible above the slope, independently of its diamond.
  const tree=await point(page,0,-58);await page.mouse.click(tree.x,tree.y);await expect(page.getByTestId('selected-object')).toContainText('Terrain · 2 · 1, 0, 2');
  const directory=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'terrain');mkdirSync(directory,{recursive:true});await page.screenshot({path:join(directory,'terrain-cut-en.png'),fullPage:true});
  const captured=await(await page.request.get('/api/saves/download')).body();expect(JSON.parse(captured.toString()).terrain.occupants[1].forest.trees).toEqual([{x:0,y:0,sprite:0},{x:30,y:15,sprite:8}]);
  await page.reload();await ready(page);await expect(canvas).toHaveAttribute('data-ruleset','terrain-state-1');await expect(canvas).toHaveAttribute('data-assets','ready');expect(await(await page.request.get('/api/saves/download')).body()).toEqual(captured);
  expect(errors).toEqual([]);
 }finally{await replace(page,original);}
});

test('terrain Japanese narrow DPR2 view and failed recipe decoding gate the live reveal',async({browser})=>{
 const context=await browser.newContext({viewport:{width:640,height:900},deviceScaleFactor:2,locale:'ja-JP'}),page=await context.newPage();
 await page.goto('/');await ready(page,'ja');const original=await(await page.request.get('/api/saves/download')).body();
 try{
  await replace(page,save);await expect(page.locator('canvas')).toHaveAttribute('data-ruleset','terrain-state-1');await ready(page,'ja');
  await openPanel(page,'view','ja');await page.getByLabel('高さ表示',{exact:true}).fill('4');await expect(page.locator('canvas')).toHaveAttribute('data-assets','ready');
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  const directory=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'terrain');mkdirSync(directory,{recursive:true});await page.screenshot({path:join(directory,'terrain-cut-ja-dpr2.png'),fullPage:true});
  await page.route('**/content/bundled-v2/**/*.png',route=>route.abort());
  await page.reload();await expect(page.locator('.command-bar')).toHaveCount(0);await expect(page.locator('canvas')).not.toHaveAttribute('data-assets','ready');
 }finally{await replace(page,original);await context.close();}
});

test('late night-sheet decoding cannot replace a newer day view or its hit targets',async({page})=>{
 await page.goto('/');await ready(page);const original=await(await page.request.get('/api/saves/download')).body();
 let release=()=>{},waiting=0;const held=new Promise<void>(r=>{release=r;});
 try{
  await replace(page,save);await expect(page.locator('canvas')).toHaveAttribute('data-ruleset','terrain-state-1');await ready(page);
  await openPanel(page,'view');const canvas=page.locator('canvas');await page.getByLabel('Height cut',{exact:true}).fill('4');await expect.poll(()=>canvas.evaluate(el=>el.dataset.terrainCut)).toBe('4');
  const before=await canvas.evaluate(el=>el.toDataURL());
  await page.route('**/content/bundled-v2/**/*.png',async route=>{const response=await route.fetch();waiting++;await held;await route.fulfill({response});});
  await page.getByLabel('Night',{exact:true}).check();await expect.poll(()=>waiting).toBeGreaterThan(0);await expect(canvas).toHaveAttribute('data-assets','loading');
  await page.getByLabel('Night',{exact:true}).uncheck();await expect.poll(()=>canvas.evaluate(el=>el.toDataURL())).toBe(before);
  const received=page.waitForResponse(r=>r.url().includes('/content/bundled-v2/')&&r.url().endsWith('.png'));release();await received;
  await page.evaluate(()=>new Promise<void>(resolve=>requestAnimationFrame(()=>requestAnimationFrame(()=>resolve()))));
  await expect(canvas).toHaveAttribute('data-terrain-variant','spring/day');expect(await canvas.evaluate(el=>el.toDataURL())).toBe(before);
  await closePanel(page);const q=await point(page,0,-58);await page.mouse.click(q.x,q.y);await expect(page.getByTestId('terrain-surface')).toHaveText('Forest');
 }finally{release();await replace(page,original);}
});

test('synthetic full-terrain landscape renders behind the connection gate and gives way to the real world on the same canvas',async({page})=>{
 let release=()=>{};const held=new Promise<void>(r=>{release=r;});
 await page.route('**/api/bootstrap?*',async route=>{await held;await route.continue();});
 const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 let images=0;page.on('request',r=>{if(r.url().includes('/content/bundled-v2/')&&r.url().endsWith('.png'))images++;});
 try{
  await page.goto('/');const canvas=page.locator('canvas');
  await expect(canvas).toHaveAttribute('data-scene','demo');await expect(canvas).toHaveAttribute('data-ruleset','terrain-state-1');await expect(canvas).toHaveAttribute('data-assets','ready');
  expect(Number(await canvas.getAttribute('data-demo-loop'))).toBeGreaterThan(100);
  await expect(page.locator('.command-bar')).toHaveCount(0);await expect(page.getByRole('dialog')).toBeVisible();
  expect(Number(await canvas.getAttribute('data-drawn'))).toBeGreaterThan(1000);
  // Fractional demo zoom must not expose the pale background between sea tiles.
  const seaError=await canvas.evaluate(el=>{
    const data=el.getContext('2d')!.getImageData(80,60,80,60).data;
    const expected=[0,114,188,255];let maximum=0;
    for(let i=0;i<data.length;i++)maximum=Math.max(maximum,Math.abs(data[i]-expected[i%4]));
    return maximum;
  });
  // Allow channel rounding while rejecting antialiased gaps to the backdrop.
  expect(seaError).toBeLessThanOrEqual(3);
  // This world-space rectangle lies inside the northeast hillside, including
  // the former exposed ledges and rail-bed joins. Ocean must not show through.
  expect(await canvas.evaluate(el=>{
    const [cx,cy]=el.dataset.camera!.split(',').map(Number),zoom=Number(el.dataset.zoom),r=el.getBoundingClientRect(),dpr=el.width/r.width;
    const x=Math.round((r.width/2+(1000-cx)*zoom)*dpr),y=Math.round((r.height/2+(100-cy)*zoom)*dpr);
    const pixels=el.getContext('2d')!.getImageData(x,y,Math.round(280*zoom*dpr),Math.round(90*zoom*dpr)).data;
    let ocean=0;
    for(let i=0;i<pixels.length;i+=4)if(pixels[i]<=3&&Math.abs(pixels[i+1]-114)<=3&&Math.abs(pixels[i+2]-188)<=3)ocean++;
    return ocean;
  })).toBe(0);
  await canvas.evaluate(el=>el.dataset.instance='synthetic-demo');
  const train=await canvas.getAttribute('data-demo-train'),firstFrame=await canvas.evaluate(el=>el.toDataURL());
  await expect(canvas).not.toHaveAttribute('data-demo-train',train!);
  await expect.poll(()=>canvas.evaluate(el=>el.toDataURL())).not.toBe(firstFrame);
  await page.evaluate(()=>{Object.defineProperty(document,'hidden',{configurable:true,value:true});document.dispatchEvent(new Event('visibilitychange'));});
  const hiddenTrain=await canvas.getAttribute('data-demo-train');
  await page.evaluate(()=>new Promise<void>(r=>requestAnimationFrame(()=>requestAnimationFrame(()=>r()))));
  await expect(canvas).toHaveAttribute('data-demo-train',hiddenTrain!);
  await page.evaluate(()=>{Object.defineProperty(document,'hidden',{configurable:true,value:false});document.dispatchEvent(new Event('visibilitychange'));});
  await expect(canvas).not.toHaveAttribute('data-demo-train',hiddenTrain!);
  await page.emulateMedia({reducedMotion:'reduce'});
  await page.evaluate(()=>new Promise<void>(r=>requestAnimationFrame(()=>requestAnimationFrame(()=>r()))));
  const stillTrain=await canvas.getAttribute('data-demo-train'),stillFrame=await canvas.evaluate(el=>el.toDataURL());
  await page.evaluate(()=>new Promise<void>(r=>requestAnimationFrame(()=>requestAnimationFrame(()=>r()))));
  await expect(canvas).toHaveAttribute('data-demo-train',stillTrain!);expect(await canvas.evaluate(el=>el.toDataURL())).toBe(stillFrame);
  const decoded=images,directory=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'terrain');mkdirSync(directory,{recursive:true});
  const terrainPNG=await canvas.evaluate(el=>el.toDataURL().split(',')[1]);writeFileSync(join(directory,'island-terrain.png'),Buffer.from(terrainPNG,'base64'));
  await page.screenshot({path:join(directory,'connection-landscape-desktop.png'),fullPage:true});
  await page.setViewportSize({width:360,height:800});await expect.poll(async()=>Number(await canvas.getAttribute('data-zoom'))).toBeCloseTo(800/736,2);
  await page.screenshot({path:join(directory,'connection-landscape-portrait.png'),fullPage:true});
  expect(images).toBe(decoded);expect(await page.evaluate(()=>document.documentElement.scrollWidth)).toBe(360);
  await expect(page.locator('.map-viewport')).toHaveAttribute('inert','');
  release();await ready(page);await expect(canvas).toHaveAttribute('data-ruleset','reference-spatial-4');await expect(canvas).toHaveAttribute('data-instance','synthetic-demo');
  expect(await canvas.getAttribute('data-demo-loop')).toBeNull();expect(await canvas.getAttribute('data-demo-train')).toBeNull();
  expect((await state(page)).terrain).toBeUndefined();expect(errors).toEqual([]);
 }finally{release();}
});

test('empty-world preview cancellation, exact owner confirmation, zero rails, second-client epoch and save reopen',async({page,browser})=>{
 await page.goto('/');await ready(page);const original=await(await page.request.get('/api/saves/download')).body();
 const other=await browser.newContext(),watcher=await other.newPage();await watcher.goto('/');await ready(watcher);
 try{
  const before=await state(page),canvas=page.locator('canvas');await canvas.evaluate(el=>el.setAttribute('data-instance','empty-preserved'));
  await openPanel(page,'newWorld');await page.getByLabel('World name',{exact:true}).fill('Empty railway');
  await expect(page.getByLabel('Stored width (H)',{exact:true})).toHaveValue('30');await expect(page.getByLabel('Stored length (V)',{exact:true})).toHaveValue('42');
  await page.getByRole('button',{name:'Preview world',exact:true}).click();await expect(page.getByTestId('empty-world-preview')).toContainText('30 × 42 × 6');
  const directory=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'empty-world');mkdirSync(directory,{recursive:true});await page.screenshot({path:join(directory,'empty-en-preview.png'),fullPage:true});
  expect((await state(page)).worldEpoch).toBe(before.worldEpoch);
  await page.getByRole('button',{name:'Cancel',exact:true}).click();await expect(page.getByTestId('empty-world-preview')).toHaveCount(0);expect((await state(page)).worldEpoch).toBe(before.worldEpoch);
  await page.getByRole('button',{name:'Preview world',exact:true}).click();await expect(page.getByTestId('empty-world-preview')).toBeVisible();
  await page.getByRole('button',{name:'Create world',exact:true}).click();await expect(page.locator('dialog.decision-dialog')).toBeVisible();
  await page.locator('dialog.decision-dialog').getByRole('button',{name:'Cancel',exact:true}).click();expect((await state(page)).worldEpoch).toBe(before.worldEpoch);
  await page.getByRole('button',{name:'Create world',exact:true}).click();await page.locator('dialog.decision-dialog').getByRole('button',{name:'Create world',exact:true}).click();
  await expect(canvas).toHaveAttribute('data-ruleset','terrain-state-1');await ready(page);await ready(watcher);
  await expect.poll(async()=> (await state(watcher)).worldEpoch).not.toBe(before.worldEpoch);
  await expect(canvas).toHaveAttribute('data-instance','empty-preserved');const after=await state(page);expect(after.tracks).toEqual([]);expect(after.entities).toEqual([]);expect(after.terrain.name).toBe('Empty railway');
  const captured=await(await page.request.get('/api/saves/download')).body();
  await page.reload();await ready(page);expect(await(await page.request.get('/api/saves/download')).body()).toEqual(captured);
  await replace(page,original);await replace(page,captured);expect((await state(page)).terrain.name).toBe('Empty railway');
 }finally{await replace(page,original);await other.close();}
});

test('Japanese narrow empty-world form, custom limits, candidate invalidation and owner-only endpoint',async({browser})=>{
 const context=await browser.newContext({viewport:{width:390,height:844},locale:'ja-JP'}),page=await context.newPage();
 await page.goto('/');await ready(page,'ja');const original=await(await page.request.get('/api/saves/download')).body();
 try{
  await openPanel(page,'newWorld','ja');
  await page.getByLabel('格納幅（H）',{exact:true}).fill('65');await expect(page.getByRole('button',{name:'世界をプレビュー',exact:true})).toBeDisabled();
  await page.getByLabel('格納幅（H）',{exact:true}).fill('1');await page.getByLabel('格納奥行（V）',{exact:true}).fill('1');await page.getByLabel('高さ（Z）',{exact:true}).fill('1');await page.getByLabel('海面高さ',{exact:true}).fill('0');
  await page.getByRole('button',{name:'世界をプレビュー',exact:true}).click();await expect(page.getByTestId('empty-world-preview')).toContainText('1 × 1 × 1');
  await page.getByLabel('世界名',{exact:true}).fill('変更');await expect(page.getByTestId('empty-world-preview')).toHaveCount(0);
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  const directory=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'empty-world');mkdirSync(directory,{recursive:true});await page.screenshot({path:join(directory,'empty-ja-narrow.png'),fullPage:true});
  // Advance the authoritative clock after a ready preview. It must disappear without replacement.
  await page.getByRole('button',{name:'世界をプレビュー',exact:true}).click();await expect(page.getByTestId('empty-world-preview')).toBeVisible();
  await page.getByRole('button',{name:'世界を作成',exact:true}).click();await expect(page.locator('dialog.decision-dialog')).toBeVisible();
  await page.screenshot({path:join(directory,'empty-ja-confirm.png'),fullPage:true});
  await page.locator('dialog.decision-dialog').getByRole('button',{name:'キャンセル',exact:true}).click();
  const s=await state(page),captured=await(await page.request.get('/api/saves/download')).body();await replace(page,captured);
  await expect(page.getByTestId('empty-world-preview')).toHaveCount(0);expect((await state(page)).worldEpoch).not.toBe(s.worldEpoch);
  const observer=await browser.newContext();try{
   const p=await observer.newPage();await p.goto('/');await ready(p);await p.request.post('/api/leave');await p.request.post('/api/access',{data:{secret:'observer-test'}});await p.reload();await ready(p);
   const current=await state(p);const response=await p.request.post('/api/worlds/empty/preview',{data:{name:'Denied',dimensions:{h:1,v:1,z:1},waterLevel:0,epoch:current.worldEpoch,revision:current.revision}});expect(response.status()).toBe(403);
   await openPanel(p,'newWorld');await expect(p.getByRole('button',{name:'Preview world',exact:true})).toBeDisabled();
  }finally{await observer.close();}
 }finally{await replace(page,original);await context.close();}
});

test('empty candidate API rejects stale revisions, forged digests and late confirmations; maximum admitted empty world renders and reopens',async({page})=>{
 await page.goto('/');await ready(page);const original=await(await page.request.get('/api/saves/download')).body();
 async function preview(h=30,v=42,z=6,water=1){const s=await state(page);const response=await page.request.post('/api/worlds/empty/preview',{data:{name:'Capacity',dimensions:{h,v,z},waterLevel:water,epoch:s.worldEpoch,revision:s.revision}});expect(response.ok()).toBe(true);return {...await response.json(),epoch:s.worldEpoch,revision:s.revision};}
 async function commit(c:any){return page.request.post('/api/worlds/empty/commit',{data:{id:c.id,requestHash:c.requestHash,candidateHash:c.candidateHash,epoch:c.epoch,revision:c.revision}});}
 try{
  let candidate=await preview();expect((await commit({...candidate,candidateHash:'0'.repeat(64)})).status()).toBe(409);
  // A step is an authoritative revision change without an epoch change.
  await page.locator('.command-bar .step').click();await expect.poll(async()=>(await state(page)).revision).not.toBe(candidate.revision);
  expect((await commit(candidate)).status()).toBe(409);
  candidate=await preview();await replace(page,original);expect((await commit(candidate)).status()).toBe(409);
  candidate=await preview(64,64,64,63);expect(candidate.saveBytes).toBeLessThan(16*1024*1024);expect((await commit(candidate)).ok()).toBe(true);
  await expect(page.locator('canvas')).toHaveAttribute('data-ruleset','terrain-state-1');await ready(page);await expect(page.locator('canvas')).toHaveAttribute('data-assets','ready');
  // Independent source H/V projection of stored centre H32,V32 at Z63: (1040,-744).
  expect(await page.locator('canvas').evaluate(el=>(el as HTMLCanvasElement).dataset.camera!.split(',').slice(0,2).map(Number))).toEqual([1040,-744]);
  const captured=await(await page.request.get('/api/saves/download')).body();const data=JSON.parse(captured.toString());expect(data.terrain.cells).toHaveLength(4096);expect(data.rails).toEqual([]);
  await page.reload();await ready(page);expect(await(await page.request.get('/api/saves/download')).body()).toEqual(captured);
  expect((await commit(candidate)).status()).toBe(409);
 }finally{await replace(page,original);}
});
