<script lang="ts">
  import { catalogPreview } from './lib/catalog-preview.ts';
  import type { SignalDirection } from './lib/building-catalog.ts';
  let { id, direction, unavailable }: { id: string; direction?: SignalDirection; unavailable: string } = $props();
  let src = $state(''), failed = $state(false);
  $effect(() => {
    const current = id, heading = direction; let active = true; src = ''; failed = false;
    void catalogPreview(current, heading).then(value => { if (active) src = value; }).catch(() => { if (active) failed = true; });
    return () => { active = false; };
  });
</script>
<span class="catalog-preview" data-preview-id={id} data-direction={direction} aria-hidden="true">
  {#if src}<img {src} alt="" width="96" height="80"/>{:else}<span title={failed ? unavailable : undefined}>{failed ? '—' : '…'}</span>{/if}
</span>
