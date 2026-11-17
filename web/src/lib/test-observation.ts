import type { ConnectionStatus, ReadState } from './session-client.ts';

/** Opt-in, read-only view of the state consumed by the shell. No commands,
 * mutable model objects or transport interception are exposed to the harness. */
export interface AppliedSession { connection: ConnectionStatus; state?: ReadState }
export interface TestObservationWindow {
  __freetrainObserveApplied?: boolean;
  __freetrainAppliedSession?: () => AppliedSession;
}
/** Resolves after reactive changes and the corresponding ready map draw.
 * It observes rendering and never requests or forces a draw. */
export interface MapDrawObservation {
  __freetrainWaitForDraw?: () => Promise<void>;
}
export function installTestObservation(read: () => AppliedSession): () => void {
  const target = window as Window & TestObservationWindow;
  if (!target.__freetrainObserveApplied) return () => {};
  Object.defineProperty(target, '__freetrainAppliedSession', { configurable: true, value: read });
  return () => { delete target.__freetrainAppliedSession; };
}
