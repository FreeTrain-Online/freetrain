# ExAV authority and scope reconciliation

**P2-01 build-input update — 25 October 2026:** [canonical bundled preparation](web-parity-bundled-preparation.md)
replaces the historical default importer/Docker inputs with checked-in pinned
ExAV resources and a prepared catalog. EXAV-P0-09-INPUTS's reproducible-input
portion is delivered; full recipes/runtime and distribution acceptance stay open.
The P0 review's separately pinned implementation findings below remain historical
evidence and are not repinned to the current working tree.

**P0-09 source integration completed — 21 October 2026.** Stable **FreeTrain
ExAV 2.1.1.12 / V2**, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, SVN r115, **`trunk/`**, is the
sole gameplay and bundled-content authority. This review integrates P0-01–08
and the earlier `EXAV-01`–`EXAV-16` research into maintained specifications.
Overall P0 and the existing UX/native handoff remain open. Implementation,
runtime comparisons, import compatibility and P1–P11 acceptance remain open.

[Maintained decisions](parity/exav-integration-rules.json) and the
[generated source review](parity/exav-integration-review.json) record the
requirement evidence, source assignments, six branch identities and explicit
gap dispositions. The review reads pinned Git objects, including where the
archive working tree has approved corrections. It does not use the corrected
working tree as a replacement historical pin. The inspected application/import/build inputs are separately pinned to `357c6e4095e98333e619d153db679c731e87831c`; the review retains hashes and symbols for the historical asset importer, Docker inputs and delivered protocol/content/save constants.

## Authority, identity and reuse

New-world parity targets stable V2. Engine release, simulation ruleset, plugin
family, individual pack identity/version/hash, protocol and save schema are
independent. The [P0-05 transition design](web-parity-persistence.md) reserves
`exav-2.1.1.12-v1` and `exav-bundled-v2-1`; these are design names, not a
running migration. Protocol 2, `prototype-content-1` and Save 1 remain the
delivered values. Prototype Save 1 retains its labeled compatibility design;
there is no implicit conversion to ExAV geometry, clock or economy.

Original FreeTrain `1f755ca`, this repository's old `core/` and `plugins/`,
historical manuals and `freetrain-modern/main` supply comparison, workflow,
implementation and English-translation references. They do not establish
another required game profile. Same-path files, matching IDs, donor strings,
asset hashes and prototype tests cannot transfer semantic authority on their
own. Reuse only the specific result justified by the owning ExAV review and
its dependencies. Avalonia UI/localization remains a reference; its native
wrapper, host behavior and save model are not the stable oracle.

Only **EXAV-DIR-001** (`SOURTH` → `SOUTH`) and **EXAV-ENC-001** (UTF-8 manifests
and fallback metadata reader) are already approved source corrections. Their
separate working-tree hashes remain in [source selection](parity/exav-baseline.json).
Other anomalies below require an explicit port decision before the affected
feature is accepted; source integration does not approve a silent fix.

## Integrated requirement map

Paths and symbols with hashes are retained per requirement in the generated
review. The table states delivery obligations; no row claims implemented parity.

| Requirement | Stable behavior and maintained specification | Delivery / acceptance owner |
| --- | --- | --- |
| **EXAV-01 — identity** | Independent engine/rules/pack/save identities and prototype compatibility in [persistence](web-parity-persistence.md). V1/V2 is not a serializer-layout discriminator. | P1/P2/P10: validate content before replacing a world; explicit same-line save adaptation. No second selectable original/V1 simulation is assumed. |
| **EXAV-02 — money** | Initial cash 15,000,000,000; default bankruptcy top-up 10,000,000,000; widened money interfaces coexist with integer intermediate expressions. [Persistence](web-parity-persistence.md), [scenarios](web-parity-scenarios.md). | P1/P8/P10: exact amounts, field-specific units/migrations, overflow and invalid top-up decisions. No universal ×10,000 conversion. |
| **EXAV-03 — train contracts** | Required fare, amenity and seated capacity; amenity clamps 50–200; triprange defaults to short on parse failure. [Content](web-parity-content.md). | P2/P5/P6: validated V2 contracts, inheritance/reversal and metadata display. Triprange does not authorize beta passenger behavior. |
| **EXAV-04 — transport arithmetic** | Packing = seats + twice standing; float/truncating boarding, actual exported counts, configured fare × integer distance × 2; operating charges on calls 1,5,9… . [EXAV-SCN-010–014](web-parity-scenarios.md). | P5/P6/P8: independent traces, boundary arithmetic, multiple departures and accounting. |
| **EXAV-05 — station lifecycle** | Hourly depletion ×0.8, midnight upkeep, retained timer handles, special listeners and old-timer migration. [Persistence](web-parity-persistence.md), [scenarios](web-parity-scenarios.md). | P1/P6/P10: alignment/removal/reload, special catchments and inspectors that do not alter durable state. |
| **EXAV-06 — population/calendar** | Expanded population types/tables; footprint/height demand cap; source holiday/vacation rules. [EXAV-SCN-017–020](web-parity-scenarios.md). | P1/P6/P7: actual consumer chains, creation-order/reach boundaries and explicit calendar-defect disposition. |
| **EXAV-07 — development** | Imported score + 0.1 exported score, diffusion and V2 candidate price scale ×10,000; registered development tuning. [Actions](web-parity-actions.md), [scenarios](web-parity-scenarios.md). | P7/P8/P9: controlled randomness, ownership/protection/replacement, no-candidate cases and atomic shared settings. |
| **EXAV-08 — timetable** | Empty-stock/physical-turn variants, −999/−1000 sentinels, copying/clearing/duplicate checks and `.ftdd` rule collections. [Geometry](web-parity-geometry.md), [actions](web-parity-actions.md), [persistence](web-parity-persistence.md). | P5/P9/P10: multi-car blocked-tail/turning traces, copy isolation, masks/order and bounded inert diagram conversion. |
| **EXAV-09 — speed limits** | World `otherObjects` state, flat/slope tools and 4/3/2-minute restrictions with slower native-speed preservation. [Geometry](web-parity-geometry.md), [actions](web-parity-actions.md). | P4/P5/P9/P10: place/remove/display, infrastructure deletion, world isolation and saved continuation. |
| **EXAV-10 — platforms/passages** | Roofless thin platforms, host range 8, three passage materials, optional appearance state and 180,000 × length daily upkeep. [Geometry](web-parity-geometry.md), [gallery](web-parity-geometry-gallery.md), [persistence](web-parity-persistence.md). | P2/P4/P6/P10: host chains, old appearance defaults, timer cancellation and EXAV-GAL-001 crop decision. |
| **EXAV-11 — infrastructure economics** | Flat rail units 6,000,000/2,000,000; construction height factor −2h below ground, h+1 otherwise; slope units 40,000,000/8,000,000. Other operations have distinct bookings. [Geometry](web-parity-geometry.md), [scenarios](web-parity-scenarios.md). | P3/P4/P7/P8: endpoints/categories/occupancy and authoritative preview/commit consistency; no universal underground multiplier. |
| **EXAV-12 — train assets** | Purchase time and resale price divided by completed 365-day years + 2; absent purchase date means no refund. [EXAV-SCN-026](web-parity-scenarios.md), [persistence](web-parity-persistence.md). | P5/P8/P10: purchase/formation, placed/unplaced sale, anniversaries and legacy date defaults. |
| **EXAV-13 — terrain/removal** | Registered Mountain2 rectangle editing and voxel repair; operation-specific train-protection checks. [Actions](web-parity-actions.md), [geometry](web-parity-geometry.md). | P3/P4/P7: boundaries/terrain/occupied voxels, source booking and atomic shared commands. Map-construction capability remains included despite launcher retirement. |
| **EXAV-14 — reports/view controls** | Station list, station/train information versus continuous tracking, labels/fonts/poles, yesterday/month/year histories and April fiscal rollover. [Actions](web-parity-actions.md), [scenarios](web-parity-scenarios.md). | P2/P6/P8/P9: live bilingual controls, history continuity, picking/cuts and stable selection/focus/drafts. |
| **EXAV-15 — stadium/finance** | Event/attendance stadium demand and special listeners; bank/stocks/subsidiary values, views and persisted state. [Content](web-parity-content.md), [scenarios](web-parity-scenarios.md), [persistence](web-parity-persistence.md). | P6/P7/P8/P10: event demand, transactions/categories, histories and explicit bank-defect decisions. |
| **EXAV-16 — platform/loading** | DirectXWrapper effect boundary, plugin version/code lookup, SOAP warning and removed BZip2 decompression. [Content](web-parity-content.md), [optional loading contract](web-parity-optional-plugins.md), [persistence](web-parity-persistence.md). | P2/P9/P10/P11: portable effects/audio/trusted registrations, deterministic packs, notices and bounded import. Windows loader regressions are not the browser contract. |

## Residual source accounting and gaps

All **465 stable code files**—456 C#, six C++ and three headers—have an explicit
scope, phase owner and destination through **24 assignments**. Each generated
record retains its source hash, available lexical C# project memberships,
historical same-path comparison and references to the P0-02–08 artifacts that
mention it. **73 historical-only code paths** within the same root families
are retained separately with hashes and compatibility/native-host dispositions.
Path absence can mean relocation; it does not prove a removed feature.

This closes source **assignment**, not a semantic review of every method or
native compilation. Broad support-file assignments require feature-specific
dependency review before implementation acceptance, as required by
[incremental feature completion](web-parity-actions.md#incremental-feature-completion).
Discovery status remains `inventoried`; generated references alone do not prove
semantic equivalence. Unrecognized new plugin source fails the assignment check.

| Difference / gap | Current disposition and acceptance handoff |
| --- | --- |
| **EXAV-D01 — widths/units** | Preserve exact source formulas and normal-range vectors. P1/P8/P10 decide intermediate overflow and each migration field, including train prices that still multiply integers. |
| **EXAV-D02 — holiday braces** | Source tests for day 23 and day 30/31 escape intended month guards. EXAV-SCN-020 preserves out-of-month expectations; P1/P7 must explicitly approve any correction. |
| **EXAV-D03 — serialization/timers** | P0-05 now supplies the stable 377-declaration/1,048-field review and separate older-save layouts. P1/P6/P10 still require runtime fixtures, callback/default decisions and continuation; field names alone do not prove compatibility. |
| **EXAV-D04 — bus/delegation** | `Bus.draw` throws; exact stable C# search finds no `new Bus(`. No playable bus system is established. **Delegation is registered by `World`**, selectable through the controller collection, and included with the warning/algorithm defects. The earlier blanket source-only delegation claim is superseded by EXAV-ACTION-015. P5/P10 own null/root fallback, cycles/reparenting and saved behavior. |
| **EXAV-D05 — announced versus consumed** | `getTimer(` occurs only at its declaration. `.calcEntering(` occurs in three population wrappers/combinators; the stable station attraction consumer chain is absent. Triprange is parsed/exposed/displayed. These exact-text searches are not compiler call graphs, but do not support a dedicated stop-timer or destination-routing feature claim. New evidence must establish a consumer before expanding stable scope. |
| **EXAV-D06 — native defects/import** | P3/P4 require validated preview/commit rules; P6/P10 keep inspectors ephemeral and specify safe import wrappers. Do not reproduce native crashes or removed BZ decompression as gameplay. Source observations remain separate from approved portable behavior. |
| **EXAV-P0-09-BUILD** | Five unresolved items belong to old `core/FreeTrain.Core.csproj`: `plugin.xml`, `whatsnew.txt`, old tracking `.cs`/`.resx` paths and old `world/structs/StationListenerImpl.cs`. Two `Straight.bmp` references belong to old/2008 TAT projects. P2/P5/P9 bind portable resources explicitly. Embedded GIF identity supports narrow evidence reuse, not a successful legacy build. |
| **EXAV-P0-09-INPUTS** | The running importer still selects historical `plugins/`/`core/res/`; Docker copies those inputs. P2 must migrate reproducible reviewed bundled inputs, catalogs and notices before claiming an ExAV build. P11 checks distributions and clean/default caches. Merely editing a source pin is insufficient. |

Existing **EXAV-CONTENT-001–008**, **EXAV-ACTION-001–015**, **EXAV-GEO-01–11**,
**EXAV-GAL-001**, **EXAV-SCN-D01–04**, `TRANS-*`, `SAVE-*` and `EXAV-OPT-*`
retain their individual decisions and phase owners in the linked reviews.
In particular, catchment asymmetry/fifth-listener replacement, bank totals/order,
disconnected-train scheduling, source arithmetic, the invalid south stair crop
and optional missing/case-only resources remain acceptance blockers for their
affected slices. Integration does not replace these registers with one generic
“known issues” waiver.

## Branch and content boundaries

The branch version and distinguishing symbols were reread from the same pinned
archive commit. Branch profiles are comparison/compatibility identities, not
additional bundled denominators.

| Archive subtree | Engine / plugin family | Disposition |
| --- | --- | --- |
| `trunk/` | 2.1.1.12 / V2 | Sole stable authority; minute clock and aggregate stable passenger behavior. |
| `Beta2.2.0.1/trunk/` | 2.2.0.7 / V2 | Experiment: passenger cohorts, range-dependent partial alighting, arrival attraction and extra inspector/actions. Excluded from stable requirements; `.ftgd` extension does not make its graph stable-compatible. |
| `ALPHA3/trunk/` | 3.1.1.4 / V2 | Experiment: seconds, maxspeed banks, finer masks, `.ftgd3`, changed ranges/prices. Excluded from stable requirements; same V2 XML does not imply identical economics or timers. |
| `Beta2.0.2.11/trunk/` | 2.0.2.11 / V1 | Historical compatibility reference. |
| `Beta2.0.2.11/ALPHA3/trunk/` | 3.0.1.11 / V1 | Separate nested alpha time/save profile; never recurse it into the older beta profile. |
| `Release1.0/trunk/` | 1.0.1.0 / V1 | Historical release reference. |

Stable bundled scope stays **56 manifests / 482 direct declarations / one
generated child / 238 media**. The 42 original-only direct IDs are historical
differences, not missing bundled ExAV features. A4 and user-file external music,
the debug window and video recorder remain owner-excluded; video is absent from
stable trunk. Bundled MIDI, departure bells, ordinary diagnostics and
map-construction capability remain included. Heat/RoadSimulator/NeoFT and other
previously deferred enhancements do not become stable gates through source retention.

The [canonical optional review](web-parity-optional-plugins.md) owns the separate
non-free V2 set: 452 source manifests, **450 selected** immediate-child manifests,
11,470 selected direct declarations and 23,273 selected generated children.
Nested source-only copies explain duplicate IDs. The old 508-manifest combined
tree is not bundled scope; V1 and the older 228-manifest suite retain separate
compatibility identities, including 82 older-suite-only IDs. Full V1 conversion
and legacy runtime compatibility remain delivery work, not established by
historical counts. Pack family alone cannot determine save compatibility.

The owner-directed optional interface remains explicit startup directory
preparation/reuse and one localized credits button per enabled set, supplied by
that set's XML. No mandatory operator conversion/core rebuild is part of the
reserved contract. Non-free originals, derivatives, caches, credits and terms
stay in the separate distribution. Base CI/builds must work without either
archive or optional sibling checkout.

## Delivery handoff and verification

**EXAV-A01** and the source-specification portion of **EXAV-A03** are integrated
here and in P0-05; **EXAV-A02**'s canonical V2 source inventory/contract is covered
by P0-08, with historical families retained as compatibility work. **EXAV-A04**
is source-specified by P0-06. **EXAV-A05–08** remain P1/P2 contracts/conversion,
P3–P9 playable features, P10 import/continuation and P11 acceptance/distribution.
They are not checked off by this review.

English/Japanese wording ships with each feature, preserving ExAV Japanese
text/hash and symbol/key/ID, donor provenance, classification and review status.
MAN/DEV links and granular control/dependency semantics also ship with the
feature. Existing shell translations remain useful; no translated old rules
or plugin definition replaces ExAV numeric behavior.

All P0-01–09 **source inventory/specification/integration tasks** are now
recorded. Overall P0 stays open until its final coverage/schema/priority review
and existing UX3 handoff are accepted. UX3-14/17, A-03/A-14, UX3-GATE and native
device/IME/assistive checks remain open. P2 owns actual bundled import/build
migration; P10 owns stable-runtime and missing SOAP/diagram fixture acquisition
and continuation. No application, browser, deployment, performance or native
runtime acceptance is established by this source review.

Reproduce from the repository root using an explicit archive containing the pin:

```sh
python3 scripts/review-exav-integration.py --source-root /path/to/freetrain-exav-archive
python3 scripts/review-exav-integration.py --source-root /path/to/freetrain-exav-archive --check
python3 -m unittest discover -s scripts/tests
git diff --check
```

The checker verifies source hashes, all requirement/gap landmarks, six branch
versions, complete source assignments, historical comparisons, project/review
references and input fingerprints. Offline tests use checked-in metadata and
synthetic scope cases; they do not discover sibling repositories. Passing these
checks verifies research reproducibility and scope accounting, not gameplay parity.

Validation on 21 October: all eight ExAV source `--check` commands (discovery, content, actions, geometry, persistence, scenarios, optional V2 and integration), **105 Python tests** and `git diff --check` pass. These are source/tooling checks only.
