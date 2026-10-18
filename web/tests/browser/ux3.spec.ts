import { expect, test, type Page } from '@playwright/test';
import { mkdir, writeFile } from 'node:fs/promises';
import { bootstrap, clickSprite, clickTile, closePanel, openPanel, ready, selectStation, selectTrain } from './ux-helpers.ts';
const evidence = process.env.FREETRAIN_UX3_EVIDENCE!;
const sizes = [[1920,1080],[1280,720],[1024,768],[640,900],[360,800],[800,450]];
test.beforeEach(async () => { await mkdir(evidence, { recursive: true }); });
async function fit(page: Page) {
  const bounds = await page.evaluate(() => {
    window.scrollTo(100, 100);
    return { width: innerWidth, height: innerHeight, scrollWidth: document.documentElement.scrollWidth,
      scrollHeight: document.documentElement.scrollHeight, x: scrollX, y: scrollY };
  });
  expect(bounds.scrollWidth).toBeLessThanOrEqual(bounds.width); expect(bounds.scrollHeight).toBeLessThanOrEqual(bounds.height);
  expect(bounds.x).toBe(0); expect(bounds.y).toBe(0); return bounds;
}
async function settled(page: Page) { await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))); }

test('UX3 responsive Japanese panels, scrolling, expansion and narrow Step stay usable', async ({ page }) => {
  await page.goto('/'); await ready(page); await openPanel(page, 'settings');
  await page.getByLabel('Language', { exact: true }).selectOption('ja'); await closePanel(page);
  const measurements: unknown[] = [];
  for (const [width,height] of sizes) {
    await page.setViewportSize({ width,height });
    for (const panel of ['help','catalog','saves']) {
      await openPanel(page, panel, 'ja');
      const bounds = await fit(page), box = (await page.locator('.primary-panel').boundingBox())!;
      expect(box.x).toBeGreaterThanOrEqual(0); expect(box.y).toBeGreaterThanOrEqual(0);
      expect(box.x + box.width).toBeLessThanOrEqual(width); expect(box.y + box.height).toBeLessThanOrEqual(height);
      await expect(page.getByRole('button', { name: 'パネルを閉じる', exact: true })).toBeInViewport();
      if (width === 360) {
        const stage = (await page.locator('.map-stage').boundingBox())!;
        expect(box.height / stage.height).toBeLessThanOrEqual(.53);
        await page.getByRole('button', { name: 'パネルを拡大', exact: true }).click();
        await expect(page.getByRole('button', { name: 'パネルを縮小', exact: true })).toHaveAttribute('aria-expanded', 'true');
      }
      const camera = await page.locator('canvas').getAttribute('data-camera'), zoom = await page.locator('canvas').getAttribute('data-zoom');
      const body = page.locator('.primary-panel .panel-body'); await body.hover(); await page.mouse.wheel(0, 1000);
      if (await body.evaluate(el => el.scrollHeight > el.clientHeight)) await expect.poll(() => body.evaluate(el => el.scrollTop)).toBeGreaterThan(0);
      await expect(page.locator('canvas')).toHaveAttribute('data-camera', camera!); await expect(page.locator('canvas')).toHaveAttribute('data-zoom', zoom!);
      measurements.push({width,height,panel,bounds,box,scroll:await body.evaluate(el => ({ top:el.scrollTop, height:el.clientHeight, total:el.scrollHeight }))});
      await page.screenshot({path:`${evidence}/${panel}-ja-${width}x${height}.png`});
    }
    await closePanel(page);
  }
  await page.setViewportSize({width:360,height:800});
  await page.getByRole('button', {name:'ゲーム ▾',exact:true}).click();
  if (await page.getByTestId('clock-state').textContent() === '運転中') {
    await page.keyboard.press('Escape'); await page.getByRole('button',{name:'Ⅱ 一時停止',exact:true}).click();
    await page.getByRole('button', {name:'ゲーム ▾',exact:true}).click();
  }
  const before = await bootstrap(page);
  await page.getByRole('button',{name:'1ステップ',exact:true}).click();
  await expect.poll(async () => (await bootstrap(page)).tick).toBe((BigInt(before.tick)+1n).toString());
  await page.keyboard.press('Escape');
  await page.setViewportSize({width:1280,height:720}); await settled(page); await selectStation(page);
  await page.setViewportSize({width:360,height:450});
  await page.getByLabel('駅名',{exact:true}).fill('Narrow editing');
  for (const button of await page.locator('.panel-footer button').all()) await expect(button).toBeInViewport({ratio:1});
  await page.getByLabel('駅名',{exact:true}).scrollIntoViewIfNeeded(); await expect(page.getByLabel('駅名',{exact:true})).toBeInViewport();
  await fit(page); await page.screenshot({path:`${evidence}/station-reduced-height-ja.png`});
  await writeFile(`${evidence}/responsive.json`, JSON.stringify(measurements,null,2));
});

test('UX3 keyboard toolbars, native editing, Escape layers and deliberate map focus', async ({ page }) => {
  await page.goto('/'); await ready(page);
  const game = page.getByRole('button',{name:'Game ▾',exact:true}); await game.focus();
  await page.keyboard.press('End'); await expect(page.getByRole('button',{name:/Session ·/})).toBeFocused();
  await page.keyboard.press('Home'); await expect(game).toBeFocused();
  await page.keyboard.press('Enter'); await page.keyboard.press('End');
  await expect(page.locator('#game-menu button').last()).toBeFocused();
  await page.keyboard.press('Escape'); await expect(game).toBeFocused();
  const inspect = page.getByRole('button',{name:'Inspect / drive',exact:true}); await inspect.focus();
  await page.keyboard.press('ArrowDown'); await expect(page.getByRole('button',{name:'Build track',exact:true})).toBeFocused();
  await page.keyboard.press('Enter'); await expect(page.locator('canvas')).toBeFocused();
  await expect(page.getByTestId('map-selection')).toContainText('Build track');
  await page.keyboard.press('Escape'); await expect(inspect).toHaveAttribute('aria-pressed','true');
  await selectStation(page); const input = page.getByLabel('Station name',{exact:true}); await input.fill('Editing');
  await input.press('Home'); await input.press('Shift+End');
  expect(await input.evaluate(el => (el as HTMLInputElement).selectionEnd)).toBe(7);
  await input.press('Escape'); await expect(page.locator('.decision-dialog')).toHaveCount(0);
  await page.getByRole('button',{name:'Close panel',exact:true}).focus(); await page.keyboard.press('Escape');
  await expect(page.locator('.decision-dialog')).toBeVisible(); await page.keyboard.press('Escape');
  await expect(input).toHaveValue('Editing'); await expect(page.locator('.primary-panel')).toBeVisible();
  await page.getByRole('button',{name:'Reset draft',exact:true}).click();
  await openPanel(page,'settings');
  const scheme = page.getByLabel('UI color scheme',{exact:true}); await scheme.focus();
  await page.keyboard.press('p'); await page.keyboard.press('Enter'); await expect(scheme).toHaveValue('plum');
});

test('UX3 DPR, resize, pan and zoom preserve backing size, camera and sprite picking', async ({ browser }) => {
  const results: unknown[] = [];
  for (const dpr of [1,2]) {
    const context = await browser.newContext({ deviceScaleFactor:dpr, viewport:{width:1280,height:720} });
    try {
      const page = await context.newPage(); await page.goto('/'); await ready(page);
      const canvas = page.locator('canvas');
      await page.getByRole('button',{name:'Zoom in',exact:true}).click(); await settled(page);
      const box = (await canvas.boundingBox())!; await page.mouse.move(box.x+box.width/2,box.y+box.height/2);
      await page.mouse.down(); await page.mouse.move(box.x+box.width/2+35,box.y+box.height/2+20); await page.mouse.up(); await settled(page);
      const camera = await canvas.getAttribute('data-camera'), zoom = await canvas.getAttribute('data-zoom');
      await page.setViewportSize({width:1024,height:768}); await settled(page);
      await expect(canvas).toHaveAttribute('data-camera',camera!); await expect(canvas).toHaveAttribute('data-zoom',zoom!);
      const dimensions = await canvas.evaluate(el => {const c=el as HTMLCanvasElement,r=c.getBoundingClientRect();return {w:c.width,h:c.height,cssW:r.width,cssH:r.height,dpr:devicePixelRatio};});
      expect(dimensions.w).toBe(Math.round(dimensions.cssW*dpr)); expect(dimensions.h).toBe(Math.round(dimensions.cssH*dpr));
      await clickSprite(page,{x:16,y:16,z:0},'pier'); await expect(page.getByTestId('selected-object')).toContainText('building-1');
      results.push(dimensions);
    } finally { await context.close(); }
  }
  await writeFile(`${evidence}/dpr.json`,JSON.stringify(results,null,2));
});

test('UX3 coarse pointer targets, enlarged text and reduced visible height retain fields/actions', async ({ browser }) => {
  const context = await browser.newContext({hasTouch:true,isMobile:true,viewport:{width:360,height:800},deviceScaleFactor:2});
  try {
    const page = await context.newPage(); await page.goto('/'); await ready(page);
    const targets = await page.locator('.command-bar button:visible,.tool-strip button:visible,.map-navigation button:visible').evaluateAll(els => els.map(el=>{const r=el.getBoundingClientRect();return {label:el.textContent,w:r.width,h:r.height};}));
    for(const target of targets) { expect(target.w).toBeGreaterThanOrEqual(44); expect(target.h).toBeGreaterThanOrEqual(44); }
    await openPanel(page,'settings');
    await page.addStyleTag({content:'button,select,input,p,h2,label { font-size: 200% !important; }'});
    await page.setViewportSize({width:360,height:450}); await fit(page);
    for (const button of await page.locator('.command-bar button:visible').all()) await expect(button).toBeInViewport({ratio:1});
    await page.getByLabel('UI color scheme',{exact:true}).selectOption('plum');
    await page.getByRole('button',{name:'Close panel',exact:true}).click();
    await openPanel(page,'session'); await page.getByRole('button',{name:'Leave game',exact:true}).click();
    await expect(page.getByRole('dialog',{name:'Welcome to FreeTrain Online',exact:true})).toBeVisible();
    const gateScheme = page.getByLabel('UI color scheme',{exact:true}); await gateScheme.focus();
    await page.keyboard.press('c'); await page.keyboard.press('Enter'); await expect(gateScheme).toHaveValue('classic-blue');
    const key = page.getByLabel('Operator access key',{exact:true}); await key.fill('');
    await page.setViewportSize({width:360,height:320}); await fit(page);
    await key.scrollIntoViewIfNeeded(); await expect(key).toBeInViewport();
    const connect = page.getByRole('button',{name:'Connect',exact:true}); await connect.scrollIntoViewIfNeeded(); await expect(connect).toBeInViewport();
    await page.screenshot({path:`${evidence}/enlarged-keyboard-height.png`});
    await page.keyboard.press('Escape'); await expect(page.getByRole('dialog')).toBeVisible();
    await writeFile(`${evidence}/coarse-targets.json`,JSON.stringify(targets,null,2));
  } finally { await context.close(); }
});

function luminance(rgb: string) {const values=rgb.match(/[\d.]+/g)!.slice(0,3).map(Number).map(x=>{x/=255;return x<=.04045?x/12.92:((x+.055)/1.055)**2.4;});return .2126*values[0]+.7152*values[1]+.0722*values[2];}
function contrast(a: string,b: string) {const x=luminance(a),y=luminance(b);return (Math.max(x,y)+.05)/(Math.min(x,y)+.05);}
test('UX3 every preset has readable text/states in English and narrow Japanese without canvas lifecycle work', async ({ page }) => {
  await page.addInitScript(() => {const original=HTMLImageElement.prototype.decode; (window as any).decodes=0; HTMLImageElement.prototype.decode=function(){(window as any).decodes++;return original.call(this);};});
  await page.goto('/'); await ready(page); const canvas=page.locator('canvas'); await canvas.evaluate(el=>el.setAttribute('data-instance','preserved'));
  await page.getByRole('button',{name:'Build track',exact:true}).click(); await openPanel(page,'settings');
  const before=await bootstrap(page),camera=await canvas.getAttribute('data-camera'),zoom=await canvas.getAttribute('data-zoom'),decodes=await page.evaluate(()=>(window as any).decodes);
  const observations:unknown[]=[];
  for(const locale of ['en','ja']) {
    await page.getByLabel('Language',{exact:true}).selectOption(locale);
    await page.setViewportSize(locale==='en'?{width:1280,height:720}:{width:360,height:800}); await settled(page);
    for(const scheme of ['evergreen','classic-blue','slate','plum']) {
      const select=page.getByLabel(locale==='en'?'UI color scheme':'UI配色',{exact:true}); await select.focus();
      await select.selectOption(scheme); await expect(select).toBeFocused();
      const colors=await page.evaluate(()=>{
        const root=getComputedStyle(document.documentElement), color=(v:string)=>root.getPropertyValue(v).trim();
        const rgb=(value:string)=>{const probe=document.createElement('span');probe.style.color=value;document.body.append(probe);const result=getComputedStyle(probe).color;probe.remove();return result;};
        return { ink:rgb(color('--ink')),muted:rgb(color('--muted')),surface:rgb(color('--ui-window-background')),primary:rgb(color('--ui-button-primary')),alternative:rgb(color('--ui-button-alternative')),inactive:rgb(color('--ui-button-inactive')),focus:rgb(color('--focus')),notice:rgb(color('--ui-notice-background')) };
      });
      const ratios={normal:contrast(colors.ink,colors.surface),primary:contrast('rgb(255,255,255)',colors.primary),disabled:contrast(colors.muted,colors.inactive),notice:contrast(colors.ink,colors.notice),focus:contrast(colors.focus,colors.surface)};
      for(const [key,value] of Object.entries(ratios)) expect(value,`${scheme} ${key}`).toBeGreaterThanOrEqual(key==='focus'?3:4.5);
      await expect(canvas).toHaveAttribute('data-instance','preserved'); await expect(canvas).toHaveAttribute('data-camera',camera!); await expect(canvas).toHaveAttribute('data-zoom',zoom!);
      expect(await page.evaluate(()=>(window as any).decodes)).toBe(decodes);
      const radii=await page.locator('button,.primary-panel,input,select').evaluateAll(els=>els.map(el=>getComputedStyle(el).borderRadius)); expect(radii.every(value=>value==='0px')).toBe(true);
      await fit(page); await page.screenshot({path:`${evidence}/preset-${scheme}-${locale}.png`}); observations.push({scheme,locale,colors,ratios,decodes});
    }
    if(locale==='en') await page.getByLabel('Language',{exact:true}).selectOption('en');
  }
  expect(await bootstrap(page)).toEqual(before);
  await writeFile(`${evidence}/presets.json`,JSON.stringify(observations,null,2));
});

test('UX3 manual keys reset on blur, visibility, tool and lease loss without automatic resume', async ({ page }) => {
  await page.goto('/'); await ready(page); await selectTrain(page);
  await page.getByRole('combobox',{name:'Controller',exact:true}).selectOption('manual'); await expect(page.getByRole('status')).toHaveText('Confirmed by server');
  const canvas=page.locator('canvas');
  for(const reason of ['blur','hidden','tool','panel']) {
    await canvas.focus(); await page.keyboard.down('ArrowRight'); await expect(page.getByTestId('manual-input')).toContainText('ArrowRight');
    if(reason==='blur') await page.evaluate(()=>window.dispatchEvent(new Event('blur')));
    if(reason==='hidden') await page.evaluate(()=>document.dispatchEvent(new Event('visibilitychange')));
    if(reason==='tool') await page.getByRole('button',{name:'Build track',exact:true}).click();
    if(reason==='panel') await openPanel(page,'settings');
    await expect(page.getByTestId('manual-input')).toHaveText('Keys released'); await page.keyboard.up('ArrowRight');
    if(reason==='tool') await page.getByRole('button',{name:'Inspect / drive',exact:true}).click();
    if(reason==='panel') await closePanel(page);
    await canvas.focus(); await page.keyboard.press('Control+ArrowRight'); await expect(page.getByTestId('manual-input')).toHaveText('Keys released');
  }
  await selectTrain(page); await page.getByRole('button',{name:'Release control',exact:true}).click();
  await canvas.focus(); await page.keyboard.press('ArrowRight'); await expect(page.getByTestId('manual-input')).toHaveText('Keys released');
  await page.getByRole('combobox',{name:'Controller',exact:true}).selectOption('automatic'); await expect(page.getByRole('status')).toHaveText('Confirmed by server');
  await page.getByRole('button',{name:'Release control',exact:true}).click();
});

test('UX3 independent UI state and observer explanations retain shared clock and authority', async ({ browser }) => {
  const aContext=await browser.newContext(),bContext=await browser.newContext();
  try {
    const a=await aContext.newPage(),b=await bContext.newPage(); await a.goto('/'); await ready(a); await b.goto('/'); await ready(b);
    await a.getByRole('button',{name:'Run',exact:false}).click(); await expect(b.getByTestId('clock-state')).toHaveText('Running');
    const bCamera=await b.locator('canvas').getAttribute('data-camera'); await a.getByRole('button',{name:'Zoom in',exact:true}).click(); await openPanel(a,'settings');
    await expect(b.locator('canvas')).toHaveAttribute('data-camera',bCamera!); await expect(b.locator('.primary-panel')).toHaveCount(0);
    const tick=await b.getByTestId('tick').textContent(); await expect(b.getByTestId('tick')).not.toHaveText(tick!);
    await openPanel(a,'session'); await a.getByRole('button',{name:'Leave game',exact:true}).click();
    await a.getByLabel('Operator access key',{exact:true}).fill('observer-test'); await a.getByRole('button',{name:'Connect',exact:true}).click(); await ready(a);
    await expect(a.getByTestId('participant-role')).toHaveText('Observer');
    await expect(a.getByRole('button',{name:'Build track',exact:true})).toBeDisabled();
    await a.getByRole('button',{name:'Inspect / drive',exact:true}).focus();
    await expect(a.locator('#mutation-help')).toContainText('Observers can inspect');
    await openPanel(a,'saves'); await expect(a.locator('#saves-help')).toContainText('Owner'); await expect(a.getByRole('button',{name:'Save slot',exact:true})).toBeDisabled();
    await openPanel(a,'view'); await a.getByLabel('Night',{exact:true}).check();
    await expect(b.getByTestId('clock-state')).toHaveText('Running');
    await b.getByRole('button',{name:'Pause',exact:false}).click();
  } finally {await aContext.close();await bContext.close();}
});

test('UX3 live DPR and pinch viewport changes retain camera and reachable panels', async ({ page }) => {
  await page.setViewportSize({width:1280,height:720}); await page.goto('/'); await ready(page);
  const canvas=page.locator('canvas'),camera=await canvas.getAttribute('data-camera'),zoom=await canvas.getAttribute('data-zoom');
  const cdp=await page.context().newCDPSession(page);
  await cdp.send('Emulation.setDeviceMetricsOverride',{width:1280,height:720,deviceScaleFactor:2,mobile:false});
  await expect.poll(()=>canvas.evaluate(el=>(el as HTMLCanvasElement).width)).toBe(2480);
  await expect(canvas).toHaveAttribute('data-camera',camera!); await expect(canvas).toHaveAttribute('data-zoom',zoom!);
  await openPanel(page,'settings');
  await cdp.send('Emulation.setPageScaleFactor',{pageScaleFactor:1.5}); await settled(page);
  const dimensions=await page.evaluate(()=>({scale:visualViewport!.scale,visibleHeight:visualViewport!.height,shell:document.querySelector('.app-shell')!.getBoundingClientRect().toJSON()}));
  expect(dimensions.scale).toBeCloseTo(1.5); expect(dimensions.shell.height).toBeCloseTo(720,0);
  await cdp.send('Emulation.setPageScaleFactor',{pageScaleFactor:1}); await fit(page);
  await page.addStyleTag({content:'.app-shell { padding: 20px 12px 24px 16px; }'}); await settled(page);
  await page.getByRole('button',{name:'Close panel',exact:true}).click();
  await clickSprite(page,{x:16,y:16,z:0},'pier'); await expect(page.getByTestId('selected-object')).toContainText('building-1');
  await writeFile(`${evidence}/viewport-emulation.json`,JSON.stringify({dimensions,note:'CDP DPR/pinch and CSS safe-inset simulation; not native desktop browser zoom or physical-device evidence.'},null,2));
});

test('UX3 success expires, actionable rejection remains and tool Escape cancels before exit', async ({ page }) => {
  await page.goto('/'); await ready(page);
  if(await page.getByTestId('clock-state').textContent()==='Running') await page.getByRole('button',{name:'Pause',exact:false}).click();
  await page.getByRole('button',{name:'Single step',exact:false}).click(); await expect(page.getByRole('status')).toHaveText('Confirmed by server');
  await expect(page.locator('.command-feedback')).toHaveText('',{timeout:6000});
  await page.getByRole('button',{name:'Build track',exact:true}).click();
  await clickTile(page,{x:23,y:16,z:1});
  await page.getByRole('button',{name:'Confirm track change',exact:true}).click(); await expect(page.getByRole('status')).toHaveText('Track cannot be placed here.');
  await page.waitForTimeout(4200); await expect(page.getByRole('status')).toHaveText('Track cannot be placed here.');
  await expect(page.locator('.tool-card')).toContainText('Track cannot be placed here.');
  await page.locator('canvas').focus(); await page.keyboard.press('Escape'); await expect(page.getByRole('button',{name:'Build track',exact:true})).toHaveAttribute('aria-pressed','true');
  await page.keyboard.press('Escape'); await expect(page.getByRole('button',{name:'Inspect / drive',exact:true})).toHaveAttribute('aria-pressed','true');
});
