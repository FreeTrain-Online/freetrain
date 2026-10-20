# Parity artifact status

**Current authority: stable FreeTrain ExAV 2.1.1.12 / V2.**

| Location | Status |
| --- | --- |
| [exav-baseline.json](exav-baseline.json) | Current pinned archive source selection and approved source corrections |
| [exav-ledger.json](exav-ledger.json) | Current ExAV discovery ledger; behavior reviews and implementation remain open |
| [exav-content-rules.json](exav-content-rules.json) | Maintained P0-02 ExAV recipe/effect/state rules, differences and translation policy |
| [exav-content-review.json](exav-content-review.json) | P0-02 source reconciliation; complete bundled accounting and XML text inventory, runtime parity unverified |
| [exav-action-rules.json](exav-action-rules.json), [exav-action-review.json](exav-action-review.json) | P0-03 workflow/scope reconciliation complete: 15 deltas, 87 workflow families, all 483 consumer decisions and 56 bundled dependency closures; granular controls, MAN/DEV and translation classification required per slice |
| [exav-geometry-rules.json](exav-geometry-rules.json), [exav-geometry.json](exav-geometry.json), [exav-geometry-behavior.json](exav-geometry-behavior.json) | P0-04 reassessment: 56 reviewed source/manifest files, 11 deltas, pinned comparisons and regenerated tables; four dependency chains reviewed; expanded visual review remains open |
| [exav-geometry-dependency-review.json](exav-geometry-dependency-review.json) | Four source dependency chains complete: 63 pinned files, 14 findings and 33 acceptance descriptions; 97 distinct files with initial geometry review. Full feature/runtime acceptance remains with implementing slices |
| [exav-manifest-encoding-review.json](exav-manifest-encoding-review.json) | Separate evidence for the approved UTF-8 working-tree conversion |
| [historical/](historical/README.md) | Superseded original-FreeTrain parity artifacts, earlier optional-suite inventory and historical manual evidence |

The historical files were relocated on **20 October 2026** to distinguish them
from the current ExAV inputs. Keep their original source revisions, hashes, IDs
and scope. They remain useful comparison, translation and regression evidence;
their completion status does not establish ExAV parity. Maintained tools still
reproduce them in `historical/` using their original inputs.

See the [source policy](../web-feature-parity.md) and
[ExAV inventory report](../web-parity-exav-inventory.md) for review boundaries
and reproduction commands. New ExAV artifacts belong at this level with an
`exav-` name; historical records remain under `historical/`.
