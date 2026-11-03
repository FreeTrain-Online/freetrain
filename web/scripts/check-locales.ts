import { locales } from '../src/lib/localization.ts';
import { canonicalLocale } from '../src/lib/locale-matching.ts';
import { bundledLocalization } from '../src/lib/content-localization.ts';
import { sha256 } from '../src/lib/content-recipes.ts';
import stockCatalog from '../src/generated/stock-catalog.json' with {type:'json'};
import stockEnglish from '../../content/localization/exav-stable-bundled/stock-en.json' with {type:'json'};

export function parameters(text: string): string[] {
  return [...new Set([...text.matchAll(/\{([a-zA-Z][a-zA-Z0-9]*)\}/g)].map(m => m[1]))].sort();
}
export function validateMessages(source: Record<string, string>, translated: Record<string, string>, label: string): void {
  if (JSON.stringify(Object.keys(source).sort()) !== JSON.stringify(Object.keys(translated).sort())) throw new Error(`${label}: missing or unknown keys`);
  for (const [key, value] of Object.entries(translated)) {
    if (typeof value !== 'string' || !value.trim()) throw new Error(`${label}.${key}: empty message`);
    if (JSON.stringify(parameters(value)) !== JSON.stringify(parameters(source[key]))) throw new Error(`${label}.${key}: interpolation parameters differ`);
  }
}
export async function checkLocales() {
  const stockEntries=stockEnglish.entries as Record<string,{original:string;text:string}>;
  if(Object.keys(stockEntries).length!==stockCatalog.length*5)throw new Error('Stock translation coverage changed');
  for(const stock of stockCatalog)for(const field of ['company','type','name','author','description'] as const){
    const e=stockEntries[`${stock.id}/${field}`];
    if(stock.binding.catalogHash!==stockEnglish.catalogHash||!e?.text.trim()||e.original!==stock[field])throw new Error(`Stale stock translation: ${stock.id}/${field}`);
  }
  const source = locales.en;
  for (const [id, locale] of Object.entries(locales)) {
    if (canonicalLocale(id) !== id || !locale.label.trim() || !['ltr','rtl'].includes(locale.direction)) throw new Error(`Invalid locale: ${id}`);
    validateMessages(source.messages, locale.messages, `${id}/app`);
    validateMessages(source.gallery, locale.gallery, `${id}/gallery`);
    validateMessages({ gameTime: source.formats.gameTime }, { gameTime: locale.formats.gameTime }, `${id}/formats`);
    if (locale.formats.weekdays.length !== 7 || locale.formats.weekdays.some(day => !day.trim())) throw new Error(`${id}: seven weekdays required`);
    for (const [form, wording] of Object.entries(locale.formats.frameCount)) {
      if (!['zero','one','two','few','many','other'].includes(form) || !wording.trim() || parameters(wording).join() !== 'count') throw new Error(`${id}: invalid plural form`);
    }
    if (!locale.formats.frameCount.other) throw new Error(`${id}: plural fallback required`);
  }
  const review = JSON.parse(await Deno.readTextFile(new URL('../../content/bundled-v2/review.json', import.meta.url)));
  if (bundledLocalization.packId !== 'exav-stable-bundled' || bundledLocalization.sourceRevision !== review.source.revision) throw new Error('Content localization source changed');
  for (const [key, entry] of Object.entries(bundledLocalization.entries)) {
    if (key !== `${entry.pluginId}/${entry.contributionId}/${entry.field}`) throw new Error(`Invalid content identity: ${key}`);
    const row = review.translations.find((row: Record<string,string>) => row.path === entry.sourcePath && row.key === entry.sourceKey);
    if (!row || row.sourceSha256 !== entry.sourceSha256 || row.originalText !== entry.originalText ||
      row.originalTextSha256 !== entry.originalTextSha256 || await sha256(new TextEncoder().encode(entry.originalText)) !== entry.originalTextSha256)
      throw new Error(`Stale content translation: ${key}`);
  }
  for (const [id, locale] of Object.entries(bundledLocalization.locales)) {
    if (canonicalLocale(id) !== id) throw new Error(`Invalid content locale: ${id}`);
    for (const [key, entry] of Object.entries(locale.entries)) {
      const original = bundledLocalization.entries[key];
      if (!original || !entry.text.trim() || !['reviewed-with-feature','draft'].includes(entry.status)) throw new Error(`Invalid content translation: ${id}/${key}`);
      if (JSON.stringify(parameters(entry.text)) !== JSON.stringify(parameters(original.originalText))) throw new Error(`Content parameters differ: ${key}`);
    }
  }
  console.log(`Localization checks passed: ${Object.keys(locales).length} UI locales, ${Object.keys(bundledLocalization.entries).length} source-bound content strings`);
}
if (import.meta.main) await checkLocales();
