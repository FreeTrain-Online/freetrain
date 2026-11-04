import {writeFileSync} from 'node:fs';
import {test,expect} from './fixtures.ts';
import {canonicalState,ready} from './browser-helpers.ts';
import {wideMovingFleetSave} from '../helpers/moving-fleet-save.ts';

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
