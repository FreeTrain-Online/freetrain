import {readFileSync} from 'node:fs';
import {test,expect,type Page,type WebSocketRoute} from './fixtures.ts';
import {canonicalState as state,ready,closePanel,exposeMapPoint} from './browser-helpers.ts';
const profile=JSON.parse(readFileSync('src/generated/station-profile.json','utf8'));
const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
const definition=profile.definitions.find((d:any)=>d.sizeX===1&&d.sizeY===1&&d.height===1).reference;
const compat='protocolVersion=8&contentVersion=terrain-content-1&contentVersion=mixed-railway-content-1';
const save=async(p:Page)=>(await p.request.get('/api/saves/download')).body();
test.use({hasTouch:true});
async function pick(page:Page,p:any,ja=false,readOnly=false){
 await closePanel(page);await page.getByRole('button',{name:ja?'確認':'Inspect',exact:true}).click();
 await expect(page.locator('canvas.world-canvas')).toHaveAttribute('data-presentation','ready');
 const frame=catalog.renderRecipes.find((r:any)=>r.id===(p.kind==='wide'?`system:fat-${p.direction/2%2}`:`system:thin-${p.direction}`)).frames[0],mask=catalog.masks[frame.masks['spring/day']];
 let sample:{x:number;y:number}|undefined;
 for(let y=1;y<mask.length-1&&!sample;y++)for(let x=1;x<mask[y].length-1&&!sample;x++)if(mask.slice(y-1,y+2).every((r:string)=>r.slice(x-1,x+2)==='111'))sample={x:x+.5,y:y+.5};
 expect(sample).toBeTruthy();
 const point=await page.locator('canvas.world-canvas').evaluate((el,a)=>{const r=el.getBoundingClientRect(),[cx,cy]=el.dataset.camera!.split(',').map(Number),zoom=Number(el.dataset.zoom),x=a.location.x-19,y=a.location.y;return {x:r.x+r.width/2+(16*(x+y)-a.anchor[0]+a.sample.x-cx)*zoom,y:r.y+r.height/2+(8*(y-x)-16*a.location.z-a.anchor[1]+a.sample.y-cy)*zoom};},{location:p.location,anchor:frame.anchor,sample:sample!});
 const exposed=await exposeMapPoint(page,point);if(ja)await page.touchscreen.tap(exposed.x,exposed.y);else await page.mouse.click(exposed.x,exposed.y);
 await expect(page.getByTestId('platform-identity')).toContainText(p.id);
 await expect(page.getByLabel(ja?'論理的な接続先':'Logical connection',{exact:true})).toBeVisible();
 if(!readOnly)await expect(page.getByLabel(ja?'論理的な接続先':'Logical connection',{exact:true})).toBeEnabled();
}
for(const ja of [false,true])test(`P6-12 ${ja?'JA narrow touch':'EN desktop'} platform properties connect reconnect clear conflicts parent removal and exact peer reopen`,async({page,browser,baseURL,worldServer})=>{
 test.setTimeout(180000);if(ja)await page.setViewportSize({width:430,height:860});await page.addInitScript(ja=>localStorage.setItem('freetrain-online.locale',ja?'ja':'en'),ja);
 const errors:string[]=[],messages:any[]=[];let socket:WebSocketRoute|undefined,serial=0;
 page.on('pageerror',e=>errors.push(String(e)));
 await page.routeWebSocket('**/api/session?*',route=>{socket=route.connectToServer();socket.onMessage(m=>{messages.push(JSON.parse(String(m)));route.send(m);});route.onMessage(m=>socket!.send(m));});
 const context=await browser.newContext({baseURL}),peer=await context.newPage();peer.on('pageerror',e=>errors.push(String(e)));
 async function command(payload:any){const requestId='properties-'+(++serial),s=await state(page);socket!.send(JSON.stringify({type:'command',requestId,expectedWorldEpoch:s.worldEpoch,payload}));await expect.poll(()=>messages.find(m=>m.requestId===requestId)?.type).toBe('accepted');}
 async function station(x:number,y:number,remove?:any){const s=await state(page),query={expectedWorldEpoch:s.worldEpoch,expectedGeometryRevision:s.mixed?.geometryRevision??s.terrain.geometryRevision,expectedMinute:s.tick,expectedContentVersion:s.contentVersion,action:remove?'remove':'place',position:{x,y,z:2},definition:remove?null:definition,stationId:remove?.id??'0',expectedStationRevision:remove?.revision??'0'};
  const r=await page.request.post('/api/station/building/preview?'+compat,{data:query});expect(r.ok()).toBe(true);const q=await r.json();expect(q.legal,q.reason).toBe(true);await command({kind:'editStationBuilding',query,expectedCost:q.cost});}
 async function place(x:number,y:number,wide=false){const s=await state(page),query:any={expectedWorldEpoch:s.worldEpoch,expectedGeometryRevision:s.mixed.geometryRevision,expectedMinute:s.tick,expectedContentVersion:s.contentVersion,action:'place',position:{x,y,z:2},direction:2,length:4,platformId:'0',expectedPlatformRevision:'0',...(wide?{secondLane:false}:{roof:false})};
  const r=await page.request.post('/api/station/'+(wide?'wide-platform':'platform')+'/preview?'+compat,{data:query}),q=await r.json();expect(q.legal,q.reason).toBe(true);await command({kind:wide?'editWidePlatform':'editThinPlatform',query,expectedCost:q.cost});}
 const label=(en:string,jp:string)=>ja?jp:en;
 const name=()=>page.getByLabel(label('Platform name','ホーム名'),{exact:true}),host=()=>page.getByLabel(label('Logical connection','論理的な接続先'),{exact:true}),apply=()=>page.getByRole('button',{name:label('Apply properties','プロパティを適用'),exact:true}),reset=()=>page.getByRole('button',{name:label('Reset draft','下書きを戻す'),exact:true});
 async function applyDraft(){if(ja){await apply().scrollIntoViewIfNeeded();await apply().tap();}else await apply().click();await expect(apply()).toBeDisabled();await expect(host()).toBeEnabled();await expect(name()).toHaveValue((await page.getByTestId('confirmed-name').textContent())!);}
 try{
  await page.goto('/');await ready(page,ja?'ja':'en');const s=await state(page),r=await page.request.post('/api/worlds/empty/preview',{data:{name:'Platform properties',dimensions:{h:48,v:40,z:8},waterLevel:2,epoch:s.worldEpoch,revision:s.revision}}),candidate=await r.json();expect(r.ok()).toBe(true);
  expect((await page.request.post('/api/worlds/empty/commit',{data:{id:candidate.id,requestHash:candidate.requestHash,candidateHash:candidate.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).ok()).toBe(true);await ready(page,ja?'ja':'en');
  expect((await peer.request.post('/api/access',{data:{secret:'operator-test'}})).ok()).toBe(true);await peer.goto('/');await ready(peer);
  await station(19,25);await station(24,24);await place(25,25);await place(25,28,true);await place(25,31);
  let g=(await state(page)).mixed.stationGraph;const [a,b]=g.buildings.map((b:any)=>b.id),[slim,wide,child]=g.platforms;
  const accounts=(await state(page)).mixed.accounts,queue=(await state(page)).mixed.queue;
  await pick(page,slim,ja);await expect(page.getByText(label('Wide-platform track connections','広いホームの線路接続'),{exact:true})).toHaveCount(0);
  await name().fill('Independent / 独立ホーム');await host().selectOption(b);await page.getByLabel(label('Departure bells','発車ベル'),{exact:true}).selectOption(profile.bells[2].contributionId);
  const before=await save(page);await page.getByRole('button',{name:label('Preview bell','ベルを試聴'),exact:true}).click();expect(await save(page)).toEqual(before);
  await applyDraft();await expect(page.getByTestId('confirmed-name')).toHaveText('Independent / 独立ホーム');await expect.poll(async()=>(await state(peer)).mixed.stationGraph.platforms[0].hostId).toBe(b);
  expect((await state(page)).mixed.stationGraph.platforms[0].bell).toEqual(profile.bells[2]);
  // Unapplied properties use the shared Keep/Discard/Apply navigation workflow.
  await name().fill('Discard me');await page.getByRole('button',{name:label('Close panel','パネルを閉じる'),exact:true}).click();const dialog=page.locator('.decision-dialog');await expect(dialog).toBeVisible();
  await dialog.getByRole('button',{name:label('Keep editing','編集を続ける'),exact:true}).click();await expect(name()).toHaveValue('Discard me');
  await page.getByRole('button',{name:label('Close panel','パネルを閉じる'),exact:true}).click();await dialog.getByRole('button',{name:label('Discard draft','下書きを破棄'),exact:true}).click();await pick(page,slim,ja);await expect(name()).toHaveValue('Independent / 独立ホーム');
  await name().fill('Applied on close / 適用');await closePanel(page);await expect(dialog).toBeVisible();await dialog.getByRole('button',{name:label('Apply properties','プロパティを適用'),exact:true}).click();await expect(dialog).toHaveCount(0);await expect(page.locator('.primary-panel')).toHaveCount(0);await pick(page,slim,ja);await expect(name()).toHaveValue('Applied on close / 適用');
  await host().selectOption('');await applyDraft();await expect(page.getByTestId('platform-disconnected')).toBeVisible();
  await host().selectOption(wide.id);await applyDraft();await expect(page.getByTestId('platform-resolved-station')).toContainText(a);
  // Child links remain source ordered. Reparenting the wide platform clears both.
  await pick(page,child,ja);await host().selectOption(wide.id);await applyDraft();await name().fill('Child / 子');await applyDraft();
  await pick(page,wide,ja);await expect(page.getByText(label('Wide-platform track connections','広いホームの線路接続'),{exact:true})).toBeVisible();
  await name().fill('Parent / 親');await host().selectOption(b);await page.getByLabel(label('Departure bells','発車ベル'),{exact:true}).selectOption(profile.bells[4].contributionId);await applyDraft();
  g=(await state(page)).mixed.stationGraph;expect(g.platforms.filter((p:any)=>p.id!==wide.id).every((p:any)=>p.hostId===null)).toBe(true);expect(g.platforms.find((p:any)=>p.id===wide.id).hostId).toBe(b);
  // Dirty drafts retain their original revision, including a selected candidate's root.
  await name().fill('Local draft');await pick(peer,wide);await peer.getByLabel('Platform name',{exact:true}).fill('Peer / 相手');await peer.getByRole('button',{name:'Apply properties',exact:true}).click();
  await expect(page.getByTestId('confirmed-name')).toHaveText('Peer / 相手');await expect(name()).toHaveValue('Local draft');await expect(apply()).toBeDisabled();await reset().click();await expect(name()).toHaveValue('Peer / 相手');
  await name().fill('Keep after removal');g=(await state(page)).mixed.stationGraph;await station(24,24,g.buildings.find((s:any)=>s.id===b));await expect(apply()).toBeDisabled();await expect(name()).toHaveValue('Keep after removal');await reset().click();await expect(host()).toHaveValue('');
  await host().selectOption(a);await name().fill('Final parent / 最終');await applyDraft();
  const final=await save(page);g=(await state(page)).mixed.stationGraph;
  expect((await state(page)).mixed.accounts.transactions).toHaveLength(accounts.transactions.length+1); // removed station, properties are free
  expect((await state(page)).mixed.queue.pending).toHaveLength(queue.pending.length-2);
  await expect.poll(async()=>(await state(peer)).mixed.stationGraph).toEqual(g);
  await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/platform-properties-${ja?'ja':'en'}.png`});
  await closePanel(peer);const current=await state(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+current.worldEpoch,{data:final})).ok()).toBe(true);await ready(page,ja?'ja':'en');await ready(peer);expect(await save(page)).toEqual(final);
  await worldServer.restart!();for(const p of [page,peer])expect((await p.request.post('/api/access',{data:{secret:p===page?'':'operator-test'}})).ok()).toBe(true);await page.reload();await peer.reload();await ready(page,ja?'ja':'en');await ready(peer);expect(await save(page)).toEqual(final);
  await pick(page,g.platforms.find((p:any)=>p.id===wide.id),ja);await expect(name()).toHaveValue('Final parent / 最終');await expect(host()).toHaveValue(a);await expect(page.getByLabel(label('Departure bells','発車ベル'),{exact:true})).toHaveValue(profile.bells[4].contributionId);
  // World replacement cancels dirty decisions and never replays properties.
  await name().fill('Replaced draft');await closePanel(page);await expect(dialog).toBeVisible();const replaced=await state(page);
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+replaced.worldEpoch,{data:final})).ok()).toBe(true);await ready(page,ja?'ja':'en');await expect(dialog).toHaveCount(0);await expect(page.locator('.primary-panel')).toHaveCount(0);expect(await save(page)).toEqual(final);
  // A late candidate response cannot reopen a closed editor.
  let release!:()=>void,seen=false;const held=new Promise<void>(r=>{release=r;});
  await page.route('**/api/station/platform/properties?*',async route=>{const response=await route.fetch();seen=true;await held;await route.fulfill({response});});
  await pick(page,g.platforms.find((p:any)=>p.id===wide.id),ja,true);await expect.poll(()=>seen).toBe(true);await closePanel(page);release();await expect(page.locator('.platform-properties')).toHaveCount(0);await page.unroute('**/api/station/platform/properties?*');
  await closePanel(peer);expect((await peer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);await peer.reload();await ready(peer);await pick(peer,g.platforms.find((p:any)=>p.id===wide.id),false,true);
  await expect(peer.getByLabel('Platform name',{exact:true})).toBeDisabled();await expect(peer.getByRole('button',{name:'Apply properties',exact:true})).toBeDisabled();await expect(peer.getByRole('button',{name:'Remove platform…',exact:true})).toBeDisabled();
  expect(await save(page)).toEqual(final);expect(messages.filter(m=>m.type==='soundEvent')).toEqual([]);expect(errors).toEqual([]);
 }finally{await context.close();}
});
