import assert from 'node:assert/strict';
import {stationDefinitions,stationCatalog,stationBuildingAt,stationBuildingQuery,stationBuildingFence} from '../src/lib/station-building-edit.ts';
import {parseStationBuildingPreview} from '../src/lib/protocol.ts';
import {createShellModel} from '../src/lib/shell-model.ts';
import type {Snapshot} from '../src/generated/contracts.ts';
const scene=async()=>JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/fixtures/snapshot-stations.json',import.meta.url))) as Snapshot;
Deno.test('station catalog binds all 35 authored footprints, prices and reviewed EN/JA names',async()=>{
 const catalog=JSON.parse(await Deno.readTextFile(new URL('../public/content/bundled-v2/catalog.json',import.meta.url)));
 const en=stationCatalog(catalog,'en'),ja=stationCatalog(catalog,'ja');assert.equal(en.length,35);
 for(const [i,d] of en.entries()){
  assert.equal(d.reference.recipeId,stationDefinitions[i].reference.recipeId);
  assert(d.title.length);assert(!/[\u3040-\u30ff\u4e00-\u9fff]/u.test(d.title+d.orientation),d.title);
  assert(BigInt(d.price)>0n);assert(BigInt(d.upkeep)>=0n);assert.notEqual(d.title,ja[i].title);
 }
});
Deno.test('station removal resolves the actual identity and revision from every footprint cell',async()=>{
 const s=await scene(),b=s.mixed!.stationGraph!.buildings[0],d=stationDefinitions.find(d=>d.reference.recipeId===b.definition.recipeId)!;
 for(let x=0;x<d.sizeX;x++)for(let y=0;y<d.sizeY;y++)for(let z=0;z<d.height;z++){
  const p={x:b.location.x+x,y:b.location.y+y,z:b.location.z+z};assert.equal(stationBuildingAt(s,p)?.id,b.id);
  const q=stationBuildingQuery(s,p,null,true)!;assert.equal(q.stationId,b.id);assert.equal(q.expectedStationRevision,b.revision);assert.deepEqual(q.position,p);
 }
 assert.equal(stationBuildingQuery(s,{x:0,y:0,z:0},null,true),undefined);
 assert.notEqual(stationBuildingFence(s),stationBuildingFence({...s,tick:String(BigInt(s.tick)+1n)}));
 const q=stationBuildingQuery(s,b.location,b.definition)!;
 const p={query:q,legal:true,reason:null,cost:'100',positions:[b.location],losses:[]};assert.deepEqual(parseStationBuildingPreview(p),p);
 for(const bad of [{...p,positions:[]},{...p,positions:[b.location,b.location]},{...p,reason:'obstacle'},{...p,legal:false},{...p,cost:'-1'}])assert.throws(()=>parseStationBuildingPreview(bad));
});
Deno.test('canonical station drafts retain peer conflicts, reset against current names, and use renameStation',async()=>{
 let read=await scene(),publish:(s:Snapshot)=>void=()=>{},sent:any;
 const client:any={mode:'remote',role:'operator',subscribe:(f:any)=>{publish=f;f(read);return()=>{};},dispose:()=>{},send:async(c:any)=>{sent=c;const b=read.mixed!.stationGraph!.buildings[0];b.name=c.payload.name;b.revision=String(BigInt(b.revision)+1n);publish(structuredClone(read));return {type:'accepted'};}};
 const model=createShellModel(()=>client,'en',0);
 try{
  await new Promise(r=>setTimeout(r,10));const b=read.mixed!.stationGraph!.buildings[0];model.selectStation(b.id);model.setDraft('中央駅');
  b.name='Peer';b.revision='1';publish(structuredClone(read));assert.equal(model.state.draft,'中央駅');assert.equal(model.state.draftRevision,'0');
  model.resetDraft();assert.equal(model.state.draft,'Peer');assert.equal(model.state.draftRevision,'1');
  model.setDraft('Applied');await model.rename();assert.equal(sent.payload.kind,'renameStation');assert.equal(sent.payload.stationId,b.id);assert.equal(sent.payload.expectedRevision,'1');assert.equal(model.state.draftDirty,false);
 }finally{model.dispose();}
});
