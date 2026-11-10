<script lang="ts">
 import {untrack} from 'svelte';
 import {messages} from './lib/localization.ts';
 import {primaryAction} from './lib/primary-action.ts';
 import {stationBuildingFence} from './lib/station-building-edit.ts';
 import {passageQuery} from './lib/passage-edit.ts';
 import type {ShellModel,ShellState} from './lib/shell-model.ts';
 import type {Position,PassagePreview,PassageQuery} from './generated/contracts.ts';
 let {model,view,picked,onpreview,onpick,onframe,level=$bindable(2),heightCut,onreveal}:{model:ShellModel;view:ShellState;picked?:Position;onpreview:(p?:PassagePreview)=>void;onpick:()=>void;onframe:(p:Position[])=>void;level?:number;heightCut:number;onreveal:()=>void}=$props();
 const text=$derived(messages[view.locale]);
 let action=$state<'place'|'remove'>('place'),material=$state<PassageQuery['material']>('concrete');
 let x=$state(0),y=$state(0),endX=$state(1),endY=$state(0),anchor=$state<Position>();
 let result=$state<PassagePreview>(),checking=$state(false),failed=$state(false),stale=$state(false);
 let fence=$state<string>(),version=0,request:AbortController|undefined;
 const blocked=$derived(view.pending||view.connection!=='ready'||view.role==='observer'||view.feedback==='unresolved');
 function reset(){version++;request?.abort();fence=undefined;result=undefined;checking=false;failed=false;stale=false;onpreview();}
 function edit(){anchor=undefined;reset();}
 export function cancelDraft(){const had=!!result||checking||!!anchor;anchor=undefined;reset();return had;}
 export function isRemoving(){return action==='remove';}
 export function usesAutomaticPick(){return true;}
 export function pickPolicy(){return anchor?{kind:'sameLevel' as const,level:anchor.z}:{kind:'thinPlatformOrGround' as const};}
 async function check(){
  reset();if(blocked||!view.read)return;
  if(![x,y,endX,endY,level].every(Number.isSafeInteger)){failed=true;return;}
  const q=passageQuery(view.read,{x,y,z:level},{x:endX,y:endY,z:level},material,action==='remove');if(!q)return;
  fence=stationBuildingFence(view.read);const id=version;request=new AbortController();checking=true;
  try{const p=await model.previewPassage(q,request.signal);if(id!==version||blocked||fence!==stationBuildingFence(view.read))return;result=p;onpreview(p);}
  catch{if(id===version)failed=true;}finally{if(id===version)checking=false;}
 }
 async function commit(){
  if(blocked||!result?.legal||fence!==stationBuildingFence(view.read))return;
  const p=result;anchor=undefined;reset();await model.send({kind:'editPassage',query:p.query,expectedCost:p.cost});
 }
 $effect(()=>{const p=picked;if(p)untrack(()=>{
  reset();if(!anchor){anchor=p;x=p.x;y=p.y;level=p.z;return;}
  // Source align4To: select the dominant horizontal axis at the anchor height.
  if(Math.abs(p.x-anchor.x)>=Math.abs(p.y-anchor.y)){endX=p.x;endY=anchor.y;}else{endX=anchor.x;endY=p.y;}
  void check();
 });});
 $effect(()=>{void view.locale;untrack(()=>{anchor=undefined;reset();});});
 $effect(()=>{if(fence&&fence!==stationBuildingFence(view.read)){reset();anchor=undefined;stale=true;}if(view.connection!=='ready'||view.role==='observer')untrack(()=>{anchor=undefined;reset();});});
 $effect(()=>()=>{version++;request?.abort();onpreview();});
</script>
<section class="passage-task" use:primaryAction aria-label={text.passageTab}>
 <p class="hint">{text.passageSummary}</p>
 <form onsubmit={e=>{e.preventDefault();void check();}}>
 <fieldset disabled={blocked}>
 <label>{text.passageAction}<select aria-label={text.passageAction} bind:value={action} onchange={edit}><option value="place">{text.stationaryPlace}</option><option value="remove">{text.railRemove}</option></select></label>
 <label>{text.passageMaterial}<select aria-label={text.passageMaterial} bind:value={material} onchange={edit}><option value="concrete">{text.passageConcrete}</option><option value="brown">{text.passageBrown}</option><option value="green">{text.passageGreen}</option></select></label>
 <div class="form-actions">
 <label>{text.passageStartX}<input type="number" step="1" bind:value={x} oninput={edit}/></label>
 <label>{text.passageStartY}<input type="number" step="1" bind:value={y} oninput={edit}/></label>
 <label>{text.passageEndX}<input type="number" step="1" bind:value={endX} oninput={edit}/></label>
 <label>{text.passageEndY}<input type="number" step="1" bind:value={endY} oninput={edit}/></label>
 <label>{text.passageLevel}<input type="number" step="1" bind:value={level} oninput={edit}/></label>
 </div>
 <button type="button" onclick={()=>{anchor=undefined;reset();onpick();}}>{text.passagePick}</button>
 <button disabled={checking}>{text.stationCheck}</button>
 </fieldset>
 </form>
 <details><summary>{text.help}</summary><p class="hint">{text.passageHelp}</p></details>
 <p role="status" data-testid="passage-status">{checking?text.checkingGeometry:failed?text.geometryFailed:stale?text.railStale:result?result.legal?text.passageLegal:result.reason==='outside'?text.trainOutside:result.reason==='passageTarget'?text.passageTarget:text.passageGeometry:anchor?text.passagePickEnd:text.passagePick}</p>
 {#if level>heightCut}<button onclick={onreveal}>{text.revealPreview}</button>{/if}
 {#if result}<p data-testid="passage-cost">{text.cornerCost}: ¥{result.cost} · {text.stationCells}: {result.positions.length}</p>{/if}
 {#if result?.positions.length}<button type="button" onclick={()=>onframe(result!.positions)}>{text.passageFrame}</button>{/if}
 <div class="form-actions"><button class="primary" disabled={blocked||!result?.legal} onclick={commit}>{action==='remove'?text.confirmRemoval:text.passageConfirm}</button><button disabled={view.pending} onclick={()=>{anchor=undefined;reset();}}>{text.cancelPreview}</button></div>
</section>
<style>
 fieldset > label {display:grid;gap:3px;margin-block:6px;}
 .form-actions label {max-width:7rem;display:grid;}
 input {min-width:0;width:100%;}
</style>
