import type { Position } from '../generated/contracts.ts';
import type { SpatialMap, Point } from './map-geometry.ts';
export interface Camera { x: number; y: number; zoom: number }
export function toScreen(p: Position, camera: Camera, width: number, height: number, space: SpatialMap): Point {
  const q=space.center(p); return {x:(q.x-camera.x)*camera.zoom+width/2,y:(q.y-camera.y)*camera.zoom+height/2};
}
export function tileAt(p: Point,z:number,camera:Camera,width:number,height:number,space:SpatialMap):Position {
  return space.pick({x:(p.x-width/2)/camera.zoom+camera.x,y:(p.y-height/2)/camera.zoom+camera.y},z);
}
export function zoomAt(camera: Camera, factor: number, p: Point, width: number, height: number): Camera {
  const zoom = Math.min(4, Math.max(0.5, camera.zoom*factor));
  return { x: camera.x+(p.x-width/2)*(1/camera.zoom-1/zoom), y: camera.y+(p.y-height/2)*(1/camera.zoom-1/zoom), zoom };
}
export function same(a: Position, b: Position) { return a.x===b.x && a.y===b.y && a.z===b.z; }
export interface SpriteMask { width: number; height: number; mask: readonly string[] }
export function opaqueAt(sprite: SpriteMask, x: number, y: number) {
  const ix=Math.floor(x), iy=Math.floor(y);
  return ix>=0 && iy>=0 && ix<sprite.width && iy<sprite.height && sprite.mask[iy][ix]==='1';
}
export function visible(x: number,y: number,w: number,h: number,width: number,height: number) { return x+w>=0 && y+h>=0 && x<=width && y<=height; }
