import assert from 'node:assert/strict';
import {applyDelta,immutable,parseMessageValue,parseCityPreview} from '../src/lib/protocol.ts';
import {cityQuery} from '../src/lib/city-edit.ts';
import type {Snapshot,Delta} from '../src/generated/contracts.ts';
const load=async(name:string)=>JSON.parse(await Deno.readTextFile(new URL(`./fixtures/city-population.${name}.json`,import.meta.url)));
Deno.test('real completed city heat refresh reconstructs the exact C# successor without changing population provenance or catchment',async()=>{
 const before=immutable(parseMessageValue(await load('before')) as Snapshot),d=parseMessageValue(await load('delta')) as Delta;
 const expected=parseMessageValue(await load('snapshot')) as Snapshot,next=applyDelta(before,d);assert(next);assert.deepEqual(JSON.parse(JSON.stringify(next)),expected);
 const old=before.mixed!.stationGraph!.listeners[0],current=next.mixed!.stationGraph!.listeners[0];
 assert.equal(old.provider!.input,'city-variable');assert.equal(old.provider!.landValue,0);assert.equal(current.provider!.landValue,30);
 assert.deepEqual(old.provider!.population,current.provider!.population);assert.equal(current.provider!.population.factor,5);
 assert.deepEqual(current.stations,old.stations);assert.deepEqual(next.mixed!.stationGraph!.listenerMemberships,before.mixed!.stationGraph!.listenerMemberships);
 assert.deepEqual(next.mixed!.city!.producers,before.mixed!.city!.producers);
 assert.equal(BigInt(next.mixed!.city!.structures[0].revision),BigInt(before.mixed!.city!.structures[0].revision)+1n);
});
Deno.test('city provider metadata requires its matching owner revision and preserves geometry and initial-height contracts',async()=>{
 const before=immutable(parseMessageValue(await load('before')) as Snapshot),delta=await load('delta') as Delta;
 const bad=structuredClone(delta);bad.entities=[];bad.mixed!.city!.structures[0].revision=before.mixed!.city!.structures[0].revision;
 assert.equal(applyDelta(before,bad),undefined);
 for(const change of [
  (d:Delta)=>d.mixed!.stationGraph!.listeners[0].provider!.zSize++,
  (d:Delta)=>d.mixed!.stationGraph!.listeners[0].provider!.input='controlled',
  (d:Delta)=>d.mixed!.stationGraph!.listeners[0].provider!.population.factor++,
 ]){const d=structuredClone(delta);change(d);assert.equal(applyDelta(before,d),undefined);}
});
Deno.test('population inspector accepts distinct residents outgoing incoming and land while rejecting malformed responses',async()=>{
 const read=parseMessageValue(await load('snapshot')) as Snapshot,structure=read.mixed!.city!.structures[0];
 const preview={query:cityQuery(read,structure.id)!,canCancel:false,reason:'operationUnavailable',cost:'0',structure,
  population:{residents:600,outgoing:480,incoming:60,landValue:30}};
 assert.deepEqual(parseCityPreview(preview).population,preview.population);
 for(const change of [
  (p:typeof preview)=>p.population.incoming=-1,
  (p:typeof preview)=>p.population.outgoing=1.5,
  (p:typeof preview)=>p.query.expectedOwnerRevision='999',
 ]){const p=structuredClone(preview);change(p);assert.throws(()=>parseCityPreview(p));}
});
