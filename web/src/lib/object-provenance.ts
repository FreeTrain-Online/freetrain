import type { RenderCatalog } from './content-recipes.ts';

export const bundledPackId = 'exav-stable-bundled';
export interface ObjectOrigin {
  author?: string;
  plugin?: string;
}

/** Follow the contribution's owning plugin, never an image's shared sheet or
 * namespace. Generated system recipes retain their originating plugin too. */
export function objectOrigin(catalog: RenderCatalog, id: string): ObjectOrigin | undefined {
  const pluginId = catalog.contributions?.find(item => item.id === id)?.pluginId
    ?? catalog.renderRecipes.find(item => item.id === id)?.pluginId;
  if (!pluginId) return;
  const plugin = catalog.plugins?.find(item => item.id === pluginId);
  if (!plugin) return;
  return {
    author: typeof plugin.author === 'string' ? plugin.author.trim() || undefined : undefined,
    plugin: plugin.title?.trim() || undefined,
  };
}
