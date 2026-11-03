import {validFleet} from './fleet-state.ts';
import profile from '../generated/railway-profile.json' with {type:'json'};
import ports from '../../../src/FreeTrain.Online.Content/reference-spatial.json' with {type:'json'};
import type {Snapshot,Definition,RailwayVoxel,SpatialEntity} from '../generated/contracts.ts';
import {validTerrain,shareTerrainValidation} from './terrain-state.ts';
import {WorldSpace,move} from './map-geometry.ts';
export const railwayProfile=profile;
const same=(a:Definition|null,b:Definition|null)=>a===b||!!a&&!!b&&a.packId===b.packId&&a.contributionId===b.contributionId&&a.recipeId===b.recipeId;
const key=(p:{x:number;y:number;z:number})=>`${p.x},${p.y},${p.z}`;
const maximum=9223372036854775807n,minimum=-9223372036854775808n;
export function validMixed(read:Snapshot):boolean {
 const m=read.mixed,t=read.terrain;
 if(!m||!t||!read.entities||read.tracks.length||read.trains.length||read.stations.length||read.buildings.length||
    BigInt(m.geometryRevision)>BigInt(read.revision)||BigInt(m.geometryRevision)<BigInt(t.geometryRevision)||BigInt(m.nextEntityId)<BigInt(t.nextEntityId))return false;
 const terrainIds=new Set(t.occupants.map(o=>o.entityId)),terrainEntities=read.entities.filter(e=>terrainIds.has(e.id));
 const terrainRead={...read,cash:'0',entities:terrainEntities,mixed:undefined};
 const tunnelCorners=new Map<number,number>(),boundarySpace=new WorldSpace(read.map.width,read.map.height,read.map.levels);
 for(const v of m.voxels){const r=v.traffic?.rail,b=r&&profile.bindings.find(b=>same(b.reference,r.definition));
  if(b?.handler==='tunnel'){
   const p=v.position,c=r!.terrainBackup;if(!c||!boundarySpace.contains(p))return false;
   const s=boundarySpace.storage(p),i=s.v*read.map.width+s.h;
   if(t.cells[i].ground!==p.z||tunnelCorners.has(i))return false;
   tunnelCorners.set(i,c.ne<<12|c.se<<8|c.sw<<4|c.nw);
  }
 }
 if(!validTerrain(terrainRead,tunnelCorners))return false;
 shareTerrainValidation(terrainRead,read);
 const space=new WorldSpace(read.map.width,read.map.height,read.map.levels),occupied=new Set<string>();
 const entities=new Map<string,SpatialEntity>(),owners=new Map<string,RailwayVoxel[]>(),voxels=new Map<string,RailwayVoxel>();
 const binding=(d:Definition|null)=>d&&profile.bindings.find(b=>same(b.reference,d));
 for(const e of read.entities){
  if(entities.has(e.id)||BigInt(e.id)>=BigInt(m.nextEntityId))return false;entities.set(e.id,e);
  for(const f of e.footprint){const p={x:e.anchor.x+f.offset.x,y:e.anchor.y+f.offset.y,z:e.anchor.z+f.offset.z},k=key(p);
   if(!space.contains(p)||occupied.has(k))return false;occupied.add(k);}
 }
 for(const v of m.voxels){
  if(!space.contains(v.position)||voxels.has(key(v.position))||terrainIds.has(v.ownerId)||!entities.has(v.ownerId))return false;
  voxels.set(key(v.position),v);const group=owners.get(v.ownerId)??[];group.push(v);owners.set(v.ownerId,group);
 }
 if(m.speedLimits)for(const list of [m.speedLimits.low,m.speedLimits.medium,m.speedLimits.fast]) {
  if(list.length>8192||list.some(p=>!voxels.get(key(p))?.traffic))return false;
 }
 const slopes=new Map(m.slopes.map(s=>[s.entityId,s]));if(slopes.size!==m.slopes.length)return false;
 for(const e of read.entities){
  if(terrainIds.has(e.id))continue;
  const group=owners.get(e.id),s=slopes.get(e.id);
  if(!group||group.length!==e.footprint.length||e.owned!==(group[0].construction===undefined)||e.silentlyReclaimable||e.value!=='0')return false;
  for(const f of e.footprint){const v=voxels.get(key({x:e.anchor.x+f.offset.x,y:e.anchor.y+f.offset.y,z:e.anchor.z+f.offset.z}));if(v?.ownerId!==e.id||group[0].kind!=='stationary'&&!f.transparent)return false;}
  if(group[0].kind==='stationary'){
   const b=binding(e.definition),type=b&&'stationary' in b?b.stationary:undefined;
   const site=group[0].construction!==undefined;
   if(!type||group.length!==type.sizeX*type.sizeY*type.height||site&&group.every(v=>v.construction?.state===4)||
    e.footprint.some(f=>f.transparent!==(!site&&type.sizeX===1&&type.sizeY===1))||
    group.some(v=>v.kind!=='stationary'||!same(v.definition,e.definition)||(v.construction!==undefined)!==site||
     v.construction?.needsDoor&&v.position.x===e.anchor.x&&v.position.y===e.anchor.y+type.sizeY-1||v.position.x<e.anchor.x||v.position.x>=e.anchor.x+type.sizeX||v.position.y<e.anchor.y||v.position.y>=e.anchor.y+type.sizeY||v.position.z<e.anchor.z||v.position.z>=e.anchor.z+type.height))return false;
  }else if(s){
   if(!same(e.definition,profile.slopeOwner)||group.length!==8)return false;
   for(let i=0;i<4;i++){
    let p=e.anchor;for(let j=0;j<i;j++)p=move(p,s.climb);p={...p,z:e.anchor.z+Math.floor(i/2)};
    const rail=voxels.get(key(p)),pair=voxels.get(key({...p,z:e.anchor.z+(i<2?1:0)})),pattern=rail?.traffic?.rail?.pattern??-1,expected=28+s.climb/2*4+i;
    if(rail?.ownerId!==e.id||rail.kind!=='traffic'||pattern!==expected&&pattern!==expected+16||pair?.ownerId!==e.id||
       pair.kind!==(i<2?'slopefiller':pattern>=44?'slopewall':'slopesupport')||i>=2&&pair.sprite!==s.climb+i-2)return false;
   }
  }else if(group.length!==1||key(group[0].position)!==key(e.anchor)||!['traffic','pier'].includes(group[0].kind)||
      !same(e.definition,group[0].kind==='traffic'?profile.trafficOwner:group[0].definition))return false;
 }
 if([...slopes.keys()].some(id=>!owners.has(id)))return false;
 const cars=new Set<string>();
 for(const v of m.voxels){
  if(v.construction!==undefined&&(!v.construction||!Number.isInteger(v.construction.state)||v.construction.state<0||v.construction.state>4||!Number.isInteger(v.construction.ground)||v.construction.ground<0||v.construction.ground>6||typeof v.construction.needsDoor!=='boolean'))return false;
  if(v.kind==='stationary'){
   if(v.traffic!==null||binding(v.definition)?.handler!=='stationary'||v.sprite!==0||v.supportedOwnerId!==null||!v.drawGroundBelow||!v.drawGroundAbove)return false;
   continue;
  }
  if(v.construction!==undefined)return false;
  if(v.kind!=='traffic'){
   if(v.traffic!==null||v.kind==='slopefiller'&&(v.definition!==null||v.sprite!==0)||v.kind!=='pier'&&v.supportedOwnerId!==null||
      v.kind==='pier'&&(!v.drawGroundBelow||!v.drawGroundAbove))return false;
   if(v.kind!=='slopefiller'){const b=binding(v.definition);if(!b||b.handler!==v.kind||b.variant>=0&&b.variant!==v.sprite)return false;}
   if(v.kind==='pier'){
    const id=v.supportedOwnerId;if(!id||BigInt(id)>=BigInt(m.nextEntityId))return false;
    if(entities.has(id)&&!owners.get(id)?.some(o=>o.traffic?.rail&&o.position.x===v.position.x&&o.position.y===v.position.y&&o.position.z>v.position.z))return false;
   }
   continue;
  }
  const c=v.traffic;
  if(!c||v.definition!==null||v.sprite!==0||v.supportedOwnerId!==null||!v.drawGroundBelow||!v.drawGroundAbove||!c.rail&&!c.road&&!c.accessory)return false;
  const r=c.rail;
  if(r){
   const b=binding(r.definition),h=b?.handler;
   if(!h||!['normal','junction','slope','bridge','girder','tunnel','garage','signal'].includes(h)||
      ['normal','junction','slope'].includes(h)&&b!.variant!==r.pattern||h!=='slope'&&r.pattern>=28||h==='slope'&&!slopes.has(v.ownerId)||
      (r.terrainBackup!==null)!==(h==='tunnel')||r.controllerId!==null||
      ['normal','junction','slope'].includes(h)&&(r.variant!==0||r.direction!==0)||h==='girder'&&r.variant>3||h==='signal'&&r.variant>3||
      ['tunnel','garage'].includes(h)&&r.variant>1)return false;
   if(r.terrainBackup&&Object.values(r.terrainBackup).some(c=>c>4*(read.map.levels-v.position.z)-1))return false;
   if(['bridge','girder','tunnel','garage','signal'].includes(h)){
    const p=ports.normalRailPorts[r.pattern];if(!p||(p[0]+4)%8!==p[1]||h!=='girder'&&p[0]%2!==0||
      h==='signal'&&(r.direction%2!==0||!p.includes(r.direction)))return false;
   }
  }
  if(c.road&&binding(c.road.definition)?.handler!=='road'||c.car&&!m.fleet&&binding(c.car.definition)?.handler!=='car'||
     c.platform&&(!r||binding(c.platform)?.handler!=='platform'))return false;
  if(c.car){if(cars.has(c.car.carId))return false;cars.add(c.car.carId);}
  if(c.accessory){const h=binding(c.accessory.definition)?.handler;
   if(h!=='pole'&&h!=='crossing'||h==='pole'&&c.accessory.direction>1||h==='crossing'&&(!r||!c.road||!(r.pattern===10&&(c.road.edges&68)===68||r.pattern===4&&(c.road.edges&17)===17)))return false;
  }
 }
 let cash=BigInt(m.accounts.initialCash),sequence=1n,revision=0n,minute=0n,operation='',topUps=0n;
 const increment=BigInt(m.accounts.bankruptcyIncrement);if(increment<=0n)return false;
 for(const e of m.accounts.transactions){
  const r=BigInt(e.worldRevision),time=BigInt(e.absoluteMinute),amount=BigInt(e.expenditure);
  if(BigInt(e.sequence)!==sequence++||r<1n||r<revision||r>BigInt(m.geometryRevision)||time<minute||time>BigInt(read.tick)||
     r===revision&&(time!==minute||e.operation!==operation)||!e.operation.trim()||e.operation.length>128||/[\u0000-\u001f\u007f-\u009f]/u.test(e.operation))return false;
  cash-=amount;if(cash<minimum||cash>maximum)return false;
  const rescue=cash<0n?(-cash+increment-1n)/increment:0n;cash+=rescue*increment;topUps+=rescue;
  if(rescue!==BigInt(e.topUps)||cash!==BigInt(e.cashAfter)||cash>maximum||topUps>maximum||sequence>maximum)return false;
  revision=r;minute=time;operation=e.operation;
 }
 return cash===BigInt(read.cash)&&validFleet(read);
}
