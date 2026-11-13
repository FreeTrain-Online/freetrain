import assert from 'node:assert/strict';
import Ajv from 'ajv';
import schema from '../../prototype/protocol/save-v11.schema.json' with {type:'json'};
import literal from '../../prototype/protocol/fixtures/snapshot-city.json' with {type:'json'};
import removal from '../../prototype/protocol/fixtures/delta-city.json' with {type:'json'};
import save from './fixtures/city-v11.json' with {type:'json'};
import {parseMessageValue,immutable,applyDelta,parseCityPreview} from '../src/lib/protocol.ts';
import {cityQuery,currentCityDraft} from '../src/lib/city-edit.ts';
import {StateAssembler,stateChunkBytes} from '../src/lib/state-transfer.ts';
import type {Snapshot,Delta,StateChunk} from '../src/generated/contracts.ts';
const snapshot=()=>parseMessageValue(structuredClone(literal)) as Snapshot;
Deno.test('city current save/schema and snapshot retain q/back/rho, selected plan, caller, initial producer and half listener asymmetry',()=>{
 const validate=new Ajv({strict:true,validateFormats:false}).compile(schema);assert(validate(save),JSON.stringify(validate.errors));
 assert(!validate({...save,saveVersion:10}));
 const s=snapshot(),c=s.mixed!.city!;assert.equal(c.heat.q[0],-12.5);assert.equal(c.heat.back[0],8.25);assert.equal(c.heat.rho[0],0.25);
 assert.equal(c.landCache[0].entityValue,'500');assert.equal(c.plans[0].phase,'build');assert.equal(c.structures.find(o=>o.id==='20')!.caller,'catalogplayer');
 const g=s.mixed!.stationGraph!;assert.deepEqual(g.listeners[0].stations,['1','2','3','5']);assert.equal(g.listeners[0].provider!.input,'city-fixed');assert(!g.listenerMemberships.find(m=>m.stationId==='5')?.providers.includes('10'));
});
Deno.test('city malformed definitions, cells, producer provenance, handles, buffers, plans, fences and bounds reject before publication',()=>{
 const mutations:((s:Snapshot)=>void)[]=[
 s=>s.mixed!.city!.structures[0].definition.contributionId='missing',s=>s.mixed!.city!.structures[0].subsidiary=null,
 s=>s.mixed!.city!.structures[3].construction[0].callback!.handlerId='wrong',s=>s.mixed!.city!.structures[3].construction[0].cell.state=4,
 s=>s.mixed!.city!.structures[3].construction[0].offset.z=1,s=>s.mixed!.city!.structures[3].caller='unknown' as never,
 s=>s.mixed!.city!.heat.q.pop(),s=>s.mixed!.city!.heat.rho[0]=-1,s=>s.mixed!.city!.settings.maximumPricePower=0,
 s=>s.mixed!.city!.producers[0].ownerId='20',s=>s.mixed!.stationGraph!.listeners[0].provider!.input='controlled',
 s=>s.mixed!.stationGraph!.listeners[0].provider!.input='city-ground',s=>s.mixed!.stationGraph!.listeners[0].provider!.population.base++,
 s=>s.mixed!.city!.plans[0].stationId='999',s=>s.mixed!.city!.plans[0].selected!.finalPrice='0',
 s=>s.mixed!.city!.fences[0].hostOwnerId='999',s=>s.mixed!.city!.properties[0].id='1',
 s=>s.entities!.find(e=>e.id==='20')!.footprint[0].transparent=true,s=>s.mixed!.queue!.pending.pop(),
 s=>(s.mixed!.city as unknown as {extra:number}).extra=1,s=>delete (s.mixed!.city as unknown as {heat?:unknown}).heat,
 s=>s.mixed!.city!.properties=Array(8193).fill(s.mixed!.city!.properties[0]),s=>delete s.mixed!.city,
 ];
 for(const mutate of mutations){const s=structuredClone(literal) as Snapshot;mutate(s);assert.throws(()=>parseMessageValue(s));}
});
Deno.test('city coherent removal delta cancels callbacks and owners once, preserves cached samples and rejects stale or malformed successors',()=>{
 const before=immutable(snapshot()),d=parseMessageValue(removal) as Delta,next=applyDelta(before,d);assert(next);
 assert(!next.mixed!.city!.structures.some(s=>s.id==='20'));assert(!next.entities!.some(e=>e.id==='20'));assert(!next.mixed!.queue!.pending.some(a=>a.handlerId.startsWith('city/construction/20/')));
 assert.deepEqual(next.mixed!.city!.landCache,before.mixed!.city!.landCache);assert.equal(applyDelta(next,d),undefined);
 for(const mutate of [
 (d:Delta)=>d.removed.entityIds=[],(d:Delta)=>d.mixed!.city!.structures=structuredClone(literal.mixed.city.structures) as never,
 (d:Delta)=>{d.mixed!.city!.properties[0].revision='1';d.mixed!.city!.properties[0].location.x++;},
 (d:Delta)=>d.mixed!.city!.structures[0].caller='automated',
 ]){const bad=structuredClone(d);mutate(bad);assert.equal(applyDelta(before,bad),undefined);}
});
Deno.test('city previews are read-only and drafts expire on session revision, minute, epoch, content or owner revision',()=>{
 const read=immutable(snapshot()),q=cityQuery(read,'20')!;assert(currentCityDraft(read,q));
 const p={query:q,canCancel:true,reason:null,cost:'0',structure:read.mixed!.city!.structures.find(s=>s.id==='20')!};assert.deepEqual(parseCityPreview(p),p);
 for(const mutate of [(s:Snapshot)=>s.revision=String(BigInt(s.revision)+1n),(s:Snapshot)=>s.tick='1981',(s:Snapshot)=>s.worldEpoch='2',(s:Snapshot)=>s.contentVersion='unknown',(s:Snapshot)=>s.mixed!.city!.structures[3].revision='1']){
  const s=structuredClone(read) as Snapshot;mutate(s);assert(!currentCityDraft(s,q));
 }
 assert.throws(()=>parseCityPreview({...p,cost:'1'}));assert.throws(()=>parseCityPreview({...p,query:{...q,ownerId:'10'}}));assert.equal(cityQuery(read,'999'),undefined);
});
Deno.test('bounded city chunks publish only the complete checksum-validated owner and listener graph',async()=>{
 const s=snapshot(),message={...s,terrain:undefined,entities:save.spatial.entities};
 const data=new TextEncoder().encode(JSON.stringify({message,packedTerrain:save.packedTerrain})+' '.repeat(stateChunkBytes));
 const sha256=Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',data)),n=>n.toString(16).padStart(2,'0')).join('');
 const assembler=new StateAssembler();let result;
 for(let offset=0;offset<data.length;offset+=stateChunkBytes){const chunk:StateChunk={type:'stateChunk',transferId:'c'.repeat(32),worldEpoch:s.worldEpoch,revision:s.revision,
  index:offset/stateChunkBytes,count:Math.ceil(data.length/stateChunkBytes),totalBytes:data.length,sha256,data:btoa(String.fromCharCode(...data.slice(offset,offset+stateChunkBytes)))};
  result=await assembler.accept(chunk);if(offset+stateChunkBytes<data.length)assert.equal(result,undefined);
 }
 assert.deepEqual(result,s);
});

Deno.test('coalesced city publications accept advanced owner revisions and newly created then revised owners without permitting allocator reuse',()=>{
 const before=immutable(snapshot()),d=structuredClone(removal) as Delta;
 d.revision=String(BigInt(before.revision)+8n);d.mixed!.geometryRevision=d.revision;
 const host=d.mixed!.city!.fenceHosts[0];host.revision='3';d.mixed!.city!.fences[0].side=1;
 d.entities=[{...structuredClone(literal.entities.find(e=>e.id===host.id)!),revision:'3'}] as Delta['entities'];
 const original=d.mixed!.city!.properties[0],created={...structuredClone(original),id:'21',revision:'3',location:{x:24,y:22,z:2}};
 d.mixed!.city!.properties.push(created);d.mixed!.nextEntityId='22';
 d.entities!.push({...structuredClone(literal.entities.find(e=>e.id===original.id)!),id:created.id,revision:created.revision,anchor:created.location});
 const next=applyDelta(before,parseMessageValue(d) as Delta);assert(next);assert.equal(next.mixed!.city!.properties.at(-1)!.revision,'3');
 const reused=structuredClone(d);reused.mixed!.city!.properties.at(-1)!.id='19';reused.entities!.at(-1)!.id='19';assert.equal(applyDelta(before,reused),undefined);
});
Deno.test('live heat publication retains exact Single diffusion, both buffers and repeated timer cookies',async()=>{
 const before=JSON.parse(await Deno.readTextFile(new URL('./fixtures/city-heat-v11.json',import.meta.url)));
 const after=parseMessageValue(JSON.parse(await Deno.readTextFile(new URL('./fixtures/city-heat-v11.json.snapshot.json',import.meta.url)))) as Snapshot;
 const heat=after.mixed!.city!.heat,index=(h:number,v:number)=>h*(heat.verticalSize+2)+v;
 assert.equal(before.mixed.city.heat.q[index(2,2)],10);
 assert.equal(heat.q[index(2,2)],3.7559996); // server round-trip Single representation
 assert.equal(heat.back[index(2,2)],10);
 assert.equal(after.mixed!.city!.heatCallback!.registration,before.mixed.city.heatCallback.registration);
 assert.equal(BigInt(after.mixed!.city!.heatCallback!.dueMinute)-BigInt(before.mixed.city.heatCallback.dueMinute),600n);
 const bad=structuredClone(after);bad.mixed!.city!.heat.q[index(2,2)]=Infinity;assert.throws(()=>parseMessageValue(bad));
});
