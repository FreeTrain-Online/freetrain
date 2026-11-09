# P6-12 platform properties and connections — 9 November 2026

Constructed slim and wide platform picks now open properties in the existing
inspector. Station picks keep their station-name workflow. Both use the shared
Apply/Reset footer and guarded navigation with Keep editing, Discard and Apply.
Save10/protocol8 remain unchanged. P6-13 retains actual lane editing and safe
extension/removal integration; wide-only lane controls are visible but disabled
with an explicit pending explanation. Slim platforms have no lane controls.

## Source and behavior

The implementing authority is stable ExAV archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`,
`trunk/core/controllers/rail/PlatformPropertyDialog.cs`, read from that revision
and decoded as CP932. Its constructor populates independent platform name,
`listHosts()` and the selected departure bell, shows the disconnected warning,
and exposes left/right track controls only for FatPlatform. Its OK handler sets
name, host and bell. The host setter and candidate geometry remain the reviewed
[P6-11 implementation](web-parity-station-hosts.md), including recursive child
disconnection, descendant-target reachability loss and same-host preservation.

The browser adapts this to one atomic, revision-checked command rather than three
live mutable setters. Names follow the existing online station policy: trim,
1–128 UTF-16 code units and no control characters. A platform name never renames
its station. Exact qualified bell IDs come from the existing prepared catalog;
all six choices reuse source-bound EN/JA names, media hashes and credits.

The editor shows the saved resolved station, candidate direct IDs and resolved
station IDs, a disconnected/pass-through warning, a draft destination summary,
and EN/JA help. Logical connections are separate from cosmetic footbridges and
timetable stop requests. Reparenting clears child connections, while metadata
changes at the same host preserve children. Existing automatic construction
connections and removal/rebuild behavior remain intact. Picking a wide center or
yard reaches the same platform identity and property draft. Art provenance remains
available in the inspector.

Bell preview remains local and verifies the prepared WAV length/hash before
playback. Closing the editor stops/fences the preview. Preview/retry buttons have
explicit `type="button"` so embedding the existing BellControls in a property
form cannot submit edits. No property edit or preview emits a simulation departure
event; actual departure playback belongs to P6-20.

## Authority and continuation

`POST /api/station/platform/properties` is an authenticated, compatible,
size-bounded read-only query. It returns the authoritative P6-11 candidates with
direct host and resolved station revisions/locations. The echoed query carries
world epoch, mixed geometry revision, content version and platform ID/revision.
Remote and shell clients fence cancelled/late responses, reconnects and epochs;
the decoder rejects malformed, duplicate, self and inconsistent candidate IDs.
The editor also matches candidates against the current validated world.

Dirty drafts freeze the query and candidate revisions. Peer edits, parent removal
and geometry changes mark the draft conflicted and disable Apply until Reset;
they preserve the typed name and selected bell/host. This deliberately uses a
conservative geometry fence, so unrelated mixed geometry edits can also require
Reset. Clean drafts refresh their authoritative candidates. Closed, replaced and
disconnected editors discard pending responses and cannot replay a mutation.

`editPlatformProperties` uses the existing FIFO, role, epoch and command receipt
boundary. Commit revalidates platform, geometry, content, selected direct host,
resolved station revision, actual candidate membership, name and qualified bell
before publishing a detached successor. Clear-host uses null and zero host/root
revisions. A combined metadata/host edit increments each changed identity once;
wide and station spatial owners match graph revisions. Exact no-ops preserve the
world and children. No account charge, timer re-registration, RNG consumption,
passenger/controller effect or departure sound is introduced. Current saves already
persist names, bell definitions and host graphs; no save migration is added.

## Verification

Fresh final checks on 9 November 2026:

- **1,039/1,039 relevant Release .NET tests** pass, including five new platform
  property tests and existing station/platform/mixed/fleet/train/harbor tests.
  An additional **69/69 contract/network tests** pass, including the shared
  property-command fixture round trip. Neither selection has skips.
- **358/358 web units** pass: 356 ordinary tests plus two retention tests.
- **43/43 selected Chromium tests** pass without skips: 40 canonical scenarios
  and three prototype draft-navigation regressions. These include both new
  MAN-A06/08 property journeys, existing station/host/thin/wide/transport/rendering
  workflows and the existing Settings bell preview/hash/cancellation checks.
  Final EN desktop and JA narrow property captures were inspected.
- **28/28 station Python tests**, all **821 independent literal fixtures**, raw
  pinned-source review, generated contracts/content, EN/JA locales, Svelte,
  production build, project boundaries and whitespace checks pass. Svelte reports
  zero errors/warnings. The existing Vite bundle-size advisory remains.

Domain tests exercise thin/wide atomic edits, unchanged queues/accounts/RNG,
exact save reopen, no-ops, recursive children, roles, invalid inputs and stale
platform/host/root/content/geometry/epoch fences. Web units exercise candidate
response validation and immutable identity/revision checks. Chromium exercises EN
desktop and JA430×860 real touch with two peers, local bell preview, connection
changes, child disconnection, dirty navigation, conflicts/reset, parent removal,
exact HTTP reopen/checkpoint restart, late response cancellation, world replacement
and Observer controls, plus existing station/platform/draft/audio regressions.

```sh
dotnet test tests/FreeTrain.Online.Tests -c Release --no-restore --filter 'FullyQualifiedName~Station|FullyQualifiedName~Platform|FullyQualifiedName~Mixed|FullyQualifiedName~Fleet|FullyQualifiedName~Train|FullyQualifiedName~Harbor'
dotnet test tests/FreeTrain.Online.Tests -c Release --no-build --no-restore --filter 'FullyQualifiedName~ContractTests|FullyQualifiedName~NetworkTests'
DENO_JOBS=4 deno task --cwd web test
deno task --cwd web build
deno task --cwd web test:browser:run platform-properties.spec.ts station-hosts.spec.ts thin-platform.spec.ts wide-platform.spec.ts station-building.spec.ts station-building-domain.spec.ts station-transport.spec.ts station-rendering.spec.ts station-editing.spec.ts reference-world.spec.ts
python3 -m unittest discover -s scripts/tests -p 'test_station*.py'
python3 scripts/station-fixtures.py --check --source-root /Users/dmark/dev/freetrain-exav-archive
python3 scripts/review-stations.py --check --source-root /Users/dmark/dev/freetrain-exav-archive
python3 scripts/check-project-boundaries.py
git diff --check
```

Logs: `tmp/p6-12-dotnet-final.log`, `tmp/p6-12-dotnet-contract-network.log`,
`tmp/p6-12-web-unit-final.log`, `tmp/p6-12-web-build.log`,
`tmp/p6-12-browser-final.log` and `tmp/p6-12-{python,fixtures,review,boundaries}.log`.
Final browser captures are in `tmp/p6-12-browser-final/`.

Earlier iterations corrected collection-content assertions in three new .NET
cases, explicit selector labels, the actual bell-preview form-submission bug,
and test waits that observed disabled Apply during command submission rather than
waiting for the receipt and refreshed candidates. Final results above supersede
those failing runs.

Native/original-runtime/human visual fidelity, other browser engines, optional
content, full-suite/capacity/soak, Linux/container and deployment acceptance are not
credited. Passenger service, fares/demand, timetable stop handling and real departure
effects retain their later P6 owners. The relevant .NET selection includes existing
fleet capacity regression tests; this is not a new capacity measurement.
