# P5-25 — Train information, framing, tracking and color settings

Delivered 7 November 2026 at browser/portable scope. Stable ExAV 2.1.1.12,
archive `883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, remains the authority.
[Train contract](web-parity-train-contract.md) and
[workflow mappings](web-parity-actions.md) supply MAN-W10/A13, ACTION-002/008
and DEV-P32. Manual driving remains removed under the owner's decision.

## Source bindings and browser behavior

| Source | Delivered behavior |
| --- | --- |
| `core/controllers/rail/TrainInformationWindow.cs`, `populateMenu`, `onStateChanged`, `onMove` | Nested group/member order; actual train name, state, length, capacity and passenger count; integer normal-capacity percentage, including values over 100%; zero capacity displays an undefined percentage. Move frames the current head once. |
| `core/world/rail/Train.cs`, `TrainGroup.cs`, `TrainItem.cs` | Stable TrainId selection, current head after consist reversal, and effective shared controller through the existing delegation rules. |
| `core/views/QuarterViewDrawer.cs`, `searchTrain`, `drawTrainName` | Name visibility is local; only Inside heads receive labels. Outside trains retain an external location for Move/Follow. |
| `core/framework/GlobalOptions.cs`, `FontOptions.cs`, `ConfigDialog.cs` | Visibility defaults off and label color defaults yellow. Browser font families (sans serif, serif, monospace) and 8–32 CSS pixels replace installed Windows font selection. Local storage preserves presentation without changing world state. |
| `plugins/org.kohsuke.freetrain.trains.colorTest/src/ColorConfigDialog.cs`, `ColorTestTrainCar.cs` | Seven bundled templates, body/three line colors, initial red/blue/green/yellow, RGB-triplet `colorMap` XML copy/export and shared test-car definition semantics. |
| `core/contributions/train/ColoredTrainCarImpl.cs`, prepared P2 palette rules | Blue/green/red/yellow source entries and their 180/255 dark shades; authored/night entry precedence and night RGB888 conversion. Derived images and exact alpha masks use verified prepared template pixels. |

**View** provides the nested selector and information. Picking a car also shows
information and Move/Follow in its inspector. Car-specific inspection and command
drafts remain independent. The selector preserves authoritative group membership
order; groups are nonselectable headings. It does not mutate groups or assignments.
Normal, seated and packed capacities come from the actual car definitions, including
reverse wrappers. This slice reads saved passenger counts; P6 retains boarding,
platforms, fares and station gameplay.

**Move to train** is one-time framing and ends any current follow. **Follow train**
is local continuous camera tracking of the current head of one TrainId. Movement,
explicit reversal and Outside return preserve the followed identity. External
references are framed directly, without clamping them to a fabricated map cell.
Unplaced trains disable Move/Follow. Withdrawal, sale, disconnect and world
replacement stop following; sale/replacement clear information selection.
Selecting another train does not silently change the followed train. Dragging or
scrolling to pan stops following; zoom preserves it. Framing accounts for the
visible area beside/above the current panel. Name styles remain after replacement.
Native font rasterization and human pixel fidelity are not claimed.

**Settings → Color-test train settings** provides local day/night previews,
copyable XML and a `colorMap.xml` download. English template names translate the
seven Japanese `system/plugin.xml` names; other UI resources are paired EN/JA in
`app.json`. RGB triplets, template IDs and XML attribute names remain invariant.
These local drafts survive locale changes and shell navigation; replacement clears
them. Observer participants can preview/export.

Applying colors requires a separate confirmation. A single world-local fleet
configuration stores the template, four lowercase RGB colors and an independent
revision. It affects every color-test car in that world, including future purchases.
Other models and the prepared/process-global catalog remain unchanged. Owner and
Operator apply through the existing FIFO/epoch/authority/receipt path; Observer,
stale revision, invalid template/pack/palette and invalid saves reject atomically.
Peer edits keep a local draft but require Reset before applying again.

The override persists in the optional `fleet.colorTest` field of the current
Save9/protocol7 contract. Its first revision is 1. Graph reconstruction during
movement, reversal, purchase/sale, group/controller changes and save/load retains
it. It never changes car identities, occupancy, passenger capacity, clock, queues,
accounts or content pack hashes. Domain and browser validators verify registered
exact template references. Derived render catalogs and surfaces are consumer-local;
image decodes are cached, and presentation changes do not restart asset loading.

## Verification

Only selected tests were run; no complete web or backend suite is credited.

- **41/41 selected .NET tests:** color configuration/role/stale/receipt behavior,
  first configuration before stock, malformed palettes/templates, two purchased
  and deployed zero-capacity color-test trains, world isolation, graph operations,
  exact save/reopen, Outside-return fixture, fleet persistence and group tests.
- **75/75 selected client tests:** information/capacity/percentage/selection,
  framing and label validation; independent palette/night/XML vectors; existing
  content recipes, fleet/group state and wire contracts.
- **5/5 selected Chromium cases:** EN desktop and JA390×844/200% information and
  labels, unchanged exact save bytes and decode count for local controls; actual
  moving-head tracking, pan/zoom, Unplaced/replacement reset; all seven templates,
  locale/navigation draft retention, XML download, confirmed shared apply,
  Observer peer/stale draft, HTTP reopen and actual checkpoint process restart;
  Outside countdown-zero return, explicit reversal, withdrawal and sale.
- Independent browser oracles compare **28,672 pixels and 28,672 alpha picks**
  across seven templates, day/night and two level orientations, while verifying
  prepared-catalog immutability.
- Production web/generated/content/locales/Svelte checks pass with zero Svelte
  errors/warnings; the existing bundle-size advisory remains. Release backend,
  pinned source review, project boundaries and whitespace checks pass.

Inputs/logs: `tmp/p5-25-*`. Inspected EN/JA captures:
`tmp/p5-25-browser-evidence/information/`. Recreate gameplay-built browser inputs
with `FREETRAIN_COLOR_FLEET_OUTPUT` and `FREETRAIN_INFORMATION_OUTSIDE_OUTPUT`
while running the selected `Color_test_two` and `Information_follow` backend tests.
Run only this browser file with:

```sh
cd web
deno task build
deno task test:browser:run train-information.spec.ts --project=canonical --workers=1
```

Native/other-engine checks, Linux/container acceptance, optional-pack repairs,
full-fleet capacity, P5-26 cross-tool regression and P5-27 recovery retain their
separate owners. This slice does not close P5 or grant P6 passenger acceptance.
