import source from '../../../content/localization/exav-stable-bundled/source.json' with { type: 'json' };
import en from '../../../content/localization/exav-stable-bundled/en.json' with { type: 'json' };
import { matchLocale } from './locale-matching.ts';

export interface ContentTextSource {
  pluginId: string; contributionId: string; field: string; sourcePath: string;
  sourceKey: string; sourceSha256: string; originalText: string; originalTextSha256: string;
}
export interface ContentTranslation { text: string; status: string }
export interface ContentLocalization {
  packId: string; sourceRevision: string; sourceLocale: string;
  entries: Record<string, ContentTextSource>;
  locales: Record<string, { entries: Record<string, ContentTranslation> }>;
}
export const bundledLocalization: ContentLocalization = { ...source, locales: { en } };

/** Bind presentation to pack, plugin, stable contribution and field. A changed
 * source cannot acquire an old translation. Missing/unreviewed text uses the source. */
export function contentText(bundle: ContentLocalization, pack: { packId: string; source: { revision: string } },
  target: { pluginId: string; id: string }, field: string, original: string, locale: string): string {
  if (pack.packId !== bundle.packId || pack.source.revision !== bundle.sourceRevision) return original;
  const key = `${target.pluginId}/${target.id}/${field}`, entry = bundle.entries[key];
  if (!entry || entry.originalText !== original) return original;
  const language = matchLocale(locale, [bundle.sourceLocale, ...Object.keys(bundle.locales)]);
  if (!language || language === bundle.sourceLocale) return original;
  const translated = bundle.locales[language]?.entries[key];
  return translated?.status === 'reviewed-with-feature' && translated.text.trim() ? translated.text : original;
}
