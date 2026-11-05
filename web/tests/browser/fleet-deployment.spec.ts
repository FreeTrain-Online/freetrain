import {test,expect,type Page,type WebSocketRoute} from './fixtures.ts';
import {canonicalState as state,closePanel,openPanel,ready} from './browser-helpers.ts';
import {settled} from './layout-helpers.ts';
import {mkdirSync,readFileSync} from 'node:fs';
const en=JSON.parse(readFileSync('src/locales/en/app.json','utf8'));
const ja=JSON.parse(readFileSync('src/locales/ja/app.json','utf8'));
const evidence=process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!;
const save=async(p:Page)=>(await p.request.get('/api/saves/download')).body();

for(const locale of ['en','ja'] as const)test(`${locale} reversal recovery preserves identities timer and accounts through peer restart`,async({page,browser,baseURL,worldServer})=>{
 test.setTimeout(90000);const text=locale==='ja'?ja:en,errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 const initial=await state(page),isolated=JSON.parse(readFileSync('tests/helpers/moving-fleet-v9.json','utf8'));
 // Retain the occupied cells of a purchased/deployed ring as an isolated
 // straight consist for TRAIN-D001's both-null retry.
 isolated.mixed.voxels=isolated.mixed.voxels.filter((v:any)=>v.traffic?.car);
 const owners=new Set(isolated.mixed.voxels.map((v:any)=>v.ownerId));isolated.spatial.entities=isolated.spatial.entities.filter((e:any)=>owners.has(e.id));
 for(const v of isolated.mixed.voxels){v.traffic.rail.pattern=10;v.traffic.rail.definition.contributionId=v.traffic.rail.definition.recipeId='system:pattern/10';}
 isolated.paused=true;
 const upload=await page.request.post('/api/saves/upload?expectedWorldEpoch='+initial.worldEpoch,{data:JSON.stringify(isolated)});expect(upload.ok(),await upload.text()).toBe(true);
 await page.goto('/');await ready(page);await openPanel(page,'settings');await page.getByLabel('Language',{exact:true}).selectOption(locale);await closePanel(page);
 const context=await browser.newContext({baseURL}),peer=await context.newPage();
 try{
  await peer.goto('/');await ready(peer);await owned(page,text);
  const before=await state(page),train=before.mixed.fleet.trains[0],saved=await save(page),canvas=page.locator('canvas');
  await canvas.evaluate(el=>el.dataset.instance='recovery-preserved');const camera=await canvas.getAttribute('data-camera');
  const reverse=page.getByRole('button',{name:text.fleetReverse,exact:true});await reverse.click();
  await expect(page.getByTestId('deployment-car')).toHaveCount(3);expect(await save(page)).toEqual(saved);
  await page.getByRole('button',{name:text.cancel,exact:true}).click();await expect(reverse).toBeFocused();
  await reverse.click();await peer.getByRole('button',{name:/Single step/}).first().click();
  await expect(page.getByText(text.stockStale,{exact:true})).toBeVisible();await expect(page.getByRole('button',{name:text.fleetReverseConfirm,exact:true})).toBeDisabled();
  await page.getByRole('button',{name:text.cancel,exact:true}).click();const current=await state(page);
  await reverse.click();await page.getByRole('button',{name:text.fleetReverseConfirm,exact:true}).dblclick();
  await expect(page.getByTestId('deployment-car')).toHaveCount(0);
  await expect.poll(async()=>(await state(page)).mixed.fleet.trains[0].isReversed).toBe(true);let s=await state(page);
  expect(s.mixed.fleet.trains[0].cars.map((c:any)=>c.id)).toEqual(train.cars.map((c:any)=>c.id).reverse());
  expect(s.mixed.fleet.trains[0].isReversed).toBe(true);expect(s.mixed.queue).toEqual(current.mixed.queue);expect(s.mixed.accounts).toEqual(before.mixed.accounts);
  await expect.poll(async()=>(await state(peer)).mixed.fleet).toEqual(s.mixed.fleet);
  await expect(canvas).toHaveAttribute('data-camera',camera!);
  await closePanel(page);await pickRecoveryCar(page,s);
  await expect(page.getByTestId('train-car-id')).toHaveText('39');await expect(page.getByTestId('fleet-state')).toContainText(text.moving);
  await owned(page,text);
  await peer.getByRole('button',{name:/Single step/}).first().click();
  await expect(page.getByTestId('fleet-state')).toContainText(text.fleetEmergency);await expect(page.getByTestId('fleet-recovery-reason')).toContainText(text.fleetMissingTrack);
  s=await state(page);expect(s.mixed.fleet.trains[0].isReversed).toBe(false);expect(s.mixed.fleet.trains[0].moveCount).toBe(0);expect(s.mixed.accounts).toEqual(before.mixed.accounts);expect(s.mixed.queue.pending).toHaveLength(1);
  await expect(canvas).toHaveAttribute('data-instance','recovery-preserved');
  mkdirSync(evidence,{recursive:true});await page.screenshot({path:`${evidence}/emergency-${locale}.png`});
  const stopped=await save(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:stopped})).ok()).toBe(true);await ready(page,locale);await ready(peer);expect(await save(page)).toEqual(stopped);
  await worldServer.restart!();for(const p of [page,peer])expect((await p.request.post('/api/access',{data:{secret:''}})).ok()).toBe(true);
  await page.reload();await ready(page,locale);await peer.reload();await ready(peer);expect(await save(page)).toEqual(stopped);await owned(page,text);
  await expect(page.getByTestId('fleet-recovery-reason')).toContainText(text.fleetMissingTrack);
  if(locale==='ja'){
   await page.setViewportSize({width:390,height:844});await page.locator('.primary-panel .sheet-toggle').click();
   await page.locator('html').evaluate(el=>el.style.fontSize='200%');
   await page.getByRole('button',{name:text.fleetReverse,exact:true}).scrollIntoViewIfNeeded();
   await expect(page.getByRole('button',{name:text.fleetReverse,exact:true})).toBeVisible();
   expect(await page.locator('.primary-panel').evaluate(el=>el.scrollWidth<=el.clientWidth+1)).toBe(true);
   await page.screenshot({path:`${evidence}/emergency-ja-narrow.png`});
   await page.locator('html').evaluate(el=>el.style.fontSize='');await page.setViewportSize({width:1280,height:900});
  }
  for(let n=0;n<2;n++)await peer.getByRole('button',{name:/Single step/}).first().click();
  await expect.poll(async()=>(await state(page)).mixed.fleet.trains[0].isReversed).toBe(true);
  // Repair the south approach from an unoccupied cell with the real rail tool.
  await closePanel(page);await page.getByRole('button',{name:text.trackTask,exact:true}).click();
  for(const [name,value] of [[text.railFromX,24],[text.railFromY,21],[text.railToX,24],[text.railToY,24],[text.buildLevel,2]] as const)await page.getByLabel(name,{exact:true}).fill(String(value));
  await page.getByRole('button',{name:text.railCheck,exact:true}).click();await page.getByRole('button',{name:text.confirmTrack,exact:true}).click();await expect(page.getByTestId('rail-route')).toHaveCount(0);
  await page.getByRole('button',{name:text.referenceInspect,exact:true}).click();
  for(let n=0;n<2;n++)await peer.getByRole('button',{name:/Single step/}).first().click();
  await expect.poll(async()=>(await state(page)).mixed.fleet.trains[0].moveCount).toBe(1);s=await state(page);
  expect(s.mixed.fleet.trains[0].state).toBe('moving');expect(s.mixed.fleet.trains[0].cars[0].state.location).toEqual({x:24,y:21,z:2});expect(s.mixed.queue.pending).toHaveLength(1);
  await owned(page,text);await expect(page.getByTestId('fleet-state')).toContainText(text.moving);await expect(page.getByTestId('fleet-recovery-reason')).toHaveCount(0);
  await page.screenshot({path:`${evidence}/recovery-${locale}.png`});
  await expect.poll(async()=>(await state(peer)).mixed).toEqual(s.mixed);await expect(canvas).toHaveAttribute('data-presentation','ready');expect(errors).toEqual([]);
 }finally{await context.close();}
});
async function clickCell(page:Page,x:number,y:number,z:number){
 await settled(page);
 const centerX=Math.trunc(((await state(page)).map.height-1)/2);
 const p=await page.locator('canvas').evaluate((el,a)=>{const c=el as HTMLCanvasElement,r=c.getBoundingClientRect(),[cx,cy]=c.dataset.camera!.split(',').map(Number),zoom=Number(c.dataset.zoom);return{x:r.x+r.width/2+(16*(a.x+a.y-a.centerX)+16-cx)*zoom,y:r.y+r.height/2+(8*(-a.x+a.y+a.centerX)-16*a.z+8-cy)*zoom};},{x,y,z,centerX});
 await page.mouse.click(p.x,p.y);
}
async function pickRecoveryCar(page:Page,s:any){
 const canvas=page.locator('canvas');await expect(canvas).toHaveAttribute('data-render-revision',s.revision);
 const car=s.mixed.fleet.trains[0].cars[0],p=car.state.location,center=Math.trunc((s.map.height-1)/2);
 const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
 // Straight symmetric stock: pick an actual roof-mask pixel at origin +(0,-9).
 const frame=catalog.renderRecipes.find((r:any)=>r.id===car.definition.recipeId).frames.find((f:any)=>f.selector.angle===((car.state.heading*2)&7));
 const mask=catalog.masks[frame.masks['spring/day']];let sample:{a:number;b:number}|undefined;
 for(let b=1;b<12&&!sample;b++)for(let a=5;a<27&&!sample;a++)if(mask[b][a]==='1')sample={a,b};
 expect(sample).toBeDefined();
 const q={x:16*(p.x-center+p.y)+sample!.a+.5,y:8*(p.y-p.x+center)-16*p.z-9+sample!.b+.5};
 const pan=await canvas.evaluate((el,q)=>{const r=el.getBoundingClientRect(),[x,y]=el.dataset.camera!.split(',').map(Number),z=Number(el.dataset.zoom);return Math.abs(q.x-x)*z>r.width/3||Math.abs(q.y-y)*z>r.height/3?{deltaX:(q.x-x)*z,deltaY:(q.y-y)*z,deltaMode:0}:undefined;},q);
 if(pan){const before=await canvas.getAttribute('data-camera');await canvas.dispatchEvent('wheel',pan);await expect(canvas).not.toHaveAttribute('data-camera',before!);}
 await expect(canvas).toHaveAttribute('data-presentation','ready');await settled(page);
 const point=await canvas.evaluate((el,q)=>{const r=el.getBoundingClientRect(),[x,y]=el.dataset.camera!.split(',').map(Number),z=Number(el.dataset.zoom);return{x:r.x+r.width/2+(q.x-x)*z,y:r.y+r.height/2+(q.y-y)*z};},q);
 await page.mouse.click(point.x,point.y);
}
async function coords(page:Page,text:typeof en,x=28,y=27,z=1){
 for(const [key,n] of [['X',x],['Y',y],['Z',z]] as const)await page.locator('.deployment-controls').getByLabel(key,{exact:true}).fill(String(n));
 await page.locator('.deployment-controls').getByLabel('Z',{exact:true}).press('Enter');
}
async function owned(page:Page,text:typeof en){
 await openPanel(page,'fleet',text===ja?'ja':'en');await page.getByRole('button',{name:new RegExp(`^${text.ownedStock}`)}).click();await page.locator('.catalog-item-main').first().click();
}
async function build(page:Page,text:typeof en){
 const s=await state(page),r=await page.request.post('/api/worlds/empty/preview',{data:{name:'First playable fleet',dimensions:{h:30,v:42,z:6},waterLevel:1,epoch:s.worldEpoch,revision:s.revision}});expect(r.ok()).toBe(true);
 const c=await r.json();expect((await page.request.post('/api/worlds/empty/commit',{data:{id:c.id,requestHash:c.requestHash,candidateHash:c.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).ok()).toBe(true);
 await ready(page,text===ja?'ja':'en');
 // Build the actual P4 ramp, then attach ground and elevated approaches.
 await page.getByRole('button',{name:text.slopeTask,exact:true}).click();
 for(const [name,value] of [[text.slopeX,30],[text.slopeY,27],[text.slopeLevel,1]] as const)await page.getByLabel(name,{exact:true}).fill(String(value));
 await page.getByLabel(text.slopeDirection,{exact:true}).selectOption('2');await page.getByRole('button',{name:text.slopeCheck,exact:true}).click();
 await page.getByRole('button',{name:text.slopeConfirm,exact:true}).click();await expect(page.getByTestId('slope-base')).toHaveCount(0);
 await page.getByRole('button',{name:text.trackTask,exact:true}).click();
 for(const [from,to,z] of [[27,30,1],[33,36,2]]){
  for(const [name,value] of [[text.railFromX,from],[text.railFromY,27],[text.railToX,to],[text.railToY,27],[text.buildLevel,z]] as const)await page.getByLabel(name,{exact:true}).fill(String(value));
  await page.getByRole('button',{name:text.railCheck,exact:true}).click();await page.getByRole('button',{name:text.confirmTrack,exact:true}).click();await expect(page.getByTestId('rail-route')).toHaveCount(0);
 }
 await page.getByRole('button',{name:text.referenceInspect,exact:true}).click();
}
for(const locale of ['en','ja'] as const)test(`${locale} playable purchase deploy ramp run inspect withdraw redeploy and checkpoint with a peer`,async({page,browser,baseURL,worldServer})=>{
 test.setTimeout(90000);const text=locale==='ja'?ja:en,errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 let socket:WebSocketRoute|undefined,last:any;const receipts:any[]=[];
 await page.routeWebSocket('**/api/session?*',route=>{const server=route.connectToServer();socket=server;route.onMessage(m=>{const d=JSON.parse(String(m));if(d.payload?.kind==='deployFleet')last=d;server.send(m);});server.onMessage(m=>{const d=JSON.parse(String(m));if(['accepted','rejected'].includes(d.type))receipts.push(d);route.send(m);});});
 await page.goto('/');await ready(page);await openPanel(page,'settings');await page.getByLabel('Language',{exact:true}).selectOption(locale);await closePanel(page);
 await build(page,text);
 const context=await browser.newContext({baseURL}),peer=await context.newPage();
 try{
  await peer.goto('/');await ready(peer);await openPanel(page,'fleet',locale);
  await page.getByLabel(text.stockLength,{exact:true}).selectOption('2');await page.getByRole('button',{name:text.stockQuote,exact:true}).click();
  await page.getByRole('button',{name:text.stockConfirmBuy,exact:true}).click();await page.getByRole('button',{name:new RegExp(`^${text.ownedStock}`)}).click();await page.locator('.catalog-item-main').first().click();
  const canvas=page.locator('canvas');await canvas.evaluate(el=>el.dataset.instance='deployment-preserved');const camera=await canvas.getAttribute('data-camera');const bought=await save(page);
  await coords(page,text,31,27,1);await expect(page.getByText(text.deploymentMissingRail,{exact:true})).toBeVisible();await expect(page.getByRole('button',{name:text.deploymentConfirm,exact:true})).toBeDisabled();expect(await save(page)).toEqual(bought);
  await coords(page,text);await expect(page.getByTestId('deployment-car')).toHaveCount(2);await expect(canvas).toHaveAttribute('data-fleet-ghost-cars','2');
  const first=await page.getByTestId('deployment-car').allTextContents();expect(first.every(t=>t.includes(text.headingEast))).toBe(true);
  await page.getByRole('button',{name:text.cancel,exact:true}).click();expect(await save(page)).toEqual(bought);await expect(canvas).toHaveAttribute('data-fleet-ghost-cars','0');
  await page.getByRole('button',{name:text.deploymentPick,exact:true}).click();await clickCell(page,28,27,1);await expect(page.locator('.deployment-controls').getByLabel('X',{exact:true})).toHaveValue('28');
  await expect(page.getByTestId('deployment-car')).toHaveCount(2);await expect(page.locator('.map-stage')).toHaveClass(/fleet-picking/);
  await expect(page.locator('.primary-panel')).toHaveCSS('width','352px');await expect(canvas).toHaveAttribute('data-fleet-ghost-cars','2');
  await expect(page.getByRole('button',{name:text.deploymentPreview,exact:true})).toHaveCount(0);
  await peer.getByRole('button',{name:/Single step/}).first().click();await expect(page.getByText(text.stockStale,{exact:true})).toBeVisible();await expect(page.getByRole('button',{name:text.deploymentConfirm,exact:true})).toBeDisabled();
  await page.getByRole('button',{name:text.cancel,exact:true}).click();await coords(page,text);
  await expect(page.getByTestId('deployment-car')).toHaveCount(2);await expect(canvas).toHaveAttribute('data-fleet-ghost-cars','2');await page.getByRole('button',{name:text.deploymentConfirm,exact:true}).scrollIntoViewIfNeeded();
  mkdirSync(evidence,{recursive:true});await page.screenshot({path:`${evidence}/deployment-${locale}.png`});
  await page.getByRole('button',{name:text.deploymentConfirm,exact:true}).dblclick();await expect(page.getByRole('button',{name:text.withdrawTrain,exact:true})).toBeVisible();
  let s=await state(page);expect(s.mixed.fleet.trains[0].cars.map((c:any)=>c.state.location.x)).toEqual([29,28]);expect((await state(peer)).mixed).toEqual(s.mixed);
  const deployed=await save(page),n=receipts.length;socket!.send(JSON.stringify(last));await expect.poll(()=>receipts.length).toBe(n+1);expect(receipts.at(-1).type).toBe('accepted');expect(await save(page)).toEqual(deployed);
  socket!.send(JSON.stringify({...last,requestId:'old-deploy-new-id'}));await expect.poll(()=>receipts.at(-1)?.requestId).toBe('old-deploy-new-id');expect(receipts.at(-1).code).toBe('staleRevision');
  // Actual shared Run/Pause and native callbacks cross the source ramp height.
  await peer.getByRole('button',{name:/Run/}).first().click();await expect.poll(async()=>(await state(page)).mixed.fleet.trains[0].cars.some((c:any)=>c.state.location?.z===2)).toBe(true);await peer.getByRole('button',{name:/Pause/}).first().click();
  await expect.poll(async()=>(await state(page)).paused).toBe(true);s=await state(page);
  expect(s.mixed.accounts.transactions.some((t:any)=>t.operation==='trainMove')).toBe(true);
  await closePanel(page);const car=s.mixed.fleet.trains[0].cars[0];
  // Inspect the actual car at its independently projected cell center.
  await clickCell(page,car.state.location.x,car.state.location.y,car.state.location.z);
  await expect(page.getByTestId('train-car-id')).toBeVisible();
  await expect(canvas).toHaveAttribute('data-instance','deployment-preserved');await expect(canvas).toHaveAttribute('data-camera',camera!);
  await owned(page,text);await page.getByRole('button',{name:text.withdrawTrain,exact:true}).click();const accounts=s.mixed.accounts;
  await page.getByRole('button',{name:text.withdrawConfirm,exact:true}).click();await expect(page.getByRole('button',{name:text.deploymentPick,exact:true})).toBeVisible();
  s=await state(page);expect(s.mixed.fleet.trains[0].state).toBe('unplaced');expect(s.mixed.accounts).toEqual(accounts);expect(s.mixed.queue.pending).toEqual([]);
  const expectedReversed=!s.mixed.fleet.trains[0].isReversed;await page.getByLabel(text.deploymentReverse,{exact:true}).check();await coords(page,text);await expect(page.getByTestId('deployment-car').first()).toContainText(text.headingWest);await page.getByRole('button',{name:text.deploymentConfirm,exact:true}).click();
  await expect(page.getByRole('button',{name:text.withdrawTrain,exact:true})).toBeVisible();s=await state(page);expect(s.mixed.fleet.trains[0].isReversed).toBe(expectedReversed);const saved=await save(page);
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:saved})).ok()).toBe(true);await expect.poll(async()=>(await state(page)).worldEpoch).not.toBe(s.worldEpoch);await ready(peer);
  expect(await save(page)).toEqual(saved);await worldServer.restart!();for(const p of [page,peer])expect((await p.request.post('/api/access',{data:{secret:''}})).ok()).toBe(true);await page.reload();await ready(page,locale);await peer.reload();await ready(peer);
  expect(await save(page)).toEqual(saved);expect((await state(peer)).mixed.fleet).toEqual(JSON.parse(saved.toString()).mixed.fleet);
  const before=(await state(page)).tick;await peer.getByRole('button',{name:/Single step/}).first().click();await peer.getByRole('button',{name:/Single step/}).first().click();await expect.poll(async()=>BigInt((await state(page)).tick)).toBe(BigInt(before)+2n);
  expect((await state(page)).mixed.fleet.trains[0].moveCount).toBeGreaterThan(s.mixed.fleet.trains[0].moveCount);expect(errors).toEqual([]);
 }finally{await context.close();}
});

test('Japanese narrow deployment keeps drafts and focus, discards late previews and disables observer mutations',async({page,browser,baseURL})=>{
 const initial=await state(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+initial.worldEpoch,{data:readFileSync('../prototype/protocol/saves/fleet-v9.json')})).ok()).toBe(true);
 await page.setViewportSize({width:390,height:844});await page.goto('/');await ready(page);await openPanel(page,'settings');await page.getByLabel('Language',{exact:true}).selectOption('ja');await openPanel(page,'fleet','ja');
 await page.getByRole('button',{name:/^保有車両/}).click();await page.locator('.catalog-item-main').last().click();await page.locator('.primary-panel .sheet-toggle').click();
 await page.locator('html').evaluate(el=>el.style.fontSize='200%');
 const before=await save(page);
 await page.getByLabel(ja.stockName,{exact:true}).fill('編集中');await expect(page.getByRole('button',{name:ja.deploymentPick,exact:true})).toBeDisabled();
 await page.getByRole('button',{name:ja.resetDraft,exact:true}).click();
 for(const [key,n] of [['X',3],['Y',2],['Z',2]] as const)await page.locator('.deployment-controls').getByLabel(key,{exact:true}).fill(String(n));
 await page.locator('.deployment-controls').getByLabel('Z',{exact:true}).press('Enter');await expect(page.getByTestId('deployment-car')).toHaveCount(1);
 await page.getByRole('button',{name:ja.deploymentConfirm,exact:true}).scrollIntoViewIfNeeded();
 const box=await page.locator('.deployment-review').boundingBox();expect(box!.x).toBeGreaterThanOrEqual(0);expect(box!.x+box!.width).toBeLessThanOrEqual(390);
 mkdirSync(evidence,{recursive:true});await page.screenshot({path:`${evidence}/deployment-ja-narrow.png`});
 await page.getByRole('button',{name:ja.cancel,exact:true}).click();await expect(page.locator('.deployment-controls').getByLabel('Z',{exact:true})).toBeFocused();expect(await save(page)).toEqual(before);
 let release!:()=>void;const held=new Promise<void>(r=>release=r);let requested=false;
 await page.route('**/api/fleet/deployment/preview?*',async route=>{const response=await route.fetch();requested=true;await held;try{await route.fulfill({response});}catch{/* Request was intentionally aborted. */}});
 await page.locator('.deployment-controls').getByLabel('Z',{exact:true}).press('Enter');await expect.poll(()=>requested).toBe(true);await page.keyboard.press('Escape');release();
 await expect(page.getByTestId('deployment-car')).toHaveCount(0);await expect(page.locator('canvas')).toHaveAttribute('data-fleet-ghost-cars','0');expect(await save(page)).toEqual(before);
 const context=await browser.newContext({baseURL}),observer=await context.newPage();
 try{
  expect((await observer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);await observer.goto('/');await ready(observer);await owned(observer,en);
  await expect(observer.getByRole('button',{name:en.withdrawTrain,exact:true})).toBeDisabled();await expect(observer.getByRole('button',{name:en.fleetReverse,exact:true})).toBeDisabled();await observer.locator('.catalog-item-main').last().click();await expect(observer.locator('.deployment-controls').getByLabel('X',{exact:true})).toBeDisabled();
  await expect(observer.getByRole('button',{name:en.deploymentPick,exact:true})).toBeDisabled();expect(await save(page)).toEqual(before);
 }finally{await context.close();release();}
});

for(const locale of ['en','ja'] as const)test(`P5-15 ${locale} equal-minute traffic and inspector operations stay coherent with a peer`,async({page,browser,baseURL,worldServer})=>{
 test.setTimeout(90000);const input=process.env.FREETRAIN_OPERATIONS_FLEET_OUTPUT;
 test.skip(!input,'Set FREETRAIN_OPERATIONS_FLEET_OUTPUT to the gameplay-built FleetOperationsTests save.');
 const text=locale==='ja'?ja:en,initial=await state(page),data=readFileSync(input!);
 expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+initial.worldEpoch,{data})).ok()).toBe(true);
 const context=await browser.newContext({baseURL,locale:locale==='en'?'ja-JP':'en-US'}),peer=await context.newPage();
 const errors:string[]=[],messages:any[][]=[[],[]];
 for(const [i,p] of [page,peer].entries()){
  p.on('pageerror',e=>errors.push(String(e)));
  await p.routeWebSocket('**/api/session?*',route=>{const server=route.connectToServer();server.onMessage(m=>{messages[i].push(JSON.parse(String(m)));route.send(m);});route.onMessage(m=>server.send(m));});
 }
 const latest=(i:number)=>messages[i].filter(m=>m.type==='snapshot'||m.type==='delta').at(-1);
 try{
  await page.goto('/');await ready(page);await openPanel(page,'settings');await page.getByLabel('Language',{exact:true}).selectOption(locale);await closePanel(page);
  await peer.goto('/');await ready(peer,locale==='en'?'ja':'en');await owned(page,text);
  const canvas=page.locator('canvas');await canvas.evaluate(el=>el.dataset.instance='operations-preserved');
  async function step(){const tick=BigInt((await state(page)).tick)+1n;await peer.getByRole('button',{name:locale==='en'?/1ステップ/:/Single step/}).first().click();for(const i of [0,1])await expect.poll(()=>latest(i)?.tick).toBe(String(tick));}
  await step();await step();let s=await state(page);
  expect(s.mixed.fleet.trains.map((t:any)=>t.moveCount)).toEqual([0,1]);
  expect(s.mixed.fleet.trains.map((t:any)=>t.cars[0].state.location.x)).toEqual([20,22]);
  await expect(page.getByTestId('fleet-state')).toContainText(text.fleetEmergency);
  // The head cell has just been vacated; feedback describes the current snapshot.
  await expect(page.getByTestId('fleet-recovery-reason')).toContainText(text.fleetRetry);
  for(const i of [0,1])expect(latest(i).mixed).toEqual(s.mixed);
  await step();await step();s=await state(page);
  expect(s.mixed.fleet.trains.map((t:any)=>t.moveCount)).toEqual([1,2]);
  expect(s.mixed.fleet.trains.map((t:any)=>t.cars[0].state.location.x)).toEqual([21,23]);
  expect(s.mixed.queue.pending.map((a:any)=>a.order)).toEqual(['4','5']);
  await expect(page.getByTestId('fleet-state')).toContainText(text.moving);
  await closePanel(page);await pickRecoveryCar(page,s);
  await expect(page.getByTestId('train-car-id')).toHaveText(s.mixed.fleet.trains[0].cars[0].id);
  const reverse=page.getByRole('button',{name:text.fleetReverse,exact:true});await reverse.click();
  await step();await expect(page.getByText(text.stockStale,{exact:true})).toBeVisible();
  await expect(page.getByRole('button',{name:text.fleetReverseConfirm,exact:true})).toBeDisabled();
  await page.getByRole('button',{name:text.cancel,exact:true}).click();await expect(reverse).toBeFocused();
  s=await state(page);await reverse.click();await page.getByRole('button',{name:text.fleetReverseConfirm,exact:true}).click();
  await expect.poll(async()=>(await state(page)).mixed.fleet.trains[0].isReversed).toBe(true);
  let current=await state(page);expect(current.mixed.queue).toEqual(s.mixed.queue);expect(current.mixed.accounts).toEqual(s.mixed.accounts);
  await step();current=await state(page);
  expect(current.mixed.fleet.trains.map((t:any)=>t.cars[0].state.location.x)).toEqual([20,24]);
  expect(current.mixed.fleet.trains.map((t:any)=>t.moveCount)).toEqual([2,3]);
  for(const i of [0,1])expect(latest(i).mixed).toEqual(current.mixed);
  if(locale==='ja'){
   await page.setViewportSize({width:390,height:844});await page.locator('.primary-panel .sheet-toggle').click();
   await page.locator('html').evaluate(el=>el.style.fontSize='200%');
   await page.getByRole('button',{name:text.withdrawTrain,exact:true}).scrollIntoViewIfNeeded();
   expect(await page.locator('.primary-panel').evaluate(el=>el.scrollWidth<=el.clientWidth+1)).toBe(true);
  }
  const neighbor=current.mixed.queue.pending.find((a:any)=>a.handlerId.endsWith('/'+current.mixed.fleet.trains[1].id));
  await page.getByRole('button',{name:text.withdrawTrain,exact:true}).click();await page.getByRole('button',{name:text.withdrawConfirm,exact:true}).click();
  await expect.poll(async()=>(await state(page)).mixed.fleet.trains[0].state).toBe('unplaced');current=await state(page);
  expect(current.mixed.queue.pending).toEqual([neighbor]);expect(current.mixed.accounts).toEqual(s.mixed.accounts);
  expect(current.mixed.voxels.filter((v:any)=>v.traffic?.car)).toHaveLength(1);
  const saved=await save(page);await peer.reload();await ready(peer,locale==='en'?'ja':'en');
  expect((await state(peer)).mixed).toEqual(current.mixed);await expect(canvas).toHaveAttribute('data-instance','operations-preserved');
  await worldServer.restart!();for(const p of [page,peer])expect((await p.request.post('/api/access',{data:{secret:''}})).ok()).toBe(true);
  await page.reload();await ready(page,locale);expect(await save(page)).toEqual(saved);
  // Independent source return input: the first connected return is occupied,
  // with a free alternate. Feedback must retain that source precedence.
  current=await state(page);
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+current.worldEpoch,{data:readFileSync(input!+'.return')})).ok()).toBe(true);
  await owned(page,text);await expect(page.getByTestId('fleet-state')).toContainText(text.fleetForwarding);
  await expect(page.getByTestId('fleet-recovery-reason')).toContainText(text.fleetOccupiedReturn);
  expect((await peer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);
  await peer.reload();await ready(peer,locale==='en'?'ja':'en');const peerText=locale==='en'?ja:en;
  current=await state(peer);const selected=structuredClone(current);selected.mixed.fleet.trains=[current.mixed.fleet.trains[1]];
  await pickRecoveryCar(peer,selected);
  await expect(peer.getByRole('button',{name:peerText.fleetReverse,exact:true})).toBeDisabled();
  await expect(peer.getByRole('button',{name:peerText.withdrawTrain,exact:true})).toBeDisabled();expect(errors).toEqual([]);
 }finally{await context.close();}
});
