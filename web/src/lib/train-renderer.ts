// Behavioral extraction: pinned ExAV 883cf7d/trunk/core/world/rail/
// Train.TrainCar.draw, SingleRailRoad.guide and JunctionRailRoad.guide.
// Original authors; LGPL-2.1-or-later.
import type { Definition, Position, RailComponent } from '../generated/contracts.ts';
import type { ReadState } from './session-client.ts';
import { instantiate, variantKey, type Layer, type Recipe, type Variant } from './content-recipes.ts';
import { move } from './map-geometry.ts';
import { normalPorts } from './world-identity.ts';

export interface TrainTarget { kind:'train'; id:string; carId:string; position:Position; definition:Definition }
/** The recipe already incorporates reverse-tail wrappers and authored colors.
 * Formation reversal changes the requested orientation independently. */
export function trainCarLayer(recipe:Recipe,carId:string,position:[number,number,number],heading:number,
  reversed:boolean,outgoing=heading,slope?:{climb:number;level:number}):Layer {
  let x=0,y=-9,angle=heading*2,selector:number|undefined;
  if(slope) {
    y+=[0,-4,8,4][slope.level];
    const d=reversed?(heading+4)%8:heading,climbing=(heading===slope.climb)!==reversed;
    selector=recipe.fields.asymmetric===true?d+(climbing?0:1):(climbing?d:(d+4)%8)/2;
  }else {
    let diff=(outgoing-heading+8)%8;
    if(diff!==0&&diff!==1&&diff!==7)throw new Error('Invalid car drawing curve');
    if(diff===7)diff=-1;
    if(diff) {
      const dd=(2*outgoing+3*diff+16)%16;
      x=2<dd&&dd<10?3:-3;y+=6<dd&&dd<=14?2:-2;
    }
    angle=(angle+diff+16)%16;
    if(reversed)angle^=8;
    if(recipe.fields.asymmetric!==true)angle&=7;
  }
  if(!['train-car','reverse-train-car'].includes(recipe.handler))throw new Error('Car recipe unavailable: '+recipe.id);
  const layers=instantiate(recipe,carId,position,slope?{slope:selector}:{angle});
  if(layers.length!==1||layers[0].frame.status!=='ready')throw new Error('Car frame unavailable: '+recipe.id);
  const layer=layers[0],anchor=layer.frame.anchor!;
  return {...layer,frame:{...layer.frame,anchor:[anchor[0]-x,anchor[1]-y]}};
}

type Train=NonNullable<NonNullable<ReadState['mixed']>['fleet']>['trains'][number];
/** Followers use their predecessor's committed route. At a head's selectable
 * junction the next exit is not known yet: draw neutral until movement commits.
 * Never evaluate a controller or infer another participant's held input. */
export function carOutgoing(_read:ReadState,train:Train,index:number,rail:RailComponent,handler:string):number {
  const car=train.cars[index],h=car.state.heading!;
  if(handler!=='normal'&&handler!=='junction')return h;
  const edges=rail.pattern<12?normalPorts(`normal${rail.pattern}`)!:[
    [1,5,4],[1,5,6],[2,6,5],[2,6,7],[3,7,6],[3,7,0],[4,0,7],[4,0,1],
    [5,1,0],[5,1,2],[6,2,1],[6,2,3],[7,3,2],[7,3,4],[0,4,3],[0,4,5],
  ][rail.pattern-12];
  const left=(h+7)%8,right=(h+1)%8;
  if(edges.includes(h)) {
    if(handler!=='junction'||!edges.includes(left)&&!edges.includes(right))return h;
    if(index===0)return h;
    const previous=train.cars[index-1].state.location,next=move(car.state.location!,h);
    if(previous&&previous.x===next.x&&previous.y===next.y&&previous.z===next.z)return h;
  }
  return edges.includes(left)?left:right;
}

export function prepareCar(read:ReadState,train:Train,index:number,rail:RailComponent,handler:string,
  recipe:Recipe,variant:Variant,centerX:number):{target:TrainTarget;layer:Layer} {
  const car=train.cars[index],p=car.state.location!;
  const slope=handler==='slope'?{climb:Math.floor((rail.pattern-28)%16/4)*2,level:(rail.pattern-28)%4}:undefined;
  const layer=trainCarLayer(recipe,car.id,[p.x-centerX,p.y,p.z],car.state.heading!,train.isReversed,
    carOutgoing(read,train,index,rail,handler),slope);
  if(!layer.frame.masks?.[variantKey(variant)])throw new Error('Car variant unavailable');
  return {target:{kind:'train',id:train.id,carId:car.id,position:p,definition:car.definition},layer};
}
