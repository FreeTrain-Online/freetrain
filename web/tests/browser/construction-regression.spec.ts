import { readFileSync } from 'node:fs';
import { expect,test,type Page } from './fixtures.ts';
import { closePanel,openPanel,ready,canonicalState as state } from './browser-helpers.ts';
const en=JSON.parse(readFileSync('src/locales/en/app.json','utf8'));
const ja=JSON.parse(readFileSync('src/locales/ja/app.json','utf8'));
test.use({actionTimeout:10_000});
const tasks=['trackTask','slopeTask','bridgeTask','girderTask','tunnelTask','garageTask','signalTask','poleTask','stationaryTask','speedTask','removeTool'] as const;
const save=async(p:Page)=>(await p.request.get('/api/saves/download')).body();
async function restore(p:Page,bytes:Buffer){const s=await state(p);expect((await p.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:bytes})).ok()).toBe(true);}
async function empty(p:Page){
 const s=await state(p),r=await p.request.post('/api/worlds/empty/preview',{data:{name:'Construction regression',dimensions:{h:30,v:42,z:6},waterLevel:1,epoch:s.worldEpoch,revision:s.revision}});expect(r.ok()).toBe(true);
 const c=await r.json();expect((await p.request.post('/api/worlds/empty/commit',{data:{id:c.id,requestHash:c.requestHash,candidateHash:c.candidateHash,epoch:s.worldEpoch,revision:s.revision}})).ok()).toBe(true);
 await expect(p.locator('canvas')).toHaveAttribute('data-ruleset','terrain-state-1');await ready(p);
}
async function target(p:Page,y=27,z=1,text=en){
 for(const [key,value] of [['railFromX',30],['railFromY',y],['railToX',34],['railToY',y],['buildLevel',z]] as const)
  await p.getByLabel(text[key],{exact:true}).fill(String(value));
 await p.getByRole('button',{name:text.railCheck,exact:true}).click();
}
const button=(p:Page,name:string)=>p.getByRole('button',{name,exact:true});

for(const locale of ['en','ja'] as const)test(`all construction tasks: roles, touch, enlarged text, locale cancellation and scene identity ${locale}`,async({browser})=>{
 test.setTimeout(120_000);
 const ctx=await browser.newContext({viewport:{width:430,height:850},hasTouch:true,deviceScaleFactor:2}),p=await ctx.newPage();
 await p.addInitScript(()=>{const decode=HTMLImageElement.prototype.decode;(window as any).decodes=0;HTMLImageElement.prototype.decode=function(){(window as any).decodes++;return decode.call(this);};});
 await p.goto('/');await ready(p);const original=await save(p),text=locale==='ja'?ja:en;
 try{
  await empty(p);await button(p,en.trackTask).click();await target(p,25);await button(p,en.confirmTrack).click();await expect(p.getByTestId('rail-route')).toHaveCount(0);
  if(locale==='ja'){await openPanel(p,'settings');await p.getByLabel('Language',{exact:true}).selectOption('ja');await closePanel(p);}
  await button(p,text.referenceInspect).click();
  await openPanel(p,'catalog',locale);
  const warm=p.locator('#building-catalog');
  for(const category of ['signal','stationary']){await warm.getByLabel(text.catalogCategory,{exact:true}).selectOption(category);await expect.poll(()=>warm.locator('img').evaluateAll(images=>images.length>0&&images.every(i=>(i as HTMLImageElement).complete))).toBe(true);}
  await warm.getByRole('button',{name:text.closeCatalog,exact:true}).click();
  await p.addStyleTag({content:'button,select,input,p,h2,label {font-size:200% !important}'});
  const canvas=p.locator('canvas');await expect(canvas).toHaveAttribute('data-refinement','ready');
  await canvas.evaluate(el=>(el as any).regressionIdentity=true);
  const camera=await canvas.getAttribute('data-camera'),bounds=await canvas.boundingBox(),before=await save(p),decodes=await p.evaluate(()=>(window as any).decodes);
  for(const task of tasks){
   const tool=button(p,text[task]);await tool.scrollIntoViewIfNeeded();await tool.tap();
   await expect(tool).toHaveAttribute('aria-pressed','true');
   // Catalog is an independent sheet; closing it keeps the task and selection.
   const catalog=p.locator('#building-catalog');if(await catalog.count())await catalog.getByRole('button',{name:text.closeCatalog,exact:true}).click();
   for(const field of await p.locator('.tool-card input:not([type=checkbox]),.tool-card select').all()){await field.scrollIntoViewIfNeeded();await expect(field).toBeInViewport();if(await field.evaluate(el=>el.tagName==='INPUT')){await field.tap();await expect(field).toBeFocused();}else {await field.focus();await expect(field).toBeFocused();}}
   const primary=p.locator('.tool-card button.primary');await primary.scrollIntoViewIfNeeded();await expect(primary).toBeInViewport();await expect(primary).toBeDisabled();
   expect(await p.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
   await canvas.focus();await p.keyboard.press('Escape');await button(p,text.referenceInspect).click();
   await expect(canvas).toHaveAttribute('data-camera',camera!);
   expect(await canvas.evaluate(el=>(el as any).regressionIdentity)).toBe(true);expect(await canvas.boundingBox()).toEqual(bounds);expect(await canvas.getAttribute('data-camera')).toBe(camera);
  }
  expect(await p.evaluate(()=>(window as any).decodes)).toBe(decodes);expect(await save(p)).toEqual(before);
  // Enter Settings with a legal draft: targeting is cancelled before changing locale.
  await button(p,text.trackTask).click();await target(p,27,1,text);await expect(button(p,text.confirmTrack)).toBeEnabled();
  await openPanel(p,'settings',locale);await expect(p.getByTestId('rail-route')).toHaveCount(0);
  await p.getByLabel(text.language,{exact:true}).selectOption(locale==='ja'?'en':'ja');await closePanel(p);
  const next=locale==='ja'?en:ja;await expect(button(p,next.confirmTrack)).toBeDisabled();expect(await save(p)).toEqual(before);
  // Role replacement starts a fresh connection; every mutation task remains gated.
  for(const [secret,role] of [['operator-test',next.operator],['observer-test',next.observer]]){
   expect((await p.request.post('/api/access',{data:{secret}})).ok()).toBe(true);await p.reload();await ready(p,locale==='ja'?'en':'ja');await expect(p.getByTestId('participant-role')).toHaveText(role);
   for(const task of tasks)await expect(button(p,next[task]))[secret==='observer-test'?'toBeDisabled':'toBeEnabled']();
  }
 }finally{await p.request.post('/api/access',{data:{secret:''}});await restore(p,original);await ctx.close();}
});

test('concurrent support edit invalidates a route; delayed receipt cannot clear a newer task or replay',async({page:p,browser})=>{
 let held:(string|Buffer)[]=[];let delay=false,commands=0;let release=()=>{};
 await p.routeWebSocket('**/api/session?*',route=>{
  const server=route.connectToServer();release=()=>{delay=false;for(const m of held.splice(0))route.send(m);};
  route.onMessage(m=>{if(JSON.parse(String(m)).payload?.kind==='editRailway')commands++;server.send(m);});
  server.onMessage(m=>{if(delay&&JSON.parse(String(m)).type==='accepted')held.push(m);else route.send(m);});
 });
 await p.goto('/');await ready(p);const original=await save(p),ctx=await browser.newContext(),peer=await ctx.newPage();
 try{
  await empty(p);await peer.goto('/');await ready(peer);
  await button(p,en.bridgeTask).click();await target(p,27,3);await expect(button(p,en.confirmBridge)).toBeEnabled();
  await button(peer,en.bridgeTask).click();await target(peer,27,3);await button(peer,en.confirmBridge).click();
  await expect(p.getByTestId('rail-status')).toContainText(en.railStale);await expect(button(p,en.confirmBridge)).toBeDisabled();
  await button(p,en.trackTask).click();await target(p,30);await expect(button(p,en.confirmTrack)).toBeEnabled();delay=true;await button(p,en.confirmTrack).click();
  await expect.poll(()=>held.length).toBe(1);
  // Cancel while the receipt is delayed, then enter a different task.
  await p.locator('canvas').focus();await p.keyboard.press('Escape');
  release();await expect(p.locator('.command-feedback')).toContainText(en.accepted);
  await button(p,en.bridgeTask).click();await target(p,32,3);await expect(button(p,en.confirmBridge)).toBeEnabled();
  expect(commands).toBe(1);expect((await state(p)).mixed.accounts.transactions).toHaveLength(2);
  await expect(p.getByTestId('rail-route')).toContainText('30, 32, 3');
 }finally{release();await restore(p,original);await ctx.close();}
});

test('late preview after epoch replacement cannot restore a cancelled route or clear the new one',async({page:p})=>{
 await p.goto('/');await ready(p);const original=await save(p);let release=()=>{};const gate=new Promise<void>(r=>release=r);let held=false;
 try{
  await empty(p);const bytes=await save(p);await button(p,en.bridgeTask).click();
  await p.route('**/api/railway/preview?*',async route=>{const response=await route.fetch();if(!held){held=true;await gate;}await route.fulfill({response});});
  await target(p,27,3);await expect.poll(()=>held).toBe(true);
  const epoch=(await state(p)).worldEpoch;await restore(p,bytes);await expect(p.locator('canvas')).not.toHaveAttribute('data-world-epoch',epoch);await ready(p);
  await button(p,en.trackTask).click();await target(p,30);await expect(button(p,en.confirmTrack)).toBeEnabled();release();
  await expect(p.getByTestId('rail-route')).toContainText('30, 30, 1');await button(p,en.confirmTrack).click();await expect(p.getByTestId('rail-route')).toHaveCount(0);
  expect((await state(p)).mixed.accounts.transactions).toHaveLength(1);
 }finally{release();await restore(p,original);}
});

test('running shared time survives every construction task, held-input reset and modal focus isolation',async({page:p})=>{
 test.setTimeout(90_000);
 await p.goto('/');await ready(p);const original=await save(p);
 try{
  await empty(p);await button(p,en.trackTask).click();await target(p,25);await button(p,en.confirmTrack).click();await expect(p.getByTestId('rail-route')).toHaveCount(0);
  await p.getByRole('button',{name:'Run',exact:false}).click();
  const canvas=p.locator('canvas');const tick=(await state(p)).tick;
  for(const task of tasks){await button(p,en[task]).click();await expect(p.getByTestId('clock-state')).toHaveText('Running');}
  await expect.poll(async()=>(await state(p)).tick).not.toBe(tick);
  // Lost pointer capture, blur and hidden-tab transitions must end a held pan.
  for(const event of ['blur','visibilitychange','pointercancel']){
   const b=(await canvas.boundingBox())!;await p.mouse.move(b.x+b.width*.6,b.y+b.height*.3);await p.mouse.down({button:'middle'});await p.mouse.move(b.x+b.width*.6+20,b.y+b.height*.3+10);
   await p.evaluate(event=>{(event==='visibilitychange'?document:event==='pointercancel'?document.querySelector('canvas')!:window).dispatchEvent(new Event(event));},event);
   const camera=await canvas.getAttribute('data-camera');await p.mouse.move(b.x+b.width*.6+70,b.y+b.height*.3+30);await p.mouse.up({button:'middle'});await expect(canvas).toHaveAttribute('data-camera',camera!);
  }
  await button(p,en.trackTask).click();await target(p,30);await expect(button(p,en.confirmTrack)).toBeEnabled();
  await p.locator('.command-bar').getByRole('button',{name:en.game+' ▾',exact:true}).click();await p.locator('#game-menu').getByRole('button',{name:en.credits,exact:true}).click();
  const dialog=p.getByRole('dialog');await expect(dialog).toBeVisible();const camera=await canvas.getAttribute('data-camera');
  await canvas.evaluate(el=>(el as HTMLElement).focus());expect(await dialog.evaluate(el=>el.contains(document.activeElement))).toBe(true);
  await p.keyboard.press('b');await p.keyboard.press('Shift+Enter');await expect(dialog).toBeVisible();await p.mouse.wheel(0,-300);await expect(canvas).toHaveAttribute('data-camera',camera!);
  await p.keyboard.press('Escape');await expect(dialog).toHaveCount(0);await expect(p.getByTestId('clock-state')).toHaveText('Running');await expect(button(p,en.confirmTrack)).toBeDisabled();
  expect((await state(p)).mixed.accounts.transactions).toHaveLength(1);
 }finally{await restore(p,original);}
});

test('disconnect during a detached preview clears targeting and never replays after reconnect',async({page:p})=>{
 let disconnect=()=>{},connections=0;
 await p.routeWebSocket('**/api/session?*',route=>{connections++;const server=route.connectToServer();disconnect=()=>{server.close();route.close();};});
 await p.goto('/');await ready(p);const original=await save(p);let release=()=>{};const gate=new Promise<void>(r=>release=r);let held=false;
 try{
  await empty(p);const before=await save(p);await button(p,en.trackTask).click();
  await p.route('**/api/railway/preview?*',async route=>{const response=await route.fetch();held=true;await gate;await route.fulfill({response});});
  await target(p);await expect.poll(()=>held).toBe(true);const previous=connections;disconnect();await expect.poll(()=>connections).toBeGreaterThan(previous);await ready(p);release();
  if(await button(p,en.trackTask).getAttribute('aria-pressed')!=='true')await button(p,en.trackTask).click();
  await expect(button(p,en.confirmTrack)).toBeDisabled();await expect(p.getByTestId('rail-route')).toHaveCount(0);expect(await save(p)).toEqual(before);
  await p.unroute('**/api/railway/preview?*');await target(p,30);await expect(button(p,en.confirmTrack)).toBeEnabled();await button(p,en.confirmTrack).click();await expect(p.getByTestId('rail-route')).toHaveCount(0);
  expect((await state(p)).mixed.accounts.transactions).toHaveLength(1);
 }finally{release();await restore(p,original);}
});

test('role replacement during preview cannot retain or submit the Owner draft',async({page:p})=>{
 await p.goto('/');await ready(p);const original=await save(p);let release=()=>{};const gate=new Promise<void>(r=>release=r);let held=false;
 try{
  await empty(p);const before=await save(p);await button(p,en.trackTask).click();
  await p.route('**/api/railway/preview?*',async route=>{const response=await route.fetch();held=true;await gate;await route.fulfill({response});});
  await target(p);await expect.poll(()=>held).toBe(true);
  expect((await p.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);await p.reload();await ready(p);release();
  await expect(p.getByTestId('participant-role')).toHaveText(en.observer);await expect(button(p,en.trackTask)).toBeDisabled();await expect(p.getByTestId('rail-route')).toHaveCount(0);
  await p.request.post('/api/access',{data:{secret:''}});expect(await save(p)).toEqual(before);
 }finally{release();await p.request.post('/api/access',{data:{secret:''}});await restore(p,original);}
});
