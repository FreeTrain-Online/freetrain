import assert from 'node:assert/strict';
import {readRuleFile,writeRuleFile,encodeAppendRules,maximumRuleFileBytes} from '../src/lib/fleet-rule-file.ts';
import {validWireShape} from '../src/lib/protocol.ts';
import type {FleetRule} from '../src/generated/contracts.ts';
const path=new URL('./fixtures/timetable/',import.meta.url);
const expected=JSON.parse(await Deno.readTextFile(new URL('expected.json',path))) as FleetRule[];
const source=await Deno.readFile(new URL('single.ftdd',path));
const utf8=(s:string)=>new TextEncoder().encode(s);
Deno.test('SCN-009/MAN-A12 inert source records preserve all actions, masks, order and repeated identity as independent values',async()=>{
 for(const name of ['single','binary-array']){
  const bytes=await Deno.readFile(new URL(name+'.ftdd',path)),original=bytes.slice(),result=readRuleFile(bytes);
  assert.equal(result.format,'legacy');assert.deepEqual(result.rules,expected);assert.deepEqual(bytes,original);
  result.rules[8].action='go';assert.equal(result.rules[0].action,'pass');
 }
});
Deno.test('portable v1 export/read retains exact rule values, duplicates and order through 1024 rules',()=>{
 for(const rules of [[],expected,Array.from({length:1024},(_,i)=>({...expected[i%expected.length]}))]){
  const json=writeRuleFile(rules),result=readRuleFile(utf8(json));
  assert.equal(result.format,'portable');assert.deepEqual(result.rules,rules);
  assert.equal(writeRuleFile(result.rules),json);
 }
});
Deno.test('each truncated source prefix, trailing data, invalid headers and unknown versions fail without mutating input',()=>{
 for(let n=0;n<source.length;n++)assert.throws(()=>readRuleFile(source.slice(0,n)),String(n));
 for(const index of [0,1,5,9,13]){const broken=source.slice();broken[index]=42;const original=broken.slice();assert.throws(()=>readRuleFile(broken));assert.deepEqual(broken,original);}
 assert.throws(()=>readRuleFile(new Uint8Array([...source,0])));
 assert.throws(()=>readRuleFile(new Uint8Array(maximumRuleFileBytes+1)));
});
Deno.test('legacy metadata refuses alien class/assembly/member/primitive types, dangling refs, duplicate ids and null runs',()=>{
 function changeText(from:string,to:string){assert.equal(from.length,to.length);
  const target=utf8(from);let offset=-1;for(let i=0;i<=source.length-target.length;i++)if(target.every((b,j)=>source[i+j]===b)){offset=i;break;}
  assert.ok(offset>=0);const altered=source.slice();altered.set(utf8(to),offset);assert.throws(()=>readRuleFile(altered));
 }
 changeText('AdvStationRule','BadStationRule');changeText('RuleCollection','FakeCollection');changeText('2.0.3837.12345','9.0.3837.12345');
 changeText('CollectionBase+list','CollectionBase+evil');changeText('value__','method_');
 // First forward ref to ArrayList is 09 02 00 00 00. Root id already exists.
 const refIndex=source.findIndex((b,i)=>b===9&&source[i+1]===2&&source.slice(i+2,i+5).every(b=>b===0));assert.ok(refIndex>0);
 for(const id of [99,1,0]){const altered=source.slice();altered[refIndex+1]=id;assert.throws(()=>readRuleFile(altered));}
 const nullRun=source.findIndex((b,i)=>b===13&&source[i+1]===7);assert.ok(nullRun>0);
 for(const n of [0,8,255]){const altered=source.slice();altered[nullRun+1]=n;assert.throws(()=>readRuleFile(altered));}
});
Deno.test('portable reader rejects wrong schema/type/version, invalid masks/actions and excessive rules',()=>{
 const value=JSON.parse(writeRuleFile(expected));
 for(const v of [null,[],{}, {...value,version:2},{...value,format:'ftdd'},{...value,extra:0},{...value,rules:null},
  {...value,rules:Array(1025).fill(expected[0])},...['month','day','dayOfWeek','hour','minutes','action'].map(f=>({...value,rules:[{...expected[0],[f]:f==='action'?'straight':99}]})),
  {...value,rules:[{...expected[0],callback:'run'}]},{...value,rules:[null]}])assert.throws(()=>readRuleFile(utf8(JSON.stringify(v))));
 assert.throws(()=>readRuleFile(new Uint8Array([123,255,125])));
});
Deno.test('atomic append uses six-byte source action mapping and stays within existing 16 KiB at maximum capacity',()=>{
 const rules=Array.from({length:1024},(_,i)=>expected[i%expected.length]);
 const encoded=encodeAppendRules(rules);assert.equal(encoded.length,8192);
 assert.deepEqual([...atob(encodeAppendRules(expected.slice(0,1)))].map(c=>c.charCodeAt(0)),[4,23,5,8,0,0]);
 assert.equal(atob(encodeAppendRules([expected[5]])).charCodeAt(5),5);
 const payload={kind:'editFleetRule',controllerId:'9007199254740997',expectedRevision:'123',location:{x:22,y:20,z:2},settingKind:'harbor',operation:'append',index:-1,offset:0,rule:null,expectedGeometryRevision:null,rulesBase64:encoded};
 const command={type:'command',requestId:'append',expectedWorldEpoch:'1',payload};
 assert.equal(validWireShape(command),true);assert.ok(utf8(JSON.stringify(command)).length<16384);
 for(const rulesBase64 of ['',encoded+'AAAAAAAA','%%%%%%==','AA AA AA'])assert.equal(validWireShape({...command,payload:{...payload,rulesBase64}}),false);
});
