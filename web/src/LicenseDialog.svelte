<script lang="ts">
  import DecisionModal from './DecisionModal.svelte';
  import { messages, type Locale } from './lib/localization.ts';
  import licenseText from '../../COPYING.txt?raw';
  let { locale, onclose }: { locale: Locale; onclose: () => void } = $props();
  const text = $derived(messages[locale]);
</script>

<DecisionModal title={text.license} oncancel={onclose} wide>
  <p><a href="https://github.com/rdmark/freetrain" target="_blank" rel="noopener noreferrer">{text.sourceCode}</a></p>
  <!-- Keyboard focus lets readers scroll the long license with arrow and Page Down keys. -->
  <!-- svelte-ignore a11y_no_noninteractive_tabindex -->
  <pre class="license-text" tabindex="0" role="region" aria-label="GNU Lesser General Public License 2.1">{licenseText}</pre>
  {#snippet actions()}
    <button onclick={onclose}>{text.closeDialog}</button>
  {/snippet}
</DecisionModal>

<style>
  .license-text { white-space: pre-wrap; overflow-wrap: anywhere; font: inherit; tab-size: 2; margin: 0; }
</style>
