<script lang="ts">
  import {onMount,onDestroy,tick} from 'svelte';
  import {messages} from './lib/localization.ts';
  import {drivingTrain} from './lib/manual-driving.ts';
  import type {ShellModel,ShellState} from './lib/shell-model.ts';
  let {model,view,trainId,disabled=false}:{model:ShellModel;view:ShellState;trainId:string;disabled?:boolean}=$props();
  const text=$derived(messages[view.locale]);
  const train=$derived(drivingTrain(view.read,trainId));
  const blocked=$derived(disabled||view.pending||view.connection!=='ready'||view.role==='observer');
  const owned=$derived(!!train?.controlLease&&train.controlLease.participantId===view.participantId);
  const enabled=$derived(!blocked&&owned&&train?.controller==='manual'&&'placed' in train&&train.placed);
  const controls=[['ArrowLeft','manualLeft'],['ArrowRight','manualRight'],['Shift','manualPass'],['ArrowUp','manualDepart'],['ArrowDown','manualReverse']] as const;
  const keys=new Set<string>(),pointers=new Map<number,string>();
  let held=$state(''),context:string|undefined,active=true;
  let root:HTMLElement;
  let dirty=false,sending=false;
  async function flush(){dirty=true;if(sending)return;sending=true;
    try{while(dirty){dirty=false;await model.input(new Set(keys),trainId);}}finally{sending=false;}}
  function reset(){const had=keys.size>0;keys.clear();pointers.clear();held='';if(had)void flush();}
  async function restoreFocus(target:HTMLElement,id:string,epoch:string|undefined){
    await tick();
    if(active&&trainId===id&&view.read?.worldEpoch===epoch&&document.activeElement===document.body)
      target.focus({preventScroll:true});
  }
  async function takeControl(event:MouseEvent){
    const target=event.currentTarget as HTMLButtonElement,id=trainId,epoch=view.read?.worldEpoch;
    if(await model.acquireControl(id))await restoreFocus(target,id,epoch);
  }
  async function configure(event:Event){
    const select=event.currentTarget as HTMLSelectElement,value=select.value as 'automatic'|'manual';
    const id=trainId,epoch=view.read?.worldEpoch;select.value=train?.controller??'automatic';reset();
    if(await model.controller(value,id,()=>active&&trainId===id&&view.read?.worldEpoch===epoch))
      await restoreFocus(select,id,epoch);
  }
  function change(key:string,down:boolean){if(!enabled)return;if(down)keys.add(key);else keys.delete(key);held=[...keys].join(' + ');void flush();}
  function press(event:PointerEvent,key:string){if(!enabled||event.button!==0)return;event.preventDefault();
    const button=event.currentTarget as HTMLButtonElement;button.focus();button.setPointerCapture(event.pointerId);pointers.set(event.pointerId,key);change(key,true);}
  function release(event:PointerEvent){const key=pointers.get(event.pointerId);if(!key)return;pointers.delete(event.pointerId);if(![...pointers.values()].includes(key))change(key,false);}
  function keyboard(event:KeyboardEvent,down:boolean,buttonKey:string){
    if(event.isComposing||event.keyCode===229||(down&&(event.altKey||event.ctrlKey||event.metaKey))||!enabled)return;
    if(event.key==='Escape'){reset();return;}
    if(event.key===' '||event.key==='Enter'){event.preventDefault();if(!event.repeat)change(buttonKey,down);return;}
    if(!controls.some(c=>c[0]===event.key)||event.repeat)return;
    event.preventDefault();change(event.key,down);
  }
  $effect(()=>{const next=`${view.read?.worldEpoch}/${trainId}/${train?.controller}/${train?.controlLease?.leaseId}/${view.connection}/${enabled}`;
    if(context!==next){reset();context=next;}});
  onMount(()=>{let focused=document.hasFocus();
    const timer=setInterval(()=>{if(keys.size)void flush();},150);
    // Neutral input renews ownership without extending a held command. Only
    // the focused driving surface renews, so it cannot clear another surface's keys.
    const idle=setInterval(()=>{if(enabled&&focused&&!document.hidden&&root.contains(document.activeElement)&&!keys.size)
      void model.input(new Set(),trainId);},1000);
    const blur=()=>{focused=false;reset();},focus=()=>{focused=true;};
    const visibility=()=>{focused=!document.hidden&&document.hasFocus();reset();};
    window.addEventListener('blur',blur);window.addEventListener('focus',focus);document.addEventListener('visibilitychange',visibility);
    return()=>{clearInterval(timer);clearInterval(idle);window.removeEventListener('blur',blur);window.removeEventListener('focus',focus);document.removeEventListener('visibilitychange',visibility);};});
  onDestroy(()=>{active=false;reset();});
</script>
<section bind:this={root} class="fleet-manual-controls" aria-label={text.manual}>
  <p class="control-holder" data-testid="fleet-control-holder">{text.control}: {train?.controlLease?.participantId??text.unclaimed}</p>
  <div class="form-actions">
    <button disabled={blocked} onclick={takeControl}>{text.takeControl}</button>
    <button disabled={blocked||!owned} onclick={()=>{reset();void model.releaseControl(trainId);}}>{text.releaseControl}</button>
  </div>
  <label class="field">{text.controller}<select aria-label={text.controller} disabled={blocked} value={train?.controller}
    onchange={configure}>
    <option value="automatic">{text.automatic}</option><option value="manual">{text.manual}</option>
    {#if train?.controller==='timetable'}<option value="timetable" disabled>{text.manualOtherController}</option>{/if}
  </select></label>
  <div class="manual-buttons" role="group" aria-label={text.manual} onfocusout={event=>{if(!(event.relatedTarget instanceof Node)||!event.currentTarget.contains(event.relatedTarget))reset();}}>
    {#each controls as [key,label]}
      <button type="button" disabled={!enabled} aria-pressed={held.split(' + ').includes(key)}
        onpointerdown={event=>press(event,key)} onpointerup={release} onpointercancel={reset} onlostpointercapture={release}
        onkeydown={event=>keyboard(event,true,key)} onkeyup={event=>keyboard(event,false,key)}>{text[label]}</button>
    {/each}
  </div>
  <span class="hint" data-testid="fleet-manual-input" aria-live="polite">{held||text.keysReleased}</span>
  <details><summary>{text.help}</summary><p class="hint">{text.manualFleetHelp}</p></details>
</section>
<style>
  .manual-buttons{display:flex;flex-wrap:wrap;gap:.4rem;margin:.5rem 0;}
  .manual-buttons button{min-height:44px;touch-action:none;user-select:none;}
  .manual-buttons button[aria-pressed="true"]{outline:2px solid var(--accent,#e7bd70);}
</style>
