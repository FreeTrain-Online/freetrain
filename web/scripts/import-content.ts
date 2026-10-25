const result = await new Deno.Command('python3', { args: ['../scripts/prepare-bundled-content.py', ...Deno.args], stdout: 'inherit', stderr: 'inherit' }).output();
if (!result.success) Deno.exit(result.code);
