<script lang="ts">
 import {fleetStateLabel} from './lib/fleet-recovery.ts';
 import {messages,type Locale} from './lib/localization.ts';
 import {effectiveFleetController} from './lib/fleet-state.ts';
 import {trainInformation,trainSelectionOptions,type ReadFleet} from './lib/train-information.ts';
 let {fleet,locale,trainId,onselect,onmove,onfollow,following,selectable=true}:{selectable?:boolean;fleet?:ReadFleet;locale:Locale;trainId?:string;onselect:(id:string)=>void;onmove:(id:string)=>void;onfollow:(id?:string)=>void;following?:string}=$props();
 const text=$derived(messages[locale]),train=$derived(fleet?.trains.find(t=>t.id===trainId));
 const info=$derived(train?trainInformation(train):undefined),controller=$derived(fleet&&train?effectiveFleetController(fleet,train.id):undefined);
 const placed=$derived(!!train?.cars[0].state.location);
 const fmt=(n:number)=>new Intl.NumberFormat(locale).format(n);
</script>
<section class="train-information" aria-label={text.trainInformation}>
 {#if selectable}<label class="field">{text.trainSelect}<select aria-label={text.trainSelect} value={trainId??''} onchange={e=>onselect(e.currentTarget.value)}>
  <option value="">{text.trainSelect}</option>
  {#each fleet?trainSelectionOptions(fleet,text.groupRoot):[] as row}<option value={row.id} disabled={row.group}>{'› '.repeat(Math.min(row.depth,8))}{row.name} · #{row.id}</option>{/each}
 </select></label>{/if}
 {#if train}
  <h3>{train.name}</h3>
  <dl class="details">
   <div><dt>{text.trainState}</dt><dd data-testid="information-state">{text[fleetStateLabel[train.state]]}</dd></div>
   <div><dt>{text.cars}</dt><dd>{fmt(train.cars.length)}</dd></div>
   {#if info}<div><dt>{text.stockNormal}</dt><dd data-testid="information-capacity">{fmt(info.normal)}</dd></div>
    <div><dt>{text.stockSeated}</dt><dd>{fmt(info.seated)}</dd></div><div><dt>{text.stockPacked}</dt><dd>{fmt(info.packed)}</dd></div>
    <div><dt>{text.trainPassengers}</dt><dd data-testid="information-passengers">{fmt(info.passengers)} ({info.percent===undefined?'—':fmt(info.percent)}%)</dd></div>{/if}
   <div><dt>{text.effectiveController}</dt><dd>{controller?.name??'—'} · #{controller?.id??'—'}</dd></div>
   <div><dt>{text.position}</dt><dd data-testid="information-position">{placed?`${train.cars[0].state.location!.x}, ${train.cars[0].state.location!.y}, ${train.cars[0].state.location!.z}`:text.stockUnplaced}</dd></div>
   {#if train.cars[0].state.kind==='outside'}<div><dt>{text.trainLocationState}</dt><dd>{text.trainOutside}</dd></div>{/if}
  </dl>
  <div class="form-actions"><button disabled={!placed} onclick={()=>onmove(train.id)}>{text.trainMove}</button><button aria-pressed={following===train.id} disabled={!placed} onclick={()=>onfollow(following===train.id?undefined:train.id)}>{following===train.id?text.trainStopFollowing:text.trainFollow}</button></div>
 {:else}<p>{text.stockEmpty}</p>{/if}
 <p class="hint">{text.trainTrackingHelp}</p><p class="hint">{text.trainOccupancyHelp}</p>
</section>
