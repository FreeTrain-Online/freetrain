import {trainTurnScene} from './train-turn-scene.ts';
import {normalPorts} from '../../src/lib/world-identity.ts';
import {TrainCompositor} from '../../src/lib/train-compositor.ts';
import {movingCarLayer} from '../../src/lib/train-motion.ts';
// Bundled only by the focused browser test and served through a test route.
// Uses the production pose/painter/picker with actual decoded prepared PNGs.
import {trainCarLayer} from '../../src/lib/train-renderer.ts';
import {RecipeImageCache,variantKey,type RenderCatalog,type Variant} from '../../src/lib/content-recipes.ts';
import fixtures from '../../../doc/parity/train-fixtures.json' with {type:'json'};
import source from '../../../prototype/protocol/fixtures/snapshot-fleet.json' with {type:'json'};
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
   compositor.draw(paint,view,240,180,scene,scene.operations,catalog,handler==='tunnel'?[]:[car],16,15);
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
