import type { DepartureBell, EffectsBaseline } from '../generated/contracts.ts';

/** Ordered WebSocket delivery permits constant-space deduplication. Suppressed
 * events still consume their identity, so enabling audio never drains a backlog. */
export function createDepartureEvents() {
  let baseline: EffectsBaseline | undefined, sequence = 0n, ordinal = -1;
  return {
    reset(value?: EffectsBaseline) {
      baseline = value; sequence = BigInt(value?.publicationSequence ?? '0'); ordinal = -1;
    },
    accept(event: DepartureBell, worldEpoch: string) {
      if (!baseline || event.worldEpoch !== worldEpoch || event.worldEpoch !== baseline.worldEpoch || event.liveEpoch !== baseline.liveEpoch) return false;
      const next = BigInt(event.publicationSequence);
      if (next <= BigInt(baseline.publicationSequence) || next < sequence || next === sequence && event.effectOrdinal <= ordinal) return false;
      sequence = next; ordinal = event.effectOrdinal; return true;
    },
  };
}
