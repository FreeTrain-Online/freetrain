<script lang="ts">
  import { onMount, tick } from 'svelte';
  import { messages, persistLocale, initialLocale, type Locale } from './lib/localization.ts';
  import { createShellModel, catalog } from './lib/shell-model.ts';
  import { createRemoteSessionClient, leaveGame } from './lib/remote-session-client.ts';
  import { createBrowserAudio } from './lib/browser-audio.ts';
  import SaveControls from './SaveControls.svelte';
  import MapView from './MapView.svelte';
  import PanelFrame from './PanelFrame.svelte';
  import SessionGate from './SessionGate.svelte';
  import type { Selection } from './lib/map-renderer.ts';
  import { panels, type Panel, type Tool } from './lib/game-ui.ts';
  const icon = '/assets/FTicon.png';
  const audio = createBrowserAudio();
  let soundEnabled = $state(false), soundFailed = $state(false), saveBusy = $state(false);
  const model = createShellModel(() => createRemoteSessionClient((event, epoch) => { void audio.event(event, epoch).then(() => { soundEnabled = audio.enabled; }); }), initialLocale());
  const dispose = () => { model.dispose(); audio.dispose(); };
  let view = $state(model.state);
  let mapSelection = $state<Selection>();
  let map: MapView;
  let panel = $state<Panel>(), menu = $state(false), tool = $state<Tool>('inspect');
  let renderState = $state<'loading' | 'ready' | 'failed'>('loading');
  let launcher: HTMLElement | undefined;
  let previousEpoch: string | undefined;
  const text = $derived(messages[view.locale]);
  const read = $derived(view.read);
  const live = $derived(view.connection === 'ready' && !!read && !view.leaving && !view.signedOut && renderState === 'ready');
  const train = $derived(mapSelection?.kind === 'train' ? read?.trains.find(item => item.id === mapSelection?.id) : undefined);
  const station = $derived(mapSelection?.kind === 'station' ? read?.stations.find(item => item.id === mapSelection?.id) : undefined);
  const building = $derived(mapSelection?.kind === 'building' ? read?.buildings.find(item => item.id === mapSelection?.id) : undefined);
  const track = $derived(mapSelection?.kind === 'track' ? read?.tracks.find(item => `${item.position.x},${item.position.y},${item.position.z}` === mapSelection?.id) : undefined);
  const mapObjectPosition = $derived(train?.position ?? mapSelection?.position);
  const disabled = $derived(saveBusy || view.pending || !live || view.role === 'observer');
  const selected = $derived(catalog.find(item => item.id === view.selectedBuilding)!);
  $effect(() => persistLocale(view.locale));
  $effect(() => { if (!live) { panel = undefined; menu = false; mapSelection = undefined; } });
  $effect(() => {
    if (read) {
      if (previousEpoch && previousEpoch !== read.worldEpoch) { if (panel === 'inspector') panel = undefined; menu = false; mapSelection = undefined; }
      previousEpoch = read.worldEpoch;
    }
  });
  function closePanel() { panel = undefined; menu = false; launcher?.focus({ preventScroll: true }); }
  function openPanel(next: Panel, source?: HTMLElement) {
    map?.resetInteraction(); menu = false;
    if (source) launcher = source;
    panel = panel === next ? undefined : next;
    if (!panel) launcher?.focus({ preventScroll: true });
  }
  function select(selection: Selection | undefined) {
    mapSelection = selection;
    if (selection?.kind === 'station') model.selectStation(selection.id);
    if (selection) { panel = 'inspector'; menu = false; }
  }
  function leave() { if (saveBusy) return; audio.disable(); soundEnabled = false; map?.resetInteraction(); void model.leave(leaveGame); }
  function escape(event: KeyboardEvent) {
    if (event.key !== 'Escape' || !live || event.isComposing || event.keyCode === 229) return;
    if (menu) { event.preventDefault(); menu = false; launcher?.focus({ preventScroll: true }); return; }
    if (event.target instanceof HTMLElement && event.target.matches('input,select,textarea,[contenteditable="true"]')) return;
    event.preventDefault();
    if (panel) closePanel(); else map?.escape();
  }
  function uiFocus(event: FocusEvent) { if (!(event.target instanceof HTMLCanvasElement)) map?.suspendInput(); }
  $effect(() => { if (live) { void tick().then(() => map?.focusMap()); } });
  onMount(() => {
    const unsubscribe = model.subscribe(next => { view = next; });
    const pagehide = (event: PageTransitionEvent) => { if (!event.persisted) dispose(); };
    const viewport = () => { document.documentElement.style.setProperty('--viewport-height', `${window.visualViewport?.height ?? window.innerHeight}px`); };
    viewport(); window.visualViewport?.addEventListener('resize', viewport);
    window.addEventListener('pagehide', pagehide); document.addEventListener('focusin', uiFocus);
    return () => { window.visualViewport?.removeEventListener('resize', viewport); window.removeEventListener('pagehide', pagehide); document.removeEventListener('focusin', uiFocus); unsubscribe(); dispose(); };
  });
</script>

<svelte:head><title>FreeTrain Online</title></svelte:head>
<svelte:window onkeydown={escape}/>
<div class="app-shell" class:gated={!live} data-shell="svelte">
  {#if live}
    <nav class="command-bar" aria-label={text.game}>
      <img class="app-icon" src={icon} width="32" height="32" alt="FreeTrain"/>
      <button aria-expanded={menu} aria-controls="game-menu" onclick={event => { map.resetInteraction(); launcher = event.currentTarget; menu = !menu; }}>{text.game} ▾</button>
      <div class="transport">
        <button disabled={disabled || !read?.paused} onclick={() => model.send({ kind: 'run' })}>▶ {text.run}</button>
        <button disabled={disabled || read?.paused} onclick={() => model.send({ kind: 'pause' })}>Ⅱ {text.pause}</button>
        <button class="step" disabled={disabled || !read?.paused} onclick={() => model.send({ kind: 'step' })}>▸| {text.step}</button>
      </div>
      <span data-testid="clock-state">{text[read?.paused === false ? 'running' : 'paused']}</span>
      <span class="clock-summary">{text.tick} <span data-testid="tick">{new Intl.NumberFormat(view.locale).format(BigInt(read!.tick))}</span></span>
      <strong class="cash-summary" aria-label={text.cash}>{new Intl.NumberFormat(view.locale).format(BigInt(read!.cash))}</strong>
      <div class="bar-end">
        <button aria-expanded={panel === 'view'} onclick={event => openPanel('view', event.currentTarget)}>{text.view}</button>
        <button aria-expanded={panel === 'session'} onclick={event => openPanel('session', event.currentTarget)}>{text.session} · <span data-testid="participant-role">{text[view.role!]}</span></button>
        <span class="sr-only" data-testid="connection">{text[view.connection]}</span>
      </div>
    </nav>
  {/if}
  <main class="map-stage">
    <MapView bind:this={map} {model} view={{ ...view, pending: view.pending || saveBusy }} {live} {panel} bind:tool bind:renderState onselect={select} onclose={closePanel}/>
    {#if live && menu}
      <nav id="game-menu" class="game-menu" aria-label={text.game}>
        {#each Object.entries(panels).filter(([id]) => id !== 'inspector') as [id, label]}<button onclick={event => openPanel(id as Panel, event.currentTarget)}>{text[label]}</button>{/each}
        <p>{text.tick} {new Intl.NumberFormat(view.locale).format(BigInt(read!.tick))} · {text.cash} {new Intl.NumberFormat(view.locale).format(BigInt(read!.cash))}</p>
      </nav>
    {/if}
    {#if live && panel && panel !== 'view'}
      <PanelFrame title={text[panels[panel]]} closeLabel={text.close} onclose={closePanel}>
        {#if panel === 'inspector'}
          {#if mapSelection && mapObjectPosition}<p data-testid="selected-object">{text[mapSelection.kind]} · {mapSelection.id} · {mapObjectPosition.x}, {mapObjectPosition.y}, {mapObjectPosition.z}</p>{/if}
          {#if train}
            <h3>{train.name}</h3><dl class="details"><div><dt>{text.cars}</dt><dd>{train.cars}</dd></div><div><dt>{text.position}</dt><dd data-testid="train-position">{train.position.x}, {train.position.y}, {train.position.z}</dd></div><div><dt>{text.trainState}</dt><dd data-testid="train-state">{text[train.state]}</dd></div></dl>
            <p class="control-holder" data-testid="control-holder">{text.control}: {train.controlLease?.participantId ?? text.unclaimed}</p>
            <div class="form-actions"><button disabled={disabled} onclick={() => model.acquireControl(train!.id)}>{text.takeControl}</button><button disabled={disabled || train.controlLease?.participantId !== view.participantId} onclick={() => model.releaseControl(train!.id)}>{text.releaseControl}</button></div>
            <label class="field">{text.controller}<select disabled={disabled} value={train.controller} onchange={event => { const value = event.currentTarget.value as 'automatic' | 'manual'; const id = train!.id; event.currentTarget.value = train!.controller; void model.controller(value, id, () => mapSelection?.kind === 'train' && mapSelection.id === id); }}><option value="automatic">{text.automatic}</option><option value="manual">{text.manual}</option></select></label>
            <button onclick={() => map.focusTrain(train!.id)}>{text.findTrain}</button><p class="hint">{text.manualHelp}</p>
          {:else if station}
            <dl class="details"><div><dt>{text.confirmedName}</dt><dd data-testid="confirmed-name">{station.name}</dd></div></dl>
            <form onsubmit={event => { event.preventDefault(); void model.rename(); }}>
              <label class="field">{text.stationName}<input value={view.draft} oninput={event => model.setDraft(event.currentTarget.value)} oncompositionstart={() => model.setComposing(true)} oncompositionend={() => model.setComposing(false)} onblur={() => model.setComposing(false)} onkeydown={event => { if (event.key === 'Enter' && (event.isComposing || event.keyCode === 229 || view.composing)) event.preventDefault(); }} maxlength="120" aria-describedby="draft-hint" /></label>
              <p class="hint" id="draft-hint">{text.draft}</p><div class="form-actions"><button type="submit" disabled={disabled || view.composing || !view.draft.trim()}>{text.rename}</button><button type="button" onclick={model.resetDraft}>{text.resetDraft}</button></div>
            </form>
          {:else if building}
            <dl class="details"><div><dt>{text.height}</dt><dd>{building.height}</dd></div><div><dt>{text.position}</dt><dd>{building.position.x}, {building.position.y}, {building.position.z}</dd></div></dl>
          {:else if track}<p>{text[track.shape]} · {track.revision}</p>
          {:else}<p>{mapSelection ? text.unavailableObject : text.pickObject}</p>{/if}
        {:else if panel === 'catalog'}
          <p class="hint">{text.catalogHint}</p><ul class="catalog" aria-label={text.buildings}>
            {#each catalog as item (item.id)}<li><button aria-pressed={selected.id === item.id} onclick={() => model.selectBuilding(item.id)}><img class="building-icon" src={item.family === 'pier' ? '/assets/pier.png' : '/assets/condo-top.png'} alt=""/><span><strong>{text[item.family]} {item.variant}</strong><small>{text[item.family === 'pier' ? 'fixed' : 'variable']} · {item.height}</small></span></button></li>{/each}
          </ul><h3>{text.selectedBuilding}</h3><p data-testid="building-name">{text[selected.family]} {selected.variant}</p><p>{text.height}: {selected.height}</p>
        {:else if panel === 'saves'}
          <SaveControls locale={view.locale} epoch={read?.worldEpoch} disabled={view.pending || view.connection !== 'ready' || view.role !== 'owner'} onbusy={value => { saveBusy = value; }}/>
        {:else if panel === 'settings'}
          <label class="field">{text.language}<select aria-label={text.language} value={view.locale} onchange={event => model.setLocale(event.currentTarget.value as Locale)}><option value="en">English</option><option value="ja">日本語</option></select></label>
          <button onclick={async () => { if (soundEnabled) { audio.disable(); soundEnabled = false; } else { soundEnabled = await audio.enable(); soundFailed = !soundEnabled; } }}>{soundEnabled ? text.disableSound : text.enableSound}</button><p data-testid="sound-feedback">{soundFailed ? text.soundFailed : soundEnabled ? text.soundOn : text.soundOff}</p>
        {:else if panel === 'session'}
          <p>{text[view.connection]} · {view.role ? text[view.role] : ''}</p><button disabled={saveBusy || view.pending} onclick={model.reconnect}>{text.reconnect}</button><button disabled={saveBusy} onclick={leave}>{text.leaveGame}</button>
        {:else if panel === 'help'}
          <h3>{text.network}</h3><p>{text.networkNotice}</p><p>{text.mapHelp}</p><p>{text.constructionHelp}</p><p>{text.manualHelp}</p><p>{text.visualSample}</p><p>{text.mapSize}</p>
        {/if}
      </PanelFrame>
    {/if}
  </main>
  {#if !live}<SessionGate {model} {view} renderFailed={renderState === 'failed'} onleave={leave}/>{/if}
  {#if live}<p class="command-feedback" role="status" aria-live="polite" aria-atomic="true">{text[view.feedback]}</p>{/if}
</div>
