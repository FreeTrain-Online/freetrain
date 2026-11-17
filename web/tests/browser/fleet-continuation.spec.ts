import { test } from './fixtures.ts';
import { continuationScenario, continuationScenarios } from './continuation-scenarios.ts';

for (const locale of ['en', 'ja'] as const)
for (const scenario of continuationScenarios)
  test(`P5-27 ${locale} fleet ${scenario}`, continuationScenario('fleet', '', locale, scenario));
