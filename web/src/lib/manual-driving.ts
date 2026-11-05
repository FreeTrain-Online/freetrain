import {effectiveFleetController} from './fleet-state.ts';
import type {ReadState} from './session-client.ts';

// The prototype and canonical fleet share transport intent, never domain state.
export function drivingTrain(read:ReadState|undefined,id:string|undefined) {
  const legacy=read?.trains.find(t=>t.id===id);
  if(legacy)return legacy;
  const fleet=read?.mixed?.fleet,train=fleet?.trains.find(t=>t.id===id);
  if(!fleet||!train)return;
  const controller=effectiveFleetController(fleet,train.id);
  return {id:train.id,name:train.name,revision:train.revision,controller:controller?.kind==='simple'?'automatic':controller?.kind,
    controlLease:read?.fleetControl?.find(c=>c.trainId===id)?.lease,placed:train.state!=='unplaced'};
}

export function validFleetControl(read:Pick<ReadState,'fleetControl'|'mixed'>):boolean {
  const seen=new Set<string>();
  return !read.fleetControl||read.fleetControl.every(c=>!seen.has(c.trainId)&&!!read.mixed?.fleet?.trains.some(t=>t.id===c.trainId)&&
    (seen.add(c.trainId),true));
}
