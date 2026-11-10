import {readFileSync} from 'node:fs';
import {test,expect,type Page} from './fixtures.ts';
import {ready} from './browser-helpers.ts';

const output=process.env.FREETRAIN_YARD_FLEET_OUTPUT;
const boot='/api/bootstrap?protocolVersion=9&contentVersion=terrain-content-1&contentVersion=mixed-railway-content-1';
const snapshot=async(p:Page)=>(await(await p.request.get(boot)).json()).snapshot;

test('P6-19 constructed yard Simple wait and packed departure publish atomically to EN/JA peers through checkpoint restart',async({page,browser,baseURL,worldServer})=>{
 test.skip(!output,'Generate FREETRAIN_YARD_FLEET_OUTPUT with StationYardStopTests first.');
 test.setTimeout(120000);
 const seed=JSON.parse(readFileSync(output!,'utf8'));
 const arrived=JSON.parse(readFileSync(output!+'.arrived.json','utf8'));
 const departed=JSON.parse(readFileSync(output!+'.departed.json','utf8'));
 const context=await browser.newContext({baseURL,locale:'ja-JP',viewport:{width:430,height:860},isMobile:true,hasTouch:true});
 const peer=await context.newPage(),messages:any[][]=[[],[]],sockets:any[]=[],errors:string[]=[];
 for(const [i,p] of [page,peer].entries()){
  p.on('pageerror',e=>errors.push(String(e)));
  await p.routeWebSocket('**/api/session?*',route=>{
   const server=route.connectToServer();sockets[i]=server;
   server.onMessage(m=>{messages[i].push(JSON.parse(String(m)));route.send(m);});route.onMessage(m=>server.send(m));
  });
 }
 const latest=(i:number)=>{const m=messages[i].filter(m=>['join','snapshot','delta'].includes(m.type)).at(-1);return m?.snapshot??m;};
 let serial=0;
 async function step(){
  const before=await snapshot(page),requestId='yard-step-'+(++serial),start=messages[0].length;
  const command={type:'command',requestId,expectedWorldEpoch:before.worldEpoch,payload:{kind:'step'}};
  sockets[0].send(JSON.stringify(command));
  await expect.poll(()=>messages[0].slice(start).find(m=>m.requestId===requestId)?.type).toBe('accepted');
  const tick=String(BigInt(before.tick)+1n);
  for(const i of [0,1])await expect.poll(()=>latest(i)?.tick).toBe(tick);
  return command;
 }
 try{
  await page.goto('/');await ready(page);
  expect((await peer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);
  await peer.goto('/');await ready(peer,'ja');
  const epoch=(await snapshot(page)).worldEpoch;
  const upload=await page.request.post('/api/saves/upload?expectedWorldEpoch='+epoch,{data:Buffer.from(JSON.stringify(seed))});
  expect(upload.ok(),await upload.text()).toBe(true);
  for(const i of [0,1])await expect.poll(()=>latest(i)?.mixed).toEqual(seed.mixed);
  for(const p of [page,peer])await p.locator('canvas').evaluate(el=>el.setAttribute('data-instance','yard-service'));
  await ready(page);await ready(peer,'ja');
  while(BigInt((await snapshot(page)).tick)<BigInt(arrived.clock.absoluteMinute))await step();
  for(const i of [0,1])expect(latest(i).mixed).toEqual(arrived.mixed);
  for(const i of [0,1])expect(messages[i].filter(m=>m.type==='departureBell')).toEqual([]);
  const waiting=await(await page.request.get('/api/saves/download')).json();
  expect(waiting.mixed.fleet.trains[0].stopCallCount).toBe(1);
  expect(waiting.mixed.stationGraph.buildings[0].trains.today).toBe(1);
  expect(waiting.mixed.accounts).toEqual(seed.mixed.accounts); // positive wait never charges movement
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+(await snapshot(page)).worldEpoch,{data:Buffer.from(JSON.stringify(waiting))})).ok()).toBe(true);
  for(const i of [0,1])await expect.poll(()=>latest(i)?.mixed).toEqual(arrived.mixed);
  await worldServer.restart!();
  for(const [i,p] of [page,peer].entries())expect((await p.request.post('/api/access',{data:{secret:i===0?'':'observer-test'}})).ok()).toBe(true);
  await page.reload();await peer.reload();await ready(page);await ready(peer,'ja');
  for(const p of [page,peer])expect((await snapshot(p)).mixed).toEqual(arrived.mixed);
  for(const p of [page,peer])await p.locator('canvas').evaluate(el=>el.setAttribute('data-instance','yard-service'));
  let command:any;
  while(BigInt((await snapshot(page)).tick)<BigInt(departed.clock.absoluteMinute))command=await step();
  for(const i of [0,1])expect(latest(i).mixed).toEqual(departed.mixed);
  for(const i of [0,1]){const bell=messages[i].filter(m=>m.type==='departureBell');expect(bell).toHaveLength(1);expect(bell[0].bell).toEqual(departed.mixed.stationGraph.platforms[0].bell);}
  const train=departed.mixed.fleet.trains[0],station=departed.mixed.stationGraph.buildings[0];
  expect(train.passengers).toBeGreaterThan(0);expect(train.stopCallCount).toBe(0);expect(train.moveCount).toBe(1);
  expect(station.exports.today).toBe(train.passengers);expect(station.trains.today).toBe(2);
  const saved=await(await page.request.get('/api/saves/download')).json(),start=messages[0].length;
  sockets[0].send(JSON.stringify(command));
  await expect.poll(()=>messages[0].slice(start).find(m=>m.requestId===command.requestId)?.type).toBe('accepted');
  expect(await(await page.request.get('/api/saves/download')).json()).toEqual(saved);
  for(const i of [0,1])expect(messages[i].filter(m=>m.type==='departureBell')).toHaveLength(1);
  for(const p of [page,peer])await expect(p.locator('canvas')).toHaveAttribute('data-instance','yard-service');
  expect(errors).toEqual([]);
 }finally{await context.close();}
});
