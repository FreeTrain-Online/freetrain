import { expect, test } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import {readFileSync,mkdtempSync,writeFileSync,truncateSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import { ready, openPanel } from './ux-helpers.ts';
const ja=JSON.parse(readFileSync('src/locales/ja/app.json','utf8'));
const bootstrap = async (page:any) => (await (await page.request.get('/api/bootstrap?protocolVersion=5&contentVersion=prototype-content-1')).json()).snapshot;
const pause = async(page:any)=>{if(!(await bootstrap(page)).paused)await page.getByRole('button',{name:'Pause',exact:false}).click();};

test('prototype and relabelled saves are rejected; writing is unavailable and world stays intact',async({page})=>{
  await page.goto('/');await ready(page);await pause(page);const before=await bootstrap(page);
  await openPanel(page,'session');await expect(page.getByTestId('compatibility-mode')).toHaveText('Temporary reference world');
  await openPanel(page,'saves');
  await expect(page.getByTestId('saving-unavailable')).toHaveText('Saving is unavailable for this reference world.');
  await expect(page.getByRole('button',{name:'Save slot',exact:true})).toBeDisabled();
  await expect(page.getByRole('button',{name:'Download save',exact:true})).toBeDisabled();
  const fixture=JSON.parse(await readFile('../prototype/protocol/historical/save-v1/save-reference.json','utf8'));
  for(const version of [1,2,999]){
    const buffer=Buffer.from(JSON.stringify({...fixture,saveVersion:version}));
    await expect(page.getByLabel('Upload save',{exact:true})).toBeEnabled();
    await page.getByLabel('Upload save',{exact:true}).setInputFiles({name:'unsupported.json',mimeType:'application/json',buffer});
    await expect(page.getByTestId('save-feedback')).toContainText('Prototype saves cannot be loaded');
    await expect(page.locator('.decision-dialog')).toHaveCount(0);
    expect(await bootstrap(page)).toEqual(before);
  }
});

test('malformed and oversized data report errors without replacing the live world',async({page})=>{
  await page.goto('/');await ready(page);await pause(page);const before=await bootstrap(page);await openPanel(page,'saves');
  for(const [buffer,message] of [[Buffer.from('{broken'),'Invalid save data']] as const){
    await expect(page.getByLabel('Upload save',{exact:true})).toBeEnabled();
    await page.getByLabel('Upload save',{exact:true}).setInputFiles({name:'bad.json',mimeType:'application/json',buffer});
    await expect(page.getByTestId('save-feedback')).toContainText(message);expect(await bootstrap(page)).toEqual(before);
  }
  const directory=mkdtempSync(join(tmpdir(),'freetrain-oversized-')),path=join(directory,'large.json');
  try{
    writeFileSync(path,'x');truncateSync(path,256*1024*1024+1);
    await page.getByLabel('Upload save',{exact:true}).setInputFiles(path);
    await expect(page.getByTestId('save-feedback')).toContainText('256 MiB');expect(await bootstrap(page)).toEqual(before);
  }finally{rmSync(directory,{recursive:true,force:true});}
});

test('ignored checkpoint diagnostic has a readable Japanese error and disables writing',async({browser})=>{
  const context=await browser.newContext({locale:'ja-JP',viewport:{width:640,height:1000},deviceScaleFactor:2});
  try {
    const page=await context.newPage();
    await page.route('**/api/saves/status',route=>route.fulfill({json:{savingAvailable:false,loadingAvailable:false,startupError:'incompatibleSave'}}));
    await page.goto('/');await ready(page,'ja');await openPanel(page,'saves','ja');
    await expect(page.getByTestId('startup-save-error')).toContainText(ja.incompatibleSave);
    await expect(page.getByTestId('saving-unavailable')).toHaveText(ja.savingUnavailable);
    await expect(page.getByText(ja.loadHint,{exact:true})).toBeVisible();
    expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
    await page.screenshot({path:'../tmp/parity-p1/save-policy-ja.png',fullPage:true});
  } finally {await context.close();}
});

test('bundled WAV and a transport sound fixture play once; reconnect never replays it', async ({ page }) => {
  let pushSound:((epoch:string)=>void)|undefined;
  await page.routeWebSocket('**/api/session?*',route=>{route.connectToServer();pushSound=epoch=>route.send(JSON.stringify({type:'soundEvent',eventId:'audio-fixture-once',worldEpoch:epoch,kind:'stationArrived'}));});
  await page.addInitScript(() => {
    (window as unknown as { soundPlays: number }).soundPlays = 0;
    const original = HTMLMediaElement.prototype.play;
    HTMLMediaElement.prototype.play = function () {
      (window as unknown as { soundPlays: number }).soundPlays++;
      this.addEventListener('loadedmetadata', () => { (window as unknown as { wavDuration: number }).wavDuration = this.duration; }, { once: true });
      return original.call(this);
    };
  });
  await page.goto('/'); await ready(page);
  expect(await page.evaluate(() => (window as unknown as { soundPlays: number }).soundPlays)).toBe(0);
  await openPanel(page, 'settings');
  await page.getByRole('button', { name: 'Enable sound', exact: true }).click();
  await expect(page.getByTestId('sound-feedback')).toHaveText('Station sound on');
  await expect.poll(() => page.evaluate(() => (window as unknown as { wavDuration: number }).wavDuration)).toBeGreaterThan(0);
  const count = await page.evaluate(() => (window as unknown as { soundPlays: number }).soundPlays);
  await openPanel(page, 'session');
  await page.getByRole('button', { name: 'Reconnect', exact: true }).click(); await ready(page);
  expect(await page.evaluate(() => (window as unknown as { soundPlays: number }).soundPlays)).toBe(count);
  const epoch=(await bootstrap(page)).worldEpoch;
  pushSound!(epoch);
  await expect.poll(() => page.evaluate(() => (window as unknown as { soundPlays: number }).soundPlays)).toBe(count + 1);
  await openPanel(page, 'session');
  await page.getByRole('button', { name: 'Reconnect', exact: true }).click(); await ready(page);
  expect(await page.evaluate(() => (window as unknown as { soundPlays: number }).soundPlays)).toBe(count + 1);
  await openPanel(page, 'settings');
  await page.getByRole('button', { name: 'Disable sound', exact: true }).click();
  await expect(page.getByTestId('sound-feedback')).toHaveText('Sound off');
});
