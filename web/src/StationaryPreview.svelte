<script lang="ts">
  import {loadRenderCatalog,RecipeImageCache,instantiate,traversal} from './lib/content-recipes.ts';
  import {messages} from './lib/localization.ts';
  let {type,locale}:{type:{id:string;groupEn:string;groupJa:string;variant:number};locale:'en'|'ja'}=$props();
  let canvas:HTMLCanvasElement,failed=$state(false);
  const cache=new RecipeImageCache();
  let catalogPromise:ReturnType<typeof loadRenderCatalog>|undefined;
  $effect(()=>{
    const id=type.id;let active=true;failed=false;
    void (async()=>{
      try{
        const catalog=await (catalogPromise??=loadRenderCatalog().catch(error=>{catalogPromise=undefined;throw error;})),recipe=catalog.renderRecipes.find(r=>r.id===id)!;
        const layers=traversal(instantiate(recipe,id,[0,0,0]));
        const ready=await cache.ready(catalog,layers,{season:'spring',night:false});
        if(!active||!canvas)return;
        const ctx=canvas.getContext('2d')!;ctx.clearRect(0,0,128,112);ctx.imageSmoothingEnabled=false;
        ready.draw(ctx,{x:32,y:72,zoom:2});
      }catch{if(active)failed=true;}
    })();
    return()=>{active=false;};
  });
</script>
<canvas bind:this={canvas} width="128" height="112" aria-label={`${locale==='ja'?type.groupJa:type.groupEn} · ${type.variant}`} data-testid="stationary-preview"></canvas>
{#if failed}<p role="status">{messages[locale].stationaryPreviewUnavailable}</p>{/if}
