import {execFileSync} from 'node:child_process';
import {readFileSync,writeFileSync} from 'node:fs';
import {test,expect,type Page,type WebSocketRoute} from './fixtures.ts';
import {canonicalState as state,ready,exposeMapPoint} from './browser-helpers.ts';
test.use({hasTouch:true});
const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
async function save(p:Page){return(await p.request.get('/api/saves/download')).body();}
async function empty(p:Page,ja=false){const s=await state(p),r=await p.request.post('/api/worlds/empty/preview',{data:{name:'Passages',dimensions:{h:48,v:40,z:8},waterLevel:2,epoch:s.worldEpoch,revision:s.revision}}),c=await r.json();expect((await p.request.post('/api/worlds/empty/commit',{data:{id:c.id,requestHash:c.requestHash,candidateHash:c.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).ok()).toBe(true);await ready(p,ja?'ja':'en');}
async function task(p:Page,ja=false){const button=p.getByRole('button',{name:ja?'駅':'Stations',exact:true});if(await button.getAttribute('aria-pressed')!=='true')await button.click();await p.getByRole('tab',{name:ja?'連絡通路':'Passages',exact:true}).click();}
async function quote(p:Page,ja=false,material='concrete',action='place',x=24,endX=27,y=25){
 await p.getByLabel(ja?'通路の操作':'Passage action',{exact:true}).selectOption(action);await p.getByLabel(ja?'通路の素材':'Passage material',{exact:true}).selectOption(material);
 for(const [label,value] of [[ja?'通路の始点 X':'Passage start X',x],[ja?'通路の始点 Y':'Passage start Y',y],[ja?'通路の終点 X':'Passage end X',endX],[ja?'通路の終点 Y':'Passage end Y',y],[ja?'通路の高さ':'Passage level',2]] as const)await p.getByLabel(label,{exact:true}).fill(String(value));
 const response=p.waitForResponse(r=>r.url().includes('/api/station/passage/preview?'));await p.locator('.passage-task form').getByRole('button',{name:ja?'配置を確認':'Check placement',exact:true}).click();return(await response).json();
}
for(const ja of [false,true])test(`passage ${ja?'JA narrow touch':'EN'} all materials preview cancel remove peer exact reopen and checkpoint`,async({page,browser,baseURL,worldServer})=>{
 test.setTimeout(150000);const context=await browser.newContext({baseURL}),peer=await context.newPage();
 if(ja){await page.setViewportSize({width:430,height:860});await page.addInitScript(()=>localStorage.setItem('freetrain-online.locale','ja'));}
 const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));let socket:WebSocketRoute;const messages:any[]=[];let serial=0;
 await page.routeWebSocket('**/api/session?*',r=>{socket=r.connectToServer();socket.onMessage(m=>{messages.push(JSON.parse(String(m)));r.send(m);});r.onMessage(m=>socket.send(m));});
 async function platform(x:number,heading:number){const s=await state(page),q={expectedWorldEpoch:s.worldEpoch,expectedGeometryRevision:s.mixed?.geometryRevision??s.terrain.geometryRevision,expectedMinute:s.tick,expectedContentVersion:s.contentVersion,action:'place',position:{x,y:25,z:2},direction:heading,length:4,roof:true,platformId:'0',expectedPlatformRevision:'0'};const r=await page.request.post('/api/station/platform/preview?protocolVersion=8&contentVersion=terrain-content-1&contentVersion=mixed-railway-content-1',{data:q}),v=await r.json();expect(v.legal,v.reason).toBe(true);const requestId='passage-platform-'+serial++;socket.send(JSON.stringify({type:'command',requestId,expectedWorldEpoch:s.worldEpoch,payload:{kind:'editThinPlatform',query:q,expectedCost:v.cost}}));await expect.poll(()=>messages.find(m=>m.requestId===requestId)?.type).toBe('accepted');}
 try{
  await page.goto('/');await ready(page,ja?'ja':'en');await empty(page,ja);await ready(page,ja?'ja':'en');
  await platform(25,4);await platform(26,0);
  expect((await peer.request.post('/api/access',{data:{secret:'operator-test'}})).ok()).toBe(true);await peer.goto('/');await ready(peer);
  await task(page,ja);const initial=await state(page),initialBytes=await save(page),confirm=page.getByRole('button',{name:ja?'通路の設置を確定':'Confirm passage',exact:true});
  await quote(page,ja);await page.getByRole('button',{name:ja?'通路のプレビューを表示':'Show passage preview',exact:true}).click();
  await page.getByRole('button',{name:ja?'地図で通路の両端を選択':'Pick passage endpoints on map',exact:true}).click();
  for(const x of [24,27]){
   const point=await page.locator('canvas.world-canvas').evaluate((el,x)=>{const r=el.getBoundingClientRect(),[cx,cy]=el.dataset.camera!.split(',').map(Number),zoom=Number(el.dataset.zoom);return {x:r.x+r.width/2+(16*(x-19+25)+16-cx)*zoom,y:r.y+r.height/2+(8*(25-x+19)-32+8-cy)*zoom};},x);
   const exposed=await exposeMapPoint(page,point);if(ja)await page.touchscreen.tap(exposed.x,exposed.y);else await page.mouse.click(exposed.x,exposed.y);
  }
  await expect(confirm).toBeEnabled();await expect(page.getByLabel(ja?'通路の始点 X':'Passage start X',{exact:true})).toHaveValue('24');await expect(page.getByLabel(ja?'通路の終点 X':'Passage end X',{exact:true})).toHaveValue('27');expect(await save(page)).toEqual(initialBytes);
  await page.getByRole('button',{name:ja?'プレビューを取消':'Cancel preview',exact:true}).click();
  for(const material of ['concrete','brown','green']){
   const before=await save(page),q=await quote(page,ja,material);expect(q.legal,q.reason).toBe(true);expect(q.cost).toBe('0');expect(q.positions).toHaveLength(4);expect(await save(page)).toEqual(before);
   await page.getByRole('button',{name:ja?'通路のプレビューを表示':'Show passage preview',exact:true}).click();await expect(page.locator('canvas.world-canvas')).toHaveAttribute('data-station-ghost-ready','true');
   await page.getByRole('button',{name:ja?'プレビューを取消':'Cancel preview',exact:true}).click();await expect(confirm).toBeDisabled();expect(await save(page)).toEqual(before);
   await quote(page,ja,material);await confirm.click();await expect.poll(async()=>(await state(peer)).mixed.stationGraph.platforms[0].yards[0].outlook.material).toBe(material);
   const s=await state(page);expect(s.mixed.voxels).toEqual(initial.mixed.voxels);expect(s.mixed.accounts).toEqual(initial.mixed.accounts);expect(s.mixed.queue).toEqual(initial.mixed.queue);
   expect(s.mixed.stationGraph.platforms.map((p:any)=>[p.hostId,p.children,p.name,p.bell,p.dayTimer])).toEqual(initial.mixed.stationGraph.platforms.map((p:any)=>[p.hostId,p.children,p.name,p.bell,p.dayTimer]));
   await expect(page.locator('canvas.world-canvas')).toHaveAttribute('data-presentation','ready');
   await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/passage-${ja?'ja':'en'}-${material}.png`});
   const bytes=await save(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:bytes})).ok()).toBe(true);await ready(page,ja?'ja':'en');expect(await save(page)).toEqual(bytes);await task(page,ja);
   const remove=await quote(page,ja,material,'remove');expect(remove.legal).toBe(true);await page.getByRole('button',{name:ja?'撤去を確定':'Confirm removal',exact:true}).click();await expect.poll(async()=>(await state(peer)).mixed.stationGraph.platforms[0].yards[0].outlook.kind).toBe('plain');
  }
  // Empty paths are legal exact no-ops; ordinary/parallel platform rails reject.
  const before=await save(page);await quote(page,ja,'concrete','place',30,31,30);await confirm.click();expect(await save(page)).toEqual(before);
  await quote(page,ja,'green');await confirm.click();await expect.poll(async()=>(await state(peer)).mixed.stationGraph.platforms[0].yards[0].outlook.kind).toBe('passage');
  const bytes=await save(page);await worldServer.restart!();expect((await page.request.post('/api/access',{data:{secret:''}})).ok()).toBe(true);await page.reload();await ready(page,ja?'ja':'en');expect(await save(page)).toEqual(bytes);expect(bytes).not.toEqual(initialBytes);expect(errors).toEqual([]);
 }finally{await context.close();}
});
test('passage stale peer edits late cancellation observer and replacement cannot publish old confirmations',async({page,browser,baseURL})=>{
 test.setTimeout(90000);const context=await browser.newContext({baseURL}),peer=await context.newPage(),observerContext=await browser.newContext({baseURL}),observer=await observerContext.newPage();let socket:WebSocketRoute;const messages:any[]=[];
 try{
  await page.goto('/');await ready(page);await empty(page);
  await peer.goto('/');await ready(peer);await task(peer);const q=await quote(peer,false,'brown','place',30,31,30),bytes=await save(page);
  await page.getByRole('button',{name:'Single step',exact:false}).click();await expect(peer.getByRole('button',{name:'Confirm passage',exact:true})).toBeDisabled();
  let release!:()=>void,seen=false;const hold=new Promise<void>(r=>release=r);
  await peer.route('**/api/station/passage/preview?*',async r=>{const response=await r.fetch();seen=true;await hold;await r.fulfill({response});});
  await peer.locator('.passage-task form').getByRole('button',{name:'Check placement',exact:true}).click();await expect.poll(()=>seen).toBe(true);await peer.getByRole('button',{name:'Cancel preview',exact:true}).click();release();await expect(peer.getByRole('button',{name:'Confirm passage',exact:true})).toBeDisabled();await peer.unroute('**/api/station/passage/preview?*');
  expect((await observer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);
  await observer.routeWebSocket('**/api/session?*',r=>{socket=r.connectToServer();socket.onMessage(m=>{messages.push(JSON.parse(String(m)));r.send(m);});r.onMessage(m=>socket.send(m));});await observer.goto('/');await ready(observer);await expect(observer.getByRole('button',{name:'Stations',exact:true})).toBeDisabled();
  socket!.send(JSON.stringify({type:'command',requestId:'observer-passage',expectedWorldEpoch:q.query.expectedWorldEpoch,payload:{kind:'editPassage',query:q.query,expectedCost:'0'}}));await expect.poll(()=>messages.find(m=>m.requestId==='observer-passage')?.code).toBe('notAuthorized');
  await quote(peer,false,'brown','place',30,31,30);const s=await state(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:bytes})).ok()).toBe(true);await ready(peer);await expect(peer.locator('.passage-task')).toHaveCount(0);
 }finally{await context.close();await observerContext.close();}
});
test('all 48 passage appearances match independent source crop pixels and alpha picking in eight variants',async({page})=>{
 test.setTimeout(180000);const code=execFileSync('deno',['run','-A','scripts/bundle-rendering-tests.ts','tests/helpers/passage-rendering-harness.ts'],{encoding:'utf8',maxBuffer:16*1024*1024});await page.route('**/__tests/passage.js',r=>r.fulfill({contentType:'text/javascript',body:code}));await page.goto('/');await ready(page);
 const reports=[];
 for(const season of ['spring','summer','autumn','winter'])for(const night of [false,true]){
  const r=await page.evaluate(async({catalog,season,night})=>{const {passagePixels}=await import('/__tests/passage.js');return passagePixels(catalog,{season,night});},{catalog,season,night});expect(r.count).toBe(48);expect(r.pixels).toBe(576000);expect(r.picks).toBe(r.pixels);reports.push({season,night,count:r.count,pixels:r.pixels,picks:r.picks});writeFileSync(`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/passages-${season}-${night?'night':'day'}.png`,Buffer.from(r.png.split(',')[1],'base64'));
 }
 writeFileSync(`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/passages-report.json`,JSON.stringify(reports,null,2));
});
for(const ja of [false,true])test(`stairs ${ja?'JA narrow touch':'EN'} materials neighbors remove peer exact reopen and checkpoint`,async({page,browser,baseURL,worldServer})=>{
 test.setTimeout(150000);const context=await browser.newContext({baseURL}),peer=await context.newPage();
 if(ja){await page.setViewportSize({width:430,height:860});await page.addInitScript(()=>localStorage.setItem('freetrain-online.locale','ja'));}
 let socket:WebSocketRoute;const messages:any[]=[];
 await page.routeWebSocket('**/api/session?*',r=>{socket=r.connectToServer();socket.onMessage(m=>{messages.push(JSON.parse(String(m)));r.send(m);});r.onMessage(m=>socket.send(m));});
 async function stair(material='concrete',action='place',y=26){
  await page.getByLabel(ja?'部品':'Part',{exact:true}).selectOption('stairs');
  await page.getByLabel(ja?'階段の操作':'Stair action',{exact:true}).selectOption(action);
  await page.getByLabel(ja?'階段の素材':'Stair material',{exact:true}).selectOption(material);
  await page.getByLabel(ja?'階段 X':'Stair X',{exact:true}).fill('25');
  await page.getByLabel(ja?'階段 Y':'Stair Y',{exact:true}).fill(String(y));
  await page.getByLabel(ja?'階段の高さ':'Stair level',{exact:true}).fill('2');
  const response=page.waitForResponse(r=>r.url().includes('/api/station/passage/preview?'));await page.locator('.passage-task form').getByRole('button',{name:ja?'配置を確認':'Check placement',exact:true}).click();return(await response).json();
 }
 try{
  await page.goto('/');await ready(page,ja?'ja':'en');await empty(page,ja);
  const s=await state(page),q={expectedWorldEpoch:s.worldEpoch,expectedGeometryRevision:s.terrain.geometryRevision,expectedMinute:s.tick,expectedContentVersion:s.contentVersion,action:'place',position:{x:25,y:25,z:2},direction:4,length:4,roof:true,platformId:'0',expectedPlatformRevision:'0'};
  const r=await page.request.post('/api/station/platform/preview?protocolVersion=8&contentVersion=terrain-content-1&contentVersion=mixed-railway-content-1',{data:q}),v=await r.json();expect(v.legal,v.reason).toBe(true);
  socket!.send(JSON.stringify({type:'command',requestId:'stair-platform',expectedWorldEpoch:s.worldEpoch,payload:{kind:'editThinPlatform',query:q,expectedCost:v.cost}}));await expect.poll(()=>messages.find(m=>m.requestId==='stair-platform')?.type).toBe('accepted');
  await peer.goto('/');await ready(peer);await task(page,ja);
  expect((await stair()).reason).toBe('stairNeighbor');
  await page.getByLabel(ja?'部品':'Part',{exact:true}).selectOption('passage');await quote(page,ja);await page.getByRole('button',{name:ja?'通路の設置を確定':'Confirm passage',exact:true}).click();
  const confirm=page.getByRole('button',{name:ja?'階段の設置を確定':'Confirm stairs',exact:true});
  for(const material of ['concrete','brown','green']){
   const before=await save(page),p=await stair(material);expect(p.legal,p.reason).toBe(true);expect(p.positions).toEqual([{x:25,y:26,z:2}]);expect(p.cost).toBe('0');expect(await save(page)).toEqual(before);
   await page.getByRole('button',{name:ja?'通路のプレビューを表示':'Show passage preview',exact:true}).click();await expect(page.locator('canvas.world-canvas')).toHaveAttribute('data-station-ghost-ready','true');
   if(material==='concrete'){
    await page.getByRole('button',{name:ja?'地図で階段の位置を選択':'Pick stair location on map',exact:true}).click();
    const point=await page.locator('canvas.world-canvas').evaluate(el=>{const r=el.getBoundingClientRect(),[cx,cy]=el.dataset.camera!.split(',').map(Number),zoom=Number(el.dataset.zoom);return {x:r.x+r.width/2+(16*(25-19+26)+16-cx)*zoom,y:r.y+r.height/2+(8*(26-25+19)-32+8-cy)*zoom};});
    const exposed=await exposeMapPoint(page,point);if(ja)await page.touchscreen.tap(exposed.x,exposed.y);else await page.mouse.click(exposed.x,exposed.y);
    await expect(confirm).toBeEnabled();await expect(page.getByLabel(ja?'階段 X':'Stair X',{exact:true})).toHaveValue('25');await expect(page.getByLabel(ja?'階段 Y':'Stair Y',{exact:true})).toHaveValue('26');
   }
   await page.getByRole('button',{name:ja?'プレビューを取消':'Cancel preview',exact:true}).click();await expect(confirm).toBeDisabled();
   await stair(material);await confirm.click();await expect.poll(async()=>(await state(peer)).mixed.stationGraph.platforms[0].yards[1].outlook).toEqual({kind:'stair',material,bridge:false,upward:false});
   await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/stairs-${ja?'ja':'en'}-${material}.png`});
   const bytes=await save(page),now=await state(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+now.worldEpoch,{data:bytes})).ok()).toBe(true);await ready(page,ja?'ja':'en');expect(await save(page)).toEqual(bytes);await task(page,ja);
   await stair(material,'remove');await page.getByRole('button',{name:ja?'撤去を確定':'Confirm removal',exact:true}).click();await expect.poll(async()=>(await state(peer)).mixed.stationGraph.platforms[0].yards[1].outlook.kind).toBe('plain');
  }
  await stair('green');await confirm.click();await expect.poll(async()=>(await state(page)).mixed.stationGraph.platforms[0].yards[1].outlook.kind).toBe('stair');
  const bytes=await save(page);await worldServer.restart!();expect((await page.request.post('/api/access',{data:{secret:''}})).ok()).toBe(true);await page.reload();await ready(page,ja?'ja':'en');expect(await save(page)).toEqual(bytes);
 }finally{await context.close();}
});
test('all 96 stair appearances match literal crops and alpha picks in eight variants including corrected index23',async({page})=>{
 test.setTimeout(180000);const code=execFileSync('deno',['run','-A','scripts/bundle-rendering-tests.ts','tests/helpers/stair-rendering-harness.ts'],{encoding:'utf8',maxBuffer:16*1024*1024});await page.route('**/__tests/stairs.js',r=>r.fulfill({contentType:'text/javascript',body:code}));await page.goto('/');await ready(page);
 for(const season of ['spring','summer','autumn','winter'])for(const night of [false,true]){
  const r=await page.evaluate(async({catalog,season,night})=>{const {stairPixels}=await import('/__tests/stairs.js');return stairPixels(catalog,{season,night});},{catalog,season,night});expect(r.count).toBe(96);expect(r.pixels).toBe(1152000);expect(r.picks).toBe(r.pixels);writeFileSync(`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/stairs-${season}-${night?'night':'day'}.png`,Buffer.from(r.png.split(',')[1],'base64'));
 }
});
test('stairs render and pick before or after overlapping train cars with height cuts',async({page})=>{
 const code=execFileSync('deno',['run','-A','scripts/bundle-rendering-tests.ts','tests/helpers/stair-rendering-harness.ts'],{encoding:'utf8',maxBuffer:16*1024*1024});await page.route('**/__tests/stair-overlap.js',r=>r.fulfill({contentType:'text/javascript',body:code}));await page.goto('/');await ready(page);
 const report=await page.evaluate(async catalog=>{const {stairTrainOverlap}=await import('/__tests/stair-overlap.js');return stairTrainOverlap(catalog);},catalog);expect(report).toEqual({count:12,picks:76800});
});
