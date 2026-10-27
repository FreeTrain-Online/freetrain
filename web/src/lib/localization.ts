import en from '../locales/en/app.json' with { type: 'json' };
import ja from '../locales/ja/app.json' with { type: 'json' };
import enFormats from '../locales/en/formats.json' with { type: 'json' };
import jaFormats from '../locales/ja/formats.json' with { type: 'json' };
import enGallery from '../locales/en/gallery.json' with { type: 'json' };
import jaGallery from '../locales/ja/gallery.json' with { type: 'json' };
import { matchLocale } from './locale-matching.ts';

export { appName } from './app-identity.ts';
export type PluralMessages = Partial<Record<Intl.LDMLPluralRule, string>> & { other: string };
export type Messages = { [K in keyof typeof en]: string };
export interface LocaleDefinition {
  label: string;
  direction: 'ltr' | 'rtl';
  messages: Messages;
  gallery: { [K in keyof typeof enGallery]: string };
  formats: { weekdays: string[]; gameTime: string; frameCount: PluralMessages };
}
// Register a language here once. Selectors, negotiation and document direction
// all consume this registry; translations live in their own language directory.
export const locales = {
  en: { label: 'English', direction: 'ltr', messages: en, formats: enFormats, gallery: enGallery },
  ja: { label: '日本語', direction: 'ltr', messages: ja, formats: jaFormats, gallery: jaGallery },
} satisfies Record<string, LocaleDefinition>;
export type Locale = keyof typeof locales;
export const defaultLocale: Locale = 'en';
export const messages = Object.fromEntries(Object.entries(locales).map(([id, definition]) => [id, definition.messages])) as Record<Locale, Messages>;

export function resolveLocale(value: string | null | undefined): Locale {
  return (matchLocale(value, Object.keys(locales)) as Locale | undefined) ?? defaultLocale;
}
export function negotiateLocale(values: readonly string[]): Locale {
  for (const value of values) {
    const match = matchLocale(value, Object.keys(locales));
    if (match) return match as Locale;
  }
  return defaultLocale;
}
export function initialLocale(): Locale {
  try {
    const match = matchLocale(localStorage.getItem('freetrain-online.locale'), Object.keys(locales));
    if (match) return match as Locale;
  } catch { /* A blocked storage setting must not prevent startup. */ }
  return negotiateLocale(navigator.languages?.length ? navigator.languages : [navigator.language]);
}
export function persistLocale(locale: Locale): void {
  document.documentElement.lang = locale;
  document.documentElement.dir = locales[locale].direction;
  try { localStorage.setItem('freetrain-online.locale', locale); } catch { /* Preference is optional. */ }
}
/** Plain text only: placeholders cannot introduce HTML or be substituted twice. */
export function interpolate(template: string, values: Record<string, string | number | bigint>): string {
  return template.replace(/\{([a-zA-Z][a-zA-Z0-9]*)\}/g, (_, key: string) => {
    if (!Object.hasOwn(values, key)) throw new Error(`Missing translation parameter: ${key}`);
    return String(values[key]);
  });
}
/** Each language provides the plural categories it needs, always including
 * 'other'. Never assemble translated noun fragments around English grammar. */
export function pluralMessage(locale: string, forms: PluralMessages, count: number): string {
  const rule = new Intl.PluralRules(locale).select(count);
  return interpolate(forms[rule] ?? forms.other, { count: new Intl.NumberFormat(locale).format(count) });
}
