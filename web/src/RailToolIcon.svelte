<script lang="ts">let {slope=false,bridge=false}:{slope?:boolean;bridge?:boolean}=$props();</script>
<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false">
  {#if bridge}
    <path d="M2 14h20M3 14V5l6 9 6-9 6 9V5M3 5h18M5 16v5m14-5v5" />
  {:else if slope}
    <path d="M2 16h4L18 6h4M2 19h4L18 9h4" />
    <path d="M10 16v5M15 12v9M20 9v12M2 21h20" />
  {:else}
    <path d="M8.3 6h7.4M7.4 10h9.2M6.3 15h11.4M5.2 20h13.6" />
    <path d="M9.5 3 6 21M14.5 3 18 21" />
  {/if}
</svg>

<style>
  svg { display: block; width: 24px; height: 24px; margin: auto; }
</style>
