import profile from '../generated/fleet-profile.json' with {type:'json'};
import railPorts from '../../../src/FreeTrain.Online.Content/reference-spatial.json' with {type:'json'};
import railwayProfile from '../generated/railway-profile.json' with {type:'json'};
import type {Snapshot,FleetPose,Fleet,ContentBinding} from '../generated/contracts.ts';
import type {ReadState} from './session-client.ts';
import {WorldSpace,int32} from './map-geometry.ts';
import {maximumGameMinute} from './game-time.ts';
type ReadFleet=NonNullable<NonNullable<ReadState['mixed']>['fleet']>;
const key=(p:{x:number;y:number;z:number})=>`${p.x},${p.y},${p.z}`;
const equal=(a:unknown,b:unknown)=>JSON.stringify(a)===JSON.stringify(b);
const binding=(a:ContentBinding,b:ContentBinding)=>a.packId===b.packId&&a.version===b.version&&a.catalogHash===b.catalogHash&&a.contributionId===b.contributionId;
const name=(s:string)=>s.trim().length>0&&!/[\u0000-\u001f\u007f-\u009f]/u.test(s);
const junctionPorts=[[1,5,4],[1,5,6],[2,6,5],[2,6,7],[3,7,6],[3,7,0],[4,0,7],[4,0,1],[5,1,0],[5,1,2],[6,2,1],[6,2,3],[7,3,2],[7,3,4],[0,4,3],[0,4,5]];
export function fleetRailPorts(pattern:number):number[]{return pattern<12?railPorts.normalRailPorts[pattern]:pattern<28?junctionPorts[pattern-12]:[Math.floor((pattern-28)%16/4)*2,(Math.floor((pattern-28)%16/4)*2+4)%8];}
function pose(s:FleetPose):boolean {
 return s.kind==='unplaced'?s.location===null&&s.heading===null&&s.countdown===null:
  s.location!==null&&s.heading!==null&&(s.kind==='inside'?s.countdown===null:s.countdown!==null);
}
export function validFleet(read:Snapshot):boolean {
 const m=read.mixed!,f=m.fleet,q=m.queue;if(!f)return q===undefined;
 if(!q||f.trains.reduce((n,t)=>n+t.cars.length,0)>1024)return false;
 const spatial=new Set(read.entities!.map(e=>e.id)),ids=new Set<string>();
 for(const row of [...f.trains,...f.trains.flatMap(t=>t.cars),...f.groups,...f.controllers]){
  if(ids.has(row.id)||spatial.has(row.id)||BigInt(row.id)>=BigInt(m.nextEntityId))return false;ids.add(row.id);
 }
 const groups=new Map(f.groups.map(g=>[g.id,g])),controllers=new Map(f.controllers.map(c=>[c.id,c]));
 const root=groups.get(f.rootGroupId),reference=(id:string|null)=>id===null||controllers.has(id);
 if(!root||root.parentId!==null||f.controllers.filter(c=>c.kind==='simple').length!==1||f.controllers.filter(c=>c.kind==='delegation').length!==1)return false;
 for(const c of f.controllers){
  if(!name(c.name)||(['simple','delegation'].includes(c.kind)?c.provider!==null:!c.provider||!profile.providers.some(p=>p.kind===c.kind&&binding(c.provider!,p.binding)))||
   c.kind!=='timetable'&&(c.harbors.length||(c.junctions?.length??0))||
   new Set((c.junctions??[]).map(j=>key(j.location))).size!==(c.junctions?.length??0)||new Set(c.harbors.map(h=>key(h.location))).size!==c.harbors.length||
   c.harbors.some(h=>h.kind==='pass'&&(h.minutes!==0||h.reverse)||h.kind==='daily'&&h.minutes>=1440))return false;
 }
 for(const g of f.groups){
  if(!name(g.name)||!reference(g.controllerId)||g.id!==root.id&&(!g.parentId||!groups.has(g.parentId)))return false;
  const expected=new Set([...f.groups.filter(c=>c.parentId===g.id).map(c=>c.id),...f.trains.filter(t=>t.groupId===g.id).map(t=>t.id)]);
  if(g.members.length!==expected.size||g.members.some(id=>!expected.delete(id)))return false;
  const seen=new Set<string>();let current:typeof g|undefined=g;
  while(current){if(seen.has(current.id))return false;seen.add(current.id);current=current.parentId===null?undefined:groups.get(current.parentId);}
 }
 const occupied=new Map(m.voxels.filter(v=>v.traffic?.car).map(v=>[v.traffic!.car!.carId,v]));
 const space=new WorldSpace(read.map.width,read.map.height,read.map.levels);
 for(const t of f.trains){
  const stock=profile.trains.find(s=>binding(t.definition,s.binding));
  if(!stock||!name(t.name)||!groups.has(t.groupId)||!reference(t.controllerId)||t.purchaseMinute!==null&&BigInt(t.purchaseMinute)>BigInt(read.tick))return false;
  const n=t.cars.length;let formation:string[];
  if(stock.body){formation=Array(n).fill(stock.body);if(stock.head)formation[0]=stock.head;if(stock.tail)formation[n-1]=stock.tail;}
  else {const pattern=(stock.patterns as string[][]).find(p=>p.length===n);if(!pattern)return false;formation=pattern;}
  const definitions=formation.map(id=>profile.cars.find(c=>c.reference.contributionId===id)!);
  const normal=definitions.reduce((n,c)=>n+c.normal,0),seated=definitions.reduce((n,c)=>n+(c.seated>=0&&c.seated<=c.normal?c.seated:c.normal),0),packed=seated+(normal-seated)*2;
  if(stock.pricePerCar*n>2147483647||normal>2147483647||packed>2147483647||t.passengers>packed||
   t.boardingOrigin!==null&&(!pose(t.boardingOrigin)||t.boardingOrigin.kind==='unplaced'))return false;
  for(const [i,c] of t.cars.entries()){
   const original=t.isReversed?n-i-1:i;
   if(c.originalIndex!==original||!equal(c.definition,definitions[original].reference)||!pose(c.state)||(t.state==='unplaced')!==(c.state.kind==='unplaced'))return false;
   const s=c.state;
   if(s.kind==='inside'){
    const v=occupied.get(c.id),r=v?.traffic?.rail,car=v?.traffic?.car;
    if(!v||!r||!car||key(v.position)!==key(s.location!)||car.trainId!==t.id||!equal(car.definition,c.definition))return false;
    // Source placement starts at rr.dir1, even on a curve with no opposite port.
    // Normal/junction guides can turn one eighth; special-purpose/slope guides keep heading.
    const edges=fleetRailPorts(r.pattern),handler=railwayProfile.bindings.find(b=>b.reference.packId===r.definition.packId&&
      b.reference.contributionId===r.definition.contributionId&&b.reference.recipeId===r.definition.recipeId)?.handler;
    if(!edges.includes(s.heading!)&&!((handler==='normal'||handler==='junction')&&
      [((s.heading!+7)%8),((s.heading!+1)%8)].some(d=>edges.includes(d))))return false;
    occupied.delete(c.id);
   }else if(s.kind==='outside'){
    const p=s.location!,storage=space.storage(p);
    if(!(storage.h<0||storage.h>=read.map.width||storage.v<int32(2*p.z)||int32(read.map.height-int32(2*int32(read.map.levels-p.z)))<=storage.v))return false;
   }
  }
 }
 if(occupied.size)return false;
 const expected=new Map(m.voxels.filter(v=>v.construction&&v.construction.state<4).map(v=>[`construction/${v.ownerId}/${v.position.x}/${v.position.y}/${v.position.z}`,true]));
 for(const t of f.trains)if(t.state!=='unplaced')expected.set(`train/${t.id}`,false);
 const orders=new Set<string>(),registrations=new Set<string>();let priorMinute=-1n,priorOrder=-1n;
 for(const a of q.pending){const due=BigInt(a.dueMinute),order=BigInt(a.order);
  if(!expected.has(a.handlerId)||a.registration!==a.order||orders.has(a.order)||registrations.has(a.registration)||order>=BigInt(q.nextOrder)||
   due<=BigInt(read.tick)||due>maximumGameMinute||due<priorMinute||due===priorMinute&&order<=priorOrder||expected.get(a.handlerId)&&due-BigInt(read.tick)>119n)return false;
  expected.delete(a.handlerId);orders.add(a.order);registrations.add(a.registration);priorMinute=due;priorOrder=order;
 }
 return expected.size===0;
}

// A complete fleet replacement in a mixed delta carries deletions atomically.
// Preserve fixed car ownership, built-ins/groups, allocator high-water and revisions.
export function validFleetSuccessor(before:ReadFleet|undefined,after:Fleet|undefined,firstNewId:string):boolean {
 if(!before)return !after||[...after.trains,...after.trains.flatMap(t=>t.cars),...after.groups,...after.controllers].every(r=>BigInt(r.id)>=BigInt(firstNewId)&&r.revision==='0');
 if(!after||before.rootGroupId!==after.rootGroupId)return false;
 const members=(f:ReadFleet)=>[...f.trains.map(t=>({...t,memberType:'train'})),...f.trains.flatMap(t=>t.cars.map(c=>({...c,owner:t.id,memberType:'car'}))),...f.groups.map(g=>({...g,memberType:'group'})),...f.controllers.map(c=>({...c,memberType:'controller'}))];
 const old=new Map(members(before).map(r=>[r.id,r])),now=new Map(members(after).map(r=>[r.id,r]));
 for(const g of before.groups)if(!now.has(g.id))return false;
 for(const c of before.controllers)if(['simple','delegation'].includes(c.kind)&&!now.has(c.id))return false;
 for(const row of now.values()){
  const b=old.get(row.id);
  if(!b){if(BigInt(row.id)<BigInt(firstNewId)||row.revision!=='0')return false;continue;}
  // A publication may coalesce several committed movements or commands.
  // Revisions must advance for changed records, but need not advance by one.
  if(row.memberType!==b.memberType||row.revision===b.revision&&!equal(row,b)||BigInt(row.revision)<BigInt(b.revision))return false;
  if('cars' in row&&'cars' in b&&(!equal(row.definition,b.definition)||!equal(row.cars.map(c=>c.id).sort(),b.cars.map(c=>c.id).sort())))return false;
  if('owner' in row&&'owner' in b&&(row.owner!==b.owner||row.originalIndex!==b.originalIndex||!equal(row.definition,b.definition)))return false;
  if('provider' in row&&'provider' in b&&(row.kind!==b.kind||!equal(row.provider,b.provider)))return false;
 }
 return true;
}

// Source Train.getController: only explicit delegation walks parent groups;
// a null assignment and an exhausted delegation chain fall back to Simple.
export function effectiveFleetController(fleet:ReadFleet,trainId:string) {
 const train=fleet.trains.find(t=>t.id===trainId);if(!train)return undefined;
 let id=train.controllerId,group=fleet.groups.find(g=>g.id===train.groupId);
 const seen=new Set<string>();
 while(id!==null&&fleet.controllers.find(c=>c.id===id)?.kind==='delegation'&&group){
  if(seen.has(group.id))return undefined;seen.add(group.id);
  id=group.controllerId;group=fleet.groups.find(g=>g.id===group!.parentId);
 }
 const controller=fleet.controllers.find(c=>c.id===id);
 return !controller||controller.kind==='delegation'?fleet.controllers.find(c=>c.kind==='simple'):controller;
}
