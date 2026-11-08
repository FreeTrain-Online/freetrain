import assert from 'node:assert/strict';
import {stationScene,addRail} from './helpers/station-scene.ts';
import {widePlatformAt,widePlatformQuery,widePlatformGhost} from '../src/lib/wide-platform-edit.ts';
import {parseWidePlatformPreview} from '../src/lib/protocol.ts';
import {WorldSpace,offset} from '../src/lib/map-geometry.ts';
import {RailwayRenderPlan,pickRailwayLocation} from '../src/lib/railway-renderer.ts';
import {StationRenderPlan} from '../src/lib/station-renderer.ts';
import {validStations} from '../src/lib/station-state.ts';
import {TerrainScene} from '../src/lib/terrain-renderer.ts';
import {RecipeImageCache,type RenderCatalog} from '../src/lib/content-recipes.ts';
import type {Heading,Snapshot,StationPlatform} from '../src/generated/contracts.ts';
import profile from '../src/generated/station-profile.json' with {type:'json'};
const catalog=JSON.parse(await Deno.readTextFile(new URL('../public/content/bundled-v2/catalog.json',import.meta.url))) as RenderCatalog;
const fixtures=JSON.parse(await Deno.readTextFile(new URL('../../doc/parity/station-fixtures.json',import.meta.url)));
const rows=fixtures.groups.wide.cases.filter((r:any)=>r.input.lanes[0]);
function platform(s:Snapshot,row:any){
 const {heading:direction,length,lanes}=row.input as {heading:Heading;length:number;lanes:boolean[]},[x,y,z]=row.input.base;
 const m=s.mixed!,space=new WorldSpace(s.map.width,s.map.height,s.map.levels),location={x,y,z},id=m.nextEntityId,d=offset(direction);
 m.nextEntityId=String(BigInt(id)+1n);
 const p:StationPlatform={id,revision:'0',kind:'wide',location,direction,length,name:'PF 1',roof:false,lane0:true,lane1:lanes[1],bell:profile.bells[0],hostId:null,children:[],yards:[],dayTimer:null};
 const definition=(id:string)=>({packId:s.requiredPacks[0].id,contributionId:id,recipeId:id});
 s.entities!.push({id,revision:'0',anchor:location,definition:definition(`system:fat-${direction/2%2}`),footprint:Array.from({length},(_,i)=>({offset:{x:d.x*i,y:d.y*i,z:0},transparent:false})),owned:true,silentlyReclaimable:false,value:'0'});
 for(let lane=0;lane<2;lane++)for(let i=0;lanes[lane]&&i<length;i++){
  const [x,y,z]=row.expected.lanes[lane][i],position={x,y,z},storage=space.storage(position),owner=addRail(s,storage.h,storage.v),v=m.voxels.at(-1)!;
  const pattern=direction%4===0?10:4;v.traffic!.rail!.pattern=pattern;v.traffic!.rail!.definition=definition(`system:pattern/${pattern}`);v.traffic!.platform=definition('system:yard');
  p.yards.push({trafficOwnerId:owner,location:position,lane,index:i,roof:false,outlook:{kind:'plain',material:'concrete',bridge:false,upward:false}});
 }
 m.stationGraph!.platforms.push(p);return p;
}
const images=new RecipeImageCache(async media=>{const b=Object.values(catalog.surfaceSets).flatMap(Object.values).find(b=>b.image.url===media.url)!;return {image:{} as CanvasImageSource,width:b.width,height:b.height};});
Deno.test('eight independent wide geometries resolve center and lane identity, exact ghost recipes and validated canonical ownership',()=>{
 assert.equal(rows.length,8);
 for(const row of rows){
  const s=stationScene(64,24),p=platform(s,row);assert(validStations(s));
  const positions=[...row.expected.center,...row.expected.lanes.flat()].map(([x,y,z]:number[])=>({x,y,z}));
  for(const at of positions){assert.equal(widePlatformAt(s,at)?.id,p.id);const q=widePlatformQuery(s,at,p.direction,p.length,p.lane1,true)!;assert.equal(q.platformId,p.id);assert.equal(q.expectedPlatformRevision,p.revision);}
  const q=widePlatformQuery(s,p.location,p.direction,p.length,p.lane1)!;
  const preview={query:q,legal:true,reason:null,cost:'2000000',positions};assert.deepEqual(parseWidePlatformPreview(preview),preview);
  assert.deepEqual(parseWidePlatformPreview({...preview,positions:positions.map(p=>({z:p.z,y:p.y,x:p.x}))}).positions,positions);
  assert.deepEqual(widePlatformGhost(q,positions).map(g=>g.recipeId),[...Array(4).fill(`system:fat-${p.direction/2%2}`),...Array(p.yards.length).fill(`system:pattern/${p.direction%4===0?10:4}`)]);
  for(const length of [0,-1,101,NaN,1.1])assert.equal(widePlatformQuery(s,p.location,p.direction,length,false),undefined);
  assert.equal(widePlatformQuery(s,p.location,1,4,false),undefined);
  for(const bad of [{...preview,cost:'-1'},{...preview,positions:[]},{...preview,positions:positions.slice(4)},{...preview,positions:[...positions].reverse()},{...preview,positions:[...positions,positions[0]]},{...preview,legal:false},{...preview,query:{...q,secondLane:!p.lane1}},{...preview,query:{...q,platformId:'1'}}])assert.throws(()=>parseWidePlatformPreview(bad));
  const broken=structuredClone(s);broken.mixed!.stationGraph!.platforms[0].yards[0].lane=1;assert(!validStations(broken));
 }
});
Deno.test('wide center source rectangles anchors cuts and opaque picks work in every orientation and eight variants',async()=>{
 for(const row of rows)for(const season of ['spring','summer','autumn','winter'] as const)for(const night of [false,true]){
  const s=stationScene(64,24),p=platform(s,row),before=JSON.stringify(s),variant={season,night},plan=new StationRenderPlan(s,catalog,variant),space=new WorldSpace(64,24,8);
  const cell=space.storage(p.location),index=cell.v*64+cell.h,ops=plan.operations(index,2,0,false);
  assert.equal(ops.length,1);assert('layer' in ops[0]);assert.deepEqual(ops[0].layer.frame.rectangle,row.expected.rectangle);assert.deepEqual(ops[0].layer.frame.anchor,row.expected.anchor);
  assert.equal(ops[0].target.kind,'railway');assert.equal(ops[0].target.id,p.id);assert.equal(plan.operations(index,1,0,false).length,0);assert.equal(plan.operations(index,7,0,true).length,1);
  assert.equal(plan.operations(index,7,3,true).length,0);assert(plan.contains(p.location));
  const scene=await TerrainScene.prepare(s,catalog,images,variant,2);let picked=false;
  for(const op of scene.operations)if(op.target.kind==='railway'&&op.target.id===p.id&&'layer'in op){
   const l=op.layer,[x,y,z]=l.position,[ax,ay]=l.frame.anchor!,mask=catalog.masks[l.frame.masks![`${season}/${night?'night':'day'}`]];
   for(let b=0;b<mask.length&&!picked;b++)for(let a=0;a<mask[b].length&&!picked;a++)if(mask[b][a]==='1'){
    const hit=scene.pick({x:16*(x+y)-ax+a+.5,y:8*(y-x)-16*z-ay+b+.5},{x:0,y:0,zoom:1});picked=hit?.kind==='railway'&&hit.component==='platform'&&hit.platformId===p.id;
   }
  }
  assert(picked);assert.equal(JSON.stringify(s),before);
 }
});
Deno.test('wide yard chip precedes normal rail and real fleet car art in all four directions',async()=>{
 const {trainSceneFixture}=await import('./helpers/train-scene.ts');
 const source=JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/fixtures/snapshot-fleet.json',import.meta.url))) as Snapshot;
 const stock=catalog.renderRecipes.find(r=>r.handler==='train-composition')!;
 for(const direction of [0,2,4,6] as const){
  const s=trainSceneFixture(source,catalog,stock),m=s.mixed!,v=m.voxels[1],car=m.fleet!.trains[0].cars[1];car.state.heading=direction;const pattern=direction%4===0?10:4;
  v.traffic!.rail!.pattern=pattern;v.traffic!.rail!.definition={packId:catalog.packId,contributionId:`system:pattern/${pattern}`,recipeId:`system:pattern/${pattern}`};v.traffic!.platform={packId:catalog.packId,contributionId:'system:yard',recipeId:'system:yard'};
  const d=offset((direction+2)%8),location={x:v.position.x-d.x,y:v.position.y-d.y,z:v.position.z};
  m.stationGraph={nextStationName:1,nextPlatformName:2,buildings:[],platforms:[{id:'31',revision:'0',kind:'wide',location,direction,length:1,name:'PF 1',roof:false,lane0:true,lane1:false,bell:profile.bells[0],hostId:null,children:[],dayTimer:null,yards:[{trafficOwnerId:v.ownerId,location:v.position,lane:0,index:0,roof:false,outlook:{kind:'plain',material:'concrete',bridge:false,upward:false}}]}],passages:[],listeners:[],listenerMemberships:[]};
  const space=new WorldSpace(s.map.width,s.map.height,s.map.levels),cell=space.storage(v.position),plan=new RailwayRenderPlan(s,catalog,{season:'spring',night:false});
  const ops=plan.operations(cell.v*space.horizontalSize+cell.h,5,0,true,2),carIndex=ops.findIndex(o=>o.target.kind==='train'),yard=ops.findIndex(o=>o.target.kind==='railway'&&o.target.component==='platform'),rail=ops.findIndex(o=>o.target.kind==='railway'&&o.target.component==='rail');
  assert(yard>=0&&rail>yard&&carIndex>rail);assert('layer'in ops[yard]);assert.deepEqual(ops[yard].layer.frame.rectangle,[0,0,32,16]);assert.deepEqual(ops[yard].layer.frame.anchor,[0,0]);
 }
});
Deno.test('wide automatic targeting evaluates the first lane and returns the center at the actual elevated rail level',()=>{
 for(const row of rows){const s=stationScene(64,24),p=platform(s,row),space=new WorldSpace(64,24,8);
  // Raise the scene without changing its horizontal geometry.
  for(const v of s.mixed!.voxels)v.position.z=4;
  const location={...p.location,z:4},point=space.center(location);
  assert.deepEqual(pickRailwayLocation(s,point,7,{kind:'widePlatform',direction:p.direction}),location);
  for(const v of s.mixed!.voxels)v.traffic!.rail!.pattern=p.direction%4===0?4:10;
  assert.notEqual(pickRailwayLocation(s,point,7,{kind:'widePlatform',direction:p.direction})?.z,4);
 }
});
Deno.test('center rebuild identity changes invalidate every retained center column',()=>{
 const s=stationScene(64,24),p=platform(s,rows[0]),before=new StationRenderPlan(s,catalog,{season:'spring',night:false}),next=structuredClone(s);
 next.mixed!.stationGraph!.platforms[0].id='999';next.mixed!.nextEntityId='1000';const after=new StationRenderPlan(next,catalog,{season:'spring',night:false});assert.equal(after.changedColumns(before).length,p.length);
});
