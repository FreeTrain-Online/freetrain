<script lang="ts">
 import DecisionModal from './DecisionModal.svelte';
 import FleetControllerAssignment from './FleetControllerAssignment.svelte';
 import {messages} from './lib/localization.ts';
 import {validControllerName} from './lib/fleet-controllers.ts';
 import {fleetGroupOptions,canMoveFleetGroup} from './lib/fleet-groups.ts';
 import type {ShellModel,ShellState} from './lib/shell-model.ts';
 let {model,view,disabled=false}:{model:ShellModel;view:ShellState;disabled?:boolean}=$props();
 const text=$derived(messages[view.locale]),fleet=$derived(view.read?.mixed?.fleet);
 const blocked=$derived(disabled||view.pending||view.connection!=='ready'||view.role==='observer');
 let groupId=$state(''),trainId=$state(''),composing=$state(false);
 const group=$derived(fleet?.groups.find(g=>g.id===groupId)??fleet?.groups.find(g=>g.id===fleet.rootGroupId));
 const train=$derived(fleet?.trains.find(t=>t.id===trainId));
 const options=$derived(fleet?fleetGroupOptions(fleet,text.groupRoot):[{id:'0',label:text.groupRoot,depth:0}]);
 let draft=$state<{action:'create'|'rename'|'move';kind:'group'|'train';id:string;revision:string;name:string;epoch:string;parentId:string;parents:Map<string,string>}>();
 const target=$derived(draft?.kind==='train'?fleet?.trains.find(t=>t.id===draft!.id):fleet?.groups.find(g=>g.id===draft?.id));
 const stale=$derived(!!draft&&(draft.epoch!==view.read?.worldEpoch||draft.action!=='create'&&target?.revision!==draft.revision||draft.action!=='rename'&&
  (fleet?.groups.find(g=>g.id===draft!.parentId)?.revision??(!fleet&&draft.parentId==='0'?'0':undefined))!==draft.parents.get(draft.parentId)));
 const destinations=$derived(options.filter(o=>{
  if(draft?.action!=='move')return true;
  return draft.kind==='train'?o.id!==train?.groupId:!!fleet&&canMoveFleetGroup(fleet,draft.id,o.id);
 }));
 const valid=$derived(!!draft&&(draft.action==='move'?destinations.some(o=>o.id===draft!.parentId):validControllerName(draft.name)));
 function open(action:'create'|'rename'|'move',kind:'group'|'train'='group'){
  if(blocked||!view.read)return;const source=kind==='train'?train:group;
  if(action!=='create'&&!source)return;
  draft={action,kind,id:action==='create'?'0':source!.id,revision:action==='create'?'0':source!.revision,name:action==='rename'?source!.name:'',epoch:view.read.worldEpoch,
   parentId:action==='create'?group?.id??'0':'',parents:new Map(fleet?.groups.map(g=>[g.id,g.revision])??[['0','0']])};composing=false;
 }
 async function apply(){
  if(!draft||blocked||stale||!valid||composing)return;const current=draft;
  const ok=await model.send({kind:'manageFleetGroup',action:current.action,targetKind:current.kind,targetId:current.id,expectedRevision:current.revision,
   name:current.action==='move'?'':current.name,parentId:current.action==='rename'?null:current.parentId,expectedParentRevision:current.action==='rename'?null:current.parents.get(current.parentId)!});
  if(ok&&draft===current){if(current.action==='move'&&current.kind==='train')trainId=current.id;draft=undefined;}
 }
</script>
<section class="fleet-group-manager" aria-label={text.controllerGroup}>
 <p class="hint">{text.groupHelp}</p>
 <label class="field">{text.controllerGroup}<select aria-label={text.controllerGroup} value={group?.id??'0'} onchange={event=>{groupId=event.currentTarget.value;}} disabled={view.pending}>
  {#each options as o}<option value={o.id}>{o.label}</option>{/each}
 </select></label>
 <div class="form-actions" data-testid="group-actions">
  <button disabled={blocked||!view.read||!!fleet&&fleet.groups.length>=256} onclick={()=>open('create')}>{text.groupCreate}</button>
  <button disabled={blocked||!group} onclick={()=>open('rename')}>{text.groupRename}</button>
  <button disabled={blocked||!group||group.id===fleet?.rootGroupId||!fleet?.groups.some(g=>canMoveFleetGroup(fleet,group!.id,g.id))} onclick={()=>open('move')}>{text.groupMove}</button>
 </div>
 {#if group}<FleetControllerAssignment {model} {view} targetKind="group" targetId={group.id} disabled={blocked}/>{/if}
 {#if fleet?.trains.length}
  <div class="fleet-group-trains">
   <label class="field">{text.groupTrain}<select aria-label={text.groupTrain} bind:value={trainId} disabled={view.pending}>
    <option value="">{text.controllerChoose}</option>
    {#each fleet.trains as t}<option value={t.id}>{t.name} · #{t.id}</option>{/each}
   </select></label>
   {#if train}
    <p>{text.groupCurrent}: {options.find(o=>o.id===train.groupId)?.label}</p>
    <button disabled={blocked||!fleet.groups.some(g=>g.id!==train.groupId)} onclick={()=>open('move','train')}>{text.groupMove}</button>
    <FleetControllerAssignment {model} {view} targetId={train.id} disabled={blocked}/>
   {/if}
  </div>
 {/if}
</section>
{#if draft}
 <DecisionModal leftAligned title={text[draft.action==='create'?'groupCreate':draft.action==='rename'?'groupRename':'groupMove']} oncancel={()=>{if(!view.pending)draft=undefined;}}>
  {#if draft.action!=='create'}<p>{target?.name??draft.id} · #{draft.id}</p>{/if}
  {#if draft.action!=='move'}
   <label class="field">{text.groupName}<input aria-label={text.groupName} maxlength="256" bind:value={draft.name} disabled={blocked} oncompositionstart={()=>{composing=true;}} oncompositionend={()=>{composing=false;}}/></label>
  {/if}
  {#if draft.action!=='rename'}
   <label class="field">{text.groupParent}<select aria-label={text.groupParent} bind:value={draft.parentId} disabled={blocked}>
    {#if draft.action==='move'}<option value="">{text.controllerChoose}</option>{/if}
    {#each destinations as o}<option value={o.id}>{o.label}</option>{/each}
   </select></label>
   {#if draft.action==='move'}<p class="hint">{text.groupMoveHelp}</p>{/if}
  {/if}
  {#if stale}<p role="alert">{text.groupConflict}</p>{/if}
  {#if !['idle','waiting','accepted'].includes(view.feedback)}<p role="alert">{text[view.feedback]}</p>{/if}
  {#snippet actions()}
   <button class="primary" disabled={blocked||stale||!valid||composing} onclick={apply}>{text.controllerApply}</button>
   <button disabled={view.pending} onclick={()=>{draft=undefined;}}>{text.cancel}</button>
  {/snippet}
 </DecisionModal>
{/if}
<style>
 .fleet-group-manager{min-width:0;}
 .fleet-group-trains{border-top:1px solid var(--border,#aaa);margin-top:1rem;padding-top:1rem;}
</style>
