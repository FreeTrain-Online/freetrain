import type {Position,StationBuildingQuery} from '../generated/contracts.ts';
import type {ReadState} from './session-client.ts';
import type {RenderCatalog} from './content-recipes.ts';
import {contentText,bundledLocalization} from './content-localization.ts';
import {railwayFence,railwayRevision} from './railway-edit.ts';
import profile from '../generated/station-profile.json' with {type:'json'};
export const stationDefinitions=profile.definitions;
export const stationBuildingFence=(read?:ReadState)=>`${railwayFence(read)}/${read?.tick}`;
export function stationBuildingAt(read:ReadState,p:Position){
 return read.mixed?.stationGraph?.buildings.find(b=>{
  const d=stationDefinitions.find(d=>d.reference.recipeId===b.definition.recipeId);
  return d&&p.x>=b.location.x&&p.x<b.location.x+d.sizeX&&p.y>=b.location.y&&p.y<b.location.y+d.sizeY&&p.z>=b.location.z&&p.z<b.location.z+d.height;
 });
}
export function stationBuildingQuery(read:ReadState,position:Position,definition:StationBuildingQuery['definition'],remove=false):StationBuildingQuery|undefined {
 const revision=railwayRevision(read);if(revision===undefined)return;
 const b=remove?stationBuildingAt(read,position):undefined;
 if(remove&&!b)return;
 return {expectedWorldEpoch:read.worldEpoch,expectedGeometryRevision:revision,expectedMinute:read.tick,expectedContentVersion:read.contentVersion as StationBuildingQuery['expectedContentVersion'],action:remove?'remove':'place',position,definition:remove?null:definition,stationId:b?.id??'0',expectedStationRevision:b?.revision??'0'};
}
// Authored orientations are separate contributions. Never rotate an existing
// footprint or infer another contribution's heading from its dimensions.
export function stationCatalog(catalog:RenderCatalog,locale:string){return stationDefinitions.map(d=>{
 const contribution=catalog.contributions!.find(c=>c.id===d.reference.contributionId)!;
 const field=(tag:string)=>contribution.declaration.children.find(c=>c.tag===tag)?.text??'';
 const localized=(tag:string)=>contentText(bundledLocalization,catalog,contribution,tag,field(tag),locale);
 const recipe=catalog.renderRecipes.find(r=>r.id===d.reference.recipeId)!;
 return {...d,title:localized('name')||localized('group'),description:localized('description'),orientation:localized('type'),price:String(recipe.fields.price),upkeep:String(recipe.fields.operationCost)};
});}
