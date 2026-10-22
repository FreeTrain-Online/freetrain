import profile from '../../../prototype/protocol/prototype-profile.json' with { type: 'json' };
import type { RequiredPack, Snapshot } from '../generated/contracts.ts';
export interface WorldIdentity { ruleset:string;contentVersion:string;requiredPacks:readonly RequiredPack[] }
export class IncompatibleWorldError extends Error {}
export function sameIdentity(a:WorldIdentity,b:WorldIdentity):boolean {
  return a.ruleset===b.ruleset&&a.contentVersion===b.contentVersion&&a.requiredPacks.length===b.requiredPacks.length&&
    new Set(a.requiredPacks.map(p=>p.id)).size===a.requiredPacks.length&&
    a.requiredPacks.every(pack=>b.requiredPacks.some(p=>p.id===pack.id&&p.version===pack.version&&p.catalogHash===pack.catalogHash));
}
export function supportedIdentity(identity:WorldIdentity):boolean { return sameIdentity(identity,profile); }
export function validSnapshot(read:Snapshot):boolean {
  // Only the reviewed prototype pack is currently installed. Canonical packs arrive in P2.
  return supportedIdentity(read)&&read.map.coordinateSystem==='prototypeXY'&&read.map.width===64&&read.map.height===64&&read.map.levels===16&&
    read.trains.every(t=>t.cars===t.carPositions.length&&t.cars===t.carHeadings.length&&t.heading===t.carHeadings[0]&&
      t.carHeadings.every(h=>h===0||h===2||h===4||h===6)&&
      t.position.x===t.carPositions[0].x&&t.position.y===t.carPositions[0].y&&t.position.z===t.carPositions[0].z);
}
