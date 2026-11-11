import {readFileSync} from 'node:fs';
import {test,expect,type Page,type WebSocketRoute} from './fixtures.ts';
import {canonicalState as state,ready,closePanel,openPanel} from './browser-helpers.ts';

const en=JSON.parse(readFileSync('src/locales/en/app.json','utf8'));
const ja=JSON.parse(readFileSync('src/locales/ja/app.json','utf8'));
const save=async(p:Page)=>(await p.request.get('/api/saves/download')).body();
async function scene(p:Page,locale='en'){
 await p.goto('/');await ready(p,locale);const s=await state(p);
 const r=await p.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:readFileSync('tests/fixtures/station-controller-v10.json')});
 expect(r.ok(),await r.text()).toBe(true);await ready(p,locale);
}

for(const locale of ['en','ja'] as const)test(`P6-24 ${locale} station cross-tool authority, lost receipt and live canvas identity`,async({browser,baseURL})=>{
 test.setTimeout(120000);const text=locale==='ja'?ja:en;
 const context=await browser.newContext({baseURL,viewport:locale==='ja'?{width:430,height:860}:{width:1280,height:900},deviceScaleFactor:locale==='ja'?2:1,hasTouch:true});
 const p=await context.newPage(),peerContext=await browser.newContext({baseURL}),peer=await peerContext.newPage();
 let socket:WebSocketRoute|undefined,drop=()=>{},offline=false,lose=false,last:any,commands=0;
 const receipts:any[]=[],errors:string[]=[];
 await p.addInitScript(l=>localStorage.setItem('freetrain-online.locale',l),locale);
 p.on('pageerror',e=>errors.push(String(e)));peer.on('pageerror',e=>errors.push(String(e)));
 await p.route('**/api/bootstrap?*',r=>offline?r.fulfill({status:503}):r.continue());
 await p.routeWebSocket('**/api/session?*',route=>{
  const server=route.connectToServer();socket=server;drop=()=>{server.close();route.close();};
  route.onMessage(m=>{const d=JSON.parse(String(m));if(d.payload?.kind==='editPassage'){commands++;last=d;}server.send(m);});
  server.onMessage(m=>{const d=JSON.parse(String(m));if(['accepted','rejected'].includes(d.type)){receipts.push(d);if(lose&&d.requestId===last?.requestId)return;}route.send(m);});
 });
 async function passage(){
  await closePanel(p);const b=p.getByRole('button',{name:text.stationTask,exact:true});if(await b.getAttribute('aria-pressed')!=='true')await b.click();
  await p.getByRole('tab',{name:locale==='ja'?'連絡通路':'Passages',exact:true}).click();
  for(const [label,value] of [[locale==='ja'?'通路の始点 X':'Passage start X',25],[locale==='ja'?'通路の終点 X':'Passage end X',25],[locale==='ja'?'通路の始点 Y':'Passage start Y',14],[locale==='ja'?'通路の終点 Y':'Passage end Y',16],[locale==='ja'?'通路の高さ':'Passage level',2]] as const)await p.getByLabel(label,{exact:true}).fill(String(value));
  await p.locator('.passage-task form').getByRole('button',{name:locale==='ja'?'配置を確認':'Check placement',exact:true}).click();
 }
 const confirm=()=>p.getByRole('button',{name:locale==='ja'?'通路の設置を確定':'Confirm passage',exact:true});
 try{
  await scene(p,locale);await peer.request.post('/api/access',{data:{secret:'operator-test'}});await peer.goto('/');await ready(peer);
  const canvas=p.locator('.world-canvas');await canvas.evaluate(el=>el.dataset.instance='station-cross-tool');
  // Visit each construction task against the live station graph and real train.
  for(const task of ['stationTask','trackTask','slopeTask','bridgeTask','girderTask','tunnelTask','garageTask','signalTask','poleTask','stationaryTask','speedTask','terrainTool','removeTool'] as const){
   await p.getByRole('button',{name:text[task],exact:true}).click();
  }
  // A station footprint protects the same ground used by the terrain tool.
  await p.getByRole('button',{name:text.terrainTool,exact:true}).click();
  const building=(await state(p)).mixed.stationGraph.buildings[0];
  for(const [key,value] of [['cornerX',building.location.x],['cornerY',building.location.y],['cornerZ',building.location.z]] as const)await p.getByLabel(text[key],{exact:true}).fill(String(value));
  const protectedSave=await save(p);await p.getByRole('button',{name:text.cornerCheck,exact:true}).click();
  await expect(p.getByRole('button',{name:locale==='ja'?'地形の変更を確定':'Confirm terrain change',exact:true})).toBeDisabled();expect(await save(p)).toEqual(protectedSave);
  const gameplay=await state(p);
  for(const enabled of [true,false]){
   const label=locale==='ja'?(enabled?'マップ作成を開始':'マップ作成を終了'):(enabled?'Enter map authoring':'Exit map authoring');
   await p.getByRole('button',{name:label,exact:true}).click();
   await p.getByRole('button',{name:locale==='ja'?'モード変更を確定':'Confirm mode change',exact:true}).click();
   await expect.poll(async()=>(await state(p)).terrain.constructionMode).toBe(enabled);
   const mode=await state(p);expect(mode.tick).toBe(gameplay.tick);expect(mode.paused).toBe(gameplay.paused);
   for(const key of ['stationGraph','fleet','queue','accounts'])expect(mode.mixed[key]).toEqual(gameplay.mixed[key]);
  }
  await closePanel(p);const before=await save(p),initial=await state(p);
  await passage();await expect(confirm()).toBeEnabled();expect(await save(p)).toEqual(before);
  // Source pole setters allow a real car/platform host. This shared edit must
  // invalidate the passage quote while retaining the actual rail and train.
  await peer.getByRole('button',{name:'Electric poles',exact:true}).click();
  for(const [label,value] of [['Start X',25],['Start Y',15],['Track level',2]] as const)await peer.getByLabel(label,{exact:true}).fill(String(value));
  await peer.getByRole('button',{name:'Preview route',exact:true}).click();await peer.getByRole('button',{name:'Confirm pole change',exact:true}).click();
  await expect(peer.getByTestId('rail-route')).toHaveCount(0);await expect(confirm()).toBeDisabled();
  expect((await state(p)).mixed.fleet).toEqual(initial.mixed.fleet);
  expect((await state(p)).mixed.queue).toEqual(initial.mixed.queue);
  await passage();await expect(confirm()).toBeEnabled();
  // A real peer callback invalidates the cosmetic quote, even with unchanged terrain.
  await peer.locator('.world-canvas').focus();await peer.keyboard.press('n');
  await expect.poll(async()=>(await state(p)).tick).toBe(String(BigInt(initial.tick)+1n));await expect(confirm()).toBeDisabled();
  const advanced=await state(p);expect(advanced.terrain).toEqual(initial.terrain);
  await passage();await expect(confirm()).toBeEnabled();lose=true;
  if(locale==='ja')await confirm().tap();else{await confirm().focus();await p.keyboard.press('Enter');}
  await expect.poll(()=>receipts.some(r=>r.requestId===last?.requestId&&r.type==='accepted')).toBe(true);
  const committed=await save(p),after=await state(p);
  expect(after.mixed.stationGraph.platforms[0].yards[0].outlook.kind).toBe('passage');
  expect(after.mixed.fleet).toEqual(advanced.mixed.fleet);expect(after.mixed.queue).toEqual(advanced.mixed.queue);
  expect(after.mixed.accounts).toEqual(advanced.mixed.accounts);expect(after.mixed.voxels).toEqual(advanced.mixed.voxels);
  await expect.poll(async()=>(await state(peer)).mixed).toEqual(after.mixed);
  await expect(canvas).toHaveAttribute('data-instance','station-cross-tool');
  offline=true;drop();await expect(p.getByRole('dialog')).toBeVisible();await expect(canvas).toHaveAttribute('data-scene','demo');
  await p.keyboard.press('Enter');await p.keyboard.press('j');expect(commands).toBe(1);
  offline=false;lose=false;await ready(p,locale);expect(commands).toBe(1);expect(await save(p)).toEqual(committed);
  // Retrying exactly the receipt is idempotent; a new ID with the old query rejects.
  let count=receipts.length;socket!.send(JSON.stringify(last));await expect.poll(()=>receipts.length).toBe(count+1);
  expect(receipts.at(-1).type).toBe('accepted');expect(await save(p)).toEqual(committed);
  socket!.send(JSON.stringify({...last,requestId:'stale-station-passage'}));
  await expect.poll(()=>receipts.at(-1)?.requestId).toBe('stale-station-passage');expect(receipts.at(-1).type).toBe('rejected');expect(await save(p)).toEqual(committed);
  await openPanel(p,'stations',locale);
  await p.getByLabel(text.stationSelect,{exact:true}).selectOption(after.mixed.stationGraph.buildings[0].id);
  await p.getByRole('button',{name:text.stationMove,exact:true}).click();
  await closePanel(p);await expect(canvas).toHaveAttribute('data-presentation','ready');expect(await save(p)).toEqual(committed);
  await p.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/station-cross-tool-${locale}.png`});
  // An Observer cannot bypass disabled UI by sending the same payload directly.
  await p.request.post('/api/access',{data:{secret:'observer-test'}});await p.reload();await ready(p,locale);
  await expect(p.getByRole('button',{name:text.stationTask,exact:true})).toBeDisabled();
  count=receipts.length;socket!.send(JSON.stringify({...last,requestId:'observer-station-passage'}));await expect.poll(()=>receipts.length).toBe(count+1);
  expect(receipts.at(-1).code).toBe('notAuthorized');
  expect((await p.request.get('/api/saves/download')).status()).toBe(403);
  await p.request.post('/api/access',{data:{secret:''}});expect(await save(p)).toEqual(committed);
  expect(errors).toEqual([]);
 }finally{await context.close();await peerContext.close();}
});

test('P6-24 cold station join gates commands until live state and station art are ready',async({page,browser,baseURL})=>{
 await scene(page);const bytes=await save(page),context=await browser.newContext({baseURL}),peer=await context.newPage();
 let releaseAssets=()=>{},releaseJoin=()=>{};const assets=new Promise<void>(r=>releaseAssets=r),join=new Promise<void>(r=>releaseJoin=r);
 try{
  await peer.route('**/assets/*.png',async r=>{await assets;await r.continue();});
  await peer.routeWebSocket('**/api/session?*',r=>{const server=r.connectToServer();let delivery=Promise.resolve();server.onMessage(m=>{delivery=delivery.then(async()=>{await join;r.send(m);});});});
  await peer.goto('/',{waitUntil:'domcontentloaded'});await expect(peer.getByRole('dialog')).toBeVisible();await expect(peer.locator('.command-bar')).toHaveCount(0);
  await peer.keyboard.press('j');await peer.keyboard.press('Enter');releaseJoin();await expect(peer.getByTestId('connection')).toHaveText(en.ready);
  await expect(peer.locator('.command-bar')).toHaveCount(0);releaseAssets();await ready(peer);
  await expect(peer.locator('.world-canvas')).toHaveAttribute('data-presentation','ready');expect(await save(peer)).toEqual(bytes);
 }finally{releaseJoin();releaseAssets();await context.close();}
});
