import { parseMessage, applyDelta, immutable } from '../src/lib/protocol.ts';
import type { Snapshot, Delta } from '../src/generated/contracts.ts';
function assert(value:unknown):asserts value {if(!value)throw new Error('Assertion failed');}
function equal(a:unknown,b:unknown){assert(JSON.stringify(a)===JSON.stringify(b));}
function rejects(action:()=>unknown){try{action();}catch{return;}throw new Error('Invalid terrain accepted');}
const read=()=>Deno.readTextFile(new URL('../../prototype/protocol/fixtures/snapshot-terrain.json',import.meta.url)).then(s=>parseMessage(s) as Snapshot);
const change=()=>Deno.readTextFile(new URL('../../prototype/protocol/fixtures/delta-terrain.json',import.meta.url)).then(s=>parseMessage(s) as Delta);
Deno.test('shared server terrain snapshot carries actual zero-rail world, stable realized trees and original provenance',async()=>{
  const state=await read();
  assert(state.tracks.length===0&&state.entities!.length===3&&state.terrain!.cells.length===9);
  equal(state.terrain!.occupants[1].forest!.trees,[{x:0,y:0,sprite:0},{x:30,y:15,sprite:8}]);
  equal(state.terrain!.occupants[2].creekConnections,13);
  equal(state.terrain!.waterLevel,3);equal(JSON.parse(state.terrain!.provenance.requestJson).WaterLevel,1);
  const frozen=immutable(state);assert(Object.isFrozen(frozen.terrain!.occupants[1].forest!.trees[0]));
});
Deno.test('shared server terrain removals clear geometry and payload while preserving old snapshot and allocator',async()=>{
  const before=immutable(await read()),delta=await change(),next=applyDelta(before,delta);
  assert(next&&next.terrain);equal(next.entities,[]);equal(next.terrain.occupants,[]);
  equal(next.terrain.nextEntityId,'7');equal(next.terrain.constructionMode,false);equal(next.terrain.waterLevel,0);
  assert(next.terrain.cells.every(c=>c.ground===1&&c.surface==='cut'));
  equal(before.terrain!.occupants.length,3);equal(before.terrain!.cells[0].ground,2);
  assert(!applyDelta(next,delta)); // Out of order/repeated base is never replayed.
});
Deno.test('terrain deltas reject mixed graphs, stale epochs, pack drift, duplicate cells, lost removals and allocator rewind',async()=>{
  const before=await read(),delta=await change();
  for(const patch of [
    {...delta,worldEpoch:'2'}, {...delta,baseRevision:'999'}, {...delta,terrain:undefined}, {...delta,entities:undefined},
    {...delta,requiredPacks:[]}, {...delta,removed:{...delta.removed,entityIds:[]}},
    {...delta,terrain:{...delta.terrain!,nextEntityId:'6'}},
    {...delta,terrain:{...delta.terrain!,cells:[delta.terrain!.cells[0],delta.terrain!.cells[0]]}},
    {...delta,terrain:{...delta.terrain!,cells:[{index:9,ground:1,surface:'cut'}]}},
    {...delta,terrain:{...delta.terrain!,geometryRevision:'0'}},
  ])assert(!applyDelta(before,patch as Delta));
});
Deno.test('terrain snapshot validator rejects mismatched payloads, footprints, definitions, ownership and water',async()=>{
  const original=await read();
  const changes:((s:Snapshot)=>void)[]=[
    s=>{delete s.terrain;},s=>{delete s.entities;},s=>{s.map.width=2049;},s=>{s.requiredPacks[0].catalogHash='0'.repeat(64);},
    s=>{s.terrain!.cells.pop();},s=>{s.terrain!.cells[0].surface='natural';},s=>{s.terrain!.waterLevel=6;},
    s=>{s.terrain!.occupants.pop();},s=>{s.terrain!.occupants[1].ne=5;},s=>{s.terrain!.occupants[1].forest!.trees[0].sprite=99;},
    s=>{s.terrain!.occupants[1].forest!.definition.packId='optional';},s=>{s.terrain!.nextEntityId='2';},
    s=>{s.entities![0].owned=true;},s=>{s.entities![1].footprint[0].transparent=true;},s=>{s.entities![2].anchor={...s.entities![1].anchor};},
    s=>{s.entities![0].definition.recipeId='missing';},s=>{s.terrain!.provenance.requestJson='{}';},
  ];
  for(const change of changes){const state=structuredClone(original);change(state);rejects(()=>parseMessage(JSON.stringify(state)));}
});
Deno.test('terrain wire and save schemas accept server fixtures including zero occupants and reject retired Save 5',async()=>{
  const {default:Ajv}=await import('ajv');
  const schema=JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/save-v7.schema.json',import.meta.url)));
  const fixture=JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/saves/terrain-v7.json',import.meta.url)));
  const validate=new Ajv({strict:true}).compile(schema);assert(validate(fixture));
  for(const patch of [{saveVersion:5},{packedTerrain:undefined},{revision:1},{requiredPacks:[]},{map:{...fixture.map,width:2049}}])assert(!validate({...fixture,...patch}));
  const empty={...fixture,spatial:{nextEntityId:'7',entities:[]},packedTerrain:{...fixture.packedTerrain,occupantCount:0,occupants:'',definitions:[],cells:btoa(String.fromCharCode(...Array(9).fill(1))) }};
  assert(validate(empty));
});
Deno.test('terrain upserts keep realized forest edits and forbid reused identities or unversioned payload changes',async()=>{
  const before=await read();
  const delta=parseMessage(await Deno.readTextFile(new URL('../../prototype/protocol/fixtures/delta-terrain-update.json',import.meta.url))) as Delta;
  const next=applyDelta(before,delta);assert(next);
  equal(next.terrain!.occupants.find(o=>o.entityId==='2')!.forest!.trees,[{x:17,y:9,sprite:1}]);
  equal(next.terrain!.nextEntityId,'8');assert(!next.entities!.some(e=>e.id==='3'));assert(next.entities!.some(e=>e.id==='7'));
  for(const update of [
    {...delta,entities:delta.entities!.filter(e=>e.id!=='2')},
    {...delta,entities:delta.entities!.map(e=>e.id==='2'?{...e,revision:'0'}:e)},
    {...delta,entities:delta.entities!.map(e=>e.id==='7'?{...e,id:'6'}:e),terrain:{...delta.terrain!,occupants:delta.terrain!.occupants.map(o=>o.entityId==='7'?{...o,entityId:'6'}:o)}},
  ])assert(!applyDelta(before,update as Delta));
  const coalesced={...delta,entities:delta.entities!.map(e=>e.id==='2'?{...e,revision:'2'}:e),revision:'8',terrain:{...delta.terrain!,geometryRevision:'6'}};
  assert(applyDelta(before,coalesced as Delta));
});

Deno.test('corner legality previews require bounded quarter heights, exact decimal fences and known diagnostics',async()=>{
  const {parseCornerPreview}=await import('../src/lib/protocol.ts');
  const result={worldEpoch:'9007199254740993',geometryRevision:'17',position:{x:3,y:3,z:1},action:'raise',legal:true,reason:null,cost:'0',skipped:[],
    changes:[{position:{x:3,y:3,z:1},beforeGround:1,afterGround:1,beforeNE:0,beforeSE:0,beforeSW:0,beforeNW:0,afterNE:1,afterSE:0,afterSW:0,afterNW:0,remove:false,losesForest:false,losesOwnership:false}]};
  equal(parseCornerPreview(result),result);
  rejects(()=>parseCornerPreview({...result,worldEpoch:9007199254740993}));
  rejects(()=>parseCornerPreview({...result,reason:'silentlyReclaim'}));
  rejects(()=>parseCornerPreview({...result,cost:'1'}));
  rejects(()=>parseCornerPreview({...result,legal:false}));
  rejects(()=>parseCornerPreview({...result,changes:[]}));
  rejects(()=>parseCornerPreview({...result,changes:[{...result.changes[0],afterNE:5}]}));
  rejects(()=>parseCornerPreview({...result,changes:Array(257).fill(result.changes[0])}));
});
Deno.test('corner preview response from replaced connection or changed geometry never reaches the current draft',async()=>{
  const {createFixtureService}=await import('../src/lib/session-client.ts');
  const {createShellModel}=await import('../src/lib/shell-model.ts');
  const service=createFixtureService();let complete:(result:any)=>void=()=>{};
  service.publish(await read());
  const model=createShellModel(()=>({...service.connect(),previewCorner:()=>new Promise(resolve=>{complete=resolve;})}),'en',1);
  const until=async(test:()=>boolean)=>{while(!test())await new Promise(r=>setTimeout(r,1));};
  try{
    await until(()=>model.state.connection==='ready');
    const query={expectedWorldEpoch:model.state.read!.worldEpoch,expectedGeometryRevision:model.state.read!.terrain!.geometryRevision,position:{x:1,y:1,z:1},action:'raise' as const,offsetX:null,offsetY:null,stroke:null};
    const pending=model.previewCorner(query,new AbortController().signal).then(()=>false,()=>true);
    const next=structuredClone(model.state.read) as Snapshot;next.terrain!.geometryRevision=String(BigInt(next.terrain!.geometryRevision)+1n);next.revision=String(BigInt(next.revision)+1n);service.publish(next);
    complete({});assert(await pending);
    const second=model.previewCorner({...query,expectedGeometryRevision:next.terrain!.geometryRevision},new AbortController().signal).then(()=>false,()=>true);
    model.reconnect();await until(()=>model.state.connection==='ready');complete({});assert(await second);
  }finally{model.dispose();}
});
Deno.test('terrain editing uses the source ground ray through water and decorations, respects height cuts and rejects missing ground',async()=>{
  const {pickTerrainGround}=await import('../src/lib/terrain-edit.ts');
  const {WorldSpace}=await import('../src/lib/map-geometry.ts');
  const s=await read(),space=new WorldSpace(s.map.width,s.map.height,s.map.levels);
  // A flat synthetic grid keeps this oracle independent of drawing and its sprites.
  s.terrain!.cells=s.terrain!.cells.map(()=>({ground:1,surface:'natural'}));s.terrain!.waterLevel=3;
  const position=space.location({h:1,v:1,z:1}),q=space.center(position);
  equal(pickTerrainGround(s,q,s.map.levels-1),position);
  assert(!pickTerrainGround(s,q,0));
  assert(!pickTerrainGround(s,{x:-99999,y:-99999},s.map.levels-1));
});
