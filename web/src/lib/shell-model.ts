import type { CommandPayload, Rejected } from '../generated/contracts.ts';
import type { ConnectionStatus, FixtureBehavior, ReadState, SessionClient } from './session-client.ts';
import type { Locale } from './localization.ts';

// Imported reference families: a fixed pier and the condo's four supported heights.
export const catalog = Array.from({ length: 5 }, (_, index) => ({
  id: `catalog-${index + 1}`, family: index === 0 ? 'pier' as const : 'condo' as const,
  height: index === 0 ? 1 : index + 1, variant: 1,
}));
export interface ShellState {
  read?: ReadState;
  leaving: boolean;
  anonymousAccess: boolean;
  participantId?: string;
  role?: 'owner' | 'operator' | 'observer';
  locale: Locale;
  draft: string;
  composing: boolean;
  pending: boolean;
  connection: ConnectionStatus;
  feedback: Rejected['code'] | 'idle' | 'waiting' | 'accepted' | 'unavailable' | 'unresolved' | 'leaveFailed';
  behavior: FixtureBehavior;
  selectedBuilding: string;
}
export function createShellModel(connect: () => SessionClient, locale: Locale, reconnectDelay = 200) {
  let inputSequence = BigInt(Date.now()) * 1000n;
  let state: ShellState = { locale, leaving: false, anonymousAccess: false, draft: '', composing: false, pending: false, connection: 'loading', feedback: 'idle', behavior: 'normal', selectedBuilding: catalog[0].id };
  let client: SessionClient | undefined;
  let unsubscribe: (() => void) | undefined;
  let unsubscribeStatus: (() => void) | undefined;
  let timer: ReturnType<typeof setTimeout> | undefined;
  let generation = 0;
  let disposed = false;
  let dirty = false;
  const listeners = new Set<(state: ShellState) => void>();
  const update = (patch: Partial<ShellState>) => {
    state = { ...state, ...patch };
    for (const listener of listeners) listener(state);
  };
  const release = () => {
    generation++;
    clearTimeout(timer);
    unsubscribe?.(); unsubscribe = undefined;
    unsubscribeStatus?.(); unsubscribeStatus = undefined;
    client?.dispose(); client = undefined;
  };
  const reconnect = () => {
    if (disposed || state.leaving) return;
    const ambiguous = (state.pending && client?.mode === 'remote') || state.feedback === 'unresolved';
    release();
    update({ connection: 'loading', pending: false, feedback: ambiguous ? 'unresolved' : 'idle' });
    timer = setTimeout(() => {
      try {
        client = connect();
        unsubscribe = client.subscribe(read => {
          if (state.read && state.read.worldEpoch !== read.worldEpoch) dirty = false;
          update({ read, participantId: client?.participantId, role: client?.role, connection: 'ready', ...(!dirty ? { draft: read.stations[0]?.name ?? '' } : {}) });
        });
        unsubscribeStatus = client.subscribeStatus?.(connection => {
          if (connection === 'accessRequired') dirty = false;
          update({ connection, anonymousAccess: client?.anonymousAccess ?? false,
            ...(connection !== 'ready' ? { pending: false } : {}),
            ...(connection === 'accessRequired' ? { read: undefined, role: undefined, participantId: undefined, draft: '', composing: false } : {}),
            ...(client?.unresolved ? { feedback: 'unresolved' } : connection === 'disconnected' ? { feedback: 'unavailable' }
              : connection === 'ready' && state.feedback === 'unavailable' ? { feedback: 'idle' } : {}),
          });
        });
      } catch { release(); update({ connection: 'disconnected', feedback: 'unavailable' }); }
    }, reconnectDelay);
  };
  const send = async (payload: CommandPayload) => {
    if (disposed || !client || !state.read || state.pending || state.connection !== 'ready') return false;
    const current = generation;
    const submittedDraft = state.draft;
    update({ pending: true, feedback: 'waiting' });
    try {
      const result = await client.send({ type: 'command', requestId: crypto.randomUUID(), expectedWorldEpoch: state.read!.worldEpoch, payload });
      if (disposed || current !== generation) return;
      if (result.type === 'accepted') {
        if (payload.kind === 'renameStation' && submittedDraft === state.draft) {
          dirty = false;
          update({ draft: state.read!.stations[0].name });
        }
        update({ pending: false, feedback: 'accepted' });
        return true;
      } else {
        update({ pending: false, feedback: result.code });
        return false;
      }
    } catch {
      if (!disposed && current === generation) update({ pending: false, feedback: client?.unresolved ? 'unresolved' : 'unavailable' });
      return false;
    }
  };
  let acquiring: Promise<boolean | undefined> | undefined;
  const ensureControl = async () => {
    const train = state.read?.trains[0];
    if (!train || !client || state.role === 'observer') return false;
    if (train.controlLease && train.controlLease.participantId === client.participantId) return true;
    if (!acquiring) acquiring = send({ kind: 'acquireControl', trainId: train.id }).finally(() => { acquiring = undefined; });
    return await acquiring;
  };
  reconnect();
  return {
    get state() { return state; },
    subscribe(listener: (state: ShellState) => void) { listeners.add(listener); listener(state); return () => { listeners.delete(listener); }; },
    setLocale(locale: Locale) { update({ locale }); },
    setDraft(draft: string) { dirty = true; update({ draft }); },
    setComposing(composing: boolean) { update({ composing }); },
    setBehavior(behavior: FixtureBehavior) { update({ behavior }); },
    selectBuilding(selectedBuilding: string) { update({ selectedBuilding }); },
    resetDraft() { dirty = false; update({ draft: state.read?.stations[0].name ?? '' }); },
    rename() {
      if (state.composing || !state.draft.trim() || !state.read) return;
      return send({ kind: 'renameStation', stationId: state.read.stations[0].id, expectedRevision: state.read.stations[0].revision, name: state.draft });
    },
    async controller(controller: 'automatic' | 'manual') {
      if (!await ensureControl()) return false;
      const train = state.read?.trains[0];
      if (train?.controlLease) return send({ kind: 'setController', trainId: train.id, expectedRevision: train.revision, controller, leaseId: train.controlLease.leaseId });
    },
    acquireControl() { const train = state.read?.trains[0]; if (train) return send({ kind: 'acquireControl', trainId: train.id }); },
    releaseControl() {
      const train = state.read?.trains[0];
      if (train?.controlLease) return send({ kind: 'releaseControl', trainId: train.id, leaseId: train.controlLease.leaseId });
    },
    async input(keys: ReadonlySet<string>) {
      if (!client || state.connection !== 'ready' || state.read?.trains[0]?.controller !== 'manual') return;
      if (keys.size === 0 && state.read.trains[0].controlLease?.participantId !== client.participantId) return;
      const current = generation;
      if (!await ensureControl() || current !== generation || !client || state.connection !== 'ready') return;
      const train = state.read!.trains[0];
      if (!train.controlLease) return;
      const payload: CommandPayload = { kind: 'controllerInput', trainId: train.id, leaseId: train.controlLease.leaseId,
        inputSequence: String(++inputSequence), pass: keys.has('Shift'), depart: keys.has('ArrowUp'), reverse: keys.has('ArrowDown'),
        left: keys.has('ArrowLeft'), right: keys.has('ArrowRight'), reset: keys.size === 0 };
      try {
        const result = await client.send({ type: 'command', requestId: crypto.randomUUID(), expectedWorldEpoch: state.read!.worldEpoch, payload });
        if (current === generation && result.type === 'rejected') update({ feedback: result.code });
      }
      catch { /* Transport reports loss; the server also expires held input. */ }
    },
    async leave(revoke: () => Promise<void>) {
      if (disposed || state.leaving) return;
      release();
      const current = generation;
      dirty = false;
      update({ leaving: true, read: undefined, participantId: undefined, role: undefined, anonymousAccess: false,
        draft: '', composing: false, pending: false, connection: 'loading', feedback: 'idle' });
      try {
        await revoke();
        if (disposed || current !== generation) return;
        update({ leaving: false });
        reconnect();
      } catch {
        if (!disposed && current === generation) update({ leaving: false, connection: 'disconnected', feedback: 'leaveFailed' });
      }
    },
    send, reconnect,
    dispose() { disposed = true; release(); listeners.clear(); },
  };
}
export type ShellModel = ReturnType<typeof createShellModel>;
