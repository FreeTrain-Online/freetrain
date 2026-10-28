import { compile } from 'json-schema-to-typescript';
import { readFile, writeFile } from 'node:fs/promises';
const schema = JSON.parse(await readFile(new URL('../../prototype/protocol/schema.json', import.meta.url), 'utf8'));
const output = new URL('../src/generated/contracts.ts', import.meta.url);
const generated = await compile(schema, 'GameDocument', {
  bannerComment: '/* Generated from prototype/protocol/schema.json. Run deno task generate:contracts. */',
  unreachableDefinitions: true,
});
if (process.argv.includes('--check')) {
  if (await readFile(output, 'utf8') !== generated) throw new Error('Generated contracts are stale. Run deno task generate:contracts.');
} else await writeFile(output, generated);

// Compact, exact terrain handler metadata. The full art catalog remains lazily loaded.
const catalogBytes = await readFile(new URL('../../src/FreeTrain.Online.Content/bundled-v2.json', import.meta.url));
const catalog = JSON.parse(catalogBytes.toString('utf8'));
const { createHash } = await import('node:crypto');
const packId = catalog.packId;
const definition = (id, recipeId = id) => ({ packId, contributionId: id, recipeId });
const terrain = {
  ruleset: 'terrain-state-1', contentVersion: 'terrain-content-1',
  requiredPacks: [{id:packId, version:catalog.version, catalogHash:createHash('sha256').update(catalogBytes).digest('hex')}],
  definitions: [{reference:definition('system:terrain/mountain','geometry:quarter-mountain-1'),kind:'mountain',treeSpriteCount:0,treeDensity:0,price:0},
    ...catalog.renderRecipes.filter(r=>['forest','terrace','creek'].includes(r.handler)).map(r=>({
      reference:definition(r.id),kind:r.handler==='forest'?'mountain':r.handler,
      treeSpriteCount:r.handler==='forest'?Number(r.fields.count):0,treeDensity:r.handler==='forest'?Number(r.fields.density):0,
      price:r.handler==='forest'?Number(r.fields.price):0,
    }))],
};
const terrainOutput = new URL('../src/generated/terrain-profile.json', import.meta.url);
const terrainJson = JSON.stringify(terrain,null,2)+'\n';
if(process.argv.includes('--check')) {
  if(await readFile(terrainOutput,'utf8')!==terrainJson)throw new Error('Generated terrain profile is stale.');
} else await writeFile(terrainOutput,terrainJson);
