import { spawn } from 'node:child_process';
import { appendFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { ProcessEvidence } from '../tests/browser/process-evidence.ts';
const [label, separator, command, ...args] = Deno.args;
if (separator !== '--' || !command) throw new Error('Expected label -- command args');
const evidence = new ProcessEvidence(join(resolve(Deno.env.get('FREETRAIN_BROWSER_ARTIFACT_ROOT')!), 'processes', 'configuration'));
const logs = evidence.start(label, command);
const child = spawn(command, args, { stdio: ['ignore', 'pipe', 'pipe'] });
child.stdout.on('data', data => appendFileSync(logs.stdout, data));
child.stderr.on('data', data => appendFileSync(logs.stderr, data));
child.on('error', error => { logs.event('startup-failed', { error: String(error) }); process.exitCode = 1; });
for (const signal of ['SIGTERM', 'SIGINT'] as const) process.on(signal, () => child.kill(signal));
child.on('close', (code, signal) => {
  logs.event('exit', { code, signal });
  process.removeAllListeners('SIGTERM'); process.removeAllListeners('SIGINT');
  process.exitCode = code ?? (signal ? 1 : 0);
});
