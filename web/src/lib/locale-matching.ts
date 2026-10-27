/** BCP 47 lookup: exact tag, then progressively less specific parents. No
 * inference between scripts or unrelated regions. Invalid tags never throw. */
export function canonicalLocale(value: string | null | undefined): string | undefined {
  if (!value) return undefined;
  try { return Intl.getCanonicalLocales(value)[0]; } catch { return undefined; }
}
export function matchLocale(value: string | null | undefined, available: readonly string[]): string | undefined {
  let tag = canonicalLocale(value);
  const tags = new Map(available.map(id => [canonicalLocale(id)?.toLowerCase(), id]));
  while (tag) {
    const match = tags.get(tag.toLowerCase());
    if (match) return match;
    tag = tag.includes('-') ? tag.slice(0, tag.lastIndexOf('-')) : undefined;
    if (tag?.split('-').at(-1)?.length === 1) tag = tag.slice(0, tag.lastIndexOf('-'));
  }
  return undefined;
}
