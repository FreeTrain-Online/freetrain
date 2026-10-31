import Ajv from 'ajv';
import schema from '../../../prototype/protocol/schema.json' with {type:'json'};
import type { Definition, Snapshot, SpatialEntity, Terrain, TerrainTree, TerrainOccupant, StateChunk, WireMessage } from '../generated/contracts.ts';
import { parseMessageValue, validWireShape } from './protocol.ts';
import { terrainProfile } from './terrain-state.ts';

export const stateChunkBytes=48*1024,maximumStateBytes=256*1024*1024;
export interface PackedTerrain extends Omit<Terrain,'cells'|'occupants'> {
  encoding:'terrain-packed-1';definitions:Definition[];cells:string;occupantCount:number;occupants:string;
}
const validatePacked=new Ajv({strict:true,validateFormats:false}).compile({$defs:schema.$defs,$ref:'#/$defs/PackedTerrain'});
function bytes(text:string,maximum:number):Uint8Array {
  if(text.length>4*Math.ceil(maximum/3))throw new Error('Packed data limit');
  const raw=atob(text);if(raw.length>maximum||btoa(raw)!==text)throw new Error('Invalid base64');
  const result=new Uint8Array(raw.length);for(let i=0;i<raw.length;i++)result[i]=raw.charCodeAt(i);return result;
}
function boundedJson(text:string):unknown {
  // Packed worlds have tiny JSON structure around bounded base64 blobs. Scan
  // container counts/depth before JSON.parse can allocate an expanded graph.
  let containers=0,depth=0;
  for(let i=0;i<text.length;i++) {
    const c=text.charCodeAt(i);
    if(c===34) {
      let end=i;
      do {
        end=text.indexOf('"',end+1);if(end<0)throw new Error('State string');
        let slashes=0;for(let j=end-1;j>i&&text.charCodeAt(j)===92;j--)slashes++;
        if(slashes%2===0)break;
      }while(true);
      i=end;
    }else if(c===123||c===91){if(++containers>1000000||++depth>32)throw new Error('State JSON structure');}
    else if(c===125||c===93)depth--;
  }
  return JSON.parse(text);
}
export function unpackTerrain(p:PackedTerrain,map:Snapshot['map']):{terrain:Terrain;entities:SpatialEntity[]} {
  const count=map.width*map.height;
  if(!Number.isSafeInteger(count)||map.width<1||map.width>2048||map.height<1||map.height>2048||map.levels<1||map.levels>64||
    count>1700000||count*map.levels>108800000||p.occupantCount>2*count||p.definitions.length>terrainProfile.definitions.length||p.encoding!=='terrain-packed-1')throw new Error('Packed terrain dimensions');
  for(const d of p.definitions)if(!terrainProfile.definitions.some(b=>b.reference.packId===d.packId&&b.reference.contributionId===d.contributionId&&b.reference.recipeId===d.recipeId))throw new Error('Packed definition');
  const cells=bytes(p.cells,count),rows=bytes(p.occupants,p.occupantCount*61);
  if(cells.length!==count||rows.length<p.occupantCount*43)throw new Error('Packed terrain lengths');
  if(count*24+p.occupantCount*1024+rows.length*2>2048*1024*1024)throw new Error('Packed expansion limit');
  const values:Terrain['cells']=[],grid:Terrain['cells']=[];
  for(const n of cells) {
    if(n>=192||(n&63)>=map.levels)throw new Error('Packed cell');
    grid.push(values[n]??=( {ground:n&63,surface:(['natural','cut','terrace'] as const)[n>>6]}));
  }
  const entities:SpatialEntity[]=[],occupants:TerrainOccupant[]=[],view=new DataView(rows.buffer,rows.byteOffset,rows.byteLength);
  const footprints=[Object.freeze([{offset:Object.freeze({x:0,y:0,z:0}),transparent:false}]),Object.freeze([{offset:Object.freeze({x:0,y:0,z:0}),transparent:true}])];
  let offset=0,lastId=0n;
  for(let i=0;i<p.occupantCount;i++) {
    if(offset+43>rows.length)throw new Error('Truncated packed row');
    const id=view.getBigInt64(offset,true),revision=view.getBigInt64(offset+8,true),d=view.getUint16(offset+16,true),flags=rows[offset+27],kind=rows[offset+36],corners=view.getUint16(offset+37,true),f=view.getUint16(offset+40,true),trees=rows[offset+42];
    if(id<=lastId||revision<0n||d>=p.definitions.length||flags>7||kind>2||trees>6||f===65535&&trees!==0||f!==65535&&f>=p.definitions.length||offset+43+3*trees>rows.length)throw new Error('Invalid packed row');
    lastId=id;
    entities.push({id:String(id),revision:String(revision),definition:p.definitions[d],anchor:{x:view.getInt32(offset+18,true),y:view.getInt32(offset+22,true),z:rows[offset+26]},
      footprint:footprints[(flags>>2)&1] as unknown as SpatialEntity['footprint'],owned:!!(flags&1),silentlyReclaimable:!!(flags&2),value:String(view.getBigInt64(offset+28,true))});
    const o:TerrainOccupant={entityId:String(id),kind:(['mountain','terrace','creek'] as const)[kind],ne:corners>>12,se:(corners>>8)&15,sw:(corners>>4)&15,nw:corners&15,creekConnections:rows[offset+39],forest:null};
    offset+=43;
    if(f!==65535) {
      const realized:TerrainTree[]=[];
      for(let t=0;t<trees;t++)realized.push({x:rows[offset++],y:rows[offset++],sprite:rows[offset++]});
      o.forest={definition:p.definitions[f],trees:realized as NonNullable<TerrainOccupant['forest']>['trees']};
    }
    occupants.push(o);
  }
  if(offset!==rows.length)throw new Error('Trailing packed bytes');
  return {entities,terrain:{name:p.name,waterLevel:p.waterLevel,constructionMode:p.constructionMode,geometryRevision:p.geometryRevision,nextEntityId:p.nextEntityId,provenance:p.provenance,cells:grid,occupants}};
}

// One bounded assembly per socket, with no partial publication. A connection reset
// discards this object. Duplicate/conflicting/interleaved transfers fail closed.
export class StateAssembler {
  private active?:{header:StateChunk;data:Uint8Array<ArrayBuffer>;seen:Uint8Array;received:number;expires:number};
  get pending():boolean{return !!this.active;}
  async accept(chunk:StateChunk,now=Date.now()):Promise<WireMessage|undefined> {
    if(![chunk.totalBytes,chunk.count,chunk.index].every(Number.isInteger)||chunk.totalBytes<1||chunk.totalBytes>maximumStateBytes||chunk.index<0||chunk.count!==Math.ceil(chunk.totalBytes/stateChunkBytes)||chunk.index>=chunk.count)throw new Error('State envelope');
    const part=bytes(chunk.data,stateChunkBytes),expected=Math.min(stateChunkBytes,chunk.totalBytes-chunk.index*stateChunkBytes);
    if(part.length!==expected)throw new Error('State chunk length');
    const a=this.active??={header:chunk,data:new Uint8Array(chunk.totalBytes),seen:new Uint8Array(chunk.count),received:0,expires:now+30000};
    const h=a.header;
    if(now>=a.expires||h.transferId!==chunk.transferId||h.worldEpoch!==chunk.worldEpoch||h.revision!==chunk.revision||h.count!==chunk.count||h.totalBytes!==chunk.totalBytes||h.sha256!==chunk.sha256||a.seen[chunk.index])throw new Error('Conflicting state chunk');
    a.data.set(part,chunk.index*stateChunkBytes);a.seen[chunk.index]=1;a.received++;
    if(a.received!==chunk.count)return;
    const hash=Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',a.data)),n=>n.toString(16).padStart(2,'0')).join('');
    if(hash!==h.sha256)throw new Error('State integrity');
    const payload=boundedJson(new TextDecoder('utf-8',{fatal:true}).decode(a.data)) as {message:WireMessage;packedTerrain:PackedTerrain|null};
    if(!payload||Object.keys(payload).sort().join(',')!=='message,packedTerrain'||!validWireShape(payload.message))throw new Error('State payload');
    const message=payload.message,snapshot=message.type==='join'?message.snapshot:message.type==='snapshot'?message:undefined;
    if(!snapshot||snapshot.worldEpoch!==h.worldEpoch||snapshot.revision!==h.revision)throw new Error('State identity');
    if(payload.packedTerrain!==null) {
      if(snapshot.terrain!==undefined||(!snapshot.mixed&&snapshot.entities?.length!==0)||!validatePacked(payload.packedTerrain))throw new Error('Packed state header');
      const unpacked=unpackTerrain(payload.packedTerrain,snapshot.map);
      if(snapshot.mixed)unpacked.entities.push(...(snapshot.entities??[]));
      Object.assign(snapshot,unpacked);
    }
    const result=parseMessageValue(message);this.active=undefined;return result;
  }
}
