import {addPassagePlatform} from './helpers/passage-scene.ts';
import assert from 'node:assert/strict';
import {stationScene} from './helpers/station-scene.ts';
import {passagePath,passageQuery,platformAppearance,passageGhost} from '../src/lib/passage-edit.ts';
import {parsePassagePreview} from '../src/lib/protocol.ts';
import {RailwayRenderPlan} from '../src/lib/railway-renderer.ts';
import {WorldSpace,offset} from '../src/lib/map-geometry.ts';
import type {Snapshot} from '../src/generated/contracts.ts';
import type {RenderCatalog} from '../src/lib/content-recipes.ts';
const catalog=JSON.parse(await Deno.readTextFile(new URL('../public/content/bundled-v2/catalog.json',import.meta.url))) as RenderCatalog;
Deno.test('passage preview shape requires zero cost and exact ordered cardinal geometry',()=>{
 const s=stationScene(64,24),from={x:10,y:10,z:2},to={x:12,y:10,z:2},q=passageQuery(s,from,to,'brown')!;
 const p={query:q,legal:true,reason:null,cost:'0',positions:passagePath(from,to)};assert.deepEqual(parsePassagePreview(p),p);
 for(const bad of [{...p,cost:'1'},{...p,positions:p.positions.toReversed()},{...p,legal:false},{...p,positions:[]}])assert.throws(()=>parsePassagePreview(bad));
 assert.equal(passagePath(from,from).length,0);assert.equal(passagePath(from,{...to,y:11}).length,0);assert.equal(passagePath(from,{...to,z:3}).length,0);
 assert.equal(passagePath(from,{...to,x:1000}).length,0);
});
Deno.test('all material heading bridge width combinations select independent source crops and train ordering',()=>{
 let count=0;
 for(const direction of [0,2,4,6] as const)for(const material of ['concrete','brown','green'] as const)for(const bridge of [false,true])for(const double of [false,true]){
  const s=stationScene(64,24),p=addPassagePlatform(s,direction),y=p.yards[0];y.outlook={kind:'passage',material,bridge,upward:false};
  if(double){const d=offset((direction+6)%8);addPassagePlatform(s,((direction+4)%8) as Heading,{x:10+d.x,y:10+d.y,z:2});}
  const art=platformAppearance(p,y,s.mixed!.stationGraph!.platforms),prefix=`${material}-`,index=direction/2*3+(double?(bridge?2:1):0);
  assert.equal(art[0].recipeId,`system:${prefix}passage-${index}`);
  const recipe=catalog.renderRecipes.find(r=>r.id===art[0].recipeId)!;
  assert.deepEqual(recipe.frames[0].rectangle,double?[(direction/2*2+(bridge?2:1))*32,40,32,32]:[direction/2*32,8,32,32]);
  assert.equal(art[0].stage,direction===0||direction===6?40:20);
  const plan=new RailwayRenderPlan(s,catalog,{season:'spring',night:false});
  const layers=plan.layers.filter(l=>l.entityId===y.trafficOwnerId&&l.recipeId.includes('passage'));
  assert.equal(layers.length,bridge&&(direction===2||direction===4)?2:1);
  if(layers.length===2){assert.equal(layers[1].recipeId,`system:${prefix}passage-${direction===2?13:12}`);assert.equal(layers[1].frame.stage,40);}
  count++;
 }
 assert.equal(count,48);
});
Deno.test('neighbor appearance invalidates retained columns without a traffic component change',()=>{
 const s=stationScene(64,24),p=addPassagePlatform(s,4),other=addPassagePlatform(s,0,{x:11,y:10,z:2});
 const before=new RailwayRenderPlan(s,catalog,{season:'spring',night:false});
 other.yards[0].outlook={kind:'plain',material:'concrete',bridge:false,upward:false};
 const after=new RailwayRenderPlan(s,catalog,{season:'spring',night:false});
 const space=new WorldSpace(64,24,8),cell=space.storage(p.location);
 assert(after.changedColumns(before).includes(cell.v*space.horizontalSize+cell.h));

});
Deno.test('ghost simulates endpoint material retention promotion and neighbor width without mutating reads',()=>{
 const s=stationScene(64,24),p=addPassagePlatform(s,4),from=p.location,to={x:11,y:10,z:2},q=passageQuery(s,from,to,'green')!;
 p.yards[0].outlook={kind:'plain',material:'concrete',bridge:false,upward:false};const bytes=JSON.stringify(s);
 const preview={query:q,legal:true,reason:null,cost:'0',positions:passagePath(from,to)};
 const ghost=passageGhost(s,preview);assert.equal(ghost[0].recipeId,'system:green-passage-6');assert.equal(JSON.stringify(s),bytes);
 p.yards[0].outlook={kind:'passage',material:'brown',bridge:true,upward:false};
 assert.equal(passageGhost(s,preview)[0].recipeId,'system:brown-passage-6');
 assert.equal(passageGhost(s,{...preview,query:{...q,action:'remove'}})[0].recipeId,'system:thin-4');
});
Deno.test('passage requests fence epoch geometry minute content abort reconnect and disposal, with locale independent transport',async()=>{
 const {createFixtureService}=await import('../src/lib/session-client.ts'),{createShellModel}=await import('../src/lib/shell-model.ts');
 const terrain=JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/fixtures/snapshot-terrain.json',import.meta.url))) as Snapshot;
 for(const change of ['epoch','geometry','minute','content','abort','connection','dispose','locale']){
  const service=createFixtureService();service.publish(terrain);let complete!:(p:any)=>void;
  const model=createShellModel(()=>({...service.connect(),previewPassage:()=>new Promise(r=>complete=r)}),'en',1);
  try{
   for(let i=0;i<100&&model.state.connection!=='ready';i++)await new Promise(r=>setTimeout(r,1));
   const controller=new AbortController(),q=passageQuery(terrain,{x:1,y:0,z:2},{x:2,y:0,z:2},'brown')!,result=model.previewPassage(q,controller.signal),rejected=change==='locale'?undefined:assert.rejects(result);
   if(change==='epoch')service.publish({...terrain,worldEpoch:'999'});
   if(change==='geometry')service.publish({...terrain,terrain:{...terrain.terrain!,geometryRevision:'999'}});
   if(change==='minute')service.publish({...terrain,tick:String(BigInt(terrain.tick)+1n)});
   if(change==='content')service.publish({...terrain,contentVersion:'changed'} as unknown as Snapshot);
   if(change==='abort')controller.abort();if(change==='connection')model.reconnect();if(change==='dispose')model.dispose();if(change==='locale')model.setLocale('ja');
   complete({query:q,legal:true,reason:null,cost:'0',positions:[q.from,q.to]});
   if(rejected)await rejected;else assert.equal((await result).query,q);
  }finally{model.dispose();}
 }
});
