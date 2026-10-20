# Stable ExAV source and coverage inventory

P0-01 discovery was reassessed and completed on **20 October 2026** against
**FreeTrain ExAV 2.1.1.12 / V2**, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, SVN r115, **`trunk/`**.
[Source selection and dispositions](parity/exav-baseline.json) and the separate
[generated ledger](parity/exav-ledger.json) are maintained in this repository.
P0-02–P0-09, the overall P0 gate, implementation and runtime acceptance remain open.

The ledger reads pinned Git objects from an explicitly supplied archive repository.
It excludes the archive's beta, alpha and Release1.0 trees. It neither imports
source/art into the application nor changes game rules, protocol, saves or build
assets. Existing original-FreeTrain outputs retain their original revision and
review decisions; none have been relabeled as ExAV evidence.
They now reside under [parity/historical/](parity/historical/README.md);
the [artifact index](parity/README.md) identifies current and superseded files.

## Fresh accounting

| Inventory | Stable ExAV discovery |
| --- | ---: |
| Files in `trunk/`, all hashed and dispositioned | 1,041 |
| Bundled manifests | 56 |
| Direct declarations, including 33 factories | 482 |
| Children derived from `GenericStructure.loadSprites` | 1 |
| Loose plugin/core media files | 238 |
| C# source files, including reference/tool/experimental roots | 456 |
| Lexical event/command bindings | 298 |
| Additional lexical controls, delegates, hooks and HTML surfaces | 1,108 |
| Serialization candidate files | 195 |
| Retained C#/native project files | 67 |
| Projects listed in `FreeTrain.2008.sln` | 33 |
| Project file/resource/reference item edges | 1,158 |

The source count comprises core 270, plugins 146, libraries 12, tools 15 and
experiments 13. All 195 serialization candidates are in core (134) or plugins
(61). Counts are discovery records, not implemented features or proven runtime
reachability. UI binding and additional surface counts overlap in what they
describe; do not add them to obtain a capability count. Native sources, `.resx`,
scripts, notices and documentation also remain in the full file denominator.

The one generated record is `null-contrib-0:0`; the stable manifests use direct
commercial/variable-height declarations where the historical ledger discovered
many generic groups. The old **116** generated-child total does not transfer.
P0-02 must still reconcile every factory's generated/alias/local registration,
resources, build bindings and saved state. This discovery count is not that
complete runtime registration graph.

## Identity, hashes and review boundaries

Coverage IDs use the **`exav-883cf7d-trunk:`** namespace. Source paths retain the
actual archive prefix **`trunk/`**; original comparison paths remain relative to
their original snapshot. Contribution and plugin IDs are preserved verbatim.
The ledger records repository/revision/subtree, engine version, plugin family,
configuration and generator hashes, a full-file-set fingerprint, per-file SHA-256
and Git blob IDs. Dependency targets and project memberships link back to those
hashed files. Relative resource references retain their source spelling.

Every coverage record has scope/reachability, review ownership, dependencies,
behavior, modern destinations, fixtures, evidence, differences and status.
Discovery assigns **`inventoried`**, never implemented or verified. Empty modern
destinations/fixtures/evidence are unassigned. Translation references identify
the ExAV file/hash and contribution or source identity; per-string Japanese keys,
English donor text, wording and review status remain P0-02/P0-03 work.

Project membership is extracted from both older Visual Studio XML and MSBuild
projects, retaining conditions and distinguishing the selected 2008 solution from
other projects. Conditions, imports, build events and C# expressions are not
executed. Inclusion in a project is not proof that a caller or dynamic plugin
registration is reachable. A file without project membership is retained for
review rather than silently excluded. Complete semantic dependency closure,
action destinations and serializer fields/callback behavior belong to the
subsequent reviews; this ledger supplies their pinned inputs.

At the pinned archive commit, all 56 manifests declare Shift-JIS and are decoded
strictly as Windows CP932, preserving Japanese text and hashing the original bytes.
**EXAV-ENC-001 — owner-approved conversion, 20 October 2026:** all 56 stable
checkout manifests now use **UTF-8 without BOM**, including their XML declarations.
Japanese text, entities, all 482 direct declarations and line endings are preserved.
The fallback metadata reader in `trunk/core/framework/plugin/Plugin.cs` now uses
`Encoding.UTF8`; normal XML loading already honors the declared encoding.
The conversion is applied to the working tree, without committing or repinning.
[Conversion evidence](parity/exav-manifest-encoding-review.json) records original
and corrected hashes for every manifest and the fallback reader. Future ported
manifests use UTF-8; pinned historical reproduction retains its CP932 support.

Entity expansion retains
the scanner's size/depth limits and rejects external DTDs/entities and parameter
entities. No legacy executable, assembly or serialized object is activated.

## Findings to carry into the next reviews

| Finding | Disposition / next owner |
| --- | --- |
| 351 shared direct IDs, **131 ExAV-only**, **42 original-only** | ID comparison only. Shared identity does not establish equivalent definitions or behavior. Original-only IDs are historical differences, not missing stable content. P0-02/P0-09. |
| Only **3 of 456 C# files** are byte-identical at the same path in original `1f755ca`; 425 differ and 28 paths are absent there | The unchanged files are terrace `AssemblyInfo.cs`, `MountainDisambiguator.cs` and `Strategy.cs`. Encoding/line endings also affect these byte comparisons; do not infer 425 semantic changes. Even identical bytes need dependency review before reusing conclusions. P0-02–05. |
| Pinned stable `GenericStructureContribution.parseDirection` recognizes **`SOURTH`**, not `SOUTH` | **EXAV-DIR-001 — owner-approved correction, 20 October 2026:** treat `SOURTH` as a source typo and use **`SOUTH`** for the south direction in the port's parser and content. The ExAV-only scanner preserves the source token and behavior as evidence, pinned to loader SHA-256 `978c938267a06309118096dbb6ee768b2e03c2003610c939123dea7fb205792d`. `ALL` assigns cardinal entries; absence of direction elements supplies the fallback. The bundled generic null entry has no direction element, so this is not evidence of a visible bundled direction failure. The stable archive checkout now has the `SOUTH` correction applied in its working tree (not committed or repinned); the corrected file hash is recorded in `exav-baseline.json`. Carry the correction into P0-02/P0-04 and optional-pack compatibility. |
| A4 music and debug-window manifests are present but owner-excluded | Keep them in the denominator with explicit exclusions. The old recorder manifest is absent from stable; do not manufacture a missing-content requirement. Bundled music and useful player diagnostics retain their separate scope. |
| 75 missing manifest resource paths, all DLL references; no discovered contribution collisions or unknown factories | Preserve discovery diagnostics. DLL paths refer to absent assembly files; P0-02 must prove qualified type/project/build-output resolution before resolving them. No loose artwork path is missing in this manifest scan. |
| Seven unresolved project items | Five belong to old `core/FreeTrain.Core.csproj`: `plugin.xml`, `whatsnew.txt`, `controllers/rail/TrainTrackingWindow.cs`, its `.resx`, and `world/structs/StationListenerImpl.cs`. Two are `res/Straight.bmp` in the old and 2008 TAT projects. Keep old-project and selected-solution gaps distinct; historical GIF/ImageList review does not automatically close the ExAV TAT gap. P0-02/P0-03. |
| Every selected-solution project path exists; two core C# files lack selected-solution item membership | Retain `core/contributions/common/EntityBuilderContribution.cs` and `core/world/structs/TwoDimStructure.cs` as source/build questions. This does not establish runtime availability or an exclusion. P0-02/P0-03/P0-05. |

The ExAV ledger deliberately does **not** apply `action-map.json`, content rules,
geometry tables or serialization decisions from the original baseline. The
[content](web-parity-content.md), [action](web-parity-actions.md),
[geometry](web-parity-geometry.md) and [persistence](web-parity-persistence.md)
documents remain historical semantic reviews awaiting ExAV reconciliation.
Manual/wiki workflows remain evidence to reconcile against these source inputs.
The canonical non-LGPL optional V2 set is outside this ledger and remains P0-08;
no optional repository is discovered or read. EXAV-ENC-001 changes only stable
`trunk/plugins/**/plugin.xml` and its fallback reader; historical archive branches
and optional plugin repositories retain their original encodings.

## Reproduction and checks

From this repository root, with Python 3.9 or newer and an explicit archive Git
repository containing the pinned commit:

```sh
python3 scripts/inventory-parity.py --source exav --source-root /path/to/freetrain-exav-archive
python3 scripts/inventory-parity.py --source exav --source-root /path/to/freetrain-exav-archive --check
python3 scripts/inventory-parity.py --source exav --source-root /path/to/freetrain-exav-archive --report tmp/parity-p0/exav-inventory.md

# Historical outputs keep their source and review family.
python3 scripts/inventory-parity.py --source historical --check

# Includes offline ExAV metadata/accounting and isolated Git-subtree tests.
# Does not require or search for an archive or optional sibling repository.
python3 -m unittest discover -s scripts/tests -v
```

`--source exav` requires `--source-root` and writes/checks only
`doc/parity/exav-ledger.json`; it rejects historical action-review and optional
extra-suite arguments. Omitting `--source` preserves historical generation.
Ordinary builds/CI use the checked-in metadata and tests without a developer's
archive path. Fresh ExAV regeneration is an explicit research check until the
separate reviewed import/build-input migration is delivered. The offline tests
check metadata integrity and source-selection behavior; they cannot replace
regeneration against the archive. No runtime, visual or save-continuation
acceptance is claimed by these source/tool checks.
