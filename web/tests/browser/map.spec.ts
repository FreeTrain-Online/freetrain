import { expect, test, type Page } from '@playwright/test';
import { ready, bootstrap, at, clickTile, clickSprite, closePanel, openPanel, selectTrain, selectStation } from './ux-helpers.ts';

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
  const zoom=await canvas.getAttribute('data-zoom');await page.getByRole('button',{name:'Zoom in',exact:true}).click();await expect(canvas).not.toHaveAttribute('data-zoom',zoom!);
  await page.getByRole('button',{name:'Reset camera',exact:true}).click();
  await openPanel(page, 'view');
  await page.getByLabel('Night',{exact:true}).check();
  await page.screenshot({path:'test-results/map-night.png',fullPage:true});
  await page.getByLabel('Night',{exact:true}).uncheck();await page.getByLabel('Recolor',{exact:true}).check();await page.getByLabel('Winter ground',{exact:true}).check();
  await page.screenshot({path:'test-results/map-winter-recolor.png',fullPage:true});
  await page.getByLabel('Height cut',{exact:true}).focus();await page.keyboard.press('Home');await expect(page.getByLabel('Height cut',{exact:true})).toHaveValue('0');
  await closePanel(page);await clickTile(page,{x:34,y:22,z:0});await expect(page.getByTestId('selected-object')).toContainText('building-2');
  await page.screenshot({path:'test-results/map-height-cut.png',fullPage:true});
  await openPanel(page, 'view');
  await page.getByLabel('Height cut',{exact:true}).focus();await page.keyboard.press('End');await page.getByLabel('Winter ground',{exact:true}).uncheck();await page.getByLabel('Recolor',{exact:true}).uncheck();
  await page.screenshot({path:'test-results/map-en.png',fullPage:true});
  const after=await bootstrap(page);expect(after.cash).toBe(before.cash);expect(after.tracks).toEqual(before.tracks);expect(after.tick).toBe(before.tick);
  expect(Number(await canvas.getAttribute('data-culled'))).toBeGreaterThan(0);expect(errors).toEqual([]);
});

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
    await page.getByRole('button',{name:'Inspect / drive',exact:true}).click();await expect(page.getByTestId('clock-state')).toHaveText('Paused');
    await page.reload();await ready(page);expect((await bootstrap(page)).cash).toBe(built.cash);
  }finally{timers.forEach(clearTimeout);}
});

test('manual map keys depart at station, release on focus loss, and never capture text or IME',async({page})=>{
  const inputs:any[]=[];
  await page.routeWebSocket('**/api/session?*',route=>{const server=route.connectToServer();route.onMessage(message=>{const data=JSON.parse(String(message));if(data.type==='command'&&data.payload.kind==='controllerInput')inputs.push(data.payload);server.send(message);});});
  await page.goto('/');await ready(page);
  await selectStation(page);
  const draft=page.getByLabel('Station name',{exact:true});await draft.focus();const count=inputs.length;await page.keyboard.press('ArrowUp');await page.keyboard.press('Shift');expect(inputs).toHaveLength(count);
  await draft.dispatchEvent('compositionstart');await page.keyboard.press('ArrowDown');await draft.dispatchEvent('compositionend');expect(inputs).toHaveLength(count);
  await selectTrain(page);
  await page.getByRole('combobox',{name:'Controller',exact:true}).selectOption('manual');
  await page.getByRole('button',{name:'Run',exact:false}).click();
  await expect(page.getByTestId('train-state')).toHaveText('Station',{timeout:12000});
  await page.getByRole('button',{name:'Pause',exact:false}).click();
  const canvas=page.locator('canvas');await canvas.focus();await page.keyboard.down('ArrowUp');await expect.poll(()=>inputs.some(input=>input.depart)).toBe(true);
  await page.getByRole('button',{name:'Run',exact:false}).click(); // Moving focus releases Up; refocus to hold through a tick.
  await canvas.focus();await page.keyboard.up('ArrowUp');await page.keyboard.down('ArrowUp');
  await expect(page.getByTestId('train-state')).toHaveText('Moving',{timeout:5000});
  await page.getByRole('combobox',{name:'Controller',exact:true}).focus();await expect.poll(()=>inputs.at(-1)?.reset).toBe(true);await page.keyboard.up('ArrowUp');
  await page.getByRole('button',{name:'Pause',exact:false}).click();await canvas.focus();await page.keyboard.down('ArrowRight');await expect(page.getByTestId('manual-input')).toContainText('ArrowRight');
  await page.evaluate(()=>window.dispatchEvent(new Event('blur')));await expect.poll(()=>inputs.at(-1)?.reset).toBe(true);await expect(page.getByTestId('manual-input')).toHaveText('Keys released');await page.keyboard.up('ArrowRight');
  await page.getByRole('combobox',{name:'Controller',exact:true}).selectOption('automatic');
});

test('Japanese playable map fits narrow doubled-density displays with scaled picking',async({browser})=>{
  const context=await browser.newContext({viewport:{width:640,height:900},deviceScaleFactor:2,locale:'ja-JP'});
  const page=await context.newPage();await page.goto('/');await expect(page.getByTestId('connection')).toHaveText('ゲームに接続済み');await expect(page.locator('canvas')).toHaveAttribute('data-assets','ready');
  const dimensions=await page.locator('canvas').evaluate(el=>({pixels:(el as HTMLCanvasElement).width,css:el.getBoundingClientRect().width}));expect(dimensions.pixels).toBe(Math.round(dimensions.css*2));
  await clickSprite(page,{x:16,y:16,z:0},'pier');await expect(page.getByTestId('selected-object')).toContainText('building-1');
  for(const width of [720,640,360]){await page.setViewportSize({width,height:900});expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);}
  await page.screenshot({path:'test-results/map-ja-narrow-2x.png',fullPage:true});await page.setViewportSize({width:1280,height:900});await page.screenshot({path:'test-results/map-ja.png',fullPage:true});await context.close();
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
  await page.getByRole('combobox',{name:'Controller',exact:true}).selectOption('automatic');
  sampling=true;await page.getByRole('button',{name:'Run',exact:false}).click();
  await expect.poll(()=>arrivals.length,{timeout:15000}).toBeGreaterThanOrEqual(2);
  await page.getByRole('button',{name:'Pause',exact:false}).click();sampling=false;
  expect(arrivals.every(item=>item.position==='20,8')).toBe(true);expect(arrivals[1].tick-arrivals[0].tick).toBeGreaterThanOrEqual(74n);
  expect([...positions].some(p=>p.startsWith('28,'))).toBe(true);expect([...positions].some(p=>p.endsWith(',24'))).toBe(true);
  const snapshot=await bootstrap(page);const tracks=new Set(snapshot.tracks.map((t:any)=>`${t.position.x},${t.position.y},${t.position.z}`));
  for(const p of snapshot.trains[0].carPositions)expect(tracks.has(`${p.x},${p.y},${p.z}`)).toBe(true);
});
