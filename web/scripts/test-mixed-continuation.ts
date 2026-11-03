// Generate the gameplay-built fixture and require both browser continuations.
// A missing fixture is an error, never a silently skipped acceptance run.
const directory = await Deno.makeTempDir({ prefix: 'freetrain-mixed-continuation-' });
const fixture = `${directory}/network.json`;
const env = { FREETRAIN_MIXED_NETWORK_OUTPUT: fixture };
async function run(command: string, args: string[]) {
  const result = await new Deno.Command(command, { args, env, stdin: 'inherit', stdout: 'inherit', stderr: 'inherit' }).output();
  if (!result.success) throw new Error(`${command} failed (${result.code})`);
}
try {
  await run('dotnet', ['test', '../tests/FreeTrain.Online.Tests', '-c', 'Release', '--no-build', '--no-restore',
    '--filter', 'FullyQualifiedName~MixedContinuationTests']);
  if (!(await Deno.stat(fixture)).isFile) throw new Error('Mixed continuation fixture was not generated');
  await run('deno', ['run', '-A', 'npm:@playwright/test@1.64.1', 'test', '--config', 'playwright.continuation.config.ts', ...Deno.args]);
} finally { await Deno.remove(directory, { recursive: true }); }
