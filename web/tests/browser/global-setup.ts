import { execFileSync } from 'node:child_process';
import { copyFileSync, mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
import type { FullConfig } from '@playwright/test';

/** Generate current-format saves through the production C# scenarios. Never
 * depend on a developer's previous experiments in the ignored tmp directory. */
export default function setup(config: FullConfig) {
  if (!config.projects.some(project => project.name === 'canonical')) return;
  const root = resolve(__dirname, '../../..');
  const output = resolve(root, 'tmp');
  mkdirSync(output, { recursive: true });
  const configuration = process.env.FREETRAIN_ONLINE_TEST_SERVER_CONFIGURATION ?? 'Release';
  if (!['Debug', 'Release'].includes(configuration)) throw new Error('Invalid test server configuration');
  execFileSync('dotnet', ['test', resolve(root, 'tests/FreeTrain.Online.Tests'),
    '-c', configuration, '-p:RestoreLockedMode=true', '--filter', [
      'Manager_browser_input_is_source_built_deployed_stock',
      'TAT_junction_manager_patches_copy_cleanup_geometry_fences_and_reopen_are_atomic',
      'Turning_real_signal_composes_replacement_central_reverse_old_head_delay_and_one_callback',
      'Rule_file_append_preserves_existing_order_bypasses_only_duplicate_guard_and_continues_after_save',
      'Signal_real_shared_harbor',
    ].map(name => `FullyQualifiedName~${name}`).join('|')], {
    cwd: root, stdio: 'inherit', env: { ...process.env,
      FREETRAIN_CONTROLLER_FLEET_OUTPUT: process.env.FREETRAIN_CONTROLLER_FLEET_OUTPUT ?? resolve(output, 'p5-18-manager-fleet.json'),
      FREETRAIN_TAT_FLEET_OUTPUT: resolve(output, 'p5-19-tat-fleet.json'),
      FREETRAIN_TURNING_FLEET_OUTPUT: resolve(output, 'p5-21-turning-fleet.json'),
      FREETRAIN_RULE_FILE_FLEET_OUTPUT: resolve(output, 'p5-23-rule-fleet.json'),
      FREETRAIN_SIGNAL_FLEET_OUTPUT: resolve(output, 'p5-24-signal-fleet'),
    },
  });
  copyFileSync(resolve(output, 'p5-19-tat-fleet.json'), resolve(output, 'p5-20-tat-fleet.json'));
}
