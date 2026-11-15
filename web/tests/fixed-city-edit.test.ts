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
   assert(d.title.length);if(locale==='en')assert(!/[\p{Script=Han}\p{Script=Hiragana}\p{Script=Katakana}]/u.test(d.title));assert.equal(items[i].price,Number(d.unitPrice)*(d.kind==='variable'?d.minimumHeight:1));
   assert.equal(items[i].id,d.reference.recipeId);
   assert(items[i].detail.includes(`${d.sizeX} × ${d.sizeY} × ${d.minimumHeight}`));
   const recipe=content.renderRecipes.find((r:any)=>r.id===d.reference.recipeId);assert.equal(recipe.handler,d.kind==='half'?'half-voxel':d.kind==='variable'?'variable-height':'fixed-3d');
   if(d.kind==='fixed')assert.equal(recipe.frames.length,d.sizeX*d.sizeY*d.minimumHeight);
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

Deno.test('variable selection retains exact height and removal resolves invisible upper occupied members',async()=>{
 const {cityScene,addCity}=await import('./helpers/city-scene.ts');
 const d=fixedCityDefinitions.find(d=>d.kind==='variable'&&d.sizeX===1&&d.sizeY===1)!;
 const s=cityScene(),b=addCity(s,d,10,22,undefined,0,false,d.maximumHeight);
 const query=fixedCityQuery(s,b.location,b.definition,false,d.maximumHeight)!;
 assert.equal(query.height,d.maximumHeight);assert.deepEqual(parseFixedCityPreview({query,legal:true,reason:null,cost:d.unitPrice,positions:[b.location],losses:[]}).query,query);
 const upper={...b.location,z:b.location.z+b.height-1};assert.equal(fixedCityAt(s,upper)?.id,b.id);
 assert.equal(fixedCityQuery(s,upper,null,true)?.ownerId,b.id);assert.equal(fixedCityQuery(s,upper,null,true)?.height,undefined);
 for(const height of [0,-1,257,1.5])assert.throws(()=>parseFixedCityPreview({query:{...query,height},legal:true,reason:null,cost:'1',positions:[b.location],losses:[]}));
});

Deno.test('P7-12 all 129 registered cards preserve source categories, identities, eligibility and complete selected art',async()=>{
 const {catalogPreviewLayers}=await import('../src/lib/catalog-preview.ts');
 const {cityCatalogChoice}=await import('../src/lib/fixed-city-edit.ts');
 const {objectOrigin}=await import('../src/lib/object-provenance.ts');
 const content=JSON.parse(await Deno.readTextFile(new URL('../public/content/bundled-v2/catalog.json',import.meta.url)));
 assert.equal(fixedCityDefinitions.length,129);
 assert.deepEqual(['fixed','variable','half'].map(k=>fixedCityDefinitions.filter(d=>d.kind===k).length),[66,55,8]);
 for(const locale of ['en','ja'] as const){
  const items=catalogItems('city',locale,content),variants=items.flatMap(i=>i.variants);
  assert.equal(new Set(variants.map(v=>v.id)).size,129);
  assert(!variants.some(v=>v.id.includes('null-contrib')));
  for(const d of fixedCityDefinitions){
   const row=variants.find(v=>v.id===d.reference.recipeId)!;
   const source=content.contributions.find((c:any)=>c.id===d.reference.contributionId);
   const field=(tag:string)=>source.declaration.children.find((n:any)=>n.tag===tag)?.text??'';
   assert.equal(row.sourceGroup,field('group'));assert.equal(row.sourceSubgroup,field('subgroup'));
   assert(row.search!.includes(d.reference.contributionId));
   const origin=objectOrigin(content,row.id,locale)!;assert(origin.author);assert(row.search!.includes(origin.author));
   assert(row.detail.includes(locale==='en'?(d.computerCannotBuild?'Manual placement only':'Eligible for automatic development'):(d.computerCannotBuild?'手動建設のみ':'自動発展の対象')));
   const initial=cityCatalogChoice(row.id);
   const choices=d.kind==='variable'?[{...initial,height:d.minimumHeight},{...initial,height:d.maximumHeight}]:d.kind==='half'?d.halfVariants.map(v=>({...initial,side:v.side===0?'back' as const:'fore' as const,direction:v.direction as 0|2|4|6,color:v.color})):[initial];
   for(const choice of choices){
    const layers=catalogPreviewLayers(content,row.id,undefined,choice);assert(layers.length);
    assert(layers.every(l=>l.frame.status==='ready'));
    if(d.kind==='variable'){
     assert.equal(Math.max(...layers.map(l=>l.position[2])),choice.height-1);
     const selected=catalogItems('city',locale,content,{[row.id]:choice}).flatMap(i=>i.variants).find(v=>v.id===row.id)!;
     assert.equal(selected.price,Number(d.unitPrice)*choice.height);assert(selected.detail.includes(`${d.sizeX} × ${d.sizeY} × ${choice.height}`));
    }
    if(d.kind==='half'){assert.equal(layers.length,1);assert.equal(layers[0].frame.selector.side,choice.side==='fore'?0:1);assert.equal(layers[0].frame.selector.direction,choice.direction);}
   }
   if(d.kind==='variable')assert.throws(()=>catalogPreviewLayers(content,row.id,undefined,{...initial,height:d.maximumHeight+1}));
   if(d.kind==='half')assert.throws(()=>catalogPreviewLayers(content,row.id,undefined,{...initial,color:99}));
  }
 }
});
