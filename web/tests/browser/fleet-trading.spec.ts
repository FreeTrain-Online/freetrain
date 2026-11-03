import {readFileSync,mkdirSync} from 'node:fs';
import {test,expect,type Page,type WebSocketRoute} from './fixtures.ts';
import {canonicalState,closePanel,openPanel,ready} from './browser-helpers.ts';

const state=canonicalState;
const evidence=process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!;
test('purchase, stale/cancelled quotes, duplicate confirmation, rename and resale publish once to two peers and reopen exactly',async({page,browser,baseURL})=>{
  const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
  let socket:WebSocketRoute|undefined,lastTrade:any;
  const receipts:any[]=[];
  await page.routeWebSocket('**/api/session?*',route=>{
    const server=route.connectToServer();socket=server;
    route.onMessage(message=>{const m=JSON.parse(String(message));if(m.payload?.kind==='tradeFleet')lastTrade=m;server.send(message);});
    server.onMessage(message=>{const m=JSON.parse(String(message));if(['accepted','rejected'].includes(m.type))receipts.push(m);route.send(message);});
  });
  await page.goto('/');await ready(page);const canvas=page.locator('canvas');await canvas.evaluate(el=>el.setAttribute('data-instance','trade-preserved'));
  const context=await browser.newContext({baseURL}),peer=await context.newPage();
  try{
    await peer.goto('/');await ready(peer);await openPanel(page,'fleet');await openPanel(peer,'fleet');
    await peer.getByRole('button',{name:'Owned stock',exact:false}).click();await expect(peer.getByText('No trains owned.',{exact:false})).toBeVisible();
    const before=await state(page);
    await page.getByLabel('Company',{exact:true}).selectOption('JR東日本');await expect(page.locator('.fleet-catalog li')).toHaveCount(2);
    await page.getByLabel('Type',{exact:true}).selectOption('485系');await expect(page.locator('.fleet-catalog li')).toHaveCount(1);
    await page.getByRole('button',{name:/JR East.*485 series.*Raicho/}).click();
    await page.getByLabel('Search catalog',{exact:true}).fill('no-such-stock');await expect(page.getByText('No rolling stock matches these filters.')).toBeVisible();
    await page.getByLabel('Search catalog',{exact:true}).fill('Raicho');await expect(page.locator('.fleet-catalog li')).toHaveCount(1);
    await page.getByLabel('Formation length (cars)',{exact:true}).selectOption('3');await page.getByLabel('Quantity (trains)',{exact:true}).fill('2');
    await expect(page.locator('.formation-preview img')).toHaveCount(3);
    await page.screenshot({path:`${evidence}/fleet-purchase-en.png`});
    await page.getByRole('button',{name:'Get purchase quote',exact:true}).click();let dialog=page.getByRole('dialog',{name:'Purchase quote',exact:true});
    await expect(dialog).toBeVisible();await expect(dialog.getByTestId('stock-total')).toHaveText('300,000,000');
    await expect(dialog).toContainText('400');await expect(dialog).toContainText('Capacities below are totals');
    expect(await state(page)).toEqual(before);await page.keyboard.press('Escape');await expect(page.getByRole('button',{name:'Get purchase quote',exact:true})).toBeFocused();
    await page.getByRole('button',{name:'Get purchase quote',exact:true}).click();await expect(dialog).toBeVisible();
    await peer.getByRole('button',{name:'Step',exact:false}).first().click();
    await expect(dialog.getByText('This quote is out of date.',{exact:false})).toBeVisible();await expect(dialog.getByRole('button',{name:'Confirm purchase',exact:true})).toBeDisabled();
    await dialog.getByRole('button',{name:'Cancel',exact:true}).click();
    await page.getByRole('button',{name:'Get purchase quote',exact:true}).click();await expect(dialog).toBeVisible();
    await dialog.getByRole('button',{name:'Confirm purchase',exact:true}).dblclick();
    await expect(dialog).toHaveCount(0);await expect(peer.locator('.catalog li')).toHaveCount(2);
    const bought=await state(page);expect(bought.mixed.fleet.trains).toHaveLength(2);
    expect(bought.cash).toBe('14700000000');expect(bought.mixed.accounts.transactions).toHaveLength(1);
    expect(bought.mixed.fleet.trains.map((t:any)=>t.cars.length)).toEqual([3,3]);
    // The same wire request reuses its original receipt; a new request with the old query rejects.
    const count=receipts.length;socket!.send(JSON.stringify(lastTrade));await expect.poll(()=>receipts.length).toBe(count+1);
    expect(receipts.at(-1).type).toBe('accepted');expect(await state(page)).toEqual(bought);
    socket!.send(JSON.stringify({...lastTrade,requestId:'stale-trade-new-id'}));await expect.poll(()=>receipts.at(-1)?.requestId).toBe('stale-trade-new-id');expect(receipts.at(-1).code).toBe('staleRevision');
    await page.getByRole('button',{name:'Owned stock',exact:false}).click();
    const id=bought.mixed.fleet.trains[0].id;await page.getByRole('button',{name:new RegExp(`TR${id}.*#${id}`)}).click();
    await page.getByLabel('Name',{exact:true}).fill('Northern Star');
    await closePanel(page);await expect(page.getByRole('dialog',{name:'Unsaved train name',exact:true})).toBeVisible();
    await page.getByRole('button',{name:'Keep editing',exact:true}).click();
    await page.getByLabel('Language',{exact:true}).selectOption('ja');await expect(page.getByLabel('名称',{exact:true})).toHaveValue('Northern Star');
    await page.getByLabel('名称',{exact:true}).press('Enter');await expect(peer.getByRole('button',{name:/Northern Star/})).toBeVisible();
    await page.getByRole('button',{name:'売却見積もり',exact:true}).click();dialog=page.getByRole('dialog',{name:'売却見積もり',exact:true});
    await expect(dialog.getByTestId('stock-total')).toHaveText('75,000,000');await dialog.getByRole('button',{name:'キャンセル',exact:true}).click();
    expect((await state(page)).mixed.fleet.trains).toHaveLength(2);
    await page.getByRole('button',{name:'売却見積もり',exact:true}).click();await dialog.getByRole('button',{name:'売却を確定',exact:true}).click();
    await expect(peer.locator('.catalog li')).toHaveCount(1);
    const sold=await state(page);expect(sold.cash).toBe('14775000000');expect(sold.mixed.accounts.transactions).toHaveLength(2);expect(sold.mixed.accounts.transactions.at(-1).revenue).toBe('75000000');
    const saved=await(await page.request.get('/api/saves/download')).json();expect(saved.mixed).toEqual(sold.mixed);
    expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+sold.worldEpoch,{data:Buffer.from(JSON.stringify(saved))})).ok()).toBe(true);
    await expect.poll(async()=>(await state(page)).worldEpoch).not.toBe(sold.worldEpoch);
    await peer.reload();await ready(peer);expect((await state(peer)).mixed).toEqual(saved.mixed);
    await expect(canvas).toHaveAttribute('data-instance','trade-preserved');expect(errors).toEqual([]);
    await openPanel(page,'fleet','ja');await page.getByRole('button',{name:'保有車両',exact:false}).click();await expect(page.locator('.catalog li')).toHaveCount(1);
    mkdirSync(evidence,{recursive:true});await page.screenshot({path:`${evidence}/fleet-owned-ja.png`});
  }finally{await context.close();}
});

test('Japanese narrow purchase and observer catalog browse preserve controls at enlarged text',async({page,browser,baseURL})=>{
  await page.setViewportSize({width:390,height:844});await page.goto('/');await ready(page);await openPanel(page,'fleet');
  await page.getByLabel('Language',{exact:true}).selectOption('ja');
  await page.locator('.primary-panel .sheet-toggle').click();
  await page.locator('html').evaluate(el=>el.style.fontSize='200%');
  await page.getByLabel('編成両数（両）',{exact:true}).selectOption('2');await page.getByLabel('購入編成数（編成）',{exact:true}).fill('1');
  await page.getByRole('button',{name:'購入見積もり',exact:true}).click();const dialog=page.getByRole('dialog',{name:'購入見積もり',exact:true});
  await expect(dialog.getByTestId('stock-total')).toHaveText('60,000,000');
  const b=await dialog.boundingBox();expect(b!.x).toBeGreaterThanOrEqual(0);expect(b!.x+b!.width).toBeLessThanOrEqual(390);
  await page.screenshot({path:`${evidence}/fleet-purchase-ja-narrow.png`});
  await dialog.getByRole('button',{name:'購入を確定',exact:true}).click();await expect(dialog).toHaveCount(0);
  const context=await browser.newContext({baseURL}),viewer=await context.newPage();
  try{
    expect((await viewer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);
    await viewer.goto('/');await ready(viewer);await openPanel(viewer,'fleet');
    await viewer.getByLabel('Search catalog',{exact:true}).fill('Komachi');await expect(viewer.locator('.fleet-catalog li')).toHaveCount(1);
    await viewer.getByRole('button',{name:/JR East.*E3 series.*Komachi/}).click();await expect(viewer.getByRole('button',{name:'Get purchase quote',exact:true})).toBeDisabled();
    await viewer.getByRole('button',{name:'Owned stock',exact:false}).click();await viewer.locator('.catalog-item-main').click();
    await expect(viewer.getByLabel('Name',{exact:true})).toBeDisabled();await expect(viewer.getByRole('button',{name:'Get sale quote',exact:true})).toBeDisabled();
    expect((await state(viewer)).mixed.fleet.trains).toHaveLength(1);
  }finally{await context.close();}
});

test('placed inventory requires withdrawal before sale and rename drafts detect a peer edit',async({page,browser,baseURL})=>{
  const before=await state(page),save=readFileSync('../prototype/protocol/saves/fleet-v9.json');
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+before.worldEpoch,{data:save})).ok()).toBe(true);
  await page.goto('/');await ready(page);await openPanel(page,'fleet');await page.getByRole('button',{name:'Owned stock',exact:false}).click();
  await page.locator('.catalog-item-main').first().click();await expect(page.getByText('Withdraw this train before selling.')).toBeVisible();
  await expect(page.getByRole('button',{name:'Get sale quote',exact:true})).toBeDisabled();
  const context=await browser.newContext({baseURL}),peer=await context.newPage();
  try{
    await peer.goto('/');await ready(peer);await openPanel(peer,'fleet');await peer.getByRole('button',{name:'Owned stock',exact:false}).click();await peer.locator('.catalog-item-main').first().click();
    await page.getByLabel('Name',{exact:true}).fill('My draft');await peer.getByLabel('Name',{exact:true}).fill('Peer name');await peer.getByRole('button',{name:'Rename train',exact:true}).click();
    await expect(page.getByText('This train changed while you were editing.',{exact:false})).toBeVisible();await expect(page.getByLabel('Name',{exact:true})).toHaveValue('My draft');
    await expect(page.getByRole('button',{name:'Rename train',exact:true})).toBeDisabled();await page.getByRole('button',{name:'Reset draft',exact:true}).click();await expect(page.getByLabel('Name',{exact:true})).toHaveValue('Peer name');
  }finally{await context.close();}
});

 test('reference worlds expose unavailable purchase diagnostics while retaining local stock browsing',async({page})=>{
  const initial=await state(page);
  const save=readFileSync('../prototype/protocol/saves/reference-v9.json');
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+initial.worldEpoch,{data:save})).ok()).toBe(true);
  await page.goto('/');await ready(page);await openPanel(page,'fleet');
  await expect(page.getByText('Rolling stock is unavailable for this world or selected pack.')).toBeVisible();
  await expect(page.getByRole('button',{name:'Get purchase quote',exact:true})).toBeDisabled();
  await page.getByLabel('Search catalog',{exact:true}).fill('Color test');await expect(page.locator('.fleet-catalog li')).toHaveCount(1);
 });
