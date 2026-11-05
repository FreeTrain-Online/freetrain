import { applyDelta, parseMessage } from '../src/lib/protocol.ts';
import fixture from '../../prototype/protocol/fixtures/snapshot.json' with { type: 'json' };
import removal from '../../prototype/protocol/fixtures/delta-removal.json' with { type: 'json' };
import type { Delta, Snapshot } from '../src/generated/contracts.ts';
import {sessionCloseReason} from '../src/lib/session-close.ts';
function assert(condition: unknown): asserts condition { if (!condition) throw new Error('Assertion failed'); }
Deno.test('delta applies complete scalar/entity/track changes immutably with explicit removals', () => {
  const state = structuredClone(fixture) as Snapshot;
  const changed = { ...removal, cash: '9007199254740993', paused: false,
    tracks: [{ position: { x: 23, y: 16, z: 1 }, revision: '1', shape: 'buffer' }],
    removed: { ...removal.removed, trackPositions: [state.tracks[0].position], trainIds: [state.trains[0].id] },
  } as Delta;
  const result = applyDelta(state, changed)!;
  assert(result.revision === '1' && result.cash === '9007199254740993' && !result.paused);
  assert(result.trains.length === 0 && result.buildings.length === state.buildings.length - 1);
  assert(!result.tracks.some(t => JSON.stringify(t.position) === JSON.stringify(state.tracks[0].position)));
  assert(result.tracks.some(t => t.position.x === 23 && t.shape === 'buffer'));
  assert(Object.isFrozen(result) && Object.isFrozen(result.tracks));
  assert(state.revision === '0' && state.trains.length === 1);
});
Deno.test('missing base, wrong epoch, and nonadvancing delta require snapshot resync', () => {
  const state = structuredClone(fixture) as Snapshot;
  for (const patch of [{ baseRevision: '99' }, { worldEpoch: '2' }, { revision: '0' }]) {
    assert(applyDelta(state, { ...removal, ...patch } as Delta) === undefined);
  }
});
Deno.test('runtime wire validation rejects numeric loss, unknown fields, and unsupported messages', () => {
  for (const value of [{ ...fixture, cash: 9007199254740992 }, { ...fixture, extra: true }, { type: 'join', protocolVersion: 2 }, { ...removal, cash: '9223372036854775808' }]) {
    let rejected = false; try { parseMessage(JSON.stringify(value)); } catch { rejected = true; }
    assert(rejected);
  }
  assert(parseMessage(JSON.stringify(fixture)).type === 'snapshot');
  assert(parseMessage(JSON.stringify(removal)).type === 'delta');
});
Deno.test('rejected packet diagnostics identify the schema field without including payload values',()=>{
 const value={type:'accepted',requestId:'DO_NOT_ECHO_REQUEST_CONTENT',worldEpoch:1,sequence:'1',tick:'0',revision:'0'};
 let reason='';try{parseMessage(JSON.stringify(value));}catch(error){reason=(error as Error).message;}
 assert(reason.includes('(accepted)')&&reason.includes('/worldEpoch')&&reason.includes('string'));
 assert(!reason.includes(value.requestId));
 for(const type of ['constructor','toString','DO_NOT_ECHO_REQUEST_CONTENT']) {
  let rejected='';try{parseMessage(JSON.stringify({type}));}catch(error){rejected=(error as Error).message;}
  assert(rejected==='Invalid protocol message');
 }
});
Deno.test('session close reasons stay within the browser UTF-8 limit and preserve complete characters',()=>{
 for(const source of ['ASCII '.repeat(100),'列車'.repeat(100),'🚆'.repeat(100),'reason\nwith\rcontrols\tend']) {
  const reason=sessionCloseReason(source);assert(new TextEncoder().encode(reason).length<=123);
  assert(!/[\r\n\t\uFFFD]/u.test(reason));
  assert(new TextDecoder('utf-8',{fatal:true}).decode(new TextEncoder().encode(reason))===reason);
 }
 assert(sessionCloseReason('commandAcknowledgementTimeout/controllerInput')==='commandAcknowledgementTimeout/controllerInput');
});
