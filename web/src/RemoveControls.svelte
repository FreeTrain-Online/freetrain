<script lang="ts">
  import { untrack } from 'svelte';
  import { primaryAction } from './lib/primary-action.ts';
  import { messages } from './lib/localization.ts';
  import { railwayFence, railwayRevision, railwayReasons, railwayStartLevel } from './lib/railway-edit.ts';
  import { removalAt, removalQuery } from './lib/object-removal.ts';
  import type { ShellModel, ShellState } from './lib/shell-model.ts';
  import type { Selection } from './lib/map-renderer.ts';
  import type { Position, RemovalPreview, RemovalQuery } from './generated/contracts.ts';
  let {model,view,picked,level=$bindable(1),heightCut,onpreview,onpoints,onreset,onreveal}:
    {model:ShellModel;view:ShellState;picked?:Selection|Position;level?:number;heightCut:number;onpreview:(preview?:RemovalPreview)=>void;onpoints:(points:{position:Position;label:string}[])=>void;onreset:()=>void;onreveal:()=>void}=$props();
  const text=$derived(messages[view.locale]);
  const blocked=$derived(view.pending||view.connection!=='ready'||view.role==='observer'||view.feedback==='unresolved');
  let mode=$state<'single'|'line'>('single'),point=$state<'from'|'to'>('from');
  let ax=$state(0),ay=$state(0),bx=$state(0),by=$state(0),hasA=$state(false),hasB=$state(false);
  let autoLevel=$state(true),autoCoordinates=true;
  let result=$state<RemovalPreview>();
  let status=$state<'idle'|'checking'|'ready'|'failed'|'stale'|'terrain'>('idle');
  let fence=$state<string>(),submitting=$state(false);
  let version=0,request:AbortController|undefined;
  const kinds={terrain:'terrain',forest:'removeForest',creek:'removeCreek',terrace:'removeTerraceObject',slope:'removeSlope',pier:'removePier',railway:'railway',line:'removeLine'} as const;
  const reasons={...railwayReasons,terrainRequired:'removeTerrainHelp',noRailway:'removeNoRailway',nothingRemovable:'removeNothing',operationUnavailable:'removeUnavailable'} as const;
  function reset(){version++;request?.abort();result=undefined;fence=undefined;status='idle';onpreview();}
  function points(){onpoints([...(hasA?[{position:{x:ax,y:ay,z:level},label:'A'}]:[]),...(mode==='line'&&hasB?[{position:{x:bx,y:by,z:level},label:'B'}]:[])]);}
  function edit(){reset();points();}
  function detectLevel(){if(autoLevel&&autoCoordinates&&view.read){const z=railwayStartLevel(view.read,{x:ax,y:ay},heightCut);if(z!==undefined){level=z;autoCoordinates=false;}}}
  function editStart(){hasA=true;autoCoordinates=true;edit();}
  function choosePoint(next:'from'|'to'){reset();if(next==='to'&&hasA)detectLevel();point=next;points();}
  export function pickMode(){return mode==='single'&&autoLevel?'object':autoLevel&&point==='from'?'auto':'level';}
  export function cancelDraft(){const had=hasA||hasB||status!=='idle';reset();hasA=false;hasB=false;point='from';autoCoordinates=true;onpoints([]);onreset();return had;}
  async function check(selected?:Selection){
    reset();if(blocked||!view.read)return;
    const read=view.read,revision=railwayRevision(read);if(revision===undefined)return;
    detectLevel();hasA=true;if(mode==='line')hasB=true;points();
    if(![ax,ay,level,...(mode==='line'?[bx,by]:[])].every(Number.isSafeInteger)){status='failed';return;}
    const query:RemovalQuery|undefined=mode==='line'?{
      expectedWorldEpoch:read.worldEpoch,expectedGeometryRevision:revision,expectedContentVersion:read.contentVersion as RemovalQuery['expectedContentVersion'],
      position:{x:ax,y:ay,z:level},end:{x:bx,y:by,z:level},expectedEntityId:'0',expectedEntityRevision:'0',
    }:selected?removalQuery(read,selected):removalAt(read,{x:ax,y:ay,z:level});
    if(!query){status='terrain';return;}
    fence=railwayFence(read);status='checking';request=new AbortController();const id=version;
    try{
      const preview=await model.previewRemoval(query,request.signal);
      if(id!==version||fence!==railwayFence(view.read)||blocked)return;
      result=preview;status='ready';onpreview(preview);
    }catch{if(id===version)status='failed';}
  }
  async function commit(){
    if(blocked||status!=='ready'||!result?.legal||fence!==railwayFence(view.read))return;
    const id=version;submitting=true;
    const accepted=await model.send({kind:'removeObject',query:result.query});submitting=false;
    if(id!==version)return;
    if(accepted)cancelDraft();else {reset();status='stale';}
  }
  $effect(()=>{const p=picked;if(p)untrack(()=>{
    if(blocked)return;
    const position='kind' in p?p.position:p;
    reset();
    if(mode==='single'||point==='from'){
      ax=position.x;ay=position.y;if(autoLevel)level=position.z;hasA=true;autoCoordinates=false;
      if(mode==='line')point='to';
    }else{bx=position.x;by=position.y;hasB=true;}
    points();if(mode==='single'||hasA&&hasB)void check(mode==='single'&&'kind' in p?p:undefined);
  });});
  $effect(()=>{
    const current=railwayFence(view.read);
    if(fence&&fence!==current&&!submitting){reset();status='stale';}
    if(view.connection!=='ready'||view.role==='observer')untrack(()=>cancelDraft());
  });
  $effect(()=>()=>{version++;request?.abort();onpreview();onpoints([]);});
</script>

<section class="tool-card track-task remove-task" use:primaryAction aria-label={text.removeTool}>
  <h2>{text.removeTool}</h2>
  <label>{text.removeMode}<select aria-label={text.removeMode} bind:value={mode} disabled={view.pending} onchange={cancelDraft}><option value="single">{text.removeSingle}</option><option value="line">{text.removeLine}</option></select></label>
  <p class="hint">{text[mode==='line'?'removeLineHelp':'removeHelp']}</p>
  <form onsubmit={event=>{event.preventDefault();void check();}}>
    <div class="form-actions">
      <label>{text.removeLevel}<input aria-label={text.removeLevel} type="number" min="0" max={(view.read?.map.levels??1)-1} step="1" bind:value={level} disabled={view.pending} oninput={event=>{autoLevel=false;level=event.currentTarget.valueAsNumber;edit();}}/></label>
      <label><input type="checkbox" aria-label={text.railAutoLevel} bind:checked={autoLevel} disabled={view.pending} onchange={()=>{autoCoordinates=true;edit();}}/>{text.railAutoLevel}</label>
    </div>
    <fieldset disabled={view.pending}><legend>{text[mode==='line'?'railFrom':'removeSingle']}</legend><div class="form-actions">
      <label>X<input aria-label={text[mode==='line'?'railFromX':'removeX']} type="number" step="1" bind:value={ax} oninput={editStart}/></label>
      <label>Y<input aria-label={text[mode==='line'?'railFromY':'removeY']} type="number" step="1" bind:value={ay} oninput={editStart}/></label>
      {#if mode==='line'}<button type="button" aria-pressed={point==='from'} onclick={()=>choosePoint('from')}>{text.railPickFrom}</button>{/if}
    </div></fieldset>
    {#if mode==='line'}
      <fieldset disabled={view.pending}><legend>{text.railTo}</legend><div class="form-actions">
        <label>X<input aria-label={text.railToX} type="number" step="1" bind:value={bx} oninput={()=>{hasB=true;edit();}}/></label>
        <label>Y<input aria-label={text.railToY} type="number" step="1" bind:value={by} oninput={()=>{hasB=true;edit();}}/></label>
        <button type="button" aria-pressed={point==='to'} onclick={()=>choosePoint('to')}>{text.railPickTo}</button>
      </div></fieldset>
    {/if}
    <button disabled={blocked||status==='checking'}>{text.previewRemoval}</button>
  </form>
  <p role="status" data-testid="remove-status">{status==='checking'?text.checkingGeometry:status==='failed'?text.geometryFailed:status==='stale'?text.removeStale:status==='terrain'?text.removeTerrainHelp:result?result.legal?text.removeReady:text[reasons[result.reason as keyof typeof reasons]??'removeUnavailable']:text[mode==='line'?(point==='from'?'railPickFrom':'railPickTo'):'removePick']}</p>
  {#if hasA}<p data-testid="remove-target">{result?text[kinds[result.objectKind]]:text[mode==='line'?'removeLine':'removeSingle']} · {ax}, {ay}, {level}{#if mode==='line'&&hasB} → {bx}, {by}, {level}{/if}</p>{/if}
  {#if result?.positions.length}
    <details class="rail-losses"><summary>{text.removeAffected} ({result.positions.length})</summary><ul>{#each result.positions as p}<li>{p.x}, {p.y}, {p.z}</li>{/each}</ul></details>
  {/if}
  {#if result?.skipped.length}
    <details open><summary>{text.removeSkipped} ({result.skipped.length})</summary><ul class="remove-skips">{#each result.skipped as s}<li>{s.position.x}, {s.position.y}, {s.position.z}: {text[reasons[s.reason as keyof typeof reasons]??'removeUnavailable']}</li>{/each}</ul></details>
  {/if}
  {#if level>heightCut}<button onclick={onreveal}>{text.revealPreview}</button>{/if}
  {#if !['idle','accepted','waiting'].includes(view.feedback)}<p>{text[view.feedback]}</p>{/if}
  <div class="form-actions"><button class="primary" disabled={blocked||status!=='ready'||!result?.legal} onclick={commit}>{text.confirmRemoval}</button><button disabled={view.pending||!hasA&&!hasB&&status==='idle'} onclick={cancelDraft}>{text.cancelPreview}</button></div>
</section>
