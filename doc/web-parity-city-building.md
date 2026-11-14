# P7-09 playable populated buildings — 14 November 2026

The **City buildings** tool (`C`) exposes bundled fixed building definitions through
English/Japanese catalog selection, authored variants and footprints, map or
coordinate placement, authoritative review, Confirm/Cancel, construction progress,
inspection and whole-owner removal. This closes the first playable producer slice;
variable-height/half-voxel families, full catalog reconciliation, provider refresh
and automatic growth retain P7-10–14/25–29 ownership.

The behavior authority remains stable ExAV 2.1.1.12, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`. The
[source contract](web-parity-city-contract.md), [fixed domain](web-parity-fixed-city.md)
and [presentation](web-parity-city-rendering.md) supply the existing source-derived
construction, ownership, accounts, timing, population and art. CITY-D013/020's
reviewed direct catalog adaptation uses positive empty-volume legality and charges
the authored selector price. This does not enable commented/inverted native tools.

`POST /api/city/fixed/preview` returns a bounded `FixedCityPreview`; the
`editFixedCity` command rechecks epoch, geometry revision, source minute, content,
owner/revision for removal and exact expected cost. Preview operates on detached
clock/random/account state, including first heat activation. Only a successful
commit publishes it. Legal construction occupies the complete volume; source
workdays and 09:00–17:59 callbacks advance the site without a completion shortcut.
Completion creates a fresh owner and actual city provider; the inspector follows
that specific site-to-completed-owner handoff. Deletion, rebuilding or world
replacement cannot silently select a different structure.

The shared Remove tool resolves any occupied fixed-building member, including
construction sites. A completed 1×1 house quotes ¥500,000 demolition; an unfinished
site quotes zero. Removal detaches the actual station provider, cancels owner
callbacks and books the reviewed cost atomically. Placement and city demolition
quotes remain visible beside sticky confirmation controls on narrow screens.
Authored orientation/variation choices select exact prepared definitions rather
than rotating an inferred footprint. Variable/half definitions are excluded.

All 66 bundled fixed definitions have catalog bindings and prepared previews.
71 source-bound name/group translations were checked against pinned Japanese and
historical English donors, including correcting “Single condo” to “Detached house”.
Original Japanese, identities, author/plugin metadata and raw assets remain intact;
[the translation review](parity/city-building-localization-review.json) records exact
keys. This is not gameplay acceptance for every fixed family or the complete P7
catalog gate. Save11/protocol10/City1 and prepared pack hashes stay unchanged; the
query/command extension adds no persisted state.

Fresh focused verification:

- **39 .NET tests pass**: FixedCitySession, FixedCityConstruction, CityConstruction
  and StationBuildingSession. They cover pure activation previews, role/cost/minute
  rejection, legal/blocked geometry, real natural completion, exact reopen,
  demolition booking, callback cancellation and both provider membership directions.
- **48 web tests pass**: fixed city editing, city state/rendering, station building
  editing, shared removal, shell model and localization. Generated contracts,
  content, locale validation, Svelte diagnostics and the production build pass.
- **14 selected Chromium checks pass**: three P7-09 gameplay/guard scenarios, six
  catalog scenarios, four station build/remove regressions and the EN/JA DPR2
  construction-inspector continuation scenario. The final cost-visibility rerun
  is recorded in the validation manifest. No complete suites were run.

The EN desktop/keyboard and JA 430×860 touch scenarios start from a production
SaveCodec empty-world seed at source Jan 2 09:00 with no city/providers. Each
constructs a real P6 station and the bundled 1×1×1 ¥11,000,000 detached house
`{27D6D0C0-2B1A-4494-9257-14C208168075}` through gameplay. Shared Run at 2× advances
natural source callbacks. The completed provider is `city-fixed` with ten residents;
its independently copied ResidentialPopulation holiday 09–17 outgoing table is
`[4,5,4,4,3,3,4,4,3]` after percentage truncation. One nearby station and the source
40 cap preserve that exact demand. Tests then pause, verify demand, save/reopen
byte-for-byte, inspect, demolish and rebuild with a new identity while an Operator
peer observes publication. No passenger editor, rail-owner supply or test completion
command supplies this demand.

Separate guards exercise forged Observer commands, stale minutes, canceled late
responses and an EN→JA locale switch. Existing catalog/station regressions cover
peer conflicts, disconnection, world replacement, keyboard and narrow browsing.
The new site-to-owner inspector test keeps both EN/JA inspectors open across actual
source completion. The focused browser setup also regenerates existing production
fixtures through 14 selected .NET fixture cases; that is separate from the 39-test
city regression run.

Reproduction and raw logs/capture hashes are in local ignored
`tmp/p7-09-validation.json`, `tmp/p7-09-*.log` and
`tmp/p7-09-browser-evidence/`. Human/original-runtime pixel fidelity, native/device,
optional packs, capacity/performance and overall P7 acceptance remain qualified by
their existing owners. This slice introduces no capacity or performance claim.
