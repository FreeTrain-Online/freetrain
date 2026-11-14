import assert from 'node:assert/strict';
import {cityScene,addCity,fixedDefinitions} from './helpers/city-scene.ts';
import {addRail} from './helpers/station-scene.ts';
import {TerrainScene} from '../src/lib/terrain-renderer.ts';
import {CityRenderPlan} from '../src/lib/city-renderer.ts';
import {RecipeImageCache,type RenderCatalog,type Variant} from '../src/lib/content-recipes.ts';
import {WorldSpace} from '../src/lib/map-geometry.ts';
import {validSnapshot} from '../src/lib/world-identity.ts';
const catalog=JSON.parse(await Deno.readTextFile(new URL('../public/content/bundled-v2/catalog.json',import.meta.url))) as RenderCatalog;
const day:Variant={season:'spring',night:false},view={x:0,y:0,zoom:1};
const images=(c=catalog,onDecode=()=>{})=>new RecipeImageCache(async media=>{onDecode();const b=Object.values(c.surfaceSets).flatMap(Object.values).find(b=>b.image.url===media.url)!;return {image:{} as CanvasImageSource,width:b.width,height:b.height};});
const ops=(s:TerrainScene)=>s.operations.filter(o=>o.target.kind==='building');
Deno.test('all 66 fixed definitions render prepared members and stable alpha picks in eight appearances',async()=>{
 assert.equal(fixedDefinitions.length,66);
 for(const d of fixedDefinitions){const s=cityScene(),b=addCity(s,d);assert(validSnapshot(s),d.reference.recipeId);const bytes=JSON.stringify(s);
  for(const season of ['spring','summer','autumn','winter'] as const)for(const night of [false,true]){
   const scene=await TerrainScene.prepare(s,catalog,images(),{season,night},31),layers=ops(scene),recipe=catalog.renderRecipes.find(r=>r.id===d.reference.recipeId)!;
   assert.equal(layers.length,recipe.frames.filter(f=>f.status!=='invisible').length);let found=false;
   for(const o of layers){assert('layer' in o);assert.equal(o.target.id,b.id);const l=o.layer,[x,y,z]=l.position,[ax,ay]=l.frame.anchor!,mask=catalog.masks[l.frame.masks![`${season}/${night?'night':'day'}`]],row=mask.findIndex(r=>r.includes('1'));if(row<0)continue;
    const point={x:16*(x+y)-ax+mask[row].indexOf('1')+.5,y:8*(y-x)-16*z-ay+row+.5};
    if(scene.pick(point,view)?.id===b.id){found=true;assert.equal(scene.pick({x:point.x*.75+7,y:point.y*.75+9},{x:7,y:9,zoom:.75})?.id,b.id);}
   }assert(found,d.reference.recipeId);
  }assert.equal(JSON.stringify(s),bytes);
 }
});
Deno.test('all fixed footprint members including invisible cells get source brown silhouettes at every cut',async()=>{
 for(const d of fixedDefinitions){const s=cityScene(),b=addCity(s,d),space=new WorldSpace(40,60,32);
  for(let cut=0;cut<=2+b.height;cut++){
   const scene=await TerrainScene.prepare(s,catalog,images(),day,cut),art=ops(scene);
   if(cut<2){assert.equal(art.length,0);continue;}if(cut>=2+b.height){assert(art.every(o=>'layer' in o));continue;}
   assert.equal(art.length,d.sizeX*d.sizeY);
   for(const op of art){assert('points' in op);assert.equal(op.color,'#b37333');assert.equal(scene.pick(space.center(op.target.position),view)?.id,b.id);}
   assert(!scene.operations.some(o=>o.target.kind==='terrain'&&art.some(a=>a.target.position.x===o.target.position.x&&a.target.position.y===o.target.position.y)));
  }
 }
});
Deno.test('saved construction states grounds doors and connectivity retain source pass order at cuts',async()=>{
 const d=fixedDefinitions.find(d=>d.sizeX===2&&d.sizeY===2&&d.minimumHeight===3)!;
 for(let stage=0;stage<5;stage++)for(let ground=0;ground<7;ground++)for(const door of [false,true]){
  const s=cityScene(),b=addCity(s,d,10,22,stage,ground,door);assert(validSnapshot(s));
  const scene=await TerrainScene.prepare(s,catalog,images(),day,2),art=ops(scene);assert(art.every(o=>'layer' in o));
  const part=(x:number,y:number)=>art.filter(o=>o.target.position.x===b.location.x+x&&o.target.position.y===b.location.y+y).map(o=>'layer' in o?o.layer.recipeId.replace('system:construction/',''):'cut');
  assert.deepEqual(part(0,1),stage===0?['ground/'+ground,'fence/2/0','fence/3/0']:stage<4?['ground/0',`bones/${stage-1}/1100`,'fence/2/0','fence/3/0']:['bones/2/1100','wall/2','wall/3','fence/2/0','fence/3/0']);
  assert(part(1,1).includes(`fence/2/${door?1:0}`));
  assert.equal(ops(await TerrainScene.prepare(s,catalog,images(),day,31)).some(o=>o.target.position.z===4),stage>0);
 }
});
Deno.test('every fixed selector binds every construction stage and source traversal without completed-art substitutes',()=>{
 for(const d of fixedDefinitions)for(let stage=0;stage<5;stage++){
  const s=cityScene(),b=addCity(s,d,10,22,stage),plan=new CityRenderPlan(s,catalog,day);assert(validSnapshot(s));
  assert.equal(plan.columnIndices.size,d.sizeX*d.sizeY);assert(plan.layers.every(l=>l.recipeId.startsWith('system:construction/')));
  for(const index of plan.columnIndices){const operations=plan.operations(index,31,0,true);assert(operations.every(o=>o.target.id===b.id&&'layer' in o));}
 }
});
Deno.test('city stage/build/removal invalidate full footprints and preserve unrelated operations and image cache',async()=>{
 const s=cityScene(),railId=addRail(s,30,50);let decoded=0;const cache=images(catalog,()=>decoded++);
 const empty=await TerrainScene.prepare(s,catalog,cache,day,31),built=structuredClone(s),b=addCity(built,fixedDefinitions.find(d=>d.sizeX===2&&d.sizeY===2)!,10,22,0);
 const site=await TerrainScene.prepare(built,catalog,cache,day,31,empty);assert(site.changedColumns(empty)!.length>=4);
 const next=structuredClone(built);next.mixed!.city!.structures[0].construction[0].cell.ground=4;
 const stage=await TerrainScene.prepare(next,catalog,cache,day,31,site);assert(stage.changedColumns(site)!.length>0);
 const complete=cityScene();addRail(complete,30,50);addCity(complete,fixedDefinitions.find(d=>d.sizeX===2&&d.sizeY===2)!);
 const done=await TerrainScene.prepare(complete,catalog,cache,day,31,stage),count=decoded;
 const again=await TerrainScene.prepare(complete,catalog,cache,day,31,done);assert.equal(decoded,count);assert.deepEqual(again.changedColumns(done),[]);
 const removed=await TerrainScene.prepare(s,catalog,cache,day,31,done);assert(removed.changedColumns(done)!.length>=4);assert.equal(ops(removed).length,0);
 assert(removed.operations.includes(empty.operations.find(o=>o.target.id===railId)!));assert(ops(site).length>0);
});
Deno.test('missing or malformed fixed and construction art blocks readiness above cuts and offscreen',()=>{
 const s=cityScene(200,100),b=addCity(s,fixedDefinitions[0],150,70);
 for(const mutate of [(c:RenderCatalog)=>{c.renderRecipes=c.renderRecipes.filter(r=>r.id!==b.definition.recipeId);},(c:RenderCatalog)=>{c.renderRecipes.find(r=>r.id===b.definition.recipeId)!.frames=[];},(c:RenderCatalog)=>{c.renderRecipes.find(r=>r.id===b.definition.recipeId)!.frames[0].cell=[9,0,0];},(c:RenderCatalog)=>{delete c.renderRecipes.find(r=>r.id===b.definition.recipeId)!.frames[0].masks!['spring/day'];}]){
  const c=structuredClone(catalog);mutate(c);assert.throws(()=>new CityRenderPlan(s,c,day));
 }
 const site=cityScene();addCity(site,fixedDefinitions[0],10,22,0);const c=structuredClone(catalog);c.renderRecipes=c.renderRecipes.filter(r=>r.id!=='system:construction/ground/0');assert.throws(()=>new CityRenderPlan(site,c,day));
});
Deno.test('opaque city cells suppress water/underground art and full view skips submerged members',async()=>{
 const s=cityScene(),b=addCity(s,fixedDefinitions.find(d=>d.minimumHeight===3)!);s.terrain!.waterLevel=3;
 assert(ops(await TerrainScene.prepare(s,catalog,images(),day,31)).every(o=>o.target.position.z>=3));
 const buried=structuredClone(s);buried.terrain!.waterLevel=0;buried.terrain!.cells.forEach(c=>c.ground=4);assert(validSnapshot(buried));
 const cut=await TerrainScene.prepare(buried,catalog,images(),day,2);assert.equal(cut.pick(new WorldSpace(40,60,32).center(b.location),view)?.id,b.id);
});
