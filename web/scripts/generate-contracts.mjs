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

// Exact reviewed railway bindings, consumed by untrusted snapshot validation.
const railway={ruleset:'mixed-railway-1',contentVersion:'mixed-railway-content-1',requiredPacks:terrain.requiredPacks,
  trafficOwner:definition('system:traffic','geometry:traffic-components-1'),slopeOwner:definition('system:slope','geometry:eight-cell-slope-1'),bindings:[]};
const add=(id,handler,variant=-1)=>railway.bindings.push({reference:definition(id),handler,variant});
for(let i=0;i<60;i++)add(`system:pattern/${i}`,i<12?'normal':i<28?'junction':'slope',i);
add('system:pier','pier',0);
for(let d=0;d<8;d+=2)for(let i=2;i<4;i++){const p=d/2*4+i;add(`system:pattern/slope-support-${p}`,'slopesupport',d+i-2);add(`system:pattern/underground-slope-support-${p}`,'slopewall',d+i-2);}
for(const [id,kind] of [['{874B8BFD-1EF4-4828-9C5A-D0EA1049D28D}','bridge'],['{7FD493FF-D6A7-4a13-A471-FB737641D8A0}','girder'],['{3E66FC7A-2B58-4182-8A8E-2D5A01BE78E5}','tunnel'],['{93481807-D998-46d7-B1BE-CB2B5D086D67}','garage']])add(id,kind);
for(const r of catalog.renderRecipes.filter(r=>['electric-pole','signal'].includes(r.handler)))add(r.id,r.handler==='signal'?'signal':'pole');
const railwayOutput=new URL('../src/generated/railway-profile.json',import.meta.url),railwayJson=JSON.stringify(railway,null,2)+'\n';
if(process.argv.includes('--check')){if(await readFile(railwayOutput,'utf8')!==railwayJson)throw new Error('Generated railway profile is stale.');}else await writeFile(railwayOutput,railwayJson);
