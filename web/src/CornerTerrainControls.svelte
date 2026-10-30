<script lang="ts">
  import { untrack } from 'svelte';
  import type { CornerTerrainQuery, CornerTerrainPreview, CornerTerrainSite, Position } from './generated/contracts.ts';
  import type { ShellModel, ShellState } from './lib/shell-model.ts';
  import { messages } from './lib/localization.ts';
  let {model,view,picked,onpreview,onreset}: {model:ShellModel;view:ShellState;picked?:{position:Position;offsetX:number;offsetY:number;reverse:boolean;append?:boolean};onpreview:(p:Position|undefined)=>void;onreset:()=>void}=$props();
  const text=$derived(messages[view.locale]);
  let x=$state(0),y=$state(0),z=$state(1),action=$state<'raise'|'lower'>('raise');
  let result=$state<CornerTerrainPreview>(),query:CornerTerrainQuery|undefined;
  let submitting=$state(false);
  let status=$state<'idle'|'checking'|'ready'|'failed'|'stale'>('idle'),uncertain=$state(false);
  let sites:CornerTerrainSite[]=[];
  let count=$state(0);
  let version=0,request:AbortController|undefined;
  const blocked=$derived(view.pending||view.connection!=='ready'||view.role==='observer'||uncertain);
  const reasons={invalidInput:'cornerInvalid',outside:'outsideMap',unmatchedCorners:'cornerUnmatched',groundLevel:'cornerGround',cornerLimit:'cornerLimit',floor:'cornerFloor',occupied:'cornerOccupied',roof:'cornerRoof',capacityLimit:'cornerCapacity'} as const;
  export function cancelDraft(){if(count||status!=='idle'){clear();onreset();return true;}return false;}
  function clear(){sites=[];count=0;reset();}
  function reset(){version++;request?.abort();result=undefined;query=undefined;status='idle';uncertain=false;onpreview(undefined);}
  async function check(p?:typeof picked,append=false) {
    if(blocked)return;reset();const id=version,read=view.read;if(!read?.terrain)return;
    if(p){x=p.position.x;y=p.position.y;z=p.position.z;}
    if(![x,y,z].every(Number.isSafeInteger)){status='failed';return;}
    const target:CornerTerrainQuery={expectedWorldEpoch:read.worldEpoch,expectedGeometryRevision:read.terrain.geometryRevision,
      position:{x,y,z},stroke:null,action:p?.reverse?(action==='raise'?'lower':'raise'):action,offsetX:p?.offsetX??null,offsetY:p?.offsetY??null};
    const site:CornerTerrainSite={position:target.position,action:target.action,offsetX:target.offsetX,offsetY:target.offsetY};
    if(!p?.append&&!append)sites=[];
    if(sites.length<64)sites.push(site);
    count=sites.length;
    const first=sites[0];target.position=first.position;target.action=first.action;target.offsetX=first.offsetX;target.offsetY=first.offsetY;target.stroke=sites.slice(1);
    status='checking';request=new AbortController();
    try{
      const preview=await model.previewCorner(target,request.signal);
      if(id!==version||read.worldEpoch!==view.read?.worldEpoch||target.expectedGeometryRevision!==view.read?.terrain?.geometryRevision)return;
      query=target;result=preview;status='ready';onpreview(preview.position);
    }catch{if(id===version)status='failed';}
  }
  async function commit(){
    if(blocked||status!=='ready'||!result?.legal||!query)return;
    const id=version,target=query;submitting=true;
    const accepted=await model.send({kind:'editCorner',query:target});
    submitting=false;if(id!==version)return;
    if(accepted){clear();onreset();}
    else if(view.feedback==='unresolved')uncertain=true;
    else {status='stale';result=undefined;onpreview(undefined);}
  }
  $effect(()=>{const p=picked;if(p)untrack(()=>void check(p));});
  $effect(()=>{
    if(result&&!submitting&&(view.read?.worldEpoch!==result.worldEpoch||view.read?.terrain?.geometryRevision!==result.geometryRevision)){
      version++;request?.abort();sites=[];count=0;result=undefined;status='stale';onpreview(undefined);
    }
    if(view.connection!=='ready'||view.role==='observer'){clear();}
  });
  $effect(()=>()=>{version++;request?.abort();onpreview(undefined);});
</script>
<section class="tool-card" aria-label={text.terrainTool}>
  <h2>{text.terrainTool}</h2>
  <p class="hint">{text.cornerHelp}</p>
  <form onsubmit={event=>{event.preventDefault();void check();}}>
    <div class="form-actions">
      <label>{text.cornerAction}<select aria-label={text.cornerAction} bind:value={action} disabled={view.pending} onchange={clear}><option value="raise">{text.cornerRaise}</option><option value="lower">{text.cornerLower}</option></select></label>
      <label>X<input aria-label={text.cornerX} type="number" step="1" bind:value={x} oninput={reset} disabled={view.pending}/></label>
      <label>Y<input aria-label={text.cornerY} type="number" step="1" bind:value={y} oninput={reset} disabled={view.pending}/></label>
      <label>Z<input aria-label={text.cornerZ} type="number" min="0" max={(view.read?.map.levels??1)-1} step="1" bind:value={z} oninput={reset} disabled={view.pending}/></label>
    </div>
    <button disabled={blocked||status==='checking'}>{text.cornerCheck}</button>
    <button type="button" disabled={blocked||count>=64} onclick={()=>check(undefined,true)}>{text.cornerAdd}</button>
  </form>
  <p>{text.cornerSites}: {count}/64</p>
  <p role="status" data-testid="corner-status">{status==='checking'?text.checkingGeometry:status==='failed'?text.geometryFailed:status==='stale'?text.cornerStale:result?result.legal?text.cornerLegal:text[reasons[result.reason??'invalidInput']]:text.pickTile}</p>
  {#if result}
    {#if result.skipped.length}<p>{text.cornerSkipped}: {result.skipped.length}</p><ul>{#each result.skipped as skipped}<li>{skipped.position.x}, {skipped.position.y}, {skipped.position.z}: {text[reasons[skipped.reason]]}</li>{/each}</ul>{/if}
    <p data-testid="corner-position">{result.position.x}, {result.position.y}, {result.position.z} · {result.action==='raise'?text.cornerRaise:text.cornerLower}</p>
    <p>{text.cornerCost}: {result.cost}</p>
    <ul class="corner-changes">{#each result.changes as c}<li>{c.position.x}, {c.position.y}: {c.beforeGround} [{c.beforeNE},{c.beforeSE},{c.beforeSW},{c.beforeNW}] → {c.afterGround} [{c.afterNE},{c.afterSE},{c.afterSW},{c.afterNW}]{#if c.losesForest} · {text.cornerForestLoss}{/if}{#if c.losesOwnership} · {text.cornerOwnershipLoss}{/if}</li>{/each}</ul>
  {/if}
  {#if !['idle','waiting','accepted'].includes(view.feedback)}<p>{text[view.feedback]}</p>{/if}
  <div class="form-actions"><button class="primary" disabled={blocked||status!=='ready'||!result?.legal} onclick={commit}>{text.confirmTerrain}</button><button disabled={view.pending} onclick={()=>{clear();onreset();}}>{text.cancelPreview}</button></div>
</section>
