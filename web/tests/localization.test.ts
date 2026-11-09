import assert from 'node:assert/strict';
import { locales, resolveLocale, negotiateLocale, interpolate, pluralMessage } from '../src/lib/localization.ts';
import { matchLocale } from '../src/lib/locale-matching.ts';
import { checkLocales, validateMessages } from '../scripts/check-locales.ts';
import { bundledLocalization, contentText } from '../src/lib/content-localization.ts';
import { bundledPluginLocalization, pluginText } from '../src/lib/plugin-localization.ts';
import { objectOrigin } from '../src/lib/object-provenance.ts';
import type { RenderCatalog } from '../src/lib/content-recipes.ts';

Deno.test('registered UI locales and reviewed content strings retain complete source bindings', checkLocales);
Deno.test('BCP 47 lookup handles regions, scripts, extensions, invalid tags and ordered browser preferences', () => {
  assert.equal(resolveLocale('JA-jp'), 'ja');
  assert.equal(resolveLocale('en-US-u-nu-latn'), 'en');
  assert.equal(resolveLocale('sv-SE'), 'en');
  assert.equal(negotiateLocale(['sv-SE','ja-JP','en']), 'ja');
  assert.equal(matchLocale('zh-Hant-TW', ['zh','zh-Hant','zh-Hans']), 'zh-Hant');
  assert.equal(matchLocale('pt-PT', ['pt-BR']), undefined);
  for (const tag of ['__proto__','constructor','en_uk','',null]) assert.equal(resolveLocale(tag), 'en');
  for (const id of Object.keys(locales)) assert.equal(resolveLocale(id), id);
});
Deno.test('catalog validation rejects missing/unknown keys, empty wording and changed interpolation', () => {
  for (const bad of [{}, {hello:''}, {hello:'Hi',extra:'Unexpected'}, {hello:'Hello {person}'}]) {
    assert.throws(() => validateMessages({hello:'Hello {name}'}, bad, 'fixture'));
  }
  validateMessages({hello:'{name}: {count}'}, {hello:'{count} — {name}'}, 'fixture');
  assert.equal(interpolate('{name}: {count}', {name:'{count}<b>',count:2n}), '{count}<b>: 2');
  assert.throws(() => interpolate('{missing}', {}));
});
Deno.test('plural rules accommodate languages with more categories than English', () => {
  assert.equal(pluralMessage('en',locales.en.formats.frameCount,1),'1 frame');
  assert.equal(pluralMessage('en',locales.en.formats.frameCount,2),'2 frames');
  assert.equal(pluralMessage('ja',locales.ja.formats.frameCount,1),'1コマ');
  const forms={one:'one {count}',few:'few {count}',many:'many {count}',other:'other {count}'};
  assert.equal(pluralMessage('pl',forms,2),'few 2');assert.equal(pluralMessage('pl',forms,5),'many 5');
});
Deno.test('pack-local presentation falls back for missing languages, changed identities, source text and drafts', () => {
  const entry = Object.values(bundledLocalization.entries).find(e => e.originalText === '開拓')!;
  const pack = {packId:bundledLocalization.packId, source:{revision:bundledLocalization.sourceRevision}};
  const target = {pluginId:entry.pluginId,id:entry.contributionId};
  const translated = (locale:string, original=entry.originalText) => contentText(bundledLocalization,pack,target,'name',original,locale);
  assert.equal(translated('en-GB'), 'Pioneering');
  assert.equal(translated('ja-JP'), '開拓'); assert.equal(translated('sv-SE'), '開拓');
  assert.equal(translated('en','変更'), '変更');
  assert.equal(contentText(bundledLocalization,{...pack,packId:'optional'},target,'name','開拓','en'), '開拓');
  assert.equal(contentText(bundledLocalization,{...pack,source:{revision:'changed'}},target,'name','開拓','en'), '開拓');
  const draft=structuredClone(bundledLocalization);
  draft.locales.en.entries[`${target.pluginId}/${target.id}/name`].status='draft';
  assert.equal(contentText(draft,pack,target,'name','開拓','en'),'開拓');
});

Deno.test('plugin titles and authors localize by owning plugin, retaining Japanese and Latin names', async () => {
  const catalog: RenderCatalog = JSON.parse(await Deno.readTextFile(new URL('../public/content/bundled-v2/catalog.json', import.meta.url)));
  // The review also retains excluded plugins; every prepared plugin is covered.
  assert.ok(catalog.plugins!.length > 0);
  for (const plugin of catalog.plugins!) {
    const entry = bundledPluginLocalization.entries[plugin.id];
    for (const field of ['title', 'author'] as const) {
      assert.equal(pluginText(catalog, plugin, field, 'en-US'), entry[field].text);
      for (const locale of ['ja', 'ja-JP', 'sv-SE', 'invalid_tag'])
        assert.equal(pluginText(catalog, plugin, field, locale), plugin[field]);
    }
  }
  const signal = catalog.contributions!.find(item => item.pluginId === 'org.kohsuke.freetrain.rail.signal')!;
  assert.deepEqual(objectOrigin(catalog, signal.id, 'en'), {author:'Kohsuke Kawaguchi', plugin:'Railway Signals'});
  assert.deepEqual(objectOrigin(catalog, signal.id, 'ja'), {author:'川口耕介', plugin:'鉄道用信号機'});
  // A generated recipe without a direct declaration still follows its owner.
  const generated = catalog.renderRecipes.find(item => !catalog.contributions!.some(c => c.id === item.id))!;
  const owner = catalog.plugins!.find(plugin => plugin.id === generated.pluginId)!;
  assert.deepEqual(objectOrigin(catalog, generated.id, 'en'), {
    author: pluginText(catalog, owner, 'author', 'en')?.trim() || undefined,
    plugin: pluginText(catalog, owner, 'title', 'en')?.trim() || undefined,
  });
  const latin = catalog.plugins!.find(plugin => plugin.author === 'romulanWarbird')!;
  assert.equal(pluginText(catalog, latin, 'author', 'en'), 'romulanWarbird');
  assert.equal(pluginText(catalog, latin, 'author', 'ja'), 'romulanWarbird');
  assert.equal(pluginText(catalog, catalog.plugins!.find(plugin => plugin.id === 'org.kohsuke.freetrain.music')!, 'author', 'en'), '');
  assert.equal(objectOrigin(catalog, 'missing', 'en'), undefined);
});

Deno.test('plugin translations fall back on foreign packs, revision drift, unknown plugins and changed fields', () => {
  const pack = {packId:bundledPluginLocalization.packId, source:{revision:bundledPluginLocalization.sourceRevision}};
  const plugin = {id:'system', title:'システム', author:'川口耕介'};
  assert.equal(pluginText(pack, plugin, 'title', 'en'), 'System');
  assert.equal(pluginText({...pack, packId:'optional'}, plugin, 'title', 'en'), plugin.title);
  assert.equal(pluginText({...pack, source:{revision:'changed'}}, plugin, 'title', 'en'), plugin.title);
  assert.equal(pluginText(pack, {...plugin, id:'unknown'}, 'author', 'en'), plugin.author);
  assert.equal(pluginText(pack, {...plugin, author:'別の作者'}, 'author', 'en'), '別の作者');
  assert.equal(pluginText(pack, {...plugin, title:'変更'}, 'title', 'en'), '変更');
  assert.equal(pluginText(pack, {...plugin, author:null}, 'author', 'en'), undefined);
});
