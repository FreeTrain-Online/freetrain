import {readFileSync} from 'node:fs';
import {test,expect,type Page} from './fixtures.ts';
import {ready} from './browser-helpers.ts';
const compatibility='protocolVersion=10&contentVersion=terrain-content-1&contentVersion=mixed-railway-content-1';
const snapshot=async(p:Page)=>(await(await p.request.get('/api/bootstrap?'+compatibility)).json()).snapshot;
test('P7-05 EN/JA clients preserve city continuations, enforce cancellation authority, reject invalid saves and restore checkpoints',async({page,browser,baseURL,worldServer})=>{
 test.setTimeout(120000);
 const seed=JSON.parse(readFileSync('tests/fixtures/city-v11.json','utf8'));
 const context=await browser.newContext({baseURL,locale:'ja-JP',viewport:{width:430,height:860},isMobile:true,hasTouch:true}),peer=await context.newPage();
 const messages:any[][]=[[],[]],sockets:any[]=[],errors:string[]=[];
 for(const [i,p] of [page,peer].entries()){
  p.on('pageerror',e=>errors.push(String(e)));
  await p.routeWebSocket('**/api/session?*',route=>{const server=route.connectToServer();sockets[i]=server;
   server.onMessage(m=>{messages[i].push(JSON.parse(String(m)));route.send(m);});route.onMessage(m=>server.send(m));});
 }
 const latest=(i:number)=>{const m=messages[i].filter(m=>['join','snapshot','delta'].includes(m.type)).at(-1);return m?.snapshot??m;};
 let serial=0;
 async function send(payload:any,index=0,requestId='city-'+(++serial)){const s=await snapshot(page),start=messages[index].length;sockets[index].send(JSON.stringify({type:'command',requestId,expectedWorldEpoch:s.worldEpoch,payload}));
  await expect.poll(()=>messages[index].slice(start).find(m=>m.requestId===requestId)).toBeTruthy();return messages[index].slice(start).find(m=>m.requestId===requestId);}
 const query=(s:any)=>({expectedWorldEpoch:s.worldEpoch,expectedRevision:s.revision,expectedMinute:s.tick,expectedContentVersion:s.contentVersion,
  ownerId:'20',expectedOwnerRevision:s.mixed.city.structures.find((o:any)=>o.id==='20').revision});
 try{
  await page.goto('/');await ready(page);
  const initial=await snapshot(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+initial.worldEpoch,{data:Buffer.from(JSON.stringify(seed))})).ok()).toBe(true);
  expect((await peer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);
  await peer.goto('/');await ready(peer,'ja');
  for(const i of [0,1])await expect.poll(()=>latest(i)?.mixed?.city).toEqual(seed.mixed.city);
  const before=await snapshot(page),q=query(before);
  const quote=await peer.request.post('/api/city/preview?'+compatibility,{data:q});expect(quote.ok()).toBe(true);expect((await quote.json()).canCancel).toBe(true);
  expect(await snapshot(page)).toEqual(before);
  expect((await peer.request.post('/api/city/preview?'+compatibility.replace('protocolVersion=10','protocolVersion=9'),{data:q})).status()).toBe(426);
  expect((await send({kind:'cancelCityConstruction',query:q},1)).code).toBe('notAuthorized');
  expect((await send({kind:'cancelCityConstruction',query:{...q,expectedRevision:String(BigInt(q.expectedRevision)+1n)}})).code).toBe('staleRevision');
  const bad=structuredClone(seed);bad.mixed.stationGraph.listeners[0].provider.input='controlled';
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+before.worldEpoch,{data:Buffer.from(JSON.stringify(bad))})).ok()).toBe(false);
  expect(await snapshot(page)).toEqual(before);
  const receipt=await send({kind:'cancelCityConstruction',query:q});expect(receipt.type).toBe('accepted');
  expect(await send({kind:'cancelCityConstruction',query:q},0,receipt.requestId)).toEqual(receipt);
  for(const i of [0,1])await expect.poll(()=>latest(i)?.mixed?.city?.structures.some((o:any)=>o.id==='20')).toBe(false);
  const after=await snapshot(page);expect(after.entities.some((e:any)=>e.id==='20')).toBe(false);expect(after.mixed.queue.pending.some((a:any)=>a.handlerId.startsWith('city/construction/20/'))).toBe(false);
  expect(after.mixed.city.landCache).toEqual(before.mixed.city.landCache);expect(after.mixed.stationGraph).toEqual(before.mixed.stationGraph);
  expect((await send({kind:'cancelCityConstruction',query:q})).code).toBe('staleRevision');
  const saved=await(await page.request.get('/api/saves/download')).json();expect(saved.saveVersion).toBe(11);
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+after.worldEpoch,{data:Buffer.from(JSON.stringify(saved))})).ok()).toBe(true);
  for(const i of [0,1])await expect.poll(()=>latest(i)?.worldEpoch).toBe(String(BigInt(after.worldEpoch)+1n));
  expect((await send({kind:'cancelCityConstruction',query:q})).code).toBe('staleEpoch');
  await worldServer.restart!();
  for(const p of [page,peer])expect((await p.request.post('/api/access',{data:{secret:p===page?'':'observer-test'}})).ok()).toBe(true);
  await page.reload();await peer.reload();await ready(page);await ready(peer,'ja');
  for(const p of [page,peer])expect((await snapshot(p)).mixed).toEqual(saved.mixed);
  // These 19 minutes precede the equal-time heat/development callbacks.
  // Unsupported development rolls back heat and the complete minute.
  expect((await send({kind:'step'})).type).toBe('accepted');
  for(const i of [0,1])await expect.poll(()=>latest(i)?.tick).toBe(String(BigInt(saved.clock.absoluteMinute)+1n));
  for(let i=1;i<19;i++)expect((await send({kind:'step'})).type).toBe('accepted');
  const boundary=await snapshot(page),checkpoint=await(await page.request.get('/api/saves/download')).body();
  expect((await send({kind:'step'})).code).toBe('operationUnavailable');
  expect(await snapshot(page)).toEqual(boundary);expect(await(await page.request.get('/api/saves/download')).body()).toEqual(checkpoint);
  expect(errors).toEqual([]);
 }finally{await context.close();}
});
