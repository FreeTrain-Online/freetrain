<script lang="ts">
  import { createTerrainPainter, type PaintState, type PaintTarget } from './lib/terrain-painter.ts';
  import { isReferenceWorld, isTerrainWorld, isMixedWorld } from './lib/world-identity.ts';
  import { pickTerrainStrategy, pickTerrainIntersection } from './lib/terrain-edit.ts';
  import { gameDate } from './lib/game-time.ts';
  import { toolbar } from './lib/toolbar.ts';
  import { primaryAction } from './lib/primary-action.ts';
  import { onMount, untrack, tick } from 'svelte';
  import type { ShellModel, ShellState } from './lib/shell-model.ts';
  import { messages } from './lib/localization.ts';
  import { MapRenderer, loadImages, type Selection } from './lib/map-renderer.ts';
  import { same, tileAt, zoomAt, type Camera } from './lib/map-display.ts';
  import { createWheelClassifier } from './lib/map-input.ts';
  import { snap } from './lib/map-geometry.ts';
  import { homeLocation, mapSpace } from './lib/map-space.ts';
  import { localSpatialPreview, matchesSpatialPreview } from './lib/spatial-preview.ts';
  import type { Position, Track, SpatialPreview } from './generated/contracts.ts';
  import { demoScene, demoCamera } from './lib/demo-scene.ts';
  import { demoLoopPeriod } from './lib/demo-railway-path.ts';
  import { tools, type Tool, type Panel } from './lib/game-ui.ts';
  import SlopeControls from './SlopeControls.svelte';
  import type { SlopePreview } from './generated/contracts.ts';
  import TrackControls from './TrackControls.svelte';
  import RemoveControls from './RemoveControls.svelte';
  import type { RailwayPreview } from './generated/contracts.ts';
  import CornerTerrainControls from './CornerTerrainControls.svelte';
  import RailToolIcon from './RailToolIcon.svelte';
  import PanelFrame from './PanelFrame.svelte';
  let { model, view, live, panel, inputSuspended, tool = $bindable('inspect'), renderState = $bindable('loading'), selection, ontool, onselect, onclose, onclock, oncatalog }:
    { model: ShellModel; view: ShellState; live: boolean; panel?: Panel; inputSuspended: boolean; tool?: Tool; renderState?: 'loading' | 'ready' | 'failed'; selection?: Selection; ontool: (tool: Tool) => void; onselect: (selection: Selection | undefined) => void; onclose: () => void; oncatalog: () => void; onclock: (kind: 'run' | 'pause' | 'step') => void } = $props();
  const text = $derived(messages[view.locale]);
  const liveScene = $derived((view.connection === 'ready' || view.connection === 'loading' && view.connectionStage === 'synchronizing') && !!view.read && !view.leaving && !view.signedOut);
  let canvas: HTMLCanvasElement;
  let renderer: MapRenderer | undefined;
  const space = $derived(mapSpace(view.read?.map ?? demoScene.map));
  let camera: Camera = demoCamera(1280,900);
  let narrowRailOffset=0;
  let zoomPercent = $state(100);
  let width = 0, height = 0, dirty = true, lastDpr = 0;
  let level = $state(1), heightCut = $state(15);
  let shape = $state<Track['shape']>('buffer');
  let drawPoles=$state(true);
  let drawSpeedLimits=$state(true);
  let night = $state(false), winter = $state(false), recolor = $state(false);
  let preview = $state<Position>(), selectedPosition = $state<Position>();
  let railControls=$state<{cancelDraft:()=>boolean;usesAutomaticPick:()=>boolean;isRemoving?:()=>boolean;usesSlopePick?:()=>boolean}>();
  let removeControls=$state<{cancelDraft:()=>boolean;pickMode:()=> 'object'|'auto'|'level'}>(),removePicked=$state<Selection|Position>();
  let removeLevel=$state(1),removalPath=$state<Position[]>([]);
  let railPicked=$state<Position>(),railPreview=$state<RailwayPreview>(),slopePreview=$state<SlopePreview>();
  let railPoints=$state<{position:Position;label:string}[]>([]),railLevel=$state(1);
  let cornerControls=$state<{cancelDraft:()=>boolean;cancelHold:()=>void}>();
  let rectangleMode=false;
  let cornerSelection=$state<Position>();
  let terrainStrategy=$state<string>();
  let paintState=$state<PaintState>({active:false,completed:0,status:'idle'});
  const painter=createTerrainPainter({get state(){return model.state;},previewCorner:(q,s)=>model.previewCorner(q,s),send:p=>model.send(p)},state=>{paintState=state;});
  const paintingMode=()=>terrainStrategy==='paintRaise'||terrainStrategy==='paintLower';
  let rectangleEnd=$state<Position>();
  let terrainSites=$state<{position:Position;skipped:boolean}[]>();
  let cornerGeneration=$state(0);
  let cornerPicked=$state<{position:Position;offsetX:number|null;offsetY:number|null;reverse:boolean;append?:boolean;painting?:boolean;soft?:boolean}>();
  let rawPosition:Position|undefined;
  let anchor=$state<Position>();
  let snapMode=$state<0|4|8>(0);
  let geometry=$state<SpatialPreview>();
  let geometryStatus=$state<'idle'|'checking'|'ready'|'failed'>('idle');
  let previewRequest:AbortController|undefined;
  let draftVersion = 0;
  let targetRevision = $state<string>();
  let uncertain = $state(false);
  let held = $state('');
  const keys = new Set<string>();
  let narrow = $state(false);
  let sendingInput = false, inputDirty = false;
  let inputTrainId: string | undefined;
  let epoch: string | undefined;
  let drag: { x: number; y: number; camera: Camera; moved: boolean; pan: boolean; panOnly: boolean; paint: boolean; pointerId: number } | undefined;
  const terrain = $derived(!!view.read && (isTerrainWorld(view.read) || isMixedWorld(view.read)));
  const railway = $derived(terrain || !!view.read && isMixedWorld(view.read));
  const reference = $derived(isReferenceWorld(view.read) || terrain || !!view.read && isMixedWorld(view.read));
  const worldHint = $derived(view.read && isMixedWorld(view.read) ? text.mixedWorldHint : terrain ? text.terrainWorldHint : text.referenceWorldHint);
  // Keep interaction resets tied to role changes, rather than every new view snapshot.
  const observer = $derived(view.role === 'observer');
  const blocked = $derived(view.pending || !live || observer);
  const selectedTrain = $derived(selection?.kind === 'train' ? view.read?.trains.find(item => item.id === selection?.id) : undefined);
  const shapes: Exclude<Track['shape'], `normal${number}`>[] = ['buffer', 'eastWest', 'northSouth', 'curveSE', 'curveSW', 'curveNE', 'curveNW', 'junctionE'];
  async function flushInput() {
    inputDirty = true;
    if (sendingInput) return;
    sendingInput = true;
    try { while (inputDirty) { inputDirty = false; await model.input(new Set(keys), inputTrainId); } }
    finally { sendingInput = false; }
  }
  function clearInput() { const had = keys.size > 0; keys.clear(); held = ''; if (had) void flushInput(); }
  let paintDelay:ReturnType<typeof setTimeout>|undefined;
  function cancelGesture() {
    clearTimeout(paintDelay);paintDelay=undefined;
    untrack(()=>cornerControls?.cancelHold());
    if(painter.state.active)painter.stop();
    if (drag && canvas.hasPointerCapture(drag.pointerId)) canvas.releasePointerCapture(drag.pointerId);
    drag = undefined;
  }
  function clearTarget() { draftVersion++; previewRequest?.abort(); previewRequest=undefined;geometry=undefined;geometryStatus='idle';rawPosition=undefined;targetRevision=undefined;selectedPosition=undefined;preview=undefined;uncertain=false;dirty=true; }
  function cancelDraft() { untrack(()=>{railControls?.cancelDraft();removeControls?.cancelDraft();});removePicked=undefined;removalPath=[];railPicked=undefined;railPreview=undefined;slopePreview=undefined;railPoints=[];rectangleMode=false;terrainStrategy=undefined;rectangleEnd=undefined;terrainSites=undefined;untrack(()=>cornerGeneration++);cornerPicked=undefined;cornerSelection=undefined;clearTarget();anchor=undefined;snapMode=0; }
  async function chooseTarget(raw:Position) {
    clearTarget();rawPosition={...raw};
    const read=view.read;if(!read)return;
    const version=draftVersion;
    const query={expectedWorldEpoch:read.worldEpoch,position:{...raw},anchor:anchor?{...anchor}:null,snap:anchor?snapMode:0 as 0|4|8};
    try {
      const local=localSpatialPreview(read,query);
      selectedPosition=local.position;preview=selectedPosition;targetRevision=read.tracks.find(t=>same(t.position,local.position))?.revision;
      geometryStatus='checking';const request=previewRequest=new AbortController();dirty=true;
      const checked=await model.previewSpatial(query,request.signal);
      if(request.signal.aborted||version!==draftVersion||!live||view.read?.worldEpoch!==read.worldEpoch)return;
      if(!matchesSpatialPreview(local,checked))throw new Error('Spatial mismatch');
      geometry=checked;geometryStatus='ready';dirty=true;
    } catch { if(version===draftVersion){geometry=undefined;geometryStatus='failed';dirty=true;} }
  }
  function useAnchor() { if(selectedPosition&&geometry?.inside){const point={...selectedPosition};clearTarget();anchor=point;} }
  function changeSnap(value:string) { snapMode=Number(value) as 0|4|8; if(rawPosition)void chooseTarget({...rawPosition}); }
  export function suspendInput() { clearInput(); cancelGesture(); }
  export function resetInteraction() { clearInput(); cancelGesture(); cancelDraft(); }
  export function focusMap() { if (live) canvas.focus({ preventScroll: true }); }
  export function escape() {
    if(railway&&tool==='remove'&&removeControls?.cancelDraft()) { /* First Escape clears the object. */ }
    else if(railway&&(tool==='build'||tool==='slope'||tool==='bridge'||tool==='girder'||tool==='tunnel'||tool==='speed'||tool==='stationary'||tool==='pole'||tool==='signal'||tool==='garage')&&railControls?.cancelDraft()) { /* First Escape clears the route. */ }
    else if(tool==='terrain'&&painter.state.active){painter.stop();}
    else if(tool==='terrain'&&cornerControls?.cancelDraft()) { /* First Escape clears the review. */ }
    else if (selectedPosition) cancelDraft();
    else if (tool !== 'inspect') activateTool('inspect');
    cancelGesture();clearInput();
  }
  function key(event: KeyboardEvent, down: boolean) {
    if (!live || inputSuspended || (down && (event.altKey || event.ctrlKey || event.metaKey)) || event.isComposing || event.keyCode === 229 || event.target !== canvas) return;
    if (event.code === 'Space') {
      event.preventDefault();
      if (down && !event.repeat) onclock(view.read?.paused ? 'run' : 'pause');
      return;
    }
    if (event.key.toLowerCase() === 'n' && view.read?.paused) {
      event.preventDefault();
      if (down && !event.repeat) onclock('step');
      return;
    }
    if (['+', '=', '-', '−'].includes(event.key)) {
      event.preventDefault();
      if (down && !event.repeat) zoomStep(event.key === '+' || event.key === '=' ? .25 : -.25);
      return;
    }
    const shortcut = tools.find(item=>item.shortcut.toLowerCase()===event.key.toLowerCase())?.id;
    if (shortcut) {
      event.preventDefault();
      if (down && !event.repeat && shortcut !== tool && (shortcut === 'inspect' || !blocked) && !(shortcut === 'remove' && reference && !railway) && !((shortcut === 'slope'||shortcut === 'bridge'||shortcut === 'girder'||shortcut === 'tunnel'||shortcut === 'speed'||shortcut === 'stationary'||shortcut === 'pole'||shortcut === 'signal'||shortcut === 'garage') && !railway) && !(shortcut === 'terrain' && !terrain)) ontool(shortcut);
      return;
    }
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
    if(narrowRailOffset){camera={...camera,y:camera.y-narrowRailOffset};narrowRailOffset=0;dirty=true;}
    resetInteraction(); tool = next === tool && next !== 'inspect' ? 'inspect' : next;
    focusMap();
    // A small world must remain reachable above the narrow tool sheet. Shift
    // only when opening the task; previews/confirmations preserve the camera.
    if(narrow&&railway&&(tool==='build'||tool==='remove'||tool==='slope'||tool==='bridge'||tool==='girder'||tool==='tunnel'||tool==='speed'||tool==='stationary'||tool==='pole'||tool==='signal'||tool==='garage')){
      const active=tool;
      void tick().then(()=>{
        const card=document.querySelector('.track-task');
        if(!live||tool!==active||!card)return;
        const r=canvas.getBoundingClientRect(),top=card.getBoundingClientRect().top-r.top;
        const catalogBottom=document.querySelector('#building-catalog')?.getBoundingClientRect().bottom;
        const visibleTop=catalogBottom===undefined?0:Math.min(top,Math.max(0,catalogBottom-r.top+8));
        narrowRailOffset=(height/2-(visibleTop+Math.max(0,top))/2)/camera.zoom;
        camera={...camera,y:camera.y+narrowRailOffset};dirty=true;
      });
    }
  }
  function point(event: MouseEvent) { const r = canvas.getBoundingClientRect(); return { x: event.clientX - r.left, y: event.clientY - r.top }; }
  function move(event: PointerEvent) {
    if (!live || inputSuspended) return;
    const p = point(event);
    if(drag?.paint){
      if(event.pointerId!==drag.pointerId)return;
      if(event.buttons!==1){cancelGesture();return;}
      const target=paintTarget(event);if(!target){cancelGesture();return;}
      if(Math.hypot(p.x-drag.x,p.y-drag.y)>3)drag.moved=true;
      if(drag.moved){
        clearTimeout(paintDelay);paintDelay=undefined;
        selectPaintTarget(target);
        if(painter.state.active)painter.move(target);else painter.start(target,terrainStrategy==='paintLower'?'lower':'raise');
      }
      dirty=true;return;
    }
    if(tool==='terrain'&&paintingMode()&&!drag&&!panel){dirty=true;}
    if (drag) {
      if (Math.hypot(p.x - drag.x, p.y - drag.y) > 3) drag.moved = true;
      if (drag.moved) {
        if (drag.pan) camera = { ...drag.camera, x: drag.camera.x - (p.x - drag.x) / camera.zoom, y: drag.camera.y - (p.y - drag.y) / camera.zoom };
        preview = undefined; dirty = true; return;
      }
    }
    if (!railway && tool !== 'inspect' && tool !== 'terrain' && !panel && !view.pending && !uncertain) {
      const raw=tileAt(p,level,camera,width,height,space);
      preview=selectedPosition??(anchor?snap(raw,anchor,snapMode):raw);dirty=true;
    }
  }
  function paintTarget(event:PointerEvent):PaintTarget|undefined {
    const p=point(event),read=view.read;if(!read?.terrain||p.x<0||p.y<0||p.x>=width||p.y>=height)return;
    const world={x:(p.x-width/2)/camera.zoom+camera.x,y:(p.y-height/2)/camera.zoom+camera.y};
    const position=pickTerrainIntersection(read,world,heightCut);if(!position)return;
    return {position,offsetX:null,offsetY:null,reverse:event.shiftKey};
  }
  function selectPaintTarget(target:PaintTarget){
    cornerSelection=target.position;
    cornerPicked={...target,painting:true};
  }
  function pickCorner(event:PointerEvent,append:boolean){
    const p=point(event),read=view.read;if(!read?.terrain)return;
    const world={x:(p.x-width/2)/camera.zoom+camera.x,y:(p.y-height/2)/camera.zoom+camera.y};
    const intersection=paintingMode();
    const position=intersection?pickTerrainIntersection(read,world,heightCut):pickTerrainStrategy(read,world,heightCut,terrainStrategy);if(!position)return;
    if(intersection)cornerSelection=position;
    const projected=space.project(position);
    const next={position,offsetX:intersection?null:Math.max(0,Math.min(31,Math.floor(world.x-projected.x))),offsetY:intersection?null:Math.max(-32,Math.min(32,Math.floor(world.y-projected.y))),reverse:event.ctrlKey,soft:event.shiftKey,append};
    cornerPicked=next;
  }
  function down(event: PointerEvent) {
    if (!live || inputSuspended || (event.button !== 0 && event.button !== 1)) return;
    event.preventDefault(); focusMap();
    drag = { ...point(event), camera: { ...camera }, moved: false, pan: tool === 'inspect' || event.button === 1, panOnly: event.button === 1, paint: false, pointerId: event.pointerId };
    canvas.setPointerCapture(event.pointerId);
    if(tool==='terrain'&&paintingMode()&&!drag.pan&&!blocked&&!panel){
      drag.paint=true;const target=paintTarget(event);if(target){cornerSelection=target.position;dirty=true;paintDelay=setTimeout(()=>{paintDelay=undefined;selectPaintTarget(target);painter.start(target,terrainStrategy==='paintLower'?'lower':'raise');},200);}return;
    }
  }
  function up(event: PointerEvent) {
    const was = drag,selectIntersection=was?.paint&&!was.moved&&paintDelay!==undefined; cancelGesture();
    if(selectIntersection&&!blocked&&!panel){pickCorner(event,false);return;}
    if (!live || inputSuspended || !was || was.pointerId !== event.pointerId || was.paint || was.moved || was.panOnly || event.button !== 0) return;
    const p = point(event);
    if (tool === 'inspect') { clearInput(); onselect(renderer?.pick(p, camera.zoom)); }
    else if(tool==='terrain'&&!blocked&&!panel)pickCorner(event,false);
    else if(railway&&tool==='remove'&&!blocked&&!panel){
      const pick=removeControls?.pickMode();
      removePicked=pick==='object'?renderer?.pick(p,camera.zoom):pick==='auto'?renderer?.pickLocation(p,{kind:'railOrGround'}):tileAt(p,removeLevel,camera,width,height,space);
      if(!removePicked)removeControls?.cancelDraft();
    }
    else if(railway&&(tool==='build'||tool==='slope'||tool==='bridge'||tool==='girder'||tool==='tunnel'||tool==='speed'||tool==='stationary'||tool==='pole'||tool==='signal'||tool==='garage')&&!blocked&&!panel){
      railPicked=railControls?.usesAutomaticPick()?renderer?.pickLocation(p,{kind:tool==='slope'||tool==='speed'&&railControls?.usesSlopePick?.()?'slopeOrGround':tool==='speed'?'trafficOrGround':tool==='stationary'?(railControls?.isRemoving?.()?'stationaryOrGround':'ground'):'railOrGround'}):tileAt(p,railLevel,camera,width,height,space);
    }
    else if (!blocked && !panel && !uncertain) { void chooseTarget(tileAt(p,level,camera,width,height,space)); }
    dirty = true;
  }
  async function commit() {
    if (blocked || panel || uncertain || !selectedPosition || !view.read || geometryStatus!=='ready'||!geometry?.inside||geometry.worldEpoch!==view.read.worldEpoch) return;
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
  export function zoom(factor: number) { if (!live) return;cancelGesture(); camera = zoomAt(camera, factor, visibleCenter(), width, height); dirty = true; }
  function zoomStep(delta: number) { zoom((camera.zoom + delta) / camera.zoom); }
  export function home() { zoom(1.5 / camera.zoom); }
  function frameWorld() {
    cancelGesture();
    narrowRailOffset=0;
    const read=view.read, location=homeLocation(read?.map??demoScene.map);
    if(read?.terrain && (isTerrainWorld(read)||isMixedWorld(read))) {
      const h=Math.floor(read.map.width/2),v=Math.floor(read.map.height/2);
      location.z=Math.max(read.terrain.waterLevel,read.terrain.cells[v*read.map.width+h].ground);
    }
    camera = { ...space.center(location), zoom: Math.min(1.5, Math.max(.5, width / 850)) }; dirty = true;
  }
  export function focusTrain(id = selectedTrain?.id) {
    const train = view.read?.trains.find(t => t.id === id);
    if (train && live) {
      const position = space.center(train.position), center = visibleCenter();
      camera = { x: position.x - (center.x - width / 2) / camera.zoom, y: position.y - (center.y - height / 2) / camera.zoom, zoom: camera.zoom };
      dirty = true; focusMap();
    }
  }
  $effect(() => {
    const read = view.read;
    if (liveScene && read) {
      if (epoch !== read.worldEpoch) { epoch = read.worldEpoch; renderState='loading'; heightCut=read.map.levels-1;railLevel=Math.min(railLevel,read.map.levels-1);removeLevel=Math.min(removeLevel,read.map.levels-1);resetInteraction(); tool = 'inspect'; frameWorld(); }
      renderer?.setRead(read); dirty = true;
    } else {
      renderer?.setRead(demoScene); resetInteraction(); tool = 'inspect';
      renderState = renderState === 'failed' ? 'failed' : 'loading'; dirty = true;
    }
  });
  $effect(() => { if (!live || tool !== 'inspect' || selectedTrain?.controller !== 'manual' || selectedTrain.controlLease?.participantId !== view.participantId) clearInput(); });
  $effect(() => { if (inputSuspended) suspendInput(); });
  $effect(() => { if (observer || !live) resetInteraction(); });
  $effect(() => {
    if (selectedPosition && view.read?.tracks.find(t => same(t.position, selectedPosition!))?.revision !== targetRevision) cancelDraft();
  });
  $effect(() => { void panel; void level; void shape; cancelGesture(); cancelDraft(); clearInput(); });
    $effect(() => { void heightCut;cancelGesture(); });
    $effect(() => { void heightCut; void drawPoles; void drawSpeedLimits; void night; void winter; void recolor; void selection; void preview; void cornerSelection; void rectangleEnd; void terrainSites; void removalPath; void railPreview; void slopePreview; void railPoints; void anchor; void tool; dirty = true; });
  onMount(() => {
    let active = true, frame = 0;
    let demoTime=0,lastDemoTick:number|undefined,lastDemoPaint=-Infinity;
    const reduced=matchMedia('(prefers-reduced-motion: reduce)');
    const motion=()=>{lastDemoTick=undefined;dirty=true;};reduced.addEventListener('change',motion);
    const compact = matchMedia('(max-width: 639px)');
    const density = () => { narrow = compact.matches; }; density(); compact.addEventListener('change', density);
    const resize = new ResizeObserver(entries => { cancelGesture();width = entries[0].contentRect.width; height = entries[0].contentRect.height; dirty = true; }); resize.observe(canvas);
    loadImages().then(images => {
      if (!active) return;
      renderer = new MapRenderer(canvas, images,()=>{if(active)dirty=true;}); renderer.setRead(liveScene && view.read ? view.read : demoScene); frameWorld(); dirty = true;
    }).catch(() => { if (active) renderState = 'failed'; });
    const paint = (now:number) => {
      if (!active) return;
      const animate=!liveScene&&!document.hidden&&!reduced.matches;
      if(animate) {
        if(lastDemoTick!==undefined)demoTime=(demoTime+now-lastDemoTick)%demoLoopPeriod;
        if(now-lastDemoPaint>=1000/30){dirty=true;lastDemoPaint=now;}
      }
      lastDemoTick=animate?now:undefined;
      if (lastDpr !== devicePixelRatio) { lastDpr = devicePixelRatio; dirty = true; }
      if (dirty && renderer && width && height && !document.hidden) {
        try {
          const ready=renderer.draw(liveScene ? camera : demoCamera(width,height), width, height, devicePixelRatio, liveScene
            ? { heightCut, level, drawPoles, drawSpeedLimits, night, winter, recolor, selection, preview:cornerSelection?undefined:preview, terrainCorner:cornerSelection, rectangleEnd, terrainSites, removalPath, railPreview, slopePreview, railPoints, anchor, shape, remove: tool === 'remove', season: (['winter','spring','summer','autumn'] as const)[Math.floor((gameDate(view.read!.tick).month%12)/3)] }
            : { heightCut: demoScene.map.levels-1, level: 1, night: false, winter: false, recolor: false, shape: 'buffer', remove: false, demoTime });
          canvas.dataset.scene = liveScene ? 'live' : 'demo';
          // Terrain revisions keep the completed scene visible while preparing
          // its replacement. Initial reveal still waits for a complete image.
          if (liveScene) { if(ready)renderState='ready'; zoomPercent = Math.round(camera.zoom * 100); }
        } catch { if(liveScene)renderState = 'failed'; }
        dirty = renderer.needsFrame;
      }
      frame = requestAnimationFrame(paint);
    }; frame = requestAnimationFrame(paint);
    const renew = setInterval(() => { if (keys.size) void flushInput(); }, 150);
    const blur = () => { clearInput(); cancelGesture(); };
    const visibility = () => { lastDemoTick=undefined;blur(); dirty = true; };
    const classifyWheel = createWheelClassifier();
    const wheel = (event: WheelEvent) => {
      if (!live || inputSuspended || (!event.deltaX && !event.deltaY)) return;
      event.preventDefault(); cancelGesture();
      if (classifyWheel(event, performance.now()) === 'pan') {
        camera = { ...camera, x: camera.x + event.deltaX / camera.zoom, y: camera.y + event.deltaY / camera.zoom };
      } else if (event.deltaY) {
        camera = zoomAt(camera, event.ctrlKey ? Math.exp(-event.deltaY * .01) : event.deltaY < 0 ? 1.15 : 1 / 1.15, point(event), width, height);
      }
      dirty = true;
    };
    canvas.addEventListener('wheel', wheel, { passive: false }); window.addEventListener('blur', blur); document.addEventListener('visibilitychange', visibility);
    return () => { compact.removeEventListener('change', density);reduced.removeEventListener('change',motion); active = false; resetInteraction(); clearInterval(renew); cancelAnimationFrame(frame); resize.disconnect(); canvas.removeEventListener('wheel', wheel); window.removeEventListener('blur', blur); document.removeEventListener('visibilitychange', visibility); };
  });
</script>

<div class="map-viewport" class:demo={!live} inert={!live} aria-hidden={!live}>
  <canvas bind:this={canvas} class="world-canvas" style:cursor={tool==='remove'||tool==='terrain'&&paintingMode()?'crosshair':undefined} tabindex={live ? 0 : -1} aria-label={text.mapCanvas} aria-describedby="manual-help" onpointerdown={down} onpointermove={move} onpointerup={up} onpointercancel={cancelGesture} onlostpointercapture={cancelGesture} onpointerleave={() => { if (!drag) { preview = selectedPosition; dirty = true; } }} onkeydown={event => key(event, true)} onkeyup={event => key(event, false)} onblur={()=>{clearInput();cancelGesture();}}></canvas>
</div>
{#if live}
  <div class="tool-strip" role="toolbar" aria-orientation={narrow ? 'horizontal' : 'vertical'} aria-label={text.mapTools} aria-describedby="mutation-help" use:toolbar={narrow ? 'horizontal' : 'vertical'}>
    {#each tools.filter(item=>!['slope','bridge','girder','tunnel','garage','signal','pole','stationary','speed'].includes(item.id)||railway) as item}
      {@const label=reference && item.id==='inspect' ? text.referenceInspect : railway && item.id==='build' ? text.trackTask : reference && item.id==='build' ? text.spatialTarget : text[item.label]}
      <button aria-label={label} title={`${label} (${item.shortcut})${item.id==='terrain'&&!terrain?` · ${text.terrainUnavailable}`:''}`} aria-keyshortcuts={item.shortcut} aria-describedby={item.id==='terrain'&&!terrain?'terrain-unavailable':undefined} aria-pressed={tool === item.id} disabled={item.id === 'terrain' && !terrain || item.id === 'remove' && reference && !railway || item.id !== 'inspect' && blocked} onclick={() => ontool(item.id)}>{#if item.icon==='rail'}<RailToolIcon />{:else if item.icon==='pole'}<RailToolIcon pole />{:else if item.icon==='garage'}<RailToolIcon garage />{:else if item.icon==='tunnel'}<RailToolIcon tunnel />{:else if item.icon==='girder'}<RailToolIcon girder />{:else if item.icon==='bridge'}<RailToolIcon bridge />{:else if item.icon==='slope'}<RailToolIcon slope />{:else}{item.icon}{/if}</button>
    {/each}
  </div>
  {#if !terrain}<span id="terrain-unavailable" class="sr-only">{text.terrainUnavailable}</span>{/if}
  <div class="map-navigation" class:beside-panel={!!panel}>
    <button aria-label={text.zoomOut} onclick={() => zoomStep(-.25)}>−</button><span class="zoom-level" aria-label={text.zoomLevel}>{zoomPercent}%</span><button aria-label={text.zoomIn} onclick={() => zoomStep(.25)}>＋</button><button onclick={home}>{text.homeMap}</button>
  </div>
  {#if panel === 'view'}
    <PanelFrame locale={view.locale} title={text.view} closeLabel={text.close} {onclose}>
      <div class="map-options">
        <label>{text.heightCut}<input aria-label={text.heightCut} type="range" min="0" max={view.read!.map.levels-1} bind:value={heightCut}/><span>{heightCut}</span></label>
        <label><input type="checkbox" bind:checked={drawPoles}/>{text.showPoles}</label>
        <label><input type="checkbox" bind:checked={drawSpeedLimits}/>{text.showSpeedLimits}</label>
        <label><input type="checkbox" bind:checked={night}/>{text.night}</label><label><input type="checkbox" bind:checked={winter}/>{text.winter}</label><label><input type="checkbox" bind:checked={recolor}/>{text.recolor}</label>
        <button disabled={!selectedTrain} onclick={() => focusTrain()}>{text.findTrain}</button><button onclick={home}>{text.homeMap}</button>
      </div>
    </PanelFrame>
  {/if}
  {#if tool === 'terrain' && terrain && !panel}
    {#key cornerGeneration}<CornerTerrainControls bind:this={cornerControls} {model} {view} {paintState} onadjust={(position,direction,continuous)=>{cornerSelection={...position};painter.start({position,offsetX:null,offsetY:null,reverse:false},direction,continuous);dirty=true;}} onpaintstop={cancelGesture} picked={cornerPicked} onmode={(value,strategy)=>{cancelGesture();cornerSelection=undefined;rectangleMode=value;terrainStrategy=strategy;}} onpreview={(p,end,sites)=>{preview=p;rectangleEnd=end;terrainSites=sites;dirty=true;}} onreset={()=>{cornerPicked=undefined;cornerSelection=undefined;cancelGesture();}}/>{/key}
  {/if}
  {#if railway && tool === 'slope' && !panel}
    {#key cornerGeneration}<SlopeControls bind:this={railControls} {model} {view} picked={railPicked} bind:level={railLevel} {heightCut} onpreview={p=>{slopePreview=p;dirty=true;}} onpoints={p=>{railPoints=p;dirty=true;}} onreveal={z=>{heightCut=z;}}/>{/key}
  {/if}
  {#if railway && tool === 'remove' && !panel}
    {#key cornerGeneration}<RemoveControls bind:this={removeControls} {model} {view} picked={removePicked} bind:level={removeLevel} {heightCut} onpreview={p=>{terrainSites=[...(p?.positions??[]).map(position=>({position,skipped:false})),...(p?.skipped??[]).map(s=>({position:s.position,skipped:true}))];removalPath=p?.path??[];dirty=true;}} onpoints={p=>{railPoints=p;dirty=true;}} onreset={()=>{removePicked=undefined;}} onreveal={()=>{heightCut=removeLevel;}}/>{/key}
  {/if}
  {#if railway && (tool === 'build' || tool === 'bridge' || tool === 'girder' || tool === 'tunnel' || tool === 'speed' || tool === 'stationary' || tool === 'pole' || tool === 'signal' || tool === 'garage') && !panel}
    {#key cornerGeneration}<TrackControls {oncatalog} infrastructure={tool==='speed'?'speed':tool==='stationary'?'stationary':tool==='pole'?'pole':tool==='signal'?'signal':tool==='garage'?'garage':tool==='tunnel'?'tunnel':tool==='girder'?'girder':tool==='bridge'?'bridge':'normal'} bind:this={railControls} {model} {view} picked={railPicked} bind:level={railLevel} {heightCut} onpreview={p=>{railPreview=p;dirty=true;}} onpoints={p=>{railPoints=p;dirty=true;}} onreveal={()=>{heightCut=railLevel;}}/>{/key}
  {/if}
  {#if !railway && tool !== 'inspect' && tool !== 'terrain' && !panel}
    <section class="tool-card" use:primaryAction aria-label={reference ? text.spatialTarget : text[tool === 'build' ? 'buildTool' : 'removeTool']}>
      <h2>{reference ? text.spatialTarget : text[tool === 'build' ? 'buildTool' : 'removeTool']}</h2>
      <div class="form-actions">
        <label>{text.buildLevel}<select aria-label={text.buildLevel} bind:value={level} disabled={view.pending}>{#each Array.from({length:reference ? view.read!.map.levels:2},(_,i)=>i) as z}<option value={z}>{z}</option>{/each}</select></label>
        {#if tool === 'build' && !reference}<label>{text.trackShape}<select aria-label={text.trackShape} bind:value={shape} disabled={view.pending}>{#each shapes as s}<option value={s}>{text[s]}</option>{/each}</select></label>{/if}
      </div>
      <p data-testid="preview-position">{selectedPosition ? `${selectedPosition.x}, ${selectedPosition.y}, ${selectedPosition.z}` : text[panel ? 'closePanelToTarget' : 'pickTile']}</p>
      {#if tool==='build'}
        <div class="form-actions"><label>{text.snapDirection}<select aria-label={text.snapDirection} value={snapMode} onchange={event=>changeSnap(event.currentTarget.value)} disabled={view.pending}><option value={0}>{text.snapNone}</option><option value={4}>{text.snapFour}</option><option value={8}>{text.snapEight}</option></select></label><button disabled={view.pending||!geometry?.inside} onclick={useAnchor}>{text.useAnchor}</button></div>
        {#if anchor}<p data-testid="snap-anchor">{text.snapAnchor}: {anchor.x}, {anchor.y}, {anchor.z}</p>{#if !reference}<p class="hint">{text.alignmentHint}</p>{/if}{/if}
      {/if}
      <p data-testid="geometry-status" aria-live="polite">{text[geometryStatus==='checking'?'checkingGeometry':geometryStatus==='failed'?'geometryFailed':geometryStatus==='ready'?(geometry?.inside?'geometryChecked':'outsideMap'):'pickTile']}</p>
      {#if geometry?.heading!==null&&geometry?.heading!==undefined}<p data-testid="preview-heading">{text.directionLabel}: {text[(['headingNorth','headingNorthEast','headingEast','headingSouthEast','headingSouth','headingSouthWest','headingWest','headingNorthWest'] as const)[geometry.heading]]} · {text.distanceLabel}: {geometry.distance}</p>{/if}
      <p class="hint">{reference ? worldHint : text.unvalidatedPreview}</p>
      {#if !['idle', 'accepted', 'waiting'].includes(view.feedback)}<p>{text[view.feedback]}</p>{/if}
      {#if selectedPosition && selectedPosition.z > heightCut}<button onclick={() => { heightCut = level; }}>{text.revealPreview}</button>{/if}
      <div class="form-actions"><button class="primary" disabled={reference || blocked || !!panel || uncertain || !selectedPosition || geometryStatus!=='ready'||!geometry?.inside || (tool === 'remove' && !view.read?.tracks.some(t => same(t.position, selectedPosition!)))} onclick={commit}>{text.confirmTrack}</button><button disabled={view.pending||(!selectedPosition&&!anchor)} onclick={cancelDraft}>{text.cancelPreview}</button></div>
    </section>
  {/if}
  <div class="sr-only" data-testid="map-selection" aria-live="polite" aria-atomic="true">{text[tool === 'build' ? 'buildTool' : tool === 'remove' ? 'removeTool' : 'inspectTool']} · {tool === 'inspect' && selectedTrain?.controller === 'manual' && selectedTrain.controlLease?.participantId === view.participantId ? `${text.driving} ${selectedTrain.name} · ` : ''}{tool !== 'inspect' && panel ? text.closePanelToTarget : selection ? `${text[selection.kind]} · ${selection.id}` : text.pickObject}</div>
  <span id="manual-help" class="sr-only">{reference ? worldHint : text.manualHelp}</span><span class="sr-only" data-testid="manual-input">{held || text.keysReleased}</span>
{/if}
