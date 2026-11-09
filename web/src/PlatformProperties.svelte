<script lang="ts">
 import {onDestroy,untrack} from 'svelte';
 import BellControls from './BellControls.svelte';
 import {messages} from './lib/localization.ts';
 import {currentPlatformProperties,platformPropertiesQuery,resolvedPlatformStation,validPlatformName} from './lib/platform-properties.ts';
 import profile from './generated/station-profile.json' with {type:'json'};
 import type {PlatformProperties} from './generated/contracts.ts';
 import type {ShellModel,ShellState} from './lib/shell-model.ts';
 let {model,view,id,disabled,ondraft,onremove}:{model:ShellModel;view:ShellState;id:string;disabled:boolean;ondraft:(dirty:boolean,canApply:boolean,draft:string)=>void;onremove:()=>void}=$props();
 const text=$derived(messages[view.locale]);
 const platform=$derived(view.read?.mixed?.stationGraph?.platforms.find(p=>p.id===id));
 const station=$derived(resolvedPlatformStation(view.read,id));
 let properties=$state.raw<PlatformProperties>(),name=$state(''),host=$state(''),bell=$state(''),dirty=$state(false),composing=$state(false),failed=$state(false),loading=$state(false);
 let request:AbortController|undefined,version=0;
 const stale=$derived(!!properties&&!currentPlatformProperties(view.read,properties));
 const canApply=$derived(!disabled&&!loading&&!composing&&dirty&&!!properties&&!stale&&validPlatformName(name)&&profile.bells.some(b=>b.contributionId===bell)&&(!host||!!properties.candidates.find(c=>c.id===host)));
 const proposedStation=$derived(host?properties?.candidates.find(c=>c.id===host)?.stationId:undefined);
 const memberName=(id:string)=>view.read?.mixed?.stationGraph?.buildings.find(b=>b.id===id)?.name??view.read?.mixed?.stationGraph?.platforms.find(p=>p.id===id)?.name??id;
 export async function resetDraft(){
  const read=view.read,p=platform;if(!read||!p)return;
  request?.abort();request=new AbortController();const current=++version;properties=undefined;dirty=false;failed=false;loading=true;
  name=p.name;host=p.hostId??'';bell=p.bell.contributionId;
  try{const result=await model.platformProperties(platformPropertiesQuery(read,p),request.signal);if(current===version)properties=result;}
  catch{if(current===version)failed=true;}finally{if(current===version)loading=false;}
 }
 export function discardDraft(){void resetDraft();}
 export async function applyDraft(){
  if(!canApply||!properties)return false;
  const current=version,c=properties.candidates.find(c=>c.id===host),definition=profile.bells.find(b=>b.contributionId===bell)!;
  const accepted=await model.send({kind:'editPlatformProperties',query:properties.query,name,bell:definition,hostId:host||null,expectedHostRevision:c?.revision??'0',expectedStationRevision:c?.stationRevision??'0'});
  if(accepted&&current===version){await resetDraft();return true;}return false;
 }
 $effect(()=>{void id;untrack(()=>void resetDraft());});
 $effect(()=>{if(!dirty&&!loading&&stale&&platform)untrack(()=>void resetDraft());});
 $effect(()=>ondraft(dirty,canApply,name));
 onDestroy(()=>{version++;request?.abort();ondraft(false,false,'');});
</script>
<section aria-label={text.platformProperties} class="platform-properties">
 <p data-testid="railway-component">{text.platformTab}</p>
 <p data-testid="platform-identity">{platform?.name??text.unavailableObject} · {id} · {platform?.revision??''}</p>
 <dl class="details"><div><dt>{text.confirmedName}</dt><dd data-testid="confirmed-name">{platform?.name??text.unavailableObject}</dd></div><div><dt>{text.platformResolvedStation}</dt><dd data-testid="platform-resolved-station">{station?`${station.name} · ${station.id}`:text.platformNoHost}</dd></div></dl>
 {#if !platform?.hostId}<p role="status" data-testid="platform-disconnected">{text.platformDisconnected}</p>{/if}
 <form id="platform-properties-form" onsubmit={e=>{e.preventDefault();void applyDraft();}}>
 <fieldset disabled={disabled||loading||!platform}>
 <label class="field">{text.platformName}<input aria-label={text.platformName} maxlength="128" value={name} oninput={e=>{dirty=true;name=e.currentTarget.value;}} oncompositionstart={()=>{composing=true;}} oncompositionend={()=>{composing=false;}} onblur={()=>{composing=false;}} onkeydown={e=>{if(e.key==='Enter'&&(e.isComposing||e.keyCode===229||composing))e.preventDefault();}}/></label>
 <label class="field">{text.platformHost}<select aria-label={text.platformHost} value={host} onchange={e=>{dirty=true;host=e.currentTarget.value;}} disabled={!properties||stale}>
 <option value="">{text.platformNoHost}</option>
 {#if host&&!properties?.candidates.some(c=>c.id===host)}<option value={host}>{memberName(host)} · {host}</option>{/if}
 {#each properties?.candidates??[] as c(c.id)}<option value={c.id}>{memberName(c.id)} · {c.id} → {memberName(c.stationId)} · {c.stationId}</option>{/each}
 </select></label>
 <p data-testid="platform-proposed-station">{text.platformResolvedStation}: {proposedStation?memberName(proposedStation):text.platformNoHost}</p>
 <BellControls locale={view.locale} selected={bell} disabled={disabled||stale} onchange={value=>{dirty=true;bell=value;}}/>
 </fieldset>
 <p class="hint">{text.platformPropertiesHelp}</p>
 {#if loading}<p role="status">{text.checkingGeometry}</p>{/if}
 {#if failed}<p role="status">{text.geometryFailed}</p>{/if}
 {#if stale}<p role="alert">{text.draftConflict}</p>{/if}
 </form>
 {#if platform?.kind==='wide'}<fieldset><legend>{text.platformLaneControls}</legend><button disabled>{text.platformLaneLeft}</button><button disabled>{text.platformLaneRight}</button><p class="hint">{text.platformLanePending}</p></fieldset>{/if}
 <button disabled={disabled||!platform} onclick={onremove}>{text.platformRemove}</button>
</section>
