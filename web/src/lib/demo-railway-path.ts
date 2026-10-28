import { move, directionTo } from './map-geometry.ts';
import rails from '../../../src/FreeTrain.Online.Content/reference-spatial.json' with { type:'json' };
import type { Position } from '../generated/contracts.ts';

import { demoGround, demoSpace, demoDimensions } from './demo-terrain.ts';

// A fixed coastal alignment with long cardinal/diagonal runs, pulled slightly
// inland from the raster shoreline. It circles the complete island and northern
// peninsula with 18 gentle bends instead of following every beach-cell wiggle.
// This presentation path never creates gameplay rail entities.
const space=demoSpace;
const start:Position={x:30,y:29,z:demoDimensions.waterLevel};
const positions:Position[]=[];
let p=start;
const runs:readonly (readonly [number,number])[]=[
  [2,12],[3,4],[2,8],[1,4],[2,8],[3,12],[4,8],[5,8],[6,4],
  [5,12],[6,12],[7,4],[6,4],[7,8],[0,4],[7,4],[0,12],[1,8],
];
for(const [heading,length] of runs)for(let i=0;i<length;i++) {
  if(!space.contains(p)||demoGround(p.x,p.y).ground<demoDimensions.waterLevel)throw new Error('Decorative coastal alignment left dry land');
  positions.push(p);p=move(p,heading);
}
if(p.x!==start.x||p.y!==start.y)throw new Error('Decorative coastal alignment did not close');
export const demoLoop=Object.freeze(positions.map((p,i)=>{
  const previous=positions[(i+positions.length-1)%positions.length],next=positions[(i+1)%positions.length];
  const ports=[directionTo(p,previous),directionTo(p,next)];
  const pattern=rails.normalRailPorts.findIndex(pair=>pair.every(d=>ports.includes(d)));
  if(pattern<0)throw new Error('Unsupported decorative rail bend');
  return Object.freeze({position:Object.freeze(p),pattern});
}));
export const demoLoopPeriod=demoLoop.length*650;
/** Continuous centre-line through each tile, with rounded 45° turns. Time is
 * local presentation time; every follower samples its own position and tangent. */
export function demoCar(timeMs:number,car:number) {
  const distance=((timeMs/650-car)%demoLoop.length+demoLoop.length)%demoLoop.length;
  const i=Math.floor(distance),t=distance-i;
  const a=demoLoop[(i+demoLoop.length-1)%demoLoop.length].position,b=demoLoop[i].position,c=demoLoop[(i+1)%demoLoop.length].position;
  const start={x:(a.x+b.x)/2,y:(a.y+b.y)/2},end={x:(b.x+c.x)/2,y:(b.y+c.y)/2};
  const x=(1-t)**2*start.x+2*(1-t)*t*b.x+t*t*end.x,y=(1-t)**2*start.y+2*(1-t)*t*b.y+t*t*end.y;
  const dx=(1-t)*(b.x-start.x)+t*(end.x-b.x),dy=(1-t)*(b.y-start.y)+t*(end.y-b.y);
  const angle=(Math.round(Math.atan2(dx,-dy)*8/Math.PI)+16)%16;
  return {position:{x,y,z:b.z},angle};
}
export function demoCorridor(radius:number):Set<number> {
  const cells=new Set<number>();
  for(const {position} of demoLoop)for(let y=-radius;y<=radius;y++)for(let x=-radius;x<=radius;x++) {
    const p={x:position.x+x,y:position.y+y,z:position.z};
    if(!space.contains(p))continue;
    const s=space.storage(p);cells.add(s.v*48+s.h);
  }
  return cells;
}
