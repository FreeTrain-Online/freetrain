// Run from web/. Every process-restart case requires a fresh domain oracle.
const directory = await Deno.makeTempDir({ prefix: 'freetrain-station-continuation-' });
const fixture = `${directory}/station.json`;
const env = { FREETRAIN_STATION_CONTINUATION_OUTPUT: fixture };
async function run(command: string, args: string[]) {
  const result = await new Deno.Command(command, { args, env, stdin: 'inherit', stdout: 'inherit', stderr: 'inherit' }).output();
  if (!result.success) throw new Error(`${command} failed (${result.code})`);
}
try {
  await run('dotnet', ['test', '../tests/FreeTrain.Online.Tests', '-c', 'Release', '--no-restore',
    '-m:1', '-p:UseSharedCompilation=false', '--filter', 'FullyQualifiedName~P6_25']);
  for (const mode of ['service', 'reverse', 'turning', 'midnight'])
    for (const suffix of ['', '.expected'])
      if (!(await Deno.stat(`${fixture}.${mode}${suffix}`)).isFile) throw new Error(`Missing ${mode}${suffix} oracle`);
  await run('deno', ['task', 'test:browser:run', 'station-continuation.spec.ts', '--project=canonical', ...Deno.args]);
} finally { await Deno.remove(directory, { recursive: true }); }
