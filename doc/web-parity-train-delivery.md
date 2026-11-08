# P5-29 content, distribution and bilingual reconciliation — 8 November 2026

P5-29 reconciles delivered train gameplay with pinned ExAV
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, without changing Save9/protocol7,
canonical gameplay, content bytes or admission limits. P5-30 remains the integrated
phase acceptance and P6 handoff.

The reproducible [delivery inventory](parity/train-delivery.json), generated and
checked by [reconcile-train-delivery.py](../scripts/reconcile-train-delivery.py),
accounts for all 34 P5 contributions, 18 operations, 638 source literals and 55
catalog strings. It preserves original identities/hashes and joins each text to
its delivered feature family and implementation/test paths. This supplements the
immutable P5-01 specification; its historical “unimplemented” status describes
that earlier review, not current runtime.

## Content and action closure

All six trains, eight cars and seven color templates resolve to registered
definitions and ready recipes. All 325 admitted stock/length choices resolve real registered
cars, with source body/head/tail precedence and exact prices. Both reverse tails
remain source wrappers. The color-test stock is real purchasable zero-capacity
content, with seven templates and shared persisted colors. Rendering fixtures
prove crops, poses, composition and picking; gameplay credit comes separately
from RollingStock/Fleet domain tests and actual browser purchase/deployment.
No fallback sprite, P4 trusted test car or synthetic optional fixture is counted
as bundled stock gameplay.

| Source family/action | Delivered route and evidence |
| --- | --- |
| Stock factories, compositions and car wrappers | `RollingStockCatalog`, generated stock/fleet catalogs, stock and renderer unit tests |
| Purchase, name and resale; workflow01 | Trains purchase/owned tabs, exact quotes and transactions; trading tests and new gameplay-built reconciliation matrix |
| Placement and withdrawal | Owned train deployment controls, legal previews and occupancy; deployment domain/browser tests |
| Manager and simple/delegation controllers | Shared diagram manager and effective-controller assignments; controller manager/group tests |
| TAT junction, stop, advanced and TURNING; workflow03 | Selected controller/location configuration, ordered rules and masks; basic/advanced controller, timetable and rule-file tests |
| Group hierarchy | Diagram manager group tree, inheritance and individual overrides; group domain/browser tests |
| Diagram import/export | Bounded legacy `.ftdd` import and portable `.ftdd.json` export; ordered append, copies, conflicts, reopen/restart |
| Information and tracking | View/owned train information, local labels, Move/Follow; independent information/color/renderer tests |
| Signals, speed limits, garages and stationary hosts | Existing P4 tools plus P5 timing, stop and movement consumers; no station/passenger gameplay credit |
| Historical Manual provider | Owner-removed on 7 November; inert prepared declaration remains source metadata, excluded from canonical provider registry and UI. Retirement tests enforce this boundary. |

## Wording reconciliation

The inventory retains all 279 technical/format literals, 143 numeric/mask/designer
values, 206 presentation/dynamic strings and ten content diagnostics. Technical
IDs, XML/RGB syntax and file formats remain invariant. Typed choices and locale
formatting replace designer digit lists and sample values. Browser labels replace
desktop accelerator syntax, placeholder names and modal layout strings; this is
semantic adaptation, not verbatim reproduction of 206 desktop labels.

The feature bindings enumerate 172 distinct literal `text.*` keys actually used by their
components and require nonempty EN/JA resources. Existing locale checks also
validate the complete resource key/placeholder contract and all 30 source-bound
English stock fields; Japanese stock fields preserve original source wording.
Changed source originals cannot inherit stale translations. The source stock
speed rating words become exact minutes/voxel. Source travel-range metadata and
passenger occupancy wording do not grant P6 passenger selection/fare behavior;
capacity/status presentation retains that explicit qualification. Manual driving
instructions are retired with their provider. Source plugin metadata, IDs and
authorship remain available through the prepared catalog/provenance UI; metadata
retention alone does not claim a translated workflow or callable desktop menu.

Workflow01 now builds normal rail, purchases a three-car train, protects an
unsaved name on navigation to language settings, tests Keep editing and explicit
discard, changes locale, deploys real cars, withdraws and resells. It runs in both
starting locales at desktop and 390×844 coarse layouts with 200% root text. The
TAT/shared-manager, nested-group and diagram-file workflows each use the same
four-way locale/layout matrix. Draft conflict/cancel, assignment, ordering,
observer, save/reopen and process-restart assertions remain in those tests.
Captures distinguish locale and layout. Inspected coarse draft and rule-preview
captures keep their controls reachable through the existing scrolling dialogs.

The information test previously read the authoritative HTTP tick before its
client received the command receipt and could send an ignored next shortcut.
It now waits for the client's Step control to become enabled before each keyboard
step. Production command serialization is unchanged.

## Distribution and validation

Base-only web production build, local Release server build/publish and
`linux/amd64` Docker build pass. Docker uses its allowlisted independent source
context; no archive, historical/optional root or host build output is required.
The extracted container has 854 files; all 610 static files match local `web/dist`
byte for byte. All 238 catalog assets and four notices match their recorded
hashes. Local and container managed notices agree with the catalog. No
`plugin-set.xml` or `prepared-pack.json` appears in these artifacts. The unchanged
bundled hash is `9e85cf889fd8bd49c24c6173c971bf7fe935a5d3a36438f4e242e84bee3c35b9`.

Supported synthetic optional source and prepared registrations each pass their
separate four-case browser run, including separate credits/catalogs, tamper
rejection and prepared terrain. A subsequent base-only run explicitly asserts
zero optional packs before each real fleet workflow. These synthetic fixtures
carry no canonical optional stock/repair acceptance. No container runtime,
deployment integration, full regression, capacity or unrelated phase suite is
run for this step; the container is built and inspected as an artifact.

Fresh focused evidence is in `tmp/p5-29-*`: 86/86 relevant Release .NET unit
tests, 52/52 selected client tests and 13/13 Python reconciliation/source-review
unit tests. Production/generated/content/localization/Svelte checks pass with
zero errors/warnings; Vite retains its existing bundle-size advisory. The final
selected Chromium run passes **42/42 without skips** (`tmp/p5-29-final-selected.log`),
in addition to **4/4 source optional**, **4/4 prepared optional** and **4/4 base-only
after optional**. The fixture-dependent P5-15 traffic cases are outside this
selection, not counted as skipped acceptance. Exploratory runs
and their corrected test failures remain in the logs rather than being counted
as passing final runs.

P2 original-runtime/human full-gallery fidelity, native IME/display/accessibility
and other-engine checks retain their existing owners. Canonical optional V2
resource repairs and older-suite compatibility remain P2/P9/P10/P11 work. The
P4/P5 resource findings remain P11; P6 passenger/station gameplay and P5-30's
complete integrated phase review remain open.
