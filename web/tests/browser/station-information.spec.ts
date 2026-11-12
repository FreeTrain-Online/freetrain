import {readFileSync,writeFileSync} from 'node:fs';
import {test,expect,type Page,type WebSocketRoute} from './fixtures.ts';
import {canonicalState as state,ready,closePanel,openPanel,exposeMapPoint} from './browser-helpers.ts';
import {fit} from './layout-helpers.ts';
const en=JSON.parse(readFileSync('src/locales/en/app.json','utf8'));
const ja=JSON.parse(readFileSync('src/locales/ja/app.json','utf8'));
const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
const compat='protocolVersion=10&contentVersion=mixed-railway-content-1';
const bytes=async(p:Page)=>(await p.request.get('/api/saves/download')).body();
test.use({hasTouch:true,actionTimeout:15000});
async function pick(page:Page,object:any,text:typeof en,touch:boolean,hasController=false){
 await closePanel(page);await page.getByRole('button',{name:text.referenceInspect,exact:true}).click();
 const canvas=page.locator('canvas.world-canvas');await expect(canvas).toHaveAttribute('data-presentation','ready');
 const recipe=object.kind?`system:thin-${object.direction}`:object.definition.recipeId;
 const frame=catalog.renderRecipes.find((r:any)=>r.id===recipe).frames.find((f:any)=>f.status==='ready');
 const mask=catalog.masks[frame.masks['spring/day']];let sample:any;
 for(let y=1;y<mask.length-1&&!sample;y++)for(let x=1;x<mask[y].length-1&&!sample;x++)
  if(mask.slice(y-1,y+2).every((r:string)=>r.slice(x-1,x+2)==='111'))sample={x:x+.5,y:y+.5};
 expect(sample).toBeTruthy();const center=Math.floor(((await state(page)).map.height-1)/2);
 const location=object.kind?{...object.location,x:object.location.x+object.length-1}:object.location;
 const q={x:16*(location.x+frame.cell[0]-center+location.y+frame.cell[1])-frame.anchor[0]+sample.x,
  y:8*(location.y+frame.cell[1]-location.x-frame.cell[0]+center)-16*(object.location.z+frame.cell[2])-frame.anchor[1]+sample.y};
 // Bring the exact painted opaque sample into view, then clear the narrow sheet.
 await canvas.dispatchEvent('wheel',await canvas.evaluate((el,q)=>{const [x,y]=el.dataset.camera!.split(',').map(Number),z=Number(el.dataset.zoom);return {deltaX:(q.x-x)*z,deltaY:(q.y-y)*z,deltaMode:0};},q));
 await page.evaluate(()=>new Promise<void>(r=>requestAnimationFrame(()=>requestAnimationFrame(()=>r()))));
 const point=await canvas.evaluate((el,q)=>{const r=el.getBoundingClientRect(),[x,y]=el.dataset.camera!.split(',').map(Number),z=Number(el.dataset.zoom);return {x:r.x+r.width/2+(q.x-x)*z,y:r.y+r.height/2+(q.y-y)*z};},q);
 const p=await exposeMapPoint(page,point);
 await page.evaluate(()=>new Promise<void>(r=>requestAnimationFrame(()=>requestAnimationFrame(()=>r()))));
 await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/pick-${object.id}.png`});
 writeFileSync(`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/pick-${object.id}.json`,JSON.stringify({object,q,p,camera:await canvas.getAttribute('data-camera')}));
 if(touch)await page.touchscreen.tap(p.x,p.y);else await page.mouse.click(p.x,p.y);
 if(hasController)await expect(page.locator('.primary-panel .fleet-controller-manager')).toBeVisible();
}

for(const locale of ['en','ja'] as const)test(`P6-23 ${locale} station information, sorted identity list, labels, peer drafts and midnight reopen`,async({page,browser,baseURL})=>{
 test.setTimeout(180000);const text=locale==='ja'?ja:en,touch=locale==='ja';
 if(touch)await page.setViewportSize({width:430,height:860});
 await page.addInitScript(l=>localStorage.setItem('freetrain-online.locale',l),locale);
 const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 const context=await browser.newContext({baseURL}),peer=await context.newPage();
 let socket:WebSocketRoute|undefined,serial=0;const receipts:any[]=[];
 await peer.routeWebSocket('**/api/session?*',route=>{const server=route.connectToServer();socket=server;
  server.onMessage(m=>{receipts.push(JSON.parse(String(m)));route.send(m);});route.onMessage(m=>server.send(m));});
 async function command(payload:any){const requestId='info-'+(++serial),s=await state(page);socket!.send(JSON.stringify({type:'command',requestId,expectedWorldEpoch:s.worldEpoch,payload}));await expect.poll(()=>receipts.find(m=>m.requestId===requestId)?.type).toBe('accepted');}
 async function consistent(){const s=await state(page);await expect.poll(async()=>(await state(peer)).mixed).toEqual(s.mixed);return s;}
 async function upload(data:Buffer){const s=await state(page);const r=await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data});expect(r.ok(),await r.text()).toBe(true);await ready(page,locale);await ready(peer);await consistent();}
 async function list(id?:string){await openPanel(page,'stations',locale);if(id)await page.getByLabel(text.stationSelect,{exact:true}).selectOption(id);}
 async function step(){const minute=BigInt((await state(page)).tick)+1n;await page.locator('canvas.world-canvas').focus();await page.locator('canvas.world-canvas').press('n');await expect.poll(async()=>(await state(page)).tick).toBe(String(minute));return consistent();}
 try{
  await page.goto('/');await ready(page,locale);await list();await expect(page.getByText(text.stationListEmpty,{exact:true})).toBeVisible();await expect(page.getByRole('button',{name:text.stationMove,exact:true})).toBeDisabled();
  expect((await peer.request.post('/api/access',{data:{secret:'operator-test'}})).ok()).toBe(true);await peer.goto('/');await ready(peer);
  await upload(readFileSync('tests/fixtures/station-controller-v11.json'));
  let s=await consistent();const a=s.mixed.stationGraph.buildings.find((b:any)=>b.id===s.mixed.stationGraph.platforms[0].hostId),b=s.mixed.stationGraph.buildings.find((b:any)=>b.id!==a.id),train=s.mixed.fleet.trains[0];
  const q={expectedWorldEpoch:s.worldEpoch,expectedRevision:s.revision,expectedMinute:s.tick,stationId:a.id,platformId:s.mixed.stationGraph.platforms[0].id,trainId:train.id};
  const viewer=await browser.newContext({baseURL});
  try{
   const path='/api/station/information?'+compat,before=await bytes(page);
   expect((await viewer.request.post(path,{data:q})).status()).toBe(401);
   expect((await viewer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);
   const r=await viewer.request.post(path,{data:q});expect(r.ok()).toBe(true);expect((await r.json()).reason).toBe('wait');
   expect((await viewer.request.post(path,{data:{...q,expectedRevision:String(BigInt(q.expectedRevision)+1n)}})).status()).toBe(409);
   expect((await viewer.request.post(path,{data:{...q,expectedWorldEpoch:String(BigInt(q.expectedWorldEpoch)+1n)}})).status()).toBe(409);
   expect((await viewer.request.post(path,{data:{...q,trainId:'-1'}})).status()).toBe(400);
   expect((await viewer.request.post(path.replace('protocolVersion=10','protocolVersion=8'),{data:q})).status()).toBe(426);
   expect(await bytes(page)).toEqual(before);
  }finally{await viewer.close();}
  // Empty and duplicate names are rendered by ID; native list selection supports keys.
  await list(a.id);await expect(page.getByTestId('station-waiting')).toHaveText('5,000');await expect(page.getByTestId('station-demand')).toHaveText('5,000');
  await expect(page.getByTestId('station-boarded-today')).toHaveText('0');await expect(page.getByTestId('station-average-boarded')).toHaveText('0');
  const selector=page.getByLabel(text.stationSelect,{exact:true});const ids=await selector.locator('option').evaluateAll(options=>options.map(o=>(o as HTMLOptionElement).value));await selector.focus();await selector.press('End');await expect(selector).toHaveValue(ids.at(-1)!);await selector.press('Home');await expect(selector).toHaveValue(ids[0]);await selector.selectOption(a.id);
  await page.getByLabel(text.stationDiagnosisTrain,{exact:true}).selectOption(train.id);await expect(page.getByTestId('station-diagnosis')).toHaveText(text.stationDiagnosisWait);await expect(page.getByTestId('station-stop-index')).toContainText('2 / 2');
  await page.getByLabel(text.stationDiagnosisPlatform,{exact:true}).selectOption(s.mixed.stationGraph.platforms[1].id);await expect(page.getByTestId('station-diagnosis')).toHaveText(text.stationDiagnosisAway);
  await selector.selectOption(b.id);await expect(page.getByTestId('station-demand')).toHaveText('5,000');await expect(page.getByTestId('station-zero-supply')).toHaveCount(0);
  await selector.selectOption(a.id);
  // The read-only check uses actual shared TAT settings and call counts.
  await page.getByLabel(text.stationDiagnosisTrain,{exact:true}).selectOption(train.id);
  const configure=async(kind:string)=>{
   const f=(await state(page)).mixed.fleet,c=f.controllers.find((c:any)=>c.kind==='timetable');
   await command({kind:'manageFleetController',action:'configure',controllerId:c.id,expectedRevision:c.revision,name:'',provider:null,
    harbors:[{location:a.location,kind,minutes:0,reverse:false}],location:a.location});
  };
  await configure('pass');await expect(page.getByTestId('station-diagnosis')).toHaveText(text.stationDiagnosisPass);
  await configure('advanced');let c=(await state(page)).mixed.fleet.controllers.find((c:any)=>c.kind==='timetable');
  await command({kind:'editFleetRule',controllerId:c.id,expectedRevision:c.revision,location:a.location,settingKind:'harbor',operation:'add',index:-1,offset:0,
   rule:{month:-1,day:-1,dayOfWeek:-1,hour:-1,minutes:-1,action:'forwarding'},expectedGeometryRevision:null});
  await expect(page.getByTestId('station-diagnosis')).toHaveText(text.stationDiagnosisWait);
  let active=await state(page);for(let n=0;active.mixed.fleet.trains[0].stopCallCount===0&&n<4;n++)active=await step();
  expect(active.mixed.fleet.trains[0].stopCallCount).toBe(1);await expect(page.getByTestId('station-diagnosis')).toHaveText(text.stationDiagnosisForwarding);
  await expect(page.getByTestId('station-boarded-today')).toHaveText('0');
  await upload(readFileSync('tests/fixtures/station-controller-v11.json'));await list(a.id);
  // Move changes only the local camera; it leaves time, save and canvas intact.
  const canvas=page.locator('canvas.world-canvas');await canvas.evaluate(el=>el.setAttribute('data-instance','station-information'));
  const beforeMove=await bytes(page);await canvas.dispatchEvent('wheel',{deltaX:150,deltaY:80,deltaMode:0});const camera=await canvas.getAttribute('data-camera');
  await page.getByRole('button',{name:text.stationMove,exact:true}).first().click();await expect(canvas).not.toHaveAttribute('data-camera',camera!);expect(await bytes(page)).toEqual(beforeMove);await expect(canvas).toHaveAttribute('data-instance','station-information');
  // Peer updates confirmed name and information without overwriting a local draft.
  await page.getByRole('button',{name:text.stationProperties,exact:true}).click();
  await expect(page.getByTestId('station-information-name')).toContainText(a.name);
  await page.getByLabel(text.stationName,{exact:true}).fill('Local draft / 下書き');
  await command({kind:'renameStation',stationId:a.id,expectedRevision:a.revision,name:'Peer station / 相手駅'});
  await expect(page.getByTestId('station-information-name')).toContainText('Peer station / 相手駅');await expect(page.getByLabel(text.stationName,{exact:true})).toHaveValue('Local draft / 下書き');await expect(page.getByRole('button',{name:text.rename,exact:true})).toBeDisabled();
  await closePanel(page);let dialog=page.locator('.decision-dialog');await expect(dialog).toBeVisible();await dialog.getByRole('button',{name:text.keepEditing,exact:true}).click();
  await page.getByRole('button',{name:text.resetDraft,exact:true}).click();await page.getByLabel(text.stationName,{exact:true}).fill('Same / 同名');await page.getByLabel(text.stationName,{exact:true}).press('Enter');
  await expect(page.getByTestId('confirmed-name')).toHaveText('Same / 同名');await consistent();
  await list(a.id);s=await state(page);const other=s.mixed.stationGraph.buildings.find((x:any)=>x.id===b.id);
  await command({kind:'renameStation',stationId:other.id,expectedRevision:other.revision,name:'Same / 同名'});
  await expect(selector).toHaveValue(a.id);await expect(selector.locator('option')).toHaveText([a.id,b.id].sort((a,b)=>Number(BigInt(a)-BigInt(b))).map(id=>`Same / 同名 · #${id}`));
  await fit(page);await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/station-list-${locale}.png`});
  // Label controls remain local and survive reload. They cannot rename shared data.
  const beforeLabels=await bytes(page);await openPanel(page,'view',locale);await page.getByLabel(text.stationNamesVisible,{exact:true}).uncheck();await page.getByLabel(text.stationNameFont,{exact:true}).selectOption('serif');await page.getByLabel(text.stationNameSize,{exact:true}).fill('18');await page.getByLabel(text.stationNameSize,{exact:true}).press('Tab');await page.getByLabel(text.stationNameColor,{exact:true}).fill('#12abcd');
  expect(await bytes(page)).toEqual(beforeLabels);await page.reload();await ready(page,locale);await openPanel(page,'view',locale);await expect(page.getByLabel(text.stationNamesVisible,{exact:true})).not.toBeChecked();await expect(page.getByLabel(text.stationNameFont,{exact:true})).toHaveValue('serif');await expect(page.getByLabel(text.stationNameSize,{exact:true})).toHaveValue('18');await expect(page.getByLabel(text.stationNameColor,{exact:true})).toHaveValue('#12abcd');
  // A response delayed beyond several simulation minutes still updates during Run.
  await list(a.id);await expect(page.getByTestId('station-observed-minute')).toBeVisible();
  const observed=await page.getByTestId('station-observed-minute').textContent();let delayed=0;
  await page.route('**/api/station/information?*',async route=>{const response=await route.fetch();delayed++;await new Promise(r=>setTimeout(r,150));await route.fulfill({response});});
  await page.locator('.transport button').first().click();await expect.poll(async()=>(await state(page)).paused).toBe(false);
  await expect(page.getByTestId('station-observed-minute')).not.toHaveText(observed!);await expect(page.getByTestId('station-waiting')).not.toHaveText('—');expect(delayed).toBeGreaterThan(0);
  await page.locator('.transport button').first().click();await expect.poll(async()=>(await state(page)).paused).toBe(true);await page.unrouteAll({behavior:'wait'});
  // Construct a real station outside the controlled provider's advertised cube.
  await closePanel(page);await page.getByRole('button',{name:text.stationTask,exact:true}).click();
  if(!await page.locator('#building-catalog').count())await page.getByRole('button',{name:text.chooseFromCatalog,exact:true}).click();
  await page.locator(`[data-catalog-variant="${a.definition.recipeId}"]`).click();await page.getByRole('button',{name:text.closeCatalog,exact:true}).click();
  for(const [label,value] of [[text.stationX,43],[text.stationY,13],[text.stationLevel,2]] as const)await page.getByLabel(label,{exact:true}).fill(String(value));
  await page.locator('.station-task form').getByRole('button',{name:text.stationCheck,exact:true}).click();await page.getByRole('button',{name:text.stationConfirm,exact:true}).click();
  await expect.poll(async()=>(await state(page)).mixed.stationGraph.buildings.length).toBe(3);
  const zero=(await state(page)).mixed.stationGraph.buildings.find((b:any)=>b.location.x===43);await list(zero.id);
  await expect(page.getByTestId('station-demand')).toHaveText('0');await expect(page.getByTestId('station-waiting')).toHaveText('0');await expect(page.getByTestId('station-zero-supply')).toHaveText(text.stationDiagnosisZeroSupply);
  // This committed scene reaches 23:59 through production train/hour callbacks.
  await upload(readFileSync('tests/fixtures/station-information-midnight-v11.json'));s=await state(page);const midnight=s.mixed.stationGraph.buildings[0],platform=s.mixed.stationGraph.platforms[0];expect(midnight.exports.today).toBeGreaterThan(0);
  await list(midnight.id);await expect(page.getByTestId('station-boarded-today')).toHaveText(midnight.exports.today.toLocaleString(locale));
  await expect(page.getByTestId('station-average-boarded')).toHaveText('22');await expect(page.getByTestId('station-average-alighted')).toHaveText('2');
  const before=await bytes(page);s=await step();expect(BigInt(s.tick)%1440n).toBe(0n);
  await expect(page.getByTestId('station-boarded-today')).toHaveText('0');await expect(page.getByTestId('station-boarded-yesterday')).toHaveText(midnight.exports.today.toLocaleString(locale));await expect(page.getByTestId('station-calls-yesterday')).toHaveText(midnight.trains.today.toLocaleString(locale));
  await expect(page.getByTestId('station-alighted-today')).toHaveText('0');await expect(page.getByTestId('station-alighted-yesterday')).toHaveText('300');await expect(page.getByTestId('station-waiting')).toHaveText('9,286');await expect(page.getByTestId('station-average-boarded')).toHaveText('22');await expect(page.getByTestId('station-average-alighted')).toHaveText('2');
  const after=await bytes(page);await upload(before);await list(midnight.id);await step();expect(await bytes(page)).toEqual(after);
  await upload(after);expect(await bytes(page)).toEqual(after);await list(midnight.id);await expect(page.getByTestId('station-boarded-yesterday')).toHaveText(midnight.exports.today.toLocaleString(locale));await expect(page.getByTestId('station-waiting')).not.toHaveText('—');
  await page.getByRole('button',{name:text.stationMove,exact:true}).click();await page.locator('.station-logs').scrollIntoViewIfNeeded();await expect(page.getByTestId('station-calls-yesterday')).toBeVisible();
  await fit(page);await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/station-midnight-${locale}.png`});
  // A selected station disappearing clears selection and disables Move.
  await pick(peer,midnight,en,false);await peer.getByRole('button',{name:en.stationRemove,exact:true}).click();await peer.getByRole('button',{name:en.confirmRemoval,exact:true}).click();
  await expect(page.getByText(text.stationListEmpty,{exact:true})).toBeVisible();await expect(page.getByRole('button',{name:text.stationMove,exact:true})).toBeDisabled();await expect(page.getByTestId('station-information-name')).toHaveCount(0);
  await pick(page,platform,text,touch);await expect(page.getByTestId('station-diagnosis')).toHaveText(text.stationDiagnosisDisconnected);await expect(page.getByTestId('station-zero-supply')).toHaveCount(0);
  writeFileSync(`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/station-information-${locale}.json`,JSON.stringify({beforeMidnight:JSON.parse(before.toString()),afterMidnight:JSON.parse(after.toString()),removed:await consistent()},null,2));
  expect(errors).toEqual([]);
 }finally{await context.close();}
});
