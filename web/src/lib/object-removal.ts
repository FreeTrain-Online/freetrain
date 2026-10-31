import type { RemovalQuery } from '../generated/contracts.ts';
import type { ReadState } from './session-client.ts';
import type { Selection } from './map-renderer.ts';
import { railwayRevision } from './railway-edit.ts';
import { same } from './map-display.ts';

export const removalQueryKey = (q:RemovalQuery) => JSON.stringify([q.expectedWorldEpoch,q.expectedGeometryRevision,q.expectedContentVersion,q.expectedEntityId,q.expectedEntityRevision,q.position.x,q.position.y,q.position.z,q.end?.x,q.end?.y,q.end?.z]);

export function removalAt(read:ReadState,position:RemovalQuery['position']):RemovalQuery|undefined {
  const entity=read.entities?.find(e=>e.footprint.some(m=>same({x:e.anchor.x+m.offset.x,y:e.anchor.y+m.offset.y,z:e.anchor.z+m.offset.z},position)));
  return entity?removalQuery(read,{kind:'track',id:entity.id,position}):undefined;
}

export function removalQuery(read:ReadState, picked:Selection):RemovalQuery|undefined {
  const revision=railwayRevision(read);
  if(revision===undefined)return;
  const entity=read.entities?.find(e=>e.id===picked.id)??read.entities?.find(e=>same(e.anchor,picked.position))??
    (picked.kind==='terrain'&&picked.surface==='ground'?read.entities?.find(e=>e.anchor.x===picked.position.x&&e.anchor.y===picked.position.y&&e.anchor.z===picked.position.z-1&&read.terrain?.occupants.some(o=>o.entityId===e.id&&o.kind==='terrace')):undefined);
  if(!entity)return;
  return {expectedWorldEpoch:read.worldEpoch,expectedGeometryRevision:revision,expectedContentVersion:read.contentVersion as RemovalQuery['expectedContentVersion'],position:{...entity.anchor},expectedEntityId:entity.id,expectedEntityRevision:entity.revision};
}
