import {trainSceneFixture} from './train-scene.ts';
import source from '../../../prototype/protocol/fixtures/snapshot-fleet.json' with {type:'json'};
import {RailwayRenderPlan} from '../../src/lib/railway-renderer.ts';
import {stationScene} from './station-scene.ts';
import {addPassagePlatform} from './passage-scene.ts';
import {TerrainScene} from '../../src/lib/terrain-renderer.ts';
import {WorldSpace,offset} from '../../src/lib/map-geometry.ts';
import {RecipeImageCache,variantKey,type RenderCatalog,type Variant} from '../../src/lib/content-recipes.ts';
import type {Heading,Snapshot} from '../../src/generated/contracts.ts';
export async function stairPixels(catalog:RenderCatalog,variant:Variant) {
 const cache=new RecipeImageCache(),space=new WorldSpace(64,24,8),canvas=document.createElement('canvas'),expected=document.createElement('canvas');canvas.width=expected.width=120;canvas.height=expected.height=100;
 const ctx=canvas.getContext('2d')!,want=expected.getContext('2d')!,gallery=document.createElement('canvas');gallery.width=8*120;gallery.height=12*100;const contact=gallery.getContext('2d')!;
 let count=0,pixels=0,picks=0;
 const sheets=new Map<string,HTMLImageElement>();
 for(const heading of [0,2,4,6] as Heading[])for(const material of ['concrete','brown','green'] as const)for(const upward of [false,true])for(const roof of [false,true])for(const double of [false,true]){
  const s=stationScene(64,24),p=addPassagePlatform(s,heading);p.yards[0].outlook={kind:'stair',material,bridge:false,upward};p.yards[0].roof=roof;p.roof=roof;const voxel=s.mixed!.voxels.find(v=>v.ownerId===p.yards[0].trafficOwnerId)!;voxel.traffic!.platform={...voxel.traffic!.platform!,contributionId:`system:thin-${heading+(roof?1:0)}`,recipeId:`system:thin-${heading+(roof?1:0)}`};
  if(double){const d=offset((heading+6)%8);addPassagePlatform(s,((heading+4)%8) as Heading,{x:10+d.x,y:10+d.y,z:2});}
  const scene=await TerrainScene.prepare(s,catalog,cache,variant,7),ops=scene.operations.filter(o=>o.target.kind==='railway'&&o.target.component==='platform'&&o.target.platformId===p.id),q=space.project(p.location),view={x:44-q.x,y:42-q.y,zoom:1};
  ctx.clearRect(0,0,120,100);want.clearRect(0,0,120,100);scene.paintOperations(ctx,view,120,100,ops);
  const prefix=`${material}-`,recipe=catalog.renderRecipes.find(r=>r.id===`system:${prefix}passage-0`)!,url=catalog.surfaceSets[recipe.frames[0].surfaceSet!][variantKey(variant)].image.url;
  let sheet=sheets.get(url);if(!sheet){sheet=new Image();sheet.src=url;await sheet.decode();sheets.set(url,sheet);}
  // Literal PassagewayRail constructors; index23 explicitly uses the portable correction.
  const table=[
   [10,80,32,32,6,16],[42,80,32,32,6,16],[10,80,32,32,6,16],[42,80,32,32,6,16],
   [0,120,32,32,0,16],[32,120,32,32,0,16],[0,120,32,32,0,16],[32,120,32,32,0,16],
   [80,76,32,36,0,20],[112,76,32,36,0,20],[192,4,32,36,0,20],[224,4,32,36,0,20],
   [90,120,32,32,6,16],[122,120,32,32,6,16],[90,120,32,32,6,16],[122,120,32,32,6,16],
   [166,120,32,32,-6,16],[198,120,32,32,-6,16],[166,120,32,32,-6,16],[198,120,32,32,-6,16],
   [176,76,32,36,0,20],[208,76,32,36,0,20],[256,4,32,36,0,20],[288,4,32,36,0,20],
   [256,120,32,32,0,16],[288,120,32,32,0,16],[256,120,32,32,0,16],[288,120,32,32,0,16],
   [246,80,32,32,-6,16],[278,80,32,32,-6,16],[246,80,32,32,-6,16],[278,80,32,32,-6,16]];
  const index=heading*4|(upward?0:4)|(double?2:0)|(roof?1:0),[x,y,w,h,ax,ay]=table[index];
  want.drawImage(sheet,x,y,w,h,44-ax,42-ay,w,h);
  const got=ctx.getImageData(0,0,120,100).data,target=want.getImageData(0,0,120,100).data;
  for(let n=0;n<got.length;n++)if(got[n]!==target[n])throw new Error(`Passage pixel ${heading}/${material}/${upward}/${roof}/${double}/${n}`);
  for(let y=0;y<100;y++)for(let x=0;x<120;x++){
   const hit=scene.pickOperations({x:x+.5,y:y+.5},view,ops);
   if(!!hit!==(target[(y*120+x)*4+3]>0)||hit&&(hit.kind!=='railway'||hit.platformId!==p.id))throw new Error('Stair alpha pick');picks++;
  }
  pixels+=120*100;contact.fillStyle='#d8e5d9';contact.fillRect(count%8*120,Math.floor(count/8)*100,120,100);contact.drawImage(canvas,count%8*120,Math.floor(count/8)*100);
  count++;
 }
 return {count,pixels,picks,png:gallery.toDataURL()};
}

export async function stairTrainOverlap(catalog:RenderCatalog){
 const cache=new RecipeImageCache(),stock=catalog.renderRecipes.find(r=>r.handler==='train-composition')!,variant={season:'spring' as const,night:false};
 let count=0,picks=0;
 for(const heading of [0,2,4,6] as Heading[])for(const material of ['concrete','brown','green'] as const){
  const s=trainSceneFixture(source as Snapshot,catalog,stock),v=s.mixed!.voxels[0],at=v.position,template=stationScene(64,24),p=addPassagePlatform(template,heading);
  p.location=at;p.yards[0].location=at;p.yards[0].trafficOwnerId=v.ownerId;p.yards[0].outlook={kind:'stair',material,upward:false,bridge:false};
  s.mixed!.stationGraph={nextStationName:1,nextPlatformName:1,buildings:[],platforms:[p],passages:[],listeners:[],listenerMemberships:[]};
  v.traffic!.platform={...p.bell,contributionId:`system:thin-${heading}`,recipeId:`system:thin-${heading}`};
  const plan=new RailwayRenderPlan(s,catalog,variant),space=new WorldSpace(16,32,6),cell=space.storage(at),ops=plan.operations(cell.v*16+cell.h,5,0,true,2).filter(o=>o.target.kind==='train'||o.target.component==='platform');
  const stair=ops.find(o=>o.target.kind==='railway')!,car=ops.find(o=>o.target.kind==='train')!;
  if(!stair||!car||ops.length!==2||ops[heading===2||heading===4?0:1]!==stair)throw new Error('Stair/train source ordering');
  if(plan.operations(cell.v*16+cell.h,1,0,true,2).some(o=>o.target.kind==='train'||o.target.component==='platform'))throw new Error('Stair cut leak');
  const layers=ops.map(o=>({...o.layer,position:[0,0,0] as [number,number,number]})),expected=(heading===2||heading===4?[stair,car]:[car,stair]).map(o=>({...o.layer,position:[0,0,0] as [number,number,number]}));
  const ready=await cache.ready(catalog,layers,variant),a=document.createElement('canvas'),b=document.createElement('canvas');a.width=b.width=80;a.height=b.height=80;
  const view={x:24,y:40,zoom:1},ctx=a.getContext('2d')!,want=b.getContext('2d')!;ready.draw(ctx,view,{kind:'normal'},layers);
  for(const layer of expected)ready.paintNormal(want,view,layer);
  const got=ctx.getImageData(0,0,80,80).data,target=want.getImageData(0,0,80,80).data;
  for(let n=0;n<got.length;n++)if(got[n]!==target[n])throw new Error('Stair/train overlap pixels');
  for(let y=0;y<80;y++)for(let x=0;x<80;x++){
   const hit=ready.pick(x+.5,y+.5,view,layers),desired=expected.toReversed().map(l=>ready.pick(x+.5,y+.5,view,[l])).find(Boolean);
   if(hit?.recipeId!==desired?.recipeId)throw new Error('Stair/train overlap pick');picks++;
  }
  count++;
 }
 return {count,picks};
}
