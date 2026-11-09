import {readFileSync} from 'node:fs';
import {test,expect,type Page,type WebSocketRoute} from './fixtures.ts';
import {canonicalState as state,ready,closePanel,openPanel} from './browser-helpers.ts';
test.use({hasTouch:true});
const profile=JSON.parse(readFileSync('src/generated/station-profile.json','utf8'));
const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
const type=profile.definitions.find((d:any)=>d.height>1&&d.sizeX*d.sizeY>1);
const bytes=async(p:Page)=>(await p.request.get('/api/saves/download')).body();
async function empty(p:Page){
 const s=await state(p),r=await p.request.post('/api/worlds/empty/preview',{data:{name:'Playable station buildings',dimensions:{h:48,v:40,z:8},waterLevel:2,epoch:s.worldEpoch,revision:s.revision}}),c=await r.json();
 expect(r.ok()).toBe(true);expect((await p.request.post('/api/worlds/empty/commit',{data:{id:c.id,requestHash:c.requestHash,candidateHash:c.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).ok()).toBe(true);
 await expect(p.locator('.command-bar')).toBeVisible();
}
async function point(p:Page,world:{x:number;y:number}){return p.locator('canvas.world-canvas').evaluate((el,w)=>{
 const r=el.getBoundingClientRect(),[cx,cy]=el.dataset.camera!.split(',').map(Number),z=Number(el.dataset.zoom);
 return {x:r.x+r.width/2+(w.x-cx)*z,y:r.y+r.height/2+(w.y-cy)*z};
},world);}
async function pick(p:Page,b:any,touch=false){
 await expect(p.locator('canvas.world-canvas')).toHaveAttribute('data-presentation','ready');
 await p.evaluate(()=>new Promise<void>(resolve=>requestAnimationFrame(()=>requestAnimationFrame(()=>resolve()))));
 const recipe=catalog.renderRecipes.find((r:any)=>r.id===b.definition.recipeId),f=recipe.frames.find((f:any)=>f.cell[2]===1&&f.status==='ready');
 const mask=catalog.masks[f.masks['spring/day']];let row=-1,col=-1;
 for(let y=2;y<mask.length-2&&row<0;y++)for(let x=2;x<mask[y].length-2;x++)if(mask.slice(y-2,y+3).every((r:string)=>r.slice(x-2,x+3)==='11111')){row=y;col=x;break;}
 expect(row).toBeGreaterThanOrEqual(0);
 const x=b.location.x+f.cell[0]-19,y=b.location.y+f.cell[1];
 const q=await point(p,{x:16*(x+y)-f.anchor[0]+col+.5,y:8*(y-x)-16*(b.location.z+1)-f.anchor[1]+row+.5});
 if(touch)await p.touchscreen.tap(q.x,q.y);else await p.mouse.click(q.x,q.y);
}
async function task(p:Page,ja=false){await closePanel(p);const t=p.getByRole('button',{name:ja?'駅':'Stations',exact:true});if(await t.getAttribute('aria-pressed')!=='true')await t.click();await expect(p.locator('.station-task')).toBeVisible();await expect(p.getByLabel(ja?'駅舎の操作':'Building action',{exact:true})).toHaveCount(0);}
async function quote(p:Page,ja=false,x=25,y=25){
 if(!await p.locator('#building-catalog').count())await p.getByRole('button',{name:ja?'一覧から選択':'Choose from catalog',exact:true}).click();
 await p.locator(`[data-catalog-variant="${type.reference.recipeId}"]`).click();
 await p.getByRole('button',{name:ja?'建物一覧を閉じる':'Close building catalog',exact:true}).click();
 await p.getByLabel(ja?'駅舎 X':'Station X',{exact:true}).fill(String(x));await p.getByLabel(ja?'駅舎 Y':'Station Y',{exact:true}).fill(String(y));
 await p.locator('.station-task form').getByRole('button',{name:ja?'配置を確認':'Check placement',exact:true}).click();
 await expect(p.getByRole('button',{name:ja?'駅舎の建設を確定':'Confirm station building',exact:true})).toBeEnabled();
}
for(const ja of [false,true])test(`playable station ${ja?'JA narrow touch':'EN desktop keyboard'} build name remove and exact save reopen with operator peer`,async({page,browser,baseURL})=>{
 test.setTimeout(120000);
 if(ja)await page.setViewportSize({width:430,height:860});
 await page.addInitScript(ja=>{localStorage.setItem('freetrain-online.locale',ja?'ja':'en');},ja);
 const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 const context=await browser.newContext({baseURL,locale:ja?'en-US':'ja-JP',viewport:{width:1280,height:900}}),peer=await context.newPage();
 try{
  await page.goto('/');await ready(page,ja?'ja':'en');await empty(page);await ready(page,ja?'ja':'en');
  expect((await peer.request.post('/api/access',{data:{secret:'operator-test'}})).ok()).toBe(true);await peer.goto('/');await ready(peer,ja?'en':'ja');
  await task(page,ja);await expect(page.locator('#building-catalog [data-catalog-variant]')).toHaveCount(35);
  const original=await bytes(page);await quote(page,ja,30,30);
  expect(await bytes(page)).toEqual(original);await expect(page.locator('canvas.world-canvas')).toHaveAttribute('data-station-ghost-cells',String(type.sizeX*type.sizeY*type.height));
  await page.getByRole('button',{name:ja?'プレビューを取消':'Cancel preview',exact:true}).click();
  if(ja){
   // Closing the upper catalog lets the task sheet grow. Pan the target
   // into the clear map area before tapping, as with other catalog tools.
   const canvas=page.locator('canvas.world-canvas'),camera=await canvas.getAttribute('data-camera');
   await canvas.dispatchEvent('wheel',{deltaX:1,deltaY:150.5,deltaMode:0});
   await expect(canvas).not.toHaveAttribute('data-camera',camera!);
  }
  const target=await point(page,{x:16*(30-19+30)+16,y:8*19-32+8});
  if(ja)await page.touchscreen.tap(target.x,target.y);else await page.mouse.click(target.x,target.y);
  await expect(page.getByRole('button',{name:ja?'駅舎の建設を確定':'Confirm station building',exact:true})).toBeEnabled();
  await expect(page.locator('canvas.world-canvas')).toHaveAttribute('data-station-ghost-ready','true');
  // The authored Hakodate red roof must reach the map ghost after decoding;
  // footprint metadata alone is insufficient evidence that sprites were drawn.
  await expect.poll(()=>page.locator('canvas.world-canvas').evaluate((el,p)=>{
   const r=el.getBoundingClientRect(),scale=el.width/r.width,x=(p.x-r.x)*scale,y=(p.y-r.y)*scale;
   const left=Math.max(0,Math.floor(x-30*scale)),top=Math.max(0,Math.floor(y-45*scale)),w=Math.min(el.width-left,Math.ceil(60*scale)),h=Math.min(el.height-top,Math.ceil(65*scale));
   const data=el.getContext('2d')!.getImageData(left,top,w,h).data;let red=0;
   for(let i=0;i<data.length;i+=4)if(data[i]>data[i+1]*1.4&&data[i]>data[i+2]*1.4)red++;return red;
  },target)).toBeGreaterThan(0);
  await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/station-playable-${ja?'ja':'en'}-preview.png`});
  // Picking keeps focus on the map; Return must reach the nested building controls.
  await page.locator('canvas.world-canvas').focus();await page.keyboard.press('Enter');
  await expect.poll(async()=>(await state(page)).mixed?.stationGraph?.buildings.length).toBe(1);
  const built=await state(page),b=built.mixed.stationGraph.buildings[0];expect(b.location).toEqual({x:30,y:30,z:2});
  await expect.poll(async()=>(await state(peer)).mixed?.stationGraph?.buildings).toEqual(built.mixed.stationGraph.buildings);
  await page.locator('canvas.world-canvas').focus();await page.keyboard.press('i');await expect(page.getByRole('button',{name:ja?'確認':'Inspect',exact:true})).toHaveAttribute('aria-pressed','true');
  await pick(page,b,ja);await expect(page.getByTestId('confirmed-name')).toHaveText(b.name);
  const input=page.getByLabel(ja?'駅名':'Station name',{exact:true});await input.fill('Draft / 下書き');
  await page.getByRole('button',{name:ja?'パネルを閉じる':'Close panel',exact:true}).click();const dialog=page.locator('.decision-dialog');await expect(dialog).toBeVisible();
  await dialog.getByRole('button',{name:ja?'編集を続ける':'Keep editing',exact:true}).click();await expect(input).toHaveValue('Draft / 下書き');
  await page.getByRole('button',{name:ja?'パネルを閉じる':'Close panel',exact:true}).click();await dialog.getByRole('button',{name:ja?'下書きを破棄':'Discard draft',exact:true}).click();
  await pick(page,b,ja);await expect(input).toHaveValue(b.name);await input.fill('Central / 中央駅');
  await page.getByRole('button',{name:ja?'パネルを閉じる':'Close panel',exact:true}).click();await dialog.getByRole('button',{name:ja?'駅名を適用':'Apply name',exact:true}).click();await expect(dialog).toHaveCount(0);
  await expect.poll(async()=>(await state(peer)).mixed.stationGraph.buildings[0].name).toBe('Central / 中央駅');
  const saved=await bytes(page),s=await state(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:saved})).ok()).toBe(true);await ready(page,ja?'ja':'en');
  expect(await bytes(page)).toEqual(saved);await expect(page.locator('.primary-panel')).toHaveCount(0);
  await pick(page,b,ja);await expect(input).toHaveValue('Central / 中央駅');
  await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/station-playable-${ja?'ja':'en'}-properties.png`});
  await page.getByRole('button',{name:ja?'駅舎を撤去…':'Remove station building…',exact:true}).click();
  await expect(page.locator('.remove-task')).toBeVisible();await expect(page.locator('.station-task')).toHaveCount(0);
  const confirm=page.getByRole('button',{name:ja?'撤去を確定':'Confirm removal',exact:true});await expect(confirm).toBeEnabled();expect(await bytes(page)).toEqual(saved);
  await page.getByRole('button',{name:ja?'プレビューを取消':'Cancel preview',exact:true}).click();await expect(confirm).toBeDisabled();expect(await bytes(page)).toEqual(saved);
  if(!ja){await pick(page,b);await expect(confirm).toBeEnabled();}
  else{
   // Any occupied cell can target the building, including an upper storey.
   await page.getByLabel('ボクセル X',{exact:true}).fill(String(b.location.x));
   await page.getByLabel('ボクセル Y',{exact:true}).fill(String(b.location.y+1));
   await page.getByLabel('撤去する高さ',{exact:true}).fill(String(b.location.z+1));
   await page.locator('.remove-task form').getByRole('button',{name:'撤去をプレビュー',exact:true}).click();await expect(confirm).toBeEnabled();
  }
  await expect(page.locator('canvas.world-canvas')).toHaveAttribute('data-station-ghost-cells',String(type.sizeX*type.sizeY*type.height));
  await confirm.click();
  await expect.poll(async()=>(await state(peer)).mixed.stationGraph.buildings.length).toBe(0);expect((await state(page)).mixed.queue.pending).toHaveLength(0);expect(errors).toEqual([]);
 }finally{await context.close();}
});
test('station quotes and name drafts fence peer edits, Observer, late previews, disconnect and world replacement',async({page,browser,baseURL})=>{
 test.setTimeout(120000);
 let socket:WebSocketRoute|undefined,offline=false;
 await page.route('**/api/bootstrap?*',r=>offline?r.fulfill({status:503}):r.continue());
 await page.routeWebSocket('**/api/session?*',r=>{const server=r.connectToServer();socket=r;server.onMessage(m=>r.send(m));r.onMessage(m=>server.send(m));});
 const context=await browser.newContext({baseURL}),peer=await context.newPage(),observerContext=await browser.newContext({baseURL}),observer=await observerContext.newPage();
 try{
  await page.goto('/');await ready(page);await empty(page);
  expect((await peer.request.post('/api/access',{data:{secret:'operator-test'}})).ok()).toBe(true);await peer.goto('/');await ready(peer);
  expect((await observer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);await observer.goto('/');await ready(observer);
  await expect(observer.getByRole('button',{name:'Stations',exact:true})).toBeDisabled();
  await task(peer);await quote(peer);const confirm=peer.getByRole('button',{name:'Confirm station building',exact:true});
  await page.getByRole('button',{name:'Single step',exact:false}).click();await expect(confirm).toBeDisabled();
  await quote(peer);await confirm.click();await expect.poll(async()=>(await state(page)).mixed?.stationGraph?.buildings.length).toBe(1);
  const b=(await state(page)).mixed.stationGraph.buildings[0];
  for(const p of [page,peer,observer]){await closePanel(p);await p.getByRole('button',{name:'Inspect',exact:true}).click();await pick(p,b);await expect(p.getByTestId('confirmed-name')).toHaveText(b.name);}
  await expect(observer.getByRole('button',{name:'Apply name',exact:true})).toBeDisabled();await expect(observer.getByRole('button',{name:'Remove station building…',exact:true})).toBeDisabled();
  const input=page.getByLabel('Station name',{exact:true});await input.fill('Preserved local draft');
  await peer.getByLabel('Station name',{exact:true}).fill('Peer station');await peer.getByRole('button',{name:'Apply name',exact:true}).click();
  await expect(page.getByTestId('confirmed-name')).toHaveText('Peer station');await expect(input).toHaveValue('Preserved local draft');await expect(page.getByRole('button',{name:'Apply name',exact:true})).toBeDisabled();
  await page.getByRole('button',{name:'Reset draft',exact:true}).click();await expect(input).toHaveValue('Peer station');
  // A replaced world discards even a pending dirty decision, preserving the save.
  const saved=await bytes(page),s=await state(page);await input.fill('Replaced draft');await closePanel(page);await expect(page.locator('.decision-dialog')).toBeVisible();
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:saved})).ok()).toBe(true);
  await expect(page.locator('.decision-dialog')).toHaveCount(0);await ready(page);await expect(page.locator('.primary-panel')).toHaveCount(0);expect(await bytes(page)).toEqual(saved);
  await task(page);await quote(page,false,30,25);
  let release!:()=>void;const held=new Promise<void>(r=>{release=r;});let seen=false;
  await page.route('**/api/station/building/preview?*',async r=>{const response=await r.fetch();seen=true;await held;await r.fulfill({response});});
  await page.getByLabel('Station X',{exact:true}).fill('31');await page.locator('.station-task form').getByRole('button',{name:'Check placement',exact:true}).click();await expect.poll(()=>seen).toBe(true);
  await page.getByRole('button',{name:'Cancel preview',exact:true}).click();release();await expect(page.getByRole('button',{name:'Confirm station building',exact:true})).toBeDisabled();expect(await bytes(page)).toEqual(saved);
  await page.unroute('**/api/station/building/preview?*');
  await page.locator('canvas').focus();await page.keyboard.press('i');await pick(page,b);await input.fill('Offline draft');
  offline=true;socket!.close();await expect(page.getByRole('heading',{name:'Connection lost',exact:true})).toBeVisible();
  offline=false;await ready(page);await pick(page,b);await expect(input).toHaveValue('Peer station');expect(await bytes(page)).toEqual(saved);
 }finally{await context.close();await observerContext.close();}
});

test('Remove tool fences station quotes by minute and cancels late station removal previews',async({page})=>{
 await page.goto('/');await ready(page);await empty(page);await task(page);await quote(page);
 await page.getByRole('button',{name:'Confirm station building',exact:true}).click();
 await expect.poll(async()=>(await state(page)).mixed?.stationGraph?.buildings.length).toBe(1);
 const b=(await state(page)).mixed.stationGraph.buildings[0];
 await page.getByRole('button',{name:'Remove',exact:true}).click();
 await page.getByLabel('Voxel X',{exact:true}).fill(String(b.location.x));
 await page.getByLabel('Voxel Y',{exact:true}).fill(String(b.location.y));
 await page.getByLabel('Removal level',{exact:true}).fill(String(b.location.z));
 const check=page.locator('.remove-task form').getByRole('button',{name:'Preview removal',exact:true});await check.click();
 const confirm=page.getByRole('button',{name:'Confirm removal',exact:true});await expect(confirm).toBeEnabled();
 await page.getByRole('button',{name:'Single step',exact:false}).click();await expect(confirm).toBeDisabled();
 const saved=await bytes(page);
 let release!:()=>void;const held=new Promise<void>(r=>{release=r;});let seen=false;
 await page.route('**/api/station/building/preview?*',async r=>{const response=await r.fetch();seen=true;await held;await r.fulfill({response});});
 await page.locator('.remove-task form').getByRole('button',{name:'Preview removal',exact:true}).click();await expect.poll(()=>seen).toBe(true);
 await page.getByRole('button',{name:'Cancel preview',exact:true}).click();release();
 await expect(confirm).toBeDisabled();expect(await bytes(page)).toEqual(saved);
 await page.unroute('**/api/station/building/preview?*');
 await check.click();await expect(confirm).toBeEnabled();
 await page.locator('canvas').focus();await page.keyboard.press('Enter');
 await expect.poll(async()=>(await state(page)).mixed.stationGraph.buildings.length).toBe(0);
});
