<script lang="ts">
  import { messages, type Locale } from './lib/localization.ts';
  import { schemes, type Scheme } from './lib/ui-preferences.ts';
  let { locale, scheme, onchange }: { locale: Locale; scheme: Scheme; onchange: (value: Scheme) => void } = $props();
  const text = $derived(messages[locale]);
</script>
<label class="field">{text.colorScheme}<select aria-label={text.colorScheme} value={scheme} onchange={event => onchange(event.currentTarget.value as Scheme)}>
  {#each schemes as id}<option value={id}>{text[id]}</option>{/each}
</select></label>
