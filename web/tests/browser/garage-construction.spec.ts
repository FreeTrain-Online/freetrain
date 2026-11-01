import {test,expect,type Page} from '@playwright/test';
import {mkdirSync,readFileSync} from 'node:fs';
import {join} from 'node:path';
import {ready,openPanel,closePanel,canonicalState as state} from './ux-helpers.ts';
const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
const garage='{93481807-D998-46d7-B1BE-CB2B5D086D67}';
const out=(name:string)=>{const d=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'garage');mkdirSync(d,{recursive:true});return join(d,name);};
async function save(p:Page){return(await p.request.get('/api/saves/download')).body();}
async function replace(p:Page,bytes:Buffer){const s=await state(p);expect((await p.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:bytes})).ok()).toBe(true);await ready(p,(await p.locator("html").getAttribute("lang"))??"en");}
async function empty(p:Page){
 const s=await state(p),r=await p.request.post('/api/worlds/empty/preview',{data:{name:'Garage workflow',dimensions:{h:30,v:42,z:6},waterLevel:1,epoch:s.worldEpoch,revision:s.revision}});expect(r.ok()).toBe(true);
 const c=await r.json();expect((await p.request.post('/api/worlds/empty/commit',{data:{id:c.id,requestHash:c.requestHash,candidateHash:c.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).ok()).toBe(true);
 await expect(p.locator('canvas')).toHaveAttribute('data-ruleset','terrain-state-1');await ready(p);
}
async function target(p:Page,from=[30,27],to=[34,27],z=1,ja=false){
 for(const [name,value] of [[ja?'始点 X':'Start X',from[0]],[ja?'始点 Y':'Start Y',from[1]],[ja?'終点 X':'End X',to[0]],[ja?'終点 Y':'End Y',to[1]],[ja?'線路の高さ':'Track level',z]] as const)await p.getByLabel(name,{exact:true}).fill(String(value));
 await p.getByRole('button',{name:ja?'経路をプレビュー':'Preview route',exact:true}).click();
}
async function confirm(p:Page,ja=false,normal=false){await p.getByRole('button',{name:normal?'Confirm track change':ja?'機関庫の変更を確定':'Confirm garage change',exact:true}).click();await expect(p.getByTestId('rail-route')).toHaveCount(0);await expect(p.locator('.command-feedback')).toContainText(ja?'サーバーで確定':'Confirmed by server');}
async function pixels(p:Page,voxels:any[]){
 await expect(p.locator('canvas')).toHaveAttribute('data-presentation','ready');
 return p.locator('canvas').evaluate(async(el,args)=>{
  const canvas=el as HTMLCanvasElement,r=canvas.getBoundingClientRect(),[cx,cy]=canvas.dataset.camera!.split(',').map(Number),zoom=Number(canvas.dataset.zoom),variant=canvas.dataset.terrainVariant!;
  const actual=canvas.getContext('2d')!,counts:number[]=[];
  for(const v of args.voxels){
   const rail=v.traffic?.rail,recipe=args.catalog.renderRecipes.find((r:any)=>r.id===(rail?.definition.recipeId??v.definition.recipeId));
   const frames=rail?recipe.frames.filter((f:any)=>f.selector.index===rail.variant):recipe.frames;
   let count=0;
   for(const f of frames){
    const image=new Image();image.src=args.catalog.surfaceSets[f.surfaceSet][variant].image.url;await image.decode();
    const sheet=document.createElement('canvas');sheet.width=image.naturalWidth;sheet.height=image.naturalHeight;const ctx=sheet.getContext('2d')!;ctx.drawImage(image,0,0);
    const [sx,sy,w,h]=f.rectangle,data=ctx.getImageData(sx,sy,w,h).data;
    const x=r.width/2+(16*(v.position.x+v.position.y-20)-f.anchor[0]-cx)*zoom;
    const y=r.height/2+(8*(-v.position.x+v.position.y+20)-16*v.position.z-f.anchor[1]-cy)*zoom;
    for(let b=0;b<h;b++)for(let a=0;a<w;a++){
     const i=(b*w+a)*4;if(data[i+3]!==255)continue;
     const px=Math.floor((x+(a+.5)*zoom)*canvas.width/r.width),py=Math.floor((y+(b+.5)*zoom)*canvas.height/r.height);
     if(px<0||py<0||px>=canvas.width||py>=canvas.height)continue;
     const pixel=actual.getImageData(px,py,1,1).data;
     if(pixel[0]===data[i]&&pixel[1]===data[i+1]&&pixel[2]===data[i+2])count++;
    }
   }counts.push(count);
  }return counts;
 },{catalog,voxels});
}


for(const ja of [false,true])test(`garage axes, ground legality, cumulative removal, exact reopen and peer publication ${ja?'JA':'EN'}`,async({page,browser})=>{
 test.setTimeout(120_000);await page.goto('/');await ready(page);const original=await save(page);const peer=await browser.newPage();
 try{
  await peer.goto('/');await ready(peer);
  for(const [x,y,art] of [[34,27,0],[30,31,1],[26,27,0],[30,23,1]]){
   await empty(page);
   if(ja){await openPanel(page,'settings');await page.getByLabel('Language',{exact:true}).selectOption('ja');await closePanel(page);}
   await ready(page,ja?'ja':'en');
   await page.getByRole('button',{name:ja?'機関庫':'Train garage',exact:true}).click();
   const before=await save(page);await target(page,[30,27],[x,y],2,ja);
   await expect(page.getByTestId('rail-status')).toContainText(ja?'地表の高さ':'local ground level');
   await expect(page.getByRole('button',{name:ja?'機関庫の変更を確定':'Confirm garage change',exact:true})).toBeDisabled();
   expect(await save(page)).toEqual(before);
   await target(page,[30,27],[x,y],1,ja);await expect(page.getByTestId('rail-cost')).toContainText('32,500,000');
   expect(await save(page)).toEqual(before);await confirm(page,ja);
   const built=await state(page),rails=built.mixed.voxels.filter((v:any)=>v.traffic?.rail);
   expect(rails).toHaveLength(5);expect(rails.every((v:any)=>v.traffic.rail.definition.recipeId===garage&&v.traffic.rail.variant===art)).toBe(true);
   await expect.poll(async()=>JSON.stringify((await state(peer)).mixed)).toBe(JSON.stringify(built.mixed));
   expect((await pixels(page,rails)).every(n=>n>10)).toBe(true);
   await page.screenshot({path:out(`garage-${ja?'ja':'en'}-${x}-${y}.png`),fullPage:true});
   const captured=await save(page);await replace(page,captured);expect(await save(page)).toEqual(captured);
   await page.getByRole('button',{name:ja?'機関庫':'Train garage',exact:true}).click();await page.getByLabel(ja?'線路の操作':'Track action',{exact:true}).selectOption('remove');
   await target(page,[30,27],[x,y],1,ja);await expect(page.getByTestId('rail-cost')).toContainText('2,000,000');await confirm(page,ja);
   const removed=await state(page);expect(removed.mixed.voxels.filter((v:any)=>v.traffic?.rail.definition.recipeId===garage)).toHaveLength(1);
   expect(removed.mixed.accounts.transactions.map((t:any)=>t.expenditure)).toEqual(['32500000','200000','400000','600000','800000']);
   await target(page,[x,y],[30,27],1,ja);await expect(page.getByTestId('rail-cost')).toContainText('800,000');await confirm(page,ja);
   expect((await state(page)).mixed.voxels.every((v:any)=>v.traffic.rail.definition.contributionId.startsWith('system:pattern/'))).toBe(true);
   if(ja){await openPanel(page,'settings','ja');await page.getByLabel('言語',{exact:true}).selectOption('en');await closePanel(page);}
  }
 }finally{await peer.close();await replace(page,original);}
});

test('garage map picks, keyboard cancellation, mixed track replacement and observer controls preserve the map',async({page,browser})=>{
 test.setTimeout(90_000);page.setDefaultTimeout(10_000);
 await page.goto('/');await ready(page);const original=await save(page);const viewer=await browser.newPage();
 try{
  await empty(page);const canvas=page.locator('canvas');await canvas.evaluate(el=>(window as any).garageCanvas=el);
  await canvas.focus();await page.keyboard.press('h');await expect(page.getByRole('button',{name:'Train garage',exact:true})).toHaveAttribute('aria-pressed','true');
  for(const x of [30,34]){
   const q=await canvas.evaluate((el,x)=>{const c=el as HTMLCanvasElement,r=c.getBoundingClientRect(),[cx,cy]=c.dataset.camera!.split(',').map(Number),z=Number(c.dataset.zoom);return{x:r.x+r.width/2+(16*(x+27-20)+16-cx)*z,y:r.y+r.height/2+(8*(-x+27+20)-16+8-cy)*z};},x);
   await page.mouse.click(q.x,q.y);
  }
  await expect(page.getByLabel('Track level',{exact:true})).toHaveValue('1');await expect(page.getByRole('button',{name:'Confirm garage change',exact:true})).toBeEnabled();
  const before=await save(page);await canvas.focus();await page.keyboard.press('Escape');await expect(page.getByTestId('rail-route')).toHaveCount(0);expect(await save(page)).toEqual(before);
  await page.getByRole('button',{name:'Track',exact:true}).click();await target(page,[30,27],[31,27],1);await confirm(page,false,true);
  await page.getByRole('button',{name:'Train garage',exact:true}).click();await target(page);await confirm(page);
  const built=await state(page);expect(built.mixed.voxels).toHaveLength(5);expect(built.mixed.accounts.transactions.map((t:any)=>t.expenditure)).toEqual(['12000000','32500000']);
  expect(await canvas.evaluate(el=>el===(window as any).garageCanvas)).toBe(true);
  expect((await viewer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);await viewer.goto('/');await ready(viewer);
  await expect(viewer.getByRole('button',{name:'Train garage',exact:true})).toBeDisabled();
  await page.setViewportSize({width:430,height:850});await expect(page.getByRole('button',{name:'Train garage',exact:true})).toHaveAttribute('aria-pressed','true');
  await target(page);await expect(page.getByRole('button',{name:'Confirm garage change',exact:true})).toBeEnabled();
  await page.screenshot({path:out('garage-narrow-preview.png'),fullPage:true});
 }finally{await viewer.close();await replace(page,original);}
});
