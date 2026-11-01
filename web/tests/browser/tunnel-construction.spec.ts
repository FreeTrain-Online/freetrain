import {test,expect,type Page} from '@playwright/test';
import {mkdirSync,readFileSync} from 'node:fs';
import {join} from 'node:path';
import {ready,openPanel,closePanel,canonicalState as state} from './ux-helpers.ts';
const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
const tunnel='{3E66FC7A-2B58-4182-8A8E-2D5A01BE78E5}';
const out=(name:string)=>{const d=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'tunnel');mkdirSync(d,{recursive:true});return join(d,name);};
async function selectTunnel(p:Page,ja=false){const button=p.getByRole('button',{name:ja?'トンネル':'Tunnel',exact:true});if(await button.getAttribute('aria-pressed')!=='true')await button.click();}
async function save(p:Page){return(await p.request.get('/api/saves/download')).body();}
async function replace(p:Page,bytes:Buffer){const s=await state(p);const r=await p.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:bytes});expect(r.ok(),await r.text()).toBe(true);await ready(p,(await p.locator('html').getAttribute('lang'))??'en');}
async function empty(p:Page){
 const s=await state(p),r=await p.request.post('/api/worlds/empty/preview',{data:{name:'Tunnel restoration workflow',dimensions:{h:30,v:42,z:6},waterLevel:1,epoch:s.worldEpoch,revision:s.revision}});expect(r.ok()).toBe(true);
 const c=await r.json();expect((await p.request.post('/api/worlds/empty/commit',{data:{id:c.id,requestHash:c.requestHash,candidateHash:c.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).ok()).toBe(true);await ready(p);
}
// Explicit canonical authored terrain, distinct from a generation-parity fixture.
async function mountains(p:Page,points:number[][]){
 await empty(p);const doc=JSON.parse((await save(p)).toString()),t=doc.packedTerrain;
 const grid=Buffer.alloc(30*42,2|(1<<6));
 // A buried ordinary gap between two Mountain cells exercises underground cuts.
 if(points.length===2){const x=(points[0][0]+points[1][0])/2,y=(points[0][1]+points[1][1])/2,h=Math.floor((x-20+y)/2),v=y-x+20;grid[v*30+h]=4|(1<<6);}
 t.cells=grid.toString('base64');
 t.definitions=[{packId:'exav-stable-bundled',contributionId:'system:terrain/mountain',recipeId:'geometry:quarter-mountain-1'}];
 t.occupantCount=points.length;t.nextEntityId=String(points.length+1);const data=Buffer.alloc(points.length*43);
 for(let i=0;i<points.length;i++){
  const b=data.subarray(i*43,(i+1)*43);b.writeBigInt64LE(BigInt(i+1));b.writeInt32LE(points[i][0],18);b.writeInt32LE(points[i][1],22);b[26]=2;b[27]=1;b.writeUInt16LE(0x0123,37);b.writeUInt16LE(65535,40);
 }t.occupants=data.toString('base64');doc.spatial.nextEntityId=t.nextEntityId;
 await replace(p,Buffer.from(JSON.stringify(doc)));return save(p);
}
async function target(p:Page,from=[30,27],to=[34,27],z=2,ja=false){
 for(const [name,value] of [[ja?'始点 X':'Start X',from[0]],[ja?'始点 Y':'Start Y',from[1]],[ja?'終点 X':'End X',to[0]],[ja?'終点 Y':'End Y',to[1]],[ja?'線路の高さ':'Track level',z]] as const)await p.getByLabel(name,{exact:true}).fill(String(value));
 await p.getByRole('button',{name:ja?'経路をプレビュー':'Preview route',exact:true}).click();
}
async function confirm(p:Page,ja=false){await p.getByRole('button',{name:ja?'トンネルの変更を確定':'Confirm tunnel change',exact:true}).click();await expect(p.getByTestId('rail-route')).toHaveCount(0);await expect(p.locator('.command-feedback')).toContainText(ja?'サーバーで確定':'Confirmed by server');}
async function cut(p:Page,z:number){await openPanel(p,'view');await p.getByLabel('Height cut',{exact:true}).fill(String(z));await closePanel(p);await expect(p.locator('canvas')).toHaveAttribute('data-terrain-cut',String(z));}
async function click(p:Page,x:number,y:number,z:number){
 const q=await p.locator('canvas').evaluate((el,a)=>{const c=el as HTMLCanvasElement,r=c.getBoundingClientRect(),[cx,cy]=c.dataset.camera!.split(',').map(Number),zoom=Number(c.dataset.zoom);return{x:r.x+r.width/2+(16*(a.x+a.y-20)+16-cx)*zoom,y:r.y+r.height/2+(8*(-a.x+a.y+20)-16*a.z+8-cy)*zoom};},{x,y,z});await p.mouse.click(q.x,q.y);
}

async function pixels(p:Page,voxels:any[]){
 await expect(p.locator('canvas')).toHaveAttribute('data-presentation','ready');
 return p.locator('canvas').evaluate(async(el,args)=>{
  const canvas=el as HTMLCanvasElement,r=canvas.getBoundingClientRect(),[cx,cy]=canvas.dataset.camera!.split(',').map(Number),zoom=Number(canvas.dataset.zoom),variant=canvas.dataset.terrainVariant!;
  const actual=canvas.getContext('2d')!,counts:number[]=[];
  for(const v of args.voxels){
   const rail=v.traffic?.rail,recipe=args.catalog.renderRecipes.find((r:any)=>r.id===(rail?.definition.recipeId??v.definition.recipeId));
   const frames=rail?recipe.frames.filter((f:any)=>f.selector.index===rail.variant):recipe.frames;
   let count=0;
   for(const f of frames){
    const image=new Image();image.src=args.catalog.surfaceSets[f.surfaceSet][variant].image.url;await image.decode();
    const sheet=document.createElement('canvas');sheet.width=image.naturalWidth;sheet.height=image.naturalHeight;const ctx=sheet.getContext('2d')!;ctx.drawImage(image,0,0);
    const [sx,sy,w,h]=f.rectangle,data=ctx.getImageData(sx,sy,w,h).data;
    const x=r.width/2+(16*(v.position.x+v.position.y-20)-f.anchor[0]-cx)*zoom;
    const y=r.height/2+(8*(-v.position.x+v.position.y+20)-16*v.position.z-f.anchor[1]-cy)*zoom;
    for(let b=0;b<h;b++)for(let a=0;a<w;a++){
     const i=(b*w+a)*4;if(data[i+3]!==255)continue;
     const px=Math.floor((x+(a+.5)*zoom)*canvas.width/r.width),py=Math.floor((y+(b+.5)*zoom)*canvas.height/r.height);
     if(px<0||py<0||px>=canvas.width||py>=canvas.height)continue;
     const pixel=actual.getImageData(px,py,1,1).data;
     if(pixel[0]===data[i]&&pixel[1]===data[i+1]&&pixel[2]===data[i+2])count++;
    }
   }counts.push(count);
  }return counts;
 },{catalog,voxels});
}

test('four cardinal routes, both removal orders, mixed ordinary rail and exact saved corner restoration',async({page})=>{
 test.setTimeout(120_000);await page.goto('/');await ready(page);const original=await save(page);
 try{
  for(const [x,y,variant,pattern] of [[34,27,0,4],[26,27,0,4],[30,23,1,10],[30,31,1,10]]){
   const before=await mountains(page,[[30,27],[x,y]]),terrainBefore=(await state(page)).terrain;
   await selectTunnel(page);await target(page,[30,27],[x,y]);
   await expect(page.getByTestId('rail-status')).toContainText('route is legal');await expect(page.getByTestId('rail-cost')).toContainText('60,000,000');
   expect(await save(page)).toEqual(before);await page.getByRole('button',{name:'Cancel preview',exact:true}).click();expect(await save(page)).toEqual(before);
   await target(page,[30,27],[x,y]);await confirm(page);const s=await state(page);
   expect(s.mixed.voxels).toHaveLength(5);expect(s.mixed.voxels.filter((v:any)=>v.traffic.rail.definition.recipeId===tunnel)).toHaveLength(2);
   expect(s.mixed.voxels.filter((v:any)=>v.traffic.rail.definition.recipeId===tunnel).every((v:any)=>v.traffic.rail.variant===variant&&v.traffic.rail.pattern===pattern&&JSON.stringify(v.traffic.rail.terrainBackup)===JSON.stringify({ne:0,se:1,sw:2,nw:3}))).toBe(true);
   const captured=await save(page);await replace(page,captured);expect(await save(page)).toEqual(captured);await cut(page,2);
   expect((await pixels(page,s.mixed.voxels.filter((v:any)=>v.traffic.rail.definition.recipeId===tunnel))).every(n=>n>10)).toBe(true);
   await page.getByRole('button',{name:'Inspect',exact:true}).click();
   await click(page,(30+x)/2,(27+y)/2,2);await expect(page.getByTestId('selected-object')).toContainText('Railway');await closePanel(page);
   await page.screenshot({path:out(`tunnel-${x}-${y}-cut2.png`),fullPage:true});await cut(page,5);
   await selectTunnel(page);await page.getByLabel('Track action',{exact:true}).selectOption('remove');await target(page,[30,27],[x,y]);await expect(page.getByTestId('rail-cost')).toContainText('4,000,000');await confirm(page);
   let after=await state(page);expect(after.mixed.voxels).toHaveLength(4);expect(after.mixed.voxels.find((v:any)=>v.position.x===x&&v.position.y===y).traffic.rail.definition.recipeId).toBe(tunnel);
   await target(page,[x,y],[30,27]);await confirm(page);after=await state(page);
   expect(after.mixed.voxels).toHaveLength(3);expect(after.terrain.cells).toEqual(terrainBefore.cells);
   expect(after.terrain.occupants.map((o:any)=>[o.ne,o.se,o.sw,o.nw,o.forest])).toEqual([[0,1,2,3,null],[0,1,2,3,null]]);
   expect(after.entities.filter((e:any)=>after.terrain.occupants.some((o:any)=>o.entityId===e.id)).every((e:any)=>!e.owned)).toBe(true);
  }
 }finally{await replace(page,original);}
});

test('cut map picks, keyboard shortcut, illegal and cancelled work, stale quote and two peers preserve canvas',async({page,browser})=>{
 await page.goto('/');await ready(page);const original=await save(page),peer=await browser.newPage();
 try{
  const before=await mountains(page,[[30,27],[34,27]]);await peer.goto('/');await ready(peer);
  const canvas=page.locator('canvas');await canvas.evaluate(el=>(el as any).__tunnelIdentity=true);await canvas.focus();await page.keyboard.press('u');
  await expect(page.getByRole('heading',{name:'Tunnel',exact:true})).toBeVisible();
  await target(page,[30,27],[34,27],0);await expect(page.getByTestId('rail-status')).toContainText('crosses water');await expect(page.getByRole('button',{name:'Confirm tunnel change',exact:true})).toBeDisabled();expect(await save(page)).toEqual(before);
  await target(page,[30,29],[34,29]);await expect(page.getByTestId('rail-status')).toContainText('Mountain cell');expect(await save(page)).toEqual(before);
  await page.getByRole('button',{name:'Cancel preview',exact:true}).click();await cut(page,2);await selectTunnel(page);
  await click(page,30,27,2);await click(page,34,27,2);await expect(page.getByTestId('rail-status')).toContainText('route is legal');
  await page.getByRole('button',{name:'Cancel preview',exact:true}).click();expect(await save(page)).toEqual(before);
  await target(page);await selectTunnel(peer);await target(peer);await confirm(page);
  await expect(peer.getByTestId('rail-status')).toContainText('world changed');await expect(peer.getByRole('button',{name:'Confirm tunnel change',exact:true})).toBeDisabled();
  expect(await canvas.evaluate(el=>(el as any).__tunnelIdentity)).toBe(true);expect(await state(peer)).toEqual(await state(page));
  await page.screenshot({path:out('tunnel-two-peer-cut.png'),fullPage:true});
 }finally{await peer.close();await replace(page,original);}
});

test('Japanese narrow DPR2 tunnel confirmation, automatic level and destination exclusion remain usable',async({browser})=>{
 const context=await browser.newContext({viewport:{width:430,height:850},deviceScaleFactor:2}),page=await context.newPage();
 await page.goto('/');await ready(page);const original=await save(page);
 try{
  await mountains(page,[[30,27],[34,27]]);await openPanel(page,'settings');await page.getByLabel('Language',{exact:true}).selectOption('ja');await closePanel(page);await ready(page,'ja');
  await selectTunnel(page,true);await target(page,[30,27],[34,27],2,true);
  await page.getByLabel('始点の高さを自動検出',{exact:true}).check();await expect(page.getByLabel('線路の高さ',{exact:true})).toHaveValue('2');
  await expect(page.getByRole('button',{name:'トンネルの変更を確定',exact:true})).toBeEnabled();await page.screenshot({path:out('tunnel-ja-narrow-preview.png'),fullPage:true});await confirm(page,true);
  await page.getByLabel('線路の操作',{exact:true}).selectOption('remove');await target(page,[30,27],[34,27],2,true);await confirm(page,true);
  expect((await state(page)).mixed.voxels).toHaveLength(4);
 }finally{await replace(page,original);await context.close();}
});

async function slopedRidge(p:Page,dimensions={h:30,v:42,z:12},portalX=30){
 const s=await state(p),r=await p.request.post('/api/worlds/empty/preview',{data:{name:'Natural sloped tunnel portals',dimensions,waterLevel:4,epoch:s.worldEpoch,revision:s.revision}});expect(r.ok()).toBe(true);
 const c=await r.json();expect((await p.request.post('/api/worlds/empty/commit',{data:{id:c.id,requestHash:c.requestHash,candidateHash:c.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).ok()).toBe(true);await ready(p);
 const doc=JSON.parse((await save(p)).toString()),t=doc.packedTerrain,grid=Buffer.alloc(dimensions.h*dimensions.v),rows:Buffer[]=[];let id=1;const center=Math.floor((dimensions.v-1)/2),endX=portalX+13;
 for(let v=0;v<dimensions.v;v++)for(let h=0;h<dimensions.h;h++){
  const x=h-Math.floor(v/2)+center,y=h+Math.ceil(v/2);grid[v*dimensions.h+h]=x>portalX&&x<endX?7:6;
  if(x!==portalX&&x!==endX)continue;
  const row=Buffer.alloc(43);row.writeBigInt64LE(BigInt(id++));row.writeInt32LE(x,18);row.writeInt32LE(y,22);row[26]=6;row.writeUInt16LE(x===portalX?0x4400:0x0044,37);row.writeUInt16LE(65535,40);rows.push(row);
 }
 t.cells=grid.toString('base64');t.occupants=Buffer.concat(rows).toString('base64');t.occupantCount=rows.length;t.nextEntityId=String(id);
 t.definitions=[{packId:'exav-stable-bundled',contributionId:'system:terrain/mountain',recipeId:'geometry:quarter-mountain-1'}];doc.spatial.nextEntityId=String(id);
 await replace(p,Buffer.from(JSON.stringify(doc)));
 const loaded=await state(p);await expect(p.locator('canvas')).toHaveAttribute('data-world-epoch',loaded.worldEpoch);await ready(p);
}

test('outside and inside sloped-hill clicks resolve portals, automatic height and actual quoted cost before exact reopen removal',async({page})=>{
 await page.goto('/');await ready(page);const original=await save(page);
 try{
  await slopedRidge(page);const before=await save(page),snapshotBefore=await state(page),terrainBefore=snapshotBefore.terrain;await selectTunnel(page);
  for(const [a,b] of [[29,44],[30,43],[31,42]]){
   await target(page,[a,27],[b,27],6);await expect(page.getByTestId('rail-status')).toContainText('route is legal');
   await expect(page.getByTestId('rail-route')).toContainText('30, 27, 6 → 43, 27, 6');await expect(page.getByTestId('rail-cost')).toContainText('168,000,000');
   expect(await save(page)).toEqual(before);await page.getByRole('button',{name:'Cancel preview',exact:true}).click();
  }
  // A surface pick one cell into the hill is a whole level higher. Automatic
  // detection can choose the nearby lower portals; manual level7 remains exact.
  await target(page,[31,27],[42,27],7);await expect(page.getByRole('button',{name:'Confirm tunnel change',exact:true})).toBeDisabled();
  await page.getByLabel('Auto-detect start level',{exact:true}).check();
  await expect(page.getByLabel('Track level',{exact:true})).toHaveValue('6');await expect(page.getByTestId('rail-route')).toContainText('30, 27, 6 → 43, 27, 6');
  await expect(page.getByRole('button',{name:'Confirm tunnel change',exact:true})).toBeEnabled();
  await page.getByRole('button',{name:'Cancel preview',exact:true}).click();
  await click(page,31,27,7);await click(page,42,27,7);
  await expect(page.getByTestId('rail-route')).toContainText('30, 27, 6 → 43, 27, 6');await expect(page.getByLabel('Track level',{exact:true})).toHaveValue('6');
  const refreshed=page.waitForResponse(r=>r.url().includes('/api/railway/preview'));
  await page.getByRole('button',{name:'Preview route',exact:true}).click();
  const quote=await(await refreshed).json();expect(quote.query.from.z).toBe(7);expect(quote.cells[0].position.z).toBe(6);
  await expect(page.getByRole('button',{name:'Confirm tunnel change',exact:true})).toBeEnabled();
  await page.screenshot({path:out('tunnel-resolved-natural-portals.png'),fullPage:true});
  await confirm(page);const built=await state(page);expect(built.mixed.voxels).toHaveLength(14);expect(built.mixed.voxels.every((v:any)=>v.position.z===6)).toBe(true);
  const captured=await save(page);await replace(page,captured);expect(await save(page)).toEqual(captured);await cut(page,6);await selectTunnel(page);
  await page.getByLabel('Track action',{exact:true}).selectOption('remove');await target(page,[30,27],[43,27],6);await confirm(page);
  await target(page,[43,27],[30,27],6);await confirm(page);
  const after=await state(page);expect(after.terrain.cells).toEqual(terrainBefore.cells);const geometry=(s:any)=>s.terrain.occupants.map((o:any)=>{const p=s.entities.find((e:any)=>e.id===o.entityId).anchor;return JSON.stringify([p.x,p.y,p.z,o.kind,o.ne,o.se,o.sw,o.nw]);}).sort();
  expect(geometry(after)).toEqual(geometry(snapshotBefore));
 }finally{await replace(page,original);}
});

test('an older server rejecting the route request gives actionable feedback and permits retry without mutation',async({page})=>{
 await page.goto('/');await ready(page);const original=await save(page);
 try{
  await slopedRidge(page);const before=await save(page);await selectTunnel(page);
  await page.route('**/api/railway/preview?**',route=>route.fulfill({status:400,contentType:'application/json',body:JSON.stringify({code:'invalidInput'})}));
  await target(page,[30,27],[43,27],6);await expect(page.getByTestId('rail-status')).toContainText('restart the server');await expect(page.getByRole('button',{name:'Confirm tunnel change',exact:true})).toBeDisabled();expect(await save(page)).toEqual(before);
  await page.unroute('**/api/railway/preview?**');await page.getByRole('button',{name:'Preview route',exact:true}).click();await expect(page.getByRole('button',{name:'Confirm tunnel change',exact:true})).toBeEnabled();
  expect(await save(page)).toEqual(before);
 }finally{await replace(page,original);}
});


test('large-world tunnel placement and restoration retain the socket, canvas and live screen through promotion and deltas',async({page})=>{
 test.setTimeout(120_000);let bootstraps=0,closes=0,resyncs=0,chunks=0;const durations:number[]=[];
 page.on('request',r=>{if(r.url().includes('/api/bootstrap?'))bootstraps++;});
 page.on('websocket',ws=>{
  ws.on('close',()=>closes++);
  ws.on('framesent',e=>{try{if(JSON.parse(String(e.payload)).type==='resync')resyncs++;}catch{}});
  ws.on('framereceived',e=>{try{if(JSON.parse(String(e.payload)).type==='stateChunk')chunks++;}catch{}});
 });
 await page.goto('/');await ready(page);const original=await save(page);
 try{
  await slopedRidge(page,{h:400,v:600,z:24},450);await ready(page);bootstraps=0;closes=0;resyncs=0;chunks=0;
  const canvas=page.locator('canvas');await canvas.evaluate(el=>(el as any).__largeTunnelCanvas=true);const camera=await canvas.getAttribute('data-camera');
  await page.locator('.app-shell').evaluate(el=>{
   (window as any).__tunnelGates=0;new MutationObserver(()=>{if(el.classList.contains('gated'))(window as any).__tunnelGates++;}).observe(el,{attributes:true,attributeFilter:['class']});
  });
  await selectTunnel(page);
  const commit=async()=>{const t=Date.now();await confirm(page);durations.push(Date.now()-t);await ready(page);};
  for(const y of [450,449]){
   await target(page,[450,y],[463,y],6);await expect(page.getByRole('button',{name:'Confirm tunnel change',exact:true})).toBeEnabled();await commit();
  }
  await page.getByLabel('Track action',{exact:true}).selectOption('remove');
  await target(page,[450,450],[463,450],6);await commit();await target(page,[463,450],[450,450],6);await commit();
  const saved=JSON.parse((await save(page)).toString());expect(saved.mixed.accounts.transactions).toHaveLength(4);
  expect(saved.mixed.voxels).toHaveLength(26);expect(chunks).toBeGreaterThan(0);
  expect(bootstraps).toBe(0);expect(closes).toBe(0);expect(resyncs).toBe(0);expect(await page.evaluate(()=>(window as any).__tunnelGates)).toBe(0);
  expect(await canvas.evaluate(el=>(el as any).__largeTunnelCanvas)).toBe(true);await expect(canvas).toHaveAttribute('data-camera',camera!);
  console.log(JSON.stringify({configuration:process.env.FREETRAIN_ONLINE_TEST_SERVER_CONFIGURATION??'Release',durations,bootstraps,closes,resyncs,chunks}));
  await page.screenshot({path:out('tunnel-large-world-same-connection.png'),fullPage:true});
 }finally{await replace(page,original);}
});
