import assert from 'node:assert/strict';
import {applyDelta,immutable,parseMessageValue,parseLandPreview,parseRemovalPreview} from '../src/lib/protocol.ts';
import {landQuery,staticGroundDefinitions,mixedGroundDefinitions} from '../src/lib/land-edit.ts';
import {landPropertyArt} from '../src/lib/land-property-art.ts';
import {staticGroundArt} from '../src/lib/static-ground-art.ts';
import {CityRenderPlan} from '../src/lib/city-renderer.ts';
import {TerrainScene} from '../src/lib/terrain-renderer.ts';
import {catalogPreviewLayers} from '../src/lib/catalog-preview.ts';
import {RecipeImageCache,type RenderCatalog} from '../src/lib/content-recipes.ts';
import {removalAt} from '../src/lib/object-removal.ts';
import type {Snapshot,Delta} from '../src/generated/contracts.ts';
const load=async(suffix:string)=>JSON.parse(await Deno.readTextFile(new URL(`./fixtures/static-ground${suffix}`,import.meta.url)));
const before=immutable(parseMessageValue(await load('.before.json')) as Snapshot),after=parseMessageValue(await load('.snapshot.json')) as Snapshot;
const delta=parseMessageValue(await load('.delta.json')) as Delta,removed=parseMessageValue(await load('.removed.json')) as Snapshot;
const p={x:14,y:12,z:2};
const source=JSON.parse(await Deno.readTextFile(new URL('../public/content/bundled-v2/catalog.json',import.meta.url))) as RenderCatalog;
const catalog=landPropertyArt(source);
const cache=new RecipeImageCache(async media=>{const b=Object.values(catalog.surfaceSets).flatMap(Object.values).find(b=>b.image.url===media.url)!;return {image:{} as CanvasImageSource,width:b.width,height:b.height};});
Deno.test('static ground C# deltas attach four real providers replace property and detach both listener directions immutably',async()=>{
 const original=JSON.stringify(before),next=applyDelta(before,delta);assert(next);assert.deepEqual(JSON.parse(JSON.stringify(next)),after);assert.equal(JSON.stringify(before),original);
 assert.equal(after.mixed!.city!.ground.length,4);assert.equal(after.mixed!.city!.properties.length,0);assert.equal(after.mixed!.city!.producers.length,4);
 assert.equal(after.mixed!.stationGraph!.listeners.length,4);assert.equal(after.mixed!.stationGraph!.listenerMemberships[0].providers.length,4);
 assert.equal(after.mixed!.accounts.transactions.at(-1)!.expenditure,'1200000');assert.deepEqual(after.mixed!.queue,before.mixed!.queue);
 const cleared=applyDelta(immutable(after),parseMessageValue(await load('.removed.delta.json')) as Delta);assert(cleared);assert.deepEqual(JSON.parse(JSON.stringify(cleared)),removed);assert.equal(cleared.mixed!.stationGraph!.listeners.length,0);
 assert.deepEqual(cleared.mixed!.accounts,after.mixed!.accounts);assert.equal(applyDelta(next,delta),undefined);
});
Deno.test('all twenty static definitions render authored seasonal night pixels alpha picks and cuts with four explicit crop intersections',async()=>{
 assert.equal(staticGroundDefinitions.length,20);assert.equal(source.renderRecipes.filter(r=>r.handler==='land-0d'&&r.status==='source-diagnostic').length,4);
 for(const d of staticGroundDefinitions){
  const read=structuredClone(after),g=read.mixed!.city!.ground.at(-1)!;g.definition=d.reference;read.entities!.find(e=>e.id===g.id)!.definition=d.reference;read.mixed!.city!.producers.find(p=>p.ownerId===g.id)!.initialDefinition=d.reference;
  const original=JSON.stringify(read);assert.equal(catalogPreviewLayers(source,d.reference.recipeId).length,1);
  for(const season of ['spring','summer','autumn','winter'] as const)for(const night of [false,true]){
   const variant={season,night},plan=new CityRenderPlan(read,catalog,variant);assert.equal(plan.layers.filter(l=>l.entityId===g.id).length,1);
   const scene=await TerrainScene.prepare(read,catalog,cache,variant,15),op=scene.operations.find(o=>o.target.id===g.id&&'layer' in o);assert(op&&'layer' in op);
   const l=op.layer,[x,y,z]=l.position,[ax,ay]=l.frame.anchor!,mask=catalog.masks[l.frame.masks![`${season}/${night?'night':'day'}`]];
   assert(mask);let picked=false;
   for(let row=0;row<mask.length&&!picked;row++)for(let col=0;col<mask[row].length&&!picked;col++)if(mask[row][col]==='1')picked=scene.pick({x:16*(x+y)-ax+col+.5,y:8*(y-x)-16*z-ay+row+.5},{x:0,y:0,zoom:1})?.id===g.id;
   assert(picked,d.reference.recipeId);assert.equal(l.frame.rectangle![3],d.reference.recipeId.endsWith(',4')?16:32);
   const index=[...plan.columnIndices].find(i=>plan.operations(i,2,0,false).some(o=>o.target.id===g.id))!;assert(index!==undefined);assert(!plan.operations(index,1,0,false).some(o=>o.target.id===g.id));
  }
  assert.equal(JSON.stringify(read),original);
 }
 const changed=structuredClone(source);changed.surfaceSets[source.renderRecipes.find(r=>r.handler==='land-0d'&&r.status==='source-diagnostic')!.frames[0].surfaceSet!]['spring/day'].image.sha256='0'.repeat(64);assert.throws(()=>staticGroundArt(changed));
});
Deno.test('ground previews preserve exact source area charges including blocked cells and reject malformed review amounts and definitions',()=>{
 const d=staticGroundDefinitions[0],query=landQuery(before,p,{x:15,y:12,z:2},'cover',d.reference)!;
 const good={query,legal:true,reason:null,amount:'600000',cells:[{position:p,status:'placeable',value:'0',ownerId:'2',definition:d.reference},{position:{x:15,y:12,z:2},status:'protected',value:'0',ownerId:'1'}],reclaimed:[],positions:[p,{x:15,y:12,z:2}]};
 assert.deepEqual(parseLandPreview(good),good);assert.deepEqual(parseLandPreview({...good,cells:good.cells.map(c=>({...c,status:'protected',definition:undefined}))}).amount,'600000');
 for(const change of [{amount:'300000'},{query:{...query,definition:null}},{cells:good.cells.map(c=>({...c,status:'buyable'}))},{cells:good.cells.map(c=>({...c,value:'1'}))}])assert.throws(()=>parseLandPreview({...good,...change}));
 const remove={...good,query:{...query,action:'removeCover',definition:null},amount:'0',cells:good.cells.map(c=>({...c,status:'removable',definition:undefined}))};assert.deepEqual(parseLandPreview(remove),remove);assert.throws(()=>parseLandPreview({...remove,amount:'1'}));
});
Deno.test('static cover removal clears only changed columns and general map removal binds exact owner identity',async()=>{
 const variant={season:'winter' as const,night:true},a=await TerrainScene.prepare(after,catalog,cache,variant,15),b=await TerrainScene.prepare(removed,catalog,cache,variant,15,a);
 assert(b.changedColumns(a)!.length>=4);assert.equal(new CityRenderPlan(after,catalog,variant).changedColumns(new CityRenderPlan(removed,catalog,variant)).length,4);assert(!b.operations.some(o=>after.mixed!.city!.ground.some(g=>g.id===o.target.id)));
 const q=removalAt(after,p)!;assert.equal(q.expectedEntityId,after.mixed!.city!.ground[0].id);
 const preview={query:q,legal:true,reason:null,objectKind:'groundCover',positions:[p],path:[p],skipped:[]};assert.deepEqual(parseRemovalPreview(preview),preview);
});

Deno.test('five mixed selectors retain ordered source choices and use deterministic catalog mosaics without gameplay random draws',()=>{
 assert.equal(mixedGroundDefinitions.length,5);
 const literal=[['0,0','1,0'],['2,0','3,0','0,1','1,1','2,1','3,1'],['0,3','1,3','2,3','3,3','0,4','1,4'],['0,0','1,0','0,2','1,2','2,2','3,2'],['2,4','3,4']];
 for(const [i,d] of mixedGroundDefinitions.entries()){
  assert.deepEqual(d.selections.map(r=>r.contributionId.split('-').at(-1)),literal[i]);
  assert.equal(catalogPreviewLayers(source,d.reference.recipeId).length,literal[i].length);
  const query=landQuery(before,p,p,'cover',d.reference)!,definition=d.selections.at(-1)!;
  const good={query,legal:true,reason:null,amount:'300000',cells:[{position:p,status:'placeable',value:'0',ownerId:null,definition}],reclaimed:[],positions:[p]};
  assert.deepEqual(parseLandPreview(good),good);
  assert.throws(()=>parseLandPreview({...good,cells:[{...good.cells[0],definition:staticGroundDefinitions.find(s=>!d.selections.some(r=>r.recipeId===s.reference.recipeId))!.reference}]}));
 }
});
