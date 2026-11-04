import assert from 'node:assert/strict';
import {TerrainScene} from '../src/lib/terrain-renderer.ts';
import {RecipeImageCache,type RenderCatalog} from '../src/lib/content-recipes.ts';
import {trainSceneFixture} from './helpers/train-scene.ts';
import type {Snapshot} from '../src/generated/contracts.ts';

const catalog=JSON.parse(await Deno.readTextFile(new URL('../public/content/bundled-v2/catalog.json',import.meta.url))) as RenderCatalog;
const source=JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/fixtures/snapshot-fleet.json',import.meta.url))) as Snapshot;
const gc=(globalThis as unknown as {gc?:()=>void}).gc;

Deno.test({name:'successive train scenes release previous scenes after reusing their caches',ignore:!gc,fn:async()=>{
 const cache=new RecipeImageCache(async media=>{
  const binding=Object.values(catalog.surfaceSets).flatMap(Object.values).find(b=>b.image.url===media.url)!;
  return {image:{} as CanvasImageSource,width:binding.width,height:binding.height};
 });
 for(const [width,height] of [[16,32],[200,100]]) {
  const read=trainSceneFixture(source,catalog,catalog.renderRecipes.find(r=>r.handler==='train-composition')!,false,width,height);
  let current:TerrainScene|undefined;
  const references:WeakRef<TerrainScene>[]=[];
  for(let n=0;n<100;n++) {
   current=await TerrainScene.prepare(read,catalog,cache,{season:'spring',night:false},5,current);
   for(const _ of current.viewportOperations({x:0,y:64,zoom:n%2?.5:2},512,256)){ /* Materialize each scene's visible cache. */ }
   references.push(new WeakRef(current));
  }
  // WeakRef targets stay alive for their current job. Cross that boundary
  // before collecting, and retain only the latest scene as the renderer does.
  for(let n=0;n<3;n++){await new Promise(resolve=>setTimeout(resolve,0));gc!();}
  assert.equal(references.at(-1)!.deref(),current);
  assert.equal(references.filter(reference=>reference.deref()).length,1,`${width}×${height} scenes retained obsolete render data`);
 }
}});
