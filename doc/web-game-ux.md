# Web game UX contract

This is the implementation contract for the FreeTrain web game UX.
The simulation, protocol, saves and canvas projection stay unchanged.
English and Japanese are acceptance languages.

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
| `manual-input` / MapView → model.input | Focused map, Inspect, selected manual train, matching local lease; compact Driving label | Shift pass, Up depart, Down reverse, Left/Right junction; 150 ms renewal path and server expiry retained; no automatic reacquisition from held keys |
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
it must not be encoded as a second transport or simulation. `loading` alone
currently covers bootstrap/join/resync, and the client does not expose retry
timing or auth-expiry cause. Add small observational status metadata as needed
(stage, attempt-in-flight, next retry, auth cause), keeping transport ownership
in remote-session-client. Do not infer those causes from guessed timers.

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
must survive failure until revocation is confirmed; current reconnect is allowed
after leave failure and must be blocked in UX1. Successful leave currently
reconnects to discover `anonymousAccess`; keep that discovery but preserve
deliberately signed-out presentation, never auto-enter using the old cookie.

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
Extend model methods to accept a selected train/station identity and revision
(today they use `[0]`). Capture the
target across async lease acquisition and recheck epoch/selection before the
controller command. Keep one pending world mutation; held-input reset/renewal
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
production CSS during UX1. Maintain focus contrast and readable Japanese text.

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
or remote stylesheet loading is in scope. Production will keep the root scheme
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
UX1 introduces roles/styles; UX2 wires dropdown/persistence; UX3 checks every
preset's text, focus/pressed/disabled states, notices, English/Japanese layouts,
refresh/fallback behavior and unchanged renderer/session state.

### Responsive layout

Desktop ≥1024 px: 40 px bar + strip, right panel 8 px inset. At 1280×720 the
resting canvas is 1240×680 = 91.49% of viewport (surface, before overlays).
Compact 640–1023: grouped overflow, bounded right sheet; suspend tool targeting
while a primary sheet is open. Portrait <640 with height >600: 44 px targets,
52 px bar plus 52 px tool row; initial bottom sheet ≤58% of stage, Expand control
permits larger content, header/footer always reachable. At height ≤600 use a
side sheet, regardless of portrait breakpoint. The initial narrow rail card is
a bottom sheet too; map remains visible above it. Sheet body scrolls; document
does not. Modals may temporarily cover more map.

Use `100vh` then `100dvh`, bounded grid `minmax(0,1fr)`, zero min sizes and
logical properties; account for safe areas and visualViewport/keyboard height.
Never solve overflow by clipping controls. Move summaries to menus under text
zoom; preserve native zoom. Panel/map navigation avoids occluded regions.

## Review and implementation handoff

Review artifacts cover resting map, unvalidated rail target, selected train,
shared-world replacement, welcome/sign-in and disconnected recovery for desktop,
Japanese compact and Japanese narrow sizes.

Decisions reconciled against the local foundation plan:

- Game overflow includes Buildings, Saves, Settings and Help; View/Session move
  there only on narrow layouts, leaving tools and confirmed pause discoverable.
- Current time is displayed as tick, with no invented date/rate/speed control.
- Catalog selection is independent of map selection; no unsupported Place action.
- Shape/level live in the rail card, height cut in View; a selected tool survives
  a panel, its geometry does not. A panel suspends targeting, not inspection/pan.
- Uncertainty locks the submitted draft against replay. Known construction rates
  can appear as explanatory rules (100 per changed tile; buffer extension 200;
  removal no refund), never as a validated quote for an arbitrary preview.
- Leave-failed cannot expose ordinary reconnect; render readiness is separate
  from transport readiness. Both require UX1 behavior beyond current model state.
- Selected-entity forms require explicit target/revision parameters in the model;
  preserving `[0]` while changing the visuals would fail the inspector requirement.

No unresolved specification blocker remains for UX1. The approved direction
includes the color-scheme extension; dimensions and preset values can be refined
from measured usability. Native Japanese IME, touch, virtual keyboard and other browser engines
are not established by mockup screenshots. There are no roads, terrain tools,
building placement, Undo/Redo, speed modes or speculative parity buttons.

UX1's first slice must deliver Inspect → selected inspector and Rail → explicit
Confirm/Cancel alongside clock and mandatory session gates. Migrate saves and
session controls before retiring their old UI. UX3 must measure panel lifecycle,
renderer performance and all acceptance rows against the fresh UX0 baseline;
mockup screenshots do not check any UX1–UX3 implementation box.
