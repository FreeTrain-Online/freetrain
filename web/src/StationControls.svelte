<script lang="ts">
 import type {PlatformPreview} from './lib/wide-platform-edit.ts';
 import StationBuildingControls from './StationBuildingControls.svelte';
 import PlatformControls from './PlatformControls.svelte';
 import {messages} from './lib/localization.ts';
 import type {ShellModel,ShellState} from './lib/shell-model.ts';
 import type {Position,StationBuildingPreview} from './generated/contracts.ts';
 let {model,view,picked,removeTarget,onpreview,onplatformpreview,onpick,onframe,level=$bindable(1),heightCut,onreveal}:{model:ShellModel;view:ShellState;picked?:Position;removeTarget?:Position;onpreview:(p?:StationBuildingPreview)=>void;onplatformpreview:(p?:PlatformPreview)=>void;onpick:()=>void;onframe:(positions:Position[])=>void;level?:number;heightCut:number;onreveal:()=>void}=$props();
 const text=$derived(messages[view.locale]);
 let tab=$state<'building'|'platform'>('building');
 let building=$state<StationBuildingControls>(),platform=$state<PlatformControls>();
 export function cancelDraft(){return tab==='building'?building?.cancelDraft():platform?.cancelDraft();}
 export function isRemoving(){return tab==='building'?building?.isRemoving():platform?.isRemoving();}
 export function usesAutomaticPick(){return tab==='building'?building?.usesAutomaticPick():platform?.usesAutomaticPick();}
 export function pickPolicy(){return tab==='platform'?platform?.pickPolicy()??{kind:'railOrGround' as const}:{kind:'ground' as const};}
 function select(value:'building'|'platform'){cancelDraft();onpick();tab=value;}
</script>
<section class="tool-card track-task station-task" aria-label={text.stationTask}>
 <h2>{text.stationTask}</h2>
 <div tabindex="-1" role="tablist" aria-label={text.stationTask} onkeydown={e=>{if(['ArrowLeft','ArrowRight','Home','End'].includes(e.key)){e.preventDefault();select(e.key==='Home'?'building':e.key==='End'?'platform':tab==='building'?'platform':'building');queueMicrotask(()=>document.querySelector<HTMLButtonElement>('.station-task [aria-selected="true"]')?.focus());}}}>
  <button role="tab" aria-selected={tab==='building'} tabindex={tab==='building'?0:-1} onclick={()=>select('building')}>{text.stationBuildingTab}</button>
  <button role="tab" aria-selected={tab==='platform'} tabindex={tab==='platform'?0:-1} onclick={()=>select('platform')}>{text.platformTab}</button>
 </div>
 {#if tab==='building'}<StationBuildingControls bind:this={building} {model} {view} {picked} {removeTarget} {onpreview} {onpick} bind:level {heightCut} {onreveal}/>
 {:else}<PlatformControls bind:this={platform} {model} {view} {picked} onpreview={onplatformpreview} {onpick} {onframe} bind:level {heightCut} {onreveal}/>{/if}
</section>
