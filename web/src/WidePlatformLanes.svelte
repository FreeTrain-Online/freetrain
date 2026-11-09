<script lang="ts">
 import {onDestroy,untrack} from 'svelte';
 import {messages} from './lib/localization.ts';
 import {widePlatformLaneQuery} from './lib/wide-platform-edit.ts';
 import {stationBuildingFence} from './lib/station-building-edit.ts';
 import {railwayReasons} from './lib/railway-edit.ts';
 import type {ShellModel,ShellState} from './lib/shell-model.ts';
 import type {WidePlatformPreview} from './generated/contracts.ts';
 let {model,view,id,disabled}:{model:ShellModel;view:ShellState;id:string;disabled:boolean}=$props();
 const text=$derived(messages[view.locale]);
 const reasons={...railwayReasons,occupied:'railCar',incompatiblePlatformRail:'platformRail',invalidPlatformGeometry:'platformGeometry',platformNotFound:'platformNotFound',unavailableStationBinding:'railBinding'} as const;
 const platform=$derived(view.read?.mixed?.stationGraph?.platforms.find(p=>p.id===id));
 const blocked=$derived(disabled||view.pending||view.connection!=='ready'||view.role==='observer'||view.feedback==='unresolved');
 let result=$state<WidePlatformPreview>(),checking=$state(false),failed=$state(false),fence=$state<string>();
 let request:AbortController|undefined,version=0;
 function reset(){version++;request?.abort();result=undefined;checking=false;failed=false;fence=undefined;}
 async function check(lane:0|1){
  reset();if(blocked||!view.read)return;
  const query=widePlatformLaneQuery(view.read,id,lane);if(!query)return;
  const current=version;fence=stationBuildingFence(view.read);request=new AbortController();checking=true;
  try{const preview=await model.previewWidePlatform(query,request.signal);if(current===version&&!blocked&&fence===stationBuildingFence(view.read))result=preview;}
  catch{if(current===version)failed=true;}finally{if(current===version)checking=false;}
 }
 async function commit(){
  if(blocked||!result?.legal||fence!==stationBuildingFence(view.read))return;
  const preview=result;reset();await model.send({kind:'editWidePlatform',query:preview.query,expectedCost:preview.cost});
 }
 $effect(()=>{void view.locale;void id;untrack(reset);});
 $effect(()=>{const current=stationBuildingFence(view.read),stop=blocked;untrack(()=>{if(fence&&fence!==current||stop)reset();});});
 onDestroy(reset);
</script>
<fieldset class="platform-lanes" disabled={blocked}>
 <legend>{text.platformLaneControls}</legend>
 <p class="hint">{text.platformLaneHelp}</p>
 <button type="button" aria-label={text.platformLaneRight} disabled={checking} onclick={()=>void check(0)}>{text.platformLaneRight}: {platform?.lane0?text.platformLaneRemove:text.platformLaneAdd}</button>
 <button type="button" aria-label={text.platformLaneLeft} disabled={checking} onclick={()=>void check(1)}>{text.platformLaneLeft}: {platform?.lane1?text.platformLaneRemove:text.platformLaneAdd}</button>
 {#if checking}<p role="status">{text.checkingGeometry}</p>{/if}
 {#if failed}<p role="alert">{text.geometryFailed}</p>{/if}
 {#if result}
 <p role="status" data-testid="platform-lane-status">{result.legal?text.platformLegal:text[reasons[result.reason as keyof typeof reasons]??'geometryFailed']}</p>
 <p>{result.query.secondLane?text.platformLaneLeft:text.platformLaneRight}: {result.query.action==='addLane'?text.platformLaneAdd:text.platformLaneRemove} · {text.cornerCost}: ¥{result.cost} · {text.stationCells}: {result.positions.length}</p>
 {/if}
 <div class="form-actions"><button type="button" disabled={!result?.legal||checking} onclick={()=>void commit()}>{text.platformLaneConfirm}</button><button type="button" onclick={reset}>{text.cancelPreview}</button></div>
</fieldset>
