import { RecipeImageCache, instantiate, traversal, composeTrain, validateCatalog, halfBlend, type RenderCatalog, type Recipe } from '../src/lib/content-recipes.ts';
const assert=(value:unknown,message='Assertion failed')=>{if(!value)throw new Error(message);};
const equal=(a:unknown,b:unknown)=>assert(JSON.stringify(a)===JSON.stringify(b),JSON.stringify([a,b]));
const c=JSON.parse(await Deno.readTextFile(new URL('../public/content/bundled-v2/catalog.json',import.meta.url))) as RenderCatalog;
const byHandler=(handler:string)=>c.renderRecipes.find(r=>r.handler===handler)!;
Deno.test('all compiled source recipes and masks validate and exact identity rejects drift',()=>{
  validateCatalog(c);assert(c.renderRecipes.length>=812);
  const invalid=structuredClone(c);invalid.renderRecipes.push(invalid.renderRecipes[0]);
  let rejected=false;try{validateCatalog(invalid);}catch{rejected=true;}assert(rejected);
});
Deno.test('variable buildings assemble every declared height with source top-first overlap priority',()=>{
  for(const r of c.renderRecipes.filter(r=>r.handler==='variable-height')) {
    for(let height=Number(r.fields.minHeight);height<=Number(r.fields.maxHeight);height++) {
      const layers=instantiate(r,'building',[5,6,2],{height});
      assert(layers.every(l=>l.position[2]>=2 && l.position[2]<2+height));
      const top=layers.filter(l=>l.position[2]===height+1);assert(top.length>0);assert(top.every(l=>l.frame.key.startsWith('top/0/')));
    }
  }
});
Deno.test('train compositions preserve body defaults and tail-wins single-car source behavior',()=>{
  const r=c.renderRecipes.find(r=>r.handler==='train-composition' && (r.fields.composition as Record<string,string>).tail)!;
  const comp=r.fields.composition as Record<string,string>;
  equal(composeTrain(c,r.id,1),[comp.tail]);equal(composeTrain(c,r.id,3),[comp.head??comp.body,comp.body,comp.tail]);
  for(const car of c.renderRecipes.filter(r=>['train-car','reverse-train-car'].includes(r.handler))) {
    const n=car.fields.asymmetric?16:8;
    for(let angle=0;angle<n;angle++)assert(instantiate(car,'car',[0,0,0],{angle}).length===1);
  }
});
Deno.test('draw and reverse picking share traversal, cut, crop anchor and variant alpha mask',async()=>{
  const r=byHandler('land-0d'),f=r.frames[0],key='spring/day',rows=c.masks[f.masks![key]];
  const row=rows.findIndex(row=>row.includes('1')),column=rows[row].indexOf('1');
  const layers=[...instantiate(r,'back',[0,0,0]),...instantiate(r,'front',[0,0,0])];
  equal(traversal(layers).map(l=>l.entityId),['back','front']);equal(traversal(layers,-1),[]);
  const cache=new RecipeImageCache(async media=>{
    const b=Object.values(c.surfaceSets).flatMap(s=>Object.values(s)).find(b=>b.image.url===media.url)!;
    return {image:{} as CanvasImageSource,width:b.width,height:b.height};
  });
  const ready=await cache.ready(c,layers,{season:'spring',night:false}), calls:unknown[][]=[];
  const ctx={imageSmoothingEnabled:true,drawImage:(...args:unknown[])=>calls.push(args)} as unknown as CanvasRenderingContext2D;
  const view={x:100,y:100,zoom:2};ready.draw(ctx,view);assert(calls.length===2);
  const x=100-f.anchor![0]*2+column*2+1,y=100-f.anchor![1]*2+row*2+1;
  assert(ready.pick(x,y,view)?.entityId==='front');assert(!ready.pick(x,y,{...view,heightCut:-1}));
  assert(!ready.pick(-100,-100,view));
  equal(halfBlend([255,3,9,255],[1,5,8,255]),[127,3,8,255]);
  equal(halfBlend([255,3,9,0],[1,5,8,255]),[1,5,8,255]);
});
Deno.test('asset readiness deduplicates concurrent decodes and evicts failures for retry',async()=>{
  const media=Object.values(c.surfaceSets)[0]['spring/day'].image;
  let attempts=0;const cache=new RecipeImageCache(async()=>{attempts++;if(attempts===1)throw new Error('offline');return {image:{} as CanvasImageSource,width:1,height:1};});
  await Promise.allSettled([cache.get(media),cache.get(media)]);assert(attempts===1);
  await Promise.all([cache.get(media),cache.get(media)]);assert(attempts===2);
});
