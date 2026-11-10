import {readFileSync,writeFileSync} from 'node:fs';
import {test,expect,type Page} from './fixtures.ts';
import {canonicalState as state,ready,closePanel,openPanel,exposeMapPoint} from './browser-helpers.ts';
import {fit} from './layout-helpers.ts';

const en=JSON.parse(readFileSync('src/locales/en/app.json','utf8'));
const ja=JSON.parse(readFileSync('src/locales/ja/app.json','utf8'));
const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
const output=process.env.FREETRAIN_SERVICE_OUTPUT??'tests/fixtures/passenger-service';
const save=async(p:Page)=>(await p.request.get('/api/saves/download')).body();
test.use({hasTouch:true,actionTimeout:15000});

async function platformPick(page:Page,platform:any,text:typeof en,touch:boolean){
 await closePanel(page);await page.getByRole('button',{name:text.referenceInspect,exact:true}).click();
 await expect(page.locator('canvas.world-canvas')).toHaveAttribute('data-presentation','ready');
 await page.evaluate(()=>new Promise<void>(resolve=>requestAnimationFrame(()=>requestAnimationFrame(()=>resolve()))));
 const frame=catalog.renderRecipes.find((r:any)=>r.id===`system:thin-${platform.direction}`).frames[0];
 const mask=catalog.masks[frame.masks['spring/day']];let sample:any;
 for(let y=1;y<mask.length-1&&!sample;y++)for(let x=1;x<mask[y].length-1&&!sample;x++)
  if(mask.slice(y-1,y+2).every((r:string)=>r.slice(x-1,x+2)==='111'))sample={x:x+.5,y:y+.5};
 expect(sample).toBeTruthy();
 const center=Math.floor(((await state(page)).map.height-1)/2);
 const point=await page.locator('canvas.world-canvas').evaluate((el,a)=>{
  const r=el.getBoundingClientRect(),[cx,cy]=el.dataset.camera!.split(',').map(Number),zoom=Number(el.dataset.zoom),x=a.location.x-a.center,y=a.location.y;
  return {x:r.x+r.width/2+(16*(x+y)-a.anchor[0]+a.sample.x-cx)*zoom,y:r.y+r.height/2+(8*(y-x)-16*a.location.z-a.anchor[1]+a.sample.y-cy)*zoom};
 },{location:platform.location,anchor:frame.anchor,sample,center});
 const p=await exposeMapPoint(page,point);
 await page.evaluate(()=>new Promise<void>(resolve=>requestAnimationFrame(()=>requestAnimationFrame(()=>resolve()))));
 await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/pick-${touch?'ja':'en'}.png`});
 if(touch)await page.touchscreen.tap(p.x,p.y);else await page.mouse.click(p.x,p.y);
 await expect(page.getByTestId('platform-identity')).toContainText(platform.id);
}

for(const west of [false,true])test(`P6-21 ${west?'JA narrow touch westbound':'EN desktop eastbound'} constructs two stations, purchases and runs packed Simple passenger service with exact continuation`,async({page,browser,baseURL,worldServer})=>{
 test.setTimeout(240000);
 const locale=west?'ja':'en',text=west?ja:en,peerLocale=west?'en':'ja';
 // Committed baselines are generated and checked by StationPassengerServiceTests.
 // FREETRAIN_SERVICE_OUTPUT can select freshly regenerated equivalents.
 const path=output+(west?'-west':'-east');
 const baseline=JSON.parse(readFileSync(path+'.json','utf8')),metadata=JSON.parse(readFileSync(path+'.metadata.json','utf8'));
 expect(baseline.mixed.stationGraph.buildings).toEqual([]);
 expect(baseline.mixed.stationGraph.platforms).toEqual([]);
 expect(baseline.mixed.fleet.trains).toEqual([]);
 expect(baseline.mixed.stationGraph.listeners[0].provider.input).toBe('controlled');
 expect(baseline.mixed.stationGraph.listeners[0].provider.population.base).toBe(10000);
 if(west)await page.setViewportSize({width:430,height:860});
 await page.addInitScript(locale=>localStorage.setItem('freetrain-online.locale',locale),locale);
 const context=await browser.newContext({baseURL,locale:peerLocale==='ja'?'ja-JP':'en-US',viewport:{width:1280,height:900}}),peer=await context.newPage();
 const errors:string[]=[],messages:any[][]=[[],[]];
 for(const [i,p] of [page,peer].entries()){
  p.on('pageerror',e=>errors.push(String(e)));
  await p.routeWebSocket('**/api/session?*',route=>{const server=route.connectToServer();
   server.onMessage(m=>{messages[i].push(JSON.parse(String(m)));route.send(m);});route.onMessage(m=>server.send(m));});
 }
 const latest=(i:number)=>{const m=messages[i].filter(m=>['join','snapshot','delta'].includes(m.type)).at(-1);return m?.snapshot??m;};
 const bells=(i:number)=>messages[i].filter(m=>m.type==='departureBell');
 async function consistent(){const s=await state(page);for(const i of [0,1])await expect.poll(()=>latest(i)?.mixed).toEqual(s.mixed);return s;}
 async function step(){
  const tick=BigInt((await state(page)).tick)+1n;
  await expect(page.locator('.transport .step')).toBeEnabled();
  if(west){
   const menu=page.locator('.command-bar').getByRole('button',{name:'ゲーム ▾',exact:true});
   await menu.tap();await page.locator('#game-menu .overflow-step').tap();await menu.tap();
  }else await page.locator('.transport .step').click();
  for(const i of [0,1])await expect.poll(()=>latest(i)?.tick).toBe(String(tick));
  return consistent();
 }
 async function until(predicate:(s:any)=>boolean,limit=180){
  let s=await state(page);for(let n=0;!predicate(s)&&n<limit;n++)s=await step();expect(predicate(s)).toBe(true);return s;
 }
 async function upload(bytes:Buffer){const s=await state(page),r=await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:bytes});expect(r.ok(),await r.text()).toBe(true);await ready(page,locale);await ready(peer,peerLocale);await consistent();}
 async function stationsTool(){await closePanel(page);const b=page.getByRole('button',{name:text.stationTask,exact:true});if(await b.getAttribute('aria-pressed')!=='true')await b.click();}
 async function owned(){await openPanel(page,'fleet',locale);await page.getByRole('button',{name:new RegExp('^'+text.ownedStock)}).click();await page.locator('.fleet-owned .catalog-item-main').first().click();}
 async function deploy(){
  const controls=page.locator('.deployment-controls');
  for(const [key,value] of [['X',west?37:25],['Y',15],['Z',2]] as const)await controls.getByLabel(key,{exact:true}).fill(String(value));
  await controls.getByLabel(text.deploymentReverse,{exact:true}).setChecked(west);
  await controls.getByLabel('Z',{exact:true}).press('Enter');
  await expect(page.getByTestId('deployment-car')).toHaveCount(3);
  await page.getByRole('button',{name:text.deploymentConfirm,exact:true}).click();
  await expect(page.getByRole('button',{name:text.withdrawTrain,exact:true})).toBeVisible();await consistent();
 }
 async function host(platform:any,id:string){
  await platformPick(page,platform,text,west);
  const select=page.getByLabel(text.platformHost,{exact:true});await expect(select).toBeEnabled();await select.selectOption(id);
  await page.getByRole('button',{name:text.platformApply,exact:true}).click();
  await expect.poll(async()=>(await state(page)).mixed.stationGraph.platforms.find((p:any)=>p.id===platform.id).hostId).toBe(id||null);
  await consistent();
 }
 const evidence:any={controlledSupply:10000,citySources:'P7 pending',direction:west?'west':'east'};
 try{
  await page.goto('/');await ready(page,locale);
  expect((await peer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);await peer.goto('/');await ready(peer,peerLocale);
  await upload(Buffer.from(JSON.stringify(baseline)));
  // All stations/platforms and the formation below are created through real UI.
  await stationsTool();
  for(const [i,x] of [23,36].entries()){
   if(!await page.locator('#building-catalog').count())await page.getByRole('button',{name:text.chooseFromCatalog,exact:true}).click();
   await page.locator(`[data-catalog-variant="${metadata.stationRecipe}"]`).click();
   await page.getByRole('button',{name:text.closeCatalog,exact:true}).click();
   for(const [label,value] of [[text.stationX,x],[text.stationY,13],[text.stationLevel,2]] as const)await page.getByLabel(label,{exact:true}).fill(String(value));
   await page.locator('.station-task form').getByRole('button',{name:text.stationCheck,exact:true}).click();
   await page.getByRole('button',{name:text.stationConfirm,exact:true}).click();
   await expect.poll(async()=>(await state(page)).mixed.stationGraph.buildings.length).toBe(i+1);await consistent();
  }
  await page.getByRole('tab',{name:text.platformTab,exact:true}).click();
  for(const [i,x] of [25,37].entries()){
   for(const [label,value] of [[text.platformX,x],[text.platformY,15],[text.platformLevel,2]] as const)await page.getByLabel(label,{exact:true}).fill(String(value));
   await page.getByLabel(text.platformDirection,{exact:true}).selectOption('2');
   await page.locator('.platform-task form').getByRole('button',{name:text.stationCheck,exact:true}).click();
   await page.getByRole('button',{name:text.platformConfirm,exact:true}).click();
   await expect.poll(async()=>(await state(page)).mixed.stationGraph.platforms.length).toBe(i+1);await consistent();
  }
  let s=await consistent(),g=s.mixed.stationGraph;
  const source=g.buildings[west?1:0].id,destination=g.buildings[west?0:1].id,platform=g.platforms[west?1:0];
  expect(g.platforms.map((p:any)=>p.hostId)).toEqual(g.buildings.map((b:any)=>b.id));
  expect(g.listeners[0].stations).toEqual([source]);expect(g.listenerMemberships.find((m:any)=>m.stationId===source).providers).toEqual([g.listeners[0].providerId]);
  expect(g.listenerMemberships.find((m:any)=>m.stationId===destination)?.providers??[]).toEqual([]);
  await host(platform,'');await expect(page.getByTestId('platform-disconnected')).toBeVisible();
  await openPanel(page,'fleet',locale);await page.locator('.fleet-search input').fill(metadata.stockId);
  await page.locator('.fleet-catalog .catalog-item-main').click();await page.getByLabel(text.stockLength,{exact:true}).selectOption('3');
  await page.getByRole('button',{name:text.stockQuote,exact:true}).click();await page.getByRole('button',{name:text.stockConfirmBuy,exact:true}).click();
  await page.getByRole('button',{name:new RegExp('^'+text.ownedStock)}).click();await page.locator('.fleet-owned .catalog-item-main').first().click();
  await deploy();s=await until(s=>s.mixed.fleet.trains[0].moveCount>0);
  expect(s.mixed.fleet.trains[0].passengers).toBe(0);expect(s.mixed.fleet.trains[0].stopCallCount).toBe(0);
  expect(s.mixed.stationGraph.buildings.find((b:any)=>b.id===source).trains.today).toBe(0);
  for(const i of [0,1])expect(bells(i)).toHaveLength(0);
  await page.getByRole('button',{name:text.withdrawTrain,exact:true}).click();await page.getByRole('button',{name:text.withdrawConfirm,exact:true}).click();
  await expect(page.getByRole('button',{name:text.deploymentPick,exact:true})).toBeVisible();
  await host((await state(page)).mixed.stationGraph.platforms.find((p:any)=>p.id===platform.id),source);
  await owned();await deploy();
  s=await until(s=>s.mixed.fleet.trains[0].stopCallCount===1);
  expect(s.mixed.fleet.trains[0].state).toBe('stoppingatstation');
  const waiting=await save(page),beforeBells=bells(0).length;
  evidence.waiting=JSON.parse(waiting.toString());
  const depart=await until(s=>s.mixed.fleet.trains[0].stopCallCount===0),uninterrupted=await save(page),train=depart.mixed.fleet.trains[0];
  expect(train.passengers).toBe(metadata.packed);expect(train.passengers).toBeGreaterThan(metadata.normal);
  expect(depart.mixed.stationGraph.buildings.find((b:any)=>b.id===source).passengers.gone).toBe(metadata.packed);
  for(const i of [0,1])expect(bells(i)).toHaveLength(beforeBells+1);
  // Replay the very same wait through save/reopen and an independent process.
  await upload(waiting);expect(await save(page)).toEqual(waiting);
  const bellBaseline=messages.map((_,i)=>bells(i).length);
  await worldServer.restart!();
  for(const [i,p] of [page,peer].entries())expect((await p.request.post('/api/access',{data:{secret:i===0?'':'observer-test'}})).ok()).toBe(true);
  await page.reload();await peer.reload();await ready(page,locale);await ready(peer,peerLocale);
  expect(await save(page)).toEqual(waiting);await consistent();
  for(const i of [0,1])expect(bells(i)).toHaveLength(bellBaseline[i]);
  await until(s=>s.tick===depart.tick);expect(await save(page)).toEqual(uninterrupted);
  for(const i of [0,1])expect(bells(i)).toHaveLength(bellBaseline[i]+1);
  await openPanel(page,'view',locale);await page.getByLabel(text.trainSelect,{exact:true}).selectOption(train.id);
  await page.getByRole('button',{name:text.trainMove,exact:true}).click();
  await page.getByTestId('information-passengers').scrollIntoViewIfNeeded();
  await expect(page.getByTestId('information-passengers')).toBeVisible();
  await expect(page.getByTestId('information-passengers')).toHaveText(`${metadata.packed.toLocaleString(locale)} (${Math.trunc(metadata.packed*100/metadata.normal).toLocaleString(locale)}%)`);
  await fit(page);await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/service-${locale}-packed.png`});
  evidence.departed=JSON.parse(uninterrupted.toString());evidence.waitingAfterBoarding=10000-metadata.packed;
  s=await until(s=>s.mixed.stationGraph.buildings.find((b:any)=>b.id===destination).trains.today===1);
  expect(s.mixed.fleet.trains[0].passengers).toBe(0);expect(s.mixed.fleet.trains[0].boardingOrigin).toBe(null);
  const dest=s.mixed.stationGraph.buildings.find((b:any)=>b.id===destination);
  expect(dest.imports.today).toBe(metadata.packed);expect(dest.passengers.accumulatedUnloaded).toBe(metadata.packed);
  const fares=s.mixed.accounts.transactions.filter((t:any)=>t.operation==='stationUnload');
  expect(fares).toHaveLength(1);expect(fares[0].revenue).toBe(String(metadata.packed*metadata.fare*12*2));
  const net=s.mixed.accounts.transactions.reduce((cash:bigint,t:any)=>cash+BigInt(t.revenue??'0')-BigInt(t.expenditure??'0'),0n);
  expect(BigInt(s.cash)).toBe(BigInt(baseline.mixed.accounts.initialCash)+net);
  expect(s.mixed.accounts.transactions.every((t:any)=>t.topUps==='0')).toBe(true);
  await expect(page.getByTestId('current-funds')).toHaveText('¥'+BigInt(s.cash).toLocaleString(locale));
  await expect(page.getByTestId('information-passengers')).toHaveText('0 (0%)');
  evidence.arrived=JSON.parse((await save(page)).toString());
  const cash=s.cash,events=messages.map((_,i)=>bells(i).length);
  s=await until(s=>s.mixed.fleet.trains[0].stopCallCount===0);
  expect(s.mixed.fleet.trains[0].passengers).toBe(0);expect(s.mixed.stationGraph.buildings.find((b:any)=>b.id===destination).exports.today).toBe(0);
  for(const i of [0,1])expect(bells(i)).toHaveLength(events[i]+1);expect(BigInt(s.cash)).toBeLessThanOrEqual(BigInt(cash));
  await expect(page.getByTestId('information-passengers')).toHaveText('0 (0%)');
  evidence.zeroSupplyDeparture=JSON.parse((await save(page)).toString());
  await page.getByRole('button',{name:text.trainMove,exact:true}).click();
  await page.getByTestId('information-passengers').scrollIntoViewIfNeeded();
  await expect(page.getByTestId('information-passengers')).toBeVisible();
  await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/service-${locale}-zero.png`});
  writeFileSync(`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/service-${locale}.json`,JSON.stringify(evidence,null,2));
  expect(errors).toEqual([]);
 }finally{await context.close();}
});
