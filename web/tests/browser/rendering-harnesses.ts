import { execFileSync } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const entries = {
  train: ['train-rendering.spec.ts', 'train-information.spec.ts'],
  passage: ['passage.spec.ts'],
  stair: ['passage.spec.ts'],
  city: ['city-rendering.spec.ts'],
  station: ['station-rendering.spec.ts'],
} as const;
export type RenderingHarness = keyof typeof entries;

/** Build immutable inputs before workers start; never cache across runs. */
export function prepareRenderingHarnesses(directory: string, files?: string[]) {
  for (const [name, consumers] of Object.entries(entries)) {
    if (files && !files.some(file => consumers.some(spec => file.endsWith(spec)))) continue;
    const code = execFileSync('deno', ['run', '-A', 'scripts/bundle-rendering-tests.ts',
      `tests/helpers/${name}-rendering-harness.ts`], { encoding: 'utf8', maxBuffer: 16 * 1024 * 1024 });
    if (!code.trim()) throw new Error(`Empty rendering harness: ${name}`);
    writeFileSync(join(directory, `${name}.js`), code);
    console.log(`Prepared rendering harness: ${name}`);
  }
}

export function renderingHarness(name: RenderingHarness = 'train'): string {
  const directory = process.env.FREETRAIN_BROWSER_HARNESSES;
  if (!directory) throw new Error('Rendering harness preflight did not run');
  return readFileSync(join(directory, `${name}.js`), 'utf8');
}
