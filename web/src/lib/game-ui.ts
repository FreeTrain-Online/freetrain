import type { messages } from './localization.ts';
export type Tool = 'inspect' | 'build' | 'remove' | 'terrain' | 'slope' | 'bridge' | 'girder' | 'tunnel' | 'garage' | 'signal';
export const panels = {
  newWorld: 'newWorld', inspector: 'selectionInspector', catalog: 'buildings', saves: 'saves', view: 'view',
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
  terrain: { kind: 'tool', tool: 'terrain', labelKey: 'terrainTool', icon: 'pointer', availability: 'mutation' },
  signal: { kind: 'tool', tool: 'signal', labelKey: 'signalTask', icon: 'rail', availability: 'mutation' },
  garage: { kind: 'tool', tool: 'garage', labelKey: 'garageTask', icon: 'rail', availability: 'mutation' },
  tunnel: { kind: 'tool', tool: 'tunnel', labelKey: 'tunnelTask', icon: 'rail', availability: 'mutation' },
  girder: { kind: 'tool', tool: 'girder', labelKey: 'girderTask', icon: 'rail', availability: 'mutation' },
  bridge: { kind: 'tool', tool: 'bridge', labelKey: 'bridgeTask', icon: 'rail', availability: 'mutation' },
  slope: { kind: 'tool', tool: 'slope', labelKey: 'slopeTask', icon: 'rail', availability: 'mutation' },
  demolish: { kind: 'tool', tool: 'remove', labelKey: 'removeTool', icon: 'cross', availability: 'mutation' },
  inspector: { kind: 'panel', panel: 'inspector', labelKey: 'inspector', icon: 'window', availability: 'live' },
  catalog: { kind: 'panel', panel: 'catalog', labelKey: 'buildings', icon: 'window', availability: 'live' },
  newWorld: { kind: 'panel', panel: 'newWorld', labelKey: 'newWorld', icon: 'window', availability: 'owner-save' },
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
  'slope-direction': field('slopeDirection', 'tool-card', 'live'),
  'slope-x': field('slopeX', 'tool-card', 'live'),
  'slope-y': field('slopeY', 'tool-card', 'live'),
  'slope-level': field('slopeLevel', 'tool-card', 'live'),
  'slope-preview': action('slopeCheck', 'tool-card', 'live'),
  'slope-confirm': action('slopeConfirm', 'tool-card', 'mutation'),
  'rail-action': field('railAction', 'tool-card', 'mutation'),
  'rail-auto-level': field('railAutoLevel', 'tool-card', 'live'),
  'rail-route': field('railRouting', 'tool-card', 'live'),
  'rail-from-x': field('railFromX', 'tool-card', 'live'),
  'rail-from-y': field('railFromY', 'tool-card', 'live'),
  'rail-to-x': field('railToX', 'tool-card', 'live'),
  'rail-to-y': field('railToY', 'tool-card', 'live'),
  'rail-pick-from': action('railPickFrom', 'tool-card', 'live'),
  'rail-pick-to': action('railPickTo', 'tool-card', 'live'),
  'rail-preview': action('railCheck', 'tool-card', 'live'),
  'rail-level': field('buildLevel', 'tool-card', 'live'),
  'rail-shape': field('trackShape', 'tool-card', 'live'),
  'rail-alignment': field('snapDirection', 'tool-card', 'live'),
  'rail-anchor': action('useAnchor', 'tool-card', 'live'),
  'authoring-enter': action('enterAuthoring', 'tool-card', 'owner-save'),
  'authoring-exit': action('exitAuthoring', 'tool-card', 'owner-save'),
  'authoring-confirm': action('confirmAuthoringMode', 'tool-card', 'owner-save'),
  'authoring-species': field('authoringSpecies', 'tool-card', 'mutation'),
  'authoring-ground': field('forestGround', 'tool-card', 'mutation'),
  'authoring-preview': action('previewDecorations', 'tool-card', 'mutation'),
  'terrain-action': field('cornerAction', 'tool-card', 'mutation'),
  'terrain-paint-stop': action('paintStop', 'tool-card', 'live'),
  'terrain-preview': action('cornerCheck', 'tool-card', 'mutation'),
  'terrain-add-site': action('cornerAdd', 'tool-card', 'mutation'),
  'terrain-confirm': action('confirmTerrain', 'tool-card', 'mutation'),
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
  'world-name': field('worldName', 'newWorld', 'owner-save'),
  'world-h': field('worldH', 'newWorld', 'owner-save'),
  'world-v': field('worldV', 'newWorld', 'owner-save'),
  'world-z': field('worldZ', 'newWorld', 'owner-save'),
  'world-water': field('worldWater', 'newWorld', 'owner-save'),
  'world-defaults': action('sourceDefaults', 'newWorld', 'owner-save'),
  'preview-world': action('previewWorld', 'newWorld', 'owner-save'),
  'create-world': action('createWorld', 'replace-modal', 'owner-save'),
  'cancel-world': action('cancel', 'newWorld', 'owner-save'),
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
  { id: 'inspect', shortcut: 'I', label: registry.inspect.labelKey, icon: '↖' },
  { id: 'build', shortcut: 'T', label: registry.rail.labelKey, icon: 'rail' },
  { id: 'bridge', shortcut: 'B', label: registry.bridge.labelKey, icon: 'bridge' },
  { id: 'slope', shortcut: 'S', label: registry.slope.labelKey, icon: 'slope' },
  { id: 'girder', shortcut: 'V', label: registry.girder.labelKey, icon: 'girder' },
  { id: 'signal', shortcut: 'F', label: registry.signal.labelKey, icon: '🚦' },
  { id: 'garage', shortcut: 'H', label: registry.garage.labelKey, icon: 'garage' },
  { id: 'tunnel', shortcut: 'U', label: registry.tunnel.labelKey, icon: 'tunnel' },
  { id: 'terrain', shortcut: 'G', label: registry.terrain.labelKey, icon: '⛰' },
  { id: 'remove', shortcut: 'R', label: registry.demolish.labelKey, icon: '×' },
] as const satisfies readonly { id: Tool; label: keyof typeof messages.en; icon: string; shortcut: string }[];
