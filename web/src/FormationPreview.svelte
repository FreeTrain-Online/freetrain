<script lang="ts">
  import {formationPreview} from './lib/catalog-preview.ts';
  let {cars,label,unavailable}:{cars:string[];label:string;unavailable:string}=$props();
  let preview=$state<{src:string;width:number;height:number}>(),failed=$state(false);
  $effect(()=>{
    const ids=cars;let active=true;preview=undefined;failed=false;
    void formationPreview(ids).then(value=>{if(active)preview=value;}).catch(()=>{if(active)failed=true;});
    return ()=>{active=false;};
  });
</script>
<div class="formation-preview" aria-label={label} data-car-count={cars.length}>
  {#if preview}<img src={preview.src} width={preview.width} height={preview.height} alt={label}/>{:else}<span title={failed?unavailable:undefined}>{failed?'—':'…'}</span>{/if}
</div>
