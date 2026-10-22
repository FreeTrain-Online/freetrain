// Stable ExAV 2.1.1.12, archive 883cf7d/trunk/core/world/{Direction,Location,World}.cs
// and core/views/QuarterViewDrawer.cs (fixed-level application of picking candidates).
// Original: Copyright (c) 2002 Kohsuke Kawaguchi et al.; LGPL-2.1-or-later.
import type { Heading, Position, StoragePosition } from '../generated/contracts.ts';
export interface Point { x: number; y: number }
export interface SpatialMap {
  project(p: Position): Point;
  center(p: Position): Point;
  pick(p: Point, z: number): Position;
  contains(p: Position): boolean;
  storage(p: Position): StoragePosition | null;
  ground(): Iterable<Position>;
}
export function int32(value: number): number {
  if (!Number.isInteger(value) || value < -2147483648 || value > 2147483647) throw new RangeError('Spatial Int32 overflow');
  return value;
}
export function heading(value: number): Heading {
  if (!Number.isInteger(value) || value < 0 || value > 7) throw new RangeError('Invalid heading');
  return value;
}
const offsets: readonly Point[] = [{x:0,y:-1},{x:1,y:-1},{x:1,y:0},{x:1,y:1},{x:0,y:1},{x:-1,y:1},{x:-1,y:0},{x:-1,y:-1}];
export function offset(h: Heading): Point { return {...offsets[heading(h)]}; }
export function rotate(h: Heading, eighthTurns: number): Heading { return ((heading(h)+int32(eighthTurns)%8+8)%8); }
export function angle(a: Heading,b: Heading): number { const d=Math.abs(heading(a)-heading(b));return Math.min(d,8-d); }
export function directionTo(from: Position,to: Position): Heading {
  if(from.z!==to.z)throw new RangeError('Direction requires the same level');
  const dx=Math.sign(int32(to.x-from.x)),dy=Math.sign(int32(to.y-from.y));
  const result=offsets.findIndex(p=>p.x===dx&&p.y===dy);
  if(result<0)throw new RangeError('Direction requires distinct locations');
  return result;
}
export function move(p:Position,h:Heading):Position { const d=offset(h);return {x:int32(p.x+d.x),y:int32(p.y+d.y),z:int32(p.z)}; }
export function distance(from:Position,to:Position):number {
  const dx=int32(to.x-from.x),dy=int32(to.y-from.y),dz=int32(to.z-from.z);
  return Math.trunc(Math.sqrt(int32(int32(int32(dx*dx)+int32(dy*dy))+int32(dz*dz))));
}
export function snap(p:Position,anchor:Position,mode:0|4|8):Position {
  if(p.z!==anchor.z)throw new RangeError('Snapping requires the same level');
  if(mode===0)return {...p};
  const dx=int32(Math.abs(int32(p.x-anchor.x))),dy=int32(Math.abs(int32(p.y-anchor.y)));
  if(mode===4)return dx>=dy?{x:p.x,y:anchor.y,z:p.z}:{x:anchor.x,y:p.y,z:p.z};
  if(mode!==8)throw new RangeError('Invalid snap mode');
  if(Math.trunc(dy/2)>dx)return {x:anchor.x,y:p.y,z:p.z};
  if(Math.trunc(dx/2)>dy)return {x:p.x,y:anchor.y,z:p.z};
  return {x:p.x,y:int32(anchor.y+dx*Math.sign(int32(p.y-anchor.y))),z:p.z};
}
export class WorldSpace implements SpatialMap {
  readonly centerX:number;
  constructor(readonly horizontalSize:number,readonly verticalSize:number,readonly levels:number) {
    if([horizontalSize,verticalSize,levels].some(v=>int32(v)<=0))throw new RangeError('Invalid dimensions');
    this.centerX=Math.trunc((verticalSize-1)/2);
  }
  storage(p:Position):StoragePosition {
    const xx=int32(p.x-this.centerX);
    return {h:Math.floor(int32(xx+p.y)/2),v:int32(p.y-xx),z:int32(p.z)};
  }
  location(p:StoragePosition):Position {
    return {x:int32(int32(p.h-Math.trunc(p.v/2))+this.centerX),y:int32(p.h+Math.trunc(int32(p.v+1)/2)),z:int32(p.z)};
  }
  containsHorizontal(p:Position):boolean { const s=this.storage(p);return s.h>=0&&s.h<this.horizontalSize&&s.v>=0&&s.v<this.verticalSize; }
  contains(p:Position):boolean { return p.z>=0&&p.z<this.levels&&this.containsHorizontal(p); }
  project(p:Position):Point {
    const x=int32(int32(p.x+p.z)-this.centerX),y=int32(p.y-p.z);
    return {x:int32(16*int32(x+y)),y:int32(8*int32(-x+y))};
  }
  // Terrain diamonds have their center 16,8 pixels from the source top-left.
  center(p:Position):Point { const q=this.project(p);return {x:int32(q.x+16),y:int32(q.y+8)}; }
  pick(p:Point,z:number):Position {
    if(!Number.isFinite(p.x)||!Number.isFinite(p.y))throw new RangeError('Invalid screen point');
    const t=2*p.y-16;
    return {x:int32(Math.floor((p.x-t)/32)+this.centerX-z),y:int32(Math.floor((p.x+t)/32)+z),z:int32(z)};
  }
  *ground():Iterable<Position> {
    for(let v=0;v<this.verticalSize;v++)for(let h=0;h<this.horizontalSize;h++)yield this.location({h,v,z:0});
  }
}
