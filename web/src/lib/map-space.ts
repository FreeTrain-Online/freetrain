import type { Map, Position } from '../generated/contracts.ts';
import { WorldSpace, type SpatialMap } from './map-geometry.ts';
import { PrototypeMap } from './prototype/map-geometry.ts';
export function mapSpace(map:Map):SpatialMap {
  switch(map.coordinateSystem) {
    case 'hv':return new WorldSpace(map.width,map.height,map.levels);
    case 'prototypeXY':return new PrototypeMap(map.width,map.height,map.levels);
    default:throw new Error('Unsupported coordinate system');
  }
}
export function homeLocation(map:Map):Position {
  if(map.coordinateSystem==='prototypeXY')return {x:22,y:16,z:0};
  return new WorldSpace(map.width,map.height,map.levels).location({h:Math.floor(map.width/2),v:Math.floor(map.height/2),z:0});
}
