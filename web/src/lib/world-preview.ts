import { WorldSpace, offset, type Point } from './map-geometry.ts';

export type PreviewMode = 'topDown' | 'isometric';
export interface TerrainOverview {
  width:number; height:number; step:number; samples:string;
  metrics:{cells:number;wetCells:number;forestCells:number;clearFlatCells:number;largestBuildableArea:number;minimumQuarterHeight:number;maximumQuarterHeight:number};
}
export interface WorldCandidate {
  id:string; requestHash:string; candidateHash:string; name:string;
  dimensions:{h:number;v:number;z:number};waterLevel:number;saveBytes:number;snapshotBytes:number;expiresAt:string;
  ground?:number[];imagePreview?:string;overview:TerrainOverview;
  geography?:{metrics:{seaCells:number;landCells:number;seaComponents:number;landComponents:number;waterPercent:number;lakeCells:number;lakeComponents:number;riverCount:number};seaMask:string;forestMask?:string;startingMask?:string;forests?:{eligibleCells:number;maskCells:number;forestCells:number;trees:number;reservedCells:number;largestFlatArea:number}};
}
/** Compass vectors use the same canonical XY axes as the preview geometry. */
export function compassVector(mode:PreviewMode, heading:0|2|4|6):Point {
  const p=offset(heading);
  return mode==='topDown'?{x:24*p.x,y:24*p.y}:{x:24*(p.x+p.y),y:12*(p.y-p.x)};
}
export function previewCell(mode:PreviewMode,h:number,v:number,ground:number,corners:readonly number[],step:number) {
  // The bounded grid is a coarse H/V overview. At step=1 these are the exact
  // source cell centers/corners; larger steps retain proportions and height scale.
  const space=new WorldSpace(1,1,64),p=space.location({h,v,z:0});
  const center=mode==='topDown'?{x:16*p.x,y:16*p.y}:space.center(p);
  const offsets=mode==='topDown'?[{x:8,y:-8},{x:8,y:8},{x:-8,y:8},{x:-8,y:-8}]:[{x:0,y:-8},{x:16,y:0},{x:0,y:8},{x:-16,y:0}];
  const base=offsets.map(q=>({x:center.x+q.x,y:center.y+q.y}));
  const top=base.map((q,i)=>({x:q.x,y:q.y-(mode==='isometric'?(ground+corners[i]/4)*16/step:0)}));
  return {base,top};
}
export function renderWorldPreview(candidate:WorldCandidate,mode:PreviewMode):string {
  const {width,height,step,samples}=candidate.overview;
  if(width<1||width>128||height<1||height>128||step<1)throw new Error('Invalid overview dimensions');
  const bytes=Uint8Array.from(atob(samples),c=>c.charCodeAt(0));
  if(bytes.length!==width*height*4)throw new Error('Invalid overview samples');
  const cells=[];let xmin=Infinity,ymin=Infinity,xmax=-Infinity,ymax=-Infinity;
  for(let v=0;v<height;v++)for(let h=0;h<width;h++) {
    const i=4*(v*width+h),flags=bytes[i+3],wet=Boolean(flags&1),ground=wet?candidate.waterLevel:bytes[i];
    const q=bytes[i+1]|bytes[i+2]<<8,corners=wet?[0,0,0,0]:[q>>12,q>>8&15,q>>4&15,q&15];
    const {base,top}=previewCell(mode,h,v,ground,corners,step);
    for(const p of [...base,...top]){xmin=Math.min(xmin,p.x);xmax=Math.max(xmax,p.x);ymin=Math.min(ymin,p.y);ymax=Math.max(ymax,p.y);}
    const shade=Math.round(70+140*ground/candidate.dimensions.z);
    const color=wet?'#0072bc':flags&4?'#eec652':flags&2?'#185226':`rgb(${Math.round(shade*.7)},${shade},${Math.round(shade*.5)})`;
    cells.push({base,top,color,wet});
  }
  const canvas=document.createElement('canvas'),ctx=canvas.getContext('2d');
  if(!ctx)throw new Error('Preview canvas unavailable');
  const scale=Math.min(2,1000/(xmax-xmin+8),600/(ymax-ymin+8));
  canvas.width=Math.ceil((xmax-xmin+8)*scale);canvas.height=Math.ceil((ymax-ymin+8)*scale);
  ctx.scale(scale,scale);ctx.translate(4-xmin,4-ymin);
  const polygon=(points:Point[],color:string)=>{
    ctx.beginPath();points.forEach((p,i)=>i===0?ctx.moveTo(p.x,p.y):ctx.lineTo(p.x,p.y));ctx.closePath();
    ctx.fillStyle=color;ctx.strokeStyle=color;ctx.lineWidth=.35;ctx.fill();ctx.stroke();
  };
  for(const {base,top,color,wet} of cells) {
    if(mode==='isometric') {
      polygon([base[1],base[2],top[2],top[1]],wet?'#075985':'#566747');
      polygon([base[2],base[3],top[3],top[2]],wet?'#0c4a6e':'#425337');
    }
    polygon(top,color);
  }
  return canvas.toDataURL('image/png');
}
