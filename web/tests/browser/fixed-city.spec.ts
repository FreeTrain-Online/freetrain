import {readFileSync} from 'node:fs';
import {test,expect,type Page} from './fixtures.ts';
import {ready,openPanel} from './browser-helpers.ts';
const texts=['en','ja'].map(locale=>JSON.parse(readFileSync(`src/locales/${locale}/app.json`,'utf8')));
const compatibility='protocolVersion=11&contentVersion=terrain-content-1&contentVersion=mixed-railway-content-1';
const snapshot=async(p:Page)=>(await(await p.request.get('/api/bootstrap?'+compatibility)).json()).snapshot;
test('P7-07 EN/JA peers observe natural fixed producer completion, literal station demand and exact checkpoint restart',async({page,browser,baseURL,worldServer})=>{
 test.setTimeout(120000);
 const seed=JSON.parse(readFileSync('tests/fixtures/fixed-city-v11.json','utf8'));
 const oracle=JSON.parse(readFileSync('tests/fixtures/fixed-city-v11.json.snapshot.json','utf8'));
 const context=await browser.newContext({baseURL,locale:'ja-JP',viewport:{width:430,height:860},isMobile:true,hasTouch:true}),peer=await context.newPage();
 const messages:any[][]=[[],[]],sockets:any[]=[],errors:string[]=[];
 for(const [i,p] of [page,peer].entries()){
  p.on('pageerror',e=>errors.push(String(e)));
  await p.routeWebSocket('**/api/session?*',route=>{const server=route.connectToServer();sockets[i]=server;
   server.onMessage(m=>{messages[i].push(JSON.parse(String(m)));route.send(m);});route.onMessage(m=>server.send(m));});
 }
 const latest=(i:number)=>{const m=messages[i].filter(m=>['join','snapshot','delta'].includes(m.type)).at(-1);return m?.snapshot??m;};
 async function step(index:number,id:string){const s=await snapshot(page),start=messages[index].length;sockets[index].send(JSON.stringify({type:'command',requestId:id,expectedWorldEpoch:s.worldEpoch,payload:{kind:'step'}}));
  await expect.poll(()=>messages[index].slice(start).find(m=>m.requestId===id)).toBeTruthy();return messages[index].slice(start).find(m=>m.requestId===id);}
 try{
  await page.goto('/');await ready(page);const initial=await snapshot(page);
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+initial.worldEpoch,{data:Buffer.from(JSON.stringify(seed))})).ok()).toBe(true);
  expect((await peer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);
  await peer.goto('/');await ready(peer,'ja');
  for(const i of [0,1])await expect.poll(()=>latest(i)?.mixed?.city).toEqual(seed.mixed.city);
  for(const [i,p] of [page,peer].entries()){
   await openPanel(p,'stations',i===0?'en':'ja');await p.getByLabel(texts[i].stationSelect,{exact:true}).selectOption('1');await expect(p.getByTestId('station-demand')).toHaveText('0');
  }
  expect((await step(1,'fixed-observer')).code).toBe('notAuthorized');
  const receipt=await step(0,'fixed-complete');expect(receipt.type).toBe('accepted');
  for(const i of [0,1])await expect.poll(()=>latest(i)?.mixed?.city).toEqual(oracle.mixed.city);
  expect((await snapshot(page)).mixed).toEqual(oracle.mixed);
  for(const p of [page,peer]){
   // Jan 2 holiday 14:00: ten residents * 30% outgoing, capped at 40.
   await expect(p.getByTestId('station-demand')).toHaveText('3');await expect(p.getByTestId('station-waiting')).toHaveText('3');
  }
  expect(await step(0,'fixed-complete')).toEqual(receipt);expect((await snapshot(page)).tick).toBe(oracle.tick);
  const saved=await(await page.request.get('/api/saves/download')).body(),s=await snapshot(page);
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:saved})).ok()).toBe(true);
  for(const i of [0,1])await expect.poll(()=>latest(i)?.worldEpoch).toBe(String(BigInt(s.worldEpoch)+1n));
  expect((await snapshot(page)).mixed).toEqual(oracle.mixed);
  await worldServer.restart!();
  for(const p of [page,peer])expect((await p.request.post('/api/access',{data:{secret:p===page?'':'observer-test'}})).ok()).toBe(true);
  await page.reload();await peer.reload();await ready(page);await ready(peer,'ja');
  for(const [i,p] of [page,peer].entries()){
   expect((await snapshot(p)).mixed).toEqual(oracle.mixed);await openPanel(p,'stations',i===0?'en':'ja');await p.getByLabel(texts[i].stationSelect,{exact:true}).selectOption('1');await expect(p.getByTestId('station-demand')).toHaveText('3');
  }
  expect((await step(0,'fixed-next-minute')).type).toBe('accepted');
  expect((await snapshot(page)).mixed.city).toEqual(oracle.mixed.city);expect(errors).toEqual([]);
 }finally{await context.close();}
});
