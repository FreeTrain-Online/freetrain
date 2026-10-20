# Parity artifact status

**Current authority: stable FreeTrain ExAV 2.1.1.12 / V2.**

| Location | Status |
| --- | --- |
| [exav-baseline.json](exav-baseline.json) | Current pinned archive source selection and approved source corrections |
| [exav-ledger.json](exav-ledger.json) | Current ExAV discovery ledger; behavior reviews and implementation remain open |
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
