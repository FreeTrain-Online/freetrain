import type { Media, RenderCatalog } from './content-recipes.ts';

export interface BundledAudio {
  id: string; pluginId: string; sourcePath: string; name: string;
  kind: 'music' | 'bell'; media: Media; author: string;
}
/** Bind exact source declarations to prepared, hashed media. No inferred file
 * replacement and no user-provided MIDI is passed to the bundled sequencer. */
export function bundledAudio(catalog: RenderCatalog): BundledAudio[] {
  return (catalog.contributions ?? []).filter(c => c.type === 'bgm' || c.type === 'trainDepartureBell').map(c => {
    const children = c.declaration.children;
    const href = c.type === 'bgm' ? children.find(n => n.tag === 'href')?.text : children.find(n => n.tag === 'sound')?.attributes.href;
    if (!href || /[\\:#?%]|(^|\/)\.\.?($|\/)/.test(href)) throw new Error('Invalid bundled audio reference');
    const source = c.sourcePath.slice(0, c.sourcePath.lastIndexOf('/') + 1) + href;
    const asset = catalog.assets?.find(a => a.path === source);
    if (!asset?.native || asset.mediaType !== (c.type === 'bgm' ? 'mid' : 'wav')) throw new Error('Missing bundled audio');
    return { id: c.id, pluginId: c.pluginId, sourcePath: c.sourcePath,
      name: children.find(n => n.tag === 'name')?.text ?? c.id, kind: c.type === 'bgm' ? 'music' : 'bell',
      media: asset.native, author: children.find(n => n.tag === 'author')?.text ?? String(asset.author ?? '') };
  });
}
