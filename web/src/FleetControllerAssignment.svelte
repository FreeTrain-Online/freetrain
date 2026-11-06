<script lang="ts">
 import {messages} from './lib/localization.ts';
 import {effectiveFleetController} from './lib/fleet-state.ts';
 import type {ShellModel,ShellState} from './lib/shell-model.ts';
 let {model,view,targetId,targetKind='train',disabled=false}:{model:ShellModel;view:ShellState;targetId:string;targetKind?:'train'|'group';disabled?:boolean}=$props();
 const text=$derived(messages[view.locale]),fleet=$derived(view.read?.mixed?.fleet);
 const target=$derived(targetKind==='train'?fleet?.trains.find(t=>t.id===targetId):fleet?.groups.find(g=>g.id===targetId));
 const effective=$derived(targetKind==='train'&&fleet?effectiveFleetController(fleet,targetId):undefined);
 const label=(c:NonNullable<typeof fleet>['controllers'][number])=>c.kind==='simple'?text.automatic:c.kind==='delegation'?text.controllerDelegation:c.name;
 async function assign(event:Event){
  const select=event.currentTarget as HTMLSelectElement,controller=fleet?.controllers.find(c=>c.id===select.value);
  select.value=target?.controllerId??'';
  if(!target||!controller)return;
  await model.send({kind:'assignFleetController',targetKind,targetId:target.id,expectedRevision:target.revision,
   controllerId:controller.id,expectedControllerRevision:controller.revision});
 }
</script>
{#if fleet&&target}
 <label class="field">{text.sharedController}<select aria-label={text.sharedController} value={target.controllerId??''}
  disabled={disabled||view.pending||view.connection!=='ready'||view.role==='observer'} onchange={assign}>
  {#if target.controllerId===null}<option value="" disabled>{text.automatic}</option>{/if}
  {#each fleet.controllers as c}<option value={c.id}>{label(c)} · #{c.id}</option>{/each}
 </select></label>
 {#if effective}<p class="hint" data-testid="effective-controller">{text.effectiveController}: {label(effective)} · #{effective.id}</p>{/if}
{/if}
