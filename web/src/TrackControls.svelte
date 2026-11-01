<script lang="ts">
  import { untrack } from 'svelte';
  import { primaryAction } from './lib/primary-action.ts';
  import type { Position, RailwayPreview, RailwayQuery } from './generated/contracts.ts';
  import type { ShellModel, ShellState } from './lib/shell-model.ts';
  import { messages } from './lib/localization.ts';
  import { railwayFence, railwayRevision, railwayReasons, railwayStartLevel, RailwayPreviewRequestError } from './lib/railway-edit.ts';
  let {model,view,picked,level=$bindable(1),infrastructure='normal',onpreview,onpoints,onreveal,heightCut}:
    {model:ShellModel;view:ShellState;picked?:Position;level?:number;infrastructure?:'normal'|'bridge'|'girder'|'tunnel'|'garage';onpreview:(p?:RailwayPreview)=>void;onpoints:(p:{position:Position;label:string}[])=>void;onreveal:()=>void;heightCut:number}=$props();
  const text=$derived(messages[view.locale]);
  const wording=$derived(infrastructure==='garage' ? {task:'garageTask',description:'garageDescription',help:'garageHelp',rules:'garageRules',removal:'garageRemovalHelp',confirm:'confirmGarage'} as const : infrastructure==='tunnel' ? {task:'tunnelTask',description:'tunnelDescription',help:'tunnelHelp',rules:'tunnelRules',removal:'tunnelRemovalHelp',confirm:'confirmTunnel'} as const : infrastructure==='girder' ? {task:'girderTask',description:'girderDescription',help:'girderHelp',rules:'girderRules',removal:'girderRemovalHelp',confirm:'confirmGirder'} as const : infrastructure==='bridge' ? {task:'bridgeTask',description:'bridgeDescription',help:'bridgeHelp',rules:'bridgeRules',removal:'bridgeRemovalHelp',confirm:'confirmBridge'} as const : {task:'trackTask',description:'bridgeDescription',help:'railHelp',rules:'railHeuristicHelp',removal:'railRemovalHelp',confirm:'confirmTrack'} as const);
  let action=$state<'lay'|'remove'>('lay');
  let routing=$state<'closest'|'source'|'alternate'>('closest');
  let ax=$state(0),ay=$state(0),bx=$state(0),by=$state(0);
  let hasA=$state(false),hasB=$state(false),point=$state<'from'|'to'>('from');
  let autoLevel=$state(true),autoCoordinates=$state(true);
  let result=$state<RailwayPreview>(),status=$state<'idle'|'checking'|'ready'|'failed'|'stale'>('idle');
  let requestRejected=$state(false);
  let fence=$state<string>(),submitting=$state(false);
  let version=0,request:AbortController|undefined;
  const blocked=$derived(view.pending||view.connection!=='ready'||view.role==='observer'||view.feedback==='unresolved');
  function reset(){requestRejected=false;version++;request?.abort();fence=undefined;result=undefined;status='idle';onpreview();}
  function points(){onpoints([...(hasA?[{position:{x:ax,y:ay,z:level},label:'A'}]:[]),...(hasB?[{position:{x:bx,y:by,z:level},label:'B'}]:[])]);}
  function edit(){reset();points();}
  export function usesAutomaticPick(){return autoLevel&&(point==='from'||(infrastructure==='girder'||infrastructure==='tunnel')&&action==='lay');}
  function startLevel(position:Position){return view.read?railwayStartLevel(view.read,position,heightCut,infrastructure==='girder'&&action==='lay'?1:0):undefined;}
  function editLevel(){const refresh=result!==undefined||status==='checking';edit();if((infrastructure==='girder'||infrastructure==='tunnel')&&hasA&&hasB&&refresh)void check();}
  function detectCoordinateLevel(){
    if(autoLevel&&autoCoordinates&&view.read){
      const detected=startLevel({x:ax,y:ay,z:level});
      if(detected!==undefined){level=detected;autoCoordinates=false;}
    }
  }
  function editStart(){hasA=true;autoCoordinates=true;edit();}
  function changeLevelMode(enabled:boolean){const refresh=result!==undefined||status==='checking';autoLevel=enabled;reset();autoCoordinates=true;if(hasA)detectCoordinateLevel();points();if((infrastructure==='girder'||infrastructure==='tunnel')&&hasA&&hasB&&refresh)void check();}
  function choosePoint(next:'from'|'to'){reset();if(next==='to'&&hasA)detectCoordinateLevel();point=next;points();}
  export function cancelDraft(){const had=hasA||hasB||status!=='idle';reset();hasA=false;hasB=false;point='from';autoCoordinates=true;onpoints([]);return had;}
  async function check(){
    if(blocked)return;reset();const read=view.read,revision=railwayRevision(read);if(!read||revision===undefined)return;
    detectCoordinateLevel();
    // Each automatic tunnel quote starts from the selected column again. The
    // last resolved display level must not accumulate one-level adjustments.
    if(infrastructure==='tunnel'&&action==='lay'&&autoLevel)level=startLevel({x:ax,y:ay,z:level})??level;
    if(![ax,ay,bx,by,level].every(Number.isSafeInteger)){status='failed';return;}
    hasA=true;hasB=true;points();
    const query:RailwayQuery={expectedWorldEpoch:read.worldEpoch,expectedGeometryRevision:revision,expectedContentVersion:read.contentVersion as RailwayQuery['expectedContentVersion'],from:{x:ax,y:ay,z:level},to:{x:bx,y:by,z:level},action:infrastructure==='normal'?'lay':action,routing,infrastructure,...(infrastructure==='tunnel'&&action==='lay'?{tunnelRouting:autoLevel?'portals-auto' as const:'portals' as const}:{})};
    const id=version;fence=railwayFence(read);status='checking';request=new AbortController();
    try{
      const preview=await model.previewRailway(query,request.signal);
      if(id!==version||fence!==railwayFence(view.read)||blocked)return;
      result=preview;status='ready';if(infrastructure==='tunnel'&&action==='lay'&&autoLevel&&preview.legal&&preview.cells.length)level=preview.cells[0].position.z;onpreview(preview);
      if(infrastructure!=='normal'&&preview.cells.length)onpoints([{position:preview.cells[0].position,label:'A'},{position:preview.cells.at(-1)!.position,label:'B'}]);
    }catch(error){if(id===version){requestRejected=error instanceof RailwayPreviewRequestError;status='failed';onpreview();}}
  }
  async function commit(){
    if(blocked||status!=='ready'||!result?.legal||fence!==railwayFence(view.read))return;
    const id=version,query=result.query;submitting=true;
    const accepted=await model.send({kind:'editRailway',query});submitting=false;
    if(id!==version)return;
    if(accepted)cancelDraft();
    else {reset();status='stale';}
  }
  $effect(()=>{const p=picked;if(p)untrack(()=>{
    if(blocked)return;
    reset();if(point==='from'){if(autoLevel)level=infrastructure==='girder'&&action==='lay'?(startLevel(p)??p.z):p.z;ax=p.x;ay=p.y;hasA=true;autoCoordinates=false;point='to';}else{bx=p.x;by=p.y;hasB=true;}
    points();if(hasA&&hasB)void check();
  });});
  $effect(()=>{
    const current=railwayFence(view.read);
    if(fence&&fence!==current&&!submitting){reset();autoCoordinates=true;status='stale';}
    if(view.connection!=='ready'||view.role==='observer')untrack(()=>cancelDraft());
  });
  $effect(()=>()=>{version++;request?.abort();onpreview();onpoints([]);});
</script>
<section class="tool-card track-task" use:primaryAction aria-label={text[wording.task]}>
  <h2>{text[wording.task]}</h2>
  {#if infrastructure!=='normal'}<p>{text[wording.description]}</p>{/if}
  <p class="hint">{text[wording.help]}</p>
  <form onsubmit={event=>{event.preventDefault();void check();}}>
    <div class="form-actions">
      {#if infrastructure!=='normal'}<label>{text.railAction}<select aria-label={text.railAction} bind:value={action} disabled={view.pending} onchange={edit}><option value="lay">{text.railLay}</option><option value="remove">{text.railRemove}</option></select></label>{/if}
      <label>{text.buildLevel}<input aria-label={text.buildLevel} type="number" min="0" max={(view.read?.map.levels??1)-1} step="1" bind:value={level} disabled={view.pending} oninput={event=>{autoLevel=false;level=event.currentTarget.valueAsNumber;editLevel();}}/></label>
    </div>
    <label><input type="checkbox" aria-label={text.railAutoLevel} bind:checked={autoLevel} disabled={view.pending} onchange={event=>changeLevelMode(event.currentTarget.checked)}/>{text.railAutoLevel}</label>
    <p class="hint" data-testid="rail-level-state">{autoLevel?(hasA&&!autoCoordinates?`${text.railLevelDetected}: ${level}`:text.railLevelWaiting):`${text.railLevelManual}: ${level}`}</p>
    <fieldset disabled={view.pending}><legend>{text.railFrom}</legend><div class="form-actions">
      <label>X<input aria-label={text.railFromX} type="number" step="1" bind:value={ax} oninput={editStart}/></label>
      <label>Y<input aria-label={text.railFromY} type="number" step="1" bind:value={ay} oninput={editStart}/></label>
      <button type="button" aria-pressed={point==='from'} onclick={()=>choosePoint('from')}>{text.railPickFrom}</button>
    </div></fieldset>
    <fieldset disabled={view.pending}><legend>{text.railTo}</legend><div class="form-actions">
      <label>X<input aria-label={text.railToX} type="number" step="1" bind:value={bx} oninput={()=>{hasB=true;edit();}}/></label>
      <label>Y<input aria-label={text.railToY} type="number" step="1" bind:value={by} oninput={()=>{hasB=true;edit();}}/></label>
      <button type="button" aria-pressed={point==='to'} onclick={()=>choosePoint('to')}>{text.railPickTo}</button>
    </div></fieldset>
    {#if infrastructure==='normal'}<label>{text.railRouting}<select aria-label={text.railRouting} bind:value={routing} disabled={view.pending} onchange={edit}><option value="closest">{text.railClosest}</option><option value="source">{text.railSource}</option><option value="alternate">{text.railAlternate}</option></select></label>{/if}
    {#if action==='remove'}<p class="hint">{text[wording.removal]}</p>{/if}
    <button disabled={blocked||status==='checking'}>{text.railCheck}</button>
  </form>
  <p role="status" data-testid="rail-status">{status==='checking'?text.checkingGeometry:status==='failed'?text[requestRejected?'railPreviewRequestRejected':'geometryFailed']:status==='stale'?text.railStale:result?result.legal?text.railLegal:text[railwayReasons[result.reason as keyof typeof railwayReasons]??'geometryFailed']:text[point==='from'?'railPickFrom':'railPickTo']}</p>
  {#if result}
    <p data-testid="rail-route">{result.cells[0]?.position.x??result.query.from.x}, {result.cells[0]?.position.y??result.query.from.y}, {result.cells[0]?.position.z??level} → {result.cells.at(-1)?.position.x??result.query.to.x}, {result.cells.at(-1)?.position.y??result.query.to.y}, {result.cells.at(-1)?.position.z??level}{#if infrastructure==='normal'} · {result.reverseBend?text.railAlternate:text.railSource}{/if}</p>
    {#if result.cells.some(c=>c.protectedByCar)}<p>{text.railProtected}</p>{/if}
    {#if result.losses.length}<details open><summary>{text.railLossDetails}</summary><ul class="rail-losses">{#each result.losses as loss}<li>{text[loss.kind==='pier'?'railPierLoss':loss.kind==='reclamation'?'railReclamationLoss':'railAccessoryLoss']}: {loss.position.x}, {loss.position.y}, {loss.position.z} · {text.owner}: {loss.ownerId}{#if loss.supportedOwnerId} · {text.railwaySupportedOwner}: {loss.supportedOwnerId}{/if}</li>{/each}</ul></details>{/if}
  {/if}
  {#if level>heightCut}<button onclick={onreveal}>{text.revealPreview}</button>{/if}
  <details><summary>{text.railRoutingHelp}</summary><p class="hint">{text[wording.rules]}</p><p class="hint">{text.railCutHelp}</p></details>
  {#if !['idle','waiting','accepted'].includes(view.feedback)}<p role="status">{text[view.feedback]}</p>{/if}
  <div class="track-confirm">
    {#if result}<p data-testid="rail-cost">{text.cornerCost}: {BigInt(result.cost).toLocaleString(view.locale)} · {text.railCells}: {result.cells.length} · {text.railPiers}: {result.addedPiers.length} · {text.railLosses}: {result.losses.length}</p>{/if}
  <div class="form-actions"><button class="primary" disabled={blocked||status!=='ready'||!result?.legal} onclick={commit}>{text[wording.confirm]}</button><button disabled={view.pending||(!hasA&&!hasB&&!result)} onclick={cancelDraft}>{text.cancelPreview}</button></div>
  </div>
</section>
