export const schemes = ['evergreen', 'classic-blue', 'slate', 'plum'] as const;
export type Scheme = typeof schemes[number];
export const schemeKey = 'freetrain-online.ui.v1';
export function resolveScheme(value: unknown): Scheme {
  return schemes.includes(value as Scheme) ? value as Scheme : 'evergreen';
}
export function initialScheme(): Scheme {
  try {
    const saved = JSON.parse(localStorage.getItem(schemeKey) ?? 'null');
    return saved?.version === 1 ? resolveScheme(saved.scheme) : 'evergreen';
  } catch { return 'evergreen'; }
}
export function applyScheme(scheme: Scheme) {
  const safe = resolveScheme(scheme);
  document.documentElement.dataset.uiScheme = safe;
  try { localStorage.setItem(schemeKey, JSON.stringify({ version: 1, scheme: safe })); } catch { /* Optional preference. */ }
}
