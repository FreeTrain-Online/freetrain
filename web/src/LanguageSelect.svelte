<script lang="ts">
  import { locales, messages, type Locale } from './lib/localization.ts';
  let { locale, onchange }: { locale: Locale; onchange: (value: Locale) => void } = $props();
</script>

<label class="field">{messages[locale].language}<select aria-label={messages[locale].language} value={locale} onchange={event => onchange(event.currentTarget.value as Locale)}>
  {#each Object.entries(locales) as [id, definition]}
    <option value={id} lang={id} dir={definition.direction}>{definition.label}</option>
  {/each}
</select></label>
