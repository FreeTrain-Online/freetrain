import assert from 'node:assert/strict';
import {applyDelta,immutable,parseMessageValue,parseLandPreview} from '../src/lib/protocol.ts';
import {landQuery} from '../src/lib/land-edit.ts';
import {landPropertyArt,landPropertyRecipe} from '../src/lib/land-property-art.ts';
import {CityRenderPlan} from '../src/lib/city-renderer.ts';
import {TerrainScene} from '../src/lib/terrain-renderer.ts';
import {RecipeImageCache,type RenderCatalog} from '../src/lib/content-recipes.ts';
import {WorldSpace} from '../src/lib/map-geometry.ts';
import type {Snapshot,Delta} from '../src/generated/contracts.ts';
const load=async(suffix:string)=>JSON.parse(await Deno.readTextFile(new URL(`./fixtures/land-trading${suffix}`,import.meta.url)));
const before=immutable(parseMessageValue(await load('.before.json')) as Snapshot);
const delta=parseMessageValue(await load('.delta.json')) as Delta;
const after=parseMessageValue(await load('.snapshot.json')) as Snapshot;
const p={x:14,y:12,z:2};
Deno.test('land C# successor reconstructs exact properties Subsidiaries receipts producer teardown and immutable predecessor',()=>{
 const bytes=JSON.stringify(before),next=applyDelta(before,delta);assert(next);assert.deepEqual(JSON.parse(JSON.stringify(next)),after);
 assert.equal(JSON.stringify(before),bytes);assert.equal(next.mixed!.city!.properties.length,3);assert.equal(next.mixed!.city!.ground.length,1);
 assert.deepEqual(next.mixed!.accounts.transactions.map(t=>t.genre),Array(3).fill('{2A666F1A-9F40-4F67-98F4-DEAC1E55296D}'));
 assert.deepEqual(next.mixed!.accounts.transactions.slice(1).map(t=>t.expenditure),['30','30']);
 assert.equal(next.mixed!.city!.properties[0].id,before.mixed!.city!.properties[0].id);assert.equal(applyDelta(next,delta),undefined);
 assert.deepEqual(next.mixed!.queue,before.mixed!.queue);assert.deepEqual(next.mixed!.random,before.mixed!.random);
});
Deno.test('land preview validates exact ordered values classifications and rejects mismatched totals and duplicate owners',()=>{
 const q=landQuery(before,p,{x:15,y:13,z:2},'buy')!,value=before.entities!.find(e=>e.id==='1')!.value;
 const good={query:q,legal:true,reason:null,amount:String(BigInt(value)+60n),cells:[{position:p,status:'buyable',value:'30',ownerId:null},{position:{x:14,y:13,z:2},status:'owned',value:'30',ownerId:'3'},{position:{x:15,y:12,z:2},status:'owned',value:'30',ownerId:'2'},{position:{x:15,y:13,z:2},status:'buyable',value:'30',ownerId:null}],reclaimed:[{ownerId:'1',position:p,value,removalCost:'0'}],positions:[p,{x:14,y:13,z:2},{x:15,y:12,z:2},{x:15,y:13,z:2}]};
 assert.deepEqual(parseLandPreview(good),good);
 for(const change of [{amount:'1'},{query:{...q,end:{x:15,y:13,z:3}}},{cells:good.cells.map((c,i)=>i===0?{...c,position:{x:19,y:12,z:2}}:c)},{cells:[good.cells[0],good.cells[0]]},{reclaimed:[good.reclaimed[0],good.reclaimed[0]]},{legal:false},{cells:[{...good.cells[0],value:'-1'}]}])assert.throws(()=>parseLandPreview({...good,...change}));
 const inspect={query:landQuery(after,p,p,'inspect')!,legal:true,reason:null,amount:'0',cells:[{position:p,status:'owned',value:'30',ownerId:after.mixed!.city!.properties[1].id}],reclaimed:[],positions:[p]};
 assert.deepEqual(parseLandPreview(inspect),inspect);assert.throws(()=>parseLandPreview({...inspect,amount:'30'}));
});
Deno.test('land query preserves first corner XY and fixed level and exact current authority fields',()=>{
 const q=landQuery(before,{x:15,y:13,z:2},p,'sell')!;assert.deepEqual(q.anchor,{x:15,y:13,z:2});assert.deepEqual(q.end,p);
 assert.equal(q.expectedGeometryRevision,before.mixed!.geometryRevision);assert.equal(q.expectedMinute,before.tick);assert.equal(q.expectedWorldEpoch,before.worldEpoch);
});
Deno.test('owned land uses the pinned 32x16 property sprite in eight appearances and incremental removal clears the column',async()=>{
 const base=JSON.parse(await Deno.readTextFile(new URL('../public/content/bundled-v2/catalog.json',import.meta.url))) as RenderCatalog;
 const catalog=landPropertyArt(base);assert.equal(landPropertyArt(catalog),catalog);const bytes=JSON.stringify(base);
 for(const season of ['spring','summer','autumn','winter'] as const)for(const night of [false,true]){
  const variant={season,night},plan=new CityRenderPlan(after,catalog,variant);assert.equal(plan.layers.filter(l=>l.recipeId===landPropertyRecipe).length,3);
  for(const l of plan.layers.filter(l=>l.recipeId===landPropertyRecipe)){assert.deepEqual(l.frame.rectangle,[0,0,32,16]);assert.deepEqual(l.frame.anchor,[0,0]);assert(catalog.masks[l.frame.masks![`${season}/${night?'night':'day'}`]].some(r=>r.includes('1')));}
  const images=new RecipeImageCache(async media=>{const b=Object.values(catalog.surfaceSets).flatMap(Object.values).find(b=>b.image.url===media.url)!;return {image:{} as CanvasImageSource,width:b.width,height:b.height};});
  const scene=await TerrainScene.prepare(after,catalog,images,variant,15);
  const owned=scene.operations.filter(o=>'layer' in o&&o.layer.recipeId===landPropertyRecipe);assert.equal(owned.length,3);
  const space=new WorldSpace(after.map.width,after.map.height,after.map.levels),target=scene.pick(space.center(p),{x:0,y:0,zoom:1});assert.equal(target?.id,after.mixed!.city!.properties[1].id);
  assert.equal(target?.kind,'terrain');
  const previous=new CityRenderPlan(before,catalog,variant);assert(plan.changedColumns(previous).length>=2);
  const removed=await TerrainScene.prepare(before,catalog,images,variant,15,scene);
  assert.equal(removed.operations.filter(o=>'layer' in o&&o.layer.recipeId===landPropertyRecipe).length,1);
  assert.notEqual(removed.pick(space.center(p),{x:0,y:0,zoom:1})?.id,after.mixed!.city!.properties[1].id);
 }
 assert.equal(JSON.stringify(base),bytes);
});
