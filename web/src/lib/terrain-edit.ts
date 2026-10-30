import type { Position } from '../generated/contracts.ts';
import type { ReadState } from './session-client.ts';
import { WorldSpace, type Point } from './map-geometry.ts';

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
