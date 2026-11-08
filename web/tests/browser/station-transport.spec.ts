import {readFileSync} from 'node:fs';
import {test,expect,type Page} from './fixtures.ts';
import {ready} from './browser-helpers.ts';
const boot='/api/bootstrap?protocolVersion=8&contentVersion=terrain-content-1&contentVersion=mixed-railway-content-1';
const snapshot=async(p:Page)=>(await(await p.request.get(boot)).json()).snapshot;
test('P6-05 EN/JA peers retain canonical station transport through rejected upload, reopen and checkpoint restart',async({page,browser,baseURL,worldServer})=>{
 // P6-07/09/10 own station/platform presentation. This live-map witness keeps
 // a building, logs and listener edges; full platform transport uses the shared
 // literal fixture in the .NET/client decoder/chunk tests.
 const save=JSON.parse(readFileSync('../prototype/protocol/saves/stations-v10.json').toString()),initial=await snapshot(page);
 const yardIds=new Set(save.mixed.stationGraph.platforms.flatMap((p:any)=>p.yards.map((y:any)=>y.trafficOwnerId)));
 save.spatial.entities=save.spatial.entities.filter((e:any)=>e.id!=='3'&&!yardIds.has(e.id));
 save.mixed.voxels=save.mixed.voxels.filter((v:any)=>!yardIds.has(v.ownerId));
 save.mixed.stationGraph.platforms=[];save.mixed.stationGraph.buildings[0].children=[];
 save.clock.pending=save.clock.pending.filter((t:any)=>!t.handlerId.startsWith('platform/'));save.mixed.queue.pending=save.clock.pending;
 const input=Buffer.from(JSON.stringify(save));
 expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+initial.worldEpoch,{data:input})).ok()).toBe(true);
 const context=await browser.newContext({baseURL,locale:'ja-JP',viewport:{width:430,height:860}}),peer=await context.newPage();
 const messages:any[][]=[[],[]],errors:string[]=[];
 for(const [i,p] of [page,peer].entries()){
  p.on('pageerror',e=>errors.push(String(e)));
  await p.routeWebSocket('**/api/session?*',route=>{const server=route.connectToServer();server.onMessage(message=>{messages[i].push(JSON.parse(String(message)));route.send(message);});route.onMessage(message=>server.send(message));});
 }
 const latest=(i:number)=>messages[i].filter(m=>m.type==='join'||m.type==='snapshot'||m.type==='delta').at(-1);
 try{
  await page.goto('/');await peer.goto('/');await ready(page);await ready(peer,'ja');
  for(const [i,p] of [page,peer].entries()){
   const join=messages[i].find(m=>m.type==='join');expect(join.protocolVersion).toBe(8);expect(join.snapshot.mixed).toEqual(save.mixed);
   await p.locator('canvas').evaluate(el=>el.setAttribute('data-instance','station-transport'));
  }
  await page.getByRole('button',{name:'Step',exact:false}).first().click();
  for(const i of [0,1])await expect.poll(()=>latest(i)?.tick).toBe('518');
  const before=await snapshot(page);expect(before.mixed.stationGraph).toEqual(save.mixed.stationGraph);
  const corrupt=structuredClone(save);corrupt.mixed.stationGraph.buildings[0].children=['999'];
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+before.worldEpoch,{data:Buffer.from(JSON.stringify(corrupt))})).ok()).toBe(false);
  expect((await snapshot(page))).toEqual(before);
  const downloaded=await(await page.request.get('/api/saves/download')).json();expect(downloaded.saveVersion).toBe(10);
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+before.worldEpoch,{data:Buffer.from(JSON.stringify(downloaded))})).ok()).toBe(true);
  for(const i of [0,1])await expect.poll(()=>latest(i)?.worldEpoch).toBe(String(BigInt(before.worldEpoch)+1n));
  for(const p of [page,peer]){expect((await snapshot(p)).mixed).toEqual(downloaded.mixed);await expect(p.locator('canvas')).toHaveAttribute('data-instance','station-transport');}
  await worldServer.restart!();
  for(const p of [page,peer])expect((await p.request.post('/api/access',{data:{secret:''}})).ok()).toBe(true);
  await page.reload();await peer.reload();await ready(page);await ready(peer,'ja');
  for(const p of [page,peer])expect((await snapshot(p)).mixed).toEqual(downloaded.mixed);
  await page.getByRole('button',{name:'Step',exact:false}).first().click();
  for(const p of [page,peer])await expect.poll(async()=>(await snapshot(p)).tick).toBe('519');
  expect(errors).toEqual([]);
 }finally{await context.close();}
});
