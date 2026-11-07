# P5-18 shared controller manager — 6 November 2026

P5-18 delivers named shared controller instances, provider selection,
configuration, rename, independent copy, referenced-deletion diagnostics and
train/group assignment. The inventory and picked-train inspector expose the
actual assignment and effective controller. Signal properties select the shared
controller to configure at that Location, preserving TRAIN-D010: the signal has
no controller override. MAN-T08 / MAN-W09 / MAN-A12 and DEV-P20 are covered at
browser/portable scope.

## Source and translation reconciliation

Authority remains ExAV 2.1.1.12, archive
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, raw `trunk/` objects. The
[P5-01 contract](web-parity-train-contract.md#controllers-harbors-groups-and-files),
[pinned review](parity/train-review.json),
[localization inventory](parity/train-localization-review.json) and
[P5-16 typed controller contract](web-parity-fleet-controllers.md) supply the
source bindings. Raw hashes and excerpts were rechecked against the archive;
these are source-derived checks, not desktop-runtime captures.

| Source | Delivered behavior or disposition |
| --- | --- |
| `core/controllers/rail/TrainControllerDialog.cs:43,198–240` | Hide Simple/Delegation from the instance manager; create through an exact registered provider. Only TAT is available after the 7 November manual-driving removal. The browser collects the name before committing creation instead of publishing a provisional numbered instance. |
| Same file, `buttonConfig_Click`, `list_AfterLabelEdit` | Configure the selected shared instance; rename retains its identity and assignments. |
| Same file, `buttonCopy_Click:308–355` | Typed deep copy with a fresh stable ID, revision zero, independent location records and first unused numeric name suffix. Shared immutable content bindings are retained; BinaryFormatter activation is not used. |
| Same file, `buttonDelete_Click:254–260` | TRAIN-D009 replaces dangling-reference deletion with authoritative rejection while any train/group directly refers to the instance. The browser lists IDs/names/counts and requires explicit reassignment. Built-ins cannot be copied, renamed, configured or deleted. |
| `core/controllers/rail/TrainPlacementController.cs`, `Train.cs`, `TrainGroup.cs`, `DelegationTrainControllerImpl.cs` | Assignment references one shared instance; explicit Delegation follows group ancestry. Null/exhausted delegation falls back to Simple under TRAIN-D002. Group creation/movement is delivered separately in [P5-22](web-parity-fleet-groups.md). |
| Manual `ManualTrainController.cs:config` | Historical source only; the Manual provider, records and driving UI were removed on 7 November. |
| TAT `TATTrainController.cs:config`, `getStopTimeSpan`, `onJunction`; `StationHandler.cs` | Location-keyed basic harbor drafts, default Wait10/depart, pass, fixed duration and daily time with optional reversal. Equality waits 1440. Unconfigured junctions choose straight. This executable foundation makes named TAT instances usable; junction configuration and the remaining P5-19 acceptance are still open. |
| `SignalRailRoad.getStopTimeSpan` | Dispatch remains through the passing train's effective controller. Local inspector selection opens that controller's Location-keyed settings; no signal assignment is saved. |

Source strings TRAIN-TEXT-0070/0072/0074/0076/0078/0079/0081/0083 become the
EN/JA manager actions, name/type and title. English uses “controller” to distinguish
the shared driving instance; Japanese retains ダイヤグラム. The source's
accelerator markers become ordinary accessible browser labels.
TRAIN-TEXT-0084/0085's provisional numbered creation name is replaced by explicit
name entry; TRAIN-TEXT-0087 retains the numeric copy suffix. Existing English
donors inform meaning but do not override the pinned source. Manual driving was removed on 7 November 2026. TAT basic labels preserve Pass/Fixed/Daily,
game-minute duration, minute-of-day and reversal meanings from StationConfigDialog.
Shared-reference explanations, stale/role diagnostics and confirmation text
are named browser/multiplayer adaptations, delivered in both locales.

## Authoritative mutation boundary

`ManageFleetControllerCommand` and `AssignFleetControllerCommand` use the existing
protocol7 command FIFO, trusted participant/connection generation, world epoch,
role checks, network receipt ledger and publication path. Owner/Operator can
mutate; Observer can inspect configuration and usages. Commands are inert bounded
data, never executable types. Provider creation matches the complete registered
pack/version/hash/contribution binding. Creating the first controller can also
initialize the built-ins/root before any stock purchase, with revision-zero fresh
IDs and the same detached terrain-to-mixed promotion used by stock purchase.
Invalid/cancelled work leaves the live world, allocator and save bytes untouched.

Instance edits/copy/delete check the selected configuration revision. Assignment
checks both the train/group member revision and destination controller revision.
Independent creation has no prior instance revision: it requires ID/revision zero,
current epoch and a compatible provider. Every copied/new identity comes from the
shared allocator; deleted IDs are never reused. The 256-controller limit and
8192-location limit remain unchanged. Copies preserve ordered typed data and get
independent setting records. Names retain the existing 256-character/control
character policy; duplicate explicit names remain distinguishable by stable IDs.

A full candidate graph and persistence transition validate before publication.
Deletion returns `controllerReferenced` for direct train/group dependencies.
Configuration commands patch one selected Location with zero (remove) or one
replacement setting, preserving all other locations and their order. This keeps
commands below the existing 16 KB receive limit even with 8192 saved entries;
the maximum-size regression edits/removes the last entry and checks untouched
settings, command bytes and atomic overflow rejection.
Each mutation has pending/committed duplicate-receipt tests; a retry does not
create, copy, assign or configure twice. Controller/group changes no longer require a driving lease. Authorized peers
edit through the existing epoch/member/controller revision and receipt checks;
no held input or transient ownership remains.
No manager action advances shared game time, moves a train, changes cash or books
a transaction. First promotion initializes the existing source construction
accounts without a transaction. Pending callbacks are retained; the next callback
resolves the current shared instance/configuration.

## Browser workflows

Trains → Controller manager provides create/type/name, configure, rename, copy and
confirmed delete. Direct reference counts and train/group usage lists distinguish
assignment from inheritance. Group assignment is available here; a train's Owned
stock entry and map inspector show Shared controller plus Effective controller.
Use Shared controller to choose Simple, a named timetable instance or Delegation.
Manual providers, records, quick selectors and leased driving controls have been
removed; saves containing Manual records reject without conversion.

Configuration drafts are local until Apply. Apply/Cancel completes one location
before selecting another. Cancel/open/read preserves exact save
bytes. Another peer's revision change/deletion or a world replacement makes a
draft stale. IME composition blocks name submission. Failed/uncertain commands
use the existing feedback and no reconnect replay. The signal inspector prefills
its Location, selects the shared instance locally, and writes only that instance's
settings on confirmation. A previously unconfigured location does not silently
select a different harbor's settings.

Basic TAT settings reuse the existing Save9 data model. The manager can add/select
and remove location settings, configure pass/fixed/daily and optional reversal,
and edit copied settings independently. Removed/rebuilt harbor locations retain
the source location-keyed data. There is no new junction data to prune in this
slice. P5-19 owns the junction configuration model and confirmed stale-junction
cleanup, full basic TAT scenarios and Station/yard adapters; P5-20 owns ordered
rules/masks. Neither later step is marked complete here.

## Original P5-18 verification and limits

These counts and manual-driving references describe the original implementation.
For current removal verification, see [the removal decision](web-parity-fleet-manual.md).

- Selected fleet/contract/network/multiplayer backend regression set: **357/357**,
  including **18 controller-manager cases** covering exact shared signal callback
  waits133/480, isolated copied settings, saved continuation, first creation,
  capacity/allocator fences, built-ins, malformed data, roles/epochs/revisions,
  receipts and control contention/reset.
- Selected web client/contract tests: **63/63** across `fleet-controllers`,
  `fleet-state`, `manual-driving` and shared contracts.
- Selected new Chromium scenarios: **5/5**. EN desktop and JA 390×844 with 150%
  text cover lifecycle, shared/group assignments, copy isolation, usages,
  reference-blocked/unused deletion, cancel, HTTP reopen, actual backend restart,
  Observer guards and peer stale drafts. Additional cases exercise identical
  create receipts, actual signal-art picking/inspector configuration and creation
  before stock purchase. EN/JA manager/configuration captures were visually
  inspected.
- Production build, generated contracts/content, EN/JA locale checks and Svelte
  diagnostics pass (zero errors/warnings; existing bundle-size advisory).
  Pinned 70-file source review, project boundaries and whitespace checks pass.

Evidence: `tmp/p5-18-*`; generated gameplay input:
`tmp/p5-18-manager-fleet.json`; final manager browser captures:
`tmp/p5-18-browser-evidence/controller-manager/`.
The two existing EN/JA canonical manual-driving regressions also pass **2/2**,
including JA 390×844/200% text, signal departures/reversal, peers and restart.
Their pointer hold now explicitly waits for the renewed lease to enable Depart;
the earlier immediate pointer press raced the pending acknowledgement.
No full web suite, full backend suite, new native/IME/human-fidelity,
Linux-container, optional-suite or capacity acceptance was performed. Save9,
protocol7, saved schema/catalog/packs and admission limits are unchanged;
commands and one rejection code are additive. P5-MANUAL-001 remains open and
owner-deferred; this step does not establish its closure.
