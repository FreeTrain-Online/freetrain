import type { SpatialPreview, SpatialQuery } from '../generated/contracts.ts';
import type { ReadState } from './session-client.ts';
import { directionTo, distance, snap } from './map-geometry.ts';
import { mapSpace } from './map-space.ts';
import { sameIdentity } from './world-identity.ts';
export function localSpatialPreview(read:ReadState,query:SpatialQuery):SpatialPreview {
  if(query.expectedWorldEpoch!==read.worldEpoch)throw new Error('Stale spatial query');
  const space=mapSpace(read.map),position=query.anchor?snap(query.position,query.anchor,query.snap):query.position;
  if(query.snap!==0&&!query.anchor)throw new Error('Missing snap anchor');
  const p=space.project(position);
  return {worldEpoch:read.worldEpoch,revision:read.revision,ruleset:read.ruleset,contentVersion:read.contentVersion,
    requiredPacks:read.requiredPacks.map(pack=>({...pack})),position:{...position},inside:space.contains(position),projected:{a:p.x,b:p.y},storage:space.storage(position),
    heading:query.anchor&&(query.anchor.x!==position.x||query.anchor.y!==position.y)?directionTo(query.anchor,position):null,
    distance:query.anchor?distance(query.anchor,position):null};
}
export function matchesSpatialPreview(local:SpatialPreview,remote:SpatialPreview):boolean {
  return sameIdentity(local,remote)&&local.worldEpoch===remote.worldEpoch&&BigInt(remote.revision)>=BigInt(local.revision)&&
    local.position.x===remote.position.x&&local.position.y===remote.position.y&&local.position.z===remote.position.z&&
    local.inside===remote.inside&&local.projected.a===remote.projected.a&&local.projected.b===remote.projected.b&&
    local.heading===remote.heading&&local.distance===remote.distance&&
    (local.storage===null?remote.storage===null:remote.storage!==null&&local.storage.h===remote.storage.h&&local.storage.v===remote.storage.v&&local.storage.z===remote.storage.z);
}
