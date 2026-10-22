// OBSOLETE GAMEPLAY BASELINE: reference-v1 X/Y bounds and centered picking.
// The adapter preserves Save 1 worlds; it never reinterprets their cells as H/V.
import type { Position } from '../../generated/contracts.ts';
import { WorldSpace, type SpatialMap, type Point } from '../map-geometry.ts';
const origin=new WorldSpace(1,1,1);
export const project=(p:Position):Point=>origin.project(p);
export function unproject(p:Point,z:number):Point { return {x:p.x/32-(p.y+16*z)/16,y:p.x/32+(p.y+16*z)/16}; }
export class PrototypeMap implements SpatialMap {
  constructor(readonly width:number,readonly height:number,readonly levels:number) {}
  project=project;
  center=project;
  pick(p:Point,z:number):Position { const q=unproject(p,z);return {x:Math.floor(q.x+.5),y:Math.floor(q.y+.5),z}; }
  contains(p:Position):boolean { return p.x>=0&&p.x<this.width&&p.y>=0&&p.y<this.height&&p.z>=0&&p.z<this.levels; }
  storage():null { return null; }
  *ground():Iterable<Position> { for(let y=0;y<this.height;y++)for(let x=0;x<this.width;x++)yield {x,y,z:0}; }
}
