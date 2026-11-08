// Stable ExAV 2.1.1.12: PThreeDimStructure.VoxelImpl.draw and
// QuarterViewDrawer. Original authors; LGPL-2.1-or-later.
import type {Definition,Position} from '../generated/contracts.ts';
import type {RailwayTarget} from './railway-renderer.ts';
import type {ReadState} from './session-client.ts';
import {WorldSpace,offset,type Point} from './map-geometry.ts';
import {instantiate,variantKey,type Layer,type RenderCatalog,type Variant} from './content-recipes.ts';
import profile from '../generated/station-profile.json' with {type:'json'};

export interface StationTarget {kind:'station';id:string;position:Position;definition:Definition}
export type StationOperation={target:StationTarget|RailwayTarget;layer:Layer}|{target:StationTarget;points:Point[];color:string};
const key=(p:Position)=>`${p.x},${p.y},${p.z}`;
export class StationRenderPlan {
  readonly layers:Layer[]=[];
  readonly columnIndices=new Set<number>();
  private columns=new Map<number,{operations:StationOperation[];bases:StationTarget[]}>();
  private tops=new Map<string,number>();
  private space:WorldSpace;
  private occupied=new Set<string>();
  private signatures=new Map<number,string>();
  constructor(read:ReadState,catalog:RenderCatalog,variant:Variant) {
    const space=this.space=new WorldSpace(read.map.width,read.map.height,read.map.levels),recipes=new Map(catalog.renderRecipes.map(r=>[r.id,r]));
    for(const b of read.mixed?.stationGraph?.buildings??[]) {
      const d=profile.definitions.find(d=>d.reference.packId===b.definition.packId&&d.reference.contributionId===b.definition.contributionId&&d.reference.recipeId===b.definition.recipeId);
      const recipe=recipes.get(b.definition.recipeId);
      if(!d||recipe?.handler!=='fixed-3d'||recipe.frames.length!==d.sizeX*d.sizeY*d.height||
        recipe.frames.some(f=>f.status!=='invisible'&&(f.status!=='ready'||!f.masks?.[variantKey(variant)])))throw new Error('Station recipe unavailable: '+b.definition.recipeId);
      this.tops.set(b.id,b.location.z+d.height);
      for(let x=0;x<d.sizeX;x++)for(let y=0;y<d.sizeY;y++)for(let z=0;z<d.height;z++) {
        const p={x:b.location.x+x,y:b.location.y+y,z:b.location.z+z},s=space.storage(p),index=s.v*space.horizontalSize+s.h;
        this.occupied.add(key(p));this.columnIndices.add(index);
        const column=this.columns.get(index)??{operations:[],bases:[]};this.columns.set(index,column);
        const target:StationTarget={kind:'station',id:b.id,position:p,definition:b.definition};
        if(z===0)column.bases.push(target);
        const frames=recipe.frames.filter(f=>f.cell[0]===x&&f.cell[1]===y&&f.cell[2]===z);
        if(frames.length!==1)throw new Error('Station cell unavailable');
        for(const layer of instantiate({...recipe,frames:frames.map(f=>({...f,cell:[0,0,0]}))},b.id,[p.x-space.centerX,p.y,p.z])) {
          this.layers.push(layer);column.operations.push({target,layer});
        }
      }
    }
    for(const platform of read.mixed?.stationGraph?.platforms??[])if(platform.kind==='wide') {
      const definition={packId:read.requiredPacks[0].id,contributionId:`system:fat-${platform.direction/2%2}`,recipeId:`system:fat-${platform.direction/2%2}`},recipe=recipes.get(definition.recipeId);
      if(!recipe||recipe.frames.some(f=>f.status!=='ready'||!f.masks?.[variantKey(variant)]))throw new Error('Wide platform center recipe unavailable');
      const d=offset(platform.direction);this.tops.set(platform.id,platform.location.z);
      for(let i=0;i<platform.length;i++) {
        const p={x:platform.location.x+d.x*i,y:platform.location.y+d.y*i,z:platform.location.z},s=space.storage(p),index=s.v*space.horizontalSize+s.h;
        this.occupied.add(key(p));this.columnIndices.add(index);
        const column=this.columns.get(index)??{operations:[],bases:[]};this.columns.set(index,column);
        const target:RailwayTarget={kind:'railway',id:platform.id,platformId:platform.id,position:p,component:'platform',definition};
        // FatPlatformVoxel draws its sprite at every visible cut, without the
        // PThreeDimStructure gray cap. Source V/H/Z traversal orders other cells.
        for(const layer of instantiate(recipe,platform.id,[p.x-space.centerX,p.y,p.z])){
          this.layers.push(layer);column.operations.push({target,layer});
        }
      }
    }
    for(const [index,column] of this.columns)this.signatures.set(index,JSON.stringify([column,column.bases.map(t=>this.tops.get(t.id))]));
  }
  contains(p:Position):boolean {return this.occupied.has(key(p));}
  signature(index:number):string|undefined {return this.signatures.get(index);}
  changedColumns(previous:StationRenderPlan):number[] {return [...new Set([...this.columnIndices,...previous.columnIndices])].filter(i=>this.signature(i)!==previous.signature(i));}
  operations(index:number,cut:number,water:number,full:boolean):StationOperation[] {
    const column=this.columns.get(index);if(!column)return [];
    // Drawer passes cutZ-z+1. Normal art starts at base+height; otherwise
    // EVERY base footprint voxel draws Gray, including authored invisible cells.
    const normal=(id:string)=>full||cut>=this.tops.get(id)!;
    const result=column.operations.filter(op=>normal(op.target.id)&&op.target.position.z<=cut&&(!full||op.target.position.z>=water));
    for(const target of column.bases)if(!normal(target.id)&&target.position.z<=cut) {
      const q=this.space.project(target.position);
      result.push({target,points:[{x:16,y:0},{x:32,y:8},{x:16,y:16},{x:0,y:8}].map(p=>({x:q.x+p.x,y:q.y+p.y})),color:'#808080'});
    }
    return result;
  }
}
