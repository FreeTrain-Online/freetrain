import assert from 'node:assert/strict';
import Ajv from 'ajv';
import {applyDelta,immutable,parseMessage,parseMessageValue} from '../src/lib/protocol.ts';
import {StateAssembler,stateChunkBytes} from '../src/lib/state-transfer.ts';
import type {Snapshot,Delta,StateChunk} from '../src/generated/contracts.ts';
const root=new URL('../../prototype/protocol/',import.meta.url);
const json=async(path:string)=>JSON.parse(await Deno.readTextFile(new URL(path,root)));
const scene=async()=>parseMessageValue(await json('fixtures/snapshot-fleet.json')) as Snapshot;
Deno.test('source deployment and withdrawal deltas publish exact occupancy, native initial delay and retained inventory to two peers',async()=>{
 let one=immutable(await scene()),two=immutable(await scene());const source=one;
 for(const action of ['placement','withdrawal']){
  const delta=parseMessageValue(await json(`fixtures/delta-fleet-${action}.json`)) as Delta;
  const a=applyDelta(one,delta),b=applyDelta(two,structuredClone(delta));assert(a&&b);assert.deepEqual(a,b);
  const train=a.mixed!.fleet!.trains[1];assert.equal(train.id,'20');assert.equal(a.cash,source.cash);
  assert.deepEqual(a.mixed!.fleet!.groups,source.mixed!.fleet!.groups);assert.deepEqual(a.mixed!.accounts,source.mixed!.accounts);
  assert.deepEqual(a.mixed!.voxels[2].traffic!.rail,source.mixed!.voxels[2].traffic!.rail);
  assert.equal(a.entities!.find(e=>e.id==='9')!.revision,action==='placement'?'1':'2');
  if(action==='placement'){
   assert.equal(train.state,'moving');assert.deepEqual(train.cars[0].state,{kind:'inside',location:{x:3,y:2,z:2},heading:0,countdown:null});
   assert.equal(a.mixed!.queue!.pending[1].dueMinute,'130083');assert.equal(a.mixed!.voxels[2].traffic!.car!.carId,'21');
   for(const change of [(d:Delta)=>d.mixed!.queue!.pending.pop(),(d:Delta)=>d.mixed!.voxels[2].traffic!.car=null,
    (d:Delta)=>d.mixed!.fleet!.trains[1].cars[0].state.heading=2]){
    const bad=structuredClone(delta);change(bad);assert.equal(applyDelta(one,bad),undefined);
   }
  }else{
   assert.equal(train.state,'unplaced');assert.equal(train.cars[0].state.location,null);assert.equal(a.mixed!.voxels[2].traffic!.car,null);
   assert.deepEqual(a.mixed!.queue!.pending,source.mixed!.queue!.pending);assert.equal(a.mixed!.queue!.nextOrder,'9007199254740995');
  }
  one=a;two=b;
 }
 assert.equal(source.mixed!.fleet!.trains[1].state,'unplaced');assert.equal(source.mixed!.queue!.nextOrder,'9007199254740994');
});
Deno.test('source rr.dir1 on a curve without an opposite port survives decoding while impossible normal and special headings reject',async()=>{
 const source=await scene(),rail=source.mixed!.voxels[0].traffic!.rail!,car=source.mixed!.fleet!.trains[0].cars[0];
 rail.pattern=11;rail.definition={packId:rail.definition.packId,contributionId:'system:pattern/11',recipeId:'system:pattern/11'};
 car.state.heading=0;assert.equal((parseMessage(JSON.stringify(source)) as Snapshot).mixed!.fleet!.trains[0].cars[0].state.heading,0);
 car.state.heading=2;assert.throws(()=>parseMessage(JSON.stringify(source)));
 // The literal tunnel has N/S ports: a special-purpose guide cannot turn an eighth.
 const special=await json('fixtures/delta-fleet-placement.json') as Delta;
 const placed=applyDelta(immutable(await scene()),special);assert(placed);
 const bad=structuredClone(placed) as Snapshot;bad.mixed!.fleet!.trains[1].cars[0].state.heading=1;
 assert.throws(()=>parseMessage(JSON.stringify(bad)));
});
Deno.test('source-priced sale delta removes reversed Inside/Outside stock and books revenue coherently for two peers',async()=>{
 const one=immutable(await scene()),two=immutable(await scene());
 const delta=parseMessageValue(await json('fixtures/delta-fleet-sale.json')) as Delta;
 const a=applyDelta(one,delta),b=applyDelta(two,structuredClone(delta));assert(a&&b);assert.deepEqual(a,b);
 assert.equal(a.cash,'7050000000');assert.equal(a.mixed!.accounts.transactions.at(-1)!.revenue,'50000000');
 assert.equal(a.mixed!.accounts.transactions.at(-1)!.expenditure,'0');
 assert.deepEqual(a.mixed!.fleet!.trains.map(t=>t.id),['20']);assert.deepEqual(a.mixed!.fleet!.groups[1].members,[]);
 assert.equal(a.mixed!.fleet!.controllers.length,one.mixed!.fleet!.controllers.length);
 assert.equal(a.mixed!.voxels[0].traffic!.car,null);assert.deepEqual(a.mixed!.queue!.pending,[]);
 assert.equal(a.mixed!.nextEntityId,one.mixed!.nextEntityId);assert.equal(a.entities!.find(e=>e.id==='7')!.revision,'1');
 assert.equal(one.cash,'7000000000');assert.equal(one.mixed!.fleet!.trains.length,2);
 const bad:((d:Delta)=>void)[]=[
  d=>d.mixed!.accounts.transactions.at(-1)!.revenue='50000001',
  d=>d.mixed!.accounts.transactions.at(-1)!.expenditure='1',
  d=>d.mixed!.accounts.transactions.at(-1)!.revenue='-1',
  d=>d.mixed!.accounts.transactions.at(-1)!.revenue='9223372036854775808',
  d=>d.mixed!.accounts.transactions[0].revenue='1',
  d=>d.mixed!.queue=structuredClone(one.mixed!.queue),
  d=>d.mixed!.fleet!.groups[1].members=['17'],
 ];
 for(const change of bad){const d=structuredClone(delta);change(d);assert.equal(applyDelta(one,d),undefined);}
 // Exact Int64 revenue and cash arithmetic beyond the JS safe integer boundary.
 const read=structuredClone(a) as Snapshot;read.mixed!.accounts.initialCash='9007199254740993';
 read.mixed!.accounts.transactions=[{...read.mixed!.accounts.transactions.at(-1)!,sequence:'1',revenue:'9007199254740993',cashAfter:'18014398509481986'}];
 read.cash='18014398509481986';assert.equal((parseMessage(JSON.stringify(read)) as Snapshot).cash,read.cash);
 read.mixed!.accounts.transactions[0].revenue='9223372036854775807';
 assert.throws(()=>parseMessage(JSON.stringify(read)));
 const save=await json('saves/fleet-v9.json');save.mixed=structuredClone(a.mixed);save.spatial.entities=read.entities!.filter(e=>BigInt(e.id)>=7n);
 save.revision=a.revision;save.clock.pending=[];
 const validate=new Ajv({strict:true}).compile(await json('save-v9.schema.json'));
 assert(validate(save),JSON.stringify(validate.errors));
});
Deno.test('literal Save9 fleet preserves reversed stock, shared controllers, absent harbor locations and exact queue integers',async()=>{
 const read=await scene(),f=read.mixed!.fleet!,q=read.mixed!.queue!;
 assert.equal(read.mixed!.nextEntityId,'9007199254741003');assert.equal(q.pending[0].registration,'9007199254740993');
 assert.deepEqual(f.trains[0].cars.map(c=>c.id),['19','18']);assert.equal(f.trains[1].purchaseMinute,null);
 assert.deepEqual(f.groups[0].members,['20','16']);assert.equal(f.controllers[3].harbors[0].location.x,-123);
 const schema=await json('save-v9.schema.json'),save=await json('saves/fleet-v9.json');
 const validate=new Ajv({strict:true}).compile(schema);assert(validate(save),JSON.stringify(validate.errors));
 assert.equal(validate({...save,saveVersion:8}),false);
 assert.throws(()=>parseMessage(JSON.stringify({...read,mixed:{...read.mixed,queue:{...q,nextOrder:9007199254740994}}})));
});
Deno.test('complete fleet delta removes inventory and unused controllers coherently for two immutable peers',async()=>{
 const one=immutable(await scene()),two=immutable(await scene()),delta=parseMessageValue(await json('fixtures/delta-fleet.json')) as Delta;
 const a=applyDelta(one,delta),b=applyDelta(two,structuredClone(delta));assert(a&&b);assert.deepEqual(a,b);
 assert.equal(a.mixed!.fleet!.trains.length,1);assert.equal(a.mixed!.fleet!.controllers.length,3);
 assert.deepEqual(a.mixed!.fleet!.groups[0].members,['16']);assert.equal(a.mixed!.fleet!.trains[0].name,'Renamed');
 assert.equal(a.mixed!.queue!.pending[0].dueMinute,'130084');assert.equal(one.mixed!.fleet!.trains.length,2);
 assert(Object.isFrozen(a.mixed!.fleet!.trains[0].cars));
});
Deno.test('fleet graph, original cars, exact provider identities, reserved signal ownership and queues fail before exposure',async()=>{
 const source=await scene();const mutations:((s:Snapshot)=>void)[]=[
 s=>s.mixed!.fleet!.trains[0].controllerId='999',s=>s.mixed!.fleet!.groups[1].parentId='16',
 s=>s.mixed!.fleet!.groups[0].members.reverse(), // order is legal; covered separately below
 s=>s.mixed!.fleet!.trains[0].cars[0].originalIndex=0,
 s=>s.mixed!.fleet!.trains[0].definition.catalogHash='0'.repeat(64),s=>s.mixed!.fleet!.controllers[3].provider!.contributionId='unknown',
 s=>s.mixed!.fleet!.trains[0].cars[0].state.countdown=10,s=>s.mixed!.voxels[0].traffic!.car!.carId='18',
 s=>s.mixed!.fleet!.trains[0].purchaseMinute='130082',s=>s.mixed!.fleet!.trains[0].passengers=2147483647,
 s=>s.mixed!.fleet!.trains[0].cars[1].state.countdown=101,
 s=>s.mixed!.fleet!.trains[0].cars[1].state.location={x:1,y:0,z:2147483647},
 s=>s.mixed!.voxels[3].traffic!.rail!.controllerId='15',s=>s.mixed!.fleet!.controllers[3].harbors.push(s.mixed!.fleet!.controllers[3].harbors[0]),
 s=>s.mixed!.queue!.pending[0].dueMinute=s.tick,s=>s.mixed!.queue!.pending.push(s.mixed!.queue!.pending[0]),
 s=>s.mixed!.queue!.pending[0].handlerId='train/20',s=>s.mixed!.queue!.pending[0].interval='1',s=>delete s.mixed!.queue,
 ];
 for(const [i,mutate] of mutations.entries()){
  const s=structuredClone(source);mutate(s);
  if(i===2){assert.deepEqual((parseMessage(JSON.stringify(s)) as Snapshot).mixed!.fleet!.groups[0].members,['16','20']);continue;}
  assert.throws(()=>parseMessage(JSON.stringify(s)),undefined,`mutation ${i}`);
 }
});
Deno.test('delta revisions, car ownership, built-ins and allocator high-water reject without mutating peers',async()=>{
 const before=immutable(await scene()),original=await json('fixtures/delta-fleet.json') as Delta;
 const mutations:((d:Delta)=>void)[]=[
 d=>d.mixed!.fleet!.trains[0].revision='0',d=>d.mixed!.fleet!.groups[0].revision='0',d=>d.mixed!.geometryRevision='5',
 d=>d.mixed!.fleet!.trains[0].cars[0].revision='2',d=>d.mixed!.fleet!.trains[0].cars[0].definition.recipeId='unknown',
 d=>d.mixed!.fleet!.controllers[0].id='22',d=>d.mixed!.fleet!.groups[1].id='22',d=>d.mixed!.nextEntityId='22',
 d=>d.mixed!.queue!.nextOrder='9007199254740993',
 ];
 for(const mutate of mutations){const d=structuredClone(original);mutate(d);assert.equal(applyDelta(before,d),undefined);}
 assert.equal(before.mixed!.fleet!.trains[0].name,'共有列車');
});
Deno.test('queue-only callback successor is coherent without a geometry revision and cannot disappear in scalar publication',async()=>{
 const before=immutable(await scene()),d=await json('fixtures/delta-fleet.json') as Delta;
 d.mixed={...structuredClone(before.mixed!),queue:d.mixed!.queue};
 const next=applyDelta(before,d);assert(next);assert.equal(next.mixed!.geometryRevision,'5');assert.equal(next.mixed!.queue!.pending[0].dueMinute,'130084');
 const missing=structuredClone(d);missing.mixed!.queue!.pending=[];assert.equal(applyDelta(before,missing),undefined);
 const expired=structuredClone(d);expired.mixed=structuredClone(before.mixed!);assert.equal(applyDelta(before,expired),undefined);
 const rewind=structuredClone(d);rewind.tick='130080';assert.equal(applyDelta(before,rewind),undefined);
});
Deno.test('out-of-order fleet chunks publish only the complete validated header to two peers',async()=>{
 const snapshot=await scene(),save=await json('saves/fleet-v9.json');
 const bytes=new TextEncoder().encode(JSON.stringify({message:{...snapshot,terrain:undefined,entities:save.spatial.entities},packedTerrain:save.packedTerrain})+' '.repeat(2*stateChunkBytes));
 const sha256=Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',bytes)),n=>n.toString(16).padStart(2,'0')).join('');
 const chunks:StateChunk[]=[];
 for(let offset=0;offset<bytes.length;offset+=stateChunkBytes)chunks.push({type:'stateChunk',transferId:'c'.repeat(32),worldEpoch:'1',revision:snapshot.revision,index:chunks.length,count:Math.ceil(bytes.length/stateChunkBytes),totalBytes:bytes.length,sha256,data:btoa(String.fromCharCode(...bytes.slice(offset,offset+stateChunkBytes)))});
 for(const peer of [new StateAssembler(),new StateAssembler()]){
  let result;for(const c of [chunks[1],chunks[0],...chunks.slice(2)]){result=await peer.accept(c);if(c!==chunks.at(-1))assert.equal(result,undefined);}
  assert.deepEqual(result,snapshot);
 }
});
Deno.test('protocol6 peers reject under protocol7 even with otherwise exact identity',async()=>{
 const s=await scene();assert.throws(()=>parseMessage(JSON.stringify({type:'sessionOffer',protocolVersion:6,contentVersion:s.contentVersion,ruleset:s.ruleset,requiredPacks:s.requiredPacks,worldEpoch:'1',serverIncarnation:'old',sessionId:'shared',participantId:'owner',role:'owner'})));
});
