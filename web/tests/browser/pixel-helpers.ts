import { expect, type Page } from '@playwright/test';
import type { RenderCatalog } from '../../src/lib/content-recipes.ts';

type Component = 'rail' | 'signal' | 'accessory' | 'stationary';

/** Compare opaque source pixels with a stable displayed frame. Read each canvas
 * once, retaining the original crop, anchor, bounds, DPR and zoom calculations. */
export async function sourcePixelCounts(page: Page, catalog: RenderCatalog, objects: any[], component: Component, mapHeight = 42) {
  const canvas = page.locator('canvas.world-canvas');
  await page.evaluate(() => new Promise<void>(resolve => requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
  await expect(canvas).toHaveAttribute('data-presentation', 'ready');
  return canvas.evaluate(async (element, { catalog, objects, component, mapHeight }) => {
    const canvas = element as HTMLCanvasElement, bounds = canvas.getBoundingClientRect();
    const [cx, cy] = canvas.dataset.camera!.split(',').map(Number);
    const center = Math.trunc((mapHeight - 1) / 2);
    const zoom = Number(canvas.dataset.zoom), variant = canvas.dataset.terrainVariant!;
    const actual = canvas.getContext('2d')!.getImageData(0, 0, canvas.width, canvas.height).data;
    const host = window as unknown as { sourcePixelSheets?: Map<string, Promise<ImageData>> };
    const sheets = host.sourcePixelSheets ??= new Map();
    const sheet = (url: string) => {
      let pending = sheets.get(url);
      if (!pending) {
        pending = (async () => {
          const image = new Image(); image.src = url; await image.decode();
          const canvas = document.createElement('canvas');
          canvas.width = image.naturalWidth; canvas.height = image.naturalHeight;
          const context = canvas.getContext('2d')!; context.drawImage(image, 0, 0);
          return context.getImageData(0, 0, canvas.width, canvas.height);
        })().catch(error => { sheets.delete(url); throw error; });
        sheets.set(url, pending);
      }
      return pending;
    };
    const counts: number[] = [];
    for (const object of objects) {
      const rail = component === 'accessory' ? object.traffic?.accessory : object.traffic?.rail;
      const recipe = catalog.renderRecipes.find(r => r.id === (rail?.definition.recipeId ?? object.definition.recipeId))!;
      const frames = component === 'stationary' ? recipe.frames.filter(f => f.status === 'ready')
        : !rail ? recipe.frames : recipe.frames.filter(f => component === 'signal'
          ? f.selector?.direction === rail.direction : component === 'accessory'
          ? f.selector?.axis === rail.direction : f.selector?.index === rail.variant);
      let count = 0;
      for (const frame of frames) {
        const source = await sheet(catalog.surfaceSets[frame.surfaceSet!][variant].image.url);
        const [sx, sy, width, height] = frame.rectangle!, [ax, ay] = frame.anchor!;
        const [dx, dy, dz] = component === 'stationary' ? frame.cell! : [0, 0, 0];
        const origin = component === 'stationary' ? object.anchor : object.position;
        const p = { x: origin.x + dx, y: origin.y + dy, z: origin.z + dz };
        const x = bounds.width / 2 + (16 * (p.x + p.y - center) - ax - cx) * zoom;
        const y = bounds.height / 2 + (8 * (-p.x + p.y + center) - 16 * p.z - ay - cy) * zoom;
        for (let b = 0; b < height; b++) for (let a = 0; a < width; a++) {
          const s = ((sy + b) * source.width + sx + a) * 4;
          if (source.data[s + 3] !== 255) continue;
          const px = Math.floor((x + (a + .5) * zoom) * canvas.width / bounds.width);
          const py = Math.floor((y + (b + .5) * zoom) * canvas.height / bounds.height);
          if (px < 0 || py < 0 || px >= canvas.width || py >= canvas.height) continue;
          const d = (py * canvas.width + px) * 4;
          if (actual[d] === source.data[s] && actual[d + 1] === source.data[s + 1] && actual[d + 2] === source.data[s + 2]
            && (component !== 'stationary' || actual[d + 3] === 255)) count++;
        }
      }
      counts.push(count);
    }
    return counts;
  }, { catalog, objects, component, mapHeight });
}
