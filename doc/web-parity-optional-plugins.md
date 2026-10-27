**P2-03 implementation — 27 October 2026:** the [directory loader, shared preparation,
authoritative pack negotiation and localized per-set credits](web-parity-plugin-startup.md)
now run with source and prepared synthetic sets. The canonical non-free repo owns
its metadata/credits. Full canonical conversion still fails named adapter/resource
validation; historical-suite and full P2-03 acceptance remain open. The reserved
interface and “not yet implemented” statements below describe the P0 specification.

# ExAV optional V2 plugin review and loading contract

**P0-08 source inventory/specification complete — 21 October 2026.** The
[rules and decisions](parity/exav-optional-rules.json) and
[metadata review](parity/exav-optional-review.json) reconcile the canonical
non-free V2 set against stable ExAV. Portable conversion, launch loading, credits
UI, recipe repairs and save/runtime compatibility remain P2/P9/P10/P11 work.
P0-09 [source integration](web-parity-exav-integration.md) subsequently completed; overall P0 and the existing UX/native handoff remain open.

## Sources and accounting

The canonical repository is now **`freetrain-online-plugins-nonfree`**, previously
referred to locally as `freetrain_online_extra_plugins`. Original source remains
pinned to **`c8ec62e47a7f5089b666e57072164a0e14934466`**, root `plugins/`.
Its `readmeV2.txt` identifies the additional set as **2.1.0.10, 26 September 2012**,
for V2 engines from beta 2.1.0.12 / alpha 3.1.0.1 onward and warns against mixing
non-V2 plugins. This package version is distinct from engine, protocol, catalog
and save versions. The review target is stable **ExAV 2.1.1.12**, archive
**`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`**, `trunk/`.

The separate normalized revision **`55ac13e7d292b1d003cf6f97a63be1675d7202f4`**
includes `de8c7ce`'s UTF-8 conversion and filename-casing changes. All **4,933**
files map one-to-one: **507** change bytes, including two renamed manifests.
Decoded text is identical after XML codec-declaration normalization, and every
expanded manifest recipe is identical. Neither pin is replaced by working files;
normalization does not resolve the resource diagnostics below.

| Denominator | Canonical optional V2 evidence |
| --- | ---: |
| Files, each with path/mode/size/SHA-256 | 4,933 |
| All discovered manifests / direct declarations | 452 / 11,484 |
| Distinct explicit IDs / duplicated explicit IDs | 11,470 / 14 |
| Generic generated child records / distinct generated IDs | 23,417 / 23,273 |
| Source ID conflicts, explicit plus generated | 158 |
| Selected immediate-child manifests / declarations | 450 / 11,470 |
| Selected explicit/generated ID conflicts, including bundled IDs | 0 |
| Media / media referenced by manifest edges | 4,399 / 4,212 |
| Explicit file edges: exact / case-only / missing | 5,615: 4,779 / 36 / 800 |
| Local XML fragments expanded from pinned objects | 6 |
| Explicit class bindings / distinct effective or explicit type bindings | 6,919 / 37 |
| XML presentation candidates, pending wording review | 21,686 |
| Retained text/notice candidates / Windows DLLs | 31 / 2 |

These are separate source denominators, not placeable-item or runtime coverage
totals. Media not referenced directly remains inventoried for loader/alternative
review. Recipes retain manifest path, ordinal, ID/type, the hash of the complete
expanded XML tree, selected numeric/population parameters and implementation
binding. Full plugin definitions and artwork remain in the separate repository.
Source hashes and qualified project memberships connect the adapters to the
reviewed [bundled ExAV content](web-parity-content.md).

## Source findings and dispositions

**Startup and membership — EXAV-OPT-003/004/009.** Stable
`core/framework/Core.cs:getDefaultProfile` enumerates immediate children;
`PluginManager.init` checks those individual directories for `plugin.xml`.
The nested `plugins/toaru_matibiru/toaru_matibiru_{b,e}/plugin.xml` files are
source-only copies of top-level plugins, outside the default profile. Their
14 explicit IDs and 144 generated IDs account for all 158 inventory conflicts;
12 of the 14 duplicate explicit definitions differ. Preserve the nested records
without recursively activating them or treating them as interchangeable.
The specified optional profile therefore has 450 manifests, 11,470 declarations
and 23,273 generated children, with no internal or bundled ID collision.

`Core.parseProfile` checks `File.Exists` on directory arguments and never adds
the enumerated subdirectories to its return set. Its nonempty-argument path
returns an empty plugin list. A working browser startup argument is a deliberate
adaptation. Plugin identity comes from the directory basename; implicit `system`
and explicit plugin dependencies must resolve before registration. The review
records transitive dependency closure for all 452 manifests; none are missing.
Factory names, explicit/generated contribution IDs and picture identities retain
their distinct registries. Reject conflicts in the **selected** composition.

**Code and resources — EXAV-OPT-005/010.** Of 800 missing explicit file edges,
**773 are DLL paths**, bound by qualified type and actual project membership to
stable bundled source; the files remain absent. Stable `PluginUtil.loadType`
searches by DLL basename and takes the first hit when the requested file is
missing. That Windows fallback is not the portable extension contract.
The set contains two binary DLLs and no C# sources: rail signal and map capture.
The signal type has stable bundled source; this does not prove binary equality.
`freetrain.tools.capture.MenuContributionImpl` has no stable source binding and
needs a specified portable adapter or an explicit scope decision in P2/P9.
Map capture is not automatically covered by the debug/video exclusions.

The remaining **27 missing image edges affect 26 declarations**; examples include
the shino `3x3buil` filenames, `kiha55b.bmp`, two fence families' sheets,
red-brick station images and shikashika amusement/road images. Another **36 file
edges resolve only by case folding**. Both pins retain these diagnostics. P2
must record exact portable path mappings or reviewed repairs before accepting
the affected recipes; there are no guessed substitutions in this review.

The six SYSTEM entities in the E233/Keiyo and Keisei AE manifests resolve only
through an exact path allowlist to pinned local XML bytes. Other external,
out-of-root and nested entities are rejected. Both picture and contribution
reference candidates have identity matches; loader-specific interpretation,
image decoding/crops, effects and runtime legality remain P2 verification.

**V2 rules and translations.** All 11,484 recipe bindings except map capture map
to stable source/projects. This is source availability, not portable support.
Preserve ExAV total/seated capacity, fare/amenity/triprange, Int64 prices,
population types, road styles, direction/color choices and instance state.
Generic child IDs follow ExAV's color × sprite registration; direction selections
are separately inventoried. Applying approved `SOUTH` spelling changes no
selection in this particular set. Do not infer equivalent definitions from IDs.
Each feature reviews the optional presentation candidates in English/Japanese
under this pack identity; ordinal plus path distinguishes duplicate declarations.
No English donor or wording is approved by this inventory.

**Credits and permissions — EXAV-OPT-002/006.** Preserve `readmeV2.txt`'s collection
acknowledgments, `changelog.txt`, per-plugin author/homepage declarations and all
31 text/notice candidates with hashes. Owner-reported redistribution permission
remains recorded; attribution does not establish new derivative terms. Per-file
conversion/translation permission and notice classification stay in the optional
conversion ledger. Bundled LGPL notices and optional notices remain separate.

Ten maintained source-notice notes distinguish the actual wording: Ayase's road
notice permits redistribution without unauthorized modification; teha invites
modification/other uses; three Hermes notices describe image use as free and
request a message before publication. Other notes preserve modified-art attribution
chains and map-capture acknowledgments. These statements apply to their named
inputs; they do not supply uniform collection terms. The review retains the
owner's permission statement without inventing a new grant for conversion.

**Older suites — EXAV-OPT-007.** The historical 228-manifest `PluginsExtra`
ledger remains pinned to `6886a0fa74245e621cf627bb045b70598ec30760`; **82** of
its direct IDs are absent from canonical V2. Earlier V1 (436 manifests), V2-copy
(452) and combined-tree (508) research retains its own provenance. Preserve
separate selected profiles and P10 compatibility mappings; this review neither
blends these families nor certifies their runtime behavior or saves.

The supplied 0.4 save's 150 distinct contribution references have **138 exact
selected optional matches, 11 stable bundled matches and one unmatched ID**,
`{556E9EC4-5751-43DA-8F08-A1484E233811}`. The owner-reported 0.6.2 sample's
seven IDs all match bundled ExAV. These bindings use the separately hashed P0-05
sample metadata; they prove identity lookup only. Preserve the unmatched legacy
reference for P10 mapping rather than dropping it or guessing an alias.

## Owner-directed launch and credits contract

The following is the **reserved P2/P9 interface**, not a working launch command
in today's prototype:

```sh
dotnet run --project src/FreeTrain.Online.Server -- \
  --plugin-dir /path/to/freetrain-online-plugins-nonfree
# Repeat --plugin-dir for another compatible plugin-set root.
```

Each argument names a **plugin-set directory**, containing `plugin-set.xml` and
the manifest root it declares. Non-free sources, localized credits, converted
assets, catalog, notices and packages stay in that separate repo/distribution.
Default build/start/test remains independent of optional checkouts or downloads.
Container operators mount the selected directories. Only the server operator
configures local paths; clients receive the enabled catalog and credits.

On explicit startup, validate set metadata, selected manifests, dependencies and
portable adapters, then prepare or reuse a deterministic converted pack in a
separate cache keyed by source/recipe/converter hashes. Prepared distributions
may ship that output. An operator need not rebuild core or run a mandatory manual
conversion step. The offline importer still accepts explicit source and separate
output paths for reproducible packaging; it shares validation with startup.
Publish the composed catalog only after all selected sets validate. Unsupported
code, missing resources and conflicts produce owner-specific startup diagnostics.
Never silently skip declarations, override an ID or load arbitrary legacy DLLs.

Proposed metadata shape, to be added **in the plugin repo** during implementation:

```xml
<?xml version="1.0" encoding="utf-8"?>
<plugin-set schemaVersion="1" id="exav-additional-v2-nonfree" version="2.1.0.10">
  <plugins root="plugins" discovery="immediate-children" />
  <credits defaultLocale="ja">
    <locale lang="en">
      <button>Additional plugin credits</button>
      <title>Additional plugin set credits</title>
      <text>Reviewed English acknowledgments supplied by the plugin set.</text>
    </locale>
    <locale lang="ja">
      <button>追加プラグインのクレジット</button>
      <title>追加プラグインセットのクレジット</title>
      <text>プラグインセット側で確認した謝辞を記載します。</text>
    </locale>
  </credits>
  <notice path="readmeV2.txt" />
</plugin-set>
```

This is a schema example, not approved collection credit text or an existing
file in either optional pin. Version 1 requires nonempty English/Japanese button,
title and plain credits text, a valid default locale, relative in-set roots and
notice paths, and no external XML entities. Select the current UI language;
an unsupported locale falls back to `defaultLocale`. Missing/malformed metadata
fails that set's startup validation. Render credits as plain text with preserved
paragraphs. Retain the original Japanese acknowledgments alongside the reviewed
English wording and linked notice text; a manifest author's name alone does not
replace collection acknowledgments.

Each enabled set adds **one additional credits button**, localized from its own
XML, opening its own credits dialog. Base-only mode has no optional-set button;
multiple sets have distinct buttons. Language switching updates both button and
dialog. Keyboard opening/closing and focus return, narrow-screen/long-text layout
and both locales belong to P9 acceptance. Existing game credits/license UI remains.

Pack metadata includes stable set ID/version, source revision, catalog/asset hashes,
dependencies and credits/notice hashes. Saves name required pack identity and
recipe/catalog requirements without local paths or content binaries. The server's
set governs join/load compatibility; missing or incompatible packs must preserve
the current world and original input and support retry after installation/restart.
See [persistence requirements](web-parity-domain-transition.md#optional-non-free-pack-design).
Separate output/cache namespaces and a clean base rebuild after an optional run
must prove that non-free content does not enter default artifacts.

## Reproduction and completion boundary

```sh
python3 scripts/review-exav-optional.py \
  --source-root /path/to/freetrain-exav-archive \
  --optional-root /path/to/freetrain-online-plugins-nonfree
python3 scripts/review-exav-optional.py \
  --source-root /path/to/freetrain-exav-archive \
  --optional-root /path/to/freetrain-online-plugins-nonfree --check
python3 -m unittest discover -s scripts/tests
git diff --check
```

Only the separate optional metadata review is written; no source repository,
historical ledger, bundled ledger or runtime asset is changed. Checks read the
configured Git objects, ignoring dirty files and requiring both explicit roots.
Default Python tests use checked-in evidence and synthetic XML/graphs without
discovering siblings. Source/specification closure does not imply a loadable pack,
permission clearance for every derivative, rendered fidelity, implemented credits
or successful startup/join/save continuation. Those acceptance cases are
**OPT-AC-01–05** in the maintained rules. P0-09 integration is now recorded [separately](web-parity-exav-integration.md). Next: the
existing UX handoff before affected gameplay implementation.

Validation on 21 October: the optional `--check`, bundled ExAV inventory/content
checks, historical inventory/content checks, all **100 Python tests** and
`git diff --check` pass. No runtime/browser/native acceptance was run for this
source/specification change.
