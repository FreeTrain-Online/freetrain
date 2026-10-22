/* Generated from prototype/protocol/schema.json. Run deno task generate:contracts. */

export type GameDocument = WireMessage | SpatialQuery | SpatialPreview;
export type WireMessage =
  Join | Command | Accepted | Rejected | Snapshot | Delta | Resync | Heartbeat | InputReset | SoundEvent;
export type Counter = string;
export type Money = string;
export type Heading = number;
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
      shape: "eastWest" | "northSouth" | "curveSE" | "curveSW" | "curveNE" | "curveNW" | "junctionE" | "buffer";
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
  protocolVersion: 3;
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
  shape: "eastWest" | "northSouth" | "curveSE" | "curveSW" | "curveNE" | "curveNW" | "junctionE" | "buffer";
  revision: Counter;
}
export interface RequiredPack {
  id: string;
  version: string;
  catalogHash: string;
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
    | "controlBusy";
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
}
export interface Removed {
  trainIds: string[];
  stationIds: string[];
  buildingIds: string[];
  trackPositions: Position[];
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
