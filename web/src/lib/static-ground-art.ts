import {variableCityArt} from './variable-city-art.ts';
// ASSET-CROP-15..18: retain source pixels; absent lower rows are transparent.
import corrections from '../generated/static-ground-art.json' with {type:'json'};
import type {RenderCatalog,Frame} from './content-recipes.ts';
const cache=new WeakMap<RenderCatalog,RenderCatalog>();
export function staticGroundArt(catalog:RenderCatalog):RenderCatalog {
 const previous=cache.get(catalog);if(previous)return previous;
 const original=catalog;catalog=variableCityArt(catalog);
 if(catalog.packId!=='exav-stable-bundled'||catalog.source.revision!==corrections.sourceRevision)return catalog;
 const recipes=catalog.renderRecipes.map(recipe=>{
  const changes=corrections.frames.filter(c=>c.recipe===recipe.id);if(!changes.length)return recipe;
  return {...recipe,frames:recipe.frames.map(frame=>{
   const change=changes.find(c=>c.key===frame.key);if(!change)return frame;
   if(frame.status!=='invalid-source-crop'||JSON.stringify(frame.rectangle)!==JSON.stringify(change.originalRectangle)||JSON.stringify(frame.anchor)!==JSON.stringify(change.originalAnchor)||
      Object.entries(change.surfaces).some(([key,hash])=>catalog.surfaceSets[frame.surfaceSet!]?.[key]?.image.sha256!==hash))throw new Error('Static ground crop provenance changed');
   return {...frame,status:'ready',rectangle:change.rectangle as Frame['rectangle'],anchor:change.anchor as Frame['anchor'],masks:change.masks} as Frame;
  })};
 });
 const result={...catalog,renderRecipes:recipes,masks:{...catalog.masks,...corrections.masks}};cache.set(original,result);cache.set(catalog,result);cache.set(result,result);return result;
}
