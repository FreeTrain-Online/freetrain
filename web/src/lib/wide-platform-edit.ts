import type {Heading,Position,WidePlatformQuery,ThinPlatformPreview,WidePlatformPreview} from '../generated/contracts.ts';
import type {ReadState} from './session-client.ts';
import {railwayRevision} from './railway-edit.ts';
import {offset} from './map-geometry.ts';
export type PlatformPreview=ThinPlatformPreview|WidePlatformPreview;
export function widePlatformAt(read:ReadState,p:Position){
 return read.mixed?.stationGraph?.platforms.find(b=>b.kind==='wide'&&[
  ...Array.from({length:b.length},(_,i)=>{const d=offset(b.direction);return {x:b.location.x+d.x*i,y:b.location.y+d.y*i,z:b.location.z};}),
  ...b.yards.map(y=>y.location)].some(q=>q.x===p.x&&q.y===p.y&&q.z===p.z));
}
export function widePlatformQuery(read:ReadState,position:Position,direction:Heading,length:number,secondLane:boolean,remove=false):WidePlatformQuery|undefined {
 const revision=railwayRevision(read);if(revision===undefined||![0,2,4,6].includes(direction)||!Number.isInteger(length)||length<1||length>100)return;
 const b=remove?widePlatformAt(read,position):undefined;if(remove&&!b)return;
 return {expectedWorldEpoch:read.worldEpoch,expectedGeometryRevision:revision,expectedMinute:read.tick,expectedContentVersion:read.contentVersion as WidePlatformQuery['expectedContentVersion'],action:remove?'remove':'place',position,direction:direction as WidePlatformQuery['direction'],length,secondLane,platformId:b?.id??'0',expectedPlatformRevision:b?.revision??'0'};
}
export function widePlatformGhost(query:WidePlatformQuery,positions:Position[]){
 const d=offset(query.direction);
 return positions.map(p=>({position:p,recipeId:(p.x-query.position.x)*-d.y+(p.y-query.position.y)*d.x===0?`system:fat-${query.direction/2%2}`:`system:pattern/${query.direction%4===0?10:4}`}));
}
