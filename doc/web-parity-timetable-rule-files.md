# Timetable rule files — P5-23

Browser/portable implementation delivered 7 November 2026. MAN-A12 / MAN-O09,
DEV-P20 and SCN-009 cover ordered read/append, source validation bypasses and
portable export. Whole-world historical import remains P10.

## Source behavior and supported input

Authority remains ExAV archive `883cf7d81cf49db25ff38cd3a25d0f140afe31a0`,
`trunk/plugins/org.kohsuke.freetrain.tc.tat/src/`. The existing pinned train
review includes `StationAdvancedDialog.cs`, `AdvancedStationHandler.cs` and
`TimeMask.cs`; `AssemblyInfo.cs` declares assembly version `2.0.*` and the
project names it `FreeTrain.TATTrainController`.

`buttonReadDia_Click` reads a BinaryFormatter
`AdvancedStationHandler.RuleCollection`, appends every rule in enumerated order
and clears the detached input collection. It does not call `createRule` or Add's
duplicate-mask guard. Copy creates a new `AdvStationRule` and copies the five
mask fields and action; Replace replaces a list entry. The sorter is inactive.
`buttonSaveDia_Click` serializes the collection with BinaryFormatter; the SOAP
routes are commented out. The browser retains this ordering and duplicate
behavior, with validation and explicit confirmation before shared mutation.

The runtime reader in `web/src/lib/fleet-rule-nrbf.ts` is project-owned,
inert record decoding. It does not use an object deserializer, assembly loader,
binder, constructor, delegate or callback. Names are compared to fixed strings;
no file path, URL or executable type can be resolved. The layout references are
Microsoft's [MS-NRBF record enumeration](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-nrbf/954a0657-b901-4813-9398-4ec732fe8b32)
and [class/member metadata](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-nrbf/847b0b6a-86af-4203-8ed0-f84345f845b9).

The admitted legacy profile is a single unwrapped NRBF 1.0 stream with header
ID -1 and an exact `freetrain.world.rail.tattc.AdvancedStationHandler+RuleCollection`
root. The library name must be `FreeTrain.TATTrainController`, version `2.0.*.*`,
neutral culture and null public-key token. Older versions and other profiles
are rejected. Only these exact metadata layouts are mapped:

| Type | Ordered members and descriptors |
| --- | --- |
| RuleCollection | `CollectionBase+list`: System.Collections.ArrayList |
| System.Collections.ArrayList | `_items`: ObjectArray; `_size`, `_version`: Int32 |
| AdvStationRule | `action`: StationAction; `month`, `day`, `dayOfWeek`, `hour`, `minutes`: SByte |
| StationAction | `value__`: Int32, ordinal 0–7 |

The action ordinals map to Pass, Stop, Go, Reverse, Forwarding,
ReverseForwarding, Reverse2 and RForwarding2 in that order. Portable
`reverseforwarding` represents source `rforwarding`; no ordinal is cast to the
larger internal enum, which also contains junction actions.

Accepted records are the header/end, BinaryLibrary, typed user/system classes,
ClassWithId, MemberReference, ObjectNull, the two null-run records and
ArraySingleObject or one-dimensional zero-based BinaryArray of Objects.
Only declared Int32/SByte primitive members are read. Deferred references and
negative enum-object IDs are supported. Collection size must fit the backing
array, unused array slots must be null, and every object must be reachable
through the exact mapped graph. Duplicate/zero IDs, missing references/metadata,
unknown classes/fields/descriptors/records, incompatible root/version, invalid
null runs, truncation and trailing bytes reject the whole file. A null internal
list or empty list is a valid empty collection; empty append is disabled.

Bounds apply before allocation or iteration: 512 KiB per file, 256 UTF-8 bytes
per string, 32 KiB of strings, six members per class, two library records, 4,096
objects, 8,192 records, depth 16 and 2,048 total backing-array elements. At most
1,024 actual rules are admitted. All decoded masks/actions must pass current
station-rule validation. Source read bypasses the Add guard and editor creation;
the portable adaptation preserves duplicate admission while rejecting invalid
masks/actions. It does not grant import permission to unsupported source values.

Repeated source rule references become independent immutable rule values. This
retains reachable Copy/Replace semantics and isolates later edits. Import never
mutates its input bytes or writes the selected file.

## Portable format and authoritative append

Save exports UTF-8 JSON as **`diagram.ftdd.json`**, MIME `application/json`.
The exact top-level fields are `format`, `version` and `rules`:

```json
{
  "format": "freetrain-online-timetable",
  "version": 1,
  "rules": [
    {"month": 4, "day": 23, "dayOfWeek": 5, "hour": 8, "minutes": 0, "action": "go"}
  ]
}
```

Every rule has exactly the five integer masks and one explicit action tag.
Rule values, order and duplicates round-trip, including empty and maximum-sized
lists. Controller IDs, locations and revisions belong to the destination and are
not exported. Extra fields, wrong format/version, invalid rules and excessive
lists fail before a preview. This format is **not a desktop binary `.ftdd`
writer**. No desktop write compatibility or native executable acceptance is
claimed.

The existing `EditFleetRuleCommand` gains `append` and optional `rulesBase64`.
The latter is only a transport representation: six bytes per rule, five signed
mask bytes (255 means wildcard), followed by source action ordinal 0–7, encoded
as canonical unpadded Base64. Each record encodes to eight characters; all 1,024
rules use 8,192 characters, within the existing 16 KiB message limit. Portable
files remain ordinary explicit JSON. Append uses harbor context, index -1,
offset 0, null single-rule payload and null geometry revision. Other edit
operations cannot carry this batch field, and junction append is unsupported.

The server revalidates the entire decoded batch, active Advanced harbor,
controller revision, epoch, participant and location capacity. Graph publication
also checks the 8,192-rule controller total and existing lease/transition rules.
A detached candidate publishes once, incrementing the controller revision once;
any invalid late entry or exhausted limit rejects the complete append. The
existing FIFO and command receipt ledger prevent duplicate append on retry.
No train movement, shared-clock, cash or callback changes occur. Save9/protocol7
remain current; existing single-rule commands omit the optional batch field.

## Browser workflow

Trains → Controller manager → select TAT → Configure controller → select an
Advanced harbor → Ordered timetable rules. Junctions retain their existing
ordered editor and have no rule-file controls.

Read accepts `.ftdd` and portable JSON, checks file size before reading, then
shows the format, filename, existing/imported/result counts and every imported
rule in append order. The bilingual explanation states the duplicate-mask
bypass. Append these rules commits the complete preview; Cancel discards it.
Exports contain the current committed rules. Observer can inspect/export but
cannot import or mutate rules.

A preview captures controller revision and world epoch **before** asynchronous
file reading. Peer edits, including a rename while a read is delayed, invalidate
confirmation. Component destruction discards late completions. Replacement
closes the editor through the existing shell lifecycle. All controls use the
existing modal focus/scroll behavior; no workflow changes shared time.

## Evidence and qualifications

Fresh focused verification on 7 November:

- **43/43 selected .NET tests**, including 18 new rule-file cases: all eight
  source ordinals, signed masks, SCN-009 A/B/C append order, duplicate admission,
  immutable edit isolation, save continuation, malformed late entries, roles,
  epoch/member/context rejection, receipt replay and location/controller limits.
  A full 1,024-rule append fits the existing transport boundary. The browser
  input is freshly saved from gameplay-built rails/signals and deployed trains.
- **55/55 selected client/contract tests**: six new reader/round-trip/admission
  tests plus the existing controller and shared-contract files. Both NRBF array
  encodings preserve eight actions, April23/December31 masks and repeated
  references. Every proper prefix of the valid binary fixture is rejected;
  invalid headers/types/assemblies/fields/references/null runs, malformed JSON,
  excessive input and exact 1,024-rule portable round trips are covered.
- **4/4 selected Chromium workflows**: EN/JA file import/export/re-import,
  existing ordered-rule regressions, duplicate order and edit isolation, Cancel
  and invalid/oversized-file byte identity, delayed-read peer staleness, Observer
  inspection/export, HTTP reopen and actual checkpoint process restart. JA uses
  390×844 and 200% text; EN/JA preview captures are visually inspected.
- Production web build, generated contracts/content, locales and Svelte pass
  with zero errors/warnings (existing bundle-size advisory). Pinned 70-file
  source review, 744 source-fixture specifications, project boundaries and
  whitespace checks pass.

Logs: `tmp/p5-23-*-final.log`; captures:
`tmp/p5-23-browser-evidence/rule-files/`; gameplay input:
`tmp/p5-23-rule-fleet.json`. Tests use the production web build and Debug backend.

`web/tests/fixtures/timetable/` contains **synthetic structural fixtures**,
generated independently of the runtime reader by
`scripts/build-timetable-rule-fixtures.py`; `--check` verifies exact bytes. Both
also pass the pre-existing independent Python NRBF structural reader (794/800
bytes, 19 objects each). They follow source fields and framework layouts observed
in prior P0 reports. They are not captured files from an original ExAV executable.
Native file-profile/human acceptance, other assembly/runtime layouts, Linux
containers, optional packs, full-fleet capacity and full suites were not newly
verified. P5-MANUAL-001 remains owner-deferred; P5-24/P6 integration and P10
historical world compatibility keep their own gates.
