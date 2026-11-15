import { expect, type Page, type Response, type WebSocketRoute } from '@playwright/test';
import type { Join, Snapshot, Delta, Command, WireMessage, Accepted, Rejected } from '../../src/generated/contracts.ts';

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

/** Both decoders must publish the replacement before another UI action. */
export async function replaceSave(page: Page, data: Buffer | object, epoch: string, peers: readonly Page[] = [page]) {
  const response = await page.request.post(`/api/saves/upload?expectedWorldEpoch=${epoch}`, { data });
  expect(response.ok(), await response.text()).toBe(true);
  for (const peer of peers) {
    await expect(peer.locator('canvas.world-canvas')).toHaveAttribute('data-world-epoch', String(BigInt(epoch) + 1n));
    await expect(peer.locator('canvas.world-canvas')).toHaveAttribute('data-presentation', 'ready');
  }
}
export async function restartCheckpoint(server: { restart?: () => Promise<void> }, peers: readonly { page: Page; secret?: string; language?: string }[]) {
  if (!server.restart) throw new Error('Checkpoint restart requires a managed world server');
  await server.restart();
  for (const { page, secret, language = 'en' } of peers) {
    await authenticate(page, secret);
    await page.reload();
    await expect(page.getByTestId('connection')).toHaveText(language === 'ja' ? 'ゲームに接続済み' : 'Connected to game');
    await expect(page.locator('canvas.world-canvas')).toHaveAttribute('data-presentation', 'ready');
  }
}

export function previewResponse(page: Page, path: string): Promise<Response> {
  return page.waitForResponse(response => new URL(response.url()).pathname === path && response.request().method() === 'POST');
}

/** Intercept once per page, preserving the real server and client decoders. */
export async function collectSession(page: Page) {
  const messages: WireMessage[] = [];
  let socket: WebSocketRoute | undefined;
  await page.routeWebSocket('**/api/session?*', route => {
    const server = route.connectToServer();
    socket = server;
    server.onMessage(message => { messages.push(JSON.parse(String(message))); route.send(message); });
    route.onMessage(message => server.send(message));
  });
  return {
    messages,
    latest: () => { const m = messages.filter((m): m is Join | Snapshot | Delta => ['join', 'delta', 'snapshot'].includes(m.type)).at(-1); return m?.type === 'join' ? m.snapshot : m; },
    async command(command: Command) {
      if (!socket) throw new Error('Session is not connected');
      const start = messages.length;
      socket.send(JSON.stringify(command));
      const receipt = () => messages.slice(start).find((m): m is Accepted | Rejected => (m.type === 'accepted' || m.type === 'rejected') && m.requestId === command.requestId);
      await expect.poll(receipt).toBeTruthy();
      return receipt()!;
    },
  };
}

export function delayedResponse() {
  let release!: () => void;
  const held = new Promise<void>(resolve => { release = resolve; });
  return { held, release };
}
