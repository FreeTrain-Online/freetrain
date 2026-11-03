import assert from 'node:assert/strict';
import { instantiate, type RenderCatalog } from '../src/lib/content-recipes.ts';

// Source-derived P5-02 crops, independent of prepared recipe output. World pose
// offsets are retained for P5-09; these tests exercise the delivered P2 API only.
const fixtures = JSON.parse(await Deno.readTextFile(new URL('../../doc/parity/train-fixtures.json', import.meta.url)));
const catalog = JSON.parse(await Deno.readTextFile(new URL('../public/content/bundled-v2/catalog.json', import.meta.url))) as RenderCatalog;
for (const kind of ['level', 'slope']) {
  Deno.test(`P5 independent ${kind} frames match exact bundled car recipes including reversed formations and tail wrappers`, () => {
    const rows = fixtures.groups.rendering.cases.filter((c: {input: {kind: string}}) => c.input.kind === kind);
    assert.equal(rows.length, kind === 'level' ? 192 : 256);
    for (const row of rows) {
      const {input: i, expected: e} = row;
      const recipe = catalog.renderRecipes.find(r => r.id === i.recipe)!;
      assert(recipe, row.id);
      const before = JSON.stringify(recipe);
      const layers = instantiate(recipe, 'A', [20, 20, 2], kind === 'level' ? {angle: e.selector} : {slope: e.selector});
      assert.equal(layers.length, 1, row.id);
      assert.deepEqual(layers[0].frame.rectangle, e.rectangle, row.id);
      assert.deepEqual(layers[0].frame.anchor, [0, 0], row.id);
      assert.equal(JSON.stringify(recipe), before, row.id);
    }
  });
}
