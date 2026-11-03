# Browser regression tests

Tests are grouped by the behavior they protect.

| Suite | Coverage |
| --- | --- |
| `session-lifecycle.spec.ts` | Connection gate, sign-in, asset readiness, disconnect/rejoin, leave and revoked access |
| `station-editing.spec.ts` | Unsaved drafts, focus traps, retargeting, stale revisions and interrupted edits |
| `ui-preferences.spec.ts` | Color scheme persistence, fallback, contrast and canvas preservation |
| `responsive-layout.spec.ts` | Viewport sizes, Japanese panels, scrolling, touch targets, enlarged text and viewport emulation |
| `keyboard-controls.spec.ts` | Toolbar/menu navigation, native editing, focus, Escape layers and command feedback |
| `manual-driving.spec.ts` | Driving input suspension and key release on focus, visibility, tool and lease changes |
| `map.spec.ts`, `map-panning.spec.ts` | Map shortcuts, panning, picking, camera, DPR and world replacement |
| `shell.spec.ts` | Integrated inspector, clock and track confirmation/cancellation workflows |
| `multiplayer.spec.ts` | Shared state, authority, leases and independent participant UI state |
| `persistence.spec.ts` | Save availability, replacement confirmation and recovery |
| `fleet-persistence.spec.ts` | Save9 inventory graph, two-peer scalar publication, rejected upload and reconnect; deployed continuation uses controlled-dispatcher backend/client tests |

Construction and terrain suites retain their existing functional names.
`browser-helpers.ts` holds shared interaction and world fixtures;
`layout-helpers.ts` holds viewport checks. Helpers do not register tests or hooks.
Tests requiring the prototype regression world remain in the
`prototype-regression` project; file organization does not change their backend.

See [browser validation commands](../../../README.web.md#prerequisites-and-checks)
for default, extended, performance and continuation coverage. From `web/`, run
a category against a current build with, for example:

```sh
deno task test:browser:run station-editing.spec.ts
```

Layout, preference and map measurements are written beneath the current
`FREETRAIN_ONLINE_BROWSER_EVIDENCE` directory in functional subdirectories.
Historical UX acceptance documents retain their original phase labels.
