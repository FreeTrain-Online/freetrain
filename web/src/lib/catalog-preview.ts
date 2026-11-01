import { RecipeImageCache, instantiate, traversal } from './content-recipes.ts';
import { bundledCatalog } from './bundled-catalog.ts';
import type { SignalDirection } from './building-catalog.ts';

const cache = new RecipeImageCache();
const previews = new Map<string, Promise<string>>();

export function catalogPreview(id: string, direction?: SignalDirection): Promise<string> {
  const key = JSON.stringify([id, direction ?? 0]);
  let pending = previews.get(key);
  if (!pending) {
    pending = (async () => {
      const content = await bundledCatalog();
      const recipe = content.renderRecipes.find(recipe => recipe.id === id);
      if (!recipe) throw new Error('Catalog preview unavailable');
      const layers = traversal(instantiate(recipe, id, [0, 0, 0], direction === undefined ? {} : { selectors: { direction } })).filter(layer => layer.frame.status === 'ready');
      if (!layers.length) throw new Error('Catalog preview unavailable');
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
