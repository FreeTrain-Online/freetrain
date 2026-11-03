import {readFileSync} from 'node:fs';
import {test,expect,type Page} from './fixtures.ts';
import {ready} from './browser-helpers.ts';
const boot='/api/bootstrap?protocolVersion=7&contentVersion=terrain-content-1&contentVersion=mixed-railway-content-1';
const snapshot=async(page:Page)=>(await(await page.request.get(boot)).json()).snapshot;
// Inventory has no dispatcher requirement. Deployed queue/continuation is covered
// by the controlled-dispatcher backend and client literal tests, before P5-08.
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
