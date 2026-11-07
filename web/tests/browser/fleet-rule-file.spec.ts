import {test,expect,type Page} from './fixtures.ts';
import {canonicalState as state,openPanel,closePanel,ready} from './browser-helpers.ts';
import {readFileSync,mkdirSync} from 'node:fs';
const en=JSON.parse(readFileSync('src/locales/en/app.json','utf8'));
const ja=JSON.parse(readFileSync('src/locales/ja/app.json','utf8'));
const expected=JSON.parse(readFileSync('tests/fixtures/timetable/expected.json','utf8'));
const evidence=process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!;
for(const locale of ['en','ja'] as const)test(`${locale} rule files preview ordered append, portable round trip, peer conflict and restart`,async({page,browser,baseURL,worldServer})=>{
 test.setTimeout(120000);const text=locale==='ja'?ja:en,errors:string[]=[];page.on('pageerror',e=>errors.push(String(e)));
 const initial=await state(page),input=readFileSync('../tmp/p5-23-rule-fleet.json');
 expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+initial.worldEpoch,{data:input})).ok()).toBe(true);
 await page.goto('/');await ready(page);
 if(locale==='ja'){await openPanel(page,'settings');await page.getByLabel('Language',{exact:true}).selectOption('ja');await closePanel(page);await page.setViewportSize({width:390,height:844});await page.evaluate(()=>document.documentElement.style.fontSize='200%');}
 const baseline=await state(page),source=baseline.mixed.fleet.controllers.find((c:any)=>c.kind==='timetable');
 async function manager(p:Page,t=text,l=locale){await openPanel(p,'fleet',l);await p.getByRole('button',{name:t.controllerManager,exact:true}).click();const controls=p.locator('.fleet-controller-manager');await controls.getByLabel(t.controllerManager,{exact:true}).selectOption(source.id);return controls;}
 const controls=await manager(page);
 async function rules(){
  await controls.getByRole('button',{name:text.controllerConfigure,exact:true}).click();const dialog=page.getByRole('dialog');
  await dialog.getByRole('button',{name:text.controllerRules,exact:true}).click();return page.getByRole('dialog',{name:text.controllerRules,exact:true});
 }
 const saved=async()=>(await page.request.get('/api/saves/download')).body();
 const current=async()=>(await state(page)).mixed.fleet.controllers.find((c:any)=>c.id===source.id);
 let dialog=await rules();const before=await saved();
 const legacy={name:'diagram.ftdd',mimeType:'application/octet-stream',buffer:readFileSync('tests/fixtures/timetable/single.ftdd')};
 await dialog.getByLabel(text.ruleFileRead,{exact:true}).setInputFiles(legacy);
 await expect(dialog.getByRole('list',{name:text.ruleFilePreview,exact:true}).getByRole('listitem')).toHaveCount(9);
 await expect(dialog).toContainText(text.ruleFileLegacy);await expect(dialog).toContainText(text.ruleFileAppendHelp);
 expect(await saved()).toEqual(before);
 mkdirSync(evidence+'/rule-files',{recursive:true});await dialog.getByRole('list',{name:text.ruleFilePreview,exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:evidence+`/rule-files/${locale}-preview.png`});
 await dialog.getByRole('button',{name:text.cancel,exact:true}).click();expect(await saved()).toEqual(before);
 await dialog.getByLabel(text.ruleFileRead,{exact:true}).setInputFiles({name:'wrong.ftdd.json',mimeType:'application/json',buffer:Buffer.from('{"format":"wrong","version":1,"rules":[]}')});
 await expect(dialog).toContainText(text.ruleFileError);expect(await saved()).toEqual(before);
 await dialog.getByLabel(text.ruleFileRead,{exact:true}).setInputFiles({name:'oversize.ftdd',mimeType:'application/octet-stream',buffer:Buffer.alloc(512*1024+1)});await expect(dialog).toContainText(text.ruleFileError);expect(await saved()).toEqual(before);
 await dialog.getByLabel(text.ruleFileRead,{exact:true}).setInputFiles(legacy);
 await dialog.getByRole('button',{name:text.ruleFileAppend,exact:true}).click();await expect(dialog.getByRole('button',{name:text.ruleFileAppend,exact:true})).toHaveCount(0);
 expect((await current()).harbors[0].rules).toEqual([source.harbors[0].rules[0],...expected]);
 const downloadEvent=page.waitForEvent('download');await dialog.getByRole('button',{name:text.ruleFileSave,exact:true}).click();const download=await downloadEvent;
 expect(download.suggestedFilename()).toBe('diagram.ftdd.json');const exported=readFileSync((await download.path())!);const portable=JSON.parse(exported.toString());
 expect(portable.format).toBe('freetrain-online-timetable');expect(portable.rules).toEqual((await current()).harbors[0].rules);
 await dialog.getByLabel(text.ruleFileRead,{exact:true}).setInputFiles({name:'diagram.ftdd.json',mimeType:'application/json',buffer:exported});
 await expect(dialog).toContainText(text.ruleFilePortable);await dialog.getByRole('button',{name:text.ruleFileAppend,exact:true}).click();await expect.poll(async()=>(await current()).harbors[0].rules.length).toBe(20);
 expect((await current()).harbors[0].rules).toEqual([...portable.rules,...portable.rules]);
 await dialog.getByLabel(text.controllerRules,{exact:true}).selectOption('1');await dialog.getByRole('button',{name:text.ruleEdit,exact:true}).click();await dialog.getByLabel(text.ruleAction,{exact:true}).selectOption('stop');await dialog.getByRole('button',{name:text.controllerApply,exact:true}).click();
 await expect(dialog.getByRole('button',{name:text.controllerApply,exact:true})).toHaveCount(0);
 expect((await current()).harbors[0].rules[9].action).toBe('pass');
 // Capture the member revision before file reading; a peer rename invalidates
 // the pending append even though the actual rules are unchanged.
 await page.evaluate(()=>{
  const original=File.prototype.arrayBuffer;
  File.prototype.arrayBuffer=async function(){const bytes=await original.call(this);return new Promise<ArrayBuffer>(resolve=>{(window as any).releaseRuleRead=()=>{File.prototype.arrayBuffer=original;resolve(bytes);};});};
 });
 await dialog.getByLabel(text.ruleFileRead,{exact:true}).setInputFiles(legacy);await expect(dialog).toContainText(text.ruleFileReading);
 const context=await browser.newContext({baseURL}),peer=await context.newPage();
 try{
  expect((await peer.request.post('/api/access',{data:{secret:'operator-test'}})).ok()).toBe(true);await peer.goto('/');await ready(peer);
  const remote=await manager(peer,en,'en');await remote.getByRole('button',{name:en.controllerRename,exact:true}).click();const rename=peer.getByRole('dialog');await rename.getByLabel(en.controllerName,{exact:true}).fill('Peer rules');await rename.getByRole('button',{name:en.controllerApply,exact:true}).click();
  await expect.poll(()=>page.evaluate(()=>typeof (window as any).releaseRuleRead)).toBe('function');await page.evaluate(()=>(window as any).releaseRuleRead());
  await expect(dialog).toContainText(text.controllerConflict);await expect(dialog.getByRole('button',{name:text.ruleFileAppend,exact:true})).toBeDisabled();
  const afterPeer=await saved();await dialog.getByRole('button',{name:text.cancel,exact:true}).click();expect(await saved()).toEqual(afterPeer);
 }finally{await context.close();}
 await dialog.getByRole('button',{name:text.close,exact:true}).click();
 const observerContext=await browser.newContext({baseURL}),observer=await observerContext.newPage();
 try{
  expect((await observer.request.post('/api/access',{data:{secret:'observer-test'}})).ok()).toBe(true);await observer.goto('/');await ready(observer);const remote=await manager(observer,en,'en');
  await remote.getByRole('button',{name:en.controllerConfigure,exact:true}).click();await observer.getByRole('dialog').getByRole('button',{name:en.controllerRules,exact:true}).click();
  await expect(observer.getByRole('dialog').getByLabel(en.ruleFileRead,{exact:true})).toBeDisabled();await expect(observer.getByRole('dialog').getByRole('button',{name:en.ruleFileSave,exact:true})).toBeEnabled();
 }finally{await observerContext.close();}
 const finalRules=(await current()).harbors[0].rules,downloaded=await saved(),epoch=(await state(page)).worldEpoch;
 expect((await page.request.post('/api/saves/upload?expectedWorldEpoch='+epoch,{data:downloaded})).ok()).toBe(true);
 await expect(page.getByRole('dialog')).toHaveCount(0);await page.goto('/');await ready(page,locale);expect((await current()).harbors[0].rules).toEqual(finalRules);
 await closePanel(page);await worldServer.restart!();expect((await page.request.post('/api/access',{data:{secret:''}})).ok()).toBe(true);await page.reload();await ready(page,locale);
 expect((await current()).harbors[0].rules).toEqual(finalRules);const final=await state(page);expect(final.tick).toBe(baseline.tick);expect(final.cash).toBe(baseline.cash);expect(final.mixed.queue).toEqual(baseline.mixed.queue);expect(errors).toEqual([]);
});
