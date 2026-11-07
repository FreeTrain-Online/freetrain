import { writeFile } from 'node:fs/promises';
import { at,bootstrap,clickSprite,clickTile,closePanel,evidenceDirectory,openPanel,ready,selectStation,selectTrain,worldReplacementFixture } from './browser-helpers.ts';
import { settled } from './layout-helpers.ts';
import { expect,test } from './fixtures.ts';

test('map clock shortcuts toggle on Space press and step on N without disrupting panning or text',async({page})=>{
  const commands:string[]=[];
  await page.routeWebSocket('**/api/session?*',route=>{
    const server=route.connectToServer();
    route.onMessage(message=>{const data=JSON.parse(String(message));if(data.type==='command')commands.push(data.payload.kind);server.send(message);});
  });
  await page.goto('/');await ready(page);
  if(!(await bootstrap(page)).paused)await page.getByRole('button',{name:'Pause',exact:false}).click();
  const canvas=page.locator('canvas');await canvas.focus();
  const tick=BigInt((await bootstrap(page)).tick);
  await page.keyboard.press('n');
  await expect.poll(async()=>BigInt((await bootstrap(page)).tick)).toBe(tick+1n);
  await expect(canvas).toBeFocused();
  await page.keyboard.down('Space');await page.keyboard.down('Space');
  await expect(page.getByTestId('clock-state')).toHaveText('Running');
  await page.keyboard.up('Space');
  await expect(page.getByTestId('clock-state')).toHaveText('Running');
  expect(commands.filter(kind=>kind==='run')).toHaveLength(1);
  const steps=commands.filter(kind=>kind==='step').length;
  await page.keyboard.press('n');await expect(canvas).toBeFocused();
  await page.keyboard.press('Tab');await expect(canvas).not.toBeFocused();
  expect(commands.filter(kind=>kind==='step')).toHaveLength(steps);
  await canvas.focus();await page.keyboard.press('Space');
  await expect(page.getByTestId('clock-state')).toHaveText('Paused');
  const count=commands.length,camera=await canvas.getAttribute('data-camera'),rect=(await canvas.boundingBox())!;
  await page.mouse.move(rect.x+rect.width/2,rect.y+rect.height/2);await page.mouse.down();
  await page.mouse.move(rect.x+rect.width/2+60,rect.y+rect.height/2+20);await page.mouse.up();
  await expect(canvas).not.toHaveAttribute('data-camera',camera!);
  await page.keyboard.press('Control+Space');await page.keyboard.press('Tab');
  await expect(canvas).not.toBeFocused();
  expect(commands).toHaveLength(count);
  await selectStation(page);
  const draft=page.getByLabel('Station name',{exact:true});await draft.focus();
  await page.keyboard.press('Space');await page.keyboard.press('n');await page.keyboard.press('Tab');
  expect(commands).toHaveLength(count);
  await page.getByRole('button',{name:'Reset draft',exact:true}).click();
  await openPanel(page,'help');
  await expect(page.locator('.primary-panel')).toContainText('press N to advance a single step while paused');
});

test('source sprites pick real buildings and station; camera, cut and display effects stay local',async({page})=>{
  const errors:string[]=[];page.on('pageerror',error=>errors.push(error.message));
  await page.goto('/');await ready(page);
  const before=await bootstrap(page);
  await clickSprite(page,{x:34,y:22,z:2},'condo-top');
  await expect(page.getByTestId('selected-object')).toContainText('building-2');
  await expect(page.locator('.primary-panel')).toContainText('Height');
  await closePanel(page);
  await clickSprite(page,{x:16,y:16,z:0},'pier');
  await expect(page.getByTestId('selected-object')).toContainText('building-1');
  await closePanel(page);
  const station=await at(page,{x:20,y:8,z:1},{x:0,y:-12});await page.mouse.click(station.x,station.y);
  await expect(page.getByTestId('map-selection')).toContainText('Station');
  await clickTile(page,{x:20,y:8,z:1});
  await expect(page.getByTestId('map-selection')).toContainText('Track');
  await closePanel(page);
  const canvas=page.locator('canvas'), camera=await canvas.getAttribute('data-camera');
  const rect=(await canvas.boundingBox())!;
  await page.mouse.move(rect.x+rect.width/2,rect.y+rect.height/2);await page.mouse.down();await page.mouse.move(rect.x+rect.width/2+70,rect.y+rect.height/2+25);await page.mouse.up();
  await expect(canvas).not.toHaveAttribute('data-camera',camera!);
  const pannedCamera=await canvas.getAttribute('data-camera');
  const zoom=await canvas.getAttribute('data-zoom');await page.getByRole('button',{name:'Zoom in',exact:true}).click();await expect(canvas).not.toHaveAttribute('data-zoom',zoom!);
  await page.getByRole('button',{name:'Reset camera',exact:true}).click();
  await expect(canvas).toHaveAttribute('data-zoom','1.500');
  await expect(canvas).toHaveAttribute('data-camera',pannedCamera!);
  await openPanel(page, 'view');
  await page.getByLabel('Night',{exact:true}).check();

  await page.getByLabel('Night',{exact:true}).uncheck();await page.getByLabel('Recolor',{exact:true}).check();await page.getByLabel('Winter ground',{exact:true}).check();

  await page.getByLabel('Height cut',{exact:true}).focus();await page.keyboard.press('Home');await expect(page.getByLabel('Height cut',{exact:true})).toHaveValue('0');
  await closePanel(page);await clickTile(page,{x:34,y:22,z:0});await expect(page.getByTestId('selected-object')).toContainText('building-2');

  await openPanel(page, 'view');
  await page.getByLabel('Height cut',{exact:true}).focus();await page.keyboard.press('End');await page.getByLabel('Winter ground',{exact:true}).uncheck();await page.getByLabel('Recolor',{exact:true}).uncheck();

  const after=await bootstrap(page);expect(after.cash).toBe(before.cash);expect(after.tracks).toEqual(before.tracks);expect(after.tick).toBe(before.tick);
  expect(Number(await canvas.getAttribute('data-culled'))).toBeGreaterThan(0);expect(errors).toEqual([]);
});

for (const width of [1280, 640]) {
  test(`reset camera preserves the visible centre with a panel at ${width}px`,async({page})=>{
    await page.setViewportSize({width,height:900});
    await page.goto('/');await ready(page);
    const canvas=page.locator('canvas');
    const box=(await canvas.boundingBox())!;
    await page.mouse.move(box.x+box.width/2,box.y+box.height/3);
    await page.mouse.down();await page.mouse.move(box.x+box.width/2+60,box.y+box.height/3+30);await page.mouse.up();
    await openPanel(page,'view');
    await page.getByRole('button',{name:'Zoom out',exact:true}).click();
    await expect(canvas).not.toHaveAttribute('data-zoom','1.500');
    const visibleWorldCentre=()=>canvas.evaluate(el=>{
      const r=el.getBoundingClientRect(),panel=document.querySelector('.primary-panel')!.getBoundingClientRect();
      const [x,y]=el.dataset.camera!.split(',').map(Number),zoom=Number(el.dataset.zoom);
      const center=panel.width>=r.width*.8
        ? {x:r.width/2,y:Math.max(0,panel.top-r.top)/2}
        : {x:Math.max(0,panel.left-r.left)/2,y:r.height/2};
      return {x:x+(center.x-r.width/2)/zoom,y:y+(center.y-r.height/2)/zoom};
    });
    const before=await visibleWorldCentre();
    await page.getByRole('button',{name:'Reset camera',exact:true}).first().click();
    await expect(canvas).toHaveAttribute('data-zoom','1.500');
    const after=await visibleWorldCentre();
    expect(Math.abs(after.x-before.x)).toBeLessThan(.15);
    expect(Math.abs(after.y-before.y)).toBeLessThan(.15);
  });
}

test('preview and cancel never charge; buffer extension confirms once, removal and invalid placement use host state',async({page})=>{
  const timers:ReturnType<typeof setTimeout>[]=[];
  await page.routeWebSocket('**/api/session?*',route=>{const server=route.connectToServer();route.onMessage(message=>{
    const data=JSON.parse(String(message));if(data.type==='command'&&data.payload.kind==='buildTrack')timers.push(setTimeout(()=>server.send(message),600));else server.send(message);
  });});
  try {
    await page.goto('/');await ready(page);
    await page.getByRole('button',{name:'Run',exact:false}).click();await expect(page.getByTestId('clock-state')).toHaveText('Running');
    await page.getByRole('button',{name:'Build track',exact:true}).click();await expect(page.getByTestId('clock-state')).toHaveText('Running');
    await page.getByRole('button',{name:'Pause',exact:false}).click();await expect(page.getByTestId('clock-state')).toHaveText('Paused');
    const before=await bootstrap(page);
    await clickTile(page,{x:37,y:16,z:1});await expect(page.getByTestId('preview-position')).toHaveText('37, 16, 1');
    expect((await bootstrap(page)).cash).toBe(before.cash);
    await page.getByRole('button',{name:'Cancel preview',exact:true}).click();await expect(page.getByRole('button',{name:'Confirm track change',exact:true})).toBeDisabled();
    await clickTile(page,{x:37,y:16,z:1});await page.getByRole('button',{name:'Confirm track change',exact:true}).dblclick();
    await expect(page.getByRole('status')).toHaveText('Waiting for server confirmation…');expect((await bootstrap(page)).cash).toBe(before.cash);
    await expect(page.getByRole('status')).toHaveText('Confirmed by server');
    const built=await bootstrap(page);expect(BigInt(before.cash)-BigInt(built.cash)).toBe(200n);expect(built.tracks).toHaveLength(before.tracks.length+1);
    expect(built.tracks.find((t:any)=>t.position.x===36&&t.position.y===16).shape).toBe('eastWest');
    await clickTile(page,{x:23,y:16,z:1});await page.getByRole('button',{name:'Confirm track change',exact:true}).click();await expect(page.getByRole('status')).toHaveText('Track cannot be placed here.');expect((await bootstrap(page)).cash).toBe(built.cash);
    await page.getByRole('button',{name:'Remove',exact:true}).click();await clickTile(page,{x:37,y:16,z:1});await page.getByRole('button',{name:'Confirm track change',exact:true}).click();await expect(page.getByRole('status')).toHaveText('Confirmed by server');
    const removed=await bootstrap(page);expect(removed.tracks).toHaveLength(before.tracks.length);expect(removed.cash).toBe(built.cash);
    // Protected station rail is rejected atomically.
    await clickTile(page,{x:20,y:8,z:1});await page.getByRole('button',{name:'Confirm track change',exact:true}).click();await expect(page.getByRole('status')).toHaveText('Track cannot be placed here.');
    await page.getByRole('button',{name:'Inspect',exact:true}).click();await expect(page.getByTestId('clock-state')).toHaveText('Paused');
    await page.reload();await ready(page);expect((await bootstrap(page)).cash).toBe(built.cash);
  }finally{timers.forEach(clearTimeout);}
});

test('train selection exposes no driving controls and map keys send no train input',async({page})=>{
  const commands:string[]=[];
  await page.routeWebSocket('**/api/session?*',route=>{const server=route.connectToServer();route.onMessage(message=>{const data=JSON.parse(String(message));if(data.type==='command')commands.push(data.payload.kind);server.send(message);});});
  await page.goto('/');await ready(page);await selectTrain(page);
  await expect(page.getByRole('combobox',{name:'Controller',exact:true})).toHaveCount(0);
  await expect(page.getByRole('button',{name:'Take control',exact:true})).toHaveCount(0);
  const canvas=page.locator('canvas');await canvas.focus();
  for(const key of ['ArrowUp','ArrowDown','ArrowLeft','ArrowRight','Shift'])await canvas.press(key);
  await page.waitForTimeout(1200);
  expect(commands.filter(k=>['acquireControl','releaseControl','setController','controllerInput'].includes(k))).toEqual([]);
});

test('Japanese playable map fits narrow doubled-density displays with scaled picking',async({browser})=>{
  const context=await browser.newContext({viewport:{width:640,height:900},deviceScaleFactor:2,locale:'ja-JP'});
  const page=await context.newPage();await page.goto('/');await expect(page.getByTestId('connection')).toHaveText('ゲームに接続済み');await expect(page.locator('canvas')).toHaveAttribute('data-assets','ready');
  const dimensions=await page.locator('canvas').evaluate(el=>({pixels:(el as HTMLCanvasElement).width,css:el.getBoundingClientRect().width}));expect(dimensions.pixels).toBe(Math.round(dimensions.css*2));
  await clickSprite(page,{x:16,y:16,z:0},'pier');await expect(page.getByTestId('selected-object')).toContainText('building-1');
  for(const width of [720,640,360]){await page.setViewportSize({width,height:900});expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);}
  await page.setViewportSize({width:1280,height:900});await context.close();
});

test('real automatic reference loop reaches station, dwells, departs and returns through the junction',async({page})=>{
  const arrivals:{tick:bigint;position:string}[]=[];const positions=new Set<string>();let station=false,sampling=false;
  page.on('websocket',socket=>socket.on('framereceived',frame=>{
    const message=JSON.parse(String(frame.payload));if(!sampling||message.type!=='delta')return;
    const train=message.trains[0];if(!train)return;
    positions.add(`${train.position.x},${train.position.y}`);
    if(train.state==='station'&&!station)arrivals.push({tick:BigInt(message.tick),position:`${train.position.x},${train.position.y}`});
    station=train.state==='station';
  }));
  await page.goto('/');await ready(page);
  await selectTrain(page);
  sampling=true;await page.getByRole('button',{name:'Run',exact:false}).click();
  await expect.poll(()=>arrivals.length,{timeout:15000}).toBeGreaterThanOrEqual(2);
  await page.getByRole('button',{name:'Pause',exact:false}).click();sampling=false;
  expect(arrivals.every(item=>item.position==='20,8')).toBe(true);expect(arrivals[1].tick-arrivals[0].tick).toBeGreaterThanOrEqual(74n);
  expect([...positions].some(p=>p.startsWith('28,'))).toBe(true);expect([...positions].some(p=>p.endsWith(',24'))).toBe(true);
  const snapshot=await bootstrap(page);const tracks=new Set(snapshot.tracks.map((t:any)=>`${t.position.x},${t.position.y},${t.position.z}`));
  for(const p of snapshot.trains[0].carPositions)expect(tracks.has(`${p.x},${p.y},${p.z}`)).toBe(true);
});

test('epoch snapshot fixture discards old geometry and restores initial framing', async ({ page }) => {
  const replaceWorld=await worldReplacementFixture(page);
  await page.goto('/'); await ready(page);
  await page.locator('canvas').evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
  const homeZoom = await page.locator('canvas').evaluate(el => Math.min(1.5, Math.max(.5, el.getBoundingClientRect().width / 850)).toFixed(3));
  const homeCamera = await page.locator('canvas').getAttribute('data-camera');
  await page.getByRole('button', { name: 'Zoom in', exact: true }).click();
  await page.getByRole('button', { name: 'Build track', exact: true }).click(); await clickTile(page, { x: 37, y: 16, z: 1 });
  await expect(page.getByTestId('preview-position')).toHaveText('37, 16, 1');
  const next=await replaceWorld();
  await expect(page.locator('canvas')).toHaveAttribute('data-world-epoch',next.worldEpoch);
  await expect(page.getByRole('button', { name: 'Inspect', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('.tool-card')).toHaveCount(0); await expect(page.locator('canvas')).toHaveAttribute('data-zoom', homeZoom!);
  await expect(page.locator('canvas')).toHaveAttribute('data-camera', homeCamera!);
});

test('DPR, resize, pan and zoom preserve backing size, camera and sprite picking', async ({ browser }) => {
  const evidence = await evidenceDirectory('map');
  const results: unknown[] = [];
  for (const dpr of [1,2]) {
    const context = await browser.newContext({ deviceScaleFactor:dpr, viewport:{width:1280,height:720} });
    try {
      const page = await context.newPage(); await page.goto('/'); await ready(page);
      const canvas = page.locator('canvas');
      await page.getByRole('button',{name:'Zoom in',exact:true}).click(); await settled(page);
      const box = (await canvas.boundingBox())!; await page.mouse.move(box.x+box.width/2,box.y+box.height/2);
      await page.mouse.down(); await page.mouse.move(box.x+box.width/2+35,box.y+box.height/2+20); await page.mouse.up(); await settled(page);
      const camera = await canvas.getAttribute('data-camera'), zoom = await canvas.getAttribute('data-zoom');
      await page.setViewportSize({width:1024,height:768}); await settled(page);
      await expect(canvas).toHaveAttribute('data-camera',camera!); await expect(canvas).toHaveAttribute('data-zoom',zoom!);
      const dimensions = await canvas.evaluate(el => {const c=el as HTMLCanvasElement,r=c.getBoundingClientRect();return {w:c.width,h:c.height,cssW:r.width,cssH:r.height,dpr:devicePixelRatio};});
      expect(dimensions.w).toBe(Math.round(dimensions.cssW*dpr)); expect(dimensions.h).toBe(Math.round(dimensions.cssH*dpr));
      await clickSprite(page,{x:16,y:16,z:0},'pier'); await expect(page.getByTestId('selected-object')).toContainText('building-1');
      results.push(dimensions);
    } finally { await context.close(); }
  }
  await writeFile(`${evidence}/dpr.json`,JSON.stringify(results,null,2));
});
