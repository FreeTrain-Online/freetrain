// Inert, allowlisted MS-NRBF record reader. Never resolves CLR types or executes
// constructors/callbacks. ExAV 883cf7d: AdvancedStationHandler, TimeMask.
import type {FleetRule} from '../generated/contracts.ts';
import {stationActions} from './fleet-controllers.ts';
export const maximumRuleFileBytes=512*1024;
const prefix='freetrain.world.rail.tattc.';
const collection=prefix+'AdvancedStationHandler+RuleCollection',rule=prefix+'AdvStationRule',action=prefix+'StationAction',list='System.Collections.ArrayList';
type Ref={ref:number};
type Value=Ref|null;
type Meta={name:string;names:string[];types:number[];extras:(number|string|null)[]};
type Node={name:string;members:Map<string,number|Value>}|{items:Value[]};
export function readLegacyRuleFile(bytes:Uint8Array):FleetRule[]{
 let pos=0,records=0,stringBytes=0,arrayElements=0;
 const nodes=new Map<number,Node>(),metadata=new Map<number,Meta>(),libraries=new Set<number>(),references=new Set<number>();
 const fail=():never=>{throw new Error('Unsupported or malformed timetable file');};
 const take=(n:number)=>{if(n<0||n>bytes.length-pos)fail();const b=bytes.subarray(pos,pos+n);pos+=n;return b;};
 const byte=()=>take(1)[0];
 const int=()=>{const b=take(4);return new DataView(b.buffer,b.byteOffset,4).getInt32(0,true);};
 const length=(max:number)=>{const n=int();if(n<0||n>max)fail();return n;};
 const string=()=>{
  let n=0;
  for(let i=0;i<5;i++){const b=byte();if(i===4&&b>7)fail();n+=(b&127)*2**(7*i);if(!(b&128)){
   stringBytes+=n;if(n>256||stringBytes>32768)fail();return new TextDecoder('utf-8',{fatal:true}).decode(take(n));
  }}return fail();
 };
 const lib=(id:number)=>{if(!libraries.has(id))fail();};
 const primitive=(t:number)=>{if(t===8)return int();if(t===10){const b=byte();return b>127?b-256:b;}return fail();};
 const add=(id:number,node:Node):Ref=>{if(id===0||nodes.has(id)||nodes.size>=4096)fail();nodes.set(id,node);return {ref:id};};
 const checkMeta=(m:Meta,system:boolean)=>{
  let names:string[],types:number[],extras:(number|string|null)[];
  if(m.name===collection&&!system){names=['CollectionBase+list'];types=[3];extras=[list];}
  else if(m.name===list&&system){names=['_items','_size','_version'];types=[5,0,0];extras=[null,8,8];}
  else if(m.name===rule&&!system){names=['action','month','day','dayOfWeek','hour','minutes'];types=[4,0,0,0,0,0];extras=[action,10,10,10,10,10];}
  else if(m.name===action&&!system){names=['value__'];types=[0];extras=[8];}
  else return fail();
  if(JSON.stringify([m.names,m.types,m.extras])!==JSON.stringify([names,types,extras]))fail();
 };
 function value(depth=0,inArray=false):Value|{nulls:number}{
  if(depth>16)fail();let kind=byte();if(++records>8192)fail();
  while(kind===12){
   const id=int(),name=string();if(id<=0||libraries.has(id)||libraries.size>=2||!/^FreeTrain\.TATTrainController, Version=2\.0\.\d{1,5}\.\d{1,5}, Culture=neutral, PublicKeyToken=null$/.test(name))fail();
   libraries.add(id);kind=byte();if(++records>8192)fail();
  }
  if(depth===0&&kind!==1&&kind!==4&&kind!==5&&kind!==7&&kind!==16)fail();
  if(kind===10)return null;
  if(kind===9){const id=int();references.add(id);return {ref:id};}
  if(kind===13||kind===14){if(!inArray)fail();return {nulls:kind===13?byte():int()};}
  if(kind===1||kind===4||kind===5){
   const id=int();let m:Meta;
   if(kind===1){const previous=metadata.get(int());if(!previous)return fail();m=previous;}
   else {
    const name=string(),count=length(6),names=Array.from({length:count},string),types=Array.from({length:count},byte);
    const extras=types.map(t=>{if(t===0)return byte();if(t===3)return string();if(t===4){const n=string();lib(int());return n;}if(t===5)return null;return fail();});
    if(kind===5)lib(int());m={name,names,types,extras};checkMeta(m,kind===4);metadata.set(id,m);
   }
   const members=new Map<string,number|Value>(),ref=add(id,{name:m.name,members});
   m.names.forEach((n,i)=>{const v=m.types[i]===0?primitive(m.extras[i] as number):value(depth+1);if(v&&typeof v==='object'&&'nulls' in v)fail();members.set(n,v as number|Value);});return ref;
  }
  if(kind===16||kind===7){
   const id=int();let count:number;
   if(kind===7){if(byte()!==0||int()!==1)fail();count=length(2048);if(byte()!==2)fail();}
   else count=length(2048);
   arrayElements+=count;if(arrayElements>2048)fail();const items:Value[]=[],ref=add(id,{items});
   while(items.length<count){const v=value(depth+1,true);if(v&&'nulls' in v){if(v.nulls<1||v.nulls>count-items.length)fail();for(let i=0;i<v.nulls;i++)items.push(null);}else items.push(v);}
   return ref;
  }
  return fail();
 }
 if(bytes.length>maximumRuleFileBytes||byte()!==0)fail();
 const rootId=int();if(rootId<=0||int()!==-1||int()!==1||int()!==0)fail();
 while(pos<bytes.length&&bytes[pos]!==11)value();
 if(byte()!==11||pos!==bytes.length||!nodes.has(rootId)||[...references].some(id=>!nodes.has(id)))fail();
 const used=new Set<number>();
 function resolve(v:unknown):Node{
  if(!v||typeof v!=='object'||!('ref' in v)||typeof v.ref!=='number')return fail();
  const n=nodes.get(v.ref);if(!n)return fail();used.add(v.ref);return n;
 }
 function member(v:unknown,type:string,name:string):number|Value{
  const n=resolve(v);if(!('name' in n)||n.name!==type||!n.members.has(name))return fail();return n.members.get(name)!;
 }
 const listRef=member({ref:rootId},collection,'CollectionBase+list');
 // A never-used CollectionBase may retain a null internal list.
 if(listRef===null){if(nodes.size!==1)fail();return [];}
 const size=member(listRef,list,'_size'),version=member(listRef,list,'_version');
 const array=resolve(member(listRef,list,'_items'));
 if(typeof size!=='number'||size<0||size>1024||typeof version!=='number'||version<0||!('items' in array)||size>array.items.length||array.items.slice(size).some(v=>v!==null))return fail();
 const rules=array.items.slice(0,size).map(ref=>{
  const ordinal=member(member(ref,rule,'action'),action,'value__');
  if(typeof ordinal!=='number'||ordinal<0||ordinal>=stationActions.length)return fail();
  const result={action:stationActions[ordinal]} as FleetRule;
  for(const field of ['month','day','dayOfWeek','hour','minutes'] as const){const v=member(ref,rule,field);if(typeof v!=='number')return fail();result[field]=v as never;}
  return result;
 });
 // Reject unreferenced objects and alien graphs, including cycles. Each repeated
 // legacy rule reference becomes an independent immutable value on append.
 if(used.size!==nodes.size)fail();return rules;
}
