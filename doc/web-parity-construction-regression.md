# P4-22 construction multiplayer and English/Japanese regression — 1 November 2026

This gate covers delivered browser/portable construction. Source behavior remains
bound to [66 reviewed files and decisions](web-parity-railway-contract.md) and
[1,484 independent railway vectors](web-parity-railway-fixtures.md). Integration
checks establish authorization, lifecycle and coherent publication; they are not
an independent geometry oracle or native fidelity acceptance. Save8/protocol6 and
all tool prices and handler profiles remain current.

## Requirements and execution surfaces

| Requirement | Evidence |
| --- | --- |
| REG-01, REG-10 | Existing operation registry and keyboard toolbar; normal, slope, bridge, girder, tunnel, garage, signal, pole, stationary accessory, speed-limit and whole-object removal browser suites exercise their actual forms and preview/Confirm/Cancel actions. No future train/station/road command is exposed. |
| REG-02 | Per-tool late-preview tests; `railway-edit.test.ts` fences all nine shared route/accessory request families against epoch, geometry, content, abort, reconnect and disposal. Locale alone preserves the model request. Browser bridge-support contention, epoch replacement with a retained late HTTP reply, role replacement during preview, detached-preview disconnect/reconnect, delayed receipt and existing lost-acknowledgement regression cover correction and no replay. |
| REG-03 | Construction regression exercises running shared time, pointer cancel/blur/visibility resets, modal inertness/focus trap/Escape and cancellation before Settings/locale entry. Existing shell/primary-action tests cover modifier/repeat/IME guards and explicit confirmation. |
| REG-04 | Existing first-rail multi-chunk profile promotion keeps the current world visible until complete; startup/reference-world/terrain suites cover readiness and replacement. Required pack/epoch fencing and corrupt recovery remain in P4-21. |
| REG-05 | Both locales, 430×850, touch/DPR2, 200% text and internally scrolling actions; same canvas object/bounds and decode count across all construction tasks; camera restores after closing each narrow sheet. Existing per-tool art/picking tests cover all supported headings and cuts. |
| REG-06 | Owner and Operator construction versus Observer gating across all tasks. Per-tool session tests reject forged Observer commands, stale quotes and disconnected participants; concurrent commands publish one booking. Owner-only save and captured epoch/busy gates retain P4-21 coverage. Credentials/drafts/leases are not preferences. |
| REG-07 | Current generated contracts/content, literal UTF-8 locales and build checks; full client, source-review, fixture, boundary and .NET suites. Full continuation remains P4-21. |
| MAN-A03 | `railway-construction.spec.ts`: two map points, legal/cost/cancel/build, joins/branches/obstacles, underground cut, removal and shared peers. |
| MAN-A04 | Slope, bridge/girder/tunnel suites: flat/ramp/elevated connections, support losses, exact removal, cut-aware picking and reopen. Speed-limit flat/ramp membership and cleanup stay in the speed-limit suite. |
| MAN-A12 (P4 portion) | Signal/controller reference persistence and protected removal, pole choices, stationary variants/sites and speed lists. Controller editing, train stops and motion remain P5/P6. |
| MAN-A14 (P4 portion) | Actual cut slider/manual level/automatic picks and source underground geometry, combined terrain protection and middle-button pan. Buildings, native gestures and later-phase restrictions retain their owners. |

Catalog preview images are warmed before recording steady decode identity. Narrow
sheets temporarily move the small-world camera to expose the map; closing a task
restores the previous camera. Neither operation remounts the canvas or changes
the durable world.

Opening Settings from a construction draft deliberately clears targeting and the
quote before changing language. Closing Settings returns to the selected task
with Confirm disabled. This follows the existing panel-entry contract; language
selection does not retain hidden geometry that could later be submitted.

## Source-text translation reconciliation

[All 78 source text records](parity/railway-localization-review.json) retain their
pinned Japanese literals, donor revision/text and initial P4-01 scope status.
Their separate **P4-22 delivery** records bind exact current EN/JA resource keys,
wording and explicit semantic adaptations. The source validator rejects absent
records, unknown keys and wording drift even after a review digest is refreshed.
No donor is promoted to authority or silently rewritten.

The inherited road-window default title is superseded by each actual railway
task title; playable roads remain P7. Native switch integers become typed speed
values. Automatic Traffic deletion cleanup replaces the redundant orphan-limit
cleanup menu. Positive Show electric poles replaces the native negative Hide
toggle. Garage description explicitly retains decorative scope. Native modal
errors use detached, corrective preview feedback and never pause shared time.
Car-protection wording is translated now; real moving-car workflows remain P5.
All 64 bundled definitions, including 54 stationary variants and four pole
choices, retain source identities and original author/plugin metadata. Optional
suite compatibility retains its separate acceptance.

## Verification

- Release **1,114/1,114 .NET** (`tmp/p4-22-dotnet.log`).
- Client **221/221** (`tmp/p4-22-client-final.log`), including the nine-family
  preview fencing matrix and extended slope/removal content/disposal checks.
- Python **163 tests, one skip** (`tmp/p4-22-python.log`); 66 pinned sources,
  1,484 independent vectors, 64 accessory bindings and project boundaries pass
  (`tmp/p4-22-source.log`, `tmp/p4-22-fixtures.log`,
  `tmp/p4-22-accessories.log`, `tmp/p4-22-boundaries.log`).
- Production/generated/content/locales/Svelte pass (`tmp/p4-22-build.log`).
  The existing large-chunk advisory remains.
- Final focused Chromium **7/7** (`tmp/p4-22-browser-final4.log`), with actual
  touch focus on every numeric field and keyboard focus on every select at 200%
  text. Final EN/JA captures in `tmp/browser-evidence-KXyNzj/construction/` were
  visually inspected.
- Broad Chromium construction and shared-shell regression **113/113**
  (`tmp/p4-22-browser-regression.log`, 67 canonical construction / 46 prototype
  shell cases). Together with the focused run, **120/120** browser cases pass.
  Broad captures are in `tmp/browser-evidence-TaMwk5/`; representative Japanese
  cross-tool and tunnel captures were visually inspected.
- Final translation corruption regressions **9/9**
  (`tmp/p4-22-translation-final.log`); whitespace passes.

Initial harness assertions were corrected for canonical Track labels, task
click toggles, camera restoration after closing narrow sheets, modal Enter's
Close action and reconnect returning to Inspect. Final tests wait for a new
socket and explicitly reopen the task. These were fixture corrections; no
production gameplay change was required.

The final focused cases use real HTTP saves, participant credentials and socket
reconnects against an isolated game. Preview/quote races deliberately retain HTTP
responses; delayed/lost receipts use intercepted real WebSocket messages. Proxy
ECONNRESET/EPIPE messages during deliberate connection changes are expected and
are not evidence of a failed assertion.

From `web/`, run the seven focused cases with:

```sh
deno run -A npm:@playwright/test@1.64.1 test construction-regression.spec.ts --project canonical
```

Build the Release server and web bundle first. The normal Playwright configuration
starts isolated canonical/prototype servers and temporary save directories. Set
`FREETRAIN_ONLINE_TEST_SERVER_PORT` and `FREETRAIN_ONLINE_TEST_WEB_PORT` to unused
port pairs when another local game is running. The focused tests restore the
original isolated world in `finally`; they do not upload to the owner's game.

P4-23 legal-network performance/capacity and Linux distribution, P4-24 integrated
acceptance/P5 handoff, P2 human artwork review, native IME/display/accessibility,
owner-deferred browser coverage, P5–P11 gameplay/import and optional-pack gates
remain open. Touch, DPR and text enlargement here are Chromium emulation.
