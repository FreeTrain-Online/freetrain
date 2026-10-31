import assert from 'node:assert/strict';
import { parseMessage, parseRailwayPreview } from '../src/lib/protocol.ts';
import { railwayRevision,railwayFence,railwayQueryKey,isRailwayPromotion,railwayStartLevel } from '../src/lib/railway-edit.ts';
import { createFixtureService } from '../src/lib/session-client.ts';
import { createShellModel } from '../src/lib/shell-model.ts';
import type { RailwayQuery,RailwayPreview,Snapshot } from '../src/generated/contracts.ts';
import {railwayProfile} from '../src/lib/mixed-state.ts';
const terrain=JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/fixtures/snapshot-terrain.json',import.meta.url))) as Snapshot;
const q:RailwayQuery={expectedWorldEpoch:terrain.worldEpoch,expectedGeometryRevision:terrain.terrain!.geometryRevision,expectedContentVersion:'terrain-content-1',from:{x:1,y:0,z:2},to:{x:2,y:0,z:2},action:'lay',routing:'closest'};
const preview:RailwayPreview={query:q,legal:true,reason:null,cost:'12000000',reverseBend:false,cells:[q.from,q.to].map(position=>({position,beforePattern:null,afterPattern:4,protectedByCar:false})),addedPiers:[],losses:[]};
const until=async(predicate:()=>boolean)=>{for(let i=0;i<100;i++){if(predicate())return;await new Promise(r=>setTimeout(r,1));}throw new Error('Timeout');};

Deno.test('railway preview validates exact money, bounded patterns and legal/blocked consistency',()=>{
 assert.deepEqual(parseRailwayPreview(preview),preview);
 assert.deepEqual(parseRailwayPreview({...preview,legal:false,reason:'obstacle',cost:'0'}).reason,'obstacle');
 for(const patch of [{cost:12000000},{cost:'-1'},{reason:'obstacle'},{cells:[]},{extra:true},{cells:[{...preview.cells[0],afterPattern:60}]}])assert.throws(()=>parseRailwayPreview({...preview,...patch}));
 assert.throws(()=>parseMessage(JSON.stringify({type:'command',requestId:'rail',expectedWorldEpoch:'1',payload:{kind:'editRailway',query:{...q,routing:'ignore-obstacles'}}})));
 assert.equal(railwayQueryKey(q),railwayQueryKey({...q,from:{z:2,y:0,x:1}}));
 assert.notEqual(railwayQueryKey(q),railwayQueryKey({...q,to:{...q.to,x:3}}));
});

Deno.test('first railway publication admits only the exact same-epoch terrain profile handoff',()=>{
 const promoted={...terrain,...railwayProfile,revision:String(BigInt(terrain.revision)+1n)} as unknown as Snapshot;
 assert(isRailwayPromotion(terrain,promoted));
 for(const changed of [{worldEpoch:'999'},{revision:terrain.revision},{map:{...terrain.map,width:99}},{requiredPacks:[]},{contentVersion:'unknown'},{terrain:{...terrain.terrain!,name:'replacement'}}])assert(!isRailwayPromotion(terrain,{...promoted,...changed}));
 assert(!isRailwayPromotion(promoted,terrain));
 assert.equal(railwayRevision(terrain),terrain.terrain!.geometryRevision);
 assert.notEqual(railwayFence(terrain),railwayFence(promoted));
});

Deno.test('automatic keyboard level uses actual ground, higher existing rail and the local cut without mutating the read',()=>{
 const read=structuredClone(terrain);read.terrain!.cells=read.terrain!.cells.map(c=>({...c,ground:4}));
 read.mixed={geometryRevision:'4',nextEntityId:'10',voxels:[
  {position:{x:1,y:0,z:5},traffic:{rail:{pattern:4}}},
  {position:{x:1,y:0,z:2},traffic:{rail:{pattern:4}}},
  {position:{x:2,y:1,z:5},kind:'pier'},
 ]} as unknown as Snapshot['mixed'];
 const before=JSON.stringify(read);
 assert.equal(railwayStartLevel(read,{x:1,y:0},5),5);
 assert.equal(railwayStartLevel(read,{x:1,y:0},4),4);
 assert.equal(railwayStartLevel(read,{x:1,y:0},2),2);
 assert.equal(railwayStartLevel(read,{x:2,y:1},5),4,'A pier does not determine the track level');
 assert.equal(railwayStartLevel(read,{x:1,y:0},999),5);
 for(const position of [{x:999,y:999},{x:NaN,y:0},{x:1.5,y:0},{x:Number.MAX_SAFE_INTEGER,y:0}])assert.equal(railwayStartLevel(read,position,5),undefined);
 assert.equal(railwayStartLevel(read,{x:1,y:0},-1),undefined);
 assert.equal(JSON.stringify(read),before);
});

Deno.test('late railway preview cannot survive an edit, profile change, aborted request or connection replacement',async()=>{
 for(const change of ['geometry','profile','abort','connection']){
  const service=createFixtureService();service.publish(terrain);let complete!:(p:RailwayPreview)=>void;
  const model=createShellModel(()=>({...service.connect(),previewRailway:()=>new Promise<RailwayPreview>(resolve=>complete=resolve)}),'en',1);
  try{
   await until(()=>model.state.connection==='ready');const request=new AbortController();const result=model.previewRailway(q,request.signal);const rejected=assert.rejects(result);
   if(change==='geometry')service.publish({...terrain,terrain:{...terrain.terrain!,geometryRevision:'999'}});
   if(change==='profile')service.publish({...terrain,...railwayProfile} as unknown as Snapshot);
   if(change==='abort')request.abort();
   if(change==='connection')model.reconnect();
   complete(preview);await rejected;
  }finally{model.dispose();}
 }
});
