import {test,expect,type Page,type WebSocketRoute} from './fixtures.ts';
import {canonicalState as state,closePanel,openPanel,ready} from './browser-helpers.ts';
import {settled} from './layout-helpers.ts';
import {mkdirSync,readFileSync} from 'node:fs';
const en=JSON.parse(readFileSync('src/locales/en/app.json','utf8'));
const ja=JSON.parse(readFileSync('src/locales/ja/app.json','utf8'));
const evidence=process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!;
const save=async(p:Page)=>(await p.request.get('/api/saves/download')).body();
async function clickCell(page:Page,x:number,y:number,z:number){
 await settled(page);
 const p=await page.locator('canvas').evaluate((el,a)=>{const c=el as HTMLCanvasElement,r=c.getBoundingClientRect(),[cx,cy]=c.dataset.camera!.split(',').map(Number),zoom=Number(c.dataset.zoom);return{x:r.x+r.width/2+(16*(a.x+a.y-20)+16-cx)*zoom,y:r.y+r.height/2+(8*(-a.x+a.y+20)-16*a.z+8-cy)*zoom};},{x,y,z});
 await page.mouse.click(p.x,p.y);
}
async function coords(page:Page,text:typeof en,x=28,y=27,z=1){
 for(const [key,n] of [['X',x],['Y',y],['Z',z]] as const)await page.locator('.deployment-controls').getByLabel(key,{exact:true}).fill(String(n));
 await page.getByRole('button',{name:text.deploymentPreview,exact:true}).click();
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
  await page.getByRole('button',{name:text.deploymentPreview,exact:true}).click();await expect(page.getByTestId('deployment-car')).toHaveCount(2);
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
  await page.getByRole('button',{name:text.withdrawConfirm,exact:true}).click();await expect(page.getByRole('button',{name:text.deploymentPreview,exact:true})).toBeVisible();
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
 await page.getByLabel(ja.stockName,{exact:true}).fill('編集中');await expect(page.getByRole('button',{name:ja.deploymentPreview,exact:true})).toBeDisabled();
 await page.getByRole('button',{name:ja.resetDraft,exact:true}).click();
 for(const [key,n] of [['X',3],['Y',2],['Z',2]] as const)await page.locator('.deployment-controls').getByLabel(key,{exact:true}).fill(String(n));
 await page.locator('.deployment-controls').getByLabel('Z',{exact:true}).press('Enter');await expect(page.getByTestId('deployment-car')).toHaveCount(1);
 await page.getByRole('button',{name:ja.deploymentConfirm,exact:true}).scrollIntoViewIfNeeded();
 const box=await page.locator('.deployment-review').boundingBox();expect(box!.x).toBeGreaterThanOrEqual(0);expect(box!.x+box!.width).toBeLessThanOrEqual(390);
 mkdirSync(evidence,{recursive:true});await page.screenshot({path:`${evidence}/deployment-ja-narrow.png`});
 await page.getByRole('button',{name:ja.cancel,exact:true}).click();await expect(page.locator('.deployment-controls').getByLabel('Z',{exact:true})).toBeFocused();expect(await save(page)).toEqual(before);
 let release!:()=>void;const held=new Promise<void>(r=>release=r);let requested=false;
 await page.route('**/api/fleet/deployment/preview?*',async route=>{const response=await route.fetch();requested=true;await held;try{await route.fulfill({response});}catch{/* Request was intentionally aborted. */}});
 await page.getByRole('button',{name:ja.deploymentPreview,exact:true}).click();await expect.poll(()=>requested).toBe(true);await page.keyboard.press('Escape');release();
 await expect(page.getByTestId('deployment-car')).toHaveCount(0);await expect(page.locator('canvas')).toHaveAttribute('data-fleet-ghost-cars','0');expect(await save(page)).toEqual(before);
 const context=await browser.newContext({baseURL}),observer=await context.newPage();
 try{
  expect((await observer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);await observer.goto('/');await ready(observer);await owned(observer,en);
  await expect(observer.getByRole('button',{name:en.withdrawTrain,exact:true})).toBeDisabled();await observer.locator('.catalog-item-main').last().click();await expect(observer.getByRole('button',{name:en.deploymentPreview,exact:true})).toBeDisabled();
  await expect(observer.getByRole('button',{name:en.deploymentPick,exact:true})).toBeDisabled();expect(await save(page)).toEqual(before);
 }finally{await context.close();release();}
});
