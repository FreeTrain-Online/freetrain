import type { DepartureBell, SoundEvent } from '../generated/contracts.ts';
import { bundledBells } from './bundled-audio.ts';
import { loadRenderCatalog, sha256 } from './content-recipes.ts';

type Clip = { url: string; bytes: number; sha256: string };
let bellCatalog: ReturnType<typeof loadRenderCatalog> | undefined;
async function departureClip(event: DepartureBell): Promise<Clip> {
  const catalog = await (bellCatalog ??= loadRenderCatalog().catch(error => { bellCatalog = undefined; throw error; }));
  if (event.bell.packId !== catalog.packId || event.bell.recipeId !== event.bell.contributionId) throw new Error('Unknown departure bell');
  const bell = bundledBells(catalog).find(b => b.id === event.bell.contributionId);
  if (!bell) throw new Error('Unknown departure bell');
  return bell.media;
}

// Local effects use a gesture-unlocked media element. Async loads are cancelled
// on mute/replacement/disconnect; no delayed sound is carried into another world.
export function createBrowserAudio(create = () => new Audio('/assets/station.wav'), hidden = () => document.hidden,
  visible: (event: DepartureBell) => boolean = () => true,
  resolveClip = departureClip,
  verifyClip = async (clip: Clip) => {
    const response = await fetch(clip.url); if (!response.ok) throw new Error('Bell unavailable');
    const bytes = new Uint8Array(await response.arrayBuffer());
    if (bytes.length !== clip.bytes || await sha256(bytes) !== clip.sha256) throw new Error('Bell hash mismatch');
  }) {
  let audio: HTMLAudioElement | undefined;
  let enabled = false, disposed = false, generation = 0, enabling = 0;
  const seen = new Set<string>();
  const play = async (url = '/assets/station.wav') => {
    if (disposed) return false;
    const current = generation;
    try {
      audio ??= create(); audio.src = url; audio.preload = 'auto'; audio.currentTime = 0;
      await audio.play(); return true;
    } catch { if (current === generation) enabled = false; return false; }
  };
  return {
    // Called directly from the sound button's trusted user gesture; previews WAV.
    async enable() {
      const attempt = ++enabling, current = generation;
      enabled = true; const result = await play();
      if (attempt === enabling && current === generation) enabled = result;
      return enabled;
    },
    disable() { generation++; enabling++; enabled = false; audio?.pause(); },
    cancel() { generation++; audio?.pause(); },
    get enabled() { return enabled; },
    async event(event: SoundEvent | DepartureBell, worldEpoch: string) {
      if (disposed || event.worldEpoch !== worldEpoch) return;
      if (event.type === 'soundEvent') {
        if (seen.has(event.eventId)) return;
        seen.add(event.eventId); if (seen.size > 128) seen.delete(seen.values().next().value!);
        if (enabled && !hidden()) await play();
        return;
      }
      // Departure identity has already been consumed by the live subscription.
      if (!enabled || hidden() || !visible(event)) return;
      const current = generation;
      try {
        const clip = await resolveClip(event); await verifyClip(clip);
        if (current === generation && enabled && !hidden() && visible(event)) await play(clip.url);
      } catch { /* Media failure cannot affect the authoritative simulation. */ }
    },
    dispose() { disposed = true; generation++; enabling++; enabled = false; audio?.pause(); if (audio) audio.src = ''; seen.clear(); },
  };
}
