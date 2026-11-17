import { readFileSync } from 'node:fs';
import { test, expect } from '../browser/fixtures.ts';
import { closePanel, exposeMapPoint, openPanel, ready } from '../browser/browser-helpers.ts';
import { waitForMapDraw, panMapBy, projectMapPoint, renderedCamera, renderedTool } from '../browser/map-picking.ts';

type FrameWindow = Window & { holdMapFrames?: boolean; releaseMapFrames?: () => void; pickWheelObserved?: boolean };
test.use({ viewport: { width: 430, height: 860 }, hasTouch: true });

test('map picking waits for completed tool and pan draws, then exposes the same world point', async ({ page, sessions }) => {
  test.setTimeout(60000);
  // Delay frame delivery without a busy loop, CPU throttling or resource stress.
  await page.addInitScript(() => {
    const host = window as FrameWindow, native = requestAnimationFrame.bind(window), held: FrameRequestCallback[] = [];
    window.requestAnimationFrame = callback => native(time => {
      if (host.holdMapFrames) held.push(callback); else callback(time);
    });
    host.releaseMapFrames = () => { host.holdMapFrames = false; for (const callback of held.splice(0)) native(callback); };
  });
  const session = await sessions.observe(page);
  await page.goto('/'); await ready(page);
  await sessions.replaceSave(session, readFileSync('tests/fixtures/station-controller-v11.json'), 'pick harness scene');
  await page.getByRole('button', { name: 'Stations', exact: true }).click();
  await page.getByRole('button', { name: 'Close building catalog', exact: true }).click();
  await renderedTool(page, 'station');
  const beforeTool = await renderedCamera(page);
  await page.evaluate(() => {
    (window as FrameWindow).holdMapFrames = true;
    // The full controller journey covers actual mouse/touch input. Here a DOM
    // click schedules the tool change while its rendering is deliberately held.
    document.querySelector<HTMLButtonElement>('.tool-strip button[aria-label="Inspect"]')!.click();
  });
  const draw = waitForMapDraw(page); draw.catch(() => {});
  const tool = renderedTool(page, 'inspect'); tool.catch(() => {});
  try {
    // Intentional absence window: a pending tool draw must not satisfy readiness.
    expect(await Promise.race([tool.then(() => 'early'), new Promise(resolve => setTimeout(() => resolve('waiting'), 100))])).toBe('waiting');
    expect(await Promise.race([draw.then(() => 'early'), new Promise(resolve => setTimeout(() => resolve('waiting'), 100))])).toBe('waiting');
    expect(await renderedCamera(page)).toEqual(beforeTool);
  } finally { await page.evaluate(() => (window as FrameWindow).releaseMapFrames!()); }
  await tool; await draw;

  const beforePan = await renderedCamera(page);
  await page.evaluate(() => {
    (window as FrameWindow).holdMapFrames = true;
    document.querySelector('canvas.world-canvas')!.addEventListener('wheel', () => { (window as FrameWindow).pickWheelObserved = true; }, { once: true });
  });
  const pan = panMapBy(page, { x: 60.3152, y: -9.614 }); pan.catch(() => {});
  await page.waitForFunction(() => (window as FrameWindow).pickWheelObserved, undefined, { polling: 25 });
  try {
    // Intentional absence window: wheel dispatch alone is not a completed draw.
    expect(await Promise.race([pan.then(() => 'early'), new Promise(resolve => setTimeout(() => resolve('waiting'), 100))])).toBe('waiting');
    expect(await renderedCamera(page)).toEqual(beforePan);
  } finally { await page.evaluate(() => (window as FrameWindow).releaseMapFrames!()); }
  const afterPan = await pan;
  expect(await renderedCamera(page)).toEqual(afterPan);
  await expect(projectMapPoint(page, { x: 207.5, y: 112.5 }, beforePan)).rejects.toThrow('Map camera changed before pick');

  await expect(page.locator('canvas.world-canvas')).toHaveAttribute('data-rendered-reduced-motion', 'false');
  await page.evaluate(() => { (window as FrameWindow).holdMapFrames = true; });
  await page.emulateMedia({ reducedMotion: 'reduce' });
  const preferenceDraw = waitForMapDraw(page); preferenceDraw.catch(() => {});
  try {
    // Intentional absence window: an earlier ready draw cannot satisfy a new media preference.
    expect(await Promise.race([preferenceDraw.then(() => 'early'), new Promise(resolve => setTimeout(() => resolve('waiting'), 100))])).toBe('waiting');
    await expect(page.locator('canvas.world-canvas')).toHaveAttribute('data-rendered-reduced-motion', 'false');
  } finally { await page.evaluate(() => (window as FrameWindow).releaseMapFrames!()); }
  await preferenceDraw;
  await expect(page.locator('canvas.world-canvas')).toHaveAttribute('data-rendered-reduced-motion', 'true');
  await waitForMapDraw(page);

  await closePanel(page); await openPanel(page, 'settings');
  const canvas = page.locator('canvas.world-canvas'), sheet = await page.locator('.primary-panel').boundingBox();
  expect(sheet).not.toBeNull();
  const covered = { x: sheet!.x + sheet!.width / 2, y: sheet!.y + sheet!.height / 2 };
  const camera = await renderedCamera(page);
  const world = await canvas.evaluate((el, { covered, camera }) => {
    const r = el.getBoundingClientRect();
    return { x: camera.x + (covered.x - r.x - r.width / 2) / camera.zoom, y: camera.y + (covered.y - r.y - r.height / 2) / camera.zoom };
  }, { covered, camera });
  const exposed = await exposeMapPoint(page, covered);
  expect(await canvas.evaluate((el, p) => document.elementFromPoint(p.x, p.y) === el, exposed)).toBe(true);
  expect((await projectMapPoint(page, world)).point).toEqual(exposed);
});
