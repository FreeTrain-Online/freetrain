<script lang="ts">
 import {WorldSpace} from './lib/map-geometry.ts';
 import {untrack} from 'svelte';
 import {messages} from './lib/localization.ts';
 import {primaryAction} from './lib/primary-action.ts';
 import {bundledCatalog} from './lib/bundled-catalog.ts';
 import {stationDefinitions,stationCatalog,stationBuildingQuery,stationBuildingFence} from './lib/station-building-edit.ts';
 import {railwayReasons} from './lib/railway-edit.ts';
 import ObjectProvenance from './ObjectProvenance.svelte';
 import type {ShellModel,ShellState} from './lib/shell-model.ts';
 import type {Position,StationBuildingPreview} from './generated/contracts.ts';
 let {model,view,picked,onpreview,onpick,level=$bindable(1),heightCut,onreveal,oncatalog}:{model:ShellModel;view:ShellState;picked?:Position;onpreview:(p?:StationBuildingPreview)=>void;onpick:()=>void;level?:number;heightCut:number;onreveal:()=>void;oncatalog:()=>void}=$props();
 const text=$derived(messages[view.locale]);
 let catalog=$state<Awaited<ReturnType<typeof bundledCatalog>>>();
 $effect(()=>{let active=true;void bundledCatalog().then(c=>{if(active)catalog=c;});return()=>{active=false;};});
 const items=$derived(catalog?stationCatalog(catalog,view.locale):[]);
 const definitionId=$derived(view.catalogSelections.station);
 const selected=$derived(items.find(d=>d.reference.recipeId===definitionId));
 let x=$state(0),y=$state(0),autoLevel=$state(true);
 let detectCoordinates=true;
 let result=$state<StationBuildingPreview>(),status=$state<'idle'|'checking'|'ready'|'failed'|'stale'|'missing'>('idle');
 let fence=$state<string>(),submitting=false,version=0,request:AbortController|undefined;
 const blocked=$derived(view.pending||view.connection!=='ready'||view.role==='observer'||view.feedback==='unresolved');
 function reset(){version++;request?.abort();fence=undefined;result=undefined;status='idle';onpreview();}
 export function cancelDraft(){const had=status!=='idle';reset();return had;}
 export function usesAutomaticPick(){return autoLevel;}
 function edit(){reset();}
 async function check(){
  reset();if(blocked||!view.read)return;
  if(![x,y,level].every(Number.isSafeInteger)){status='failed';return;}
  const read=view.read;
  if(autoLevel&&detectCoordinates&&read.terrain){
   const space=new WorldSpace(read.map.width,read.map.height,read.map.levels),p={x,y,z:0};
   if(space.containsHorizontal(p)){const cell=space.storage(p);level=read.terrain.cells[cell.v*space.horizontalSize+cell.h].ground;}
  }
  const query=stationBuildingQuery(read,{x,y,z:level},stationDefinitions.find(d=>d.reference.recipeId===definitionId)!.reference);
  if(!query){status='missing';return;}
  fence=stationBuildingFence(read);const id=version;request=new AbortController();status='checking';
  try{const p=await model.previewStationBuilding(query,request.signal);if(id!==version||fence!==stationBuildingFence(view.read)||blocked)return;result=p;status='ready';onpreview(p);}
  catch{if(id===version)status='failed';}
 }
 async function commit(){
  if(blocked||!result?.legal||status!=='ready'||fence!==stationBuildingFence(view.read))return;
  const id=version;submitting=true;
  const accepted=await model.send({kind:'editStationBuilding',query:result.query,expectedCost:result.cost});submitting=false;
  if(id!==version)return;reset();if(!accepted)status='stale';
 }
 $effect(()=>{void view.locale;void definitionId;untrack(reset);});
 $effect(()=>{const p=picked;if(p)untrack(()=>{reset();x=p.x;y=p.y;detectCoordinates=false;if(autoLevel)level=p.z;void check();});});
 $effect(()=>{const current=stationBuildingFence(view.read);if(fence&&fence!==current&&!submitting){reset();status='stale';}if(view.connection!=='ready'||view.role==='observer')untrack(reset);});
 $effect(()=>()=>{version++;request?.abort();onpreview();});
 const reasons={...railwayReasons,stationGround:'stationGround',stationNotFound:'stationNotFound',unavailableStationBinding:'railBinding'} as const;
</script>
<section class="station-building-task" use:primaryAction aria-label={text.stationTask}>
 <p class="hint">{text.stationServicePending}</p>
 <form onsubmit={e=>{e.preventDefault();void check();}}>
 <fieldset disabled={view.pending}>
 <p data-testid="catalog-selection">{text.stationBuildingType}: {selected?.title}{#if selected?.orientation} · {selected.orientation}{/if}</p>
 <button type="button" onclick={oncatalog}>{text.chooseFromCatalog}</button>
 {#if selected}<p>{selected.description}</p><p>{text.stationaryDimensions}: {selected.sizeX} × {selected.sizeY} × {selected.height} · {text.price}: ¥{BigInt(selected.price).toLocaleString(view.locale)} · {text.stationUpkeep}: ¥{BigInt(selected.upkeep).toLocaleString(view.locale)}</p><ObjectProvenance id={definitionId} locale={view.locale}/>{/if}
 <div class="form-actions"><label>X<input aria-label={text.stationX} type="number" step="1" bind:value={x} oninput={()=>{detectCoordinates=true;edit();}}/></label><label>Y<input aria-label={text.stationY} type="number" step="1" bind:value={y} oninput={()=>{detectCoordinates=true;edit();}}/></label><label>{text.stationLevel}<input aria-label={text.stationLevel} type="number" step="1" bind:value={level} oninput={()=>{autoLevel=false;edit();}}/></label></div>
 <label><input type="checkbox" bind:checked={autoLevel} onchange={()=>{detectCoordinates=true;edit();}}/>{text.railAutoLevel}</label>
 <button type="button" onclick={()=>{reset();onpick();}}>{text.stationPick}</button>
 </fieldset>
 <p class="hint">{text.stationBuildingHelp}</p>
 <button disabled={blocked||status==='checking'}>{text.stationCheck}</button>
 </form>
 <p role="status" data-testid="station-status">{status==='checking'?text.checkingGeometry:status==='failed'?text.geometryFailed:status==='stale'?text.railStale:status==='missing'?text.stationNotFound:result?result.legal?text.stationLegal:text[reasons[result.reason as keyof typeof reasons]??'geometryFailed']:text.stationPick}</p>
 {#if result}<p data-testid="station-cost">{text.cornerCost}: ¥{BigInt(result.cost).toLocaleString(view.locale)} · {text.stationCells}: {result.positions.length}</p>
 {#if result.losses.length}<details open><summary>{text.railLossDetails}</summary><ul>{#each result.losses as loss}<li>{text.railReclamationLoss}: {loss.ownerId} · {loss.position.x}, {loss.position.y}, {loss.position.z}</li>{/each}</ul></details>{/if}{/if}
 {#if level>heightCut}<button onclick={onreveal}>{text.revealPreview}</button>{/if}
 <div class="form-actions"><button class="primary" disabled={blocked||status!=='ready'||!result?.legal} onclick={commit}>{text.stationConfirm}</button><button disabled={view.pending} onclick={reset}>{text.cancelPreview}</button></div>
</section>
