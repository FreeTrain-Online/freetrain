import {test,expect,type Page,type WebSocketRoute} from './fixtures.ts';
import {canonicalState as state,openPanel,closePanel,ready} from './browser-helpers.ts';
import {readFileSync,mkdirSync} from 'node:fs';
import {settled} from './layout-helpers.ts';
import {validTrainMotion} from '../../src/lib/train-motion-contract.ts';
const en=JSON.parse(readFileSync('src/locales/en/app.json','utf8'));
const ja=JSON.parse(readFileSync('src/locales/ja/app.json','utf8'));
const evidence=process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!;
test.use({hasTouch:true});
async function owned(page:Page,text:typeof en,locale='en'){
  await openPanel(page,'fleet',locale);await page.getByRole('button',{name:new RegExp(`^${text.ownedStock}`)}).click();
  await page.locator('.fleet-owned .catalog-item-main').first().click();
}
async function pickTrain(page:Page){
  await closePanel(page);const read=await state(page),car=read.mixed.fleet.trains[0].cars[0],p=car.state.location;
  const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
  const frame=catalog.renderRecipes.find((r:any)=>r.id===car.definition.recipeId).frames.find((f:any)=>f.selector.angle===((car.state.heading*2)&7));
  const mask=catalog.masks[frame.masks['spring/day']];let sample:{a:number;b:number}|undefined;
  for(let b=1;b<12&&!sample;b++)for(let a=5;a<27&&!sample;a++)if(mask[b][a]==='1')sample={a,b};
  expect(sample).toBeDefined();const center=Math.trunc((read.map.height-1)/2);
  const q={x:16*(p.x-center+p.y)+sample!.a+.5,y:8*(p.y-p.x+center)-16*p.z-9+sample!.b+.5};
  const canvas=page.locator('canvas.world-canvas');
  const pan=await canvas.evaluate((el,q)=>{const [x,y]=el.dataset.camera!.split(',').map(Number),z=Number(el.dataset.zoom);return {deltaX:(q.x-x)*z,deltaY:(q.y-y)*z,deltaMode:0};},q);
  await canvas.dispatchEvent('wheel',pan);await settled(page);
  const point=await canvas.evaluate((el,q)=>{const r=el.getBoundingClientRect(),[x,y]=el.dataset.camera!.split(',').map(Number),z=Number(el.dataset.zoom);return{x:r.x+r.width/2+(q.x-x)*z,y:r.y+r.height/2+(q.y-y)*z};},q);
  await page.mouse.click(point.x,point.y);
}
for(const right of [false,true])for(const smooth of [true,false])test(`held ${right?'Right':'Left'} crosses a manual junction without reconnect (${smooth?'smooth':'discrete'})`,async({page,browser,baseURL})=>{
  test.setTimeout(60000);const errors:string[]=[],packets:any[]=[];let connections=0,peerConnections=0;
  page.on('pageerror',e=>errors.push(String(e)));
  const initial=await state(page),path=(process.env.FREETRAIN_MANUAL_JUNCTION_OUTPUT??'../tmp/manual-junction-fix.json')+(right?'.right':'');
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+initial.worldEpoch,{data:readFileSync(path)})).ok()).toBe(true);
  await page.routeWebSocket('**/api/session?*',route=>{connections++;const server=route.connectToServer();
    server.onMessage(m=>{const d=JSON.parse(String(m));if(d.type==='delta'||d.type==='snapshot')packets.push(d);route.send(m);});});
  const url=smooth?'/':'/?smoothTrains=0';await page.goto(url);await ready(page);
  await page.getByRole('combobox',{name:en.gameSpeed,exact:true}).selectOption('0.125');
  await expect(page.locator('.command-feedback')).toHaveText(en.accepted);
  const context=await browser.newContext({baseURL}),peer=await context.newPage();
  try{
    peer.on('pageerror',e=>errors.push(String(e)));
    await peer.routeWebSocket('**/api/session?*',route=>{peerConnections++;route.connectToServer();});
    expect((await peer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);
    await peer.goto(url);await ready(peer);
    await owned(page,en);await page.getByRole('button',{name:en.takeControl,exact:true}).click();
    await expect(page.locator('.command-feedback')).toHaveText(en.accepted);
    const lease=(await state(page)).fleetControl[0];await pickTrain(page);await closePanel(page);
    const canvas=page.locator('canvas.world-canvas'),key=right?'ArrowRight':'ArrowLeft';await canvas.focus();await page.keyboard.down(key);
    await expect(page.getByTestId('manual-input')).toHaveText(key);await page.keyboard.press('Space');
    await expect.poll(async()=>(await state(page)).mixed.fleet.trains[0].moveCount,{timeout:15000}).toBeGreaterThanOrEqual(3);
    await page.keyboard.press('Space');await page.keyboard.up(key);
    await expect.poll(async()=>(await state(page)).paused).toBe(true);
    for(const p of [page,peer]){await ready(p);await expect(p.locator('canvas.world-canvas')).toHaveAttribute('data-presentation','ready');await expect(p.locator('canvas.world-canvas')).not.toHaveAttribute('data-assets','failed');}
    const current=await state(page),train=current.mixed.fleet.trains[0];
    expect(current.fleetControl[0].lease.leaseId).toBe(lease.lease.leaseId);
    expect(train.cars.some((c:any)=>c.state.location.y===(right?19:21))).toBe(true);
    const moves=packets.flatMap(p=>p.trainMotion?.steps??[]),turn=moves.flatMap((s:any)=>s.cars).find((c:any)=>c.from.position.x===22&&c.from.position.y===20);
    expect(turn).toBeDefined();expect(turn.from.outgoing).toBe(right?7:5);expect(turn.to.heading).toBe(right?7:5);
    for(const p of packets)expect(validTrainMotion(p.trainMotion,p.tick)).toBe(true);
    expect(connections).toBe(1);expect(peerConnections).toBe(1);expect(errors).toEqual([]);
    await pickTrain(page);await expect(page.getByTestId('train-car-id')).toBeVisible();
  }finally{await context.close();}
});
test('idle focused manual controls and map retain ownership; blur still expires it',async({page})=>{
  test.setTimeout(60000);
  const initial=await state(page),input=JSON.parse(readFileSync(process.env.FREETRAIN_MANUAL_FLEET_OUTPUT??'../tmp/p5-17-manual-fleet.json','utf8'));
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+initial.worldEpoch,{data:JSON.stringify(input)})).ok()).toBe(true);
  await page.goto('/');await ready(page);await owned(page,en);
  const controls=page.locator('.fleet-manual-controls');await controls.getByRole('button',{name:en.takeControl,exact:true}).click();
  await expect(page.locator('.command-feedback')).toHaveText(en.accepted);
  const lease=(await state(page)).fleetControl[0];
  // Reproduce taking most of the original lease to choose a controller. Real
  // server wall time must advance; browser fake timers cannot cover expiry.
  await page.waitForTimeout(4100);
  await controls.getByRole('combobox').selectOption('manual');await expect(page.locator('.command-feedback')).toHaveText(en.accepted);
  await expect(controls.getByRole('combobox')).toBeFocused();
  await page.waitForTimeout(6200);
  await expect(controls.getByRole('button',{name:en.manualDepart,exact:true})).toBeEnabled();
  let current=await state(page);expect(current.fleetControl[0].lease.leaseId).toBe(lease.lease.leaseId);
  expect(current.mixed.fleet.trains[0].moveCount).toBe(0);expect(current.tick).toBe(input.clock.absoluteMinute);
  // Switch surfaces without pressing any driving key. Idle ownership must
  // remain live on the focused map too, using the same lease generation.
  await pickTrain(page);await closePanel(page);const canvas=page.locator('canvas.world-canvas');await canvas.focus();
  await page.waitForTimeout(6200);current=await state(page);
  expect(current.fleetControl[0].lease.leaseId).toBe(lease.lease.leaseId);
  await expect(page.getByTestId('manual-input')).toHaveText(en.keysReleased);
  await page.evaluate(()=>window.dispatchEvent(new Event('blur')));
  await expect.poll(async()=>(await state(page)).fleetControl,{timeout:7000}).toEqual([]);
  await canvas.focus();await page.keyboard.press('ArrowUp');expect((await state(page)).fleetControl).toEqual([]);
});
for(const locale of ['en','ja'] as const)test(`${locale} canonical manual signal driving, peer contention, resets and reopen`,async({page,browser,baseURL,worldServer})=>{
  test.setTimeout(90000);const text=locale==='ja'?ja:en,errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
  const initial=await state(page),input=JSON.parse(readFileSync(process.env.FREETRAIN_MANUAL_FLEET_OUTPUT??'../tmp/p5-17-manual-fleet.json','utf8'));
  const result=await page.request.post('/api/saves/upload?expectedWorldEpoch='+initial.worldEpoch,{data:JSON.stringify(input)});expect(result.ok(),await result.text()).toBe(true);
  let socket:WebSocketRoute|undefined,holdAcquire=false,deferredAcquire:string|Buffer|undefined;const inputs:any[]=[],receipts:any[]=[],controllerChanges:any[]=[];
  await page.routeWebSocket('**/api/session?*',route=>{const server=route.connectToServer();socket=server;
    route.onMessage(m=>{const d=JSON.parse(String(m));if(d.payload?.kind==='controllerInput')inputs.push(d);
      if(d.payload?.kind==='setController')controllerChanges.push(d);
      if(holdAcquire&&d.payload?.kind==='acquireControl'){deferredAcquire=m;return;}server.send(m);});
    server.onMessage(m=>{const d=JSON.parse(String(m));if(['accepted','rejected'].includes(d.type))receipts.push(d);route.send(m);});});
  await page.goto('/');await ready(page);await openPanel(page,'settings');await page.getByLabel('Language',{exact:true}).selectOption(locale);await closePanel(page);
  const peerContext=await browser.newContext({baseURL}),peer=await peerContext.newPage();
  try{
    expect((await peer.request.post('/api/access',{data:{secret:'operator-test'}})).ok()).toBe(true);await peer.goto('/');await ready(peer);await owned(peer,en);
    if(locale==='ja'){await page.setViewportSize({width:390,height:844});await page.evaluate(()=>document.documentElement.style.fontSize='200%');}
    await owned(page,text,locale);const controls=page.locator('.fleet-manual-controls');
    await controls.getByRole('combobox',{name:text.controller,exact:true}).selectOption('manual');
    await expect(page.locator('.command-feedback')).toHaveText(text.accepted);
    await expect(controls.getByTestId('fleet-control-holder')).not.toContainText(text.unclaimed);
    await expect(peer.locator('.fleet-manual-controls select')).toHaveValue('manual');
    await peer.locator('.fleet-owned .catalog-item-main').nth(1).click();
    await peer.getByRole('combobox',{name:en.controller,exact:true}).selectOption('manual');
    await expect(peer.locator('.command-feedback')).toHaveText(en.accepted);
    await peer.locator('.fleet-owned .catalog-item-main').first().click();
    await controls.getByRole('button',{name:text.takeControl,exact:true}).click();
    await peer.getByRole('button',{name:en.takeControl,exact:true}).click();await expect(peer.locator('.command-feedback')).toHaveText(en.controlBusy);
    async function steps(count:number){for(let n=0;n<count;n++){
      const before=await state(page),requestId=crypto.randomUUID();socket!.send(JSON.stringify({type:'command',requestId,expectedWorldEpoch:before.worldEpoch,payload:{kind:'step'}}));
      await expect.poll(()=>receipts.find(r=>r.requestId===requestId)?.type).toBe('accepted');
    }}
    await steps(2);let current=await state(page);expect(current.mixed.fleet.trains[0].stopCallCount).toBe(1);
    // Hold a real pointer on the touch-sized control through authoritative callbacks.
    await controls.getByRole('button',{name:text.takeControl,exact:true}).click();
    const depart=controls.getByRole('button',{name:text.manualDepart,exact:true});await depart.scrollIntoViewIfNeeded();
    const box=(await depart.boundingBox())!;await page.mouse.move(box.x+box.width/2,box.y+box.height/2);await page.mouse.down();
    await expect(controls.getByTestId('fleet-manual-input')).toHaveText('ArrowUp');await steps(5);await page.mouse.up();
    current=await state(page);expect(current.mixed.fleet.trains[0].cars[0].state.location.x).toBe(23);
    expect(current.mixed.fleet.trains[0].stopCallCount).toBe(0);expect(current.mixed.fleet.trains[1].moveCount).toBe(0);
    await expect(controls.getByTestId('fleet-manual-input')).toHaveText(text.keysReleased);
    // Lost acknowledgements replay the receipt, never the held input after reset.
    const old=inputs.find(d=>d.payload.depart&&!d.payload.reset)!;expect(old).toBeTruthy();
    await expect.poll(()=>inputs.at(-1)?.payload.reset).toBe(true);const savedSequence=inputs.at(-1).payload.inputSequence;
    socket!.send(JSON.stringify(old));await expect.poll(()=>receipts.filter(r=>r.requestId===old.requestId).length).toBeGreaterThan(1);
    await expect(controls.getByTestId('fleet-manual-input')).toHaveText(text.keysReleased);
    const right=controls.getByRole('button',{name:text.manualRight,exact:true});await right.focus();await page.keyboard.down('ArrowRight');
    await expect(controls.getByTestId('fleet-manual-input')).toHaveText('ArrowRight');
    await page.keyboard.down('Control');await page.keyboard.up('ArrowRight');
    await expect(controls.getByTestId('fleet-manual-input')).toHaveText(text.keysReleased);await page.keyboard.up('Control');
    await page.keyboard.down('ArrowRight');await expect(controls.getByTestId('fleet-manual-input')).toHaveText('ArrowRight');
    await page.evaluate(()=>window.dispatchEvent(new Event('blur')));await expect(controls.getByTestId('fleet-manual-input')).toHaveText(text.keysReleased);await page.keyboard.up('ArrowRight');
    expect(BigInt(inputs.at(-1).payload.inputSequence)).toBeGreaterThan(BigInt(savedSequence));
    await pickTrain(page);await expect(page.getByTestId('train-car-id')).toBeVisible();
    await page.getByRole('button',{name:text.takeControl,exact:true}).click();await closePanel(page);
    const canvas=page.locator('canvas.world-canvas');await canvas.focus();await page.keyboard.down('ArrowLeft');
    await expect(page.getByTestId('manual-input')).toHaveText('ArrowLeft');
    await page.evaluate(()=>window.dispatchEvent(new Event('blur')));await expect(page.getByTestId('manual-input')).toHaveText(text.keysReleased);await page.keyboard.up('ArrowLeft');
    await canvas.focus();await page.keyboard.press('Control+ArrowRight');await expect(page.getByTestId('manual-input')).toHaveText(text.keysReleased);
    await owned(page,text,locale);
    await expect(page.getByText(text.deploymentControllerUnavailable,{exact:true})).toHaveCount(0);
    await controls.getByTestId('fleet-manual-input').scrollIntoViewIfNeeded();
    mkdirSync(evidence,{recursive:true});await page.screenshot({path:`${evidence}/manual-${locale}.png`,fullPage:true});
    await controls.getByRole('button',{name:text.releaseControl,exact:true}).click();
    await peer.getByRole('button',{name:en.takeControl,exact:true}).click();await expect(peer.locator('.command-feedback')).toHaveText(en.accepted);
    await expect(controls.getByRole('button',{name:text.manualDepart,exact:true})).toBeDisabled();
    await peer.getByRole('button',{name:en.releaseControl,exact:true}).click();
    // Selection can change while a lease acknowledgement is pending. An old
    // selector must not apply its controller change after its component closes.
    const changes=controllerChanges.length;holdAcquire=true;
    await controls.getByRole('combobox').selectOption('automatic');await expect.poll(()=>!!deferredAcquire).toBe(true);
    await page.locator('.fleet-owned .catalog-item-main').nth(1).click();holdAcquire=false;socket!.send(deferredAcquire!);
    await expect(page.locator('.command-feedback')).toHaveText(text.accepted);
    expect(controllerChanges.length).toBe(changes);
    const afterSelection=await state(page);expect(afterSelection.mixed.fleet.controllers.find((c:any)=>c.id===afterSelection.mixed.fleet.trains[0].controllerId).kind).toBe('manual');
    await page.locator('.fleet-owned .catalog-item-main').first().click();
    const observerContext=await browser.newContext({baseURL});
    try{
      const observer=await observerContext.newPage();expect((await observer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);
      await observer.goto('/');await ready(observer);await owned(observer,en);
      const readonly=observer.locator('.fleet-manual-controls');
      await expect(readonly.getByRole('combobox')).toBeDisabled();
      for(const name of [en.takeControl,en.releaseControl,en.manualLeft,en.manualRight,en.manualPass,en.manualDepart,en.manualReverse])
        await expect(readonly.getByRole('button',{name,exact:true})).toBeDisabled();
    }finally{await observerContext.close();}
    // Reopen and actual process restart preserve assignment and clear all leases.
    const downloaded=await(await page.request.get('/api/saves/download')).body();expect(downloaded.toString()).not.toContain('leaseId');
    const epoch=(await state(page)).worldEpoch;
    expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+epoch,{data:downloaded})).ok()).toBe(true);
    await expect.poll(async()=>(await state(page)).worldEpoch).not.toBe(epoch);
    expect((await state(page)).fleetControl).toEqual([]);
    await worldServer.restart!();expect((await page.request.post('/api/access',{data:{secret:''}})).ok()).toBe(true);
    await page.reload();await ready(page,locale);await owned(page,text,locale);
    await expect(page.locator('.fleet-manual-controls select')).toHaveValue('manual');
    await expect(page.getByTestId('fleet-control-holder')).toContainText(text.unclaimed);
    expect(errors).toEqual([]);
  }finally{await peerContext.close();}
});
