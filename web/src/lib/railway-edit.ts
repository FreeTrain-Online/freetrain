import type { RailwayQuery } from '../generated/contracts.ts';
import type { ReadState } from './session-client.ts';
import { isTerrainWorld, isMixedWorld } from './world-identity.ts';
import { WorldSpace } from './map-geometry.ts';
import type { Position } from '../generated/contracts.ts';

export const railwayRevision = (read?: ReadState) => read?.mixed?.geometryRevision ?? read?.terrain?.geometryRevision;
export const railwayFence = (read?: ReadState) => `${read?.worldEpoch}/${railwayRevision(read)}/${read?.contentVersion}`;
export const railwayQueryKey = (q: RailwayQuery) => JSON.stringify([q.expectedWorldEpoch,q.expectedGeometryRevision,q.expectedContentVersion,q.action,q.routing,q.infrastructure??'normal',q.tunnelRouting??'exact',q.signalType??'{5349DE2A-DF3E-49B4-87A0-973105B7E6A4}',q.signalDirection??0,q.poleType??'{DDF7F835-8100-45fd-8AE7-EC498796D387}',q.stationaryType??'',q.speed??'low',q.speedSlope??false,q.speedClimb??0,q.from.x,q.from.y,q.from.z,q.to.x,q.to.y,q.to.z,q.via?.map(p=>[p.x,p.y,p.z])??[]]);

// Keyboard XY already identifies a column. Match the map's rail-or-ground
// policy within that column, respecting a cut into otherwise higher ground.
// Girder laying requests one level of clearance, retaining higher visible rail.
export function railwayStartLevel(read:ReadState,position:Pick<Position,'x'|'y'>,heightCut:number,clearance:0|1=0,traffic=false):number|undefined {
  if(!read.terrain||![position.x,position.y,heightCut].every(Number.isSafeInteger)||heightCut<0)return;
  const space=new WorldSpace(read.map.width,read.map.height,read.map.levels);
  try{
    const p={...position,z:0};if(!space.containsHorizontal(p))return;
    const s=space.storage(p),cut=Math.min(heightCut,read.map.levels-1);
    const ground=read.terrain.cells[s.v*read.map.width+s.h].ground;
    let level=clearance ? Math.min(read.map.levels-1,ground+clearance) : Math.min(cut,ground);
    for(const voxel of read.mixed?.voxels??[])if((traffic?voxel.traffic:voxel.traffic?.rail)&&voxel.position.x===position.x&&voxel.position.y===position.y&&voxel.position.z<=cut)
      level=Math.max(level,voxel.position.z);
    return level;
  }catch(error){if(error instanceof RangeError)return;throw error;}
}

// A single, exact installed-profile handoff on the existing connection. Both
// profiles use the same verified pack set; replacement worlds still re-bootstrap.
export function isRailwayPromotion(before: ReadState, after: ReadState): boolean {
  return isTerrainWorld(before) && isMixedWorld(after) && before.worldEpoch === after.worldEpoch &&
    BigInt(after.revision) > BigInt(before.revision) && JSON.stringify(before.map) === JSON.stringify(after.map) &&
    JSON.stringify(before.requiredPacks) === JSON.stringify(after.requiredPacks) &&
    before.terrain?.name === after.terrain?.name && JSON.stringify(before.terrain?.provenance) === JSON.stringify(after.terrain?.provenance);
}

export const slopeQueryKey = (q:import('../generated/contracts.ts').SlopeQuery) => JSON.stringify([q.expectedWorldEpoch,q.expectedGeometryRevision,q.expectedContentVersion,q.action,q.climb,q.position.x,q.position.y,q.position.z]);

export const railwayReasons = {
  speedLimitTraffic:'speedLimitTraffic',speedLimitOverlap:'speedLimitOverlap',stationaryNotFound:'stationaryNotFound',stationaryGround:'stationaryGround',poleNotFound:'poleNotFound',poleTarget:'poleTarget',signalNotFound:'signalNotFound',signalTarget:'signalTarget',invalidInput:'geometryFailed',nonCardinalSlope:'slopeCardinal',notSlope:'slopeNotFound',slopeDirectionMismatch:'slopeMismatch',occupiedCar:'railCar',unavailableSlopeBinding:'railBinding',staleRevision:'railStale',
  garageGroundRequired:'garageGroundRequired',garageDependency:'garageDependency',tunnelMountainRequired:'tunnelMountainRequired',tunnelComponentDependency:'tunnelComponentDependency',girderAboveGround:'girderAboveGround',samePoint:'railSamePoint',noncoplanar:'railNoncoplanar',routeWorkLimit:'railWorkLimit',outside:'outsideMap',
  unusableStart:'railStart',underwater:'railUnderwater',obstacle:'railObstacle',carOccupied:'railCar',
  existingMiddleRail:'railMiddle',cannotAttach:'railAttach',invalidRailPattern:'railPattern',
  unavailableRailBinding:'railBinding',unavailablePierBinding:'railBinding',unavailableCrossingBinding:'railBinding',
  unavailableReclamationHandler:'railBinding',platformDependency:'railPlatform',railwayCapacityLimit:'railVoxelCapacity',
  accountCapacityLimit:'railJournalCapacity',constructionOverflow:'railOverflow',
} as const;

// Distinguish an unreadable request (including an older running server) from a
// normal, successfully checked illegal route.
export class RailwayPreviewRequestError extends Error {}
