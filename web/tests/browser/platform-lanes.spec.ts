import {readFileSync} from 'node:fs';
import {test,expect,type Page,type WebSocketRoute} from './fixtures.ts';
import {canonicalState as state,ready,closePanel,exposeMapPoint} from './browser-helpers.ts';
const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
const save=async(p:Page)=>(await p.request.get('/api/saves/download')).body();
test.use({hasTouch:true});
async function inspect(page:Page,p:any,ja:boolean){
 await closePanel(page);await page.getByRole('button',{name:ja?'確認':'Inspect',exact:true}).click();
 await expect(page.locator('canvas.world-canvas')).toHaveAttribute('data-presentation','ready');
 const frame=catalog.renderRecipes.find((r:any)=>r.id===`system:fat-${p.direction/2%2}`).frames[0],mask=catalog.masks[frame.masks['spring/day']];
 let sample:{x:number;y:number}|undefined;
 for(let y=1;y<mask.length-1&&!sample;y++)for(let x=1;x<mask[y].length-1&&!sample;x++)if(mask.slice(y-1,y+2).every((r:string)=>r.slice(x-1,x+2)==='111'))sample={x:x+.5,y:y+.5};
 const point=await page.locator('canvas.world-canvas').evaluate((el,a)=>{const r=el.getBoundingClientRect(),[cx,cy]=el.dataset.camera!.split(',').map(Number),zoom=Number(el.dataset.zoom),x=a.location.x-19,y=a.location.y;return {x:r.x+r.width/2+(16*(x+y)-a.anchor[0]+a.sample.x-cx)*zoom,y:r.y+r.height/2+(8*(y-x)-16*a.location.z-a.anchor[1]+a.sample.y-cy)*zoom};},{location:p.location,anchor:frame.anchor,sample:sample!});
 const exposed=await exposeMapPoint(page,point);if(ja)await page.touchscreen.tap(exposed.x,exposed.y);else await page.mouse.click(exposed.x,exposed.y);
 await expect(page.getByTestId('platform-identity')).toContainText(p.id);
}
for(const ja of [false,true])test(`P6-13 ${ja?'JA narrow touch':'EN desktop'} lane confirmation all states peer conflict cancellation and exact checkpoint continuation`,async({page,browser,baseURL,worldServer})=>{
 test.setTimeout(180000);if(ja)await page.setViewportSize({width:430,height:860});await page.addInitScript(ja=>localStorage.setItem('freetrain-online.locale',ja?'ja':'en'),ja);
 const label=(en:string,jp:string)=>ja?jp:en,errors:string[]=[],messages:any[]=[];let socket:WebSocketRoute|undefined,serial=0;
 page.on('pageerror',e=>errors.push(String(e)));await page.routeWebSocket('**/api/session?*',route=>{socket=route.connectToServer();socket.onMessage(m=>{messages.push(JSON.parse(String(m)));route.send(m);});route.onMessage(m=>socket!.send(m));});
 const context=await browser.newContext({baseURL}),peer=await context.newPage();
 async function send(payload:any,code='accepted'){const requestId='lane-'+(++serial),s=await state(page);socket!.send(JSON.stringify({type:'command',requestId,expectedWorldEpoch:s.worldEpoch,payload}));await expect.poll(()=>{const m=messages.find(m=>m.requestId===requestId);return m?.code??m?.type;}).toBe(code);}
 const p=async()=>(await state(page)).mixed.stationGraph.platforms[0];
 const right=()=>page.getByRole('button',{name:label('Edit right track','右の線路を編集'),exact:true}),left=()=>page.getByRole('button',{name:label('Edit left track','左の線路を編集'),exact:true}),confirm=()=>page.getByRole('button',{name:label('Confirm track change','線路の変更を確定'),exact:true}),cancel=()=>page.locator('.platform-lanes').getByRole('button',{name:label('Cancel preview','プレビューを取消'),exact:true});
 async function quote(lane:number){const response=page.waitForResponse(r=>r.url().includes('/api/station/wide-platform/preview?'));await (lane?left():right()).click();const result=await (await response).json();await expect(confirm()).toBeEnabled();return result;}
 try{
  await page.goto('/');await ready(page,ja?'ja':'en');let s=await state(page);const r=await page.request.post('/api/worlds/empty/preview',{data:{name:'Lane lifecycle',dimensions:{h:48,v:40,z:8},waterLevel:2,epoch:s.worldEpoch,revision:s.revision}}),c=await r.json();
  expect((await page.request.post('/api/worlds/empty/commit',{data:{id:c.id,requestHash:c.requestHash,candidateHash:c.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).ok()).toBe(true);await ready(page,ja?'ja':'en');
  expect((await peer.request.post('/api/access',{data:{secret:'operator-test'}})).ok()).toBe(true);await peer.goto('/');await ready(peer);
  s=await state(page);await send({kind:'editWidePlatform',query:{expectedWorldEpoch:s.worldEpoch,expectedGeometryRevision:s.terrain.geometryRevision,expectedMinute:s.tick,expectedContentVersion:s.contentVersion,action:'place',position:{x:25,y:25,z:2},direction:2,length:4,secondLane:false,platformId:'0',expectedPlatformRevision:'0'},expectedCost:'2000000'});
  const original=await p();await inspect(page,original,ja);
  const accounts=(await state(page)).mixed.accounts,queue=(await state(page)).mixed.queue;
  for(const [lane,lane0,lane1] of [[1,true,true],[0,false,true],[1,false,false],[0,true,false],[1,true,true]] as const){
   const before=await save(page),q=await quote(lane);expect(q.legal,q.reason).toBe(true);expect(q.cost).toBe('0');expect(q.positions).toHaveLength(4);expect(await save(page)).toEqual(before);
   await cancel().click();await expect(confirm()).toBeDisabled();expect(await save(page)).toEqual(before);await quote(lane);
   if(ja){await confirm().scrollIntoViewIfNeeded();await confirm().tap();}else await confirm().click();
   await expect.poll(async()=>{const g=(await state(peer)).mixed.stationGraph.platforms[0];return [g.lane0,g.lane1];}).toEqual([lane0,lane1]);await expect(confirm()).toBeDisabled();
   expect((await p()).id).toBe(original.id);expect((await p()).yards).toHaveLength((Number(lane0)+Number(lane1))*4);
  }
  expect((await state(page)).mixed.accounts).toEqual(accounts);expect((await state(page)).mixed.queue).toEqual(queue);
  // Local property drafts disable lane edits until reset.
  await page.getByLabel(label('Platform name','ホーム名'),{exact:true}).fill('Unapplied');await expect(left()).toBeDisabled();await page.getByRole('button',{name:label('Reset draft','下書きを戻す'),exact:true}).click();await expect(left()).toBeEnabled();
  // A peer edit invalidates the explicit quote; replay fails at the server too.
  const stale=await quote(1);await inspect(peer,await p(),false);await peer.getByLabel('Platform name',{exact:true}).fill('Peer lane platform');await peer.getByRole('button',{name:'Apply properties',exact:true}).click();await expect(page.getByTestId('confirmed-name')).toHaveText('Peer lane platform');await expect(confirm()).toBeDisabled();
  const bytes=await save(page);await send({kind:'editWidePlatform',query:stale.query,expectedCost:'0'},'staleRevision');expect(await save(page)).toEqual(bytes);
  // Late replies cannot recreate a canceled quote.
  let release!:()=>void,seen=false;const held=new Promise<void>(r=>release=r);await page.route('**/api/station/wide-platform/preview?*',async route=>{const response=await route.fetch();seen=true;await held;await route.fulfill({response});});
  await left().click();await expect.poll(()=>seen).toBe(true);await cancel().click();release();await expect(confirm()).toBeDisabled();await page.unroute('**/api/station/wide-platform/preview?*');
  await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/platform-lanes-${ja?'ja':'en'}.png`});
  const final=await save(page);s=await state(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:final})).ok()).toBe(true);await ready(page,ja?'ja':'en');await ready(peer);expect(await save(page)).toEqual(final);
  await worldServer.restart!();for(const q of [page,peer])expect((await q.request.post('/api/access',{data:{secret:q===page?'':'operator-test'}})).ok()).toBe(true);await page.reload();await peer.reload();await ready(page,ja?'ja':'en');await ready(peer);expect(await save(page)).toEqual(final);
  await inspect(page,await p(),ja);await quote(0);s=await state(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:final})).ok()).toBe(true);await ready(page,ja?'ja':'en');await expect(page.locator('.platform-lanes')).toHaveCount(0);expect(await save(page)).toEqual(final);
  expect((await peer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);await peer.reload();await ready(peer);await inspect(peer,await p(),false);await expect(peer.getByRole('button',{name:'Edit left track',exact:true})).toBeDisabled();expect(errors).toEqual([]);
 }finally{await context.close();}
});
test('P6-13 occupied lane and complete demolition reject after a moving train invalidates the quote, with peer save continuation',async({page,browser,baseURL,worldServer})=>{
 test.setTimeout(120000);const replies:any[]=[];let socket:WebSocketRoute|undefined,serial=0;
 await page.routeWebSocket('**/api/session?*',route=>{socket=route.connectToServer();socket.onMessage(m=>{replies.push(JSON.parse(String(m)));route.send(m);});route.onMessage(m=>socket!.send(m));});
 const context=await browser.newContext({baseURL}),peer=await context.newPage();
 const compat='protocolVersion=8&contentVersion=terrain-content-1&contentVersion=mixed-railway-content-1';
 async function send(payload:any,expected='accepted'){const s=await state(page),requestId='occupancy-'+(++serial);socket!.send(JSON.stringify({type:'command',requestId,expectedWorldEpoch:s.worldEpoch,payload}));await expect.poll(()=>{const m=replies.find(m=>m.requestId===requestId);return m?.code??m?.type;}).toBe(expected);}
 async function query(action:string,lane:number){const s=await state(page),p=s.mixed.stationGraph.platforms[0];return {expectedWorldEpoch:s.worldEpoch,expectedGeometryRevision:s.mixed.geometryRevision,expectedMinute:s.tick,expectedContentVersion:s.contentVersion,action,position:p.location,direction:p.direction,length:p.length,secondLane:lane===1,platformId:p.id,expectedPlatformRevision:p.revision};}
 async function preview(q:any){const r=await page.request.post('/api/station/wide-platform/preview?'+compat,{data:q});expect(r.ok()).toBe(true);return r.json();}
 async function deploy(action:string,reverse=false,lane=0){const s=await state(page);await send({kind:'deployFleet',query:{expectedWorldEpoch:s.worldEpoch,expectedRevision:s.revision,expectedMinute:s.tick,expectedContentVersion:s.contentVersion,trainId:s.mixed.fleet.trains[0].id,action,tail:action==='place'?{x:reverse?28:25,y:lane===0?26:24,z:2}:null,reverse}});}
 try{
  await page.goto('/');await ready(page);let s=await state(page);const r=await page.request.post('/api/worlds/empty/preview',{data:{name:'Occupied lane safety',dimensions:{h:48,v:40,z:8},waterLevel:2,epoch:s.worldEpoch,revision:s.revision}}),c=await r.json();
  expect((await page.request.post('/api/worlds/empty/commit',{data:{id:c.id,requestHash:c.requestHash,candidateHash:c.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).ok()).toBe(true);await ready(page);
  expect((await peer.request.post('/api/access',{data:{secret:'operator-test'}})).ok()).toBe(true);await peer.goto('/');await ready(peer);
  s=await state(page);await send({kind:'editWidePlatform',query:{expectedWorldEpoch:s.worldEpoch,expectedGeometryRevision:s.terrain.geometryRevision,expectedMinute:s.tick,expectedContentVersion:s.contentVersion,action:'place',position:{x:25,y:25,z:2},direction:2,length:4,secondLane:true,platformId:'0',expectedPlatformRevision:'0'},expectedCost:'2000000'});
  const stock=JSON.parse(readFileSync('src/generated/fleet-profile.json','utf8')).trains[0];s=await state(page);
  await send({kind:'tradeFleet',query:{expectedWorldEpoch:s.worldEpoch,expectedRevision:s.revision,expectedMinute:s.tick,expectedContentVersion:s.contentVersion,action:'buy',definition:stock.binding,length:1,quantity:1,trainId:'0'},expectedPrice:String(stock.pricePerCar)});
  for(const reverse of [false,true])for(const lane of [0,1]){
   const quoted=await query('removeLane',lane);expect((await preview(quoted)).legal).toBe(true);await deploy('place',reverse,lane);
   const before=await save(page);await send({kind:'editWidePlatform',query:quoted,expectedCost:'0'},'staleRevision');
   const occupied=await query('removeLane',lane);expect((await preview(occupied)).reason).toBe('occupied');await send({kind:'editWidePlatform',query:occupied,expectedCost:'0'},'invalidPlacement');
   expect((await preview(await query('remove',lane))).reason).toBe('occupied');expect(await save(page)).toEqual(before);
   await inspect(page,(await state(page)).mixed.stationGraph.platforms[0],false);
   await page.getByRole('button',{name:lane?'Edit left track':'Edit right track',exact:true}).click();await expect(page.getByTestId('platform-lane-status')).toHaveText(JSON.parse(readFileSync('src/locales/en/app.json','utf8')).railCar);await expect(page.getByRole('button',{name:'Confirm track change',exact:true})).toBeDisabled();
   const other=await query('removeLane',1-lane);await send({kind:'editWidePlatform',query:other,expectedCost:'0'});
   expect((await state(page)).mixed.fleet.trains[0].cars[0].state.location).toEqual({x:reverse?28:25,y:lane===0?26:24,z:2});
   const reattach=await query('addLane',1-lane);await send({kind:'editWidePlatform',query:reattach,expectedCost:'0'});
   await deploy('withdraw');
  }
  await deploy('place');await closePanel(page);await send({kind:'step'});await send({kind:'step'});
  await expect.poll(async()=>(await state(peer)).mixed.fleet.trains[0].moveCount).toBeGreaterThan(0);
  const final=await save(page);s=await state(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:final})).ok()).toBe(true);await ready(page);await ready(peer);expect(await save(page)).toEqual(final);
  await worldServer.restart!();for(const p of [page,peer])expect((await p.request.post('/api/access',{data:{secret:p===page?'':'operator-test'}})).ok()).toBe(true);await page.reload();await peer.reload();await ready(page);await ready(peer);expect(await save(page)).toEqual(final);
  await send({kind:'step'});await send({kind:'step'});await expect.poll(async()=>(await state(peer)).mixed.fleet).toEqual((await state(page)).mixed.fleet);
 }finally{await context.close();}
});
