import profile from '../generated/terrain-profile.json' with { type: 'json' };
import type { Snapshot, Definition, TerrainOccupant, SpatialEntity, Delta } from '../generated/contracts.ts';
import { WorldSpace, offset } from './map-geometry.ts';
import { maximumGameMinute } from './game-time.ts';
export const terrainProfile = profile;
const sameDefinition=(a:Definition,b:Definition)=>a.packId===b.packId&&a.contributionId===b.contributionId&&a.recipeId===b.recipeId;

type Page={entities:(SpatialEntity|undefined)[];occupants:(TerrainOccupant|undefined)[]};
// Two slots per column, paged so a bounded edit copies only touched pages.
// Slots are ground and terrace backing; validated terrain has no other occupancy.
type Index={pages:(Page|undefined)[];recipes:Map<string,number>};
function recipesFor(index:Index,e:SpatialEntity,o:TerrainOccupant,d:number){for(const id of [e.definition.recipeId,...(o.forest?[o.forest.definition.recipeId]:[])])index.recipes.set(id,(index.recipes.get(id)??0)+d);}
const indexes=new WeakMap<object,Index>();
// Geometry certificates carry only a weak predecessor link. Clock publications
// share the same index; abandoned edits never retain a world-history chain.
const edits=new WeakMap<Index,{previous:WeakRef<Index>;columns:readonly number[];global:boolean}>();
const get=(index:Index,i:number)=>{const p=index.pages[i>>>9],j=i&511;return {e:p?.entities[j],o:p?.occupants[j]};};
function put(index:Index,i:number,e:SpatialEntity|undefined,o:TerrainOccupant|undefined,copied?:Set<number>) {
 const n=i>>>9,j=i&511;
 if(copied&&!copied.has(n)){const old=index.pages[n];index.pages[n]={entities:old?.entities.slice()??[],occupants:old?.occupants.slice()??[]};copied.add(n);}
 const page=index.pages[n]??=( {entities:[],occupants:[]} );page.entities[j]=e;page.occupants[j]=o;
}
const orders=new WeakMap<object,boolean>();
export function terrainById<T>(values:readonly T[],id:string,key:(value:T)=>string):T|undefined {
 let sorted=orders.get(values);if(sorted===undefined){sorted=values.every((v,i)=>{if(!i)return true;const a=key(values[i-1]),b=key(v);return a.length<b.length||a.length===b.length&&a<b;});orders.set(values,sorted);}
 if(!sorted)return values.find(v=>key(v)===id);
 let lo=0,hi=values.length;
 while(lo<hi){const m=(lo+hi)>>>1,k=key(values[m]);if(k.length<id.length||k.length===id.length&&k<id)lo=m+1;else hi=m;}
 return lo<values.length&&key(values[lo])===id?values[lo]:undefined;
}
// Validate the whole proposed graph before exposing any replacement or merged delta.
export function validTerrain(read:Snapshot,tunnelCorners:ReadonlyMap<number,number>=new Map()):boolean {
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
  const graph:Index={pages:[],recipes:new Map()};
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
    put(graph,2*index+(terrace?1:0),e,o);recipesFor(graph,e,o,1);
  }
  // Canonical tunnel backups certify the removed natural boundary; they do
  // not populate terrain occupants or affect visible terrain/picking.
  for(const [i,corners] of tunnelCorners){if(i<0||i>=w*h||get(graph,2*i).e)return false;quarters[i]=corners;}
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
  indexes.set(read,graph);return true;
}

// Incremental validation is available only from an already validated immutable
// snapshot. Untrusted replacements still take the full validator above.
export function validTerrainDelta(before:Snapshot,read:Snapshot,delta:Delta):boolean {
 const original=indexes.get(before);if(!original||!Object.isFrozen(before))return validTerrain(read);
 const t=read.terrain!,space=new WorldSpace(read.map.width,read.map.height,read.map.levels),w=read.map.width,h=read.map.height,z=read.map.levels;
 if(t.occupants.length!==read.entities!.length||t.occupants.length>2*w*h||t.waterLevel<0||t.waterLevel>=z||
   BigInt(t.geometryRevision)>BigInt(read.revision)||BigInt(read.tick)<0n||BigInt(read.tick)>maximumGameMinute||read.cash!=='0'||
   read.trains.length||read.tracks.length||read.buildings.length||read.stations.length)return false;
 const graph:Index={pages:original.pages.slice(),recipes:new Map(original.recipes)},copied=new Set<number>(),columns=new Set<number>();
 const column=(e:SpatialEntity)=>{const s=space.storage(e.anchor);return s.v*w+s.h;};
 const changedIds=new Set([...delta.entities!.map(e=>e.id),...delta.terrain!.occupants.map(o=>o.entityId),...delta.removed.entityIds!]);
 for(const id of changedIds){
  const e=terrainById(before.entities!,id,e=>e.id),o=terrainById(before.terrain!.occupants,id,o=>o.entityId);
  if(e&&o){recipesFor(graph,e,o,-1);const i=column(e);columns.add(i);put(graph,2*i+(o.kind==='terrace'?1:0),undefined,undefined,copied);}
 }
 for(const id of changedIds){
  const e=terrainById(read.entities!,id,e=>e.id),o=terrainById(t.occupants,id,o=>o.entityId);
  if(!e&&!o)continue;if(!e||!o||!space.contains(e.anchor))return false;
  const i=column(e),s=2*i+(o.kind==='terrace'?1:0);if(get(graph,s).e)return false;
  columns.add(i);put(graph,s,e,o,copied);recipesFor(graph,e,o,1);
 }
 for(const c of delta.terrain!.cells){if(c.ground<0||c.ground>=z)return false;columns.add(c.index);}
 const resolve=(d:Definition)=>profile.definitions.find(b=>sameDefinition(b.reference,d));
 const validateColumn=(i:number)=>{
  const c=t.cells[i],normal=get(graph,2*i),terrace=get(graph,2*i+1);
  if(c.surface==='terrace'&&!terrace.e)return false;
  for(const {e,o} of [normal,terrace]){
   if(!e&&!o)continue;if(!e||!o)return false;
   const binding=resolve(e.definition),isTerrace=o.kind==='terrace',flat=(o.ne|o.se|o.sw|o.nw)===0,m=e.footprint[0];
   if(!binding||binding.treeSpriteCount!==0||binding.kind!==o.kind||BigInt(e.id)>=BigInt(t.nextEntityId)||
    e.footprint.length!==1||m.offset.x!==0||m.offset.y!==0||m.offset.z!==0||e.anchor.z!==c.ground-(isTerrace?1:0)||
    isTerrace&&(c.surface!=='terrace'||e.owned)||m.transparent!==(o.kind==='creek'||o.kind==='mountain'&&flat)||
    e.silentlyReclaimable!==(o.kind==='creek'))return false;
   let value=0;
   if(o.kind==='mountain'){
    const max=4*z-1-4*c.ground;if(o.ne>max||o.se>max||o.sw>max||o.nw>max||o.creekConnections!==0)return false;
    if(o.forest){const f=resolve(o.forest.definition);if(!f||f.kind!=='mountain'||f.treeSpriteCount<1||o.forest.trees.length>2*f.treeDensity||o.forest.trees.some(t=>t.sprite>=f.treeSpriteCount))return false;value=f.price;}
   }else if(o.forest||o.kind==='creek'&&!flat||isTerrace&&o.creekConnections!==0)return false;
   if(e.value!==String(value))return false;
  }return true;
 };
 const neighborhoods=new Set(columns),offsets=Array.from({length:8},(_,d)=>offset(d));
 const neighbor=(i:number,d:number)=>{const v=Math.floor(i/w),u=i%w,delta=offsets[d],vv=v+delta.y-delta.x,hh=Math.floor((2*u+(v%2)+delta.x+delta.y)/2);return vv<0||vv>=h||hh<0||hh>=w?undefined:vv*w+hh;};
 for(const i of columns){if(!validateColumn(i))return false;for(let d=0;d<8;d++){const n=neighbor(i,d);if(n!==undefined)neighborhoods.add(n);}}
 const fine=(i:number,c:number)=>{const o=get(graph,2*i).o;return 4*t.cells[i].ground+(o?.kind==='mountain'?[o.ne,o.se,o.sw,o.nw][c]:0);};
 for(const i of neighborhoods){
  if(t.cells[i].surface!=='natural')continue;
  for(let c=0;c<4;c++){
   let mismatch=false,discontinuity=false;const d=2*c+1,expected=fine(i,c);
   for(let sample=0;sample<3;sample++){
    const n=neighbor(i,sample===0?d:sample===1?(d+7)%8:(d+1)%8);if(n===undefined)continue;
    if(t.cells[n].surface!=='natural'){discontinuity=true;break;}
    if(fine(n,(c+(sample===0?2:sample===1?1:3))%4)!==expected)mismatch=true;
   }if(!discontinuity&&mismatch)return false;
  }
 }
 indexes.set(read,graph);
 edits.set(graph,{previous:new WeakRef(original),columns:[...neighborhoods],global:t.waterLevel!==before.terrain!.waterLevel});
 return true;
}
// Only immutable, validated consecutive geometry can certify a local redraw.
// Replacements, lost predecessors and global water changes use the full path.
export function terrainChangedColumns(before:Snapshot,after:Snapshot):readonly number[]|undefined {
 if(!Object.isFrozen(before)||!Object.isFrozen(after))return;
 const base=indexes.get(before);let current=indexes.get(after);if(!base||!current)return;
 const columns=new Set<number>();
 for(let n=0;n<64;n++){
  if(current===base)return [...columns];
  const edit=edits.get(current);if(!edit||edit.global)return;
  for(const i of edit.columns)columns.add(i);
  current=edit.previous.deref();if(!current)return;
 }
}
export function shareTerrainValidation(before:object,after:object){const index=indexes.get(before);if(index)indexes.set(after,index);}

export function isValidatedTerrain(read:object){return Object.isFrozen(read)&&indexes.has(read);}

export function terrainLookup(read:Snapshot){
 const graph=indexes.get(read)!;
 return {
  occupant(key:number){if(key<0)return;const i=Math.floor(key/read.map.levels),z=key%read.map.levels;
   const normal=get(graph,2*i),terrace=get(graph,2*i+1);return normal.e?.anchor.z===z?normal.o:terrace.e?.anchor.z===z?terrace.o:undefined;},
  entity(id:string){return terrainById(read.entities!,id,e=>e.id);},
  recipes(){return new Set([...graph.recipes].filter(([,count])=>count>0).map(([id])=>id));}
 };
}
