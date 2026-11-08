import type {Heading,Position,ThinPlatformQuery} from '../generated/contracts.ts';
import type {ReadState} from './session-client.ts';
import {railwayRevision} from './railway-edit.ts';
import {offset} from './map-geometry.ts';
export function thinPlatformAt(read:ReadState,p:Position){
 return read.mixed?.stationGraph?.platforms.find(b=>b.kind==='thin'&&b.yards.some(y=>y.location.x===p.x&&y.location.y===p.y&&y.location.z===p.z));
}
export function thinPlatformQuery(read:ReadState,position:Position,direction:Heading,length:number,roof:boolean,remove=false):ThinPlatformQuery|undefined {
 const revision=railwayRevision(read);if(revision===undefined||![0,2,4,6].includes(direction)||!Number.isInteger(length)||length<1||length>100)return;
 const b=remove?thinPlatformAt(read,position):undefined;if(remove&&!b)return;
 return {expectedWorldEpoch:read.worldEpoch,expectedGeometryRevision:revision,expectedMinute:read.tick,expectedContentVersion:read.contentVersion as ThinPlatformQuery['expectedContentVersion'],action:remove?'remove':'place',position,direction:direction as ThinPlatformQuery['direction'],length,roof,platformId:b?.id??'0',expectedPlatformRevision:b?.revision??'0'};
}
export function thinPlatformGhost(query:ThinPlatformQuery,positions:Position[]){
 const d=offset(query.direction);
 return positions.map(p=>{const i=(p.x-query.position.x)*d.x+(p.y-query.position.y)*d.y;
  const roof=query.roof&&i>=Math.trunc(query.length/4)&&i<query.length-Math.trunc(query.length/4);
  return {position:p,recipeId:`system:thin-${query.direction+(roof?1:0)}`};});
}
