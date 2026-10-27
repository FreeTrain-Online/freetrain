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
