import { sha256, type Media } from './content-recipes.ts';

export interface MidiSynth {
  freetrainTimer: ReturnType<typeof setInterval>;
  getAudioContext(): AudioContext;
  setMasterVol(value: number): void;
  setLoop(value: number): void;
  loadMIDI(bytes: ArrayBuffer): void;
  playMIDI(): void;
  stopMIDI(): void;
  getPlayStatus(): { play: number; maxTick: number; curTick: number };
}
declare global { interface Window { WebAudioTinySynth?: new (options: { quality: number; useReverb: number; voices: number }) => MidiSynth } }
let library: Promise<void> | undefined;
export function loadMidiSynth(): Promise<void> {
  if (window.WebAudioTinySynth) return Promise.resolve();
  return library ??= new Promise<void>((resolve, reject) => {
    const script = document.createElement('script');
    script.src = '/vendor/webaudio-tinysynth/webaudio-tinysynth.js';
    script.onload = () => window.WebAudioTinySynth ? resolve() : reject(new Error('MIDI synthesis unavailable'));
    script.onerror = () => { script.remove(); library = undefined; reject(new Error('MIDI synthesis unavailable')); };
    document.head.append(script);
  });
}

export type PlaybackState = 'stopped' | 'loading' | 'playing' | 'failed';
export function createMidiPlayer(
  changed: (state: PlaybackState) => void,
  create = () => new window.WebAudioTinySynth!({ quality: 1, useReverb: 0, voices: 64 }),
  fetchMedia = async (media: Media, signal: AbortSignal) => {
    if (!media.url.startsWith('/content/bundled-v2/') || /[?#%\\]|(^|\/)\.\.?($|\/)/.test(media.url)) throw new Error('Invalid music URL');
    const response = await fetch(media.url, { signal });
    if (!response.ok) throw new Error('Music unavailable');
    const bytes = new Uint8Array(await response.arrayBuffer());
    if (bytes.length !== media.bytes || await sha256(bytes) !== media.sha256) throw new Error('Music hash mismatch');
    return bytes.buffer;
  },
) {
  let synth: MidiSynth | undefined, generation = 0, disposed = false;
  let request: AbortController | undefined, monitor: ReturnType<typeof setInterval> | undefined;
  function stop() {
    generation++; request?.abort(); request = undefined;
    clearInterval(monitor); monitor = undefined;
    synth?.stopMIDI();
    if (!disposed) changed('stopped');
  }
  return {
    // Invoke directly from a trusted gesture after loadMidiSynth resolves.
    async play(media: Media): Promise<void> {
      if (disposed) return;
      stop(); const current = generation;
      const controller = request = new AbortController(); changed('loading');
      try {
        synth ??= create();
        synth.setMasterVol(0.35); synth.setLoop(1);
        // Resume synchronously before the first await, preserving user activation.
        const resumed = synth.getAudioContext().resume();
        const [bytes] = await Promise.all([fetchMedia(media, controller.signal), resumed]);
        if (disposed || current !== generation) return;
        // Only exact catalog-pinned bundled MIDI reaches the upstream parser.
        synth.loadMIDI(bytes);
        if (!synth.getPlayStatus().maxTick) throw new Error('Empty MIDI');
        synth.playMIDI(); changed('playing');
        monitor = setInterval(() => { if (!synth?.getPlayStatus().play) stop(); }, 250);
      } catch {
        if (!disposed && current === generation) { stop(); changed('failed'); }
      }
    },
    stop,
    dispose() {
      stop(); disposed = true;
      if (synth) { clearInterval(synth.freetrainTimer); void synth.getAudioContext().close(); synth = undefined; }
    },
  };
}
