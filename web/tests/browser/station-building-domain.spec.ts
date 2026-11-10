import {readFileSync} from 'node:fs';
import {test,expect,type Page,type WebSocketRoute} from './fixtures.ts';
import {canonicalState as state,ready} from './browser-helpers.ts';
const profile=JSON.parse(readFileSync('src/generated/station-profile.json','utf8'));
const compat='protocolVersion=9&contentVersion=terrain-content-1&contentVersion=mixed-railway-content-1';
const save=async(p:Page)=>(await p.request.get('/api/saves/download')).body();

// Domain API witness: building controls and source art belong to P6-07/08.
test('P6-06 actual construction/removal API publishes exact station identities and timers to EN/JA peers',async({page,browser,baseURL,worldServer})=>{
 test.setTimeout(120000);
 const messages:any[][]=[[],[]],servers:(WebSocketRoute|undefined)[]=[],errors:string[]=[];
 const context=await browser.newContext({baseURL,locale:'ja-JP',viewport:{width:430,height:860}}),peer=await context.newPage();
 for(const [i,p] of [page,peer].entries()){
  p.on('pageerror',e=>errors.push(String(e)));
  await p.routeWebSocket('**/api/session?*',route=>{const server=route.connectToServer();servers[i]=server;
   server.onMessage(m=>{messages[i].push(JSON.parse(String(m)));route.send(m);});route.onMessage(m=>server.send(m));});
 }
 let serial=0;
 async function command(payload:any,index=0,requestId='station-domain-'+(++serial)){
  const s=await state(page);const sent={type:'command',requestId,expectedWorldEpoch:s.worldEpoch,payload};
  const start=messages[index].length;servers[index]!.send(JSON.stringify(sent));
  await expect.poll(()=>messages[index].slice(start).find(m=>m.requestId===requestId)).toBeTruthy();
  return {reply:messages[index].slice(start).find(m=>m.requestId===requestId),sent};
 }
 const preview=async(p:Page,q:any)=>p.request.post('/api/station/building/preview?'+compat,{data:q});
 const latest=(i:number)=>{const m=messages[i].filter(m=>['join','delta','snapshot'].includes(m.type)).at(-1);return m?.snapshot??m;};
 try{
  await page.goto('/');await ready(page);
  const s=await state(page),r=await page.request.post('/api/worlds/empty/preview',{data:{name:'Constructed stations',dimensions:{h:48,v:40,z:8},waterLevel:2,epoch:s.worldEpoch,revision:s.revision}});
  expect(r.ok()).toBe(true);const candidate=await r.json();
  expect((await page.request.post('/api/worlds/empty/commit',{data:{id:candidate.id,requestHash:candidate.requestHash,candidateHash:candidate.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).ok()).toBe(true);
  await ready(page);expect((await peer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);await peer.goto('/');await ready(peer,'ja');
  const type=profile.definitions.find((d:any)=>d.height>1&&d.sizeX*d.sizeY>1);
  expect(type).toBeTruthy();
  const current=await state(page),q={expectedWorldEpoch:current.worldEpoch,expectedGeometryRevision:current.terrain.geometryRevision,expectedMinute:current.tick,
   expectedContentVersion:current.contentVersion,action:'place',position:{x:25,y:25,z:2},definition:type.reference,stationId:'0',expectedStationRevision:'0'};
  const before=await save(page),response=await preview(page,q);expect(response.ok(),await response.text()).toBe(true);const quote=await response.json();
  expect(quote.legal,quote.reason).toBe(true);expect(quote.positions).toHaveLength(type.sizeX*type.sizeY*type.height);expect(await save(page)).toEqual(before);
  expect((await preview(peer,q)).ok()).toBe(true);
  expect((await command({kind:'editStationBuilding',query:q,expectedCost:quote.cost},1)).reply.code).toBe('notAuthorized');expect(await save(page)).toEqual(before);
  expect((await command({kind:'editStationBuilding',query:q,expectedCost:String(BigInt(quote.cost)+1n)})).reply.code).toBe('staleRevision');
  const committed=await command({kind:'editStationBuilding',query:q,expectedCost:quote.cost});expect(committed.reply.type).toBe('accepted');
  const built=await state(page),graph=built.mixed.stationGraph,b=graph.buildings[0];
  expect(b.definition).toEqual(type.reference);expect(b.location).toEqual(q.position);expect(graph.nextStationName).toBe(2);
  expect(built.mixed.voxels).toHaveLength(0);expect(built.entities.find((e:any)=>e.id===b.id).footprint).toHaveLength(quote.positions.length);
  expect(built.mixed.queue.pending).toHaveLength(2);expect(built.mixed.accounts.transactions[0].expenditure).toBe(quote.cost);
  await expect.poll(()=>latest(1)?.mixed?.stationGraph).toEqual(graph);
  const bytes=await save(page);const start=messages[0].length;servers[0]!.send(JSON.stringify(committed.sent));
  await expect.poll(()=>messages[0].slice(start).find(m=>m.requestId===committed.sent.requestId)?.type).toBe('accepted');expect(await save(page)).toEqual(bytes);
  expect((await command({kind:'editStationBuilding',query:q,expectedCost:quote.cost})).reply.code).toBe('staleRevision');
  const occupied={...q,expectedGeometryRevision:built.mixed.geometryRevision,expectedContentVersion:built.contentVersion};expect((await(await preview(page,occupied)).json()).reason).toBe('obstacle');
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+built.worldEpoch,{data:bytes})).ok()).toBe(true);
  await expect.poll(()=>latest(1)?.worldEpoch).toBe(String(BigInt(built.worldEpoch)+1n));expect((await state(page)).mixed.stationGraph).toEqual(graph);
  await worldServer.restart!();
  for(const p of [page,peer])expect((await p.request.post('/api/access',{data:{secret:p===page?'':'observer-test'}})).ok()).toBe(true);
  await page.reload();await peer.reload();await ready(page);await ready(peer,'ja');expect((await state(page)).mixed.stationGraph).toEqual(graph);
  const reopened=await state(page),remove={...q,expectedWorldEpoch:reopened.worldEpoch,expectedGeometryRevision:reopened.mixed.geometryRevision,
   expectedContentVersion:reopened.contentVersion,expectedMinute:reopened.tick,action:'remove',definition:null,position:quote.positions.at(-1),stationId:b.id,expectedStationRevision:b.revision};
  const demolition=await(await preview(page,remove)).json();expect(demolition.legal,demolition.reason).toBe(true);
  expect(demolition.cost).toBe(String(500000*type.sizeX*type.sizeY));
  expect((await command({kind:'editStationBuilding',query:remove,expectedCost:demolition.cost})).reply.type).toBe('accepted');
  const removed=await state(page);expect(removed.mixed.stationGraph.buildings).toHaveLength(0);expect(removed.mixed.queue.pending).toHaveLength(0);
  expect(removed.mixed.accounts.transactions.at(-1).genre).toBe('{2A666F1A-9F40-4F67-98F4-DEAC1E55296D}');
  await expect.poll(()=>latest(1)?.mixed?.stationGraph?.buildings.length).toBe(0);expect(errors).toEqual([]);
 }finally{await context.close();}
});
