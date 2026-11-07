<script lang="ts">
 import DecisionModal from './DecisionModal.svelte';
 import {messages,locales} from './lib/localization.ts';
 import {blankFleetRule,sameRuleMask,validFleetRule,maximumLocationRules,maximumControllerRules} from './lib/fleet-controllers.ts';
 import type {FleetRule,Position} from './generated/contracts.ts';
 import type {ShellModel,ShellState} from './lib/shell-model.ts';
 let {model,view,controllerId,location,settingKind,epoch,disabled=false,onclose}:{model:ShellModel;view:ShellState;controllerId:string;location:Position;settingKind:'harbor'|'junction';epoch:string;disabled?:boolean;onclose:()=>void}=$props();
 const text=$derived(messages[view.locale]),junction=$derived(settingKind==='junction');
 const controller=$derived(view.read?.mixed?.fleet?.controllers.find(c=>c.id===controllerId));
 const setting=$derived((junction?controller?.junctions:controller?.harbors)?.find(s=>s.location.x===location.x&&s.location.y===location.y&&s.location.z===location.z));
 const rules=$derived(setting?.rules??[]);
 const total=$derived([...controller?.harbors??[],...controller?.junctions??[]].reduce((n,s)=>n+(s.rules?.length??0),0));
 const replaced=$derived(epoch!==view.read?.worldEpoch);
 const blocked=$derived(disabled||view.pending||view.connection!=='ready'||view.role==='observer'||!setting||replaced);
 let selected=$state(-1);
 let lastEntry=$state<FleetRule>();
 let draft=$state<{operation:'add'|'replace'|'copy'|'move'|'remove'|'clear';index:number;offset:number;rule:FleetRule|null;revision:string;epoch:string;geometry:string}>();
 const stale=$derived(!!draft&&(controller?.revision!==draft.revision||view.read?.worldEpoch!==draft.epoch||junction&&view.read?.mixed?.geometryRevision!==draft.geometry));
 const duplicate=$derived(!!draft&&draft.operation==='add'&&!junction&&!!draft.rule&&rules.some(r=>sameRuleMask(r,draft!.rule!)));
 const valid=$derived(!!draft&&!duplicate&&(!draft.rule||validFleetRule(draft.rule,junction)));
 const fields=['month','day','dayOfWeek','hour','minutes'] as const;
 const fieldKeys={month:'ruleMonth',day:'ruleDay',dayOfWeek:'ruleWeekday',hour:'ruleHour',minutes:'ruleMinute'} as const;
 const ruleActions=$derived(junction?['straight','curve'] as const:['pass','stop','go','reverse','forwarding','reverseforwarding','reverse2','rforwarding2'] as const);
 const actionKeys={straight:'controllerStraight',curve:'controllerCurve',pass:'controllerPass',stop:'ruleStop',go:'ruleGo',reverse:'ruleReverse',forwarding:'ruleForwarding',reverseforwarding:'ruleReverseForwarding',reverse2:'ruleTurningReverse',rforwarding2:'ruleTurningForwarding'} as const;
 const numbers=(field:typeof fields[number])=>field==='month'?Array.from({length:12},(_,n)=>n+1):field==='day'?Array.from({length:31},(_,n)=>n+1):field==='dayOfWeek'?Array.from({length:7},(_,n)=>n):field==='hour'?Array.from({length:24},(_,n)=>n):[0,10,20,30,40,50];
 const weekdays=$derived(locales[view.locale].formats.weekdays);
 const show=(r:FleetRule)=>fields.filter(f=>!junction||f!=='minutes').map(f=>r[f]===-1?'*':f==='dayOfWeek'?weekdays[r[f]]:String(r[f]).padStart(2,'0')).join(' / ')+' · '+text[actionKeys[r.action]];
 function edit(operation:NonNullable<typeof draft>['operation'],offset=0){
  if(blocked||!controller||!view.read)return;
  const index=operation==='add'||operation==='clear'?-1:selected;
  if((index<0||index>=rules.length)&&operation!=='add'&&operation!=='clear')return;
  draft={operation,index,offset,rule:operation==='add'?lastEntry?{...lastEntry}:blankFleetRule(junction):operation==='replace'?{...rules[index]}:null,
   revision:controller.revision,epoch:view.read.worldEpoch,geometry:view.read.mixed!.geometryRevision};
 }
 async function apply(){
  if(!draft||blocked||stale||!valid)return;
  const current=draft;
  const ok=await model.send({kind:'editFleetRule',controllerId,expectedRevision:current.revision,location,settingKind,
   operation:current.operation,index:current.index,offset:current.offset,rule:current.rule,expectedGeometryRevision:junction?current.geometry:null});
  if(ok&&draft===current){
   if(current.rule)lastEntry=junction&&current.operation==='add'?{...blankFleetRule(true),action:current.rule.action}:{...current.rule};
   selected=current.operation==='copy'||current.operation==='add'?rules.length-1:current.operation==='move'?current.index+current.offset:current.operation==='clear'||current.operation==='remove'?-1:current.index;draft=undefined;
  }
 }
</script>
<DecisionModal wide leftAligned title={text.controllerRules} oncancel={()=>{if(!view.pending)onclose();}}>
 <p>{controller?.name} · {location.x}, {location.y}, {location.z}</p>
 <p class="hint">{junction?text.ruleJunctionHelp:text.ruleHarborHelp}</p>
 <p class="hint">{junction?text.ruleJunctionColumns:text.ruleHarborColumns}</p>
 <label class="field">{text.controllerRules}<select aria-label={text.controllerRules} size="5" bind:value={selected} disabled={!!draft}>
  {#each rules as r,i}<option value={i}>{i+1}. {show(r)}</option>{/each}
 </select></label>
 <div class="form-actions">
  <button disabled={blocked||!!draft||rules.length>=maximumLocationRules||total>=maximumControllerRules} onclick={()=>edit('add')}>{text.ruleAdd}</button>
  <button disabled={blocked||!!draft||selected<0} onclick={()=>edit('replace')}>{text.ruleEdit}</button>
  <button disabled={blocked||!!draft||selected<0||rules.length>=maximumLocationRules||total>=maximumControllerRules} onclick={()=>edit('copy')}>{text.ruleCopy}</button>
  <button disabled={blocked||!!draft||selected<=0} onclick={()=>edit('move',-1)}>{text.ruleUp}</button>
  <button disabled={blocked||!!draft||selected<0||selected>=rules.length-1} onclick={()=>edit('move',1)}>{text.ruleDown}</button>
  <button disabled={blocked||!!draft||selected<0} onclick={()=>edit('remove')}>{text.ruleRemove}</button>
  <button disabled={blocked||!!draft||!rules.length} onclick={()=>edit('clear')}>{text.ruleClear}</button>
 </div>
 {#if draft}
  <p>{text[({add:'ruleAdd',replace:'ruleEdit',copy:'ruleCopy',move:'ruleReorder',remove:'ruleRemove',clear:'ruleClear'} as const)[draft.operation]]}</p>
  {#if draft.rule}
   <div class="rule-fields">
    {#each fields.filter(f=>!junction||f!=='minutes') as field}
     <label class="field">{text[fieldKeys[field]]}<select aria-label={text[fieldKeys[field]]} bind:value={draft.rule[field]} disabled={blocked}>
      <option value={-1}>{text.ruleAny}</option>
      {#each numbers(field) as n}<option value={n}>{field==='dayOfWeek'?weekdays[n]:String(n).padStart(2,'0')}</option>{/each}
     </select></label>
    {/each}
   </div>
   <label class="field">{text.ruleAction}<select aria-label={text.ruleAction} bind:value={draft.rule.action} disabled={blocked}>
    {#each ruleActions as action}<option value={action}>{text[actionKeys[action]]}</option>{/each}
   </select></label>
   {#if !junction}<p class="hint">{text.ruleTurningHelp}</p>{/if}
  {:else if draft.operation==='clear'}<p role="alert">{text.ruleClearHelp}</p>
  {:else}<p>{draft.index+1}. {rules[draft.index]?show(rules[draft.index]):''}{draft.operation==='move'?' → '+(draft.index+draft.offset+1):''}</p>{/if}
  {#if duplicate}<p role="alert">{text.ruleDuplicate}</p>{/if}
 {/if}
 {#if stale||replaced}<p role="alert">{text.controllerConflict}</p>{/if}
 {#if !['idle','waiting','accepted'].includes(view.feedback)}<p role="alert">{text[view.feedback]}</p>{/if}
 {#snippet actions()}
  {#if draft}<button class="primary" disabled={blocked||stale||!valid} onclick={apply}>{text.controllerApply}</button>
   <button disabled={view.pending} onclick={()=>{draft=undefined;}}>{text.cancel}</button>
  {:else}<button onclick={onclose}>{text.close}</button>{/if}
 {/snippet}
</DecisionModal>
<style>
 .rule-fields{display:grid;grid-template-columns:repeat(auto-fit,minmax(7rem,1fr));gap:.5rem;}
 select{max-width:100%;}
</style>
