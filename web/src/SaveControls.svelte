<script lang="ts">
  import DecisionModal from './DecisionModal.svelte';
  import { messages, type Locale } from './lib/localization.ts';
  import { downloadSave, listSaveSlots, saveRequest, SaveError, type SaveFeedback, type SaveSlot } from './lib/save-transfer.ts';
  let { locale, epoch, disabled, onbusy, onmodal, slot = $bindable('slot1') }: { locale: Locale; epoch?: string; disabled: boolean; onbusy: (busy: boolean) => void; onmodal: () => void; slot?: string } = $props();
  const text = $derived(messages[locale]);
  let feedback = $state<SaveFeedback>('idle');
  let busy = $state(false);
  let slots = $state<SaveSlot[]>([]);

  let confirmLoad = $state(false);
  let upload: File | undefined = $state();
  let confirmationEpoch = $state<string>();
  const selected = $derived(slots.find(item => item.id === slot));
  const refresh = () => listSaveSlots().then(value => { slots = value; }).catch(() => { feedback = 'saveFailed'; });
  $effect(() => { if (confirmLoad && (disabled || epoch !== confirmationEpoch)) { feedback = epoch !== confirmationEpoch ? 'staleEpoch' : 'saveFailed'; confirmLoad = false; upload = undefined; } });
  function confirm() { confirmationEpoch = epoch; onmodal(); confirmLoad = true; }
  const cancel = () => { confirmLoad = false; upload = undefined; };
  $effect(() => { if (epoch && !disabled) { void refresh(); } });
  async function act(action: () => Promise<void>, outcome: SaveFeedback) {
    if (busy || disabled) return;
    busy = true; onbusy(true); feedback = 'saving'; confirmLoad = false;
    try { await action(); feedback = outcome; await refresh(); }
    catch (error) { feedback = error instanceof SaveError ? error.code : 'saveFailed'; }
    finally { busy = false; onbusy(false); upload = undefined; }
  }
</script>
<div class="save-controls">
  <p class="hint">{text.loadHint}</p><p class="hint" id="saves-help">{text.savesHint}</p>
  <div class="form-actions" aria-describedby="saves-help">
    <label>{text.saveSlot}<select aria-label={text.saveSlot} bind:value={slot} disabled={busy || disabled} onchange={() => { confirmLoad = false; upload = undefined; }}>
      {#each ['slot1','slot2','slot3','checkpoint'] as id}<option value={id}>{id === 'checkpoint' ? text.checkpoint : `${text.saveSlot} ${id.slice(-1)}`}{slots.find(item => item.id === id)?.exists ? '' : ` (${text.emptySlot})`}</option>{/each}
    </select></label>
    <button disabled={busy || disabled || slot === 'checkpoint'} onclick={() => act(() => saveRequest(`${slot}/save`, epoch!), 'saved')}>{text.saveGame}</button>
    <button disabled={busy || disabled || !selected?.exists} onclick={() => { upload = undefined; confirm(); }}>{text.loadGame}</button>
    <button disabled={busy || disabled} onclick={() => act(downloadSave, 'saved')}>{text.downloadSave}</button>
    <label class="field">{text.uploadSave}<input type="file" accept=".json,application/json" disabled={busy || disabled} onchange={event => {
      upload = event.currentTarget.files?.[0]; if (upload) confirm(); event.currentTarget.value = '';
    }} /></label>
  </div>
  {#if confirmLoad}
    <DecisionModal title={text.replaceWorld} oncancel={cancel}>
      <p>{text.sharedReplacement}</p><p>{upload?.name ?? (slot === 'checkpoint' ? text.checkpoint : `${text.saveSlot} ${slot.slice(-1)}`)}</p>
      {#snippet actions()}
        <button class="primary" disabled={busy || disabled || epoch !== confirmationEpoch} onclick={() => act(() => saveRequest(upload ? 'upload' : `${slot}/load`, confirmationEpoch!, upload), 'loaded')}>{text.confirmLoad}</button>
        <button onclick={cancel}>{text.cancel}</button>
      {/snippet}
    </DecisionModal>
  {/if}
  <p data-testid="save-feedback" aria-live="polite">{feedback === 'idle' ? '' : text[feedback]}</p>
</div>
