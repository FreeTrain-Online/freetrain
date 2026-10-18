import fixture from '../../../prototype/protocol/fixtures/snapshot.json' with { type: 'json' };
import type { Snapshot } from '../generated/contracts.ts';
import type { ReadState } from './session-client.ts';
// Bundled presentation data only. Never publish this through the session model.
// Deep freezing makes accidental modification fail, including nested entity data.
function freeze<T extends object>(value: T): T {
  Object.values(value).forEach(child => { if (child && typeof child === 'object') freeze(child); });
  return Object.freeze(value);
}
export const demoScene: ReadState = freeze({ ...structuredClone(fixture), worldEpoch: 'local-demo-only' } as Snapshot);
