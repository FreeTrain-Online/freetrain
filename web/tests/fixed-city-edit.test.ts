import assert from 'node:assert/strict';
import {fixedCityInspection,completedFixedCityOwner,fixedCityDefinitions,fixedCityCatalog,fixedCityAt,fixedCityQuery,fixedCityFence} from '../src/lib/fixed-city-edit.ts';
import {catalogItems} from '../src/lib/building-catalog.ts';
import {parseFixedCityPreview} from '../src/lib/protocol.ts';
import type {Snapshot} from '../src/generated/contracts.ts';
Deno.test('fixed catalog binds authored dimensions, prices, localized names and supported variants',async()=>{
 const content=JSON.parse(await Deno.readTextFile(new URL('../public/content/bundled-v2/catalog.json',import.meta.url)));
 for(const locale of ['en','ja'] as const){
  const rows=fixedCityCatalog(content,locale),items=catalogItems('city',locale,content).flatMap(i=>i.variants);
  assert.equal(rows.length,items.length);assert(rows.length>20);
  for(const d of rows){
   const i=items.findIndex(v=>v.id===d.reference.recipeId);
   assert(d.title.length);if(locale==='en')assert(!/[\p{Script=Han}\p{Script=Hiragana}\p{Script=Katakana}]/u.test(d.title));assert.equal(items[i].price,Number(d.unitPrice));
   assert.equal(items[i].id,d.reference.recipeId);
   assert(items[i].detail.includes(`${d.sizeX} × ${d.sizeY} × ${d.minimumHeight}`));
   const recipe=content.renderRecipes.find((r:any)=>r.id===d.reference.recipeId);assert.equal(recipe.handler,'fixed-3d');
   assert.equal(recipe.frames.length,d.sizeX*d.sizeY*d.minimumHeight);
  }
 }
});
Deno.test('fixed removal resolves each occupied cell and previews reject inconsistent or duplicate responses',async()=>{
 const s=JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/fixtures/snapshot-city.json',import.meta.url))) as Snapshot;
 const b=s.mixed!.city!.structures[0],d=fixedCityDefinitions.find(d=>d.reference.recipeId===b.definition.recipeId)!;
 for(let x=0;x<d.sizeX;x++)for(let y=0;y<d.sizeY;y++)for(let z=0;z<b.height;z++){
  const p={x:b.location.x+x,y:b.location.y+y,z:b.location.z+z},q=fixedCityQuery(s,p,null,true)!;
  assert.equal(fixedCityAt(s,p)?.id,b.id);assert.equal(q.ownerId,b.id);assert.equal(q.expectedOwnerRevision,b.revision);
 }
 assert.equal(fixedCityFence(s),fixedCityFence({...s,tick:String(BigInt(s.tick)+1n)}));
 const query=fixedCityQuery(s,b.location,b.definition)!;
 const p={query,legal:true,reason:null,cost:'100',positions:[b.location],losses:[]};assert.deepEqual(parseFixedCityPreview(p),p);
 for(const bad of [{...p,positions:[]},{...p,positions:[b.location,b.location]},{...p,reason:'obstacle'},{...p,legal:false},{...p,cost:'-1'}])assert.throws(()=>parseFixedCityPreview(bad));
});

Deno.test('inspection follows natural fresh-owner completion but never demolition, rebuild or world replacement',async()=>{
 const before=JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/fixtures/snapshot-city.json',import.meta.url))) as Snapshot;
 const site=before.mixed!.city!.structures.find(s=>s.construction.length)!;
 const after=structuredClone(before),newId=before.mixed!.nextEntityId;
 const completed={...structuredClone(site),id:newId,construction:[]};
 after.mixed!.city!.structures=after.mixed!.city!.structures.filter(s=>s.id!==site.id).concat(completed);
 assert.equal(completedFixedCityOwner(fixedCityInspection(before,site.id),after,site.id)?.id,newId);
 assert.equal(completedFixedCityOwner(fixedCityInspection(before,site.id),{...after,worldEpoch:'999'},site.id),undefined);
 completed.construction=structuredClone(site.construction);assert.equal(completedFixedCityOwner(fixedCityInspection(before,site.id),after,site.id),undefined);
 after.mixed!.city!.structures=[];assert.equal(completedFixedCityOwner(fixedCityInspection(before,site.id),after,site.id),undefined);
});
