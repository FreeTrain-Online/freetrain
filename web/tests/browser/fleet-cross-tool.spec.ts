import {readFileSync,mkdirSync} from 'node:fs';
import {test,expect,type Page,type WebSocketRoute} from './fixtures.ts';
import {canonicalState as state,openPanel,closePanel,ready,previewRemovalLine,confirmRemoval} from './browser-helpers.ts';

const en=JSON.parse(readFileSync('src/locales/en/app.json','utf8'));
const ja=JSON.parse(readFileSync('src/locales/ja/app.json','utf8'));
const tasks=['trackTask','slopeTask','bridgeTask','girderTask','tunnelTask','garageTask',
 'signalTask','poleTask','stationaryTask','speedTask','removeTool'] as const;
const button=(p:Page,name:string)=>p.getByRole('button',{name,exact:true});
const save=async(p:Page)=>(await p.request.get('/api/saves/download')).body();

async function track(p:Page,text=en,from=22,to=40,y=27,endY=y){
 const tool=button(p,text.trackTask);if(await tool.getAttribute('aria-pressed')!=='true')await tool.click();
 for(const [key,value] of [['railFromX',from],['railFromY',y],['railToX',to],['railToY',endY],['buildLevel',1]] as const)
  await p.getByLabel(text[key],{exact:true}).fill(String(value));
 await button(p,text.railCheck).click();
}
async function owned(p:Page,text=en,index=0){
 await openPanel(p,'fleet',text===ja?'ja':'en');
 await p.getByRole('button',{name:new RegExp('^'+text.ownedStock)}).click();
 await p.locator('.catalog-item-main').nth(index).click();
}
async function placement(p:Page,x=26){
 for(const [key,value] of [['X',x],['Y',27],['Z',1]] as const)
  await p.locator('.deployment-controls').getByLabel(key,{exact:true}).fill(String(value));
 await p.locator('.deployment-controls').getByLabel('Z',{exact:true}).press('Enter');
}
async function setup(p:Page,locale='en',quantity=1){
 await p.goto('/');await ready(p);const s=await state(p);
 const r=await p.request.post('/api/worlds/empty/preview',{data:{name:'Moving cross-tool regression',dimensions:{h:40,v:48,z:6},waterLevel:1,epoch:s.worldEpoch,revision:s.revision}});
 expect(r.ok(),await r.text()).toBe(true);const c=await r.json();
 expect((await p.request.post('/api/worlds/empty/commit',{data:{id:c.id,requestHash:c.requestHash,candidateHash:c.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).ok()).toBe(true);
 await ready(p);await track(p);await button(p,en.confirmTrack).click();await expect(p.getByTestId('rail-route')).toHaveCount(0);
 await openPanel(p,'fleet');await p.getByLabel(en.stockLength,{exact:true}).selectOption('3');
 await p.getByLabel(en.stockQuantity,{exact:true}).fill(String(quantity));
 await button(p,en.stockQuote).click();await button(p,en.stockConfirmBuy).click();
 await owned(p);await placement(p);await expect(p.getByTestId('deployment-car')).toHaveCount(3);
 await button(p,en.deploymentConfirm).click();await expect(button(p,en.withdrawTrain)).toBeVisible();await closePanel(p);
 if(locale==='ja'){await openPanel(p,'settings');await p.getByLabel(en.language,{exact:true}).selectOption('ja');await closePanel(p);}
 return state(p);
}
async function step(p:Page,minutes=2){
 const tick=BigInt((await state(p)).tick);
 for(let n=0;n<minutes;n++)await p.getByRole('button',{name:/Single step/}).first().click();
 await expect.poll(async()=>BigInt((await state(p)).tick)).toBe(tick+BigInt(minutes));
}
function consistent(s:any){
 const cars=s.mixed.fleet.trains.flatMap((t:any)=>t.cars.filter((c:any)=>c.state.kind==='inside').map((c:any)=>({t,c})));
 expect(s.mixed.voxels.filter((v:any)=>v.traffic?.car)).toHaveLength(cars.length);
 for(const {t,c} of cars){
  const host=s.mixed.voxels.find((v:any)=>JSON.stringify(v.position)===JSON.stringify(c.state.location));
  expect(host?.traffic.car).toMatchObject({trainId:t.id,carId:c.id});
 }
 expect(new Set(s.mixed.queue.pending.map((e:any)=>e.handlerId)).size).toBe(s.mixed.queue.pending.length);
}

for(const locale of ['en','ja'] as const)test(`${locale} real moving fleet survives construction tasks, input resets and role changes`,async({page:p})=>{
 test.setTimeout(120000);const text=locale==='ja'?ja:en,errors:string[]=[];p.on('pageerror',e=>errors.push(String(e)));
 await p.addInitScript(()=>{const original=HTMLImageElement.prototype.decode;(window as any).__decodes=0;HTMLImageElement.prototype.decode=function(){(window as any).__decodes++;return original.call(this);};});
 await p.addInitScript(()=>{
  (window as any).__bellContexts=[];
  const connect=AudioNode.prototype.connect;
  AudioNode.prototype.connect=function(destination:AudioNode|AudioParam,...args:number[]){
   if(destination instanceof AudioDestinationNode&&!(window as any).__bellContexts.includes(this.context))(window as any).__bellContexts.push(this.context);
   return connect.call(this,destination as AudioNode,...args);
  } as typeof AudioNode.prototype.connect;
 });
 const initial=await setup(p,locale),ids=initial.mixed.fleet.trains[0].cars.map((c:any)=>c.id);
 const canvas=p.locator('.world-canvas');await canvas.evaluate(el=>el.dataset.instance='moving-cross-tool');
 if(locale==='ja'){await p.setViewportSize({width:390,height:844});await p.evaluate(()=>document.documentElement.style.fontSize='200%');}
 // Warm the source catalog cards before checking that task changes retain images.
 await openPanel(p,'catalog',locale);const catalog=p.locator('#building-catalog');
 for(const category of ['signal','stationary']){
  await catalog.getByLabel(text.catalogCategory,{exact:true}).selectOption(category);
  await expect.poll(()=>catalog.locator('img').evaluateAll(images=>images.length>0&&images.every(i=>(i as HTMLImageElement).complete))).toBe(true);
 }
 await catalog.getByRole('button',{name:text.closeCatalog,exact:true}).click();
 await p.locator('.transport button').first().click();
 await openPanel(p,'settings',locale);await button(p,text.enableSound).click();
 await expect(p.getByTestId('sound-feedback')).toHaveText(text.soundOn);
 await button(p,text.previewBell).click();
 await expect.poll(()=>p.evaluate(()=>(window as any).__bellContexts.length)).toBe(1);
 await expect.poll(()=>p.evaluate(()=>(window as any).__bellContexts[0].state)).toBe('closed');
 await closePanel(p);await expect(p.getByTestId('clock-state')).toHaveText(text.running);
 const decodes=await p.evaluate(()=>(window as any).__decodes);
 const tick=(await state(p)).tick;
 for(const task of tasks){await button(p,text[task]).click();await expect(p.getByTestId('clock-state')).toHaveText(text.running);}
 await expect.poll(async()=>(await state(p)).tick).not.toBe(tick);
 await expect.poll(async()=>(await state(p)).mixed.fleet.trains[0].moveCount).toBeGreaterThan(0);
 await button(p,text.referenceInspect).click();
 for(const event of ['blur','visibilitychange','pointercancel']){
  const box=(await canvas.boundingBox())!;await p.mouse.move(box.x+box.width*.6,box.y+box.height*.3);await p.mouse.down({button:'middle'});await p.mouse.move(box.x+box.width*.6+20,box.y+box.height*.3+10);
  await p.evaluate(event=>{(event==='visibilitychange'?document:event==='pointercancel'?document.querySelector('canvas')!:window).dispatchEvent(new Event(event));},event);
  const camera=await canvas.getAttribute('data-camera');await p.mouse.move(box.x+box.width*.6+70,box.y+box.height*.3+30);await p.mouse.up({button:'middle'});await expect(canvas).toHaveAttribute('data-camera',camera!);
 }
 await p.locator('.transport button').first().click();await expect.poll(async()=>(await state(p)).paused).toBe(true);
 await expect(canvas).toHaveAttribute('data-presentation','ready');await expect(canvas).toHaveAttribute('data-instance','moving-cross-tool');
 const after=await state(p);consistent(after);expect(after.mixed.fleet.trains[0].cars.map((c:any)=>c.id).sort()).toEqual(ids.sort());
 expect(after.mixed.accounts.transactions.filter((t:any)=>!['trainMove'].includes(t.operation))).toEqual(initial.mixed.accounts.transactions);
 expect(await p.evaluate(()=>(window as any).__decodes)).toBe(decodes);
 const saved=await save(p);mkdirSync(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!+'/fleet-cross-tool',{recursive:true});
 await p.screenshot({path:process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!+`/fleet-cross-tool/${locale}.png`});
 for(const [secret,role] of [['operator-test',text.operator],['observer-test',text.observer]]){
  expect((await p.request.post('/api/access',{data:{secret}})).ok()).toBe(true);await p.reload();await ready(p,locale);
  await expect(p.getByTestId('participant-role')).toHaveText(role);
  for(const task of tasks)await expect(button(p,text[task]))[secret==='observer-test'?'toBeDisabled':'toBeEnabled']();
 }
 await p.request.post('/api/access',{data:{secret:''}});expect(await save(p)).toEqual(saved);expect(errors).toEqual([]);
});

test('cold join to a running purchased fleet gates all input until assets and the first live draw are ready',async({page:p,browser,baseURL})=>{
 await setup(p);await p.locator('.transport button').first().click();
 const context=await browser.newContext({baseURL}),peer=await context.newPage();
 let releaseAssets=()=>{},releaseJoin=()=>{};
 const assets=new Promise<void>(r=>releaseAssets=r),join=new Promise<void>(r=>releaseJoin=r);
 try{
  await peer.route('**/assets/*.png',async route=>{await assets;await route.continue();});
  await peer.routeWebSocket('**/api/session?*',route=>{
   const server=route.connectToServer();let delivery=Promise.resolve();
   server.onMessage(m=>{delivery=delivery.then(async()=>{await join;route.send(m);});});
  });
  await peer.goto('/',{waitUntil:'domcontentloaded'});await expect(peer.getByRole('dialog')).toBeVisible();
  await expect(peer.locator('.command-bar')).toHaveCount(0);await peer.keyboard.press('n');await peer.keyboard.press('r');
  releaseJoin();await expect(peer.getByTestId('connection')).toHaveText(en.ready);
  await expect(peer.locator('.command-bar')).toHaveCount(0);releaseAssets();await ready(peer);
  await expect(peer.locator('.world-canvas')).toHaveAttribute('data-presentation','ready');
  await expect.poll(async()=>(await state(peer)).mixed.fleet.trains[0].moveCount).toBeGreaterThan(0);
  await p.locator('.transport button').first().click();await expect.poll(async()=>(await state(peer)).paused).toBe(true);
  consistent(await state(peer));
 }finally{releaseAssets();releaseJoin();await context.close();}
});

test('peer movement invalidates a construction quote and fresh whole-object removal skips actual car hosts at zero cost',async({page:p,browser,baseURL})=>{
 await setup(p);const context=await browser.newContext({baseURL}),peer=await context.newPage();
 try{
  await peer.request.post('/api/access',{data:{secret:'operator-test'}});await peer.goto('/');await ready(peer);
  await track(p,en,29,26,27,30);await expect(button(p,en.confirmTrack)).toBeEnabled();
  await step(peer);await expect(p.getByTestId('rail-status')).toContainText(en.railStale);await expect(button(p,en.confirmTrack)).toBeDisabled();
  const moved=await state(p),bytes=await save(p);consistent(moved);expect(moved.mixed.fleet.trains[0].cars[0].state.location).toEqual({x:29,y:27,z:1});
  await track(p,en,29,26,27,30);await expect(button(p,en.confirmTrack)).toBeDisabled();expect(await save(p)).toEqual(bytes);
  await previewRemovalLine(p,[28,27],[30,27]);await confirmRemoval(p);
  const after=await state(p);consistent(after);expect(after.cash).toBe(moved.cash);
  expect(after.mixed.fleet).toEqual(moved.mixed.fleet);expect(after.mixed.queue).toEqual(moved.mixed.queue);
  expect(after.mixed.voxels.some((v:any)=>v.position.x===30&&v.position.y===27)).toBe(false);
  for(const x of [27,28,29])expect(after.mixed.voxels.find((v:any)=>v.position.x===x&&v.position.y===27)?.traffic.car).toBeTruthy();
  await expect.poll(async()=>(await state(peer)).mixed).toEqual(after.mixed);
 }finally{await context.close();}
});

test('late fleet previews cannot resurrect sold selection or old-world ghosts',async({page:p,browser,baseURL})=>{
 await setup(p,'en',2);const context=await browser.newContext({baseURL}),peer=await context.newPage();
 let release=()=>{},held=false;const gate=new Promise<void>(r=>release=r);
 try{
  await peer.goto('/');await ready(peer);await owned(p,en,1);
  await p.route('**/api/fleet/deployment/preview?*',async route=>{const response=await route.fetch();held=true;await gate;await route.fulfill({response});});
  await placement(p,34);await expect.poll(()=>held).toBe(true);
  await owned(peer,en,1);await button(peer,en.stockSaleQuote).click();await button(peer,en.stockConfirmSell).click();
  await expect.poll(async()=>(await state(p)).mixed.fleet.trains.length).toBe(1);release();
  await expect(p.getByTestId('deployment-car')).toHaveCount(0);await expect(p.locator('.world-canvas')).toHaveAttribute('data-fleet-ghost-cars','0');
  expect(await button(p,en.deploymentConfirm).count()).toBe(0);await p.unroute('**/api/fleet/deployment/preview?*');
  await owned(p);let releaseSecond=()=>{};const second=new Promise<void>(r=>releaseSecond=r);held=false;
  await p.route('**/api/fleet/deployment/preview?*',async route=>{const response=await route.fetch();held=true;await second;await route.fulfill({response});});
  try{
   await button(p,en.fleetReverse).click();await expect.poll(()=>held).toBe(true);const bytes=await save(p),s=await state(p);
   expect((await p.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:bytes})).ok()).toBe(true);
   await expect.poll(async()=>(await state(p)).worldEpoch).not.toBe(s.worldEpoch);await ready(p);releaseSecond();
   await expect(p.getByTestId('deployment-car')).toHaveCount(0);await expect(p.locator('.world-canvas')).toHaveAttribute('data-fleet-ghost-cars','0');
   expect(await save(p)).toEqual(bytes);consistent(await state(p));
  }finally{releaseSecond();}
 }finally{release();await context.close();}
});

test('lost fleet acknowledgement and reconnect never replay; explicit receipt retry applies once',async({page:p})=>{
 test.setTimeout(90000);let socket:WebSocketRoute|undefined,drop=()=>{},offline=false,lose=false,commands=0,last:any;
 const receipts:any[]=[];
 await p.route('**/api/bootstrap?*',route=>offline?route.fulfill({status:503}):route.continue());
 await p.routeWebSocket('**/api/session?*',route=>{
  const server=route.connectToServer();socket=server;drop=()=>{server.close();route.close();};
  route.onMessage(m=>{const d=JSON.parse(String(m));if(d.payload?.kind==='deployFleet'){commands++;last=d;}server.send(m);});
  server.onMessage(m=>{const d=JSON.parse(String(m));if(['accepted','rejected'].includes(d.type)){receipts.push(d);if(lose&&d.requestId===last?.requestId)return;}route.send(m);});
 });
 await setup(p);await owned(p);commands=0;const before=await state(p),train=before.mixed.fleet.trains[0];
 await button(p,en.fleetReverse).click();lose=true;await button(p,en.fleetReverseConfirm).click();
 await expect.poll(()=>receipts.some(r=>r.requestId===last?.requestId&&r.type==='accepted')).toBe(true);
 const committed=await save(p);expect((await state(p)).mixed.fleet.trains[0].cars.map((c:any)=>c.id)).toEqual(train.cars.map((c:any)=>c.id).reverse());
 offline=true;drop();await expect(p.getByRole('dialog')).toBeVisible();await expect(p.locator('.world-canvas')).toHaveAttribute('data-scene','demo');
 await p.keyboard.press('Enter');await p.keyboard.press('r');expect(commands).toBe(1);
 offline=false;lose=false;await ready(p);await expect(p.getByTestId('deployment-car')).toHaveCount(0);
 expect(commands).toBe(1);expect(await save(p)).toEqual(committed);
 const n=receipts.length;socket!.send(JSON.stringify(last));await expect.poll(()=>receipts.length).toBe(n+1);
 expect(receipts.at(-1).type).toBe('accepted');expect(await save(p)).toEqual(committed);
 socket!.send(JSON.stringify({...last,requestId:'old-fleet-query-new-id'}));await expect.poll(()=>receipts.at(-1)?.requestId).toBe('old-fleet-query-new-id');
 expect(receipts.at(-1).code).toBe('staleRevision');expect(await save(p)).toEqual(committed);consistent(await state(p));
});
