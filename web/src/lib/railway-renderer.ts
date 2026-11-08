import { speedLimitFrame, speedIcons } from './speed-limit-display.ts';
// Stable ExAV 2.1.1.12: Traffic.draw, RailRoad.drawBefore/After,
// QuarterViewDrawer.shouldDrawGround/fromABToXYZ and Disambiguators.
// Original authors; LGPL-2.1-or-later.
import type { Definition, Position, RailwayVoxel } from '../generated/contracts.ts';
import type { ReadState } from './session-client.ts';
import { WorldSpace, type Point } from './map-geometry.ts';
import { instantiate, variantKey, type Layer, type RenderCatalog, type Variant } from './content-recipes.ts';
import { railwayProfile } from './mixed-state.ts';
import { prepareCar, type TrainTarget } from './train-renderer.ts';

export interface RailwayTarget {
  kind:'railway'; id:string; position:Position;
  component:'rail'|'pier'|'support'|'accessory'|'stationary'|'speedLimit'|'platform'; definition:Definition; controllerId?:string; platformId?:string;
}
export interface RailwayOperation {target:RailwayTarget|TrainTarget;layer:Layer;tunnel?:'portal'|'interior'}
const key=(p:Position)=>`${p.x},${p.y},${p.z}`;
export type RailwayPickPolicy={kind:'railOrGround'}|{kind:'trafficOrGround'}|{kind:'slopeOrGround'}|{kind:'ground'}|{kind:'stationaryOrGround'}|{kind:'sameLevel';level:number};
/** Source controller disambiguation is geometric, independent of sprite alpha.
 * The second point keeps the first point's level, including vacant elevated cells. */
export function pickRailwayLocation(read:ReadState,point:Point,heightCut:number,policy:RailwayPickPolicy):Position|undefined {
  const space=new WorldSpace(read.map.width,read.map.height,read.map.levels);
  const slopeOwners=new Set(read.mixed?.slopes.map(s=>s.entityId));
  const rails=new Set(read.mixed?.voxels.filter(v=>v.traffic?.rail||policy.kind==='trafficOrGround'&&v.traffic||policy.kind==='slopeOrGround'&&slopeOwners.has(v.ownerId)||policy.kind==='stationaryOrGround'&&v.kind==='stationary').map(v=>key(v.position)));
  for(let z=Math.min(heightCut,space.levels-1);z>=0;z--) {
    const p=space.pick(point,z);if(!space.contains(p))continue;
    const s=space.storage(p),ground=read.terrain!.cells[s.v*space.horizontalSize+s.h].ground;
    if(policy.kind==='sameLevel'?z===policy.level:policy.kind==='ground'?z===ground:rails.has(key(p))||z<=ground)return p;
  }
}
/** Preflight every saved binding, including offscreen and cut voxels. A car
 * requires its exact fleet pose and recipe; missing art never gains a fallback. */
export class RailwayRenderPlan {
  readonly layers:Layer[]=[];
  private columns=new Map<number,RailwayOperation[]>();
  private voxels=new Map<string,RailwayVoxel>();
  private signatures=new Map<number,string>();
  private space:WorldSpace;
  private columnStructures=new Map<number,Set<string>>();
  private structures=new Map<string,{anchor:Position;height:number;definition:Definition}>();
  constructor(read:ReadState,catalog:RenderCatalog,variant:Variant) {
    const space=new WorldSpace(read.map.width,read.map.height,read.map.levels);
    this.space=space;
    const recipes=new Map(catalog.renderRecipes.map(r=>[r.id,r]));
    const cars=new Map(read.mixed!.fleet?.trains.flatMap(train=>train.cars.map((car,index)=>[car.id,{train,index}] as const)));
    const states=new Map<number,RailwayVoxel[]>();
    const ownerIds=new Set(read.mixed!.voxels.filter(v=>v.kind==='stationary').map(v=>v.ownerId));
    const owners=new Map(read.entities!.filter(e=>ownerIds.has(e.id)).map(e=>[e.id,e]));
    const binding=(d:Definition)=>{
      const b=railwayProfile.bindings.find(b=>b.reference.packId===d.packId&&b.reference.contributionId===d.contributionId&&b.reference.recipeId===d.recipeId);
      if(!b)throw new Error('Railway binding unavailable: '+d.recipeId);return b;
    };
    const limits=new Map<string,('low'|'medium'|'fast')[]>();
    for(const speed of ['low','medium','fast'] as const)for(const p of read.mixed!.speedLimits?.[speed]??[]){
      const k=key(p),values=limits.get(k)??[];if(!values.includes(speed))values.push(speed);limits.set(k,values);
    }
    for(const v of read.mixed!.voxels)this.voxels.set(key(v.position),v);
    for(const v of read.mixed!.voxels) {
      const s=space.storage(v.position),index=s.v*space.horizontalSize+s.h,ops=this.columns.get(index)??[];
      this.columns.set(index,ops);
      const column=states.get(index)??[];column.push(v);states.set(index,column);
      const add=(d:Definition,component:RailwayTarget['component'],selectors?:Record<string,number|boolean>,controllerId?:string,targetDefinition=d,cell?:number[])=>{
        binding(d);
        const recipe=recipes.get(d.recipeId);if(!recipe)throw new Error('Railway recipe unavailable: '+d.recipeId);
        const pier=binding(d);
        const selected=d.contributionId.startsWith('system:bridge-pier/')
          ? recipe.frames.filter(f=>f.key===`pier/${(pier.variant-2)%2}/${Math.floor((pier.variant-2)/2)}`)
          : selectors?recipe.frames.filter(f=>Object.entries(selectors).every(([k,value])=>f.selector[k]===value)):recipe.frames;
        const frames=cell?selected.filter(f=>f.cell.every((n,i)=>n===cell[i])).map(f=>({...f,cell:[0,0,0] as [number,number,number]})):selected;
        if(!frames.length||frames.some(f=>f.status!=='invisible'&&(f.status!=='ready'||!f.masks?.[variantKey(variant)])))throw new Error('Railway frame unavailable: '+d.recipeId);
        const target:RailwayTarget={kind:'railway',id:v.ownerId,position:v.position,component,definition:targetDefinition,...(controllerId?{controllerId}:{})};
        for(const layer of instantiate({...recipe,handler:d.contributionId.startsWith('system:bridge-pier/')?'source-geometry':recipe.handler,frames},v.ownerId,[v.position.x-space.centerX,v.position.y,v.position.z],{selectors})) {
          if(recipe.handler==='electric-pole'&&variant.drawPoles===false)continue;
          // P2's signal stages describe needsToDrawAfter's name. GEO-007
          // follows SignalRailRoad's actual inverted calls in the live world.
          if(recipe.handler==='signal')layer.frame={...layer.frame,stage:layer.frame.stage===40?10:40};
          // Only the frontmost S/W tunnel background belongs behind a car.
          // Interior backgrounds also close the gaps between authored foregrounds.
          const front=v.traffic?.rail?.variant===1?{...v.position,y:v.position.y+1}:{...v.position,x:v.position.x-1};
          const next=pier.handler==='tunnel'?this.voxels.get(key(front))?.traffic?.rail:undefined;
          const tunnel=pier.handler==='tunnel'?(next&&binding(next.definition).handler==='tunnel'?'interior':'portal'):undefined;
          this.layers.push(layer);ops.push({target,layer,...(tunnel?{tunnel}:{})});
        }
      };
      if(v.kind==='stationary'){
        const owner=owners.get(v.ownerId)!;
        const d=v.definition!,c=v.construction;
        if(c){
          const type=railwayProfile.bindings.find(b=>b.reference.contributionId===d.contributionId)!;
          if(!('stationary' in type)||!type.stationary)throw new Error('Missing stationary dimensions');
          const x=v.position.x-owner.anchor.x,y=v.position.y-owner.anchor.y;
          const connected=[y!==0,x!==type.stationary.sizeX-1,y!==type.stationary.sizeY-1,x!==0];
          const part=(part:string)=>add({packId:d.packId,contributionId:`system:construction/${part}`,recipeId:`system:construction/${part}`},'stationary',undefined,undefined,d);
          const ground=v.position.z===read.terrain!.cells[index].ground;
          const fence=(i:number)=>{if(!connected[i])part(`fence/${i}/${c.needsDoor?1:0}`);};
          if(ground){fence(0);fence(1);}
          if(c.state===0){if(ground)part(`ground/${c.ground}`);}
          else if(c.state<4){if(ground)part('ground/0');part(`bones/${c.state-1}/${connected.map(n=>n?'1':'0').join('')}`);}
          else{for(const i of [0,1])if(!connected[i])part(`wall/${i}`);part(`bones/2/${connected.map(n=>n?'1':'0').join('')}`);for(const i of [2,3])if(!connected[i])part(`wall/${i}`);}
          if(ground){fence(2);fence(3);}
        }else {
          const type=binding(d);if(!('stationary' in type)||!type.stationary)throw new Error('Missing stationary dimensions');
          this.structures.set(v.ownerId,{anchor:owner.anchor,height:type.stationary.height,definition:d});
          const members=this.columnStructures.get(index)??new Set<string>();members.add(v.ownerId);this.columnStructures.set(index,members);
          add(d,'stationary',undefined,undefined,d,[v.position.x-owner.anchor.x,v.position.y-owner.anchor.y,v.position.z-owner.anchor.z]);
        }
        continue;
      }
      if(v.kind==='slopefiller')continue; // Explicit invisible occupancy still controls holes.
      if(v.kind!=='traffic'){add(v.definition!,v.kind==='pier'?'pier':'support');continue;}
      const c=v.traffic!;
      if(c.road)throw new Error('Railway consumer unavailable');
      if(c.platform){
        const platform=read.mixed!.stationGraph?.platforms.find(p=>p.yards.some(y=>y.trafficOwnerId===v.ownerId&&key(y.location)===key(v.position)));
        const yard=platform?.yards.find(y=>y.trafficOwnerId===v.ownerId);
        const recipe=recipes.get(c.platform.recipeId);
        if(platform?.kind!=='thin'||yard?.outlook.kind!=='plain'||!recipe||
          recipe.frames.some(f=>f.status!=='ready'||!f.masks?.[variantKey(variant)]))throw new Error('Platform recipe unavailable');
        const target:RailwayTarget={kind:'railway',id:v.ownerId,position:v.position,component:'platform',definition:c.platform,platformId:platform.id};
        for(const layer of instantiate(recipe,v.ownerId,[v.position.x-space.centerX,v.position.y,v.position.z])){
          layer.frame={...layer.frame,stage:platform.direction===2||platform.direction===4?20:40};
          this.layers.push(layer);ops.push({target,layer});
        }
      }
      if(c.car) {
        const owner=cars.get(c.car.carId),recipe=recipes.get(c.car.definition.recipeId);
        if(!owner||!recipe||!c.rail)throw new Error('Car binding unavailable');
        const op=prepareCar(read,owner.train,owner.index,c.rail,binding(c.rail.definition).handler,recipe,variant,space.centerX);
        this.layers.push(op.layer);ops.push(op);
      }
      if(c.accessory)add(c.accessory.definition,'accessory',{axis:c.accessory.direction});
      if(c.rail) {
        const r=c.rail,b=binding(r.definition);
        if(b.handler==='signal') {
          add(r.definition,'rail',{direction:r.direction},r.controllerId??undefined);
          add({packId:r.definition.packId,contributionId:`system:pattern/${r.pattern}`,recipeId:`system:pattern/${r.pattern}`},'rail',undefined,r.controllerId??undefined,r.definition);
        }else add(r.definition,'rail',['bridge','girder','tunnel','garage'].includes(b.handler)?{index:r.variant}:undefined,r.controllerId??undefined);
      }
      if(variant.drawSpeedLimits!==false)for(const speed of limits.get(key(v.position))??[]){
        const icon=speedIcons[speed],definition={packId:read.requiredPacks[0].id,contributionId:icon.id,recipeId:icon.id};
        const layer:Layer={entityId:v.ownerId,recipeId:icon.id,frame:speedLimitFrame(speed),position:[v.position.x-space.centerX,v.position.y,v.position.z]};
        this.layers.push(layer);ops.push({target:{kind:'railway',id:v.ownerId,position:v.position,component:'speedLimit',definition},layer});
      }
      // Traffic: accessory-before, rail-before, car, rail-after, accessory-after.
      ops.sort((a,b)=>a.target.position.z-b.target.position.z||this.order(a)-this.order(b));
    }
    for(const ops of this.columns.values())ops.sort((a,b)=>a.target.position.z-b.target.position.z||this.order(a)-this.order(b));
    for(const [index,voxels] of states)this.signatures.set(index,JSON.stringify([voxels,voxels.map(v=>limits.get(key(v.position))),
      this.columns.get(index)?.map(o=>o.tunnel),this.columns.get(index)?.filter(o=>o.target.kind==='railway'&&o.target.component==='platform').map(o=>o.target),
      this.columns.get(index)?.filter(o=>o.target.kind==='train').map(o=>[o.target,o.layer.recipeId,o.layer.frame.key,o.layer.frame.anchor])]));
  }
  signature(index:number):string|undefined {return this.signatures.get(index);}
  changedColumns(previous:RailwayRenderPlan):number[] {
    // TunnelRail.invalidateVoxel suppresses a car update behind another S/W
    // tunnel. Keep conservative damage here so retained pick/state operations
    // remain current too; source foreground occlusion still hides the car.
    return [...new Set([...this.signatures.keys(),...previous.signatures.keys()])].filter(i=>this.signature(i)!==previous.signature(i));
  }
  private order(op:RailwayOperation):number {
    if(op.target.kind==='train')return 30;
    if(op.target.component==='speedLimit')return 60;
    if(op.target.component==='platform')return (op.layer.frame.stage??20)>=30?41:21;
    const after=(op.layer.frame.stage??20)>=30;
    return op.target.component==='accessory'?(after?50:10):(after?40:20);
  }
  drawGround(p:Position,levels:number):boolean {
    // Retain the source ceiling/floor exceptions exactly.
    const above=p.z===levels-1?undefined:this.voxels.get(key(p));
    const below=p.z===0?undefined:this.voxels.get(key({...p,z:p.z-1}));
    return above?.drawGroundBelow!==false&&below?.drawGroundAbove!==false;
  }
  operations(index:number,cut:number,water:number,full:boolean,ground:number):(RailwayOperation|{target:RailwayTarget;points:Point[];color:string})[] {
    const p=this.space.location({h:index%this.space.horizontalSize,v:Math.floor(index/this.space.horizontalSize),z:water});
    const minimum=full?(water<=ground&&!this.drawGround(p,this.space.levels)?Math.max(0,water-1):water):0;
    const result:(RailwayOperation|{target:RailwayTarget;points:Point[];color:string})[]=(this.columns.get(index)??[]).filter(op=>{
      const structure=this.structures.get(op.target.id);
      return op.target.position.z<=cut&&op.target.position.z>=minimum&&(!structure||full||cut>=structure.anchor.z+structure.height);
    });
    if(!full)for(const id of this.columnStructures.get(index)??[]){
      const s=this.structures.get(id)!;
      const storage=this.space.storage(s.anchor);
      if(storage.v*this.space.horizontalSize+storage.h!==index||s.anchor.z<minimum||s.anchor.z>cut||cut>=s.anchor.z+s.height)continue;
      const q=this.space.project(s.anchor);
      result.push({target:{kind:'railway',id,position:s.anchor,component:'stationary',definition:s.definition},
        points:[{x:16,y:0},{x:32,y:8},{x:16,y:16},{x:0,y:8}].map(p=>({x:q.x+p.x,y:q.y+p.y})),color:'#808080'});
    }
    return result.sort((a,b)=>a.target.position.z-b.target.position.z);
  }
}
