import {test,expect} from './fixtures.ts';
import {canonicalState,openPanel,ready} from './browser-helpers.ts';
import {readFileSync} from 'node:fs';
const messages=Object.fromEntries(['en','ja'].map(l=>[l,JSON.parse(readFileSync(`src/locales/${l}/app.json`,'utf8'))]));

for(const narrow of [false,true])test.describe(narrow?'coarse 390×844 200%':'desktop',()=>{
 test.use({hasTouch:narrow,viewport:narrow?{width:390,height:844}:{width:1280,height:900}});
 for(const locale of ['en','ja'])test(`P5-29 ${locale} trade and locale navigation protect a train draft`,async({page})=>{
  const errors:string[]=[];page.on('pageerror',error=>errors.push(String(error)));
  await page.addInitScript(({locale,narrow})=>{
   localStorage.setItem('freetrain-online.locale',locale);
   if(narrow)document.addEventListener('DOMContentLoaded',()=>document.documentElement.style.fontSize='200%');
  },{locale,narrow});
  const text=messages[locale],other=locale==='en'?'ja':'en',translated=messages[other];
  await page.goto('/');await ready(page,locale);
  expect((await(await page.request.get('/api/content')).json()).packs).toEqual([]);
  const before=await canonicalState(page);
  const preview=await page.request.post('/api/worlds/empty/preview',{data:{name:'Bilingual fleet reconciliation',dimensions:{h:40,v:48,z:6},waterLevel:1,epoch:before.worldEpoch,revision:before.revision}});
  expect(preview.ok(),await preview.text()).toBe(true);const candidate=await preview.json();
  expect((await page.request.post('/api/worlds/empty/commit',{data:{id:candidate.id,requestHash:candidate.requestHash,candidateHash:candidate.candidateHash,epoch:before.worldEpoch,revision:before.revision}})).ok()).toBe(true);
  await ready(page,locale);
  // Build real normal rail through the authoring controls before buying stock.
  await page.getByRole('button',{name:text.trackTask,exact:true}).click();
  for(const [key,value] of [['railFromX',22],['railFromY',27],['railToX',40],['railToY',27],['buildLevel',1]] as const)
   await page.getByLabel(text[key],{exact:true}).fill(String(value));
  await page.getByRole('button',{name:text.railCheck,exact:true}).click();
  await page.getByRole('button',{name:text.confirmTrack,exact:true}).click();
  await expect(page.getByTestId('rail-route')).toHaveCount(0);
  await openPanel(page,'fleet',locale);
  const initial=await canonicalState(page);
  await page.getByLabel(text.stockLength,{exact:true}).selectOption('3');
  await page.getByRole('button',{name:text.stockQuote,exact:true}).click();
  await page.getByRole('button',{name:text.stockConfirmBuy,exact:true}).click();
  await page.getByRole('button',{name:new RegExp('^'+text.ownedStock)}).click();
  await page.locator('.fleet-owned .catalog-item-main').first().click();
  const purchased=await canonicalState(page),original=purchased.mixed.fleet.trains[0].name;
  await page.getByLabel(text.stockName,{exact:true}).fill('未保存・Unsaved');
  // Settings is the supported locale-change route. Navigation must resolve the
  // draft before it can unmount the train form, including on a coarse layout.
  const settings=async()=>{
   await page.locator('.command-bar').getByRole('button',{name:locale==='ja'?'ゲーム ▾':'Game ▾',exact:true}).click();
   await page.locator('#game-menu').getByRole('button',{name:locale==='ja'?'設定':'Settings',exact:true}).click();
  };
  await settings();
  let decision=page.getByRole('dialog',{name:text.stockDirtyTitle,exact:true});
  await expect(decision).toContainText('未保存・Unsaved');
  const capture=async(stage:string)=>page.screenshot({path:process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE+`/p5-29-${locale}-${narrow?'coarse':'desktop'}-${stage}.png`});
  await capture('draft');
  await decision.getByRole('button',{name:text.keepEditing,exact:true}).click();
  await expect(page.getByLabel(text.stockName,{exact:true})).toHaveValue('未保存・Unsaved');
  expect((await canonicalState(page)).mixed).toEqual(purchased.mixed);
  await settings();
  decision=page.getByRole('dialog',{name:text.stockDirtyTitle,exact:true});
  await decision.getByRole('button',{name:text.discard,exact:true}).click();
  await page.getByLabel(text.language,{exact:true}).selectOption(other);
  await openPanel(page,'fleet',other);
  await page.getByRole('button',{name:new RegExp('^'+translated.ownedStock)}).click();
  await page.locator('.fleet-owned .catalog-item-main').first().click();
  await expect(page.getByLabel(translated.stockName,{exact:true})).toHaveValue(original);
  expect((await canonicalState(page)).mixed).toEqual(purchased.mixed);
  for(const [axis,value] of [['X',26],['Y',27],['Z',1]] as const)
   await page.locator('.deployment-controls').getByLabel(axis,{exact:true}).fill(String(value));
  await page.locator('.deployment-controls').getByLabel('Z',{exact:true}).press('Enter');
  await expect(page.getByTestId('deployment-car')).toHaveCount(3);
  await capture('deployment');
  await page.getByRole('button',{name:translated.deploymentConfirm,exact:true}).click();
  await expect(page.getByRole('button',{name:translated.withdrawTrain,exact:true})).toBeVisible();
  expect((await canonicalState(page)).mixed.fleet.trains[0].cars.every((c:any)=>c.state.kind==='inside')).toBe(true);
  await page.getByRole('button',{name:translated.withdrawTrain,exact:true}).click();
  await page.getByRole('button',{name:translated.withdrawConfirm,exact:true}).click();
  await page.getByRole('button',{name:translated.stockSaleQuote,exact:true}).click();
  await page.getByRole('button',{name:translated.stockConfirmSell,exact:true}).click();
  await expect.poll(async()=>(await canonicalState(page)).mixed.fleet.trains.length).toBe(0);
  const sold=await canonicalState(page);expect(sold.tick).toBe(initial.tick);
  expect(sold.mixed.accounts.transactions.length).toBe(purchased.mixed.accounts.transactions.length+1);
  expect(errors).toEqual([]);
  await page.screenshot({path:process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE+`/p5-29-${locale}-${narrow?'coarse':'desktop'}.png`});
 });
});
