import {readFileSync} from 'node:fs';
import {test,expect,type Page,type WebSocketRoute} from './fixtures.ts';
import {canonicalState as state,ready,closePanel} from './browser-helpers.ts';
const profile=JSON.parse(readFileSync('src/generated/station-profile.json','utf8'));
const definition=profile.definitions.find((d:any)=>d.sizeX===1&&d.sizeY===1&&d.height===1).reference;
const compat='protocolVersion=8&contentVersion=terrain-content-1&contentVersion=mixed-railway-content-1';
const save=async(p:Page)=>(await p.request.get('/api/saves/download')).body();
test.use({hasTouch:true});

for(const ja of [false,true])test(`P6-11 actual automatic hosts ${ja?'JA narrow touch':'EN desktop'} publish reciprocal graphs and never rehost after removal or rebuild`,async({page,browser,baseURL,worldServer})=>{
 test.setTimeout(120000);if(ja)await page.setViewportSize({width:430,height:860});
 await page.addInitScript(ja=>localStorage.setItem('freetrain-online.locale',ja?'ja':'en'),ja);
 const errors:string[]=[],messages:any[]=[];let socket:WebSocketRoute|undefined,serial=0;
 page.on('pageerror',e=>errors.push(String(e)));
 await page.routeWebSocket('**/api/session?*',route=>{socket=route.connectToServer();socket.onMessage(m=>{messages.push(JSON.parse(String(m)));route.send(m);});route.onMessage(m=>socket!.send(m));});
 const context=await browser.newContext({baseURL}),peer=await context.newPage();peer.on('pageerror',e=>errors.push(String(e)));
 async function command(payload:any){const requestId='host-'+(++serial),s=await state(page);socket!.send(JSON.stringify({type:'command',requestId,expectedWorldEpoch:s.worldEpoch,payload}));
  await expect.poll(()=>messages.find(m=>m.requestId===requestId)?.type).toBe('accepted');}
 async function station(x:number,y:number,remove?:any){
  const s=await state(page),query={expectedWorldEpoch:s.worldEpoch,expectedGeometryRevision:s.mixed?.geometryRevision??s.terrain.geometryRevision,expectedMinute:s.tick,
   expectedContentVersion:s.contentVersion,action:remove?'remove':'place',position:{x,y,z:2},definition:remove?null:definition,
   stationId:remove?.id??'0',expectedStationRevision:remove?.revision??'0'};
  const before=await save(page),r=await page.request.post('/api/station/building/preview?'+compat,{data:query});expect(r.ok()).toBe(true);
  const quote=await r.json();expect(quote.legal,quote.reason).toBe(true);expect(await save(page)).toEqual(before);
  await command({kind:'editStationBuilding',query,expectedCost:quote.cost});
 }
 async function platform(x:number,y:number,z:number,wide=false){
  await closePanel(page);const stations=page.getByRole('button',{name:ja?'駅':'Stations',exact:true});if(await stations.getAttribute('aria-pressed')!=='true')await stations.click();
  await page.getByRole('tab',{name:ja?'ホーム':'Platforms',exact:true}).click();
  await page.getByRole('checkbox',{name:ja?'広いホーム':'Wide platform',exact:true}).setChecked(wide);
  for(const [name,value] of [[ja?'ホーム X':'Platform X',x],[ja?'ホーム Y':'Platform Y',y],[ja?'ホームの高さ':'Platform level',z]] as const)await page.getByLabel(name,{exact:true}).fill(String(value));
  const before=await save(page);await page.locator('.platform-task form').getByRole('button',{name:ja?'配置を確認':'Check placement',exact:true}).click();
  const confirm=page.getByRole('button',{name:wide?(ja?'広いホームの建設を確定':'Confirm wide platform'):(ja?'狭いホームの建設を確定':'Confirm slim platform'),exact:true});
  await expect(confirm).toBeEnabled();expect(await save(page)).toEqual(before);
  if(ja){await confirm.scrollIntoViewIfNeeded();await confirm.tap();}else await confirm.click();
  await expect.poll(async()=>(await state(page)).mixed.stationGraph.platforms.filter((p:any)=>p.location.x===x&&p.location.y===y&&p.location.z===z).length).toBe(1);
  await expect(page.locator('canvas.world-canvas')).toHaveAttribute('data-presentation','ready');
 }
 try{
  await page.goto('/');await ready(page,ja?'ja':'en');
  const s=await state(page),r=await page.request.post('/api/worlds/empty/preview',{data:{name:'Constructed host discovery',dimensions:{h:48,v:40,z:8},waterLevel:2,epoch:s.worldEpoch,revision:s.revision}}),candidate=await r.json();
  expect(r.ok()).toBe(true);expect((await page.request.post('/api/worlds/empty/commit',{data:{id:candidate.id,requestHash:candidate.requestHash,candidateHash:candidate.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).ok()).toBe(true);
  await ready(page,ja?'ja':'en');expect((await peer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);await peer.goto('/');await ready(peer);
  await station(19,25);await station(24,24);const oldest=(await state(page)).mixed.stationGraph.buildings[0].id;
  await platform(25,25,2);await platform(25,28,3,true);await platform(19,25,7);
  let current=await state(page),g=current.mixed.stationGraph;
  expect(g.platforms.map((p:any)=>p.hostId)).toEqual([oldest,oldest,oldest]);expect(g.platforms.map((p:any)=>p.revision)).toEqual(['0','0','0']);
  expect(g.buildings[0].children).toEqual(g.platforms.map((p:any)=>p.id));expect(g.buildings[0].revision).toBe('3');
  expect(current.entities.find((e:any)=>e.id===oldest).revision).toBe('3');
  await expect.poll(async()=>(await state(peer)).mixed.stationGraph).toEqual(g);
  // A real client must accept the host owner revision and continue publishing.
  await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/station-hosts-${ja?'ja':'en'}.png`});
  const built=await save(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+current.worldEpoch,{data:built})).ok()).toBe(true);
  await ready(page,ja?'ja':'en');await ready(peer);expect(await save(page)).toEqual(built);
  await worldServer.restart!();for(const p of [page,peer])expect((await p.request.post('/api/access',{data:{secret:p===page?'':'observer-test'}})).ok()).toBe(true);
  await page.reload();await peer.reload();await ready(page,ja?'ja':'en');await ready(peer);expect(await save(page)).toEqual(built);
  await station(19,25,(await state(page)).mixed.stationGraph.buildings[0]);
  current=await state(page);expect(current.mixed.stationGraph.platforms.every((p:any)=>p.hostId===null&&p.children.length===0)).toBe(true);
  expect(current.mixed.stationGraph.platforms.map((p:any)=>p.revision)).toEqual(['1','1','1']);
  expect(current.mixed.queue.pending).toHaveLength(5); // three platforms + remaining station hour/day
  await expect.poll(async()=>(await state(peer)).mixed.stationGraph).toEqual(current.mixed.stationGraph);
  await station(19,25);current=await state(page);expect(current.mixed.stationGraph.platforms.every((p:any)=>p.hostId===null)).toBe(true);
  expect(BigInt(current.mixed.stationGraph.buildings.at(-1).id)).toBeGreaterThan(BigInt(oldest));
  await platform(25,31,2);g=(await state(page)).mixed.stationGraph;expect(g.platforms.at(-1).hostId).toBe(g.buildings[0].id);
  expect(g.platforms.slice(0,3).every((p:any)=>p.hostId===null)).toBe(true);
  expect(g.buildings.every((b:any)=>b.trains.today===0&&b.passengers.gone===0)).toBe(true);
  expect(messages.filter(m=>m.type==='soundEvent')).toEqual([]);expect(errors).toEqual([]);
  await expect(peer.locator('canvas.world-canvas')).toHaveAttribute('data-presentation','ready');
 }finally{await context.close();}
});
