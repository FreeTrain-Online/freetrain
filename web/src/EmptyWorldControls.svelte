<script lang="ts">
  import { onDestroy } from 'svelte';
  import DecisionModal from './DecisionModal.svelte';
  import { messages, type Locale } from './lib/localization.ts';
  type Candidate = { id: string; requestHash: string; candidateHash: string; name: string; dimensions: { h: number; v: number; z: number }; waterLevel: number; saveBytes: number; snapshotBytes: number; expiresAt: string };
  let { locale, epoch, revision, disabled, onbusy, onmodal }: { locale: Locale; epoch?: string; revision?: string; disabled: boolean; onbusy: (busy: boolean) => void; onmodal: () => void } = $props();
  const text = $derived(messages[locale]);
  let name = $state(''), h = $state<number|undefined>(30), v = $state<number|undefined>(42), z = $state<number|undefined>(6), water = $state<number|undefined>(1);
  let candidate = $state<Candidate>();
  let capturedEpoch = $state<string>(), capturedRevision = $state<string>();
  let busy = $state(false), confirmation = $state(false);
  let feedback = $state<'creationFailed'|'staleCandidate'|'creationBusy'|'created'|'idle'>('idle');
  let generation = 0;
  const valid = $derived([h,v,z,water].every(Number.isInteger) && h!>=1 && h!<=64 && v!>=1 && v!<=64 && z!>=1 && z!<=64 && water!>=0 && water!<z! && name.length<=128 && !/[\u0000-\u001f\u007f-\u009f]/.test(name));
  async function call(path: string, body: unknown) {
    const response = await fetch(`/api/worlds/empty/${path}`, { method:'POST', credentials:'same-origin', headers:{'Content-Type':'application/json'}, body:JSON.stringify(body), signal:AbortSignal.timeout(35000) });
    if (!response.ok) {
      const result = await response.json().catch(()=>({}));
      throw new Error(result.code === 'creationBusy' ? 'creationBusy' : ['staleEpoch','staleRevision','staleCandidate'].includes(result.code) ? 'staleCandidate' : 'creationFailed');
    }
    return response.json();
  }
  function invalidate() {
    generation++; confirmation=false;
    const old=candidate; candidate=undefined;
    if(old)void call('cancel',old.id).catch(()=>{});
  }
  onDestroy(invalidate);
  $effect(()=>{ if(candidate && (disabled || epoch!==capturedEpoch || revision!==capturedRevision || Date.now()>=Date.parse(candidate.expiresAt))) { invalidate(); feedback='staleCandidate'; } });
  $effect(()=>{ if(candidate) { const timer=setTimeout(()=>{invalidate();feedback='staleCandidate';},Math.max(0,Date.parse(candidate.expiresAt)-Date.now()));return()=>clearTimeout(timer); } });
  async function preview() {
    if(busy||disabled||!valid)return;
    invalidate(); const token=generation, e=epoch, r=revision;
    busy=true;onbusy(true);feedback='idle';
    try {
      const result:Candidate=await call('preview',{name:name || text.unnamedWorld,dimensions:{h,v,z},waterLevel:water,epoch:e,revision:r});
      if(token!==generation||epoch!==e||revision!==r||disabled){void call('cancel',result.id).catch(()=>{});feedback='staleCandidate';return;}
      capturedEpoch=e;capturedRevision=r;candidate=result;
    }catch(error){feedback=error instanceof Error && error.message==='creationBusy'?'creationBusy':error instanceof Error && error.message==='staleCandidate'?'staleCandidate':'creationFailed';}
    finally{busy=false;onbusy(false);}
  }
  async function commit() {
    if(busy||disabled||!candidate||epoch!==capturedEpoch||revision!==capturedRevision)return;
    const retained=candidate;confirmation=false;busy=true;onbusy(true);
    // A failed/uncertain HTTP outcome is never replayed. A new preview is required.
    candidate=undefined;generation++;
    try {await call('commit',{id:retained.id,requestHash:retained.requestHash,candidateHash:retained.candidateHash,epoch:capturedEpoch,revision:capturedRevision});feedback='created';}
    catch(error){feedback=error instanceof Error && error.message==='staleCandidate'?'staleCandidate':'creationFailed';void call('cancel',retained.id).catch(()=>{});}
    finally{busy=false;onbusy(false);}
  }
</script>
<p class="hint">{text.emptyWorldHelp}</p>
<fieldset disabled={busy || disabled} oninput={invalidate} onchange={invalidate}>
  <label class="field">{text.worldName}<input maxlength="128" bind:value={name} placeholder={text.unnamedWorld}/></label>
  <label class="field">{text.worldH}<input type="number" min="1" max="64" step="1" bind:value={h}/></label>
  <label class="field">{text.worldV}<input type="number" min="1" max="64" step="1" bind:value={v}/></label>
  <label class="field">{text.worldZ}<input type="number" min="1" max="64" step="1" bind:value={z}/></label>
  <label class="field">{text.worldWater}<input type="number" min="0" max={(z??1)-1} step="1" bind:value={water}/></label>
  <button onclick={()=>{invalidate();h=30;v=42;z=6;water=1;}}>{text.sourceDefaults}</button>
</fieldset>
<div class="form-actions">
  <button disabled={busy||disabled||!valid} onclick={preview}>{text.previewWorld}</button>
  {#if candidate}<button disabled={busy||disabled} onclick={()=>{onmodal();confirmation=true;}}>{text.createWorld}</button><button onclick={invalidate}>{text.cancel}</button>{/if}
</div>
{#if candidate}
  <div data-testid="empty-world-preview">
    <svg viewBox="0 0 240 140" role="img" aria-label={text.emptyPreviewLabel}><polygon points={`120,10 ${120+100*candidate.dimensions.h/64},${10+60*candidate.dimensions.h/64} ${120+100*(candidate.dimensions.h-candidate.dimensions.v)/64},${10+60*(candidate.dimensions.h+candidate.dimensions.v)/64} ${120-100*candidate.dimensions.v/64},${10+60*candidate.dimensions.v/64}`} fill="#6c9c55" stroke="currentColor"/></svg>
    <p>{candidate.name} · {candidate.dimensions.h} × {candidate.dimensions.v} × {candidate.dimensions.z}</p>
    <p>{text.worldWater}: {candidate.waterLevel} · {text.zeroRails}</p>
    <p>{text.candidateBytes}: {candidate.saveBytes.toLocaleString(locale)} / {candidate.snapshotBytes.toLocaleString(locale)}</p>
  </div>
{/if}
{#if confirmation && candidate}
  <DecisionModal title={text.confirmEmptyWorld} oncancel={()=>{confirmation=false;}}>
    <p>{text.sharedReplacement}</p><p>{candidate.name} · {candidate.dimensions.h} × {candidate.dimensions.v} × {candidate.dimensions.z}</p>
    {#snippet actions()}<button class="primary" disabled={busy||disabled} onclick={commit}>{text.createWorld}</button><button onclick={()=>{confirmation=false;}}>{text.cancel}</button>{/snippet}
  </DecisionModal>
{/if}
<p aria-live="polite" data-testid="creation-feedback">{busy?text.preparingWorld:feedback==='idle'?'':text[feedback]}</p>
<style>fieldset{border:0;padding:0;min-width:0}input{max-width:100%;box-sizing:border-box}svg{width:100%;max-height:150px}</style>
