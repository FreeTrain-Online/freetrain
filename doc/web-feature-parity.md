# Web parity coverage inventory

The canonical game/content baseline is legacy `main` at the commit pinned in
[baseline.json](parity/baseline.json). The implementation comparison is pinned
separately at `a48e8fa` (including Credits and License). Inventory is P0 work;
it does not establish legacy behavior parity or close native/device UX acceptance.

The owner's original Windows manual, **chapters 2–4**, and the
[later online-manual comparison](web-parity-online-manual.md) are the player-facing
oracle for baseline workflows and toolbar/task-window structure. The maintained
[manual requirements and browser mapping](web-parity-manual.md) records provenance,
all documented menu families, focused windows, stable `MAN-*` requirements,
acceptance obligations and explicit source/prose differences. Use it alongside
the pinned source; it does not replace the source ledger, exclude later bundled
features or close the complete P0-03 action-map gate.

The maintained inputs and outputs are:

| File | Meaning |
| --- | --- |
| [baseline.json](parity/baseline.json) | Revisions, expected manifest counts and root dispositions |
| [base-ledger.json](parity/base-ledger.json) | All 54 manifests, declarations/generated children, resources, source files, UI bindings and serialization candidates |
| [base-content-review.json](parity/base-content-review.json) | P0-02 source reconciliation: complete recipes, build mappings, aliases, resource graph, notices and contribution/instance state evidence |
| [content-rules.json](parity/content-rules.json) | Maintained decoding/crop/anchor/effect/state rules and explicit difference dispositions |
| [Base content reconciliation](web-parity-content.md) | P0-02 accounting, source findings and boundaries of the evidence |
| [extra-suite-ledger.json](parity/extra-suite-ledger.json) | Separate metadata inventory for the 228-manifest non-free suite; no artwork or binaries |
| [action-map.json](parity/action-map.json) | Reviewed main-window operation mappings and remaining differences |
| [Manual workflow oracle](web-parity-manual.md) | Chapters 2–4: toolbar/menu/task-window requirements, source differences and pending acceptance cases |
| [Online manual comparison](web-parity-online-manual.md), [snapshot inventory](parity/online-manual-review.json) | All 16 saved pages, individual edit/capture dates and hashes; added details, source gaps and MAN-O01–16 / MAN-A17–22 follow-through |
| [geometry.json](parity/geometry.json) | Source-derived eight directions, 60 rail patterns, 16 support slices and traffic draw order |
| [scenarios.json](parity/scenarios.json) | Initial independently calculated acceptance examples; implementation status stays explicit |
| [Domain transition design](web-parity-domain-transition.md) | Draft spatial/time/save/pack boundaries and migration policy |

Run from the repository root with Python 3.9 or newer:

```sh
# Base generation/checks never discover or read a sibling checkout.
python3 scripts/inventory-parity.py
python3 scripts/inventory-parity.py --check
python3 scripts/reconcile-parity-content.py --check
python3 scripts/parity-rail-gallery.py --check
python3 -m unittest discover -s scripts/tests -v

# Explicit optional metadata inventory; source directory is configurable.
python3 scripts/inventory-parity.py --extra-root /path/to/freetrain_plugins
python3 scripts/inventory-parity.py --extra-root /path/to/freetrain_plugins --check

# Temporary review outputs, never release assets.
python3 scripts/inventory-parity.py --report tmp/parity-p0/inventory.md
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
semantic parser, complete nested-type graph or proof of reachability. Designer
bindings, dynamic menu/toolbar construction, manifest-driven actions and plugin
forms must be reconciled before the complete action-map gate closes. No implemented
Undo/Redo binding was found in the reviewed baseline core/lib C# source; do not
add it to scope without a reachable implementation.

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
directory. `FREETRAIN_BROWSER_EVIDENCE` selects an explicit run directory, with
`ux1`, `ux2`, `ux3` subdirectories and the network measurement. Existing
`FREETRAIN_UX1_EVIDENCE`, `FREETRAIN_UX2_EVIDENCE` and `FREETRAIN_UX3_EVIDENCE`
overrides are honored. Use new directories for acceptance runs: the old fixed
date defaults reused historical captures. The browser harness now isolates its
default output so later runs preserve prior evidence.
