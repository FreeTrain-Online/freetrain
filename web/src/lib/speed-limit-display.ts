// Source SpeedLimit controllers: blt at voxel origin, same surface in all light/season variants.
import type {Definition,Position} from '../generated/contracts.ts';
import icons from '../generated/speed-limit-icons.json' with {type:'json'};
import type {Frame,RenderCatalog} from './content-recipes.ts';
export const speedIcons=icons;
type Speed=keyof typeof icons;
export function speedLimitFrame(speed:Speed):Frame {
 const icon=icons[speed],id=`speed-limit/${speed}`;
 return {key:id,cell:[0,0,0],status:'ready',rectangle:[0,0,icon.width,icon.height],anchor:[0,0],
  surfaceSet:id,baseSurfaceSet:id,masks:Object.fromEntries(['spring','summer','autumn','winter'].flatMap(s=>['day','night'].map(t=>[`${s}/${t}`,id]))),stage:60,selector:{}};
}
const catalogs=new WeakMap<RenderCatalog,RenderCatalog>();
export function speedLimitCatalog(catalog:RenderCatalog):RenderCatalog {
 const cached=catalogs.get(catalog);if(cached)return cached;
 const surfaceSets={...catalog.surfaceSets},masks={...catalog.masks};
 for(const [speed,icon] of Object.entries(icons)){
  const id=`speed-limit/${speed}`,binding={image:icon.image,width:icon.width,height:icon.height,sourcePath:`trunk/plugins/SpeedLimit/${speed[0].toUpperCase()+speed.slice(1)}SpeedIcon.bmp`};
  surfaceSets[id]=Object.fromEntries(['spring','summer','autumn','winter'].flatMap(s=>['day','night'].map(t=>[`${s}/${t}`,binding])));
  masks[id]=icon.mask;
 }
 const prepared={...catalog,surfaceSets,masks};catalogs.set(catalog,prepared);catalogs.set(prepared,prepared);return prepared;
}

export function speedLimitAt(limits:Readonly<Record<Speed,readonly Position[]>>|undefined,p:Position,definition:Definition):Definition|undefined {
 const speed=(Object.keys(icons) as Speed[]).find(s=>icons[s].id===definition.contributionId);
 return speed&&limits?.[speed].some(loc=>loc.x===p.x&&loc.y===p.y&&loc.z===p.z)?definition:undefined;
}
