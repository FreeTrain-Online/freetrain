import { locales, messages, initialLocale, persistLocale, resolveLocale, interpolate, pluralMessage } from './lib/localization.ts';
import { loadRenderCatalog, RecipeImageCache, instantiate, type RenderCatalog, type Variant, type ReadyScene, type DrawEffect } from './lib/content-recipes.ts';
import './content-gallery.css';
const select=(id:string)=>document.getElementById(id) as HTMLSelectElement;
const input=(id:string)=>document.getElementById(id) as HTMLInputElement;
const status=document.getElementById('status')!, frames=document.getElementById('frames')!, credits=document.getElementById('credits')!;
const canvas=document.getElementById('composition') as HTMLCanvasElement;
const plugin=select('plugin'), choice=select('recipe'),season=select('season'),effect=select('effect'),night=input('night'),height=input('height');
let locale=initialLocale();
let text=locales[locale].gallery;
const language=select('language');
language.replaceChildren(...Object.entries(locales).map(([id,definition])=>new Option(definition.label,id)));
function localize() {
  persistLocale(locale); text=locales[locale].gallery; language.value=locale;
  document.title=text.title;
  for(const element of document.querySelectorAll<HTMLElement>('[data-i18n]')) element.textContent=text[element.dataset.i18n as keyof typeof text];
  document.getElementById('language-label')!.textContent=messages[locale].language;
  canvas.setAttribute('aria-label',text.composition);
  document.getElementById('selection')!.textContent='';
}
localize();
language.addEventListener('change',()=>{locale=resolveLocale(language.value);localize();void render();});
const cache=new RecipeImageCache();
let generation=0, catalog:RenderCatalog, scene:ReadyScene|undefined;
const view={x:300,y:250,zoom:2};
function options() {
  const recipes=catalog.renderRecipes.filter(r=>!plugin.value||r.pluginId===plugin.value);
  choice.replaceChildren(...recipes.map(r=>new Option(`${r.id} (${r.handler})`,r.id)));
  void render();
}
async function render() {
  if(!catalog)return;
  const current=++generation; scene=undefined; canvas.dataset.ready='false'; frames.replaceChildren();
  const ctx=canvas.getContext('2d')!;ctx.clearRect(0,0,canvas.width,canvas.height);
  const recipe=catalog.renderRecipes.find(r=>r.id===choice.value);if(!recipe)return;
  const words=text;
  status.textContent=words.loading;credits.textContent=`${recipe.pluginId}\n${recipe.sourcePath??''}\n${recipe.id}\n${recipe.handler}\n${words.declaredAuthor}: ${JSON.stringify(catalog.plugins?.find(p=>p.id===recipe.pluginId)?.author??null)}\n${words.attribution}`;
  const variant:Variant={season:season.value as Variant['season'],night:night.checked};
  const variantKey=`${variant.season}/${variant.night?'night':'day'}`;
  if(recipe.handler==='variable-height') {
    height.min=String(recipe.fields.minHeight);height.max=String(recipe.fields.maxHeight);
    height.value=String(Math.max(Number(height.min),Math.min(Number(height.max),Number(height.value))));
  }
  height.disabled=recipe.handler!=='variable-height';
  try {
    const layers=instantiate(recipe,recipe.id,[0,0,0],{height:Number(height.value)});
    const ready=await cache.ready(catalog,layers,variant);
    const cards=await Promise.all(recipe.frames.map(async frame=>{
      const article=document.createElement('article'),label=document.createElement('p');label.textContent=frame.key;article.append(label);
      if(frame.status==='invisible') { label.textContent+=' — '+words.invisible;return article; }
      if(!frame.masks?.[variantKey]) { label.textContent+=' — '+words.invalidCrop;article.dataset.diagnostic='true';return article; }
      const binding=catalog.surfaceSets[frame.surfaceSet!][variantKey];
      const asset=catalog.assets?.find(a=>a.path===binding.sourcePath);
      const source=document.createElement('p');source.textContent=`${binding.sourcePath} · ${words.author}: ${JSON.stringify(asset?.author??null)} · ${words.credits}: ${JSON.stringify(asset?.credits??null)}`;article.append(source);
      const image=await cache.get(binding.image), [sx,sy,w,h]=frame.rectangle!;
      const c=document.createElement('canvas'); c.width=w*2;c.height=h*2;const context=c.getContext('2d')!;
      context.imageSmoothingEnabled=false;context.drawImage(image.image,sx,sy,w,h,0,0,w*2,h*2);article.append(c);return article;
    }));
    if(current!==generation)return;
    frames.replaceChildren(...cards);scene=ready;ctx.fillStyle='#c7d5c2';ctx.fillRect(0,0,canvas.width,canvas.height);
    const drawEffect:DrawEffect=effect.value==='shape'?{kind:'shape',color:'#ffd45a'}:effect.value==='alpha'?{kind:'alpha'}:{kind:'normal'};
    ready.draw(ctx,view,drawEffect);canvas.dataset.ready='true';canvas.dataset.recipe=recipe.id;canvas.dataset.variant=variantKey;
    status.textContent=interpolate(words.summary,{frames:pluralMessage(locale,locales[locale].formats.frameCount,recipe.frames.length),diagnostics:recipe.frames.filter(f=>f.status==='invalid-source-crop').length,recipes:catalog.renderRecipes.length});
  } catch(error) { if(current===generation)status.textContent=text.failed; }
}
canvas.addEventListener('click',event=>{
  const rect=canvas.getBoundingClientRect();const picked=scene?.pick((event.clientX-rect.left)*canvas.width/rect.width,(event.clientY-rect.top)*canvas.height/rect.height,view);
  document.getElementById('selection')!.textContent=picked?`${picked.recipeId} · ${picked.frame.key}`:text.noSprite;
});
plugin.addEventListener('change',options);for(const control of [choice,season,night,height,effect])control.addEventListener('change',()=>void render());
loadRenderCatalog().then(value=>{
  catalog=value;
  plugin.append(...[...new Set(catalog.renderRecipes.map(r=>r.pluginId))].sort().map(p=>new Option(p,p)));
  options();
}).catch(error=>{status.textContent=text.failed;});
