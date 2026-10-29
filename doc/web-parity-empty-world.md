**P3-16a capacity update — 29 October 2026:** [Current packed state, coherent transfer, admissions and evidence](web-parity-map-capacity.md) supersede the temporary size/byte/working limits and Save 6/protocol 4 statements below. Earlier measurements remain historical slice evidence. Current envelopes are H/V≤2048, at most 1,700,000 stored cells, Z≤64 (generated minimum H/V16,Z8), Save 7/protocol 5 and 256 MiB assembled state. The concrete scale target is 1500×1090×45 stored. Overall P3 and the recorded P2/native/full-game gates remain open.

# P3-06 empty-world browser slice — 29 October 2026

Game → New world now creates a playable flat canonical terrain world. The Owner
edits a name, stored H/V/Z dimensions and global water level, previews the detached
candidate, then explicitly confirms replacement for all participants. Cancel,
invalid input, stale state, role denial and failed generation preserve the world.
Heightmaps, procedural generation, editing and the overall P3 gate remain open.

The source authority remains pinned ExAV 2.1.1.12 / `883cf7d`, specifically
`core/contributions/others/{NewWorldDialog,EmptyNewGameContributionImpl}.cs`,
`core/framework/NewWorldDialog.cs` and `core/world/World.cs`, as bound by the
[P3-01 contract](web-parity-terrain-contract.md). This delivers the empty-map parts
of MAN-M01/O02/E02/A02/A19, DEV-P09/P17 and EXAV-13. Heightmap/provider-selection
parts of those requirements remain P3-07. There is no original-runtime capture
or native pixel comparison in this step.

## Controls and admission

The Source defaults button expands displayed source dimensions 30/30/6 once into
stored **H30 × V42 × Z6**, water1. Custom H/V are already stored dimensions; no
second V padding occurs. Ground equals the single global water level. There are
zero rails, entities, forests or decorations; new worlds are paused with the
canonical initial calendar and independent default gameplay random streams.
The source does not scatter trees as part of empty-world creation.

The initial empty-only browser range is H/V1–64, Z1–64 and water0…Z−1, subject to
the structural/work validator. Name length is at most128 UTF-16 units with no
control characters; nonempty names, including whitespace, are preserved. The UI
expands an empty name to the selected language's Untitled world / 名称未設定 before
fingerprinting, so reopening needs no locale-dependent reconstruction. The service
never trims names or infers displayed dimensions from stored ones.

This range is a new bounded web admission policy, not ExAV's maximum map size.
The 128²/dense-world profiles remain disabled. No heightmap or generated request
can enter this endpoint. The preview is an accessible schematic flat footprint
with exact dimensions, level, zero-rail count and actual save/snapshot byte counts.
It is not a second live map, terrain editor or full P3-12 preview mode.

## Candidate and replacement boundary

- `Session/WorldCreationSession.cs` joins begin, completion, cancellation and commit
  to the existing bounded FIFO. It validates current Owner/connection, epoch and
  revision at each publication boundary. One candidate is retained per session;
  another Owner receives busy while it is valid. The same Owner's new preview
  supersedes its old job. IDs prevent late completion/cancellation from affecting
  the new job.
- `Server/EmptyWorldCreation.cs` builds the immutable flat world and serializes
  Save6 on a detached worker. At most four workers run globally; the job lifetime
  is600 seconds and the worker deadline30 seconds. Empty construction is bounded
  to4096 cells; its completed worker checks the deadline before publication.
  Later multi-stage algorithms still require cooperative work/cancellation batches.
- Terrain readiness comes from the exact executable bundled terrain profile,
  through `WorldCreationCapabilities.EmptyReady`. Forest/provider inventory remains
  unavailable to creation. Optional installed content is not used by this empty
  route, and its resources never enter the candidate.
- Candidate SHA-256 binds UTF-8 `requestHash + LF + catalogSetHash + LF` followed by
  the canonical Save6 bytes. Actual saved state includes creation provenance,
  required packs, clock and random state. The service retains those exact bytes,
  measures snapshot encoding too, rechecks the hash and validates the saved world
  on commit. Confirm does not run generation again.
- Commit increments the world epoch once, publishes replacement snapshots to all
  peers and requests a checkpoint. A second confirmation fails. Revision changes,
  replacement, disconnect, changed connection and expiry invalidate/release the
  candidate and its global reservation. Global reservation slots include Ready
  jobs; session disposal releases them too.
- `EmptyWorldControls.svelte` invalidates previews on edited options, epoch/revision
  changes, disconnect, expiry and panel close. Confirm uses the captured candidate
  hashes and fence. HTTP timeout/failure is reported as potentially uncertain and
  is never automatically replayed; a new preview is needed. Existing canvas,
  modal focus/gesture isolation, role checks, busy exclusion and shared clock
  behavior are preserved. Home now frames the centre terrain surface, including
  high water/ground, rather than always framing elevation0.

Endpoints are bounded authenticated JSON POSTs at
`/api/worlds/empty/{preview,commit,cancel}`. They require a currently connected
Owner, not merely a valid cookie. Preview receives explicit name/dimensions/water
and epoch/revision; commit receives candidate ID/hashes and epoch/revision; cancel
receives only the ID. No new socket protocol or save version is necessary.

## Resources and measurements

P3-02's general dense-world reservations remain provisional. Empty-only admission
reserves the existing hard96MiB job envelope, with at most four retained jobs;
Ready retains only the bounded canonical byte buffer, not a live object graph.
The measured complete flat create/serialize/validate/commit path, including repeated
restore and snapshot construction, stays under that hard envelope. Allocations are
thread-local managed churn, not peak RSS or browser memory. Actual save and
snapshot lengths must also fit the request's transfer allowance and16MiB state cap.

Local macOS26.7 / Arm64, .NET SDK10.0.402 / runtime10.0.13, Release build.
One sample per boundary, with other regression classes running concurrently:

| Stored dimensions / water | Save bytes | Snapshot bytes | Thread-local managed allocation | Elapsed ms |
| --- | ---: | ---: | ---: | ---: |
| 1×1×1 / 0 | 7,339 | 990 | 1,000,168 | 12.85 |
| 30×42×6 / 1 | 48,890 | 42,541 | 16,445,768 | 102.47 |
| 64×64×64 / 63 | 146,578 | 140,229 | 52,089,688 | 229.48 |

These are complete headless round trips through candidate generation, validation,
commit and snapshot, not isolated worker latency, peak memory or p95 browser
interaction results. First-use/cross-test concurrency affects elapsed time. The
initial attempt used process-wide allocation counters while tests ran concurrently;
that counter was corrected to thread-local measurement before accepting admission.
Dense terrain/vegetation encoding still needs its separate representation/capacity
work in P3-17. No provisional full-game performance target is closed here.

## Text review

Existing [source text records](parity/terrain-localization-review.json) preserve
Japanese source expressions/hashes and English donor paths/lines/hashes. The
following shipped keys in `web/src/locales/{en,ja}/app.json` bind those records;
new browser wording is reviewed here without changing the frozen P3-01 inventory.

| Shipped keys | Source records / classification |
| --- | --- |
| `newWorld`, `createWorld` | TERRAIN-TEXT-005/014: 新しいゲーム / 新しいゲームの作成; meaning-adapted to New world / Create world and 新しい世界 / 世界を作成. |
| `worldName` | TERRAIN-TEXT-008 名前(&N):; meaning-adapted field label World name / 世界名. |
| `worldH`, `worldV`, `worldZ` | TERRAIN-TEXT-009 サイズ(&S): and numeric separators010/013; adapted into explicit stored-axis labels, preserving the source default expansion. |
| `unnamedWorld` | TERRAIN-TEXT-015 ななしさん, historical donor Terra Incognita; reviewed adaptation Untitled world / 名称未設定. |
| Existing `cancel` | TERRAIN-TEXT-004/012 ｷｬﾝｾﾙ(&C); semantic carryover with browser typography and no native accelerator. |
| `worldWater`, `sourceDefaults`, `emptyWorldHelp`, `previewWorld`, `emptyPreviewLabel`, `zeroRails`, `candidateBytes`, `preparingWorld`, `creationFailed`, `staleCandidate`, `creationBusy`, `created`, `confirmEmptyWorld` | New browser controls/help/feedback under WORLDGEN-D002, translated EN/JA. Water is explicit global-plane configuration; feedback distinguishes invalidated/busy/uncertain outcomes without displaying backend exception strings. |

Provider author/description text and empty-map provider selection remain source
metadata; this single-route panel does not pretend to deliver the later provider
picker. The new `confirmEmptyWorld` title and existing `sharedReplacement` resource provide the Owner
confirmation and explain that every participant's world changes.

## Verification and remaining gates

Validated the working tree based on `5878c952062974a742eb1cd615d7163013056813`;
these additions are uncommitted. Release .NET build and Svelte/TypeScript checks
pass with zero warnings/errors.
All **429 .NET**, **120 client** and **141 Python** cases pass. Generated contracts,
bundled content, locale validation, source review, project boundaries and whitespace
checks pass. The .NET additions cover exact commit/cancel/reopen, immutable defaults,
all role/fence/hash failures, late workers, real-byte/allocation admission, missing
runtime content and retained global reservation release.

Chromium covers **25 canonical cases across the full run and final focused run**,
plus **54 historical cases**. The full run passed78/79: the new high-elevation
camera test incorrectly expected y<−900. Source H/V projection gives the exact
centre `(1040,−744)` for H32,V32,Z63; the assertion now uses that literal pair.
The final seven terrain cases pass, including all three creation cases, without
changing the passing camera implementation. Test-development fixes also added the
New world helper label and used the specific confirmation dialog/clock selector.
The full catalog gallery, source frame variants, existing multiplayer/draft/focus/
canvas/save/clock regressions passed in the full run. All **six optional source/prepared fixture cases** pass separately (three per
startup mode); no non-free source is used.

Commands (from the root unless marked otherwise):

```sh
dotnet build FreeTrain.Online.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test FreeTrain.Online.slnx -c Release --no-build --no-restore -m:1 -nr:false
deno task --cwd web test
deno task --cwd web build
python3 -m unittest discover -s scripts/tests
python3 scripts/review-terrain.py --check
python3 scripts/check-project-boundaries.py
# From web/:
deno run -A npm:@playwright/test@1.64.1 test
deno run -A npm:@playwright/test@1.64.1 test terrain.spec.ts --project=canonical
deno run -A npm:@playwright/test@1.64.1 test --config=playwright.optional.config.ts
FREETRAIN_ONLINE_OPTIONAL_PREPARED=1 deno run -A npm:@playwright/test@1.64.1 test --config=playwright.optional.config.ts
```

Raw logs are local `tmp/p3-06-{build,dotnet,client,python,web-build,browser-final,terrain-browser,optional-browser,optional-prepared-browser}.log`.
Final focused screenshots under `tmp/browser-evidence-4Iep6h/empty-world/` show the
English preview and narrow Japanese form/confirmation; visual inspection confirms
readable controls with internal scrolling and no document overflow. This is
Chromium emulation, not native mobile/device or cross-browser acceptance.

P3-06 is the empty-world milestone only. P3-07 owns bounded heightmap decoding;
P3-08–12 own generated maps and expanded preview UX; P3-13–16 own tools/mode;
P3-17 owns the full terrain/topology/capacity acceptance matrix. Existing P2 crops,
optional-suite repairs, native/fidelity and full-game gates remain open.
