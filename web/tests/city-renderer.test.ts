import {variableCityArt} from '../src/lib/variable-city-art.ts';
import assert from 'node:assert/strict';
import {cityScene,addCity,fixedDefinitions} from './helpers/city-scene.ts';
import {addRail} from './helpers/station-scene.ts';
import {TerrainScene} from '../src/lib/terrain-renderer.ts';
import {CityRenderPlan} from '../src/lib/city-renderer.ts';
import {RecipeImageCache,type RenderCatalog,type Variant} from '../src/lib/content-recipes.ts';
import {WorldSpace} from '../src/lib/map-geometry.ts';
import {validSnapshot} from '../src/lib/world-identity.ts';
const catalog=variableCityArt(JSON.parse(await Deno.readTextFile(new URL('../public/content/bundled-v2/catalog.json',import.meta.url))) as RenderCatalog);
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
Deno.test('localized city mask damage covers every changed full-render ground column at stage transitions and removal',async()=>{
 const cache=images(catalog),s=cityScene(),built=structuredClone(s);
 addCity(built,fixedDefinitions.find(d=>d.sizeX===2&&d.sizeY===2)!,10,22,0);
 const staged=structuredClone(built);for(const v of staged.mixed!.city!.structures[0].construction)v.cell.state=v.cell.state===4?4:3;
 const complete=cityScene();addCity(complete,fixedDefinitions.find(d=>d.sizeX===2&&d.sizeY===2)!);
 let prior=await TerrainScene.prepare(s,catalog,cache,day,31);
 const signatures=(scene:TerrainScene)=>{
  const columns=new Map<number,unknown[]>();for(const op of scene.operations){const i=scene.columnIndex(op),rows=columns.get(i)??[];rows.push(op);columns.set(i,rows);}
  return new Map([...columns].map(([i,ops])=>[i,JSON.stringify(ops)]));
 };
 for(const read of [built,staged,complete,s]){
  const next=await TerrainScene.prepare(read,catalog,cache,day,31,prior),changed=new Set(next.changedColumns(prior)!);
  const a=signatures(prior),b=signatures(next);
  for(const i of new Set([...a.keys(),...b.keys()]))if(a.get(i)!==b.get(i))assert(changed.has(i),`Changed full-render column ${i} was not invalidated`);
  prior=next;
 }
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

Deno.test('variable stacks match 18 independent source layer-order fixtures including overlay and short top precedence',async()=>{
 const fixtures=JSON.parse(await Deno.readTextFile(new URL('../../doc/parity/city-fixtures.json',import.meta.url)));
 const {instantiate}=await import('../src/lib/content-recipes.ts');
 const template=catalog.renderRecipes.find(r=>r.handler==='variable-height')!;
 for(const row of fixtures.groups.variableRecipes.cases){
  const i=row.input,frame=template.frames[0],frames=[];
  for(const [prefix,n] of [['bottom',i.bottoms],['top',i.tops]] as const)for(let z=0;z<n;z++)frames.push({...frame,key:`${prefix}/${z}/0`,cell:[0,0,0] as [number,number,number]});
  frames.push({...frame,key:'middle/0',cell:[0,0,0]});
  const recipe={...template,fields:{...template.fields,minHeight:'1',maxHeight:'20',bottomCount:i.bottoms,topCount:i.tops,overlay:i.overlay},frames};
  const layers=instantiate(recipe,'1',[0,0,0],{height:i.height});
  const actual=Array.from({length:i.height},(_,z)=>layers.filter(l=>l.position[2]===z).map(l=>l.frame.key.startsWith('middle')?'middle':l.frame.key.split('/').slice(0,2).join(':')));
  assert.deepEqual(actual,row.expected.layers,row.id);
 }
});
Deno.test('all 55 variable definitions at min and max heights bind stacks, gray cuts, stable owners and all appearances',async()=>{
 const {cityProfile}=await import('../src/lib/city-state.ts');const definitions=cityProfile.structures.filter(d=>d.kind==='variable');assert.equal(definitions.length,55);
 for(const d of definitions)for(const height of new Set([d.minimumHeight,d.maximumHeight])){
  const s=cityScene(),b=addCity(s,d,10,22,undefined,0,false,height);assert(validSnapshot(s),d.reference.recipeId);
  for(const season of ['spring','summer','autumn','winter'] as const)for(const night of [false,true]){
   const plan=new CityRenderPlan(s,catalog,{season,night}),recipe=catalog.renderRecipes.find(r=>r.id===d.reference.recipeId)!;
   assert.equal(plan.columnIndices.size,d.sizeX*d.sizeY);
   for(let z=0;z<height;z++){
    const prefix=z>=height-Number(recipe.fields.topCount)?`top/${height-z-1}/`:z<Number(recipe.fields.bottomCount)?`bottom/${z}/`:'middle/';
    assert(plan.layers.filter(l=>l.position[2]===2+z).every(l=>l.frame.key.startsWith(prefix)));
    assert.equal(plan.layers.filter(l=>l.position[2]===2+z).length,recipe.frames.filter(f=>f.key.startsWith(prefix)&&f.status!=='invisible').length);
   }
   const cut=[...plan.columnIndices].flatMap(i=>plan.operations(i,2,0,false));assert.equal(cut.length,d.sizeX*d.sizeY);assert(cut.every(o=>'color' in o&&o.color==='#808080'&&o.target.id===b.id));
  }
  const scene=await TerrainScene.prepare(s,catalog,images(),day,31);let found=false;
  for(const op of ops(scene))if('layer' in op){const l=op.layer,[x,y,z]=l.position,mask=catalog.masks[l.frame.masks!['spring/day']],row=mask.findIndex(r=>r.includes('1'));if(row<0)continue;
   const q={x:16*(x+y)-l.frame.anchor![0]+mask[row].indexOf('1')+.5,y:8*(y-x)-16*z-l.frame.anchor![1]+row+.5};if(scene.pick(q,view)?.id===b.id)found=true;
  }assert(found);
 }
});

Deno.test('variable crop accommodation is bounded, provenance-checked and preserves original diagnostics and invisible occupancy',async()=>{
 const raw=JSON.parse(await Deno.readTextFile(new URL('../public/content/bundled-v2/catalog.json',import.meta.url))) as RenderCatalog,original=JSON.stringify(raw),fixed=variableCityArt(raw);
 const bad=raw.renderRecipes.filter(r=>r.handler==='variable-height').flatMap(r=>r.frames.filter(f=>f.status==='invalid-source-crop').map(f=>[r,f] as const));assert.equal(bad.length,14);
 for(const [r,f] of bad){
  const corrected=fixed.renderRecipes.find(a=>a.id===r.id)!.frames.find(a=>a.key===f.key)!;
  if(corrected.status==='invisible')continue;
  const [x,y,w,h]=corrected.rectangle!;assert(x>=0&&y>=0&&w>0&&h>0&&x+w<=353&&y+h<=352);
  assert.equal(corrected.anchor![0],f.anchor![0]-(x-f.rectangle![0]));assert.equal(corrected.anchor![1],f.anchor![1]);
  assert.equal(Object.keys(corrected.masks!).length,8);
 }
 assert.equal(JSON.stringify(raw),original);assert.equal(variableCityArt(raw),fixed);
 const drift=structuredClone(raw);drift.renderRecipes.find(r=>r.id===bad[0][0].id)!.frames.find(f=>f.key===bad[0][1].key)!.anchor![0]++;
 assert.throws(()=>variableCityArt(drift));
 const s=cityScene();const {cityProfile}=await import('../src/lib/city-state.ts');addCity(s,cityProfile.structures.find(d=>d.kind==='variable')!);
 const broken=structuredClone(raw),recipe=broken.renderRecipes.find(r=>r.id===s.mixed!.city!.structures[0].definition.recipeId)!;recipe.frames=recipe.frames.filter(f=>!f.key.startsWith('top/'));
 assert.throws(()=>new CityRenderPlan(s,broken,day));
});
