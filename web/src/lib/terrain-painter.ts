import { railwayRevision } from './railway-edit.ts';
import type { CornerTerrainQuery, Position } from '../generated/contracts.ts';
import type { ShellModel } from './shell-model.ts';
import { WorldSpace } from './map-geometry.ts';

export type PaintTarget={position:Position;offsetX:number;offsetY:number;reverse:boolean};
export type PaintState={active:boolean;completed:number;status:'idle'|'painting'|'blocked'|'stopped'|'failed';reason?:string};
// One preview/commit at a time. Mouse movement replaces the next target; it never
// builds a queue of edits that could continue after release or a lost connection.
export function createTerrainPainter(model:Pick<ShellModel,'state'|'previewCorner'|'send'>,changed:(state:PaintState)=>void,interval=150){
 let generation=0,targetVersion=0,timer:ReturnType<typeof setTimeout>|undefined,request:AbortController|undefined;
 let blockedTarget:string|undefined,strokeEpoch:string|undefined;
 let target:PaintTarget|undefined,anchor:Position|undefined,action:'raise'|'lower'='raise',busy=false;
 let state:PaintState={active:false,completed:0,status:'idle'};
 const publish=(patch:Partial<PaintState>)=>{state={...state,...patch};changed(state);};
 const available=()=>{const s=model.state;return s.connection==='ready'&&!!s.read?.terrain&&(s.role==='owner'||s.role==='operator')&&(!state.active||strokeEpoch===s.read?.worldEpoch)&&!s.leaving&&!s.signedOut&&s.feedback!=='unresolved';};
 function stop(status:PaintState['status']='stopped',reason?:string){generation++;clearTimeout(timer);request?.abort();request=undefined;anchor=undefined;publish({active:false,status,reason});}
 async function dab(){
  if(!state.active||busy)return;
  if(!available())return stop('failed');
  const s=model.state,read=s.read!,t=target;if(!t)return;
  if(s.pending){timer=setTimeout(dab,interval);return;}
  const key=`${read.worldEpoch}/${railwayRevision(read)!}/${targetVersion}`;
  if(blockedTarget===key){timer=setTimeout(dab,interval);return;}
  const id=generation,version=targetVersion,space=new WorldSpace(read.map.width,read.map.height,read.map.levels);
  const position={...(anchor??t.position)};
  if(!space.containsHorizontal(position)){publish({status:'blocked',reason:'outside'});timer=setTimeout(dab,interval);return;}
  const stored=space.storage(position);position.z=read.terrain!.cells[stored.v*read.map.width+stored.h].ground;
  const query:CornerTerrainQuery={expectedWorldEpoch:read.worldEpoch,expectedGeometryRevision:railwayRevision(read)!,
   position,action:t.reverse?(action==='raise'?'lower':'raise'):action,offsetX:anchor?null:t.offsetX,offsetY:anchor?null:t.offsetY,stroke:null};
  busy=true;request=new AbortController();publish({status:'painting',reason:undefined});
  try{
   const preview=await model.previewCorner(query,request.signal);
   if(id!==generation||!state.active)return;
   const current=model.state;
   if(!available()||current.pending||current.read?.worldEpoch!==query.expectedWorldEpoch||railwayRevision(current.read)!==query.expectedGeometryRevision)return stop('failed','staleRevision');
   if(!preview.legal){blockedTarget=key;publish({status:'blocked',reason:preview.reason??'invalidInput'});return;}
   // Releasing while the read-only preview was pending prevents this command.
   const accepted=await model.send({kind:'editCorner',query});
   if(id!==generation||!state.active)return;
   if(!accepted)return stop('failed',model.state.feedback);
   if(model.state.read?.worldEpoch!==query.expectedWorldEpoch||!available())return stop('failed');
   if(version===targetVersion)anchor={...preview.position};
   publish({completed:state.completed+1,status:'painting'});
  }catch{if(id===generation&&state.active)stop('failed');}
  finally{busy=false;if(id===generation&&state.active)timer=setTimeout(dab,interval);}
 }
 return {
  get state(){return state;},
  start(next:PaintTarget,direction:'raise'|'lower'){
   if(busy||model.state.pending||!available())return false;
   stop('idle');strokeEpoch=model.state.read!.worldEpoch;target=next;targetVersion++;action=direction;publish({active:true,completed:0,status:'painting'});void dab();return true;
  },
  move(next:PaintTarget){target=next;anchor=undefined;targetVersion++;},
  stop(){stop();},
 };
}
