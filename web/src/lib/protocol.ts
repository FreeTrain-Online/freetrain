import Ajv from 'ajv';
import schema from '../../../prototype/protocol/schema.json' with { type: 'json' };
import type { Delta, Snapshot, SpatialPreview, CornerTerrainPreview, WireMessage } from '../generated/contracts.ts';
import type { ReadState } from './session-client.ts';
import { IncompatibleWorldError, isReferenceWorld, isTerrainWorld, sameIdentity, supportedIdentity, validSnapshot } from './world-identity.ts';
import { maximumGameMinute } from './game-time.ts';
const validatedReads=new WeakSet<object>(),frozenObjects=new WeakSet<object>();
const ajv = new Ajv({ strict: true });
for (const [name, unsigned] of [['int64', false], ['uint63', true]] as const) {
  ajv.addFormat(name, (value: string) => /^(0|-?[1-9][0-9]*)$/.test(value) &&
    BigInt(value) >= (unsigned ? 0n : -9223372036854775808n) && BigInt(value) <= 9223372036854775807n);
}
const validate = ajv.compile(schema.$defs.WireMessage ? { ...schema, $ref: '#/$defs/WireMessage' } : schema);
const validatePreview=ajv.compile({...schema,$id:undefined,$ref:'#/$defs/SpatialPreview'});
const validateCorner=ajv.compile({...schema,$id:undefined,$ref:'#/$defs/CornerTerrainPreview'});
export function parseCornerPreview(value:unknown):CornerTerrainPreview {
  if(!validateCorner(value))throw new Error('Invalid corner preview');
  const result=value as CornerTerrainPreview;
  if(result.cost!=='0'||(result.legal?(result.reason!==null||result.changes.length===0):(result.reason===null||result.changes.length!==0)))throw new Error('Inconsistent corner preview');
  return result;
}
export function parseSpatialPreview(value:unknown):SpatialPreview {
  if(!validatePreview(value))throw new Error('Invalid spatial preview');
  return value as SpatialPreview;
}
export function parseMessage(text: string): WireMessage {
  if (text.length > 16 * 1024 * 1024 || new TextEncoder().encode(text).byteLength > 16 * 1024 * 1024) throw new Error('Message too large');
  return parseMessageValue(JSON.parse(text));
}
export function validWireShape(value:unknown):value is WireMessage { return !!validate(value); }
export function parseMessageValue(value:unknown):WireMessage {
  if (!validate(value)) throw new Error('Invalid protocol message');
  const message=value as WireMessage;
  if(message.type==='sessionOffer'&&!supportedIdentity(message))throw new IncompatibleWorldError('Incompatible world');
  if(message.type==='snapshot'&&!validSnapshot(message))throw new IncompatibleWorldError('Incompatible world');
  if(message.type==='join'&&(!sameIdentity(message,message.snapshot)||message.worldEpoch!==message.snapshot.worldEpoch||!validSnapshot(message.snapshot)))throw new IncompatibleWorldError('Incompatible world');
  if(message.type==='snapshot')validatedReads.add(message);
  if(message.type==='join')validatedReads.add(message.snapshot);
  return value as WireMessage;
}
export function immutable<T extends object>(value: T): T {
  if(frozenObjects.has(value))return value;
  for (const child of Object.values(value)) if (child && typeof child === 'object') immutable(child);
  frozenObjects.add(value);return Object.freeze(value);
}
export function applyDelta(state: ReadState, delta: Delta): ReadState | undefined {
  if (delta.worldEpoch !== state.worldEpoch || !sameIdentity(state,delta) || delta.baseRevision !== state.revision || BigInt(delta.revision) <= BigInt(delta.baseRevision)) return;
  const terrain=isTerrainWorld(state);
  if((isReferenceWorld(state)||terrain)&&(!delta.entities||!delta.removed.entityIds))return;
  if(!isReferenceWorld(state)&&!terrain&&(delta.entities!==undefined||delta.removed.entityIds!==undefined))return;
  if(delta.entities&&(new Set(delta.entities.map(e=>e.id)).size!==delta.entities.length||
    delta.entities.some(e=>delta.removed.entityIds?.includes(e.id))))return;
  if(terrain) {
    if(!state.terrain||!delta.terrain||BigInt(delta.terrain.nextEntityId)<BigInt(state.terrain.nextEntityId)||
      BigInt(delta.terrain.geometryRevision)<BigInt(state.terrain.geometryRevision)||
      new Set(delta.terrain.cells.map(c=>c.index)).size!==delta.terrain.cells.length||
      delta.terrain.cells.some(c=>c.index>=state.terrain!.cells.length)||
      new Set(delta.terrain.occupants.map(o=>o.entityId)).size!==delta.terrain.occupants.length||
      delta.terrain.occupants.some(o=>delta.removed.entityIds?.includes(o.entityId)))return;
    const old=new Map(delta.entities!.length||delta.terrain.occupants.length?state.entities!.map(e=>[e.id,e]):[]);
    for(const e of delta.entities!) {
      const before=old.get(e.id);
      if(!before?(BigInt(e.id)<BigInt(state.terrain.nextEntityId)||e.revision!=='0'):
        (BigInt(e.revision)<BigInt(before.revision)||
          e.revision===before.revision&&JSON.stringify(e)!==JSON.stringify(before)))return;
    }
    const changedEntities=new Map(delta.entities!.map(e=>[e.id,e]));
    const oldOccupants=new Map(delta.terrain.occupants.length?state.terrain.occupants.map(o=>[o.entityId,o]):[]);
    for(const o of delta.terrain.occupants) {
      const before=oldOccupants.get(o.entityId);
      if(before&&JSON.stringify(o)!==JSON.stringify(before)&&
        (!changedEntities.has(o.entityId)||BigInt(changedEntities.get(o.entityId)!.revision)<=BigInt(old.get(o.entityId)!.revision)))return;
    }
    if((delta.terrain.cells.length||delta.terrain.occupants.length||delta.entities!.length||delta.removed.entityIds!.length||
      delta.terrain.waterLevel!==state.terrain.waterLevel||delta.terrain.constructionMode!==state.terrain.constructionMode)&&
      BigInt(delta.terrain.geometryRevision)<=BigInt(state.terrain.geometryRevision))return;
  } else if(delta.terrain!==undefined)return;
  if(terrain&&delta.terrain&&state.terrain&&delta.entities!.length===0&&delta.removed.entityIds!.length===0&&
    delta.terrain.cells.length===0&&delta.terrain.occupants.length===0&&
    delta.terrain.geometryRevision===state.terrain.geometryRevision&&delta.terrain.nextEntityId===state.terrain.nextEntityId&&
    delta.terrain.waterLevel===state.terrain.waterLevel&&delta.terrain.constructionMode===state.terrain.constructionMode&&
    delta.trains.length===0&&delta.stations.length===0&&delta.buildings.length===0&&delta.tracks.length===0&&
    delta.removed.trainIds.length===0&&delta.removed.stationIds.length===0&&delta.removed.buildingIds.length===0&&delta.removed.trackPositions.length===0) {
    // The unchanged graph was already checked before publication. Clock/run/pause
    // never clone or revalidate millions of terrain cells.
    if(!frozenObjects.has(state)||!validatedReads.has(state)){if(!validSnapshot(state as Snapshot))return;}
    if(BigInt(delta.tick)<0n||BigInt(delta.tick)>maximumGameMinute||delta.cash!=='0'||typeof delta.paused!=='boolean')return;
    const next=immutable({...state,revision:delta.revision,tick:delta.tick,paused:delta.paused,cash:delta.cash});
    validatedReads.add(next);return next;
  }
  function merge<T>(before: readonly T[], changed: readonly T[], removed: readonly string[], key: (item: T) => string): T[] {
    const items = new Map(before.map(item => [key(item), item]));
    for (const id of removed) items.delete(id);
    for (const item of changed) items.set(key(item), item);
    return [...items.values()];
  }
  const position = (p: { readonly x: number; readonly y: number; readonly z: number }) => `${p.x},${p.y},${p.z}`;
  const cellChanges=new Map(delta.terrain?.cells.map(c=>[c.index,c]));
  const terrainState=terrain?{...state.terrain!,...delta.terrain!,
    cells:state.terrain!.cells.map((c,i)=>{const change=cellChanges.get(i);return change?{ground:change.ground,surface:change.surface}:c;}),
    occupants:merge(state.terrain!.occupants,delta.terrain!.occupants,delta.removed.entityIds!,o=>o.entityId)}:undefined;
  const next={ ...state, ...(terrainState?{terrain:terrainState}:{}), revision: delta.revision, tick: delta.tick, paused: delta.paused, cash: delta.cash,
    ...(state.entities?{entities:merge(state.entities,delta.entities??[],delta.removed.entityIds??[],e=>e.id)}:{}),
    trains: merge(state.trains, delta.trains, delta.removed.trainIds, x => x.id),
    stations: merge(state.stations, delta.stations, delta.removed.stationIds, x => x.id),
    buildings: merge(state.buildings, delta.buildings, delta.removed.buildingIds, x => x.id),
    tracks: merge(state.tracks, delta.tracks, delta.removed.trackPositions.map(position), x => position(x.position)),
  } as Snapshot;
  return validSnapshot(next)?immutable(next):undefined;
}
