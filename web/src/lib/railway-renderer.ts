// Stable ExAV 2.1.1.12: Traffic.draw, RailRoad.drawBefore/After,
// QuarterViewDrawer.shouldDrawGround/fromABToXYZ and Disambiguators.
// Original authors; LGPL-2.1-or-later.
import type { Definition, Position, RailwayVoxel } from '../generated/contracts.ts';
import type { ReadState } from './session-client.ts';
import { WorldSpace, type Point } from './map-geometry.ts';
import { instantiate, variantKey, type Layer, type RenderCatalog, type Variant } from './content-recipes.ts';
import { railwayProfile } from './mixed-state.ts';

export interface RailwayTarget {
  kind:'railway'; id:string; position:Position;
  component:'rail'|'pier'|'support'|'accessory'; definition:Definition; controllerId?:string;
}
export interface RailwayOperation {target:RailwayTarget;layer:Layer}
const key=(p:Position)=>`${p.x},${p.y},${p.z}`;
export type RailwayPickPolicy={kind:'railOrGround'}|{kind:'slopeOrGround'}|{kind:'ground'}|{kind:'sameLevel';level:number};
/** Source controller disambiguation is geometric, independent of sprite alpha.
 * The second point keeps the first point's level, including vacant elevated cells. */
export function pickRailwayLocation(read:ReadState,point:Point,heightCut:number,policy:RailwayPickPolicy):Position|undefined {
  const space=new WorldSpace(read.map.width,read.map.height,read.map.levels);
  const slopeOwners=new Set(read.mixed?.slopes.map(s=>s.entityId));
  const rails=new Set(read.mixed?.voxels.filter(v=>v.traffic?.rail||policy.kind==='slopeOrGround'&&slopeOwners.has(v.ownerId)).map(v=>key(v.position)));
  for(let z=Math.min(heightCut,space.levels-1);z>=0;z--) {
    const p=space.pick(point,z);if(!space.contains(p))continue;
    const s=space.storage(p),ground=read.terrain!.cells[s.v*space.horizontalSize+s.h].ground;
    if(policy.kind==='sameLevel'?z===policy.level:policy.kind==='ground'?z===ground:rails.has(key(p))||z<=ground)return p;
  }
}
/** Preflight every saved binding, including offscreen and cut voxels. No
 * reference-line fallback and no P5 car art inferred from a missing pose. */
export class RailwayRenderPlan {
  readonly layers:Layer[]=[];
  private columns=new Map<number,RailwayOperation[]>();
  private voxels=new Map<string,RailwayVoxel>();
  private signatures=new Map<number,string>();
  private space:WorldSpace;
  constructor(read:ReadState,catalog:RenderCatalog,variant:Variant) {
    const space=new WorldSpace(read.map.width,read.map.height,read.map.levels);
    this.space=space;
    const recipes=new Map(catalog.renderRecipes.map(r=>[r.id,r]));
    const states=new Map<number,RailwayVoxel[]>();
    const binding=(d:Definition)=>{
      const b=railwayProfile.bindings.find(b=>b.reference.packId===d.packId&&b.reference.contributionId===d.contributionId&&b.reference.recipeId===d.recipeId);
      if(!b)throw new Error('Railway binding unavailable: '+d.recipeId);return b;
    };
    for(const v of read.mixed!.voxels) {
      this.voxels.set(key(v.position),v);
      const s=space.storage(v.position),index=s.v*space.horizontalSize+s.h,ops=this.columns.get(index)??[];
      this.columns.set(index,ops);
      const column=states.get(index)??[];column.push(v);states.set(index,column);
      const add=(d:Definition,component:RailwayTarget['component'],selectors?:Record<string,number|boolean>,controllerId?:string,targetDefinition=d)=>{
        binding(d);
        const recipe=recipes.get(d.recipeId);if(!recipe)throw new Error('Railway recipe unavailable: '+d.recipeId);
        const pier=binding(d);
        const selected=d.contributionId.startsWith('system:bridge-pier/')
          ? recipe.frames.filter(f=>f.key===`pier/${(pier.variant-2)%2}/${Math.floor((pier.variant-2)/2)}`)
          : selectors?recipe.frames.filter(f=>Object.entries(selectors).every(([k,value])=>f.selector[k]===value)):recipe.frames;
        if(!selected.length||selected.some(f=>f.status!=='invisible'&&(f.status!=='ready'||!f.masks?.[variantKey(variant)])))throw new Error('Railway frame unavailable: '+d.recipeId);
        const target:RailwayTarget={kind:'railway',id:v.ownerId,position:v.position,component,definition:targetDefinition,...(controllerId?{controllerId}:{})};
        for(const layer of instantiate({...recipe,handler:d.contributionId.startsWith('system:bridge-pier/')?'source-geometry':recipe.handler,frames:selected},v.ownerId,[v.position.x-space.centerX,v.position.y,v.position.z],{selectors})) {
          if(recipe.handler==='electric-pole'&&variant.drawPoles===false)continue;
          // P2's signal stages describe needsToDrawAfter's name. GEO-007
          // follows SignalRailRoad's actual inverted calls in the live world.
          if(recipe.handler==='signal')layer.frame={...layer.frame,stage:layer.frame.stage===40?10:40};
          this.layers.push(layer);ops.push({target,layer});
        }
      };
      if(v.kind==='slopefiller')continue; // Explicit invisible occupancy still controls holes.
      if(v.kind!=='traffic'){add(v.definition!,v.kind==='pier'?'pier':'support');continue;}
      const c=v.traffic!;
      if(c.road||c.platform||c.car)throw new Error('Railway consumer unavailable'); // P5/P6/P7 supply their exact pose/dispatch.
      if(c.accessory)add(c.accessory.definition,'accessory',{axis:c.accessory.direction});
      if(c.rail) {
        const r=c.rail,b=binding(r.definition);
        if(b.handler==='signal') {
          add(r.definition,'rail',{direction:r.direction},r.controllerId??undefined);
          add({packId:r.definition.packId,contributionId:`system:pattern/${r.pattern}`,recipeId:`system:pattern/${r.pattern}`},'rail',undefined,r.controllerId??undefined,r.definition);
        }else add(r.definition,'rail',['bridge','girder','tunnel','garage'].includes(b.handler)?{index:r.variant}:undefined,r.controllerId??undefined);
      }
      // Traffic: accessory-before, rail-before, [P5 car], rail-after, accessory-after.
      ops.sort((a,b)=>a.target.position.z-b.target.position.z||this.order(a)-this.order(b));
    }
    for(const [index,voxels] of states)this.signatures.set(index,JSON.stringify(voxels));
  }
  signature(index:number):string|undefined {return this.signatures.get(index);}
  private order(op:RailwayOperation):number {
    const after=(op.layer.frame.stage??20)>=30;
    return op.target.component==='accessory'?(after?50:10):(after?40:20);
  }
  drawGround(p:Position,levels:number):boolean {
    // Retain the source ceiling/floor exceptions exactly.
    const above=p.z===levels-1?undefined:this.voxels.get(key(p));
    const below=p.z===0?undefined:this.voxels.get(key({...p,z:p.z-1}));
    return above?.drawGroundBelow!==false&&below?.drawGroundAbove!==false;
  }
  operations(index:number,cut:number,water:number,full:boolean,ground:number):RailwayOperation[] {
    const p=this.space.location({h:index%this.space.horizontalSize,v:Math.floor(index/this.space.horizontalSize),z:water});
    const minimum=full?(water<=ground&&!this.drawGround(p,this.space.levels)?Math.max(0,water-1):water):0;
    return (this.columns.get(index)??[]).filter(op=>op.target.position.z<=cut&&op.target.position.z>=minimum);
  }
}
