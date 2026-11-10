import assert from 'node:assert/strict';
import Ajv from 'ajv';
import {parseMessageValue,parseMessage,applyDelta,immutable} from '../src/lib/protocol.ts';
import {StateAssembler,stateChunkBytes} from '../src/lib/state-transfer.ts';
import type {Snapshot,Delta,StateChunk} from '../src/generated/contracts.ts';
const json=async(path:string)=>JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/'+path,import.meta.url)));
const scene=async()=>parseMessageValue(await json('fixtures/snapshot-stations.json')) as Snapshot;
const delta=(s:Snapshot):Delta=>({type:'delta',worldEpoch:s.worldEpoch,baseRevision:s.revision,revision:'1',tick:'518',paused:true,cash:s.cash,
 trains:[],stations:[],buildings:[],tracks:[],removed:{trainIds:[],stationIds:[],buildingIds:[],trackPositions:[],entityIds:[]},ruleset:s.ruleset,contentVersion:s.contentVersion,requiredPacks:s.requiredPacks,
 entities:[],terrain:{waterLevel:0,constructionMode:false,geometryRevision:'0',nextEntityId:'1',cells:[],occupants:[]},mixed:structuredClone(s.mixed)});
Deno.test('station literal Save10 and protocol8 retain exact graph, source asymmetry and large repeat cookies',async()=>{
 const s=await scene(),g=s.mixed!.stationGraph!;
 assert.deepEqual(g.buildings[0].children,['3','2']);assert.equal(g.buildings[0].imports.thisWeek,23.125);
 assert.equal(g.buildings[0].hourTimer!.registration,'9007199254740993');assert.equal(g.buildings[0].hourTimer!.interval,'60');
 assert.deepEqual(g.listeners,[{providerId:'40',stations:['1']}]);assert.deepEqual(g.listenerMemberships,[]);
 const schema=await json('save-v10.schema.json'),validate=new Ajv({strict:true}).compile(schema),save=await json('saves/stations-v10.json');
 assert(validate(save),JSON.stringify(validate.errors));assert(!validate({...save,saveVersion:9}));
});
Deno.test('station decoder rejects malformed hosts, art, ownership, logs, listener references and timer queues before reveal',async()=>{
 const s=await scene();const mutations:((s:Snapshot)=>void)[]=[
 s=>s.mixed!.stationGraph!.platforms[0].hostId='2',s=>s.mixed!.stationGraph!.buildings[0].children=[],
 s=>s.mixed!.stationGraph!.buildings[0].definition.recipeId='missing',s=>s.mixed!.stationGraph!.platforms[0].bell.recipeId='missing',
 s=>s.mixed!.stationGraph!.platforms[0].yards[0].trafficOwnerId='999',s=>s.mixed!.stationGraph!.platforms[0].yards[0].outlook.upward=true,
 s=>s.entities![0].footprint[0].transparent=true,s=>s.mixed!.stationGraph!.buildings[0].imports.thisWeek=-1,
 s=>s.mixed!.stationGraph!.listeners[0].stations=['1','1'],s=>s.mixed!.stationGraph!.listeners[0].providerId='999',
 s=>s.mixed!.stationGraph!.listenerMemberships=[{stationId:'1',providers:['999']}],
 s=>s.mixed!.stationGraph!.buildings[0].hourTimer!.order='0',s=>s.mixed!.queue!.pending.pop(),
 s=>s.mixed!.queue!.pending[0].interval='0',s=>delete s.mixed!.queue,s=>delete s.mixed!.stationGraph,
 ];for(const mutate of mutations){const candidate=structuredClone(s);mutate(candidate);assert.throws(()=>parseMessage(JSON.stringify(candidate)));}
 assert.equal(s.mixed!.stationGraph!.buildings[0].name,'Station / 駅');
});
Deno.test('controlled station provider expressions are bounded and require owner revisions on update',async()=>{
 const s=await scene(),g=s.mixed!.stationGraph!,l=g.listeners[0];
 l.provider={input:'controlled',landValue:3,xySize:6,zSize:8,special:false,
  population:{kind:'PersistentPopulation',base:0,factor:1,entering:0,tables:[],children:[
   {kind:'ResidentialPopulation',base:125,factor:1,entering:0,tables:[],children:[]}]}};
 g.listenerMemberships=[{stationId:g.buildings[0].id,providers:[l.providerId]}];
 assert.deepEqual(parseMessage(JSON.stringify(s)),s);
 const d=delta(s);d.mixed!.geometryRevision='1';d.mixed!.stationGraph!.listeners[0].provider!.landValue=4;
 assert.equal(applyDelta(immutable(s),structuredClone(d)),undefined);
 d.entities=s.entities!.filter(e=>e.id===l.providerId).map(e=>({...e,revision:'1'}));
 assert(applyDelta(immutable(s),d));
 const mutations:((x:Snapshot)=>void)[]=[
  x=>x.mixed!.stationGraph!.listeners[0].provider!.population.children=[],
  x=>x.mixed!.stationGraph!.listeners[0].provider!.population.tables=[Array(24).fill(0)],
  x=>x.mixed!.stationGraph!.listeners[0].provider!.input='city' as 'controlled',
  x=>{let p=x.mixed!.stationGraph!.listeners[0].provider!.population;for(let i=0;i<18;i++){p.children=[structuredClone(p)];p=p.children[0];}},
  x=>x.mixed!.stationGraph!.listeners[0].stations=[],
 ];
 for(const mutate of mutations){const bad=structuredClone(s);mutate(bad);assert.throws(()=>parseMessage(JSON.stringify(bad)));}
 assert.equal(l.provider.input,'controlled');
});
Deno.test('station logical edits require revisions and fixed geometry; complete removal preserves name allocators atomically',async()=>{
 const s=immutable(await scene()),d=delta(s as Snapshot);d.mixed!.geometryRevision='1';d.mixed!.stationGraph!.platforms[0].name='Edited';
 assert.equal(applyDelta(s,structuredClone(d)),undefined);d.mixed!.stationGraph!.platforms[0].revision='1';const renamed=applyDelta(s,d);assert(renamed);
 for(const mutate of [(d:Delta)=>d.mixed!.stationGraph!.nextStationName=1,(d:Delta)=>d.mixed!.stationGraph!.platforms[0].length=3]){
  const bad=structuredClone(d);mutate(bad);assert.equal(applyDelta(s,bad),undefined);
 }
 const removal=delta(s as Snapshot);removal.mixed!.geometryRevision='1';removal.mixed!.voxels=[];removal.mixed!.queue!.pending=[];
 removal.mixed!.stationGraph={nextStationName:17,nextPlatformName:23,buildings:[],platforms:[],passages:[],listeners:[],listenerMemberships:[]};
 removal.removed.entityIds=s.entities!.map(e=>e.id);const empty=applyDelta(s,removal);assert(empty);assert.equal(empty.entities!.length,0);
 assert.equal(empty.mixed!.stationGraph!.nextPlatformName,23);assert.equal(s.mixed!.stationGraph!.platforms.length,2);
 const missing=structuredClone(removal);missing.removed.entityIds!.pop();assert.equal(applyDelta(s,missing),undefined);
});
Deno.test('station chunk assembly publishes only a complete coherent terrain, ownership, host and FIFO graph',async()=>{
 const snapshot=await scene(),save=await json('saves/stations-v10.json'),header={...snapshot,terrain:undefined,entities:save.spatial.entities};
 const bytes=new TextEncoder().encode(JSON.stringify({message:header,packedTerrain:save.packedTerrain})+' '.repeat(stateChunkBytes));
 const hash=Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',bytes)),n=>n.toString(16).padStart(2,'0')).join('');
 const assembler=new StateAssembler();let result;
 for(let offset=0;offset<bytes.length;offset+=stateChunkBytes){
  const chunk:StateChunk={type:'stateChunk',transferId:'c'.repeat(32),worldEpoch:'1',revision:'0',index:offset/stateChunkBytes,count:Math.ceil(bytes.length/stateChunkBytes),totalBytes:bytes.length,sha256:hash,data:btoa(String.fromCharCode(...bytes.slice(offset,offset+stateChunkBytes)))};
  result=await assembler.accept(chunk);if(offset+stateChunkBytes<bytes.length)assert.equal(result,undefined);
 }assert.deepEqual(result,snapshot);
});

Deno.test('host reparent and recursive disconnect deltas preserve real station identity and require reciprocal owner revisions',async()=>{
 const s=immutable(await scene()),d=delta(s as Snapshot);d.mixed!.geometryRevision='1';
 const g=d.mixed!.stationGraph!,thin=g.platforms.find(p=>p.kind==='thin')!,wide=g.platforms.find(p=>p.kind==='wide')!,station=g.buildings[0];
 // The thin platform explicitly joins the wide platform's real station chain.
 thin.hostId=wide.id;thin.revision='1';wide.children=[thin.id];wide.revision='1';station.children=[wide.id];station.revision='1';
 d.entities=s.entities!.filter(e=>[station.id,wide.id].includes(e.id)).map(e=>({...e,revision:'1'}));
 const joined=applyDelta(s,d);assert(joined);assert.equal(joined.mixed!.stationGraph!.platforms[0].hostId,wide.id);
 for(const mutate of [(bad:Delta)=>bad.mixed!.stationGraph!.platforms.find(p=>p.kind==='wide')!.children=[],
  (bad:Delta)=>bad.entities!.find(e=>e.id===wide.id)!.revision='0',
  (bad:Delta)=>bad.mixed!.stationGraph!.buildings[0].children.push(thin.id)]){
  const bad=structuredClone(d);mutate(bad);assert.equal(applyDelta(s,bad),undefined);
 }
 const clear=delta(joined);clear.baseRevision=joined.revision;clear.revision='2';clear.mixed!.geometryRevision='2';
 const cleared=clear.mixed!.stationGraph!;
 for(const p of cleared.platforms){p.hostId=null;p.children=[];p.revision='2';}
 cleared.buildings[0].children=[];cleared.buildings[0].revision='2';
 clear.entities=joined.entities!.filter(e=>[station.id,wide.id].includes(e.id)).map(e=>({...e,revision:'2'}));
 const disconnected=applyDelta(joined,clear);assert(disconnected);
 assert(disconnected.mixed!.stationGraph!.platforms.every(p=>p.hostId===null));
 assert.deepEqual(disconnected.mixed!.queue,joined.mixed!.queue);
 assert.deepEqual(disconnected.mixed!.accounts,joined.mixed!.accounts);
 assert.equal(s.mixed!.stationGraph!.platforms[0].hostId,station.id);
});
