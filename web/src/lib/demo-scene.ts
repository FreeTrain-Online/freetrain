import type { Snapshot, TerrainOccupant, Position, Definition } from '../generated/contracts.ts';
import type { ReadState } from './session-client.ts';
import { demoDimensions, demoSpace as space, demoGroundGrid } from './demo-terrain.ts';
import { terrainProfile } from './terrain-state.ts';
import { demoCorridor } from './demo-railway-path.ts';
import type { Camera } from './map-display.ts';

// A fixed presentation landscape, never a session candidate or a gameplay
// generator. Geometry and realized trees use the same model as loaded worlds.
const {width,height,levels,waterLevel}=demoDimensions;
const name='Coastal Highlands';
const requestJson=JSON.stringify({source:'empty',Profile:'terrain-generation-1',Name:name,Dimensions:{H:width,V:height,Z:levels},WaterLevel:waterLevel});
const hash=(x:number,y:number,salt=0)=>{
  let n=Math.imul(x+97,374761393)^Math.imul(y+193,668265263)^Math.imul(salt+1,1274126177);
  n=Math.imul(n^(n>>>13),1274126177);return (n^(n>>>16))>>>0;
};
function landscape():Snapshot {
  const cells:NonNullable<Snapshot['terrain']>['cells']=[],occupants:TerrainOccupant[]=[],entities:NonNullable<Snapshot['entities']>=[];
  const corners:number[][]=[];
  let nextId=1;
  const mountain=terrainProfile.definitions.find(d=>d.kind==='mountain'&&d.treeSpriteCount===0)!.reference;
  const terrace=terrainProfile.definitions.find(d=>d.kind==='terrace')!.reference;
  const forests=terrainProfile.definitions.filter(d=>d.treeSpriteCount>0&&!/-[mg]$/.test(d.reference.recipeId));
  const add=(position:Position,definition:Definition,kind:TerrainOccupant['kind'],cs:number[],forest:TerrainOccupant['forest']=null,connections=0)=>{
    const id=String(nextId++),[ne,se,sw,nw]=cs;
    entities.push({id,revision:'0',definition:{...definition},anchor:position,footprint:[{offset:{x:0,y:0,z:0},transparent:kind==='creek'||kind==='mountain'&&cs.every(n=>n===0)}],owned:false,silentlyReclaimable:kind==='creek',value:forest?'100':'0'});
    occupants.push({entityId:id,kind,ne,se,sw,nw,forest,creekConnections:connections});
  };
  const trackBed=demoCorridor(1),clearance=demoCorridor(3);
  const ground=demoGroundGrid(trackBed);
  for(const sample of ground) {
    cells.push({ground:sample.ground,surface:'natural'});corners.push(sample.corners);
  }
  const terraced=new Set<number>();
  // Hand-authored level garden: backing terraces preserve their original corners.
  for(let v=52;v<=56;v++)for(let h=19;h<=21;h++) {
    const index=v*width+h,cell=cells[index];
    if(clearance.has(index)||cell.ground<waterLevel||corners[index].some(n=>n!==0))continue;
    add(space.location({h,v,z:cell.ground}),terrace,'terrace',corners[index]);
    cell.ground++;cell.surface='terrace';corners[index]=[0,0,0,0];terraced.add(index);
  }
  for(let v=0;v<height;v++)for(let h=0;h<width;h++) {
    const index=v*width+h,cell=cells[index],cs=corners[index],n=hash(h,v);
    const grove=Math.sin(h*.31+v*.12)+Math.cos(v*.23-h*.09);
    const forest=cell.ground>waterLevel&&!clearance.has(index)&&!terraced.has(index)&&grove>-.1&&n%5<3;
    if(!forest&&cs.every(n=>n===0))continue;
    let decoration:TerrainOccupant['forest']=null;
    if(forest) {
      const definition=forests[Math.floor(h/8+v/15)%forests.length];
      const count=1+n%Math.min(4,2*definition.treeDensity);
      // Store the realized source-projected positions and sprite choices once.
      const trees=Array.from({length:count},(_,i)=>{
        const r=hash(h,v,i+1),x=r%16,y=(r>>>4)%16;
        return {x:x+y,y:((-x+y)>>1)+8,sprite:(r>>>8)%definition.treeSpriteCount};
      }).sort((a,b)=>a.y-b.y||a.x-b.x);
      decoration={definition:{...definition.reference},trees:trees as NonNullable<TerrainOccupant['forest']>['trees']};
    }
    add(space.location({h,v,z:cell.ground}),mountain,'mountain',cs,decoration);
  }
  return {
    type:'snapshot',worldEpoch:'local-demo-only',revision:'0',tick:'130080',paused:true,cash:'0',
    map:{width,height,levels,coordinateSystem:'hv'},trains:[],stations:[],buildings:[],tracks:[],
    ruleset:terrainProfile.ruleset,contentVersion:terrainProfile.contentVersion,requiredPacks:terrainProfile.requiredPacks.map(p=>({...p})),entities,
    terrain:{name,waterLevel,constructionMode:false,geometryRevision:'0',nextEntityId:String(nextId),cells,occupants,
      provenance:{profile:'terrain-generation-1',source:'empty',requestJson,requestHash:'7ebc13b98d689fcc4821b8f3a862b393d8f2afba44cd5cd13ff499eb94e0b522'}},
  };
}
function freeze<T extends object>(value:T):T {
  Object.values(value).forEach(child=>{if(child&&typeof child==='object')freeze(child);});return Object.freeze(value);
}
export const demoScene:ReadState=freeze(landscape());
/** Cover the viewport with the landscape, keeping the coast and highlands in
 * frame across desktop and portrait layouts. Independent of the live camera. */
export function demoCamera(width:number,height:number):Camera {
  const center=space.center(space.location({h:23,v:48,z:waterLevel}));
  // Desktop framing shows the complete island and coastal circuit. Portrait
  // retains a closer view; the local backdrop extends the surrounding sea.
  const zoom=width>=800?Math.min(width/(48*32+64),height/(96*8-32)):Math.max(width/(48*32-32),height/(96*8-32));
  return {...center,zoom};
}
