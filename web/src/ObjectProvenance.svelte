<script lang="ts">
  import { bundledCatalog } from './lib/bundled-catalog.ts';
  import { bundledPackId, objectOrigin, type ObjectOrigin } from './lib/object-provenance.ts';
  import { messages, type Locale } from './lib/localization.ts';
  let { id, packId = bundledPackId, locale }: { id: string; packId?: string; locale: Locale } = $props();
  const text = $derived(messages[locale]);
  let origin = $state<ObjectOrigin>(), failed = $state(false);
  $effect(() => {
    const selected = id, pack = packId; let active = true;
    origin = undefined; failed = false;
    if (pack === bundledPackId) {
      void bundledCatalog().then(catalog => {
        if (active) origin = objectOrigin(catalog, selected);
      }).catch(() => { if (active) failed = true; });
    }
    return () => { active = false; };
  });
</script>
{#if origin}
  <span class="object-provenance">
    <span data-testid="object-author">{text.objectAuthor}: {origin.author ?? text.metadataNotSpecified}</span>
    <span data-testid="object-plugin">{text.objectPlugin}: {origin.plugin ?? text.metadataNotSpecified}</span>
  </span>
{:else if failed}
  <span class="object-provenance">{text.objectMetadataUnavailable}</span>
{/if}
