import type { CommandPayload } from '../../src/generated/contracts.ts';
import { compatibilityURL, authenticate, collectSession, pausedCanonicalScene } from './transport-helpers.ts';
import {readFileSync} from 'node:fs';
import {test,expect,type Page} from './fixtures.ts';
import {canonicalState as state,ready} from './browser-helpers.ts';
const profile=JSON.parse(readFileSync('src/generated/station-profile.json','utf8'));
const previewPath=compatibilityURL('/api/station/building/preview',['terrain-content-1','mixed-railway-content-1']);
const save=async(p:Page)=>(await p.request.get('/api/saves/download')).body();

// Real publication/decoder witness. Session/construction tests own domain matrices.
test('P6-06 construction and removal publish station owners through real EN/JA peer decoders',async({page,browser,baseURL})=>{
 test.setTimeout(120000);
 const errors:string[]=[];
 const context=await browser.newContext({baseURL,locale:'ja-JP',viewport:{width:430,height:860}}),peer=await context.newPage();
 const sessions=await Promise.all([page,peer].map(async p=>{p.on('pageerror',e=>errors.push(String(e)));return collectSession(p);}));
 let serial=0;
 async function command(payload:CommandPayload){
  const s=await state(page);
  return sessions[0].command({type:'command',requestId:'station-publication-'+(++serial),expectedWorldEpoch:s.worldEpoch,payload});
 }
 const preview=async(p:Page,q:object)=>p.request.post(previewPath,{data:q});
 const latest=(i:number)=>sessions[i].latest();
 try{
  await page.goto('/');await ready(page);
  await test.step('Prepare a paused canonical scene',async()=>{
   await pausedCanonicalScene(page,await state(page),{name:'Constructed stations',dimensions:{h:48,v:40,z:8},waterLevel:2});
  });
  await authenticate(peer,'observer-test');await peer.goto('/');await ready(peer,'ja');
  const type=profile.definitions.find((d:any)=>d.height>1&&d.sizeX*d.sizeY>1);
  expect(type).toBeTruthy();
  const current=await state(page),q={expectedWorldEpoch:current.worldEpoch,expectedGeometryRevision:current.terrain.geometryRevision,expectedMinute:current.tick,
   expectedContentVersion:current.contentVersion,action:'place' as const,position:{x:25,y:25,z:2},definition:type.reference,stationId:'0',expectedStationRevision:'0'};
  const before=await save(page),response=await preview(page,q);expect(response.ok(),await response.text()).toBe(true);const quote=await response.json();
  expect(quote.legal,quote.reason).toBe(true);expect(quote.positions).toHaveLength(type.sizeX*type.sizeY*type.height);expect(await save(page)).toEqual(before);
  expect((await command({kind:'editStationBuilding',query:q,expectedCost:quote.cost})).type).toBe('accepted');
  const built=await state(page),graph=built.mixed.stationGraph,b=graph.buildings[0];
  expect(b.definition).toEqual(type.reference);expect(b.location).toEqual(q.position);expect(graph.nextStationName).toBe(2);
  expect(built.mixed.voxels).toHaveLength(0);expect(built.entities.find((e:any)=>e.id===b.id).footprint).toHaveLength(quote.positions.length);
  await expect.poll(()=>latest(1)?.mixed?.stationGraph).toEqual(graph);
  for(const p of [page,peer])await expect(p.locator('canvas.world-canvas')).toHaveAttribute('data-render-revision',built.revision);
  const remove={...q,expectedWorldEpoch:built.worldEpoch,expectedGeometryRevision:built.mixed.geometryRevision,
   expectedContentVersion:built.contentVersion,expectedMinute:built.tick,action:'remove' as const,definition:null,position:quote.positions.at(-1),stationId:b.id,expectedStationRevision:b.revision};
  const demolition=await(await preview(page,remove)).json();expect(demolition.legal,demolition.reason).toBe(true);
  expect((await command({kind:'editStationBuilding',query:remove,expectedCost:demolition.cost})).type).toBe('accepted');
  const removed=await state(page);expect(removed.mixed.stationGraph.buildings).toHaveLength(0);
  await expect.poll(()=>latest(1)?.mixed?.stationGraph?.buildings.length).toBe(0);
  for(const p of [page,peer])await expect(p.locator('canvas.world-canvas')).toHaveAttribute('data-render-revision',removed.revision);
  expect(errors).toEqual([]);
 }finally{await context.close();}
});
