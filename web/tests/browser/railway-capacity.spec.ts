import {test,expect,type Page,type Browser} from '@playwright/test';
import {spawn,execFileSync,type ChildProcess} from 'node:child_process';
import {readFileSync,mkdirSync,writeFileSync,mkdtempSync} from 'node:fs';
import {join,resolve} from 'node:path';
import {tmpdir} from 'node:os';
import {createHash} from 'node:crypto';
import {openPanel,closePanel} from './browser-helpers.ts';
const digest=(bytes:Buffer)=>createHash('sha256').update(bytes).digest('hex');

const serverURL='http://127.0.0.1:5420',webURL='http://127.0.0.1:4493';
const directory=resolve(process.env.FREETRAIN_ONLINE_PROFILE_EVIDENCE??'../tmp/p4-23-capacity/browser'),saves=mkdtempSync(join(tmpdir(),'freetrain-capacity-'));
const dll=resolve('../src/FreeTrain.Online.Server/bin/Release/net10.0/FreeTrain.Online.Server.dll');
const probe=resolve('../tools/TerrainTransportProbe/bin/Release/net10.0/TerrainTransportProbe.dll');
const boot='/api/bootstrap?protocolVersion=7&contentVersion=terrain-content-1&contentVersion=reference-spatial-content-2&contentVersion=mixed-railway-content-1&metadataOnly=true';
let server:ChildProcess,web:ChildProcess;
const log=(child:ChildProcess)=>{child.stdout?.on('data',()=>{});child.stderr?.on('data',b=>process.stderr.write(b));return child;};
async function startServer(){server=log(spawn('dotnet',[dll,'--urls',serverURL],{env:{...process.env,FreeTrainOnline__Saves__Directory:saves,
  FreeTrainOnline__Participants__0__Id:'viewer',FreeTrainOnline__Participants__0__Role:'observer',FreeTrainOnline__Participants__0__Secret:'capacity-observer'}}));
  await expect.poll(async()=>{try{return(await fetch(serverURL+'/health')).ok;}catch{return false;}},{timeout:60000}).toBe(true);
}
async function stop(child:ChildProcess){if(child.exitCode!==null)return;child.kill('SIGTERM');await new Promise<void>((resolve,reject)=>{const t=setTimeout(()=>{child.kill('SIGKILL');reject(new Error('Shutdown deadline'));},30000);child.once('exit',()=>{clearTimeout(t);resolve();});});}
async function ready(page:Page){await expect(page.getByTestId('connection')).toHaveText(/^(Connected to game|ゲームに接続済み)$/,{timeout:60000});await expect(page.locator('canvas')).toHaveAttribute('data-scene','live',{timeout:60000});if(['terrain-state-1','mixed-railway-1'].includes((await page.locator('canvas').getAttribute('data-ruleset'))??''))await expect(page.locator('canvas')).toHaveAttribute('data-assets','ready',{timeout:60000});await expect(page.locator('canvas')).toHaveAttribute('data-presentation','ready',{timeout:60000});}
const epoch=async(page:Page)=>(await(await page.request.get(boot)).json()).worldEpoch;
async function resident(page:Page){
  const cdp=await page.context().newCDPSession(page);await cdp.send('Performance.enable');
  const m=await cdp.send('Performance.getMetrics');await cdp.send('HeapProfiler.collectGarbage');
  const live=await cdp.send('Performance.getMetrics');await cdp.detach();
  return {...Object.fromEntries(m.metrics.map(m=>[m.name,m.value])),retainedJSHeapAfterGC:live.metrics.find(m=>m.name==='JSHeapUsedSize')!.value};
}
async function memorySampler(browser:Browser) {
  const cdp=await browser.newBrowserCDPSession();let busy=false,serverRss=0,browserRss=0;
  const rss=(ids:number[])=>{try{return execFileSync('ps',['-o','rss=','-p',ids.join(',')],{encoding:'utf8',stdio:['ignore','pipe','ignore']}).trim().split(/\s+/).reduce((n,s)=>n+Number(s)*1024,0);}catch{return 0;}};
  const collect=async()=>{if(busy)return;busy=true;try{const info=await cdp.send('SystemInfo.getProcessInfo');browserRss=Math.max(browserRss,rss(info.processInfo.map(p=>p.id)));if(server?.pid)serverRss=Math.max(serverRss,rss([server.pid]));}finally{busy=false;}};
  let pending=Promise.resolve();const timer=setInterval(()=>{if(!busy)pending=collect();},250);await collect();
  return {values:()=>({sampledServerPeakRss:serverRss,sampledBrowserPeakRss:browserRss,sampleIntervalMs:250}),stop:async()=>{clearInterval(timer);await pending;await cdp.detach();}};
}
test.beforeAll(async()=>{
  mkdirSync(directory,{recursive:true});await startServer();
  web=log(spawn('deno',['run','--no-config','--node-modules-dir=manual','-A','node_modules/vite/bin/vite.js','preview','--host','127.0.0.1','--port','4493','--strictPort'],{env:{...process.env,FREETRAIN_ONLINE_API_TARGET:serverURL}}));
  await expect.poll(async()=>{try{return(await fetch(webURL)).ok;}catch{return false;}}).toBe(true);
});
test.afterAll(async()=>{if(web)await stop(web);if(server)await stop(server);});

const fixtureRoot=resolve(process.env.FREETRAIN_RAILWAY_CAPACITY_FIXTURES??'../tmp/p4-23-capacity');
const stats=(values:number[])=>{const a=[...values].sort((a,b)=>a-b);return {p50:a[Math.ceil(a.length*.5)-1],p95:a[Math.ceil(a.length*.95)-1],max:a.at(-1)};};
async function timedButton(page:Page,label:string,selector:string,text:string){return page.evaluate(({label,selector,text})=>new Promise<number>((resolve,reject)=>{
 const element=document.querySelector(selector)!,start=performance.now();
 const timeout=setTimeout(()=>{observer.disconnect();reject(new Error('Feedback deadline '+label));},30000);
 const observer=new MutationObserver(()=>{if(element.textContent?.includes(text)){clearTimeout(timeout);observer.disconnect();resolve(performance.now()-start);}});
 observer.observe(element,{childList:true,subtree:true,characterData:true});
 [...document.querySelectorAll('button')].find(b=>b.textContent===label)!.click();
}),{label,selector,text});}
async function frame(page:Page){await page.evaluate(()=>new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r))));}
for(const mode of ['small','medium','large','limit','route','empty','heavy'])for(const locale of (mode==='empty'||mode==='heavy'?['en','ja']:['en']))test(`P4-23 ${mode} ${locale}: legal rails, cold/steady interaction, chunks and exact restart`,async({page,browser})=>{
 test.setTimeout(600000);const memory=await memorySampler(browser),errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 const measurement=JSON.parse(readFileSync(join(fixtureRoot,mode,'measurement.json'),'utf8'));
 const text=JSON.parse(readFileSync(`src/locales/${locale}/app.json`,'utf8'));
 const bytes=readFileSync(join(fixtureRoot,mode,'world.json')),cold:number[]=[],paints:number[]=[],picks:number[]=[],zooms:number[]=[],previews:number[]=[],feedback:number[]=[],local:number[]=[],rendered:number[]=[];
 await page.addInitScript(()=>{
  const Native=window.WebSocket,w=window as any;w.railCapacityWire=[];w.railCapacityChunks=0;
  window.WebSocket=class extends Native{
   override set onmessage(listener:((this:WebSocket,e:MessageEvent)=>any)|null){super.onmessage=listener&&((e:MessageEvent)=>{
    const m=JSON.parse(e.data);if(m.type==='stateChunk')w.railCapacityChunks++;
    if(['delta','accepted','rejected'].includes(m.type))w.railCapacityWire.push({type:m.type,bytes:e.data.length,received:performance.now()});
    if(w.railCapacityDelay&&['delta','accepted','rejected'].includes(m.type))setTimeout(()=>listener.call(this,e),50);else listener.call(this,e);
   });}
   override get onmessage(){return super.onmessage;}
   override send(data:string|ArrayBufferLike|Blob|ArrayBufferView){
    if(typeof data==='string'&&JSON.parse(data).type==='command'){
     w.railCapacityWire.push({type:'sent',bytes:data.length,at:performance.now()});
     if(w.railCapacityDelay){setTimeout(()=>{if(this.readyState===Native.OPEN)super.send(data);},50);return;}
    }super.send(data);
   }
  };
 });
 try{
  await page.goto('/');await ready(page);const start=Date.now();
  const response=await page.request.post('/api/saves/upload?expectedWorldEpoch='+await epoch(page),{data:bytes,timeout:60000});expect(response.ok(),await response.text()).toBe(true);
  const uploadMs=Date.now()-start;await expect(page.locator('canvas')).toHaveAttribute('data-world-epoch',await epoch(page));await ready(page);
  expect(digest(await(await page.request.get('/api/saves/download',{timeout:60000})).body())).toBe(digest(bytes));
  console.log(mode,locale,'uploaded');
  for(let i=0;i<3;i++){const began=Date.now();await page.reload();await ready(page);await expect(page.locator('canvas')).toHaveAttribute('data-refinement','ready');cold.push(Date.now()-began);}
  if(locale==='ja'){await openPanel(page,'settings');await page.getByLabel('Language',{exact:true}).selectOption(locale);await closePanel(page);}
  const canvas=page.locator('canvas');await canvas.evaluate(c=>c.dataset.instance='rail-capacity');
  console.log(mode,locale,'cold reloads complete');
  // Pan through the actual pointer path to the ring/network, using projection.
  const target=measurement.networkOrigin??JSON.parse(bytes.toString()).mixed.voxels[mode==='route'?1024:0].position;
  const panBox=(await canvas.boundingBox())!;await page.mouse.move(panBox.x+panBox.width/2,panBox.y+panBox.height/2);await page.mouse.down({button:'middle'});
  await canvas.evaluate((c,{target,v})=>{
   const [cx,cy]=c.dataset.camera!.split(',').map(Number),zoom=Number(c.dataset.zoom),center=Math.floor((v-1)/2);
   const a=16*(target.x+target.y-center)+16,b=8*(-target.x+target.y+center)-16*target.z+8;
   c.dispatchEvent(new PointerEvent('pointermove',{bubbles:true,pointerId:1,button:1,buttons:4,clientX:c.getBoundingClientRect().x+c.getBoundingClientRect().width/2+(cx-a)*zoom,clientY:c.getBoundingClientRect().y+c.getBoundingClientRect().height/2+(cy-b)*zoom}));
  },{target,v:measurement.v});await page.mouse.up({button:'middle'});await frame(page);
  await expect(canvas).toHaveAttribute('data-refinement','ready');
  for(let n=0;n<30;n++){
   const box=(await canvas.boundingBox())!;await page.mouse.move(box.x+box.width/2,box.y+box.height/2);await page.mouse.down({button:'middle'});await page.mouse.move(box.x+box.width/2+(n%2? -16:16),box.y+box.height/2);await page.mouse.up({button:'middle'});await frame(page);paints.push(Number(await canvas.getAttribute('data-paint-ms')));
   await expect(canvas).toHaveAttribute('data-refinement','ready');
   const began=Date.now();await page.mouse.click(box.x+box.width/2,box.y+box.height/2);await expect(page.getByTestId('selected-object')).toBeVisible();picks.push(Date.now()-began);await closePanel(page);
  }
  console.log(mode,locale,'pan/pick complete');
  for(let n=0;n<30;n++){
   const ms=await canvas.evaluate(async(c,n)=>{const before=c.dataset.zoom!,box=c.getBoundingClientRect(),began=performance.now();c.dispatchEvent(new WheelEvent('wheel',{bubbles:true,cancelable:true,deltaMode:1,deltaY:n%2?1:-1,clientX:box.x+box.width/2,clientY:box.y+box.height/2}));while(c.dataset.zoom===before){if(performance.now()-began>2000)throw new Error('Zoom feedback deadline');await new Promise(requestAnimationFrame);}return performance.now()-began;},n);zooms.push(ms);
   await expect(canvas).toHaveAttribute('data-refinement','ready');
  }
  console.log(mode,locale,'zoom complete');
  if(mode==='empty'||mode==='heavy'){
   await page.getByRole('button',{name:text.track,exact:true}).click();
   for(let n=0;n<30;n++){
    if(n%5===0)console.log(mode,locale,'construction',n);
    const o=measurement.networkOrigin;
    for(const [label,value] of [[text.railFromX,o.x+4+n],[text.railFromY,o.y+32],[text.railToX,o.x+5+n],[text.railToY,o.y+32],[text.buildLevel,2]] as const)await page.getByLabel(label,{exact:true}).fill(String(value));
    const began=Date.now();local.push(await timedButton(page,text.railCheck,'[data-testid="rail-status"]',text.checkingGeometry));
    await expect(page.getByRole('button',{name:text.confirmTrack,exact:true})).toBeEnabled();previews.push(Date.now()-began);
    await page.evaluate(n=>(window as any).railCapacityDelay=n>=15,n);
    const committed=Date.now();feedback.push(await timedButton(page,text.confirmTrack,'.command-feedback',text.accepted));
    await frame(page);await expect(canvas).toHaveAttribute('data-presentation','ready');rendered.push(Date.now()-committed);
    await expect(page.getByRole('button',{name:text.confirmTrack,exact:true})).toBeDisabled();
   }
   await closePanel(page);
  }
  console.log(mode,locale,'construction complete');
  const final=await(await page.request.get('/api/saves/download',{timeout:60000})).body();
  const finalSha=digest(final);writeFileSync(join(directory,mode+'-'+locale+'-final.json'),final);
  const wire=await page.evaluate(()=>({events:(window as any).railCapacityWire,chunks:(window as any).railCapacityChunks}));
  await page.getByRole('button',{name:text.referenceInspect,exact:true}).click();await frame(page);await expect(canvas).toHaveAttribute('data-presentation','ready');
  const beforeImage=await canvas.evaluate(c=>c.toDataURL());await page.setViewportSize({width:1882,height:1040});await frame(page);await page.setViewportSize({width:1880,height:1040});await frame(page);await expect(canvas).toHaveAttribute('data-presentation','ready');
  const afterImage=await canvas.evaluate(c=>c.toDataURL());writeFileSync(join(directory,mode+'-'+locale+'-cached.png'),Buffer.from(beforeImage.split(',')[1],'base64'));writeFileSync(join(directory,mode+'-'+locale+'-fresh.png'),Buffer.from(afterImage.split(',')[1],'base64'));
  expect(digest(Buffer.from(await canvas.evaluate(c=>c.toDataURL())))).toBe(digest(Buffer.from(beforeImage)));
  await page.screenshot({path:join(directory,mode+'-'+locale+'.png'),fullPage:true});
  const rejoinStart=Date.now();await page.reload();await ready(page);const rejoinMs=Date.now()-rejoinStart;
  const observerContext=await browser.newContext({baseURL:webURL});
  try{const observer=await observerContext.newPage();await observer.request.post('/api/access',{data:{secret:'capacity-observer'}});await observer.goto('/');await ready(observer);
   const restartStart=Date.now();await stop(server);await startServer();await page.request.post('/api/access',{data:{secret:''}});await observer.request.post('/api/access',{data:{secret:'capacity-observer'}});await page.reload();await observer.reload();await ready(page);await ready(observer);
   expect(digest(await(await page.request.get('/api/saves/download',{timeout:60000})).body())).toBe(finalSha);
   const report={mode,locale,viewport:page.viewportSize(),dpr:await page.evaluate(()=>devicePixelRatio),browser:browser.version(),uploadMs,cold,rejoinMs,restartMs:Date.now()-restartStart,paints,picks,zooms,local,previews,feedback,rendered,wire,finalSha,
    summary:{paint:stats(paints),pick:stats(picks),zoom:stats(zooms),local:stats(local),preview:stats(previews),commitLocal:stats(feedback.slice(0,15)),commit100msRTT:stats(feedback.slice(15))},targets:{localFeedbackP95Below100ms:local.length?stats(local).p95<100:null,authoritativeP95Below250msAt100msRTT:feedback.length?stats(feedback.slice(15)).p95<250:null,paintP95Below33_4ms:stats(paints).p95<33.4,pickP95Below100ms:stats(picks).p95<100,zoomP95Below100ms:stats(zooms).p95<100},
    canvas:await canvas.evaluate(c=>({width:c.width,height:c.height,bounds:c.getBoundingClientRect().toJSON(),camera:c.dataset.camera,zoom:c.dataset.zoom})),...memory.values(),...await resident(page)};
   writeFileSync(join(directory,mode+'-'+locale+'.json'),JSON.stringify(report,null,2)+'\n');
   // P4-23 records each performance miss explicitly in targets; resource and
   // correctness failures remain assertions. No P3 latency waiver is inherited.
   expect(report.sampledServerPeakRss).toBeLessThan(6*1024**3);expect(report.sampledBrowserPeakRss).toBeLessThan(6*1024**3);expect(errors).toEqual([]);
  }finally{await observerContext.close();}
 }finally{await memory.stop();}
});

// Separately sample an exact railway owner and the unreserved resource field;
// the main scene cases measure the constructed network viewport.
test('P4-23 heavy resource field and exact railway picking while shared time runs',async({page,browser})=>{
 test.setTimeout(180000);const memory=await memorySampler(browser);
 try{
  const fixture=JSON.parse(readFileSync(join(fixtureRoot,'heavy','measurement.json'),'utf8'));
  const bytes=readFileSync(join(fixtureRoot,'heavy','world.json')),saved=JSON.parse(bytes.toString());
  await page.goto('/');await ready(page);
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+await epoch(page),{data:bytes,timeout:60000})).ok()).toBe(true);
  const canvas=page.locator('canvas');await expect(canvas).toHaveAttribute('data-world-epoch',await epoch(page));await ready(page);
  async function panTo(target:{x:number,y:number,z:number}){
   const box=(await canvas.boundingBox())!;await page.mouse.move(box.x+box.width/2,box.y+box.height/2);await page.mouse.down({button:'middle'});
   await canvas.evaluate((c,p)=>{const r=c.getBoundingClientRect(),[cx,cy]=c.dataset.camera!.split(',').map(Number),zoom=Number(c.dataset.zoom);
    const x=16*(p.x+p.y-544)+16,y=8*(-p.x+p.y+544)-16*p.z+8;
    c.dispatchEvent(new PointerEvent('pointermove',{bubbles:true,pointerId:1,button:1,buttons:4,clientX:r.x+r.width/2+(cx-x)*zoom,clientY:r.y+r.height/2+(cy-y)*zoom}));
   },target);await page.mouse.up({button:'middle'});await frame(page);await expect(canvas).toHaveAttribute('data-presentation','ready');
  }
  const p={x:fixture.networkOrigin.x+6,y:fixture.networkOrigin.y+4,z:2};await panTo(p);
  const owner=saved.mixed.voxels.find((v:any)=>v.position.x===p.x&&v.position.y===p.y&&v.position.z===p.z).ownerId;
  const box=(await canvas.boundingBox())!,railPicks:number[]=[];
  for(let n=0;n<30;n++){
   const began=Date.now();await page.mouse.click(box.x+box.width/2+.75,box.y+box.height/2+.75);
   await expect(page.getByTestId('selected-object')).toHaveText(`Railway · ${owner} · ${p.x}, ${p.y}, ${p.z}`);
   railPicks.push(Date.now()-began);await closePanel(page);
  }
  // H750,V545 is outside the clear construction reservation.
  await panTo({x:750-Math.floor(545/2)+544,y:750+Math.ceil(545/2),z:2});
  await page.getByRole('button',{name:'Run',exact:false}).first().click();await expect(page.getByTestId('clock-state')).toHaveText('Running');
  const paints:number[]=[];
  for(let n=0;n<30;n++){
   await page.mouse.move(box.x+box.width/2,box.y+box.height/2);await page.mouse.down({button:'middle'});await page.mouse.move(box.x+box.width/2+(n%2?-32:32),box.y+box.height/2+8);await page.mouse.up({button:'middle'});await frame(page);
   paints.push(Number(await canvas.getAttribute('data-paint-ms')));await expect(canvas).toHaveAttribute('data-presentation','ready');
  }
  const zoomSamples=await canvas.evaluate(async c=>{
   const samples:{feedbackMs:number,settledMs:number}[]=[];
   for(let n=0;n<30;n++){
    const r=c.getBoundingClientRect(),before=c.dataset.zoom,start=performance.now();
    c.dispatchEvent(new WheelEvent('wheel',{bubbles:true,cancelable:true,deltaMode:1,deltaY:n%2?1:-1,clientX:r.x+r.width/2,clientY:r.y+r.height/2}));
    while(c.dataset.zoom===before){if(performance.now()-start>30000)throw new Error('Zoom deadline');await new Promise(requestAnimationFrame);}
    const feedbackMs=performance.now()-start;
    while(c.dataset.presentation!=='ready'){if(performance.now()-start>30000)throw new Error('Refinement deadline');await new Promise(requestAnimationFrame);}
    samples.push({feedbackMs,settledMs:performance.now()-start});
   }return samples;
  });
  await page.getByRole('button',{name:'Pause',exact:false}).first().click();await expect(page.getByTestId('clock-state')).toHaveText('Paused');
  await page.screenshot({path:join(directory,'heavy-resource-field.png'),fullPage:true});
  const final=JSON.parse((await(await page.request.get('/api/saves/download',{timeout:60000})).body()).toString());
  expect(final.mixed).toEqual(saved.mixed);expect(final.packedTerrain).toEqual(saved.packedTerrain);expect(BigInt(final.clock.absoluteMinute)).toBeGreaterThan(BigInt(saved.clock.absoluteMinute));
  writeFileSync(join(directory,'heavy-resource-field.json'),JSON.stringify({railPicks,paints,railPick:stats(railPicks),paint:stats(paints),zoomSamples,zoomFeedback:stats(zoomSamples.map(s=>s.feedbackMs)),zoomSettled:stats(zoomSamples.map(s=>s.settledMs)),owner,clockAdvanced:true,...memory.values()},null,2)+'\n');
 }finally{await memory.stop();}
});

for(const kind of ['voxel-edge','journal'])for(const locale of ['en','ja'])test(`P4 upper limit ${kind} ${locale}: last slot, useful rejection and unchanged recoverable world`,async({page})=>{
 test.setTimeout(180000);
 const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.addInitScript(()=>{
  const Native=window.WebSocket;
  window.WebSocket=class extends Native{
   constructor(url:string|URL,protocols?:string|string[]){super(url,protocols);(window as any).limitSocket=this;}
  };
 });
 const bytes=readFileSync(join(fixtureRoot,kind,'world.json')),initial=JSON.parse(bytes.toString());
 const text=JSON.parse(readFileSync(`src/locales/${locale}/app.json`,'utf8'));
 await page.goto('/');await ready(page);
 expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+await epoch(page),{data:bytes,timeout:60000})).ok()).toBe(true);
 await expect(page.locator('canvas')).toHaveAttribute('data-world-epoch',await epoch(page));await ready(page);
 if(locale==='ja'){await openPanel(page,'settings');await page.getByLabel('Language',{exact:true}).selectOption('ja');await closePanel(page);}
 const snapshot=async()=>await(await page.request.get('/api/saves/download',{timeout:60000})).body();
 expect(digest(await snapshot())).toBe(digest(bytes));
 await page.getByRole('button',{name:text.track,exact:true}).click();
 const x=kind==='journal'?32:812,y=kind==='journal'?32:750;
 async function preview(start:number){
  for(const [label,value] of [[text.railFromX,start],[text.railFromY,y],[text.railToX,start+1],[text.railToY,y],[text.buildLevel,2]] as const)await page.getByLabel(label,{exact:true}).fill(String(value));
  const pending=page.waitForResponse(r=>r.url().includes('/api/railway/preview'));
  await page.getByRole('button',{name:text.railCheck,exact:true}).click();return(await pending).json();
 }
 const allowed=await preview(x);expect(allowed.legal).toBe(true);
 await expect(page.getByRole('button',{name:text.confirmTrack,exact:true})).toBeEnabled();
 expect(digest(await snapshot())).toBe(digest(bytes));
 await page.getByRole('button',{name:text.confirmTrack,exact:true}).click();
 await expect(page.locator('.command-feedback')).toHaveText(text.accepted);
 await expect(page.getByRole('button',{name:text.confirmTrack,exact:true})).toBeDisabled();
 const full=await snapshot(),saved=JSON.parse(full.toString());
 expect(saved.mixed.voxels.length).toBe(kind==='journal'?3:8192);
 expect(saved.mixed.accounts.transactions.length).toBe(initial.mixed.accounts.transactions.length+1);
 if(kind==='journal')expect(saved.mixed.accounts.transactions.length).toBe(8192);
 const rejected=await preview(x+1),reason=kind==='journal'?'accountCapacityLimit':'railwayCapacityLimit';
 expect(rejected).toMatchObject({legal:false,reason,cost:'0'});
 await expect(page.getByTestId('rail-status')).toHaveText(text[kind==='journal'?'railJournalCapacity':'railVoxelCapacity']);
 await expect(page.getByRole('button',{name:text.confirmTrack,exact:true})).toBeDisabled();
 expect(digest(await snapshot())).toBe(digest(full));
 // An untrusted client bypassing the disabled button must also be rejected,
 // without closing the connection, changing money/IDs or terminating the host.
 const receipt=await page.evaluate(query=>new Promise<any>((resolve,reject)=>{
  const socket=(window as any).limitSocket as WebSocket,requestId='forced-capacity-overflow';
  const timeout=setTimeout(()=>{socket.removeEventListener('message',listener);reject(new Error('Overflow receipt deadline'));},10000);
  const listener=(event:MessageEvent)=>{const m=JSON.parse(event.data);if(m.requestId===requestId){clearTimeout(timeout);socket.removeEventListener('message',listener);resolve(m);}};
  socket.addEventListener('message',listener);socket.send(JSON.stringify({type:'command',requestId,expectedWorldEpoch:query.expectedWorldEpoch,payload:{kind:'editRailway',query}}));
 }),rejected.query);
 expect(receipt).toMatchObject({type:'rejected',code:'invalidPlacement'});
 expect((await page.request.get('/health')).ok()).toBe(true);await ready(page);
 expect(digest(await snapshot())).toBe(digest(full));
 await page.setViewportSize({width:430,height:850});await page.getByTestId('rail-status').scrollIntoViewIfNeeded();
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
 await page.screenshot({path:join(directory,`upper-limit-${kind}-${locale}.png`),fullPage:true});
 await page.setViewportSize({width:1280,height:900});
 // The limit and the world survive a canonical reopen, rather than resetting
 // the journal or discarding entities to make room.
 expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+await epoch(page),{data:full,timeout:60000})).ok()).toBe(true);
 await page.reload();await ready(page);expect(digest(await snapshot())).toBe(digest(full));
 await page.getByRole('button',{name:text.step,exact:false}).click();
 await expect(page.locator('.command-feedback')).toHaveText(text.accepted);
 const stepped=JSON.parse((await snapshot()).toString());
 expect(BigInt(stepped.clock.absoluteMinute)).toBeGreaterThan(BigInt(saved.clock.absoluteMinute));
 expect(stepped.mixed).toEqual(saved.mixed);expect(stepped.packedTerrain).toEqual(saved.packedTerrain);
 expect(errors).toEqual([]);
 writeFileSync(join(directory,`upper-limit-${kind}-${locale}.json`),JSON.stringify({kind,locale,railwayVoxels:saved.mixed.voxels.length,accountEntries:saved.mixed.accounts.transactions.length,reason,receipt,sha256:digest(full),noPartialEffect:true,reopenedExactly:true,clockStillWorks:true,pageErrors:errors},null,2)+'\n');
});
