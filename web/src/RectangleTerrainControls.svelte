<script lang="ts">
  import { railwayRevision } from './lib/railway-edit.ts';
  import { untrack } from 'svelte';
  import type { CornerTerrainQuery, CornerTerrainPreview, Position } from './generated/contracts.ts';
  import type { ShellModel, ShellState } from './lib/shell-model.ts';
  import { messages } from './lib/localization.ts';
  import { terrainReasons } from './lib/terrain-edit.ts';
  let {model,view,picked,onpreview,onreset,authoring=false,terrace=false}: {model:ShellModel;view:ShellState;authoring?:boolean;terrace?:boolean;picked?:{position:Position;reverse:boolean;soft?:boolean};onpreview:(p:Position|undefined,end?:Position,sites?:{position:Position;skipped:boolean}[])=>void;onreset:()=>void}=$props();
  const text=$derived(messages[view.locale]);
  let x=$state(0),y=$state(0),z=$state(1),endX=$state(1),endY=$state(1);
  let action=$state<'raise'|'lower'|'repair'|'plantForest'|'placeCreek'|'placeTerrace'>(untrack(()=>terrace?'placeTerrace':authoring?'plantForest':'raise')),soft=$state(false),anchor=$state<Position>();
  let result=$state<CornerTerrainPreview>(),query:CornerTerrainQuery|undefined;
  let submitting=$state(false),uncertain=$state(false);
  let status=$state<'idle'|'checking'|'ready'|'failed'|'stale'>('idle');
  let anchorFence:string|undefined;
  let requestFence=$state<string>();
  let version=0,request:AbortController|undefined;
  type ForestChoice={binding:{packId:string;contributionId:string};species:string;variant:string};
  let forestChoices=$state<ForestChoice[]>([]),species=$state('cedar'),variant=$state('natural');
  const speciesLabels={cedar:'treeCedar',pine:'treePine',camphor:'treeCamphor',maple:'treeMaple',ginkgo:'treeGinkgo',beech:'treeBeech'} as const;
  const selectedForest=$derived(forestChoices.find(c=>c.species===species&&c.variant===variant));
  $effect(()=>{if(!authoring)return;const controller=new AbortController();void fetch('/api/worlds/generated/forests',{signal:controller.signal}).then(r=>r.ok?r.json():[]).then(rows=>{if(!controller.signal.aborted)forestChoices=rows;}).catch(()=>{});return()=>controller.abort();});
  const blocked=$derived(view.pending||view.connection!=='ready'||view.role==='observer'||uncertain||authoring&&!view.read?.terrain?.constructionMode);
  export function cancelDraft(){if(anchor||status!=='idle'){clear();onreset();return true;}return false;}
  function reset(){requestFence=undefined;version++;request?.abort();query=undefined;result=undefined;status='idle';uncertain=false;onpreview(undefined);}
  function clear(){anchor=undefined;anchorFence=undefined;reset();}
  async function check(mode=action,profile=soft){
    if(blocked||authoring&&action==='plantForest'&&!selectedForest)return;reset();const id=version,read=view.read;if(!read?.terrain)return;
    if(![x,y,z,endX,endY].every(Number.isSafeInteger)){status='failed';return;}
    const target:CornerTerrainQuery={expectedWorldEpoch:read.worldEpoch,expectedGeometryRevision:railwayRevision(read)!,
      position:{x,y,z},action:mode,offsetX:null,offsetY:null,stroke:null,rectangle:{end:{x:endX,y:endY,z},soft:authoring||terrace?false:profile},...(authoring&&mode==='plantForest'&&selectedForest?{forest:{packId:selectedForest.binding.packId,contributionId:selectedForest.binding.contributionId,recipeId:selectedForest.binding.contributionId}}:{})};
    requestFence=`${read.worldEpoch}/${railwayRevision(read)!}`;status='checking';request=new AbortController();
    try{
      const preview=await model.previewCorner(target,request.signal);
      if(id!==version||read.worldEpoch!==view.read?.worldEpoch||target.expectedGeometryRevision!==railwayRevision(view.read))return;
      query=target;result=preview;status='ready';onpreview(target.position,authoring||terrace?undefined:target.rectangle!.end,authoring||terrace?[...preview.changes.map(c=>({position:c.position,skipped:false})),...preview.skipped.map(c=>({position:c.position,skipped:true}))]:undefined);
    }catch{if(id===version)status='failed';}
  }
  function pick(p:NonNullable<typeof picked>){
    if(blocked)return;
    if(!anchor){reset();x=p.position.x;y=p.position.y;z=p.position.z;anchor={x,y,z};anchorFence=`${view.read?.worldEpoch}/${railwayRevision(view.read)}`;onpreview(anchor);}
    else {endX=p.position.x;endY=p.position.y;anchor=undefined;void check(!authoring&&!terrace&&p.reverse?'repair':action,!authoring&&!terrace&&(p.soft||soft));}
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
  $effect(()=>{const p=picked;if(p)untrack(()=>pick(p));});
  $effect(()=>{
    if(requestFence&&!submitting&&requestFence!==`${view.read?.worldEpoch}/${railwayRevision(view.read)}`){clear();status='stale';}
    if(anchor&&anchorFence!==`${view.read?.worldEpoch}/${railwayRevision(view.read)}`){clear();status='stale';}
    if(result&&!submitting&&(view.read?.worldEpoch!==result.worldEpoch||railwayRevision(view.read)!==result.geometryRevision)){
      reset();anchor=undefined;status='stale';
    }
    if(view.connection!=='ready'||view.role==='observer'||authoring&&!view.read?.terrain?.constructionMode)clear();
  });
  $effect(()=>()=>{version++;request?.abort();onpreview(undefined);});
</script>
<section aria-label={terrace?text.terraceBatch:authoring?text.authoringDecorations:text.rectangleTool}>
  <h2>{terrace?text.terraceBatch:authoring?text.authoringDecorations:text.rectangleTool}</h2><p class="hint">{terrace?text.terraceBatchHelp:authoring?text.authoringDecorationsHelp:text.rectangleHelp}</p>
  <form onsubmit={event=>{event.preventDefault();anchor=undefined;void check();}}>
    {#if terrace}<p>{text.placeTerrace}</p>{:else}<label>{text.cornerAction}<select aria-label={text.cornerAction} bind:value={action} disabled={blocked} onchange={clear}>{#if authoring}<option value="plantForest">{text.plantForest}</option><option value="placeCreek">{text.placeCreek}</option>{:else}<option value="raise">{text.rectangleRaise}</option><option value="lower">{text.rectangleLower}</option><option value="repair">{text.rectangleRepair}</option>{/if}</select></label>{/if}
    {#if authoring&&action==='plantForest'}
      <label>{text.authoringSpecies}<select aria-label={text.authoringSpecies} bind:value={species} disabled={blocked} onchange={reset}>{#each Object.entries(speciesLabels) as [id,label]}<option value={id}>{text[label]}</option>{/each}</select></label>
      <label>{text.forestGround}<select aria-label={text.forestGround} bind:value={variant} disabled={blocked} onchange={reset}><option value="natural">{text.forestNatural}</option><option value="mountain">{text.forestMountain}</option><option value="grass">{text.forestGrass}</option></select></label>
      {#if !selectedForest}<p role="status">{text.forestUnavailable}</p>{/if}
    {/if}
    <div class="form-actions">
      <label>X<input aria-label={text.cornerX} type="number" step="1" bind:value={x} oninput={clear} disabled={blocked}/></label>
      <label>Y<input aria-label={text.cornerY} type="number" step="1" bind:value={y} oninput={clear} disabled={blocked}/></label>
      <label>Z<input aria-label={text.cornerZ} type="number" min="0" max={(view.read?.map.levels??1)-1} step="1" bind:value={z} oninput={clear} disabled={blocked}/></label>
      <label>{text.rectangleEndX}<input aria-label={text.rectangleEndX} type="number" step="1" bind:value={endX} oninput={clear} disabled={blocked}/></label>
      <label>{text.rectangleEndY}<input aria-label={text.rectangleEndY} type="number" step="1" bind:value={endY} oninput={clear} disabled={blocked}/></label>
    </div>
    {#if !authoring&&!terrace}<label><input type="checkbox" bind:checked={soft} disabled={blocked||action!=='raise'} onchange={reset}/>{text.rectangleSoft}</label>{/if}
    <button disabled={blocked||status==='checking'||authoring&&action==='plantForest'&&!selectedForest}>{terrace?text.terraceCheck:authoring?text.previewDecorations:text.rectangleCheck}</button>
  </form>
  <p role="status" data-testid="rectangle-status">{status==='checking'?text.checkingGeometry:status==='failed'?(terrace?text.terraceBatchInvalid:authoring?text.authoringInvalid:text.rectangleInvalid):status==='stale'?text.cornerStale:result?result.legal?(terrace?text.terraceBatchReady:authoring?text.authoringLegal:text.cornerLegal):text[terrainReasons[result.reason??'invalidInput']]:anchor?text.rectanglePickEnd:text.pickTile}</p>
  {#if result}
    <p data-testid="rectangle-position">{x}, {y}, {z} → {endX}, {endY}, {z} · {result.action==='placeTerrace'?text.placeTerrace:result.action==='plantForest'?text.plantForest:result.action==='placeCreek'?text.placeCreek:result.action==='repair'?text.rectangleRepair:result.action==='raise'?text.rectangleRaise:text.rectangleLower}</p>
    <p>{text.cornerCost}: {result.cost}</p>
    {#if terrace}<p data-testid="terrace-batch-count">{text.terraceBatchCount}: {result.changes.length}</p>{/if}
    {#if result.skipped.length}<p>{text.cornerSkipped}: {result.skipped.length}</p><ul>{#each result.skipped as skipped}<li>{skipped.position.x}, {skipped.position.y}, {skipped.position.z}: {text[terrainReasons[skipped.reason]]}</li>{/each}</ul>{/if}
    {#if result.decorations}<p data-testid="authoring-result">{text.authoringCells}: {result.decorations.length} · {text.authoringTrees}: {result.decorations.reduce((n,d)=>n+(d.forest?.trees.length??0),0)}</p>
    <ul>{#each result.decorations as d}<li>{d.position.x}, {d.position.y}, {d.position.z}: {#if d.forest}{text.authoringTrees}: {d.forest.trees.length}{:else}{text.creekConnections}: {[[8,text.headingNorth],[4,text.headingEast],[2,text.headingSouth],[1,text.headingWest]].filter(([bit])=>(d.creekConnections&Number(bit))!==0).map(([,label])=>label).join(' · ')||text.creekIsolated}{/if}</li>{/each}</ul>{/if}
    <ul class="corner-changes">{#each result.changes as c}<li>{c.position.x}, {c.position.y}: {c.beforeGround} [{c.beforeNE},{c.beforeSE},{c.beforeSW},{c.beforeNW}] → {c.afterGround} [{c.afterNE},{c.afterSE},{c.afterSW},{c.afterNW}]{#if c.losesForest} · {text.cornerForestLoss}{/if}{#if c.losesOwnership} · {text.cornerOwnershipLoss}{/if}</li>{/each}</ul>
  {/if}
  {#if !['idle','waiting','accepted'].includes(view.feedback)}<p>{text[view.feedback]}</p>{/if}
  <div class="form-actions"><button class="primary" disabled={blocked||status!=='ready'||!result?.legal} onclick={commit}>{text.confirmTerrain}</button><button disabled={view.pending||(!anchor&&!result)} onclick={()=>{clear();onreset();}}>{text.cancelPreview}</button></div>
</section>
