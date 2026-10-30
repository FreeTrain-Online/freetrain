<script lang="ts">
  import { untrack } from 'svelte';
  import type { CornerTerrainQuery, CornerTerrainPreview, Position } from './generated/contracts.ts';
  import type { ShellModel, ShellState } from './lib/shell-model.ts';
  import { messages } from './lib/localization.ts';
  import { terrainReasons } from './lib/terrain-edit.ts';
  let {model,view,picked,onpreview,onreset}: {model:ShellModel;view:ShellState;picked?:{position:Position;reverse:boolean;soft?:boolean};onpreview:(p:Position|undefined,end?:Position)=>void;onreset:()=>void}=$props();
  const text=$derived(messages[view.locale]);
  let x=$state(0),y=$state(0),z=$state(1),endX=$state(1),endY=$state(1);
  let action=$state<'raise'|'lower'|'repair'>('raise'),soft=$state(false),anchor=$state<Position>();
  let result=$state<CornerTerrainPreview>(),query:CornerTerrainQuery|undefined;
  let submitting=$state(false),uncertain=$state(false);
  let status=$state<'idle'|'checking'|'ready'|'failed'|'stale'>('idle');
  let anchorFence:string|undefined;
  let version=0,request:AbortController|undefined;
  const blocked=$derived(view.pending||view.connection!=='ready'||view.role==='observer'||uncertain);
  export function cancelDraft(){if(anchor||status!=='idle'){clear();onreset();return true;}return false;}
  function reset(){version++;request?.abort();query=undefined;result=undefined;status='idle';uncertain=false;onpreview(undefined);}
  function clear(){anchor=undefined;anchorFence=undefined;reset();}
  async function check(mode=action,profile=soft){
    if(blocked)return;reset();const id=version,read=view.read;if(!read?.terrain)return;
    if(![x,y,z,endX,endY].every(Number.isSafeInteger)){status='failed';return;}
    const target:CornerTerrainQuery={expectedWorldEpoch:read.worldEpoch,expectedGeometryRevision:read.terrain.geometryRevision,
      position:{x,y,z},action:mode,offsetX:null,offsetY:null,stroke:null,rectangle:{end:{x:endX,y:endY,z},soft:profile}};
    status='checking';request=new AbortController();
    try{
      const preview=await model.previewCorner(target,request.signal);
      if(id!==version||read.worldEpoch!==view.read?.worldEpoch||target.expectedGeometryRevision!==view.read?.terrain?.geometryRevision)return;
      query=target;result=preview;status='ready';onpreview(target.position,target.rectangle!.end);
    }catch{if(id===version)status='failed';}
  }
  function pick(p:NonNullable<typeof picked>){
    if(blocked)return;
    if(!anchor){reset();x=p.position.x;y=p.position.y;z=p.position.z;anchor={x,y,z};anchorFence=`${view.read?.worldEpoch}/${view.read?.terrain?.geometryRevision}`;onpreview(anchor);}
    else {endX=p.position.x;endY=p.position.y;anchor=undefined;void check(p.reverse?'repair':action,p.soft||soft);}
  }
  async function commit(){
    if(blocked||status!=='ready'||!result?.legal||!query)return;
    const id=version,target=query;submitting=true;
    const accepted=await model.send({kind:'editCorner',query:target});
    submitting=false;if(id!==version)return;
    if(accepted){clear();onreset();}
    else if(view.feedback==='unresolved')uncertain=true;
    else {status='stale';result=undefined;onpreview(undefined);}
  }
  $effect(()=>{const p=picked;if(p)untrack(()=>pick(p));});
  $effect(()=>{
    if(anchor&&anchorFence!==`${view.read?.worldEpoch}/${view.read?.terrain?.geometryRevision}`){clear();status='stale';}
    if(result&&!submitting&&(view.read?.worldEpoch!==result.worldEpoch||view.read?.terrain?.geometryRevision!==result.geometryRevision)){
      reset();anchor=undefined;status='stale';
    }
    if(view.connection!=='ready'||view.role==='observer')clear();
  });
  $effect(()=>()=>{version++;request?.abort();onpreview(undefined);});
</script>
<section aria-label={text.rectangleTool}>
  <h2>{text.rectangleTool}</h2><p class="hint">{text.rectangleHelp}</p>
  <form onsubmit={event=>{event.preventDefault();anchor=undefined;void check();}}>
    <label>{text.cornerAction}<select aria-label={text.cornerAction} bind:value={action} disabled={blocked} onchange={clear}><option value="raise">{text.rectangleRaise}</option><option value="lower">{text.rectangleLower}</option><option value="repair">{text.rectangleRepair}</option></select></label>
    <div class="form-actions">
      <label>X<input aria-label={text.cornerX} type="number" step="1" bind:value={x} oninput={clear} disabled={blocked}/></label>
      <label>Y<input aria-label={text.cornerY} type="number" step="1" bind:value={y} oninput={clear} disabled={blocked}/></label>
      <label>Z<input aria-label={text.cornerZ} type="number" min="0" max={(view.read?.map.levels??1)-1} step="1" bind:value={z} oninput={clear} disabled={blocked}/></label>
      <label>{text.rectangleEndX}<input aria-label={text.rectangleEndX} type="number" step="1" bind:value={endX} oninput={clear} disabled={blocked}/></label>
      <label>{text.rectangleEndY}<input aria-label={text.rectangleEndY} type="number" step="1" bind:value={endY} oninput={clear} disabled={blocked}/></label>
    </div>
    <label><input type="checkbox" bind:checked={soft} disabled={blocked||action!=='raise'} onchange={reset}/>{text.rectangleSoft}</label>
    <button disabled={blocked||status==='checking'}>{text.rectangleCheck}</button>
  </form>
  <p role="status" data-testid="rectangle-status">{status==='checking'?text.checkingGeometry:status==='failed'?text.rectangleInvalid:status==='stale'?text.cornerStale:result?result.legal?text.cornerLegal:text[terrainReasons[result.reason??'invalidInput']]:anchor?text.rectanglePickEnd:text.pickTile}</p>
  {#if result}
    <p data-testid="rectangle-position">{x}, {y}, {z} → {endX}, {endY}, {z} · {result.action==='repair'?text.rectangleRepair:result.action==='raise'?text.rectangleRaise:text.rectangleLower}</p>
    <p>{text.cornerCost}: {result.cost}</p>
    {#if result.skipped.length}<p>{text.cornerSkipped}: {result.skipped.length}</p><ul>{#each result.skipped as skipped}<li>{skipped.position.x}, {skipped.position.y}, {skipped.position.z}: {text[terrainReasons[skipped.reason]]}</li>{/each}</ul>{/if}
    <ul class="corner-changes">{#each result.changes as c}<li>{c.position.x}, {c.position.y}: {c.beforeGround} [{c.beforeNE},{c.beforeSE},{c.beforeSW},{c.beforeNW}] → {c.afterGround} [{c.afterNE},{c.afterSE},{c.afterSW},{c.afterNW}]{#if c.losesForest} · {text.cornerForestLoss}{/if}{#if c.losesOwnership} · {text.cornerOwnershipLoss}{/if}</li>{/each}</ul>
  {/if}
  {#if !['idle','waiting','accepted'].includes(view.feedback)}<p>{text[view.feedback]}</p>{/if}
  <div class="form-actions"><button class="primary" disabled={blocked||status!=='ready'||!result?.legal} onclick={commit}>{text.confirmTerrain}</button><button disabled={view.pending} onclick={()=>{clear();onreset();}}>{text.cancelPreview}</button></div>
</section>
