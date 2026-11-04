import {trainTurnScene} from './helpers/train-turn-scene.ts';
import assert from 'node:assert/strict';
import {TrainMotionPlayer,movingCarLayer,trainMotionBufferMs,compareTrainLayers} from '../src/lib/train-motion.ts';
import {validTrainMotion} from '../src/lib/train-motion-contract.ts';
import {trainCarLayer} from '../src/lib/train-renderer.ts';
import {trainSceneFixture} from './helpers/train-scene.ts';
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
Deno.test('committed coalesced moves replay each car at native duration and hold at underrun without changing state',()=>{
 const player=new TrainMotionPlayer(),a=initial(),b=advance(a,3),bytes=JSON.stringify(b);
 player.update(a,0);player.update(b,10);
 assert.equal(player.sample(10).length,3);assert.equal(player.sample(410+trainMotionBufferMs)[0].progress,.5);
 assert.deepEqual(player.sample(810+trainMotionBufferMs)[0].from,b.trainMotion!.steps[1].cars[0].from);
 assert.deepEqual(player.sample(1610+trainMotionBufferMs)[0].from,b.trainMotion!.steps[2].cars[0].from);
 assert.deepEqual(player.sample(2410+trainMotionBufferMs),[]);assert.deepEqual(player.sample(10000),[]);assert.equal(JSON.stringify(b),bytes);
});
Deno.test('pause, step, speed changes, reconnect/epoch, missing history, reversal and disabled motion discard travel',()=>{
 for(const kind of ['pause','rate','epoch','gap','reversal','withdrawal','reset','disabled']) {
  const player=new TrainMotionPlayer(),a=initial(),b=advance(a);player.update(a,0);player.update(b,10);assert.equal(player.sample(20).length,3);
  const c=structuredClone(b);
  if(kind==='pause')c.paused=true;
  if(kind==='rate')c.playbackRate=.25;
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
