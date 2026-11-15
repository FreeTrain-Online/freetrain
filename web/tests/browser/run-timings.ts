import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

/** Separate files avoid cross-worker writes; all timings use monotonic clocks. */
export function writeTimings(name: string, data: unknown) {
  const root = process.env.FREETRAIN_BROWSER_ARTIFACT_ROOT;
  if (!root) return;
  mkdirSync(join(root, 'timings'), { recursive: true });
  writeFileSync(
    join(root, 'timings', `${name}.json`),
    JSON.stringify(data, null, 2) + '\n',
  );
}
