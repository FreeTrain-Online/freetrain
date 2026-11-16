import {
  appendFileSync,
  existsSync,
  mkdirSync,
  readdirSync,
  readFileSync,
  statSync,
  writeFileSync,
} from 'node:fs';
import { join, resolve } from 'node:path';
import type { TestInfo } from '@playwright/test';

export function processEvidenceDirectory(workerIndex: number) {
  return resolve(
    process.env.FREETRAIN_BROWSER_ARTIFACT_ROOT ?? '../tmp/browser-artifacts',
    'processes',
    `worker-${workerIndex}`,
  );
}
export class ProcessEvidence {
  private sequence = 0;
  constructor(readonly directory: string) {
    mkdirSync(directory, { recursive: true });
  }
  start(label: string, command: string) {
    const prefix = `${String(++this.sequence).padStart(3, '0')}-${label}`;
    const stdout = join(this.directory, prefix + '.stdout.log'),
      stderr = join(this.directory, prefix + '.stderr.log');
    writeFileSync(stdout, '');
    writeFileSync(stderr, '');
    const event = (kind: string, details: object = {}) =>
      appendFileSync(
        join(this.directory, 'lifecycle.ndjson'),
        JSON.stringify({
          time: new Date().toISOString(),
          process: prefix,
          kind,
          ...details,
        }) + '\n',
      );
    event('start', { command, stdout, stderr });
    return { stdout, stderr, event };
  }
}
export function processLogOffsets(directory: string): Map<string, number> {
  return new Map(
    (existsSync(directory) ? readdirSync(directory) : []).map(
      (name) => [name, statSync(join(directory, name)).size],
    ),
  );
}
/** Copies per-test slices; run-owned originals also include worker teardown. */
export async function attachProcessEvidence(
  info: TestInfo,
  directory: string,
  offsets: Map<string, number>,
) {
  const files = existsSync(directory) ? readdirSync(directory) : [];
  const active = new Set<string>();
  const lifecycle = join(directory, 'lifecycle.ndjson');
  if (existsSync(lifecycle)) {
    for (
      const line of readFileSync(lifecycle, 'utf8').trim().split('\n').filter(
        Boolean,
      )
    ) {
      const event = JSON.parse(line);
      if (event.kind === 'start') active.add(event.process);
      if (event.kind === 'exit' || event.kind === 'startup-failed') {
        active.delete(event.process);
      }
    }
  }
  const manifest: object[] = [];
  for (const name of files) {
    const source = join(directory, name),
      bytes = readFileSync(source),
      start = offsets.get(name) ?? 0;
    if (
      name !== 'lifecycle.ndjson' && offsets.has(name) &&
      bytes.length === start &&
      !active.has(name.replace(/\.(stdout|stderr)\.log$/, ''))
    ) continue;
    const path = info.outputPath('processes', name);
    mkdirSync(info.outputPath('processes'), { recursive: true });
    writeFileSync(path, bytes.subarray(start));
    await info.attach(`process-${name}`, { path, contentType: 'text/plain' });
    manifest.push({ source, path, start, end: bytes.length });
  }
  const path = info.outputPath('processes', 'association.json');
  mkdirSync(info.outputPath('processes'), { recursive: true });
  writeFileSync(
    path,
    JSON.stringify(
      {
        testId: info.testId,
        title: info.titlePath,
        workerIndex: info.workerIndex,
        directory,
        files: manifest,
      },
      null,
      2,
    ),
  );
  await info.attach('process-association', {
    path,
    contentType: 'application/json',
  });
}
