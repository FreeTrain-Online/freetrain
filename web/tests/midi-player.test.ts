import assert from 'node:assert/strict';
import vm from 'node:vm';
import { createMidiPlayer, type MidiSynth, type PlaybackState } from '../src/lib/midi-player.ts';
import { bundledAudio } from '../src/lib/bundled-audio.ts';
import { sha256, type RenderCatalog } from '../src/lib/content-recipes.ts';
const catalog: RenderCatalog = JSON.parse(await Deno.readTextFile(new URL('../public/content/bundled-v2/catalog.json', import.meta.url)));
const tracks = bundledAudio(catalog), media = tracks.find(t => t.kind === 'music')!.media;
Deno.test('bundled synthesizer and license match their recorded source and local patch provenance', async () => {
  const root=new URL('../public/vendor/webaudio-tinysynth/',import.meta.url);
  const provenance=JSON.parse(await Deno.readTextFile(new URL('provenance.json',root)));
  assert.equal(await sha256(await Deno.readFile(new URL('webaudio-tinysynth.js',root))),provenance.distributedSha256);
  assert.equal(await sha256(await Deno.readFile(new URL('LICENSE.txt',root))),provenance.files.LICENSE);
  assert.equal(await sha256(await Deno.readFile(new URL('README.md',root))),provenance.files['README.md']);
});
function fixture(fetcher: (media:typeof media,signal:AbortSignal)=>Promise<ArrayBuffer> = async () => new ArrayBuffer(1)) {
  let created=0,plays=0,stops=0,resumes=0,closed=0,playing=0;
  const states:PlaybackState[]=[];
  const synth={freetrainTimer:undefined, getAudioContext:()=>({resume(){resumes++;return Promise.resolve();},close(){closed++;return Promise.resolve();}}),
    setMasterVol(){},setLoop(){},loadMIDI(){},playMIDI(){plays++;playing=1;},stopMIDI(){stops++;playing=0;},getPlayStatus:()=>({play:playing,maxTick:100,curTick:0})} as unknown as MidiSynth;
  const player=createMidiPlayer(s=>states.push(s),()=>{created++;return synth;},fetcher);
  return {player,states,get created(){return created;},get plays(){return plays;},get resumes(){return resumes;},get closed(){return closed;},get stops(){return stops;}};
}
Deno.test('bundled audio binds all three source MIDI tracks and six bells without substituting resources', () => {
  assert.equal(tracks.filter(t=>t.kind==='music').length,3);
  assert.equal(tracks.filter(t=>t.kind==='bell').length,6);
  assert.equal(new Set(tracks.map(t=>t.id)).size,9);
  const missing=structuredClone(catalog);missing.assets=[];
  assert.throws(()=>bundledAudio(missing));
});
Deno.test('MIDI starts only on play and resumes its context before asynchronous media loading', async () => {
  const f=fixture();assert.equal(f.created,0);
  const playing=f.player.play(media);assert.equal(f.resumes,1);
  await playing;assert.equal(f.plays,1);assert.equal(f.states.at(-1),'playing');
  f.player.stop();assert.equal(f.states.at(-1),'stopped');
  f.player.dispose();assert.equal(f.closed,1);
  await f.player.play(media);assert.equal(f.plays,1);
});
Deno.test('stop and replacement fence late MIDI loads even when transport ignores abort', async () => {
  const releases:((b:ArrayBuffer)=>void)[]=[],signals:AbortSignal[]=[];
  const f=fixture((_media,signal)=>{signals.push(signal);return new Promise(r=>releases.push(r));});
  const old=f.player.play(media),newer=f.player.play(media);
  assert.equal(signals[0].aborted,true);
  releases[1](new ArrayBuffer(1));await newer;assert.equal(f.plays,1);
  releases[0](new ArrayBuffer(1));await old;assert.equal(f.plays,1);
  const stopped=f.player.play(media);f.player.stop();releases[2](new ArrayBuffer(1));await stopped;
  assert.equal(f.plays,1);f.player.dispose();
});
Deno.test('failed media never plays and a later gesture can retry', async () => {
  let fail=true;const f=fixture(async()=>{if(fail)throw new Error('unavailable');return new ArrayBuffer(1);});
  await f.player.play(media);assert.equal(f.plays,0);assert.equal(f.states.at(-1),'failed');
  fail=false;await f.player.play(media);assert.equal(f.plays,1);f.player.dispose();
});
Deno.test('default media loader rejects altered bytes and foreign URLs before invoking MIDI parser', async () => {
  const original=globalThis.fetch;globalThis.fetch=async()=>new Response(new Uint8Array(media.bytes));
  const states:PlaybackState[]=[];let parses=0;
  const synth={getAudioContext:()=>({resume:async()=>{},close:async()=>{}}),setMasterVol(){},setLoop(){},stopMIDI(){},loadMIDI(){parses++;}} as unknown as MidiSynth;
  const player=createMidiPlayer(s=>states.push(s),()=>synth);
  try {
    await player.play(media);assert.equal(states.at(-1),'failed');
    await player.play({...media,url:'https://example.org/music.mid'});assert.equal(states.at(-1),'failed');assert.equal(parses,0);
  } finally {player.dispose();globalThis.fetch=original;}
});
Deno.test('vendored MIDI timing retains exact tempo, trailing silence and the same duration on successive loops', async () => {
  const source=await Deno.readTextFile(new URL('../public/vendor/webaudio-tinysynth/webaudio-tinysynth.js',import.meta.url));
  let schedule=()=>{};
  const sandbox:Record<string,any>={setInterval:(callback:()=>void)=>{schedule=callback;return 1;}};
  vm.runInNewContext(source.replace('})(this);','window.testCore=WebAudioTinySynthCore; })(this);'),sandbox);
  const synth:Record<string,any>={internalcontext:0};sandbox.testCore.call(synth,synth);synth.init();
  const events:number[][]=[];
  synth.actx={currentTime:0};synth.reset=()=>{};synth.stopMIDI=()=>{};synth.locateMIDI=()=>{};synth.send=(message:number[],at:number)=>{if(message[0]===0x90)events.push([message[1],at]);};
  // One note, 96 PPQN, 500001 microseconds/quarter, two-quarter track with
  // one quarter of authored silence after the note-off. No synthesis needed.
  const track=[0,0xff,0x51,3,7,0xa1,0x21,0,0x90,60,100,96,0x80,60,0,96,0xff,0x2f,0];
  const midi=new Uint8Array([77,84,104,100,0,0,0,6,0,0,0,1,0,96,77,84,114,107,0,0,0,track.length,...track]);
  synth.loadMIDI(midi.buffer);assert.equal(synth.maxTick,192);
  assert.equal(synth.song.ev[0].m[1],60000000/500001);
  assert.equal(synth.song.ev.at(-1).t,192);
  synth.loop=1;synth.playing=1;synth.playIndex=0;synth.playTick=0;synth.playTime=.1;synth.tick2Time=.5/96;
  schedule();synth.actx.currentTime=.5;schedule();synth.actx.currentTime=1;schedule();
  synth.actx.currentTime=1.5;schedule();synth.actx.currentTime=2;schedule();
  assert.equal(events.length,3);
  for(let i=0;i<3;i++)assert.ok(Math.abs(events[i][1]-(.1+i*1.000002))<1e-9);
});
