<script lang="ts">
  import { untrack } from 'svelte';
  import { primaryAction } from './lib/primary-action.ts';
  import type { Position, SlopePreview, SlopeQuery } from './generated/contracts.ts';
  import type { ShellModel, ShellState } from './lib/shell-model.ts';
  import { messages } from './lib/localization.ts';
  import { railwayFence, railwayRevision, railwayReasons, railwayStartLevel } from './lib/railway-edit.ts';
  let {model,view,picked,level=$bindable(1),onpreview,onpoints,onreveal,heightCut}:
    {model:ShellModel;view:ShellState;picked?:Position;level?:number;onpreview:(p?:SlopePreview)=>void;onpoints:(p:{position:Position;label:string}[])=>void;onreveal:(level:number)=>void;heightCut:number}=$props();
  const text=$derived(messages[view.locale]);
  let action=$state<'lay'|'remove'>('lay'),climb=$state<0|2|4|6>(2);
  let x=$state(0),y=$state(0),hasTarget=$state(false),autoLevel=$state(true);
  let result=$state<SlopePreview>(),status=$state<'idle'|'checking'|'ready'|'failed'|'stale'>('idle');
  let fence=$state<string>(),submitting=$state(false);
  let version=0,request:AbortController|undefined;
  const blocked=$derived(view.pending||view.connection!=='ready'||view.role==='observer'||view.feedback==='unresolved');
  function reset(){version++;request?.abort();fence=undefined;result=undefined;status='idle';onpreview();}
  function points(){onpoints(hasTarget?[{position:{x,y,z:level},label:'A'}]:[]);}
  function edit(){reset();points();}
  export function usesAutomaticPick(){return autoLevel||action==='remove';}
  export function cancelDraft(){const had=hasTarget||status!=='idle';reset();hasTarget=false;onpoints([]);return had;}
  async function check(detect=true){
    if(blocked)return;reset();const read=view.read,revision=railwayRevision(read);if(!read||revision===undefined)return;
    if(detect&&autoLevel){const z=railwayStartLevel(read,{x,y},heightCut);if(z!==undefined)level=z;}
    if(![x,y,level].every(Number.isSafeInteger)){status='failed';return;}
    hasTarget=true;points();
    const query:SlopeQuery={expectedWorldEpoch:read.worldEpoch,expectedGeometryRevision:revision,expectedContentVersion:read.contentVersion as SlopeQuery['expectedContentVersion'],position:{x,y,z:level},action,climb};
    const id=version;fence=railwayFence(read);status='checking';request=new AbortController();
    try{
      const preview=await model.previewSlope(query,request.signal);
      if(id!==version||fence!==railwayFence(view.read)||blocked)return;
      result=preview;status='ready';onpreview(preview);
    }catch{if(id===version){status='failed';onpreview();}}
  }
  async function commit(){
    if(blocked||status!=='ready'||!result?.legal||fence!==railwayFence(view.read))return;
    const id=version,query=result.query;submitting=true;
    const accepted=await model.send({kind:'editSlope',query});submitting=false;
    if(id!==version)return;
    if(accepted)cancelDraft();else {reset();status='stale';}
  }
  $effect(()=>{const p=picked;if(p)untrack(()=>{
    if(blocked)return;
    reset();x=p.x;y=p.y;if(usesAutomaticPick())level=p.z;hasTarget=true;
    if(action==='remove'){
      const member=view.read?.mixed?.voxels.find(v=>v.position.x===x&&v.position.y===y&&v.position.z===level);
      const owner=view.read?.mixed?.slopes.find(s=>s.entityId===member?.ownerId);
      if(owner)climb=owner.climb as typeof climb;
    }
    points();void check(false);
  });});
  $effect(()=>{
    const current=railwayFence(view.read);
    if(fence&&fence!==current&&!submitting){reset();status='stale';}
    if(view.connection!=='ready'||view.role==='observer')untrack(()=>cancelDraft());
  });
  $effect(()=>()=>{version++;request?.abort();onpreview();onpoints([]);});
</script>
<section class="tool-card track-task slope-task" use:primaryAction aria-label={text.slopeTask}>
  <h2>{text.slopeTask}</h2><p class="hint">{text.slopeHelp}</p>
  <form onsubmit={event=>{event.preventDefault();void check();}}>
    <div class="form-actions">
      <label>{text.railAction}<select aria-label={text.railAction} bind:value={action} disabled={view.pending} onchange={edit}><option value="lay">{text.railLay}</option><option value="remove">{text.railRemove}</option></select></label>
      <label>{text.slopeDirection}<select aria-label={text.slopeDirection} bind:value={climb} disabled={view.pending} onchange={edit}>{#each [0,2,4,6] as d}<option value={d}>{text[(['slopeNorth','slopeEast','slopeSouth','slopeWest'] as const)[d/2]]}</option>{/each}</select></label>
    </div>
    <div class="form-actions">
      <label>X<input aria-label={text.slopeX} type="number" step="1" bind:value={x} disabled={view.pending} oninput={()=>{hasTarget=true;edit();}}/></label>
      <label>Y<input aria-label={text.slopeY} type="number" step="1" bind:value={y} disabled={view.pending} oninput={()=>{hasTarget=true;edit();}}/></label>
      <label>{text.slopeLevel}<input aria-label={text.slopeLevel} type="number" min="0" max={(view.read?.map.levels??1)-1} step="1" bind:value={level} disabled={view.pending} oninput={event=>{autoLevel=false;level=event.currentTarget.valueAsNumber;edit();}}/></label>
    </div>
    <label><input type="checkbox" aria-label={text.railAutoLevel} bind:checked={autoLevel} disabled={view.pending} onchange={edit}/>{text.railAutoLevel}</label>
    <button disabled={blocked||status==='checking'}>{text.slopeCheck}</button>
  </form>
  <p role="status" data-testid="slope-status">{status==='checking'?text.checkingGeometry:status==='failed'?text.geometryFailed:status==='stale'?text.railStale:result?result.legal?text.railLegal:text[railwayReasons[result.reason as keyof typeof railwayReasons]??'geometryFailed']:text.slopePick}</p>
  {#if result}
    <p data-testid="slope-base">{text.slopeBase}: {result.base.x}, {result.base.y}, {result.base.z} → {result.base.z+1}</p>
    <details><summary>{text.slopeFootprint} ({result.members.length})</summary><ul class="rail-losses">{#each result.members as member}<li>{member.position.x}, {member.position.y}, {member.position.z} · {text[member.kind==='traffic'?'slopeRail':member.kind==='slopefiller'?'slopeInvisible':member.kind==='slopewall'?'slopeWall':'slopeSupport']}</li>{/each}</ul></details>
    {#if result.losses.length}<details open><summary>{text.railLossDetails}</summary><ul class="rail-losses" data-testid="slope-losses">{#each result.losses as loss}<li>{text[loss.kind==='pier'?'railPierLoss':'railAccessoryLoss']}: {loss.position.x}, {loss.position.y}, {loss.position.z} · {text.owner}: {loss.ownerId}{#if loss.supportedOwnerId} · {text.railwaySupportedOwner}: {loss.supportedOwnerId}{/if}</li>{/each}</ul></details>{/if}
    {#if result.members.some(m=>m.position.z>heightCut)}<button onclick={()=>onreveal(Math.max(...result!.members.map(m=>m.position.z)))}>{text.revealPreview}</button>{/if}
  {/if}
  <details><summary>{text.slopeDetails}</summary><p class="hint">{text.slopeRemovalHelp}</p><p class="hint">{text.slopeGhostHelp}</p><p class="hint">{text.railCutHelp}</p></details>
  {#if !['idle','waiting','accepted'].includes(view.feedback)}<p role="status">{text[view.feedback]}</p>{/if}
  <div class="track-confirm">
    {#if result}<p data-testid="slope-cost">{text.cornerCost}: {BigInt(result.cost).toLocaleString(view.locale)} · {text.railCells}: {result.members.length} · {text.railPiers}: {result.addedPiers.length} · {text.railLosses}: {result.losses.length}</p>{/if}
    <div class="form-actions"><button class="primary" disabled={blocked||status!=='ready'||!result?.legal} onclick={commit}>{text.slopeConfirm}</button><button disabled={view.pending||(!hasTarget&&!result)} onclick={cancelDraft}>{text.cancelPreview}</button></div>
  </div>
</section>
