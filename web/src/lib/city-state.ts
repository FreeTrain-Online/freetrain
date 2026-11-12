import type {City,Definition,MixedAction,Snapshot} from '../generated/contracts.ts';
import profile from '../generated/city-profile.json' with {type:'json'};
import type {ReadState} from './session-client.ts';
import {WorldSpace} from './map-geometry.ts';
const equal=(a:unknown,b:unknown)=>JSON.stringify(a)===JSON.stringify(b);
const same=(a:Definition,b:Definition)=>a.packId===b.packId&&a.contributionId===b.contributionId&&a.recipeId===b.recipeId;
const key=(p:{x:number;y:number;z:number})=>`${p.x},${p.y},${p.z}`;
const max=9223372036854775807n;
export const cityProfile=profile;
export const cityOwners=(c:City|undefined)=>c?[...c.properties,...c.structures,...c.ground,...c.fenceHosts]:[];
export const cityTimers=(c:City|undefined):MixedAction[]=>c?[...c.structures.flatMap(s=>[...s.construction.map(v=>v.callback),s.subsidiary?.callback]),c.heatCallback,c.developmentCallback].filter((a):a is MixedAction=>!!a):[];
const structureDefinition=(d:Definition)=>profile.structures.find(s=>same(s.reference,d));
const groundDefinition=(d:Definition)=>profile.ground.find(s=>same(s.reference,d));
const price=(d:NonNullable<ReturnType<typeof structureDefinition>>,height:number)=>BigInt(d.unitPrice)*BigInt(d.kind==='variable'?height:1);
export function validCity(read:Snapshot):boolean {
 const c=read.mixed?.city;
 // A city discriminator cannot be laundered through controlled/foreign owners.
 const listeners=read.mixed?.stationGraph?.listeners??[];
 if(!c)return listeners.every(l=>!l.provider||l.provider.input==='controlled');
 const space=new WorldSpace(read.map.width,read.map.height,read.map.levels),entities=new Map(read.entities!.map(e=>[e.id,e]));
 const owners=cityOwners(c),ids=new Set(owners.map(o=>o.id)),producers=new Map(c.producers.map(p=>[p.ownerId,p]));
 const reserved=new Set([...(read.terrain?.occupants??[]).map(o=>o.entityId),...(read.mixed!.stationGraph?.buildings??[]).map(o=>o.id),
  ...(read.mixed!.stationGraph?.platforms??[]).map(o=>o.id),...(read.mixed!.fleet?.trains??[]).flatMap(t=>[t.id,...t.cars.map(v=>v.id)]),
  ...(read.mixed!.fleet?.groups??[]).map(o=>o.id),...(read.mixed!.fleet?.controllers??[]).map(o=>o.id)]);
 if(c.format!==1||owners.length>8192||ids.size!==owners.length||owners.some(o=>reserved.has(o.id)||BigInt(o.id)<1n||BigInt(o.revision)<0n)||
  producers.size!==c.producers.length||c.structures.reduce((n,s)=>n+s.construction.length,0)>8192)return false;
 const heat=c.heat,n=(read.map.width+2)*(read.map.height+2);
 if(heat.horizontalSize!==read.map.width||heat.verticalSize!==read.map.height||heat.q.length!==n||heat.back.length!==n||heat.rho.length!==n||
  [heat.q,heat.back,heat.rho].some(a=>a.some(v=>!Number.isFinite(v)||Math.abs(v)>3.4028234663852886e38))||heat.rho.some(v=>v<0))return false;
 if(Object.values(c.settings).some(v=>!Number.isFinite(v))||c.settings.updateFrequencyHours<1||c.settings.maximumPricePower===0||!Number.isFinite(1/c.settings.maximumPricePower))return false;
 const owner=(o:{id:string;revision:string;location:{x:number;y:number;z:number}},d:Definition,sx:number,sy:number,sz:number,owned:boolean,reusable:boolean,value:bigint,transparent:boolean)=>{
  const e=entities.get(o.id);
  if(!e||e.revision!==o.revision||!equal(e.anchor,o.location)||!same(e.definition,d)||e.owned!==owned||e.silentlyReclaimable!==reusable||BigInt(e.value)!==value||value<0n||value>max||sx*sy*sz!==e.footprint.length)return false;
  const offsets=new Set(e.footprint.map(f=>key(f.offset)));
  return offsets.size===e.footprint.length&&e.footprint.every(f=>f.transparent===transparent&&f.offset.x>=0&&f.offset.x<sx&&f.offset.y>=0&&f.offset.y<sy&&f.offset.z>=0&&f.offset.z<sz)&&
   !read.mixed!.voxels.some(v=>v.ownerId===o.id);
 };
 const hasProducer=(id:string,d:Definition,height:number,required:boolean)=>{
  const p=producers.get(id);return required?!!p&&same(p.initialDefinition,d)&&p.initialHeight===height:!p;
 };
 for(const s of c.structures){const d=structureDefinition(s.definition);
  if(!d||s.height<d.minimumHeight||s.height>d.maximumHeight||s.halves.length>2||(d.kind==='half')!==(s.halves.length>0)||
   !hasProducer(s.id,s.definition,s.height,s.construction.length===0&&!!d.population))return false;
  if(s.halves.length&&(!s.owned||s.construction.length||new Set(s.halves.map(h=>h.side)).size!==s.halves.length||new Set(s.halves.map(h=>h.direction%4)).size!==1||s.halves.some(h=>{
   const v=structureDefinition(h.definition);return !v||v.kind!=='half'||!v.halfVariants.some(f=>f.direction===h.direction&&f.side===(h.side==='back'?0:1)&&f.color===h.color);
  })))return false;
  const value=s.construction.length?0n:d.kind==='variable'?price(d,s.height):BigInt(s.subsidiary?.marketPrice??'-1');
  if(!owner(s,s.definition,d.sizeX,d.sizeY,d.kind==='half'?1:s.height,s.owned,false,value,false))return false;
  if(s.construction.length){
   const offsets=new Set(s.construction.map(v=>key(v.offset)));
   if(s.owned||s.subsidiary||offsets.size!==d.sizeX*d.sizeY*s.height||s.construction.every(v=>v.cell.state===4)||s.construction.some(v=>
    v.offset.x<0||v.offset.x>=d.sizeX||v.offset.y<0||v.offset.y>=d.sizeY||v.offset.z<0||v.offset.z>=s.height||
    v.cell.needsDoor&&v.offset.x===0&&v.offset.y===d.sizeY-1||(v.cell.state<4)!==!!v.callback||
    v.callback&&(v.callback.handlerId!==`city/construction/${s.id}/${v.offset.x}/${v.offset.y}/${v.offset.z}`||v.callback.interval!=='0'||BigInt(v.callback.dueMinute)-BigInt(read.tick)>119n)))return false;
  }else if(!s.subsidiary)return false;
  if(s.subsidiary&&(BigInt(s.subsidiary.marketPrice)<0n||BigInt(s.subsidiary.totalLandValue)<0n||s.subsidiary.callback&&
    (s.subsidiary.callback.handlerId!==`city/subsidiary/${s.id}`||s.subsidiary.callback.interval!=='0')))return false;
 }
 for(const g of c.ground){const d=groundDefinition(g.definition);
  if(!d||!['land-0d','rice','vinyl'].includes(d.handler)||g.variant<0||
   (d.handler==='land-0d'?g.variant!==0:d.handler==='rice'?g.variant>8:g.variant>2)||
   !owner(g,g.definition,1,1,1,g.owned,true,BigInt(d.price),true)||!hasProducer(g.id,g.definition,1,!!d.population))return false;
 }
 for(const o of c.properties)if(!owner(o,profile.propertyDefinition,1,1,1,true,true,0n,false))return false;
 for(const o of c.fenceHosts)if(!owner(o,profile.fenceHostDefinition,1,1,1,true,true,0n,true))return false;
 if(c.producers.some(p=>!c.structures.some(s=>s.id===p.ownerId)&&!c.ground.some(g=>g.id===p.ownerId)))return false;
 const occupied=new Map<string,string>();for(const e of read.entities!)for(const f of e.footprint)occupied.set(key({x:e.anchor.x+f.offset.x,y:e.anchor.y+f.offset.y,z:e.anchor.z+f.offset.z}),e.id);
 if(new Set(c.fences.map(f=>`${key(f.location)}/${f.side}`)).size!==c.fences.length||c.fences.some(f=>f.side<0||f.side>3||occupied.get(key(f.location))!==f.hostOwnerId||groundDefinition(f.definition)?.handler!=='fence'||groundDefinition(f.definition)?.remover))return false;
 const box=(p:{x:number;y:number;z:number},x:number,y:number,z:number)=>x>0&&y>0&&z>0&&[p,{...p,x:p.x+x-1},{...p,y:p.y+y-1},{x:p.x+x-1,y:p.y+y-1,z:p.z+z-1}].every(v=>space.contains(v));
 if(new Set(c.landCache.map(v=>`${key(v.corner)}/${v.sizeX}/${v.sizeY}`)).size!==c.landCache.length||c.landCache.some(v=>BigInt(v.landValue)<0n||BigInt(v.entityValue)<0n||!box(v.corner,v.sizeX,v.sizeY,1)))return false;
 if(new Set(c.plans.map(p=>p.stationId)).size!==c.plans.length||c.plans.some(p=>{
  if(!read.mixed!.stationGraph?.buildings.some(b=>b.id===p.stationId)||p.budget<0||p.work.length!==4||p.work.some(i=>i<0)||
   p.scanning&&!space.contains(p.scanning)||['selectstruct1','selectstruct2','fitplace','build'].includes(p.phase)&&!p.scanning||['fitplace','build'].includes(p.phase)&&!p.selected)return true;
  const v=p.selected;if(!v)return false;
  if(!box(v.site,v.sizeX,v.sizeY,v.height)||BigInt(v.value)<0n||BigInt(v.finalPrice)<0n)return true;
  const d=structureDefinition(v.definition),g=groundDefinition(v.definition);
  return d?d.computerCannotBuild||d.kind==='half'||v.sizeX!==d.sizeX||v.sizeY!==d.sizeY||v.height<d.minimumHeight||v.height>d.maximumHeight||BigInt(v.value)!==price(d,v.height)||v.value!==v.finalPrice:
   !g||g.computerCannotBuild||v.height!==1||BigInt(v.value)!==BigInt(g.price)*4n||BigInt(v.finalPrice)!==BigInt(g.price)*BigInt(v.sizeX)*BigInt(v.sizeY);
 }))return false;
 if(c.heatCallback&&(c.heatCallback.handlerId!=='city/heat'||BigInt(c.heatCallback.interval)<=0n)||c.developmentCallback&&(c.developmentCallback.handlerId!=='city/development'||BigInt(c.developmentCallback.interval)<=0n))return false;
 const timers=cityTimers(c);if(new Set(timers.map(t=>t.handlerId)).size!==timers.length)return false;
 for(const l of listeners){
  if(!ids.has(l.providerId)){if(l.provider&&l.provider.input!=='controlled')return false;continue;}
  const p=producers.get(l.providerId),v=l.provider;if(!p||!v)return false;
  const s=c.structures.find(s=>s.id===l.providerId),d=s?structureDefinition(p.initialDefinition):undefined,g=s?undefined:groundDefinition(p.initialDefinition);
  const population=d?.kind==='variable'?{kind:'MultiplierPopulation',base:0,factor:p.initialHeight,entering:0,children:[d.population],tables:[]}:d?.population??g?.population;
  if(v.input!==`city-${d?.kind??'ground'}`||v.special||v.xySize!==(d?d.sizeX*d.sizeY:1)||v.zSize!==(s&&d?.kind!=='half'?p.initialHeight:1)||!equal(v.population,population))return false;
 }
 return true;
}
export function validCitySuccessor(before:NonNullable<ReadState['mixed']>['city'],after:City|undefined,allocator:string):boolean {
 if(!after)return !before;
 const old=new Map(cityOwners(before as City|undefined).map(o=>[o.id,o]));
 const kind=(c:City,id:string)=>c.structures.some(o=>o.id===id)?'structure':c.ground.some(o=>o.id===id)?'ground':c.properties.some(o=>o.id===id)?'property':'fenceHost';
 for(const o of cityOwners(after)){
  const prior=old.get(o.id);
  if(!prior){if(BigInt(o.id)<BigInt(allocator))return false;continue;}
  if(!before||kind(before as City,o.id)!==kind(after,o.id)||BigInt(o.revision)<BigInt(prior.revision))return false;
  if('definition' in o&&'definition' in prior&&!equal(o.definition,prior.definition)||!equal(o.location,prior.location))return false;
  if(o.revision===prior.revision&&(!equal(o,prior)||!equal(before.producers.filter(p=>p.ownerId===o.id),after.producers.filter(p=>p.ownerId===o.id))||
   !equal(before.fences.filter(f=>f.hostOwnerId===o.id),after.fences.filter(f=>f.hostOwnerId===o.id))))return false;
  const a=before.producers.find(p=>p.ownerId===o.id),b=after.producers.find(p=>p.ownerId===o.id);if(a&&b&&!equal(a,b))return false;
 }
 return true;
}
