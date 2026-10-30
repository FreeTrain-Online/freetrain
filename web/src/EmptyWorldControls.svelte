<script lang="ts">
  import { onDestroy } from 'svelte';
  import { renderWorldPreview, compassVector, type PreviewMode, type WorldCandidate } from './lib/world-preview.ts';
  import DecisionModal from './DecisionModal.svelte';
  import { messages, interpolate, type Locale } from './lib/localization.ts';
  type Provider = { binding: { contributionId:string }; name:string; author:string; description:string; dimensions:{h:number;v:number;z:number};waterLevel:number };
  let { locale, epoch, revision, contentIdentity, disabled, onbusy, onmodal }: { locale: Locale; epoch?: string; revision?: string; contentIdentity: string; disabled: boolean; onbusy: (busy: boolean) => void; onmodal: () => void } = $props();
  const text = $derived(messages[locale]);
  let source = $state('empty'), file = $state<File>(), provider = $state('');
  let seed = $state<number|undefined>(0), relief = $state('hills'), amount = $state<number|undefined>(50), scale = $state<number|undefined>(24), smoothness = $state<number|undefined>(50);
  function reliefDefaults(kind:string) { relief=kind; [amount,scale,smoothness]=kind==='plains'?[20,32,75]:kind==='mountains'?[85,32,25]:[50,24,50]; }
  let coast = $state('inland'), sides = $state(1), coverage = $state<number|undefined>(40);
  let lakeCount=$state<number|undefined>(0), lakeArea=$state<number|undefined>(3), riverDirection=$state('none'), riverWidth=$state<number|undefined>(1), riverMeander=$state<number|undefined>(30);
  const riverDirections=['none','northSouth','eastWest'] as const;
  const directions = [{bit:1,label:'coastNorth'},{bit:2,label:'coastEast'},{bit:4,label:'coastSouth'},{bit:8,label:'coastWest'}] as const;
  type ForestChoice = {binding:{packId:string;version:string;catalogHash:string;contributionId:string};species:string;variant:string};
  let forestChoices=$state<ForestChoice[]>([]), forestCoverage=$state<number|undefined>(0), clustering=$state<number|undefined>(50), startingArea=$state<number|undefined>(0), forestVariant=$state('natural');
  const species=['cedar','pine','camphor','maple','ginkgo','beech'] as const;
  const speciesLabels={cedar:'treeCedar',pine:'treePine',camphor:'treeCamphor',maple:'treeMaple',ginkgo:'treeGinkgo',beech:'treeBeech'} as const;
  let treeSet=$state<string[]>([...species]);
  const palette=$derived(forestChoices.filter(c=>c.variant===forestVariant && treeSet.includes(c.species)).map(c=>c.binding));
  let providers = $state<Provider[]>([]);
  let requestAbort:AbortController|undefined;
  $effect(()=>{contentIdentity;const controller=new AbortController();void fetch('/api/worlds/heightmap/providers',{signal:controller.signal}).then(r=>r.ok?r.json():[]).then(rows=>{if(!controller.signal.aborted)providers=rows;}).catch(()=>{});return()=>controller.abort();});
  $effect(()=>{contentIdentity;const controller=new AbortController();void fetch('/api/worlds/generated/forests',{signal:controller.signal}).then(r=>r.ok?r.json():[]).then(rows=>{if(!controller.signal.aborted)forestChoices=rows;}).catch(()=>{});return()=>controller.abort();});
  const maxSide = $derived(2048);
  const selectedProvider = $derived(providers.find(p=>p.binding.contributionId===provider));
  let name = $state(''), h = $state<number|undefined>(30), v = $state<number|undefined>(42), z = $state<number|undefined>(6), water = $state<number|undefined>(1);
  const forestValid=$derived([forestCoverage,clustering,startingArea].every(Number.isInteger) && forestCoverage!>=0 && forestCoverage!<=90 && clustering!>=0 && clustering!<=100 && startingArea!>=0 && startingArea!<=Math.floor((h??0)*(v??0)/4) && (forestCoverage===0 || palette.length===treeSet.length && palette.length>0));
  const waterValid=$derived([lakeCount,lakeArea,riverWidth,riverMeander].every(Number.isInteger) && lakeCount!>=0 && lakeCount!<=8 && lakeArea!>=1 && lakeArea!<=20 && riverWidth!>=1 && riverWidth!<=8 && riverMeander!>=0 && riverMeander!<=100 && (coast==='inland'?0:coverage!)+100*(startingArea??0)/((h??1)*(v??1))+lakeCount!*lakeArea!+100*(riverDirection==='none'?0:1)*riverWidth!*((h??0)+(v??0))/2/((h??1)*(v??1))<=75);
  let candidate = $state<WorldCandidate>();
  let previewMode = $state<PreviewMode>('topDown');
  let previewImage=$state<string>();
  $effect(()=>{previewImage=candidate?renderWorldPreview(candidate,previewMode):undefined;});
  let capturedEpoch = $state<string>(), capturedRevision = $state<string>(), capturedContent = $state<string>();
  let phase = $state<'idle'|'preview'|'commit'>('idle'), confirmation = $state(false);
  const busy = $derived(phase!=='idle');
  type Feedback = 'creationFailed'|'staleCandidate'|'creationBusy'|'created'|'idle'|'generationCancelled'|'imageFormat'|'imageBytes'|'imagePixels'|'imageAlpha'|'imageAnimation'|'imageInvalid'|'coastCoverageImpossible'|'coastTopologyImpossible'|'coastBoundaryImpossible'|'lakeImpossible'|'riverImpossible'|'startingAreaImpossible';
  let feedback = $state<Feedback>('idle');
  let generation = 0, disposed = false;
  function setPhase(value:typeof phase) { phase=value;onbusy(value!=='idle'); }
  function current(token:number,e:string|undefined,r:string|undefined,content:string) {
    return !disposed && token===generation && !disabled && epoch===e && revision===r && contentIdentity===content;
  }
  const valid = $derived([h,v,z,water].every(Number.isInteger) && h!>=1 && h!<=maxSide && v!>=1 && v!<=maxSide && h!*v!<=1700000 && z!>=1 && z!<=64 && water!>=0 && water!<z! && name.length<=128 && !/[\u0000-\u001f\u007f-\u009f]/.test(name) && (source==='empty' || source==='generated' && h!>=16 && v!>=16 && z!>=8 && water!>=1 && water!<=z!-4 && [seed,amount,scale,smoothness].every(Number.isInteger) && seed!>=-2147483648 && seed!<=2147483647 && amount!>=0 && amount!<=100 && scale!>=4 && scale!<=128 && smoothness!>=0 && smoothness!<=100 && waterValid && forestValid && (coast==='inland' || Number.isInteger(coverage) && coverage!>=5 && coverage!<=75 && (coast!=='sides' || sides!==0)) || source==='heightmap' && Boolean(provider || file && file.size>0 && file.size<=8388608)));
  async function call(path: string, body: unknown) {
    const signal=path==='preview'&&requestAbort?AbortSignal.any([requestAbort.signal,AbortSignal.timeout(35000)]):AbortSignal.timeout(35000);
    const response = await fetch(`/api/worlds/empty/${path}`, { method:'POST', credentials:'same-origin', headers:{'Content-Type':'application/json'}, body:JSON.stringify(body), signal });
    if (!response.ok) {
      const result = await response.json().catch(()=>({}));
      throw new Error(result.code === 'creationBusy' ? 'creationBusy' : ['staleEpoch','staleRevision','staleCandidate'].includes(result.code) ? 'staleCandidate' : 'creationFailed');
    }
    return response.json();
  }
  function invalidate() {
    if(candidate || phase==='preview')feedback='staleCandidate';
    generation++; confirmation=false;requestAbort?.abort();requestAbort=undefined;
    if(phase==='preview')setPhase('idle');
    const old=candidate; candidate=undefined;
    if(old)void call('cancel',old.id).catch(()=>{});
  }
  function cancelPreview() { invalidate();feedback='generationCancelled'; }
  function newSeed() { invalidate();seed=crypto.getRandomValues(new Int32Array(1))[0];feedback='idle'; }
  function sizePreset(size:number) { invalidate();h=size;v=size;z=size===64?16:size===128?32:64;water=Math.floor(z/4); }
  onDestroy(()=>{disposed=true;invalidate();onbusy(false);});
  $effect(()=>{
    if((candidate || phase==='preview') && (disabled || epoch!==capturedEpoch || revision!==capturedRevision || contentIdentity!==capturedContent)) {
      invalidate();feedback='staleCandidate';
    }
  });
  $effect(()=>{ if(candidate) { const timer=setTimeout(()=>{invalidate();feedback='staleCandidate';},Math.max(0,Date.parse(candidate.expiresAt)-Date.now()));return()=>clearTimeout(timer); } });
  async function preview() {
    if(busy||disabled||!valid)return;
    invalidate(); const token=generation, e=epoch, r=revision, content=contentIdentity;
    capturedEpoch=e;capturedRevision=r;capturedContent=content;
    setPhase('preview');feedback='idle';requestAbort=new AbortController();
    const signal=AbortSignal.any([requestAbort.signal,AbortSignal.timeout(35000)]);
    try {
      let result:WorldCandidate;
      if(source==='empty')result=await call('preview',{name:name || text.unnamedWorld,dimensions:{h,v,z},waterLevel:water,epoch:e,revision:r});
      else if(source==='generated') {
        const response=await fetch('/api/worlds/generated/preview',{method:'POST',headers:{'Content-Type':'application/json'},signal,body:JSON.stringify({name:name||text.unnamedWorld,dimensions:{h,v,z},waterLevel:water,epoch:e,revision:r,options:{seed,relief:{kind:['plains','hills','mountains'].indexOf(relief),amount,featureScale:scale,smoothness},coast:{kind:['inland','island','sides'].indexOf(coast),sides:coast==='sides'?sides:0,waterPercent:coast==='inland'?0:coverage},lakes:{count:lakeCount,areaPercent:lakeCount?lakeArea:0},rivers:{direction:riverDirections.indexOf(riverDirection as typeof riverDirections[number]),width:riverWidth,meander:riverMeander},forests:{coveragePercent:forestCoverage,clustering,palette:forestCoverage?palette:[]},startingArea}})});
        if(!response.ok){const error=await response.json().catch(()=>({}));throw new Error(error.code==='geographyImpossible'?(error.constraint==='starting-area'?'startingAreaImpossible':String(error.constraint).startsWith('lake-')?'lakeImpossible':String(error.constraint).startsWith('river-')?'riverImpossible':error.constraint==='coast-coverage'?'coastCoverageImpossible':['coast-boundary','coast-sector','coast-island-rim'].includes(error.constraint)?'coastBoundaryImpossible':'coastTopologyImpossible'):error.code||'creationFailed');}
        result=await response.json();
      } else {
        const query=new URLSearchParams({name:name||text.unnamedWorld,h:String(h),v:String(v),z:String(z),water:String(water),epoch:String(e),revision:String(r)});
        if(provider)query.set('provider',provider);
        const response=await fetch('/api/worlds/heightmap/preview?'+query,{method:'POST',body:provider?undefined:file,headers:provider?{}:{'Content-Type':file?.type || 'application/octet-stream'},signal});
        if(!response.ok){const error=await response.json().catch(()=>({}));throw new Error(error.code||'creationFailed');}
        result=await response.json();
      }
      if(!current(token,e,r,content)){void call('cancel',result.id).catch(()=>{});return;}
      if(Date.now()>=Date.parse(result.expiresAt))throw new Error('staleCandidate');
      candidate=result;
    }catch(error){
      if(!current(token,e,r,content))return;
      const code=error instanceof Error?error.message:'';
      const imageErrors:Record<string,Feedback>={'image-format':'imageFormat','image-bytes':'imageBytes','image-pixels':'imagePixels','image-alpha':'imageAlpha','image-animation':'imageAnimation','image-invalid':'imageInvalid'};
      feedback=imageErrors[code]??(['coastCoverageImpossible','coastTopologyImpossible','coastBoundaryImpossible','lakeImpossible','riverImpossible','startingAreaImpossible'].includes(code)?code as Feedback:code==='creationBusy'?'creationBusy':['staleCandidate','staleEpoch','staleRevision'].includes(code)?'staleCandidate':'creationFailed');
    }
    finally{if(token===generation && !disposed){requestAbort=undefined;setPhase('idle');}}
  }
  async function commit() {
    if(busy||disabled||!candidate||epoch!==capturedEpoch||revision!==capturedRevision||contentIdentity!==capturedContent)return;
    if(Date.now()>=Date.parse(candidate.expiresAt)){invalidate();feedback='staleCandidate';return;}
    const retained=candidate;confirmation=false;setPhase('commit');
    // A failed/uncertain HTTP outcome is never replayed. A new preview is required.
    candidate=undefined;const token=++generation;
    try {await call('commit',{id:retained.id,requestHash:retained.requestHash,candidateHash:retained.candidateHash,epoch:capturedEpoch,revision:capturedRevision});if(!disposed && token===generation)feedback='created';}
    catch(error){if(!disposed && token===generation)feedback=error instanceof Error && error.message==='staleCandidate'?'staleCandidate':'creationFailed';void call('cancel',retained.id).catch(()=>{});}
    finally{if(!disposed && token===generation)setPhase('idle');}
  }
</script>
<p class="hint">{source==='empty'?text.emptyWorldHelp:source==='generated'?text.generatedHelp:text.heightmapHelp}</p>
<fieldset disabled={busy || disabled} oninput={invalidate} onchange={invalidate}>
  <label class="field">{text.worldSource}<select aria-label={text.worldSource} bind:value={source} onchange={event=>{source=event.currentTarget.value;invalidate();provider='';if(source!=='empty'){h=32;v=32;z=16;water=4;}}}><option value="empty">{text.emptySource}</option><option value="heightmap">{text.heightmapSource}</option><option value="generated">{text.generatedSource}</option></select></label>
  {#if source==='heightmap'}
    {#if providers.length}<label class="field">{text.fixedHeightmap}<select aria-label={text.fixedHeightmap} bind:value={provider} onchange={event=>{provider=event.currentTarget.value;invalidate();const p=providers.find(p=>p.binding.contributionId===provider);if(p){name=p.name;h=p.dimensions.h;v=p.dimensions.v;z=p.dimensions.z;water=p.waterLevel;}}}><option value="">{text.uploadHeightmap}</option>{#each providers as p}<option value={p.binding.contributionId}>{p.name}</option>{/each}</select></label>{/if}
    {#if selectedProvider}<p>{selectedProvider.author} · {selectedProvider.description}</p>{:else}<label class="field">{text.heightmapImage}<input type="file" accept=".png,.jpg,.jpeg,.bmp,.gif" onchange={event=>{invalidate();file=event.currentTarget.files?.[0];feedback=file && file.size>8388608?'imageBytes':'idle';}}/></label>{/if}
  {/if}
  {#if source==='generated'}
    <label class="field">{text.reliefKind}<select aria-label={text.reliefKind} bind:value={relief} onchange={event=>reliefDefaults(event.currentTarget.value)}><option value="plains">{text.reliefPlains}</option><option value="hills">{text.reliefHills}</option><option value="mountains">{text.reliefMountains}</option></select></label>
    <div class="form-actions" aria-label={text.worldSizePresets}>{#each [64,128,256] as size}<button onclick={()=>sizePreset(size)}>{size} × {size}</button>{/each}</div>
    <label class="field">{text.generationSeed}<input type="number" min="-2147483648" max="2147483647" step="1" bind:value={seed}/></label>
    <button onclick={newSeed}>{text.newGenerationSeed}</button>
    <label class="field">{text.reliefAmount}<input type="number" min="0" max="100" step="1" bind:value={amount}/></label>
    <label class="field">{text.featureScale}<input type="number" min="4" max="128" step="1" bind:value={scale}/></label>
    <label class="field">{text.reliefSmoothness}<input type="number" min="0" max="100" step="1" bind:value={smoothness}/></label>
    <label class="field">{text.coastKind}<select aria-label={text.coastKind} bind:value={coast}><option value="inland">{text.coastInland}</option><option value="island">{text.coastIsland}</option><option value="sides">{text.coastSides}</option></select></label>
    {#if coast==='sides'}<fieldset class="coast-directions"><legend>{text.coastSides}</legend>{#each directions as direction}<label><input type="checkbox" checked={Boolean(sides & direction.bit)} onchange={event=>{sides=event.currentTarget.checked?sides|direction.bit:sides & ~direction.bit;}}/>{text[direction.label]}</label>{/each}</fieldset>{/if}
    {#if coast!=='inland'}<label class="field">{text.coastCoverage}<input type="number" min="5" max="75" step="1" bind:value={coverage}/></label>{/if}
    <p class="hint">{text.coastHelp}</p>
    <label class="field">{text.lakeCount}<input type="number" min="0" max="8" step="1" bind:value={lakeCount}/></label>
    {#if lakeCount}<label class="field">{text.lakeArea}<input type="number" min="1" max="20" step="1" bind:value={lakeArea}/></label>{/if}
    <label class="field">{text.riverDirection}<select aria-label={text.riverDirection} bind:value={riverDirection}><option value="none">{text.riverNone}</option><option value="northSouth">{text.riverNorthSouth}</option><option value="eastWest">{text.riverEastWest}</option></select></label>
    {#if riverDirection!=='none'}
      <label class="field">{text.riverWidth}<input type="number" min="1" max="8" step="1" bind:value={riverWidth}/></label>
      <label class="field">{text.riverMeander}<input type="number" min="0" max="100" step="1" bind:value={riverMeander}/></label>
    {/if}
    <p class="hint">{text.waterFeaturesHelp}</p>
    {#if !waterValid}<p role="status">{text.waterSettingsInvalid}</p>{/if}
    <label class="field">{text.forestPreset}<select aria-label={text.forestPreset} value={forestCoverage===0?'0':forestCoverage===15?'15':forestCoverage===35?'35':forestCoverage===70?'70':'custom'} onchange={event=>{if(event.currentTarget.value!=='custom')forestCoverage=Number(event.currentTarget.value);}}><option value="0">{text.forestNone}</option><option value="15">{text.forestSparse}</option><option value="35">{text.forestStandard}</option><option value="70">{text.forestDense}</option><option value="custom">{text.forestCustom}</option></select></label>
    <label class="field">{text.forestCoverage}<input type="number" min="0" max="90" step="1" bind:value={forestCoverage}/></label>
    {#if forestCoverage}
      <label class="field">{text.forestClustering}<input type="number" min="0" max="100" step="1" bind:value={clustering}/></label>
      <label class="field">{text.forestGround}<select aria-label={text.forestGround} bind:value={forestVariant}><option value="natural">{text.forestNatural}</option><option value="mountain">{text.forestMountain}</option><option value="grass">{text.forestGrass}</option></select></label>
      <fieldset class="coast-directions"><legend>{text.forestSpecies}</legend>{#each species as tree}<label><input type="checkbox" bind:group={treeSet} value={tree}/>{text[speciesLabels[tree]]}</label>{/each}</fieldset>
      {#if !palette.length}<p role="status">{text.forestUnavailable}</p>{/if}
    {/if}
    <label class="field">{text.startingArea}<input type="number" min="0" max={Math.floor((h??0)*(v??0)/4)} step="1" bind:value={startingArea}/></label>
    <p class="hint">{text.forestHelp}</p>
  {/if}
  <label class="field">{text.worldName}<input maxlength="128" bind:value={name} placeholder={text.unnamedWorld}/></label>
  <label class="field">{text.worldH}<input type="number" min={source==='generated'?16:1} max={maxSide} step="1" disabled={!!provider} bind:value={h}/></label>
  <label class="field">{text.worldV}<input type="number" min={source==='generated'?16:1} max={maxSide} step="1" disabled={!!provider} bind:value={v}/></label>
  <label class="field">{text.worldZ}<input type="number" min={source==='generated'?8:1} max="64" step="1" disabled={!!provider} bind:value={z}/></label>
  <label class="field">{text.worldWater}<input type="number" min={source==='generated'?1:0} max={(z??1)-(source==='generated'?4:1)} step="1" disabled={!!provider} bind:value={water}/></label>
  {#if source==='empty'}<button onclick={()=>{invalidate();h=30;v=42;z=6;water=1;}}>{text.sourceDefaults}</button>{/if}
</fieldset>
<div class="form-actions">
  <button disabled={busy||disabled||!valid} onclick={preview}>{text.previewWorld}</button>
  {#if phase==='preview'}<button onclick={cancelPreview}>{text.cancel}</button>{/if}
  {#if !busy && !candidate && feedback!=='idle' && feedback!=='created' && feedback!=='generationCancelled'}<button disabled={disabled||!valid} onclick={preview}>{text.retryGeneration}</button>{/if}
  {#if candidate && source==='generated'}<button disabled={busy||disabled||!valid} onclick={preview}>{text.regenerateWorld}</button>{/if}
  {#if candidate}<button disabled={busy||disabled} onclick={()=>{onmodal();confirmation=true;}}>{text.createWorld}</button><button onclick={cancelPreview}>{text.cancel}</button>{/if}
</div>
{#if candidate}
  {@const metrics=candidate.overview.metrics}
  <div data-testid="empty-world-preview">
    {#if candidate.imagePreview}<img src={candidate.imagePreview} alt={text.heightmapImagePreview}/>{/if}
    <div class="form-actions" role="group" aria-label={text.worldPreviewMode}><button aria-pressed={previewMode==='topDown'} onclick={()=>{previewMode='topDown';}}>{text.previewTopDown}</button><button aria-pressed={previewMode==='isometric'} onclick={()=>{previewMode='isometric';}}>{text.previewIsometric}</button></div>
    <figure data-testid="world-overview" data-mode={previewMode}>
      <svg class="preview-compass" viewBox="0 0 144 86" role="img" aria-label={text.previewCompass}>
        {#each [{heading:0,label:'coastNorth'},{heading:2,label:'coastEast'},{heading:4,label:'coastSouth'},{heading:6,label:'coastWest'}] as direction}
          {@const vector=compassVector(previewMode,direction.heading as 0|2|4|6)}
          <line x1="72" y1="43" x2={72+vector.x} y2={43+vector.y} stroke="currentColor"/>
          <text x={72+vector.x*1.65} y={43+vector.y*1.35} text-anchor="middle" dominant-baseline="middle">{text[direction.label as 'coastNorth'|'coastEast'|'coastSouth'|'coastWest']}</text>
        {/each}
      </svg>
      <img src={previewImage} alt={previewMode==='topDown'?text.worldTopDownPreview:text.worldIsometricPreview}/>
      <figcaption>{interpolate(text.worldPreviewSampling,{step:candidate.overview.step})}</figcaption>
    </figure>
    <p data-testid="overview-metrics">{interpolate(text.realizedCoverage,{water:(100*metrics.wetCells/metrics.cells).toFixed(1),forest:(100*metrics.forestCells/metrics.cells).toFixed(1),min:metrics.minimumQuarterHeight/4,max:metrics.maximumQuarterHeight/4})}</p>
    <p data-testid="buildable-metrics">{interpolate(text.realizedBuildable,{flat:metrics.clearFlatCells.toLocaleString(locale),connected:metrics.largestBuildableArea.toLocaleString(locale)})}</p>
    <p class="hint">{text.buildableHelp}</p>
    <p>{candidate.name} · {candidate.dimensions.h} × {candidate.dimensions.v} × {candidate.dimensions.z}</p>
    <p>{text.worldWater}: {candidate.waterLevel} · {source==='empty'?text.zeroRails:text.noRails}</p>
    {#if candidate.geography}<p data-testid="geography-metrics">{interpolate(text.coastMetrics,{water:candidate.geography.metrics.waterPercent.toFixed(1),land:candidate.geography.metrics.landCells.toLocaleString(locale),seas:candidate.geography.metrics.seaComponents})}</p><p data-testid="water-metrics">{interpolate(text.waterMetrics,{lakes:candidate.geography.metrics.lakeComponents,lakeCells:candidate.geography.metrics.lakeCells,rivers:candidate.geography.metrics.riverCount})}</p>{/if}
    {#if candidate.geography?.forests}<p data-testid="forest-metrics">{interpolate(text.forestMetrics,{eligible:candidate.geography.forests.eligibleCells,mask:candidate.geography.forests.maskCells,forests:candidate.geography.forests.forestCells,trees:candidate.geography.forests.trees,reserved:candidate.geography.forests.reservedCells,flat:candidate.geography.forests.largestFlatArea})}</p><p>{text.forestLegend}</p>{/if}
    <p>{text.candidateBytes}: {candidate.saveBytes.toLocaleString(locale)} / {candidate.snapshotBytes.toLocaleString(locale)}</p>
  </div>
{/if}
{#if confirmation && candidate}
  <DecisionModal title={text.confirmEmptyWorld} oncancel={()=>{confirmation=false;}}>
    <p>{text.sharedReplacement}</p><p>{candidate.name} · {candidate.dimensions.h} × {candidate.dimensions.v} × {candidate.dimensions.z}</p>
    {#snippet actions()}<button class="primary" disabled={busy||disabled} onclick={commit}>{text.createWorld}</button><button onclick={()=>{confirmation=false;}}>{text.cancel}</button>{/snippet}
  </DecisionModal>
{/if}
{#if busy}<progress aria-label={phase==='commit'?text.committingWorld:text.preparingWorld}></progress>{/if}
<p aria-live="polite" data-testid="creation-feedback">{busy?(phase==='commit'?text.committingWorld:text.preparingWorld):feedback==='idle'?'':text[feedback]}</p>
<style>fieldset{border:0;padding:0;min-width:0}input{max-width:100%;box-sizing:border-box}img{width:100%;max-height:260px;object-fit:contain}figure{margin:0}figcaption{font-size:.85rem}svg.preview-compass{display:block;width:144px;height:86px;margin:auto}svg text{font-size:11px;fill:currentColor}progress{width:100%}select{max-width:100%}.coast-directions{display:flex;flex-wrap:wrap;gap:.75rem}.coast-directions label{display:flex;align-items:center;gap:.35rem;min-height:44px}</style>
