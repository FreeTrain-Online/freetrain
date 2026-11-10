<script lang="ts">
 import {messages} from './lib/localization.ts';
 import {formatGameTime} from './lib/game-time.ts';
 import type {ShellModel,ShellState} from './lib/shell-model.ts';
 import type {StationInformation} from './generated/contracts.ts';
 import {resolvedPlatformStation} from './lib/platform-properties.ts';
 import {stationInformationQuery} from './lib/station-information.ts';
 let {model,view,stationId,platformId,onmove,movable=true}:{model:ShellModel;view:ShellState;stationId?:string;platformId?:string;movable?:boolean;onmove:(id:string)=>void}=$props();
 const text=$derived(messages[view.locale]),read=$derived(view.read),graph=$derived(read?.mixed?.stationGraph);
 const station=$derived(graph?.buildings.find(b=>b.id===stationId));
 let chosenPlatform=$state(''),trainId=$state(''),retry=$state(0);
 const platforms=$derived(platformId?graph?.platforms.filter(p=>p.id===platformId)??[]:graph?.platforms.filter(p=>resolvedPlatformStation(read,p.id)?.id===stationId)??[]);
 const platform=$derived(platforms.find(p=>p.id===chosenPlatform)??platforms[0]);
 const currentTrainId=$derived(read?.mixed?.fleet?.trains.some(t=>t.id===trainId)?trainId:'0');
 // Refresh a serialized observation independently of the moving read revision.
 // The timestamp makes an in-flight network observation explicit during Run.
 const identity=$derived(read&&(station||platform)?JSON.stringify({epoch:read.worldEpoch,stationId:station?.id??'0',platformId:platform?.id??'0',trainId:currentTrainId}):undefined);
 const connected=$derived(view.connection==='ready');
 let result=$state.raw<StationInformation>(),failed=$state(false);
 const info=$derived(result&&result.query.expectedWorldEpoch===read?.worldEpoch&&result.query.stationId===(station?.id??'0')&&result.query.platformId===(platform?.id??'0')&&result.query.trainId===currentTrainId?result:undefined);
 $effect(()=>{
  const key=identity,ready=connected;void retry;result=undefined;failed=false;
  if(!key||!ready)return;
  const target=JSON.parse(key) as {epoch:string;stationId:string;platformId:string;trainId:string};
  const controller=new AbortController();let timer:ReturnType<typeof setTimeout>|undefined;
  async function refresh(){
   const current=model.state.read;
   if(!current||current.worldEpoch!==target.epoch||controller.signal.aborted)return;
   const q={...stationInformationQuery(current,target.stationId,target.platformId,target.trainId),latest:true};
   try{const value=await model.stationInformation(q,controller.signal);if(!controller.signal.aborted){result=value;failed=false;}}
   catch{if(!controller.signal.aborted)failed=true;}
   if(!controller.signal.aborted)timer=setTimeout(refresh,250);
  }
  void refresh();
  return ()=>{controller.abort();clearTimeout(timer);};
 });
 const fmt=(n:number|null|undefined)=>n==null?'—':new Intl.NumberFormat(view.locale).format(n);
 const reasons={disconnected:'stationDiagnosisDisconnected',noPlatform:'stationDiagnosisNoPlatform',noTrain:'stationDiagnosisNoTrain',unplaced:'stationDiagnosisUnplaced',direction:'stationDiagnosisDirection',length:'stationDiagnosisLength',away:'stationDiagnosisAway',position:'stationDiagnosisPosition',pass:'stationDiagnosisPass',forwarding:'stationDiagnosisForwarding',wait:'stationDiagnosisWait',ready:'stationDiagnosisReady'} as const;
</script>
<section class="station-information" aria-label={text.stationInformation}>
 <h3>{text.stationInformation}</h3>
 {#if info}<p class="hint" data-testid="station-observed-minute">{text.stationObservedAt}: {formatGameTime(info.observedMinute,view.locale,true)}</p>{/if}
 {#if station}
  <h4 data-testid="station-information-name">{station.name} · #{station.id}</h4>
  <dl class="details">
   <div><dt>{text.stationWaiting}</dt><dd data-testid="station-waiting">{fmt(info?.waiting)}</dd></div>
   <div><dt>{text.stationDemand}</dt><dd data-testid="station-demand">{fmt(info?.demand)}</dd></div>
   <div><dt>{text.stationAverageBoarded}</dt><dd data-testid="station-average-boarded">{fmt(info?.averageBoarded)}</dd></div>
   <div><dt>{text.stationAverageAlighted}</dt><dd data-testid="station-average-alighted">{fmt(info?.averageAlighted)}</dd></div>
  </dl>
  <table class="station-logs" aria-label={text.stationLogs}>
   <thead><tr><th scope="col">{text.stationLogs}</th><th scope="col">{text.stationToday}</th><th scope="col">{text.stationYesterday}</th></tr></thead>
   <tbody>{#each [{label:text.stationBoarded,log:station.exports,id:'boarded'},{label:text.stationAlighted,log:station.imports,id:'alighted'},{label:text.stationCalls,log:station.trains,id:'calls'}] as {label,log,id}}
    <tr><th scope="row">{label}</th><td data-testid={`station-${id}-today`}>{fmt(log.today)}</td><td data-testid={`station-${id}-yesterday`}>{fmt(log.yesterday)}</td></tr>
   {/each}</tbody>
  </table>
  {#if movable}<button onclick={()=>onmove(station.id)}>{text.stationMove}</button>{/if}
  <p class="hint">{text.stationSupplyHelp}</p>
 {/if}
 <h4>{text.stationDiagnosis}</h4>
 {#if !platformId}<label class="field">{text.stationDiagnosisPlatform}<select aria-label={text.stationDiagnosisPlatform} value={platform?.id??''} onchange={e=>{chosenPlatform=e.currentTarget.value;}}>
  {#if !platforms.length}<option value="">{text.stationDiagnosisNoPlatform}</option>{/if}
  {#each platforms as p}<option value={p.id}>{p.name} · #{p.id}</option>{/each}
 </select></label>{/if}
 <label class="field">{text.stationDiagnosisTrain}<select aria-label={text.stationDiagnosisTrain} value={currentTrainId} onchange={e=>{trainId=e.currentTarget.value;}}>
  <option value="0">{text.stationDiagnosisNoTrain}</option>
  {#each read?.mixed?.fleet?.trains??[] as t}<option value={t.id}>{t.name} · #{t.id}</option>{/each}
 </select></label>
 {#if platform}<p>{text.stationPlatformLength}: {platform.length}</p>{#if currentTrainId!=='0'}<p>{text.stockLength}: {read?.mixed?.fleet?.trains.find(t=>t.id===currentTrainId)?.cars.length}</p>{/if}{/if}
 {#if info}
  <p data-testid="station-diagnosis">{text[reasons[info.reason]]}</p>
  {#if info.demand===0}<p data-testid="station-zero-supply">{text.stationDiagnosisZeroSupply}</p>{/if}
  {#if info.stopIndex!==null}<p data-testid="station-stop-index">{text.stationStopIndex}: {info.stopIndex} / {info.yardIndex??'—'}</p>{/if}
  {#if info.controllerId}<p>{text.effectiveController}: {read?.mixed?.fleet?.controllers.find(c=>c.id===info.controllerId)?.name} · #{info.controllerId}</p>{/if}
  {#if info.waitMinutes>0}<p>{text.controllerMinutes}: {fmt(info.waitMinutes)}</p>{/if}
 {:else}<p role="status">{failed?text.stationInformationUnavailable:text.stationInformationLoading}</p>{#if failed}<button onclick={()=>retry++}>{text.retryNow}</button>{/if}{/if}
 <p class="hint">{text.stationDiagnosisHelp}</p>
</section>
<style>
 .station-logs{width:100%;border-collapse:collapse;margin:1em 0;font-variant-numeric:tabular-nums;}
 th,td{padding:.4em .25em;text-align:end;}th:first-child{text-align:start;}
 h4{overflow-wrap:anywhere;}select{max-width:100%;}
</style>
