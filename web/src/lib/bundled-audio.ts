import type { Media, RenderCatalog } from './content-recipes.ts';

export interface BundledBell {
  id: string; pluginId: string; sourcePath: string; name: string;
  media: Media; author: string;
}
/** Bind departure bells to their exact prepared, hashed WAV resources.
 * BGM declarations remain source metadata; playback is deferred. */
export function bundledBells(catalog: RenderCatalog): BundledBell[] {
  return (catalog.contributions ?? []).filter(c => c.type === 'trainDepartureBell').map(c => {
    const children = c.declaration.children;
    const href = children.find(n => n.tag === 'sound')?.attributes.href;
    if (!href || /[\\:#?%]|(^|\/)\.\.?($|\/)/.test(href)) throw new Error('Invalid bundled audio reference');
    const source = c.sourcePath.slice(0, c.sourcePath.lastIndexOf('/') + 1) + href;
    const asset = catalog.assets?.find(a => a.path === source);
    if (!asset?.native || asset.mediaType !== 'wav') throw new Error('Missing bundled audio');
    return { id: c.id, pluginId: c.pluginId, sourcePath: c.sourcePath,
      name: children.find(n => n.tag === 'name')?.text ?? c.id,
      media: asset.native, author: children.find(n => n.tag === 'author')?.text ?? String(asset.author ?? '') };
  });
}
