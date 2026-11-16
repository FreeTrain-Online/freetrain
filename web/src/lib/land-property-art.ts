import {staticGroundArt} from './static-ground-art.ts';
import art from '../generated/land-property-art.json' with {type:'json'};
import type {RenderCatalog,Recipe} from './content-recipes.ts';
export const landPropertyRecipe='system:land-property';
const cache=new WeakMap<RenderCatalog,RenderCatalog>();
export function landPropertyArt(catalog:RenderCatalog):RenderCatalog {
 const previous=cache.get(catalog);if(previous)return previous;
 const original=catalog;catalog=staticGroundArt(catalog);
 const prepared=cache.get(catalog);if(prepared){cache.set(original,prepared);return prepared;}
 if(catalog.packId!=='exav-stable-bundled'||catalog.source.revision!==art.sourceRevision)return catalog;
 const recipe:Recipe={id:landPropertyRecipe,pluginId:'system',sourcePath:'trunk/core/world/land/LandPropertyVoxel.cs',handler:'source-geometry',status:'ready',fields:{},frames:[{key:'property',pictureId:'{0E7A9F09-4482-4b78-8A8D-F59F02574B1B}',rectangle:[0,0,32,16],anchor:[0,0],cell:[0,0,0],stage:20,selector:{},status:'ready',surfaceSet:landPropertyRecipe,masks:art.frameMasks}]};
 const result={...catalog,renderRecipes:[...catalog.renderRecipes,recipe],surfaceSets:{...catalog.surfaceSets,[landPropertyRecipe]:art.sets},masks:{...catalog.masks,...art.masks}};
 cache.set(original,result);cache.set(catalog,result);cache.set(result,result);return result;
}
