import { WorldSpace } from './map-geometry.ts';
export const demoDimensions={width:48,height:96,levels:16,waterLevel:4} as const;
export const demoSpace=new WorldSpace(48,96,16);
// Shared synthetic corner samples. Plateaus separate successive ramps so a
// cell spans at most one whole level and needs quarter offsets only in 0..4.
function relief(depth:number,period:number):number {
  if(depth<=0)return 0;
  const band=Math.floor(depth/period),ramp=depth-band*period;
  return 4*band+Math.min(4,Math.floor(ramp*4/(period-3)));
}
function fine(x:number,y:number):number {
  const h=(x-demoSpace.centerX+y)/2,v=y-x+demoSpace.centerX;
  return 12+Math.max(relief(24-Math.hypot(h-22,(v-54)*.65),8),relief(16-Math.hypot((h-30)*1.15,(v-27)*.65),4));
}
export function demoGround(x:number,y:number) {
  const samples=[[.5,-.5],[.5,.5],[-.5,.5],[-.5,-.5]].map(([dx,dy])=>fine(x+dx,y+dy));
  const ground=Math.floor(Math.min(...samples)/4);
  return {ground,corners:samples.map(n=>n-ground*4)};
}

/** Build one shared corner field before materializing cells. Flattening the
 * decorative rail bed must also repair touching neighbours; marking isolated
 * cells as cuts leaves open faces through which the backdrop can show. */
export function demoGroundGrid(flatten:ReadonlySet<number>) {
  type Site={height:number};
  const sites=new Map<string,Site>(),byCell:Site[][]=[];
  for(let v=0;v<demoDimensions.height;v++)for(let h=0;h<demoDimensions.width;h++) {
    const p=demoSpace.location({h,v,z:0}),sample=demoGround(p.x,p.y);
    byCell.push([[1,-1],[1,1],[-1,1],[-1,-1]].map(([dx,dy],corner)=>{
      const key=`${2*p.x+dx},${2*p.y+dy}`,height=4*sample.ground+sample.corners[corner];
      let site=sites.get(key);
      if(!site){site={height};sites.set(key,site);}
      else if(site.height!==height)throw new Error('Inconsistent synthetic terrain corner');
      return site;
    }));
  }
  for(const index of flatten)for(const site of byCell[index])site.height=4*demoDimensions.waterLevel;
  // Lower excess samples until every cell fits one whole-level interval.
  // Heights are bounded nonnegative integers and only decrease in this pass.
  // Each shared object updates all touching cells, preserving seamless joins.
  let changed:boolean;
  do {
    changed=false;
    for(const cell of byCell) {
      const cap=4*(Math.floor(Math.min(...cell.map(s=>s.height))/4)+1);
      for(const site of cell)if(site.height>cap){site.height=cap;changed=true;}
    }
  }while(changed);
  return byCell.map(cell=>{
    const ground=Math.floor(Math.min(...cell.map(s=>s.height))/4);
    return {ground,corners:cell.map(s=>s.height-4*ground)};
  });
}
