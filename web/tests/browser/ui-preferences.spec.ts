import { writeFile } from 'node:fs/promises';
import { expect,test } from './fixtures.ts';
import { bootstrap,evidenceDirectory,openPanel,ready,selectStation } from './browser-helpers.ts';
import { fit,settled } from './layout-helpers.ts';

function luminance(rgb: string) {const values=rgb.match(/[\d.]+/g)!.slice(0,3).map(Number).map(x=>{x/=255;return x<=.04045?x/12.92:((x+.055)/1.055)**2.4;});return .2126*values[0]+.7152*values[1]+.0722*values[2];}
function contrast(a: string,b: string) {const x=luminance(a),y=luminance(b);return (Math.max(x,y)+.05)/(Math.min(x,y)+.05);}

test('schemes persist across refresh and gates without changing camera, canvas, draft or world', async ({ page }) => {
  await page.goto('/'); await ready(page);
  if (await page.getByTestId('clock-state').textContent() === 'Running') await page.getByRole('button', { name: 'Pause', exact: false }).click();
  const errors: string[] = []; page.on('pageerror', error => errors.push(error.message));
  await page.locator('canvas').evaluate(el => el.setAttribute('data-instance', 'same'));
  const oldZoom = await page.locator('canvas').getAttribute('data-zoom');
  await page.getByRole('button', { name: 'Zoom in', exact: true }).click();
  await expect(page.locator('canvas')).not.toHaveAttribute('data-zoom', oldZoom!);
  const canvas = page.locator('canvas'), camera = await canvas.getAttribute('data-camera'), zoom = await canvas.getAttribute('data-zoom');
  const before = await bootstrap(page);
  await page.getByRole('button', { name: 'Build track', exact: true }).click();
  await openPanel(page, 'settings');
  for (const scheme of ['classic-blue', 'slate', 'plum', 'evergreen']) {
    await page.getByLabel('UI color scheme', { exact: true }).selectOption(scheme);
    await expect(page.locator('html')).toHaveAttribute('data-ui-scheme', scheme);
    await expect(canvas).toHaveAttribute('data-instance', 'same'); await expect(canvas).toHaveAttribute('data-camera', camera!); await expect(canvas).toHaveAttribute('data-zoom', zoom!);
    await expect(page.getByRole('button', { name: 'Build track', exact: true })).toHaveAttribute('aria-pressed', 'true');
  }
  await page.getByLabel('UI color scheme', { exact: true }).selectOption('plum');
  expect(await page.evaluate(() => JSON.parse(localStorage.getItem('freetrain-online.ui.v1')!))).toEqual({ version: 1, scheme: 'plum' });
  expect(await bootstrap(page)).toEqual(before);
  await page.reload(); await ready(page); await expect(page.locator('html')).toHaveAttribute('data-ui-scheme', 'plum');
  await selectStation(page); await page.getByLabel('Station name', { exact: true }).fill('Preserve my draft');
  await page.getByRole('button', { name: 'Reset draft', exact: true }).click();
  await openPanel(page, 'session'); await page.getByRole('button', { name: 'Leave game', exact: true }).click();
  await expect(page.getByLabel('Enter access key to join game', { exact: true })).toBeVisible(); await expect(page.getByLabel('UI color scheme', { exact: true })).toHaveValue('plum');
  await page.getByLabel('UI color scheme', { exact: true }).selectOption('classic-blue');
  await page.getByLabel('Language', { exact: true }).selectOption('ja'); await page.setViewportSize({ width: 360, height: 800 });
  await expect(page.getByLabel('アクセスキーを入力してゲームに参加', { exact: true })).toBeVisible();

  await page.reload(); await expect(page.getByLabel('UI配色', { exact: true })).toHaveValue('classic-blue'); expect(errors).toEqual([]);
});

test('invalid or obsolete stored scheme falls back before gameplay', async ({ page }) => {
  await page.goto('/'); await ready(page);
  if (await page.getByTestId('clock-state').textContent() === 'Running') await page.getByRole('button', { name: 'Pause', exact: false }).click();
  for (const stored of ['not JSON', '{"version":1,"scheme":"unknown"}', '{"version":2,"scheme":"plum"}']) {
    await page.evaluate(value => localStorage.setItem('freetrain-online.ui.v1', value), stored); await page.reload(); await ready(page);
    await expect(page.locator('html')).toHaveAttribute('data-ui-scheme', 'evergreen');
  }
});

test('every preset has readable text/states in English and narrow Japanese without canvas lifecycle work', async ({ page }) => {
  const evidence = await evidenceDirectory('ui-preferences');
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
      await fit(page);  observations.push({scheme,locale,colors,ratios,decodes});
    }
    if(locale==='en') await page.getByLabel('Language',{exact:true}).selectOption('en');
  }
  expect(await bootstrap(page)).toEqual(before);
  await writeFile(`${evidence}/presets.json`,JSON.stringify(observations,null,2));
});
