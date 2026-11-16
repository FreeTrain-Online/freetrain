import assert from 'node:assert/strict';
import literal from '../../prototype/protocol/fixtures/snapshot-city.json' with { type: 'json' };
import removal from '../../prototype/protocol/fixtures/delta-city.json' with { type: 'json' };
import { applyDelta, immutable, parseMessageValue } from '../src/lib/protocol.ts';
import type { Delta, Join, Snapshot, StateChunk } from '../src/generated/contracts.ts';
import { stateChunkBytes } from '../src/lib/state-transfer.ts';
import { canonicalJSON, SessionObservation } from './browser/session-observation.ts';

const snapshot = () => structuredClone(literal) as Snapshot;
function join(s = snapshot(), participantId = 'owner'): Join {
  return { type: 'join', protocolVersion: 11, serverIncarnation: 'fixture', sessionId: 'reference', participantId, role: 'owner', worldEpoch: s.worldEpoch, contentVersion: s.contentVersion, ruleset: s.ruleset, requiredPacks: s.requiredPacks, snapshot: s };
}
async function connected() {
  const observer = new SessionObservation(); observer.beginConnection(); observer.enqueue(join()); await observer.settled(); return observer;
}
function compact(before: Snapshot, full = structuredClone(removal) as Delta): Delta {
  const delta = structuredClone(full);
  delta.baseRevision = before.revision; delta.revision = String(BigInt(before.revision) + 1n);
  delta.cityHeatBaseRevision = before.revision;
  delta.mixed!.city!.heat.q = []; delta.mixed!.city!.heat.back = []; delta.mixed!.city!.heat.rho = [];
  return delta;
}
async function chunks(message: Join | Snapshot): Promise<StateChunk[]> {
  const s = message.type === 'join' ? message.snapshot : message;
  const data = new TextEncoder().encode(JSON.stringify({ message, packedTerrain: null }) + ' '.repeat(stateChunkBytes));
  const sha256 = Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', data)), n => n.toString(16).padStart(2, '0')).join('');
  const result: StateChunk[] = [];
  for (let offset = 0; offset < data.length; offset += stateChunkBytes) result.push({ type: 'stateChunk', transferId: 'a'.repeat(32), worldEpoch: s.worldEpoch, revision: s.revision, index: result.length, count: Math.ceil(data.length / stateChunkBytes), totalBytes: data.length, sha256, data: btoa(String.fromCharCode(...data.slice(offset, offset + stateChunkBytes))) });
  return result;
}

Deno.test('observer resolves chained heat references and changed heat through production decoding, without mutating wire values', async () => {
  const observer = await connected(), first = compact(snapshot());
  const full = applyDelta(immutable(parseMessageValue(snapshot()) as Snapshot), parseMessageValue(removal) as Delta)!;
  observer.enqueue(first); await observer.settled(); assert.deepEqual(observer.latest(), full);
  const second = compact(canonicalJSON(full), { ...structuredClone(removal), entities: [], removed: { trainIds: [], stationIds: [], buildingIds: [], trackPositions: [], entityIds: [] } } as Delta);
  observer.enqueue(second); await observer.settled();
  assert.deepEqual(observer.latest()!.mixed!.city!.heat, literal.mixed.city.heat);
  assert.deepEqual(first.mixed!.city!.heat.q, []);
  const changed = structuredClone(removal) as Delta;
  const changedObserver = await connected();
  changed.mixed!.city!.heat = structuredClone(literal.mixed.city.heat); changed.mixed!.city!.heat.q[0] += 1;
  changedObserver.enqueue(changed); await changedObserver.settled();
  assert.equal(changedObserver.latest()!.mixed!.city!.heat.q[0], literal.mixed.city.heat.q[0] + 1);
});

Deno.test('observer rejects wrong bases, malformed heat references and missing predecessors without repair or resync', async () => {
  for (const patch of [
    (d: Delta) => { d.baseRevision = '999'; },
    (d: Delta) => { d.cityHeatBaseRevision = '999'; },
    (d: Delta) => { d.mixed!.city!.heat.horizontalSize++; },
    (d: Delta) => { d.mixed!.city!.heat.q.push(1); },
    (d: Delta) => { delete d.cityHeatBaseRevision; },
    (d: Delta) => { d.worldEpoch = '99'; },
  ]) {
    const observer = await connected(), delta = compact(snapshot()); patch(delta); observer.enqueue(delta);
    await assert.rejects(() => observer.settled(), /Session decoder rejection/);
    observer.enqueue(snapshot()); await assert.rejects(() => observer.settled(), /Session decoder rejection/);
    observer.beginConnection(); observer.enqueue(join()); await assert.rejects(() => observer.settled(), /Session decoder rejection/);
  }
  const missing = new SessionObservation(); missing.beginConnection(); missing.enqueue(compact(snapshot()));
  await assert.rejects(() => missing.settled(), /invalid delta base/);
});

Deno.test('observer resets state, receipts and identity on reconnect and ignores old socket messages', async () => {
  const observer = await connected(), generation = observer.generation;
  observer.enqueue({ type: 'accepted', requestId: 'old', worldEpoch: '1', revision: '1', tick: '1', sequence: '1' }); await observer.settled();
  observer.close(generation); assert.equal(observer.latest(), undefined); assert.equal(observer.ready, false);
  observer.beginConnection(); assert.equal(observer.latest(), undefined); assert.equal(observer.receipt('old'), undefined);
  observer.enqueue(join(), generation); await observer.settled(); assert.equal(observer.latest(), undefined);
  const next = snapshot(); next.worldEpoch = '2';
  observer.enqueue(join(next, 'builder')); await observer.settled();
  assert.equal(observer.identity!.participantId, 'builder'); assert.equal(observer.latest()!.worldEpoch, '2'); assert(observer.ready);
});

Deno.test('world replacement replaces the predecessor and rejects deltas from the old world', async () => {
  const observer = await connected(), next = snapshot(); next.worldEpoch = '2';
  observer.enqueue(next); await observer.settled(); assert.deepEqual(observer.latest(), next);
  observer.enqueue(compact(snapshot())); await assert.rejects(() => observer.settled(), /invalid delta base/);
});

Deno.test('legacy histories process reconnect joins before enqueueing their successors', async () => {
  const observer = new SessionObservation(true); observer.beginConnection();
  const next = snapshot(); next.worldEpoch = '2';
  const delta = compact(next); delta.worldEpoch = '2';
  for (const message of [join(), join(next), delta]) { observer.enqueue(message); await observer.settled(); }
  assert.equal(observer.generation, 2); assert.equal(observer.latest()!.worldEpoch, '2');
  assert.equal(observer.latest()!.revision, delta.revision);
});

Deno.test('chunk digest completion is ordered before the following delta, publishing no partial snapshot', async () => {
  const observer = new SessionObservation(); observer.beginConnection(); const parts = await chunks(join());
  for (const part of parts.slice(0, -1)) observer.enqueue(part);
  await observer.settled(); assert.equal(observer.latest(), undefined); assert.equal(observer.ready, false);
  observer.enqueue(parts.at(-1)!); observer.enqueue(compact(snapshot())); await observer.settled();
  assert.equal(observer.latest()!.revision, String(BigInt(literal.revision) + 1n)); assert(observer.ready);
});

Deno.test('a digest from an abandoned connection cannot replace the new connection state', async () => {
  const observer = new SessionObservation(), parts = await chunks(join()); observer.beginConnection();
  for (const part of parts.slice(0, -1)) observer.enqueue(part);
  await observer.settled(); observer.enqueue(parts.at(-1)!);
  // Let the final frame start its digest, then replace this connection.
  await Promise.resolve(); observer.beginConnection(); const next = snapshot(); next.worldEpoch = '2';
  observer.enqueue(join(next)); await observer.settled(); assert.equal(observer.latest()!.worldEpoch, '2');
});

Deno.test('invalid transfers reject duplicate chunks, corruption and interruption promptly', async () => {
  const parts = await chunks(join());
  for (const packets of [[parts[0], parts[0]], parts.map(p => ({ ...p, sha256: '0'.repeat(64) })), [parts[0], snapshot()]]) {
    const observer = new SessionObservation(); observer.beginConnection(); packets.forEach(packet => observer.enqueue(packet));
    await assert.rejects(() => observer.settled(), /Session decoder rejection/);
  }
});

Deno.test('receipts correlate by request ID rather than timing or position and history stays bounded', async () => {
  const observer = await connected();
  observer.enqueue({ type: 'rejected', requestId: 'no', code: 'notAuthorized', arguments: {} });
  await observer.settled(); assert.equal(observer.receipt('no')?.type, 'rejected');
  for (let n = 0; n < 300; n++) {
    observer.enqueue({ type: 'accepted', requestId: `r${n}`, worldEpoch: '1', revision: String(n), tick: '1', sequence: String(n) });
    await observer.settled();
  }
  assert.equal(observer.receipt('r299')?.type, 'accepted'); assert.equal(observer.receipt('absent'), undefined);
  assert.equal(observer.history.length, 100); assert.equal(observer.latest()!.revision, literal.revision);
});
