import { loadRenderCatalog } from './content-recipes.ts';

let pending: ReturnType<typeof loadRenderCatalog> | undefined;

// Catalog previews and source attribution share the same verified base catalog.
export function bundledCatalog(): ReturnType<typeof loadRenderCatalog> {
  return pending ??= loadRenderCatalog().catch(error => { pending = undefined; throw error; });
}
