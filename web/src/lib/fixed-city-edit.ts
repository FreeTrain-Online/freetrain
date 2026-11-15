import type {Definition,Position,FixedCityQuery} from '../generated/contracts.ts';
import type {ReadState} from './session-client.ts';
import type {RenderCatalog} from './content-recipes.ts';
import {cityProfile} from './city-state.ts';
import {contentText,bundledLocalization} from './content-localization.ts';
import {railwayRevision} from './railway-edit.ts';
export {stationBuildingFence as fixedCityFence} from './station-building-edit.ts';
// Exact authored identities carry their own footprint and baked orientation.
export const fixedCityDefinitions=cityProfile.structures.filter(d=>['fixed','variable','half'].includes(d.kind));
export interface CityCatalogChoice {height:number;side:'back'|'fore';direction:0|2|4|6;color:number}
export function cityCatalogChoice(id:string,choices:Record<string,CityCatalogChoice>={}):CityCatalogChoice {
 const d=fixedCityDefinitions.find(d=>d.reference.recipeId===id)!;
 const v=d.halfVariants.find(v=>v.direction===0&&v.side===0)??d.halfVariants[0];
 return choices[id]??{height:d.minimumHeight,side:v?.side===1?'fore':'back',direction:(v?.direction??0) as 0|2|4|6,color:v?.color??0};
}
export function fixedCityAt(read:ReadState,p:Position){return read.mixed?.city?.structures.find(b=>{
 const d=fixedCityDefinitions.find(d=>d.reference.recipeId===b.definition.recipeId);
 return d&&p.x>=b.location.x&&p.x<b.location.x+d.sizeX&&p.y>=b.location.y&&p.y<b.location.y+d.sizeY&&p.z>=b.location.z&&p.z<b.location.z+(d.kind==='half'?1:b.height);
});}
export function fixedCityQuery(read:ReadState,position:Position,definition:FixedCityQuery['definition'],remove=false,height?:number,half?:FixedCityQuery['half']):FixedCityQuery|undefined{
 const revision=railwayRevision(read);if(revision===undefined)return;
 const b=remove?fixedCityAt(read,position):undefined;if(remove&&!b)return;
 return {expectedWorldEpoch:read.worldEpoch,expectedGeometryRevision:revision,expectedMinute:read.tick,expectedContentVersion:read.contentVersion as FixedCityQuery['expectedContentVersion'],action:remove?'remove':'place',position,definition:remove?null:definition,ownerId:b?.id??'0',expectedOwnerRevision:b?.revision??'0',...(!remove&&height!==undefined?{height}:{}),...(half?{half}:{})};
}
export function fixedCityCatalog(catalog:RenderCatalog,locale:string){return fixedCityDefinitions.filter(d=>!catalog.contributions!.find(c=>c.id===d.reference.contributionId)!.declaration.children.some(c=>c.tag==='playerCannotBuild')).map(d=>{
 const c=catalog.contributions!.find(c=>c.id===d.reference.contributionId)!;
 const localized=(tag:string)=>contentText(bundledLocalization,catalog,c,tag,c.declaration.children.find(c=>c.tag===tag)?.text??'',locale);
 const sourceGroup=c.declaration.children.find(c=>c.tag==='group')?.text??'',sourceSubgroup=c.declaration.children.find(c=>c.tag==='subgroup')?.text??'';
 return {...d,pluginId:c.pluginId,title:localized('name')||localized('group'),description:localized('description'),orientation:localized('type'),sourceGroup,sourceSubgroup,group:localized('group'),subgroup:localized('subgroup')};
});}
// Completion replaces the site identity. Follow only that one source lifecycle
// handoff; deletion, rebuilding and replacement must not select another object.
export interface FixedCityInspection {epoch:string;id:string;definition:Definition;location:Position;height:number;allocator:string}
export function fixedCityInspection(read:ReadState,id:string):FixedCityInspection|undefined{
 const site=read.mixed?.city?.structures.find(b=>b.id===id);
 if(!site?.construction.length)return;
 return {epoch:read.worldEpoch,id,definition:{...site.definition},location:{...site.location},height:site.height,allocator:read.mixed!.nextEntityId};
}
export function completedFixedCityOwner(site:FixedCityInspection|undefined,after:ReadState,id:string){
 if(!site||site.id!==id||site.epoch!==after.worldEpoch||after.mixed?.city?.structures.some(b=>b.id===id))return;
 return after.mixed?.city?.structures.find(b=>!b.construction.length&&BigInt(b.id)>=BigInt(site.allocator)&&
  b.definition.packId===site.definition.packId&&b.definition.contributionId===site.definition.contributionId&&b.definition.recipeId===site.definition.recipeId&&
  b.location.x===site.location.x&&b.location.y===site.location.y&&b.location.z===site.location.z&&b.height===site.height);
}
