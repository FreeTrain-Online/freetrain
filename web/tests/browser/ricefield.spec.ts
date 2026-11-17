import {requireMixedState} from './session.ts';
import {assertSaveBytes,assertCanonicalState} from './assertions.ts';
import {readFileSync} from 'node:fs';
import {test,expect,type Page} from './fixtures.ts';
import {browserFixture} from './generated-fixtures.ts';
import {ready,closePanel,exposeMapPoint} from './browser-helpers.ts';
const texts=['en','ja'].map(l=>JSON.parse(readFileSync(`src/locales/${l}/app.json`,'utf8')));
const bytes=async(p:Page)=>(await p.request.get('/api/saves/download')).body();
async function point(p:Page,x:number,y:number){return p.locator('canvas.world-canvas').evaluate((el,w)=>{const r=el.getBoundingClientRect(),[x,y]=el.dataset.camera!.split(',').map(Number),z=Number(el.dataset.zoom);return {x:r.x+r.width/2+(w.x-x)*z,y:r.y+r.height/2+(w.y-y)*z};},{x:16*(x-15+y)+16,y:8*(y-(x-15))-32+8});}
async function quote(p:Page,t:any){
 for(const [label,value] of [[t.landAnchorX,14],[t.landAnchorY,12],[t.landEndX,15],[t.landEndY,13]] as const)await p.getByLabel(label,{exact:true}).fill(String(value));
 await p.locator('.land-task form').getByRole('button',{name:t.groundCheck,exact:true}).click();
}
test.use({hasTouch:true});
for(const ja of [false,true])test(`P7-18 ricefield ${ja?'JA phone touch':'EN keyboard'} authored catalog real providers peer removal and exact restart`,async({page,browser,baseURL,worldServer,sessions})=>{
 test.setTimeout(120000);const t=texts[ja?1:0],locale=ja?'ja':'en';if(ja)await page.setViewportSize({width:430,height:860});
 await page.addInitScript(ja=>localStorage.setItem('freetrain-online.locale',ja?'ja':'en'),ja);
 const context=await browser.newContext({baseURL}),peer=await context.newPage();const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 try{
  const owner=await sessions.observe(page,'owner');
  const exact=async(data:Buffer,phase:string)=>assertSaveBytes(await bytes(page),data,{phase,peer:'owner'});
  await page.goto('/');await ready(page,locale);await owner.ready();
  await sessions.replaceSave(owner,readFileSync(browserFixture('ricefield')),'seed ricefield');await ready(page,locale);
  const viewer=await sessions.observe(peer,'peer');await peer.goto('/');await ready(peer);await viewer.ready();await sessions.checkpoint(owner,'initial peers');await page.getByRole('button',{name:t.landTask,exact:true}).click();
  await page.getByLabel(t.landAction,{exact:true}).selectOption('cover');const catalog=page.getByLabel(t.groundType,{exact:true});expect(await catalog.locator('option').count()).toBe(45);
  // Both source styles share the actual 3x3 catalog mosaic and rectangle workflow.
  await catalog.selectOption(ja?'{5787D011-657E-4CFA-8EDE-E0DDDE21C3F2}':'{8EBBA0C5-30E1-41DE-9968-019EB816BC34}');await expect(page.locator('.land-task .catalog-preview img')).toBeVisible();
  const original=await bytes(page);await quote(page,t);const confirm=page.getByRole('button',{name:t.groundConfirm,exact:true});await expect(confirm).toBeEnabled();
  await expect(page.getByTestId('land-counts')).toContainText(`${t.landSelected}: 4`);await expect(page.getByTestId('land-amount')).toContainText(ja?'12,000,000':'12,000,000');await exact(original,'preview or cancellation preserves save');
  await page.getByRole('button',{name:t.cancelPreview,exact:true}).click();await expect(confirm).toBeDisabled();await exact(original,'preview or cancellation preserves save');
  // Ground/property alpha picks participate in the same two-corner workflow.
  for(const [x,y] of [[14,12],[15,13]]){const at=await exposeMapPoint(page,await point(page,x,y));if(ja)await page.touchscreen.tap(at.x,at.y);else await page.mouse.click(at.x,at.y);}
  await expect(confirm).toBeEnabled();await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/ricefield-${locale}-review.png`});
  await page.locator('canvas').focus();expect((await owner.actionReceipt(()=>page.keyboard.press('Enter'))).type).toBe('accepted');
  let after=requireMixedState(await sessions.checkpoint(owner,'ricefield placed'),'city','stationGraph');expect(after.mixed.city.ground).toHaveLength(4);
  await assertCanonicalState(after.mixed.city.ground.map(g=>g.variant),[0,2,6,8],{phase:'rectangle variants',peer:'owner'});await assertCanonicalState(after.mixed.accounts.transactions.slice(1).map(r=>r.expenditure),['1500000','1500000','1500000','1500000','6000000'],{phase:'ordered source charges',peer:'owner'});expect(after.mixed.city.properties).toHaveLength(0);expect(after.mixed.city.producers).toHaveLength(4);expect(after.mixed.stationGraph.listeners).toHaveLength(4);
  expect(after.mixed.stationGraph.listenerMemberships[0].providers).toHaveLength(4);expect(after.mixed.accounts.transactions.at(-1).expenditure).toBe('6000000');
  const saved=await bytes(page);await sessions.replaceSave(owner,saved,'ricefield reopen');await ready(page,locale);await exact(saved,'ricefield reopened bytes');
  await sessions.restartCheckpoint(worldServer,[{session:owner},{session:viewer}],'ricefield process restart');await ready(page,locale);await ready(peer);await exact(saved,'restart bytes');
  await page.getByRole('button',{name:t.referenceInspect,exact:true}).click();const at=await exposeMapPoint(page,await point(page,15,13));if(ja)await page.touchscreen.tap(at.x,at.y);else await page.mouse.click(at.x,at.y);
  await expect(page.getByTestId('land-current-value')).toBeVisible();await page.screenshot({path:`${process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE}/ricefield-${locale}-placed.png`});await closePanel(page);
  // A peer's actual edit updates the first browser and detaches both listener directions.
  const en=texts[0];await peer.getByRole('button',{name:en.landTask,exact:true}).click();await peer.getByLabel(en.landAction,{exact:true}).selectOption('removeCover');await quote(peer,en);
  await expect(peer.getByRole('button',{name:en.groundConfirm,exact:true})).toBeEnabled();await expect(peer.getByTestId('land-amount')).toContainText('¥0');expect((await viewer.actionReceipt(()=>peer.getByRole('button',{name:en.groundConfirm,exact:true}).click())).type).toBe('accepted');
  after=requireMixedState(await sessions.checkpoint(owner,'peer removal'),'city','stationGraph');expect(after.mixed.city.ground).toHaveLength(0);expect(after.mixed.city.producers).toHaveLength(0);expect(after.mixed.stationGraph.listeners).toHaveLength(0);expect(after.mixed.stationGraph.listenerMemberships[0].providers).toHaveLength(0);
  expect(after.mixed.accounts.transactions.at(-1).expenditure).toBe('6000000');const removed=await bytes(page);
  await sessions.replaceSave(owner,removed,'removed reopen');await ready(page,locale);await exact(removed,'removed bytes');
  expect(errors).toEqual([]);
 }finally{await context.close();}
});
