import {test,expect,type Page,type Browser} from '@playwright/test';
import {mkdirSync} from 'node:fs';
import {join} from 'node:path';
import {createHash} from 'node:crypto';
import type {StateChunk} from '../../src/generated/contracts.ts';
import {ready,openPanel,closePanel,canonicalState as state} from './ux-helpers.ts';
const compat='protocolVersion=6&contentVersion=terrain-content-1&contentVersion=mixed-railway-content-1';
async function save(page:Page){return (await page.request.get('/api/saves/download')).body();}
async function replace(page:Page,bytes:Buffer){const s=await state(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:bytes})).ok()).toBe(true);}
async function empty(page:Page,locale='en'){
 const s=await state(page);const response=await page.request.post('/api/worlds/empty/preview',{data:{name:'Playable railway',dimensions:{h:30,v:42,z:6},waterLevel:1,epoch:s.worldEpoch,revision:s.revision}});expect(response.ok()).toBe(true);
 const c=await response.json();expect((await page.request.post('/api/worlds/empty/commit',{data:{id:c.id,requestHash:c.requestHash,candidateHash:c.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).ok()).toBe(true);
 await expect(page.locator('canvas')).toHaveAttribute('data-ruleset','terrain-state-1');await ready(page,locale);
}
async function target(page:Page,from=[30,27],to=[34,27],level=1,ja=false){
 for(const [name,value] of [[ja?'始点 X':'Start X',from[0]],[ja?'始点 Y':'Start Y',from[1]],[ja?'終点 X':'End X',to[0]],[ja?'終点 Y':'End Y',to[1]],[ja?'線路の高さ':'Track level',level]] as const)await page.getByLabel(name,{exact:true}).fill(String(value));
 await page.getByRole('button',{name:ja?'経路をプレビュー':'Preview route',exact:true}).click();
 await expect(page.getByTestId('rail-status')).toContainText(ja?'施工可能':'route is legal');
}
async function confirm(page:Page,ja=false){await page.getByRole('button',{name:ja?'線路の変更を確定':'Confirm track change',exact:true}).click();await expect(page.getByTestId('rail-route')).toHaveCount(0);await expect(page.locator('.command-feedback')).toContainText(ja?'サーバーで確定':'Confirmed by server');await expect(page.getByRole('button',{name:ja?'線路の変更を確定':'Confirm track change',exact:true})).toBeDisabled();}
async function click(page:Page,x:number,y:number,z=1){
 const p=await page.locator('canvas').evaluate((el,a)=>{const c=el as HTMLCanvasElement,r=c.getBoundingClientRect(),[cx,cy]=c.dataset.camera!.split(',').map(Number),zoom=Number(c.dataset.zoom);return {x:r.x+r.width/2+(16*(a.x+a.y-20)+16-cx)*zoom,y:r.y+r.height/2+(8*(-a.x+a.y+20)-16*a.z+8-cy)*zoom};},{x,y,z});
 await page.mouse.click(p.x,p.y);
}
const output=(name:string)=>{const d=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'track');mkdirSync(d,{recursive:true});return join(d,name);};

for (const focus of ['map', 'preview button', 'preview form']) {
 test(`Return confirms track changes from the ${focus} without replaying the command`, async ({page}) => {
  let commands=0;
  await page.routeWebSocket('**/api/session?*',route=>{
   const server=route.connectToServer();
   route.onMessage(message=>{if(JSON.parse(String(message)).payload?.kind==='editRailway')commands++;server.send(message);});
  });
  await page.goto('/');await ready(page);const original=await save(page);
  try {
   await empty(page);await page.getByRole('button',{name:'Track',exact:true}).click();
   const canvas=page.locator('canvas'),primary=page.getByRole('button',{name:'Confirm track change',exact:true});
   if(focus==='map') {
    await click(page,30,27);await click(page,34,27);await expect(canvas).toBeFocused();
   } else {
    await target(page);
    if(focus==='preview button')await page.getByRole('button',{name:'Preview route',exact:true}).focus();
    else await expect(page.locator('body')).toBeFocused();
   }
   await expect(primary).toBeEnabled();const before=await save(page);
   await page.keyboard.press('Shift+Enter');
   await page.evaluate(()=>document.activeElement?.dispatchEvent(new KeyboardEvent('keydown',{key:'Enter',repeat:true,bubbles:true,cancelable:true})));
   expect(await save(page)).toEqual(before);expect(commands).toBe(0);
   await page.keyboard.press('Enter');
   await expect(page.getByTestId('rail-route')).toHaveCount(0);
   await expect(page.locator('.command-feedback')).toContainText('Confirmed by server');
   await expect(primary).toBeDisabled();
   expect((await state(page)).mixed.voxels).toHaveLength(5);
   await canvas.focus();await page.keyboard.press('Enter');
   expect(commands).toBe(1);
  } finally {await replace(page,original);}
 });
}

async function wireCommand(browser:Browser,q:any,secret?:string,duplicate=false){
 const page=await browser.newPage();let finish!:(message:any)=>void;
 const receipt=new Promise<any>(resolve=>finish=resolve);
 try{
  if(secret)expect((await page.request.post('/api/access',{data:{secret}})).ok()).toBe(true);
  await page.routeWebSocket('**/api/session?*',route=>{
   const server=route.connectToServer();let n=0;
   const command={type:'command',requestId:'railway-receipt',expectedWorldEpoch:q.expectedWorldEpoch,payload:{kind:'editRailway',query:q}};
   server.onMessage(message=>{
    const m=JSON.parse(String(message));route.send(message);
    if(m.type==='join'){server.send(JSON.stringify(command));if(duplicate)server.send(JSON.stringify(command));}
    if(m.type==='rejected'||m.type==='accepted'&&++n===(duplicate?2:1))finish(m);
   });
  });
  await page.goto('/');await ready(page);
  if(secret){await expect(page.getByRole('button',{name:'Track',exact:true})).toBeDisabled();expect((await page.request.post('/api/railway/preview?'+compat,{data:q})).ok()).toBe(true);}
  return await receipt;
 }finally{await page.close();}
}

test('first track confirmation keeps the live world visible during a delayed railway promotion',async({page})=>{
 let delay=false,release=()=>{};
 await page.routeWebSocket('**/api/session?*',route=>{
  const server=route.connectToServer(),parts:Buffer[]=[],held:(string|Buffer)[]=[];
  let waiting=false;
  release=()=>{delay=false;waiting=false;for(const message of held.splice(0))route.send(message);};
  server.onMessage(message=>{
   if(waiting){held.push(message);return;}
   const m=JSON.parse(String(message));
   if(delay&&(m.type==='stateChunk'||m.type==='snapshot')){
    const chunk=m.type==='stateChunk'?m as StateChunk:{type:'stateChunk',transferId:'a'.repeat(32),worldEpoch:m.worldEpoch,revision:m.revision};
    if(m.type==='stateChunk'){
     parts[m.index]=Buffer.from(m.data,'base64');
     if(parts.filter(Boolean).length!==m.count)return;
    }else parts.push(Buffer.from(JSON.stringify({message:m,packedTerrain:null})));
    // Pad the real payload to force multiple valid chunks even on a tiny map.
    const bytes=Buffer.concat([...parts,Buffer.alloc(48*1024,32)]),size=48*1024;
    const sha256=createHash('sha256').update(bytes).digest('hex');
    for(let offset=0,index=0;offset<bytes.length;offset+=size,index++){
     const next=JSON.stringify({...chunk,index,count:Math.ceil(bytes.length/size),totalBytes:bytes.length,sha256,data:bytes.subarray(offset,offset+size).toString('base64')});
     if(index===0)route.send(next);else held.push(next);
    }
    waiting=true;return;
   }
   route.send(message);
  });
  route.onMessage(message=>server.send(message));
 });
 await page.goto('/');await ready(page);const original=await save(page);
 try{
  await empty(page);
  const canvas=page.locator('canvas'),epoch=await canvas.getAttribute('data-world-epoch'),camera=await canvas.getAttribute('data-camera');
  await page.getByRole('button',{name:'Track',exact:true}).click();await target(page);
  delay=true;await page.getByRole('button',{name:'Confirm track change',exact:true}).click();
  await expect(page.getByTestId('connection')).toHaveText('Connecting…');
  await expect(canvas).toHaveAttribute('data-scene','live');
  await expect(canvas).toHaveAttribute('data-world-epoch',epoch!);
  await expect(page.locator('.app-shell')).not.toHaveClass(/gated/);
  await expect(page.getByRole('button',{name:'Track',exact:true})).toHaveAttribute('aria-pressed','true');
  await expect(page.getByRole('button',{name:'Confirm track change',exact:true})).toBeDisabled();
  await expect(page.locator('.transport button').first()).toBeDisabled();
  release();
  await expect(canvas).toHaveAttribute('data-ruleset','mixed-railway-1');await ready(page);
  await expect(canvas).toHaveAttribute('data-refinement','ready');
  await expect(canvas).toHaveAttribute('data-camera',camera!);
  await expect(canvas).toHaveAttribute('data-world-epoch',epoch!);
  await expect(page.getByRole('button',{name:'Track',exact:true})).toHaveAttribute('aria-pressed','true');
 }finally{release();await replace(page,original);}
});

test('two map points preview/cancel/build, attach, branch and remove in two peers without replacing canvas, camera or shared time',async({page,browser})=>{
 await page.goto('/');await ready(page);const original=await save(page);const context=await browser.newContext(),peer=await context.newPage();
 const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 try{
  await empty(page);await peer.goto('/');await ready(peer);const before=await state(page),bytes=await save(page),canvas=page.locator('canvas');
  await canvas.evaluate(el=>el.dataset.instance='track-canvas');const camera=await canvas.getAttribute('data-camera'),zoom=await canvas.getAttribute('data-zoom');
  let bootstrap=0;page.on('request',r=>{if(r.url().includes('/api/bootstrap'))bootstrap++;});
  await page.getByRole('button',{name:'Track',exact:true}).click();await click(page,30,27);await expect(page.getByLabel('Start X',{exact:true})).toHaveValue('30');
  await click(page,34,27);await expect(page.getByTestId('rail-status')).toContainText('route is legal');await expect(page.getByTestId('rail-cost')).toContainText('30,000,000');
  expect(await save(page)).toEqual(bytes);await page.screenshot({path:output('track-en-preview.png'),fullPage:true});
  await page.getByRole('button',{name:'Cancel preview',exact:true}).click();expect(await save(page)).toEqual(bytes);
  await target(page);await confirm(page);await expect(canvas).toHaveAttribute('data-ruleset','mixed-railway-1');await expect(peer.locator('canvas')).toHaveAttribute('data-ruleset','mixed-railway-1');await ready(peer);
  const built=await state(page);expect(built.mixed.voxels).toHaveLength(5);expect(await state(peer)).toEqual(built);expect(built.tick).toBe(before.tick);expect(built.worldEpoch).toBe(before.worldEpoch);expect(built.terrain).toEqual(before.terrain);
  await expect(canvas).toHaveAttribute('data-instance','track-canvas');await expect(canvas).toHaveAttribute('data-camera',camera!);await expect(canvas).toHaveAttribute('data-zoom',zoom!);expect(bootstrap).toBe(0);
  await target(page,[34,27],[36,27]);await confirm(page);await target(page,[32,27],[34,29]);await confirm(page);
  await expect.poll(async()=>(await state(peer)).mixed.voxels.find((v:any)=>v.position.x===32&&v.position.y===27).traffic.rail.pattern).toBe(23);
  const builtBytes=await save(page);await replace(page,builtBytes);await ready(page);await page.getByRole('button',{name:'Remove track',exact:true}).click();await target(page,[34,27],[36,27]);await confirm(page);
  await expect.poll(async()=>(await state(peer)).mixed.voxels.some((v:any)=>v.position.x===36&&v.position.y===27)).toBe(false);
  expect((await state(page)).tick).toBe(before.tick);expect(errors).toEqual([]);
 }finally{await replace(page,original);await context.close();}
});

test('automatic map picking builds visible surface track above level one and reuses elevated rail height',async({page})=>{
 await page.goto('/');await ready(page);const original=await save(page);
 try{
  await empty(page);
  const authored=JSON.parse((await save(page)).toString());authored.packedTerrain.cells=Buffer.alloc(30*42,0x44).toString('base64');authored.packedTerrain.geometryRevision='1';authored.revision='1';
  // One high-ground start permits a legal elevated extension to lower ground.
  const cells=Buffer.from(authored.packedTerrain.cells,'base64');for(const x of [33,34])cells[(27-(x-20))*30+Math.floor((x-20+27)/2)]=0x42;authored.packedTerrain.cells=cells.toString('base64');
  await replace(page,Buffer.from(JSON.stringify(authored)));await ready(page);
  await page.getByRole('button',{name:'Track',exact:true}).click();await expect(page.getByLabel('Auto-detect start level',{exact:true})).toBeChecked();
  await click(page,30,27,4);await expect(page.getByLabel('Track level',{exact:true})).toHaveValue('4');await expect(page.getByTestId('rail-level-state')).toContainText('Detected level: 4');
  await click(page,32,27,4);await expect(page.getByTestId('rail-status')).toContainText('route is legal');await expect(page.getByTestId('rail-cost')).toContainText('18,000,000');
  await confirm(page);await expect.poll(async()=>(await state(page)).mixed?.voxels.filter((v:any)=>v.traffic?.rail).length).toBe(3);
  expect((await state(page)).mixed.voxels.filter((v:any)=>v.traffic?.rail).every((v:any)=>v.position.z===4)).toBe(true);
  // This is an actual alpha-mask rail inspector pick after the preview is gone.
  await page.getByRole('button',{name:'Inspect',exact:true}).click();await expect.poll(async()=>{await click(page,31,27,4);const label=page.getByTestId('selected-object');return await label.count()?label.textContent():'';}).toContain('Railway ·');
  await expect(page.getByTestId('selected-object')).toContainText('31, 27, 4');await closePanel(page);
  await page.getByRole('button',{name:'Track',exact:true}).click();
  await click(page,32,27,4);await click(page,34,27,4);await expect(page.getByTestId('rail-status')).toContainText('route is legal');
  await expect(page.getByTestId('rail-route')).toContainText('32, 27, 4 → 34, 27, 4');await expect(page.getByTestId('rail-cost')).toContainText('New piers: 4');await confirm(page);
  // Confirmation precedes asynchronous art preparation. Wait for the completed
  // visible scene before asking its geometric picker to target the new rail.
  await page.getByRole('button',{name:'Inspect',exact:true}).click();await expect.poll(async()=>{await click(page,34,27,4);const label=page.getByTestId('selected-object');return await label.count()?label.textContent():'';}).toContain('34, 27, 4');
  await closePanel(page);await page.getByRole('button',{name:'Track',exact:true}).click();
  // The next A sits on elevated rail above ground 2, so its rail level wins.
  await click(page,34,27,4);await expect(page.getByLabel('Track level',{exact:true})).toHaveValue('4');await click(page,36,27,4);await expect(page.getByTestId('rail-status')).toContainText('route is legal');
  await page.screenshot({path:output('track-auto-level.png'),fullPage:true});
 }finally{await replace(page,original);}
});

test('automatic coordinate levels, cut-aware picking and explicit manual override retain one flat route height',async({page})=>{
 await page.goto('/');await ready(page);const original=await save(page);
 try{
  await empty(page);const authored=JSON.parse((await save(page)).toString());authored.packedTerrain.cells=Buffer.alloc(30*42,0x44).toString('base64');authored.packedTerrain.geometryRevision='1';authored.revision='1';
  await replace(page,Buffer.from(JSON.stringify(authored)));await ready(page);await page.getByRole('button',{name:'Track',exact:true}).click();
  for(const [name,value] of [['Start X',30],['Start Y',27]] as const)await page.getByLabel(name,{exact:true}).fill(String(value));
  await page.getByRole('button',{name:'Pick end on map',exact:true}).click();await expect(page.getByLabel('Track level',{exact:true})).toHaveValue('4');await click(page,32,27,4);
  await expect(page.getByTestId('rail-status')).toContainText('route is legal');await expect(page.getByTestId('rail-route')).toContainText('30, 27, 4 → 32, 27, 4');
  // Changing the start coordinates re-detects its level on the next preview.
  for(const [name,value] of [['Start X',30],['Start Y',28],['End X',32],['End Y',28]] as const)await page.getByLabel(name,{exact:true}).fill(String(value));
  await page.getByRole('button',{name:'Preview route',exact:true}).click();await expect(page.getByTestId('rail-status')).toContainText('route is legal');await expect(page.getByLabel('Track level',{exact:true})).toHaveValue('4');
  await page.getByLabel('Track level',{exact:true}).fill('1');await expect(page.getByLabel('Auto-detect start level',{exact:true})).not.toBeChecked();await page.getByRole('button',{name:'Preview route',exact:true}).click();
  await expect(page.getByTestId('rail-route')).toContainText('30, 28, 1 → 32, 28, 1');await expect(page.getByTestId('rail-level-state')).toContainText('Manual level: 1');
  await page.getByLabel('Auto-detect start level',{exact:true}).check();await page.getByRole('button',{name:'Preview route',exact:true}).click();await expect(page.getByLabel('Track level',{exact:true})).toHaveValue('4');
  await openPanel(page,'view');await page.getByLabel('Height cut',{exact:true}).fill('2');await closePanel(page);await expect(page.locator('canvas')).toHaveAttribute('data-terrain-cut','2');
  await click(page,30,27,2);await expect(page.getByLabel('Track level',{exact:true})).toHaveValue('2');await click(page,32,27,2);
  await expect(page.getByTestId('rail-status')).toContainText('route is legal');await expect(page.getByTestId('rail-route')).toContainText('30, 27, 2 → 32, 27, 2');
 }finally{await replace(page,original);}
});

test('late previews, stale correction, Observer rejection and participant receipt deduplication preserve one atomic booking',async({page,browser})=>{
 await page.goto('/');await ready(page);const original=await save(page);const context=await browser.newContext(),peer=await context.newPage();
 let release=()=>{};const held=new Promise<void>(r=>release=r);
 try{
  await empty(page);await page.getByRole('button',{name:'Track',exact:true}).click();let delayed=false;
  await page.route('**/api/railway/preview?*',async route=>{const response=await route.fetch();if(!delayed){delayed=true;await held;}await route.fulfill({response});});
  const first=target(page,[30,27],[34,27]);await expect.poll(()=>delayed).toBe(true);
  await target(page,[30,28],[34,28]);release();await first;await expect(page.getByTestId('rail-route')).toContainText('30, 28, 1 → 34, 28, 1');
  await peer.goto('/');await ready(peer);await peer.getByRole('button',{name:'Track',exact:true}).click();await target(peer,[30,30],[34,30]);await confirm(peer);
  await expect(page.getByTestId('rail-status')).toContainText('world changed');await expect(page.getByRole('button',{name:'Confirm track change',exact:true})).toBeDisabled();
  await target(page,[30,28],[34,28]);await confirm(page);expect((await state(page)).mixed.accounts.transactions).toHaveLength(2);
  const current=await state(page),q={expectedWorldEpoch:current.worldEpoch,expectedGeometryRevision:current.mixed.geometryRevision,expectedContentVersion:current.contentVersion,from:{x:30,y:32,z:1},to:{x:34,y:32,z:1},action:'lay',routing:'closest'};
  expect((await wireCommand(browser,q,'observer-test')).code).toBe('notAuthorized');
  // Independent Owner participant: resend one identical request ID twice.
  expect((await wireCommand(browser,q,undefined,true)).type).toBe('accepted');
  expect((await state(page)).mixed.accounts.transactions).toHaveLength(3);
 }finally{release();await replace(page,original);await context.close();}
});

test('elevated and buried normal routes build, preview exact support losses, save/reopen and remove',async({page})=>{
 await page.goto('/');await ready(page);const original=await save(page);
 try{
  await empty(page);
  // Authored terrain fixture: one clear cut at ground 3, all other ground 1.
  const authored=JSON.parse((await save(page)).toString()),cells=Buffer.from(authored.packedTerrain.cells,'base64');cells[17*30+18]=0x43;
  authored.packedTerrain.cells=cells.toString('base64');authored.packedTerrain.geometryRevision='1';authored.revision='1';await replace(page,Buffer.from(JSON.stringify(authored)));await ready(page);
  await page.getByRole('button',{name:'Track',exact:true}).click();await target(page,[30,27],[32,27],3);await expect(page.getByTestId('rail-cost')).toContainText('42,000,000');await expect(page.getByTestId('rail-cost')).toContainText('New piers: 4');await confirm(page);
  const elevated=await save(page);await replace(page,elevated);await ready(page);await page.getByRole('button',{name:'Remove track',exact:true}).click();await target(page,[30,27],[32,27],3);await expect(page.locator('.rail-losses li')).toHaveCount(4);await page.screenshot({path:output('track-elevated-removal.png'),fullPage:true});await confirm(page);
  await page.getByLabel('Track action',{exact:true}).selectOption('lay');await target(page,[30,29],[32,29],0);await expect(page.getByTestId('rail-cost')).toContainText('36,000,000');await confirm(page);
  const buried=await save(page);await replace(page,buried);await ready(page);await openPanel(page,'view');await page.getByLabel('Height cut',{exact:true}).fill('0');await closePanel(page);await expect(page.locator('canvas')).toHaveAttribute('data-terrain-cut','0');
  await page.getByRole('button',{name:'Remove track',exact:true}).click();await target(page,[30,29],[32,29],0);await confirm(page);expect((await state(page)).mixed.voxels).toHaveLength(0);
 }finally{await replace(page,original);}
});

test('Japanese narrow DPR2 keyboard controls keep the canvas and shared clock, cancel on Escape and survive reopen',async({browser})=>{
 const context=await browser.newContext({viewport:{width:390,height:844},deviceScaleFactor:2,locale:'ja-JP'}),page=await context.newPage();await page.goto('/');await ready(page,'ja');const original=await save(page);
 try{
  await empty(page,'ja');const before=await state(page);await page.getByRole('button',{name:'線路',exact:true}).focus();await page.keyboard.press('Enter');await page.locator('canvas').evaluate(()=>new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r))));
  const camera=await page.locator('canvas').getAttribute('data-camera');await page.getByRole('button',{name:'線路',exact:true}).click();await page.getByRole('button',{name:'線路',exact:true}).click();await expect(page.locator('canvas')).toHaveAttribute('data-camera',camera!);
  await target(page,[30,27],[34,27],1,true);await page.getByLabel('始点の高さを自動検出',{exact:true}).check();await page.getByRole('button',{name:'経路をプレビュー',exact:true}).click();await expect(page.getByTestId('rail-level-state')).toContainText('検出した高さ: 1');await expect(page.getByTestId('rail-cost')).toBeInViewport();
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);await page.screenshot({path:output('track-ja-narrow.png'),fullPage:true});
  await page.keyboard.press('Escape');await expect(page.getByRole('button',{name:'線路の変更を確定',exact:true})).toBeDisabled();
  await target(page,[30,27],[34,27],1,true);await confirm(page,true);expect((await state(page)).tick).toBe(before.tick);
  const bytes=await save(page);await page.reload();await ready(page,'ja');expect(await save(page)).toEqual(bytes);await openPanel(page,'help','ja');await expect(page.locator('.primary-panel')).toContainText('遠回りの探索は行いません');
 }finally{await replace(page,original);await context.close();}
});

test('a lost construction acknowledgement recovers the committed route without replay or another confirmation',async({page})=>{
 let dropped=false,commands=0;
 await page.routeWebSocket('**/api/session?*',route=>{
  const server=route.connectToServer();
  route.onMessage(message=>{const m=JSON.parse(String(message));if(m.type==='command'&&m.payload.kind==='editRailway')commands++;server.send(message);});
  server.onMessage(message=>{const m=JSON.parse(String(message));if(m.type==='accepted'&&!dropped){dropped=true;server.close();route.close();return;}route.send(message);});
 });
 await page.goto('/');await ready(page);const original=await save(page);
 try{
  await empty(page);await page.getByRole('button',{name:'Track',exact:true}).click();await target(page);
  await page.getByRole('button',{name:'Confirm track change',exact:true}).click();await expect(page.locator('.command-feedback')).toContainText('unresolved');await ready(page);
  await expect(page.locator('canvas')).toHaveAttribute('data-ruleset','mixed-railway-1');
  expect((await state(page)).mixed.accounts.transactions).toHaveLength(1);expect(commands).toBe(1);
  // A fresh task still requires recovery from the unresolved outcome.
  if(!await page.getByRole('button',{name:'Preview route',exact:true}).count())await page.getByRole('button',{name:'Track',exact:true}).click();
  await expect(page.getByRole('button',{name:'Confirm track change',exact:true})).toBeDisabled();await expect(page.getByRole('button',{name:'Preview route',exact:true})).toBeDisabled();
 }finally{await replace(page,original);}
});
