import { test, expect } from '@playwright/test';

test('server-selected source directories prepare at startup and expose separate localized credits',async({page})=>{
  await page.goto('/');
  await expect(page.getByTestId('connection')).toHaveText('Connected to game',{timeout:30000});
  await page.getByRole('button',{name:'Game ▾',exact:true}).click();
  const one=page.getByRole('button',{name:'fixture-one credits',exact:true});
  await expect(one).toBeVisible();
  await expect(page.getByRole('button',{name:'fixture-two credits',exact:true})).toBeVisible();
  await one.focus();await page.keyboard.press('Enter');
  const dialog=page.getByRole('dialog',{name:'Fixture credits'});
  await expect(dialog).toBeVisible();await expect(dialog).toContainText('Second paragraph.');
  await expect(dialog.getByRole('link',{name:'notice.txt'})).toHaveAttribute('href',/\/api\/content\/fixture-one\/[a-f0-9]{64}\/notices\/notice.txt/);
  await page.keyboard.press('Escape');await expect(dialog).toBeHidden();
  await expect(page.getByRole('button',{name:'Game ▾',exact:true})).toBeFocused();
  const content=await (await page.request.get('/api/content')).json();
  expect(content.packs.map((p:{id:string})=>p.id)).toEqual(['fixture-one','fixture-two']);
  for(const pack of content.packs) expect((await page.request.get(pack.catalogUrl)).status()).toBe(200);
});

test('Japanese credits and narrow long-text dialog remain reachable with keyboard close',async({page})=>{
  await page.addInitScript(()=>localStorage.setItem('freetrain-online.locale','ja'));
  await page.goto('/');
  await expect(page.getByTestId('connection')).toHaveText('ゲームに接続済み',{timeout:30000});
  await page.getByRole('button',{name:'ゲーム ▾',exact:true}).click();
  await page.getByRole('button',{name:'fixture-two 謝辞',exact:true}).click();
  const dialog=page.getByRole('dialog',{name:'謝辞',exact:true});
  await expect(dialog).toContainText('検証用の画像。');
  await page.keyboard.press('Tab');await page.keyboard.press('Escape');await expect(dialog).toBeHidden();
});

test('changed authoritative catalog blocks join and never reveals a ready world',async({page})=>{
  let sockets=0;page.on('websocket',()=>sockets++);
  await page.route('**/api/content/fixture-one/**/catalog.json',async route=>{await route.fulfill({status:200,body:'{}',contentType:'application/json'});});
  await page.goto('/');
  await expect(page.getByRole('heading',{name:'Update required',exact:true})).toBeVisible();
  expect(sockets).toBe(0);
  await expect(page.getByRole('button',{name:'Game ▾',exact:true})).toBeHidden();
  await expect(page.locator('.session-dialog')).toBeVisible({timeout:15000});
});

test('reviewed fixed map uses exact prepared source bytes and metadata and reopens as realized terrain',async({page})=>{
 await page.goto('/');await expect(page.getByTestId('connection')).toHaveText('Connected to game',{timeout:30000});
 const original=await(await page.request.get('/api/saves/download')).body();
 try{
  await page.getByRole('button',{name:'Game ▾',exact:true}).click();await page.getByRole('button',{name:'New world',exact:true}).click();
  await page.getByLabel('World source',{exact:true}).selectOption('heightmap');await page.getByLabel('Prepared map',{exact:true}).selectOption('fixture-map');
  await expect(page.getByLabel('World name',{exact:true})).toHaveValue('Prepared terrain');await expect(page.getByLabel('Stored width (H)',{exact:true})).toHaveValue('3');await expect(page.getByLabel('Stored width (H)',{exact:true})).toBeDisabled();
  await expect(page.getByRole('dialog',{name:'New world',exact:true})).toContainText('Fixture author');
  await page.getByRole('button',{name:'Preview world',exact:true}).click();await expect(page.getByTestId('empty-world-preview')).toContainText('3 × 2 × 6');
  await page.getByRole('button',{name:'Create world',exact:true}).click();await page.locator('dialog.decision-dialog').getByRole('button',{name:'Create world',exact:true}).click();
  await expect(page.locator('canvas')).toHaveAttribute('data-ruleset','terrain-state-1');await expect(page.getByTestId('connection')).toHaveText('Connected to game');
  const captured=await(await page.request.get('/api/saves/download')).body(),data=JSON.parse(captured.toString());
  // The resulting terrain uses bundled geometry. The exact source pack/resource
  // remains provenance; reopening cannot require or decode its image again.
  expect(JSON.parse(data.packedTerrain.provenance.requestJson).Image.Provider.PackId).toBe('fixture-one');
  await page.reload();await expect(page.getByTestId('connection')).toHaveText('Connected to game',{timeout:30000});expect(await(await page.request.get('/api/saves/download')).body()).toEqual(captured);
 }finally{
  const state=await(await page.request.get('/api/bootstrap?protocolVersion=8&contentVersion=terrain-content-1&contentVersion=reference-spatial-content-2')).json();
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+state.worldEpoch,{data:original})).ok()).toBe(true);
 }
});
