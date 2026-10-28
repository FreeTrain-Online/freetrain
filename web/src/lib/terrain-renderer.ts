// Stable ExAV 2.1.1.12: QuarterViewDrawer, MountainVoxel.drawGround/drawTrees,
// TerraceVoxel.draw and ColorMap. Original authors; LGPL-2.1-or-later.
import type { Position, Snapshot, TerrainOccupant } from '../generated/contracts.ts';
import { WorldSpace, move, type Point } from './map-geometry.ts';
import { RecipeImageCache, type RenderCatalog, type Layer, type Variant, type ReadyScene, type View } from './content-recipes.ts';
import { isTerrainWorld, validSnapshot } from './world-identity.ts';
import paletteData from './terrain-palettes.json' with { type: 'json' };
import type { ReadState } from './session-client.ts';
type Occupant=NonNullable<ReadState['terrain']>['occupants'][number];
export interface TerrainTarget { kind:'terrain'; id:string; position:Position; surface:'ground'|'mountain'|'water'|'underground'|'terrace'|'forest'|'creek'|'cliff' }
type Operation = { target:TerrainTarget; points:Point[]; color:string; edge?:string } | {target:TerrainTarget; layer:Layer};
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
  private constructor(readonly operations:readonly Operation[],private ready:ReadyScene) {}
  static async prepare(read:ReadState,catalog:RenderCatalog,cache:RecipeImageCache,variant:Variant,heightCut:number):Promise<TerrainScene> {
    // Validation only reads its argument; retain the immutable transport view.
    if(!isTerrainWorld(read)||!validSnapshot(read as Snapshot))throw new Error('Unsupported terrain read');
    if(!Number.isInteger(heightCut)||heightCut<0)throw new Error('Invalid terrain height cut');
    if(catalog.packId!==read.requiredPacks[0].id||catalog.version!==read.requiredPacks[0].version)throw new Error('Terrain catalog mismatch');
    const t=read.terrain!,space=new WorldSpace(read.map.width,read.map.height,read.map.levels),ops:Operation[]=[],layers:Layer[]=[];
    const recipes=new Map(catalog.renderRecipes.map(r=>[r.id,r]));
    const entities=new Map(read.entities!.map(e=>[e.id,e])),at=new Map<string,Occupant>();
    const key=(p:Position)=>`${p.x},${p.y},${p.z}`;
    for(const o of t.occupants)at.set(key(entities.get(o.entityId)!.anchor),o);
    const full=heightCut>=read.map.levels-1,cut=Math.min(heightCut,read.map.levels-1);
    const addSprite=(id:string,frameKey:string|undefined,p:Position,target:TerrainTarget,offset:Point={x:0,y:0})=>{
      const recipe=recipes.get(id),frame=frameKey===undefined?recipe?.frames[0]:recipe?.frames.find(f=>f.key===frameKey);
      if(!frame||frame.status!=='ready'||!frame.masks?.[`${variant.season}/${variant.night?'night':'day'}`])throw new Error('Terrain recipe unavailable: '+id+'/'+frameKey);
      const layer:Layer={entityId:target.id,recipeId:id,frame:{...frame,anchor:[frame.anchor![0]-offset.x,frame.anchor![1]-offset.y]},position:[p.x-space.centerX,p.y,p.z]};
      layers.push(layer);ops.push({target,layer});
    };
    const addPolygon=(p:Position,target:TerrainTarget,points:Point[],color:string,edge?:string)=>{
      const q=space.project(p);ops.push({target,points:points.map(a=>({x:q.x+a.x,y:q.y+a.y})),color,edge});
    };
    for(let v=0;v<read.map.height;v++)for(let h=0;h<read.map.width;h++) {
      const cell=t.cells[v*read.map.width+h],base=space.location({h,v,z:cell.ground});
      const target=(p:Position,surface:TerrainTarget['surface'],id?:string):TerrainTarget=>({kind:'terrain',id:id??`cell:${h},${v}`,position:{...p},surface});
      const surface=at.get(key(base)),terrace=at.get(key({...base,z:base.z-1}));
      if(terrace?.kind==='terrace'&&base.z-1<=cut&&(!full||base.z-1>=t.waterLevel))addSprite(entities.get(terrace.entityId)!.definition.recipeId,undefined,{...base,z:base.z-1},target({...base,z:base.z-1},'terrace',terrace.entityId));
      if(cut<cell.ground) {
        // A nontransparent Terrace at the cut owns this voxel; the source
        // draws its sprite instead of an underground chip over it.
        if(!(terrace?.kind==='terrace'&&cut===base.z-1))addSprite('system:ground/underground',undefined,{...base,z:cut},target({...base,z:cut},'underground'));
        continue;
      }
      if(full&&cell.ground<t.waterLevel) {
        const p={...base,z:t.waterLevel};addPolygon(p,target(p,'water'),diamond,terrainColor([0,114,188],variant.night));continue;
      }
      const mountain=surface?.kind==='mountain'?surface:undefined;
      const flat=!mountain||[mountain.ne,mountain.se,mountain.sw,mountain.nw].every(n=>n===0);
      if(flat)addSprite('system:ground/'+(base.z<t.waterLevel?'water':variant.season==='winter'?'winter':'spring'),undefined,base,target(base,'ground'));
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
    }
    return new TerrainScene(ops,await cache.ready(catalog,layers,variant));
  }
  draw(ctx:CanvasRenderingContext2D,view:View,width:number,height:number):number {
    let drawn=0;
    for(const op of this.operations) {
      if('layer' in op) {
        const l=op.layer,[x,y,z]=l.position,[ax,ay]=l.frame.anchor!,[,,w,h]=l.frame.rectangle!;
        const left=view.x+(16*(x+y)-ax)*view.zoom,top=view.y+(8*(y-x)-16*z-ay)*view.zoom;
        if(left+w*view.zoom<0||top+h*view.zoom<0||left>width||top>height)continue;
        this.ready.draw(ctx,view,{kind:'normal'},[l]);drawn++;continue;
      }
      const points=op.points.map(p=>({x:view.x+p.x*view.zoom,y:view.y+p.y*view.zoom}));
      if(points.every(p=>p.x<0)||points.every(p=>p.x>width)||points.every(p=>p.y<0)||points.every(p=>p.y>height))continue;
      ctx.beginPath();points.forEach((p,i)=>i?ctx.lineTo(p.x,p.y):ctx.moveTo(p.x,p.y));ctx.closePath();ctx.fillStyle=op.color;ctx.fill();
      if(op.edge){ctx.strokeStyle=op.edge;ctx.lineWidth=view.zoom;ctx.stroke();}drawn++;
    }
    return drawn;
  }
  pick(p:Point,view:View):TerrainTarget|undefined {
    for(let i=this.operations.length-1;i>=0;i--) {
      const op=this.operations[i];
      if('layer' in op ? this.ready.pick(p.x,p.y,view,[op.layer]) : inPolygon({x:(p.x-view.x)/view.zoom,y:(p.y-view.y)/view.zoom},op.points))return {...op.target,position:{...op.target.position}};
    }
  }
}
