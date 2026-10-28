import { TerrainScene, mountainPoints, treeOffset, mountainColor, terrainColor, hsv } from '../src/lib/terrain-renderer.ts';
import { RecipeImageCache, type RenderCatalog, type Variant } from '../src/lib/content-recipes.ts';
import type { Snapshot } from '../src/generated/contracts.ts';
import { WorldSpace } from '../src/lib/map-geometry.ts';
function assert(v:unknown,message='Assertion failed'):asserts v {if(!v)throw new Error(message);}
function equal(a:unknown,b:unknown){assert(JSON.stringify(a)===JSON.stringify(b),`${JSON.stringify(a)} != ${JSON.stringify(b)}`);}
const catalog=JSON.parse(await Deno.readTextFile(new URL('../public/content/bundled-v2/catalog.json',import.meta.url))) as RenderCatalog;
const original=JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/fixtures/snapshot-terrain.json',import.meta.url))) as Snapshot;
const day:Variant={season:'spring',night:false};
const cache=()=>new RecipeImageCache(async media=>{const b=Object.values(catalog.surfaceSets).flatMap(Object.values).find(b=>b.image.url===media.url)!;return {image:{} as CanvasImageSource,width:b.width,height:b.height};});
function flat(w=1,h=1,g=1):Snapshot {
  const s=structuredClone(original);s.map.width=w;s.map.height=h;s.entities=[];s.terrain!.occupants=[];s.terrain!.waterLevel=0;
  s.terrain!.cells=Array.from({length:w*h},()=>({ground:g,surface:'cut'}));
  const request=JSON.parse(s.terrain!.provenance.requestJson);request.Dimensions={H:w,V:h,Z:6};s.terrain!.provenance.requestJson=JSON.stringify(request);return s;
}
function mountain(cs=[4,0,0,0],forest=false) {
  const s=flat(),o=structuredClone(original.terrain!.occupants[1]),e=structuredClone(original.entities![1]);
  [o.ne,o.se,o.sw,o.nw]=cs;if(!forest)o.forest=null;
  e.anchor={x:0,y:0,z:1};e.value=forest?'100':'0';e.footprint[0].transparent=cs.every(c=>c===0);
  s.entities=[e];s.terrain!.occupants=[o];return s;
}
const view={x:0,y:0,zoom:1};
Deno.test('terrain projection uses diagonal NE/SE/SW/NW quarters and source tree slope arithmetic',()=>{
  equal(mountainPoints({ne:4,se:1,sw:0,nw:2}),[{x:16,y:-16},{x:32,y:4},{x:16,y:16},{x:0,y:0}]);
  equal(treeOffset({ne:4,sw:0},{x:30,y:15}),{x:30,y:22});
  equal(treeOffset({ne:0,sw:4},{x:17,y:9}),{x:17,y:2});
  equal(treeOffset({ne:1,sw:0},{x:16,y:3}),{x:16,y:-5});
  equal(hsv([60,162,90]),[63,96,31]);equal(terrainColor([63,96,31],true),'#0f1807');
  const o={ne:4,se:0,sw:0,nw:0};
  assert(mountainColor(o,day,false)!==mountainColor(o,{season:'winter',night:false},false));
  equal(mountainColor(o,day,true,true),mountainColor(o,{season:'winter',night:true},true,true));
});
Deno.test('terrain picks literal independent ground, sloping face, clipping plane, sea plane and underwater targets',async()=>{
  const ground=await TerrainScene.prepare(flat(),catalog,cache(),day,5);
  equal(ground.pick({x:16,y:-8},view)?.position,{x:0,y:0,z:1});
  assert(!ground.pick({x:0,y:-16},view));assert(!ground.pick({x:-1,y:-8},view));
  const slope=await TerrainScene.prepare(mountain(),catalog,cache(),day,5);
  equal(slope.pick({x:16,y:-25},view)?.surface,'mountain');
  equal(slope.pick({x:48,y:-25},{x:16,y:25,zoom:2})?.surface,'mountain');
  const clipped=await TerrainScene.prepare(mountain(),catalog,cache(),day,1);
  assert(!clipped.pick({x:16,y:-25},view));equal(clipped.pick({x:16,y:-8},view)?.surface,'mountain');
  const underground=await TerrainScene.prepare(flat(1,1,3),catalog,cache(),day,1);
  equal(underground.pick({x:16,y:-8},view)?.surface,'underground');
  const sea=flat();sea.terrain!.waterLevel=3;
  const full=await TerrainScene.prepare(sea,catalog,cache(),day,5);
  equal(full.pick({x:16,y:-40},view)?.surface,'water');assert(!full.pick({x:16,y:-8},view));
  const under=await TerrainScene.prepare(sea,catalog,cache(),day,4);
  equal(under.pick({x:16,y:-8},view)?.surface,'ground');assert(!under.pick({x:16,y:-40},view));
});
Deno.test('terrain stored H/V boundaries include every asymmetric edge without phantom border cells',async()=>{
  const s=flat(2,3),scene=await TerrainScene.prepare(s,catalog,cache(),day,5),space=new WorldSpace(2,3,6);
  // Literal source coordinates for the four H/V corners, at z1.
  for(const [p,q] of [[{x:1,y:0,z:1},{x:16,y:-8}],[{x:2,y:1,z:1},{x:48,y:-8}],[{x:0,y:1,z:1},{x:16,y:8}],[{x:1,y:2,z:1},{x:48,y:8}]] as const) {
    equal(space.center(p),q);equal(scene.pick(q,view)?.position,p);
  }
  assert(!scene.pick({x:-2,y:0},view));assert(!scene.pick({x:82,y:0},view));
});
Deno.test('realized trees use selected sprite, saved order and alpha masks across all season/night variants without new decodes on revisit',async()=>{
  const s=mountain([0,0,0,0],true);s.terrain!.occupants[0].forest!.trees=[{x:16,y:8,sprite:0}];
  const before=JSON.stringify(s);let decodes=0;const images=new RecipeImageCache(async media=>{decodes++;const b=Object.values(catalog.surfaceSets).flatMap(Object.values).find(b=>b.image.url===media.url)!;return {image:{} as CanvasImageSource,width:b.width,height:b.height};});
  for(const season of ['spring','summer','autumn','winter'] as const)for(const night of [false,true]) {
    const scene=await TerrainScene.prepare(s,catalog,images,{season,night},5);
    // Cedar tree's opaque centre is above its terrain diamond; its corner is transparent.
    equal(scene.pick({x:16,y:-35},view)?.surface,'forest');assert(!scene.pick({x:0,y:-48},view));
    const layers=scene.operations.filter(op=>'layer' in op).map(op=>'layer' in op?op.layer:undefined);
    equal(layers.at(-1)?.frame.key,'tree/0');
  }
  const count=decodes;await TerrainScene.prepare(s,catalog,images,day,5);equal(decodes,count);
  equal(JSON.stringify(s),before);
  equal(s.terrain!.occupants[0].forest!.trees,[{x:16,y:8,sprite:0}]);
});
Deno.test('terrain missing recipes fail readiness and preserve diagnosed source crop deferrals',async()=>{
  const c=structuredClone(catalog);c.renderRecipes=c.renderRecipes.filter(r=>r.id!=='system:ground/spring');
  let rejected=false;try{await TerrainScene.prepare(flat(),c,cache(),day,5);}catch{rejected=true;}assert(rejected);
  const invalid=catalog.renderRecipes.flatMap(r=>r.frames).filter(f=>f.status==='invalid-source-crop').length;assert(invalid>0);
  await TerrainScene.prepare(original,catalog,cache(),day,5);
  equal(catalog.renderRecipes.flatMap(r=>r.frames).filter(f=>f.status==='invalid-source-crop').length,invalid);
});
Deno.test('creek connection variants and terrace front pixels retain entity targets and ground occlusion',async()=>{
  for(let mask=0;mask<16;mask++) {
    const s=flat(),e=structuredClone(original.entities![2]),o=structuredClone(original.terrain!.occupants[2]);
    e.anchor={x:0,y:0,z:1};o.creekConnections=mask;s.entities=[e];s.terrain!.occupants=[o];
    const scene=await TerrainScene.prepare(s,catalog,cache(),day,5);
    const creek=scene.operations.find(op=>'layer' in op&&op.layer.recipeId===e.definition.recipeId)!;
    assert('layer' in creek);equal(creek.layer.frame.selector.connections,[1,4,5].includes(mask)?5:[2,8,10].includes(mask)?10:mask);
    equal(scene.pick({x:16,y:-8},view)?.id,'3');
  }
  const s=flat(1,1,2),e=structuredClone(original.entities![0]),o=structuredClone(original.terrain!.occupants[0]);e.anchor={x:0,y:0,z:1};s.entities=[e];s.terrain!.occupants=[o];s.terrain!.cells[0].surface='terrace';
  const scene=await TerrainScene.prepare(s,catalog,cache(),day,5);
  equal(scene.pick({x:16,y:-2},view)?.surface,'terrace');equal(scene.pick({x:16,y:-24},view)?.surface,'ground');
  const cut=await TerrainScene.prepare(s,catalog,cache(),day,0);equal(cut.pick({x:16,y:8},view)?.surface,'underground');
  const terraceCut=await TerrainScene.prepare(s,catalog,cache(),day,1);equal(terraceCut.pick({x:16,y:-8},view)?.surface,'terrace');
});
Deno.test('all 18 exact forest treatments render overlays only on flat Mountains and cliffs use pinned quarter recipes',async()=>{
  const profile=JSON.parse(await Deno.readTextFile(new URL('../src/generated/terrain-profile.json',import.meta.url)));
  for(const definition of profile.definitions.filter((d:any)=>d.treeSpriteCount>0)) {
    const s=mountain([0,0,0,0],true);s.terrain!.occupants[0].forest!.definition=definition.reference;
    s.terrain!.occupants[0].forest!.trees=[{x:16,y:8,sprite:0}];
    const scene=await TerrainScene.prepare(s,catalog,cache(),day,5);
    const overlay=scene.operations.some(op=>'layer' in op&&op.layer.frame.key==='ground');
    equal(overlay,/-[mg]$/.test(definition.reference.recipeId));
    s.terrain!.occupants[0].ne=4;s.entities![0].footprint[0].transparent=false;
    const slope=await TerrainScene.prepare(s,catalog,cache(),day,5);
    assert(!slope.operations.some(op=>'layer' in op&&op.layer.frame.key==='ground'));
  }
  const scene=await TerrainScene.prepare(mountain([0,1,2,3]),catalog,cache(),day,5);
  const cliffs=scene.operations.filter(op=>'layer' in op&&op.layer.recipeId.startsWith('system:cliff/'));
  equal(cliffs.map(op=>'layer' in op?op.layer.recipeId:''),['system:cliff/0/3/2','system:cliff/1/2/1']);
});
Deno.test('terrain palettes match every HSV entry and raw hash in the bundled pinned source',async()=>{
  const {default:copy}=await import('../src/lib/terrain-palettes.json',{with:{type:'json'}});
  const {sha256}=await import('../src/lib/content-recipes.ts');
  const bytes=await Deno.readFile(new URL('../../content/bundled-v2/source/'+copy.source,import.meta.url));
  equal(await sha256(bytes),copy.sha256);const xml=new TextDecoder().decode(bytes);
  for(const [season,colors] of Object.entries(copy.palettes)) {
    const body=new RegExp(`<${season}>([\\s\\S]*?)</${season}>`).exec(xml)![1];
    equal([...body.matchAll(/<color type="hsv">([^<]+)<\/color>/g)].map(m=>m[1].split(',').map(Number)),colors);
  }
});
