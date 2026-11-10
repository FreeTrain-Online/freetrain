// Stable ExAV 883cf7d StationPassagewayController / ThinPlatform.Outlook.
import type {PassageQuery,PassagePreview,Position,PlatformYard} from '../generated/contracts.ts';
import type {ReadState} from './session-client.ts';
import {railwayRevision} from './railway-edit.ts';
import {offset} from './map-geometry.ts';
type Platform=NonNullable<NonNullable<ReadState['mixed']>['stationGraph']>['platforms'][number];
const key=(p:Position)=>`${p.x},${p.y},${p.z}`;
export function passageQuery(read:ReadState,from:Position,to:Position,material:PassageQuery['material'],remove=false,part:PassageQuery['part']='passage'):PassageQuery|undefined {
 const revision=railwayRevision(read);if(revision===undefined)return;
 return {expectedWorldEpoch:read.worldEpoch,expectedGeometryRevision:revision,expectedMinute:read.tick,expectedContentVersion:read.contentVersion as PassageQuery['expectedContentVersion'],action:remove?'remove':'place',from,to,material,part};
}
export function passagePath(from:Position,to:Position):Position[] {
 const dx=to.x-from.x,dy=to.y-from.y,n=Math.abs(dx)+Math.abs(dy)+1;
 if(from.z!==to.z||dx!==0&&dy!==0||n<2||n>100)return [];
 return Array.from({length:n},(_,i)=>({x:from.x+Math.sign(dx)*i,y:from.y+Math.sign(dy)*i,z:from.z}));
}
export function platformAppearance(platform:Platform,yard:PlatformYard,platforms:readonly Platform[]) {
 const o=yard.outlook;
 if(o.kind==='plain')return [{position:yard.location,recipeId:platform.kind==='wide'?'system:yard':`system:thin-${platform.direction+(yard.roof?1:0)}`,stage:platform.kind==='wide'?15:platform.direction===2||platform.direction===4?20:40}];
 const d=offset((platform.direction+6)%8),p={x:yard.location.x+d.x,y:yard.location.y+d.y,z:yard.location.z};
 const double=platforms.some(b=>b.kind==='thin'&&b.direction===(platform.direction+4)%8&&b.yards.some(y=>key(y.location)===key(p)&&y.outlook.kind==='passage'));
 const prefix=`${o.material}-`;
 if(o.kind==='stair'){
  const index=platform.direction*4|(o.upward?0:4)|(double?2:0)|(yard.roof?1:0);
  return [{position:yard.location,recipeId:`system:${prefix}stair-${index}`,stage:platform.direction===2||platform.direction===4?20:40}];
 }
 const index=platform.direction/2*3+(double?(o.bridge?2:1):0);
 const entries=[{position:yard.location,recipeId:`system:${prefix}passage-${index}`,stage:platform.direction===2||platform.direction===4?20:40}];
 if(o.bridge&&(platform.direction===2||platform.direction===4))entries.push({position:yard.location,recipeId:`system:${prefix}passage-${platform.direction===2?13:12}`,stage:40});
 return entries;
}
export function passageGhost(read:ReadState,preview:PassagePreview) {
 const q=preview.query,path=new Set(preview.positions.map(key)),direction=q.to.x>q.from.x?2:q.to.x<q.from.x?6:q.to.y>q.from.y?4:0;
 const platforms=(read.mixed?.stationGraph?.platforms??[]).map(p=>({...p,yards:p.yards.map(y=>{
  if(p.kind!=='thin'||!path.has(key(y.location)))return y;
  const old=y.outlook;
  if(q.part==='stairs'){
   if(q.action==='remove')return old.kind==='stair'?{...y,outlook:{kind:'plain' as const,material:'concrete' as const,bridge:false,upward:false}}:y;
   const d=offset(p.direction),at={x:y.location.x+d.x,y:y.location.y+d.y,z:y.location.z};
   const upward=read.mixed?.stationGraph?.platforms.some(b=>b.kind==='thin'&&b.yards.some(n=>key(n.location)===key(at)&&n.outlook.kind==='passage'))??false;
   return {...y,outlook:{kind:'stair' as const,material:q.material,bridge:false,upward}};
  }
  if(q.action==='remove')return old.kind==='passage'?{...y,outlook:{kind:'plain' as const,material:'concrete' as const,bridge:false,upward:false}}:y;
  if(old.kind==='passage'&&old.bridge)return y;
  const bridge=old.kind==='passage'||!(key(y.location)===key(q.from)&&(direction+2)%8===p.direction||key(y.location)===key(q.to)&&(direction+6)%8===p.direction);
  return {...y,outlook:{kind:'passage' as const,material:q.material,bridge,upward:false}};
 })}));
 // Include adjacent yards whose width changes as well as the edited yards.
 return platforms.flatMap(p=>p.yards.filter(y=>path.has(key(y.location))||preview.positions.some(a=>a.z===y.location.z&&Math.abs(a.x-y.location.x)+Math.abs(a.y-y.location.y)===1)).flatMap(y=>platformAppearance(p,y,platforms)));
}
