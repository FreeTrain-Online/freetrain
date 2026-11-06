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
