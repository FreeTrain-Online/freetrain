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

import { createDepartureEvents } from '../src/lib/departure-events.ts';
import type { DepartureBell } from '../src/generated/contracts.ts';
const liveEpoch = 'a'.repeat(32);
const baseline = {worldEpoch:'1',liveEpoch,publicationSequence:'10'};
const departure = (sequence='11', ordinal=0): DepartureBell => ({type:'departureBell',worldEpoch:'1',liveEpoch,
  publicationSequence:sequence,effectOrdinal:ordinal,trainId:'7',platformId:'8',
  bell:{packId:'exav-stable-bundled',contributionId:'selected',recipeId:'selected'},location:{x:27,y:15,z:2}});
Deno.test('departure subscription rejects history, duplicates and old epochs with constant-space ordered identities', () => {
  const events=createDepartureEvents();assert.equal(events.accept(departure(),'1'),false);events.reset(baseline);
  assert.equal(events.accept(departure('10'),'1'),false);
  assert.equal(events.accept(departure(),'1'),true);assert.equal(events.accept(departure(),'1'),false);
  assert.equal(events.accept(departure('11',1),'1'),true);
  for(let i=12;i<1000;i++)assert.equal(events.accept(departure(String(i)),'1'),true);
  assert.equal(events.accept(departure(),'1'),false);
  assert.equal(events.accept({...departure('1000'),liveEpoch:'b'.repeat(32)},'1'),false);
  events.reset({...baseline,publicationSequence:'999'});assert.equal(events.accept(departure('999'),'1'),false);
  events.reset({...baseline,worldEpoch:'2',liveEpoch:'b'.repeat(32)});
  assert.equal(events.accept(departure('1000'),'2'),false);
  assert.equal(events.accept({...departure(),worldEpoch:'2',liveEpoch:'b'.repeat(32)},'2'),true);
  events.reset();assert.equal(events.accept(departure('1001'),'1'),false);
});
Deno.test('departure WAV resolution is selected, gesture gated, visible and cancelled across async mute/replacement', async () => {
  const urls:string[]=[],resolved:string[]=[],verified:string[]=[];
  let hidden=false,visible=true,release:(()=>void)|undefined;
  const media={src:'',currentTime:0,preload:'',play(){urls.push(this.src);return Promise.resolve();},pause(){}};
  const audio=createBrowserAudio(()=>media as unknown as HTMLAudioElement,()=>hidden,()=>visible,
    async e=>{resolved.push(e.bell.contributionId);if(release===undefined)await new Promise<void>(r=>{release=r;});return {url:'/selected.wav',bytes:42,sha256:'hash'};},
    async clip=>{verified.push(clip.url);});
  await audio.event(departure(),'1');assert.deepEqual(resolved,[]);
  await audio.enable();hidden=true;await audio.event(departure(),'1');hidden=false;
  visible=false;await audio.event(departure(),'1');visible=true;
  const pending=audio.event(departure(),'1');await Promise.resolve();audio.cancel();release!();await pending;
  assert.deepEqual(urls,['/assets/station.wav']);
  await audio.event(departure('12'),'1');assert.deepEqual(urls,['/assets/station.wav','/selected.wav']);
  assert.deepEqual(resolved,['selected','selected']);assert.equal(verified.length,2);
  audio.disable();await audio.event(departure('13'),'1');assert.equal(urls.length,2);
  audio.dispose();await audio.enable();assert.equal(audio.enabled,false);
});

import { parseMessage } from '../src/lib/protocol.ts';
Deno.test('departure envelopes validate exact counters, epoch, ordinal, selected definition and source location', () => {
  assert.deepEqual(parseMessage(JSON.stringify(departure())),departure());
  for(const patch of [{publicationSequence:11},{publicationSequence:'01'},{liveEpoch:'old'},
    {effectOrdinal:-1},{effectOrdinal:4096},{bell:{packId:'x',contributionId:'y'}},{location:{x:1.5,y:15,z:2}},
    {eventId:'invented'}, {trainId:'9223372036854775808'}])assert.throws(()=>parseMessage(JSON.stringify({...departure(),...patch})));
});
