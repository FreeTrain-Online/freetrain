<script lang="ts">
  import { onMount, type Snippet } from 'svelte';
  import { primaryAction } from './lib/primary-action.ts';
  let { title, oncancel, children, actions, wide = false, leftAligned = false }: { title: string; oncancel: () => void; children: Snippet; actions: Snippet; wide?: boolean; leftAligned?: boolean } = $props();
  let dialog: HTMLDialogElement;
  onMount(() => {
    const origin = document.activeElement as HTMLElement | null;
    dialog.showModal();
    dialog.focus({ preventScroll: true });
    return () => {
      dialog.close();
      const target = origin?.isConnected && origin !== document.body && !origin.closest('[inert]') ? origin
        : document.querySelector<HTMLElement>('.primary-panel h2') ?? document.querySelector<HTMLElement>('.command-bar button') ?? document.querySelector('canvas');
      target?.focus({ preventScroll: true });
    };
  });
  function key(event: KeyboardEvent) {
    event.stopPropagation();
    if (event.key === 'Escape') { event.preventDefault(); oncancel(); }
    if (event.key === 'Tab') {
      const controls = [...dialog.querySelectorAll<HTMLElement>('button:not(:disabled),input:not(:disabled),select:not(:disabled),a[href],[tabindex="0"]')];
      const index = controls.indexOf(document.activeElement as HTMLElement);
      if (event.shiftKey && index <= 0) { event.preventDefault(); controls.at(-1)?.focus(); }
      else if (!event.shiftKey && (index < 0 || index === controls.length - 1)) { event.preventDefault(); controls[0]?.focus(); }
    }
  }
</script>
<dialog class="decision-dialog" class:wide class:left-aligned={leftAligned} bind:this={dialog} use:primaryAction aria-label={title} tabindex="-1" onkeydown={key} oncancel={event => { event.preventDefault(); oncancel(); }} onpointerdown={event => { if (event.target === dialog) event.preventDefault(); }}>
  <div class="panel-title"><h2>{title}</h2></div>
  <div class="panel-body">{@render children()}</div>
  <div class="panel-footer form-actions">{@render actions()}</div>
</dialog>
