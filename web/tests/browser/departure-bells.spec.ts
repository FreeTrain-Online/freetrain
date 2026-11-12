import {readFileSync,writeFileSync,mkdirSync} from 'node:fs';
import {join} from 'node:path';
import {test,expect,type Page} from './fixtures.ts';
import {ready,openPanel,closePanel} from './browser-helpers.ts';
const en=JSON.parse(readFileSync('src/locales/en/app.json','utf8'));
const ja=JSON.parse(readFileSync('src/locales/ja/app.json','utf8'));

const output=process.env.FREETRAIN_BELL_FLEET_OUTPUT;
const boot='/api/bootstrap?protocolVersion=10&contentVersion=mixed-railway-content-1&contentVersion=terrain-content-1';
const state=async(p:Page)=>(await(await p.request.get(boot)).json()).snapshot;
function evidence(name:string,value:unknown){const directory=process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!;mkdirSync(directory,{recursive:true});writeFileSync(join(directory,name),JSON.stringify(value,null,2)+'\n');}
const plays=(p:Page)=>p.evaluate(()=>(window as any).__liveBellPlays as string[]);

test('P6-20 selected forwarding bell plays once on gesture-enabled EN/JA peers; retries, reconnect, replacement and mute have no backlog',async({page,browser,baseURL,worldServer})=>{
 test.skip(!output,'Generate FREETRAIN_BELL_FLEET_OUTPUT with StationYardStopTests.');test.setTimeout(120000);
 const seed=JSON.parse(readFileSync(output!,'utf8')),expected=JSON.parse(readFileSync(output!+'.departed.json','utf8'));
 const context=await browser.newContext({baseURL,locale:'ja-JP',viewport:{width:430,height:860},isMobile:true,hasTouch:true});
 const peer=await context.newPage(),pages=[page,peer],messages:any[][]=[[],[]],clients:any[]=[],servers:any[]=[],errors:string[]=[];
 let serial=0;
 for(const [i,p] of pages.entries()){
  await p.addInitScript(()=>{
   (window as any).__liveBellPlays=[];
   const original=HTMLMediaElement.prototype.play;
   HTMLMediaElement.prototype.play=function(){(window as any).__liveBellPlays.push(new URL(this.src,location.href).pathname);return original.call(this);};
  });
  p.on('pageerror',error=>errors.push(String(error)));
  await p.routeWebSocket('**/api/session?*',route=>{
   clients[i]=route;const server=servers[i]=route.connectToServer();
   server.onMessage(m=>{const parsed=JSON.parse(String(m));messages[i].push(parsed);route.send(m);if(parsed.type==='departureBell')route.send(m);});
   route.onMessage(m=>server.send(m));
  });
 }
 const latest=(i:number)=>{const m=messages[i].filter(m=>['join','snapshot','delta'].includes(m.type)).at(-1);return m?.snapshot??m;};
 async function upload(){
  const before=await state(page),epoch=String(BigInt(before.worldEpoch)+1n);
  const response=await page.request.post('/api/saves/upload?expectedWorldEpoch='+before.worldEpoch,{data:Buffer.from(JSON.stringify(seed))});
  expect(response.ok(),await response.text()).toBe(true);
  for(const [i,p] of pages.entries()){await expect.poll(()=>latest(i)?.worldEpoch).toBe(epoch);await ready(p,i===0?'en':'ja');await center(p);}
 }
 async function center(p:Page){
  const canvas=p.locator('canvas.world-canvas');
  const delta=await canvas.evaluate(el=>{const c=JSON.parse((el as HTMLElement).dataset.cameraState!);return {x:192-c.x,y:128-c.y,zoom:c.zoom};});
  await canvas.dispatchEvent('wheel',{deltaX:delta.x*delta.zoom,deltaY:delta.y*delta.zoom,deltaMode:0});
  await expect.poll(()=>canvas.evaluate(el=>Math.abs(JSON.parse((el as HTMLElement).dataset.cameraState!).x-192))).toBeLessThan(1);
 }
 async function depart(){
  let command:any;
  while(BigInt((await state(page)).tick)<BigInt(expected.clock.absoluteMinute)){
   const before=await state(page),requestId='bell-step-'+(++serial),start=messages[0].length;
   command={type:'command',requestId,expectedWorldEpoch:before.worldEpoch,payload:{kind:'step'}};servers[0].send(JSON.stringify(command));
   await expect.poll(()=>messages[0].slice(start).find(m=>m.requestId===requestId)?.type).toBe('accepted');
   for(const i of [0,1])await expect.poll(()=>latest(i)?.tick).toBe(String(BigInt(before.tick)+1n));
  }
  for(const i of [0,1])expect(latest(i).mixed).toEqual(expected.mixed);
  return command;
 }
 async function enable(i:number){const text=i===0?en:ja;await openPanel(pages[i],'settings',i===0?'en':'ja');await pages[i].getByRole('button',{name:text.enableSound,exact:true}).click();await expect(pages[i].getByTestId('sound-feedback')).toHaveText(text.soundOn);await closePanel(pages[i]);}
 try{
  await page.goto('/');await ready(page);
  expect((await peer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);await peer.goto('/');await ready(peer,'ja');
  await upload();await depart();for(const p of pages)expect(await plays(p)).toEqual([]); // gesture gate consumes, never queues
  await enable(0);await enable(1);
  const catalog=await(await page.request.get('/content/bundled-v2/catalog.json')).json();
  const bell=seed.mixed.stationGraph.platforms[0].bell,contribution=catalog.contributions.find((c:any)=>c.id===bell.contributionId);
  const href=contribution.declaration.children.find((c:any)=>c.tag==='sound').attributes.href;
  const path=contribution.sourcePath.slice(0,contribution.sourcePath.lastIndexOf('/')+1)+href;
  const selected=catalog.assets.find((a:any)=>a.path===path).native.url;
  await upload();const command=await depart();
  for(const p of pages)await expect.poll(()=>plays(p)).toEqual(['/assets/station.wav',selected]);
  evidence('departure-live-peers.json',{selected,bell,peers:await Promise.all(pages.map(async(p,i)=>({locale:i===0?'en':'ja',plays:await plays(p),events:messages[i].filter(m=>m.type==='departureBell'),baseline:messages[i].filter(m=>m.type==='snapshot').at(-1)?.effectsBaseline})))});
  const old=messages[0].filter(m=>m.type==='departureBell').at(-1);
  for(const i of [0,1]){
   const event=messages[i].filter(m=>m.type==='departureBell').at(-1);expect(event.bell).toEqual(bell);
   const index=messages[i].indexOf(event);expect(messages[i].slice(0,index).filter(m=>['delta','snapshot'].includes(m.type)).at(-1).tick).toBe(expected.clock.absoluteMinute);
   clients[i].send(JSON.stringify(event));
  }
  const before=await(await page.request.get('/api/saves/download')).json(),count=messages[0].length;
  servers[0].send(JSON.stringify(command));await expect.poll(()=>messages[0].slice(count).find(m=>m.requestId===command.requestId)?.type).toBe('accepted');
  expect(await(await page.request.get('/api/saves/download')).json()).toEqual(before);
  for(const p of pages)expect(await plays(p)).toEqual(['/assets/station.wav',selected]);
  // Reconnect starts at the committed sequence; explicitly re-delivered old event stays silent.
  await openPanel(page,'session');await page.getByRole('button',{name:en.reconnect,exact:true}).click();await ready(page);clients[0].send(JSON.stringify(old));
  await upload();for(const i of [0,1])clients[i].send(JSON.stringify(old)); // old epoch after replacement
  await openPanel(peer,'settings','ja');await peer.getByRole('button',{name:ja.disableSound,exact:true}).click();await closePanel(peer);
  // Keep EN viewport outside the departure location. Both peers suppress this live event.
  await page.locator('canvas.world-canvas').dispatchEvent('wheel',{deltaX:10000,deltaY:0,deltaMode:0});
  await depart();for(const p of pages)expect(await plays(p)).toEqual(['/assets/station.wav',selected]);
  await center(page);await enable(1); // enabling adds only its explicit preview
  expect(await plays(peer)).toEqual(['/assets/station.wav',selected,'/assets/station.wav']);
  await worldServer.restart!();
  for(const [i,p] of pages.entries()){expect((await p.request.post('/api/access',{data:{secret:i===0?'':'observer-test'}})).ok()).toBe(true);await p.reload();await ready(p,i===0?'en':'ja');expect(await plays(p)).toEqual([]);}
  evidence('departure-lifecycle.json',{events:messages.map(items=>items.filter(m=>m.type==='departureBell')),restartPlays:await Promise.all(pages.map(plays)),errors});
  expect(errors).toEqual([]);
 }finally{await context.close();}
});

test('P6-20 rejected real departure callback and ambiguous retry publish no event or audio',async({page})=>{
 test.skip(!output,'Generate FREETRAIN_BELL_FLEET_OUTPUT with StationYardStopTests.');
 const seed=JSON.parse(readFileSync(output!+'.rejected.json','utf8')),messages:any[]=[];let server:any;
 await page.routeWebSocket('**/api/session?*',route=>{server=route.connectToServer();server.onMessage((m:any)=>{messages.push(JSON.parse(String(m)));route.send(m);});route.onMessage(m=>server.send(m));});
 await page.addInitScript(()=>{(window as any).__liveBellPlays=[];const original=HTMLMediaElement.prototype.play;HTMLMediaElement.prototype.play=function(){(window as any).__liveBellPlays.push(new URL(this.src,location.href).pathname);return original.call(this);};});
 await page.goto('/');await ready(page);await openPanel(page,'settings');await page.getByRole('button',{name:en.enableSound,exact:true}).click();await expect(page.getByTestId('sound-feedback')).toHaveText(en.soundOn);await closePanel(page);
 const epoch=(await state(page)).worldEpoch;
 expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+epoch,{data:Buffer.from(JSON.stringify(seed))})).ok()).toBe(true);
 await expect.poll(async()=>(await state(page)).worldEpoch).toBe(String(BigInt(epoch)+1n));await ready(page);
 const due=BigInt(seed.mixed.queue.pending.find((p:any)=>p.handlerId.startsWith('train/')).dueMinute);
 let serial=0;
 async function step(command:any){const start=messages.length;server.send(JSON.stringify(command));await expect.poll(()=>messages.slice(start).find(m=>m.requestId===command.requestId)?.type).toMatch(/accepted|rejected/);return messages.slice(start).find(m=>m.requestId===command.requestId);}
 while(BigInt((await state(page)).tick)<due-1n){const before=await state(page);expect((await step({type:'command',requestId:'advance-'+(++serial),expectedWorldEpoch:before.worldEpoch,payload:{kind:'step'}})).type).toBe('accepted');}
 const before=await(await page.request.get('/api/saves/download')).json();
 const command={type:'command',requestId:'failed-departure',expectedWorldEpoch:(await state(page)).worldEpoch,payload:{kind:'step'}};
 for(let i=0;i<2;i++){const response=await step(command);expect(response.type).toBe('rejected');expect(response.code).toBe('operationUnavailable');}
 expect(await(await page.request.get('/api/saves/download')).json()).toEqual(before);
 expect(messages.filter(m=>m.type==='departureBell')).toEqual([]);expect(await plays(page)).toEqual(['/assets/station.wav']);
 evidence('departure-rejected-retry.json',{receipts:messages.filter(m=>m.requestId==='failed-departure'),events:messages.filter(m=>m.type==='departureBell'),plays:await plays(page),saveUnchanged:true});
});
