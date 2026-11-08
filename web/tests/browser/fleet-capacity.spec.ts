import {readFileSync,writeFileSync,mkdirSync} from 'node:fs';
import {resolve,join} from 'node:path';
import {createHash} from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {test,expect} from './fixtures.ts';
import {ready,openPanel,closePanel} from './browser-helpers.ts';
const root=resolve(process.env.FREETRAIN_FLEET_CAPACITY_FIXTURES??'../tmp/p5-28-capacity');
const output=resolve(process.env.FREETRAIN_ONLINE_PROFILE_EVIDENCE??'../tmp/p5-28-capacity/browser');
const hash=(b:Buffer)=>createHash('sha256').update(b).digest('hex');
const stats=(v:number[])=>{const a=[...v].sort((a,b)=>a-b);return {count:a.length,p50:a[Math.max(0,Math.ceil(a.length*.5)-1)],p95:a[Math.max(0,Math.ceil(a.length*.95)-1)],max:a.at(-1)};};
for(const mode of ['small','medium','large','network','empty'])test(`P5-28 ${mode} legal fleet resources, feedback, sustained motion and restart`,async({page,browser,baseURL,worldServer})=>{
 mkdirSync(output,{recursive:true});
 const bytes=readFileSync(join(root,mode,'world.json')),fixture=JSON.parse(readFileSync(join(root,mode,'measurement.json'),'utf8'));
 expect(hash(bytes)).toBe(fixture.saveSha256);
 const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 // Only instrument browser primitives; no production profiling hooks or altered simulation.
 await page.addInitScript(()=>{
  const w=window as any;w.cap={wire:[],frames:[],paint:[],images:{},delay:0,record:false};
  const nativeDecode=HTMLImageElement.prototype.decode;
  HTMLImageElement.prototype.decode=async function(){await nativeDecode.call(this);w.cap.images[this.src]={width:this.naturalWidth,height:this.naturalHeight,rgbaBytes:this.naturalWidth*this.naturalHeight*4};};
  const Native=window.WebSocket;
  window.WebSocket=class extends Native {
   override set onmessage(listener:((this:WebSocket,e:MessageEvent)=>any)|null){super.onmessage=listener&&((e:MessageEvent)=>{
    const m=JSON.parse(e.data);w.cap.wire.push({type:m.type,bytes:new TextEncoder().encode(e.data).length,at:performance.now(),tick:m.tick});
    if(w.cap.delay&&['delta','accepted','rejected'].includes(m.type))setTimeout(()=>listener.call(this,e),50);else listener.call(this,e);
   });}
   override get onmessage(){return super.onmessage;}
   override send(data:any){if(typeof data==='string'){const m=JSON.parse(data);if(m.type==='command'){
    w.cap.wire.push({type:'sent',bytes:new TextEncoder().encode(data).length,at:performance.now(),commandId:m.commandId});
    if(w.cap.delay){setTimeout(()=>{if(this.readyState===Native.OPEN)super.send(data);},50);return;}
   }}super.send(data);}
  };
  new MutationObserver(records=>{if(w.cap.record)for(const r of records)w.cap.paint.push(Number((r.target as HTMLCanvasElement).dataset.paintMs??0));}).observe(document,{subtree:true,attributes:true,attributeFilter:['data-paint-ms']});
  let last=0;function frame(now:number){if(w.cap.record&&last){w.cap.frames.push(now-last);}last=now;requestAnimationFrame(frame);}requestAnimationFrame(frame);
 });
 await page.goto('/');await ready(page);
 const metadata=async()=>(await(await page.request.get('/api/bootstrap?protocolVersion=7&contentVersion=terrain-content-1&contentVersion=reference-spatial-content-2&contentVersion=mixed-railway-content-1&metadataOnly=true')).json());
 const saved=async()=>(await page.request.get('/api/saves/download',{timeout:60000})).body();
 const began=Date.now();expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+(await metadata()).worldEpoch,{data:bytes,timeout:60000})).ok()).toBe(true);
 const uploadMs=Date.now()-began;
 const canvas=page.locator('canvas.world-canvas');
 const settled=async()=>{await ready(page);await expect(canvas).toHaveAttribute('data-assets','ready');await expect(canvas).toHaveAttribute('data-presentation','ready');await expect(canvas).toHaveAttribute('data-refinement','ready');};
 console.log(mode,'uploaded');const cold:number[]=[];
 for(let n=0;n<3;n++){const start=Date.now();await page.reload();await settled();cold.push(Date.now()-start);}
 expect(await saved()).toEqual(bytes);
 // Center on the first actual deployed car rather than the empty center of a large map.
 const initial=JSON.parse(bytes.toString()),target=initial.mixed.fleet.trains[0].cars[0].state.location;
 async function centerFleet(){
 const box=(await canvas.boundingBox())!;await page.mouse.move(box.x+box.width/2,box.y+box.height/2);await page.mouse.down({button:'middle'});
 await canvas.evaluate((c,{target,v})=>{const [cx,cy]=c.dataset.camera!.split(',').map(Number),zoom=Number(c.dataset.zoom),center=Math.floor((v-1)/2),b=c.getBoundingClientRect();
  const a=16*(target.x+target.y-center)+16,y=8*(-target.x+target.y+center)-16*target.z+8;
  c.dispatchEvent(new PointerEvent('pointermove',{bubbles:true,pointerId:1,button:1,buttons:4,clientX:b.x+b.width/2+(cx-a)*zoom,clientY:b.y+b.height/2+(cy-y)*zoom}));
 },{target,v:fixture.dimensions.v});await page.mouse.up({button:'middle'});await settled();
 const expected=[16*(target.x+target.y-Math.floor((fixture.dimensions.v-1)/2))+16,8*(-target.x+target.y+Math.floor((fixture.dimensions.v-1)/2))-16*target.z+8];
 await expect.poll(async()=>{const actual=(await canvas.getAttribute('data-camera'))!.split(',').map(Number);return Math.max(...actual.map((v,i)=>Math.abs(v-expected[i])));}).toBeLessThan(1);
 }
 await centerFleet();
 console.log(mode,'cold and centered');const cdp=await page.context().newCDPSession(page),bcdp=await browser.newBrowserCDPSession();await cdp.send('Performance.enable');
 let browserPeakRss=0;const memorySamples:any[]=[];
 async function memory(){const metrics=await cdp.send('Performance.getMetrics'),info=await bcdp.send('SystemInfo.getProcessInfo');
  const rss=Number(execFileSync('ps',['-o','rss=','-p',info.processInfo.map(p=>p.id).join(',')],{encoding:'utf8'}).trim().split(/\s+/).reduce((s,v)=>s+Number(v)*1024,0));browserPeakRss=Math.max(browserPeakRss,rss);
  const serverRss=Number(execFileSync('ps',['-o','rss=','-p',String(worldServer.pid!())],{encoding:'utf8'}).trim())*1024;
  memorySamples.push({at:Date.now(),browserRss:rss,metrics:Object.fromEntries(metrics.metrics.map(m=>[m.name,m.value])),serverRss});}
 const text=JSON.parse(readFileSync('src/locales/en/app.json','utf8'));
 const button=(key:string)=>page.getByRole('button',{name:['run','pause'].includes(key)?new RegExp(text[key]+'$'):text[key],exact:true});
 // Run/pause pairs preserve the actual running protocol and make all 60 feedback
 // samples independent of changes to a selected train or a stale construction draft.
 const feedback:{local:number[],rtt100:number[]}={local:[],rtt100:[]};
 for(const delayed of [false,true]){
  await page.evaluate(d=>(window as any).cap.delay=d?100:0,delayed);
  for(let n=0;n<15;n++)for(const key of ['run','pause']){
   const start=Date.now();await button(key).click();await expect(button(key==='run'?'pause':'run')).toBeVisible();feedback[delayed?'rtt100':'local'].push(Date.now()-start);
  }
 }
 await page.evaluate(()=>(window as any).cap.delay=0);
 console.log(mode,'feedback complete');const windows:any[]=[];
 for(const running of [false,true]){
  // Restart each measurement from the accepted scene to keep finite journal
  // headroom identical; no count/resource limit is raised for the experiment.
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+(await metadata()).worldEpoch,{data:bytes,timeout:60000})).ok()).toBe(true);await expect(canvas).toHaveAttribute('data-world-epoch',(await metadata()).worldEpoch);await settled();await centerFleet();
  await page.evaluate(()=>{(window as any).cap.frames=[];(window as any).cap.paint=[];(window as any).cap.wire=[];(window as any).cap.record=true;});
  if(running){await page.getByLabel(text.gameSpeed,{exact:true}).selectOption('0.125');await button('run').click();}
  console.log(mode,running?'running window':'paused window');const start=Date.now();for(let n=0;n<12;n++){await page.waitForTimeout(5000);await memory();}
  if(running)await button('pause').click();
  const trace=await page.evaluate(()=>{const c=(window as any).cap;c.record=false;return {frames:c.frames,paint:c.paint,wire:c.wire};});
  const state=JSON.parse((await saved()).toString());
  expect(state.mixed.fleet.trains).toHaveLength(fixture.trains);
  if(running)expect(Math.min(...state.mixed.fleet.trains.map((t:any)=>t.moveCount))).toBeGreaterThan(0);else expect(await saved()).toEqual(bytes);
  const view=await canvas.evaluate(c=>({camera:c.dataset.camera,zoom:c.dataset.zoom,width:c.width,height:c.height,dpr:devicePixelRatio}));
  windows.push({running,view,elapsedMs:Date.now()-start,frameMs:stats(trace.frames),paintMs:stats(trace.paint),trace,finalTick:state.clock?.absoluteMinute??state.tick,
   moves:state.mixed.fleet.trains.map((t:any)=>t.moveCount),transactions:state.mixed.accounts?.transactions?.length});
 }
 await cdp.send('HeapProfiler.collectGarbage');await memory();
 const images=await page.evaluate(()=>(window as any).cap.images);
 const final=await saved();const saveStart=Date.now();await openPanel(page,'saves');await page.getByLabel(text.saveSlot,{exact:true}).selectOption('slot2');await button('saveGame').click();await expect(page.getByTestId('save-feedback')).toHaveText(text.saved);const saveMs=Date.now()-saveStart;await closePanel(page);
 const context=await browser.newContext({baseURL}),peer=await context.newPage();const joinStart=Date.now();await peer.goto('/');await ready(peer);await expect(peer.locator('canvas')).toHaveAttribute('data-assets','ready');const joinMs=Date.now()-joinStart;await context.close();
 const restartStart=Date.now();await worldServer.restart!();await page.request.post('/api/access',{data:{secret:''}});await page.reload();await settled();const restartMs=Date.now()-restartStart;expect(await saved()).toEqual(final);
 await centerFleet();await page.evaluate(()=>new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve))));await settled();await page.screenshot({path:join(output,mode+'.png'),fullPage:true});expect(errors).toEqual([]);
 writeFileSync(join(output,mode+'.json'),JSON.stringify({mode,browser:browser.version(),viewport:page.viewportSize(),fixtureSha256:hash(bytes),uploadMs,coldMs:cold,
  feedback:{local:stats(feedback.local),rtt100:stats(feedback.rtt100),raw:feedback},windows,memorySamples,browserPeakRss,images,
  decodedImageRgbaEstimateBytes:Object.values(images).reduce((s:number,v:any)=>s+v.rgbaBytes,0),saveMs,joinMs,restartMs,finalSha256:hash(final),errors},null,2)+'\n');
 await cdp.detach();await bcdp.detach();
});
