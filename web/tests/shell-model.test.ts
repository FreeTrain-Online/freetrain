import { createFixtureService, type ReadState, type SessionClient } from '../src/lib/session-client.ts';
import { createShellModel } from '../src/lib/shell-model.ts';
import type { Accepted, Snapshot } from '../src/generated/contracts.ts';

function assert(condition: unknown, message = 'Assertion failed'): asserts condition {
  if (!condition) throw new Error(message);
}
async function until(predicate: () => boolean) {
  const deadline = Date.now() + 2000;
  while (!predicate()) {
    if (Date.now() > deadline) throw new Error('Timed out');
    await new Promise(resolve => setTimeout(resolve, 2));
  }
}
function setup() {
  const service = createFixtureService({ normal: 2, delayed: 40 });
  const model = createShellModel(() => service.connect(), 'en', 1);
  return { service, model };
}

Deno.test('delayed confirmation leaves confirmed state unchanged and blocks duplicate submission', async () => {
  const { service, model } = setup();
  try {
    await until(() => model.state.connection === 'ready');
    service.setBehavior('delayed');
    const pending = model.send({ kind: 'step' });
    assert(model.state.pending && model.state.read?.tick === '0');
    await model.send({ kind: 'step' });
    await pending;
    assert(model.state.read?.tick === '1' && !model.state.pending);
  } finally { model.dispose(); }
});

Deno.test('rejection retains Japanese draft, confirmed name, and train controller', async () => {
  const { service, model } = setup();
  try {
    await until(() => model.state.connection === 'ready');
    service.setBehavior('rejected');
    model.setDraft('新宿駅');
    await model.rename();
    await model.controller('manual');
    assert(model.state.draft === '新宿駅' && model.state.read?.stations[0].name === '中央駅');
    assert(model.state.read?.trains[0].controller === 'automatic' && model.state.feedback === 'invalidInput');
  } finally { model.dispose(); }
});

Deno.test('incoming snapshots preserve dirty draft, IME composition and catalog selection', async () => {
  const { service, model } = setup();
  try {
    await until(() => model.state.connection === 'ready');
    model.setDraft('編集中'); model.setComposing(true); model.selectBuilding('catalog-5');
    model.selectCatalogItem('signal','{6FA55842-E693-414f-A120-0A3D0C613B21}',6);
    const next = structuredClone(model.state.read) as Snapshot;
    next.stations[0].name = '別の駅'; next.stations[0].revision = '1';
    next.trains[0].name = 'Updated train'; service.publish(next);
    await model.rename();
    assert(model.state.draft === '編集中' && model.state.composing && !model.state.pending);
    assert(model.state.selectedBuilding === 'catalog-5' && model.state.read?.trains[0].name === 'Updated train');
    assert(model.state.signalDirection===6 && model.state.catalogSelections.signal==='{6FA55842-E693-414f-A120-0A3D0C613B21}');
    model.setComposing(false); model.resetDraft();
    assert(model.state.draft === '別の駅');
  } finally { model.dispose(); }
});

Deno.test('editing again during pending rename retains the newer draft', async () => {
  const { service, model } = setup();
  try {
    await until(() => model.state.connection === 'ready');
    service.setBehavior('delayed'); model.setDraft('確定する駅');
    const pending = model.rename(); model.setDraft('次の下書き'); await pending;
    assert(model.state.read?.stations[0].name === '確定する駅' && model.state.draft === '次の下書き');
  } finally { model.dispose(); }
});

Deno.test('reconnect cancels pending fixture changes and keeps exactly one subscription', async () => {
  const { service, model } = setup();
  try {
    await until(() => model.state.connection === 'ready');
    model.setDraft('保存した駅'); await model.rename();
    service.setBehavior('delayed');
    const pending = model.send({ kind: 'step' });
    model.reconnect(); await pending;
    for (let i = 0; i < 20; i++) { model.reconnect(); await until(() => model.state.connection === 'ready'); }
    assert(service.diagnostics.connections === 1 && service.diagnostics.subscriptions === 1);
    assert(model.state.read?.tick === '0' && model.state.read.stations[0].name === '保存した駅');
  } finally { model.dispose(); }
  assert(service.diagnostics.connections === 0 && service.diagnostics.subscriptions === 0);
});

Deno.test('late result from a disposed connection cannot change the new connection feedback', async () => {
  const service = createFixtureService({ normal: 1, delayed: 1 });
  let complete: (value: Accepted) => void = () => {};
  const model = createShellModel((): SessionClient => {
    const client = service.connect();
    return { ...client, send: () => new Promise(resolve => { complete = resolve; }) };
  }, 'en', 1);
  try {
    await until(() => model.state.connection === 'ready');
    const pending = model.send({ kind: 'step' }); model.reconnect();
    await until(() => model.state.connection === 'ready');
    complete({ type: 'accepted', requestId: 'old', worldEpoch: '1', sequence: '1', revision: '1', tick: '1' });
    await pending;
    assert(model.state.feedback === 'idle' && !model.state.pending && model.state.read?.tick === '0');
  } finally { model.dispose(); }
});

Deno.test('failed connection can recover and disposal cancels scheduled reconnect', async () => {
  const service = createFixtureService(); let fail = true;
  const model = createShellModel(() => { if (fail) throw new Error('offline'); return service.connect(); }, 'ja', 1);
  await until(() => model.state.connection === 'disconnected');
  assert(model.state.feedback === 'unavailable');
  fail = false; model.reconnect(); await until(() => model.state.connection === 'ready');
  model.reconnect(); model.dispose();
  await new Promise(resolve => setTimeout(resolve, 10));
  assert(service.diagnostics.connections === 0);
});

Deno.test('fixture snapshots are immutable; stale revisions reject without publishing', async () => {
  const service = createFixtureService({ normal: 1, delayed: 1 });
  const client = service.connect(); let read: ReadState | undefined; let updates = 0;
  const unsubscribe = client.subscribe(next => { read = next; updates++; });
  try {
    assert(Object.isFrozen(read) && Object.isFrozen(read!.stations[0]));
    const result = await client.send({ type: 'command', requestId: 'stale', expectedWorldEpoch: '1', payload: { kind: 'renameStation', stationId: 'station-1', expectedRevision: '42', name: '駅' } });
    assert(result.type === 'rejected' && result.code === 'staleRevision' && updates === 1);
    unsubscribe(); assert(service.diagnostics.subscriptions === 0);
  } finally { client.dispose(); }
});

Deno.test('controller changes acquire a lease and use the newly confirmed revision and token', async () => {
  const { model } = setup();
  try {
    await until(() => model.state.connection === 'ready');
    await model.controller('manual');
    assert(model.state.read?.trains[0].controller === 'manual');
    assert(model.state.read.trains[0].controlLease?.participantId === model.state.participantId);
    assert(model.state.read.trains[0].controlLease?.leaseId === 'fixture-lease');
    await model.releaseControl();
    assert(model.state.read.trains[0].controlLease === null);
  } finally { model.dispose(); }
});

Deno.test('leave cancels pending work and clears client state before returning to sign-in', async () => {
  const service = createFixtureService({ normal: 2, delayed: 100 });
  let signedOut = false;
  const model = createShellModel((): SessionClient => signedOut ? {
    mode: 'remote', subscribe: () => () => {},
    subscribeStatus(listener) { listener('accessRequired'); return () => {}; },
    send: () => Promise.reject(new Error('Signed out')), dispose() {},
  } : service.connect(), 'en', 1);
  try {
    await until(() => model.state.connection === 'ready');
    service.setBehavior('delayed'); model.setDraft('Local draft');
    const pending = model.send({ kind: 'step' });
    await model.leave(async () => { signedOut = true; }); await pending;
    await until(() => model.state.connection === 'accessRequired');
    assert(service.diagnostics.connections === 0 && service.diagnostics.subscriptions === 0);
    assert(!model.state.read && !model.state.role && !model.state.participantId);
    assert(!model.state.pending && !model.state.leaving && model.state.draft === '');
  } finally { model.dispose(); }
});

Deno.test('failed leave stays disconnected and can be retried without reconnecting mid-request', async () => {
  const { service, model } = setup();
  let finish: () => void = () => {};
  try {
    await until(() => model.state.connection === 'ready');
    await model.leave(() => Promise.reject(new Error('Network failure')));
    assert(model.state.feedback === 'leaveFailed' && model.state.connection === 'disconnected');
    assert(!model.state.read && service.diagnostics.connections === 0);
    model.reconnect();
    await new Promise(resolve => setTimeout(resolve, 10));
    assert(model.state.feedback === 'leaveFailed' && service.diagnostics.connections === 0);
    const retry = model.leave(() => new Promise(resolve => { finish = resolve; }));
    model.reconnect();
    assert(model.state.leaving && service.diagnostics.connections === 0);
    finish(); await retry;
    await until(() => model.state.connection === 'accessRequired');
    assert(!model.state.leaving && model.state.signedOut && !model.state.read);
    model.connectAuthorized();
    await until(() => model.state.connection === 'ready');
    assert(!model.state.signedOut);
  } finally { model.dispose(); }
});

Deno.test('selected station retains its original revision and never renames another station', async () => {
  const { service, model } = setup();
  try {
    await until(() => model.state.connection === 'ready');
    const snapshot = structuredClone(model.state.read) as Snapshot;
    snapshot.stations.push({ ...snapshot.stations[0], id: 'station-2', name: 'Second station' });
    service.publish(snapshot);
    model.selectStation('station-2'); model.setDraft('下書き');
    snapshot.stations[1].revision = '1'; snapshot.stations[1].name = 'Changed by peer'; service.publish(snapshot);
    await model.rename();
    assert(model.state.feedback === 'staleRevision' && model.state.draft === '下書き');
    assert(model.state.read!.stations[0].name === '中央駅');
    model.resetDraft(); model.setDraft('二番目'); await model.rename();
    assert(model.state.read!.stations[1].name === '二番目');
    model.selectStation('missing'); model.setDraft('Never retarget');
    assert(model.rename() === undefined && model.state.read!.stations[0].name === '中央駅');
  } finally { model.dispose(); }
});

Deno.test('controller lease acquisition remains bound to the selected train across async completion', async () => {
  const { service, model } = setup();
  try {
    await until(() => model.state.connection === 'ready');
    const snapshot = structuredClone(model.state.read) as Snapshot;
    snapshot.trains.push({ ...snapshot.trains[0], id: 'train-2', name: 'Second train' }); service.publish(snapshot);
    await model.controller('manual', 'train-2');
    assert(model.state.read!.trains[0].controller === 'automatic');
    assert(model.state.read!.trains[1].controller === 'manual');
    service.setBehavior('delayed');
    let selected = true;
    const switched = model.controller('manual', 'train-1', () => selected);
    selected = false; await switched;
    assert(model.state.read!.trains[0].controller === 'automatic');
    await model.releaseControl('train-1');
    const pending = model.controller('manual', 'train-1');
    model.reconnect(); await pending;
    await until(() => model.state.connection === 'ready');
    assert(model.state.read!.trains[0].controller === 'automatic');
  } finally { model.dispose(); }
});

Deno.test('late reads and statuses cannot reopen a leaving or deliberately signed-out world', async () => {
  const service = createFixtureService();
  let lateRead: ((read: ReadState) => void) | undefined;
  let lateStatus: ((status: 'ready') => void) | undefined;
  const model = createShellModel(() => {
    const client = service.connect();
    return { ...client,
      subscribe(listener: (read: ReadState) => void) { lateRead ??= listener; return client.subscribe(listener); },
      subscribeStatus(listener: (status: 'ready') => void) { lateStatus ??= listener; listener('ready'); return () => {}; },
    };
  }, 'en', 1);
  let finish: () => void = () => {};
  try {
    await until(() => model.state.connection === 'ready');
    const read = model.state.read!;
    const leaving = model.leave(() => new Promise(resolve => { finish = resolve; }));
    lateRead!(read); lateStatus!('ready');
    assert(model.state.leaving && !model.state.read && !model.state.role);
    finish(); await leaving;
    await until(() => model.state.connection === 'accessRequired');
    lateRead!(read); lateStatus!('ready');
    assert(model.state.signedOut && !model.state.read && model.state.connection === 'accessRequired');
  } finally { finish(); model.dispose(); }
});

Deno.test('same-socket synchronization preserves one pending mutation until acknowledgement or disconnect', async () => {
  const service = createFixtureService();
  let stage: 'bootstrap' | 'synchronizing' = 'bootstrap';
  let status: ((status: 'ready' | 'loading' | 'disconnected') => void) | undefined;
  let complete: (value: Accepted) => void = () => {};
  const model = createShellModel(() => {
    const client = service.connect();
    return { ...client, get connectionStage() { return stage; },
      subscribeStatus(listener: typeof status) { status = listener; listener!('ready'); return () => {}; },
      send: () => new Promise<Accepted>(resolve => { complete = resolve; }),
    };
  }, 'en', 1);
  try {
    await until(() => model.state.connection === 'ready');
    const pending = model.send({ kind: 'step' });
    stage = 'synchronizing'; status!('loading');
    assert(model.state.pending && model.state.connection === 'loading');
    status!('ready');
    assert(await model.send({ kind: 'step' }) === false && model.state.pending);
    status!('disconnected'); assert(!model.state.pending);
    complete({ type: 'accepted', requestId: 'old', worldEpoch: '1', sequence: '1', revision: '1', tick: '1' }); await pending;
  } finally { model.dispose(); }
});

Deno.test('dirty metadata retains target/revision across conflict and deletion until explicit Reset', async () => {
  const { service, model } = setup();
  try {
    await until(() => model.state.connection === 'ready');
    const station = model.state.read!.stations[0]; model.selectStation(station.id); model.setDraft('編集を保持');
    const next = structuredClone(model.state.read) as Snapshot;
    next.stations[0].revision = String(BigInt(station.revision) + 1n); next.stations[0].name = '別の編集'; service.publish(next);
    assert(model.state.draftDirty && model.state.draftStationId === station.id && model.state.draftRevision === station.revision);
    assert(model.state.draft === '編集を保持');
    next.stations = []; service.publish(next);
    assert(model.state.draftDirty && model.state.draftStationId === station.id && model.state.draft === '編集を保持');
    assert(model.rename() === undefined);
    model.resetDraft(); assert(!model.state.draftDirty && model.state.draft === '');
  } finally { model.dispose(); }
});

Deno.test('epoch replacement discards station drafts with an interruption notice and no replay', async () => {
  const { service, model } = setup();
  try {
    await until(() => model.state.connection === 'ready');
    model.selectStation(model.state.read!.stations[0].id); model.setDraft('旧ワールドの編集');
    const next = structuredClone(model.state.read) as Snapshot; next.worldEpoch = '2'; service.publish(next);
    assert(!model.state.draftDirty && model.state.draftInterrupted && !model.state.draftStationId);
    assert(model.state.read!.stations[0].name === '中央駅');
  } finally { model.dispose(); }
});
