import {readFileSync} from 'node:fs';
import {createHash} from 'node:crypto';

/** A real moving-ring save, expanded to exercise progressive viewport builds. */
export function wideMovingFleetSave(twoTrains:boolean|number=false) {
 const count=typeof twoTrains==='number'?twoTrains:twoTrains?2:1;
 const save=JSON.parse(readFileSync('tests/helpers/moving-fleet-v9.json','utf8'));
 save.map.width=512;
 save.packedTerrain.cells=Buffer.alloc(512*64,0x42).toString('base64');
 const provenance=save.packedTerrain.provenance,request=JSON.parse(provenance.requestJson);
 request.Dimensions.H=512;provenance.requestJson=JSON.stringify(request);
 provenance.requestHash=createHash('sha256').update(provenance.requestJson).digest('hex');
 const shift=(p:{x:number;y:number},distance:number)=>{p.x+=distance;p.y+=distance;};
 for(const entity of save.spatial.entities)shift(entity.anchor,224);
 for(const voxel of save.mixed.voxels)shift(voxel.position,224);
 for(const train of save.mixed.fleet.trains)for(const car of train.cars)if(car.state.location)shift(car.state.location,224);
 const base=structuredClone(save);
 for(let index=1;index<count;index++) {
  const id=(value:string)=>String(BigInt(value)+BigInt(index*1000));
  const entities=structuredClone(base.spatial.entities),voxels=structuredClone(base.mixed.voxels),train=structuredClone(base.mixed.fleet.trains[0]);
  const distance=index*(typeof twoTrains==='number'?16:32);
  for(const entity of entities){entity.id=id(entity.id);shift(entity.anchor,distance);}
  for(const voxel of voxels) {
   voxel.ownerId=id(voxel.ownerId);shift(voxel.position,distance);
   if(voxel.traffic?.car){voxel.traffic.car.trainId=id(voxel.traffic.car.trainId);voxel.traffic.car.carId=id(voxel.traffic.car.carId);}
  }
  train.id=id(train.id);train.name=`Ring ${index+1}`;
  for(const car of train.cars){car.id=id(car.id);shift(car.state.location,distance);}
  save.spatial.entities.push(...entities);save.mixed.voxels.push(...voxels);save.mixed.fleet.trains.push(train);
  save.mixed.fleet.groups[0].members.push(train.id);
  save.spatial.nextEntityId=save.mixed.nextEntityId=String(index*1000+42);
  for(const queue of [save.clock,save.mixed.queue]) {
   queue.pending.push({...queue.pending[0],registration:String(index),order:String(index),handlerId:`train/${train.id}`});queue.nextOrder=String(index+1);
  }
 }
 return save;
}

/** Flat approaches, curves, bridges and tunnel portals share each moving ring. */
export function tunnelMovingFleetSave() {
 const save=wideMovingFleetSave(2),profile=JSON.parse(readFileSync('src/generated/railway-profile.json','utf8'));
 const tunnel=profile.bindings.find((b:any)=>b.handler==='tunnel').reference;
 const bridge=profile.bindings.find((b:any)=>b.handler==='bridge').reference;
 for(const v of save.mixed.voxels) {
  const rail=v.traffic?.rail;if(!rail)continue;
  if(rail.pattern===10)Object.assign(rail,{definition:tunnel,variant:1,terrainBackup:{ne:0,se:0,sw:0,nw:0}});
  if(rail.pattern===4)Object.assign(rail,{definition:bridge,variant:0});
 }
 return save;
}
