<script lang="ts">
  import { tick, type Snippet } from 'svelte';
  let { title, closeLabel, onclose, children, footer }: { title: string; closeLabel: string; onclose: () => void; children: Snippet; footer?: Snippet } = $props();
  let heading: HTMLHeadingElement;
  $effect(() => { void title; void tick().then(() => heading?.focus({ preventScroll: true })); });
</script>
<section id="primary-panel" class="primary-panel" aria-label={title}>
  <div class="panel-title"><h2 bind:this={heading} tabindex="-1">{title}</h2><button aria-label={closeLabel} onclick={onclose}>×</button></div>
  <div class="panel-body">{@render children()}</div>
  {#if footer}<div class="panel-footer form-actions">{@render footer()}</div>{/if}
</section>
