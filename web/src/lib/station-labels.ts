import type {ReadState} from './session-client.ts';
import {WorldSpace} from './map-geometry.ts';
import type {TrainLabels} from './train-information.ts';
export type StationLabels=TrainLabels;
export const stationLabelKey='freetrain-online.station-labels.v1';
export const defaultStationLabels:StationLabels={visible:true,font:'sans-serif',size:12,color:'#ffffff'};
export function resolveStationLabels(value:unknown):StationLabels {
  const v=value as Partial<StationLabels>|null;
  return {visible:typeof v?.visible==='boolean'?v.visible:true,font:['sans-serif','serif','monospace'].includes(v?.font??'')?v!.font!:'sans-serif',
    size:Number.isInteger(v?.size)&&v!.size!>=8&&v!.size!<=32?v!.size!:12,color:typeof v?.color==='string'&&/^#[0-9a-f]{6}$/i.test(v.color)?v.color:'#ffffff'};
}
export function initialStationLabels():StationLabels {try{return resolveStationLabels(JSON.parse(localStorage.getItem(stationLabelKey)??'null'));}catch{return {...defaultStationLabels};}}
export function persistStationLabels(value:StationLabels) {try{localStorage.setItem(stationLabelKey,JSON.stringify(resolveStationLabels(value)));}catch{/* Optional local presentation. */}}
/** Source labels use the base projection (not footprint center), Y-16,
 * after world art and before train names. The source does not filter by cut. */
export function stationNamePositions(read:ReadState) {
  const space=new WorldSpace(read.map.width,read.map.height,read.map.levels);
  return (read.mixed?.stationGraph?.buildings??[]).map(b=>{const p=space.project(b.location);return {id:b.id,name:b.name,x:p.x,y:p.y-16};});
}
