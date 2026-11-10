import {addRail} from './station-scene.ts';
import {WorldSpace} from '../../src/lib/map-geometry.ts';
import type {Snapshot,StationPlatform,Heading} from '../../src/generated/contracts.ts';
import profile from '../../src/generated/station-profile.json' with {type:'json'};
export function addPassagePlatform(s:Snapshot,direction:Heading,location={x:10,y:10,z:2}) {
 const space=new WorldSpace(s.map.width,s.map.height,s.map.levels),pos=space.storage(location),id=s.mixed!.nextEntityId;
 s.mixed!.nextEntityId=String(BigInt(id)+1n);
 const owner=addRail(s,pos.h,pos.v,location.z),v=s.mixed!.voxels.at(-1)!,pattern=direction%4===0?10:4;
 v.traffic!.rail!.pattern=pattern;v.traffic!.rail!.definition={...v.traffic!.rail!.definition,contributionId:`system:pattern/${pattern}`,recipeId:`system:pattern/${pattern}`};
 v.traffic!.platform={...v.traffic!.rail!.definition,contributionId:`system:thin-${direction}`,recipeId:`system:thin-${direction}`};
 const p:StationPlatform={id,revision:'0',kind:'thin',location,direction,length:1,name:'Passage',roof:false,lane0:true,lane1:false,bell:profile.bells[0],hostId:null,children:[],dayTimer:null,
 yards:[{trafficOwnerId:owner,location,lane:0,index:0,roof:false,outlook:{kind:'passage',material:'concrete',bridge:false,upward:false}}]};
 s.mixed!.stationGraph!.platforms.push(p);return p;
}
