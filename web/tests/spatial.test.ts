import vectors from '../../prototype/scenarios/spatial-vectors.json' with { type:'json' };
import fixture from '../../prototype/protocol/fixtures/snapshot.json' with { type:'json' };
import type { Snapshot, SpatialQuery } from '../src/generated/contracts.ts';
import { WorldSpace, offset, rotate, angle, directionTo, move, distance, snap } from '../src/lib/map-geometry.ts';
import { localSpatialPreview, matchesSpatialPreview } from '../src/lib/spatial-preview.ts';
import { tileAt, toScreen, zoomAt } from '../src/lib/map-display.ts';
import { mapSpace } from '../src/lib/map-space.ts';
import { parseMessage, applyDelta } from '../src/lib/protocol.ts';
import removal from '../../prototype/protocol/fixtures/delta-removal.json' with { type:'json' };
function assert(value:unknown,message='Assertion failed'):asserts value {if(!value)throw new Error(message);}
function equal(a:unknown,b:unknown) {assert(JSON.stringify(a)===JSON.stringify(b),`${JSON.stringify(a)} != ${JSON.stringify(b)}`);}
function throws(action:()=>unknown) {let failed=false;try{action();}catch{failed=true;}assert(failed);}
for(const row of vectors.cases)Deno.test(`shared authoritative spatial vector: ${row.id}`,()=>{
  const read={...structuredClone(fixture),map:row.map} as Snapshot;
  const actual=localSpatialPreview(read,row.query as SpatialQuery);
  for(const key of ['position','inside','projected','storage','heading','distance'] as const)equal(actual[key],row.expected[key]);
});
Deno.test('eight headings retain canonical indices, rotations, angles and diagonal steps',()=>{
  const rows=[[0,0,-1,7,1,6,2,4],[1,1,-1,0,2,7,3,5],[2,1,0,1,3,0,4,6],[3,1,1,2,4,1,5,7],
    [4,0,1,3,5,2,6,0],[5,-1,1,4,6,3,7,1],[6,-1,0,5,7,4,0,2],[7,-1,-1,6,0,5,1,3]];
  for(const [index,x,y,left,right,left90,right90,opposite] of rows){
    equal(offset(index),{x,y});equal([rotate(index,-1),rotate(index,1),rotate(index,-2),rotate(index,2),rotate(index,4)],[left,right,left90,right90,opposite]);
    equal(move({x:10,y:20,z:3},index),{x:10+x,y:20+y,z:3});
    equal(directionTo({x:10,y:20,z:3},{x:10+x*5,y:20+y*8,z:3}),index);
    equal(angle(index,opposite),4);equal(angle(index,left),1);
  }
  for(const h of [-1,8,1.5,NaN])throws(()=>offset(h));
});
Deno.test('H/V projection and source picking round trip at zoom, pan, elevation and negative coordinates',()=>{
  const space=new WorldSpace(4,8,3),camera={x:37,y:-52,zoom:1.7};
  for(const p of [...space.ground(),{x:-3,y:7,z:2},{x:3,y:0,z:2}]){
    const screen=toScreen(p,camera,1000,600,space);
    equal(tileAt(screen,p.z,camera,1000,600,space),p);
    equal(tileAt(screen,p.z,zoomAt(camera,1.25,screen,1000,600),1000,600,space),p);
  }
  equal(space.pick({x:16,y:8},0),{x:3,y:0,z:0});
  equal(space.pick({x:-.01,y:8},0),{x:2,y:-1,z:0});
  equal(space.storage({x:0,y:0,z:0}),{h:-2,v:3,z:0});
  equal(space.location({h:0,v:-1,z:0}),{x:3,y:0,z:0});
  assert(space.containsHorizontal({x:3,y:0,z:3})&&!space.contains({x:3,y:0,z:3}));
  equal(new WorldSpace(4,9,3).project({x:4,y:0,z:0}),{x:0,y:0});
});
Deno.test('source snapping thresholds and checked Int32 failure boundaries are explicit',()=>{
  const origin={x:0,y:0,z:0};equal(snap({x:2,y:5,z:0},origin,8),{x:2,y:2,z:0});equal(snap({x:2,y:6,z:0},origin,8),{x:0,y:6,z:0});
  equal(snap({x:3,y:3,z:0},origin,4),{x:3,y:0,z:0});equal(distance(origin,{x:1,y:1,z:0}),1);equal(distance(origin,{x:0,y:0,z:3}),3);
  throws(()=>distance(origin,{x:46341,y:0,z:0}));throws(()=>move({x:2147483647,y:0,z:0},2));
  throws(()=>new WorldSpace(4,8,3).project({x:2147483647,y:0,z:1}));throws(()=>directionTo(origin,origin));
  throws(()=>snap({x:1,y:1,z:1},origin,4));
});
Deno.test('preview results must match epoch, identity, snapped target, projection, bounds and revision',()=>{
  const query:SpatialQuery={expectedWorldEpoch:'1',position:{x:12,y:25,z:1},anchor:{x:10,y:20,z:1},snap:8};
  const local=localSpatialPreview(fixture as Snapshot,query);
  assert(matchesSpatialPreview(local,{...local,revision:'1'}));
  for(const patch of [{worldEpoch:'2'},{ruleset:'other'},{inside:false},{position:{x:12,y:23,z:1}},{heading:7},{projected:{a:0,b:0}},{requiredPacks:[]},{revision:'-1'}])assert(!matchesSpatialPreview(local,{...local,...patch}));
});
Deno.test('wire profiles and individual car headings cannot silently change meaning',()=>{
  for(const patch of [{ruleset:'other'},{requiredPacks:[]},{map:{...fixture.map,coordinateSystem:'hv'}},{map:{...fixture.map,width:128}},{trains:[{...fixture.trains[0],heading:8}]},{trains:[{...fixture.trains[0],heading:1.5}]},{trains:[{...fixture.trains[0],heading:3,carHeadings:[3,2,2]}]},{trains:[{...fixture.trains[0],carHeadings:[2]}]}])throws(()=>parseMessage(JSON.stringify({...fixture,...patch})));
  for(const patch of [{ruleset:'other'},{contentVersion:'other'},{requiredPacks:[]}])assert(applyDelta(fixture as Snapshot,{...removal,...patch} as never)===undefined);
});
Deno.test('renderer consumes server car headings and canonical H/V terrain without using prototype topology',async()=>{
  const {MapRenderer}=await import('../src/lib/map-renderer.ts');
  const read={...structuredClone(fixture),ruleset:'spatial-source-fixture',map:{width:4,height:8,levels:3,coordinateSystem:'hv'},tracks:[],stations:[],buildings:[],trains:Array.from({length:8},(_,h)=>({id:`h${h}`,name:'fixture',revision:'0',position:{x:3,y:1,z:1},cars:1,controller:'automatic',heading:h,carHeadings:[h],carPositions:[{x:3,y:1,z:1}],state:'moving',controlLease:null}))} as Snapshot;
  const ctx=new Proxy({}, {get:()=>()=>{}}),canvas={width:800,height:600,dataset:{} as Record<string,string>,getContext:()=>ctx} as unknown as HTMLCanvasElement;
  const renderer=new MapRenderer(canvas,new Map()),space=mapSpace(read.map),camera={...space.center({x:3,y:1,z:1}),zoom:1};
  renderer.setRead(read);renderer.draw(camera,800,600,1,{heightCut:2,level:1,night:false,winter:false,recolor:false,shape:'buffer',remove:false});
  equal(canvas.dataset.headings,'0,1,2,3,4,5,6,7');equal(canvas.dataset.coordinateSystem,'hv');
  assert(renderer.pick({x:400,y:300},1)?.kind==='train');
});

Deno.test('shared canonical live fixture validates, renders and picks every source rail pattern',async()=>{
  const read=JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/fixtures/snapshot-canonical.json',import.meta.url))) as Snapshot;
  equal(parseMessage(JSON.stringify(read)),read);
  const {normalPorts}=await import('../src/lib/world-identity.ts');
  const catalog=await Deno.readFile(new URL('../../src/FreeTrain.Online.Content/reference-spatial.json',import.meta.url));
  const hash=[...new Uint8Array(await crypto.subtle.digest('SHA-256',catalog))].map(b=>b.toString(16).padStart(2,'0')).join('');
  equal(hash,read.requiredPacks[0].catalogHash);
  const {MapRenderer}=await import('../src/lib/map-renderer.ts');
  const ctx=new Proxy({}, {get:()=>()=>{}}),canvas={width:1280,height:900,dataset:{},getContext:()=>ctx} as unknown as HTMLCanvasElement;
  const renderer=new MapRenderer(canvas,new Map()),space=mapSpace(read.map),camera={...space.center({x:31,y:32,z:0}),zoom:1};
  renderer.setRead(read);renderer.draw(camera,1280,900,1,{heightCut:7,level:0,night:false,winter:false,recolor:false,shape:'buffer',remove:false});
  const directions=new Set<number>();
  for(const track of read.tracks){
    const p=toScreen(track.position,camera,1280,900,space);
    equal(renderer.pick(p,1)?.position,track.position);
    equal(tileAt(p,0,camera,1280,900,space),track.position);
    for(const h of normalPorts(track.shape)!)directions.add(h);
  }
  equal(directions.size,8);
  for(const mutate of [
    (r:Snapshot)=>r.tracks.pop(),
    (r:Snapshot)=>r.tracks.push(structuredClone(r.tracks[0])),
    (r:Snapshot)=>{r.tracks[0].shape='eastWest';},
    (r:Snapshot)=>{r.tick='1128717404863200';},
    (r:Snapshot)=>{r.tracks[0].position.x=2147483647;},
    (r:Snapshot)=>{r.map.coordinateSystem='prototypeXY';},
  ]){const broken=structuredClone(read);mutate(broken);throws(()=>parseMessage(JSON.stringify(broken)));}
});
Deno.test('canonical source-minute display uses fixed years and ten-minute precision in both locales',async()=>{
  const {gameDate,formatGameTime,maximumGameMinute}=await import('../src/lib/game-time.ts');
  equal(gameDate('130080'),{year:1,month:4,day:1,weekday:6,hour:8,minute:0});
  equal(formatGameTime('130089','en'),'Year 1 · 4/1 (Sat) 08:00');
  equal(formatGameTime('130090','ja'),'1年4月1日(土) 08:10');
  equal(gameDate('1661760'),{year:4,month:3,day:1,weekday:6,hour:0,minute:0});
  equal(gameDate(String(maximumGameMinute)).year,2147483647);
  throws(()=>gameDate('-1'));throws(()=>gameDate(String(maximumGameMinute+1n)));
});
Deno.test('current save carries canonical clock state and strict source action binding boundary',async()=>{
  const {default:Ajv}=await import('ajv');
  const schema=JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/save-v10.schema.json',import.meta.url)));
  const save=JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/saves/reference-v10.json',import.meta.url)));
  const validate=new Ajv({strict:true}).compile(schema);
  assert(validate(save));
  for(const clock of [null,{...save.clock,absoluteMinute:130080},{...save.clock,absoluteMinute:'-1'},
    {...save.clock,pending:[{handlerId:'unknown'}]},{...save.clock,extra:0}])assert(!validate({...save,clock}));
});
Deno.test('canonical clock deltas retain geometry and reject unsupported clock range and old profile',async()=>{
  const read=JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/fixtures/snapshot-canonical.json',import.meta.url))) as Snapshot;
  const delta={type:'delta',worldEpoch:read.worldEpoch,baseRevision:'0',revision:'2',tick:'130081',paused:false,cash:'0',
    trains:[],stations:[],buildings:[],tracks:[],removed:{trainIds:[],stationIds:[],buildingIds:[],trackPositions:[],entityIds:[]},entities:[],
    ruleset:read.ruleset,contentVersion:read.contentVersion,requiredPacks:read.requiredPacks} as const;
  const next=applyDelta(read,delta as never);assert(next);equal(next.tick,'130081');equal(next.paused,false);equal(next.tracks,read.tracks);
  assert(!applyDelta(read,{...delta,tick:'1128717404863200'} as never));
  throws(()=>parseMessage(JSON.stringify({...read,ruleset:'reference-spatial-1'})));
  throws(()=>parseMessage(JSON.stringify({...read,ruleset:'reference-spatial-2'})));
});

Deno.test('Save 9 freezes complete random state and rejects malformed or incompatible streams',async()=>{
  const {default:Ajv}=await import('ajv');
  const schema=JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/save-v10.schema.json',import.meta.url)));
  const save=JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/saves/reference-v10.json',import.meta.url)));
  const validate=new Ajv({strict:true}).compile(schema);
  assert(validate(save));
  equal(save.ruleset,'reference-spatial-4');equal(save.random.streams.length,10);
  for(const mutate of [
    (s:typeof save)=>{delete s.random;},
    (s:typeof save)=>{s.random=null;},
    (s:typeof save)=>{s.random.algorithm='future';},
    (s:typeof save)=>{s.random.version=2;},
    (s:typeof save)=>{s.random.seed='0';},
    (s:typeof save)=>{s.random.streams.pop();},
    (s:typeof save)=>{s.random.streams[1].id=s.random.streams[0].id;},
    (s:typeof save)=>{s.random.streams[0].id='unknown';},
    (s:typeof save)=>{s.random.streams[0].state.index=55;},
    (s:typeof save)=>{s.random.streams[0].state.index='0';},
    (s:typeof save)=>{s.random.streams[0].state.values.pop();},
    (s:typeof save)=>{s.random.streams[0].state.values[0]=-1;},
    (s:typeof save)=>{s.random.streams[0].state.values[0]=2147483647;},
    (s:typeof save)=>{s.random.streams[0].state.values.fill(0);},
    (s:typeof save)=>{s.random.streams[0].state.extra=0;},
  ]){const broken=structuredClone(save);mutate(broken);assert(!validate(broken));}
});

Deno.test('canonical stable owners, footprints and exact definition identities reject corrupt graphs before reveal',async()=>{
  const source=JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/fixtures/snapshot-canonical.json',import.meta.url))) as Snapshot;
  for(const mutate of [
    (r:Snapshot)=>{delete r.entities;},
    (r:Snapshot)=>{delete r.tracks[0].entityId;},
    (r:Snapshot)=>{r.tracks[0].entityId='999';},
    (r:Snapshot)=>{r.tracks[0].entityId=r.tracks[1].entityId;},
    (r:Snapshot)=>{r.entities![1].id=r.entities![0].id;},
    (r:Snapshot)=>{r.entities!.pop();},
    (r:Snapshot)=>{r.entities![0].definition.packId='optional';},
    (r:Snapshot)=>{r.entities![0].definition.contributionId='Normal-rail';},
    (r:Snapshot)=>{r.entities![0].definition.recipeId='normal12';},
    (r:Snapshot)=>{r.entities![0].anchor.z=1;},
    (r:Snapshot)=>{r.entities![0].footprint[0].transparent=false;},
    (r:Snapshot)=>{r.entities![0].footprint[0].offset.z=1;},
    (r:Snapshot)=>{r.entities![0].footprint.push(structuredClone(r.entities![0].footprint[0]));},
    (r:Snapshot)=>{r.entities![0].owned=false;},
    (r:Snapshot)=>{r.entities![0].silentlyReclaimable=true;},
    (r:Snapshot)=>{r.requiredPacks.push(structuredClone(r.requiredPacks[0]));},
    (r:Snapshot)=>{r.requiredPacks[0].version='1';},
    (r:Snapshot)=>{r.ruleset='reference-spatial-3';},
  ]){const broken=structuredClone(source);mutate(broken);throws(()=>parseMessage(JSON.stringify(broken)));}
});
Deno.test('entity delta removals and replacements preserve decimal IDs and reject dangling or mixed owners',async()=>{
  const read=JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/fixtures/snapshot-canonical.json',import.meta.url))) as Snapshot;
  const old=read.entities![0].id,newId='9007199254740993';
  const changed={...structuredClone(read.entities![0]),id:newId};
  const delta={type:'delta',worldEpoch:read.worldEpoch,baseRevision:read.revision,revision:'1',tick:read.tick,paused:read.paused,cash:read.cash,
    trains:[],stations:[],buildings:[],tracks:read.tracks.filter(t=>t.entityId===old).map(t=>({...t,entityId:newId})),entities:[changed],
    removed:{trainIds:[],stationIds:[],buildingIds:[],trackPositions:[],entityIds:[old]},ruleset:read.ruleset,contentVersion:read.contentVersion,requiredPacks:read.requiredPacks} as const;
  const parsed=parseMessage(JSON.stringify(delta));assert(parsed.type==='delta');
  const next=applyDelta(read,parsed);assert(next);assert(next.entities!.some(e=>e.id===newId));assert(!next.entities!.some(e=>e.id===old));
  equal(read.entities![0].id,old);
  for(const patch of [{entities:[]},{tracks:[]},{removed:{...delta.removed,entityIds:[]}},{entities:undefined},{entities:[changed,changed]},
    {requiredPacks:[...read.requiredPacks,...read.requiredPacks]}])assert(!applyDelta(read,{...delta,...patch} as never));
});
Deno.test('current save structurally requires stable graph and old canonical saves remain unsupported',async()=>{
  const {default:Ajv}=await import('ajv');
  const schema=JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/save-v10.schema.json',import.meta.url)));
  const save=JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/saves/reference-v10.json',import.meta.url)));
  const validate=new Ajv({strict:true}).compile(schema);assert(validate(save));
  for(const patch of [{spatial:null},{spatial:{...save.spatial,nextEntityId:33}},{saveVersion:4},{ruleset:'reference-spatial-3'},
    {requiredPacks:[...save.requiredPacks,...save.requiredPacks]}])assert(!validate({...save,...patch}));
});
