import assert from 'node:assert/strict';
import {stationScene,addStation,addRail,stationDefinitions} from './helpers/station-scene.ts';
import {TerrainScene} from '../src/lib/terrain-renderer.ts';
import {RecipeImageCache,type RenderCatalog,type Variant} from '../src/lib/content-recipes.ts';
import {WorldSpace} from '../src/lib/map-geometry.ts';
import {validSnapshot} from '../src/lib/world-identity.ts';
import {stationNamePositions,resolveStationLabels,defaultStationLabels} from '../src/lib/station-labels.ts';
const catalog=JSON.parse(await Deno.readTextFile(new URL('../public/content/bundled-v2/catalog.json',import.meta.url))) as RenderCatalog;
const fixtures=JSON.parse(await Deno.readTextFile(new URL('../../doc/parity/station-fixtures.json',import.meta.url)));
const day:Variant={season:'spring',night:false},view={x:0,y:0,zoom:1};
const images=(c=catalog,onDecode=()=>{})=>new RecipeImageCache(async media=>{onDecode();const b=Object.values(c.surfaceSets).flatMap(Object.values).find(b=>b.image.url===media.url)!;return {image:{} as CanvasImageSource,width:b.width,height:b.height};});
const stationOps=(s:TerrainScene)=>s.operations.filter(o=>o.target.kind==='station');
function opaquePick(scene:TerrainScene,id:string,variant=day) {
  for(const op of stationOps(scene))if('layer' in op){
    const l=op.layer,[x,y,z]=l.position,[ax,ay]=l.frame.anchor!,mask=catalog.masks[l.frame.masks![`${variant.season}/${variant.night?'night':'day'}`]];
    for(let b=0;b<mask.length;b++)for(let a=0;a<mask[b].length;a++)if(mask[b][a]==='1') {
      const p={x:16*(x+y)-ax+a+.5,y:8*(y-x)-16*z-ay+b+.5};
      if(scene.pick(p,view)?.id===id)return {p,target:scene.pick(p,view)!};
    }
  }
  throw new Error('No visible station pixel '+id);
}
Deno.test('all 35 independently specified station footprints render immediately and pick stable identities in eight variants',async()=>{
  assert.equal(stationDefinitions.length,35);
  for(const d of stationDefinitions){
    const row=fixtures.groups.stations.cases.find((r:any)=>r.input.definition===d.reference.contributionId);
    assert.deepEqual([d.sizeX,d.sizeY,d.height],row.expected.size);
    const s=stationScene(),b=addStation(s,d);assert(validSnapshot(s),d.reference.contributionId);const bytes=JSON.stringify(s);
    const recipe=catalog.renderRecipes.find(r=>r.id===d.reference.recipeId)!;
    for(const season of ['spring','summer','autumn','winter'] as const)for(const night of [false,true]) {
      const variant={season,night},scene=await TerrainScene.prepare(s,catalog,images(),variant,7),ops=stationOps(scene);
      assert.equal(ops.length,recipe.frames.filter(f=>f.status!=='invisible').length);
      assert(ops.every(o=>o.target.id===b.id&&o.target.kind==='station'&&o.target.definition===b.definition));
      const hit=opaquePick(scene,b.id,variant);assert.equal(hit.target.kind,'station');
      assert.equal(scene.pick({x:hit.p.x*2+7,y:hit.p.y*2+9},{x:7,y:9,zoom:2})?.id,b.id);
      assert(!scene.operations.some(o=>o.target.kind==='railway'),'Station is not a stationary accessory');
      assert.equal(JSON.stringify(s),bytes);
    }
  }
});
Deno.test('Hakodate literal source crops and anchors use the actual member positions and V/H/Z traversal',async()=>{
  const s=stationScene(),b=addStation(s,stationDefinitions.find(d=>d.reference.contributionId==='{3D64E31D-0579-4d37-920A-5A5201A8C984}')!);
  const scene=await TerrainScene.prepare(s,catalog,images(),day,7),ops=stationOps(scene);
  // Pinned terminal1.bmp: origin0,0 offset16, X1/Y2/Z2; DefaultSpriteLoader.load3D.
  const expected=[[[0,0,1],[0,0,32,16],[0,0]],[[0,0,0],[0,8,16,24],[0,8]],[[0,1,1],[16,0,32,24],[0,8]],[[0,1,0],[16,16,32,24],[0,8]]];
  for(const [cell,rectangle,anchor] of expected){
    const op=ops.find(o=>o.target.position.x===b.location.x+cell[0]&&o.target.position.y===b.location.y+cell[1]&&o.target.position.z===b.location.z+cell[2]);
    assert(op&&'layer' in op);assert.deepEqual(op.layer.frame.rectangle,rectangle);assert.deepEqual(op.layer.frame.anchor,anchor);
    assert.deepEqual(op.layer.position,[b.location.x-19+cell[0],b.location.y+cell[1],2+cell[2]]);
  }
  const space=new WorldSpace(24,40,8),order=scene.operations.map(o=>space.storage(o.target.position));
  for(let i=1;i<order.length;i++){const a=order[i-1],b=order[i];assert(b.v>a.v||b.v===a.v&&(b.h>a.h||b.h===a.h&&b.z>=a.z));}
});
Deno.test('intersecting cuts draw every base member Gray including invisible voxels, suppress ground, and pick the same station',async()=>{
  for(const d of stationDefinitions){
    const s=stationScene(),b=addStation(s,d),space=new WorldSpace(24,40,8);
    for(let cut=0;cut<=2+d.height;cut++){
      const scene=await TerrainScene.prepare(s,catalog,images(),day,cut),ops=stationOps(scene);
      if(cut<2){assert.equal(ops.length,0);continue;}
      if(cut>=2+d.height){assert(ops.every(o=>'layer' in o));continue;}
      assert.equal(ops.length,d.sizeX*d.sizeY);
      for(const op of ops){assert('points' in op);assert.equal(op.color,'#808080');const p=space.center(op.target.position);assert.equal(scene.pick(p,view)?.id,b.id);}
      assert(!scene.operations.some(o=>o.target.kind==='terrain'&&s.entities![0].footprint.some(f=>f.offset.z===0&&o.target.position.x===b.location.x+f.offset.x&&o.target.position.y===b.location.y+f.offset.y)));
    }
  }
});
Deno.test('station placement/removal damages full footprints and neighboring grid masks while preserving unrelated operations and image cache',async()=>{
  const s=stationScene(),railId=addRail(s,20,30),cache=images(catalog,()=>decodes++);let decodes=0;
  const empty=await TerrainScene.prepare(s,catalog,cache,day,7),built=structuredClone(s),b=addStation(built,stationDefinitions.at(-1)!);
  const scene=await TerrainScene.prepare(built,catalog,cache,day,7,empty),count=decodes,changed=scene.changedColumns(empty)!;
  const space=new WorldSpace(24,40,8);
  for(const f of built.entities!.find(e=>e.id===b.id)!.footprint){const p=space.storage({x:b.location.x+f.offset.x,y:b.location.y+f.offset.y,z:b.location.z});assert(changed.includes(p.v*24+p.h));}
  const masks=scene.operations.filter(o=>o.terrainMasks?.length);assert(masks.length>0);
  const oldRail=empty.operations.find(o=>o.target.id===railId&&o.target.kind==='railway');assert(scene.operations.includes(oldRail!));
  const noChange=await TerrainScene.prepare(built,catalog,cache,day,7,scene);assert.equal(decodes,count);assert.deepEqual(noChange.changedColumns(scene),[]);
  const removed=await TerrainScene.prepare(s,catalog,cache,day,7,scene);assert.equal(stationOps(removed).length,0);
  for(const i of changed)assert(removed.changedColumns(scene)!.includes(i));
  assert.deepEqual(removed.operations,(await TerrainScene.prepare(s,catalog,images(),day,7)).operations);
  assert(stationOps(scene).length>0,'Previous immutable picks retained');
});
Deno.test('missing or malformed station art blocks readiness even above the cut and offscreen',async()=>{
  const s=stationScene(200,100),b=addStation(s,stationDefinitions[0],150,70);
  for(const mutate of [(c:RenderCatalog)=>{c.renderRecipes=c.renderRecipes.filter(r=>r.id!==b.definition.recipeId);},(c:RenderCatalog)=>{c.renderRecipes.find(r=>r.id===b.definition.recipeId)!.frames[0].status='invalid-source-crop';},(c:RenderCatalog)=>{delete c.renderRecipes.find(r=>r.id===b.definition.recipeId)!.frames[0].masks!['spring/day'];},(c:RenderCatalog)=>{c.renderRecipes.find(r=>r.id===b.definition.recipeId)!.frames[0].cell=[1,0,0];}]){
    const c=structuredClone(catalog);mutate(c);await assert.rejects(()=>TerrainScene.prepare(s,c,images(c),day,0));
  }
});
Deno.test('water and underground cut surfaces respect opaque station occupancy and full-height underwater traversal',async()=>{
  const s=stationScene(),b=addStation(s,stationDefinitions.find(d=>d.height===2)!);s.terrain!.waterLevel=3;
  const scene=await TerrainScene.prepare(s,catalog,images(),day,7);
  assert(stationOps(scene).every(o=>o.target.position.z===3));
  assert(!scene.operations.some(o=>o.target.kind==='terrain'&&o.target.position.x===b.location.x&&o.target.position.y===b.location.y));
  // A controlled loaded-state drawing witness, not legal underground placement.
  const buried=structuredClone(s);buried.terrain!.waterLevel=0;for(const c of buried.terrain!.cells)c.ground=4;
  assert(validSnapshot(buried));
  const cut=await TerrainScene.prepare(buried,catalog,images(),day,2),p=new WorldSpace(24,40,8).center(b.location);
  assert.equal(cut.pick(p,view)?.id,b.id);assert(stationOps(cut).every(o=>'points' in o));
  assert(!cut.operations.some(o=>o.target.kind==='terrain'&&o.target.position.x===b.location.x&&o.target.position.y===b.location.y));
});
Deno.test('station labels preserve stored bilingual names, literal base Y-16 projection and independent bounded local preferences',()=>{
  const s=stationScene(),b=addStation(s,stationDefinitions.at(-1)!);assert.deepEqual(stationNamePositions(s),[{id:b.id,name:b.name,x:192,y:64}]);
  assert.deepEqual(resolveStationLabels(null),defaultStationLabels);
  assert.deepEqual(resolveStationLabels({visible:false,font:'serif',size:20,color:'#12abef'}),{visible:false,font:'serif',size:20,color:'#12abef'});
  assert.deepEqual(resolveStationLabels({visible:9,font:'url(x)',size:200,color:'bad'}),defaultStationLabels);
});
