// ExAV ColorConfigDialog / ColoredTrainCarImpl. Original authors, LGPL-2.1-or-later.
import type {FleetColorTest} from '../generated/contracts.ts';
import {type RenderCatalog,type Recipe,RecipeImageCache,runtimeSurfaces} from './content-recipes.ts';
export const colorTestCarId='{6E5E12DD-69CB-4DF2-B4F0-2BCDA703CBB0}';
export const defaultTrainColors:FleetColorTest={revision:'0',template:{packId:'exav-stable-bundled',contributionId:'{243D9F53-67FB-432E-B985-3B9B696D8CE0}-1',recipeId:'{243D9F53-67FB-432E-B985-3B9B696D8CE0}-1'},body:'#ff0000',line1:'#0000ff',line2:'#00ff00',line3:'#ffff00'};
export const rgb=(hex:string)=>[1,3,5].map(i=>parseInt(hex.slice(i,i+2),16));
export function nightColor(rgb:readonly number[]):number[]{
 const lights:Record<string,number[]>={'8,0,0':[255,8,8],'0,8,0':[252,243,148],'0,0,8':[255,227,99]};
 return lights[rgb.join(',')]??rgb.map(v=>Math.trunc(v/4));
}
export function palette(colors:readonly string[],night:boolean){
 const source=[[0,0,255],[0,255,0],[255,0,0],[255,255,0]].flatMap(c=>[c,c.map(n=>Math.trunc(n*180/255))]);
 const target=colors.flatMap(c=>{const value=rgb(c);return [value,value.map(n=>Math.trunc(n*180/255))];});
 const rows=source.map((from,i)=>({from,to:target[i]}));
 return [...(night?rows.map(r=>({from:nightColor(r.from),to:nightColor(r.to)})):[]),...rows].reverse();
}
export function recolorPixels(bytes:Uint8ClampedArray,colors:readonly string[],night:boolean){
 const table=new Map<string,number[]>();for(const {from,to} of palette(colors,night))if(!table.has(from.join(',')))table.set(from.join(','),to);
 for(let i=0;i<bytes.length;i+=4)if(bytes[i+3]){const mapped=table.get(`${bytes[i]},${bytes[i+1]},${bytes[i+2]}`);if(mapped)bytes.set(mapped,i);}
}
export function colorMapXML(c:FleetColorTest){
 const escape=(s:string)=>s.replaceAll('&','&amp;').replaceAll('"','&quot;').replaceAll('<','&lt;').replaceAll('>','&gt;');
 return `<colorMap picture="${escape(c.template.contributionId)}" base="${rgb(c.body).join(',')}" line1="${rgb(c.line1).join(',')}" line2="${rgb(c.line2).join(',')}" line3="${rgb(c.line3).join(',')}" />`;
}
export function colorTemplates(c:RenderCatalog){
 const english=['Generic three-color thin bands','Generic four-color broad bands','Generic four-color thin bands','103 series style','205 series style','211 series style','E231 series style'];
 return (c.contributions??[]).filter(d=>d.type==='colorMapTrainPicture').map((d,i)=>({id:d.id,ja:d.declaration.children.find(c=>c.tag==='name')?.text??d.id,en:english[i]??d.id}));
}
/** One cached derived catalog per consumer, with verified raw template decodes
 * retained by RecipeImageCache. Prepared content and other worlds stay immutable. */
export class TrainColorCatalog {
 private key='';private source?:RenderCatalog;private pending?:Promise<RenderCatalog>;
 constructor(private cache:RecipeImageCache){}
 prepare(c:RenderCatalog,config?:FleetColorTest):Promise<RenderCatalog>{
  if(!config)return Promise.resolve(c);
  const key=JSON.stringify(config);if(this.source===c&&this.key===key&&this.pending)return this.pending;
  this.source=c;this.key=key;return this.pending=this.build(c,config);
 }
 private async build(c:RenderCatalog,config:FleetColorTest){
  const picture=c.pictures?.find(p=>p.id===config.template.contributionId),original=c.renderRecipes.find(r=>r.id===colorTestCarId);
  if(!picture||!original||config.template.packId!==c.packId||config.template.recipeId!==picture.id)throw new Error('Unavailable color-test template');
  const surfaces={...c.surfaceSets},masks={...c.masks},images=new Map(),frames=original.frames.map(f=>({...f,pictureId:picture.id,masks:{...f.masks}}));
  const colors=[config.body,config.line1,config.line2,config.line3];
  for(const variant of picture.variants)for(const light of ['day','night'] as const){
   const key=`${variant.season}/${light}`,media=variant[light].image,raw=await this.cache.get(media);
   const canvas=new OffscreenCanvas(raw.width,raw.height),ctx=canvas.getContext('2d')!;ctx.drawImage(raw.image,0,0);
   const data=ctx.getImageData(0,0,raw.width,raw.height);recolorPixels(data.data,colors,light==='night');ctx.putImageData(data,0,0);
   const surface=`local-color-test/${key}`;surfaces[surface]={[key]:{image:media,width:raw.width,height:raw.height,sourcePath:variant[light].path}};
   images.set(surface,{image:canvas,width:raw.width,height:raw.height});
   for(const [i,frame] of frames.entries()){
    const [x,y,w,h]=frame.rectangle!;if(x<0||y<0||x+w>raw.width||y+h>raw.height)throw new Error('Invalid color-test crop');
    const mask=`local-color-test/${key}/${i}`;masks[mask]=Array.from({length:h},(_,yy)=>Array.from({length:w},(_,xx)=>data.data[((y+yy)*raw.width+x+xx)*4+3]?'1':'0').join(''));
    frame.masks[key]=mask;
   }
  }
  // Surface sets contain all variants. Base images remain the uncolored template
  // for silhouettes and source alpha highlighting.
  const colored='local-color-test/colored',base='local-color-test/base';surfaces[colored]={};surfaces[base]={};
  const coloredImages=new Map<string,import('./content-recipes.ts').SpriteImage>();
  for(const variant of picture.variants)for(const light of ['day','night'] as const){
   const key=`${variant.season}/${light}`,surface=`local-color-test/${key}`;surfaces[colored][key]=surfaces[surface][key];surfaces[base][key]=surfaces[surface][key];
   coloredImages.set(`${colored}/${key}`,images.get(surface));
  }
  for(const frame of frames){frame.surfaceSet=colored;frame.baseSurfaceSet=base;}
  const recipe:Recipe={...original,frames};
  return {...c,surfaceSets:surfaces,masks,renderRecipes:c.renderRecipes.map(r=>r.id===original.id?recipe:r),[runtimeSurfaces]:coloredImages};
 }
}
