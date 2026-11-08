// Browser-only tests: source declaration geometry is independent of the live
// renderer. No harness or source review is shipped in the production bundle.
import sourceReview from '../../../doc/parity/exav-content-review.json' with {type:'json'};
import source from '../../../prototype/protocol/fixtures/snapshot-fleet.json' with {type:'json'};
import {stationScene,addStation,stationDefinitions} from './station-scene.ts';
import {trainSceneFixture} from './train-scene.ts';
import {TerrainScene} from '../../src/lib/terrain-renderer.ts';
import {TerrainViewport} from '../../src/lib/terrain-viewport.ts';
import {WorldSpace} from '../../src/lib/map-geometry.ts';
import {RecipeImageCache,variantKey,type RenderCatalog,type Variant} from '../../src/lib/content-recipes.ts';
import type {Snapshot} from '../../src/generated/contracts.ts';

// DefaultSpriteLoader.load3D: independently calculate source crops from pinned
// XML, without reading compiled recipe cells, rectangles or anchors.
function sourceFrames(id:string) {
  const e=sourceReview.recipes.find(r=>r.contributionId===id)!.declaration;
  const text=(tag:string)=>e.children.find(c=>c.tag===tag)!.text;
  const [X,Y]=text('size').split(',').map(Number),Z=Number(text('height')),a=e.children.find(c=>c.tag==='sprite')!.attributes as Record<string,string>;
  const [ox,oy]=a.origin.split(',').map(Number),offset=a.offset===undefined?8*(2*Z+X-1):Number(a.offset);
  const frames=[];
  for(let y=0;y<Y;y++)for(let x=0;x<X;x++)for(let z=0;z<Z;z++){
    const vx=ox+16*(x+y),vy=oy+offset+8*(y-x)-16*z;
    if(z===Z-1){
      let sx=vx,sy=vy,w=32,h=16;
      if(y===0||x===X-1){sy-=16;h+=16;if(y===0&&x<X-1&&Y>1)w=16;else if(x===X-1&&y>0&&X>1){sx+=16;w-=16;}}
      if(sy<0){h+=sy;sy=0;}if(h>0)frames.push({cell:[x,y,z],rect:[sx,sy,w,h],anchor:[vx-sx,vy-sy]});
    }else if(z===0&&(x===0||y===Y-1)){
      const sx=vx+(x===0?0:16),sy=vy-16*(Z-2)-8,w=x===0&&y===Y-1?32:16;
      frames.push({cell:[x,y,z],rect:[sx,sy,w,16*(Z-1)+8],anchor:[vx-sx,vy-sy]});
    }
  }
  return frames;
}
export async function stationGallery(catalog:RenderCatalog,variant:Variant) {
  const cache=new RecipeImageCache(),key=variantKey(variant),gallery=document.createElement('canvas');gallery.width=5*400;gallery.height=7*220;
  const contact=gallery.getContext('2d')!,actual=new OffscreenCanvas(400,220),expected=new OffscreenCanvas(400,220),ctx=actual.getContext('2d')!,want=expected.getContext('2d')!;
  let pixels=0,picks=0;
  for(const [i,d] of stationDefinitions.entries()){
    const s=stationScene(),b=addStation(s,d),space=new WorldSpace(24,40,8),scene=await TerrainScene.prepare(s,catalog,cache,variant,7),ops=scene.operations.filter(o=>o.target.kind==='station');
    const q=space.project(b.location),view={x:50-q.x,y:90-q.y,zoom:1};ctx.clearRect(0,0,400,220);want.clearRect(0,0,400,220);
    scene.paintOperations(ctx as unknown as CanvasRenderingContext2D,view,400,220,ops);
    const recipe=catalog.renderRecipes.find(r=>r.id===d.reference.recipeId)!,sheet=new Image();sheet.src=catalog.surfaceSets[recipe.frames.find(f=>f.surfaceSet)!.surfaceSet!][key].image.url;await sheet.decode();
    const frames=sourceFrames(d.reference.contributionId).map(f=>({...f,p:{x:b.location.x+f.cell[0],y:b.location.y+f.cell[1],z:2+f.cell[2]}}));
    frames.sort((a,b)=>{const p=space.storage(a.p),q=space.storage(b.p);return p.v-q.v||p.h-q.h||p.z-q.z;});
    for(const f of frames){const p=space.project(f.p),[sx,sy,w,h]=f.rect;want.drawImage(sheet,sx,sy,w,h,view.x+p.x-f.anchor[0],view.y+p.y-f.anchor[1],w,h);}
    const got=ctx.getImageData(0,0,400,220).data,target=want.getImageData(0,0,400,220).data;
    for(let n=0;n<got.length;n++)if(got[n]!==target[n])throw new Error(`Station crop pixel ${d.reference.recipeId}/${key}/${n}`);
    for(let y=0;y<220;y++)for(let x=0;x<400;x++){
      const hit=scene.pickOperations({x:x+.5,y:y+.5},view,ops);
      if(!!hit!==(target[(y*400+x)*4+3]>0)||hit&&(hit.kind!=='station'||hit.id!==b.id))throw new Error('Station alpha pick mismatch');picks++;
    }
    pixels+=400*220;contact.fillStyle='#d8e5d9';contact.fillRect(i%5*400,Math.floor(i/5)*220,400,220);contact.drawImage(actual,i%5*400,Math.floor(i/5)*220);
    contact.fillStyle='#17232b';contact.font='12px sans-serif';contact.fillText(`${i+1}: ${d.sizeX}×${d.sizeY}×${d.height}`,i%5*400+8,Math.floor(i/5)*220+204);
  }
  return {png:gallery.toDataURL(),stations:stationDefinitions.length,pixels,picks};
}
export async function stationLocalPixels(catalog:RenderCatalog) {
  const s=trainSceneFixture(source as Snapshot,catalog,catalog.renderRecipes.find(r=>r.handler==='train-composition')!,false,200,100);
  s.mixed!.stationGraph={nextStationName:1,nextPlatformName:1,buildings:[],platforms:[],passages:[],listeners:[],listenerMemberships:[]};
  const built=structuredClone(s),space=new WorldSpace(200,100,6),head=s.mixed!.fleet!.trains[0].cars[0].state.location!,p=space.storage({...head,x:head.x-1,y:head.y+1});
  addStation(built,stationDefinitions.find(d=>d.sizeX===1&&d.sizeY===2&&d.height===2)!,p.h,p.v);
  const cache=new RecipeImageCache(),variant:Variant={season:'spring',night:false},empty=await TerrainScene.prepare(s,catalog,cache,variant,5),viewport=new TerrainViewport(empty),q=space.project(head),view={x:180-q.x,y:150-q.y,zoom:1};
  const canvas=document.createElement('canvas');canvas.width=400;canvas.height=300;const ctx=canvas.getContext('2d')!;
  const settle=(vp:TerrainViewport)=>{for(let n=0;n<1000;n++){ctx.clearRect(0,0,400,300);const r=vp.draw(ctx,view,400,300,1);if(!r.pending)return r;}throw new Error('Station viewport never settled');};
  settle(viewport);const reports=[];let previous=empty;
  for(const read of [built,s]){
    const scene=await TerrainScene.prepare(read,catalog,cache,variant,5,previous);viewport.setScene(scene);const result=settle(viewport),png=canvas.toDataURL(),actual=ctx.getImageData(0,0,400,300).data;
    const fresh=await TerrainScene.prepare(read,catalog,cache,variant,5),full=new TerrainViewport(fresh);settle(full);const expected=ctx.getImageData(0,0,400,300).data;
    for(let i=0;i<actual.length;i++)if(actual[i]!==expected[i])throw new Error(`Station local/full raster mismatch ${i}`);
    let picks=0;for(let y=50;y<250;y+=2)for(let x=50;x<350;x+=2){const point={x:x+.5,y:y+.5};if(JSON.stringify(viewport.pick(point,view))!==JSON.stringify(full.pick(point,view)))throw new Error('Station local/full pick mismatch');picks++;}
    reports.push({png,picks,kind:result.updateKind,columns:scene.changedColumns(previous)});previous=scene;
  }
  return reports;
}
