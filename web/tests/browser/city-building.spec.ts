import {browserFixture} from './generated-fixtures.ts';
import {readFileSync} from 'node:fs';
import {test,expect,type Page} from './fixtures.ts';
import {canonicalState as state,ready,closePanel,openPanel,exposeMapPoint} from './browser-helpers.ts';
const texts=['en','ja'].map(l=>JSON.parse(readFileSync(`src/locales/${l}/app.json`,'utf8')));
const house='{27D6D0C0-2B1A-4494-9257-14C208168075}';
const stations=JSON.parse(readFileSync('src/generated/station-profile.json','utf8'));
const station=stations.definitions.find((d:any)=>d.sizeX===1&&d.sizeY===1&&d.height===1);
const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
const bytes=async(p:Page)=>(await p.request.get('/api/saves/download')).body();
async function point(p:Page,w:{x:number;y:number}){return p.locator('canvas.world-canvas').evaluate((el,w)=>{const r=el.getBoundingClientRect(),[x,y]=el.dataset.camera!.split(',').map(Number),z=Number(el.dataset.zoom);return {x:r.x+r.width/2+(w.x-x)*z,y:r.y+r.height/2+(w.y-y)*z};},w);}
async function quote(p:Page,t:any,x=14){
 await p.getByLabel(t.cityX,{exact:true}).fill(String(x));await p.getByLabel(t.cityY,{exact:true}).fill('12');
 await p.locator('.city-task form').getByRole('button',{name:t.stationCheck,exact:true}).click();
 await expect(p.getByRole('button',{name:t.cityConfirm,exact:true})).toBeEnabled();
}
async function pickHouse(p:Page,touch:boolean){
 const f=catalog.renderRecipes.find((r:any)=>r.id===house).frames[0],mask=catalog.masks[f.masks['spring/day']];let row=0,col=0;
 outer:for(let y=2;y<mask.length-2;y++)for(let x=2;x<mask[y].length-2;x++)if(mask.slice(y-2,y+3).every((r:string)=>r.slice(x-2,x+3)==='11111')){row=y;col=x;break outer;}
 const q=await exposeMapPoint(p,await point(p,{x:16*(14-11+12)-f.anchor[0]+col+.5,y:8*(12-(14-11))-32-f.anchor[1]+row+.5}));
 if(touch)await p.touchscreen.tap(q.x,q.y);else await p.mouse.click(q.x,q.y);
}
test.use({hasTouch:true});
for(const ja of [false,true])test(`P7-09 playable populated building ${ja?'JA narrow touch':'EN keyboard'} natural demand remove rebuild reopen and peers`,async({page,browser,baseURL})=>{
 test.setTimeout(180000);const t=texts[ja?1:0],locale=ja?'ja':'en';
 if(ja)await page.setViewportSize({width:430,height:860});
 await page.addInitScript(ja=>localStorage.setItem('freetrain-online.locale',ja?'ja':'en'),ja);
 const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 const context=await browser.newContext({baseURL}),peer=await context.newPage();
 try{
  await page.goto('/');await ready(page,locale);
  const initial=await state(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+initial.worldEpoch,{data:readFileSync(browserFixture('city'))})).ok()).toBe(true);await ready(page,locale);
  await page.getByRole('button',{name:t.stationTask,exact:true}).click();
  await page.locator(`[data-catalog-variant="${station.reference.recipeId}"]`).click();await page.getByRole('button',{name:t.closeCatalog,exact:true}).click();
  await page.getByLabel(t.stationX,{exact:true}).fill('12');await page.getByLabel(t.stationY,{exact:true}).fill('12');
  await page.locator('.station-task form').getByRole('button',{name:t.stationCheck,exact:true}).click();await page.getByRole('button',{name:t.stationConfirm,exact:true}).click();
  await expect.poll(async()=>(await state(page)).mixed.stationGraph?.buildings.length).toBe(1);
  expect((await peer.request.post('/api/access',{data:{secret:'operator-test'}})).ok()).toBe(true);await peer.goto('/');await ready(peer);
  await page.getByRole('button',{name:t.cityTask,exact:true}).click();await page.locator(`[data-catalog-variant="${house}"]`).click();await page.getByRole('button',{name:t.closeCatalog,exact:true}).click();
  const baseline=await bytes(page);await quote(page,t);expect(await bytes(page)).toEqual(baseline);
  await expect(page.locator('canvas')).toHaveAttribute('data-city-ghost-cells','1');await page.getByRole('button',{name:t.cancelPreview,exact:true}).click();
  const ground=await exposeMapPoint(page,await point(page,{x:16*(14-11+12)+16,y:8*(12-(14-11))-32+8}));
  if(ja)await page.touchscreen.tap(ground.x,ground.y);else await page.mouse.click(ground.x,ground.y);
  const confirm=page.getByRole('button',{name:t.cityConfirm,exact:true});await expect(confirm).toBeEnabled();
  await expect(page.getByTestId('city-cost')).toBeInViewport();
  await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/city-building-${locale}-preview.png`});
  await page.locator('canvas').focus();await page.keyboard.press('Enter');
  await expect.poll(async()=>(await state(peer)).mixed.city?.structures.length).toBe(1);
  expect((await state(page)).mixed.city.producers).toHaveLength(0);
  // Clock advancement and unrelated construction retain the reviewed quote.
  await quote(page,t,16);await page.getByLabel(t.gameSpeed,{exact:true}).selectOption('2');await page.getByRole('button',{name:t.run,exact:false}).click();
  await expect.poll(async()=>(await state(page)).mixed.city.producers.length,{timeout:100000}).toBe(1);
  await page.getByRole('button',{name:t.pause,exact:false}).click();
  await expect(confirm).toBeEnabled();
  const completed=await state(page),b=completed.mixed.city.structures[0];expect(b.construction).toEqual([]);expect(b.location).toEqual({x:14,y:12,z:2});
  expect(completed.mixed.stationGraph.listeners[0].provider.input).toBe('city-fixed');
  expect(completed.mixed.stationGraph.listeners[0].provider.population.base).toBe(10);
  // Independent pinned ResidentialPopulation.cs holiday table for 09–17.
  // Ten residents, one station, cap 40: truncate(10 * percent / 100).
  const expectedDemand=[4,5,4,4,3,3,4,4,3][Number(BigInt(completed.tick)/60n%24n)-9];
  expect(expectedDemand).toBeGreaterThan(0);
  await openPanel(page,'stations',locale);await page.getByLabel(t.stationSelect,{exact:true}).selectOption(completed.mixed.stationGraph.buildings[0].id);
  await expect(page.getByTestId('station-demand')).toHaveText(String(expectedDemand));await closePanel(page);
  const saved=await bytes(page),s=await state(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:saved})).ok()).toBe(true);await ready(page,locale);expect(await bytes(page)).toEqual(saved);
  await pickHouse(page,ja);await expect(page.getByTestId('city-progress')).toHaveText(t.cityComplete);
  await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/city-building-${locale}-inspector.png`});
  await page.getByRole('button',{name:t.cityRemove,exact:true}).click();const remove=page.getByRole('button',{name:t.confirmRemoval,exact:true});await expect(remove).toBeEnabled();await expect(page.getByTestId('city-remove-cost')).toBeInViewport();await expect(page.getByTestId('city-remove-cost')).toContainText('500,000');expect(await bytes(page)).toEqual(saved);await remove.click();
  await expect.poll(async()=>(await state(peer)).mixed.city.structures.length).toBe(0);expect((await state(page)).mixed.stationGraph.listeners).toEqual([]);
  await page.getByRole('button',{name:t.cityTask,exact:true}).click();await page.getByRole('button',{name:t.closeCatalog,exact:true}).click();
  await quote(page,t);await confirm.click();await expect.poll(async()=>(await state(peer)).mixed.city.structures.length).toBe(1);
  expect((await state(page)).mixed.city.structures[0].id).not.toBe(b.id);
  expect(errors).toEqual([]);
 }finally{await context.close();}
});

test('P7-09 Observer, clock advancement, late response and locale fences preserve city drafts and save',async({page,browser,baseURL})=>{
 test.setTimeout(90000);const t=texts[0],context=await browser.newContext({baseURL}),observer=await context.newPage();
 let server:any;const receipts:any[]=[];
 await observer.routeWebSocket('**/api/session?*',route=>{server=route.connectToServer();server.onMessage(m=>{receipts.push(JSON.parse(String(m)));route.send(m);});route.onMessage(m=>server.send(m));});
 try{
  await page.goto('/');await ready(page);const initial=await state(page);
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+initial.worldEpoch,{data:readFileSync(browserFixture('city'))})).ok()).toBe(true);await ready(page);
  expect((await observer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);await observer.goto('/');await ready(observer);
  await expect(observer.getByRole('button',{name:t.cityTask,exact:true})).toBeDisabled();
  await page.getByRole('button',{name:t.cityTask,exact:true}).click();await page.locator(`[data-catalog-variant="${house}"]`).click();await page.getByRole('button',{name:t.closeCatalog,exact:true}).click();
  let query:any;await page.route('**/api/city/fixed/preview?*',async r=>{query=r.request().postDataJSON();await r.continue();});
  await quote(page,t);const baseline=await bytes(page);
  server.send(JSON.stringify({type:'command',requestId:'observer-city',expectedWorldEpoch:query.expectedWorldEpoch,payload:{kind:'editFixedCity',query,expectedCost:'11000000'}}));
  await expect.poll(()=>receipts.find(r=>r.requestId==='observer-city')?.code).toBe('notAuthorized');expect(await bytes(page)).toEqual(baseline);
  const tick=BigInt((await state(page)).tick);
  await page.getByRole('button',{name:t.step,exact:false}).click();
  await expect.poll(async()=>BigInt((await state(page)).tick)).toBe(tick+1n);
  await expect(page.getByRole('button',{name:t.cityConfirm,exact:true})).toBeEnabled();
  const afterStep=await bytes(page);await page.unroute('**/api/city/fixed/preview?*');
  let release!:()=>void;const held=new Promise<void>(r=>{release=r;});let seen=false;
  await page.route('**/api/city/fixed/preview?*',async r=>{const response=await r.fetch();seen=true;await held;await r.fulfill({response});});
  await page.locator('.city-task form').getByRole('button',{name:t.stationCheck,exact:true}).click();await expect.poll(()=>seen).toBe(true);
  await page.getByRole('button',{name:t.cancelPreview,exact:true}).click();release();await expect(page.getByRole('button',{name:t.cityConfirm,exact:true})).toBeDisabled();expect(await bytes(page)).toEqual(afterStep);
  await page.unroute('**/api/city/fixed/preview?*');await quote(page,t);
  await openPanel(page,'settings');await page.getByLabel(t.language,{exact:true}).selectOption('ja');await closePanel(page);
  await expect(page.getByRole('button',{name:texts[1].cityConfirm,exact:true})).toBeDisabled();expect(await bytes(page)).toEqual(afterStep);
  await quote(page,texts[1]);await page.getByRole('button',{name:texts[1].cityConfirm,exact:true}).click();
  await expect.poll(async()=>(await state(observer)).mixed.city?.structures.length).toBe(1);
 }finally{await context.close();}
});

const apartment='{C7536844-7CD0-49e0-8982-2819F5792B32}';
const apartmentDefinition=JSON.parse(readFileSync('src/generated/city-profile.json','utf8')).structures.find((d:any)=>d.reference.recipeId===apartment);
for(const ja of [false,true])test(`P7-10 variable height ${ja?'JA narrow touch':'EN keyboard'} min max quotes natural scaled demand gray cut demolition peers and continuation`,async({page,browser,baseURL})=>{
 test.setTimeout(180000);const t=texts[ja?1:0],locale=ja?'ja':'en';if(ja)await page.setViewportSize({width:430,height:860});
 await page.addInitScript(ja=>localStorage.setItem('freetrain-online.locale',ja?'ja':'en'),ja);
 const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 const context=await browser.newContext({baseURL}),peer=await context.newPage();
 try{
  await page.goto('/');await ready(page,locale);const initial=await state(page);
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+initial.worldEpoch,{data:readFileSync(browserFixture('city'))})).ok()).toBe(true);await ready(page,locale);
  await page.getByRole('button',{name:t.stationTask,exact:true}).click();await page.locator(`[data-catalog-variant="${station.reference.recipeId}"]`).click();await page.getByRole('button',{name:t.closeCatalog,exact:true}).click();
  await page.getByLabel(t.stationX,{exact:true}).fill('12');await page.getByLabel(t.stationY,{exact:true}).fill('12');await page.locator('.station-task form').getByRole('button',{name:t.stationCheck,exact:true}).click();await page.getByRole('button',{name:t.stationConfirm,exact:true}).click();
  await expect.poll(async()=>(await state(page)).mixed.stationGraph?.buildings.length).toBe(1);
  expect((await peer.request.post('/api/access',{data:{secret:'operator-test'}})).ok()).toBe(true);await peer.goto('/');await ready(peer);
  await page.getByRole('button',{name:t.cityTask,exact:true}).click();await page.locator(`[data-catalog-variant="${apartment}"]`).click();await page.getByRole('button',{name:t.closeCatalog,exact:true}).click();
  const height=page.getByLabel(t.cityHeight,{exact:true}),confirm=page.getByRole('button',{name:t.cityConfirm,exact:true});
  await expect(height).toHaveAttribute('min','8');await expect(height).toHaveAttribute('max','20');await expect(height).toHaveAttribute('step','4');await expect(height).toHaveValue('8');
  const original=await bytes(page);await quote(page,t);await expect(page.locator('canvas')).toHaveAttribute('data-city-ghost-cells','2');
  await height.fill('20');await expect(confirm).toBeDisabled();expect(await bytes(page)).toEqual(original);
  await quote(page,t);await expect(page.locator('canvas')).toHaveAttribute('data-city-ghost-cells','5');await expect(page.getByTestId('city-cost')).toContainText((BigInt(apartmentDefinition.unitPrice)*5n).toLocaleString(locale));
  await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/city-variable-${locale}-preview.png`});await confirm.click();
  await expect.poll(async()=>(await state(peer)).mixed.city?.structures[0]?.height).toBe(5);expect((await state(page)).mixed.city.producers).toEqual([]);
  const siteSave=await bytes(page),before=await state(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+before.worldEpoch,{data:siteSave})).ok()).toBe(true);await ready(page,locale);expect(await bytes(page)).toEqual(siteSave);
  await page.getByLabel(t.gameSpeed,{exact:true}).selectOption('2');await page.getByRole('button',{name:t.run,exact:false}).click();
  await expect.poll(async()=>(await state(page)).mixed.city.producers.length,{timeout:120000}).toBe(1);await page.getByRole('button',{name:t.pause,exact:false}).click();
  const completed=await state(page),building=completed.mixed.city.structures[0],provider=completed.mixed.stationGraph.listeners[0].provider;
  expect(building.height).toBe(5);expect(building.construction).toEqual([]);expect(provider.input).toBe('city-variable');expect(provider.population.kind).toBe('MultiplierPopulation');expect(provider.population.factor).toBe(5);expect(provider.population.children[0].base).toBe(120);expect([provider.xySize,provider.zSize]).toEqual([1,5]);expect(provider.landValue).toBe(0);
  // 120 residents × source completion-hour percentage × height; cap is 0*1*max(1,5/4)+10.
  await openPanel(page,'stations',locale);await page.getByLabel(t.stationSelect,{exact:true}).selectOption(completed.mixed.stationGraph.buildings[0].id);await expect(page.getByTestId('station-demand')).toHaveText('10');await closePanel(page);
  const saved=await bytes(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+completed.worldEpoch,{data:saved})).ok()).toBe(true);await ready(page,locale);expect(await bytes(page)).toEqual(saved);
  await openPanel(page,'view',locale);await page.getByLabel(t.heightCut,{exact:true}).fill('2');await closePanel(page);
  const pixel=await exposeMapPoint(page,await point(page,{x:16*(14-11+12)+16,y:8*(12-(14-11))-32+8}));
  const rgba=await page.locator('canvas').evaluate((el,p)=>{const r=el.getBoundingClientRect();return [...(el as HTMLCanvasElement).getContext('2d')!.getImageData(Math.floor((p.x-r.x)*(el as HTMLCanvasElement).width/r.width),Math.floor((p.y-r.y)*(el as HTMLCanvasElement).height/r.height),1,1).data];},pixel);expect(rgba).toEqual([128,128,128,255]);
  if(ja)await page.touchscreen.tap(pixel.x,pixel.y);else await page.mouse.click(pixel.x,pixel.y);await expect(page.getByTestId('city-progress')).toHaveText(t.cityComplete);
  await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/city-variable-${locale}-cut-inspector.png`});
  await page.getByRole('button',{name:t.cityRemove,exact:true}).click();await page.getByRole('button',{name:t.confirmRemoval,exact:true}).click();await expect.poll(async()=>(await state(peer)).mixed.city.structures.length).toBe(0);expect((await state(page)).mixed.stationGraph.listeners).toEqual([]);
  await page.getByRole('button',{name:t.cityTask,exact:true}).click();await page.getByRole('button',{name:t.closeCatalog,exact:true}).click();await page.getByLabel(t.cityHeight,{exact:true}).fill('8');await quote(page,t);await confirm.click();
  await expect.poll(async()=>(await state(peer)).mixed.city?.structures[0]?.height).toBe(2);expect((await state(page)).mixed.city.structures[0].id).not.toBe(building.id);expect(errors).toEqual([]);
 }finally{await context.close();}
});

for(const ja of [false,true])test(`P7-11 half-building ${ja?'JA narrow touch':'EN keyboard'} all eight definitions sharing partial removal saved listener and peer publication`,async({page,browser,baseURL})=>{
 test.setTimeout(180000);const t=texts[ja?1:0],locale=ja?'ja':'en',halves=catalog.contributions.filter((c:any)=>c.type==='HalfVoxelStructure');
 if(ja)await page.setViewportSize({width:430,height:860});await page.addInitScript(l=>localStorage.setItem('freetrain-online.locale',l),locale);
 const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));const context=await browser.newContext({baseURL}),peer=await context.newPage();
 try{
  await page.goto('/');await ready(page,locale);const initial=await state(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+initial.worldEpoch,{data:readFileSync(browserFixture('city'))})).ok()).toBe(true);await ready(page,locale);
  await page.getByRole('button',{name:t.stationTask,exact:true}).click();await page.locator(`[data-catalog-variant="${station.reference.recipeId}"]`).click();await page.getByRole('button',{name:t.closeCatalog,exact:true}).click();
  await page.getByLabel(t.stationX,{exact:true}).fill('12');await page.getByLabel(t.stationY,{exact:true}).fill('12');await page.locator('.station-task form').getByRole('button',{name:t.stationCheck,exact:true}).click();await page.getByRole('button',{name:t.stationConfirm,exact:true}).click();
  await expect.poll(async()=>(await state(page)).mixed.stationGraph?.buildings.length).toBe(1);
  expect((await peer.request.post('/api/access',{data:{secret:'operator-test'}})).ok()).toBe(true);await peer.goto('/');await ready(peer);
  await page.getByRole('button',{name:t.cityTask,exact:true}).click();
  for(const [i,d] of halves.entries()){
   if(i)await page.getByRole('button',{name:t.chooseFromCatalog,exact:true}).click();await page.locator(`[data-catalog-variant="${d.id}"]`).click();await page.getByRole('button',{name:t.closeCatalog,exact:true}).click();
   await page.getByLabel(t.cityHalfAction,{exact:true}).selectOption('place');await page.getByLabel(t.cityHalfSide,{exact:true}).selectOption('back');await page.getByLabel(t.cityHalfDirection,{exact:true}).selectOption(String(i%2?2:0));
   const baseline=await bytes(page);await quote(page,t);await expect(page.getByTestId('city-cost')).toContainText(ja?'2,500,000':'2,500,000');expect(await bytes(page)).toEqual(baseline);
   const confirm=page.getByRole('button',{name:t.cityConfirm,exact:true});if(ja)await confirm.tap();else{await page.locator('canvas').focus();await page.keyboard.press('Enter');}
   await expect.poll(async()=>(await state(peer)).mixed.city?.structures.length).toBe(1);const first=await state(page),owner=first.mixed.city.structures[0],listener=first.mixed.stationGraph.listeners[0];
   expect(owner.construction).toEqual([]);expect(listener.provider.input).toBe('city-half');
   await page.locator('.city-task form').getByRole('button',{name:t.stationCheck,exact:true}).click();await expect(confirm).toBeDisabled();
   await page.getByLabel(t.cityHalfSide,{exact:true}).selectOption('fore');await page.getByLabel(t.cityHalfDirection,{exact:true}).selectOption(String(i%2?0:2));
   await page.locator('.city-task form').getByRole('button',{name:t.stationCheck,exact:true}).click();await expect(confirm).toBeDisabled();
   await page.getByLabel(t.cityHalfDirection,{exact:true}).selectOption(String(i%2?6:4));await quote(page,t);await expect(page.getByTestId('city-cost')).toContainText('¥0');await confirm.click();
   await expect.poll(async()=>(await state(peer)).mixed.city.structures[0].halves.length).toBe(2);expect((await state(page)).mixed.stationGraph.listeners[0]).toEqual(listener);
   await page.getByLabel(t.cityHalfAction,{exact:true}).selectOption('remove');await page.getByLabel(t.cityHalfSide,{exact:true}).selectOption('back');await quote(page,t);await expect(page.getByTestId('city-cost')).toContainText('¥0');await confirm.click();
   await expect.poll(async()=>(await state(peer)).mixed.city.structures[0].halves.length).toBe(1);expect((await state(page)).mixed.city.producers[0].initialDefinition).toEqual(owner.definition);expect((await state(page)).mixed.stationGraph.listeners[0]).toEqual(listener);
   if(i===0){const saved=await bytes(page),s=await state(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:saved})).ok()).toBe(true);await ready(page,locale);expect(await bytes(page)).toEqual(saved);await page.getByRole('button',{name:t.cityTask,exact:true}).click();await page.getByRole('button',{name:t.closeCatalog,exact:true}).click();}
   await page.getByLabel(t.cityHalfAction,{exact:true}).selectOption('remove');await page.getByLabel(t.cityHalfSide,{exact:true}).selectOption('fore');await quote(page,t);await expect(page.getByTestId('city-cost')).toContainText('300,000');
   if(i===7)await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/half-city-${locale}-removal.png`});await confirm.click();
   await expect.poll(async()=>(await state(peer)).mixed.city.structures.length).toBe(0);expect((await state(page)).mixed.stationGraph.listeners).toEqual([]);
  }
  // Real cut picking and whole-owner demolition use the same shared identity.
  await page.getByLabel(t.cityHalfAction,{exact:true}).selectOption('place');await page.getByLabel(t.cityHalfSide,{exact:true}).selectOption('back');await quote(page,t);await page.getByRole('button',{name:t.cityConfirm,exact:true}).click();
  await expect.poll(async()=>(await state(peer)).mixed.city.structures.length).toBe(1);
  await page.locator('canvas').focus();await page.keyboard.press('Escape');
  await openPanel(page,'view',locale);await page.getByLabel(t.heightCut,{exact:true}).fill('2');await closePanel(page);
  const pixel=await exposeMapPoint(page,await point(page,{x:16*(14-11+12)+16,y:8*(12-(14-11))-32+8}));
  const rgba=await page.locator('canvas').evaluate((el,p)=>{const r=el.getBoundingClientRect();return [...(el as HTMLCanvasElement).getContext('2d')!.getImageData(Math.floor((p.x-r.x)*(el as HTMLCanvasElement).width/r.width),Math.floor((p.y-r.y)*(el as HTMLCanvasElement).height/r.height),1,1).data];},pixel);expect(rgba).toEqual([146,94,42,255]);
  if(ja)await page.touchscreen.tap(pixel.x,pixel.y);else await page.mouse.click(pixel.x,pixel.y);
  await expect(page.getByTestId('city-halves')).toContainText(t.cityHalfBack);await expect(page.getByTestId('city-progress')).toHaveText(t.cityComplete);
  await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/half-city-${locale}-cut-inspector.png`});
  await page.getByRole('button',{name:t.cityRemove,exact:true}).click();await page.getByRole('button',{name:t.confirmRemoval,exact:true}).click();await expect.poll(async()=>(await state(peer)).mixed.city.structures.length).toBe(0);
  expect(errors).toEqual([]);
 }finally{await context.close();}
});

for(const ja of [false,true])test(`P7-12 complete catalog ${ja?'JA narrow':'EN'} all 129 identities art and authoritative quotes`,async({page})=>{
 test.setTimeout(240000);const locale=ja?'ja':'en',t=texts[ja?1:0];
 if(ja)await page.setViewportSize({width:430,height:860});await page.addInitScript(l=>localStorage.setItem('freetrain-online.locale',l),locale);
 const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto('/');await ready(page,locale);const initial=await state(page);
 expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+initial.worldEpoch,{data:readFileSync(browserFixture('city'))})).ok()).toBe(true);await ready(page,locale);
 const before=await bytes(page);await page.getByRole('button',{name:t.cityTask,exact:true}).click();
 const cat=page.locator('#building-catalog'),cards=cat.locator('[data-catalog-variant]'),search=cat.getByLabel(t.catalogSearch,{exact:true});
 await expect(cards).toHaveCount(129);
 const definitions=JSON.parse(readFileSync('src/generated/city-profile.json','utf8')).structures.filter((d:any)=>['fixed','variable','half'].includes(d.kind));
 const ids=await cards.evaluateAll(buttons=>buttons.map(b=>b.getAttribute('data-catalog-variant')));
 expect(new Set(ids)).toEqual(new Set(definitions.map((d:any)=>d.reference.recipeId)));
 await page.getByLabel(t.cityX,{exact:true}).fill('14');await page.getByLabel(t.cityY,{exact:true}).fill('12');
 const confirm=page.getByRole('button',{name:t.cityConfirm,exact:true}),check=page.locator('.city-task form').getByRole('button',{name:t.stationCheck,exact:true});
 for(const [i,d] of definitions.entries()){
  const id=d.reference.recipeId;
  await cat.locator(`[data-catalog-variant="${id}"]`).click();
  await expect(confirm).toBeDisabled();
  const preview=cat.locator('.catalog-active .catalog-preview');await expect(preview).toHaveAttribute('data-preview-id',id);
  const image=preview.locator('img');await expect(image).toHaveCount(1);await image.evaluate(el=>(el as HTMLImageElement).decode());
  expect(await image.evaluate(el=>{const c=document.createElement('canvas');c.width=96;c.height=80;const ctx=c.getContext('2d')!;ctx.drawImage(el as HTMLImageElement,0,0);return ctx.getImageData(0,0,96,80).data.some((v,i)=>i%4===3&&v>0);})).toBe(true);
  if(d.kind==='variable')await page.getByLabel(t.cityHeight,{exact:true}).fill(String((i%2?d.maximumHeight:d.minimumHeight)*4));
  if(d.kind==='half'){await page.getByLabel(t.cityHalfSide,{exact:true}).selectOption(i%2?'fore':'back');await page.getByLabel(t.cityHalfDirection,{exact:true}).selectOption(String(i%4*2));}
  const response=page.waitForResponse(r=>r.url().includes('/api/city/fixed/preview?'));await check.click();const quote=await(await response).json();
  expect(quote.legal,`${id}: ${quote.reason}`).toBe(true);expect(quote.query.definition).toEqual(d.reference);
  const height=d.kind==='variable'?(i%2?d.maximumHeight:d.minimumHeight):d.minimumHeight;
  expect(quote.cost).toBe(String(BigInt(d.unitPrice)*BigInt(d.kind==='variable'?height:1)));
  expect(quote.positions.length).toBe(d.kind==='half'?1:d.sizeX*d.sizeY*height);
  if(d.kind==='variable'){expect(quote.query.height).toBe(height);await expect(preview).toHaveAttribute('data-height',String(height));}
  if(d.kind==='half'){expect(quote.query.half).toEqual({side:i%2?'fore':'back',direction:i%4*2,color:0});await expect(preview).toHaveAttribute('data-side',i%2?'fore':'back');}
  await expect(confirm).toBeEnabled();
 }
 const group=cat.getByLabel(t.citySourceGroup,{exact:true}),subgroup=cat.getByLabel(t.citySourceSubgroup,{exact:true});
 await group.selectOption('半ボクセル建築');await subgroup.selectOption('古い民家');await expect(cards).toHaveCount(8);await expect(confirm).toBeEnabled();
 await search.fill(ja?'切妻二階建て':'Two-story gabled');await expect(cards).toHaveCount(8);
 await page.getByRole('button',{name:t.closeCatalog,exact:true}).click();await page.getByRole('button',{name:t.chooseFromCatalog,exact:true}).click();
 await expect(search).toHaveValue(ja?'切妻二階建て':'Two-story gabled');await expect(subgroup).toHaveValue('古い民家');await expect(confirm).toBeEnabled();
 await search.fill('no-such-definition');await expect(cards).toHaveCount(0);await expect(cat.getByText(t.catalogNoResults,{exact:true})).toBeVisible();
 await group.selectOption('*');await search.fill('');
 const manual=definitions.find((d:any)=>d.computerCannotBuild);await search.fill(manual.reference.recipeId);await expect(cards).toHaveCount(2); // family contains both exact baked orientations
 await cat.locator(`[data-catalog-variant="${manual.reference.recipeId}"]`).click();await expect(cat.locator('.catalog-active')).toContainText(t.cityManualOnly);
 await search.fill('');
 if(ja){await cat.getByRole('button',{name:t.expandPanel,exact:true}).click();expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);}
 await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/city-catalog-${locale}.png`});
 expect(await bytes(page)).toEqual(before);expect(errors).toEqual([]);
});

test('P7-12 observer catalog and peer edits preserve local choices while rejecting affected quotes',async({page,browser,baseURL})=>{
 test.setTimeout(90000);const t=texts[0];await page.goto('/');await ready(page);const initial=await state(page);
 expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+initial.worldEpoch,{data:readFileSync(browserFixture('city'))})).ok()).toBe(true);await ready(page);
 const definitions=JSON.parse(readFileSync('src/generated/city-profile.json','utf8')).structures;
 const variable=definitions.find((d:any)=>d.kind==='variable'&&d.sizeX===1&&d.sizeY===1&&d.maximumHeight>=4),half=definitions.find((d:any)=>d.kind==='half');
 const operatorContext=await browser.newContext({baseURL}),viewerContext=await browser.newContext({baseURL}),operator=await operatorContext.newPage(),viewer=await viewerContext.newPage();
 try{
  expect((await operator.request.post('/api/access',{data:{secret:'operator-test'}})).ok()).toBe(true);await operator.goto('/');await ready(operator);
  expect((await viewer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);await viewer.goto('/');await ready(viewer);
  await openPanel(viewer,'catalog');const viewerCatalog=viewer.locator('#building-catalog');await viewerCatalog.getByLabel(t.catalogCategory,{exact:true}).selectOption('city');
  await expect(viewerCatalog.locator('[data-catalog-variant]')).toHaveCount(129);await viewerCatalog.locator(`[data-catalog-variant="${variable.reference.recipeId}"]`).click();
  await expect(viewer.getByRole('button',{name:'Inspect',exact:true})).toHaveAttribute('aria-pressed','true');await expect(viewer.locator('.city-task')).toHaveCount(0);
  await page.getByRole('button',{name:t.cityTask,exact:true}).click();const cat=page.locator('#building-catalog');await cat.locator(`[data-catalog-variant="${variable.reference.recipeId}"]`).click();
  await page.getByLabel(t.cityHeight,{exact:true}).fill('12');await quote(page,t);const confirm=page.getByRole('button',{name:t.cityConfirm,exact:true});
  const source=await cat.locator('.catalog-active img').getAttribute('src');await page.getByLabel(t.cityHeight,{exact:true}).fill('16');await expect(confirm).toBeDisabled();
  await expect.poll(()=>cat.locator('.catalog-active img').getAttribute('src')).not.toBe(source);await quote(page,t);
  await cat.getByLabel(t.catalogCategory,{exact:true}).selectOption('signal');await expect(confirm).toBeEnabled();await cat.getByLabel(t.catalogCategory,{exact:true}).selectOption('city');
  await expect(page.getByLabel(t.cityHeight,{exact:true})).toHaveValue('16');await expect(confirm).toBeEnabled();
  await operator.getByRole('button',{name:t.cityTask,exact:true}).click();await operator.locator(`[data-catalog-variant="${half.reference.recipeId}"]`).click();await quote(operator,t);await operator.getByRole('button',{name:t.cityConfirm,exact:true}).click();
  await expect.poll(async()=>(await state(page)).mixed.city.structures.length).toBe(1);await expect(page.getByLabel(t.cityHeight,{exact:true})).toHaveValue('16');
  // Site leases are authoritatively re-reviewed on confirmation; unrelated
  // geometry and time updates intentionally retain an existing quote.
  const afterPeer=await bytes(page);await confirm.click();await expect(confirm).toBeDisabled();
  await expect(page.getByTestId('city-status')).toHaveText(t.railStale);expect(await bytes(page)).toEqual(afterPeer);
  await expect(viewerCatalog.locator(`[data-catalog-variant="${variable.reference.recipeId}"]`)).toHaveAttribute('aria-pressed','true');
  await cat.locator(`[data-catalog-variant="${half.reference.recipeId}"]`).click();await cat.locator(`[data-catalog-variant="${variable.reference.recipeId}"]`).click();
  await expect(page.getByLabel(t.cityHeight,{exact:true})).toHaveValue('16');await quote(page,t,16);await confirm.click();
  await expect.poll(async()=>(await state(operator)).mixed.city.structures.length).toBe(2);expect((await state(viewer)).mixed.city.structures.find((s:any)=>s.definition.recipeId===variable.reference.recipeId).height).toBe(4);
 }finally{await operatorContext.close();await viewerContext.close();}
});
