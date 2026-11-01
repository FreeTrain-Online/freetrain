<script lang="ts">
  import { formatGameTime } from './lib/game-time.ts';
  import { isReferenceWorld, isTerrainWorld, isMixedWorld } from './lib/world-identity.ts';
  import { toolbar } from './lib/toolbar.ts';
  import { onMount, tick } from 'svelte';
  import { appName, messages, persistLocale, initialLocale } from './lib/localization.ts';
  import { createShellModel, catalog } from './lib/shell-model.ts';
  import { createRemoteSessionClient, leaveGame } from './lib/remote-session-client.ts';
  import { createBrowserAudio } from './lib/browser-audio.ts';
  import DecisionModal from './DecisionModal.svelte';
  import { activatePrimary } from './lib/primary-action.ts';
  import CreditsDialog from './CreditsDialog.svelte';
  import { creditFor, type PluginPack } from './lib/optional-content.ts';
  import LicenseDialog from './LicenseDialog.svelte';
  import SchemeSelect from './SchemeSelect.svelte';
  import LanguageSelect from './LanguageSelect.svelte';
  import BellControls from './BellControls.svelte';
  import { applyScheme, initialScheme, type Scheme } from './lib/ui-preferences.ts';
  import EmptyWorldControls from './EmptyWorldControls.svelte';
  import SaveControls from './SaveControls.svelte';
  import MapView from './MapView.svelte';
  import PanelFrame from './PanelFrame.svelte';
  import BuildingCatalog from './BuildingCatalog.svelte';
  import ObjectProvenance from './ObjectProvenance.svelte';
  import type { CatalogCategory, SignalDirection } from './lib/building-catalog.ts';
  import SessionGate from './SessionGate.svelte';
  import type { Selection } from './lib/map-renderer.ts';
  import { panels, type Panel, type Tool } from './lib/game-ui.ts';
  const icon = '/assets/FTOicon.png';
  const audio = createBrowserAudio();
  let soundEnabled = $state(false), soundFailed = $state(false), saveBusy = $state(false);
  let enabledContentHash = $state('');
  let enabledPacks = $state<PluginPack[]>([]), optionalCredits = $state<PluginPack>();
  const model = createShellModel(() => createRemoteSessionClient((event, epoch) => { void audio.event(event, epoch).then(() => { soundEnabled = audio.enabled; }); }, content => { enabledPacks = content.packs; enabledContentHash = content.setHash; }), initialLocale());
  const dispose = () => { model.dispose(); audio.dispose(); };
  // The model publishes replacement states containing validated immutable reads.
  // Preserve their identity for validation and incremental rendering caches.
  let view = $state.raw(model.state);
  let mapSelection = $state<Selection>();
  let map: MapView;
  let panel = $state<Panel>(), menu = $state(false), tool = $state<Tool>('inspect');
  let catalogOpen = $state(false), catalogCategory = $state<CatalogCategory>('signal');
  let catalogLauncher: HTMLElement | undefined;
  let creditsOpen = $state(false);
  let licenseOpen = $state(false);
  let renderState = $state<'loading' | 'ready' | 'failed'>('loading');
  let launcher: HTMLElement | undefined;
  let gameLauncher = $state<HTMLButtonElement>();
  let menuLeft = $state(0);
  let menuTop = $state(0);
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
  // A same-socket state transfer keeps the completed world on screen.
  const synchronizing = $derived(view.connection === 'loading' && view.connectionStage === 'synchronizing');
  const live = $derived((view.connection === 'ready' || synchronizing) && !!read && !view.leaving && !view.signedOut && renderState === 'ready');
  const train = $derived(mapSelection?.kind === 'train' ? read?.trains.find(item => item.id === mapSelection?.id) : undefined);
  const station = $derived(mapSelection?.kind === 'station' ? read?.stations.find(item => item.id === mapSelection?.id) : undefined);
  const building = $derived(mapSelection?.kind === 'building' ? read?.buildings.find(item => item.id === mapSelection?.id) : undefined);
  const track = $derived(mapSelection?.kind === 'track' ? read?.tracks.find(item => (item.entityId ?? `${item.position.x},${item.position.y},${item.position.z}`) === mapSelection?.id) : undefined);
  const railwayVoxel = $derived(mapSelection?.kind === 'railway' ? read?.mixed?.voxels.find(v=>v.ownerId===mapSelection?.id&&v.position.x===mapSelection.position.x&&v.position.y===mapSelection.position.y&&v.position.z===mapSelection.position.z) : undefined);
  const railwayDefinition = $derived(mapSelection?.kind === 'railway' ? mapSelection.component==='rail' ? railwayVoxel?.traffic?.rail?.definition : mapSelection.component==='accessory' ? railwayVoxel?.traffic?.accessory?.definition : railwayVoxel?.definition : undefined);
  const mapObjectPosition = $derived(train?.position ?? mapSelection?.position);
  const terrain = $derived(!!read && isTerrainWorld(read));
  const mixed = $derived(!!read && isMixedWorld(read));
  const reference = $derived(isReferenceWorld(read) || terrain || mixed);
  const worldHint = $derived(mixed ? text.mixedWorldHint : terrain ? text.terrainWorldHint : text.referenceWorldHint);
  const clockDisabled = $derived(saveBusy || view.pending || view.connection !== 'ready' || !live || view.role === 'observer');
  const disabled = $derived(reference || clockDisabled);
  const draftStation = $derived(read?.stations.find(item => item.id === view.draftStationId));
  const draftConflict = $derived(view.draftDirty && (!draftStation || draftStation.revision !== view.draftRevision));
  const canApply = $derived(!disabled && !view.composing && !!view.draft.trim() && !draftConflict);
  const selected = $derived(catalog.find(item => item.id === view.selectedBuilding)!);
  $effect(() => persistLocale(view.locale));
  $effect(() => applyScheme(scheme));
  $effect(() => { if (!live) { catalogOpen = false; panel = undefined; menu = false; mapSelection = undefined; navigation = undefined; creditsOpen = false; optionalCredits = undefined; licenseOpen = false; } });
  $effect(() => {
    if (read) {
      if (previousEpoch && previousEpoch !== read.worldEpoch) { if (panel === 'inspector') panel = undefined; menu = false; mapSelection = undefined; navigation = undefined; }
      previousEpoch = read.worldEpoch;
    }
  });
  function syncMenuPosition() {
    const stage = document.querySelector<HTMLElement>('.map-stage');
    const stageRect = stage?.getBoundingClientRect();
    const buttonRect = gameLauncher?.getBoundingClientRect();
    if (!stageRect || !buttonRect) return;
    menuLeft = buttonRect.left - stageRect.left;
    menuTop = 0;
  }
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
    if (next === 'catalog' && reference) {
      const nextOpen = !catalogOpen || !!panel;
      request(() => {
        if (source) catalogLauncher = gameLauncher;
        panel = undefined; catalogOpen = nextOpen;
        if (catalogOpen) void tick().then(() => document.querySelector<HTMLElement>('#building-catalog h2')?.focus({ preventScroll: true }));
      });
      return;
    }
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
  function showCatalog() {
    if (tool === 'signal' || tool === 'stationary') catalogCategory = tool;
    catalogLauncher = document.querySelector<HTMLElement>('.tool-card button') ?? undefined;
    catalogOpen = true;
  }
  function closeCatalog() {
    catalogOpen = false;
    if (catalogLauncher?.isConnected) catalogLauncher.focus({ preventScroll: true });
    else map?.focusMap();
  }
  function chooseCatalogItem(category: CatalogCategory, id: string, direction?: SignalDirection) {
    if (view.pending || saveBusy || synchronizing) return;
    const choose = () => {
      model.selectCatalogItem(category, id, direction);
      if (!clockDisabled && (terrain || mixed)) {
        panel = undefined;
        if (tool !== category) map?.activateTool(category);
      }
    };
    if (view.draftDirty) request(choose); else choose();
  }
  function requestTool(next: Tool) { request(() => {
    closeNow(); map?.activateTool(next);
    if (tool === 'signal' || tool === 'stationary') { catalogCategory = tool; catalogOpen = true; }
  }); }

  async function resolveDraft(apply: boolean) {
    const action = navigation, epoch = read?.worldEpoch;
    if (apply) {
      if (!canApply || !await model.rename() || view.draftDirty || !live || read?.worldEpoch !== epoch || navigation !== action) return;
    } else model.resetDraft();
    navigation = undefined; await tick(); action?.();
  }
  function leaveNow() { if (saveBusy) return; audio.disable(); soundEnabled = false; map?.resetInteraction(); void model.leave(leaveGame); }
  function leave() { if (!saveBusy) request(leaveNow); }
  function clock(kind: 'run' | 'pause' | 'step') {
    if (!clockDisabled && (kind !== 'step' || read?.paused)) void model.send({ kind });
  }
  function key(event: KeyboardEvent) {
    // Map picking keeps canvas focus; a disabled preview button can leave body
    // focused. Both should still activate the visible panel's default action.
    if (event.key === 'Enter' && live && !synchronizing && !menu && !navigation && !creditsOpen && !optionalCredits && !licenseOpen
      && event.target instanceof HTMLElement && event.target.matches('canvas, body, html')) {
      const scope = document.querySelector<HTMLElement>('.map-stage .primary-panel, .map-stage .tool-card');
      if (scope) activatePrimary(event, scope);
      return;
    }
    if (event.key !== 'Escape' || !live || event.isComposing || event.keyCode === 229) return;
    if (menu) { event.preventDefault(); closeMenu(); return; }
    if (catalogOpen && event.target instanceof HTMLElement && event.target.closest('#building-catalog')) { event.preventDefault(); closeCatalog(); return; }
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
<svelte:window onkeydown={key}/>
<div class="app-shell" class:gated={!live} data-shell="svelte">
  {#if live}
    <div class="command-bar" role="toolbar" aria-label={text.game} aria-describedby="mutation-help" use:toolbar>
      <img class="app-icon" src={icon} width="32" height="32" alt={appName}/>
      <button bind:this={gameLauncher} aria-expanded={menu} aria-controls="game-menu" onclick={event => { map.resetInteraction(); syncMenuPosition(); menu = !menu; if (menu) void tick().then(() => [...document.querySelectorAll<HTMLButtonElement>('#game-menu button')].find(button => !button.disabled && button.getClientRects().length)?.focus({ preventScroll: true })); }}>{text.game} ▾</button>
      <div class="transport">
        <button disabled={clockDisabled} onclick={() => model.send({ kind: read!.paused ? 'run' : 'pause' })}>{read?.paused ? '▶' : 'Ⅱ'} {text[read?.paused ? 'run' : 'pause']}</button>
        <button class="step" disabled={clockDisabled || !read?.paused} onclick={() => model.send({ kind: 'step' })}>▶| {text.step}</button>
      </div>
      {#if reference}<span class="sr-only" data-testid="reference-world">{mixed ? text.mixedWorld : terrain ? text.terrainWorld : text.referenceWorld}</span>{/if}
      <span data-testid="clock-state">{text[read?.paused === false ? 'running' : 'paused']}</span>
      {#if reference}<span class="clock-summary" data-testid="game-time">{formatGameTime(read!.tick,view.locale)}</span>{:else}
      <span class="clock-summary">{text.tick} <span data-testid="tick">{new Intl.NumberFormat(view.locale).format(BigInt(read!.tick))}</span></span>
      <strong class="cash-summary" aria-label={text.cash}>{new Intl.NumberFormat(view.locale).format(BigInt(read!.cash))}</strong>
      {/if}
      <div class="bar-end">
        <button aria-controls="primary-panel" aria-expanded={panel === 'view'} onclick={event => openPanel('view', event.currentTarget)}>{text.view}</button>
        <button aria-controls="primary-panel" aria-expanded={panel === 'session'} onclick={event => openPanel('session', event.currentTarget)}>{text.session} · <span data-testid="participant-role">{text[view.role!]}</span></button>
        <span class="sr-only" data-testid="connection">{text[view.connection]}</span>
      </div>
    </div>
  {/if}
  <main class="map-stage" class:with-catalog={live && catalogOpen && !panel}>
    <MapView bind:this={map} {model} view={{ ...view, pending: view.pending || saveBusy || synchronizing }} {live} {panel} inputSuspended={synchronizing || menu || !!navigation || creditsOpen || !!optionalCredits || licenseOpen} selection={mapSelection} ontool={requestTool} bind:tool bind:renderState onselect={select} onclose={closePanel} onclock={clock} oncatalog={showCatalog}/>
    {#if live && menu}
      <button class="menu-dismiss" tabindex="-1" aria-label={text.closeMenu} onpointerdown={event => { event.preventDefault(); map.resetInteraction(); }} onclick={closeMenu}></button>
      <nav id="game-menu" class="game-menu" aria-label={text.game} use:toolbar={'vertical'} style:left={`${menuLeft}px`} style:top={`${menuTop}px`}>
        {#if terrain||mixed}<button disabled={clockDisabled||!terrain} title={!terrain?text.terrainUnavailable:undefined} onclick={()=>requestTool('terrain')}>{text.terrainTool}</button>{/if}
        <button class="overflow-step" disabled={clockDisabled || !read?.paused} onclick={() => model.send({ kind: 'step' })}>{text.step}</button>
        {#each Object.entries(panels).filter(([id]) => id !== 'inspector') as [id, label]}<button aria-expanded={id === 'catalog' && reference ? catalogOpen && !panel : panel === id} aria-controls={id === 'catalog' && reference ? 'building-catalog' : 'primary-panel'} onclick={event => openPanel(id as Panel, event.currentTarget)}>{text[label]}</button>{/each}
        <button aria-haspopup="dialog" onclick={() => { map.resetInteraction(); closeMenu(); creditsOpen = true; }}>{text.credits}</button>
        {#each enabledPacks as pack (pack.id)}
          <button aria-haspopup="dialog" onclick={() => { map.resetInteraction(); closeMenu(); optionalCredits = pack; }}>{creditFor(pack,view.locale).button}</button>
        {/each}
        <button aria-haspopup="dialog" onclick={() => { map.resetInteraction(); closeMenu(); licenseOpen = true; }}>{text.license}</button>
        {#if reference}<p data-testid="menu-game-time">{formatGameTime(read!.tick,view.locale)}</p><p>{worldHint}</p>{:else}<p>{text.tick} {new Intl.NumberFormat(view.locale).format(BigInt(read!.tick))} · {text.cash} {new Intl.NumberFormat(view.locale).format(BigInt(read!.cash))}</p>{/if}
      </nav>
    {/if}
    {#if live && catalogOpen && !panel}
      <BuildingCatalog locale={view.locale} category={catalogCategory} selections={view.catalogSelections} signalDirection={view.signalDirection} pending={view.pending || saveBusy || synchronizing} oncategory={value => { catalogCategory = value; }} onselect={chooseCatalogItem} onclose={closeCatalog}/>
    {/if}
    {#if live && panel && panel !== 'view'}
      {#snippet stationActions()}
          <button class="primary" type="submit" form="station-name-form" disabled={!canApply}>{text.rename}</button><button type="button" onclick={model.resetDraft}>{text.resetDraft}</button>
      {/snippet}
      <PanelFrame locale={view.locale} title={text[panels[panel]]} closeLabel={text.close} onclose={closePanel} primaryInContent={panel === 'newWorld' || panel === 'saves' || (panel === 'inspector' && mapSelection?.kind === 'station')} footer={panel === 'inspector' && mapSelection?.kind === 'station' ? stationActions : undefined}>
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
          {:else if track}<p>{track.shape.startsWith('normal') ? `${text.normalRail} ${track.shape.slice(6)}` : text[track.shape as keyof typeof text]} · {track.revision}</p>
          {:else if mapSelection?.kind === 'railway' && railwayVoxel && railwayDefinition}
            <p data-testid="railway-component">{text[({rail:'track',pier:'pier',support:'railwaySupport',accessory:'railwayAccessory',stationary:'stationaryTask'} as const)[mapSelection.component]]}</p>
            <p data-testid="railway-definition">{railwayDefinition.packId} · {railwayDefinition.contributionId} · {railwayDefinition.recipeId}</p>
            <ObjectProvenance id={railwayDefinition.contributionId} packId={railwayDefinition.packId} locale={view.locale}/>
            {#if railwayVoxel.traffic?.rail}<p>{text.railwayPattern}: {railwayVoxel.traffic.rail.pattern}</p>{/if}
            {#if railwayVoxel.traffic?.rail?.controllerId}<p>{text.controller}: {railwayVoxel.traffic.rail.controllerId}</p>{/if}
            {#if railwayVoxel.supportedOwnerId}<p>{text.railwaySupportedOwner}: {railwayVoxel.supportedOwnerId}</p>{/if}
          {:else if mapSelection?.kind === 'terrain'}<p data-testid="terrain-surface">{text[({ground:'terrainGround',mountain:'terrainMountain',water:'terrainWater',underground:'terrainUnderground',terrace:'terrainTerrace',forest:'terrainForest',creek:'terrainCreek',cliff:'terrainCliff'} as const)[mapSelection.surface]]}</p><p>{text.terrainWorldHint}</p>
          {:else}<p>{mapSelection ? text.unavailableObject : text.pickObject}</p>{/if}
        {:else if panel === 'catalog'}
          {#if reference}<p>{worldHint}</p>{:else}
          <p class="hint">{text.catalogHint}</p><ul class="catalog" aria-label={text.buildings}>
            {#each catalog as item (item.id)}<li><button aria-pressed={selected.id === item.id} onclick={() => model.selectBuilding(item.id)}><img class="building-icon" src={item.family === 'pier' ? '/assets/pier.png' : '/assets/condo-top.png'} alt=""/><span><strong>{text[item.family]} {item.variant}</strong><small>{text[item.family === 'pier' ? 'fixed' : 'variable']} · {item.height}</small></span></button></li>{/each}
          </ul><h3>{text.selectedBuilding}</h3><p data-testid="building-name">{text[selected.family]} {selected.variant}</p><p>{text.height}: {selected.height}</p>
          {/if}
        {:else if panel === 'newWorld'}
          <EmptyWorldControls contentIdentity={JSON.stringify([view.participantId,enabledContentHash])} locale={view.locale} epoch={read?.worldEpoch} revision={read?.revision} disabled={view.pending || view.connection !== 'ready' || view.role !== 'owner' || !reference} onbusy={value => { saveBusy = value; }} onmodal={() => map.resetInteraction()}/>
        {:else if panel === 'saves'}
          <SaveControls bind:slot={savedSlot} onmodal={() => map.resetInteraction()} locale={view.locale} epoch={read?.worldEpoch} disabled={view.pending || view.connection !== 'ready' || view.role !== 'owner'} onbusy={value => { saveBusy = value; }}/>
        {:else if panel === 'settings'}
          <SchemeSelect locale={view.locale} {scheme} onchange={value => { scheme = value; }}/>
          <LanguageSelect locale={view.locale} onchange={model.setLocale}/>
          <BellControls locale={view.locale}/>
          <button onclick={async () => { if (soundEnabled) { audio.disable(); soundEnabled = false; } else { soundEnabled = await audio.enable(); soundFailed = !soundEnabled; } }}>{soundEnabled ? text.disableSound : text.enableSound}</button><p data-testid="sound-feedback">{soundFailed ? text.soundFailed : soundEnabled ? text.soundOn : text.soundOff}</p>
        {:else if panel === 'session'}
          {#if reference}<p>{worldHint}</p>{/if}
          {#if read?.ruleset==='prototype-reference-1'}<p data-testid="compatibility-mode">{text.temporaryReferenceWorld}</p>{/if}
          <p>{text[view.connection]} · {view.role ? text[view.role] : ''}</p><p>{text[view.feedback]}</p><button disabled={saveBusy || view.pending} onclick={() => request(model.reconnect)}>{text.reconnect}</button><button disabled={saveBusy} onclick={leave}>{text.leaveGame}</button>
        {:else if panel === 'help'}
          <p>{text.mapHelp}</p><p>{text.toolShortcuts}</p><p>{text.toolbarHelp}</p>
          {#if reference}<p>{worldHint}</p>{#if terrain||mixed}<h3>{text.trackTask}</h3><p>{text.railHelp}</p><p>{text.railHeuristicHelp}</p><p>{text.railRemovalHelp}</p><p>{text.railCutHelp}</p><h3>{text.stationaryTask}</h3><p>{text.stationaryHelp}</p><p>{text.stationaryRules}</p><p>{text.stationaryRemovalHelp}</p><h3>{text.poleTask}</h3><p>{text.poleHelp}</p><p>{text.poleRules}</p><p>{text.poleRemovalHelp}</p><h3>{text.signalTask}</h3><p>{text.signalHelp}</p><p>{text.signalRules}</p><p>{text.signalRemovalHelp}</p><h3>{text.garageTask}</h3><p>{text.garageDescription}</p><p>{text.garageHelp}</p><p>{text.garageRules}</p><p>{text.garageRemovalHelp}</p><h3>{text.tunnelTask}</h3><p>{text.tunnelDescription}</p><p>{text.tunnelHelp}</p><p>{text.tunnelRules}</p><p>{text.tunnelRemovalHelp}</p><h3>{text.girderTask}</h3><p>{text.girderDescription}</p><p>{text.girderHelp}</p><p>{text.girderRules}</p><p>{text.girderRemovalHelp}</p><h3>{text.bridgeTask}</h3><p>{text.bridgeDescription}</p><p>{text.bridgeHelp}</p><p>{text.bridgeRules}</p><p>{text.bridgeRemovalHelp}</p><h3>{text.slopeTask}</h3><p>{text.slopeHelp}</p><p>{text.slopeRemovalHelp}</p><p>{text.slopeGhostHelp}</p>{/if}
          {:else}<p>{text.observerHint}</p><p>{text.constructionHelp}</p><p>{text.manualHelp}</p>{/if}
          {#if terrain}<h3>{text.terrainTool}</h3><p>{text.terrainPaintHelp}</p><p>{text.cornerHelp}</p><p>{text.rectangleHelp}</p>{/if}
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
  {#if live && optionalCredits}
    <DecisionModal title={creditFor(optionalCredits,view.locale).title} oncancel={() => { optionalCredits = undefined; }}>
      <p style="white-space:pre-wrap;overflow-wrap:anywhere">{creditFor(optionalCredits,view.locale).text}</p>
      {#each optionalCredits.notices as notice}<p><a href={notice.url} target="_blank" rel="noopener noreferrer">{notice.path}</a></p>{/each}
      {#snippet actions()}<button class="primary" onclick={() => { optionalCredits = undefined; }}>{text.closeDialog}</button>{/snippet}
    </DecisionModal>
  {/if}
  {#if live && creditsOpen}<CreditsDialog locale={view.locale} onclose={() => { creditsOpen = false; }}/>{/if}
  {#if live && licenseOpen}<LicenseDialog locale={view.locale} onclose={() => { licenseOpen = false; }}/>{/if}
  {#if live}<p id="mutation-help" class="sr-only">{reference ? worldHint : text.observerHint}</p><p class="command-feedback" role="status" aria-live="polite" aria-atomic="true">{view.draftInterrupted ? text.draftInterrupted + ' ' : ''}{view.feedback === 'idle' || successExpired ? '' : text[view.feedback]}</p>{/if}
</div>
