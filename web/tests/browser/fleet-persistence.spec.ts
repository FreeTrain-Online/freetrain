import {readFileSync} from 'node:fs';
import {test,expect,type Page,type WebSocketRoute} from './fixtures.ts';
import {ready} from './browser-helpers.ts';
const boot='/api/bootstrap?protocolVersion=7&contentVersion=terrain-content-1&contentVersion=mixed-railway-content-1';
const snapshot=async(page:Page)=>(await(await page.request.get(boot)).json()).snapshot;
// Inventory regression is self-contained; the moving case below consumes a
// gameplay-built save emitted by the focused backend movement test.
function inventory(){
 const save=JSON.parse(readFileSync('../prototype/protocol/saves/fleet-v9.json','utf8'));
 const sold=JSON.parse(readFileSync('../prototype/protocol/fixtures/delta-fleet-sale.json','utf8'));
 save.mixed=sold.mixed;save.revision=sold.revision;
 save.spatial.entities=save.spatial.entities.map((e:any)=>sold.entities.find((changed:any)=>changed.id===e.id)??e);
 save.clock.pending=[];save.clock.absoluteMinute='130089';
 return save;
}
test('fleet Save9 two-peer load, scalar delta, rejected upload and reconnect retain one exact graph',async({page,browser,baseURL})=>{
 const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 const initial=await snapshot(page),save=inventory(),data=Buffer.from(JSON.stringify(save));
 expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+initial.worldEpoch,{data})).ok()).toBe(true);
 const peerContext=await browser.newContext({baseURL});const peer=await peerContext.newPage();peer.on('pageerror',e=>errors.push(String(e)));
 const messages:any[][]=[[],[]];
 for(const [i,p] of [page,peer].entries())await p.routeWebSocket('**/api/session?*',route=>{
  const server=route.connectToServer();server.onMessage(message=>{messages[i].push(JSON.parse(String(message)));route.send(message);});route.onMessage(message=>server.send(message));
 });
 try{
  await page.goto('/');await ready(page);await peer.goto('/');await ready(peer);
  for(const p of [page,peer])await p.locator('canvas').evaluate(el=>el.setAttribute('data-instance','fleet-preserved'));
  const joined=messages.map(rows=>rows.find(m=>m.type==='join').snapshot);
  expect(joined[0]).toEqual(joined[1]);expect(joined[0].mixed.fleet).toEqual(save.mixed.fleet);
  expect(joined[0].cash).toBe('7050000000');expect(joined[0].mixed.accounts.transactions.at(-1).revenue).toBe('50000000');
  const beforeTime=await peer.getByTestId('game-time').textContent();
  await page.getByRole('button',{name:'Step',exact:false}).first().click();
  await expect.poll(async()=>(await snapshot(page)).tick).toBe('130090');
  await expect(peer.getByTestId('game-time')).not.toHaveText(beforeTime!);
  for(const rows of messages)expect(rows.find(m=>m.type==='delta').mixed.fleet).toEqual(save.mixed.fleet);
  const saved=await(await page.request.get('/api/saves/download')).json();expect(saved.saveVersion).toBe(9);expect(saved.mixed).toEqual(save.mixed);
  const bad=structuredClone(saved);bad.mixed.fleet.trains[0].controllerId='999';
  const current=await snapshot(page);
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+current.worldEpoch,{data:Buffer.from(JSON.stringify(bad))})).status()).toBe(400);
  const badRevenue=structuredClone(saved);badRevenue.mixed.accounts.transactions.at(-1).revenue='50000001';
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+current.worldEpoch,{data:Buffer.from(JSON.stringify(badRevenue))})).status()).toBe(400);
  expect(await snapshot(peer)).toEqual(current);
  expect((await page.request.get('/api/bootstrap?protocolVersion=6&contentVersion=mixed-railway-content-1')).status()).toBe(426);
  await peer.reload();await ready(peer);
  expect(messages[1].filter(m=>m.type==='join').at(-1).snapshot.mixed).toEqual(saved.mixed);
  await expect(page.locator('canvas')).toHaveAttribute('data-instance','fleet-preserved');expect(errors).toEqual([]);
 }finally{await peerContext.close();}
});

test('production fleet transport moves one coherent consist for two browser peers and continues after HTTP reopen',async({page,browser,baseURL})=>{
 const input=process.env.FREETRAIN_MOVING_FLEET_OUTPUT;
 test.skip(!input,'Set FREETRAIN_MOVING_FLEET_OUTPUT to the gameplay-built FleetMovementTests save.');
 const save=JSON.parse(readFileSync(input!,'utf8')),initial=await snapshot(page);
 expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+initial.worldEpoch,{data:Buffer.from(JSON.stringify(save))})).ok()).toBe(true);
 const context=await browser.newContext({baseURL}),peer=await context.newPage(),errors:string[]=[],messages:any[][]=[[],[]];
 let commandSocket:WebSocketRoute|undefined;const connections=[0,0];
 for(const [i,p] of [page,peer].entries()){
  p.on('pageerror',error=>errors.push(String(error)));
  await p.routeWebSocket('**/api/session?*',route=>{
   connections[i]++;const server=route.connectToServer();if(i===0)commandSocket=server;
   server.onMessage(message=>{messages[i].push(JSON.parse(String(message)));route.send(message);});
   route.onMessage(message=>server.send(message));
  });
 }
 try{
  await page.goto('/');await peer.goto('/');
  // P5-09 owns moving art. Keep its explicit unsupported-consumer render guard;
  // actual clients still decode the complete state. Exercise the production
  // protocol through their real sockets without claiming deployment UI/art.
  for(const p of [page,peer])await expect(p.getByRole('heading',{name:'Map unavailable',exact:true})).toBeVisible();
  for(const rows of messages)await expect.poll(()=>rows.filter(m=>m.type==='join').length).toBe(1);
  const joined=messages.map(rows=>rows.find(m=>m.type==='join').snapshot);expect(joined[0]).toEqual(joined[1]);
  const first=joined[0].mixed.fleet.trains[0],ids=first.cars.map((c:any)=>c.id);
  expect(first.cars.map((c:any)=>c.state.location)).toEqual([{x:24,y:18,z:2},{x:24,y:19,z:2},{x:24,y:20,z:2}]);
  async function steps(tick:number){
   for(let n=0;n<2;n++){
    const requestId=`move-step-${tick}-${n}`,epoch=(await snapshot(page)).worldEpoch;
    commandSocket!.send(JSON.stringify({type:'command',requestId,expectedWorldEpoch:epoch,payload:{kind:'step'}}));
    await expect.poll(()=>messages[0].find(m=>m.requestId===requestId)?.type).toBe('accepted');
    await expect.poll(async()=>(await snapshot(page)).tick).toBe(String(tick-1+n));
   }
   for(const rows of messages)await expect.poll(()=>rows.filter(m=>m.type==='delta').at(-1)?.tick).toBe(String(tick));
   const a=messages[0].filter(m=>m.type==='delta').at(-1),b=messages[1].filter(m=>m.type==='delta').at(-1);
   expect(a).toEqual(b);const train=a.mixed.fleet.trains[0];expect(train.cars.map((c:any)=>c.id)).toEqual(ids);
   const slots=a.mixed.voxels.filter((v:any)=>v.traffic?.car);expect(slots).toHaveLength(3);
   for(const car of train.cars)expect(slots.find((v:any)=>v.traffic.car.carId===car.id).position).toEqual(car.state.location);
   return a;
  }
  const moved=await steps(102);expect(moved.mixed.fleet.trains[0].moveCount).toBe(1);
  expect(moved.mixed.fleet.trains[0].cars.map((c:any)=>c.state.location)).toEqual([{x:24,y:17,z:2},{x:24,y:18,z:2},{x:24,y:19,z:2}]);
  expect(BigInt(moved.cash)).toBe(BigInt(joined[0].cash)-120000n);
  expect(moved.mixed.accounts.transactions.at(-1)).toMatchObject({operation:'trainMove',expenditure:'120000',absoluteMinute:'102'});
  expect(moved.mixed.queue.pending).toHaveLength(1);expect(moved.mixed.queue.pending[0].dueMinute).toBe('104');
  const saved=await(await page.request.get('/api/saves/download')).json();expect(saved.mixed).toEqual(moved.mixed);
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+moved.worldEpoch,{data:Buffer.from(JSON.stringify(saved))})).ok()).toBe(true);
  for(const rows of messages)await expect.poll(()=>rows.filter(m=>m.type==='snapshot').at(-1)?.worldEpoch).toBe(String(BigInt(moved.worldEpoch)+1n));
  for(const rows of messages)expect(rows.filter(m=>m.type==='snapshot').at(-1).mixed).toEqual(saved.mixed);
  const second=await steps(104);expect(second.mixed.fleet.trains[0].cars[0].state.location).toEqual({x:24,y:16,z:2});
  const curve=await steps(106);expect(curve.mixed.fleet.trains[0].cars[0].state).toMatchObject({location:{x:25,y:15,z:2},heading:1});
  expect(connections).toEqual([1,1]);
  await peer.reload();await expect.poll(()=>messages[1].filter(m=>m.type==='join').length).toBe(2);
  expect(messages[1].filter(m=>m.type==='join').at(-1).snapshot.mixed).toEqual(curve.mixed);
  expect(errors).toEqual([]);
 }finally{await context.close();}
});
