import profile from '../generated/terrain-profile.json' with { type: 'json' };
import type { Snapshot, Definition, TerrainOccupant } from '../generated/contracts.ts';
import { WorldSpace, move } from './map-geometry.ts';
import { maximumGameMinute } from './game-time.ts';
export const terrainProfile = profile;
const sameDefinition=(a:Definition,b:Definition)=>a.packId===b.packId&&a.contributionId===b.contributionId&&a.recipeId===b.recipeId;

// Validate the whole proposed graph before exposing any replacement or merged delta.
export function validTerrain(read:Snapshot):boolean {
  const t=read.terrain, {width:w,height:h,levels:z}=read.map;
  if(!t||!read.entities||read.map.coordinateSystem!=='hv'||w<1||w>128||h<1||h>128||z<1||z>64||w*h*z>1048576||
    t.cells.length!==w*h||t.occupants.length>2*w*h||read.entities.length!==t.occupants.length||
    t.waterLevel>=z||t.cells.some(c=>c.ground>=z)||BigInt(t.geometryRevision)>BigInt(read.revision)||
    BigInt(read.tick)>maximumGameMinute||read.cash!=='0'||read.tracks.length||read.trains.length||read.stations.length||read.buildings.length||
    /[\u0000-\u001f\u007f-\u009f]/u.test(t.name))return false;
  let request;
  try { request=JSON.parse(t.provenance.requestJson); } catch { return false; }
  if(request.source!==t.provenance.source||request.Profile!==t.provenance.profile||request.Name!==t.name||
    request.Dimensions?.H!==w||request.Dimensions?.V!==h||request.Dimensions?.Z!==z)return false;
  const space=new WorldSpace(w,h,z), occupants=new Map(t.occupants.map(o=>[o.entityId,o]));
  if(occupants.size!==t.occupants.length||new Set(read.entities.map(e=>e.id)).size!==read.entities.length)return false;
  const at=new Map<string,TerrainOccupant>();
  const key=(p:{x:number;y:number;z:number})=>`${p.x},${p.y},${p.z}`;
  const corners=(o:TerrainOccupant)=>[o.ne,o.se,o.sw,o.nw];
  for(const e of read.entities) {
    const o=occupants.get(e.id),binding=profile.definitions.find(d=>sameDefinition(d.reference,e.definition));
    if(!o||!binding||binding.treeSpriteCount!==0||binding.kind!==o.kind||BigInt(e.id)>=BigInt(t.nextEntityId)||
      !space.contains(e.anchor)||e.footprint.length!==1)return false;
    const s=space.storage(e.anchor),cell=t.cells[s.v*w+s.h],m=e.footprint[0],cs=corners(o);
    const terrace=o.kind==='terrace',flat=cs.every(n=>n===0);
    if(at.has(key(e.anchor))||m.offset.x!==0||m.offset.y!==0||m.offset.z!==0||
      e.anchor.z!==cell.ground-(terrace?1:0)||terrace&&(cell.surface!=='terrace'||e.owned)||
      m.transparent!==(o.kind==='creek'||o.kind==='mountain'&&flat)||e.silentlyReclaimable!==(o.kind==='creek'))return false;
    let value=0;
    if(o.kind==='mountain') {
      if(cs.some(n=>4*cell.ground+n>4*z-1)||o.creekConnections!==0)return false;
      if(o.forest) {
        const f=profile.definitions.find(d=>sameDefinition(d.reference,o.forest!.definition));
        if(!f||f.kind!=='mountain'||f.treeSpriteCount<1||o.forest.trees.length>2*f.treeDensity||o.forest.trees.some(tree=>tree.sprite>=f.treeSpriteCount))return false;
        value=f.price;
      }
    } else if(o.forest||o.kind==='creek'&&!flat||terrace&&o.creekConnections!==0)return false;
    if(e.value!==String(value))return false;
    at.set(key(e.anchor),o);
  }
  const cell=(p:{x:number;y:number;z:number})=>{const s=space.storage(p);return t.cells[s.v*w+s.h];};
  const fine=(p:{x:number;y:number;z:number},corner:number)=>{const c=cell(p),o=at.get(key({...p,z:c.ground}));return 4*c.ground+(o?.kind==='mountain'?corners(o)[corner]:0);};
  for(let v=0;v<h;v++)for(let u=0;u<w;u++) {
    const c=t.cells[v*w+u],p=space.location({h:u,v,z:c.ground});
    if(c.surface==='terrace'&&at.get(key({...p,z:p.z-1}))?.kind!=='terrace')return false;
    if(c.surface!=='natural')continue;
    for(let corner=0;corner<4;corner++) {
      const d=2*corner+1;
      const samples:[[typeof p,number],[typeof p,number],[typeof p,number],[typeof p,number]]=[
        [p,corner],[move(p,d),(corner+2)%4],[move(p,(d+7)%8),(corner+1)%4],[move(p,(d+1)%8),(corner+3)%4]];
      const inside=samples.filter(([p])=>space.containsHorizontal(p));
      if(inside.some(([p])=>cell(p).surface!=='natural'))continue;
      if(new Set(inside.map(([p,i])=>fine(p,i))).size!==1)return false;
    }
  }
  return true;
}
