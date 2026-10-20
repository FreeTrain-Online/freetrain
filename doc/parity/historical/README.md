# Historical parity evidence — superseded as current authority

Moved here on **20 October 2026**. These records describe original FreeTrain
`1f755ca6f291253b8cd5024ef53402b85d467170`, its earlier browser implementation
reviews, the earlier optional suite or historical manual sources. They do not
select the current game rules/content or close stable ExAV review gates.

| Files | Retained role |
| --- | --- |
| [baseline.json](baseline.json), [base-ledger.json](base-ledger.json) | Original-FreeTrain pins, root dispositions and generated discovery |
| [content-rules.json](content-rules.json), [base-content-review.json](base-content-review.json) | Original content decoding, recipes, resources, notices and saved-state review |
| [action-map.json](action-map.json), [action-review.json](action-review.json) | Original action/configuration decisions and generated evidence |
| [geometry.json](geometry.json), [geometry-behavior.json](geometry-behavior.json) | Original source-derived geometry/clock/train expectations |
| [scenarios.json](scenarios.json) | Original source-derived scenario seeds; not current ExAV expectations |
| [domain-transition.json](domain-transition.json), [domain-transition-review.json](domain-transition-review.json), [serialization-review.json](serialization-review.json) | Earlier contract/save design and original serializer evidence |
| [extra-suite-ledger.json](extra-suite-ledger.json) | Earlier separately pinned 228-manifest non-free suite; distinct from the [canonical optional V2 P0-08 review](../../web-parity-optional-plugins.md); older-suite runtime compatibility remains open |
| [online-manual-review.json](online-manual-review.json) | Historical manual snapshot provenance; still useful workflow evidence to reconcile against ExAV |

Each JSON file retains its original provenance and IDs. Only artifact-location
references change with this move. Old “canonical”, “complete” and similar labels
apply to the recorded historical input. Reuse conclusions only after reviewing
the corresponding ExAV source and dependencies. Moving these files does not
remove compatibility, optional-suite or manual reconciliation obligations.

From the repository root, the existing historical checks now read/write this
directory:

```sh
python3 scripts/inventory-parity.py --source historical --check
python3 scripts/reconcile-parity-content.py --check
python3 scripts/parity-rail-gallery.py --check
python3 scripts/review-parity-persistence.py --check
```

The optional-suite ledger requires its explicitly supplied pinned repository
for regeneration/checking, as before. Default checks do not discover siblings.
Use the [current artifact index](../README.md) and
[source policy](../../web-feature-parity.md) for the ExAV authority.
