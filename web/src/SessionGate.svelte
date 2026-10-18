<script lang="ts">
  import { onMount, tick } from 'svelte';
  import type { ShellModel, ShellState } from './lib/shell-model.ts';
  import { messages, type Locale } from './lib/localization.ts';
  import { authorize } from './lib/remote-session-client.ts';
  const icon = '/assets/FTicon.png';
  let { model, view, renderFailed, onleave }: { model: ShellModel; view: ShellState; renderFailed: boolean; onleave: () => void } = $props();
  const text = $derived(messages[view.locale]);
  const signingIn = $derived(view.connection === 'accessRequired');
  let secret = $state(''), accessFailed = $state(false), authorizing = $state(false);
  let dialog: HTMLElement;
  let active = true;
  const title = $derived(view.leaving ? text.leavingGame : view.feedback === 'leaveFailed' ? text.leaveFailedTitle
    : signingIn ? (view.authRequiredAgain ? text.signInAgain : text.welcome)
    : view.connection === 'incompatible' ? text.incompatibleTitle
    : renderFailed ? text.renderFailedTitle
    : view.connection === 'disconnected' ? (view.read ? text.connectionLost : text.cannotReach)
    : view.connectionStage === 'bootstrap' && view.connection !== 'ready' ? text.connectingGame : text.loadingGame);
  onMount(() => {
    dialog.focus({ preventScroll: true });
    const keepFocus = (event: FocusEvent) => { if (!dialog.contains(event.target as Node)) dialog.focus({ preventScroll: true }); };
    document.addEventListener('focusin', keepFocus);
    return () => { active = false; secret = ''; document.removeEventListener('focusin', keepFocus); };
  });
  $effect(() => {
    void view.connection; void view.leaving; void renderFailed;
    void tick().then(() => { if (active && dialog && !dialog.contains(document.activeElement)) dialog.focus({ preventScroll: true }); });
  });
  function scrimPointer(event: PointerEvent) { if (event.target === event.currentTarget) { event.preventDefault(); dialog.focus({ preventScroll: true }); } }
  function key(event: KeyboardEvent) {
    if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); }
    if (event.key !== 'Tab') return;
    const controls = [...dialog.querySelectorAll<HTMLElement>('button:not(:disabled), input:not(:disabled), select:not(:disabled)')];
    const index = controls.indexOf(document.activeElement as HTMLElement);
    if (event.shiftKey && index <= 0) { event.preventDefault(); controls.at(-1)?.focus(); }
    else if (!event.shiftKey && (index === controls.length - 1 || index < 0)) { event.preventDefault(); controls[0]?.focus(); }
  }
  async function connect(event: SubmitEvent) {
    event.preventDefault(); if (authorizing) return;
    authorizing = true; accessFailed = false;
    const signedOut = view.signedOut;
    const accepted = await authorize(secret).catch(() => false);
    secret = '';
    if (!active || view.connection !== 'accessRequired' || view.leaving || view.signedOut !== signedOut) return;
    authorizing = false; accessFailed = !accepted;
    if (accepted) model.connectAuthorized();
  }
</script>

<div class="session-scrim" role="presentation" onpointerdown={scrimPointer} onpointerup={scrimPointer}>
  <div class="session-dialog" bind:this={dialog} role="dialog" aria-modal="true" aria-labelledby="session-title" tabindex="-1" onkeydown={key}>
    <div class="panel-title"><strong class="brand"><img src={icon} width="32" height="32" alt=""/>FreeTrain</strong></div>
    <div class="panel-body">
      <label class="field">{text.language}<select aria-label={text.language} value={view.locale} onchange={event => model.setLocale(event.currentTarget.value as Locale)}><option value="en">English</option><option value="ja">日本語</option></select></label>
      <h1 id="session-title">{title}</h1>
      <p data-testid="connection">{text[view.connection]}</p>
      {#if signingIn}
        {#if view.authRequiredAgain}<p>{text.authenticationRequired}</p>{/if}
        <form onsubmit={connect}>
          <label class="field">{text.operatorSecret}<input type="password" autocomplete="current-password" bind:value={secret} required={!view.anonymousAccess} disabled={authorizing}/></label>
          {#if view.anonymousAccess}<p class="hint">{text.localAccessHint}</p>{/if}
          {#if accessFailed}<p role="alert">{text.accessFailed}</p>{/if}
          <button type="submit" class="primary" disabled={authorizing}>{authorizing ? text.connectingGame : text.connect}</button>
        </form>
      {:else if view.feedback === 'leaveFailed'}
        <p>{text.leaveFailed}</p><button onclick={onleave}>{text.retryLeave}</button>
      {:else if view.connection === 'incompatible'}
        <p>{text.incompatible}</p><button onclick={() => location.reload()}>{text.reload}</button>
      {:else if view.connection === 'disconnected'}
        <p>{text.recoveryHint}</p>
        {#if view.retryAt}<p>{text.retryScheduled} {new Date(view.retryAt).toLocaleTimeString(view.locale)}</p>{/if}
        <button class="primary" onclick={model.reconnect}>{text.retryNow}</button>
      {:else if !view.leaving && !renderFailed}
        <p>{text.waitForSync}</p>
      {/if}
      {#if renderFailed}<p role="alert">{text.assetsFailed}</p><button onclick={() => location.reload()}>{text.reload}</button>{/if}
      <p role="status" aria-live="polite" aria-atomic="true">{text[view.feedback === 'idle' && view.connection === 'loading' ? 'loading' : view.feedback]}</p>
      {#if !view.leaving && view.feedback !== 'leaveFailed' && (view.role || view.authRequiredAgain)}<button disabled={authorizing} onclick={onleave}>{text.leaveGame}</button>{/if}
    </div>
  </div>
</div>
