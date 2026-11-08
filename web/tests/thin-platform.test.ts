import assert from 'node:assert/strict';
import {stationScene,addRail} from './helpers/station-scene.ts';
import {thinPlatformAt,thinPlatformQuery,thinPlatformGhost} from '../src/lib/thin-platform-edit.ts';
import {parseThinPlatformPreview} from '../src/lib/protocol.ts';
import {move,WorldSpace} from '../src/lib/map-geometry.ts';
import {RailwayRenderPlan} from '../src/lib/railway-renderer.ts';
import {TerrainScene} from '../src/lib/terrain-renderer.ts';
import {RecipeImageCache,type RenderCatalog} from '../src/lib/content-recipes.ts';
import type {Heading,Snapshot,StationPlatform} from '../src/generated/contracts.ts';
const catalog=JSON.parse(await Deno.readTextFile(new URL('../public/content/bundled-v2/catalog.json',import.meta.url))) as RenderCatalog;
function platform(s:Snapshot,direction:Heading,roof:boolean,length=4){
 const m=s.mixed!,space=new WorldSpace(s.map.width,s.map.height,s.map.levels),location={x:20,y:20,z:2},id=m.nextEntityId;
 m.nextEntityId=String(BigInt(id)+1n);
 const p:StationPlatform={id,revision:'0',kind:'thin',location,direction,length,name:'PF 1',roof,lane0:true,lane1:false,bell:{packId:s.requiredPacks[0].id,contributionId:'{0DA56511-F6A5-450d-A50A-2A98573BD9DE}',recipeId:'{0DA56511-F6A5-450d-A50A-2A98573BD9DE}'},hostId:null,children:[],yards:[],dayTimer:null};
 const bell=JSON.parse(Deno.readTextFileSync(new URL('../src/generated/station-profile.json',import.meta.url))).bells[0];p.bell=bell;
 let position=location;
 for(let i=0;i<length;i++){
  const storage=space.storage(position),owner=addRail(s,storage.h,storage.v),v=m.voxels.at(-1)!;
  const pattern=direction%4===0?10:4;v.traffic!.rail!.pattern=pattern;v.traffic!.rail!.definition={packId:s.requiredPacks[0].id,contributionId:`system:pattern/${pattern}`,recipeId:`system:pattern/${pattern}`};
  const hasRoof=roof&&i>=Math.trunc(length/4)&&i<length-Math.trunc(length/4);
  v.traffic!.platform={packId:s.requiredPacks[0].id,contributionId:`system:thin-${direction+(hasRoof?1:0)}`,recipeId:`system:thin-${direction+(hasRoof?1:0)}`};
  p.yards.push({trafficOwnerId:owner,location:position,lane:0,index:i,roof:hasRoof,outlook:{kind:'plain',material:'concrete',bridge:false,upward:false}});position=move(position,direction);
 }
 m.stationGraph!.platforms.push(p);return p;
}
Deno.test('platform query resolves every yard identity, validates native length bounds and gives exact roof-run ghosts',()=>{
 for(const direction of [0,2,4,6] as const)for(const roof of [false,true]){
  const s=stationScene(64,48),p=platform(s,direction,roof,8);
  for(const y of p.yards){assert.equal(thinPlatformAt(s,y.location)?.id,p.id);const q=thinPlatformQuery(s,y.location,direction,8,roof,true)!;assert.equal(q.platformId,p.id);assert.equal(q.expectedPlatformRevision,p.revision);}
  const q=thinPlatformQuery(s,p.location,direction,8,roof)!;
  assert.deepEqual(thinPlatformGhost(q,p.yards.map(y=>y.location)).map(g=>g.recipeId),p.yards.map(y=>`system:thin-${direction+(y.roof?1:0)}`));
  for(const length of [0,-1,101,NaN,1.1])assert.equal(thinPlatformQuery(s,p.location,direction,length,roof),undefined);
 }
});
Deno.test('platform preview rejects malformed fields, duplicate cells, inconsistent legality and negative costs',()=>{
 const s=stationScene(),q=thinPlatformQuery(s,{x:20,y:20,z:2},2,4,true)!;
 const p={query:q,legal:true,reason:null,cost:'2000000',positions:Array.from({length:4},(_,i)=>({...q.position,x:q.position.x+i}))};assert.deepEqual(parseThinPlatformPreview(p),p);
 for(const bad of [{...p,cost:'-1'},{...p,positions:[]},{...p,positions:[q.position,q.position]},{...p,legal:false},{...p,query:{...q,direction:1}},{...p,query:{...q,length:101}}])assert.throws(()=>parseThinPlatformPreview(bad));
});
Deno.test('literal thin sprites use exact source rectangles and before/after passes in all eight variants and every cut',async()=>{
 const images=new RecipeImageCache(async media=>{const b=Object.values(catalog.surfaceSets).flatMap(Object.values).find(b=>b.image.url===media.url)!;return {image:{} as CanvasImageSource,width:b.width,height:b.height};});
 for(const direction of [0,2,4,6] as const)for(const roof of [false,true])for(const season of ['spring','summer','autumn','winter'] as const)for(const night of [false,true]){
  const s=stationScene(64,48),p=platform(s,direction,roof,1),before=JSON.stringify(s),variant={season,night};
  const plan=new RailwayRenderPlan(s,catalog,variant),ops=plan.operations(new WorldSpace(64,48,8).storage(p.location).v*64+new WorldSpace(64,48,8).storage(p.location).h,7,0,true,2);
  const art=ops.find(o=>o.target.kind==='railway'&&o.target.component==='platform')!;assert('layer'in art);
  assert.deepEqual(art.layer.frame.rectangle,[32*(direction+Number(roof)),0,32,24]);assert.deepEqual(art.layer.frame.anchor,[0,8]);
  assert.equal(art.layer.frame.stage,direction===2||direction===4?20:40);assert.equal(art.target.platformId,p.id);
  const scene=await TerrainScene.prepare(s,catalog,images,variant,7);let picked=false;
  for(const op of scene.operations)if(op.target.kind==='railway'&&op.target.component==='platform'&&'layer'in op){
   const l=op.layer,[x,y,z]=l.position,[ax,ay]=l.frame.anchor!,mask=catalog.masks[l.frame.masks![`${season}/${night?'night':'day'}`]];
   for(let b=0;b<mask.length&&!picked;b++)for(let a=0;a<mask[b].length&&!picked;a++)if(mask[b][a]==='1'){
    const hit=scene.pick({x:16*(x+y)-ax+a+.5,y:8*(y-x)-16*z-ay+b+.5},{x:0,y:0,zoom:1});picked=hit?.kind==='railway'&&hit.component==='platform';
   }
  }
  assert(picked);const cell=new WorldSpace(64,48,8).storage(p.location);for(const cut of [0,1,2,3,7])assert.equal(plan.operations(cell.v*64+cell.h,cut,0,false,2).filter(o=>o.target.kind==='railway'&&o.target.component==='platform').length,cut<2?0:1);
  assert.equal(JSON.stringify(s),before);
 }
});

Deno.test('rebuilding a platform on retained Traffic updates picked platform identity and invalidates its columns',()=>{
 const s=stationScene(64,48),p=platform(s,2,true),before=new RailwayRenderPlan(s,catalog,{season:'spring',night:false});
 const next=structuredClone(s);next.mixed!.stationGraph!.platforms[0].id='999';next.mixed!.nextEntityId='1000';
 const after=new RailwayRenderPlan(next,catalog,{season:'spring',night:false});assert.equal(after.changedColumns(before).length,p.length);
});
Deno.test('all four thin outlooks preserve before/car/after ordering for real fleet car art',async()=>{
 const {trainSceneFixture}=await import('./helpers/train-scene.ts');
 const source=JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/fixtures/snapshot-fleet.json',import.meta.url))) as Snapshot;
 const stock=catalog.renderRecipes.find(r=>r.handler==='train-composition')!;
 for(const direction of [0,2,4,6] as const){
  const s=trainSceneFixture(source,catalog,stock),m=s.mixed!,v=m.voxels[1];
  // Art dispatch fixture: normalize this one car's heading/rail to the platform's axis.
  const car=m.fleet!.trains[0].cars[1];car.state.heading=direction;const pattern=direction%4===0?10:4;
  v.traffic!.rail!.pattern=pattern;v.traffic!.rail!.definition={packId:catalog.packId,contributionId:`system:pattern/${pattern}`,recipeId:`system:pattern/${pattern}`};
  v.traffic!.platform={packId:catalog.packId,contributionId:`system:thin-${direction+1}`,recipeId:`system:thin-${direction+1}`};
  m.stationGraph={nextStationName:1,nextPlatformName:2,buildings:[],platforms:[{id:'31',revision:'0',kind:'thin',location:v.position,direction,length:1,name:'PF 1',roof:true,lane0:true,lane1:false,bell:{packId:'unused',contributionId:'unused',recipeId:'unused'},hostId:null,children:[],dayTimer:null,yards:[{trafficOwnerId:v.ownerId,location:v.position,lane:0,index:0,roof:true,outlook:{kind:'plain',material:'concrete',bridge:false,upward:false}}]}],passages:[],listeners:[],listenerMemberships:[]};
  const space=new WorldSpace(s.map.width,s.map.height,s.map.levels),cell=space.storage(v.position),plan=new RailwayRenderPlan(s,catalog,{season:'spring',night:false});
  const ops=plan.operations(cell.v*space.horizontalSize+cell.h,5,0,true,2),train=ops.findIndex(o=>o.target.kind==='train'),platformIndex=ops.findIndex(o=>o.target.kind==='railway'&&o.target.component==='platform');
  assert(train>=0&&platformIndex>=0);assert(direction===2||direction===4?platformIndex<train:platformIndex>train);
 }
});
