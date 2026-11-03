import { mkdtempSync,readFileSync,rmSync,truncateSync,writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { expect,test } from './fixtures.ts';
import { canonicalState,closePanel,openPanel,ready } from './browser-helpers.ts';
const fixture=process.env.FREETRAIN_MIXED_NETWORK_OUTPUT;
if(!fixture)throw new Error('Use deno task test:browser:continuation to generate the required gameplay fixture.');
for(const locale of ['en','ja'] as const)test(`complete P4 network slot 2, replacement, reconnect and recovery ${locale}`,async({page,browser})=>{
 test.setTimeout(120_000);await page.goto('/');await ready(page);
 const original=await(await page.request.get('/api/saves/download')).body(),network=readFileSync(fixture!);
 const text=JSON.parse(readFileSync(`src/locales/${locale}/app.json`,'utf8'));
 const errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 const peer=await browser.newPage();
 async function replace(bytes:Buffer){const s=await canonicalState(page);expect((await page.request.post(`/api/saves/upload?expectedWorldEpoch=${s.worldEpoch}`,{data:bytes})).ok()).toBe(true);}
 async function saved(){return(await page.request.get('/api/saves/download')).body();}
 try{
  await replace(network);await page.reload();await ready(page);
  if(locale==='ja'){await openPanel(page,'settings');await page.getByLabel('Language',{exact:true}).selectOption('ja');await closePanel(page);await ready(page,'ja');await page.setViewportSize({width:430,height:850});}
  await expect(page.locator('canvas')).toHaveAttribute('data-ruleset','mixed-railway-1');
  await peer.goto('/');await ready(peer);
  await openPanel(page,'saves',locale);await page.getByLabel(text.saveSlot,{exact:true}).selectOption('slot2');
  await page.getByRole('button',{name:text.saveGame,exact:true}).click();await expect(page.getByTestId('save-feedback')).toHaveText(text.saved);
  expect(await saved()).toEqual(network);
  const [download]=await Promise.all([page.waitForEvent('download'),page.getByRole('button',{name:text.downloadSave,exact:true}).click()]);
  expect(readFileSync((await download.path())!)).toEqual(network);
  const epoch=(await canonicalState(page)).worldEpoch;
  await page.getByRole('button',{name:text.loadGame,exact:true}).click();await page.getByRole('button',{name:text.confirmLoad,exact:true}).click();
  await ready(page,locale);
  await expect.poll(async()=>(await canonicalState(peer)).worldEpoch).not.toBe(epoch);expect(await saved()).toEqual(network);
  await openPanel(page,'session',locale);await page.getByRole('button',{name:text.reconnect,exact:true}).click();await ready(page,locale);
  await page.reload();await ready(page,locale);expect(await saved()).toEqual(network);
  await openPanel(page,'saves',locale);
  for(const [invalid,message] of [[Buffer.from('{broken'),text.invalidSave],[Buffer.from(network.toString().replace(/"catalogHash":"[a-f0-9]+"/,`"catalogHash":"${'0'.repeat(64)}"`)),text.incompatibleSave]] as const){
   await expect(page.getByLabel(text.uploadSave,{exact:true})).toBeEnabled();
   await page.getByLabel(text.uploadSave,{exact:true}).setInputFiles({name:'rejected-mixed.json',mimeType:'application/json',buffer:invalid});
   await expect(page.getByTestId('save-feedback')).toContainText(message);expect(await saved()).toEqual(network);
   await expect(page.locator('.decision-dialog')).toHaveCount(0);
  }
  const oversized=mkdtempSync(join(tmpdir(),'mixed-oversize-')),large=join(oversized,'large.json');
  try{await expect(page.getByLabel(text.uploadSave,{exact:true})).toBeEnabled();writeFileSync(large,'x');truncateSync(large,256*1024*1024+1);await page.getByLabel(text.uploadSave,{exact:true}).setInputFiles(large);await expect(page.getByTestId('save-feedback')).toHaveText(text.saveTooLarge);expect(await saved()).toEqual(network);}
  finally{rmSync(oversized,{recursive:true,force:true});}
  await expect(page.getByLabel(text.uploadSave,{exact:true})).toBeEnabled();
  await page.getByLabel(text.uploadSave,{exact:true}).setInputFiles({name:'mixed-network.json',mimeType:'application/json',buffer:network});
  const recoveryEpoch=(await canonicalState(page)).worldEpoch;
  await page.getByRole('button',{name:text.confirmLoad,exact:true}).click();await expect.poll(async()=>(await canonicalState(page)).worldEpoch).not.toBe(recoveryEpoch);await ready(page,locale);await openPanel(page,'saves',locale);
  expect(await saved()).toEqual(network);expect(errors).toEqual([]);

 }finally{await peer.close();await replace(original);}
});
