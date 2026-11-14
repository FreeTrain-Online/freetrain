import {execFileSync} from 'node:child_process';
import {readFileSync,writeFileSync} from 'node:fs';
import {test,expect,type Page,type WebSocketRoute} from './fixtures.ts';
import {canonicalState as state,ready,openPanel,closePanel} from './browser-helpers.ts';
import {settled} from './layout-helpers.ts';
const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
const profile=JSON.parse(readFileSync('src/generated/station-profile.json','utf8'));
const compat='protocolVersion=10&contentVersion=terrain-content-1&contentVersion=mixed-railway-content-1';
async function harness(page:Page) {
  const code=execFileSync('deno',['run','-A','scripts/bundle-rendering-tests.ts','tests/helpers/station-rendering-harness.ts'],{encoding:'utf8',maxBuffer:16*1024*1024});
  await page.route('**/__tests/station-rendering.js',r=>r.fulfill({contentType:'text/javascript',body:code}));
}
async function point(page:Page,x:number,y:number) {return page.locator('canvas').evaluate((el,p)=>{
  const r=el.getBoundingClientRect(),[cx,cy]=el.dataset.camera!.split(',').map(Number),z=Number(el.dataset.zoom);
  return {x:r.x+r.width/2+(p.x-cx)*z,y:r.y+r.height/2+(p.y-cy)*z};
},{x,y});}
test('all 35 station orientations match independent pinned XML crop pixels and alpha picks in eight variants',async({page})=>{
  test.setTimeout(180000);await harness(page);await page.goto('/');await ready(page);const reports=[];
  for(const season of ['spring','summer','autumn','winter'])for(const night of [false,true]){
    const r=await page.evaluate(async({catalog,season,night})=>{const {stationGallery}=await import('/__tests/station-rendering.js');return stationGallery(catalog,{season,night});},{catalog,season,night});
    expect(r.stations).toBe(35);expect(r.pixels).toBe(35*400*220);expect(r.picks).toBe(r.pixels);
    writeFileSync(`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/stations-${season}-${night?'night':'day'}.png`,Buffer.from(r.png.split(',')[1],'base64'));
    reports.push({season,night,stations:r.stations,pixels:r.pixels,picks:r.picks});
  }
  writeFileSync(`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/station-rendering-report.json`,JSON.stringify(reports,null,2));
});
test('station build/remove local raster and picks match fresh full scenes with overlapping train and terrain art',async({page})=>{
  test.setTimeout(90000);await harness(page);await page.goto('/');await ready(page);
  const reports=await page.evaluate(async catalog=>{const {stationLocalPixels}=await import('/__tests/station-rendering.js');return stationLocalPixels(catalog);},catalog);
  expect(reports).toHaveLength(2);
  for(const [i,r] of reports.entries()){
    expect(r.kind).toBe('local');expect(r.picks).toBe(15000);expect(r.columns.length).toBeGreaterThanOrEqual(2);
    writeFileSync(`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/station-local-${i?'removed':'built'}.png`,Buffer.from(r.png.split(',')[1],'base64'));
  }
});
test('real station art cut picks names and EN/JA presentation preserve canvas camera and cache across panels, peers and removal',async({page,browser,baseURL})=>{
  test.setTimeout(120000);
  const context=await browser.newContext({baseURL,locale:'ja-JP',viewport:{width:430,height:860},deviceScaleFactor:2}),peer=await context.newPage();
  let server:WebSocketRoute;const messages:any[]=[],errors:string[]=[];
  page.on('pageerror',e=>errors.push(String(e)));peer.on('pageerror',e=>errors.push(String(e)));
  await page.routeWebSocket('**/api/session?*',route=>{server=route.connectToServer();server.onMessage(m=>{messages.push(JSON.parse(String(m)));route.send(m);});route.onMessage(m=>server.send(m));});
  let serial=0;
  async function edit(action:string,building?:any) {
    const s=await state(page),q={expectedWorldEpoch:s.worldEpoch,expectedGeometryRevision:s.mixed?.geometryRevision??s.terrain.geometryRevision,expectedMinute:s.tick,expectedContentVersion:s.contentVersion,
      action,position:building?.location??{x:29,y:30,z:2},definition:action==='place'?type.reference:null,stationId:building?.id??'0',expectedStationRevision:building?.revision??'0'};
    const response=await page.request.post('/api/station/building/preview?'+compat,{data:q});expect(response.ok(),await response.text()).toBe(true);const quote=await response.json();expect(quote.legal,quote.reason).toBe(true);
    const requestId='station-art-'+(++serial);server.send(JSON.stringify({type:'command',requestId,expectedWorldEpoch:s.worldEpoch,payload:{kind:'editStationBuilding',query:q,expectedCost:quote.cost}}));
    await expect.poll(()=>messages.find(m=>m.requestId===requestId)?.type).toBe('accepted');
  }
  const type=profile.definitions.find((d:any)=>d.sizeX===1&&d.sizeY===2&&d.height===2);
  try{
    await page.addInitScript(()=>{const decode=HTMLImageElement.prototype.decode;(window as any).__stationDecodes=0;HTMLImageElement.prototype.decode=function(){(window as any).__stationDecodes++;return decode.call(this);};});
    await page.goto('/');await ready(page);
    const s=await state(page),r=await page.request.post('/api/worlds/empty/preview',{data:{name:'Station art',dimensions:{h:48,v:40,z:8},waterLevel:2,epoch:s.worldEpoch,revision:s.revision}}),c=await r.json();
    expect((await page.request.post('/api/worlds/empty/commit',{data:{id:c.id,requestHash:c.requestHash,candidateHash:c.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).ok()).toBe(true);await ready(page);
    await peer.goto('/');await ready(peer,'ja');
    const canvas=page.locator('canvas');await canvas.evaluate(el=>el.dataset.instance='station-preserved');const camera=await canvas.getAttribute('data-camera');
    await edit('place');await expect(canvas).toHaveAttribute('data-presentation','ready');
    const built=await state(page),b=built.mixed.stationGraph.buildings[0];await expect.poll(async()=>(await state(peer)).mixed.stationGraph).toEqual(built.mixed.stationGraph);
    // The HTTP snapshot can arrive before the socket delta and its new raster.
    await expect(canvas).toHaveAttribute('data-render-revision',built.revision);
    await expect(canvas).toHaveAttribute('data-presentation','ready');
    const bytes=await(await page.request.get('/api/saves/download')).body();
    const f=catalog.renderRecipes.find((r:any)=>r.id===type.reference.recipeId).frames.find((f:any)=>f.cell[1]===1&&f.cell[2]===1),mask=catalog.masks[f.masks['spring/day']];
    const row=mask.findIndex((s:string)=>s.includes('1')),col=mask[row].indexOf('1'),x=b.location.x-19,y=b.location.y+1;
    const pixel={x:16*(x+y)-f.anchor[0]+col+.5,y:8*(y-x)-16*(b.location.z+1)-f.anchor[1]+row+.5};
    const p=await point(page,pixel.x,pixel.y);await page.mouse.click(p.x,p.y);await expect(page.getByTestId('selected-object')).toContainText('Station · '+b.id);await expect(page.getByTestId('confirmed-name')).toHaveText(b.name);
    expect(await canvas.getAttribute('data-camera')).toBe(camera);
    let sheets=0;page.on('request',r=>{if(r.url().includes('/content/')&&r.url().endsWith('.png'))sheets++;});
    const initialDecodes=await page.evaluate(()=>(window as any).__stationDecodes);
    await openPanel(page,'view');await settled(page);const named=await canvas.evaluate(el=>el.toDataURL());
    await page.getByLabel('Show station names',{exact:true}).uncheck();
    // Checkbox state changes before the next canvas paint. Observe both pixel
    // transitions so the hidden baseline cannot still contain station names.
    await expect.poll(async()=>(await canvas.evaluate(el=>el.toDataURL()))!==named).toBe(true);
    await page.getByLabel('Show station names',{exact:true}).check();
    await expect.poll(async()=>(await canvas.evaluate(el=>el.toDataURL()))===named).toBe(true);
    await page.getByLabel('Station name font',{exact:true}).selectOption('serif');await page.getByLabel('Station name size',{exact:true}).fill('18');await page.getByLabel('Station name size',{exact:true}).blur();
    expect(await page.evaluate(()=>(window as any).__stationDecodes)).toBe(initialDecodes);
    await page.getByLabel('Height cut',{exact:true}).fill('2');await expect(canvas).toHaveAttribute('data-terrain-cut','2');await expect(canvas).toHaveAttribute('data-presentation','ready');await closePanel(page);
    const cut=await point(page,16*(b.location.x-19+b.location.y)+16,8*(b.location.y-b.location.x+19)-32+8);await page.mouse.click(cut.x,cut.y);
    await expect(page.getByTestId('selected-object')).toContainText('Station · '+b.id);await expect(page.getByTestId('confirmed-name')).toHaveText(b.name);
    await expect(page.getByTestId('selected-object')).toContainText(`${b.location.x}, ${b.location.y}, 2`);
    const gray=await canvas.evaluate((el,p)=>{const r=el.getBoundingClientRect(),x=(p.x-r.x)*el.width/r.width,y=(p.y-r.y)*el.height/r.height;return [...el.getContext('2d')!.getImageData(Math.floor(x),Math.floor(y),1,1).data];},cut);
    // Selection has a hollow outline; the center must be the source Gray cap.
    expect(gray).toEqual([128,128,128,255]);
    await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/station-en-cut.png`});
    await openPanel(page,'view');await page.getByLabel('Height cut',{exact:true}).fill('7');await expect(canvas).toHaveAttribute('data-terrain-cut','7');await expect(canvas).toHaveAttribute('data-presentation','ready');
    await page.getByLabel('Night',{exact:true}).check();await page.getByLabel('Winter ground',{exact:true}).check();await expect(canvas).toHaveAttribute('data-terrain-variant','winter/night');await expect(canvas).toHaveAttribute('data-presentation','ready');
    await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/station-en-night.png`});
    await openPanel(peer,'view','ja');await peer.getByLabel('駅名を表示',{exact:true}).uncheck();await peer.getByLabel('駅名を表示',{exact:true}).check();await closePanel(peer);
    const jp=await point(peer,pixel.x,pixel.y);await peer.mouse.click(jp.x,jp.y);await expect(peer.getByTestId('selected-object')).toContainText('駅 · '+b.id);
    await peer.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/station-ja-narrow.png`});
    await openPanel(page,'session');await openPanel(page,'help');await expect(canvas).toHaveAttribute('data-instance','station-preserved');expect(await canvas.getAttribute('data-camera')).toBe(camera);
    const decoded=sheets,decodeCount=await page.evaluate(()=>(window as any).__stationDecodes);await openPanel(page,'session');await openPanel(page,'help');expect(sheets).toBe(decoded);expect(await page.evaluate(()=>(window as any).__stationDecodes)).toBe(decodeCount);expect((await(await page.request.get('/api/saves/download')).body())).toEqual(bytes);
    await closePanel(page);await edit('remove',b);await expect(canvas).toHaveAttribute('data-presentation','ready');await expect(canvas).toHaveAttribute('data-terrain-update','local');
    await expect.poll(async()=>(await state(peer)).mixed.stationGraph.buildings.length).toBe(0);
    const gone=await point(page,pixel.x,pixel.y);await page.mouse.click(gone.x,gone.y);await expect(page.getByTestId('selected-object')).not.toContainText('Station · '+b.id);
    expect(errors).toEqual([]);
  }finally{await context.close();}
});
