import type {ReadState} from './session-client.ts';
import type {Locale} from './localization.ts';
import type {StationInformationQuery} from '../generated/contracts.ts';
export function stationList(read:ReadState|undefined,locale:Locale){
 const compare=new Intl.Collator(locale,{numeric:true}).compare;
 return [...(read?.mixed?.stationGraph?.buildings??[])].sort((a,b)=>compare(a.name,b.name)||(BigInt(a.id)<BigInt(b.id)?-1:BigInt(a.id)>BigInt(b.id)?1:0));
}
export function stationInformationQuery(read:ReadState,stationId='0',platformId='0',trainId='0'):StationInformationQuery {
 return {expectedWorldEpoch:read.worldEpoch,expectedRevision:read.revision,expectedMinute:read.tick,stationId,platformId,trainId};
}
export function currentStationInformation(read:ReadState|undefined,q:StationInformationQuery){
 return !!read&&read.worldEpoch===q.expectedWorldEpoch&&read.revision===q.expectedRevision&&read.tick===q.expectedMinute;
}
