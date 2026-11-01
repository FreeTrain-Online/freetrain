import assert from 'node:assert/strict';
import {TerrainScene} from '../src/lib/terrain-renderer.ts';
import {RailwayRenderPlan,pickRailwayLocation} from '../src/lib/railway-renderer.ts';
import {RecipeImageCache,type RenderCatalog,type Variant} from '../src/lib/content-recipes.ts';
import {WorldSpace} from '../src/lib/map-geometry.ts';
import {validSnapshot} from '../src/lib/world-identity.ts';
import type {Snapshot,Position,RailwayVoxel} from '../src/generated/contracts.ts';
const catalog=JSON.parse(await Deno.readTextFile(new URL('../public/content/bundled-v2/catalog.json',import.meta.url))) as RenderCatalog;
const geometryFixtures=JSON.parse(await Deno.readTextFile(new URL('../../doc/parity/railway-fixtures.json',import.meta.url)));
const original=JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/fixtures/snapshot-mixed.json',import.meta.url))) as Snapshot;
const day:Variant={season:'spring',night:false},view={x:0,y:0,zoom:1};
const definition=(id:string,recipeId=id)=>({packId:'exav-stable-bundled',contributionId:id,recipeId});
function flat(w=3,h=5,ground=1):Snapshot {
 const s=structuredClone(original);s.map={width:w,height:h,levels:6,coordinateSystem:'hv'};s.revision='0';s.cash='15000000000';s.entities=[];
 s.terrain={...s.terrain!,geometryRevision:'0',nextEntityId:'1',waterLevel:0,occupants:[],cells:Array.from({length:w*h},()=>({ground,surface:'cut'}))};
 const r=JSON.parse(s.terrain.provenance.requestJson);r.Dimensions={H:w,V:h,Z:6};s.terrain.provenance.requestJson=JSON.stringify(r);
 s.mixed={geometryRevision:'0',nextEntityId:'1',voxels:[],slopes:[],accounts:{initialCash:s.cash,bankruptcyIncrement:'10000000000',transactions:[]}};return s;
}
function rail(s:Snapshot,h:number,v:number,z:number,pattern=1):RailwayVoxel {
 const p=new WorldSpace(s.map.width,s.map.height,s.map.levels).location({h,v,z}),id=s.mixed!.nextEntityId;
 const voxel:RailwayVoxel={position:p,ownerId:id,kind:'traffic',traffic:{rail:{definition:definition(`system:pattern/${pattern}`),pattern,variant:0,direction:0,controllerId:null,terrainBackup:null},road:null,accessory:null,car:null,platform:null},definition:null,sprite:0,drawGroundBelow:true,drawGroundAbove:true,supportedOwnerId:null};
 s.entities!.push({id,revision:'0',definition:definition('system:traffic','geometry:traffic-components-1'),anchor:p,footprint:[{offset:{x:0,y:0,z:0},transparent:true}],owned:true,silentlyReclaimable:false,value:'0'});
 s.mixed!.voxels.push(voxel);s.mixed!.nextEntityId=String(BigInt(id)+1n);return voxel;
}
function pier(s:Snapshot,h:number,v:number,z:number,supportedOwnerId:string) {
 const r=rail(s,h,v,z);r.kind='pier';r.traffic=null;r.definition=definition('system:pier');r.supportedOwnerId=supportedOwnerId;s.entities!.at(-1)!.definition=r.definition;return r;
}
function cache(c=catalog,onDecode=()=>{}) {return new RecipeImageCache(async media=>{onDecode();const b=Object.values(c.surfaceSets).flatMap(Object.values).find(b=>b.image.url===media.url)!;return {image:{} as CanvasImageSource,width:b.width,height:b.height};});}
const prepare=(s:Snapshot,cut=5,c=catalog,previous?:TerrainScene)=>TerrainScene.prepare(s,c,cache(c),day,cut,previous);
const railwayOps=(scene:TerrainScene)=>scene.operations.filter(op=>op.target.kind==='railway');
function opaquePoint(scene:TerrainScene,id:string):Position&{a:number;b:number} {
 const op=railwayOps(scene).find(o=>o.target.id===id&&'layer' in o)!;assert('layer' in op);
 const l=op.layer,[x,y,z]=l.position,[ax,ay]=l.frame.anchor!,mask=catalog.masks[l.frame.masks!['spring/day']];
 for(let b=0;b<mask.length;b++)for(let a=0;a<mask[b].length;a++)if(mask[b][a]==='1') {
  const p={a:16*(x+y)-ax+a+.5,b:8*(y-x)-16*z-ay+b+.5,...op.target.position};
  if(scene.pick({x:p.a,y:p.b},view)?.id===id)return p;
 }
 throw new Error('No visible independent mask sample');
}
Deno.test('all 28 normal/junction recipes use literal anchors, alpha picks and saved owner IDs in eight seasonal light variants',async()=>{
 for(const season of ['spring','summer','autumn','winter'] as const)for(const night of [false,true])for(let pattern=0;pattern<28;pattern++) {
  const s=flat(1,1);rail(s,0,0,1,pattern);assert(validSnapshot(s));const before=JSON.stringify(s);
  const scene=await TerrainScene.prepare(s,catalog,cache(),{season,night},5),ops=railwayOps(scene);assert.equal(ops.length,1);assert('layer' in ops[0]);
  assert.equal(ops[0].layer.recipeId,`system:pattern/${pattern}`);assert.deepEqual(ops[0].layer.frame.anchor,geometryFixtures.patterns[pattern].anchor);assert.deepEqual(ops[0].layer.frame.rectangle,geometryFixtures.patterns[pattern].rectangle);assert.equal(ops[0].target.id,'1');
  const p=opaquePoint(scene,'1');assert.equal(scene.pick({x:p.a,y:p.b},view)?.kind,'railway');assert.equal(scene.pick({x:p.a*2+30,y:p.b*2+50},{x:30,y:50,zoom:2})?.id,'1');
  assert.equal(JSON.stringify(s),before);
 }
});
Deno.test('asymmetric columns retain source V/H/Z ordering, elevated piers and ground over buried rails',async()=>{
 const s=flat(3,5,2),high=rail(s,0,0,4,2);pier(s,0,0,3,high.ownerId);rail(s,2,1,2,17);rail(s,1,4,1,10);
 const scene=await prepare(s);const ops=scene.operations;
 const order=ops.map(op=>{const p=new WorldSpace(3,5,6).storage(op.target.position);return [p.v,p.h,p.z];});
 for(let i=1;i<order.length;i++)assert(order[i][0]>order[i-1][0]||order[i][0]===order[i-1][0]&&(order[i][1]>order[i-1][1]||order[i][1]===order[i-1][1]&&order[i][2]>=order[i-1][2]));
 assert.deepEqual(railwayOps(scene).map(o=>[o.target.id,o.target.position.z]),[['2',3],['1',4],['3',2],['4',1]]);
 const p=opaquePoint(scene,high.ownerId);assert.equal(scene.pick({x:p.a,y:p.b},view)?.id,high.ownerId);
 // The buried track is visited first, then covered by the actual ground chip.
 const last=ops.filter(o=>o.target.position.x===s.mixed!.voxels[3].position.x&&o.target.position.y===s.mixed!.voxels[3].position.y);
 assert.equal(last.at(-1)!.target.kind,'terrain');
});
Deno.test('cut includes exactly cutZ, full view hides underwater rails, cut mode reveals underwater and underground rails',async()=>{
 const s=flat(1,1,1);s.terrain!.waterLevel=3;rail(s,0,0,1,10);rail(s,0,0,4,4);
 assert.deepEqual(railwayOps(await prepare(s)).map(o=>o.target.position.z),[4]);
 assert.deepEqual(railwayOps(await prepare(s,4)).map(o=>o.target.position.z),[1,4]);
 assert.deepEqual(railwayOps(await prepare(s,3)).map(o=>o.target.position.z),[1]);
 const buried=flat(1,1,3);rail(buried,0,0,1,1);const cut=await prepare(buried,1),p=opaquePoint(cut,'1');assert.equal(cut.pick({x:p.a,y:p.b},view)?.kind,'railway');
 assert(!railwayOps(await prepare(buried,0)).length);
});
Deno.test('saved holes honor source floor/ceiling exceptions and drawGroundBelow/Above independently',()=>{
 const s=flat(1,1);const top=rail(s,0,0,2);top.kind='slopefiller';top.traffic=null;top.drawGroundBelow=false;top.drawGroundAbove=false;
 // Test the drawing contract independently of the full eight-cell slope graph.
 const plan=new RailwayRenderPlan(s,catalog,day),p=(z:number)=>({x:0,y:0,z});
 assert.equal(plan.drawGround(p(2),6),false);assert.equal(plan.drawGround(p(3),6),false);assert.equal(plan.drawGround(p(1),6),true);
 rail(s,0,0,1);const submerged=new RailwayRenderPlan(s,catalog,day);
 assert.deepEqual(submerged.operations(0,5,2,true,3).map(o=>o.target.position.z),[1],'Hole at the water start includes the source one-lower voxel');
 assert.equal(submerged.operations(0,5,2,true,1).length,0);
 const ceiling=flat(1,1),v=rail(ceiling,0,0,5);v.kind='slopefiller';v.traffic=null;v.drawGroundBelow=false;
 assert.equal(new RailwayRenderPlan(ceiling,catalog,day).drawGround(p(5),6),true);
});
Deno.test('signal dispatch retains GEO-007 and pattern art, with a reserved car slot between before and after',()=>{
 for(const [id,stages] of [['{5349DE2A-DF3E-49B4-87A0-973105B7E6A4}',[40,10,10,40]],['{6FA55842-E693-414f-A120-0A3D0C613B21}',[10,40,40,10]]] as const)for(let i=0;i<4;i++) {
  const s=flat(1,1),v=rail(s,0,0,1,i%2?4:10);v.traffic!.rail!.definition=definition(id);v.traffic!.rail!.direction=i*2;v.traffic!.rail!.controllerId='99';
  const plan=new RailwayRenderPlan(s,catalog,day),ops=plan.operations(0,5,0,true,1),signal=ops.find(o=>o.layer.recipeId===id)!;
  assert.equal(signal.layer.frame.stage,stages[i]);assert.equal(ops.length,2);assert.equal(ops[0].layer.recipeId,stages[i]===10?id:`system:pattern/${v.traffic!.rail!.pattern}`);
  for(const op of ops){assert.equal(op.target.definition.contributionId,id);assert.equal(op.target.controllerId,'99');}
 }
});
Deno.test('controller picking uses descending cut candidates, fixed level and stored boundaries independently of transparent art',()=>{
 const s=flat(3,15),space=new WorldSpace(3,15,6),r=rail(s,1,10,4),q=space.center(r.position);
 assert.deepEqual(pickRailwayLocation(s,q,5,{kind:'railOrGround'}),r.position);
 assert.equal(pickRailwayLocation(s,q,3,{kind:'railOrGround'})?.z,1);
 assert.equal(pickRailwayLocation(s,q,5,{kind:'ground'})?.z,1);
 const empty=space.location({h:2,v:14,z:3});assert.deepEqual(pickRailwayLocation(s,space.center(empty),5,{kind:'sameLevel',level:3}),empty);
 assert.equal(pickRailwayLocation(s,q,2,{kind:'sameLevel',level:3}),undefined);
 assert.equal(pickRailwayLocation(s,{x:-5000,y:-5000},5,{kind:'railOrGround'}),undefined);
});
Deno.test('unknown, invalid and missing active-variant rail recipes reject readiness even offscreen or above the cut',async()=>{
 const s=flat();rail(s,2,4,5,17);
 for(const mutate of [(c:RenderCatalog)=>{c.renderRecipes=c.renderRecipes.filter(r=>r.id!=='system:pattern/17');},(c:RenderCatalog)=>{c.renderRecipes.find(r=>r.id==='system:pattern/17')!.frames[0].status='invalid-source-crop';},(c:RenderCatalog)=>{delete c.renderRecipes.find(r=>r.id==='system:pattern/17')!.frames[0].masks!['spring/day'];}]) {
  const c=structuredClone(catalog);mutate(c);await assert.rejects(()=>prepare(s,0,c));
 }
});
Deno.test('geometry cache reuses untouched columns and decoded sheets, invalidates changed rail and retains the old immutable picks',async()=>{
 const s=flat(),r=rail(s,0,0,3,1);rail(s,2,4,1,10);let decodes=0;const images=cache(catalog,()=>decodes++);
 const old=await TerrainScene.prepare(s,catalog,images,day,5),oldOps=old.operations.slice(),count=decodes;
 const next=structuredClone(s);next.mixed!.voxels[0].traffic!.rail!.pattern=2;next.mixed!.voxels[0].traffic!.rail!.definition=definition('system:pattern/2');
 const scene=await TerrainScene.prepare(next,catalog,images,day,5,old);
 const unchanged=oldOps.find(o=>o.target.id==='2'&&o.target.kind==='railway');assert(scene.operations.includes(unchanged!));
 const changed=oldOps.find(o=>o.target.id===r.ownerId&&o.target.kind==='railway');assert(!scene.operations.includes(changed!));
 assert.deepEqual(old.operations,oldOps);assert.equal(decodes,count);
 const fresh=await prepare(next);assert.deepEqual(scene.operations,fresh.operations);
});
Deno.test('mixed persistence fixture renders exact tunnel, signal and orphan-capable pier bindings without state mutation',async()=>{
 const before=JSON.stringify(original),scene=await prepare(original);assert(railwayOps(scene).length>=4);assert.equal(JSON.stringify(original),before);
});
Deno.test('all 15 literal legal ramps retain paired holes, invisible occupancy and source support anchors at every cut',async()=>{
 const fixtures=JSON.parse(await Deno.readTextFile(new URL('../../doc/parity/railway-fixtures.json',import.meta.url)));
 let count=0;
 for(const row of fixtures.groups.slopes.cases.filter((r:any)=>r.expected.outcome==='apply')) {
  const s=flat(32,16,2);s.map.levels=8;const request=JSON.parse(s.terrain!.provenance.requestJson);request.Dimensions.Z=8;s.terrain!.provenance.requestJson=JSON.stringify(request);
  const space=new WorldSpace(32,16,8),anchor={x:row.input.base[0],y:row.input.base[1],z:row.input.base[2]},position=(p:number[])=>({x:p[0],y:p[1],z:p[2]});
  s.terrain!.waterLevel=row.input.water;
  const voxels:RailwayVoxel[]=row.expected.rails.map((r:any,i:number)=>{
   const p=position(r.location),storage=space.storage(p);s.terrain!.cells[storage.v*32+storage.h].ground=row.input.ground[i];
   return {position:p,ownerId:'1',kind:'traffic',traffic:{rail:{definition:definition(`system:pattern/${r.patternId}`),pattern:r.patternId,variant:0,direction:0,controllerId:null,terrainBackup:null},road:null,accessory:null,car:null,platform:null},definition:null,sprite:0,drawGroundBelow:true,drawGroundAbove:true,supportedOwnerId:null};
  });
  for(const [i,p] of row.expected.paired.entries()) {
   const kind=p.kind==='filler'?'slopefiller':p.kind==='support'?'slopesupport':'slopewall';
   voxels.push({position:position(p.location),ownerId:'1',kind,traffic:null,definition:kind==='slopefiller'?null:definition(`system:pattern/${kind==='slopewall'?'underground-':''}slope-support-${row.input.direction/2*4+i}`),sprite:p.sprite??0,drawGroundBelow:p.below,drawGroundAbove:p.above,supportedOwnerId:null});
  }
  s.mixed!.voxels=voxels;s.mixed!.slopes=[{entityId:'1',climb:row.input.direction}];s.mixed!.nextEntityId='2';
  s.entities=[{id:'1',revision:'0',definition:definition('system:slope','geometry:eight-cell-slope-1'),anchor,footprint:voxels.map(v=>({offset:{x:v.position.x-anchor.x,y:v.position.y-anchor.y,z:v.position.z-anchor.z},transparent:true})),owned:true,silentlyReclaimable:false,value:'0'}];
  assert(validSnapshot(s),row.id);
  const saved=JSON.stringify(s);
  for(let cut=0;cut<8;cut++) {
   const scene=await prepare(s,cut);
   assert(railwayOps(scene).every(op=>op.target.position.z<=cut));
   assert(!scene.operations.some(op=>op.target.kind==='railway'&&voxels.some(v=>v.kind==='slopefiller'&&JSON.stringify(v.position)===JSON.stringify(op.target.position))));
   for(const p of row.expected.paired) {
    // A source flag suppresses the ordinary cut/ground chip at its matching plane.
    const z=p.below?p.location[2]+1:p.location[2];
    if((!p.below||!p.above)&&z===cut&&z<s.map.levels-1)assert(!scene.operations.some(op=>op.target.kind==='terrain'&&op.target.position.x===p.location[0]&&op.target.position.y===p.location[1]&&op.target.position.z===z&&'layer' in op&&op.layer.recipeId.startsWith('system:ground/')),row.id);
   }
  }
  assert.equal(JSON.stringify(s),saved);count++;
 }
 assert.equal(count,15);
});
Deno.test('large mixed viewport publishes matching railway pixels and picks together and reuses two bounded buffers',async()=>{
 const {TerrainViewport}=await import('../src/lib/terrain-viewport.ts');
 const descriptor=Object.getOwnPropertyDescriptor(globalThis,'OffscreenCanvas'),canvases:{width:number;height:number}[]=[];
 const context=()=>{let scale=1;return new Proxy({setTransform:(a:number)=>{scale=a;},getTransform:()=>({a:scale})},{get:(t,k)=>Reflect.get(t,k)??(()=>{})}) as unknown as CanvasRenderingContext2D;};
 class Canvas {private ctx=context();constructor(public width:number,public height:number){canvases.push(this);}getContext(){return this.ctx;}}
 Object.defineProperty(globalThis,'OffscreenCanvas',{configurable:true,writable:true,value:Canvas});
 try {
  const s=flat(200,100);const r=rail(s,2,20,3,1),images=cache(),old=await TerrainScene.prepare(s,catalog,images,day,5),viewport=new TerrainViewport(old),ctx=context();
  const camera={x:-64,y:-100,zoom:1},p={x:16.5,y:20.5}; // Rail H2,V20,Z3 origin (64,112), plus opaque (16.5,8.5).
  const settle=()=>{for(let i=0;i<1000;i++)if(!viewport.draw(ctx,camera,128,96,1).pending)return;throw new Error('Mixed viewport did not settle');};
  assert(!viewport.draw(ctx,camera,128,96,1).ready);settle();assert.equal(viewport.pick(p,camera)?.id,r.ownerId);assert.deepEqual(viewport.pickLocation(p,camera,{kind:'railOrGround'}),r.position);
  const next=structuredClone(s);next.entities=[];next.mixed!.voxels=[];
  const scene=await TerrainScene.prepare(next,catalog,images,day,5,old);viewport.setScene(scene);
  assert(viewport.draw(ctx,camera,128,96,1).ready);assert.equal(viewport.pick(p,camera)?.id,r.ownerId,'Displayed railway keeps its old pick while replacement is pending');assert.deepEqual(viewport.pickLocation(p,camera,{kind:'railOrGround'}),r.position);
  settle();assert.notEqual(viewport.pick(p,camera)?.kind,'railway');assert.notDeepEqual(viewport.pickLocation(p,camera,{kind:'railOrGround'}),r.position);
  assert.equal(canvases.filter(c=>c.width===256&&c.height===288).length,2);
 }finally {if(descriptor)Object.defineProperty(globalThis,'OffscreenCanvas',descriptor);else Reflect.deleteProperty(globalThis,'OffscreenCanvas');}
});
Deno.test('S/W slope pier binding preserves the pinned second core crop, anchor and saved selector',async()=>{
 const recipe=catalog.renderRecipes.find(r=>r.id==='system:pier/slopeNE')!;
 assert.equal(recipe.sourceSymbol,'BridgePierVoxel.slopeNESprite');
 assert.deepEqual(recipe.frames[0].rectangle,[32,0,32,32]);assert.deepEqual(recipe.frames[0].anchor,[0,16]);
 const s=flat(1,1,0),r=rail(s,0,0,3,4),p=pier(s,0,0,1,r.ownerId);
 p.definition=definition('system:pier/slopeNE');p.sprite=1;s.entities!.at(-1)!.definition=p.definition;
 assert(validSnapshot(s));const before=JSON.stringify(s),scene=await prepare(s);
 const op=railwayOps(scene).find(o=>o.target.id===p.ownerId)!;assert('layer' in op);
 assert.equal(op.layer.recipeId,'system:pier/slopeNE');assert.deepEqual(op.layer.frame.rectangle,[32,0,32,32]);
 assert.equal(JSON.stringify(s),before);
 const invalid=structuredClone(s);invalid.mixed!.voxels[1].sprite=0;assert(!validSnapshot(invalid));
});

Deno.test('bridge begin middle end and all four pier crops retain exact source frames and foreground dispatch in eight variants',async()=>{
 const id='{874B8BFD-1EF4-4828-9C5A-D0EA1049D28D}';
 for(const season of ['spring','summer','autumn','winter'] as const)for(const night of [false,true]){
  for(let index=0;index<6;index++){
   const s=flat(1,1,0),v=rail(s,0,0,3,index<3?4:10);v.traffic!.rail!.definition=definition(id);v.traffic!.rail!.variant=index;
   assert(validSnapshot(s));const plan=new RailwayRenderPlan(s,catalog,{season,night});
   assert.deepEqual(plan.layers.map(l=>l.frame.stage),[10,40]);
   assert.deepEqual(plan.layers.map(l=>l.frame.rectangle),[[index*32,0,32,32],[index*32,32,32,32]]);
   assert(plan.layers.every(l=>l.frame.anchor?.[0]===0&&l.frame.anchor?.[1]===16));
  }
  for(let style=0;style<4;style++){
   const s=flat(1,1,0),v=rail(s,0,0,3,4);v.traffic!.rail!.definition=definition(id);v.traffic!.rail!.variant=2;
   const p=pier(s,0,0,1,v.ownerId);p.definition=definition(`system:bridge-pier/${style}`,id);p.sprite=style+2;s.entities!.at(-1)!.definition=p.definition;
   assert(validSnapshot(s));const plan=new RailwayRenderPlan(s,catalog,{season,night}),layers=plan.layers.filter(l=>l.entityId===p.ownerId);
   assert.equal(layers.length,1,'Pier frames must survive special-rail default index filtering');
   assert.deepEqual(layers[0].frame.rectangle,[style%2*32,Math.floor(style/2)*32,32,32]);
   assert.equal(layers[0].frame.stage,0);
   const scene=await TerrainScene.prepare(s,catalog,cache(),{season,night},5);assert(railwayOps(scene).some(o=>o.target.id===p.ownerId));
  }
 }
});

Deno.test('girder four literal crops draw only before cars, preserve alpha picks and obey cuts in eight variants',async()=>{
 const id='{7FD493FF-D6A7-4a13-A471-FB737641D8A0}';
 for(const season of ['spring','summer','autumn','winter'] as const)for(const night of [false,true])for(const [direction,pattern,index] of [[0,10,3],[1,1,1],[2,4,0],[3,7,2],[4,10,3],[5,1,1],[6,4,0],[7,7,2]]){
  const s=flat(1,1,0),v=rail(s,0,0,3,pattern),r=v.traffic!.rail!;r.definition=definition(id);r.variant=index;r.direction=direction;
  assert(validSnapshot(s));const saved=JSON.stringify(s),plan=new RailwayRenderPlan(s,catalog,{season,night});
  assert.equal(plan.layers.length,1);assert.equal(plan.layers[0].frame.stage,10);
  assert.deepEqual(plan.layers[0].frame.rectangle,[index*32,0,32,32]);assert.deepEqual(plan.layers[0].frame.anchor,[0,16]);
  const scene=await TerrainScene.prepare(s,catalog,cache(),{season,night},5),p=opaquePoint(scene,v.ownerId);
  assert.equal(scene.pick({x:p.a,y:p.b},view)?.id,v.ownerId);assert.equal(railwayOps(await prepare(s,2)).length,0);
  assert.equal(JSON.stringify(s),saved);
 }
});
Deno.test('tunnel two-axis backgrounds and foregrounds use saved corners, alpha picking and cut visibility in eight variants',async()=>{
 const id='{3E66FC7A-2B58-4182-8A8E-2D5A01BE78E5}';
 for(const season of ['spring','summer','autumn','winter'] as const)for(const night of [false,true])for(const [direction,pattern,index] of [[0,10,1],[2,4,0],[4,10,1],[6,4,0]]){
  const s=flat(1,1,2),v=rail(s,0,0,2,pattern),r=v.traffic!.rail!;r.definition=definition(id);r.variant=index;r.direction=direction;r.terrainBackup={ne:0,se:1,sw:2,nw:3};
  assert(validSnapshot(s));const saved=JSON.stringify(s),plan=new RailwayRenderPlan(s,catalog,{season,night});
  assert.deepEqual(plan.layers.map(l=>l.frame.stage),[10,40]);
  assert.deepEqual(plan.layers.map(l=>l.frame.rectangle),[[index*32,0,32,32],[index*32+64,0,32,32]]);
  for(const l of plan.layers)assert.deepEqual(l.frame.anchor,[0,16]);
  const scene=await TerrainScene.prepare(s,catalog,cache(),{season,night},2),p=opaquePoint(scene,v.ownerId);
  assert.equal(scene.pick({x:p.a,y:p.b},view)?.id,v.ownerId);assert.equal(railwayOps(await prepare(s,1)).length,0);
  assert.equal(JSON.stringify(s),saved);
 }
});
Deno.test('natural terrain continuity uses exact tunnel backups and rejects a tampered corner or an overlapping mountain',()=>{
 const s=flat(3,5,2);for(const cell of s.terrain!.cells)cell.surface='natural';
 const v=rail(s,1,2,2,4),r=v.traffic!.rail!;r.definition=definition('{3E66FC7A-2B58-4182-8A8E-2D5A01BE78E5}');r.terrainBackup={ne:0,se:0,sw:0,nw:0};
 assert(validSnapshot(s));r.terrainBackup.ne=1;assert.equal(validSnapshot(s),false);
 r.terrainBackup.ne=0;v.position.z=1;s.entities![0].anchor.z=1;assert.equal(validSnapshot(s),false);
});
Deno.test('garage axes dispatch exact background and foreground crops with independent picking in every light and season',async()=>{
 for(const season of ['spring','summer','autumn','winter'] as const)for(const night of [false,true])for(const [pattern,index] of [[4,0],[10,1]]){
  const s=flat(1,1),v=rail(s,0,0,1,pattern);
  v.traffic!.rail!.definition=definition('{93481807-D998-46d7-B1BE-CB2B5D086D67}');v.traffic!.rail!.variant=index;
  assert(validSnapshot(s));const scene=await TerrainScene.prepare(s,catalog,cache(),{season,night},5),ops=railwayOps(scene);
  assert.equal(ops.length,2);
  assert.deepEqual(ops.map(op=>{assert('layer' in op);return op.layer.frame.rectangle;}),[[32*index,0,32,27],[32*index+64,0,32,27]]);
  assert.deepEqual(ops.map(op=>{assert('layer' in op);return op.layer.frame.anchor;}),[[0,11],[0,11]]);
  const p=opaquePoint(scene,v.ownerId);assert.equal(scene.pick({x:p.a,y:p.b},view)?.id,v.ownerId);
  assert.equal(railwayOps(await TerrainScene.prepare(s,catalog,cache(),{season,night},0)).length,0);
 }
});
Deno.test('signal types and directions retain literal crops, picking and cuts across eight seasonal light variants',async()=>{
 for(const id of ['{5349DE2A-DF3E-49B4-87A0-973105B7E6A4}','{6FA55842-E693-414f-A120-0A3D0C613B21}'])for(const direction of [0,2,4,6])for(const season of ['spring','summer','autumn','winter'] as const)for(const night of [false,true]){
  const s=flat(1,1),v=rail(s,0,0,1,direction%4===0?10:4);v.traffic!.rail!.definition=definition(id);v.traffic!.rail!.direction=direction;
  assert(validSnapshot(s));const scene=await TerrainScene.prepare(s,catalog,cache(),{season,night},5);
  const ops=railwayOps(scene),signal=ops.find(o=>'layer' in o&&o.layer.recipeId===id)!;assert('layer' in signal);
  assert.deepEqual(signal.layer.frame.rectangle,[direction/2*32,0,32,24]);assert.deepEqual(signal.layer.frame.anchor,[0,8]);
  const p=opaquePoint(scene,'1');assert.equal(scene.pick({x:p.a,y:p.b},view)?.kind,'railway');
  assert.equal(railwayOps(await TerrainScene.prepare(s,catalog,cache(),{season,night},0)).length,0);
 }
});
Deno.test('all bundled poles dispatch axis-specific before/after crops with exact picking and cuts',async()=>{
 for(const id of ['{DDF7F835-8100-45fd-8AE7-EC498796D387}','{DD7D4548-5E0E-48e4-AB6E-64A443C240F7}','{86E56A60-19DF-4ba7-BFE8-ABFBDF5DFA0A}','{FD1635C1-05BF-41af-9185-4891B983E2A4}'])for(const [pattern,axis] of [[4,0],[10,1]])for(const season of ['spring','summer','autumn','winter'] as const)for(const night of [false,true]){
  const s=flat(1,1),v=rail(s,0,0,1,pattern);v.traffic!.accessory={definition:definition(id),direction:axis};
  assert(validSnapshot(s));const saved=JSON.stringify(s),scene=await TerrainScene.prepare(s,catalog,cache(),{season,night},5);
  const ops=railwayOps(scene),poles=ops.filter(o=>'layer' in o&&o.layer.recipeId===id);
  assert.equal(poles.length,2);
  assert.deepEqual(poles.map(o=>{assert('layer' in o);return o.layer.frame.rectangle;}),[[axis*32,0,32,32],[axis*32+64,0,32,32]]);
  assert.deepEqual(poles.map(o=>{assert('layer' in o);return o.layer.frame.anchor;}),[[0,16],[0,16]]);
  assert.deepEqual(ops.map(o=>o.target.component),['accessory','rail','accessory']);
  const p=opaquePoint(scene,v.ownerId);const picked=scene.pick({x:p.a,y:p.b},view);assert.equal(picked?.id,v.ownerId);assert(picked?.kind==='railway');assert.equal(picked.component,'accessory');assert.equal(picked.definition.contributionId,id);
  assert.equal(railwayOps(await TerrainScene.prepare(s,catalog,cache(),{season,night},0)).length,0);assert.equal(JSON.stringify(s),saved);
  const hidden=await TerrainScene.prepare(s,catalog,cache(),{season,night,drawPoles:false},5,scene);assert.equal(railwayOps(hidden).length,1);assert.equal(railwayOps(hidden)[0].target.component,'rail');assert.equal(JSON.stringify(s),saved);
  v.traffic!.accessory!.direction=2;assert.equal(validSnapshot(s),false);
 }
});
