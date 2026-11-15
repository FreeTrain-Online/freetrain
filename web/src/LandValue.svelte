<script lang="ts">
 import {messages} from './lib/localization.ts';
 import {landQuery} from './lib/land-edit.ts';
 import type {ShellModel,ShellState} from './lib/shell-model.ts';
 import type {Position,LandPreview} from './generated/contracts.ts';
 let {model,view,position}:{model:ShellModel;view:ShellState;position:Position}=$props();
 const text=$derived(messages[view.locale]);let result=$state<LandPreview>();
 $effect(()=>{
  const read=view.read,p=position,controller=new AbortController();
  const query=read&&landQuery(read,p,p,'inspect');
  if(query&&view.connection==='ready')void model.previewLand(query,controller.signal).then(value=>{if(!controller.signal.aborted)result=value;}).catch(()=>{if(!controller.signal.aborted)result=undefined;});
  return()=>controller.abort();
 });
</script>
{#if result?.legal}<dl class="details" data-testid="land-inspector"><div><dt>{text.landValue}</dt><dd data-testid="land-current-value">¥{BigInt(result.cells[0].value).toLocaleString(view.locale)}</dd></div></dl><p>{text[result.cells[0].status==='owned'?'landOwned':'landUnowned']}</p>{/if}
