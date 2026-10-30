import {test,expect,type Page,type Browser} from '@playwright/test';
import {spawn,execFileSync,type ChildProcess} from 'node:child_process';
import {readFileSync,mkdirSync,writeFileSync,mkdtempSync} from 'node:fs';
import {join,resolve} from 'node:path';
import {tmpdir} from 'node:os';
import {createHash} from 'node:crypto';
import {openPanel,closePanel} from './ux-helpers.ts';
const digest=(bytes:Buffer)=>createHash('sha256').update(bytes).digest('hex');

const serverURL='http://127.0.0.1:5420',webURL='http://127.0.0.1:4493';
const directory=resolve(process.env.FREETRAIN_ONLINE_PROFILE_EVIDENCE??'../tmp/p3-capacity/browser-capacity'),saves=mkdtempSync(join(tmpdir(),'freetrain-capacity-'));
const dll=resolve('../src/FreeTrain.Online.Server/bin/Release/net10.0/FreeTrain.Online.Server.dll');
const probe=resolve('../tools/TerrainTransportProbe/bin/Release/net10.0/TerrainTransportProbe.dll');
const boot='/api/bootstrap?protocolVersion=5&contentVersion=terrain-content-1&contentVersion=reference-spatial-content-2&metadataOnly=true';
let server:ChildProcess,web:ChildProcess;
const log=(child:ChildProcess)=>{child.stdout?.on('data',()=>{});child.stderr?.on('data',b=>process.stderr.write(b));return child;};
async function startServer(){server=log(spawn('dotnet',[dll,'--urls',serverURL],{env:{...process.env,FreeTrainOnline__Saves__Directory:saves,
  FreeTrainOnline__Participants__0__Id:'viewer',FreeTrainOnline__Participants__0__Role:'observer',FreeTrainOnline__Participants__0__Secret:'capacity-observer'}}));
  await expect.poll(async()=>{try{return(await fetch(serverURL+'/health')).ok;}catch{return false;}},{timeout:60000}).toBe(true);
}
async function stop(child:ChildProcess){if(child.exitCode!==null)return;child.kill('SIGTERM');await new Promise<void>((resolve,reject)=>{const t=setTimeout(()=>{child.kill('SIGKILL');reject(new Error('Shutdown deadline'));},30000);child.once('exit',()=>{clearTimeout(t);resolve();});});}
async function ready(page:Page){await expect(page.getByTestId('connection')).toHaveText('Connected to game',{timeout:60000});await expect(page.locator('canvas')).toHaveAttribute('data-scene','live',{timeout:60000});if(await page.locator('canvas').getAttribute('data-ruleset')==='terrain-state-1')await expect(page.locator('canvas')).toHaveAttribute('data-assets','ready',{timeout:60000});}
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
for(const [h,v,z,mode] of [[256,256,64,'dense'],[1500,1090,45,'heavy'],[1500,1090,45,'generated']] as const) {
test(`${h}×${v}×${z} ${mode}: atomic transfer, viewport picking, reconnect, exact save and checkpoint restart`,async({page,browser})=>{
  const memory=await memorySampler(browser);
  try {
  // Delay control traffic inside the page. Relaying every 87 MB snapshot through
  // Playwright's protocol would measure the relay's memory and tracing overhead.
  await page.addInitScript(()=>{
    const Native=window.WebSocket,w=window as any;
    window.WebSocket=class extends Native {
      override set onmessage(listener:((this:WebSocket,event:MessageEvent)=>any)|null) {
        super.onmessage=listener&&((event:MessageEvent)=>{
          const delayed=w.capacityDelayControls&&typeof event.data==='string'&&event.data.length<4096&&['delta','accepted','rejected'].includes(JSON.parse(event.data).type);
          if(delayed)setTimeout(()=>listener.call(this,event),50);else listener.call(this,event);
        });
      }
      override get onmessage(){return super.onmessage;}
      override send(data:string|ArrayBufferLike|Blob|ArrayBufferView) {
        if(w.capacityDelayControls&&typeof data==='string'&&JSON.parse(data).type==='command')setTimeout(()=>{if(this.readyState===Native.OPEN)super.send(data);},50);
        else super.send(data);
      }
    };
  });
  const fixture=resolve(`../tmp/p3-capacity/${h}-${mode}`);
  execFileSync('dotnet',[probe,'--capacity',String(h),String(v),String(z),mode,fixture],{stdio:'pipe',timeout:60000});
  const bytes=readFileSync(join(fixture,'world.json')),before=Date.now(),errors:string[]=[];
  page.on('pageerror',e=>errors.push(String(e)));await page.goto('/');await ready(page);
  await page.locator('canvas').evaluate(c=>c.dataset.instance='capacity-preserved');
  const e=await epoch(page),uploadStart=Date.now();
  const uploaded=await page.request.post('/api/saves/upload?expectedWorldEpoch='+e,{data:bytes,timeout:60000});expect(uploaded.ok()).toBe(true);
  const applyMs=Date.now()-uploadStart;await expect.poll(()=>epoch(page)).not.toBe(e);await expect(page.locator('canvas')).toHaveAttribute('data-world-epoch',await epoch(page));await ready(page);
  await expect(page.locator('canvas')).toHaveAttribute('data-instance','capacity-preserved');
  expect(digest(await(await page.request.get('/api/saves/download',{timeout:60000})).body())).toBe(digest(bytes));
  const box=(await page.locator('canvas').boundingBox())!,samples:number[]=[];
  await page.evaluate(()=>{const w=window as any;w.capacityFrames=[];w.capacityLast=undefined;w.capacityRecording=true;const record=(t:number)=>{if(!w.capacityRecording)return;if(w.capacityLast!==undefined)w.capacityFrames.push(t-w.capacityLast);w.capacityLast=t;requestAnimationFrame(record);};requestAnimationFrame(record);});
  for(let n=0;n<20;n++){
    await page.mouse.move(box.x+box.width/2,box.y+box.height/2);await page.mouse.down({button:'middle'});
    await page.mouse.move(box.x+box.width/2+32,box.y+box.height/2+8);await page.mouse.up({button:'middle'});
    await page.evaluate(()=>new Promise(requestAnimationFrame));samples.push(Number(await page.locator('canvas').getAttribute('data-paint-ms')));
  }
  const intervals=await page.evaluate(()=>{const w=window as any;w.capacityRecording=false;return w.capacityFrames as number[];});intervals.sort((a,b)=>a-b);
  writeFileSync(join(directory,`${h}-${mode}-pan.json`),JSON.stringify({samples},null,2)+'\n');
  await expect(page.locator('canvas')).toHaveAttribute('data-refinement','ready');
  const image=await page.locator('canvas').evaluate(c=>c.toDataURL()),size=page.viewportSize()!;
  const cachedCamera=await page.locator('canvas').getAttribute('data-camera');
  await page.setViewportSize({...size,width:size.width+2});await page.evaluate(()=>new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r))));
  await page.setViewportSize(size);await page.evaluate(()=>new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r))));await expect(page.locator('canvas')).toHaveAttribute('data-refinement','ready');
  const fresh=await page.locator('canvas').evaluate(c=>c.toDataURL());
  writeFileSync(join(directory,`${h}-${mode}-cached.png`),Buffer.from(image.split(',')[1],'base64'));
  writeFileSync(join(directory,`${h}-${mode}-fresh.png`),Buffer.from(fresh.split(',')[1],'base64'));
  expect(await page.locator('canvas').getAttribute('data-camera')).toBe(cachedCamera);
  expect(digest(Buffer.from(fresh))).toBe(digest(Buffer.from(image)));
  const pickStart=Date.now();await page.mouse.click(box.x+box.width/2,box.y+box.height/2);await expect(page.getByTestId('selected-object')).toContainText('Terrain');const pickMs=Date.now()-pickStart;
  const rejoinStart=Date.now();await page.reload();await ready(page);const rejoinMs=Date.now()-rejoinStart;
  const observer=await browser.newContext({baseURL:webURL});const watcher=await observer.newPage();
  try{
    await watcher.goto('/');await ready(watcher);await watcher.request.post('/api/leave');await watcher.request.post('/api/access',{data:{secret:'capacity-observer'}});await watcher.reload();await ready(watcher);
    expect((await watcher.request.post('/api/saves/upload?expectedWorldEpoch='+await epoch(watcher),{data:'{}'})).status()).toBe(403);
    await page.getByRole('button',{name:'Run',exact:false}).first().click();await expect(watcher.getByTestId('clock-state')).toHaveText('Running');
    await watcher.reload();await ready(watcher); // A coherent join while shared time advances.
    await page.getByRole('button',{name:'Pause',exact:false}).first().click();await expect(watcher.getByTestId('clock-state')).toHaveText('Paused');
    await expect(page.getByRole('button',{name:'Single step',exact:false}).first()).toBeEnabled();
    await page.evaluate(()=>(window as any).capacityDelayControls=true);const steps:number[]=[];
    for(let i=0;i<10;i++) {
      steps.push(await page.evaluate(()=>new Promise<number>((resolve,reject)=>{
        const feedback=document.querySelector('.command-feedback')!,start=performance.now();
        const timeout=setTimeout(()=>{observer.disconnect();reject(new Error('Command feedback deadline'));},5000);
        const observer=new MutationObserver(()=>{if(feedback.textContent==='Confirmed by server'){clearTimeout(timeout);observer.disconnect();resolve(performance.now()-start);}});
        observer.observe(feedback,{childList:true,characterData:true,subtree:true});
        (Array.from(document.querySelectorAll('button')).find(b=>b.textContent?.includes('Single step')) as HTMLButtonElement).click();
      })));
    }
    await page.evaluate(()=>(window as any).capacityDelayControls=false);steps.sort((a,b)=>a-b);const stepP95=steps[Math.floor(steps.length*.95)];
    const final=await(await page.request.get('/api/saves/download')).body();
    const restartStart=Date.now();await stop(server);await startServer();
    // Credentials are incarnation-local. Re-authenticate explicitly after a
    // restart rather than weakening the access gate for a large-world reconnect.
    await page.request.post('/api/access',{data:{secret:''}});await watcher.request.post('/api/access',{data:{secret:'capacity-observer'}});
    await page.reload();await watcher.reload();await ready(page);await ready(watcher);
    expect(digest(await(await page.request.get('/api/saves/download',{timeout:60000})).body())).toBe(digest(final));
    samples.sort((a,b)=>a-b);const report={h,v,z,mode,applyMs,rejoinMs,pickMs,stepP95,injectedCommandRttMs:100,restartMs:Date.now()-restartStart,paintP95:samples[Math.floor(samples.length*.95)],frameIntervalP95:intervals[Math.floor(intervals.length*.95)],elapsedMs:Date.now()-before,chrome:await resident(page),...memory.values()};
    writeFileSync(join(directory,`${h}-${mode}.json`),JSON.stringify(report,null,2)+'\n');
    expect(report.paintP95).toBeLessThan(33.4);expect(report.pickMs).toBeLessThan(100);expect(report.stepP95).toBeLessThan(250);
    expect(report.rejoinMs).toBeLessThan(60000);expect(report.restartMs).toBeLessThan(60000);
    expect(report.sampledServerPeakRss).toBeLessThan(6*1024**3);expect(report.sampledBrowserPeakRss).toBeLessThan(6*1024**3);
    await page.screenshot({path:join(directory,`${h}-${mode}.png`),fullPage:true});expect(errors).toEqual([]);
  }finally{await observer.close();}
  }finally{await memory.stop();}
});
}

for(const mode of ['heavy','generated'] as const)for(const dpr of mode==='heavy'?[1,1.25,2]:[1,2]) {
test(`${mode} DPR${dpr}: responsive zoom refinement, latest camera, picking and fresh pixels`,async({browser})=>{
  const context=await browser.newContext({baseURL:webURL,viewport:{width:1880,height:1040},deviceScaleFactor:dpr});
  const page=await context.newPage(),errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
  try {
    const fixture=resolve(`../tmp/p3-capacity/1500-${mode}`);
    execFileSync('dotnet',[probe,'--capacity','1500','1090','45',mode,fixture],{stdio:'pipe',timeout:60000});
    await page.goto('/');await ready(page);
    const response=await page.request.post('/api/saves/upload?expectedWorldEpoch='+await epoch(page),{data:readFileSync(join(fixture,'world.json')),timeout:60000});
    expect(response.ok()).toBe(true);
    const canvas=page.locator('canvas');await expect(canvas).toHaveAttribute('data-world-epoch',await epoch(page));await ready(page);
    await expect(canvas).toHaveAttribute('data-refinement','ready');
    const initialCamera=await canvas.getAttribute('data-camera');
    await canvas.evaluate(canvas=>{
      const w=window as any;
      w.zoomDiagnostics={lastInput:0,settled:0,paints:[],longTasks:[]};
      w.zoomPaintObserver=new MutationObserver(()=>{
        w.zoomDiagnostics.paints.push(Number(canvas.dataset.paintMs));
        if(canvas.dataset.refinement==='ready')w.zoomDiagnostics.settled=performance.now();
      });w.zoomPaintObserver.observe(canvas,{attributes:true,attributeFilter:['data-paint-ms']});
      w.zoomTaskObserver=new PerformanceObserver(list=>{w.zoomDiagnostics.longTasks.push(...list.getEntries().map(t=>t.duration));});
      w.zoomTaskObserver.observe({type:'longtask'});
    });
    const samples=await canvas.evaluate(async canvas=>{
      const box=canvas.getBoundingClientRect(),samples:{feedbackMs:number;paintMs:number;zoom:number;pending:boolean}[]=[];
      for(const deltaY of [...Array(9).fill(-1),...Array(17).fill(1),...Array(8).fill(-1)]) {
        const before=Number(canvas.dataset.zoom);
        if(deltaY<0&&before>=4||deltaY>0&&before<=.5)continue;
        const start=performance.now();
        (window as any).zoomDiagnostics.lastInput=start;
        await new Promise<void>((resolve,reject)=>{
          const timeout=setTimeout(()=>{observer.disconnect();reject(new Error('Zoom feedback deadline'));},3000);
          const observer=new MutationObserver(()=>{
            if(Number(canvas.dataset.zoom)!==before){clearTimeout(timeout);observer.disconnect();resolve();}
          });observer.observe(canvas,{attributes:true,attributeFilter:['data-zoom']});
          canvas.dispatchEvent(new WheelEvent('wheel',{bubbles:true,cancelable:true,deltaY,clientX:box.x+box.width*.37,clientY:box.y+box.height*.42}));
        });
        samples.push({feedbackMs:performance.now()-start,paintMs:Number(canvas.dataset.paintMs),zoom:Number(canvas.dataset.zoom),pending:canvas.dataset.refinement==='pending'});
      }
      return samples;
    });
    expect(samples.some(s=>s.zoom===4)).toBe(true);expect(samples.some(s=>s.zoom===.5)).toBe(true);
    expect(samples.every(s=>s.pending)).toBe(true);
    await expect(canvas).toHaveAttribute('data-refinement','ready');
    const refinement=await page.evaluate(()=>{
      const w=window as any,d=w.zoomDiagnostics;
      d.longTasks.push(...w.zoomTaskObserver.takeRecords().map((t:PerformanceEntry)=>t.duration));
      w.zoomPaintObserver.disconnect();w.zoomTaskObserver.disconnect();
      return {lastSettleMs:d.settled-d.lastInput,paints:d.paints as number[],longTasks:d.longTasks as number[]};
    });
    expect(refinement.lastSettleMs).toBeGreaterThan(0);expect(refinement.lastSettleMs).toBeLessThan(1000);
    const sort=(values:number[])=>values.sort((a,b)=>a-b),feedback=sort(samples.map(s=>s.feedbackMs)),paint=sort(samples.map(s=>s.paintMs));
    const p95=(values:number[])=>values[Math.ceil(values.length*.95)-1];
    expect(p95(feedback)).toBeLessThan(100);expect(Math.max(...feedback)).toBeLessThan(250);expect(p95(paint)).toBeLessThan(33.4);
    const fractionalBox=(await canvas.boundingBox())!;
    await page.mouse.click(fractionalBox.x+fractionalBox.width/2,fractionalBox.y+fractionalBox.height/2);
    await expect(page.getByTestId('selected-object')).toContainText('Terrain');await closePanel(page);
    await page.getByRole('button',{name:'Reset camera',exact:true}).click();await expect(canvas).toHaveAttribute('data-camera',initialCamera!);await expect(canvas).toHaveAttribute('data-refinement','ready');
    // Cancel an unfinished request by returning to the already published Home view.
    await canvas.dispatchEvent('wheel',{deltaY:-1,clientX:900,clientY:500});await expect(canvas).toHaveAttribute('data-refinement','pending');
    await page.getByRole('button',{name:'Reset camera',exact:true}).click();await expect(canvas).toHaveAttribute('data-camera',initialCamera!);await expect(canvas).toHaveAttribute('data-zoom','1.500');await expect(canvas).toHaveAttribute('data-refinement','ready');
    const box=(await canvas.boundingBox())!;
    await page.mouse.click(box.x+box.width/2,box.y+box.height/2);await expect(page.getByTestId('selected-object')).toContainText('Terrain');
    const selected=await page.getByTestId('selected-object').textContent();
    await page.getByRole('button',{name:'Close panel',exact:true}).click();
    // A fresh rebuild must produce the same settled image and pick at fixed scale.
    const before=await canvas.evaluate(c=>c.toDataURL());
    await page.setViewportSize({width:1882,height:1040});await page.evaluate(()=>new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r))));await expect(canvas).toHaveAttribute('data-refinement','ready');
    await page.setViewportSize({width:1880,height:1040});await page.evaluate(()=>new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r))));await expect(canvas).toHaveAttribute('data-refinement','ready');
    expect(digest(Buffer.from(await canvas.evaluate(c=>c.toDataURL())))).toBe(digest(Buffer.from(before)));
    await page.mouse.click(box.x+box.width/2,box.y+box.height/2);await expect(page.getByTestId('selected-object')).toHaveText(selected!);
    writeFileSync(join(directory,`zoom-${mode}-dpr${dpr}.json`),JSON.stringify({mode,dpr,samples,feedbackP95:p95(feedback),paintP95:p95(paint),refinement},null,2)+'\n');
    expect(errors).toEqual([]);
  }finally{await context.close();}
});
}

test('minimum parity extent is admitted through empty, heightmap and generated preview/confirmation workflows',async({page})=>{
  test.setTimeout(300000);await page.goto('/');await ready(page);
  for(const mode of ['empty','heightmap','generated']) {
    const fixture=resolve(`../tmp/p3-capacity/creation-${mode}`);
    execFileSync('dotnet',[probe,'--capacity','1500','1090','45',mode,fixture],{stdio:'pipe',timeout:60000});
    const expected=readFileSync(join(fixture,'world.json'));
    await openPanel(page,'newWorld');await page.getByLabel('World source',{exact:true}).selectOption(mode);
    if(mode==='heightmap')await page.getByLabel('Terrain image',{exact:true}).setInputFiles(join(fixture,'source.png'));
    if(mode==='generated') {
      await page.getByLabel('Terrain style',{exact:true}).selectOption('mountains');await page.getByLabel('Generation seed',{exact:true}).fill('-2147483648');
      await page.getByLabel('Relief (0–100)',{exact:true}).fill('100');await page.getByLabel('Feature scale (cells)',{exact:true}).fill('4');await page.getByLabel('Smoothness (0–100)',{exact:true}).fill('0');
    }
    await page.getByLabel('World name',{exact:true}).fill(mode==='empty'?'Capacity empty':mode==='heightmap'?'Capacity heightmap':'Capacity relief');
    await page.getByLabel('Stored width (H)',{exact:true}).fill('1500');await page.getByLabel('Stored length (V)',{exact:true}).fill('1090');
    await page.getByLabel('Levels (Z)',{exact:true}).fill('45');await page.getByLabel('Water level',{exact:true}).fill(mode==='empty'?'1':'4');
    const before=await epoch(page),start=Date.now(),response=page.waitForResponse(r=>r.url().includes(`/worlds/${mode}/preview`));await page.getByRole('button',{name:'Preview world',exact:true}).click();
    const result=await response;expect(result.ok(),result.ok()?'':await result.text()).toBe(true);
    await expect(page.getByTestId('empty-world-preview')).toBeVisible({timeout:60000});
    expect(await page.locator('canvas').count()).toBe(1);expect(await page.locator('svg rect').count()).toBe(0);
    await page.getByRole('button',{name:'Create world',exact:true}).click();await page.locator('dialog.decision-dialog').getByRole('button',{name:'Create world',exact:true}).click();
    await expect(page.locator('canvas')).not.toHaveAttribute('data-world-epoch',before);await closePanel(page);await ready(page);
    const saved=await(await page.request.get('/api/saves/download',{timeout:60000})).body();expect(digest(saved)).toBe(digest(expected));
    writeFileSync(join(directory,`creation-${mode}.json`),JSON.stringify({mode,h:1500,v:1090,z:45,elapsedMs:Date.now()-start,saveBytes:saved.length},null,2)+'\n');
  }
});

for(const mode of ['coast-island','water-coast','forest-dense','forest-coast'] as const) test(`minimum parity extent ${mode==='coast-island'?'coast: exact island':mode.startsWith('forest-')?'forest: '+mode:'water: lakes and rivers'} preview, bounded publication and checkpoint restart`,async({page,browser})=>{
 const memory=await memorySampler(browser),errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 const fixture=join(directory,'fixtures',mode);
 try{
  execFileSync('dotnet',[probe,'--capacity','1500','1090','45',mode,fixture],{stdio:'pipe',timeout:60000});
  const expected=digest(readFileSync(join(fixture,'world.json')));await page.goto('/');await ready(page);
  await page.locator('canvas').evaluate(c=>c.dataset.instance='coast-large');
  await openPanel(page,'newWorld');await page.getByLabel('World source',{exact:true}).selectOption('generated');
  await page.getByLabel('Terrain style',{exact:true}).selectOption('mountains');await page.getByLabel('Generation seed',{exact:true}).fill('-2147483648');
  await page.getByLabel('Relief (0–100)',{exact:true}).fill('100');await page.getByLabel('Feature scale (cells)',{exact:true}).fill('4');await page.getByLabel('Smoothness (0–100)',{exact:true}).fill('0');
  if(mode==='forest-dense')await page.getByLabel('Terrain style',{exact:true}).selectOption('hills');
  await page.getByLabel('Coast layout',{exact:true}).selectOption(mode==='coast-island'?'island':mode==='forest-dense'?'inland':'sides');
  if(mode.startsWith('forest-')){
   await page.getByLabel('Forest mask coverage (0–90%)',{exact:true}).fill('90');await page.getByLabel('Forest clustering (0–100)',{exact:true}).fill('80');
   await page.getByLabel('Forest ground appearance',{exact:true}).selectOption('mountain');await page.getByLabel('Connected flat starting area (cells)',{exact:true}).fill(mode==='forest-dense'?'64':'16');
  }
  if(mode==='water-coast'||mode==='forest-coast'){
   await page.getByLabel('Target sea coverage (%)',{exact:true}).fill('30');await page.getByLabel('Lake count (0–8)',{exact:true}).fill('2');
   await page.getByLabel('River direction',{exact:true}).selectOption('northSouth');await page.getByLabel('Channel width (cells)',{exact:true}).fill('3');await page.getByLabel('Meander (0–100)',{exact:true}).fill('80');
  }
  await page.getByLabel('World name',{exact:true}).fill('Capacity relief');
  await page.getByLabel('Stored width (H)',{exact:true}).fill('1500');await page.getByLabel('Stored length (V)',{exact:true}).fill('1090');await page.getByLabel('Levels (Z)',{exact:true}).fill('45');
  const before=await epoch(page),start=Date.now(),waiting=page.waitForResponse(r=>r.url().includes('/worlds/generated/preview'));
  await page.getByRole('button',{name:'Preview world',exact:true}).click();const response=await waiting;expect(response.ok(),await response.text()).toBe(true);
  const candidate=await response.json();if(mode.startsWith('forest-'))expect(candidate.geography.forests).toMatchObject({reservedCells:mode==='forest-dense'?64:16});expect(candidate.geography.metrics).toMatchObject(mode==='forest-dense'?{seaCells:0,lakeComponents:0,riverCount:0}:mode==='coast-island'?{seaSides:15,landComponents:1,seaComponents:1,seaCells:654000,landCells:981000}:{seaSides:5,seaComponents:1,lakeComponents:1,riverCount:1});
  const previewMs=Date.now()-start;expect(previewMs).toBeLessThan(30000);expect(await epoch(page)).toBe(before);
  await expect(page.getByTestId('geography-metrics')).toContainText(mode==='coast-island'?'40.0%':'Surface water:');
  expect(candidate.overview).toMatchObject({width:125,height:91,step:12});
  let wetCells=0;for(let bits of Buffer.from(candidate.geography.seaMask,'base64'))while(bits){wetCells+=bits&1;bits>>=1;}
  expect(wetCells).toBe(candidate.geography.metrics.seaCells+candidate.geography.metrics.lakeCells);
  expect(candidate.overview.metrics).toMatchObject({cells:1635000,wetCells,forestCells:candidate.geography.forests.forestCells});
  expect(Buffer.from(candidate.overview.samples,'base64').length).toBeLessThanOrEqual(128*128*4);
  const overviewSwitchMs=await page.evaluate(async()=>{
   const image=document.querySelector<HTMLImageElement>('[data-testid="world-overview"] img')!,previous=image.src;
   const button=[...document.querySelectorAll('button')].find(b=>b.textContent==='Isometric')!,started=performance.now();button.click();
   while(image.src===previous)await new Promise(requestAnimationFrame);
   return performance.now()-started;
  });
  expect(overviewSwitchMs).toBeLessThan(100);
  await expect(page.getByTestId('world-overview')).toHaveAttribute('data-mode','isometric');
  await page.getByTestId('world-overview').scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,`${mode}-overview.png`),fullPage:true});
  await page.getByRole('button',{name:'Create world',exact:true}).click();await page.locator('dialog.decision-dialog').getByRole('button',{name:'Create world',exact:true}).click();
  await expect(page.locator('canvas')).not.toHaveAttribute('data-world-epoch',before);await closePanel(page);await ready(page);
  const readyMs=Date.now()-start;expect(readyMs).toBeLessThan(60000);await expect(page.locator('canvas')).toHaveAttribute('data-instance','coast-large');
  expect(digest(await(await page.request.get('/api/saves/download',{timeout:60000})).body())).toBe(expected);
  const canvas=page.locator('canvas');await expect(canvas).toHaveAttribute('data-refinement','ready');
  const panBox=(await canvas.boundingBox())!,paints:number[]=[];
  for(let i=0;i<20;i++){
   await page.mouse.move(panBox.x+panBox.width/2,panBox.y+panBox.height/2);await page.mouse.down({button:'middle'});
   await page.mouse.move(panBox.x+panBox.width/2+32,panBox.y+panBox.height/2+8);await page.mouse.up({button:'middle'});
   await page.evaluate(()=>new Promise(requestAnimationFrame));paints.push(Number(await canvas.getAttribute('data-paint-ms')));
  }
  paints.sort((a,b)=>a-b);
  expect(paints[Math.ceil(paints.length*.95)-1]).toBeLessThan(33.4);
  const box=(await canvas.boundingBox())!;await page.mouse.click(box.x+box.width/2,box.y+box.height/2);await expect(page.getByTestId('selected-object')).toContainText('Terrain');await closePanel(page);
  await page.screenshot({path:join(directory,`${mode}-large.png`),fullPage:true});
  await page.reload();await ready(page);expect(digest(await(await page.request.get('/api/saves/download',{timeout:60000})).body())).toBe(expected);
  await stop(server);await startServer();await page.request.post('/api/access',{data:{secret:''}});await page.reload();await ready(page);
  expect(digest(await(await page.request.get('/api/saves/download',{timeout:60000})).body())).toBe(expected);
  const rss=memory.values();expect(rss.sampledServerPeakRss).toBeLessThan(6*1024**3);expect(rss.sampledBrowserPeakRss).toBeLessThan(6*1024**3);
  writeFileSync(join(directory,`${mode}-large.json`),JSON.stringify({previewMs,readyMs,overviewSwitchMs,overview:candidate.overview.metrics,paintP95:paints[Math.ceil(paints.length*.95)-1],...rss,...await resident(page),metrics:candidate.geography.metrics,forests:candidate.geography.forests,saveBytes:candidate.saveBytes,snapshotBytes:candidate.snapshotBytes},null,2)+'\n');
  expect(errors).toEqual([]);
 }finally{await memory.stop();}
});
