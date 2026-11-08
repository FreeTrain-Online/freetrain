import { readFileSync } from 'node:fs';
import { expect,test,type Page } from './fixtures.ts';
import { canonicalState,closePanel,openPanel,ready } from './browser-helpers.ts';
const compat='protocolVersion=8&contentVersion=terrain-content-1';
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

test('intersection selection stays fixed while held buttons raise and lower all four touching tiles; release and Escape stop',async({page})=>{
 const pending=new Set<string>();
 page.on('websocket',socket=>{
  socket.on('framesent',frame=>{const message=JSON.parse(String(frame.payload));if(message.type==='command'&&message.payload?.kind==='editCorner')pending.add(message.requestId);});
  socket.on('framereceived',frame=>{const message=JSON.parse(String(frame.payload));if(message.type==='accepted'||message.type==='rejected')pending.delete(message.requestId);});
 });
 await page.goto('/');await ready(page);const original=await(await page.request.get('/api/saves/download')).body();
 try{
  await empty(page);await page.getByRole('button',{name:'Modify terrain',exact:true}).click();
  await expect(page.getByLabel('Terrain selection',{exact:true})).toHaveValue('paint');
  await expect(page.getByLabel('Terrain selection',{exact:true}).locator('option[value=corner]')).toHaveCount(0);
  const before=await(await page.request.get('/api/saves/download')).body();
  const p=await pixel(page,20,20,1,16,3);await page.mouse.click(p.x,p.y);
  await expect(page.locator('canvas')).toHaveAttribute('data-terrain-corner','20,20');
  await expect(page.locator('.corner-changes li')).toHaveCount(4);
  expect(await(await page.request.get('/api/saves/download')).body()).toEqual(before);
  const raise=page.getByRole('button',{name:'Raise corner',exact:true}),lower=page.getByRole('button',{name:'Lower corner',exact:true});
  await raise.click();await expect.poll(async()=>Number((await state(page)).terrain.geometryRevision)).toBe(1);
  await page.waitForTimeout(300);expect(Number((await state(page)).terrain.geometryRevision)).toBe(1);
  await raise.hover();await page.mouse.down();
  await expect.poll(async()=>Number((await state(page)).terrain.geometryRevision)).toBeGreaterThanOrEqual(2);
  await page.mouse.up();await expect(page.getByTestId('paint-status')).toHaveAttribute('data-active','false');
  // A commit sent before release may finish after the hold becomes inactive.
  await expect.poll(()=>pending.size).toBe(0);
  const raised=await state(page);await page.waitForTimeout(350);expect((await state(page)).terrain.geometryRevision).toBe(raised.terrain.geometryRevision);
  expect(raised.terrain.occupants).toHaveLength(4);
  expect(raised.terrain.occupants.every((o:any)=>o.ne+o.se+o.sw+o.nw>=2)).toBe(true);
  await expect(page.locator('canvas')).toHaveAttribute('data-terrain-corner','20,20');

  await lower.focus();await page.keyboard.down(' ');
  await expect.poll(async()=>Number((await state(page)).terrain.geometryRevision)).toBeGreaterThan(Number(raised.terrain.geometryRevision));
  await page.keyboard.up(' ');await expect(page.getByTestId('paint-status')).toHaveAttribute('data-active','false');
  await expect.poll(()=>pending.size).toBe(0);
  const stopped=await state(page);await page.waitForTimeout(350);expect((await state(page)).terrain.geometryRevision).toBe(stopped.terrain.geometryRevision);
  await raise.hover();await page.mouse.down();await expect(page.getByTestId('paint-status')).toHaveAttribute('data-active','true');
  await page.keyboard.press('Escape');await page.mouse.up();await expect(page.getByTestId('paint-status')).toHaveAttribute('data-active','false');
  await expect(page.locator('canvas')).toHaveAttribute('data-terrain-corner','20,20');
  await openPanel(page,'help');await closePanel(page);await expect(page.locator('canvas')).toHaveAttribute('data-terrain-corner','');
 }finally{await page.mouse.up();await replace(page,original);}
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
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);

  await page.getByRole('button',{name:'地形の変更を確定',exact:true}).focus();await page.keyboard.press('Enter');
  await expect.poll(async()=>JSON.parse((await(await page.request.get('/api/saves/download')).body()).toString()).packedTerrain.occupantCount).toBe(4);
  expect((await state(page)).paused).toBe(true);
 }finally{await replace(page,original);await context.close();}
});

test('large terrain edits redraw locally and match a full render with overlapping forests and removed art',{tag:'@performance'},async({page})=>{
 test.setTimeout(120_000);await page.goto('/');await ready(page);const original=await(await page.request.get('/api/saves/download')).body(),errors:string[]=[];
 page.on('pageerror',e=>errors.push(String(e)));
 try{
  const s=await state(page),r=await page.request.post('/api/worlds/empty/preview',{data:{name:'Local terrain redraw',dimensions:{h:400,v:600,z:24},waterLevel:4,epoch:s.worldEpoch,revision:s.revision}});expect(r.ok()).toBe(true);const c=await r.json();
  expect((await page.request.post('/api/worlds/empty/commit',{data:{id:c.id,requestHash:c.requestHash,candidateHash:c.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).ok()).toBe(true);
  // Explicit authored forests: one follows the edited slope and is eventually
  // removed; a nearby canopy outside the validated ring must survive every
  // clipped repaint as an unchanged overlapping operation.
  const doc=JSON.parse((await(await page.request.get('/api/saves/download')).body()).toString()),t=doc.packedTerrain;
  const profile=JSON.parse(readFileSync('src/generated/terrain-profile.json','utf8')),forest=profile.definitions.find((d:any)=>d.treeSpriteCount>0&&d.treeDensity>0);
  t.definitions=[profile.definitions[0].reference,forest.reference];t.nextEntityId='3';doc.spatial.nextEntityId='3';t.occupantCount=2;
  const data=Buffer.alloc(98);
  for(const [i,x,y] of [[0,349,350],[1,352,350]]){
   const row=data.subarray(i*49,(i+1)*49);row.writeBigInt64LE(BigInt(i+1));row.writeInt32LE(x,18);row.writeInt32LE(y,22);row[26]=4;row[27]=5;row.writeBigInt64LE(BigInt(forest.price),28);row.writeUInt16LE(1,40);row[42]=2;
   row.set([0,0,0,30,15,forest.treeSpriteCount-1],43);
  }t.occupants=data.toString('base64');await replace(page,Buffer.from(JSON.stringify(doc)));
  const loaded=await state(page),canvas=page.locator('canvas');await expect(canvas).toHaveAttribute('data-world-epoch',loaded.worldEpoch);await ready(page);await expect(canvas).toHaveAttribute('data-presentation','ready');
  await canvas.evaluate(el=>el.setAttribute('data-instance','local-terrain'));const camera=await canvas.getAttribute('data-camera');
  await page.getByRole('button',{name:'Modify terrain',exact:true}).click();
  const hash=()=>canvas.evaluate(async el=>{const c=el as HTMLCanvasElement,data=c.getContext('2d')!.getImageData(0,0,c.width,c.height).data;return Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',data)),v=>v.toString(16).padStart(2,'0')).join('');});
  const timings=[];
  for(const action of ['raise','raise','lower','lower']){
   for(const [label,value] of [['Corner cell X',349],['Corner cell Y',350],['Corner cell Z',4]] as const)await page.getByLabel(label,{exact:true}).fill(String(value));
   await page.getByLabel('Terrain action',{exact:true}).selectOption(action);
   await page.getByRole('button',{name:'Preview corner',exact:true}).click();await expect(page.getByTestId('corner-status')).toContainText('change is legal');
   await canvas.evaluate(el=>{
    const w=window as any;w.__terrainEditTiming={start:0,ready:0,pending:0};
    document.querySelector<HTMLButtonElement>('.tool-card button.primary')!.addEventListener('click',()=>{w.__terrainEditTiming.start=performance.now();},{once:true});
    const observer=new MutationObserver(records=>{const t=w.__terrainEditTiming;if(!t.start)return;
     if(records.some(r=>r.oldValue==='ready')&&el.getAttribute('data-presentation')==='pending'&&!t.pending)t.pending=performance.now();
     if(records.some(r=>r.oldValue==='pending')&&el.getAttribute('data-presentation')==='ready'){t.ready=performance.now();observer.disconnect();}
    });observer.observe(el,{attributes:true,attributeOldValue:true,attributeFilter:['data-presentation']});
   });
   await page.getByRole('button',{name:'Confirm terrain change',exact:true}).click();await expect(page.locator('.command-feedback')).toContainText('Confirmed by server');
   await page.waitForFunction(()=>(window as any).__terrainEditTiming.ready>0);
   timings.push(await page.evaluate(()=>{const t=(window as any).__terrainEditTiming;return {visualMs:t.ready-t.start,refineMs:t.ready-t.pending};}));
   await expect(canvas).toHaveAttribute('data-terrain-update','local');const local=await hash();
   // A variant change invalidates the entire raster, providing an independent
   // full-render pixel oracle at the same camera, cut, and terrain revision.
   await openPanel(page,'view');await page.getByLabel('Night',{exact:true}).check();await expect(canvas).toHaveAttribute('data-terrain-variant',/night$/);await expect(canvas).toHaveAttribute('data-presentation','ready');
   await page.getByLabel('Night',{exact:true}).uncheck();await closePanel(page);await expect(canvas).toHaveAttribute('data-terrain-variant',/day$/);await expect(canvas).toHaveAttribute('data-presentation','ready');
   await expect(canvas).toHaveAttribute('data-terrain-update','full');expect(await hash()).toBe(local);
  }
  const after=JSON.parse((await(await page.request.get('/api/saves/download')).body()).toString());expect(after.packedTerrain.occupantCount).toBe(1);
  expect(Buffer.from(after.packedTerrain.occupants,'base64')).toEqual(data.subarray(49));
  await expect(canvas).toHaveAttribute('data-instance','local-terrain');expect(await canvas.getAttribute('data-camera')).toBe(camera);expect(errors).toEqual([]);
  console.log(JSON.stringify({configuration:process.env.FREETRAIN_ONLINE_TEST_SERVER_CONFIGURATION??'Release',dimensions:'400x600x24',timings}));
 }finally{await replace(page,original);}
});
