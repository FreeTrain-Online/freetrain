import {readFileSync} from 'node:fs';
import {test,expect,type Page,type WebSocketRoute} from './fixtures.ts';
import {ready,closePanel} from './browser-helpers.ts';
const boot='/api/bootstrap?protocolVersion=7&contentVersion=terrain-content-1&contentVersion=mixed-railway-content-1';
const snapshot=async(page:Page)=>(await(await page.request.get(boot)).json()).snapshot;

test('P5-13 limited movement timing and 39-passenger costs agree for EN/JA peers through reopen',async({page,browser,baseURL})=>{
 const input=process.env.FREETRAIN_TIMING_FLEET_OUTPUT;
 test.skip(!input,'Set FREETRAIN_TIMING_FLEET_OUTPUT to the gameplay-built FleetTimingTests save.');
 const save=JSON.parse(readFileSync(input!,'utf8')),initial=await snapshot(page);
 expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+initial.worldEpoch,{data:Buffer.from(JSON.stringify(save))})).ok()).toBe(true);
 const context=await browser.newContext({baseURL,locale:'ja-JP'}),peer=await context.newPage();
 const messages:any[][]=[[],[]],errors:string[]=[];
 for(const [i,p] of [page,peer].entries()){
  p.on('pageerror',error=>errors.push(String(error)));
  await p.routeWebSocket('**/api/session?*',route=>{
   const server=route.connectToServer();server.onMessage(message=>{messages[i].push(JSON.parse(String(message)));route.send(message);});
   route.onMessage(message=>server.send(message));
  });
 }
 const latest=(i:number)=>messages[i].filter(m=>m.type==='snapshot'||m.type==='delta').at(-1);
 try{
  await page.goto('/');await peer.goto('/');await ready(page);await ready(peer,'ja');
  for(const p of [page,peer])await p.locator('canvas').evaluate(el=>el.setAttribute('data-instance','timing-preserved'));
  const ids=save.mixed.fleet.trains[0].cars.map((c:any)=>c.id),times=[102,106,109,111,113,115,117,119,121,123,125,127,129,131,133,135,137];
  let now=100;const charges:number[]=[];
  for(const [index,due] of times.entries()){
   while(now<due){
    await page.getByRole('button',{name:'Step',exact:false}).first().click();now++;
    for(const i of [0,1])await expect.poll(()=>latest(i)?.tick).toBe(String(now));
   }
   const current=await snapshot(page),fleet=current.mixed.fleet,move=index+1;
   expect(fleet.trains[0].moveCount).toBe(move);expect(fleet.trains[0].passengers).toBe(39);
   expect(fleet.trains[0].cars.map((c:any)=>c.id)).toEqual(ids);
   if(move%4===1)charges.push(due);
   expect(current.cash).toBe(String(10000000-charges.length*122000));
   expect(current.mixed.accounts.transactions.map((a:any)=>a.absoluteMinute)).toEqual(charges.map(String));
   for(const a of current.mixed.accounts.transactions)expect(a).toMatchObject({operation:'trainMove',expenditure:'122000',genre:'{AC30BB0B-044C-4784-A83D-FCB1F60B3CF2}'});
   expect(current.mixed.queue.pending).toHaveLength(1);
   expect(current.mixed.queue.pending[0].dueMinute).toBe(String(times[index+1]??139));
   expect(latest(0).mixed).toEqual(current.mixed);expect(latest(1).mixed).toEqual(current.mixed);
   for(const c of fleet.trains[0].cars)expect(current.mixed.voxels.find((v:any)=>JSON.stringify(v.position)===JSON.stringify(c.state.location)).traffic.car.carId).toBe(c.id);
   if(move===4){
    const saved=await(await page.request.get('/api/saves/download')).json();
    expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+current.worldEpoch,{data:Buffer.from(JSON.stringify(saved))})).ok()).toBe(true);
    for(const i of [0,1])await expect.poll(()=>latest(i)?.worldEpoch).toBe(String(BigInt(current.worldEpoch)+1n));
    expect(latest(1).mixed).toEqual(saved.mixed);
   }
  }
  expect(charges).toEqual([102,113,121,129,137]);
  const saved=await(await page.request.get('/api/saves/download')).json();
  await peer.reload();await ready(peer,'ja');await expect.poll(()=>messages[1].filter(m=>m.type==='join').length).toBe(2);
  expect(messages[1].filter(m=>m.type==='join').at(-1).snapshot.mixed).toEqual(saved.mixed);
  await expect(page.locator('canvas')).toHaveAttribute('data-instance','timing-preserved');expect(errors).toEqual([]);
 }finally{await context.close();}
});

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
 const context=await browser.newContext({baseURL,locale:'ja-JP'}),peer=await context.newPage(),errors:string[]=[],messages:any[][]=[[],[]];
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
  for(const p of [page,peer]){await ready(p,p===peer?'ja':'en');await expect(p.locator('canvas')).toHaveAttribute('data-presentation','ready');}
  const canvas=page.locator('canvas');await canvas.evaluate(el=>el.setAttribute('data-instance','moving-preserved'));
  let sheets=0;page.on('request',r=>{if(r.url().includes('/content/')&&r.url().endsWith('.png'))sheets++;});
  for(const rows of messages)await expect.poll(()=>rows.filter(m=>m.type==='join').length).toBe(1);
  const joined=messages.map(rows=>rows.find(m=>m.type==='join').snapshot);expect(joined[0]).toEqual(joined[1]);
  const first=joined[0].mixed.fleet.trains[0],ids=first.cars.map((c:any)=>c.id);
  expect(first.cars.map((c:any)=>c.state.location)).toEqual([{x:24,y:18,z:2},{x:24,y:19,z:2},{x:24,y:20,z:2}]);
  // Literal straight N/S car pose: frame 0 at voxel origin +(0,-9).
  const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
  const poses=JSON.parse(readFileSync('../doc/parity/train-fixtures.json','utf8')).groups.rendering.cases;
  const ports=JSON.parse(readFileSync('../src/FreeTrain.Online.Content/reference-spatial.json','utf8')).normalRailPorts;
  async function pickCar(state:any,target=page,index=0){
   const canvas=target.locator('canvas');
   await expect(canvas).toHaveAttribute('data-render-revision',state.revision);
   await expect(canvas).toHaveAttribute('data-presentation','ready');
   const c=state.mixed.fleet.trains[0].cars[index],p=c.state.location,center=Math.trunc((state.map.height-1)/2);
   const rail=state.mixed.voxels.find((v:any)=>v.traffic?.car?.carId===c.id).traffic.rail,h=c.state.heading;
   // This gameplay save assigns simple: its selectable junction stays straight.
   const outgoing=rail.pattern>=12||ports[rail.pattern].includes(h)?h:ports[rail.pattern].includes((h+7)%8)?(h+7)%8:(h+1)%8;
   const pose=poses.find((r:any)=>r.input.kind==='level'&&r.input.recipe===c.definition.recipeId&&r.input.heading===h&&r.input.outgoing===outgoing&&r.input.isReversed===state.mixed.fleet.trains[0].isReversed);
   const frame=catalog.renderRecipes.find((r:any)=>r.id===c.definition.recipeId).frames.find((f:any)=>f.selector.angle===pose.expected.selector);
   // A roof pixel away from the rail and following cars.
   const mask=catalog.masks[frame.masks['spring/day']];let sample:{a:number;b:number}|undefined;
   for(let b=1;b<12&&!sample;b++)for(let a=5;a<27&&!sample;a++)if(mask[b][a]==='1')sample={a,b};
   expect(sample).toBeDefined();
   const q={x:16*(p.x-center+p.y)+pose.expected.offset[0]+sample!.a+.5,y:8*(p.y-p.x+center)-16*p.z+pose.expected.offset[1]+sample!.b+.5};
   const pan=await canvas.evaluate((el,q)=>{const r=el.getBoundingClientRect(),[x,y]=el.dataset.camera!.split(',').map(Number),z=Number(el.dataset.zoom);return Math.abs(q.x-x)*z>r.width/3||Math.abs(q.y-y)*z>r.height/3?{deltaX:(q.x-x)*z,deltaY:(q.y-y)*z,deltaMode:0}:undefined;},q);
   if(pan){const before=await canvas.getAttribute('data-camera');await canvas.dispatchEvent('wheel',pan);await expect(canvas).not.toHaveAttribute('data-camera',before!);await expect(canvas).toHaveAttribute('data-presentation','ready');}
   const point=await canvas.evaluate((el,q)=>{const r=el.getBoundingClientRect(),[x,y]=el.dataset.camera!.split(',').map(Number),z=Number(el.dataset.zoom);return {x:r.x+r.width/2+(q.x-x)*z,y:r.y+r.height/2+(q.y-y)*z};},q);
   await target.mouse.click(point.x,point.y);await expect(target.getByTestId('train-car-id')).toHaveText(c.id);
   await expect(target.getByText(target===peer?'車両':'Car',{exact:true})).toBeVisible();
   await expect(target.getByTestId('train-position')).toHaveText(`${p.x}, ${p.y}, ${p.z}`);
   await closePanel(target);
  }
  await pickCar(joined[0]);await pickCar(joined[0],page,1);await pickCar(joined[0]);await pickCar(joined[1],peer);
  const beforePixels=await canvas.evaluate(el=>el.toDataURL());
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
  await expect.poll(()=>canvas.evaluate(el=>el.toDataURL())).not.toBe(beforePixels);await pickCar({...joined[0],...moved});
  await pickCar({...joined[1],...moved},peer);
  await canvas.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/moving-fleet-straight.png`});
  expect(sheets).toBe(0);await expect(canvas).toHaveAttribute('data-instance','moving-preserved');
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
  await pickCar({...joined[0],...curve});
  await expect(canvas).toHaveAttribute('data-presentation','ready');
  await canvas.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/moving-fleet-curve.png`});
  expect(connections).toEqual([1,1]);
  await peer.reload();await expect.poll(()=>messages[1].filter(m=>m.type==='join').length).toBe(2);
  expect(messages[1].filter(m=>m.type==='join').at(-1).snapshot.mixed).toEqual(curve.mixed);
  expect(errors).toEqual([]);
 }finally{await context.close();}
});
