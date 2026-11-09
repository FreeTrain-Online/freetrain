import english from '../../../content/localization/exav-stable-bundled/plugin-en.json' with { type: 'json' };
import { matchLocale } from './locale-matching.ts';

export interface PluginTranslation {
  sourcePath: string;
  sourceSha256: string;
  title: { original: string; text: string };
  author: { original: string; text: string };
}
export const bundledPluginLocalization: {
  packId: string; sourceRevision: string; sourceLocale: string;
  entries: Record<string, PluginTranslation>;
} = english;

/** Localize presentation only. Japanese and unsupported locales retain source
 * metadata; a different pack, revision or field cannot inherit these translations. */
export function pluginText(pack: { packId: string; source: { revision: string } },
  plugin: { id: string; title?: string; author: unknown }, field: 'title' | 'author', locale: string): string | undefined {
  const original = plugin[field];
  if (typeof original !== 'string') return;
  const bundle = bundledPluginLocalization;
  if (pack.packId !== bundle.packId || pack.source.revision !== bundle.sourceRevision ||
    matchLocale(locale, [bundle.sourceLocale, 'en']) !== 'en') return original;
  const entry = bundle.entries[plugin.id]?.[field];
  return entry?.original === original ? entry.text : original;
}
