import {cityProfile} from './city-state.ts';
export const groundCatalogDefinitions=cityProfile.ground.filter(d=>['land-0d','random-land','forest','rice'].includes(d.handler));
export const mixedGroundDefinitions=groundCatalogDefinitions.filter(d=>d.handler==='random-land');
export const staticGroundDefinitions=cityProfile.ground.filter(d=>d.handler==='land-0d');
import type {LandQuery,Position} from '../generated/contracts.ts';
import type {ReadState} from './session-client.ts';
import {railwayRevision} from './railway-edit.ts';
export function landQuery(read:ReadState,anchor:Position,end:Position,action:LandQuery['action'],definition?:LandQuery['definition']):LandQuery|undefined {
 const revision=railwayRevision(read);if(revision===undefined)return;
 return {expectedWorldEpoch:read.worldEpoch,expectedGeometryRevision:revision,expectedMinute:read.tick,
  expectedContentVersion:read.contentVersion as LandQuery['expectedContentVersion'],action,anchor:{...anchor},end:{...end},...(definition?{definition}:{})};
}

export const forestGroundDefinitions=groundCatalogDefinitions.filter(d=>d.handler==='forest');
const forestSpecies=['treeCedar','treePine','treeCamphor','treeMaple','treeGinkgo','treeBeech'] as const;
export function forestGroundLabels(id:string){
 const i=forestGroundDefinitions.findIndex(d=>d.reference.recipeId===id);
 return {species:forestSpecies[i%6],ground:(['forestNatural','forestMountain','forestGrass'] as const)[Math.floor(i/6)]};
}

export const riceGroundDefinitions=groundCatalogDefinitions.filter(d=>d.handler==='rice');
