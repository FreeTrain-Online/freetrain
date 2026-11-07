import type {Snapshot,TrainMotionPose} from '../../src/generated/contracts.ts';
import type {RenderCatalog,Recipe} from '../../src/lib/content-recipes.ts';
import {WorldSpace} from '../../src/lib/map-geometry.ts';
import {normalPorts} from '../../src/lib/world-identity.ts';
import {trainSceneFixture} from './train-scene.ts';

/** Independent four-quarter ramp, with flat approach/exit rails. */
export function trainSlopeScene(source:Snapshot,catalog:RenderCatalog,stock:Recipe,climb:0|2|4|6,downhill=false,reversed=false,underground=false) {
 const space=new WorldSpace(32,48,6),anchor=space.location({h:15,v:24,z:2});
 const [dx,dy]=[[0,-1],[1,0],[0,1],[-1,0]][climb/2],heading=downhill?(climb+4)%8:climb;
 const at=(k:number)=>({x:anchor.x+k*dx,y:anchor.y+k*dy,z:k<2?2:3});
 const pose=(step:number):TrainMotionPose=>{
  const k=downhill?4-step:step-1;
  return {position:at(k),heading,outgoing:heading,reversed,...(k>=0&&k<4?{slope:{climb,level:k}}:{})};
 };
 const snapshot=(step:number)=>{
  const s=trainSceneFixture(source,catalog,stock,reversed,32,48),train=s.mixed!.fleet!.trains[0];
  const template=structuredClone(s.mixed!.voxels[0]),entity=structuredClone(s.entities![0]);
  const definition=(id:string,recipeId=id)=>({packId:catalog.packId,contributionId:id,recipeId});
  for(const [i,c] of train.cars.entries())c.state={kind:'inside',location:pose(step-i).position,heading,countdown:null};
  s.terrain!.cells.forEach(c=>c.ground=underground?4:2);s.entities=[];s.mixed!.voxels=[];
  const flat=Array.from({length:12},(_,i)=>i).find(i=>normalPorts(`normal${i}`)!.includes(climb)&&normalPorts(`normal${i}`)!.includes((climb+4)%8))!;
  for(let k=-4;k<=7;k++) {
   const ramp=k>=0&&k<4,v=structuredClone(template),id=ramp?'42':String(k+34),pattern=ramp?(underground?44:28)+climb*2+k:flat;
   v.ownerId=id;v.position=at(k);v.traffic!.rail!.pattern=pattern;
   v.traffic!.rail!.definition={...v.traffic!.rail!.definition,contributionId:`system:pattern/${pattern}`,recipeId:`system:pattern/${pattern}`};
   const car=train.cars.find(c=>JSON.stringify(c.state.location)===JSON.stringify(v.position));
   v.traffic!.car=car?{definition:car.definition,trainId:train.id,carId:car.id}:null;
   s.mixed!.voxels.push(v);
   if(ramp) {
    const kind=k<2?'slopefiller':underground?'slopewall':'slopesupport';
    s.mixed!.voxels.push({...structuredClone(v),position:{...v.position,z:k<2?3:2},kind,traffic:null,
     definition:k<2?null:definition(`system:pattern/${underground?'underground-':''}slope-support-${climb*2+k}`),
     sprite:k<2?0:climb+k-2,drawGroundBelow:!(k<2&&underground),drawGroundAbove:!(k>=2&&underground)});
   }else s.entities.push({...structuredClone(entity),id,anchor:v.position});
  }
  const members=s.mixed!.voxels.filter(v=>v.ownerId==='42');
  s.entities.push({...structuredClone(entity),id:'42',definition:definition('system:slope','geometry:eight-cell-slope-1'),anchor,
   footprint:members.map(v=>({offset:{x:v.position.x-anchor.x,y:v.position.y-anchor.y,z:v.position.z-anchor.z},transparent:true}))});
  s.mixed!.slopes=[{entityId:'42',climb}];s.mixed!.nextEntityId='43';
  return s;
 };
 return {space,pose,snapshot};
}
