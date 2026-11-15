<script lang="ts">
 import {messages} from './lib/localization.ts';
 import {cityQuery} from './lib/city-edit.ts';
 import type {ShellModel,ShellState} from './lib/shell-model.ts';
 import type {CityPreview} from './generated/contracts.ts';
 let {model,view,id}:{model:ShellModel;view:ShellState;id:string}=$props();
 const text=$derived(messages[view.locale]);
 let result=$state<CityPreview>();
 $effect(()=>{
  const read=view.read,controller=new AbortController();result=undefined;
  const query=read&&cityQuery(read,id);
  if(query&&view.connection==='ready')void model.cityInformation(query,controller.signal).then(value=>{
   if(!controller.signal.aborted)result=value;
  }).catch(()=>{});
  return ()=>controller.abort();
 });
 const fmt=(n:number)=>new Intl.NumberFormat(view.locale).format(n);
</script>
{#if result?.population}<dl class="details" data-testid="city-population">
 <div><dt>{text.cityResidents}</dt><dd data-testid="city-residents">{fmt(result.population.residents)}</dd></div>
 <div><dt>{text.cityOutgoing}</dt><dd data-testid="city-outgoing">{fmt(result.population.outgoing)}</dd></div>
 <div><dt>{text.cityIncoming}</dt><dd data-testid="city-incoming">{fmt(result.population.incoming)}</dd></div>
 <div><dt>{text.cityLandValue}</dt><dd data-testid="city-land-value">{fmt(result.population.landValue)}</dd></div>
</dl><p class="hint">{text.citySupplyHelp}</p>{/if}
