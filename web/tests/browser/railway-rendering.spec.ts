import {test,expect,type Page} from '@playwright/test';
import {readFileSync,mkdirSync} from 'node:fs';
import {join} from 'node:path';
import {ready,openPanel,closePanel,canonicalState} from './ux-helpers.ts';
const save=readFileSync('../prototype/protocol/saves/mixed-v8.json');
const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
async function replace(page:Page,data:Buffer) {const before=await canonicalState(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+before.worldEpoch,{data})).ok()).toBe(true);}
async function point(page:Page,x:number,y:number) {return page.locator('canvas').evaluate((el,p)=>{
 const canvas=el as HTMLCanvasElement,r=canvas.getBoundingClientRect(),[cx,cy]=canvas.dataset.camera!.split(',').map(Number),zoom=Number(canvas.dataset.zoom);
 return {x:r.x+r.width/2+(p.x-cx)*zoom,y:r.y+r.height/2+(p.y-cy)*zoom};
},{x,y});}
const output=(name:string)=>{const directory=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'railway');mkdirSync(directory,{recursive:true});return join(directory,name);};

test('mixed railway art matches prepared pixels, picks rail/pier IDs, cuts exactly and reopens in two peers with one canvas',async({page,browser})=>{
 await page.goto('/');await ready(page);const original=await(await page.request.get('/api/saves/download')).body();
 const peerContext=await browser.newContext(),peer=await peerContext.newPage();await peer.goto('/');await ready(peer);
 const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));let sheets=0;page.on('request',r=>{if(r.url().endsWith('.png')&&r.url().includes('/content/bundled-v2/'))sheets++;});
 const canvas=page.locator('canvas');await canvas.evaluate(el=>el.dataset.instance='railway-preserved');
 try {
  await replace(page,save);for(const p of [page,peer]){await expect(p.locator('canvas')).toHaveAttribute('data-ruleset','mixed-railway-1');await ready(p);}
  const before=await canonicalState(page);expect(await canonicalState(peer)).toEqual(before);const captured=await(await page.request.get('/api/saves/download')).body();expect(JSON.parse(captured.toString())).toEqual(JSON.parse(save.toString()));
  await expect(canvas).toHaveAttribute('data-assets','ready');await expect(page.getByRole('button',{name:'Remove',exact:true})).toBeEnabled();
  const p=await point(page,16.5,-55.5);await page.mouse.click(p.x,p.y);await expect(page.getByTestId('selected-object')).toHaveText('Railway · 7 · 1, 0, 4');
  await expect(page.getByTestId('railway-definition')).toHaveText('exav-stable-bundled · system:pattern/1 · system:pattern/1');
  const f=catalog.renderRecipes.find((r:any)=>r.id==='system:pattern/1').frames[0];
  const pixels=await canvas.evaluate(async(el,args)=>{
   const canvas=el as HTMLCanvasElement,r=canvas.getBoundingClientRect(),[cx,cy]=canvas.dataset.camera!.split(',').map(Number),zoom=Number(canvas.dataset.zoom),variant=canvas.dataset.terrainVariant!;
   const image=new Image();image.src=args.set[variant].image.url;await image.decode();const sheet=document.createElement('canvas');sheet.width=image.naturalWidth;sheet.height=image.naturalHeight;const ctx=sheet.getContext('2d')!;ctx.drawImage(image,0,0);
   const expected=[...ctx.getImageData(args.rectangle[0]+16,args.rectangle[1]+8,1,1).data];
   const x=(r.width/2+(16.5-cx)*zoom)*canvas.width/r.width,y=(r.height/2+(-55.5-cy)*zoom)*canvas.height/r.height;
   return {expected,actual:[...canvas.getContext('2d')!.getImageData(Math.floor(x),Math.floor(y),1,1).data]};
  },{set:catalog.surfaceSets[f.surfaceSet],rectangle:f.rectangle});expect(pixels.actual).toEqual(pixels.expected);
  await closePanel(page);const pier=await point(page,16.5,-40.5);await page.mouse.click(pier.x,pier.y);await expect(page.getByTestId('selected-object')).toHaveText('Railway · 8 · 1, 0, 3');await expect(page.getByTestId('railway-component')).toHaveText('Railroad pier');
  await page.screenshot({path:output('rail-pier-en.png'),fullPage:true});
  await openPanel(page,'view');await page.getByLabel('Height cut',{exact:true}).fill('3');await expect(canvas).toHaveAttribute('data-terrain-cut','3');await expect(canvas).toHaveAttribute('data-presentation','ready');await closePanel(page);
  const cut=await point(page,16.5,-55.5);await page.mouse.click(cut.x,cut.y);await expect(page.getByTestId('selected-object')).toContainText('Railway · 8');
  await openPanel(page,'view');await page.getByLabel('Height cut',{exact:true}).fill('5');await expect(canvas).toHaveAttribute('data-terrain-cut','5');
  // Retained-buffer readiness precedes completion of the requested cut.
  await expect(canvas).toHaveAttribute('data-presentation','ready');const hash=await canvas.evaluate(el=>el.toDataURL());
  await page.getByLabel('Night',{exact:true}).check();await expect(canvas).toHaveAttribute('data-terrain-variant','spring/night');
  await page.getByLabel('Winter ground',{exact:true}).check();await expect(canvas).toHaveAttribute('data-terrain-variant','winter/night');
  await page.getByLabel('Night',{exact:true}).uncheck();await page.getByLabel('Winter ground',{exact:true}).uncheck();await expect(canvas).toHaveAttribute('data-terrain-variant','spring/day');await expect(canvas).toHaveAttribute('data-presentation','ready');
  await expect.poll(()=>canvas.evaluate(el=>el.toDataURL())).toBe(hash);const decoded=sheets;
  await openPanel(page,'session');await openPanel(page,'help');expect(sheets).toBe(decoded);await expect(canvas).toHaveAttribute('data-instance','railway-preserved');
  expect(await canonicalState(page)).toEqual(before);expect((await(await page.request.get('/api/saves/download')).body())).toEqual(captured);
  await page.reload();await ready(page);expect((await(await page.request.get('/api/saves/download')).body())).toEqual(captured);expect(errors).toEqual([]);
 }finally {await replace(page,original);await peerContext.close();}
});

test('Japanese DPR2 railway inspector stays localized and failed railway media blocks live readiness',async({browser})=>{
 const context=await browser.newContext({viewport:{width:640,height:900},deviceScaleFactor:2,locale:'ja-JP'}),page=await context.newPage();
 await page.goto('/');await ready(page,'ja');const original=await(await page.request.get('/api/saves/download')).body();
 try {
  await replace(page,save);await expect(page.locator('canvas')).toHaveAttribute('data-ruleset','mixed-railway-1');await ready(page,'ja');
  const p=await point(page,16.5,-55.5);await page.mouse.click(p.x,p.y);await expect(page.getByTestId('selected-object')).toHaveText('鉄道 · 7 · 1, 0, 4');
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);await page.screenshot({path:output('rail-ja-dpr2.png'),fullPage:true});
  const frame=catalog.renderRecipes.find((r:any)=>r.id==='system:pattern/1').frames[0],url=catalog.surfaceSets[frame.surfaceSet]['spring/day'].image.url;
  await page.route('**'+url,route=>route.abort());await page.reload();await expect(page.locator('.command-bar')).toHaveCount(0);await expect(page.locator('canvas')).not.toHaveAttribute('data-assets','ready');
 }finally {await replace(page,original);await context.close();}
});

test('a real clock publication carrying a validated mixed removal invalidates rail pixels and picks without a terrain revision',async({page})=>{
 await page.goto('/');await ready(page);const original=await(await page.request.get('/api/saves/download')).body();
 try {
  await replace(page,save);await page.routeWebSocket('**/api/session?*',route=>{
   const server=route.connectToServer();server.onMessage(message=>{
    const body=JSON.parse(String(message));
    if(body.type==='delta'&&body.baseRevision==='6'){
     const delta=JSON.parse(readFileSync('../prototype/protocol/fixtures/delta-mixed.json','utf8'));
     route.send(JSON.stringify({...delta,worldEpoch:body.worldEpoch,baseRevision:body.baseRevision,revision:body.revision,tick:body.tick}));
    }else route.send(message);
   });route.onMessage(message=>server.send(message));
  });
  await page.reload();await ready(page);const canvas=page.locator('canvas');await canvas.evaluate(el=>el.dataset.instance='delta-preserved');
  const p=await point(page,16.5,-55.5);await page.mouse.click(p.x,p.y);await expect(page.getByTestId('selected-object')).toContainText('Railway · 7');await closePanel(page);
  const before=await canvas.evaluate(el=>el.toDataURL());await page.getByRole('button',{name:'Step',exact:false}).first().click();
  await expect.poll(()=>canvas.evaluate(el=>el.toDataURL())).not.toBe(before);await expect(canvas).toHaveAttribute('data-instance','delta-preserved');
  await page.mouse.click(p.x,p.y);await expect(page.getByTestId('selected-object')).not.toContainText('Railway · 7');
 }finally {await replace(page,original);}
});
