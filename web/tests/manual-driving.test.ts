import assert from 'node:assert/strict';
import {drivingTrain,validFleetControl} from '../src/lib/manual-driving.ts';
import {applyDelta,immutable,parseMessageValue} from '../src/lib/protocol.ts';
import {createShellModel} from '../src/lib/shell-model.ts';
import {createFixtureService,type SessionClient} from '../src/lib/session-client.ts';
import type {Snapshot,Delta,Command} from '../src/generated/contracts.ts';
const fixture=()=>Deno.readTextFile(new URL('../../prototype/protocol/fixtures/snapshot-fleet.json',import.meta.url)).then(s=>JSON.parse(s) as Snapshot);
const lease={participantId:'owner',leaseId:'generation-1',expiresAt:'9999999999999'};
async function until(predicate:()=>boolean){for(let i=0;i<100;i++){if(predicate())return;await new Promise(r=>setTimeout(r,2));}throw new Error('Timed out');}

Deno.test('canonical driving resolves shared nested delegation and per-train lease ownership',async()=>{
 const read=await fixture(),fleet=read.mixed!.fleet!;
 fleet.groups.find(g=>g.id==='16')!.controllerId='14';
 read.fleetControl=[{trainId:'17',lease}];
 assert.equal(drivingTrain(read,'17')!.controller,'manual');assert.equal(drivingTrain(read,'20')!.controller,'timetable');
 assert.equal(drivingTrain(read,'17')!.controlLease,lease);assert.equal(drivingTrain(read,'20')!.controlLease,undefined);
 assert.equal(drivingTrain(read,'missing'),undefined);
 fleet.trains[0].controllerId=null;assert.equal(drivingTrain(read,'17')!.controller,'automatic');
});

Deno.test('lease-only deltas retain immutable fleet geometry and remove released ownership',async()=>{
 const before=immutable(parseMessageValue(await fixture()) as Snapshot);
 const delta:Delta={type:'delta',worldEpoch:before.worldEpoch,baseRevision:before.revision,revision:String(BigInt(before.revision)+1n),tick:before.tick,paused:before.paused,cash:before.cash,
  ruleset:before.ruleset,contentVersion:before.contentVersion,requiredPacks:before.requiredPacks,trains:[],stations:[],buildings:[],tracks:[],entities:[],
  removed:{trainIds:[],stationIds:[],buildingIds:[],trackPositions:[],entityIds:[]},mixed:before.mixed,
  terrain:{...before.terrain!,cells:[],occupants:[]},fleetControl:[{trainId:'17',lease}]};
 const next=applyDelta(before,delta)!;assert.ok(next);assert.equal(next.mixed,before.mixed);assert.equal(next.terrain!.cells,before.terrain!.cells);
 assert.equal(next.fleetControl![0].lease.leaseId,'generation-1');assert.ok(Object.isFrozen(next.fleetControl));
 const released=applyDelta(next,{...delta,baseRevision:next.revision,revision:String(BigInt(next.revision)+1n),fleetControl:[]})!;
 assert.deepEqual(released.fleetControl,[]);assert.equal(before.fleetControl,undefined);
 assert.equal(applyDelta(before,{...delta,fleetControl:[{trainId:'999',lease}]}),undefined);
 assert.equal(applyDelta(before,{...delta,fleetControl:[{trainId:'17',lease},{trainId:'17',lease}]}),undefined);
 const malformed=await fixture();assert.throws(()=>parseMessageValue({...malformed,fleetControl:[{trainId:'999',lease}]}));
 assert.equal(validFleetControl({...before,fleetControl:[{trainId:'17',lease}]}),true);
});

Deno.test('manual intent captures one canonical lease synchronously and never reacquires from held keys',async()=>{
 const read=await fixture();read.mixed!.fleet!.trains[0].controllerId='14';read.fleetControl=[{trainId:'17',lease}];
 const service=createFixtureService({normal:1}),sent:Command[]=[];
 const model=createShellModel(():SessionClient=>{const c=service.connect();return {...c,participantId:'owner',role:'owner',send:async command=>{
  sent.push(structuredClone(command));return {type:'accepted',requestId:command.requestId,worldEpoch:read.worldEpoch,revision:read.revision,sequence:'1',tick:read.tick};
 }};},'en',1);
 try{
  await until(()=>model.state.connection==='ready');service.publish(read);
  await model.input(new Set(['Shift','ArrowDown']),'17');assert.equal(sent.length,1);
  assert.equal(sent[0].payload.kind,'controllerInput');
  if(sent[0].payload.kind!=='controllerInput')throw new Error('Unexpected input');
  assert.equal(sent[0].payload.leaseId,'generation-1');assert.equal(sent[0].payload.pass,true);assert.equal(sent[0].payload.reverse,true);
  const next=structuredClone(read);next.fleetControl=[];service.publish(next);
  await model.input(new Set(['ArrowUp']),'17');assert.equal(sent.length,1);
  next.fleetControl=[{trainId:'17',lease:{...lease,participantId:'peer'}}];service.publish(next);
  await model.input(new Set(['ArrowDown']),'17');assert.equal(sent.length,1);
  model.reconnect();await model.input(new Set(['ArrowDown']),'17');assert.equal(sent.length,1);
 }finally{model.dispose();}
});
