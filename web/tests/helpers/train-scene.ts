import type {Snapshot,Definition,RailwayVoxel} from '../../src/generated/contracts.ts';
import {composeTrain,type RenderCatalog,type Recipe} from '../../src/lib/content-recipes.ts';
import {WorldSpace} from '../../src/lib/map-geometry.ts';

// Explicit art fixtures; gameplay movement is tested separately.
export function trainSceneFixture(source:Snapshot,catalog:RenderCatalog,stock:Recipe,reversed=false,width=16,height=32):Snapshot {
 const definition=(id:string):Definition=>({packId:catalog.packId,contributionId:id,recipeId:id});
 const s=structuredClone(source),space=new WorldSpace(width,height,6);s.map={width,height,levels:6,coordinateSystem:'hv'};s.entities=[];
 s.terrain={...s.terrain!,waterLevel:0,occupants:[],cells:Array.from({length:width*height},()=>({ground:2,surface:'cut'}))};
 const r=JSON.parse(s.terrain.provenance.requestJson);r.Dimensions={H:width,V:height,Z:6};s.terrain.provenance.requestJson=JSON.stringify(r);
 const train=s.mixed!.fleet!.trains[0],template=structuredClone(train.cars[0]);
 train.definition={...train.definition,contributionId:stock.id};train.isReversed=reversed;train.passengers=0;train.boardingOrigin=null;train.controllerId='12';train.moveCount=0;
 train.cars=composeTrain(catalog,stock.id,3).map((id,i)=>({...structuredClone(template),id:String(18+i),originalIndex:i,definition:definition(id),
  state:{kind:'inside' as const,location:{...space.location({h:6,v:14,z:2}),y:13+i},heading:0,countdown:null}})) as typeof train.cars;
 // Use a straight N/S chain in canonical coordinates, independent of H/V parity.
 for(const c of train.cars)c.state.location!.x=space.location({h:6,v:14,z:2}).x;
 if(reversed){train.cars.reverse();for(const c of train.cars)c.state.heading=4;}
 const m=s.mixed!;m.fleet!.trains=[train];m.fleet!.groups[0].members=['16'];m.fleet!.groups[1].members=['17'];m.nextEntityId='30';
 m.queue!.pending=m.queue!.pending.slice(0,1);m.queue!.pending[0].handlerId='train/17';
 m.voxels=train.cars.map((c,i)=>{
  const p=c.state.location!,id=String(i+1),d=definition('system:traffic');d.recipeId='geometry:traffic-components-1';
  s.entities!.push({id,revision:'0',definition:d,anchor:p,footprint:[{offset:{x:0,y:0,z:0},transparent:true}],owned:true,silentlyReclaimable:false,value:'0'});
  return {position:p,ownerId:id,kind:'traffic',traffic:{rail:{definition:definition('system:pattern/10'),pattern:10,variant:0,direction:0,terrainBackup:null,controllerId:null},car:{definition:c.definition,trainId:train.id,carId:c.id},road:null,platform:null,accessory:null},definition:null,sprite:0,drawGroundBelow:true,drawGroundAbove:true,supportedOwnerId:null} as RailwayVoxel;
 });m.slopes=[];return s;
}
