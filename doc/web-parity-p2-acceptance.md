# P2 remaining acceptance — 27 October 2026

This pass implements bundled audio playback and scalable localization, and checks
the content/render/picking and optional-loading regressions. The final P2 phase
gate remains open for the existing native/device handoff and fidelity qualifications.
It does not reopen the owner's deferred optional-suite repairs or POST-ASSETS work.

## Delivered behavior

Settings exposes the three bundled BGM contributions and six departure bells,
bound by exact contribution identity to the prepared native MIDI/WAV bytes. Names
are presented in English/Japanese without editing original manifests or IDs.
Authors remain literal source attribution. Existing live station-event sound and
its event-ID/epoch/no-backlog rules stay in place; bell selection is a local preview,
not an implemented station-configuration or departure simulation.

MIDI uses the locally bundled [WebAudio TinySynth](https://github.com/g200kg/webaudio-tinysynth)
source at `3d75aee4b3f43cbd932265e7d60201fd5b770397`, with its Apache-2.0 license
linked from the application License dialog. Its algorithmic General MIDI timbres
need no sound bank, CDN, extra build dependency or network service. This is the
portable browser synthesis decision for P2; it does not promise the timbre of a
particular Windows DirectMusic installation. The repository retains original MIDI
and WAV bytes and notices. POST-ASSETS audio-master conversion is separate.

Source behavior comes from stable ExAV `core/framework/BGMManager.cs`,
`core/contributions/sound/BGMContribution.cs` and
`lib/DirectXWraper/DirectAudio.cpp`: selection replaces/stops the previous track,
silence stops it, and BGM loops. The browser adapter starts only from Play and
loops until Stop. Hiding/leaving the page or entering session recovery stops music
without resuming it automatically. Closing a Settings panel preserves local BGM;
it does not remount the map, change shared time or write music into a world/save.

The vendored patch records three bounded changes in `provenance.json`: expose the
scheduler for disposal, retain exact microsecond tempo instead of rounded BPM, and
preserve end-of-track silence plus default-tempo/leading-delay resets on each loop.
A source-independent MIDI fixture verifies successive loop durations and fractional
tempo. No custom sample bank or replacement music is introduced.

The adapter resumes its AudioContext directly in the user gesture, verifies the
catalog/media hashes before passing the bundled MIDI to the sequencer, fences late
loads with generation and abort state, and reports retryable localized failures.
A stopped/replaced/disposed request cannot start later. Bells likewise verify
bytes before decoding and release their AudioContext on completion/stop/unmount.
The general-purpose upstream file loader is not exposed to players.

[The localization guide](web-parity-localization.md) describes per-language app,
gallery and formatting files, a typed registry, common selectors, BCP 47 fallback,
HTML language/direction and `Intl` plural rules. Content wording has separate,
pack-owned source and locale records, with exact Japanese source/donor/hash/review
evidence. This pass supplies the nine audio names; it does not classify every
future gameplay candidate as translated.

## Decisions and remaining handoff

| Acceptance area | Disposition |
| --- | --- |
| Bundled MIDI approach/playback | Implemented with pinned local GM synthesis, gesture start, source looping, Stop, cancellation, error/retry and original notices. Actual notes/output and all bundled bells checked in Chromium. Native listening/timbre comparison remains unverified. |
| English/Japanese P2 presentation | Shell text preserved in separate catalogs; gallery and audio controls localized, including source-bound audio names, interpolation, plurals and narrow layout. Later gameplay text follows its owning feature and P11 reconciliation. |
| Source resources/fidelity | Carry forward P2-02's source dispositions and owner spot check. RGB888 is the explicit portable rendering choice; original-runtime/native 555/565 matching is not claimed. The 21 diagnosed crops are assigned to POST-ASSETS-07 by owner direction. |
| Complete rendered variants/picking | Automated gallery covers all 6,496 season/light combinations and actual alpha/shape/picking regressions. This is not a new exhaustive human/original-runtime fidelity review. Procedural terrain/stadium and world consumers retain P3/P7 and other gameplay owners. |
| Optional preparation/startup | Synthetic source and prepared flows, hashes/credits, dependency rejection and base-only restart remain covered. Full canonical V2 repairs and historical-suite acceptance are postponed in the separate distribution's `README-online.md`; no non-free content enters this build. |
| UX/native handoff | Existing UX3-14/17, A-03/A-14 and UX3-GATE remain deferred. Chromium's narrow/DPR emulation does not replace native device/browser/audio observations. Full-content capacity/performance targets are not closed by correctness tests. |

The owner-qualified P2-02/P2-03 checkboxes retain their recorded dispositions.
P2 is not promoted to full legacy-game parity by this pass. Remaining handoff is
recorded rather than inferred from automated fixtures or from other phases' work.

## Generated content and packaging

The three native MIDI descriptors now use `retained-midi`, replacing the obsolete
`playback-adapter-pending` label. No source/media/recipe parameters change. Catalog
schema/pack version remain 2; the live reference profile and Save 5 are unchanged.
Both generated catalogs and provenance are regenerated together. New catalog SHA-256:
`d8a407b6dd1827ff4caa13e4c4d8bfcba629a4c453ef74ec871a12f9467c7d21`.

The frontend Docker stage includes bundled localization evidence. `.dockerignore`
also now includes the XML/recipe/optional-preparation scripts already referenced by
the P2-03 Dockerfile, so a clean build receives them. Synthesizer bytes and its
served `LICENSE.txt` are included and checked against recorded hashes. Optional
sources, generated derivatives, credits and caches retain their external roots.

## Verification

Validation passes: **287 .NET / 105 client / 133 Python tests**, production frontend
and managed builds, generated content/contracts, pinned source regeneration,
localization validation, project boundaries and whitespace checks. The full
Chromium run passes **72 cases (18 canonical + 54 historical)**, including every
bundled recipe in four seasons and two light modes. Optional source/prepared runs
pass **three cases each**, with original source directories removed for the
prepared run. After the served-license filename correction, the three focused
music/gallery cases pass again on the final frontend, including narrow Japanese
bell reachability and the served license. No rendering/source-media bytes changed
in that correction.

The final local **linux/arm64** image is
`freetrain-online:p2-acceptance`, SHA-256
`4cee1fa9c21102670aca3f3a9111cd97ba3a80ff0de85bb0055b30350dac2fb8`.
The bundled container check passes all 238 assets, 505 unique media outputs,
provenance/plugin metadata, four notices, synthesizer/served-license hashes and
reference-world bootstrap. Optional container checks pass read-only source mounts
with an external cache, read-only prepared mounts and a base-only restart; source
and prepared flows advertise the same set hash. This does not publish a service.

English and 360-pixel Japanese Settings screenshots were visually inspected.
Final focused evidence is in `tmp/p2-acceptance-browser-final-evidence/audio/`;
the full run's screenshots/measurements are in `tmp/browser-evidence-z5XGrd/`.
The new audio checks observe actual WebAudio output on all three tracks, decode
all six WAVs, reject changed music bytes, fence cancelled loads, preserve BGM
across panel navigation, stop on hiding, and preserve canvas/world state. The
independent timing fixture checks authored trailing silence and repeated-loop
fractional tempo. Passing correctness cases do not establish sound fidelity or
full-content performance targets.

Reproduce with the existing Python/.NET/client/build commands, then:

```sh
python3 scripts/prepare-bundled-content.py --check
deno task --cwd web check:locales
cd web
deno run --cached-only -A npm:@playwright/test@1.64.1 test
deno run --cached-only -A npm:@playwright/test@1.64.1 test --config playwright.optional.config.ts
FREETRAIN_ONLINE_OPTIONAL_PREPARED=1 deno run --cached-only -A npm:@playwright/test@1.64.1 test --config playwright.optional.config.ts
cd ..
docker build -f deploy/Dockerfile -t freetrain-online:p2-acceptance .
python3 scripts/check-bundled-deployment.py --image freetrain-online:p2-acceptance --platform linux/arm64
python3 scripts/check-optional-deployment.py --image freetrain-online:p2-acceptance --platform linux/arm64
```

These container runs use the host's arm64 target and original synthetic optional
fixtures; they do not exercise or package the non-free collection. No native
browser/device acceptance, full-game capacity run or original-runtime audio capture
is inferred. Logs are under `tmp/p2-acceptance-*`; earlier P2 records retain their
own catalog/image pins and evidence scope.
