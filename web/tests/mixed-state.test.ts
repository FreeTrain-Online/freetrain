import assert from 'node:assert/strict';
import Ajv from 'ajv';
import {parseMessage,applyDelta,immutable,parseMessageValue} from '../src/lib/protocol.ts';
import {validSnapshot,supportedIdentity} from '../src/lib/world-identity.ts';
import {StateAssembler,stateChunkBytes} from '../src/lib/state-transfer.ts';
import type {Snapshot,Delta,StateChunk} from '../src/generated/contracts.ts';
const base=new URL('../../prototype/protocol/',import.meta.url);
const json=async(p:string)=>JSON.parse(await Deno.readTextFile(new URL(p,base)));
const scene=async()=>parseMessageValue(await json('fixtures/snapshot-mixed.json')) as Snapshot;
Deno.test('literal mixed Save9 schema, snapshot and complete removal delta preserve terrain, accounts and orphan piers',async()=>{
 const before=await scene(),delta=parseMessageValue(await json('fixtures/delta-mixed.json')) as Delta;
 assert(supportedIdentity(before));const next=applyDelta(immutable(before),delta);assert(next);
 assert.equal(next.cash,'6999800000');assert.equal(next.mixed!.voxels.length,3);assert.equal(next.mixed!.voxels[0].supportedOwnerId,'7');
 assert.equal(next.entities!.length,6);assert.equal(next.terrain!.nextEntityId,'7');assert.equal(next.mixed!.nextEntityId,'11');
 assert.equal(before.mixed!.voxels.length,4);assert.equal(next.mixed!.accounts.transactions.length,3);
 const schema=await json('save-v9.schema.json'),validate=new Ajv({strict:true}).compile(schema),save=await json('saves/mixed-v9.json');
 assert(validate(save),JSON.stringify(validate.errors));assert.equal(validate({...save,saveVersion:7}),false);
});
Deno.test('unknown profiles, overwritten terrain, malformed components, journal replay and future timestamps reject before reveal',async()=>{
 const before=await scene();
 const mutations:((s:Snapshot)=>void)[]=[
 s=>s.requiredPacks[0].catalogHash='0'.repeat(64),s=>s.contentVersion='missing',s=>s.mixed!.nextEntityId='7',
 s=>s.mixed!.voxels[0].traffic!.rail!.definition.recipeId='system:pattern/2',s=>s.mixed!.voxels[0].ownerId='1',
 s=>{s.mixed!.voxels[0].position.z=2;s.entities!.find(e=>e.id==='7')!.anchor.z=2;},
 s=>s.mixed!.voxels[2].traffic!.rail!.terrainBackup=null,s=>s.mixed!.accounts.transactions[0].cashAfter='0',
 s=>s.mixed!.accounts.transactions[0].genre='unknown',s=>s.mixed!.accounts.transactions[0].absoluteMinute='130082',
 s=>s.mixed!.geometryRevision='4',s=>s.mixed!.voxels[0].traffic!.road={definition:s.mixed!.voxels[0].traffic!.rail!.definition,edges:17},
 ];
 for(const mutate of mutations){const candidate=structuredClone(before);mutate(candidate);assert.throws(()=>parseMessage(JSON.stringify(candidate)));}
 assert.equal(before.cash,'7000000000');
});
Deno.test('mixed deltas reject owner revision loss, allocator rewind, journal rewrite, malformed removals and stale bases atomically',async()=>{
 const before=immutable(await scene()),original=await json('fixtures/delta-mixed.json') as Delta;
 const mutations:((d:Delta)=>void)[]=[d=>d.baseRevision='4',d=>d.mixed!.nextEntityId='10',d=>d.entities![0].revision='0',
 d=>d.mixed!.accounts.transactions[0].operation='rewritten',d=>d.removed.entityIds=[],d=>d.mixed!.geometryRevision='5',
 d=>delete d.mixed];
 for(const mutate of mutations){const delta=structuredClone(original);mutate(delta);assert.equal(applyDelta(before,delta),undefined);}
 assert.equal(before.entities!.length,7);assert.equal(before.mixed!.geometryRevision,'5');
});
Deno.test('mixed chunk transfer retains railway header entities and publishes only the complete validated graph',async()=>{
 const snapshot=await scene(),saved=await json('saves/mixed-v9.json');
 const header={...snapshot,terrain:undefined,entities:saved.spatial.entities};
 const bytes=new TextEncoder().encode(JSON.stringify({message:header,packedTerrain:saved.packedTerrain})+' '.repeat(stateChunkBytes));
 const hash=Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',bytes)),n=>n.toString(16).padStart(2,'0')).join('');
 const assembler=new StateAssembler();let result;
 for(let offset=0;offset<bytes.length;offset+=stateChunkBytes){
  const chunk:StateChunk={type:'stateChunk',transferId:'b'.repeat(32),worldEpoch:snapshot.worldEpoch,revision:snapshot.revision,
   index:offset/stateChunkBytes,count:Math.ceil(bytes.length/stateChunkBytes),totalBytes:bytes.length,sha256:hash,data:btoa(String.fromCharCode(...bytes.slice(offset,offset+stateChunkBytes)))};
  result=await assembler.accept(chunk);if(offset+stateChunkBytes<bytes.length)assert.equal(result,undefined);
 }
 assert.deepEqual(result,snapshot);
});
Deno.test('protocol5 peers fail closed under the deliberate protocol7 change',async()=>{
 const snapshot=await scene();
 assert.throws(()=>parseMessage(JSON.stringify({type:'sessionOffer',protocolVersion:5,contentVersion:snapshot.contentVersion,ruleset:snapshot.ruleset,
  requiredPacks:snapshot.requiredPacks,worldEpoch:'1',serverIncarnation:'old',sessionId:'shared',participantId:'owner',role:'owner'})));
});
Deno.test('all independent legal slope vectors validate their exact eight-cell membership, flags and recipes',async()=>{
 const fixtures=JSON.parse(await Deno.readTextFile(new URL('../../doc/parity/railway-fixtures.json',import.meta.url)));
 const original=await scene(),definition=(id:string,recipeId=id)=>({packId:original.requiredPacks[0].id,contributionId:id,recipeId});
 let count=0;
 for(const row of fixtures.groups.slopes.cases.filter((r:{expected:{outcome:string}})=>r.expected.outcome==='apply')){
  const snapshot=structuredClone(original),d=row.input.direction,anchor={x:row.input.base[0],y:row.input.base[1],z:row.input.base[2]},position=(p:number[])=>({x:p[0],y:p[1],z:p[2]});
  snapshot.map={width:32,height:16,levels:8,coordinateSystem:'hv'};snapshot.revision='0';snapshot.cash='15000000000';
  snapshot.terrain={...snapshot.terrain!,name:'Slope fixture',geometryRevision:'0',nextEntityId:'1',cells:Array.from({length:512},()=>({ground:2,surface:'natural'})),occupants:[],waterLevel:0,
   provenance:{profile:'terrain-generation-1',source:'empty',requestHash:'0'.repeat(64),requestJson:JSON.stringify({source:'empty',Profile:'terrain-generation-1',Name:'Slope fixture',Dimensions:{H:32,V:16,Z:8},WaterLevel:0})}};
  const voxels=row.expected.rails.map((r:{location:number[];patternId:number})=>({position:position(r.location),ownerId:'1',kind:'traffic',traffic:{rail:{definition:definition(`system:pattern/${r.patternId}`),pattern:r.patternId,variant:0,terrainBackup:null,direction:0,controllerId:null},road:null,accessory:null,car:null,platform:null},definition:null,sprite:0,drawGroundBelow:true,drawGroundAbove:true,supportedOwnerId:null}));
  for(const [i,p] of row.expected.paired.entries()){
   const kind=p.kind==='filler'?'slopefiller':p.kind==='support'?'slopesupport':'slopewall';
   voxels.push({position:position(p.location),ownerId:'1',kind,traffic:null,definition:kind==='slopefiller'?null:definition(`system:pattern/${kind==='slopewall'?'underground-':''}slope-support-${d/2*4+i}`),sprite:kind==='slopefiller'?0:p.sprite,drawGroundBelow:p.below,drawGroundAbove:p.above,supportedOwnerId:null});
  }
  snapshot.mixed={geometryRevision:'0',nextEntityId:'2',voxels,slopes:[{entityId:'1',climb:d}],accounts:{initialCash:'15000000000',bankruptcyIncrement:'10000000000',transactions:[]}};
  snapshot.entities=[{id:'1',revision:'0',definition:definition('system:slope','geometry:eight-cell-slope-1'),anchor,
   footprint:voxels.map((v:{position:{x:number;y:number;z:number}})=>({offset:{x:v.position.x-anchor.x,y:v.position.y-anchor.y,z:v.position.z-anchor.z},transparent:true})),owned:true,silentlyReclaimable:false,value:'0'}];
  assert(validSnapshot(parseMessage(JSON.stringify(snapshot)) as Snapshot));
  snapshot.mixed.voxels.pop();assert.throws(()=>parseMessage(JSON.stringify(snapshot)));count++;
 }
 assert.equal(count,15);
});
Deno.test('mixed clock deltas preserve validated graph references and reject changed graph without a geometry revision',async()=>{
 const state=immutable(await scene()),d=await json('fixtures/delta-mixed.json') as Delta;
 const delta:Delta={...d,mixed:structuredClone(state.mixed) as Delta['mixed'],cash:state.cash,entities:[],removed:{trainIds:[],stationIds:[],buildingIds:[],trackPositions:[],entityIds:[]},tick:'130082'};
 const next=applyDelta(state,delta);assert(next);assert.equal(next.mixed,state.mixed);assert.equal(next.entities,state.entities);assert.equal(next.terrain,state.terrain);
 delta.mixed!.voxels[0].traffic!.rail!.pattern=2;assert.equal(applyDelta(state,delta),undefined);
});

Deno.test('speed lists validate Traffic eligibility, preserve duplicate order and reject absent, filler and pier membership',async()=>{
 const read=await scene(),traffic=read.mixed!.voxels.find(v=>v.traffic)!,pier=read.mixed!.voxels.find(v=>v.kind==='pier')!;
 read.mixed!.speedLimits={low:[traffic.position,traffic.position],medium:[],fast:[traffic.position]};
 assert.doesNotThrow(()=>parseMessage(JSON.stringify(read)));
 for(const p of [pier.position,{...traffic.position,z:7},{x:-999,y:0,z:0}]){
  const invalid=structuredClone(read);invalid.mixed!.speedLimits!.low=[p];assert.throws(()=>parseMessage(JSON.stringify(invalid)));
 }
 const oversized=structuredClone(read);oversized.mixed!.speedLimits!.low=Array(8193).fill(traffic.position);assert.throws(()=>parseMessage(JSON.stringify(oversized)));
});

Deno.test('shared playback rates validate and update clock-only deltas without rebuilding geometry',async()=>{
 const state=immutable(await scene()),d=await json('fixtures/delta-mixed.json') as Delta;
 for(const playbackRate of [0.125,0.25,0.5,1,2] as const){
  assert.doesNotThrow(()=>parseMessageValue({...state,playbackRate}));
  assert.doesNotThrow(()=>parseMessageValue({type:'command',requestId:'speed',expectedWorldEpoch:state.worldEpoch,payload:{kind:'setPlaybackRate',rate:playbackRate}}));
  const delta:Delta={...d,playbackRate,mixed:structuredClone(state.mixed) as Delta['mixed'],cash:state.cash,entities:[],removed:{trainIds:[],stationIds:[],buildingIds:[],trackPositions:[],entityIds:[]},tick:state.tick};
  const next=applyDelta(state,parseMessageValue(delta) as Delta);assert(next);
  assert.equal(next.playbackRate,playbackRate);assert.equal(next.tick,state.tick);
  assert.equal(next.mixed,state.mixed);assert.equal(next.terrain,state.terrain);assert.equal(next.entities,state.entities);
 }
 for(const rate of [0,-1,0.3,3,'1',null])assert.throws(()=>parseMessageValue({...state,playbackRate:rate}));
});
