# P5-19 basic TAT — 7 November 2026

P5-19 completes the browser/portable basic TAT slice through the shared controller
manager: Location-keyed stop/departure settings and junction default routes,
independent copies, confirmed missing-junction cleanup and saved continuation.
MAN-T08/W09/A12 and SCN-008 are covered at this scope. Advanced ordered rules
remain P5-20; real Station/platform/yard passenger gameplay remains P6.

## Source and translation reconciliation

Authority is ExAV 2.1.1.12, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, raw `trunk/` objects. The existing
70-file train source/hash review was rerun against the archive. The
[train contract](web-parity-train-contract.md#controllers-harbors-groups-and-files)
and [manager foundation](web-parity-fleet-controller-manager.md) remain inputs.

| Source in `plugins/org.kohsuke.freetrain.tc.tat/src/` | Delivered contract |
| --- | --- |
| `TATTrainController.cs`, `Junction.cs` | Each registered instance owns Location-keyed junction records. No record means Straight; a record supplies its Straight/Curve default. Ordered advanced rules remain P5-20. |
| `JunctionConfigDialog.cs` | Default direction selection maps to the EN/JA Straight/Curve control. Apply commits one local draft; Cancel is read-only. |
| `TATTrainController.refresh` | Only absent junction locations are pruned. Browser opening remains read-only; a separate confirmed action lists/removes missing junction settings. Harbor settings are preserved, including absent harbor locations. |
| `StationHandler.cs` | Unconfigured harbor: wait10 on first call, then depart. Pass: zero on every call. Fixed: duration on first call; later zero or reverse. Daily: positive time until the configured minute, with equality waiting1440; later zero or reverse. |
| `StationConfigDialog.cs` | Pass/Fixed/Daily choice, fixed10 default, separate departure hour/minute and reverse-on-departure. A newly selected daily mode starts at00:00 without reversal. The fixed editor retains minimum10 and increment10. |
| `TATModalController.cs` and the existing typed harbor boundary | Signal settings use the signal Location. A labeled test yard adapter resolves its host Station Location, independently of its rail cell. No per-train toggle or signal controller override is introduced. |

Direction labels and departure hour/minute retain the meanings of source
JunctionConfigDialog/StationConfigDialog strings from the pinned localization
inventory. Japanese uses ポイント, 直進, 分岐 and 発車時/発車分; English uses
Junction, Straight, Curve and Departure hour/minute. Shared-setting explanations,
explicit cleanup confirmation and stale-geometry handling are browser adaptations
provided in both locales. No advanced-rule enable field is invented.

The existing portable Int32 minute-duration ceiling (2,147,483,647) remains,
below the native duration editor's larger decimal bound. Existing inert fixed
records may contain smaller nonnegative durations; the source handler semantics
(including zero) are retained independently of the editor's minimum10. P10 owns
historical input compatibility. Daily stored minutes remain0–1439.

## Model, persistence and authority

`FleetController.Junctions` is immutable, ordered, bounded to8192 unique Location
records per instance and valid only for TAT providers. Route values are strictly
Straight/Curve. A source location can remain after rail removal; a new setting
requires an actual junction at confirmation. Rebuilding the same location reuses
its retained setting until explicit cleanup. Existing head guidance consumes the
passing train's effective shared controller; followers retain predecessor paths.

Save9/protocol7 gain an optional inert `junctions` array. Absence means empty;
empty arrays are omitted by the writer, preserving existing literal save/snapshot/
delta output. No version migration, provider activation, catalog/pack change or
executable payload is introduced. Codec preflight and graph/wire/schema validation
bound arrays before reveal. Revision checks compare junction contents as well as
harbor contents, preventing configuration changes without a member revision.
Typed copies allocate independent junction records as well as harbor records.

One-location configuration patches contain zero(remove) or one replacement, never
the full8192-entry collection. Commands reject mixed harbor/junction payloads,
unknown routes and nonexistent new junction targets atomically. Junction edits
and cleanup require the current mixed geometry revision as well as the controller
revision; a peer edit, movement/geometry update or replacement can stale a draft.
Cleanup runs in the existing FIFO and changes only the selected shared instance.
The manager's Owner/Operator, Observer, epoch, connection, receipt and driver-lease
rules remain in force. No open/cancel action edits state or advances game time.

## Verification and limits

- **202/202 selected .NET tests**: fleet movement, controllers/manager, session and
  persistence. New cases cover actual signal callbacks at arrivals479/480/481
  waiting1/1440/1439 for departure480, reversal and exact saved continuation;
  actual junction head/follower movement for default Straight and configured
  Curve; real first/later callbacks through a labeled host-Station yard adapter;
  distinct TAT10/Simple30/Manual5 defaults; fixed/pass/daily/zero semantics;
  patch/copy/cleanup/revision fences; malformed/duplicate/8192-entry settings.
- **89/89 selected client/contract tests**: fleet controllers/state, shared
  contracts, train motion and mixed state. New checks cover bounded patches,
  strict route/revision encoding and read-only missing-junction diagnostics.
- **5/5 selected Chromium tests** cover EN and JA(390×844,150% text), junction/daily drafts,
  Cancel/save-byte identity, peer configuration/stale drafts, cleanup confirmation,
  HTTP save/reopen and backend restart. Existing shared manager EN/JA and actual
  signal-inspector regressions are included in the final selected run.
- Production build, generated contracts/content, locales and Svelte diagnostics
  pass; existing bundle-size advisory remains. Source review, project boundaries
  and whitespace checks pass.

Logs: `tmp/p5-19-*`; generated real-construction fixture:
`tmp/p5-19-tat-fleet.json`; browser captures:
`tmp/p5-19-browser-evidence/tat/`. Captures are inspected at EN desktop and JA
narrow/text-scaled sizes. No full web/backend suite, new native/IME/human-fidelity,
Linux-container, optional-suite or capacity acceptance is claimed. P5-MANUAL-001
remains owner-deferred. Positive signal waits retain source StoppingAtStation
labeling under GEO-005. Both-signal/different-controller integrated acceptance
remains P5-24, independently of this basic TAT delivery.
