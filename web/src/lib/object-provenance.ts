import type { RenderCatalog } from './content-recipes.ts';
import { pluginText } from './plugin-localization.ts';

export const bundledPackId = 'exav-stable-bundled';
export interface ObjectOrigin {
  author?: string;
  plugin?: string;
}

/** Follow the contribution's owning plugin, never an image's shared sheet or
 * namespace. Generated system recipes retain their originating plugin too. */
export function objectOrigin(catalog: RenderCatalog, id: string, locale = 'ja'): ObjectOrigin | undefined {
  const pluginId = catalog.contributions?.find(item => item.id === id)?.pluginId
    ?? catalog.renderRecipes.find(item => item.id === id)?.pluginId;
  if (!pluginId) return;
  const plugin = catalog.plugins?.find(item => item.id === pluginId);
  if (!plugin) return;
  return {
    author: pluginText(catalog, plugin, 'author', locale)?.trim() || undefined,
    plugin: pluginText(catalog, plugin, 'title', locale)?.trim() || undefined,
  };
}
