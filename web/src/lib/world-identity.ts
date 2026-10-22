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
    a.requiredPacks.every(pack=>b.requiredPacks.some(p=>p.id===pack.id&&p.version===pack.version&&p.catalogHash===pack.catalogHash));
}
export function supportedIdentity(identity:WorldIdentity):boolean { return sameIdentity(identity,profile)||sameIdentity(identity,canonical); }
export function validSnapshot(read:Snapshot):boolean {
  if (sameIdentity(read,canonical)) {
    if(read.map.coordinateSystem!=='hv'||read.map.width<1||read.map.width>64||read.map.height<1||read.map.height>128||read.map.levels<1||read.map.levels>16||
      read.tick!=='0'||read.revision!=='0'||!read.paused||read.cash!=='0'||read.trains.length||read.stations.length||read.buildings.length||read.tracks.length<8||read.tracks.length>1024)return false;
    const space=new WorldSpace(read.map.width,read.map.height,read.map.levels);
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
  return supportedIdentity(read)&&read.map.coordinateSystem==='prototypeXY'&&read.map.width===64&&read.map.height===64&&read.map.levels===16&&read.tracks.every(t=>!normalPorts(t.shape))&&
    read.trains.every(t=>t.cars===t.carPositions.length&&t.cars===t.carHeadings.length&&t.heading===t.carHeadings[0]&&
      t.carHeadings.every(h=>h===0||h===2||h===4||h===6)&&
      t.position.x===t.carPositions[0].x&&t.position.y===t.carPositions[0].y&&t.position.z===t.carPositions[0].z);
}

export function normalPorts(shape:string):readonly number[]|undefined {
  const index=/^normal([0-9]|1[01])$/.exec(shape);
  return index?catalog.normalRailPorts[Number(index[1])]:undefined;
}
export function isReferenceWorld(read:WorldIdentity|undefined):boolean {return !!read&&sameIdentity(read,canonical);}
