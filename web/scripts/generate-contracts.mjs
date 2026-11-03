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
add('system:pier/slopeNE','pier',1);
for(let d=0;d<8;d+=2)for(let i=2;i<4;i++){const p=d/2*4+i;add(`system:pattern/slope-support-${p}`,'slopesupport',d+i-2);add(`system:pattern/underground-slope-support-${p}`,'slopewall',d+i-2);}
for(const [id,kind] of [['{874B8BFD-1EF4-4828-9C5A-D0EA1049D28D}','bridge'],['{7FD493FF-D6A7-4a13-A471-FB737641D8A0}','girder'],['{3E66FC7A-2B58-4182-8A8E-2D5A01BE78E5}','tunnel'],['{93481807-D998-46d7-B1BE-CB2B5D086D67}','garage']])add(id,kind);
for(const r of catalog.renderRecipes.filter(r=>['electric-pole','signal'].includes(r.handler)))add(r.id,r.handler==='signal'?'signal':'pole');
for(let i=0;i<4;i++)railway.bindings.push({reference:definition(`system:bridge-pier/${i}`,'{874B8BFD-1EF4-4828-9C5A-D0EA1049D28D}'),handler:'pier',variant:i+2});
// Source-bound stationary dimensions, prices and independent variant identities.
const groups={'レンガ橋脚':'Brick pier','コン橋脚':'Concrete pier','レンガ橋脚(側道付)':'Brick pier with side path','コン橋脚(側道付)':'Concrete pier with side path','コン橋脚(複合)':'Composite concrete pier','信号所':'Signal box'};
const counts={};
const stationary=catalog.contributions.filter(c=>c.type==='railStationary').map(c=>{
  const recipe=catalog.renderRecipes.find(r=>r.id===c.id),f=recipe.fields;
  const group=c.declaration.children.find(c=>c.tag==='group').text;
  if(!groups[group]||recipe.status!=='ready'||recipe.frames.some(f=>!['ready','invisible'].includes(f.status)))throw new Error('Unavailable stationary recipe/translation');
  const variant=counts[group]=(counts[group]??0)+1;
  const type={sizeX:f.size[0],sizeY:f.size[1],height:Number(f.height),price:Number(f.price)};
  railway.bindings.push({reference:definition(c.id),handler:'stationary',variant:-1,stationary:type});
  return {id:c.id,pluginId:c.pluginId,groupJa:group,groupEn:groups[group],variant,...type};
});
for(const r of catalog.renderRecipes.filter(r=>r.id.startsWith('system:construction/')))add(r.id,'constructionart');
const stationaryOutput=new URL('../src/generated/stationary-types.json',import.meta.url),stationaryJson=JSON.stringify(stationary,null,2)+'\n';
if(process.argv.includes('--check')){if(await readFile(stationaryOutput,'utf8')!==stationaryJson)throw new Error('Generated stationary types are stale.');}else await writeFile(stationaryOutput,stationaryJson);
const railwayOutput=new URL('../src/generated/railway-profile.json',import.meta.url),railwayJson=JSON.stringify(railway,null,2)+'\n';
if(process.argv.includes('--check')){if(await readFile(railwayOutput,'utf8')!==railwayJson)throw new Error('Generated railway profile is stale.');}else await writeFile(railwayOutput,railwayJson);

// Exact source overlay surfaces and alpha masks, independent of metadata recipes.
// Our content PNG writer emits RGBA8 with filter 0; reject any other encoding.
const { inflateSync }=await import('node:zlib');
const speedIcons={};
for(const [index,speed] of ['Low','Medium','Fast'].entries()){
  const asset=catalog.assets.find(a=>a.path===`trunk/plugins/SpeedLimit/${speed}SpeedIcon.bmp`);
  if(asset?.status!=='converted-surface')throw new Error('Missing speed-limit icon');
  const png=await readFile(new URL('../public'+asset.day.url,import.meta.url));
  if(createHash('sha256').update(png).digest('hex')!==asset.day.sha256||png[24]!==8||png[25]!==6)throw new Error('Invalid speed icon PNG');
  const chunks=[];
  for(let pos=8;pos<png.length;){const n=png.readUInt32BE(pos),kind=png.toString('ascii',pos+4,pos+8);if(kind==='IDAT')chunks.push(png.subarray(pos+8,pos+8+n));pos+=12+n;}
  const raw=inflateSync(Buffer.concat(chunks)),stride=asset.width*4+1;
  if(raw.length!==asset.height*stride)throw new Error('Invalid speed icon rows');
  const mask=Array.from({length:asset.height},(_,y)=>{
    if(raw[y*stride]!==0)throw new Error('Unexpected speed icon filter');
    return Array.from({length:asset.width},(_,x)=>raw[y*stride+1+x*4+3]?'1':'0').join('');
  });
  const id=`{CFC7C99B-4EDE-493B-9DD1-26863390290${index+1}}`;
  speedIcons[speed.toLowerCase()]={id,image:asset.day,width:asset.width,height:asset.height,mask};
}
const speedOutput=new URL('../src/generated/speed-limit-icons.json',import.meta.url),speedJson=JSON.stringify(speedIcons,null,2)+'\n';
if(process.argv.includes('--check')){if(await readFile(speedOutput,'utf8')!==speedJson)throw new Error('Generated speed-limit icons are stale.');}else await writeFile(speedOutput,speedJson);

// Data-only stock identities for untrusted fleet reads; no pack activation.
const child=(e,tag)=>e.children.find(c=>c.tag===tag);
const carDefinitions=catalog.contributions.filter(c=>c.type==='trainCar');
const capacity=(id,seen=new Set())=>{
  if(seen.has(id))throw new Error('Cyclic car');seen.add(id);
  const c=carDefinitions.find(c=>c.id===id),e=c.declaration;
  if(c.implementation.name==='freetrain.contributions.train.ReverseTrainCarImpl')return capacity(child(e,'base').attributes.carRef,seen);
  return c.implementation.name==='freetrain.world.rail.cttrain.ColorTestTrainCar'?[0,0]:[Number(child(e,'capacity').text),Number(child(e,'seatedcapacity').text)];
};
const stockBinding=id=>({packId,version:catalog.version,catalogHash:terrain.requiredPacks[0].catalogHash,contributionId:id});
const fleet={requiredPacks:terrain.requiredPacks,cars:carDefinitions.map(c=>({reference:definition(c.id),normal:capacity(c.id)[0],seated:capacity(c.id)[1]})),
 trains:catalog.contributions.filter(c=>c.type==='train').map(c=>{
  const e=c.declaration,comp=child(e,'composition'),config=child(e,'config');
  let head=null,body=null,tail=null,patterns=[];
  if(comp){head=child(comp,'head')?.attributes.carRef??null;body=child(comp,'body')?.attributes.carRef??null;tail=child(comp,'tail')?.attributes.carRef??null;}
  else {
   const chars=Object.fromEntries(config.children.filter(c=>c.tag==='car').map(c=>[c.attributes.char,c.attributes.ref]));
   patterns=config.children.filter(c=>c.tag==='composition').map(c=>[...c.text.replace(/\s/gu,'')].map(c=>chars[c]));
  }
  return {binding:stockBinding(c.id),pricePerCar:Number(child(e,'price').text),head,body,tail,patterns};
 }),providers:catalog.contributions.filter(c=>c.type==='trainController').map(c=>({binding:stockBinding(c.id),
  kind:c.implementation.name==='freetrain.world.rail.manualtc.TrainConrollerContributionImpl'?'manual':'timetable'}))};
const fleetOutput=new URL('../src/generated/fleet-profile.json',import.meta.url),fleetJson=JSON.stringify(fleet,null,2)+'\n';
if(process.argv.includes('--check')){if(await readFile(fleetOutput,'utf8')!==fleetJson)throw new Error('Generated fleet profile is stale.');}else await writeFile(fleetOutput,fleetJson);
