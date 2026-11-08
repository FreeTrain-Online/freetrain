import type { View } from './content-recipes.ts';
import type { Point } from './map-geometry.ts';
import type { TerrainOperation, TerrainScene } from './terrain-renderer.ts';
import type { RailwayPickPolicy } from './railway-renderer.ts';
import {TerrainOperationIndex} from './terrain-operation-index.ts';

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
type Raster={image:OffscreenCanvas;scene:TerrainScene;view:View;cameraZoom:number;width:number;height:number;operations:readonly TerrainOperation[];index:TerrainOperationIndex;drawn:number};
type Region={view:View;width:number;height:number};
type Bounds={left:number;top:number;right:number;bottom:number};
type Work=Region&{key:string;scene:TerrainScene;image:OffscreenCanvas;steps:Generator<void,Raster>};

function covers(region:Region,view:View,width:number,height:number,insetX=0,insetY=0) {
  const scale=region.view.zoom/view.zoom;
  const left=region.view.x-view.x*scale,top=region.view.y-view.y*scale;
  const xInset=width*scale*insetX,yInset=height*scale*insetY;
  return left>=xInset&&top>=yInset&&left+width*scale<=region.width-xInset&&top+height*scale<=region.height-yInset;
}

/** Bounded, overscanned viewport images. Camera motion presents the last completed image
 * immediately; replacement work yields between ordered build/paint batches.
 * Only a complete image and its matching pick operations are published. */
export class TerrainViewport {
  private raster?:Raster;
  private spare?:OffscreenCanvas;
  private work?:Work;
  private completedKey='';
  private updateKind='full';
  constructor(private scene:TerrainScene) {}

  setScene(scene:TerrainScene) {
    // Reuse the displayed image until the new geometry is fully rendered.
    // Its picks must keep using the scene that produced those visible pixels.
    // Local publications must not starve a zoom/pan rebuild. Finish its immutable
    // source scene, then patch it to the latest publication before displaying it.
    const columns=this.work&&scene.changedColumns(this.work.scene);
    if(this.work&&(!columns||columns.length>2048)) {
      this.spare=this.work.image;this.work=undefined;
    }
    const patched=this.patch(scene);this.scene=scene;this.updateKind=patched?'local':'full';
    if(!patched)this.completedKey='';
  }

  private patch(scene:TerrainScene):boolean {
    const old=this.raster;if(!old)return false;
    const columns=scene.changedColumns(old.scene);if(!columns||columns.length>2048)return false;
    if(!columns.length){old.index.replace(scene,new Map());this.raster={...old,scene};scene.retainViewportOperations(old.operations);return true;}
    const changed=new Map<number,readonly TerrainOperation[]>();let drawn=old.drawn;
    let left=Infinity,top=Infinity,right=-Infinity,bottom=-Infinity;
    const damage=(op:TerrainOperation,owner:TerrainScene)=>{
      const b=owner.boundsFor(op);left=Math.min(left,b.left);top=Math.min(top,b.top);right=Math.max(right,b.right);bottom=Math.max(bottom,b.bottom);
    };
    const visible=(op:TerrainOperation,owner=scene)=>{
      const b=owner.boundsFor(op),v=old.view;
      return v.x+b.right*v.zoom>=0&&v.y+b.bottom*v.zoom>=0&&v.x+b.left*v.zoom<=old.width&&v.y+b.top*v.zoom<=old.height;
    };
    for(const i of columns){
      for(const op of old.index.column(i)){damage(op,old.scene);if(visible(op,old.scene))drawn--;}
      const ops=scene.columnOperations(i).filter(op=>visible(op));changed.set(i,ops);drawn+=ops.length;
      for(const op of ops)damage(op,scene);
    }
    const publish=()=>{
      old.index.replace(scene,changed);const operations=old.index.operations;
      this.raster={...old,scene,operations,drawn};scene.retainViewportOperations(operations);
    };
    if(left===Infinity){publish();return true;}
    // Repaint all overlapping layers, including unchanged forests/cliffs, in
    // source order. Old AND new bounds clear pixels from lowered/removed art.
    const v=old.view,clip:Bounds={left:Math.max(0,Math.floor(v.x+left*v.zoom)-2),top:Math.max(0,Math.floor(v.y+top*v.zoom)-2),
      right:Math.min(old.width,Math.ceil(v.x+right*v.zoom)+2),bottom:Math.min(old.height,Math.ceil(v.y+bottom*v.zoom)+2)};
    if(clip.right<=clip.left||clip.bottom<=clip.top){publish();return true;}
    if((clip.right-clip.left)*(clip.bottom-clip.top)>old.width*old.height/3)return false;
    // The complete local repaint is synchronous. Updating the retained image
    // in place avoids copying the overscanned viewport for every moving train.
    publish();
    const paint=old.image.getContext('2d')!;paint.setTransform(1,0,0,1,0,0);paint.imageSmoothingEnabled=false;
    paint.save();paint.beginPath();paint.rect(clip.left,clip.top,clip.right-clip.left,clip.bottom-clip.top);paint.clip();
    paint.fillStyle=scene.backgroundColor;paint.fillRect(clip.left,clip.top,clip.right-clip.left,clip.bottom-clip.top);
    const operations=old.index.query({left:(clip.left-v.x)/v.zoom,top:(clip.top-v.y)/v.zoom,right:(clip.right-v.x)/v.zoom,bottom:(clip.bottom-v.y)/v.zoom});
    scene.paintOperations(paint as unknown as CanvasRenderingContext2D,v,old.width,old.height,operations,[clip]);paint.restore();
    return true;
  }

  private *render(scene:TerrainScene,image:OffscreenCanvas,view:View,width:number,height:number,dpr:number,cameraZoom:number):Generator<void,Raster> {
    const paint=image.getContext('2d')!;
    paint.setTransform(dpr,0,0,dpr,0,0);paint.imageSmoothingEnabled=false;
    paint.fillStyle=scene.backgroundColor;paint.fillRect(0,0,width,height);
    yield;
    const operations:TerrainOperation[]=[];
    for(const batch of scene.viewportOperations(view,width,height)) {operations.push(...batch);yield;}
    let drawn=0;
    for(let i=0;i<operations.length;i+=64) {
      drawn+=scene.paintOperations(paint as unknown as CanvasRenderingContext2D,view,width,height,operations.slice(i,i+64));
      yield;
    }
    return {image,scene,view,cameraZoom,width,height,operations,index:new TerrainOperationIndex(scene,operations),drawn};
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
      // Reveal newly exposed terrain before filling the much larger panning
      // buffer. This matters for cold views and long pans as well as zoom-out.
      const reveal=!this.raster||view.zoom!==this.raster.cameraZoom||!covers(this.raster,view,width,height);
      const renderWidth=reveal?width*(1+2*revealOverscan):paddedWidth;
      const renderHeight=reveal?height*(1+2*revealOverscan):paddedHeight;
      const pixelWidth=Math.max(1,Math.floor(renderWidth*ratio)),pixelHeight=Math.max(1,Math.floor(renderHeight*ratio));
      const target={x:view.x*ratio+(pixelWidth-width*ratio)/2,y:view.y*ratio+(pixelHeight-height*ratio)/2,zoom};
      const image=this.work?.image??this.spare??new OffscreenCanvas(pixelWidth,pixelHeight);
      this.spare=undefined;
      if(image.width!==pixelWidth)image.width=pixelWidth;
      if(image.height!==pixelHeight)image.height=pixelHeight;
      this.work={key,scene:this.scene,image,view:target,width:pixelWidth,height:pixelHeight,steps:this.render(this.scene,image,target,pixelWidth,pixelHeight,1,view.zoom)};
      // Give the camera's first frame to the cached image before doing refinement.
    }else if(this.work) {
      // Leave half a 60 Hz frame for presentation and incoming publications.
      const deadline=performance.now()+8;
      do {
        const step=this.work.steps.next();
        if(step.done) {
          this.spare=this.raster?.image;this.raster=step.value;
          this.completedKey=this.work.key;this.work=undefined;
          if(this.raster.scene!==this.scene&&!this.patch(this.scene))this.completedKey='';
          this.raster.scene.retainViewportOperations(this.raster.operations);
          break;
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
    const settled=!!raster&&this.completedKey===key&&raster.scene===this.scene&&covers(raster,view,width,height);
    return {ready:!!raster,settled,pending:!!this.work||!!needsBuffer||this.completedKey!==key,drawn:raster?.drawn??0,renderZoom:(raster?.view.zoom??zoom)/dpr,updateKind:this.updateKind};
  }
  get presentation() {return this.raster?{scene:this.raster.scene,operations:this.raster.operations,index:this.raster.index,view:this.raster.view,width:this.raster.width,height:this.raster.height}:undefined;}
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
