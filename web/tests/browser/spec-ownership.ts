/** Fail discovery for new, deleted or multiply assigned specs rather than silently
 * routing new canonical coverage through the prototype backend. */
export function validateSpecOwnership(
  files: string[],
  manifest: Record<string, string[]>,
) {
  const assigned = Object.values(manifest).flat();
  const errors = [
    ...files.filter((file) => !assigned.includes(file)).map((file) =>
      `Unowned browser spec: ${file}`
    ),
    ...assigned.filter((file) => !files.includes(file)).map((file) =>
      `Missing browser spec: ${file}`
    ),
    ...assigned.filter((file, index) => assigned.indexOf(file) !== index)
      .map((file) => `Duplicate browser owner: ${file}`),
  ];
  if (errors.length) throw new Error(errors.join('\n'));
}

export const browserSpecs = {
  'canonical': [
    'land-trading.spec.ts',
    'static-ground.spec.ts',
    'city-building.spec.ts',
    'city-transport.spec.ts',
    'city-heat.spec.ts',
    'fixed-city.spec.ts',
    'city-rendering.spec.ts',
    'station-continuation.spec.ts',
    'station-cross-tool.spec.ts',
    'station-information.spec.ts',
    'station-controller.spec.ts',
    'passenger-service.spec.ts',
    'passenger-feedback.spec.ts',
    'departure-bells.spec.ts',
    'station-yard.spec.ts',
    'passage.spec.ts',
    'platform-lanes.spec.ts',
    'platform-properties.spec.ts',
    'station-hosts.spec.ts',
    'wide-platform.spec.ts',
    'thin-platform.spec.ts',
    'station-transport.spec.ts',
    'station-building-domain.spec.ts',
    'station-building.spec.ts',
    'station-rendering.spec.ts',
    'fleet-reconciliation.spec.ts',
    'fleet-continuation.spec.ts',
    'fleet-cross-tool.spec.ts',
    'train-information.spec.ts',
    'fleet-groups.spec.ts',
    'fleet-rule-file.spec.ts',
    'signal-integration.spec.ts',
    'fleet-controller-manager.spec.ts',
    'game-toolbar.spec.ts',
    'train-runtime.spec.ts',
    'construction-regression.spec.ts',
    'cross-tool.spec.ts',
    'building-catalog.spec.ts',
    'object-removal.spec.ts',
    'railway-construction.spec.ts',
    'bridge-construction.spec.ts',
    'girder-construction.spec.ts',
    'garage-construction.spec.ts',
    'signal-construction.spec.ts',
    'pole-construction.spec.ts',
    'speed-limit.spec.ts',
    'stationary-construction.spec.ts',
    'tunnel-construction.spec.ts',
    'slope-construction.spec.ts',
    'railway-rendering.spec.ts',
    'reference-world.spec.ts',
    'terrain.spec.ts',
    'corner-terrain.spec.ts',
    'rectangle-terrain.spec.ts',
    'terrace-terrain.spec.ts',
    'authoring-terrain.spec.ts',
    'paint-terrain.spec.ts',
    'fleet-deployment.spec.ts',
    'fleet-trading.spec.ts',
    'fleet-persistence.spec.ts',
    'train-rendering.spec.ts',
  ],
  'startup': [
    'startup-world.spec.ts',
  ],
  'prototype-regression': [
    'keyboard-controls.spec.ts',
    'map-panning.spec.ts',
    'map.spec.ts',
    'multiplayer.spec.ts',
    'persistence.spec.ts',
    'primary-action.spec.ts',
    'responsive-layout.spec.ts',
    'session-lifecycle.spec.ts',
    'shell.spec.ts',
    'spatial.spec.ts',
    'station-editing.spec.ts',
    'ui-preferences.spec.ts',
  ],
  'continuation': [
    'mixed-continuation.spec.ts',
  ],
  'optional': [
    'optional-content.spec.ts',
  ],
  'capacity': [
    'capacity.spec.ts',
    'fleet-capacity.spec.ts',
    'railway-capacity.spec.ts',
    'station-capacity.spec.ts',
  ],
};
