import {execFileSync} from 'node:child_process';
import {readFileSync,mkdirSync} from 'node:fs';
import {test,expect,type Page} from './fixtures.ts';
import {canonicalState as state,ready,openPanel,closePanel} from './browser-helpers.ts';
const en=JSON.parse(readFileSync('src/locales/en/app.json','utf8')),ja=JSON.parse(readFileSync('src/locales/ja/app.json','utf8'));
const evidence=process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!;
async function setup(page:Page,locale='en'){
 const before=await state(page),input=readFileSync('tests/fixtures/fleet-information-v10.json');
 const response=await page.request.post('/api/saves/upload?expectedWorldEpoch='+before.worldEpoch,{data:input});expect(response.ok(),await response.text()).toBe(true);
 await page.goto('/');await ready(page);
 if(locale==='ja'){await openPanel(page,'settings');await page.getByLabel(en.language,{exact:true}).selectOption('ja');await closePanel(page);}
 return state(page);
}
for(const locale of ['en','ja'] as const)test(`${locale} information, local labels, Move, Follow and replacement`,async({page})=>{
 const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));const text=locale==='ja'?ja:en;
 await page.addInitScript(()=>{const original=HTMLImageElement.prototype.decode;(window as any).__decodes=0;HTMLImageElement.prototype.decode=function(){(window as any).__decodes++;return original.call(this);};});
 const initial=await setup(page,locale),train=initial.mixed.fleet.trains[1],unplaced=initial.mixed.fleet.trains[0];
 if(locale==='ja'){await page.setViewportSize({width:390,height:844});await page.evaluate(()=>document.documentElement.style.fontSize='200%');}
 const bytes=await(await page.request.get('/api/saves/download')).body();
 await openPanel(page,'view',locale);const info=page.locator('.train-information'),canvas=page.locator('.world-canvas'),selector=info.getByLabel(text.trainSelect,{exact:true});
 await selector.selectOption(train.id);await expect(info.getByTestId('information-capacity')).toHaveText('0');await expect(info.getByTestId('information-passengers')).toHaveText('0 (—%)');
 await info.getByRole('button',{name:text.trainMove,exact:true}).click();await expect(canvas).toHaveAttribute('data-following-train','');
 await info.getByRole('button',{name:text.trainFollow,exact:true}).click();await expect(canvas).toHaveAttribute('data-following-train',train.id);
 const decodes=await page.evaluate(()=>(window as any).__decodes);
 await page.getByLabel(text.trainNamesVisible,{exact:true}).check();await page.getByLabel(text.trainNameFont,{exact:true}).selectOption('monospace');
 await page.getByLabel(text.trainNameSize,{exact:true}).fill('18');await page.getByLabel(text.trainNameSize,{exact:true}).press('Tab');await page.getByLabel(text.trainNameColor,{exact:true}).fill('#12abcd');
 expect(await page.evaluate(()=>(window as any).__decodes)).toBe(decodes);expect(await(await page.request.get('/api/saves/download')).body()).toEqual(bytes);
 mkdirSync(evidence+'/information',{recursive:true});await page.screenshot({path:evidence+`/information/${locale}.png`});
 await closePanel(page);await canvas.focus();const camera=await canvas.getAttribute('data-camera-state');
 for(let i=0;i<3;i++){
  // HTTP state can publish before the originating client's receipt arrives.
  // Wait until that client can send again before pressing another shortcut.
  await expect(page.locator('.transport .step')).toBeEnabled();
  const tick=(await state(page)).tick;await canvas.focus();await page.keyboard.press('n');
  await expect.poll(async()=>(await state(page)).tick).toBe(String(BigInt(tick)+1n));
 }
 expect((await state(page)).mixed.fleet.trains[1].cars[0].state.location).not.toEqual(train.cars[0].state.location);
 await expect.poll(()=>canvas.getAttribute('data-camera-state')).not.toBe(camera);await expect(canvas).toHaveAttribute('data-following-train',train.id);
 await page.keyboard.press('+');await expect(canvas).toHaveAttribute('data-following-train',train.id);
 const box=(await canvas.boundingBox())!;await page.mouse.move(box.x+box.width*.3,box.y+box.height*.3);await page.mouse.down();await page.mouse.move(box.x+box.width*.3+40,box.y+box.height*.3+20,{steps:4});await page.mouse.up();await expect(canvas).toHaveAttribute('data-following-train','');
 await openPanel(page,'view',locale);await expect(selector).toHaveValue(train.id);await selector.selectOption(unplaced.id);
 await expect(info.getByRole('button',{name:text.trainMove,exact:true})).toBeDisabled();await expect(info.getByRole('button',{name:text.trainFollow,exact:true})).toBeDisabled();
 await selector.selectOption(train.id);await info.getByRole('button',{name:text.trainFollow,exact:true}).click();const before=await state(page);
 expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+before.worldEpoch,{data:bytes})).ok()).toBe(true);
 await expect(canvas).toHaveAttribute('data-following-train','');await ready(page,locale);await openPanel(page,'view',locale);await expect(selector).toHaveValue('');await expect(page.getByLabel(text.trainNamesVisible,{exact:true})).toBeChecked();expect(errors).toEqual([]);
});
test('color templates, exact XML, retained drafts, shared apply, observer and checkpoint',async({page,browser,baseURL,worldServer})=>{
 test.setTimeout(90000);const initial=await setup(page);await openPanel(page,'settings');const editor=page.locator('.train-color-controls'),template=editor.getByLabel(en.trainColorTemplate,{exact:true});
 await expect(template.locator('option')).toHaveCount(7);const saved=await(await page.request.get('/api/saves/download')).body();
 for(const option of await template.locator('option').evaluateAll(nodes=>nodes.map(n=>(n as HTMLOptionElement).value))){await template.selectOption(option);await expect(editor.getByRole('img',{name:en.trainColorDay,exact:true})).toBeVisible();await expect(editor.getByRole('img',{name:en.trainColorNight,exact:true})).toBeVisible();}
 await editor.getByLabel(en.trainColorBody,{exact:true}).fill('#a1b2c3');await expect(editor.getByLabel(en.trainColorXML,{exact:true})).toHaveValue(/base="161,178,195"/);
 const pending=page.waitForEvent('download');await editor.getByRole('button',{name:en.trainColorExport,exact:true}).click();const download=await pending;expect(download.suggestedFilename()).toBe('colorMap.xml');expect(readFileSync((await download.path())!,'utf8')).toContain('base="161,178,195"');
 await page.getByLabel(en.language,{exact:true}).selectOption('ja');await page.setViewportSize({width:390,height:844});await page.evaluate(()=>document.documentElement.style.fontSize='200%');await expect(editor.getByLabel(ja.trainColorBody,{exact:true})).toHaveValue('#a1b2c3');
 mkdirSync(evidence+'/information',{recursive:true});await editor.getByLabel(ja.trainColorBody,{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:evidence+'/information/colors-ja.png'});await closePanel(page);await openPanel(page,'settings','ja');await expect(editor.getByLabel(ja.trainColorBody,{exact:true})).toHaveValue('#a1b2c3');expect(await(await page.request.get('/api/saves/download')).body()).toEqual(saved);await page.getByLabel(ja.language,{exact:true}).selectOption('en');await page.setViewportSize({width:1280,height:900});await page.evaluate(()=>document.documentElement.style.fontSize='100%');
 const context=await browser.newContext({baseURL}),peer=await context.newPage();
 try{
  expect((await peer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);await peer.goto('/');await ready(peer);await openPanel(peer,'settings');const remote=peer.locator('.train-color-controls');
  await remote.getByLabel(en.trainColorLine1,{exact:true}).fill('#123456');await expect(remote.getByRole('button',{name:en.trainColorApply,exact:true})).toBeDisabled();
  await editor.getByRole('button',{name:en.trainColorApply,exact:true}).click();let dialog=page.getByRole('dialog',{name:en.trainColorApply,exact:true});await dialog.getByRole('button',{name:en.cancel,exact:true}).click();expect(await(await page.request.get('/api/saves/download')).body()).toEqual(saved);
  await editor.getByRole('button',{name:en.trainColorApply,exact:true}).click();dialog=page.getByRole('dialog',{name:en.trainColorApply,exact:true});await dialog.getByRole('button',{name:en.trainColorApply,exact:true}).click();await expect(dialog).toHaveCount(0);
  await expect(remote).toContainText(en.trainColorStale);await remote.getByRole('button',{name:en.resetDraft,exact:true}).click();await expect(remote.getByLabel(en.trainColorBody,{exact:true})).toHaveValue('#a1b2c3');
  const after=await state(page);await expect(page.locator('.world-canvas')).toHaveAttribute('data-presentation','ready');
  await expect.poll(()=>page.locator('.world-canvas').evaluate((canvas:any)=>{const ctx=canvas.getContext('2d'),pixels=ctx.getImageData(0,0,canvas.width,canvas.height).data;let n=0;for(let i=0;i<pixels.length;i+=4)if(pixels[i]===161&&pixels[i+1]===178&&pixels[i+2]===195)n++;return n;})).toBeGreaterThan(5);
  expect(after.mixed.fleet.colorTest.body).toBe('#a1b2c3');expect(after.tick).toBe(initial.tick);expect(after.mixed.fleet.trains).toEqual(initial.mixed.fleet.trains);
  mkdirSync(evidence+'/information',{recursive:true});await page.screenshot({path:evidence+'/information/colors-en.png'});
  const bytes=await(await page.request.get('/api/saves/download')).body();expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+after.worldEpoch,{data:bytes})).ok()).toBe(true);
  await page.reload();await ready(page);await openPanel(page,'settings');await expect(editor.getByLabel(en.trainColorBody,{exact:true})).toHaveValue('#a1b2c3');
  await worldServer.restart!();expect((await page.request.post('/api/access',{data:{secret:''}})).ok()).toBe(true);await page.reload();await ready(page);expect((await state(page)).mixed.fleet.colorTest).toEqual(after.mixed.fleet.colorTest);
 }finally{await context.close();}
});

test('all seven color templates preserve exact source palette, night shades, crop pixels and alpha picks',async({page})=>{
 const harness=execFileSync('deno',['run','-A','scripts/bundle-rendering-tests.ts'],{encoding:'utf8',maxBuffer:16*1024*1024});
 await page.route('**/__tests/train-colors.js',r=>r.fulfill({contentType:'text/javascript',body:harness}));await page.goto('/');await ready(page);
 const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
 const report=await page.evaluate(async catalog=>{
  const {TrainColorCatalog,defaultTrainColors,colorTestCarId,RecipeImageCache,instantiate}=await import('/__tests/train-colors.js');
  const cache=new RecipeImageCache(),builder=new TrainColorCatalog(cache),before=JSON.stringify(catalog);let pixels=0,picks=0;
  const source=[[0,0,255],[0,0,180],[0,255,0],[0,180,0],[255,0,0],[180,0,0],[255,255,0],[180,180,0]];
  const target=[[18,52,86],[12,36,60],[254,220,186],[179,155,131],[0,128,255],[0,90,180],[128,128,128],[90,90,90]];
  for(let n=1;n<=7;n++){
   const id=`{243D9F53-67FB-432E-B985-3B9B696D8CE0}-${n}`,config={...defaultTrainColors,template:{packId:catalog.packId,contributionId:id,recipeId:id},body:'#123456',line1:'#fedcba',line2:'#0080ff',line3:'#808080'};
   const derived=await builder.prepare(catalog,config),recipe=derived.renderRecipes.find((r:any)=>r.id===colorTestCarId);
   for(const night of [false,true]){
    const raw=await cache.get(catalog.pictures.find((p:any)=>p.id===id).variants[0][night?'night':'day'].image);
    const original=new OffscreenCanvas(raw.width,raw.height),paint=original.getContext('2d')!;paint.drawImage(raw.image,0,0);
    for(const angle of [0,6]){
     const layer=instantiate(recipe,'test',[0,0,0],{angle})[0],rect=layer.frame.rectangle,[x,y,w,h]=rect;
     const expected=paint.getImageData(x,y,w,h).data,scene=await cache.ready(derived,[layer],{season:'spring',night});
     const output=new OffscreenCanvas(w,h),ctx=output.getContext('2d')!;scene.draw(ctx,{x:0,y:0,zoom:1});const actual=ctx.getImageData(0,0,w,h).data;
     for(let i=0;i<expected.length;i+=4){
      let color=[expected[i],expected[i+1],expected[i+2]];const alpha=expected[i+3];
      if(alpha){
       const match=source.findIndex(s=>s.every((v:number,c:number)=>v===color[c]));
       const dark=night?source.findIndex(s=>s.every((v:number,c:number)=>Math.trunc(v/4)===color[c])):-1;
       if(match>=0)color=target[match];else if(dark>=0)color=target[dark].map((v:number)=>Math.trunc(v/4));
      }else color=[0,0,0];
      if(!color.every((v:number,c:number)=>v===actual[i+c])||actual[i+3]!==alpha)throw new Error(`Palette pixel mismatch ${n}/${night}/${angle}/${i}`);
      const p=i/4,hit=scene.pick(p%w+.5,Math.floor(p/w)+.5,{x:0,y:0,zoom:1});if(!!hit!==(alpha!==0))throw new Error('Alpha pick mismatch');pixels++;picks++;
     }
    }
   }
  }
  if(JSON.stringify(catalog)!==before)throw new Error('Prepared catalog mutated');return {pixels,picks};
 },catalog);
 expect(report).toEqual({pixels:7*2*2*32*32,picks:7*2*2*32*32});
});

test('stable TrainId follow survives Outside return and explicit reversal, then stops on withdrawal and sale',async({page})=>{
 const before=await state(page),input=readFileSync('tests/fixtures/fleet-follow-outside-v10.json');
 expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+before.worldEpoch,{data:input})).ok()).toBe(true);await page.goto('/');await ready(page);
 const initial=await state(page),train=initial.mixed.fleet.trains[0],canvas=page.locator('.world-canvas');
 await openPanel(page,'view');const info=page.locator('.train-information');await info.getByLabel(en.trainSelect,{exact:true}).selectOption(train.id);
 await expect(info).toContainText(en.trainOutside);await info.getByRole('button',{name:en.trainFollow,exact:true}).click();await expect(canvas).toHaveAttribute('data-following-train',train.id);
 await closePanel(page);await canvas.focus();for(let i=0;i<2;i++){const tick=(await state(page)).tick;await canvas.press('n');await expect.poll(async()=>(await state(page)).tick).toBe(String(BigInt(tick)+1n));}
 const returned=(await state(page)).mixed.fleet.trains[0];expect(returned.id).toBe(train.id);expect(returned.cars[0].id).toBe(train.cars[0].id);expect(returned.cars[0].state.heading).toBe(6);expect(returned.cars[0].state.kind).toBe('inside');await expect(canvas).toHaveAttribute('data-following-train',train.id);
 await openPanel(page,'fleet');await page.getByRole('button',{name:new RegExp('^'+en.ownedStock)}).click();
 await page.getByRole('button',{name:en.fleetReverse,exact:true}).click();await page.getByRole('button',{name:en.fleetReverseConfirm,exact:true}).click();
 await expect.poll(async()=>(await state(page)).mixed.fleet.trains[0].cars[0].id).not.toBe(train.cars[0].id);await expect(canvas).toHaveAttribute('data-following-train',train.id);
 await page.getByRole('button',{name:en.withdrawTrain,exact:true}).click();await page.getByRole('button',{name:en.withdrawConfirm,exact:true}).click();
 await expect(canvas).toHaveAttribute('data-following-train','');await page.getByRole('button',{name:en.stockSaleQuote,exact:true}).click();await page.getByRole('button',{name:en.stockConfirmSell,exact:true}).click();
 await openPanel(page,'view');await expect(info.getByLabel(en.trainSelect,{exact:true})).toHaveValue('');
});
