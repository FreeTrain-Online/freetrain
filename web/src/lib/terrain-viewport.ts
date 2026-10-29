import type { View } from './content-recipes.ts';
import type { Point } from './map-geometry.ts';
import type { TerrainOperation, TerrainScene } from './terrain-renderer.ts';

// Physical-pixel scales keep sprite phases reusable even with fractional DPR or
// a reduced backing resolution on large windows.
const scales=[.125,.1875,.25,.375,.5,.75,1,1.5,2,3,4,6,8,12,16];
const maximumPixels=16_777_216;
export function terrainRasterZoom(zoom:number):number {
  return scales.reduce((best,scale)=>Math.abs(Math.log(scale/zoom))<Math.abs(Math.log(best/zoom))?scale:best);
}
type Raster={image:OffscreenCanvas;view:View;width:number;height:number;operations:TerrainOperation[];drawn:number};
type Work={key:string;image:OffscreenCanvas;steps:Generator<void,Raster>};

/** Two bounded viewport images. Camera motion presents the last completed image
 * immediately; replacement work yields between ordered build/paint batches.
 * Only a complete image and its matching pick operations are published. */
export class TerrainViewport {
  private raster?:Raster;
  private spare?:OffscreenCanvas;
  private work?:Work;
  private completedKey='';
  constructor(private scene:TerrainScene) {}

  private *render(image:OffscreenCanvas,view:View,width:number,height:number,dpr:number):Generator<void,Raster> {
    const paint=image.getContext('2d')!;
    paint.setTransform(dpr,0,0,dpr,0,0);paint.imageSmoothingEnabled=false;
    paint.fillStyle=this.scene.backgroundColor;paint.fillRect(0,0,width,height);
    yield;
    const operations:TerrainOperation[]=[];
    for(const batch of this.scene.viewportOperations(view,width,height)) {operations.push(...batch);yield;}
    let drawn=0;
    for(let i=0;i<operations.length;i+=64) {
      drawn+=this.scene.paintOperations(paint as unknown as CanvasRenderingContext2D,view,width,height,operations.slice(i,i+64));
      yield;
    }
    return {image,view,width,height,operations,drawn};
  }
  draw(ctx:CanvasRenderingContext2D,view:View,width:number,height:number,dpr:number) {
    // The nearest-scale ratio squared is at most 1.5. Reserve that headroom
    // once per window so effective sprite scale stays fixed throughout a gesture.
    const density=Math.min(dpr,Math.sqrt(maximumPixels/(width*height*1.5)));
    let zoom=terrainRasterZoom(view.zoom*density);
    while(width*height*(zoom/view.zoom)**2>maximumPixels)zoom/=2;
    const ratio=zoom/view.zoom,renderWidth=width*ratio,renderHeight=height*ratio;
    const pixelWidth=Math.max(1,Math.floor(renderWidth)),pixelHeight=Math.max(1,Math.floor(renderHeight));
    const target={x:view.x*ratio,y:view.y*ratio,zoom};
    const key=JSON.stringify([target,width,height,dpr,pixelWidth,pixelHeight]);
    if(key===this.completedKey) {
      // Returning to the displayed camera cancels an unfinished newer view too.
      if(this.work){this.spare=this.work.image;this.work=undefined;}
    }else if(key!==this.work?.key) {
      const image=this.work?.image??this.spare??new OffscreenCanvas(pixelWidth,pixelHeight);
      this.spare=undefined;
      if(image.width!==pixelWidth)image.width=pixelWidth;
      if(image.height!==pixelHeight)image.height=pixelHeight;
      this.work={key,image,steps:this.render(image,target,renderWidth,renderHeight,1)};
      // Give the camera's first frame to the cached image before doing refinement.
    }else if(this.work) {
      const deadline=performance.now()+5;
      do {
        const step=this.work.steps.next();
        if(step.done) {
          this.spare=this.raster?.image;this.raster=step.value;
          this.scene.operations=step.value.operations;
          this.completedKey=this.work.key;this.work=undefined;break;
        }
      }while(performance.now()<deadline);
    }
    const raster=this.raster;
    if(raster) {
      const scale=view.zoom/raster.view.zoom;
      ctx.imageSmoothingEnabled=false;
      ctx.drawImage(raster.image,view.x-raster.view.x*scale,view.y-raster.view.y*scale,raster.width*scale,raster.height*scale);
    }
    return {ready:!!raster,pending:!!this.work,drawn:raster?.drawn??0,renderZoom:zoom/dpr};
  }
  pick(p:Point,view:View) {
    const raster=this.raster;if(!raster)return;
    const scale=view.zoom/raster.view.zoom,x=(p.x-view.x)/scale+raster.view.x,y=(p.y-view.y)/scale+raster.view.y;
    // Newly exposed background has no visible terrain yet. Do not pick an
    // offscreen overhang retained in the old image's operation array.
    if(x<0||y<0||x>=raster.width||y>=raster.height)return;
    return this.scene.pickOperations(p,view,raster.operations);
  }
}
