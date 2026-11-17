import { requireMixedState } from './session.ts';
import type { Command } from '../../src/generated/contracts.ts';
import { restartWorldServer } from './session.ts';
import { assertExact } from './assertions.ts';
import type { ObservedSession } from './session.ts';
import {browserFixture} from './generated-fixtures.ts';
import {readFileSync} from 'node:fs';
import {test,expect,type Page} from './fixtures.ts';
import {ready} from './browser-helpers.ts';

const boot='/api/bootstrap?protocolVersion=11&contentVersion=terrain-content-1&contentVersion=mixed-railway-content-1';
const snapshot=async(p:Page)=>(await(await p.request.get(boot)).json()).snapshot;

test('P6-19 constructed yard Simple wait and packed departure publish atomically to EN/JA peers through checkpoint restart',async({page,browser,baseURL,worldServer,sessions})=>{
 const output=browserFixture('yard');
 test.setTimeout(120000);
 const seed=JSON.parse(readFileSync(output!,'utf8'));
 const arrived=JSON.parse(readFileSync(output!+'.arrived.json','utf8'));
 const departed=JSON.parse(readFileSync(output!+'.departed.json','utf8'));
 const context=await browser.newContext({baseURL,locale:'ja-JP',viewport:{width:430,height:860},isMobile:true,hasTouch:true});
 const peer=await context.newPage(),observed: ObservedSession[]=[],errors:string[]=[];
 for(const [i,p] of [page,peer].entries()){
  p.on('pageerror',e=>errors.push(String(e)));
  observed[i]=await sessions.observe(p,i===0?'owner':'peer');
 }
 const latest=(i:number)=>observed[i].latest();
 let serial=0;
 async function step(){return test.step("Advance one simulation minute",async()=>{
  const before=await snapshot(page),requestId='yard-step-'+(++serial);
  const command:Command={type:'command',requestId,expectedWorldEpoch:before.worldEpoch,payload:{kind:'step'}};
  const response=await observed[0].command(command);
  if(response.type==='accepted')await sessions.checkpoint(observed[0],'accepted command applied');
  const tick=String(BigInt(before.tick)+1n);
  for(const i of [0,1])await expect.poll(()=>latest(i)?.tick).toBe(tick);
  return command;
 });}
 try{
  await page.goto('/');await ready(page);
  expect((await peer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);
  await peer.goto('/');await ready(peer,'ja');
  const epoch=(await snapshot(page)).worldEpoch;
  const upload=await page.request.post('/api/saves/upload?expectedWorldEpoch='+epoch,{data:Buffer.from(JSON.stringify(seed))});
  expect(upload.ok(),await upload.text()).toBe(true);
  await sessions.assertCheckpoint(observed[0],state=>state?.mixed,seed.mixed,'station-yard.spec: independent fixture checkpoint');
  for(const p of [page,peer])await p.locator('canvas').evaluate(el=>el.setAttribute('data-instance','yard-service'));
  await ready(page);await ready(peer,'ja');
  while(BigInt((await snapshot(page)).tick)<BigInt(arrived.clock.absoluteMinute))await step();
  for(const i of [0,1])await assertExact(requireMixedState(latest(i)).mixed,arrived.mixed,"requireMixedState(latest(i)).mixed");
  for(const i of [0,1])expect(observed[i].bells).toEqual([]);
  const waiting=await(await page.request.get('/api/saves/download')).json();
  expect(waiting.mixed.fleet.trains[0].stopCallCount).toBe(1);
  expect(waiting.mixed.stationGraph.buildings[0].trains.today).toBe(1);
  await assertExact(waiting.mixed.accounts,seed.mixed.accounts,"waiting.mixed.accounts"); // positive wait never charges movement
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+(await snapshot(page)).worldEpoch,{data:Buffer.from(JSON.stringify(waiting))})).ok()).toBe(true);
  await sessions.assertCheckpoint(observed[0],state=>state?.mixed,arrived.mixed,'station-yard.spec: independent fixture checkpoint');
  await restartWorldServer(worldServer);
  for(const [i,p] of [page,peer].entries())expect((await p.request.post('/api/access',{data:{secret:i===0?'':'observer-test'}})).ok()).toBe(true);
  await page.reload();await peer.reload();await ready(page);await ready(peer,'ja');
  for(const p of [page,peer])await assertExact((await snapshot(p)).mixed,arrived.mixed,"(await snapshot(p)).mixed");
  for(const p of [page,peer])await p.locator('canvas').evaluate(el=>el.setAttribute('data-instance','yard-service'));
  let command:any;
  while(BigInt((await snapshot(page)).tick)<BigInt(departed.clock.absoluteMinute))command=await step();
  for(const i of [0,1])await assertExact(requireMixedState(latest(i)).mixed,departed.mixed,"requireMixedState(latest(i)).mixed");
  for(const i of [0,1]){const bell=observed[i].bells;expect(bell).toHaveLength(1);await assertExact(bell[0].bell,departed.mixed.stationGraph.platforms[0].bell,"bell[0].bell");}
  const train=departed.mixed.fleet.trains[0],station=departed.mixed.stationGraph.buildings[0];
  expect(train.passengers).toBeGreaterThan(0);expect(train.stopCallCount).toBe(0);expect(train.moveCount).toBe(1);
  expect(station.exports.today).toBe(train.passengers);expect(station.trains.today).toBe(2);
  const saved=await(await page.request.get('/api/saves/download')).json();
  const response=await observed[0].command(command);
  if(response.type==='accepted')await sessions.checkpoint(observed[0],'accepted command applied');
  await assertExact(await(await page.request.get('/api/saves/download')).json(),saved,"await(await page.request.get('/api/saves/download')).json()");
  for(const i of [0,1])expect(observed[i].bells).toHaveLength(1);
  for(const p of [page,peer])await expect(p.locator('canvas')).toHaveAttribute('data-instance','yard-service');
  expect(errors).toEqual([]);
 }finally{await context.close();}
});
