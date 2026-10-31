<script lang="ts">
  import DecisionModal from './DecisionModal.svelte';
  import { messages, type Locale } from './lib/localization.ts';
  import licenseText from '../../content/bundled-v2/source/trunk/doc/COPYING.txt?raw';
  import stbImageSharpLicense from '../../doc/notices/StbImageSharp/LICENSE.txt?raw';
  const licensePreamble = `FreeTrain itself is licensed under the LGPL. All use and redistribution are
subject to this license. Individual plugins may have different licensing
agreements, but unless specifically noted, please assume LGPL.

    FreeTrain
    Copyright (C) 2002, Kohsuke Kawaguchi et al.
    FreeTrainEx
    Copyright (C) 2005, C477 et al.
    FreeTrainEx-Aver
    Copyright (C) 2008, FreeTrainEx-Aver Project et al.
    FreeTrain Online
    Copyright (C) 2026, Daniel Markstedt <daniel@mindani.net>

    This library is free software; you can redistribute it and/or
    modify it under the terms of the GNU Lesser General Public
    License as published by the Free Software Foundation; either
    version 2.1 of the License, or any later version.

    This library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
    Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public
    License along with this library; if not, write to the Free Software
    Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA`;
  let { locale, onclose }: { locale: Locale; onclose: () => void } = $props();
  const text = $derived(messages[locale]);
</script>

<DecisionModal title={text.license} oncancel={onclose} wide>
  <p><a href="https://github.com/rdmark/freetrain" target="_blank" rel="noopener noreferrer">{text.sourceCode}</a></p>
  <pre class="license-text">{licensePreamble}</pre>
  <hr />
  <!-- Keyboard focus lets readers scroll the long license with arrow and Page Down keys. -->
  <!-- svelte-ignore a11y_no_noninteractive_tabindex -->
  <pre class="license-text" tabindex="0" role="region" aria-label="GNU Lesser General Public License 2.1">{licenseText}</pre>
  <hr />
  <pre class="license-text">{stbImageSharpLicense}</pre>
  {#snippet actions()}
    <button class="primary" onclick={onclose}>{text.closeDialog}</button>
  {/snippet}
</DecisionModal>

<style>
  .license-text { white-space: pre-wrap; overflow-wrap: anywhere; font: inherit; tab-size: 2; margin: 0; }
</style>
