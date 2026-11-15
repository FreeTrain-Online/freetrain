<script lang="ts">
  import { messages, type Locale } from './lib/localization.ts';
  import { catalogCategories, catalogItems, signalDirections, type SignalDirection, type CatalogCategory } from './lib/building-catalog.ts';
  import { bundledCatalog } from './lib/bundled-catalog.ts';
  import {cityCatalogChoice,type CityCatalogChoice} from './lib/fixed-city-edit.ts';
  import CatalogPreview from './CatalogPreview.svelte';
  import ObjectProvenance from './ObjectProvenance.svelte';
  let { locale, category, selections, signalDirection, cityChoices, queries, cityFilter, pending, onquery, onfilter, oncategory, onselect, onclose }:
    { locale: Locale; category: CatalogCategory; selections: Record<CatalogCategory, string>; signalDirection: SignalDirection; pending: boolean;cityChoices:Record<string,CityCatalogChoice>;queries:Record<CatalogCategory,string>;cityFilter:{group:string;subgroup:string};onquery:(category:CatalogCategory,query:string)=>void;onfilter:(group:string,subgroup?:string)=>void;
      oncategory: (category: CatalogCategory) => void; onselect: (category: CatalogCategory, id: string, direction?: SignalDirection) => void; onclose: () => void } = $props();
  const text = $derived(messages[locale]);
  let expanded = $state(false);
  let content = $state<Awaited<ReturnType<typeof bundledCatalog>>>();
  $effect(() => { let active = true; void bundledCatalog().then(value => { if (active) content = value; }); return () => { active = false; }; });
  const items = $derived(catalogItems(category, locale, content,cityChoices));
  const groups=$derived([...new Map(items.flatMap(i=>i.variants).map(v=>[v.sourceGroup??'',v.group||text.cityUngrouped])).entries()]);
  const subgroups=$derived([...new Map(items.flatMap(i=>i.variants).filter(v=>cityFilter.group==='*'||v.sourceGroup===cityFilter.group).map(v=>[v.sourceSubgroup??'',v.subgroup||text.cityUngrouped])).entries()]);
  const visible = $derived(items.filter(item => (category!=='city'||item.variants.some(v=>(cityFilter.group==='*'||v.sourceGroup===cityFilter.group)&&(cityFilter.subgroup==='*'||v.sourceSubgroup===cityFilter.subgroup)))&&`${item.title} ${item.variants.map(v => `${v.title} ${v.detail} ${v.search??''}`).join(' ')}`.toLocaleLowerCase(locale).includes(queries[category].trim().toLocaleLowerCase(locale))));
</script>
<aside id="building-catalog" class="building-catalog" class:catalog-expanded={expanded} aria-label={text.buildings}>
  <div class="panel-title">
    <h2 tabindex="-1">{text.buildings}</h2>
    <div class="panel-actions">
      <button class="sheet-toggle" aria-expanded={expanded} aria-controls="catalog-content" onclick={() => { expanded = !expanded; }}>{text[expanded ? 'collapsePanel' : 'expandPanel']}</button>
      <button aria-label={text.closeCatalog} onclick={onclose}>×</button>
    </div>
  </div>
  <div class="catalog-filters">
    <label class="field">{text.catalogCategory}<select aria-label={text.catalogCategory} value={category} onchange={event => oncategory(event.currentTarget.value as CatalogCategory)}>{#each catalogCategories as item}<option value={item.id}>{text[item.label]}</option>{/each}</select></label>
    <label class="field">{text.catalogSearch}<input type="search" value={queries[category]} oninput={event=>onquery(category,event.currentTarget.value)}/></label>
    {#if category==='city'}
    <label class="field">{text.citySourceGroup}<select aria-label={text.citySourceGroup} value={cityFilter.group} onchange={event=>onfilter(event.currentTarget.value)}><option value="*">{text.cityAllGroups}</option>{#each groups as [value,label]}<option {value}>{label}</option>{/each}</select></label>
    <label class="field">{text.citySourceSubgroup}<select aria-label={text.citySourceSubgroup} value={cityFilter.subgroup} onchange={event=>onfilter(cityFilter.group,event.currentTarget.value)}><option value="*">{text.cityAllGroups}</option>{#each subgroups as [value,label]}<option {value}>{label}</option>{/each}</select></label>
    {/if}
  </div>
  <p class="catalog-count" role="status">{text.catalogResults.replace('{count}', visible.length.toLocaleString(locale)).replace('{total}', items.length.toLocaleString(locale))}</p>
  <div id="catalog-content" class="catalog-content">
    <ul class="catalog" aria-label={text.buildings}>
      {#each visible as item (item.id)}
        {@const active = item.variants.some(variant => variant.id === selections[category])}
        {@const chosen = item.variants.find(variant => variant.id === selections[category]) ?? item.variants[0]}
        <li class="catalog-item" class:catalog-active={active} data-variant={chosen.id}>
          <button class="catalog-item-main" aria-label={item.variants.length === 1 ? chosen.title : item.title} aria-pressed={active} data-catalog-variant={item.variants.length === 1 ? chosen.id : undefined} disabled={pending} onclick={() => onselect(category, chosen.id)}>
            <CatalogPreview id={chosen.id} choice={category==='city'?cityCatalogChoice(chosen.id,cityChoices):undefined} direction={category === 'signal' ? active ? signalDirection : 0 : undefined} unavailable={text.catalogPreviewUnavailable}/>
            <span><strong>{item.variants.length === 1 ? chosen.title : item.title}</strong><small>{chosen.detail}</small><small>{text.price}: ¥{chosen.price.toLocaleString(locale)}</small><small><ObjectProvenance id={chosen.id} {locale}/></small></span>
          </button>
          {#if item.variants.length > 1}
            <div class="catalog-options" role="group" aria-label={`${item.title} · ${text.catalogVariants}`}>
              <div class="catalog-option-buttons">
                {#each item.variants as variant (variant.id)}
                  <button aria-label={variant.title} title={`${variant.title} · ${variant.detail}`} aria-pressed={active && chosen.id === variant.id} data-catalog-variant={variant.id} disabled={pending} onclick={() => onselect(category, variant.id)}>{variant.label}</button>
                {/each}
              </div>
            </div>
          {/if}
          {#if category === 'signal'}
            <div class="catalog-options" role="group" aria-label={`${item.title} · ${text.signalDirection}`}>
              <span class="hint">{text.signalDirection}</span>
              <div class="catalog-option-buttons">
                {#each signalDirections as direction}
                  <button aria-pressed={active && signalDirection === direction.value} disabled={pending} onclick={() => onselect(category, chosen.id, direction.value)}>{text[direction.label]}</button>
                {/each}
              </div>
            </div>
          {/if}
        </li>
      {/each}
    </ul>
    {#if !visible.length}<p>{text.catalogNoResults}</p>{/if}
  </div>
  <p class="catalog-help hint">{text.catalogBuildHelp}</p>
</aside>
