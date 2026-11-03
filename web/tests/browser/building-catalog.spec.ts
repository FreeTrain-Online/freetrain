import { expect,extended,test,type Page } from './fixtures.ts';
import { canonicalState,closePanel,openPanel,ready } from './browser-helpers.ts';

test.use({ actionTimeout: 10_000 });
const map = (page: Page) => page.locator('canvas.world-canvas');
async function save(page: Page) { return (await page.request.get('/api/saves/download')).body(); }
async function empty(page: Page) {
  const state = await canonicalState(page);
  const response = await page.request.post('/api/worlds/empty/preview', { data: {
    name: 'Catalog workflow', dimensions: { h: 30, v: 42, z: 6 }, waterLevel: 1, epoch: state.worldEpoch, revision: state.revision,
  } });
  expect(response.ok()).toBe(true);
  const candidate = await response.json();
  expect((await page.request.post('/api/worlds/empty/commit', { data: {
    id: candidate.id, requestHash: candidate.requestHash, candidateHash: candidate.candidateHash, epoch: state.worldEpoch, revision: state.revision,
  } })).ok()).toBe(true);
  await expect(map(page)).toHaveAttribute('data-ruleset', 'terrain-state-1'); await ready(page);
}
async function restore(page: Page, bytes: Buffer) {
  const state = await canonicalState(page);
  expect((await page.request.post('/api/saves/upload?expectedWorldEpoch=' + state.worldEpoch, { data: bytes })).ok()).toBe(true);
}

for (const ja of [false, true]) test(`independent catalog, category browsing and synchronized construction ${ja ? 'JA' : 'EN'}`, async ({ page }) => {
  test.setTimeout(90_000);
  await page.goto('/'); await ready(page); const original = await save(page);
  try {
    await empty(page);
    if (ja) { await openPanel(page, 'settings'); await page.getByLabel('Language', { exact: true }).selectOption('ja'); await closePanel(page); }
    if(ja&&!extended())await page.setViewportSize({width:430,height:850});
    const lang = ja ? 'ja' : 'en', catalog = page.locator('#building-catalog');
    const category = catalog.getByLabel(ja ? 'カテゴリ' : 'Category', { exact: true });
    const search = catalog.getByLabel(ja ? '一覧を検索' : 'Search catalog', { exact: true });
    const inspect = page.getByRole('button', { name: ja ? '確認' : 'Inspect', exact: true });
    const camera = await map(page).getAttribute('data-camera'), before = await save(page);
    const canvasBounds = await map(page).boundingBox();
    await openPanel(page, 'catalog', lang); await expect(catalog).toBeVisible();
    await expect.poll(() => map(page).boundingBox()).toEqual(canvasBounds);
    await expect(catalog.locator('.catalog-item')).toHaveCount(2);
    await expect(catalog.locator('img')).toHaveCount(2);
    const signalCard = catalog.getByRole('button', { name: ja ? '三灯式' : 'Three-aspect signal', exact: true });
    await expect(signalCard.getByTestId('object-author')).toHaveText(`${ja ? '作者' : 'Author'}: 川口耕介`);
    await expect(signalCard.getByTestId('object-plugin')).toHaveText(`${ja ? 'プラグイン' : 'Plugin'}: 鉄道用信号機`);
    await expect(inspect).toHaveAttribute('aria-pressed', 'true');
    await category.selectOption('stationary'); await expect(catalog.locator('.catalog-item')).toHaveCount(9);
    await expect(catalog.locator('[data-catalog-variant]')).toHaveCount(54);
    await expect(catalog.locator('img')).toHaveCount(9);
    await expect(catalog.getByTestId('object-author')).toHaveCount(9);
    await expect(catalog.locator('.catalog-item').first().getByTestId('object-author')).toHaveText(`${ja ? '作者' : 'Author'}: いっちー`);
    await expect(catalog.locator('.catalog-item').first().getByTestId('object-plugin')).toHaveText(`${ja ? 'プラグイン' : 'Plugin'}: もっともっと橋脚`);
    await expect.poll(() => catalog.locator('img').evaluateAll(images => images.every(image => (image as HTMLImageElement).complete))).toBe(true);
    expect(await catalog.locator('img').evaluateAll(images => images.every(image => {
      const canvas = document.createElement('canvas'); canvas.width = 96; canvas.height = 80;
      const ctx = canvas.getContext('2d')!; ctx.drawImage(image as HTMLImageElement, 0, 0);
      return ctx.getImageData(0, 0, 96, 80).data.some((value, index) => index % 4 === 3 && value > 0);
    }))).toBe(true);

    await search.fill('no such item'); await expect(catalog.locator('.catalog-item')).toHaveCount(0);
    await category.selectOption('signal'); await expect(search).toHaveValue('');
    await category.selectOption('stationary'); await expect(search).toHaveValue('no such item'); await search.fill('');
    await expect(inspect).toHaveAttribute('aria-pressed', 'true');
    expect(await map(page).getAttribute('data-camera')).toBe(camera); expect(await save(page)).toEqual(before);
    await category.selectOption('signal');
    await catalog.getByRole('button', { name: ja ? '入替信号' : 'Shunting signal', exact: true }).click();
    await expect(page.getByRole('button', { name: ja ? '信号' : 'Signals', exact: true })).toHaveAttribute('aria-pressed', 'true');
    await expect(page.getByTestId('catalog-selection')).toContainText(ja ? '入替信号' : 'Shunting signal');
    const direction = catalog.getByRole('group', { name: `${ja ? '入替信号' : 'Shunting signal'} · ${ja ? '信号の向き' : 'Signal direction'}`, exact: true }); await direction.getByRole('button', { name: ja ? '南 (4)' : 'South (4)', exact: true }).click();
    for (const [label, value] of [[ja ? '始点 X' : 'Start X', '30'], [ja ? '始点 Y' : 'Start Y', '27'], [ja ? '線路の高さ' : 'Track level', '1']]) await page.getByLabel(label, { exact: true }).fill(value);
    await page.getByRole('button', { name: ja ? '経路をプレビュー' : 'Preview route', exact: true }).click();
    const confirm = page.getByRole('button', { name: ja ? '信号の変更を確定' : 'Confirm signal change', exact: true }); await expect(confirm).toBeEnabled();
    if(ja&&!extended()){
      await expect(confirm).toBeInViewport();await confirm.focus();await page.keyboard.press('Enter');await expect(page.getByTestId('rail-route')).toHaveCount(0);
      const built=await canonicalState(page);expect(built.mixed.voxels[0].traffic.rail.direction).toBe(4);expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);return;
    }
    await category.selectOption('stationary'); await expect(confirm).toBeEnabled();
    await category.selectOption('signal');
    await catalog.getByRole('button', { name: ja ? '三灯式' : 'Three-aspect signal', exact: true }).click();
    await expect(confirm).toBeDisabled(); await expect(page.getByTestId('signal-direction')).toContainText(ja ? '南 (4)' : 'South (4)');
    await expect(page.getByLabel(ja ? '始点 X' : 'Start X', { exact: true })).toHaveValue('30');
    await page.getByRole('button', { name: ja ? '経路をプレビュー' : 'Preview route', exact: true }).click(); await expect(confirm).toBeEnabled();
    const north = catalog.getByRole('group', { name: `${ja ? '三灯式' : 'Three-aspect signal'} · ${ja ? '信号の向き' : 'Signal direction'}`, exact: true }).getByRole('button', { name: ja ? '北 (0)' : 'North (0)', exact: true });
    await north.click(); await expect(confirm).toBeDisabled();
    await expect(page.getByLabel(ja ? '始点 X' : 'Start X', { exact: true })).toHaveValue('30');
    await expect(catalog.locator('.catalog-active .catalog-preview')).toHaveAttribute('data-direction', '0');
    await page.getByRole('button', { name: ja ? '経路をプレビュー' : 'Preview route', exact: true }).click(); await expect(confirm).toBeEnabled();

    await catalog.getByRole('button', { name: ja ? '建物一覧を閉じる' : 'Close building catalog', exact: true }).click();
    await expect(catalog).toHaveCount(0); await expect(confirm).toBeEnabled();
    await page.getByRole('button', { name: ja ? '一覧から選択' : 'Choose from catalog', exact: true }).click();
    await expect(category).toHaveValue('signal'); await expect(confirm).toBeEnabled();
    await catalog.getByRole('button', { name: ja ? '三灯式' : 'Three-aspect signal', exact: true }).focus(); await page.keyboard.press('Escape');
    await expect(catalog).toHaveCount(0); await expect(confirm).toBeEnabled();
    expect(await save(page)).toEqual(before);
    await page.getByRole('button', { name: ja ? '鉄道アクセサリ' : 'Railway accessories', exact: true }).click();
    await expect(category).toHaveValue('stationary'); await expect(catalog.locator('.catalog-item')).toHaveCount(9);
    const second = catalog.getByRole('button', { name: `${ja ? 'レンガ橋脚' : 'Brick pier'} · 2`, exact: true });
    await second.click(); await expect(second).toHaveAttribute('aria-pressed', 'true');
    await expect(page.getByTestId('catalog-selection')).toContainText(`${ja ? 'レンガ橋脚' : 'Brick pier'} · 2`);
    for (const [label, value] of [[ja ? '始点 X' : 'Start X', '30'], [ja ? '始点 Y' : 'Start Y', '27'], [ja ? '線路の高さ' : 'Track level', '1']]) await page.getByLabel(label, { exact: true }).fill(value);
    await page.getByRole('button', { name: ja ? '経路をプレビュー' : 'Preview route', exact: true }).click();
    const accessoryConfirm = page.getByRole('button', { name: ja ? 'アクセサリの操作を確定' : 'Confirm accessory action', exact: true }); await expect(accessoryConfirm).toBeEnabled();
    const fifth = catalog.getByRole('button', { name: `${ja ? 'レンガ橋脚' : 'Brick pier'} · 5`, exact: true }); await fifth.click();
    await expect(accessoryConfirm).toBeDisabled();
    await expect(page.getByLabel(ja ? '始点 X' : 'Start X', { exact: true })).toHaveValue('30');
    const family = catalog.locator('.catalog-active');
    await family.locator('.catalog-item-main').click(); await expect(fifth).toHaveAttribute('aria-pressed', 'true');
    await expect(family).toHaveAttribute('data-variant', '{{98619BD4-46C3-4B91-85FE-11147914F2A5}-E');

    expect(await save(page)).toEqual(before);
  } finally { await restore(page, original); }
});

test('catalog keyboard selection, narrow layout and observer browsing', async ({ page, browser }) => {
  test.setTimeout(90_000);
  await page.goto('/'); await ready(page); const original = await save(page), viewer = await browser.newPage();
  try {
    await empty(page); await page.setViewportSize({ width: 430, height: 850 });
    await map(page).focus(); await page.keyboard.press('f');
    const catalog = page.locator('#building-catalog'); await expect(catalog).toBeVisible();
    await catalog.getByRole('button', { name: 'Expand panel', exact: true }).click();
    const shunting = catalog.getByRole('button', { name: 'Shunting signal', exact: true });
    await shunting.focus(); await page.keyboard.press('Enter'); await expect(shunting).toHaveAttribute('aria-pressed', 'true');
    await catalog.getByRole('button', { name: 'Reduce panel', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Confirm signal change', exact: true })).toBeInViewport();

    expect(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth)).toBe(false);
    expect((await viewer.request.post('/api/access', { data: { secret: 'observer-test' } })).ok()).toBe(true);
    await viewer.goto('/'); await ready(viewer); await openPanel(viewer, 'catalog');
    await viewer.locator('#building-catalog').getByRole('button', { name: 'Shunting signal', exact: true }).click();
    await expect(viewer.getByRole('button', { name: 'Inspect', exact: true })).toHaveAttribute('aria-pressed', 'true');
    await expect(viewer.locator('.track-task')).toHaveCount(0);
    const viewerCatalog = viewer.locator('#building-catalog');
    await viewerCatalog.getByLabel('Category', { exact: true }).selectOption('stationary');
    const variants = viewerCatalog.locator('[data-catalog-variant]'); await expect(variants).toHaveCount(54);
    for (const button of await variants.all()) {
      const id = await button.getAttribute('data-catalog-variant'); await button.click();
      await expect(viewerCatalog.locator('.catalog-active .catalog-preview')).toHaveAttribute('data-preview-id', id!);
      await expect.poll(() => viewerCatalog.locator('.catalog-active img').count()).toBe(1);
      expect(await viewerCatalog.locator('.catalog-active img').evaluate(async image => {
        await (image as HTMLImageElement).decode();
        const canvas = document.createElement('canvas'); canvas.width = 96; canvas.height = 80;
        const ctx = canvas.getContext('2d')!; ctx.drawImage(image as HTMLImageElement, 0, 0);
        return ctx.getImageData(0, 0, 96, 80).data.some((value, index) => index % 4 === 3 && value > 0);
      })).toBe(true);
    }
    await expect(viewer.getByRole('button', { name: 'Inspect', exact: true })).toHaveAttribute('aria-pressed', 'true');
  } finally { await viewer.close(); await restore(page, original); }
});
