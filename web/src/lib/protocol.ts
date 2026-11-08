import {validStationSuccessor} from './station-state.ts';
import {validTrainMotion} from './train-motion-contract.ts';
import {validFleetSuccessor} from './fleet-state.ts';
import Ajv from 'ajv';
import schema from '../../../prototype/protocol/schema.json' with { type: 'json' };
import type { Delta, Snapshot, SpatialPreview, CornerTerrainPreview, WireMessage } from '../generated/contracts.ts';
import type { ReadState } from './session-client.ts';
import { IncompatibleWorldError, isReferenceWorld, isTerrainWorld, isMixedWorld, sameIdentity, supportedIdentity, validSnapshot, shareSnapshotValidation } from './world-identity.ts';
import { maximumGameMinute } from './game-time.ts';
import { terrainById, validTerrainDelta, shareTerrainValidation } from './terrain-state.ts';
const validatedReads=new WeakSet<object>(),frozenObjects=new WeakSet<object>();
const ajv = new Ajv({ strict: true });
for (const [name, unsigned] of [['int64', false], ['uint63', true]] as const) {
  ajv.addFormat(name, (value: string) => /^(0|-?[1-9][0-9]*)$/.test(value) &&
    BigInt(value) >= (unsigned ? 0n : -9223372036854775808n) && BigInt(value) <= 9223372036854775807n);
}
const validate = ajv.compile(schema.$defs.WireMessage ? { ...schema, $ref: '#/$defs/WireMessage' } : schema);
// Compile a focused diagnostic only on rejection. Report schema paths/reasons,
// never message contents, credentials or the saved world.
const diagnosticValidators=new Map<string,ReturnType<typeof ajv.compile>>();
const diagnosticDefinitions=new Map([['snapshot','Snapshot'],['delta','Delta'],['join','Join'],['stateChunk','StateChunk'],['accepted','Accepted'],['rejected','Rejected']]);
function invalidMessage(value:unknown):string {
  const type=value&&typeof value==='object'&&'type' in value?value.type:undefined;
  const definition=typeof type==='string'?diagnosticDefinitions.get(type):undefined;
  if(!definition)return 'Invalid protocol message';
  let check=diagnosticValidators.get(definition);
  if(!check){check=ajv.compile({...schema,$id:undefined,$ref:`#/$defs/${definition}`});diagnosticValidators.set(definition,check);}
  check(value);
  const details=check.errors?.slice(0,3).map(e=>`${e.instancePath||'/'} ${e.message}`).join('; ');
  return `Invalid protocol message (${type})${details?': '+details:''}`;
}
const validatePreview=ajv.compile({...schema,$id:undefined,$ref:'#/$defs/SpatialPreview'});
const validateCorner=ajv.compile({...schema,$id:undefined,$ref:'#/$defs/CornerTerrainPreview'});
const validateSlope=ajv.compile({...schema,$id:undefined,$ref:'#/$defs/SlopePreview'});
const validateRemoval=ajv.compile({...schema,$id:undefined,$ref:'#/$defs/RemovalPreview'});
export function parseRemovalPreview(value:unknown):import('../generated/contracts.ts').RemovalPreview {
  if(!validateRemoval(value))throw new Error('Invalid removal preview');
  const p=value as import('../generated/contracts.ts').RemovalPreview;
  if(p.legal ? p.reason!==null||p.positions.length===0 : p.reason===null||p.positions.length!==0)throw new Error('Inconsistent removal preview');
  if(new Set(p.positions.map(p=>`${p.x},${p.y},${p.z}`)).size!==p.positions.length)throw new Error('Duplicate removal positions');
  if(new Set(p.path.map(p=>`${p.x},${p.y},${p.z}`)).size!==p.path.length)throw new Error('Duplicate removal path');
  if((p.objectKind==='line')!==!!p.query.end||p.legal&&p.path.length===0)throw new Error('Inconsistent removal mode');
  if(p.query.end ? p.query.expectedEntityId!=='0'||p.query.expectedEntityRevision!=='0' : p.query.expectedEntityId==='0')throw new Error('Invalid removal identity');
  return p;
}
export function parseSlopePreview(value:unknown):import('../generated/contracts.ts').SlopePreview {
  if(!validateSlope(value))throw new Error('Invalid slope preview');
  const p=value as import('../generated/contracts.ts').SlopePreview;
  const unique=new Set(p.members.map(m=>JSON.stringify(m.position)));
  if(BigInt(p.cost)<0n || (p.legal ? p.reason!==null||p.members.length!==8||unique.size!==8||p.rails.length!==4 : p.reason===null||p.cost!=='0'))throw new Error('Inconsistent slope preview');
  return p;
}
const validateFleetDeployment=ajv.compile({...schema,$id:undefined,$ref:'#/$defs/FleetDeploymentPreview'});
export function parseFleetDeploymentPreview(value:unknown):import('../generated/contracts.ts').FleetDeploymentPreview {
  if(!validateFleetDeployment(value))throw new Error('Invalid deployment preview');
  const p=value as import('../generated/contracts.ts').FleetDeploymentPreview;
  if(p.legal ? p.reason!==null||p.query.action==='place'&&!p.cars.length||p.query.action==='withdraw'&&!!p.cars.length : p.reason===null||!!p.cars.length)throw new Error('Inconsistent deployment preview');
  if(new Set(p.cars.map(c=>c.carId)).size!==p.cars.length||new Set(p.cars.map(c=>JSON.stringify(c.location))).size!==p.cars.length)throw new Error('Duplicate deployment pose');
  return p;
}
const validateFleetTrade=ajv.compile({...schema,$id:undefined,$ref:'#/$defs/FleetTradePreview'});
export function parseFleetTradePreview(value:unknown):import('../generated/contracts.ts').FleetTradePreview {
  if(!validateFleetTrade(value))throw new Error('Invalid fleet quote');
  const p=value as import('../generated/contracts.ts').FleetTradePreview;
  if([p.price,p.normalCapacity,p.seatedCapacity,p.packedCapacity].some(n=>BigInt(n)<0n)||BigInt(p.seatedCapacity)>BigInt(p.normalCapacity)||BigInt(p.packedCapacity)!==2n*BigInt(p.normalCapacity)-BigInt(p.seatedCapacity))throw new Error('Inconsistent fleet quote');
  return p;
}
const validateRailway=ajv.compile({...schema,$id:undefined,$ref:'#/$defs/RailwayPreview'});
export function parseRailwayPreview(value:unknown):import('../generated/contracts.ts').RailwayPreview {
  if(!validateRailway(value))throw new Error('Invalid railway preview');
  const result=value as import('../generated/contracts.ts').RailwayPreview;
  if(BigInt(result.cost)<0n || (result.legal ? result.reason!==null||(result.query.infrastructure==='speed'&&result.query.speedSlope?result.cells.length!==4:result.query.infrastructure==='stationary'?result.cells.length<1:['signal','pole'].includes(result.query.infrastructure??'')?result.cells.length!==1:result.cells.length<2) : result.reason===null||result.cost!=='0'))throw new Error('Inconsistent railway preview');
  return result;
}
export function parseCornerPreview(value:unknown):CornerTerrainPreview {
  if(!validateCorner(value))throw new Error('Invalid corner preview');
  const result=value as CornerTerrainPreview;
  if(result.cost!=='0'||(result.legal?(result.reason!==null||result.changes.length===0&&result.action!=='plantForest'):(result.reason===null||result.changes.length!==0)))throw new Error('Inconsistent corner preview');
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
  if (!validate(value)) throw new Error(invalidMessage(value));
  const message=value as WireMessage;
  const motionRead=message.type==='join'?message.snapshot:message;
  if((motionRead.type==='snapshot'||motionRead.type==='delta')&&!validTrainMotion(motionRead.trainMotion,motionRead.tick))throw new Error(`Invalid train motion history (${motionRead.type})`);
  if(message.type==='sessionOffer'&&!supportedIdentity(message))throw new IncompatibleWorldError('Incompatible world');
  if(message.type==='snapshot'&&(!validSnapshot(message)))throw new IncompatibleWorldError('Incompatible world');
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
  if(!validTrainMotion(delta.trainMotion,delta.tick))return;
  const mixed=isMixedWorld(state);
  const terrain=isTerrainWorld(state)||mixed;
  if(mixed!==!!delta.mixed||mixed&&BigInt(delta.tick)<BigInt(state.tick))return;
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
    const old={get:(id:string)=>terrainById(state.entities!,id,e=>e.id)};
    for(const e of delta.entities!) {
      const before=old.get(e.id);
      if(!before?(BigInt(e.id)<BigInt(state.terrain.nextEntityId)||e.revision!=='0'):
        (BigInt(e.revision)<BigInt(before.revision)||
          e.revision===before.revision&&JSON.stringify(e)!==JSON.stringify(before)))return;
    }
    const changedEntities=new Map(delta.entities!.map(e=>[e.id,e]));
    const oldOccupants={get:(id:string)=>terrainById(state.terrain!.occupants,id,o=>o.entityId)};
    for(const o of delta.terrain.occupants) {
      const before=oldOccupants.get(o.entityId);
      if(before&&JSON.stringify(o)!==JSON.stringify(before)&&
        (!changedEntities.has(o.entityId)||BigInt(changedEntities.get(o.entityId)!.revision)<=BigInt(old.get(o.entityId)!.revision)))return;
    }
    if(!mixed&&(delta.terrain.cells.length||delta.terrain.occupants.length||delta.entities!.length||delta.removed.entityIds!.length||
      delta.terrain.waterLevel!==state.terrain.waterLevel||delta.terrain.constructionMode!==state.terrain.constructionMode)&&
      BigInt(delta.terrain.geometryRevision)<=BigInt(state.terrain.geometryRevision))return;
  } else if(delta.terrain!==undefined)return;
  if((!mixed||JSON.stringify(delta.mixed)===JSON.stringify(state.mixed))&&terrain&&delta.terrain&&state.terrain&&delta.entities!.length===0&&delta.removed.entityIds!.length===0&&
    delta.terrain.cells.length===0&&delta.terrain.occupants.length===0&&
    delta.terrain.geometryRevision===state.terrain.geometryRevision&&delta.terrain.nextEntityId===state.terrain.nextEntityId&&
    delta.terrain.waterLevel===state.terrain.waterLevel&&delta.terrain.constructionMode===state.terrain.constructionMode&&
    delta.trains.length===0&&delta.stations.length===0&&delta.buildings.length===0&&delta.tracks.length===0&&
    delta.removed.trainIds.length===0&&delta.removed.stationIds.length===0&&delta.removed.buildingIds.length===0&&delta.removed.trackPositions.length===0) {
    // The unchanged graph was already checked before publication. Clock/run/pause
    // never clone or revalidate millions of terrain cells.
    if(!frozenObjects.has(state)||!validatedReads.has(state)){if(!validSnapshot(state as Snapshot))return;}
    if(BigInt(delta.tick)<0n||BigInt(delta.tick)>maximumGameMinute||delta.cash!==(mixed?state.cash:'0')||typeof delta.paused!=='boolean')return;
    if(mixed&&(JSON.stringify(delta.mixed)!==JSON.stringify(state.mixed)||
      state.mixed?.queue?.pending.some(a=>BigInt(a.dueMinute)<=BigInt(delta.tick))))return;
    const next=immutable({...state,revision:delta.revision,tick:delta.tick,paused:delta.paused,cash:delta.cash,trainMotion:delta.trainMotion,...(delta.playbackRate !== undefined || state.playbackRate !== undefined ? {playbackRate:delta.playbackRate ?? 1} : {})});
    shareTerrainValidation(state,next);shareSnapshotValidation(state,next);validatedReads.add(next);return next;
  }
  function merge<T>(before: readonly T[], changed: readonly T[], removed: readonly string[], key: (item: T) => string): T[] {
    if(!changed.length&&!removed.length)return before as T[];
    const items = new Map(before.map(item => [key(item), item]));
    for (const id of removed) items.delete(id);
    for (const item of changed) items.set(key(item), item);
    return [...items.values()];
  }
  const position = (p: { readonly x: number; readonly y: number; readonly z: number }) => `${p.x},${p.y},${p.z}`;
  // Only changed records need recursive freezing. All retained records came
  // from the immutable validated predecessor.
  if(terrain)immutable(delta);
  function terrainMerge<T>(before:readonly T[],changed:readonly T[],removed:readonly string[],key:(item:T)=>string):T[] {
    if(!changed.length&&!removed.length)return before as T[];
    const changes=new Map(changed.map(item=>[key(item),item])),deleted=new Set(removed),result:T[]=[];
    for(const item of before){const id=key(item);if(!deleted.has(id))result.push(changes.get(id)??item);changes.delete(id);}
    result.push(...[...changes.values()].sort((a,b)=>{const x=key(a),y=key(b);return x.length-y.length||(x<y?-1:x>y?1:0);}));
    Object.freeze(result);frozenObjects.add(result);return result;
  }
  const terrainCells=terrain&&delta.terrain!.cells.length?state.terrain!.cells.slice():undefined;
  if(terrainCells){for(const c of delta.terrain!.cells)terrainCells[c.index]=immutable({ground:c.ground,surface:c.surface});Object.freeze(terrainCells);frozenObjects.add(terrainCells);}

  const terrainState=terrain?{...state.terrain!,...delta.terrain!,
    cells:delta.terrain!.cells.length?terrainCells!:state.terrain!.cells,
    occupants:terrainMerge(state.terrain!.occupants,delta.terrain!.occupants,delta.removed.entityIds!,o=>o.entityId)}:undefined;
  const next={ ...state, ...(mixed?{mixed:delta.mixed}:{}), ...(terrainState?{terrain:terrainState}:{}), revision: delta.revision, tick: delta.tick, paused: delta.paused, cash: delta.cash, trainMotion:delta.trainMotion, ...(delta.playbackRate !== undefined || state.playbackRate !== undefined ? {playbackRate:delta.playbackRate ?? 1} : {}),
    ...(state.entities?{entities:(terrain?terrainMerge:merge)(state.entities,delta.entities??[],delta.removed.entityIds??[],e=>e.id)}:{}),
    trains: merge(state.trains, delta.trains, delta.removed.trainIds, x => x.id),
    stations: merge(state.stations, delta.stations, delta.removed.stationIds, x => x.id),
    buildings: merge(state.buildings, delta.buildings, delta.removed.buildingIds, x => x.id),
    tracks: merge(state.tracks, delta.tracks, delta.removed.trackPositions.map(position), x => position(x.position)),
  } as Snapshot;
  if(mixed) {
    if(!validSnapshot(next,state as Snapshot)||!state.mixed||!next.mixed)return;
    const before=state.mixed,after=next.mixed;
    if(!validStationSuccessor(before.stationGraph,after.stationGraph,before.nextEntityId,state as Snapshot,next)||
      !validFleetSuccessor(before.fleet,after.fleet,before.nextEntityId)||
      before.queue&&after.queue&&BigInt(after.queue.nextOrder)<BigInt(before.queue.nextOrder))return;
    if(BigInt(after.nextEntityId)<BigInt(before.nextEntityId)||BigInt(after.geometryRevision)<BigInt(before.geometryRevision))return;
    if(after.accounts.initialCash!==before.accounts.initialCash||after.accounts.bankruptcyIncrement!==before.accounts.bankruptcyIncrement||
      after.accounts.transactions.length<before.accounts.transactions.length||before.accounts.transactions.some((t,i)=>JSON.stringify(t)!==JSON.stringify(after.accounts.transactions[i])))return;
    const old=new Map(state.entities!.map(e=>[e.id,e]));
    const terrainIds=new Set(state.terrain!.occupants.map(o=>o.entityId)),nextTerrainIds=new Set(next.terrain!.occupants.map(o=>o.entityId));
    for(const e of delta.entities!){const b=old.get(e.id);
      if(!b&&BigInt(e.id)<BigInt(before.nextEntityId)||b&&(terrainIds.has(e.id)!==nextTerrainIds.has(e.id)||
        !terrainIds.has(e.id)&&JSON.stringify(b.definition)!==JSON.stringify(e.definition)))return;
    }
    const terrainChanged=delta.terrain!.cells.length||delta.terrain!.occupants.length||
      delta.entities!.some(e=>terrainIds.has(e.id)||nextTerrainIds.has(e.id))||delta.removed.entityIds!.some(id=>terrainIds.has(id))||
      delta.terrain!.waterLevel!==state.terrain!.waterLevel;
    if(terrainChanged&&BigInt(delta.terrain!.geometryRevision)<=BigInt(state.terrain!.geometryRevision))return;
    if((JSON.stringify({...before,queue:undefined})!==JSON.stringify({...after,queue:undefined})||terrainChanged||delta.entities!.length||delta.removed.entityIds!.length||
        delta.terrain!.constructionMode!==state.terrain!.constructionMode)&&BigInt(after.geometryRevision)<=BigInt(before.geometryRevision))return;
    const priorVoxels=new Map<string,unknown[]>(),nowVoxels=new Map<string,unknown[]>();
    for(const [rows,index] of [[before.voxels,priorVoxels],[after.voxels,nowVoxels]] as const)
      for(const v of rows){const group=index.get(v.ownerId)??[];group.push(v);index.set(v.ownerId,group);}
    // Payload changes require a revised owner even when its footprint is unchanged.
    for(const e of next.entities!){const b=old.get(e.id);if(!b||e.revision!==b.revision)continue;
      const prior=priorVoxels.get(e.id)??[],now=nowVoxels.get(e.id)??[];
      if(JSON.stringify(prior)!==JSON.stringify(now)||JSON.stringify(before.slopes.find(s=>s.entityId===e.id))!==JSON.stringify(after.slopes.find(s=>s.entityId===e.id)))return;
    }
    const frozen=immutable(next);validatedReads.add(frozen);return frozen;
  }
  if(terrain) {
    if(!frozenObjects.has(state)||!validatedReads.has(state)){if(!validSnapshot(state as Snapshot))return;}
    try {if(!validTerrainDelta(state as Snapshot,next,delta))return;}catch{return;}
    const frozen=immutable(next);validatedReads.add(frozen);return frozen;
  }
  return validSnapshot(next)?immutable(next):undefined;
}

const validateStationBuilding=ajv.compile({...schema,$id:undefined,$ref:'#/$defs/StationBuildingPreview'});
export function parseStationBuildingPreview(value:unknown):import('../generated/contracts.ts').StationBuildingPreview {
  if(!validateStationBuilding(value))throw new Error('Invalid station preview');
  const p=value as import('../generated/contracts.ts').StationBuildingPreview;
  if(BigInt(p.cost)<0n || (p.legal ? p.reason!==null||!p.positions.length : p.reason===null||p.cost!=='0'))throw new Error('Inconsistent station preview');
  if(new Set(p.positions.map(p=>`${p.x},${p.y},${p.z}`)).size!==p.positions.length)throw new Error('Duplicate station positions');
  return p;
}

const validateThinPlatform=ajv.compile({...schema,$id:undefined,$ref:'#/$defs/ThinPlatformPreview'});
export function parseThinPlatformPreview(value:unknown):import('../generated/contracts.ts').ThinPlatformPreview {
  if(!validateThinPlatform(value))throw new Error('Invalid station preview');
  const p=value as import('../generated/contracts.ts').ThinPlatformPreview;
  if(BigInt(p.cost)<0n || (p.legal ? p.reason!==null||!p.positions.length : p.reason===null||p.cost!=='0'))throw new Error('Inconsistent station preview');
  if(new Set(p.positions.map(p=>`${p.x},${p.y},${p.z}`)).size!==p.positions.length)throw new Error('Duplicate station positions');
  const q=p.query;
  if(q.action==='place'?(q.platformId!=='0'||q.expectedPlatformRevision!=='0'):q.platformId==='0')throw new Error('Invalid platform identity');
  if(p.legal&&q.action==='place'){
    const dx=q.direction===2?1:q.direction===6?-1:0,dy=q.direction===4?1:q.direction===0?-1:0;
    if(p.positions.length!==q.length||p.positions.some((v,i)=>v.x!==q.position.x+dx*i||v.y!==q.position.y+dy*i||v.z!==q.position.z))throw new Error('Invalid platform geometry');
  }
  return p;
}

const validateWidePlatform=ajv.compile({...schema,$id:undefined,$ref:'#/$defs/WidePlatformPreview'});
export function parseWidePlatformPreview(value:unknown):import('../generated/contracts.ts').WidePlatformPreview {
  if(!validateWidePlatform(value))throw new Error('Invalid station preview');
  const p=value as import('../generated/contracts.ts').WidePlatformPreview;
  if(BigInt(p.cost)<0n || (p.legal ? p.reason!==null||!p.positions.length : p.reason===null||p.cost!=='0'))throw new Error('Inconsistent station preview');
  if(new Set(p.positions.map(p=>`${p.x},${p.y},${p.z}`)).size!==p.positions.length)throw new Error('Duplicate station positions');
  const q=p.query;
  if(q.action==='place'?(q.platformId!=='0'||q.expectedPlatformRevision!=='0'):q.platformId==='0')throw new Error('Invalid platform identity');
  if(p.legal&&q.action==='place'){
    const dx=q.direction===2?1:q.direction===6?-1:0,dy=q.direction===4?1:q.direction===0?-1:0;
    const expected=Array.from({length:q.secondLane?3:2},(_,lane)=>Array.from({length:q.length},(_,i)=>({x:q.position.x+dx*i-dy*(lane===2?-1:lane),y:q.position.y+dy*i+dx*(lane===2?-1:lane),z:q.position.z}))).flat();
    if(p.positions.length!==expected.length||p.positions.some((v,i)=>v.x!==expected[i].x||v.y!==expected[i].y||v.z!==expected[i].z))throw new Error('Invalid platform geometry');
  }
  return p;
}
