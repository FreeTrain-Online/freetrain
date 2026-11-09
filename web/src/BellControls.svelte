<script lang="ts">
  import { onMount } from 'svelte';
  import { messages, type Locale } from './lib/localization.ts';
  import { loadRenderCatalog, sha256, type RenderCatalog } from './lib/content-recipes.ts';
  import { bundledBells, bellCredit, type BundledBell } from './lib/bundled-audio.ts';
  import { bundledLocalization, contentText } from './lib/content-localization.ts';
  let { locale }: { locale: Locale } = $props();
  const text = $derived(messages[locale]);
  let catalog = $state<RenderCatalog>(), bells = $state<BundledBell[]>([]), loaded = $state(false), failed = $state(false);
  let bell = $state(''), bellFailed = $state(false);
  let bellContext: AudioContext | undefined, bellGeneration = 0;
  let active = false;
  const title = (item: BundledBell) => catalog ? contentText(bundledLocalization, catalog, item, 'name', item.name, locale) : item.name;
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
    loadRenderCatalog().then(value => {
      if (!active) return;
      catalog = value; bells = bundledBells(value); loaded = true;
      bell = bells[0]?.id ?? '';
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
  <legend>{text.departureBells}</legend>
  {#if failed}<p role="status">{text.soundFailed}</p><button onclick={load}>{text.retryNow}</button>{/if}
  <label class="field">{text.departureBells}<select aria-label={text.departureBells} bind:value={bell} disabled={!loaded} onchange={stopBell}>
    {#each bells as item (item.id)}<option value={item.id}>{title(item)}</option>{/each}
  </select></label>
  <button disabled={!loaded || !bell} onclick={previewBell}>{text.previewBell}</button>
  {#each bells.filter(item => item.id === bell) as item}<p data-testid="audio-credit">{text.audioCredit}: {catalog ? bellCredit(catalog, item, locale) : item.author}</p>{/each}
  {#if bellFailed}<p role="status">{text.soundFailed}</p>{/if}
</fieldset>
