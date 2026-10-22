// OBSOLETE GAMEPLAY BASELINE: original-FreeTrain prototype rail shapes only.
import type { Track } from '../../generated/contracts.ts';
import type { Point } from '../map-geometry.ts';
export const ports: Record<Track['shape'], readonly Point[]> = {
  eastWest: [{x:1,y:0},{x:-1,y:0}], northSouth: [{x:0,y:-1},{x:0,y:1}],
  curveSE: [{x:0,y:1},{x:1,y:0}], curveSW: [{x:0,y:1},{x:-1,y:0}],
  curveNE: [{x:0,y:-1},{x:1,y:0}], curveNW: [{x:0,y:-1},{x:-1,y:0}],
  junctionE: [{x:0,y:-1},{x:0,y:1},{x:1,y:0}], buffer: [{x:-1,y:0}],
};
