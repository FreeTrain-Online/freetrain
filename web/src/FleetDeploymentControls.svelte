<script lang="ts">
  import {effectiveFleetController} from './lib/fleet-state.ts';
  import {primaryAction} from './lib/primary-action.ts';
  import {onDestroy,tick} from 'svelte';
  import type {ShellModel,ShellState} from './lib/shell-model.ts';
  import type {Position,FleetDeploymentPreview,FleetDeploymentQuery} from './generated/contracts.ts';
  import {messages} from './lib/localization.ts';
  import {currentQuote} from './lib/stock-catalog.ts';
  let {model,view,trainId,disabled,picked,onmap,onpreview}:{model:ShellModel;view:ShellState;trainId:string;disabled:boolean;picked?:Position;onmap:(active:boolean)=>void;onpreview:(p:FleetDeploymentPreview|undefined)=>void}=$props();
  const text=$derived(messages[view.locale]),read=$derived(view.read!),train=$derived(read.mixed?.fleet?.trains.find(t=>t.id===trainId));
  const effective=$derived(read.mixed?.fleet?effectiveFleetController(read.mixed.fleet,trainId):undefined);
  let x=$state(0),y=$state(0),z=$state(1),reverse=$state(false),picking=$state(false),quote=$state<FleetDeploymentPreview>(),loading=$state(false),failed=$state(false),submitting=$state(false);
  let request:AbortController|undefined,launcher:HTMLElement|undefined;
  const stale=$derived(!!quote&&!currentQuote(read,quote.query));
  const headings=['headingNorth','headingNorthEast','headingEast','headingSouthEast','headingSouth','headingSouthWest','headingWest','headingNorthWest'] as const;
  const reasons={missingRail:'deploymentMissingRail',carOccupied:'deploymentOccupied',duplicateCell:'deploymentDuplicate',invalidPlacementHeading:'deploymentHeading',outside:'deploymentOutside',unboundJunctionController:'deploymentUnbound',trainAlreadyPlaced:'deploymentState',trainNotPlaced:'deploymentState'} as const;
  export function cancelDraft(){if(!quote&&!picking&&!loading)return false;cancel(true);return true;}
  function cancel(restore=false){request?.abort();request=undefined;loading=false;quote=undefined;failed=false;onpreview(undefined);picking=false;onmap(false);if(restore)void tick().then(()=>launcher?.isConnected&&launcher.focus({preventScroll:true}));}
  $effect(()=>{if(picked){x=picked.x;y=picked.y;z=picked.z;cancel();}});
  $effect(()=>{if(stale)onpreview(undefined);});
  $effect(()=>{if(disabled){cancel();}});
  onDestroy(()=>{request?.abort();onpreview(undefined);onmap(false);});
  function change(){cancel();}
  async function preview(){
    if(disabled||loading||submitting||!train)return;
    launcher=document.activeElement instanceof HTMLElement?document.activeElement:undefined;
    cancel();const controller=request=new AbortController();loading=true;
    const query:FleetDeploymentQuery={expectedWorldEpoch:read.worldEpoch,expectedRevision:read.revision,expectedMinute:read.tick,expectedContentVersion:read.contentVersion!,trainId,action:train.state==='unplaced'?'place':'withdraw',tail:train.state==='unplaced'?{x,y,z}:null,reverse:train.state==='unplaced'&&reverse};
    try{const result=await model.previewFleetDeployment(query,controller.signal);if(request===controller){quote=result;onpreview(result);}}
    catch{if(request===controller&&!controller.signal.aborted)failed=true;}
    finally{if(request===controller){request=undefined;loading=false;}}
  }
  async function confirm(){if(disabled||stale||!quote?.legal||submitting)return;submitting=true;
    try{if(await model.send({kind:'deployFleet',query:quote.query}))cancel(true);}finally{submitting=false;}}
</script>
<section class="deployment-controls" aria-label={text.deployTrain} use:primaryAction>
  <h3>{train?.state==='unplaced'?text.deployTrain:text.withdrawTrain}</h3>
  {#if train?.state==='unplaced'}
    <button type="button" aria-pressed={picking} disabled={disabled||loading||submitting} onclick={()=>{cancel();picking=true;onmap(true);}}>{text.deploymentPick}</button>
    {#if picking}<p role="status">{text.deploymentPicking}</p>{/if}
    <form onsubmit={e=>{e.preventDefault();void preview();}}>
      <fieldset class="deployment-target" disabled={disabled||loading||submitting}><legend>{text.deploymentTail}</legend>
        <label class="field">X<input aria-label="X" type="number" step="1" required bind:value={x} oninput={change}/></label>
        <label class="field">Y<input aria-label="Y" type="number" step="1" required bind:value={y} oninput={change}/></label>
        <label class="field">Z<input aria-label="Z" type="number" min="0" max={read.map.levels-1} step="1" required bind:value={z} oninput={change}/></label>
        <label class="deployment-reverse"><input type="checkbox" bind:checked={reverse} onchange={change}/><span>{text.deploymentReverse}</span></label>
      </fieldset>
      <button class:primary={!quote} type="submit" disabled={disabled||loading||submitting}>{loading?text.waiting:text.deploymentPreview}</button>
    </form>
  {:else}<p>{text.deploymentWithdrawHelp}</p><button disabled={disabled||loading||submitting} onclick={preview}>{text.withdrawTrain}</button>{/if}
  {#if effective}<p>{text.controller}: {effective.name} · {text[effective.kind==='simple'?'deploymentSimple':effective.kind==='manual'?'manual':'deploymentTimetable']}</p>
    {#if effective.kind!=='simple'}<p role="status">{text.deploymentControllerUnavailable}</p>{/if}
  {/if}
  <details class="deployment-help"><summary>{text.help}</summary>{#if train?.state==='unplaced'}<p class="hint">{text.deploymentHelp}</p>{/if}<p class="hint">{text.deploymentController}</p></details>
  {#if failed}<p role="alert">{text.deploymentFailed}</p>{/if}
  {#if quote}
    <div class="deployment-review" aria-label={text.deploymentPreview}>
      {#if !quote.legal}<p role="alert">{text[reasons[quote.reason as keyof typeof reasons]??'deploymentFailed']}</p>{/if}
      <ol>{#each quote.cars as car}<li data-testid="deployment-car">#{car.carId} · {car.location.x}, {car.location.y}, {car.location.z} · {text[headings[car.heading]]}</li>{/each}</ol>
      {#if stale}<p role="alert">{text.stockStale}</p>{/if}
      {#if !['idle','waiting','accepted'].includes(view.feedback)}<p role="alert">{text[view.feedback]}</p>{/if}
      <div class="form-actions"><button class="primary" disabled={disabled||submitting||stale||!quote.legal} onclick={confirm}>{text[quote.query.action==='place'?'deploymentConfirm':'withdrawConfirm']}</button><button disabled={submitting||view.pending} onclick={()=>cancel(true)}>{text.cancel}</button></div>
    </div>
  {/if}
</section>
