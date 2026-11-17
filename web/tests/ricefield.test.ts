import assert from 'node:assert/strict';
import {applyDelta,immutable,parseMessageValue,parseLandPreview} from '../src/lib/protocol.ts';
import {riceGroundDefinitions,groundCatalogDefinitions,landQuery} from '../src/lib/land-edit.ts';
import {landPropertyArt} from '../src/lib/land-property-art.ts';
import {CityRenderPlan} from '../src/lib/city-renderer.ts';
import {TerrainScene} from '../src/lib/terrain-renderer.ts';
import {catalogPreviewLayers} from '../src/lib/catalog-preview.ts';
import {RecipeImageCache,type RenderCatalog} from '../src/lib/content-recipes.ts';
import {gameDate} from '../src/lib/game-time.ts';
import type {Snapshot,Delta} from '../src/generated/contracts.ts';
const load=async(suffix:string)=>JSON.parse(await Deno.readTextFile(new URL(`./fixtures/ricefield${suffix}`,import.meta.url)));
const before=immutable(parseMessageValue(await load('.before.json')) as Snapshot),after=parseMessageValue(await load('.snapshot.json')) as Snapshot;
const source=JSON.parse(await Deno.readTextFile(new URL('../public/content/bundled-v2/catalog.json',import.meta.url))) as RenderCatalog;
const catalog=landPropertyArt(source);
const cache=new RecipeImageCache(async media=>{const b=Object.values(catalog.surfaceSets).flatMap(Object.values).find(b=>b.image.url===media.url)!;return {image:{} as CanvasImageSource,width:b.width,height:b.height};});
Deno.test('ricefield canonical delta keeps rectangle corners ordered double receipts and real listener teardown',async()=>{
 const original=JSON.stringify(before),next=applyDelta(before,parseMessageValue(await load('.delta.json')) as Delta);assert(next);assert.deepEqual(JSON.parse(JSON.stringify(next)),after);assert.equal(JSON.stringify(before),original);
 assert.deepEqual(after.mixed!.city!.ground.map(g=>g.variant),[0,2,6,8]);assert.equal(after.mixed!.city!.producers.length,4);assert.equal(after.mixed!.stationGraph!.listeners.length,4);
 assert.deepEqual(after.mixed!.accounts.transactions.slice(1).map(t=>t.expenditure),['1500000','1500000','1500000','1500000','6000000']);
 const removed=applyDelta(immutable(after),parseMessageValue(await load('.removed.delta.json')) as Delta);assert(removed);assert.deepEqual(JSON.parse(JSON.stringify(removed)),parseMessageValue(await load('.removed.json')));assert.equal(removed.mixed!.stationGraph!.listeners.length,0);assert.deepEqual(removed.mixed!.accounts,after.mixed!.accounts);
});
Deno.test('both ricefield styles render all nine exact source crops in all eight appearances with alpha picks and cut handling',async()=>{
 assert.equal(riceGroundDefinitions.length,2);assert.equal(groundCatalogDefinitions.length,45);
 const crops=[[0,0],[32,16],[96,0],[0,16],[128,16],[96,16],[32,0],[64,16],[64,0]];
 for(const [style,d] of riceGroundDefinitions.entries()){
  const preview=catalogPreviewLayers(source,d.reference.recipeId);assert.equal(preview.length,9);
  for(let variant=0;variant<9;variant++){
   const read=structuredClone(after),g=read.mixed!.city!.ground.at(-1)!;g.definition=d.reference;g.variant=variant;read.mixed!.city!.producers.find(p=>p.ownerId===g.id)!.initialDefinition=d.reference;read.entities!.find(e=>e.id===g.id)!.definition=d.reference;
   for(const season of ['spring','summer','autumn','winter'] as const)for(const night of [false,true]){
    const appearance={season,night},plan=new CityRenderPlan(read,catalog,appearance),layer=plan.layers.find(l=>l.entityId===g.id)!;
    assert.deepEqual(layer.frame.rectangle,[crops[variant][0],crops[variant][1]+32*style,32,16]);
    const scene=await TerrainScene.prepare(read,catalog,cache,appearance,15),op=scene.operations.find(o=>o.target.id===g.id&&'layer' in o);assert(op&&'layer' in op);
    const [x,y,z]=layer.position,[ax,ay]=layer.frame.anchor!,mask=catalog.masks[layer.frame.masks![`${season}/${night?'night':'day'}`]];assert(mask);
    let picked=false;for(let row=0;row<mask.length&&!picked;row++)for(let col=0;col<mask[row].length&&!picked;col++)if(mask[row][col]==='1')picked=scene.pick({x:16*(x+y)-ax+col+.5,y:8*(y-x)-16*z-ay+row+.5},{x:0,y:0,zoom:1})?.id===g.id;
    assert(picked);const index=[...plan.columnIndices].find(i=>plan.operations(i,2,0,false).some(o=>o.target.id===g.id))!;assert(index!==undefined);assert(!plan.operations(index,1,0,false).some(o=>o.target.id===g.id));
   }
  }
 }
});
Deno.test('ricefield preview validates selected area plus successful cell charge including all-blocked rectangles',()=>{
 const d=riceGroundDefinitions[0],p={x:14,y:12,z:2},query=landQuery(before,p,{x:15,y:12,z:2},'cover',d.reference)!;
 const good={query,legal:true,reason:null,amount:'4500000',cells:[{position:p,status:'placeable',value:'0',ownerId:null,definition:d.reference},{position:{x:15,y:12,z:2},status:'protected',value:'0',ownerId:'1'}],reclaimed:[],positions:[p,{x:15,y:12,z:2}]};assert.deepEqual(parseLandPreview(good),good);
 for(const amount of ['1500000','3000000','6000000'])assert.throws(()=>parseLandPreview({...good,amount}));
 const blocked={...good,amount:'3000000',cells:good.cells.map(c=>({...c,status:'protected',definition:undefined}))};assert.deepEqual(parseLandPreview(blocked),blocked);
 assert.throws(()=>parseLandPreview({...good,cells:[{...good.cells[0],definition:riceGroundDefinitions[1].reference},good.cells[1]]}));
});
Deno.test('ricefield seasonal and night boundaries use fixed game calendar including year rollover',()=>{
 for(const [minute,month,day] of [[84960,3,1],[217440,6,1],[349920,9,1],[480960,12,1],[525600,1,1]]){
  const date=gameDate(String(minute));assert.equal(date.month,month);assert.equal(date.day,day);assert.equal(date.hour,0);assert.equal(gameDate(String(minute-1)).hour,23);
 }
 assert.equal(gameDate('360').hour,6);assert.equal(gameDate('1080').hour,18);
});
