import { expect, type Page, type TestInfo, type WebSocketRoute } from '@playwright/test';
import type { Command, Join, SessionOffer, Snapshot, WireMessage } from '../../src/generated/contracts.ts';
import type { TestObservationWindow } from '../../src/lib/test-observation.ts';
import { assertCanonicalState } from './assertions.ts';
import type { Receipt, StateTarget } from './session-observation.ts';
import { canonicalJSON, createObservation, parseMessageValue, reached } from './session-runtime.ts';

const compatibility = 'protocolVersion=11&contentVersion=prototype-content-1&contentVersion=reference-spatial-content-2&contentVersion=terrain-content-1&contentVersion=mixed-railway-content-1';
export type BootstrapObservation = { kind: 'metadata'; offer: SessionOffer } | { kind: 'snapshot'; join: Join; snapshot: Snapshot };
export async function sessionBootstrap(page: Page): Promise<BootstrapObservation> {
  const response = await page.request.get(`/api/bootstrap?${compatibility}`);
  if (!response.ok()) throw new Error(`Bootstrap HTTP ${response.status()}`);
  const message = parseMessageValue(await response.json());
  if (message.type === 'join') return { kind: 'snapshot', join: message, snapshot: message.snapshot };
  if (message.type === 'sessionOffer') return { kind: 'metadata', offer: message };
  throw new Error(`Unexpected bootstrap ${message.type}`);
}

export class ObservedSession {
  readonly observation = createObservation();
  private socket?: WebSocketRoute;
  private raw: { message: WireMessage; bytes: number }[] = [];
  private rawBytes = 0;
  private sent: { serial: number; requestId: string }[] = [];
  private serial = 0;
  constructor(readonly page: Page, readonly peer: string, private info: TestInfo) {}
  /** Raw compact wire ring; never use it as a materialized snapshot. Full payloads
   * live in the managed diagnostics attachments. */
  get messages(): readonly WireMessage[] { return this.raw.map(event => event.message); }
  latest(): Snapshot | undefined { const state = this.observation.latest(); return state && canonicalJSON(state); }
  async install() {
    await this.page.addInitScript(() => { (window as Window & TestObservationWindow).__freetrainObserveApplied = true; });
    await this.page.routeWebSocket('**/api/session?*', route => {
      const generation = this.observation.beginConnection();
      this.sent = [];
      this.raw = []; this.rawBytes = 0;
      const server = this.socket = route.connectToServer();
      server.onMessage(payload => {
        const text = typeof payload === 'string' ? payload : payload.toString('utf8');
        this.observation.enqueue(text, generation);
        // Diagnostic retention is bounded in both count and total bytes.
        try {
          const message = JSON.parse(text) as WireMessage, bytes = Buffer.byteLength(text);
          if (bytes <= 512 * 1024) { this.raw.push({ message, bytes }); this.rawBytes += bytes; }
          while (this.raw.length > 32 || this.rawBytes > 512 * 1024) this.rawBytes -= this.raw.shift()!.bytes;
        } catch { /* The production parser records the fatal diagnostic. */ }
        route.send(payload);
      });
      route.onMessage(payload => {
        try {
          const message: unknown = JSON.parse(String(payload));
          if (message && typeof message === 'object' && 'type' in message && message.type === 'command' && 'requestId' in message && typeof message.requestId === 'string') {
            this.sent.push({ serial: ++this.serial, requestId: message.requestId });
            if (this.sent.length > 256) this.sent.shift();
          }
        } catch { /* Server owns command validation. */ }
        server.send(payload);
      });
      server.onClose((code, reason) => { this.observation.close(generation); route.close({ code, reason }); });
      route.onClose((code, reason) => { this.observation.close(generation); server.close({ code, reason }); });
    });
  }
  private async wait<T>(read: () => T | Promise<T>, done: (value: T) => boolean, diagnosis: string, timeout = 15000): Promise<T> {
    const end = Date.now() + timeout;
    for (;;) {
      this.observation.check();
      const value = await read();
      if (done(value)) return value;
      if (Date.now() >= end) throw new Error(`${diagnosis}: peer=${this.peer} generation=${this.observation.generation} latest=${JSON.stringify(this.observation.history.slice(-5))}`);
      await new Promise(resolve => setTimeout(resolve, 25));
    }
  }
  async ready(): Promise<Snapshot> {
    await this.wait(() => this.observation.ready, Boolean, 'Session join/publication lag');
    return this.latest()!;
  }
  async publication(target: StateTarget): Promise<Snapshot> {
    return (await this.wait(() => this.latest(), state => reached(state, target), `Publication lag target=${JSON.stringify(target)}`))!;
  }
  receipt(requestId: string): Receipt | undefined { return this.observation.receipt(requestId); }
  async waitReceipt(requestId: string): Promise<Receipt> {
    return (await this.wait(() => this.receipt(requestId), Boolean, `Missing command receipt requestId=${requestId}`))!;
  }
  async command(command: Command): Promise<Receipt> {
    this.observation.check();
    if (!this.socket || !this.observation.ready) throw new Error('Session is not ready for a command');
    // Request correlation is registered before send; it does not depend on history offsets.
    const receipt = this.waitReceipt(command.requestId);
    this.socket.send(JSON.stringify(command));
    return receipt;
  }
  async acceptedAction(action: () => Promise<unknown>): Promise<StateTarget> {
    const baseline = this.serial;
    // Capture command identity before the UI action can produce a receipt.
    const command = this.wait(() => this.sent.find(item => item.serial > baseline), Boolean, 'UI action sent no command');
    // Catch the pre-registered wait if the UI action itself fails.
    command.catch(() => {});
    await action();
    const receipt = await this.waitReceipt((await command)!.requestId);
    if (receipt.type === 'rejected') throw new Error(`Command rejection: peer=${this.peer} requestId=${receipt.requestId} code=${receipt.code}`);
    await this.publication(receipt);
    return receipt;
  }
  async converge(target: Snapshot, phase: string, timeout = 15000): Promise<void> {
    const wire = await this.publication(target);
    await assertCanonicalState(wire, target, { phase: `${phase}: reconstructed traffic`, peer: this.peer }, this.info);
    const applied = await this.wait(() => this.page.evaluate(() => {
      const view = (window as Window & TestObservationWindow).__freetrainAppliedSession?.();
      return view && JSON.parse(JSON.stringify(view)) as typeof view;
    }), view => view?.connection === 'ready' && reached(view.state, target), `Browser-applied state lag target=${JSON.stringify({ worldEpoch: target.worldEpoch, revision: target.revision, tick: target.tick })}`, timeout);
    await assertCanonicalState(applied!.state, target, { phase: `${phase}: browser applied`, peer: this.peer }, this.info);
    await expect(this.page.locator('canvas.world-canvas')).toHaveAttribute('data-world-epoch', target.worldEpoch);
    await expect(this.page.locator('canvas.world-canvas')).toHaveAttribute('data-render-revision', target.revision);
    await expect(this.page.locator('canvas.world-canvas')).toHaveAttribute('data-presentation', 'ready');
  }
  async reconnect(action: () => Promise<unknown>, target: Snapshot, phase: string) {
    const generation = this.observation.generation;
    const connected = this.wait(() => this.observation.generation, next => next > generation, 'Reconnect did not open a new session');
    connected.catch(() => {});
    await action(); await connected; await this.ready(); await this.converge(target, phase);
  }
}

export class SessionFixture {
  private sessions = new Map<Page, ObservedSession>();
  constructor(private info: TestInfo) {}
  async observe(page: Page, peer = `peer-${this.sessions.size}`): Promise<ObservedSession> {
    const existing = this.sessions.get(page); if (existing) return existing;
    const session = new ObservedSession(page, peer, this.info);
    this.sessions.set(page, session); await session.install(); return session;
  }
  async converge(target: Snapshot, phase: string, peers = [...this.sessions.values()]) {
    if (!target.paused) throw new Error(`Exact checkpoint requires a paused scene: ${phase}`);
    for (const session of peers) await session.converge(target, phase);
    return target;
  }
  async checkpoint(owner: ObservedSession, phase: string): Promise<Snapshot> {
    const bootstrap = await sessionBootstrap(owner.page);
    // HTTP and socket publications can carry different motion-history windows
    // at the same revision. Select one fixed socket publication, including every
    // field, after reaching the HTTP watermark; never compare mixed encodings.
    const target = bootstrap.kind === 'snapshot' ? await owner.publication(bootstrap.snapshot) : await owner.ready();
    return this.converge(target, phase);
  }
  async replaceSave(owner: ObservedSession, data: Buffer, phase: string) {
    const before = await owner.ready();
    const response = await owner.page.request.post(`/api/saves/upload?expectedWorldEpoch=${before.worldEpoch}`, { data });
    if (!response.ok()) throw new Error(`Save replacement rejected HTTP ${response.status()}: ${(await response.text()).slice(0, 200)}`);
    const next = await owner.publication({ worldEpoch: String(BigInt(before.worldEpoch) + 1n), revision: '0', tick: '0' });
    return this.converge(next, phase);
  }
  async minute(owner: ObservedSession, action: () => Promise<unknown>, phase: string) {
    const before = await owner.ready();
    if (!before.paused) throw new Error('Single-minute advancement requires a paused scene');
    const receipt = await owner.acceptedAction(action);
    const target = await owner.publication(receipt);
    if (target.worldEpoch !== before.worldEpoch || BigInt(target.tick) !== BigInt(before.tick) + 1n) throw new Error(`Single-minute target mismatch: ${JSON.stringify(receipt)}`);
    return this.converge(target, phase);
  }
  async restartCheckpoint(server: { restart?: () => Promise<void> }, peers: readonly { session: ObservedSession; secret?: string }[], phase: string) {
    if (!server.restart) throw new Error('Checkpoint restart requires a managed world server');
    await server.restart();
    for (const { session, secret = '' } of peers) {
      const response = await session.page.request.post('/api/access', { data: { secret } });
      if (!response.ok()) throw new Error(`Restart authentication HTTP ${response.status()}`);
      await session.page.reload(); await session.ready();
    }
    return this.checkpoint(peers[0].session, phase);
  }
}
