<script lang="ts">
  import DecisionModal from './DecisionModal.svelte';
  import { appName, messages, type Locale } from './lib/localization.ts';
  let { locale, onclose }: { locale: Locale; onclose: () => void } = $props();
  const text = $derived(messages[locale]);
</script>

<DecisionModal title={text.credits} oncancel={onclose}>
  <h3>{text.originalAuthor}</h3>
  <p>{text.originalAuthorName}</p>
  <p><a href="http://www.kohsuke.org/freetrain/" target="_blank" rel="noopener noreferrer">FreeTrain — kohsuke.org</a></p>
  <h3>{text.originalContributors}</h3>
  <p>{text.originalCommunity}</p>
  <p>{text.originalContributorNames}</p>
  <p>{text.communityThanks}</p>
  <h3>{text.englishVersion}</h3>
  <p>{text.englishContributorNames}</p>
  <p><a href="https://sourceforge.net/projects/freetrain/" target="_blank" rel="noopener noreferrer">FreeTrain — SourceForge</a></p>
  <h3>{appName}</h3>
  <p>{text.onlineContributorNames}</p>
  {#snippet actions()}
    <button onclick={onclose}>{text.closeDialog}</button>
  {/snippet}
</DecisionModal>
