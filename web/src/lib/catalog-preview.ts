import {staticGroundArt} from './static-ground-art.ts';
import { RecipeImageCache, instantiate, traversal, type RenderCatalog } from './content-recipes.ts';
import {cityCatalogChoice,fixedCityDefinitions,type CityCatalogChoice} from './fixed-city-edit.ts';
import { bundledCatalog } from './bundled-catalog.ts';
import type { SignalDirection } from './building-catalog.ts';

const cache = new RecipeImageCache();
const previews = new Map<string, Promise<string>>();

/** Draw a coupled consist rather than isolated catalog tiles. */
export async function formationPreview(ids: readonly string[]): Promise<{src:string;width:number;height:number}> {
  const content = await bundledCatalog();
  const layers = traversal(ids.flatMap((id, index) => {
    const recipe = content.renderRecipes.find(recipe => recipe.id === id);
    if (!recipe) throw new Error('Formation preview unavailable');
    // Side frames have transparent margins; overlap those margins so the
    // couplings touch instead of leaving a thumbnail gap between vehicles.
    return instantiate(recipe, `${id}/${index}`, [index*7/8, index*7/8, 0], {angle:recipe.fields.asymmetric===true?14:6});
  }));
  if (!layers.length || layers.some(layer => layer.frame.status !== 'ready')) throw new Error('Formation preview unavailable');
  const bounds = layers.map(({position:[x,y,z],frame}) => ({
    x:16*(x+y)-frame.anchor![0], y:8*(y-x)-16*z-frame.anchor![1],
    w:frame.rectangle![2], h:frame.rectangle![3],
  }));
  const left=Math.min(...bounds.map(b=>b.x)), top=Math.min(...bounds.map(b=>b.y));
  const width=2*(Math.max(...bounds.map(b=>b.x+b.w))-left), height=2*(Math.max(...bounds.map(b=>b.y+b.h))-top);
  const ready=await cache.ready(content,layers,{season:'spring',night:false});
  const canvas=document.createElement('canvas');canvas.width=width;canvas.height=height;
  const ctx=canvas.getContext('2d')!;ctx.imageSmoothingEnabled=false;
  ready.draw(ctx,{x:-left*2,y:-top*2,zoom:2});
  return {src:canvas.toDataURL(),width,height};
}

/** Use the same executable crop corrections and selected stack/half as the map. */
export function catalogPreviewLayers(content:RenderCatalog,id:string,direction?:SignalDirection,choice?:CityCatalogChoice) {
  content=staticGroundArt(content);
  const recipe=content.renderRecipes.find(recipe=>recipe.id===id);
  if(!recipe)throw new Error('Catalog preview unavailable');
  if(recipe.handler==='rice')return traversal(Array.from({length:9},(_,i)=>instantiate(recipe,id,[Math.floor(i/3),i%3,0],{selectors:{x:Math.floor(i/3),y:i%3}})).flat());
  if(recipe.handler==='random-land'){
    const ids=recipe.fields.lands as string[],width=Math.ceil(Math.sqrt(ids.length));
    return traversal(ids.flatMap((id,i)=>{const r=content.renderRecipes.find(r=>r.id===id);if(r?.handler!=='land-0d')throw new Error('Mixed ground preview unavailable');return instantiate(r,id,[i%width,Math.floor(i/width),0]);}));
  }
  const d=fixedCityDefinitions.find(d=>d.reference.recipeId===id),selected=choice??(d?cityCatalogChoice(id):undefined);
  const half=d?.kind==='half';
  if(half&&!d.halfVariants.some(v=>v.direction===selected!.direction&&v.side===(selected!.side==='fore'?1:0)&&v.color===selected!.color))throw new Error('Unsupported half selection');
  const layers=traversal(instantiate(recipe,id,[0,0,0],
    half?{selectors:{direction:selected!.direction,side:selected!.side==='fore'?0:1,color:selected!.color}}:
    d?.kind==='variable'?{height:selected!.height}:
    ['train-car','reverse-train-car'].includes(recipe.handler)?{angle:4}:
    direction===undefined?{}:{selectors:{direction}}));
  if(!layers.length||layers.some(layer=>layer.frame.status!=='ready'))throw new Error('Catalog preview unavailable');
  return layers;
}
export function catalogPreview(id: string, direction?: SignalDirection, choice?:CityCatalogChoice): Promise<string> {
  const key = JSON.stringify([id, direction ?? 0,choice]);
  let pending = previews.get(key);
  if (!pending) {
    pending = (async () => {
      const content = staticGroundArt(await bundledCatalog());
      const layers = catalogPreviewLayers(content,id,direction,choice);
      const bounds = layers.map(({ position: [x, y, z], frame }) => ({
        x: 16 * (x + y) - frame.anchor![0], y: 8 * (y - x) - 16 * z - frame.anchor![1],
        w: frame.rectangle![2], h: frame.rectangle![3],
      }));
      const left = Math.min(...bounds.map(b => b.x)), top = Math.min(...bounds.map(b => b.y));
      const width = Math.max(...bounds.map(b => b.x + b.w)) - left;
      const height = Math.max(...bounds.map(b => b.y + b.h)) - top;
      const zoom = Math.min(2, 88 / width, 72 / height);
      const ready = await cache.ready(content, layers, { season: 'spring', night: false });
      const canvas = document.createElement('canvas'); canvas.width = 96; canvas.height = 80;
      const ctx = canvas.getContext('2d')!; ctx.imageSmoothingEnabled = false;
      ready.draw(ctx, { x: (96 - width * zoom) / 2 - left * zoom, y: (80 - height * zoom) / 2 - top * zoom, zoom });
      return canvas.toDataURL();
    })().catch(error => { previews.delete(key); throw error; });
    previews.set(key, pending);
  }
  return pending;
}
