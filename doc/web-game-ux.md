# Web game UX contract

This is the implementation contract for the FreeTrain web game UX.
The simulation, protocol, saves and canvas projection stay unchanged.
English and Japanese are acceptance languages.

Future parity slices use the original manual's chapters 2–4 as the player-workflow
oracle. The [manual mapping and browser adaptation contract](web-parity-manual.md)
specify toolbar shortcuts, focused construction windows, station/platform tabs,
purchase versus owned-train placement, and independent timetable management.
The [online-manual supplement](web-parity-online-manual.md) adds group timetable
inheritance/reparenting, terrain modifiers, revised creation routes and recovery
guidance, subject to its explicit source gaps and coverage dispositions.
Those are planned subsystem requirements; the operation table below continues to
describe delivered behavior only.

## Implementation status

The production app now uses the approved visual foundation:
a fixed viewport, a 40 px desktop command bar and tool strip, square
controls, semantic surface colors and overlay panels. At 1280×720 the canvas
measures 1240×680 (91.49% of the viewport). Panels preserve its element, bounds,
image decodes and live camera. Narrow layouts use a tool row and bounded sheets;
long content scrolls inside the panel or session dialog.

The first playable slice includes picked train/station/building/track inspectors,
clock controls, explicit rail/removal Confirm and Cancel, and access to every
previous operation through Game, View and Session. Train operations capture the
selected ID; station edits retain the selected ID and original revision through
confirmation. Catalog choice is independent of map selection. Manual input
requires deliberate canvas focus, Inspect, a selected manual train and its owned
lease. Panel/menu focus and session transitions release held input.

Before a session is usable, a mandatory localized dialog covers a separate,
immutable bundled demo scene. Bootstrap, joining/synchronizing, sign-in,
connection loss, renewed authentication, incompatibility, leaving/failed leave
and render failure have distinct presentations. Authorized discovery/join remains
automatic. Chrome appears only after a coherent live read, decoded images and a
successful first live draw. The demo never enters the session model or save and
command paths. If its images fail, session controls remain available over a
plain background.

The remote client exposes observational connection stage, retry time and
reauthentication metadata. It still owns transport and backoff. A failed Leave
latches revocation intent and offers only Retry leave; late reads/results cannot
reopen the old game. Confirmed leave discovers access policy while retaining the
signed-out gate until explicit authorization. Same-world reconnect restores the
live camera after synchronization; a different epoch clears selection/geometry
and uses initial framing. Uncertain commands are not replayed. A same-socket
resync retains the pending mutation until its acknowledgement or disconnect.

Production owners are [App.svelte](../web/src/App.svelte) (composition, panels,
menu and derived gate), [MapView.svelte](../web/src/MapView.svelte) (persistent
canvas, separate demo/live cameras, readiness and tools),
[PanelFrame.svelte](../web/src/PanelFrame.svelte) (bounded nonmodal frame),
[SessionGate.svelte](../web/src/SessionGate.svelte) (mandatory dialog/focus), and
[game-ui.ts](../web/src/lib/game-ui.ts) (typed delivered-operation registry).
[DecisionModal.svelte](../web/src/DecisionModal.svelte) uses the browser modal
top layer for dirty-edit and shared-world replacement decisions; it traps focus,
makes background controls inert and consumes dismissal gestures.
[ui-preferences.ts](../web/src/lib/ui-preferences.ts) and
[SchemeSelect.svelte](../web/src/SchemeSelect.svelte) own scheme validation and selection.
[schemes.css](../web/src/schemes.css) carries the curated surface roles from UX0.
Existing shell-model, save-transfer and remote-session-client remain the command
and authority boundaries; no simulation/protocol/save-format change is introduced.

UX2 completes the current workflows. A station draft blocks panel close/switch,
retargeting, tool changes and explicit session navigation with Apply / Discard /
Keep editing. Apply navigates only after a successful authoritative result for
the original draft; rejection retains the decision. Revised/deleted targets keep
the original draft visible and disable Apply until explicit Reset/Discard.
Disconnect and epoch replacement override the decision, discard the interrupted
draft with feedback and submit nothing. Native input/IME Escape remains local;
Escape from other panel controls opens the dirty decision or closes one layer.

Save load/upload now opens a modal naming the consequence for every participant,
with captured epoch and slot/file, explicit Confirm load and Cancel. A pending
save excludes other local world mutations, panel transitions and Leave; it is
never replayed. Slot choice, catalog choice, construction parameters and local
view options survive panel switches. Closing a panel restores its launcher (the
Game button when a menu item has gone); picked inspectors restore the canvas.
Panel headings receive initial focus, and station actions stay in a fixed footer.

A primary panel clears construction geometry while preserving tool/options.
The full tool card resumes after Close with a fresh target required; while a
panel occupies the map, the context line explains suspended targeting. This
avoids overlapping Confirm and panel actions in compact layouts. Middle and
Space drag consume their entire gesture even when Space is released first.
Toolbar zoom and Find train use the measured unobscured map region; a zoom
percentage is shown without live announcements. Target revision changes cancel
stale construction geometry, and late acknowledgements cannot clear a newer draft.

Settings and every session gate offer Evergreen, Classic blue, Slate and Plum.
Selection updates semantic root colors immediately; only the allowlisted ID in
`{version: 1, scheme}` is stored under `freetrain.ui.v1`. Missing, corrupt,
obsolete or invalid preferences use Evergreen. The preference is applied before
mounting the app and survives demo/live, refresh and Leave. Language retains its
existing storage path. No credentials, leases or world drafts are persisted.
Run/Pause uses one stable button derived from confirmed clock state; keyboard
focus stays on that button when its action changes. Narrow Game overflow retains Step.

UX3 adds explicit Expand/Reduce controls to narrow portrait sheets. Initial sheets
are capped at 52% of the stage; short windows use side sheets. Primary controls
have at least 32 px desktop and 44 px coarse-pointer targets, including square
icon buttons. Nonmodal panels expose named dialog semantics. Command/tool
bars have one Tab stop each, arrow/Home/End navigation and visible focus; Game
supports Up/Down/Home/End. Native fields, composition and browser modifier
shortcuts retain their own behavior. Meaningful tool/selection/driving state is
announced to screen readers without a visible map label; command results remain
separate, and ticks and pointer movement are silent.
Compact feedback sits clear of navigation and stays empty while idle. A success notification expires after
four seconds; failures stay visible and also appear beside station/tool edits.
Role, save and lease explanations are available as readable text, including for
keyboard users whose disabled controls cannot receive focus.

The shell follows the visible viewport's height/offset for keyboard occlusion.
Pinch magnification preserves layout dimensions instead of shrinking the game
again. Safe insets remain part of the shell padding. Panels and gates scroll
internally, and sheet expansion never changes the canvas camera or bounds.
Only the existing locale preference and versioned allowlisted scheme ID are
persisted; appearance and audio intent stay local to the running app.

Verification lives in `../web/tests/browser/*.ts`,
the shell/map/persistence/multiplayer journeys and shell-model tests. Integrated
evidence covers six required viewport sizes, English/Japanese, DPR 1/2 and live
DPR changes, doubled text, coarse-pointer and reduced-height emulation, preset
contrast, focus/cancellation, authority and independent local UI. The profile
runner supports `FREETRAIN_PROFILE_EVIDENCE` so new evidence does not overwrite
the UX0 baseline; it also measures 30 panel opens and verifies canvas identity,
camera, bounds and unchanged image decode count.

Native desktop browser zoom, Japanese IME, physical touch/virtual keyboards,
safe-area devices and assistive technology still need recorded checks. Chromium
emulation does not establish those results; no other-engine coverage is claimed.
UX3's implementation and automated evidence do not close the remaining device
acceptance gate or establish game parity. Neither the adaptation/input work nor
the regression closure requires additional game logic. Roads, terrain, building
placement and other deferred gameplay retain their parity-plan prerequisites.

## Current operations and destinations

Every row describes a delivered baseline operation. IDs refer to registry
entries; a panel's fields and entity actions are deliberately not toolbar buttons.
“Mutation” means live Owner/Operator, connected, no pending mutation or save;
the server remains authoritative. Owner-only saves retain the current frontend
policy, including list/download, even if service permissions are broader.

| Stable ID / current source | Destination and behavior | Availability / preserved detail |
| --- | --- | --- |
| `game`, `saves`, `settings`, `help` / App, SaveControls | Game menu → one primary panel | Always reachable in live chrome; diagnostics retain prototype limitations |
| `run`, `pause`, `step` / App → model.send | Top clock group; one Run/Pause presentation derived from confirmed paused state, separate Step | Mutation; Step only paused; do not optimistically flip the clock |
| confirmed tick, cash / App | Top summary; compact overflow on constrained widths | Read-only; `Intl.NumberFormat(locale).format(BigInt(value))`, no floating-point conversion; tick is not a wall clock |
| `inspect` / MapView | Exclusive default tool, selection → inspector | All live roles; primary click picks; primary drag >3 CSS px pans without selecting on release |
| `rail`, `demolish` / MapView → model.send | Exclusive tools; compact card with target, Confirm, Cancel | Mutation; choose target then confirm; no pause on tool entry |
| `rail-level`, `rail-shape` / MapView | Tool card fields; level 0/1; eight existing shapes | buffer, eastWest, northSouth, curveSE/SW/NE/NW, junctionE only; parameter changes invalidate geometry |
| `confirm`, `cancel-draft` / MapView | Tool card footer, immediate actions | Confirm needs a target; removal needs a current track/revision; pending prevents duplicate submission; Cancel sends nothing |
| `inspector` / map pick and App details | Selected train/station/building/track panel | Stable ID; track identity is coordinate tuple + world epoch, revision is an edit precondition; never fall back to first entity |
| `controller`, `take-control`, `release-control` / App → model | Selected-train inspector; confirmed controller select, holder and handoff buttons | Mutation; controller change retains lease acquisition path; Release only own lease; selection never acquires control |
| `manual-input` / MapView → model.input | Focused map, Inspect, selected manual train, matching local lease; accessible driving status | Shift pass, Up depart, Down reverse, Left/Right junction; 150 ms renewal path and server expiry retained; no automatic reacquisition from held keys |
| `rename-station`, `reset-name` / App → model.rename/resetDraft | Selected-station inspector: confirmed name, draft, Apply, Reset | Mutation for Apply, trim/nonempty, max 120, target ID/revision captured with draft; preserve compositionstart/end, isComposing/keyCode 229 safeguards |
| `catalog`, `catalog-choice` / App → model.selectBuilding | Game → Buildings panel; thumbnails/details for pier variant 1 height 1 and condo variant 1 heights 2–5 | Local browsing; five entries; no Place button, price, rotation or legality claims; inspection must stop overwriting catalog choice |
| `view`, `height-cut` / MapView | View panel; cut range 0–15 | All live roles; distinct from construction level; hidden target offers explicit Raise height cut action |
| `night`, `winter`, `recolor` / MapView | Independent View switches | Local appearance only; decorative water/fence/slope explanation in Help |
| `zoom-in`, `zoom-out`, `reset-camera` / MapView | Map corner cluster and View actions | All live roles; wheel zoom at pointer, buttons at unobscured map center; reset explicit |
| `find-train` / MapView | Selected-train inspector and View → selected train | Frame selected stable ID, avoid panel occlusion; absent selection explains “Select a train”; no first-train fallback |
| `sound` / App → browser-audio | Settings switch with status and Retry enable | Gesture-gated station preview; enable failure visible; events not replayed on refresh/reconnect/load |
| `language` / App → model.setLocale | Settings and every session gate | en/ja, preserve existing locale storage/migration; no credential persistence |
| `color-scheme` / new UX scope | Settings dropdown and session gates, independent local preference | Curated presets only; no free color picker, arbitrary stylesheet URL, or shared-world command |
| `save-slot`, `write-save`, `load-save` / SaveControls | Saves panel: slots 1–3 and checkpoint; Save/Load | Owner-only current frontend policy; checkpoint read-only; empty slots cannot load; busy feedback retained |
| `download-save`, `upload-save` / SaveControls | Saves panel; download and labeled JSON file input | Owner-only frontend policy; upload max 1 MiB, clear file input after selection; invalid data leaves world intact |
| `replace-world` / SaveControls | Modal over Saves; shared consequence and explicit Confirm load/Cancel | Owner, current epoch, slot/file captured; load/upload replaces shared game, clears input and starts paused; no automatic retries |
| `session`, `reconnect`, `leave` / App → model | Session panel shows confirmed connection/role and unresolved result; reconnect and Leave | Reconnect unavailable during leave; leave blocked during save; mute audio/clear secret; Retry leave after failure |
| `connect`, `retry`, `retry-leave`, `reload` / App, remote client | Mandatory session gate; access-key form / recovery actions | Automatic discovery/join remains; empty key allowed only when anonymousAccess is true; incompatible requires update/reload |

`game` is a menu, the other launchers open one panel. On narrow layouts Game
also contains View and Session and clock Step/cash/tick overflow. No current
operation is removed until its replacement destination works, including saves,
reconnect and Leave. Keep disabled mutation explanations keyboard accessible;
do not rely on a disabled button's hover tooltip.

## Interaction and focus transitions

Exactly one of Inspect, Rail, Demolish is selected. Clicking the active
construction tool returns to Inspect. A different tool/parameter clears held
input, releases stale pointer capture and cancels geometry, without commands or
clock changes. Confirm clears submitted geometry on success, retains a correctable
draft on rejection, and leaves the tool selected. An uncertain outcome locks the
submitted draft against replay; require fresh authoritative review/targeting.

One primary panel, one optional compact tool card, one menu and one modal are
enough. A panel may coexist with a selected construction tool, but targeting is
suspended, its old geometry cleared, and its tool card says “Close panel to
choose a target.” Closing resumes the tool with no target. Never overlap Confirm
with panel/map navigation. Narrow and compact layouts can put tool options in
the primary sheet; this is the same state, not a second panel.

| Event | Transition |
| --- | --- |
| Same panel launcher | Close, subject to dirty-edit decision; return focus to launcher |
| Other panel launcher | Request panel switch; retain safe filters/options; gate dirty edits before switching |
| Pick object in Inspect | Select stable entity and open/update inspector; a dirty old inspector requires Apply/Discard/Keep editing before retargeting |
| Close dirty form, select different object, switch tool | Modal offers Apply, Discard, Keep editing; Apply is only enabled with valid target/authority and follows normal confirmation; do not navigate until accepted |
| Entity deleted/revised, role lost | Show unavailable/conflict with original draft; never retarget; disable Apply when invalid, offer explicit Reset/Discard; revision change never silently updates draft precondition |
| Disconnect / epoch change / leave | Safety transition overrides dirty decision: clear targets/geometry/input; no submission; explain interrupted draft; never restore it into another epoch |
| Open/focus panel/menu/modal | Clear held keys, cancel map gesture/capture; lease is not implicitly released; renewal does not resume |
| Modal closes | Restore initiating control if present; otherwise safe current panel/launcher; never submit on focus |
| Deliberate construction entry / panel close to targeting | Focus canvas with preventScroll and announce tool; no pointer event replay |

Escape acts once in priority order: mandatory session modal consumes it; a
dismissible modal cancels; menu/popover closes; focused form control keeps native
editing/IME behavior; primary panel requests close; draft cancels; construction
returns to Inspect. A form's native Escape must not cascade to world cancellation.
No Escape closes two layers. Modal background is inert (DOM and map handlers),
focus trapped; backdrop dismissal consumes pointer-down/up and cannot click
through. Nonmodal panels have labeled regions and initial heading/first control
focus; do not claim aria-modal or trap focus. Panel scrolling consumes wheel;
empty overlay space alone passes input through.

Map panning: drag threshold retained; middle drag in all modes; Space+primary
outside editable/IME controls. A moved/cancelled gesture never picks/builds on
release. Manual arrows/Shift run only with deliberate canvas focus, Inspect,
selected manual train and owned lease. They never pan the camera. Reset held
input on blur, hidden tab, overlay focus, tool/parameter change, disconnect,
epoch/target/controller change and lease loss; preserve reset delivery/server
expiry. No stored key state resumes on focus. Toolbar keyboard navigation uses
roving focus (arrows within group, Home/End); Tab enters/exits groups. Native
select/input arrow keys win. Assign no new global letter shortcuts in UX1.

## Session gate state machine

UI presentation derives from existing client/model state plus asset readiness;
it must not be encoded as a second transport or simulation. `loading` covers bootstrap/join/resync; UX1 adds observational stage and retry-time
metadata while keeping transport ownership in remote-session-client. A 401 after
authorized use is presented as authentication required again, without guessing
expiry versus revocation. Loading denotes an attempt in flight; Retry now is
available only during disconnected backoff. Do not infer causes from guessed timers.

| State | Entry / displayed scene | Actions / next state |
| --- | --- | --- |
| bootstrap | Initial or explicit reconnect; demo and Connecting status | Existing discovery automatically → sign-in / joining / unavailable / incompatible |
| sign-in | bootstrap 401 or confirmed deliberate leave; demo, welcome/key/language | Connect (single in flight), invalid key inline, anonymous hint only if advertised; accepted → bootstrap |
| joining / synchronizing | Bootstrap validated; WebSocket join or resync pending; demo | Loading game; timeout/failure → unavailable; current-generation coherent snapshot → wait for render |
| live-ready | Current client ready + validated coherent authoritative read + live required images decoded + first live draw ready, no leave latch | Reveal live scene/chrome atomically, Inspect/no draft/held keys; no extra click for authorized autojoin |
| disconnected | A formerly ready transport loses socket or coherence; swap to demo | Existing bounded automatic backoff, status; Retry now only between attempts, Leave game; uncertainty stays visible |
| unavailable | Initial network/5xx/timeouts; demo | Cannot reach game, retry policy/actions as above, no stale editable world |
| auth-required again | 401 after prior authorization; demo; clear credentials/displayed read | Explain sign-in needed again without asserting expired vs revoked unless known; sign in → bootstrap |
| incompatible | 426 / compatibility mismatch; demo or plain fallback | Explain client/server mismatch; Reload after update, language and Leave where authenticated; no retry spin |
| leaving | Explicit Leave; generation invalidated, read cleared, demo; close other overlays | Signing out; prevent reconnect/Connect/duplicate leave until revoke resolves |
| leave-failed | revoke request times out/non-ok; demo | Retry leave; preserve leave intent latch; no Retry now/reconnect path into old cookie session |
| render-failed | Required live assets fail; demo if usable else plain canvas | Actionable Reload/retry assets + Leave/language; never expose unusable live input; sign-in/retry do not depend on demo assets |

Existing model `generation`/client disposed guards reject late command/read
callbacks; extend that discipline to asset/scene completions. The leave latch
survives failure until revocation is confirmed; UX1 blocks ordinary reconnect
after leave failure. Successful leave reconnects to discover `anonymousAccess`
while preserving deliberately signed-out presentation; it cannot reveal an old
world without explicit authorization.

Store live camera in memory separately before showing demo. Same-epoch reconnect
uses a fresh authoritative read then restores camera; different epoch resets
selection/drafts and initial framing. A pending accepted/rejected command belongs
to its submitting generation/draft; reconnect never replays it. Keep unresolved
feedback across modal → game and demand user review, not a false success toast.
Leave is explicit revocation, not merely a socket close.

Demo is a bundled immutable scene rendered locally with existing assets; its
epoch/IDs are presentation-only. No service requests, save/catalog subscriptions,
commands, lease acquisition or input routing use demo entities. Separate
`demoScene/demoCamera` from `liveRead/liveCamera`; don't assign demo to model.read.
The development mockups use a local fixture to illustrate this boundary,
not a new game/service endpoint. Demo decode failure leaves an accessible plain
background and all gate actions usable. Scene/modal changes never pause the
shared world. Existing last-operator disconnect policy remains server-owned.

## Component and state ownership

| Owner | State / responsibility |
| --- | --- |
| GameShell | Fixed viewport/responsive arrangement, OverlayHost, derived session gate |
| CommandBar / ToolStrip | Registry-driven labels/icons/pressed/expanded/disabled presentation; emit intents |
| UI transition module | `tool`, `panel`, `menu`, `modal`, selected entity `{epoch,kind,id}`, catalog choice, draft identity, launcher/focus destination |
| Inspector panels | Train/station ID-bound forms; rename `{epoch,id,expectedRevision,value,dirty,composing}`; authoritative values separate |
| Tool card | `{draftId,epoch,tool,level,shape,position,targetRevision?,status}`; status local-unvalidated / submitting / rejected / uncertain; relevant change invalidates |
| MapStage / MapView | One persistent canvas/renderer, local camera, pointer capture, high-frequency hover, pending paint; report selection and readiness |
| SessionGate / DemoBackdrop | Mandatory modal presentation; separate demo scene/camera; local decode failure fallback; no session mutation beyond explicit authorized intents |
| OverlayHost / PanelFrame | Named layers, inertness, gesture isolation, focus restoration, bounded internal scrolling; one modal |
| Save panel / existing transfer module | Slots/file/busy/confirmation and HTTP validation; cross-feature busy coordinator excludes overlapping mutation/leave |
| Existing shell model / remote client | Confirmed read/roles/connection, generation/command IDs, pending/ambiguous results, input reset and retries; server remains sole authority |
| Settings/audio | Locale, color-scheme ID, gesture-gated sound, versioned safe view preferences only |

Implement a small typed registry in the frontend using the stable operation IDs
and behavior kinds described above. Keep entries limited to delivered operations
and shell destinations; handlers continue through the model/transfer modules.
The UX1 model accepts selected train/station identities and captures station
revision with its draft. Compatibility defaults remain for existing model clients;
the production inspector passes picked IDs. Capture the target across async lease
acquisition and recheck generation, epoch and target before the controller command. Keep one pending world mutation; held-input reset/renewal
retains its existing transport path and must not be accidentally queued behind
an open panel. Do not build a generic command bus/plugin framework.

Keep canvas instance, asset decode cache and camera outside panel lifecycle.
Opening a panel never changes canvas bounds or calls home/setRead just for panel
state. Shared read updates still drive actual world redraws; pointer movement
must not publish through the entire shell. Version safe preferences (locale,
color-scheme ID, audio intent, appearance); never serialize credentials, lease IDs, selected
entities, pending commands, destructive tools or drafts.

## Visual and responsive tokens

CSS px / initial values; mockup CSS is a review fixture, tokens become shared
production CSS in UX1. Maintain focus contrast and readable Japanese text.

| Token | Value / rule |
| --- | --- |
| `--radius` | `0` on all authored controls, surfaces, tooltips, badges, menus, dialogs |
| `--space-1/2/3/4` | `2px / 4px / 6px / 8px`; controls 2–4 px, group gap 4 px, panel padding 8 px |
| `--bar`, `--strip`, `--target` | Fine pointer 40 / 40 / 32 px; coarse 52 / 52 / 44 px; narrow strip becomes top tool launcher |
| `--panel` / tool card / modal | 352 px (range 320–384); card ≤320; modal ≤440 px; all bounded by visible viewport minus safe insets and 8 px shadow clearance |
| `--surface`, `--field`, `--ink`, `--muted` | `#d4d4cc`, `#fff`, `#182522`, `#404c46`; opacity only for disabled decoration, keep explanation readable |
| `--border`, `--raised`, `--inset` | 1px `#66716b`; light top/left + dark bottom/right; invert edges for pressed/input |
| `--shadow` | `3px 3px 0 #17241e66`; permanent bars border only; no large blur |
| `--accent`, `--focus`, `--danger` | `#184f45`, `#005bd1`, `#8d2920`; active label + inset/aria-pressed, visible 2px focus outline with 2px offset; danger uses text/icon and crossed preview |
| Typography | system sans-serif with Japanese fallback, 13px/1.45 normal, 14px form text, 12px secondary; text zoom allowed; tabular numeric summaries |
| Layers | canvas 0, feedback/navigation 10, tool card 20, primary panel 30, menu/popover 40, modal scrim/dialog 50/51, notifications 60 (not over modal/actions) |

### Selectable UI color schemes

Expose a labeled native dropdown in Settings and the mandatory session gate so
the preference is available before joining. Initial presets: **Evergreen** (the
approved mockup), **Classic blue**, **Slate**, **Plum**; localize display names
but persist stable IDs `evergreen`, `classic-blue`, `slate`, `plum`. No free color
picker in this milestone. These are UI schemes; canvas ground, sprites, night,
winter and recoloring keep their separate display semantics.

Use semantic CSS custom properties rather than component-specific hex colors.
These are distinct conceptual roles even when a preset gives them the same value:

| Conceptual element | CSS property | Evergreen background / use |
| --- | --- | --- |
| Menu bar | `--ui-menu-bar` | `#d4d4cc`; permanent command bar and tool strip |
| Window background | `--ui-window-background` | `#d4d4cc`; panel/dialog bodies, menus and neutral HUD surfaces |
| Active main button | `--ui-button-primary` | `#184f45`; enabled primary action and selected/pressed tool |
| Active alternative buttons | `--ui-button-alternative` | `#eeeee8`; enabled secondary actions such as Close/Cancel |
| Inactive button | `--ui-button-inactive` | `#d0d2cb`; disabled/unavailable actions |
| Window title bar | `--ui-title-bar` | Defaults to `var(--ui-button-primary)`; independent override supported |

“Active” in this vocabulary means enabled; selected/pressed is still a separate
interaction state conveyed with border/inset and `aria-pressed`. Disabled styling
wins over primary/pressed styling. The mockup is approximately three tones,
with a further disabled shade; its green title bars differ from the gray top
menu bar. Sharing colors must not collapse those roles into one variable.

Preset stylesheets define only role values. Component styles refer to those
properties, never to a scheme ID. Bundle the curated styles locally; a selector
such as `:root[data-ui-scheme="classic-blue"]` makes stylesheet overrides simple:

```css
:root[data-ui-scheme="classic-blue"] {
  --ui-menu-bar: #c0c0c0;
  --ui-window-background: #c0c0c0;
  --ui-button-primary: #000080;
  --ui-button-alternative: #ececec;
  --ui-button-inactive: #bdbdbd;
  /* --ui-title-bar inherits the primary color unless explicitly overridden. */
}
```

One bundled file or one local file per preset is fine; the public contract is
the semantic property names and allowlisted scheme IDs. No arbitrary user CSS
or remote stylesheet loading is in scope. Production keeps the root scheme
attribute as local UI state, set before first paint from a versioned preference;
invalid/missing/obsolete IDs fall back to Evergreen. Persist only the ID, not raw
colors/CSS, in safe local preferences, never in world saves or session secrets.

Text colors are not preset options: retain fixed readable dark normal text,
white title/primary text, muted/disabled text and danger text. Keep field
backgrounds white. Curate surface colors around those foregrounds and check
contrast in each preset; do not offer a font-color picker. The yellow notice
surface has a separate `--ui-notice-background` token (initially `#f4e9c6`) with
fixed text/border and meaning; current presets leave it unchanged. Focus rings,
danger/warning semantics, borders and typography remain shared, not a full
Windows appearance editor. A later change may theme notices without changing
the five required roles.

Apply a selection immediately by changing the root scheme attribute, without
remounting canvas/panels, decoding assets again, resetting camera, losing form
drafts, changing tool selection or sending commands. Scheme selection itself is
not a dirty form requiring Apply. Keep the same scheme across demo/live and
reconnect/leave; changing settings still follows ordinary held-input focus rules.
UX1 supplies roles/styles; UX2 delivers dropdown/persistence; UX3 checks every
preset's text, focus/pressed/disabled states, notices, English/Japanese layouts,
refresh/fallback behavior and unchanged renderer/session state.

### Responsive layout

Desktop ≥1024 px: 40 px bar + strip, right panel 8 px inset. At 1280×720 the
resting canvas is 1240×680 = 91.49% of viewport (surface, before overlays).
Compact 640–1023: grouped overflow, bounded right sheet; suspend tool targeting
while a primary sheet is open. Portrait <640 with height >600: 44 px targets,
52 px bar plus 52 px tool row; initial bottom sheet ≤52% of stage, Expand/Reduce controls
permits larger content, header/footer always reachable. At height ≤600 use a
side sheet, regardless of portrait breakpoint. The initial narrow rail card is
a bottom sheet too; map remains visible above it. Sheet body scrolls; document
does not. Modals may temporarily cover more map.

Use `100vh` then `100dvh`, bounded grid `minmax(0,1fr)`, zero min sizes and
logical properties; account for safe areas and visualViewport/keyboard height.
Never solve overflow by clipping controls. Move summaries to menus under text
zoom; preserve native zoom. Panel/map navigation avoids occluded regions.
