import { StateAssembler, stateChunkBytes, unpackTerrain } from '../src/lib/state-transfer.ts';
import { applyDelta, immutable, parseMessage } from '../src/lib/protocol.ts';
import type { Snapshot, StateChunk, Delta } from '../src/generated/contracts.ts';
import type { PackedTerrain } from '../src/lib/state-transfer.ts';
import { deepStrictEqual as equal } from 'node:assert';
const assert=(value:unknown)=>{if(!value)throw new Error('Assertion failed');};
const snapshot=()=>Deno.readTextFile(new URL('../../prototype/protocol/fixtures/snapshot-terrain.json',import.meta.url)).then(s=>parseMessage(s) as Snapshot);
async function fixture() {
  const original=await snapshot(),saved=JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/saves/terrain-v8.json',import.meta.url)));
  const data=new TextEncoder().encode(JSON.stringify({message:{...original,entities:[],terrain:undefined},packedTerrain:saved.packedTerrain})+' '.repeat(2*stateChunkBytes));
  const hash=Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',data)),n=>n.toString(16).padStart(2,'0')).join('');
  const chunks:StateChunk[]=[];
  for(let offset=0;offset<data.length;offset+=stateChunkBytes)chunks.push({type:'stateChunk',transferId:'a'.repeat(32),worldEpoch:original.worldEpoch,revision:original.revision,
    index:chunks.length,count:Math.ceil(data.length/stateChunkBytes),totalBytes:data.length,sha256:hash,data:btoa(String.fromCharCode(...data.slice(offset,offset+stateChunkBytes)))});
  return {original,chunks,packed:saved.packedTerrain as PackedTerrain};
}
Deno.test('server packed save expands to the independent logical wire fixture without losing trees or IDs',async()=>{
  const {original,packed}=await fixture();equal(unpackTerrain(packed,original.map),{entities:original.entities,terrain:original.terrain});
});
Deno.test('bounded state assembly permits out-of-order chunks and publishes only a complete hash-validated world',async()=>{
  const {original,chunks}=await fixture(),a=new StateAssembler();
  equal(parseMessage(JSON.stringify(chunks[0])),chunks[0]);
  assert(await a.accept(chunks[1])===undefined);assert(a.pending);
  assert(await a.accept(chunks[0])===undefined);
  equal(await a.accept(chunks[2]),original);assert(!a.pending);
});
Deno.test('metadata-only session offers are unambiguous protocol messages',async()=>{
  const identity=await snapshot();
  const offer={type:'sessionOffer',protocolVersion:6,contentVersion:'terrain-content-1',serverIncarnation:'test',sessionId:'reference-spatial',participantId:'owner',role:'owner',worldEpoch:'1',ruleset:'terrain-state-1',requiredPacks:identity.requiredPacks};
  equal(parseMessage(JSON.stringify(offer)),offer);
});
Deno.test('duplicates, conflicting identity, corruption, count bombs, length mismatch and expiry fail closed',async()=>{
  const {chunks}=await fixture();
  const rejected=async(action:()=>Promise<unknown>)=>{let rejected=false;try{await action();}catch{rejected=true;}assert(rejected);};
  const duplicate=new StateAssembler();await duplicate.accept(chunks[0]);await rejected(()=>duplicate.accept(chunks[0]));
  for(const patch of [{transferId:'b'.repeat(32)},{worldEpoch:'2'},{revision:'999'},{sha256:'0'.repeat(64)},{totalBytes:0x7fffffff},{count:999999},{data:'AA=='}]) {
    const a=new StateAssembler();await a.accept(chunks[0]);await rejected(()=>a.accept({...chunks[1],...patch}));
  }
  const expired=new StateAssembler();await expired.accept(chunks[0],0);await rejected(()=>expired.accept(chunks[1],30000));
  const corrupt=new StateAssembler();for(const c of chunks.slice(0,-1))await corrupt.accept({...c,sha256:'0'.repeat(64)});
  await rejected(()=>corrupt.accept({...chunks.at(-1)!,sha256:'0'.repeat(64)}));
});
Deno.test('packed terrain validates binary allocation bounds and malformed geometry before exposure',async()=>{
  const {original,packed}=await fixture();
  for(const p of [{...packed,occupantCount:2147483647},{...packed,cells:'AA=='},{...packed,occupants:packed.occupants+'AAAA'},{...packed,cells:btoa(String.fromCharCode(192,...Array(8).fill(2)))}]) {
    let rejected=false;try{unpackTerrain(p,original.map);}catch{rejected=true;}assert(rejected);
  }
});
Deno.test('clock-only terrain deltas preserve graph references and reject clock overflow',async()=>{
  const before=immutable(await snapshot());
  const delta:Delta={type:'delta',worldEpoch:before.worldEpoch,baseRevision:before.revision,revision:'7',tick:'130082',paused:false,cash:'0',
    ruleset:before.ruleset,contentVersion:before.contentVersion,requiredPacks:before.requiredPacks,trains:[],stations:[],buildings:[],tracks:[],entities:[],
    removed:{trainIds:[],stationIds:[],buildingIds:[],trackPositions:[],entityIds:[]},
    terrain:{waterLevel:before.terrain!.waterLevel,constructionMode:before.terrain!.constructionMode,geometryRevision:before.terrain!.geometryRevision,nextEntityId:before.terrain!.nextEntityId,cells:[],occupants:[]}};
  const next=applyDelta(before,delta);assert(next?.terrain===before.terrain);assert(next?.entities===before.entities);
  assert(!applyDelta(before,{...delta,tick:'9223372036854775807'}));
});
Deno.test('expanded JSON object bombs are rejected by the structure scan before parsing',async()=>{
  const data=new TextEncoder().encode('['+'{},'.repeat(1000000)+'{}]');
  const hash=Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',data)),n=>n.toString(16).padStart(2,'0')).join('');
  const assembly=new StateAssembler();let message='';
  try{
    for(let offset=0,index=0;offset<data.length;offset+=stateChunkBytes,index++)await assembly.accept({type:'stateChunk',transferId:'a'.repeat(32),worldEpoch:'1',revision:'0',index,count:Math.ceil(data.length/stateChunkBytes),totalBytes:data.length,sha256:hash,data:btoa(String.fromCharCode(...data.slice(offset,offset+stateChunkBytes)))});
  }catch(error){message=(error as Error).message;}
  equal(message,'State JSON structure');
});
