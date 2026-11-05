<script lang="ts">
  import FleetManualControls from './FleetManualControls.svelte';
  import FleetDeploymentControls from './FleetDeploymentControls.svelte';
  import {onDestroy,tick} from 'svelte';
  import {messages} from './lib/localization.ts';
  import type {ShellModel,ShellState} from './lib/shell-model.ts';
  import type {FleetTradePreview,FleetTradeQuery} from './generated/contracts.ts';
  import {stocks,stockText,stockAvailable,formationCars,formationPreviewCars,tradeQuery,currentQuote,permittedLengths} from './lib/stock-catalog.ts';
  import CatalogPreview from './CatalogPreview.svelte';
  import FormationPreview from './FormationPreview.svelte';
  import DecisionModal from './DecisionModal.svelte';
  let {model,view,busy,ondraft,requestNavigation,picked,onmap,onpreview}:{model:ShellModel;view:ShellState;busy:boolean;
    picked?:import('./generated/contracts.ts').Position;onmap:(active:boolean)=>void;onpreview:(p:import('./generated/contracts.ts').FleetDeploymentPreview|undefined)=>void;
    ondraft:(dirty:boolean,canApply:boolean,draft:string)=>void;requestNavigation:(action:()=>void)=>void}=$props();
  const text=$derived(messages[view.locale]), read=$derived(view.read!);
  let tab=$state<'purchase'|'owned'>('purchase'),company=$state(''),type=$state(''),search=$state(''),stockId=$state(stocks[0].id);
  let length=$state(1),quantity=$state(1),trainId=$state(''),draft=$state(''),draftRevision=$state(''),dirty=$state(false),composing=$state(false);
  let quote=$state<FleetTradePreview>(),loading=$state(false),failed=$state(false),submitting=$state(false);
  let deploymentControls=$state<FleetDeploymentControls>();
  export function cancelDeployment(){return deploymentControls?.cancelDraft()??false;}
  let controller:AbortController|undefined;
  let quoteLauncher:HTMLElement|undefined;
  const disabled=$derived(busy||view.pending||submitting||view.connection!=='ready'||view.role==='observer');
  const stock=$derived(stocks.find(s=>s.id===stockId)??stocks[0]);
  const available=$derived(stockAvailable(stock,read));
  const companies=$derived([...new Set(stocks.map(s=>s.company))]);
  const types=$derived([...new Set(stocks.filter(s=>!company||s.company===company).map(s=>s.type))]);
  const visible=$derived(stocks.filter(s=>(!company||s.company===company)&&(!type||s.type===type)&&
    [s.id,...(['company','type','name','author','description'] as const).flatMap(f=>[s[f],stockText(s,f,view.locale)])].join(' ').toLocaleLowerCase(view.locale).includes(search.trim().toLocaleLowerCase(view.locale))));
  const trains=$derived(read.mixed?.fleet?.trains??[]);
  const train=$derived(trains.find(t=>t.id===trainId));
  const conflict=$derived(dirty&&(!train||train.revision!==draftRevision));
  const validName=$derived(!!draft.trim()&&draft.trim().length<=256&&!/[\u0000-\u001f\u007f-\u009f]/u.test(draft));
  const stale=$derived(!!quote&&!currentQuote(read,quote.query));
  const cars=$derived(formationPreviewCars(stock,length));
  const ownedStock=$derived(train?stocks.find(s=>s.id===train.definition.contributionId):undefined);
  const fmt=(value:string|number)=>new Intl.NumberFormat(view.locale).format(typeof value==='string'?BigInt(value):value);
  $effect(()=>{if(!dirty){draft=train?.name??'';draftRevision=train?.revision??'';}});
  $effect(()=>ondraft(dirty,!disabled&&!!train&&dirty&&!conflict&&validName&&!composing,draft));
  $effect(()=>{if(trainId&&!trains.some(t=>t.id===trainId)){trainId='';dirty=false;}});
  let epoch:string|undefined;
  $effect(()=>{if(epoch!==read.worldEpoch){epoch=read.worldEpoch;controller?.abort();quote=undefined;loading=false;trainId='';dirty=false;}});
  function cancel(restore=false){controller?.abort();controller=undefined;loading=false;quote=undefined;
    if(restore)void tick().then(()=>{if(quoteLauncher?.isConnected)quoteLauncher.focus({preventScroll:true});});
  }
  onDestroy(()=>{controller?.abort();ondraft(false,false,'');});
  export function discardDraft(){dirty=false;}
  export async function applyDraft(){return rename();}
  function selectStock(id:string){cancel();stockId=id;length=stocks.find(s=>s.id===id)!.lengths[0];failed=false;}
  async function preview(query:FleetTradeQuery){
    if(disabled||loading)return;
    quoteLauncher=document.activeElement instanceof HTMLElement?document.activeElement:undefined;
    cancel();const request=new AbortController();controller=request;loading=true;failed=false;
    try{const result=await model.previewFleetTrade(query,request.signal);if(controller===request)quote=result;}
    catch{if(!request.signal.aborted)failed=true;}
    finally{if(controller===request){loading=false;controller=undefined;}}
  }
  async function confirm(){
    if(!quote||stale||disabled)return;
    const selected=quote;submitting=true;
    try{if(await model.send({kind:'tradeFleet',query:selected.query,expectedPrice:selected.price})){cancel(true);if(selected.query.action==='sell'){trainId='';dirty=false;}}}
    finally{submitting=false;}
  }
  function sale(){if(!train||train.state!=='unplaced')return;
    void preview({...tradeQuery(read,stock,0,0),action:'sell',definition:null,trainId:train.id});
  }
  async function rename(){
    if(disabled||!train||!dirty||conflict||!validName||composing)return;
    const id=train.id,value=draft,revision=draftRevision;
    const ok=await model.send({kind:'renameFleet',trainId:id,expectedRevision:revision,name:value});
    if(ok&&trainId===id&&draft===value){dirty=false;return true;}
    return false;
  }
</script>
<div class="fleet-controls">
  <div class="form-actions fleet-tabs" role="group" aria-label={text.fleet}>
    <button aria-pressed={tab==='purchase'} disabled={loading||submitting} onclick={()=>requestNavigation(()=>{cancel();tab='purchase';})}>{text.stockPurchase}</button>
    <button aria-pressed={tab==='owned'} disabled={loading||submitting} onclick={()=>{if(tab!=='owned')requestNavigation(()=>{cancel();trainId=trains[0]?.id??'';dirty=false;failed=false;tab='owned';});}}>{text.ownedStock} ({fmt(trains.length)})</button>
  </div>
  {#if tab==='purchase'}
    <div class="fleet-layout">
      <div class="fleet-sidebar">
        <div class="fleet-filters">
          <label class="field">{text.stockCompany}<select aria-label={text.stockCompany} bind:value={company} onchange={()=>{type='';}}><option value="">{text.stockAll}</option>{#each companies as c}<option value={c}>{stockText(stocks.find(s=>s.company===c)!,'company',view.locale)}</option>{/each}</select></label>
          <label class="field">{text.stockType}<select aria-label={text.stockType} bind:value={type}><option value="">{text.stockAll}</option>{#each types as t}<option value={t}>{stockText(stocks.find(s=>s.type===t)!,'type',view.locale)}</option>{/each}</select></label>
          <label class="field fleet-search">{text.catalogSearch}<input type="search" bind:value={search}/></label>
        </div>
        <ul class="catalog fleet-catalog" aria-label={text.stockPurchase}>
          {#each visible as item (item.id)}
            <li class="catalog-item" class:catalog-active={stock.id===item.id}>
              <button class="catalog-item-main" aria-pressed={stock.id===item.id} disabled={submitting||loading} onclick={()=>selectStock(item.id)}>
                <CatalogPreview id={formationCars(item,item.lengths[0])[0]} unavailable={text.catalogPreviewUnavailable}/>
                <span><small>{stockText(item,'company',view.locale)} · {stockText(item,'type',view.locale)}</small><strong>{stockText(item,'name',view.locale)}</strong></span>
              </button>
            </li>
          {/each}
        </ul>
        {#if !visible.length}<p role="status">{text.stockNoResults}</p>{/if}
      </div>
      <div class="fleet-details">
        <h3>{stockText(stock,'name',view.locale)}</h3>
        <p>{stockText(stock,'description',view.locale)}</p><p>{text.objectAuthor}: {stockText(stock,'author',view.locale)}</p>
        <dl class="details"><div><dt>{text.stockSpeed}</dt><dd>{fmt(stock.minutes)}</dd></div><div><dt>{text.stockFare}</dt><dd>{fmt(stock.fare)}</dd></div><div><dt>{text.stockAmenity}</dt><dd>{fmt(stock.amenity)}</dd></div></dl>
        <p class="hint">{text.stockFareHelp}</p>
        <p class="hint">{text.stockPermitted}: {permittedLengths(stock.lengths)}</p>
        <form onsubmit={event=>{event.preventDefault();void preview(tradeQuery(read,stock,length,quantity));}}>
          <div class="fleet-filters">
            <label class="field">{text.stockLength}<select aria-label={text.stockLength} bind:value={length} disabled={loading||submitting}>{#each stock.lengths as n}<option value={n}>{fmt(n)}</option>{/each}</select></label>
            <label class="field">{text.stockQuantity}<input type="number" min="1" max="100" step="1" required bind:value={quantity} disabled={loading||submitting}/></label>
          </div>
          <FormationPreview {cars} label={`${text.cars}: ${length}`} unavailable={text.catalogPreviewUnavailable}/>
          <p class="hint">{text.stockLimits}</p>
          {#if !available}<p role="status">{text.stockUnavailable}</p>{/if}
          <button type="submit" class="primary" disabled={disabled||loading||!available||!Number.isInteger(quantity)||quantity<1||quantity>100}>{loading?text.waiting:text.stockQuote}</button>
        </form>
      </div>
    </div>
  {:else}
    <div class="fleet-layout">
      <div class="fleet-sidebar">
        {#if !trains.length}<p role="status">{text.stockEmpty}</p>{/if}
        <ul class="catalog fleet-owned" aria-label={text.ownedStock}>
          {#each trains as item (item.id)}
            <li class="catalog-item" class:catalog-active={item.id===trainId}><button class="catalog-item-main" aria-pressed={item.id===trainId} disabled={submitting||loading} onclick={()=>{if(item.id!==trainId)requestNavigation(()=>{cancel();trainId=item.id;dirty=false;failed=false;});}}>
              <CatalogPreview id={item.cars[0].definition.recipeId} unavailable={text.catalogPreviewUnavailable}/>
              <span><strong>{item.name}</strong><small>#{item.id} · {fmt(item.cars.length)} {text.stockCarsUnit} · {item.state==='unplaced'?text.stockUnplaced:text.stockPlaced}</small></span>
            </button></li>
          {/each}
        </ul>
      </div>
      <div class="fleet-details">
        {#if train}
          <form class="fleet-rename" onsubmit={event=>{event.preventDefault();void rename();}}>
            <label class="field">{text.stockName}<input maxlength="256" required value={draft} oncompositionstart={()=>{composing=true;}} oncompositionend={()=>{composing=false;}} oninput={event=>{if(!dirty)draftRevision=train!.revision;dirty=true;draft=event.currentTarget.value;}} disabled={disabled}/></label>
            {#if conflict}<p role="status">{text.stockRenameConflict}</p>{:else if dirty&&!validName}<p role="status">{text.stockRenameInvalid}</p>{/if}
            <div class="form-actions"><button class:primary={dirty} type="submit" disabled={disabled||!dirty||conflict||!validName||composing}>{text.stockRename}</button><button type="button" disabled={submitting} onclick={()=>{dirty=false;composing=false;}}>{text.resetDraft}</button></div>
          </form>
          {#if train.state!=='unplaced'}{#key `${read.worldEpoch}/${train.id}`}<FleetManualControls {model} {view} trainId={train.id} disabled={disabled||dirty||loading||!!quote}/>{/key}{/if}
          <p>{text.stockModel}: {ownedStock?stockText(ownedStock,'name',view.locale):text.stockUnavailable}</p>
          {#key `${read.worldEpoch}/${train.id}`}<FleetDeploymentControls bind:this={deploymentControls} {model} {view} trainId={train.id} disabled={disabled||dirty||loading} {picked} {onmap} {onpreview}/>{/key}
          <button disabled={disabled||loading||dirty||train.state!=='unplaced'} onclick={sale}>{text.stockSaleQuote}</button>
          {#if train.state!=='unplaced'}<p>{text.stockWithdrawFirst}</p>{/if}
        {/if}
      </div>
    </div>
  {/if}
  {#if failed}<p role="alert">{text.stockQuoteFailed}</p>{/if}
</div>
{#if quote}
  <DecisionModal leftAligned title={text[quote.query.action==='buy'?'stockBuying':'stockSelling']} oncancel={()=>{if(!submitting)cancel(true);}}>
    <p>{text.stockQuoteHelp}</p>
    {#if quote.query.action==='buy'}<p>{stockText(stock,'name',view.locale)} · {quote.query.length} × {quote.query.quantity}</p>{:else}<p>{train?.name}</p>{/if}
    <dl class="details"><div><dt>{text.stockTotal}</dt><dd data-testid="stock-total">{fmt(quote.price)}</dd></div>
      {#if quote.query.action==='buy'}<div><dt>{text.stockNormal}</dt><dd>{fmt(quote.normalCapacity)}</dd></div><div><dt>{text.stockSeated}</dt><dd>{fmt(quote.seatedCapacity)}</dd></div><div><dt>{text.stockPacked}</dt><dd>{fmt(quote.packedCapacity)}</dd></div>{/if}
    </dl>
    {#if quote.query.action==='buy'}<p class="hint">{text.stockCapacityScope}</p>{/if}
    {#if stale}<p role="alert">{text.stockStale}</p>{/if}
    {#if !['idle','waiting','accepted'].includes(view.feedback)}<p role="alert">{text[view.feedback]}</p>{/if}
    {#snippet actions()}<button class="primary" disabled={disabled||stale} onclick={confirm}>{text[quote!.query.action==='buy'?'stockConfirmBuy':'stockConfirmSell']}</button><button disabled={submitting||view.pending} onclick={()=>cancel(true)}>{text.cancel}</button>{/snippet}
  </DecisionModal>
{/if}
