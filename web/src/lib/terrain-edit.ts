import { terrainProfile, terrainLookup, isValidatedTerrain } from './terrain-state.ts';
import type { Position, Snapshot } from '../generated/contracts.ts';
import type { ReadState } from './session-client.ts';
import { WorldSpace, type Point } from './map-geometry.ts';

// Canonical selection: the NE vertex of this cell, shared with (x,y-1),
// (x+1,y-1), and (x+1,y). Keep X/Y fixed while the terrain height changes.
export function terrainCornerPoint(read:ReadState,position:Position):Point|undefined {
  if(!read.terrain)return;
  const space=new WorldSpace(read.map.width,read.map.height,read.map.levels);
  if(!space.containsHorizontal(position))return;
  const s=space.storage(position),ground=read.terrain.cells[s.v*read.map.width+s.h].ground;
  const occupant=isValidatedTerrain(read)
    ?terrainLookup(read as Snapshot).occupant((s.v*read.map.width+s.h)*read.map.levels+ground)
    :read.terrain.occupants.find(o=>read.entities?.some(e=>e.id===o.entityId&&e.anchor.x===position.x&&e.anchor.y===position.y&&e.anchor.z===ground));
  const p=space.project({...position,z:ground});
  return {x:p.x+16,y:p.y-4*(occupant?.kind==='mountain'?occupant.ne:0)};
}

export function pickTerrainIntersection(read:ReadState,point:Point,heightCut:number):Position|undefined {
  const picked=pickTerrainGround(read,point,heightCut);if(!picked)return;
  // Visit the four vertices of the picked diamond; measure actual projected
  // heights, so the marker follows slopes as well as flat ground.
  let best:Position|undefined,distance=Infinity;
  for(const [dx,dy] of [[0,0],[0,1],[-1,1],[-1,0]]){
    const p={x:picked.x+dx,y:picked.y+dy,z:picked.z},corner=terrainCornerPoint(read,p);
    if(!corner)continue;
    const d=(point.x-corner.x)**2+(point.y-corner.y)**2;
    if(d<distance){best=p;distance=d;}
  }
  return best;
}

// QuarterViewDrawer.fromABToXYZ + GroundDisambiguator. Terrain tools target
// the source ground ray, independently of tree alpha, the sea plane and sprites.
export function pickTerrainGround(read:ReadState,point:Point,heightCut:number):Position|undefined {
  if(!read.terrain)return;
  const space=new WorldSpace(read.map.width,read.map.height,read.map.levels);
  for(let z=Math.min(heightCut,read.map.levels-1);z>=0;z--){
    const p=space.pick({x:Math.floor(point.x),y:Math.floor(point.y)},z);
    if(!space.contains(p))continue;
    const s=space.storage(p);
    if(read.terrain.cells[s.v*read.map.width+s.h].ground===z)return p;
  }
}

export const terrainReasons = {invalidInput:'cornerInvalid',outside:'outsideMap',unmatchedCorners:'cornerUnmatched',groundLevel:'cornerGround',cornerLimit:'cornerLimit',floor:'cornerFloor',occupied:'cornerOccupied',roof:'cornerRoof',capacityLimit:'cornerCapacity',noChange:'terrainNoChange',inconsistentNeighbors:'repairInconsistent',unrepresentableHeight:'repairUnrepresentable',mountainRequired:'mountainRequired',terraceRequired:'terraceRequired',higherNeighbor:'higherNeighbor'} as const;

// One compact column index per immutable entity array; no scene rebuild or sprite-alpha dependency.
const strategyIndexes=new WeakMap<object,{mountain:Uint8Array;terrace:Uint8Array}>();
export function pickTerrainStrategy(read:ReadState,point:Point,heightCut:number,strategy?:string):Position|undefined {
  if(!strategy||strategy==='placeTerrace'||strategy==='removeCut'||!read.terrain||!read.entities)return pickTerrainGround(read,point,heightCut);
  const space=new WorldSpace(read.map.width,read.map.height,read.map.levels);
  let index=strategyIndexes.get(read.entities);
  if(!index){
    index={mountain:new Uint8Array(read.map.width*read.map.height),terrace:new Uint8Array(read.map.width*read.map.height)};
    const kinds=new Map(terrainProfile.definitions.filter(d=>d.treeSpriteCount===0).map(d=>[d.reference.contributionId,d.kind]));
    for(const e of read.entities){const kind=kinds.get(e.definition.contributionId);if(kind!=='mountain'&&kind!=='terrace')continue;
      const s=space.storage(e.anchor);index[kind][s.v*read.map.width+s.h]=e.anchor.z+1;}
    strategyIndexes.set(read.entities,index);
  }
  const heights=index[strategy==='removeTerrace'?'terrace':'mountain'];
  for(let z=Math.min(heightCut,read.map.levels-1);z>=0;z--){
    const p=space.pick({x:Math.floor(point.x),y:Math.floor(point.y)},z);
    if(!space.contains(p))continue;
    const s=space.storage(p);if(heights[s.v*read.map.width+s.h]===z+1)return p;
  }
  return pickTerrainGround(read,point,heightCut);
}
