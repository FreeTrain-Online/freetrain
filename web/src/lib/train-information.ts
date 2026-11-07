import profile from '../generated/fleet-profile.json' with {type:'json'};
import type {ReadState} from './session-client.ts';
export type ReadFleet=NonNullable<NonNullable<ReadState['mixed']>['fleet']>;
export type ReadTrain=ReadFleet['trains'][number];

/** Source TrainInformationWindow: stored group member order, normal capacity,
 * integer passenger percentage (packed occupancy may exceed 100%). */
export function trainInformation(train:ReadTrain){
 const cars=train.cars.map(car=>profile.cars.find(c=>c.reference.packId===car.definition.packId&&c.reference.contributionId===car.definition.contributionId));
 if(cars.some(c=>!c))return undefined;
 const normal=cars.reduce((n,c)=>n+c!.normal,0),seated=cars.reduce((n,c)=>n+(c!.seated>=0&&c!.seated<=c!.normal?c!.seated:c!.normal),0);
 return {normal,seated,packed:seated+2*(normal-seated),passengers:train.passengers,percent:normal===0?undefined:Math.trunc(train.passengers*100/normal)};
}
export function trainSelectionOptions(fleet:ReadFleet,rootLabel:string){
 const groups=new Map(fleet.groups.map(g=>[g.id,g])),trains=new Map(fleet.trains.map(t=>[t.id,t]));
 const rows:{id:string;name:string;depth:number;group:boolean}[]=[],seen=new Set<string>(),stack=[{id:fleet.rootGroupId,depth:0}];
 while(stack.length){
  const {id,depth}=stack.pop()!;if(seen.has(id))continue;seen.add(id);
  const group=groups.get(id),train=trains.get(id);if(!group&&!train)continue;
  rows.push({id,name:group?(id===fleet.rootGroupId&&group.name==='Root'?rootLabel:group.name):train!.name,depth,group:!!group});
  if(group)for(const member of [...group.members].reverse())stack.push({id:member,depth:depth+1});
 }
 return rows;
}
export function trainLocation(read:ReadState|undefined,id:string|undefined){
 if(!id)return undefined;
 const train=read?.mixed?.fleet?.trains.find(t=>t.id===id);
 return train?train.cars[0].state.location??undefined:read?.trains?.find(t=>t.id===id)?.position;
}
export const trainLabelKey='freetrain-online.train-labels.v1';
export interface TrainLabels {visible:boolean;font:'sans-serif'|'serif'|'monospace';size:number;color:string}
export const defaultTrainLabels:TrainLabels={visible:false,font:'sans-serif',size:12,color:'#ffff00'};
export function resolveTrainLabels(value:unknown):TrainLabels{
 const v=value as Partial<TrainLabels>|null;
 return {visible:typeof v?.visible==='boolean'?v.visible:false,font:['sans-serif','serif','monospace'].includes(v?.font??'')?v!.font!:'sans-serif',
  size:Number.isInteger(v?.size)&&v!.size!>=8&&v!.size!<=32?v!.size!:12,color:v?.color?.length===7&&/^#[0-9a-f]{6}$/i.test(v.color)?v!.color!:'#ffff00'};
}
export function initialTrainLabels():TrainLabels{try{return resolveTrainLabels(JSON.parse(localStorage.getItem(trainLabelKey)??'null'));}catch{return {...defaultTrainLabels};}}
export function persistTrainLabels(value:TrainLabels){try{localStorage.setItem(trainLabelKey,JSON.stringify(resolveTrainLabels(value)));}catch{/* Optional local presentation. */}}
