import { stationDefinitions, stationCatalog } from './station-building-edit.ts';
import type { RenderCatalog } from './content-recipes.ts';
import stationaryTypes from '../generated/stationary-types.json' with { type: 'json' };
import { messages, type Locale } from './localization.ts';

// Add categories as their construction workflows become available. Entries use
// the same contribution identities as authoritative railway preview requests.
export const catalogCategories = [
  { id: 'signal', label: 'signalTask' },
  { id: 'stationary', label: 'stationaryTask' },
  { id: 'station', label: 'stationTask' },
] as const;
export type CatalogCategory = typeof catalogCategories[number]['id'];
export const signalTypes = [
  { id: '{5349DE2A-DF3E-49B4-87A0-973105B7E6A4}', label: 'signalThreeAspect' },
  { id: '{6FA55842-E693-414f-A120-0A3D0C613B21}', label: 'signalShunting' },
] as const;
export type SignalType = typeof signalTypes[number]['id'];
export const signalDirections = [
  { value: 0, label: 'signalNorth' }, { value: 2, label: 'signalEast' },
  { value: 4, label: 'signalSouth' }, { value: 6, label: 'signalWest' },
] as const;
export type SignalDirection = typeof signalDirections[number]['value'];
export const initialCatalogSelection = { signal: signalTypes[0].id, stationary: stationaryTypes[0].id, station: stationDefinitions[0].reference.recipeId };
export interface CatalogVariant {
  id: string;
  title: string;
  label: string;
  detail: string;
  price: number;
}
export interface CatalogItem {
  id: string;
  title: string;
  variants: CatalogVariant[];
}
export function catalogItems(category: CatalogCategory, locale: Locale, content?: RenderCatalog): CatalogItem[] {
  const text = messages[locale];
  if (category === 'signal') return signalTypes.map(type => ({ id: type.id, title: text[type.label],
    variants: [{ id: type.id, title: text[type.label], label: '', detail: text.signalDescription, price: 0 }] }));
  if (category === 'station') {
    const groups = new Map<string, CatalogItem>();
    for (const type of content ? stationCatalog(content, locale) : []) {
      // Authored orientations may swap X/Y. Keep their exact footprints while
      // grouping the same source family, plugin and construction economics.
      const footprint = [type.sizeX, type.sizeY].sort((a, b) => a - b);
      const key = JSON.stringify([type.pluginId, type.familyId, footprint, type.height, type.price, type.upkeep]);
      let group = groups.get(key);
      if (!group) { group = { id: key, title: type.familyTitle, variants: [] }; groups.set(key, group); }
      const label = (group.variants.length + 1).toLocaleString(locale);
      const name = [type.title, type.orientation].filter(Boolean).join(' · ');
      group.variants.push({ id: type.reference.recipeId, title: `${name} · ${label}`, label,
        detail: `${name} · ${type.description ? `${type.description} · ` : ''}${text.stationaryDimensions}: ${type.sizeX} × ${type.sizeY} × ${type.height} · ${text.stationUpkeep}: ¥${BigInt(type.upkeep).toLocaleString(locale)}`, price: Number(type.price) });
    }
    return [...groups.values()];
  }
  const groups = new Map<string, CatalogItem>();
  for (const type of stationaryTypes) {
    // Match the sister fork's family boundary: distinct plugins, footprints,
    // heights and prices remain separate. IDs and baked orientations stay exact.
    const key = JSON.stringify([type.pluginId, type.groupJa, type.sizeX, type.sizeY, type.height, type.price]);
    const title = locale === 'ja' ? type.groupJa : type.groupEn;
    let group = groups.get(key);
    if (!group) { group = { id: key, title, variants: [] }; groups.set(key, group); }
    group.variants.push({ id: type.id, title: `${title} · ${type.variant}`, label: type.variant.toLocaleString(locale),
      detail: `${text.stationaryDimensions}: ${type.sizeX} × ${type.sizeY} × ${type.height}`, price: type.price });
  }
  return [...groups.values()];
}
export function isCatalogItem(category: CatalogCategory, id: string): boolean {
  if (category === 'station') return stationDefinitions.some(type => type.reference.recipeId === id);
  return category === 'signal' ? signalTypes.some(type => type.id === id) : stationaryTypes.some(type => type.id === id);
}
