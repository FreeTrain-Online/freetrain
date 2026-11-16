import { expect, type Page } from '@playwright/test';
import type { Camera } from '../../src/lib/map-display.ts';

export type MapPoint = { x: number; y: number };

/** The full-precision camera belongs to a completed draw, including its hit map. */
export async function renderedCamera(page: Page): Promise<Camera> {
  const text = await page.locator('canvas.world-canvas').getAttribute('data-camera-state');
  if (!text) throw new Error('Map has no completed camera observation');
  const camera = JSON.parse(text) as Camera;
  if (![camera.x, camera.y, camera.zoom].every(Number.isFinite) || camera.zoom <= 0) throw new Error('Invalid rendered map camera');
  return camera;
}

export async function renderedTool(page: Page, tool: string): Promise<void> {
  const canvas = page.locator('canvas.world-canvas');
  const activation = await canvas.getAttribute('data-tool-activation');
  if (activation === null) throw new Error('Map has no tool activation observation');
  await expect(canvas).toHaveAttribute('data-rendered-tool-activation', activation);
  await expect(canvas).toHaveAttribute('data-rendered-tool', tool);
  await expect(canvas).toHaveAttribute('data-presentation', 'ready');
}

/** Ordinary wheel input, followed by the exact target draw. A prior tool redraw
 * cannot satisfy this wait. A tiny horizontal component makes vertical pans
 * unambiguous to the wheel/trackpad classifier; it is included in the target. */
export async function panMapBy(page: Page, delta: MapPoint): Promise<Camera> {
  const canvas = page.locator('canvas.world-canvas'), before = await renderedCamera(page);
  if (!delta.x && !delta.y) return before;
  const dx = delta.x || .5;
  const expected: Camera = { x: before.x + dx / before.zoom, y: before.y + delta.y / before.zoom, zoom: before.zoom };
  await canvas.dispatchEvent('wheel', { deltaX: dx, deltaY: delta.y, deltaMode: 0 });
  await expect.poll(() => renderedCamera(page), { message: `Map must render the requested pan ${JSON.stringify(expected)}` }).toEqual(expected);
  await expect(canvas).toHaveAttribute('data-presentation', 'ready');
  return expected;
}

/** Read camera and canvas bounds together. When an expected draw is supplied,
 * reject a changed camera before issuing input rather than tapping stale pixels. */
export async function projectMapPoint(page: Page, world: MapPoint, expected?: Camera): Promise<{ point: MapPoint; camera: Camera }> {
  return page.locator('canvas.world-canvas').evaluate((el, { world, expected }) => {
    const camera = JSON.parse(el.dataset.cameraState!) as Camera;
    if (expected && (camera.x !== expected.x || camera.y !== expected.y || camera.zoom !== expected.zoom)) throw new Error(`Map camera changed before pick: expected=${JSON.stringify(expected)} actual=${JSON.stringify(camera)}`);
    const r = el.getBoundingClientRect();
    const point = { x: r.x + r.width / 2 + (world.x - camera.x) * camera.zoom, y: r.y + r.height / 2 + (world.y - camera.y) * camera.zoom };
    return { point, camera };
  }, { world, expected });
}
