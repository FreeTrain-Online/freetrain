import { appName } from '../../src/lib/app-identity.ts';
import { writeFile } from 'node:fs/promises';
import { readFileSync } from 'node:fs';
import { expect,test } from './fixtures.ts';
import { at,bootstrap,clickSprite,closePanel,evidenceDirectory,openPanel,ready,selectStation } from './browser-helpers.ts';
import { fit,settled } from './layout-helpers.ts';

const sizes = [[1920,1080],[1280,720],[1024,768],[640,900],[360,800],[800,450]];

test('fixed viewport, persistent canvas and overlay-independent camera at target sizes', async ({ page }) => {
  const evidence = await evidenceDirectory('responsive-layout');
  const errors: string[] = []; page.on('pageerror', error => errors.push(error.message));
  await page.setViewportSize({ width: 1280, height: 720 });
  const sprites=JSON.parse(readFileSync('src/generated/sprites.json','utf8')).sprites;
  const mapPaths=Object.values(sprites).flatMap((s:any)=>[s.image,s.nightImage,s.tintImage]);
  await page.addInitScript((mapPaths: string[]) => {
    const testWindow = window as unknown as { mapDecodes: number }; testWindow.mapDecodes = 0;
    const decode = HTMLImageElement.prototype.decode;
    // Count this prototype map's sprites. Settings color previews have their own
    // short-lived blob-image cache and do not reload the map's decoded art.
    HTMLImageElement.prototype.decode = function () { if(mapPaths.includes(new URL(this.src,location.href).pathname))testWindow.mapDecodes++; return decode.call(this); };
  },mapPaths);
  await page.goto('/'); await ready(page);
  const canvas = page.locator('canvas'); await canvas.evaluate(el => { el.setAttribute('data-instance', 'original'); });
  const box = (await canvas.boundingBox())!;
  expect(box.width * box.height / (1280 * 720)).toBeCloseTo(.91493, 4);
  const before = await bootstrap(page);
  const decodes = await page.evaluate(() => (window as unknown as { mapDecodes: number }).mapDecodes);
  const observations: unknown[] = [{ viewport: { width: 1280, height: 720 }, canvas: box, canvasCoverage: box.width * box.height / (1280 * 720), decodes }];
  const center = await at(page, { x: 22, y: 16, z: 0 });
  await page.mouse.move(center.x, center.y); await page.mouse.down(); await page.mouse.move(center.x + 70, center.y + 25); await page.mouse.up();
  const camera = await canvas.getAttribute('data-camera'), zoom = await canvas.getAttribute('data-zoom');

  for (const id of ['settings', 'view', 'catalog', 'saves', 'help', 'session']) {
    await openPanel(page, id);
    await expect(canvas).toHaveAttribute('data-camera', camera!); await expect(canvas).toHaveAttribute('data-zoom', zoom!);
    expect(await canvas.boundingBox()).toEqual(box); await expect(canvas).toHaveAttribute('data-instance', 'original');
    expect(await page.evaluate(() => (window as unknown as { mapDecodes: number }).mapDecodes)).toBe(decodes);
    await closePanel(page);
  }
  expect(await bootstrap(page)).toEqual(before);
  for (const [width, height] of [[1920, 1080], [1280, 720], [1024, 768], [800, 900], [640, 900], [360, 800], [800, 450], [640, 450], [360, 450]]) {
    await page.setViewportSize({ width, height });
    await openPanel(page, 'settings'); await page.getByLabel('Language', { exact: true }).selectOption('ja');
    await openPanel(page, 'help', 'ja');
    const bounds = await page.evaluate(() => ({ w: document.documentElement.scrollWidth, h: document.documentElement.scrollHeight, x: scrollX, y: scrollY }));
    expect(bounds).toEqual({ w: width, h: height, x: 0, y: 0 });
    const panel = (await page.locator('.primary-panel').boundingBox())!;
    expect(panel.x).toBeGreaterThanOrEqual(0); expect(panel.y).toBeGreaterThanOrEqual(0);
    expect(panel.x + panel.width).toBeLessThanOrEqual(width); expect(panel.y + panel.height).toBeLessThanOrEqual(height);
    observations.push({ viewport: { width, height }, document: bounds, panel });

    await openPanel(page, 'settings', 'ja'); await page.getByLabel('言語', { exact: true }).selectOption('en'); await closePanel(page);
  }
  expect(errors).toEqual([]);
  await writeFile(`${evidence}/layout-observations.json`, JSON.stringify(observations, null, 2) + '\n');
});

test('responsive Japanese panels, scrolling, expansion and narrow Step stay usable', async ({ page }) => {
  const evidence = await evidenceDirectory('responsive-layout');
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
  await fit(page);
  await writeFile(`${evidence}/responsive.json`, JSON.stringify(measurements,null,2));
});

test('coarse pointer targets, enlarged text and reduced visible height retain fields/actions', async ({ browser }) => {
  const evidence = await evidenceDirectory('responsive-layout');
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
    await expect(page.getByRole('dialog',{name:appName,exact:true})).toBeVisible();
    const gateScheme = page.getByLabel('UI color scheme',{exact:true}); await gateScheme.focus();
    await page.keyboard.press('c'); await page.keyboard.press('Enter'); await expect(gateScheme).toHaveValue('classic-blue');
    const key = page.getByLabel('Enter access key to join game',{exact:true}); await key.fill('');
    await page.setViewportSize({width:360,height:320}); await fit(page);
    await key.scrollIntoViewIfNeeded(); await expect(key).toBeInViewport();
    const connect = page.getByRole('button',{name:'Connect',exact:true}); await connect.scrollIntoViewIfNeeded(); await expect(connect).toBeInViewport();

    await page.keyboard.press('Escape'); await expect(page.getByRole('dialog')).toBeVisible();
    await writeFile(`${evidence}/coarse-targets.json`,JSON.stringify(targets,null,2));
  } finally { await context.close(); }
});

test('live DPR and pinch viewport changes retain camera and reachable panels', async ({ page }) => {
  const evidence = await evidenceDirectory('responsive-layout');
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
