import { execFileSync } from 'node:child_process';
import { existsSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { createRequire } from 'node:module';
import type * as Runtime from './session-observation.ts';

/** Playwright's CommonJS transform under Deno does not preserve production JSON
 * import attributes. Bundle the unchanged production decoder with the pinned
 * Vite toolchain once per run, before discovery; do not copy its implementation.
 * Deno unit tests import session-observation.ts directly. */
export function prepareSessionRuntime(root: string) {
  const path = join(root, 'session-observer.cjs');
  if (!existsSync(path)) {
    const code = execFileSync('deno', ['run', '-A', 'scripts/bundle-rendering-tests.ts', 'tests/browser/session-observation.ts', 'cjs'], { encoding: 'utf8', maxBuffer: 32 * 1024 * 1024 });
    if (!code.trim()) throw new Error('Empty session observation runtime');
    writeFileSync(path, code);
  }
  process.env.FREETRAIN_BROWSER_SESSION_RUNTIME = path;
}
let runtime: typeof Runtime | undefined;
function load(): typeof Runtime {
  const path = process.env.FREETRAIN_BROWSER_SESSION_RUNTIME;
  if (!path) throw new Error('Session observer runtime must be prepared by the browser configuration');
  return runtime ??= createRequire(resolve('deno.json'))(path) as typeof Runtime;
}
export const createObservation = (implicitConnections = false): Runtime.SessionObservation => new (load().SessionObservation)(implicitConnections);
export const canonicalJSON: typeof Runtime.canonicalJSON = state => load().canonicalJSON(state);
export const parseMessageValue: typeof Runtime.parseMessageValue = value => load().parseMessageValue(value);
export const reached: typeof Runtime.reached = (actual, target) => load().reached(actual, target);
