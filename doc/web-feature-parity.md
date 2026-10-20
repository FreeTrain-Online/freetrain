# Web parity coverage inventory

**Authority replaced — 20 October 2026:** the current parity source is **FreeTrain ExAV 2.1.1.12**, repository `/Users/dmark/dev/freetrain-exav-archive` at **`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`** (SVN r115), **subtree `trunk/`**, including its bundled `plugins/`. This repository's old `core/`, `plugins/`, archived desktop source and original-FreeTrain `1f755ca` are historical comparison and English-translation donors only. They do not define current game behavior, bundled coverage or a second parity target.

**P0-01 discovery, P0-02 source reconciliation, revised P0-03 workflow/scope reconciliation and P0-04 source specification completed — 20 October 2026; P0-05–P0-09 and the overall P0 gate remain open.** The separate [stable ExAV inventory](web-parity-exav-inventory.md), [source selection](parity/exav-baseline.json) and [ledger](parity/exav-ledger.json) now pin and inventory archive `trunk/`: 1,041 files, 56 manifests / 482 direct declarations, 456 C# files, 298 lexical UI bindings and 195 serialization candidates. The [ExAV content review](web-parity-content.md) now reconciles all bundled declarations/resources, source/project bindings, recipes/state and XML translation candidates. [P0-03 reconciliation](web-parity-actions.md) assigns 87 workflow families, all 483 declaration/generated-child consumers and 56 bundled dependency closures, with 15 reviewed differences. Per-control/dependency detail, granular reciprocal MAN/DEV links and translation classification are mandatory in each implementation slice; see [incremental feature completion](web-parity-actions.md#incremental-feature-completion). Neither discovery nor source reconciliation establishes implementation/runtime parity.

**P0-04 source specification complete — 20 October 2026:** the
[geometry/clock/train specification](web-parity-geometry.md),
[four-chain dependency review](web-parity-geometry-dependencies.md) and
[expanded gallery review](web-parity-geometry-gallery.md) reconcile stable ExAV
with 98 distinct reviewed files. Separate generated tables retain 60 patterns,
16 supports, level/slope poses and attachments; source deltas cover costs,
forwarding, speed limits, platforms and drawing. The 152 rail/car studies remain
separate from 332 expanded crop records and 1,176 expanded compositions. Six raw
and 12 composition records explicitly diagnose the source-invalid stair crop;
no correction or native pixel outcome is inferred. All 28 contact sheets were
inspected; 88 Python tests and ExAV/historical reproducibility checks pass.
**P0-04 is closed at source-specification level.** P0-05–09, overall P0 and
implementation/runtime/native/save-continuation gates remain open.

The existing `parity/historical/baseline.json`, `base-ledger.json`, action/content reviews, geometry/scenarios and serialization/transition reports below still pin the original-FreeTrain source and earlier implementation revisions. They retain their historical provenance and have **not** been repinned. Passing their historical `--check` commands verifies historical reproducibility, not ExAV coverage. Reuse reviewed results only after checking ExAV symbols and their dependencies. See the [complete P0 reassessment](../tmp/web-feature-parity-checklist.md#p0-reassessment-and-repository-recommendation--20-october-2026) and [ExAV source assessment](../tmp/web-feature-parity-exav-delta.md).

The original Windows manual, online manual and developer wiki remain historical workflow/authoring evidence. Reconcile their claims and `MAN-*` requirements with stable ExAV source and owner decisions before treating them as current behavior. The delivered browser UX/multiplayer contracts remain implementation requirements, independently of historical game-source authority. `freetrain-modern/main` remains a useful modern Avalonia reference for its full toolbar, building catalog, rail laying and other implemented UI actions.

ExAV is Japanese-only. Carry existing English translations over during each feature slice, keyed to ExAV symbols/resource keys or contribution IDs with Japanese source text/hash and English donor/review status. Keep rules/numeric data and stable identities from ExAV. Existing bilingual shell text remains reusable; changed/new catalog, controls, errors, reports and help need explicit English/Japanese coverage. Translation donors are not gameplay or plugin-definition authorities.

Keep LGPL bundled ExAV content separate from the non-LGPL V2 additional set, canonical `/Users/dmark/dev/freetrain_online_extra_plugins` at observed **`c8ec62e47a7f5089b666e57072164a0e14934466`**, plugin root `plugins/`. Preserve separate inventories, generated outputs, pack identities, notices and packages. Prior V2-copy counts/hashes remain historical until reconciled against this canonical repository. Default builds must not discover/include optional sibling content automatically.

The current ExAV discovery outputs are:

| File | Meaning |
| --- | --- |
| [exav-baseline.json](parity/exav-baseline.json) | Archive commit/subtree, stable engine/V2 identity, root/scope dispositions and generated-registration source review |
| [exav-ledger.json](parity/exav-ledger.json) | Separate namespaced source/content/UI/save discovery, hashes, project memberships/dependencies, translation references and open review fields |
| [Bundled content reconciliation](web-parity-content.md), [ExAV review](parity/exav-content-review.json), [ExAV rules](parity/exav-content-rules.json) | Current P0-02 source accounting, recipes/effects/state, XML text inventory and explicit handoffs; historical review retained below |
| [ExAV discovery report](web-parity-exav-inventory.md) | Fresh accounting, source differences, build gaps, limits and explicit-source reproduction commands |
| [Player action reassessment](web-parity-actions.md), [rules](parity/exav-action-rules.json), [review](parity/exav-action-review.json) | Current P0-03 scope closure: 87 workflow families / 46 consumer groups, all 298 bindings / 1,108 surfaces / 483 contribution decisions; text/control details are slice obligations |
| [Manifest encoding conversion](parity/exav-manifest-encoding-review.json) | EXAV-ENC-001: separate working-tree evidence for 56 Shift-JIS → UTF-8 manifests and the UTF-8 fallback metadata reader; pinned archive provenance remains unchanged |

Superseded original-FreeTrain artifacts and supplemental historical evidence now
live under [parity/historical/](parity/historical/README.md). Their source pins,
IDs and review status retain their original meaning. The
[artifact index](parity/README.md) distinguishes them from current ExAV outputs.
The reference artifacts below retain historical provenance except where explicitly marked ExAV:

| File | Meaning |
| --- | --- |
| [baseline.json](parity/historical/baseline.json) | Historical original-FreeTrain revisions/counts/root dispositions; its old “canonical” labels do not select the current ExAV authority |
| [base-ledger.json](parity/historical/base-ledger.json) | All 54 manifests, declarations/generated children, resources, source files, UI bindings and serialization candidates |
| [base-content-review.json](parity/historical/base-content-review.json) | P0-02 source reconciliation: complete recipes, build mappings, aliases, resource graph, notices and contribution/instance state evidence |
| [content-rules.json](parity/historical/content-rules.json) | Maintained decoding/crop/anchor/effect/state rules and explicit difference dispositions |
| [extra-suite-ledger.json](parity/historical/extra-suite-ledger.json) | Separate metadata inventory for the 228-manifest non-free suite; no artwork or binaries |
| [action-map.json](parity/historical/action-map.json), [action-review.json](parity/historical/action-review.json) | Complete P0-03 source mappings and generated control/contribution/manual evidence; missing gameplay stays explicit |
| [Player action reconciliation](web-parity-actions.md) | Workflow destinations, dynamic registration routes, configuration/gesture review and source-only dispositions |
| [Manual workflow oracle](web-parity-manual.md) | Chapters 2–4: toolbar/menu/task-window requirements, source differences and pending acceptance cases |
| [Online manual comparison](web-parity-online-manual.md), [snapshot inventory](parity/historical/online-manual-review.json) | All 16 saved pages, individual edit/capture dates and hashes; added details, source gaps and MAN-O01–16 / MAN-A17–22 follow-through |
| [geometry.json](parity/historical/geometry.json) | Source-derived eight directions, 60 rail patterns, 16 support slices and traffic draw order |
| [Geometry/clock/train specification](web-parity-geometry.md), [ExAV geometry](parity/exav-geometry.json), [behavior tables](parity/exav-geometry-behavior.json) | P0-04 source specification complete: initial 56-file/11-delta review, four dependency chains and expanded gallery; 98 distinct reviewed files, 152 rail/car studies plus separately counted expanded records and explicit source diagnostics |
| [scenarios.json](parity/historical/scenarios.json) | Initial independently calculated acceptance examples; implementation status stays explicit |
| [Domain transition design](web-parity-domain-transition.md) | Draft spatial/time/save/pack boundaries and migration policy |
| [Contract/content/persistence specification](web-parity-persistence.md), [transition decisions](parity/historical/domain-transition.json), [source evidence](parity/historical/domain-transition-review.json) | P0-05: eight-direction/version boundaries, explicit prototype compatibility mode/reference disposition, pack requirements and authority/draft/readiness obligations; implementation remains P1/P2/P10 |
| [Serializer type/field inventory](parity/historical/serialization-review.json) | P0-05: all 186 candidate files, 359 declarations, 903 fields, nested identities/inheritance, custom serializers/callbacks and explicit exclusions; not a compiler, importer or runtime compatibility verdict |

The following details and reproduction commands describe the historical review
family. Use the [separate ExAV commands](web-parity-exav-inventory.md#reproduction-and-checks)
for current-source discovery. Run historical commands from the repository root
with Python 3.9 or newer; they do not validate ExAV behavior:

```sh
# Base generation/checks never discover or read a sibling checkout.
python3 scripts/inventory-parity.py
python3 scripts/inventory-parity.py --check
python3 scripts/reconcile-parity-content.py --check
python3 scripts/parity-rail-gallery.py --check
python3 scripts/review-parity-persistence.py --check
python3 -m unittest discover -s scripts/tests -v

# Explicit optional metadata inventory; source directory is configurable.
python3 scripts/inventory-parity.py --extra-root /path/to/freetrain_plugins
python3 scripts/inventory-parity.py --extra-root /path/to/freetrain_plugins --check

# Temporary review outputs, never release assets.
python3 scripts/inventory-parity.py --report tmp/parity-p0/inventory.md
python3 scripts/inventory-parity.py --action-report tmp/parity-p0/action-reconciliation.md
python3 scripts/reconcile-parity-content.py --report tmp/parity-p0/content-reconciliation.md
python3 scripts/parity-rail-gallery.py --gallery tmp/parity-p0/rail-gallery
```

The generator reads pinned Git objects, including archived original plugin
paths. It does not load .NET assemblies or reinterpret the current runtime catalog.
The extra suite's Fuji TV manifest has been renamed to `plugin.xml`; imports do
not need compatibility handling for its former uppercase extension. The pinned
historical inventory retains `plugin.XML` as the original source path at its
recorded revision. A full generation must reproduce byte-for-byte
under `--check`. Generated ledgers store one record per line for manageable diffs;
all text remains literal UTF-8.

Each pack supplies its baseline revision for every contained record. Coverage IDs
identify the original path, manifest declaration ordinal/ID, generated child ID or
UI binding. Records carry dependencies, reachability, scope, behavior, modern
destinations, fixtures, evidence, status and differences. Source/resource records
also carry SHA-256 hashes. Empty destinations/fixtures/evidence mean **unassigned**,
never implemented or verified. `inventoried` is the only implementation status
assigned by discovery. Future reviewed status changes belong in maintained review
inputs, rather than manual edits to generated output. The action map is the first
such input; its mapping status is separate from implementation status.

Declarations of type `contribution` register factories, not catalog items.
`GenericStructureContribution.loadSprites` registers a primitive for each
color × sprite-direction pair using `{parent}-{color}:{direction}`. Children
have their own records. Its eight-entry direction fallback table is preserved;
height variations are recipes rather than additional registered IDs. Enumeration
does not prove that every recipe constructs successfully. P0-02's separate content
review now specifies half-voxel combinations, train composition/reversal/recolors,
implicit resources and crop/anchor/effect rules, retaining complete ordered
declaration trees and source hashes. Conversion and runtime fidelity remain open;
neither scanner is a complete importer.

Source and UI scanning is lexical. It finds class/interface/enum names, serialization
markers, selected event subscriptions and `Command` registrations. It is not a C#
semantic parser, complete nested-type graph or proof of reachability. P0-03's
[separate source reconciliation](web-parity-actions.md) now reviews all 248 bindings,
985 additional surfaces across 180 source reviews, and dynamic declaration/generated
routes. Explicit reviewed reachability, missing destinations, adaptations and
source-only/excluded dispositions remain distinct from implementation status.
All 45 manual requirement families have reciprocal source/workflow links. No
implemented Undo/Redo binding was found in the reviewed baseline core/lib C# source;
do not add it to scope without a reachable implementation.

P0-05's separate serializer inventory now expands every candidate file into
nested declarations/fields and resolved source inheritance, retaining custom
writer keys/type substitutions, excluded fields, callbacks and unresolved
framework bases. Its explicit [mapping rules](web-parity-persistence.md) and
opt-in local ExAV record review supersede the initial file-only state scan.
They do not establish compiler reachability, exact CLR wire layouts, safe service
import or historical continuation. The owner sample remains outside base CI and
runtime assets; default generation does not search local saves or optional packs.

Resource edges distinguish exact/case-folded matches, missing resources and external
references. Missing DLL paths frequently describe historical build outputs whose
source remains present; these are review diagnostics, not evidence of missing art.
Do not suppress the diagnostics or count them as resolved solely because a source
file has a similar name. The content review maps all 76 base DLL edges by exact
qualified type and project compile membership, keeping discovery diagnostics
intact. Per-resource manifest credits, notice hashes and unused/alternative file
dispositions are retained there. No individual image authorship is inferred from
a plugin credit. The extra suite's resource/permission review remains separate.

The extra suite is optional, separately inventoried and non-free under its authors'
permissions. Its collection README is permission/attribution evidence, not an LGPL
grant or proof of permission for every future browser derivative. The optional
ledger contains IDs/hashes/dependency/recipe metadata only, is not a prepared pack,
and is outside the runtime content/build inputs. No originals, DLLs, converted
images or visual captures from that suite are copied into the base distribution.
Default inventory generation works without that repository. Real asset conversion,
separate pack artifacts and startup loading are later deliverables.

Preserve the existing [UX contract](web-game-ux.md),
[multiplayer authority/lifecycle rules](web-multiplayer-operation.md), and protocol,
content and Save v1 compatibility until a reviewed replacement exists. P0 inventory
and specifications can proceed while physical-device acceptance remains unverified;
gameplay implementation follows the existing UX handoff gate.

Browser test runs create a fresh evidence directory under the checkout's temporary
directory. `FREETRAIN_ONLINE_BROWSER_EVIDENCE` selects an explicit run directory, with
`ux1`, `ux2`, `ux3` subdirectories and the network measurement. Existing
`FREETRAIN_ONLINE_UX1_EVIDENCE`, `FREETRAIN_ONLINE_UX2_EVIDENCE` and `FREETRAIN_ONLINE_UX3_EVIDENCE`
overrides are honored. Use new directories for acceptance runs: the old fixed
date defaults reused historical captures. The browser harness now isolates its
default output so later runs preserve prior evidence.
