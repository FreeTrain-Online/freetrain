import { requireMixedState } from './session.ts';
import type { Command } from '../../src/generated/contracts.ts';
import { restartWorldServer } from './session.ts';
import { assertExact } from './assertions.ts';
import type { ObservedSession } from './session.ts';
import { compatibilityURL, protocolVersion } from './transport-helpers.ts';
import {readFileSync} from 'node:fs';
import {test,expect,type Page} from './fixtures.ts';
import {ready} from './browser-helpers.ts';
const boot=compatibilityURL('/api/bootstrap',['terrain-content-1','mixed-railway-content-1']);
const snapshot=async(p:Page)=>(await(await p.request.get(boot)).json()).snapshot;
test('P6-05/P6-16 EN/JA peers retain station providers through rejected upload, reopen and checkpoint restart',async({page,browser,baseURL,worldServer,sessions})=>{
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
 const observed: ObservedSession[]=[],errors:string[]=[];
 for(const [i,p] of [page,peer].entries()){
  p.on('pageerror',e=>errors.push(String(e)));
  observed[i]=await sessions.observe(p,i===0?'owner':'peer');
 }
 const latest=(i:number)=>observed[i].latest();
 try{
  await page.goto('/');await peer.goto('/');await ready(page);await ready(peer,'ja');
  for(const [i,p] of [page,peer].entries()){
   const join=observed[i].messages.find(m=>m.type==='join');if(join?.type!=='join')throw new Error('Missing initial join');expect(join.protocolVersion).toBe(protocolVersion);await sessions.assertCheckpoint(observed[0],state=>state.mixed,save.mixed,'station provider join');
   await p.locator('canvas').evaluate(el=>el.setAttribute('data-instance','station-transport'));
  }
  await page.getByRole('button',{name:'Step',exact:false}).first().click();
  for(const i of [0,1])await expect.poll(()=>latest(i)?.tick).toBe('518');
  const before=await snapshot(page);await assertExact(before.mixed.stationGraph,save.mixed.stationGraph,"before.mixed.stationGraph");
  const corrupt=structuredClone(save);corrupt.mixed.stationGraph.buildings[0].children=['999'];
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+before.worldEpoch,{data:Buffer.from(JSON.stringify(corrupt))})).ok()).toBe(false);
  await assertExact((await snapshot(page)),before,"(await snapshot(page))");
  const badProvider=structuredClone(save);badProvider.mixed.stationGraph.listeners[0].provider.population.children=[];
  badProvider.mixed.stationGraph.listeners[0].provider.population.kind='MultiplierPopulation';
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+before.worldEpoch,{data:Buffer.from(JSON.stringify(badProvider))})).ok()).toBe(false);
  await assertExact((await snapshot(page)),before,"(await snapshot(page))");
  const downloaded=await(await page.request.get('/api/saves/download')).json();expect(downloaded.saveVersion).toBe(11);
  await sessions.replaceSave(observed[0],Buffer.from(JSON.stringify(downloaded)),'station provider save replacement');
  for(const i of [0,1])await expect.poll(()=>latest(i)?.worldEpoch).toBe(String(BigInt(before.worldEpoch)+1n));
  for(const p of [page,peer]){await assertExact((await snapshot(p)).mixed,downloaded.mixed,"(await snapshot(p)).mixed");await expect(p.locator('canvas')).toHaveAttribute('data-instance','station-transport');}
  await sessions.restartCheckpoint(worldServer,[{session:observed[0]},{session:observed[1]}],'station provider checkpoint restart');await ready(peer,'ja');
  for(const p of [page,peer])await assertExact((await snapshot(p)).mixed,downloaded.mixed,"(await snapshot(p)).mixed");
  await page.getByRole('button',{name:'Step',exact:false}).first().click();
  for(const p of [page,peer])await expect.poll(async()=>(await snapshot(p)).tick).toBe('519');
  expect(errors).toEqual([]);
 }finally{await context.close();}
});

test('P6-17 EN/JA peers publish aligned hour, midnight logs and relative platform upkeep exactly once',async({page,browser,baseURL,worldServer,sessions})=>{
 test.setTimeout(120000);
 const literal=JSON.parse(readFileSync('../prototype/protocol/saves/stations-v11.json','utf8'));
 const stationCases=JSON.parse(readFileSync('../doc/parity/station-fixtures.json','utf8')).groups.stations.cases;
 const context=await browser.newContext({baseURL,locale:'ja-JP',viewport:{width:430,height:860},isMobile:true,hasTouch:true}),peer=await context.newPage();
 const observed: ObservedSession[]=[],errors:string[]=[];
 for(const [i,p] of [page,peer].entries()){
  p.on('pageerror',e=>errors.push(String(e)));
  observed[i]=await sessions.observe(p,i===0?'owner':'peer');
 }
 const latest=(i:number)=>observed[i].latest();
 let serial=0;
 async function send(payload:any,index=0,requestId='station-statistics-'+(++serial)){return test.step("Send a correlated domain command",async()=>{
  const current=await snapshot(page);
  const command:Command={type:'command',requestId,expectedWorldEpoch:current.worldEpoch,payload};
  const response=await observed[index].command(command);
  if(response.type==='accepted')await sessions.checkpoint(observed[0],'accepted command applied');
  return {reply:response,command};
 });}
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
   await assertExact(await snapshot(peer),before,"await snapshot(peer)");
   const stepped=await send({kind:'step'});expect(stepped.reply.type).toBe('accepted');
   const after=await snapshot(page),g=after.mixed.stationGraph,station=g.buildings[0];
   for(const i of [0,1]){
    await expect.poll(()=>latest(i)?.tick).toBe(String(minute+1));
    await sessions.assertCheckpoint(observed[0],state=>state?.mixed?.stationGraph,g,'station-transport.spec: independent fixture checkpoint');
    await assertExact(requireMixedState(latest(i)).mixed.accounts,after.mixed.accounts,"requireMixedState(latest(i)).mixed.accounts");
    await assertExact(requireMixedState(latest(i)).mixed.queue,after.mixed.queue,"requireMixedState(latest(i)).mixed.queue");
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
   const response=await observed[0].command(stepped.command);
  if(response.type==='accepted')await sessions.checkpoint(observed[0],'accepted command applied');
   await assertExact((await(await page.request.get('/api/saves/download')).json()),downloaded,"(await(await page.request.get('/api/saves/download')).json())");
   expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+after.worldEpoch,{data:Buffer.from(JSON.stringify(downloaded))})).ok()).toBe(true);
   const reopened=await snapshot(page);await assertExact(reopened.mixed,after.mixed,"reopened.mixed");
   expect((await send({kind:'step'})).reply.type).toBe('accepted');
   await assertExact((await snapshot(page)).mixed,after.mixed,"(await snapshot(page)).mixed");
  }
  const checkpoint=await(await page.request.get('/api/saves/download')).json();
  await restartWorldServer(worldServer);
  for(const [i,p] of [page,peer].entries())expect((await p.request.post('/api/access',{data:{secret:i===0?'':'observer-test'}})).ok()).toBe(true);
  await page.reload();await peer.reload();await ready(page);await ready(peer,'ja');
  for(const p of [page,peer])await assertExact((await snapshot(p)).mixed,checkpoint.mixed,"(await snapshot(p)).mixed");
  expect((await send({kind:'step'})).reply.type).toBe('accepted');
  await assertExact((await snapshot(page)).mixed,checkpoint.mixed,"(await snapshot(page)).mixed");
  expect(errors).toEqual([]);
 }finally{await context.close();}
});
