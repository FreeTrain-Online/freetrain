import {test,expect,type Page} from '@playwright/test';
import {readFileSync,mkdirSync,writeFileSync} from 'node:fs';
import {join} from 'node:path';
import {ready,openPanel,closePanel} from './ux-helpers.ts';
const boot='/api/bootstrap?protocolVersion=5&contentVersion=reference-spatial-content-2&contentVersion=terrain-content-1';
const state=async(page:Page)=>(await(await page.request.get(boot)).json()).snapshot;
const savedGround=(s:any)=>[...Buffer.from(s.packedTerrain.cells,'base64')].map(n=>n&63);
const save=readFileSync('../prototype/protocol/saves/terrain-v7.json');
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
  const captured=await(await page.request.get('/api/saves/download')).body();expect(JSON.parse(captured.toString()).packedTerrain.occupantCount).toBe(3);
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
  await page.getByLabel('格納幅（H）',{exact:true}).fill('2049');await expect(page.getByRole('button',{name:'世界をプレビュー',exact:true})).toBeDisabled();
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
  const captured=await(await page.request.get('/api/saves/download')).body();const data=JSON.parse(captured.toString());expect(savedGround(data)).toHaveLength(4096);expect(data.rails).toEqual([]);
  await page.reload();await ready(page);expect(await(await page.request.get('/api/saves/download')).body()).toEqual(captured);
  expect((await commit(candidate)).status()).toBe(409);
 }finally{await replace(page,original);}
});

// Hand-authored BMP rows. No browser image decoder supplies the height oracle.
function heightmapBmp(){
 const b=Buffer.alloc(70);b.write('BM');b.writeInt32LE(70,2);b.writeInt32LE(54,10);b.writeInt32LE(40,14);b.writeInt32LE(2,18);b.writeInt32LE(2,22);b.writeInt16LE(1,26);b.writeInt16LE(24,28);
 for(const row of [54,62])for(let c=0;c<3;c++)b[row+3+c]=255;return b;
}
test('heightmap browser preview is detached, confirms exact terrain to both clients and reopens without the image',async({page,browser})=>{
 await page.goto('/');await ready(page);const original=await(await page.request.get('/api/saves/download')).body();
 const other=await browser.newContext(),peer=await other.newPage();await peer.goto('/');await ready(peer);
 try{
  const canvas=page.locator('canvas');await canvas.evaluate(el=>el.dataset.instance='heightmap-preserved');
  await openPanel(page,'newWorld');await page.getByLabel('World source',{exact:true}).selectOption('heightmap');
  await page.getByLabel('Terrain image',{exact:true}).setInputFiles({name:'ramp.bmp',mimeType:'image/bmp',buffer:heightmapBmp()});
  await page.getByLabel('World name',{exact:true}).fill('Image terrain');
  await page.getByLabel('Stored width (H)',{exact:true}).fill('4');await page.getByLabel('Stored length (V)',{exact:true}).fill('4');
  await page.getByLabel('Levels (Z)',{exact:true}).fill('8');await page.getByLabel('Water level',{exact:true}).fill('1');
  await page.getByRole('button',{name:'Preview world',exact:true}).click();
  await expect(page.getByRole('img',{name:'Decoded source image',exact:true})).toBeVisible();
  // Independent browser PNG decoder validates our thumbnail encoder and pixels.
  const decoded=page.getByRole('img',{name:'Decoded source image',exact:true});
  await expect.poll(()=>decoded.evaluate((image:HTMLImageElement)=>image.naturalWidth)).toBe(2);
  expect(await decoded.evaluate((image:HTMLImageElement)=>{
   const canvas=document.createElement('canvas');canvas.width=2;canvas.height=2;
   const ctx=canvas.getContext('2d')!;ctx.drawImage(image,0,0);return Array.from(ctx.getImageData(0,0,2,2).data);
  })).toEqual([0,0,0,255,255,255,255,255,0,0,0,255,255,255,255,255]);
await expect(page.getByRole('img',{name:'Resulting terrain heights and global water',exact:true})).toBeVisible();
  expect(await(await page.request.get('/api/saves/download')).body()).toEqual(original);
  await page.getByRole('button',{name:'Create world',exact:true}).click();await page.locator('dialog.decision-dialog').getByRole('button',{name:'Cancel',exact:true}).click();
  const directory=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'heightmap');mkdirSync(directory,{recursive:true});await page.screenshot({path:join(directory,'ramp-preview-en.png'),fullPage:true});
  await page.getByRole('button',{name:'Create world',exact:true}).click();await page.locator('dialog.decision-dialog').getByRole('button',{name:'Create world',exact:true}).click();
  await expect(canvas).toHaveAttribute('data-ruleset','terrain-state-1');await expect(peer.locator('canvas')).toHaveAttribute('data-ruleset','terrain-state-1');await ready(page);await ready(peer);
  await expect(canvas).toHaveAttribute('data-instance','heightmap-preserved');
  const captured=await(await page.request.get('/api/saves/download')).body(),saved=JSON.parse(captured.toString());
  expect(savedGround(saved)).toEqual([0,1,2,3,1,2,3,4,0,1,2,3,1,2,3,4]);expect(saved.rails).toEqual([]);
  await closePanel(page);await page.screenshot({path:join(directory,'ramp-live-en.png'),fullPage:true});
  await page.reload();await ready(page);await expect(canvas).toHaveAttribute('data-assets','ready');expect(await(await page.request.get('/api/saves/download')).body()).toEqual(captured);
  await replace(page,original);await replace(page,captured);expect((await state(page)).terrain.cells.map((c:any)=>c.ground)).toEqual(savedGround(saved));
 }finally{await replace(page,original);await other.close();}
});
test('heightmap Japanese narrow form reports image failures, invalidates drafts and cancels in-flight uploads',async({browser})=>{
 const context=await browser.newContext({viewport:{width:390,height:844},locale:'ja-JP'}),page=await context.newPage();
 await page.goto('/');await ready(page,'ja');const original=await(await page.request.get('/api/saves/download')).body();
 try{
  await openPanel(page,'newWorld','ja');await page.getByLabel('世界の作成方法',{exact:true}).selectOption('heightmap');
  const input=page.getByLabel('地形画像ファイル',{exact:true});await input.setInputFiles({name:'invalid.bmp',mimeType:'image/bmp',buffer:Buffer.from('invalid')});
  await page.getByRole('button',{name:'世界をプレビュー',exact:true}).click();await expect(page.getByTestId('creation-feedback')).toContainText('画像形式が未対応');
  await input.setInputFiles({name:'ramp.bmp',mimeType:'image/bmp',buffer:heightmapBmp()});
  await page.getByLabel('格納幅（H）',{exact:true}).fill('4');await page.getByLabel('格納奥行（V）',{exact:true}).fill('4');
  await page.getByRole('button',{name:'世界をプレビュー',exact:true}).click();await expect(page.getByTestId('empty-world-preview')).toBeVisible();
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  const directory=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'heightmap');mkdirSync(directory,{recursive:true});await page.screenshot({path:join(directory,'ramp-preview-ja-narrow.png'),fullPage:true});
  await page.getByLabel('世界名',{exact:true}).fill('変更');await expect(page.getByTestId('empty-world-preview')).toHaveCount(0);
  let release=()=>{};const held=new Promise<void>(r=>{release=r;});await page.route('**/api/worlds/heightmap/preview?*',async route=>{await held;await route.continue().catch(()=>{});});
  await page.getByRole('button',{name:'世界をプレビュー',exact:true}).click();await page.getByRole('button',{name:'キャンセル',exact:true}).click();release();
  await expect(page.getByTestId('empty-world-preview')).toHaveCount(0);await expect(page.getByRole('button',{name:'世界をプレビュー',exact:true})).toBeEnabled();
  expect(await(await page.request.get('/api/saves/download')).body()).toEqual(original);
 }finally{await replace(page,original);await context.close();}
});

test('seeded relief controls preview reproducibly, commit exact mountains to both clients and reopen realized Save6',async({page,browser})=>{
 await page.goto('/');await ready(page);const original=await(await page.request.get('/api/saves/download')).body(),before=await state(page);
 const context=await browser.newContext(),watcher=await context.newPage();await watcher.goto('/');await ready(watcher);
 try{
  const canvas=page.locator('canvas');await canvas.evaluate(el=>el.dataset.instance='relief-preserved');
  await openPanel(page,'newWorld');await page.getByLabel('World source',{exact:true}).selectOption('generated');
  await page.getByLabel('Terrain style',{exact:true}).selectOption('mountains');
  await expect(page.getByLabel('Relief (0–100)',{exact:true})).toHaveValue('85');
  await page.getByLabel('Levels (Z)',{exact:true}).fill('32');await page.getByLabel('World name',{exact:true}).fill('Seeded mountain railway');
  async function preview(){const response=page.waitForResponse(r=>r.url().includes('/worlds/generated/preview'));await page.getByRole('button',{name:'Preview world',exact:true}).click();const result=await(await response).json();await expect(page.getByTestId('empty-world-preview')).toBeVisible();return result;}
  const first=await preview();expect(first.ground).toHaveLength(1024);expect(new Set(first.ground).size).toBeGreaterThan(4);
  expect(await(await page.request.get('/api/saves/download')).body()).toEqual(original);
  await page.getByRole('button',{name:'Cancel',exact:true}).click();const second=await preview();expect(second.candidateHash).toBe(first.candidateHash);expect(second.ground).toEqual(first.ground);
  await page.getByLabel('Generation seed',{exact:true}).fill('42');await expect(page.getByTestId('empty-world-preview')).toHaveCount(0);
  const varied=await preview();expect(varied.ground).not.toEqual(first.ground);
  await page.getByLabel('Relief (0–100)',{exact:true}).fill('0');const flat=await preview();expect(new Set(flat.ground).size).toBe(1);expect(flat.ground[0]).toBe(5);
  await page.getByLabel('Relief (0–100)',{exact:true}).fill('85');await page.getByLabel('Feature scale (cells)',{exact:true}).fill('8');const scaled=await preview();expect(scaled.ground).not.toEqual(varied.ground);await page.getByLabel('Feature scale (cells)',{exact:true}).fill('32');
  await page.getByLabel('Generation seed',{exact:true}).fill('0');const final=await preview();expect(final.candidateHash).toBe(first.candidateHash);
  const directory=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'relief');mkdirSync(directory,{recursive:true});await page.getByTestId('empty-world-preview').scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'relief-en-preview.png'),fullPage:true});
  await page.getByRole('button',{name:'Create world',exact:true}).click();await page.locator('dialog.decision-dialog').getByRole('button',{name:'Create world',exact:true}).click();
  await ready(page);await ready(watcher);await expect(canvas).toHaveAttribute('data-ruleset','terrain-state-1');await expect(canvas).toHaveAttribute('data-assets','ready');await expect(canvas).toHaveAttribute('data-instance','relief-preserved');
  await expect.poll(async()=>(await state(watcher)).worldEpoch).not.toBe(before.worldEpoch);
  const committed=await state(page);expect(committed.terrain.cells.map((c:any)=>c.ground)).toEqual(final.ground);expect(committed.tracks).toEqual([]);
  const captured=await(await page.request.get('/api/saves/download')).body();expect(captured.length).toBe(final.saveBytes);
  await closePanel(page);await ready(page);await expect(page.getByTestId('reference-world')).toHaveText('Terrain world');await page.screenshot({path:join(directory,'relief-en-live.png'),fullPage:true});
  await page.reload();await ready(page);expect(await(await page.request.get('/api/saves/download')).body()).toEqual(captured);
  await replace(page,original);await replace(page,captured);expect((await state(page)).terrain.cells.map((c:any)=>c.ground)).toEqual(final.ground);
 }finally{await replace(page,original);await context.close();}
});

test('Japanese narrow seeded relief form validates ranges, changes presets, invalidates drafts and cancels late responses',async({browser})=>{
 const context=await browser.newContext({viewport:{width:390,height:844},locale:'ja-JP'}),page=await context.newPage();await page.goto('/');await ready(page,'ja');
 const original=await(await page.request.get('/api/saves/download')).body();
 let release=()=>{};
 try{
  await openPanel(page,'newWorld','ja');await page.getByLabel('世界の作成方法',{exact:true}).selectOption('generated');
  await page.getByLabel('地形の種類',{exact:true}).selectOption('plains');await expect(page.getByLabel('起伏（0～100）',{exact:true})).toHaveValue('20');
  await page.getByLabel('生成シード',{exact:true}).fill('2147483648');await expect(page.getByRole('button',{name:'世界をプレビュー',exact:true})).toBeDisabled();
  await page.getByLabel('生成シード',{exact:true}).fill('-2147483648');await page.getByLabel('格納幅（H）',{exact:true}).fill('15');await expect(page.getByRole('button',{name:'世界をプレビュー',exact:true})).toBeDisabled();
  await page.getByLabel('格納幅（H）',{exact:true}).fill('16');await page.getByRole('button',{name:'世界をプレビュー',exact:true}).click();await expect(page.getByTestId('empty-world-preview')).toBeVisible();
  await page.getByLabel('滑らかさ（0～100）',{exact:true}).fill('100');await expect(page.getByTestId('empty-world-preview')).toHaveCount(0);
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  const directory=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'relief');mkdirSync(directory,{recursive:true});await page.getByLabel('地形の種類',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'relief-ja-narrow.png'),fullPage:true});
  // Let the server finish a job but hold its response while the user cancels.
  const held=new Promise<void>(r=>release=r);let seen=false;
  await page.route('**/api/worlds/generated/preview',async route=>{const response=await route.fetch();seen=true;await held;await route.fulfill({response}).catch(()=>{});});
  await page.getByRole('button',{name:'世界をプレビュー',exact:true}).click();await expect.poll(()=>seen).toBe(true);await page.getByRole('button',{name:'キャンセル',exact:true}).click();release();
  await expect(page.getByRole('button',{name:'世界をプレビュー',exact:true})).toBeEnabled();await expect(page.getByTestId('empty-world-preview')).toHaveCount(0);
  expect(await(await page.request.get('/api/saves/download')).body()).toEqual(original);
 }finally{release();await replace(page,original);await context.close();}
});

test('generated endpoint rejects unavailable features, roles, stale fences and forged digests without changing the world',async({page,browser})=>{
 await page.goto('/');await ready(page);const original=await(await page.request.get('/api/saves/download')).body();
 const options={seed:0,relief:{kind:1,amount:50,featureScale:24,smoothness:50},coast:{kind:0,sides:0,waterPercent:0},lakes:{count:0,areaPercent:0},rivers:{direction:0,width:1,meander:30},forests:{coveragePercent:0,clustering:50,palette:[]},startingArea:0};
 const s=await state(page),input={name:'API relief',dimensions:{h:32,v:32,z:32},waterLevel:4,options,epoch:s.worldEpoch,revision:s.revision};
 try{
  for(const invalid of [{...input,dimensions:{h:2049,v:32,z:32}},{...input,options:{...options,startingArea:257}},{...input,options:{...options,coast:{kind:1,sides:0,waterPercent:0}}}])expect((await page.request.post('/api/worlds/generated/preview',{data:invalid})).status()).toBe(400);
  expect((await page.request.post('/api/worlds/generated/preview',{data:{...input,revision:String(BigInt(s.revision)+1n)}})).status()).toBe(409);
  const response=await page.request.post('/api/worlds/generated/preview',{data:input});expect(response.ok()).toBe(true);const candidate=await response.json();
  expect((await page.request.post('/api/worlds/empty/commit',{data:{id:candidate.id,requestHash:candidate.requestHash,epoch:s.worldEpoch,revision:s.revision,candidateHash:'0'.repeat(64)}})).status()).toBe(409);
  expect(await(await page.request.get('/api/saves/download')).body()).toEqual(original);
  await replace(page,original);expect((await page.request.post('/api/worlds/empty/commit',{data:{id:candidate.id,requestHash:candidate.requestHash,candidateHash:candidate.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).status()).toBe(409);
  const observer=await browser.newContext();try{const p=await observer.newPage();await p.goto('/');await ready(p);await p.request.post('/api/leave');await p.request.post('/api/access',{data:{secret:'observer-test'}});await p.reload();await ready(p);expect((await p.request.post('/api/worlds/generated/preview',{data:input})).status()).toBe(403);}finally{await observer.close();}
 }finally{await replace(page,original);}
});

test('coast directions, realized preview, impossible diagnostics and exact island confirmation',async({page,browser})=>{
 await page.goto('/');await ready(page);const original=await(await page.request.get('/api/saves/download')).body();
 const context=await browser.newContext(),watcher=await context.newPage();await watcher.goto('/');await ready(watcher);
 try{
  await openPanel(page,'newWorld');await page.getByLabel('World source',{exact:true}).selectOption('generated');
  await page.getByLabel('Stored width (H)',{exact:true}).fill('32');await page.getByLabel('Stored length (V)',{exact:true}).fill('27');
  await page.getByLabel('Coast layout',{exact:true}).selectOption('sides');
  const names=['North','East','South','West'];
  async function preview(){const waiting=page.waitForResponse(r=>r.url().includes('/worlds/generated/preview'));await page.getByRole('button',{name:'Preview world',exact:true}).click();const response=await waiting;expect(response.ok(),await response.text()).toBe(true);await expect(page.getByTestId('empty-world-preview')).toBeVisible();return response.json();}
  for(const selected of [[0],[1],[2],[3],[0,1],[0,2]]){
   for(let i=0;i<4;i++)await page.getByRole('checkbox',{name:names[i],exact:true}).setChecked(selected.includes(i));
   const result=await preview();expect(result.geography.metrics.seaSides).toBe(selected.reduce((n,i)=>n|(1<<i),0));
   expect(result.geography.metrics.landComponents).toBe(1);expect(result.geography.metrics.waterPercent).toBeGreaterThanOrEqual(35);expect(result.geography.metrics.waterPercent).toBeLessThanOrEqual(45);
   await expect(page.getByTestId('geography-metrics')).toContainText('Surface water:');
  }
  for(const name of names)await page.getByRole('checkbox',{name,exact:true}).uncheck();await expect(page.getByRole('button',{name:'Preview world',exact:true})).toBeDisabled();
  await page.getByLabel('Coast layout',{exact:true}).selectOption('island');
  await page.getByLabel('Stored width (H)',{exact:true}).fill('16');await page.getByLabel('Stored length (V)',{exact:true}).fill('16');await page.getByLabel('Target sea coverage (%)',{exact:true}).fill('5');
  await page.getByRole('button',{name:'Preview world',exact:true}).click();await expect(page.getByTestId('creation-feedback')).toContainText('cannot');
  await expect(page.getByTestId('empty-world-preview')).toHaveCount(0);expect(await(await page.request.get('/api/saves/download')).body()).toEqual(original);
  await page.getByLabel('Stored width (H)',{exact:true}).fill('32');await page.getByLabel('Stored length (V)',{exact:true}).fill('27');await page.getByLabel('Target sea coverage (%)',{exact:true}).fill('40');
  const final=await preview();expect(final.geography.metrics.seaComponents).toBe(1);expect(final.geography.metrics.seaSides).toBe(15);
  await page.getByLabel('Generation seed',{exact:true}).fill('42');await expect(page.getByTestId('empty-world-preview')).toHaveCount(0);
  await page.getByLabel('Generation seed',{exact:true}).fill('0');expect((await preview()).candidateHash).toBe(final.candidateHash);
  const directory=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'coasts');mkdirSync(directory,{recursive:true});await page.getByTestId('geography-metrics').scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'coast-en-preview.png'),fullPage:true});
  const before=(await state(page)).worldEpoch;await page.locator('canvas').evaluate(c=>c.dataset.instance='coast-preserved');
  await page.getByRole('button',{name:'Create world',exact:true}).click();await page.locator('dialog.decision-dialog').getByRole('button',{name:'Create world',exact:true}).click();
  await ready(page);await ready(watcher);await expect.poll(async()=>(await state(watcher)).worldEpoch).not.toBe(before);
  await expect(page.locator('canvas')).toHaveAttribute('data-instance','coast-preserved');
  expect((await state(page)).terrain.cells.map((c:any)=>c.ground)).toEqual(final.ground);
  const saved=await(await page.request.get('/api/saves/download')).body();await closePanel(page);await ready(page);await page.screenshot({path:join(directory,'coast-en-live.png'),fullPage:true});
  await page.reload();await ready(page);expect(await(await page.request.get('/api/saves/download')).body()).toEqual(saved);
  await replace(page,original);await replace(page,saved);expect(await(await page.request.get('/api/saves/download')).body()).toEqual(saved);
 }finally{await replace(page,original);await context.close();}
});

test('Japanese narrow coast controls and compass remain reachable and invalidate the candidate',async({browser})=>{
 const context=await browser.newContext({viewport:{width:390,height:844},locale:'ja-JP'}),page=await context.newPage();await page.goto('/');await ready(page,'ja');
 try{
  await openPanel(page,'newWorld','ja');await page.getByLabel('世界の作成方法',{exact:true}).selectOption('generated');
  await page.getByLabel('海岸の配置',{exact:true}).selectOption('sides');await page.getByRole('checkbox',{name:'東',exact:true}).check();
  await page.getByLabel('海の目標面積（%）',{exact:true}).fill('76');await expect(page.getByRole('button',{name:'世界をプレビュー',exact:true})).toBeDisabled();
  await page.getByLabel('海の目標面積（%）',{exact:true}).fill('40');await page.getByRole('button',{name:'世界をプレビュー',exact:true}).click();
  await expect(page.getByTestId('geography-metrics')).toContainText('水面：');await expect(page.getByText('概観の方位：',{exact:false})).toBeVisible();
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  const directory=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'coasts');mkdirSync(directory,{recursive:true});await page.getByLabel('海岸の配置',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'coast-ja-narrow.png'),fullPage:true});
  await page.getByRole('checkbox',{name:'南',exact:true}).check();await expect(page.getByTestId('empty-world-preview')).toHaveCount(0);
 }finally{await context.close();}
});

test('lakes and rivers preview exact geometry, preserve drafts and publish to both clients with Save7 reopen',async({page,browser})=>{
 await page.goto('/');await ready(page);const original=await(await page.request.get('/api/saves/download')).body();
 const context=await browser.newContext(),watcher=await context.newPage();await watcher.goto('/');await ready(watcher);
 try{
  await page.locator('canvas').evaluate(c=>c.dataset.instance='water-preserved');
  await openPanel(page,'newWorld');await page.getByLabel('World source',{exact:true}).selectOption('generated');
  await page.getByLabel('Stored width (H)',{exact:true}).fill('64');await page.getByLabel('Stored length (V)',{exact:true}).fill('64');await page.getByLabel('Levels (Z)',{exact:true}).fill('32');
  await page.getByLabel('World name',{exact:true}).fill('Lake railway');await page.getByLabel('Lake count (0–8)',{exact:true}).fill('1');await page.getByLabel('River direction',{exact:true}).selectOption('none');
  async function preview(){const waiting=page.waitForResponse(r=>r.url().includes('/worlds/generated/preview'));await page.getByRole('button',{name:'Preview world',exact:true}).click();const response=await waiting;expect(response.ok(),await response.text()).toBe(true);await expect(page.getByTestId('empty-world-preview')).toBeVisible();return response.json();}
  const basin=await preview();expect(basin.geography.metrics).toMatchObject({lakeComponents:1,riverCount:0});
  await page.getByLabel('River direction',{exact:true}).selectOption('northSouth');
  const first=await preview();expect(first.geography.metrics).toMatchObject({lakeComponents:0,riverCount:1,seaComponents:1,seaSides:5});
  expect(await(await page.request.get('/api/saves/download')).body()).toEqual(original);
  await expect(page.getByTestId('water-metrics')).toContainText('Enclosed lakes: 0');
  await page.getByLabel('Channel width (cells)',{exact:true}).fill('2');await expect(page.getByTestId('empty-world-preview')).toHaveCount(0);
  const wide=await preview();expect(wide.geography.seaMask).not.toBe(first.geography.seaMask);
  await page.getByLabel('River direction',{exact:true}).selectOption('eastWest');
  const across=await preview();expect(across.geography.metrics).toMatchObject({lakeComponents:0,riverCount:1,seaSides:10});
  await page.getByLabel('River direction',{exact:true}).selectOption('northSouth');
  await page.getByLabel('Coast layout',{exact:true}).selectOption('island');await page.getByLabel('Target sea coverage (%)',{exact:true}).fill('30');
  const final=await preview();expect(final.geography.metrics).toMatchObject({lakeComponents:0,riverCount:1,seaSides:15});
  const directory=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'water');mkdirSync(directory,{recursive:true});await page.getByTestId('water-metrics').scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'water-en-preview.png'),fullPage:true});
  const before=(await state(page)).worldEpoch;
  await page.getByRole('button',{name:'Create world',exact:true}).click();await page.locator('dialog.decision-dialog').getByRole('button',{name:'Cancel',exact:true}).click();
  await expect(page.getByTestId('empty-world-preview')).toBeVisible();expect((await state(page)).worldEpoch).toBe(before);
  await page.getByRole('button',{name:'Create world',exact:true}).click();await page.locator('dialog.decision-dialog').getByRole('button',{name:'Create world',exact:true}).click();
  await ready(page);await ready(watcher);await expect.poll(async()=>(await state(watcher)).worldEpoch).not.toBe(before);
  await expect(page.locator('canvas')).toHaveAttribute('data-instance','water-preserved');
  expect((await state(page)).terrain.cells.map((c:any)=>c.ground)).toEqual(final.ground);
  const saved=await(await page.request.get('/api/saves/download')).body();expect(saved.length).toBe(final.saveBytes);const document=JSON.parse(saved.toString());expect(document.saveVersion).toBe(7);
  const provenance=JSON.parse(document.packedTerrain.provenance.requestJson);expect(provenance.Options.Lakes.Count).toBe(1);expect(provenance.Options.Rivers.Width).toBe(2);expect(provenance.Options.Rivers.Direction).toBe(1);expect(provenance.Options.Rivers).not.toHaveProperty('Count');expect(provenance.Options.Rivers).not.toHaveProperty('Outlet');
  await closePanel(page);await page.screenshot({path:join(directory,'water-en-live.png'),fullPage:true});
  await page.reload();await ready(page);expect(await(await page.request.get('/api/saves/download')).body()).toEqual(saved);
  await replace(page,original);await replace(page,saved);expect(await(await page.request.get('/api/saves/download')).body()).toEqual(saved);
 }finally{await replace(page,original);await context.close();}
});

test('Japanese narrow directional rivers work without lakes, show impossible rims and cancel a late preview',async({browser})=>{
 const context=await browser.newContext({viewport:{width:390,height:844},locale:'ja-JP'}),page=await context.newPage();await page.goto('/');await ready(page,'ja');
 let release=()=>{},seen=false;const held=new Promise<void>(r=>release=r);
 try{
  const original=await(await page.request.get('/api/saves/download')).body();
  await openPanel(page,'newWorld','ja');await page.getByLabel('世界の作成方法',{exact:true}).selectOption('generated');
  await page.getByLabel('川の方向',{exact:true}).selectOption('eastWest');await expect(page.getByRole('button',{name:'世界をプレビュー',exact:true})).toBeEnabled();
  await page.getByLabel('川幅（セル）',{exact:true}).fill('5');await expect(page.getByRole('button',{name:'世界をプレビュー',exact:true})).toBeDisabled();await page.getByLabel('川幅（セル）',{exact:true}).fill('1');
  await page.getByLabel('川の方向',{exact:true}).selectOption('none');await page.getByLabel('湖の数（0〜8）',{exact:true}).fill('8');
  await page.getByLabel('格納幅（H）',{exact:true}).fill('16');await page.getByLabel('格納奥行（V）',{exact:true}).fill('16');
  await page.getByRole('button',{name:'世界をプレビュー',exact:true}).click();await expect(page.getByTestId('creation-feedback')).toContainText('形成できません');expect(await(await page.request.get('/api/saves/download')).body()).toEqual(original);
  await page.getByLabel('格納幅（H）',{exact:true}).fill('64');await page.getByLabel('格納奥行（V）',{exact:true}).fill('64');await page.getByLabel('湖の数（0〜8）',{exact:true}).fill('1');
  await page.getByLabel('川の方向',{exact:true}).selectOption('northSouth');
  await page.getByRole('button',{name:'世界をプレビュー',exact:true}).click();await expect(page.getByTestId('water-metrics')).toContainText('湖：0');
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  const directory=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'water');mkdirSync(directory,{recursive:true});await page.getByLabel('川の方向',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'water-ja-narrow.png'),fullPage:true});
  await page.getByLabel('蛇行（0〜100）',{exact:true}).fill('80');await expect(page.getByTestId('empty-world-preview')).toHaveCount(0);
  await page.route('**/api/worlds/generated/preview',async route=>{const response=await route.fetch();seen=true;await held;await route.fulfill({response}).catch(()=>{});});
  await page.getByRole('button',{name:'世界をプレビュー',exact:true}).click();await expect.poll(()=>seen).toBe(true);await page.getByRole('button',{name:'キャンセル',exact:true}).click();release();await expect(page.getByTestId('empty-world-preview')).toHaveCount(0);
  expect(await(await page.request.get('/api/saves/download')).body()).toEqual(original);
 }finally{release();await context.close();}
});

test('forest controls reserve connected level land, show realized cover and confirm exact trees to both clients and Save7',async({page,browser})=>{
 await page.goto('/');await ready(page);const original=await(await page.request.get('/api/saves/download')).body();
 const context=await browser.newContext(),watcher=await context.newPage();await watcher.goto('/');await ready(watcher);
 try{
  await page.locator('canvas').evaluate(c=>c.dataset.instance='forest-preserved');
  await openPanel(page,'newWorld');await page.getByLabel('World source',{exact:true}).selectOption('generated');
  await page.getByLabel('Stored length (V)',{exact:true}).fill('27');await page.getByLabel('Levels (Z)',{exact:true}).fill('32');
  await page.getByLabel('Connected flat starting area (cells)',{exact:true}).fill('64');
  async function preview(){const waiting=page.waitForResponse(r=>r.url().includes('/worlds/generated/preview'));await page.getByRole('button',{name:'Preview world',exact:true}).click();const response=await waiting;expect(response.ok(),await response.text()).toBe(true);await expect(page.getByTestId('empty-world-preview')).toBeVisible();return response.json();}
  const bare=await preview();expect(bare.geography.forests).toMatchObject({forestCells:0,reservedCells:64});
  await page.getByLabel('Forest cover',{exact:true}).selectOption('70');await expect(page.getByTestId('empty-world-preview')).toHaveCount(0);
  await expect(page.getByRole('checkbox',{name:'Cedar',exact:true})).toBeChecked();
  const first=await preview();expect(first.ground).toEqual(bare.ground);expect(first.geography.seaMask).toBe(bare.geography.seaMask);expect(first.geography.startingMask).toBe(bare.geography.startingMask);expect(first.geography.forests.forestCells).toBeGreaterThan(400);
  await page.getByLabel('Forest clustering (0–100)',{exact:true}).fill('100');const clustered=await preview();expect(clustered.geography.forestMask).not.toBe(first.geography.forestMask);
  for(const tree of ['Pine','Camphor','Maple','Ginkgo','Beech'])await page.getByRole('checkbox',{name:tree,exact:true}).uncheck();
  await page.getByLabel('Forest ground appearance',{exact:true}).selectOption('grass');const final=await preview();
  expect(final.ground).toEqual(bare.ground);expect(final.geography.startingMask).toBe(bare.geography.startingMask);
  await expect(page.getByTestId('forest-metrics')).toContainText('Reserved: 64');
  const directory=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'forests');mkdirSync(directory,{recursive:true});await page.getByTestId('forest-metrics').scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'forest-en-preview.png'),fullPage:true});
  const before=(await state(page)).worldEpoch;expect(await(await page.request.get('/api/saves/download')).body()).toEqual(original);
  await page.getByRole('button',{name:'Create world',exact:true}).click();await page.locator('dialog.decision-dialog').getByRole('button',{name:'Cancel',exact:true}).click();await expect(page.getByTestId('empty-world-preview')).toBeVisible();
  await page.getByRole('button',{name:'Create world',exact:true}).click();await page.locator('dialog.decision-dialog').getByRole('button',{name:'Create world',exact:true}).click();
  await ready(page);await ready(watcher);await expect.poll(async()=>(await state(watcher)).worldEpoch).not.toBe(before);await expect(page.locator('canvas')).toHaveAttribute('data-instance','forest-preserved');
  const actual=(await state(page)).terrain;expect(actual.occupants.filter((o:any)=>o.forest).length).toBe(final.geography.forests.forestCells);
  expect((await state(watcher)).terrain.occupants).toEqual(actual.occupants);
  expect(actual.occupants.filter((o:any)=>o.forest).every((o:any)=>o.forest.definition.contributionId==='{FBD0CD06-016A-4037-AB15-02040218CBAA}-g')).toBe(true);
  const saved=await(await page.request.get('/api/saves/download')).body();expect(saved.length).toBe(final.saveBytes);
  await closePanel(page);await ready(page);await page.screenshot({path:join(directory,'forest-en-live.png'),fullPage:true});
  const pixels=await page.locator('canvas').evaluate(c=>c.toDataURL());await openPanel(page,'view');await page.getByLabel('Winter ground',{exact:true}).check();await expect(page.locator('canvas')).toHaveAttribute('data-terrain-variant','winter/day');await closePanel(page);
  expect(await page.locator('canvas').evaluate(c=>c.toDataURL())).not.toBe(pixels);expect(await(await page.request.get('/api/saves/download')).body()).toEqual(saved);
  await page.reload();await ready(page);expect(await(await page.request.get('/api/saves/download')).body()).toEqual(saved);
  await replace(page,original);await replace(page,saved);expect(await(await page.request.get('/api/saves/download')).body()).toEqual(saved);
 }finally{await replace(page,original);await context.close();}
});

test('Japanese narrow forests validate species and starting-area failure, invalidate drafts and cancel late results',async({browser})=>{
 const context=await browser.newContext({viewport:{width:390,height:844},locale:'ja-JP'}),page=await context.newPage();await page.goto('/');await ready(page,'ja');
 let release=()=>{};
 try{
  const original=await(await page.request.get('/api/saves/download')).body();await openPanel(page,'newWorld','ja');await page.getByLabel('世界の作成方法',{exact:true}).selectOption('generated');
  await page.getByLabel('森林の被覆',{exact:true}).selectOption('35');
  for(const tree of ['杉','松','楠','楓','銀杏','ブナ'])await page.getByRole('checkbox',{name:tree,exact:true}).uncheck();
  await expect(page.getByRole('button',{name:'世界をプレビュー',exact:true})).toBeDisabled();await page.getByRole('checkbox',{name:'楓',exact:true}).check();
  await page.getByLabel('森林の選択率（0〜90%）',{exact:true}).fill('91');await expect(page.getByRole('button',{name:'世界をプレビュー',exact:true})).toBeDisabled();await page.getByLabel('森林の選択率（0〜90%）',{exact:true}).fill('35');
  await page.getByLabel('格納奥行（V）',{exact:true}).fill('27');await page.getByLabel('高さ（Z）',{exact:true}).fill('32');
  await page.getByLabel('海岸の配置',{exact:true}).selectOption('island');await page.getByLabel('連続した平坦な開始区域（セル）',{exact:true}).fill('200');
  await page.getByRole('button',{name:'世界をプレビュー',exact:true}).click();await expect(page.getByTestId('creation-feedback')).toContainText('開始区域が見つかりません');expect(await(await page.request.get('/api/saves/download')).body()).toEqual(original);
  await page.getByLabel('連続した平坦な開始区域（セル）',{exact:true}).fill('16');await page.getByRole('button',{name:'世界をプレビュー',exact:true}).click();await expect(page.getByTestId('forest-metrics')).toContainText('開始区域：16');
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  const directory=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'forests');mkdirSync(directory,{recursive:true});await page.getByLabel('森林の地面表示',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'forest-ja-narrow.png'),fullPage:true});
  await page.getByLabel('森林のまとまり（0〜100）',{exact:true}).fill('100');await expect(page.getByTestId('empty-world-preview')).toHaveCount(0);
  const held=new Promise<void>(r=>release=r);let seen=false;await page.route('**/api/worlds/generated/preview',async route=>{const response=await route.fetch();seen=true;await held;await route.fulfill({response}).catch(()=>{});});
  await page.getByRole('button',{name:'世界をプレビュー',exact:true}).click();await expect.poll(()=>seen).toBe(true);await page.getByRole('button',{name:'キャンセル',exact:true}).click();release();await expect(page.getByTestId('empty-world-preview')).toHaveCount(0);
  expect(await(await page.request.get('/api/saves/download')).body()).toEqual(original);
 }finally{release();await context.close();}
});
