import assert from 'node:assert/strict';
import {parsePlatformProperties,parseMessage} from '../src/lib/protocol.ts';
import {currentPlatformProperties,platformPropertiesQuery,resolvedPlatformStation,validPlatformName} from '../src/lib/platform-properties.ts';
import type {Snapshot} from '../src/generated/contracts.ts';
const scene=async()=>parseMessage(await Deno.readTextFile(new URL('../../prototype/protocol/fixtures/snapshot-stations.json',import.meta.url))) as Snapshot;
Deno.test('platform editor retains exact candidate and root revisions across dirty drafts',async()=>{
 const read=await scene(),g=read.mixed!.stationGraph!,p=g.platforms[0],b=g.buildings[0];
 const q=platformPropertiesQuery(read,p),value={query:q,candidates:[{id:b.id,revision:b.revision,location:b.location,isStation:true,stationId:b.id,stationRevision:b.revision,stationLocation:b.location}]};
 assert(currentPlatformProperties(read,parsePlatformProperties(value)));assert.equal(resolvedPlatformStation(read,p.id)?.id,b.id);
 for(const mutate of [(r:Snapshot)=>r.worldEpoch='999',(r:Snapshot)=>r.mixed!.geometryRevision='999',(r:Snapshot)=>r.mixed!.stationGraph!.platforms[0].revision='999',
  (r:Snapshot)=>r.mixed!.stationGraph!.buildings[0].revision='999',(r:Snapshot)=>r.mixed!.stationGraph!.buildings=[]]){
  const changed=structuredClone(read);mutate(changed);assert(!currentPlatformProperties(changed,value));
 }
 assert(currentPlatformProperties(read,value));
});
Deno.test('platform properties response rejects duplicate self inconsistent and malformed candidate identities',async()=>{
 const read=await scene(),p=read.mixed!.stationGraph!.platforms[0],b=read.mixed!.stationGraph!.buildings[0];
 const value={query:platformPropertiesQuery(read,p),candidates:[{id:b.id,revision:b.revision,location:b.location,isStation:true,stationId:b.id,stationRevision:b.revision,stationLocation:b.location}]};
 for(const mutate of [(v:any)=>v.candidates.push(v.candidates[0]),(v:any)=>v.candidates[0].id=p.id,(v:any)=>v.candidates[0].stationRevision='999',
  (v:any)=>v.candidates[0].id=1,(v:any)=>v.candidates[0].id='01',(v:any)=>v.extra=true]){const v=structuredClone(value);mutate(v);assert.throws(()=>parsePlatformProperties(v));}
 for(const name of ['Independent / 独立ホーム','  New name  ','a'.repeat(128)])assert(validPlatformName(name));
 for(const name of ['','  ','bad\0name','bad\u0085name','a'.repeat(129)])assert(!validPlatformName(name));
});
Deno.test('station controller keys resolve platform chains and stop on disconnected, removed or cyclic hosts',async()=>{
 const read=await scene(),g=read.mixed!.stationGraph!,station=g.buildings[0],first=g.platforms[0];
 const second={...first,id:'999999',hostId:first.id};g.platforms.push(second);
 const before=JSON.stringify(read);
 assert.deepEqual(resolvedPlatformStation(read,second.id)?.location,station.location);
 assert.equal(resolvedPlatformStation(read,station.id)?.id,station.id);
 assert.equal(JSON.stringify(read),before);
 first.hostId=null;assert.equal(resolvedPlatformStation(read,second.id),undefined);
 first.hostId=station.id;g.buildings=[];assert.equal(resolvedPlatformStation(read,second.id),undefined);
 first.hostId=second.id;assert.equal(resolvedPlatformStation(read,second.id),undefined);
 assert.equal(resolvedPlatformStation(undefined,second.id),undefined);
});
