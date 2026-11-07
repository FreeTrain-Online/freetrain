import type { TrainMotion } from '../generated/contracts.ts';
import type { ReadState } from './session-client.ts';

/** Resource and route checks supplement the structural wire schema. Malformed
 * presentation data never supplies a shortcut across unrecorded track. */
export function validTrainMotion(motion:ReadState['trainMotion']|TrainMotion,minute:string):boolean {
  if(!motion)return true;
  if(motion.steps.length>128||motion.reset&&motion.steps.length>0)return false;
  let previous=-1n,previousMinute=-1n,cars=0;
  for(const step of motion.steps) {
    const sequence=BigInt(step.sequence),time=BigInt(step.minute);
    if(sequence<=previous||sequence>BigInt(motion.sequence)||time<previousMinute||time>BigInt(minute)||step.cars.length>100)return false;
    if(previous>=0n&&sequence!==previous+1n)return false;
    previous=sequence;previousMinute=time;cars+=step.cars.length;
    if(cars>8192||new Set(step.cars.map(c=>c.carId)).size!==step.cars.length)return false;
    for(const car of step.cars) {
      const a=car.from,b=car.to,dx=b.position.x-a.position.x,dy=b.position.y-a.position.y;
      if(a.reversed!==b.reversed||Math.abs(dx)>1||Math.abs(dy)>1||dx===0&&dy===0)return false;
      const directions=[[0,-1],[1,-1],[1,0],[1,1],[0,1],[-1,1],[-1,0],[-1,-1]];
      if(directions[b.heading][0]!==dx||directions[b.heading][1]!==dy||a.outgoing!==b.heading)return false;
      for(const p of [a,b]) {
        if(![0,1,7].includes((p.outgoing-p.heading+8)%8))return false;
        if(p.slope&&(![0,2,4,6].includes(p.slope.climb)||!Number.isInteger(p.slope.level)||p.slope.level<0||p.slope.level>3||
          p.outgoing!==p.heading||![p.slope.climb,(p.slope.climb+4)%8].includes(p.heading)))return false;
      }
      const slope=a.slope,dz=slope?.level===1&&a.heading===slope.climb?1:
        slope?.level===2&&a.heading===(slope.climb+4)%8?-1:0;
      if(b.position.z-a.position.z!==dz)return false;
      if(a.slope&&b.slope) {
        const direction=a.heading===a.slope.climb?1:-1;
        if(a.slope.climb===b.slope.climb) {
          // Adjacent ramps can join directly at 3 -> 0 (or 0 -> 3).
          if(b.slope.level!==(a.slope.level+direction+4)%4)return false;
        }else if(a.slope.level!==(direction===1?3:0)||b.slope.level!==a.slope.level)return false;
      }else if(a.slope||b.slope) {
        const p=a.slope?a:b,s=p.slope!,up=b.heading===s.climb;
        if(s.level!==(a.slope?(up?3:0):(up?0:3)))return false;
      }
    }
  }
  return motion.steps.length===0||previous===BigInt(motion.sequence);
}
