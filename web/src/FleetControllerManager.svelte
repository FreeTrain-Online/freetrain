<script lang="ts">
 import FleetRuleEditor from './FleetRuleEditor.svelte';
 import DecisionModal from './DecisionModal.svelte';
 import FleetControllerAssignment from './FleetControllerAssignment.svelte';
 import {messages} from './lib/localization.ts';
 import {controllerUsages,controllerCopyName,validControllerName,missingControllerJunctions} from './lib/fleet-controllers.ts';
 import profile from './generated/fleet-profile.json' with {type:'json'};
 import type {FleetController,FleetHarbor,FleetJunction,Position} from './generated/contracts.ts';
 import type {ShellModel,ShellState} from './lib/shell-model.ts';
 let {model,view,disabled=false,location}:{model:ShellModel;view:ShellState;disabled?:boolean;location?:Position}=$props();
 const text=$derived(messages[view.locale]),fleet=$derived(view.read?.mixed?.fleet);
 const blocked=$derived(disabled||view.pending||view.connection!=='ready'||view.role==='observer');
 const instances=$derived(fleet?.controllers.filter(c=>c.provider!==null)??[]);
 let selectedId=$state(''),groupId=$state('');
 let ruleTarget=$state<{controllerId:string;location:Position;settingKind:'harbor'|'junction';epoch:string}>();
 function openRules(){
  const target=setting??junction;if(!draft?.source||!target||changed)return;
  ruleTarget={controllerId:draft.source.id,location:{...target.location},settingKind:configurationKind,epoch:draft.epoch};draft=undefined;
 }
 const selected=$derived(instances.find(c=>c.id===selectedId));
 const usages=$derived(fleet&&selected?controllerUsages(fleet,selected.id):undefined);
 const group=$derived(fleet?.groups.find(g=>g.id===groupId)??fleet?.groups[0]);
 let draft=$state<{action:'create'|'rename'|'copy'|'delete'|'configure'|'cleanJunctions';source?:Pick<FleetController,'id'|'revision'|'name'|'kind'>;epoch:string;name:string;provider:string;harbors:FleetHarbor[];junctions:FleetJunction[];geometry:string;removedLocation?:Position}>();
 let configurationKind=$state<'harbor'|'junction'>('harbor');
 let x=$state(0),y=$state(0),z=$state(0),index=$state(0),composing=$state(false),changed=$state(false);
 const stale=$derived(!!draft&&((draft.action==='cleanJunctions'||draft.action==='configure'&&configurationKind==='junction')&&draft.geometry!==view.read?.mixed?.geometryRevision||draft.epoch!==view.read?.worldEpoch||!!draft.source&&!fleet?.controllers.some(c=>c.id===draft!.source!.id&&c.revision===draft!.source!.revision)));
 const draftUsages=$derived(draft?.source&&fleet?controllerUsages(fleet,draft.source.id):undefined);
 const referenced=$derived(!!draftUsages&&(draftUsages.trains.length+draftUsages.groups.length)>0);
 const setting=$derived(configurationKind==='harbor'?draft?.harbors[index]:undefined);
 const junction=$derived(configurationKind==='junction'?draft?.junctions[index]:undefined);
 const missing=$derived(view.read&&selected?missingControllerJunctions(view.read,selected.id):[]);
 const valid=$derived(!!draft&&(!['create','rename'].includes(draft.action)||validControllerName(draft.name))&&
  (draft.action!=='configure'||draft.source?.kind==='manual'||!!setting||!!junction||!!draft.removedLocation)&&
  (draft.action!=='configure'||!setting||setting.kind!=='fixed'||setting.minutes>=10)&&
  draft.harbors.every(h=>Number.isInteger(h.minutes)&&h.minutes>=0&&h.minutes<=2147483647&&(h.kind!=='daily'||h.minutes<1440)));
 const typeName=(kind:string)=>kind==='manual'?text.manual:text.controllerTimetable;
 function open(action:NonNullable<typeof draft>['action']){
  if(blocked&&action!=='configure')return;
  if(action!=='create'&&!selected)return;
  draft={action,source:action==='create'?undefined:selected,epoch:view.read!.worldEpoch,
   name:action==='copy'&&fleet&&selected?controllerCopyName(fleet,selected.id):action==='create'?'':selected!.name,
   geometry:view.read!.mixed?.geometryRevision??'0',junctions:(selected?.junctions??[]).map(j=>({...j,location:{...j.location},rules:j.rules?.map(r=>({...r}))})),
   provider:profile.providers[0]?.binding.contributionId??'',harbors:(selected?.harbors??[]).map(h=>({...h,location:{...h.location},rules:h.rules?.map(r=>({...r}))}))};
  configurationKind='harbor';composing=false;changed=false;index=0;x=location?.x??0;y=location?.y??0;z=location?.z??0;
  if(location&&draft){const found=draft.harbors.findIndex(h=>h.location.x===x&&h.location.y===y&&h.location.z===z);index=found;}
 }
 function changeKind(){index=-1;}
 function addJunction(){
  if(!draft||![x,y,z].every(v=>Number.isInteger(v)&&v>=-2147483648&&v<=2147483647))return;
  const found=draft.junctions.findIndex(j=>j.location.x===x&&j.location.y===y&&j.location.z===z);
  if(found>=0){index=found;return;}
  if(draft.junctions.length>=8192)return;
  changed=true;draft.junctions.push({location:{x,y,z},defaultRoute:'straight'});index=draft.junctions.length-1;
 }
 function add(){
  if(configurationKind==='junction'){addJunction();return;}
  if(!draft||![x,y,z].every(v=>Number.isInteger(v)&&v>=-2147483648&&v<=2147483647))return;
  const found=draft.harbors.findIndex(h=>h.location.x===x&&h.location.y===y&&h.location.z===z);
  if(found>=0){index=found;return;}
  if(draft.harbors.length>=8192)return;
  changed=true;draft.harbors.push({location:{x,y,z},kind:'fixed',minutes:10,reverse:false});index=draft.harbors.length-1;
 }
 async function apply(){
  if(!draft||stale||blocked||!valid||composing)return;
  const current=draft,provider=profile.providers.find(p=>p.binding.contributionId===current.provider)?.binding;
  const ok=await model.send({kind:'manageFleetController',action:current.action,controllerId:current.source?.id??'0',
   expectedRevision:current.source?.revision??'0',name:['create','rename'].includes(current.action)?current.name:'',
   provider:current.action==='create'?provider??null:null,
   location:current.action==='configure'?current.removedLocation??setting?.location??junction?.location??null:null,
   junctions:current.action==='configure'&&configurationKind==='junction'?current.removedLocation?[]:junction?[{location:junction.location,defaultRoute:junction.defaultRoute}]:[]:null,
   expectedGeometryRevision:current.action==='cleanJunctions'||current.action==='configure'&&configurationKind==='junction'?current.geometry:null,
   harbors:current.action==='configure'&&configurationKind==='harbor'?current.removedLocation?[]:setting?[{location:setting.location,kind:setting.kind,minutes:setting.minutes,reverse:setting.reverse}]:[]:null});
  if(ok&&draft===current){draft=undefined;}
 }
</script>
<section class="fleet-controller-manager" aria-label={text.controllerManager}>
 {#if location}<p class="hint">{text.signalSharedController}</p>{/if}
 <label class="field">{text.controllerManager}<select aria-label={text.controllerManager} bind:value={selectedId} disabled={view.pending}>
  <option value="">{text.controllerChoose}</option>
  {#each instances as c}<option value={c.id}>{c.name} · {typeName(c.kind)} · #{c.id}</option>{/each}
 </select></label>
 <div class="form-actions">
  {#if !location}<button disabled={blocked||!view.read||!!fleet&&fleet.controllers.length>=256} onclick={()=>open('create')}>{text.controllerCreate}</button>{/if}
  <button disabled={!selected||view.pending} onclick={()=>open('configure')}>{text.controllerConfigure}</button>
  {#if !location}
   {#if selected?.kind==='timetable'&&missing.length}<button disabled={blocked} onclick={()=>open('cleanJunctions')}>{text.controllerCleanJunctions} ({missing.length})</button>{/if}
   <button disabled={blocked||!selected} onclick={()=>open('rename')}>{text.controllerRename}</button>
   <button disabled={blocked||!selected||!!fleet&&fleet.controllers.length>=256} onclick={()=>open('copy')}>{text.controllerCopy}</button>
   <button disabled={blocked||!selected} onclick={()=>open('delete')}>{text.controllerDelete}</button>
  {/if}
 </div>
 {#if selected&&usages}
  <p data-testid="controller-reference-count">{text.controllerReferences}: {usages.trains.length+usages.groups.length}</p>
  <ul aria-label={text.controllerReferences}>
   {#each usages.trains as t}<li>{text.ownedStock}: {t.name} · #{t.id}</li>{/each}
   {#each usages.groups as g}<li>{text.controllerGroup}: {g.name} · #{g.id}</li>{/each}
  </ul>
 {/if}
 {#if !location&&group}
  <label class="field">{text.controllerGroup}<select aria-label={text.controllerGroup} value={group.id} onchange={event=>{groupId=event.currentTarget.value;}}>
   {#each fleet?.groups??[] as g}<option value={g.id}>{g.name} · #{g.id}</option>{/each}
  </select></label>
  <FleetControllerAssignment {model} {view} targetKind="group" targetId={group.id} disabled={blocked}/>
  <p class="hint">{text.controllerDelegationHelp}</p>
 {/if}
</section>
{#if draft}
 <DecisionModal leftAligned title={text[({create:'controllerCreate',rename:'controllerRename',copy:'controllerCopy',delete:'controllerDelete',configure:'controllerConfigure',cleanJunctions:'controllerCleanJunctions'} as const)[draft.action]]} oncancel={()=>{if(!view.pending)draft=undefined;}}>
  {#if draft.source}<p>{draft.source.name} · #{draft.source.id} · {typeName(draft.source.kind)}</p>{/if}
  {#if draft.action==='create'}
   <label class="field">{text.controllerType}<select aria-label={text.controllerType} bind:value={draft.provider} disabled={blocked}>
    {#each profile.providers as p}<option value={p.binding.contributionId}>{typeName(p.kind)}</option>{/each}
   </select></label>
  {/if}
  {#if draft.action==='create'||draft.action==='rename'}
   <label class="field">{text.controllerName}<input aria-label={text.controllerName} maxlength="256" bind:value={draft.name} disabled={blocked} oncompositionstart={()=>{composing=true;}} oncompositionend={()=>{composing=false;}}/></label>
  {:else if draft.action==='copy'}<p>{text.controllerCopyHelp}</p><p>{draft.name}</p>
  {:else if draft.action==='delete'}
   <p>{referenced?text.controllerReferenced:text.controllerDeleteHelp}</p>
   <ul>{#each draftUsages?.trains??[] as t}<li>{text.ownedStock}: {t.name} · #{t.id}</li>{/each}{#each draftUsages?.groups??[] as g}<li>{text.controllerGroup}: {g.name} · #{g.id}</li>{/each}</ul>
  {:else if draft.action==='cleanJunctions'}
   <p>{text.controllerCleanJunctionsHelp}</p><ul>{#each missing as j}<li>{j.location.x}, {j.location.y}, {j.location.z}</li>{/each}</ul>
  {:else if draft.source?.kind==='manual'}<p>{text.manualFleetHelp}</p>
  {:else if draft.action==='configure'}
   <p class="hint">{text.controllerSharedHelp}</p>
   <label class="field">{text.controllerSettingType}<select aria-label={text.controllerSettingType} bind:value={configurationKind} disabled={changed||!!location} onchange={changeKind}>
    <option value="harbor">{text.controllerHarbor}</option><option value="junction">{text.controllerJunction}</option>
   </select></label>
   <p class="hint">{configurationKind==='junction'?text.controllerJunctionHelp:text.controllerHarborHelp}</p>
   <div class="form-actions">
    {#each ['x','y','z'] as axis}<label>{axis.toUpperCase()}<input aria-label={axis.toUpperCase()} type="number" step="1" value={axis==='x'?x:axis==='y'?y:z} disabled={blocked||changed} oninput={event=>{const n=Number(event.currentTarget.value);if(axis==='x')x=n;else if(axis==='y')y=n;else z=n;}}/></label>{/each}
    <button disabled={blocked||changed||(configurationKind==='harbor'?draft.harbors:draft.junctions).length>=8192&&!(configurationKind==='harbor'?draft.harbors:draft.junctions).some(h=>h.location.x===x&&h.location.y===y&&h.location.z===z)} onclick={add}>{text.controllerLocation}</button>
   </div>
   {#if (configurationKind==='harbor'?draft.harbors:draft.junctions).length}
    <label class="field">{text.position}<select aria-label={text.position} bind:value={index} disabled={changed}>{#each (configurationKind==='harbor'?draft.harbors:draft.junctions) as h,i}<option value={i}>{h.location.x}, {h.location.y}, {h.location.z}</option>{/each}</select></label>
   {/if}
   {#if draft.removedLocation}<p>{text.controllerRemoveLocation}: {draft.removedLocation.x}, {draft.removedLocation.y}, {draft.removedLocation.z}</p>{/if}
   {#if junction}
    <label class="field">{text.controllerDefaultRoute}<select aria-label={text.controllerDefaultRoute} bind:value={junction.defaultRoute} disabled={blocked} onchange={()=>{changed=true;}}>
     <option value="straight">{text.controllerStraight}</option><option value="curve">{text.controllerCurve}</option>
    </select></label>
    <button disabled={changed} onclick={openRules}>{text.controllerRules}</button>
    {#if changed}<p class="hint">{text.ruleSaveSettingFirst}</p>{/if}
    <button disabled={blocked} onclick={()=>{draft!.removedLocation={...junction.location};changed=true;index=-1;}}>{text.controllerRemoveLocation}</button>
   {/if}
   {#if setting}
    <label class="field">{text.controllerStop}<select aria-label={text.controllerStop} value={setting.kind} disabled={blocked} onchange={event=>{changed=true;setting.kind=event.currentTarget.value as FleetHarbor['kind'];if(setting.kind==='pass'||setting.kind==='advanced'){setting.minutes=0;setting.reverse=false;}else {setting.minutes=setting.kind==='daily'?0:10;setting.reverse=false;}}}>
     <option value="pass">{text.controllerPass}</option><option value="fixed">{text.controllerFixed}</option><option value="daily">{text.controllerDaily}</option><option value="advanced">{text.controllerAdvanced}</option>
    </select></label>
    {#if setting.kind==='advanced'}
     <button disabled={changed} onclick={openRules}>{text.controllerRules}</button>
     {#if changed}<p class="hint">{text.ruleSaveSettingFirst}</p>{/if}
    {:else if setting.kind!=='pass'}
     {#if setting.kind==='daily'}
      <div class="form-actions">
       <label class="field">{text.controllerDepartureHour}<input aria-label={text.controllerDepartureHour} type="number" min="0" max="23" step="1" value={Math.floor(setting.minutes/60)} disabled={blocked} oninput={event=>{changed=true;setting.minutes=Number(event.currentTarget.value)*60+setting.minutes%60;}}/></label>
       <label class="field">{text.controllerDepartureMinute}<input aria-label={text.controllerDepartureMinute} type="number" min="0" max="59" step="1" value={setting.minutes%60} disabled={blocked} oninput={event=>{changed=true;setting.minutes=Math.floor(setting.minutes/60)*60+Number(event.currentTarget.value);}}/></label>
      </div>
     {:else}
      <label class="field">{text.controllerMinutes}<input type="number" min="10" max="2147483647" step="10" bind:value={setting.minutes} disabled={blocked} oninput={()=>{changed=true;}}/></label>
     {/if}
     <label><input type="checkbox" bind:checked={setting.reverse} disabled={blocked} onchange={()=>{changed=true;}}/>{text.controllerReverse}</label>
    {/if}
    <button disabled={blocked} onclick={()=>{draft!.removedLocation={...setting.location};changed=true;index=-1;}}>{text.controllerRemoveLocation}</button>
   {/if}
  {/if}
  {#if stale}<p role="alert">{text.controllerConflict}</p>{/if}
  {#if !['idle','waiting','accepted'].includes(view.feedback)}<p role="alert">{text[view.feedback]}</p>{/if}
  {#snippet actions()}
   {#if !(draft!.action==='configure'&&draft!.source?.kind==='manual')}
    <button class="primary" disabled={blocked||stale||!valid||composing||draft!.action==='delete'&&referenced} onclick={apply}>{text[draft!.action==='delete'?'controllerDelete':'controllerApply']}</button>
   {/if}
   <button disabled={view.pending} onclick={()=>{draft=undefined;}}>{text.cancel}</button>
  {/snippet}
 </DecisionModal>
{/if}
{#if ruleTarget}<FleetRuleEditor {model} {view} {disabled} {...ruleTarget} onclose={()=>{ruleTarget=undefined;}}/>{/if}
<style>
 .fleet-controller-manager{min-width:0;}
 .form-actions input[type="number"]{width:5.5rem;}
 ul{overflow-wrap:anywhere;}
</style>
