import assert from 'node:assert/strict';
import {parseRemovalPreview,parseMessage} from '../src/lib/protocol.ts';
import {removalAt,removalQuery,removalQueryKey} from '../src/lib/object-removal.ts';
import {createFixtureService} from '../src/lib/session-client.ts';
import {createShellModel} from '../src/lib/shell-model.ts';
import type {Snapshot,RemovalPreview} from '../src/generated/contracts.ts';
const read=JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/fixtures/snapshot-terrain.json',import.meta.url))) as Snapshot;
const query=removalQuery(read,{kind:'terrain',id:'2',position:{x:1,y:0,z:2},surface:'forest'})!;
const preview:RemovalPreview={query,legal:true,reason:null,objectKind:'forest',positions:[query.position],path:[query.position],skipped:[]};
const until=async(p:()=>boolean)=>{for(let i=0;i<100;i++){if(p())return;await new Promise(r=>setTimeout(r,1));}throw new Error('Timeout');};

Deno.test('object removal captures exact owner identity and anchors compound members and bare terrace tops',()=>{
 assert.equal(query.expectedEntityId,'2');assert.equal(query.expectedEntityRevision,read.entities![1].revision);
 assert.equal(removalQuery(read,{kind:'terrain',id:'cell:2,2',position:{x:1,y:2,z:3},surface:'water'}),undefined);
 const compound=structuredClone(read);compound.entities![1].footprint.push({offset:{x:1,y:0,z:1},transparent:true});
 assert.deepEqual(removalQuery(compound,{kind:'track',id:'2',position:{x:2,y:0,z:3}})?.position,{x:1,y:0,z:2});
 assert.equal(removalAt(compound,{x:2,y:0,z:3})?.expectedEntityId,'2');
 const terrace=structuredClone(read);terrace.entities=terrace.entities!.filter(e=>e.id!=='2');
 assert.equal(removalQuery(terrace,{kind:'terrain',id:'cell:0,0',position:{x:1,y:0,z:2},surface:'ground'})?.expectedEntityId,'1');
 assert.equal(removalQueryKey(query),removalQueryKey({...query,position:{z:2,y:0,x:1}}));
 assert.notEqual(removalQueryKey(query),removalQueryKey({...query,expectedEntityRevision:'999'}));
 assert.notEqual(removalQueryKey(query),removalQueryKey({...query,end:{x:2,y:1,z:2}}));
});

Deno.test('line removal previews carry a complete path, affected cells and protected skips',()=>{
 const line={query:{...query,expectedEntityId:'0',expectedEntityRevision:'0',end:{x:3,y:0,z:2}},objectKind:'line',legal:true,reason:null,
  positions:[query.position],path:[query.position,{x:2,y:0,z:2},{x:3,y:0,z:2}],skipped:[{position:{x:2,y:0,z:2},reason:'occupiedCar'}]};
 assert.deepEqual(parseRemovalPreview(line),line);
 for(const patch of [{path:[]},{path:[query.position,query.position]},{objectKind:'railway'},{query:{...line.query,expectedEntityId:'2'}},{skipped:[{position:query.position}]}])
  assert.throws(()=>parseRemovalPreview({...line,...patch}));
});

Deno.test('object removal protocol rejects malformed, duplicate and inconsistent loss previews',()=>{
 assert.deepEqual(parseRemovalPreview(preview),preview);
 assert.equal(parseRemovalPreview({...preview,legal:false,reason:'occupiedCar',positions:[]}).legal,false);
 for(const patch of [{positions:[]},{reason:'obstacle'},{positions:[query.position,query.position]},{objectKind:'anything'},{extra:true},{query:{...query,expectedEntityId:2}}])
  assert.throws(()=>parseRemovalPreview({...preview,...patch}));
 assert.equal(parseMessage(JSON.stringify({type:'command',requestId:'remove',expectedWorldEpoch:read.worldEpoch,payload:{kind:'removeObject',query}})).type,'command');
});

Deno.test('late removal previews reject geometry changes, epoch replacement, abort and reconnect',async()=>{
 for(const change of ['geometry','epoch','abort','connection']){
  const service=createFixtureService();service.publish(read);let complete!:(p:RemovalPreview)=>void;
  const model=createShellModel(()=>({...service.connect(),previewRemoval:()=>new Promise<RemovalPreview>(r=>complete=r)}),'en',1);
  try{
   await until(()=>model.state.connection==='ready');const request=new AbortController();const pending=model.previewRemoval(query,request.signal),rejected=assert.rejects(pending);
   if(change==='geometry')service.publish({...read,terrain:{...read.terrain!,geometryRevision:'999'}});
   if(change==='epoch')service.publish({...read,worldEpoch:'999'});
   if(change==='abort')request.abort();
   if(change==='connection')model.reconnect();
   complete(preview);await rejected;
  }finally{model.dispose();}
 }
});
