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
