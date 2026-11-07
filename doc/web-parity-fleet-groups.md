# P5-22 nested groups and registered delegation — 7 November 2026

P5-22 delivers create/name, nested group selection, train moves, group reparenting,
shared group-controller assignment and visible effective controllers. Open
**Trains → Controller manager**, select a group, then create, rename or move it.
The train selector below offers **Move to group** and a separate train controller
assignment. Native selects/buttons support keyboard and touch without drag/drop.
Groups can be created before the first purchase or named controller.

## Source and removal disposition

Authority remains ExAV 2.1.1.12, archive
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, raw `trunk/` objects. The
[P5-01 contract](web-parity-train-contract.md#controllers-harbors-groups-and-files)
and [70-file pinned review](parity/train-review.json) were rechecked against the
archive. These are source inspections, not desktop-runtime captures.

| Source | Delivered behavior / explicit adaptation |
| --- | --- |
| `core/controllers/rail/TrainPlacementController.cs:625–628,648–675` | Names edit the selected TrainItem; dropping a train/group changes its parent; the context action creates a child with the interactive TrainGroup constructor. The browser collects the name before committing and uses explicit selectors/buttons. |
| `core/world/rail/TrainGroup.cs:12–16` | The named constructor leaves the controller null; the interactive constructor assigns the registered Delegation instance. New browser child groups use Delegation even though their name is collected first. The root retains P5-01's explicit Simple adaptation. |
| `core/world/rail/TrainItem.cs:28,36–42` | Controller is a shared reference independent of membership. Moving removes from the old collection and appends to the new one, retaining the explicit controller. Browser publication changes both reciprocal membership lists atomically. |
| `core/world/rail/Train.cs`, registered `DelegationTrainControllerImpl` | Only explicit Delegation traverses groups. Null resolves to Simple under TRAIN-D002; exhaustion at an all-delegating root also resolves to Simple. Train overrides remain active until replaced with Delegation. |
| `TrainPlacementController.cs:679–702`, `TrainGroup.cs`, `TrainItem.cs` | Sell is enabled only for an unplaced Train, and there is no group-delete action/API. The [FAQ's no-delete claim, MAN-E08](web-parity-manual.md), is consistent with this source. Browser groups cannot be deleted, including empty groups after sale; names and parentage remain editable. |

Raw source SHA-256 pins include `TrainGroup.cs`
`97ad5bee15ea7b535fbdad0665806f070211d719c46cb38ec395624a539098b8`,
`TrainItem.cs`
`8820be8ce5c188a0137d26a8de1096a2c13b54d42a215293f5f36dca594d9e1b`,
and `TrainPlacementController.cs`
`a16274994ea5f41f37ba092a45b1c67083994999f0825e268be55d228ef40cc4`.

TRAIN-TEXT-0119 (新しいグループの追加) and TRAIN-TEXT-0146 (名前) supply
create/name meanings; TRAIN-TEXT-0263's provisional numbered name is replaced by
explicit entry. English/Japanese rename, parent, move, current-group, effective
controller, inheritance, stale-draft and no-delete explanations are browser
adaptations. Group Delegation says “Use parent group controller”; train Delegation
says “Use group controller”. Root's default display name is localized locally;
user-entered group names are preserved. Native IME acceptance remains unclaimed.

## Commands, invariants and durable state

`ManageFleetGroupCommand` adds bounded inert `create`/`rename`/`move` operations to
the existing protocol7 FIFO and receipt boundary. Commands carry world epoch,
target kind/ID/revision and destination group ID/revision. Rename has no parent
payload. Parent zero is accepted only when creating the first group in a world
without a fleet, selecting the root initialized in that same publication. All
fresh members then start at revision zero. Failed creation publishes nothing.

Names are trimmed, nonempty, at most 256 UTF-16 units and free of control
characters. Existing 256-group admission remains enforced. Owner/Operator can
mutate; Observer can select and inspect. Duplicate requests reuse their receipts.
Target/destination revisions fence concurrent membership edits; browser drafts
retain their names and reject stale epochs or revisions until canceled/reopened.

Moves preserve controller overrides, car order/poses, purchase data, accounts,
random state, shared time and callback queue. The old parent's member is removed;
the destination appends it; the moved item and affected existing groups each
advance one revision. Root moves, self/descendant moves, missing parents,
duplicate/dangling membership and cycles reject before publication. Existing
graph validation also rejects malformed Save9 input before world/file replacement.
Iterative browser ancestry traversal handles the deepest admitted tree and
terminates on malformed cycles.

Existing shared-controller assignment edits the selected group's stored ID,
while effective ID is resolved separately for groups and trains. Reparenting
checks all changed effective train controllers against participant leases through
the controller publication path. Another participant's affected driver blocks
the entire change; an owned changed effective controller releases held input.
Direct train moves also reject another participant's active driver. Unaffected
explicit train overrides continue using their original controller.

Existing Save9/protocol7 group/member/controller fields carry the complete graph;
no save version changes. Sale removes the train from its actual group and leaves
the empty group. Imported historical graphs remain P10.

## Fresh selected verification

- **104/104 .NET** group, controller-manager, graph and malformed fleet-save
  checks pass. MAN-A17 exercises nested reassignment, ancestor-controller changes,
  explicit override retention/removal, subtree moves, rename, unchanged queue/time/
  occupancy, exact reopen, equal continuation, withdrawal and sale. Additional
  cases cover atomic rejection, role/epoch/revision fences, receipt deduplication,
  inherited manual leases and initial creation before a fleet exists.
- **69/69 client/contract** checks pass across `fleet-groups`, `fleet-controllers`,
  `fleet-state` and shared wire fixtures, including exact Int64 IDs, inert command
  admission, null/all-delegating fallback, ordered nested selectors, illegal move
  exclusion, a 256-group chain and malformed cycles.
- **6/6 selected Chromium** journeys pass: four group workflows and two existing
  shared-controller management regressions. Journeys cover EN/JA
  nested moves/controller changes, keyboard cancel/focus return, Japanese touch
  at 390×844 with 200% text, peer conflicts/Observer browsing, HTTP reopen and
  actual checkpoint process restart. Source input is purchased/deployed stock
  built through the existing gameplay construction path, not trusted car poses.
  EN desktop and JA 390×844/200% group and move-dialog captures were visually
  inspected; buttons/selects remain reachable and modal actions wrap correctly.
- Production web build, generated contracts/content/locales and Svelte checks
  pass (zero errors/warnings; existing bundle-size advisory). Pinned source,
  744 independent fixture specifications and project boundaries pass.

Logs/input: `tmp/p5-22-*`; captures: `tmp/p5-22-browser-evidence/groups/`.
The browser journeys use the Debug backend and production browser bundle. No full
web/backend suite, new Linux/container, optional-pack, capacity, native/human
fidelity or historical-import acceptance is claimed. P5-MANUAL-001 remains
owner-deferred; P5-23 rule files and P5-25 tracking retain their own scope.
