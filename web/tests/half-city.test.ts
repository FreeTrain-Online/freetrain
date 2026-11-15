import assert from 'node:assert/strict';
import {cityScene,addCity} from './helpers/city-scene.ts';
import {cityProfile,validCity} from '../src/lib/city-state.ts';
import {fixedCityQuery,fixedCityAt,fixedCityCatalog} from '../src/lib/fixed-city-edit.ts';
import {CityRenderPlan} from '../src/lib/city-renderer.ts';
import {TerrainScene} from '../src/lib/terrain-renderer.ts';
import {parseFixedCityPreview} from '../src/lib/protocol.ts';
import {RecipeImageCache,type RenderCatalog} from '../src/lib/content-recipes.ts';
const catalog=JSON.parse(await Deno.readTextFile(new URL('../public/content/bundled-v2/catalog.json',import.meta.url))) as RenderCatalog;
const definitions=cityProfile.structures.filter(d=>d.kind==='half'),variant={season:'spring',night:false} as const;
const cache=new RecipeImageCache(async media=>{const b=Object.values(catalog.surfaceSets).flatMap(Object.values).find(b=>b.image.url===media.url)!;return {image:{} as CanvasImageSource,width:b.width,height:b.height};});
Deno.test('all eight half definitions expose reviewed EN/JA raw art and exact side selectors',()=>{
 assert.equal(definitions.length,8);
 for(const locale of ['en','ja'])for(const d of definitions){
  const item=fixedCityCatalog(catalog,locale).find(i=>i.reference.recipeId===d.reference.recipeId)!;assert(item.title);if(locale==='en')assert.match(item.title,/Two-story gabled house [A-H]/);
  for(const v of d.halfVariants){const s=cityScene(),b=addCity(s,d);b.halves=[{definition:d.reference,side:v.side===0?'back':'fore',direction:v.direction,color:v.color}];assert(validCity(s));
   const q=fixedCityQuery(s,b.location,d.reference,false,undefined,{...v,side:v.side===0?'back':'fore',direction:v.direction as 0|2|4|6})!;
   assert.equal(parseFixedCityPreview({query:q,legal:true,reason:null,cost:'0',positions:[b.location],losses:[]}).query.half?.direction,v.direction);
   const plan=new CityRenderPlan(s,catalog,variant),index=[...plan.columnIndices][0],ops=plan.operations(index,31,0,true);
   assert.equal(ops.length,2);const layer=ops[1];assert('layer'in layer);assert.equal(layer.layer.frame.selector.side,1-v.side);assert.equal(layer.layer.frame.selector.direction,v.direction);
   assert.equal(plan.contains({...b.location,z:b.location.z+1}),false);assert.equal(fixedCityAt(s,{...b.location,z:b.location.z+1}),undefined);
  }
 }
});
Deno.test('half cuts preserve strict source height inequality, brown base and Back before Fore with stable owner picks',async()=>{
 const s=cityScene(),b=addCity(s,definitions[0]);b.halves=[{definition:definitions[1].reference,side:'fore',direction:4,color:0},{definition:definitions[0].reference,side:'back',direction:0,color:0}];assert(validCity(s));
 const plan=new CityRenderPlan(s,catalog,variant),index=[...plan.columnIndices][0];
 assert.deepEqual(plan.operations(index,1,0,false),[]);
 for(const cut of [2,3]){const ops=plan.operations(index,cut,0,false);assert.equal(ops.length,1);assert('color'in ops[0]);assert.equal(ops[0].color,'#925e2a');}
 assert.equal(plan.operations(index,4,0,false).length,3);const full=plan.operations(index,31,0,true);assert.equal(full.length,3);
 assert('layer'in full[1]&&'layer'in full[2]);assert.equal(full[1].layer.recipeId,definitions[0].reference.recipeId);assert.equal(full[2].layer.recipeId,definitions[1].reference.recipeId);
 const scene=await TerrainScene.prepare(s,catalog,cache,variant,31),ops=scene.operations.filter(o=>o.target.kind==='building');assert.equal(ops.length,3);assert(ops.every(o=>o.target.id===b.id));
 const broken=structuredClone(catalog);broken.renderRecipes.find(r=>r.id===definitions[0].reference.recipeId)!.frames=[];assert.throws(()=>new CityRenderPlan(s,broken,variant));
});
Deno.test('partial removal retains initial provider and changing halves invalidates column while adjacent upper owner remains',()=>{
 const s=cityScene(),b=addCity(s,definitions[0]);b.halves=[{definition:definitions[1].reference,side:'fore',direction:4,color:0}];assert(validCity(s));
 const upper=addCity(s,definitions[2]);upper.location={...b.location,z:3};s.entities!.find(e=>e.id===upper.id)!.anchor=upper.location;upper.halves=[{definition:definitions[2].reference,side:'back',direction:0,color:0}];
 const plan=new CityRenderPlan(s,catalog,variant);assert.equal(plan.operations([...plan.columnIndices][0],31,0,true).length,4);
 const next=structuredClone(s);next.mixed!.city!.structures[0].halves.push({definition:definitions[0].reference,side:'back',direction:0,color:0});const after=new CityRenderPlan(next,catalog,variant);assert.equal(after.changedColumns(plan).length,1);
 const bad=structuredClone(s);bad.mixed!.city!.structures[0].halves[0].direction=2;bad.mixed!.city!.structures[0].halves.push({definition:definitions[0].reference,side:'back',direction:0,color:0});assert.equal(validCity(bad),false);
});
