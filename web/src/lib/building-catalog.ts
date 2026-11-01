import stationaryTypes from '../generated/stationary-types.json' with { type: 'json' };
import { messages, type Locale } from './localization.ts';

// Add categories as their construction workflows become available. Entries use
// the same contribution identities as authoritative railway preview requests.
export const catalogCategories = [
  { id: 'signal', label: 'signalTask' },
  { id: 'stationary', label: 'stationaryTask' },
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
export const initialCatalogSelection = { signal: signalTypes[0].id, stationary: stationaryTypes[0].id };
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
export function catalogItems(category: CatalogCategory, locale: Locale): CatalogItem[] {
  const text = messages[locale];
  if (category === 'signal') return signalTypes.map(type => ({ id: type.id, title: text[type.label],
    variants: [{ id: type.id, title: text[type.label], label: '', detail: text.signalDescription, price: 0 }] }));
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
  return category === 'signal' ? signalTypes.some(type => type.id === id) : stationaryTypes.some(type => type.id === id);
}
