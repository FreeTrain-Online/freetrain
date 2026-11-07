import type {ReadState} from './session-client.ts';
type Fleet=NonNullable<NonNullable<ReadState['mixed']>['fleet']>;

// Iterative traversal bounds both legitimate deep trees and malformed input.
export function fleetGroupOptions(fleet:Fleet,rootLabel?:string){
 const groups=new Map(fleet.groups.map(g=>[g.id,g])),seen=new Set<string>();
 const rows:{id:string;label:string;depth:number}[]=[],stack=[{id:fleet.rootGroupId,depth:0}];
 while(stack.length){
  const {id,depth}=stack.pop()!,group=groups.get(id);if(!group||seen.has(id))continue;seen.add(id);
  rows.push({id,label:`${'› '.repeat(Math.min(depth,8))}${id===fleet.rootGroupId&&group.name==='Root'&&rootLabel?rootLabel:group.name} · #${id}`,depth});
  for(const member of [...group.members].reverse())if(groups.has(member))stack.push({id:member,depth:depth+1});
 }
 return rows;
}
export function canMoveFleetGroup(fleet:Fleet,id:string,parentId:string){
 const groups=new Map(fleet.groups.map(g=>[g.id,g])),target=groups.get(id),seen=new Set<string>();
 if(!target||id===fleet.rootGroupId||target.parentId===parentId)return false;
 let parent=groups.get(parentId);if(!parent)return false;
 while(parent){
  if(parent.id===id||seen.has(parent.id))return false;seen.add(parent.id);
  if(parent.parentId===null)return true;
  parent=groups.get(parent.parentId);if(!parent)return false;
 }
 return false;
}
