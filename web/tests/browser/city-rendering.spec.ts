import {execFileSync} from 'node:child_process';
import {readFileSync,writeFileSync} from 'node:fs';
import {test,expect} from './fixtures.ts';
import {ready,openPanel,closePanel,canonicalState} from './browser-helpers.ts';
const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
async function harness(page:any){
 const code=execFileSync('deno',['run','-A','scripts/bundle-rendering-tests.ts','tests/helpers/city-rendering-harness.ts'],{encoding:'utf8',maxBuffer:16*1024*1024});
 await page.route('**/__tests/city-rendering.js',(r:any)=>r.fulfill({contentType:'text/javascript',body:code}));
}
const save=(name:string,png:string)=>writeFileSync(`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/${name}.png`,Buffer.from(png.split(',')[1],'base64'));
test('all 66 fixed definitions match independent pinned XML raw pixels and alpha picks in eight appearances',async({page})=>{
 test.setTimeout(240000);await harness(page);await page.goto('/');await ready(page);const reports=[];
 for(const season of ['spring','summer','autumn','winter'])for(const night of [false,true]){
  const r=await page.evaluate(async({catalog,season,night})=>{const {cityGallery}=await import('/__tests/city-rendering.js');return cityGallery(catalog,{season,night});},{catalog,season,night});
  expect(r.definitions).toBe(66);expect(r.pixels).toBe(66*400*300);expect(r.picks).toBe(r.pixels);save(`city-${season}-${night?'night':'day'}`,r.png);reports.push({...r,png:undefined,season,night});
 }
 writeFileSync(`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/city-rendering-report.json`,JSON.stringify(reports,null,2));
});
test('all saved construction stages grounds and doors match literal source sheet geometry and alpha picks',async({page})=>{
 test.setTimeout(180000);await harness(page);await page.goto('/');await ready(page);const reports=[];
 for(const night of [false,true]){
  const r=await page.evaluate(async({catalog,night})=>{const {constructionGallery}=await import('/__tests/city-rendering.js');return constructionGallery(catalog,{season:'spring',night});},{catalog,night});
  expect(r.cases).toBe(70);expect(r.picks).toBe(70*300*220);save(`city-construction-${night?'night':'day'}`,r.png);reports.push({...r,png:undefined,night});
 }
 writeFileSync(`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/city-construction-report.json`,JSON.stringify(reports,null,2));
});
test('real site completion renders canonical owners in EN and JA narrow DPR2 and preserves saves',async({page,browser,baseURL})=>{
 test.setTimeout(120000);const seed=readFileSync('tests/fixtures/fixed-city-v11.json'),context=await browser.newContext({baseURL,locale:'ja-JP',viewport:{width:430,height:860},deviceScaleFactor:2}),peer=await context.newPage(),errors:string[]=[];
 for(const p of [page,peer])p.on('pageerror',e=>errors.push(String(e)));
 try{
  await page.goto('/');await ready(page);const initial=await canonicalState(page);
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+initial.worldEpoch,{data:seed})).ok()).toBe(true);await ready(page);await peer.goto('/');await ready(peer,'ja');
  const before=await canonicalState(page),site=before.mixed.city.structures[0];
  async function pick(p:any,b:any,locale='en',close=true){
   const center=(before.map.height-2)/2;let pixel;
   if(b.construction.length){const f=catalog.renderRecipes.find((r:any)=>r.id==='system:construction/bones/2/0000').frames[0],mask=catalog.masks[f.masks['winter/day']],y=mask.findIndex((r:string)=>r.includes('1')),x=mask[y].indexOf('1');pixel={x:16*(b.location.x-center+b.location.y)-f.anchor[0]+x+.5,y:8*(b.location.y-b.location.x+center)-16*b.location.z-f.anchor[1]+y+.5};}
   else{const r=catalog.renderRecipes.find((r:any)=>r.id===b.definition.recipeId),f=r.frames.find((f:any)=>f.status==='ready'),mask=catalog.masks[f.masks['spring/day']],y=mask.findIndex((r:string)=>r.includes('1')),x=mask[y].indexOf('1');pixel={x:16*(b.location.x-center+f.cell[0]+b.location.y+f.cell[1])-f.anchor[0]+x+.5,y:8*(b.location.y+f.cell[1]-b.location.x+center-f.cell[0])-16*(b.location.z+f.cell[2])-f.anchor[1]+y+.5};}
   const c=p.locator('canvas');await expect(c).toHaveAttribute('data-presentation','ready');
   const point=await c.evaluate((el:any,p:any)=>{const r=el.getBoundingClientRect(),[cx,cy]=el.dataset.camera.split(',').map(Number),z=Number(el.dataset.zoom);return {x:r.x+r.width/2+(p.x-cx)*z,y:r.y+r.height/2+(p.y-cy)*z};},pixel);
   await p.mouse.click(point.x,point.y);await expect(p.getByTestId('selected-object')).toContainText((locale==='ja'?'建物':'Building')+' · '+b.id);
   await p.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/city-${locale}-${b.construction.length?'site':'completed'}.png`});if(close)await closePanel(p);
  }
  // The naturally constructed source fixture sits near the west map edge.
  // Pan through the real map gesture before inspecting its alpha pixels.
  for(const p of [page,peer]){
   const c=p.locator('canvas'),r=await c.boundingBox(),[cx,cy]=(await c.getAttribute('data-camera'))!.split(',').map(Number),zoom=Number(await c.getAttribute('data-zoom'));
   const center=Math.trunc((before.map.height-1)/2),dx=(cx-(16*(site.location.x-center+site.location.y)+16))*zoom,dy=(cy-(8*(site.location.y-site.location.x+center)-16*site.location.z+8))*zoom;
   // Several drags stay inside the narrow viewport and preserve the map's pointer capture.
   const count=Math.ceil(Math.max(Math.abs(dx)/(r!.width*.6),Math.abs(dy)/(r!.height*.6),1));
   for(let i=0;i<count;i++){await p.mouse.move(r!.x+r!.width*.2,r!.y+r!.height*.4);await p.mouse.down({button:'middle'});await p.mouse.move(r!.x+r!.width*.2+dx/count,r!.y+r!.height*.4+dy/count,{steps:5});await p.mouse.up({button:'middle'});}

  }
  await pick(page,site,'en',false);await pick(peer,site,'ja',false);
  await page.getByRole('button',{name:'▶| Single step',exact:true}).click();
  await expect.poll(async()=>(await canonicalState(page)).mixed.city.structures[0].construction.length).toBe(0);
  const after=await canonicalState(page),b=after.mixed.city.structures[0];expect(b.id).not.toBe(site.id);
  for(const p of [page,peer]){await expect(p.getByTestId('city-progress')).toHaveText(p===page?'Completed':'完成');await expect(p.getByTestId('selected-object')).toContainText(' · '+b.id);await closePanel(p);}
  for(const p of [page,peer]){await expect(p.locator('canvas')).toHaveAttribute('data-render-revision',after.revision);await expect(p.locator('canvas')).toHaveAttribute('data-presentation','ready');}
  const bytes=await(await page.request.get('/api/saves/download')).body();await pick(page,b);await pick(peer,b,'ja');
  const canvas=page.locator('canvas'),camera=await canvas.getAttribute('data-camera');
  await openPanel(page,'view');await page.getByLabel('Height cut',{exact:true}).fill('2');await page.getByLabel('Night',{exact:true}).check();await page.getByLabel('Winter ground',{exact:true}).check();
  await expect(canvas).toHaveAttribute('data-terrain-cut','2');await expect(canvas).toHaveAttribute('data-terrain-variant','winter/night');await expect(canvas).toHaveAttribute('data-presentation','ready');await closePanel(page);
  const center=Math.trunc((before.map.height-1)/2),pixel={x:16*(b.location.x-center+b.location.y)+16,y:8*(b.location.y-b.location.x+center)-16*b.location.z+8};
  const point=await canvas.evaluate((el:any,p:any)=>{const r=el.getBoundingClientRect(),[cx,cy]=el.dataset.camera.split(',').map(Number),z=Number(el.dataset.zoom);return {x:r.x+r.width/2+(p.x-cx)*z,y:r.y+r.height/2+(p.y-cy)*z};},pixel);
  await page.mouse.click(point.x,point.y);await expect(page.getByTestId('selected-object')).toContainText('Building · '+b.id);
  const rgba=await canvas.evaluate((el:any,p:any)=>{const r=el.getBoundingClientRect();return [...el.getContext('2d').getImageData(Math.floor((p.x-r.x)*el.width/r.width),Math.floor((p.y-r.y)*el.height/r.height),1,1).data];},point);expect(rgba).toEqual([179,115,51,255]);
  await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/city-en-forced-cut.png`});expect(await canvas.getAttribute('data-camera')).toBe(camera);
  expect(await(await page.request.get('/api/saves/download')).body()).toEqual(bytes);expect(errors).toEqual([]);
 }finally{await context.close();}
});
test('city site stage completion and removal local pixels and picks match full scenes beside trains and stations',async({page})=>{
 test.setTimeout(90000);await harness(page);await page.goto('/');await ready(page);
 const reports=await page.evaluate(async catalog=>{const {cityLocalPixels}=await import('/__tests/city-rendering.js');return cityLocalPixels(catalog);},catalog);
 expect(reports).toHaveLength(4);for(const [i,r] of reports.entries()){expect(r.kind).toBe('local');expect(r.picks).toBe(30000);expect(r.columns.length).toBeGreaterThanOrEqual(4);save('city-local-'+i,r.png);}
});
test('every fixed footprint including invisible members paints and picks source brown cut pixels at forced winter night',async({page})=>{
 await harness(page);await page.goto('/');await ready(page);
 const r=await page.evaluate(async catalog=>{const {cityCutGallery}=await import('/__tests/city-rendering.js');return cityCutGallery(catalog);},catalog);
 expect(r.definitions).toBe(66);expect(r.members).toBeGreaterThan(150);save('city-cut-winter-night',r.png);
});
