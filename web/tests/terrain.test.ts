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
  const schema=JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/save-v8.schema.json',import.meta.url)));
  const fixture=JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/saves/terrain-v8.json',import.meta.url)));
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

Deno.test('rectangle repair preview accepts exact losses and explicit bounded repair diagnostics',async()=>{
  const {parseCornerPreview}=await import('../src/lib/protocol.ts');
  const result={worldEpoch:'3',geometryRevision:'17',position:{x:10,y:8,z:1},action:'repair',legal:true,reason:null,cost:'0',
    skipped:[{position:{x:11,y:8,z:1},reason:'inconsistentNeighbors'},{position:{x:11,y:9,z:1},reason:'unrepresentableHeight'}],
    changes:[{position:{x:10,y:8,z:1},beforeGround:1,afterGround:2,beforeNE:4,beforeSE:4,beforeSW:4,beforeNW:1,afterNE:0,afterSE:0,afterSW:0,afterNW:0,remove:true,losesForest:true,losesOwnership:true}]};
  equal(parseCornerPreview(result),result);
  equal(parseCornerPreview({...result,legal:false,reason:'noChange',changes:[]}).legal,false);
  rejects(()=>parseCornerPreview({...result,skipped:Array(65).fill(result.skipped[0])}));
  rejects(()=>parseCornerPreview({...result,action:'flatten'}));
});

Deno.test('terrace strategies target the mountain or backed-up terrace ray independently of ground and water',async()=>{
  const {pickTerrainStrategy}=await import('../src/lib/terrain-edit.ts');const {WorldSpace}=await import('../src/lib/map-geometry.ts');
  const s=await read(),space=new WorldSpace(s.map.width,s.map.height,s.map.levels),terrace=s.entities![0];
  const point=space.center(terrace.anchor);
  equal(pickTerrainStrategy(s,point,s.map.levels-1,'removeTerrace'),terrace.anchor);
  equal(pickTerrainStrategy(s,point,terrace.anchor.z,'removeTerrace'),terrace.anchor);
  assert(!pickTerrainStrategy(s,{x:-9999,y:-9999},s.map.levels-1,'placeTerrace'));
  const mountain=s.entities![1];equal(pickTerrainStrategy(s,space.center(mountain.anchor),s.map.levels-1,'placeCut'),mountain.anchor);
});
Deno.test('all four terrace strategies and their source diagnostics survive strict preview and command validation',async()=>{
  const {parseCornerPreview}=await import('../src/lib/protocol.ts');const {default:Ajv}=await import('ajv');
  const schema=JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/schema.json',import.meta.url)));
  const ajv=new Ajv({strict:true});
  for(const [name,unsigned] of [['int64',false],['uint63',true]] as const)ajv.addFormat(name,(v:string)=>/^(0|-?[1-9][0-9]*)$/.test(v)&&BigInt(v)>=(unsigned?0n:-9223372036854775808n)&&BigInt(v)<=9223372036854775807n);
  const validate=ajv.compile({...schema,$ref:'#/$defs/CornerTerrainQuery'});
  for(const action of ['placeTerrace','removeTerrace','placeCut','removeCut']){
    const query={expectedWorldEpoch:'1',expectedGeometryRevision:'0',position:{x:2,y:2,z:1},action,offsetX:null,offsetY:null,stroke:null};assert(validate(query));
    for(const reason of ['mountainRequired','terraceRequired','higherNeighbor'])equal(parseCornerPreview({worldEpoch:'1',geometryRevision:'0',position:query.position,action,legal:false,reason,cost:'0',changes:[],skipped:[]}).reason,reason);
  }
});
Deno.test('authoring previews accept exact realized scenery and source zero-tree outcomes, retaining strict field bounds',async()=>{
  const {parseCornerPreview}=await import('../src/lib/protocol.ts');
  const f=(await read()).terrain!.occupants.find(o=>o.forest)!.forest!;
  const p={x:20,y:20,z:1};const change={position:p,beforeGround:1,afterGround:1,beforeNE:0,beforeSE:0,beforeSW:0,beforeNW:0,afterNE:0,afterSE:0,afterSW:0,afterNW:0,remove:false,losesForest:false,losesOwnership:false};
  const preview={worldEpoch:'1',geometryRevision:'0',position:p,action:'plantForest',legal:true,reason:null,cost:'0',changes:[change],skipped:[],decorations:[{position:p,forest:f,creekConnections:0}]};
  equal(parseCornerPreview(preview),preview);
  equal(parseCornerPreview({...preview,changes:[],decorations:[]}).changes,[]);
  equal(parseCornerPreview({...preview,action:'placeCreek',decorations:[{position:p,forest:null,creekConnections:15}]}).action,'placeCreek');
  for(const patch of [{cost:'100'},{decorations:[{position:p,forest:null,creekConnections:16}]},{decorations:Array(65).fill(preview.decorations[0])},{action:'placeCreek',changes:[]},{action:'raise',changes:[]}])rejects(()=>parseCornerPreview({...preview,...patch}));
});
Deno.test('authoring mode uses a fenced boolean command and definitions are explicit prepared identities',async()=>{
  const {validWireShape}=await import('../src/lib/protocol.ts');
  const command={type:'command',requestId:'mode',expectedWorldEpoch:'1',payload:{kind:'setAuthoringMode',expectedGeometryRevision:'0',enabled:true}};
  assert(validWireShape(command));
  for(const patch of [{enabled:'true'},{expectedGeometryRevision:'-1'},{unlimitedMoney:true}])assert(!validWireShape({...command,payload:{...command.payload,...patch}}));
});

Deno.test('incremental terrain validation equals independent full validation, preserves predecessor and rejects local graph corruption',async()=>{
 const {validTerrain}=await import('../src/lib/terrain-state.ts');
 const before=immutable(await read()),delta=parseMessage(await Deno.readTextFile(new URL('../../prototype/protocol/fixtures/delta-terrain-update.json',import.meta.url))) as Delta;
 const saved=JSON.stringify(before),next=applyDelta(before,delta);assert(next);
 assert(validTerrain(structuredClone(next) as Snapshot));equal(JSON.stringify(before),saved);
 for(const mutate of [
  (d:Delta)=>{d.entities![0].footprint[0].transparent=!d.entities![0].footprint[0].transparent;},
  (d:Delta)=>{d.entities![0].anchor.z=5;},
  (d:Delta)=>{d.entities![0].definition.recipeId='missing';},
  (d:Delta)=>{d.terrain!.occupants[0].forest!.trees[0].sprite=99;},
  (d:Delta)=>{d.terrain!.cells=[{index:0,ground:0,surface:'natural'}];},
 ]){const bad=structuredClone(delta);mutate(bad);assert(!applyDelta(before,bad));equal(JSON.stringify(before),saved);}
});

Deno.test('local redraw certificates include geometry neighbors, survive clock-only reads and reject global or unvalidated changes',async()=>{
 const {terrainChangedColumns}=await import('../src/lib/terrain-state.ts');
 const before=immutable(await read()),delta=parseMessage(await Deno.readTextFile(new URL('../../prototype/protocol/fixtures/delta-terrain-update.json',import.meta.url))) as Delta;
 const next=applyDelta(before,delta) as Snapshot;assert(next);
 const columns=terrainChangedColumns(before,next);assert(columns&&columns.length>0&&columns.length<=9);
 equal(terrainChangedColumns(next,next),[]);
 const clock={...delta,baseRevision:next.revision,revision:String(BigInt(next.revision)+1n),entities:[],removed:{...delta.removed,entityIds:[]},
  terrain:{...next.terrain!,cells:[],occupants:[]}} as Delta;
 const timed=applyDelta(next,clock) as Snapshot;assert(timed);equal(terrainChangedColumns(before,timed),columns);
 const global=applyDelta(before,await change()) as Snapshot;assert(global);assert(terrainChangedColumns(before,global)===undefined);
 assert(terrainChangedColumns(before,immutable(structuredClone(next)))===undefined);
});
