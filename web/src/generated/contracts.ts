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
  | RemovalPreview
  | FleetTradeQuery
  | FleetTradePreview
  | FleetDeploymentQuery
  | FleetDeploymentPreview
  | StationBuildingQuery
  | StationBuildingPreview
  | ThinPlatformQuery
  | ThinPlatformPreview
  | WidePlatformQuery
  | WidePlatformPreview
  | PlatformPropertiesQuery
  | PlatformProperties
  | PassageQuery
  | PassagePreview
  | StationInformationQuery
  | StationInformation
  | CityQuery
  | CityPreview;
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
  | SessionOffer
  | DepartureBell;
export type Counter = string;
export type Money = string;
export type Heading = number;
export type EntityId = string;
/**
 * @minItems 24
 * @maxItems 24
 */
export type PopulationTable = [
  number,
  number,
  number,
  number,
  number,
  number,
  number,
  number,
  number,
  number,
  number,
  number,
  number,
  number,
  number,
  number,
  number,
  number,
  number,
  number,
  number,
  number,
  number,
  number
];
export type PlaybackRate = 0.125 | 0.25 | 0.5 | 1 | 2;
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
    }
  | {
      kind: "tradeFleet";
      query: FleetTradeQuery;
      expectedPrice: Counter;
    }
  | {
      kind: "renameFleet";
      trainId: Counter;
      expectedRevision: Counter;
      name: string;
    }
  | {
      kind: "deployFleet";
      query: FleetDeploymentQuery;
    }
  | {
      kind: "setPlaybackRate";
      rate: PlaybackRate;
    }
  | {
      kind: "manageFleetController";
      action: "create" | "rename" | "copy" | "configure" | "delete" | "cleanJunctions";
      controllerId: Counter;
      expectedRevision: Counter;
      name: string;
      provider: ContentBinding | null;
      harbors:
        | []
        | [
            {
              location: Position;
              kind: "pass" | "fixed" | "daily" | "advanced";
              minutes: number;
              reverse: boolean;
            }
          ]
        | null;
      location: Position | null;
      junctions?:
        | []
        | [
            {
              location: Position;
              defaultRoute: "straight" | "curve";
            }
          ]
        | null;
      expectedGeometryRevision?: Counter | null;
    }
  | {
      kind: "assignFleetController";
      targetKind: "train" | "group";
      targetId: Counter;
      expectedRevision: Counter;
      controllerId: Counter;
      expectedControllerRevision: Counter;
    }
  | {
      kind: "editFleetRule";
      controllerId: EntityId;
      expectedRevision: Counter;
      location: Position;
      settingKind: "harbor" | "junction";
      operation: "add" | "replace" | "copy" | "move" | "remove" | "clear" | "append";
      index: number;
      offset: number;
      rule: FleetRule | null;
      expectedGeometryRevision: Counter | null;
      rulesBase64?: string | null;
    }
  | {
      kind: "manageFleetGroup";
      action: "create" | "rename" | "move";
      targetKind: "group" | "train";
      targetId: Counter;
      expectedRevision: Counter;
      name: string;
      parentId: Counter | null;
      expectedParentRevision: Counter | null;
    }
  | {
      kind: "configureFleetColor";
      expectedRevision: Counter;
      configuration: FleetColorTest;
    }
  | {
      kind: "editStationBuilding";
      query: StationBuildingQuery;
      expectedCost: Money;
    }
  | {
      kind: "editThinPlatform";
      query: ThinPlatformQuery;
      expectedCost: Money;
    }
  | {
      kind: "editWidePlatform";
      query: WidePlatformQuery;
      expectedCost: Money;
    }
  | {
      kind: "editPlatformProperties";
      query: PlatformPropertiesQuery;
      name: string;
      bell: Definition;
      hostId: Counter | null;
      expectedHostRevision: Counter;
      expectedStationRevision: Counter;
    }
  | {
      kind: "editPassage";
      query: PassageQuery;
      expectedCost: Money;
    }
  | CancelCityConstruction;

export interface Join {
  type: "join";
  protocolVersion: 10;
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
  playbackRate?: PlaybackRate;
  trainMotion?: TrainMotion;
  effectsBaseline?: EffectsBaseline;
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
  speedLimits?: SpeedLimits;
  fleet?: Fleet;
  queue?: MixedQueue;
  stationGraph?: StationGraph;
  city?: City;
}
export interface RailwayVoxel {
  position: Position;
  ownerId: EntityId;
  kind: "traffic" | "slopefiller" | "slopesupport" | "slopewall" | "pier" | "stationary";
  traffic: Traffic | null;
  definition: Definition | null;
  sprite: number;
  drawGroundBelow: boolean;
  drawGroundAbove: boolean;
  supportedOwnerId: EntityId | null;
  construction?: ConstructionCell;
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
  controllerId: null;
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
export interface ConstructionCell {
  state: number;
  ground: number;
  needsDoor: boolean;
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
  revenue?: Counter;
}
export interface SpeedLimits {
  /**
   * @maxItems 8192
   */
  low: Position[];
  /**
   * @maxItems 8192
   */
  medium: Position[];
  /**
   * @maxItems 8192
   */
  fast: Position[];
}
export interface Fleet {
  rootGroupId: EntityId;
  /**
   * @minItems 0
   * @maxItems 128
   */
  trains: FleetTrain[];
  /**
   * @minItems 1
   * @maxItems 256
   */
  groups: [FleetGroup, ...FleetGroup[]];
  /**
   * @minItems 2
   * @maxItems 256
   */
  controllers: [FleetController, FleetController, ...FleetController[]];
  colorTest?: FleetColorTest;
}
export interface FleetTrain {
  id: EntityId;
  revision: Counter;
  name: string;
  definition: ContentBinding;
  groupId: EntityId;
  controllerId: EntityId | null;
  /**
   * @minItems 1
   * @maxItems 100
   */
  cars: [FleetCar, ...FleetCar[]];
  isReversed: boolean;
  state: "unplaced" | "moving" | "stoppingatstation" | "stoppingatsignal" | "emergencystopping";
  passengers: number;
  boardingOrigin: FleetPose | null;
  forwarding: boolean;
  moveCount: number;
  stopCallCount: number;
  purchaseMinute: Counter | null;
}
export interface ContentBinding {
  packId: string;
  version: string;
  catalogHash: string;
  contributionId: string;
}
export interface FleetCar {
  id: EntityId;
  revision: Counter;
  originalIndex: number;
  definition: Definition;
  state: FleetPose;
}
export interface FleetPose {
  kind: "unplaced" | "inside" | "outside";
  location: Position | null;
  heading: number | null;
  countdown: number | null;
}
export interface FleetGroup {
  id: EntityId;
  revision: Counter;
  name: string;
  parentId: EntityId | null;
  controllerId: EntityId | null;
  /**
   * @minItems 0
   * @maxItems 384
   */
  members: EntityId[];
}
export interface FleetController {
  id: EntityId;
  revision: Counter;
  name: string;
  kind: "simple" | "delegation" | "timetable";
  provider: ContentBinding | null;
  /**
   * @minItems 0
   * @maxItems 8192
   */
  harbors: FleetHarbor[];
  /**
   * @maxItems 8192
   */
  junctions?: FleetJunction[];
}
export interface FleetHarbor {
  location: Position;
  kind: "pass" | "fixed" | "daily" | "advanced";
  minutes: number;
  reverse: boolean;
  /**
   * @maxItems 1024
   */
  rules?: FleetRule[];
}
export interface FleetRule {
  month: -1 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12;
  day:
    | -1
    | 1
    | 2
    | 3
    | 4
    | 5
    | 6
    | 7
    | 8
    | 9
    | 10
    | 11
    | 12
    | 13
    | 14
    | 15
    | 16
    | 17
    | 18
    | 19
    | 20
    | 21
    | 22
    | 23
    | 24
    | 25
    | 26
    | 27
    | 28
    | 29
    | 30
    | 31;
  dayOfWeek: number;
  hour: number;
  minutes: -1 | 0 | 10 | 20 | 30 | 40 | 50;
  action:
    | "pass"
    | "stop"
    | "go"
    | "reverse"
    | "forwarding"
    | "reverseforwarding"
    | "reverse2"
    | "rforwarding2"
    | "straight"
    | "curve";
}
export interface FleetJunction {
  location: Position;
  defaultRoute: "straight" | "curve";
  /**
   * @maxItems 1024
   */
  rules?: FleetRule[];
}
export interface FleetColorTest {
  revision: Counter;
  template: Definition;
  body: string;
  line1: string;
  line2: string;
  line3: string;
}
export interface MixedQueue {
  nextOrder: Counter;
  /**
   * @minItems 0
   * @maxItems 65536
   */
  pending: MixedAction[];
}
export interface MixedAction {
  registration: Counter;
  order: Counter;
  dueMinute: Counter;
  handlerId: string;
  interval: Counter;
}
export interface StationGraph {
  nextStationName: number;
  nextPlatformName: number;
  /**
   * @maxItems 8192
   */
  buildings: StationBuilding[];
  /**
   * @maxItems 8192
   */
  platforms: StationPlatform[];
  /**
   * @maxItems 8192
   */
  passages: StationPassage[];
  /**
   * @maxItems 8192
   */
  listeners: StationListener[];
  /**
   * @maxItems 8192
   */
  listenerMemberships: StationListenerMembership[];
}
export interface StationBuilding {
  id: EntityId;
  revision: Counter;
  definition: Definition;
  location: Position;
  name: string;
  /**
   * @maxItems 8192
   */
  children: EntityId[];
  passengers: StationPassengers;
  imports: StationLog;
  exports: StationLog;
  trains: StationLog;
  hourTimer: MixedAction | null;
  dayTimer: MixedAction | null;
}
export interface StationPassengers {
  gone: number;
  accumulatedLoaded: number;
  accumulatedUnloaded: number;
}
export interface StationLog {
  today: number;
  yesterday: number;
  thisWeek: number;
  lastWeekPerDay: number;
}
export interface StationPlatform {
  id: EntityId;
  revision: Counter;
  kind: "thin" | "wide";
  location: Position;
  direction: 0 | 2 | 4 | 6;
  length: number;
  name: string;
  roof: boolean;
  lane0: boolean;
  lane1: boolean;
  bell: Definition;
  hostId: EntityId | null;
  /**
   * @maxItems 8192
   */
  children: EntityId[];
  /**
   * @maxItems 8192
   */
  yards: PlatformYard[];
  dayTimer: MixedAction | null;
}
export interface PlatformYard {
  trafficOwnerId: EntityId;
  location: Position;
  lane: number;
  index: number;
  roof: boolean;
  outlook: PlatformOutlook;
}
export interface PlatformOutlook {
  kind: "plain" | "passage" | "stair";
  material: "concrete" | "brown" | "green";
  bridge: boolean;
  upward: boolean;
}
export interface StationPassage {
  trafficOwnerId: EntityId;
  location: Position;
  direction: 0 | 2 | 4 | 6;
  material: "concrete" | "brown" | "green";
}
export interface StationListener {
  providerId: EntityId;
  /**
   * @maxItems 4
   */
  stations:
    [] | [EntityId] | [EntityId, EntityId] | [EntityId, EntityId, EntityId] | [EntityId, EntityId, EntityId, EntityId];
  provider?: StationProvider;
}
export interface StationProvider {
  population: Population;
  landValue: number;
  zSize: number;
  special: boolean;
  input: "controlled" | "city-fixed" | "city-variable" | "city-half" | "city-ground";
  xySize: number;
}
export interface Population {
  kind:
    | "AgriculturalPopulation"
    | "BarPopulation"
    | "BusinessInnPopulation"
    | "LeisureHotelPopulation"
    | "LeisureLandPopulation"
    | "OfficePopulation"
    | "ResidentialPopulation"
    | "RestaurantPopulation"
    | "SchoolPopulation"
    | "ShopperPopulation"
    | "ConstantPopulation"
    | "CombinationPopulation"
    | "MultiplierPopulation"
    | "PersistentPopulation"
    | "HourlyPopulation"
    | "HourlyWeeklyPopulation"
    | "StadiumPopulation";
  base: number;
  factor: number;
  entering: number;
  /**
   * @maxItems 256
   */
  children: Population[];
  /**
   * @maxItems 14
   */
  tables:
    | []
    | [PopulationTable]
    | [PopulationTable, PopulationTable]
    | [PopulationTable, PopulationTable, PopulationTable]
    | [PopulationTable, PopulationTable, PopulationTable, PopulationTable]
    | [PopulationTable, PopulationTable, PopulationTable, PopulationTable, PopulationTable]
    | [PopulationTable, PopulationTable, PopulationTable, PopulationTable, PopulationTable, PopulationTable]
    | [
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable
      ]
    | [
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable
      ]
    | [
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable
      ]
    | [
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable
      ]
    | [
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable
      ]
    | [
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable
      ]
    | [
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable
      ]
    | [
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable,
        PopulationTable
      ];
}
export interface StationListenerMembership {
  stationId: EntityId;
  /**
   * @maxItems 8192
   */
  providers: EntityId[];
}
export interface City {
  format: 1;
  /**
   * @maxItems 8192
   */
  properties: CityProperty[];
  /**
   * @maxItems 8192
   */
  structures: CityStructure[];
  /**
   * @maxItems 8192
   */
  ground: CityGround[];
  /**
   * @maxItems 8192
   */
  producers: CityProducer[];
  /**
   * @maxItems 32768
   */
  fences: CityFence[];
  heat: CityHeat;
  settings: CitySettings;
  /**
   * @maxItems 8192
   */
  plans: CitySearchPlan[];
  /**
   * @maxItems 8192
   */
  landCache: CityLandCache[];
  /**
   * @maxItems 8192
   */
  fenceHosts: CityFenceHost[];
  heatCallback: MixedAction | null;
  developmentCallback: MixedAction | null;
}
export interface CityProperty {
  id: EntityId;
  revision: Counter;
  location: Position;
}
export interface CityStructure {
  id: EntityId;
  revision: Counter;
  definition: Definition;
  location: Position;
  height: number;
  owned: boolean;
  subsidiary: CitySubsidiary | null;
  /**
   * @maxItems 2
   */
  halves: [] | [CityHalf] | [CityHalf, CityHalf];
  /**
   * @maxItems 8192
   */
  construction: CityConstructionVoxel[];
  caller: "legacyplayer" | "catalogplayer" | "automated";
}
export interface CitySubsidiary {
  marketPrice: Counter;
  totalLandValue: Counter;
  callback: MixedAction | null;
}
export interface CityHalf {
  definition: Definition;
  side: "back" | "fore";
  direction: 0 | 2 | 4 | 6;
  color: number;
}
export interface CityConstructionVoxel {
  offset: Position;
  cell: ConstructionCell;
  callback: MixedAction | null;
}
export interface CityGround {
  id: EntityId;
  revision: Counter;
  definition: Definition;
  location: Position;
  owned: boolean;
  variant: number;
}
export interface CityProducer {
  ownerId: EntityId;
  initialDefinition: Definition;
  initialHeight: number;
}
export interface CityFence {
  hostOwnerId: EntityId;
  location: Position;
  side: number;
  definition: Definition;
}
export interface CityHeat {
  horizontalSize: number;
  verticalSize: number;
  /**
   * @maxItems 1708196
   */
  q: number[];
  /**
   * @maxItems 1708196
   */
  back: number[];
  /**
   * @maxItems 1708196
   */
  rho: number[];
}
export interface CitySettings {
  strengthDiffusion: number;
  populationPower: number;
  populationBase: number;
  populationScale: number;
  landPriceScale: number;
  replacementFactor: number;
  maximumPricePower: number;
  alpha: number;
  diffusion: number;
  updateFrequencyHours: number;
  landValuePower: number;
  bareConductivity: number;
}
export interface CitySearchPlan {
  stationId: EntityId;
  phase: "start" | "selectstruct1" | "selectstruct2" | "fitplace" | "build" | "end";
  strength: number;
  budget: number;
  /**
   * @minItems 4
   * @maxItems 4
   */
  work: [number, number, number, number];
  scanning: Position | null;
  selected: CitySelectedPlan | null;
}
export interface CitySelectedPlan {
  definition: Definition;
  site: Position;
  sizeX: number;
  sizeY: number;
  height: number;
  value: Counter;
  finalPrice: Counter;
}
export interface CityLandCache {
  corner: Position;
  sizeX: number;
  sizeY: number;
  landValue: Counter;
  entityValue: Counter;
}
export interface CityFenceHost {
  id: EntityId;
  revision: Counter;
  location: Position;
}
export interface TrainMotion {
  sequence: string;
  reset: boolean;
  /**
   * @maxItems 128
   */
  steps: TrainMotionStep[];
}
export interface TrainMotionStep {
  sequence: string;
  minute: string;
  trainId: string;
  duration: number;
  /**
   * An empty array explicitly resets this train after an unsupported or discontinuous transition.
   *
   * @maxItems 100
   */
  cars: TrainMotionCar[];
}
export interface TrainMotionCar {
  carId: string;
  from: TrainMotionPose;
  to: TrainMotionPose;
}
export interface TrainMotionPose {
  position: Position;
  heading: number;
  outgoing: number;
  reversed: boolean;
  slope?: TrainMotionSlope;
}
export interface TrainMotionSlope {
  climb: 0 | 2 | 4 | 6;
  level: number;
}
export interface EffectsBaseline {
  worldEpoch: Counter;
  liveEpoch: string;
  publicationSequence: Counter;
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
  /**
   * @maxItems 8
   */
  via?:
    | []
    | [Position]
    | [Position, Position]
    | [Position, Position, Position]
    | [Position, Position, Position, Position]
    | [Position, Position, Position, Position, Position]
    | [Position, Position, Position, Position, Position, Position]
    | [Position, Position, Position, Position, Position, Position, Position]
    | [Position, Position, Position, Position, Position, Position, Position, Position];
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
  infrastructure?: "normal" | "bridge" | "girder" | "tunnel" | "garage" | "signal" | "pole" | "stationary" | "speed";
  stationaryType?: string;
  speed?: "low" | "medium" | "fast";
  speedSlope?: boolean;
  speedClimb?: 0 | 2 | 4 | 6;
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
export interface FleetTradeQuery {
  expectedWorldEpoch: Counter;
  expectedRevision: Counter;
  expectedMinute: Counter;
  expectedContentVersion: string;
  action: "buy" | "sell";
  definition: ContentBinding | null;
  length: number;
  quantity: number;
  trainId: Counter;
}
export interface FleetDeploymentQuery {
  expectedWorldEpoch: Counter;
  expectedRevision: Counter;
  expectedMinute: Counter;
  expectedContentVersion: string;
  trainId: Counter;
  action: "place" | "withdraw" | "reverse";
  tail: Position | null;
  reverse: boolean;
}
export interface StationBuildingQuery {
  expectedWorldEpoch: Counter;
  expectedGeometryRevision: Counter;
  expectedMinute: Counter;
  expectedContentVersion: "terrain-content-1" | "mixed-railway-content-1";
  action: "place" | "remove";
  position: Position;
  definition: Definition | null;
  stationId: Counter;
  expectedStationRevision: Counter;
}
export interface ThinPlatformQuery {
  expectedWorldEpoch: Counter;
  expectedGeometryRevision: Counter;
  expectedMinute: Counter;
  expectedContentVersion: "terrain-content-1" | "mixed-railway-content-1";
  action: "place" | "remove";
  position: Position;
  direction: 0 | 2 | 4 | 6;
  length: number;
  roof: boolean;
  platformId: Counter;
  expectedPlatformRevision: Counter;
}
export interface WidePlatformQuery {
  expectedWorldEpoch: Counter;
  expectedGeometryRevision: Counter;
  expectedMinute: Counter;
  expectedContentVersion: "terrain-content-1" | "mixed-railway-content-1";
  action: "place" | "remove" | "addLane" | "removeLane";
  position: Position;
  direction: 0 | 2 | 4 | 6;
  length: number;
  platformId: Counter;
  expectedPlatformRevision: Counter;
  secondLane: boolean;
}
export interface PlatformPropertiesQuery {
  expectedWorldEpoch: Counter;
  expectedGeometryRevision: Counter;
  expectedContentVersion: "terrain-content-1" | "mixed-railway-content-1";
  platformId: Counter;
  expectedPlatformRevision: Counter;
}
export interface PassageQuery {
  expectedWorldEpoch: Counter;
  expectedGeometryRevision: Counter;
  expectedMinute: Counter;
  expectedContentVersion: "terrain-content-1" | "mixed-railway-content-1";
  action: "place" | "remove";
  from: Position;
  to: Position;
  material: "concrete" | "brown" | "green";
  part?: "passage" | "stairs";
}
export interface CancelCityConstruction {
  kind: "cancelCityConstruction";
  query: CityQuery;
}
export interface CityQuery {
  expectedWorldEpoch: Counter;
  expectedRevision: Counter;
  expectedMinute: Counter;
  expectedContentVersion: string;
  ownerId: EntityId;
  expectedOwnerRevision: Counter;
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
    | "operationUnavailable"
    | "controllerReferenced";
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
  playbackRate?: PlaybackRate;
  trainMotion?: TrainMotion;
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
  protocolVersion: 10;
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
export interface DepartureBell {
  type: "departureBell";
  worldEpoch: Counter;
  liveEpoch: string;
  publicationSequence: Counter;
  effectOrdinal: number;
  trainId: Counter;
  platformId: Counter;
  bell: Definition;
  location: Position;
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
export interface FleetTradePreview {
  query: FleetTradeQuery;
  price: Counter;
  normalCapacity: Counter;
  seatedCapacity: Counter;
  packedCapacity: Counter;
}
export interface FleetDeploymentPreview {
  query: FleetDeploymentQuery;
  legal: boolean;
  reason: string | null;
  isReversed: boolean;
  /**
   * @maxItems 100
   */
  cars: FleetDeploymentCar[];
}
export interface FleetDeploymentCar {
  carId: Counter;
  location: Position;
  heading: number;
}
export interface StationBuildingPreview {
  query: StationBuildingQuery;
  legal: boolean;
  reason: string | null;
  cost: Money;
  /**
   * @maxItems 32768
   */
  positions: Position[];
  /**
   * @maxItems 24576
   */
  losses: RailwayLoss[];
}
export interface ThinPlatformPreview {
  query: ThinPlatformQuery;
  legal: boolean;
  reason: string | null;
  cost: Money;
  /**
   * @maxItems 32768
   */
  positions: Position[];
}
export interface WidePlatformPreview {
  query: WidePlatformQuery;
  legal: boolean;
  reason: string | null;
  cost: Money;
  /**
   * @maxItems 32768
   */
  positions: Position[];
}
export interface PlatformProperties {
  query: PlatformPropertiesQuery;
  /**
   * @maxItems 8192
   */
  candidates: PlatformHostCandidate[];
}
export interface PlatformHostCandidate {
  id: Counter;
  revision: Counter;
  location: Position;
  isStation: boolean;
  stationId: Counter;
  stationRevision: Counter;
  stationLocation: Position;
}
export interface PassagePreview {
  query: PassageQuery;
  legal: boolean;
  reason: string | null;
  cost: Money;
  /**
   * @maxItems 32768
   */
  positions: Position[];
}
export interface StationInformationQuery {
  expectedWorldEpoch: Counter;
  expectedRevision: Counter;
  expectedMinute: Counter;
  stationId: Counter;
  platformId: Counter;
  trainId: Counter;
  latest?: boolean;
}
export interface StationInformation {
  query: StationInformationQuery;
  demand: number | null;
  waiting: number | null;
  averageBoarded: number | null;
  averageAlighted: number | null;
  stopIndex: number | null;
  yardIndex: number | null;
  controllerId: Counter | null;
  reason:
    | "disconnected"
    | "noPlatform"
    | "noTrain"
    | "unplaced"
    | "direction"
    | "length"
    | "away"
    | "position"
    | "pass"
    | "forwarding"
    | "wait"
    | "ready";
  waitMinutes: number;
  observedRevision: Counter;
  observedMinute: Counter;
}
export interface CityPreview {
  query: CityQuery;
  canCancel: boolean;
  reason: string | null;
  cost: Counter;
  structure: CityStructure;
}
