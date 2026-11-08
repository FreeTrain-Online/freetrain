import type {Snapshot,Definition} from '../../src/generated/contracts.ts';
import source from '../../../prototype/protocol/fixtures/snapshot-mixed.json' with {type:'json'};
import definitions from '../../src/generated/station-profile.json' with {type:'json'};
import {WorldSpace} from '../../src/lib/map-geometry.ts';
export const stationDefinitions=definitions.definitions;
export function stationScene(width=24,height=40):Snapshot {
  const s=structuredClone(source) as Snapshot;
  s.map={width,height,levels:8,coordinateSystem:'hv'};s.entities=[];s.tracks=[];s.stations=[];s.trains=[];s.buildings=[];
  s.terrain={...s.terrain!,geometryRevision:'0',nextEntityId:'1',waterLevel:0,occupants:[],cells:Array.from({length:width*height},()=>({ground:2,surface:'cut'}))};
  const r=JSON.parse(s.terrain.provenance.requestJson);r.Dimensions={H:width,V:height,Z:8};s.terrain.provenance.requestJson=JSON.stringify(r);
  s.mixed={geometryRevision:'0',nextEntityId:'1',voxels:[],slopes:[],accounts:{initialCash:s.cash,bankruptcyIncrement:'10000000000',transactions:[]},
    stationGraph:{nextStationName:1,nextPlatformName:1,buildings:[],platforms:[],passages:[],listeners:[],listenerMemberships:[]},queue:{nextOrder:'0',pending:[]}};
  return s;
}
export function addStation(s:Snapshot,d=stationDefinitions[0],h=6,v=14) {
  const location=new WorldSpace(s.map.width,s.map.height,s.map.levels).location({h,v,z:2}),m=s.mixed!,id=m.nextEntityId,footprint=[];
  for(let x=0;x<d.sizeX;x++)for(let y=0;y<d.sizeY;y++)for(let z=0;z<d.height;z++)footprint.push({offset:{x,y,z},transparent:false});
  s.entities!.push({id,revision:'0',definition:d.reference,anchor:location,footprint,owned:true,silentlyReclaimable:false,value:'0'});
  const log=()=>({today:0,yesterday:0,thisWeek:0,lastWeekPerDay:0});
  const b={id,revision:'0',definition:d.reference,location,name:'中央駅 / Central',children:[],passengers:{gone:0,accumulatedLoaded:0,accumulatedUnloaded:0},imports:log(),exports:log(),trains:log(),hourTimer:null,dayTimer:null};
  m.stationGraph!.buildings.push(b);m.stationGraph!.nextStationName++;m.nextEntityId=String(BigInt(id)+1n);return b;
}
export function addRail(s:Snapshot,h:number,v:number,z=2) {
  const p=new WorldSpace(s.map.width,s.map.height,s.map.levels).location({h,v,z}),m=s.mixed!,id=m.nextEntityId;
  const definition=(id:string):Definition=>({packId:s.requiredPacks[0].id,contributionId:id,recipeId:id});
  s.entities!.push({id,revision:'0',definition:{...definition('system:traffic'),recipeId:'geometry:traffic-components-1'},anchor:p,footprint:[{offset:{x:0,y:0,z:0},transparent:true}],owned:true,silentlyReclaimable:false,value:'0'});
  m.voxels.push({position:p,ownerId:id,kind:'traffic',definition:null,sprite:0,drawGroundAbove:true,drawGroundBelow:true,supportedOwnerId:null,
    traffic:{rail:{definition:definition('system:pattern/10'),pattern:10,variant:0,direction:0,controllerId:null,terrainBackup:null},car:null,road:null,accessory:null,platform:null}});
  m.nextEntityId=String(BigInt(id)+1n);return id;
}
