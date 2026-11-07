<script lang="ts">
  import {effectiveFleetController} from './lib/fleet-state.ts';
  import {fleetRecoveryReason,fleetStateLabel} from './lib/fleet-recovery.ts';
  import {primaryAction} from './lib/primary-action.ts';
  import {onDestroy,tick,untrack} from 'svelte';
  import type {ShellModel,ShellState} from './lib/shell-model.ts';
  import type {Position,FleetDeploymentPreview,FleetDeploymentQuery} from './generated/contracts.ts';
  import {messages} from './lib/localization.ts';
  import {currentQuote} from './lib/stock-catalog.ts';
  let {model,view,trainId,disabled,picked,onmap,onpreview,allowPlacement=true}:{model:ShellModel;view:ShellState;trainId:string;disabled:boolean;allowPlacement?:boolean;picked?:Position;onmap:(active:boolean)=>void;onpreview:(p:FleetDeploymentPreview|undefined)=>void}=$props();
  const text=$derived(messages[view.locale]),read=$derived(view.read!),train=$derived(read.mixed?.fleet?.trains.find(t=>t.id===trainId));
  const effective=$derived(read.mixed?.fleet?effectiveFleetController(read.mixed.fleet,trainId):undefined);
  let x=$state(0),y=$state(0),z=$state(1),reverse=$state(false),picking=$state(false),quote=$state<FleetDeploymentPreview>(),loading=$state(false),failed=$state(false),submitting=$state(false);
  let request:AbortController|undefined,launcher:HTMLElement|undefined;
  const stale=$derived(!!quote&&!currentQuote(read,quote.query));
  const headings=['headingNorth','headingNorthEast','headingEast','headingSouthEast','headingSouth','headingSouthWest','headingWest','headingNorthWest'] as const;
  const reasons={missingRail:'deploymentMissingRail',carOccupied:'deploymentOccupied',duplicateCell:'deploymentDuplicate',invalidPlacementHeading:'deploymentHeading',outside:'deploymentOutside',unboundJunctionController:'deploymentUnbound',trainAlreadyPlaced:'deploymentState',trainNotPlaced:'deploymentState'} as const;
  export function cancelDraft(){if(!quote&&!picking&&!loading)return false;cancel(true);return true;}
  function cancel(restore=false,keepPicking=false){request?.abort();request=undefined;loading=false;quote=undefined;failed=false;onpreview(undefined);if(!keepPicking){picking=false;onmap(false);}if(restore)void tick().then(()=>launcher?.isConnected&&launcher.focus({preventScroll:true}));}
  $effect(()=>{const position=picked;if(position)untrack(()=>{if(picking){x=position.x;y=position.y;z=position.z;void preview();}});});
  $effect(()=>{if(stale)onpreview(undefined);});
  $effect(()=>{if(disabled){cancel();}});
  onDestroy(()=>{request?.abort();onpreview(undefined);onmap(false);});
  function change(){cancel(false,true);}
  function updatePreview(event?:Event){
    if(quote&&!stale&&quote.query.action==='place'&&quote.query.tail?.x===x&&quote.query.tail.y===y&&quote.query.tail.z===z&&quote.query.reverse===reverse)return;
    if([x,y,z].every(Number.isInteger)&&z>=0&&z<read.map.levels)void preview(event?.currentTarget instanceof HTMLElement?event.currentTarget:undefined);
  }
  function coordinateKey(e:KeyboardEvent){if(e.key==='Enter'&&!quote&&!e.isComposing&&e.keyCode!==229&&!e.repeat&&!e.altKey&&!e.ctrlKey&&!e.metaKey&&!e.shiftKey){e.preventDefault();e.stopPropagation();updatePreview(e);}}
  async function preview(source?:HTMLElement,action?:'reverse'){
    if(disabled||submitting||!train)return;
    launcher=source??(document.activeElement instanceof HTMLElement?document.activeElement:undefined);
    cancel(false,true);const controller=request=new AbortController();loading=true;
    const query:FleetDeploymentQuery={expectedWorldEpoch:read.worldEpoch,expectedRevision:read.revision,expectedMinute:read.tick,expectedContentVersion:read.contentVersion!,trainId,action:action??(train.state==='unplaced'?'place':'withdraw'),tail:train.state==='unplaced'?{x,y,z}:null,reverse:train.state==='unplaced'&&reverse};
    try{const result=await model.previewFleetDeployment(query,controller.signal);if(request===controller){quote=result;onpreview(result);}}
    catch{if(request===controller&&!controller.signal.aborted)failed=true;}
    finally{if(request===controller){request=undefined;loading=false;}}
  }
  async function confirm(){if(disabled||stale||!quote?.legal||submitting)return;submitting=true;
    try{if(await model.send({kind:'deployFleet',query:quote.query}))cancel(true);}finally{submitting=false;}}
</script>
<section class="deployment-controls" aria-label={train?.state==='unplaced'&&allowPlacement?text.deployTrain:text.fleetOperations} use:primaryAction>
  <h3>{train?.state==='unplaced'?(allowPlacement?text.deployTrain:text.stockUnplaced):text.fleetOperations}</h3>
  {#if train}<p data-testid="fleet-state">{text.trainState}: {text[fleetStateLabel[train.state]]}{train.forwarding?` · ${text.fleetForwarding}`:''}</p>
    {@const reason=fleetRecoveryReason(read,train)}
    {#if reason}<p role="status" data-testid="fleet-recovery-reason">{text[reason]} {text.fleetRecoveryHelp}</p>{/if}
  {/if}
  {#if train?.state==='unplaced'&&allowPlacement}
    <button type="button" aria-pressed={picking} disabled={disabled||loading||submitting} onclick={()=>{cancel();picking=true;onmap(true);}}>{text.deploymentPick}</button>
    {#if picking}<p role="status">{text.deploymentPicking}</p>{/if}
    <form onsubmit={e=>{e.preventDefault();updatePreview();}}>
      <fieldset class="deployment-target" disabled={disabled||submitting}><legend>{text.deploymentTail}</legend>
        <label class="field">X<input aria-label="X" type="number" step="1" required bind:value={x} oninput={change} onchange={updatePreview} onkeydown={coordinateKey}/></label>
        <label class="field">Y<input aria-label="Y" type="number" step="1" required bind:value={y} oninput={change} onchange={updatePreview} onkeydown={coordinateKey}/></label>
        <label class="field">Z<input aria-label="Z" type="number" min="0" max={read.map.levels-1} step="1" required bind:value={z} oninput={change} onchange={updatePreview} onkeydown={coordinateKey}/></label>
        <label class="deployment-reverse"><input type="checkbox" bind:checked={reverse} onchange={event=>{reverse=event.currentTarget.checked;updatePreview(event);}}/><span>{text.deploymentReverse}</span></label>
      </fieldset>
    </form>
  {:else if train&&train.state!=='unplaced'}<p>{text.deploymentWithdrawHelp}</p><div class="form-actions"><button disabled={disabled||loading||submitting} onclick={event=>{void preview(event.currentTarget);}}>{text.withdrawTrain}</button><button disabled={disabled||loading||submitting} onclick={event=>{void preview(event.currentTarget,'reverse');}}>{text.fleetReverse}</button></div>{/if}
  {#if effective}<p>{text.controller}: {effective.name} · {text[effective.kind==='simple'?'deploymentSimple':'deploymentTimetable']}</p>
  {/if}
  {#if allowPlacement||train?.state!=='unplaced'}<details class="deployment-help"><summary>{text.help}</summary>{#if train?.state==='unplaced'}<p class="hint">{text.deploymentHelp}</p>{:else}<p class="hint">{text.fleetReverseHelp}</p>{/if}<p class="hint">{text.deploymentController}</p></details>{/if}
  {#if loading}<p role="status">{text.waiting}</p>{/if}
  {#if failed}<p role="alert">{text.deploymentFailed}</p>{/if}
  {#if quote}
    <div class="deployment-review" aria-label={text.deploymentPreview}>
      {#if !quote.legal}<p role="alert">{text[reasons[quote.reason as keyof typeof reasons]??'deploymentFailed']}</p>{/if}
      {#if stale}<p role="alert">{text.stockStale}</p>{/if}
      {#if !['idle','waiting','accepted'].includes(view.feedback)}<p role="alert">{text[view.feedback]}</p>{/if}
      <div class="form-actions"><button class="primary" disabled={disabled||submitting||stale||!quote.legal} onclick={confirm}>{text[quote.query.action==='place'?'deploymentConfirm':quote.query.action==='reverse'?'fleetReverseConfirm':'withdrawConfirm']}</button><button disabled={submitting||view.pending} onclick={()=>cancel(true)}>{text.cancel}</button></div>
      <ol>{#each quote.cars as car}<li data-testid="deployment-car">#{car.carId} · {car.location.x}, {car.location.y}, {car.location.z} · {text[headings[car.heading]]}</li>{/each}</ol>
    </div>
  {/if}
</section>
