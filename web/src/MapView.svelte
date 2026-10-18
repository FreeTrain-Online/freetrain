<script lang="ts">
  import { onMount } from 'svelte';
  import type { ShellModel, ShellState } from './lib/shell-model.ts';
  import { messages } from './lib/localization.ts';
  import { MapRenderer, loadImages, type Selection } from './lib/map-renderer.ts';
  import { project, same, tileAt, zoomAt, type Camera } from './lib/map-geometry.ts';
  import type { Position, Track } from './generated/contracts.ts';
  import { demoScene } from './lib/demo-scene.ts';
  import { tools, type Tool, type Panel } from './lib/game-ui.ts';
  import PanelFrame from './PanelFrame.svelte';
  let { model, view, live, panel, inputSuspended, tool = $bindable('inspect'), renderState = $bindable('loading'), selection, ontool, onselect, onclose }:
    { model: ShellModel; view: ShellState; live: boolean; panel?: Panel; inputSuspended: boolean; tool?: Tool; renderState?: 'loading' | 'ready' | 'failed'; selection?: Selection; ontool: (tool: Tool) => void; onselect: (selection: Selection | undefined) => void; onclose: () => void } = $props();
  const text = $derived(messages[view.locale]);
  const liveScene = $derived(view.connection === 'ready' && !!view.read && !view.leaving && !view.signedOut);
  let canvas: HTMLCanvasElement;
  let renderer: MapRenderer | undefined;
  let camera: Camera = { ...project({ x: 22, y: 16, z: 0 }), zoom: 1 };
  let zoomPercent = $state(100);
  let width = 0, height = 0, dirty = true, lastDpr = 0;
  let level = $state(1), heightCut = $state(15);
  let shape = $state<Track['shape']>('buffer');
  let night = $state(false), winter = $state(false), recolor = $state(false);
  let preview = $state<Position>(), selectedPosition = $state<Position>();
  let draftVersion = 0;
  let targetRevision = $state<string>();
  let uncertain = $state(false);
  let held = $state('');
  const keys = new Set<string>();
  let space = false, sendingInput = false, inputDirty = false;
  let inputTrainId: string | undefined;
  let epoch: string | undefined;
  let drag: { x: number; y: number; camera: Camera; moved: boolean; pan: boolean; panOnly: boolean; pointerId: number } | undefined;
  const blocked = $derived(view.pending || !live || view.role === 'observer');
  const selectedTrain = $derived(selection?.kind === 'train' ? view.read?.trains.find(item => item.id === selection?.id) : undefined);
  const shapes: Track['shape'][] = ['buffer', 'eastWest', 'northSouth', 'curveSE', 'curveSW', 'curveNE', 'curveNW', 'junctionE'];
  async function flushInput() {
    inputDirty = true;
    if (sendingInput) return;
    sendingInput = true;
    try { while (inputDirty) { inputDirty = false; await model.input(new Set(keys), inputTrainId); } }
    finally { sendingInput = false; }
  }
  function clearInput() { const had = keys.size > 0; keys.clear(); held = ''; space = false; if (had) void flushInput(); }
  function cancelGesture() {
    if (drag && canvas.hasPointerCapture(drag.pointerId)) canvas.releasePointerCapture(drag.pointerId);
    drag = undefined;
  }
  function cancelDraft() { draftVersion++; targetRevision = undefined; selectedPosition = undefined; preview = undefined; uncertain = false; dirty = true; }
  export function suspendInput() { clearInput(); cancelGesture(); }
  export function resetInteraction() { clearInput(); cancelGesture(); cancelDraft(); }
  export function focusMap() { if (live) canvas.focus({ preventScroll: true }); }
  export function escape() {
    if (selectedPosition) cancelDraft();
    else if (tool !== 'inspect') activateTool('inspect');
    clearInput();
  }
  function key(event: KeyboardEvent, down: boolean) {
    if (!live || inputSuspended || event.isComposing || event.keyCode === 229 || event.target !== canvas) return;
    if (event.code === 'Space') { event.preventDefault(); space = down; return; }
    if (tool !== 'inspect' || !selectedTrain || selectedTrain.controller !== 'manual' || selectedTrain.controlLease?.participantId !== view.participantId) return;
    if (!['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Shift'].includes(event.key)) return;
    event.preventDefault();
    if (event.repeat) return;
    inputTrainId = selectedTrain.id;
    if (down) keys.add(event.key); else keys.delete(event.key);
    held = [...keys].join(' + '); void flushInput();
  }
  export function activateTool(next: Tool) {
    if (!live || (next !== 'inspect' && blocked)) return;
    resetInteraction(); tool = next === tool && next !== 'inspect' ? 'inspect' : next;
    focusMap();
  }
  function point(event: MouseEvent) { const r = canvas.getBoundingClientRect(); return { x: event.clientX - r.left, y: event.clientY - r.top }; }
  function move(event: PointerEvent) {
    if (!live || inputSuspended) return;
    const p = point(event);
    if (drag) {
      if (Math.hypot(p.x - drag.x, p.y - drag.y) > 3) drag.moved = true;
      if (drag.moved) {
        if (drag.pan) camera = { ...drag.camera, x: drag.camera.x - (p.x - drag.x) / camera.zoom, y: drag.camera.y - (p.y - drag.y) / camera.zoom };
        preview = undefined; dirty = true; return;
      }
    }
    if (tool !== 'inspect' && !panel && !view.pending && !uncertain) { preview = selectedPosition ?? tileAt(p, level, camera, width, height); dirty = true; }
  }
  function down(event: PointerEvent) {
    if (!live || inputSuspended || (event.button !== 0 && event.button !== 1)) return;
    event.preventDefault(); focusMap();
    drag = { ...point(event), camera: { ...camera }, moved: false, pan: tool === 'inspect' || event.button === 1 || space, panOnly: event.button === 1 || space, pointerId: event.pointerId };
    canvas.setPointerCapture(event.pointerId);
  }
  function up(event: PointerEvent) {
    const was = drag; cancelGesture();
    if (!live || inputSuspended || !was || was.pointerId !== event.pointerId || was.moved || was.panOnly || event.button !== 0 || space) return;
    const p = point(event);
    if (tool === 'inspect') { clearInput(); onselect(renderer?.pick(p, camera.zoom)); }
    else if (!blocked && !panel && !uncertain) { draftVersion++; selectedPosition = tileAt(p, level, camera, width, height); preview = selectedPosition; targetRevision = view.read?.tracks.find(t => same(t.position, selectedPosition!))?.revision; }
    dirty = true;
  }
  async function commit() {
    if (blocked || panel || uncertain || !selectedPosition || !view.read) return;
    const position = { ...selectedPosition }, submittedEpoch = epoch, submittedVersion = draftVersion;
    let accepted;
    if (tool === 'build') accepted = await model.send({ kind: 'buildTrack', position, shape });
    else if (tool === 'remove') {
      const track = view.read.tracks.find(t => same(t.position, position));
      if (track && track.revision === targetRevision) accepted = await model.send({ kind: 'removeTrack', position, expectedRevision: targetRevision! });
    }
    if (epoch !== submittedEpoch || draftVersion !== submittedVersion) return;
    if (accepted) cancelDraft();
    else if (view.feedback === 'unresolved') uncertain = true;
  }
  function visibleCenter() {
    const r = canvas.getBoundingClientRect(), overlay = document.querySelector('.primary-panel')?.getBoundingClientRect();
    if (!overlay) return { x: width / 2, y: height / 2 };
    if (overlay.width >= r.width * .8) return { x: width / 2, y: Math.max(0, overlay.top - r.top) / 2 };
    return { x: Math.max(0, overlay.left - r.left) / 2, y: height / 2 };
  }
  export function zoom(factor: number) { if (!live) return; camera = zoomAt(camera, factor, visibleCenter(), width, height); dirty = true; }
  export function home() { camera = { ...project({ x: 22, y: 16, z: 0 }), zoom: Math.min(1.5, Math.max(.5, width / 850)) }; dirty = true; }
  export function focusTrain(id = selectedTrain?.id) {
    const train = view.read?.trains.find(t => t.id === id);
    if (train && live) {
      const position = project(train.position), center = visibleCenter();
      camera = { x: position.x - (center.x - width / 2) / camera.zoom, y: position.y - (center.y - height / 2) / camera.zoom, zoom: camera.zoom };
      dirty = true; focusMap();
    }
  }
  $effect(() => {
    const read = view.read;
    if (liveScene && read) {
      if (epoch !== read.worldEpoch) { epoch = read.worldEpoch; resetInteraction(); tool = 'inspect'; home(); }
      renderer?.setRead(read); dirty = true;
    } else {
      renderer?.setRead(demoScene); resetInteraction(); tool = 'inspect';
      renderState = renderState === 'failed' ? 'failed' : 'loading'; dirty = true;
    }
  });
  $effect(() => { if (!live || tool !== 'inspect' || selectedTrain?.controller !== 'manual' || selectedTrain.controlLease?.participantId !== view.participantId) clearInput(); });
  $effect(() => { if (inputSuspended) suspendInput(); });
  $effect(() => { if (view.role === 'observer' || !live) resetInteraction(); });
  $effect(() => {
    if (selectedPosition && view.read?.tracks.find(t => same(t.position, selectedPosition!))?.revision !== targetRevision) cancelDraft();
  });
  $effect(() => { void panel; void level; void shape; cancelGesture(); cancelDraft(); clearInput(); });
  $effect(() => { void heightCut; void night; void winter; void recolor; void selection; void preview; void tool; dirty = true; });
  onMount(() => {
    let active = true, frame = 0;
    const resize = new ResizeObserver(entries => { width = entries[0].contentRect.width; height = entries[0].contentRect.height; dirty = true; }); resize.observe(canvas);
    loadImages().then(images => {
      if (!active) return;
      renderer = new MapRenderer(canvas, images); renderer.setRead(liveScene && view.read ? view.read : demoScene); home(); dirty = true;
    }).catch(() => { if (active) renderState = 'failed'; });
    const paint = () => {
      if (!active) return;
      if (lastDpr !== devicePixelRatio) { lastDpr = devicePixelRatio; dirty = true; }
      if (dirty && renderer && width && height && !document.hidden) {
        const demoCamera = { ...project({ x: 22, y: 16, z: 0 }), zoom: Math.min(1.5, Math.max(.8, width / 850)) };
        try {
          renderer.draw(liveScene ? camera : demoCamera, width, height, devicePixelRatio, liveScene
            ? { heightCut, level, night, winter, recolor, selection, preview, shape, remove: tool === 'remove' }
            : { heightCut: 15, level: 1, night: false, winter: false, recolor: false, shape: 'buffer', remove: false });
          canvas.dataset.scene = liveScene ? 'live' : 'demo';
          if (liveScene) { renderState = 'ready'; zoomPercent = Math.round(camera.zoom * 100); }
        } catch { renderState = 'failed'; }
        dirty = false;
      }
      frame = requestAnimationFrame(paint);
    }; frame = requestAnimationFrame(paint);
    const renew = setInterval(() => { if (keys.size) void flushInput(); }, 150);
    const blur = () => { clearInput(); cancelGesture(); };
    const visibility = () => { blur(); dirty = true; };
    const wheel = (event: WheelEvent) => { if (!live || inputSuspended) return; event.preventDefault(); camera = zoomAt(camera, event.deltaY < 0 ? 1.15 : 1 / 1.15, point(event), width, height); dirty = true; };
    canvas.addEventListener('wheel', wheel, { passive: false }); window.addEventListener('blur', blur); document.addEventListener('visibilitychange', visibility);
    return () => { active = false; resetInteraction(); clearInterval(renew); cancelAnimationFrame(frame); resize.disconnect(); canvas.removeEventListener('wheel', wheel); window.removeEventListener('blur', blur); document.removeEventListener('visibilitychange', visibility); };
  });
</script>

<div class="map-viewport" class:demo={!live} inert={!live} aria-hidden={!live}>
  <canvas bind:this={canvas} class="world-canvas" tabindex={live ? 0 : -1} aria-label={text.mapCanvas} aria-describedby="manual-help" onpointerdown={down} onpointermove={move} onpointerup={up} onpointercancel={cancelGesture} onlostpointercapture={() => { drag = undefined; }} onpointerleave={() => { if (!drag) { preview = selectedPosition; dirty = true; } }} onkeydown={event => key(event, true)} onkeyup={event => key(event, false)} onblur={clearInput}></canvas>
</div>
{#if live}
  <nav class="tool-strip" aria-label={text.mapTools}>
    {#each tools as item}<button aria-label={text[item.label]} title={text[item.label]} aria-pressed={tool === item.id} disabled={item.id !== 'inspect' && blocked} onclick={() => ontool(item.id)}>{item.icon}</button>{/each}
  </nav>
  <div class="map-navigation" class:beside-panel={!!panel}>
    <button aria-label={text.zoomOut} onclick={() => zoom(.8)}>−</button><span class="zoom-level" aria-label={text.zoomLevel}>{zoomPercent}%</span><button aria-label={text.zoomIn} onclick={() => zoom(1.25)}>＋</button><button onclick={home}>{text.homeMap}</button>
  </div>
  {#if panel === 'view'}
    <PanelFrame title={text.view} closeLabel={text.close} {onclose}>
      <div class="map-options">
        <label>{text.heightCut}<input aria-label={text.heightCut} type="range" min="0" max="15" bind:value={heightCut}/><span>{heightCut}</span></label>
        <label><input type="checkbox" bind:checked={night}/>{text.night}</label><label><input type="checkbox" bind:checked={winter}/>{text.winter}</label><label><input type="checkbox" bind:checked={recolor}/>{text.recolor}</label>
        <button disabled={!selectedTrain} onclick={() => focusTrain()}>{text.findTrain}</button><button onclick={home}>{text.homeMap}</button>
      </div>
    </PanelFrame>
  {/if}
  {#if tool !== 'inspect' && !panel}
    <section class="tool-card" aria-label={text[tool === 'build' ? 'buildTool' : 'removeTool']}>
      <h2>{text[tool === 'build' ? 'buildTool' : 'removeTool']}</h2>
      <div class="form-actions">
        <label>{text.buildLevel}<select aria-label={text.buildLevel} bind:value={level} disabled={view.pending}><option value={0}>0</option><option value={1}>1</option></select></label>
        {#if tool === 'build'}<label>{text.trackShape}<select aria-label={text.trackShape} bind:value={shape} disabled={view.pending}>{#each shapes as s}<option value={s}>{text[s]}</option>{/each}</select></label>{/if}
      </div>
      <p data-testid="preview-position">{selectedPosition ? `${selectedPosition.x}, ${selectedPosition.y}, ${selectedPosition.z}` : text[panel ? 'closePanelToTarget' : 'pickTile']}</p>
      <p class="hint">{text.unvalidatedPreview}</p>
      {#if selectedPosition && selectedPosition.z > heightCut}<button onclick={() => { heightCut = level; }}>{text.revealPreview}</button>{/if}
      <div class="form-actions"><button class="primary" disabled={blocked || !!panel || uncertain || !selectedPosition || (tool === 'remove' && !view.read?.tracks.some(t => same(t.position, selectedPosition!)))} onclick={commit}>{text.confirmTrack}</button><button disabled={view.pending} onclick={cancelDraft}>{text.cancelPreview}</button></div>
    </section>
  {/if}
  <div class="selection-feedback" data-testid="map-selection">{tool !== 'inspect' && panel ? text.closePanelToTarget : selection ? `${text[selection.kind]} · ${selection.id}` : text.pickObject}</div>
  <span id="manual-help" class="sr-only">{text.manualHelp}</span><span class="sr-only" data-testid="manual-input">{held || text.keysReleased}</span>
{/if}
