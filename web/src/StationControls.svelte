<script lang="ts">
 import PassageControls from './PassageControls.svelte';
 import type {StationAccessoryPreview} from './lib/wide-platform-edit.ts';
 import StationBuildingControls from './StationBuildingControls.svelte';
 import PlatformControls from './PlatformControls.svelte';
 import {messages} from './lib/localization.ts';
 import type {ShellModel,ShellState} from './lib/shell-model.ts';
 import type {Position,StationBuildingPreview} from './generated/contracts.ts';
 let {model,view,picked,onpreview,onplatformpreview,onpick,onframe,oncatalog,onpassage,level=$bindable(1),heightCut,onreveal}:{model:ShellModel;view:ShellState;picked?:Position;onpreview:(p?:StationBuildingPreview)=>void;onplatformpreview:(p?:StationAccessoryPreview)=>void;onpick:()=>void;onframe:(positions:Position[])=>void;oncatalog:()=>void;onpassage:()=>void;level?:number;heightCut:number;onreveal:()=>void}=$props();
 const text=$derived(messages[view.locale]);
 let tab=$state<'building'|'platform'|'passage'>('building');
 let building=$state<StationBuildingControls>(),platform=$state<PlatformControls>(),passage=$state<PassageControls>();
 export function cancelDraft(){return tab==='building'?building?.cancelDraft():tab==='passage'?passage?.cancelDraft():platform?.cancelDraft();}
 export function isRemoving(){return tab==='passage'?passage?.isRemoving():tab==='building'?false:platform?.isRemoving();}
 export function usesAutomaticPick(){return tab==='building'?building?.usesAutomaticPick():tab==='passage'?passage?.usesAutomaticPick():platform?.usesAutomaticPick();}
 export function pickPolicy(){return tab==='passage'?passage?.pickPolicy()??{kind:'thinPlatformOrGround' as const}:tab==='platform'?platform?.pickPolicy()??{kind:'railOrGround' as const}:{kind:'ground' as const};}
 export function selectBuilding(){if(tab!=='building')select('building');}
 function select(value:'building'|'platform'|'passage'){cancelDraft();onpick();tab=value;if(value==='passage')onpassage();}
</script>
<section class="tool-card track-task station-task" aria-label={text.stationTask}>
 <h2>{text.stationTask}</h2>
 <div tabindex="-1" role="tablist" aria-label={text.stationTask} onkeydown={e=>{if(['ArrowLeft','ArrowRight','Home','End'].includes(e.key)){e.preventDefault();const tabs=['building','platform','passage'] as const;select(tabs[e.key==='Home'?0:e.key==='End'?2:(tabs.indexOf(tab)+(e.key==='ArrowRight'?1:2))%3]);queueMicrotask(()=>document.querySelector<HTMLButtonElement>('.station-task [aria-selected="true"]')?.focus());}}}>
  <button role="tab" aria-selected={tab==='building'} tabindex={tab==='building'?0:-1} onclick={()=>select('building')}>{text.stationBuildingTab}</button>
  <button role="tab" aria-selected={tab==='platform'} tabindex={tab==='platform'?0:-1} onclick={()=>select('platform')}>{text.platformTab}</button>
  <button role="tab" aria-selected={tab==='passage'} tabindex={tab==='passage'?0:-1} onclick={()=>select('passage')}>{text.passageTab}</button>
 </div>
 {#if tab==='building'}<StationBuildingControls {oncatalog} bind:this={building} {model} {view} {picked} {onpreview} {onpick} bind:level {heightCut} {onreveal}/>
 {:else if tab==='passage'}<PassageControls bind:this={passage} {model} {view} {picked} onpreview={onplatformpreview} {onpick} {onframe} bind:level {heightCut} {onreveal}/>
 {:else}<PlatformControls bind:this={platform} {model} {view} {picked} onpreview={onplatformpreview} {onpick} {onframe} bind:level {heightCut} {onreveal}/>{/if}
</section>
