import { restartWorldServer } from './session.ts';
import { assertExact } from './assertions.ts';
import type { ObservedSession } from './session.ts';
import {readFileSync} from 'node:fs';
import {test,expect,type Page} from './fixtures.ts';
import {ready} from './browser-helpers.ts';
const compatibility='protocolVersion=11&contentVersion=terrain-content-1&contentVersion=mixed-railway-content-1';
const state=async(p:Page)=>(await(await p.request.get('/api/bootstrap?'+compatibility)).json()).snapshot;
const save=async(p:Page)=>(await p.request.get('/api/saves/download')).body();
const root=()=>process.env.FREETRAIN_PASSENGER_FEEDBACK_OUTPUT??'tests/fixtures/passenger-feedback';

for(const locale of ['en','ja'])test(`P7-14 ${locale} real city passenger feedback commits once across receipt retry, peers, reconnect and restart`,async({page,browser,baseURL,worldServer,sessions})=>{
 test.setTimeout(180000);
 if(locale==='ja')await page.setViewportSize({width:430,height:860});
 await page.addInitScript(locale=>localStorage.setItem('freetrain-online.locale',locale),locale);
 const other=locale==='en'?'ja':'en';
 const context=await browser.newContext({baseURL,locale:other==='ja'?'ja-JP':'en-US'}),peer=await context.newPage();
 const observed: ObservedSession[]=[],errors:string[]=[];
 for(const [i,p] of [page,peer].entries()){
  p.on('pageerror',e=>errors.push(String(e)));
  observed[i]=await sessions.observe(p,i===0?'owner':'peer');
 }
 const latest=(i:number)=>observed[i].latest();
 const bells=()=>observed.map(session=>session.count('departureBell'));
 async function step(index:number,id:string){return test.step("Advance one simulation minute",async()=>{
  const s=await state(page);
  const response=await observed[index].command({type:'command',requestId:id,expectedWorldEpoch:s.worldEpoch,payload:{kind:'step'}});
  if(response.type==='accepted')await sessions.checkpoint(observed[0],'accepted command applied');
  return response;
 });}
 async function upload(bytes:Buffer){
  await sessions.replaceSave(observed[0],bytes,'feedback save replacement');
  await ready(page,locale);await ready(peer,other);
  await assertExact(await save(page),bytes,'feedback replacement bytes');
 }

 try{
  await page.goto('/');await ready(page,locale);
  expect((await peer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);
  await peer.goto('/');await ready(peer,other);
  for(const suffix of ['', '.unload']){
   const seed=readFileSync(root()+suffix),expected=readFileSync(root()+suffix+'.after.json');
   const before=JSON.parse(seed.toString()),after=JSON.parse(expected.toString());
   await upload(seed);
   expect(before.mixed.stationGraph.listeners[0].provider.input).toBe('city-variable');
   expect((await step(1,'denied'+suffix)).code).toBe('notAuthorized');await assertExact(await save(page),seed,"await save(page)");
   const baseline=bells(),receipt=await step(0,'feedback'+suffix);expect(receipt.type).toBe('accepted');
   await assertExact(await save(page),expected,"await save(page)");
   const canonical=(await state(page)).mixed;
   await sessions.assertCheckpoint(observed[0],state=>state?.mixed,canonical,'passenger-feedback.spec: independent fixture checkpoint');
   await assertExact(canonical.city.heat,after.mixed.city.heat,"canonical.city.heat");
   const heat=canonical.city.heat;
   // 64x64 storage: station (23,13) => H2,V21; (36,13) => H9,V8.
   const index=suffix?9*(heat.verticalSize+2)+8:2*(heat.verticalSize+2)+21;
   expect(heat.q[index]).toBe(suffix?2.6041665:7.5);
   if(!suffix){expect(canonical.stationGraph.listeners[0].provider.landValue).toBe(10);expect(canonical.fleet.trains[0].passengers).toBe(3);}
   await expect.poll(()=>bells().map((v,i)=>v-baseline[i])).toEqual(suffix?[0,0]:[1,1]);
   const published=bells();
   expect(await step(0,'feedback'+suffix)).toEqual(receipt);await assertExact(await save(page),expected,"await save(page)");expect(bells()).toEqual(published);
   // Reconnecting and reopening the committed state cannot replay its deposit/bell.
   await page.reload();await peer.reload();await ready(page,locale);await ready(peer,other);
   await assertExact(await save(page),expected,"await save(page)");expect(bells()).toEqual(published);
   await upload(expected);expect(bells()).toEqual(published);
   if(!suffix){
    await upload(seed);await restartWorldServer(worldServer);
    for(const [i,p] of [page,peer].entries())expect((await p.request.post('/api/access',{data:{secret:i?'observer-test':''}})).ok()).toBe(true);
    await page.reload();await peer.reload();await ready(page,locale);await ready(peer,other);
    await assertExact(await save(page),seed,"await save(page)");expect((await step(0,'restart-before')).type).toBe('accepted');await assertExact(await save(page),expected,"await save(page)");
   }
   const committed=await save(page);
   await restartWorldServer(worldServer);
   for(const [i,p] of [page,peer].entries())expect((await p.request.post('/api/access',{data:{secret:i?'observer-test':''}})).ok()).toBe(true);
   await page.reload();await peer.reload();await ready(page,locale);await ready(peer,other);
   await assertExact(await save(page),committed,"await save(page)");
   expect((await step(0,'after-restart'+suffix)).type).toBe('accepted');
   expect((await state(page)).mixed.city.heat.q[index]).toBe(suffix?2.6041665:7.5);
  }
  const rejected=readFileSync(root()+'.rejected');await upload(rejected);const baseline=bells();
  const receipt=await step(0,'rejected-feedback');expect(receipt.code).toBe('operationUnavailable');
  await assertExact(await save(page),rejected,"await save(page)");expect(bells()).toEqual(baseline);
  expect(await step(0,'rejected-feedback')).toEqual(receipt);await assertExact(await save(page),rejected,"await save(page)");
  expect(errors).toEqual([]);
 }finally{await context.close();}
});
