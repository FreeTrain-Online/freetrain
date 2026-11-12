import {readFileSync} from 'node:fs';
import {test,expect,type Page} from './fixtures.ts';
import {ready} from './browser-helpers.ts';
const boot='/api/bootstrap?protocolVersion=10&contentVersion=terrain-content-1&contentVersion=mixed-railway-content-1';
const snapshot=async(p:Page)=>(await(await p.request.get(boot)).json()).snapshot;
test('P6-05/P6-16 EN/JA peers retain station providers through rejected upload, reopen and checkpoint restart',async({page,browser,baseURL,worldServer})=>{
 // P6-07/09/10 own station/platform presentation. This live-map witness keeps
 // a building, logs and listener edges; full platform transport uses the shared
 // literal fixture in the .NET/client decoder/chunk tests.
 const save=JSON.parse(readFileSync('../prototype/protocol/saves/stations-v11.json').toString()),initial=await snapshot(page);
 const yardIds=new Set(save.mixed.stationGraph.platforms.flatMap((p:any)=>p.yards.map((y:any)=>y.trafficOwnerId)));
 save.spatial.entities=save.spatial.entities.filter((e:any)=>e.id!=='3'&&!yardIds.has(e.id));
 save.mixed.voxels=save.mixed.voxels.filter((v:any)=>!yardIds.has(v.ownerId));
 save.mixed.stationGraph.platforms=[];save.mixed.stationGraph.buildings[0].children=[];
 save.clock.pending=save.clock.pending.filter((t:any)=>!t.handlerId.startsWith('platform/'));save.mixed.queue.pending=save.clock.pending;
 save.mixed.stationGraph.listeners[0].provider={input:'controlled',landValue:3,xySize:2,zSize:8,special:true,
  population:{kind:'CombinationPopulation',base:0,factor:1,entering:0,tables:[],children:[
   {kind:'ResidentialPopulation',base:125,factor:1,entering:0,tables:[],children:[]},
   {kind:'StadiumPopulation',base:1000,factor:1,entering:500,tables:[],children:[]}]}};
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
   const join=messages[i].find(m=>m.type==='join');expect(join.protocolVersion).toBe(10);expect(join.snapshot.mixed).toEqual(save.mixed);
   await p.locator('canvas').evaluate(el=>el.setAttribute('data-instance','station-transport'));
  }
  await page.getByRole('button',{name:'Step',exact:false}).first().click();
  for(const i of [0,1])await expect.poll(()=>latest(i)?.tick).toBe('518');
  const before=await snapshot(page);expect(before.mixed.stationGraph).toEqual(save.mixed.stationGraph);
  const corrupt=structuredClone(save);corrupt.mixed.stationGraph.buildings[0].children=['999'];
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+before.worldEpoch,{data:Buffer.from(JSON.stringify(corrupt))})).ok()).toBe(false);
  expect((await snapshot(page))).toEqual(before);
  const badProvider=structuredClone(save);badProvider.mixed.stationGraph.listeners[0].provider.population.children=[];
  badProvider.mixed.stationGraph.listeners[0].provider.population.kind='MultiplierPopulation';
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+before.worldEpoch,{data:Buffer.from(JSON.stringify(badProvider))})).ok()).toBe(false);
  expect((await snapshot(page))).toEqual(before);
  const downloaded=await(await page.request.get('/api/saves/download')).json();expect(downloaded.saveVersion).toBe(11);
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

test('P6-17 EN/JA peers publish aligned hour, midnight logs and relative platform upkeep exactly once',async({page,browser,baseURL,worldServer})=>{
 test.setTimeout(120000);
 const literal=JSON.parse(readFileSync('../prototype/protocol/saves/stations-v11.json','utf8'));
 const stationCases=JSON.parse(readFileSync('../doc/parity/station-fixtures.json','utf8')).groups.stations.cases;
 const context=await browser.newContext({baseURL,locale:'ja-JP',viewport:{width:430,height:860},isMobile:true,hasTouch:true}),peer=await context.newPage();
 const messages:any[][]=[[],[]],sockets:any[]=[],errors:string[]=[];
 for(const [i,p] of [page,peer].entries()){
  p.on('pageerror',e=>errors.push(String(e)));
  await p.routeWebSocket('**/api/session?*',route=>{const server=route.connectToServer();sockets[i]=server;
   server.onMessage(m=>{messages[i].push(JSON.parse(String(m)));route.send(m);});route.onMessage(m=>server.send(m));});
 }
 const latest=(i:number)=>{const m=messages[i].filter(m=>['join','snapshot','delta'].includes(m.type)).at(-1);return m?.snapshot??m;};
 let serial=0;
 async function send(payload:any,index=0,requestId='station-statistics-'+(++serial)){
  const current=await snapshot(page),start=messages[index].length;
  const command={type:'command',requestId,expectedWorldEpoch:current.worldEpoch,payload};
  sockets[index].send(JSON.stringify(command));
  await expect.poll(()=>messages[index].slice(start).find(m=>m.requestId===requestId)).toBeTruthy();
  return {reply:messages[index].slice(start).find(m=>m.requestId===requestId),command};
 }
 try{
  await page.goto('/');await ready(page);
  expect((await peer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);
  await peer.goto('/');await ready(peer,'ja');
  // Authored boundary states isolate publication at each source due time. Full
  // uninterrupted hour/week arithmetic is independently checked by .NET tests.
  for(const minute of [539,1439,1956]){
   const seed=structuredClone(literal),b=seed.mixed.stationGraph.buildings[0];
   seed.clock.absoluteMinute=String(minute);
   b.passengers={gone:320,accumulatedLoaded:10000,accumulatedUnloaded:10000};
   b.hourTimer.dueMinute=String(minute===1956?1980:minute+1);
   b.dayTimer.dueMinute=String(minute===1956?2880:1440);
   seed.clock.pending=seed.clock.pending.map((t:any)=>t.handlerId.endsWith('/hour')?b.hourTimer:t.handlerId==='station/1/day'?b.dayTimer:t)
    .sort((a:any,b:any)=>Number(BigInt(a.dueMinute)-BigInt(b.dueMinute))||Number(BigInt(a.order)-BigInt(b.order)));
   seed.mixed.queue={...seed.mixed.queue,pending:seed.clock.pending};
   const epoch=(await snapshot(page)).worldEpoch;
   const uploaded=await page.request.post('/api/saves/upload?expectedWorldEpoch='+epoch,{data:Buffer.from(JSON.stringify(seed))});
   expect(uploaded.ok(),`minute ${minute}: ${await uploaded.text()}`).toBe(true);
   for(const i of [0,1])await expect.poll(()=>latest(i)?.tick).toBe(String(minute));
   await ready(page);await ready(peer,'ja');
   const before=await snapshot(page);
   expect(await snapshot(peer)).toEqual(before);
   const stepped=await send({kind:'step'});expect(stepped.reply.type).toBe('accepted');
   const after=await snapshot(page),g=after.mixed.stationGraph,station=g.buildings[0];
   for(const i of [0,1]){
    await expect.poll(()=>latest(i)?.tick).toBe(String(minute+1));
    await expect.poll(()=>latest(i)?.mixed?.stationGraph).toEqual(g);
    expect(latest(i).mixed.accounts).toEqual(after.mixed.accounts);
    expect(latest(i).mixed.queue).toEqual(after.mixed.queue);
   }
   expect(station.passengers).toEqual(minute===1956?b.passengers:{gone:256,accumulatedLoaded:9996,accumulatedUnloaded:9996});
   const charges=after.mixed.accounts.transactions;
   if(minute===1439){
    for(const key of ['imports','exports','trains']){
     expect(station[key]).toEqual({today:0,yesterday:17,thisWeek:23.125+Math.pow(17,0.2),lastWeekPerDay:29.5});
    }
    expect(charges).toHaveLength(1);expect(charges[0].operation).toBe('station/1/day');
    expect(charges[0].expenditure).toBe(String(stationCases.find((r:any)=>r.id===b.definition.contributionId).expected.operationCost));
   }else{
    expect(station.imports).toEqual(b.imports);expect(station.exports).toEqual(b.exports);expect(station.trains).toEqual(b.trains);
    expect(charges).toHaveLength(minute===1956?2:0);
    if(minute===1956){expect(charges.map((c:any)=>c.operation)).toEqual(['platform/2/day','platform/3/day']);expect(charges.map((c:any)=>c.expenditure)).toEqual(['720000','720000']);}
   }
   expect(new Set(after.mixed.queue.pending.map((t:any)=>t.handlerId)).size).toBe(4);
   const downloaded=await(await page.request.get('/api/saves/download')).json();
   // A duplicate receipt cannot execute the hour/day or charge platforms twice.
   const start=messages[0].length;sockets[0].send(JSON.stringify(stepped.command));
   await expect.poll(()=>messages[0].slice(start).find(m=>m.requestId===stepped.command.requestId)?.type).toBe('accepted');
   expect((await(await page.request.get('/api/saves/download')).json())).toEqual(downloaded);
   expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+after.worldEpoch,{data:Buffer.from(JSON.stringify(downloaded))})).ok()).toBe(true);
   const reopened=await snapshot(page);expect(reopened.mixed).toEqual(after.mixed);
   expect((await send({kind:'step'})).reply.type).toBe('accepted');
   expect((await snapshot(page)).mixed).toEqual(after.mixed);
  }
  const checkpoint=await(await page.request.get('/api/saves/download')).json();
  await worldServer.restart!();
  for(const [i,p] of [page,peer].entries())expect((await p.request.post('/api/access',{data:{secret:i===0?'':'observer-test'}})).ok()).toBe(true);
  await page.reload();await peer.reload();await ready(page);await ready(peer,'ja');
  for(const p of [page,peer])expect((await snapshot(p)).mixed).toEqual(checkpoint.mixed);
  expect((await send({kind:'step'})).reply.type).toBe('accepted');
  expect((await snapshot(page)).mixed).toEqual(checkpoint.mixed);
  expect(errors).toEqual([]);
 }finally{await context.close();}
});
