import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { expect,test,type Page } from './fixtures.ts';
import { closePanel,openPanel,ready } from './browser-helpers.ts';
const ja=JSON.parse(readFileSync('src/locales/ja/app.json','utf8'));
const bootPath='/api/bootstrap?protocolVersion=10&contentVersion=reference-spatial-content-2&contentVersion=terrain-content-1&contentVersion=mixed-railway-content-1';
const bootstrap=async(page:Page)=>(await(await page.request.get(bootPath)).json()).snapshot;
// Rail-loop coverage deliberately loads its fixture; fresh services now generate terrain.
const referenceSave=readFileSync('../prototype/protocol/saves/reference-v11.json');
let previousWorld:Buffer;
test.beforeEach(async({request})=>{
  const before=await(await request.get(bootPath)).json();
  previousWorld=await(await request.get('/api/saves/download')).body();
  expect((await request.post('/api/saves/upload?expectedWorldEpoch='+before.worldEpoch,{data:referenceSave})).ok()).toBe(true);
});
test.afterEach(async({request})=>{
  const current=(await(await request.get(bootPath)).json()).snapshot;
  expect((await request.post('/api/saves/upload?expectedWorldEpoch='+current.worldEpoch,{data:previousWorld})).ok()).toBe(true);
});
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
  await expect(page.getByRole('button',{name:'Run',exact:false})).toBeEnabled();await expect(page.getByRole('button',{name:'Remove',exact:true})).toBeDisabled();
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

});

test('real Save 9 download, upload confirmation and socket epoch replacement preserve the canvas',async({page})=>{
  await page.goto('/');await ready(page);const before=await bootstrap(page),canvas=page.locator('canvas');await canvas.evaluate(el=>el.setAttribute('data-instance','save-preserved'));
  const save=await(await page.request.get('/api/saves/download')).body();expect(JSON.parse(save.toString()).saveVersion).toBe(11);
  await openPanel(page,'saves');await expect(page.getByRole('button',{name:'Save slot',exact:true})).toBeEnabled();
  await page.getByLabel('Upload save',{exact:true}).setInputFiles({name:'canonical.json',mimeType:'application/json',buffer:save});
  const confirmation=page.getByRole('dialog',{name:'Replace the current game with this save?',exact:true});
  await expect(confirmation).toBeVisible();await confirmation.getByRole('button',{name:'Confirm load',exact:true}).click();
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
  await page.route('**/api/bootstrap?*',async route=>{const response=await route.fetch(),body=await response.json();body.requiredPacks[0].catalogHash='0'.repeat(64);if(body.snapshot)body.snapshot.requiredPacks[0].catalogHash='0'.repeat(64);await route.fulfill({response,json:body});});
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
    await target(page,{x:27,y:24,z:0});await expect(page.getByTestId('geometry-status')).toContainText(ja.geometryChecked);
    await page.getByRole('button',{name:'方向合わせの基準にする',exact:true}).click();await page.getByLabel('対象の方向合わせ',{exact:true}).selectOption('8');
    await target(page,{x:29,y:29,z:0});await expect(page.getByTestId('preview-heading')).toContainText('南東');
    expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);

  }finally{await context.close();}
});

test('canonical Observer can inspect and query geometry while save endpoints enforce roles',async({browser})=>{
  const context=await browser.newContext();
  try{
    const page=await context.newPage();expect((await page.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);
    await page.goto('/');await ready(page);await expect(page.getByTestId('participant-role')).toHaveText('Observer');
    const before=await bootstrap(page);
    const response=await page.request.post('/api/spatial/preview?protocolVersion=10&contentVersion=reference-spatial-content-2',{data:{expectedWorldEpoch:before.worldEpoch,position:{x:27,y:24,z:0},anchor:null,snap:0}});
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

test('canonical clock step, shared run/pause, panel navigation and Save 9 continuation',async({page,browser})=>{
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
  await page.routeWebSocket('**/api/session?*',route=>{
    const server=route.connectToServer();
    server.onMessage(message=>{const body=JSON.parse(String(message));if(body.type==='join')body.snapshot.tracks[0].entityId='999';route.send(JSON.stringify(body));});
    route.onMessage(message=>server.send(message));
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

for(const exhaustive of [false,true])test(`bundled recipe gallery ${exhaustive?'exhaustive':'representative'} season/light variants with attributed paths`,{tag:exhaustive?'@extended':'@smoke'},async({page})=>{
  test.setTimeout(exhaustive?300_000:30_000);
  const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
  const before=await bootstrap(page);
  await page.goto('/content-gallery.html');
  const canvas=page.locator('#composition');await expect(canvas).toHaveAttribute('data-ready','true');
  const catalog=await (await page.request.get('/content/bundled-v2/catalog.json')).json();
  const selected=exhaustive?catalog.renderRecipes:[...new Map(catalog.renderRecipes.map((r:any)=>[`${r.handler}:${r.status}`,r])).values()];
  const ids=selected.map((r:any)=>r.id);
  const result=await page.evaluate(async(ids)=>{
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
    for(const id of ids) {
      for(const s of ['spring','summer','autumn','winter'])for(const n of [false,true]) {
        recipe.value=id;season.value=s;night.checked=n;recipe.dispatchEvent(new Event('change'));
        await wait(id,`${s}/${n?'night':'day'}`);variants++;
        frames+=document.querySelectorAll('#frames canvas').length;diagnostics+=document.querySelectorAll('[data-diagnostic]').length;
      }
    }
    return {variants,frames,diagnostics};
  },ids);
  expect(result.variants).toBe(ids.length*8);expect(result.frames).toBeGreaterThan(exhaustive?20000:0);
  expect(result.diagnostics).toBeGreaterThan(0);expect(errors).toEqual([]);
  const example=catalog.renderRecipes.find((r:any)=>r.handler==='variable-height'&&r.status==='ready');
  await page.locator('#recipe').selectOption(example.id);await expect(canvas).toHaveAttribute('data-recipe',example.id);
  await expect(page.locator('#credits')).toContainText(example.pluginId);

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

test('bundled bells keep gesture-gated localized previews with BGM deferred and a preserved world',async({page})=>{
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
  const musicRequests:string[]=[];
  page.on('request',request=>{if(/\.mid$|webaudio-tinysynth/.test(request.url()))musicRequests.push(request.url());});
  await page.goto('/');await ready(page);const before=await bootstrap(page);
  await page.locator('canvas').evaluate(el=>{el.dataset.identity='audio-preserved';});
  await openPanel(page,'settings');
  await expect(page.getByLabel('Music',{exact:true})).toHaveCount(0);
  await expect(page.getByRole('button',{name:'Play music',exact:true})).toHaveCount(0);
  expect(await page.evaluate(()=>(window as any).__audioContexts.length)).toBe(0);
  const bells=page.getByLabel('Departure bells',{exact:true});await expect(bells.locator('option')).toHaveCount(6);
  const systemBellIds=['{9B087AEA-6E9C-48cd-A1F3-1B774500752E}','{87B1BA1B-C18F-4E39-AD50-245255F34388}'];
  for(const id of await bells.locator('option').evaluateAll(options=>options.map(o=>(o as HTMLOptionElement).value))) {
    const contexts=await page.evaluate(()=>(window as any).__audioContexts.length);
    await bells.selectOption(id);
    await expect(page.getByTestId('audio-credit')).toHaveText(`Audio credits: ${systemBellIds.includes(id)?'Kohsuke Kawaguchi':'915'}`);
    await page.getByRole('button',{name:'Preview bell',exact:true}).click();
    await expect.poll(()=>page.evaluate(()=>(window as any).__audioContexts.length)).toBe(contexts+1);
    await expect.poll(()=>page.evaluate(()=>(window as any).__audioContexts.at(-1)?.state)).toBe('closed');
  }
  expect(await page.evaluate(()=>(window as any).__audioPeak)).toBeGreaterThan(.0001);

  await page.getByLabel('Language',{exact:true}).selectOption('ja');
  await expect(page.locator('html')).toHaveAttribute('lang','ja');await expect(page.locator('html')).toHaveAttribute('dir','ltr');
  await expect(page.getByLabel('発車ベル',{exact:true}).locator('option')).toHaveCount(6);
  const japaneseBells=page.getByLabel('発車ベル',{exact:true});
  for(const id of await japaneseBells.locator('option').evaluateAll(options=>options.map(o=>(o as HTMLOptionElement).value))) {
    await japaneseBells.selectOption(id);
    await expect(page.getByTestId('audio-credit')).toHaveText(`音源のクレジット: ${systemBellIds.includes(id)?'川口耕介':'915さん'}`);
  }
  const selectedBell=await japaneseBells.inputValue();
  await page.getByLabel('言語',{exact:true}).selectOption('en');
  await expect(page.getByTestId('audio-credit')).toHaveText('Audio credits: 915');
  await expect(bells).toHaveValue(selectedBell);
  await page.getByLabel('Language',{exact:true}).selectOption('ja');
  await expect(page.getByTestId('audio-credit')).toHaveText('音源のクレジット: 915さん');
  await expect(japaneseBells).toHaveValue(selectedBell);
  await expect(page.getByRole('button',{name:'音楽を再生',exact:true})).toHaveCount(0);
  await page.setViewportSize({width:360,height:800});
  await page.getByRole('button',{name:'ベルを試聴',exact:true}).click();
  await expect.poll(()=>page.evaluate(()=>(window as any).__audioContexts.at(-1)?.state)).toBe('closed');

  await closePanel(page);
  await expect(page.locator('canvas')).toHaveAttribute('data-identity','audio-preserved');
  await openPanel(page,'settings','ja');
  await expect(page.getByLabel('発車ベル',{exact:true}).locator('option')).toHaveCount(6);
  expect(musicRequests).toEqual([]);
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  expect(await bootstrap(page)).toEqual(before);expect(errors).toEqual([]);
});

test('bell hash failures remain localized and retryable; closing Settings fences late previews',async({page})=>{
  await page.addInitScript(()=>{
    (window as any).__bellStarts=0;
    const original=AudioBufferSourceNode.prototype.start;
    AudioBufferSourceNode.prototype.start=function(...args:Parameters<typeof original>){
      (window as any).__bellStarts++;return original.apply(this,args);
    };
  });
  await page.goto('/');await ready(page);await openPanel(page,'settings');
  const preview=page.getByRole('button',{name:'Preview bell',exact:true});
  await expect(preview).toBeEnabled();
  await page.route('**/*.wav',route=>route.fulfill({body:'altered WAV'}));
  await preview.click();
  await expect(page.getByRole('status').filter({hasText:'Sound could not play.'})).toBeVisible();
  expect(await page.evaluate(()=>(window as any).__bellStarts)).toBe(0);
  await page.unroute('**/*.wav');
  await preview.click();
  await expect.poll(()=>page.evaluate(()=>(window as any).__bellStarts)).toBe(1);
  let release=()=>{},waiting=false;const held=new Promise<void>(r=>{release=r;});
  await page.route('**/*.wav',async route=>{
    const response=await route.fetch();waiting=true;await held;await route.fulfill({response});
  });
  try {
    await preview.click();await expect.poll(()=>waiting).toBe(true);
    await closePanel(page);release();
    await page.unrouteAll({behavior:'wait'});
    await openPanel(page,'settings');
    expect(await page.evaluate(()=>(window as any).__bellStarts)).toBe(1);
    await preview.click();
    await expect.poll(()=>page.evaluate(()=>(window as any).__bellStarts)).toBe(2);
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

test('mixed Save10 loads into the playable railway shell, steps and saves exactly while malformed mixed peers block reveal',async({page})=>{
  const data=readFileSync('../prototype/protocol/saves/mixed-v11.json');
  const before=await bootstrap(page);
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+before.worldEpoch,{data})).ok()).toBe(true);
  await page.goto('/');await ready(page);
  await expect(page.getByTestId('reference-world')).toHaveText('Railway world');
  await expect(page.getByRole('button',{name:'Remove',exact:true})).toBeEnabled();
  const initial=await bootstrap(page);expect(initial.mixed.voxels).toHaveLength(4);expect(initial.cash).toBe('7000000000');
  await page.getByRole('button',{name:'Step',exact:false}).first().click();
  await expect.poll(async()=>(await bootstrap(page)).tick).toBe('130082');
  const saved=JSON.parse((await(await page.request.get('/api/saves/download')).body()).toString());
  expect(saved.saveVersion).toBe(11);expect(saved.mixed).toEqual(JSON.parse(data.toString()).mixed);
  await page.routeWebSocket('**/api/session?*',route=>{
    const server=route.connectToServer();server.onMessage(message=>{const body=JSON.parse(String(message));if(body.type==='join')body.snapshot.mixed.accounts.transactions[0].cashAfter='1';route.send(JSON.stringify(body));});
    route.onMessage(message=>server.send(message));
  });
  await page.reload();await expect(page.getByRole('heading',{name:'Update required',exact:true})).toBeVisible();
  await expect(page.locator('canvas')).toHaveAttribute('data-scene','demo');
});
