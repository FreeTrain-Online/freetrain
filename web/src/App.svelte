<script lang="ts">
  import { toolbar } from './lib/toolbar.ts';
  import { onMount, tick } from 'svelte';
  import { appName, messages, persistLocale, initialLocale, type Locale } from './lib/localization.ts';
  import { createShellModel, catalog } from './lib/shell-model.ts';
  import { createRemoteSessionClient, leaveGame } from './lib/remote-session-client.ts';
  import { createBrowserAudio } from './lib/browser-audio.ts';
  import DecisionModal from './DecisionModal.svelte';
  import CreditsDialog from './CreditsDialog.svelte';
  import LicenseDialog from './LicenseDialog.svelte';
  import SchemeSelect from './SchemeSelect.svelte';
  import { applyScheme, initialScheme, type Scheme } from './lib/ui-preferences.ts';
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
  let creditsOpen = $state(false);
  let licenseOpen = $state(false);
  let renderState = $state<'loading' | 'ready' | 'failed'>('loading');
  let launcher: HTMLElement | undefined;
  let gameLauncher = $state<HTMLButtonElement>();
  let scheme = $state<Scheme>(initialScheme());
  const feedbackCode = $derived(view.feedback);
  let successExpired = $state(false);
  $effect(() => {
    successExpired = false;
    if (feedbackCode === 'accepted') { const timer = setTimeout(() => { successExpired = true; }, 4000); return () => clearTimeout(timer); }
  });
  let savedSlot = $state('slot1');
  let navigation = $state<(() => void) | undefined>();
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
  const draftStation = $derived(read?.stations.find(item => item.id === view.draftStationId));
  const draftConflict = $derived(view.draftDirty && (!draftStation || draftStation.revision !== view.draftRevision));
  const canApply = $derived(!disabled && !view.composing && !!view.draft.trim() && !draftConflict);
  const selected = $derived(catalog.find(item => item.id === view.selectedBuilding)!);
  $effect(() => persistLocale(view.locale));
  $effect(() => applyScheme(scheme));
  $effect(() => { if (!live) { panel = undefined; menu = false; mapSelection = undefined; navigation = undefined; creditsOpen = false; licenseOpen = false; } });
  $effect(() => {
    if (read) {
      if (previousEpoch && previousEpoch !== read.worldEpoch) { if (panel === 'inspector') panel = undefined; menu = false; mapSelection = undefined; navigation = undefined; }
      previousEpoch = read.worldEpoch;
    }
  });
  function returnFocus() { (launcher?.isConnected ? launcher : gameLauncher)?.focus({ preventScroll: true }); }
  function request(action: () => void) {
    if (saveBusy) return;
    map?.resetInteraction(); menu = false;
    if (view.draftDirty) navigation = action;
    else action();
  }
  function closeMenu() { menu = false; gameLauncher?.focus({ preventScroll: true }); }
  function closeNow() { panel = undefined; menu = false; returnFocus(); }
  function closePanel() { request(() => { closeNow(); if (tool !== 'inspect') map?.focusMap(); }); }
  function openPanel(next: Panel, source?: HTMLElement) {
    const nextPanel = panel === next ? undefined : next;
    request(() => { if (source) launcher = source; panel = nextPanel; if (!panel) returnFocus(); });
  }
  function select(selection: Selection | undefined) {
    if (selection?.kind === mapSelection?.kind && selection?.id === mapSelection?.id) { if (selection) { panel = 'inspector'; menu = false; } return; }
    request(() => {
      mapSelection = selection;
      if (selection?.kind === 'station') model.selectStation(selection.id);
      launcher = document.querySelector('canvas') ?? undefined;
      if (selection) { panel = 'inspector'; void tick().then(() => document.querySelector<HTMLElement>('.primary-panel h2')?.focus({ preventScroll: true })); }
    });
  }
  function requestTool(next: Tool) { request(() => { closeNow(); map?.activateTool(next); }); }
  async function resolveDraft(apply: boolean) {
    const action = navigation, epoch = read?.worldEpoch;
    if (apply) {
      if (!canApply || !await model.rename() || view.draftDirty || !live || read?.worldEpoch !== epoch || navigation !== action) return;
    } else model.resetDraft();
    navigation = undefined; await tick(); action?.();
  }
  function leaveNow() { if (saveBusy) return; audio.disable(); soundEnabled = false; map?.resetInteraction(); void model.leave(leaveGame); }
  function leave() { if (!saveBusy) request(leaveNow); }
  function escape(event: KeyboardEvent) {
    if (event.key !== 'Escape' || !live || event.isComposing || event.keyCode === 229) return;
    if (menu) { event.preventDefault(); closeMenu(); return; }
    if (event.target instanceof HTMLElement && event.target.matches('input,select,textarea,[contenteditable="true"]')) return;
    event.preventDefault();
    if (panel) closePanel(); else map?.escape();
  }
  function uiFocus(event: FocusEvent) { if (!(event.target instanceof HTMLCanvasElement)) map?.suspendInput(); }
  $effect(() => { if (live) { void tick().then(() => map?.focusMap()); } });
  onMount(() => {
    const unsubscribe = model.subscribe(next => { view = next; });
    const pagehide = (event: PageTransitionEvent) => { if (!event.persisted) dispose(); };
    const viewport = () => {
      const visible = window.visualViewport;
      // Pinch magnification keeps layout dimensions; keyboard occlusion reduces height.
      document.documentElement.style.setProperty('--viewport-height', `${visible ? visible.height * visible.scale : window.innerHeight}px`);
      document.documentElement.style.setProperty('--viewport-top', `${visible?.scale === 1 ? visible.offsetTop : 0}px`);
    };
    viewport(); window.addEventListener('resize', viewport); window.visualViewport?.addEventListener('resize', viewport); window.visualViewport?.addEventListener('scroll', viewport);
    window.addEventListener('pagehide', pagehide); document.addEventListener('focusin', uiFocus);
    return () => { window.removeEventListener('resize', viewport); window.visualViewport?.removeEventListener('scroll', viewport); window.visualViewport?.removeEventListener('resize', viewport); window.removeEventListener('pagehide', pagehide); document.removeEventListener('focusin', uiFocus); unsubscribe(); dispose(); };
  });
</script>

<svelte:head><title>{appName}</title></svelte:head>
<svelte:window onkeydown={escape}/>
<div class="app-shell" class:gated={!live} data-shell="svelte">
  {#if live}
    <div class="command-bar" role="toolbar" aria-label={text.game} aria-describedby="mutation-help" use:toolbar>
      <img class="app-icon" src={icon} width="32" height="32" alt={appName}/>
      <button bind:this={gameLauncher} aria-expanded={menu} aria-controls="game-menu" onclick={event => { map.resetInteraction(); menu = !menu; if (menu) void tick().then(() => [...document.querySelectorAll<HTMLButtonElement>('#game-menu button')].find(button => !button.disabled && button.getClientRects().length)?.focus({ preventScroll: true })); }}>{text.game} ▾</button>
      <div class="transport">
        <button disabled={disabled} onclick={() => model.send({ kind: read!.paused ? 'run' : 'pause' })}>{read?.paused ? '▶' : 'Ⅱ'} {text[read?.paused ? 'run' : 'pause']}</button>
        <button class="step" disabled={disabled || !read?.paused} onclick={() => model.send({ kind: 'step' })}>▸| {text.step}</button>
      </div>
      <span data-testid="clock-state">{text[read?.paused === false ? 'running' : 'paused']}</span>
      <span class="clock-summary">{text.tick} <span data-testid="tick">{new Intl.NumberFormat(view.locale).format(BigInt(read!.tick))}</span></span>
      <strong class="cash-summary" aria-label={text.cash}>{new Intl.NumberFormat(view.locale).format(BigInt(read!.cash))}</strong>
      <div class="bar-end">
        <button aria-controls="primary-panel" aria-expanded={panel === 'view'} onclick={event => openPanel('view', event.currentTarget)}>{text.view}</button>
        <button aria-controls="primary-panel" aria-expanded={panel === 'session'} onclick={event => openPanel('session', event.currentTarget)}>{text.session} · <span data-testid="participant-role">{text[view.role!]}</span></button>
        <span class="sr-only" data-testid="connection">{text[view.connection]}</span>
      </div>
    </div>
  {/if}
  <main class="map-stage">
    <MapView bind:this={map} {model} view={{ ...view, pending: view.pending || saveBusy }} {live} {panel} inputSuspended={menu || !!navigation || creditsOpen || licenseOpen} selection={mapSelection} ontool={requestTool} bind:tool bind:renderState onselect={select} onclose={closePanel}/>
    {#if live && menu}
      <button class="menu-dismiss" tabindex="-1" aria-label={text.closeMenu} onpointerdown={event => { event.preventDefault(); map.resetInteraction(); }} onclick={closeMenu}></button>
      <nav id="game-menu" class="game-menu" aria-label={text.game} use:toolbar={'vertical'}>
        <button class="overflow-step" disabled={disabled || !read?.paused} onclick={() => model.send({ kind: 'step' })}>{text.step}</button>
        {#each Object.entries(panels).filter(([id]) => id !== 'inspector') as [id, label]}<button aria-expanded={panel === id} aria-controls="primary-panel" onclick={event => openPanel(id as Panel, event.currentTarget)}>{text[label]}</button>{/each}
        <button aria-haspopup="dialog" onclick={() => { map.resetInteraction(); closeMenu(); creditsOpen = true; }}>{text.credits}</button>
        <button aria-haspopup="dialog" onclick={() => { map.resetInteraction(); closeMenu(); licenseOpen = true; }}>{text.license}</button>
        <p>{text.tick} {new Intl.NumberFormat(view.locale).format(BigInt(read!.tick))} · {text.cash} {new Intl.NumberFormat(view.locale).format(BigInt(read!.cash))}</p>
      </nav>
    {/if}
    {#if live && panel && panel !== 'view'}
      {#snippet stationActions()}
          <button type="submit" form="station-name-form" disabled={!canApply}>{text.rename}</button><button type="button" onclick={model.resetDraft}>{text.resetDraft}</button>
      {/snippet}
      <PanelFrame locale={view.locale} title={text[panels[panel]]} closeLabel={text.close} onclose={closePanel} footer={panel === 'inspector' && mapSelection?.kind === 'station' ? stationActions : undefined}>
        {#if panel === 'inspector'}
          {#if mapSelection && mapObjectPosition}<p data-testid="selected-object">{text[mapSelection.kind]} · {mapSelection.id} · {mapObjectPosition.x}, {mapObjectPosition.y}, {mapObjectPosition.z}</p>{/if}
          {#if train}
            <h3>{train.name}</h3><dl class="details"><div><dt>{text.cars}</dt><dd>{train.cars}</dd></div><div><dt>{text.position}</dt><dd data-testid="train-position">{train.position.x}, {train.position.y}, {train.position.z}</dd></div><div><dt>{text.trainState}</dt><dd data-testid="train-state">{text[train.state]}</dd></div></dl>
            <p class="hint" id="lease-help">{text.leaseHint}</p>
            <p class="control-holder" data-testid="control-holder">{text.control}: {train.controlLease?.participantId ?? text.unclaimed}</p>
            <div class="form-actions" aria-describedby="mutation-help lease-help"><button disabled={disabled} onclick={() => model.acquireControl(train!.id)}>{text.takeControl}</button><button disabled={disabled || train.controlLease?.participantId !== view.participantId} onclick={() => model.releaseControl(train!.id)}>{text.releaseControl}</button></div>
            <label class="field">{text.controller}<select disabled={disabled} value={train.controller} onchange={event => { const value = event.currentTarget.value as 'automatic' | 'manual'; const id = train!.id; event.currentTarget.value = train!.controller; void model.controller(value, id, () => mapSelection?.kind === 'train' && mapSelection.id === id); }}><option value="automatic">{text.automatic}</option><option value="manual">{text.manual}</option></select></label>
            <button onclick={() => map.focusTrain(train!.id)}>{text.findTrain}</button><p class="hint">{text.manualHelp}</p>
          {:else if station || (mapSelection?.kind === 'station' && view.draftDirty)}
            <dl class="details"><div><dt>{text.confirmedName}</dt><dd data-testid="confirmed-name">{station?.name ?? text.unavailableObject}</dd></div></dl>
            <form id="station-name-form" onsubmit={event => { event.preventDefault(); if (canApply) void model.rename(); }}>
              <label class="field">{text.stationName}<input value={view.draft} oninput={event => model.setDraft(event.currentTarget.value)} oncompositionstart={() => model.setComposing(true)} oncompositionend={() => model.setComposing(false)} onblur={() => model.setComposing(false)} onkeydown={event => { if (event.key === 'Enter' && (event.isComposing || event.keyCode === 229 || view.composing)) event.preventDefault(); }} maxlength="120" aria-describedby="draft-hint" /></label>
              {#if !['idle', 'accepted', 'waiting'].includes(view.feedback)}<p>{text[view.feedback]}</p>{/if}
              <p class="hint" id="draft-hint">{draftConflict ? text.draftConflict : text.draft}</p>
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
          <SaveControls bind:slot={savedSlot} onmodal={() => map.resetInteraction()} locale={view.locale} epoch={read?.worldEpoch} disabled={view.pending || view.connection !== 'ready' || view.role !== 'owner'} onbusy={value => { saveBusy = value; }}/>
        {:else if panel === 'settings'}
          <SchemeSelect locale={view.locale} {scheme} onchange={value => { scheme = value; }}/>
          <label class="field">{text.language}<select aria-label={text.language} value={view.locale} onchange={event => model.setLocale(event.currentTarget.value as Locale)}><option value="en">English</option><option value="ja">日本語</option></select></label>
          <button onclick={async () => { if (soundEnabled) { audio.disable(); soundEnabled = false; } else { soundEnabled = await audio.enable(); soundFailed = !soundEnabled; } }}>{soundEnabled ? text.disableSound : text.enableSound}</button><p data-testid="sound-feedback">{soundFailed ? text.soundFailed : soundEnabled ? text.soundOn : text.soundOff}</p>
        {:else if panel === 'session'}
          <p>{text[view.connection]} · {view.role ? text[view.role] : ''}</p><p>{text[view.feedback]}</p><button disabled={saveBusy || view.pending} onclick={() => request(model.reconnect)}>{text.reconnect}</button><button disabled={saveBusy} onclick={leave}>{text.leaveGame}</button>
        {:else if panel === 'help'}
          <p>{text.observerHint}</p><p>{text.toolbarHelp}</p><h3>{text.network}</h3><p>{text.networkNotice}</p><p>{text.mapHelp}</p><p>{text.constructionHelp}</p><p>{text.manualHelp}</p><p>{text.visualSample}</p><p>{text.mapSize}</p>
        {/if}
      </PanelFrame>
    {/if}
  </main>
  {#if live && navigation}
    <DecisionModal title={text.dirtyTitle} oncancel={() => { navigation = undefined; }}>
      <p>{text.dirtyHint}</p><p>{view.draft}</p>
      {#if draftConflict}<p role="alert">{text.draftConflict}</p>{/if}
      {#snippet actions()}
        <button class="primary" disabled={!canApply} onclick={() => resolveDraft(true)}>{text.rename}</button>
        <button disabled={view.pending} onclick={() => resolveDraft(false)}>{text.discard}</button>
        <button onclick={() => { navigation = undefined; }}>{text.keepEditing}</button>
      {/snippet}
    </DecisionModal>
  {/if}
  {#if !live}<SessionGate {model} {view} renderFailed={renderState === 'failed'} onleave={leaveNow} {scheme} onscheme={value => { scheme = value; }}/>{/if}
  {#if live && creditsOpen}<CreditsDialog locale={view.locale} onclose={() => { creditsOpen = false; }}/>{/if}
  {#if live && licenseOpen}<LicenseDialog locale={view.locale} onclose={() => { licenseOpen = false; }}/>{/if}
  {#if live}<p id="mutation-help" class="sr-only">{text.observerHint}</p><p class="command-feedback" role="status" aria-live="polite" aria-atomic="true">{view.draftInterrupted ? text.draftInterrupted + ' ' : ''}{view.feedback === 'idle' || successExpired ? '' : text[view.feedback]}</p>{/if}
</div>
