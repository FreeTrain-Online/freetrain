import type {Snapshot,CityStructure} from '../../src/generated/contracts.ts';
import seed from '../fixtures/fixed-city-v11.json.snapshot.json' with {type:'json'};
import {stationScene} from './station-scene.ts';
import {cityProfile} from '../../src/lib/city-state.ts';
import {WorldSpace} from '../../src/lib/map-geometry.ts';
export const fixedDefinitions=cityProfile.structures.filter(d=>d.kind==='fixed');
export function cityScene(width=40,height=60):Snapshot {
 const s=stationScene(width,height);s.map.levels=32;
 const r=JSON.parse(s.terrain!.provenance.requestJson);r.Dimensions.Z=32;s.terrain!.provenance.requestJson=JSON.stringify(r);
 const city=structuredClone(seed.mixed.city) as NonNullable<NonNullable<Snapshot['mixed']>['city']>;
 city.structures=[];city.producers=[];city.properties=[];city.ground=[];city.fenceHosts=[];city.fences=[];city.plans=[];city.landCache=[];city.heatCallback=null;city.developmentCallback=null;
 city.heat={horizontalSize:width,verticalSize:height,q:Array((width+2)*(height+2)).fill(0),back:Array((width+2)*(height+2)).fill(0),rho:Array((width+2)*(height+2)).fill(0)};
 s.mixed!.city=city;return s;
}
export function addCity(s:Snapshot,d=fixedDefinitions[0],h=10,v=22,stage?:number,ground=0,door=false):CityStructure {
 const m=s.mixed!,id=m.nextEntityId,location=new WorldSpace(s.map.width,s.map.height,s.map.levels).location({h,v,z:2}),footprint=[];
 let nextOrder=BigInt(m.queue!.nextOrder);
 const b:CityStructure={id,revision:'0',definition:d.reference,location,height:d.minimumHeight,owned:stage===undefined,subsidiary:stage===undefined?{marketPrice:d.unitPrice,totalLandValue:'0',callback:null}:null,halves:[],construction:[],caller:'catalogplayer'};
 for(let x=0;x<d.sizeX;x++)for(let y=0;y<d.sizeY;y++)for(let z=0;z<b.height;z++){
  const offset={x,y,z};footprint.push({offset,transparent:false});
  if(stage!==undefined){const state=stage===4&&x===0&&y===0&&z===0?3:stage,callback=state<4?{registration:String(nextOrder),order:String(nextOrder++),dueMinute:String(BigInt(s.tick)+40n),handlerId:`city/construction/${id}/${x}/${y}/${z}`,interval:'0'}:null;
   b.construction.push({offset,cell:{state,ground,needsDoor:door&&!(x===0&&y===d.sizeY-1)},callback});if(callback)m.queue.pending.push(callback);
  }
 }
 m.queue!.nextOrder=String(nextOrder);
 m.queue!.pending.sort((a,b)=>Number(BigInt(a.dueMinute)-BigInt(b.dueMinute))||Number(BigInt(a.order)-BigInt(b.order)));
 s.entities!.push({id,revision:'0',definition:d.reference,anchor:location,footprint,owned:b.owned,silentlyReclaimable:false,value:stage===undefined?d.unitPrice:'0'});
 m.city!.structures.push(b);m.nextEntityId=String(BigInt(id)+1n);
 if(stage===undefined&&d.population)m.city!.producers.push({ownerId:id,initialDefinition:d.reference,initialHeight:b.height});
 return b;
}
