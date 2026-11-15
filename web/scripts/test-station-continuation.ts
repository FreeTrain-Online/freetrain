// Shared browser preflight generates and validates the selected continuation inputs.
const result = await new Deno.Command('deno', {
  args: [
    'task',
    'test:browser:run',
    'station-continuation.spec.ts',
    '--project=canonical',
    ...Deno.args,
  ],
  stdin: 'inherit',
  stdout: 'inherit',
  stderr: 'inherit',
}).output();
Deno.exit(result.code);
