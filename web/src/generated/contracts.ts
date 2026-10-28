/* Generated from prototype/protocol/schema.json. Run deno task generate:contracts. */

export type GameDocument = WireMessage | SpatialQuery | SpatialPreview;
export type WireMessage =
  Join | Command | Accepted | Rejected | Snapshot | Delta | Resync | Heartbeat | InputReset | SoundEvent;
export type Counter = string;
export type Money = string;
export type Heading = number;
export type EntityId = string;
export type CommandPayload =
  | {
      kind: "run";
    }
  | {
      kind: "pause";
    }
  | {
      kind: "step";
    }
  | {
      kind: "renameStation";
      stationId: string;
      expectedRevision: Counter;
      name: string;
    }
  | {
      kind: "buildTrack";
      position: Position;
      shape:
        | "eastWest"
        | "northSouth"
        | "curveSE"
        | "curveSW"
        | "curveNE"
        | "curveNW"
        | "junctionE"
        | "buffer"
        | "normal0"
        | "normal1"
        | "normal2"
        | "normal3"
        | "normal4"
        | "normal5"
        | "normal6"
        | "normal7"
        | "normal8"
        | "normal9"
        | "normal10"
        | "normal11";
    }
  | {
      kind: "removeTrack";
      position: Position;
      expectedRevision: Counter;
    }
  | {
      kind: "setController";
      trainId: string;
      expectedRevision: Counter;
      controller: "automatic" | "manual";
      leaseId: string;
    }
  | {
      kind: "controllerInput";
      trainId: string;
      inputSequence: Counter;
      pass: boolean;
      depart: boolean;
      reverse: boolean;
      left: boolean;
      right: boolean;
      reset: boolean;
      leaseId: string;
    }
  | {
      kind: "acquireControl";
      trainId: string;
    }
  | {
      kind: "releaseControl";
      trainId: string;
      leaseId: string;
    };

export interface Join {
  type: "join";
  protocolVersion: 4;
  contentVersion: string;
  serverIncarnation: string;
  sessionId: string;
  participantId: string;
  worldEpoch: Counter;
  snapshot: Snapshot;
  role: "owner" | "operator" | "observer";
  ruleset: string;
  /**
   * @maxItems 32
   */
  requiredPacks: RequiredPack[];
}
export interface Snapshot {
  type: "snapshot";
  worldEpoch: Counter;
  revision: Counter;
  tick: Counter;
  paused: boolean;
  cash: Money;
  map: Map;
  trains: Train[];
  stations: Station[];
  buildings: Building[];
  tracks: Track[];
  ruleset: string;
  contentVersion: string;
  /**
   * @maxItems 32
   */
  requiredPacks: RequiredPack[];
  /**
   * @maxItems 65536
   */
  entities?: SpatialEntity[];
  terrain?: Terrain;
}
export interface Map {
  width: number;
  height: number;
  levels: number;
  coordinateSystem: "hv" | "prototypeXY";
}
export interface Train {
  id: string;
  revision: Counter;
  name: string;
  position: Position;
  cars: number;
  controller: "automatic" | "manual";
  /**
   * @minItems 1
   */
  carPositions: [Position, ...Position[]];
  state: "moving" | "station" | "buffer" | "blocked";
  controlLease: ControlLease | null;
  heading: Heading;
  /**
   * @minItems 1
   */
  carHeadings: [Heading, ...Heading[]];
}
export interface Position {
  x: number;
  y: number;
  z: number;
}
export interface ControlLease {
  participantId: string;
  leaseId: string;
  expiresAt: Counter;
}
export interface Station {
  id: string;
  revision: Counter;
  name: string;
  position: Position;
}
export interface Building {
  id: string;
  definitionId: string;
  position: Position;
  height: number;
}
export interface Track {
  position: Position;
  shape:
    | "eastWest"
    | "northSouth"
    | "curveSE"
    | "curveSW"
    | "curveNE"
    | "curveNW"
    | "junctionE"
    | "buffer"
    | "normal0"
    | "normal1"
    | "normal2"
    | "normal3"
    | "normal4"
    | "normal5"
    | "normal6"
    | "normal7"
    | "normal8"
    | "normal9"
    | "normal10"
    | "normal11";
  revision: Counter;
  entityId?: EntityId;
}
export interface RequiredPack {
  id: string;
  version: string;
  catalogHash: string;
}
export interface SpatialEntity {
  id: EntityId;
  revision: Counter;
  definition: Definition;
  anchor: Position;
  /**
   * @minItems 1
   * @maxItems 65536
   */
  footprint: [VoxelMember, ...VoxelMember[]];
  owned: boolean;
  silentlyReclaimable: boolean;
  value: Money;
}
export interface Definition {
  packId: string;
  contributionId: string;
  recipeId: string;
}
export interface VoxelMember {
  offset: Position;
  transparent: boolean;
}
export interface Terrain {
  name: string;
  waterLevel: number;
  constructionMode: boolean;
  geometryRevision: Counter;
  nextEntityId: EntityId;
  provenance: TerrainProvenance;
  /**
   * @maxItems 16384
   */
  cells: TerrainCell[];
  /**
   * @maxItems 32768
   */
  occupants: TerrainOccupant[];
}
export interface TerrainProvenance {
  profile: "terrain-generation-1";
  source: "empty" | "heightmap" | "generated";
  requestHash: string;
  requestJson: string;
}
export interface TerrainCell {
  ground: number;
  surface: "natural" | "cut" | "terrace";
}
export interface TerrainOccupant {
  entityId: EntityId;
  kind: "mountain" | "terrace" | "creek";
  ne: number;
  se: number;
  sw: number;
  nw: number;
  forest: Forest | null;
  creekConnections: number;
}
export interface Forest {
  definition: Definition;
  /**
   * @maxItems 6
   */
  trees:
    | []
    | [TerrainTree]
    | [TerrainTree, TerrainTree]
    | [TerrainTree, TerrainTree, TerrainTree]
    | [TerrainTree, TerrainTree, TerrainTree, TerrainTree]
    | [TerrainTree, TerrainTree, TerrainTree, TerrainTree, TerrainTree]
    | [TerrainTree, TerrainTree, TerrainTree, TerrainTree, TerrainTree, TerrainTree];
}
export interface TerrainTree {
  x: number;
  y: number;
  sprite: number;
}
export interface Command {
  type: "command";
  requestId: string;
  expectedWorldEpoch: Counter;
  payload: CommandPayload;
}
export interface Accepted {
  type: "accepted";
  requestId: string;
  worldEpoch: Counter;
  sequence: Counter;
  tick: Counter;
  revision: Counter;
}
export interface Rejected {
  type: "rejected";
  requestId: string;
  code:
    | "invalidPlacement"
    | "staleEpoch"
    | "staleRevision"
    | "insufficientFunds"
    | "notAuthorized"
    | "queueFull"
    | "invalidInput"
    | "requestConflict"
    | "retryWindowFull"
    | "leaseRequired"
    | "controlBusy"
    | "operationUnavailable";
  arguments: {
    [k: string]: string;
  };
}
export interface Delta {
  type: "delta";
  worldEpoch: Counter;
  baseRevision: Counter;
  revision: Counter;
  tick: Counter;
  trains: Train[];
  stations: Station[];
  buildings: Building[];
  removed: Removed;
  paused: boolean;
  cash: Money;
  tracks: Track[];
  ruleset: string;
  contentVersion: string;
  /**
   * @maxItems 32
   */
  requiredPacks: RequiredPack[];
  /**
   * @maxItems 65536
   */
  entities?: SpatialEntity[];
  terrain?: TerrainDelta;
}
export interface Removed {
  trainIds: string[];
  stationIds: string[];
  buildingIds: string[];
  trackPositions: Position[];
  entityIds?: EntityId[];
}
export interface TerrainDelta {
  waterLevel: number;
  constructionMode: boolean;
  geometryRevision: Counter;
  nextEntityId: EntityId;
  /**
   * @maxItems 16384
   */
  cells: TerrainCellChange[];
  /**
   * @maxItems 32768
   */
  occupants: TerrainOccupant[];
}
export interface TerrainCellChange {
  index: number;
  ground: number;
  surface: "natural" | "cut" | "terrace";
}
export interface Resync {
  type: "resync";
}
export interface Heartbeat {
  type: "heartbeat";
}
export interface InputReset {
  type: "inputReset";
}
export interface SoundEvent {
  type: "soundEvent";
  eventId: string;
  worldEpoch: Counter;
  kind: "stationArrived";
}
export interface SpatialQuery {
  expectedWorldEpoch: Counter;
  position: Position;
  anchor: Position | null;
  snap: 0 | 4 | 8;
}
export interface SpatialPreview {
  worldEpoch: Counter;
  revision: Counter;
  ruleset: string;
  contentVersion: string;
  /**
   * @maxItems 32
   */
  requiredPacks: RequiredPack[];
  position: Position;
  inside: boolean;
  projected: ProjectedPoint;
  storage: StoragePosition | null;
  heading: Heading | null;
  distance: number | null;
}
export interface ProjectedPoint {
  a: number;
  b: number;
}
export interface StoragePosition {
  h: number;
  v: number;
  z: number;
}
