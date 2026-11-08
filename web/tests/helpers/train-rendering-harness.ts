import {trainTurnScene} from './train-turn-scene.ts';
import {trainSlopeScene} from './train-slope-scene.ts';
import {normalPorts} from '../../src/lib/world-identity.ts';
import {TrainCompositor} from '../../src/lib/train-compositor.ts';
import {movingCarLayer,compareTrainLayers} from '../../src/lib/train-motion.ts';
// Bundled only by the focused browser test and served through a test route.
// Uses the production pose/painter/picker with actual decoded prepared PNGs.
import {trainCarLayer} from '../../src/lib/train-renderer.ts';
import {RecipeImageCache,variantKey,type RenderCatalog,type Variant} from '../../src/lib/content-recipes.ts';
import fixtures from '../../../doc/parity/train-fixtures.json' with {type:'json'};
import source from '../../../prototype/protocol/fixtures/snapshot-fleet.json' with {type:'json'};
import mixedSource from '../../../prototype/protocol/fixtures/snapshot-mixed.json' with {type:'json'};
import type {Snapshot} from '../../src/generated/contracts.ts';
import {trainSceneFixture} from './train-scene.ts';
import {TerrainScene} from '../../src/lib/terrain-renderer.ts';
import {TerrainViewport} from '../../src/lib/terrain-viewport.ts';
import {RailwayRenderPlan} from '../../src/lib/railway-renderer.ts';
import {railwayProfile} from '../../src/lib/mixed-state.ts';
import {WorldSpace} from '../../src/lib/map-geometry.ts';

export async function poseGallery(catalog:RenderCatalog,variant:Variant) {
 const cache=new RecipeImageCache(),key=variantKey(variant),recipes=catalog.renderRecipes.filter(r=>['train-car','reverse-train-car'].includes(r.handler));
 const images=new Map<string,HTMLImageElement>();
 const image=async(url:string)=>{let i=images.get(url);if(!i){i=new Image();i.src=url;await i.decode();images.set(url,i);}return i;};
 const tiles=[];
 for(const recipe of recipes){
  const family=recipe.fields.asymmetric===true?'asymmetric':recipe.id==='{6E5E12DD-69CB-4DF2-B4F0-2BCDA703CBB0}'?'color':'symmetric';
  const rows=fixtures.groups.rendering.cases.filter(c=>c.input.family===family&&c.input.wrapper===(recipe.handler==='reverse-train-car'));
  for(const row of rows){
   const i=row.input,e=row.expected;
   const layer=trainCarLayer(recipe,recipe.id,[0,0,0],i.heading,i.isReversed,'outgoing' in i?i.outgoing:i.heading,
    i.kind==='slope'?{climb:i.climb,level:i.level}:undefined);
   tiles.push({recipe,row,layer});
  }
 }
 const ready=await cache.ready(catalog,tiles.map(t=>t.layer),variant),gallery=document.createElement('canvas');gallery.width=16*48;gallery.height=Math.ceil(tiles.length/16)*48;
 const contact=gallery.getContext('2d')!,actual=new OffscreenCanvas(48,48),expected=new OffscreenCanvas(48,48),a=actual.getContext('2d')!,e=expected.getContext('2d')!;
 let pixels=0,picks=0;
 for(const [n,t] of tiles.entries()){
  a.clearRect(0,0,48,48);e.clearRect(0,0,48,48);
  ready.paintNormal(a as unknown as CanvasRenderingContext2D,{x:8,y:20,zoom:1},t.layer);
  const frame=t.layer.frame,sheet=await image(catalog.surfaceSets[frame.surfaceSet!][key].image.url),[sx,sy,w,h]=t.row.expected.rectangle,[dx,dy]=t.row.expected.offset;
  e.drawImage(sheet,sx,sy,w,h,8+dx,20+dy,w,h);
  const got=a.getImageData(0,0,48,48).data,want=e.getImageData(0,0,48,48).data;
  for(let p=0;p<got.length;p++)if(got[p]!==want[p])throw new Error(`Pixel mismatch ${t.recipe.id}/${t.row.id}/${key}/${p}`);
  pixels+=48*48;
  for(let y=0;y<48;y++)for(let x=0;x<48;x++){
   const hit=ready.pick(x+.5,y+.5,{x:8,y:20,zoom:1},[t.layer]);
   if(!!hit!==(want[(y*48+x)*4+3]>0)||hit&&hit.entityId!==t.recipe.id)throw new Error(`Alpha pick mismatch ${t.recipe.id}/${t.row.id}/${x},${y}`);
   picks++;
  }
  const tx=n%16*48,ty=Math.floor(n/16)*48;
  contact.fillStyle=n%2?'#c2cfc0':'#d8e5d9';contact.fillRect(tx,ty,48,48);contact.drawImage(actual,tx,ty);
 }
 return {png:gallery.toDataURL(),tiles:tiles.length,recipes:recipes.length,pixels,picks};
}

export async function infrastructureGallery(catalog:RenderCatalog,variant:Variant) {
 const cache=new RecipeImageCache(),stock=catalog.renderRecipes.find(r=>r.handler==='train-composition')!,tiles=[];
 const bindings=railwayProfile.bindings.filter(b=>['bridge','girder','tunnel','garage','signal'].includes(b.handler));
 const poles=railwayProfile.bindings.filter(b=>b.handler==='pole');
 for(const b of bindings){
  const recipe=catalog.renderRecipes.find(r=>r.id===b.reference.recipeId)!;
  const choices=[...new Set(recipe.frames.map(f=>b.handler==='signal'?f.selector.direction:f.selector.index))];
  for(const choice of choices)for(const pole of [undefined,...poles]){
   const s=trainSceneFixture(source as Snapshot,catalog,stock),v=s.mixed!.voxels[0],p=v.position,r=v.traffic!.rail!,car=s.mixed!.fleet!.trains[0].cars[0];
   r.definition=b.reference;r.variant=b.handler==='signal'?0:choice as number;r.direction=b.handler==='signal'?choice as number:0;
   r.pattern=(b.handler==='signal'?choice as number%4===0:choice as number%2===1)?10:4;
   car.state.heading=r.pattern===10?0:2;
   if(pole)v.traffic!.accessory={definition:pole.reference,direction:r.pattern===10?1:0};
   const plan=new RailwayRenderPlan(s,catalog,variant),st=new WorldSpace(16,32,6).storage(p);
   const ops=plan.operations(st.v*16+st.h,5,0,true,2).filter(o=>'layer' in o&&o.target.position.z===2);
   const layers=ops.map(o=>{if(!('layer' in o))throw new Error('Missing art');return {...o.layer,position:[0,0,0] as [number,number,number]};});
   const order=ops.map(o=>o.target.kind==='train'?30:o.target.component==='accessory'?((('layer' in o?o.layer.frame.stage:0)??20)>=30?50:10):((('layer' in o?o.layer.frame.stage:0)??20)>=30?40:20));
   if(order.some((n,i)=>i>0&&order[i-1]>n)||!order.includes(30))throw new Error('Traffic order mismatch');
   tiles.push({layers,id:b.handler+'/'+choice+'/'+(pole?.reference.recipeId??'none')});
  }
 }
 const ready=await cache.ready(catalog,tiles.flatMap(t=>t.layers),variant),gallery=document.createElement('canvas');gallery.width=16*64;gallery.height=Math.ceil(tiles.length/16)*64;
 const paint=gallery.getContext('2d')!;
 for(const [i,t] of tiles.entries()){
  paint.fillStyle=i%2?'#c2cfc0':'#d8e5d9';paint.fillRect(i%16*64,Math.floor(i/16)*64,64,64);
  ready.draw(paint,{x:i%16*64+16,y:Math.floor(i/16)*64+30,zoom:1},{kind:'normal'},t.layers);
 }
 return {png:gallery.toDataURL(),tiles:tiles.length,poles:poles.length};
}

export async function localMovementPixels(catalog:RenderCatalog) {
 const cache=new RecipeImageCache(),stock=catalog.renderRecipes.find(r=>r.handler==='train-composition')!,s=trainSceneFixture(source as Snapshot,catalog,stock,false,200,100);
 // A vacant rail at the next head position; all other rail stays in place.
 const vacant=structuredClone(s.mixed!.voxels[0]);vacant.ownerId='4';vacant.position={...vacant.position,y:vacant.position.y-1};vacant.traffic!.car=null;
 s.mixed!.voxels.push(vacant);s.entities!.push({...structuredClone(s.entities![0]),id:'4',anchor:vacant.position});
 const variant:Variant={season:'spring',night:false},before=await TerrainScene.prepare(s,catalog,cache,variant,5),viewport=new TerrainViewport(before);
 const p=new WorldSpace(200,100,6).project(s.mixed!.fleet!.trains[0].cars[0].state.location!),view={x:200-p.x,y:140-p.y,zoom:1};
 const canvas=document.createElement('canvas');canvas.width=400;canvas.height=300;const ctx=canvas.getContext('2d')!;
 const settle=(v:TerrainViewport)=>{for(let n=0;n<10000;n++){ctx.clearRect(0,0,400,300);const result=v.draw(ctx,view,400,300,1);if(!result.pending)return result;}throw new Error('Viewport never completed');};
 settle(viewport);const initial=canvas.toDataURL();
 const next=structuredClone(s),train=next.mixed!.fleet!.trains[0],positions=[vacant.position,...train.cars.map(c=>c.state.location!)];
 for(const [i,c] of train.cars.entries())c.state.location={...positions[i]};
 for(const v of next.mixed!.voxels){const car=train.cars.find(c=>c.state.location!.x===v.position.x&&c.state.location!.y===v.position.y);v.traffic!.car=car?{definition:car.definition,trainId:train.id,carId:car.id}:null;}
 next.revision='7';next.mixed!.geometryRevision='7';
 const moved=await TerrainScene.prepare(next,catalog,cache,variant,5,before);viewport.setScene(moved);const result=settle(viewport),patched=canvas.toDataURL();
 if(result.updateKind!=='local'||patched===initial)throw new Error('Movement did not patch visible pixels');
 settle(new TerrainViewport(await TerrainScene.prepare(next,catalog,cache,variant,5)));
 if(canvas.toDataURL()!==patched)throw new Error('Clipped movement differs from a full render');
 // Every visible target in the patch must agree with a fresh full render.
 const fresh=await TerrainScene.prepare(next,catalog,cache,variant,5),full=new TerrainViewport(fresh);settle(full);
 let picks=0;
 for(let y=80;y<200;y++)for(let x=100;x<300;x++){
  if(JSON.stringify(viewport.pick({x:x+.5,y:y+.5},view))!==JSON.stringify(full.pick({x:x+.5,y:y+.5},view)))throw new Error('Patched pick differs from full render');picks++;
 }
 return {png:patched,picks,columns:moved.changedColumns(before)};
}

export async function tunnelMovementPixels(catalog:RenderCatalog,variant:Variant) {
 const cache=new RecipeImageCache(),stock=catalog.renderRecipes.find(r=>r.handler==='train-composition')!,s=trainSceneFixture(source as Snapshot,catalog,stock);
 const train=s.mixed!.fleet!.trains[0];train.cars=[train.cars[0]];train.cars[0].state.heading=4;
 const tunnel=railwayProfile.bindings.find(b=>b.handler==='tunnel')!;
 for(const [i,v] of s.mixed!.voxels.entries()){
  v.traffic!.car=i===0?v.traffic!.car:null;
  Object.assign(v.traffic!.rail!,{definition:tunnel.reference,variant:1,terrainBackup:{ne:0,se:0,sw:0,nw:0}});
 }
 const p=new WorldSpace(16,32,6).project(train.cars[0].state.location!),view={x:100-p.x,y:80-p.y,zoom:1};
 const canvas=document.createElement('canvas');canvas.width=240;canvas.height=180;const ctx=canvas.getContext('2d')!;
 const before=await TerrainScene.prepare(s,catalog,cache,variant,5);before.draw(ctx,view,240,180);const png=canvas.toDataURL();
 const next=structuredClone(s);next.mixed!.fleet!.trains[0].cars[0].state.location={...next.mixed!.voxels[1].position};
 next.mixed!.voxels[1].traffic!.car=next.mixed!.voxels[0].traffic!.car;next.mixed!.voxels[0].traffic!.car=null;
 const after=await TerrainScene.prepare(next,catalog,cache,variant,5,before);ctx.clearRect(0,0,240,180);after.draw(ctx,view,240,180);
 if(canvas.toDataURL()!==png)throw new Error('Continuous S/W tunnel exposed a moving interior car');
 let picks=0;
 for(let y=0;y<180;y++)for(let x=0;x<240;x++){
  if(before.pick({x:x+.5,y:y+.5},view)?.kind==='train'||after.pick({x:x+.5,y:y+.5},view)?.kind==='train')throw new Error('Hidden tunnel car is pickable');picks++;
 }
 return {png,picks};
}

export async function smoothMovementPixels(catalog:RenderCatalog,variant:Variant) {
 const stock=catalog.renderRecipes.find(r=>r.handler==='train-composition')!,cache=new RecipeImageCache();
 const gallery=document.createElement('canvas');gallery.width=5*240;gallery.height=3*180;const contact=gallery.getContext('2d')!;
 let picks=0,frames=0,hidden=0;
 for(const [row,handler] of ['normal','bridge','tunnel'].entries()){
  const s=trainSceneFixture(source as Snapshot,catalog,stock),t=s.mixed!.fleet!.trains[0];t.cars=[t.cars[0]];t.cars[0].state.heading=4;
  for(const [i,v] of s.mixed!.voxels.entries()){
   v.traffic!.car=i===1?{definition:t.cars[0].definition,trainId:t.id,carId:t.cars[0].id}:null;
   if(handler!=='normal')Object.assign(v.traffic!.rail!,{definition:railwayProfile.bindings.find(b=>b.handler===handler)!.reference,variant:1,terrainBackup:handler==='tunnel'?{ne:0,se:0,sw:0,nw:0}:null});
  }
  const from={position:{...s.mixed!.voxels[0].position},heading:4,outgoing:4,reversed:false},to={...from,position:{...s.mixed!.voxels[1].position}};t.cars[0].state.location=to.position;
  const scene=await TerrainScene.prepare(s,catalog,cache,variant,5),space=new WorldSpace(16,32,6),p=space.project(from.position),view={x:100-p.x,y:80-p.y,zoom:1};
  const compositor=new TrainCompositor(),canvas=document.createElement('canvas');canvas.width=240;canvas.height=180;const paint=canvas.getContext('2d')!;
  const staticOps=scene.operations.filter(o=>o.target.kind!=='train'),original=scene.operations.find(o=>o.target.kind==='train')!;
  const recipe=catalog.renderRecipes.find(r=>r.id===t.cars[0].definition.recipeId)!;
  for(const [column,progress] of [0,.25,.5,.75,1].entries()){
   const car={trainId:t.id,carId:t.cars[0].id,from,to,progress};
   paint.fillStyle=scene.backgroundColor;paint.fillRect(0,0,240,180);scene.draw(paint,view,240,180);
   compositor.draw(paint,view,240,180,scene,scene.operations,catalog,[car],16,15);
   const dynamic={target:original.target,layer:movingCarLayer(recipe,car,15)};
   // Independent full operation replay at the containing source voxel.
   const position=progress<.5?from.position:to.position,storage=space.storage(position),index=storage.v*16+storage.h;
   const expected=handler==='tunnel'?[...scene.operations]:[...staticOps];let insert=expected.findIndex(o=>scene.columnIndex(o)>index||scene.columnIndex(o)===index&&(o.target.position.z>position.z||o.target.kind==='railway'&&'layer' in o&&(o.layer.frame.stage??20)>=30));
   if(insert<0)insert=expected.length;if(handler!=='tunnel')expected.splice(insert,0,dynamic);
   const reference=document.createElement('canvas');reference.width=240;reference.height=180;const ref=reference.getContext('2d')!;ref.fillStyle=scene.backgroundColor;ref.fillRect(0,0,240,180);compositor.paintOperations(ref,view,240,180,expected);
   const a=paint.getImageData(0,0,240,180).data,b=ref.getImageData(0,0,240,180).data;
   for(let i=0;i<a.length;i++)if(a[i]!==b[i])throw new Error(`Smooth ${handler}/${progress}: damaged pixel differs at ${i}`);
   let hits=0;
   for(let y=45;y<125;y++)for(let x=60;x<180;x++){
    const p={x:x+.5,y:y+.5},actual=compositor.pick(p,view);
    const car=expected.indexOf(dynamic),carHit=handler!=='tunnel'&&scene.pickOperations(p,view,[dynamic]);
    const visible=carHit?expected.filter((op,i)=>!(i>car&&op.target.position.z===dynamic.target.position.z&&
     (op.target.kind==='terrain'&&op.target.surface==='ground'||op.target.kind==='railway'&&op.target.component==='rail'&&'layer' in op&&(op.layer.frame.stage??20)<30))):expected;
    const wanted=scene.pickOperations(p,view,visible);
    if(JSON.stringify(actual)!==JSON.stringify(wanted))throw new Error(`Smooth ${handler}/${progress}: alpha pick differs at ${x},${y}`);
    if(actual?.kind==='train')hits++;picks++;
   }
   if(handler==='tunnel'){if(hits)throw new Error('Intermediate tunnel car became pickable at '+progress+' hits='+hits);hidden++;}
   contact.drawImage(canvas,column*240,row*180);frames++;
  }
 }
 return {png:gallery.toDataURL(),frames,picks,hidden};
}

/** Independent tunnel oracle: whole cars over the static scene, alpha-clipped
 * by every solid interior and portal foreground, without voxel ownership. */
export async function trainTunnelPixels(catalog:RenderCatalog,variant:Variant,measure=false) {
 const stock=catalog.renderRecipes.find(r=>r.handler==='train-composition')!,cache=new RecipeImageCache();
 const tunnel=railwayProfile.bindings.find(b=>b.handler==='tunnel')!;
 const space=new WorldSpace(32,48,6),anchor=space.location({h:15,v:24,z:2});
 const phases=[0,.25,.49,.5,.51,.75,1],gallery=document.createElement('canvas');
 gallery.width=phases.length*260;gallery.height=8*200;const contact=gallery.getContext('2d')!;
 let frames=0,picks=0,visible=0,hidden=0,row=0,paintCalls=0,totalPaintCalls=0;const frameTimes:number[]=[];
 for(const heading of [0,2,4,6])for(const reversed of [false,true]) {
  const [dx,dy]=[[0,-1],[1,0],[0,1],[-1,0]][heading/2];
  const position=(k:number)=>({x:anchor.x+k*dx,y:anchor.y+k*dy,z:2});
  const s=trainSceneFixture(source as Snapshot,catalog,stock,reversed,32,48),train=s.mixed!.fleet!.trains[0];
  const template=structuredClone(s.mixed!.voxels[0]),entity=structuredClone(s.entities![0]);
  const pattern=heading%4===0?10:4,portal=dy>0||dx<0?3:0;
  s.entities=[];s.mixed!.voxels=[];s.mixed!.nextEntityId='54';
  // Authority is beyond the exit while presentation is still entering.
  for(const [i,c] of train.cars.entries())c.state={kind:'inside',location:position(7-i),heading,countdown:null};
  for(let k=-5;k<=8;k++) {
   const v=structuredClone(template),id=String(k+45);v.ownerId=id;v.position=position(k);
   const car=train.cars.find(c=>JSON.stringify(c.state.location)===JSON.stringify(v.position));
   v.traffic!.car=car?{definition:car.definition,trainId:train.id,carId:car.id}:null;
   v.traffic!.rail!.pattern=pattern;
   v.traffic!.rail!.definition=k>=0&&k<=3?tunnel.reference:{...v.traffic!.rail!.definition,contributionId:`system:pattern/${pattern}`,recipeId:`system:pattern/${pattern}`};
   v.traffic!.rail!.variant=k>=0&&k<=3&&heading%4===0?1:0;
   v.traffic!.rail!.terrainBackup=k>=0&&k<=3?{ne:0,se:0,sw:0,nw:0}:null;
   s.mixed!.voxels.push(v);s.entities.push({...structuredClone(entity),id,anchor:v.position});
  }
  const scene=await TerrainScene.prepare(s,catalog,cache,variant,5),compositor=new TrainCompositor();
  if(measure){const paint=scene.paintOperations.bind(scene);scene.paintOperations=(...args)=>{paintCalls++;return paint(...args);};}
  const p=space.project(position(2)),view={x:130-p.x,y:100-p.y,zoom:1};
  const staticOps=scene.operations.filter(o=>o.target.kind!=='train');
  const originals=new Map(scene.operations.filter(o=>o.target.kind==='train').map(o=>[o.target.kind==='train'?o.target.carId:'',o]));
  const blockers=staticOps.filter(o=>o.target.kind==='railway'&&o.target.definition.recipeId===tunnel.reference.recipeId&&'layer' in o&&
   ((o.layer.frame.stage??20)>=30||JSON.stringify(o.target.position)!==JSON.stringify(position(portal))));
  const canvas=document.createElement('canvas'),reference=document.createElement('canvas'),sprites=document.createElement('canvas');
  canvas.width=reference.width=sprites.width=260;canvas.height=reference.height=sprites.height=200;
  const paint=canvas.getContext('2d')!,ref=reference.getContext('2d')!,ink=sprites.getContext('2d')!;
  for(let step=-2;step<=6;step++)for(const [column,progress] of phases.entries()) {
   const cars=train.cars.map((c,i)=>({trainId:train.id,carId:c.id,
    from:{position:position(step-i),heading,outgoing:heading,reversed},
    to:{position:position(step+1-i),heading,outgoing:heading,reversed},progress}));
   paint.fillStyle=scene.backgroundColor;paint.fillRect(0,0,260,200);scene.draw(paint,view,260,200);
   const start=performance.now(),calls=paintCalls;
   compositor.draw(paint,view,260,200,scene,scene.operations,catalog,cars,32,space.centerX);
   if(measure){frameTimes.push(performance.now()-start);totalPaintCalls+=paintCalls-calls;frames++;continue;}
   const dynamic=cars.map(car=>({target:originals.get(car.carId)!.target,
    layer:movingCarLayer(catalog.renderRecipes.find(r=>r.id===train.cars.find(c=>c.id===car.carId)!.definition.recipeId)!,car,space.centerX)}));
   dynamic.sort((a,b)=>compareTrainLayers(a.layer,b.layer));
   ref.fillStyle=scene.backgroundColor;ref.fillRect(0,0,260,200);scene.paintOperations(ref,view,260,200,staticOps);
   ink.clearRect(0,0,260,200);scene.paintOperations(ink,view,260,200,dynamic);
   ink.globalCompositeOperation='destination-out';scene.paintOperations(ink,view,260,200,blockers);ink.globalCompositeOperation='source-over';
   ref.drawImage(sprites,0,0);
   const a=paint.getImageData(0,0,260,200).data,b=ref.getImageData(0,0,260,200).data;
   for(let i=0;i<a.length;i++)if(a[i]!==b[i]){
    const point={x:(i/4|0)%260+.5,y:Math.floor((i/4|0)/260)+.5};
    const hits=[...staticOps,...dynamic].filter(o=>scene.pickOperations(point,view,[o])).map(o=>({target:o.target,...('layer' in o?{frame:o.layer.frame.key,tunnel:'tunnel' in o?o.tunnel:undefined,position:o.layer.position}:{})}));
    throw new Error(`Tunnel pixels: ${heading}/${reversed}/${step}/${progress} at ${i/4|0}, actual=${JSON.stringify([...a.slice(i-i%4,i-i%4+4)])}, expected=${JSON.stringify([...b.slice(i-i%4,i-i%4+4)])}, hits=${JSON.stringify(hits)}`);
   }
   let hits=0;
   for(let y=30;y<170;y+=4)for(let x=20;x<240;x+=4) {
    const point={x:x+.5,y:y+.5},blocked=scene.pickOperations(point,view,blockers);
    const expected=(!blocked&&scene.pickOperations(point,view,dynamic))||scene.pickOperations(point,view,staticOps);
    const actual=compositor.pick(point,view);
    if(JSON.stringify(actual)!==JSON.stringify(expected))throw new Error(`Tunnel pick: ${heading}/${reversed}/${step}/${progress} at ${x},${y}`);
    if(actual?.kind==='train')hits++;picks++;
   }
   if(hits)visible++;else hidden++;
   if(step===3)contact.drawImage(canvas,column*260,row*200);frames++;
  }
  row++;
 }
 return {png:gallery.toDataURL(),frames,picks,visible,hidden,frameTimes,paintCalls:totalPaintCalls};
}

/** Flat-track oracle: render every rail/ground pixel first, then whole cars.
 * Audit both ends of a three-car consist in all eight directions. */
export async function trainTrackOverlayPixels(catalog:RenderCatalog,variant:Variant) {
 const offsets=[[0,-1],[1,-1],[1,0],[1,1],[0,1],[-1,1],[-1,0],[-1,-1]];
 const stock=catalog.renderRecipes.find(r=>r.handler==='train-composition')!,cache=new RecipeImageCache(),space=new WorldSpace(16,32,6),anchor=space.location({h:7,v:16,z:2});
 const gallery=document.createElement('canvas');gallery.width=5*260;gallery.height=16*200;const contact=gallery.getContext('2d')!;
 let frames=0,carPixels=0;
 for(const reversed of [false,true])for(let heading=0;heading<8;heading++) {
  const s=trainSceneFixture(source as Snapshot,catalog,stock,reversed),train=s.mixed!.fleet!.trains[0],[dx,dy]=offsets[heading];
  const position=(k:number)=>({x:anchor.x+dx*k,y:anchor.y+dy*k,z:2});
  const pattern=Array.from({length:12},(_,i)=>i).find(i=>normalPorts(`normal${i}`)!.includes(heading)&&normalPorts(`normal${i}`)!.includes((heading+4)%8))!;
  const template=structuredClone(s.mixed!.voxels[0]),entity=structuredClone(s.entities![0]);
  for(const [i,c] of train.cars.entries())c.state={kind:'inside',location:position(1-i),heading,countdown:null};
  s.entities=[];s.mixed!.voxels=[];
  for(let k=-3;k<=5;k++) {
   const v=structuredClone(template),id=String(k+4);v.position=position(k);v.ownerId=id;
   const car=train.cars.find(c=>c.state.location!.x===v.position.x&&c.state.location!.y===v.position.y);
   v.traffic!.car=car?{definition:car.definition,trainId:train.id,carId:car.id}:null;
   v.traffic!.rail!.pattern=pattern;v.traffic!.rail!.definition={...v.traffic!.rail!.definition,contributionId:`system:pattern/${pattern}`,recipeId:`system:pattern/${pattern}`};
   s.mixed!.voxels.push(v);s.entities.push({...structuredClone(entity),id,anchor:v.position});
  }
  const scene=await TerrainScene.prepare(s,catalog,cache,variant,5),p=space.project(anchor),view={x:130-p.x,y:100-p.y,zoom:1};
  const original=new Map(scene.operations.filter(o=>o.target.kind==='train').map(o=>[o.target.kind==='train'?o.target.carId:'',o]));
  const compositor=new TrainCompositor(),canvas=document.createElement('canvas'),reference=document.createElement('canvas');canvas.width=reference.width=260;canvas.height=reference.height=200;
  const paint=canvas.getContext('2d')!,ref=reference.getContext('2d')!,staticOps=scene.operations.filter(o=>o.target.kind!=='train');
  for(const [column,progress] of [0,.25,.5,.75,1].entries()) {
   const cars=train.cars.map((c,i)=>({trainId:train.id,carId:c.id,from:{position:position(-i),heading,outgoing:heading,reversed},to:{position:position(1-i),heading,outgoing:heading,reversed},progress}));
   paint.fillStyle=scene.backgroundColor;paint.fillRect(0,0,260,200);scene.draw(paint,view,260,200);
   compositor.draw(paint,view,260,200,scene,scene.operations,catalog,cars,16,space.centerX);
   const dynamic=cars.map(car=>({target:original.get(car.carId)!.target,layer:movingCarLayer(catalog.renderRecipes.find(r=>r.id===train.cars.find(c=>c.id===car.carId)!.definition.recipeId)!,car,space.centerX)}));
   const index=(op:typeof dynamic[number])=>{const p=space.storage({x:Math.round(op.layer.position[0]+space.centerX),y:Math.round(op.layer.position[1]),z:2});return p.v*16+p.h;};dynamic.sort((a,b)=>index(a)-index(b));
   ref.fillStyle=scene.backgroundColor;ref.fillRect(0,0,260,200);scene.paintOperations(ref,view,260,200,[...staticOps,...dynamic]);
   const a=paint.getImageData(0,0,260,200).data,b=ref.getImageData(0,0,260,200).data;
   for(let i=0;i<a.length;i++)if(a[i]!==b[i])throw new Error(`Track covers car: heading=${heading}, reversed=${reversed}, progress=${progress}, pixel=${i/4|0}`);
   for(let y=40;y<160;y++)for(let x=40;x<220;x++) {
    const p={x:x+.5,y:y+.5},expected=scene.pickOperations(p,view,dynamic);if(!expected)continue;
    const actual=compositor.pick(p,view);if(JSON.stringify(actual)!==JSON.stringify(expected))throw new Error(`Track picks over car: ${heading}/${reversed}/${progress} at ${x},${y}`);carPixels++;
   }
   contact.drawImage(canvas,column*260,((reversed?8:0)+heading)*200);frames++;
  }
 }
 return {png:gallery.toDataURL(),frames,carPixels};
}

/** Continuous-depth oracle for adjacent cars entering/exiting a tight bend. */
export async function trainTurnBlendPixels(catalog:RenderCatalog,variant:Variant) {
 const stock=catalog.renderRecipes.find(r=>r.handler==='train-composition')!,cache=new RecipeImageCache();
 const gallery=document.createElement('canvas');gallery.width=8*240;gallery.height=8*180;const contact=gallery.getContext('2d')!;
 const phases=[0,.01,.25,.43,.49,.5,.51,.57,.75,.99,1];
 let frames=0,overlapPicks=0;
 for(let rotation=0;rotation<8;rotation++)for(const turn of [-1,1])for(const reversed of [false,true]) {
  const route=trainTurnScene(source as Snapshot,catalog,stock,rotation,turn,reversed),p=route.space.project(route.pose(5).position),view={x:120-p.x,y:90-p.y,zoom:1};
  let previous:TerrainScene|undefined;
  const compositor=new TrainCompositor(),canvas=document.createElement('canvas'),reference=document.createElement('canvas');canvas.width=reference.width=240;canvas.height=reference.height=180;
  const paint=canvas.getContext('2d')!,ref=reference.getContext('2d')!;
  for(let step=3;step<11;step++) {
   const s=route.snapshot(step),train=s.mixed!.fleet!.trains[0],scene=await TerrainScene.prepare(s,catalog,cache,variant,5,previous);previous=scene;
   const original=new Map(scene.operations.filter(o=>o.target.kind==='train').map(o=>[o.target.kind==='train'?o.target.carId:'',o]));
   const staticOps=scene.operations.filter(o=>o.target.kind!=='train');
   for(const progress of phases) {
    const cars=train.cars.map((c,i)=>({trainId:train.id,carId:c.id,from:route.pose(step-i),to:route.pose(step-i+1),progress}));
    paint.fillStyle=scene.backgroundColor;paint.fillRect(0,0,240,180);scene.draw(paint,view,240,180);
    compositor.draw(paint,view,240,180,scene,scene.operations,catalog,cars,32,route.space.centerX);
    const dynamic=cars.map(car=>({target:original.get(car.carId)!.target,layer:movingCarLayer(catalog.renderRecipes.find(r=>r.id===train.cars.find(c=>c.id===car.carId)!.definition.recipeId)!,car,route.space.centerX)}));
    // Independent isometric depth: continuous V, continuous H, then height/ID.
    // Integer voxel ownership never determines which overlapping car is visible.
    dynamic.sort((a,b)=>{const [x,y,z]=a.layer.position,[xx,yy,zz]=b.layer.position;for(const d of [(y-x)-(yy-xx),(x+y)-(xx+yy),z-zz])if(Math.abs(d)>1e-8)return d;return a.layer.entityId.localeCompare(b.layer.entityId);});
    ref.fillStyle=scene.backgroundColor;ref.fillRect(0,0,240,180);scene.paintOperations(ref,view,240,180,[...staticOps,...dynamic]);
    const a=paint.getImageData(0,0,240,180).data,b=ref.getImageData(0,0,240,180).data;
    for(let i=0;i<a.length;i++)if(a[i]!==b[i])throw new Error(`Bend car blending: rotation=${rotation}, turn=${turn}, reversed=${reversed}, step=${step}, progress=${progress}, pixel=${i/4|0}`);
    for(let i=0;i<dynamic.length;i++)for(let j=i+1;j<dynamic.length;j++) {
     const a=scene.boundsFor(dynamic[i]),b=scene.boundsFor(dynamic[j]);
     const left=Math.max(0,Math.ceil(view.x+Math.max(a.left,b.left))),right=Math.min(240,Math.floor(view.x+Math.min(a.right,b.right)));
     const top=Math.max(0,Math.ceil(view.y+Math.max(a.top,b.top))),bottom=Math.min(180,Math.floor(view.y+Math.min(a.bottom,b.bottom)));
     for(let y=top;y<bottom;y+=2)for(let x=left;x<right;x+=2) {
      const p={x:x+.5,y:y+.5};if(!scene.pickOperations(p,view,[dynamic[i]])||!scene.pickOperations(p,view,[dynamic[j]]))continue;
      const actual=compositor.pick(p,view),expected=scene.pickOperations(p,view,dynamic);
      if(JSON.stringify(actual)!==JSON.stringify(expected))throw new Error(`Bend overlap pick: ${rotation}/${turn}/${reversed}/${step}/${progress} at ${x},${y}`);overlapPicks++;
     }
    }
    if(rotation===0&&turn===-1&&!reversed&&[0,.25,.49,.5,.51,.75,.99,1].includes(progress))contact.drawImage(canvas,[0,.25,.49,.5,.51,.75,.99,1].indexOf(progress)*240,(step-3)*180);
    frames++;
   }
  }
 }
 return {png:gallery.toDataURL(),frames,overlapPicks};
}

/** Surface-ramp oracle: terrain and rail beds under whole, depth-sorted cars. */
export async function trainSlopePixels(catalog:RenderCatalog,variant:Variant) {
 const stock=catalog.renderRecipes.find(r=>r.handler==='train-composition')!,cache=new RecipeImageCache();
 const gallery=document.createElement('canvas');gallery.width=7*260;gallery.height=16*200;const contact=gallery.getContext('2d')!;
 const phases=[0,.25,.49,.5,.51,.75,1];let frames=0,carPixels=0,row=0;
 for(const climb of [0,2,4,6] as const)for(const downhill of [false,true])for(const reversed of [false,true]) {
  const route=trainSlopeScene(source as Snapshot,catalog,stock,climb,downhill,reversed),p=route.space.project(route.pose(3).position),view={x:130-p.x,y:100-p.y,zoom:1};
  const compositor=new TrainCompositor(),canvas=document.createElement('canvas'),reference=document.createElement('canvas');canvas.width=reference.width=260;canvas.height=reference.height=200;
  const paint=canvas.getContext('2d')!,ref=reference.getContext('2d')!;let previous:TerrainScene|undefined;
  for(let step=0;step<7;step++) {
   // Authority is several voxels ahead of presentation, as with coalesced moves.
   const s=route.snapshot(Math.min(7,step+3)),train=s.mixed!.fleet!.trains[0],scene=await TerrainScene.prepare(s,catalog,cache,variant,5,previous);previous=scene;
   const original=new Map(scene.operations.filter(o=>o.target.kind==='train').map(o=>[o.target.kind==='train'?o.target.carId:'',o]));
   const beds=scene.operations.filter(o=>o.target.kind!=='train');
   for(const [column,progress] of phases.entries()) {
    const cars=train.cars.map((c,i)=>({trainId:train.id,carId:c.id,from:route.pose(step-i),to:route.pose(step+1-i),progress}));
    paint.fillStyle=scene.backgroundColor;paint.fillRect(0,0,260,200);scene.draw(paint,view,260,200);
    compositor.draw(paint,view,260,200,scene,scene.operations,catalog,cars,32,route.space.centerX);
    const dynamic=cars.map(car=>({target:original.get(car.carId)!.target,layer:movingCarLayer(catalog.renderRecipes.find(r=>r.id===train.cars.find(c=>c.id===car.carId)!.definition.recipeId)!,car,route.space.centerX)}));
    dynamic.sort((a,b)=>compareTrainLayers(a.layer,b.layer));
    ref.fillStyle=scene.backgroundColor;ref.fillRect(0,0,260,200);scene.paintOperations(ref,view,260,200,[...beds,...dynamic]);
    const a=paint.getImageData(0,0,260,200).data,b=ref.getImageData(0,0,260,200).data;
    for(let i=0;i<a.length;i++)if(a[i]!==b[i]){
     const point={x:(i/4|0)%260+.5,y:Math.floor((i/4|0)/260)+.5};
     const hits=[...beds,...dynamic].filter(o=>scene.pickOperations(point,view,[o])).map(o=>({target:o.target,...('layer' in o?{recipe:o.layer.recipeId,position:o.layer.position}: {})}));
     throw new Error(`Slope pixels: ${climb}/${downhill}/${reversed}/${step}/${progress} at ${i/4|0}, actual=${JSON.stringify(compositor.pick(point,view))}, hits=${JSON.stringify(hits)}`);
    }
    for(let y=30;y<170;y+=2)for(let x=30;x<230;x+=2) {
     const point={x:x+.5,y:y+.5},expected=scene.pickOperations(point,view,dynamic);if(!expected)continue;
     if(JSON.stringify(compositor.pick(point,view))!==JSON.stringify(expected))throw new Error(`Slope pick: ${climb}/${downhill}/${reversed}/${step}/${progress} at ${x},${y}`);carPixels++;
    }
    if(step===3)contact.drawImage(canvas,column*260,row*200);frames++;
   }
  }
  row++;
 }
 return {png:gallery.toDataURL(),frames,carPixels};
}

// P5-25: browser-only exports for independent palette/crop/mask verification.
export {TrainColorCatalog,defaultTrainColors,colorTestCarId} from '../../src/lib/train-colors.ts';
export {RecipeImageCache,instantiate} from '../../src/lib/content-recipes.ts';

function gridFixture(catalog:RenderCatalog,pattern?:number):Snapshot {
 const s=structuredClone(mixedSource) as Snapshot;
 s.map={width:3,height:5,levels:6,coordinateSystem:'hv'};s.entities=[];
 s.terrain={...s.terrain!,geometryRevision:'0',nextEntityId:'1',waterLevel:0,occupants:[],cells:Array.from({length:15},()=>({ground:1,surface:'cut'}))};
 const request=JSON.parse(s.terrain.provenance.requestJson);request.Dimensions={H:3,V:5,Z:6};s.terrain.provenance.requestJson=JSON.stringify(request);
 s.mixed={geometryRevision:'0',nextEntityId:'1',voxels:[],slopes:[],accounts:{initialCash:s.cash,bankruptcyIncrement:'10000000000',transactions:[]}};
 const space=new WorldSpace(3,5,6),p=space.location({h:0,v:0,z:1}),id='1';
 const definition={packId:'exav-stable-bundled',contributionId:'{{98619BD4-46C3-4B90-1703d-kokakyo2}-E',recipeId:'{{98619BD4-46C3-4B90-1703d-kokakyo2}-E'};
 s.entities.push({id,revision:'0',definition,anchor:p,footprint:[{offset:{x:0,y:0,z:0},transparent:true}],owned:true,silentlyReclaimable:false,value:'0'});
 s.mixed.nextEntityId='2';s.mixed.voxels.push({position:p,ownerId:id,kind:'stationary',traffic:null,definition,sprite:0,drawGroundBelow:true,drawGroundAbove:true,supportedOwnerId:null});
 if(pattern!==undefined) {
  s.entities[0].definition={packId:catalog.packId,contributionId:'system:traffic',recipeId:'geometry:traffic-components-1'};
  const v=s.mixed.voxels[0];v.kind='traffic';v.definition=null;
  const definition={packId:catalog.packId,contributionId:`system:pattern/${pattern}`,recipeId:`system:pattern/${pattern}`};
  v.traffic={rail:{definition,pattern,variant:0,direction:0,controllerId:null,terrainBackup:null},road:null,accessory:null,car:null,platform:null};
 }
 return s;
}

export async function structureGridPixels(catalog:RenderCatalog,pattern?:number) {
 const s=gridFixture(catalog,pattern);
 let checked=0;
 for(const night of [false,true]) {
  const scene=await TerrainScene.prepare(s,catalog,new RecipeImageCache(),{season:'spring',night},5);
  const structure=scene.operations.filter(o=>o.target.kind==='railway'),grids=scene.operations.filter(o=>'points' in o&&o.target.kind==='terrain'&&o.target.surface==='ground'&&o.color==='transparent');
  for(const cached of [false,true])for(const dpr of [1,2])for(const zoom of [.75,1,2,4]) {
   const width=320,height=240,view={x:80.25,y:100.5,zoom};
   const a=new OffscreenCanvas(width*dpr,height*dpr),b=new OffscreenCanvas(width*dpr,height*dpr),ctx=a.getContext('2d')!,expected=b.getContext('2d')!;
   for(const c of [ctx,expected]){c.setTransform(dpr,0,0,dpr,0,0);scene.paintOperations(c as unknown as CanvasRenderingContext2D,view,width,height,structure);}
   const Canvas=globalThis.OffscreenCanvas;
   try {
    if(!cached)Reflect.set(globalThis,'OffscreenCanvas',undefined);
    scene.paintOperations(ctx as unknown as CanvasRenderingContext2D,view,width,height,grids);
   }finally{Reflect.set(globalThis,'OffscreenCanvas',Canvas);}
   const actual=ctx.getImageData(0,0,a.width,a.height).data,reference=expected.getImageData(0,0,b.width,b.height).data;
   for(let i=0;i<actual.length;i+=4)if(reference[i+3]===255) {
    checked++;if(actual.slice(i,i+4).some((v,j)=>v!==reference[i+j]))throw new Error(`Grid painted structure at zoom ${zoom}, DPR ${dpr}, pixel ${i/4}`);
   }
  }
 }
 return {checked};
}


export async function diagonalTrackPixels(catalog:RenderCatalog) {
 let checked=0,hidden=0;
 for(const pattern of [1,7]) {
  const s=gridFixture(catalog,pattern),space=new WorldSpace(3,5,6),template=s.mixed!.voxels[0],owner=s.entities![0];
  s.mixed!.voxels=[];s.entities=[];
  for(let i=0;i<3;i++) {
   const position=space.location({h:pattern===1?1:i,v:pattern===1?i*2:2,z:1}),id=String(i+1);
   s.mixed!.voxels.push({...structuredClone(template),position,ownerId:id});
   s.entities.push({...structuredClone(owner),id,anchor:position});
  }
  s.mixed!.nextEntityId='4';
  for(const night of [false,true]) {
   const scene=await TerrainScene.prepare(s,catalog,new RecipeImageCache(),{season:'spring',night},5);
   const rails=scene.operations.filter(o=>o.target.kind==='railway');
   for(const mode of ['direct','fallback','viewport'])for(const dpr of [1,2])for(const zoom of [.75,1,4]) {
    const width=480,height=300,view={x:80,y:100,zoom};
    const a=new OffscreenCanvas(width*dpr,height*dpr),b=new OffscreenCanvas(width*dpr,height*dpr),ctx=a.getContext('2d')!,ref=b.getContext('2d')!;
    ctx.setTransform(dpr,0,0,dpr,0,0);ref.setTransform(dpr,0,0,dpr,0,0);
    scene.paintOperations(ref as unknown as CanvasRenderingContext2D,view,width,height,rails);
    const Canvas=globalThis.OffscreenCanvas;
    try {
     if(mode==='fallback')Reflect.set(globalThis,'OffscreenCanvas',undefined);
     if(mode==='viewport') {
      const viewport=new TerrainViewport(scene);let settled=false;
      for(let n=0;n<1000;n++)if(!viewport.draw(ctx as unknown as CanvasRenderingContext2D,view,width,height,dpr).pending){settled=true;break;}
      if(!settled)throw new Error('Diagonal track viewport did not settle');
     }else scene.paintOperations(ctx as unknown as CanvasRenderingContext2D,view,width,height,scene.operations);
    }finally{Reflect.set(globalThis,'OffscreenCanvas',Canvas);}
    const actual=ctx.getImageData(0,0,a.width,a.height).data,expected=ref.getImageData(0,0,b.width,b.height).data;
    for(let i=0;i<actual.length;i+=4)if(expected[i+3]===255) {
     checked++;if(actual.slice(i,i+4).some((v,j)=>v!==expected[i+j]))throw new Error(`Ground broke diagonal ${pattern}, ${mode}, zoom ${zoom}, DPR ${dpr}, pixel ${i/4}`);
    }
   }
   // A raised neighboring tile still covers rail: protection is same-level only.
   const raised=structuredClone(s);raised.terrain!.cells[3].ground=2;
   const high=await TerrainScene.prepare(raised,catalog,new RecipeImageCache(),{season:'spring',night},5);
   const a=new OffscreenCanvas(160,120),b=new OffscreenCanvas(160,120),ctx=a.getContext('2d')!,ref=b.getContext('2d')!,view={x:16,y:48,zoom:1};
   high.paintOperations(ctx as unknown as CanvasRenderingContext2D,view,160,120,high.operations);
   scene.paintOperations(ref as unknown as CanvasRenderingContext2D,view,160,120,rails);
   const actual=ctx.getImageData(0,0,160,120).data,expected=ref.getImageData(0,0,160,120).data;
   for(let i=0;i<actual.length;i+=4)if(expected[i+3]===255&&actual.slice(i,i+3).some((v,j)=>v!==expected[i+j]))hidden++;
  }
 }
 if(!hidden)throw new Error('Raised terrain must still obscure lower track');
 return {checked,hidden};
}
