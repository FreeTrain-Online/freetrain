<script lang="ts">
 import {WorldSpace} from './lib/map-geometry.ts';
 import {untrack} from 'svelte';
 import {messages} from './lib/localization.ts';
 import {primaryAction} from './lib/primary-action.ts';
 import {stationBuildingFence} from './lib/station-building-edit.ts';
 import {thinPlatformQuery} from './lib/thin-platform-edit.ts';
 import {railwayReasons} from './lib/railway-edit.ts';
 import type {ShellModel,ShellState} from './lib/shell-model.ts';
 import type {Position,Heading,ThinPlatformPreview} from './generated/contracts.ts';
 let {model,view,picked,removeTarget,onpreview,onpick,onframe,level=$bindable(1),heightCut,onreveal}:{model:ShellModel;view:ShellState;picked?:Position;removeTarget?:Position;onpreview:(p?:ThinPlatformPreview)=>void;onpick:()=>void;onframe:(positions:Position[])=>void;level?:number;heightCut:number;onreveal:()=>void}=$props();
 const text=$derived(messages[view.locale]);
 let action=$state<'place'|'remove'>('place'),direction=$state<Heading>(2),length=$state(4),roof=$state(true);
 let x=$state(0),y=$state(0),autoLevel=$state(true);
 let detectCoordinates=true;
 let result=$state<ThinPlatformPreview>(),status=$state<'idle'|'checking'|'ready'|'failed'|'stale'|'missing'|'invalid'>('idle');
 let fence=$state<string>(),submitting=false,version=0,request:AbortController|undefined;
 const blocked=$derived(view.pending||view.connection!=='ready'||view.role==='observer'||view.feedback==='unresolved');
 function reset(){version++;request?.abort();fence=undefined;result=undefined;status='idle';onpreview();}
 export function cancelDraft(){const had=status!=='idle';reset();return had;}
 export function isRemoving(){return action==='remove';}
 export function usesAutomaticPick(){return autoLevel;}
 function edit(){reset();}
 async function check(){
  reset();if(blocked||!view.read)return;
  if(![x,y,level].every(Number.isSafeInteger)){status='failed';return;}
  if(!Number.isInteger(length)||length<1||length>100){status='invalid';return;}
  const read=view.read;
  if(autoLevel&&detectCoordinates&&read.terrain){
   const space=new WorldSpace(read.map.width,read.map.height,read.map.levels),p={x,y,z:0};
   if(space.containsHorizontal(p)){const cell=space.storage(p);level=read.terrain.cells[cell.v*space.horizontalSize+cell.h].ground;}
  }
  const query=thinPlatformQuery(read,{x,y,z:level},direction,length,roof,action==='remove');
  if(!query){status='missing';return;}
  fence=stationBuildingFence(read);const id=version;request=new AbortController();status='checking';
  try{const p=await model.previewThinPlatform(query,request.signal);if(id!==version||fence!==stationBuildingFence(view.read)||blocked)return;result=p;status='ready';onpreview(p);}
  catch{if(id===version)status='failed';}
 }
 async function commit(){
  if(blocked||!result?.legal||status!=='ready'||fence!==stationBuildingFence(view.read))return;
  const id=version;submitting=true;
  const accepted=await model.send({kind:'editThinPlatform',query:result.query,expectedCost:result.cost});submitting=false;
  if(id!==version)return;reset();if(!accepted)status='stale';
 }
 $effect(()=>{void view.locale;untrack(reset);});
 $effect(()=>{const p=removeTarget??picked;if(p)untrack(()=>{reset();if(removeTarget)action='remove';x=p.x;y=p.y;detectCoordinates=false;if(autoLevel||removeTarget)level=p.z;void check();});});
 $effect(()=>{const current=stationBuildingFence(view.read);if(fence&&fence!==current&&!submitting){reset();status='stale';}if(view.connection!=='ready'||view.role==='observer')untrack(reset);});
 $effect(()=>()=>{version++;request?.abort();onpreview();});
 const reasons={...railwayReasons,stationGround:'stationGround',platformNotFound:'platformNotFound',invalidPlatformGeometry:'platformGeometry',incompatiblePlatformRail:'platformRail',unavailableStationBinding:'railBinding'} as const;
</script>
<section class="platform-task" use:primaryAction aria-label={text.platformTab}>
 <p class="hint">{text.platformDisconnected}</p>
 <form onsubmit={e=>{e.preventDefault();void check();}}>
 <fieldset disabled={view.pending}>
 <label>{text.platformAction}<select aria-label={text.platformAction} bind:value={action} onchange={edit}><option value="place">{text.stationaryPlace}</option><option value="remove">{text.railRemove}</option></select></label>
 {#if action==='place'}
 <label>{text.platformStyle}<select aria-label={text.platformStyle} bind:value={roof} onchange={edit}><option value={true}>{text.platformRoofed}</option><option value={false}>{text.platformUnroofed}</option></select></label>
 <label>{text.platformDirection}<select aria-label={text.platformDirection} bind:value={direction} onchange={edit}><option value={0}>{text.headingNorth}</option><option value={2}>{text.headingEast}</option><option value={4}>{text.headingSouth}</option><option value={6}>{text.headingWest}</option></select></label>
 <label>{text.platformLength}<input aria-label={text.platformLength} type="number" min="1" max="100" step="1" bind:value={length} oninput={edit}/></label>
 <p>{text.stationUpkeep}: ¥{(180000n*BigInt(Number.isSafeInteger(length)?length:0)).toLocaleString(view.locale)}</p>
 {/if}
 <div class="form-actions"><label>X<input aria-label={text.platformX} type="number" step="1" bind:value={x} oninput={()=>{detectCoordinates=true;edit();}}/></label><label>Y<input aria-label={text.platformY} type="number" step="1" bind:value={y} oninput={()=>{detectCoordinates=true;edit();}}/></label><label>{text.platformLevel}<input aria-label={text.platformLevel} type="number" step="1" bind:value={level} oninput={()=>{autoLevel=false;edit();}}/></label></div>
 <label class="automatic"><input type="checkbox" bind:checked={autoLevel} onchange={()=>{detectCoordinates=true;edit();}}/>{text.railAutoLevel}</label>
 <button type="button" onclick={()=>{reset();onpick();}}>{text.platformPick}</button>
 </fieldset>
 <details><summary>{text.help}</summary><p class="hint">{text.platformHelp}</p></details>
 <button disabled={blocked||status==='checking'}>{text.stationCheck}</button>
 </form>
 <p role="status" data-testid="platform-status">{status==='checking'?text.checkingGeometry:status==='failed'?text.geometryFailed:status==='stale'?text.railStale:status==='missing'?text.platformNotFound:status==='invalid'?text.platformGeometry:result?result.legal?text.platformLegal:text[reasons[result.reason as keyof typeof reasons]??'geometryFailed']:text.platformPick}</p>
 {#if level>heightCut}<button onclick={onreveal}>{text.revealPreview}</button>{/if}
 <div class="platform-confirm">
 {#if result}<p data-testid="platform-cost">{text.cornerCost}: ¥{BigInt(result.cost).toLocaleString(view.locale)} · {text.stationCells}: {result.positions.length}</p>
 {/if}
 {#if result?.positions.length}<button type="button" onclick={()=>onframe(result!.positions)}>{text.platformFrame}</button>{/if}
 <div class="form-actions"><button class="primary" disabled={blocked||status!=='ready'||!result?.legal} onclick={commit}>{text[action==='remove'?'confirmRemoval':'platformConfirm']}</button><button disabled={view.pending} onclick={reset}>{text.cancelPreview}</button></div>
 </div>
</section>

<style>
 fieldset > label {display:grid;gap:3px;margin-block:6px;}
 fieldset > label.automatic {display:flex;align-items:center;}
 .platform-confirm {position:sticky;bottom:-8px;padding-block:4px;background:var(--ui-window-background);}
 .platform-confirm p {margin-block:4px;}
</style>
