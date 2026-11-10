import {stationScene} from './station-scene.ts';
import {addPassagePlatform} from './passage-scene.ts';
import {TerrainScene} from '../../src/lib/terrain-renderer.ts';
import {WorldSpace,offset} from '../../src/lib/map-geometry.ts';
import {RecipeImageCache,variantKey,type RenderCatalog,type Variant} from '../../src/lib/content-recipes.ts';
import type {Heading} from '../../src/generated/contracts.ts';
export async function passagePixels(catalog:RenderCatalog,variant:Variant) {
 const cache=new RecipeImageCache(),space=new WorldSpace(64,24,8),canvas=document.createElement('canvas'),expected=document.createElement('canvas');canvas.width=expected.width=120;canvas.height=expected.height=100;
 const ctx=canvas.getContext('2d')!,want=expected.getContext('2d')!,gallery=document.createElement('canvas');gallery.width=8*120;gallery.height=6*100;const contact=gallery.getContext('2d')!;
 let count=0,pixels=0,picks=0;
 const sheets=new Map<string,HTMLImageElement>();
 for(const heading of [0,2,4,6] as Heading[])for(const material of ['concrete','brown','green'] as const)for(const bridge of [false,true])for(const double of [false,true]){
  const s=stationScene(64,24),p=addPassagePlatform(s,heading);p.yards[0].outlook={kind:'passage',material,bridge,upward:false};
  if(double){const d=offset((heading+6)%8);addPassagePlatform(s,((heading+4)%8) as Heading,{x:10+d.x,y:10+d.y,z:2});}
  const scene=await TerrainScene.prepare(s,catalog,cache,variant,7),ops=scene.operations.filter(o=>o.target.kind==='railway'&&o.target.component==='platform'&&o.target.platformId===p.id),q=space.project(p.location),view={x:44-q.x,y:42-q.y,zoom:1};
  ctx.clearRect(0,0,120,100);want.clearRect(0,0,120,100);scene.paintOperations(ctx,view,120,100,ops);
  const prefix=`${material}-`,recipe=catalog.renderRecipes.find(r=>r.id===`system:${prefix}passage-0`)!,url=catalog.surfaceSets[recipe.frames[0].surfaceSet!][variantKey(variant)].image.url;
  let sheet=sheets.get(url);if(!sheet){sheet=new Image();sheet.src=url;await sheet.decode();sheets.set(url,sheet);}
  const index=heading/2;
  // Source PassagewayRail: each direction has a single crop and two double crops.
  const crop=double?[(index*2+(bridge?2:1))*32,40]:[index*32,8];
  want.drawImage(sheet,crop[0],crop[1],32,32,44,26,32,32);
  if(bridge&&(heading===2||heading===4))want.drawImage(sheet,heading===2?160:128,8,32,32,44,26,32,32);
  const got=ctx.getImageData(0,0,120,100).data,target=want.getImageData(0,0,120,100).data;
  for(let n=0;n<got.length;n++)if(got[n]!==target[n])throw new Error(`Passage pixel ${heading}/${material}/${bridge}/${double}/${n}`);
  for(let y=0;y<100;y++)for(let x=0;x<120;x++){
   const hit=scene.pickOperations({x:x+.5,y:y+.5},view,ops);
   if(!!hit!==(target[(y*120+x)*4+3]>0)||hit&&(hit.kind!=='railway'||hit.platformId!==p.id))throw new Error('Passage alpha pick');picks++;
  }
  pixels+=120*100;contact.fillStyle='#d8e5d9';contact.fillRect(count%8*120,Math.floor(count/8)*100,120,100);contact.drawImage(canvas,count%8*120,Math.floor(count/8)*100);
  count++;
 }
 return {count,pixels,picks,png:gallery.toDataURL()};
}
