import {readFileSync} from 'node:fs';
import {test,expect,type Page} from './fixtures.ts';
import {browserFixture} from './generated-fixtures.ts';
import {canonicalState as state,ready,closePanel,exposeMapPoint} from './browser-helpers.ts';
const texts=['en','ja'].map(l=>JSON.parse(readFileSync(`src/locales/${l}/app.json`,'utf8')));
const bytes=async(p:Page)=>(await p.request.get('/api/saves/download')).body();
const compatibility='protocolVersion=11&contentVersion=terrain-content-1&contentVersion=mixed-railway-content-1';
async function point(p:Page,x:number,y:number){return p.locator('canvas.world-canvas').evaluate((el,w)=>{const r=el.getBoundingClientRect(),[x,y]=el.dataset.camera!.split(',').map(Number),z=Number(el.dataset.zoom);return {x:r.x+r.width/2+(w.x-x)*z,y:r.y+r.height/2+(w.y-y)*z};},{x:16*(x-15+y)+16,y:8*(y-(x-15))-32+8});}
async function quote(p:Page,t:any,x=14,y=12,ex=15,ey=13){
 for(const [label,value] of [[t.landAnchorX,x],[t.landAnchorY,y],[t.landEndX,ex],[t.landEndY,ey]] as const)await p.getByLabel(label,{exact:true}).fill(String(value));
 await p.locator('.land-task form').getByRole('button',{name:t.landCheck,exact:true}).click();
}
test.use({hasTouch:true});
for(const ja of [false,true])test(`P7-15 land ${ja?'JA phone touch':'EN keyboard'} authoritative partial rectangle cancellation current-value sale peers retry and exact restart`,async({page,browser,baseURL,worldServer})=>{
 test.setTimeout(120000);const t=texts[ja?1:0],locale=ja?'ja':'en';if(ja)await page.setViewportSize({width:430,height:860});
 await page.addInitScript(ja=>localStorage.setItem('freetrain-online.locale',ja?'ja':'en'),ja);
 const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 const context=await browser.newContext({baseURL,viewport:{width:430,height:860},isMobile:true,hasTouch:true}),peer=await context.newPage();
 const receipts:any[]=[];let socket:any,observedQuery:any;
 await page.routeWebSocket('**/api/session?*',route=>{socket=route.connectToServer();socket.onMessage(m=>{receipts.push(JSON.parse(String(m)));route.send(m);});route.onMessage(m=>socket.send(m));});
 async function command(payload:any,id:string){const s=await state(page),start=receipts.length;socket.send(JSON.stringify({type:'command',requestId:id,expectedWorldEpoch:s.worldEpoch,payload}));await expect.poll(()=>receipts.slice(start).find(r=>r.requestId===id)).toBeTruthy();return receipts.slice(start).find(r=>r.requestId===id);}
 try{
  await page.goto('/');await ready(page,locale);const initial=await state(page);
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+initial.worldEpoch,{data:readFileSync(browserFixture('land'))})).ok()).toBe(true);await ready(page,locale);
  expect((await peer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);await peer.goto('/');await ready(peer);
  await expect(peer.getByRole('button',{name:texts[0].landTask,exact:true})).toBeDisabled();
  await page.getByRole('button',{name:t.landTask,exact:true}).click();
  await page.route('**/api/land/preview?*',async r=>{observedQuery=r.request().postDataJSON();await r.continue();});
  const original=await bytes(page);await quote(page,t);const confirm=page.getByRole('button',{name:t.landConfirm,exact:true});await expect(confirm).toBeEnabled();
  await expect(page.getByTestId('land-counts')).toContainText(`${t.landSelected}: 2`);expect(await bytes(page)).toEqual(original);
  await page.getByRole('button',{name:t.cancelPreview,exact:true}).click();await expect(confirm).toBeDisabled();expect(await bytes(page)).toEqual(original);
  // Actual two-corner map selection fixes the anchor level and uses the same quote.
  for(const [x,y] of [[14,12],[15,13]]){const p=await exposeMapPoint(page,await point(page,x,y));if(ja)await page.touchscreen.tap(p.x,p.y);else await page.mouse.click(p.x,p.y);}
  await expect(confirm).toBeEnabled();await expect(page.getByTestId('land-amount')).toBeInViewport();
  await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/land-${locale}-preview.png`});
  const reviewed=await(await page.request.post('/api/land/preview?'+compatibility,{data:observedQuery})).json();expect(reviewed.cells.filter((c:any)=>c.status==='buyable')).toHaveLength(2);
  const before=await state(page);await page.locator('canvas').focus();await page.keyboard.press('Enter');
  await expect.poll(async()=>(await state(peer)).mixed.city.properties.length).toBe(3);
  const after=await state(page);expect(BigInt(before.mixed.accounts.transactions.at(-1)?.cashAfter??before.mixed.accounts.initialCash)-BigInt(after.mixed.accounts.transactions.at(-1).cashAfter)).toBe(BigInt(reviewed.amount));
  expect(after.mixed.city.ground).toHaveLength(1);expect(after.mixed.city.producers).toHaveLength(1);
  const purchased=await bytes(page),checkpoint=await state(page);
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+checkpoint.worldEpoch,{data:purchased})).ok()).toBe(true);await ready(page,locale);expect(await bytes(page)).toEqual(purchased);
  // Inspect the actual property sprite, including the observer's read-only view.
  await page.getByRole('button',{name:t.referenceInspect,exact:true}).click();const at=await exposeMapPoint(page,await point(page,14,12));if(ja)await page.touchscreen.tap(at.x,at.y);else await page.mouse.click(at.x,at.y);
  await expect(page.getByTestId('land-current-value')).toHaveText('¥30');await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/land-${locale}-inspector.png`});await closePanel(page);
  await page.getByRole('button',{name:t.landTask,exact:true}).click();await page.getByLabel(t.landAction,{exact:true}).selectOption('sell');await quote(page,t);
  await expect(confirm).toBeEnabled();await expect(page.getByTestId('land-counts')).toContainText(`${t.landSelected}: 3`);
  const q=observedQuery,save=await bytes(page);expect((await command({kind:'tradeLand',query:q,expectedAmount:'91'},'land-wrong-price')).code).toBe('staleRevision');expect(await bytes(page)).toEqual(save);
  const payload={kind:'tradeLand',query:q,expectedAmount:'90'},receipt=await command(payload,'land-sale');expect(receipt.type).toBe('accepted');const sold=await bytes(page);
  expect(await command(payload,'land-sale')).toEqual(receipt);expect(await bytes(page)).toEqual(sold);await expect.poll(async()=>(await state(peer)).mixed.city.properties.length).toBe(0);
  const s=await state(page);expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+s.worldEpoch,{data:sold})).ok()).toBe(true);expect(await bytes(page)).toEqual(sold);
  await worldServer.restart!();
  for(const [p,secret] of [[page,''],[peer,'observer-test']] as const)expect((await p.request.post('/api/access',{data:{secret}})).ok()).toBe(true);
  await page.reload();await peer.reload();await ready(page,locale);await ready(peer);expect(await bytes(page)).toEqual(sold);expect((await state(peer)).mixed.city.properties).toEqual([]);expect(errors).toEqual([]);
 }finally{await context.close();}
});

test('P7-15 evolving heat refreshes observer value and invalidates old sale quote without mutation',async({page,browser,baseURL})=>{
 test.setTimeout(90000);const t=texts[0],context=await browser.newContext({baseURL}),peer=await context.newPage();
 try{
  await page.goto('/');await ready(page);const initial=await state(page);
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+initial.worldEpoch,{data:readFileSync(browserFixture('land')+'.changing')})).ok()).toBe(true);await ready(page);
  expect((await peer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);await peer.goto('/');await ready(peer);
  const at=await exposeMapPoint(peer,await point(peer,14,12));await peer.mouse.click(at.x,at.y);await expect(peer.getByTestId('land-current-value')).toHaveText('¥30');
  await page.getByRole('button',{name:t.landTask,exact:true}).click();await page.getByLabel(t.landAction,{exact:true}).selectOption('sell');await quote(page,t,14,12,14,12);
  await expect(page.getByTestId('land-amount')).toContainText('¥30');const confirm=page.getByRole('button',{name:t.landConfirm,exact:true});await expect(confirm).toBeEnabled();
  const before=BigInt((await state(page)).tick);await page.getByRole('button',{name:t.step,exact:false}).click();await expect.poll(async()=>BigInt((await state(page)).tick)).toBe(before+1n);
  await expect(peer.getByTestId('land-current-value')).toHaveText('¥20');const unchanged=await bytes(page);
  await confirm.click();await expect(page.getByTestId('land-status')).toHaveText(t.railStale);expect(await bytes(page)).toEqual(unchanged);
  await quote(page,t,14,12,14,12);await expect(page.getByTestId('land-amount')).toContainText('¥20');await confirm.click();await expect.poll(async()=>(await state(peer)).mixed.city.properties.length).toBe(0);
  const final=await state(page);expect(final.mixed.accounts.transactions.at(-1).revenue).toBe('20');
 }finally{await context.close();}
});
