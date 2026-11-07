import {trainTurnScene} from './helpers/train-turn-scene.ts';
import {trainSlopeScene} from './helpers/train-slope-scene.ts';
import assert from 'node:assert/strict';
import {TrainMotionPlayer,movingCarLayer,trainMotionBufferMs,compareTrainLayers} from '../src/lib/train-motion.ts';
import {validTrainMotion} from '../src/lib/train-motion-contract.ts';
import {trainCarLayer} from '../src/lib/train-renderer.ts';
import {trainSceneFixture} from './helpers/train-scene.ts';
import {validSnapshot} from '../src/lib/world-identity.ts';
import type {Snapshot,TrainMotionPose,TrainMotionStep} from '../src/generated/contracts.ts';
import type {RenderCatalog} from '../src/lib/content-recipes.ts';
const catalog=JSON.parse(await Deno.readTextFile(new URL('../public/content/bundled-v2/catalog.json',import.meta.url))) as RenderCatalog;
const source=JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/fixtures/snapshot-fleet.json',import.meta.url))) as Snapshot;
const stock=catalog.renderRecipes.find(r=>r.handler==='train-composition')!;
function initial() {const s=trainSceneFixture(source,catalog,stock);s.paused=false;s.playbackRate=.125;s.trainMotion={sequence:'0',reset:false,steps:[]};return s;}
function advance(s:Snapshot,count=1):Snapshot {
 const next=structuredClone(s),train=next.mixed!.fleet!.trains[0],steps:TrainMotionStep[]=[];
 for(let n=1;n<=count;n++){
  const cars=train.cars.map(c=>{
   const from={position:{...c.state.location!},heading:0,outgoing:0,reversed:false};
   c.state.location!.y--;return {carId:c.id,from,to:{...from,position:{...c.state.location!}}};
  });
  steps.push({sequence:String(BigInt(s.trainMotion!.sequence)+BigInt(n)),minute:String(BigInt(s.tick)+BigInt(n*2)),trainId:train.id,duration:2,cars});
 }
 next.tick=steps.at(-1)!.minute;next.trainMotion={sequence:steps.at(-1)!.sequence,reset:false,steps};return next;
}
Deno.test('committed coalesced moves replay each car at native duration through Normal and hold at underrun without changing state',()=>{
 for(const rate of [.125,.25,.5,1]) {
 const player=new TrainMotionPlayer(),a=initial();a.playbackRate=rate;
 const b=advance(a,3),bytes=JSON.stringify(b),duration=100/rate;
 player.update(a,0);player.update(b,10);
 assert.equal(player.sample(10).length,3);assert.equal(player.sample(10+duration/2+trainMotionBufferMs)[0].progress,.5);
 assert.deepEqual(player.sample(10+duration+trainMotionBufferMs)[0].from,b.trainMotion!.steps[1].cars[0].from);
 assert.deepEqual(player.sample(10+2*duration+trainMotionBufferMs)[0].from,b.trainMotion!.steps[2].cars[0].from);
 assert.deepEqual(player.sample(10+3*duration+trainMotionBufferMs),[]);assert.deepEqual(player.sample(10000),[]);assert.equal(JSON.stringify(b),bytes);
 }
});
Deno.test('Fast uses committed poses, clears queued animation, and resumes smoothing after returning to Normal',()=>{
 const player=new TrainMotionPlayer(),a=initial();a.playbackRate=1;
 player.update(a,0);const b=advance(a);player.update(b,10);assert.equal(player.sample(20).length,3);
 const fast={...b,playbackRate:2};player.update(fast,30);assert.deepEqual(player.sample(40),[]);
 const c=advance(fast,3);player.update(c,50);assert.deepEqual(player.sample(60),[]);
 const normal={...c,playbackRate:1};player.update(normal,70);assert.deepEqual(player.sample(80),[]);
 player.update(advance(normal),90);assert.equal(player.sample(100).length,3);
});
Deno.test('Run fills confirmed history and calibrates a cold server before moving, without repeated startup underruns',()=>{
 for(const rate of [1,.5,.125]) {
  const player=new TrainMotionPlayer();let state=initial();state.paused=true;state.playbackRate=rate;
  player.update(state,0);state={...state,paused:false};player.update(state,10);
  // First callbacks include cold-server stalls, then settle above nominal cost.
  const interval=Math.max(240,100/rate),arrivals=[interval+60,interval+860];
  while(arrivals.at(-1)!<20_000)arrivals.push(arrivals.at(-1)!+interval);
  let next=0,lastY:number|undefined,started=false,firstMovement=0;
  for(let now=20;now<19_000;now+=10) {
   while(next<arrivals.length&&arrivals[next]<=now){state=advance(state);player.update(state,arrivals[next++]);}
   const before=JSON.stringify(state),cars=player.sample(now);
   assert.equal(JSON.stringify(state),before,'presentation changed authority');
   if(!cars.length){assert(!started,`startup underrun at ${rate}/${now}`);continue;}
   const c=cars[0],y=c.from.position.y+(c.to.position.y-c.from.position.y)*c.progress;
   if(lastY!==undefined&&y!==lastY){if(!started)firstMovement=now;started=true;}
   if(started&&lastY!==undefined)assert(y<lastY,`repeated hold at ${rate}/${now}`);
   lastY=y;
  }
  assert(started);assert(firstMovement<=Math.max(1520,arrivals[0]+10)); // Run at 10 ms, plus one sampled frame.
 }
});
Deno.test('one late coalesced publication does not prematurely finish a fresh refill',()=>{
 const player=new TrainMotionPlayer(),a=initial();a.playbackRate=1;a.paused=true;player.update(a,0);
 const running={...a,paused:false};player.update(running,10);
 const b=advance(running,2);player.update(b,550);
 assert(player.buffering);assert.equal(player.sample(650)[0].progress,0);
 assert(player.buffering);assert.equal(player.sample(900)[0].progress,0);
 const c=advance(b);player.update(c,1250);assert.equal(player.sample(1250)[0].progress,0);
 const d=advance(c,8);player.update(d,1260); // Remaining queued packets arrive in a burst.
 assert(player.buffering);assert.equal(player.sample(1290)[0].progress,0);
 assert.equal(player.sample(1300)[0].progress,0);assert(!player.buffering);
 const progress=player.sample(1350)[0].progress;
 assert(progress>.3&&progress<.6,'calibration used stale, queued packets as server pacing');
});
Deno.test('running speed changes preserve fractional poses and confirmed routes while refilling at the new pace',()=>{
 for(const [fromRate,toRate] of [[1,.25],[.125,1],[.5,.125],[.25,.5]])for(const pacing of [1,1.8]) {
  const player=new TrainMotionPlayer(),a=initial();a.playbackRate=fromRate;player.update(a,0);
  let state=advance(a,3);player.update(state,10);
  const changed=210+50/fromRate,pose=player.sample(changed);
  assert.equal(pose[0].progress,.5);
  state={...state,playbackRate:toRate};player.update(state,changed);
  assert.deepEqual(player.sample(changed),pose,'speed change snapped a fractional car');
  const interval=100/toRate*pacing,until=changed+20_000;
  let arrival=changed+interval,lastY=pose[0].from.position.y-.5,started=false;
  for(let now=changed+10;now<until;now+=10) {
   while(arrival<=now){state=advance(state);player.update(state,arrival);arrival+=interval;}
   const bytes=JSON.stringify(state),cars=player.sample(now);assert.equal(JSON.stringify(state),bytes);
   assert.equal(cars.length,3,`${fromRate}/${toRate}/${pacing}: lost buffered cars`);
   const c=cars[0],y=c.from.position.y+(c.to.position.y-c.from.position.y)*c.progress;
   if(y<lastY)started=true;
   if(started)assert(y<lastY,`${fromRate}/${toRate}/${pacing}: repeated hold at ${now-changed}`);
   else assert.equal(y,lastY,'refilling jumped to authority');
   assert(lastY-y<.3,'speed transition skipped a recorded step');lastY=y;
  }
  assert(started);
 }
});
Deno.test('rapid speed changes retain the held pose, and Pause cancels the pending refill',()=>{
 const player=new TrainMotionPlayer(),a=initial();a.playbackRate=1;player.update(a,0);
 let state=advance(a,3);player.update(state,10);const pose=player.sample(260);
 for(const [now,rate] of [[260,.25],[360,.5],[460,.125]]) {
  state={...state,playbackRate:rate};player.update(state,now);assert.deepEqual(player.sample(now+1),pose);
 }
 player.update({...state,paused:true},500);assert.deepEqual(player.sample(501),[]);
 assert.deepEqual(player.sample(2500),[]);
});
Deno.test('foreground resume discards background travel and refills fresh confirmed steps before continuous playback',()=>{
 for(const rate of [1,.5,.125]) {
  const player=new TrainMotionPlayer(),a=initial();a.playbackRate=rate;player.update(a,0);
  let state=advance(a,3);player.update(state,10);player.sample(250);
  // Publications continued while the browser stopped presenting frames.
  state=advance(state,5);player.update(state,900);
  const current={...state.mixed!.fleet!.trains[0].cars[0].state.location!};
  player.resume(1000);assert(player.buffering);assert.deepEqual(player.sample(1000),[]);
  const interval=Math.max(260,100/rate),arrivals=[1000+interval,1700+interval];
  while(arrivals.at(-1)!<16_000)arrivals.push(arrivals.at(-1)!+interval);
  let next=0,lastY:number|undefined,started=false;
  for(let now=1010;now<15_000;now+=10) {
   while(next<arrivals.length&&arrivals[next]<=now){state=advance(state);player.update(state,arrivals[next++]);}
   const bytes=JSON.stringify(state),cars=player.sample(now);assert.equal(JSON.stringify(state),bytes);
   if(!cars.length){assert(!started,'resume ran out of fresh history');continue;}
   const c=cars[0],y=c.from.position.y+(c.to.position.y-c.from.position.y)*c.progress;
   if(lastY===undefined)assert.deepEqual(c.from.position,current,'replayed stale background movement');
   if(lastY!==undefined&&y<lastY)started=true;
   if(started)assert(y<lastY!,`repeated foreground hold at ${rate}/${now}`);
   lastY=y;
  }
  assert(started);assert(!player.buffering);
 }
});
Deno.test('foreground resume respects Pause, Fast, reduced motion and canceled refills',()=>{
 for(const kind of ['paused','fast','disabled','pauseAfterResume']) {
  const player=new TrainMotionPlayer(),a=initial();a.playbackRate=kind==='fast'?2:1;a.paused=kind==='paused';
  player.update(a,0);let state=advance(a);player.update(state,100);
  if(kind==='disabled')player.setEnabled(false);
  player.resume(1000);
  if(kind==='pauseAfterResume'){assert(player.buffering);state={...state,paused:true};player.update(state,1100);}
  assert(!player.buffering);assert.deepEqual(player.sample(2500),[]);
 }
});
Deno.test('pause, step, reconnect/epoch, missing history, reversal and disabled motion discard travel',()=>{
 for(const starting of [false,true])for(const kind of ['pause','epoch','gap','reversal','withdrawal','reset','disabled']) {
  const player=new TrainMotionPlayer(),a=initial(),b=advance(a);
  if(starting)player.update({...a,paused:true},-10);
  player.update(a,0);player.update(b,10);assert.equal(player.sample(20).length,3);
  const c=structuredClone(b);
  if(kind==='pause')c.paused=true;
  if(kind==='epoch')c.worldEpoch=String(BigInt(c.worldEpoch)+1n);
  if(kind==='gap'){c.trainMotion!.sequence='9';c.trainMotion!.steps=[];}
  if(kind==='reversal')c.mixed!.fleet!.trains[0].isReversed=true;
  if(kind==='withdrawal')c.mixed!.fleet!.trains=[];
  if(kind==='reset')player.reset();
  if(kind==='disabled')player.setEnabled(false);
  player.update(c,30);assert.deepEqual(player.sample(40),[],kind);
 }
 const a=initial();a.paused=true;const player=new TrainMotionPlayer();player.update(a,0);const b=advance(a);player.update(b,1);assert.deepEqual(player.sample(2),[]);
});
Deno.test('straight and curved fractional geometry joins every authored endpoint in both orientations and all planar directions',()=>{
 for(const recipe of catalog.renderRecipes.filter(r=>['train-car','reverse-train-car'].includes(r.handler)))for(const reversed of [false,true])for(let heading=0;heading<8;heading++)for(const turn of [-1,0,1]) {
  const outgoing=(heading+turn+8)%8,directions=[[0,-1],[1,-1],[1,0],[1,1],[0,1],[-1,1],[-1,0],[-1,-1]],d=directions[outgoing];
  const from:TrainMotionPose={position:{x:30,y:20,z:2},heading,outgoing,reversed},to:TrainMotionPose={position:{x:30+d[0],y:20+d[1],z:2},heading:outgoing,outgoing,reversed};
  const car={trainId:'1',carId:'2',from,to,progress:0};
  for(const [progress,pose] of [[0,from],[1,to]] as const){
   const l=movingCarLayer(recipe,{...car,progress},15),expected=trainCarLayer(recipe,'2',[pose.position.x-15,pose.position.y,2],pose.heading,reversed,pose.outgoing);
   const origin=(l:typeof expected)=>({x:16*(l.position[0]+l.position[1])-l.frame.anchor![0],y:8*(l.position[1]-l.position[0])-32-l.frame.anchor![1]});
   const a=origin(l),b=origin(expected);assert(Math.abs(a.x-b.x)<1e-8,recipe.id);assert(Math.abs(a.y-b.y)<1e-8,recipe.id);assert.deepEqual(l.frame.rectangle,expected.frame.rectangle);
  }
  const middle=movingCarLayer(recipe,{...car,progress:.5},15);assert(middle.position.every(Number.isFinite));assert.equal(middle.entityId,'2');
 }
});
Deno.test('motion contract rejects duplicate identities, route shortcuts, future/unordered minutes and oversized histories',()=>{
 const a=initial(),b=advance(a,3);assert(validTrainMotion(b.trainMotion,b.tick));
 for(const kind of ['duplicate','shortcut','future','sequence','reversal','direction','count']) {
  const m=structuredClone(b.trainMotion!);
  if(kind==='duplicate')m.steps[0].cars.push(m.steps[0].cars[0]);
  if(kind==='shortcut')m.steps[0].cars[0].to.position.y-=3;
  if(kind==='future')m.steps[0].minute=String(BigInt(b.tick)+1n);
  if(kind==='sequence')m.steps[1].sequence='8';
  if(kind==='reversal')m.steps[0].cars[0].to.reversed=true;
  if(kind==='direction')m.steps[0].cars[0].to.heading=4;
  if(kind==='count')m.steps=Array.from({length:129},()=>m.steps[0]);
  assert(!validTrainMotion(m,b.tick),kind);
 }
});

Deno.test('ramp translation joins every authored pitch/height endpoint continuously in both directions and orientations',()=>{
 for(const recipe of catalog.renderRecipes.filter(r=>['train-car','reverse-train-car'].includes(r.handler)))
 for(const climb of [0,2,4,6] as const)for(const downhill of [false,true])for(const reversed of [false,true]) {
  const route=trainSlopeScene(source,catalog,stock,climb,downhill,reversed);
  const origin=(l:ReturnType<typeof trainCarLayer>)=>({x:16*(l.position[0]+l.position[1])-l.frame.anchor![0],y:8*(l.position[1]-l.position[0])-16*l.position[2]-l.frame.anchor![1]});
  let previous:ReturnType<typeof origin>|undefined;
  for(let step=0;step<6;step++) {
   const from=route.pose(step),to=route.pose(step+1),car={trainId:'1',carId:'2',from,to,progress:0};
   for(const [progress,pose] of [[0,from],[1,to]] as const) {
    const actual=movingCarLayer(recipe,{...car,progress},route.space.centerX),p=pose.position;
    const expected=trainCarLayer(recipe,'2',[p.x-route.space.centerX,p.y,p.z],pose.heading,reversed,pose.outgoing,pose.slope);
    const a=origin(actual),b=origin(expected);
    assert(Math.abs(a.x-b.x)<1e-8,recipe.id);assert(Math.abs(a.y-b.y)<1e-8,recipe.id);
    assert.deepEqual(actual.frame.rectangle,expected.frame.rectangle);
   }
   for(let n=0;n<=100;n++) {
    const progress=n/100,layer=movingCarLayer(recipe,{...car,progress},route.space.centerX),p=origin(layer);
    assert(layer.position.every(Number.isFinite));
    // Sprite pitches can change at entry/exit; their common centre translates
    // uniformly across the discrete storage-Z transition and every join.
    const expectedZ=(pose:TrainMotionPose)=>pose.position.z+(pose.slope?[0,.25,-.5,-.25][pose.slope.level]:0);
    assert(Math.abs(layer.position[2]-(expectedZ(from)+(expectedZ(to)-expectedZ(from))*progress))<1e-8);
    if(previous)assert(Math.hypot(p.x-previous.x,p.y-previous.y)<.3,'ramp anchor jumped');
    previous=p;
   }
  }
 }
});

Deno.test('coalesced slope history stays buffered across elevation changes and rejects malformed ramp routes',()=>{
 for(const climb of [0,2,4,6] as const)for(const downhill of [false,true])for(const reversed of [false,true])for(const underground of [false,true]) {
  const route=trainSlopeScene(source,catalog,stock,climb,downhill,reversed,underground),a=route.snapshot(0),b=route.snapshot(7);
  assert(validSnapshot(a));assert(validSnapshot(b));
  a.paused=b.paused=false;a.playbackRate=b.playbackRate=.125;a.tick='0';b.tick='14';a.trainMotion={sequence:'0',reset:false,steps:[]};
  b.trainMotion={sequence:'7',reset:false,steps:Array.from({length:7},(_,n)=>({sequence:String(n+1),minute:String((n+1)*2),trainId:b.mixed!.fleet!.trains[0].id,duration:2,
   cars:b.mixed!.fleet!.trains[0].cars.map((c,i)=>({carId:c.id,from:route.pose(n-i),to:route.pose(n+1-i)}))}))};
  assert(validTrainMotion(b.trainMotion,b.tick));
  const player=new TrainMotionPlayer();player.update(a,0);player.update(b,10);
  for(let n=0;n<7;n++)assert.equal(player.sample(210+n*800+400).length,3,'lost buffered ramp route');
  for(const kind of ['height','level','climb','entry','turn']) {
   const motion=structuredClone(b.trainMotion),car=motion.steps[2].cars[0];
   if(kind==='height')car.to.position.z+=1;
   if(kind==='level')car.to.slope!.level=car.from.slope!.level;
   if(kind==='climb')car.to.slope!.climb=((climb+2)%8) as typeof climb;
   if(kind==='entry')motion.steps[0].cars[0].to.slope!.level=2;
   if(kind==='turn')car.to.outgoing=(car.to.heading+1)%8;
   assert(!validTrainMotion(motion,b.tick),kind);
  }
 }
});

Deno.test('adjoining ramps retain continuous uphill, downhill, crest and valley routes',()=>{
 for(const heading of [0,2,4,6] as const)for(const downhill of [false,true])for(const crest of [false,true]) {
  const climb=(downhill?(heading+4)%8:heading) as 0|2|4|6,level=downhill?0:3;
  const from:TrainMotionPose={position:{x:30,y:20,z:3},heading,outgoing:heading,reversed:false,slope:{climb,level}};
  const [dx,dy]=[[0,-1],[1,0],[0,1],[-1,0]][heading/2];
  const to:TrainMotionPose={...from,position:{x:30+dx,y:20+dy,z:3},slope:{climb:crest?((climb+4)%8) as typeof climb:climb,level:crest?level:downhill?3:0}};
  const car={trainId:'1',carId:'2',from,to,progress:.5};
  assert(validTrainMotion({sequence:'1',reset:false,steps:[{sequence:'1',minute:'1',trainId:'1',duration:2,cars:[car]}]},'1'));
  assert(movingCarLayer(catalog.renderRecipes.find(r=>r.handler==='train-car')!,car,15).position.every(Number.isFinite));
 }
});

Deno.test('a sequenced discontinuity suppresses subsequent travel in the same publication and resumes from the next confirmed state',()=>{
 const player=new TrainMotionPlayer(),a=initial(),b=advance(a);player.update(a,0);player.update(b,10);assert.equal(player.sample(20).length,3);
 const c=advance(b,2);c.trainMotion!.steps[0].cars=[];
 assert(validTrainMotion(c.trainMotion,c.tick));player.update(c,30);assert.deepEqual(player.sample(40),[]);
 const d=advance(c);player.update(d,50);assert.equal(player.sample(60).length,3);
});

Deno.test('all slow rates join confirmed steps continuously through publication jitter and slightly slower server pacing',()=>{
 for(const rate of [.125,.25,.5])for(const pacing of [1,1.08]) {
  const player=new TrainMotionPlayer();let state=initial();state.playbackRate=rate;player.update(state,0);
  const interval=100/rate*pacing,jitter=[40,0,70,10,100,20,0,60];
  const arrivals=Array.from({length:60},(_,i)=>(i+1)*interval+jitter[i%jitter.length]);
  let next=0,previous:{x:number;y:number}|undefined,held=0,frames=0,maxDelta=0;
  for(let now=0;now<arrivals.at(-1)!-interval;now+=10) {
   while(next<arrivals.length&&arrivals[next]<=now){state=advance(state);player.update(state,arrivals[next]);next++;}
   const cars=player.sample(now);
   if(now<arrivals[0]+trainMotionBufferMs+100||next===0)continue;
   assert.equal(cars.length,3,`underrun at ${rate}/${pacing}/${now}`);
   const c=cars[0],p={x:c.from.position.x+(c.to.position.x-c.from.position.x)*c.progress,y:c.from.position.y+(c.to.position.y-c.from.position.y)*c.progress};
   if(previous){const delta=Math.hypot(p.x-previous.x,p.y-previous.y);if(delta<1e-8)held++;maxDelta=Math.max(maxDelta,delta);}
   previous=p;frames++;
  }
  assert.equal(held,0,`${rate}/${pacing} paused at a voxel`);assert(frames>100);assert(maxDelta<.1,'a join jumped');
 }
});
Deno.test('native blocked intervals remain holds, and history loss never extrapolates beyond a confirmed endpoint',()=>{
 const player=new TrainMotionPlayer(),a=initial(),b=advance(a);a.playbackRate=b.playbackRate=.5;player.update(a,0);player.update(b,200);
 const c=advance(b);c.tick=String(BigInt(c.tick)+2n);c.trainMotion!.steps[0].minute=c.tick;player.update(c,600);
 // First travel is 400..600, followed by two blocked minutes (200ms).
 assert.equal(player.sample(700)[0].progress,0);assert.equal(player.sample(900)[0].progress,.5);
 assert.deepEqual(player.sample(10000),[]);
});
Deno.test('startup calibration retains real blocked minutes between confirmed moves',()=>{
 const player=new TrainMotionPlayer(),a=initial();a.playbackRate=.5;a.paused=true;player.update(a,0);
 const running={...a,paused:false};player.update(running,10);
 const b=advance(running);player.update(b,210);
 const c=advance(b);c.tick=String(BigInt(c.tick)+2n);c.trainMotion!.steps[0].minute=c.tick;
 player.update(c,610);assert.equal(player.sample(610)[0].progress,0);
 assert.equal(player.sample(650)[0].progress,0); // Finish after the delivery burst settles.
 assert.equal(player.sample(750)[0].progress,.5);
 assert.equal(player.sample(950)[0].progress,0); // Two genuinely blocked minutes.
 assert.equal(player.sample(1150)[0].progress,.5);
 assert.deepEqual(player.sample(1250),[]);
});
Deno.test('changing speed during a genuine blocked interval preserves its remaining game minutes',()=>{
 for(const rapid of [false,true]) {
 const player=new TrainMotionPlayer(),a=initial();a.playbackRate=.5;player.update(a,0);
 const b=advance(a);player.update(b,200);
 const c=advance(b);c.tick=String(BigInt(c.tick)+2n);c.trainMotion!.steps[0].minute=c.tick;player.update(c,600);
 assert.equal(player.sample(700)[0].progress,0); // One blocked minute remains.
 let slower={...c,playbackRate:.25};player.update(slower,700);
 if(rapid){slower={...slower,playbackRate:.125};player.update(slower,800);}
 const calibrated=rapid?2400:1500,minute=rapid?400:200;
 player.update({...slower,tick:String(BigInt(slower.tick)+4n)},calibrated);
 assert.equal(player.sample(calibrated)[0].progress,0);
 assert.equal(player.sample(calibrated+minute/2)[0].progress,0);
 assert.equal(player.sample(calibrated+minute*1.5)[0].progress,.25);
 assert.deepEqual(player.sample(calibrated+minute*3),[]);
 }
});

Deno.test('adjacent cars retain continuous depth at both ends of a tight turn without rounded-voxel order flips',()=>{
 const recipes=new Map(catalog.renderRecipes.map(r=>[r.id,r]));
 for(const [rotation,turn] of [[3,1],[5,-1],[0,-1],[2,1]])for(const reversed of [false,true]) {
  const route=trainTurnScene(source,catalog,stock,rotation,turn,reversed),train=route.snapshot(3).mixed!.fleet!.trains[0];
  const last=[0,0],flips=[0,0];
  for(let step=3;step<11;step++)for(let frame=0;frame<100;frame++) {
   const layers=train.cars.map((car,i)=>movingCarLayer(recipes.get(car.definition.recipeId)!,{trainId:train.id,carId:car.id,from:route.pose(step-i),to:route.pose(step-i+1),progress:frame/100},route.space.centerX));
   for(let i=0;i<2;i++){
    const a=layers[i],b=layers[i+1],depth=(a.position[1]-a.position[0])-(b.position[1]-b.position[0]),horizontal=(a.position[0]+a.position[1])-(b.position[0]+b.position[1]);
    const expected=Math.abs(depth)>1e-8?Math.sign(depth):Math.sign(horizontal),actual=Math.sign(compareTrainLayers(a,b));
    assert.equal(actual,expected,`${rotation}/${turn}/${reversed}/${step}/${frame}/${i}`);
    if(last[i]&&last[i]!==actual)flips[i]++;last[i]=actual;
   }
  }
  for(const count of flips)assert(count<=1,`spurious car depth flips: ${rotation}/${turn}/${reversed}: ${flips}`);
 }
});
Deno.test('shared-depth car ties tolerate floating point noise and use stable identity, independent of input order',()=>{
 const recipe=catalog.renderRecipes.find(r=>r.handler==='train-car')!,pose={position:{x:30,y:20,z:2},heading:3,outgoing:3,reversed:false};
 const a=movingCarLayer(recipe,{trainId:'1',carId:'18',from:pose,to:{...pose,position:{x:31,y:21,z:2}},progress:.5},15);
 const b={...a,entityId:'19',position:[a.position[0]+1e-12,a.position[1]-1e-12,a.position[2]] as [number,number,number]};
 assert(compareTrainLayers(a,b)<0);assert(compareTrainLayers(b,a)>0);assert.equal(compareTrainLayers(a,a),0);
});
