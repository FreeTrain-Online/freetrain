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

Deno.test('slope preview rejects malformed occupancy, illegal money and unsupported command directions',async()=>{
 const {parseSlopePreview}=await import('../src/lib/protocol.ts');
 const {slopeQueryKey}=await import('../src/lib/railway-edit.ts');
 const query={expectedWorldEpoch:q.expectedWorldEpoch,expectedGeometryRevision:q.expectedGeometryRevision,expectedContentVersion:q.expectedContentVersion,position:q.from,climb:2 as const,action:'lay' as const};
 const rails=Array.from({length:4},(_,i)=>({level:i,position:{x:i+1,y:0,z:2+Math.floor(i/2)},pattern:32+i,zDiff:[0,1,-1,0][i] as -1|0|1,zAngle:(i===2?6:2) as 2|6}));
 const members=rails.flatMap((r,i)=>[{position:r.position,kind:'traffic' as const,pattern:r.pattern,drawGroundBelow:true,drawGroundAbove:true},
  {position:{...r.position,z:3-Math.floor(i/2)},kind:i<2?'slopefiller' as const:'slopesupport' as const,pattern:null,drawGroundBelow:true,drawGroundAbove:true}]);
 const p={query,legal:true,reason:null,cost:'120000000',base:q.from,climb:2 as const,members,rails,addedPiers:[],losses:[]};
 assert.deepEqual(parseSlopePreview(p),p);
 assert.equal(parseSlopePreview({...p,legal:false,reason:'obstacle',cost:'0',rails:[]}).reason,'obstacle');
 for(const patch of [{cost:120},{cost:'-1'},{reason:'obstacle'},{members:members.slice(1)},{members:members.map(()=>members[0])},{rails:[]},{extra:true},{legal:false}])assert.throws(()=>parseSlopePreview({...p,...patch}));
 assert.equal(slopeQueryKey(query),slopeQueryKey({...query,position:{z:2,y:0,x:1}}));
 assert.notEqual(slopeQueryKey(query),slopeQueryKey({...query,climb:0}));
 for(const patch of [{climb:1},{action:'removeEntity'},{cost:'0'},{position:null}])assert.throws(()=>parseMessage(JSON.stringify({type:'command',requestId:'slope',expectedWorldEpoch:'1',payload:{kind:'editSlope',query:{...query,...patch}}})));
});

Deno.test('late slope previews reject abort, epoch, geometry, profile and reconnect generations',async()=>{
 for(const change of ['geometry','epoch','profile','content','abort','connection','dispose']){
  const service=createFixtureService();service.publish(terrain);let complete!:(p:any)=>void;
  const model=createShellModel(()=>({...service.connect(),previewSlope:()=>new Promise<any>(resolve=>complete=resolve)}),'en',1);
  try{
   await until(()=>model.state.connection==='ready');const request=new AbortController();
   const query={expectedWorldEpoch:q.expectedWorldEpoch,expectedGeometryRevision:q.expectedGeometryRevision,expectedContentVersion:q.expectedContentVersion,position:q.from,climb:2 as const,action:'lay' as const};
   const result=model.previewSlope(query,request.signal),rejected=assert.rejects(result);
   if(change==='geometry')service.publish({...terrain,terrain:{...terrain.terrain!,geometryRevision:'999'}});
   if(change==='epoch')service.publish({...terrain,worldEpoch:'999'});
   if(change==='content')service.publish({...terrain,contentVersion:'changed-content'} as unknown as Snapshot);
   if(change==='dispose')model.dispose();
   if(change==='profile')service.publish({...terrain,...railwayProfile} as unknown as Snapshot);
   if(change==='abort')request.abort();
   if(change==='connection')model.reconnect();
   complete({query});await rejected;
  }finally{model.dispose();}
 }
});

Deno.test('slope removal geometric picking includes invisible filler occupancy at the visible cut',async()=>{
 const {pickRailwayLocation}=await import('../src/lib/railway-renderer.ts');
 const {WorldSpace}=await import('../src/lib/map-geometry.ts');
 const read=structuredClone(terrain);read.terrain!.cells=read.terrain!.cells.map(c=>({...c,ground:0}));
 const position={x:1,y:0,z:2},space=new WorldSpace(read.map.width,read.map.height,read.map.levels);
 read.mixed={slopes:[{entityId:'99',climb:2}],voxels:[{position,ownerId:'99',kind:'slopefiller',traffic:null}]} as any;
 const center=space.center(position);
 assert.deepEqual(pickRailwayLocation(read,center,2,{kind:'slopeOrGround'}),position);
 assert.notDeepEqual(pickRailwayLocation(read,center,2,{kind:'railOrGround'}),position);
 assert.notDeepEqual(pickRailwayLocation(read,center,1,{kind:'slopeOrGround'}),position);
});

Deno.test('bridge preview echoes unsnapped input while cells carry snapped geometry and infrastructure fences exclude other tasks',()=>{
 const bridge={...q,infrastructure:'bridge' as const,to:{x:5,y:2,z:7}};
 const p={...preview,query:bridge};assert.equal(parseRailwayPreview(p).query.infrastructure,'bridge');
 assert.notEqual(railwayQueryKey(q),railwayQueryKey({...q,infrastructure:'bridge'}));
 assert.equal(railwayQueryKey(q),railwayQueryKey({...q,infrastructure:'normal'}));
 assert.throws(()=>parseRailwayPreview({...p,query:{...bridge,infrastructure:'unknown'}}));
});

Deno.test('girder requests retain distinct task identity through the shared preview and command schema',()=>{
 const girder={...q,infrastructure:'girder' as const};
 assert.equal(parseRailwayPreview({...preview,query:girder}).query.infrastructure,'girder');
 assert.notEqual(railwayQueryKey(girder),railwayQueryKey({...girder,infrastructure:'bridge'}));
 assert.notEqual(railwayQueryKey(girder),railwayQueryKey(q));
 assert.equal(parseMessage(JSON.stringify({type:'command',requestId:'girder',expectedWorldEpoch:'1',payload:{kind:'editRailway',query:girder}})).type,'command');
});

Deno.test('girder automatic height starts above actual ground, preserves elevated attachment and stays within world levels',()=>{
 const read=structuredClone(terrain);read.terrain!.cells=read.terrain!.cells.map(c=>({...c,ground:2}));
 const position={x:1,y:0};const before=JSON.stringify(read);
 assert.equal(railwayStartLevel(read,position,5,1),3);
 assert.equal(railwayStartLevel(read,position,1,1),3,'A cut does not make a buried girder legal');
 assert.equal(railwayStartLevel(read,position,5),2,'Normal rail retains its ground-level policy');
 assert.equal(JSON.stringify(read),before);
 read.mixed={voxels:[{position:{...position,z:4},traffic:{rail:{pattern:4}}}]} as Snapshot['mixed'];
 assert.equal(railwayStartLevel(read,position,5,1),4);
 assert.equal(railwayStartLevel(read,position,3,1),3,'Hidden rail does not set the level');
 read.terrain!.cells=read.terrain!.cells.map(c=>({...c,ground:read.map.levels-1}));
 assert.equal(railwayStartLevel(read,position,5,1),read.map.levels-1,'The authoritative preview reports no ground clearance at the ceiling');
});
Deno.test('tunnel requests keep their own preview identity and pass the command schema',()=>{
 const tunnel={...q,infrastructure:'tunnel' as const};
 assert.equal(parseRailwayPreview({...preview,query:tunnel}).query.infrastructure,'tunnel');
 for(const infrastructure of ['normal','bridge','girder'] as const)assert.notEqual(railwayQueryKey(tunnel),railwayQueryKey({...tunnel,infrastructure}));
 assert.equal(parseMessage(JSON.stringify({type:'command',requestId:'tunnel',expectedWorldEpoch:'1',payload:{kind:'editRailway',query:tunnel}})).type,'command');
});

Deno.test('portal routing is explicit in the command schema and every preview fence',()=>{
 const exact={...q,infrastructure:'tunnel' as const};
 assert.equal(railwayQueryKey(exact),railwayQueryKey({...exact,tunnelRouting:'exact'}));
 for(const tunnelRouting of ['portals','portals-auto'] as const){
  const query={...exact,tunnelRouting};assert.notEqual(railwayQueryKey(exact),railwayQueryKey(query));
  assert.equal(parseRailwayPreview({...preview,query}).query.tunnelRouting,tunnelRouting);
  assert.equal(parseMessage(JSON.stringify({type:'command',requestId:'portal',expectedWorldEpoch:'1',payload:{kind:'editRailway',query}})).type,'command');
 }
 assert.notEqual(railwayQueryKey({...exact,tunnelRouting:'portals'}),railwayQueryKey({...exact,tunnelRouting:'portals-auto'}));
 assert.throws(()=>parseRailwayPreview({...preview,query:{...exact,tunnelRouting:'anywhere'}}));
});

Deno.test('signal preview identities include type and direction',()=>{
 const q={expectedWorldEpoch:'1',expectedGeometryRevision:'2',expectedContentVersion:'mixed-railway-content-1',from:{x:1,y:2,z:1},to:{x:1,y:2,z:1},action:'lay',routing:'source',infrastructure:'signal'} as const;
 assert.equal(railwayQueryKey(q),railwayQueryKey({...q,signalType:'{5349DE2A-DF3E-49B4-87A0-973105B7E6A4}',signalDirection:0}));
 assert.notEqual(railwayQueryKey(q),railwayQueryKey({...q,signalDirection:2}));
 assert.notEqual(railwayQueryKey(q),railwayQueryKey({...q,signalType:'{6FA55842-E693-414f-A120-0A3D0C613B21}'}));
});
Deno.test('signal previews accept exactly one cell while route tools retain their minimum',()=>{
 const signal={...preview,query:{...q,to:q.from,infrastructure:'signal' as const},cost:'0',cells:[preview.cells[0]]};
 assert.deepEqual(parseRailwayPreview(signal),signal);
 assert.throws(()=>parseRailwayPreview({...signal,cells:[]}));
 assert.throws(()=>parseRailwayPreview({...signal,cells:preview.cells}));
 assert.throws(()=>parseRailwayPreview({...signal,query:q}));
});
Deno.test('pole request identity and single-cell preview retain exact type selection',()=>{
 const query={...q,to:q.from,infrastructure:'pole' as const};
 assert.equal(railwayQueryKey(query),railwayQueryKey({...query,poleType:'{DDF7F835-8100-45fd-8AE7-EC498796D387}'}));
 assert.notEqual(railwayQueryKey(query),railwayQueryKey({...query,poleType:'{FD1635C1-05BF-41af-9185-4891B983E2A4}'}));
 const pole={...preview,query,cost:'0',cells:[preview.cells[0]]};assert.deepEqual(parseRailwayPreview(pole),pole);
 assert.throws(()=>parseRailwayPreview({...pole,cells:[]}));assert.throws(()=>parseRailwayPreview({...pole,cells:preview.cells}));
});
Deno.test('stationary preview identities bind exact malformed-looking contribution IDs and accept full height footprints',()=>{
 const query={...q,to:q.from,infrastructure:'stationary' as const,stationaryType:'{{98619BD4-46C3-4B91-85FE-11147914F2A5}-E'};
 const quote={...preview,query,cost:'0',cells:[preview.cells[0]]};
 assert.deepEqual(parseRailwayPreview(quote),quote);
 assert.deepEqual(parseRailwayPreview({...quote,cells:preview.cells}).cells,preview.cells);
 assert.throws(()=>parseRailwayPreview({...quote,cells:[]}));
 assert.notEqual(railwayQueryKey(query),railwayQueryKey({...query,stationaryType:'other variant'}));
 assert.equal(parseMessage(JSON.stringify({type:'command',requestId:'stationary',expectedWorldEpoch:'1',payload:{kind:'editRailway',query}})).type,'command');
});

Deno.test('speed-limit preview identity includes typed speed, shape and direction, with exact four-cell slope validation',()=>{
 const query={...q,infrastructure:'speed' as const,speed:'low' as const,speedSlope:false,speedClimb:0 as const};
 const p={...preview,query,cost:'0'};
 assert.deepEqual(parseRailwayPreview(p),p);
 for(const patch of [{speed:'fast' as const},{speedSlope:true},{speedClimb:2 as const}])assert.notEqual(railwayQueryKey(query),railwayQueryKey({...query,...patch}));
 assert.equal(railwayQueryKey(query),railwayQueryKey({...q,infrastructure:'speed'}));
 const slope={...p,query:{...query,speedSlope:true},cells:[0,1,2,3].map(i=>({...p.cells[0],position:{x:1,y:-i,z:2+Math.floor(i/2)}}))};
 assert.deepEqual(parseRailwayPreview(slope),slope);
 assert.throws(()=>parseRailwayPreview({...slope,cells:slope.cells.slice(1)}));
 for(const patch of [{speed:'warp'},{speedClimb:1},{speedSlope:'yes'}])assert.throws(()=>parseMessage(JSON.stringify({type:'command',requestId:'speed',expectedWorldEpoch:'1',payload:{kind:'editRailway',query:{...query,...patch}}})));
});

Deno.test('every delivered route/accessory preview fences epoch, geometry, content, cancellation and reconnect independently of locale',async()=>{
 for(const infrastructure of ['normal','bridge','girder','tunnel','garage','signal','pole','stationary','speed'] as const){
  for(const change of ['epoch','geometry','content','abort','connection','dispose','locale']){
   const service=createFixtureService();service.publish(terrain);let complete!:(p:RailwayPreview)=>void;
   const model=createShellModel(()=>({...service.connect(),previewRailway:()=>new Promise<RailwayPreview>(r=>complete=r)}),'en',1);
   try{
    await until(()=>model.state.connection==='ready');const abort=new AbortController(),query={...q,infrastructure};
    const result=model.previewRailway(query,abort.signal),rejected=change==='locale'?undefined:assert.rejects(result);
    if(change==='epoch')service.publish({...terrain,worldEpoch:'999'});
    if(change==='geometry')service.publish({...terrain,terrain:{...terrain.terrain!,geometryRevision:'999'}});
    if(change==='content')service.publish({...terrain,contentVersion:'changed-content'} as unknown as Snapshot);
    if(change==='abort')abort.abort();
    if(change==='connection')model.reconnect();
    if(change==='dispose')model.dispose();
    if(change==='locale')model.setLocale('ja');
    complete({...preview,query});
    if(rejected)await rejected;else {assert.equal((await result).query,query);assert.equal(model.state.locale,'ja');}
   }finally{model.dispose();}
  }
 }
});
