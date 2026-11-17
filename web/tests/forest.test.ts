import assert from 'node:assert/strict';
import {applyDelta,immutable,parseMessageValue,parseLandPreview} from '../src/lib/protocol.ts';
import {landQuery,forestGroundDefinitions,forestGroundLabels} from '../src/lib/land-edit.ts';
import {landPropertyArt} from '../src/lib/land-property-art.ts';
import {TerrainScene} from '../src/lib/terrain-renderer.ts';
import {catalogPreviewLayers} from '../src/lib/catalog-preview.ts';
import {RecipeImageCache,type RenderCatalog} from '../src/lib/content-recipes.ts';
import type {Snapshot,Delta} from '../src/generated/contracts.ts';
const load=async(suffix:string)=>JSON.parse(await Deno.readTextFile(new URL(`./fixtures/forest${suffix}`,import.meta.url)));
const before=immutable(parseMessageValue(await load('.before.json')) as Snapshot),after=immutable(parseMessageValue(await load('.snapshot.json')) as Snapshot);
const source=JSON.parse(await Deno.readTextFile(new URL('../public/content/bundled-v2/catalog.json',import.meta.url))) as RenderCatalog;
const catalog=landPropertyArt(source);
const cache=new RecipeImageCache(async media=>{const b=Object.values(catalog.surfaceSets).flatMap(Object.values).find(b=>b.image.url===media.url)!;return {image:{} as CanvasImageSource,width:b.width,height:b.height};});
Deno.test('paid forest production C# delta and removal preserve canonical terrain ownership accounts and empty providers',async()=>{
 const original=JSON.stringify(before),next=applyDelta(before,parseMessageValue(await load('.delta.json')) as Delta);assert(next);
 assert.deepEqual(JSON.parse(JSON.stringify(next)),after);assert.equal(JSON.stringify(before),original);assert.equal(after.terrain!.occupants.length,4);
 assert(after.terrain!.occupants.every(o=>after.entities!.some(e=>e.id===o.entityId&&e.owned&&e.value==='100')));assert.equal(after.mixed!.accounts.transactions.at(-1)!.expenditure,'400');assert.equal(after.mixed!.city!.producers.length,0);
 assert.deepEqual(after.terrain!.provenance,before.terrain!.provenance);assert.deepEqual(after.mixed!.queue,before.mixed!.queue);
 const removed=applyDelta(immutable(next),parseMessageValue(await load('.removed.delta.json')) as Delta);assert(removed);
 assert.deepEqual(JSON.parse(JSON.stringify(removed)),parseMessageValue(await load('.removed.json')));assert.equal(removed.terrain!.occupants.length,0);assert.deepEqual(removed.mixed!.accounts,after.mixed!.accounts);
});
Deno.test('all eighteen planted species and ground treatments render and alpha pick in eight season light variants without changing realized trees',async()=>{
 assert.equal(forestGroundDefinitions.length,18);
 for(const [i,d] of forestGroundDefinitions.entries()){
  assert.equal(forestGroundLabels(d.reference.recipeId).species,['treeCedar','treePine','treeCamphor','treeMaple','treeGinkgo','treeBeech'][i%6]);
  assert.equal(forestGroundLabels(d.reference.recipeId).ground,['forestNatural','forestMountain','forestGrass'][Math.floor(i/6)]);
  assert(catalogPreviewLayers(source,d.reference.recipeId).length>0);
  const read=structuredClone(after),o=read.terrain!.occupants[0];o.forest!.definition=d.reference;o.forest!.trees=[{x:16,y:8,sprite:0}];const original=JSON.stringify(read);
  for(const season of ['spring','summer','autumn','winter'] as const)for(const night of [false,true]){
   const scene=await TerrainScene.prepare(read,catalog,cache,{season,night},15);const op=scene.operations.find(op=>op.target.id===o.entityId&&'layer' in op&&op.layer.frame.key==='tree/0');assert(op&&'layer' in op);
   const l=op.layer,[x,y,z]=l.position,[ax,ay]=l.frame.anchor!,mask=catalog.masks[l.frame.masks![`${season}/${night?'night':'day'}`]];let picked=false;
   for(let row=0;row<mask.length&&!picked;row++)for(let col=0;col<mask[row].length&&!picked;col++)if(mask[row][col]==='1')picked=scene.pick({x:16*(x+y)-ax+col+.5,y:8*(y-x)-16*z-ay+row+.5},{x:0,y:0,zoom:1})?.id===o.entityId;
   assert(picked,`${d.reference.recipeId}/${season}/${night}`);
   const cut=await TerrainScene.prepare(read,catalog,cache,{season,night},1);assert(!cut.operations.some(op=>op.target.id===o.entityId&&op.target.surface==='forest'));
  }
  assert.equal(JSON.stringify(read),original);
 }
});
Deno.test('forest rectangle review validates the actual source definition price and full skipped area',()=>{
 const d=forestGroundDefinitions[0],p={x:14,y:12,z:2},query=landQuery(before,p,p,'cover',d.reference)!;
 const good={query,legal:true,reason:null,amount:'100',cells:[{position:p,status:'placeable',value:'0',ownerId:null,definition:d.reference}],reclaimed:[],positions:[p]};
 assert.deepEqual(parseLandPreview(good),good);assert.deepEqual(parseLandPreview({...good,cells:[{...good.cells[0],status:'protected',definition:undefined}]}).amount,'100');
 for(const change of [{amount:'0'},{amount:'300000'},{query:{...query,definition:{...d.reference,packId:'missing'}}},{cells:[{...good.cells[0],definition:forestGroundDefinitions[1].reference}]}])assert.throws(()=>parseLandPreview({...good,...change}));
});
