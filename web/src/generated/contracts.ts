/* Generated from prototype/protocol/schema.json. Run deno task generate:contracts. */

export type GameDocument =
  | WireMessage
  | SpatialQuery
  | SpatialPreview
  | CornerTerrainQuery
  | CornerTerrainPreview
  | RailwayQuery
  | RailwayPreview
  | SlopeQuery
  | SlopePreview
  | RemovalQuery
  | RemovalPreview;
export type WireMessage =
  | Join
  | Command
  | Accepted
  | Rejected
  | Snapshot
  | Delta
  | Resync
  | Heartbeat
  | InputReset
  | SoundEvent
  | StateChunk
  | SessionOffer;
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
    }
  | {
      kind: "editCorner";
      query: CornerTerrainQuery;
    }
  | {
      kind: "setAuthoringMode";
      expectedGeometryRevision: Counter;
      enabled: boolean;
    }
  | {
      kind: "editRailway";
      query: RailwayQuery;
    }
  | {
      kind: "editSlope";
      query: SlopeQuery;
    }
  | {
      kind: "removeObject";
      query: RemovalQuery;
    };

export interface Join {
  type: "join";
  protocolVersion: 6;
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
   * @maxItems 3400000
   */
  entities?: SpatialEntity[];
  terrain?: Terrain;
  mixed?: Mixed;
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
   * @maxItems 1700000
   */
  cells: TerrainCell[];
  /**
   * @maxItems 3400000
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
export interface Mixed {
  geometryRevision: Counter;
  nextEntityId: EntityId;
  /**
   * @maxItems 8192
   */
  voxels: RailwayVoxel[];
  /**
   * @maxItems 1024
   */
  slopes: SlopeOwner[];
  accounts: ConstructionAccounts;
}
export interface RailwayVoxel {
  position: Position;
  ownerId: EntityId;
  kind: "traffic" | "slopefiller" | "slopesupport" | "slopewall" | "pier";
  traffic: Traffic | null;
  definition: Definition | null;
  sprite: number;
  drawGroundBelow: boolean;
  drawGroundAbove: boolean;
  supportedOwnerId: EntityId | null;
}
export interface Traffic {
  rail: RailComponent | null;
  road: RoadComponent | null;
  accessory: AccessoryComponent | null;
  car: CarComponent | null;
  platform: Definition | null;
}
export interface RailComponent {
  definition: Definition;
  pattern: number;
  variant: number;
  terrainBackup: Corners | null;
  direction: number;
  controllerId: EntityId | null;
}
export interface Corners {
  ne: number;
  se: number;
  sw: number;
  nw: number;
}
export interface RoadComponent {
  definition: Definition;
  edges: number;
}
export interface AccessoryComponent {
  definition: Definition;
  direction: number;
}
export interface CarComponent {
  definition: Definition;
  trainId: EntityId;
  carId: EntityId;
}
export interface SlopeOwner {
  entityId: EntityId;
  climb: 0 | 2 | 4 | 6;
}
export interface ConstructionAccounts {
  initialCash: Money;
  bankruptcyIncrement: EntityId;
  /**
   * @maxItems 8192
   */
  transactions: AccountTransaction[];
}
export interface AccountTransaction {
  sequence: EntityId;
  worldRevision: EntityId;
  absoluteMinute: Counter;
  operation: string;
  genre:
    | "{AC30BB0B-044C-4784-A83D-FCB1F60B3CF2}"
    | "{CC00A6D1-D078-4D3C-AFB2-EDC6CB9F4CB3}"
    | "{2A666F1A-9F40-4F67-98F4-DEAC1E55296D}"
    | "{C0A9ABA5-801F-4AA6-93EA-6FF563C2B407}";
  expenditure: Counter;
  topUps: Counter;
  cashAfter: Counter;
}
export interface Command {
  type: "command";
  requestId: string;
  expectedWorldEpoch: Counter;
  payload: CommandPayload;
}
export interface CornerTerrainQuery {
  expectedWorldEpoch: Counter;
  expectedGeometryRevision: Counter;
  position: Position;
  action:
    | "raise"
    | "lower"
    | "repair"
    | "placeTerrace"
    | "removeTerrace"
    | "placeCut"
    | "removeCut"
    | "plantForest"
    | "placeCreek";
  offsetX: number | null;
  offsetY: number | null;
  stroke: CornerTerrainSite[] | null;
  rectangle?: RectangleTerrainSelection | null;
  forest?: Definition | null;
}
export interface CornerTerrainSite {
  position: Position;
  action: "raise" | "lower";
  offsetX: number | null;
  offsetY: number | null;
}
export interface RectangleTerrainSelection {
  end: Position;
  soft: boolean;
}
export interface RailwayQuery {
  expectedWorldEpoch: Counter;
  expectedGeometryRevision: Counter;
  expectedContentVersion: "terrain-content-1" | "mixed-railway-content-1";
  to: Position;
  action: "lay" | "remove";
  routing: "closest" | "source" | "alternate";
  from: Position;
  poleType?:
    | "{DDF7F835-8100-45fd-8AE7-EC498796D387}"
    | "{DD7D4548-5E0E-48e4-AB6E-64A443C240F7}"
    | "{86E56A60-19DF-4ba7-BFE8-ABFBDF5DFA0A}"
    | "{FD1635C1-05BF-41af-9185-4891B983E2A4}";
  signalType?: "{5349DE2A-DF3E-49B4-87A0-973105B7E6A4}" | "{6FA55842-E693-414f-A120-0A3D0C613B21}";
  signalDirection?: 0 | 2 | 4 | 6;
  tunnelRouting?: "exact" | "portals" | "portals-auto";
  infrastructure?: "normal" | "bridge" | "girder" | "tunnel" | "garage" | "signal" | "pole";
}
export interface SlopeQuery {
  expectedWorldEpoch: Counter;
  expectedGeometryRevision: Counter;
  expectedContentVersion: "terrain-content-1" | "mixed-railway-content-1";
  action: "lay" | "remove";
  position: Position;
  climb: 0 | 2 | 4 | 6;
}
export interface RemovalQuery {
  expectedWorldEpoch: Counter;
  expectedGeometryRevision: Counter;
  expectedContentVersion: "terrain-content-1" | "mixed-railway-content-1";
  position: Position;
  expectedEntityId: Counter;
  expectedEntityRevision: Counter;
  end?: Position | null;
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
   * @maxItems 3400000
   */
  entities?: SpatialEntity[];
  terrain?: TerrainDelta;
  mixed?: Mixed;
}
export interface Removed {
  trainIds: string[];
  stationIds: string[];
  buildingIds: string[];
  trackPositions: Position[];
  /**
   * @maxItems 3400000
   */
  entityIds?: EntityId[];
}
export interface TerrainDelta {
  waterLevel: number;
  constructionMode: boolean;
  geometryRevision: Counter;
  nextEntityId: EntityId;
  /**
   * @maxItems 1700000
   */
  cells: TerrainCellChange[];
  /**
   * @maxItems 3400000
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
export interface StateChunk {
  type: "stateChunk";
  transferId: string;
  worldEpoch: Counter;
  revision: Counter;
  index: number;
  count: number;
  totalBytes: number;
  sha256: string;
  data: string;
}
export interface SessionOffer {
  type: "sessionOffer";
  protocolVersion: 6;
  contentVersion: string;
  serverIncarnation: string;
  sessionId: string;
  participantId: string;
  worldEpoch: Counter;
  role: "owner" | "operator" | "observer";
  ruleset: string;
  /**
   * @maxItems 32
   */
  requiredPacks: RequiredPack[];
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
export interface CornerTerrainPreview {
  worldEpoch: Counter;
  geometryRevision: Counter;
  position: Position;
  action:
    | "raise"
    | "lower"
    | "repair"
    | "placeTerrace"
    | "removeTerrace"
    | "placeCut"
    | "removeCut"
    | "plantForest"
    | "placeCreek";
  legal: boolean;
  reason:
    | (
        | "invalidInput"
        | "outside"
        | "unmatchedCorners"
        | "groundLevel"
        | "cornerLimit"
        | "floor"
        | "occupied"
        | "roof"
        | "capacityLimit"
        | "noChange"
        | "inconsistentNeighbors"
        | "unrepresentableHeight"
        | "mountainRequired"
        | "terraceRequired"
        | "higherNeighbor"
      )
    | null;
  cost: Money;
  /**
   * @maxItems 256
   */
  changes: CornerTerrainChange[];
  /**
   * @maxItems 64
   */
  skipped: CornerTerrainSkip[];
  /**
   * @maxItems 64
   */
  decorations?: AuthoringDecoration[];
}
export interface CornerTerrainChange {
  position: Position;
  beforeGround: number;
  afterGround: number;
  beforeNE: number;
  beforeSE: number;
  beforeSW: number;
  beforeNW: number;
  afterNE: number;
  afterSE: number;
  afterSW: number;
  afterNW: number;
  remove: boolean;
  losesForest: boolean;
  losesOwnership: boolean;
}
export interface CornerTerrainSkip {
  position: Position;
  reason:
    | "invalidInput"
    | "outside"
    | "unmatchedCorners"
    | "groundLevel"
    | "cornerLimit"
    | "floor"
    | "occupied"
    | "roof"
    | "capacityLimit"
    | "noChange"
    | "inconsistentNeighbors"
    | "unrepresentableHeight"
    | "mountainRequired"
    | "terraceRequired"
    | "higherNeighbor";
}
export interface AuthoringDecoration {
  position: Position;
  forest: Forest | null;
  creekConnections: number;
}
export interface RailwayPreview {
  query: RailwayQuery;
  legal: boolean;
  reason: string | null;
  cost: Money;
  reverseBend: boolean;
  /**
   * @maxItems 8192
   */
  cells: RailwayRouteCell[];
  /**
   * @maxItems 8192
   */
  addedPiers: Position[];
  /**
   * @maxItems 24576
   */
  losses: RailwayLoss[];
}
export interface RailwayRouteCell {
  position: Position;
  beforePattern: number | null;
  afterPattern: number | null;
  protectedByCar: boolean;
}
export interface RailwayLoss {
  kind: "pier" | "reclamation" | "accessory";
  position: Position;
  ownerId: Counter;
  supportedOwnerId: Counter | null;
}
export interface SlopePreview {
  query: SlopeQuery;
  legal: boolean;
  reason: string | null;
  cost: Money;
  base: Position;
  climb: 0 | 2 | 4 | 6;
  /**
   * @maxItems 8
   */
  members:
    | []
    | [SlopeMember]
    | [SlopeMember, SlopeMember]
    | [SlopeMember, SlopeMember, SlopeMember]
    | [SlopeMember, SlopeMember, SlopeMember, SlopeMember]
    | [SlopeMember, SlopeMember, SlopeMember, SlopeMember, SlopeMember]
    | [SlopeMember, SlopeMember, SlopeMember, SlopeMember, SlopeMember, SlopeMember]
    | [SlopeMember, SlopeMember, SlopeMember, SlopeMember, SlopeMember, SlopeMember, SlopeMember]
    | [SlopeMember, SlopeMember, SlopeMember, SlopeMember, SlopeMember, SlopeMember, SlopeMember, SlopeMember];
  /**
   * @maxItems 4
   */
  rails:
    | []
    | [SlopeRailPosition]
    | [SlopeRailPosition, SlopeRailPosition]
    | [SlopeRailPosition, SlopeRailPosition, SlopeRailPosition]
    | [SlopeRailPosition, SlopeRailPosition, SlopeRailPosition, SlopeRailPosition];
  /**
   * @maxItems 8192
   */
  addedPiers: Position[];
  /**
   * @maxItems 24576
   */
  losses: RailwayLoss[];
}
export interface SlopeMember {
  position: Position;
  kind: "traffic" | "slopefiller" | "slopesupport" | "slopewall";
  pattern: number | null;
  drawGroundBelow: boolean;
  drawGroundAbove: boolean;
}
export interface SlopeRailPosition {
  level: number;
  position: Position;
  pattern: number;
  zDiff: -1 | 0 | 1;
  zAngle: 0 | 2 | 4 | 6;
}
export interface RemovalPreview {
  query: RemovalQuery;
  legal: boolean;
  reason: string | null;
  objectKind: "terrain" | "forest" | "creek" | "terrace" | "slope" | "pier" | "railway" | "line";
  /**
   * @maxItems 8192
   */
  positions: Position[];
  /**
   * @maxItems 8192
   */
  path: Position[];
  /**
   * @maxItems 8192
   */
  skipped: RemovalSkip[];
}
export interface RemovalSkip {
  position: Position;
  reason: string;
}
