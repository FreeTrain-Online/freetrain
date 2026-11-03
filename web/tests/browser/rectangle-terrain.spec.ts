import { expect,test,type Page } from './fixtures.ts';
import { canonicalState,closePanel,openPanel,ready } from './browser-helpers.ts';
const compat='protocolVersion=6&contentVersion=terrain-content-1';
const state=canonicalState;
const save=async(page:Page)=>(await page.request.get('/api/saves/download')).body();
async function replace(page:Page,bytes:Buffer){const s=await state(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:bytes})).ok()).toBe(true);}
async function empty(page:Page,locale='en'){
 const s=await state(page);const response=await page.request.post('/api/worlds/empty/preview',{data:{name:'Rectangles',dimensions:{h:30,v:42,z:6},waterLevel:1,epoch:s.worldEpoch,revision:s.revision}});expect(response.ok()).toBe(true);
 const c=await response.json();expect((await page.request.post('/api/worlds/empty/commit',{data:{id:c.id,requestHash:c.requestHash,candidateHash:c.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).ok()).toBe(true);
 await expect(page.locator('canvas')).toHaveAttribute('data-ruleset','terrain-state-1');await ready(page,locale);
}
async function tool(page:Page,locale='en'){
 await page.getByRole('button',{name:locale==='ja'?'地形操作':'Modify terrain',exact:true}).click();
 await page.getByLabel(locale==='ja'?'地形の選択方法':'Terrain selection',{exact:true}).selectOption('rectangle');
}
async function target(page:Page,locale='en',action='raise',start=[20,20,1],end=[21,21]){
 await page.getByLabel(locale==='ja'?'地形の操作':'Terrain action',{exact:true}).selectOption(action);
 for(const [label,value] of [[locale==='ja'?'隅のセル X':'Corner cell X',start[0]],[locale==='ja'?'隅のセル Y':'Corner cell Y',start[1]],[locale==='ja'?'隅のセル Z':'Corner cell Z',start[2]],[locale==='ja'?'終点 X':'End X',end[0]],[locale==='ja'?'終点 Y':'End Y',end[1]]] as const)
  await page.getByLabel(label,{exact:true}).fill(String(value));
 await page.getByRole('button',{name:locale==='ja'?'矩形をプレビュー':'Preview rectangle',exact:true}).click();
}
const confirm=(page:Page,locale='en')=>page.getByRole('button',{name:locale==='ja'?'地形の変更を確定':'Confirm terrain change',exact:true});
async function pixel(page:Page,x:number,y:number){return page.locator('canvas').evaluate((canvas,args)=>{
 const [cx,cy]=(canvas as HTMLCanvasElement).dataset.camera!.split(',').map(Number),zoom=Number((canvas as HTMLCanvasElement).dataset.zoom),r=canvas.getBoundingClientRect();
 return {x:r.x+r.width/2+(16*(args.x+args.y-20)+16-cx)*zoom,y:r.y+r.height/2+(8*(-args.x+args.y+20)-16+3-cy)*zoom};
},{x,y});}

test('rectangle reversed coordinates preview exact fours, preserve canvas and cash, confirm once and reopen exact Save7',async({page})=>{
 await page.goto('/');await ready(page);const original=await save(page);
 try{
  await empty(page);const before=await save(page);const cash=(await state(page)).cash;
  const canvas=page.locator('canvas');await canvas.evaluate(el=>el.setAttribute('data-instance','rectangle'));
  await tool(page);await target(page,'en','raise',[21,21,1],[20,20]);
  await expect(page.getByTestId('rectangle-status')).toContainText('change is legal');await expect(page.locator('.corner-changes li')).toHaveCount(4);
  await expect(page.locator('.corner-changes')).toContainText('[0,4,0,0]');expect(await save(page)).toEqual(before);
  await confirm(page).click();await expect.poll(async()=>JSON.parse((await save(page)).toString()).packedTerrain.occupantCount).toBe(4);
  await expect(confirm(page)).toBeDisabled();expect((await state(page)).cash).toBe(cash);await expect(canvas).toHaveAttribute('data-instance','rectangle');
  const after=await save(page);await page.reload();await ready(page);expect(await save(page)).toEqual(after);
  await tool(page);await target(page,'en','lower');await expect(page.getByTestId('rectangle-status')).toContainText('Adjust the other corners');await expect(confirm(page)).toBeDisabled();
 }finally{await replace(page,original);}
});

test('rectangle pointer two picks use Shift upper profile and Ctrl repair; Escape and panels clear reviews',async({page})=>{
 await page.goto('/');await ready(page);const original=await save(page);
 try{
  await empty(page);await tool(page);const a=await pixel(page,20,20),b=await pixel(page,21,21);
  await page.mouse.click(a.x,a.y);await expect(page.getByTestId('rectangle-status')).toContainText('opposite endpoint');
  await page.keyboard.down('Shift');await page.mouse.click(b.x,b.y);await page.keyboard.up('Shift');
  await expect(page.getByTestId('rectangle-status')).toContainText('change is legal');await expect(page.locator('.corner-changes')).toContainText('[0,2,0,0]');
  await page.keyboard.press('Escape');await expect(confirm(page)).toBeDisabled();
  await page.mouse.click(a.x,a.y);await page.keyboard.down('Control');await page.mouse.click(b.x,b.y);await page.keyboard.up('Control');
  await expect(page.getByTestId('rectangle-position')).toContainText('Repair corners');await expect(page.getByTestId('rectangle-status')).toContainText('No change');
  await target(page);await openPanel(page,'help');await closePanel(page);await expect(confirm(page)).toBeDisabled();
 }finally{await replace(page,original);}
});

test('explicit repair fixes a mismatched cut cell inclusively and reports unchanged cells as skips',async({page})=>{
 await page.goto('/');await ready(page);const original=await save(page);
 try{
  await empty(page);const cut=JSON.parse((await save(page)).toString());const cells=Buffer.from(cut.packedTerrain.cells,'base64');cells[20*30+10]=64;cut.packedTerrain.cells=cells.toString('base64');
  await replace(page,Buffer.from(JSON.stringify(cut)));await ready(page);await tool(page);
  const before=await save(page);await target(page,'en','repair',[20,20,1],[21,21]);
  await expect(page.getByTestId('rectangle-status')).toContainText('change is legal');await expect(page.locator('.corner-changes li')).toHaveCount(1);
  await expect(page.locator('.tool-card')).toContainText('Skipped sites: 3');await expect(page.locator('.corner-changes')).toContainText('0 [0,0,0,0] → 1 [0,0,0,0]');expect(await save(page)).toEqual(before);
  await confirm(page).click();await expect.poll(async()=>Buffer.from(JSON.parse((await save(page)).toString()).packedTerrain.cells,'base64')[610]).toBe(65);
  await target(page,'en','repair',[20,20,1],[20,20]);await expect(page.getByTestId('rectangle-status')).toContainText('No change');await expect(confirm(page)).toBeDisabled();
 }finally{await replace(page,original);}
});

test('rectangle malformed, oversized and stale requests preserve state; delayed previews and peer commits cannot retarget review',async({page,browser})=>{
 await page.goto('/');await ready(page);const original=await save(page);const peer=await browser.newPage();let release=()=>{};const held=new Promise<void>(r=>{release=r;});
 try{
  await empty(page);const s=await state(page),q={expectedWorldEpoch:s.worldEpoch,expectedGeometryRevision:s.terrain.geometryRevision,position:{x:20,y:20,z:1},action:'raise',offsetX:null,offsetY:null,stroke:null,rectangle:{end:{x:21,y:21,z:1},soft:false}};
  const before=await save(page);
  for(const patch of [{rectangle:{end:{x:30,y:30,z:1},soft:false}},{rectangle:{end:{x:21,y:21,z:2},soft:false}},{offsetX:1,offsetY:0}])
   expect((await page.request.post('/api/terrain/corner/preview?'+compat,{data:{...q,...patch}})).status()).toBe(400);
  expect(await save(page)).toEqual(before);await tool(page);
  let delayed=false;await page.route('**/api/terrain/corner/preview?*',async route=>{const response=await route.fetch();if(!delayed){delayed=true;await held;}await route.fulfill({response});});
  await target(page);await expect.poll(()=>delayed).toBe(true);await page.getByLabel('End X',{exact:true}).fill('22');await page.getByRole('button',{name:'Preview rectangle',exact:true}).click();
  await expect(page.getByTestId('rectangle-position')).toContainText('22, 21, 1');release();await expect(page.getByTestId('rectangle-position')).toContainText('22, 21, 1');
  await peer.goto('/');await ready(peer);await tool(peer);await target(peer);await confirm(peer).click();
  await expect(page.getByTestId('rectangle-status')).toContainText('Terrain changed');await expect(confirm(page)).toBeDisabled();
  expect((await page.request.post('/api/terrain/corner/preview?'+compat,{data:q})).status()).toBe(409);
 }finally{release();await peer.close();await replace(page,original);}
});

test('Japanese narrow keyboard rectangle excavation, bounded errors and repair controls retain pause state',async({browser})=>{
 const context=await browser.newContext({viewport:{width:390,height:844},locale:'ja-JP',deviceScaleFactor:2}),page=await context.newPage();
 await page.goto('/');await ready(page,'ja');const original=await save(page);const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 try{
  await empty(page,'ja');await tool(page,'ja');await target(page,'ja','lower');await expect(page.getByTestId('rectangle-status')).toContainText('変更できます');
  await expect(page.locator('.corner-changes li')).toHaveCount(4);expect((await state(page)).paused).toBe(true);
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);

  await confirm(page,'ja').focus();await page.keyboard.press('Enter');await expect.poll(async()=>JSON.parse((await save(page)).toString()).packedTerrain.occupantCount).toBe(4);
  await target(page,'ja','repair',[20,20,1],[20,20]);await expect(page.getByTestId('rectangle-status')).toContainText('変更はありません');
  await target(page,'ja','raise',[20,20,1],[30,30]);await expect(page.getByTestId('rectangle-status')).toContainText('最大64セル');await expect(confirm(page,'ja')).toBeDisabled();expect(errors).toEqual([]);
 }finally{await replace(page,original);await context.close();}
});
