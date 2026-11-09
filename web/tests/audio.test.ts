import assert from 'node:assert/strict';
import { bundledBells, bellCredit } from '../src/lib/bundled-audio.ts';
import type { RenderCatalog } from '../src/lib/content-recipes.ts';
import { createBrowserAudio } from '../src/lib/browser-audio.ts';
const event = (id: string, epoch = '1') => ({ type: 'soundEvent' as const, eventId: id, worldEpoch: epoch, kind: 'stationArrived' as const });
function fixture() {
  let plays = 0, paused = 0, fail = false, hidden = false;
  const media = { src: '', currentTime: 0, preload: '', play() { plays++; return fail ? Promise.reject(new Error('Blocked')) : Promise.resolve(); }, pause() { paused++; } };
  const audio = createBrowserAudio(() => media as unknown as HTMLAudioElement, () => hidden);
  return { audio, get plays() { return plays; }, get paused() { return paused; }, set fail(value: boolean) { fail = value; }, set hidden(value: boolean) { hidden = value; } };
}
Deno.test('audio waits for user enable and plays each current live event once', async () => {
  const f = fixture(); await f.audio.event(event('old'), '1'); assert.equal(f.plays, 0);
  assert.equal(await f.audio.enable(), true); assert.equal(f.plays, 1);
  await f.audio.event(event('old'), '1'); assert.equal(f.plays, 1);
  await f.audio.event(event('live'), '1'); await f.audio.event(event('live'), '1'); assert.equal(f.plays, 2);
  await f.audio.event(event('wrong', '2'), '1'); assert.equal(f.plays, 2);
  f.audio.dispose();
});
Deno.test('hidden, disabled and disposed audio never queues a replay backlog', async () => {
  const f = fixture(); await f.audio.enable(); f.hidden = true;
  await f.audio.event(event('hidden'), '1'); f.hidden = false; await f.audio.event(event('hidden'), '1'); assert.equal(f.plays, 1);
  f.audio.disable(); await f.audio.event(event('disabled'), '1'); await f.audio.enable();
  await f.audio.event(event('disabled'), '1'); assert.equal(f.plays, 2);
  f.audio.dispose(); await f.audio.event(event('disposed'), '1'); assert.equal(f.plays, 2); assert.equal(f.paused, 2);
});
Deno.test('playback rejection disables audio and permits a later user retry', async () => {
  const f = fixture(); f.fail = true; assert.equal(await f.audio.enable(), false); assert.equal(f.audio.enabled, false);
  f.fail = false; assert.equal(await f.audio.enable(), true);
  f.fail = true; await f.audio.event(event('fails'), '1'); assert.equal(f.audio.enabled, false);
  f.audio.dispose();
});

Deno.test('bell previews bind all six exact WAVs without depending on retained BGM assets', async () => {
  const catalog: RenderCatalog = JSON.parse(await Deno.readTextFile(new URL('../public/content/bundled-v2/catalog.json', import.meta.url)));
  const bells = bundledBells(catalog);
  assert.equal(bells.length, 6);
  assert.equal(new Set(bells.map(bell => bell.id)).size, 6);
  const withoutMusic = structuredClone(catalog);
  withoutMusic.assets = withoutMusic.assets?.filter(asset => asset.mediaType !== 'mid');
  assert.deepEqual(bundledBells(withoutMusic), bells);
  const missing = structuredClone(catalog); missing.assets = [];
  assert.throws(() => bundledBells(missing), /Missing bundled audio/);
});

Deno.test('audio credits romanize matching plugin authors and preserve source, Latin and distinct credits', async () => {
  const catalog: RenderCatalog = JSON.parse(await Deno.readTextFile(new URL('../public/content/bundled-v2/catalog.json', import.meta.url)));
  const bells = bundledBells(catalog);
  for (const bell of bells) {
    assert.equal(bellCredit(catalog, bell, 'en-US'), bell.pluginId === 'system' ? 'Kohsuke Kawaguchi' : '915');
    for (const locale of ['ja', 'ja-JP', 'sv-SE']) assert.equal(bellCredit(catalog, bell, locale), bell.author);
    assert.equal(bellCredit({...catalog, packId:'other'}, bell, 'en'), bell.author);
    assert.equal(bellCredit({...catalog, source:{revision:'changed'}}, bell, 'en'), bell.author);
    for (const author of ['Latin Author', '別の作者', '']) {
      assert.equal(bellCredit(catalog, {...bell, author}, 'en'), author);
      assert.equal(bellCredit(catalog, {...bell, author}, 'ja'), author);
    }
  }
});
