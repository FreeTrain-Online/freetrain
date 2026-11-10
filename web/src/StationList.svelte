<script lang="ts">
 import {messages} from './lib/localization.ts';
 import {stationList} from './lib/station-information.ts';
 import type {ShellModel,ShellState} from './lib/shell-model.ts';
 import StationInformation from './StationInformation.svelte';
 let {model,view,selected,onselect,onmove,oninspect}:{model:ShellModel;view:ShellState;selected?:string;onselect:(id:string)=>void;onmove:(id:string)=>void;oninspect:(id:string)=>void}=$props();
 const text=$derived(messages[view.locale]),rows=$derived(stationList(view.read,view.locale));
 const station=$derived(rows.find(b=>b.id===selected));
</script>
<section aria-label={text.stationList}>
 <label class="field">{text.stationSelect}<select aria-label={text.stationSelect} size="6" value={station?.id??''} onchange={e=>onselect(e.currentTarget.value)} ondblclick={()=>{if(station)onmove(station.id);}}>
  {#each rows as row (row.id)}<option value={row.id}>{row.name} · #{row.id}</option>{/each}
 </select></label>
 {#if !rows.length}<p>{text.stationListEmpty}</p>{/if}
 <div class="form-actions"><button disabled={!station} onclick={()=>{if(station)onmove(station.id);}}>{text.stationMove}</button><button disabled={!station} onclick={()=>{if(station)oninspect(station.id);}}>{text.stationProperties}</button></div>
 {#if station}{#key `${view.read!.worldEpoch}/${station.id}`}<StationInformation movable={false} {model} {view} stationId={station.id} {onmove}/>{/key}{/if}
</section>
