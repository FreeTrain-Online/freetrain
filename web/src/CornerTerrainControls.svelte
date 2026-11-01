<script lang="ts">
  import { railwayRevision } from './lib/railway-edit.ts';
  import { primaryAction } from './lib/primary-action.ts';
  import type { PaintState } from './lib/terrain-painter.ts';
  import RectangleTerrainControls from './RectangleTerrainControls.svelte';
  import { terrainReasons } from './lib/terrain-edit.ts';
  import { onMount, untrack } from 'svelte';
  import type { CornerTerrainQuery, CornerTerrainPreview, Position } from './generated/contracts.ts';
  import type { ShellModel, ShellState } from './lib/shell-model.ts';
  import { messages } from './lib/localization.ts';
  let {model,view,picked,onpreview,onreset,onmode,paintState,onpaintstop,onadjust}: {model:ShellModel;view:ShellState;picked?:{position:Position;offsetX:number|null;offsetY:number|null;reverse:boolean;append?:boolean;painting?:boolean;soft?:boolean};onpreview:(p:Position|undefined,end?:Position,sites?:{position:Position;skipped:boolean}[])=>void;onreset:()=>void;onmode:(rectangle:boolean,strategy?:string)=>void;paintState:PaintState;onpaintstop:()=>void;onadjust:(p:Position,direction:'raise'|'lower',continuous:boolean)=>void}=$props();
  let scope=$state<'rectangle'|'terrace'|'authoring'|'paint'>('paint');
  let terraceBatch=$state(false);
  let modeReview=$state(false);
  let modeFence=$state<string>();
  let modeUncertain=$state(false);
  const activeMode=$derived(view.read?.terrain?.constructionMode??false);
  async function changeMode(){
    const read=view.read;if(!read?.terrain||view.role!=='owner'||view.pending||modeUncertain)return;
    if(modeFence!==`${read.worldEpoch}/${railwayRevision(read)!}`){modeReview=false;onreset();return;}
    const accepted=await model.send({kind:'setAuthoringMode',expectedGeometryRevision:railwayRevision(read)!,enabled:!activeMode});
    if(accepted)modeReview=false;else if(view.feedback==='unresolved')modeUncertain=true;
  }
  $effect(()=>{if(modeFence&&(modeFence!==`${view.read?.worldEpoch}/${railwayRevision(view.read)}`||view.role!=='owner'||view.connection!=='ready')){modeReview=false;modeFence=undefined;onreset();}});
  let rectangleControls=$state<{cancelDraft:()=>boolean}>();
  const text=$derived(messages[view.locale]);
  let x=$state(0),y=$state(0),z=$state(1),action=$state<'raise'|'lower'>('raise'),terraceAction=$state<'placeTerrace'|'removeTerrace'|'placeCut'|'removeCut'>('placeTerrace');
  let result=$state<CornerTerrainPreview>(),query:CornerTerrainQuery|undefined;
  let submitting=$state(false);
  let status=$state<'idle'|'checking'|'ready'|'failed'|'stale'>('idle'),uncertain=$state(false);
  let selected=$state(false);
  let requestFence=$state<string>();
  let version=0,request:AbortController|undefined;
  const blocked=$derived(view.pending||view.connection!=='ready'||view.role==='observer'||uncertain);
  export function cancelDraft(){if(paintState.active){onpaintstop();return true;}if(modeReview){modeReview=false;modeFence=undefined;onreset();return true;}if(scope==='rectangle'||scope==='authoring'||scope==='terrace'&&terraceBatch)return rectangleControls?.cancelDraft()??false;if(selected||status!=='idle'){clear();onreset();return true;}return false;}
  function clear(){selected=false;reset();}
  function reset(){requestFence=undefined;version++;request?.abort();result=undefined;query=undefined;status='idle';uncertain=false;onpreview(undefined);}
  async function check(p?:typeof picked) {
    if(blocked)return;reset();const id=version,read=view.read;if(!read?.terrain)return;
    if(p){x=p.position.x;y=p.position.y;z=p.position.z;}
    if(![x,y,z].every(Number.isSafeInteger)){selected=false;status='failed';return;}
    selected=true;
    const target:CornerTerrainQuery={expectedWorldEpoch:read.worldEpoch,expectedGeometryRevision:railwayRevision(read)!,
      position:{x,y,z},stroke:null,action:scope==='terrace'?terraceAction:p?.reverse?(action==='raise'?'lower':'raise'):action,offsetX:scope==='terrace'?null:p?.offsetX??null,offsetY:scope==='terrace'?null:p?.offsetY??null};
    requestFence=`${read.worldEpoch}/${railwayRevision(read)!}`;status='checking';request=new AbortController();
    try{
      const preview=await model.previewCorner(target,request.signal);
      if(id!==version||read.worldEpoch!==view.read?.worldEpoch||target.expectedGeometryRevision!==railwayRevision(view.read))return;
      query=target;result=preview;status='ready';onpreview(preview.position);
    }catch{if(id===version)status='failed';}
  }
  function adjust(direction:'raise'|'lower',continuous:boolean){
    if(!selected||view.connection!=='ready'||view.role==='observer'||uncertain)return;
    reset();onadjust({x,y,z},direction,continuous);
  }
  let holdTimer:ReturnType<typeof setTimeout>|undefined;
  let heldDirection:'raise'|'lower'|undefined;
  export function cancelHold(){clearTimeout(holdTimer);holdTimer=undefined;heldDirection=undefined;}
  function beginHold(direction:'raise'|'lower'){
    cancelHold();heldDirection=direction;
    holdTimer=setTimeout(()=>{holdTimer=undefined;adjust(direction,true);},200);
  }
  function releaseHold(){
    const single=holdTimer!==undefined,direction=heldDirection;
    onpaintstop();
    if(single&&direction)adjust(direction,false);
  }
  function hold(event:PointerEvent,direction:'raise'|'lower'){
    if(event.button!==0)return;
    event.preventDefault();event.currentTarget instanceof HTMLElement&&event.currentTarget.setPointerCapture(event.pointerId);
    beginHold(direction);
  }
  function holdKey(event:KeyboardEvent,direction:'raise'|'lower'){
    if(event.key!=='Enter'&&event.key!==' ')return;
    event.preventDefault();if(!event.repeat)beginHold(direction);
  }
  function releaseKey(event:KeyboardEvent){if(event.key==='Enter'||event.key===' '){event.preventDefault();releaseHold();}}
  async function commit(){
    if(blocked||status!=='ready'||!result?.legal||!query)return;
    const id=version,target=query;submitting=true;
    const accepted=await model.send({kind:'editCorner',query:target});
    submitting=false;if(id!==version)return;
    if(accepted){clear();onreset();}
    else if(view.feedback==='unresolved')uncertain=true;
    else {status='stale';result=undefined;onpreview(undefined);}
  }
  onMount(()=>onmode(false,'paintRaise'));
  $effect(()=>{const p=picked;if(p&&(scope==='paint'||scope==='terrace'&&!terraceBatch)&&!modeReview)untrack(()=>{if(p.painting){reset();x=p.position.x;y=p.position.y;z=p.position.z;selected=true;}else void check(p);});});
  $effect(()=>{
    if(requestFence&&!submitting&&requestFence!==`${view.read?.worldEpoch}/${railwayRevision(view.read)}`){clear();status='stale';}
    if(result&&!submitting&&(view.read?.worldEpoch!==result.worldEpoch||railwayRevision(view.read)!==result.geometryRevision)){
      version++;request?.abort();selected=false;result=undefined;status='stale';onpreview(undefined);
    }
    if(view.connection!=='ready'||view.role==='observer'){clear();}
  });
  $effect(()=>()=>{cancelHold();version++;request?.abort();onpreview(undefined);});
</script>
<div class="tool-card" use:primaryAction>
<section aria-label={text.authoringMode}>
  <h2>{text.authoringMode}</h2><p role="status" data-testid="authoring-mode">{activeMode?text.authoringOn:text.authoringOff}</p>
  <p class="hint">{text.authoringHelp}</p>
  {#if view.role==='owner'}
    {#if modeReview}
      <p>{activeMode?text.authoringExitReview:text.authoringEnterReview}</p>
      {#if !['idle','waiting','accepted'].includes(view.feedback)}<p role="status">{text[view.feedback]}</p>{/if}
      <div class="form-actions"><button class="primary" disabled={blocked||modeUncertain} onclick={changeMode}>{text.confirmAuthoringMode}</button><button disabled={view.pending} onclick={()=>{modeReview=false;onreset();}}>{text.cancel}</button></div>
    {:else}
      <button disabled={blocked||modeUncertain} onclick={()=>{clear();rectangleControls?.cancelDraft();onreset();modeFence=`${view.read?.worldEpoch}/${railwayRevision(view.read)}`;modeReview=true;}}>{activeMode?text.exitAuthoring:text.enterAuthoring}</button>
    {/if}
  {/if}
</section>
<div class="terrain-scope"><label>{text.terrainScope}<select aria-label={text.terrainScope} bind:value={scope} disabled={view.pending||modeReview} onchange={()=>{terraceBatch=false;clear();onreset();onmode(scope!=='paint',scope==='terrace'?terraceAction:scope==='paint'?(action==='raise'?'paintRaise':'paintLower'):undefined);}}><option value="paint">{text.terrainPaint}</option><option value="rectangle">{text.rectangleTool}</option><option value="terrace">{text.terraceTool}</option><option value="authoring" disabled={!activeMode}>{text.authoringDecorations}</option></select></label></div>
{#if !modeReview}
{#if scope==='terrace'&&terraceAction==='placeTerrace'}
  <label><input type="checkbox" bind:checked={terraceBatch} disabled={view.pending} onchange={()=>{clear();rectangleControls?.cancelDraft();onreset();onmode(true,'placeTerrace');}}/>{text.terraceBatch}</label>
{/if}
{#if scope==='rectangle'||scope==='authoring'||scope==='terrace'&&terraceBatch}
  {#key scope}<RectangleTerrainControls bind:this={rectangleControls} {model} {view} {picked} {onpreview} {onreset} authoring={scope==='authoring'} terrace={scope==='terrace'}/>{/key}
{:else}
<section aria-label={text.terrainTool}>
  <h2>{scope==='paint'?text.terrainPaint:scope==='terrace'?text.terraceTool:text.terrainTool}</h2>
  <p class="hint">{scope==='paint'?text.terrainPaintHelp:text.terraceHelp}</p>
  {#if scope==='paint'}
    <div class="form-actions">
      {#each ['raise','lower'] as direction}
        <button type="button" disabled={!selected||view.connection!=='ready'||view.role==='observer'||uncertain} onpointerdown={event=>hold(event,direction as 'raise'|'lower')} onpointerup={releaseHold} onpointercancel={onpaintstop} onlostpointercapture={()=>{if(heldDirection)onpaintstop();}} onblur={onpaintstop} onkeydown={event=>holdKey(event,direction as 'raise'|'lower')} onkeyup={releaseKey} onclick={event=>{if(event.detail===0)adjust(direction as 'raise'|'lower',false);}}>{direction==='raise'?text.cornerRaise:text.cornerLower}</button>
      {/each}
    </div>
    <p role="status" data-testid="paint-status" data-active={paintState.active}>{paintState.status==='blocked'?text.cornerAdjustBlocked:text[paintState.status==='painting'?'paintActive':paintState.status==='failed'?'paintFailed':'paintReady']} · {text.paintChanges}: {paintState.completed}</p>
    {#if paintState.status==='blocked'&&paintState.reason&&paintState.reason in terrainReasons}<p>{text[terrainReasons[paintState.reason as keyof typeof terrainReasons]]}</p>{/if}
    <button type="button" disabled={!paintState.active} onclick={onpaintstop}>{text.paintStop}</button>
  {/if}
  <form onsubmit={event=>{event.preventDefault();void check();}}>
    <div class="form-actions">
      {#if scope==='terrace'}
      <label>{text.cornerAction}<select aria-label={text.cornerAction} bind:value={terraceAction} disabled={view.pending||modeReview} onchange={()=>{clear();onreset();onmode(true,terraceAction);}}>
        <option value="placeTerrace">{text.placeTerrace}</option><option value="removeTerrace">{text.removeTerrace}</option><option value="placeCut">{text.placeCut}</option><option value="removeCut">{text.removeCut}</option>
      </select></label>
      {:else}
      <label>{text.cornerAction}<select aria-label={text.cornerAction} bind:value={action} disabled={view.pending} onchange={()=>{clear();onreset();if(scope==='paint'){onmode(false,action==='raise'?'paintRaise':'paintLower');}}}><option value="raise">{scope==='paint'?text.paintRaise:text.cornerRaise}</option><option value="lower">{scope==='paint'?text.paintLower:text.cornerLower}</option></select></label>
      {/if}
      <label>X<input aria-label={text.cornerX} type="number" step="1" bind:value={x} oninput={()=>{clear();onreset();}} disabled={view.pending}/></label>
      <label>Y<input aria-label={text.cornerY} type="number" step="1" bind:value={y} oninput={()=>{clear();onreset();}} disabled={view.pending}/></label>
      <label>Z<input aria-label={text.cornerZ} type="number" min="0" max={(view.read?.map.levels??1)-1} step="1" bind:value={z} oninput={()=>{clear();onreset();}} disabled={view.pending}/></label>
    </div>
    <button disabled={blocked||status==='checking'}>{scope==='terrace'?text.terraceCheck:text.cornerCheck}</button>

  </form>

  <p role="status" data-testid="corner-status">{status==='checking'?text.checkingGeometry:status==='failed'?text.geometryFailed:status==='stale'?text.cornerStale:result?result.legal?text.cornerLegal:text[terrainReasons[result.reason??'invalidInput']]:text.pickTile}</p>
  {#if result}
    {#if result.skipped.length}<p>{text.cornerSkipped}: {result.skipped.length}</p><ul>{#each result.skipped as skipped}<li>{skipped.position.x}, {skipped.position.y}, {skipped.position.z}: {text[terrainReasons[skipped.reason]]}</li>{/each}</ul>{/if}
    <p data-testid="corner-position">{result.position.x}, {result.position.y}, {result.position.z} · {result.action==='raise'?text.cornerRaise:result.action==='lower'?text.cornerLower:result.action==='repair'?text.rectangleRepair:text[result.action]}</p>
    <p>{text.cornerCost}: {result.cost}</p>
    <ul class="corner-changes">{#each result.changes as c}<li>{c.position.x}, {c.position.y}: {c.beforeGround} [{c.beforeNE},{c.beforeSE},{c.beforeSW},{c.beforeNW}] → {c.afterGround} [{c.afterNE},{c.afterSE},{c.afterSW},{c.afterNW}]{#if c.losesForest} · {text.cornerForestLoss}{/if}{#if c.losesOwnership} · {text.cornerOwnershipLoss}{/if}</li>{/each}</ul>
  {/if}
  {#if !['idle','waiting','accepted'].includes(view.feedback)}<p>{text[view.feedback]}</p>{/if}
  <div class="form-actions"><button class="primary" disabled={blocked||status!=='ready'||!result?.legal} onclick={commit}>{text.confirmTerrain}</button><button disabled={view.pending||!result} onclick={()=>{clear();onreset();}}>{text.cancelPreview}</button></div>
</section>

{/if}
{/if}
</div>
