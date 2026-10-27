import { IncompatibleWorldError } from './world-identity.ts';
import { matchLocale } from './locale-matching.ts';
import { sha256, validateCatalog, type RenderCatalog } from './content-recipes.ts';

export interface PluginCredit { button: string; title: string; text: string }
export interface PluginPack {
  id: string; version: string; hash: string; catalogHash: string; catalogUrl: string;
  sourceRevision: string; profile: string;
  credits: { defaultLocale: string; locales: Record<string,PluginCredit> };
  notices: { path: string; url: string; sha256: string }[];
}
export interface EnabledContent { setHash: string; packs: PluginPack[] }
export const creditFor = (pack: PluginPack, locale: string): PluginCredit => pack.credits.locales[matchLocale(locale,Object.keys(pack.credits.locales)) ?? pack.credits.defaultLocale];

/** The server's bootstrap pins the complete selected set. The browser cannot add local packs. */
export async function loadEnabledContent(expectedHash: string, signal: AbortSignal): Promise<EnabledContent> {
  const response = await fetch('/api/content', {signal, credentials:'same-origin',cache:'no-store'});
  if(!response.ok) throw new Error('Enabled content unavailable');
  const content:EnabledContent = await response.json();
  if(!/^[0-9a-f]{64}$/.test(expectedHash) || content.setHash!==expectedHash || !Array.isArray(content.packs)) throw new IncompatibleWorldError('Enabled content changed');
  const ids=new Set<string>();
  await Promise.all(content.packs.map(async pack=>{
    if(!/^[a-z0-9][a-z0-9._-]{0,95}$/.test(pack.id) || ids.has(pack.id) || !/^[0-9a-f]{64}$/.test(pack.hash) || !/^[0-9a-f]{64}$/.test(pack.catalogHash) ||
       !new RegExp(`^/api/content/${pack.id.replaceAll('.', '\\.')}/[0-9a-f]{64}/catalog\\.json$`).test(pack.catalogUrl) || /[?#%\\]|(^|\/)\.\.?($|\/)/.test(pack.catalogUrl))
      throw new IncompatibleWorldError('Invalid enabled pack');
    ids.add(pack.id);
    for(const locale of ['en','ja',pack.credits.defaultLocale]) {
      const c=pack.credits.locales[locale];
      if(!c || !c.button?.trim() || !c.title?.trim() || !c.text?.trim()) throw new IncompatibleWorldError('Incomplete plugin credits');
    }
    const catalogResponse=await fetch(pack.catalogUrl,{signal,credentials:'same-origin',cache:'no-store'});
    if(!catalogResponse.ok) throw new Error('Enabled catalog unavailable');
    const bytes=new Uint8Array(await catalogResponse.arrayBuffer());
    if(await sha256(bytes)!==pack.catalogHash) throw new IncompatibleWorldError('Enabled catalog hash mismatch');
    const catalog:RenderCatalog=JSON.parse(new TextDecoder().decode(bytes));
    validateCatalog(catalog,pack,content.packs.map(p=>p.catalogUrl.slice(0,-'catalog.json'.length)));
    for(const notice of pack.notices) if(!notice.url.startsWith(pack.catalogUrl.slice(0,-'catalog.json'.length)) || /[?#%\\]|(^|\/)\.\.?($|\/)/.test(notice.url)) throw new Error('Invalid plugin notice');
  }));
  return content;
}
