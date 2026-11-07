import type {ReadState} from './session-client.ts';
type Fleet=NonNullable<NonNullable<ReadState['mixed']>['fleet']>;
export function controllerUsages(fleet:Fleet,id:string){
 return {trains:fleet.trains.filter(t=>t.controllerId===id),groups:fleet.groups.filter(g=>g.controllerId===id)};
}
export function controllerCopyName(fleet:Fleet,id:string){
 const source=fleet.controllers.find(c=>c.id===id);if(!source)return '';
 for(let n=1;;n++){const end=` ${n}`,name=source.name.slice(0,256-end.length)+end;if(!fleet.controllers.some(c=>c.name===name))return name;}
}
export const validControllerName=(name:string)=>!!name.trim()&&name.trim().length<=256&&!/[\u0000-\u001f\u007f-\u009f]/u.test(name);
export function controllerJunctionLocations(read:ReadState){
 return read.mixed?.voxels.filter(v=>v.traffic?.rail?.pattern!==undefined&&v.traffic.rail.pattern>=12&&v.traffic.rail.pattern<28).map(v=>v.position)??[];
}
export function missingControllerJunctions(read:ReadState,id:string){
 const keys=new Set(controllerJunctionLocations(read).map(p=>`${p.x},${p.y},${p.z}`));
 return read.mixed?.fleet?.controllers.find(c=>c.id===id)?.junctions?.filter(j=>!keys.has(`${j.location.x},${j.location.y},${j.location.z}`))??[];
}
