import type {ReadState} from './session-client.ts';
import type {FleetRule} from '../generated/contracts.ts';
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
export const maximumLocationRules=1024,maximumControllerRules=8192;
export const blankFleetRule=(junction:boolean):FleetRule=>({month:-1,day:-1,dayOfWeek:-1,hour:-1,minutes:-1,action:junction?'straight':'pass'});
export const sameRuleMask=(a:FleetRule,b:FleetRule)=>a.month===b.month&&a.day===b.day&&a.dayOfWeek===b.dayOfWeek&&a.hour===b.hour&&a.minutes===b.minutes;
export function validFleetRule(r:FleetRule,junction:boolean):boolean {
 return [r.month,r.day,r.dayOfWeek,r.hour,r.minutes].every(Number.isInteger)&&
  (r.month===-1||r.month>=1&&r.month<=12)&&(r.day===-1||r.day>=1&&r.day<=31)&&r.dayOfWeek>=-1&&r.dayOfWeek<=6&&r.hour>=-1&&r.hour<=23&&
  (junction?r.minutes===-1&&['straight','curve'].includes(r.action):
   (r.minutes===-1||r.minutes>=0&&r.minutes<=50&&r.minutes%10===0)&&['pass','stop','go','reverse','forwarding','reverseforwarding','reverse2','rforwarding2'].includes(r.action));
}
