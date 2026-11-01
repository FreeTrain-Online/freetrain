import { terrainLookup, terrainChangedColumns } from './terrain-state.ts';
// Stable ExAV 2.1.1.12: QuarterViewDrawer, MountainVoxel.drawGround/drawTrees,
// TerraceVoxel.draw and ColorMap. Original authors; LGPL-2.1-or-later.
import type { Position, Snapshot, TerrainOccupant } from '../generated/contracts.ts';
import { WorldSpace, move, type Point } from './map-geometry.ts';
import { RecipeImageCache, type RenderCatalog, type Layer, type Variant, type ReadyScene, type View } from './content-recipes.ts';
import { isTerrainWorld, isMixedWorld, validSnapshot } from './world-identity.ts';
import { RailwayRenderPlan, pickRailwayLocation, type RailwayTarget, type RailwayPickPolicy } from './railway-renderer.ts';
import paletteData from './terrain-palettes.json' with { type: 'json' };
import type { ReadState } from './session-client.ts';
type Occupant=NonNullable<ReadState['terrain']>['occupants'][number];
export interface TerrainTarget { kind:'terrain'; id:string; position:Position; surface:'ground'|'mountain'|'water'|'underground'|'terrace'|'forest'|'creek'|'cliff' }
export type SceneTarget=TerrainTarget|RailwayTarget;
type Operation = { target:SceneTarget; points:Point[]; color:string; edge?:string } | {target:SceneTarget; layer:Layer};
export type TerrainOperation = Operation;
type Bounds={left:number;top:number;right:number;bottom:number};
const diamond=[{x:16,y:0},{x:32,y:8},{x:16,y:16},{x:0,y:8}];
export function hsv([H,S,V]:readonly number[]):number[] {
  const h=H/40,s=S/240,v=V/240,i=Math.trunc(h),f=i%2?h-i:1-(h-i);
  const m=Math.trunc(256*v*(1-s)),n=Math.trunc(256*v*(1-s*f)),o=Math.trunc(256*v);
  return [[o,n,m],[n,o,m],[m,o,n],[m,n,o],[n,m,o],[o,m,n]][i%6];
}
export function terrainColor(rgb:readonly number[],night:boolean):string {return '#'+rgb.map(n=>(night?Math.trunc(n/4):n).toString(16).padStart(2,'0')).join('');}
export function mountainColor(o:Pick<TerrainOccupant,'ne'|'se'|'sw'|'nw'>,variant:Variant,underwater:boolean,cut=false):string {
  const colors=paletteData.palettes[underwater?'water':variant.season];
  const dv=o.sw-o.ne,dh=o.se-o.nw;
  const index=cut?3:Math.min(colors.length-1,Math.trunc(colors.length*Math.abs(80*(dh-dv)-20*(dh+dv)+2560)/Math.sqrt((dh*dh+dv*dv+512)*2*(6400+400+6400))));
  // The source cut face uses the palette directly, without night mapping.
  return terrainColor(hsv(colors[index]),cut?false:variant.night);
}
export function mountainPoints(o:Pick<TerrainOccupant,'ne'|'se'|'sw'|'nw'>):Point[] {
  return [{x:16,y:-4*o.ne},{x:32,y:8-4*o.se},{x:16,y:16-4*o.sw},{x:0,y:8-4*o.nw}];
}
export function treeOffset(o:Pick<TerrainOccupant,'ne'|'sw'>,tree:{x:number;y:number}):Point {
  const h=o.ne-o.sw;
  return {x:tree.x,y:Math.trunc(tree.y*(h+4)/4)+(h>0?-8:2)};
}
export function inPolygon(p:Point,points:readonly Point[]):boolean {
  let inside=false;
  for(let i=0,j=points.length-1;i<points.length;j=i++) {
    const a=points[j],b=points[i],cross=(p.x-a.x)*(b.y-a.y)-(p.y-a.y)*(b.x-a.x);
    if(Math.abs(cross)<1e-8&&p.x>=Math.min(a.x,b.x)&&p.x<=Math.max(a.x,b.x)&&p.y>=Math.min(a.y,b.y)&&p.y<=Math.max(a.y,b.y))return true;
    if((a.y>p.y)!==(b.y>p.y)&&p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
  }
  return inside;
}
/** A scene is built from a validated immutable read. No random draws or world
 * mutation. Operations preserve the source v/h/z traversal and sprite anchors. */
export class TerrainScene {
  private bounds=new WeakMap<Operation,Bounds>();
  private polygonKeys=new WeakMap<Operation,string>();
  private polygonRasters=new Map<string,{image:OffscreenCanvas;bytes:number}>();
  private polygonBytes=0;
  private raster?:{image:OffscreenCanvas;spare?:OffscreenCanvas;view:View;width:number;height:number;dpr:number;drawn:number};
  private viewKey='';
  private cellCache=new Map<number,{signature:string;operations:Operation[]}>();
  private cacheIdentity='';
  private read?:Snapshot;
  private space?:WorldSpace;
  private locationPicker?:(p:Point,view:View,policy:RailwayPickPolicy)=>Position|undefined;
  private constructor(public operations:readonly Operation[],private ready:ReadyScene,
    private region?:{build:(h0:number,h1:number,v0:number,v1:number)=>Operation[];width:number;height:number;levels:number;marginX:number;marginY:number},
    private background='#d8e5d9') {}
  get supportsViewport():boolean {return !!this.region;}
  get backgroundColor():string {return this.background;}
  private viewBounds(view:View,width:number,height:number) {
    const r=this.region!;
    return {
      h0:Math.max(0,Math.floor((-view.x/view.zoom-r.marginX)/32)-1),
      h1:Math.min(r.width,Math.ceil(((width-view.x)/view.zoom+r.marginX)/32)+1),
      v0:Math.max(0,Math.floor((-view.y/view.zoom-r.marginY)/8)-1),
      v1:Math.min(r.height,Math.ceil(((height-view.y)/view.zoom+16*(r.levels-1)+r.marginY)/8)+1),
    };
  }
  /** Bounded source-ordered batches let the live viewport yield between cells. */
  *viewportOperations(view:View,width:number,height:number):Generator<readonly Operation[]> {
    if(!this.region){yield this.operations;return;}
    const {h0,h1,v0,v1}=this.viewBounds(view,width,height);
    for(let v=v0;v<v1;v++)for(let h=h0;h<h1;h+=128)
      yield this.region.build(h,Math.min(h+128,h1),v,v+1);
  }
  private prepareView(view:View,width:number,height:number) {
    const r=this.region;if(!r)return;
    // Projection is x=32H+16(V%2), y=8V−16Z. Include the full elevation
    // range and sprite/realized-tree overhang before restricting stored rows.
    const {h0,h1,v0,v1}=this.viewBounds(view,width,height);
    const key=[h0,h1,v0,v1].join(',');
    if(key!==this.viewKey){this.operations=r.build(h0,h1,v0,v1);this.viewKey=key;}
  }
  static async prepare(read:ReadState,catalog:RenderCatalog,cache:RecipeImageCache,variant:Variant,heightCut:number,previous?:TerrainScene):Promise<TerrainScene> {
    // Validation only reads its argument; retain the immutable transport view.
    if(!(isTerrainWorld(read)||isMixedWorld(read))||!validSnapshot(read as Snapshot))throw new Error('Unsupported terrain read');
    if(!Number.isInteger(heightCut)||heightCut<0)throw new Error('Invalid terrain height cut');
    if(catalog.packId!==read.requiredPacks[0].id||catalog.version!==read.requiredPacks[0].version)throw new Error('Terrain catalog mismatch');
    const t=read.terrain!,space=new WorldSpace(read.map.width,read.map.height,read.map.levels);
    const railway=read.mixed?new RailwayRenderPlan(read,catalog,variant):undefined;
    const cacheIdentity=JSON.stringify([read.worldEpoch,read.requiredPacks,read.map,variant,heightCut]);
    const reusable=railway&&previous?.cacheIdentity===cacheIdentity?previous.cellCache:undefined;
    const cellCache=new Map<number,{signature:string;operations:Operation[]}>();
    const finish=(scene:TerrainScene)=>{
      scene.cellCache=cellCache;scene.cacheIdentity=cacheIdentity;scene.read=read as Snapshot;scene.space=space;
      if(previous?.cacheIdentity===cacheIdentity)scene.bounds=previous.bounds;
      scene.locationPicker=(p,view,policy)=>pickRailwayLocation(read,{x:(p.x-view.x)/view.zoom,y:(p.y-view.y)/view.zoom},heightCut,policy);
      return scene;
    };
    let ops:Operation[]=[],layers:Layer[]=[];
    const cells=new Map<number,Operation[]>(),maximumCachedCells=65536,large=read.map.width*read.map.height>16384;
    const recipes=new Map(catalog.renderRecipes.map(r=>[r.id,r]));
    const lookup=terrainLookup(read as Snapshot),entities={get:(id:string)=>lookup.entity(id)},at={get:(key:number)=>lookup.occupant(key)};
    const key=(p:Position)=>{const s=space.storage(p);return s.h<0||s.h>=read.map.width||s.v<0||s.v>=read.map.height||p.z<0||p.z>=read.map.levels?-1:(s.v*read.map.width+s.h)*read.map.levels+p.z;};
    const full=heightCut>=read.map.levels-1,cut=Math.min(heightCut,read.map.levels-1);
    const groundEdge=terrainColor(variant.season==='winter'?[192,200,216]:[187,171,115],variant.night);
    const rimEdge=terrainColor(variant.season==='winter'?[96,104,128]:[96,84,32],variant.night);
    const addSprite=(id:string,frameKey:string|undefined,p:Position,target:TerrainTarget,offset:Point={x:0,y:0})=>{
      const recipe=recipes.get(id),frame=frameKey===undefined?recipe?.frames[0]:recipe?.frames.find(f=>f.key===frameKey);
      if(!frame||frame.status!=='ready'||!frame.masks?.[`${variant.season}/${variant.night?'night':'day'}`])throw new Error('Terrain recipe unavailable: '+id+'/'+frameKey);
      const layer:Layer={entityId:target.id,recipeId:id,frame:{...frame,anchor:[frame.anchor![0]-offset.x,frame.anchor![1]-offset.y]},position:[p.x-space.centerX,p.y,p.z]};
      layers.push(layer);ops.push({target,layer});
    };
    const addPolygon=(p:Position,target:TerrainTarget,points:Point[],color:string,edge?:string)=>{
      const q=space.project(p);ops.push({target,points:points.map(a=>({x:q.x+a.x,y:q.y+a.y})),color,edge});
    };
    const build=(h0:number,h1:number,v0:number,v1:number):Operation[]=>{
    ops=[];layers=[];
    const cacheCells=large&&(h1-h0)*(v1-v0)<=maximumCachedCells;
    for(let v=v0;v<v1;v++)for(let h=h0;h<h1;h++) {
      const index=v*read.map.width+h,cached=cacheCells?cells.get(index):undefined;
      if(cached){ops.push(...cached);continue;}
      const start=ops.length;
      let signature='';
      let reused=false;
      try {
      const cell=t.cells[v*read.map.width+h],base=space.location({h,v,z:cell.ground});
      if(railway) {
        // Terrain faces can overhang a neighbor. Include its corners/ground in
        // the dependency key, alongside this column's exact saved operations.
        signature=JSON.stringify([t.waterLevel,cell,...[base,move(base,0),move(base,2),move(base,4),move(base,6)].map(p=>{
          const s=space.storage(p),surface=at.get(key(p)),backing=at.get(key({...p,z:p.z-1}));
          return [space.containsHorizontal(p)?t.cells[s.v*read.map.width+s.h]:null,surface,backing,surface&&entities.get(surface.entityId)?.definition,backing&&entities.get(backing.entityId)?.definition];
        }),railway.signature(index)]);
        const old=reusable?.get(index);
        if(old?.signature===signature){reused=true;ops.push(...old.operations);for(const op of old.operations)if('layer' in op)layers.push(op.layer);continue;}
      }
      const target=(p:Position,surface:TerrainTarget['surface'],id?:string):TerrainTarget=>({kind:'terrain',id:id??`cell:${h},${v}`,position:{...p},surface});
      const surface=at.get(key(base)),terrace=at.get(key({...base,z:base.z-1}));
      const visibleTerrace=terrace?.kind==='terrace'&&base.z-1<=cut&&(!full||base.z-1>=t.waterLevel);
      if(visibleTerrace) {
        const p={...base,z:base.z-1},tt=target(p,'terrace',terrace.entityId);
        addSprite(entities.get(terrace.entityId)!.definition.recipeId,undefined,p,tt);
      }
      if(cut<cell.ground) {
        // A nontransparent Terrace at the cut owns this voxel; the source
        // draws its sprite instead of an underground chip over it.
        if(!(terrace?.kind==='terrace'&&cut===base.z-1)&&(!railway||railway.drawGround({...base,z:cut},read.map.levels)))addSprite('system:ground/underground',undefined,{...base,z:cut},target({...base,z:cut},'underground'));
        continue;
      }
      if(full&&cell.ground<t.waterLevel) {
        // Seal adjacent sea diamonds at fractional zoom: Canvas antialiasing
        // otherwise leaves pale seams through an opaque, continuous water plane.
        const p={...base,z:t.waterLevel},color=terrainColor([0,114,188],variant.night);
        addPolygon(p,target(p,'water'),diamond,color,color);continue;
      }
      const mountain=surface?.kind==='mountain'?surface:undefined;
      const flat=!mountain||[mountain.ne,mountain.se,mountain.sw,mountain.nw].every(n=>n===0);
      if(flat&&(!railway||railway.drawGround(base,read.map.levels))) {
        const ground=target(base,'ground');
        addSprite('system:ground/'+(base.z<t.waterLevel?'water':variant.season==='winter'?'winter':'spring'),undefined,base,ground);
        if(base.z>=t.waterLevel)addPolygon(base,ground,diamond,'transparent',groundEdge);
        // Outline the upper rim even when the lower cell's slope reaches this
        // top: that continuous join is the normal edge of a natural plateau.
        if(visibleTerrace||base.z>=t.waterLevel)for(const [i,d] of ([2,4,6,0] as const).entries()) {
          const neighbor=move(base,d),s=space.storage(neighbor);
          const nc=space.containsHorizontal(neighbor)?t.cells[s.v*read.map.width+s.h]:undefined;
          const terraceRim=visibleTerrace&&!(nc?.ground===base.z&&at.get(key({...neighbor,z:base.z-1}))?.kind==='terrace');
          const drop=nc&&Math.max(nc.ground,full?t.waterLevel:0)<base.z;
          if(terraceRim||drop)addPolygon(base,ground,[diamond[i],diamond[(i+1)%4]],'transparent',rimEdge);
        }
      }
      if(mountain) {
        const mt=target(base,'mountain',mountain.entityId),clipped=!full&&cut===base.z;
        if(clipped)addPolygon(base,mt,diamond,mountainColor(mountain,variant,base.z<t.waterLevel,true));
        else if(!flat) {
          addPolygon(base,mt,mountainPoints(mountain),mountainColor(mountain,variant,base.z<t.waterLevel),terrainColor(base.z<t.waterLevel?[0,0,128]:hsv(paletteData.palettes[variant.season][0]),variant.night));
          for(const [side,d,l,r] of [[0,6,mountain.nw,mountain.sw],[1,4,mountain.sw,mountain.se]]) {
            const neighbor=move(base,d),s=space.storage(neighbor),inside=space.containsHorizontal(neighbor),nc=inside?t.cells[s.v*read.map.width+s.h]:undefined;
            if(l+r>0&&(!nc||nc.ground<=base.z)&&at.get(key(neighbor))?.kind!=='mountain')addSprite(`system:cliff/${side}/${l}/${r}`,undefined,base,target(base,'cliff',mountain.entityId),{x:side*16,y:0});
          }
        }
        if(mountain.forest) {
          const id=mountain.forest.definition.recipeId;
          if(!clipped&&flat&&recipes.get(id)?.frames.some(f=>f.key==='ground'))addSprite(id,'ground',base,target(base,'forest',mountain.entityId));
          // Source still draws trees at its cut face. Preserve saved array order.
          for(const tree of mountain.forest.trees)addSprite(id,`tree/${tree.sprite}`,base,target(base,'forest',mountain.entityId),treeOffset(mountain,tree));
        }
      } else if(surface?.kind==='creek') {
        const mask=surface.creekConnections,connection=[1,4,5].includes(mask)?5:[2,8,10].includes(mask)?10:mask;
        const id=entities.get(surface.entityId)!.definition.recipeId,frame=recipes.get(id)?.frames.find(f=>f.selector.connections===connection);
        if(!frame)throw new Error('Missing creek frame');addSprite(id,frame.key,base,target(base,'creek',surface.entityId));
      }
      } finally {
        // Every column is complete before the next H/V cell: source Z order,
        // then Traffic's before / reserved car / after passes at that voxel.
        if(railway) {
          if(!reused) {
            const column=ops.splice(start);
            column.push(...railway.operations(index,cut,t.waterLevel,full,t.cells[index].ground));
            column.sort((a,b)=>a.target.position.z-b.target.position.z);ops.push(...column);
          }
          if(cellCache.size<maximumCachedCells)cellCache.set(index,{signature,operations:ops.slice(start)});
        }
        if(cacheCells)cells.set(index,ops.slice(start));
      }
    }
    if(cells.size>maximumCachedCells)for(const id of cells.keys()) {
      const h=id%read.map.width,v=Math.floor(id/read.map.width);
      if(h<h0||h>=h1||v<v0||v>=v1)cells.delete(id);
      if(cells.size<=maximumCachedCells)break;
    }
    return ops;
    };
    if(read.map.width*read.map.height<=16384) {
      const all=build(0,read.map.width,0,read.map.height);
      return finish(new TerrainScene(all,await cache.ready(catalog,[...layers,...(railway?.layers??[])],variant)));
    }
    // Decode each required frame/surface once. Terrain instances and their
    // operations are materialized only for the current viewport, in source order.
    const ids=lookup.recipes();
    const templates:Layer[]=[];let marginX=32,marginY=64;
    for(const recipe of catalog.renderRecipes)if(ids.has(recipe.id)||recipe.id.startsWith('system:ground/')||recipe.id.startsWith('system:cliff/')) {
      for(const frame of recipe.frames)if(frame.status==='ready'&&frame.masks?.[`${variant.season}/${variant.night?'night':'day'}`]) {
        templates.push({entityId:'template',recipeId:recipe.id,frame,position:[0,0,0]});
        const [ax,ay]=frame.anchor!,[,,w,h]=frame.rectangle!;
        marginX=Math.max(marginX,Math.abs(ax)+w+32);marginY=Math.max(marginY,Math.abs(ay)+h+64);
      }
    }
    templates.push(...(railway?.layers??[]));
    for(const l of railway?.layers??[]) {
      const [ax,ay]=l.frame.anchor!,[,,w,h]=l.frame.rectangle!;marginX=Math.max(marginX,Math.abs(ax)+w+32);marginY=Math.max(marginY,Math.abs(ay)+h+64);
    }
    return finish(new TerrainScene([],await cache.ready(catalog,templates,variant),{build,width:read.map.width,height:read.map.height,levels:read.map.levels,marginX,marginY},read.worldEpoch==='local-demo-only'?'#0072bc':variant.night?'#17232b':'#d8e5d9'));
  }
  changedColumns(previous:TerrainScene):readonly number[]|undefined {
    if(this.cacheIdentity!==previous.cacheIdentity||!this.read||!previous.read||this.read.mixed||previous.read.mixed)return;
    return terrainChangedColumns(previous.read,this.read);
  }
  columnIndex(op:TerrainOperation):number {
    const s=this.space!.storage(op.target.position);return s.v*this.space!.horizontalSize+s.h;
  }
  columnOperations(index:number):readonly TerrainOperation[] {
    const r=this.region!;return r.build(index%r.width,index%r.width+1,Math.floor(index/r.width),Math.floor(index/r.width)+1);
  }
  boundsFor(op:TerrainOperation):Bounds {return this.operationBounds(op);}
  draw(ctx:CanvasRenderingContext2D,view:View,width:number,height:number):number {
    this.prepareView(view,width,height);
    const dpr=typeof ctx.getTransform==='function'?ctx.getTransform().a:1;
    if(this.region&&dpr>0&&Number.isFinite(dpr)&&typeof OffscreenCanvas!=='undefined'&&Math.ceil(width*dpr)*Math.ceil(height*dpr)<=16777216) {
      const old=this.raster;
      const reusable=old&&old.width===width&&old.height===height&&old.dpr===dpr&&old.view.zoom===view.zoom;
      if(reusable&&old.view.x===view.x&&old.view.y===view.y){ctx.drawImage(old.image,0,0,width,height);return old.drawn;}
      const image=reusable?(old.spare??new OffscreenCanvas(Math.round(width*dpr),Math.round(height*dpr))):new OffscreenCanvas(Math.round(width*dpr),Math.round(height*dpr)),paint=image.getContext('2d')!;
      const dx=reusable?view.x-old.view.x:width,dy=reusable?view.y-old.view.y:height;
      let clips:Bounds[]|undefined;
      if(reusable&&Math.abs(dx)<width&&Math.abs(dy)<height&&Math.abs(dx*dpr-Math.round(dx*dpr))<1e-7&&Math.abs(dy*dpr-Math.round(dy*dpr))<1e-7) {
        paint.setTransform(1,0,0,1,0,0);paint.imageSmoothingEnabled=false;paint.globalCompositeOperation='copy';paint.drawImage(old.image,Math.round(dx*dpr),Math.round(dy*dpr));paint.globalCompositeOperation='source-over';
        clips=[];
        if(dx)clips.push({left:dx>0?0:width+dx,top:0,right:dx>0?dx:width,bottom:height});
        if(dy)clips.push({left:0,top:dy>0?0:height+dy,right:width,bottom:dy>0?dy:height});
      }
      paint.setTransform(dpr,0,0,dpr,0,0);paint.save();
      if(clips){paint.beginPath();for(const b of clips)paint.rect(b.left,b.top,b.right-b.left,b.bottom-b.top);paint.clip();}
      // Composite antialiased terrain onto the same opaque map background once.
      // Re-copying translucent edge pixels would otherwise accumulate rounding.
      paint.fillStyle=this.background;paint.fillRect(0,0,width,height);
      const drawn=this.paint(paint as unknown as CanvasRenderingContext2D,view,width,height,clips);paint.restore();
      this.raster={image,spare:reusable?old.image:undefined,view:{...view},width,height,dpr,drawn};ctx.drawImage(image,0,0,width,height);return drawn;
    }
    return this.paint(ctx,view,width,height);
  }
  private operationBounds(op:Operation):Bounds {
    const known=this.bounds.get(op);if(known)return known;
    let b:Bounds;
    if('layer' in op) {
      const l=op.layer,[x,y,z]=l.position,[ax,ay]=l.frame.anchor!,[,,w,h]=l.frame.rectangle!;
      const left=16*(x+y)-ax,top=8*(y-x)-16*z-ay;b={left,top,right:left+w,bottom:top+h};
    }else {
      let left=Infinity,top=Infinity,right=-Infinity,bottom=-Infinity;
      for(const p of op.points){left=Math.min(left,p.x);top=Math.min(top,p.y);right=Math.max(right,p.x);bottom=Math.max(bottom,p.y);}
      const pad=op.edge?2:1;b={left:left-pad,top:top-pad,right:right+pad,bottom:bottom+pad};
    }
    this.bounds.set(op,b);return b;
  }
  private paintPolygon(ctx:CanvasRenderingContext2D,view:View,op:Extract<Operation,{points:Point[]}>):boolean {
    if(!this.region||typeof OffscreenCanvas==='undefined')return false;
    const b=this.operationBounds(op),dpr=ctx.getTransform().a,scale=view.zoom*dpr;
    const px=(view.x+b.left*view.zoom)*dpr,py=(view.y+b.top*view.zoom)*dpr;
    const ix=Math.floor(px+1e-7),iy=Math.floor(py+1e-7),fx=Math.max(0,Math.round((px-ix)*1e7)/1e7),fy=Math.max(0,Math.round((py-iy)*1e7)/1e7);
    let shape=this.polygonKeys.get(op);
    if(!shape){shape=[op.color,op.edge??'',...op.points.flatMap(p=>[p.x-b.left,p.y-b.top])].join('/');this.polygonKeys.set(op,shape);}
    const width=Math.ceil((b.right-b.left)*scale+fx),height=Math.ceil((b.bottom-b.top)*scale+fy),bytes=width*height*4;
    if(bytes>16*1024*1024)return false;
    const key=shape+'/'+[scale,fx,fy].join(',');let cached=this.polygonRasters.get(key);
    if(!cached) {
      while(this.polygonBytes+bytes>64*1024*1024&&this.polygonRasters.size) {
        const first=this.polygonRasters.keys().next().value!;this.polygonBytes-=this.polygonRasters.get(first)!.bytes;this.polygonRasters.delete(first);
      }
      const image=new OffscreenCanvas(width,height),paint=image.getContext('2d')!;paint.beginPath();
      op.points.forEach((p,i)=>{const x=(p.x-b.left)*scale+fx,y=(p.y-b.top)*scale+fy;i?paint.lineTo(x,y):paint.moveTo(x,y);});
      paint.closePath();paint.fillStyle=op.color;paint.fill();
      if(op.edge){paint.strokeStyle=op.edge;paint.lineWidth=scale;paint.stroke();}
      cached={image,bytes};this.polygonRasters.set(key,cached);this.polygonBytes+=bytes;
    }
    ctx.drawImage(cached.image,ix/dpr,iy/dpr,width/dpr,height/dpr);return true;
  }
  private paint(ctx:CanvasRenderingContext2D,view:View,width:number,height:number,clips?:Bounds[]):number {
    return this.paintOperations(ctx,view,width,height,this.operations,clips);
  }
  paintOperations(ctx:CanvasRenderingContext2D,view:View,width:number,height:number,operations:readonly Operation[],clips?:Bounds[]):number {
    let drawn=0;
    ctx.imageSmoothingEnabled=false;
    for(const op of operations) {
      const b=this.operationBounds(op),left=view.x+b.left*view.zoom,top=view.y+b.top*view.zoom,right=view.x+b.right*view.zoom,bottom=view.y+b.bottom*view.zoom;
      if(right<0||bottom<0||left>width||top>height)continue;
      drawn++;
      if(clips&&!clips.some(c=>right>=c.left&&bottom>=c.top&&left<=c.right&&top<=c.bottom))continue;
      if('layer' in op) {
        this.ready.paintNormal(ctx,view,op.layer,!!this.region);continue;
      }
      if(this.paintPolygon(ctx,view,op))continue;
      const points=op.points.map(p=>({x:view.x+p.x*view.zoom,y:view.y+p.y*view.zoom}));
      if(points.every(p=>p.x<0)||points.every(p=>p.x>width)||points.every(p=>p.y<0)||points.every(p=>p.y>height))continue;
      ctx.beginPath();points.forEach((p,i)=>i?ctx.lineTo(p.x,p.y):ctx.moveTo(p.x,p.y));ctx.closePath();ctx.fillStyle=op.color;ctx.fill();
      if(op.edge){ctx.strokeStyle=op.edge;ctx.lineWidth=view.zoom;ctx.stroke();}
    }
    return drawn;
  }
  pick(p:Point,view:View):SceneTarget|undefined {
    return this.pickOperations(p,view,this.operations);
  }
  pickLocation(p:Point,view:View,policy:RailwayPickPolicy):Position|undefined {
    return this.locationPicker?.(p,view,policy);
  }
  pickOperations(p:Point,view:View,operations:readonly Operation[]):SceneTarget|undefined {
    const world={x:(p.x-view.x)/view.zoom,y:(p.y-view.y)/view.zoom};
    for(let i=operations.length-1;i>=0;i--) {
      const op=operations[i];
      const b=this.operationBounds(op);if(world.x<b.left||world.x>b.right||world.y<b.top||world.y>b.bottom)continue;
      if('layer' in op ? this.ready.pick(p.x,p.y,view,[op.layer]) : inPolygon(world,op.points))return {...op.target,position:{...op.target.position}};
    }
  }
}
