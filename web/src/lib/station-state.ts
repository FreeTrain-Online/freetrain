import type {Snapshot,StationGraph,MixedAction,StationPlatform} from '../generated/contracts.ts';
import type {ReadState} from './session-client.ts';
import profile from '../generated/station-profile.json' with {type:'json'};
import rail from '../generated/railway-profile.json' with {type:'json'};
import {fleetRailPorts} from './fleet-state.ts';
import {move,WorldSpace} from './map-geometry.ts';
import {maximumGameMinute} from './game-time.ts';
const equal=(a:unknown,b:unknown)=>JSON.stringify(a)===JSON.stringify(b);
const key=(p:{x:number;y:number;z:number})=>`${p.x},${p.y},${p.z}`;
const validName=(s:string)=>s.trim().length>0&&!/[\u0000-\u001f\u007f-\u009f]/u.test(s);
const unique=(ids:string[])=>new Set(ids).size===ids.length;
export function stationTimers(g:StationGraph|undefined):MixedAction[]{return [...(g?.buildings??[]).flatMap(b=>[b.hourTimer,b.dayTimer]),...(g?.platforms??[]).map(p=>p.dayTimer)].filter((t):t is MixedAction=>t!==null);}
export function validMixedQueue(read:Snapshot):boolean {
 const m=read.mixed!,q=m.queue;
 if(!m.fleet&&!m.stationGraph)return q===undefined;
 if(!q)return false;
 const timers=new Map(stationTimers(m.stationGraph).map(t=>[t.handlerId,t]));
 const expected=new Map(m.voxels.filter(v=>v.construction&&v.construction.state<4).map(v=>[`construction/${v.ownerId}/${v.position.x}/${v.position.y}/${v.position.z}`,true]));
 for(const t of m.fleet?.trains??[])if(t.state!=='unplaced')expected.set(`train/${t.id}`,false);
 for(const id of timers.keys())expected.set(id,false);
 const orders=new Set<string>(),registrations=new Set<string>();let priorMinute=-1n,priorOrder=-1n;
 for(const a of q.pending){const due=BigInt(a.dueMinute),order=BigInt(a.order),timer=timers.get(a.handlerId);
  if(!expected.has(a.handlerId)||orders.has(a.order)||registrations.has(a.registration)||order>=BigInt(q.nextOrder)||
   BigInt(a.registration)>order||due<=BigInt(read.tick)||due>maximumGameMinute||due<priorMinute||due===priorMinute&&order<=priorOrder||
   (timer?!equal(timer,a):a.interval!=='0'||a.registration!==a.order)||expected.get(a.handlerId)&&due-BigInt(read.tick)>119n)return false;
  expected.delete(a.handlerId);orders.add(a.order);registrations.add(a.registration);priorMinute=due;priorOrder=order;
 }
 return expected.size===0;
}
export function validStations(read:Snapshot):boolean {
 const m=read.mixed!,g=m.stationGraph;
 if(!g)return !m.voxels.some(v=>v.traffic?.platform);
 if(g.buildings.length+g.platforms.length+g.passages.length>8192||g.platforms.reduce((n,p)=>n+p.yards.length,0)>8192||
  g.buildings.reduce((n,b)=>n+b.children.length,0)+g.platforms.reduce((n,p)=>n+p.children.length,0)>8192||
  g.listenerMemberships.reduce((n,l)=>n+l.providers.length,0)>32768)return false;
 const members=[...g.buildings,...g.platforms],ids=new Set(members.map(b=>b.id));
 const fleetIds=new Set([...(m.fleet?.trains??[]),...(m.fleet?.trains??[]).flatMap(t=>t.cars),...(m.fleet?.groups??[]),...(m.fleet?.controllers??[])].map(x=>x.id));
 if(ids.size!==members.length||members.some(b=>fleetIds.has(b.id)||BigInt(b.id)>=BigInt(m.nextEntityId)||!validName(b.name)||!unique(b.children)))return false;
 const buildings=new Map(g.buildings.map(b=>[b.id,b])),platforms=new Map(g.platforms.map(p=>[p.id,p]));
 const entities=new Map(read.entities!.map(e=>[e.id,e])),voxels=new Map(m.voxels.map(v=>[key(v.position),v]));
 const space=new WorldSpace(read.map.width,read.map.height,read.map.levels);
 const timer=(t:MixedAction|null,id:string,interval:string)=>!t||t.handlerId===id&&t.interval===interval&&BigInt(t.registration)<=BigInt(t.order)&&
  BigInt(t.dueMinute)>BigInt(read.tick)&&BigInt(t.dueMinute)<=maximumGameMinute&&BigInt(t.dueMinute)-BigInt(read.tick)<=BigInt(interval)&&
  (!id.startsWith('station/')||BigInt(t.dueMinute)%BigInt(interval)===0n);
 const owner=(id:string,revision:string,location:{x:number;y:number;z:number},definition:unknown,offsets:{x:number;y:number;z:number}[])=>{
  const e=entities.get(id);return !!e&&e.revision===revision&&equal(e.anchor,location)&&equal(e.definition,definition)&&e.owned&&!e.silentlyReclaimable&&e.value==='0'&&
   e.footprint.length===offsets.length&&e.footprint.every(f=>!f.transparent&&offsets.some(p=>key(p)===key(f.offset)))&&
   offsets.every(o=>{const p={x:location.x+o.x,y:location.y+o.y,z:location.z+o.z};return space.contains(p)&&!voxels.has(key(p));});
 };
 for(const b of g.buildings){
  const d=profile.definitions.find(d=>equal(d.reference,b.definition));if(!d)return false;
  const offsets=[];for(let x=0;x<d.sizeX;x++)for(let y=0;y<d.sizeY;y++)for(let z=0;z<d.height;z++)offsets.push({x,y,z});
  if(!owner(b.id,b.revision,b.location,b.definition,offsets)||(b.hourTimer===null)!==(b.dayTimer===null)||
   !timer(b.hourTimer,`station/${b.id}/hour`,'60')||!timer(b.dayTimer,`station/${b.id}/day`,'1440'))return false;
  for(const l of [b.imports,b.exports,b.trains])if(!Number.isFinite(l.thisWeek)||!Number.isFinite(l.lastWeekPerDay))return false;
 }
 const yards=new Set<string>(),yardOwners=new Set<string>();
 const definition=(id:string)=>({packId:read.requiredPacks[0].id,contributionId:id,recipeId:id});
 const center=(p:StationPlatform,i:number)=>{let q=p.location;for(let j=0;j<i;j++)q=move(q,p.direction);return q;};
 const traffic=(id:string,location:{x:number;y:number;z:number},direction:number)=>{
  const v=voxels.get(key(location)),r=v?.traffic?.rail,ports=r&&fleetRailPorts(r.pattern);
  return v?.kind==='traffic'&&v.ownerId===id&&r&&rail.bindings.some(b=>b.handler==='normal'&&equal(b.reference,r.definition))&&
   ports?.includes(direction)&&(ports[0]+4)%8===ports[1]?v:undefined;
 };
 for(const p of g.platforms){
  if(!profile.bells.some(b=>equal(b,p.bell))||!timer(p.dayTimer,`platform/${p.id}/day`,'1440')||
   p.kind==='thin'&&(!p.lane0||p.lane1||entities.has(p.id))||p.kind==='wide'&&p.roof||
   p.yards.length!==p.length*(Number(p.lane0)+Number(p.lane1)))return false;
  if(p.kind==='wide'){
   const offsets=Array.from({length:p.length},(_,i)=>{const q=center(p,i);return {x:q.x-p.location.x,y:q.y-p.location.y,z:0};});
   if(!owner(p.id,p.revision,p.location,definition(`system:fat-${p.direction/2%2}`),offsets))return false;
  }
  const seen=new Set<string>();
  for(const y of p.yards){const k=`${y.lane}/${y.index}`,o=y.outlook;
   const pos=p.kind==='thin'?center(p,y.index):move(center(p,y.index),(p.direction+(y.lane===0?2:6))%8);
   const roof=p.kind==='thin'&&p.roof&&y.index>=Math.trunc(p.length/4)&&y.index<p.length-Math.trunc(p.length/4);
   const v=traffic(y.trafficOwnerId,y.location,p.direction);
   if(seen.has(k)||yards.has(key(y.location))||yardOwners.has(y.trafficOwnerId)||y.index>=p.length||!(y.lane===0?p.lane0:p.lane1)||
    !equal(pos,y.location)||y.roof!==roof||!v||!equal(v.traffic!.platform,definition(p.kind==='thin'?`system:thin-${p.direction+(roof?1:0)}`:'system:yard'))||
    (p.kind==='wide'||o.kind==='plain')&&(o.kind!=='plain'||o.material!=='concrete'||o.bridge||o.upward)||o.kind==='passage'&&o.upward||o.kind==='stair'&&o.bridge)return false;
   seen.add(k);yards.add(key(y.location));yardOwners.add(y.trafficOwnerId);
  }
  const path=new Set<string>();let current:StationPlatform|undefined=p;
  while(current){if(path.has(current.id))return false;path.add(current.id);
   const host=current.hostId;if(host===null){if(p.hostId!==null)return false;break;}
   const parent=buildings.get(host)??platforms.get(host);if(!parent||!parent.children.includes(current.id))return false;
   current=platforms.get(host);
  }
 }
 for(const b of members)for(const id of b.children)if(platforms.get(id)?.hostId!==b.id)return false;
 for(const v of m.voxels)if(v.traffic?.platform&&!yards.has(key(v.position)))return false;
 for(const p of g.passages){const v=traffic(p.trafficOwnerId,p.location,p.direction);
  if(!v||v.traffic!.platform||yards.has(key(p.location))||yardOwners.has(p.trafficOwnerId))return false;
  yards.add(key(p.location));yardOwners.add(p.trafficOwnerId);
 }
 const providers=new Set(g.listeners.map(l=>l.providerId));
 if(!unique(g.listeners.map(l=>l.providerId))||!unique(g.listenerMemberships.map(l=>l.stationId))||g.listeners.some(l=>
   !entities.has(l.providerId)||ids.has(l.providerId)||!unique(l.stations)||l.stations.some(id=>!buildings.has(id)))||
   g.listenerMemberships.some(l=>!buildings.has(l.stationId)||!unique(l.providers)||l.providers.some(id=>!providers.has(id))))return false;
 const timers=stationTimers(g);return unique(timers.map(t=>t.registration))&&unique(timers.map(t=>t.order));
}
export function validStationSuccessor(before:NonNullable<ReadState['mixed']>['stationGraph'],after:StationGraph|undefined,firstId:string,previous:Snapshot,next:Snapshot):boolean {
 if(!after)return !before;
 if(before&&(after.nextStationName<before.nextStationName||after.nextPlatformName<before.nextPlatformName))return false;
 const rows=(g:NonNullable<ReadState['mixed']>['stationGraph'])=>[...(g?.buildings??[]).map(b=>({...b,type:'building'})),...(g?.platforms??[]).map(p=>({...p,type:'platform'}))];
 const old=new Map(rows(before).map(b=>[b.id,b]));
 for(const b of rows(after)){const a=old.get(b.id);
  if(!a){if(BigInt(b.id)<BigInt(firstId)||b.revision!=='0')return false;continue;}
  if(b.type!==a.type||BigInt(b.revision)<BigInt(a.revision)||b.revision===a.revision&&!equal(b,a)||!equal(b.location,a.location))return false;
  if('definition'in b&&'definition'in a&&!equal(b.definition,a.definition))return false;
  if('kind'in b&&'kind'in a&&['kind','direction','length','roof'].some(k=>!equal(b[k as keyof typeof b],a[k as keyof typeof a])))return false;
 }
 for(const b of after.buildings)if(before?.buildings.some(a=>a.id===b.id&&a.revision===b.revision)&&
  !equal(before.listenerMemberships.find(l=>l.stationId===b.id)?.providers??[],after.listenerMemberships.find(l=>l.stationId===b.id)?.providers??[]))return false;
 for(const id of new Set([...(before?.listeners??[]).map(l=>l.providerId),...after.listeners.map(l=>l.providerId),...(before?.passages??[]).map(p=>p.trafficOwnerId),...after.passages.map(p=>p.trafficOwnerId)])){
  if((!equal(before?.listeners.find(l=>l.providerId===id)?.stations??[],after.listeners.find(l=>l.providerId===id)?.stations??[])||
     !equal(before?.passages.find(p=>p.trafficOwnerId===id),after.passages.find(p=>p.trafficOwnerId===id)))&&
     next.entities!.some(e=>e.id===id&&e.revision===previous.entities!.find(e=>e.id===id)?.revision))return false;
 }
 return true;
}
