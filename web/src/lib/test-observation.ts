import type { ConnectionStatus, ReadState } from './session-client.ts';

/** Opt-in, read-only view of the state consumed by the shell. No commands,
 * mutable model objects or transport interception are exposed to the harness. */
export interface AppliedSession { connection: ConnectionStatus; state?: ReadState }
export interface TestObservationWindow {
  __freetrainObserveApplied?: boolean;
  __freetrainAppliedSession?: () => AppliedSession;
}
export function installTestObservation(read: () => AppliedSession): () => void {
  const target = window as Window & TestObservationWindow;
  if (!target.__freetrainObserveApplied) return () => {};
  Object.defineProperty(target, '__freetrainAppliedSession', { configurable: true, value: read });
  return () => { delete target.__freetrainAppliedSession; };
}
