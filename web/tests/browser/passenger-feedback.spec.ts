import {readFileSync} from 'node:fs';
import {test,expect,type Page} from './fixtures.ts';
import {ready,latestCanonicalPublication} from './browser-helpers.ts';
const compatibility='protocolVersion=11&contentVersion=terrain-content-1&contentVersion=mixed-railway-content-1';
const state=async(p:Page)=>(await(await p.request.get('/api/bootstrap?'+compatibility)).json()).snapshot;
const save=async(p:Page)=>(await p.request.get('/api/saves/download')).body();
const root=()=>process.env.FREETRAIN_PASSENGER_FEEDBACK_OUTPUT??'tests/fixtures/passenger-feedback';

for(const locale of ['en','ja'])test(`P7-14 ${locale} real city passenger feedback commits once across receipt retry, peers, reconnect and restart`,async({page,browser,baseURL,worldServer})=>{
 test.setTimeout(180000);
 if(locale==='ja')await page.setViewportSize({width:430,height:860});
 await page.addInitScript(locale=>localStorage.setItem('freetrain-online.locale',locale),locale);
 const other=locale==='en'?'ja':'en';
 const context=await browser.newContext({baseURL,locale:other==='ja'?'ja-JP':'en-US'}),peer=await context.newPage();
 const messages:any[][]=[[],[]],sockets:any[]=[],errors:string[]=[];
 for(const [i,p] of [page,peer].entries()){
  p.on('pageerror',e=>errors.push(String(e)));
  await p.routeWebSocket('**/api/session?*',route=>{const server=route.connectToServer();sockets[i]=server;
   server.onMessage(m=>{messages[i].push(JSON.parse(String(m)));route.send(m);});route.onMessage(m=>server.send(m));});
 }
 const latest=(i:number)=>latestCanonicalPublication(messages[i]);
 const bells=()=>messages.map(ms=>ms.filter(m=>m.type==='departureBell').length);
 async function step(index:number,id:string){
  const s=await state(page),start=messages[index].length;
  sockets[index].send(JSON.stringify({type:'command',requestId:id,expectedWorldEpoch:s.worldEpoch,payload:{kind:'step'}}));
  await expect.poll(()=>messages[index].slice(start).find(m=>m.requestId===id)).toBeTruthy();
  return messages[index].slice(start).find(m=>m.requestId===id);
 }
 async function upload(bytes:Buffer){
  const s=await state(page),r=await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:bytes});
  expect(r.ok(),await r.text()).toBe(true);await ready(page,locale);await ready(peer,other);
  for(const i of [0,1])await expect.poll(()=>latest(i)?.worldEpoch).toBe(String(BigInt(s.worldEpoch)+1n));
  // The first terrain -> mixed upload changes the negotiated socket identity.
  // Wait for its new join before injecting commands on the connected socket.
  for(const i of [0,1])await expect.poll(()=>messages[i].filter(m=>m.type==='join').at(-1)?.contentVersion).toBe('mixed-railway-content-1');
  expect(await save(page)).toEqual(bytes);
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
   expect((await step(1,'denied'+suffix)).code).toBe('notAuthorized');expect(await save(page)).toEqual(seed);
   const baseline=bells(),receipt=await step(0,'feedback'+suffix);expect(receipt.type).toBe('accepted');
   expect(await save(page)).toEqual(expected);
   const canonical=(await state(page)).mixed;
   for(const i of [0,1])await expect.poll(()=>latest(i)?.mixed).toEqual(canonical);
   expect(canonical.city.heat).toEqual(after.mixed.city.heat);
   const heat=canonical.city.heat;
   // 64x64 storage: station (23,13) => H2,V21; (36,13) => H9,V8.
   const index=suffix?9*(heat.verticalSize+2)+8:2*(heat.verticalSize+2)+21;
   expect(heat.q[index]).toBe(suffix?2.6041665:7.5);
   if(!suffix){expect(canonical.stationGraph.listeners[0].provider.landValue).toBe(10);expect(canonical.fleet.trains[0].passengers).toBe(3);}
   await expect.poll(()=>bells().map((v,i)=>v-baseline[i])).toEqual(suffix?[0,0]:[1,1]);
   const published=bells();
   expect(await step(0,'feedback'+suffix)).toEqual(receipt);expect(await save(page)).toEqual(expected);expect(bells()).toEqual(published);
   // Reconnecting and reopening the committed state cannot replay its deposit/bell.
   await page.reload();await peer.reload();await ready(page,locale);await ready(peer,other);
   expect(await save(page)).toEqual(expected);expect(bells()).toEqual(published);
   await upload(expected);expect(bells()).toEqual(published);
   if(!suffix){
    await upload(seed);await worldServer.restart!();
    for(const [i,p] of [page,peer].entries())expect((await p.request.post('/api/access',{data:{secret:i?'observer-test':''}})).ok()).toBe(true);
    await page.reload();await peer.reload();await ready(page,locale);await ready(peer,other);
    expect(await save(page)).toEqual(seed);expect((await step(0,'restart-before')).type).toBe('accepted');expect(await save(page)).toEqual(expected);
   }
   const committed=await save(page);
   await worldServer.restart!();
   for(const [i,p] of [page,peer].entries())expect((await p.request.post('/api/access',{data:{secret:i?'observer-test':''}})).ok()).toBe(true);
   await page.reload();await peer.reload();await ready(page,locale);await ready(peer,other);
   expect(await save(page)).toEqual(committed);
   expect((await step(0,'after-restart'+suffix)).type).toBe('accepted');
   expect((await state(page)).mixed.city.heat.q[index]).toBe(suffix?2.6041665:7.5);
  }
  const rejected=readFileSync(root()+'.rejected');await upload(rejected);const baseline=bells();
  const receipt=await step(0,'rejected-feedback');expect(receipt.code).toBe('operationUnavailable');
  expect(await save(page)).toEqual(rejected);expect(bells()).toEqual(baseline);
  expect(await step(0,'rejected-feedback')).toEqual(receipt);expect(await save(page)).toEqual(rejected);
  expect(errors).toEqual([]);
 }finally{await context.close();}
});
