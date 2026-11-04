import type { Position } from '../generated/contracts.ts';
import type { Recipe, RenderCatalog, View } from './content-recipes.ts';
import type { TerrainOperation, TerrainScene } from './terrain-renderer.ts';
import type { Point } from './map-geometry.ts';
import { compareTrainLayers, movingCarLayer, type MovingCar } from './train-motion.ts';
import type {TerrainOperationIndex} from './terrain-operation-index.ts';

type Bounds={left:number;top:number;right:number;bottom:number};
const overlaps=(a:Bounds,b:Bounds)=>a.left<=b.right&&a.right>=b.left&&a.top<=b.bottom&&a.bottom>=b.top;
const union=(a:Bounds,b:Bounds):Bounds=>({left:Math.min(a.left,b.left),top:Math.min(a.top,b.top),right:Math.max(a.right,b.right),bottom:Math.max(a.bottom,b.bottom)});
/** Replays damaged sprite regions in source order. The retained terrain raster
 * remains unchanged. A spatial index bounds work to overlapping operations;
 * completed paint and alpha picks share these exact fractional operations. */
export class TrainCompositor {
  private source?:readonly TerrainOperation[];
  private scene?:TerrainScene;
  private spatial?:TerrainOperationIndex;
  private buckets=new Map<string,Set<TerrainOperation>>();
  private order=new Map<TerrainOperation,number>();
  private cars=new Map<string,TerrainOperation>();
  private dynamic:TerrainOperation[]=[];
  private hidden=new Set<string>();
  private centerX=0;
  private width=0;
  private scratch?:OffscreenCanvas;
  private maskedImage?:OffscreenCanvas;
  private catalog?:RenderCatalog;
  private recipes=new Map<string,Recipe>();
  private positions=new Map<string,Position>();
  private keys(b:Bounds) {const keys:string[]=[];for(let y=Math.floor(b.top/64);y<=Math.floor(b.bottom/64);y++)for(let x=Math.floor(b.left/64);x<=Math.floor(b.right/64);x++)keys.push(`${x},${y}`);return keys;}
  private query(b:Bounds) {if(this.spatial)return this.spatial.query(b);const result=new Set<TerrainOperation>();for(const key of this.keys(b))for(const op of this.buckets.get(key)??[])if(overlaps(this.scene!.boundsFor(op),b))result.add(op);return [...result];}
  private setSource(scene:TerrainScene,source:readonly TerrainOperation[],width:number,centerX:number,spatial?:TerrainOperationIndex) {
    if(spatial) {
      if(!this.spatial){this.buckets.clear();this.order.clear();}
      this.spatial=spatial;this.scene=scene;this.source=source;this.width=width;this.centerX=centerX;this.cars=spatial.cars;
      return;
    }
    if(this.spatial){this.spatial=undefined;this.source=undefined;this.cars=new Map();}
    if(this.scene===scene&&this.source===source)return;
    this.scene=scene;this.width=width;this.centerX=centerX;
    // Most operations retain their identity after a train move. Keep their
    // spatial entries instead of rebuilding the entire zoomed-out terrain index.
    const present=new Set(source),previous=new Set(this.source);
    for(const op of previous)if(!present.has(op))for(const key of this.keys(scene.boundsFor(op))) {
      const bucket=this.buckets.get(key);bucket?.delete(op);if(!bucket?.size)this.buckets.delete(key);
    }
    this.source=source;this.order.clear();this.cars.clear();
    source.forEach((op,index)=>{
      this.order.set(op,index);
      if(op.target.kind==='train')this.cars.set(op.target.carId,op);
      if(!previous.has(op))for(const key of this.keys(scene.boundsFor(op))){const bucket=this.buckets.get(key)??new Set<TerrainOperation>();bucket.add(op);this.buckets.set(key,bucket);}
    });
  }
  private rank(op:TerrainOperation):number {
    let p=op.target.position;
    if(this.dynamic.includes(op)&&'layer' in op)p={x:Math.round(op.layer.position[0]+this.centerX),y:Math.round(op.layer.position[1]),z:p.z};
    const x=p.x-this.centerX,column=(p.y-x)*this.width+Math.floor((x+p.y)/2);
    let pass=0;
    if(op.target.kind==='train')pass=30;
    else if(op.target.kind==='railway')pass=op.target.component==='speedLimit'?60:op.target.component==='accessory'?((('layer' in op?op.layer.frame.stage:20)??20)>=30?50:10):((('layer' in op?op.layer.frame.stage:20)??20)>=30?40:20);
    return column*100000+p.z*100+pass;
  }
  private compose(ops:TerrainOperation[],dynamic:TerrainOperation[]) {
    const staticOps=ops.filter(op=>op.target.kind!=='train'||!this.hidden.has(op.target.carId)).sort((a,b)=>this.spatial?this.spatial.compare(a,b):this.order.get(a)!-this.order.get(b)!);
    for(const op of dynamic){const rank=this.rank(op);let i=0;while(i<staticOps.length&&this.rank(staticOps[i])<=rank)i++;staticOps.splice(i,0,op);}
    return staticOps;
  }
  private isBed(op:TerrainOperation) {
    return op.target.kind==='terrain'&&op.target.surface==='ground'||
      op.target.kind==='railway'&&op.target.component==='rail'&&'layer' in op&&(op.layer.frame.stage??20)<30;
  }
  private isMoving(op:TerrainOperation) {return op.target.kind==='train'&&this.hidden.has(op.target.carId);}
  private maskedBy(op:TerrainOperation,car:TerrainOperation) {
    if(!this.isMoving(car)||car.target.position.z!==op.target.position.z)return false;
    return this.isBed(op)||this.isMoving(op)&&'layer' in op&&'layer' in car&&compareTrainLayers(op.layer,car.layer)<0;
  }
  /** Keep source terrain/structure order, while roadbeds stay below whole cars
   * and overlapping cars use continuous depth instead of rounded voxel slots.
   * Pixel masks leave the surrounding terrain and structural foregrounds intact. */
  paintOperations(ctx:CanvasRenderingContext2D,view:View,width:number,height:number,ops:readonly TerrainOperation[]) {
    // Flat roadbeds are below whole moving cars, irrespective of their voxel
    // traversal slots. Paint them once, then the cars in continuous depth order.
    // Structural foregrounds and mixed elevations keep the general mask path.
    const moving=ops.filter(op=>this.isMoving(op));
    const level=moving[0]?.target.position.z;
    if(moving.length&&moving.every(car=>car.target.position.z===level)&&ops.every(op=>this.isMoving(op)||this.isBed(op)&&op.target.position.z===level)) {
      this.scene!.paintOperations(ctx,view,width,height,ops.filter(op=>!this.isMoving(op)));
      this.scene!.paintOperations(ctx,view,width,height,moving.sort((a,b)=>'layer' in a&&'layer' in b?compareTrainLayers(a.layer,b.layer):0));
      return;
    }
    const cars:TerrainOperation[]=[];
    for(const op of ops) {
      const covered=cars.filter(car=>this.maskedBy(op,car)&&overlaps(this.scene!.boundsFor(car),this.scene!.boundsFor(op)));
      if(covered.length) {
        const b=this.scene!.boundsFor(op),left=Math.max(0,Math.floor(view.x+b.left*view.zoom)-1),top=Math.max(0,Math.floor(view.y+b.top*view.zoom)-1);
        const w=Math.min(width,Math.ceil(view.x+b.right*view.zoom)+1)-left,h=Math.min(height,Math.ceil(view.y+b.bottom*view.zoom)+1)-top;
        if(w<=0||h<=0)continue;
        const local={...view,x:view.x-left,y:view.y-top};
        const image=this.maskedImage??=new OffscreenCanvas(w,h);
        if(image.width<w)image.width=w;if(image.height<h)image.height=h;
        const paint=image.getContext('2d')!;paint.setTransform(1,0,0,1,0,0);paint.clearRect(0,0,w,h);
        this.scene!.paintOperations(paint as unknown as CanvasRenderingContext2D,local,w,h,[op]);
        paint.globalCompositeOperation='destination-out';
        this.scene!.paintOperations(paint as unknown as CanvasRenderingContext2D,local,w,h,covered);
        paint.globalCompositeOperation='source-over';ctx.drawImage(image,0,0,w,h,left,top,w,h);
      }else this.scene!.paintOperations(ctx,view,width,height,[op]);
      if(this.isMoving(op))cars.push(op);
    }
  }
  draw(ctx:CanvasRenderingContext2D,view:View,width:number,height:number,scene:TerrainScene,source:readonly TerrainOperation[],catalog:RenderCatalog,cars:MovingCar[],mapWidth:number,centerX:number,raster?:{view:View;width:number;height:number;index?:TerrainOperationIndex}) {
    this.setSource(scene,source,mapWidth,centerX,raster?.index);this.dynamic=[];this.hidden.clear();this.positions.clear();
    if(this.catalog!==catalog){this.catalog=catalog;this.recipes=new Map(catalog.renderRecipes.map(r=>[r.id,r]));}
    const damage:Bounds[]=[];
    for(const car of cars) {
      const original=this.cars.get(car.carId);if(!original||!('layer' in original))continue;
      const recipe=this.recipes.get(original.layer.recipeId);if(!recipe)continue;
      const layer=movingCarLayer(recipe,car,centerX),op={target:original.target,layer};
      this.dynamic.push(op);this.hidden.add(car.carId);
      this.positions.set(car.carId,{x:layer.position[0]+centerX,y:layer.position[1],z:layer.position[2]});
      const b=union(scene.boundsFor(original),scene.boundsFor(op));
      // A two-pixel margin covers nearest-neighbor rasterization at all scales.
      const pad=2/view.zoom,box={left:b.left-pad,top:b.top-pad,right:b.right+pad,bottom:b.bottom+pad};
      if(view.x+box.right*view.zoom<0||view.y+box.bottom*view.zoom<0||view.x+box.left*view.zoom>width||view.y+box.top*view.zoom>height)continue;
      damage.push(box);
    }
    // Merge overlapping damage once, so every pixel has one consistent replay.
    for(let i=0;i<damage.length;i++)for(let j=i+1;j<damage.length;) {
      if(overlaps(damage[i],damage[j])){damage[i]=union(damage[i],damage[j]);damage.splice(j,1);i=-1;break;}else j++;
    }
    let operations=0;
    const density=ctx.getTransform().a;
    const rasterRegion=raster??{view:{x:view.x*density,y:view.y*density,zoom:view.zoom*density},width:Math.ceil(width*density),height:Math.ceil(height*density)};
    for(const box of damage) {
      // Render a halo around the copied rectangle. Clipping a stroked terrain
      // polygon at the damage edge can otherwise change antialias coverage.
      const v=rasterRegion.view,left=Math.max(0,Math.floor(v.x+box.left*v.zoom)),top=Math.max(0,Math.floor(v.y+box.top*v.zoom));
      const right=Math.min(rasterRegion.width,Math.ceil(v.x+box.right*v.zoom)),bottom=Math.min(rasterRegion.height,Math.ceil(v.y+box.bottom*v.zoom));
      if(right<=left||bottom<=top)continue;
      const halo=2,w=right-left,h=bottom-top,region={left:(left-halo-v.x)/v.zoom,top:(top-halo-v.y)/v.zoom,right:(right+halo-v.x)/v.zoom,bottom:(bottom+halo-v.y)/v.zoom};
      const ops=this.compose(this.query(region),this.dynamic.filter(o=>overlaps(scene.boundsFor(o),region)));operations+=ops.length;
      const scratch=this.scratch??=new OffscreenCanvas(w+2*halo,h+2*halo);
      if(scratch.width<w+2*halo)scratch.width=w+2*halo;if(scratch.height<h+2*halo)scratch.height=h+2*halo;
      const paint=scratch.getContext('2d')!;paint.setTransform(1,0,0,1,0,0);paint.fillStyle=scene.backgroundColor;paint.fillRect(0,0,w+2*halo,h+2*halo);
      this.paintOperations(paint as unknown as CanvasRenderingContext2D,{x:v.x-left+halo,y:v.y-top+halo,zoom:v.zoom},w+2*halo,h+2*halo,ops);
      // Preserve the retained raster's scale and phase while the camera moves.
      const scale=view.zoom/v.zoom;ctx.imageSmoothingEnabled=false;
      ctx.drawImage(scratch,halo,halo,w,h,view.x+(left-v.x)*scale,view.y+(top-v.y)*scale,w*scale,h*scale);
    }
    return {cars:this.dynamic.length,operations};
  }
  position(carId:string) {return this.positions.get(carId);}
  pick(point:Point,view:View) {
    if(!this.scene)return;
    const x=(point.x-view.x)/view.zoom,y=(point.y-view.y)/view.zoom,box={left:x,top:y,right:x,bottom:y};
    const ops=this.compose(this.query(box),this.dynamic.filter(o=>overlaps(this.scene!.boundsFor(o),box)));
    const hits=new Map<TerrainOperation,ReturnType<TerrainScene['pickOperations']>>();
    const hit=(op:TerrainOperation)=>{if(!hits.has(op))hits.set(op,this.scene!.pickOperations(point,view,[op]));return hits.get(op);};
    for(let i=ops.length-1;i>=0;i--) {
      const op=ops[i];
      if(ops.some((car,j)=>j<i&&this.maskedBy(op,car)&&hit(car)))continue;
      const target=hit(op);if(target)return target;
    }
  }
}
