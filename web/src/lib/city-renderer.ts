import {landPropertyArt,landPropertyRecipe} from './land-property-art.ts';
// Stable ExAV PThreeDimStructure.VoxelImpl.draw, Commercial.heightCutColor,
// ConstructionSite.VoxelImpl.draw and QuarterViewDrawer. LGPL-2.1-or-later.
import type {Definition,Position} from '../generated/contracts.ts';
import type {ReadState} from './session-client.ts';
import {WorldSpace,type Point} from './map-geometry.ts';
import {instantiate,variableFrames,variantKey,type Layer,type RenderCatalog,type Variant} from './content-recipes.ts';
import {cityProfile} from './city-state.ts';
import {constructionParts} from './construction-renderer.ts';
export type CityTarget = {kind:'building';id:string;position:Position;definition:Definition} | {kind:'terrain';id:string;position:Position;surface:'ground'};
export type CityOperation={target:CityTarget;layer:Layer;halfHeight?:number}|{target:CityTarget;points:Point[];color:string};
const key=(p:Position)=>`${p.x},${p.y},${p.z}`;
export class CityRenderPlan {
  readonly layers:Layer[]=[];
  readonly columnIndices=new Set<number>();
  private columns=new Map<number,{operations:CityOperation[];bases:CityTarget[]}>();
  private halves=new Set<string>();
  private tops=new Map<string,number>();
  private colors=new Map<string,string>();
  private occupied=new Set<string>();
  private signatures=new Map<number,string>();
  private space:WorldSpace;
  constructor(read:ReadState,catalog:RenderCatalog,variant:Variant) {
    catalog=landPropertyArt(catalog);
    const space=this.space=new WorldSpace(read.map.width,read.map.height,read.map.levels),recipes=new Map(catalog.renderRecipes.map(r=>[r.id,r]));
    const checked=(id:string)=>{
      const r=recipes.get(id);
      if(!r||!r.frames.length||r.frames.some(f=>f.status!=='invisible'&&(f.status!=='ready'||!f.masks?.[variantKey(variant)]||!catalog.masks[f.masks[variantKey(variant)]])))throw new Error('City recipe unavailable: '+id);
      return r;
    };
    for(const b of read.mixed?.city?.structures??[]) {
      const d=cityProfile.structures.find(d=>d.reference.packId===b.definition.packId&&d.reference.contributionId===b.definition.contributionId&&d.reference.recipeId===b.definition.recipeId);
      if(!d)throw new Error('Unknown city definition');
      if(d.kind==='half'){
        const p=b.location,s=space.storage(p),index=s.v*space.horizontalSize+s.h;
        this.halves.add(b.id);this.occupied.add(key(p));this.columnIndices.add(index);
        const target:CityTarget={kind:'building',id:b.id,position:p,definition:b.definition};
        const column=this.columns.get(index)??{operations:[] as CityOperation[],bases:[] as CityTarget[]};column.bases.push(target);this.columns.set(index,column);
        const add=(recipe:ReturnType<typeof checked>,halfHeight?:number)=>{
          for(const layer of instantiate(recipe,b.id,[p.x-space.centerX,p.y,p.z])){this.layers.push(layer);column.operations.push({target,layer,halfHeight});}
        };
        add(checked('system:ground/spring'));
        // Source paints Back before Fore; recipe Side uses Fore=0, Back=1.
        for(const half of [...b.halves].sort((a,b)=>(a.side==='back'?0:1)-(b.side==='back'?0:1))){
          const recipe=checked(half.definition.recipeId),height=cityProfile.structures.find(d=>d.reference.recipeId===half.definition.recipeId)!.minimumHeight;
          const frames=recipe.frames.filter(f=>f.selector.direction===half.direction&&f.selector.side===(half.side==='fore'?0:1)&&f.selector.color===half.color);
          if(recipe.handler!=='half-voxel'||frames.length!==1)throw new Error('Half recipe unavailable');
          add({...recipe,handler:'fixed-3d',frames},height);
        }
        continue;
      }
      const recipe=checked(b.definition.recipeId),constructing=b.construction.length>0;
      if(recipe.handler!==(d.kind==='variable'?'variable-height':'fixed-3d'))throw new Error('City footprint recipe unavailable: '+b.definition.recipeId);
      const expanded=d.kind==='variable'?variableFrames(recipe,b.height):recipe.frames;
      const construction=new Map(b.construction.map(c=>[key(c.offset),c.cell]));
      this.tops.set(b.id,b.location.z+b.height);this.colors.set(b.id,d.kind==='variable'?'#808080':'#b37333');
      for(let x=0;x<d.sizeX;x++)for(let y=0;y<d.sizeY;y++)for(let z=0;z<b.height;z++) {
        const p={x:b.location.x+x,y:b.location.y+y,z:b.location.z+z},s=space.storage(p),index=s.v*space.horizontalSize+s.h;
        this.occupied.add(key(p));this.columnIndices.add(index);
        const column=this.columns.get(index)??{operations:[],bases:[]};this.columns.set(index,column);
        const target:CityTarget={kind:'building',id:b.id,position:p,definition:b.definition};
        const frames=expanded.filter(f=>f.cell[0]===x&&f.cell[1]===y&&f.cell[2]===z);
        if(!frames.length)throw new Error('City cell unavailable: '+b.definition.recipeId);
        const add=(r:typeof recipe)=>{for(const layer of instantiate(r,b.id,[p.x-space.centerX,p.y,p.z])){this.layers.push(layer);column.operations.push({target,layer});}};
        if(constructing){
          for(const part of constructionParts(x,y,d.sizeX,d.sizeY,p.z===read.terrain!.cells[index].ground,construction.get(key({x,y,z}))!))add(checked(`system:construction/${part}`));
        }else{
          if(z===0)column.bases.push(target);
          add({...recipe,handler:'fixed-3d',frames:frames.map(f=>({...f,cell:[0,0,0]}))});
        }
      }
    }
    for(const ground of read.mixed?.city?.ground??[]){
      const d=cityProfile.ground.find(d=>d.reference.recipeId===ground.definition.recipeId);
      if(!d||!['land-0d','rice'].includes(d.handler))continue;
      const p=ground.location,hv=space.storage(p),index=hv.v*space.horizontalSize+hv.h;
      const target:CityTarget={kind:'terrain',id:ground.id,position:p,surface:'ground'};
      this.occupied.add(key(p));this.columnIndices.add(index);
      const column=this.columns.get(index)??{operations:[],bases:[]};this.columns.set(index,column);
      for(const layer of instantiate(checked(ground.definition.recipeId),ground.id,[p.x-space.centerX,p.y,p.z],d.handler==='rice'?{selectors:{x:Math.floor(ground.variant/3),y:ground.variant%3}}:{})){this.layers.push(layer);column.operations.push({target,layer});}
    }
    for(const property of read.mixed?.city?.properties??[]){
      const p=property.location,hv=space.storage(p),index=hv.v*space.horizontalSize+hv.h;
      const target:CityTarget={kind:'terrain',id:property.id,position:p,surface:'ground'};
      this.occupied.add(key(p));this.columnIndices.add(index);
      const column=this.columns.get(index)??{operations:[],bases:[]};this.columns.set(index,column);
      for(const layer of instantiate(checked(landPropertyRecipe),property.id,[p.x-space.centerX,p.y,p.z])){this.layers.push(layer);column.operations.push({target,layer});}
    }
    for(const [index,column] of this.columns)this.signatures.set(index,JSON.stringify([column,column.bases.map(t=>this.tops.get(t.id))]));
  }
  contains(p:Position):boolean{return this.occupied.has(key(p));}
  signature(index:number):string|undefined{return this.signatures.get(index);}
  changedColumns(previous:CityRenderPlan):number[]{return [...new Set([...this.columnIndices,...previous.columnIndices])].filter(i=>this.signature(i)!==previous.signature(i));}
  operations(index:number,cut:number,water:number,full:boolean):CityOperation[]{
    const column=this.columns.get(index);if(!column)return [];
    const halfOps:CityOperation[]=[];
    for(const target of column.bases)if(this.halves.has(target.id)&&target.position.z<=cut&&(!full||target.position.z>=water)){
      if(!full){const q=this.space.project(target.position);halfOps.push({target,points:[{x:16,y:0},{x:32,y:8},{x:16,y:16},{x:0,y:8}].map(p=>({x:q.x+p.x,y:q.y+p.y})),color:'#925e2a'});}
      halfOps.push(...column.operations.filter(op=>op.target.id===target.id&&'layer' in op&&(full||op.halfHeight!==undefined&&op.halfHeight<cut-target.position.z)));
    }

    const normal=(id:string)=>full||cut>=this.tops.get(id)!;
    // Construction ignores heightCutDiff; it draws every traversed voxel.
    const built=new Set(column.bases.map(t=>t.id));
    const result=column.operations.filter(op=>!this.halves.has(op.target.id)&&(!built.has(op.target.id)||normal(op.target.id))&&op.target.position.z<=cut&&(!full||op.target.position.z>=water));
    for(const target of column.bases)if(!this.halves.has(target.id)&&!normal(target.id)&&target.position.z<=cut){
      const q=this.space.project(target.position);
      result.push({target,points:[{x:16,y:0},{x:32,y:8},{x:16,y:16},{x:0,y:8}].map(p=>({x:q.x+p.x,y:q.y+p.y})),color:this.colors.get(target.id)!});
    }
    return [...halfOps,...result].sort((a,b)=>a.target.position.z-b.target.position.z);
  }
}
