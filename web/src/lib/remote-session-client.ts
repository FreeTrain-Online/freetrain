import type { Accepted, Command, Join, Rejected, SoundEvent } from '../generated/contracts.ts';
import type { ConnectionStatus, ReadState, SessionClient } from './session-client.ts';
import { applyDelta, immutable, parseMessage, parseSpatialPreview } from './protocol.ts';
import { IncompatibleWorldError, sameIdentity } from './world-identity.ts';

const compatibility = 'protocolVersion=3&contentVersion=reference-spatial-content-2&contentVersion=prototype-content-1';
export async function authorize(secret: string): Promise<boolean> {
  const response = await fetch('/api/access', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ secret }), signal: AbortSignal.timeout(5000) });
  return response.ok;
}
export async function leaveGame(): Promise<void> {
  const response = await fetch('/api/leave', { method: 'POST', credentials: 'same-origin', signal: AbortSignal.timeout(5000) });
  if (!response.ok) throw new Error('Leave game could not be confirmed');
}
// A retry is automatic only on the same open socket, with the same request ID,
// within the server's advertised/documented two-minute in-memory receipt window.
// Any connection loss ends pending operations as unresolved; they are never replayed.
export function createRemoteSessionClient(onSound?: (event: SoundEvent, epoch: string) => void): SessionClient {
  const listeners = new Set<(read: ReadState) => void>();
  const statuses = new Set<(status: ConnectionStatus) => void>();
  const pending = new Map<string, { command: Command; resolve: (value: Accepted | Rejected) => void; reject: (error: Error) => void; timer: ReturnType<typeof setTimeout>; attempts: number }>();
  const controller = new AbortController();
  let socket: WebSocket | undefined;
  let read: ReadState | undefined;
  let join: Join | undefined;
  let status: ConnectionStatus = 'loading';
  let disposed = false;
  let anonymousAccess = false;
  let connectionStage: 'bootstrap' | 'joining' | 'synchronizing' = 'bootstrap';
  let retryAt: number | undefined;
  let authorizedBefore = false;
  let authRequiredAgain = false;
  let reconnectTimer: ReturnType<typeof setTimeout> | undefined;
  let heartbeat: ReturnType<typeof setInterval> | undefined;
  let connectionTimer: ReturnType<typeof setTimeout> | undefined;
  let attempts = 0;
  let resyncing = false;
  let unresolved = false;
  const setStatus = (next: ConnectionStatus) => { status = next; for (const listener of statuses) listener(next); };
  const publish = (next: ReadState) => { read = immutable(next); for (const listener of listeners) listener(read); };
  const clearPending = () => {
    if (pending.size) unresolved = true;
    for (const item of pending.values()) { clearTimeout(item.timer); item.reject(new Error('unresolved')); }
    pending.clear();
  };
  const fail = () => {
    clearInterval(heartbeat); clearTimeout(connectionTimer); clearPending();
    if (socket) { socket.onclose = null; socket.close(); socket = undefined; }
    join = undefined;
    if (disposed) return;
    const delay = Math.min(5000, 250 * 2 ** Math.min(attempts++, 5));
    retryAt = Date.now() + delay;
    setStatus('disconnected');
    clearTimeout(reconnectTimer);
    reconnectTimer = setTimeout(connect, delay);
  };
  const suspend = () => { if (socket?.readyState === WebSocket.OPEN) socket.send(JSON.stringify({ type: 'inputReset' })); };
  const incompatible = () => {
    clearInterval(heartbeat);clearTimeout(connectionTimer);clearTimeout(reconnectTimer);clearPending();
    if(socket){socket.onclose=null;socket.close();socket=undefined;}
    join=undefined;read=undefined;setStatus('incompatible');
  };
  const visibility = () => { if (document.hidden) suspend(); };
  const blur = () => suspend();
  document.addEventListener('visibilitychange', visibility);
  window.addEventListener('blur', blur);
  const connect = async () => {
    if (disposed) return;
    retryAt = undefined; connectionStage = 'bootstrap';
    setStatus('loading');
    try {
      const response = await fetch(`/api/bootstrap?${compatibility}`, { signal: AbortSignal.any([controller.signal, AbortSignal.timeout(5000)]), credentials: 'same-origin' });
      if (disposed) return;
      if (response.status === 401) {
        const access = await response.json().catch(() => ({}));
        if (disposed) return;
        anonymousAccess = access.anonymousAccess === true;
        authRequiredAgain = authorizedBefore; read = undefined;
        return setStatus('accessRequired');
      }
      if (response.status === 426) return setStatus('incompatible');
      if (!response.ok) throw new Error('Bootstrap failed');
      const bootstrap = parseMessage(await response.text());
      if (bootstrap.type !== 'join') throw new Error('Expected join');
      connectionStage = 'joining'; setStatus('loading');
      const url = new URL(`/api/session?${compatibility}`, location.href);
      url.protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
      const current = socket = new WebSocket(url);
      connectionTimer = setTimeout(fail, 5000);
      current.onmessage = event => {
        if (disposed || socket !== current) return;
        try {
          const message = parseMessage(event.data);
          switch (message.type) {
            case 'join':
              if (message.protocolVersion !== 3 || !sameIdentity(bootstrap,message) || message.worldEpoch !== message.snapshot.worldEpoch) throw new Error('Compatibility mismatch');
              clearTimeout(connectionTimer); join = message; resyncing = false; attempts = 0; authorizedBefore = true; authRequiredAgain = false;
              publish(message.snapshot); setStatus('ready');
              if (document.hidden) suspend();
              break;
            case 'snapshot':
              if (!join) throw new Error('Snapshot before join');
              if(!sameIdentity(join,message))throw new IncompatibleWorldError('World profile changed');
              clearTimeout(connectionTimer); resyncing = false;
              if (read && read.worldEpoch !== message.worldEpoch) clearPending();
              publish(message); setStatus('ready'); break;
            case 'delta': {
              if (resyncing) break;
              const next = read && applyDelta(read, message);
              if (next) publish(next);
              else {
                resyncing = true; connectionStage = 'synchronizing'; setStatus('loading'); current.send(JSON.stringify({ type: 'resync' }));
                connectionTimer = setTimeout(fail, 5000);
              }
              break;
            }
            case 'soundEvent':
              if (read && status === 'ready') onSound?.(message, read.worldEpoch);
              break;
            case 'accepted': case 'rejected': {
              const item = pending.get(message.requestId);
              if (!item) break;
              clearTimeout(item.timer); pending.delete(message.requestId);
              item.resolve(message); break;
            }
            default: throw new Error('Unexpected server message');
          }
        } catch (error) { if(error instanceof IncompatibleWorldError)incompatible();else current.close(1002); }
      };
      current.onopen = () => { heartbeat = setInterval(() => { if (current.readyState === WebSocket.OPEN) current.send('{"type":"heartbeat"}'); }, 5000); };
      current.onclose = fail;
      current.onerror = () => { /* onclose handles reconnect and ambiguous results. */ };
    } catch (error) { if (!disposed) {if(error instanceof IncompatibleWorldError)incompatible();else fail();} }
  };
  void connect();
  return {
    mode: 'remote',
    get connectionStage() { return connectionStage; },
    get retryAt() { return retryAt; },
    get authRequiredAgain() { return authRequiredAgain; },
    get anonymousAccess() { return anonymousAccess; },
    get participantId() { return join?.participantId; },
    get role() { return join?.role; },
    get unresolved() { return unresolved; },
    subscribe(listener) { listeners.add(listener); if (read) listener(read); return () => { listeners.delete(listener); }; },
    subscribeStatus(listener) { statuses.add(listener); listener(status); return () => { statuses.delete(listener); }; },
    async previewSpatial(query,signal) {
      const identity=join;
      if(disposed||status!=='ready'||!identity)throw new Error('Unavailable');
      const response=await fetch(`/api/spatial/preview?${compatibility}`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(query),credentials:'same-origin',signal:AbortSignal.any([signal,controller.signal,AbortSignal.timeout(5000)])});
      if(!response.ok)throw new Error('Spatial preview failed');
      const preview=parseSpatialPreview(await response.json());
      if(disposed||join!==identity||status!=='ready'||preview.worldEpoch!==query.expectedWorldEpoch||!sameIdentity(identity,preview))throw new Error('Stale spatial preview');
      return preview;
    },
    send(command) {
      if (disposed || status !== 'ready' || socket?.readyState !== WebSocket.OPEN || pending.size >= 32 || pending.has(command.requestId)) return Promise.reject(new Error('Unavailable'));
      return new Promise((resolve, reject) => {
        const retry = () => {
          const item = pending.get(command.requestId);
          if (!item) return;
          if (++item.attempts >= 3) {
            fail(); return;
          }
          if (socket?.readyState === WebSocket.OPEN) socket.send(JSON.stringify(command));
          item.timer = setTimeout(retry, 2000);
        };
        pending.set(command.requestId, { command, resolve, reject, timer: setTimeout(retry, 2000), attempts: 0 });
        socket!.send(JSON.stringify(command));
      });
    },
    dispose() {
      disposed = true; controller.abort(); clearTimeout(reconnectTimer); clearInterval(heartbeat); clearTimeout(connectionTimer);
      document.removeEventListener('visibilitychange', visibility); window.removeEventListener('blur', blur);
      clearPending(); listeners.clear(); statuses.clear();
      if (socket) { socket.onclose = null; socket.close(); }
    },
  };
}
