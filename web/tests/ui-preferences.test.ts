import { resolveScheme, schemes } from '../src/lib/ui-preferences.ts';
Deno.test('UI scheme IDs allow only the curated presets', () => {
  for (const id of schemes) if (resolveScheme(id) !== id) throw new Error(`Lost preset ${id}`);
  for (const value of [null, undefined, '', 'unknown', '__proto__', 'PLUM', {}, 1]) {
    if (resolveScheme(value) !== 'evergreen') throw new Error('Unsafe scheme accepted');
  }
});
