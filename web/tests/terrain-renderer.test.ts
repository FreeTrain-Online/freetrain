import { MapRenderer, type MapOptions } from '../src/lib/map-renderer.ts';
import { TerrainScene, mountainPoints, treeOffset, mountainColor, terrainColor, hsv } from '../src/lib/terrain-renderer.ts';
import { RecipeImageCache, type RenderCatalog, type Variant, type View } from '../src/lib/content-recipes.ts';
import type { Snapshot } from '../src/generated/contracts.ts';
import { WorldSpace, move } from '../src/lib/map-geometry.ts';
import { TerrainViewport } from '../src/lib/terrain-viewport.ts';
import { TrainCompositor } from '../src/lib/train-compositor.ts';
import { TerrainOperationIndex } from '../src/lib/terrain-operation-index.ts';
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
async function withViewport(run:(scene:TerrainScene,viewport:TerrainViewport,ctx:CanvasRenderingContext2D,canvases:{width:number;height:number}[])=>void|Promise<void>) {
  const descriptor=Object.getOwnPropertyDescriptor(globalThis,'OffscreenCanvas'),canvases:{width:number;height:number}[]=[];
  const context=()=>{
    let scale=1;
    return new Proxy({setTransform:(a:number)=>{scale=a;},getTransform:()=>({a:scale})},{get:(target,key)=>Reflect.get(target,key)??(()=>{})}) as unknown as CanvasRenderingContext2D;
  };
  class Canvas {
    private ctx=context();
    constructor(public width:number,public height:number){canvases.push(this);}
    getContext(){return this.ctx;}
  }
  Object.defineProperty(globalThis,'OffscreenCanvas',{configurable:true,writable:true,value:Canvas});
  try {
    const scene=await TerrainScene.prepare(flat(200,100),catalog,cache(),day,5);
    await run(scene,new TerrainViewport(scene),context(),canvases);
  }finally{
    if(descriptor)Object.defineProperty(globalThis,'OffscreenCanvas',descriptor);else Reflect.deleteProperty(globalThis,'OffscreenCanvas');
  }
}
Deno.test('progressive terrain publishes matching picks atomically and cancels a newer camera when returning to the completed view',async()=>{
  await withViewport((scene,viewport,ctx)=>{
    const a={x:0,y:32,zoom:1},b={x:-128,y:32,zoom:1};
    const settle=(v:typeof a)=>{for(let n=0;n<1000;n++)if(!viewport.draw(ctx,v,128,96,1).pending)return;throw new Error('Viewport never settled');};
    assert(!viewport.draw(ctx,a,128,96,1).ready);assert(!viewport.pick({x:16,y:24},a));
    settle(a);const operations=scene.operations;
    equal(viewport.pick({x:16,y:24},a)?.position,{x:49,y:0,z:1});
    assert(viewport.draw(ctx,b,128,96,1).pending);
    assert(scene.operations===operations,'Unfinished work must not replace pick operations');
    // x120 in the shifted camera is newly exposed beyond the old image.
    assert(!viewport.pick({x:120,y:24},b));
    assert(!viewport.draw(ctx,a,128,96,1).pending,'Returning Home must cancel obsolete work');
    for(let n=0;n<5;n++)assert(!viewport.draw(ctx,a,128,96,1).pending);
    assert(scene.operations===operations);
    settle(b);assert(scene.operations!==operations);
    equal(viewport.pick({x:16,y:24},b)?.position,{x:53,y:4,z:1});
  });
});
Deno.test('small gameplay maps retain terrain pixels between animation frames',async()=>{
  await withViewport(async(_scene,_viewport,ctx)=>{
    const scene=await TerrainScene.prepare(flat(16,32),catalog,cache(),day,5);
    assert(scene.supportsViewport,'Small maps must use the retained viewport too');
    const viewport=new TerrainViewport(scene),camera={x:0,y:32,zoom:1};
    for(let n=0;n<1000;n++)if(!viewport.draw(ctx,camera,128,96,1).pending)break;
    let repaints=0;const paint=scene.paintOperations.bind(scene);
    scene.paintOperations=(...args)=>{repaints++;return paint(...args);};
    for(let n=0;n<60;n++)assert(!viewport.draw(ctx,camera,128,96,1).pending);
    equal(repaints,0);
  });
});

Deno.test('offscreen edits on small maps retain complete source operations for a later pan',async()=>{
 await withViewport(async(_scene,_viewport,ctx)=>{
  const before=flat(64,64),scene=await TerrainScene.prepare(before,catalog,cache(),day,5),viewport=new TerrainViewport(scene);
  const settle=(v:{x:number;y:number;zoom:number})=>{for(let n=0;n<10000;n++)if(!viewport.draw(ctx,v,128,96,1).pending)return;throw new Error('Viewport never settled');};
  const first={x:0,y:32,zoom:1};settle(first);
  const after=structuredClone(before);after.revision='2';after.terrain!.geometryRevision='2';after.terrain!.cells[60*64+40].ground=2;
  const next=await TerrainScene.prepare(after,catalog,cache(),day,5,scene),complete=next.operations;
  viewport.setScene(next);settle(first);
  assert(next.operations===complete,'A clipped local patch must not replace the complete small-map scene');
  const far={x:-1280,y:-432,zoom:1};settle(far);
  equal(viewport.pick({x:16,y:24},far)?.position,{x:41,y:70,z:2});
 });
});
Deno.test('local spatial updates retain source order, replace both moving-car entries, and remove obsolete hits',async()=>{
  await withViewport((scene)=>{
    const recipe=catalog.renderRecipes.find(r=>r.handler==='train-car')!;
    const car=(column:number,carId:string)=>{
      const base=scene.columnOperations(column)[0];
      return {...base,target:{kind:'train' as const,id:'train',carId,position:base.target.position,definition:{packId:catalog.packId,contributionId:recipe.id,recipeId:recipe.id}}};
    };
    const first=car(0,'a'),second=car(1,'b'),ground=scene.columnOperations(2)[0],original=[first,second,ground];
    const index=new TerrainOperationIndex(scene,original),a=car(1,'a'),b=car(0,'b');
    index.replace(scene,new Map([[1,[a]],[0,[b]]]));
    assert(index.cars.get('a')===a&&index.cars.get('b')===b,'Changing columns must retain both car identities');
    equal(index.operations,[b,a,ground]);equal(original,[first,second,ground]);
    equal(index.query({left:-1000,top:-1000,right:1000,bottom:1000}),[b,a,ground]);
    index.replace(scene,new Map([[0,[]]]));
    assert(!index.cars.has('b'));
    equal(index.query({left:-1000,top:-1000,right:1000,bottom:1000}),[a,ground]);
  });
});
Deno.test('geometry replacement retains the completed viewport and matching picks until the new buffer is complete',async()=>{
  await withViewport(async(_scene,viewport,ctx,canvases)=>{
    const camera={x:0,y:32,zoom:1},point={x:16,y:24};
    const settle=()=>{for(let n=0;n<1000;n++)if(!viewport.draw(ctx,camera,128,96,1).pending)return;throw new Error('Viewport never settled');};
    settle();assert(viewport.pick(point,camera)?.position.z===1);
    const raised=await TerrainScene.prepare(flat(200,100,2),catalog,cache(),day,5);
    viewport.setScene(raised);
    const pending=viewport.draw(ctx,camera,128,96,1);
    assert(pending.ready&&pending.pending,'Replacement must keep displaying the completed image');
    assert(viewport.pick(point,camera)?.position.z===1,'Pending geometry must not replace visible picks');
    // A newer revision discards unfinished work, while preserving the front buffer.
    const newer=await TerrainScene.prepare(flat(200,100,3),catalog,cache(),day,5);
    viewport.setScene(newer);
    assert(viewport.draw(ctx,camera,128,96,1).ready);
    settle();assert(viewport.pick(point,camera)?.position.z===3);
    viewport.setScene(raised);assert(viewport.draw(ctx,camera,128,96,1).ready);
    settle();assert(viewport.pick(point,camera)?.position.z===2);
    assert(canvases.filter(c=>c.width===256&&c.height===288).length===2,'Geometry revisions must reuse the two viewport buffers');
  });
});
Deno.test('cached terrain covers small pans and refreshes before the buffered edge is exposed',async()=>{
  await withViewport((scene,viewport,ctx)=>{
    const camera={x:0,y:32,zoom:1};
    for(let n=0;n<1000;n++)if(!viewport.draw(ctx,camera,128,96,1).pending)break;
    const operations=scene.operations;
    const smallPan={...camera,x:-24,y:16};
    assert(!viewport.draw(ctx,smallPan,128,96,1).pending,'The safe interior must reuse the cached image');
    assert(scene.operations===operations);
    // This point lies beyond the original viewport, inside the overscan.
    const point={x:120,y:24};
    equal(viewport.pick(point,smallPan)?.position,scene.pick(point,smallPan)?.position);
    assert(viewport.pick(point,smallPan),'Cached offscreen terrain must be available immediately');
    const edgePan={...camera,x:-40};
    assert(viewport.draw(ctx,edgePan,128,96,1).pending,'Refresh must start before exhausting the margin');
    assert(viewport.pick(point,edgePan),'The old buffer must cover the viewport during refresh');
    assert(!viewport.draw(ctx,camera,128,96,1).pending,'Returning to the safe interior cancels the edge refresh');
    assert(scene.operations===operations);
    for(const direction of [-1,1]) {
      assert(!viewport.draw(ctx,{...camera,y:camera.y+direction*40},128,96,1).pending,'Vertical pans must reuse the larger safe interior');
      assert(viewport.draw(ctx,{...camera,y:camera.y+direction*60},128,96,1).pending,'Vertical refresh must start halfway through the full-viewport margin');
      assert(!viewport.draw(ctx,camera,128,96,1).pending);
    }
  });
});
Deno.test('progressive terrain completes during continuous small pans without restarting its build',async()=>{
  await withViewport((scene,viewport,ctx)=>{
    // Force one generator step per frame so the camera moves during both build
    // and paint work, independent of the machine running the test.
    const descriptor=Object.getOwnPropertyDescriptor(performance,'now');let time=0;
    Object.defineProperty(performance,'now',{configurable:true,value:()=>time+=6});
    const original=scene.viewportOperations.bind(scene);let builds=0;
    scene.viewportOperations=function*(...args:Parameters<typeof original>){builds++;yield* original(...args);};
    try {
      let ready=false;
      for(let n=0;n<1000;n++) {
        const camera={x:-n*.01,y:32,zoom:1};
        if(viewport.draw(ctx,camera,128,96,1).ready) {
          assert(viewport.pick({x:16,y:24},camera),'Completed moving view must publish picks');
          ready=true;break;
        }
      }
      assert(ready,'Rendering must finish while the camera keeps moving within its coverage');
      equal(builds,1);
    }finally {
      if(descriptor)Object.defineProperty(performance,'now',descriptor);else Reflect.deleteProperty(performance,'now');
    }
  });
});
Deno.test('progressive viewport keeps continuous camera picking and bounds backing images on fractional-DPI and large windows',async()=>{
  await withViewport((_scene,viewport,ctx,canvases)=>{
    const v={x:0,y:32,zoom:1.15};
    for(let n=0;n<1000;n++)if(!viewport.draw(ctx,v,128,96,1.3).pending)break;
    equal(viewport.pick({x:16*1.15,y:32-8*1.15},v)?.position,{x:49,y:0,z:1});
    viewport.draw(ctx,v,5000,3000,3);
    viewport.draw(ctx,{...v,zoom:1.2},5100,3100,3);
    assert(canvases.length<100,'Camera requests must reuse viewport images');
    assert(canvases.every(c=>c.width*c.height<=16_777_216),'Backing images must stay within the pixel bound');
  });
});
Deno.test('local scene publications cannot starve initial rendering or zoom refinement and publish the latest picks',async()=>{
  await withViewport(async(scene,viewport,ctx)=>{
    const raised=flat(200,100);raised.terrain!.cells[0].ground=2;
    const edited=await TerrainScene.prepare(raised,catalog,cache(),day,5);
    // These two fixtures differ only at column zero, as a certified edit would.
    scene.changedColumns=edited.changedColumns=()=>[0];
    const descriptor=Object.getOwnPropertyDescriptor(performance,'now');let time=0;
    Object.defineProperty(performance,'now',{configurable:true,value:()=>time+=6});
    let current=scene;
    try {
      for(const zoom of [1,.5,2]) {
        const camera={x:0,y:32,zoom};let complete=false;
        const previousZoom=viewport.presentation?.view.zoom;
        for(let frame=0;frame<1000;frame++) {
          if(frame%3===0){current=current===scene?edited:scene;viewport.setScene(current);}
          const result=viewport.draw(ctx,camera,128,96,1);
          if(frame===0&&previousZoom!==undefined)equal(result.renderZoom,previousZoom);
          if(!result.pending) {
            assert(viewport.presentation?.scene===current,'Completed pixels and picks must use the latest publication');
            const point={x:16*zoom,y:32-8*zoom-(current===edited?16*zoom:0)};
            equal(viewport.pick(point,camera)?.position.z,current===edited?2:1);
            complete=true;break;
          }
        }
        assert(complete,`Local updates starved rendering at zoom ${zoom}`);
      }
    }finally {
      if(descriptor)Object.defineProperty(performance,'now',descriptor);else Reflect.deleteProperty(performance,'now');
    }
  });
});
Deno.test('zooming out replaces the buffer and publishes newly visible terrain without a pan',async()=>{
  await withViewport((scene,viewport,ctx)=>{
    const width=128,height=96,center={x:1600,y:400};
    const camera=(zoom:number)=>({x:width/2-center.x*zoom,y:height/2-center.y*zoom,zoom});
    const settle=(view:View)=>{
      for(let n=0;n<1000;n++)if(!viewport.draw(ctx,view,width,height,1.3).pending)return;
      throw new Error('Zoomed viewport never settled');
    };
    settle(camera(4));
    let operations=scene.operations;
    const point={x:8,y:height/2};
    assert(!viewport.pick(point,camera(.5)),'The initial buffer must exclude the newly exposed edge');
    // Include zooms sharing a physical raster scale as well as scale changes.
    for(const zoom of [2,1.5,1.3,1,.8,.5]) {
      const view=camera(zoom);
      assert(viewport.draw(ctx,view,width,height,1.3).pending,'A zoom change must request further frames');
      assert(scene.operations===operations,'Pending zoom work must preserve visible picks');
      let revealed=false;
      for(let n=0;n<1000;n++) {
        const result=viewport.draw(ctx,view,width,height,1.3);
        if(scene.operations!==operations) {
          assert(result.pending,'The visible reveal must publish before the full panning buffer finishes');
          assert(viewport.pick(point,view),'The reveal must already cover the newly visible edge');
          revealed=true;break;
        }
      }
      assert(revealed,'Zoom must show newly visible terrain before background buffering');
      const revealCount=scene.operations.length;
      settle(view);
      assert(scene.operations!==operations,'Zoom must publish a replacement without any pan');
      assert(scene.operations.length>revealCount,'Background work must expand the revealed area into the full buffer');
      operations=scene.operations;
      const expected=scene.pick(point,view);
      assert(expected,'Newly exposed edge must contain terrain');
      equal(viewport.pick(point,view)?.position,expected.position);
      assert(!viewport.draw(ctx,view,width,height,1.3).pending,'The replacement must settle at the unchanged camera');
    }
  });
});
Deno.test('local publications reuse terrain polygon rasters and update only changed animation spatial entries',async()=>{
  await withViewport(async(scene,viewport,ctx,canvases)=>{
    const camera={x:0,y:32,zoom:.5};
    for(let n=0;n<1000;n++)if(!viewport.draw(ctx,camera,128,96,1).pending)break;
    const compositor=new TrainCompositor(),operations=[...scene.operations];
    assert(operations.length>100);
    compositor.draw(ctx,camera,128,96,scene,operations,catalog,[],200,49);
    const bounds=scene.boundsFor.bind(scene);let examined=0;
    scene.boundsFor=op=>{examined++;return bounds(op);};
    operations[0]={...operations[0]};
    compositor.draw(ctx,camera,128,96,scene,operations,catalog,[],200,49);
    assert(examined<=2,'One changed operation must not rebuild every spatial entry');

    const read=mountain(),first=await TerrainScene.prepare(read,catalog,cache(),day,5);
    const before=canvases.length;
    first.paintOperations(ctx,view,128,96,first.operations);
    assert(canvases.length>before,'The terrain fixture must rasterize a polygon');
    const allocated=canvases.length;
    const next=await TerrainScene.prepare(read,catalog,cache(),day,5,first);
    next.paintOperations(ctx,view,128,96,next.operations);
    equal(canvases.length,allocated);
  });
});
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
Deno.test('terrace rims follow the upper block perimeter without shared edges or wall outlines',async()=>{
  for(const direction of [0,2,4,6] as const)for(const raised of [false,true]) {
    const s=flat(3,5,2),space=new WorldSpace(3,5,6),base=space.location({h:1,v:2,z:1});
    const neighbor=move(base,direction);if(raised)neighbor.z++;
    for(const [i,p] of [base,neighbor].entries()) {
      const e=structuredClone(original.entities![0]),o=structuredClone(original.terrain!.occupants[0]);
      e.id=String(i+1);e.anchor=p;o.entityId=e.id;s.entities!.push(e);s.terrain!.occupants.push(o);
      const {h,v}=space.storage(p);s.terrain!.cells[v*3+h]={ground:p.z+1,surface:'terrace'};
    }
    const scene=await TerrainScene.prepare(s,catalog,cache(),day,5);
    const rims=scene.operations.filter(op=>'points' in op&&op.edge==='#605420');
    equal(rims.length,raised?8:6);
    assert(rims.every(op=>op.target.surface==='ground'),'Only upper rims should be outlined');
    const cut=await TerrainScene.prepare(s,catalog,cache(),day,1);
    assert(!cut.operations.some(op=>'points' in op&&op.edge==='#605420'),'Hidden tops must not leave wall outlines');
  }
  const s=flat(1,1,2),e=structuredClone(original.entities![0]),o=structuredClone(original.terrain!.occupants[0]);
  e.anchor={x:0,y:0,z:1};s.entities=[e];s.terrain!.occupants=[o];s.terrain!.cells[0].surface='terrace';
  const scene=await TerrainScene.prepare(s,catalog,cache(),day,5);
  equal(scene.operations.filter(op=>'points' in op&&op.edge==='#605420').length,4);
  for(const [season,night,groundEdge,terraceEdge] of [
    ['spring',false,'#bbab73','#605420'],['spring',true,'#2e2a1c','#181508'],
    ['winter',false,'#c0c8d8','#606880'],['winter',true,'#303236','#181a20'],
  ] as const) {
    const variantScene=await TerrainScene.prepare(s,catalog,cache(),{season,night},5);
    const outlines=variantScene.operations.filter(op=>'points' in op);
    equal(outlines.map(op=>'points' in op&&op.edge),[groundEdge,...Array(4).fill(terraceEdge)]);
    equal(variantScene.pick({x:16,y:-24},view)?.surface,'ground');
  }
});
Deno.test('flat terrain rims mark upper height boundaries in every direction and variant',async()=>{
  for(const [season,night,edge] of [
    ['spring',false,'#605420'],['spring',true,'#181508'],
    ['winter',false,'#606880'],['winter',true,'#181a20'],
  ] as const)for(const [i,direction] of ([2,4,6,0] as const).entries()) {
    const s=flat(3,5,2),space=new WorldSpace(3,5,6),base=space.location({h:1,v:2,z:2});
    const neighbor=move(base,direction),{h,v}=space.storage(neighbor);
    s.terrain!.cells[v*3+h].ground=1;
    const scene=await TerrainScene.prepare(s,catalog,cache(),{season,night},5);
    const rims=scene.operations.filter(op=>'points' in op&&op.edge===edge);
    equal(rims.length,4);
    const rim=rims.find(op=>op.target.position.x===base.x&&op.target.position.y===base.y)!;
    assert('points' in rim);equal(rim.target.surface,'ground');equal(rim.target.position.z,2);
    const q=space.project(base),points=mountainPoints({ne:0,se:0,sw:0,nw:0});
    equal(rim.points,[points[i],points[(i+1)%4]].map(p=>({x:q.x+p.x,y:q.y+p.y})));
    const cut=await TerrainScene.prepare(s,catalog,cache(),{season,night},1);
    assert(!cut.operations.some(op=>'points' in op&&op.edge===edge),'Hidden tops must not leave rims');
  }
  const level=await TerrainScene.prepare(flat(3,5,2),catalog,cache(),day,5);
  assert(!level.operations.some(op=>'points' in op&&op.edge==='#605420'),'Level tile joins keep their faint grid');
});
Deno.test('natural plateau rims remain distinct where lower slopes meet the top, but not at level water',async()=>{
  for(const [direction,corners] of [[2,[0,0,4,4]],[4,[4,0,0,4]],[6,[4,4,0,0]],[0,[0,4,4,0]]] as const) {
    const s=flat(3,5,2),space=new WorldSpace(3,5,6),base=space.location({h:1,v:2,z:2});
    const neighbor={...move(base,direction),z:1},{h,v}=space.storage(neighbor);
    s.terrain!.cells[v*3+h].ground=1;
    const slope=mountain([...corners]),e=slope.entities![0],o=slope.terrain!.occupants[0];
    e.anchor=neighbor;s.entities=[e];o.position=neighbor;s.terrain!.occupants=[o];
    const scene=await TerrainScene.prepare(s,catalog,cache(),day,5);
    equal(scene.operations.filter(op=>'points' in op&&op.edge==='#605420'&&op.target.position.x===base.x&&op.target.position.y===base.y).length,1);
  }
  const s=flat(3,5,2);s.terrain!.waterLevel=2;s.terrain!.cells[7].ground=1;
  const scene=await TerrainScene.prepare(s,catalog,cache(),day,5);
  assert(!scene.operations.some(op=>'points' in op&&op.edge==='#605420'),'Water at the top height has no exposed drop');
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
Deno.test('pre-connection landscape uses valid continuous terrain, realized forests, coast, cuts and terraces without gameplay entities or a creek',async()=>{
  const {demoScene,demoCamera}=await import('../src/lib/demo-scene.ts');
  const {parseMessage}=await import('../src/lib/protocol.ts');
  const snapshot=parseMessage(JSON.stringify({...demoScene,worldEpoch:'1'})) as Snapshot;
  assert(snapshot.terrain&&snapshot.entities);
  assert(Object.isFrozen(demoScene.terrain!.occupants[0]));
  equal(snapshot.trains,[]);equal(snapshot.tracks,[]);equal(snapshot.buildings,[]);
  const t=snapshot.terrain;
  const {sha256}=await import('../src/lib/content-recipes.ts');
  equal(await sha256(new TextEncoder().encode(t.provenance.requestJson)),t.provenance.requestHash);
  assert(t.cells.some(c=>c.ground<t.waterLevel)&&t.cells.some(c=>c.ground>t.waterLevel+2));
  assert(t.cells.filter(c=>c.surface==='natural').length>t.cells.length*.9);
  assert(!t.occupants.some(o=>o.kind==='creek'));
  assert(t.occupants.filter(o=>o.forest).every(o=>snapshot.entities!.find(e=>e.id===o.entityId)!.anchor.z>t.waterLevel));
  for(const kind of ['mountain','terrace'])assert(t.occupants.some(o=>o.kind===kind));
  assert(t.occupants.some(o=>o.ne>0&&o.ne<4));assert(t.occupants.some(o=>o.forest?.trees.length));
  const before=JSON.stringify(demoScene),scene=await TerrainScene.prepare(demoScene,catalog,cache(),day,15);
  assert(scene.operations.some(op=>op.target.surface==='water'));assert(scene.operations.some(op=>op.target.surface==='forest'));
  const under=await TerrainScene.prepare(demoScene,catalog,cache(),day,4);
  assert(under.operations.some(op=>op.target.surface==='underground'));
  equal(JSON.stringify(demoScene),before);
  for(const [w,h] of [[1920,1080],[1280,900],[360,800]])assert(Number.isFinite(demoCamera(w,h).zoom)&&demoCamera(w,h).zoom>0);
});
Deno.test('decorative loop closes with reciprocal source rail ports on a clear level bed; cars follow continuously and wrap independently',async()=>{
  const {demoLoop,demoLoopPeriod,demoCar}=await import('../src/lib/demo-railway-path.ts');
  const {demoScene}=await import('../src/lib/demo-scene.ts');
  const {directionTo}=await import('../src/lib/map-geometry.ts');
  const {default:rails}=await import('../../src/FreeTrain.Online.Content/reference-spatial.json',{with:{type:'json'}});
  const space=new WorldSpace(48,96,16),t=demoScene.terrain!;
  const {demoGround}=await import('../src/lib/demo-terrain.ts');
  const {inPolygon}=await import('../src/lib/terrain-renderer.ts');
  assert(demoLoop.length>100);equal(new Set(demoLoop.map(l=>`${l.position.x},${l.position.y}`)).size,demoLoop.length);
  for(let i=0;i<demoLoop.length;i++) {
    const {position:p,pattern}=demoLoop[i],a=demoLoop[(i+demoLoop.length-1)%demoLoop.length].position,b=demoLoop[(i+1)%demoLoop.length].position;
    assert(rails.normalRailPorts[pattern].includes(directionTo(p,a))&&rails.normalRailPorts[pattern].includes(directionTo(p,b)));
    const s=space.storage(p);equal(t.cells[s.v*48+s.h].ground,4);
    equal(demoGround(p.x,p.y).ground,4);
    assert([-4,-3,-2,-1,0,1,2,3,4].some(x=>[-4,-3,-2,-1,0,1,2,3,4].some(y=>demoGround(p.x+x,p.y+y).ground<4)));
    assert(!demoScene.entities!.some(e=>e.anchor.x===p.x&&e.anchor.y===p.y&&e.anchor.z===p.z));
  }
  // Every dry part of the island lies inside the circuit or in its narrow
  // outer beach strip, including the northern peninsula.
  for(const p of space.ground())if(demoGround(p.x,p.y).ground>=4&&!inPolygon(p,demoLoop.map(l=>l.position))) {
    assert(demoLoop.some(l=>Math.max(Math.abs(l.position.x-p.x),Math.abs(l.position.y-p.y))<=3));
  }
  const headings=demoLoop.map((l,i)=>directionTo(l.position,demoLoop[(i+1)%demoLoop.length].position));
  const runs:number[]=[];
  headings.forEach((h,i)=>{if(i===0||h!==headings[i-1])runs.push(1);else runs[runs.length-1]++;});
  assert(runs.length<=20&&Math.min(...runs)>=4&&Math.max(...runs)>=12);
  const angles=new Set<number>();let bentFollowers=false;
  for(let time=0;time<demoLoopPeriod;time+=125) {
    const head=demoCar(time,0),tail=demoCar(time,2);angles.add(head.angle);bentFollowers||=head.angle!==tail.angle;
    assert(Math.hypot(head.position.x-tail.position.x,head.position.y-tail.position.y)>1);
    const next=demoCar(time+.01,0);assert(Math.hypot(next.position.x-head.position.x,next.position.y-head.position.y)<.001);
  }
  equal(angles.size,16);assert(bentFollowers);
  equal(demoCar(0,0),demoCar(demoLoopPeriod,0));equal(demoCar(0,2),demoCar(demoLoopPeriod,2));
  assert(!t.occupants.some(o=>o.kind==='creek'));equal(demoScene.tracks,[]);equal(demoScene.trains,[]);
});
Deno.test('decorative rail and composed train use ready prepared art and reject missing car recipes',async()=>{
  const {DemoRailway}=await import('../src/lib/demo-railway.ts');
  await DemoRailway.prepare(catalog,cache());
  const missing={...catalog,renderRecipes:catalog.renderRecipes.filter(r=>r.id!=='{3983B298-ADB1-4905-94E5-03B7AAE5A221}-h')};
  let rejected=false;try{await DemoRailway.prepare(missing,cache());}catch{rejected=true;}assert(rejected);
});
Deno.test('synthetic rail-bed blending has no cut exemptions and restores the northeast slope occupants',async()=>{
  const {demoScene}=await import('../src/lib/demo-scene.ts');
  const {demoGroundGrid,demoSpace}=await import('../src/lib/demo-terrain.ts');
  const {demoCorridor}=await import('../src/lib/demo-railway-path.ts');
  const {validSnapshot}=await import('../src/lib/world-identity.ts');
  const read=structuredClone(demoScene) as Snapshot,t=read.terrain!;
  const grid=demoGroundGrid(demoCorridor(1));let slopes=0;
  assert(t.cells.every(c=>c.surface==='natural'||c.surface==='terrace'));
  for(let v=29;v<=31;v++)for(let h=32;h<=33;h++) {
    const c=t.cells[v*48+h],p=demoSpace.location({h,v,z:c.ground});
    equal(c.surface,'natural');
    equal(c.ground,grid[v*48+h].ground);
    if(grid[v*48+h].corners.some(n=>n!==0)) {
      const entity=read.entities!.find(e=>e.anchor.x===p.x&&e.anchor.y===p.y&&e.anchor.z===p.z);
      const o=t.occupants.find(o=>o.entityId===entity?.id&&o.kind==='mountain');
      assert(o);equal([o.ne,o.se,o.sw,o.nw],grid[v*48+h].corners);slopes++;
    }
  }
  assert(slopes>0);
  // Remove only the intentional inland Terrace garden. Validate the underlying
  // complete natural surface so no cut/terrace exemption can conceal a seam.
  t.cells=grid.map(c=>({ground:c.ground,surface:'natural'}));
  const terraces=new Set(t.occupants.filter(o=>o.kind==='terrace').map(o=>o.entityId));
  t.occupants=t.occupants.filter(o=>!terraces.has(o.entityId));read.entities=read.entities!.filter(e=>!terraces.has(e.id));
  assert(validSnapshot(read));
  for(const c of grid)assert(c.corners.every(n=>n>=0&&n<=4));
});

Deno.test('map sprite changes retain the front buffer through preparation and progressive rendering',async()=>{
  await withViewport(async(scene,_viewport,ctx,canvases)=>{
    const prepare=TerrainScene.prepare;
    let complete!:(scene:TerrainScene)=>void,invalidated!:()=>void;
    let preparationStarted=Promise.withResolvers<void>();
    const images:unknown[]=[];
    ctx.drawImage=((image:unknown)=>{images.push(image);}) as typeof ctx.drawImage;
    const canvas={width:128,height:96,dataset:{},getContext:()=>ctx} as unknown as HTMLCanvasElement;
    const renderer=new MapRenderer(canvas,new Map(),()=>invalidated());
    const read=flat(200,100);
    // Use the real scene/raster pipeline with controlled asynchronous preparation.
    Object.assign(renderer,{catalog:Promise.resolve(catalog),catalogHash:read.requiredPacks[0].catalogHash});
    TerrainScene.prepare=()=>new Promise(resolve=>{complete=resolve;preparationStarted.resolve();});
    const camera={x:0,y:0,zoom:1};
    let options:MapOptions={heightCut:5,level:1,night:false,winter:false,recolor:false,shape:'eastWest',remove:false};
    const draw=()=>{images.length=0;return renderer.draw(camera,128,96,1,options);};
    const settle=()=>{for(let n=0;n<1000;n++){draw();if(!renderer.needsFrame)return;}throw new Error('Map never settled');};
    const prepared=async(next:TerrainScene)=>{
      const ready=new Promise<void>(resolve=>{invalidated=resolve;});
      // Catalog/color preparation can cross several promise boundaries. Observe
      // the actual scene request instead of assuming one microtask starts it.
      await preparationStarted.promise;preparationStarted=Promise.withResolvers<void>();
      complete(next);await ready;
    };
    try {
      renderer.setRead(read);assert(!draw());await prepared(scene);settle();
      let front=images[0];assert(front);
      for(const variant of [{season:'spring',night:true},{season:'winter',night:true},{season:'winter',night:false}] as Variant[]) {
        options={...options,night:variant.night,winter:variant.season==='winter'};
        assert(draw(),'Loading sprites must retain a drawable scene');
        assert(images[0]===front,'Asynchronous preparation must preserve the front buffer');
        const replacement=await prepare(flat(200,100),catalog,cache(),variant,5);
        await prepared(replacement);
        assert(draw());assert(renderer.needsFrame);
        assert(images[0]===front,'The first progressive frame must still present the old buffer');
        settle();assert(images[0]!==front,'The completed replacement must flip buffers');
        front=images[0];
      }
      assert(canvases.filter(c=>c.width===256&&c.height===288).length===2,'Sprite changes must reuse the two viewport buffers');
      renderer.setRead({...read,worldEpoch:'another-world'});
      assert(!draw(),'A different world must not retain the previous world buffer');
      assert(images.length===0);
    } finally {TerrainScene.prepare=prepare;}
  });
});

Deno.test('certified local terrain edits patch the completed raster and picks atomically, while global changes retain the full redraw',async()=>{
 const {parseMessage,applyDelta,immutable}=await import('../src/lib/protocol.ts');
 const before=immutable(parseMessage(JSON.stringify(flat(200,100))) as Snapshot);
 const position=new WorldSpace(200,100,6).location({h:3,v:2,z:1});
 const definition={packId:before.requiredPacks[0].id,contributionId:'system:terrain/mountain',recipeId:'geometry:quarter-mountain-1'};
 const delta:import('../src/generated/contracts.ts').Delta={type:'delta',worldEpoch:before.worldEpoch,baseRevision:before.revision,revision:String(BigInt(before.revision)+1n),tick:before.tick,paused:true,cash:'0',ruleset:before.ruleset,contentVersion:before.contentVersion,requiredPacks:before.requiredPacks,
  trains:[],stations:[],buildings:[],tracks:[],removed:{trainIds:[],stationIds:[],buildingIds:[],trackPositions:[],entityIds:[]},
  entities:[{id:before.terrain!.nextEntityId,revision:'0',definition,anchor:position,footprint:[{offset:{x:0,y:0,z:0},transparent:false}],owned:false,silentlyReclaimable:false,value:'0'}],
  terrain:{waterLevel:0,constructionMode:false,geometryRevision:String(BigInt(before.terrain!.geometryRevision)+1n),nextEntityId:String(BigInt(before.terrain!.nextEntityId)+1n),cells:[],occupants:[{entityId:before.terrain!.nextEntityId,kind:'mountain',ne:1,se:0,sw:0,nw:0,creekConnections:0,forest:null}]}};
 const after=applyDelta(before,delta) as Snapshot;assert(after);
 await withViewport(async(_unused,_viewport,ctx)=>{
  const scene=await TerrainScene.prepare(before,catalog,cache(),day,5),viewport=new TerrainViewport(scene),camera={x:0,y:32,zoom:1};
  for(let n=0;n<1000;n++)if(!viewport.draw(ctx,camera,128,96,1).pending)break;
  const prior=scene.operations,edited=await TerrainScene.prepare(after,catalog,cache(),day,5,scene);
  let replayed=0;const paint=edited.paintOperations.bind(edited);
  edited.paintOperations=(...args)=>{replayed+=args[4].length;return paint(...args);};
  const images:unknown[]=[];ctx.drawImage=((image:unknown)=>images.push(image)) as typeof ctx.drawImage;
  viewport.draw(ctx,camera,128,96,1);const front=images.at(-1);
  viewport.setScene(edited);const result=viewport.draw(ctx,camera,128,96,1);
  assert(result.ready&&!result.pending,'A local edit must not rebuild the overscanned viewport');
  assert(scene.operations===prior,'The previous immutable scene must keep its old picks');
  assert(images.at(-1)===front,'Local changes must not copy the entire retained image');
  assert(replayed<prior.length/2,'Local repaint must query nearby operations instead of scanning the viewport');
  equal(viewport.pick({x:112,y:36},camera)?.id,before.terrain!.nextEntityId);
  const water={...delta,baseRevision:after.revision,revision:String(BigInt(after.revision)+1n),entities:[],terrain:{...delta.terrain!,waterLevel:1,geometryRevision:String(BigInt(after.terrain!.geometryRevision)+1n),occupants:[]}};
  const changed=applyDelta(after,water) as Snapshot;assert(changed);
  viewport.setScene(await TerrainScene.prepare(changed,catalog,cache(),day,5,edited));
  assert(viewport.draw(ctx,camera,128,96,1).pending,'A global water change must use the full redraw');
 });
});
