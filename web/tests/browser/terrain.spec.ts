import {test,expect,type Page} from '@playwright/test';
import {readFileSync,mkdirSync} from 'node:fs';
import {join} from 'node:path';
import {ready,openPanel,closePanel} from './ux-helpers.ts';
const boot='/api/bootstrap?protocolVersion=4&contentVersion=reference-spatial-content-2&contentVersion=terrain-content-1';
const state=async(page:Page)=>(await(await page.request.get(boot)).json()).snapshot;
const save=readFileSync('../prototype/protocol/saves/terrain-v6.json');
async function replace(page:Page,bytes:Buffer){const s=await state(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:bytes})).ok()).toBe(true);}
async function point(page:Page,x:number,y:number){return page.locator('canvas').evaluate((canvas,args)=>{
 const r=canvas.getBoundingClientRect(),[cx,cy]=(canvas as HTMLCanvasElement).dataset.camera!.split(',').map(Number),z=Number((canvas as HTMLCanvasElement).dataset.zoom);
 return {x:r.x+r.width/2+(args.x-cx)*z,y:r.y+r.height/2+(args.y-cy)*z};
},{x,y});}

test('real terrain save replacement renders sea/cut/forest, picks independently, retains canvas and image cache across view/panel changes, then reopens',async({page})=>{
 await page.goto('/');await ready(page);const original=await(await page.request.get('/api/saves/download')).body();
 const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 const canvas=page.locator('canvas');await canvas.evaluate(el=>el.setAttribute('data-instance','terrain-preserved'));
 let sheets=0;page.on('request',r=>{if(r.url().includes('/content/bundled-v2/')&&r.url().endsWith('.png'))sheets++;});
 try {
  await replace(page,save);await expect(canvas).toHaveAttribute('data-ruleset','terrain-state-1');await expect(canvas).toHaveAttribute('data-assets','ready');await ready(page);
  await expect(canvas).toHaveAttribute('data-instance','terrain-preserved');await expect(page.getByTestId('reference-world')).toHaveText('Terrain world');
  await expect(page.getByRole('button',{name:'Remove track',exact:true})).toBeDisabled();
  // H0,V0 at water z3: source centre 16,-40; no sprite or land at this height.
  const q=await point(page,16,-40);await page.mouse.click(q.x,q.y);await expect(page.getByTestId('selected-object')).toHaveText('Terrain · cell:0,0 · 1, 0, 3');
  await openPanel(page,'view');await page.getByLabel('Height cut',{exact:true}).fill('4');await expect(canvas).toHaveAttribute('data-assets','ready');
  await expect.poll(()=>sheets).toBeGreaterThan(0);
  const terrainHash=await canvas.evaluate(el=>el.toDataURL());
  await page.getByLabel('Night',{exact:true}).check();await expect(canvas).toHaveAttribute('data-terrain-variant','spring/night');await expect.poll(async()=>canvas.evaluate(el=>el.toDataURL())).not.toBe(terrainHash);
  await page.getByLabel('Winter ground',{exact:true}).check();await expect(canvas).toHaveAttribute('data-terrain-variant','winter/night');
  await page.getByLabel('Night',{exact:true}).uncheck();await page.getByLabel('Winter ground',{exact:true}).uncheck();
  await expect.poll(async()=>canvas.evaluate(el=>el.toDataURL())).toBe(terrainHash);
  const decoded=sheets;
  for(let i=0;i<3;i++){await openPanel(page,'help');await openPanel(page,'view');}
  expect(sheets).toBe(decoded);await expect(canvas).toHaveAttribute('data-instance','terrain-preserved');
  await closePanel(page);
  // H0,V0's realized cedar at X0,Y0 is visible above the slope, independently of its diamond.
  const tree=await point(page,0,-58);await page.mouse.click(tree.x,tree.y);await expect(page.getByTestId('selected-object')).toContainText('Terrain · 2 · 1, 0, 2');
  const directory=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'terrain');mkdirSync(directory,{recursive:true});await page.screenshot({path:join(directory,'terrain-cut-en.png'),fullPage:true});
  const captured=await(await page.request.get('/api/saves/download')).body();expect(JSON.parse(captured.toString()).terrain.occupants[1].forest.trees).toEqual([{x:0,y:0,sprite:0},{x:30,y:15,sprite:8}]);
  await page.reload();await ready(page);await expect(canvas).toHaveAttribute('data-ruleset','terrain-state-1');await expect(canvas).toHaveAttribute('data-assets','ready');expect(await(await page.request.get('/api/saves/download')).body()).toEqual(captured);
  expect(errors).toEqual([]);
 }finally{await replace(page,original);}
});

test('terrain Japanese narrow DPR2 view and failed recipe decoding gate the live reveal',async({browser})=>{
 const context=await browser.newContext({viewport:{width:640,height:900},deviceScaleFactor:2,locale:'ja-JP'}),page=await context.newPage();
 await page.goto('/');await ready(page,'ja');const original=await(await page.request.get('/api/saves/download')).body();
 try{
  await replace(page,save);await expect(page.locator('canvas')).toHaveAttribute('data-ruleset','terrain-state-1');await ready(page,'ja');
  await openPanel(page,'view','ja');await page.getByLabel('高さ表示',{exact:true}).fill('4');await expect(page.locator('canvas')).toHaveAttribute('data-assets','ready');
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  const directory=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'terrain');mkdirSync(directory,{recursive:true});await page.screenshot({path:join(directory,'terrain-cut-ja-dpr2.png'),fullPage:true});
  await page.route('**/content/bundled-v2/**/*.png',route=>route.abort());
  await page.reload();await expect(page.locator('.command-bar')).toHaveCount(0);await expect(page.locator('canvas')).not.toHaveAttribute('data-assets','ready');
 }finally{await replace(page,original);await context.close();}
});

test('late night-sheet decoding cannot replace a newer day view or its hit targets',async({page})=>{
 await page.goto('/');await ready(page);const original=await(await page.request.get('/api/saves/download')).body();
 let release=()=>{},waiting=0;const held=new Promise<void>(r=>{release=r;});
 try{
  await replace(page,save);await expect(page.locator('canvas')).toHaveAttribute('data-ruleset','terrain-state-1');await ready(page);
  await openPanel(page,'view');const canvas=page.locator('canvas');await page.getByLabel('Height cut',{exact:true}).fill('4');await expect.poll(()=>canvas.evaluate(el=>el.dataset.terrainCut)).toBe('4');
  const before=await canvas.evaluate(el=>el.toDataURL());
  await page.route('**/content/bundled-v2/**/*.png',async route=>{const response=await route.fetch();waiting++;await held;await route.fulfill({response});});
  await page.getByLabel('Night',{exact:true}).check();await expect.poll(()=>waiting).toBeGreaterThan(0);await expect(canvas).toHaveAttribute('data-assets','loading');
  await page.getByLabel('Night',{exact:true}).uncheck();await expect.poll(()=>canvas.evaluate(el=>el.toDataURL())).toBe(before);
  const received=page.waitForResponse(r=>r.url().includes('/content/bundled-v2/')&&r.url().endsWith('.png'));release();await received;
  await page.evaluate(()=>new Promise<void>(resolve=>requestAnimationFrame(()=>requestAnimationFrame(()=>resolve()))));
  await expect(canvas).toHaveAttribute('data-terrain-variant','spring/day');expect(await canvas.evaluate(el=>el.toDataURL())).toBe(before);
  await closePanel(page);const q=await point(page,0,-58);await page.mouse.click(q.x,q.y);await expect(page.getByTestId('terrain-surface')).toHaveText('Forest');
 }finally{release();await replace(page,original);}
});
