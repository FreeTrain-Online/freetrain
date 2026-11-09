import type {PlatformProperties,PlatformPropertiesQuery,StationPlatform} from '../generated/contracts.ts';
import type {ReadState} from './session-client.ts';
import {railwayRevision} from './railway-edit.ts';
export const validPlatformName=(s:string)=>!!s.trim()&&s.trim().length<=128&&!/[\u0000-\u001f\u007f-\u009f]/u.test(s);
export function platformPropertiesQuery(read:ReadState,p:Pick<StationPlatform,'id'|'revision'>):PlatformPropertiesQuery {
 return {expectedWorldEpoch:read.worldEpoch,expectedGeometryRevision:railwayRevision(read)!,expectedContentVersion:read.contentVersion as PlatformPropertiesQuery['expectedContentVersion'],platformId:p.id,expectedPlatformRevision:p.revision};
}
export function resolvedPlatformStation(read:ReadState|undefined,id:string|undefined){
 const graph=read?.mixed?.stationGraph,seen=new Set<string>();
 while(id){if(seen.has(id))return;seen.add(id);const station=graph?.buildings.find(b=>b.id===id);if(station)return station;id=graph?.platforms.find(p=>p.id===id)?.hostId??undefined;}
}
export function currentPlatformProperties(read:ReadState|undefined,properties:PlatformProperties):boolean {
 const q=properties.query,g=read?.mixed?.stationGraph;
 if(!read||read.worldEpoch!==q.expectedWorldEpoch||railwayRevision(read)!==q.expectedGeometryRevision||read.contentVersion!==q.expectedContentVersion||g?.platforms.find(p=>p.id===q.platformId)?.revision!==q.expectedPlatformRevision)return false;
 return properties.candidates.every(c=>{
  const member=g?.buildings.find(b=>b.id===c.id)??g?.platforms.find(p=>p.id===c.id),station=resolvedPlatformStation(read,c.id);
  return member?.revision===c.revision&&station?.id===c.stationId&&station?.revision===c.stationRevision&&c.isStation===!!g?.buildings.find(b=>b.id===c.id)&&JSON.stringify(member.location)===JSON.stringify(c.location)&&JSON.stringify(station.location)===JSON.stringify(c.stationLocation);
 });
}
