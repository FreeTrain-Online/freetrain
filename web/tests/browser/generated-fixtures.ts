import { statSync } from 'node:fs';
import { basename, matchesGlob, resolve } from 'node:path';

type Consumer = string | { file: string; title: RegExp };
type FixtureFamily = {
  env: string;
  producers: string[];
  suffixes: string[];
  consumers: Consumer[];
};

/** Every generated input and companion oracle has one producer and run-owned path. */
export const fixtureFamilies = {
  passengerFeedback: {
    env: 'FREETRAIN_PASSENGER_FEEDBACK_OUTPUT',
    producers: [
      'Real_city_yards_and_train_deposit_literal_board_and_unload_heat_once_with_exact_reopen',
      'Failed_feedback_minute_rolls_back_all_state_and_previous_transients',
    ],
    suffixes: ['', '.after.json', '.before.json', '.snapshot.json', '.delta.json',
      '.unload', '.unload.after.json', '.unload.before.json', '.unload.snapshot.json', '.unload.delta.json', '.rejected'],
    consumers: ['passenger-feedback.spec.ts'],
  },
  cityPopulation: {
    env: 'FREETRAIN_CITY_POPULATION_OUTPUT',
    producers: ['Population_refresh_fixture_has_real_completed_supply_and_atomic_heat_continuation'],
    suffixes: ['', '.before.json', '.snapshot.json', '.delta.json'],
    consumers: [{file:'city-building.spec.ts',title:/P7-13/}],
  },
  city: {
    env: 'FREETRAIN_PLAYABLE_CITY_OUTPUT',
    producers: [
      'Playable_city_browser_seed_is_empty_source_clock_without_producers_or_completion_shortcut',
    ],
    suffixes: [''],
    consumers: [{file:'city-building.spec.ts',title:/P7-09|P7-10|P7-11|P7-12/}],
  },
  controllers: {
    env: 'FREETRAIN_CONTROLLER_FLEET_OUTPUT',
    producers: ['Manager_browser_input_is_source_built_deployed_stock'],
    suffixes: [''],
    consumers: [{
      file: 'fleet-controller-manager.spec.ts',
      title:
        /shared controller management|controller drafts detect|signal inspector|canonical train panels/,
    }, { file: 'fleet-groups.spec.ts', title: /MAN-A17|group drafts fence/ }],
  },
  tat: {
    env: 'FREETRAIN_TAT_FLEET_OUTPUT',
    producers: ['TAT_junction_manager_patches_copy_cleanup_geometry_fences_and_reopen_are_atomic'],
    suffixes: [''],
    consumers: [{
      file: 'fleet-controller-manager.spec.ts',
      title: /basic TAT|advanced ordered rules/,
    }],
  },
  turning: {
    env: 'FREETRAIN_TURNING_FLEET_OUTPUT',
    producers: [
      'Turning_real_signal_composes_replacement_central_reverse_old_head_delay_and_one_callback',
    ],
    suffixes: [''],
    consumers: [{ file: 'fleet-controller-manager.spec.ts', title: /TURNING actions/ }],
  },
  rules: {
    env: 'FREETRAIN_RULE_FILE_FLEET_OUTPUT',
    producers: [
      'Rule_file_append_preserves_existing_order_bypasses_only_duplicate_guard_and_continues_after_save',
    ],
    suffixes: [''],
    consumers: ['fleet-rule-file.spec.ts'],
  },
  signals: {
    env: 'FREETRAIN_SIGNAL_FLEET_OUTPUT',
    producers: ['Signal_real_shared_harbor_has_distinct_waits_simple_pass_and_exact_reopen'],
    suffixes: ['-three.json', '-shunting.json'],
    consumers: ['signal-integration.spec.ts'],
  },
  bells: {
    env: 'FREETRAIN_BELL_FLEET_OUTPUT',
    producers: ['Generate_real_selected_bell_departure_fixtures'],
    suffixes: ['', '.departed.json', '.rejected.json'],
    consumers: ['departure-bells.spec.ts'],
  },
  fleetContinuation: {
    env: 'FREETRAIN_FLEET_CONTINUATION_OUTPUT',
    producers: ['P5_27_every_minute_reopen_preserves_exact_continuation'],
    suffixes: ['', '.expected'],
    consumers: ['fleet-continuation.spec.ts'],
  },
  operations: {
    env: 'FREETRAIN_OPERATIONS_FLEET_OUTPUT',
    producers: [
      'Equal_minute_shared_track_observes_registration_order_instead_of_train_identity',
      'Equal_minute_return_waits_or_enters_when_blocking_train_vacates_first_connected_candidate',
    ],
    suffixes: ['', '.return'],
    consumers: [{ file: 'fleet-deployment.spec.ts', title: /P5-15/ }],
  },
  outside: {
    env: 'FREETRAIN_OUTSIDE_FLEET_OUTPUT',
    producers: [
      'Gameplay_built_three_car_border_trip_exchanges_once_at50_returns_after0_and_reopens_at_every_boundary',
      'Exchange_earns_before_same_move_charge_and_preserves51_origin_or_suppresses_reload',
    ],
    suffixes: ['', '.50', '.101', '.charged'],
    consumers: [{ file: 'fleet-persistence.spec.ts', title: /P5-14/ }],
  },
  timing: {
    env: 'FREETRAIN_TIMING_FLEET_OUTPUT',
    producers: [
      'SCN014_actual_ring_moves_charge_1_5_9_13_17_with_exact_limit_times_and_controlled_receipt',
    ],
    suffixes: [''],
    consumers: [{ file: 'fleet-persistence.spec.ts', title: /P5-13/ }],
  },
  moving: {
    env: 'FREETRAIN_MOVING_FLEET_OUTPUT',
    producers: [
      'Purchased_and_deployed_stock_runs_gameplay_built_ring_through_simple_junction_and_reopen',
    ],
    suffixes: [''],
    consumers: [{ file: 'fleet-persistence.spec.ts', title: /production fleet transport/ }],
  },
  stationContinuation: {
    env: 'FREETRAIN_STATION_CONTINUATION_OUTPUT',
    producers: ['P6_25_callback_boundaries_reopen_and_checkpoint_match_every_canonical_successor'],
    suffixes: ['service', 'reverse', 'turning', 'midnight'].flatMap(
      (mode) => ['.' + mode, '.' + mode + '.expected'],
    ),
    consumers: ['station-continuation.spec.ts'],
  },
  yard: {
    env: 'FREETRAIN_YARD_FLEET_OUTPUT',
    producers: [
      'Production_Simple_unloads_waits_boards_and_moves_with_one_publication_and_exact_reopen',
    ],
    suffixes: ['', '.arrived.json', '.departed.json'],
    consumers: ['station-yard.spec.ts'],
  },
  mixed: {
    env: 'FREETRAIN_MIXED_NETWORK_OUTPUT',
    producers: ['Complete_network_preserves_every_delivered_component_and_callback_continuation'],
    suffixes: [''],
    consumers: ['mixed-continuation.spec.ts'],
  },
} satisfies Record<string, FixtureFamily>;

export type FixtureName = keyof typeof fixtureFamilies;
export type SelectedTest = { file: string; title: string };

/** Editor/test-server runs select cases over RPC rather than command arguments.
 * Prepare all possible consumers in their configured projects in that case. */
export function configuredFixtures(projects: {
  testDir: string;
  testMatch: string | RegExp | (string | RegExp)[];
  testIgnore: string | RegExp | (string | RegExp)[];
}[]): FixtureName[] {
  return (Object.keys(fixtureFamilies) as FixtureName[]).filter((name) => {
    const family: FixtureFamily = fixtureFamilies[name];
    return family.consumers.some((consumer) => {
      const file = typeof consumer === 'string' ? consumer : consumer.file;
      return projects.some((project) => {
        const fullPath = resolve(project.testDir, file);
        const matches = (pattern: string | RegExp) =>
          typeof pattern === 'string'
            ? matchesGlob(file, pattern) || matchesGlob(fullPath, pattern)
            : pattern.test(fullPath);
        const include = Array.isArray(project.testMatch) ? project.testMatch : [project.testMatch];
        const exclude = Array.isArray(project.testIgnore)
          ? project.testIgnore
          : [project.testIgnore];
        return include.some(matches) && !exclude.some(matches);
      });
    });
  });
}

export function selectedFixtures(tests: SelectedTest[]): FixtureName[] {
  return (Object.keys(fixtureFamilies) as FixtureName[]).filter((name) => {
    const family: FixtureFamily = fixtureFamilies[name];
    return tests.some((test) =>
      family.consumers.some((consumer) =>
        typeof consumer === 'string'
          ? basename(test.file) === consumer
          : basename(test.file) === consumer.file && consumer.title.test(test.title)
      )
    );
  });
}

export function validateFixture(name: FixtureName, path: string) {
  const family = fixtureFamilies[name];
  for (const suffix of family.suffixes) {
    try {
      const file = statSync(path + suffix);
      if (!file.isFile() || file.size === 0) throw new Error('not a nonempty file');
    } catch {
      throw new Error(
        `Missing ${name} fixture ${path + suffix}; producer: ${family.producers.join(', ')}`,
      );
    }
  }
}

/** Resolve at test execution time: discovery runs before global setup. */
export function browserFixture(name: FixtureName): string {
  const family = fixtureFamilies[name], path = process.env[family.env];
  if (!path) {
    throw new Error(
      `Missing ${name} fixture setup; producer: ${
        family.producers.join(', ')
      }. Run deno task test:browser:run.`,
    );
  }
  validateFixture(name, path);
  return path;
}
