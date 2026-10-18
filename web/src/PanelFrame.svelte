<script lang="ts">
  import { onMount, type Snippet } from 'svelte';
  let { title, closeLabel, onclose, children }: { title: string; closeLabel: string; onclose: () => void; children: Snippet } = $props();
  let heading: HTMLHeadingElement;
  onMount(() => { heading.focus({ preventScroll: true }); });
</script>
<section class="primary-panel" aria-label={title}>
  <div class="panel-title"><h2 bind:this={heading} tabindex="-1">{title}</h2><button aria-label={closeLabel} onclick={onclose}>×</button></div>
  <div class="panel-body">{@render children()}</div>
</section>
