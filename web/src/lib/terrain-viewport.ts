import type { View } from './content-recipes.ts';
import type { Point } from './map-geometry.ts';
import type { TerrainOperation, TerrainScene } from './terrain-renderer.ts';
import type { RailwayPickPolicy } from './railway-renderer.ts';

// Physical-pixel scales keep sprite phases reusable even with fractional DPR or
// a reduced backing resolution on large windows.
const scales=[.125,.1875,.25,.375,.5,.75,1,1.5,2,3,4,6,8,12,16];
const maximumPixels=16_777_216;
// Half a viewport on each horizontal side, a full viewport above and below.
// Refresh after using half of the margin in either direction.
const overscanX=.5,overscanY=1;
// Zoom-out reveals new terrain before paying for the full panning buffer.
const revealOverscan=.125;
export function terrainRasterZoom(zoom:number):number {
  return scales.reduce((best,scale)=>Math.abs(Math.log(scale/zoom))<Math.abs(Math.log(best/zoom))?scale:best);
}
type Raster={image:OffscreenCanvas;scene:TerrainScene;view:View;cameraZoom:number;width:number;height:number;operations:TerrainOperation[];drawn:number};
type Region={view:View;width:number;height:number};
type Work=Region&{key:string;image:OffscreenCanvas;steps:Generator<void,Raster>};

function covers(region:Region,view:View,width:number,height:number,insetX=0,insetY=0) {
  const scale=region.view.zoom/view.zoom;
  const left=region.view.x-view.x*scale,top=region.view.y-view.y*scale;
  const xInset=width*scale*insetX,yInset=height*scale*insetY;
  return left>=xInset&&top>=yInset&&left+width*scale<=region.width-xInset&&top+height*scale<=region.height-yInset;
}

/** Two bounded, overscanned viewport images. Camera motion presents the last completed image
 * immediately; replacement work yields between ordered build/paint batches.
 * Only a complete image and its matching pick operations are published. */
export class TerrainViewport {
  private raster?:Raster;
  private spare?:OffscreenCanvas;
  private work?:Work;
  private completedKey='';
  constructor(private scene:TerrainScene) {}

  setScene(scene:TerrainScene) {
    // Reuse the displayed image until the new geometry is fully rendered.
    // Its picks must keep using the scene that produced those visible pixels.
    if(this.work)this.spare=this.work.image;
    this.work=undefined;this.completedKey='';this.scene=scene;
  }

  private *render(image:OffscreenCanvas,view:View,width:number,height:number,dpr:number,cameraZoom:number):Generator<void,Raster> {
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
    return {image,scene:this.scene,view,cameraZoom,width,height,operations,drawn};
  }
  draw(ctx:CanvasRenderingContext2D,view:View,width:number,height:number,dpr:number) {
    // The nearest-scale ratio squared is at most 1.5. Reserve that headroom
    // once per window so effective sprite scale stays fixed throughout a gesture.
    const paddedWidth=width*(1+2*overscanX),paddedHeight=height*(1+2*overscanY);
    const density=Math.min(dpr,Math.sqrt(maximumPixels/(paddedWidth*paddedHeight*1.5)));
    let zoom=terrainRasterZoom(view.zoom*density);
    while(paddedWidth*paddedHeight*(zoom/view.zoom)**2>maximumPixels)zoom/=2;
    const ratio=zoom/view.zoom;
    // Translation is handled by coverage, so small pans keep the same work.
    const key=JSON.stringify([view.zoom,width,height,dpr,zoom]);
    if(key===this.completedKey&&this.raster&&covers(this.raster,view,width,height,overscanX/2,overscanY/2)) {
      // Returning to the safe interior cancels an obsolete edge refresh too.
      if(this.work){this.spare=this.work.image;this.work=undefined;}
    }else if(!this.work||key!==this.work.key||!covers(this.work,view,width,height)) {
      const reveal=this.raster&&view.zoom<this.raster.cameraZoom;
      const renderWidth=reveal?width*(1+2*revealOverscan):paddedWidth;
      const renderHeight=reveal?height*(1+2*revealOverscan):paddedHeight;
      const pixelWidth=Math.max(1,Math.floor(renderWidth*ratio)),pixelHeight=Math.max(1,Math.floor(renderHeight*ratio));
      const target={x:view.x*ratio+(pixelWidth-width*ratio)/2,y:view.y*ratio+(pixelHeight-height*ratio)/2,zoom};
      const image=this.work?.image??this.spare??new OffscreenCanvas(pixelWidth,pixelHeight);
      this.spare=undefined;
      if(image.width!==pixelWidth)image.width=pixelWidth;
      if(image.height!==pixelHeight)image.height=pixelHeight;
      this.work={key,image,view:target,width:pixelWidth,height:pixelHeight,steps:this.render(image,target,pixelWidth,pixelHeight,1,view.zoom)};
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
    // A completed reveal still needs frames to expand into the panning buffer.
    const needsBuffer=raster&&!covers(raster,view,width,height,overscanX/2,overscanY/2);
    return {ready:!!raster,pending:!!this.work||!!needsBuffer,drawn:raster?.drawn??0,renderZoom:zoom/dpr};
  }
  pick(p:Point,view:View) {
    const raster=this.raster;if(!raster)return;
    const scale=view.zoom/raster.view.zoom,x=(p.x-view.x)/scale+raster.view.x,y=(p.y-view.y)/scale+raster.view.y;
    // Newly exposed background has no visible terrain yet. Do not pick an
    // offscreen overhang retained in the old image's operation array.
    if(x<0||y<0||x>=raster.width||y>=raster.height)return;
    return raster.scene.pickOperations(p,view,raster.operations);
  }
  pickLocation(p:Point,view:View,policy:RailwayPickPolicy) {
    const raster=this.raster;if(!raster)return;
    const scale=view.zoom/raster.view.zoom,x=(p.x-view.x)/scale+raster.view.x,y=(p.y-view.y)/scale+raster.view.y;
    if(x<0||y<0||x>=raster.width||y>=raster.height)return;
    return raster.scene.pickLocation(p,view,policy);
  }
}
