import type { Snapshot } from '../../src/generated/contracts.ts';
import type { Recipe, RenderCatalog } from '../../src/lib/content-recipes.ts';
import { WorldSpace } from '../../src/lib/map-geometry.ts';
import { normalPorts } from '../../src/lib/world-identity.ts';
import { trainSceneFixture } from './train-scene.ts';

// Two consecutive legal 45-degree normal curves make a tight 90-degree bend.
// Literal positions/ports supply the route; no production controller is called.
export function trainTurnScene(source:Snapshot,catalog:RenderCatalog,stock:Recipe,rotation:number,turn:number,reversed:boolean) {
 const s=trainSceneFixture(source,catalog,stock,reversed,32,64),space=new WorldSpace(32,64,6);
 const directions=[[0,-1],[1,-1],[1,0],[1,1],[0,1],[-1,1],[-1,0],[-1,-1]];
 const headings=[0,0,0,0,turn,2*turn,2*turn,2*turn,2*turn,2*turn,2*turn,2*turn,2*turn,2*turn,2*turn,2*turn].map(h=>(h+rotation+8)%8);
 const positions=[space.location({h:15,v:32,z:2})];
 for(const heading of headings){const p=positions.at(-1)!,d=directions[heading];positions.push({x:p.x+d[0],y:p.y+d[1],z:2});}
 const pose=(i:number)=>({position:positions[i],heading:headings[Math.max(0,i-1)],outgoing:headings[i],reversed});
 const voxel=structuredClone(s.mixed!.voxels[0]),entity=structuredClone(s.entities![0]);s.mixed!.voxels=[];s.entities=[];
 for(let i=0;i<headings.length;i++) {
  const p=pose(i),ports=[(p.heading+4)%8,p.outgoing];
  const pattern=Array.from({length:12},(_,i)=>i).find(i=>normalPorts(`normal${i}`)!.every(h=>ports.includes(h)))!;
  const v=structuredClone(voxel),id=String(30+i);v.position=p.position;v.ownerId=id;v.traffic!.car=null;
  v.traffic!.rail!.pattern=pattern;v.traffic!.rail!.definition={...v.traffic!.rail!.definition,contributionId:`system:pattern/${pattern}`,recipeId:`system:pattern/${pattern}`};
  s.mixed!.voxels.push(v);s.entities.push({...structuredClone(entity),id,anchor:p.position});
 }
 s.mixed!.nextEntityId=String(30+headings.length);
 return {space,pose,snapshot(step:number){
  const read=structuredClone(s),train=read.mixed!.fleet!.trains[0];
  for(const [i,c] of train.cars.entries()) {const p=pose(step+1-i);c.state={kind:'inside',location:p.position,heading:p.heading,countdown:null};}
  for(const v of read.mixed!.voxels){const car=train.cars.find(c=>c.state.location!.x===v.position.x&&c.state.location!.y===v.position.y);v.traffic!.car=car?{definition:car.definition,trainId:train.id,carId:car.id}:null;}
  return read;
 }};
}
