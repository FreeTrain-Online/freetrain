import {test,expect,type Page} from '@playwright/test';
import {mkdirSync,readFileSync} from 'node:fs';
import {join} from 'node:path';
import {ready,openPanel,closePanel,canonicalState as state} from './ux-helpers.ts';
const types=JSON.parse(readFileSync('src/generated/stationary-types.json','utf8'));
const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
const map=(p:Page)=>p.locator('canvas.world-canvas');
const out=(name:string)=>{const d=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'stationary');mkdirSync(d,{recursive:true});return join(d,name);};
async function save(p:Page){return(await p.request.get('/api/saves/download')).body();}
async function replace(p:Page,bytes:Buffer){const s=await state(p);const r=await p.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:bytes});expect(r.ok(),await r.text()).toBe(true);await ready(p,(await p.locator('html').getAttribute('lang'))??'en');}
async function empty(p:Page){
 const s=await state(p),r=await p.request.post('/api/worlds/empty/preview',{data:{name:'Stationary accessories',dimensions:{h:30,v:42,z:6},waterLevel:1,epoch:s.worldEpoch,revision:s.revision}});expect(r.ok()).toBe(true);
 const c=await r.json();expect((await p.request.post('/api/worlds/empty/commit',{data:{id:c.id,requestHash:c.requestHash,candidateHash:c.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).ok()).toBe(true);
 await expect(map(p)).toHaveAttribute('data-ruleset','terrain-state-1');await ready(p);
}
async function target(p:Page,ja=false,x=30,y=27,z=1){
 for(const [name,value] of [[ja?'始点 X':'Start X',x],[ja?'始点 Y':'Start Y',y],[ja?'線路の高さ':'Track level',z]] as const)await p.getByLabel(name,{exact:true}).fill(String(value));
 await p.getByRole('button',{name:ja?'経路をプレビュー':'Preview route',exact:true}).click();
}
async function language(p:Page,ja:boolean){await openPanel(p,'settings');await p.getByLabel('Language',{exact:true}).selectOption(ja?'ja':'en');await closePanel(p);}
for(const ja of [false,true])test(`stationary source lifecycle, detached preview, cancellation, peer and reload ${ja?'JA':'EN'}`,async({page,browser})=>{
 test.setTimeout(120_000);await page.goto('/');await ready(page);const original=await save(page),peer=await browser.newPage();
 try{
  await empty(page);await peer.goto('/');await ready(peer);
  // Start on a weekday, so actual source callbacks advance during the browser test.
  const initial=JSON.parse((await save(page)).toString());initial.clock.absoluteMinute='3420';await replace(page,Buffer.from(JSON.stringify(initial)));
  if(ja)await language(page,true);
  const tool=page.getByRole('button',{name:ja?'鉄道アクセサリ':'Railway accessories',exact:true});await tool.click();
  const selector=page.getByLabel(ja?'アクセサリの種類':'Accessory variant',{exact:true});await expect(selector.locator('option')).toHaveCount(54);
  const tall=types.find((t:any)=>t.height===2);await selector.selectOption(tall.id);
  await expect.poll(()=>page.getByTestId('stationary-preview').evaluate(c=>Array.from((c as HTMLCanvasElement).getContext('2d')!.getImageData(0,0,128,112).data).some((v,i)=>i%4===3&&v>0))).toBe(true);
  const before=await save(page);await target(page,ja);const confirm=page.getByRole('button',{name:ja?'アクセサリの操作を確定':'Confirm accessory action',exact:true});await expect(confirm).toBeEnabled();await expect(page.getByTestId('rail-cost')).toContainText('0');expect(await save(page)).toEqual(before);
  await confirm.click();await expect(page.getByTestId('rail-route')).toHaveCount(0);let built=await state(page);expect(built.mixed.voxels).toHaveLength(2);expect(built.mixed.voxels.every((v:any)=>v.construction.state===0)).toBe(true);expect(built.mixed.accounts.transactions).toHaveLength(0);
  await expect.poll(async()=>JSON.stringify((await state(peer)).mixed)).toBe(JSON.stringify(built.mixed));
  const bytes=await save(page);await replace(page,bytes);expect(await save(page)).toEqual(bytes);await tool.click();
  await page.getByRole('button',{name:ja?'運転':'Run',exact:false}).click();
  await expect.poll(async()=>(await state(page)).mixed.voxels.some((v:any)=>v.construction?.state>0),{timeout:20000}).toBe(true);
  await page.getByRole('button',{name:ja?'一時停止':'Pause',exact:false}).click();await expect(map(page)).toHaveAttribute('data-presentation','ready');
  await page.screenshot({path:out(`construction-${ja?'ja':'en'}.png`),fullPage:true});
  await page.getByLabel(ja?'線路の操作':'Track action',{exact:true}).selectOption('remove');await target(page,ja,30,27,2);await expect(confirm).toBeEnabled();await confirm.click();await expect(page.getByTestId('rail-route')).toHaveCount(0);
  built=await state(page);expect(built.mixed.voxels).toHaveLength(0);expect(built.mixed.accounts.transactions).toHaveLength(0);expect(JSON.parse((await save(page)).toString()).clock.pending).toHaveLength(0);
  await target(page,ja);await expect(confirm).toBeDisabled();
  if(ja){await openPanel(page,'settings','ja');await page.getByLabel('言語',{exact:true}).selectOption('en');await closePanel(page);}
 }finally{await peer.close();await replace(page,original);}
});
function gallery(saved:any){
 const packId='exav-stable-bundled';saved.ruleset='mixed-railway-1';saved.contentVersion='mixed-railway-content-1';saved.revision='1';
 const entities:any[]=[],voxels:any[]=[];
 for(const [i,t] of types.entries()){
  const h=5+i%9*2,v=7+Math.floor(i/9)*4,anchor={x:h-Math.trunc(v/2)+20,y:h+Math.trunc((v+1)/2),z:1},id=String(i+1),definition={packId,contributionId:t.id,recipeId:t.id};
  const footprint=[];for(let z=0;z<t.height;z++){footprint.push({offset:{x:0,y:0,z},transparent:true});voxels.push({position:{...anchor,z:1+z},ownerId:id,kind:'stationary',traffic:null,definition,sprite:0,drawGroundBelow:true,drawGroundAbove:true,supportedOwnerId:null});}
  entities.push({id,revision:'0',definition,anchor,footprint,owned:true,silentlyReclaimable:false,value:'0'});
 }
 saved.spatial={nextEntityId:'55',entities};saved.mixed={geometryRevision:'1',nextEntityId:'55',voxels,slopes:[],accounts:{initialCash:'15000000000',bankruptcyIncrement:'10000000000',transactions:[]}};return saved;
}
test('all 54 completed variants form a live source-art gallery, with actual-type removal, A/Escape and narrow controls',async({page,browser})=>{
 test.setTimeout(120_000);await page.goto('/');await ready(page);const original=await save(page),viewer=await browser.newPage();
 try{
  await empty(page);const fixture=gallery(JSON.parse((await save(page)).toString()));await replace(page,Buffer.from(JSON.stringify(fixture)));await expect(map(page)).toHaveAttribute('data-presentation','ready');
  const saved=await save(page);await replace(page,saved);expect(await save(page)).toEqual(saved);
  // Verify visible opaque samples against each prepared source rectangle on the actual world canvas.
  const counts=await map(page).evaluate(async(el,args)=>{
   const canvas=el as HTMLCanvasElement,r=canvas.getBoundingClientRect(),[cx,cy]=canvas.dataset.camera!.split(',').map(Number),zoom=Number(canvas.dataset.zoom),variant=canvas.dataset.terrainVariant!,actual=canvas.getContext('2d')!;
   const result=[];for(const e of args.entities){let count=0;const recipe=args.catalog.renderRecipes.find((r:any)=>r.id===e.definition.recipeId);
    for(const f of recipe.frames.filter((f:any)=>f.status==='ready')){
     const image=new Image();image.src=args.catalog.surfaceSets[f.surfaceSet][variant].image.url;await image.decode();const sheet=document.createElement('canvas');sheet.width=image.width;sheet.height=image.height;const ctx=sheet.getContext('2d')!;ctx.drawImage(image,0,0);
     const [sx,sy,w,h]=f.rectangle,[ax,ay]=f.anchor,[dx,dy,dz]=f.cell,p={x:e.anchor.x+dx,y:e.anchor.y+dy,z:e.anchor.z+dz};
     for(let y=0;y<h;y++)for(let x=0;x<w;x++){const expected=ctx.getImageData(sx+x,sy+y,1,1).data;if(expected[3]!==255)continue;
      const px=(r.width/2+(16*(p.x+p.y-20)-ax+x+.5-cx)*zoom)*canvas.width/r.width,py=(r.height/2+(8*(-p.x+p.y+20)-16*p.z-ay+y+.5-cy)*zoom)*canvas.height/r.height;
      if(px<0||py<0||px>=canvas.width||py>=canvas.height)continue;const a=actual.getImageData(Math.floor(px),Math.floor(py),1,1).data;
      if(a[0]===expected[0]&&a[1]===expected[1]&&a[2]===expected[2]&&a[3]===255)count++;
     }
    }result.push(count);
   }return result;
  },{catalog,entities:fixture.spatial.entities});
  expect(counts).toHaveLength(54);for(const n of counts)expect(n).toBeGreaterThan(2);
  await page.screenshot({path:out('gallery-all-54.png'),fullPage:true});
  await map(page).focus();await page.keyboard.press('a');await expect(page.getByRole('button',{name:'Railway accessories',exact:true})).toHaveAttribute('aria-pressed','true');
  const tall=fixture.spatial.entities.find((e:any)=>e.footprint.length===2);await target(page,false,tall.anchor.x,tall.anchor.y,2);
  const confirm=page.getByRole('button',{name:'Confirm accessory action',exact:true});await expect(confirm).toBeDisabled();
  await page.getByLabel('Track action',{exact:true}).selectOption('remove');await target(page,false,tall.anchor.x,tall.anchor.y,2);await expect(confirm).toBeEnabled();await expect(page.getByTestId('rail-cost')).toContainText('500,000');
  await page.setViewportSize({width:430,height:850});await page.screenshot({path:out('narrow-remove.png'),fullPage:true});await expect(confirm).toBeInViewport();
  const camera=await map(page).getAttribute('data-camera');await page.getByRole('button',{name:'Inspect',exact:true}).focus();await page.keyboard.press('End');
  await expect(page.getByRole('button',{name:'Remove',exact:true})).toBeFocused();await expect(page.getByRole('button',{name:'Remove',exact:true})).toBeInViewport();
  expect(await page.evaluate(()=>scrollX)).toBe(0);expect(await map(page).getAttribute('data-camera')).toBe(camera);await confirm.click();await expect(page.getByTestId('rail-route')).toHaveCount(0);
  const after=await state(page);expect(after.mixed.voxels.some((v:any)=>v.ownerId===tall.id)).toBe(false);expect(after.mixed.accounts.transactions[0].genre).toBe('{2A666F1A-9F40-4F67-98F4-DEAC1E55296D}');
  await page.setViewportSize({width:1280,height:900});await page.getByLabel('Track action',{exact:true}).selectOption('lay');await page.getByLabel('Auto-detect start level',{exact:true}).check();
  const point=await map(page).evaluate(el=>{const c=el as HTMLCanvasElement,r=c.getBoundingClientRect(),[cx,cy]=c.dataset.camera!.split(',').map(Number),zoom=Number(c.dataset.zoom);return{x:r.x+r.width/2+(16*(30+27-20)+16-cx)*zoom,y:r.y+r.height/2+(8*(-30+27+20)-16+8-cy)*zoom};});
  const unchanged=await save(page);await page.mouse.click(point.x,point.y);await expect(confirm).toBeEnabled();await map(page).focus();await page.keyboard.press('Escape');await expect(page.getByTestId('rail-route')).toHaveCount(0);expect(await save(page)).toEqual(unchanged);
  expect((await viewer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);await viewer.goto('/');await ready(viewer);await expect(viewer.getByRole('button',{name:'Railway accessories',exact:true})).toBeDisabled();
 }finally{await viewer.close();await replace(page,original);}
});
