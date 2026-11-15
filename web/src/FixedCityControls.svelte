<script lang="ts">
 import {currentQuote} from './lib/quote-validity.ts';
 import {WorldSpace} from './lib/map-geometry.ts';
 import {untrack} from 'svelte';
 import {messages} from './lib/localization.ts';
 import {primaryAction} from './lib/primary-action.ts';
 import {bundledCatalog} from './lib/bundled-catalog.ts';
 import {fixedCityDefinitions,fixedCityCatalog,fixedCityQuery,fixedCityFence} from './lib/fixed-city-edit.ts';
 import {railwayReasons} from './lib/railway-edit.ts';
 import ObjectProvenance from './ObjectProvenance.svelte';
 import type {ShellModel,ShellState} from './lib/shell-model.ts';
 import type {Position,FixedCityPreview} from './generated/contracts.ts';
 let {model,view,picked,onpreview,onpick,level=$bindable(1),heightCut,onreveal,oncatalog}:{model:ShellModel;view:ShellState;picked?:Position;onpreview:(p?:FixedCityPreview)=>void;onpick:()=>void;level?:number;heightCut:number;onreveal:()=>void;oncatalog:()=>void}=$props();
 const text=$derived(messages[view.locale]);
 let catalog=$state<Awaited<ReturnType<typeof bundledCatalog>>>();
 $effect(()=>{let active=true;void bundledCatalog().then(c=>{if(active)catalog=c;});return()=>{active=false;};});
 const items=$derived(catalog?fixedCityCatalog(catalog,view.locale):[]);
 const definitionId=$derived(view.catalogSelections.city);
 const selected=$derived(items.find(d=>d.reference.recipeId===definitionId));
 let x=$state(0),y=$state(0),autoLevel=$state(true),floors=$state(4);
 const validHeight=$derived(Number.isSafeInteger(floors)&&floors%4===0&&!!selected&&floors>=selected.minimumHeight*4&&floors<=selected.maximumHeight*4);
 $effect(()=>{floors=(selected?.minimumHeight??1)*4;});
 let detectCoordinates=true;
 let result=$state<FixedCityPreview>(),status=$state<'idle'|'checking'|'ready'|'failed'|'stale'|'missing'>('idle');
 let fence=$state<string>(),submitting=false,version=0,request:AbortController|undefined;
 const blocked=$derived(view.pending||view.connection!=='ready'||view.role==='observer'||view.feedback==='unresolved');
 function reset(){version++;request?.abort();fence=undefined;result=undefined;status='idle';onpreview();}
 export function cancelDraft(){const had=status!=='idle';reset();return had;}
 export function usesAutomaticPick(){return autoLevel;}
 function edit(){reset();}
 async function check(){
  reset();if(blocked||!view.read)return;
  if(![x,y,level,floors].every(Number.isSafeInteger)||floors%4!==0||!selected){status='failed';return;}
  const read=view.read;
  if(autoLevel&&detectCoordinates&&read.terrain){
   const space=new WorldSpace(read.map.width,read.map.height,read.map.levels),p={x,y,z:0};
   if(space.containsHorizontal(p)){const cell=space.storage(p);level=read.terrain.cells[cell.v*space.horizontalSize+cell.h].ground;}
  }
  const query=fixedCityQuery(read,{x,y,z:level},fixedCityDefinitions.find(d=>d.reference.recipeId===definitionId)!.reference,false,selected.kind==='variable'?floors/4:undefined);
  if(!query){status='missing';return;}
  fence=fixedCityFence(read);const id=version;request=new AbortController();status='checking';
  try{const p=await model.previewFixedCity(query,request.signal);if(id!==version||fence!==fixedCityFence(view.read)||blocked)return;result=p;status='ready';onpreview(p);}
  catch{if(id===version)status='failed';}
 }
 async function commit(){
  if(blocked||!result?.legal||status!=='ready'||fence!==fixedCityFence(view.read))return;
  const id=version;submitting=true;
  const accepted=await model.send({kind:'editFixedCity',query:result.query,expectedCost:result.cost});submitting=false;
  if(id!==version)return;reset();if(!accepted)status='stale';
 }
 // Derived text stays stable across snapshots; direct view reads would reset every tick.
 $effect(()=>{void text;void definitionId;void floors;untrack(reset);});
 $effect(()=>{const p=picked;if(p)untrack(()=>{reset();x=p.x;y=p.y;detectCoordinates=false;if(autoLevel)level=p.z;void check();});});
 $effect(()=>{const current=fixedCityFence(view.read);if(fence&&(fence!==current||result&&!currentQuote(view.read,result.query))&&!submitting){reset();status='stale';}if(view.connection!=='ready'||view.role==='observer')untrack(reset);});
 $effect(()=>()=>{version++;request?.abort();onpreview();});
 const reasons={invalidPlacement:'cityInvalid',...railwayReasons,stationGround:'stationGround',stationNotFound:'stationNotFound',unavailableStationBinding:'railBinding'} as const;
</script>
<section class="tool-card track-task city-task" use:primaryAction aria-label={text.cityTask}>
 <h2>{text.cityTask}</h2>
 <form onsubmit={e=>{e.preventDefault();void check();}}>
 <fieldset disabled={view.pending}>
 <p data-testid="catalog-selection">{text.cityType}: {selected?.title}{#if selected?.orientation} · {selected.orientation}{/if}</p>
 <button type="button" onclick={oncatalog}>{text.chooseFromCatalog}</button>
 {#if selected}<p>{selected.description}</p><p>{text.stationaryDimensions}: {selected.sizeX} × {selected.sizeY} × {selected.kind==='variable'?floors/4:selected.minimumHeight} · {text.price}: ¥{validHeight?(BigInt(selected.unitPrice)*BigInt(selected.kind==='variable'?floors/4:1)).toLocaleString(view.locale):'—'}</p><ObjectProvenance id={definitionId} locale={view.locale}/>{/if}
 {#if selected?.kind==='variable'}<label>{text.cityHeight}<input aria-label={text.cityHeight} type="number" min={selected.minimumHeight*4} max={selected.maximumHeight*4} step="4" bind:value={floors} oninput={edit}/></label>{/if}
 <div class="form-actions"><label>X<input aria-label={text.cityX} type="number" step="1" bind:value={x} oninput={()=>{detectCoordinates=true;edit();}}/></label><label>Y<input aria-label={text.cityY} type="number" step="1" bind:value={y} oninput={()=>{detectCoordinates=true;edit();}}/></label><label>{text.cityLevel}<input aria-label={text.cityLevel} type="number" step="1" bind:value={level} oninput={()=>{autoLevel=false;edit();}}/></label></div>
 <label><input type="checkbox" bind:checked={autoLevel} onchange={()=>{detectCoordinates=true;edit();}}/>{text.railAutoLevel}</label>
 <button type="button" onclick={()=>{reset();onpick();}}>{text.cityPick}</button>
 </fieldset>
 <p class="hint">{text.cityHelp}</p>
 <button disabled={blocked||status==='checking'}>{text.stationCheck}</button>
 </form>
 <p role="status" data-testid="city-status">{status==='checking'?text.checkingGeometry:status==='failed'?text.geometryFailed:status==='stale'?text.railStale:status==='missing'?text.cityMissing:result?result.legal?text.cityLegal:text[reasons[result.reason as keyof typeof reasons]??'geometryFailed']:text.cityPick}</p>
 {#if result}
 {#if result.losses.length}<details open><summary>{text.railLossDetails}</summary><ul>{#each result.losses as loss}<li>{text.railReclamationLoss}: {loss.ownerId} · {loss.position.x}, {loss.position.y}, {loss.position.z}</li>{/each}</ul></details>{/if}{/if}
 {#if level>heightCut}<button onclick={onreveal}>{text.revealPreview}</button>{/if}
 <div class="form-actions">{#if result}<p class="city-cost-review" data-testid="city-cost">{text.cornerCost}: ¥{BigInt(result.cost).toLocaleString(view.locale)} · {text.cityCells}: {result.positions.length}</p>{/if}<button class="primary" disabled={blocked||status!=='ready'||!result?.legal} onclick={commit}>{text.cityConfirm}</button><button disabled={view.pending} onclick={reset}>{text.cancelPreview}</button></div>
</section>

<style>.city-cost-review {flex-basis:100%;margin:0 0 4px;}</style>
