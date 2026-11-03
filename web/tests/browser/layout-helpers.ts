import { expect, type Page } from './fixtures.ts';

export async function fit(page: Page) {
  const bounds = await page.evaluate(() => {
    window.scrollTo(100, 100);
    return { width: innerWidth, height: innerHeight, scrollWidth: document.documentElement.scrollWidth,
      scrollHeight: document.documentElement.scrollHeight, x: scrollX, y: scrollY };
  });
  expect(bounds.scrollWidth).toBeLessThanOrEqual(bounds.width); expect(bounds.scrollHeight).toBeLessThanOrEqual(bounds.height);
  expect(bounds.x).toBe(0); expect(bounds.y).toBe(0); return bounds;
}
export async function settled(page: Page) { await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))); }

