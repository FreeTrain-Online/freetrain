<script lang="ts">
  import { onMount } from 'svelte';
  import { messages, type Locale } from './lib/localization.ts';
  import { loadRenderCatalog, sha256, type RenderCatalog } from './lib/content-recipes.ts';
  import { bundledAudio, type BundledAudio } from './lib/bundled-audio.ts';
  import { bundledLocalization, contentText } from './lib/content-localization.ts';
  import { loadMidiSynth, type createMidiPlayer, type PlaybackState } from './lib/midi-player.ts';
  let { locale, player, playback, selected = $bindable('') }: {
    locale: Locale; player: ReturnType<typeof createMidiPlayer>; playback: PlaybackState; selected?: string;
  } = $props();
  const text = $derived(messages[locale]);
  let catalog = $state<RenderCatalog>(), items = $state<BundledAudio[]>([]), loaded = $state(false), failed = $state(false);
  let bell = $state(''), bellFailed = $state(false);
  let bellContext: AudioContext | undefined, bellGeneration = 0;
  let active = false;
  const music = $derived(items.filter(item => item.kind === 'music'));
  const bells = $derived(items.filter(item => item.kind === 'bell'));
  const title = (item: BundledAudio) => catalog ? contentText(bundledLocalization, catalog, item, 'name', item.name, locale) : item.name;
  function stopBell() { bellGeneration++; if (bellContext) void bellContext.close(); bellContext = undefined; }
  async function previewBell() {
    stopBell(); bellFailed = false;
    const current = bellGeneration, item = bells.find(item => item.id === bell); if (!item) return;
    try {
      const context = bellContext = new AudioContext(); const resumed = context.resume();
      const response = await fetch(item.media.url); if (!response.ok) throw new Error('Bell unavailable');
      const bytes = new Uint8Array(await response.arrayBuffer());
      if (bytes.length !== item.media.bytes || await sha256(bytes) !== item.media.sha256) throw new Error('Bell hash mismatch');
      const buffer = await context.decodeAudioData(bytes.buffer); await resumed;
      if (current !== bellGeneration) return;
      const sound = context.createBufferSource(); sound.buffer = buffer; sound.connect(context.destination);
      sound.onended = () => { if (current === bellGeneration) stopBell(); }; sound.start();
    } catch { if (current === bellGeneration) { stopBell(); bellFailed = true; } }
  }
  function load() {
    failed = false;
    Promise.all([loadRenderCatalog(), loadMidiSynth()]).then(([value]) => {
      if (!active) return;
      catalog = value; items = bundledAudio(value); loaded = true;
      if (!selected) selected = items.find(item => item.kind === 'music')?.id ?? '';
      bell = items.find(item => item.kind === 'bell')?.id ?? '';
    }).catch(() => { if (active) failed = true; });
  }
  onMount(() => {
    active = true; load();
    const hidden = () => { if (document.hidden) stopBell(); };
    document.addEventListener('visibilitychange', hidden);
    return () => { active = false; stopBell(); document.removeEventListener('visibilitychange', hidden); };
  });
</script>

<fieldset>
  <legend>{text.music}</legend>
  <p>{text.musicHint}</p>
  <label class="field">{text.music}<select aria-label={text.music} value={selected} disabled={!loaded} onchange={event => { player.stop(); selected = event.currentTarget.value; }}>
    {#each music as item (item.id)}<option value={item.id}>{title(item)}</option>{/each}
  </select></label>
  <div class="form-actions">
    <button disabled={!loaded || !selected} onclick={() => { const item = music.find(item => item.id === selected); if (item) void player.play(item.media); }}>{text.playMusic}</button>
    <button disabled={playback !== 'loading' && playback !== 'playing'} onclick={player.stop}>{text.stopMusic}</button>
  </div>
  <p role="status" data-testid="music-status" data-state={playback}>{failed || playback === 'failed' ? text.musicFailed : playback === 'loading' ? text.musicLoading : playback === 'playing' ? text.musicPlaying : text.musicStopped}</p>
  {#if failed}<button onclick={load}>{text.retryNow}</button>{/if}
  {#each music.filter(item => item.id === selected) as item}<p>{text.audioCredit}: {item.author}</p>{/each}
  <p><a href="https://github.com/g200kg/webaudio-tinysynth" target="_blank" rel="noopener noreferrer">{text.midiSynthesis}</a></p>
  <label class="field">{text.departureBells}<select aria-label={text.departureBells} bind:value={bell} disabled={!loaded} onchange={stopBell}>
    {#each bells as item (item.id)}<option value={item.id}>{title(item)}</option>{/each}
  </select></label>
  <button disabled={!loaded || !bell} onclick={previewBell}>{text.previewBell}</button>
  {#if bellFailed}<p role="status">{text.soundFailed}</p>{/if}
</fieldset>
