import { test } from './fixtures.ts';
import { continuationScenario, continuationScenarios } from './continuation-scenarios.ts';

// All four source callback boundaries retain both locales and the complete
// journey. Each boundary can also be rerun from an independently prepared save.
for (const mode of ['service', 'reverse', 'turning', 'midnight'])
for (const locale of ['en', 'ja'] as const)
for (const scenario of continuationScenarios)
  test(`P6-25 ${mode} ${locale} station ${scenario}`, continuationScenario('station', mode, locale, scenario));
