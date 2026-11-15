<script lang="ts">
  import { catalogPreview } from './lib/catalog-preview.ts';
  import type { SignalDirection } from './lib/building-catalog.ts';
  import type {CityCatalogChoice} from './lib/fixed-city-edit.ts';
  let { id, direction, choice, unavailable }: { id: string; direction?: SignalDirection; choice?:CityCatalogChoice; unavailable: string } = $props();
  let src = $state(''), failed = $state(false);
  $effect(() => {
    const current = id, heading = direction, selected=choice; let active = true; src = ''; failed = false;
    void catalogPreview(current, heading,selected).then(value => { if (active) src = value; }).catch(() => { if (active) failed = true; });
    return () => { active = false; };
  });
</script>
<span class="catalog-preview" data-preview-id={id} data-direction={choice?.direction??direction} data-height={choice?.height} data-side={choice?.side} data-color={choice?.color} aria-hidden="true">
  {#if src}<img {src} alt="" width="96" height="80"/>{:else}<span title={failed ? unavailable : undefined}>{failed ? '—' : '…'}</span>{/if}
</span>
