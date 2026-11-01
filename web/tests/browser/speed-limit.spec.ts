import {test,expect,type Page} from '@playwright/test';
import {mkdirSync,readFileSync} from 'node:fs';
import {join} from 'node:path';
import {ready,openPanel,closePanel,canonicalState as state} from './ux-helpers.ts';
const icons=JSON.parse(readFileSync('src/generated/speed-limit-icons.json','utf8'));
const out=(name:string)=>{const d=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'speed-limit');mkdirSync(d,{recursive:true});return join(d,name);};
const text=(ja=false)=>({task:ja?'速度制限':'Speed limits',confirm:ja?'速度制限の変更を確定':'Confirm speed-limit change',fromX:ja?'始点 X':'Start X',fromY:ja?'始点 Y':'Start Y',toX:ja?'終点 X':'End X',toY:ja?'終点 Y':'End Y',level:ja?'線路の高さ':'Track level',check:ja?'経路をプレビュー':'Preview route',action:ja?'線路の操作':'Track action',speed:ja?'速度':'Speed',shape:ja?'区間の形状':'Section shape',direction:ja?'上り方向':'Uphill direction'});
async function save(p:Page){return(await p.request.get('/api/saves/download')).body();}
async function replace(p:Page,b:Buffer,ja=false){const s=await state(p);expect((await p.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:b})).ok()).toBe(true);await ready(p,ja?'ja':'en');}
async function empty(p:Page,ja=false){
 const s=await state(p),r=await p.request.post('/api/worlds/empty/preview',{data:{name:'Speed-limit workflow',dimensions:{h:30,v:42,z:6},waterLevel:1,epoch:s.worldEpoch,revision:s.revision}});expect(r.ok()).toBe(true);const c=await r.json();
 expect((await p.request.post('/api/worlds/empty/commit',{data:{id:c.id,requestHash:c.requestHash,candidateHash:c.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).ok()).toBe(true);await ready(p,ja?'ja':'en');await expect(p.locator('canvas')).toHaveAttribute('data-ruleset','terrain-state-1');
}
async function range(p:Page,ja=false,from=[30,27,1],to=[33,27]){
 const t=text(ja);for(const [name,n] of [[t.fromX,from[0]],[t.fromY,from[1]],[t.level,from[2]],[t.toX,to[0]],[t.toY,to[1]]] as const)if(await p.getByLabel(name,{exact:true}).count())await p.getByLabel(name,{exact:true}).fill(String(n));
 await p.getByRole('button',{name:t.check,exact:true}).click();
}
async function confirm(p:Page,ja=false){await p.getByRole('button',{name:text(ja).confirm,exact:true}).click();await expect(p.getByTestId('rail-route')).toHaveCount(0);await expect(p.locator('.command-feedback')).toContainText(ja?'サーバーで確定':'Confirmed by server');await expect(p.locator('canvas')).toHaveAttribute('data-presentation','ready');}
async function track(p:Page){
 await p.getByRole('button',{name:'Track',exact:true}).click();await range(p);await p.getByRole('button',{name:'Confirm track change',exact:true}).click();await expect(p.getByTestId('rail-route')).toHaveCount(0);
}
async function locale(p:Page,ja:boolean){await openPanel(p,'settings',ja?'en':'ja');await p.getByLabel(ja?'Language':'言語',{exact:true}).selectOption(ja?'ja':'en');await closePanel(p);await ready(p,ja?'ja':'en');}
async function click(p:Page,x:number,y:number,z:number){const q=await p.locator('canvas').evaluate((el,a)=>{const c=el as HTMLCanvasElement,r=c.getBoundingClientRect(),[cx,cy]=c.dataset.camera!.split(',').map(Number),zoom=Number(c.dataset.zoom);return{x:r.x+r.width/2+(16*(a.x+a.y-20)+16-cx)*zoom,y:r.y+r.height/2+(8*(-a.x+a.y+20)-16*a.z+8-cy)*zoom};},{x,y,z});await p.mouse.click(q.x,q.y);}
async function canvasHash(p:Page){
 return p.locator('canvas').evaluate(async el=>{
  await new Promise<void>(resolve=>requestAnimationFrame(()=>requestAnimationFrame(()=>resolve())));
  const blob=await new Promise<Blob>(resolve=>(el as HTMLCanvasElement).toBlob(b=>resolve(b!)));
  return Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',await blob.arrayBuffer())),v=>v.toString(16).padStart(2,'0')).join('');
 });
}
async function sourcePixels(p:Page,speed:'low'|'medium'|'fast',position:{x:number;y:number;z:number}){
 return p.locator('canvas').evaluate(async(el,{icon,position})=>{
  const c=el as HTMLCanvasElement,r=c.getBoundingClientRect(),[cx,cy]=c.dataset.camera!.split(',').map(Number),zoom=Number(c.dataset.zoom),image=new Image();image.src=icon.image.url;await image.decode();
  const sheet=document.createElement('canvas');sheet.width=icon.width;sheet.height=icon.height;const ctx=sheet.getContext('2d')!;ctx.drawImage(image,0,0);const data=ctx.getImageData(0,0,icon.width,icon.height).data;
  const x=r.width/2+(16*(position.x+position.y-20)-cx)*zoom,y=r.height/2+(8*(-position.x+position.y+20)-16*position.z-cy)*zoom;let n=0;
  for(let b=0;b<icon.height;b++)for(let a=0;a<icon.width;a++){
   const i=(b*icon.width+a)*4;if(data[i+3]!==255)continue;const px=Math.floor((x+(a+.5)*zoom)*c.width/r.width),py=Math.floor((y+(b+.5)*zoom)*c.height/r.height);if(px<0||py<0||px>=c.width||py>=c.height)continue;
   const pixel=c.getContext('2d')!.getImageData(px,py,1,1).data;if(pixel[0]===data[i]&&pixel[1]===data[i+1]&&pixel[2]===data[i+2])n++;
  }return n;
 },{icon:icons[speed],position});
}
for(const ja of [false,true])test(`speed-limit level sections, three speeds, overlap, no limit, peers, exact reopen and visibility ${ja?'JA':'EN'}`,async({page,browser})=>{
 test.setTimeout(120_000);await page.goto('/');await ready(page);const original=await save(page),peer=await browser.newPage();
 try{
  await empty(page);await track(page);await peer.goto('/');await ready(peer);if(ja)await locale(page,true);
  const t=text(ja),canvas=page.locator('canvas');await canvas.evaluate(el=>el.dataset.instance='speed-canvas');const camera=await canvas.getAttribute('data-camera'),bare=await state(page);
  await page.getByRole('button',{name:t.task,exact:true}).click();
  for(const speed of ['low','medium','fast'] as const){
   await page.getByLabel(t.speed,{exact:true}).selectOption(speed);const before=await save(page);await range(page,ja);await expect(page.getByRole('button',{name:t.confirm,exact:true})).toBeEnabled();expect(await save(page)).toEqual(before);await confirm(page,ja);
   const built=await state(page);expect(built.mixed.speedLimits[speed]).toHaveLength(4);expect(built.mixed.voxels).toEqual(bare.mixed.voxels);expect(built.mixed.accounts).toEqual(bare.mixed.accounts);expect(built.tick).toBe(bare.tick);
   await expect.poll(async()=>JSON.stringify((await state(peer)).mixed)).toBe(JSON.stringify(built.mixed));expect(await sourcePixels(page,speed,{x:30,y:27,z:1})).toBeGreaterThan(8);
   await range(page,ja);await expect(page.getByRole('button',{name:t.confirm,exact:true})).toBeDisabled();await expect(page.getByTestId('speed-membership')).toContainText(ja?{low:'低速',medium:'中速',fast:'高速'}[speed]:{low:'Low',medium:'Medium',fast:'Fast'}[speed]);
   await page.getByRole('button',{name:ja?'プレビューを取消':'Cancel preview',exact:true}).click();
   const shown=await canvasHash(page),saved=await save(page);
   await openPanel(page,'view',ja?'ja':'en');await page.getByLabel(ja?'速度制限を表示':'Show speed limits',{exact:true}).uncheck();await expect(canvas).toHaveAttribute('data-presentation','ready');await expect.poll(()=>canvasHash(page)).not.toBe(shown);expect(await save(page)).toEqual(saved);
   await page.getByLabel(ja?'速度制限を表示':'Show speed limits',{exact:true}).check();await closePanel(page);await expect.poll(()=>canvasHash(page)).toBe(shown);
   await page.screenshot({path:out(`${ja?'ja':'en'}-${speed}.png`),fullPage:true});
   await replace(page,saved,ja);expect(await save(page)).toEqual(saved);await page.reload();await ready(page,ja?'ja':'en');expect(await save(page)).toEqual(saved);
   await page.getByRole('button',{name:t.task,exact:true}).click();await page.getByLabel(t.action,{exact:true}).selectOption('remove');await range(page,ja);await confirm(page,ja);expect((await state(page)).mixed.speedLimits).toBeUndefined();
   await range(page,ja);await expect(page.getByTestId('speed-membership')).toContainText(ja?'制限なし':'No limit');await expect(page.getByRole('button',{name:t.confirm,exact:true})).toBeEnabled();await page.getByLabel(t.action,{exact:true}).selectOption('lay');
  }
  // Separate authored world never inherits membership; restoring a limited world restores exactly.
  await range(page,ja);await confirm(page,ja);const limited=await save(page);await empty(page,ja);expect((await state(page)).mixed).toBeUndefined();await replace(page,limited,ja);expect(await save(page)).toEqual(limited);
 }finally{await peer.close();await replace(page,original,ja);}
});
test('speed-limit four-position slopes in all cardinal directions, wrong-direction rejection, member removal and demolition cleanup',async({page})=>{
 test.setTimeout(120_000);await page.goto('/');await ready(page);const original=await save(page);
 try{
  for(const d of [0,2,4,6]){
   await empty(page);await page.getByRole('button',{name:'Slope',exact:true}).click();
   for(const [name,n] of [['Slope X',30],['Slope Y',27],['Base / selected level',1]] as const)await page.getByLabel(name,{exact:true}).fill(String(n));
   await page.getByLabel('Uphill direction',{exact:true}).selectOption(String(d));await page.getByRole('button',{name:'Preview slope',exact:true}).click();await page.getByRole('button',{name:'Confirm slope change',exact:true}).click();await expect(page.getByTestId('slope-base')).toHaveCount(0);
   await page.getByRole('button',{name:'Speed limits',exact:true}).click();await page.getByLabel('Section shape',{exact:true}).selectOption({label:'Slope section'});await page.getByLabel('Uphill direction',{exact:true}).selectOption(String(d));await range(page);await confirm(page);
   const built=await state(page),dx=[0,1,0,-1][d/2],dy=[-1,0,1,0][d/2];expect(built.mixed.speedLimits.low).toEqual([0,1,2,3].map(i=>({x:30+i*dx,y:27+i*dy,z:1+Math.floor(i/2)})));
   const saved=await save(page);await replace(page,saved);expect(await save(page)).toEqual(saved);await page.getByRole('button',{name:'Speed limits',exact:true}).click();await page.getByLabel('Section shape',{exact:true}).selectOption({label:'Slope section'});await page.getByLabel('Track action',{exact:true}).selectOption('remove');await page.getByLabel('Uphill direction',{exact:true}).selectOption(String((d+2)%8));await range(page,false,[30+3*dx,27+3*dy,2]);await expect(page.getByRole('button',{name:'Confirm speed-limit change',exact:true})).toBeDisabled();
   await page.getByLabel('Uphill direction',{exact:true}).selectOption(String(d));await range(page,false,[30+3*dx,27+3*dy,2]);await confirm(page);expect((await state(page)).mixed.speedLimits).toBeUndefined();
   await page.getByLabel('Track action',{exact:true}).selectOption('lay');await range(page);await confirm(page);await page.screenshot({path:out(`slope-${d}.png`),fullPage:true});
   await page.getByRole('button',{name:'Slope',exact:true}).click();await page.getByLabel('Track action',{exact:true}).selectOption('remove');for(const [name,n] of [['Slope X',30],['Slope Y',27],['Base / selected level',1]] as const)await page.getByLabel(name,{exact:true}).fill(String(n));await page.getByLabel('Uphill direction',{exact:true}).selectOption(String(d));await page.getByRole('button',{name:'Preview slope',exact:true}).click();await page.getByRole('button',{name:'Confirm slope change',exact:true}).click();await expect(page.getByTestId('slope-base')).toHaveCount(0);expect((await state(page)).mixed.speedLimits).toBeUndefined();
  }
 }finally{await replace(page,original);}
});
test('speed-limit L/Escape, map selection, stale quote, Observer and narrow controls',async({page,browser})=>{
 await page.goto('/');await ready(page);const original=await save(page),viewer=await browser.newPage();
 try{
  await empty(page);const untouched=await save(page);await page.getByRole('button',{name:'Speed limits',exact:true}).click();await page.getByLabel('Track action',{exact:true}).selectOption('remove');await range(page);await confirm(page);expect((await state(page)).mixed).toBeUndefined();expect(await save(page)).toEqual(untouched);await track(page);const canvas=page.locator('canvas'),before=await save(page),camera=await canvas.getAttribute('data-camera');await canvas.focus();await page.keyboard.press('l');await expect(page.getByRole('button',{name:'Speed limits',exact:true})).toHaveAttribute('aria-pressed','true');
  await click(page,30,27,1);await click(page,33,27,1);await expect(page.getByRole('button',{name:'Confirm speed-limit change',exact:true})).toBeEnabled();await canvas.focus();await page.keyboard.press('Escape');await expect(page.getByTestId('rail-route')).toHaveCount(0);expect(await save(page)).toEqual(before);await expect(canvas).toHaveAttribute('data-camera',camera!);
  await range(page);const s=await state(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:before})).ok()).toBe(true);await ready(page);await expect(page.getByTestId('rail-route')).toHaveCount(0);
  await page.getByRole('button',{name:'Speed limits',exact:true}).click();await page.setViewportSize({width:430,height:850});await range(page);await expect(page.getByRole('button',{name:'Confirm speed-limit change',exact:true})).toBeEnabled();await page.screenshot({path:out('en-narrow.png'),fullPage:true});
  expect((await viewer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);await viewer.goto('/');await ready(viewer);await expect(viewer.getByRole('button',{name:'Speed limits',exact:true})).toBeDisabled();
 }finally{await viewer.close();await replace(page,original);}
});

test('Japanese slope controls, source-icon inspection and narrow confirmation',async({page})=>{
 await page.goto('/');await ready(page);const original=await save(page);
 try{
  await empty(page);await page.getByRole('button',{name:'Slope',exact:true}).click();
  for(const [name,n] of [['Slope X',30],['Slope Y',27],['Base / selected level',1]] as const)await page.getByLabel(name,{exact:true}).fill(String(n));
  await page.getByLabel('Uphill direction',{exact:true}).selectOption('2');await page.getByRole('button',{name:'Preview slope',exact:true}).click();await page.getByRole('button',{name:'Confirm slope change',exact:true}).click();await expect(page.getByTestId('slope-base')).toHaveCount(0);
  await locale(page,true);await page.getByRole('button',{name:'速度制限',exact:true}).click();await page.getByLabel('区間の形状',{exact:true}).selectOption({label:'速度制限区間（勾配）'});await page.getByLabel('上り方向',{exact:true}).selectOption('2');await page.getByLabel('速度',{exact:true}).selectOption('fast');
  await page.setViewportSize({width:430,height:850});await range(page,true);await expect(page.getByRole('button',{name:'速度制限の変更を確定',exact:true})).toBeEnabled();await page.screenshot({path:out('ja-narrow-slope-preview.png'),fullPage:true});await confirm(page,true);
  await page.setViewportSize({width:1280,height:900});await page.locator('canvas').focus();await page.keyboard.press('i');await canvasHash(page);
  const q=await page.locator('canvas').evaluate(el=>{const c=el as HTMLCanvasElement,r=c.getBoundingClientRect(),[cx,cy]=c.dataset.camera!.split(',').map(Number),zoom=Number(c.dataset.zoom);return{x:r.x+r.width/2+(16*(30+27-20)+16.5-cx)*zoom,y:r.y+r.height/2+(8*(-30+27+20)-16+5.5-cy)*zoom};});
  await page.mouse.click(q.x,q.y);await expect(page.getByTestId('railway-component')).toHaveText('速度制限');await expect(page.getByTestId('railway-definition')).toContainText(icons.fast.id);await page.screenshot({path:out('ja-speed-inspector.png'),fullPage:true});
 }finally{await replace(page,original,true);}
});
