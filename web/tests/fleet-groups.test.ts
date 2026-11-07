import assert from 'node:assert/strict';
import {fleetGroupOptions,canMoveFleetGroup} from '../src/lib/fleet-groups.ts';
import {effectiveFleetController,effectiveFleetGroupController} from '../src/lib/fleet-state.ts';
import {parseMessageValue,validWireShape} from '../src/lib/protocol.ts';
import type {Snapshot,FleetGroup} from '../src/generated/contracts.ts';
async function scene(){
 const read=parseMessageValue(JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/fixtures/snapshot-fleet.json',import.meta.url)))) as Snapshot;
 return read.mixed!.fleet!;
}
Deno.test('MAN-A17 group selectors preserve stored sibling order and exclude root, self, descendants and existing parent',async()=>{
 const f=await scene(),root=f.groups.find(g=>g.id===f.rootGroupId)!,d=f.controllers.find(c=>c.kind==='delegation')!;
 f.groups=[root,{id:'9007199254740993',revision:'0',name:'支線',parentId:root.id,controllerId:d.id,members:['9007199254740994']},
  {id:'9007199254740994',revision:'0',name:'Child',parentId:'9007199254740993',controllerId:d.id,members:[]},
  {id:'9007199254740995',revision:'0',name:'Other',parentId:root.id,controllerId:null,members:[]}];
 root.members=['9007199254740995','9007199254740993'];
 const before=JSON.stringify(f),rows=fleetGroupOptions(f,'ルート');
 assert.deepEqual(rows.map(r=>[r.id,r.depth]),[[root.id,0],['9007199254740995',1],['9007199254740993',1],['9007199254740994',2]]);
 assert.equal(canMoveFleetGroup(f,root.id,'9007199254740993'),false);
 for(const id of [root.id,'9007199254740993','9007199254740994','999'])assert.equal(canMoveFleetGroup(f,'9007199254740993',id),false);
 assert.equal(canMoveFleetGroup(f,'9007199254740993','9007199254740995'),true);
 assert.equal(JSON.stringify(f),before);
});
Deno.test('group and train effective identities distinguish null fallback, inheritance, ancestor changes and retained overrides',async()=>{
 const f=await scene(),root=f.groups.find(g=>g.id===f.rootGroupId)!,train=f.trains[0],simple=f.controllers.find(c=>c.kind==='simple')!,delegation=f.controllers.find(c=>c.kind==='delegation')!;
 const timetable=f.controllers.find(c=>c.kind==='timetable')!;
 root.controllerId=timetable.id;
 const group:FleetGroup={id:'999',revision:'0',name:'Branch',parentId:root.id,controllerId:delegation.id,members:[train.id]};f.groups.push(group);train.groupId=group.id;train.controllerId=delegation.id;
 assert.equal(effectiveFleetGroupController(f,group.id)?.id,timetable.id);assert.equal(effectiveFleetController(f,train.id)?.id,timetable.id);
 train.controllerId=simple.id;root.controllerId=delegation.id;assert.equal(effectiveFleetController(f,train.id)?.id,simple.id);
 train.controllerId=delegation.id;assert.equal(effectiveFleetController(f,train.id)?.id,simple.id);
 group.controllerId=null;root.controllerId=timetable.id;
 assert.equal(effectiveFleetController(f,train.id)?.id,simple.id);assert.equal(effectiveFleetGroupController(f,group.id)?.id,simple.id);
});
Deno.test('bounded iterative selectors and group lookup terminate on the deepest admitted tree and malformed cycles',async()=>{
 const f=await scene(),root=f.groups.find(g=>g.id===f.rootGroupId)!,d=f.controllers.find(c=>c.kind==='delegation')!;
 f.groups=[root];root.members=['1000'];root.controllerId=d.id;
 for(let n=0;n<255;n++)f.groups.push({id:String(1000+n),revision:'0',name:'Group',parentId:n?String(999+n):root.id,controllerId:d.id,members:n===254?[]:[String(1001+n)]});
 assert.equal(fleetGroupOptions(f).length,256);assert.equal(effectiveFleetGroupController(f,'1254')?.kind,'simple');
 f.groups[1].parentId='1254';f.groups.at(-1)!.members=['1000'];
 assert.equal(canMoveFleetGroup(f,'1000','1254'),false);assert.equal(effectiveFleetGroupController(f,'1254'),undefined);
 assert.equal(fleetGroupOptions(f).length,256);
});
Deno.test('group wire commands retain Int64 identity strings, bounded names and inert operations',()=>{
 const command={type:'command',requestId:'groups',expectedWorldEpoch:'1',payload:{kind:'manageFleetGroup',action:'move',targetKind:'group',targetId:'9007199254740993',expectedRevision:'2',name:'',parentId:'9007199254740995',expectedParentRevision:'3'}};
 assert.equal(validWireShape(command),true);
 for(const patch of [{action:'delete'},{targetKind:'type'},{targetId:9007199254740992},{parentId:'-1'},{name:'a'.repeat(257)},{extra:true}])assert.equal(validWireShape({...command,payload:{...command.payload,...patch}}),false);
 assert.equal(validWireShape({...command,payload:{...command.payload,action:'rename',name:'本線',parentId:null,expectedParentRevision:null}}),true);
});
