import { expect, type Page, type Response } from '@playwright/test';
import type { Join } from '../../src/generated/contracts.ts';

export const protocolVersion: Join['protocolVersion'] = 11;
export function compatibilityURL(path: string, contentVersions: readonly string[]) {
  const query = new URLSearchParams({ protocolVersion: String(protocolVersion) });
  for (const version of contentVersions) query.append('contentVersion', version);
  return `${path}?${query}`;
}
export async function authenticate(page: Page, secret = '') {
  const response = await page.request.post('/api/access', { data: { secret } });
  expect(response.ok(), await response.text()).toBe(true);
}

/** A small paused scene; worker reset still owns credentials and receipt isolation. */
export async function pausedCanonicalScene(page: Page, current: { worldEpoch: string; revision: string },
  scene: { name: string; dimensions: { h: number; v: number; z: number }; waterLevel: number }) {
  const response = await page.request.post('/api/worlds/empty/preview', {
    data: { ...scene, epoch: current.worldEpoch, revision: current.revision },
  });
  expect(response.ok(), await response.text()).toBe(true);
  const candidate = await response.json();
  const committed = await page.request.post('/api/worlds/empty/commit', { data: {
    id: candidate.id, requestHash: candidate.requestHash, candidateHash: candidate.candidateHash,
    epoch: current.worldEpoch, revision: current.revision,
  } });
  expect(committed.ok(), await committed.text()).toBe(true);
  await expect(page.locator('canvas.world-canvas')).toHaveAttribute('data-world-epoch', String(BigInt(current.worldEpoch) + 1n));
  await expect(page.locator('canvas.world-canvas')).toHaveAttribute('data-presentation', 'ready');
  await expect(page.getByTestId('clock-state')).toHaveText('Paused');
}

export function previewResponse(page: Page, path: string): Promise<Response> {
  return page.waitForResponse(response => new URL(response.url()).pathname === path && response.request().method() === 'POST');
}

export function delayedResponse() {
  let release!: () => void;
  const held = new Promise<void>(resolve => { release = resolve; });
  return { held, release };
}
