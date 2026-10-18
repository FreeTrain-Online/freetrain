import type { messages } from './localization.ts';
export type Tool = 'inspect' | 'build' | 'remove';
export const panels = {
  inspector: 'selectionInspector', catalog: 'buildings', saves: 'saves', view: 'view',
  settings: 'settings', session: 'session', help: 'help',
} as const satisfies Record<string, keyof typeof messages.en>;
export type Panel = keyof typeof panels;

// Delivered commands and destinations only. Model/service own authorization.
type Availability = 'live' | 'mutation' | 'owner-save' | 'gate' | 'local';
type Descriptor = { labelKey: keyof typeof messages.en; icon: 'pointer' | 'rail' | 'cross' | 'window' | 'command' | 'switch'; availability: Availability } & (
  | { kind: 'tool'; tool: Tool }
  | { kind: 'panel'; panel: Panel }
  | { kind: 'menu'; menu: 'game' }
  | { kind: 'action'; destination: 'clock' | 'map' | 'tool-card' | Panel | 'session-gate' | 'replace-modal' | 'credits-modal' | 'license-modal' }
  | { kind: 'switch'; destination: 'view' | 'settings' }
  | { kind: 'field'; destination: 'tool-card' | Panel | 'session-gate' }
);
const action = (labelKey: keyof typeof messages.en, destination: Extract<Descriptor, { kind: 'action' }>['destination'], availability: Availability): Descriptor =>
  ({ kind: 'action', labelKey, icon: 'command', destination, availability });
const field = (labelKey: keyof typeof messages.en, destination: Extract<Descriptor, { kind: 'field' }>['destination'], availability: Availability): Descriptor =>
  ({ kind: 'field', labelKey, icon: 'command', destination, availability });
export const registry = {
  game: { kind: 'menu', menu: 'game', labelKey: 'game', icon: 'window', availability: 'live' },
  inspect: { kind: 'tool', tool: 'inspect', labelKey: 'inspectTool', icon: 'pointer', availability: 'live' },
  rail: { kind: 'tool', tool: 'build', labelKey: 'buildTool', icon: 'rail', availability: 'mutation' },
  demolish: { kind: 'tool', tool: 'remove', labelKey: 'removeTool', icon: 'cross', availability: 'mutation' },
  inspector: { kind: 'panel', panel: 'inspector', labelKey: 'inspector', icon: 'window', availability: 'live' },
  catalog: { kind: 'panel', panel: 'catalog', labelKey: 'buildings', icon: 'window', availability: 'live' },
  saves: { kind: 'panel', panel: 'saves', labelKey: 'saves', icon: 'window', availability: 'live' },
  view: { kind: 'panel', panel: 'view', labelKey: 'view', icon: 'window', availability: 'live' },
  settings: { kind: 'panel', panel: 'settings', labelKey: 'settings', icon: 'window', availability: 'live' },
  session: { kind: 'panel', panel: 'session', labelKey: 'session', icon: 'window', availability: 'live' },
  help: { kind: 'panel', panel: 'help', labelKey: 'help', icon: 'window', availability: 'live' },
  credits: action('credits', 'credits-modal', 'local'), // Also available in the session gate.
  license: action('license', 'license-modal', 'local'), // Also available in the session gate.
  run: action('run', 'clock', 'mutation'),
  pause: action('pause', 'clock', 'mutation'),
  step: action('step', 'clock', 'mutation'),
  'rail-level': field('buildLevel', 'tool-card', 'live'),
  'rail-shape': field('trackShape', 'tool-card', 'live'),
  confirm: action('confirmTrack', 'tool-card', 'mutation'),
  'cancel-draft': action('cancelPreview', 'tool-card', 'live'),
  controller: field('controller', 'inspector', 'mutation'),
  'take-control': action('takeControl', 'inspector', 'mutation'),
  'release-control': action('releaseControl', 'inspector', 'mutation'),
  'manual-input': action('manualHelp', 'map', 'mutation'),
  'rename-station': action('rename', 'inspector', 'mutation'),
  'reset-name': action('resetDraft', 'inspector', 'live'),
  'catalog-choice': field('selectedBuilding', 'catalog', 'live'),
  'height-cut': field('heightCut', 'view', 'live'),
  night: { kind: 'switch', labelKey: 'night', icon: 'switch', destination: 'view', availability: 'live' },
  winter: { kind: 'switch', labelKey: 'winter', icon: 'switch', destination: 'view', availability: 'live' },
  recolor: { kind: 'switch', labelKey: 'recolor', icon: 'switch', destination: 'view', availability: 'live' },
  'zoom-in': action('zoomIn', 'map', 'live'),
  'zoom-out': action('zoomOut', 'map', 'live'),
  'reset-camera': action('homeMap', 'map', 'live'),
  'find-train': action('findTrain', 'inspector', 'live'),
  sound: { kind: 'switch', labelKey: 'enableSound', icon: 'switch', destination: 'settings', availability: 'local' },
  language: field('language', 'settings', 'local'), // Same control also belongs in every session gate.
  'color-scheme': field('colorScheme', 'settings', 'local'), // Also available before joining.
  'save-slot': field('saveSlot', 'saves', 'owner-save'),
  'write-save': action('saveGame', 'saves', 'owner-save'),
  'load-save': action('loadGame', 'saves', 'owner-save'),
  'download-save': action('downloadSave', 'saves', 'owner-save'),
  'upload-save': field('uploadSave', 'saves', 'owner-save'),
  'replace-world': action('confirmLoad', 'replace-modal', 'owner-save'),
  reconnect: action('reconnect', 'session', 'live'),
  leave: action('leaveGame', 'session', 'live'), // Also offered by authenticated failure gates.
  connect: action('connect', 'session-gate', 'gate'),
  retry: action('retryNow', 'session-gate', 'gate'),
  'retry-leave': action('retryLeave', 'session-gate', 'gate'),
  reload: action('reload', 'session-gate', 'gate'),
} as const satisfies Record<string, Descriptor>;
export type ActionId = keyof typeof registry;


export const tools = [
  { id: 'inspect', label: registry.inspect.labelKey, icon: '↖' },
  { id: 'build', label: registry.rail.labelKey, icon: '╫' },
  { id: 'remove', label: registry.demolish.labelKey, icon: '×' },
] as const satisfies readonly { id: Tool; label: keyof typeof messages.en; icon: string }[];
