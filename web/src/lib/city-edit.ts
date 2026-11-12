import type {CityQuery} from '../generated/contracts.ts';
import type {ReadState} from './session-client.ts';
export function cityQuery(read:ReadState,ownerId:string):CityQuery|undefined {
 const owner=read.mixed?.city?.structures.find(s=>s.id===ownerId);if(!owner)return;
 return {expectedWorldEpoch:read.worldEpoch,expectedRevision:read.revision,expectedMinute:read.tick,
  expectedContentVersion:read.contentVersion,ownerId,expectedOwnerRevision:owner.revision};
}
export function currentCityDraft(read:ReadState,query:CityQuery):boolean {
 return JSON.stringify(cityQuery(read,query.ownerId))===JSON.stringify(query);
}
