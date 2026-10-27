import { expect,test,type Page } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { createHash } from 'node:crypto';
import { ready,openPanel,closePanel } from './ux-helpers.ts';
const bootPath='/api/bootstrap?protocolVersion=3&contentVersion=reference-spatial-content-2';
const bootstrap=async(page:Page)=>(await(await page.request.get(bootPath)).json()).snapshot;
async function at(page:Page,p:{x:number;y:number;z:number}) {
  return page.locator('canvas').evaluate((el,p)=>{
    const canvas=el as HTMLCanvasElement,r=canvas.getBoundingClientRect(),[cx,cy]=canvas.dataset.camera!.split(',').map(Number),zoom=Number(canvas.dataset.zoom);
    return{x:r.x+r.width/2+(16*(p.x+p.y-31)+16-cx)*zoom,y:r.y+r.height/2+(8*(p.y-p.x+31)-16*p.z+8-cy)*zoom};
  },p);
}
async function target(page:Page,p:{x:number;y:number;z:number}){const q=await at(page,p);await page.mouse.click(q.x,q.y);}
async function checker(page:Page){await page.getByRole('button',{name:'Check a location',exact:true}).click();await page.getByLabel('Track level',{exact:true}).selectOption('0');}

test('prepared bundled catalog and decoded source sheets ship separately from the selected reference world',async({page})=>{
  await page.goto('/');await ready(page);const before=await bootstrap(page),canvas=page.locator('canvas');
  await canvas.evaluate(el=>el.setAttribute('data-instance','prepared-preserved'));
  const response=await page.request.get('/content/bundled-v2/catalog.json');expect(response.ok()).toBeTruthy();
  const bytes=await response.body(),catalog=JSON.parse(bytes.toString());
  const hash=(await(await page.request.get('/content/bundled-v2/catalog.sha256')).text()).trim();
  expect(createHash('sha256').update(bytes).digest('hex')).toBe(hash);
  expect(catalog.source.revision).toBe('883cf7d81cf49db25ff38cd3a25d0f140afe31a0');
  expect(catalog.counts.contributions).toBe(448);expect(catalog.status).toBe('prepared; not selected by a world profile');
  const rail=catalog.assets.find((a:any)=>a.path==='trunk/core/res/RailRoads.bmp');
  for(const mode of ['day','night']){
    const image=await page.request.get(rail[mode].url);expect(image.ok()).toBeTruthy();
    expect(createHash('sha256').update(await image.body()).digest('hex')).toBe(rail[mode].sha256);
    expect(await page.evaluate(async url=>{const image=new Image();image.src=url;await image.decode();return [image.naturalWidth,image.naturalHeight];},rail[mode].url)).toEqual([rail.width,rail.height]);
  }
  expect(before.requiredPacks.every((p:any)=>p.id!=='exav-stable-bundled')).toBeTruthy();
  expect(await bootstrap(page)).toEqual(before);await expect(canvas).toHaveAttribute('data-instance','prepared-preserved');
});

test('default live H/V map renders, picks source rails and keeps unsupported operations unavailable',async({page})=>{
  const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
  await page.goto('/');await ready(page);
  const canvas=page.locator('canvas');await expect(canvas).toHaveAttribute('data-coordinate-system','hv');await expect(canvas).toHaveAttribute('data-ruleset','reference-spatial-4');
  await expect(page.getByTestId('reference-world')).toHaveText('Spatial reference world');
  await expect(page.getByRole('button',{name:'Run',exact:false})).toBeEnabled();await expect(page.getByRole('button',{name:'Remove track',exact:true})).toBeDisabled();
  const before=await bootstrap(page);expect(before.tracks).toHaveLength(32);expect(before.trains).toHaveLength(0);
  expect(before.entities).toHaveLength(32);
  for(const track of before.tracks){const entity=before.entities.find((e:any)=>e.id===track.entityId);expect(entity.anchor).toEqual(track.position);expect(entity.definition.recipeId).toBe(track.shape);}
  await canvas.evaluate(el=>el.setAttribute('data-instance','canonical-preserved'));
  await target(page,{x:31,y:24,z:0});await expect(page.getByTestId('selected-object')).toHaveText(`Track · ${before.tracks.find((t:any)=>t.position.x===31&&t.position.y===24).entityId} · 31, 24, 0`);await expect(page.locator('.primary-panel')).toContainText('Normal rail pattern 6');
  await openPanel(page,'session');await expect(page.locator('.primary-panel')).toContainText('Economy, trains and construction are not available yet.');
  await expect(canvas).toHaveAttribute('data-instance','canonical-preserved');expect(await bootstrap(page)).toEqual(before);expect(errors).toEqual([]);
});

test('source H/V targeting checks eight-direction snapping without changing the world',async({page})=>{
  await page.goto('/');await ready(page);const before=await bootstrap(page);await checker(page);
  await target(page,{x:27,y:24,z:0});await expect(page.getByTestId('geometry-status')).toContainText('within the map');
  await page.getByRole('button',{name:'Use as alignment anchor',exact:true}).click();await page.getByLabel('Align target',{exact:true}).selectOption('8');
  await target(page,{x:29,y:29,z:0});await expect(page.getByTestId('preview-position')).toHaveText('29, 26, 0');await expect(page.getByTestId('preview-heading')).toContainText('South-east');
  await expect(page.getByRole('button',{name:'Confirm track change',exact:true})).toBeDisabled();expect(await bootstrap(page)).toEqual(before);
  const directory=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'canonical');mkdirSync(directory,{recursive:true});await page.screenshot({path:join(directory,'reference-en.png'),fullPage:true});
});

test('real Save 5 download, upload confirmation and socket epoch replacement preserve the canvas',async({page})=>{
  await page.goto('/');await ready(page);const before=await bootstrap(page),canvas=page.locator('canvas');await canvas.evaluate(el=>el.setAttribute('data-instance','save-preserved'));
  const save=await(await page.request.get('/api/saves/download')).body();expect(JSON.parse(save.toString()).saveVersion).toBe(5);
  await openPanel(page,'saves');await expect(page.getByRole('button',{name:'Save slot',exact:true})).toBeEnabled();
  await page.getByLabel('Upload save',{exact:true}).setInputFiles({name:'canonical.json',mimeType:'application/json',buffer:save});
  await expect(page.getByRole('dialog')).toBeVisible();await page.getByRole('button',{name:'Confirm load',exact:true}).click();
  await expect(canvas).toHaveAttribute('data-world-epoch',String(BigInt(before.worldEpoch)+1n));await expect(canvas).toHaveAttribute('data-instance','save-preserved');
  expect(await(await page.request.get('/api/saves/download')).body()).toEqual(save);
});

test('prototype and corrupt saves report errors without replacement confirmation or mutation',async({page})=>{
  await page.goto('/');await ready(page);const before=await bootstrap(page);await openPanel(page,'saves');
  for(const [body,message] of [['{"saveVersion":1}','not supported'],['{"saveVersion":2}','not supported'],['{"saveVersion":3}','not supported'],['{"saveVersion":4}','not supported'],['{broken','Invalid save data']] as const){
    await expect(page.getByLabel('Upload save',{exact:true})).toBeEnabled();
    await page.getByLabel('Upload save',{exact:true}).setInputFiles({name:'unsupported.json',mimeType:'application/json',buffer:Buffer.from(body)});
    await expect(page.getByTestId('save-feedback')).toContainText(message);await expect(page.getByRole('dialog',{name:'Replace the current game with this save?',exact:true})).toHaveCount(0);
    expect(await bootstrap(page)).toEqual(before);
  }
});

test('random state validation rejects incompatible saves before confirmation and keeps the current epoch',async({page})=>{
  await page.goto('/');await ready(page);await openPanel(page,'saves');
  const before=await bootstrap(page),bytes=await(await page.request.get('/api/saves/download')).body();
  for(const [mutation,message] of [['algorithm','not supported'],['duplicate','Invalid save data'],['zero','Invalid save data']] as const){
    const save=JSON.parse(bytes.toString());
    if(mutation==='algorithm')save.random.algorithm='future';
    if(mutation==='duplicate')save.random.streams[1].id=save.random.streams[0].id;
    if(mutation==='zero')save.random.streams[0].state.values.fill(0);
    await expect(page.getByLabel('Upload save',{exact:true})).toBeEnabled();
    await page.getByLabel('Upload save',{exact:true}).setInputFiles({name:'random.json',mimeType:'application/json',buffer:Buffer.from(JSON.stringify(save))});
    await expect(page.getByTestId('save-feedback')).toContainText(message);
    await expect(page.getByRole('dialog',{name:'Replace the current game with this save?',exact:true})).toHaveCount(0);
    expect(await bootstrap(page)).toEqual(before);
    expect(await(await page.request.get('/api/saves/download')).body()).toEqual(bytes);
  }
});

test('new epoch clears a pending H/V preview and discards its delayed response',async({page})=>{
  let release=()=>{},waiting=false;const held=new Promise<void>(r=>{release=r;});
  await page.route('**/api/spatial/preview?*',async route=>{try{const response=await route.fetch();waiting=true;await held;await route.fulfill({response});}catch{}});
  try{
    await page.goto('/');await ready(page);await checker(page);await target(page,{x:27,y:24,z:0});await expect.poll(()=>waiting).toBe(true);
    const before=await bootstrap(page),save=await(await page.request.get('/api/saves/download')).body();
    expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+before.worldEpoch,{data:save})).ok()).toBe(true);
    await expect(page.locator('canvas')).toHaveAttribute('data-world-epoch',String(BigInt(before.worldEpoch)+1n));release();await expect(page.getByTestId('preview-position')).toHaveCount(0);
  }finally{release();}
});

test('unknown canonical content pack blocks reveal before the live canvas',async({page})=>{
  await page.route('**/api/bootstrap?*',async route=>{const response=await route.fetch(),body=await response.json();body.requiredPacks[0].catalogHash='0'.repeat(64);body.snapshot.requiredPacks[0].catalogHash='0'.repeat(64);await route.fulfill({response,json:body});});
  await page.goto('/');await expect(page.getByRole('heading',{name:'Update required',exact:true})).toBeVisible();await expect(page.locator('canvas')).toHaveAttribute('data-scene','demo');
});

test('canonical Japanese presentation fits narrow DPR 2 and retains canvas through panels',async({browser})=>{
  const context=await browser.newContext({viewport:{width:640,height:1000},deviceScaleFactor:2,locale:'ja-JP'});
  try{
    const page=await context.newPage();await page.goto('/');await ready(page,'ja');await expect(page.getByTestId('reference-world')).toHaveText('空間参照ワールド');
    await openPanel(page,'session','ja');await expect(page.locator('.primary-panel')).toContainText('8方向の環状線');await closePanel(page);
    await page.getByRole('button',{name:'ゲーム ▾',exact:true}).click();
    await expect(page.getByTestId('menu-game-time')).toContainText('年4月1日(土)');
    await page.keyboard.press('Escape');
    await page.getByRole('button',{name:'位置を確認',exact:true}).click();await page.getByLabel('線路の高さ',{exact:true}).selectOption('0');
    await target(page,{x:27,y:24,z:0});await expect(page.getByTestId('geometry-status')).toContainText('対象はマップの範囲内です。');
    await page.getByRole('button',{name:'方向合わせの基準にする',exact:true}).click();await page.getByLabel('対象の方向合わせ',{exact:true}).selectOption('8');
    await target(page,{x:29,y:29,z:0});await expect(page.getByTestId('preview-heading')).toContainText('南東');
    expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
    const directory=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'canonical');mkdirSync(directory,{recursive:true});await page.screenshot({path:join(directory,'reference-ja-2x.png'),fullPage:true});
  }finally{await context.close();}
});

test('canonical Observer can inspect and query geometry while save endpoints enforce roles',async({browser})=>{
  const context=await browser.newContext();
  try{
    const page=await context.newPage();expect((await page.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);
    await page.goto('/');await ready(page);await expect(page.getByTestId('participant-role')).toHaveText('Observer');
    const before=await bootstrap(page);
    const response=await page.request.post('/api/spatial/preview?protocolVersion=3&contentVersion=reference-spatial-content-2',{data:{expectedWorldEpoch:before.worldEpoch,position:{x:27,y:24,z:0},anchor:null,snap:0}});
    expect(response.ok()).toBe(true);expect((await response.json()).storage).toEqual({h:10,v:28,z:0});
    expect((await page.request.post('/api/saves/slot1/save?expectedWorldEpoch='+before.worldEpoch)).status()).toBe(403);
    expect((await page.request.get('/api/saves/download')).status()).toBe(403);
    expect(await bootstrap(page)).toEqual(before);
  }finally{await context.close();}
});

test('save availability must be known before upload, and Cancel never sends a replacement',async({page})=>{
  let release=()=>{},waiting=false,uploads=0;const held=new Promise<void>(r=>{release=r;});
  await page.route('**/api/saves/status',async route=>{const response=await route.fetch();waiting=true;await held;await route.fulfill({response});});
  page.on('request',request=>{if(new URL(request.url()).pathname==='/api/saves/upload')uploads++;});
  try{
    await page.goto('/');await ready(page);const before=await bootstrap(page),save=await(await page.request.get('/api/saves/download')).body();
    await openPanel(page,'saves');await expect.poll(()=>waiting).toBe(true);
    await expect(page.getByLabel('Upload save',{exact:true})).toBeDisabled();
    release();await expect(page.getByLabel('Upload save',{exact:true})).toBeEnabled();
    await page.getByLabel('Upload save',{exact:true}).setInputFiles({name:'canonical.json',mimeType:'application/json',buffer:save});
    const modal=page.getByRole('dialog',{name:'Replace the current game with this save?',exact:true});await expect(modal).toBeVisible();
    expect(uploads).toBe(0);await modal.getByRole('button',{name:'Cancel',exact:true}).click();await expect(modal).toHaveCount(0);
    expect(uploads).toBe(0);expect(await bootstrap(page)).toEqual(before);
  }finally{release();}
});

test('canonical clock step, shared run/pause, panel navigation and Save 5 continuation',async({page,browser})=>{
  const observerContext=await browser.newContext();
  try{
    await page.goto('/');await ready(page);
    const original=await(await page.request.get('/api/saves/download')).json();
    const before=await bootstrap(page);
    const save={...original,clock:{absoluteMinute:'130079',nextOrder:'0',pending:[]},paused:true};
    expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+before.worldEpoch,{data:save})).ok()).toBe(true);
    await expect(page.getByTestId('game-time')).toHaveText('Year 1 · 4/1 (Sat) 07:50');
    await page.getByRole('button',{name:'Step',exact:false}).first().click();
    await expect(page.getByTestId('game-time')).toHaveText('Year 1 · 4/1 (Sat) 08:00');
    expect((await bootstrap(page)).tick).toBe('130080');
    const observer=await observerContext.newPage();
    expect((await observer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);
    await observer.goto('/');await ready(observer);
    await expect(observer.getByRole('button',{name:'Run',exact:false})).toBeDisabled();
    await page.getByRole('button',{name:'Run',exact:false}).click();
    await expect(observer.getByTestId('clock-state')).toHaveText('Running');
    const running=await bootstrap(page);await openPanel(page,'settings');
    await expect.poll(async()=>BigInt((await bootstrap(page)).tick)>BigInt(running.tick)).toBe(true);
    await page.getByRole('button',{name:'Pause',exact:false}).click();
    await expect(observer.getByTestId('clock-state')).toHaveText('Paused');
    const captured=await(await page.request.get('/api/saves/download')).json();
    expect(captured.paused).toBe(true);expect(captured.clock.absoluteMinute).toBe((await bootstrap(page)).tick);
    const paused=await bootstrap(page);
    expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+paused.worldEpoch,{data:captured})).ok()).toBe(true);
    await expect(page.locator('canvas')).toHaveAttribute('data-world-epoch',String(BigInt(paused.worldEpoch)+1n));
    expect((await bootstrap(page)).tick).toBe(captured.clock.absoluteMinute);
  }finally{
    const pause=page.getByRole('button',{name:'Pause',exact:false});if(await pause.count())await pause.click();
    await observerContext.close();
  }
});

test('dangling entity ownership blocks reveal before the live canvas',async({page})=>{
  await page.route('**/api/bootstrap?*',async route=>{
    const response=await route.fetch(),body=await response.json();body.snapshot.tracks[0].entityId='999';
    await route.fulfill({response,json:body});
  });
  await page.goto('/');await expect(page.getByRole('heading',{name:'Update required',exact:true})).toBeVisible();
  await expect(page.locator('canvas')).toHaveAttribute('data-scene','demo');
});

test('invalid entity graphs report localized save errors before confirmation and preserve current state',async({page})=>{
  await page.goto('/');await ready(page);await openPanel(page,'saves');
  const before=await bootstrap(page),bytes=await(await page.request.get('/api/saves/download')).body();
  for(const mutation of ['owner','duplicate','allocator'] as const){
    const save=JSON.parse(bytes.toString());
    if(mutation==='owner')save.rails[0].entityId='999';
    if(mutation==='duplicate')save.spatial.entities[1].id=save.spatial.entities[0].id;
    if(mutation==='allocator')save.spatial.nextEntityId='1';
    await expect(page.getByLabel('Upload save',{exact:true})).toBeEnabled();
    await page.getByLabel('Upload save',{exact:true}).setInputFiles({name:'entities.json',mimeType:'application/json',buffer:Buffer.from(JSON.stringify(save))});
    await expect(page.getByTestId('save-feedback')).toContainText('Invalid save data');
    await expect(page.getByRole('dialog',{name:'Replace the current game with this save?',exact:true})).toHaveCount(0);
    expect(await bootstrap(page)).toEqual(before);expect(await(await page.request.get('/api/saves/download')).body()).toEqual(bytes);
  }
});

test('bundled recipe gallery renders every source frame in all season/light variants with attributed paths',async({page})=>{
  test.setTimeout(300_000);
  const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
  const before=await bootstrap(page);
  await page.goto('/content-gallery.html');
  const canvas=page.locator('#composition');await expect(canvas).toHaveAttribute('data-ready','true');
  const catalog=await (await page.request.get('/content/bundled-v2/catalog.json')).json();
  const result=await page.evaluate(async()=>{
    const recipe=document.getElementById('recipe') as HTMLSelectElement,season=document.getElementById('season') as HTMLSelectElement,
      night=document.getElementById('night') as HTMLInputElement,canvas=document.getElementById('composition') as HTMLCanvasElement;
    let variants=0,frames=0,diagnostics=0;
    const wait=async(id:string,key:string)=>{
      const start=performance.now();
      while(canvas.dataset.ready!=='true'||canvas.dataset.recipe!==id||canvas.dataset.variant!==key) {
        if(performance.now()-start>10000)throw new Error(document.getElementById('status')!.textContent??'Gallery timeout');
        await new Promise(requestAnimationFrame);
      }
    };
    for(const option of [...recipe.options]) {
      for(const s of ['spring','summer','autumn','winter'])for(const n of [false,true]) {
        recipe.value=option.value;season.value=s;night.checked=n;recipe.dispatchEvent(new Event('change'));
        await wait(option.value,`${s}/${n?'night':'day'}`);variants++;
        frames+=document.querySelectorAll('#frames canvas').length;diagnostics+=document.querySelectorAll('[data-diagnostic]').length;
      }
    }
    return {variants,frames,diagnostics};
  });
  expect(result.variants).toBe(catalog.renderRecipes.length*8);expect(result.frames).toBeGreaterThan(20000);
  expect(result.diagnostics).toBeGreaterThan(0);expect(errors).toEqual([]);
  const example=catalog.renderRecipes.find((r:any)=>r.handler==='variable-height'&&r.status==='ready');
  await page.locator('#recipe').selectOption(example.id);await expect(canvas).toHaveAttribute('data-recipe',example.id);
  await expect(page.locator('#credits')).toContainText(example.pluginId);
  const directory=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'recipes');mkdirSync(directory,{recursive:true});
  await page.screenshot({path:join(directory,'variable-night-winter.png'),fullPage:true});
  const provenance=await(await page.request.get('/content/bundled-v2/provenance.json')).body();
  expect(createHash('sha256').update(provenance).digest('hex')).toBe(catalog.provenance.sha256);
  for(const metadata of catalog.pluginMetadata){const response=await page.request.get(metadata.url);expect(response.ok()).toBeTruthy();expect(createHash('sha256').update(await response.body()).digest('hex')).toBe(metadata.sha256);}
  const land=catalog.renderRecipes.find((r:any)=>r.handler==='land-0d'&&r.status==='ready');
  await page.locator('#recipe').selectOption(land.id);await expect(canvas).toHaveAttribute('data-recipe',land.id);
  const sample=await canvas.evaluate(el=>{
    const c=el as HTMLCanvasElement,rgba=c.getContext('2d')!.getImageData(0,0,c.width,c.height).data;
    for(let i=0;i<rgba.length;i+=4)if(rgba[i]!==199||rgba[i+1]!==213||rgba[i+2]!==194)return {x:(i/4)%c.width,y:Math.floor(i/4/c.width),rgb:[...rgba.slice(i,i+3)]};
    throw new Error('No opaque land sprite');
  });
  await page.locator('#effect').selectOption('alpha');await expect(canvas).toHaveAttribute('data-ready','true');
  const pixel=()=>canvas.evaluate((el,p)=>[...(el as HTMLCanvasElement).getContext('2d')!.getImageData(p.x,p.y,1,1).data].slice(0,3),sample);
  expect(await pixel()).toEqual(sample.rgb.map((v:number,i:number)=>(v>>1)+([199,213,194][i]>>1)));
  await page.locator('#effect').selectOption('shape');await expect(canvas).toHaveAttribute('data-ready','true');
  expect(await pixel()).toEqual([255,212,90]);
  expect(await bootstrap(page)).toEqual(before);
});

test('bundled MIDI and bells produce audio only after gestures, with localized controls and a preserved world',async({page})=>{
  await page.addInitScript(()=>{
    const audioWindow=window as unknown as {__audioPeak:number;__audioContexts:AudioContext[]};
    audioWindow.__audioPeak=0;audioWindow.__audioContexts=[];
    const original=AudioNode.prototype.connect;
    AudioNode.prototype.connect=function(destination:AudioNode|AudioParam,...args:number[]) {
      if(destination instanceof AudioDestinationNode) {
        const context=this.context as AudioContext;
        if(!audioWindow.__audioContexts.includes(context))audioWindow.__audioContexts.push(context);
        const analyser=context.createAnalyser();analyser.fftSize=256;
        original.call(this,analyser);
        const values=new Float32Array(256);
        const timer=setInterval(()=>{
          if(context.state==='closed'){clearInterval(timer);return;}
          analyser.getFloatTimeDomainData(values);
          audioWindow.__audioPeak=Math.max(audioWindow.__audioPeak,...values.map(Math.abs));
        },10);
      }
      return original.call(this,destination as AudioNode,...args);
    } as typeof AudioNode.prototype.connect;
  });
  const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
  await page.goto('/');await ready(page);const before=await bootstrap(page);
  await page.locator('canvas').evaluate(el=>{el.dataset.identity='audio-preserved';});
  const license=await page.request.get('/vendor/webaudio-tinysynth/LICENSE.txt');
  expect(license.ok()).toBe(true);expect(await license.text()).toContain('Apache License');
  await openPanel(page,'settings');
  const tracks=page.getByLabel('Music',{exact:true});
  await expect(tracks.locator('option')).toHaveCount(3);
  await expect(page.getByTestId('music-status')).toHaveAttribute('data-state','stopped');
  expect(await page.evaluate(()=>(window as any).__audioContexts.length)).toBe(0);
  for(const id of await tracks.locator('option').evaluateAll(options=>options.map(o=>(o as HTMLOptionElement).value))) {
    await tracks.selectOption(id);await page.evaluate(()=>{(window as any).__audioPeak=0;});
    await page.getByRole('button',{name:'Play music',exact:true}).click();
    await expect(page.getByTestId('music-status')).toHaveAttribute('data-state','playing');
    await expect.poll(()=>page.evaluate(()=>(window as any).__audioPeak),{timeout:10000}).toBeGreaterThan(.0001);
    await page.getByRole('button',{name:'Stop music',exact:true}).click();
    await expect(page.getByTestId('music-status')).toHaveAttribute('data-state','stopped');
  }
  const bells=page.getByLabel('Departure bells',{exact:true});await expect(bells.locator('option')).toHaveCount(6);
  for(const id of await bells.locator('option').evaluateAll(options=>options.map(o=>(o as HTMLOptionElement).value))) {
    await bells.selectOption(id);await page.getByRole('button',{name:'Preview bell',exact:true}).click();
    await expect.poll(()=>page.evaluate(()=>(window as any).__audioContexts.at(-1)?.state)).toBe('closed');
  }
  const evidence=join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!,'audio');mkdirSync(evidence,{recursive:true});
  await page.screenshot({path:join(evidence,'settings-en.png'),fullPage:true});
  await page.getByLabel('Language',{exact:true}).selectOption('ja');
  await expect(page.locator('html')).toHaveAttribute('lang','ja');await expect(page.locator('html')).toHaveAttribute('dir','ltr');
  await expect(page.getByLabel('音楽',{exact:true}).locator('option').first()).toHaveText('開拓');
  await page.setViewportSize({width:360,height:800});
  await page.getByRole('button',{name:'音楽を再生',exact:true}).click();
  await expect(page.getByTestId('music-status')).toHaveText('再生中');
  await page.screenshot({path:join(evidence,'settings-ja-narrow.png'),fullPage:true});
  await page.getByRole('button',{name:'ベルを試聴',exact:true}).click();
  await expect.poll(()=>page.evaluate(()=>(window as any).__audioContexts.at(-1)?.state)).toBe('closed');
  await page.screenshot({path:join(evidence,'settings-ja-bells.png'),fullPage:true});
  await page.locator('.primary-panel .panel-title button:last-child').click();
  await expect(page.locator('canvas')).toHaveAttribute('data-identity','audio-preserved');
  await openPanel(page,'settings','ja');await expect(page.getByTestId('music-status')).toHaveText('再生中');
  // Visibility stops playback without a replay on the next visible frame.
  await page.evaluate(()=>{Object.defineProperty(document,'hidden',{configurable:true,value:true});document.dispatchEvent(new Event('visibilitychange'));});
  await expect(page.getByTestId('music-status')).toHaveText('停止中');
  await page.evaluate(()=>{Object.defineProperty(document,'hidden',{configurable:true,value:false});document.dispatchEvent(new Event('visibilitychange'));});
  await expect(page.getByTestId('music-status')).toHaveText('停止中');
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  expect(await bootstrap(page)).toEqual(before);expect(errors).toEqual([]);
});

test('music hash failures are localized and retryable; cancelled loads never start later',async({page})=>{
  await page.goto('/');await ready(page);await openPanel(page,'settings');
  await expect(page.getByRole('button',{name:'Play music',exact:true})).toBeEnabled();
  await page.route('**/*.mid',route=>route.fulfill({body:'altered MIDI'}));
  await page.getByRole('button',{name:'Play music',exact:true}).click();
  await expect(page.getByTestId('music-status')).toHaveAttribute('data-state','failed');
  await page.unroute('**/*.mid');
  let release=()=>{},waiting=false;const held=new Promise<void>(r=>{release=r;});
  await page.route('**/*.mid',async route=>{
    try {const response=await route.fetch();waiting=true;await held;await route.fulfill({response});}catch{/* Cancel aborts this request. */}
  });
  try {
    await page.getByRole('button',{name:'Play music',exact:true}).click();await expect.poll(()=>waiting).toBe(true);
    await page.getByRole('button',{name:'Stop music',exact:true}).click();release();
    await expect(page.getByTestId('music-status')).toHaveAttribute('data-state','stopped');
    await page.unroute('**/*.mid');
    await page.getByRole('button',{name:'Play music',exact:true}).click();
    await expect(page.getByTestId('music-status')).toHaveAttribute('data-state','playing');
    await page.getByRole('button',{name:'Stop music',exact:true}).click();
  }finally{release();}
});

test('gallery language switching keeps the composition canvas, decoded images and selected source identities',async({page})=>{
  await page.goto('/content-gallery.html');const canvas=page.locator('#composition');await expect(canvas).toHaveAttribute('data-ready','true');
  await canvas.evaluate(el=>{el.dataset.identity='locale-preserved';});
  const recipe=await page.locator('#recipe').inputValue();
  await page.getByLabel('Language',{exact:true}).selectOption('ja');
  await expect(page.getByRole('heading',{name:'同梱画像レシピ',exact:true})).toBeVisible();
  await expect(canvas).toHaveAttribute('data-ready','true');await expect(canvas).toHaveAttribute('data-identity','locale-preserved');
  await expect(page.locator('#recipe')).toHaveValue(recipe);await expect(page.locator('#status')).toContainText('レシピ');
  await page.reload();await expect(page.locator('html')).toHaveAttribute('lang','ja');
  await expect(page.getByRole('heading',{name:'同梱画像レシピ',exact:true})).toBeVisible();
});
