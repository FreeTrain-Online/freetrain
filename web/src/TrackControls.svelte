<script lang="ts">
  import stationaryTypes from './generated/stationary-types.json' with {type:'json'};
  import { signalTypes, signalDirections, type SignalType } from './lib/building-catalog.ts';
  import { untrack } from 'svelte';
  import { WorldSpace } from './lib/map-geometry.ts';
  import { primaryAction } from './lib/primary-action.ts';
  import type { Position, RailwayPreview, RailwayQuery } from './generated/contracts.ts';
  import type { ShellModel, ShellState } from './lib/shell-model.ts';
  import { messages } from './lib/localization.ts';
  import { railwayFence, railwayRevision, railwayReasons, railwayStartLevel, RailwayPreviewRequestError } from './lib/railway-edit.ts';
  let {model,view,picked,level=$bindable(1),infrastructure='normal',onpreview,onpoints,onreveal,oncatalog,heightCut}:
    {model:ShellModel;view:ShellState;picked?:Position;level?:number;infrastructure?:'normal'|'bridge'|'girder'|'tunnel'|'garage'|'signal'|'pole'|'stationary'|'speed';onpreview:(p?:RailwayPreview)=>void;onpoints:(p:{position:Position;label:string}[])=>void;onreveal:()=>void;oncatalog:()=>void;heightCut:number}=$props();
  const text=$derived(messages[view.locale]);
  const speedLabels={low:'speedLow',medium:'speedMedium',fast:'speedFast'} as const;
  const speedOptions=['low','medium','fast'] as const;
  const positionKey=(p:Position)=>`${p.x},${p.y},${p.z}`;
  const listedSpeeds=$derived(speedOptions.map(speed=>({speed,positions:new Set(view.read?.mixed?.speedLimits?.[speed].map(positionKey))})));
  function speedNames(p:Position){return listedSpeeds.filter(s=>s.positions.has(positionKey(p))).map(s=>text[speedLabels[s.speed]]).join(' / ')||text.speedNone;}
  let speed=$state<'low'|'medium'|'fast'>('low');
  let speedSlope=$state(false),speedClimb=$state<0|2|4|6>(0);
  const pointTool=$derived(infrastructure==='speed'&&speedSlope||infrastructure==='stationary'||infrastructure==='signal'||infrastructure==='pole');
  const wording=$derived(infrastructure==='speed' ? {task:'speedTask',description:'speedDescription',help:'speedHelp',rules:'speedRules',removal:'speedRemovalHelp',confirm:'confirmSpeed'} as const : infrastructure==='stationary' ? {task:'stationaryTask',description:'stationaryDescription',help:'stationaryHelp',rules:'stationaryRules',removal:'stationaryRemovalHelp',confirm:'confirmStationary'} as const : infrastructure==='pole' ? {task:'poleTask',description:'poleDescription',help:'poleHelp',rules:'poleRules',removal:'poleRemovalHelp',confirm:'confirmPole'} as const : infrastructure==='signal' ? {task:'signalTask',description:'signalDescription',help:'signalHelp',rules:'signalRules',removal:'signalRemovalHelp',confirm:'confirmSignal'} as const : infrastructure==='garage' ? {task:'garageTask',description:'garageDescription',help:'garageHelp',rules:'garageRules',removal:'garageRemovalHelp',confirm:'confirmGarage'} as const : infrastructure==='tunnel' ? {task:'tunnelTask',description:'tunnelDescription',help:'tunnelHelp',rules:'tunnelRules',removal:'tunnelRemovalHelp',confirm:'confirmTunnel'} as const : infrastructure==='girder' ? {task:'girderTask',description:'girderDescription',help:'girderHelp',rules:'girderRules',removal:'girderRemovalHelp',confirm:'confirmGirder'} as const : infrastructure==='bridge' ? {task:'bridgeTask',description:'bridgeDescription',help:'bridgeHelp',rules:'bridgeRules',removal:'bridgeRemovalHelp',confirm:'confirmBridge'} as const : {task:'trackTask',description:'bridgeDescription',help:'railHelp',rules:'railHeuristicHelp',removal:'railRemovalHelp',confirm:'confirmTrack'} as const);
  const stationaryType=$derived(view.catalogSelections.stationary);
  const stationary=$derived(stationaryTypes.find(t=>t.id===stationaryType)!);
  const signalType=$derived(view.catalogSelections.signal as SignalType);
  const poleTypes=['{DDF7F835-8100-45fd-8AE7-EC498796D387}','{DD7D4548-5E0E-48e4-AB6E-64A443C240F7}','{86E56A60-19DF-4ba7-BFE8-ABFBDF5DFA0A}','{FD1635C1-05BF-41af-9185-4891B983E2A4}'] as const;
  let poleType=$state<NonNullable<RailwayQuery['poleType']>>(poleTypes[0]);
  const signalDirection=$derived(view.signalDirection);
  let action=$state<'lay'|'remove'>('lay');
  let routing=$state<'closest'|'source'|'alternate'>('closest');
  let ax=$state(0),ay=$state(0),bx=$state(0),by=$state(0);
  let via=$state<Position[]>([]),hovering=$state(false);
  let hoverTimer:ReturnType<typeof setTimeout>|undefined;
  let hoverKey='';
  let hasA=$state(false),hasB=$state(false),point=$state<'from'|'to'>('from');
  let autoLevel=$state(true),autoCoordinates=$state(true);
  let result=$state<RailwayPreview>(),status=$state<'idle'|'checking'|'ready'|'failed'|'stale'>('idle');
  const routeEnd=$derived(result&&infrastructure==='normal'&&result.query.from.x===result.query.to.x&&result.query.from.y===result.query.to.y&&result.query.from.z===result.query.to.z?result.query.to:result?.cells.at(-1)?.position??result?.query.to);
  const stationaryTarget=$derived(action==='remove'&&result?.legal?stationaryTypes.find(t=>t.id===view.read?.mixed?.voxels.find(v=>v.position.x===result?.cells[0]?.position.x&&v.position.y===result?.cells[0]?.position.y&&v.position.z===result?.cells[0]?.position.z)?.definition?.contributionId):undefined);
  const shownStationary=$derived(action==='remove'?stationaryTarget:stationary);
  let requestRejected=$state(false);
  let fence=$state<string>(),submitting=$state(false);
  let version=0,request:AbortController|undefined;
  const blocked=$derived(view.pending||view.connection!=='ready'||view.role==='observer'||view.feedback==='unresolved');
  function reset(){clearTimeout(hoverTimer);hoverKey='';requestRejected=false;version++;request?.abort();fence=undefined;result=undefined;status='idle';onpreview();}
  function points(){if(pointTool){onpoints(hasA?[{position:{x:ax,y:ay,z:level},label:'A'}]:[]);return;}onpoints([...(hasA?[{position:{x:ax,y:ay,z:level},label:'A'}]:[]),...via.map((p,i)=>({position:{...p,z:level},label:String.fromCharCode(66+i)})),...((hasB||hovering)&&!(hasA&&bx===ax&&by===ay)?[{position:{x:bx,y:by,z:level},label:String.fromCharCode(66+via.length)}]:[])]);}
  function edit(){reset();points();}
  export function usesSlopePick(){return infrastructure==='speed'&&speedSlope&&action==='remove';}
  export function isRemoving(){return action==='remove';}
  export function usesAutomaticPick(){return autoLevel&&(point==='from'||(infrastructure==='girder'||infrastructure==='tunnel')&&action==='lay');}
  function startLevel(position:Position){if(infrastructure==='stationary'&&view.read?.terrain){const space=new WorldSpace(view.read.map.width,view.read.map.height,view.read.map.levels);if(!space.containsHorizontal(position))return;const s=space.storage(position);return view.read.terrain.cells[s.v*space.horizontalSize+s.h].ground;}return view.read?railwayStartLevel(view.read,position,heightCut,infrastructure==='girder'&&action==='lay'?1:0,infrastructure==='speed'):undefined;}
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
  export function cancelDraft(){const had=hasA||hasB||status!=='idle';reset();via=[];hovering=false;hasA=false;hasB=false;point='from';autoCoordinates=true;onpoints([]);return had;}
  async function check(live=false,place=false){
    if(blocked)return;const currentHoverKey=hoverKey;reset();if(live)hoverKey=currentHoverKey;const read=view.read,revision=railwayRevision(read);if(!read||revision===undefined)return;
    detectCoordinateLevel();
    // Each automatic tunnel quote starts from the selected column again. The
    // last resolved display level must not accumulate one-level adjustments.
    if(infrastructure==='tunnel'&&action==='lay'&&autoLevel)level=startLevel({x:ax,y:ay,z:level})??level;
    if(![ax,ay,bx,by,level].every(Number.isSafeInteger)){status='failed';return;}
    if(pointTool){bx=ax;by=ay;}
    hasA=true;if(!live){hasB=true;hovering=false;}points();
    const routeVia=via.at(-1)?.x===bx&&via.at(-1)?.y===by?via.slice(0,-1):via;
    const query:RailwayQuery={expectedWorldEpoch:read.worldEpoch,expectedGeometryRevision:revision,expectedContentVersion:read.contentVersion as RailwayQuery['expectedContentVersion'],from:{x:ax,y:ay,z:level},to:{x:bx,y:by,z:level},action:infrastructure==='normal'?'lay':action,routing,infrastructure,...(infrastructure==='normal'&&routeVia.length?{via:routeVia.map(p=>({...p,z:level})) as RailwayQuery['via']}:{}),...(infrastructure==='speed'?{speed,speedSlope,speedClimb}:{}),...(infrastructure==='signal'?{signalType,signalDirection}:{}),...(infrastructure==='pole'?{poleType}:{}),...(infrastructure==='stationary'?{stationaryType}:{}),...(infrastructure==='tunnel'&&action==='lay'?{tunnelRouting:autoLevel?'portals-auto' as const:'portals' as const}:{})};
    const id=version;fence=railwayFence(read);status='checking';request=new AbortController();
    try{
      const preview=await model.previewRailway(query,request.signal);
      if(id!==version||fence!==railwayFence(view.read)||blocked)return;
      result=preview;status='ready';if(infrastructure==='tunnel'&&action==='lay'&&autoLevel&&preview.legal&&preview.cells.length)level=preview.cells[0].position.z;onpreview(preview);
      if(infrastructure!=='normal'&&!pointTool&&preview.cells.length)onpoints([{position:preview.cells[0].position,label:'A'},{position:preview.cells.at(-1)!.position,label:'B'}]);
      if(place&&preview.legal)await commit();
    }catch(error){if(id===version){requestRejected=error instanceof RailwayPreviewRequestError;status='failed';onpreview();}}
  }
  async function commit(){
    if(blocked||hovering||!hasB||status!=='ready'||!result?.legal||fence!==railwayFence(view.read))return;
    const id=version,query=result.query;submitting=true;
    const accepted=await model.send({kind:'editRailway',query});submitting=false;
    if(id!==version)return;
    if(accepted)cancelDraft();
    else {reset();status='stale';}
  }
  export function hoverPosition(p?:Position){
    if(infrastructure!=='normal'||blocked||!hasA||hasB||point!=='to')return;
    if(!p){if(hovering){reset();hovering=false;points();}return;}
    const key=`${p.x},${p.y}`;
    if(key===hoverKey)return;
    reset();hoverKey=key;bx=p.x;by=p.y;hovering=true;points();
    hoverTimer=setTimeout(()=>{void check(true);},40);
  }
  export function pickPosition(p:Position,append=false){
    if(blocked)return;
    reset();hovering=false;
    // The start is the loop-closing target, never a removable waypoint.
    if(infrastructure==='normal'&&hasA&&p.x===ax&&p.y===ay){
      if(hasB&&(bx!==ax||by!==ay)&&!(via.at(-1)?.x===bx&&via.at(-1)?.y===by)){
        if(via.length>=8){points();return;}
        via=[...via,{x:bx,y:by,z:level}];
      }
      bx=ax;by=ay;hasB=via.length>0||!append;point='to';points();
      if(hasB)void check(false,true);
      return;
    }
    if(append&&infrastructure==='normal'&&hasA){
      const nodes=[{x:ax,y:ay,z:level},...via,...(hasB?[{x:bx,y:by,z:level}]:[])];
      const index=nodes.findIndex(node=>node.x===p.x&&node.y===p.y);
      if(index!==-1){
        const keepEndpoint=hasB&&index!==nodes.length-1;
        nodes.splice(index,1);
        if(!nodes.length){cancelDraft();return;}
        ax=nodes[0].x;ay=nodes[0].y;point='to';autoCoordinates=false;
        hasB=keepEndpoint&&nodes.length>1;
        via=nodes.slice(1,hasB?-1:undefined);
        const end=nodes.at(-1)!;bx=end.x;by=end.y;
        points();
        if(nodes.length>1)void check(!hasB);
        return;
      }
    }
    if(point==='from'||pointTool){
      via=[];hasB=false;
      if(autoLevel)level=infrastructure==='girder'&&action==='lay'?(startLevel(p)??p.z):p.z;
      ax=p.x;ay=p.y;hasA=true;autoCoordinates=false;point=pointTool?'from':'to';
      if(pointTool){hasB=true;bx=ax;by=ay;}
    }else if(append&&infrastructure==='normal'&&via.length<8){
      const last=via.at(-1)??{x:ax,y:ay};
      if(p.x===last.x&&p.y===last.y){points();return;}
      bx=p.x;by=p.y;via=[...via,{...p,z:level}];hasB=false;
      points();void check(true);return;
    }else{bx=p.x;by=p.y;hasB=true;}
    points();if(hasA&&hasB)void check(false,infrastructure==='normal'&&!append);
  }
  $effect(()=>{const p=picked;if(p)untrack(()=>pickPosition(p));});
  $effect(()=>{
    const current=railwayFence(view.read);
    if(fence&&fence!==current&&!submitting){reset();autoCoordinates=true;status='stale';}
    if(view.connection!=='ready'||view.role==='observer')untrack(()=>cancelDraft());
  });
  // Changing the catalog variant or direction invalidates an outstanding quote,
  // while keeping the chosen map point ready for the next preview.
  $effect(()=>{void stationaryType;void signalType;void signalDirection;untrack(edit);});
  $effect(()=>()=>{clearTimeout(hoverTimer);version++;request?.abort();onpreview();onpoints([]);});
</script>
<section class="tool-card track-task" use:primaryAction aria-label={text[wording.task]}>
  <h2>{text[wording.task]}</h2>
  {#if infrastructure!=='normal'}<p>{text[wording.description]}</p>{/if}
  <p class="hint">{text[wording.help]}</p>
  {#if infrastructure==='normal'}<p class="hint" data-testid="rail-nodes">{text.railNodeHelp} {hasA?1+via.length+(hasB?1:0):0}/10</p>{/if}
  <form onsubmit={event=>{event.preventDefault();void check();}}>
    {#if infrastructure==='stationary'}
      {#if action==='lay'}<p data-testid="catalog-selection">{text.stationaryType}: {view.locale==='ja'?stationary.groupJa:stationary.groupEn} · {stationary.variant}</p><div class="form-actions"><button type="button" onclick={oncatalog}>{text.chooseFromCatalog}</button></div>{/if}
      {#if shownStationary}
      {#if action==='remove'}<p>{text.stationaryTarget}: {view.locale==='ja'?shownStationary.groupJa:shownStationary.groupEn} · {shownStationary.variant}</p>{/if}
      <p>{text.stationaryDimensions}: {shownStationary.sizeX} × {shownStationary.sizeY} × {shownStationary.height}{#if action==='lay'} · {text.price}: {shownStationary.price.toLocaleString(view.locale)}{/if}</p>
      {/if}
    {/if}
    {#if infrastructure==='speed'}
      <label>{text.speedSection}<select aria-label={text.speedSection} bind:value={speedSlope} disabled={view.pending} onchange={()=>{cancelDraft();}}><option value={false}>{text.speedFlat}</option><option value={true}>{text.speedSlope}</option></select></label>
      <label>{text.speedValue}<select aria-label={text.speedValue} bind:value={speed} disabled={view.pending||action==='remove'} onchange={edit}>{#each speedOptions as value}<option value={value}>{text[speedLabels[value]]}</option>{/each}</select></label>
      {#if speedSlope}<label>{text.slopeDirection}<select aria-label={text.slopeDirection} bind:value={speedClimb} disabled={view.pending} onchange={edit}>{#each signalDirections as d}<option value={d.value}>{text[d.label]}</option>{/each}</select></label>{/if}
      <p data-testid="speed-membership">{text.speedMembership}: {result ? result.cells.map(c=>`${c.position.x}, ${c.position.y}, ${c.position.z}: ${speedNames(c.position)}`).join(' · ') : text.speedMembershipHint}</p>
    {/if}
    {#if infrastructure==='pole'}<label>{text.poleType}<select aria-label={text.poleType} bind:value={poleType} onchange={edit} disabled={view.pending}>{#each poleTypes as type,i}<option value={type}>{text[(['poleDouble1','poleDouble2','poleSingle1','poleSingle2'] as const)[i]]}</option>{/each}</select></label>{/if}
    {#if infrastructure==='signal'}
      <p data-testid="catalog-selection">{text.signalType}: {text[signalTypes.find(type=>type.id===signalType)!.label]}</p><button type="button" onclick={oncatalog}>{text.chooseFromCatalog}</button>
      <p data-testid="signal-direction">{text.signalDirection}: {text[signalDirections.find(d=>d.value===signalDirection)!.label]}</p>
    {/if}
    <div class="form-actions">
      {#if infrastructure!=='normal'}<label>{text.railAction}<select aria-label={text.railAction} bind:value={action} disabled={view.pending} onchange={edit}><option value="lay">{text[infrastructure==='speed'?'speedAdd':infrastructure==='stationary'?'stationaryPlace':'railLay']}</option><option value="remove">{text.railRemove}</option></select></label>{/if}
      <label>{text.buildLevel}<input aria-label={text.buildLevel} type="number" min="0" max={(view.read?.map.levels??1)-1} step="1" bind:value={level} disabled={view.pending} oninput={event=>{autoLevel=false;level=event.currentTarget.valueAsNumber;editLevel();}}/></label>
    </div>
    <label><input type="checkbox" aria-label={text.railAutoLevel} bind:checked={autoLevel} disabled={view.pending} onchange={event=>changeLevelMode(event.currentTarget.checked)}/>{text.railAutoLevel}</label>
    <p class="hint" data-testid="rail-level-state">{autoLevel?(hasA&&!autoCoordinates?`${text.railLevelDetected}: ${level}`:text.railLevelWaiting):`${text.railLevelManual}: ${level}`}</p>
    <fieldset disabled={view.pending}><legend>{text.railFrom}</legend><div class="form-actions">
      <label>X<input aria-label={text.railFromX} type="number" step="1" bind:value={ax} oninput={editStart}/></label>
      <label>Y<input aria-label={text.railFromY} type="number" step="1" bind:value={ay} oninput={editStart}/></label>
      <button type="button" aria-pressed={point==='from'} onclick={()=>choosePoint('from')}>{text.railPickFrom}</button>
    </div></fieldset>
    {#if !pointTool}<fieldset disabled={view.pending}><legend>{text.railTo}</legend><div class="form-actions">
      <label>X<input aria-label={text.railToX} type="number" step="1" bind:value={bx} oninput={()=>{hasB=true;edit();}}/></label>
      <label>Y<input aria-label={text.railToY} type="number" step="1" bind:value={by} oninput={()=>{hasB=true;edit();}}/></label>
      <button type="button" aria-pressed={point==='to'} onclick={()=>choosePoint('to')}>{text.railPickTo}</button>
    </div></fieldset>
    {/if}
    {#if infrastructure==='normal'}<label>{text.railRouting}<select aria-label={text.railRouting} bind:value={routing} disabled={view.pending} onchange={edit}><option value="closest">{text.railClosest}</option><option value="source">{text.railSource}</option><option value="alternate">{text.railAlternate}</option></select></label>{/if}
    {#if action==='remove'}<p class="hint">{text[wording.removal]}</p>{/if}
    <button disabled={blocked||status==='checking'}>{text.railCheck}</button>
  </form>
  <p role="status" data-testid="rail-status">{status==='checking'?text.checkingGeometry:status==='failed'?text[requestRejected?'railPreviewRequestRejected':'geometryFailed']:status==='stale'?text.railStale:result?result.legal?text.railLegal:text[railwayReasons[result.reason as keyof typeof railwayReasons]??'geometryFailed']:text[point==='from'?'railPickFrom':'railPickTo']}</p>
  {#if result}
    <p data-testid="rail-route">{result.cells[0]?.position.x??result.query.from.x}, {result.cells[0]?.position.y??result.query.from.y}, {result.cells[0]?.position.z??level} → {routeEnd?.x??result.query.to.x}, {routeEnd?.y??result.query.to.y}, {routeEnd?.z??level}{#if infrastructure==='normal'} · {result.reverseBend?text.railAlternate:text.railSource}{/if}</p>
    {#if result.cells.some(c=>c.protectedByCar)}<p>{text.railProtected}</p>{/if}
    {#if result.losses.length}<details open><summary>{text.railLossDetails}</summary><ul class="rail-losses">{#each result.losses as loss}<li>{text[loss.kind==='pier'?'railPierLoss':loss.kind==='reclamation'?'railReclamationLoss':'railAccessoryLoss']}: {loss.position.x}, {loss.position.y}, {loss.position.z} · {text.owner}: {loss.ownerId}{#if loss.supportedOwnerId} · {text.railwaySupportedOwner}: {loss.supportedOwnerId}{/if}</li>{/each}</ul></details>{/if}
  {/if}
  {#if level>heightCut}<button onclick={onreveal}>{text.revealPreview}</button>{/if}
  <details><summary>{text.railRoutingHelp}</summary><p class="hint">{text[wording.rules]}</p><p class="hint">{text.railCutHelp}</p></details>
  {#if !['idle','waiting','accepted'].includes(view.feedback)}<p role="status">{text[view.feedback]}</p>{/if}
  <div class="track-confirm">
    {#if result}<p data-testid="rail-cost">{text.cornerCost}: {BigInt(result.cost).toLocaleString(view.locale)} · {text.railCells}: {result.cells.length} · {text.railPiers}: {result.addedPiers.length} · {text.railLosses}: {result.losses.length}</p>{/if}
  <div class="form-actions"><button class="primary" disabled={blocked||hovering||!hasB||status!=='ready'||!result?.legal} onclick={commit}>{text[wording.confirm]}</button><button disabled={view.pending||(!hasA&&!hasB&&!result)} onclick={cancelDraft}>{text.cancelPreview}</button></div>
  </div>
</section>
