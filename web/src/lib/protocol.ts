import Ajv from 'ajv';
import schema from '../../../prototype/protocol/schema.json' with { type: 'json' };
import type { Delta, Snapshot, SpatialPreview, WireMessage } from '../generated/contracts.ts';
import type { ReadState } from './session-client.ts';
import { IncompatibleWorldError, sameIdentity, validSnapshot } from './world-identity.ts';
const ajv = new Ajv({ strict: true });
for (const [name, unsigned] of [['int64', false], ['uint63', true]] as const) {
  ajv.addFormat(name, (value: string) => /^(0|-?[1-9][0-9]*)$/.test(value) &&
    BigInt(value) >= (unsigned ? 0n : -9223372036854775808n) && BigInt(value) <= 9223372036854775807n);
}
const validate = ajv.compile(schema.$defs.WireMessage ? { ...schema, $ref: '#/$defs/WireMessage' } : schema);
const validatePreview=ajv.compile({...schema,$id:undefined,$ref:'#/$defs/SpatialPreview'});
export function parseSpatialPreview(value:unknown):SpatialPreview {
  if(!validatePreview(value))throw new Error('Invalid spatial preview');
  return value as SpatialPreview;
}
export function parseMessage(text: string): WireMessage {
  if (text.length > 4 * 1024 * 1024) throw new Error('Message too large');
  const value: unknown = JSON.parse(text);
  if (!validate(value)) throw new Error('Invalid protocol message');
  const message=value as WireMessage;
  if(message.type==='snapshot'&&!validSnapshot(message))throw new IncompatibleWorldError('Incompatible world');
  if(message.type==='join'&&(!sameIdentity(message,message.snapshot)||message.worldEpoch!==message.snapshot.worldEpoch||!validSnapshot(message.snapshot)))throw new IncompatibleWorldError('Incompatible world');
  return value as WireMessage;
}
export function immutable<T extends object>(value: T): T {
  for (const child of Object.values(value)) if (child && typeof child === 'object') immutable(child);
  return Object.freeze(value);
}
export function applyDelta(state: ReadState, delta: Delta): ReadState | undefined {
  if (delta.worldEpoch !== state.worldEpoch || !sameIdentity(state,delta) || delta.baseRevision !== state.revision || BigInt(delta.revision) <= BigInt(delta.baseRevision)) return;
  function merge<T>(before: readonly T[], changed: readonly T[], removed: readonly string[], key: (item: T) => string): T[] {
    const items = new Map(before.map(item => [key(item), item]));
    for (const id of removed) items.delete(id);
    for (const item of changed) items.set(key(item), item);
    return [...items.values()];
  }
  const position = (p: { readonly x: number; readonly y: number; readonly z: number }) => `${p.x},${p.y},${p.z}`;
  const next={ ...state, revision: delta.revision, tick: delta.tick, paused: delta.paused, cash: delta.cash,
    trains: merge(state.trains, delta.trains, delta.removed.trainIds, x => x.id),
    stations: merge(state.stations, delta.stations, delta.removed.stationIds, x => x.id),
    buildings: merge(state.buildings, delta.buildings, delta.removed.buildingIds, x => x.id),
    tracks: merge(state.tracks, delta.tracks, delta.removed.trackPositions.map(position), x => position(x.position)),
  } as Snapshot;
  return validSnapshot(next)?immutable(next):undefined;
}
