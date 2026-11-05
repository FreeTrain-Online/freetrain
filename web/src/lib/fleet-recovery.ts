import type {ReadState} from './session-client.ts';
import {carOutgoing} from './train-renderer.ts';
import profile from '../generated/railway-profile.json' with {type:'json'};
import {move,offset,WorldSpace} from './map-geometry.ts';
import {fleetRailPorts} from './fleet-state.ts';

type Train=NonNullable<NonNullable<ReadState['mixed']>['fleet']>['trains'][number];
export const fleetStateLabel={unplaced:'stockUnplaced',moving:'moving',stoppingatstation:'fleetWaiting',
 stoppingatsignal:'fleetSignalWaiting',emergencystopping:'fleetEmergency'} as const;

// Describe the current saved snapshot without advancing time or controllers.
// After TRAIN-D001 the current head faces the retried direction.
export function fleetRecoveryReason(read:ReadState,train:Train):'fleetMissingTrack'|'fleetOccupiedTrack'|'fleetRetry'|'fleetOccupiedReturn'|'fleetMissingReturn'|undefined {
 if(train.state!=='emergencystopping')return undefined;
 const state=train.cars[0].state,p=state.location;
 if(!p)return 'fleetRetry';
 const at=(p:{x:number;y:number;z:number})=>read.mixed!.voxels.find(v=>v.position.x===p.x&&v.position.y===p.y&&v.position.z===p.z);
 if(state.kind==='outside'){
  if(state.countdown!==0)return 'fleetRetry';
  const {x,y}=offset(state.heading!);
  // ExAV calcReturnPoint: connectivity chooses the first candidate, before
  // occupancy is checked. This is feedback only; never searches for a free path.
  const candidates=state.heading!%2===0?[[y,-x],[-y,x],[y-2*x,-x-2*y],[-y-2*x,x-2*y],[-x,-y]]:
   [[(-x+y)/2,(-x-y)/2],[(-x-y)/2,(x-y)/2],[(-3*x+y)/2,(-x-3*y)/2],[(-3*x-y)/2,(x-3*y)/2],[-x,-y]];
  for(const [dx,dy] of candidates){
   const target=at({x:p.x+Math.trunc(dx),y:p.y+Math.trunc(dy),z:p.z}),rail=target?.traffic?.rail;
   if(rail&&fleetRailPorts(rail.pattern).includes(state.heading!))return target?.traffic?.car?'fleetOccupiedReturn':'fleetRetry';
  }
  return 'fleetMissingReturn';
 }
 const rail=at(p)?.traffic?.rail;
 if(!rail)return 'fleetMissingTrack';
 const handler=profile.bindings.find(b=>b.reference.packId===rail.definition.packId&&b.reference.contributionId===rail.definition.contributionId&&b.reference.recipeId===rail.definition.recipeId)?.handler;
 let go:number;
 try{go=carOutgoing(read,train,0,rail,handler??'');}catch{return 'fleetRetry';}
 const next=move(p,go);
 if(handler==='slope'){
  const level=(rail.pattern-28)%4,climb=Math.floor((rail.pattern-28)%16/4)*2;
  if(state.heading===(level===2?(climb+4)%8:climb))next.z+=level===1?1:level===2?-1:0;
 }
 const s=new WorldSpace(read.map.width,read.map.height,read.map.levels).storage(next);
 if(s.h<0||s.h>=read.map.width||s.v<2*next.z||s.v>=read.map.height-2*(read.map.levels-next.z))return 'fleetRetry';
 const target=at(next),r=target?.traffic?.rail;
 const edges=r?fleetRailPorts(r.pattern):[];
 if(!edges.includes((go+4)%8))return 'fleetMissingTrack';
 return target?.traffic?.car?'fleetOccupiedTrack':'fleetRetry';
}
