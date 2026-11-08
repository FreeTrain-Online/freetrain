import type { CornerTerrainQuery } from '../generated/contracts.ts';
import { railwayRevision } from './railway-edit.ts';
import type { CommandPayload, Rejected, SpatialQuery } from '../generated/contracts.ts';
import type { ConnectionStatus, FixtureBehavior, ReadState, SessionClient } from './session-client.ts';
import type { Locale } from './localization.ts';
import { initialCatalogSelection, isCatalogItem, signalDirections, type SignalDirection, type CatalogCategory } from './building-catalog.ts';

// Imported reference families: a fixed pier and the condo's four supported heights.
export const catalog = Array.from({ length: 5 }, (_, index) => ({
  id: `catalog-${index + 1}`, family: index === 0 ? 'pier' as const : 'condo' as const,
  height: index === 0 ? 1 : index + 1, variant: 1,
}));
export interface ShellState {
  read?: ReadState;
  leaving: boolean;
  signedOut: boolean;
  retryAt?: number;
  connectionStage?: 'bootstrap' | 'joining' | 'synchronizing';
  authRequiredAgain?: boolean;
  anonymousAccess: boolean;
  participantId?: string;
  role?: 'owner' | 'operator' | 'observer';
  locale: Locale;
  draft: string;
  draftDirty: boolean;
  draftRevision?: string;
  draftStationId?: string;
  draftInterrupted?: boolean;
  composing: boolean;
  pending: boolean;
  connection: ConnectionStatus;
  feedback: Rejected['code'] | 'idle' | 'waiting' | 'accepted' | 'unavailable' | 'unresolved' | 'leaveFailed';
  behavior: FixtureBehavior;
  selectedBuilding: string;
  catalogSelections: Record<CatalogCategory, string>;
  signalDirection: SignalDirection;
}
const stations = (read?:ReadState) => read?.mixed?.stationGraph?.buildings ?? read?.stations ?? [];
export function createShellModel(connect: () => SessionClient, locale: Locale, reconnectDelay = 200) {
  let state: ShellState = { locale, leaving: false, signedOut: false, anonymousAccess: false, draftDirty: false, draft: '', composing: false, pending: false, connection: 'loading', feedback: 'idle', behavior: 'normal', selectedBuilding: catalog[0].id, catalogSelections: { ...initialCatalogSelection }, signalDirection: 0 };
  let client: SessionClient | undefined;
  let unsubscribe: (() => void) | undefined;
  let unsubscribeStatus: (() => void) | undefined;
  let timer: ReturnType<typeof setTimeout> | undefined;
  let generation = 0;
  let disposed = false;
  let dirty = false;
  let leaveRequested = false;
  let authorizedBefore = false;
  let stationId: string | undefined;
  let draftRevision: string | undefined;
  const listeners = new Set<(state: ShellState) => void>();
  const update = (patch: Partial<ShellState>) => {
    state = { ...state, ...patch, draftDirty: dirty, draftRevision, draftStationId: stationId };
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
    if (disposed || state.leaving || leaveRequested) return;
    const ambiguous = (state.pending && client?.mode === 'remote') || state.feedback === 'unresolved';
    release();
    update({ connection: 'loading', retryAt: undefined, connectionStage: 'bootstrap', pending: false, feedback: ambiguous ? 'unresolved' : 'idle' });
    timer = setTimeout(() => {
      try {
        client = connect();
        const current = generation;
        const activeClient = client;
        unsubscribe = client.subscribe(read => {
          if (disposed || current !== generation || leaveRequested) return;
          if (state.signedOut) { update({ connection: 'accessRequired', anonymousAccess: activeClient.anonymousAccess ?? false }); return; }
          if (state.read && state.read.worldEpoch !== read.worldEpoch) {
            const interrupted = dirty; dirty = false; stationId = undefined; draftRevision = undefined;
            update({ draftInterrupted: state.draftInterrupted || interrupted });
          }
          authorizedBefore = true;
          const station = stations(read).find(item => item.id === stationId) ?? (!stationId ? stations(read)[0] : undefined);
          if (!dirty) draftRevision = station?.revision;
          update({ read, participantId: client?.participantId, role: client?.role, connection: 'ready', ...(!dirty ? { draft: station?.name ?? '' } : {}) });
        });
        unsubscribeStatus = client.subscribeStatus?.(connection => {
          if (disposed || current !== generation || leaveRequested) return;
          if (state.signedOut && connection === 'ready') return;
          if (connection !== 'ready' && !(connection === 'loading' && activeClient.connectionStage === 'synchronizing')) {
            const interrupted = dirty; dirty = false; draftRevision = undefined; stationId = undefined;
            update({ draft: '', composing: false, draftInterrupted: state.draftInterrupted || interrupted });
          }
          update({ connection, retryAt: activeClient.retryAt, connectionStage: activeClient.connectionStage, authRequiredAgain: activeClient.authRequiredAgain || (connection === 'accessRequired' && authorizedBefore && !state.signedOut), anonymousAccess: client?.anonymousAccess ?? false,
            ...(connection !== 'ready' && !(connection === 'loading' && activeClient.connectionStage === 'synchronizing') ? { pending: false } : {}),
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
        if (payload.kind === 'renameStation' && submittedDraft === state.draft && (!stationId || stationId === payload.stationId)) {
          dirty = false;
          const station = stations(state.read).find(item => item.id === payload.stationId);
          draftRevision = station?.revision;
          update({ draft: station?.name ?? '' });
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
  reconnect();
  return {
    get state() { return state; },
    async previewFleetTrade(query:import('../generated/contracts.ts').FleetTradeQuery,signal:AbortSignal) {
      const current=generation,active=client;
      if(disposed||state.connection!=='ready'||!active?.previewFleetTrade)throw new Error('Unavailable');
      const result=await active.previewFleetTrade(query,signal);
      if(disposed||signal.aborted||current!==generation||client!==active||state.connection!=='ready'||state.read?.worldEpoch!==query.expectedWorldEpoch||state.read?.revision!==query.expectedRevision||state.read?.tick!==query.expectedMinute)throw new Error('Stale fleet quote');
      return result;
    },
    async previewFleetDeployment(query:import('../generated/contracts.ts').FleetDeploymentQuery,signal:AbortSignal) {
      const current=generation,active=client;
      if(disposed||state.connection!=='ready'||!active?.previewFleetDeployment)throw new Error('Unavailable');
      const result=await active.previewFleetDeployment(query,signal);
      if(disposed||signal.aborted||current!==generation||client!==active||state.connection!=='ready'||state.read?.worldEpoch!==query.expectedWorldEpoch||state.read?.revision!==query.expectedRevision||state.read?.tick!==query.expectedMinute)throw new Error('Stale fleet quote');
      return result;
    },
    async previewRemoval(query:import('../generated/contracts.ts').RemovalQuery,signal:AbortSignal) {
      const current=generation,active=client;
      if(disposed||state.connection!=='ready'||!active?.previewRemoval)throw new Error('Unavailable');
      const result=await active.previewRemoval(query,signal);
      if(disposed||signal.aborted||current!==generation||client!==active||state.connection!=='ready'||state.read?.worldEpoch!==query.expectedWorldEpoch||railwayRevision(state.read)!==query.expectedGeometryRevision||state.read?.contentVersion!==query.expectedContentVersion)throw new Error('Stale removal preview');
      return result;
    },
    async previewSlope(query:import('../generated/contracts.ts').SlopeQuery,signal:AbortSignal) {
      const current=generation,active=client;
      if(disposed||state.connection!=='ready'||!active?.previewSlope)throw new Error('Unavailable');
      const result=await active.previewSlope(query,signal);
      if(disposed||signal.aborted||current!==generation||client!==active||state.connection!=='ready'||state.read?.worldEpoch!==query.expectedWorldEpoch||railwayRevision(state.read)!==query.expectedGeometryRevision||state.read?.contentVersion!==query.expectedContentVersion)throw new Error('Stale slope preview');
      return result;
    },
    async previewStationBuilding(query:import('../generated/contracts.ts').StationBuildingQuery,signal:AbortSignal) {
      const current=generation,active=client;
      if(disposed||state.connection!=='ready'||!active?.previewStationBuilding)throw new Error('Unavailable');
      const result=await active.previewStationBuilding(query,signal);
      if(disposed||signal.aborted||current!==generation||client!==active||state.connection!=='ready'||state.read?.worldEpoch!==query.expectedWorldEpoch||railwayRevision(state.read)!==query.expectedGeometryRevision||state.read?.tick!==query.expectedMinute||state.read?.contentVersion!==query.expectedContentVersion)throw new Error('Stale station preview');
      return result;
    },
    async previewRailway(query:import('../generated/contracts.ts').RailwayQuery,signal:AbortSignal) {
      const current=generation,active=client;
      if(disposed||state.connection!=='ready'||!active?.previewRailway)throw new Error('Unavailable');
      const result=await active.previewRailway(query,signal);
      if(disposed||signal.aborted||current!==generation||client!==active||state.connection!=='ready'||state.read?.worldEpoch!==query.expectedWorldEpoch||railwayRevision(state.read)!==query.expectedGeometryRevision||state.read?.contentVersion!==query.expectedContentVersion)throw new Error('Stale railway preview');
      return result;
    },
    async previewCorner(query:CornerTerrainQuery,signal:AbortSignal) {
      const current=generation,active=client;
      if(disposed||state.connection!=='ready'||!active?.previewCorner)throw new Error('Unavailable');
      const result=await active.previewCorner(query,signal);
      if(disposed||signal.aborted||current!==generation||client!==active||state.connection!=='ready'||state.read?.worldEpoch!==query.expectedWorldEpoch||railwayRevision(state.read)!==query.expectedGeometryRevision)throw new Error('Stale corner preview');
      return result;
    },
    async previewSpatial(query:SpatialQuery,signal:AbortSignal) {
      const current=generation,active=client;
      if(disposed||state.connection!=='ready'||!active?.previewSpatial)throw new Error('Unavailable');
      const result=await active.previewSpatial(query,signal);
      if(disposed||current!==generation||client!==active||state.read?.worldEpoch!==query.expectedWorldEpoch)throw new Error('Stale spatial preview');
      return result;
    },
    subscribe(listener: (state: ShellState) => void) { listeners.add(listener); listener(state); return () => { listeners.delete(listener); }; },
    setLocale(locale: Locale) { update({ locale }); },
    setDraft(draft: string) { dirty = true; update({ draft, draftInterrupted: false }); },
    setComposing(composing: boolean) { update({ composing }); },
    setBehavior(behavior: FixtureBehavior) { update({ behavior }); },
    selectBuilding(selectedBuilding: string) { update({ selectedBuilding }); },
    selectCatalogItem(category: CatalogCategory, id: string, direction?: SignalDirection) {
      if (state.pending || !isCatalogItem(category, id) || direction !== undefined && (category !== 'signal' || !signalDirections.some(d => d.value === direction))) return;
      update({ catalogSelections: { ...state.catalogSelections, [category]: id }, ...(direction !== undefined ? { signalDirection: direction } : {}) });
    },
    selectStation(id: string) { stationId = id; dirty = false; const station = stations(state.read).find(item => item.id === id); draftRevision = station?.revision; update({ draft: station?.name ?? '', composing: false, draftInterrupted: false }); },
    resetDraft() { dirty = false; const station = stations(state.read).find(item => item.id === stationId) ?? (!stationId ? stations(state.read)[0] : undefined); draftRevision = station?.revision; update({ draft: station?.name ?? '' }); },
    rename() {
      if (state.composing || !state.draft.trim() || !state.read) return;
      const station = stations(state.read).find(item => item.id === stationId) ?? (!stationId ? stations(state.read)[0] : undefined);
      if (station) return send({ kind: 'renameStation', stationId: station.id, expectedRevision: draftRevision ?? station.revision, name: state.draft });
    },
    async leave(revoke: () => Promise<void>) {
      if (disposed || state.leaving) return;
      leaveRequested = true;
      release();
      const current = generation;
      const unresolved = state.feedback === 'unresolved' || state.pending;
      dirty = false; stationId = undefined; draftRevision = undefined;
      update({ leaving: true, read: undefined, participantId: undefined, role: undefined, anonymousAccess: false,
        draft: '', composing: false, pending: false, connection: 'loading', retryAt: undefined, feedback: unresolved ? 'unresolved' : 'idle' });
      try {
        await revoke();
        if (disposed || current !== generation) return;
        leaveRequested = false;
        update({ leaving: false, signedOut: true });
        reconnect();
      } catch {
        if (!disposed && current === generation) update({ leaving: false, connection: 'disconnected', feedback: 'leaveFailed' });
      }
    },
    connectAuthorized() { if (leaveRequested || state.leaving) return; update({ signedOut: false }); reconnect(); },
    send, reconnect,
    dispose() { disposed = true; release(); listeners.clear(); },
  };
}
export type ShellModel = ReturnType<typeof createShellModel>;
