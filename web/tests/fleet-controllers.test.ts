import assert from 'node:assert/strict';
import {controllerUsages,controllerCopyName,validControllerName} from '../src/lib/fleet-controllers.ts';
import {effectiveFleetController} from '../src/lib/fleet-state.ts';
import {parseMessageValue,validWireShape} from '../src/lib/protocol.ts';
import type {Snapshot} from '../src/generated/contracts.ts';
const scene=async()=>parseMessageValue(JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/fixtures/snapshot-fleet.json',import.meta.url)))) as Snapshot;
Deno.test('manager diagnostics distinguish direct references and effective delegation without changing the graph',async()=>{
 const read=await scene(),f=read.mixed!.fleet!,simple=f.controllers.find(c=>c.kind==='simple')!,delegation=f.controllers.find(c=>c.kind==='delegation')!;
 const manual={...simple,id:'9007199254740997',name:'Shared',kind:'manual' as const};f.controllers.push(manual);
 f.trains[0].controllerId=delegation.id;f.groups.find(g=>g.id===f.trains[0].groupId)!.controllerId=manual.id;
 const before=JSON.stringify(f),usages=controllerUsages(f,manual.id);
 assert.equal(usages.groups.length,1);assert.equal(usages.trains.length,0);
 assert.equal(effectiveFleetController(f,f.trains[0].id)!.id,manual.id);
 f.trains[0].controllerId=manual.id;assert.equal(controllerUsages(f,manual.id).trains.length,1);
 f.trains[0].controllerId=delegation.id;assert.equal(JSON.stringify(f),before);
});
Deno.test('copy names use the first unused source numeric suffix and preserve bounded Unicode names',async()=>{
 const f=(await scene()).mixed!.fleet!,c=f.controllers[0];c.name='夜行';
 f.controllers.push({...c,id:'999',name:'夜行 1'},{...c,id:'1000',name:'夜行 3'});
 assert.equal(controllerCopyName(f,c.id),'夜行 2');c.name='a'.repeat(256);assert.equal(controllerCopyName(f,c.id).length,256);
 for(const n of ['',' ','\n','x\u007f', 'x'.repeat(257)])assert.equal(validControllerName(n),false);
 assert.equal(validControllerName('  夜行・Night  '),true);
});
Deno.test('manager and assignment commands retain exact Int64 strings, bounded fields and explicit inert configuration',()=>{
 const command={type:'command',requestId:'manager',expectedWorldEpoch:'1',payload:{kind:'manageFleetController',action:'copy',controllerId:'9007199254740997',expectedRevision:'2',name:'',provider:null,harbors:null,location:null}};
 assert.equal(validWireShape(command),true);
 assert.equal(validWireShape({...command,payload:{...command.payload,controllerId:9007199254740996}}),false);
 assert.equal(validWireShape({...command,payload:{...command.payload,action:'activateType'}}),false);
 assert.equal(validWireShape({...command,payload:{...command.payload,harbors:Array(8193).fill({})}}),false);
 const assign={...command,payload:{kind:'assignFleetController',targetKind:'group',targetId:'3',expectedRevision:'1',controllerId:'9007199254740997',expectedControllerRevision:'2'}};
 assert.equal(validWireShape(assign),true);assert.equal(validWireShape({...assign,payload:{...assign.payload,targetKind:'signal'}}),false);
 assert.equal(validWireShape({type:'rejected',requestId:'delete',code:'controllerReferenced',arguments:{}}),true);
});
Deno.test('TAT junction patches bound routes and geometry and support explicit cleanup',()=>{
 const payload={kind:'manageFleetController',action:'configure',controllerId:'9',expectedRevision:'2',name:'',provider:null,harbors:null,location:{x:22,y:20,z:2},junctions:[{location:{x:22,y:20,z:2},defaultRoute:'curve'}],expectedGeometryRevision:'7'};
 const command={type:'command',requestId:'tat',expectedWorldEpoch:'1',payload};
 assert.equal(validWireShape(command),true);
 assert.equal(validWireShape({...command,payload:{...payload,junctions:[...payload.junctions,...payload.junctions]}}),false);
 assert.equal(validWireShape({...command,payload:{...payload,junctions:[{...payload.junctions[0],defaultRoute:'left'}]}}),false);
 assert.equal(validWireShape({...command,payload:{...payload,expectedGeometryRevision:7}}),false);
 assert.equal(validWireShape({...command,payload:{...payload,action:'cleanJunctions',location:null,junctions:null}}),true);
});
Deno.test('TAT missing-junction diagnostics preserve saved location keys',async()=>{
 const {missingControllerJunctions}=await import('../src/lib/fleet-controllers.ts');
 const read=await scene(),c=read.mixed!.fleet!.controllers[0],p=read.mixed!.voxels.find(v=>v.traffic?.rail)!;
 p.traffic!.rail!.pattern=23;c.junctions=[{location:{...p.position},defaultRoute:'curve'},{location:{x:-1,y:0,z:0},defaultRoute:'straight'}];
 const before=JSON.stringify(read);
 assert.deepEqual(missingControllerJunctions(read,c.id),[c.junctions[1]]);assert.equal(JSON.stringify(read),before);
});
