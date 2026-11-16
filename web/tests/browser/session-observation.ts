import type { Accepted, Join, Rejected, Snapshot, WireMessage } from '../../src/generated/contracts.ts';
import type { ReadState } from '../../src/lib/session-client.ts';
import { applyDelta, immutable, parseMessageValue } from '../../src/lib/protocol.ts';
import { StateAssembler } from '../../src/lib/state-transfer.ts';

export type Receipt = Accepted | Rejected;
export type StateTarget = Pick<Snapshot, 'worldEpoch' | 'revision' | 'tick'>;
export type SessionIdentity = Pick<Join, 'serverIncarnation' | 'participantId' | 'sessionId' | 'role'>;
export interface SessionEvent { generation: number; type: string; worldEpoch?: string; revision?: string; tick?: string; requestId?: string }

/** Incremental production decoding. Only the current immutable predecessor is
 * retained: it already owns the heat buffers needed by a chained reference.
 * Full traffic is spooled by BrowserDiagnostics; this ring is diagnostic metadata.
 */
export class SessionObservation {
  constructor(private implicitConnections = false) {}
  generation = 0;
  identity?: SessionIdentity;
  private state?: ReadState;
  private assembly = new StateAssembler();
  private receiving = Promise.resolve();
  private failure?: Error;
  private receipts = new Map<string, Receipt>();
  private events: SessionEvent[] = [];
  private joined = false;
  private connected = false;
  private queued = 0;
  private transferStarted?: number;

  beginConnection(): number {
    this.generation++;
    this.connected = true;
    this.state = undefined;
    this.identity = undefined;
    this.joined = false;
    this.receipts.clear();
    this.assembly = new StateAssembler();
    this.transferStarted = undefined;
    this.record({ type: 'connection' });
    return this.generation;
  }
  close(generation: number) {
    if (generation !== this.generation) return;
    this.connected = false;
    this.state = undefined;
    this.record({ type: 'closed' });
  }
  get history(): readonly SessionEvent[] { return this.events; }
  get ready(): boolean { this.check(); return this.connected && this.joined && !!this.state && !this.assembly.pending; }
  latest(): ReadState | undefined { this.check(); return this.state; }
  receipt(requestId: string): Receipt | undefined { this.check(); return this.receipts.get(requestId); }
  check() {
    if (this.transferStarted !== undefined && Date.now() - this.transferStarted >= 30000) this.reject(new Error('state transfer timeout'));
    if (this.failure) throw this.failure;
  }
  private record(event: Omit<SessionEvent, 'generation'>) {
    this.events.push({ generation: this.generation, ...event });
    if (this.events.length > 100) this.events.shift();
  }
  private reject(error: unknown) {
    this.failure ??= new Error(`Session decoder rejection: generation=${this.generation} epoch=${this.state?.worldEpoch ?? '?'} revision=${this.state?.revision ?? '?'} tick=${this.state?.tick ?? '?'}: ${String(error).slice(0, 350)}\nRecent events: ${JSON.stringify(this.events.slice(-6))}`);
  }
  /** Queue frames at arrival, including the asynchronous digest of final chunks.
   * Old sockets cannot publish into a new connection even after digest completion.
   * Failures remain sticky across reconnects: resync must not hide an invalid chain.
   */
  enqueue(value: unknown, generation = this.generation): void {
    if (generation !== this.generation) return;
    if (++this.queued > 64) this.reject(new Error('receive queue limit'));
    this.receiving = this.receiving.then(async () => {
      this.queued--;
      if (generation !== this.generation || this.failure) return;
      try {
        let message = parseMessageValue(typeof value === 'string' ? JSON.parse(value) : value);
        this.record({ type: message.type, ...('worldEpoch' in message ? { worldEpoch: message.worldEpoch } : {}), ...('revision' in message ? { revision: message.revision } : {}), ...('tick' in message ? { tick: message.tick } : {}), ...('requestId' in message ? { requestId: message.requestId } : {}) });
        if (message.type === 'stateChunk') {
          this.transferStarted ??= Date.now();
          const complete = await this.assembly.accept(message);
          if (!complete || generation !== this.generation) return;
          message = complete;
          this.transferStarted = undefined;
        } else if (this.assembly.pending) throw new Error('state transfer interrupted');
        this.accept(message);
      } catch (error) { if (generation === this.generation) this.reject(error); }
    });
  }
  async settled(): Promise<void> { await this.receiving; this.check(); }
  private accept(message: WireMessage) {
    switch (message.type) {
      case 'join':
        if (this.joined && this.implicitConnections) this.beginConnection();
        if (this.joined) throw new Error('duplicate join in one connection');
        this.identity = { serverIncarnation: message.serverIncarnation, participantId: message.participantId, sessionId: message.sessionId, role: message.role };
        this.joined = true;
        this.state = immutable(message.snapshot);
        break;
      case 'snapshot':
        if (!this.joined) throw new Error('snapshot before join');
        if (this.state && (BigInt(message.worldEpoch) < BigInt(this.state.worldEpoch) || message.worldEpoch === this.state.worldEpoch && BigInt(message.revision) < BigInt(this.state.revision))) throw new Error('stale snapshot');
        if (this.state?.worldEpoch !== message.worldEpoch) this.receipts.clear();
        this.state = immutable(message);
        break;
      case 'delta': {
        const next = this.state && applyDelta(this.state, message);
        if (!next) throw new Error(`invalid delta base/heat/successor: base=${message.baseRevision} heatBase=${message.cityHeatBaseRevision ?? 'none'} target=${message.worldEpoch}/${message.revision}/${message.tick}`);
        this.state = next;
        break;
      }
      case 'accepted': case 'rejected':
        this.receipts.set(message.requestId, message);
        if (this.receipts.size > 256) this.receipts.delete(this.receipts.keys().next().value!);
        break;
      case 'soundEvent': case 'departureBell': break;
      default: throw new Error(`unexpected server message ${message.type}`);
    }
  }
}

export function reached(actual: StateTarget | undefined, target: StateTarget): boolean {
  return !!actual && (BigInt(actual.worldEpoch) > BigInt(target.worldEpoch) || actual.worldEpoch === target.worldEpoch && BigInt(actual.revision) >= BigInt(target.revision));
}

/** Canonical JSON omits optional undefined properties introduced by applyDelta. */
export function canonicalJSON(state: ReadState): Snapshot { return JSON.parse(JSON.stringify(state)) as Snapshot; }
export { parseMessageValue };
