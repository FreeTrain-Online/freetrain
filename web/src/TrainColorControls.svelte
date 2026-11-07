<script lang="ts">
 import {messages} from './lib/localization.ts';
 import {bundledCatalog} from './lib/bundled-catalog.ts';
 import {RecipeImageCache,instantiate} from './lib/content-recipes.ts';
 import {TrainColorCatalog,colorTestCarId,colorMapXML,colorTemplates,defaultTrainColors} from './lib/train-colors.ts';
 import type {FleetColorTest} from './generated/contracts.ts';
 import type {ShellModel,ShellState} from './lib/shell-model.ts';
 import DecisionModal from './DecisionModal.svelte';
 let {model,view,draft=$bindable()}:{model:ShellModel;view:ShellState;draft?:{configuration:FleetColorTest;expectedRevision:string;epoch:string}}=$props();
 const text=$derived(messages[view.locale]),saved=$derived(view.read?.mixed?.fleet?.colorTest);
 const configuration=$derived(draft?.configuration??saved??defaultTrainColors);
 const cache=new RecipeImageCache(),colors=new TrainColorCatalog(cache);
 let templates=$state<{id:string;en:string;ja:string}[]>([]),preview=$state<{day:string;night:string}>(),failed=$state(false),confirming=$state(false),copyFailed=$state(false);
 const stale=$derived(!!draft&&(draft.epoch!==view.read?.worldEpoch||draft.expectedRevision!==(saved?.revision??'0')));
 const disabled=$derived(view.pending||view.connection!=='ready'||view.role==='observer'||!view.read||!['terrain-state-1','mixed-railway-1'].includes(view.read.ruleset??''));
 const fields=['body','line1','line2','line3'] as const;
 const fieldLabels={body:'trainColorBody',line1:'trainColorLine1',line2:'trainColorLine2',line3:'trainColorLine3'} as const;
 function change(patch:Partial<FleetColorTest>){
  if(!draft)draft={configuration:{...configuration,template:{...configuration.template}},expectedRevision:saved?.revision??'0',epoch:view.read!.worldEpoch};
  draft={...draft,configuration:{...draft.configuration,...patch}};
 }
 $effect(()=>{
  const c=configuration;let active=true;failed=false;preview=undefined;
  void bundledCatalog().then(async catalog=>{
   if(active)templates=colorTemplates(catalog);
   const derived=await colors.prepare(catalog,c),recipe=derived.renderRecipes.find(r=>r.id===colorTestCarId)!;
   const result={} as {day:string;night:string};
   for(const night of [false,true]){
    const scene=await cache.ready(derived,instantiate(recipe,'preview',[0,0,0],{angle:6}),{season:'spring',night});
    const canvas=document.createElement('canvas');canvas.width=192;canvas.height=96;const ctx=canvas.getContext('2d')!;
    ctx.fillStyle=night?'#17232b':'#d8e5d9';ctx.fillRect(0,0,192,96);scene.draw(ctx,{x:48,y:0,zoom:3});result[night?'night':'day']=canvas.toDataURL();
   }
   if(active)preview=result;
  }).catch(()=>{if(active)failed=true;});
  return ()=>{active=false;};
 });
 async function apply(){
  if(!draft||stale||disabled)return;
  const value=draft;
  const ok=await model.send({kind:'configureFleetColor',expectedRevision:value.expectedRevision,configuration:{...value.configuration,revision:String(BigInt(value.expectedRevision)+1n)}});
  if(ok&&draft===value){draft=undefined;confirming=false;}
 }
 function download(){const url=URL.createObjectURL(new Blob([colorMapXML(configuration)+'\n'],{type:'application/xml'}));const a=document.createElement('a');a.href=url;a.download='colorMap.xml';a.click();setTimeout(()=>URL.revokeObjectURL(url),0);}
 async function copy(){try{await navigator.clipboard.writeText(colorMapXML(configuration));copyFailed=false;}catch{copyFailed=true;}}
</script>
<section class="train-color-controls" aria-label={text.trainColors}>
 <h3>{text.trainColors}</h3><p class="hint">{text.trainColorsHelp}</p>
 <label class="field">{text.trainColorTemplate}<select aria-label={text.trainColorTemplate} value={configuration.template.contributionId} onchange={e=>{const id=e.currentTarget.value;change({template:{packId:'exav-stable-bundled',contributionId:id,recipeId:id}});}}>
  {#each templates as t}<option value={t.id}>{t[view.locale]}</option>{/each}
 </select></label>
 <div class="train-color-inputs">{#each fields as field}<label class="field">{text[fieldLabels[field]]}<input type="color" value={configuration[field]} oninput={e=>change({[field]:e.currentTarget.value})}/></label>{/each}</div>
 {#if failed}<p role="alert">{text.catalogPreviewUnavailable}</p>{:else if preview}<div class="train-color-previews"><figure><img src={preview.day} width="192" height="96" alt={text.trainColorDay}/><figcaption>{text.trainColorDay}</figcaption></figure><figure><img src={preview.night} width="192" height="96" alt={text.trainColorNight}/><figcaption>{text.trainColorNight}</figcaption></figure></div>{/if}
 <label class="field">{text.trainColorXML}<textarea readonly value={colorMapXML(configuration)} rows="5"></textarea></label>
 <div class="form-actions"><button onclick={copy}>{text.trainColorCopy}</button><button onclick={download}>{text.trainColorExport}</button></div>
 {#if copyFailed}<p role="status">{text.trainColorCopyFailed}</p>{/if}
 {#if stale}<p role="alert">{text.trainColorStale}</p>{/if}
 <div class="form-actions"><button disabled={disabled||!draft||stale||failed||!preview} onclick={()=>{confirming=true;}}>{text.trainColorApply}</button><button disabled={view.pending||!draft} onclick={()=>{draft=undefined;}}>{text.resetDraft}</button></div>
</section>
{#if confirming}
 <DecisionModal title={text.trainColorApply} oncancel={()=>{if(!view.pending)confirming=false;}}>
  <p>{text.trainColorConfirm}</p>
  {#if stale}<p role="alert">{text.trainColorStale}</p>{/if}
  {#if !['idle','waiting','accepted'].includes(view.feedback)}<p role="alert">{text[view.feedback]}</p>{/if}
  {#snippet actions()}<button class="primary" disabled={disabled||stale||!draft} onclick={apply}>{text.trainColorApply}</button><button disabled={view.pending} onclick={()=>{confirming=false;}}>{text.cancel}</button>{/snippet}
 </DecisionModal>
{/if}
<style>
 .train-color-inputs,.train-color-previews{display:flex;flex-wrap:wrap;gap:.75rem}.train-color-inputs label{flex:1;min-width:5rem}figure{margin:0;max-width:100%}img{image-rendering:pixelated;max-width:100%;height:auto}textarea{width:100%;resize:vertical;font-family:monospace}input[type=color]{min-height:2.5rem;width:100%}
</style>
