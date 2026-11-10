import assert from 'node:assert/strict';
import {stationList,stationInformationQuery,currentStationInformation} from '../src/lib/station-information.ts';
import {parseStationInformation} from '../src/lib/protocol.ts';
import {createShellModel} from '../src/lib/shell-model.ts';
import {createFixtureService,type SessionClient} from '../src/lib/session-client.ts';
import type {Snapshot,StationInformation} from '../src/generated/contracts.ts';
const read={worldEpoch:'2',revision:'3',tick:'517',mixed:{stationGraph:{buildings:[{id:'20',name:'Station 10'},{id:'11',name:'Station 2'},{id:'4',name:'Station 2'}]}}} as Snapshot;
Deno.test('station list sorts localized numeric names with numeric stable identity ties without reordering authoritative state',()=>{
 assert.deepEqual(stationList(read,'en').map(s=>s.id),['4','11','20']);
 assert.deepEqual(read.mixed!.stationGraph!.buildings.map(b=>b.id),['20','11','4']);
 assert.deepEqual(stationList(undefined,'ja'),[]);
 assert.deepEqual(stationList({...read,mixed:{...read.mixed!,stationGraph:{...read.mixed!.stationGraph!,buildings:[{...read.mixed!.stationGraph!.buildings[0],name:'東京'}, {...read.mixed!.stationGraph!.buildings[1],name:'大阪'}]}}},'ja').map(s=>s.name),['大阪','東京']);
});
Deno.test('station query is tied to confirmed epoch revision minute and parses bounded zero versus absent demand',()=>{
 const q=stationInformationQuery(read,'20','30','40');assert.equal(currentStationInformation(read,q),true);
 for(const changed of [{worldEpoch:'3'},{revision:'4'},{tick:'518'}])assert.equal(currentStationInformation({...read,...changed},q),false);
 assert.equal(currentStationInformation(undefined,q),false);
 const r:StationInformation={query:q,demand:0,waiting:0,averageBoarded:0,averageAlighted:0,stopIndex:2,yardIndex:2,controllerId:'50',reason:'wait',waitMinutes:30,observedRevision:q.expectedRevision,observedMinute:q.expectedMinute};
 assert.deepEqual(parseStationInformation(r),r);assert.equal(parseStationInformation({...r,query:{...q,stationId:'0'},demand:null,waiting:null,averageBoarded:null,averageAlighted:null,stopIndex:null,yardIndex:null,controllerId:null,reason:'disconnected',waitMinutes:0}).demand,null);assert.throws(()=>parseStationInformation({...r,waiting:1}));assert.throws(()=>parseStationInformation({...r,waitMinutes:0}));assert.throws(()=>parseStationInformation({...r,yardIndex:1}));
 for(const invalid of [{demand:-1},{reason:'stoppedYesterday'},{waiting:2147483648},{stopIndex:'2'},{query:{...q,expectedRevision:3}}])assert.throws(()=>parseStationInformation({...r,...invalid}));
});
Deno.test('late station query rejects after peer edits, replacement or cancellation while preserving rename drafts',async()=>{
 for(const change of ['revision','worldEpoch','abort'] as const){
  const service=createFixtureService({normal:1});let resolve!:(r:StationInformation)=>void;
  const model=createShellModel(()=>{const client=service.connect();return {...client,stationInformation:()=>new Promise<StationInformation>(r=>{resolve=r;})} as SessionClient;},'en',0);
  try{
   while(model.state.connection!=='ready')await new Promise(r=>setTimeout(r,1));
   model.selectStation(model.state.read!.stations[0].id);model.setDraft('編集中');
   const q=stationInformationQuery(model.state.read!,'1'),cancel=new AbortController();
   const pending=model.stationInformation(q,cancel.signal);const rejected=assert.rejects(pending,/Stale station/);
   if(change==='abort')cancel.abort();else service.publish({...structuredClone(model.state.read!),[change]:String(BigInt(model.state.read![change])+1n)});
   resolve({query:q} as StationInformation);await rejected;
   if(change!=='worldEpoch')assert.equal(model.state.draft,'編集中');else assert.equal(model.state.draftDirty,false);
  }finally{model.dispose();}
 }
});
Deno.test('live observations carry an authoritative later minute while exact queries reject mixed timestamps',()=>{
 const q=stationInformationQuery(read,'20','30','40');
 const r:StationInformation={query:{...q,latest:true},observedRevision:'4',observedMinute:'518',demand:10000,waiting:10000,averageBoarded:0,averageAlighted:0,stopIndex:2,yardIndex:2,controllerId:'50',reason:'wait',waitMinutes:30};
 assert.equal(parseStationInformation(r).observedMinute,'518');
 assert.throws(()=>parseStationInformation({...r,query:q}));
 assert.throws(()=>parseStationInformation({...r,observedRevision:'2'}));
 assert.throws(()=>parseStationInformation({...r,observedMinute:'516'}));
});
Deno.test('latest station queries remain usable as time advances and preserve a local name draft',async()=>{
 const service=createFixtureService({normal:1});let resolve!:(r:StationInformation)=>void;
 const model=createShellModel(()=>({...service.connect(),stationInformation:()=>new Promise<StationInformation>(r=>{resolve=r;})}) as SessionClient,'en',0);
 try{
  while(model.state.connection!=='ready')await new Promise(r=>setTimeout(r,1));
  model.setDraft('Local draft');const q={...stationInformationQuery(model.state.read!,'1'),latest:true};
  const pending=model.stationInformation(q,new AbortController().signal);
  service.publish({...structuredClone(model.state.read!),revision:String(BigInt(q.expectedRevision)+1n),tick:String(BigInt(q.expectedMinute)+1n)});
  resolve({query:q,observedRevision:model.state.read!.revision,observedMinute:model.state.read!.tick} as StationInformation);
  assert.equal((await pending).observedMinute,model.state.read!.tick);assert.equal(model.state.draft,'Local draft');
 }finally{model.dispose();}
});
Deno.test('observation timestamps show the exact minute while the source clock keeps ten-minute precision',async()=>{
 const {formatGameTime}=await import('../src/lib/game-time.ts');
 assert(formatGameTime('517','en').endsWith('08:30'));assert(formatGameTime('517','en',true).endsWith('08:37'));
 assert(formatGameTime('517','ja',true).includes('08:37'));
});
