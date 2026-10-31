import {railwayProfile,validMixed} from './mixed-state.ts';
import { terrainProfile, validTerrain, isValidatedTerrain } from './terrain-state.ts';
import { maximumGameMinute } from './game-time.ts';
import canonical from '../../../prototype/protocol/canonical-profile.json' with { type: 'json' };
import catalog from '../../../src/FreeTrain.Online.Content/reference-spatial.json' with { type: 'json' };
import { WorldSpace, move } from './map-geometry.ts';
import profile from '../../../prototype/protocol/prototype-profile.json' with { type: 'json' };
import type { RequiredPack, Snapshot } from '../generated/contracts.ts';
export interface WorldIdentity { ruleset:string;contentVersion:string;requiredPacks:readonly RequiredPack[] }
export class IncompatibleWorldError extends Error {}
export function sameIdentity(a:WorldIdentity,b:WorldIdentity):boolean {
  return a.ruleset===b.ruleset&&a.contentVersion===b.contentVersion&&a.requiredPacks.length===b.requiredPacks.length&&
    new Set(a.requiredPacks.map(p=>p.id)).size===a.requiredPacks.length&&
    new Set(b.requiredPacks.map(p=>p.id)).size===b.requiredPacks.length&&
    a.requiredPacks.every(pack=>b.requiredPacks.some(p=>p.id===pack.id&&p.version===pack.version&&p.catalogHash===pack.catalogHash));
}
// The server selects the whole exact profile. A browser never combines packs or
// accepts a contribution merely because another installed pack uses the same ID.
export function resolveProfile(identity:WorldIdentity) {return [canonical,profile,terrainProfile,railwayProfile].find(p=>sameIdentity(identity,p));}
export function supportedIdentity(identity:WorldIdentity):boolean { return !!resolveProfile(identity); }
function validReferenceEntities(read:Snapshot,space:WorldSpace):boolean {
  if(!read.entities||read.entities.length!==read.tracks.length)return false;
  const entities=new Map(read.entities.map(e=>[e.id,e]));
  if(entities.size!==read.entities.length)return false;
  const used=new Set<string>();
  for(const track of read.tracks){
    const e=track.entityId&&entities.get(track.entityId);
    if(!e||used.has(e.id)||e.revision!==track.revision||e.revision!=='0'||!e.owned||e.silentlyReclaimable||e.value!=='0'||
      e.definition.packId!==canonical.requiredPacks[0].id||e.definition.contributionId!=='normal-rail'||e.definition.recipeId!==track.shape||
      e.anchor.x!==track.position.x||e.anchor.y!==track.position.y||e.anchor.z!==track.position.z)return false;
    const recipe=catalog.contributions[0].recipes.find(r=>r.id===e.definition.recipeId);
    if(!recipe||e.footprint.length!==recipe.footprint.length)return false;
    for(const member of e.footprint){
      if(!recipe.footprint.some(m=>m.offset.x===member.offset.x&&m.offset.y===member.offset.y&&m.offset.z===member.offset.z&&m.transparent===member.transparent))return false;
      if(!space.contains({x:e.anchor.x+member.offset.x,y:e.anchor.y+member.offset.y,z:e.anchor.z+member.offset.z}))return false;
    }
    used.add(e.id);
  }
  return used.size===entities.size;
}
const validatedSnapshots=new WeakSet<object>();
export function validSnapshot(read:Snapshot):boolean {
  if(Object.isFrozen(read)&&validatedSnapshots.has(read)||sameIdentity(read,terrainProfile)&&isValidatedTerrain(read))return true;
  const valid=validSnapshotUncached(read);if(valid)validatedSnapshots.add(read);return valid;
}
function validSnapshotUncached(read:Snapshot):boolean {
  if (sameIdentity(read,railwayProfile)) {try{return validMixed(read);}catch{return false;}}
  if(read.mixed!==undefined)return false;
  if (sameIdentity(read,terrainProfile)) { try { return validTerrain(read); } catch { return false; } }
  if(read.terrain!==undefined)return false;
  if (sameIdentity(read,canonical)) {
    if(read.map.coordinateSystem!=='hv'||read.map.width<1||read.map.width>64||read.map.height<1||read.map.height>128||read.map.levels<1||read.map.levels>16||
      BigInt(read.tick)>maximumGameMinute||read.cash!=='0'||read.trains.length||read.stations.length||read.buildings.length||read.tracks.length<8||read.tracks.length>1024)return false;
    const space=new WorldSpace(read.map.width,read.map.height,read.map.levels);
    if(!validReferenceEntities(read,space))return false;
    const key=(p:{x:number;y:number;z:number})=>`${p.x},${p.y},${p.z}`;
    const cells=new Map(read.tracks.map(t=>[key(t.position),t]));
    if(cells.size!==read.tracks.length)return false;
    const directions=new Set<number>();
    try {
      for(const track of read.tracks) {
        const ports=normalPorts(track.shape);
        if(!ports||track.revision!=='0'||track.position.z!==0||!space.contains(track.position))return false;
        for(const h of ports) {
          directions.add(h);
          const next=cells.get(key(move(track.position,h)));
          if(!next||!normalPorts(next.shape)?.includes((h+4)%8))return false;
        }
      }
      const visited=new Set<string>(),pending=[read.tracks[0].position];
      while(pending.length){const p=pending.pop()!,id=key(p);if(visited.has(id))continue;visited.add(id);for(const h of normalPorts(cells.get(id)!.shape)!)pending.push(move(p,h));}
      return visited.size===cells.size&&directions.size===8;
    }catch{return false;}
  }
  // Historical prototype fixtures remain explicitly tagged.
  return supportedIdentity(read)&&read.entities===undefined&&read.tracks.every(t=>t.entityId===undefined)&&read.map.coordinateSystem==='prototypeXY'&&read.map.width===64&&read.map.height===64&&read.map.levels===16&&read.tracks.every(t=>!normalPorts(t.shape))&&
    read.trains.every(t=>t.cars===t.carPositions.length&&t.cars===t.carHeadings.length&&t.heading===t.carHeadings[0]&&
      t.carHeadings.every(h=>h===0||h===2||h===4||h===6)&&
      t.position.x===t.carPositions[0].x&&t.position.y===t.carPositions[0].y&&t.position.z===t.carPositions[0].z);
}

export function normalPorts(shape:string):readonly number[]|undefined {
  const index=/^normal([0-9]|1[01])$/.exec(shape);
  return index?catalog.normalRailPorts[Number(index[1])]:undefined;
}
export function isReferenceWorld(read:WorldIdentity|undefined):boolean {return !!read&&sameIdentity(read,canonical);}

export function isTerrainWorld(read:WorldIdentity):boolean {return sameIdentity(read,terrainProfile);}

export function isMixedWorld(read:WorldIdentity):boolean {return sameIdentity(read,railwayProfile);}
