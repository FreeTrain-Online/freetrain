<script lang="ts">
  import { tick, type Snippet } from 'svelte';
  import { messages, type Locale } from './lib/localization.ts';
  import { primaryAction } from './lib/primary-action.ts';
  let { title, closeLabel, onclose, children, footer, locale, primaryInContent = false }: { title: string; closeLabel: string; onclose: () => void; children: Snippet; footer?: Snippet; locale: Locale; primaryInContent?: boolean } = $props();
  let heading: HTMLHeadingElement;
  let expanded = $state(false);
  $effect(() => { void title; expanded = false; });
  $effect(() => { void title; void tick().then(() => heading?.focus({ preventScroll: true })); });
</script>
<div id="primary-panel" class="primary-panel" class:expanded role="dialog" aria-modal="false" aria-label={title} use:primaryAction>
  <div class="panel-title"><h2 bind:this={heading} tabindex="-1">{title}</h2><div class="panel-actions"><button class="sheet-toggle" aria-expanded={expanded} aria-controls="panel-content" onclick={() => { expanded = !expanded; }}>{messages[locale][expanded ? 'collapsePanel' : 'expandPanel']}</button><button class:primary={!primaryInContent} aria-label={closeLabel} onclick={onclose}>×</button></div></div>
  <div id="panel-content" class="panel-body">{@render children()}</div>
  {#if footer}<div class="panel-footer form-actions">{@render footer()}</div>{/if}
</div>
