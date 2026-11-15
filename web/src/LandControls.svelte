<script lang="ts">
 import {untrack} from 'svelte';
 import {messages} from './lib/localization.ts';
 import {landQuery} from './lib/land-edit.ts';
 import {currentQuote,constructionFence} from './lib/quote-validity.ts';
 import {WorldSpace} from './lib/map-geometry.ts';
 import {primaryAction} from './lib/primary-action.ts';
 import type {ShellModel,ShellState} from './lib/shell-model.ts';
 import type {LandPreview,Position} from './generated/contracts.ts';
 let {model,view,picked,onpreview,onpick,level=$bindable(1),heightCut,onreveal}:{model:ShellModel;view:ShellState;picked?:Position;onpreview:(p?:LandPreview)=>void;onpick:()=>void;level?:number;heightCut:number;onreveal:()=>void}=$props();
 const text=$derived(messages[view.locale]);
 let ax=$state(0),ay=$state(0),ex=$state(0),ey=$state(0),action=$state<'buy'|'sell'>('buy'),autoLevel=$state(true),first=$state<Position>();
 let result=$state<LandPreview>(),status=$state<'idle'|'checking'|'ready'|'failed'|'stale'>('idle');
 let fence:string|undefined,version=0,request:AbortController|undefined,submitting=false;
 const blocked=$derived(view.pending||view.connection!=='ready'||view.role==='observer'||view.feedback==='unresolved');
 function reset(){version++;request?.abort();result=undefined;status='idle';fence=undefined;onpreview();}
 export function cancelDraft(){const had=status!=='idle'||!!first;reset();first=undefined;return had;}
 export function usesAutomaticPick(){return !first&&autoLevel;}
 async function check(){
  reset();if(blocked||!view.read)return;
  if(![ax,ay,ex,ey,level].every(Number.isSafeInteger)){status='failed';return;}
  if(autoLevel&&view.read.terrain){const s=new WorldSpace(view.read.map.width,view.read.map.height,view.read.map.levels),p={x:ax,y:ay,z:0};if(s.containsHorizontal(p)){const hv=s.storage(p);level=view.read.terrain.cells[hv.v*s.horizontalSize+hv.h].ground;}}
  const query=landQuery(view.read,{x:ax,y:ay,z:level},{x:ex,y:ey,z:level},action);if(!query){status='failed';return;}
  fence=constructionFence(view.read);const id=version;request=new AbortController();status='checking';
  try{const p=await model.previewLand(query,request.signal);if(id!==version||fence!==constructionFence(view.read)||blocked)return;result=p;status='ready';onpreview(p);}catch{if(id===version)status='failed';}
 }
 async function commit(){
  if(blocked||status!=='ready'||!result?.legal||fence!==constructionFence(view.read))return;
  const id=version;submitting=true;const accepted=await model.send({kind:'tradeLand',query:result.query,expectedAmount:result.amount});submitting=false;
  if(id!==version)return;reset();first=undefined;if(!accepted)status='stale';
 }
 $effect(()=>{void text;void action;untrack(()=>{reset();first=undefined;});});
 $effect(()=>{const p=picked;if(p)untrack(()=>{reset();if(!first){first={...p};ax=ex=p.x;ay=ey=p.y;level=p.z;onpreview({query:landQuery(view.read!,p,p,action)!,legal:false,reason:'draft',amount:'0',cells:[],reclaimed:[],positions:[p]});}else{ex=p.x;ey=p.y;first=undefined;void check();}});});
 $effect(()=>{const read=view.read;if(fence&&(fence!==constructionFence(read)||result&&!currentQuote(read,result.query))&&!submitting){untrack(reset);status='stale';}if(view.connection!=='ready'||view.role==='observer')untrack(cancelDraft);});
 $effect(()=>()=>{version++;request?.abort();onpreview();});
 const skipped=$derived(result?.cells.filter(c=>!['buyable','sellable'].includes(c.status))??[]);
 const skipReasons={outside:'landOutside',underground:'landUnderground',protected:'landProtected',owned:'landAlreadyOwned',notProperty:'landNotProperty',vacant:'landNotProperty'} as const;
 const eligible=$derived(result?.cells.filter(c=>c.status==='buyable'||c.status==='sellable')??[]);
</script>
<section class="tool-card track-task land-task" use:primaryAction aria-label={text.landTask}>
 <h2>{text.landTask}</h2>
 <form onsubmit={e=>{e.preventDefault();first=undefined;void check();}}>
 <fieldset disabled={view.pending}>
 <label>{text.landAction}<select aria-label={text.landAction} bind:value={action}><option value="buy">{text.landBuy}</option><option value="sell">{text.landSell}</option></select></label>
 <div class="form-actions"><label>{text.landAnchorX}<input aria-label={text.landAnchorX} type="number" bind:value={ax} oninput={cancelDraft}/></label><label>{text.landAnchorY}<input aria-label={text.landAnchorY} type="number" bind:value={ay} oninput={cancelDraft}/></label></div>
 <div class="form-actions"><label>{text.landEndX}<input aria-label={text.landEndX} type="number" bind:value={ex} oninput={cancelDraft}/></label><label>{text.landEndY}<input aria-label={text.landEndY} type="number" bind:value={ey} oninput={cancelDraft}/></label></div>
 <label>{text.landLevel}<input aria-label={text.landLevel} type="number" bind:value={level} oninput={()=>{autoLevel=false;cancelDraft();}}/></label>
 <label><input type="checkbox" bind:checked={autoLevel} onchange={cancelDraft}/>{text.railAutoLevel}</label>
 <button type="button" onclick={()=>{cancelDraft();onpick();}}>{text.landPick}</button>
 </fieldset><p class="hint">{text.landHelp}</p><button disabled={blocked||status==='checking'}>{text.landCheck}</button>
 </form>
 <p role="status" data-testid="land-status">{status==='checking'?text.checkingGeometry:status==='failed'?text.geometryFailed:status==='stale'?text.railStale:result?result.legal?text.cityLegal:result.reason==='noEligibleCells'?text.landNoCells:text.cityInvalid:text.landPick}</p>
 {#if result}<details><summary>{text.landSkipped}: {skipped.length}</summary><ul>{#each skipped as c}<li>{c.position.x}, {c.position.y}, {c.position.z} · {text[skipReasons[c.status as keyof typeof skipReasons]??'landNotProperty']}</li>{/each}</ul></details>
 {#if result.reclaimed.length}<details open><summary>{text.landReclaimed}: {result.reclaimed.length}</summary><ul>{#each result.reclaimed as r}<li>{r.ownerId} · {r.position.x}, {r.position.y}, {r.position.z} · ¥{(BigInt(r.value)+BigInt(r.removalCost)).toLocaleString(view.locale)}</li>{/each}</ul></details>{/if}{/if}
 {#if level>heightCut}<button onclick={onreveal}>{text.revealPreview}</button>{/if}
 <div class="form-actions">{#if result}<p style="flex-basis:100%;margin:0" data-testid="land-counts">{text.landSelected}: {eligible.length} · {text.landSkipped}: {skipped.length}</p><p style="flex-basis:100%;margin:0" data-testid="land-amount">{text[action==='buy'?'landDebit':'landCredit']}: ¥{BigInt(result.amount).toLocaleString(view.locale)}</p>{/if}<button class="primary" disabled={blocked||status!=='ready'||!result?.legal} onclick={commit}>{text.landConfirm}</button><button disabled={view.pending} onclick={cancelDraft}>{text.cancelPreview}</button></div>
</section>
