# P7-12 complete ordinary building catalog — 15 November 2026

The City buildings category exposes all **129** bundled ordinary building
identities through **50** source-art cards: 66 fixed, 55 variable-height and eight
half-voxel definitions. These use the P7-09–11 construction/removal paths and the
reviewed CITY-D013/020 direct-placement adaptation. P7 acceptance remains open.

## Source and registration reconciliation

Stable ExAV 2.1.1.12, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, remains the behavior authority.
`StructureContribution`, `EntityBuilderInternal`, `CommercialStructureContribution`,
`VarHeightBuildingContribution`, `HalfVoxelContribution` and their registered XML
supply the identities, player/computer eligibility, group/subgroup, authored art
and caller-specific economics. The prepared catalog and canonical city profile
are unchanged. Cards group only definitions with matching plugin, handler,
source group/subgroup, display family, unordered footprint, height range, unit
price and automatic eligibility. Every numbered choice retains its exact ID,
ordered footprint, prepared recipe and owning plugin attribution.

| Registration/content | Catalog disposition |
| --- | --- |
| 66 Commercial/fixed definitions | All manually selectable; exact authored designs/orientations and timed construction. |
| 55 variable definitions | All manually selectable, including baked recolors; authored bottom/middle/top stacks and min/max heights. Four displayed floors equal one stored voxel. |
| Eight half definitions | All selectable; only source-supported Back/Front, cardinal direction and color combinations. Bundled color is 0, so there is no color control. |
| Eight `computerCannotBuild` definitions | Six harbor and two castle definitions remain manually selectable, labeled **Manual placement only**. Other definitions display automatic eligibility; this does not enable automatic growth. |
| GenericStructure / generatedStructure | The sole bundled generated child `null-contrib-0:0` remains hidden and has no legal volume. It adds zero playable catalog entries. |
| Generic CategoryTree | All 252 original category declarations/IDs/aliases/icons/byid/byname/hide data remain retained in prepared source. Only the hidden null child refers to this tree in bundled generic building content; no playable membership is invented. Ordinary cards expose their actual XML group/subgroup, with absent fields explicitly unspecified. |
| Stadium, ground, roads, fences and empty extension registries | Retain their distinct P7/P8/P9 owners from P7-11. They are not ordinary building cards. |
| Source-only/uncompiled builders and optional/generated suites | Keep their P7-01/P7-11/P2/P7-33 dispositions and separate evidence. No inferred generated buildings or optional runtime credit. |
| Reachable color-test authoring and DEV-D06 proposals | Color-test workflow remains P9. Proposed density/sales/affinity/bus systems receive no gameplay credit. |

## Controls and synchronization

**City buildings (C)** and **Game → Building catalog → City buildings** reach the
same entries. Source category/subcategory filters use original XML values as
stable keys and reviewed EN/JA content translations as labels. Search covers
localized names/descriptions, original categories, exact IDs, plugin identity,
plugin title and author. Existing source attribution retains original plugin
ownership. Search is independent per tool category; closing/reopening retains
search and filters without changing the world, clock or camera.

Numbered variant buttons select exact baked definitions. There is no invented
rotation or live hue transform for ordinary fixed/variable art. Half buildings
expose their supported cardinal directions; their recipe Fore=0/Back=1 convention
is explicitly converted from the saved Back=0/Fore=1 selection. Variable thumbnails
use the selected full-height stack, including P7-10's already authorized crop
accommodations; missing or invalid city layers fail the preview instead of
silently drawing a partial building. No new crop corrections are introduced.

Per-definition height/half choices now live in the local shell model. Cards and
the task consume the same choices, so changing height, side, direction or variant
updates artwork, displayed dimensions and price together and cancels the previous
quote. Coordinates remain while browsing variants; closing/reopening or merely
browsing another category preserves a current quote. A different definition keeps
its own choices. Incoming state retains local choices. The server re-reviews the affected site
on confirmation and rejects changed quotes without mutation; unrelated geometry
and clock changes retain the quote. Epoch/content/mode/age fences clear invalid
drafts immediately. Pending commands
block variant/choice edits. Observers can browse and select without activating
construction. Shared state, saves and simulation random streams contain no local
catalog settings.

The reviewed authoritative quote still supplies legal footprint, losses and
actual booking. Fixed/variable construction uses the charged catalog caller;
half additions/removals retain their source-specific free/last-half economics.
Whole-owner removal, construction inspection, real providers and exact reopen
remain the P7-09–11 implementations. Save11, protocol11 and City1 are unchanged
by this slice; prior checklist entries that say protocol10 are historical evidence,
while current HEAD's `ProtocolIdentity.Version` and browser handshake are 11.

## Reciprocal requirements and verification

MAN-T10/MAN-M02/MAN-A16/MAN-A20 and DEV-A07's ordinary building catalog obligation
bind to `BuildingCatalog.svelte`, `fixed-city-edit.ts`, `building-catalog.ts`,
`catalog-preview.ts`, `FixedCityControls.svelte` and `shell-model.ts`.
MAN-W11/MAN-A14 and DEV-P16/P22 retain their existing exact placement, removal,
construction and stack consumers. The [manual reconciliation](web-parity-manual.md)
and [developer review](../tmp/web-feature-parity-developer-docs.md) link back here;
DEV-A07 remains open for P9 color-test authoring and later content obligations.

Focused commands, reports and hashes are recorded in `tmp/p7-12-validation.json`.
**78 C#, 49 Deno and 16 unique Chromium workflows pass** across recorded runs,
with no unresolved failures, skips or retries. The final 16-case browser selection
passed 15; its peer test assumed proactive invalidation instead of server re-review.
After correcting that expectation, the peer workflow passes alone; no runtime
code changed after the other 15 passes. EN desktop and JA narrow catalog captures
were visually inspected. No complete C#, Deno or browser suite is run. C# covers all fixed catalog identities
(including manual-only entries), existing literal city definitions, variable and
half construction, session authority and persistence. Deno covers all 129 cards
in both locales, every supported half selector, variable min/max stacks, source
metadata/author/ID preservation, strict unavailable-art behavior, renderer
regressions, local shell state, pending guards and localization.

Chromium checks all 129 actual card images and authoritative definition/footprint/
price quotes in each locale, supported variable and half choices, search/filter
retention, narrow layout, observers, peer edits and actual construction. Existing
signal/accessory/station catalog and P7-09–11 building workflows remain focused
regressions. Generated/content/locales/Svelte/build, pinned source review/fixtures,
project boundaries and whitespace are checked separately. Native/human art fidelity,
optional content, comprehensive provider refresh, automatic development, capacity
and full P7 acceptance retain their later gates.
