import {test,expect,type Page,type Browser} from '@playwright/test';
import {mkdirSync,readFileSync} from 'node:fs';
import {join} from 'node:path';
import {ready,openPanel,closePanel,canonicalState as state} from './ux-helpers.ts';
const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
const out=(name:string)=>{const d=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'slope');mkdirSync(d,{recursive:true});return join(d,name);};
async function save(p:Page){return(await p.request.get('/api/saves/download')).body();}
async function replace(p:Page,bytes:Buffer){const s=await state(p);expect((await p.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:bytes})).ok()).toBe(true);await ready(p);}
async function empty(p:Page,ja=false){
 const s=await state(p),r=await p.request.post('/api/worlds/empty/preview',{data:{name:'Slope workflow',dimensions:{h:30,v:42,z:6},waterLevel:1,epoch:s.worldEpoch,revision:s.revision}});expect(r.ok()).toBe(true);
 const c=await r.json();expect((await p.request.post('/api/worlds/empty/commit',{data:{id:c.id,requestHash:c.requestHash,candidateHash:c.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).ok()).toBe(true);
 await expect(p.locator('canvas')).toHaveAttribute('data-ruleset','terrain-state-1');await ready(p,ja?'ja':'en');
}
async function target(p:Page,x=30,y=27,z=1,d=2,ja=false){
 for(const [name,value] of [[ja?'勾配 X':'Slope X',x],[ja?'勾配 Y':'Slope Y',y],[ja?'下端・選択位置の高さ':'Base / selected level',z]] as const)await p.getByLabel(name,{exact:true}).fill(String(value));
 await p.getByLabel(ja?'上り方向':'Uphill direction',{exact:true}).selectOption(String(d));
 await p.getByRole('button',{name:ja?'勾配をプレビュー':'Preview slope',exact:true}).click();
}
async function confirm(p:Page,ja=false){await p.getByRole('button',{name:ja?'勾配の変更を確定':'Confirm slope change',exact:true}).click();await expect(p.getByTestId('slope-base')).toHaveCount(0);await expect(p.locator('.command-feedback')).toContainText(ja?'サーバーで確定':'Confirmed by server');}
async function click(p:Page,x:number,y:number,z:number){
 const q=await p.locator('canvas').evaluate((el,a)=>{const c=el as HTMLCanvasElement,r=c.getBoundingClientRect(),[cx,cy]=c.dataset.camera!.split(',').map(Number),zoom=Number(c.dataset.zoom);return{x:r.x+r.width/2+(16*(a.x+a.y-20)+16-cx)*zoom,y:r.y+r.height/2+(8*(-a.x+a.y+20)-16*a.z+8-cy)*zoom};},{x,y,z});
 await p.mouse.click(q.x,q.y);
}
async function cut(p:Page,z:number){await openPanel(p,'view');await p.getByLabel('Height cut',{exact:true}).fill(String(z));await closePanel(p);await expect(p.locator('canvas')).toHaveAttribute('data-terrain-cut',String(z));}
async function track(p:Page,from:number[],to:number[],z:number){
 if(await p.getByRole('button',{name:'Preview route',exact:true}).count()===0)await p.getByRole('button',{name:'Track',exact:true}).click();
 for(const [name,value] of [['Start X',from[0]],['Start Y',from[1]],['End X',to[0]],['End Y',to[1]],['Track level',z]] as const)await p.getByLabel(name,{exact:true}).fill(String(value));
 await p.getByRole('button',{name:'Preview route',exact:true}).click();await expect(p.getByTestId('rail-status')).toContainText('route is legal');await p.getByRole('button',{name:'Confirm track change',exact:true}).click();await expect(p.getByTestId('rail-route')).toHaveCount(0);
}
async function wire(browser:Browser,q:any,secret?:string,duplicate=false){
 const p=await browser.newPage();let finish!:(m:any)=>void;const receipt=new Promise<any>(r=>finish=r);
 try{
  if(secret)expect((await p.request.post('/api/access',{data:{secret}})).ok()).toBe(true);
  await p.routeWebSocket('**/api/session?*',route=>{const server=route.connectToServer();let n=0;
   server.onMessage(message=>{const m=JSON.parse(String(message));route.send(message);
    if(m.type==='join'){const c=JSON.stringify({type:'command',requestId:'slope-receipt',expectedWorldEpoch:q.expectedWorldEpoch,payload:{kind:'editSlope',query:q}});server.send(c);if(duplicate)server.send(c);}
    if(m.type==='rejected'||m.type==='accepted'&&++n===(duplicate?2:1))finish(m);
   });
  });await p.goto('/');await ready(p);
  if(secret)await expect(p.getByRole('button',{name:'Slope',exact:true})).toBeDisabled();
  return await receipt;
 }finally{await p.close();}
}

// Compare opaque source-sheet pixels at their independently projected positions.
async function sourcePixels(p:Page,voxels:any[]){
 await expect(p.locator('canvas')).toHaveAttribute('data-presentation','ready');
 return p.locator('canvas').evaluate(async(el,args)=>{
  const canvas=el as HTMLCanvasElement,r=canvas.getBoundingClientRect(),[cx,cy]=canvas.dataset.camera!.split(',').map(Number),zoom=Number(canvas.dataset.zoom),variant=canvas.dataset.terrainVariant!;
  const actual=canvas.getContext('2d')!,counts:number[]=[];
  for(const v of args.voxels){
   const f=args.catalog.renderRecipes.find((r:any)=>r.id===v.traffic.rail.definition.recipeId).frames[0];
   const image=new Image();image.src=args.catalog.surfaceSets[f.surfaceSet][variant].image.url;await image.decode();
   const sheet=document.createElement('canvas');sheet.width=image.naturalWidth;sheet.height=image.naturalHeight;const ctx=sheet.getContext('2d')!;ctx.drawImage(image,0,0);
   const [sx,sy,w,h]=f.rectangle,data=ctx.getImageData(sx,sy,w,h).data;
   const x=r.width/2+(16*(v.position.x+v.position.y-20)-f.anchor[0]-cx)*zoom;
   const y=r.height/2+(8*(-v.position.x+v.position.y+20)-16*v.position.z-f.anchor[1]-cy)*zoom;
   let count=0;
   for(let b=0;b<h;b++)for(let a=0;a<w;a++){
    const i=(b*w+a)*4;if(data[i+3]!==255)continue;
    const px=Math.floor((x+(a+.5)*zoom)*canvas.width/r.width),py=Math.floor((y+(b+.5)*zoom)*canvas.height/r.height);
    if(px<0||py<0||px>=canvas.width||py>=canvas.height)continue;
    const pixel=actual.getImageData(px,py,1,1).data;
    if(pixel[0]===data[i]&&pixel[1]===data[i+1]&&pixel[2]===data[i+2])count++;
   }counts.push(count);
  }return counts;
 },{catalog,voxels});
}

test('map slope preview/cancel and flat-to-slope-to-elevated network synchronize and reopen exactly',async({page,browser})=>{
 await page.goto('/');await ready(page);const original=await save(page),context=await browser.newContext(),peer=await context.newPage();
 try{
  await empty(page);await expect(page.getByRole('button',{name:'Modify terrain',exact:true})).toBeEnabled();
  for(const [name,key] of [['Inspect','I'],['Track','T'],['Slope','S'],['Modify terrain','G'],['Remove track','R']]){
   const button=page.getByRole('button',{name,exact:true});await expect(button).toHaveAttribute('title',`${name} (${key})`);await expect(button).toHaveAttribute('aria-keyshortcuts',key);
  }
  await page.locator('canvas').focus();await page.keyboard.press('g');await expect(page.getByRole('button',{name:'Modify terrain',exact:true})).toHaveAttribute('aria-pressed','true');await page.keyboard.press('i');
  await peer.goto('/');await ready(peer);const before=await state(page),bytes=await save(page),canvas=page.locator('canvas');await canvas.evaluate(el=>el.dataset.instance='slope-canvas');
  const camera=await canvas.getAttribute('data-camera');await canvas.focus();await page.keyboard.press('s');
  const slopeButton=page.getByRole('button',{name:'Slope',exact:true});await expect(slopeButton).toHaveAttribute('aria-pressed','true');await expect(slopeButton).toHaveAttribute('aria-keyshortcuts','S');
  await page.keyboard.press('s');await expect(slopeButton).toHaveAttribute('aria-pressed','true');await click(page,30,27,1);
  await expect(page.getByTestId('slope-status')).toContainText('route is legal');await expect(canvas).toHaveAttribute('data-slope-ghost-cells','8');await expect(page.getByTestId('slope-cost')).toContainText('40,000,000');
  expect(await save(page)).toEqual(bytes);await page.screenshot({path:out('slope-en-eight-cell-preview.png'),fullPage:true});
  await page.getByRole('button',{name:'Cancel preview',exact:true}).click();expect(await save(page)).toEqual(bytes);await expect(canvas).toHaveAttribute('data-slope-ghost-cells','0');
  await target(page);await confirm(page);await expect(canvas).toHaveAttribute('data-ruleset','mixed-railway-1');await ready(peer);await expect(peer.locator('canvas')).toHaveAttribute('data-ruleset','mixed-railway-1');
  await expect(page.getByRole('button',{name:'Modify terrain',exact:true})).toBeVisible();await expect(page.getByRole('button',{name:'Modify terrain',exact:true})).toBeDisabled();await expect(page.getByRole('button',{name:'Modify terrain',exact:true})).toHaveAttribute('title','Modify terrain (G) · Terrain editing is not available in this world.');
  await canvas.focus();await page.keyboard.press('g');await expect(slopeButton).toHaveAttribute('aria-pressed','true');
  let built=await state(page);expect(built.mixed.voxels).toHaveLength(8);expect(built.mixed.slopes).toHaveLength(1);expect(built.tick).toBe(before.tick);expect(built.terrain).toEqual(before.terrain);expect(await state(peer)).toEqual(built);
  await track(page,[27,27],[30,27],1);await track(page,[33,27],[36,27],2);
  built=await state(page);const captured=await save(page);expect(built.mixed.accounts.transactions).toHaveLength(3);expect(built.mixed.voxels.filter((v:any)=>v.kind==='slopefiller')).toHaveLength(2);
  await expect(canvas).toHaveAttribute('data-instance','slope-canvas');await expect(canvas).toHaveAttribute('data-camera',camera!);
  await page.getByRole('button',{name:'Inspect',exact:true}).click();await expect(canvas).toHaveAttribute('data-slope-ghost-cells','0');await expect(canvas).toHaveAttribute('data-assets','ready');
  await page.screenshot({path:out('slope-en-connected-network.png'),fullPage:true});
  const counts=await sourcePixels(page,built.mixed.voxels.filter((v:any)=>v.traffic?.rail?.pattern>=28));expect(counts.filter(n=>n>8),JSON.stringify(counts)).toHaveLength(2);expect(counts.filter(n=>n===0),JSON.stringify(counts)).toHaveLength(2);
  await replace(page,captured);await expect.poll(()=>save(page)).toEqual(captured);expect(await state(peer)).toEqual(await state(page));
  await page.reload();await ready(page);expect(await save(page)).toEqual(captured);
  await page.getByRole('button',{name:'Slope',exact:true}).click();await page.getByLabel('Track action',{exact:true}).selectOption('remove');
  // The upper paired filler at the lower end has no visible artwork but is a valid map target.
  await click(page,30,27,2);await expect(page.getByTestId('slope-base')).toContainText('30, 27, 1 → 2');await expect(page.getByLabel('Base / selected level',{exact:true})).toHaveValue('2');
  await confirm(page);expect((await state(page)).mixed.slopes).toHaveLength(0);expect((await state(page)).mixed.voxels.filter((v:any)=>v.traffic?.rail)).toHaveLength(6);
  const clock=(await state(page)).tick;await page.getByRole('button',{name:'Run',exact:false}).first().click();await expect.poll(async()=>(await state(page)).paused).toBe(false);
  await page.getByLabel('Track action',{exact:true}).selectOption('lay');await target(page,30,22);await expect(page.getByTestId('slope-status')).toContainText('route is legal');await page.getByRole('button',{name:'Cancel preview',exact:true}).click();await expect.poll(async()=>BigInt((await state(page)).tick)>BigInt(clock)).toBe(true);expect((await state(page)).paused).toBe(false);
 }finally{await replace(page,original);await context.close();}
});

test('all cardinal ramps show exact surface, underground and cut paired art with saved invisible occupancy',async({page})=>{
 test.setTimeout(120000);await page.goto('/');await ready(page);const original=await save(page);
 try{
  await empty(page);const baseline=await save(page);
  for(const ground of [1,3])for(const d of [0,2,4,6]){
   const authored=JSON.parse(baseline.toString());authored.packedTerrain.cells=Buffer.alloc(30*42,0x40|ground).toString('base64');authored.packedTerrain.geometryRevision='1';authored.revision='1';
   await replace(page,Buffer.from(JSON.stringify(authored)));await page.getByRole('button',{name:'Slope',exact:true}).click();await target(page,30,27,1,d);await expect(page.getByTestId('slope-status')).toContainText('route is legal');await confirm(page);
   const s=await state(page),members=s.mixed.voxels;expect(members.filter((v:any)=>v.kind==='slopefiller')).toHaveLength(2);
   for(const v of members.filter((v:any)=>v.kind!=='traffic')){expect(v.drawGroundBelow).toBe(!(v.kind==='slopefiller'&&ground>=v.position.z));expect(v.drawGroundAbove).toBe(!(v.kind!=='slopefiller'&&ground>=v.position.z+1));}
   expect(members.filter((v:any)=>v.traffic?.rail).map((v:any)=>v.traffic.rail.pattern).sort((a:number,b:number)=>a-b)).toEqual([0,1,2,3].map(i=>(ground===1?28:44)+d*2+i));
   if(ground===3)await cut(page,2);
   await page.getByRole('button',{name:'Inspect',exact:true}).click();await expect(page.locator('canvas')).toHaveAttribute('data-assets','ready');
   const counts=await sourcePixels(page,members.filter((v:any)=>v.traffic?.rail));expect(counts.filter(n=>n>8),JSON.stringify(counts)).toHaveLength(2);expect(counts.filter(n=>n===0),JSON.stringify(counts)).toHaveLength(2);
   await page.screenshot({path:out(`slope-${ground===1?'surface':'underground'}-${d}-cut2.png`),fullPage:true});
   const captured=await save(page);await replace(page,captured);expect(await save(page)).toEqual(captured);
   await cut(page,1);await page.screenshot({path:out(`slope-paired-${ground}-${d}-cut1.png`),fullPage:true});
   await page.getByRole('button',{name:'Slope',exact:true}).click();await page.getByLabel('Track action',{exact:true}).selectOption('remove');await target(page,30,27,1,d);await expect(page.getByTestId('slope-status')).toContainText('route is legal');await confirm(page);expect((await state(page)).mixed.voxels).toHaveLength(0);
  }
 }finally{await replace(page,original);}
});

test('elevated slope loss preview, mixed column art and invalid drafts preserve state',async({page})=>{
 await page.goto('/');await ready(page);const original=await save(page);
 try{
  await empty(page);await page.getByRole('button',{name:'Slope',exact:true}).click();await target(page,30,27,3,4);await expect(page.getByTestId('slope-cost')).toContainText('120,000,000');await expect(page.getByTestId('slope-cost')).toContainText('New piers: 8');await confirm(page);
  const bytes=await save(page);await page.getByLabel('Track action',{exact:true}).selectOption('remove');await target(page,30,29,4,4);await expect(page.getByTestId('slope-cost')).toContainText('32,000,000');await expect(page.getByTestId('slope-losses').locator('li')).toHaveCount(8);await page.screenshot({path:out('slope-elevated-pier-losses.png'),fullPage:true});
  await page.getByLabel('Uphill direction',{exact:true}).selectOption('2');await page.getByRole('button',{name:'Preview slope',exact:true}).click();await expect(page.getByTestId('slope-status')).toContainText('must match');expect(await save(page)).toEqual(bytes);
  await target(page,30,29,4,4);await confirm(page);expect((await state(page)).mixed.voxels).toHaveLength(0);
  await page.getByLabel('Track action',{exact:true}).selectOption('lay');await target(page,30,27,5,2);await expect(page.getByTestId('slope-status')).toContainText('outside');await expect(page.locator('canvas')).toHaveAttribute('data-slope-ghost-cells','8');await expect(page.getByRole('button',{name:'Confirm slope change',exact:true})).toBeDisabled();
  // Author two different column grounds before construction: exact source selects art per column.
  await empty(page);const authored=JSON.parse((await save(page)).toString()),cells=Buffer.from(authored.packedTerrain.cells,'base64');
  for(let i=0;i<4;i++){const x=30+i,y=27,h=Math.floor((x-20+y)/2),v=y-(x-20);cells[v*30+h]=0x40|([1,3,1,3][i]);}
  authored.packedTerrain.cells=cells.toString('base64');authored.packedTerrain.geometryRevision='1';authored.revision='1';await replace(page,Buffer.from(JSON.stringify(authored)));
  await page.getByRole('button',{name:'Slope',exact:true}).click();await target(page);await confirm(page);const mixed=await state(page);
  expect(mixed.mixed.voxels.filter((v:any)=>v.traffic?.rail).sort((a:any,b:any)=>a.position.x-b.position.x).map((v:any)=>v.traffic.rail.pattern)).toEqual([32,49,34,51]);
  const built=await save(page);await target(page);await expect(page.getByTestId('slope-status')).toContainText('blocks this route');expect(await save(page)).toEqual(built);await cut(page,1);await page.screenshot({path:out('slope-mixed-art-holes-cut1.png'),fullPage:true});
 }finally{await replace(page,original);}
});

test('late/stale slope drafts, Observer rejection and duplicate receipts retain one booking',async({page,browser})=>{
 await page.goto('/');await ready(page);const original=await save(page),context=await browser.newContext(),peer=await context.newPage();let release=()=>{};const held=new Promise<void>(r=>release=r);
 try{
  await empty(page);await page.getByRole('button',{name:'Slope',exact:true}).click();let delayed=false;
  await page.route('**/api/railway/slope/preview?*',async route=>{const response=await route.fetch();if(!delayed){delayed=true;await held;}await route.fulfill({response});});
  const first=target(page);await expect.poll(()=>delayed).toBe(true);await target(page,30,28);release();await first;await expect(page.getByTestId('slope-base')).toContainText('30, 28, 1');
  await peer.goto('/');await ready(peer);await peer.getByRole('button',{name:'Slope',exact:true}).click();await target(peer,30,32);await confirm(peer);await expect(page.getByTestId('slope-status')).toContainText('world changed');await expect(page.getByRole('button',{name:'Confirm slope change',exact:true})).toBeDisabled();
  await target(page,30,28);await confirm(page);const s=await state(page),q={expectedWorldEpoch:s.worldEpoch,expectedGeometryRevision:s.mixed.geometryRevision,expectedContentVersion:s.contentVersion,position:{x:30,y:25,z:1},climb:2,action:'lay'};
  expect((await wire(browser,q,'observer-test')).code).toBe('notAuthorized');expect((await wire(browser,q,undefined,true)).type).toBe('accepted');expect((await state(page)).mixed.accounts.transactions).toHaveLength(3);
 }finally{release();await replace(page,original);await context.close();}
});

test('Japanese narrow DPR2 slope task supports keyboard, map removal, Escape and exact reopen',async({browser})=>{
 const context=await browser.newContext({viewport:{width:390,height:844},deviceScaleFactor:2,locale:'ja-JP',hasTouch:true}),page=await context.newPage();await page.goto('/');await ready(page,'ja');const original=await save(page);
 try{
  await empty(page,true);await page.locator('canvas').focus();await page.keyboard.press('s');await expect(page.getByRole('button',{name:'勾配線路',exact:true})).toHaveAttribute('aria-pressed','true');await target(page,30,27,1,6,true);await expect(page.getByTestId('slope-status')).toContainText('施工可能');
  await expect(page.getByTestId('slope-cost')).toBeInViewport();expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);await page.screenshot({path:out('slope-ja-narrow-dpr2.png'),fullPage:true});
  await page.keyboard.press('Escape');await expect(page.getByRole('button',{name:'勾配の変更を確定',exact:true})).toBeDisabled();
  await target(page,30,27,1,6,true);await confirm(page,true);const bytes=await save(page);await page.reload();await ready(page,'ja');expect(await save(page)).toEqual(bytes);
  await page.getByRole('button',{name:'勾配線路',exact:true}).click();await page.getByLabel('線路の操作',{exact:true}).selectOption('remove');await click(page,30,27,2);await expect(page.getByTestId('slope-base')).toContainText('30, 27, 1');await confirm(page,true);expect((await state(page)).mixed.voxels).toHaveLength(0);
 }finally{const s=await state(page);await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:original});await context.close();}
});

test('lost slope acknowledgement recovers without replay and blocks another confirmation',async({page})=>{
 let dropped=false,commands=0;await page.routeWebSocket('**/api/session?*',route=>{const server=route.connectToServer();route.onMessage(message=>{const m=JSON.parse(String(message));if(m.type==='command'&&m.payload.kind==='editSlope')commands++;server.send(message);});server.onMessage(message=>{const m=JSON.parse(String(message));if(m.type==='accepted'&&!dropped){dropped=true;server.close();route.close();return;}route.send(message);});});
 await page.goto('/');await ready(page);const original=await save(page);
 try{
  await empty(page);await page.getByRole('button',{name:'Slope',exact:true}).click();await target(page);await page.getByRole('button',{name:'Confirm slope change',exact:true}).click();await expect(page.locator('.command-feedback')).toContainText('unresolved');await ready(page);
  expect(commands).toBe(1);expect((await state(page)).mixed.accounts.transactions).toHaveLength(1);
  if(!await page.getByRole('button',{name:'Preview slope',exact:true}).count())await page.getByRole('button',{name:'Slope',exact:true}).click();await expect(page.getByRole('button',{name:'Confirm slope change',exact:true})).toBeDisabled();await expect(page.getByRole('button',{name:'Preview slope',exact:true})).toBeDisabled();
 }finally{await replace(page,original);}
});
