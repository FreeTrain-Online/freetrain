import { restartWorldServer } from './session.ts';
import { assertExact } from './assertions.ts';
import type { ObservedSession } from './session.ts';
import {readFileSync} from 'node:fs';
import {test,expect,type Page} from './fixtures.ts';
import {ready} from './browser-helpers.ts';
const compatibility='protocolVersion=11&contentVersion=terrain-content-1&contentVersion=mixed-railway-content-1';
const snapshot=async(p:Page)=>(await(await p.request.get('/api/bootstrap?'+compatibility)).json()).snapshot;
test('P7-06 EN/JA peers receive real heat diffusion once and reopen/restart the exact buffers and timer',async({page,browser,baseURL,worldServer,sessions})=>{
 test.setTimeout(120000);
 const seed=JSON.parse(readFileSync('tests/fixtures/city-heat-v11.json','utf8'));
 const oracle=JSON.parse(readFileSync('tests/fixtures/city-heat-v11.json.snapshot.json','utf8'));
 const context=await browser.newContext({baseURL,locale:'ja-JP',viewport:{width:430,height:860},isMobile:true,hasTouch:true}),peer=await context.newPage();
 const observed: ObservedSession[]=[],errors:string[]=[];
 for(const [i,p] of [page,peer].entries()){
  p.on('pageerror',e=>errors.push(String(e)));
  observed[i]=await sessions.observe(p,i===0?'owner':'peer');
 }
 const latest=(i:number)=>observed[i].latest();
 async function step(index:number,id:string){return test.step("Advance one simulation minute",async()=>{const s=await snapshot(page);const response=await observed[index].command({type:'command',requestId:id,expectedWorldEpoch:s.worldEpoch,payload:{kind:'step'}});
  if(response.type==='accepted')await sessions.checkpoint(observed[0],'accepted command applied');return response;});}
 try{
  await page.goto('/');await ready(page);let s=await snapshot(page);
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:Buffer.from(JSON.stringify(seed))})).ok()).toBe(true);
  expect((await peer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);
  await peer.goto('/');await ready(peer,'ja');
  await sessions.assertCheckpoint(observed[0],state=>state?.mixed?.city,seed.mixed.city,'city-heat.spec: independent fixture checkpoint');
  expect((await step(1,'heat-observer')).code).toBe('notAuthorized');
  const receipt=await step(0,'heat-step');expect(receipt.type).toBe('accepted');
  await sessions.assertCheckpoint(observed[0],state=>state?.mixed?.city,oracle.mixed.city,'city-heat.spec: independent fixture checkpoint');
  await assertExact((await snapshot(page)).mixed,oracle.mixed,"(await snapshot(page)).mixed");
  expect(await step(0,'heat-step')).toEqual(receipt);expect((await snapshot(page)).tick).toBe(oracle.tick);
  const saved=await(await page.request.get('/api/saves/download')).body();s=await snapshot(page);
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:saved})).ok()).toBe(true);
  for(const i of [0,1])await expect.poll(()=>latest(i)?.worldEpoch).toBe(String(BigInt(s.worldEpoch)+1n));
  await assertExact((await snapshot(page)).mixed,oracle.mixed,"(await snapshot(page)).mixed");
  await restartWorldServer(worldServer);
  for(const p of [page,peer])expect((await p.request.post('/api/access',{data:{secret:p===page?'':'observer-test'}})).ok()).toBe(true);
  await page.reload();await peer.reload();await ready(page);await ready(peer,'ja');
  for(const p of [page,peer])await assertExact((await snapshot(p)).mixed,oracle.mixed,"(await snapshot(p)).mixed");
  expect((await step(0,'heat-next-minute')).type).toBe('accepted');
  await assertExact((await snapshot(page)).mixed.city,oracle.mixed.city,"(await snapshot(page)).mixed.city");
  expect(errors).toEqual([]);
 }finally{await context.close();}
});
