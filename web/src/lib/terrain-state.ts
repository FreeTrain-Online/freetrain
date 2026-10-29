import profile from '../generated/terrain-profile.json' with { type: 'json' };
import type { Snapshot, Definition, TerrainOccupant } from '../generated/contracts.ts';
import { WorldSpace, offset } from './map-geometry.ts';
import { maximumGameMinute } from './game-time.ts';
export const terrainProfile = profile;
const sameDefinition=(a:Definition,b:Definition)=>a.packId===b.packId&&a.contributionId===b.contributionId&&a.recipeId===b.recipeId;

// Validate the whole proposed graph before exposing any replacement or merged delta.
export function validTerrain(read:Snapshot):boolean {
  const t=read.terrain, {width:w,height:h,levels:z}=read.map;
  if(!t||!read.entities||read.map.coordinateSystem!=='hv'||w<1||w>2048||h<1||h>2048||z<1||z>64||w*h>1700000||w*h*z>108800000||
    t.cells.length!==w*h||t.occupants.length>2*w*h||read.entities.length!==t.occupants.length||
    t.waterLevel>=z||t.cells.some(c=>c.ground>=z)||BigInt(t.geometryRevision)>BigInt(read.revision)||
    BigInt(read.tick)>maximumGameMinute||read.cash!=='0'||read.tracks.length||read.trains.length||read.stations.length||read.buildings.length||
    /[\u0000-\u001f\u007f-\u009f]/u.test(t.name))return false;
  let request;
  try { request=JSON.parse(t.provenance.requestJson); } catch { return false; }
  if(request.source!==t.provenance.source||request.Profile!==t.provenance.profile||request.Name!==t.name||
    request.Dimensions?.H!==w||request.Dimensions?.V!==h||request.Dimensions?.Z!==z)return false;
  const space=new WorldSpace(w,h,z),occupants=new Map<string,TerrainOccupant>();
  for(const o of t.occupants){if(occupants.has(o.entityId))return false;occupants.set(o.entityId,o);}
  const occupied=new Set<number>(),quarters=new Uint16Array(w*h),terraces=new Uint8Array(w*h);
  const bindings=new WeakMap<Definition,(typeof profile.definitions)[number]>();
  const resolve=(d:Definition)=>{let b=bindings.get(d);if(!b){b=profile.definitions.find(b=>sameDefinition(b.reference,d));if(b)bindings.set(d,b);}return b;};
  const nextId=BigInt(t.nextEntityId);
  for(const e of read.entities) {
    const o=occupants.get(e.id),binding=resolve(e.definition);
    if(!o||!binding||binding.treeSpriteCount!==0||binding.kind!==o.kind||BigInt(e.id)>=nextId||
      !space.contains(e.anchor)||e.footprint.length!==1)return false;
    occupants.delete(e.id); // Duplicate entity IDs cannot reuse a payload.
    const s=space.storage(e.anchor),index=s.v*w+s.h,cell=t.cells[index],m=e.footprint[0],key=index*z+e.anchor.z;
    const terrace=o.kind==='terrace',flat=(o.ne|o.se|o.sw|o.nw)===0;
    if(occupied.has(key)||m.offset.x!==0||m.offset.y!==0||m.offset.z!==0||
      e.anchor.z!==cell.ground-(terrace?1:0)||terrace&&(cell.surface!=='terrace'||e.owned)||
      m.transparent!==(o.kind==='creek'||o.kind==='mountain'&&flat)||e.silentlyReclaimable!==(o.kind==='creek'))return false;
    occupied.add(key);
    let value=0;
    if(o.kind==='mountain') {
      const maximum=4*z-1-4*cell.ground;
      if(o.ne>maximum||o.se>maximum||o.sw>maximum||o.nw>maximum||o.creekConnections!==0)return false;
      quarters[index]=o.ne<<12|o.se<<8|o.sw<<4|o.nw;
      if(o.forest) {
        const f=resolve(o.forest.definition);
        if(!f||f.kind!=='mountain'||f.treeSpriteCount<1||o.forest.trees.length>2*f.treeDensity||o.forest.trees.some(tree=>tree.sprite>=f.treeSpriteCount))return false;
        value=f.price;
      }
    } else if(o.forest||o.kind==='creek'&&!flat||terrace&&o.creekConnections!==0)return false;
    if(e.value!==String(value))return false;
    if(terrace)terraces[index]=1;
  }
  const offsets=Array.from({length:8},(_,d)=>offset(d));
  const fine=(i:number,corner:number)=>4*t.cells[i].ground+((quarters[i]>>(12-4*corner))&15);
  for(let v=0;v<h;v++)for(let u=0;u<w;u++) {
    const index=v*w+u,c=t.cells[index];
    if(c.surface==='terrace'&&!terraces[index])return false;
    if(c.surface!=='natural')continue;
    for(let corner=0;corner<4;corner++) {
      const expected=fine(index,corner),d=2*corner+1;
      let mismatch=false,discontinuity=false;
      for(let sample=0;sample<3;sample++) {
        const direction=sample===0?d:sample===1?(d+7)%8:(d+1)%8;
        const delta=offsets[direction],vv=v+delta.y-delta.x,hh=Math.floor((2*u+(v%2)+delta.x+delta.y)/2);
        if(vv<0||vv>=h||hh<0||hh>=w)continue;
        const i=vv*w+hh,otherCorner=(corner+(sample===0?2:sample===1?1:3))%4;
        if(t.cells[i].surface!=='natural'){discontinuity=true;break;}
        if(fine(i,otherCorner)!==expected)mismatch=true;
      }
      if(!discontinuity&&mismatch)return false;
    }
  }
  return true;
}
