import {readFileSync,writeFileSync} from 'node:fs';
import {test,expect} from './fixtures.ts';
import {canonicalState,ready} from './browser-helpers.ts';
import {wideMovingFleetSave} from '../helpers/moving-fleet-save.ts';

test('cold Run and resume fill once, then move continuously through startup delivery stalls',async({page},testInfo)=>{
 test.setTimeout(90_000);
 await page.setViewportSize({width:1920,height:1080});
 const before=await canonicalState(page);
 const save=process.env.FREETRAIN_STARTUP_REGRESSION_WORLD
  ?JSON.parse(readFileSync(process.env.FREETRAIN_STARTUP_REGRESSION_WORLD,'utf8')):wideMovingFleetSave(2);
 save.paused=true;
 const uploaded=await page.request.post('/api/saves/upload?expectedWorldEpoch='+before.worldEpoch,{data:JSON.stringify(save)});
 expect(uploaded.ok(),await uploaded.text()).toBe(true);
 const publications:{time:number;tick:string;steps:number;moves:number[]}[]=[];
 let stalled=0;
 await page.routeWebSocket('**/api/session?*',route=>{
  const server=route.connectToServer();let delivery=Promise.resolve();
  server.onMessage(message=>{
   const value=JSON.parse(String(message));
   // Preserve packet order while delaying the first two movement publications.
   // This reproduces cold callbacks/delivery without depending on machine speed.
   const delay=value.type==='delta'&&!value.paused&&value.trainMotion?.steps.length&&stalled<2
    ?[300,700][stalled++]:0;
   delivery=delivery.then(async()=>{if(delay)await new Promise(resolve=>setTimeout(resolve,delay));route.send(message);});
  });
 });
 page.on('websocket',socket=>socket.on('framereceived',frame=>{
  const message=JSON.parse(String(frame.payload));
  if(message.type==='delta')publications.push({time:Date.now(),tick:message.tick,steps:message.trainMotion?.steps.length??0,moves:message.mixed?.fleet?.trains.map((t:any)=>t.moveCount)??[]});
 }));
 await page.goto('/');await ready(page);
 const canvas=page.locator('.world-canvas');
 await expect(canvas).toHaveAttribute('data-presentation','ready',{timeout:20_000});
 await canvas.focus();for(let n=0;n<4;n++)await page.keyboard.press('-');
 await expect(canvas).toHaveAttribute('data-zoom','0.500');
 await expect(canvas).toHaveAttribute('data-presentation','ready',{timeout:20_000});
 const cdp=await page.context().newCDPSession(page),report=[];
 await cdp.send('Profiler.enable');
 for(const attempt of ['cold','resume']) {
  await cdp.send('Profiler.start');
  const sample=await page.evaluate(()=>new Promise(resolve=>{
   const canvas=document.querySelector('.world-canvas') as HTMLCanvasElement;
   const intervals:number[]=[],longTasks:{start:number;duration:number}[]=[],poses:{time:number;pose:string}[]=[];
   const start=performance.now();let last=start,lastPose='',baseline='',started=false,lastMoved=start,maxHold=0,missing=0,firstMovement=0;
   const observer=new PerformanceObserver(list=>{for(const task of list.getEntries())longTasks.push({start:task.startTime-start,duration:task.duration});});
   observer.observe({type:'longtask'});
   const frame=(now:number)=>{
    intervals.push(now-last);last=now;
    const pose=canvas.dataset.trainMotionPose??'';
    if(pose&&pose!=='undefined') {
     if(!baseline)baseline=pose;
     if(pose!==baseline&&!started){started=true;firstMovement=now-start;lastMoved=now;}
     if(started){if(pose!==lastPose)lastMoved=now;else maxHold=Math.max(maxHold,now-lastMoved);}
    }else if(started)missing++;
    if(pose!==lastPose){poses.push({time:now-start,pose});lastPose=pose;}
    if(now-start<8000)requestAnimationFrame(frame);
    else {observer.disconnect();resolve({frames:intervals.length,maxFrame:Math.max(...intervals),firstMovement,maxHold,missing,longTasks,poses});}
   };
   requestAnimationFrame(frame);
   (document.querySelector('.transport button') as HTMLButtonElement).click();
  })) as {frames:number;maxFrame:number;firstMovement:number;maxHold:number;missing:number;longTasks:unknown[];poses:unknown[]};
  const {profile}=await cdp.send('Profiler.stop');
  writeFileSync(`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/startup-${attempt}.cpuprofile`,JSON.stringify(profile));
  report.push({attempt,...sample,publications:publications.splice(0)});
  writeFileSync(`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/train-startup.json`,JSON.stringify(report,null,2));
  await page.getByRole('button',{name:'Pause',exact:false}).first().click();
  await expect(page.getByTestId('clock-state')).toHaveText('Paused');
 }
 await testInfo.attach('train-startup.json',{body:JSON.stringify(report,null,2),contentType:'application/json'});
 for(const sample of report) {
  expect(sample.maxFrame).toBeLessThan(200);
  // A custom world can include unsupported geometry or offscreen cars.
  if(!process.env.FREETRAIN_STARTUP_REGRESSION_WORLD) {
   expect(sample.firstMovement).toBeGreaterThan(500);expect(sample.firstMovement).toBeLessThan(1800);
   expect(sample.maxHold).toBeLessThan(100);expect(sample.missing).toBe(0);
  }
 }
});

test('running speed changes keep visible poses and settle after one refill in both directions',async({page},testInfo)=>{
 test.setTimeout(90_000);
 await page.setViewportSize({width:1920,height:1080});
 const before=await canonicalState(page),save=wideMovingFleetSave(2);
 const uploaded=await page.request.post('/api/saves/upload?expectedWorldEpoch='+before.worldEpoch,{data:JSON.stringify(save)});
 expect(uploaded.ok(),await uploaded.text()).toBe(true);
 let stalled=2;
 await page.routeWebSocket('**/api/session?*',route=>{
  const server=route.connectToServer();let delivery=Promise.resolve();
  server.onMessage(message=>{
   const value=JSON.parse(String(message));
   const delay=value.type==='delta'&&!value.paused&&value.trainMotion?.steps.length&&stalled<2?[200,400][stalled++]:0;
   delivery=delivery.then(async()=>{if(delay)await new Promise(resolve=>setTimeout(resolve,delay));route.send(message);});
  });
 });
 await page.goto('/');await ready(page);
 const canvas=page.locator('.world-canvas'),speed=page.getByLabel('Game speed',{exact:true});
 await expect(canvas).toHaveAttribute('data-presentation','ready',{timeout:20_000});
 await canvas.focus();for(let n=0;n<4;n++)await page.keyboard.press('-');
 await expect(canvas).toHaveAttribute('data-presentation','ready',{timeout:20_000});
 await page.getByRole('button',{name:'Run',exact:false}).first().click();
 await expect(canvas).toHaveAttribute('data-train-motion','smooth');
 await expect(canvas).toHaveAttribute('data-train-motion-phase','playing');
 const report=[];
 for(const rate of ['0.25','1','0.125','0.5']) {
  await page.evaluate(()=>{
   (window as any).speedMeasurement=new Promise(resolve=>{
    const c=document.querySelector('.world-canvas') as HTMLCanvasElement,start=performance.now();
    let previous=c.dataset.trainMotionPose??'',last=start,lastMoved=start,maxHold=0,maxJump=0,maxFrame=0,missing=0,frames=0;
    let buffering=false,settled=false,refills=0;
    const frame=(now:number)=>{
     const pose=c.dataset.trainMotionPose??'';
     if(c.dataset.trainMotionPhase==='buffering') {
      if(!buffering)refills++;
      buffering=true;settled=false;
     }else if(c.dataset.trainMotionPhase==='playing'&&buffering){buffering=false;settled=true;lastMoved=now;}
     maxFrame=Math.max(maxFrame,now-last);last=now;
     if(!pose||pose==='undefined')missing++;
     else if(previous&&previous!=='undefined') {
      const a=JSON.parse(previous),b=JSON.parse(pose);
      maxJump=Math.max(maxJump,Math.hypot(a.x-b.x,a.y-b.y));
     }
     if(!settled||pose!==previous)lastMoved=now;
     else maxHold=Math.max(maxHold,now-lastMoved);
     previous=pose;frames++;
     if(now-start<4500)requestAnimationFrame(frame);
     else resolve({frames,maxFrame,maxJump,maxHold,missing,refills,settled});
    };requestAnimationFrame(frame);
   });
  });
  stalled=0;await speed.selectOption(rate);await expect(speed).toBeEnabled();
  const sample=await page.evaluate(()=>(window as any).speedMeasurement);
  report.push({rate,...sample});
  expect(sample.refills).toBe(1);expect(sample.settled).toBe(true);
  expect(sample.missing).toBe(0);expect(sample.maxJump).toBeLessThan(.7);expect(sample.maxHold).toBeLessThan(100);
  expect(sample.frames).toBeGreaterThan(100);expect(sample.maxFrame).toBeLessThan(200);
 }
 await page.getByRole('button',{name:'Pause',exact:false}).first().click();
 await expect(canvas).toHaveAttribute('data-train-motion','discrete');
 await testInfo.attach('speed-transition.json',{body:JSON.stringify(report,null,2),contentType:'application/json'});
});

test('foreground return refills once after focus, visibility and interrupted animation frames',async({page},testInfo)=>{
 test.setTimeout(90_000);
 await page.setViewportSize({width:1920,height:1080});
 const before=await canonicalState(page),save=wideMovingFleetSave(2);
 const uploaded=await page.request.post('/api/saves/upload?expectedWorldEpoch='+before.worldEpoch,{data:JSON.stringify(save)});
 expect(uploaded.ok(),await uploaded.text()).toBe(true);
 await page.goto('/');await ready(page);
 const canvas=page.locator('.world-canvas');
 await expect(canvas).toHaveAttribute('data-presentation','ready',{timeout:20_000});
 await canvas.focus();for(let n=0;n<4;n++)await page.keyboard.press('-');
 await expect(canvas).toHaveAttribute('data-presentation','ready',{timeout:20_000});
 await page.getByRole('button',{name:'Run',exact:false}).first().click();
 await expect(canvas).toHaveAttribute('data-train-motion-phase','playing');
 const report=[];
 for(const reason of ['focus','visibility','frameGap']) {
  await page.evaluate(()=>{
   (window as any).foregroundMeasurement=new Promise(resolve=>{
    const c=document.querySelector('.world-canvas') as HTMLCanvasElement,start=performance.now();
    let buffering=false,settled=false,refills=0,frames=0,missing=0,maxHold=0,maxJump=0,lastMoved=start,previous='';
    const frame=(now:number)=>{
     const pose=c.dataset.trainMotionPose??'';
     if(c.dataset.trainMotionPhase==='buffering') {
      if(!buffering)refills++;
      buffering=true;settled=false;lastMoved=now;
     }else if(c.dataset.trainMotionPhase==='playing'&&buffering){buffering=false;settled=true;lastMoved=now;previous=pose;}
     if(settled) {
      frames++;
      if(!pose||pose==='undefined')missing++;
      else if(previous&&previous!=='undefined') {
       const a=JSON.parse(previous),b=JSON.parse(pose);
       maxJump=Math.max(maxJump,Math.hypot(a.x-b.x,a.y-b.y));
      }
      if(pose!==previous)lastMoved=now;else maxHold=Math.max(maxHold,now-lastMoved);
     }
     previous=pose;
     if(now-start<4500)requestAnimationFrame(frame);
     else resolve({refills,settled,frames,missing,maxHold,maxJump});
    };requestAnimationFrame(frame);
   });
  });
  if(reason==='frameGap')await page.evaluate(()=>{
   // Withhold animation callbacks without emitting focus or visibility events.
   const until=performance.now()+750;while(performance.now()<until){ /* Withhold frames. */ }
  });else await page.evaluate(async reason=>{
   window.dispatchEvent(new Event('blur'));
   if(reason==='visibility') {
    Object.defineProperty(document,'hidden',{configurable:true,value:true});document.dispatchEvent(new Event('visibilitychange'));
   }
   await new Promise(resolve=>setTimeout(resolve,1000));
   if(reason==='visibility') {
    delete (document as any).hidden;document.dispatchEvent(new Event('visibilitychange'));
   }
   window.dispatchEvent(new Event('focus'));
   if(reason==='visibility') {
    // Native focus/visibility notifications may arrive in adjacent frames.
    await new Promise(resolve=>setTimeout(resolve,50));
    document.dispatchEvent(new Event('visibilitychange'));window.dispatchEvent(new Event('focus'));
   }
  },reason);
  const sample=await page.evaluate(()=>(window as any).foregroundMeasurement);report.push({reason,...sample});
  expect(sample.refills,reason).toBe(1);expect(sample.settled,reason).toBe(true);expect(sample.frames,reason).toBeGreaterThan(60);
  expect(sample.missing).toBe(0);expect(sample.maxHold).toBeLessThan(100);expect(sample.maxJump).toBeLessThan(.7);
  await expect(page.getByTestId('clock-state')).toHaveText('Running');
 }
 await page.getByRole('button',{name:'Pause',exact:false}).first().click();
 await expect(canvas).toHaveAttribute('data-train-motion','discrete');
 await testInfo.attach('foreground-return.json',{body:JSON.stringify(report,null,2),contentType:'application/json'});
});

test('many moving trains remain responsive with smooth Normal and discrete Fast @performance',async({page},testInfo)=>{
 test.setTimeout(120_000);
 const trains=Number(process.env.FREETRAIN_TRAIN_RUNTIME_COUNT??4),cpuRate=Number(process.env.FREETRAIN_TRAIN_RUNTIME_CPU_RATE??4);
 await page.setViewportSize({width:1920,height:1080});
 const before=await canonicalState(page),save=wideMovingFleetSave(trains);
 const upload=await page.request.post('/api/saves/upload?expectedWorldEpoch='+before.worldEpoch,{data:JSON.stringify(save)});
 expect(upload.ok(),await upload.text()).toBe(true);
 await page.goto('/');await ready(page);
 const canvas=page.locator('.world-canvas'),speed=page.getByLabel('Game speed',{exact:true});
 await expect(canvas).toHaveAttribute('data-refinement','ready',{timeout:15_000});
 await canvas.focus();for(let n=0;n<4;n++)await page.keyboard.press('-');
 await expect(canvas).toHaveAttribute('data-zoom','0.500');
 await expect(canvas).toHaveAttribute('data-refinement','ready',{timeout:15_000});
 const cdp=await page.context().newCDPSession(page),report=[];
 // Repeatable CPU pressure exposes work that exceeds a frame on slower devices.
 await cdp.send('Emulation.setCPUThrottlingRate',{rate:cpuRate});
 try {
  for(const rate of ['1','2']) {
   await speed.selectOption(rate);await expect(speed).toBeEnabled();
   await page.getByRole('button',{name:'Run',exact:false}).first().click();
   const cars=rate==='1'?trains*3:0;
   await expect(canvas).toHaveAttribute('data-moving-cars',String(cars));
   await expect(canvas).toHaveAttribute('data-train-motion',cars?'smooth':'discrete');
   await cdp.send('Profiler.enable');await cdp.send('Profiler.start');
   const sample=await canvas.evaluate((el,cars)=>new Promise<{frames:number;p95Frame:number;p95Paint:number;maxOperations:number;maxHold:number;missing:number;poses:number}>(resolve=>{
    const intervals:number[]=[],paints:number[]=[],operations:number[]=[],poses=new Set<string>();
    const start=performance.now();let last=start,lastMoved=start,lastPose='',maxHold=0,missing=0;
    const frame=(now:number)=>{
     const c=el as HTMLCanvasElement,pose=c.dataset.trainMotionPose??'';
     if(now-start>1000) {
      intervals.push(now-last);paints.push(Number(c.dataset.paintMs));operations.push(Number(c.dataset.motionOperations));
      if(Number(c.dataset.movingCars)!==cars||Boolean(pose)!==Boolean(cars))missing++;
      if(cars){
       if(pose!==lastPose){lastMoved=now;lastPose=pose;}else maxHold=Math.max(maxHold,now-lastMoved);
       poses.add(pose);
      }
     }else {lastMoved=now;lastPose=pose;}
     last=now;
     if(now-start<7000)requestAnimationFrame(frame);
     else {const p95=(a:number[])=>a.sort((x,y)=>x-y)[Math.floor(a.length*.95)];resolve({frames:intervals.length,p95Frame:p95(intervals),p95Paint:p95(paints),maxOperations:Math.max(...operations),maxHold,missing,poses:poses.size});}
    };requestAnimationFrame(frame);
   }),cars);
   const {profile}=await cdp.send('Profiler.stop');
   const nodes=new Map(profile.nodes.map(node=>[node.id,node.callFrame.functionName])),counts=new Map<string,number>();
   for(const id of profile.samples??[]){const name=nodes.get(id)??'?';counts.set(name,(counts.get(name)??0)+1);}
   const hot=[...counts].sort((a,b)=>b[1]-a[1]).slice(0,25);
   report.push({trains,cpuRate,rate,sample,hot});
   writeFileSync(`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/train-runtime-${rate}.cpuprofile`,JSON.stringify(profile));
   writeFileSync(`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/train-runtime.json`,JSON.stringify(report,null,2));
   expect(sample.frames).toBeGreaterThan(cpuRate===1?300:150);
   expect(sample.p95Frame).toBeLessThan(cpuRate===1?34:70);
   expect(sample.p95Paint).toBeLessThan(cpuRate===1?17:40);
   expect(sample.missing).toBe(0);expect(sample.maxHold).toBeLessThan(75);
   await page.getByRole('button',{name:'Pause',exact:false}).first().click();
  }
 }finally {await cdp.send('Emulation.setCPUThrottlingRate',{rate:1});}
 await testInfo.attach('train-runtime.json',{body:JSON.stringify(report,null,2),contentType:'application/json'});
});
