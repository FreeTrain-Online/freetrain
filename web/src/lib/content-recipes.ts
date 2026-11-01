/** Stable content rendering independent of gameplay/profile activation. */
export type Vec3 = [number, number, number];
export interface Media { url: string; sha256: string; bytes: number }
export interface Frame {
  key: string; cell: Vec3; status: 'ready' | 'invisible' | 'invalid-source-crop';
  rectangle?: [number,number,number,number]; anchor?: [number,number];
  pictureId?: string; surfaceSet?: string; baseSurfaceSet?: string; masks?: Record<string,string>;
  stage?: number; selector: Record<string,number|boolean>;
}
export interface Recipe {
  id: string; pluginId: string; handler: string; sourcePath?: string;
  fields: Record<string,unknown>; frames: Frame[]; status: string;
}
export interface Binding { image: Media; width: number; height: number; sourcePath: string }
export interface RenderCatalog {
  schemaVersion: number; packId: string; version: string;
  source: { revision: string };
  renderRecipes: Recipe[];
  plugins?: {id:string;title?:string;author:unknown}[];
  assets?: {path:string;author:unknown;credits:unknown;notices:unknown;mediaType?:string;native?:Media}[];
  contributions?: {id:string;pluginId:string;type:string;sourcePath:string;declaration:{children:{tag:string;text:string;attributes:Record<string,string>}[]}}[];
  surfaceSets: Record<string,Record<string,Binding>>;
  masks: Record<string,string[]>;
}
export interface Variant { season: 'spring'|'summer'|'autumn'|'winter'; night: boolean; drawPoles?:boolean; drawSpeedLimits?:boolean }
export const variantKey = (v:Variant) => `${v.season}/${v.night?'night':'day'}`;
export const catalogURL = '/content/bundled-v2/catalog.json';
export async function sha256(bytes: Uint8Array): Promise<string> {
  return [...new Uint8Array(await crypto.subtle.digest('SHA-256', bytes as BufferSource))].map(b=>b.toString(16).padStart(2,'0')).join('');
}
function safeMedia(media:Media) {
  if (!(media.url.startsWith('/content/bundled-v2/') || /^\/api\/content\/[a-z0-9][a-z0-9._-]{0,95}\/[0-9a-f]{64}\//.test(media.url)) || /[?#\\%]|(^|\/)\.\.?($|\/)/.test(media.url) ||
      !/^[0-9a-f]{64}$/.test(media.sha256) || !Number.isSafeInteger(media.bytes) || media.bytes<0)
    throw new Error('Invalid bundled media descriptor');
}
export function validateCatalog(c:RenderCatalog, enabled?:{id:string;version:string;catalogUrl:string}, mediaRoots?:string[]): RenderCatalog {
  if (c.schemaVersion!==2 || (enabled ? c.packId!==enabled.id || c.version!==enabled.version :
      c.packId!=='exav-stable-bundled' || c.version!=='2' || c.source.revision!=='883cf7d81cf49db25ff38cd3a25d0f140afe31a0')) throw new Error('Unsupported render catalog');
  const ids=new Set<string>();
  for(const recipe of c.renderRecipes) {
    if(ids.has(recipe.id)) throw new Error('Duplicate render recipe'); ids.add(recipe.id);
    for(const frame of recipe.frames) {
      if(frame.status==='invisible') continue;
      const rect=frame.rectangle, anchor=frame.anchor;
      if(!rect || !anchor || rect.length!==4 || anchor.length!==2 || frame.cell.length!==3 ||
         ![...rect,...anchor,...frame.cell].every(Number.isSafeInteger) ||
         !frame.surfaceSet || !c.surfaceSets[frame.surfaceSet] || !frame.baseSurfaceSet || !c.surfaceSets[frame.baseSurfaceSet] || !frame.masks)
        throw new Error('Invalid render frame');
      for(const [key,maskId] of Object.entries(frame.masks)) {
        const binding=c.surfaceSets[frame.surfaceSet][key], mask=c.masks[maskId];
        if(!binding || !mask || mask.length!==rect[3] || mask.some(row=>row.length!==rect[2] || /[^01]/.test(row)))
          throw new Error('Invalid render mask');
      }
    }
  }
  for(const set of Object.values(c.surfaceSets)) for(const binding of Object.values(set)) {
    safeMedia(binding.image);
    if(enabled && !(mediaRoots??[enabled.catalogUrl.slice(0,-'catalog.json'.length)]).some(root=>binding.image.url.startsWith(root)) && !binding.image.url.startsWith('/content/bundled-v2/')) throw new Error('Foreign pack media');
  }
  return c;
}
export async function loadRenderCatalog(expectedHash?: string, signal?:AbortSignal):Promise<RenderCatalog> {
  const response=await fetch(catalogURL,{signal}); if(!response.ok) throw new Error('Bundled catalog unavailable');
  const bytes=new Uint8Array(await response.arrayBuffer());
  const hashResponse=expectedHash ? undefined : await fetch('/content/bundled-v2/catalog.sha256',{signal});
  if(hashResponse && !hashResponse.ok) throw new Error('Bundled catalog checksum unavailable');
  const hash=expectedHash ?? (await hashResponse!.text()).trim();
  if(await sha256(bytes)!==hash) throw new Error('Bundled catalog hash mismatch');
  return validateCatalog(JSON.parse(new TextDecoder().decode(bytes)));
}

export interface SpriteImage { image: CanvasImageSource; width:number; height:number }
export type Decoder = (media:Media)=>Promise<SpriteImage>;
async function browserDecode(media:Media):Promise<SpriteImage> {
  safeMedia(media);
  const response=await fetch(media.url); if(!response.ok) throw new Error('Bundled image unavailable: '+media.url);
  const bytes=new Uint8Array(await response.arrayBuffer());
  if(bytes.length!==media.bytes || await sha256(bytes)!==media.sha256) throw new Error('Bundled image hash mismatch');
  const url=URL.createObjectURL(new Blob([bytes],{type:'image/png'}));
  try {
    const image=new Image(); image.src=url; await image.decode();
    return {image,width:image.naturalWidth,height:image.naturalHeight};
  } finally { URL.revokeObjectURL(url); }
}
/** Concurrent requests share a decode; failed loads can be retried. Owned by a
 * catalog consumer, so a replaced world/catalog cannot reuse stale readiness. */
export class RecipeImageCache {
  private pending=new Map<string,Promise<SpriteImage>>();
  constructor(private decode:Decoder=browserDecode) {}
  get(media:Media):Promise<SpriteImage> {
    safeMedia(media); const key=media.url+'#'+media.sha256;
    let promise=this.pending.get(key);
    if(!promise) {
      promise=this.decode(media).catch(error=>{this.pending.delete(key);throw error;});
      this.pending.set(key,promise);
    }
    return promise;
  }
  async ready(catalog:RenderCatalog, layers:readonly Layer[], variant:Variant):Promise<ReadyScene> {
    const key=variantKey(variant), images=new Map<string,SpriteImage>();
    await Promise.all(layers.map(async layer=>{
      const f=layer.frame; if(f.status==='invisible' || !f.masks?.[key]) return;
      await Promise.all([...new Set([f.surfaceSet!,f.baseSurfaceSet??f.surfaceSet!])].map(async id=>{
        const binding=catalog.surfaceSets[id][key], loaded=await this.get(binding.image);
        if(loaded.width!==binding.width || loaded.height!==binding.height) throw new Error('Bundled image extent mismatch');
        images.set(binding.image.url,loaded);
      }));
    }));
    return new ReadyScene(catalog,layers,variant,images);
  }
}
export interface RecipeOptions { height?:number; angle?:number; slope?:number; selectors?:Record<string,number|boolean> }
export interface Layer { entityId:string; recipeId:string; frame:Frame; position:Vec3 }
export function instantiate(recipe:Recipe, entityId:string, position:Vec3, options:RecipeOptions={}):Layer[] {
  let frames=recipe.frames;
  if(recipe.handler==='variable-height') {
    const min=Number(recipe.fields.minHeight),max=Number(recipe.fields.maxHeight),height=options.height??min;
    if(!Number.isInteger(height) || height<min || height>max) throw new Error('Invalid building height');
    const tops=Number(recipe.fields.topCount),bottoms=Number(recipe.fields.bottomCount);
    frames=Array.from({length:height},(_,z)=>{
      const prefix=z>=height-tops?`top/${height-z-1}/`:z<bottoms?`bottom/${z}/`:'middle/';
      return recipe.frames.filter(f=>f.key.startsWith(prefix)).map(f=>({...f,cell:[f.cell[0],f.cell[1],z] as Vec3}));
    }).flat();
  }
  if(['train-car','reverse-train-car'].includes(recipe.handler)) {
    frames=frames.filter(f=>options.slope===undefined ? f.selector.angle===(options.angle??0) : f.selector.slope===options.slope);
  }
  const defaults:Record<string,Record<string,number|boolean>>={
    road:{connections:1},fence:{direction:0},forest:{pattern:0},creek:{connections:0},rice:{x:0,y:0},
    vinyl:{index:1},signal:{direction:0},'electric-pole':{axis:0},'half-voxel':{direction:0,side:0,color:0},'special-rail':{index:0},
  };
  const selectors={...defaults[recipe.handler],...options.selectors};
  if(Object.keys(selectors).length) frames=frames.filter(f=>Object.entries(selectors).every(([k,v])=>f.selector[k]===v || (recipe.handler==='forest' && f.key==='ground')));
  return frames.filter(f=>f.status!=='invisible').map(frame=>({entityId,recipeId:recipe.id,frame,
    position:[position[0]+frame.cell[0],position[1]+frame.cell[1],position[2]+frame.cell[2]]}));
}
export function composeTrain(catalog:RenderCatalog,id:string,length:number):string[] {
  if(!Number.isSafeInteger(length) || length<1 || length>1024) throw new Error('Invalid train length');
  const recipe=catalog.renderRecipes.find(r=>r.id===id);
  if(recipe?.handler!=='train-composition') throw new Error('Unknown train composition');
  const comp=recipe.fields.composition as Record<string,string>, cars=Array<string>(length).fill(comp.body);
  if(comp.head) cars[0]=comp.head;
  if(comp.tail) cars[length-1]=comp.tail; // Source tail wins for length one.
  return cars;
}
export function traversal(layers:readonly Layer[], heightCut=Infinity):Layer[] {
  return layers.filter(l=>l.position[2]<=heightCut).sort((a,b)=>{
    const [x,y,z]=a.position,[xx,yy,zz]=b.position;
    return (y-x)-(yy-xx) || ((x+y)>>1)-((xx+yy)>>1) || z-zz || (a.frame.stage??20)-(b.frame.stage??20);
  });
}
export interface View { x:number; y:number; zoom:number; heightCut?:number }
function placement(layer:Layer,view:View) {
  const [x,y,z]=layer.position, anchor=layer.frame.anchor!;
  return {x:view.x+(16*(x+y)-anchor[0])*view.zoom,y:view.y+(8*(y-x)-16*z-anchor[1])*view.zoom};
}
/** Native RGB888 bltAlpha: halve channels separately before adding. */
export function halfBlend(source:readonly number[],destination:readonly number[]):number[] {
  return source[3]===0 ? [...destination] : [0,1,2].map(i=>(source[i]>>1)+(destination[i]>>1)).concat(destination[3]);
}
export type DrawEffect = {kind:'normal'} | {kind:'shape';color:string} | {kind:'alpha'};
export class ReadyScene {
  private normalImages=new Map<string,CanvasImageSource>();
  private frameKeys=new WeakMap<object,string>();
  private scaledFrames=new Map<string,{image:OffscreenCanvas;bytes:number}>();
  private scaledBytes=0;
  constructor(private catalog:RenderCatalog, readonly layers:readonly Layer[], readonly variant:Variant, private images:Map<string,SpriteImage>) {}
  paintNormal(ctx:CanvasRenderingContext2D,view:View,l:Layer,pixelStable=false) {
    const f=l.frame,id=f.surfaceSet!;
    let image=this.normalImages.get(id);
    if(!image){const binding=this.catalog.surfaceSets[id][variantKey(this.variant)];image=this.images.get(binding.image.url)?.image;if(!image)throw new Error('Scene assets not ready');this.normalImages.set(id,image);}
    const [x,y,z]=l.position,[ax,ay]=f.anchor!,[sx,sy,w,h]=f.rectangle!;
    const left=view.x+(16*(x+y)-ax)*view.zoom,top=view.y+(8*(y-x)-16*z-ay)*view.zoom;
    if(pixelStable&&typeof OffscreenCanvas!=='undefined') {
      // Rasterize a crop at its subpixel phase once, then copy physical pixels.
      // This avoids GPU nearest-neighbor tie rounding changing as the same
      // scaled sprite moves across a large canvas, and makes scroll reuse exact.
      const dpr=ctx.getTransform().a,scale=view.zoom*dpr,px=left*dpr,py=top*dpr;
      const ix=Math.floor(px+1e-7),iy=Math.floor(py+1e-7),fx=Math.max(0,Math.round((px-ix)*1e7)/1e7),fy=Math.max(0,Math.round((py-iy)*1e7)/1e7);
      let base=this.frameKeys.get(f);if(!base){base=id+'/'+[sx,sy,w,h].join(',');this.frameKeys.set(f,base);}
      const key=base+'/'+[scale,fx,fy].join(','),width=Math.ceil(w*scale+fx),height=Math.ceil(h*scale+fy),bytes=width*height*4;
      if(bytes<=16*1024*1024) {
        let cached=this.scaledFrames.get(key);
        if(!cached) {
          while(this.scaledBytes+bytes>64*1024*1024&&this.scaledFrames.size) {
            const first=this.scaledFrames.keys().next().value!;this.scaledBytes-=this.scaledFrames.get(first)!.bytes;this.scaledFrames.delete(first);
          }
          const raster=new OffscreenCanvas(width,height),paint=raster.getContext('2d')!;paint.imageSmoothingEnabled=false;
          paint.drawImage(image,sx,sy,w,h,fx,fy,w*scale,h*scale);cached={image:raster,bytes};this.scaledFrames.set(key,cached);this.scaledBytes+=bytes;
        }
        ctx.drawImage(cached.image,ix/dpr,iy/dpr,width/dpr,height/dpr);return;
      }
    }
    ctx.drawImage(image,sx,sy,w,h,left,top,w*view.zoom,h*view.zoom);
  }
  draw(ctx:CanvasRenderingContext2D,view:View, effect:DrawEffect={kind:'normal'}, ordered=traversal(this.layers,view.heightCut)) {
    if(!Number.isFinite(view.zoom)||view.zoom<=0) throw new Error('Invalid sprite zoom');
    const key=variantKey(this.variant); ctx.imageSmoothingEnabled=false;
    for(const l of ordered) {
      const f=l.frame; if(!f.masks?.[key]) continue;
      const [sx,sy,w,h]=f.rectangle!,p=placement(l,view), binding=this.catalog.surfaceSets[effect.kind==='normal'?f.surfaceSet!:(f.baseSurfaceSet??f.surfaceSet!)][key];
      const image=this.images.get(binding.image.url); if(!image) throw new Error('Scene assets not ready');
      if(effect.kind==='normal') {
        ctx.drawImage(image.image,sx,sy,w,h,p.x,p.y,w*view.zoom,h*view.zoom);
      } else {
        const temporary=new OffscreenCanvas(Math.ceil(w*view.zoom),Math.ceil(h*view.zoom)),paint=temporary.getContext('2d')!;
        paint.imageSmoothingEnabled=false;paint.drawImage(image.image,sx,sy,w,h,0,0,w*view.zoom,h*view.zoom);
        if(effect.kind==='shape') {
          paint.globalCompositeOperation='source-in';paint.fillStyle=effect.color;paint.fillRect(0,0,temporary.width,temporary.height);
          ctx.drawImage(temporary,p.x,p.y);
        } else {
          // Match the integer source blend, including odd channel truncation.
          const x=Math.round(p.x),y=Math.round(p.y),source=paint.getImageData(0,0,temporary.width,temporary.height);
          const destination=ctx.getImageData(x,y,temporary.width,temporary.height);
          for(let i=0;i<source.data.length;i+=4) {
            if(source.data[i+3]===0)continue;
            for(let channel=0;channel<3;channel++)destination.data[i+channel]=(source.data[i+channel]>>1)+(destination.data[i+channel]>>1);
          }
          ctx.putImageData(destination,x,y);
        }
      }
    }
  }
  pick(x:number,y:number,view:View, ordered=traversal(this.layers,view.heightCut)):Layer|undefined {
    const key=variantKey(this.variant);
    for(let i=ordered.length-1;i>=0;i--) {
      const l=ordered[i],f=l.frame,mask=f.masks?.[key]; if(!mask) continue;
      const p=placement(l,view),ix=Math.floor((x-p.x)/view.zoom),iy=Math.floor((y-p.y)/view.zoom);
      if(ix>=0 && iy>=0 && this.catalog.masks[mask][iy]?.[ix]==='1') return l;
    }
  }
}
