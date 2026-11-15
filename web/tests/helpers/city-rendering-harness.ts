import {addPassagePlatform} from './passage-scene.ts';
import {trainSceneFixture} from './train-scene.ts';
import {addStation,stationDefinitions} from './station-scene.ts';
import source from '../../../prototype/protocol/fixtures/snapshot-fleet.json' with {type:'json'};
// Test-only oracle: pinned XML/load3D and literal ConstructionSite sprite geometry.
import sourceReview from '../../../doc/parity/exav-content-review.json' with {type:'json'};
import {cityScene,addCity,fixedDefinitions} from './city-scene.ts';
import {TerrainScene} from '../../src/lib/terrain-renderer.ts';
import {TerrainViewport} from '../../src/lib/terrain-viewport.ts';
import {WorldSpace} from '../../src/lib/map-geometry.ts';
import {RecipeImageCache,variantKey,type RenderCatalog,type Variant} from '../../src/lib/content-recipes.ts';
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

export async function cityGallery(catalog:RenderCatalog,variant:Variant) {
 const cache=new RecipeImageCache(),key=variantKey(variant),gallery=document.createElement('canvas');gallery.width=6*400;gallery.height=11*300;
 const contact=gallery.getContext('2d')!,actual=new OffscreenCanvas(400,300),expected=new OffscreenCanvas(400,300),ctx=actual.getContext('2d')!,want=expected.getContext('2d')!;
 let pixels=0,picks=0;
 for(const [i,d] of fixedDefinitions.entries()){
  const s=cityScene(),b=addCity(s,d),space=new WorldSpace(40,60,32),scene=await TerrainScene.prepare(s,catalog,cache,variant,31),ops=scene.operations.filter(o=>o.target.kind==='building');
  const q=space.project(b.location),view={x:60-q.x,y:210-q.y,zoom:1};ctx.clearRect(0,0,400,300);want.clearRect(0,0,400,300);
  scene.paintOperations(ctx as unknown as CanvasRenderingContext2D,view,400,300,ops);
  const recipe=catalog.renderRecipes.find(r=>r.id===d.reference.recipeId)!,sheet=new Image();sheet.src=catalog.surfaceSets[recipe.frames.find(f=>f.surfaceSet)!.surfaceSet!][key].image.url;await sheet.decode();
  const frames=sourceFrames(d.reference.contributionId).map(f=>({...f,p:{x:b.location.x+f.cell[0],y:b.location.y+f.cell[1],z:2+f.cell[2]}}));
  frames.sort((a,b)=>{const p=space.storage(a.p),q=space.storage(b.p);return p.v-q.v||p.h-q.h||p.z-q.z;});
  for(const f of frames){const p=space.project(f.p),[sx,sy,w,h]=f.rect;want.drawImage(sheet,sx,sy,w,h,view.x+p.x-f.anchor[0],view.y+p.y-f.anchor[1],w,h);}
  const got=ctx.getImageData(0,0,400,300).data,target=want.getImageData(0,0,400,300).data;
  for(let n=0;n<got.length;n++)if(got[n]!==target[n])throw new Error(`City crop pixel ${d.reference.recipeId}/${key}/${n}`);
  for(let y=0;y<300;y++)for(let x=0;x<400;x++){
   const hit=scene.pickOperations({x:x+.5,y:y+.5},view,ops);
   if(!!hit!==(target[(y*400+x)*4+3]>0)||hit&&(hit.kind!=='building'||hit.id!==b.id))throw new Error('City alpha pick mismatch');picks++;
  }
  pixels+=400*300;contact.fillStyle='#d8e5d9';contact.fillRect(i%6*400,Math.floor(i/6)*300,400,300);contact.drawImage(actual,i%6*400,Math.floor(i/6)*300);
  contact.fillStyle='#17232b';contact.font='12px sans-serif';contact.fillText(`${i+1}: ${d.sizeX}×${d.sizeY}×${d.minimumHeight}`,i%6*400+8,Math.floor(i/6)*300+284);
 }
 return {png:gallery.toDataURL(),definitions:fixedDefinitions.length,pixels,picks};
}
// Expected rectangles/anchors come straight from the source static constructor,
// never the compiled construction frame or the production part-order helper.
function sourceParts(x:number,y:number,z:number,stage:number,ground:number,door:boolean){
 const conn=[y!==0,x!==1,y!==2,x!==0],parts:{id:string;rect:number[];anchor:number[]}[]=[];
 const add=(id:string,rect:number[],anchor=[0,16])=>parts.push({id:'system:construction/'+id,rect,anchor});
 const fence=(i:number)=>{if(!conn[i])add(`fence/${i}/${door?1:0}`,[(door&&i>=2?i+2:i)*32,0,32,32]);};
 if(z===0){fence(0);fence(1);}
 const bone=(level:number)=>{const codes=['0011','1011','1001','0000','0111','1111','1101','0101','0110','1110','1100','1010','0010','0100','1000','0001'],bits=conn.map(c=>c?'1':'0').join(''),idx=codes.indexOf(bits);add(`bones/${level}/${bits}`,[level*128+idx%4*32,Math.floor(idx/4)*32,32,32]);};
 const wall=(i:number)=>{if(!conn[i])add('wall/'+i,[[0,0,16,32],[16,0,16,32],[16,32,16,32],[0,32,16,32]][i],i===1||i===2?[-16,16]:[0,16]);};
 const floor=(g:number)=>add('ground/'+g,[[96,1,32,16],[64,17,32,16],[96,17,32,16],[64,0,32,17],[0,0,32,47],[32,0,32,47],[96,33,32,24]][g],[[0,0],[0,0],[0,0],[0,1],[0,31],[0,31],[0,8]][g]);
 if(stage===0){if(z===0)floor(ground);}else if(stage<4){if(z===0)floor(0);bone(stage-1);}else{wall(0);wall(1);bone(2);wall(2);wall(3);}
 if(z===0){fence(2);fence(3);}return parts;
}
export async function constructionGallery(catalog:RenderCatalog,variant:Variant){
 const cache=new RecipeImageCache(),gallery=document.createElement('canvas');gallery.width=7*300;gallery.height=10*220;
 const contact=gallery.getContext('2d')!,images=new Map<string,HTMLImageElement>();let pixels=0,picks=0;
 const d=fixedDefinitions.find(d=>d.sizeX===2&&d.sizeY===3)!;
 if(!d)throw new Error('Missing 2x3 source witness');
 for(let stage=0;stage<5;stage++)for(let ground=0;ground<7;ground++)for(const door of [false,true]){
  const s=cityScene(),b=addCity(s,d,10,22,stage,ground,door),space=new WorldSpace(40,60,32),scene=await TerrainScene.prepare(s,catalog,cache,variant,31),ops=scene.operations.filter(o=>o.target.kind==='building');
  const actual=new OffscreenCanvas(300,220),expected=new OffscreenCanvas(300,220),ctx=actual.getContext('2d')!,want=expected.getContext('2d')!,q=space.project(b.location),view={x:50-q.x,y:150-q.y,zoom:1};
  scene.paintOperations(ctx as unknown as CanvasRenderingContext2D,view,300,220,ops);
  const bLocation=b.location;
  const cells=b.construction.map(v=>v.offset).sort((a,b)=>{const p=space.storage({x:bLocation.x+a.x,y:bLocation.y+a.y,z:a.z}),q=space.storage({x:bLocation.x+b.x,y:bLocation.y+b.y,z:b.z});return p.v-q.v||p.h-q.h||p.z-q.z;});
  for(const c of cells){const state=stage===4&&c.x===0&&c.y===0&&c.z===0?3:stage,p=space.project({x:b.location.x+c.x,y:b.location.y+c.y,z:2+c.z});
   for(const f of sourceParts(c.x,c.y,c.z,state,ground,door&&!(c.x===0&&c.y===2))){
    const r=catalog.renderRecipes.find(r=>r.id===f.id)!,url=catalog.surfaceSets[r.frames[0].surfaceSet!][variantKey(variant)].image.url;let img=images.get(url);if(!img){img=new Image();img.src=url;await img.decode();images.set(url,img);}
    const [x,y,w,h]=f.rect;want.drawImage(img,x,y,w,h,view.x+p.x-f.anchor[0],view.y+p.y-f.anchor[1],w,h);
   }
  }
  const got=ctx.getImageData(0,0,300,220).data,target=want.getImageData(0,0,300,220).data;
  for(let n=0;n<got.length;n++)if(got[n]!==target[n])throw new Error(`Construction pixel ${stage}/${ground}/${door}/${n}`);
  for(let y=0;y<220;y++)for(let x=0;x<300;x++){const hit=scene.pickOperations({x:x+.5,y:y+.5},view,ops);if(!!hit!==(target[(y*300+x)*4+3]>0)||hit&&hit.id!==b.id)throw new Error('Construction alpha');picks++;}
  const i=stage*14+ground*2+Number(door);contact.fillStyle='#d8e5d9';contact.fillRect(i%7*300,Math.floor(i/7)*220,300,220);contact.drawImage(actual,i%7*300,Math.floor(i/7)*220);pixels+=300*220;
 }
 return {png:gallery.toDataURL(),cases:70,pixels,picks};
}

export async function cityLocalPixels(catalog:RenderCatalog){
 const s=trainSceneFixture(source as any,catalog,catalog.renderRecipes.find(r=>r.handler==='train-composition')!,false,200,100);
 s.mixed!.stationGraph={nextStationName:1,nextPlatformName:1,buildings:[],platforms:[],passages:[],listeners:[],listenerMemberships:[]};
 s.mixed!.city=cityScene(200,100).mixed!.city;
 const space=new WorldSpace(200,100,6),head=s.mixed!.fleet!.trains[0].cars[0].state.location!,p=space.storage({...head,x:head.x-2,y:head.y+1});
 addStation(s,stationDefinitions.find(d=>d.sizeX===1&&d.sizeY===2&&d.height===2)!,p.h-2,p.v);
 addPassagePlatform(s,0,{x:head.x+1,y:head.y+1,z:2});
 const site=structuredClone(s),d=fixedDefinitions.find(d=>d.sizeX===2&&d.sizeY===2&&d.minimumHeight===3)!;addCity(site,d,p.h,p.v,0);
 const stage=structuredClone(site);for(const c of stage.mixed!.city!.structures[0].construction)c.cell.state=c.offset.z===0?2:1;
 const done=structuredClone(s);addCity(done,d,p.h,p.v);
 const cache=new RecipeImageCache(),variant:Variant={season:'spring',night:false},empty=await TerrainScene.prepare(s,catalog,cache,variant,5),viewport=new TerrainViewport(empty),q=space.project(head),view={x:180-q.x,y:150-q.y,zoom:.75};
 const canvas=document.createElement('canvas');canvas.width=400;canvas.height=300;const ctx=canvas.getContext('2d')!;
 const settle=(vp:TerrainViewport)=>{for(let n=0;n<1000;n++){ctx.clearRect(0,0,400,300);const r=vp.draw(ctx,view,400,300,2);if(!r.pending)return r;}throw new Error('City viewport never settled');};
 settle(viewport);const reports=[];let previous=empty;
 for(const read of [site,stage,done,s]){
  const scene=await TerrainScene.prepare(read,catalog,cache,variant,5,previous);viewport.setScene(scene);const result=settle(viewport),png=canvas.toDataURL(),actual=ctx.getImageData(0,0,400,300).data;
  const fresh=await TerrainScene.prepare(read,catalog,cache,variant,5),full=new TerrainViewport(fresh);settle(full);const expected=ctx.getImageData(0,0,400,300).data;
  for(let i=0;i<actual.length;i++)if(actual[i]!==expected[i])throw new Error(`City local/full raster ${i}`);
  let picks=0;for(let y=0;y<300;y+=2)for(let x=0;x<400;x+=2){const point={x:x+.5,y:y+.5};if(JSON.stringify(viewport.pick(point,view))!==JSON.stringify(full.pick(point,view)))throw new Error('City local/full pick');picks++;}
  reports.push({png,picks,kind:result.updateKind,columns:scene.changedColumns(previous)});previous=scene;
 }
 return reports;
}
export async function cityCutGallery(catalog:RenderCatalog){
 const gallery=document.createElement('canvas');gallery.width=6*200;gallery.height=11*120;const contact=gallery.getContext('2d')!,cache=new RecipeImageCache();let members=0;
 for(const [i,d] of fixedDefinitions.entries()){
  const s=cityScene(),b=addCity(s,d),space=new WorldSpace(40,60,32),scene=await TerrainScene.prepare(s,catalog,cache,{season:'winter',night:true},2),q=space.project(b.location),view={x:30-q.x,y:40-q.y,zoom:1},canvas=new OffscreenCanvas(200,120),ctx=canvas.getContext('2d')!;
  const ops=scene.operations.filter(o=>o.target.kind==='building');scene.paintOperations(ctx as unknown as CanvasRenderingContext2D,view,200,120,ops);
  const declaration=sourceReview.recipes.find(r=>r.contributionId===d.reference.contributionId)!.declaration,[sx,sy]=declaration.children.find(c=>c.tag==='size')!.text.split(',').map(Number);
  for(let x=0;x<sx;x++)for(let y=0;y<sy;y++){
   const p=space.center({x:b.location.x+x,y:b.location.y+y,z:2}),pixel={x:view.x+p.x,y:view.y+p.y};
   const rgba=[...ctx.getImageData(pixel.x,pixel.y,1,1).data];if(JSON.stringify(rgba)!=='[179,115,51,255]')throw new Error('Source brown cut pixel');
   if(scene.pickOperations(pixel,view,ops)?.id!==b.id)throw new Error('Cut owner pick');members++;
  }
  contact.drawImage(canvas,i%6*200,Math.floor(i/6)*120);contact.fillStyle='#17232b';contact.font='12px sans-serif';contact.fillText(String(i+1),i%6*200+8,Math.floor(i/6)*120+110);
 }
 return {png:gallery.toDataURL(),definitions:66,members};
}

// Independent VarHeightBuildingContribution.getSprites + DefaultSpriteLoader.load2D
// oracle, reading pinned XML rather than the production stack selector/crops.
export async function variableCityGallery(catalog:RenderCatalog,variant:Variant){
 const {cityProfile}=await import('../../src/lib/city-state.ts');
 const definitions=cityProfile.structures.filter(d=>d.kind==='variable'),cache=new RecipeImageCache(),images=new Map<string,HTMLImageElement>();
 const gallery=document.createElement('canvas');gallery.width=6*200;gallery.height=Math.ceil(definitions.length*2/6)*180;const contact=gallery.getContext('2d')!;
 let cases=0,pixels=0,picks=0;
 for(const d of definitions)for(const height of [d.minimumHeight,d.maximumHeight]){
  const s=cityScene(),b=addCity(s,d,10,22,undefined,0,false,height),space=new WorldSpace(40,60,32),scene=await TerrainScene.prepare(s,catalog,cache,variant,31),ops=scene.operations.filter(o=>o.target.kind==='building');
  const actual=new OffscreenCanvas(400,320),expected=new OffscreenCanvas(400,320),ctx=actual.getContext('2d')!,want=expected.getContext('2d')!,q=space.project(b.location),view={x:130-q.x,y:270-q.y,zoom:1};
  scene.paintOperations(ctx as unknown as CanvasRenderingContext2D,view,400,320,ops);
  const e=sourceReview.recipes.find(r=>r.contributionId===d.reference.contributionId)!.declaration,pictures=e.children.find(c=>c.tag==='pictures')!,bottoms=pictures.children.filter(c=>c.tag==='bottom'),tops=pictures.children.filter(c=>c.tag==='top'),middle=pictures.children.find(c=>c.tag==='middle')!;
  const cells=[];for(let x=0;x<d.sizeX;x++)for(let y=0;y<d.sizeY;y++)for(let z=0;z<height;z++)cells.push({x,y,z});
  cells.sort((a,c)=>{const p=space.storage({x:b.location.x+a.x,y:b.location.y+a.y,z:a.z}),q=space.storage({x:b.location.x+c.x,y:b.location.y+c.y,z:c.z});return p.v-q.v||p.h-q.h||p.z-q.z;});
  const recipe=catalog.renderRecipes.find(r=>r.id===d.reference.recipeId)!;
  for(const c of cells){
   const prefix=c.z>=height-tops.length?`top/${height-c.z-1}/`:c.z<bottoms.length?`bottom/${c.z}/`:'middle/',node=c.z>=height-tops.length?tops[height-c.z-1]:c.z<bottoms.length?bottoms[c.z]:middle;
   const a=node.attributes as Record<string,string>,[ox,oy]=a.origin.split(',').map(Number),h=Number(a.offset??(node===middle?16:d.sizeX*8)),max=Number(a.height??2147483647);
   const f=recipe.frames.find(f=>f.key.startsWith(prefix)&&f.cell[0]===c.x&&f.cell[1]===c.y)!;if(f.status==='invisible')continue;
   const url=catalog.surfaceSets[f.surfaceSet!][variantKey(variant)].image.url;
   let image=images.get(url);if(!image){image=new Image();image.src=url;await image.decode();images.set(url,image);}
   const p=space.project({x:b.location.x+c.x,y:b.location.y+c.y,z:b.location.z+c.z}),anchor=h+(c.y-c.x)*8;
   const sx=ox+(c.x+c.y)*16,left=Math.max(0,sx),right=Math.min(image.width,sx+32),drawHeight=Math.min(max,anchor+16);
   if(drawHeight>0&&right>left)want.drawImage(image,left,oy,right-left,drawHeight,view.x+p.x+left-sx,view.y+p.y-anchor,right-left,drawHeight);
  }
  const got=ctx.getImageData(0,0,400,320).data,target=want.getImageData(0,0,400,320).data;
  for(let n=0;n<got.length;n++)if(got[n]!==target[n])throw new Error(`Variable crop pixel ${d.reference.recipeId}/${height}/${variantKey(variant)}/${n}`);
  for(let y=0;y<320;y++)for(let x=0;x<400;x++){
   const hit=scene.pickOperations({x:x+.5,y:y+.5},view,ops);if(!!hit!==(target[(y*400+x)*4+3]>0)||hit&&hit.id!==b.id)throw new Error('Variable alpha pick');picks++;
  }
  const cut=await TerrainScene.prepare(s,catalog,cache,variant,2),cuts=cut.operations.filter(o=>o.target.kind==='building');
  if(cuts.length!==d.sizeX*d.sizeY||cuts.some(o=>!('color'in o)||o.color!=='#808080'))throw new Error('Variable gray base cut');
  contact.fillStyle='#d8e5d9';contact.fillRect(cases%6*200,Math.floor(cases/6)*180,200,180);contact.drawImage(actual,cases%6*200,Math.floor(cases/6)*180,200,160);
  contact.fillStyle='#17232b';contact.font='11px sans-serif';contact.fillText(`${cases+1}: ${d.sizeX}×${d.sizeY}×${height}`,cases%6*200+8,Math.floor(cases/6)*180+174);cases++;pixels+=400*320;
 }
 return {png:gallery.toDataURL(),definitions:definitions.length,cases,pixels,picks};
}

// HalfVoxelContribution.loadSprites offsets, independently from runtime selectors.
export async function halfCityGallery(catalog:RenderCatalog,variant:Variant){
 const {cityProfile}=await import('../../src/lib/city-state.ts'),definitions=cityProfile.structures.filter(d=>d.kind==='half');
 const cache=new RecipeImageCache(),key=variantKey(variant),actual=new OffscreenCanvas(160,120),expected=new OffscreenCanvas(160,120),ctx=actual.getContext('2d')!,want=expected.getContext('2d')!;
 const gallery=document.createElement('canvas');gallery.width=8*160;gallery.height=12*140;const contact=gallery.getContext('2d')!;
 const offsets=[[0,8],[-8,8],[0,8],[-8,8],[-8,12],[0,12],[-8,12],[0,12]],names=['north','east','south','west'];
 let cases=0,pixels=0,picks=0;
 const load=async(id:string)=>{const r=catalog.renderRecipes.find(r=>r.id===id)!,sheet=new Image();sheet.src=catalog.surfaceSets[r.frames[0].surfaceSet!][key].image.url;await sheet.decode();return sheet;};
 const ground=await load('system:ground/spring');
 for(const [di,d] of definitions.entries())for(const direction of [0,2,4,6])for(const mode of ['back','fore','pair'] as const){
  const s=cityScene(),b=addCity(s,d),second=definitions[(di+1)%8];
  b.halves=mode==='pair'?[{definition:second.reference,side:'fore',direction:(direction+4)%8,color:0},{definition:d.reference,side:'back',direction,color:0}]:[{definition:d.reference,side:mode,direction,color:0}];
  const space=new WorldSpace(40,60,32),scene=await TerrainScene.prepare(s,catalog,cache,variant,31),ops=scene.operations.filter(o=>o.target.kind==='building'),q=space.project(b.location),view={x:60-q.x,y:80-q.y,zoom:1};
  ctx.clearRect(0,0,160,120);want.clearRect(0,0,160,120);scene.paintOperations(ctx as unknown as CanvasRenderingContext2D,view,160,120,ops);
  want.drawImage(ground,0,0,32,16,60,80,32,16);
  for(const half of [...b.halves].sort((a,b)=>(a.side==='back'?0:1)-(b.side==='back'?0:1))){
   const e=sourceReview.recipes.find(r=>r.contributionId===half.definition.contributionId)!.declaration,sprite=e.children.find(n=>n.tag==='sprite')!;
   const pattern=sprite.children.find(n=>n.tag==='pattern'&&n.attributes.direction===names[half.direction/2]&&(n.attributes.side===half.side||n.attributes.side==='either'))!;
   const [x,y]=pattern.attributes.origin.split(',').map(Number),sheet=await load(half.definition.recipeId),off=offsets[half.direction/2+(half.side==='fore'?0:4)];
   want.drawImage(sheet,x,y,24,24,60-off[0],80-off[1],24,24);
  }
  const got=ctx.getImageData(0,0,160,120).data,target=want.getImageData(0,0,160,120).data;
  for(let n=0;n<got.length;n++)if(got[n]!==target[n])throw new Error(`Half raw crop ${d.reference.recipeId}/${direction}/${mode}/${key}/${n}`);
  for(let y=0;y<120;y++)for(let x=0;x<160;x++){
   const hit=scene.pickOperations({x:x+.5,y:y+.5},view,ops);if(!!hit!==(target[(y*160+x)*4+3]>0)||hit&&hit.id!==b.id)throw new Error('Half owner alpha pick');picks++;
  }
  contact.fillStyle='#d8e5d9';contact.fillRect(cases%8*160,Math.floor(cases/8)*140,160,140);contact.drawImage(actual,cases%8*160,Math.floor(cases/8)*140);
  contact.fillStyle='#17232b';contact.font='11px sans-serif';contact.fillText(`${di+1} ${names[direction/2]} ${mode}`,cases%8*160+5,Math.floor(cases/8)*140+134);cases++;pixels+=160*120;
 }
 return {png:gallery.toDataURL(),definitions:definitions.length,cases,pixels,picks};
}
