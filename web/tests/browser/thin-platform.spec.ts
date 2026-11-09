import {readFileSync} from 'node:fs';
import {test,expect,type Page,type WebSocketRoute} from './fixtures.ts';
import {ready,canonicalState as state,closePanel,exposeMapPoint} from './browser-helpers.ts';
test.use({hasTouch:true});
const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
const save=async(p:Page)=>(await p.request.get('/api/saves/download')).body();
async function empty(p:Page){
 const s=await state(p),r=await p.request.post('/api/worlds/empty/preview',{data:{name:'Slim platforms',dimensions:{h:48,v:40,z:8},waterLevel:2,epoch:s.worldEpoch,revision:s.revision}});expect(r.ok()).toBe(true);const c=await r.json();
 expect((await p.request.post('/api/worlds/empty/commit',{data:{id:c.id,requestHash:c.requestHash,candidateHash:c.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).ok()).toBe(true);
}
async function task(p:Page,ja=false){await closePanel(p);const button=p.getByRole('button',{name:ja?'駅':'Stations',exact:true});if(await button.getAttribute('aria-pressed')!=='true')await button.click();await p.getByRole('tab',{name:ja?'ホーム':'Platforms',exact:true}).click();await expect(p.locator('.platform-task')).toBeVisible();}
async function quote(p:Page,ja=false,x=25,y=25,z=2,direction=2,roof=true){
 await p.getByLabel(ja?'ホームの操作':'Platform action',{exact:true}).selectOption('place');
 await p.getByLabel(ja?'狭いホームの種類':'Slim platform style',{exact:true}).selectOption({label:ja?roof?'狭いホーム（屋根付き）':'狭いホーム（屋根なし）':roof?'Slim, roofed':'Slim, unroofed'});
 await p.getByLabel(ja?'ホームの向き':'Platform direction',{exact:true}).selectOption(String(direction));
 for(const [name,value] of [[ja?'ホーム X':'Platform X',x],[ja?'ホーム Y':'Platform Y',y],[ja?'ホームの高さ':'Platform level',z]] as const)await p.getByLabel(name,{exact:true}).fill(String(value));
 const response=p.waitForResponse(r=>r.url().includes('/api/station/platform/preview?'));
 await p.locator('.platform-task form').getByRole('button',{name:ja?'配置を確認':'Check placement',exact:true}).click();return (await response).json();
}
for(const ja of [false,true])test(`slim platforms ${ja?'JA narrow touch':'EN keyboard'} construct both styles ground elevated and exact peer reopen removal`,async({page,browser,baseURL,worldServer})=>{
 test.setTimeout(120000);if(ja)await page.setViewportSize({width:430,height:860});await page.addInitScript(ja=>localStorage.setItem('freetrain-online.locale',ja?'ja':'en'),ja);
 const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 const context=await browser.newContext({baseURL}),peer=await context.newPage();
 try{
  await page.goto('/');await ready(page,ja?'ja':'en');await empty(page);await ready(page,ja?'ja':'en');
  expect((await peer.request.post('/api/access',{data:{secret:'operator-test'}})).ok()).toBe(true);await peer.goto('/');await ready(peer);await task(page,ja);
  const confirm=page.getByRole('button',{name:ja?'狭いホームの建設を確定':'Confirm slim platform',exact:true});
  for(const [x,y,z,direction,roof] of [[25,25,2,ja?0:2,true],[30,30,4,ja?4:6,false]] as const){
   const before=await save(page),q=await quote(page,ja,x,y,z,direction,roof);expect(q.legal,q.reason).toBe(true);expect(q.cost).toBe(z===2?'2000000':'6000000');expect(q.positions).toHaveLength(4);
   expect(await save(page)).toEqual(before);await expect(confirm).toBeEnabled();await page.getByRole('button',{name:ja?'ホームのプレビューを表示':'Show platform preview',exact:true}).click();await expect(page.locator('canvas.world-canvas')).toHaveAttribute('data-station-ghost-ready','true');
   await expect(page.locator('canvas.world-canvas')).toHaveAttribute('data-terrain-cut',String(z));
   await page.evaluate(()=>new Promise<void>(r=>requestAnimationFrame(()=>requestAnimationFrame(()=>r()))));
   await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/platform-${ja?'ja':'en'}-${z}-preview.png`});
   await confirm.click();await expect.poll(async()=>(await state(peer)).mixed?.stationGraph?.platforms.length).toBe(z===2?1:2);
   await expect(page.locator('canvas.world-canvas')).toHaveAttribute('data-presentation','ready');
   const p=(await state(page)).mixed.stationGraph.platforms.at(-1);expect(p.hostId).toBe(null);expect(p.roof).toBe(roof);expect(p.direction).toBe(direction);
   expect(p.yards.map((y:any)=>y.roof)).toEqual(roof?[false,true,true,false]:[false,false,false,false]);
  }
  const before=await save(page),occupied=await quote(page,ja,25,25,2,ja?0:2,true);expect(occupied.legal).toBe(false);expect(occupied.reason).toBe('incompatiblePlatformRail');await expect(confirm).toBeDisabled();expect(await save(page)).toEqual(before);
  let s=await state(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:before})).ok()).toBe(true);await ready(page,ja?'ja':'en');expect(await save(page)).toEqual(before);
  await worldServer.restart!();expect((await page.request.post('/api/access',{data:{secret:''}})).ok()).toBe(true);expect((await peer.request.post('/api/access',{data:{secret:'operator-test'}})).ok()).toBe(true);await page.reload();await peer.reload();await ready(page,ja?'ja':'en');await ready(peer);expect(await save(page)).toEqual(before);
  await task(page,ja);
  // Pick a visible elevated platform sprite: its opaque source pixels must lead
  // to the platform component inspector, independent of the rail below it.
  await page.getByRole('button',{name:ja?'確認':'Inspect',exact:true}).click();
  await expect(page.locator('canvas.world-canvas')).toHaveAttribute('data-presentation','ready');
  await page.evaluate(()=>new Promise<void>(r=>requestAnimationFrame(()=>requestAnimationFrame(()=>r()))));
  const current=(await state(page)).mixed.stationGraph.platforms.at(-1),yard=current.yards[0];
  const frame=catalog.renderRecipes.find((r:any)=>r.id===`system:thin-${current.direction}`).frames[0],mask=catalog.masks[frame.masks['spring/day']];
  let sample:{x:number;y:number}|undefined;
  for(let y=1;y<mask.length-1&&!sample;y++)for(let x=1;x<mask[y].length-1&&!sample;x++)if(mask.slice(y-1,y+2).every((r:string)=>r.slice(x-1,x+2)==='111'))sample={x:x+.5,y:y+.5};
  expect(sample).toBeTruthy();
  const picked=await page.locator('canvas.world-canvas').evaluate((el,a)=>{const r=el.getBoundingClientRect(),[cx,cy]=el.dataset.camera!.split(',').map(Number),zoom=Number(el.dataset.zoom),x=a.location.x-19,y=a.location.y;return {x:r.x+r.width/2+(16*(x+y)-a.anchor[0]+a.sample.x-cx)*zoom,y:r.y+r.height/2+(8*(y-x)-16*a.location.z-a.anchor[1]+a.sample.y-cy)*zoom};},{location:yard.location,anchor:frame.anchor,sample:sample!});
  if(ja)await page.touchscreen.tap(picked.x,picked.y);else await page.mouse.click(picked.x,picked.y);await expect(page.getByTestId('railway-component')).toHaveText(ja?'ホーム':'Platforms');await expect(page.getByTestId('platform-identity')).toContainText(current.id);
  await task(page,ja);
  await page.getByLabel(ja?'ホームの操作':'Platform action',{exact:true}).selectOption('remove');
  for(const [name,value] of [[ja?'ホーム X':'Platform X',30],[ja?'ホーム Y':'Platform Y',30],[ja?'ホームの高さ':'Platform level',4]] as const)await page.getByLabel(name,{exact:true}).fill(String(value));
  await page.locator('.platform-task form').getByRole('button',{name:ja?'配置を確認':'Check placement',exact:true}).click();
  const removal=page.getByRole('button',{name:ja?'撤去を確定':'Confirm removal',exact:true});await expect(removal).toBeEnabled();await expect(page.getByTestId('platform-cost')).toContainText('¥0');
  await page.getByRole('button',{name:ja?'プレビューを取消':'Cancel preview',exact:true}).click();await expect(removal).toBeDisabled();expect(await save(page)).toEqual(before);
  await page.locator('.platform-task form').getByRole('button',{name:ja?'配置を確認':'Check placement',exact:true}).click();await removal.click();
  await expect.poll(async()=>(await state(peer)).mixed.stationGraph.platforms.length).toBe(1);s=await state(page);
  expect(s.mixed.voxels.filter((v:any)=>v.position.z===4&&v.traffic?.rail)).toHaveLength(4);expect(s.mixed.voxels.filter((v:any)=>v.position.z===4&&v.traffic?.platform)).toHaveLength(0);
  expect(s.mixed.queue.pending.filter((t:any)=>t.handlerId.startsWith('platform/'))).toHaveLength(1);expect(errors).toEqual([]);
  await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/platform-${ja?'ja':'en'}-removed.png`});
  // Attach a new roofed platform to the exact restored elevated rail owners,
  // using an actual automatic-height map pick (touch on the Japanese journey).
  const restoredOwners=s.mixed.voxels.filter((v:any)=>v.position.z===4&&v.traffic?.rail).map((v:any)=>v.ownerId).sort();
  const attached=await quote(page,ja,30,30,4,ja?4:6,true);expect(attached.legal,attached.reason).toBe(true);
  await page.getByRole('button',{name:ja?'ホームのプレビューを表示':'Show platform preview',exact:true}).click();
  await page.getByRole('button',{name:ja?'プレビューを取消':'Cancel preview',exact:true}).click();
  await page.getByLabel(ja?'始点の高さを自動検出':'Auto-detect start level',{exact:true}).check();
  await page.getByRole('button',{name:ja?'地図でホームの位置を選択':'Pick platform position on map',exact:true}).click();
  await page.evaluate(()=>new Promise<void>(r=>requestAnimationFrame(()=>requestAnimationFrame(()=>r()))));
  const tile=await page.locator('canvas.world-canvas').evaluate(el=>{const r=el.getBoundingClientRect(),[cx,cy]=el.dataset.camera!.split(',').map(Number),zoom=Number(el.dataset.zoom);return {x:r.x+r.width/2+(16*(30-19+30)+16-cx)*zoom,y:r.y+r.height/2+(8*(30-30+19)-64+8-cy)*zoom};});
  if(ja){const exposed=await exposeMapPoint(page,tile);await page.touchscreen.tap(exposed.x,exposed.y);}else await page.mouse.click(tile.x,tile.y);
  await expect(confirm).toBeEnabled();await confirm.click();await expect.poll(async()=>(await state(peer)).mixed.stationGraph.platforms.length).toBe(2);
  const rebuilt=(await state(page)).mixed.stationGraph.platforms.at(-1);expect(rebuilt.location).toEqual({x:30,y:30,z:4});expect(rebuilt.yards.map((y:any)=>y.trafficOwnerId).sort()).toEqual(restoredOwners);
  expect(errors).toEqual([]);

 }finally{await context.close();}
});
test('platform authority stale quotes late cancel and replacement never publish old confirmations',async({page,browser,baseURL})=>{
 test.setTimeout(90000);const context=await browser.newContext({baseURL}),peer=await context.newPage(),observerContext=await browser.newContext({baseURL}),observer=await observerContext.newPage();
 let socket:WebSocketRoute|undefined;const replies:any[]=[];await observer.routeWebSocket('**/api/session?*',r=>{socket=r.connectToServer();socket.onMessage(m=>{replies.push(JSON.parse(String(m)));r.send(m);});r.onMessage(m=>socket!.send(m));});
 try{
  await page.goto('/');await ready(page);await empty(page);await ready(page);
  expect((await peer.request.post('/api/access',{data:{secret:'operator-test'}})).ok()).toBe(true);await peer.goto('/');await ready(peer);await task(peer);
  expect((await observer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);await observer.goto('/');await ready(observer);await expect(observer.getByRole('button',{name:'Stations',exact:true})).toBeDisabled();
  const q=await quote(peer);const bytes=await save(page);socket!.send(JSON.stringify({type:'command',requestId:'observer-platform',expectedWorldEpoch:q.query.expectedWorldEpoch,payload:{kind:'editThinPlatform',query:q.query,expectedCost:q.cost}}));await expect.poll(()=>replies.find(m=>m.requestId==='observer-platform')?.code).toBe('notAuthorized');expect(await save(page)).toEqual(bytes);
  await page.getByRole('button',{name:'Single step',exact:false}).click();const confirm=peer.getByRole('button',{name:'Confirm slim platform',exact:true});await expect(confirm).toBeDisabled();
  let release!:()=>void,seen=false;const held=new Promise<void>(r=>release=r);
  await peer.route('**/api/station/platform/preview?*',async r=>{const response=await r.fetch();seen=true;await held;await r.fulfill({response});});
  await peer.locator('.platform-task form').getByRole('button',{name:'Check placement',exact:true}).click();await expect.poll(()=>seen).toBe(true);
  await peer.getByRole('button',{name:'Cancel preview',exact:true}).click();release();await expect(confirm).toBeDisabled();
  await peer.unroute('**/api/station/platform/preview?*');await quote(peer);await expect(confirm).toBeEnabled();
  const before=await save(page),s=await state(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:before})).ok()).toBe(true);await ready(peer);await expect(peer.locator('.platform-task')).toHaveCount(0);expect(await save(page)).toEqual(before);
 }finally{await context.close();await observerContext.close();}
});
test('underground slim platform uses the explicit level, source underground quote and cut preview through exact reopen',async({page})=>{
 await page.goto('/');await ready(page);await empty(page);await ready(page);await task(page);
 const before=await save(page),q=await quote(page,false,25,25,1,2,true);expect(q.legal,q.reason).toBe(true);expect(q.cost).toBe('4000000');expect(await save(page)).toEqual(before);
 await page.getByRole('button',{name:'Show platform preview',exact:true}).click();await expect(page.locator('canvas.world-canvas')).toHaveAttribute('data-terrain-cut','1');await expect(page.locator('canvas.world-canvas')).toHaveAttribute('data-station-ghost-ready','true');
 await page.evaluate(()=>new Promise<void>(r=>requestAnimationFrame(()=>requestAnimationFrame(()=>r()))));
 await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/platform-underground-preview.png`});
 await page.getByRole('button',{name:'Confirm slim platform',exact:true}).click();await expect.poll(async()=>(await state(page)).mixed?.stationGraph?.platforms.length).toBe(1);
 const s=await state(page);expect(s.mixed.stationGraph.platforms[0].location.z).toBe(1);expect(s.mixed.voxels.every((v:any)=>v.position.z===1)).toBe(true);
 const bytes=await save(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:bytes})).ok()).toBe(true);await ready(page);expect(await save(page)).toEqual(bytes);
});
