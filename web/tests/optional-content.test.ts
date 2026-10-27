import { creditFor, loadEnabledContent, type PluginPack } from '../src/lib/optional-content.ts';
import { sha256 } from '../src/lib/content-recipes.ts';
function assert(value:unknown):asserts value {if(!value)throw new Error('Assertion failed');}
const hex='a'.repeat(64),url=`/api/content/synthetic/${hex}/catalog.json`;
const catalog={schemaVersion:2,packId:'synthetic',version:'1',source:{revision:'synthetic'},renderRecipes:[],surfaceSets:{},masks:{}};
const bytes=new TextEncoder().encode(JSON.stringify(catalog));
async function pack():Promise<PluginPack>{return {id:'synthetic',version:'1',hash:hex,catalogHash:await sha256(bytes),catalogUrl:url,profile:'synthetic',sourceRevision:'synthetic',
  credits:{defaultLocale:'ja',locales:{en:{button:'Credits',title:'Credits',text:'Original fixture'},ja:{button:'謝辞',title:'謝辞',text:'検証用'}}},notices:[]};}
Deno.test('enabled catalogs are hash-verified and credits follow the selected locale with pack fallback',async()=>{
  const p=await pack(),original=globalThis.fetch;
  globalThis.fetch=async input=>new Response(String(input)==='/api/content'?JSON.stringify({setHash:hex,packs:[p]}):bytes);
  try{const c=await loadEnabledContent(hex,new AbortController().signal);assert(c.packs.length===1);assert(creditFor(c.packs[0],'en').text==='Original fixture');assert(creditFor(c.packs[0],'fr').text==='検証用');}
  finally{globalThis.fetch=original;}
});
Deno.test('descriptor drift, stale catalog bytes, foreign URLs and duplicate identities reject the complete set',async()=>{
  for(const mutation of ['descriptor','bytes','url','duplicate','credits']){
    const p=await pack(),original=globalThis.fetch;
    const descriptor={setHash:mutation==='descriptor'?'b'.repeat(64):hex,packs:mutation==='duplicate'?[p,p]:[p]};
    if(mutation==='url')p.catalogUrl='https://example.org/catalog.json';
    if(mutation==='credits')delete p.credits.locales.ja;
    globalThis.fetch=async input=>new Response(String(input)==='/api/content'?JSON.stringify(descriptor):mutation==='bytes'?'{}':bytes);
    let rejected=false;
    try{await loadEnabledContent(hex,new AbortController().signal);}catch{rejected=true;}finally{globalThis.fetch=original;}
    assert(rejected);
  }
});
Deno.test('base-only descriptor exposes no optional credits or catalog requests',async()=>{
  const original=globalThis.fetch;let calls=0;
  globalThis.fetch=async()=>{calls++;return new Response(JSON.stringify({setHash:hex,packs:[]}));};
  try{const c=await loadEnabledContent(hex,new AbortController().signal);assert(c.packs.length===0&&calls===1);}finally{globalThis.fetch=original;}
});
