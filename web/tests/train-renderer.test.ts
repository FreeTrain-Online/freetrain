import {trainSceneFixture} from './helpers/train-scene.ts';
import assert from 'node:assert/strict';
import {trainCarLayer,carOutgoing} from '../src/lib/train-renderer.ts';
import {TerrainScene} from '../src/lib/terrain-renderer.ts';
import {RailwayRenderPlan} from '../src/lib/railway-renderer.ts';
import {RecipeImageCache,type RenderCatalog,type Variant} from '../src/lib/content-recipes.ts';
import {WorldSpace} from '../src/lib/map-geometry.ts';
import {validSnapshot} from '../src/lib/world-identity.ts';
import type {Snapshot,Definition} from '../src/generated/contracts.ts';
const catalog=JSON.parse(await Deno.readTextFile(new URL('../public/content/bundled-v2/catalog.json',import.meta.url))) as RenderCatalog;
const fixtures=JSON.parse(await Deno.readTextFile(new URL('../../doc/parity/train-fixtures.json',import.meta.url)));
const source=JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/fixtures/snapshot-fleet.json',import.meta.url))) as Snapshot;
const stocks=catalog.renderRecipes.filter(r=>r.handler==='train-composition');
const definition=(id:string):Definition=>({packId:catalog.packId,contributionId:id,recipeId:id});
const day:Variant={season:'spring',night:false};
const cache=(decoded=new Set<string>())=>new RecipeImageCache(async media=>{
 decoded.add(media.url);const b=Object.values(catalog.surfaceSets).flatMap(Object.values).find(b=>b.image.url===media.url)!;
 return {image:{} as CanvasImageSource,width:b.width,height:b.height};
});
const sceneFixture=(stock=stocks[0],reversed=false)=>trainSceneFixture(source,catalog,stock,reversed);
Deno.test('all 448 independent source car poses retain exact crops, sixteen angles, curve/ramp anchors and independent reversal',()=>{
 const recipes=new Map(catalog.renderRecipes.map(r=>[r.id,r]));
 for(const row of fixtures.groups.rendering.cases){
  const i=row.input,e=row.expected,recipe=recipes.get(i.recipe)!,before=JSON.stringify(recipe);
  const l=trainCarLayer(recipe,'car',[1,2,3],i.heading,i.isReversed,i.outgoing,i.kind==='slope'?{climb:i.climb,level:i.level}:undefined);
  assert.deepEqual(l.frame.rectangle,e.rectangle,row.id);assert.deepEqual(l.frame.anchor,e.offset.map((n:number)=>n===0?0:-n),row.id);
  assert.deepEqual(l.position,[1,2,3]);assert.equal(l.entityId,'car');assert.equal(JSON.stringify(recipe),before);
 }
});
Deno.test('every bundled formation and car including both tails/colors renders and alpha-picks stable train/car IDs in all eight variants',async()=>{
 const recipes=new Set<string>();
 for(const stock of stocks)for(const reversed of [false,true])for(const season of ['spring','summer','autumn','winter'] as const)for(const night of [false,true]){
  const s=sceneFixture(stock,reversed);assert(validSnapshot(s),stock.id);const before=JSON.stringify(s);
  const scene=await TerrainScene.prepare(s,catalog,cache(),{season,night},5),ops=scene.operations.filter(o=>o.target.kind==='train');
  assert.equal(ops.length,3);for(const op of ops){assert('layer' in op);assert(op.target.kind==='train');recipes.add(op.layer.recipeId);
   const [x,y,z]=op.layer.position,[ax,ay]=op.layer.frame.anchor!,mask=catalog.masks[op.layer.frame.masks![`${season}/${night?'night':'day'}`]];let hit=false;
   for(let b=0;b<32&&!hit;b++)for(let a=0;a<32&&!hit;a++)if(mask[b][a]==='1'){
    const p={x:16*(x+y)-ax+a+.5,y:8*(y-x)-16*z-ay+b+.5};const picked=scene.pick(p,{x:0,y:0,zoom:1});
    if(picked?.kind==='train'&&picked.carId===op.target.carId){assert.equal(picked.id,'17');hit=true;}
   }assert(hit,op.layer.recipeId);
  }
  assert.equal(JSON.stringify(s),before);assert.equal((await TerrainScene.prepare(s,catalog,cache(),{season,night},1)).operations.filter(o=>o.target.kind==='train').length,0);
 }assert.equal(recipes.size,8);
});
Deno.test('live cars preserve Traffic rail/accessory foreground occlusion for bridges, girders, tunnels, garages, both signals and poles',async()=>{
 const {railwayProfile}=await import('../src/lib/mixed-state.ts');
 const handlers=['bridge','girder','tunnel','garage','signal'];
 const bindings=railwayProfile.bindings.filter(b=>handlers.includes(b.handler));assert(bindings.length>5);
 for(const b of bindings)for(const season of ['spring','summer','autumn','winter'] as const)for(const night of [false,true]){
  const s=sceneFixture(),v=s.mixed!.voxels[0],r=v.traffic!.rail!;r.definition=b.reference;r.variant=0;r.direction=0;
  if(b.handler==='tunnel')r.terrainBackup={ne:0,se:0,sw:0,nw:0};
  // Art dispatch fixture: domain legality for each subtype is covered by P4;
  // retain real fleet occupancy and validate composed plan order here.
  const plan=new RailwayRenderPlan(s,catalog,{season,night}),space=new WorldSpace(16,32,6),st=space.storage(v.position);
  const ops=plan.operations(st.v*16+st.h,5,0,true,2).filter(o=>o.target.position.z===2);
  const car=ops.findIndex(o=>o.target.kind==='train');assert(car>=0,b.handler);
  for(const [i,op] of ops.entries())if(op.target.kind==='railway'&&'layer' in op){
   assert((op.layer.frame.stage??20)<30?i<car:i>car,b.handler);
  }
 }
});
Deno.test('junction drawing follows the actual predecessor route while a simple head stays straight',()=>{
 const s=sceneFixture(),t=s.mixed!.fleet!.trains[0],r=s.mixed!.voxels[1].traffic!.rail!;r.pattern=27;
 t.cars[1].state.heading=4;r.definition=definition('system:pattern/27');
 t.cars[0].state.location={...t.cars[1].state.location!,y:t.cars[1].state.location!.y+1};
 assert.equal(carOutgoing(s,t,1,r,'junction'),4);
 t.cars[0].state.location!.x--;assert.equal(carOutgoing(s,t,1,r,'junction'),5);
 t.cars[0].state.heading=4;assert.equal(carOutgoing(s,t,0,r,'junction'),4);
});
Deno.test('tunnel occlusion uses complete neighbor topology and invalidates a portal when its neighbor changes',async()=>{
 const {railwayProfile}=await import('../src/lib/mixed-state.ts');
 const s=sceneFixture(),tunnel=railwayProfile.bindings.find(b=>b.handler==='tunnel')!;
 for(const v of s.mixed!.voxels)Object.assign(v.traffic!.rail!,{definition:tunnel.reference,variant:1,terrainBackup:{ne:0,se:0,sw:0,nw:0}});
 const before=new RailwayRenderPlan(s,catalog,day),space=new WorldSpace(16,32,6);
 const column=(i:number)=>{const p=space.storage(s.mixed!.voxels[i].position);return p.v*16+p.h;};
 const kinds=(plan:RailwayRenderPlan,i:number)=>plan.operations(column(i),5,0,true,2).filter(o=>'layer' in o&&o.target.kind==='railway').map(o=>'tunnel' in o?o.tunnel:undefined);
 assert.deepEqual(kinds(before,0),['interior','interior']);assert.deepEqual(kinds(before,2),['portal','portal']);
 const next=structuredClone(s);next.mixed!.voxels[1].traffic!.rail={...sceneFixture().mixed!.voxels[1].traffic!.rail!};
 const after=new RailwayRenderPlan(next,catalog,day);
 assert.deepEqual(kinds(after,0),['portal','portal']);assert.deepEqual(kinds(before,0),['interior','interior']);
 assert.deepEqual(new Set(after.changedColumns(before)),new Set([column(0),column(1)]));
});
Deno.test('choice-junction heads render without evaluating shared controllers',()=>{
 const s=sceneFixture(),t=s.mixed!.fleet!.trains[0],r=s.mixed!.voxels[0].traffic!.rail!;
 r.pattern=27;r.definition=definition('system:pattern/27');t.cars[0].state.heading=4;
 for(const kind of ['timetable','delegation','simple']) {
  t.controllerId=s.mixed!.fleet!.controllers.find(c=>c.kind===kind)!.id;
  const before=JSON.stringify(s);assert.equal(carOutgoing(s,t,0,r,'junction'),4);assert.equal(JSON.stringify(s),before);
 }
});
Deno.test('motion/reversal changes only dependent columns, reuses decoded assets and clears old Inside slots on withdrawal/Outside',async()=>{
 const decoded=new Set<string>(),images=cache(decoded),s=sceneFixture(stocks[3]),before=await TerrainScene.prepare(s,catalog,images,day,5),count=decoded.size;
 const after=structuredClone(s);after.revision='7';after.mixed!.geometryRevision='7';
 const train=after.mixed!.fleet!.trains[0],car=train.cars[0];car.state.location={...car.state.location!,y:car.state.location!.y-1};after.mixed!.voxels[0].position=car.state.location;
 after.entities![0].anchor=car.state.location;
 const moved=await TerrainScene.prepare(after,catalog,images,day,5,before);assert.equal(decoded.size,count);
 const columns=moved.changedColumns(before)!;assert.equal(columns.length,2);
 const old=before.operations.find(o=>o.target.kind==='train'&&o.target.carId==='19');
 assert.equal(moved.operations.find(o=>o.target.kind==='train'&&o.target.carId==='19'),old);
 const outside=structuredClone(after);outside.mixed!.voxels[0].traffic!.car=null;outside.mixed!.fleet!.trains[0].cars[0].state.kind='outside';
 // The pure plan does not draw unplaced/Outside state without a Traffic slot.
 const plan=new RailwayRenderPlan(outside,catalog,day);assert.equal(plan.layers.filter(l=>l.entityId==='18').length,0);
 const reversed=structuredClone(s),t=reversed.mixed!.fleet!.trains[0];t.isReversed=true;t.cars.reverse();
 for(const c of t.cars)c.state.heading=4;
 const turned=await TerrainScene.prepare(reversed,catalog,images,day,5,before);
 // Reverse travel plus reverse formation preserves the physical body facing.
 assert.deepEqual(turned.changedColumns(before),[]);
});
Deno.test('offscreen and height-cut cars preflight exact recipes/variants and image failures never substitute a sprite',async()=>{
 const s=trainSceneFixture(source,catalog,stocks[0],false,200,100),id=s.mixed!.fleet!.trains[0].cars[0].definition.recipeId,before=JSON.stringify(s);
 await assert.rejects(()=>TerrainScene.prepare(s,{...catalog,renderRecipes:catalog.renderRecipes.filter(r=>r.id!==id)},cache(),day,1),/Car binding unavailable/);
 const missing=structuredClone(catalog),recipe=missing.renderRecipes.find(r=>r.id===id)!;
 for(const f of recipe.frames)delete f.masks!['spring/day'];
 await assert.rejects(()=>TerrainScene.prepare(s,missing,cache(),day,1),/Car variant unavailable/);
 const broken=new RecipeImageCache(()=>Promise.reject(new Error('missing sheet')));
 await assert.rejects(()=>TerrainScene.prepare(s,catalog,broken,day,5),/missing sheet/);assert.equal(JSON.stringify(s),before);
});
