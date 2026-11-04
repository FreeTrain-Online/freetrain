import {validTrainMotion} from './train-motion-contract.ts';
import type { Position, TrainMotionPose } from '../generated/contracts.ts';
import type { ReadState } from './session-client.ts';
import { instantiate, type Layer, type Recipe } from './content-recipes.ts';
import { trainCarLayer } from './train-renderer.ts';

type Pose=Readonly<TrainMotionPose>;
type Step=NonNullable<ReadState['trainMotion']>['steps'][number];
type Travel={step:Step;start:number;end:number};
// Covers the 100ms publication slot plus ordinary delivery/scheduling jitter.
export const trainMotionBufferMs=200;
export type MovingCar={trainId:string;carId:string;from:Pose;to:Pose;progress:number};
const same=(a:Position,b:Position)=>a.x===b.x&&a.y===b.y&&a.z===b.z;
const samePose=(a:Pose,b:Pose)=>same(a.position,b.position)&&a.heading===b.heading&&a.outgoing===b.outgoing&&a.reversed===b.reversed;

/** Plays only complete confirmed steps on a buffered, contiguous timeline.
 * No extrapolation, controller calls, occupancy writes, or simulation clock. */
export class TrainMotionPlayer {
  private read?:ReadState;
  private sequence=0n;
  private queues=new Map<string,Travel[]>();
  private enabled=true;
  private clockSamples:{minute:bigint;time:number}[]=[];
  private wallMinute=50;
  reset() {
    this.queues.clear();this.sequence=BigInt(this.read?.trainMotion?.sequence??'0');
    this.clockSamples=[];this.wallMinute=50/(this.read?.playbackRate??1);
  }
  private observeClock(read:ReadState,now:number) {
    const minute=BigInt(read.tick),samples=this.clockSamples;
    if(samples.at(-1)?.minute===minute)return;
    samples.push({minute,time:now});
    while(samples.length>2&&(samples.length>32||now-samples[1].time>2000))samples.shift();
    const elapsed=now-samples[0].time,minutes=minute-samples[0].minute;
    if(elapsed<500||minutes<2n)return;
    // Average several clock publications so a late packet does not change the
    // speed of a single voxel. Integer minutes stay bounded before conversion.
    const nominal=50/(read.playbackRate??1),observed=elapsed/Number(minutes);
    this.wallMinute+=.25*(Math.max(nominal*.9,Math.min(nominal*4,observed))-this.wallMinute);
  }
  setEnabled(enabled:boolean) {if(enabled!==this.enabled){this.enabled=enabled;this.reset();}}
  update(read:ReadState,now:number) {
    if(read===this.read)return;
    const before=this.read;this.read=read;
    const motion=read.trainMotion,sequence=BigInt(motion?.sequence??'0');
    if(!before||before.worldEpoch!==read.worldEpoch||before.playbackRate!==read.playbackRate||before.paused||read.paused||
      !this.enabled||(read.playbackRate??1)>0.5||!motion||!validTrainMotion(motion,read.tick)||motion.reset||sequence<this.sequence) {this.reset();return;}
    this.observeClock(read,now);
    const steps=motion.steps.filter(s=>BigInt(s.sequence)>this.sequence);
    if(sequence>this.sequence&&(steps.length===0||BigInt(steps[0].sequence)!==this.sequence+1n)) {this.reset();return;}
    let expected=this.sequence;
    const discontinuities=new Set<string>();
    const bufferedTails=new Set([...this.queues.values()].map(queue=>queue.at(-1)));
    const trains=new Map(read.mixed?.fleet?.trains.map(t=>[t.id,t]));
    for(const step of steps) {
      if(BigInt(step.sequence)!==++expected||BigInt(step.minute)>BigInt(read.tick)){this.reset();return;}
      if(!step.cars.length){this.queues.delete(step.trainId);discontinuities.add(step.trainId);continue;}
      if(discontinuities.has(step.trainId))continue;
      const train=trains.get(step.trainId);if(!train)continue;
      const queue=this.queues.get(step.trainId)??[];
      const tail=queue.at(-1)?.step;
      const prior=before.mixed?.fleet?.trains.find(t=>t.id===step.trainId);
      const continuous=tail?step.cars.every(c=>tail.cars.some(p=>p.carId===c.carId&&samePose(p.to,c.from))):
        step.cars.every(c=>prior?.cars.some(p=>p.id===c.carId&&p.state.kind==='inside'&&p.state.location&&same(p.state.location,c.from.position)&&p.state.heading===c.from.heading)&&prior.isReversed===c.from.reversed);
      if(!continuous||queue.length>=32||step.cars.length!==train.cars.length){this.queues.delete(step.trainId);continue;}
      const previous=queue.at(-1);
      const wait=previous?Math.max(0,Number(BigInt(step.minute)-BigInt(previous.step.minute))-previous.step.duration)*this.wallMinute:0;
      const start=previous?previous.end+wait:now+trainMotionBufferMs;
      const native=step.duration*this.wallMinute;
      // Correct the buffer gradually through future travel, without moving the
      // current car or shifting a join. One publication may contain many steps.
      const correction=previous&&!wait&&bufferedTails.has(previous)&&queue.length===1
        ?Math.max(-native*.1,Math.min(native*.25,(trainMotionBufferMs-(start-now))*.25)):0;
      const duration=native+correction;
      queue.push({step,start,end:start+duration});this.queues.set(step.trainId,queue);
    }
    this.sequence=sequence;
    // A reversal, withdrawal, unsupported step or placement can follow the last
    // recorded move in the same publication. Such a train must use current state.
    for(const [id,queue] of this.queues) {
      const last=queue.at(-1)!.step,train=trains.get(id);
      if(!train||!last.cars.every(c=>train.cars.some(p=>p.id===c.carId&&p.state.kind==='inside'&&p.state.location&&same(p.state.location,c.to.position)&&p.state.heading===c.to.heading)&&train.isReversed===c.to.reversed))this.queues.delete(id);
    }
  }
  sample(now:number):MovingCar[] {
    const result:MovingCar[]=[];
    for(const [id,queue] of this.queues) {
      while(queue.length&&now>=queue[0].end)queue.shift();
      if(!queue.length){this.queues.delete(id);continue;}
      const {step,start,end}=queue[0],progress=Math.max(0,Math.min(1,(now-start)/(end-start)));
      for(const c of step.cars)result.push({trainId:id,carId:c.carId,from:c.from,to:c.to,progress});
    }
    return result;
  }
}

/** Fractional cars share a continuous isometric depth order. Rounding to a
 * voxel lets adjacent bodies exchange depth repeatedly at a curve boundary. */
export function compareTrainLayers(a:Layer,b:Layer):number {
  const [x,y,z]=a.position,[xx,yy,zz]=b.position;
  for(const difference of [(y-x)-(yy-xx),(x+y)-(xx+yy),z-zz])
    if(Math.abs(difference)>1e-8)return difference;
  // Ignore numerical noise on a shared depth plane; identities settle exact
  // ties independently of formation order or the latest published voxel.
  return a.entityId.length-b.entityId.length||(a.entityId<b.entityId?-1:a.entityId>b.entityId?1:0);
}

function poseAngle(p:Pose) {let diff=(p.outgoing-p.heading+8)%8;if(diff===7)diff=-1;return (p.heading*2+diff+16)%16;}
function visualCenter(recipe:Recipe,p:Pose,centerX:number) {
  const l=trainCarLayer(recipe,'sample',[p.position.x-centerX,p.position.y,p.position.z],p.heading,p.reversed,p.outgoing);
  const base=instantiate(recipe,'sample',[0,0,0],{angle:recipe.fields.asymmetric===true?((poseAngle(p)+(p.reversed?8:0))%16):poseAngle(p)&7})[0];
  const dx=base.frame.anchor![0]-l.frame.anchor![0],dy=base.frame.anchor![1]-l.frame.anchor![1]-(-9);
  return {x:p.position.x-centerX+dx/32-dy/16,y:p.position.y+dx/32+dy/16,z:p.position.z};
}
/** Cubic centre-line joins authored voxel anchors with the incoming/outgoing
 * tangent. Existing sixteen angular frames turn independently for each car.
 * Arc-length sampling keeps native per-voxel duration without a bend speed pulse. */
export function movingCarLayer(recipe:Recipe,car:MovingCar,centerX:number):Layer {
  const a=visualCenter(recipe,car.from,centerX),b=visualCenter(recipe,car.to,centerX);
  const length=Math.hypot(b.x-a.x,b.y-a.y);
  const tangent=(p:Pose)=>{const angle=poseAngle(p)*Math.PI/8;return {x:Math.sin(angle)*length,y:-Math.cos(angle)*length};};
  const u=tangent(car.from),v=tangent(car.to);
  const point=(t:number)=>({x:(2*t**3-3*t*t+1)*a.x+(t**3-2*t*t+t)*u.x+(-2*t**3+3*t*t)*b.x+(t**3-t*t)*v.x,
    y:(2*t**3-3*t*t+1)*a.y+(t**3-2*t*t+t)*u.y+(-2*t**3+3*t*t)*b.y+(t**3-t*t)*v.y});
  const samples=[point(0)],distances=[0];
  for(let i=1;i<=16;i++){samples.push(point(i/16));distances.push(distances[i-1]+Math.hypot(samples[i].x-samples[i-1].x,samples[i].y-samples[i-1].y));}
  const distance=distances[16]*car.progress;let i=1;while(i<16&&distances[i]<distance)i++;
  const t=((i-1)+(distance-distances[i-1])/(distances[i]-distances[i-1]||1))/16,p=point(t);
  const dx=(6*t*t-6*t)*a.x+(3*t*t-4*t+1)*u.x+(-6*t*t+6*t)*b.x+(3*t*t-2*t)*v.x;
  const dy=(6*t*t-6*t)*a.y+(3*t*t-4*t+1)*u.y+(-6*t*t+6*t)*b.y+(3*t*t-2*t)*v.y;
  let angle=(Math.round(Math.atan2(dx,-dy)*8/Math.PI)+16+(car.from.reversed?8:0))%16;
  if(recipe.fields.asymmetric!==true)angle&=7;
  const layer=instantiate(recipe,car.carId,[p.x,p.y,a.z],{angle})[0];
  return {...layer,frame:{...layer.frame,anchor:[layer.frame.anchor![0],layer.frame.anchor![1]+9]}};
}
