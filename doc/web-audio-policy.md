# Web audio scope and future BGM project

**Owner decision — 27 October 2026:** remove WebAudio TinySynth and defer
background music (BGM) to a future improvement project. The browser MIDI
experiment demonstrated feasibility, but listening exposed pops and crackles.
Its automated playback/timing checks did not establish acceptable sound quality.

BGM playback, track selection, Silence/Stop controls and MIDI synthesis are no
longer P2/P9 deliverables or P0–P11 parity exit requirements. The web app ships
without a BGM player or synthesizer. Existing station-event sound and all six
bundled departure-bell previews remain supported, with browser gesture gating,
local playback state and no simulation/save authority. Bell previews still do
not implement station configuration or departure simulation.

Keep the existing MIDI compositions, contribution identities, translations,
source hashes, credits and notices as research/source material. Their presence
in prepared catalogs does not imply playback support. The pinned source reviews
continue to describe historical behavior; this owner decision supersedes their
BGM implementation requirements. A4 integration and external user-file music
retain their existing exclusion.

## Future improvement project

There is no requirement to use MIDI files or runtime MIDI synthesis for music.
Consider digital audio through the same browser playback facilities as sound
effects. The existing MIDI music may be rendered, rearranged or reimagined into
waveform recordings. A direct MIDI rendition or exact synthesizer timbre is not
a fidelity requirement for that project.

Choose the music assets and playback format in that project. The existing
POST-ASSETS plan for FLAC masters and Opus playback is a candidate shared audio
pipeline, not a requirement to implement or convert BGM now. Recheck browser and
OS support, licensing and delivery cost when choosing formats; preserve the
composition's attribution and document any new arrangement, performance,
rendering tools/sound-bank rights and derivative notices.

Before shipping, review listening quality for pops/crackles, clipping, gain,
transients, intentional silence and loop transitions. Validate gesture start,
stop/mute/volume, loading/cancellation/failure recovery, hidden-page/session
behavior, concurrent sound effects and supported browsers/devices. Keep music
local to each player and independent of simulation time and saves. Define any
new arrangements and loop behavior explicitly instead of treating old MIDI event
or timing details as mandatory runtime semantics.

The [P2 acceptance record](web-parity-p2-acceptance.md) retains the historical
experiment and its evidence. This decision neither closes the overall P2 gate
nor changes the existing native/device, rendering, optional-suite or POST-ASSETS
handoffs.

## Removal verification — 27 October 2026

`deno task --cwd web test` passes all 99 client cases. The production build,
generated-contract/content checks, localization checks and Svelte diagnostics
pass with zero errors or warnings. Two focused canonical Chromium cases verify
all six bell decodes/playback, gesture gating, English/Japanese controls, no
MIDI/synth requests, hash rejection/retry, cancellation on panel close and
preserved canvas/world state. These checks do not claim native audio fidelity
or close any existing browser/device handoff.

Focused browser command (from `web/`, using isolated local test ports):

```sh
FREETRAIN_ONLINE_TEST_SERVER_PORT=5480 FREETRAIN_ONLINE_TEST_WEB_PORT=4573 \
  node node_modules/@playwright/test/cli.js test --project canonical \
  --grep 'bundled bells|bell hash'
```
